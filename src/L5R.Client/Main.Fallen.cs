using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private Window? _fallenWindow;
    private FallenGallery? _fallenGallery;
    private void ShowFallenGallery(int owner)
    {
        if(IsInstanceValid(_fallenWindow)&&_fallenWindow!.Visible)
        { _fallenGallery!.SelectPlayer(owner);return; }
        HideTableTooltip();ReleaseTableFocus();
        var window=new Window { Title="Fallen cards",Theme=SidebarSkin.Theme(),Exclusive=true,Transient=true,
            Borderless=true,Unresizable=true,WrapControls=false };
        var gallery=new FallenGallery(View.Players,owner);
        _fallenWindow=window;_fallenGallery=gallery;AddChild(window);window.AddChild(gallery);
        gallery.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void Close()
        {
            var id=gallery.OwnerId;
            _fallenWindow=null;_fallenGallery=null;window.QueueFree();
            _clanHud.GetChildren().OfType<FallenCounter>().FirstOrDefault(c=>c.Player.Id==id)?.GrabFocus();
        }
        gallery.Close+=Close;window.CloseRequested+=Close;
        PresentDialog(window,1480,811);gallery.BackButton.GrabFocus();
    }
}
