using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(2000)]
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Iris In Nivel")]
public class IrisInNivel : MaskableGraphic
{
    public Transform jugador;
    public Camera camaraJuego;

    public Vector3 ajusteCentro = new Vector3(0f, 0.4f, 0f);

    [Min(0.1f)] public float duracion = 1.3f;
    [Min(0f)] public float esperaInicial = 0.15f;
    [Min(0f)] public float bordeSuave = 4f;

    // La apertura existente conserva su comportamiento; el cierre usa la misma mascara al reves.
    public bool closing;
    public bool IsComplete { get { return avance >= 1f; } }
    public float Progress { get { return avance; } }

    private float tiempo;
    private float avance;
    private Vector2 centro;

    private const int segmentos = 160;

    protected override void OnEnable()
    {
        base.OnEnable();

        tiempo = 0f;
        avance = 0f;
        centro = rectTransform.rect.center;
        raycastTarget = true;

        SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || IsComplete) return;

        if (camaraJuego == null)
            camaraJuego = Camera.main;

        centro = rectTransform.rect.center;

        if (jugador != null && camaraJuego != null)
        {
            Vector3 pantalla = camaraJuego.WorldToScreenPoint(
                jugador.position + ajusteCentro);

            if (pantalla.z > 0f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, pantalla, null, out centro);
            }
        }

        tiempo += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        avance = Mathf.Clamp01(
            (tiempo - esperaInicial) / Mathf.Max(0.1f, duracion));

        SetVerticesDirty();

        if (avance >= 1f && !closing)
            gameObject.SetActive(false);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (!Application.isPlaying) return;

        Rect r = rectTransform.rect;

        if (r.width <= 0f || r.height <= 0f) return;

        float apertura = closing ? 1f - avance : avance;
        if (apertura <= 0f)
        {
            vh.AddVert(new Vector2(r.xMin, r.yMin),
                Color.black, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMax),
                Color.black, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax),
                Color.black, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin),
                Color.black, Vector2.zero);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
            return;
        }

        float dx = Mathf.Max(
            Mathf.Abs(r.xMin - centro.x),
            Mathf.Abs(r.xMax - centro.x));

        float dy = Mathf.Max(
            Mathf.Abs(r.yMin - centro.y),
            Mathf.Abs(r.yMax - centro.y));

        float distanciaMaxima = Mathf.Sqrt(dx * dx + dy * dy);
        float suavidad = Mathf.Max(0f, bordeSuave);

        float exterior =
            (distanciaMaxima + suavidad) * 1.02f + 4f;

        float radio = (distanciaMaxima + suavidad * 2f)
            * Mathf.SmoothStep(0f, 1f, apertura);

        float interior = Mathf.Clamp(
            radio - suavidad, 0f, exterior);

        float borde = Mathf.Clamp(
            radio + suavidad, interior, exterior);

        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;

            Vector2 direccion = new Vector2(
                Mathf.Cos(angulo), Mathf.Sin(angulo));

            vh.AddVert(centro + direccion * interior,
                new Color(0f, 0f, 0f, 0f), Vector2.zero);

            vh.AddVert(centro + direccion * borde,
                Color.black, Vector2.zero);

            vh.AddVert(centro + direccion * exterior,
                Color.black, Vector2.zero);
        }

        for (int i = 0; i < segmentos; i++)
        {
            int a = i * 3;
            int b = a + 3;

            vh.AddTriangle(a, b, a + 1);
            vh.AddTriangle(a + 1, b, b + 1);

            vh.AddTriangle(a + 1, b + 1, a + 2);
            vh.AddTriangle(a + 2, b + 1, b + 2);
        }
    }
}