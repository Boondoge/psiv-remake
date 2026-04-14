using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the PartyStrip root (the parent of the CharTiles).
/// Owns:
///   - Global status icons (unknown, KO)
///   - References to the PartyStripEntry tiles
///   - Provides spawn anchors (CastAnchor) per tile for BattleUI
///   - Provides portrait hit flash trigger for enemy -> hero impacts
///
/// Tile index i is assumed to map directly to logical party index i.
/// If you want a different visual layout, reorder the tiles list in the inspector.
/// </summary>
public class PartyStripController : MonoBehaviour
{
    [Header("Party Tiles (index = party index)")]
    [SerializeField] private List<PartyStripEntry> tiles = new List<PartyStripEntry>();

    [Header("Global Status Icons")]
    [Tooltip("Icon when no command is chosen yet this round (pre-selection for an ALIVE combatant).")]
    [SerializeField] private Sprite unknownStatusSprite;

    [Tooltip("Icon when a combatant is KO (HP <= 0).")]
    [SerializeField] private Sprite koStatusSprite;

    // Cached per-tile cast anchors (RectTransform) found by name "CastAnchor".
    private readonly List<RectTransform> _castAnchors = new List<RectTransform>();

    private void Awake()
    {
        CacheCastAnchors();
    }

    private void OnValidate()
    {
        CacheCastAnchors();
    }

    private void CacheCastAnchors()
    {
        _castAnchors.Clear();

        if (tiles == null)
            return;

        for (int i = 0; i < tiles.Count; i++)
        {
            RectTransform anchor = null;

            if (tiles[i] != null)
            {
                // Look for a child named "CastAnchor" anywhere under the tile.
                var t = FindChildRecursive(tiles[i].transform, "CastAnchor");
                anchor = t != null ? t.GetComponent<RectTransform>() : null;
            }

            _castAnchors.Add(anchor);
        }
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Returns the CastAnchor for the given tile index (party index).
    /// Used to spawn caster-pose UI VFX above the character portrait.
    /// </summary>
    public RectTransform GetCastAnchorForTileIndex(int tileIndex)
    {
        if (_castAnchors == null || _castAnchors.Count == 0)
            CacheCastAnchors();

        if (tileIndex < 0 || tileIndex >= _castAnchors.Count)
            return null;

        return _castAnchors[tileIndex];
    }

    /// <summary>
    /// Initialize the strip to show all tiles as empty.
    /// Call this when entering battle or when you want a clean slate.
    /// </summary>
    public void InitializeEmpty()
    {
        if (tiles == null) return;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] != null)
                tiles[i].SetEmpty();
        }
    }

    /// <summary>
    /// Makes a single tile's portrait visible without touching any other strip state.
    /// Used to reveal the targeted party member before an enemy attack animation plays.
    /// </summary>
    public void ShowPortraitForIndex(int partyIndex)
    {
        if (tiles == null || partyIndex < 0 || partyIndex >= tiles.Count) return;
        tiles[partyIndex]?.SetPortraitVisible(true);
    }

    /// <summary>
    /// Enemy -> hero portrait hit flash helper.
    /// Returns false if the tile/index is invalid.
    /// </summary>
    public bool TryPlayPortraitHitFlash(int partyIndex, int pulses, float onSeconds, float offSeconds)
    {
        if (tiles == null) return false;
        if (partyIndex < 0 || partyIndex >= tiles.Count) return false;
        if (tiles[partyIndex] == null) return false;

        tiles[partyIndex].PlayPortraitHitFlash(pulses, onSeconds, offSeconds);
        return true;
    }

    /// <summary>
    /// Updates the party strip.
    ///
    /// activeHeroIndex:
    ///   -2 = action phase: show NO portraits
    ///   -1 = battle start: show ALL portraits
    ///  >= 0 = command selection: show only that hero's portrait
    /// </summary>
    public void RefreshStrip(
        List<BattleCombatant> players,
        int activeHeroIndex,
        IList<Sprite> perHeroCommandIcons,
        IList<string> perHeroClassCodes,
        IList<Color> perHeroBadgeColors,
        IList<Color> perHeroBadgeTextColors)
    {
        if (tiles == null || tiles.Count == 0)
            return;

        int playerCount = (players != null) ? players.Count : 0;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile == null)
                continue;

            // No combatant for this slot -> empty party slot.
            if (i < 0 || i >= playerCount || players[i] == null)
            {
                tile.SetEmpty();
                continue;
            }

            BattleCombatant combatant = players[i];
            bool isActiveHero = (i == activeHeroIndex);
            bool isKo = (combatant.hp <= 0);

            // -2 = action phase: show no portraits
            // -1 = battle start: show all portraits
            // >= 0 = command selection: show only the active hero
            bool showPortrait = activeHeroIndex == -1 || isActiveHero;

            // --- Resolve status sprite ---
            Sprite statusSprite = null;

            if (isKo && koStatusSprite != null)
            {
                statusSprite = koStatusSprite;
            }
            else
            {
                if (perHeroCommandIcons != null &&
                    i >= 0 &&
                    i < perHeroCommandIcons.Count)
                {
                    statusSprite = perHeroCommandIcons[i];
                }

                if (statusSprite == null && unknownStatusSprite != null)
                {
                    statusSprite = unknownStatusSprite;
                }
            }

            // --- Resolve class visuals (optional) ---
            string classCode = string.Empty;
            Color badgeColor = Color.clear;
            Color badgeTextColor = Color.white;

            if (perHeroClassCodes != null && i < perHeroClassCodes.Count)
                classCode = perHeroClassCodes[i];

            if (perHeroBadgeColors != null && i < perHeroBadgeColors.Count)
                badgeColor = perHeroBadgeColors[i];

            if (perHeroBadgeTextColors != null && i < perHeroBadgeTextColors.Count)
                badgeTextColor = perHeroBadgeTextColors[i];

            tile.SetFromCombatant(
                combatant,
                isActiveHero,
                showPortrait,
                statusSprite,
                classCode,
                badgeColor,
                badgeTextColor
            );
        }
    }
}
