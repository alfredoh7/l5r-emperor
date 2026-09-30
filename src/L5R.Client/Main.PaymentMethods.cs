using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private PaymentSource[] PaymentMethods(GoldQuote quote) => quote.Sources ??
        Self.Home.Concat(Self.Army).Append(Self.Stronghold).Where(c => !c.Bowed && Production(c, Self) > 0)
            .Select(c => new PaymentSource(c.Id, c.Id, "bow", $"Bow this card: {Production(c, Self)} Gold", Production(c, Self))).ToArray();

    private bool PaymentSelectionReady(int required, GoldQuote quote)
    {
        var methods = PaymentMethods(quote);
        // Coverage is presentation only; the server validates and commits the whole command.
        return _sources.All(id => methods.Any(m => m.Id == id)) &&
            !methods.Where(m => _sources.Contains(m.Id)).Any(m => m.ConflictingSourceIds?.Any(_sources.Contains) == true) &&
            methods.Where(m => _sources.Contains(m.Id)).Sum(m => m.Produced) >= required;
    }

    private void RenderPaymentMethods(int required, GoldQuote quote)
    {
        var methods = PaymentMethods(quote);
        _inspector.AddChild(Palette.Text("PAYMENT SOURCES", 18, Palette.Gold));
        foreach (var group in methods.GroupBy(m => m.CardId))
        {
            var card = FindCard(group.Key);
            var selected = group.Any(m => _sources.Contains(m.Id));
            var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var style = SidebarSkin.Inset(); style.ModulateColor = selected ? new Color(1.12f, 1.06f, .94f) : Colors.White;
            panel.AddThemeStyleboxOverride("panel", style); _inspector.AddChild(panel);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); panel.AddChild(row);
            if (card?.Definition is not null)
            {
                var face = new DecisionSourceFace(card, 112, context: "Payment source"); row.AddChild(face);
                face.Pressed += () => { var (_, content) = Dialog(card.Definition.Name, 720, 720); content.AddChild(CardPreview.Face(card, 510)); };
            }
            var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 6); row.AddChild(body);
            body.AddChild(Palette.Wrapped(card?.Definition?.Name ?? "Payment source", 18, Palette.Paper));
            foreach (var method in group)
            {
                var check = new CheckBox
                {
                    Text = method.Label, ButtonPressed = _sources.Contains(method.Id),
                    Disabled = !DecisionCanSubmit,
                    CustomMinimumSize = new(0, 40), AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    TooltipText = method.Label, AccessibilityName = method.Label
                };
                check.SetMeta("payment_source_id", method.Id);
                check.AddThemeFontOverride("font", GD.Load<FontFile>("res://Art/Fonts/Caudex-Regular.ttf"));
                check.AddThemeFontSizeOverride("font_size", 16);
                check.Toggled += on =>
                {
                    if (!DecisionCanSubmit || _chosen is null || QuoteFor(_chosen) != quote) return;
                    if (on) _sources.Add(method.Id); else _sources.Remove(method.Id);
                    RenderInspector(); Highlight();
                    _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>()
                        .FirstOrDefault(c => c.GetMeta("payment_source_id", "").AsString() == method.Id)?.CallDeferred(Control.MethodName.GrabFocus);
                };
                body.AddChild(check);
            }
        }
        var picked = methods.Where(m => _sources.Contains(m.Id)).ToArray();
        var gold = picked.Where(m => m.Kind != "discount").Sum(m => m.Produced);
        var reduction = picked.Where(m => m.Kind == "discount").Sum(m => m.Produced);
        var receipt = new PanelContainer(); receipt.AddThemeStyleboxOverride("panel", SidebarSkin.Inset()); _inspector.AddChild(receipt);
        var lines = new VBoxContainer(); lines.AddThemeConstantOverride("separation", 6); receipt.AddChild(lines);
        lines.AddChild(Palette.Text("PAYMENT SUMMARY", 18, Palette.Gold));
        lines.AddChild(Palette.Wrapped($"Gold contributed   {gold}\nCost reduction   {reduction}\nAdjusted Gold cost   {Math.Max(0, required - reduction)}", 17));
        var ready = PaymentSelectionReady(required, quote);
        lines.AddChild(Palette.Wrapped(ready ? gold == Math.Max(0, required - reduction) ? "Exact payment" : "Cost covered · excess reviewed by the engine" :
            picked.Any(m => m.ConflictingSourceIds?.Any(_sources.Contains) == true) ? "Choose one of the conflicting methods" : "Choose more payment sources", 16, ready ? Palette.Gold : Palette.Error));
        foreach (var method in picked.Where(m => m.Kind is "destroy" or "discount" or "produce" or "reaction"))
            lines.AddChild(Palette.Wrapped(method.Label, 15, Palette.PaperDim));
        lines.AddChild(Palette.Wrapped("Sources are committed only when you confirm.", 14, Palette.PaperDim));
    }
}
