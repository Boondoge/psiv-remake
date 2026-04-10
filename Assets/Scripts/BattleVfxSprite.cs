using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class BattleVfxSprite : MonoBehaviour
{
    [Header("Tuning")]
    [Tooltip("How long the VFX should stay alive after Play() is called. This should match the clip length (seconds).")]
    public float durationSeconds = 0.30f;

    [Header("Facing")]
    [Tooltip("If enabled, face the same direction as the target SpriteRenderer's transform (helps avoid 'backside' mirroring if the target is oriented correctly).")]
    public bool matchTargetRotation = true;

    [Header("Lifetime")]
    [Tooltip("If true, the VFX object destroys itself after durationSeconds. If false, the caller must destroy it.")]
    public bool autoDestroyAfterDuration = true;

    private SpriteRenderer _sr;
    private Animator _anim;
    private Coroutine _dieRoutine;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _anim = GetComponent<Animator>();
    }

    public void Play(Transform anchor, SpriteRenderer targetRenderer, int orderOffset = 5)
    {
        if (anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = anchor.position;

        if (matchTargetRotation && targetRenderer != null)
            transform.rotation = targetRenderer.transform.rotation;

        if (targetRenderer != null)
        {
            _sr.sortingLayerID = targetRenderer.sortingLayerID;
            _sr.sortingOrder = targetRenderer.sortingOrder + orderOffset;
        }

        _anim.Rebind();
        _anim.Update(0f);
        _anim.Play(0, 0, 0f);

        if (_dieRoutine != null) StopCoroutine(_dieRoutine);

        if (autoDestroyAfterDuration && durationSeconds > 0f)
            _dieRoutine = StartCoroutine(DieAfterSeconds());
    }

    private IEnumerator DieAfterSeconds()
    {
        yield return new WaitForSeconds(durationSeconds);
        Destroy(gameObject);
    }
}
