using UnityEngine;

public class GoldCoinPickup : MonoBehaviour
{
    [Header("Busqueda opcional")]
    public bool requiresSearch;
    [Min(1)] public int searchOrder = 1;
    [Min(0.1f)] public float searchRadius = 1.2f;
    [TextArea(2, 4)] public string clue;

    private bool collected;

    public bool IsCollected { get { return collected; } }

    private void Awake()
    {
        // Solo las instancias configuradas para buscar se ocultan.
        if (requiresSearch)
        {
            foreach (SpriteRenderer visual in GetComponentsInChildren<SpriteRenderer>())
                visual.enabled = false;
        }
    }

    public bool CanSearch(GoldCoinCollector collector)
    {
        return requiresSearch && !collected && isActiveAndEnabled &&
            collector != null && collector.CanCollectNextCoin &&
            collector.collectedCoins == searchOrder - 1;
    }

    public bool TryCollect(GoldCoinCollector collector)
    {
        if (collected || collector == null ||
            !collector.CanCollectNextCoin)
            return false;

        if (requiresSearch && !CanSearch(collector))
            return false;

        collected = true;
        collector.CollectCoin();

        if (requiresSearch)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Conserva la recoleccion original de los demas niveles y prefabs.
        if (requiresSearch || collected)
            return;

        GoldCoinCollector collector =
            other.GetComponentInParent<GoldCoinCollector>();

        if (collector != null)
            TryCollect(collector);
    }
}
