using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(EnemyHealth))]
public class GoblinVariantConfig : MonoBehaviour
{
    [Header("Configuración de Variante")]
    public bool isChubby = false;
    public bool isArcher = false;
    public bool hasGreenAura = false;

    [Header("Ajustes Visuales para Duende Gordito")]
    public Vector3 chubbyScale = new Vector3(1.28f, 1.16f, 1f);
    public Color chubbySkinColor = new Color(0.58f, 0.78f, 0.55f, 1f); // Verde más oscuro y pesado

    [Header("Ajustes Visuales para Duende Arquero Blindado")]
    public Vector3 archerScale = new Vector3(1.06f, 1.06f, 1f);
    public Color archerSkinColor = new Color(0.90f, 0.96f, 0.86f, 1f);

    private void Awake()
    {
        ApplyConfiguration();
    }

    public void SetVariant(bool chubby, bool archer = false, bool greenAura = false)
    {
        isChubby = chubby && !archer;
        isArcher = archer;
        hasGreenAura = greenAura;
        ApplyConfiguration();
    }

    public void SetArcherVariant(bool archer, bool greenAura = false)
    {
        SetVariant(false, archer, greenAura);
    }

    public void SetGreenAura(bool greenAura)
    {
        hasGreenAura = greenAura;
        ApplyConfiguration();
    }

    private void ApplyConfiguration()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        GoblinAI ai = GetComponent<GoblinAI>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        // Asegurar que ningún duende use WorldDepth2D y que todos estén en Layer 1 (order 2, pivote en pies)
        // para que nunca se vean a través de las copas de los árboles.
        WorldDepth2D depth2D = GetComponent<WorldDepth2D>();
        if (depth2D != null) Destroy(depth2D);

        SortingGroup rootGroup = GetComponent<SortingGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<SortingGroup>();
        rootGroup.sortingLayerName = "Layer 1";
        rootGroup.sortingOrder = 2;

        if (sr != null)
        {
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 2;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (isArcher)
        {
            // Duende Arquero Blindado: 110 HP normal / 150 HP con Aura Verde,
            // más rápido al moverse que un duende base (1.85f vs 1.35f), suelta 1 orbe de vida al morir,
            // dispara flechas rápidas con carga e icono de aviso desde lejos, y cambia a cuchillo cuerpo a cuerpo de cerca.
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.ArcherGoblin, hasGreenAura);
            }

            if (ai != null)
            {
                ai.attackDamage = 20;
                ai.moveSpeed = 1.85f;
                ai.patrolSpeed = 1.15f;
                ai.secondCoinSpeed = 2.40f;
            }

            transform.localScale = archerScale;

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

            // Duende Gordito / Fuerte: 80 HP normal / 110 HP con Aura Verde (igual que un arquero), 40 daño (2 corazones), suelta 1 orbe de vida
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.ChubbyGoblin, hasGreenAura);
            }

            if (ai != null)
            {
                ai.attackDamage = 40;
                ai.moveSpeed = 0.95f;
                ai.patrolSpeed = 0.55f;
                ai.secondCoinSpeed = 1.45f;
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

            // Duende Normal / Base: 50 HP normal / 80 HP con Aura Verde (igual que un duende fuerte), 20 daño (1 corazón)
            if (health != null)
            {
                health.ConfigureVariant(EnemyHealth.EnemyVariant.NormalGoblin, hasGreenAura);
            }

            if (ai != null)
            {
                ai.attackDamage = 20;
                ai.moveSpeed = 1.35f;
                ai.patrolSpeed = 0.65f;
                ai.secondCoinSpeed = 2.0f;
            }

            transform.localScale = Vector3.one;

            if (sr != null)
            {
                sr.color = Color.white;
            }
        }

        // Configurar el Aura Verde Mejorada si el duende fue potenciado por el hechizo de La Llorona
        GreenAuraVisualController auraCtrl = GetComponent<GreenAuraVisualController>();
        if (hasGreenAura)
        {
            if (auraCtrl == null) auraCtrl = gameObject.AddComponent<GreenAuraVisualController>();
            auraCtrl.enabled = true;
            auraCtrl.SetAuraActive(true);
        }
        else if (auraCtrl != null)
        {
            auraCtrl.SetAuraActive(false);
            auraCtrl.enabled = false;
        }
    }
}

