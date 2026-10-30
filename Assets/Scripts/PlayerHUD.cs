using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Datos del jugador")]
    public PlayerHealth playerHealth;
    public GoldCoinCollector coinCollector;
    public ToyCollector toyCollector;

    [Header("Vida")]
    public TMP_Text textoVida;
    public Image[] corazones = new Image[5];
    public Sprite corazonLleno;
    public Sprite corazonVacio;

    [Header("Monedas o Juguetes")]
    public TMP_Text textoMonedas;
    public Image[] monedas = new Image[3];
    public Sprite[] spritesJuguetes;

    private int ultimaVida = -1;
    private int ultimaVidaMaxima = -1;
    private int ultimasMonedas = -1;
    private int ultimaMetaMonedas = -1;
    private int ultimoToyMask = -1;
    private bool initializedToys = false;

    private void Start()
    {
        ResolveReferences();
        CheckInitToys();
    }

    private void ResolveReferences()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (toyCollector == null)
            toyCollector = FindObjectOfType<ToyCollector>();

        if (coinCollector == null)
            coinCollector = FindObjectOfType<GoldCoinCollector>();
    }

    public bool IsToyMode()
    {
        if (toyCollector != null) return true;
        var tc = FindObjectOfType<ToyCollector>();
        if (tc != null)
        {
            toyCollector = tc;
            return true;
        }
        var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return sceneName.IndexOf("Nivel2", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level2", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void CheckInitToys()
    {
        if (!IsToyMode()) return;
        if (initializedToys && spritesJuguetes != null && spritesJuguetes.Length >= 3 && spritesJuguetes[0] != null) return;

        if (spritesJuguetes == null || spritesJuguetes.Length < 3 || spritesJuguetes[0] == null)
        {
#if UNITY_EDITOR
            spritesJuguetes = new Sprite[3]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Bear.png"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Ball.png"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Train.png")
            };
#endif
        }

        if (spritesJuguetes != null && spritesJuguetes.Length >= 3 && monedas != null)
        {
            for (int i = 0; i < Mathf.Min(monedas.Length, spritesJuguetes.Length); i++)
            {
                if (monedas[i] != null && spritesJuguetes[i] != null)
                {
                    monedas[i].sprite = spritesJuguetes[i];
                    monedas[i].preserveAspect = true;
                    var rt = monedas[i].rectTransform;
                    if (rt != null)
                    {
                        rt.sizeDelta = new Vector2(26f, 26f);
                    }
                }
            }
            initializedToys = true;
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();
        bool toyMode = IsToyMode();
        if (toyMode) CheckInitToys();

        if (playerHealth == null || (!toyMode && coinCollector == null && toyCollector == null))
            return;

        int vida = (playerHealth.maxHealth > 5 || playerHealth.HealthPerHeart > 1)
            ? Mathf.Clamp(playerHealth.ActiveHearts, 0, 5)
            : Mathf.Clamp(playerHealth.currentHealth, 0, 5);
        int vidaMaxima = 5;

        int recogidas = toyMode
            ? (toyCollector != null ? toyCollector.Count : 0)
            : (coinCollector != null ? coinCollector.collectedCoins : 0);

        int meta = toyMode
            ? ToyCollector.RequiredToys
            : (coinCollector != null ? coinCollector.requiredCoins : 3);

        int currentToyMask = 0;
        if (toyMode && toyCollector != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (toyCollector.HasToy(i)) currentToyMask |= (1 << i);
            }
        }

        if (vida == ultimaVida &&
            vidaMaxima == ultimaVidaMaxima &&
            recogidas == ultimasMonedas &&
            meta == ultimaMetaMonedas &&
            (!toyMode || currentToyMask == ultimoToyMask))
            return;

        if (textoVida != null)
            textoVida.text = $"{vida}/{vidaMaxima}";

        for (int i = 0; i < corazones.Length; i++)
        {
            if (corazones[i] == null)
                continue;

            corazones[i].sprite =
                i < vida ? corazonLleno : corazonVacio;

            corazones[i].color = Color.white;
        }

        if (textoMonedas != null)
            textoMonedas.text = $"{recogidas}/{meta}";

        for (int i = 0; i < monedas.Length; i++)
        {
            if (monedas[i] == null)
                continue;

            if (toyMode && toyCollector != null)
            {
                bool hasThisToy = toyCollector.HasToy(i);
                monedas[i].color = hasThisToy
                    ? Color.white
                    : new Color(0.28f, 0.28f, 0.38f, 0.42f);
                monedas[i].transform.localScale = hasThisToy ? Vector3.one : new Vector3(0.92f, 0.92f, 1f);
            }
            else
            {
                monedas[i].color = i < recogidas
                    ? Color.white
                    : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
        }

        ultimaVida = vida;
        ultimaVidaMaxima = vidaMaxima;
        ultimasMonedas = recogidas;
        ultimaMetaMonedas = meta;
        ultimoToyMask = currentToyMask;
    }
}
