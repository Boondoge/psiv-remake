using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a scale-pop animation on a RectTransform: X axis scales 0→1 (show) or 1→0 (hide).
/// Y stays at 1. Starting a new animation cancels any in-progress one.
/// The RectTransform's pivot must be (0.5, 0.5) for the pop to expand from centre.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UiPopAnimation : MonoBehaviour
{
    [SerializeField] private float showDuration = 0.15f;
    [SerializeField] private float hideDuration = 0.12f;

    private Coroutine _current;
    private RectTransform _rt;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void PlayShow(Action onComplete = null)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(AnimateScale(0f, 1f, showDuration, onComplete));
    }

    public void PlayHide(Action onComplete = null)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(AnimateScale(1f, 0f, hideDuration, onComplete));
    }

    public void StopAnimation()
    {
        if (_current != null)
        {
            StopCoroutine(_current);
            _current = null;
        }
    }

    private IEnumerator AnimateScale(float from, float to, float duration, Action onComplete)
    {
        Vector3 scale = _rt.localScale;
        scale.x = from;
        scale.y = 1f;
        _rt.localScale = scale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            scale.x = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            _rt.localScale = scale;
            yield return null;
        }

        scale.x = to;
        _rt.localScale = scale;
        _current = null;
        onComplete?.Invoke();
    }
}
