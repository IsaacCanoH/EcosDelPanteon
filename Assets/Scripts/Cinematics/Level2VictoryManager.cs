using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Level2VictoryManager : MonoBehaviour
{
    public static Level2VictoryManager Instance { get; private set; }

    [Header("Escenas de Destino")]
    public string nextSceneName = "Cementerio_Nivel3";
    public string fallbackMenuSceneName = "MenuPrincipal";

    [Header("Audio")]
    public AudioClip victoryFanfare;
    public AudioClip gateOpenClip;

    [Header("UI estilo Nivel 1 (CartaAntigua_Sello)")]
    public CanvasGroup screenCanvasGroup;
    public CanvasGroup cardCanvasGroup;
    public Image blackoutImage;
    public RectTransform cardRect;
    public Image cardBackground;
    public TMP_Text victoryTitleText;
    public TMP_Text victoryBodyText;
    public TMP_Text victoryHintText;
    public GameObject buttonRowObj;
    public VictoryCelebration victoryCelebration;

    public Button nextLevelButton;
    public Button retryButton;
    public Button menuButton;
    public Button quitButton;

    private RectTransform nextBtnRt;
    private RectTransform retryBtnRt;
    private RectTransform menuBtnRt;
    private RectTransform quitBtnRt;

    private Image nextBtnImg;
    private Image retryBtnImg;
    private Image menuBtnImg;
    private Image quitBtnImg;

    // Colores exactos de los botones de la tarjeta de Victoria del Nivel 1 (Imagen 3)
    private readonly Color primaryBtnNormal = new Color32(118, 104, 67, 255);   // #766843 oliva dorado
    private readonly Color primaryBtnHover = new Color32(146, 130, 86, 255);
    private readonly Color secondaryBtnNormal = new Color32(74, 66, 49, 255);   // #4A4231 café oscuro panteón
    private readonly Color secondaryBtnHover = new Color32(102, 91, 68, 255);

    private bool victoryTriggered = false;
    private bool victoryCardInteractive = false;
    private bool actionInProgress = false;

    private GameObject playerObj;
    private ToyCollector toyCollector;
    private PlayerWeaponController playerWeapons;

    // Puerta derecha de salida (Acceso este), simétrica a Acceso oeste (Imagen 1)
    private GameObject eastGateRoot;
    private SpriteRenderer northLeafRenderer;
    private SpriteRenderer southLeafRenderer;
    private BoxCollider2D closedGateBlocker;
    private Sprite[] northGateFrames;
    private Sprite[] southGateFrames;

    // Coordenadas exactas simétricas a Acceso oeste (que está en X = -21.85, Y = -3.15 a -6.75)
    public const float EastPillarX = 21.85f;
    public const float EastLeafX = 21.52f;
    public const float NorthPillarY = -3.15f;
    public const float SouthPillarY = -6.75f;
    public const float NorthLeafY = -3.05f;
    public const float SouthLeafY = -6.68f;
    public const float GateCenterY = -4.85f;

    // Coordenadas de la Puerta de Salida Superior (Muro Norte) para el Nivel 3 (pegada exactamente al muro norte en Y = 12.0)
    public const float TopGateCenterX = 0.0f;
    public const float TopGateWallY = 12.05f;
    public const float TopPillarWestX = -1.85f;
    public const float TopPillarEastX = 1.85f;
    public const float TopPillarY = 11.90f;

    private static bool IsLevel3Scene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (IsLevel3Scene())
        {
            nextSceneName = "MenuPrincipal";
        }

        SetupProceduralUIIfMissing();
        HideImmediate();
    }

    private void Start()
    {
        FindPlayerReferences();
        if (IsLevel3Scene())
        {
            EnsureWestEntranceGateOpen();  // Puerta de inicio en el lado izquierdo (Acceso Oeste)
            BuildNorthExitGateIfMissing(); // Puerta de salida cerrada pegada al muro norte
        }
        else
        {
            BuildEastExitGateIfMissing();
        }
    }

    private void FindPlayerReferences()
    {
        if (playerObj == null)
            playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            if (toyCollector == null) toyCollector = playerObj.GetComponent<ToyCollector>();
            if (playerWeapons == null) playerWeapons = playerObj.GetComponent<PlayerWeaponController>();
        }
    }

    private void Update()
    {
        if (!victoryTriggered)
        {
            CheckVictoryConditions();
            return;
        }

        if (victoryCardInteractive && !actionInProgress)
        {
            HandleDirectVictoryButtonsInput();
        }
    }

    public void CheckVictoryConditions()
    {
        if (victoryTriggered) return;

        FindPlayerReferences();
        if (toyCollector == null || playerWeapons == null) return;

        if (IsLevel3Scene())
        {
            // En el Nivel 3 ya se cuenta con el Machete desde el inicio; la victoria ocurre al recuperar los 3 juguetes tras derrotar a La Bruja
            if (toyCollector.Count >= 3 && ToyEncounterZone.GlobalEncountersCompleted >= 3)
            {
                TriggerVictorySequence();
            }
        }
        else
        {
            // En el Nivel 2 la secuencia final inicia justo al recoger el 3er juguete Y el machete
            if (toyCollector.Count >= 3 && playerWeapons.hasMachete)
            {
                TriggerVictorySequence();
            }
        }
    }

    public void TriggerVictorySequence()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;

        StartCoroutine(Level2EndingSequenceRoutine());
    }

    #region Construcción de la Puerta Derecha (Acceso este) idéntica a la Izquierda (Imagen 1) pero Cerrada

    public void BuildEastExitGateIfMissing()
    {
        if (eastGateRoot != null) return;

        // Limpiar cualquier versión previa si existiera
        GameObject oldGate = GameObject.Find("Acceso_Este_Salida");
        if (oldGate != null) Destroy(oldGate);

        eastGateRoot = new GameObject("Acceso_Este_Salida");
        eastGateRoot.transform.position = Vector3.zero;

        // 1. Despejar tumbas o ataúdes que obstruyan la entrada de la puerta derecha (X >= 16.5, Y entre -7.2 y -2.5)
        ClearPropsInFrontOfEastGate();

        // 2. Abrir el hueco en el Tilemap del muro derecho (Layer 1 - Wall) y pintar el camino de piedra (Layer 1 - Stone Ground)
        CarveEastWallAndPaintStonePath();

        // 3. Tomar referencias visuales exactas de "Acceso oeste" (pilares, hojas de madera, arbustos, material y tinte nocturno)
        GameObject accesoOeste = GameObject.Find("Acceso oeste");
        Transform westPillarTemplate = null;
        Transform westBushTemplate = null;
        Sprite westNorthLeafSprite = null;
        Sprite westSouthLeafSprite = null;
        Material propsMat = null;
        int sortingLayerID = SortingLayer.NameToID("Layer 1");
        Color nightTint = new Color(0.6117647f, 0.6666667f, 0.78431374f, 1f);

        if (accesoOeste != null)
        {
            for (int i = 0; i < accesoOeste.transform.childCount; i++)
            {
                Transform ch = accesoOeste.transform.GetChild(i);
                if (ch.name.Contains("Pillar") && westPillarTemplate == null)
                {
                    westPillarTemplate = ch;
                    SpriteRenderer sr = ch.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null)
                    {
                        propsMat = sr.sharedMaterial;
                        sortingLayerID = sr.sortingLayerID;
                        nightTint = sr.color;
                    }
                }
                else if (ch.name.Contains("Bush") && westBushTemplate == null)
                {
                    westBushTemplate = ch;
                }
                else if (ch.name == "Puerta Lateral - Hoja Norte")
                {
                    var sr = ch.GetComponent<SpriteRenderer>();
                    if (sr != null) westNorthLeafSprite = sr.sprite;
                }
                else if (ch.name == "Puerta Lateral - Hoja Sur")
                {
                    var sr = ch.GetComponent<SpriteRenderer>();
                    if (sr != null) westSouthLeafSprite = sr.sprite;
                }
            }
        }

