using UnityEngine;
using UnityEngine.UI;

public class GoblinHealthUI : MonoBehaviour
{
    public GoblinAwarenessUI awareness;
    public CanvasGroup[] bars;
    public RectTransform[] fills;
    public Vector3 headOffset = new Vector3(0f, 1.2f, 0f);

    private void LateUpdate()
    {
        if (bars == null) return;
        for (int i = 0; i < bars.Length; i++)
        {
            CanvasGroup bar = bars[i];
            if (bar == null) continue;
            bar.alpha = 0f;
            if (awareness == null || awareness.goblins == null || i >= awareness.goblins.Length ||
                awareness.canvasRect == null || awareness.worldCamera == null ||
                fills == null || i >= fills.Length || fills[i] == null)
                continue;
            GoblinAI goblin = awareness.goblins[i];
            if (goblin == null || !goblin.isActiveAndEnabled ||
                (awareness.playerHealth != null && awareness.playerHealth.currentHealth <= 0) ||
                (awareness.collector != null && awareness.collector.IsLevelFinished))
                continue;
            GoblinCombat combat = goblin.GetComponent<GoblinCombat>();
            if (combat == null || combat.IsBanished) continue;

            Vector3 screen = awareness.worldCamera.WorldToScreenPoint(goblin.transform.position + headOffset);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height) continue;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                awareness.canvasRect, screen, null, out local);
            ((RectTransform)bar.transform).anchoredPosition = local;
            float ratio = Mathf.Clamp01((float)combat.CurrentHealth / Mathf.Max(1, combat.maxHealth));
            fills[i].anchorMax = new Vector2(ratio, 1f);
            fills[i].offsetMin = Vector2.zero;
            fills[i].offsetMax = Vector2.zero;
            Image fill = fills[i].GetComponent<Image>();
            if (fill != null) fill.color = ratio > 0.67f
                ? new Color(0.49f, 0.78f, 0.45f)
                : (ratio > 0.34f ? new Color(0.95f, 0.73f, 0.29f) : new Color(0.91f, 0.37f, 0.26f));
            bar.alpha = 1f;
        }
    }

    private void OnDisable()
    {
        if (bars == null) return;
        foreach (CanvasGroup bar in bars)
            if (bar != null) bar.alpha = 0f;
    }
}
