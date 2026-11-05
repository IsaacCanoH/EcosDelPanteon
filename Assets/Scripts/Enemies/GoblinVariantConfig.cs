using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(EnemyHealth))]
public class GoblinVariantConfig : MonoBehaviour
{
    [Header("Configuración de Variante")]
    public bool isChubby = false;
    public bool isArcher = false;

    [Header("Ajustes Visuales para Duende Gordito")]
    public Vector3 chubbyScale = new Vector3(1.28f, 1.16f, 1f);
    public Color chubbySkinColor = new Color(0.58f, 0.78f, 0.55f, 1f); // Verde más oscuro y pesado

    [Header("Ajustes Visuales para Duende Arquero")]
    public Color archerSkinColor = new Color(0.84f, 0.96f, 0.78f, 1f); // Tono cazador ágil

    private void Awake()
    {
        ApplyConfiguration();
    }

    public void SetVariant(bool chubby, bool archer = false)
    {
        isChubby = chubby && !archer;
        isArcher = archer;
        ApplyConfiguration();
    }

    public void SetArcherVariant(bool archer)
    {
        SetVariant(false, archer);
    }

    private void ApplyConfiguration()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        GoblinAI ai = GetComponent<GoblinAI>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (isArcher)
        {
            // Duende Arquero: 50 HP (misma vida que el duende normal), suelta 1 orbe de vida al morir,
            // dispara flechas rápidas con carga e icono de aviso desde lejos, y cambia a cuchillo cuerpo a cuerpo de cerca.
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.ArcherGoblin);
            }

            if (ai != null)
            {
                ai.attackDamage = 20;
                ai.moveSpeed = 1.35f;
            }

            transform.localScale = Vector3.one;

            if (sr != null)
            {
                sr.color = archerSkinColor;
            }

            GoblinArcherController archerCtrl = GetComponent<GoblinArcherController>();
            if (archerCtrl == null) archerCtrl = gameObject.AddComponent<GoblinArcherController>();
            archerCtrl.enabled = true;
        }
        else if (isChubby)
        {
            var archerCtrl = GetComponent<GoblinArcherController>();
            if (archerCtrl != null) archerCtrl.enabled = false;

            // Duende Gordito: 80 HP, 40 daño (2 corazones), más lento (0.95), suelta 1 orbe de vida (+1 corazón)
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.ChubbyGoblin);
            }

            if (ai != null)
            {
                ai.attackDamage = 40;
                ai.moveSpeed = 0.95f;
            }

            transform.localScale = chubbyScale;

            if (sr != null)
            {
                sr.color = chubbySkinColor;
            }

            if (col != null)
            {
                col.size = new Vector2(0.55f, 0.42f);
            }
        }
        else
        {
            var archerCtrl = GetComponent<GoblinArcherController>();
            if (archerCtrl != null) archerCtrl.enabled = false;

            // Duende Normal: 50 HP, 20 daño (1 corazón), velocidad estándar (1.35f)
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.NormalGoblin);
            }

            if (ai != null)
            {
                ai.attackDamage = 20;
                ai.moveSpeed = 1.35f;
            }

            transform.localScale = Vector3.one;

            if (sr != null)
            {
                sr.color = Color.white;
            }
        }
    }
}

/// <summary>
/// Controlador del Duende Arquero (Nivel 3):
/// - Misma vida que un duende normal (50 HP) y suelta orbe de vida al morir.
/// - Cuando el jugador está lejos: se mantiene a distancia con su arco, carga el disparo mostrando un icono de aviso
///   encima de su cabeza y dispara una flecha rápida hacia el jugador.
/// - Cuando el jugador se acerca: cancela el arco, cambia visualmente su arco por un cuchillo como los otros duendes
///   y ataca cuerpo a cuerpo hasta que el jugador se aleje o lo mate.
/// </summary>
public class GoblinArcherController : MonoBehaviour
{
    [Header("Distancias de Cambio Arco / Cuchillo")]
    public float meleeSwitchDistance = 2.35f;     // Si el jugador se acerca a menos de 2.35m, saca el cuchillo y ataca de cerca
    public float rangedReturnDistance = 2.85f;    // Si el jugador se aleja a más de 2.85m, vuelve a sacar el arco
    public float idealRangedStopDistance = 4.6f;  // Distancia a la que se detiene para disparar con el arco
    public float maxRangedShootDistance = 7.8f;   // Alcance máximo de disparo con arco

