using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Credit content and scrolling live separately from the menu's scene-loading flow.
public sealed class MenuCreditsPanel : MonoBehaviour
{
    private MenuPrincipal menu;
    private ScrollRect scroll;
    private TextMeshProUGUI body;
    private const float TextWidth=1300f;

    public void Build(MenuPrincipal owner,TMP_FontAsset font)
    {
        menu=owner;
        var panel=Rect("Panel_Lectura",transform,new Rect(88f,178f,1496f,604f));
        var background=panel.gameObject.AddComponent<Image>();
        background.color=new Color(0.015f,0.055f,0.065f,0.94f);
        background.raycastTarget=false;

        var viewport=Rect("Viewport",transform,new Rect(136f,208f,1372f,546f));
        var hit=viewport.gameObject.AddComponent<Image>();
        hit.color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll=viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport=viewport;
        scroll.horizontal=false;
        scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.inertia=true;
        scroll.decelerationRate=0.135f;
        scroll.scrollSensitivity=44f;

        var content=new GameObject("Contenido_Creditos",typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport,false);
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,1f);
        content.anchoredPosition=Vector2.zero;
        body=content.gameObject.AddComponent<TextMeshProUGUI>();
        if(font!=null) body.font=font;
        body.fontSize=30f;
        body.enableWordWrapping=true;
        body.richText=true;
        body.alignment=TextAlignmentOptions.Top;
        body.color=new Color(1f,0.94f,0.76f);
        body.lineSpacing=10f;
        body.paragraphSpacing=14f;
        body.margin=new Vector4(0f,8f,0f,28f);
        body.raycastTarget=true;
        body.text=
            Heading("EQUIPO CREADOR")+
            "Isaac Cano Hernandez\nLuis Manuel Ramirez Ramirez\n\n"+
            Heading("PROGRAMACIÓN Y DISEÑO DE NIVELES")+
            "Isaac Cano Hernandez\nNiveles 1 y 4\n\n"+
            "Luis Manuel Ramirez Ramirez\nNiveles 2 y 3\n\n"+
            Heading("ARTE Y ANIMACIÓN")+
            "<color=#FFC431>Sprites</color>\nUnity Asset Store\n\n"+
            "<color=#FFC431>Personajes</color>\n"+
            "Blackhole42.itch\nRaincstudios.itch\nAkari21.itch\nElthen.itch\nAuryjj.itch\n\n"+
            Heading("MÚSICA Y EFECTOS DE SONIDO")+
            "<color=#FFC431>Efectos de sonido</color>\nFreesound\n\n"+
            "<color=#FFC431>Música</color>\nLacrimosa\nWolfgang Amadeus Mozart\nVersión para piano\n\n"+
            Heading("AGRADECIMIENTOS")+
            "Universidad Tecnológica\ndel Norte de Guanajuato\n\n"+
            "Prof. Gabriel Barrón Rodriguez\nCreación de Videojuegos\n\n"+
            Heading("DESARROLLADO CON")+
            "Unity 2021.3";
        content.sizeDelta=new Vector2(TextWidth,Mathf.Ceil(body.GetPreferredValues(TextWidth,10000f).y)+50f);
        scroll.content=content;

        var track=Rect("Barra_Desplazamiento",transform,new Rect(1520f,208f,22f,546f));
        var trackImage=track.gameObject.AddComponent<Image>();
        trackImage.color=new Color(0.17f,0.19f,0.13f,1f);
        var handle=new GameObject("Indicador",typeof(RectTransform)).GetComponent<RectTransform>();
        handle.SetParent(track,false);
        handle.anchorMin=Vector2.zero;
        handle.anchorMax=Vector2.one;
        handle.offsetMin=handle.offsetMax=Vector2.zero;
        var handleImage=handle.gameObject.AddComponent<Image>();
        handleImage.color=new Color(1f,0.77f,0.19f,1f);
        var scrollbar=track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect=handle;
        scrollbar.targetGraphic=handleImage;
        scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scrollbar.transition=Selectable.Transition.None;
        scrollbar.navigation=new Navigation {mode=Navigation.Mode.None};
        scroll.verticalScrollbar=scrollbar;

        var hint=Rect("Ayuda_Lectura",transform,new Rect(550f,844f,670f,44f));
        var hintText=hint.gameObject.AddComponent<TextMeshProUGUI>();
        if(font!=null) hintText.font=font;
        hintText.text="DESLIZA PARA LEER";
        hintText.fontSize=22f;
        hintText.alignment=TextAlignmentOptions.Center;
        hintText.color=new Color(1f,0.9f,0.66f,1f);
        hintText.raycastTarget=false;
    }

    public void ResetToTop()
    {
        if(scroll==null) return;
        scroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        scroll.Rebuild(CanvasUpdate.PostLayout);
        scroll.verticalNormalizedPosition=1f;
        if(scroll.verticalScrollbar!=null)
        {
            scroll.verticalScrollbar.value=1f;
            scroll.verticalScrollbar.Rebuild(CanvasUpdate.PostLayout);
            scroll.verticalScrollbar.targetGraphic.SetVerticesDirty();
        }
    }

    private void Update()
    {
        if(menu==null || !menu.IsCreditsVisible || menu.IsLoading || scroll==null) return;
        var keyboard=Keyboard.current;
        if(keyboard==null) return;
        float direction=0f;
        if(keyboard.downArrowKey.isPressed || keyboard.pageDownKey.isPressed) direction-=1f;
        if(keyboard.upArrowKey.isPressed || keyboard.pageUpKey.isPressed) direction+=1f;
        if(direction!=0f)
        {
            scroll.StopMovement();
            float overflow=Mathf.Max(1f,scroll.content.rect.height-scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*500f*Time.unscaledDeltaTime/overflow);
        }
        if(keyboard.homeKey.wasPressedThisFrame) ResetToTop();
        if(keyboard.endKey.wasPressedThisFrame) {scroll.StopMovement();scroll.verticalNormalizedPosition=0f;}
    }

    private static string Heading(string text)
    {
        return "<size=24><color=#FFC431>"+text+"</color></size>\n";
    }

    private static RectTransform Rect(string name,Transform parent,Rect bounds)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);
        rect.sizeDelta=bounds.size;
        rect.anchoredPosition=new Vector2(bounds.center.x-836f,470.5f-bounds.center.y);
        return rect;
    }
}
