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
    public Rect2 TopFace => new((Size.X-(Size.Y-18)*.7142857f)/2 - _shownCount * .055f, 26 - _shownCount * .65f, (Size.Y-18)*.7142857f, Size.Y-18);
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
        _discard=zone is Zone.FateDiscard or Zone.DynastyDiscard;
        var cards=player.Discard.Where(c=>c.Zone==zone).ToArray();
        Count=zone==Zone.FateDeck?player.FateDeckCount:zone==Zone.DynastyDeck?player.DynastyDeckCount:cards.Length;
        _shownCount = Count;
        Material = CardPaper.Table;
        var name=(_fate?"Fate":"Dynasty")+(_discard?" discard":" deck");
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
            // One exposed paper edge per physical card: no five-layer cap.
            for(var i=0;i<Math.Ceiling(_shownCount);i++)
                PileFrame(new Rect2((Size.X-w)/2-i*.055f,26-i*.65f,w,h),new Color("302a21"),new Color(.39f,.34f,.26f,.85f));
            var texture = _face ?? (_fate ? FateBack : DynastyBack);
            PileFace(texture,frame.Grow(-1),Colors.White);
        }
        // Small inlaid symbols distinguish the four piles without permanent counts.
        DrawCircle(new Vector2(Size.X/2,Size.Y-7),10,new Color("181711"));
        DrawArc(new Vector2(Size.X/2,Size.Y-7),9,0,Mathf.Tau,40,new Color("967648"),1,true);
        DrawString(Palette.Body,new Vector2(Size.X/2-8,Size.Y-2),_fate?"F":"D",HorizontalAlignment.Center,16,13,Palette.Gold);
        if(_discard) DrawLine(new Vector2(Size.X/2-6,Size.Y-1),new Vector2(Size.X/2+6,Size.Y-1),Palette.Gold,1,true);
        if(IsHovered()) DrawRect(frame.Grow(2),Palette.Gold,false,1,true);
    }
}