    [Header("Parámetros de Disparo con Arco")]
    public float chargeDuration = 0.85f;          // Tiempo de carga antes del disparo rápido (con icono de aviso encima)
    public float arrowSpeed = 10.5f;              // Disparo rápido de flecha
    public int arrowDamage = 20;                  // 1 corazón de daño
    public float rangedCooldown = 1.55f;          // Pausa entre disparos

    public bool IsInMeleeMode { get; private set; } = false;
    public bool IsChargingBow { get; private set; } = false;

    private GoblinAI goblinAI;
    private EnemyHealth enemyHealth;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer bodyRenderer;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    // Visual del arma en la mano (Arco o Cuchillo)
    private GameObject heldWeaponObj;
    private SpriteRenderer heldWeaponRenderer;

    // Icono de aviso de carga encima del duende
    private GameObject chargeWarningObj;
    private SpriteRenderer chargeWarningRenderer;
    private SpriteRenderer chargeFillRenderer;

    private float chargeElapsed = 0f;
    private float nextRangedShotTime = 0f;
    private Vector2 lockedAimDirection = Vector2.down;

    private static Sprite cachedBowSprite;
    private static Sprite cachedBowDrawnSprite;
    private static Sprite cachedKnifeSprite;
    private static Sprite cachedWarningIconSprite;
    private static Sprite cachedArrowSprite;
    private static AudioClip cachedBowShootClip;

    private void Awake()
    {
        goblinAI = GetComponent<GoblinAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        bodyRenderer = GetComponent<SpriteRenderer>();

        CreateVisualAttachments();
    }

    private void Start()
    {
        FindPlayer();
        nextRangedShotTime = Time.time + Random.Range(0.55f, 1.15f);
        ApplyRangedModeSettings();
    }

    private void OnDisable()
    {
        if (heldWeaponObj != null) heldWeaponObj.SetActive(false);
        if (chargeWarningObj != null) chargeWarningObj.SetActive(false);
    }

    private void OnEnable()
    {
        if (heldWeaponObj != null) heldWeaponObj.SetActive(true);
    }

