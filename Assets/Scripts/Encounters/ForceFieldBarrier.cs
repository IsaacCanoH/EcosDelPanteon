using System.Collections;
using UnityEngine;

public class ForceFieldBarrier : MonoBehaviour
{
    [Header("Dimensiones de la Arena")]
    public Vector2 arenaSize = new Vector2(14f, 14f);
    public Color barrierColor = new Color(0.2f, 0.75f, 1f, 0.65f); // Azul espectral
    public AudioClip barrierHumSound;

    [Header("Objetos de Barrera")]
    public GameObject[] customBarrierVisuals;

    private BoxCollider2D[] boundaryColliders;
    private SpriteRenderer[] barrierRenderers;
    private AudioSource audioSource;
    private bool isActive;

    private static Sprite barrierWallSprite;

    private void Awake()
    {
        SetupProceduralBoundaries();
        SetBarrierActive(false);
    }

    private void SetupProceduralBoundaries()
    {
        if (customBarrierVisuals != null && customBarrierVisuals.Length > 0) return;

        boundaryColliders = new BoxCollider2D[4];
        barrierRenderers = new SpriteRenderer[4];

        // Crear 4 paredes perimetrales (Norte, Sur, Este, Oeste)
        float halfX = arenaSize.x * 0.5f;
        float halfY = arenaSize.y * 0.5f;
        float thickness = 0.65f;

        Vector2[] positions = new Vector2[]
        {
            new Vector2(0f, halfY),     // Norte
            new Vector2(0f, -halfY),    // Sur
            new Vector2(halfX, 0f),     // Este
            new Vector2(-halfX, 0f)     // Oeste
        };

        Vector2[] sizes = new Vector2[]
        {
            new Vector2(arenaSize.x + thickness * 2f, thickness),
            new Vector2(arenaSize.x + thickness * 2f, thickness),
            new Vector2(thickness, arenaSize.y),
            new Vector2(thickness, arenaSize.y)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject wall = new GameObject($"BarrierWall_{i}");
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = positions[i];

            BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
            col.size = sizes[i];
            boundaryColliders[i] = col;

            SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateBarrierSprite();
            sr.color = barrierColor;
            sr.sortingOrder = 450;
            wall.transform.localScale = new Vector3(sizes[i].x * 32f / 16f, sizes[i].y * 32f / 16f, 1f);
            barrierRenderers[i] = sr;
        }
    }

    public void ActivateBarrier()
    {
        SetBarrierActive(true);
        StartCoroutine(PulseRoutine());
    }

    public void DeactivateBarrier()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private void SetBarrierActive(bool active)
    {
        isActive = active;

        if (boundaryColliders != null)
        {
            foreach (var col in boundaryColliders)
            {
                if (col != null) col.enabled = active;
            }
        }

        if (barrierRenderers != null)
        {
            foreach (var sr in barrierRenderers)
            {
                if (sr != null) sr.enabled = active;
            }
        }

        if (customBarrierVisuals != null)
        {
            foreach (var obj in customBarrierVisuals)
            {
                if (obj != null) obj.SetActive(active);
            }
        }
    }

    private IEnumerator PulseRoutine()
    {
        while (isActive)
        {
            float alpha = Mathf.Lerp(0.45f, 0.85f, (Mathf.Sin(Time.time * 3.5f) + 1f) * 0.5f);
            if (barrierRenderers != null)
            {
                foreach (var sr in barrierRenderers)
                {
                    if (sr != null) sr.color = new Color(barrierColor.r, barrierColor.g, barrierColor.b, alpha);
                }
            }
            yield return null;
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        isActive = false;
        if (boundaryColliders != null)
        {
            foreach (var col in boundaryColliders) if (col != null) col.enabled = false;
        }

        float duration = 0.6f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(barrierColor.a, 0f, elapsed / duration);
            if (barrierRenderers != null)
            {
                foreach (var sr in barrierRenderers)
                    if (sr != null) sr.color = new Color(barrierColor.r, barrierColor.g, barrierColor.b, a);
            }
            yield return null;
        }

        SetBarrierActive(false);
    }

    private static Sprite GetOrCreateBarrierSprite()
    {
        if (barrierWallSprite != null) return barrierWallSprite;

        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.8f));
            }
        }
        tex.Apply();
        barrierWallSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return barrierWallSprite;
    }
}

