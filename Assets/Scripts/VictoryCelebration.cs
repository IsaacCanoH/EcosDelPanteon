using UnityEngine;
using UnityEngine.UI;

// Confeti de pocos pixeles, sin texturas ni efectos que tapen el recorrido.
[RequireComponent(typeof(CanvasGroup))]
public class VictoryCelebration : MonoBehaviour
{
    private const int Count = 24;
    private readonly RectTransform[] pieces = new RectTransform[Count];
    private readonly Image[] images = new Image[Count];
    private CanvasGroup group;
    private float elapsed;
    private int lastFrame = -1;

    public bool IsVisible { get { return group != null && group.alpha > 0f; } }

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        Color[] palette = {
            new Color32(244, 217, 144, 255),
            new Color32(192, 145, 68, 255),
            new Color32(232, 225, 189, 255),
            new Color32(87, 132, 119, 255)
        };
        for (int i = 0; i < Count; i++)
        {
            GameObject piece = new GameObject("Pixel_" + i, typeof(RectTransform), typeof(Image));
            pieces[i] = (RectTransform)piece.transform;
            pieces[i].SetParent(transform, false);
            pieces[i].SetAsFirstSibling();
            pieces[i].sizeDelta = new Vector2(4f + i % 3 * 2f, i % 2 == 0 ? 4f : 8f);
            images[i] = piece.GetComponent<Image>();
            images[i].color = palette[i % palette.Length];
            images[i].raycastTarget = false;
        }
        Hide();
    }

    public void Show()
    {
        elapsed = 0f;
        lastFrame = -1;
        group.alpha = 1f;
        transform.SetAsLastSibling();
        AnimatePixels();
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
    }

    private void Update()
    {
        if (!IsVisible) return;
        elapsed += Time.unscaledDeltaTime;
        int frame = Mathf.FloorToInt(elapsed * 12f);
        if (frame == lastFrame) return;
        lastFrame = frame;
        AnimatePixels();
    }

    private void AnimatePixels()
    {
        for (int i = 0; i < Count; i++)
        {
            float phase = Mathf.Repeat(elapsed / (3.2f + i % 5 * 0.24f) + i * 0.618034f, 1f);
            float x = -345f + i * 173 % 690 + Mathf.Sin(elapsed * 1.4f + i) * 12f;
            float y = 78f - phase * 160f;
            pieces[i].anchoredPosition = new Vector2(Mathf.Round(x / 2f) * 2f, Mathf.Round(y / 2f) * 2f);
            pieces[i].localRotation = Quaternion.Euler(0f, 0f,
                (Mathf.FloorToInt(elapsed * 3f) + i) % 2 * 90f);
            Color color = images[i].color;
            color.a = Mathf.Abs(x) < 245f && Mathf.Abs(y) < 32f ? 0f :
                Mathf.Clamp01(phase * 8f) * Mathf.Clamp01((1f - phase) * 6f);
            images[i].color = color;
        }
    }
}