    private void FindPlayer()
    {
        if (playerTransform != null) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    private void CreateVisualAttachments()
    {
        EnsureSprites();

        // Agrupar el cuerpo del duende y su arma (arco/cuchillo) en un mismo SortingGroup en Layer 1 (order 2),
        // exactamente igual que el Jugador, para que el orden Y por pivote contra los árboles oculte tanto al duende
        // como a su arco/cuchillo cuando camina detrás de la copa de un árbol.
        SortingGroup rootGroup = GetComponent<SortingGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<SortingGroup>();
        rootGroup.sortingLayerName = "Layer 1";
        rootGroup.sortingOrder = 2;

        if (bodyRenderer != null)
        {
            bodyRenderer.sortingLayerName = "Layer 1";
            bodyRenderer.sortingOrder = 2;
            bodyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        // 1. Arma sostenida en la mano del duende (Arco cuando está lejos, Cuchillo cuando está cerca)
        Transform existingWeapon = transform.Find("Arma_Duende_Arquero");
        if (existingWeapon != null)
        {
            heldWeaponObj = existingWeapon.gameObject;
            heldWeaponRenderer = heldWeaponObj.GetComponent<SpriteRenderer>();
        }
        else
        {
            heldWeaponObj = new GameObject("Arma_Duende_Arquero");
            heldWeaponObj.transform.SetParent(transform, false);
            heldWeaponObj.transform.localPosition = new Vector3(0.22f, 0.28f, 0f);
            heldWeaponRenderer = heldWeaponObj.AddComponent<SpriteRenderer>();
        }

        heldWeaponRenderer.sprite = cachedBowSprite;
        heldWeaponRenderer.sortingLayerName = "Layer 1";
        heldWeaponRenderer.sortingOrder = 3;
        heldWeaponRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // 2. Icono de aviso encima de su cabeza cuando está cargando el disparo
        Transform existingWarn = transform.Find("Icono_Carga_Disparo");
        if (existingWarn != null)
        {
            chargeWarningObj = existingWarn.gameObject;
            chargeWarningRenderer = chargeWarningObj.GetComponent<SpriteRenderer>();
        }
        else
        {
            chargeWarningObj = new GameObject("Icono_Carga_Disparo");
            chargeWarningObj.transform.SetParent(transform, false);
            chargeWarningObj.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            chargeWarningRenderer = chargeWarningObj.AddComponent<SpriteRenderer>();
        }

        SortingGroup warnGroup = chargeWarningObj.GetComponent<SortingGroup>();
        if (warnGroup == null) warnGroup = chargeWarningObj.AddComponent<SortingGroup>();
        warnGroup.sortingLayerName = "Layer 2";
        warnGroup.sortingOrder = 60;

        chargeWarningRenderer.sprite = cachedWarningIconSprite;
        chargeWarningRenderer.sortingLayerName = "Layer 2";
        chargeWarningRenderer.sortingOrder = 60;
        chargeWarningObj.SetActive(false);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            if (chargeWarningObj != null) chargeWarningObj.SetActive(false);
            if (heldWeaponObj != null) heldWeaponObj.SetActive(false);
            return;
        }

        if (goblinAI != null && goblinAI.IsStunned)
        {
            CancelBowCharge();
            return;
        }

        FindPlayer();
        if (playerTransform == null || (playerHealth != null && playerHealth.IsDead))
        {
            CancelBowCharge();
            return;
        }

        Vector2 myPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toPlayer = (Vector2)playerTransform.position - myPos;
        float distToPlayer = toPlayer.magnitude;

        // 1. Verificar cambio entre modo Arco (lejos) y modo Cuchillo cuerpo a cuerpo (cerca)
        if (!IsInMeleeMode && distToPlayer <= meleeSwitchDistance)
        {
            SwitchToMeleeKnifeMode();
        }
        else if (IsInMeleeMode && distToPlayer >= rangedReturnDistance)
        {
            SwitchToRangedBowMode();
        }

        // Actualizar orientación visual del arma en la mano
        UpdateHeldWeaponTransform(toPlayer);

        // 2. Si está en modo Cuchillo (cerca del jugador), GoblinAI se encarga de perseguir y atacar cuerpo a cuerpo
        if (IsInMeleeMode)
        {
            return;
        }

        // 3. Modo Arco (a distancia)
        bool isAlert = goblinAI == null || goblinAI.IsAlerted;
        if (!isAlert)
        {
            CancelBowCharge();
            return;
        }

        if (IsChargingBow)
        {
            UpdateBowCharge(toPlayer, distToPlayer);
        }
        else
        {
            // Si está en rango de tiro con arco y tiene línea de visión, iniciar la carga del disparo
            if (distToPlayer > meleeSwitchDistance &&
                distToPlayer <= maxRangedShootDistance &&
                Time.time >= nextRangedShotTime &&
                HasLineOfSightToPlayer(myPos, playerTransform.position))
            {
                BeginBowCharge(toPlayer);
            }
        }
    }

    private void SwitchToMeleeKnifeMode()
    {
        IsInMeleeMode = true;
        CancelBowCharge();

        if (heldWeaponRenderer != null)
        {
            heldWeaponRenderer.sprite = cachedKnifeSprite;
        }

        if (goblinAI != null)
        {
            goblinAI.stopDistance = 0.78f;
            goblinAI.attackRange = 1.50f;
            goblinAI.enableLungeAttack = true;
        }
    }

    private void SwitchToRangedBowMode()
    {
        IsInMeleeMode = false;
        CancelBowCharge();

        if (heldWeaponRenderer != null)
        {
            heldWeaponRenderer.sprite = cachedBowSprite;
        }

        ApplyRangedModeSettings();
    }

    private void ApplyRangedModeSettings()
    {
        if (goblinAI != null)
        {
            // Se mantiene a distancia para disparar flechas, sin lanzar acometida cuerpo a cuerpo mientras tenga el arco
            goblinAI.stopDistance = idealRangedStopDistance;
            goblinAI.attackRange = 0.25f;
            goblinAI.enableLungeAttack = false;
        }
    }

    private void BeginBowCharge(Vector2 toPlayer)
    {
        IsChargingBow = true;
        chargeElapsed = 0f;
        lockedAimDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.down;

        if (heldWeaponRenderer != null)
        {
            heldWeaponRenderer.sprite = cachedBowDrawnSprite;
        }

        if (chargeWarningObj != null)
        {
            chargeWarningObj.SetActive(true);
            chargeWarningObj.transform.localScale = Vector3.one;
        }

        FaceDirection(lockedAimDirection);
    }

    private void UpdateBowCharge(Vector2 toPlayer, float distToPlayer)
    {
        // Si el jugador se acerca durante la carga, cancelar disparo y sacar el cuchillo de inmediato
        if (distToPlayer <= meleeSwitchDistance)
        {
            SwitchToMeleeKnifeMode();
            return;
        }

        // Mantener al duende quieto apuntando mientras carga el disparo
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
        }

        if (toPlayer.sqrMagnitude > 0.001f)
        {
            lockedAimDirection = toPlayer.normalized;
            FaceDirection(lockedAimDirection);
        }

        chargeElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(chargeElapsed / Mathf.Max(0.15f, chargeDuration));

        // Animar el icono de aviso sobre la cabeza (pulso de alerta + parpadeo intenso justo antes de disparar)
        if (chargeWarningObj != null && chargeWarningRenderer != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.16f + progress * 0.22f;
            chargeWarningObj.transform.localScale = new Vector3(pulse, pulse, 1f);
            chargeWarningObj.transform.localPosition = new Vector3(0f, 1.36f + Mathf.Sin(Time.time * 16f) * 0.05f, 0f);

            Color warnColor = Color.Lerp(new Color(1f, 0.88f, 0.25f, 1f), new Color(1f, 0.22f, 0.18f, 1f), progress);
            chargeWarningRenderer.color = warnColor;
        }

        if (chargeElapsed >= chargeDuration)
        {
            FireFastArrow(lockedAimDirection);
            CancelBowCharge();
            nextRangedShotTime = Time.time + rangedCooldown;
        }
    }

