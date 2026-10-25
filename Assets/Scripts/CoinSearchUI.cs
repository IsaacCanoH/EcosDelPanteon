using Cainos.PixelArtTopDown_Basic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-300)]
public class CoinSearchUI : MonoBehaviour
{
    public GoldCoinCollector collector;
    public GoldCoinPickup[] coins;
    public IrisInNivel opening;
    public CanvasGroup clueCard;
    public CanvasGroup clueBackdrop;
    public TMP_Text clueTitle;
    public TMP_Text clueText;
    public TMP_Text clueHint;
    public CanvasGroup searchPanel;
    public RectTransform searchRect;
    public TMP_Text searchText;
    public Image progressFill;
    public RectTransform canvasRect;
    public Camera worldCamera;

    private int lastClueStage = -1;
    private bool objectiveShown;
    private bool showingObjective;
    private int ClueStage { get { return collector.waveDirector != null
        ? collector.waveDirector.CompletedWaves : collector.collectedCoins; } }
    private bool WaveActive { get { return collector != null && collector.waveDirector != null && collector.waveDirector.IsWaveActive; } }
    private GoldCoinPickup target;
    private float progress;
    private bool searching;
    private bool clueOpen;
    private bool ownsPause;
    private float previousTimeScale;
    private bool previousAudioPause;
    private TopDownCharacterController movement;
    private PlayerCoinSearch playerSearch;
    private PlayerHealth health;
    private Rigidbody2D playerBody;
    private bool movementLocked;

    public bool IsClueOpen { get { return clueOpen; } }

    private void Awake()
    {
        if (collector != null)
        {
            health = collector.GetComponent<PlayerHealth>();
            movement = collector.GetComponent<TopDownCharacterController>();
            playerSearch = collector.GetComponent<PlayerCoinSearch>();
            playerBody = collector.GetComponent<Rigidbody2D>();
        }
        ConfigureGroup(clueCard);
        ConfigureGroup(clueBackdrop);
        ConfigureGroup(searchPanel);
    }

