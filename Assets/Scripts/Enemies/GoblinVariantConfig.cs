using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class GoblinVariantConfig : MonoBehaviour
{
    [Header("Configuración de Variante")]
    public bool isChubby = false;

    [Header("Ajustes Visuales para Duende Gordito")]
    public Vector3 chubbyScale = new Vector3(1.28f, 1.16f, 1f);
    public Color chubbySkinColor = new Color(0.58f, 0.78f, 0.55f, 1f); // Verde más oscuro y pesado

    private void Awake()
    {
        ApplyConfiguration();
    }

    public void SetVariant(bool chubby)
    {
        isChubby = chubby;
        ApplyConfiguration();
    }

    private void ApplyConfiguration()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        GoblinAI ai = GetComponent<GoblinAI>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (isChubby)
        {
            // Duende Gordito: 80 HP, 20 daño, más lento (0.95), suelta 1 orbe de vida (+1 corazón)
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.ChubbyGoblin);
            }

            if (ai != null)
            {
                ai.attackDamage = 20;
                ai.moveSpeed = 0.95f;
            }

            transform.localScale = chubbyScale;

            if (sr != null)
            {
                sr.color = chubbySkinColor;
            }

            if (col != null)
            {
                col.size = new Vector2(0.55f, 0.42f);
            }
        }
        else
        {
            // Duende Normal: 50 HP, 10 daño, velocidad estándar (1.4f)
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.NormalGoblin);
            }

            if (ai != null)
            {
                ai.attackDamage = 10;
                ai.moveSpeed = 1.35f;
            }

            transform.localScale = Vector3.one;

            if (sr != null)
            {
                sr.color = Color.white;
            }
        }
    }
}