    private void CancelBowCharge()
    {
        IsChargingBow = false;
        chargeElapsed = 0f;

        if (chargeWarningObj != null)
        {
            chargeWarningObj.SetActive(false);
        }

        if (heldWeaponRenderer != null)
        {
            heldWeaponRenderer.sprite = IsInMeleeMode ? cachedKnifeSprite : cachedBowSprite;
        }
    }

    private void FireFastArrow(Vector2 direction)
    {
        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        Vector3 spawnPos = transform.position + new Vector3(0f, 0.32f, 0f) + (Vector3)(dir * 0.42f);

        GameObject arrowObj = new GameObject("Flecha_Duende");
        arrowObj.transform.position = spawnPos;

        GoblinArrowProjectile arrow = arrowObj.AddComponent<GoblinArrowProjectile>();
        arrow.Launch(dir, arrowSpeed, arrowDamage, cachedArrowSprite);

        // Sonido de cuerda de arco rápida
        AudioClip shootClip = GetOrCreateBowShootClip();
        if (shootClip != null)
        {
            AudioSource.PlayClipAtPoint(shootClip, transform.position, 0.85f * AudioManager.EnemyVolume);
        }
    }

    private void FaceDirection(Vector2 dir)
    {
        if (animator == null || dir.sqrMagnitude < 0.0001f) return;
        int d = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
            ? (dir.x > 0f ? 2 : 3)
            : (dir.y > 0f ? 1 : 0);
        animator.SetInteger("Direction", d);
    }