#if UNITY_EDITOR
        if (westNorthLeafSprite == null)
            westNorthLeafSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Puerta Lateral Hoja.png");
        if (westSouthLeafSprite == null)
            westSouthLeafSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Puerta Lateral Hoja Sur.png");
        if (westPillarTemplate == null)
        {
            GameObject pPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Pillar 01.prefab");
            if (pPf != null) westPillarTemplate = pPf.transform;
        }
#endif

        // 4. Instanciar los 2 Pilares de Piedra pegados al muro derecho (X = 21.85), idénticos a los de la izquierda
        if (westPillarTemplate != null)
        {
            GameObject topPillar = Instantiate(westPillarTemplate.gameObject, eastGateRoot.transform);
            topPillar.name = "Pilar Este - Norte";
            topPillar.transform.position = new Vector3(EastPillarX, NorthPillarY, 0f);
            topPillar.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            foreach (var sr in topPillar.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 2;
            }

            GameObject bottomPillar = Instantiate(westPillarTemplate.gameObject, eastGateRoot.transform);
            bottomPillar.name = "Pilar Este - Sur";
            bottomPillar.transform.position = new Vector3(EastPillarX, SouthPillarY, 0f);
            bottomPillar.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            foreach (var sr in bottomPillar.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 3;
            }
        }

        // 5. Instanciar los arbustos decorativos simétricos a los de la puerta izquierda
        if (westBushTemplate != null)
        {
            Vector3[] bushPositions = new Vector3[]
            {
                new Vector3(19.4f, -2.55f, 0f),
                new Vector3(17.9f, -2.65f, 0f),
                new Vector3(19.4f, -6.95f, 0f),
                new Vector3(17.9f, -6.95f, 0f)
            };
            for (int i = 0; i < bushPositions.Length; i++)
            {
                GameObject b = Instantiate(westBushTemplate.gameObject, eastGateRoot.transform);
                b.name = $"Bush Este {i + 1}";
                b.transform.position = bushPositions[i];
                foreach (var sr in b.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 2;
                }
            }
        }

        // 6. Generar los 10 frames 2.5D de apertura a partir de las texturas reales TX Puerta Lateral Hoja y TX Puerta Lateral Hoja Sur
        //    Frame 0 = Puerta cerrada sobre el muro derecho (uniéndose en el centro entre ambos pilares)
        //    Frame 9 = Puerta completamente abierta hacia la izquierda (espejo exacto de la Imagen 1)
        GenerateEastGateAnimationFrames(westNorthLeafSprite, westSouthLeafSprite, out northGateFrames, out southGateFrames);

        GameObject northLeafObj = new GameObject("Puerta Este - Hoja Norte");
        northLeafObj.transform.SetParent(eastGateRoot.transform, false);
        northLeafObj.transform.position = new Vector3(EastLeafX, NorthLeafY, 0f);
        northLeafObj.transform.localScale = new Vector3(1.18f, 1.22f, 1f);
        northLeafRenderer = northLeafObj.AddComponent<SpriteRenderer>();
        northLeafRenderer.sprite = northGateFrames[0]; // Inicia cerrada
        if (propsMat != null) northLeafRenderer.sharedMaterial = propsMat;
        northLeafRenderer.sortingLayerID = sortingLayerID;
        northLeafRenderer.sortingOrder = 2;
        northLeafRenderer.color = nightTint;

        GameObject southLeafObj = new GameObject("Puerta Este - Hoja Sur");
        southLeafObj.transform.SetParent(eastGateRoot.transform, false);
        southLeafObj.transform.position = new Vector3(EastLeafX, SouthLeafY, 0f);
        southLeafObj.transform.localScale = new Vector3(1.18f, 1.22f, 1f);
        southLeafRenderer = southLeafObj.AddComponent<SpriteRenderer>();
        southLeafRenderer.sprite = southGateFrames[0]; // Inicia cerrada
        if (propsMat != null) southLeafRenderer.sharedMaterial = propsMat;
        southLeafRenderer.sortingLayerID = sortingLayerID;
        southLeafRenderer.sortingOrder = 3;
        southLeafRenderer.color = nightTint;

        // 7. Colisionador físico sólido que mantiene cerrada la puerta durante el nivel
        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
        eastGateRoot.layer = obstaclesLayer >= 0 ? obstaclesLayer : 6;
        closedGateBlocker = eastGateRoot.AddComponent<BoxCollider2D>();
        closedGateBlocker.isTrigger = false;
        closedGateBlocker.offset = new Vector2(EastLeafX, GateCenterY);
        closedGateBlocker.size = new Vector2(1.1f, 3.8f);
    }

    /// <summary>
    /// En el Nivel 3 (La Bruja), la puerta de inicio está en el lado izquierdo (Acceso Oeste)
    /// y la puerta de salida está en el muro superior (Acceso Norte).
    /// Asegura que el acceso oeste esté activo, abierto y despejado para la entrada del jugador,
    /// y elimina cualquier portal previo en el muro este, sellando la barda este como muro continuo.
    /// </summary>
    public void EnsureWestEntranceGateOpen()
    {
        // 1. Eliminar cualquier portal temporal en el muro este
        GameObject oldEastInicio = GameObject.Find("Acceso_Este_Inicio");
        if (oldEastInicio != null) Destroy(oldEastInicio);
        GameObject oldEastSalida = GameObject.Find("Acceso_Este_Salida");
        if (oldEastSalida != null) Destroy(oldEastSalida);

        // 2. Asegurar que Barda Este sea un muro sólido continuo sin huecos
        GameObject bardaEste = GameObject.Find("Barda este");
        if (bardaEste != null)
        {
            var colliders = bardaEste.GetComponents<Collider2D>();
            foreach (var c in colliders)
            {
                if (c != null) Destroy(c);
            }
            BoxCollider2D fullEastCol = bardaEste.AddComponent<BoxCollider2D>();
            fullEastCol.size = new Vector2(0.5f, 26f);
            fullEastCol.offset = new Vector2(0f, -0.5f);
        }

        // 3. Restaurar baldosas de muro en Layer 1 - Wall si habían sido recortadas en el muro este
        GameObject wallObj = GameObject.Find("Layer 1 - Wall");
        if (wallObj != null)
        {
            Tilemap wallTm = wallObj.GetComponent<Tilemap>();
            if (wallTm != null)
            {
                TileBase sampleTile = wallTm.GetTile(new Vector3Int(21, 0, 0));
                if (sampleTile == null) sampleTile = wallTm.GetTile(new Vector3Int(21, 5, 0));
                if (sampleTile != null)
                {
                    for (int cx = 20; cx <= 22; cx++)
                    {
                        for (int cy = -6; cy <= -4; cy++)
                        {
                            wallTm.SetTile(new Vector3Int(cx, cy, 0), sampleTile);
                        }
                    }
                }
            }
        }

        // 4. Asegurar que Acceso Oeste esté completamente activo, visible y abierto
        Level2SceneRuntimeSetup.RestoreAndOpenWestEntranceGate();
    }

    private void ClearPropsInFrontOfEastGate()
    {
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t == null || t.gameObject == null) continue;
            if (eastGateRoot != null && (t == eastGateRoot.transform || t.IsChildOf(eastGateRoot.transform)))
                continue;

            string n = t.name;
            // No desactivar contenedores raíz ni tilemaps ni bardas
            if (n == "SCENE" || n == "Props" || n == "Panteones familiares" ||
                n.Contains("Barda") || n.Contains("Acceso") || n.StartsWith("Layer "))
                continue;

            // Identificar lápidas, ataúdes o props colocados frente a la puerta derecha (ej. Gravestone 01 en 18.7, -5.2 y Stone Coffin en 19.7, -6.5)
            bool isGraveOrProp = n.Contains("Gravestone") || n.Contains("Coffin") || n.Contains("Tomb") ||
                                 n.Contains("Stone") || n.Contains("Cross") || n.Contains("Monument") ||
                                 (t.parent != null && (t.parent.name == "Panteones familiares" || t.parent.name == "Props"));

            if (!isGraveOrProp) continue;

            Vector3 pos = t.position;
            if (pos.x >= 16.0f && pos.x <= 22.6f && pos.y >= -7.4f && pos.y <= -2.4f)
            {
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
        }
    }

    private void CarveEastWallAndPaintStonePath()
    {
        // 1. Abrir el pórtico en el muro derecho (Layer 1 - Wall y Layer 1 - Wall Shadow) igual que en Acceso oeste
        GameObject wallObj = GameObject.Find("Layer 1 - Wall");
        if (wallObj != null)
        {
            Tilemap wallTm = wallObj.GetComponent<Tilemap>();
            if (wallTm != null)
            {
                for (int cx = 20; cx <= 23; cx++)
                {
                    for (int cy = -6; cy <= -4; cy++)
                    {
                        wallTm.SetTile(new Vector3Int(cx, cy, 0), null);
                    }
                }
            }
        }

        GameObject shadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        if (shadowObj != null)
        {
            Tilemap shadowTm = shadowObj.GetComponent<Tilemap>();
            if (shadowTm != null)
            {
                for (int cx = 19; cx <= 23; cx++)
                {
                    for (int cy = -6; cy <= -4; cy++)
                    {
                        shadowTm.SetTile(new Vector3Int(cx, cy, 0), null);
                    }
                }
            }
        }

        // 2. Pintar el camino de losetas de piedra (Layer 1 - Stone Ground) hasta la puerta derecha igual que en la izquierda
        GameObject stoneObj = GameObject.Find("Layer 1 - Stone Ground");
        if (stoneObj != null)
        {
            Tilemap stoneTm = stoneObj.GetComponent<Tilemap>();
            if (stoneTm != null)
            {
                // Tomar las losetas de muestra del camino de la puerta izquierda (-18, -4 / -5 / -6)
                TileBase topRowTile = stoneTm.GetTile(new Vector3Int(-18, -4, 0));
                TileBase midRowTile = stoneTm.GetTile(new Vector3Int(-18, -5, 0));
                TileBase botRowTile = stoneTm.GetTile(new Vector3Int(-18, -6, 0));

                if (midRowTile == null)
                {
                    // Buscar cualquier loseta de piedra existente cerca del pasillo
                    for (int sx = -15; sx <= 15 && midRowTile == null; sx++)
                        for (int sy = -6; sy <= -4 && midRowTile == null; sy++)
                            midRowTile = stoneTm.GetTile(new Vector3Int(sx, sy, 0));
                }
                if (topRowTile == null) topRowTile = midRowTile;
                if (botRowTile == null) botRowTile = midRowTile;

                if (midRowTile != null)
                {
                    for (int cx = 13; cx <= 22; cx++)
                    {
                        stoneTm.SetTile(new Vector3Int(cx, -4, 0), topRowTile);
                        stoneTm.SetTile(new Vector3Int(cx, -5, 0), midRowTile);
                        stoneTm.SetTile(new Vector3Int(cx, -6, 0), botRowTile);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Toma las texturas originales de las dos hojas de la puerta lateral (Imagen 1) y genera 10 frames en perspectiva 2.5D:
    /// desde CERRADA en el muro derecho (frame 0) hasta ABIERTA hacia adentro (frame 9, espejo exacto de Imagen 1).
    /// </summary>
    private static void GenerateEastGateAnimationFrames(
        Sprite westNorthSprite,
        Sprite westSouthSprite,
        out Sprite[] eastNorthFrames,
        out Sprite[] eastSouthFrames)
    {
        int frameCount = 10;
        eastNorthFrames = new Sprite[frameCount];
        eastSouthFrames = new Sprite[frameCount];

        Color32[] northSrcPixels = ExtractSpritePixels(westNorthSprite, out int nw, out int nh);
        Color32[] southSrcPixels = ExtractSpritePixels(westSouthSprite, out int sw, out int sh);

        for (int f = 0; f < frameCount; f++)
        {
            float t = f / (float)(frameCount - 1); // 0 = Cerrada en el muro derecho, 1 = Abierta igual a Imagen 1
            eastNorthFrames[f] = Create25DPerspectiveGateLeafFrame(northSrcPixels, nw, nh, t, true);
            eastSouthFrames[f] = Create25DPerspectiveGateLeafFrame(southSrcPixels, sw, sh, t, false);
        }
    }

    private static Color32[] ExtractSpritePixels(Sprite sprite, out int width, out int height)
    {
        if (sprite == null || sprite.texture == null)
        {
            width = 92;
            height = 60;
            return new Color32[width * height];
        }

        Texture2D srcTex = sprite.texture;
        width = srcTex.width;
        height = srcTex.height;

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(srcTex, rt);
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = readable.GetPixels32();
        Destroy(readable);
        return pixels;
    }

    private static Sprite Create25DPerspectiveGateLeafFrame(
        Color32[] srcPixels,
        int srcW,
        int srcH,
        float openProgress,
        bool isNorthLeaf)
    {
        // En el sprite original (92x60, PPU 32, pivot (0, 0.14)):
        // x = 0 es la bisagra pegada al pilar; x = 79 es el extremo libre de la hoja.
        // Para el muro derecho, la bisagra está a la derecha (x = dstW - 1) y la hoja se extiende hacia la izquierda.
        // Cuando openProgress = 1 (ABIERTA): es el espejo horizontal exacto del sprite de la Imagen 1.
        // Cuando openProgress = 0 (CERRADA): las dos hojas bajan/suben por la línea del muro derecho uniéndose al centro.
        int padY = 44;
        int dstW = srcW;
        int dstH = srcH + padY * 2;

        Texture2D dstTex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
        dstTex.filterMode = FilterMode.Point;
        dstTex.wrapMode = TextureWrapMode.Clamp;

        Color32[] dstPixels = new Color32[dstW * dstH];

        // Ángulo de apertura en el plano del suelo:
        // En t=0 (cerrada): 14° respecto al muro vertical para que se aprecien los tablones, herrajes negros y picos en 2.5D.
        // En t=1 (abierta): 90° (completamente abierta hacia adentro, idéntica a la Imagen 1).
        float angleDeg = Mathf.Lerp(14f, 90f, openProgress);
        float rad = angleDeg * Mathf.Deg2Rad;

        float horizFactor = Mathf.Sin(rad); // 0.24 (cerrada) -> 1.00 (abierta)
        float vertFactor = Mathf.Cos(rad);  // 0.97 (cerrada) -> 0.00 (abierta)

        // Desplazamiento vertical máximo en píxeles desde la bisagra hasta el centro de la puerta cuando está cerrada
        // (Compensa la inclinación original del sprite para que ambas hojas se toquen exactamente en Y = -4.85)
        float closedTipDeltaY = isNorthLeaf ? -36f : 34f;

        int maxReachPx = Mathf.Max(1, Mathf.RoundToInt((srcW - 1) * horizFactor));

        for (int dx = 0; dx <= maxReachPx; dx++)
        {
            // Columna destino medida desde la bisagra derecha (dstW - 1) hacia la izquierda
            int dstX = (dstW - 1) - dx;
            if (dstX < 0 || dstX >= dstW) continue;

            // Muestrear la columna correspondiente del sprite original (sin dejar huecos)
            float u = dx / (float)maxReachPx;
            int srcX = Mathf.Clamp(Mathf.RoundToInt(u * (srcW - 1)), 0, srcW - 1);

            int shiftY = Mathf.RoundToInt(u * closedTipDeltaY * vertFactor);

            for (int srcY = 0; srcY < srcH; srcY++)
            {
                Color32 c = srcPixels[srcY * srcW + srcX];
                if (c.a == 0) continue;

                int dstY = srcY + padY + shiftY;
                if (dstY >= 0 && dstY < dstH)
                {
                    dstPixels[dstY * dstW + dstX] = c;
                }
            }
        }

        dstTex.SetPixels32(dstPixels);
        dstTex.Apply();

        // El pivote se mantiene exactamente en la bisagra derecha (x = 1.0) y en la altura original (0.14 de srcH + padY)
        float pivotY = (padY + srcH * 0.14f) / dstH;
        return Sprite.Create(dstTex, new Rect(0, 0, dstW, dstH), new Vector2(1f, pivotY), 32f);
    }

    /// <summary>
    /// Construye la Puerta de Salida en el Muro Superior (Barda norte) para el Nivel 3 (La Bruja).
    /// Permanece cerrada durante la partida y se abre con animación 2.5D en la cinemática final.
    /// </summary>
    public void BuildNorthExitGateIfMissing()
    {
        if (eastGateRoot != null) return;

        GameObject oldNorth = GameObject.Find("Acceso_Norte_Salida");
        if (oldNorth != null) Destroy(oldNorth);

        eastGateRoot = new GameObject("Acceso_Norte_Salida");
        eastGateRoot.transform.position = Vector3.zero;

        // 1. Despejar árboles, tumbas o arbustos que obstruyan el pasillo hacia el muro norte (X entre -3.2 y 3.2, Y >= 5.5)
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t == null || t.gameObject == null) continue;
            if (t == eastGateRoot.transform || t.IsChildOf(eastGateRoot.transform)) continue;

            string n = t.name;
            if (n == "SCENE" || n == "Props" || n == "Panteones familiares" ||
                n.Contains("Barda") || n.Contains("Acceso") || n.StartsWith("Layer ") ||
                n.StartsWith("Altar_Encounter_"))
                continue;

            bool isProp = n.Contains("Gravestone") || n.Contains("Coffin") || n.Contains("Tomb") ||
                          n.Contains("Stone") || n.Contains("Cross") || n.Contains("Tree") ||
                          n.Contains("Bush") || n.Contains("Bench") ||
                          (t.parent != null && (t.parent.name == "Panteones familiares" || t.parent.name == "Props"));

            if (!isProp) continue;

            Vector3 pos = t.position;
            if (pos.x >= -3.2f && pos.x <= 3.2f && pos.y >= 5.8f && pos.y <= 13.5f)
            {
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
        }

        // 2. Abrir el pórtico en el muro superior (Layer 1 - Wall, Layer 2 - Wall y Layer 1 - Wall Shadow) y pintar el camino de piedra
        string[] wallLayerNames = { "Layer 1 - Wall", "Layer 2 - Wall" };
        foreach (string wlName in wallLayerNames)
        {
            GameObject wallObj = GameObject.Find(wlName);
            if (wallObj != null)
            {
                Tilemap wallTm = wallObj.GetComponent<Tilemap>();
                if (wallTm != null)
                {
                    for (int cx = -2; cx <= 1; cx++)
                    {
                        for (int cy = 10; cy <= 15; cy++)
                        {
                            wallTm.SetTile(new Vector3Int(cx, cy, 0), null);
                        }
                    }
                }
            }
        }

        GameObject shadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        if (shadowObj != null)
        {
            Tilemap shadowTm = shadowObj.GetComponent<Tilemap>();
            if (shadowTm != null)
            {
                for (int cx = -2; cx <= 2; cx++)
                {
                    for (int cy = 9; cy <= 15; cy++)
                    {
                        shadowTm.SetTile(new Vector3Int(cx, cy, 0), null);
                    }
                }
            }
        }

        GameObject stoneObj = GameObject.Find("Layer 1 - Stone Ground");
        if (stoneObj != null)
        {
            Tilemap stoneTm = stoneObj.GetComponent<Tilemap>();
            if (stoneTm != null)
            {
                TileBase midRowTile = stoneTm.GetTile(new Vector3Int(-18, -5, 0));
                if (midRowTile == null)
                {
                    for (int sx = -15; sx <= 15 && midRowTile == null; sx++)
                        for (int sy = -6; sy <= 6 && midRowTile == null; sy++)
                            midRowTile = stoneTm.GetTile(new Vector3Int(sx, sy, 0));
                }

                if (midRowTile != null)
                {
                    for (int cx = -2; cx <= 1; cx++)
                    {
                        for (int cy = 3; cy <= 13; cy++)
                        {
                            stoneTm.SetTile(new Vector3Int(cx, cy, 0), midRowTile);
                        }
                    }
                }
            }
        }

        // 3. Tomar referencias de pilares, arbustos, material y tinte nocturno de "Acceso oeste"
        GameObject accesoOeste = GameObject.Find("Acceso oeste");
        if (accesoOeste == null)
        {
            accesoOeste = Level2SceneRuntimeSetup.RestoreAndOpenWestEntranceGate();
        }
        Transform westPillarTemplate = null;
        Transform westBushTemplate = null;
        Material propsMat = null;
        int sortingLayerID = SortingLayer.NameToID("Layer 1");
        Color nightTint = new Color(0.6117647f, 0.6666667f, 0.78431374f, 1f);

        if (accesoOeste != null)
        {
            for (int i = 0; i < accesoOeste.transform.childCount; i++)
            {
                Transform ch = accesoOeste.transform.GetChild(i);
                if (ch.name.Contains("Pillar") && westPillarTemplate == null)
                {
                    westPillarTemplate = ch;
                    SpriteRenderer sr = ch.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null)
                    {
                        propsMat = sr.sharedMaterial;
                        sortingLayerID = sr.sortingLayerID;
                        nightTint = sr.color;
                    }
                }
                else if (ch.name.Contains("Bush") && westBushTemplate == null)
                {
                    westBushTemplate = ch;
                }
            }
        }

#if UNITY_EDITOR
        if (westPillarTemplate == null)
        {
            GameObject pPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Pillar 01.prefab");
            if (pPf != null) westPillarTemplate = pPf.transform;
        }
#endif

        // 4. Instanciar los 2 Pilares de Piedra flanqueando la puerta en el Muro Norte
        if (westPillarTemplate != null)
        {
            GameObject leftPillar = Instantiate(westPillarTemplate.gameObject, eastGateRoot.transform);
            leftPillar.name = "Pilar Norte - Oeste";
            leftPillar.transform.position = new Vector3(TopPillarWestX, TopPillarY, 0f);
            leftPillar.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            foreach (var sr in leftPillar.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 3;
            }

            GameObject rightPillar = Instantiate(westPillarTemplate.gameObject, eastGateRoot.transform);
            rightPillar.name = "Pilar Norte - Este";
            rightPillar.transform.position = new Vector3(TopPillarEastX, TopPillarY, 0f);
            rightPillar.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            foreach (var sr in rightPillar.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 3;
            }
        }

        if (westBushTemplate != null)
        {
            Vector3[] bushPos = new Vector3[]
            {
                new Vector3(-2.95f, 11.85f, 0f),
                new Vector3( 2.95f, 11.85f, 0f)
            };
            for (int i = 0; i < bushPos.Length; i++)
            {
                GameObject b = Instantiate(westBushTemplate.gameObject, eastGateRoot.transform);
                b.name = $"Bush Norte {i + 1}";
                b.transform.position = bushPos[i];
                foreach (var sr in b.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 2;
                }
            }
        }

        // 5. Generar los 10 frames 2.5D de las dos hojas de madera de la Puerta Norte (Oeste y Este)
        GenerateNorthGateAnimationFrames(out northGateFrames, out southGateFrames);

        GameObject westLeafObj = new GameObject("Puerta Norte - Hoja Oeste");
        westLeafObj.transform.SetParent(eastGateRoot.transform, false);
        westLeafObj.transform.position = new Vector3(-1.45f, TopGateWallY, 0f);
        westLeafObj.transform.localScale = new Vector3(1.12f, 1.15f, 1f);
        northLeafRenderer = westLeafObj.AddComponent<SpriteRenderer>();
        northLeafRenderer.sprite = northGateFrames[0];
        if (propsMat != null) northLeafRenderer.sharedMaterial = propsMat;
        northLeafRenderer.sortingLayerID = sortingLayerID;
        northLeafRenderer.sortingOrder = 2;
        northLeafRenderer.color = nightTint;

        GameObject eastLeafObj = new GameObject("Puerta Norte - Hoja Este");
        eastLeafObj.transform.SetParent(eastGateRoot.transform, false);
        eastLeafObj.transform.position = new Vector3(1.45f, TopGateWallY, 0f);
        eastLeafObj.transform.localScale = new Vector3(1.12f, 1.15f, 1f);
        southLeafRenderer = eastLeafObj.AddComponent<SpriteRenderer>();
        southLeafRenderer.sprite = southGateFrames[0];
        if (propsMat != null) southLeafRenderer.sharedMaterial = propsMat;
        southLeafRenderer.sortingLayerID = sortingLayerID;
        southLeafRenderer.sortingOrder = 2;
        southLeafRenderer.color = nightTint;

        // 6. Colisionador físico sólido que mantiene cerrada la puerta superior durante todo el Nivel 3
        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
        eastGateRoot.layer = obstaclesLayer >= 0 ? obstaclesLayer : 6;
        closedGateBlocker = eastGateRoot.AddComponent<BoxCollider2D>();
        closedGateBlocker.isTrigger = false;
        closedGateBlocker.offset = new Vector2(TopGateCenterX, TopGateWallY);
        closedGateBlocker.size = new Vector2(3.6f, 1.0f);
    }

    private static void GenerateNorthGateAnimationFrames(out Sprite[] westLeafFrames, out Sprite[] eastLeafFrames)
    {
        int frameCount = 10;
        westLeafFrames = new Sprite[frameCount];
        eastLeafFrames = new Sprite[frameCount];

        for (int f = 0; f < frameCount; f++)
        {
            float t = f / (float)(frameCount - 1); // 0 = Cerrada en el muro norte, 1 = Abierta hacia arriba/adentro
            westLeafFrames[f] = CreateNorthGateLeafFrame(t, isWestLeaf: true);
            eastLeafFrames[f] = CreateNorthGateLeafFrame(t, isWestLeaf: false);
        }
    }

    private static Sprite CreateNorthGateLeafFrame(float openProgress, bool isWestLeaf)
    {
        int w = 52;
        int h = 68;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[w * h];
        Color woodDark = new Color(0.24f, 0.17f, 0.12f, 1f);
        Color woodMid = new Color(0.36f, 0.27f, 0.20f, 1f);
        Color woodLight = new Color(0.45f, 0.34f, 0.25f, 1f);
        Color ironBand = new Color(0.16f, 0.18f, 0.23f, 1f);
        Color ironRivet = new Color(0.32f, 0.36f, 0.44f, 1f);
        Color spikeCol = new Color(0.14f, 0.15f, 0.19f, 1f);

        // Cuando openProgress = 0 (cerrada): la hoja se extiende horizontalmente 42px hacia el centro (X = 0).
        // Cuando openProgress = 1 (abierta): la hoja gira 82° hacia arriba/adentro abriendo el pórtico norte.
        float angleRad = Mathf.Lerp(0f, 80f, openProgress) * Mathf.Deg2Rad;
        int reachX = Mathf.Max(6, Mathf.RoundToInt(42f * Mathf.Cos(angleRad)));
        float skewY = 26f * Mathf.Sin(angleRad);

        int baseY = 8;
        int doorBaseHeight = 32;

        for (int dx = 0; dx < reachX; dx++)
        {
            float u = dx / (float)Mathf.Max(1, reachX - 1); // 0 en la bisagra junto al pilar, 1 en el centro de la puerta
            int px = isWestLeaf ? (4 + dx) : (w - 5 - dx);
            if (px < 0 || px >= w) continue;

            int archAdd = Mathf.RoundToInt(Mathf.Sin(u * Mathf.PI * 0.5f) * 7f);
            int shiftY = Mathf.RoundToInt(u * skewY);
            int colHeight = doorBaseHeight + archAdd;

            bool isPlankSeam = (Mathf.RoundToInt(u * 42f) % 7 == 0) || dx == 0 || dx == reachX - 1;

            for (int dy = 0; dy < colHeight; dy++)
            {
                int py = baseY + dy + shiftY;
                if (py < 0 || py >= h) continue;

                Color c = isPlankSeam ? woodDark : ((Mathf.RoundToInt(u * 42f) / 7) % 2 == 0 ? woodMid : woodLight);

                // Herrajes horizontales de hierro forjado (igual que la puerta izquierda)
                if (dy == 6 || dy == 7 || dy == colHeight - 7 || dy == colHeight - 6)
                {
                    c = (Mathf.RoundToInt(u * 42f) % 6 == 3 && (dy == 6 || dy == colHeight - 7)) ? ironRivet : ironBand;
                }
                else if (dy == 0 || dy == colHeight - 1)
                {
                    c = woodDark;
                }

                pixels[py * w + px] = c;
            }

            // Picos superiores de hierro forjado
            if (Mathf.RoundToInt(u * 42f) % 5 == 2)
            {
                int topY = baseY + colHeight + shiftY;
                if (topY >= 0 && topY + 2 < h)
                {
                    pixels[topY * w + px] = spikeCol;
                    pixels[(topY + 1) * w + px] = spikeCol;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        float pivotX = isWestLeaf ? (4f / w) : ((w - 5f) / w);
        float pivotY = (baseY + 8f) / h;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotX, pivotY), 32f);
    }

    #endregion

    #region Cinemática Final y Secuencia de Victoria idéntica al Nivel 1 (Imagen 3)

    private IEnumerator Level2EndingSequenceRoutine()
    {
        bool isLvl3 = IsLevel3Scene();
        if (isLvl3)
        {
            BuildNorthExitGateIfMissing();
        }
        else
        {
            BuildEastExitGateIfMissing();
        }
        FindPlayerReferences();

        // 1. Esperar si hay algún pergamino abierto (ej. mensaje de derrota del Niño Espectral)
        while (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.1f);

        // 2. Bloquear controles del jugador y ocultar HUD de combate (igual que LockGameplay en LevelOneEnding)
        Cainos.PixelArtTopDown_Basic.TopDownCharacterController topDownCtrl = null;
        Cainos.PixelArtTopDown_Basic.Level2CharacterController lvl2Ctrl = null;
        Rigidbody2D playerRb = null;
        Collider2D playerCol = null;
        Animator playerAnim = null;
        SpriteRenderer playerSr = null;

        if (playerObj != null)
        {
            topDownCtrl = playerObj.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            lvl2Ctrl = playerObj.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
            playerCol = playerObj.GetComponent<Collider2D>();
            playerAnim = playerObj.GetComponent<Animator>();
            playerSr = playerObj.GetComponent<SpriteRenderer>();

            if (topDownCtrl != null) topDownCtrl.enabled = false;
            if (lvl2Ctrl != null) lvl2Ctrl.enabled = false;
            if (playerWeapons != null) playerWeapons.enabled = false;
            if (playerRb != null) playerRb.velocity = Vector2.zero;
            if (playerAnim != null) playerAnim.SetBool("IsMoving", false);
            if (playerCol != null) playerCol.enabled = false;
        }

        // Ocultar elementos del HUD de juego como en el Nivel 1
        GameObject hudObj = GameObject.Find("Canvas_HUD");
        if (hudObj != null)
        {
            Transform hudChild = hudObj.transform.Find("HUD");
            if (hudChild != null) hudChild.gameObject.SetActive(false);
            Transform piedrasChild = hudObj.transform.Find("Piedras_HUD");
            if (piedrasChild != null) piedrasChild.gameObject.SetActive(false);
            Transform farolChild = hudObj.transform.Find("Farol_HUD");
            if (farolChild != null) farolChild.gameObject.SetActive(false);
            Transform busquedaChild = hudObj.transform.Find("BusquedaMonedas");
            if (busquedaChild != null) busquedaChild.gameObject.SetActive(false);
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        // 3. FASE 1 (Igual al Nivel 1): Mostrar primero el mensaje de VICTORIA ("ABANDONANDO EL PANTEÓN...") + confeti durante 2.4s
        ShowVictoryCard(showButtons: false, blackBackground: false);
        if (victoryCelebration != null) victoryCelebration.Show();

        if (victoryFanfare != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(victoryFanfare, Camera.main.transform.position, 1.0f);
        }

        yield return new WaitForSecondsRealtime(2.4f);

        // Desvanecer el mensaje inicial de victoria para dar paso a la caminata y apertura de la puerta derecha
        yield return FadeOutVictoryCardRoutine(0.25f);

        // 4. FASE 2: El jugador se acerca a la Puerta de Salida (Este en Nivel 2, Norte/Arriba en Nivel 3), la puerta se abre y el jugador cruza
        if (closedGateBlocker != null) closedGateBlocker.enabled = false;
        GameObject bardaSalida = GameObject.Find(isLvl3 ? "Barda norte" : "Barda este");
        if (bardaSalida != null)
        {
            foreach (var c in bardaSalida.GetComponentsInChildren<Collider2D>())
                if (c != null) c.enabled = false;
        }

        Vector3 gateApproachPos = isLvl3
            ? new Vector3(TopGateCenterX, 10.6f, 0f)
            : new Vector3(17.6f, GateCenterY, 0f);
        if (playerObj != null)
        {
            Vector3 startPos = playerObj.transform.position;
            float dist = Vector2.Distance(startPos, gateApproachPos);

            // Si está lejos de la puerta de salida, camina hacia ella
            if (dist > 0.35f)
            {
                float walkDuration = Mathf.Clamp(dist / 4.5f, 0.55f, 1.35f);
                float tWalk = 0f;
                Vector2 dir = (gateApproachPos - startPos).normalized;

                if (playerAnim != null)
                {
                    playerAnim.SetBool("IsMoving", true);
                    if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                    {
                        playerAnim.SetInteger("Direction", dir.x < 0f ? 3 : 2);
                        if (playerSr != null) playerSr.flipX = dir.x < 0f;
                    }
                    else
                    {
                        playerAnim.SetInteger("Direction", dir.y > 0f ? 1 : 0);
                        if (playerSr != null) playerSr.flipX = false;
                    }
                }

                while (tWalk < walkDuration)
                {
                    float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
                    tWalk += dt;
                    float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tWalk / walkDuration));
                    playerObj.transform.position = Vector3.Lerp(startPos, gateApproachPos, p);
                    yield return null;
                }
            }

            playerObj.transform.position = gateApproachPos;
            if (playerAnim != null)
            {
                playerAnim.SetBool("IsMoving", false);
                playerAnim.SetInteger("Direction", isLvl3 ? 1 : 2);
            }
            if (playerSr != null) playerSr.flipX = false;
        }

        // 5. Animación de apertura de las dos hojas de la Puerta de Salida (Frame 0 -> Frame 9)
        if (gateOpenClip == null)
        {
#if UNITY_EDITOR
            gateOpenClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/sfx_farol_attack.wav");
#endif
        }
        if (gateOpenClip != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(gateOpenClip, Camera.main.transform.position, 0.9f * AudioManager.EnemyVolume);
        }

        float gateOpenDuration = 1.25f;
        float gateElapsed = 0f;
        while (gateElapsed < gateOpenDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            gateElapsed += dt;
            float p = Mathf.Clamp01(gateElapsed / gateOpenDuration);
            float smoothP = Mathf.SmoothStep(0f, 1f, p);

            int fIdx = Mathf.Clamp(Mathf.RoundToInt(smoothP * (northGateFrames.Length - 1)), 0, northGateFrames.Length - 1);
            if (northLeafRenderer != null) northLeafRenderer.sprite = northGateFrames[fIdx];
            if (southLeafRenderer != null) southLeafRenderer.sprite = southGateFrames[fIdx];

            yield return null;
        }

        if (northLeafRenderer != null) northLeafRenderer.sprite = northGateFrames[northGateFrames.Length - 1];
        if (southLeafRenderer != null) southLeafRenderer.sprite = southGateFrames[southGateFrames.Length - 1];

        yield return new WaitForSecondsRealtime(0.12f);

        // 6. El jugador camina cruzando la Puerta de Salida abierta (hacia arriba en Nivel 3, hacia la derecha en Nivel 2)
        if (playerObj != null)
        {
            Vector3 walkStart = playerObj.transform.position;
            Vector3 walkEnd = isLvl3
                ? new Vector3(TopGateCenterX, 13.8f, 0f)
                : new Vector3(23.2f, GateCenterY, 0f);
            float walkOutDuration = 1.65f;
            float walkOutElapsed = 0f;

            if (playerAnim != null)
            {
                playerAnim.SetBool("IsMoving", true);
                playerAnim.SetInteger("Direction", isLvl3 ? 1 : 2);
            }
            if (playerSr != null) playerSr.flipX = false;

            while (walkOutElapsed < walkOutDuration)
            {
                float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
                walkOutElapsed += dt;
                float p = Mathf.Clamp01(walkOutElapsed / walkOutDuration);
                playerObj.transform.position = Vector3.Lerp(walkStart, walkEnd, p);
                yield return null;
            }

            if (playerAnim != null) playerAnim.SetBool("IsMoving", false);
        }

        // 7. FASE 3 (Igual al Nivel 1): Oscurecimiento con cierre de IrisInNivel hasta quedar la pantalla en negro total
        IrisInNivel iris = FindObjectOfType<IrisInNivel>(true);
        if (iris != null)
        {
            iris.gameObject.SetActive(false);
            iris.closing = true;
            iris.duracion = 1.0f;
            if (playerObj != null) iris.jugador = playerObj.transform;
            iris.gameObject.SetActive(true);
        }

        if (screenCanvasGroup != null)
        {
            screenCanvasGroup.gameObject.SetActive(true);
            screenCanvasGroup.alpha = 1f;
            screenCanvasGroup.interactable = true;
            screenCanvasGroup.blocksRaycasts = true;
        }
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.interactable = false;
            cardCanvasGroup.blocksRaycasts = false;
        }

        float fadeDuration = 1.05f;
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            fadeElapsed += dt;
            float p = Mathf.Clamp01(fadeElapsed / fadeDuration);
            if (blackoutImage != null)
            {
                blackoutImage.color = new Color(0f, 0f, 0f, p);
            }
            yield return null;
        }
        if (blackoutImage != null) blackoutImage.color = Color.black;

        // Mantener el título "¡VICTORIA!" arriba también en la pantalla final
        if (victoryCelebration != null) victoryCelebration.Show();

        // 8. FASE 4 (Igual a la Imagen 3 del Nivel 1): Mostrar la tarjeta de pergamino sobre fondo negro con los botones
        ShowVictoryCard(showButtons: true, blackBackground: true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        victoryCardInteractive = true;
    }

    private void ShowVictoryCard(bool showButtons, bool blackBackground)
    {
        if (screenCanvasGroup != null)
        {
            screenCanvasGroup.gameObject.SetActive(true);
            screenCanvasGroup.alpha = 1f;
            screenCanvasGroup.interactable = showButtons;
            screenCanvasGroup.blocksRaycasts = true;
        }

        if (blackoutImage != null)
        {
            blackoutImage.color = blackBackground
                ? Color.black
                : new Color(0.02f, 0.03f, 0.05f, 0.56f);
        }

        if (victoryTitleText != null) victoryTitleText.text = "VICTORIA";
        if (victoryBodyText != null)
        {
            victoryBodyText.text = IsLevel3Scene()
                ? "Recuperaste la Poción Morada, el Muñeco Vudú y la Vela.\nEl panteón vuelve a estar en calma."
                : "Recuperaste los tres juguetes.\nEl panteón vuelve a estar en calma.";
        }

        if (victoryHintText != null)
        {
            victoryHintText.text = "ABANDONANDO EL PANTEÓN...";
            victoryHintText.gameObject.SetActive(!showButtons);
        }

        if (buttonRowObj != null)
        {
            buttonRowObj.SetActive(showButtons);
        }

        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1f;
            cardCanvasGroup.interactable = showButtons;
            cardCanvasGroup.blocksRaycasts = showButtons;
        }
    }

    private IEnumerator FadeOutVictoryCardRoutine(float duration)
    {
        float elapsed = 0f;
        Color startBg = blackoutImage != null ? blackoutImage.color : Color.clear;
        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float a = 1f - Mathf.Clamp01(elapsed / duration);
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = a;
            if (blackoutImage != null) blackoutImage.color = new Color(startBg.r, startBg.g, startBg.b, startBg.a * a);
            yield return null;
        }
        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;
        if (blackoutImage != null) blackoutImage.color = Color.clear;
        if (screenCanvasGroup != null) screenCanvasGroup.blocksRaycasts = false;
    }

    #endregion

    #region Interacción Directa de los 4 Botones de Victoria

    private void HandleDirectVictoryButtonsInput()
    {
        Vector2 mousePos = Input.mousePosition;

        bool overNext = nextBtnRt != null && RectTransformUtility.RectangleContainsScreenPoint(nextBtnRt, mousePos, null);
        bool overRetry = retryBtnRt != null && RectTransformUtility.RectangleContainsScreenPoint(retryBtnRt, mousePos, null);
        bool overMenu = menuBtnRt != null && RectTransformUtility.RectangleContainsScreenPoint(menuBtnRt, mousePos, null);
        bool overQuit = quitBtnRt != null && RectTransformUtility.RectangleContainsScreenPoint(quitBtnRt, mousePos, null);

        if (nextBtnImg != null) nextBtnImg.color = overNext ? primaryBtnHover : primaryBtnNormal;
        if (retryBtnImg != null) retryBtnImg.color = overRetry ? secondaryBtnHover : secondaryBtnNormal;
        if (menuBtnImg != null) menuBtnImg.color = overMenu ? secondaryBtnHover : secondaryBtnNormal;
        if (quitBtnImg != null) quitBtnImg.color = overQuit ? secondaryBtnHover : secondaryBtnNormal;

        if (Input.GetMouseButtonDown(0))
        {
            if (overNext) { OnNextLevelClicked(); return; }
            if (overRetry) { OnRetryClicked(); return; }
            if (overMenu) { OnMenuClicked(); return; }
            if (overQuit) { OnQuitClicked(); return; }
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnNextLevelClicked();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            OnRetryClicked();
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            OnMenuClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnQuitClicked();
        }
    }

    public void OnNextLevelClicked()
    {
        if (actionInProgress) return;
        actionInProgress = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ToyEncounterZone.ResetAllEncounterStatics();

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int nextBuildIdx = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextBuildIdx < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(nextBuildIdx);
            }
            else
            {
                SceneManager.LoadScene(fallbackMenuSceneName);
            }
        }
    }

    public void OnRetryClicked()
    {
        if (actionInProgress) return;
        actionInProgress = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ToyEncounterZone.ResetAllEncounterStatics();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMenuClicked()
    {
        if (actionInProgress) return;
        actionInProgress = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ToyEncounterZone.ResetAllEncounterStatics();
        SceneManager.LoadScene(fallbackMenuSceneName);
    }

    public void OnQuitClicked()
    {
        if (actionInProgress) return;
        actionInProgress = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Construcción Procedural de la Tarjeta de Victoria idéntica a la Imagen 3 del Nivel 1

    private void HideImmediate()
    {
        if (screenCanvasGroup != null)
        {
            screenCanvasGroup.alpha = 0f;
            screenCanvasGroup.interactable = false;
            screenCanvasGroup.blocksRaycasts = false;
        }
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.interactable = false;
            cardCanvasGroup.blocksRaycasts = false;
        }
        if (blackoutImage != null)
        {
            blackoutImage.color = Color.clear;
        }
    }

    private void SetupProceduralUIIfMissing()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        Sprite parchmentSprite = null;
        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF");
#if UNITY_EDITOR
        parchmentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/CartaAntigua_Sello.png");
        if (pixelFont == null)
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
        if (pixelFont == null)
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/PressStart2P-Regular SDF.asset");
#endif
        if (parchmentSprite == null && ParchmentMessageUI.Instance != null)
        {
            parchmentSprite = ParchmentMessageUI.Instance.parchmentSprite;
        }
        if (pixelFont == null && ParchmentMessageUI.Instance != null)
        {
            pixelFont = ParchmentMessageUI.Instance.pixelFont;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("VictoryCanvas_Nivel2");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2100;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        screenCanvasGroup = GetComponent<CanvasGroup>();
        if (screenCanvasGroup == null) screenCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // Fondo oscuro / negro completo (idéntico a Imagen 3)
        blackoutImage = GetComponent<Image>();
        if (blackoutImage == null) blackoutImage = gameObject.AddComponent<Image>();
        blackoutImage.color = Color.clear;

        // Banner superior "¡VICTORIA!" + Confeti pixel-art de celebración (idéntico a Victoria_Pixel del Nivel 1)
        GameObject celebObj = new GameObject("Victoria_Pixel");
        celebObj.transform.SetParent(transform, false);
        RectTransform celebRt = celebObj.AddComponent<RectTransform>();
        celebRt.anchorMin = new Vector2(0.5f, 0.86f);
        celebRt.anchorMax = new Vector2(0.5f, 0.86f);
        celebRt.pivot = new Vector2(0.5f, 0.5f);
        celebRt.anchoredPosition = Vector2.zero;
        celebRt.sizeDelta = new Vector2(800f, 180f);
        celebObj.AddComponent<CanvasGroup>();

        // Sombra del título "¡VICTORIA!" arriba
        GameObject shadowTitleObj = new GameObject("Victoria_Sombra");
        shadowTitleObj.transform.SetParent(celebObj.transform, false);
        RectTransform shadowRt = shadowTitleObj.AddComponent<RectTransform>();
        shadowRt.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRt.pivot = new Vector2(0.5f, 0.5f);
        shadowRt.anchoredPosition = new Vector2(3f, -3f);
        shadowRt.sizeDelta = new Vector2(720f, 90f);
        TextMeshProUGUI shadowTmp = shadowTitleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) shadowTmp.font = pixelFont;
        shadowTmp.text = "¡VICTORIA!";
        shadowTmp.alignment = TextAlignmentOptions.Center;
        shadowTmp.fontSize = 48f;
        shadowTmp.color = new Color(0.153f, 0.114f, 0.067f, 1f);
        shadowTmp.raycastTarget = false;

        // Texto principal "¡VICTORIA!" arriba (dorado brillante)
        GameObject mainTopTitleObj = new GameObject("Victoria_Texto");
        mainTopTitleObj.transform.SetParent(celebObj.transform, false);
        RectTransform mainTopRt = mainTopTitleObj.AddComponent<RectTransform>();
        mainTopRt.anchorMin = new Vector2(0.5f, 0.5f);
        mainTopRt.anchorMax = new Vector2(0.5f, 0.5f);
        mainTopRt.pivot = new Vector2(0.5f, 0.5f);
        mainTopRt.anchoredPosition = Vector2.zero;
        mainTopRt.sizeDelta = new Vector2(720f, 90f);
        TextMeshProUGUI mainTopTmp = mainTopTitleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) mainTopTmp.font = pixelFont;
        mainTopTmp.text = "¡VICTORIA!";
        mainTopTmp.alignment = TextAlignmentOptions.Center;
        mainTopTmp.fontSize = 48f;
        mainTopTmp.color = new Color(0.957f, 0.851f, 0.565f, 1f);
        mainTopTmp.raycastTarget = false;

        victoryCelebration = celebObj.AddComponent<VictoryCelebration>();

        // Tarjeta de pergamino con sello rojo a la izquierda (CartaAntigua_Sello.png, 620x252, igual que en Nivel 1 / Imagen 3)
        GameObject cardObj = new GameObject("Tarjeta_Victoria");
        cardObj.transform.SetParent(transform, false);
        cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, -10f);
        cardRect.sizeDelta = new Vector2(620f, 252f);
        cardRect.localScale = new Vector3(1.22f, 1.22f, 1f);

        cardCanvasGroup = cardObj.AddComponent<CanvasGroup>();
        cardBackground = cardObj.AddComponent<Image>();
        if (parchmentSprite != null)
        {
            cardBackground.sprite = parchmentSprite;
            cardBackground.color = Color.white;
        }
        else
        {
            cardBackground.color = new Color32(230, 217, 184, 255);
        }

        // Título "VICTORIA" (alineado a la izquierda sobre el pergamino igual que en Nivel 1)
        GameObject titleObj = new GameObject("Titulo_Resultado");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(34f, 88f);
        titleRt.sizeDelta = new Vector2(500f, 30f);
        victoryTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) victoryTitleText.font = pixelFont;
        victoryTitleText.text = "VICTORIA";
        victoryTitleText.alignment = TextAlignmentOptions.TopLeft;
        victoryTitleText.fontSize = 18f;
        victoryTitleText.color = new Color(0.5f, 0.36f, 0.2f, 1f);

        // Texto principal del pergamino (grande y claro igual que en Nivel 1)
        GameObject bodyObj = new GameObject("Texto_Resultado");
        bodyObj.transform.SetParent(cardObj.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(34f, 6f);
        bodyRt.sizeDelta = new Vector2(500f, 124f);
        victoryBodyText = bodyObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) victoryBodyText.font = pixelFont;
        victoryBodyText.text = "Recuperaste los tres juguetes.\nEl panteón vuelve a estar en calma.";
        victoryBodyText.alignment = TextAlignmentOptions.TopLeft;
        victoryBodyText.fontSize = 16f;
        victoryBodyText.lineSpacing = 12f;
        victoryBodyText.color = new Color(0.23f, 0.18f, 0.13f, 1f);
        victoryBodyText.enableWordWrapping = true;

        // Texto inferior durante la primera parte de la cinemática ("ABANDONANDO EL PANTEÓN...")
        GameObject hintObj = new GameObject("Ayuda_Resultado");
        hintObj.transform.SetParent(cardObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.anchoredPosition = new Vector2(34f, -92f);
        hintRt.sizeDelta = new Vector2(500f, 32f);
        victoryHintText = hintObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) victoryHintText.font = pixelFont;
        victoryHintText.text = "ABANDONANDO EL PANTEÓN...";
        victoryHintText.alignment = TextAlignmentOptions.TopLeft;
        victoryHintText.fontSize = 13f;
        victoryHintText.color = new Color(0.23f, 0.18f, 0.13f, 1f);

        // Fila inferior de botones dentro del pergamino (idéntica a Imagen 3: REINTENTAR, MENU PRINCIPAL, SIGUIENTE, SALIR)
        buttonRowObj = new GameObject("Botones_Resultado");
        buttonRowObj.transform.SetParent(cardObj.transform, false);
        RectTransform rowRt = buttonRowObj.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(34f, -92f);
        rowRt.sizeDelta = new Vector2(500f, 34f);

        var layout = buttonRowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        retryButton = CreateParchmentButton(
            buttonRowObj.transform, "Boton_Reintentar", "REINTENTAR",
            primaryBtnNormal, pixelFont, 11f, out retryBtnRt, out retryBtnImg);
        retryButton.onClick.AddListener(OnRetryClicked);

        menuButton = CreateParchmentButton(
            buttonRowObj.transform, "Boton_Menu", "MENU PRINCIPAL",
            secondaryBtnNormal, pixelFont, 9.5f, out menuBtnRt, out menuBtnImg);
        menuButton.onClick.AddListener(OnMenuClicked);

        nextLevelButton = CreateParchmentButton(
            buttonRowObj.transform, "Boton_Siguiente", "SIGUIENTE",
            primaryBtnNormal, pixelFont, 11f, out nextBtnRt, out nextBtnImg);
        nextLevelButton.onClick.AddListener(OnNextLevelClicked);

        quitButton = CreateParchmentButton(
            buttonRowObj.transform, "Boton_Salir", "SALIR",
            secondaryBtnNormal, pixelFont, 11f, out quitBtnRt, out quitBtnImg);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private Button CreateParchmentButton(
        Transform parent,
        string name,
        string label,
        Color baseColor,
        TMP_FontAsset font,
        float fontSize,
        out RectTransform btnRt,
        out Image btnImg)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        btnRt = btnObj.AddComponent<RectTransform>();

        btnImg = btnObj.AddComponent<Image>();
        btnImg.color = baseColor;
        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Texto");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(4f, 2f);
        txtRt.offsetMax = new Vector2(-4f, -2f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = fontSize;
        tmp.color = new Color(1f, 0.91f, 0.65f, 1f);
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        return btn;
    }

    #endregion
}
