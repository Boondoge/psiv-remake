/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform player;
    public float sightRange = 10f;
    public float attackRange = 2f;
    public float health = 100f;
    public float moveSpeed = 2f;
    public float retreatSpeed = 1f;
    public AttackData meleeAttack;
    public AttackData rangedAttack;
    public Animator animator;
    public Transform[] patrolPoints;

    private float currentHealth;
    private bool isAttacking;
    private bool isRetreating;
    private int currentPatrolIndex;
    private NavMeshAgent agent;
    private string personality;

    void Start()
    {
        currentHealth = health;
        isAttacking = false;
        isRetreating = false;
        currentPatrolIndex = 0;
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        Patrol();
    }

    void Update()
    {
        if (isRetreating) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (currentHealth <= health * 0.1f)
        {
            StartCoroutine(RetreatAndLunge());
        }
        else if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (distanceToPlayer <= sightRange)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }

        FacePlayer();
    }

    void Patrol()
    {
        if (patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            agent.destination = patrolPoints[currentPatrolIndex].position;
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }

        animator.SetBool("isWalking", true);
        animator.SetBool("isIdle", false);
        animator.SetBool("isAttacking", false);
    }

    void ChasePlayer()
    {
        agent.destination = player.position;
        animator.SetBool("isWalking", true);
        animator.SetBool("isIdle", false);
        animator.SetBool("isAttacking", false);
    }

    void AttackPlayer()
    {
        if (isAttacking) return;

        isAttacking = true;
        agent.isStopped = true;
        animator.SetBool("isWalking", false);
        animator.SetBool("isIdle", false);
        animator.SetTrigger("attack");

        StartCoroutine(PerformAttack());
    }

    IEnumerator PerformAttack()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        AttackData chosenAttack = distanceToPlayer <= meleeAttack.range ? meleeAttack : rangedAttack;

        yield return new WaitForSeconds(chosenAttack.attackRate); // Adjust according to your animation timing

        // The ApplyDamage() method will be called via animation event
        ResetAttack();
    }

    public void ApplyDamage()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= meleeAttack.range)
        {
            player.GetComponent<PlayerHealth>().TakeDamage(meleeAttack.damage);
        }
        else if (distanceToPlayer <= rangedAttack.range)
        {
            if (rangedAttack.projectilePrefab != null)
            {
                GameObject projectile = Instantiate(rangedAttack.projectilePrefab, transform.position, Quaternion.identity);
                projectile.GetComponent<Projectile>().SetTarget(player);
            }
        }
    }

    IEnumerator RetreatAndLunge()
    {
        isRetreating = true;
        agent.isStopped = false;
        agent.speed = retreatSpeed;
        animator.SetBool("isWalking", true);
        float retreatDistance = 3f;
        Vector3 retreatDirection = (transform.position - player.position).normalized;
        Vector3 retreatTarget = transform.position + retreatDirection * retreatDistance;

        agent.destination = retreatTarget;

        yield return new WaitForSeconds(retreatDistance / retreatSpeed);

        animator.SetBool("isWalking", false);
        agent.speed = moveSpeed * 2;
        Vector3 lungeTarget = player.position + (player.position - transform.position).normalized * 2f;
        agent.destination = lungeTarget;

        // Ensure WaitForSeconds receives a float
        float lungeDuration = Vector3.Distance(transform.position, lungeTarget) / agent.speed;
        yield return new WaitForSeconds(lungeDuration);

        if (Vector3.Distance(transform.position, player.position) < 0.5f) // Adjust the collision distance threshold as needed
        {
            // Trigger new scene
        }
        else
        {
            yield return new WaitForSeconds(5f);
            isRetreating = false;
            agent.speed = moveSpeed;
            Patrol();
        }
    }

    void FacePlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Ensure the enemy stays upright
        transform.forward = direction;
    }

    public void SetPersonality(string newPersonality)
    {
        personality = newPersonality;
        // Add logic to adjust behavior based on personality
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void ResetAttack()
    {
        isAttacking = false;
        animator.ResetTrigger("attack");
        agent.isStopped = false;
    }
}
*/