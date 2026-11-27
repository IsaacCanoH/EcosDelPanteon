using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class AndroidTouchControl : MonoBehaviour, IPointerDownHandler,
    IPointerUpHandler, IDragHandler, IPointerExitHandler
{
    public AndroidTouchAction action;
    public bool joystick;
    public RectTransform thumb;
    private int pointer = int.MinValue;
    private RectTransform rect;
    private Graphic face;
    private float radius;

    private void Awake()
    {
        rect = (RectTransform)transform;
        face = GetComponent<Graphic>();
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (pointer != int.MinValue) return;
        pointer = data.pointerId;
        if (joystick) Drag(data);
        else if (action == AndroidTouchAction.Search) AndroidTouchInput.SearchHeld = true;
        else AndroidTouchInput.Press(action);
        if (face != null) face.color = new Color(1f, 0.93f, 0.72f, 1f);
    }

    public void OnDrag(PointerEventData data)
    {
        if (joystick && pointer == data.pointerId) Drag(data);
    }

    private void Drag(PointerEventData data)
    {
        Vector2 point;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position,
            data.pressEventCamera, out point)) return;
        radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.30f;
        Vector2 value = Vector2.ClampMagnitude(point / Mathf.Max(1f, radius), 1f);
        if (thumb != null) thumb.anchoredPosition = value * radius;
        AndroidTouchInput.Move = value.sqrMagnitude < 0.0225f ? Vector2.zero : value;
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (pointer == data.pointerId) Release();
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (!joystick && pointer == data.pointerId) Release();
    }

    public void Release()
    {
        if (joystick)
        {
            AndroidTouchInput.Move = Vector2.zero;
            if (thumb != null) thumb.anchoredPosition = Vector2.zero;
        }
        else if (action == AndroidTouchAction.Search) AndroidTouchInput.SearchHeld = false;
        pointer = int.MinValue;
        if (face != null) face.color = Color.white;
    }

    private void OnDisable() { Release(); }
}

// Circular mesh and hit test keep transparent corners from intercepting another
// button or a menu. The eight atlas cells retain the approved icons and lettering.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AndroidControlImage : MaskableGraphic, ICanvasRaycastFilter
{
    public Texture2D atlas;
    public int tile;
    public override Texture mainTexture { get { return atlas != null ? atlas : Texture2D.whiteTexture; } }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        const int segments = 128;
        float radius = Mathf.Min(r.width, r.height) * 0.48f;
        Vector2 center = r.center;
        Vector2 uvCenter = new Vector2((tile % 4 + 0.5f) / 4f, (1 - tile / 4 + 0.5f) / 2f);
        mesh.AddVert(center, color, uvCenter);
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 unit = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            mesh.AddVert(center + unit * radius, color,
                uvCenter + new Vector2(unit.x * 0.48f / 4f, unit.y * 0.48f / 2f));
            if (i > 0) mesh.AddTriangle(0, i, i + 1);
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out local);
        Rect r = rectTransform.rect;
        return (local - r.center).sqrMagnitude <= Mathf.Pow(Mathf.Min(r.width, r.height) * 0.48f, 2f);
    }
}
