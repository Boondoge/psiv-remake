using UnityEngine;

/// <summary>
/// Spawns damage/heal popups for both party members and enemies.
///
/// Canvas assumptions:
///   - The battle UI canvas is Screen Space Overlay (no camera reference needed for UI→screen conversion).
///   - Party popups: CastAnchor is already a RectTransform in the same canvas, so
///     Transform.position is already a screen-space point for an Overlay canvas.
///   - Enemy popups: VfxAnchor is a world-space Transform; Camera.main.WorldToScreenPoint
///     projects it to screen space. Only the X component is used — the Y is always
///     fixed via enemyPopupY so all enemy popups appear on the same horizontal band.
///     This works because battle enemies are positioned at known world X positions
///     and the battle camera does not move during a round.
///
/// Both paths then convert screen → canvas-local via
/// RectTransformUtility.ScreenPointToLocalPointInRectangle with a null camera
/// (correct for Screen Space Overlay).
/// </summary>
public class DamagePopupManager : MonoBehaviour
{
    [Header("Prefab + Canvas")]
    [SerializeField] private DamagePopup popupPrefab;

    [Tooltip("Root RectTransform of the Screen Space Overlay battle canvas. " +
             "Popups are instantiated here and positions are expressed in its local space.")]
    [SerializeField] private RectTransform canvasRect;

    [Tooltip("Camera used to project enemy world positions to screen space. " +
             "Assign your battle camera here. Falls back to Camera.main if left empty.")]
    [SerializeField] private Camera battleCamera;

    [Header("References")]
    [SerializeField] private PartyStripController partyStripController;

    [Header("Colours")]
    [SerializeField] private Color damageColor = Color.white;
    [SerializeField] private Color healColor   = new Color(1f, 0.55f, 0f); // orange

    [Header("Timing")]
    [Tooltip("Seconds the popup stays fully visible between its show and hide animations.")]
    [SerializeField] private float holdSeconds = 0.9f;

    [Header("Enemy Popup Y")]
    [Tooltip("Fixed canvas-local Y position used for ALL enemy damage popups. " +
             "Tune this in the inspector to sit at the right height above your enemies. " +
             "Only the world X of the enemy's VfxAnchor is used; Y is always this value.")]
    [SerializeField] private float enemyPopupY = 0f;

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Total seconds a popup lives: show animation + hold + hide animation.
    /// BattleManager uses this to know how long to wait after spawning a popup
    /// before starting the next action.
    /// </summary>
    public float TotalLifetimeSeconds
    {
        get
        {
            float animTime = 0f;
            if (popupPrefab != null)
            {
                var anim = popupPrefab.GetComponent<UiPopAnimation>();
                if (anim != null) animTime = anim.ShowDuration + anim.HideDuration;
            }
            return holdSeconds + animTime;
        }
    }


    /// <summary>
    /// Spawns a popup centred on the party member's CastAnchor tile.
    /// </summary>
    public void ShowCharacterDamage(int partyIndex, int amount, bool isHeal = false)
    {
        if (partyStripController == null) return;

        RectTransform anchor = partyStripController.GetCastAnchorForTileIndex(partyIndex);
        if (anchor == null)
        {
            Debug.LogWarning($"[DamagePopupManager] No CastAnchor for party index {partyIndex}.");
            return;
        }

        // For Screen Space Overlay, Transform.position is already in screen space.
        Vector2 canvasPos = ScreenToCanvasPos(anchor.position);
        Spawn(amount.ToString(), isHeal ? healColor : damageColor, canvasPos);
    }

    /// <summary>
    /// Spawns a popup above the enemy's VfxAnchor, at a fixed canvas Y for all enemies.
    /// </summary>
    public void ShowEnemyDamage(BaseEnemyAI enemy, int amount, bool isHeal = false)
    {
        if (enemy == null) return;

        Transform vfxAnchor = enemy.GetVfxAnchor();
        if (vfxAnchor == null)
        {
            Debug.LogWarning($"[DamagePopupManager] Enemy '{enemy.name}' has no VfxAnchor.");
            return;
        }

        // Project the world-space anchor to screen space using the perspective camera.
        Camera cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[DamagePopupManager] No camera assigned and Camera.main is null. " +
                             "Assign the battle camera to DamagePopupManager.battleCamera in the inspector.");
            return;
        }
        Vector3 screenPos = cam.WorldToScreenPoint(vfxAnchor.position);

        // Convert to canvas-local, then override Y with the fixed band.
        Vector2 canvasPos = ScreenToCanvasPos(screenPos);
        canvasPos.y = enemyPopupY;

        Spawn(amount.ToString(), isHeal ? healColor : damageColor, canvasPos);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void Spawn(string text, Color color, Vector2 canvasPos)
    {
        if (popupPrefab == null || canvasRect == null)
        {
            Debug.LogWarning("[DamagePopupManager] popupPrefab or canvasRect is not assigned.");
            return;
        }

        DamagePopup popup = Instantiate(popupPrefab, canvasRect);
        popup.GetComponent<RectTransform>().anchoredPosition = canvasPos;
        popup.Show(text, color, holdSeconds);
    }

    /// <summary>
    /// Converts a screen-space point to canvas-local coordinates.
    /// Passing null as the camera is correct for Screen Space Overlay.
    /// </summary>
    private Vector2 ScreenToCanvasPos(Vector3 screenPoint)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, null, out localPoint);
        return localPoint;
    }
}
