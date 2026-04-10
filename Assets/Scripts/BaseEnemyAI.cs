using System.Collections;
using UnityEngine;

public class BaseEnemyAI : MonoBehaviour
{
    public enum EnemyMode { Exploration, Battle }
    public EnemyMode currentMode = EnemyMode.Exploration;

    [Header("References")]
    public Transform player;                 // Player transform in the world

    [Header("Animation")]
    public Animator animator;                // assign in Inspector

    // Stats
    [Header("Stats")]
    public float health = 100f;             // Treated as max health
    public int attackDamage = 10;           // Base attack power in battle or overworld
    public float battleMeleeRange = 2f;     // How close is "in melee" for battle logic

    // Used for flee calculations and turn feel
    public int battleAgility = 10;

    private float currentHealth;

    // Hit / damage sprites
    [Header("Damage FX")]
    protected Rigidbody2D rb;
    public SpriteRenderer daggerdamageEffectRenderer;  // Separate SpriteRenderer for damage sprites
    public Sprite[] damageSprites;                     // Array to hold the 3 damage sprites
    public float damageFrameDuration = 0.1f;           // Duration to show each sprite

    private SpriteRenderer spriteRenderer;             // The enemy's regular SpriteRenderer

    [Header("VFX Anchor")]
    [Tooltip("Optional explicit anchor. If null, we try to find/create a child named 'VfxAnchor'.")]
    public Transform vfxAnchor;

    public bool IsAlive => currentHealth > 0f;

    protected virtual void Start()
    {
        currentHealth = health;

        rb = GetComponent<Rigidbody2D>();

        // Use the serialized animator if set; otherwise grab from this GameObject
        if (animator == null)
            animator = GetComponent<Animator>();

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (daggerdamageEffectRenderer != null)
            daggerdamageEffectRenderer.enabled = false;

        // Default to idle anim
        SetIdle(true);
    }

    protected virtual void Update()
    {
        // If there's a global game mode, respect it
        if (GameModeManager.Instance != null)
        {
            if (GameModeManager.Instance.IsExploration && currentMode != EnemyMode.Exploration)
                return;

            if (GameModeManager.Instance.IsBattle && currentMode != EnemyMode.Battle)
                return;
        }

        switch (currentMode)
        {
            case EnemyMode.Exploration:
                ExplorationUpdate();
                break;

            case EnemyMode.Battle:
                BattleUpdate();
                break;
        }
    }

    #region VFX Anchor

    private bool _loggedMissingVfxAnchor = false;

    private void ResolveVfxAnchorIfNeeded()
    {
        if (vfxAnchor != null)
            return;

        var found = transform.Find("VfxAnchor");
        if (found != null)
            vfxAnchor = found;
    }

    /// <summary>
    /// Returns the VFX anchor if it exists. Does NOT auto-create.
    /// </summary>
    public Transform GetVfxAnchor()
    {
        ResolveVfxAnchorIfNeeded();

        if (vfxAnchor == null)
        {
            if (!_loggedMissingVfxAnchor)
            {
                Debug.LogError($"[BaseEnemyAI] Missing required child Transform 'VfxAnchor' on enemy '{name}'. " +
                               $"VFX placement may be wrong until you add it.");
                _loggedMissingVfxAnchor = true;
            }
            return null; // caller decides fallback vs skip
        }

        return vfxAnchor;
    }

    #endregion

    #region Mode Switching

    public void EnterBattleMode()
    {
        currentMode = EnemyMode.Battle;

        // Stop any physics-based movement in exploration
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SetIdle(true);
    }

    public void ExitBattleMode()
    {
        currentMode = EnemyMode.Exploration;
        SetIdle(true);
        SetAttacking(false);
    }

    #endregion

    #region Exploration Logic (lightweight)

    protected virtual void ExplorationUpdate()
    {
        // For now, just face the player if they exist.
        // You can add patrolling / wandering in another script later.
        if (player != null)
        {
            FacePlayer();
        }
    }

    #endregion

    #region Battle Logic (visual only, logic driven by BattleManager)

    /// <summary>
    /// In battle, we usually just face the player / party camera.
    /// </summary>
    protected virtual void BattleUpdate()
    {
        if (player != null)
        {
            FacePlayer();
        }
    }

    /// <summary>
    /// Called by the BattleManager when it's this enemy's turn to do a basic attack.
    /// This only handles animation / timing. Damage is applied via animation event.
    /// </summary>
    public IEnumerator PlayBattleAttack()
    {
        if (!IsAlive)
            yield break;

        SetIdle(false);
        SetAttacking(true);

        // Let the animation play; in a proper setup you'll probably:
        //  - either wait for a specific state
        //  - or allow an animation event to signal completion.
        float animLength = 0.5f;
        if (animator != null)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            animLength = stateInfo.length;
        }

        yield return new WaitForSeconds(animLength);

        SetAttacking(false);
        SetIdle(true);
    }

    /// <summary>
    /// Convenience wrapper for the battle system: plays the attack animation as a coroutine.
    /// </summary>
    public void PlayBattleAttackAnimation()
    {
        StartCoroutine(PlayBattleAttack());
    }

    /// <summary>
    /// This should be called from your attack animation via an Animation Event.
    /// Think of it as "hit frame happened".
    /// </summary>
    public void AnimationEvent_ApplyDamage()
    {
        // If we're in battle, delegate to BattleManager so it can apply damage
        if (GameModeManager.Instance != null && GameModeManager.Instance.IsBattle)
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.OnEnemyHit(this);
            }
            return;
        }

        // Otherwise, in exploration, hit ExplorerHealth directly
        if (player != null)
        {
            ExplorerHealth explorerHealth = player.GetComponent<ExplorerHealth>();
            if (explorerHealth != null)
            {
                explorerHealth.TakeDamage(attackDamage);
            }
        }
    }

    // Called by legacy animation events that still use "ApplyDamage" as their function name.
    public void ApplyDamage()
    {
        AnimationEvent_ApplyDamage();
    }

    #endregion

    #region Helpers / Visuals

    protected void FacePlayer()
    {
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;
        // For a 2D billboard in 3D, you may only want to flip X; adjust as needed.
        direction.y = 0;
        transform.forward = direction;
    }

    private void SetIdle(bool value)
    {
        if (animator == null) return;
        animator.SetBool("isIdle", value);
    }

    private void SetAttacking(bool value)
    {
        if (animator == null) return;
        animator.SetBool("isAttacking", value);
    }

    public virtual void TakeDamage(int damage)
    {
        if (!IsAlive) return;

        currentHealth -= damage;
        Debug.Log($"Enemy {name} took damage! Current health: {currentHealth}");

        // Play 3-frame damage sprite animation
        if (daggerdamageEffectRenderer != null && damageSprites != null && damageSprites.Length > 0)
            StartCoroutine(PlayDamageEffect());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator PlayDamageEffect()
    {
        daggerdamageEffectRenderer.enabled = true;

        for (int i = 0; i < damageSprites.Length; i++)
        {
            daggerdamageEffectRenderer.sprite = damageSprites[i];
            yield return new WaitForSeconds(damageFrameDuration);
        }

        daggerdamageEffectRenderer.enabled = false;
    }

    protected virtual void Die()
    {
        Debug.Log($"Enemy {name} died!");

        // Optional: trigger death animation
        // if (animator != null) animator.SetTrigger("Die");

        // Let death anim play before destroy
        Destroy(gameObject, 2f);
    }

    #endregion
}
