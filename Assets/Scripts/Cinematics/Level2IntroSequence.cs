using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Level2IntroSequence : MonoBehaviour
{
    public static Level2IntroSequence Instance { get; private set; }

    [Header("Tiempos")]
    public float initialDarknessSeconds = 0.2f;
    public float irisDuration = 1.3f;

    private Cainos.PixelArtTopDown_Basic.TopDownCharacterController legacyController;
    private Cainos.PixelArtTopDown_Basic.Level2CharacterController playerController;
    private PlayerWeaponController playerWeapons;
    private PlayerStoneThrow playerStones;
    private Transform playerTransform;

    private GameObject irisCanvasObj;
    private IrisInNivel irisComponent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        FindReferences();
        StartCoroutine(ExecuteIntroRoutine());
    }

    private void FindReferences()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            legacyController = playerObj.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            playerController = playerObj.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            playerWeapons = playerObj.GetComponent<PlayerWeaponController>();
            playerStones = playerObj.GetComponent<PlayerStoneThrow>();
        }
    }

    private IEnumerator ExecuteIntroRoutine()
    {
        Level2HUD.IntroSequencePlaying = true;
        EnablePlayerControls();

        // Si ya hay un Iris en Canvas_HUD, usarlo directamente
        IrisInNivel existingIris = FindObjectOfType<IrisInNivel>();
        if (existingIris == null)
        {
            SetupIrisUI();
        }

        // Espera en oscuridad total breve
        yield return new WaitForSeconds(initialDarknessSeconds);

        // Esperar a que el Iris se expanda y revele el nivel
        float elapsed = 0f;
        while (elapsed < irisDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (irisCanvasObj != null)
        {
            Destroy(irisCanvasObj);
        }

        yield return new WaitForSeconds(0.1f);

        // Mostrar la tarjeta de Objetivo con el diseño exacto del Nivel 1, seguida de la Pista 1 de 3
        Level2HUD.IntroSequencePlaying = false;

        if (ParchmentMessageUI.Instance == null)
        {
            GameObject pUi = new GameObject("ParchmentMessageUI");
            pUi.AddComponent<ParchmentMessageUI>();
        }

        if (ParchmentMessageUI.Instance != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isLvl3 = sceneName.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          sceneName.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0;

            ParchmentMessageUI.Instance.Show(
                "OBJETIVO",
                isLvl3
                    ? "Recupera los 3 juguetes para desafiar a La Bruja.\n" +
                      "Cuentas con tus armas del nivel anterior:\n" +
                      "1: Farol (17)  |  2: Cuchillo (28)  |  3: Machete (50).\n" +
                      "<link=\"key:F\"><color=#00000000> F </color></link> Atacar con el arma equipada."
                    : "Recupera los 3 juguetes.\n" +
                      "Cada uno despierta una oleada.\n" +
                      "Expúlsalos y revela otra pista.\n" +
                      "<link=\"key:F\"><color=#00000000> F </color></link> Farol: 3 golpes por duende.",
                null,
                () =>
                {
                    EnablePlayerControls();
                    if (Level2ClueSystem.Instance != null)
                    {
                        Level2ClueSystem.Instance.ShowActiveClue();
                    }
                },
                true
            );
        }
        else
        {
            EnablePlayerControls();
        }
    }

    private void EnablePlayerControls()
    {
        if (playerController != null) playerController.enabled = true;
        else if (legacyController != null) legacyController.enabled = true;
        if (playerWeapons != null) playerWeapons.enabled = true;
        if (playerStones != null) playerStones.enabled = true;
    }

    private void SetupIrisUI()
    {
        irisCanvasObj = new GameObject("Cortina_Iris");
        Canvas canvas = irisCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        irisCanvasObj.AddComponent<CanvasScaler>();
        irisCanvasObj.AddComponent<GraphicRaycaster>();

        GameObject irisObj = new GameObject("IrisGraphic");
        irisObj.transform.SetParent(irisCanvasObj.transform, false);
        RectTransform irisRt = irisObj.AddComponent<RectTransform>();
        irisRt.anchorMin = Vector2.zero;
        irisRt.anchorMax = Vector2.one;
        irisRt.sizeDelta = Vector2.zero;

        irisComponent = irisObj.AddComponent<IrisInNivel>();
        irisComponent.jugador = playerTransform;
        irisComponent.ajusteCentro = new Vector3(0f, 0.4f, 0f);
        irisComponent.duracion = irisDuration;
        irisComponent.esperaInicial = 0.05f;
        irisComponent.bordeSuave = 4f;
    }
}