    private void UpdateHeldWeaponTransform(Vector2 toPlayer)
    {
        if (heldWeaponObj == null || heldWeaponRenderer == null) return;

        Vector2 aimDir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.down;
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        // Colocar el arco o el cuchillo a la altura de la mano del duende apuntando hacia el jugador
        Vector3 handCenter = new Vector3(0f, 0.26f, 0f);
        float holdRadius = IsInMeleeMode ? 0.22f : 0.26f;
        heldWeaponObj.transform.localPosition = handCenter + (Vector3)(aimDir * holdRadius);
        heldWeaponObj.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        heldWeaponObj.transform.localScale = IsInMeleeMode ? new Vector3(0.85f, 0.85f, 1f) : new Vector3(0.95f, 0.95f, 1f);

        int baseOrder = bodyRenderer != null ? bodyRenderer.sortingOrder : 2;
        heldWeaponRenderer.sortingOrder = aimDir.y > 0.35f ? Mathf.Max(1, baseOrder - 1) : baseOrder + 1;
    }

    private bool HasLineOfSightToPlayer(Vector2 from, Vector2 to)
    {
        int mask = LayerMask.GetMask("Obstacles", "Wall");
        if (mask == 0) return true;
        RaycastHit2D hit = Physics2D.Linecast(from + new Vector2(0f, 0.25f), to + new Vector2(0f, 0.25f), mask);
        return hit.collider == null || hit.collider.isTrigger;
    }

    private static void EnsureSprites()
    {
        if (cachedBowSprite == null) cachedBowSprite = CreateBowSprite(drawn: false);
        if (cachedBowDrawnSprite == null) cachedBowDrawnSprite = CreateBowSprite(drawn: true);
        if (cachedKnifeSprite == null) cachedKnifeSprite = CreateKnifeSprite();
        if (cachedWarningIconSprite == null) cachedWarningIconSprite = CreateWarningIconSprite();
        if (cachedArrowSprite == null) cachedArrowSprite = CreateArrowSprite();
    }

