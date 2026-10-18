using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Datos del jugador")]
    public PlayerHealth playerHealth;
    public GoldCoinCollector coinCollector;

    [Header("Vida")]
    public TMP_Text textoVida;
    public Image[] corazones = new Image[5];
    public Sprite corazonLleno;
    public Sprite corazonVacio;

    [Header("Monedas")]
    public TMP_Text textoMonedas;
    public Image[] monedas = new Image[3];

    private int ultimaVida = -1;
    private int ultimaVidaMaxima = -1;
    private int ultimasMonedas = -1;
    private int ultimaMetaMonedas = -1;

    private void LateUpdate()
    {
        if (playerHealth == null || coinCollector == null)
            return;

        int vida = playerHealth.currentHealth;
        int vidaMaxima = playerHealth.maxHealth;
        int recogidas = coinCollector.collectedCoins;
        int meta = coinCollector.requiredCoins;

        if (vida == ultimaVida &&
            vidaMaxima == ultimaVidaMaxima &&
            recogidas == ultimasMonedas &&
            meta == ultimaMetaMonedas)
            return;

        if (textoVida != null)
            textoVida.text = $"{vida}/{vidaMaxima}";

        for (int i = 0; i < corazones.Length; i++)
        {
            if (corazones[i] == null)
                continue;

            corazones[i].sprite =
                i < vida ? corazonLleno : corazonVacio;

            corazones[i].color = Color.white;
        }

        if (textoMonedas != null)
            textoMonedas.text = $"{recogidas}/{meta}";

        for (int i = 0; i < monedas.Length; i++)
        {
            if (monedas[i] == null)
                continue;

            monedas[i].color = i < recogidas
                ? Color.white
                : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }

        ultimaVida = vida;
        ultimaVidaMaxima = vidaMaxima;
        ultimasMonedas = recogidas;
        ultimaMetaMonedas = meta;
    }
}