using UnityEngine;

public class Health : MonoBehaviour
{
    public float startingHealth = 100.0f;
    public float maxHealth = 100.0f;
    private float currentHealth;
    public GameObject explosion;                // The explosion prefab to be instantiated

    public bool isPlayer = false;               // Whether or not this health is the player
    public bool isChild = false;
    public GameObject deathCam;					// The camera to activate when the player dies
    public GameObject canvas;					// The UI canvas to destroy when the player dies

    private float lastHealthChangeTime = -Mathf.Infinity; // Track last time ChangeHealth was called
    private float healthChangeCooldown = 0.1f;            // Cooldown duration in seconds

    private void Start()
    {
        currentHealth = startingHealth;
    }

    public void ChangeHealth(float amount)
    {
        if (Time.time - lastHealthChangeTime < healthChangeCooldown)
        {
            return; // Still in cooldown, so ignore the request
        }

        lastHealthChangeTime = Time.time; // Update the time of the last change

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
        if (isChild && deathCam != null)
            deathCam.SetActive(true);

        if (!isChild)
        {
            GetComponent<MeshRenderer>().enabled = false;
            transform.GetChild(0).gameObject.SetActive(false);
        }

        if (isPlayer)
        {
            Destroy(gameObject);
            Destroy(canvas);
        }

        Instantiate(explosion, transform.position, transform.rotation);

        SwarmAgent agent = GetComponent<SwarmAgent>();
        if (agent != null)
        {
            agent.Die();
        }
        else if (isChild)
        {
            Destroy(gameObject);
        }
    }
}
