using Godot;
namespace L5R.Client;

/// <summary>Actual native art renderer and selected source pixels, not fabricated gameplay.</summary>
public partial class BattleEscalationReview(string path) : Control
{
    public override async void _Ready()
    {
        Size=new Vector2(1920,1080); MouseFilter=MouseFilterEnum.Ignore;
        var court=new CrimsonCourt(); AddChild(court); court.Present(new Rect2(1020,185,510,345),false,8,true,true);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng(path); GetTree().Quit();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,1920,1080),new Color("101e29"));
        void Text(string s,int x,int y,int size=23)=>DrawString(Palette.Body,new Vector2(x,y),s,fontSize:size,modulate:Palette.Paper);
        Text("Selected source / actual native renderer",65,65,32);
        Text("3A · approved crimson court",65,120); Text("Native court · original pigment, Daisho, live soak material",990,120);
        DrawTextureRectRegion(GD.Load<Texture2D>("res://Art/crimson-reference.png"),new Rect2(70,152,765,548),new Rect2(225,140,860,616));
        Text("Detail at the same scale · source and isolated pigment",980,643);
        var source=GD.Load<Texture2D>("res://Art/crimson-reference.png");
        DrawTextureRectRegion(source,new Rect2(1000,670,237,198),new Rect2(1300,285,158,132));
        DrawTextureRect(BattleInkArt.Extract("res://Art/crimson-reference.png",new Rect2I(1300,285,158,132),borderPigment:true),new Rect2(1300,670,237,198),false);
        Text("4A · source-derived arrow + stone",65,760);
        DrawTextureRect(BattleEscalationArt.Arrow,new Rect2(80,785,186.6f,87.9f),false);
        DrawTextureRect(BattleEscalationArt.Step,new Rect2(310,799,139.2f,54.6f),false);
        Text("1A · live frayed perimeter / original fibers",510,760);
        BattleEscalationArt.Frayed(this,new Rect2(550,790,170,218),1);
        Text("Only straight runs resize. Source proportions, blood detail and brush grain remain raster art.",70,1040,22);
    }
}