/// <summary>
/// Controlador del Duende Arquero Blindado (Nivel 3):
/// - Luce blindado con Armadura de Cuero tachonada y un Casco reforzado con protector nasal.
/// - Tiene más vida que un duende grande (110 HP vs 80 HP), se mueve más rápido que un duende base (1.85 m/s vs 1.35 m/s) y suelta orbe de vida al morir.
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
    public float arrowSpeed = 8f;              // Disparo rápido de flecha
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
    private EncounterWaveManager encounter;

    // Visual de la Armadura de Cuero y Casco sobre el cuerpo del duende
    private GameObject armorOverlayObj;
    private SpriteRenderer armorOverlayRenderer;

    // Visual del arma en la mano (Arco o Cuchillo)
    private GameObject heldWeaponObj;
    private SpriteRenderer heldWeaponRenderer;

    // Icono de aviso de carga encima del duende
    private GameObject chargeWarningObj;
    private SpriteRenderer chargeWarningRenderer;

    private float chargeElapsed = 0f;
    private float nextRangedShotTime = 0f;
    private Vector2 lockedAimDirection = Vector2.down;

    private static Sprite[] cachedArmorHelmetSprites; // 0 = Down, 1 = Up, 2 = Right, 3 = Left
    private static Sprite cachedBowSprite;
    private static Sprite cachedBowDrawnSprite;
    private static Sprite cachedKnifeSprite;
    private static Sprite cachedWarningIconSprite;
    private static Sprite cachedArrowSprite;
    private static AudioClip cachedBowShootClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticArcherCache()
    {
        cachedArmorHelmetSprites = null;
        cachedBowSprite = null;
        cachedBowDrawnSprite = null;
        cachedKnifeSprite = null;
        cachedWarningIconSprite = null;
        cachedArrowSprite = null;
        cachedBowShootClip = null;
    }

    private void Awake()
    {
        goblinAI = GetComponent<GoblinAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        encounter = GetComponentInParent<EncounterWaveManager>();
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
        if (armorOverlayObj != null) armorOverlayObj.SetActive(false);
        if (heldWeaponObj != null) heldWeaponObj.SetActive(false);
        if (chargeWarningObj != null) chargeWarningObj.SetActive(false);
    }

    private void OnEnable()
    {
        if (armorOverlayObj != null) armorOverlayObj.SetActive(true);
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

        // Agrupar el cuerpo del duende, su armadura/casco y su arma (arco/cuchillo) en un mismo SortingGroup en Layer 1 (order 2),
        // exactamente igual que el Jugador, para que el orden Y por pivote contra los árboles oculte todo el conjunto detrás de las copas.
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

        // 1. Armadura de Cuero tachonada + Casco reforzado (sobre el cuerpo del duende)
        Transform existingArmor = transform.Find("Armadura_Cuero_Casco");
        if (existingArmor != null)
        {
            armorOverlayObj = existingArmor.gameObject;
            armorOverlayRenderer = armorOverlayObj.GetComponent<SpriteRenderer>();
        }
        else
        {
            armorOverlayObj = new GameObject("Armadura_Cuero_Casco");
            armorOverlayObj.transform.SetParent(transform, false);
            armorOverlayObj.transform.localPosition = Vector3.zero;
            armorOverlayRenderer = armorOverlayObj.AddComponent<SpriteRenderer>();
        }

        armorOverlayRenderer.sprite = cachedArmorHelmetSprites[0];
        armorOverlayRenderer.sortingLayerName = "Layer 1";
        armorOverlayRenderer.sortingOrder = 3;
        armorOverlayRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        if (bodyRenderer != null && bodyRenderer.sharedMaterial != null)
        {
            armorOverlayRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
        }

        // 2. Arma sostenida en la mano del duende (Arco cuando está lejos, Cuchillo cuando está cerca)
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
        heldWeaponRenderer.sortingOrder = 4;
        heldWeaponRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // 3. Icono de aviso encima de su cabeza cuando está cargando el disparo
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
            chargeWarningObj.transform.localPosition = new Vector3(0f, 1.44f, 0f);
            chargeWarningRenderer = chargeWarningObj.AddComponent<SpriteRenderer>();
        }

        SortingGroup warnGroup = chargeWarningObj.GetComponent<SortingGroup>();
        if (warnGroup != null) Destroy(warnGroup);

        chargeWarningRenderer.sprite = cachedWarningIconSprite;
        chargeWarningRenderer.sortingLayerName = "Layer 1";
        chargeWarningRenderer.sortingOrder = 6;
        chargeWarningObj.SetActive(false);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            if (chargeWarningObj != null) chargeWarningObj.SetActive(false);
            if (heldWeaponObj != null) heldWeaponObj.SetActive(false);
            if (armorOverlayObj != null) armorOverlayObj.SetActive(false);
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
                HasLineOfSightToPlayer(myPos, playerTransform.position) &&
                (encounter == null || encounter.TryReserveBowCharge()))
            {
                BeginBowCharge(toPlayer);
            }
        }
    }

    private void LateUpdate()
    {
        if (armorOverlayObj == null || armorOverlayRenderer == null) return;
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            armorOverlayObj.SetActive(false);
            return;
        }

        EnsureSprites();
        int dir = 0;
        bool isMoving = false;
        if (animator != null)
        {
            dir = Mathf.Clamp(animator.GetInteger("Direction"), 0, 3);
            isMoving = animator.GetBool("IsMoving");
        }

        if (cachedArmorHelmetSprites != null && cachedArmorHelmetSprites.Length >= 4)
        {
            armorOverlayRenderer.sprite = cachedArmorHelmetSprites[dir];
        }

        // Sincronizar ligero rebote al caminar y destello de daño con el cuerpo del duende
        float bobOffset = isMoving ? (Mathf.Sin(Time.time * 16f) > 0f ? 0.025f : 0f) : 0f;
        armorOverlayObj.transform.localPosition = new Vector3(0f, bobOffset, 0f);

        if (bodyRenderer != null)
        {
            armorOverlayRenderer.color = bodyRenderer.color;
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
            chargeWarningObj.transform.localPosition = new Vector3(0f, 1.42f + Mathf.Sin(Time.time * 16f) * 0.05f, 0f);

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
        var encounter = GetComponentInParent<EncounterWaveManager>();
        if (encounter != null) arrowObj.transform.SetParent(encounter.transform, true);
        arrowObj.transform.position = spawnPos;

        GoblinArrowProjectile arrow = arrowObj.AddComponent<GoblinArrowProjectile>();
        arrow.Launch(dir, arrowSpeed, arrowDamage, cachedArrowSprite);

        // Sonido de cuerda de arco rápida
        AudioClip shootClip = GetOrCreateBowShootClip();
        if (TryGetComponent(out GoblinAudio goblinAudio) && goblinAudio.ataques != null)
            goblinAudio.PlayBowShot(shootClip);
        else if (shootClip != null)
            AudioSource.PlayClipAtPoint(shootClip, transform.position, 0.85f * AudioManager.EnemyVolume);
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
        heldWeaponRenderer.sortingOrder = aimDir.y > 0.35f ? Mathf.Max(1, baseOrder - 1) : baseOrder + 2;
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
        if (cachedArmorHelmetSprites == null || cachedArmorHelmetSprites.Length < 4 || cachedArmorHelmetSprites[0] == null)
        {
            cachedArmorHelmetSprites = new Sprite[4];
            for (int d = 0; d < 4; d++)
            {
                cachedArmorHelmetSprites[d] = CreateLeatherArmorAndHelmetSprite(d);
            }
        }
        if (cachedBowSprite == null) cachedBowSprite = CreateBowSprite(drawn: false);
        if (cachedBowDrawnSprite == null) cachedBowDrawnSprite = CreateBowSprite(drawn: true);
        if (cachedKnifeSprite == null) cachedKnifeSprite = CreateKnifeSprite();
        if (cachedWarningIconSprite == null) cachedWarningIconSprite = CreateWarningIconSprite();
        if (cachedArrowSprite == null) cachedArrowSprite = CreateArrowSprite();
    }

    /// <summary>
    /// Genera el sprite de Armadura de Cuero tachonada (peto, hombreras, correa de carcaj y cinturón con hebilla)
    /// y Casco reforzado (cúpula de cuero hervido con aro de hierro, remaches y protector nasal) para el Duende Arquero.
    /// direction: 0 = Down, 1 = Up, 2 = Right, 3 = Left.
    /// </summary>
    private static Sprite CreateLeatherArmorAndHelmetSprite(int direction)
    {
        int w = 32;
        int h = 36;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outline = new Color(0.14f, 0.08f, 0.05f, 1f);
        Color leatherDark = new Color(0.36f, 0.20f, 0.09f, 1f);
        Color leatherMid = new Color(0.54f, 0.31f, 0.14f, 1f);
        Color leatherLight = new Color(0.70f, 0.44f, 0.22f, 1f);
        Color strapCol = new Color(0.26f, 0.14f, 0.07f, 1f);
        Color ironDark = new Color(0.34f, 0.38f, 0.44f, 1f);
        Color ironMid = new Color(0.56f, 0.62f, 0.70f, 1f);
        Color ironLight = new Color(0.84f, 0.88f, 0.94f, 1f);
        Color brassBuckle = new Color(0.95f, 0.78f, 0.26f, 1f);
        Color featherRed = new Color(0.90f, 0.22f, 0.20f, 1f);

        int cx = 16;

        // ====================================================================
        // 1. ARMADURA DE CUERO TACHONADA (Peto, Hombreras, Cinturón y Carcaj)
        //    Ubicada en el torso del duende (Y = 6..15)
        // ====================================================================
        for (int y = 6; y <= 14; y++)
        {
            int halfW = (y >= 12) ? 6 : 5;
            for (int x = cx - halfW; x <= cx + halfW; x++)
            {
                if (x == cx - halfW || x == cx + halfW || y == 6 || y == 14)
                {
                    tex.SetPixel(x, y, outline);
                }
                else if (y == 7)
                {
                    // Cinturón pesado de cuero oscuro con hebilla dorada al frente
                    tex.SetPixel(x, y, strapCol);
                    if (direction != 1 && Mathf.Abs(x - cx) <= 1)
                        tex.SetPixel(x, y, brassBuckle);
                }
                else
                {
                    Color c = (x < cx - 1) ? leatherLight : ((x > cx + 1) ? leatherDark : leatherMid);
                    tex.SetPixel(x, y, c);
                }
            }
        }

        // Hombreras de cuero reforzado (Pauldrons) en Y = 12..15
        for (int y = 12; y <= 15; y++)
        {
            for (int dx = 5; dx <= 7; dx++)
            {
                Color pCol = (y == 15 || dx == 7) ? outline : ((y == 14) ? leatherLight : leatherDark);
                tex.SetPixel(cx - dx, y, pCol);
                tex.SetPixel(cx + dx, y, pCol);
            }
            // Remaches metálicos en las hombreras
            tex.SetPixel(cx - 6, 13, ironLight);
            tex.SetPixel(cx + 6, 13, ironLight);
        }

        // Correa diagonal de cuero y tachuelas metálicas en el peto
        for (int i = -3; i <= 3; i++)
        {
            int sx = cx + i;
            int sy = 10 + (direction == 1 ? -i : i) / 2;
            if (sy >= 8 && sy <= 13)
            {
                tex.SetPixel(sx, sy, strapCol);
            }
        }
        tex.SetPixel(cx - 3, 12, ironMid);
        tex.SetPixel(cx + 3, 12, ironMid);
        tex.SetPixel(cx - 3, 9, ironMid);
        tex.SetPixel(cx + 3, 9, ironMid);

        // Puntas de flechas asomando del carcaj en la espalda
        int quiverX = (direction == 2) ? (cx - 6) : (cx + 5);
        tex.SetPixel(quiverX, 16, strapCol);
        tex.SetPixel(quiverX, 17, featherRed);
        tex.SetPixel(quiverX + 1, 17, featherRed);

        // ====================================================================
        // 2. CASCO REFORZADO DE CUERO Y HIERRO (Y = 21..30)
        //    Deja visibles los ojos y orejas del duende mientras protege su cráneo
        // ====================================================================
        for (int y = 22; y <= 29; y++)
        {
            float t = (y - 22f) / 7f; // 0 en la banda frontal, 1 en la cima del casco
            int halfW = Mathf.RoundToInt(Mathf.Lerp(7.0f, 3.5f, t * t));

            for (int x = cx - halfW; x <= cx + halfW; x++)
            {
                bool isBorder = (x == cx - halfW || x == cx + halfW || y == 29);
                if (isBorder)
                {
                    tex.SetPixel(x, y, outline);
                }
                else if (y == 22 || y == 23)
                {
                    // Banda frontal de acero remachado sobre las cejas
                    tex.SetPixel(x, y, (y == 23 && x % 3 == 0) ? ironLight : ironMid);
                }
                else if (Mathf.Abs(x - cx) <= 1)
                {
                    // Cresta central de hierro del casco
                    tex.SetPixel(x, y, (x == cx) ? ironLight : ironDark);
                }
                else
                {
                    // Cúpula de cuero endurecido
                    tex.SetPixel(x, y, (x < cx) ? leatherLight : leatherDark);
                }
            }
        }

        // Cresta superior del casco (Y = 30)
        tex.SetPixel(cx - 1, 30, outline);
        tex.SetPixel(cx, 30, ironLight);
        tex.SetPixel(cx + 1, 30, outline);

        // Protector nasal de hierro y carrilleras laterales según la dirección
        if (direction == 0) // Mirando hacia abajo (frente)
        {
            // Protector nasal vertical entre los ojos
            tex.SetPixel(cx, 21, ironLight);
            tex.SetPixel(cx, 20, ironMid);
            tex.SetPixel(cx, 19, ironDark);
            // Carrilleras laterales
            tex.SetPixel(cx - 6, 21, ironDark);
            tex.SetPixel(cx - 6, 20, outline);
            tex.SetPixel(cx + 6, 21, ironDark);
            tex.SetPixel(cx + 6, 20, outline);
        }
        else if (direction == 1) // Mirando hacia arriba (espalda)
        {
            // Cubrenuca de cuero y hierro en la parte trasera del casco
            for (int x = cx - 6; x <= cx + 6; x++)
            {
                tex.SetPixel(x, 21, (x == cx - 6 || x == cx + 6) ? outline : ironDark);
                tex.SetPixel(x, 20, (x == cx - 6 || x == cx + 6) ? outline : leatherDark);
            }
        }
        else if (direction == 2) // Mirando a la derecha
        {
            tex.SetPixel(cx + 5, 21, ironLight);
            tex.SetPixel(cx + 5, 20, ironMid);
            tex.SetPixel(cx - 4, 21, leatherDark);
            tex.SetPixel(cx - 4, 20, leatherDark);
        }
        else if (direction == 3) // Mirando a la izquierda
        {
            tex.SetPixel(cx - 5, 21, ironLight);
            tex.SetPixel(cx - 5, 20, ironMid);
            tex.SetPixel(cx + 4, 21, leatherDark);
            tex.SetPixel(cx + 4, 20, leatherDark);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.08f), 32f);
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
    private PlayerLantern playerLantern;
    private float spawnTime;
    private bool hitSomething = false;

    public void Launch(Vector2 direction, float arrowSpeed, int arrowDamage, Sprite arrowSprite)
    {
        spawnTime = Time.time;
        moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        speed = arrowSpeed;
        damage = arrowDamage;

        var player = PlayerHealth.ActiveInstance;
        playerLantern = player != null ? player.GetComponent<PlayerLantern>() : null;

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;

        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.20f;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = arrowSprite;
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 4;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (hitSomething) return;

        transform.position += (Vector3)(moveDirection * (speed * Time.deltaTime));
        if (playerLantern != null && playerLantern.TryDispelArrow(this)) return;

        // Verificación directa contra la hitbox de cuerpo completo del Player (cabeza, torso y pies)
        PlayerHealth activePlayer = PlayerHealth.ActiveInstance;
        if (activePlayer != null && !activePlayer.IsDead && activePlayer.IsProjectileTouchingBody(transform.position, 0.22f))
        {
            hitSomething = true;
            activePlayer.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    internal bool Dispel()
    {
        if (hitSomething) return false;
        hitSomething = true;
        if (sr != null) sr.enabled = false;
        Destroy(gameObject);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitSomething || other == null) return;
        if (playerLantern != null && playerLantern.TryDispelArrow(this)) return;

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

/// <summary>
/// Controlador visual mejorado del Aura Verde Espectral (otorgada por La Llorona en el Nivel 3).
/// Hace que los duendes potenciados sean muy fáciles de identificar mediante:
/// 1. Un anillo rúnico verde brillante giratorio/pulsante bajo sus pies.
/// 2. Un manto animado de llamas esmeralda detrás y alrededor de su silueta.
/// 3. Una corona de chispas espectrales verdes ascendentes y un resplandor esmeralda (Light2D).
/// Todos los sprites permanecen dentro del SortingGroup del duende en Layer 1 para ocultarse detrás de los árboles.
/// </summary>
public class GreenAuraVisualController : MonoBehaviour
{
    private GameObject auraRootObj;
    private SpriteRenderer groundRuneRenderer;
    private SpriteRenderer flameMantleRenderer;
    private SpriteRenderer sparkCrownRenderer;
    private UnityEngine.Rendering.Universal.Light2D auraLight;
    private EnemyHealth enemyHealth;
    private float animSeed;

    private static Sprite cachedGroundRuneSprite;
    private static Sprite[] cachedFlameMantleFrames;
    private static Sprite[] cachedSparkCrownFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticGreenAuraCache()
    {
        cachedGroundRuneSprite = null;
        cachedFlameMantleFrames = null;
        cachedSparkCrownFrames = null;
    }

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        animSeed = Random.Range(0f, 10f);
        EnsureVisuals();
    }

    public void SetAuraActive(bool active)
    {
        EnsureVisuals();
        if (auraRootObj != null)
        {
            auraRootObj.SetActive(active);
        }
    }

    private void OnEnable()
    {
        if (auraRootObj != null) auraRootObj.SetActive(true);
    }

    private void OnDisable()
    {
        if (auraRootObj != null) auraRootObj.SetActive(false);
    }

    private void EnsureVisuals()
    {
        EnsureAuraSprites();

        Transform existing = transform.Find("Aura_Verde_Maldita");
        if (existing != null)
        {
            auraRootObj = existing.gameObject;
            Transform rTr = auraRootObj.transform.Find("Anillo_Runico_Verde");
            if (rTr != null) groundRuneRenderer = rTr.GetComponent<SpriteRenderer>();
            Transform fTr = auraRootObj.transform.Find("Manto_Llamas_Verdes");
            if (fTr != null) flameMantleRenderer = fTr.GetComponent<SpriteRenderer>();
            Transform sTr = auraRootObj.transform.Find("Corona_Chispas_Verdes");
            if (sTr != null) sparkCrownRenderer = sTr.GetComponent<SpriteRenderer>();
            auraLight = auraRootObj.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            return;
        }

        auraRootObj = new GameObject("Aura_Verde_Maldita");
        auraRootObj.transform.SetParent(transform, false);
        auraRootObj.transform.localPosition = Vector3.zero;

        // 1. Anillo rúnico verde neón en los pies
        GameObject runeObj = new GameObject("Anillo_Runico_Verde");
        runeObj.transform.SetParent(auraRootObj.transform, false);
        runeObj.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        groundRuneRenderer = runeObj.AddComponent<SpriteRenderer>();
        groundRuneRenderer.sprite = cachedGroundRuneSprite;
        groundRuneRenderer.sortingLayerName = "Layer 1";
        groundRuneRenderer.sortingOrder = 1;
        groundRuneRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // 2. Manto de llamas verdes espectrales detrás del cuerpo
        GameObject flameObj = new GameObject("Manto_Llamas_Verdes");
        flameObj.transform.SetParent(auraRootObj.transform, false);
        flameObj.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        flameMantleRenderer = flameObj.AddComponent<SpriteRenderer>();
        flameMantleRenderer.sprite = cachedFlameMantleFrames[0];
        flameMantleRenderer.sortingLayerName = "Layer 1";
        flameMantleRenderer.sortingOrder = 1;
        flameMantleRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // 3. Corona de chispas y volutas verdes sobre hombros y cabeza
        GameObject sparkObj = new GameObject("Corona_Chispas_Verdes");
        sparkObj.transform.SetParent(auraRootObj.transform, false);
        sparkObj.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        sparkCrownRenderer = sparkObj.AddComponent<SpriteRenderer>();
        sparkCrownRenderer.sprite = cachedSparkCrownFrames[0];
        sparkCrownRenderer.sortingLayerName = "Layer 1";
        sparkCrownRenderer.sortingOrder = 5;
        sparkCrownRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // 4. Luz puntual esmeralda para que destaque claramente en el panteón
        auraLight = auraRootObj.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        auraLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        auraLight.color = new Color(0.12f, 1.0f, 0.42f, 1f);
        auraLight.intensity = 0.85f;
        auraLight.pointLightOuterRadius = 1.45f;
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            if (auraRootObj != null && auraRootObj.activeSelf)
                auraRootObj.SetActive(false);
            return;
        }

        EnsureAuraSprites();
        float t = Time.unscaledTime + animSeed;
        int frame = Mathf.FloorToInt(t * 9.5f) % 3;

        if (flameMantleRenderer != null && cachedFlameMantleFrames != null && cachedFlameMantleFrames.Length >= 3)
        {
            flameMantleRenderer.sprite = cachedFlameMantleFrames[frame];
            float flameScaleX = 1.02f + Mathf.Sin(t * 7.5f) * 0.07f;
            float flameScaleY = 1.04f + Mathf.Cos(t * 9.0f) * 0.08f;
            flameMantleRenderer.transform.localScale = new Vector3(flameScaleX, flameScaleY, 1f);
            float alpha = 0.86f + Mathf.Sin(t * 8f) * 0.12f;
            flameMantleRenderer.color = new Color(1f, 1f, 1f, alpha);
        }

        if (sparkCrownRenderer != null && cachedSparkCrownFrames != null && cachedSparkCrownFrames.Length >= 3)
        {
            sparkCrownRenderer.sprite = cachedSparkCrownFrames[(frame + 1) % 3];
            float yBob = 0.06f + Mathf.Sin(t * 6f) * 0.03f;
            sparkCrownRenderer.transform.localPosition = new Vector3(0f, yBob, 0f);
        }

        if (groundRuneRenderer != null)
        {
            float ringPulse = 1.0f + Mathf.Sin(t * 5.5f) * 0.10f;
            groundRuneRenderer.transform.localScale = new Vector3(ringPulse, ringPulse, 1f);
            float ringAlpha = 0.82f + Mathf.Sin(t * 6.5f) * 0.15f;
            groundRuneRenderer.color = new Color(0.30f, 1.0f, 0.55f, ringAlpha);
        }

        if (auraLight != null)
        {
            auraLight.intensity = 0.80f + Mathf.Sin(t * 7f) * 0.18f;
        }
    }

    private static void EnsureAuraSprites()
    {
        if (cachedGroundRuneSprite == null)
        {
            cachedGroundRuneSprite = CreateGroundRuneSprite();
        }
        if (cachedFlameMantleFrames == null || cachedFlameMantleFrames.Length < 3 || cachedFlameMantleFrames[0] == null)
        {
            cachedFlameMantleFrames = new Sprite[3];
            cachedSparkCrownFrames = new Sprite[3];
            for (int f = 0; f < 3; f++)
            {
                cachedFlameMantleFrames[f] = CreateFlameMantleSprite(f);
                cachedSparkCrownFrames[f] = CreateSparkCrownSprite(f);
            }
        }
    }

    private static Sprite CreateGroundRuneSprite()
    {
        int w = 36;
        int h = 18;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Vector2 center = new Vector2(17.5f, 8.5f);
        Color darkGreen = new Color(0.04f, 0.45f, 0.18f, 0.65f);
        Color midEmerald = new Color(0.10f, 0.92f, 0.42f, 0.92f);
        Color neonMint = new Color(0.68f, 1.0f, 0.82f, 1.0f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x - center.x) / 16.0f;
                float ny = (y - center.y) / 7.2f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);

                if (d >= 0.82f && d <= 1.0f)
                {
                    tex.SetPixel(x, y, midEmerald);
                }
                else if (d >= 0.68f && d < 0.82f)
                {
                    float angle = Mathf.Atan2(ny, nx);
                    bool isRuneNode = Mathf.Abs(Mathf.Sin(angle * 4f)) > 0.78f;
                    tex.SetPixel(x, y, isRuneNode ? neonMint : darkGreen);
                }
                else if (d < 0.68f)
                {
                    tex.SetPixel(x, y, new Color(0.06f, 0.68f, 0.28f, (1f - d) * 0.38f));
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Sprite CreateFlameMantleSprite(int frame)
    {
        int w = 40;
        int h = 44;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        float phase = frame * 2.094f;
        float cx = 19.5f;

        Color outerGlow = new Color(0.05f, 0.65f, 0.25f, 0.52f);
        Color midFlame = new Color(0.14f, 0.96f, 0.44f, 0.88f);
        Color brightCore = new Color(0.72f, 1.0f, 0.84f, 0.96f);

        for (int y = 2; y < h - 2; y++)
        {
            float ny = (y - 4f) / 36f; // 0 en los pies, 1 sobre la cabeza
            if (ny < 0f || ny > 1.05f) continue;

            // Silueta de flama envolvente más ancha en hombros/torso y terminando en lenguas de fuego arriba
            float baseHalfWidth = Mathf.Sin(Mathf.Clamp01(ny) * Mathf.PI) * 14.5f + (1f - ny) * 4.5f;
            float wave = Mathf.Sin(ny * 10f - phase) * 1.8f;

            for (int x = 2; x < w - 2; x++)
            {
                float dx = x - (cx + wave * 0.35f);
                float adx = Mathf.Abs(dx);

                // Lenguas laterales de fuego verde
                float tongueBoost = Mathf.Sin(dx * 0.65f + phase + ny * 6f) * 2.2f * ny;
                float limit = baseHalfWidth + tongueBoost;

                if (adx <= limit)
                {
                    float edgeFactor = adx / Mathf.Max(1f, limit);
                    // Hueco interior parcial para que el halo rodee claramente al duende
                    if (edgeFactor >= 0.78f)
                    {
                        tex.SetPixel(x, y, outerGlow);
                    }
                    else if (edgeFactor >= 0.48f)
                    {
                        tex.SetPixel(x, y, midFlame);
                    }
                    else if (ny > 0.68f && edgeFactor >= 0.25f)
                    {
                        tex.SetPixel(x, y, brightCore);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0.10f, 0.85f, 0.38f, 0.28f));
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.08f), 32f);
    }

    private static Sprite CreateSparkCrownSprite(int frame)
    {
        int w = 40;
        int h = 44;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color neonSpark = new Color(0.75f, 1.0f, 0.85f, 1.0f);
        Color emeraldRim = new Color(0.12f, 0.98f, 0.46f, 0.95f);

        // Chispas ascendentes alrededor del cuerpo y sobre la cabeza para máxima visibilidad
        int[,] baseSparks = new int[,]
        {
            { 8, 10 }, { 31, 12 }, { 6, 20 }, { 33, 22 },
            { 10, 30 }, { 29, 31 }, { 15, 36 }, { 24, 37 }, { 20, 39 }
        };

        int yShift = frame * 2;
        for (int i = 0; i < baseSparks.GetLength(0); i++)
        {
            int sx = baseSparks[i, 0] + ((i % 2 == 0) ? (frame - 1) : (1 - frame));
            int sy = 6 + ((baseSparks[i, 1] - 6 + yShift * (1 + (i % 2))) % 34);

            if (sx >= 2 && sx < w - 2 && sy >= 2 && sy < h - 2)
            {
                tex.SetPixel(sx, sy, neonSpark);
                tex.SetPixel(sx, sy + 1, emeraldRim);
                tex.SetPixel(sx, sy - 1, emeraldRim);
                tex.SetPixel(sx - 1, sy, emeraldRim);
                tex.SetPixel(sx + 1, sy, emeraldRim);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.08f), 32f);
    }
}