    private static Sprite CreateBowSprite(bool drawn)
    {
        int w = 18;
        int h = 18;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color woodDark = new Color(0.32f, 0.18f, 0.08f, 1f);
        Color woodMid = new Color(0.56f, 0.34f, 0.14f, 1f);
        Color woodLight = new Color(0.74f, 0.48f, 0.22f, 1f);
        Color stringCol = new Color(0.86f, 0.84f, 0.78f, 0.95f);
        Color arrowShaft = new Color(0.65f, 0.44f, 0.20f, 1f);
        Color arrowTip = new Color(0.88f, 0.92f, 0.98f, 1f);
        Color feather = new Color(0.92f, 0.25f, 0.22f, 1f);

        // Arco curvo mirando hacia la derecha (+X)
        int cy = 9;
        for (int y = 2; y <= 16; y++)
        {
            float dy = Mathf.Abs(y - cy) / 7f;
            int bx = Mathf.RoundToInt(Mathf.Lerp(11f, 6f, dy * dy));
            tex.SetPixel(bx, y, woodMid);
            tex.SetPixel(bx + 1, y, woodLight);
            tex.SetPixel(bx - 1, y, woodDark);
        }

        // Cuerda del arco (tensa hacia atrás si drawn == true)
        int pullX = drawn ? 3 : 6;
        for (int y = 3; y <= 15; y++)
        {
            float t = Mathf.Abs(y - cy) / 6f;
            int sx = Mathf.RoundToInt(Mathf.Lerp(pullX, 6f, t));
            tex.SetPixel(sx, y, stringCol);
        }

        // Si está cargando, dibujar la flecha encajada lista para disparar
        if (drawn)
        {
            for (int x = pullX; x <= 14; x++)
            {
                tex.SetPixel(x, cy, arrowShaft);
            }
            // Punta de acero
            tex.SetPixel(15, cy, arrowTip);
            tex.SetPixel(14, cy + 1, arrowTip);
            tex.SetPixel(14, cy - 1, arrowTip);
            tex.SetPixel(16, cy, arrowTip);
            // Plumas rojas
            tex.SetPixel(pullX, cy + 1, feather);
            tex.SetPixel(pullX, cy - 1, feather);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.45f, 0.5f), 32f);
    }

    private static Sprite CreateKnifeSprite()
    {
        int w = 16;
        int h = 10;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color hilt = new Color(0.48f, 0.26f, 0.12f, 1f);
        Color guardCol = new Color(0.32f, 0.34f, 0.38f, 1f);
        Color steelLight = new Color(0.92f, 0.95f, 1.0f, 1f);
        Color steelMid = new Color(0.68f, 0.72f, 0.78f, 1f);

        int cy = 5;
        // Empuñadura
        for (int x = 2; x <= 5; x++)
        {
            tex.SetPixel(x, cy, hilt);
            tex.SetPixel(x, cy - 1, hilt);
        }
        // Guarda
        for (int y = cy - 2; y <= cy + 1; y++)
        {
            tex.SetPixel(6, y, guardCol);
        }
        // Hoja del cuchillo
        for (int x = 7; x <= 13; x++)
        {
            tex.SetPixel(x, cy, steelLight);
            if (x <= 12) tex.SetPixel(x, cy - 1, steelMid);
        }
        tex.SetPixel(14, cy, steelLight);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.25f, 0.5f), 32f);
    }

    private static Sprite CreateWarningIconSprite()
    {
        int w = 18;
        int h = 18;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color bgDark = new Color(0.10f, 0.06f, 0.08f, 0.92f);
        Color borderGold = new Color(1.0f, 0.82f, 0.22f, 1f);
        Color exclamWhite = new Color(1.0f, 0.98f, 0.92f, 1f);
        Color bowMini = new Color(1.0f, 0.45f, 0.20f, 1f);

        Vector2 center = new Vector2(8.5f, 8.5f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y); // Rombo de alerta
                if (d <= 8.2f)
                {
                    tex.SetPixel(x, y, d >= 6.8f ? borderGold : bgDark);
                }
            }
        }

        // Signo de exclamación central (!) y arco lateral de aviso de disparo
        for (int y = 7; y <= 13; y++)
        {
            tex.SetPixel(8, y, exclamWhite);
            tex.SetPixel(9, y, exclamWhite);
        }
        tex.SetPixel(8, 4, exclamWhite);
        tex.SetPixel(9, 4, exclamWhite);
        tex.SetPixel(8, 5, exclamWhite);
        tex.SetPixel(9, 5, exclamWhite);

        // Silueta de arco/mira a los lados del signo
        for (int y = 6; y <= 11; y++)
        {
            tex.SetPixel(5, y, bowMini);
            tex.SetPixel(12, y, bowMini);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 28f);
    }

    private static Sprite CreateArrowSprite()
    {
        int w = 20;
        int h = 7;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.14f, 0.09f, 0.05f, 1f);
        Color shaft = new Color(0.76f, 0.54f, 0.24f, 1f);
        Color tipLight = new Color(0.95f, 0.98f, 1.0f, 1f);
        Color tipMid = new Color(0.68f, 0.76f, 0.86f, 1f);
        Color fletch = new Color(0.95f, 0.24f, 0.20f, 1f);

        int cy = 3;
        // Astil de madera con borde para alta visibilidad
        for (int x = 3; x <= 14; x++)
        {
            tex.SetPixel(x, cy, shaft);
            tex.SetPixel(x, cy + 1, outline);
            tex.SetPixel(x, cy - 1, outline);
        }

        // Plumas traseras rojas
        tex.SetPixel(1, cy + 1, fletch);
        tex.SetPixel(2, cy + 1, fletch);
        tex.SetPixel(2, cy + 2, fletch);
        tex.SetPixel(1, cy - 1, fletch);
        tex.SetPixel(2, cy - 1, fletch);
        tex.SetPixel(2, cy - 2, fletch);
        tex.SetPixel(2, cy, shaft);

        // Punta afilada de acero
        tex.SetPixel(15, cy + 2, tipMid);
        tex.SetPixel(15, cy - 2, tipMid);
        tex.SetPixel(15, cy + 1, tipLight);
        tex.SetPixel(15, cy - 1, tipLight);
        tex.SetPixel(15, cy, tipLight);
        tex.SetPixel(16, cy + 1, tipLight);
        tex.SetPixel(16, cy - 1, tipLight);
        tex.SetPixel(16, cy, tipLight);
        tex.SetPixel(17, cy, tipLight);
        tex.SetPixel(18, cy, tipLight);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    private static AudioClip GetOrCreateBowShootClip()
    {
        if (cachedBowShootClip != null) return cachedBowShootClip;

        int sampleRate = 22050;
        int samplesCount = Mathf.RoundToInt(0.16f * sampleRate);
        float[] data = new float[samplesCount];
        uint rng = 0x98765432u;

        for (int i = 0; i < samplesCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Exp(-t * 26f);
            float stringTwang = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 180f, t / 0.16f) * t) * 0.55f;
            rng = rng * 1664525u + 1013904223u;
            float whoosh = (((rng & 0xFFFFu) / 32768f) - 1f) * 0.45f;
            data[i] = Mathf.Clamp((stringTwang + whoosh) * env, -0.9f, 0.9f);
        }

        cachedBowShootClip = AudioClip.Create("GoblinBowShootSfx", samplesCount, 1, sampleRate, false);
        cachedBowShootClip.SetData(data, 0);
        return cachedBowShootClip;
    }
}