    private static void ConfigureGroup(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private bool CanRead()
    {
        return isActiveAndEnabled && collector != null &&
            !collector.IsLevelFinished &&
            (health == null || health.currentHealth > 0) &&
            (opening == null || !opening.isActiveAndEnabled);
    }

    private bool ReadyToOpen()
    {
        return CanRead() && Time.timeScale > 0f;
    }

    private void Update()
    {
        if (clueOpen)
        {
            if (!CanRead()) CloseClue();
            else if (GameControls.CluePressed) ToggleClue();
            return;
        }
        if (!ReadyToOpen()) return;
        if (!WaveActive && ClueStage != lastClueStage)
            ShowCurrentClue();
        else if (GameControls.CluePressed)
            ToggleClue();
    }

    public void ToggleClue()
    {
        if (clueOpen && showingObjective) ShowCurrentClue();
        else if (clueOpen) CloseClue();
        else ShowCurrentClue();
    }

    public void ShowCurrentClue()
    {
        if ((!clueOpen && !ReadyToOpen()) || !CanRead() ||
            clueCard == null || clueBackdrop == null ||
            clueTitle == null || clueText == null || coins == null)
            return;

        showingObjective = !objectiveShown && collector.waveDirector != null &&
            collector.collectedCoins == 0;
        if (showingObjective)
        {
            objectiveShown = true;
            clueTitle.text = "OBJETIVO";
            clueText.text = "Recupera las 3 monedas.\n" +
                "Cada una despierta una oleada.\n" +
                "Expúlsalos y revela otra pista.\n" +
                "<mark=#C8A05266><color=#493419> F </color></mark> Farol: 3 golpes por duende.";
        }
        else if (WaveActive)
        {
            clueTitle.text = "OLEADA " + collector.waveDirector.CurrentWave + " DE 3";
            clueText.text = collector.waveDirector.CurrentWave == 3
                ? "Has recuperado las tres monedas.\nExpulsa a los últimos duendes. Restan " + collector.waveDirector.RemainingEnemies + "."
                : "Expulsa a los duendes. Restan " + collector.waveDirector.RemainingEnemies +
                    ".\nLa siguiente pista se revela al terminar la oleada.";
        }
        else
        {
            GoldCoinPickup next = null;
            foreach (GoldCoinPickup coin in coins)
            {
                if (coin != null && coin.CanSearch(collector)) { next = coin; break; }
            }
            if (next == null) { CloseClue(); return; }
            clueTitle.text = "PISTA " + next.searchOrder + " DE " + collector.requiredCoins;
            clueText.text = next.clue;
        }
        if (!showingObjective) lastClueStage = ClueStage;
        if (!clueOpen)
        {
            // Primero soltar el bloqueo propio de la busqueda, despues adquirir el de la pista.
            if (playerSearch != null) playerSearch.InterruptSearch();
            movementLocked = movement != null && movement.enabled;
            if (movementLocked) movement.enabled = false;
            if (playerBody != null) playerBody.velocity = Vector2.zero;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsPause = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            clueOpen = true;
        }
        SetClueVisibility(true);
        if (searchPanel != null) searchPanel.alpha = 0f;
    }

    public void CloseClue()
    {
        showingObjective = false;
        bool wasOpen = clueOpen;
        clueOpen = false;
        SetClueVisibility(false);
        if (wasOpen && playerSearch != null) playerSearch.InterruptSearch();
        if (movementLocked && movement != null) movement.enabled = true;
        movementLocked = false;
        if (!ownsPause) return;
        ownsPause = false;
        // No sobrescribe una velocidad que otro sistema haya cambiado mientras se leia.
        if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
    }

    private void SetClueVisibility(bool visible)
    {
        if (clueCard != null)
        {
            clueCard.alpha = visible ? 1f : 0f;
            clueCard.blocksRaycasts = visible;
        }
        if (clueBackdrop != null)
        {
            clueBackdrop.alpha = visible ? 1f : 0f;
            clueBackdrop.blocksRaycasts = visible;
        }
        if (clueHint != null) clueHint.enabled = !visible && CanRead();
    }

    public void SetSearchTarget(GoldCoinPickup next, float amount, bool isSearching)
    {
        target = next;
        progress = Mathf.Clamp01(amount);
        searching = isSearching;
    }

    private void LateUpdate()
    {
        if (clueOpen && !CanRead()) CloseClue();
        if (!clueOpen && ReadyToOpen() && !WaveActive && ClueStage != lastClueStage)
            ShowCurrentClue();

        bool ready = ReadyToOpen() && !clueOpen;
        if (clueHint != null) clueHint.enabled = ready;
        bool showSearch = ready && target != null && target.CanSearch(collector);
        if (searchPanel != null) searchPanel.alpha = showSearch ? 1f : 0f;
        if (!showSearch) return;

        if (searchText != null)
            searchText.text = searching ? "BUSCANDO..." : "MANTÉN PARA BUSCAR";
        if (progressFill != null)
        {
            progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
            progressFill.rectTransform.offsetMin = Vector2.zero;
            progressFill.rectTransform.offsetMax = Vector2.zero;
        }
        if (searchRect == null || canvasRect == null || worldCamera == null)
            return;

        Vector3 screen = worldCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 1.25f);
        if (screen.z <= 0f)
        {
            if (searchPanel != null) searchPanel.alpha = 0f;
            return;
        }
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screen, null, out local);
        Vector2 half = searchRect.sizeDelta * 0.5f;
        Rect bounds = canvasRect.rect;
        local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 12f, bounds.xMax - half.x - 12f);
        local.y = Mathf.Clamp(local.y, bounds.yMin + half.y + 12f, bounds.yMax - half.y - 140f);
        searchRect.anchoredPosition = local;
    }

    private void OnDisable()
    {
        CloseClue();
        if (searchPanel != null) searchPanel.alpha = 0f;
        if (clueHint != null) clueHint.enabled = false;
    }
}
