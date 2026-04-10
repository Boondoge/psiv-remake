using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side (or combatant-side) visual hit reaction.
///
/// Baseline PSIV behavior (current scope):
/// - Flash affects the current sprite frame while animation continues (no animator pause).
/// - Uses shader property _FlashAmount (works with your invert/preserve-lines Shader Graph materials).
/// - Flashes ALL SpriteRenderers in this prefab hierarchy (group flash).
///
/// Notes:
/// - Which *look* you get (invert vs preserve-lines) is determined by the materials assigned
///   to the SpriteRenderers on the prefab, not by this component.
/// - Future: we can add tint-based elemental flashes here without changing BattleUI flow.
/// </summary>
[DisallowMultipleComponent]
public class HitReactionGroup : MonoBehaviour
{
    [Header("Shader Flash")]
    [Tooltip("Shader property name driven by this component.")]
    [SerializeField] private string flashAmountProperty = "_FlashAmount";

    [Tooltip("Value to set when flash is 'on'. Typically 1.")]
    [SerializeField] private float flashOnValue = 1f;

    [Tooltip("Value to set when flash is 'off'. Typically 0.")]
    [SerializeField] private float flashOffValue = 0f;

    private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>(16);
    private int _flashAmountId;

    // Reusable MPB; we still Get/Set per renderer to preserve other MPB values.
    private MaterialPropertyBlock _mpb;
    private Coroutine _running;

    private void Awake()
    {
        _flashAmountId = Shader.PropertyToID(flashAmountProperty);
        _mpb = new MaterialPropertyBlock();
        CacheRenderers();
    }

    private void OnEnable()
    {
        CacheRenderers();
        SetFlashAmount(flashOffValue);
    }

    private void OnDisable()
    {
        StopAllReactions();
        SetFlashAmount(flashOffValue);
    }

    /// <summary>Rebuild cached renderer list. Call if you dynamically add/remove renderers.</summary>
    public void CacheRenderers()
    {
        _renderers.Clear();
        GetComponentsInChildren(true, _renderers);
    }

    /// <summary>
    /// Start a hit flash (non-blocking). Stops any existing reaction to prevent overlap.
    /// </summary>
    public Coroutine PlayHitFlash(int pulseCount, float onSeconds, float offSeconds)
    {
        StopAllReactions();
        _running = StartCoroutine(CoHitFlash(pulseCount, onSeconds, offSeconds));
        return _running;
    }

    /// <summary>
    /// Blocking version (yieldable). Use this from BattleUI sequencing.
    /// </summary>
    public IEnumerator PlayHitFlashRoutine(int pulseCount, float onSeconds, float offSeconds)
    {
        StopAllReactions();
        yield return CoHitFlash(pulseCount, onSeconds, offSeconds);
    }

    public void StopAllReactions()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
        SetFlashAmount(flashOffValue);
    }

    private IEnumerator CoHitFlash(int pulseCount, float onSeconds, float offSeconds)
    {
        int n = Mathf.Max(0, pulseCount);
        float onT = Mathf.Max(0f, onSeconds);
        float offT = Mathf.Max(0f, offSeconds);

        for (int i = 0; i < n; i++)
        {
            SetFlashAmount(flashOnValue);
            if (onT > 0f) yield return new WaitForSeconds(onT);

            SetFlashAmount(flashOffValue);
            if (offT > 0f) yield return new WaitForSeconds(offT);
        }

        SetFlashAmount(flashOffValue);
        _running = null;
    }

    private void SetFlashAmount(float value)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            var sr = _renderers[i];
            if (sr == null) continue;

            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_flashAmountId, value);
            sr.SetPropertyBlock(_mpb);
        }
    }
}
