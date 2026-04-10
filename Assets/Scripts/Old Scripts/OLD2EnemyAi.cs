/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAi : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;
    public LayerMask whatIsGround, whatIsPlayer;

    // Patrolling
    public Vector3 walkPoint;
    bool walkPointSet;
    public float walkPointRange;

    // Attacking
    public float timeBetweenAttacks;
    bool alreadyAttacked;
    public List<AttackData> attacks; // List of different attacks

    // States
    public float sightRange;
    public bool playerInSightRange, playerInAttackRange;

    // Animation
    private Animator animator;

    private void Awake()
    {
        player = GameObject.Find("Player").transform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // Check for sight range
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);
        playerInAttackRange = false;

        // Check if player is within any attack range
        foreach (var attack in attacks)
        {
            if (Vector3.Distance(transform.position, player.position) <= attack.range)
            {
                playerInAttackRange = true;
                break;
            }
        }

        if (!playerInSightRange && !playerInAttackRange) Patrolling();
        if (playerInSightRange && !playerInAttackRange) ChasePlayer();
        if (playerInAttackRange && playerInSightRange) AttackPlayer();

        // Set idle animation if not walking or attacking
        if (!playerInSightRange && !playerInAttackRange && !walkPointSet)
        {
            animator.SetBool("IsWalking", false);
        }
    }

    private void Patrolling()
    {
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
        {
            agent.SetDestination(walkPoint);
            animator.SetBool("IsWalking", true);
            animator.ResetTrigger("Attack");
        }

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        // Walkpoint reached
        if (distanceToWalkPoint.magnitude < 1f)
        {
            walkPointSet = false;
            animator.SetBool("IsWalking", false);
        }
    }

    private void SearchWalkPoint()
    {
        // Calculate random point in range
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);

        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround))
            walkPointSet = true;
    }

    private void ChasePlayer()
    {
        // Continue to chase the player until within stopping distance
        if (!playerInAttackRange)
        {
            agent.SetDestination(player.position);
            animator.SetBool("IsWalking", true);
            animator.ResetTrigger("Attack");
        }
    }

    private void AttackPlayer()
    {
        // Select an appropriate attack
        AttackData selectedAttack = null;
        foreach (var attack in attacks)
        {
            if (Vector3.Distance(transform.position, player.position) <= attack.range)
            {
                selectedAttack = attack;
                break;
            }
        }

        if (selectedAttack != null)
        {
            // Make sure the enemy doesn't move
            agent.SetDestination(transform.position);
            animator.SetBool("IsWalking", false);

            transform.LookAt(player);

            if (!alreadyAttacked)
            {
                // Trigger the attack animation
                animator.SetTrigger("Attack");
                alreadyAttacked = true;

                // Apply damage after a delay to sync with the animation
                if (selectedAttack.isRanged)
                {
                    Invoke(nameof(ApplyDamage), selectedAttack.delay); // Adjust the delay to match the animation if needed
                }
                else
                {
                    ApplyDamage(); // For melee, apply damage immediately if the player is in range
                }

                // Reset attack after the full attack interval
                Invoke(nameof(ResetAttack), timeBetweenAttacks);
            }
        }
        else
        {
            // If no attack is selected, move closer for melee attacks
            agent.SetDestination(player.position);
        }
    }

    // This function will be called by the animation event or directly
    public void ApplyDamage()
    {
        Debug.Log("ApplyDamage called"); // Debug to confirm the method is being called
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Debug.Log("PlayerHealth component found");
            // Find the appropriate attack that was applied
            AttackData selectedAttack = null;
            foreach (var attack in attacks)
            {
                if (Vector3.Distance(transform.position, player.position) <= attack.range)
                {
                    selectedAttack = attack;
                    break;
                }
            }
            playerHealth.TakeDamage(selectedAttack.damage);
            Debug.Log("Damage applied: " + selectedAttack.damage);
            Debug.Log("Player Health: " + playerHealth.currentHealth);
        }
        else
        {
            Debug.LogError("PlayerHealth component not found on player object");
        }
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
        animator.ResetTrigger("Attack");
    }
}
*/