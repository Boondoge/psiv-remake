using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Single-use damage/heal number that pops in, holds, then pops out and destroys itself.
///
/// Lifecycle: show-animate (UiPopAnimation) → hold → hide-animate → Destroy.
///
/// Spawn via DamagePopupManager.ShowEnemyDamage / ShowCharacterDamage — do not
/// instantiate or call Show() directly from other systems.
/// </summary>
[RequireComponent(typeof(UiPopAnimation))]
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    private UiPopAnimation _popAnim;

    private void Awake()
    {
        _popAnim = GetComponent<UiPopAnimation>();
    }

    /// <summary>
    /// Sets the displayed text and colour, then runs the full popup lifecycle.
    /// Call once immediately after instantiation.
    /// </summary>
    public void Show(string text, Color color, float holdSeconds)
    {
        if (label != null)
        {
            label.text  = text;
            label.color = color;
        }

        StartCoroutine(Lifecycle(holdSeconds));
    }

    private IEnumerator Lifecycle(float holdSeconds)
    {
        // Show: scale 0 → 1
        bool showDone = false;
        _popAnim.PlayShow(() => showDone = true);
        yield return new WaitUntil(() => showDone);

        // Hold
        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        // Hide: scale 1 → 0
        bool hideDone = false;
        _popAnim.PlayHide(() => hideDone = true);
        yield return new WaitUntil(() => hideDone);

        Destroy(gameObject);
    }
}
