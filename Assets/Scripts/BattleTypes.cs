using System;
using UnityEngine;

/// <summary>
/// Outcome of a completed battle.
/// </summary>
public enum BattleOutcome
{
    Victory,
    Defeat,
    Flee
}

public enum WeaponVisualType
{
    None,
    Sword,
    Dagger,
    Slasher,
    Claw,
    Staff,
    Gun,
    Axe
}

public enum HandItemType
{
    None,
    Shield,

    Dagger,
    Slasher,
    Claw,
    Gun,

    Sword,
    Axe,
    Staff
}

public enum WeaponStyle
{
    None,

    DaggerSingle,
    DaggerDual,

    SlasherSingle,
    SlasherDual,

    ClawSingle,
    ClawDual,

    GunSingle,
    GunDual,

    Sword,
    Axe,
    Staff
}


/// <summary>
/// Utility to derive a character's current weapon style (single vs dual wield)
/// from their equipped main/off-hand items.
/// </summary>
public static class WeaponStyleUtil
{
    public static WeaponStyle GetStyle(HandItemType main, HandItemType off, bool allowDualGun)
    {
        if (main == HandItemType.Dagger)
            return (off == HandItemType.Dagger) ? WeaponStyle.DaggerDual : WeaponStyle.DaggerSingle;

        if (main == HandItemType.Slasher)
            return (off == HandItemType.Slasher) ? WeaponStyle.SlasherDual : WeaponStyle.SlasherSingle;

        if (main == HandItemType.Claw)
            return (off == HandItemType.Claw) ? WeaponStyle.ClawDual : WeaponStyle.ClawSingle;

        if (main == HandItemType.Gun)
        {
            if (off == HandItemType.Gun && allowDualGun) return WeaponStyle.GunDual;
            return WeaponStyle.GunSingle;
        }

        if (main == HandItemType.Sword) return WeaponStyle.Sword;
        if (main == HandItemType.Axe) return WeaponStyle.Axe;
        if (main == HandItemType.Staff) return WeaponStyle.Staff;

        return WeaponStyle.None;
    }
}




/// <summary>
/// PSIV-style damage factor (element) used to categorize attack types.
/// We’ve renamed them to something readable while keeping the original numbering.
/// </summary>
public enum DamageFactor
{
    None = 0, ///  0: None
    Force = 1, ///  1: Force        – Normal physical, wind, dark (Defend halves this)
    Energy = 2, ///  2: Energy       – Laser/plasma/laconia/guardian, PD physical, etc.
    Fire = 3, ///  3: Fire         – Fire spells/breath
    Gravity = 4, ///  4: Gravity      – Gra / Distortion
    Ice = 5, ///  5: Ice          – Wat (covers water/ice)
    Lightning = 6, ///  6: Lightning    – Tandle, Thunder Claw
    Holy = 7, ///  7: Holy         – Tsu, Megid, Elsydeon damage
    Holyword = 8, ///  8: Holyword     – Holyword / added ID on Elsydeon
    BioCorrosion = 9, ///  9: BioCorrosion – Brose-style anti-bio vs mechs
    Death = 10, /// 10: Death        – Standard instant death (ID) vs organics
    MentalStatus = 11, /// 11: MentalStatus – Sleep/confuse/etc. non-ID mental status
    AntiMech = 12, /// 12: AntiMech     – Spark / Hyper Jammer type anti-machine
    AntiDark = 13, /// 13: AntiDark     – Efess/St. Fire vs “dark” enemies (or physical status vs PCs)
    UniversalKO = 14 /// 14: UniversalKO  – Crash/Negatis/Explode style universal ID
}

/// <summary>
/// Coarse category for factor metadata.
/// Mostly for future resist tables / UI.
/// </summary>
public enum DamageFactorCategory
{
    Physical,
    Magical,
    Status,
    Meta
}

