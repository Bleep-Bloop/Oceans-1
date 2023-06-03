using UnityEngine;

public class HealthComponent : MonoBehaviour
{

    [SerializeField] private int baseHealth;
    [SerializeField] private int currentHealth;

    // Start is called before the first frame update
    void Start()
    {
        currentHealth = baseHealth;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void TakeDamage(int damageTaken)
    {
        currentHealth -= damageTaken;

        if (currentHealth <= 0)
        {
            // If the player dies, end application.
            if (GetComponent<PlayerStateMachine>())
                UnityEditor.EditorApplication.isPlaying = false;

        }
    }
}
