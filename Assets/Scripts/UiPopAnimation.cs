using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a scale-pop animation on a RectTransform: X axis scales 0→1 (show) or 1→0 (hide).
/// Y stays at 1. Starting a new animation cancels any in-progress one.
/// The RectTransform's pivot must be (0.5, 0.5) for the pop to expand from centre.
///
/// Any Graphic components on child objects (e.g. TMP text labels) are automatically
/// hidden while animating — invisible during expand, revealed only when fully open,
/// and immediately hidden the moment collapse begins. No inspector wiring required.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UiPopAnimation : MonoBehaviour
{
    [SerializeField] private float showDuration = 0.15f;
    [SerializeField] private float hideDuration = 0.12f;

    public float ShowDuration => showDuration;
    public float HideDuration => hideDuration;

    [Tooltip("If true, both X and Y scale animate together (true pop). " +
             "If false, only X animates (horizontal wipe). " +
             "Use true for damage popups, false for name bar boxes.")]
    [SerializeField] private bool animateBothAxes = false;

    private Coroutine _current;
    private RectTransform _rt;
    private Graphic[] _childGraphics;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Collect all Graphics on child objects (not the root itself).
        // These are always the content labels — the root holds the background Image.
        var all = GetComponentsInChildren<Graphic>(includeInactive: true);
        var rootGraphics = GetComponents<Graphic>();
        var children = new System.Collections.Generic.List<Graphic>();
        foreach (var g in all)
        {
            bool onRoot = false;
            foreach (var rg in rootGraphics)
                if (g == rg) { onRoot = true; break; }
            if (!onRoot) children.Add(g);
        }
        _childGraphics = children.ToArray();
    }

    public void PlayShow(Action onComplete = null)
    {
        if (_current != null) StopCoroutine(_current);
        SetContentVisible(false);
        _current = StartCoroutine(AnimateScale(0f, 1f, showDuration, () =>
        {
            SetContentVisible(true);
            onComplete?.Invoke();
        }));
    }

    public void PlayHide(Action onComplete = null)
    {
        if (_current != null) StopCoroutine(_current);
        SetContentVisible(false);
        _current = StartCoroutine(AnimateScale(1f, 0f, hideDuration, onComplete));
    }

    /// <summary>
    /// Activates the GameObject and plays the show animation.
    /// Safe to call even if already visible — restarts the animation.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        PlayShow();
    }

    /// <summary>
    /// Plays the hide animation, then deactivates the GameObject when complete.
    /// No-op if already inactive.
    /// </summary>
    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        PlayHide(() => gameObject.SetActive(false));
    }

    public void StopAnimation()
    {
        if (_current != null)
        {
            StopCoroutine(_current);
            _current = null;
        }
    }

    private void SetContentVisible(bool visible)
    {
        if (_childGraphics == null) return;
        foreach (var g in _childGraphics)
            if (g != null) g.enabled = visible;
    }

    private IEnumerator AnimateScale(float from, float to, float duration, Action onComplete)
    {
        Vector3 scale = _rt.localScale;
        scale.x = from;
        scale.y = animateBothAxes ? from : 1f;
        _rt.localScale = scale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float v = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            scale.x = v;
            scale.y = animateBothAxes ? v : 1f;
            _rt.localScale = scale;
            yield return null;
        }

        scale.x = to;
        scale.y = animateBothAxes ? to : 1f;
        _rt.localScale = scale;
        _current = null;
        onComplete?.Invoke();
    }
}
