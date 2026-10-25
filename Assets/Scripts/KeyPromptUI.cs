using TMPro;
using UnityEngine;

// Mantiene el indicador visual junto al texto al que pertenece.
[DefaultExecutionOrder(200)]
public class KeyPromptUI : MonoBehaviour
{
    public TMP_Text source;
    public CanvasGroup keycap;
    public CanvasGroup prompt;
    public string unavailableText;

    private void LateUpdate()
    {
        bool visible = source != null && source.isActiveAndEnabled;
        if (prompt != null) prompt.alpha = visible ? 1f : 0f;
        if (keycap != null)
            keycap.alpha = !visible ? 0f :
                (!string.IsNullOrEmpty(unavailableText) && source.text == unavailableText ? 0.4f : 1f);
    }
}
