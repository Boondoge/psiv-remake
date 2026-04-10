using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls ONE CharTile in the party strip.
/// </summary>
public class PartyStripEntry : MonoBehaviour
{
    [Header("Portrait Root (CharTile/CharacterImage)")]
    [Tooltip("Drag the CharacterImage root GameObject here. If left empty, we fall back to portraitImage.gameObject.")]
    [SerializeField] private GameObject portraitRoot;

    [Header("Portrait Image (optional, for shader flash)")]
    [SerializeField] private Image portraitImage;

    [Header("Name & Class")]
    [SerializeField] private TextMeshProUGUI nameText;

    [SerializeField] private TextMeshProUGUI classBadgeText;
    [SerializeField] private Image classBadgeBackground;

    [Header("HP / TP (value texts only; labels are static in UI)")]
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private TextMeshProUGUI tpValueText;

    [Header("Status Icon")]
    [SerializeField] private Image statusIcon;

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color activeHeroTextColor = Color.yellow;
    [SerializeField] private Color koTextColor = Color.gray;

    [Header("Status Icon Colors")]
    [SerializeField] private Color emptyStatusColor = Color.black;

    // ShaderGraph portrait materials (optional)
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private Material _portraitMatInstance;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        // If portraitRoot not wired, infer it from portraitImage.
        if (portraitRoot == null && portraitImage != null)
            portraitRoot = portraitImage.gameObject;

        EnsurePortraitMaterialInstanceIfNeeded();
        SetPortraitFlashAmount(0f);
    }

    private void OnDisable()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = null;
        SetPortraitFlashAmount(0f);
    }

    private void EnsurePortraitMaterialInstanceIfNeeded()
    {
        if (portraitImage == null) return;
        if (portraitImage.material == null) return;

        if (!portraitImage.material.HasProperty(FlashAmountId)) return;

        if (_portraitMatInstance == null)
        {
            _portraitMatInstance = new Material(portraitImage.material);
            portraitImage.material = _portraitMatInstance;
        }
    }

    private void SetPortraitFlashAmount(float v)
    {
        if (_portraitMatInstance == null) return;
        if (!_portraitMatInstance.HasProperty(FlashAmountId)) return;
        _portraitMatInstance.SetFloat(FlashAmountId, v);
    }

    public void SetPortraitVisible(bool visible)
    {
        if (portraitRoot != null)
        {
            portraitRoot.SetActive(visible);
            return;
        }

        if (portraitImage != null)
            portraitImage.enabled = visible;
    }

    /// <summary>
    /// Clears this tile to represent an empty party slot.
    /// </summary>
    public void SetEmpty()
    {
        if (nameText != null)
        {
            nameText.text = string.Empty;
            nameText.color = normalTextColor;
        }

        if (hpValueText != null)
        {
            hpValueText.text = "--";
            hpValueText.color = normalTextColor;
        }

        if (tpValueText != null)
        {
            tpValueText.text = "--";
            tpValueText.color = normalTextColor;
        }

        SetClassBadge(string.Empty, Color.clear, Color.clear);

        if (statusIcon != null)
        {
            statusIcon.sprite = null;
            statusIcon.color = emptyStatusColor;
            statusIcon.enabled = true;
        }

        // Critical: empty slots must not show any portrait content
        SetPortraitVisible(false);

        SetPortraitFlashAmount(0f);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Populates this tile to represent a real combatant and their current state.
    /// </summary>
    public void SetFromCombatant(
        BattleCombatant combatant,
        bool isActiveHero,
        bool showPortrait,
        Sprite statusSprite,
        string classCode,
        Color classBadgeColor,
        Color classTextColor)
    {
        if (combatant == null)
        {
            SetEmpty();
            return;
        }

        gameObject.SetActive(true);

        if (nameText != null)
            nameText.text = combatant.name;

        int maxHP = Mathf.Max(1, combatant.maxHP);
        int maxTP = Mathf.Max(0, combatant.maxTP);

        int clampedHP = Mathf.Clamp(combatant.hp, 0, maxHP);
        int clampedTP = Mathf.Clamp(combatant.tp, 0, maxTP);

        if (hpValueText != null)
            hpValueText.text = $"{clampedHP}/{maxHP}";

        if (tpValueText != null)
            tpValueText.text = $"{clampedTP}/{maxTP}";

        bool isKo = combatant.hp <= 0;

        Color textColor = isKo ? koTextColor : (isActiveHero ? activeHeroTextColor : normalTextColor);

        if (nameText != null) nameText.color = textColor;
        if (hpValueText != null) hpValueText.color = textColor;
        if (tpValueText != null) tpValueText.color = textColor;

        SetClassBadge(classCode, classBadgeColor, classTextColor);

        if (statusIcon != null)
        {
            statusIcon.enabled = true;

            if (statusSprite != null)
            {
                statusIcon.sprite = statusSprite;
                statusIcon.color = Color.white;
            }
            else
            {
                statusIcon.sprite = null;
                statusIcon.color = emptyStatusColor;
            }
        }

        SetPortraitVisible(showPortrait);
    }

    /// <summary>
    /// PSIV-ish “portrait hit flash”.
    /// If portrait material has _FlashAmount, uses that. Else toggles portraitRoot active.
    /// </summary>
    public void PlayPortraitHitFlash(int pulses, float onSeconds, float offSeconds)
    {
        // If portrait is hidden, still allow flash to “show” it briefly
        if (portraitRoot == null && portraitImage != null)
            portraitRoot = portraitImage.gameObject;

        EnsurePortraitMaterialInstanceIfNeeded();

        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoPortraitFlash(pulses, onSeconds, offSeconds));
    }

    private IEnumerator CoPortraitFlash(int pulses, float onSeconds, float offSeconds)
    {
        int n = Mathf.Max(0, pulses);
        float onT = Mathf.Max(0f, onSeconds);
        float offT = Mathf.Max(0f, offSeconds);

        bool canShaderFlash = (_portraitMatInstance != null && _portraitMatInstance.HasProperty(FlashAmountId));

        // Ensure visible for the flash sequence
        if (portraitRoot != null) portraitRoot.SetActive(true);

        for (int i = 0; i < n; i++)
        {
            if (canShaderFlash)
            {
                SetPortraitFlashAmount(1f);
            }
            else
            {
                if (portraitRoot != null) portraitRoot.SetActive(false);
            }

            if (onT > 0f) yield return new WaitForSeconds(onT);

            if (canShaderFlash)
            {
                SetPortraitFlashAmount(0f);
            }
            else
            {
                if (portraitRoot != null) portraitRoot.SetActive(true);
            }

            if (offT > 0f) yield return new WaitForSeconds(offT);
        }

        if (canShaderFlash) SetPortraitFlashAmount(0f);
        _flashRoutine = null;
    }

    private void SetClassBadge(string classCode, Color badgeColor, Color textColor)
    {
        bool hasCode = !string.IsNullOrEmpty(classCode);

        if (classBadgeText != null)
        {
            classBadgeText.text = hasCode ? classCode : string.Empty;
            classBadgeText.color = textColor;
        }

        if (classBadgeBackground != null)
        {
            classBadgeBackground.color = badgeColor;
            classBadgeBackground.enabled = hasCode;
        }
    }
}