/// <summary>
/// Proyectil de flecha rápida disparada por el Duende Arquero.
/// </summary>
public class GoblinArrowProjectile : MonoBehaviour
{
    public float speed = 10.5f;
    public int damage = 20;
    public float lifetime = 3.2f;

    private Vector2 moveDirection = Vector2.right;
    private SpriteRenderer sr;
    private float spawnTime;
    private bool hitSomething = false;

    public void Launch(Vector2 direction, float arrowSpeed, int arrowDamage, Sprite arrowSprite)
    {
        spawnTime = Time.time;
        moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        speed = arrowSpeed;
        damage = arrowDamage;

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;

        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.20f;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = arrowSprite;
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 760;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (hitSomething) return;

        transform.position += (Vector3)(moveDirection * (speed * Time.deltaTime));

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitSomething || other == null) return;

        // Ignorar colisiones con otros enemigos o el jefe
        if (other.GetComponentInParent<EnemyHealth>() != null ||
            other.GetComponentInParent<GoblinAI>() != null ||
            other.GetComponentInParent<SpectralChildBossAI>() != null)
        {
            return;
        }

        // Impacto contra el jugador
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && !playerHealth.IsDead)
        {
            hitSomething = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Impacto contra muros u obstáculos sólidos
        if (!other.isTrigger && other.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
        {
            hitSomething = true;
            Destroy(gameObject);
        }
    }
}
