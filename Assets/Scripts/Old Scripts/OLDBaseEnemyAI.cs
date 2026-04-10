/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BaseEnemyAI : MonoBehaviour
{
    public enum EnemyState { Idle, Attack, Cooldown }
    public EnemyState currentState;

    public Transform player;
    
    //Attacking
    public float attackRange = 2f;
    public float attackCooldown = 2f; // Time to wait between attacks in seconds
    public int attackDamage = 10;
    private bool isCooldownActive;
    

    //Death
    public float health = 100f;
    private float currentHealth;

    //HitAnimations
    protected Rigidbody2D rb;
    public SpriteRenderer daggerdamageEffectRenderer;  // Separate SpriteRenderer for damage sprites
    public Sprite[] damageSprites;               // Array to hold the 3 damage sprites
    public float damageFrameDuration = 0.1f;     // Duration to show each sprite
    private SpriteRenderer spriteRenderer;        // The enemy's regular SpriteRenderer

    protected Animator animator; // Animator for handling animations

    protected virtual void Start()
    {
        currentHealth = health;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>(); // Get the Animator component
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Initially hide the damage effect renderer
        daggerdamageEffectRenderer.enabled = false;

        currentState = EnemyState.Idle; // Start with Idle state
    }

    protected virtual void Update()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
            case EnemyState.Cooldown:
                HandleCooldown();
                break;
        }

        // Always face the player
        FacePlayer();
    }

    protected virtual void HandleIdle()
    {
        // Play idle animation
        animator.SetBool("isIdle", true);
        animator.SetBool("isAttacking", false);

        // Transition to Attack state if the player is within attack range
        if (Vector2.Distance(transform.position, player.position) <= attackRange && !isCooldownActive)
        {
            currentState = EnemyState.Attack;
        }
    }

    protected virtual void HandleAttack()
    {
        
        // Play attack animation
        animator.SetBool("isIdle", false);
        animator.SetBool("isAttacking", true);
            
        // The ApplyDamage() method will be called via animation event
        Debug.Log("Attacking the player!");
        
        // Ensure that this attack process happens once per attack cycle
        if (!isCooldownActive)
        {
            // Set cooldown to active
            isCooldownActive = true;

            // Start cooldown after the attack animation finishes
            StartCoroutine(HandleAttackAndCooldown());
        }
        
        // If the player moves out of attack range, return to Idle state
        if (Vector2.Distance(transform.position, player.position) > attackRange)
        {
            currentState = EnemyState.Idle;
        }
        
    }
 private IEnumerator HandleAttackAndCooldown()
    {
        // Wait for the attack animation to finish
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // After the attack, transition to Cooldown state
        currentState = EnemyState.Cooldown;

        // Wait for the cooldown duration
        yield return new WaitForSeconds(attackCooldown);

        // Reset the cooldown flag
        isCooldownActive = false;

        // Return to Idle state
        currentState = EnemyState.Idle;
    }
    protected virtual void HandleCooldown()
    {
        // During cooldown, ensure the enemy doesn't perform any attacks
        animator.SetBool("isIdle", true);
        animator.SetBool("isAttacking", false);
    }

    public void ApplyDamage()
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }   
    }


    protected void FacePlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Ensure the enemy stays upright
        transform.forward = direction;
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Enemy took damage! Current health: " + currentHealth);  // Log enemy health after damage

        // Optionally, play hurt animation here
        //animator.SetTrigger("Hurt");

        // Play the 3-frame damage sprite animation
        StartCoroutine(PlayDamageEffect());

        // Check if health is below zero and handle death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator PlayDamageEffect()
    {
        // Enable the damage effect renderer
        daggerdamageEffectRenderer.enabled = true;

        // Loop through the damage sprites and display each for a short duration
        for (int i = 0; i < damageSprites.Length; i++)
        {
            daggerdamageEffectRenderer.sprite = damageSprites[i];  // Set the current frame
            yield return new WaitForSeconds(damageFrameDuration);  // Wait for the frame duration
        }

        // Hide the damage effect renderer after the animation is done
        daggerdamageEffectRenderer.enabled = false;
    }

    protected virtual void Die()
    {
        // Play death animation
        //animator.SetTrigger("Die");
        Debug.Log("Enemy died!");

        // Handle enemy death after the animation completes (e.g., destroy the object)
        Destroy(gameObject, 2f); // Delay to allow the death animation to play
    }
}*/