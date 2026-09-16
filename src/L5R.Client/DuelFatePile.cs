using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>A normal-size sleeved stack on the same projected plane as hand/focus cards.</summary>
public partial class DuelFatePile : TooltipButton
{
    public int OwnerId {get;}
    public Zone PileZone=>Zone.FateDeck;
    private readonly int _count;
    private float _shown,_from;
    private double _settle=1;
    public float ShownCount=>_shown;
    public bool Selected {get;set;}
    private Rect2 Face(float count)=>new((Size.X-146.43f)/2,8-count*.95f,146.43f,205);
    public Rect2 TopFace=>Face(_shown);
    public DuelFatePile(PlayerView player,Action open)
    {
        OwnerId=player.Id;_count=player.FateDeckCount;_shown=_count;Flat=true;
        TooltipText=$"{player.Clan} · Fate deck\n{_count} cards · contents and order remain hidden.";
        MouseDefaultCursorShape=CursorShape.PointingHand;TextureFilter=TextureFilterEnum.LinearWithMipmaps;
        foreach(var state in new[]{"normal","hover","pressed","focus"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        Pressed+=open;MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;
    }
    public override bool _HasPoint(Vector2 point)=>TopFace.Merge(Face(0)).HasPoint(point);
    public void SettleFrom(float count,bool reducedMotion){_from=count;_shown=reducedMotion?_count:count;_settle=reducedMotion?1:0;QueueRedraw();}
    public override void _Process(double delta)
    {
        if(_settle>=1)return;_settle=Math.Min(1,_settle+delta/.32);var t=(float)_settle;
        _shown=Mathf.Lerp(_from,_count,t*t*(3-2*t));QueueRedraw();
    }
    public override void _Draw()
    {
        if(_shown<.01f)return;
        var ground=Face(0);
        for(var spread=10;spread>=2;spread-=2)
            DrawRect(new Rect2(ground.Position+new Vector2(4,6),ground.Size).Grow(spread),new Color(.005f,.004f,.007f,.042f));
        for(var i=0;i<Math.Ceiling(_shown);i++)
        {
            var r=Face(Math.Min(i+1,_shown));DrawRect(r,new Color("151612"));
            DrawLine(new Vector2(r.Position.X,r.End.Y),r.End,i%3==0?new Color("b59a70"):new Color("74634d"),.60f,true);
        }
        DrawTextureRect(TablePile.FateBack,TopFace,false);
        DrawLine(TopFace.Position,TopFace.Position+Vector2.Right*TopFace.Size.X,new Color(.9f,.74f,.47f,.45f),.8f,true);
        DrawLine(TopFace.End-Vector2.Right*TopFace.Size.X,TopFace.End,new Color("a08762"),.8f,true);
        if(Selected||IsHovered())DrawRect(TopFace.Grow(1),Selected?CardTile.SelectionColor:Palette.Gold,false,1.2f,true);
    }
}
