using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuPrincipal : MonoBehaviour
{
    public static MenuPrincipal Instance { get; private set; }

    [Header("Mensajes de Estado")]
    public TMP_Text mensajeEstado;

    [Header("Título Principal")]
    public TMP_Text tituloTexto;
    public TMP_Text subtituloTexto;

    [Header("Panel de Opciones")]
    public GameObject panelOpciones;
    public Slider sliderGeneral;
    public Slider sliderEnemigos;
    public Slider sliderMusica;
    public TMP_Text textoValorGeneral;
    public TMP_Text textoValorEnemigos;
    public TMP_Text textoValorMusica;

    [Header("Botones de Opciones")]
    public Button botonAbrirOpciones;
    public Button botonCerrarOpciones;
    public Button botonRestablecerOpciones;
    public Button botonSalirJuego;

    [Header("Badges Informativos de Niveles")]
    public TMP_Text badgeDuende;
    public TMP_Text badgeNino;
    public TMP_Text badgeBruja;
    public TMP_Text badgeLlorona;

    private bool cargando;
    private bool opcionesAbiertas;
    private float ultimoPreviewEnemigo = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        if (mensajeEstado == null)
        {
            var msgGo = GameObject.Find("MensajeEstado");
            if (msgGo != null) mensajeEstado = msgGo.GetComponent<TMP_Text>();
        }

        LimpiarMensaje();
        ConstruirOpcionesSiFaltan();
        InicializarValoresAudio();
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuMusic();
        }

        ActualizarTextosValores();
    }

    private void Update()
    {
        // Atajo para abrir/cerrar opciones con la tecla O o Escape
        if (Input.GetKeyDown(KeyCode.O))
        {
            ToggleOpciones();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (opcionesAbiertas)
            {
                CerrarOpciones();
            }
        }
    }

    public void AbrirNivel(string nombreEscena)
    {
        if (cargando || opcionesAbiertas)
            return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            MostrarMensaje("Revisa la escena en Build Settings");
            Debug.LogWarning("No se puede cargar la escena: " + nombreEscena, this);
            return;
        }

        cargando = true;
        MostrarMensaje("Entrando al panteón...");
        SceneManager.LoadScene(nombreEscena);
    }

    public void MostrarProximamente()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        MostrarMensaje("¡Próximamente en la siguiente actualización!");
    }

    public void AbrirOpciones()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        opcionesAbiertas = true;
        if (panelOpciones != null)
            panelOpciones.SetActive(true);

        InicializarValoresAudio();
    }

    public void CerrarOpciones()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        opcionesAbiertas = false;
        if (panelOpciones != null)
            panelOpciones.SetActive(false);

        MostrarMensaje("Ajustes de audio guardados");
    }

    public void ToggleOpciones()
    {
        if (opcionesAbiertas) CerrarOpciones();
        else AbrirOpciones();
    }

    public void RestablecerValoresDefault()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (sliderGeneral != null) sliderGeneral.value = 1.0f;
        if (sliderEnemigos != null) sliderEnemigos.value = 0.8f;
        if (sliderMusica != null) sliderMusica.value = 0.7f;

        OnGeneralSliderChanged(1.0f);
        OnEnemigosSliderChanged(0.8f);
        OnMusicaSliderChanged(0.7f);

        MostrarMensaje("Valores de audio restablecidos");
    }

    public void SalirDelJuego()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void InicializarValoresAudio()
    {
        float master = AudioManager.MasterVolume;
        float enemy = AudioManager.EnemyVolume;
        float music = AudioManager.MusicVolume;

        if (sliderGeneral != null)
        {
            sliderGeneral.onValueChanged.RemoveListener(OnGeneralSliderChanged);
            sliderGeneral.value = master;
            sliderGeneral.onValueChanged.AddListener(OnGeneralSliderChanged);
        }

        if (sliderEnemigos != null)
        {
            sliderEnemigos.onValueChanged.RemoveListener(OnEnemigosSliderChanged);
            sliderEnemigos.value = enemy;
            sliderEnemigos.onValueChanged.AddListener(OnEnemigosSliderChanged);
        }

        if (sliderMusica != null)
        {
            sliderMusica.onValueChanged.RemoveListener(OnMusicaSliderChanged);
            sliderMusica.value = music;
            sliderMusica.onValueChanged.AddListener(OnMusicaSliderChanged);
        }

        ActualizarTextosValores();
    }

    private void OnGeneralSliderChanged(float valor)
    {
        AudioManager.MasterVolume = valor;
        ActualizarTextosValores();
    }

    private void OnEnemigosSliderChanged(float valor)
    {
        AudioManager.EnemyVolume = valor;
        ActualizarTextosValores();

        // Reproducir preview con cooldown para no saturar mientras desliza
        if (Time.unscaledTime > ultimoPreviewEnemigo + 0.35f)
        {
            ultimoPreviewEnemigo = Time.unscaledTime;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayPreviewEnemySound();
        }
    }

    private void OnMusicaSliderChanged(float valor)
    {
        AudioManager.MusicVolume = valor;
        ActualizarTextosValores();
    }

    private void ActualizarTextosValores()
    {
        if (textoValorGeneral != null)
            textoValorGeneral.text = $"{Mathf.RoundToInt(AudioManager.MasterVolume * 100f)}%";

        if (textoValorEnemigos != null)
            textoValorEnemigos.text = $"{Mathf.RoundToInt(AudioManager.EnemyVolume * 100f)}%";

        if (textoValorMusica != null)
            textoValorMusica.text = $"{Mathf.RoundToInt(AudioManager.MusicVolume * 100f)}%";
    }

    private void MostrarMensaje(string texto)
    {
        if (mensajeEstado == null)
            return;

        mensajeEstado.text = texto;
        CancelInvoke(nameof(LimpiarMensaje));
        Invoke(nameof(LimpiarMensaje), 3.0f);
    }

    private void LimpiarMensaje()
    {
        if (mensajeEstado != null)
            mensajeEstado.text = "";
    }

    /// <summary>
    /// Construye y conecta proceduralmente los elementos de UI requeridos si la escena aún no los tiene.
    /// </summary>
    public void ConstruirOpcionesSiFaltan()
    {
        Transform areaMenu = transform.Find("AreaMenu");
        if (areaMenu == null)
        {
            var amGo = GameObject.Find("AreaMenu");
            if (amGo != null) areaMenu = amGo.transform;
        }
        if (areaMenu == null) return;

        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
#if UNITY_EDITOR
        if (pixelFont == null)
        {
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
        }
#endif

        // 1. Mejorar el Título del juego
        Transform tituloTr = areaMenu.Find("Titulo");
        if (tituloTr != null)
        {
            tituloTexto = tituloTr.GetComponent<TMP_Text>();
            if (tituloTexto != null)
            {
                tituloTexto.text = "ECOS DEL PANTEÓN";
                tituloTexto.fontSize = 42;
                tituloTexto.color = new Color(0.98f, 0.90f, 0.62f, 1f); // Oro brillante
                if (pixelFont != null) tituloTexto.font = pixelFont;
                tituloTexto.alignment = TextAlignmentOptions.Center;

                // Crear subtítulo estilizado si no existe
                Transform subTr = tituloTr.Find("Subtitulo");
                if (subTr == null)
                {
                    GameObject subGo = new GameObject("Subtitulo");
                    subGo.transform.SetParent(tituloTr, false);
                    RectTransform srt = subGo.AddComponent<RectTransform>();
                    srt.anchorMin = new Vector2(0f, 0f);
                    srt.anchorMax = new Vector2(1f, 0f);
                    srt.anchoredPosition = new Vector2(0f, -24f);
                    srt.sizeDelta = new Vector2(0f, 26f);

                    subtituloTexto = subGo.AddComponent<TextMeshProUGUI>();
                    subtituloTexto.text = "— CAMINOS CRUZADOS —";
                    subtituloTexto.fontSize = 15;
                    subtituloTexto.color = new Color(0.78f, 0.72f, 0.62f, 1f); // Plata dorada
                    if (pixelFont != null) subtituloTexto.font = pixelFont;
                    subtituloTexto.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    subtituloTexto = subTr.GetComponent<TMP_Text>();
                }
            }
        }

        // 2. Decorar e identificar los 4 cuadrantes con badges claros
        ActualizarBadgeCuadrante(areaMenu, "Boton_Duende", ref badgeDuende, "NIVEL 1: EL DUENDE", "DISPONIBLE · CLIC PARA JUGAR", new Color(0.48f, 0.98f, 0.58f, 1f));
        ActualizarBadgeCuadrante(areaMenu, "Boton_Nino", ref badgeNino, "NIVEL 2: NIÑO ESPECTRAL", "DISPONIBLE · CLIC PARA JUGAR", new Color(0.48f, 0.85f, 0.98f, 1f));
        ActualizarBadgeCuadrante(areaMenu, "Boton_Bruja", ref badgeBruja, "NIVEL 3: LA BRUJA", "BLOQUEADO · PRÓXIMAMENTE", new Color(0.75f, 0.65f, 0.92f, 0.8f));
        ActualizarBadgeCuadrante(areaMenu, "Boton_Llorona", ref badgeLlorona, "NIVEL 4: LA LLORONA", "BLOQUEADO · PRÓXIMAMENTE", new Color(0.95f, 0.65f, 0.75f, 0.8f));

        // 3. Crear Barra Inferior con Botón de Opciones y Salir
        Transform barraTr = areaMenu.Find("Barra_Inferior");
        if (barraTr == null)
        {
            GameObject barraGo = new GameObject("Barra_Inferior");
            barraGo.transform.SetParent(areaMenu, false);
            RectTransform brt = barraGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 38f);
            brt.sizeDelta = new Vector2(1400f, 60f);

            // Botón Opciones (Izquierda de la barra)
            GameObject btnOpGo = CrearBotonEstilizado("Boton_Opciones", barraGo.transform, new Vector2(-280f, 0f), new Vector2(240f, 44f), "⚙ OPCIONES [O]", pixelFont, new Color(0.22f, 0.20f, 0.28f, 0.92f), new Color(0.95f, 0.88f, 0.65f, 1f));
            botonAbrirOpciones = btnOpGo.GetComponent<Button>();
            botonAbrirOpciones.onClick.AddListener(AbrirOpciones);

            // Botón Salir (Derecha de la barra)
            GameObject btnSalGo = CrearBotonEstilizado("Boton_Salir", barraGo.transform, new Vector2(280f, 0f), new Vector2(240f, 44f), "✕ SALIR [ESC]", pixelFont, new Color(0.28f, 0.16f, 0.18f, 0.92f), new Color(0.95f, 0.70f, 0.70f, 1f));
            botonSalirJuego = btnSalGo.GetComponent<Button>();
            botonSalirJuego.onClick.AddListener(SalirDelJuego);
        }

        // 4. Crear Panel Modal de Opciones
        Transform panelTr = areaMenu.Find("Panel_Opciones");
        if (panelTr == null)
        {
            panelOpciones = CrearModalOpciones(areaMenu, pixelFont);
        }
        else
        {
            panelOpciones = panelTr.gameObject;
        }

        if (panelOpciones != null)
        {
            panelOpciones.SetActive(false);
        }
    }

    private void ActualizarBadgeCuadrante(Transform parent, string nombreBoton, ref TMP_Text targetText, string titulo, string estado, Color colorAcento)
    {
        Transform btnTr = parent.Find(nombreBoton);
        if (btnTr == null) return;

        // Ocultar texto por defecto "Button" si existía
        Transform textoDefault = btnTr.Find("Text (TMP)");
        if (textoDefault != null)
        {
            textoDefault.gameObject.SetActive(false);
        }

        Transform badgeTr = btnTr.Find("Badge_Nivel");
        if (badgeTr == null)
        {
            GameObject badgeGo = new GameObject("Badge_Nivel");
            badgeGo.transform.SetParent(btnTr, false);
            RectTransform rt = badgeGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(380f, 75f);

            Image bg = badgeGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.09f, 0.85f);
            bg.raycastTarget = false;

            // Borde superior de acento
            GameObject bordeGo = new GameObject("Borde_Acento");
            bordeGo.transform.SetParent(badgeGo.transform, false);
            RectTransform brt = bordeGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(0f, -1f);
            brt.sizeDelta = new Vector2(0f, 3f);
            Image bImg = bordeGo.AddComponent<Image>();
            bImg.color = colorAcento;
            bImg.raycastTarget = false;

            // Texto de título del nivel
            GameObject txtGo = new GameObject("Texto_Titulo");
            txtGo.transform.SetParent(badgeGo.transform, false);
            RectTransform trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.45f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(0f, -4f);
            trt.sizeDelta = new Vector2(-16f, 0f);

            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = titulo;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = colorAcento;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            targetText = tmp;

            // Texto de estado
            GameObject estGo = new GameObject("Texto_Estado");
            estGo.transform.SetParent(badgeGo.transform, false);
            RectTransform ert = estGo.AddComponent<RectTransform>();
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(1f, 0.45f);
            ert.anchoredPosition = new Vector2(0f, 3f);
            ert.sizeDelta = new Vector2(-16f, 0f);

            TextMeshProUGUI tmpEst = estGo.AddComponent<TextMeshProUGUI>();
            tmpEst.text = estado;
            tmpEst.fontSize = 10;
            tmpEst.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            tmpEst.alignment = TextAlignmentOptions.Center;
            tmpEst.raycastTarget = false;
        }
        else
        {
            Transform t = badgeTr.Find("Texto_Titulo");
            if (t != null) targetText = t.GetComponent<TMP_Text>();
        }
    }

    private GameObject CrearBotonEstilizado(string nombre, Transform padre, Vector2 pos, Vector2 size, string texto, TMP_FontAsset font, Color colorFondo, Color colorTexto)
    {
        GameObject btnGo = new GameObject(nombre);
        btnGo.transform.SetParent(padre, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnGo.AddComponent<Image>();
        img.color = colorFondo;

        Button btn = btnGo.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = colorFondo;
        cb.highlightedColor = colorFondo * 1.35f;
        cb.pressedColor = colorFondo * 0.75f;
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        // Texto interno
        GameObject txtGo = new GameObject("Texto");
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 13;
        tmp.color = colorTexto;
        if (font != null) tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btnGo;
    }

    private GameObject CrearModalOpciones(Transform padre, TMP_FontAsset font)
    {
        // 1. Overlay oscuro de fondo
        GameObject modalGo = new GameObject("Panel_Opciones");
        modalGo.transform.SetParent(padre, false);
        RectTransform mrt = modalGo.AddComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.sizeDelta = Vector2.zero;

        Image overlay = modalGo.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.82f);
        overlay.raycastTarget = true;

        // 2. Tarjeta / Ventana central
        GameObject cardGo = new GameObject("Tarjeta_Opciones");
        cardGo.transform.SetParent(modalGo.transform, false);
        RectTransform crt = cardGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(740f, 520f);

        Image cardBg = cardGo.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.07f, 0.11f, 0.98f); // Obsidiana gótica

        // Borde dorado
        GameObject marcoGo = new GameObject("Marco_Dorado");
        marcoGo.transform.SetParent(cardGo.transform, false);
        RectTransform marcoRt = marcoGo.AddComponent<RectTransform>();
        marcoRt.anchorMin = Vector2.zero;
        marcoRt.anchorMax = Vector2.one;
        marcoRt.sizeDelta = new Vector2(-6f, -6f);
        Image marcoImg = marcoGo.AddComponent<Image>();
        marcoImg.color = new Color(0.78f, 0.62f, 0.32f, 0.65f);
        marcoImg.raycastTarget = false;

        // Interior oscuro sobre el marco
        GameObject innerGo = new GameObject("Fondo_Interior");
        innerGo.transform.SetParent(cardGo.transform, false);
        RectTransform inRt = innerGo.AddComponent<RectTransform>();
        inRt.anchorMin = Vector2.zero;
        inRt.anchorMax = Vector2.one;
        inRt.sizeDelta = new Vector2(-12f, -12f);
        Image inImg = innerGo.AddComponent<Image>();
        inImg.color = new Color(0.09f, 0.08f, 0.13f, 1f);
        inImg.raycastTarget = false;

        // Título del Modal
        GameObject titGo = new GameObject("Titulo_Opciones");
        titGo.transform.SetParent(cardGo.transform, false);
        RectTransform trt = titGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.anchoredPosition = new Vector2(0f, -40f);
        trt.sizeDelta = new Vector2(0f, 40f);

        TextMeshProUGUI titTmp = titGo.AddComponent<TextMeshProUGUI>();
        titTmp.text = "OPCIONES DE SONIDO";
        titTmp.fontSize = 24;
        titTmp.color = new Color(0.98f, 0.88f, 0.58f, 1f);
        if (font != null) titTmp.font = font;
        titTmp.alignment = TextAlignmentOptions.Center;

        // Subtítulo descriptivo
        GameObject subGo = new GameObject("Subtitulo_Opciones");
        subGo.transform.SetParent(cardGo.transform, false);
        RectTransform srt = subGo.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.anchoredPosition = new Vector2(0f, -72f);
        srt.sizeDelta = new Vector2(0f, 25f);

        TextMeshProUGUI subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text = "Ajusta los niveles de audio del panteón";
        subTmp.fontSize = 11;
        subTmp.color = new Color(0.70f, 0.66f, 0.60f, 1f);
        if (font != null) subTmp.font = font;
        subTmp.alignment = TextAlignmentOptions.Center;

        // Contenedor de Sliders
        float posY = -135f;
        sliderGeneral = CrearFilaSlider(cardGo.transform, "Fila_General", "VOLUMEN GENERAL", "Control maestro de todo el juego", posY, font, out textoValorGeneral, new Color(0.95f, 0.80f, 0.40f, 1f));
        posY -= 85f;
        sliderEnemigos = CrearFilaSlider(cardGo.transform, "Fila_Enemigos", "VOLUMEN ENEMIGOS", "Gritos, pasos y ataques de duendes y jefes", posY, font, out textoValorEnemigos, new Color(0.95f, 0.40f, 0.40f, 1f));
        posY -= 85f;
        sliderMusica = CrearFilaSlider(cardGo.transform, "Fila_Musica", "VOLUMEN MÚSICA", "Música ambiental y temas de fondo", posY, font, out textoValorMusica, new Color(0.40f, 0.80f, 0.95f, 1f));

        // Botones de acción en el pie del modal
        // 1. Botón Restablecer
        GameObject btnResGo = CrearBotonEstilizado("Boton_Restablecer", cardGo.transform, new Vector2(-150f, -200f), new Vector2(210f, 42f), "RESTABLECER", font, new Color(0.24f, 0.22f, 0.28f, 1f), new Color(0.85f, 0.82f, 0.80f, 1f));
        botonRestablecerOpciones = btnResGo.GetComponent<Button>();
        botonRestablecerOpciones.onClick.AddListener(RestablecerValoresDefault);

        // 2. Botón Guardar y Volver
        GameObject btnVolGo = CrearBotonEstilizado("Boton_GuardarVolver", cardGo.transform, new Vector2(150f, -200f), new Vector2(240f, 42f), "GUARDAR Y VOLVER", font, new Color(0.55f, 0.38f, 0.16f, 1f), new Color(1f, 0.95f, 0.75f, 1f));
        botonCerrarOpciones = btnVolGo.GetComponent<Button>();
        botonCerrarOpciones.onClick.AddListener(CerrarOpciones);

        return modalGo;
    }

    private Slider CrearFilaSlider(Transform padre, string nombreFila, string titulo, string descripcion, float posY, TMP_FontAsset font, out TMP_Text valorText, Color colorFill)
    {
        GameObject filaGo = new GameObject(nombreFila);
        filaGo.transform.SetParent(padre, false);
        RectTransform frt = filaGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 0.5f);
        frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = new Vector2(0f, posY);
        frt.sizeDelta = new Vector2(660f, 75f);

        // Título de la categoría
        GameObject titGo = new GameObject("Titulo");
        titGo.transform.SetParent(filaGo.transform, false);
        RectTransform trt = titGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(0.7f, 1f);
        trt.anchoredPosition = new Vector2(180f, -12f);
        trt.sizeDelta = new Vector2(360f, 22f);

        TextMeshProUGUI ttmp = titGo.AddComponent<TextMeshProUGUI>();
        ttmp.text = titulo;
        ttmp.fontSize = 12;
        ttmp.fontStyle = FontStyles.Bold;
        ttmp.color = Color.white;
        if (font != null) ttmp.font = font;

        // Slider
        GameObject sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(filaGo.transform, false);
        RectTransform srt = sliderGo.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.5f);
        srt.anchorMax = new Vector2(0f, 0.5f);
        srt.anchoredPosition = new Vector2(250f, -14f);
        srt.sizeDelta = new Vector2(440f, 20f);

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Background track del slider
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bgrt = bgGo.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.sizeDelta = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.16f, 0.22f, 1f);

        // Fill Area
        GameObject fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fart = fillAreaGo.AddComponent<RectTransform>();
        fart.anchorMin = new Vector2(0f, 0.25f);
        fart.anchorMax = new Vector2(1f, 0.75f);
        fart.sizeDelta = new Vector2(-10f, 0f);

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform frtFill = fillGo.AddComponent<RectTransform>();
        frtFill.sizeDelta = Vector2.zero;
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = colorFill;
        slider.fillRect = frtFill;

        // Handle Slide Area
        GameObject handleAreaGo = new GameObject("Handle Slide Area");
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform hart = handleAreaGo.AddComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.sizeDelta = new Vector2(-10f, 0f);

        GameObject handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        RectTransform hrt = handleGo.AddComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(20f, 26f);
        Image hImg = handleGo.AddComponent<Image>();
        hImg.color = new Color(1f, 0.98f, 0.90f, 1f);
        slider.handleRect = hrt;
        slider.targetGraphic = hImg;

        // Texto de valor porcentual (a la derecha del slider)
        GameObject valGo = new GameObject("Texto_Valor");
        valGo.transform.SetParent(filaGo.transform, false);
        RectTransform vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(1f, 0.5f);
        vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.anchoredPosition = new Vector2(-40f, -14f);
        vrt.sizeDelta = new Vector2(70f, 25f);

        TextMeshProUGUI vtmp = valGo.AddComponent<TextMeshProUGUI>();
        vtmp.text = "100%";
        vtmp.fontSize = 13;
        vtmp.fontStyle = FontStyles.Bold;
        vtmp.color = colorFill;
        if (font != null) vtmp.font = font;
        vtmp.alignment = TextAlignmentOptions.Right;
        valorText = vtmp;

        return slider;
    }
}
