using UnityEngine;

public class Health : MonoBehaviour
{
    public float startingHealth = 100.0f;
    public float maxHealth = 100.0f;
    private float currentHealth;
    public GameObject explosion;                // The explosion prefab to be instantiated

    public bool isPlayer = false;               // Whether or not this health is the player
    public GameObject deathCam;					// The camera to activate when the player dies

    private void Start()
    {
        currentHealth = startingHealth;
    }

    public void ChangeHealth(float amount)
    {

        // Notify SwarmAgent if present
        SwarmAgent agent = GetComponent<SwarmAgent>();
        if (agent != null && amount < 0)
        {
            agent.ReceiveDamage(-amount); // Convert damage to positive
        }

        currentHealth += amount;

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

    }

    public void Die()
    {
        if (isPlayer && deathCam != null)
            deathCam.SetActive(true);

        GetComponent<MeshRenderer>().enabled = false;
        Instantiate(explosion, transform.position, transform.rotation);

        SwarmAgent agent = GetComponent<SwarmAgent>();
        if (agent != null)
        {
            agent.Die();
        }
        else if (!isPlayer)
        {
            Destroy(gameObject);
        }
    }
}