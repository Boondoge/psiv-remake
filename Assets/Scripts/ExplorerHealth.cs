using UnityEngine;

/// <summary>
/// HP for the physical explorer avatar in the dungeon.
/// Mirrors the current party leader's HP (via PartyManager) and
/// forwards any exploration damage back into the party.
/// </summary>
public class ExplorerHealth : MonoBehaviour
{
    [Header("Initialization")]
    [Tooltip("If true, pulls max/current HP from PartyManager's leader on Start.")]
    public bool initializeFromPartyLeader = true;

    [Tooltip("Fallback max HP if there's no party leader.")]
    public int startingMaxHealth = 100;

    [Tooltip("Fallback current HP if there's no party leader.")]
    public int startingCurrentHealth = 100;

    [Header("Runtime (read-only in Inspector)")]
    [SerializeField] private int maxHealth;
    [SerializeField] private int currentHealth;

    public int MaxHealth
    {
        get => maxHealth;
        private set => maxHealth = Mathf.Max(1, value);
    }

    public int CurrentHealth
    {
        get => currentHealth;
        private set => currentHealth = Mathf.Clamp(value, 0, MaxHealth);
    }

    private void Start()
    {
        // Try to sync from party leader if configured
        if (initializeFromPartyLeader && PartyManager.Instance != null)
        {
            var leader = PartyManager.Instance.GetLeader();
            if (leader != null)
            {
                // Use computed max HP based on CharacterDefinition + level
                int leaderMaxHp = leader.GetMaxHP();
                MaxHealth = leaderMaxHp;

                // If leader.currentHP is 0 or less (e.g. new game), treat as full
                int leaderCurrentHp = leader.currentHP <= 0
                    ? leaderMaxHp
                    : Mathf.Clamp(leader.currentHP, 0, leaderMaxHp);

                CurrentHealth = leaderCurrentHp;
                return;
            }
        }

        // Fallback if no leader or not initializing from party
        MaxHealth = startingMaxHealth;
        CurrentHealth = Mathf.Clamp(startingCurrentHealth, 0, MaxHealth);
    }

    /// <summary>
    /// Apply exploration damage (projectiles, melee, traps).
    /// Accepts float for convenience and rounds internally.
    /// </summary>
    public void TakeDamage(float damage)
    {
        int dmg = Mathf.RoundToInt(damage);
        if (dmg < 0) dmg = 0;

        CurrentHealth -= dmg;

        Debug.Log($"Explorer took {dmg} damage. HP = {CurrentHealth}/{MaxHealth}");

        // Mirror into party leader data
        SyncToPartyLeader();

        if (CurrentHealth <= 0)
        {
            OnDeath();
        }
    }

    /// <summary>
    /// Int overload for anything that passes ints (e.g. melee).
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage);
    }

    /// <summary>
    /// Heal the explorer (and leader) by a certain amount.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;

        CurrentHealth += amount;
        Debug.Log($"Explorer healed {amount}. HP = {CurrentHealth}/{MaxHealth}");

        SyncToPartyLeader();
    }

    /// <summary>
    /// Sync CurrentHealth back into the party leader, if one exists.
    /// </summary>
    private void SyncToPartyLeader()
    {
        if (PartyManager.Instance == null) return;

        var leader = PartyManager.Instance.GetLeader();
        if (leader == null) return;

        int leaderMaxHp = leader.GetMaxHP();
        leader.currentHP = Mathf.Clamp(CurrentHealth, 0, leaderMaxHp);
    }

    /// <summary>
    /// Optional helper if you ever need to refresh from party leader again
    /// (e.g. after battle ends).
    /// </summary>
    public void SyncFromPartyLeader()
    {
        if (PartyManager.Instance == null) return;

        var leader = PartyManager.Instance.GetLeader();
        if (leader == null) return;

        int leaderMaxHp = leader.GetMaxHP();
        MaxHealth = leaderMaxHp;

        int leaderCurrentHp = leader.currentHP <= 0
            ? leaderMaxHp
            : Mathf.Clamp(leader.currentHP, 0, leaderMaxHp);

        CurrentHealth = leaderCurrentHp;
    }

    protected virtual void OnDeath()
    {
        Debug.Log("Explorer died (stub).");
        // TODO: game over / respawn / reload scene
    }
}