/// <summary>
/// Central metadata about each DamageFactor, including:
/// - Display name / short label
/// - Category
/// - Whether the standard Defend command affects it
/// This lets us keep PSIV-ish behavior in one place and avoid hardcoding
/// "F1" / "physical" assumptions all over the battle code.
/// </summary>
public static class DamageFactorInfo
{
    [Serializable]
    public class Meta
    {
        public DamageFactor factor;
        public string name;
        public string shortLabel;
        public DamageFactorCategory category;
        public bool affectedByDefend;
        public string notes;
    }

    private static readonly System.Collections.Generic.Dictionary<DamageFactor, Meta> Table
        = new System.Collections.Generic.Dictionary<DamageFactor, Meta>
    {
        {
            DamageFactor.Force,
            new Meta
            {
                factor = DamageFactor.Force,
                name = "Force",
                shortLabel = "FOR",
                category = DamageFactorCategory.Physical,
                // PSIV: Defend halves Force only
                affectedByDefend = true,
                notes = "Normal physical + wind + dark. Standard physical hits and Zan/Hewn-like effects."
            }
        },
        {
            DamageFactor.Energy,
            new Meta
            {
                factor = DamageFactor.Energy,
                name = "Energy",
                shortLabel = "ENG",
                category = DamageFactorCategory.Physical,
                affectedByDefend = false,
                notes = "Laser/Plasma/Laconia/Guardian, PD physical, Legeon, Astral, etc."
            }
        },
        {
            DamageFactor.Fire,
            new Meta
            {
                factor = DamageFactor.Fire,
                name = "Fire",
                shortLabel = "FIR",
                category = DamageFactorCategory.Magical,
                affectedByDefend = false,
                notes = "Foi/Flaeli, Firebreath, Flame Sword."
            }
        },
        {
            DamageFactor.Gravity,
            new Meta
            {
                factor = DamageFactor.Gravity,
                name = "Gravity",
                shortLabel = "GRV",
                category = DamageFactorCategory.Magical,
                affectedByDefend = false,
                notes = "Gra spells, Distortion. Close to non-elemental in PSIV."
            }
        },
        {
            DamageFactor.Ice,
            new Meta
            {
                factor = DamageFactor.Ice,
                name = "Ice",
                shortLabel = "ICE",
                category = DamageFactorCategory.Magical,
                affectedByDefend = false,
                notes = "Wat spells. Covers water/ice."
            }
        },
        {
            DamageFactor.Lightning,
            new Meta
            {
                factor = DamageFactor.Lightning,
                name = "Lightning",
                shortLabel = "LIT",
                category = DamageFactorCategory.Magical,
                affectedByDefend = false,
                notes = "Tandle, Thunder Claw."
            }
        },
        {
            DamageFactor.Holy,
            new Meta
            {
                factor = DamageFactor.Holy,
                name = "Holy",
                shortLabel = "HOL",
                category = DamageFactorCategory.Magical,
                affectedByDefend = false,
                notes = "Tsu, Rayblade, Megid, Elsydeon, Silver Tusk."
            }
        },
        {
            DamageFactor.Holyword,
            new Meta
            {
                factor = DamageFactor.Holyword,
                name = "Holyword",
                shortLabel = "HWD",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Holyword-style ID vs dark enemies; mostly DL-useless."
            }
        },
        {
            DamageFactor.BioCorrosion,
            new Meta
            {
                factor = DamageFactor.BioCorrosion,
                name = "BioCorrosion",
                shortLabel = "BIO",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Brose; bio vs mechs (machines fully vulnerable, bosses immune in PSIV)."
            }
        },
        {
            DamageFactor.Death,
            new Meta
            {
                factor = DamageFactor.Death,
                name = "Death",
                shortLabel = "DTH",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Standard instant death (ID) vs organics."
            }
        },
        {
            DamageFactor.MentalStatus,
            new Meta
            {
                factor = DamageFactor.MentalStatus,
                name = "Mental Status",
                shortLabel = "MST",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Non-ID mental status (sleep/confuse/etc.), not poison/paralysis."
            }
        },
        {
            DamageFactor.AntiMech,
            new Meta
            {
                factor = DamageFactor.AntiMech,
                name = "Anti-Mech",
                shortLabel = "AMK",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Spark/Hyper Jammer; anti-machine, others immune."
            }
        },
        {
            DamageFactor.AntiDark,
            new Meta
            {
                factor = DamageFactor.AntiDark,
                name = "Anti-Dark",
                shortLabel = "ADK",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Efess/St.Fire vs dark enemies; also mapped to physical status vs PCs."
            }
        },
        {
            DamageFactor.UniversalKO,
            new Meta
            {
                factor = DamageFactor.UniversalKO,
                name = "Universal KO",
                shortLabel = "UKO",
                category = DamageFactorCategory.Status,
                affectedByDefend = false,
                notes = "Crash/Negatis/Explode; ID that can hit both mechs and organics."
            }
        }
    };

