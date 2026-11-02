using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoblinAwarenessUI : MonoBehaviour
{
    public static GoblinAwarenessUI Instance { get; private set; }

    public GoblinAI[] goblins;
    public Image[] stunStars;
    public TMP_Text[] stunLabels;
    public TMP_Text[] labels;
    public CanvasGroup[] indicators;
    public RectTransform canvasRect;
    public Camera worldCamera;
    public PlayerHealth playerHealth;
    public GoldCoinCollector collector;
    public Vector3 headOffset = new Vector3(0f, 1.75f, 0f);

    private const int DesiredMaxSlots = 12;

    private void Awake()
    {
        Instance = this;
        if (indicators != null)
        {
            for (int i = 0; i < indicators.Length; i++)
            {
                if (indicators[i] != null)
                {
                    indicators[i].gameObject.SetActive(true);
                    indicators[i].alpha = 0f;
                }
            }
        }
        EnsureCapacity(DesiredMaxSlots);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void EnsureCapacity(int minSlots)
    {
        if (indicators == null || indicators.Length == 0 || indicators[0] == null)
            return;

        if (indicators.Length >= minSlots && goblins != null && goblins.Length >= minSlots)
            return;

        int oldCount = indicators.Length;
        int newCount = Mathf.Max(minSlots, oldCount);

        CanvasGroup templateGroup = indicators[0];
        Transform parent = templateGroup.transform.parent;

        var newGoblins = new GoblinAI[newCount];
        if (goblins != null)
        {
            for (int i = 0; i < Mathf.Min(goblins.Length, newCount); i++)
                newGoblins[i] = goblins[i];
        }

        var newIndicators = new CanvasGroup[newCount];
        var newLabels = new TMP_Text[newCount];
        var newStunLabels = new TMP_Text[newCount];
        var newStunStars = new Image[newCount * 3];

        for (int i = 0; i < oldCount; i++)
        {
            newIndicators[i] = indicators[i];
            if (labels != null && i < labels.Length) newLabels[i] = labels[i];
            if (stunLabels != null && i < stunLabels.Length) newStunLabels[i] = stunLabels[i];
            if (stunStars != null)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (i * 3 + j < stunStars.Length)
                        newStunStars[i * 3 + j] = stunStars[i * 3 + j];
                }
            }
        }

        for (int i = oldCount; i < newCount; i++)
        {
            GameObject clone = Instantiate(templateGroup.gameObject, parent);
            clone.name = "Indicador_Duende_" + (i + 1);
            clone.SetActive(true);
            CanvasGroup cg = clone.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            newIndicators[i] = cg;

            RectTransform cloneRt = clone.GetComponent<RectTransform>();
            if (cloneRt != null) cloneRt.pivot = new Vector2(0.5f, 0.5f);

            Transform signo = clone.transform.Find("Simbolo");
            if (signo == null) signo = clone.transform.Find("Signo");
            if (signo != null)
            {
                newLabels[i] = signo.GetComponent<TMP_Text>();
            }
            else
            {
                foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.gameObject.name.Contains("Simbolo") || tmp.gameObject.name.Contains("Signo"))
                    {
                        newLabels[i] = tmp;
                        break;
                    }
                }
            }

            Transform tiempo = clone.transform.Find("Tiempo_Aturdimiento");
            if (tiempo == null) tiempo = clone.transform.Find("Tiempo_Aturdido");
            if (tiempo != null)
            {
                newStunLabels[i] = tiempo.GetComponent<TMP_Text>();
            }
            else
            {
                foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.gameObject.name.Contains("Tiempo"))
                    {
                        newStunLabels[i] = tmp;
                        break;
                    }
                }
            }

            for (int j = 0; j < 3; j++)
            {
                Transform starTr = clone.transform.Find("Estrella_" + (j + 1));
                if (starTr != null) newStunStars[i * 3 + j] = starTr.GetComponent<Image>();
            }
        }

        goblins = newGoblins;
        indicators = newIndicators;
        labels = newLabels;
        stunLabels = newStunLabels;
        stunStars = newStunStars;
    }

    public void Track(GoblinAI goblin)
    {
        if (goblin == null) return;
        var list = GoblinAI.ActiveAgents;
        int count = list != null ? list.Count : 0;
        if (indicators != null && count >= indicators.Length)
        {
            EnsureCapacity(count + 4);
        }
    }

    private void LateUpdate()
    {
        var activeList = GoblinAI.ActiveAgents;
        int activeCount = activeList != null ? activeList.Count : 0;

        if (indicators != null && activeCount > indicators.Length)
        {
            EnsureCapacity(activeCount + 4);
        }

        // Ocultar todos los indicadores por defecto
        if (indicators != null)
        {
            for (int i = 0; i < indicators.Length; i++)
            {
                if (indicators[i] != null) indicators[i].alpha = 0f;
            }
        }

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null || canvasRect == null) return;
        if (playerHealth != null && playerHealth.currentHealth <= 0) return;
        if (collector != null && collector.isActiveAndEnabled && collector.IsLevelFinished) return;
        if (activeList == null || activeCount == 0) return;

        // Mantener sincronizado el arreglo goblins
        if (goblins == null || goblins.Length < activeCount)
        {
            goblins = new GoblinAI[Mathf.Max(activeCount, indicators != null ? indicators.Length : DesiredMaxSlots)];
        }

        int slot = 0;
        for (int k = 0; k < activeCount; k++)
        {
            if (indicators == null || slot >= indicators.Length) break;
            GoblinAI goblin = activeList[k];
            if (goblin == null || !goblin.isActiveAndEnabled) continue;

            EnemyHealth enemyHealth = goblin.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsDead) continue;

            GoblinCombat combat = goblin.GetComponent<GoblinCombat>();
            if (combat != null && combat.IsBanished) continue;

            goblins[slot] = goblin;
            CanvasGroup group = indicators[slot];
            if (group == null)
            {
                slot++;
                continue;
            }

            // Asegurar referencias a textos de este slot
            if (labels != null && slot < labels.Length && labels[slot] == null)
            {
                Transform signo = group.transform.Find("Simbolo");
                if (signo == null) signo = group.transform.Find("Signo");
                if (signo != null) labels[slot] = signo.GetComponent<TMP_Text>();
            }

            if (stunLabels != null && slot < stunLabels.Length && stunLabels[slot] == null)
            {
                Transform tiempo = group.transform.Find("Tiempo_Aturdimiento");
                if (tiempo == null) tiempo = group.transform.Find("Tiempo_Aturdido");
                if (tiempo != null) stunLabels[slot] = tiempo.GetComponent<TMP_Text>();
            }

            // Detección de combate / alerta / sospecha
            bool isWaveOrCombat = !goblin.enablePatrol || goblin.HasPermanentAlert;
            bool alerted = isWaveOrCombat || goblin.IsAlerted || goblin.CurrentAwareness == GoblinAI.AwarenessState.Chasing;
            bool curious = !isWaveOrCombat && (goblin.CurrentAwareness == GoblinAI.AwarenessState.Suspicious ||
                                               goblin.CurrentAwareness == GoblinAI.AwarenessState.Searching);
            bool stunned = goblin.IsStunned;

            if (!alerted && !curious && !stunned)
            {
                slot++;
                continue;
            }

            // Posición centrada horizontalmente sobre el duende
            SpriteRenderer sr = goblin.GetComponent<SpriteRenderer>();
            Collider2D col = goblin.GetComponent<Collider2D>();

            float centerX = sr != null ? sr.bounds.center.x : (col != null ? col.bounds.center.x : goblin.transform.position.x);
            // El icono de alerta/sospecha se sitúa justo encima de la barra de vida
            float topY = sr != null 
                ? (sr.bounds.max.y - (sr.bounds.size.y * 0.28f) + 0.38f) 
                : (col != null ? col.bounds.max.y + 1.28f : goblin.transform.position.y + 1.53f);
            Vector3 worldPos = new Vector3(centerX, topY, goblin.transform.position.z);

            Vector3 screen = worldCamera.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height)
            {
                slot++;
                continue;
            }

            RectTransform groupRt = (RectTransform)group.transform;
            RectTransform parentRt = groupRt.parent as RectTransform ?? canvasRect;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, screen, null, out local);

            groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = local;
            groupRt.sizeDelta = stunned ? new Vector2(78f, 58f) : new Vector2(34f, 40f);

            if (stunned)
            {
                if (labels != null && slot < labels.Length && labels[slot] != null)
                    labels[slot].gameObject.SetActive(false);

                if (stunLabels != null && slot < stunLabels.Length && stunLabels[slot] != null)
                {
                    stunLabels[slot].gameObject.SetActive(true);
                    stunLabels[slot].text = goblin.StunRemaining.ToString("0.0", CultureInfo.InvariantCulture) + " s";
                }
                for (int j = 0; j < 3; j++)
                {
                    int index = slot * 3 + j;
                    if (stunStars == null || index >= stunStars.Length || stunStars[index] == null) continue;
                    Image star = stunStars[index];
                    star.gameObject.SetActive(true);
                    float angle = Time.time * 4f + j * Mathf.PI * 2f / 3f;
                    star.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * 23f, 8f + Mathf.Sin(angle) * 7f);
                    star.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, (Mathf.Sin(angle) + 1f) * 0.5f);
                }
                group.gameObject.SetActive(true);
                group.alpha = 1f;
                slot++;
                continue;
            }

            // Ocultar elementos de aturdimiento si no está aturdido
            if (stunLabels != null && slot < stunLabels.Length && stunLabels[slot] != null)
                stunLabels[slot].gameObject.SetActive(false);
            for (int j = 0; j < 3; j++)
            {
                int index = slot * 3 + j;
                if (stunStars != null && index < stunStars.Length && stunStars[index] != null)
                    stunStars[index].gameObject.SetActive(false);
            }

            if (labels != null && slot < labels.Length && labels[slot] != null)
            {
                labels[slot].gameObject.SetActive(true);
                labels[slot].text = alerted ? "!" : "?";
                labels[slot].color = alerted
                    ? new Color(1f, 0.28f, 0.22f, 1f)
                    : new Color(1f, 0.85f, 0.3f, 1f);
            }

            group.gameObject.SetActive(true);
            group.alpha = (!isWaveOrCombat && goblin.CurrentAwareness == GoblinAI.AwarenessState.Suspicious)
                ? Mathf.Lerp(0.65f, 1f, goblin.SuspicionProgress) : 1f;

            slot++;
        }
    }

    private void OnDisable()
    {
        if (indicators == null) return;
        foreach (CanvasGroup group in indicators)
            if (group != null) group.alpha = 0f;
    }
}
