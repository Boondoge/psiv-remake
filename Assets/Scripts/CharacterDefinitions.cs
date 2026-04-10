using UnityEngine;

[CreateAssetMenu(
    menuName = "PSIV/Character Definition",
    fileName = "NewCharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique ID, e.g. \"chaz\", \"alys\", \"hahn\".")]
    public string characterId;
    [Tooltip("Display name to show in UI if no override is given.")]
    public string displayName;

    [Tooltip("Full class name, e.g. \"Hunter\", \"Ranger\", \"Techter\".")]
    public string className;

    [Tooltip("Short class code for badges, e.g. \"Hu.\", \"Ra.\", \"Te.\"")]
    public string classCode;

    [Header("Battle UI")]
    public Sprite battlePortrait;

    [Header("Leveling")]
    [Tooltip("Level that the base stats below correspond to (usually 1).")]
    public int baseLevel = 1;

    [Header("Base stats at base level")]
    public int baseMaxHP = 100;
    public int baseMaxTP = 20;
    public int baseStrength = 10;
    public int baseMental = 10;
    public int baseAgility = 10;
    public int baseDexterity = 10;

    [Header("Baseline per-level growth (NO modifiers yet)")]
    [Tooltip("How much max HP increases per level above baseLevel, before behavior modifiers.")]
    public int hpPerLevel = 5;

    [Tooltip("How much max TP increases per level above baseLevel, before behavior modifiers.")]
    public int tpPerLevel = 2;

    [Tooltip("How much STR increases per level above baseLevel, before behavior modifiers.")]
    public int strPerLevel = 1;

    [Tooltip("How much MTL increases per level above baseLevel, before behavior modifiers.")]
    public int mtlPerLevel = 1;

    [Tooltip("How much AGI increases per level above baseLevel, before behavior modifiers.")]
    public int agiPerLevel = 1;

    [Tooltip("How much DEX increases per level above baseLevel, before behavior modifiers.")]
    public int dexPerLevel = 1;
}
