using System.Collections.Generic;
using UnityEngine;

public static class BattleFactory
{
    public static List<BattleCombatant> CreateEnemiesFromBaseAI(List<BaseEnemyAI> enemies)
    {
        var list = new List<BattleCombatant>();

        foreach (var e in enemies)
        {
            if (e == null) continue;

            var combatant = new BattleCombatant
            {
                // Identity
                name  = e.name,
                level = 1,               // TODO: set per enemy type later

                // Core resources
                maxHP = Mathf.RoundToInt(e.health),
                hp    = Mathf.RoundToInt(e.health),
                maxTP = 0,
                tp    = 0,

                // Base stats (stub values for now – you can tune per enemy type later)
                strength  = 10,
                mental    = 10,
                agility   = 10,          // ← used for flee chance
                dexterity = 10,

                // Derived / battle stats
                atk      = e.attackDamage,
                def      = 0,
                magicDef = 0,

                // Link back to the scene enemy
                enemyRef = e

            };

            list.Add(combatant);
        }

        return list;
    }
}
