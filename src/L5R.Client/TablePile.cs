using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of public pile counts. No deck identities enter this control.</summary>
public partial class TablePile : TooltipButton
{
    public int OwnerId { get; }
    public Zone PileZone { get; }
    public int Count { get; }
    private readonly Texture2D? _face;
    public static readonly Texture2D DynastyBack = GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
    public static readonly Texture2D FateBack = GD.Load<Texture2D>("res://Art/original-fate-back.png");
    private readonly bool _fate, _discard;
    private float _shownCount;
    private float _startCount;
    private double _settle = 1;
    public float ShownCount => _shownCount;
    private const float CardThickness = 1.35f;
    private const float CardLean = .10f;
    public Rect2 TopFace => LayerFace(Math.Max(0, _shownCount));
    private Rect2 LayerFace(float level)
    {
        if(PileZone==Zone.RemovedFromGame) level=Math.Min(level,12);
        var height = Size.Y - 18; var width = height * .7142857f;
        return new Rect2((Size.X-width)/2-level*CardLean,26-level*CardThickness,width,height);
    }
    public override bool _HasPoint(Vector2 point) => TopFace.Merge(LayerFace(0)).HasPoint(point);
    public void SettleFrom(float count, bool reducedMotion)
    {
        _startCount = Math.Clamp(count, 0, 200); _shownCount = reducedMotion ? Count : _startCount;
        _settle = reducedMotion ? 1 : 0; QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (_settle >= 1) return;
        _settle = Math.Min(1, _settle + delta / .65);
        var t = (float)_settle; t = t * t * (3 - 2 * t);
        _shownCount = Mathf.Lerp(_startCount, Count, t); QueueRedraw();
    }
    public TablePile(PlayerView player, Zone zone, Action open, CardDefinition? recentDiscard = null)
    {
        OwnerId=player.Id; PileZone=zone;
        _fate=zone is Zone.FateDeck or Zone.FateDiscard;
        _discard=zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.RemovedFromGame;
        var cards=zone==Zone.RemovedFromGame?player.RemovedFromGame:player.Discard.Where(c=>c.Zone==zone).ToArray();
        Count=zone==Zone.FateDeck?player.FateDeckCount:zone==Zone.DynastyDeck?player.DynastyDeckCount:cards.Length;
        _shownCount = Count;
        Material = CardPaper.Table;
        var name=zone==Zone.RemovedFromGame?"Removed from the Game":(_fate?"Fate":"Dynasty")+(_discard?" discard":" deck");
        // The projection doesn't expose chronological pile order. The face is a
        // representative public card, not a claim about the top of an ordered pile.
        _face=_discard&&(recentDiscard ?? cards.FirstOrDefault()?.Definition) is {} def?CardTile.Artwork(def):null;
        TooltipText=$"{player.Clan} · {name}\n{Count} cards"+ (zone==Zone.FateDeck?$" · {player.HandCount} in hand":"")+
            (_discard?"\nClick to inspect the pile. The face previews a public discard.":"\nDeck contents and order remain hidden.");
        Flat=true; Text=""; MouseDefaultCursorShape=CursorShape.PointingHand;
        TextureFilter=TextureFilterEnum.LinearWithMipmaps;
        foreach(var state in new[]{"normal","hover","pressed","focus"}) AddThemeStyleboxOverride(state,Palette.Box(Colors.Transparent,null,0));
        Pressed+=open; MouseEntered+=QueueRedraw; MouseExited+=QueueRedraw;
    }
    private static Vector2[] FaceQuad(Rect2 r) => new[] {
        r.Position + new Vector2(0, 0),
        r.Position + new Vector2(r.Size.X, 0),
        r.End + new Vector2(0, 0),
        r.Position + new Vector2(-0, r.Size.Y)
    };
    private void PileFrame(Rect2 r, Color fill, Color rim)
    {
        var points = FaceQuad(r); DrawColoredPolygon(points, fill);
        DrawPolyline(points.Append(points[0]).ToArray(), rim, 1, true);
    }
    private void PileFace(Texture2D texture, Rect2 r, Color tint) =>
        DrawColoredPolygon(FaceQuad(r), tint, new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, texture);

    public override void _Draw()
    {
        var frame = TopFace;
        var w=frame.Size.X; var h=frame.Size.Y;
        if(_shownCount < .01)
            PileFrame(frame,new Color(.04f,.035f,.02f,.25f),new Color(.65f,.5f,.3f,.28f));
        else
        {
            DrawStyleBox(Palette.Box(new Color(0,0,0,.5f),null,0,4),new Rect2((Size.X-w)/2+2,29,w+3,h+3));
            // Each card has a dark core and a separate worn paper lip. Alternating
            // fibers and tiny deterministic offsets prevent a solid extruded-block look.
            // Fractional top thickness follows the existing draw/discard interpolation.
            for(var i=0;i<Math.Ceiling(PileZone==Zone.RemovedFromGame?Math.Min(_shownCount,12):_shownCount);i++)
            {
                var level=Math.Min(i+1,_shownCount);
                var layer=LayerFace(level);
                var jitter=(float)Math.Sin(i*2.399)*.38f;
                layer.Position+=new Vector2(jitter,0);
                PileFrame(layer,new Color("141816"),new Color("292b22"));
                var edge = (i%3) switch { 0 => new Color("b1a58a"), 1 => new Color("867e69"), _ => new Color("9d9278") };
                // Near and right edges catch the lantern. The gap between adjacent
                // lips stays almost black, just like separate sleeved paper cards.
                DrawLine(layer.Position+new Vector2(.6f,layer.Size.Y),layer.End+new Vector2(-.6f,0),edge,.63f,true);
                DrawLine(layer.Position+new Vector2(layer.Size.X,1),layer.End,edge.Darkened(.25f),.52f,true);
                for(var fiber=0;fiber<4;fiber++)
                {
                    var x=layer.Position.X+8+(i*17+fiber*37)%(int)Math.Max(1,layer.Size.X-22);
                    DrawLine(new Vector2(x,layer.End.Y),new Vector2(x+3,layer.End.Y),edge.Lightened(.12f),.48f,true);
                }
            }
            var texture = _face ?? (_fate ? FateBack : DynastyBack);
            PileFace(texture,frame.Grow(-1),Colors.White);
        }
        if(IsHovered()) DrawRect(frame.Grow(2),Palette.Gold,false,1,true);
    }
}
