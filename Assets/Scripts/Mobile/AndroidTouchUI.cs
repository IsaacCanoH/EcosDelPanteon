using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Created only in an Android player. Nothing is attached to or saved in PC scenes.
[DefaultExecutionOrder(-500)]
public sealed class AndroidTouchUI : MonoBehaviour
{
    private RectTransform safeRoot;
    private CanvasGroup gameplay;
    private Canvas controlsCanvas;
    private GameObject hintButton;
    private GameObject pauseButton;
    private GameObject localPause;
    private TMP_Text stoneCount;
    private Texture2D atlas;
    private PlayerHealth player;
    private PlayerStoneThrow stones;
    private TopDownCharacterController movement;
    private Level2CharacterController level2Movement;
    private PlayerCoinSearch search;
    private LevelOneEnding level1;
    private Level4PauseMenu level4;
    private CoinSearchUI coinClues;
    private LloronaSpeechBubble speech;
    private IrisInNivel[] irises;
    private readonly List<AndroidTouchControl> gameplayControls = new List<AndroidTouchControl>();
    private bool gameplayVisible;
    private bool ownsPause;
    private bool previousAudio;
    private float previousScale;
    private float nextBind;
    private Rect previousSafe;
    private Vector2 previousScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (AndroidTouchInput.IsAndroid) SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!AndroidTouchInput.IsAndroid || mode == LoadSceneMode.Additive) return;
        AndroidTouchInput.ResetAll();
        new GameObject("Android_Touch_Runtime").AddComponent<AndroidTouchUI>();
    }

    private void Awake()
    {
        ConfigureTouchMenus();

        atlas = Resources.Load<Texture2D>("UI/Android/ControlsAtlas");
        if (atlas == null)
        {
            Debug.LogError("Falta UI/Android/ControlsAtlas para los controles Android.");
            enabled = false;
            return;
        }
        BuildControls();
        BindPlayer();
    }

    private void ConfigureTouchMenus()
    {
        var touch = GameInputUI.EnsureEventSystem().GetComponent<InputSystemUIInputModule>();
        touch.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
        touch.enabled = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void BuildControls()
    {
        GameObject canvasObject = new GameObject("Canvas_Android_Controls", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        controlsCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1672f, 941f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        safeRoot = CreateRect("Safe_Area", canvasObject.transform);
        Stretch(safeRoot);
        var group = CreateRect("Gameplay_Controls", safeRoot);
        Stretch(group);
        gameplay = group.gameObject.AddComponent<CanvasGroup>();

        // Positions, diameters and spacing taken from the approved 1672 x 941 reference.
        var joystick = Control("Joystick", group, Vector2.zero, new Vector2(178f, 185f), 268f, 0, true, AndroidTouchAction.Search);
        var thumb = CreateRect("Thumb", joystick.transform);
        thumb.sizeDelta = new Vector2(110f, 110f);
        var thumbFace = thumb.gameObject.AddComponent<AndroidControlImage>();
        thumbFace.atlas = atlas;
        thumbFace.tile = 1;
        thumbFace.raycastTarget = false;
        joystick.thumb = thumb;
        Control("Atacar", group, Vector2.right, new Vector2(-146f, 161f), 260f, 2, false, AndroidTouchAction.Attack);
        Control("Buscar", group, Vector2.right, new Vector2(-382f, 145f), 162f, 3, false, AndroidTouchAction.Search);
        var stone = Control("Piedra", group, Vector2.right, new Vector2(-258f, 340f), 143f, 4, false, AndroidTouchAction.Stone);
        stoneCount = Text("Cantidad", stone.transform, "6", 29f);
        stoneCount.rectTransform.anchorMin = stoneCount.rectTransform.anchorMax = new Vector2(0.76f, 0.76f);
        stoneCount.rectTransform.sizeDelta = new Vector2(44f, 36f);
        Control("Arma", group, Vector2.right, new Vector2(-94f, 405f), 138f, 5, false, AndroidTouchAction.Weapon);
        hintButton = Control("Pista", safeRoot, Vector2.one, new Vector2(-190f, -72f), 111f, 6, false, AndroidTouchAction.Clue).gameObject;
        pauseButton = Control("Pausa", safeRoot, Vector2.one, new Vector2(-72f, -66f), 103f, 7, false, AndroidTouchAction.Pause).gameObject;
        BuildFallbackPause();
        pauseButton.transform.SetAsLastSibling();
        UpdateSafeArea();
        SetGameplayVisible(false);
    }

    private AndroidTouchControl Control(string name, Transform parent, Vector2 anchor, Vector2 position,
        float diameter, int tile, bool joystick, AndroidTouchAction action)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * diameter;
        var face = rect.gameObject.AddComponent<AndroidControlImage>();
        face.atlas = atlas;
        face.tile = tile;
        var control = rect.gameObject.AddComponent<AndroidTouchControl>();
        control.action = action;
        control.joystick = joystick;
        if (action != AndroidTouchAction.Clue && action != AndroidTouchAction.Pause) gameplayControls.Add(control);
        return control;
    }

    private void BindPlayer()
    {
        player = FindObjectOfType<PlayerHealth>();
        if (player != null)
        {
            stones = player.GetComponent<PlayerStoneThrow>();
            movement = player.GetComponent<TopDownCharacterController>();
            level2Movement = player.GetComponent<Level2CharacterController>();
            search = player.GetComponent<PlayerCoinSearch>();
        }
        level1 = FindObjectOfType<LevelOneEnding>();
        level4 = FindObjectOfType<Level4PauseMenu>();
        coinClues = FindObjectOfType<CoinSearchUI>();
        speech = FindObjectOfType<LloronaSpeechBubble>();
        irises = FindObjectsOfType<IrisInNivel>();
    }

    private void Update()
    {
        UpdateSafeArea();
        if (Time.unscaledTime >= nextBind)
        {
            nextBind = Time.unscaledTime + 0.5f;
            BindPlayer();
        }
        RefreshControls(true);
    }

    private void LateUpdate() { RefreshControls(false); }

    private void RefreshControls(bool processPause)
    {
        if (gameplay == null) return;
        bool alive = player != null && player.currentHealth > 0 && !player.IsDead;
        bool ending = (level1 != null && level1.State != LevelOneEnding.EndingState.Playing && level1.State != LevelOneEnding.EndingState.Paused)
            || (level4 != null && level4.IsVictory)
            || (Level2VictoryManager.Instance != null && Level2VictoryManager.Instance.IsEnding);
        bool paused = ownsPause || (level1 != null && level1.State == LevelOneEnding.EndingState.Paused)
            || (level4 != null && level4.IsPaused);
        bool message = (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen)
            || (CinematicDialogueUI.Instance != null && CinematicDialogueUI.Instance.IsShowing)
            || (coinClues != null && coinClues.IsClueOpen) || (speech != null && speech.IsVisible);
        bool story = coinClues != null && coinClues.IsStoryOpen;
        // The story canvas (4100) blocks lower UI. Keep the existing PISTA control
        // visible and hittable above it; restore normal order after continuing.
        controlsCanvas.sortingOrder = story && !paused ? 4200 : 4000;
        AndroidTouchInput.DialogueOwnsClue = (CinematicDialogueUI.Instance != null && CinematicDialogueUI.Instance.IsShowing)
            || (speech != null && speech.IsVisible);
        if (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.topRightClueAccess != null)
            ParchmentMessageUI.Instance.topRightClueAccess.SetActive(false);
        bool opening = Level2HUD.IntroSequencePlaying;
        if (irises != null) foreach (var iris in irises)
            if (iris != null && iris.isActiveAndEnabled && !iris.IsComplete) opening = true;
        bool canMove = (movement != null && movement.isActiveAndEnabled)
            || (level2Movement != null && level2Movement.isActiveAndEnabled) || (search != null && search.IsSearching);
        bool playing = alive && !ending && !paused && !message && !opening && canMove && Time.timeScale > 0f;
        SetGameplayVisible(playing);
        hintButton.SetActive(alive && !ending && !paused && !opening && (playing || message));
        pauseButton.SetActive(alive && !ending && !message && !opening && (playing || paused));
        if (stoneCount != null) stoneCount.text = stones != null ? stones.RemainingStones.ToString() : "0";
        if (processPause && alive && !ending && !message && !opening && level1 == null && level4 == null &&
            AndroidTouchInput.WasPressed(AndroidTouchAction.Pause) && (playing || ownsPause))
            SetFallbackPaused(!ownsPause);
    }

    private void SetGameplayVisible(bool visible)
    {
        if (gameplayVisible && !visible)
        {
            foreach (var control in gameplayControls) control.Release();
            AndroidTouchInput.ReleaseGameplay();
        }
        gameplayVisible = visible;
        gameplay.alpha = visible ? 1f : 0f;
        gameplay.interactable = gameplay.blocksRaycasts = visible;
    }

    private void UpdateSafeArea()
    {
        Rect area = Screen.safeArea;
        Vector2 size = new Vector2(Screen.width, Screen.height);
        if (area == previousSafe && size == previousScreen) return;
        previousSafe = area;
        previousScreen = size;
        safeRoot.anchorMin = new Vector2(area.xMin / Mathf.Max(1f, size.x), area.yMin / Mathf.Max(1f, size.y));
        safeRoot.anchorMax = new Vector2(area.xMax / Mathf.Max(1f, size.x), area.yMax / Mathf.Max(1f, size.y));
    }

    // Levels 1 and 4 already own pause menus. Levels 2 and 3 need a touch-only pause card.
    private void BuildFallbackPause()
    {
        var modal = CreateRect("Pausa_Android_Niveles_2_3", safeRoot);
        Stretch(modal);
        localPause = modal.gameObject;
        modal.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.025f, 0.02f, 0.78f);
        var card = CreateRect("Tarjeta", modal);
        card.sizeDelta = new Vector2(600f, 390f);
        card.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.13f, 0.09f, 0.98f);
        var title = Text("Titulo", card, "PAUSA", 42f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 126f);
        title.rectTransform.sizeDelta = new Vector2(540f, 70f);
        MenuButton("Continuar", card, "CONTINUAR", 15f, () => SetFallbackPaused(false));
        MenuButton("Menu", card, "MENÚ PRINCIPAL", -93f, () =>
        {
            SetFallbackPaused(false);
            SceneManager.LoadScene("MenuPrincipal");
        });
        localPause.SetActive(false);
    }

    private void SetFallbackPaused(bool paused)
    {
        if (paused == ownsPause) return;
        if (paused)
        {
            previousScale = Time.timeScale;
            previousAudio = AudioListener.pause;
            AndroidTouchInput.ReleaseGameplay();
            if (search != null) search.InterruptSearch();
            if (player != null && player.TryGetComponent(out Rigidbody2D body)) body.velocity = Vector2.zero;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        else
        {
            Time.timeScale = previousScale;
            AudioListener.pause = previousAudio;
        }
        ownsPause = paused;
        localPause.SetActive(paused);
    }

    private static void MenuButton(string name, Transform parent, string label, float y, UnityEngine.Events.UnityAction callback)
    {
        var rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(480f, 86f);
        rect.anchoredPosition = new Vector2(0f, y);
        var face = rect.gameObject.AddComponent<Image>();
        face.color = new Color(0.35f, 0.29f, 0.17f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.onClick.AddListener(callback);
        var text = Text("Texto", rect, label, 27f);
        Stretch(text.rectTransform);
    }

    private static TMP_Text Text(string name, Transform parent, string value, float size)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.92f, 0.70f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelTouches();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) CancelTouches();
    }

    private void CancelTouches()
    {
        foreach (var control in GetComponentsInChildren<AndroidTouchControl>(true)) control.Release();
        AndroidTouchInput.ResetAll();
    }

    private void OnDestroy()
    {
        if (ownsPause) SetFallbackPaused(false);
        AndroidTouchInput.ResetAll();
    }
}