    /// <summary>
    /// Returns metadata for the given factor, or null if none.
    /// </summary>
    public static Meta Get(DamageFactor factor)
    {
        if (factor == DamageFactor.None)
            return null;

        Meta meta;
        if (Table.TryGetValue(factor, out meta))
            return meta;

        return null;
    }

    /// <summary>
    /// Convenience: does Defend reduce damage for this factor?
    /// Right now we mirror PSIV: Defend halves Force only.
    /// </summary>
    public static bool IsAffectedByDefend(DamageFactor factor)
    {
        var meta = Get(factor);
        return meta != null && meta.affectedByDefend;
    }

    /// <summary>
    /// Convenience: safe display name for logs/UI.
    /// </summary>
    public static string GetDisplayName(DamageFactor factor)
    {
        if (factor == DamageFactor.None)
            return "None";

        var meta = Get(factor);
        return meta != null ? meta.name : factor.ToString();
    }

    /// <summary>
    /// Short label for compact UI (3 letters).
    /// </summary>
    public static string GetShortLabel(DamageFactor factor)
    {
        var meta = Get(factor);
        return meta != null ? meta.shortLabel : factor.ToString();
    }
}

[Serializable]
public class BattleRewards
{
    public int xp;
    public int meseta;
}

[Serializable]
public class BattleResult
{
    public BattleOutcome outcome;
    public BattleRewards rewards;
}

/// <summary>
/// Minimal data needed for a combatant (player or enemy) in battle.
/// Later we can expand this, or keep it strictly "battle only"
/// and derive from CharacterDefinition / EnemyDefinition.
/// </summary>
[Serializable]
public class BattleCombatant
{
    public string name;
    public string characterId; // stable id like "chaz" used for BattleActorVisualCatalog lookup


    // Link back to underlying data indices if needed
    public int partyIndex;   // index in PartyManager active party, or -1 if enemy
    public int enemyIndex;   // index in this encounter's enemy list, or -1 if player

    // How this combatant should look when attacking (for players primarily)
    public WeaponVisualType weaponVisual = WeaponVisualType.None;

    // Equipment hands (for single/dual wield decisions)
    public HandItemType mainHand = HandItemType.None;
    public HandItemType offHand = HandItemType.None;

    // “Fun” rule gating (Wren/Demi dual guns)
    public bool allowDualGun = false;

    // Derived style used by BattleManager/VFX
    public WeaponStyle weaponStyle
    {
        get { return WeaponStyleUtil.GetStyle(mainHand, offHand, allowDualGun); }
    }


    // Core stats used by the current simple damage model
    public int level;

    public int maxHP;
    public int hp;

    public int maxTP;
    public int tp;

    // Derived attack/defense values (if you want them separate from atk/def)
    public int attackPower;   // Derived from STR + weapon etc. For now we can just map from atk stat.
    public int defensePower;  // Derived from DEF + armor etc.

    // Base stats
    public int strength;      // STR
    public int mental;        // MTL
    public int agility;       // AGI
    public int dexterity;     // DEX

    // Simple flags
    public bool isEnemy;      // true = enemy, false = player

    // Raw battle stats (used by current damage formulas)
    public int atk;        // Attack (physical)
    public int def;        // Defense (physical)
    public int magicDef;   // often Mental is used for this; we keep explicit slot

    // For enemies that have a live AI component in the scene
    [NonSerialized] public BaseEnemyAI enemyRef;
}
