using UnityEngine;

public class Health : MonoBehaviour
{
    public float startingHealth = 100.0f;
    public float maxHealth = 100.0f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = startingHealth;
    }

    public void ChangeHealth(float amount)
    {
        currentHealth += amount;

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // Notify SwarmAgent if present
        SwarmAgent agent = GetComponent<SwarmAgent>();
        if (agent != null && amount < 0)
        {
            agent.ReceiveDamage(-amount); // Convert damage to positive
        }
    }

    public void Die()
    {
        SwarmAgent agent = GetComponent<SwarmAgent>();
        if (agent != null)
        {
            agent.Die();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}