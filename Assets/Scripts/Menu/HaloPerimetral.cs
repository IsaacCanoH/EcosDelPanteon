using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/Halo Perimetral")]
public class HaloPerimetral : MaskableGraphic,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    public bool disponible = true;
    [Min(0.2f)] public float periodo = 3.6f;
    public float desfase = 0f;
    [Range(0.01f, 0.2f)] public float margen = 0.05f;
    [Min(1f)] public float grosor = 3f;
    [Min(1f)] public float resplandor = 24f;

    private bool encima;
    private bool seleccionado;
    private float intensidad = 0.45f;
    private float realce;

    protected override void OnEnable()
    {
        base.OnEnable();
        encima = seleccionado = false;
        intensidad = disponible ? 0.45f : 0.18f;
        realce = 0f;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        bool activo = disponible && (encima || seleccionado);

        float pulso = (Mathf.Sin((Time.unscaledTime + desfase)
            * Mathf.PI * 2f / Mathf.Max(0.2f, periodo)) + 1f) * 0.5f;

        float objetivo = disponible
            ? Mathf.Lerp(0.25f, 0.67f, pulso)
            : 0.18f;

        if (activo) objetivo = 1f;

        float suavidad = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        intensidad = Mathf.Lerp(intensidad, objetivo, suavidad);
        realce = Mathf.Lerp(realce, activo ? 1f : 0f, suavidad);

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect zona = rectTransform.rect;
        Rect marco = Rect.MinMaxRect(
            zona.xMin + zona.width * margen,
            zona.yMin + zona.height * margen,
            zona.xMax - zona.width * margen,
            zona.yMax - zona.height * margen);

        if (marco.width < 10f || marco.height < 10f) return;

        float limite = Mathf.Min(marco.width, marco.height) * 0.2f;
        float linea = Mathf.Clamp(grosor, 1f, limite);

        float brillo = Mathf.Clamp(
            resplandor * (1f + realce * 0.35f), 1f, limite);

        Rect interior = Contraer(marco, linea);
        const int pasos = 8;

        for (int i = 0; i < pasos; i++)
        {
            float a = i / (float)pasos;
            float b = (i + 1) / (float)pasos;

            Banda(vh,
                Contraer(marco, -brillo * (1f - a)),
                Contraer(marco, -brillo * (1f - b)),
                0.55f * a * a,
                0.55f * b * b);

            Banda(vh,
                Contraer(interior, brillo * a),
                Contraer(interior, brillo * b),
                0.4f * (1f - a) * (1f - a),
                0.4f * (1f - b) * (1f - b));
        }

        Banda(vh, marco, interior, 1f, 1f);
    }

    private static Rect Contraer(Rect r, float distancia)
    {
        return Rect.MinMaxRect(
            r.xMin + distancia, r.yMin + distancia,
            r.xMax - distancia, r.yMax - distancia);
    }

    private void Banda(VertexHelper vh, Rect exterior, Rect interior,
        float alfaExterior, float alfaInterior)
    {
        int inicio = vh.currentVertCount;

        Contorno(vh, exterior, alfaExterior);
        Contorno(vh, interior, alfaInterior);

        for (int i = 0; i < 4; i++)
        {
            int siguiente = (i + 1) % 4;

            vh.AddTriangle(
                inicio + i, inicio + siguiente, inicio + siguiente + 4);

            vh.AddTriangle(
                inicio + i, inicio + siguiente + 4, inicio + i + 4);
        }
    }

    private void Contorno(VertexHelper vh, Rect r, float alfa)
    {
        Color tono = color;

        float fuerza = Application.isPlaying
            ? intensidad
            : (disponible ? 0.45f : 0.18f);

        tono.a *= alfa * fuerza;

        for (int i = 0; i < 4; i++)
        {
            Vector2 posicion = new Vector2(
                i < 2 ? r.xMin : r.xMax,
                i == 1 || i == 2 ? r.yMax : r.yMin);

            vh.AddVert(posicion, tono, Vector2.zero);
        }
    }

    public void OnPointerEnter(PointerEventData e) { encima = true; }
    public void OnPointerExit(PointerEventData e) { encima = false; }
    public void OnSelect(BaseEventData e) { seleccionado = true; }
    public void OnDeselect(BaseEventData e) { seleccionado = false; }
}