using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commands a player character can choose.
/// </summary>
public enum PlayerCommandType
{
    None,
    Attack,
    Defend,
    Flee
}

/// <summary>
/// Core turn-based battle manager.
/// Flow per round:
/// 1) PlayerPhase: choose commands for all living party members.
/// 2) ActionPhase: party + enemies act in agility order.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    private System.Action<BattleResult> _onFinished;

    private List<BattleCombatant> _players;
    private List<BattleCombatant> _enemies;

    // Per-hero command selection state
    private int _currentHeroIndex = -1;
    private PlayerCommandType _currentCommand = PlayerCommandType.None;
    private int _currentTargetIndex = 0;

    private bool _battleIsOver = false;
    private bool _heroTurnResolved = false;
    private bool _isHeroTurnActive = false;
    private bool _awaitingCommand = false;
    private bool _awaitingTarget = false;

    // Per-hero defend flags for the current round.
    // _isDefendingThisRound[i] == true → that hero takes reduced damage this round.
    private bool[] _isDefendingThisRound;

    // Queued player actions for this round
    private class QueuedAction
    {
        public bool isPlayer;
        public int actorIndex;          // index into players or enemies list
        public PlayerCommandType command;
        public int targetIndex;         // index into opposite side list
        public float initiative;        // used for turn order
    }

    // Used to store enemy attacks that are waiting for their hit animation event.
    private class PendingEnemyHit
    {
        public int targetIndex;
        public int damage;
        public DamageFactor factor;     // PSIV-style factor; for now enemies use Force (normal physical)
    }

    private readonly Dictionary<BaseEnemyAI, PendingEnemyHit> _pendingEnemyHits
        = new Dictionary<BaseEnemyAI, PendingEnemyHit>();

    private readonly List<QueuedAction> _queuedPlayerActions = new List<QueuedAction>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Entry point called by BattleGateway when starting a battle.
    /// </summary>
    public void BeginBattle(List<BattleCombatant> players,
                            List<BattleCombatant> enemies,
                            System.Action<BattleResult> onFinished)
    {
        _players = players;
        _enemies = enemies;
        _onFinished = onFinished;

        _battleIsOver = false;

        // Prepare defend flags for this party size
        if (_players != null && _players.Count > 0)
        {
            _isDefendingThisRound = new bool[_players.Count];
        }
        else
        {
            _isDefendingThisRound = null;
        }

        StartCoroutine(BattleLoop());
    }

    #region API called by BattleUI

    public void OnHeroCommandSelected(PlayerCommandType command)
    {
        if (!_isHeroTurnActive || !_awaitingCommand) return;
        if (command == PlayerCommandType.None) return;

        if (command == PlayerCommandType.Attack)
        {
            // Auto-target if there is only one living enemy
            int aliveCount = 0;
            int lastAliveIndex = -1;

            if (_enemies != null)
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    if (IsAlive(_enemies[i]))
                    {
                        aliveCount++;
                        lastAliveIndex = i;
                    }
                }
            }

            if (aliveCount <= 0)
            {
                // Somehow no enemies; treat as victory
                Debug.LogWarning("[BattleManager] ATTACK chosen but no living enemies.");
                EndBattle(BattleOutcome.Victory);
                return;
            }

            if (aliveCount == 1)
            {
                // Only one possible target – auto-select it
                _currentCommand = PlayerCommandType.Attack;
                _currentTargetIndex = lastAliveIndex;
                _awaitingCommand = false;
                _awaitingTarget = false;
                _heroTurnResolved = true;
            }
            else
            {
                // Multiple enemies – go to target selection via BattleUI
                _currentCommand = PlayerCommandType.Attack;
                _awaitingCommand = false;
                _awaitingTarget = true;

                // UI switches to target selection state
                BattleUI.Instance?.BeginTargetSelection(_players, _enemies, _currentHeroIndex);
            }
        }
        else if (command == PlayerCommandType.Defend)
        {
            // Defend has no target selection – it is an immediate state for this hero.
            _currentCommand = PlayerCommandType.Defend;

            // Mark this hero as defending for the round
            if (_isDefendingThisRound != null &&
                _currentHeroIndex >= 0 &&
                _currentHeroIndex < _isDefendingThisRound.Length)
            {
                _isDefendingThisRound[_currentHeroIndex] = true;
            }

            _awaitingCommand = false;
            _awaitingTarget = false;
            _heroTurnResolved = true;
        }
        else if (command == PlayerCommandType.Flee)
        {
            _currentCommand = PlayerCommandType.Flee;
            _awaitingCommand = false;
            _awaitingTarget = false;
            _heroTurnResolved = true;
        }
    }

    public void OnHeroTargetConfirmed(int enemyIndex)
    {
        if (!_isHeroTurnActive || !_awaitingTarget) return;

        _currentTargetIndex = Mathf.Clamp(enemyIndex, 0, Mathf.Max(0, _enemies.Count - 1));
        _awaitingTarget = false;
        _heroTurnResolved = true;
    }

    public void OnHeroTargetCancelled()
    {
        if (!_isHeroTurnActive || !_awaitingTarget) return;

        // Go back to command selection for this hero
        _awaitingTarget = false;
        _awaitingCommand = true;

        BattleUI.Instance?.BeginHeroTurn(_currentHeroIndex, _players.Count, _players, _enemies);
    }

    /// <summary>
    /// Called by BaseEnemyAI when its battle attack animation hits.
    /// Uses the damage we previously stored during ActionPhase.
    /// </summary>
    public void OnEnemyHit(BaseEnemyAI enemyAI)
    {
        if (enemyAI == null) return;

        PendingEnemyHit pending;
        if (!_pendingEnemyHits.TryGetValue(enemyAI, out pending))
        {
            Debug.LogWarning("[BattleManager] Enemy hit event with no pending damage.");
            return;
        }

        _pendingEnemyHits.Remove(enemyAI);

        int targetIndex = pending.targetIndex;
        if (_players == null || targetIndex < 0 || targetIndex >= _players.Count)
            return;

        BattleCombatant target = _players[targetIndex];

        if (!IsAlive(target))
            return;

        // Apply defend & factor modifiers (currently Force = normal physical)
        int finalDamage = ApplyPlayerDefenseModifiers(targetIndex, pending.damage, pending.factor);

        target.hp -= finalDamage;
        if (target.hp < 0) target.hp = 0;

        BattleUI.Instance?.PlayDamageFlash();
        BattleUI.Instance?.ShowMessage($"{enemyAI.name} hits {target.name} for {finalDamage} damage!");
        BattleUI.Instance?.RefreshStatus(_players, _enemies);

        // Optional: check party defeat here; but usually the round-end check covers it.
    }

    #endregion

    #region Core loop

    private IEnumerator BattleLoop()
    {
        BattleUI.Instance?.RefreshStatus(_players, _enemies);

        while (!_battleIsOver)
        {
            // 1) PlayerPhase – choose commands
            yield return PlayerPhase();
            if (_battleIsOver) break;

            // 2) ActionPhase – execute in initiative order
            yield return ActionPhase();
            if (_battleIsOver) break;
        }
    }

    /// <summary>
    /// Command phase: choose actions for all living party members.
    /// </summary>
    private IEnumerator PlayerPhase()
    {
        _queuedPlayerActions.Clear();

        // Clear defending flags at the start of the round
        if (_isDefendingThisRound != null)
        {
            for (int i = 0; i < _isDefendingThisRound.Length; i++)
                _isDefendingThisRound[i] = false;
        }

        if (_players == null || _players.Count == 0)
        {
            EndBattle(BattleOutcome.Defeat);
            yield break;
        }

        int heroCount = _players.Count;

        for (int i = 0; i < heroCount; i++)
        {
            if (_battleIsOver)
                yield break;

            if (!IsAlive(_players[i]))
            {
                continue;
            }

            yield return HeroCommandInput(i);
        }

        // All commands chosen; now we go to ActionPhase.
    }

    /// <summary>
    /// Handles one hero's turn in the command selection layer.
    /// </summary>
    private IEnumerator HeroCommandInput(int heroIndex)
    {
        _currentHeroIndex = heroIndex;
        _currentCommand = PlayerCommandType.None;
        _currentTargetIndex = 0;
        _heroTurnResolved = false;
        _isHeroTurnActive = true;
        _awaitingCommand = true;
        _awaitingTarget = false;

        BattleUI.Instance?.BeginHeroTurn(heroIndex, _players.Count, _players, _enemies);

        // Wait until the UI calls back into OnHeroCommandSelected / OnHeroTargetConfirmed
        while (!_heroTurnResolved && !_battleIsOver)
        {
            yield return null;
        }

        _isHeroTurnActive = false;

        if (_battleIsOver)
            yield break;

        // If they somehow ended the hero turn without a command, do nothing.
        if (_currentCommand == PlayerCommandType.None)
            yield break;

        QueueHeroAction(true, heroIndex, _currentCommand, _currentTargetIndex);
    }

    /// <summary>
    /// Action phase: resolve all queued player actions + enemy actions in agility order.
    /// </summary>
    private IEnumerator ActionPhase()
    {
        // Hide menus during action resolution
        BattleUI.Instance?.HideMenusForActionPhase();

        yield return new WaitForSeconds(0.25f);

        if (_players == null || _enemies == null)
            yield break;

        var allActions = new List<QueuedAction>();

        // Add player actions
        allActions.AddRange(_queuedPlayerActions);

        // Queue enemy actions (simple AI: one attack per living enemy)
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!IsAlive(enemy))
                continue;

            int targetIndex = FindRandomLivingPlayerIndex();
            if (targetIndex < 0)
                continue;

            var action = new QueuedAction
            {
                isPlayer = false,
                actorIndex = i,
                command = PlayerCommandType.Attack,
                targetIndex = targetIndex,
                initiative = enemy.agility + Random.Range(0f, 1f)
            };

            allActions.Add(action);
        }

        // Sort by initiative descending (higher agility first)
        allActions.Sort((a, b) => b.initiative.CompareTo(a.initiative));

        // Execute
        foreach (var act in allActions)
        {
            if (_battleIsOver)
                yield break;

            if (act.isPlayer)
            {
                // Player action
                if (_players == null || act.actorIndex < 0 || act.actorIndex >= _players.Count)
                    continue;

                var hero = _players[act.actorIndex];
                if (!IsAlive(hero))
                    continue;

                if (act.command == PlayerCommandType.Attack)
                {
                    if (_enemies == null || act.targetIndex < 0 || act.targetIndex >= _enemies.Count)
                        continue;

                    var target = _enemies[act.targetIndex];
                    if (!IsAlive(target))
                        continue;

                    // Play visuals for this action (if configured in the global catalog).
                    // Visuals must complete (and be cleaned up) BEFORE damage numbers/messages display (PSIV feel).
                    if (BattleUI.Instance != null)
                    {
                        yield return BattleUI.Instance.PlayActionSequence_PlayerVsEnemy(act.actorIndex, hero, act.command, target);
                    }

                    int damage = ComputeDamage(hero, target);
                    target.hp -= damage;
                    if (target.hp < 0) target.hp = 0;

                    BattleUI.Instance?.ShowMessage($"{hero.name} attacks {target.name} for {damage} damage!");
                    BattleUI.Instance?.RefreshStatus(_players, _enemies);

                    // Linger so the damage readout has time before the next action.
                    yield return new WaitForSeconds(0.6f);

                    if (!IsAlive(target))
                    {
                        BattleUI.Instance?.ShowMessage($"{target.name} is defeated!");
                        if (AreAllEnemiesDefeated())
                        {
                            EndBattle(BattleOutcome.Victory);
                            yield break;
                        }
                    }
                }
                else if (act.command == PlayerCommandType.Defend)
                {
                    // Defend has no immediate effect here; damage reduction is applied
                    // when this hero is hit later in the round.
                    yield return new WaitForSeconds(0.25f);
                }
                else if (act.command == PlayerCommandType.Flee)
                {
                    // Simple flee chance: compare party agility vs enemy agility.
                    int partyAgi = hero.agility;
                    int enemyAgi = 0;
                    if (_enemies != null)
                    {
                        foreach (var e in _enemies)
                        {
                            if (e != null && e.agility > enemyAgi)
                                enemyAgi = e.agility;
                        }
                    }

                    float fleeChance = 0.5f;
                    if (partyAgi + enemyAgi > 0)
                    {
                        fleeChance = Mathf.Clamp01((float)partyAgi / (partyAgi + enemyAgi));
                    }

                    float roll = Random.value;
                    if (roll <= fleeChance)
                    {
                        BattleUI.Instance?.ShowMessage($"{hero.name} successfully fled!");
                        EndBattle(BattleOutcome.Flee);
                        yield break;
                    }
                    else
                    {
                        BattleUI.Instance?.ShowMessage($"{hero.name} failed to flee!");
                    }

                    yield return new WaitForSeconds(0.25f);
                }
            }
            else
            {
                // Enemy action
                if (_enemies == null || act.actorIndex < 0 || act.actorIndex >= _enemies.Count)
                    continue;

                var enemy = _enemies[act.actorIndex];
                if (!IsAlive(enemy))
                    continue;

                if (_players == null || act.targetIndex < 0 || act.targetIndex >= _players.Count)
                    continue;

                var target = _players[act.targetIndex];
                if (!IsAlive(target))
                    continue;

                int damage = ComputeDamage(enemy, target);

                if (enemy.enemyRef != null)
                {
                    // Store damage so the animation event can apply it at the hit frame
                    _pendingEnemyHits[enemy.enemyRef] = new PendingEnemyHit
                    {
                        targetIndex = act.targetIndex,
                        damage = damage,
                        factor = DamageFactor.Force // normal physical attack for now
                    };

                    enemy.enemyRef.PlayBattleAttackAnimation();
                }
                else
                {
                    // No animation reference – apply immediately with defend + factor modifiers.
                    int finalDamage = ApplyPlayerDefenseModifiers(act.targetIndex, damage, DamageFactor.Force);

                    target.hp -= finalDamage;
                    if (target.hp < 0) target.hp = 0;

                    // TODO: Fix to crit-only, and trigger screen flash ONLY when this hit is a critical.

                    BattleUI.Instance?.PlayDamageFlash();

                    if (BattleUI.Instance != null)
                    {
                        StartCoroutine(BattleUI.Instance.PlayEnemyHitSequenceOnParty(enemy.name, act.targetIndex, target.name, finalDamage));
                    }

                    // Refresh AFTER blink so HP/damage "appears" after the flash.
                    StartCoroutine(CoRefreshAfterDelay(BattleUI.Instance.GetPartyPortraitFlashDuration()));


                    BattleUI.Instance?.ShowMessage($"{enemy.name} hits {target.name} for {finalDamage} damage!");
                    BattleUI.Instance?.RefreshStatus(_players, _enemies);
                }

                if (AreAllPlayersDefeated())
                {
                    EndBattle(BattleOutcome.Defeat);
                    yield break;
                }

                yield return new WaitForSeconds(0.25f);
            }
        }

        // After all actions, check victory/defeat in case we didn't already exit.
        if (AreAllEnemiesDefeated())
        {
            EndBattle(BattleOutcome.Victory);
        }
        else if (AreAllPlayersDefeated())
        {
            EndBattle(BattleOutcome.Defeat);
        }
        else
        {
            // New round: UI will go back to PlayerPhase via BattleLoop.
            BattleUI.Instance?.RefreshStatus(_players, _enemies);
        }
    }

    #endregion

    #region Helpers

    private void QueueHeroAction(bool isPlayer, int actorIndex, PlayerCommandType command, int targetIndex)
    {
        if (command == PlayerCommandType.None)
            return;

        float initiative = 0f;

        if (isPlayer)
        {
            if (_players == null || actorIndex < 0 || actorIndex >= _players.Count)
                return;

            var hero = _players[actorIndex];
            initiative = hero.agility + Random.Range(0f, 1f);
        }
        else
        {
            if (_enemies == null || actorIndex < 0 || actorIndex >= _enemies.Count)
                return;

            var enemy = _enemies[actorIndex];
            initiative = enemy.agility + Random.Range(0f, 1f);
        }

        _queuedPlayerActions.Add(new QueuedAction
        {
            isPlayer = isPlayer,
            actorIndex = actorIndex,
            command = command,
            targetIndex = targetIndex,
            initiative = initiative
        });
    }

    private IEnumerator CoRefreshAfterDelay(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        BattleUI.Instance?.RefreshStatus(_players, _enemies);
    }


    private bool IsAlive(BattleCombatant c)
    {
        return c != null && c.hp > 0;
    }

    private bool AreAllEnemiesDefeated()
    {
        if (_enemies == null || _enemies.Count == 0)
            return true;

        foreach (var e in _enemies)
        {
            if (IsAlive(e))
                return false;
        }
        return true;
    }

    private bool AreAllPlayersDefeated()
    {
        if (_players == null || _players.Count == 0)
            return true;

        foreach (var p in _players)
        {
            if (IsAlive(p))
                return false;
        }
        return true;
    }

    private int FindRandomLivingPlayerIndex()
    {
        if (_players == null || _players.Count == 0)
            return -1;

        var candidates = new List<int>();
        for (int i = 0; i < _players.Count; i++)
        {
            if (IsAlive(_players[i]))
                candidates.Add(i);
        }

        if (candidates.Count == 0)
            return -1;

        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    private int ComputeDamage(BattleCombatant attacker, BattleCombatant defender)
    {
        if (attacker == null || defender == null)
            return 0;

        int atk = Mathf.Max(0, attacker.atk);
        int def = Mathf.Max(0, defender.def);
        int baseDamage = atk - Mathf.FloorToInt(def * 0.5f);
        if (baseDamage < 1) baseDamage = 1;

        // Tiny randomness so numbers don't feel too static.
        int variance = Mathf.Max(1, Mathf.FloorToInt(baseDamage * 0.1f));
        int finalDamage = Random.Range(baseDamage - variance, baseDamage + variance + 1);
        if (finalDamage < 1) finalDamage = 1;

        return finalDamage;
    }

    /// <summary>
    /// Apply player defense modifiers.
    /// Currently:
    /// - Defend halves any factor where DamageFactorInfo.IsAffectedByDefend(factor) is true.
    ///   (By default, that's Force / normal physical only, mirroring PSIV.)
    /// </summary>
    private int ApplyPlayerDefenseModifiers(int playerIndex, int incomingDamage, DamageFactor factor)
    {
        int dmg = Mathf.Max(0, incomingDamage);

        bool isDefending =
            _isDefendingThisRound != null &&
            playerIndex >= 0 &&
            playerIndex < _isDefendingThisRound.Length &&
            _isDefendingThisRound[playerIndex] &&
            incomingDamage > 0;

        if (isDefending && DamageFactorInfo.IsAffectedByDefend(factor))
        {
            int before = dmg;
            dmg = Mathf.FloorToInt(dmg * 0.5f);
            if (dmg < 1) dmg = 1;

            // Optional log so you can see the effect numerically
            if (_players != null &&
                playerIndex >= 0 &&
                playerIndex < _players.Count)
            {
                var hero = _players[playerIndex];
                string factorName = DamageFactorInfo.GetDisplayName(factor);
                Debug.Log($"[BattleManager] {hero.name} defends vs {factorName}: {before} → {dmg}");
            }
        }

        return dmg;
    }

    private void EndBattle(BattleOutcome outcome)
    {
        if (_battleIsOver)
            return;

        _battleIsOver = true;

        var result = new BattleResult
        {
            outcome = outcome,
            rewards = new BattleRewards
            {
                xp = 0,
                meseta = 0
            }
        };

        // Sync HP back into PartyManager
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.SyncHPBackFromBattle();
        }

        _onFinished?.Invoke(result);
    }

    #endregion
}
