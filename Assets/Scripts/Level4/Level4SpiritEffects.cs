using System.Collections;
using UnityEngine;

internal static class Level4SpiritEffects
{
    private static Sprite pixel;
    private static Sprite ring;

    internal static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), Vector2.one * 23.5f);
                if (radius >= 21f && radius < 22.5f)
                    colors[y * size + x] = new Color(0.26f, 0.85f, 1f, 0.95f);
                else if (radius >= 19.8f && radius < 21f)
                    colors[y * size + x] = new Color(0.63f, 0.97f, 1f, 0.54f);
                else if (radius < 19.8f)
                    colors[y * size + x] = new Color(0.13f, 0.55f, 1f, 0.14f);
            }
            tex.SetPixels(colors);
            tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 26f);
            return ring;
        }
    }

    internal static SpriteRenderer CreateVisual(string name, Sprite sprite, int order, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Layer 1";
        renderer.sortingOrder = order;
        return renderer;
    }

    internal static IEnumerator PulseRing(SpriteRenderer renderer, float duration, Color tint)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            renderer.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.88f, t);
            renderer.color = new Color(tint.r, tint.g, tint.b, 1f - t);
            yield return null;
        }
        Object.Destroy(renderer.gameObject);
    }

    internal static IEnumerator AnimateChild(SpriteRenderer body, Transform owner, float duration, bool appearing)
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 16f);
        }

        var particles = new SpriteRenderer[16];
        for (int i = 0; i < particles.Length; i++)
            particles[i] = CreateVisual("Pixel_Espectral", pixel, 9, owner);
        Color original = body.color;
        Vector3 bottom = owner.position + Vector3.up * 0.13f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float dissolve = appearing ? 1f - progress : progress;
            body.color = new Color(original.r, original.g, original.b, 1f - dissolve);
            for (int i = 0; i < particles.Length; i++)
            {
                float height = i / (float)(particles.Length - 1);
                float travel = Mathf.Clamp01((dissolve - height * 0.43f) / 0.57f);
                float angle = i * 2.39996f;
                particles[i].transform.position = bottom + new Vector3(
                    Mathf.Sin(angle) * (0.16f + travel * 0.52f),
                    height * 1.05f + travel * (0.25f + 0.4f * height), 0f);
                particles[i].color = new Color(0.58f, 0.58f, 0.58f, Mathf.Sin(travel * Mathf.PI) * 0.9f);
            }
            yield return null;
        }
        body.color = new Color(original.r, original.g, original.b, appearing ? 1f : 0f);
        foreach (var particle in particles)
            if (particle != null) Object.Destroy(particle.gameObject);
    }
}
