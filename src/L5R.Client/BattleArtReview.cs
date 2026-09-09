using Godot;
namespace L5R.Client;

/// <summary>Native source/implementation comparison. No fabricated gameplay.</summary>
public partial class BattleArtReview(string path) : Control
{
    public override async void _Ready()
    {
        Size = new Vector2(1920,1080); MouseFilter = MouseFilterEnum.Ignore;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(path); GetTree().Quit();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,1920,1080),new Color("122331"));
        DrawString(Palette.Body,new Vector2(80,80),"Approved B pixels / native sliced renderer — same scale",fontSize:30);
        DrawString(Palette.Body,new Vector2(80,130),"Reference",fontSize:25);
        DrawString(Palette.Body,new Vector2(850,130),"In-game renderer",fontSize:25);
        DrawTextureRectRegion(GD.Load<Texture2D>("res://Art/battle-court-reference.png"),new Rect2(80,160,458.7f,831.6f),new Rect2(1365,238,278,504));
        DrawSetTransform(new Vector2(850,209.5f),0,new Vector2(1.65f,1.65f));
        BattleInkArt.Court(this,new Vector2(260,237),0);
        BattleInkArt.Neck(this,new Vector2(130,237),false,Colors.White);
        DrawSetTransform(new Vector2(850+60*1.65f,209.5f+(237+BattleInkArt.NeckSpan-12)*1.65f),0,new Vector2(1.65f,1.65f));
        ProvinceThresholdArt.Draw(this,new Vector2(140,186),"Province",4,0,true,true);
        DrawSetTransform(Vector2.Zero);
    }
}
