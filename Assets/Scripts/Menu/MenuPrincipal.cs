using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Artwork and hit targets share one coordinate system, including on ultrawide
// displays. Background scenery fills the display; interactive art fits safely.
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class MenuPrincipal : MonoBehaviour
{
    public static MenuPrincipal Instance { get; private set; }
    [SerializeField] private Texture2D mainArt;
    [SerializeField] private Texture2D levelArt;
    [SerializeField] private Texture2D backdropArt;
    [SerializeField] private Texture2D lanternArt;
    [SerializeField] private TMP_FontAsset pixelFont;
    private Texture2D levelBackdropArt, mainLetteringArt, levelLetteringArt;
    private RawImage backdropImage;
    private readonly RawImage[] menuLettering = new RawImage[3];
    public bool IsLevelSelection { get; private set; }
    public bool IsCreditsVisible { get; private set; }
    public bool IsLoading { get; private set; }
    public int SelectedLevel { get; private set; }

    private static readonly Vector2 DesignSize = new Vector2(1672f, 941f);
    private static readonly string[] LevelScenes = {
        "Cementerio_Nivel1", "Cementerio_Nivel2", "Cementerio_Nivel3", "Cementerio_Nivel4"
    };
    private static readonly Rect[] MainBounds = {
        new Rect(82f,350f,510f,120f), new Rect(120f,510f,470f,104f),
        new Rect(120f,660f,470f,104f)
    };
    private static readonly Rect[] CardBounds = {
        new Rect(179f,191f,314f,565f), new Rect(510f,191f,314f,565f),
        new Rect(843f,191f,314f,565f), new Rect(1175f,191f,314f,565f)
    };
    private static readonly Rect BackBounds = new Rect(56f,808f,410f,112f);
    private static readonly Rect LaunchBounds = new Rect(1143f,808f,477f,112f);
    private static readonly Color Gold = new Color(1f,0.77f,0.19f,1f);
    private RectTransform canvasRect, artRoot, backdrop;
    private CanvasGroup mainGroup, levelsGroup, creditsGroup, markerGroup, loadingGroup;
    private MenuCreditsPanel creditsPanel;
    private RawImage marker, highlight;
    private Material highlightMaterial, letteringMaterial;
    private PixelMenuSelectionGraphic underline, cardFrame, footerFrame;
    private TMP_Text loadingText;
    private PixelMenuButton[] mainButtons, levelButtons;
    private PixelMenuButton creditsBackButton;
    private bool returningFromCredits;
    private PixelMenuButton focused;
    private Vector2 markerPosition, markerTarget, markerSize, previousCanvasSize;
    private bool markerReady, pressed, changingScreen;
    private Rect previousSafeArea;
    private EventSystem eventSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        eventSystem = GameInputUI.EnsureEventSystem();
        mainArt = mainArt != null ? mainArt : Resources.Load<Texture2D>("UI/Menu/MainMenu");
        levelArt = levelArt != null ? levelArt : Resources.Load<Texture2D>("UI/Menu/LevelSelect");
        backdropArt = backdropArt != null ? backdropArt : Resources.Load<Texture2D>("UI/Menu/Backdrop");
        lanternArt = lanternArt != null ? lanternArt : Resources.Load<Texture2D>("UI/Menu/Lantern");
        levelBackdropArt = Resources.Load<Texture2D>("UI/Menu/LevelBackdrop");
        // Render the original painted glyphs directly, without a regenerated outline.
        mainLetteringArt = mainArt;
        levelLetteringArt = levelArt;
        if (mainArt == null || levelArt == null || backdropArt == null || lanternArt == null ||
            levelBackdropArt == null || mainLetteringArt == null || levelLetteringArt == null)
        {
            Debug.LogError("Faltan las capas del menú pixel art.", this);
            enabled = false;
            return;
        }
        Transform preview = transform.Find("Preview");
        if (preview != null) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); }
        BuildInterface();
        LayoutInterface();
        mainGroup.alpha = 1f;
        levelsGroup.gameObject.SetActive(false);
        creditsGroup.gameObject.SetActive(false);
        Focus(mainButtons[0]);
    }

    private void Start()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (highlightMaterial != null) Destroy(highlightMaterial);
        if (letteringMaterial != null) Destroy(letteringMaterial);
    }

    private void Update()
    {
        if (artRoot == null) return;
        if (previousCanvasSize != canvasRect.rect.size || previousSafeArea != Screen.safeArea)
            LayoutInterface();
        if (!IsLoading && !changingScreen && GameControls.CancelPressed) ReturnToMain();
        AnimateSelection(Time.unscaledDeltaTime);
    }

    private void BuildInterface()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = true;
        canvasRect = (RectTransform)transform;
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        backdrop = NewRect("Backdrop", transform);
        var background = backdrop.gameObject.AddComponent<RawImage>();
        background.texture = backdropArt;
        background.raycastTarget = false;
        backdropImage = background;
        artRoot = NewRect("Menu_Artwork", transform);
        artRoot.sizeDelta = DesignSize;
        mainGroup = ScreenGroup("Inicio");
        levelsGroup = ScreenGroup("Niveles");
        var letteringShader = Resources.Load<Shader>("UI/Menu/MenuCutout");
        if (letteringShader != null)
            letteringMaterial = new Material(letteringShader) { hideFlags = HideFlags.DontSave };
        AddLettering("Logo",mainGroup.transform,mainLetteringArt,new Rect(36f,98f,610f,218f));
        menuLettering[0]=AddLettering("Jugar_Letra",mainGroup.transform,mainLetteringArt,new Rect(180f,350f,400f,120f));
        menuLettering[1]=AddLettering("Creditos_Letra",mainGroup.transform,mainLetteringArt,new Rect(140f,570f,440f,104f));
        Place(menuLettering[1].rectTransform,new Rect(140f,510f,440f,104f));
        menuLettering[2]=AddLettering("Salir_Letra",mainGroup.transform,mainLetteringArt,new Rect(180f,670f,320f,104f));
        Place(menuLettering[2].rectTransform,new Rect(180f,660f,320f,104f));
        AddLettering("Logo",levelsGroup.transform,levelLetteringArt,new Rect(70f,20f,315f,155f));
        AddLettering("Titulo",levelsGroup.transform,levelLetteringArt,new Rect(480f,68f,790f,130f));
        for(int i=0;i<4;i++) AddPlate("Capitulo_"+(i+1),levelsGroup.transform,CardBounds[i],PixelMenuPlate.Shape.Arch);
        AddPlate("Volver_Arte",levelsGroup.transform,BackBounds,PixelMenuPlate.Shape.Button);
        AddPlate("Jugar_Arte",levelsGroup.transform,LaunchBounds,PixelMenuPlate.Shape.Button);
        mainButtons = new PixelMenuButton[3];
        string[] mainNames = { "Jugar", "Creditos", "Salir" };
        for (int i=0;i<mainButtons.Length;i++) mainButtons[i] = AddButton(mainNames[i],mainGroup.transform,MainBounds[i],false,i);
        levelButtons = new PixelMenuButton[6];
        string[] levelNames = { "Nivel_1_Duende", "Nivel_2_Nino", "Nivel_3_Bruja", "Nivel_4_Llorona" };
        for (int i=0;i<4;i++) levelButtons[i] = AddButton(levelNames[i],levelsGroup.transform,CardBounds[i],true,i);
        levelButtons[4] = AddButton("Volver",levelsGroup.transform,BackBounds,true,4);
        levelButtons[5] = AddButton("Jugar_Nivel",levelsGroup.transform,LaunchBounds,true,5);
        creditsGroup = ScreenGroup("Creditos");
        var creditsLogo=AddLettering("Logo",creditsGroup.transform,mainLetteringArt,new Rect(36f,98f,610f,218f));
        Place(creditsLogo.rectTransform,new Rect(64f,26f,350f,125f));
        var creditsTitle=AddLettering("Titulo",creditsGroup.transform,mainLetteringArt,new Rect(140f,570f,440f,104f));
        Place(creditsTitle.rectTransform,new Rect(616f,42f,440f,104f));
        creditsTitle.color=Gold;
        creditsPanel=creditsGroup.gameObject.AddComponent<MenuCreditsPanel>();
        creditsPanel.Build(this,pixelFont);
        AddPlate("Volver_Arte",creditsGroup.transform,BackBounds,PixelMenuPlate.Shape.Button);
        creditsBackButton=AddButton("Volver_Creditos",creditsGroup.transform,BackBounds,false,0,true);
        SetNavigation();
        cardFrame = NewRect("Selected_Chapter",levelsGroup.transform).gameObject.AddComponent<PixelMenuSelectionGraphic>();
        cardFrame.shape = PixelMenuSelectionGraphic.Shape.Arch;
        cardFrame.raycastTarget = false;
        footerFrame = NewRect("Focused_Footer",artRoot).gameObject.AddComponent<PixelMenuSelectionGraphic>();
        footerFrame.shape = PixelMenuSelectionGraphic.Shape.Rectangle;
        footerFrame.raycastTarget = false;
        underline = NewRect("Selected_Option",mainGroup.transform).gameObject.AddComponent<PixelMenuSelectionGraphic>();
        underline.shape = PixelMenuSelectionGraphic.Shape.Underline;
        underline.raycastTarget = false;
        highlight = NewRect("Lantern_Light",artRoot).gameObject.AddComponent<RawImage>();
        highlight.raycastTarget = false;
        var shader = Resources.Load<Shader>("UI/Menu/MenuHighlight");
        if (shader != null)
        {
            highlightMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            highlight.material = highlightMaterial;
        }
        else highlight.gameObject.SetActive(false);
        var markerRoot = NewRect("Farol_Seleccion",artRoot);
        markerGroup = markerRoot.gameObject.AddComponent<CanvasGroup>();
        markerGroup.blocksRaycasts = false;
        markerGroup.interactable = false;
        marker = markerRoot.gameObject.AddComponent<RawImage>();
        marker.texture = lanternArt;
        marker.raycastTarget = false;
        var loadingRoot = NewRect("Cargando",transform);
        Stretch(loadingRoot);
        loadingGroup = loadingRoot.gameObject.AddComponent<CanvasGroup>();
        var shade = loadingRoot.gameObject.AddComponent<Image>();
        shade.color = new Color(0.006f,0.025f,0.028f,1f);
        var textRoot = NewRect("Estado",loadingRoot);
        textRoot.sizeDelta = new Vector2(1100f,120f);
        loadingText = textRoot.gameObject.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) loadingText.font = pixelFont;
        loadingText.fontSize = 24f;
        loadingText.enableAutoSizing = true;
        loadingText.fontSizeMin = 12f;
        loadingText.fontSizeMax = 24f;
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.color = new Color(1f,0.9f,0.66f);
        loadingText.raycastTarget = false;
        loadingRoot.gameObject.SetActive(false);
        loadingGroup.alpha = 0f;
    }

    private CanvasGroup ScreenGroup(string name)
    {
        var rect = NewRect(name,artRoot);
        Stretch(rect);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private RawImage AddLettering(string name, Transform parent, Texture2D texture, Rect bounds)
    {
        var rect=NewRect(name,parent);
        Place(rect,bounds);
        var image=rect.gameObject.AddComponent<RawImage>();
        image.texture=texture;
        image.uvRect=new Rect(bounds.x/DesignSize.x,1f-bounds.yMax/DesignSize.y,bounds.width/DesignSize.x,bounds.height/DesignSize.y);
        image.raycastTarget=false;
        image.material=letteringMaterial;
        return image;
    }

    private void AddPlate(string name, Transform parent, Rect bounds, PixelMenuPlate.Shape shape)
    {
        var rect=NewRect(name,parent);
        Place(rect,bounds);
        var plate=rect.gameObject.AddComponent<PixelMenuPlate>();
        plate.texture=levelArt;
        plate.sourceBounds=bounds;
        plate.shape=shape;
        plate.raycastTarget=false;
    }

    private PixelMenuButton AddButton(string name, Transform parent, Rect bounds, bool levelScreen, int index, bool creditsScreen=false)
    {
        var rect = NewRect(name,parent);
        Place(rect,bounds);
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var button = rect.gameObject.AddComponent<PixelMenuButton>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.Initialize(this,levelScreen,index,creditsScreen);
        button.onClick.AddListener(() => Activate(button));
        return button;
    }

    private void SetNavigation()
    {
        for (int i=0;i<mainButtons.Length;i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = mainButtons[(i+mainButtons.Length-1)%mainButtons.Length];
            nav.selectOnDown = mainButtons[(i+1)%mainButtons.Length];
            mainButtons[i].navigation = nav;
        }
        creditsBackButton.navigation=new Navigation { mode=Navigation.Mode.Explicit };
        for (int i=0;i<4;i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = levelButtons[(i+3)%4];
            nav.selectOnRight = levelButtons[(i+1)%4];
            nav.selectOnDown = levelButtons[5];
            levelButtons[i].navigation = nav;
        }
        var back = new Navigation { mode = Navigation.Mode.Explicit };
        back.selectOnRight = levelButtons[5];
        back.selectOnUp = levelButtons[0];
        levelButtons[4].navigation = back;
        var play = new Navigation { mode = Navigation.Mode.Explicit };
        play.selectOnLeft = levelButtons[4];
        play.selectOnUp = levelButtons[0];
        levelButtons[5].navigation = play;
    }

    private void LayoutInterface()
    {
        Canvas.ForceUpdateCanvases();
        Vector2 size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f) size = new Vector2(Screen.width,Screen.height);
        Rect safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f,0f,Screen.width,Screen.height);
        float px = size.x / Mathf.Max(1f,Screen.width);
        float py = size.y / Mathf.Max(1f,Screen.height);
        Vector2 available = new Vector2(safe.width*px,safe.height*py);
        float fit = Mathf.Min(available.x/DesignSize.x,available.y/DesignSize.y);
        artRoot.localScale = Vector3.one*fit;
        artRoot.anchoredPosition = new Vector2(safe.center.x*px-size.x*0.5f,safe.center.y*py-size.y*0.5f);
        Texture backgroundTexture = backdropImage.texture;
        float cover = Mathf.Max(size.x/backgroundTexture.width,size.y/backgroundTexture.height);
        backdrop.sizeDelta = new Vector2(backgroundTexture.width*cover,backgroundTexture.height*cover);
        previousCanvasSize = size;
        previousSafeArea = Screen.safeArea;
        if (loadingText != null) loadingText.rectTransform.sizeDelta = new Vector2(Mathf.Min(1100f,available.x*0.9f),120f);
    }

    public void Focus(PixelMenuButton button)
    {
        if (!CanUse(button)) return;
        bool changed = focused != button;
        focused = button;
        pressed = false;
        if (!IsLevelSelection && !IsCreditsVisible)
            for(int i=0;i<menuLettering.Length;i++) menuLettering[i].color=i==button.Index ? Gold : Color.white;
        if (IsLevelSelection && button.Index < 4)
        {
            SelectedLevel = button.Index;
            Place(cardFrame.rectTransform,CardBounds[SelectedLevel]);
            for (int i=4;i<6;i++)
            {
                var nav = levelButtons[i].navigation;
                nav.selectOnUp = levelButtons[SelectedLevel];
                levelButtons[i].navigation = nav;
            }
        }
        UpdateSelectionTargets();
        if (changed && eventSystem != null && eventSystem.currentSelectedGameObject != button.gameObject)
            eventSystem.SetSelectedGameObject(button.gameObject);
    }

    public void Press(PixelMenuButton button, bool down)
    {
        if (IsLoading || changingScreen) return;
        if (down) Focus(button);
        pressed = down && button == focused;
    }

    private void UpdateSelectionTargets()
    {
        if (focused == null) return;
        Rect area;
        if (IsCreditsVisible)
        {
            area=BackBounds;
            markerTarget=ArtPoint(new Vector2(area.x+39f,area.center.y));
            markerSize=new Vector2(58f,87f);
            SetHighlight(levelArt,area);
        }
        else if (!IsLevelSelection)
        {
            area = MainBounds[focused.Index];
            float y = focused.Index == 0 ? 405f : area.y+43f;
            float markerX = focused.Index==1 ? 110f : 137f;
            markerTarget = ArtPoint(new Vector2(markerX,y));
            markerSize = new Vector2(100f,150f);
            Place(underline.rectTransform,new Rect(188f,y+44f,326f,12f));
            highlight.gameObject.SetActive(false);
        }
        else if (focused.Index < 4)
        {
            area = CardBounds[focused.Index];
            markerTarget = ArtPoint(new Vector2(area.center.x,756f));
            markerSize = new Vector2(70f,105f);
            highlight.gameObject.SetActive(false);
        }
        else
        {
            area = focused.Index == 4 ? BackBounds : LaunchBounds;
            markerTarget = ArtPoint(new Vector2(area.x+39f,area.center.y));
            markerSize = new Vector2(58f,87f);
            SetHighlight(levelArt,area);
        }
        bool footerFocused=IsCreditsVisible || (IsLevelSelection && focused.Index >= 4);
        footerFrame.gameObject.SetActive(footerFocused);
        if (footerFocused) Place(footerFrame.rectTransform,area);
        if (!markerReady) { markerPosition = markerTarget; markerReady = true; }
    }

    private void SetHighlight(Texture2D texture, Rect bounds)
    {
        if (highlightMaterial == null) return;
        highlight.gameObject.SetActive(true);
        highlight.texture = texture;
        highlight.uvRect = new Rect(bounds.x/DesignSize.x,1f-bounds.yMax/DesignSize.y,bounds.width/DesignSize.x,bounds.height/DesignSize.y);
        Place(highlight.rectTransform,bounds);
    }

    private void AnimateSelection(float delta)
    {
        if (marker == null || !markerReady) return;
        markerPosition = Vector2.Lerp(markerPosition,markerTarget,1f-Mathf.Exp(-24f*delta));
        float time = Time.unscaledTime;
        float bob = Mathf.Round(Mathf.Sin(time*2.2f)*0.8f)*4f;
        marker.rectTransform.anchoredPosition = new Vector2(Mathf.Round(markerPosition.x),Mathf.Round(markerPosition.y+bob));
        marker.rectTransform.sizeDelta = markerSize*(pressed ? 0.94f : 1f);
        float flicker = 0.88f+Mathf.Sin(time*5.3f)*0.06f+Mathf.Sin(time*12.7f)*0.025f;
        marker.color = new Color(1f,1f,1f,flicker);
        highlight.color = new Color(1f,0.66f,0.08f,pressed ? 0.72f : 0.22f+flicker*0.13f);
        cardFrame.color = new Color(1f,0.89f,0.14f,0.98f+Mathf.Sin(time*2.4f)*0.02f);
        footerFrame.color = Gold;
        underline.color = new Color(Gold.r,Gold.g,Gold.b,flicker);
    }

    private void Activate(PixelMenuButton button)
    {
        if (!CanUse(button)) return;
        Focus(button);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (IsCreditsVisible) { ReturnToMain(); return; }
        if (!IsLevelSelection)
        {
            switch (button.Index)
            {
                case 0: StartCoroutine(ChangeScreen(true)); break;
                case 1: ShowCredits(); break;
                case 2: SalirDelJuego(); break;
            }
        }
        else if (button.Index == 4) ReturnToMain();
        else if (button.Index == 5) StartSelectedLevel();
    }

    public void ReturnToMain()
    {
        if ((!IsLevelSelection && !IsCreditsVisible) || IsLoading || changingScreen) return;
        returningFromCredits=IsCreditsVisible;
        StartCoroutine(ChangeScreen(false));
    }

    public void ShowCredits()
    {
        if (IsCreditsVisible || IsLevelSelection || IsLoading || changingScreen) return;
        StartCoroutine(ChangeScreen(false,true));
    }

    private bool CanUse(PixelMenuButton button)
    {
        return button!=null && button.gameObject.activeInHierarchy && !IsLoading && !changingScreen &&
            button.CreditsScreen==IsCreditsVisible && button.LevelScreen==IsLevelSelection;
    }

    private IEnumerator ChangeScreen(bool toLevels, bool toCredits=false)
    {
        changingScreen = true;
        pressed = false;
        mainGroup.blocksRaycasts = levelsGroup.blocksRaycasts = creditsGroup.blocksRaycasts = false;
        CanvasGroup incoming = toCredits ? creditsGroup : toLevels ? levelsGroup : mainGroup;
        CanvasGroup outgoing = IsCreditsVisible ? creditsGroup : IsLevelSelection ? levelsGroup : mainGroup;
        incoming.gameObject.SetActive(true);
        outgoing.gameObject.SetActive(true);
        footerFrame.gameObject.SetActive(false);
        backdropImage.texture = toLevels || toCredits ? levelBackdropArt : backdropArt;
        LayoutInterface();
        incoming.alpha = 0f;
        markerGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed/0.18f);
            float eased = 1f-(1f-t)*(1f-t);
            incoming.alpha = eased;
            outgoing.alpha = 1f-eased;
            yield return null;
        }
        incoming.alpha = 1f;
        outgoing.alpha = 0f;
        outgoing.gameObject.SetActive(false);
        IsLevelSelection = toLevels;
        IsCreditsVisible = toCredits;
        if(toCredits) creditsPanel.ResetToTop();
        changingScreen = false;
        incoming.blocksRaycasts = true;
        focused = null;
        markerReady = false;
        markerGroup.alpha = 1f;
        Focus(toCredits ? creditsBackButton : toLevels ? levelButtons[SelectedLevel] : mainButtons[returningFromCredits?1:0]);
        returningFromCredits=false;
    }

    public void StartSelectedLevel()
    {
        if (!IsLevelSelection || IsLoading || changingScreen) return;
        if (SelectedLevel < 0 || SelectedLevel >= LevelScenes.Length) return;
        string scene = LevelScenes[SelectedLevel];
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            StartCoroutine(ShowLoadError());
            Debug.LogWarning("No se encuentra la escena del nivel: "+scene,this);
            return;
        }
        IsLoading = true;
        StartCoroutine(LoadLevel(scene));
    }

    private IEnumerator LoadLevel(string scene)
    {
        loadingGroup.gameObject.SetActive(true);
        loadingText.text = "ENTRANDO AL PANTEÓN...";
        mainGroup.blocksRaycasts = levelsGroup.blocksRaycasts = false;
        for (float elapsed=0f;elapsed<0.18f;elapsed+=Time.unscaledDeltaTime)
        {
            loadingGroup.alpha = Mathf.Clamp01(elapsed/0.18f);
            yield return null;
        }
        loadingGroup.alpha = 1f;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AsyncOperation operation = SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
        if (operation == null)
        {
            IsLoading = false;
            levelsGroup.blocksRaycasts = true;
            yield return ShowLoadError();
            yield break;
        }
        while (!operation.isDone) yield return null;
    }

    private IEnumerator ShowLoadError()
    {
        loadingGroup.gameObject.SetActive(true);
        loadingGroup.alpha = 0.94f;
        loadingText.text = "NO SE PUDO ABRIR EL NIVEL.\nINTÉNTALO DE NUEVO.";
        yield return new WaitForSecondsRealtime(1.8f);
        loadingGroup.gameObject.SetActive(false);
    }

    public void SalirDelJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f,0.5f);
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Vector2 ArtPoint(Vector2 p)
    {
        return new Vector2(p.x-DesignSize.x*0.5f,DesignSize.y*0.5f-p.y);
    }
    private static void Place(RectTransform rect, Rect bounds)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0.5f);
        rect.sizeDelta = bounds.size;
        rect.anchoredPosition = ArtPoint(bounds.center);
    }
}
