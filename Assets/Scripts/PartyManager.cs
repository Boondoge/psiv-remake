using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterData
{
    [Header("Identity")]
    [Tooltip("Unique ID, e.g. \"chaz\", \"alys\", \"hahn\". Should match the CharacterDefinition.")]
    public string characterId;

    [Tooltip("Optional name override. If empty, uses CharacterDefinition.displayName.")]
    public string displayNameOverride;

    [Header("Definition")]
    [Tooltip("Static data & growth for this character (Chaz, Alys, Hahn, etc.).")]
    public CharacterDefinition definition;

    [Header("Progression")]
    [Tooltip("Current level for this save file.")]
    public int level = 1;

    [Header("Current Resources")]
    [Tooltip("Current HP at this moment in the overworld / between battles.")]
    public int currentHP;

    [Tooltip("Current TP at this moment in the overworld / between battles.")]
    public int currentTP;

    [Header("Equipment (flat placeholders for now)")]
    [Tooltip("Total weapon attack from equipped gear.")]
    public int weaponAttack = 0;

    [Tooltip("Total armor defense from equipped gear.")]
    public int armorDefense = 0;

    [Header("Visual Equipment")]
    public WeaponVisualType weaponVisual = WeaponVisualType.None;

    [Header("Hand Equipment (supports single/dual wield)")]
    public HandItemType mainHand = HandItemType.None;
    public HandItemType offHand = HandItemType.None;

    [Tooltip("If true, this character can dual-wield guns (fun rule).")]
    public bool allowDualGun = false;



    /// <summary>
    /// Name to show in UI.
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(displayNameOverride))
            return displayNameOverride;

        if (definition != null && !string.IsNullOrEmpty(definition.displayName))
            return definition.displayName;

        return string.IsNullOrEmpty(characterId) ? "Unknown" : characterId;
    }

    /// <summary>
    /// Returns max HP based on definition + level (no behavior modifiers yet).
    /// </summary>
    public int GetMaxHP()
    {
        if (definition == null)
        {
            return Mathf.Max(1, currentHP); // fallback if something is misconfigured
        }

        int maxHP, maxTP, str, mtl, agi, dex;
        StatCalculator.ComputeBaseStats(definition, level,
            out maxHP, out maxTP, out str, out mtl, out agi, out dex);

        return Mathf.Max(1, maxHP);
    }

    /// <summary>
    /// Convenience: builds a BattleCombatant snapshot from this character.
    /// This is what BattleManager/BattleUI will operate on.
    /// </summary>
    public BattleCombatant BuildBattleCombatant()
    {
        // If no definition, fall back to something sane instead of exploding.
        if (definition == null)
        {
            Debug.LogWarning($"[CharacterData] {characterId} has no CharacterDefinition; using fallback battle stats.");

            int fallbackMaxHP = Mathf.Max(1, currentHP);
            int fallbackMaxTP = Mathf.Max(0, currentTP);

            return new BattleCombatant
            {
                name = GetDisplayName(),
                level = level,

                maxHP = fallbackMaxHP,
                hp = Mathf.Clamp(currentHP, 0, fallbackMaxHP),

                maxTP = fallbackMaxTP,
                tp = Mathf.Clamp(currentTP, 0, fallbackMaxTP),

                strength = 10,
                mental = 10,
                agility = 10,
                dexterity = 10,

                atk = 10 + weaponAttack,
                def = 10 + armorDefense,
                magicDef = 10
            };
        }

        int maxHP, maxTP, strength, mental, agility, dexterity;
        StatCalculator.ComputeBaseStats(definition, level,
            out maxHP, out maxTP, out strength, out mental, out agility, out dexterity);

        maxHP = Mathf.Max(1, maxHP);
        maxTP = Mathf.Max(0, maxTP);

        // If current HP/TP are 0 (e.g. fresh save), assume full.
        int hp = currentHP <= 0 ? maxHP : Mathf.Clamp(currentHP, 0, maxHP);
        int tp = currentTP < 0 ? 0 : Mathf.Clamp(currentTP, 0, maxTP);

        int atk, def, magicDef;
        StatCalculator.ComputeDerivedStats(
            strength, mental, agility, dexterity,
            weaponAttack, armorDefense,
            out atk, out def, out magicDef);

        return new BattleCombatant
        {
            // Identity
            name = GetDisplayName(),
            level = level,

            // Visual equipment hint
            weaponVisual = this.weaponVisual,
            mainHand = this.mainHand,
            offHand = this.offHand,
            allowDualGun = this.allowDualGun,


            // Core resources
            maxHP = maxHP,
            hp = hp,
            maxTP = maxTP,
            tp = tp,

            // Base stats
            strength = strength,
            mental = mental,
            agility = agility,
            dexterity = dexterity,

            // Derived
            atk = atk,
            def = def,
            magicDef = magicDef
        };
    }
}

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    [Header("Current Party")]
    public List<CharacterData> activeParty = new List<CharacterData>();

    // Keep a copy of the last built combatants so we can sync HP back
    private List<BattleCombatant> _lastBattleParty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Build battle combatants from activeParty using CharacterDefinition + StatCalculator.
    /// This is called by whatever starts the battle (BattleGateway, etc.).
    /// </summary>
    public List<BattleCombatant> BuildBattleParty()
    {
        var battleParty = new List<BattleCombatant>();

        if (activeParty == null)
        {
            _lastBattleParty = battleParty;
            return battleParty;
        }

        for (int i = 0; i < activeParty.Count; i++)
        {
            var c = activeParty[i];
            if (c == null) continue;

            // Ensure we have at least a usable level.
            if (c.definition != null && c.level < c.definition.baseLevel)
            {
                c.level = c.definition.baseLevel;
            }
            if (c.level < 1) c.level = 1;

            var combatant = c.BuildBattleCombatant();

            // ✅ Critical identity + slot index for visuals / UI mapping
            combatant.partyIndex = i;
            combatant.characterId = c.characterId;

            battleParty.Add(combatant);
        }

        _lastBattleParty = battleParty;
        return battleParty;
    }


    /// <summary>
    /// Sync HP from last battle back into activeParty.
    /// Call this at the end of battle.
    /// </summary>
    public void SyncHPBackFromBattle()
    {
        if (_lastBattleParty == null)
            return;

        int count = Mathf.Min(activeParty.Count, _lastBattleParty.Count);

        for (int i = 0; i < count; i++)
        {
            var charData = activeParty[i];
            var battleData = _lastBattleParty[i];

            if (charData == null || battleData == null)
                continue;

            int maxHp = charData.GetMaxHP();
            charData.currentHP = Mathf.Clamp(battleData.hp, 0, maxHp);
        }
    }

    public CharacterData GetLeader()
    {
        if (activeParty != null && activeParty.Count > 0)
            return activeParty[0];

        return null;
    }

    public void ApplyRewards(BattleRewards rewards)
    {
        Debug.Log($"Gained {rewards.xp} XP and {rewards.meseta} Meseta (stub).");
        // later: XP, leveling, etc.
    }

    public bool IsPartyDefeated()
    {
        if (activeParty == null || activeParty.Count == 0)
            return true;

        foreach (var c in activeParty)
        {
            if (c != null && c.currentHP > 0)
                return false;
        }
        return true;
    }
}
