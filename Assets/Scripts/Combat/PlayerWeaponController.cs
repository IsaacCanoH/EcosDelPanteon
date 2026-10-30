using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [Header("Armas Desbloqueadas")]
    public WeaponType currentWeapon = WeaponType.Lantern;
    public bool hasLantern = true;
    public bool hasBigKnife = false;
    public bool hasMachete = false;

    [Header("Daño por Arma (Especificación Nivel 2)")]
    public int lanternDamage = 16;
    public int bigKnifeDamage = 18;
    public int macheteDamage = 25;

    [Header("Alcance y Velocidad de Ataque")]
    public float lanternRange = 2.4f;
    public float knifeRange = 1.25f;
    public float macheteRange = 1.55f;
    [Range(30f, 180f)] public float attackArcAngle = 135f;
    public float attackCooldown = 0.36f;

    [Header("Referencias Opcionales")]
    public PlayerLantern lanternComponent;
    public AudioSource combatAudio;
    public AudioClip swingClip;
    public AudioClip hitClip;

    public event Action<WeaponType> OnWeaponChanged;

    private Animator animator;
    private SpriteRenderer playerSprite;
    private PlayerHealth playerHealth;
    private Collider2D playerCollider;
    private PlayerStoneThrow stoneThrower;
    private float nextAttackAt;
    private Vector2 lastFacingDir = Vector2.down;

    private GameObject slashVisualObj;
    private SpriteRenderer slashRenderer;
    private static Sprite slashSprite;

    private GameObject handWeaponObj;
    private SpriteRenderer handWeaponRenderer;
    public Sprite knifeSprite;
    public Sprite macheteSprite;
    public Sprite fistsHudSprite;
    public Sprite lanternHudSprite;
    public Sprite knifeHudSprite;
    public Sprite macheteHudSprite;

    private Sprite[] downThrowSprites;
    private Sprite[] upThrowSprites;
    private Sprite[] rightThrowSprites;
    private Sprite[] leftThrowSprites;
    private Coroutine attackAnimRoutine;

    public int CurrentDamage
    {
        get
        {
            switch (currentWeapon)
            {
                case WeaponType.BigKnife: return bigKnifeDamage;
                case WeaponType.Machete: return macheteDamage;
                default: return lanternDamage;
            }
        }
    }

    public float CurrentRange
    {
        get
        {
            switch (currentWeapon)
            {
                case WeaponType.BigKnife: return knifeRange;
                case WeaponType.Machete: return macheteRange;
                default: return lanternRange;
            }
        }
    }

    public string CurrentWeaponDisplayName
    {
        get
        {
            switch (currentWeapon)
            {
                case WeaponType.BigKnife: return "Cuchillo Grande (18 DMG)";
                case WeaponType.Machete: return "Machete (25 DMG)";
                default: return "Farol de Ánimas (16 DMG)";
            }
        }
    }

    private void Awake()
    {
        enabled = true;
        animator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
        playerCollider = GetComponent<Collider2D>();
        if (lanternComponent == null) lanternComponent = GetComponent<PlayerLantern>();

        LoadWeaponAndThrowSprites();
        CreateRuntimeSlashVisual();
        CreateHandWeaponVisual();

        if (stoneThrower == null) stoneThrower = GetComponent<PlayerStoneThrow>();
        if (stoneThrower == null)
        {
            stoneThrower = gameObject.AddComponent<PlayerStoneThrow>();
            stoneThrower.startingStones = 6;
        }
    }

    private void Start()
    {
        UpdateFarolHUD();
    }

    public void UpdateFarolHUD()
    {
        var farolHud = GameObject.Find("Farol_HUD");
        if (farolHud == null) return;

        var nameText = farolHud.transform.Find("Nombre_Farol")?.GetComponent<TMPro.TMP_Text>();
        var helpText = farolHud.transform.Find("Ayuda_Farol")?.GetComponent<TMPro.TMP_Text>();
        var keyText = farolHud.transform.Find("Tecla_Farol")?.GetComponentInChildren<TMPro.TMP_Text>();
        var iconImg = farolHud.transform.Find("Icono_Farol")?.GetComponent<UnityEngine.UI.Image>();

        if (keyText != null) keyText.text = "J";

        // Asegurar referencias de sprites para el HUD
        if (knifeHudSprite == null) knifeHudSprite = knifeSprite;
        if (macheteHudSprite == null) macheteHudSprite = macheteSprite;
#if UNITY_EDITOR
        if (fistsHudSprite == null)
            fistsHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_PunosHUD.png");
        if (lanternHudSprite == null)
            lanternHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Gameplay/Farol/Farol_Animas.png");
        if (knifeHudSprite == null)
            knifeHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_CuchilloGrande.png");
        if (macheteHudSprite == null)
            macheteHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_Machete.png");
#endif

        switch (currentWeapon)
        {
            case WeaponType.Lantern:
                if (nameText != null) nameText.text = "FAROL DE ÁNIMAS";
                if (helpText != null) helpText.text = "DESTELLO (16 DMG)";
                if (iconImg != null && lanternHudSprite != null) { iconImg.sprite = lanternHudSprite; iconImg.preserveAspect = true; }
                break;
            case WeaponType.BigKnife:
                if (nameText != null) nameText.text = "CUCHILLO GRANDE";
                if (helpText != null) helpText.text = "CORTE (18 DMG)";
                if (iconImg != null && knifeHudSprite != null) { iconImg.sprite = knifeHudSprite; iconImg.preserveAspect = true; }
                break;
            case WeaponType.Machete:
                if (nameText != null) nameText.text = "MACHETE";
                if (helpText != null) helpText.text = "TAJO (25 DMG)";
                if (iconImg != null && macheteHudSprite != null) { iconImg.sprite = macheteHudSprite; iconImg.preserveAspect = true; }
                break;
        }
    }

    private void LoadWeaponAndThrowSprites()
    {
#if UNITY_EDITOR
        if (knifeSprite == null)
            knifeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_CuchilloGrande.png");
        if (macheteSprite == null)
            macheteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_Machete.png");
        if (fistsHudSprite == null)
            fistsHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_PunosHUD.png");
        if (lanternHudSprite == null)
            lanternHudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Gameplay/Farol/Farol_Animas.png");
        if (knifeHudSprite == null)
            knifeHudSprite = knifeSprite;
        if (macheteHudSprite == null)
            macheteHudSprite = macheteSprite;

        Sprite[] throws = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player/Throw/Player_Throw.png") as Sprite[];
        if (throws == null || throws.Length == 0)
        {
            var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player/Throw/Player_Throw.png");
            List<Sprite> list = new List<Sprite>();
            foreach (var o in objs) if (o is Sprite s) list.Add(s);
            throws = list.ToArray();
        }

        if (throws != null && throws.Length > 0)
        {
            downThrowSprites = new Sprite[4];
            upThrowSprites = new Sprite[4];
            rightThrowSprites = new Sprite[4];
            leftThrowSprites = new Sprite[4];

            foreach (var s in throws)
            {
                if (s.name.StartsWith("Throw_Down_") && int.TryParse(s.name.Substring(11), out int dIdx) && dIdx < 4)
                    downThrowSprites[dIdx] = s;
                else if (s.name.StartsWith("Throw_Up_") && int.TryParse(s.name.Substring(9), out int uIdx) && uIdx < 4)
                    upThrowSprites[uIdx] = s;
                else if (s.name.StartsWith("Throw_Right_") && int.TryParse(s.name.Substring(12), out int rIdx) && rIdx < 4)
                    rightThrowSprites[rIdx] = s;
                else if (s.name.StartsWith("Throw_Left_") && int.TryParse(s.name.Substring(11), out int lIdx) && lIdx < 4)
                    leftThrowSprites[lIdx] = s;
            }
        }
#endif
    }

    private void CreateHandWeaponVisual()
    {
        handWeaponObj = new GameObject("HandWeaponVisual");
        handWeaponObj.transform.SetParent(transform, false);
        handWeaponRenderer = handWeaponObj.AddComponent<SpriteRenderer>();
        handWeaponRenderer.sortingOrder = playerSprite != null ? playerSprite.sortingOrder + 1 : 480;
        handWeaponRenderer.enabled = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || (playerHealth != null && playerHealth.currentHealth <= 0))
            return;

        UpdateFacingDirection();
        HandleWeaponSwitchInput();
        HandleAttackInput();
    }

    private void UpdateFacingDirection()
    {
        if (animator != null)
        {
            int dir = animator.GetInteger("Direction");
            switch (dir)
            {
                case 1:
                    lastFacingDir = Vector2.up;
                    break;
                case 2:
                    lastFacingDir = (playerSprite != null && playerSprite.flipX) ? Vector2.left : Vector2.right;
                    break;
                case 3:
                    lastFacingDir = Vector2.left;
                    break;
                default:
                    lastFacingDir = Vector2.down;
                    break;
            }
        }
    }

    private void HandleWeaponSwitchInput()
    {
        // 1 = Farol (16 DMG), 2 = Cuchillo Grande (18 DMG), 3 = Machete (25 DMG)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            if (hasLantern) EquipWeapon(WeaponType.Lantern);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (hasBigKnife)
            {
                EquipWeapon(WeaponType.BigKnife);
            }
            else
            {
                if (ParchmentMessageUI.Instance != null)
                    ParchmentMessageUI.Instance.Show("ARMA BLOQUEADA", "Derrota la primera oleada de duendes para desbloquear el Cuchillo Grande.", null, null, false);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            if (hasMachete)
            {
                EquipWeapon(WeaponType.Machete);
            }
            else
            {
                if (ParchmentMessageUI.Instance != null)
                    ParchmentMessageUI.Instance.Show("ARMA BLOQUEADA", "Derrota al Niño Espectral para desbloquear el Machete.", null, null, false);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
        {
            CycleNextWeapon();
        }
    }

    private void HandleAttackInput()
    {
        if (stoneThrower == null) stoneThrower = GetComponent<PlayerStoneThrow>();
        bool spaceUsedForStone = stoneThrower != null && stoneThrower.RemainingStones > 0;
        bool attackPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J) || (!spaceUsedForStone && Input.GetKeyDown(KeyCode.Space));
        if (!attackPressed || Time.time < nextAttackAt)
            return;

        if (currentWeapon == WeaponType.Lantern)
        {
            PerformLanternAttack();
            return;
        }

        PerformMeleeAttack();
    }

    public void PerformLanternAttack()
    {
        nextAttackAt = Time.time + attackCooldown;

        if (lanternComponent != null)
        {
            lanternComponent.TryAttack();
        }

        Vector2 origin = playerCollider != null
            ? (Vector2)playerCollider.bounds.center
            : (Vector2)transform.position + Vector2.up * 0.35f;

        float range = lanternRange;
        int damage = lanternDamage;
        float cosHalfArc = Mathf.Cos(attackArcAngle * 0.5f * Mathf.Deg2Rad);

        if (combatAudio != null && swingClip != null)
            combatAudio.PlayOneShot(swingClip);

        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(AnimateMeleeAttackRoutine(origin, lastFacingDir, range));

        HashSet<GameObject> processedRoots = new HashSet<GameObject>();
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, range);

        bool hitAny = false;
        foreach (Collider2D col in colliders)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;

            EnemyHealth enemyHealth = col.GetComponentInParent<EnemyHealth>();
            GoblinAI goblinAI = col.GetComponentInParent<GoblinAI>();
            GoblinCombat goblinCombat = col.GetComponentInParent<GoblinCombat>();

            if (enemyHealth == null && goblinAI != null)
            {
                enemyHealth = goblinAI.gameObject.AddComponent<EnemyHealth>();
            }

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                if (!processedRoots.Add(enemyHealth.gameObject)) continue;

                Vector2 toTarget = (Vector2)col.bounds.center - origin;
                if (toTarget.sqrMagnitude > 0.02f && Vector2.Dot(lastFacingDir, toTarget.normalized) < cosHalfArc)
                    continue;

                enemyHealth.TakeDamage(damage, lastFacingDir);
                if (goblinCombat != null) goblinCombat.TakeLanternHit();
                hitAny = true;
                continue;
            }

            IDamageable genericTarget = col.GetComponentInParent<IDamageable>();
            if (genericTarget != null && !genericTarget.IsDead)
            {
                MonoBehaviour mb = genericTarget as MonoBehaviour;
                if (mb != null && !processedRoots.Add(mb.gameObject)) continue;

                genericTarget.TakeDamage(damage, lastFacingDir);
                hitAny = true;
            }
        }

        if (hitAny && combatAudio != null && hitClip != null)
        {
            combatAudio.PlayOneShot(hitClip);
        }
    }

    public void UnlockAndEquip(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Lantern:
                hasLantern = true;
                break;
            case WeaponType.BigKnife:
                hasBigKnife = true;
                break;
            case WeaponType.Machete:
                hasMachete = true;
                break;
        }
        EquipWeapon(weapon);
    }

    public void EquipWeapon(WeaponType weapon)
    {
        if (weapon == WeaponType.BigKnife && !hasBigKnife) return;
        if (weapon == WeaponType.Machete && !hasMachete) return;
        if (weapon == WeaponType.Lantern && !hasLantern) return;

        currentWeapon = weapon;
        UpdateFarolHUD();
        OnWeaponChanged?.Invoke(currentWeapon);
        Debug.Log($"[Armas] Equipada: {CurrentWeaponDisplayName}");
    }

    public void CycleNextWeapon()
    {
        List<WeaponType> unlocked = new List<WeaponType> { WeaponType.Lantern };
        if (hasBigKnife) unlocked.Add(WeaponType.BigKnife);
        if (hasMachete) unlocked.Add(WeaponType.Machete);

        int idx = unlocked.IndexOf(currentWeapon);
        if (idx < 0) idx = 0;
        int nextIdx = (idx + 1) % unlocked.Count;
        EquipWeapon(unlocked[nextIdx]);
    }

    public void PerformMeleeAttack()
    {
        nextAttackAt = Time.time + attackCooldown;

        Vector2 origin = playerCollider != null
            ? (Vector2)playerCollider.bounds.center
            : (Vector2)transform.position + Vector2.up * 0.35f;

        float range = CurrentRange;
        int damage = CurrentDamage;
        float cosHalfArc = Mathf.Cos(attackArcAngle * 0.5f * Mathf.Deg2Rad);

        if (combatAudio != null && swingClip != null)
            combatAudio.PlayOneShot(swingClip);

        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(AnimateMeleeAttackRoutine(origin, lastFacingDir, range));

        HashSet<GameObject> processedRoots = new HashSet<GameObject>();
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin + lastFacingDir * (range * 0.45f), range * 0.85f);

        bool hitAny = false;
        foreach (Collider2D col in colliders)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;

            // Ignorar triggers puros de zonas, pero permitir enemigos cuyo cuerpo sea collider normal
            EnemyHealth enemyHealth = col.GetComponentInParent<EnemyHealth>();
            GoblinAI goblinAI = col.GetComponentInParent<GoblinAI>();

            if (enemyHealth == null && goblinAI != null)
            {
                // Si el duende de la escena aún no tenía EnemyHealth, se lo agregamos al vuelo con 50 de vida
                enemyHealth = goblinAI.gameObject.AddComponent<EnemyHealth>();
            }

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                if (!processedRoots.Add(enemyHealth.gameObject)) continue;

                Vector2 toTarget = (Vector2)col.bounds.center - origin;
                if (toTarget.sqrMagnitude > 0.02f && Vector2.Dot(lastFacingDir, toTarget.normalized) < cosHalfArc)
                    continue;

                enemyHealth.TakeDamage(damage, lastFacingDir);
                hitAny = true;
                continue;
            }

            IDamageable genericTarget = col.GetComponentInParent<IDamageable>();
            if (genericTarget != null && !genericTarget.IsDead)
            {
                MonoBehaviour mb = genericTarget as MonoBehaviour;
                if (mb != null && !processedRoots.Add(mb.gameObject)) continue;

                genericTarget.TakeDamage(damage, lastFacingDir);
                hitAny = true;
            }
        }

        if (hitAny && combatAudio != null && hitClip != null)
        {
            combatAudio.PlayOneShot(hitClip);
        }
    }

    private void CreateRuntimeSlashVisual()
    {
        slashVisualObj = new GameObject("MeleeSlashVisual");
        slashVisualObj.transform.SetParent(transform, false);
        slashRenderer = slashVisualObj.AddComponent<SpriteRenderer>();
        slashRenderer.sprite = GetOrCreateSlashSprite();
        slashRenderer.sortingOrder = 600;
        slashRenderer.enabled = false;
    }

    private IEnumerator AnimateMeleeAttackRoutine(Vector2 origin, Vector2 dir, float range)
    {
        // 1. Determinar dirección de animación (0=Abajo, 1=Arriba, 2=Derecha, 3=Izquierda)
        int dirIndex = 0;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            dirIndex = dir.x > 0 ? 2 : 3;
        else
            dirIndex = dir.y > 0 ? 1 : 0;

        Sprite[] throwFrames = null;
        switch (dirIndex)
        {
            case 1: throwFrames = upThrowSprites; break;
            case 2: throwFrames = rightThrowSprites; break;
            case 3: throwFrames = leftThrowSprites; break;
            default: throwFrames = downThrowSprites; break;
        }

        // 2. Determinar sprite y offset de mano del arma equipada
        Sprite weaponSprite = null;
        if (currentWeapon == WeaponType.BigKnife) weaponSprite = knifeSprite;
        else if (currentWeapon == WeaponType.Machete) weaponSprite = macheteSprite;

        Vector3 handOffset = Vector3.zero;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        switch (dirIndex)
        {
            case 0: // Abajo
                handOffset = new Vector3(0.18f, -0.05f, 0f);
                break;
            case 1: // Arriba
                handOffset = new Vector3(-0.16f, 0.22f, 0f);
                break;
            case 2: // Derecha
                handOffset = new Vector3(0.20f, 0.08f, 0f);
                break;
            case 3: // Izquierda
                handOffset = new Vector3(-0.20f, 0.08f, 0f);
                break;
        }

        // Activar arma en mano
        if (handWeaponRenderer != null && weaponSprite != null)
        {
            handWeaponRenderer.sprite = weaponSprite;
            if (playerSprite != null) handWeaponRenderer.sortingLayerID = playerSprite.sortingLayerID;
            handWeaponRenderer.sortingOrder = dirIndex == 1
                ? (playerSprite != null ? playerSprite.sortingOrder - 1 : 460)
                : (playerSprite != null ? playerSprite.sortingOrder + 1 : 480);

            float weaponScale = currentWeapon == WeaponType.Machete ? 1.35f : 1.05f;
            handWeaponObj.transform.localScale = new Vector3(weaponScale, weaponScale, 1f);
            handWeaponRenderer.enabled = true;
        }

        // Activar estela de corte (slash)
        if (slashRenderer != null)
        {
            if (playerSprite != null)
                slashRenderer.sortingLayerID = playerSprite.sortingLayerID;

            slashVisualObj.transform.position = origin + dir * (range * 0.52f);
            slashVisualObj.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);

            float scaleMultiplier = currentWeapon == WeaponType.Machete ? 1.55f :
                                    currentWeapon == WeaponType.BigKnife ? 1.25f : 0.85f;
            slashVisualObj.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);

            Color slashColor = currentWeapon == WeaponType.Machete
                ? new Color(0.85f, 0.95f, 1f, 0.95f)
                : currentWeapon == WeaponType.BigKnife
                    ? new Color(0.95f, 0.95f, 0.98f, 0.9f)
                    : new Color(1f, 0.88f, 0.65f, 0.8f);

            slashRenderer.color = slashColor;
            slashRenderer.enabled = true;
        }

        // 3. Ejecutar arco de swing y animación de mano (4 frames, 0.20s total)
        float duration = 0.20f;
        float elapsed = 0f;

        bool restoreAnimator = false;
        if (animator != null && animator.enabled)
        {
            animator.enabled = false;
            restoreAnimator = true;
        }

        float arcStart = baseAngle + 50f;
        float arcEnd = baseAngle - 50f;
        if (dirIndex == 3) // Flipped para izquierda
        {
            arcStart = baseAngle - 50f;
            arcEnd = baseAngle + 50f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Animar el sprite del jugador (mueve la mano y brazo hacia adelante)
            if (throwFrames != null && throwFrames.Length == 4 && playerSprite != null)
            {
                int frameIdx = Mathf.Clamp(Mathf.FloorToInt(t * 4f), 0, 3);
                if (throwFrames[frameIdx] != null)
                {
                    playerSprite.sprite = throwFrames[frameIdx];
                }
            }

            // Rotar el arma en mano
            if (handWeaponRenderer != null && weaponSprite != null)
            {
                float currentAngle = Mathf.Lerp(arcStart, arcEnd, Mathf.SmoothStep(0f, 1f, t));
                handWeaponObj.transform.position = transform.position + handOffset;
                handWeaponObj.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
            }

            // Desvanecer estela de corte
            if (slashRenderer != null)
            {
                Color c = slashRenderer.color;
                slashRenderer.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        // 4. Restaurar estados
        if (handWeaponRenderer != null) handWeaponRenderer.enabled = false;
        if (slashRenderer != null) slashRenderer.enabled = false;
        if (restoreAnimator && animator != null) animator.enabled = true;
    }

    private static Sprite GetOrCreateSlashSprite()
    {
        if (slashSprite != null) return slashSprite;

        int w = 24;
        int h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Vector2 arcCenter = new Vector2(2f, h * 0.5f);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2 p = new Vector2(x, y) - arcCenter;
                float r = p.magnitude;
                float a = Mathf.Abs(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);

                if (r >= 12f && r <= 19f && a <= 58f)
                {
                    float edgeFactor = 1f - (a / 58f);
                    float thickness = Mathf.Lerp(1.2f, 3.2f, edgeFactor);
                    if (Mathf.Abs(r - 16f) <= thickness)
                    {
                        tex.SetPixel(x, y, Color.white);
                        continue;
                    }
                }
                tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        slashSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.3f, 0.5f), 32f);
        return slashSprite;
    }
}

