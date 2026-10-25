using TMPro;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine;

public class GoblinAwarenessUI : MonoBehaviour
{
    public GoblinAI[] goblins;
    public Image[] stunStars;
    public TMP_Text[] stunLabels;
    public TMP_Text[] labels;
    public CanvasGroup[] indicators;
    public RectTransform canvasRect;
    public Camera worldCamera;
    public PlayerHealth playerHealth;
    public GoldCoinCollector collector;
    public Vector3 headOffset = new Vector3(0f, 1.1f, 0f);

    private void LateUpdate()
    {
        if (goblins == null || labels == null || indicators == null) return;
        for (int i = 0; i < indicators.Length; i++)
        {
            CanvasGroup group = indicators[i];
            if (group == null) continue;
            group.alpha = 0f;
            if (i < labels.Length && labels[i] != null) labels[i].gameObject.SetActive(true);
            if (stunLabels != null && i < stunLabels.Length && stunLabels[i] != null)
                stunLabels[i].gameObject.SetActive(false);
            for (int j = 0; j < 3; j++)
                if (stunStars != null && i * 3 + j < stunStars.Length && stunStars[i * 3 + j] != null)
                    stunStars[i * 3 + j].gameObject.SetActive(false);
            if (i >= goblins.Length || i >= labels.Length ||
                labels[i] == null || goblins[i] == null ||
                canvasRect == null || worldCamera == null)
                continue;

            GoblinAI goblin = goblins[i];
            if (!goblin.isActiveAndEnabled || !goblin.enablePatrol ||
                (playerHealth != null && playerHealth.currentHealth <= 0) ||
                (collector != null && collector.IsLevelFinished))
                continue;

            bool alerted = goblin.IsAlerted;
            bool curious = goblin.CurrentAwareness == GoblinAI.AwarenessState.Suspicious ||
                goblin.CurrentAwareness == GoblinAI.AwarenessState.Searching;
            bool stunned = goblin.IsStunned;
            if (!alerted && !curious && !stunned) continue;

            Vector3 screen = worldCamera.WorldToScreenPoint(
                goblin.transform.position + headOffset);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height)
                continue;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out local);
            ((RectTransform)group.transform).anchoredPosition = local;
            ((RectTransform)group.transform).sizeDelta = stunned ? new Vector2(78f, 58f) : new Vector2(34f, 40f);
            if (stunned)
            {
                labels[i].gameObject.SetActive(false);
                if (stunLabels != null && i < stunLabels.Length && stunLabels[i] != null)
                {
                    stunLabels[i].gameObject.SetActive(true);
                    stunLabels[i].text = goblin.StunRemaining.ToString("0.0", CultureInfo.InvariantCulture) + " s";
                }
                for (int j = 0; j < 3; j++)
                {
                    int index = i * 3 + j;
                    if (stunStars == null || index >= stunStars.Length || stunStars[index] == null) continue;
                    Image star = stunStars[index];
                    star.gameObject.SetActive(true);
                    float angle = Time.time * 4f + j * Mathf.PI * 2f / 3f;
                    star.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * 23f, 8f + Mathf.Sin(angle) * 7f);
                    star.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, (Mathf.Sin(angle) + 1f) * 0.5f);
                }
                group.alpha = 1f;
                continue;
            }
            labels[i].text = alerted ? "!" : "?";
            labels[i].color = alerted
                ? new Color(1f, 0.3f, 0.22f, 1f)
                : new Color(1f, 0.85f, 0.3f, 1f);
            group.alpha = goblin.CurrentAwareness == GoblinAI.AwarenessState.Suspicious
                ? Mathf.Lerp(0.65f, 1f, goblin.SuspicionProgress) : 1f;
        }
    }

    public void Track(GoblinAI goblin)
    {
        if (goblin == null || goblins == null) return;
        foreach (var current in goblins) if (current == goblin) return;
        for (int i = 0; i < goblins.Length; i++)
            if (goblins[i] == null || !goblins[i].isActiveAndEnabled)
            {
                goblins[i] = goblin;
                return;
            }
    }

    private void OnDisable()
    {
        if (indicators == null) return;
        foreach (CanvasGroup group in indicators)
            if (group != null) group.alpha = 0f;
    }
}
