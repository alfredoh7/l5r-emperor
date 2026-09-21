using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of the public engine flag. Never infers or applies a restriction.</summary>
public static class ActionLockStatus
{
    public const string Message = "May not perform actions this turn.";
    public const string Expiry = "Until this turn ends.";
    private static Texture2D? _seal;
    public static Texture2D Seal => _seal ??= GD.Load<Texture2D>("res://Art/action-lock-seal.png");
    // One marker per unit keeps folded attachment stacks quiet. Attachments show the same public status when inspected.
    public static bool VisibleOn(CardView card) => card.ActionLocked && card.FaceUp && card.Definition?.Type==CardType.Personality && card.Zone is Zone.Home or Zone.Battlefield;
    public static TextureRect Icon(float size) => new() { Texture=Seal, CustomMinimumSize=new(size,size),
        ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsVertical=Control.SizeFlags.ShrinkCenter, MouseFilter=Control.MouseFilterEnum.Ignore };
    public static Control Explanation(CardView card)
    {
        var body=new VBoxContainer();body.AddThemeConstantOverride("separation",6);body.SetMeta("action_lock_status",true);
        body.AddChild(Palette.Wrapped(Message,20,Palette.Paper));
        body.AddChild(Palette.Wrapped(Expiry,16,Palette.PaperDim));
        body.AddChild(Palette.Wrapped(card.Definition?.Type==CardType.Personality
            ?"May still be assigned and targeted."
            :"The attached Personality is unable to perform actions.",16,Palette.PaperDim));
        return body;
    }
}

/// <summary>A quiet clickable seal outside the upper-right face; follows bowing without covering printed stats.</summary>
public partial class ActionLockSeal : Button
{
    public required CardTile CardAnchor { get; init; }
    public override void _Ready()
    {
        Flat=true;TextureFilter=TextureFilterEnum.LinearWithMipmaps;
        foreach(var state in new[]{"normal","hover","pressed","focus"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        TooltipText=$"{CardAnchor.Card.Definition?.Name}\n{ActionLockStatus.Message}\n{ActionLockStatus.Expiry}\nClick to inspect.";
        SetMeta("flat_tooltip",TooltipText);AccessibilityName=TooltipText;
        Pressed+=()=>CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;FocusEntered+=QueueRedraw;FocusExited+=QueueRedraw;
        PositionAtCard();
    }
    public override void _Process(double delta)=>PositionAtCard();
    private void PositionAtCard()
    {
        Visible=ActionLockStatus.VisibleOn(CardAnchor.Card);if(!Visible)return;
        var inverse=CardAnchor.GetGlobalTransform().AffineInverse();
        var corners=CardAnchor.FaceCornersInCanvas().Select(p=>inverse*p).ToArray();
        var diameter=Math.Max(20,CardAnchor.FaceHeight*.16f);
        var extent=new Vector2(diameter,diameter);
        if(CardAnchor.ProjectToScreen is {} project)
        {
            // Preserve a readable mark at the normal table distance. Compensate for the table plane's foreshortening.
            var transform=CardAnchor.GetGlobalTransform();
            var corner=new Vector2(corners.Max(p=>p.X),corners.Min(p=>p.Y));
            var origin=transform*corner;
            var minimum=18*GetTree().Root.GetVisibleRect().Size.Y/1080f;
            extent.X=Math.Max(extent.X,minimum/Math.Max(.01f,project(transform*(corner+Vector2.Right)).DistanceTo(project(origin))));
            extent.Y=Math.Max(extent.Y,minimum/Math.Max(.01f,project(transform*(corner+Vector2.Down)).DistanceTo(project(origin))));
        }
        Size=extent;
        Position=new(corners.Max(p=>p.X)-extent.X*.05f,corners.Min(p=>p.Y)-extent.Y*.28f);
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point)=>((point-Size/2)/Size).Length()<=.47f;
    public override Control _MakeCustomTooltip(string text)=>CardAnchor.ProjectToScreen is null?ReadingTooltip.Create(this,text):null!;
    public override void _Draw()
    {
        DrawTextureRect(ActionLockStatus.Seal,new Rect2(Vector2.Zero,Size),false);
        if(HasFocus())DrawArc(Size/2,Size.X*.48f,0,Mathf.Tau,40,Palette.Gold,1.5f,true);
    }
    public Vector2 CenterOnViewport()
    {
        var point=GetGlobalTransform()*(Size/2);
        return CardAnchor.ProjectToScreen?.Invoke(point)??point;
    }
}
