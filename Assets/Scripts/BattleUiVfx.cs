using UnityEngine;

/// <summary>
/// Simple helper for UI-based battle VFX/prefabs (cast poses, buff glows, etc.).
/// Lets BattleUI know how long the prefab should remain visible.
/// </summary>
public class BattleUiVfx : MonoBehaviour
{
    [Tooltip("How long this UI VFX should remain visible before being cleaned up.")]
    public float durationSeconds = 0.45f;
}
