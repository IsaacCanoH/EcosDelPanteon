using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class Level2HUD : MonoBehaviour
{
    public PlayerHealth health;
    public ToyCollector toys;
    public PlayerWeaponController weapons;
    public PlayerStoneThrow stones;

    [Header("Elementos de Vida y Juguetes (Estilo Nivel 1)")]
    public TMP_Text objective;
    public TMP_Text healthText;
    public TMP_Text notice;
    public TMP_Text noticeTitle;
    public CanvasGroup noticeCard;
    public TMP_Text exitHint;
    public Image[] toyIcons;
    public Image[] hearts;
    public Sprite fullHeart, emptyHeart;

    [Header("HUD Piedras y Armas")]
    public TMP_Text stonesCountText;
    public TMP_Text stonesHelpText;
    public TMP_Text weaponNameText;
    public TMP_Text weaponHelpText;

    public static bool IntroSequencePlaying = false;

    private int lastCount = 0;
    private float noticeUntil;
    private bool deathShown;

    private void Awake()
    {
        if (noticeCard != null)
        {
            noticeCard.alpha = 0f;
            noticeCard.interactable = false;
            noticeCard.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        FindPlayerComponents();
        if (weapons != null)
        {
            weapons.OnWeaponChanged += HandleWeaponChanged;
        }

        SetupSubHUDLayout();
    }

    private void FindPlayerComponents()
    {
        if (health == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) health = player.GetComponent<PlayerHealth>();
        }

        if (health != null)
        {
            if (weapons == null) weapons = health.GetComponent<PlayerWeaponController>();
            if (stones == null) stones = health.GetComponent<PlayerStoneThrow>();
            if (toys == null) toys = health.GetComponent<ToyCollector>();
        }
    }

    private void OnDestroy()
    {
        if (weapons != null)
        {
            weapons.OnWeaponChanged -= HandleWeaponChanged;
        }
    }

    private void HandleWeaponChanged(WeaponType newWeapon)
    {
        if (deathShown || weapons == null) return;
        ShowNotification("ARMA", weapons.CurrentWeaponDisplayName, 2.2f);
    }

    public void ShowNotification(string title, string message, float duration = 3.0f)
    {
        if (noticeTitle != null) noticeTitle.text = title;
        if (notice != null) notice.text = message;
        noticeUntil = Time.time + duration;
    }

    private void Update()
    {
        if (health == null)
        {
            FindPlayerComponents();
            if (health == null) return;
        }

        // 1. Contador de corazones numérico: "5/5" (Idéntico a Nivel 1, NO se sobrepone a los corazones)
        int activeHearts = health.ActiveHearts;
        if (healthText != null)
        {
            healthText.text = $"{activeHearts}/5";
        }

        // 2. Iconos de corazones
        if (hearts != null)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] != null)
                    hearts[i].sprite = i < activeHearts ? fullHeart : emptyHeart;
            }
        }

        // 3. Piedras HUD (6/6 [ESPACIO] LANZAR)
        if (stones != null)
        {
            if (stonesCountText != null)
                stonesCountText.text = $"PIEDRAS {stones.RemainingStones}/{stones.startingStones}";

            if (stonesHelpText != null)
                stonesHelpText.text = stones.RemainingStones > 0 ? "ESPACIO LANZAR" : "SIN PIEDRAS";
        }

        // 4. Arma HUD
        if (weapons != null)
        {
            if (weaponNameText != null)
                weaponNameText.text = $"ARMA: {weapons.CurrentWeaponDisplayName}";

            if (weaponHelpText != null)
                weaponHelpText.text = "1: FAROL  |  2: PUÑOS  |  3: CUCHILLO";
        }

        // 5. Progreso de Juguetes
        if (toys != null && lastCount != toys.Count && !IntroSequencePlaying)
        {
            lastCount = toys.Count;
            if (objective != null)
                objective.text = $"{lastCount}/{ToyCollector.RequiredToys}";

            string cardTitle = lastCount == 0 ? "OBJETIVO" : $"JUGUETE {lastCount} DE 3";
            string cardMsg = lastCount == 0
                ? "Recupera los 3 juguetes perdidos del panteón.\nAcércate a cada juguete para purificarlo y resistir las oleadas.\n\n<mark=#C8A05266><color=#493419> ESPACIO </color></mark> Lanzar piedra (6/6)\n<mark=#C8A05266><color=#493419> CLIC IZQ / J </color></mark> Atacar\n<mark=#C8A05266><color=#493419> 1: Farol · 2: Puños · 3: Cuchillo </color></mark> Cambiar arma"
                : lastCount == 1
                    ? "¡Has purificado el primer juguete!\nEl Cuchillo Grande (18 DMG) ha sido desbloqueado.\nBusca el segundo juguete."
                    : lastCount == 2
                        ? "Queda un último juguete.\nPrepárate para el encuentro final en el cementerio."
                        : "¡Tienes los 3 juguetes!\nDirígete a la puerta lateral oeste para escapar.";

            // Las notificaciones y pistas narrativas son gestionadas por Level2ClueSystem y ToyEncounterZone
            if (toyIcons != null)
            {
                for (int i = 0; i < toyIcons.Length; i++)
                {
                    if (toyIcons[i] != null)
                        toyIcons[i].color = toys.HasToy(i) ? Color.white : new Color(0.85f, 0.87f, 0.95f, 0.8f);
                }
            }
        }

        // 6. Tarjeta de notificación breve (Nota_Mensaje)
        if (noticeCard != null)
        {
            noticeCard.alpha = Mathf.MoveTowards(noticeCard.alpha, Time.time <= noticeUntil && !deathShown ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
        }

        // 7. Muerte y reinicio
        if (health.currentHealth <= 0 && !deathShown)
        {
            deathShown = true;
            PlayerDeathScreenUI.ShowDeathScreen();
        }
    }

    private void SetupSubHUDLayout()
    {
        // Buscar o crear contenedor de Piedras y Armas debajo de los juguetes
        Transform hudRoot = transform.Find("HUD");
        if (hudRoot == null) hudRoot = transform;

        TMP_FontAsset font = null;
        if (healthText != null) font = healthText.font;

        // Fila Piedras
        Transform piedrasObj = hudRoot.Find("Piedras_Row");
        if (piedrasObj == null)
        {
            GameObject pObj = new GameObject("Piedras_Row");
            pObj.transform.SetParent(hudRoot, false);
            RectTransform rt = pObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -96f);
            rt.sizeDelta = new Vector2(300f, 36f);

            GameObject countObj = new GameObject("PiedrasCount");
            countObj.transform.SetParent(rt, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0f, 1f);
            countRt.anchorMax = new Vector2(0f, 1f);
            countRt.pivot = new Vector2(0f, 1f);
            countRt.anchoredPosition = Vector2.zero;
            countRt.sizeDelta = new Vector2(240f, 18f);
            stonesCountText = countObj.AddComponent<TextMeshProUGUI>();
            if (font != null) stonesCountText.font = font;
            stonesCountText.fontSize = 12;
            stonesCountText.color = new Color(0.9f, 0.86f, 0.72f, 1f);
            stonesCountText.text = "PIEDRAS 6/6";

            GameObject helpObj = new GameObject("PiedrasHelp");
            helpObj.transform.SetParent(rt, false);
            RectTransform helpRt = helpObj.AddComponent<RectTransform>();
            helpRt.anchorMin = new Vector2(0f, 0f);
            helpRt.anchorMax = new Vector2(0f, 0f);
            helpRt.pivot = new Vector2(0f, 0f);
            helpRt.anchoredPosition = new Vector2(0f, 0f);
            helpRt.sizeDelta = new Vector2(240f, 16f);
            stonesHelpText = helpObj.AddComponent<TextMeshProUGUI>();
            if (font != null) stonesHelpText.font = font;
            stonesHelpText.fontSize = 9;
            stonesHelpText.color = new Color(0.72f, 0.68f, 0.58f, 0.85f);
            stonesHelpText.text = "ESPACIO LANZAR";
        }
        else
        {
            stonesCountText = piedrasObj.Find("PiedrasCount")?.GetComponent<TMP_Text>();
            stonesHelpText = piedrasObj.Find("PiedrasHelp")?.GetComponent<TMP_Text>();
        }

        // Fila Arma
        Transform armaObj = hudRoot.Find("Arma_Row");
        if (armaObj == null)
        {
            GameObject aObj = new GameObject("Arma_Row");
            aObj.transform.SetParent(hudRoot, false);
            RectTransform rt = aObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -142f);
            rt.sizeDelta = new Vector2(340f, 36f);

            GameObject nameObj = new GameObject("ArmaName");
            nameObj.transform.SetParent(rt, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = new Vector2(300f, 18f);
            weaponNameText = nameObj.AddComponent<TextMeshProUGUI>();
            if (font != null) weaponNameText.font = font;
            weaponNameText.fontSize = 11;
            weaponNameText.color = new Color(0.95f, 0.93f, 0.85f, 1f);
            weaponNameText.text = "ARMA: Puños";

            GameObject helpObj = new GameObject("ArmaHelp");
            helpObj.transform.SetParent(rt, false);
            RectTransform helpRt = helpObj.AddComponent<RectTransform>();
            helpRt.anchorMin = new Vector2(0f, 0f);
            helpRt.anchorMax = new Vector2(0f, 0f);
            helpRt.pivot = new Vector2(0f, 0f);
            helpRt.anchoredPosition = Vector2.zero;
            helpRt.sizeDelta = new Vector2(300f, 16f);
            weaponHelpText = helpObj.AddComponent<TextMeshProUGUI>();
            if (font != null) weaponHelpText.font = font;
            weaponHelpText.fontSize = 9;
            weaponHelpText.color = new Color(0.72f, 0.68f, 0.58f, 0.85f);
            weaponHelpText.text = "CLIC IZQ / J ATACAR  |  1·2·3 CAMBIAR";
        }
        else
        {
            weaponNameText = armaObj.Find("ArmaName")?.GetComponent<TMP_Text>();
            weaponHelpText = armaObj.Find("ArmaHelp")?.GetComponent<TMP_Text>();
        }
    }
}
