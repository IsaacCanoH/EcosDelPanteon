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

    public GoblinWaveDirector waveDirector;
    public event Action<int> OnCoinCollected;
    public event Action OnAllCoinsCollected;

    public bool CanCollectNextCoin
    {
        get { return collectedCoins < requiredCoins &&
            (waveDirector == null || (!waveDirector.IsWaveActive && !waveDirector.IsComplete)); }
    }
    public bool IsLevelFinished
    {
        get { return collectedCoins >= requiredCoins &&
            (waveDirector == null || waveDirector.IsComplete); }
    }

    public void CollectCoin()
    {
        if (!CanCollectNextCoin)
            return;

        collectedCoins++;
        OnCoinCollected?.Invoke(collectedCoins);

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