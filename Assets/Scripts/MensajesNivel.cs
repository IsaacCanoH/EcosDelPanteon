using System.Collections;
using TMPro;
using UnityEngine;

public class MensajesNivel : MonoBehaviour
{
    public GoldCoinCollector recolector;
    public IrisInNivel apertura;
    public CanvasGroup carta;
    public TMP_Text titulo;
    public TMP_Text mensaje;
    public bool duendesTranquilosAlInicio;

    [Min(0.1f)] public float segundosMensaje = 4f;
    [Min(0.1f)] public float segundosAviso = 3f;
    [Min(0.01f)] public float duracionFundido = 0.18f;

    private Coroutine rutina;
    private int ultimaCantidad;
    private bool listo;

    private void Awake()
    {
        if (carta == null) return;
        carta.alpha = 0f;
        carta.interactable = false;
        carta.blocksRaycasts = false;
    }

    private void Start()
    {
        if (recolector == null || carta == null || titulo == null || mensaje == null)
        {
            Debug.LogError("MensajesNivel: asigna Recolector, Carta, Titulo y Mensaje.", this);
            enabled = false;
            return;
        }

        if (recolector.waveDirector != null)
        {
            carta.alpha = 0f;
            enabled = false;
            return;
        }
        ultimaCantidad = recolector.collectedCoins;
        listo = true;

        if (ultimaCantidad > 0)
            MostrarMoneda(ultimaCantidad);
        else
            rutina = StartCoroutine(Introduccion());
    }

    private void LateUpdate()
    {
        if (!listo || recolector == null) return;

        int cantidad = recolector.collectedCoins;
        if (cantidad == ultimaCantidad) return;

        int anterior = ultimaCantidad;
        ultimaCantidad = cantidad;

        if (cantidad > anterior)
            MostrarMoneda(cantidad);
    }

    private IEnumerator Introduccion()
    {
        yield return Presentar("OBJETIVO",
            $"Recupera las {recolector.requiredCoins} monedas\nde los duendes.", segundosMensaje);

        if (recolector.collectedCoins == 0)
        {
            yield return Presentar("CUIDADO",
                duendesTranquilosAlInicio
                    ? "Evita que te vean.\n¡Puedes despistarlos!"
                    : "Aléjate de los duendes.\n¡Puedes enfurecerlos!", segundosAviso);
        }

        rutina = null;
    }

    private void MostrarMoneda(int cantidad)
    {
        string texto;
        if (cantidad >= recolector.requiredCoins)
            texto = "¡Recuperaste todo el oro!";
        else if (cantidad == 1)
            texto = "Los duendes te han visto.\n¡Corre!";
        else
            texto = duendesTranquilosAlInicio
                ? "Ya tienes dos monedas.\nEncuentra la última."
                : "¡Estás bajo la maldición!\nBusca la última moneda.";

        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(AvisoDeMoneda(
            $"MONEDA {cantidad} DE {recolector.requiredCoins}", texto));
    }

    private IEnumerator AvisoDeMoneda(string encabezado, string texto)
    {
        yield return Presentar(encabezado, texto, segundosMensaje);
        rutina = null;
    }

    private IEnumerator Presentar(string encabezado, string texto, float segundos)
    {
        while (apertura != null && apertura.isActiveAndEnabled)
            yield return null;

        titulo.text = encabezado;
        mensaje.text = texto;

        yield return Fundir(1f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, segundos));
        yield return Fundir(0f);
    }

    private IEnumerator Fundir(float destino)
    {
        float inicio = carta.alpha;
        float tiempo = 0f;
        float duracion = Mathf.Max(0.01f, duracionFundido);

        while (tiempo < duracion)
        {
            tiempo += Mathf.Min(Time.deltaTime, 0.05f);
            carta.alpha = Mathf.Lerp(inicio, destino, tiempo / duracion);
            yield return null;
        }

        carta.alpha = destino;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        rutina = null;
        if (carta != null) carta.alpha = 0f;
    }
}
