using TMPro;
using UnityEngine;

// Coloca la tecla de la interfaz sobre el espacio reservado en el texto.
[DefaultExecutionOrder(250)]
[RequireComponent(typeof(TMP_Text))]
public class InlineKeyPromptUI : MonoBehaviour
{
    public RectTransform keycap;
    public string key = "F";
    private TMP_Text source;
    private string lastText;
    private Rect lastRect;
    private float lastSize;
    private float lastSpacing;
    private float normalSpacing;

    private void Awake()
    {
        source = GetComponent<TMP_Text>();
        normalSpacing = source.lineSpacing;
    }

    private void LateUpdate()
    {
        if (keycap == null) return;
        if (source == null)
        {
            source = GetComponent<TMP_Text>();
            normalSpacing = source.lineSpacing;
        }
        bool hasKey = source.text.Contains("<link=\"key:" + key + "\">");
        source.lineSpacing = hasKey ? Mathf.Max(60f, normalSpacing) : normalSpacing;
        Rect bounds = source.rectTransform.rect;
        if (source.text == lastText && bounds == lastRect && source.fontSize == lastSize && source.lineSpacing == lastSpacing) return;
        lastText = source.text;
        lastRect = bounds;
        lastSize = source.fontSize;
        lastSpacing = source.lineSpacing;
        source.ForceMeshUpdate();
        bool found = false;
        for (int i = 0; i < source.textInfo.linkCount; i++)
        {
            TMP_LinkInfo link = source.textInfo.linkInfo[i];
            if (link.GetLinkID() != "key:" + key || link.linkTextLength < 1) continue;
            int first = link.linkTextfirstCharacterIndex;
            int last = first + link.linkTextLength - 1;
            TMP_CharacterInfo left = source.textInfo.characterInfo[first];
            TMP_CharacterInfo right = source.textInfo.characterInfo[last];
            // Las letras transparentes reservan el espacio; la tecla conserva su borde y sombra.
            TMP_CharacterInfo letter = source.textInfo.characterInfo[first + 1];
            keycap.localPosition = new Vector3((left.origin + right.xAdvance) * 0.5f,
                (letter.bottomLeft.y + letter.topRight.y) * 0.5f, 0f);
            found = true;
            break;
        }
        keycap.gameObject.SetActive(found);
    }
}
