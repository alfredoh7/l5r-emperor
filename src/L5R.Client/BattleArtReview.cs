using Godot;
namespace L5R.Client;

/// <summary>Native art review; deliberately separate from a gameplay fixture.</summary>
public partial class BattleArtReview(string path) : Control
{
    public override async void _Ready()
    {
        Size = new Vector2(1920,1080); MouseFilter = MouseFilterEnum.Ignore;
        var ink = new InkMatSections(); AddChild(ink);
        ink.Sync([new("court", "Province 2", new Rect2(900,180,520,450),true)],true,"battle-art");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(path); GetTree().Quit();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,1920,1080),new Color("122331"));
        DrawString(Palette.Body,new Vector2(90,100),"Approved B border / native renderer — art review, not gameplay",fontSize:30);
        DrawTextureRectRegion(GD.Load<Texture2D>("res://Art/battle-court-reference.png"),new Rect2(90,180,506,474),new Rect2(1373,238,253,237));
        BattleInkArt.Fallen(this,new Rect2(90,760,400,120),2,.5f);
        BattleInkArt.Fallen(this,new Rect2(650,760,400,120),2,1);
        BattleInkArt.Stroke(this,new Vector2(1300,760),new Vector2(1300,900),Colors.White);
    }
}
