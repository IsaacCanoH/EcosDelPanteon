using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Built on demand by the level 1 clue sequence; never created in its epilogue.
public sealed class LevelOneStoryIntro : MonoBehaviour
{
    public const string Story = "Hace años, una mujer acudió a una bruja con la esperanza de volver a ver a sus tres hijos. Para realizar el ritual, le entregó algunos de sus juguetes y objetos personales.\n\n" +
        "Pero algo salió mal. Desde aquella noche, el panteón dejó de ser un lugar tranquilo. Los duendes comenzaron a rondar entre las tumbas y los lamentos de la mujer se escuchaban al caer el sol. Con el tiempo, la gente empezó a llamarla la Llorona.\n\n" +
        "Las pertenencias de sus hijos siguen dentro del panteón, protegidas por seres que no dejan que nadie se acerque.\n\n" +
        "Esta noche has cruzado sus puertas. Con un farol para alumbrar el camino, tendrás que seguir las pistas, recuperar los objetos y descubrir qué impide que el lugar vuelva a estar en calma.";

    private RectTransform artwork;
    private CanvasGroup visibility;
    private Button continueButton;
    private Rect lastSafeArea;
    private Vector2 lastScreen;

    public static LevelOneStoryIntro Create(TMP_FontAsset font, Action advance)
    {
        Texture2D paper = Resources.Load<Texture2D>("UI/Story/LevelOneIntroduction");
        if (paper == null || font == null)
        {
            Debug.LogError("Falta el pergamino o la tipografía de la introducción del nivel 1.");
            return null;
        }
        var root = new GameObject("Level1_Story_Introduction", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var intro = root.AddComponent<LevelOneStoryIntro>();
        intro.Build(paper, font, advance);
        return intro;
    }

    private void Build(Texture2D paper, TMP_FontAsset font, Action advance)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4100;
        canvas.pixelPerfect = true;
        visibility = gameObject.AddComponent<CanvasGroup>();
        var scrim = Rect("Fondo_Pausa", transform);
        scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
        scrim.offsetMin = scrim.offsetMax = Vector2.zero;
        scrim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
        artwork = Rect("Pergamino_Historia", transform);
        artwork.sizeDelta = new Vector2(1254f, 715f);
        var image = artwork.gameObject.AddComponent<RawImage>();
        image.texture = paper;
        image.raycastTarget = false;
        Text("Titulo", font, "ECOS DEL PANTEÓN", 36f, new Rect(190f, 28f, 900f, 58f), TextAlignmentOptions.Center);
        Text("Subtitulo", font, "EL ORIGEN", 24f, new Rect(410f, 85f, 420f, 38f), TextAlignmentOptions.Center);
        Rule("Linea_Izquierda", new Rect(350f, 102f, 105f, 2f));
        Rule("Linea_Derecha", new Rect(785f, 102f, 120f, 2f));
        var body = Text("Historia", font, Story, 14.5f, new Rect(264f, 136f, 790f, 400f), TextAlignmentOptions.TopLeft);
        body.fontStyle = FontStyles.Bold;
        body.characterSpacing = -0.35f;
        body.enableAutoSizing = true;
        body.fontSizeMin = 13.5f;
        body.fontSizeMax = 14.5f;
        body.lineSpacing = 31f;
        body.paragraphSpacing = 12f;
        Text("Estado_Pausa", font, "JUEGO EN PAUSA", 19f, new Rect(75f, 651f, 450f, 34f), TextAlignmentOptions.Left);
        var action = Rect("Continuar_Introduccion", artwork);
        Box(action, new Rect(900f, 634f, 304f, 68f));
        var hit = action.gameObject.AddComponent<Image>();
        hit.color = Color.white;
        continueButton = action.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = hit;
        var colors = continueButton.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(0.34f, 0.25f, 0.12f, 0.035f);
        colors.selectedColor = Color.clear;
        colors.pressedColor = new Color(0.34f, 0.25f, 0.12f, 0.22f);
        colors.fadeDuration = 0f;
        continueButton.colors = colors;
        continueButton.onClick.AddListener(() => advance());
        var navigation = continueButton.navigation;
        navigation.mode = Navigation.Mode.None;
        continueButton.navigation = navigation;
        if (!AndroidTouchInput.IsAndroid)
        {
            // Same layers, proportions and colors as Tecla_Continuar in the clue card,
            // enlarged uniformly from its 34 x 30 logical pixels by 1.5.
            Rule("Tecla_Sombra", new Rect(911f, 644.5f, 51f, 45f), new Color(0.16f, 0.12f, 0.07f, 0.95f));
            Rule("Tecla_Borde", new Rect(911f, 640f, 51f, 45f), new Color(0.68f, 0.51f, 0.23f));
            Rule("Tecla_Cara", new Rect(914f, 641.5f, 45f, 39f), new Color(1f, 0.90f, 0.61f));
            Text("Letra_P", font, "P", 27f, new Rect(915.5f, 638.5f, 42f, 45f), TextAlignmentOptions.Center)
                .color = new Color(0.22f, 0.16f, 0.08f);
        }
        Text("Accion_Continuar", font, "CONTINUAR", 20f,
            AndroidTouchInput.IsAndroid ? new Rect(925f, 651f, 270f, 34f) : new Rect(980f, 651f, 230f, 34f), TextAlignmentOptions.Left);
        Layout();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        visibility.alpha = visible ? 1f : 0f;
        visibility.interactable = visibility.blocksRaycasts = visible;
        if (visible && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        else if (!visible && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == continueButton.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update() { Layout(); }

    private void Layout()
    {
        Rect safe = Screen.safeArea;
        var size = new Vector2(Screen.width, Screen.height);
        if (safe == lastSafeArea && size == lastScreen) return;
        lastSafeArea = safe; lastScreen = size;
        artwork.anchoredPosition = safe.center - size * 0.5f;
        float scale = Mathf.Min(safe.width * 0.9f / 1254f, safe.height * 0.76f / 715f);
        artwork.localScale = Vector3.one * scale;
    }

    private TMP_Text Text(string name, TMP_FontAsset font, string value, float size, Rect box, TextAlignmentOptions alignment)
    {
        var rect = Rect(name, artwork);
        Box(rect, box);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size;
        text.color = new Color(0.22f, 0.17f, 0.10f);
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private void Rule(string name, Rect box, Color? color = null)
    {
        var rect = Rect(name, artwork); Box(rect, box);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color ?? new Color(0.40f, 0.31f, 0.16f);
        image.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Box(RectTransform rect, Rect box)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(box.x, -box.y);
        rect.sizeDelta = box.size;
    }
}
