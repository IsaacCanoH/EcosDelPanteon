using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida del jugador")]
    public int maxHealth = 5;

    public int currentHealth;

    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log("Vida del Player: " + currentHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            Debug.Log("¡El Player ha muerto!");
        }
    }
}