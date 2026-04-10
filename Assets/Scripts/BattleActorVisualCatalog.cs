using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global lookup for battle visuals, loaded from Resources.
/// 
/// The goal is to keep scenes free of per-character wiring:
/// BattleUI/BattleManager ask the catalog for what to spawn based on characterId + action.
/// </summary>
[CreateAssetMenu(menuName = "PSIV/Battle/Battle Actor Visual Catalog")]
public class BattleActorVisualCatalog : ScriptableObject
{
    public enum VisualActionType
    {
        None = 0,
        Attack = 1,
        Defend = 2,
        Flee = 3,
        Tech = 10,
        Skill = 11,
        Item = 12
    }

    [Serializable]
    public class Entry
    {
        [Header("Key")]
        [Tooltip("Stable characterId (matches PartyManager CharacterData.characterId / CharacterDefinition.characterId). Example: \"chaz\"")]
        public string characterId;

        public VisualActionType actionType = VisualActionType.None;

        [Tooltip("For ATTACK only: which weaponStyle this visual is for. For other actions, leave as None.")]
        public WeaponStyle weaponStyle = WeaponStyle.None;

        [Tooltip("Optional ability id for Tech/Skill/Item lookup. Leave empty for generic.")]
        public string abilityId;

        [Header("Prefabs")]
        [Tooltip("Optional UI prefab spawned at the caster's CastAnchor (party tile). Add BattleUiVfx to control duration.")]
        public GameObject casterUiPrefab;

        [Tooltip("Optional target-side prefab spawned at the enemy's VfxAnchor. Used if hitVfxSequence is empty.")]
        public BattleVfxSprite targetVfxPrefab;

        [Tooltip("Optional multi-hit ordered VFX list (e.g., dual daggers L then R). If set, this overrides targetVfxPrefab.")]
        public BattleVfxSprite[] hitVfxSequence;

        [Header("Enemy Hit Reaction (after EACH hit VFX finishes)")]
        [Tooltip("If true, pauses enemy Animator and snaps it back to its default pose/frame during the blink.")]
        public bool pauseEnemyAnimatorDuringHit = true;

        [Tooltip("How many white blinks to do after each hit (PSIV feel is often 4).")]
        public int enemyBlinkCount = 4;

        [Tooltip("Seconds enemy stays white per blink.")]
        public float enemyBlinkOnSeconds = 0.06f;

        [Tooltip("Seconds enemy stays normal per blink.")]
        public float enemyBlinkOffSeconds = 0.06f;

        [Header("Timing / Sorting")]
        [Tooltip("Sorting order offset passed into BattleVfxSprite.Play(...).")]
        public int targetOrderOffset = 5;
    }

    [Header("Entries")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>
    /// Find best matching visual entry.
    /// Matching rules (in order):
    /// 1) Exact match: characterId + actionType + weaponStyle + abilityId
    /// 2) characterId + actionType + weaponStyle (ignores abilityId)
    /// 3) characterId + actionType + (weaponStyle None) + abilityId
    /// 4) characterId + actionType + (weaponStyle None) (generic)
    /// </summary>
    public bool TryGet(
        string characterId,
        VisualActionType actionType,
        WeaponStyle weaponStyle,
        string abilityId,
        out Entry entry)
    {
        entry = null;
        if (entries == null || entries.Count == 0) return false;
        if (string.IsNullOrWhiteSpace(characterId)) return false;

        string id = characterId.Trim().ToLowerInvariant();
        string ab = string.IsNullOrWhiteSpace(abilityId) ? string.Empty : abilityId.Trim().ToLowerInvariant();

        bool Match(Entry e,
                   bool requireWeaponStyle,
                   bool requireAbilityId)
        {
            if (e == null) return false;
            if (string.IsNullOrWhiteSpace(e.characterId)) return false;
            if (!string.Equals(e.characterId.Trim().ToLowerInvariant(), id, StringComparison.Ordinal)) return false;
            if (e.actionType != actionType) return false;

            if (requireWeaponStyle && e.weaponStyle != weaponStyle) return false;
            if (!requireWeaponStyle && e.weaponStyle != WeaponStyle.None) return false;

            if (requireAbilityId)
            {
                if (string.IsNullOrWhiteSpace(e.abilityId)) return false;
                if (!string.Equals(e.abilityId.Trim().ToLowerInvariant(), ab, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        // 1) exact
        for (int i = 0; i < entries.Count; i++)
            if (Match(entries[i], requireWeaponStyle: true, requireAbilityId: true))
                return (entry = entries[i]) != null;

        // 2) ignore abilityId
        for (int i = 0; i < entries.Count; i++)
            if (Match(entries[i], requireWeaponStyle: true, requireAbilityId: false))
                return (entry = entries[i]) != null;

        // 3) weaponStyle None + abilityId
        for (int i = 0; i < entries.Count; i++)
            if (Match(entries[i], requireWeaponStyle: false, requireAbilityId: true))
                return (entry = entries[i]) != null;

        // 4) generic
        for (int i = 0; i < entries.Count; i++)
            if (Match(entries[i], requireWeaponStyle: false, requireAbilityId: false))
                return (entry = entries[i]) != null;

        return false;
    }
}
