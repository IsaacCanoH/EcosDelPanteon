using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class WeaponPickup : MonoBehaviour
{
    public WeaponType weaponType = WeaponType.BigKnife;
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 3.2f;
    public AudioClip pickupSound;

    private Vector3 startPos;
    private Vector3 baseScale;
    private SpriteRenderer sr;
    private SpriteRenderer auraSr;
    private Collider2D col;
    private bool collected = false;

    private static Sprite cachedKnifeSprite;
    private static Sprite cachedMacheteSprite;
    private static Sprite cachedGlowSprite;

    public static bool IsWeaponDropActive(WeaponType type)
    {
        foreach (var wp in FindObjectsOfType<WeaponPickup>())
        {
            if (wp != null && !wp.collected && wp.weaponType == type)
                return true;
        }
        return false;
    }

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (col is CircleCollider2D circle)
        {
            circle.radius = 0.65f;
        }

        sr = GetComponent<SpriteRenderer>();
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 2;

        SetupVisual();
        CreateGroundAura();
    }

    private void Start()
    {
        startPos = transform.position;
        StartCoroutine(AppearBounceRoutine());
    }

    private void CreateGroundAura()
    {
        GameObject auraObj = new GameObject("WeaponGroundAura");
        auraObj.transform.SetParent(transform, false);
        auraObj.transform.localPosition = Vector3.zero;
        auraSr = auraObj.AddComponent<SpriteRenderer>();
        auraSr.sprite = GetOrCreateGlowSprite();
        auraSr.sortingLayerName = "Layer 1";
        auraSr.sortingOrder = 1;
        auraSr.color = weaponType == WeaponType.BigKnife
            ? new Color(1f, 0.88f, 0.35f, 0.55f)
            : new Color(0.35f, 0.92f, 1f, 0.65f);
        auraObj.transform.localScale = Vector3.one * 1.45f;
    }

    private IEnumerator AppearBounceRoutine()
    {
        float duration = 0.42f;
        float t = 0f;
        while (t < duration && !collected)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float bounce = Mathf.Sin(p * Mathf.PI) * 0.28f;
            transform.localScale = baseScale * (Mathf.SmoothStep(0f, 1f, p) + bounce);
            yield return null;
        }
        if (!collected) transform.localScale = baseScale;
    }

    private void Update()
    {
        if (collected) return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = startPos + new Vector3(0f, bob, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2.2f) * 8f);

        if (auraSr != null)
        {
            float pulse = 0.45f + Mathf.PingPong(Time.time * 1.2f, 0.3f);
            Color c = auraSr.color;
            c.a = pulse;
            auraSr.color = c;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && Vector2.Distance(transform.position, p.transform.position) <= 1.35f)
        {
            Collider2D pCol = p.GetComponent<Collider2D>();
            if (pCol != null) OnTriggerEnter2D(pCol);
        }
    }

    public void SetupVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
        if (cachedKnifeSprite == null)
            cachedKnifeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_CuchilloGrande.png");
        if (cachedMacheteSprite == null)
            cachedMacheteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_Machete.png");
