using UnityEngine;

/// <summary>
/// Central place for turning CharacterDefinition + level (+ equipment)
/// into battle-ready stats. Phase 1: baseline only, no behavior modifiers yet.
/// </summary>
public static class StatCalculator
{
    /// <summary>
    /// Computes core stats (maxHP, maxTP, STR, MTL, AGI, DEX)
    /// from a CharacterDefinition and a level.
    /// 
    /// Later we can layer behavior-based modifiers on top of this.
    /// </summary>
    public static void ComputeBaseStats(
        CharacterDefinition def,
        int level,
        out int maxHP,
        out int maxTP,
        out int strength,
        out int mental,
        out int agility,
        out int dexterity)
    {
        if (def == null)
        {
            Debug.LogWarning("[StatCalculator] Null CharacterDefinition. Using fallback stats.");
            maxHP = 1;
            maxTP = 0;
            strength = 1;
            mental = 1;
            agility = 1;
            dexterity = 1;
            return;
        }

        if (level < def.baseLevel)
        {
            level = def.baseLevel;
        }

        int levelOffset = level - def.baseLevel;

        maxHP    = def.baseMaxHP    + def.hpPerLevel   * levelOffset;
        maxTP    = def.baseMaxTP    + def.tpPerLevel   * levelOffset;
        strength = def.baseStrength + def.strPerLevel  * levelOffset;
        mental   = def.baseMental   + def.mtlPerLevel  * levelOffset;
        agility  = def.baseAgility  + def.agiPerLevel  * levelOffset;
        dexterity= def.baseDexterity+ def.dexPerLevel  * levelOffset;
    }

    /// <summary>
    /// Computes derived battle stats from core stats + simple equipment values.
    /// This keeps your current damage feel intact for now.
    /// </summary>
    public static void ComputeDerivedStats(
        int strength,
        int mental,
        int agility,
        int dexterity,
        int weaponAttack,
        int armorDefense,
        out int atk,
        out int def,
        out int magicDef)
    {
        // Same idea as your old CharacterData.GetBattleAttack/GetBattleDefense,
        // just centralized.
        atk = strength + weaponAttack + Mathf.FloorToInt(dexterity * 0.5f);
        def = armorDefense + mental + Mathf.FloorToInt(agility * 0.5f);

        // For now, magic defense = mental. We’ll expand this when we
        // expose explicit resistances.
        magicDef = mental;
    }
}
