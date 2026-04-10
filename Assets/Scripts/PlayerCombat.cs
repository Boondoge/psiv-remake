using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public int attackDamage = 20;
    public float attackRange = 2f;
    public LayerMask enemyLayers;

    public Transform attackPoint;
    public float attackCooldown = 1f;  // Time between attacks
    private float attackTimer;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        attackTimer = 0f;
    }

    void Update()
    {
        // Decrease the attack cooldown timer
        attackTimer -= Time.deltaTime;

        // Check for input (e.g., left mouse button or spacebar) and cooldown status
        if (Input.GetButtonDown("Fire1") && attackTimer <= 0f)
        {
            Debug.Log("Player is attacking!");  // Log to confirm attack trigger
            Attack();
            attackTimer = attackCooldown; // Reset attack timer
        }
    }

    void Attack()
    {
        // Play attack animation
        //animator.SetTrigger("Attack");

        // Detect enemies in range of the attack using Physics.OverlapSphere for 3D physics
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers);

        if (hitEnemies.Length == 0)
        {
            Debug.Log("No enemies hit.");  // Log if no enemies are in range
        }

        // Apply damage to all enemies hit
        foreach (Collider enemy in hitEnemies)
        {
            BaseEnemyAI enemyAI = enemy.GetComponent<BaseEnemyAI>();
            if (enemyAI != null)
            {
                Debug.Log("Enemy hit: " + enemyAI.gameObject.name);  // Log when an enemy is hit
                enemyAI.TakeDamage(attackDamage);  // Call the enemy's TakeDamage function
            }
        }
    }

    // Draw the attack range in the editor for better visualization
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
