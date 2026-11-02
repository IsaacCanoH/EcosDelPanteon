using UnityEngine;
using UnityEngine.UI;

public class GoblinHealthUI : MonoBehaviour
{
    public GoblinAwarenessUI awareness;
    public CanvasGroup[] bars;
    public RectTransform[] fills;
    public Vector3 headOffset = new Vector3(0f, 1.2f, 0f);

    private const int DesiredMaxSlots = 12;

    private void Awake()
    {
        if (bars != null)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] != null)
                {
                    bars[i].gameObject.SetActive(true);
                    bars[i].alpha = 0f;
                }
            }
        }
        EnsureCapacity(DesiredMaxSlots);
    }

    public void EnsureCapacity(int minSlots)
    {
        if (bars == null || bars.Length == 0 || bars[0] == null)
            return;

        if (bars.Length >= minSlots && fills != null && fills.Length >= minSlots)
            return;

        int oldCount = bars.Length;
        int newCount = Mathf.Max(minSlots, oldCount);

        CanvasGroup templateBar = bars[0];
        Transform parent = templateBar.transform.parent;

        var newBars = new CanvasGroup[newCount];
        var newFills = new RectTransform[newCount];

        for (int i = 0; i < oldCount; i++)
        {
            newBars[i] = bars[i];
            if (fills != null && i < fills.Length) newFills[i] = fills[i];
        }

        for (int i = oldCount; i < newCount; i++)
        {
            GameObject clone = Instantiate(templateBar.gameObject, parent);
            clone.name = "Barra_Vida_Duende_" + (i + 1);
            clone.SetActive(true);
            CanvasGroup cg = clone.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            newBars[i] = cg;

            RectTransform cloneRt = clone.GetComponent<RectTransform>();
            if (cloneRt != null)
            {
                cloneRt.pivot = new Vector2(0.5f, 0.5f);
            }

            // Buscar el relleno interno Relleno_Vida
            RectTransform fillRt = null;
            Transform directFill = clone.transform.Find("Fondo/Relleno_Vida");
            if (directFill == null) directFill = clone.transform.Find("Relleno_Vida");
            if (directFill != null)
            {
                fillRt = directFill.GetComponent<RectTransform>();
            }
            else
            {
                foreach (var img in clone.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject.name.Contains("Relleno"))
                    {
                        fillRt = img.rectTransform;
                        break;
                    }
                }
            }

            if (fillRt == null && clone.transform.childCount > 0)
            {
                Transform child0 = clone.transform.GetChild(0);
                if (child0.childCount > 0)
                    fillRt = child0.GetChild(0).GetComponent<RectTransform>();
                else
                    fillRt = child0.GetComponent<RectTransform>();
            }

            newFills[i] = fillRt;
        }

        bars = newBars;
        fills = newFills;
    }

    private void LateUpdate()
    {
        var activeList = GoblinAI.ActiveAgents;
        int activeCount = activeList != null ? activeList.Count : 0;

        if (bars != null && activeCount > bars.Length)
        {
            EnsureCapacity(activeCount + 4);
        }

        // Ocultar todas las barras primero
        if (bars != null)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] != null) bars[i].alpha = 0f;
            }
        }

        Camera cam = awareness != null && awareness.worldCamera != null ? awareness.worldCamera : Camera.main;
        if (cam == null || bars == null) return;
        if (awareness != null && awareness.playerHealth != null && awareness.playerHealth.currentHealth <= 0) return;
        if (awareness != null && awareness.collector != null && awareness.collector.isActiveAndEnabled && awareness.collector.IsLevelFinished) return;
        if (activeList == null || activeCount == 0) return;

        int slot = 0;
        for (int k = 0; k < activeCount; k++)
        {
            if (slot >= bars.Length) break;
            GoblinAI goblin = activeList[k];
            if (goblin == null || !goblin.isActiveAndEnabled) continue;

            EnemyHealth enemyHealth = goblin.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsDead) continue;

            GoblinCombat combat = goblin.GetComponent<GoblinCombat>();
            if (combat != null && combat.IsBanished) continue;

            CanvasGroup bar = bars[slot];
            if (bar == null || fills == null || slot >= fills.Length || fills[slot] == null)
            {
                slot++;
                continue;
            }

            float ratio = 1f;
            if (enemyHealth != null && enemyHealth.isActiveAndEnabled)
            {
                ratio = Mathf.Clamp01((float)enemyHealth.currentHealth / Mathf.Max(1, enemyHealth.maxHealth));
            }
            else if (combat != null && combat.isActiveAndEnabled)
            {
                ratio = Mathf.Clamp01((float)combat.CurrentHealth / Mathf.Max(1, combat.maxHealth));
            }

            // Calcular el punto superior centrado exactamente en el sprite/colisionador del duende
            SpriteRenderer sr = goblin.GetComponent<SpriteRenderer>();
            Collider2D col = goblin.GetComponent<Collider2D>();

            float centerX = sr != null ? sr.bounds.center.x : (col != null ? col.bounds.center.x : goblin.transform.position.x);
            // Barra de vida ajustada justo encima de la cabeza del duende
            float topY = sr != null 
                ? (sr.bounds.max.y - (sr.bounds.size.y * 0.28f)) 
                : (col != null ? col.bounds.max.y + 0.90f : goblin.transform.position.y + 1.15f);
            Vector3 worldPos = new Vector3(centerX, topY, goblin.transform.position.z);

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height)
            {
                slot++;
                continue;
            }

            RectTransform barRt = (RectTransform)bar.transform;
            RectTransform parentRt = barRt.parent as RectTransform ?? (awareness != null ? awareness.canvasRect : null);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, screen, null, out local);

            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = local;

            fills[slot].anchorMin = Vector2.zero;
            fills[slot].anchorMax = new Vector2(ratio, 1f);
            fills[slot].offsetMin = Vector2.zero;
            fills[slot].offsetMax = Vector2.zero;

            Image fillImg = fills[slot].GetComponent<Image>() ?? fills[slot].GetComponentInChildren<Image>();
            if (fillImg != null)
            {
                if (ratio > 0.66f)
                    fillImg.color = new Color(0.28f, 0.85f, 0.35f, 1f);  // Verde
                else if (ratio > 0.33f)
                    fillImg.color = new Color(0.95f, 0.78f, 0.22f, 1f);  // Amarillo
                else
                    fillImg.color = new Color(0.92f, 0.25f, 0.20f, 1f);  // Rojo
            }

            bar.gameObject.SetActive(true);
            bar.alpha = 1f;
            slot++;
        }
    }

    private void OnDisable()
    {
        if (bars == null) return;
        foreach (CanvasGroup group in bars)
            if (group != null) group.alpha = 0f;
    }
}
