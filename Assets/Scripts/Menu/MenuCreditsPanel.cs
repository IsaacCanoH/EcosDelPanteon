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
            "Isaac Cano Hernández\nLuis Manuel Ramírez Ramírez\n\n"+
            Heading("PROGRAMACIÓN Y DISEÑO DE NIVELES")+
            "Isaac Cano Hernández\nNiveles 1 y 4\n\nLuis Manuel Ramírez Ramírez\nNiveles 2 y 3\n\n"+
            Heading("ARTE Y ANIMACIÓN")+
            "<color=#FFC431>Personaje principal</color>\nBlackHole42\n32x32 Top-Down Character Animations\n\n"+
            "<color=#FFC431>Duende</color>\nJayinpixel (antes Auryjj)\nFREE Top Down 32x32 Animated Goblin Enemy\n\n"+
            "<color=#FFC431>Llorona</color>\nAkari21\nOne Dollar Monsters #3: Banshee\n\n"+
            "<color=#FFC431>Ambiente</color>\nCainos - Unity Asset Store\nPixel Art Top Down - Basic\n\n"+
            "<color=#FFC431>Referencias visuales</color>\nBruja: Elthen\nNiño: RainCStudios\n\n"+
            "<color=#FFC431>Menú y pergamino de introducción</color>\nImágenes generadas con IA\nSelección e integración del equipo\n\n"+
            Heading("MÚSICA")+
            "Lacrimosa - Wolfgang Amadeus Mozart\nVersión para piano: Saturn-3-Music\nMozart - Lacrimosa (Requiem) - Piano Version\nPixabay Content License\n\n"+
            Heading("EFECTOS DE SONIDO")+
            "<color=#FFC431>Freesound - CC BY 4.0</color>\nBenboncan - Running On Gravel\nhttps://freesound.org/s/137874/\n\n"+
            "Nanakisan - Evil Laugh 8\nhttps://freesound.org/s/253532/\n\nNanakisan - Evil Laugh 4\nhttps://freesound.org/s/253530/\n\n"+
            "Licencia de estos tres sonidos:\nhttps://creativecommons.org/licenses/by/4.0/\nArchivos renombrados para su uso en Unity.\n\n"+
            "<color=#FFC431>Freesound - CC0</color>\ngreyfeather - sword slash energy wave\nDanJFilms - Woman Screaming\nLilMati - Crystal Magic - Dark Spell Rise\nalegemaate (Allan Legemaate) - Stone Scrape\n\n"+
            "<color=#FFC431>OpenGameArt - CC0</color>\nartisticdude - Goblins Sound Pack\n\n"+
            "<color=#FFC431>Kenney - CC0</color>\nRPG Audio e Impact Sounds\n\n"+
            "<color=#FFC431>Pixabay Content License</color>\nLiecio - Collect Points\nrevolt2563, vía freesound_community\nFootsteps Dirt Gravel\n\n"+
            Heading("TIPOGRAFÍAS")+
            "Press Start 2P Project Authors\nPress Start 2P - SIL OFL 1.1\nLiberation Sans - Google / Red Hat\nSIL OFL 1.1\n\n"+
            Heading("AGRADECIMIENTOS")+
            "Universidad Tecnológica\ndel Norte de Guanajuato\n\nProf. Gabriel Barrón Rodriguez\nCreación de Videojuegos\n\n"+
            Heading("DESARROLLADO CON")+
            "Unity 2021.3";
        content.sizeDelta=new Vector2(TextWidth,Mathf.Ceil(body.GetPreferredValues(TextWidth,float.PositiveInfinity).y)+50f);
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
