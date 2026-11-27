using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PixelMenuButton : Button, ICancelHandler
{
    public bool LevelScreen { get; private set; }
    public bool CreditsScreen { get; private set; }
    public int Index { get; private set; }
    private MenuPrincipal menu;
    public void Initialize(MenuPrincipal owner, bool levelScreen, int index, bool creditsScreen=false)
    {
        menu = owner;
        LevelScreen = levelScreen;
        CreditsScreen = creditsScreen;
        Index = index;
    }
    public override void OnPointerEnter(PointerEventData data)
    {
        base.OnPointerEnter(data);
        if (menu != null && IsInteractable()) menu.Focus(this);
    }
    public override void OnSelect(BaseEventData data)
    {
        base.OnSelect(data);
        if (menu != null && IsInteractable()) menu.Focus(this);
    }
    public override void OnPointerDown(PointerEventData data)
    {
        base.OnPointerDown(data);
        if (data.button == PointerEventData.InputButton.Left && menu != null && IsInteractable()) menu.Press(this,true);
    }
    public override void OnPointerUp(PointerEventData data)
    {
        base.OnPointerUp(data);
        if (data.button == PointerEventData.InputButton.Left && menu != null) menu.Press(this,false);
    }
    public override void OnPointerExit(PointerEventData data)
    {
        base.OnPointerExit(data);
        if (menu != null) menu.Press(this,false);
    }
    public void OnCancel(BaseEventData data)
    {
        if (menu != null) menu.ReturnToMain();
    }
}
