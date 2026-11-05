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
    public int lanternDamage = 17;
    public int bigKnifeDamage = 28;
    public int macheteDamage = 50;

    [Header("Alcance y Velocidad de Ataque")]
    public float lanternRange = 2.0f;
    public float knifeRange = 0.88f;
    public float macheteRange = 1.15f;
    [Range(30f, 180f)] public float attackArcAngle = 105f;
    public float attackCooldown = 0.34f;

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

    private Transform handWeaponHolder;
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
                case WeaponType.BigKnife: return $"Cuchillo ({bigKnifeDamage} DMG)";
                case WeaponType.Machete: return $"Machete ({macheteDamage} DMG)";
                default: return $"Farol de Ánimas ({lanternDamage} DMG)";
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

        // Forzar progresión real de daño y alcance para evitar valores antiguos serializados en escena:
        // Farol = 17 DMG (3 golpes a duende normal de 50 HP)
        // Cuchillo = 28 DMG (2 golpes a duende normal de 50 HP, 3 a robusto de 80 HP)
        // Machete = 50 DMG (1 golpe a duende normal de 50 HP, 2 a robusto de 80 HP)
        lanternDamage = 17;
        bigKnifeDamage = 28;
        macheteDamage = 50;
        lanternRange = 2.0f;
        knifeRange = 0.88f;
        macheteRange = 1.15f;
        attackArcAngle = 105f;

        // En el Nivel 3 (La Bruja), el jugador inicia con todo el arsenal obtenido en el Nivel 2 (Farol, Cuchillo y Machete)
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
            activeScene.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hasLantern = true;
            hasBigKnife = true;
            hasMachete = true;
        }

        if (lanternComponent != null)
        {
            lanternComponent.range = lanternRange;
        }

        LoadWeaponAndThrowSprites();
        CreateHandWeaponHolder();
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
        UpdateEquippedHandWeaponVisual();
    }

    private void CreateHandWeaponHolder()
    {
        Transform existing = transform.Find("HandWeaponHolder");
        if (existing != null)
        {
            handWeaponHolder = existing;
        }
        else
        {
            GameObject holderObj = new GameObject("HandWeaponHolder");
            holderObj.transform.SetParent(transform, false);
            handWeaponHolder = holderObj.transform;
        }
        SyncHolderScaleCompensation();
    }

    private void SyncHolderScaleCompensation()
    {
        if (handWeaponHolder == null) return;
        Vector3 parentScale = transform.localScale;
        float invX = Mathf.Abs(parentScale.x) > 0.001f ? 1f / Mathf.Abs(parentScale.x) : 1f;
        float invY = Mathf.Abs(parentScale.y) > 0.001f ? 1f / Mathf.Abs(parentScale.y) : 1f;
        handWeaponHolder.localPosition = Vector3.zero;
        handWeaponHolder.localRotation = Quaternion.identity;
        handWeaponHolder.localScale = new Vector3(invX, invY, 1f);
    }

    public void UpdateFarolHUD()
    {
        var farolHud = GameObject.Find("Farol_HUD");
        if (farolHud == null) return;

        var nameText = farolHud.transform.Find("Nombre_Farol")?.GetComponent<TMPro.TMP_Text>();
        var helpText = farolHud.transform.Find("Ayuda_Farol")?.GetComponent<TMPro.TMP_Text>();
        var keyText = farolHud.transform.Find("Tecla_Farol")?.GetComponentInChildren<TMPro.TMP_Text>();
        var iconImg = farolHud.transform.Find("Icono_Farol")?.GetComponent<UnityEngine.UI.Image>();

        if (keyText != null) keyText.text = "F";

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
                if (nameText != null) nameText.text = "FAROL DE ANIMAS";
                if (helpText != null) helpText.text = $"DESTELLO ({lanternDamage} DMG)";
                if (iconImg != null && lanternHudSprite != null) { iconImg.sprite = lanternHudSprite; iconImg.preserveAspect = true; }
                break;
            case WeaponType.BigKnife:
                if (nameText != null) nameText.text = "CUCHILLO";
                if (helpText != null) helpText.text = $"CORTE ({bigKnifeDamage} DMG)";
                if (iconImg != null && knifeHudSprite != null) { iconImg.sprite = knifeHudSprite; iconImg.preserveAspect = true; }
                break;
            case WeaponType.Machete:
                if (nameText != null) nameText.text = "MACHETE";
                if (helpText != null) helpText.text = $"TAJO ({macheteDamage} DMG)";
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
        if (handWeaponHolder == null) CreateHandWeaponHolder();

        handWeaponObj = new GameObject("HandWeaponVisual");
        handWeaponObj.transform.SetParent(handWeaponHolder, false);
        handWeaponRenderer = handWeaponObj.AddComponent<SpriteRenderer>();
        if (playerSprite != null)
        {
            handWeaponRenderer.sortingLayerID = playerSprite.sortingLayerID;
            handWeaponRenderer.sortingOrder = playerSprite.sortingOrder + 1;
        }
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

    private void LateUpdate()
    {
        SyncHolderScaleCompensation();
        UpdateEquippedHandWeaponVisual();
    }

    private float GetWeaponBaseScale()
    {
        // En espacio 1:1 (compensado de la escala X=2 del PF Player):
        // Cuchillo (24x24, 20 PPU) * 0.28 = 0.33 unidades (compacto y ajustado a la mano)
        // Machete (32x32, 20 PPU) * 0.32 = 0.51 unidades (más pequeño y proporcionado al jugador)
        return currentWeapon == WeaponType.BigKnife ? 0.28f : 0.32f;
    }

    public void UpdateEquippedHandWeaponVisual()
    {
        if (handWeaponRenderer == null || handWeaponObj == null) return;

        bool isDead = playerHealth != null && playerHealth.currentHealth <= 0;
        bool isMeleeEquipped = currentWeapon == WeaponType.BigKnife || currentWeapon == WeaponType.Machete;

        if (isDead || !isMeleeEquipped)
        {
            if (handWeaponRenderer.enabled) handWeaponRenderer.enabled = false;
            return;
        }

        if (attackAnimRoutine != null)
            return;

        Sprite weaponSprite = currentWeapon == WeaponType.BigKnife ? knifeSprite : macheteSprite;
        if (weaponSprite == null)
        {
            if (handWeaponRenderer.enabled) handWeaponRenderer.enabled = false;
            return;
        }

        handWeaponRenderer.sprite = weaponSprite;
        if (playerSprite != null)
            handWeaponRenderer.sortingLayerID = playerSprite.sortingLayerID;

        int dir = animator != null ? animator.GetInteger("Direction") : 0;
        if (dir == 2 && playerSprite != null && playerSprite.flipX)
            dir = 3;

        // Oscilación sutil de la mano al caminar para que el arma acompañe el braceo
        bool isMoving = animator != null && animator.GetBool("IsMoving");
        float walkBobY = isMoving ? Mathf.Sin(Time.time * 14f) * 0.018f : 0f;
        float walkBobX = isMoving ? Mathf.Cos(Time.time * 14f) * 0.012f : 0f;

        float scale = GetWeaponBaseScale();
        Vector3 handLocalPos;
        Quaternion handLocalRot;
        Vector3 handLocalScale;
        int sortOrder;

        // Coordenadas exactas de la mano del personaje en unidades de mundo (32x32 px, pivot inferior (0.5, 0)):
        // La mano se ubica a y = 0.35..0.37 (11.5 px desde los pies) y x = ±0.165 en vista frontal/trasera o ±0.10 en perfil.
        switch (dir)
        {
            case 1: // Arriba (espalda a cámara: mano derecha en x = +0.165, y = 0.37, detrás del cuerpo)
                handLocalPos = new Vector3(0.165f + walkBobX, 0.37f + walkBobY, 0f);
                handLocalRot = Quaternion.Euler(0f, 0f, -15f);
                handLocalScale = new Vector3(scale, scale, 1f);
                sortOrder = playerSprite != null ? playerSprite.sortingOrder - 1 : 1;
                break;

            case 2: // Derecha (mano sujetando el mango al frente en x = +0.10, y = 0.355)
                handLocalPos = new Vector3(0.10f + walkBobX, 0.355f + walkBobY, 0f);
                handLocalRot = Quaternion.Euler(0f, 0f, -22f);
                handLocalScale = new Vector3(scale, scale, 1f);
                sortOrder = playerSprite != null ? playerSprite.sortingOrder + 1 : 3;
                break;

            case 3: // Izquierda (espejo de derecha: mano en x = -0.10, y = 0.355)
                handLocalPos = new Vector3(-0.10f - walkBobX, 0.355f + walkBobY, 0f);
                handLocalRot = Quaternion.Euler(0f, 0f, 22f);
                handLocalScale = new Vector3(-scale, scale, 1f);
                sortOrder = playerSprite != null ? playerSprite.sortingOrder + 1 : 3;
                break;

            default: // 0: Abajo (frente a cámara: mano izquierda del sprite en x = +0.165, y = 0.355)
                handLocalPos = new Vector3(0.165f + walkBobX, 0.355f + walkBobY, 0f);
                handLocalRot = Quaternion.Euler(0f, 0f, -20f);
                handLocalScale = new Vector3(scale, scale, 1f);
                sortOrder = playerSprite != null ? playerSprite.sortingOrder + 1 : 3;
                break;
        }

        handWeaponObj.transform.localPosition = handLocalPos;
        handWeaponObj.transform.localRotation = handLocalRot;
        handWeaponObj.transform.localScale = handLocalScale;
        handWeaponRenderer.sortingOrder = sortOrder;
        if (!handWeaponRenderer.enabled) handWeaponRenderer.enabled = true;
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
                    ParchmentMessageUI.Instance.Show("ARMA BLOQUEADA", "Recoge el Cuchillo junto al primer juguete para desbloquearlo.", null, null, true);
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
                    ParchmentMessageUI.Instance.Show("ARMA BLOQUEADA", "Derrota al Niño Espectral y recoge el Machete en el altar para desbloquearlo.", null, null, true);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
        {
            CycleNextWeapon();
        }
    }

    private void HandleAttackInput()
    {
        bool attackPressed = GameControls.LanternPressed || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
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
        if (lanternComponent == null) lanternComponent = GetComponent<PlayerLantern>();
        if (lanternComponent != null)
        {
            lanternComponent.range = Mathf.Max(2f, lanternRange);
            if (lanternComponent.TryAttack())
            {
                nextAttackAt = Time.time + lanternComponent.cooldown;
                if (combatAudio != null && swingClip != null)
                    combatAudio.PlayOneShot(swingClip);
            }
            return;
        }

        nextAttackAt = Time.time + attackCooldown;

        Vector2 origin = playerCollider != null
            ? (Vector2)playerCollider.bounds.center
            : (Vector2)transform.position + Vector2.up * 0.35f;

        float range = Mathf.Max(2f, lanternRange);
        int damage = lanternDamage;
        float cosHalfArc = Mathf.Cos(attackArcAngle * 0.5f * Mathf.Deg2Rad);

        if (combatAudio != null && swingClip != null)
            combatAudio.PlayOneShot(swingClip);

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

        if (Level2VictoryManager.Instance != null)
        {
            Level2VictoryManager.Instance.CheckVictoryConditions();
        }
    }

    public void EquipWeapon(WeaponType weapon)
    {
        if (weapon == WeaponType.BigKnife && !hasBigKnife) return;
        if (weapon == WeaponType.Machete && !hasMachete) return;
        if (weapon == WeaponType.Lantern && !hasLantern) return;

        currentWeapon = weapon;
        UpdateFarolHUD();
        UpdateEquippedHandWeaponVisual();
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
            : (Vector2)transform.position + Vector2.up * 0.36f;

        float range = CurrentRange;
        int damage = CurrentDamage;
        float cosHalfArc = Mathf.Cos(attackArcAngle * 0.5f * Mathf.Deg2Rad);

        if (combatAudio != null && swingClip != null)
            combatAudio.PlayOneShot(swingClip);

        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(AnimateMeleeAttackRoutine(origin, lastFacingDir, range));

        HashSet<GameObject> processedRoots = new HashSet<GameObject>();
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin + lastFacingDir * (range * 0.45f), range * 0.65f);

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

                // Verificar que el borde del enemigo esté realmente dentro del alcance visual del cuchillo/machete
                Vector2 closestPoint = col.ClosestPoint(origin);
                float edgeDist = Vector2.Distance(origin, closestPoint);
                if (edgeDist > range)
                    continue;

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

                Vector2 closestPoint = col.ClosestPoint(origin);
                if (Vector2.Distance(origin, closestPoint) > range)
                    continue;

                Vector2 toTarget = (Vector2)col.bounds.center - origin;
                if (toTarget.sqrMagnitude > 0.02f && Vector2.Dot(lastFacingDir, toTarget.normalized) < cosHalfArc)
                    continue;

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
        if (handWeaponHolder == null) CreateHandWeaponHolder();

        slashVisualObj = new GameObject("MeleeSlashVisual");
        slashVisualObj.transform.SetParent(handWeaponHolder, false);
        slashRenderer = slashVisualObj.AddComponent<SpriteRenderer>();
        slashRenderer.sprite = GetOrCreateSlashSprite();
        slashRenderer.sortingOrder = 600;
        slashRenderer.enabled = false;
    }

    private IEnumerator AnimateMeleeAttackRoutine(Vector2 origin, Vector2 dir, float range)
    {
        SyncHolderScaleCompensation();

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

        Sprite weaponSprite = currentWeapon == WeaponType.BigKnife ? knifeSprite : macheteSprite;
        float baseScale = GetWeaponBaseScale();
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Trayectoria de la mano en los 4 frames de ataque (en unidades de mundo desde los pies del jugador)
        Vector3[] frameHandPositions;
        float rotStart;
        float rotEnd;
        Vector3 swingScale;

        switch (dirIndex)
        {
            case 1: // Arriba
                frameHandPositions = new Vector3[]
                {
                    new Vector3(0.17f, 0.39f, 0f),
                    new Vector3(0.16f, 0.44f, 0f),
                    new Vector3(0.12f, 0.49f, 0f),
                    new Vector3(0.08f, 0.45f, 0f)
                };
                rotStart = -10f;
                rotEnd = 85f;
                swingScale = new Vector3(baseScale, baseScale, 1f);
                break;

            case 2: // Derecha
                frameHandPositions = new Vector3[]
                {
                    new Vector3(0.06f, 0.40f, 0f),
                    new Vector3(0.14f, 0.39f, 0f),
                    new Vector3(0.20f, 0.36f, 0f),
                    new Vector3(0.15f, 0.34f, 0f)
                };
                rotStart = 35f;
                rotEnd = -65f;
                swingScale = new Vector3(baseScale, baseScale, 1f);
                break;

            case 3: // Izquierda (espejo de derecha)
                frameHandPositions = new Vector3[]
                {
                    new Vector3(-0.06f, 0.40f, 0f),
                    new Vector3(-0.14f, 0.39f, 0f),
                    new Vector3(-0.20f, 0.36f, 0f),
                    new Vector3(-0.15f, 0.34f, 0f)
                };
                rotStart = -35f;
                rotEnd = 65f;
                swingScale = new Vector3(-baseScale, baseScale, 1f);
                break;

            default: // 0: Abajo
                frameHandPositions = new Vector3[]
                {
                    new Vector3(0.17f, 0.41f, 0f),
                    new Vector3(0.16f, 0.37f, 0f),
                    new Vector3(0.14f, 0.33f, 0f),
                    new Vector3(0.10f, 0.34f, 0f)
                };
                rotStart = -15f;
                rotEnd = -115f;
                swingScale = new Vector3(baseScale, baseScale, 1f);
                break;
        }

        if (handWeaponRenderer != null && weaponSprite != null)
        {
            handWeaponRenderer.sprite = weaponSprite;
            if (playerSprite != null) handWeaponRenderer.sortingLayerID = playerSprite.sortingLayerID;
            handWeaponRenderer.sortingOrder = dirIndex == 1
                ? (playerSprite != null ? playerSprite.sortingOrder - 1 : 1)
                : (playerSprite != null ? playerSprite.sortingOrder + 1 : 3);

            handWeaponObj.transform.localScale = swingScale;
            handWeaponRenderer.enabled = true;
        }

        if (slashRenderer != null)
        {
            if (playerSprite != null)
                slashRenderer.sortingLayerID = playerSprite.sortingLayerID;

            slashVisualObj.transform.localPosition = new Vector3(0f, 0.36f, 0f) + (Vector3)(dir * (range * 0.42f));
            slashVisualObj.transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);

            float scaleMultiplier = currentWeapon == WeaponType.BigKnife ? 0.46f :
                                    currentWeapon == WeaponType.Machete ? 0.68f : 0.55f;
            slashVisualObj.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);

            Color slashColor = currentWeapon == WeaponType.Machete
                ? new Color(0.85f, 0.95f, 1f, 0.92f)
                : new Color(0.95f, 0.95f, 0.98f, 0.88f);

            slashRenderer.color = slashColor;
            slashRenderer.enabled = true;
        }

        float duration = 0.20f;
        float elapsed = 0f;

        bool restoreAnimator = false;
        if (animator != null && animator.enabled)
        {
            animator.enabled = false;
            restoreAnimator = true;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int frameIdx = Mathf.Clamp(Mathf.FloorToInt(t * 4f), 0, 3);

            if (throwFrames != null && throwFrames.Length == 4 && playerSprite != null)
            {
                if (throwFrames[frameIdx] != null)
                {
                    playerSprite.sprite = throwFrames[frameIdx];
                }
            }

            if (handWeaponRenderer != null && weaponSprite != null)
            {
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float currentAngle = Mathf.Lerp(rotStart, rotEnd, smoothT);
                handWeaponObj.transform.localPosition = frameHandPositions[frameIdx];
                handWeaponObj.transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
                handWeaponObj.transform.localScale = swingScale;
            }

            if (slashRenderer != null)
            {
                Color c = slashRenderer.color;
                slashRenderer.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.92f, 0f, t));
            }

            yield return null;
        }

        if (slashRenderer != null) slashRenderer.enabled = false;
        if (restoreAnimator && animator != null) animator.enabled = true;
        attackAnimRoutine = null;
        UpdateEquippedHandWeaponVisual();
    }

    private void OnDisable()
    {
        if (handWeaponRenderer != null) handWeaponRenderer.enabled = false;
        if (slashRenderer != null) slashRenderer.enabled = false;
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
