using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BaseEnemyAI))]
public class EncounterTrigger : MonoBehaviour
{
    public float engageDistance = 6f;   // tweak in Inspector
    private BaseEnemyAI enemyAI;
    private bool battleStarted;

    private void Awake()
    {
        enemyAI = GetComponent<BaseEnemyAI>();
    }

    private void Update()
    {
        if (battleStarted) return;
        if (!GameModeManager.Instance.IsExploration) return;
        if (enemyAI.player == null) return;

        float dist = Vector3.Distance(transform.position, enemyAI.player.position);
        if (dist <= engageDistance)
        {
            StartBattle();
        }
    }

    private void StartBattle()
    {
        battleStarted = true;

        var enemies = new List<BaseEnemyAI> { enemyAI }; // single-enemy battle for now
        BattleGateway.Instance.StartBattle(enemies);
    }
}
