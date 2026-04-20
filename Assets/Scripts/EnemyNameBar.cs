using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a row of enemy name boxes at the top of the battle screen.
///
/// State machine:
///   Battle start          → ShowFromEnemies called by BattleUI.RefreshStatus
///   Command selection     → no change; boxes remain as-is (cache hit)
///   Action phase begins   → HideAll (boxes animate out)
///   Next command phase    → ShowFromEnemies rebuilds from living enemies
///
/// Layout:
///   N=1 → left edge
///   N=2 → left and right edges
///   N=3 → left, centre, right
///   N>3 → evenly distributed left→right
///
/// All boxes animate from their centre (pivot always 0.5,0.5).
/// A one-frame coroutine lets ContentSizeFitter measure actual box widths
/// before positioning. Padding from the screen edge is controlled by the
/// container's RectTransform left/right offsets in the inspector.
/// </summary>
public class EnemyNameBar : MonoBehaviour
{
    [SerializeField] private RectTransform container;
    [SerializeField] private GameObject nameBoxPrefab;


    private readonly List<EnemyNameBox> _boxes = new List<EnemyNameBox>();
    private readonly List<string> _cachedNames = new List<string>();

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Rebuilds the bar from enemies that are still alive.
    /// If the living-enemy names haven't changed since the last call, this is a no-op.
    /// </summary>
    public void ShowFromEnemies(List<BattleCombatant> enemies)
    {
        var names = BuildLivingNames(enemies);
        if (NamesMatch(names, _cachedNames)) return;
        StartCoroutine(BuildAndShow(names));
    }

    /// <summary>
    /// Clears the cache and animates all current boxes out, destroying them on completion.
    /// The next ShowFromEnemies call will always rebuild.
    /// </summary>
    public void HideAll()
    {
        _cachedNames.Clear();

        // Snapshot and clear the live list immediately so a ShowFromEnemies
        // that fires during the hide animation doesn't see stale boxes.
        var snapshot = new List<EnemyNameBox>(_boxes);
        _boxes.Clear();

        foreach (var box in snapshot)
        {
            if (box == null) continue;
            box.Hide(() => { if (box != null) Destroy(box.gameObject); });
        }
    }

    // ---------------------------------------------------------------
    // Build coroutine
    // ---------------------------------------------------------------

    private IEnumerator BuildAndShow(List<string> names)
    {
        _cachedNames.Clear();
        _cachedNames.AddRange(names);

        // Destroy boxes from the previous round.
        foreach (var box in _boxes)
            if (box != null) Destroy(box.gameObject);
        _boxes.Clear();

        if (names.Count == 0 || nameBoxPrefab == null)
            yield break;

        foreach (var name in names)
        {
            var go = Instantiate(nameBoxPrefab, container);
            var box = go.GetComponent<EnemyNameBox>();
            if (box == null)
            {
                Debug.LogError("[EnemyNameBar] nameBoxPrefab is missing an EnemyNameBox component.");
                Destroy(go);
                continue;
            }

            box.SetName(name);

            // Pre-collapse X scale to 0 so the box is invisible while we wait
            // for layout — avoids a one-frame flash before the pop-in animation.
            var rt = go.GetComponent<RectTransform>();
            var s = rt.localScale;
            s.x = 0f;
            rt.localScale = s;

            go.SetActive(true); // must be active for ContentSizeFitter to run
            _boxes.Add(box);
        }

        // Wait one frame so ContentSizeFitter can calculate each box's preferred width.
        yield return null;

        ApplyLayout(_boxes);

        // Animate each box in from scale 0 → 1 around its centre.
        foreach (var box in _boxes)
            box.PlayShowAnimation();
    }

    // ---------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------

    private void ApplyLayout(List<EnemyNameBox> boxes)
    {
        if (boxes == null || boxes.Count == 0) return;

        int n = boxes.Count;

        for (int i = 0; i < n; i++)
        {
            var rt = boxes[i].GetComponent<RectTransform>();
            if (rt == null) continue;

            // Normalised position: 0 = left edge, 1 = right edge.
            float normX = (n == 1) ? 0f : (float)i / (n - 1);

            // Anchor is a point (not a stretch), pivot always centre so the
            // pop animation expands symmetrically.
            rt.anchorMin = new Vector2(normX, 0.5f);
            rt.anchorMax = new Vector2(normX, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            // Edge boxes: shift the centre inward by (half-width + edgeMargin)
            // so the box edge sits edgeMargin pixels from the container edge.
            // Centre (and intermediate) boxes sit exactly on their anchor point.
            float halfW = rt.rect.width * 0.5f;
            float offsetX;
            if (normX < 0.01f)
                offsetX = halfW;        // left box: shift right so left edge sits at container edge
            else if (normX > 0.99f)
                offsetX = -halfW;       // right box: shift left so right edge sits at container edge
            else
                offsetX = 0f;           // centre or intermediate

            rt.anchoredPosition = new Vector2(offsetX, 0f);
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static List<string> BuildLivingNames(List<BattleCombatant> enemies)
    {
        var names = new List<string>();
        if (enemies == null) return names;
        foreach (var e in enemies)
            if (e.hp > 0 && !names.Contains(e.name))
                names.Add(e.name);
        return names;
    }

    private static bool NamesMatch(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
