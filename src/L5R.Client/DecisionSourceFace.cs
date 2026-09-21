using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A full, public card face shared by decision panels, with enlarged hover and keyboard inspection.</summary>
public sealed partial class DecisionSourceFace : Button
{
    private readonly CardView _card;
    public DecisionSourceFace(CardView card,float height)
    {
        _card=card;CustomMinimumSize=new(height/1.4f,height);TooltipText=card.Definition?.Name??"Inspect source";
        AccessibilityName="Inspect "+TooltipText;
        foreach(var state in new[]{"normal","hover","pressed","disabled"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        var face=CardPreview.Face(card,height,height/1.4f);AddChild(face);face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
    public override Control _MakeCustomTooltip(string forText)=>CardPreview.Create(_card,"Source of this decision",null,GetTree().Root.GetVisibleRect().Size.Y/1080f,
        interactionHint:"Click the source card to keep its full scan open.\nPress Escape to close inspection.");
}
