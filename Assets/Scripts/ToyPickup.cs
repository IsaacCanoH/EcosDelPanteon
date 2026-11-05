using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class ToyPickup : MonoBehaviour
{
    [Range(0, 2)] public int toyId;
    private bool collected;
    private Transform playerTransform;
    private Vector3 initialLocalPos;
    private Vector3 initialLocalScale;
    private float floatTimer;
    private SpriteRenderer sr;
    private Collider2D col;
    private static Sprite glowSprite;
    private static Sprite[] cachedLevel3Sprites;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticCache()
    {
        glowSprite = null;
        cachedLevel3Sprites = null;
    }

    public static bool IsLevel3Scene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string GetItemDisplayName(int id, bool isLevel3)
    {
        if (isLevel3)
        {
            return id == 0 ? "Poción Morada" : (id == 1 ? "Muñeco Vudú" : "Vela Ritual");
        }
        return id == 0 ? "Osito de Peluche" : (id == 1 ? "Pelota Vintage" : "Trencito de Madera");
    }

    public static Sprite GetOrCreateLevel3ItemSprite(int id)
    {
        if (cachedLevel3Sprites == null || cachedLevel3Sprites.Length < 3)
        {
            cachedLevel3Sprites = new Sprite[3];
        }

        int clamped = Mathf.Clamp(id, 0, 2);
        if (cachedLevel3Sprites[clamped] == null)
        {
            if (clamped == 0) cachedLevel3Sprites[0] = CreatePurplePotionSprite();
            else if (clamped == 1) cachedLevel3Sprites[1] = CreateVoodooDollSprite();
            else cachedLevel3Sprites[2] = CreateRitualCandleSprite();
        }
        return cachedLevel3Sprites[clamped];
    }

    private void Awake()
    {
        ApplyCompactAltarScale();
        initialLocalPos = transform.localPosition;
        initialLocalScale = transform.localScale;
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 5;
        }
        ApplyLevel3VisualsIfNeeded();
    }

    private void Start()
    {
        ApplyLevel3VisualsIfNeeded();
        ApplyCompactAltarScale();
        initialLocalPos = transform.localPosition;
        initialLocalScale = transform.localScale;
    }

    private void OnEnable()
    {
        collected = false;
        floatTimer = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        ApplyLevel3VisualsIfNeeded();
        ApplyCompactAltarScale();
        if (initialLocalScale.sqrMagnitude > 0.001f)
            transform.localScale = initialLocalScale;
    }

    private void ApplyCompactAltarScale()
    {
        // Reducir un poco el tamaño de los objetos de los altares (Nivel 2 y Nivel 3) para que luzcan bien proporcionados sobre la piedra
        float maxAllowedScale = IsLevel3Scene() ? 0.68f : 0.65f;
        if (transform.localScale.x > maxAllowedScale || transform.localScale.y > maxAllowedScale)
        {
            transform.localScale = new Vector3(maxAllowedScale, maxAllowedScale, 1f);
        }
        initialLocalScale = transform.localScale;
    }

    public void ApplyLevel3VisualsIfNeeded()
    {
        if (!IsLevel3Scene()) return;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = GetOrCreateLevel3ItemSprite(toyId);
            sr.color = Color.white;
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 5;
        }
    }

    private void Update()
    {
        if (collected) return;

        // Efecto visual de flotación mágica suave
        floatTimer += Time.deltaTime * 3.5f;
        transform.localPosition = initialLocalPos + new Vector3(0f, Mathf.Sin(floatTimer) * 0.08f, 0f);

        // Chequeo de proximidad directa con el jugador
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= 1.45f)
        {
            TryCollect(playerTransform.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        TryCollect(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (collected) return;
        TryCollect(other.gameObject);
    }

    public bool TryCollect(GameObject target)
    {
        if (collected || target == null) return false;

        var collector = target.GetComponentInParent<ToyCollector>();
        if (collector == null) collector = FindObjectOfType<ToyCollector>();

        var health = target.GetComponentInParent<PlayerHealth>();
        if (health == null) health = FindObjectOfType<PlayerHealth>();

        if (collector == null || (health != null && health.currentHealth <= 0)) return false;

        if (!collector.Collect(toyId)) return false;

        collected = true;
        if (col != null) col.enabled = false;

        bool isLvl3 = IsLevel3Scene();
        string itemName = GetItemDisplayName(toyId, isLvl3);
        Debug.Log($"[COLECCIONABLE] ¡{itemName} recogido con éxito! Total: {collector.Count}/3");

        var hud = FindObjectOfType<Level2HUD>();
        if (hud != null)
        {
            string title = isLvl3 ? "¡OBJETO RITUAL OBTENIDO!" : "¡JUGUETE ENCONTRADO!";
            hud.ShowNotification(title, $"{itemName} purificado ({collector.Count}/3)", 2.5f);
        }

        StartCoroutine(CollectAnimationRoutine(collector.transform));
        return true;
    }

    private IEnumerator CollectAnimationRoutine(Transform collectorTransform)
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        bool isLvl3 = IsLevel3Scene();

        GameObject ringObj = new GameObject("ToyPickupGlow");
        ringObj.transform.position = startPos;
        SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetOrCreateGlowSprite();
        ringSr.sortingLayerName = "Layer 1";
        ringSr.sortingOrder = 6;
        ringSr.color = isLvl3 ? new Color(0.85f, 0.35f, 1f, 0.85f) : new Color(1f, 0.9f, 0.35f, 0.85f);

        float duration = 0.42f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / duration);

            Vector3 targetPos = collectorTransform != null
                ? collectorTransform.position + new Vector3(0f, 0.65f, 0f)
                : startPos + new Vector3(0f, 0.65f, 0f);

            transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, p))
                                 + new Vector3(0f, Mathf.Sin(p * Mathf.PI) * 0.32f, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, p * 360f);

            float scaleCurve = p < 0.4f
                ? Mathf.Lerp(1f, 1.35f, p / 0.4f)
                : Mathf.Lerp(1.35f, 0.15f, (p - 0.4f) / 0.6f);
            transform.localScale = startScale * scaleCurve;

            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f - Mathf.Pow(p, 2f);
                sr.color = c;
            }

            if (ringSr != null)
            {
                ringObj.transform.position = transform.position;
                ringObj.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.2f, p);
                ringSr.color = isLvl3
                    ? new Color(0.88f, 0.38f, 1f, (1f - p) * 0.85f)
                    : new Color(1f, 0.92f, 0.4f, (1f - p) * 0.85f);
            }

            yield return null;
        }

        if (ringObj != null) Destroy(ringObj);
        gameObject.SetActive(false);
        Destroy(gameObject, 0.05f);
    }

    private static Sprite GetOrCreateGlowSprite()
    {
        if (glowSprite != null) return glowSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return glowSprite;
    }

    #region Sprites Pixel-Art de Objetos Rituales del Nivel 3 (Poción Morada, Muñeco Vudú y Vela)

    /// <summary>
    /// Objeto 1 (Altar 1): Poción Morada — Matraz de cristal alquímico con corcho, líquido morado brillante y burbujas mágicas.
    /// </summary>
    private static Sprite CreatePurplePotionSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.12f, 0.06f, 0.18f, 1f);
        Color glassBorder = new Color(0.68f, 0.78f, 0.94f, 0.95f);
        Color glassEmpty = new Color(0.32f, 0.25f, 0.48f, 0.55f);
        Color glassShine = new Color(0.96f, 0.94f, 1.0f, 0.95f);
        Color corkDark = new Color(0.45f, 0.26f, 0.12f, 1f);
        Color corkLight = new Color(0.68f, 0.44f, 0.22f, 1f);
        Color liqDeep = new Color(0.36f, 0.08f, 0.56f, 1f);
        Color liqMid = new Color(0.62f, 0.16f, 0.88f, 1f);
        Color liqBright = new Color(0.84f, 0.34f, 1.0f, 1f);
        Color liqBubble = new Color(0.96f, 0.72f, 1.0f, 1f);
        Color auraGlow = new Color(0.75f, 0.25f, 1.0f, 0.24f);

        Vector2 bulbCenter = new Vector2(15.5f, 11.5f);
        float outerR = 9.2f;
        float innerR = 7.6f;

        // Halo suave exterior
        for (int y = 1; y < 28; y++)
        {
            for (int x = 3; x < 29; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), bulbCenter);
                if (d > outerR && d <= outerR + 2.2f)
                {
                    tex.SetPixel(x, y, auraGlow);
                }
            }
        }

        // Cuerpo esférico del matraz (Y = 3..20)
        for (int y = 2; y <= 20; y++)
        {
            for (int x = 5; x <= 26; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), bulbCenter);
                if (d <= outerR)
                {
                    if (d >= outerR - 1.05f)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                    else if (d >= innerR)
                    {
                        tex.SetPixel(x, y, glassBorder);
                    }
                    else
                    {
                        // Líquido morado hasta Y = 15 con onda en la superficie
                        float surfaceY = 14.8f + Mathf.Sin(x * 0.6f) * 0.6f;
                        if (y <= surfaceY)
                        {
                            if (Mathf.Abs(y - surfaceY) < 1.1f)
                                tex.SetPixel(x, y, liqBright);
                            else if (x < 14 && y > 8)
                                tex.SetPixel(x, y, liqBright);
                            else if (x > 18 || y < 6)
                                tex.SetPixel(x, y, liqDeep);
                            else
                                tex.SetPixel(x, y, liqMid);
                        }
                        else
                        {
                            tex.SetPixel(x, y, glassEmpty);
                        }
                    }
                }
            }
        }

        // Base plana reforzada del matraz
        for (int x = 11; x <= 20; x++)
        {
            tex.SetPixel(x, 2, outline);
            tex.SetPixel(x, 3, glassBorder);
        }

        // Cuello del frasco (Y = 20..25, X = 12..19)
        for (int y = 20; y <= 25; y++)
        {
            for (int x = 12; x <= 19; x++)
            {
                if (x == 12 || x == 19) tex.SetPixel(x, y, outline);
                else if (x == 13 || x == 18) tex.SetPixel(x, y, glassBorder);
                else tex.SetPixel(x, y, glassEmpty);
            }
        }

        // Labio ancho de cristal (Y = 25..26, X = 11..20)
        for (int x = 11; x <= 20; x++)
        {
            tex.SetPixel(x, 25, outline);
            tex.SetPixel(x, 26, (x == 11 || x == 20) ? outline : glassBorder);
        }

        // Tapón de corcho (Y = 26..29, X = 13..18)
        for (int y = 26; y <= 29; y++)
        {
            for (int x = 13; x <= 18; x++)
            {
                if (x == 13 || x == 18 || y == 29)
                    tex.SetPixel(x, y, outline);
                else
                    tex.SetPixel(x, y, (x <= 15) ? corkLight : corkDark);
            }
        }

        // Burbujas alquímicas y reflejo especular del cristal
        tex.SetPixel(13, 8, liqBubble);
        tex.SetPixel(14, 8, liqBubble);
        tex.SetPixel(17, 11, liqBubble);
        tex.SetPixel(15, 13, liqBubble);

        // Brillo curvo izquierdo en el cristal
        tex.SetPixel(9, 13, glassShine);
        tex.SetPixel(9, 12, glassShine);
        tex.SetPixel(10, 14, glassShine);
        tex.SetPixel(10, 11, glassShine);
        tex.SetPixel(11, 15, glassShine);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 40f);
    }

    /// <summary>
    /// Objeto 2 (Altar 2): Muñeco Vudú — Muñeco de arpillera cosido con botones por ojos, corazón cosido y alfileres rituales.
    /// </summary>
    private static Sprite CreateVoodooDollSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.15f, 0.10f, 0.08f, 1f);
        Color burlapDark = new Color(0.54f, 0.40f, 0.25f, 1f);
        Color burlapMid = new Color(0.70f, 0.54f, 0.36f, 1f);
        Color burlapLight = new Color(0.82f, 0.66f, 0.45f, 1f);
        Color stitchCol = new Color(0.22f, 0.14f, 0.10f, 1f);
        Color heartRed = new Color(0.86f, 0.16f, 0.28f, 1f);
        Color heartPurple = new Color(0.62f, 0.14f, 0.55f, 1f);
        Color buttonCyan = new Color(0.25f, 0.88f, 0.95f, 1f);
        Color buttonGold = new Color(1.0f, 0.82f, 0.22f, 1f);
        Color pinSteel = new Color(0.84f, 0.88f, 0.94f, 1f);
        Color pinHeadPurple = new Color(0.85f, 0.25f, 1.0f, 1f);
        Color pinHeadRed = new Color(1.0f, 0.22f, 0.28f, 1f);

        // Función auxiliar para dibujar elipses rellenas con borde
        void DrawPart(Vector2 c, float rx, float ry)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - c.x) / rx;
                    float ny = (y - c.y) / ry;
                    float d = nx * nx + ny * ny;
                    if (d <= 1.0f)
                    {
                        if (d >= 0.74f)
                            tex.SetPixel(x, y, outline);
                        else if (nx < -0.2f && ny > 0.1f)
                            tex.SetPixel(x, y, burlapLight);
                        else if (nx > 0.25f || ny < -0.25f)
                            tex.SetPixel(x, y, burlapDark);
                        else
                            tex.SetPixel(x, y, burlapMid);
                    }
                }
            }
        }

        // Pierna izquierda y derecha
        DrawPart(new Vector2(12.5f, 6.0f), 2.8f, 4.5f);
        DrawPart(new Vector2(18.5f, 6.0f), 2.8f, 4.5f);

        // Brazos extendidos
        DrawPart(new Vector2(8.0f, 14.5f), 4.2f, 2.5f);
        DrawPart(new Vector2(23.0f, 14.5f), 4.2f, 2.5f);

        // Torso de arpillera
        DrawPart(new Vector2(15.5f, 13.0f), 5.2f, 6.2f);

        // Cabeza redonda de arpillera
        DrawPart(new Vector2(15.5f, 22.5f), 6.2f, 5.8f);

        // Ojo izquierdo (botón grande celeste)
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                tex.SetPixel(13 + dx, 23 + dy, (dx == 0 && dy == 0) ? outline : buttonCyan);

        // Ojo derecho (botón dorado/rojo)
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                tex.SetPixel(18 + dx, 23 + dy, (dx == 0 && dy == 0) ? outline : buttonGold);

        // Boca cosida con puntadas verticales
        for (int x = 12; x <= 19; x++)
        {
            tex.SetPixel(x, 19, stitchCol);
            if (x % 2 == 1)
            {
                tex.SetPixel(x, 20, stitchCol);
                tex.SetPixel(x, 18, stitchCol);
            }
        }

        // Costura diagonal en la frente y torso
        tex.SetPixel(14, 26, stitchCol);
        tex.SetPixel(15, 25, stitchCol);
        tex.SetPixel(16, 24, stitchCol);

        // Corazón ritual rojo/morado cosido en el pecho (X = 13..16, Y = 12..15)
        tex.SetPixel(13, 15, heartRed);
        tex.SetPixel(15, 15, heartRed);
        for (int x = 12; x <= 16; x++) tex.SetPixel(x, 14, (x <= 14) ? heartRed : heartPurple);
        for (int x = 13; x <= 15; x++) tex.SetPixel(x, 13, heartRed);
        tex.SetPixel(14, 12, heartPurple);

        // Alfiler 1: Clavado en el corazón (arriba-izquierda hacia el pecho)
        tex.SetPixel(10, 17, pinSteel);
        tex.SetPixel(9, 18, pinSteel);
        tex.SetPixel(8, 19, pinSteel);
        tex.SetPixel(7, 20, pinHeadPurple);
        tex.SetPixel(7, 21, pinHeadPurple);
        tex.SetPixel(6, 20, pinHeadPurple);

        // Alfiler 2: Clavado en la cabeza (arriba-derecha)
        tex.SetPixel(21, 25, pinSteel);
        tex.SetPixel(22, 26, pinSteel);
        tex.SetPixel(23, 27, pinSteel);
        tex.SetPixel(24, 28, pinHeadRed);
        tex.SetPixel(24, 29, pinHeadRed);
        tex.SetPixel(25, 28, pinHeadRed);

        // Alfiler 3: Clavado en el costado derecho
        tex.SetPixel(20, 12, pinSteel);
        tex.SetPixel(21, 11, pinSteel);
        tex.SetPixel(22, 10, pinHeadPurple);
        tex.SetPixel(23, 10, pinHeadPurple);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 40f);
    }

    /// <summary>
    /// Objeto 3 (Altar 3): Vela Ritual — Vela de cera derretida sobre palmatoria antigua con llama mística dorada y violeta.
    /// </summary>
    private static Sprite CreateRitualCandleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.12f, 0.08f, 0.14f, 1f);
        Color brassDark = new Color(0.42f, 0.30f, 0.14f, 1f);
        Color brassMid = new Color(0.68f, 0.52f, 0.22f, 1f);
        Color brassLight = new Color(0.90f, 0.74f, 0.35f, 1f);
        Color waxDark = new Color(0.74f, 0.68f, 0.62f, 1f);
        Color waxMid = new Color(0.90f, 0.86f, 0.78f, 1f);
        Color waxLight = new Color(0.98f, 0.96f, 0.90f, 1f);
        Color flameOuterPurple = new Color(0.75f, 0.22f, 0.96f, 0.95f);
        Color flameMidOrange = new Color(1.0f, 0.56f, 0.18f, 1f);
        Color flameCoreYellow = new Color(1.0f, 0.96f, 0.65f, 1f);
        Color flameGlow = new Color(0.88f, 0.45f, 1.0f, 0.28f);

        // 1. Plato/Palmatoria de bronce en la base (Y = 2..6, X = 7..23)
        for (int y = 2; y <= 5; y++)
        {
            int halfW = (y == 2 || y == 5) ? 6 : 8;
            for (int x = 15 - halfW; x <= 15 + halfW; x++)
            {
                if (y == 2 || y == 5 || x == 15 - halfW || x == 15 + halfW)
                    tex.SetPixel(x, y, outline);
                else if (x < 15)
                    tex.SetPixel(x, y, brassLight);
                else if (x > 17)
                    tex.SetPixel(x, y, brassDark);
                else
                    tex.SetPixel(x, y, brassMid);
            }
        }

        // Asa lateral de la palmatoria (X = 22..26, Y = 3..7)
        tex.SetPixel(23, 4, brassMid);
        tex.SetPixel(24, 4, brassMid);
        tex.SetPixel(25, 5, brassMid);
        tex.SetPixel(25, 6, brassMid);
        tex.SetPixel(24, 7, brassMid);
        tex.SetPixel(23, 7, brassMid);

        // 2. Cuerpo cilíndrico de la vela con gotas de cera derretida (Y = 5..19, X = 11..19)
        for (int y = 5; y <= 19; y++)
        {
            for (int x = 11; x <= 19; x++)
            {
                if (x == 11 || x == 19 || y == 19)
                    tex.SetPixel(x, y, outline);
                else if (x <= 13)
                    tex.SetPixel(x, y, waxLight);
                else if (x >= 17)
                    tex.SetPixel(x, y, waxDark);
                else
                    tex.SetPixel(x, y, waxMid);
            }
        }

        // Gotas de cera derretida escurriendo por los bordes
        for (int y = 13; y <= 18; y++) tex.SetPixel(12, y, waxLight);
        for (int y = 10; y <= 18; y++) tex.SetPixel(15, y, waxLight);
        for (int y = 15; y <= 18; y++) tex.SetPixel(17, y, waxMid);
        tex.SetPixel(10, 16, waxLight);
        tex.SetPixel(10, 15, waxLight);
        tex.SetPixel(20, 14, waxDark);

        // 3. Pabilo / mecha negra (Y = 20..21)
        tex.SetPixel(15, 20, outline);
        tex.SetPixel(15, 21, outline);

        // 4. Halo de luz ritual alrededor de la flama
        Vector2 flameCenter = new Vector2(15.0f, 25.0f);
        for (int y = 19; y < 32; y++)
        {
            for (int x = 8; x < 23; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), flameCenter);
                if (d <= 6.2f)
                {
                    tex.SetPixel(x, y, flameGlow);
                }
            }
        }

        // 5. Flama de la vela (Y = 21..30) con borde violeta místico y núcleo dorado ardiente
        for (int y = 21; y <= 30; y++)
        {
            float t = (y - 21f) / 9f; // 0 en la base, 1 en la punta
            float width = Mathf.Sin(Mathf.Pow(1f - t, 0.65f) * Mathf.PI) * 3.4f;
            float sway = (t > 0.6f) ? (t - 0.6f) * 1.5f : 0f;
            float cx = 15.0f + sway;

            for (int x = 11; x <= 20; x++)
            {
                float dx = Mathf.Abs(x - cx);
                if (dx <= width)
                {
                    if (dx >= width - 0.9f || y >= 29)
                        tex.SetPixel(x, y, flameOuterPurple);
                    else if (dx >= width * 0.45f || y >= 27)
                        tex.SetPixel(x, y, flameMidOrange);
                    else
                        tex.SetPixel(x, y, flameCoreYellow);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 40f);
    }

    #endregion
}
