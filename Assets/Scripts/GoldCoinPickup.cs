using UnityEngine;

public class GoldCoinPickup : MonoBehaviour
{
    private bool collected = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        GoldCoinCollector collector =
            other.GetComponentInParent<GoldCoinCollector>();

        if (collector != null)
        {
            collected = true;

            // Sumar una moneda al contador del Player.
            collector.CollectCoin();

            // Desaparecer la moneda del escenario.
            Destroy(gameObject);
        }
    }
}