#endif

        if (weaponType == WeaponType.BigKnife)
        {
            sr.sprite = cachedKnifeSprite != null ? cachedKnifeSprite : GetOrCreateFallbackKnifeSprite();
            baseScale = Vector3.one * 0.42f;
        }
        else if (weaponType == WeaponType.Machete)
        {
            sr.sprite = cachedMacheteSprite != null ? cachedMacheteSprite : GetOrCreateFallbackMacheteSprite();
            baseScale = Vector3.one * 0.46f;
        }

        transform.localScale = baseScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerWeaponController>() == null)
            return;

        PlayerWeaponController weaponController = other.GetComponentInParent<PlayerWeaponController>();
        if (weaponController == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                weaponController = p.GetComponent<PlayerWeaponController>();
                if (weaponController == null) weaponController = p.AddComponent<PlayerWeaponController>();
            }
        }

        if (weaponController != null)
        {
            collected = true;
            if (col != null) col.enabled = false;

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, AudioManager.EnemyVolume);
            }

            StartCoroutine(CollectWeaponAnimationRoutine(weaponController));
        }
    }

    private IEnumerator CollectWeaponAnimationRoutine(PlayerWeaponController weaponController)
    {
        Vector3 collectStartPos = transform.position;
        Vector3 collectStartScale = transform.localScale;

        // Animación de recolección: el arma se eleva hacia la mano del jugador girando con un destello
        float duration = 0.42f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / duration);

            Vector3 handTarget = weaponController != null
                ? weaponController.transform.position + new Vector3(0.15f, 0.42f, 0f)
                : collectStartPos + new Vector3(0f, 0.55f, 0f);

            transform.position = Vector3.Lerp(collectStartPos, handTarget, Mathf.SmoothStep(0f, 1f, p))
                                 + new Vector3(0f, Mathf.Sin(p * Mathf.PI) * 0.32f, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, p * 360f);

            float scaleCurve = p < 0.35f
                ? Mathf.Lerp(1f, 1.3f, p / 0.35f)
                : Mathf.Lerp(1.3f, 0.25f, (p - 0.35f) / 0.65f);
            transform.localScale = collectStartScale * scaleCurve;

            if (auraSr != null)
            {
                auraSr.transform.localScale = Vector3.one * Mathf.Lerp(1.45f, 2.8f, p);
                Color ac = auraSr.color;
                ac.a = (1f - p) * 0.8f;
                auraSr.color = ac;
            }

            yield return null;
        }

        if (weaponController != null)
        {
            weaponController.UnlockAndEquip(weaponType);
        }

        Level2HUD hud = FindObjectOfType<Level2HUD>();
        if (hud != null)
        {
            string weaponName = weaponType == WeaponType.BigKnife ? "CUCHILLO EQUIPADO (28 DMG)" : "MACHETE EQUIPADO (50 DMG)";
            string sub = weaponType == WeaponType.BigKnife
                ? "Ataca con [F] | Cambia con [1] y [2]"
                : "Ataca con [F] | Cambia con [1], [2] y [3]";
            hud.ShowNotification(weaponName, sub, 3.0f);
        }

        if (Level2VictoryManager.Instance != null)
        {
            Level2VictoryManager.Instance.CheckVictoryConditions();
        }

        Destroy(gameObject);
    }

    public static WeaponPickup SpawnWeaponDrop(WeaponType type, Vector3 position)
    {
        foreach (var existing in FindObjectsOfType<WeaponPickup>())
        {
            if (existing != null && !existing.collected && existing.weaponType == type)
            {
                existing.transform.position = position;
                existing.startPos = position;
                return existing;
            }
        }

        GameObject go = new GameObject($"Pickup_{type}");
        go.transform.position = position;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.65f;
        col.isTrigger = true;

        go.AddComponent<SpriteRenderer>();
        WeaponPickup pickup = go.AddComponent<WeaponPickup>();
        pickup.weaponType = type;
        pickup.SetupVisual();
        return pickup;
    }

    private static Sprite GetOrCreateGlowSprite()
    {
        if (cachedGlowSprite != null) return cachedGlowSprite;

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
        cachedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return cachedGlowSprite;
    }

    private static Sprite GetOrCreateFallbackKnifeSprite()
    {
        if (cachedKnifeSprite != null) return cachedKnifeSprite;

        int s = 24;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int x = 0; x < s; x++)
            for (int y = 0; y < s; y++)
                tex.SetPixel(x, y, Color.clear);

        Color handle = new Color(0.50f, 0.26f, 0.10f, 1f);
        Color guardCol = new Color(0.78f, 0.68f, 0.25f, 1f);
        Color blade = new Color(0.90f, 0.94f, 0.98f, 1f);

        for (int i = 3; i < 8; i++)
        {
            tex.SetPixel(i, i, handle);
            tex.SetPixel(i + 1, i, handle);
        }
        for (int i = -2; i <= 2; i++)
        {
            tex.SetPixel(8 - i, 8 + i, guardCol);
        }
        for (int i = 9; i < 20; i++)
        {
            tex.SetPixel(i, i, blade);
            tex.SetPixel(i - 1, i, blade);
        }

        tex.Apply();
        cachedKnifeSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.2f, 0.2f), 20f);
        return cachedKnifeSprite;
    }

    private static Sprite GetOrCreateFallbackMacheteSprite()
    {
        if (cachedMacheteSprite != null) return cachedMacheteSprite;

        int s = 32;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int x = 0; x < s; x++)
            for (int y = 0; y < s; y++)
                tex.SetPixel(x, y, Color.clear);

        Color handle = new Color(0.38f, 0.16f, 0.08f, 1f);
        Color blade = new Color(0.82f, 0.90f, 0.96f, 1f);

        for (int i = 3; i < 9; i++)
        {
            tex.SetPixel(i, i, handle);
            tex.SetPixel(i + 1, i, handle);
        }
        for (int i = 9; i < 27; i++)
        {
            tex.SetPixel(i, i, blade);
            tex.SetPixel(i - 1, i, blade);
            tex.SetPixel(i - 2, i, blade);
        }

        tex.Apply();
        cachedMacheteSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.18f, 0.18f), 20f);
        return cachedMacheteSprite;
    }
}
