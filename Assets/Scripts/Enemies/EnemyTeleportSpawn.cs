using System;
using System.Collections;
using UnityEngine;

public class EnemyTeleportSpawn : MonoBehaviour
{
    [Header("Configuración de Teleportación")]
    public float warningDuration = 0.65f;
    public float materializationDuration = 0.35f;
    public AudioClip teleportSound;

    private SpriteRenderer[] renderers;
    private Collider2D[] colliders;
    private Rigidbody2D rb;
    private MonoBehaviour[] aiScripts;
    private Color[] originalColors;

    private static Sprite circleGlyphSprite;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        aiScripts = new MonoBehaviour[]
        {
            GetComponent<GoblinAI>(),
            GetComponent<EnemyHealth>()
        };

        if (renderers != null && renderers.Length > 0)
        {
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].color;
            }
        }
    }

    public void BeginSpawnSequence(Action onComplete = null)
    {
        StartCoroutine(SpawnRoutine(onComplete));
    }

    private IEnumerator SpawnRoutine(Action onComplete)
    {
        // 1. Desactivar físicas, colisiones e IA mientras dura la invocación
        if (rb != null) rb.simulated = false;
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }
        foreach (var script in aiScripts)
        {
            if (script != null) script.enabled = false;
        }

        // Hacer invisible al duende al inicio
        SetRenderersAlpha(0f);

        // 2. Crear glifo o círculo de aviso en el suelo
        GameObject glyph = new GameObject("TeleportGlyph");
        glyph.transform.position = transform.position + Vector3.down * 0.15f;
        SpriteRenderer glyphRenderer = glyph.AddComponent<SpriteRenderer>();
        glyphRenderer.sprite = GetOrCreateGlyphSprite();
        glyphRenderer.sortingLayerName = "Layer 1";
        glyphRenderer.sortingOrder = 180;
        glyph.transform.localScale = Vector3.zero;

        Color glyphColor = new Color(0.2f, 0.85f, 0.45f, 0.85f); // Resplandor espectral verde

        float timer = 0f;
        while (timer < warningDuration)
        {
            timer += Time.deltaTime;
            float t = timer / warningDuration;
            glyph.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.15f, Mathf.SmoothStep(0f, 1f, t));
            glyphRenderer.color = new Color(glyphColor.r, glyphColor.g, glyphColor.b, Mathf.PingPong(t * 4f, 1f));
            yield return null;
        }

        if (teleportSound != null)
        {
            AudioSource.PlayClipAtPoint(teleportSound, transform.position, 0.8f);
        }

        // 3. Materialización rápida del enemigo (fade-in + destello)
        timer = 0f;
        while (timer < materializationDuration)
        {
            timer += Time.deltaTime;
            float t = timer / materializationDuration;
            SetRenderersAlpha(Mathf.Clamp01(t));
            glyphRenderer.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }

        Destroy(glyph);
        SetRenderersAlpha(1f);

        // 4. Activar físicas, colisiones e IA para empezar a pelear
        if (rb != null) rb.simulated = true;
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = true;
        }
        foreach (var script in aiScripts)
        {
            if (script != null) script.enabled = true;
        }

        GoblinAI ai = GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = false;
            ai.Alert();
            ai.BeginWaveCombat(1);
        }

        onComplete?.Invoke();
    }

    private void SetRenderersAlpha(float alpha)
    {
        if (renderers == null || originalColors == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                Color baseC = i < originalColors.Length ? originalColors[i] : Color.white;
                renderers[i].color = new Color(baseC.r, baseC.g, baseC.b, baseC.a * alpha);
            }
        }
    }

    private static Sprite GetOrCreateGlyphSprite()
    {
        if (circleGlyphSprite != null) return circleGlyphSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Vector2 center = new Vector2(15.5f, 15.5f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d >= 10f && d <= 14.5f)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        circleGlyphSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return circleGlyphSprite;
    }
}

