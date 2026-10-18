using System;
using UnityEngine;

public class GoldCoinCollector : MonoBehaviour
{
    [Header("Monedas de oro")]
    public int requiredCoins = 3;
    public int collectedCoins = 0;

    [Header("Sonido de recolección")]
    public AudioSource audioRecoleccion;
    public AudioClip sonidoRecoleccion;

    public event Action OnAllCoinsCollected;

    public void CollectCoin()
    {
        if (collectedCoins >= requiredCoins)
            return;

        collectedCoins++;

        // Reproducir una vez por cada moneda recogida.
        if (audioRecoleccion != null &&
            sonidoRecoleccion != null)
        {
            audioRecoleccion.PlayOneShot(sonidoRecoleccion);
        }

        Debug.Log(
            "Monedas de oro: " +
            collectedCoins + "/" + requiredCoins);

        if (collectedCoins == requiredCoins)
        {
            Debug.Log("¡Conseguiste las 3 monedas de oro!");
            OnAllCoinsCollected?.Invoke();
        }
    }
}