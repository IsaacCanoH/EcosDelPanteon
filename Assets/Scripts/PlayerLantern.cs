using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(60)]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerLantern : MonoBehaviour
{
    public IrisInNivel opening;
    public SpriteRenderer lanternVisual;
    public SpriteRenderer flashVisual;
    public SpriteRenderer animaVisual;
    public Sprite[] animaFrames;
    public GoblinNavigation2D navigation;
    [Min(0.1f)] public float range = 2.15f;
    [Range(20f, 180f)] public float angle = 110f;
    [Min(0.1f)] public float cooldown = 1f;
    [SerializeField] private bool dispelArrows;

    [Header("Mejora del Pilar de Luz (Nivel 3 - Ataque en Radio 360°)")]
    public bool isRadialUpgraded = false;
    public float radialRange = 2.85f;
    public int radialDamage = 34;

    private UnityEngine.Rendering.SortingGroup visualGroup;
    private SpriteRenderer playerVisual;
    private float nextAttackAt;
    private float flashUntil;
    private float animaUntil;
    private float radialAttackUntil;
    private PlayerHealth health;
    private GoldCoinCollector collector;
    private PlayerCoinSearch search;
    private TopDownCharacterController movement;
    private Level2CharacterController level2Movement;
    private PlayerWeaponController weaponController;
    private PlayerStoneThrow facing;
    private Collider2D body;
    private Vector2 flashDirection;
    private Vector2 flashOrigin;
    private readonly HashSet<GameObject> hitObjects = new HashSet<GameObject>();

    // Elementos visuales de la animación de ataque en radio 360°
    private GameObject radialEffectRoot;
    private SpriteRenderer outerShockwaveRingSr;
    private SpriteRenderer innerRuneRingSr;
    private readonly List<SpriteRenderer> orbitingFlameRenderers = new List<SpriteRenderer>();
    private Light2D radialFlashLight;
    private static Sprite[] radialShockwaveFrames;
    private static Sprite[] radialRuneRingFrames;
    private static Sprite[] radialFlameFrames;

    public float CooldownRemaining { get { return Mathf.Max(0f, nextAttackAt - Time.time); } }

    private void Awake()
    {
        if (range < 2.15f) range = 2.15f;
        visualGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        playerVisual = GetComponent<SpriteRenderer>();
        health = GetComponent<PlayerHealth>();
        collector = GetComponent<GoldCoinCollector>();
        search = GetComponent<PlayerCoinSearch>();
        movement = GetComponent<TopDownCharacterController>();
        level2Movement = GetComponent<Level2CharacterController>();
        weaponController = GetComponent<PlayerWeaponController>();
        facing = GetComponent<PlayerStoneThrow>();
        body = GetComponent<Collider2D>();
        if (flashVisual != null) flashVisual.enabled = false;
        if (animaVisual != null) animaVisual.enabled = false;
    }

    private void Start()
    {
        if (isRadialUpgraded)
            EnableRadialUpgrade(radialDamage, radialRange);
    }

    public void EnableRadialUpgrade(int newDamage = 34, float newRadialRange = 2.85f)
    {
        isRadialUpgraded = true;
        radialDamage = newDamage > 0 ? newDamage : 34;
        radialRange = Mathf.Clamp(newRadialRange, 2.5f, 3.1f);
        range = radialRange;

        if (weaponController == null) weaponController = GetComponent<PlayerWeaponController>();
        if (weaponController != null)
        {
            weaponController.UpgradeLanternToRadial(radialDamage, radialRange);
        }

        EnsureRadialAttackVisuals();
        if (lanternVisual != null)
        {
            lanternVisual.color = new Color(0.55f, 0.94f, 1.0f, 1.0f);
        }
    }

    public void TriggerEmpowerPreviewWave()
    {
        EnsureRadialAttackVisuals();
        flashOrigin = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        radialAttackUntil = Time.time + 0.52f;
    }

    private void Update()
    {
        if (weaponController != null && weaponController.isActiveAndEnabled &&
            weaponController.currentWeapon != WeaponType.Lantern)
            return;

        if (GameControls.LanternPressed) TryAttack();
    }

    public bool TryAttack()
    {
        bool canMove = (movement != null && movement.enabled) ||
                       (level2Movement != null && level2Movement.enabled) ||
                       (movement == null && level2Movement == null);
        bool levelFinished = collector != null && collector.isActiveAndEnabled && collector.IsLevelFinished;

        if (!isActiveAndEnabled || Time.timeScale <= 0f || Time.time < nextAttackAt ||
            health == null || health.currentHealth <= 0 || levelFinished ||
            (opening != null && opening.isActiveAndEnabled) ||
            (search != null && search.isActiveAndEnabled && search.IsSearching) ||
            !canMove) return false;

        Vector2 origin = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        Vector2 direction = facing != null ? facing.FacingDirection : Vector2.down;
        nextAttackAt = Time.time + cooldown;
        flashUntil = Time.time + (isRadialUpgraded ? 0.30f : 0.2f);
        animaUntil = Time.time + 0.45f;
        if (isRadialUpgraded)
        {
            EnsureRadialAttackVisuals();
            radialAttackUntil = Time.time + 0.45f;
        }
        flashOrigin = origin;
        flashDirection = direction;
        hitObjects.Clear();

        float effectiveRange = isRadialUpgraded ? radialRange : range;
        int damage = isRadialUpgraded
            ? radialDamage
            : (weaponController != null ? Mathf.Max(17, weaponController.lanternDamage) : 17);

        foreach (var collider in Physics2D.OverlapCircleAll(origin, effectiveRange))
        {
            if (collider == null || collider.transform.IsChildOf(transform)) continue;

            if (dispelArrows && collider.TryGetComponent(out GoblinArrowProjectile arrow))
            {
                TryDispelArrow(arrow);
                continue;
            }

            if (dispelArrows && collider.TryGetComponent(out SpectralProjectile orb) && orb.CanBeDispelledByLantern)
            {
                TryDispelSpectralProjectile(orb);
                continue;
            }

            GoblinCombat target = collider.GetComponentInParent<GoblinCombat>();
            EnemyHealth enemyHealth = collider.GetComponentInParent<EnemyHealth>();
            GoblinAI goblinAI = collider.GetComponentInParent<GoblinAI>();
            IDamageable damageable = collider.GetComponentInParent<IDamageable>();

            if (collider.isTrigger && target == null && enemyHealth == null && damageable == null)
                continue;

            GameObject rootObj = enemyHealth != null ? enemyHealth.gameObject :
                                 target != null ? target.gameObject :
                                 goblinAI != null ? goblinAI.gameObject :
                                 (damageable as MonoBehaviour)?.gameObject;

            if (rootObj == null || !hitObjects.Add(rootObj)) continue;

            Vector2 delta = (Vector2)collider.bounds.center - origin;
            Vector2 hitDir = delta.sqrMagnitude > 0.001f ? delta.normalized : direction;

            // Si aún NO tiene el poder del Pilar de Luz, sólo ataca hacia el frente (cono direccional).
            // Cuando YA obtuvo el poder del Pilar de Luz (isRadialUpgraded), ataca en un radio de 360° alrededor del jugador.
            if (!isRadialUpgraded)
            {
                if (delta.sqrMagnitude > 0.01f &&
                    Vector2.Dot(direction, hitDir) < Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad))
                    continue;
            }

            if (navigation != null && !navigation.HasLineOfSight(origin, collider.bounds.center))
                continue;

            if (enemyHealth != null)
            {
                if (!enemyHealth.IsDead)
                {
                    enemyHealth.TakeDamage(damage, hitDir);
                    if (goblinAI != null) goblinAI.Alert();
                }
            }
            else if (target != null)
            {
                if (!target.IsBanished)
                    target.TakeLanternHit();
            }
            else if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, hitDir);
            }
        }
        return true;
    }

    internal bool TryDispelArrow(GoblinArrowProjectile arrow)
    {
        return arrow != null && CanDispelProjectileAt(arrow.transform.position) && arrow.Dispel();
    }

    internal bool TryDispelSpectralProjectile(SpectralProjectile orb)
    {
        return orb != null && orb.CanBeDispelledByLantern &&
               CanDispelProjectileAt(orb.transform.position) && orb.Dispel();
    }

    private bool CanDispelProjectileAt(Vector2 position)
    {
        if (!dispelArrows || !isActiveAndEnabled || Time.timeScale <= 0f || Time.time >= flashUntil)
            return false;

        Vector2 delta = position - flashOrigin;
        float effectiveRange = isRadialUpgraded ? radialRange : range;
        if (delta.sqrMagnitude > effectiveRange * effectiveRange)
            return false;

        if (!isRadialUpgraded && delta.sqrMagnitude > 0.01f &&
            Vector2.Dot(flashDirection, delta.normalized) < Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad))
            return false;

        return navigation == null || navigation.HasLineOfSight(flashOrigin, position);
    }

    private void LateUpdate()
    {
        if (visualGroup == null) visualGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (visualGroup == null) visualGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        if (visualGroup != null && playerVisual != null)
        {
            visualGroup.sortingLayerID = playerVisual.sortingLayerID;
            visualGroup.sortingOrder = playerVisual.sortingOrder;
        }
        Vector2 direction = facing != null ? facing.FacingDirection : Vector2.down;
        if (lanternVisual != null)
        {
            lanternVisual.transform.localPosition = new Vector3(direction.x == 0f ? 0.38f : direction.x * 0.32f,
                0.42f + direction.y * 0.08f, 0f);
            if (playerVisual != null)
            {
                lanternVisual.sortingLayerID = playerVisual.sortingLayerID;
            }
            lanternVisual.sortingOrder = direction.y > 0f ? 1 : 3;
            bool showLantern = health != null && health.currentHealth > 0 &&
                               (weaponController == null || !weaponController.isActiveAndEnabled || weaponController.currentWeapon == WeaponType.Lantern);
            lanternVisual.enabled = showLantern;
            if (isRadialUpgraded && showLantern)
            {
                float pulse = 0.85f + Mathf.Sin(Time.time * 6f) * 0.15f;
                lanternVisual.color = new Color(0.55f * pulse + 0.25f, 0.92f, 1.0f, 1.0f);
            }
        }
        UpdateAnima();
        UpdateRadialAttackVisuals();

        if (flashVisual == null) return;
        flashVisual.enabled = !isRadialUpgraded && Time.time < flashUntil && health != null && health.currentHealth > 0;
        if (!flashVisual.enabled) return;
        flashVisual.transform.position = new Vector3(flashOrigin.x, flashOrigin.y + 0.3f, transform.position.z);
        flashVisual.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flashDirection.y, flashDirection.x) * Mathf.Rad2Deg);
        if (flashVisual.sprite != null)
        {
            Transform visual = flashVisual.transform;
            Vector3 horizontal = visual.localRotation * Vector3.right;
            Vector3 vertical = visual.localRotation * Vector3.up;
            if (visual.parent != null)
            {
                horizontal = visual.parent.TransformVector(horizontal);
                vertical = visual.parent.TransformVector(vertical);
            }
            float size = range / Mathf.Max(0.01f, flashVisual.sprite.bounds.max.x);
            visual.localScale = new Vector3(size / Mathf.Max(0.01f, horizontal.magnitude),
                size / Mathf.Max(0.01f, vertical.magnitude), 1f);
        }
        flashVisual.color = new Color(1f, 1f, 1f, Mathf.Clamp01((flashUntil - Time.time) / 0.2f));
    }

    private void UpdateAnima()
    {
        if (animaVisual == null) return;
        float remaining = animaUntil - Time.time;
        animaVisual.enabled = !isRadialUpgraded && remaining > 0f && health != null && health.currentHealth > 0;
        if (!animaVisual.enabled) return;
        float t = Mathf.Clamp01(1f - remaining / 0.45f);
        float travel = Mathf.Clamp01(t * 1.5f);
        Vector2 position = flashOrigin + flashDirection * Mathf.Lerp(0.15f, range, travel);
        position.y += 0.32f + Mathf.Sin(t * Mathf.PI) * 0.08f;
        animaVisual.transform.position = new Vector3(position.x, position.y, transform.position.z);
        animaVisual.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 3f);
        animaVisual.transform.localScale = Vector3.one * (0.85f + Mathf.Sin(t * Mathf.PI) * 0.15f);
        animaVisual.color = new Color(1f, 1f, 1f, Mathf.Clamp01(remaining / 0.15f));
        if (animaFrames != null && animaFrames.Length > 0)
            animaVisual.sprite = animaFrames[Mathf.FloorToInt(t * 6f) % animaFrames.Length];
    }

    private void EnsureRadialAttackVisuals()
    {
        EnsureRadialSpritesGenerated();

        if (radialEffectRoot != null) return;

        radialEffectRoot = new GameObject("Farol_RadialAttack_FX");
        radialEffectRoot.transform.SetParent(null);
        radialEffectRoot.transform.position = transform.position;

        GameObject outerObj = new GameObject("OuterShockwaveRing");
        outerObj.transform.SetParent(radialEffectRoot.transform, false);
        outerShockwaveRingSr = outerObj.AddComponent<SpriteRenderer>();
        outerShockwaveRingSr.sprite = radialShockwaveFrames[0];
        outerShockwaveRingSr.sortingLayerName = "Layer 1";
        outerShockwaveRingSr.sortingOrder = 5;
        outerShockwaveRingSr.enabled = false;

        GameObject innerObj = new GameObject("InnerRuneRing");
        innerObj.transform.SetParent(radialEffectRoot.transform, false);
        innerRuneRingSr = innerObj.AddComponent<SpriteRenderer>();
        innerRuneRingSr.sprite = radialRuneRingFrames[0];
        innerRuneRingSr.sortingLayerName = "Layer 1";
        innerRuneRingSr.sortingOrder = 4;
        innerRuneRingSr.enabled = false;

        orbitingFlameRenderers.Clear();
        for (int i = 0; i < 8; i++)
        {
            GameObject flameObj = new GameObject($"RadialSoulFlame_{i}");
            flameObj.transform.SetParent(radialEffectRoot.transform, false);
            SpriteRenderer fSr = flameObj.AddComponent<SpriteRenderer>();
            fSr.sprite = radialFlameFrames[i % radialFlameFrames.Length];
            fSr.sortingLayerName = "Layer 1";
            fSr.sortingOrder = 6;
            fSr.enabled = false;
            orbitingFlameRenderers.Add(fSr);
        }

        GameObject lightObj = new GameObject("RadialBurstLight2D");
        lightObj.transform.SetParent(radialEffectRoot.transform, false);
        radialFlashLight = lightObj.AddComponent<Light2D>();
        radialFlashLight.lightType = Light2D.LightType.Point;
        radialFlashLight.color = new Color(0.15f, 0.85f, 1.0f, 1.0f);
        radialFlashLight.pointLightInnerRadius = 0.4f;
        radialFlashLight.pointLightOuterRadius = radialRange * 1.15f;
        radialFlashLight.intensity = 0f;
        radialFlashLight.enabled = false;
    }

    private void UpdateRadialAttackVisuals()
    {
        if (radialEffectRoot == null) return;

        float remaining = radialAttackUntil - Time.time;
        bool active = remaining > 0f && health != null && health.currentHealth > 0;

        if (!active)
        {
            if (outerShockwaveRingSr != null && outerShockwaveRingSr.enabled) outerShockwaveRingSr.enabled = false;
            if (innerRuneRingSr != null && innerRuneRingSr.enabled) innerRuneRingSr.enabled = false;
            for (int i = 0; i < orbitingFlameRenderers.Count; i++)
            {
                if (orbitingFlameRenderers[i] != null && orbitingFlameRenderers[i].enabled)
                    orbitingFlameRenderers[i].enabled = false;
            }
            if (radialFlashLight != null && radialFlashLight.enabled) radialFlashLight.enabled = false;
            return;
        }

        float duration = 0.45f;
        float t = Mathf.Clamp01(1f - remaining / duration);
        float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
        float fade = t < 0.7f ? 1f : Mathf.Clamp01((1f - t) / 0.3f);

        Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position + new Vector2(0f, 0.25f);
        radialEffectRoot.transform.position = new Vector3(center.x, center.y, 0f);

        // El sprite base mide 96x96 a 32 PPU => radio base = 1.5 unidades de mundo.
        float targetScale = (radialRange / 1.45f) * Mathf.Lerp(0.18f, 1.04f, easeOut);
        int frameIdx = Mathf.FloorToInt(t * 10f) % radialShockwaveFrames.Length;

        if (outerShockwaveRingSr != null)
        {
            outerShockwaveRingSr.enabled = true;
            outerShockwaveRingSr.sprite = radialShockwaveFrames[frameIdx];
            outerShockwaveRingSr.transform.localScale = new Vector3(targetScale, targetScale, 1f);
            outerShockwaveRingSr.transform.localRotation = Quaternion.Euler(0f, 0f, t * 140f);
            outerShockwaveRingSr.color = new Color(1f, 1f, 1f, fade * 0.95f);
        }

        if (innerRuneRingSr != null)
        {
            innerRuneRingSr.enabled = true;
            int runeFrame = Mathf.FloorToInt(t * 8f) % radialRuneRingFrames.Length;
            innerRuneRingSr.sprite = radialRuneRingFrames[runeFrame];
            float innerScale = (radialRange / 1.45f) * Mathf.Lerp(0.12f, 0.86f, Mathf.Clamp01(t * 1.25f));
            innerRuneRingSr.transform.localScale = new Vector3(innerScale, innerScale, 1f);
            innerRuneRingSr.transform.localRotation = Quaternion.Euler(0f, 0f, -t * 180f);
            innerRuneRingSr.color = new Color(0.45f, 0.95f, 1f, fade * 0.85f);
        }

        // 8 Llamas de Ánimas Azules-Doradas que barren los 360 grados alrededor del jugador
        float currentRadius = Mathf.Lerp(0.28f, radialRange * 0.96f, easeOut);
        for (int i = 0; i < orbitingFlameRenderers.Count; i++)
        {
            SpriteRenderer fSr = orbitingFlameRenderers[i];
            if (fSr == null) continue;
            fSr.enabled = true;
            fSr.sprite = radialFlameFrames[(frameIdx + i) % radialFlameFrames.Length];

            float baseAngle = i * (Mathf.PI * 2f / orbitingFlameRenderers.Count) + t * 2.4f;
            Vector3 localPos = new Vector3(Mathf.Cos(baseAngle) * currentRadius, Mathf.Sin(baseAngle) * currentRadius, 0f);
            fSr.transform.localPosition = localPos;
            fSr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 16f + i) * 14f);
            float fScale = 0.85f + Mathf.Sin(t * Mathf.PI) * 0.35f;
            fSr.transform.localScale = new Vector3(fScale, fScale, 1f);
            fSr.color = new Color(1f, 1f, 1f, fade);
        }

        if (radialFlashLight != null)
        {
            radialFlashLight.enabled = true;
            radialFlashLight.pointLightOuterRadius = radialRange * 1.25f;
            radialFlashLight.intensity = Mathf.Sin(t * Mathf.PI) * 2.2f;
        }
    }

    private static void EnsureRadialSpritesGenerated()
    {
        if (radialShockwaveFrames != null && radialShockwaveFrames.Length == 4 && radialShockwaveFrames[0] != null)
            return;

        radialShockwaveFrames = new Sprite[4];
        for (int f = 0; f < 4; f++)
        {
            radialShockwaveFrames[f] = CreateRadialShockwaveSprite(f);
        }

        radialRuneRingFrames = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            radialRuneRingFrames[f] = CreateRadialRuneRingSprite(f);
        }

        radialFlameFrames = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            radialFlameFrames[f] = CreateRadialFlameSprite(f);
        }
    }

    private static Sprite CreateRadialShockwaveSprite(int frame)
    {
        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.46f;
        float phase = frame * (Mathf.PI * 0.5f);

        Color deepBlue = new Color(0.05f, 0.48f, 0.95f, 0.72f);
        Color brightCyan = new Color(0.18f, 0.92f, 1.0f, 0.95f);
        Color goldenCore = new Color(1.0f, 0.94f, 0.55f, 0.98f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x, y) - center;
                float dist = diff.magnitude;
                float angle = Mathf.Atan2(diff.y, diff.x);

                // Ondulación de 8 puntas de fuego arcano alrededor del círculo de 360°
                float wave = Mathf.Sin(angle * 8f + phase) * 2.2f + Mathf.Cos(angle * 16f - phase) * 1.1f;
                float effectiveDist = dist + wave;
                float normR = effectiveDist / maxR;

                if (normR < 0.62f || normR > 1.04f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else if (normR >= 0.86f && normR <= 0.95f)
                {
                    tex.SetPixel(x, y, goldenCore);
                }
                else if (normR >= 0.76f && normR <= 1.00f)
                {
                    tex.SetPixel(x, y, brightCyan);
                }
                else
                {
                    tex.SetPixel(x, y, deepBlue);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Sprite CreateRadialRuneRingSprite(int frame)
    {
        int size = 80;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.45f;
        Color runeCyan = new Color(0.12f, 0.90f, 1.0f, 0.92f);
        Color softCyan = new Color(0.08f, 0.65f, 0.98f, 0.45f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x, y) - center;
                float r = diff.magnitude / maxR;
                float angle = Mathf.Atan2(diff.y, diff.x);
                int sector = Mathf.Abs(Mathf.FloorToInt((angle + Mathf.PI) / (Mathf.PI / 8f)) + frame) % 16;

                if (Mathf.Abs(r - 0.88f) < 0.04f || Mathf.Abs(r - 0.68f) < 0.035f)
                {
                    tex.SetPixel(x, y, runeCyan);
                }
                else if (r > 0.70f && r < 0.86f && sector % 2 == 0 && ((x + y + frame) % 3 == 0))
                {
                    tex.SetPixel(x, y, runeCyan);
                }
                else if (r > 0.45f && r < 0.88f && ((x + y) % 4 == 0))
                {
                    tex.SetPixel(x, y, softCyan);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Sprite CreateRadialFlameSprite(int frame)
    {
        int w = 18;
        int h = 22;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color outerCyan = new Color(0.08f, 0.78f, 1.0f, 0.92f);
        Color midAzure = new Color(0.35f, 0.96f, 1.0f, 0.98f);
        Color innerGold = new Color(1.0f, 0.96f, 0.68f, 1.0f);

        int cx = w / 2;
        for (int y = 2; y <= 18; y++)
        {
            float t = (y - 2f) / 16f;
            float halfW = Mathf.Sin(t * Mathf.PI) * 5.2f * (1f - t * 0.35f);
            float sway = Mathf.Sin(t * 4f + frame * 2.1f) * 1.1f;

            int minX = Mathf.RoundToInt(cx + sway - halfW);
            int maxX = Mathf.RoundToInt(cx + sway + halfW);
            for (int x = minX; x <= maxX; x++)
            {
                if (x < 1 || x >= w - 1) continue;
                float d = Mathf.Abs(x - (cx + sway)) / Mathf.Max(1f, halfW);
                tex.SetPixel(x, y, d < 0.35f ? innerGold : (d < 0.72f ? midAzure : outerCyan));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    private void OnDisable()
    {
        if (flashVisual != null) flashVisual.enabled = false;
        if (animaVisual != null) animaVisual.enabled = false;
        if (radialEffectRoot != null)
        {
            if (outerShockwaveRingSr != null) outerShockwaveRingSr.enabled = false;
            if (innerRuneRingSr != null) innerRuneRingSr.enabled = false;
            for (int i = 0; i < orbitingFlameRenderers.Count; i++)
            {
                if (orbitingFlameRenderers[i] != null) orbitingFlameRenderers[i].enabled = false;
            }
            if (radialFlashLight != null) radialFlashLight.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (radialEffectRoot != null)
        {
            Destroy(radialEffectRoot);
        }
    }
}

/// <summary>
/// Controla el Pilar de Luz (Rune Pillar) ubicado en la construcción junto al primer altar del Nivel 3:
/// - Permanece apagado al inicio del nivel.
/// - Al terminar la primera oleada/encuentro del primer altar, se enciende de color azul brillante.
/// - Cuando el jugador se acerca al pilar encendido, cambia automáticamente su arma al Farol,
///   reproduce una animación imbuyendo el Farol con poder mágico azul, y mejora el Farol para atacar
///   en un radio de 360° alrededor del jugador con el mismo daño que el Cuchillo (28 DMG).
/// </summary>
public class RunePillarLanternUpgrade : MonoBehaviour
{
    public static RunePillarLanternUpgrade Instance { get; private set; }

    public bool IsActivated => isActivated;
    public bool IsUpgradeGranted => isUpgradeGranted;

    private bool isActivated = false;
    private bool isUpgradeGranted = false;
    private bool isAnimatingEmpower = false;

    private SpriteRenderer pillarSr;
    private SpriteRenderer glowSr;
    private MonoBehaviour glowColorAnim;
    private SpriteRenderer baseRuneRingSr;
    private SpriteRenderer verticalBeamSr;
    private Light2D pillarBlueLight;
    private readonly List<Transform> floatingOrbs = new List<Transform>();
    private readonly List<SpriteRenderer> floatingOrbRenderers = new List<SpriteRenderer>();

    private static Sprite softOrbSprite;
    private static Sprite runeFloorCircleSprite;
    private static Sprite beamSprite;

    private void Awake()
    {
        Instance = this;
        SetupPillarVisuals();
        SetPillarLitState(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void SetupPillarVisuals()
    {
        pillarSr = GetComponent<SpriteRenderer>();
        Transform glowTr = transform.Find("Glow");
        if (glowTr != null)
        {
            glowSr = glowTr.GetComponent<SpriteRenderer>();
            if (glowSr != null)
            {
                glowSr.sortingLayerName = "Layer 1";
                glowSr.sortingOrder = 3;
            }
            foreach (var mb in glowTr.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.Contains("SpriteColorAnimation"))
                {
                    glowColorAnim = mb;
                    break;
                }
            }
        }

        // Círculo rúnico azul en la base del pilar
        GameObject ringObj = new GameObject("Pilar_Circulo_Azul");
        ringObj.transform.SetParent(transform, false);
        ringObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        baseRuneRingSr = ringObj.AddComponent<SpriteRenderer>();
        baseRuneRingSr.sprite = GetOrCreateRuneFloorCircleSprite();
        baseRuneRingSr.sortingLayerName = "Layer 1";
        baseRuneRingSr.sortingOrder = 1;
        baseRuneRingSr.color = new Color(0.08f, 0.82f, 1.0f, 0.85f);

        // Haz vertical de luz azul mágica
        GameObject beamObj = new GameObject("Pilar_Haz_Luz_Azul");
        beamObj.transform.SetParent(transform, false);
        beamObj.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        verticalBeamSr = beamObj.AddComponent<SpriteRenderer>();
        verticalBeamSr.sprite = GetOrCreateBeamSprite();
        verticalBeamSr.sortingLayerName = "Layer 1";
        verticalBeamSr.sortingOrder = 3;
        verticalBeamSr.color = new Color(0.12f, 0.86f, 1.0f, 0.42f);

        // Partículas de energía azul ascendentes alrededor del pilar
        floatingOrbs.Clear();
        floatingOrbRenderers.Clear();
        for (int i = 0; i < 6; i++)
        {
            GameObject orb = new GameObject($"Pilar_Orbe_Azul_{i}");
            orb.transform.SetParent(transform, false);
            SpriteRenderer oSr = orb.AddComponent<SpriteRenderer>();
            oSr.sprite = GetOrCreateSoftOrbSprite();
            oSr.sortingLayerName = "Layer 1";
            oSr.sortingOrder = 4;
            oSr.color = new Color(0.20f, 0.92f, 1.0f, 0.9f);
            orb.transform.localScale = Vector3.one * 0.32f;
            floatingOrbs.Add(orb.transform);
            floatingOrbRenderers.Add(oSr);
        }

        // Luz 2D azul intensa
        GameObject lightObj = new GameObject("Pilar_Luz_Azul_2D");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        pillarBlueLight = lightObj.AddComponent<Light2D>();
        pillarBlueLight.lightType = Light2D.LightType.Point;
        pillarBlueLight.color = new Color(0.08f, 0.78f, 1.0f, 1.0f);
        pillarBlueLight.pointLightInnerRadius = 0.5f;
        pillarBlueLight.pointLightOuterRadius = 3.8f;
        pillarBlueLight.intensity = 1.95f;
    }

    private void SetPillarLitState(bool lit)
    {
        if (glowSr != null)
        {
            glowSr.enabled = lit;
            if (lit) glowSr.color = new Color(0.05f, 0.88f, 1.0f, 1.0f);
        }
        if (glowColorAnim != null) glowColorAnim.enabled = lit;
        if (baseRuneRingSr != null) baseRuneRingSr.enabled = lit;
        if (verticalBeamSr != null) verticalBeamSr.enabled = lit;
        if (pillarBlueLight != null) pillarBlueLight.enabled = lit;

        for (int i = 0; i < floatingOrbRenderers.Count; i++)
        {
            if (floatingOrbRenderers[i] != null)
                floatingOrbRenderers[i].enabled = lit;
        }

        if (pillarSr != null)
        {
            pillarSr.color = lit
                ? new Color(0.78f, 0.92f, 1.0f, 1.0f)
                : new Color(0.52f, 0.56f, 0.64f, 1.0f);
        }
    }

    public void ActivatePillar()
    {
        if (isActivated) return;
        isActivated = true;
        SetPillarLitState(true);
    }

    private void Update()
    {
        if (!isActivated) return;

        float t = Time.time;

        if (baseRuneRingSr != null && baseRuneRingSr.enabled)
        {
            baseRuneRingSr.transform.localRotation = Quaternion.Euler(0f, 0f, t * 38f);
            float scalePulse = isUpgradeGranted ? 0.85f : (1.05f + Mathf.Sin(t * 4f) * 0.08f);
            baseRuneRingSr.transform.localScale = new Vector3(scalePulse, scalePulse * 0.65f, 1f);
            float alpha = isUpgradeGranted ? 0.38f : (0.75f + Mathf.Sin(t * 4.5f) * 0.2f);
            baseRuneRingSr.color = new Color(0.08f, 0.85f, 1.0f, alpha);
        }

        if (verticalBeamSr != null && verticalBeamSr.enabled)
        {
            float beamWidth = isUpgradeGranted ? 0.45f : (0.85f + Mathf.Sin(t * 5f) * 0.15f);
            verticalBeamSr.transform.localScale = new Vector3(beamWidth, 1.35f, 1f);
            verticalBeamSr.color = new Color(0.15f, 0.88f, 1.0f, isUpgradeGranted ? 0.16f : (0.38f + Mathf.Sin(t * 5f) * 0.12f));
        }

        if (pillarBlueLight != null && pillarBlueLight.enabled)
        {
            pillarBlueLight.intensity = isUpgradeGranted ? 0.95f : (1.85f + Mathf.Sin(t * 4.2f) * 0.35f);
        }

        for (int i = 0; i < floatingOrbs.Count; i++)
        {
            if (floatingOrbs[i] == null || floatingOrbRenderers[i] == null) continue;
            if (isUpgradeGranted)
            {
                floatingOrbRenderers[i].enabled = false;
                continue;
            }
            float cycle = (t * 0.65f + i * 0.18f) % 1f;
            float angle = t * 2.4f + i * (Mathf.PI * 2f / floatingOrbs.Count);
            float rx = Mathf.Cos(angle) * 0.52f;
            float ry = 0.15f + cycle * 1.45f;
            floatingOrbs[i].localPosition = new Vector3(rx, ry, 0f);
            float a = Mathf.Sin(cycle * Mathf.PI) * 0.92f;
            floatingOrbRenderers[i].color = new Color(0.25f, 0.94f, 1.0f, a);
        }

        if (isUpgradeGranted || isAnimatingEmpower) return;
        if (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float dist = Vector2.Distance(player.transform.position, transform.position);
        if (dist <= 2.15f)
        {
            StartCoroutine(EmpowerPlayerLanternRoutine(player));
        }
    }

    private IEnumerator EmpowerPlayerLanternRoutine(GameObject player)
    {
        isAnimatingEmpower = true;
        isUpgradeGranted = true;

        var topDownCtrl = player.GetComponent<TopDownCharacterController>();
        var l2Ctrl = player.GetComponent<Level2CharacterController>();
        var rb = player.GetComponent<Rigidbody2D>();
        var anim = player.GetComponent<Animator>();
        var weaponCtrl = player.GetComponent<PlayerWeaponController>();
        var playerLantern = player.GetComponent<PlayerLantern>();

        if (topDownCtrl != null) topDownCtrl.enabled = false;
        if (l2Ctrl != null) l2Ctrl.enabled = false;
        if (rb != null) rb.velocity = Vector2.zero;
        if (anim != null) anim.SetBool("IsMoving", false);

        // 1. Cambiar automáticamente el arma equipada al Farol
        if (weaponCtrl != null)
        {
            weaponCtrl.EquipWeapon(WeaponType.Lantern);
        }

        // 2. Crear partículas y rayos de energía azul que viajan desde el Pilar de Luz hacia el Farol del jugador
        List<GameObject> streamOrbs = new List<GameObject>();
        for (int i = 0; i < 8; i++)
        {
            GameObject sOrb = new GameObject($"EmpowerStreamOrb_{i}");
            SpriteRenderer sSr = sOrb.AddComponent<SpriteRenderer>();
            sSr.sprite = GetOrCreateSoftOrbSprite();
            sSr.sortingLayerName = "Layer 2";
            sSr.sortingOrder = 30;
            sSr.color = (i % 2 == 0) ? new Color(0.15f, 0.92f, 1.0f, 1f) : new Color(1.0f, 0.94f, 0.55f, 1f);
            sOrb.transform.localScale = Vector3.one * 0.45f;
            streamOrbs.Add(sOrb);
        }

        // Halo creciente alrededor del Farol del jugador
        GameObject lanternHaloObj = new GameObject("LanternEmpowerHalo");
        SpriteRenderer haloSr = lanternHaloObj.AddComponent<SpriteRenderer>();
        haloSr.sprite = GetOrCreateRuneFloorCircleSprite();
        haloSr.sortingLayerName = "Layer 2";
        haloSr.sortingOrder = 29;
        haloSr.color = new Color(0.15f, 0.90f, 1.0f, 0f);

        Vector3 pillarOrigin = transform.position + new Vector3(0f, 0.78f, 0f);
        float duration = 1.65f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / duration);

            Vector3 lanternTarget = (playerLantern != null && playerLantern.lanternVisual != null)
                ? playerLantern.lanternVisual.transform.position
                : (player.transform.position + new Vector3(0.32f, 0.42f, 0f));

            lanternHaloObj.transform.position = lanternTarget;
            lanternHaloObj.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 220f);
            float haloScale = Mathf.Lerp(0.25f, 1.15f, Mathf.Sin(p * Mathf.PI));
            lanternHaloObj.transform.localScale = new Vector3(haloScale, haloScale, 1f);
            haloSr.color = new Color(0.20f, 0.92f, 1.0f, Mathf.Sin(p * Mathf.PI) * 0.92f);

            for (int i = 0; i < streamOrbs.Count; i++)
            {
                float orbT = Mathf.Clamp01((p * 1.45f) - i * 0.06f);
                Vector3 arc = new Vector3(
                    Mathf.Sin(orbT * Mathf.PI + i) * 0.38f,
                    Mathf.Sin(orbT * Mathf.PI) * 0.65f,
                    0f);
                streamOrbs[i].transform.position = Vector3.Lerp(pillarOrigin, lanternTarget, orbT) + arc;
                float s = (0.32f + Mathf.Sin(orbT * Mathf.PI) * 0.30f);
                streamOrbs[i].transform.localScale = new Vector3(s, s, 1f);
            }

            if (pillarBlueLight != null)
            {
                pillarBlueLight.intensity = 2.2f + Mathf.Sin(p * Mathf.PI * 4f) * 1.1f;
            }

            yield return null;
        }

        for (int i = 0; i < streamOrbs.Count; i++)
        {
            if (streamOrbs[i] != null) Destroy(streamOrbs[i]);
        }
        if (lanternHaloObj != null) Destroy(lanternHaloObj);

        // 3. Activar la mejora del Farol: ataque en radio moderado de 360° (2.85m) con 34 de daño
        int upgradedDamage = 34;
        float upgradedRadius = 2.85f;
        if (playerLantern != null)
        {
            playerLantern.EnableRadialUpgrade(upgradedDamage, upgradedRadius);
            playerLantern.TriggerEmpowerPreviewWave();
        }
        else if (weaponCtrl != null)
        {
            weaponCtrl.UpgradeLanternToRadial(upgradedDamage, upgradedRadius);
        }

        // Esperar 0.45s para que se aprecie el estallido radial de 360° alrededor del jugador
        yield return new WaitForSeconds(0.45f);

        if (topDownCtrl != null) topDownCtrl.enabled = true;
        if (l2Ctrl != null) l2Ctrl.enabled = true;
        isAnimatingEmpower = false;

        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "PODER DEL PILAR DE LUZ",
                $"¡El Pilar de Luz ha otorgado su poder mágico a tu Farol!\nAhora ataca en un amplio radio de 360° a tu alrededor y causa {upgradedDamage} DMG (más que el Cuchillo).",
                null,
                null,
                true
            );
        }
    }

    private static Sprite GetOrCreateSoftOrbSprite()
    {
        if (softOrbSprite != null) return softOrbSprite;
        int size = 24;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                if (d > 1f) tex.SetPixel(x, y, Color.clear);
                else if (d < 0.35f) tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                else tex.SetPixel(x, y, new Color(0.35f, 0.95f, 1f, Mathf.Clamp01(1f - d)));
            }
        }
        tex.Apply();
        softOrbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return softOrbSprite;
    }

    private static Sprite GetOrCreateRuneFloorCircleSprite()
    {
        if (runeFloorCircleSprite != null) return runeFloorCircleSprite;
        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                if (Mathf.Abs(d - 0.92f) < 0.06f || Mathf.Abs(d - 0.68f) < 0.05f)
                    tex.SetPixel(x, y, Color.white);
                else if (d < 0.92f && ((x + y) % 5 == 0))
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.35f));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        runeFloorCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return runeFloorCircleSprite;
    }

    private static Sprite GetOrCreateBeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        int w = 24;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = (w - 1) * 0.5f;
        for (int y = 0; y < h; y++)
        {
            float vy = 1f - (y / (float)(h - 1));
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx) / cx;
                float a = Mathf.Clamp01(1f - dx) * vy;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.0f), 32f);
        return beamSprite;
    }
}
