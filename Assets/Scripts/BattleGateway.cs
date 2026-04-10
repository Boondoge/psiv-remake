using System.Collections.Generic;
using UnityEngine;

public class BattleGateway : MonoBehaviour
{
    public static BattleGateway Instance { get; private set; }

    [Header("UI")]
    public GameObject battleUIRoot;
    public GameObject explorationHUD;   // optional

    [Header("Exploration Controls")]
    public MonoBehaviour[] explorationControlScripts; // drag your movement/look scripts here

    private List<BaseEnemyAI> activeEnemies = new List<BaseEnemyAI>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (battleUIRoot != null)
            battleUIRoot.SetActive(false);
    }

    public void StartBattle(List<BaseEnemyAI> enemies)
    {
        if (GameModeManager.Instance.IsBattle) return;

        activeEnemies = enemies;

        // Switch to battle mode
        GameModeManager.Instance.SetMode(GameMode.Battle);

        // Put enemies into battle mode & stop their exploration Update()
        foreach (var e in activeEnemies)
        {
            if (e == null) continue;
            e.EnterBattleMode();
            e.enabled = false;
        }

        // Swap UI
        if (explorationHUD != null)
            explorationHUD.SetActive(false);

        if (battleUIRoot != null)
            battleUIRoot.SetActive(true);

        // Build combatants & start battle
        var enemyCombatants = BattleFactory.CreateEnemiesFromBaseAI(activeEnemies);
        var playerCombatants = PartyManager.Instance.BuildBattleParty();

        BattleManager.Instance.BeginBattle(playerCombatants, enemyCombatants, OnBattleFinished);
    }

    private void OnBattleFinished(BattleResult result)
    {
        // Handle outcome
        if (result.outcome == BattleOutcome.Victory)
        {
            foreach (var e in activeEnemies)
            {
                if (e != null) Destroy(e.gameObject);
            }
            PartyManager.Instance.ApplyRewards(result.rewards);
        }
        else if (result.outcome == BattleOutcome.Flee)
        {
            foreach (var e in activeEnemies)
            {
                if (e == null) continue;
                e.enabled = true;
                e.ExitBattleMode();
            }
        }
        else if (result.outcome == BattleOutcome.Defeat)
        {
            Debug.Log("Party defeated (stub).");
            // TODO: game over / reload scene
        }

        // NEW: sync party HP from battle data
        PartyManager.Instance.SyncHPBackFromBattle();

        activeEnemies.Clear();

        if (battleUIRoot != null)
            battleUIRoot.SetActive(false);

        if (explorationHUD != null)
            explorationHUD.SetActive(true);

        GameModeManager.Instance.SetMode(GameMode.Exploration);
    }
}
