using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Arma que Otorga")]
    public WeaponType weaponToUnlock = WeaponType.BigKnife;
    public float bobAmplitude = 0.09f;
    public float bobFrequency = 2.8f;
    public AudioClip pickupClip;

    private Vector3 startPos;
    private bool pickedUp;
    private float spawnTime;

    private static Sprite fallbackKnifeSprite;
    private static Sprite fallbackMacheteSprite;

    private void Awake()
    {
        startPos = transform.position;
        spawnTime = Time.time;
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (pickedUp) return;
        float offset = Mathf.Sin((Time.time - spawnTime) * bobFrequency) * bobAmplitude;
        transform.position = startPos + Vector3.up * offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - spawnTime > 0.25f)
            TryPickup(other);
    }

    private void TryPickup(Collider2D other)
    {
        if (pickedUp) return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead) return;

        PlayerWeaponController weapons = health.GetComponent<PlayerWeaponController>();
        if (weapons == null)
        {
            weapons = health.gameObject.AddComponent<PlayerWeaponController>();
        }

        pickedUp = true;
        weapons.UnlockAndEquip(weaponToUnlock);

        if (pickupClip != null)
        {
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.9f);
        }

        Destroy(gameObject);
    }

    public static GameObject SpawnWeaponDrop(WeaponType weapon, Vector3 position)
    {
        string objName = weapon == WeaponType.Machete ? "Pickup_Machete" : "Pickup_CuchilloGrande";
        GameObject drop = new GameObject(objName);
        drop.transform.position = position;

        SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
        sr.sprite = weapon == WeaponType.Machete ? GetOrCreateMacheteSprite() : GetOrCreateKnifeSprite();
        sr.sortingOrder = 520;

        CircleCollider2D col = drop.AddComponent<CircleCollider2D>();
        col.radius = 0.38f;
        col.isTrigger = true;

        WeaponPickup pickup = drop.AddComponent<WeaponPickup>();
        pickup.weaponToUnlock = weapon;

        return drop;
    }

    private static Sprite GetOrCreateKnifeSprite()
    {
        if (fallbackKnifeSprite != null) return fallbackKnifeSprite;

        int size = 24;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.18f, 0.14f, 0.12f, 1f);
        Color handle = new Color(0.45f, 0.28f, 0.15f, 1f);
        Color guardCol = new Color(0.35f, 0.36f, 0.4f, 1f);
        Color bladeMid = new Color(0.68f, 0.72f, 0.78f, 1f);
        Color bladeEdge = new Color(0.92f, 0.95f, 1f, 1f);

        // Dibujar cuchillo grande en diagonal (hoja ancha y rústica)
        for (int i = 3; i <= 18; i++)
        {
            if (i <= 7)
            {
                // Mango rústico de madera
                tex.SetPixel(i, i, handle);
                tex.SetPixel(i + 1, i, outline);
                tex.SetPixel(i, i + 1, outline);
            }
            else if (i == 8)
            {
                // Guarda
                for (int g = -2; g <= 2; g++)
                    tex.SetPixel(i + g, i - g, guardCol);
            }
            else
            {
                // Hoja ancha (3-4 px de grosor)
                tex.SetPixel(i, i, bladeMid);
                tex.SetPixel(i - 1, i + 1, bladeMid);
                tex.SetPixel(i + 1, i - 1, bladeEdge);
                tex.SetPixel(i - 2, i + 1, outline);
                tex.SetPixel(i + 1, i - 2, outline);
            }
        }

        tex.Apply();
        fallbackKnifeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return fallbackKnifeSprite;
    }

    private static Sprite GetOrCreateMacheteSprite()
    {
        if (fallbackMacheteSprite != null) return fallbackMacheteSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.12f, 0.12f, 0.16f, 1f);
        Color handle = new Color(0.28f, 0.16f, 0.1f, 1f);
        Color bladeDark = new Color(0.52f, 0.58f, 0.68f, 1f);
        Color bladeLight = new Color(0.88f, 0.95f, 1f, 1f);

        // Dibujar machete largo y pesado con punta ensanchada
        for (int i = 3; i <= 26; i++)
        {
            if (i <= 8)
            {
                tex.SetPixel(i, i, handle);
                tex.SetPixel(i + 1, i, handle);
                tex.SetPixel(i - 1, i, outline);
            }
            else
            {
                int width = i >= 19 ? 3 : 2;
                for (int w = 0; w <= width; w++)
                {
                    Color c = (w == width) ? bladeLight : bladeDark;
                    tex.SetPixel(i + w, i - w, c);
                }
                tex.SetPixel(i - 1, i + 1, outline);
                tex.SetPixel(i + width + 1, i - width - 1, outline);
            }
        }

        tex.Apply();
        fallbackMacheteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return fallbackMacheteSprite;
    }
}

