using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    // ============================================================
    // Status / log UI
    // ============================================================

    [Header("Status Text")]
    public TextMeshProUGUI playerStatusText;
    public TextMeshProUGUI enemyStatusText;
    public TextMeshProUGUI logText;

    [Header("Screen Flash (use sparingly)")]
    public Image damageFlashImage;
    public float flashMaxAlpha = 0.5f;
    public float flashFadeDuration = 0.25f;
    private Coroutine flashRoutine;

    // ============================================================
    // Menu-based command UI
    // ============================================================

    private enum MenuLayer { Main, Command }

    [Header("Main Battle Menu (COMD / MACR / RUN)")]
    public GameObject battleMenuPopup;
    public RectTransform mainMenuSelector;
    public List<RectTransform> mainMenuSlots;

    [Header("COMD Command Menu (Attack / Tech / Skill / Item / Defend)")]
    public GameObject comdMenuPopup;
    public RectTransform commandMenuSelector;
    public List<RectTransform> commandSlots;

    [Header("Party Strip")]
    [SerializeField] private PartyStripController partyStripController;

    [Header("Command Icons for Party Strip")]
    [SerializeField] private Image attackCommandIcon;
    [SerializeField] private Image techCommandIcon;
    [SerializeField] private Image skillCommandIcon;
    [SerializeField] private Image itemCommandIcon;
    [SerializeField] private Image defendCommandIcon;

    [Header("Menu Input")]
    public KeyCode confirmKey = KeyCode.Space;
    public KeyCode cancelKey = KeyCode.Escape;

    private MenuLayer currentLayer = MenuLayer.Main;
    private int mainIndex = 0;
    private int commandIndex = 0;
    private bool _stickToComdThisRound = false;

    // ============================================================
    // Selection / target state
    // ============================================================

    [Header("Selection Colors (for text lists)")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;

    private int heroCount = 0;
    private int currentHeroIndex = 0;

    private int enemyCount = 0;
    private int currentEnemyIndex = 0;

    private bool inTargetSelection = false;

    private List<BattleCombatant> _lastPlayers;
    private List<BattleCombatant> _lastEnemies;

    private List<Sprite> heroCommandIconsThisRound = new List<Sprite>();

    // Tracks which portrait visibility mode the strip should use.
    // _actionPhaseActive takes priority: show none (-2).
    // _commandPhaseActive (and not action): show only active hero (>= 0).
    // Neither: battle start, show all (-1).
    private bool _commandPhaseActive = false;
    private bool _actionPhaseActive = false;

    public int CurrentEnemyIndex
    {
        get
        {
            if (enemyCount <= 0) return 0;
            if (currentEnemyIndex < 0) currentEnemyIndex = 0;
            if (currentEnemyIndex >= enemyCount) currentEnemyIndex = enemyCount - 1;
            return currentEnemyIndex;
        }
    }

    // ============================================================
    // Party portrait hit reaction (enemy -> hero)
    // ============================================================

    [Header("Party Portrait Hit Reaction")]
    [Tooltip("How many pulses for party portrait hit flash.")]
    public int partyPortraitBlinkCount = 4;

    [Tooltip("Seconds portrait stays flashed each pulse.")]
    public float partyPortraitBlinkOnSeconds = 0.05f;

    [Tooltip("Seconds portrait returns to normal between pulses.")]
    public float partyPortraitBlinkOffSeconds = 0.05f;

    public float GetPartyPortraitFlashDuration()
    {
        int n = Mathf.Max(0, partyPortraitBlinkCount);
        float onT = Mathf.Max(0f, partyPortraitBlinkOnSeconds);
        float offT = Mathf.Max(0f, partyPortraitBlinkOffSeconds);
        return n * (onT + offT);
    }

    /// <summary>
    /// Enemy -> hero impact sequence, PSIV-style:
    /// enemy attack animation (handled by Enemy AI) -> portrait flash -> THEN message / UI refresh.
    /// </summary>
    public IEnumerator PlayEnemyHitSequenceOnParty(string enemyName, int targetPartyIndex, string targetName, int finalDamage)
    {
        // Flash the party portrait (if we can)
        if (partyStripController != null)
        {
            partyStripController.TryPlayPortraitHitFlash(
                targetPartyIndex,
                partyPortraitBlinkCount,
                partyPortraitBlinkOnSeconds,
                partyPortraitBlinkOffSeconds
            );
        }

        float t = GetPartyPortraitFlashDuration();
        if (t > 0f) yield return new WaitForSeconds(t);

        // After flash completes, show message (damage numbers later if you add popups)
        ShowMessage($"{enemyName} hits {targetName} for {finalDamage} damage!");

        // Refresh status after the flash so it “appears” after reaction
        RefreshStatus(_lastPlayers, _lastEnemies);
    }

    // ============================================================
    // Visual catalog (Resources-loaded)
    // ============================================================

    private BattleActorVisualCatalog _visualCatalog;

    private void EnsureVisualCatalogLoaded()
    {
        if (_visualCatalog != null) return;

        var all = Resources.LoadAll<BattleActorVisualCatalog>("");
        if (all != null && all.Length > 0) _visualCatalog = all[0];

        if (_visualCatalog == null)
            Debug.LogWarning("[BattleUI] No BattleActorVisualCatalog found in Resources. Visuals will be skipped.");
    }

    // ============================================================
    // Selector blink / pulse
    // ============================================================

    [Header("Selector Blink / Pulse")]
    [SerializeField] private float mainSelectorBlinkInterval = 0.5f;
    [SerializeField] private float commandSelectorBlinkSpeed = 2.0f;
    [SerializeField] private float commandSelectorPulseIntensity = 0.5f;

    private Image mainMenuSelectorImage;
    private Image commandMenuSelectorImage;
    private Color mainSelectorBaseColor;
    private Color commandSelectorBaseColor;
    private Color commandSelectorPulseColor;

    private float mainSelectorBlinkTimer = 0f;
    private bool mainSelectorVisible = true;

    // ============================================================
    // Unity lifecycle
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BattleUI] Duplicate instance on {gameObject.name}, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainMenuSelector != null)
        {
            mainMenuSelectorImage = mainMenuSelector.GetComponent<Image>();
            if (mainMenuSelectorImage != null) mainSelectorBaseColor = mainMenuSelectorImage.color;
        }

        if (commandMenuSelector != null)
        {
            commandMenuSelectorImage = commandMenuSelector.GetComponent<Image>();
            if (commandMenuSelectorImage != null)
            {
                commandSelectorBaseColor = commandMenuSelectorImage.color;

                float factor = 1f + Mathf.Max(0f, commandSelectorPulseIntensity);
                commandSelectorPulseColor = commandSelectorBaseColor * factor;
                commandSelectorPulseColor.a = commandSelectorBaseColor.a;
            }
        }
    }

    private void OnEnable()
    {
        currentHeroIndex = 0;
        currentEnemyIndex = 0;
        heroCount = 0;
        enemyCount = 0;

        inTargetSelection = false;
        _commandPhaseActive = false;
        _actionPhaseActive = false; // battle start shows ALL portraits

        currentLayer = MenuLayer.Main;
        mainIndex = 0;
        commandIndex = 0;
        _stickToComdThisRound = false;

        heroCommandIconsThisRound.Clear();

        if (battleMenuPopup != null) battleMenuPopup.SetActive(true);
        if (comdMenuPopup != null) comdMenuPopup.SetActive(false);

        ResetMainSelectorBlink();
        UpdateMainSelector();

        if (partyStripController != null)
            partyStripController.InitializeEmpty();

        if (commandMenuSelectorImage != null)
            commandMenuSelectorImage.color = commandSelectorBaseColor;
    }

    private void Update()
    {
        if (GameModeManager.Instance == null || !GameModeManager.Instance.IsBattle)
            return;

        if (inTargetSelection) HandleTargetSelectionInput();
        else HandleMenuInput();

        UpdateSelectorBlink();
    }

    // ============================================================
    // Public API called by BattleManager
    // ============================================================

    public void BeginHeroTurn(int heroIndex, int totalHeroes,
                              List<BattleCombatant> players,
                              List<BattleCombatant> enemies)
    {
        _commandPhaseActive = true;
        _actionPhaseActive = false;

        heroCount = totalHeroes;
        currentHeroIndex = Mathf.Clamp(heroIndex, 0, Mathf.Max(0, heroCount - 1));

        inTargetSelection = false;

        if (heroIndex == 0)
        {
            _stickToComdThisRound = false;
            heroCommandIconsThisRound.Clear();
        }

        mainIndex = 0;
        commandIndex = 0;

        if (_stickToComdThisRound)
        {
            currentLayer = MenuLayer.Command;

            if (battleMenuPopup != null) battleMenuPopup.SetActive(false);
            if (comdMenuPopup != null) comdMenuPopup.SetActive(true);

            UpdateCommandSelector();
        }
        else
        {
            currentLayer = MenuLayer.Main;

            if (battleMenuPopup != null) battleMenuPopup.SetActive(true);
            if (comdMenuPopup != null) comdMenuPopup.SetActive(false);

            ResetMainSelectorBlink();
            UpdateMainSelector();
        }

        RefreshStatus(players, enemies);
    }

    public void BeginTargetSelection(List<BattleCombatant> players,
                                     List<BattleCombatant> enemies,
                                     int heroIndex)
    {
        _commandPhaseActive = true;

        inTargetSelection = true;
        _lastPlayers = players;
        _lastEnemies = enemies;

        currentHeroIndex = Mathf.Clamp(heroIndex, 0, Mathf.Max(0, players.Count - 1));

        if (battleMenuPopup != null) battleMenuPopup.SetActive(false);
        if (comdMenuPopup != null) comdMenuPopup.SetActive(false);

        RefreshStatus(players, enemies);
    }

    public void ReturnToCommandSelection()
    {
        _commandPhaseActive = true;

        inTargetSelection = false;

        currentLayer = MenuLayer.Command;

        if (battleMenuPopup != null) battleMenuPopup.SetActive(false);
        if (comdMenuPopup != null) comdMenuPopup.SetActive(true);

        UpdateCommandSelector();
        RefreshStatus(_lastPlayers, _lastEnemies);
    }

    public void RefreshStatus(List<BattleCombatant> players, List<BattleCombatant> enemies)
    {
        _lastPlayers = players;
        _lastEnemies = enemies;

        if (playerStatusText != null)
        {
            if (players != null && players.Count > 0)
            {
                heroCount = players.Count;
                currentHeroIndex = Mathf.Clamp(currentHeroIndex, 0, heroCount - 1);

                var sb = new StringBuilder();
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    string marker = (i == currentHeroIndex) ? "> " : "  ";
                    sb.AppendLine($"{marker}{p.name}  HP: {p.hp}/{p.maxHP}");
                }
                playerStatusText.text = sb.ToString();
            }
            else
            {
                heroCount = 0;
                currentHeroIndex = 0;
                playerStatusText.text = "No players";
            }
        }

        if (enemyStatusText != null)
        {
            if (enemies != null && enemies.Count > 0)
            {
                enemyCount = enemies.Count;
                currentEnemyIndex = Mathf.Clamp(currentEnemyIndex, 0, enemyCount - 1);

                var sb = new StringBuilder();
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    string marker = (i == CurrentEnemyIndex) ? "> " : "  ";
                    sb.AppendLine($"{marker}{e.name}  HP: {e.hp}/{e.maxHP}");
                }
                enemyStatusText.text = sb.ToString();
            }
            else
            {
                enemyCount = 0;
                currentEnemyIndex = 0;
                enemyStatusText.text = "No enemies";
            }
        }

        // Bottom party strip
        if (partyStripController != null)
        {
            // -2 = action phase: show no portraits
            // -1 = battle start: show all portraits
            // >= 0 = command selection: show only active hero
            int activeHero;
            if (_actionPhaseActive)
                activeHero = -2;
            else if (_commandPhaseActive && players != null && players.Count > 0)
                activeHero = Mathf.Clamp(currentHeroIndex, 0, players.Count - 1);
            else
                activeHero = -1;

            IList<Sprite> commandIcons = null;
            if (players != null && players.Count > 0)
            {
                EnsureHeroCommandIconListSize(players.Count);
                commandIcons = heroCommandIconsThisRound;
            }

            partyStripController.RefreshStrip(
                players,
                activeHero,
                commandIcons,
                null, // Future classCodes
                null, // Future badgeColors
                null  // Future badgeTextColors
            );
        }
    }

    public void ShowMessage(string message)
    {
        if (logText != null) logText.text = message;
        Debug.Log("[BattleUI] " + message);
    }

    /// <summary>
    /// Reveals the targeted party member's portrait just before an enemy attack animation plays,
    /// so the player knows who is being targeted. The portrait then flashes on hit via PlayEnemyHitSequenceOnParty.
    /// </summary>
    public void ShowTargetedPartyPortrait(int partyIndex)
    {
        if (partyStripController != null)
            partyStripController.ShowPortraitForIndex(partyIndex);
    }

    public void HideMenusForActionPhase()
    {
        if (battleMenuPopup != null) battleMenuPopup.SetActive(false);
        if (comdMenuPopup != null) comdMenuPopup.SetActive(false);

        _commandPhaseActive = false;
        _actionPhaseActive = true; // action phase: hide all portraits
        inTargetSelection = false;
    }

    /// <summary>
    /// Player -> Enemy action visuals sequence (PSIV style).
    ///
    /// Flow per hit:
    ///   1. Spawn target VFX at enemy VfxAnchor (autoDestroy OFF so it persists through blink)
    ///   2. Wait for VFX clip duration
    ///   3. Enemy hit reaction (shader flash via HitReactionGroup)
    ///   4. Destroy VFX
    ///   5. Next hit (if multi-hit weapon like dual daggers)
    ///
    /// Caster UI prefab (optional) is spawned at the hero's CastAnchor for the
    /// duration of the entire sequence, then destroyed.
    ///
    /// This coroutine must complete BEFORE BattleManager shows damage numbers.
    /// </summary>
    public IEnumerator PlayActionSequence_PlayerVsEnemy(
        int attackerPartyIndex,
        BattleCombatant hero,
        PlayerCommandType command,
        BattleCombatant target)
    {
        EnsureVisualCatalogLoaded();

        if (_visualCatalog == null)
            yield break;

        // ---- Resolve characterId for catalog lookup ----
        string characterId = hero.characterId;

        if (string.IsNullOrWhiteSpace(characterId) &&
            PartyManager.Instance != null &&
            attackerPartyIndex >= 0 &&
            attackerPartyIndex < PartyManager.Instance.activeParty.Count)
        {
            characterId = PartyManager.Instance.activeParty[attackerPartyIndex].characterId;
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            Debug.LogWarning("[BattleUI] PlayActionSequence: missing characterId — skipping visuals.");
            yield break;
        }

        // ---- Map PlayerCommandType → VisualActionType ----
        var actionType = BattleActorVisualCatalog.VisualActionType.None;
        switch (command)
        {
            case PlayerCommandType.Attack: actionType = BattleActorVisualCatalog.VisualActionType.Attack; break;
            case PlayerCommandType.Defend: actionType = BattleActorVisualCatalog.VisualActionType.Defend; break;
            case PlayerCommandType.Flee:   actionType = BattleActorVisualCatalog.VisualActionType.Flee;   break;
        }

        if (actionType == BattleActorVisualCatalog.VisualActionType.None)
            yield break;

        // ---- Catalog lookup ----
        BattleActorVisualCatalog.Entry entry;
        if (!_visualCatalog.TryGet(characterId, actionType, hero.weaponStyle, null, out entry))
        {
            Debug.LogWarning($"[BattleUI] No catalog entry for '{characterId}' action={actionType} weapon={hero.weaponStyle}");
            yield break;
        }

        // ---- Spawn caster UI prefab at hero's CastAnchor (optional) ----
        GameObject casterInstance = null;

        if (entry.casterUiPrefab != null && partyStripController != null)
        {
            RectTransform castAnchor = partyStripController.GetCastAnchorForTileIndex(attackerPartyIndex);
            if (castAnchor != null)
            {
                casterInstance = Instantiate(entry.casterUiPrefab, castAnchor);
                casterInstance.transform.localPosition = Vector3.zero;
            }
        }

        // ---- Resolve enemy-side references ----
        BaseEnemyAI enemyAI = target.enemyRef;
        Transform vfxAnchor = null;
        SpriteRenderer enemyRenderer = null;
        HitReactionGroup hitReactionGroup = null;

        if (enemyAI != null)
        {
            vfxAnchor = enemyAI.GetVfxAnchor();
            enemyRenderer = enemyAI.GetComponent<SpriteRenderer>();
            hitReactionGroup = enemyAI.GetComponentInChildren<HitReactionGroup>();
        }

        // ---- Build ordered hit list (multi-hit sequence overrides single prefab) ----
        BattleVfxSprite[] hitPrefabs = null;

        if (entry.hitVfxSequence != null && entry.hitVfxSequence.Length > 0)
        {
            hitPrefabs = entry.hitVfxSequence;
        }
        else if (entry.targetVfxPrefab != null)
        {
            hitPrefabs = new BattleVfxSprite[] { entry.targetVfxPrefab };
        }

        // ---- Play each hit: VFX → wait → blink → destroy → next ----
        if (hitPrefabs != null && vfxAnchor != null)
        {
            for (int i = 0; i < hitPrefabs.Length; i++)
            {
                var prefab = hitPrefabs[i];
                if (prefab == null) continue;

                // Spawn with caller-controlled lifetime so VFX stays visible during blink
                var vfxInstance = Instantiate(prefab, vfxAnchor.position, Quaternion.identity);
                vfxInstance.autoDestroyAfterDuration = false;
                vfxInstance.Play(vfxAnchor, enemyRenderer, entry.targetOrderOffset);

                // Wait for the VFX animation clip to finish
                yield return new WaitForSeconds(vfxInstance.durationSeconds);

                // Enemy hit reaction (shader-based flash on all renderers)
                if (hitReactionGroup != null)
                {
                    yield return hitReactionGroup.PlayHitFlashRoutine(
                        entry.enemyBlinkCount,
                        entry.enemyBlinkOnSeconds,
                        entry.enemyBlinkOffSeconds);
                }

                // Safe to destroy the VFX now
                if (vfxInstance != null)
                    Destroy(vfxInstance.gameObject);
            }
        }
        else if (hitReactionGroup != null)
        {
            // No VFX prefabs configured, but still do the hit reaction blink
            yield return hitReactionGroup.PlayHitFlashRoutine(
                entry.enemyBlinkCount,
                entry.enemyBlinkOnSeconds,
                entry.enemyBlinkOffSeconds);
        }

        // ---- Clean up caster prefab ----
        if (casterInstance != null)
            Destroy(casterInstance);
    }


    // ============================================================
    // Input handling
    // ============================================================

    private void HandleMenuInput()
    {
        bool mainActive = (battleMenuPopup != null && battleMenuPopup.activeInHierarchy);
        bool comdActive = (comdMenuPopup != null && comdMenuPopup.activeInHierarchy);

        if (!mainActive && !comdActive) return;

        bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        if (currentLayer == MenuLayer.Main) HandleMainMenuNavigation(left, right);
        else HandleCommandMenuNavigation(left, right);

        if (Input.GetKeyDown(confirmKey) || Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Return))
            ConfirmSelection();

        if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(KeyCode.Backspace))
            CancelSelection();
    }

    private void HandleTargetSelectionInput()
    {
        if (enemyCount > 0)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveEnemySelection(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveEnemySelection(1);

            if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                BattleManager.Instance?.OnHeroTargetConfirmed(CurrentEnemyIndex);
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            BattleManager.Instance?.OnHeroTargetCancelled();
    }

    private void HandleMainMenuNavigation(bool left, bool right)
    {
        if (mainMenuSlots == null || mainMenuSlots.Count == 0) return;

        if (left)
        {
            mainIndex = (mainIndex - 1 + mainMenuSlots.Count) % mainMenuSlots.Count;
            UpdateMainSelector();
        }
        else if (right)
        {
            mainIndex = (mainIndex + 1) % mainMenuSlots.Count;
            UpdateMainSelector();
        }
    }

    private void HandleCommandMenuNavigation(bool left, bool right)
    {
        if (commandSlots == null || commandSlots.Count == 0) return;

        if (left)
        {
            commandIndex = (commandIndex - 1 + commandSlots.Count) % commandSlots.Count;
            UpdateCommandSelector();
        }
        else if (right)
        {
            commandIndex = (commandIndex + 1) % commandSlots.Count;
            UpdateCommandSelector();
        }
    }

    private void UpdateMainSelector()
    {
        if (mainMenuSelector == null) return;
        if (mainMenuSlots == null || mainMenuSlots.Count == 0) return;
        if (mainIndex < 0 || mainIndex >= mainMenuSlots.Count) return;

        RectTransform slot = mainMenuSlots[mainIndex];
        if (slot == null) return;

        RectTransform selectorParent = mainMenuSelector.parent as RectTransform;
        RectTransform slotParent = slot.parent as RectTransform;

        if (selectorParent != null && slotParent == selectorParent)
            mainMenuSelector.anchoredPosition = slot.anchoredPosition;
        else
            mainMenuSelector.position = slot.position;

        ResetMainSelectorBlink();
    }

    private void UpdateCommandSelector()
    {
        if (commandMenuSelector == null) return;
        if (commandSlots == null || commandSlots.Count == 0) return;
        if (commandIndex < 0 || commandIndex >= commandSlots.Count) return;

        RectTransform slot = commandSlots[commandIndex];
        if (slot == null) return;

        commandMenuSelector.SetParent(slot, false);
        commandMenuSelector.anchorMin = new Vector2(0.5f, 0.5f);
        commandMenuSelector.anchorMax = new Vector2(0.5f, 0.5f);
        commandMenuSelector.pivot = new Vector2(0.5f, 0.5f);
        commandMenuSelector.anchoredPosition = Vector2.zero;
    }

    private void ConfirmSelection()
    {
        if (currentLayer == MenuLayer.Main) ConfirmMainMenu();
        else ConfirmCommandMenu();
    }

    private void CancelSelection()
    {
        if (currentLayer == MenuLayer.Command)
        {
            currentLayer = MenuLayer.Main;

            if (comdMenuPopup != null) comdMenuPopup.SetActive(false);
            if (battleMenuPopup != null) battleMenuPopup.SetActive(true);

            ResetMainSelectorBlink();
            UpdateMainSelector();
        }
    }

    private void ConfirmMainMenu()
    {
        switch (mainIndex)
        {
            case 0:
                _stickToComdThisRound = true;
                OpenCommandMenu();
                break;
            case 1:
                Debug.Log("[BattleUI] MACR selected (not implemented).");
                break;
            case 2:
                Debug.Log("[BattleUI] RUN selected.");
                OnFleeButton();
                break;
        }
    }

    private void OpenCommandMenu()
    {
        currentLayer = MenuLayer.Command;
        commandIndex = 0;
        UpdateCommandSelector();

        if (battleMenuPopup != null) battleMenuPopup.SetActive(false);
        if (comdMenuPopup != null) comdMenuPopup.SetActive(true);
    }

    private void ConfirmCommandMenu()
    {
        switch (commandIndex)
        {
            case 0: OnAttackButton(); break;
            case 1: Debug.Log("[BattleUI] TECH (stub)."); break;
            case 2: Debug.Log("[BattleUI] SKILL (stub)."); break;
            case 3: Debug.Log("[BattleUI] ITEM (stub)."); break;
            case 4: OnDefendButton(); break;
        }
    }

    private void MoveEnemySelection(int delta)
    {
        if (enemyCount <= 0) return;

        currentEnemyIndex += delta;
        if (currentEnemyIndex < 0) currentEnemyIndex = enemyCount - 1;
        if (currentEnemyIndex >= enemyCount) currentEnemyIndex = 0;

        RefreshStatus(_lastPlayers, _lastEnemies);
    }

    // ============================================================
    // Button callbacks used by BattleManager
    // ============================================================

    public void OnAttackButton()
    {
        if (attackCommandIcon != null && attackCommandIcon.sprite != null)
        {
            SetHeroCommandIcon(currentHeroIndex, attackCommandIcon.sprite);
            RefreshStatus(_lastPlayers, _lastEnemies);
        }

        BattleManager.Instance?.OnHeroCommandSelected(PlayerCommandType.Attack);
    }

    public void OnDefendButton()
    {
        if (defendCommandIcon != null && defendCommandIcon.sprite != null)
        {
            SetHeroCommandIcon(currentHeroIndex, defendCommandIcon.sprite);
            RefreshStatus(_lastPlayers, _lastEnemies);
        }

        BattleManager.Instance?.OnHeroCommandSelected(PlayerCommandType.Defend);
    }

    public void OnFleeButton()
    {
        BattleManager.Instance?.OnHeroCommandSelected(PlayerCommandType.Flee);
    }

    // ============================================================
    // Screen flash (currently used by BattleManager; TODO: crit-only later)
    // ============================================================

    public void PlayDamageFlash()
    {
        if (damageFlashImage == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        Color c = damageFlashImage.color;
        c.a = flashMaxAlpha;
        damageFlashImage.color = c;

        float t = 0f;
        while (t < flashFadeDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(flashMaxAlpha, 0f, t / flashFadeDuration);
            damageFlashImage.color = c;
            yield return null;
        }

        c.a = 0f;
        damageFlashImage.color = c;
        flashRoutine = null;
    }

    // ============================================================
    // Helpers for per-hero command icons
    // ============================================================

    private void EnsureHeroCommandIconListSize(int count)
    {
        if (heroCommandIconsThisRound == null)
            heroCommandIconsThisRound = new List<Sprite>(count);

        if (heroCommandIconsThisRound.Count < count)
        {
            int toAdd = count - heroCommandIconsThisRound.Count;
            for (int i = 0; i < toAdd; i++) heroCommandIconsThisRound.Add(null);
        }
        else if (heroCommandIconsThisRound.Count > count)
        {
            heroCommandIconsThisRound.RemoveRange(count, heroCommandIconsThisRound.Count - count);
        }
    }

    private void SetHeroCommandIcon(int heroIndex, Sprite sprite)
    {
        if (heroIndex < 0) return;

        EnsureHeroCommandIconListSize(heroIndex + 1);
        heroCommandIconsThisRound[heroIndex] = sprite;
    }

    // ============================================================
    // Selector blink logic
    // ============================================================

    private void ResetMainSelectorBlink()
    {
        mainSelectorBlinkTimer = 0f;
        mainSelectorVisible = true;

        if (mainMenuSelectorImage != null)
            mainMenuSelectorImage.enabled = true;
    }

    private void UpdateSelectorBlink()
    {
        float dt = Time.unscaledDeltaTime;

        if (mainMenuSelectorImage != null &&
            battleMenuPopup != null && battleMenuPopup.activeInHierarchy &&
            currentLayer == MenuLayer.Main &&
            !inTargetSelection)
        {
            if (mainSelectorBlinkInterval > 0f)
            {
                mainSelectorBlinkTimer += dt;
                if (mainSelectorBlinkTimer >= mainSelectorBlinkInterval)
                {
                    mainSelectorBlinkTimer = 0f;
                    mainSelectorVisible = !mainSelectorVisible;
                }
                mainMenuSelectorImage.enabled = mainSelectorVisible;
            }
        }
        else if (mainMenuSelectorImage != null)
        {
            mainMenuSelectorImage.enabled = true;
        }

        if (commandMenuSelectorImage != null &&
            comdMenuPopup != null && comdMenuPopup.activeInHierarchy &&
            currentLayer == MenuLayer.Command &&
            !inTargetSelection)
        {
            if (commandSelectorBlinkSpeed > 0f && commandSelectorPulseIntensity > 0f)
            {
                float s = (Mathf.Sin(Time.unscaledTime * commandSelectorBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                commandMenuSelectorImage.color = Color.Lerp(commandSelectorBaseColor, commandSelectorPulseColor, s);
            }
        }
        else if (commandMenuSelectorImage != null)
        {
            commandMenuSelectorImage.color = commandSelectorBaseColor;
        }
    }
}