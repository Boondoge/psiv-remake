using System;
using UnityEngine;
using TMPro;

/// <summary>
/// A single enemy name label with a pop-in/pop-out animation.
/// Attach to the name box prefab root alongside UiPopAnimation.
/// </summary>
[RequireComponent(typeof(UiPopAnimation))]
public class EnemyNameBox : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameLabel;

    private UiPopAnimation _popAnim;

    private void Awake()
    {
        _popAnim = GetComponent<UiPopAnimation>();
    }

    public void SetName(string enemyName)
    {
        if (nameLabel != null)
            nameLabel.text = enemyName;
    }

    /// <summary>Activates the box and plays the pop-in animation (X: 0→1).</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        _popAnim?.PlayShow();
    }

    /// <summary>
    /// Plays the pop-in animation on an already-active box.
    /// Used by EnemyNameBar after the layout coroutine activates the box itself.
    /// </summary>
    public void PlayShowAnimation()
    {
        _popAnim?.PlayShow();
    }

    /// <summary>Plays the pop-out animation (X: 1→0) then deactivates the box.</summary>
    public void Hide(Action onComplete = null)
    {
        _popAnim?.PlayHide(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    /// <summary>Stops any in-progress animation and immediately deactivates the box.</summary>
    public void CancelAndHide()
    {
        _popAnim?.StopAnimation();
        gameObject.SetActive(false);
    }
}
