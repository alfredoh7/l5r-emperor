using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Exactly the engine's methods and token ids. The client never invents a method from a card's printed production: a quote with no
    // Sources (an older payload) offers none, so no id the engine did not name can be selected, and a published alternative such as
    // Jade Mine's "#alt" is selectable only because the engine listed it.
    private static readonly PaymentSource[] NoPaymentMethods = [];
    private static PaymentSource[] PaymentMethods(GoldQuote quote) => quote.Sources ?? NoPaymentMethods;

    // "reduce" (issue #667, Profits: "reduce the total Gold produced by 1") is the one method that produces less than zero.
    private static bool PaymentMethodUnderstood(PaymentSource method) =>
        (method.Produced > 0 || method.Kind == "reduce" && method.Produced < 0) && method.Kind is "bow" or "produce" or "reaction" or "destroy" or "discount" or "reduce";

    // Hovering (or focusing) a payment method highlights the card it belongs to, so each Profits in play names its own card.
    private string? _hoveredPaymentCardId;
    private void HoverPaymentCard(string? cardId)
    {
        if (_hoveredPaymentCardId == cardId) return;
        _hoveredPaymentCardId = cardId; Highlight();
    }

    // Holdings the player has chosen to bow for the payment in progress: drawn bowed on the table before anything is committed.
    private HashSet<string> _previewBows = [];
    private string? _previewBowFlip;

    private HashSet<string> PreviewBowedCards() => _chosen is { } chosen && QuoteFor(chosen) is { } quote
        ? PaymentMethods(quote).Where(m => m.Kind == "bow" && _sources.Contains(m.Id)).Select(m => m.CardId).ToHashSet() : [];

    // Gold chosen sources contribute: producers and reactions add, a "reduce" source subtracts; cost reductions are not Gold.
    private int PaidGold(GoldQuote quote) => PaymentMethods(quote).Where(m => _sources.Contains(m.Id) && m.Kind != "discount").Sum(m => m.Produced);

    // The engine's payment method behind a click on this holding (null when the card is not a payment source for the open cost).
    private string? HoldingPaymentMethod(CardView card) => _chosen is { } chosen && QuoteFor(chosen) is { } quote &&
        (quote.Kind == GoldCostKind.Variable || chosen.Type == "chooseOpponentPayment" || AmountFor(chosen, quote) > 0)
        ? PaymentMethods(quote).FirstOrDefault(m => m.CardId == card.Id && m.Kind == "bow" && PaymentMethodUnderstood(m))?.Id : null;

    // The player chooses the holdings to bow, so a costed action opens with nothing pre-selected (the engine's suggestion stays one click away).
    private static bool PlayerPicksPayment(ActionOption option) => QuoteFor(option) is { } quote && (quote.Kind == GoldCostKind.Variable || quote.Cost > 0);

    private void ToggleHoldingPayment(string methodId, string cardId)
    {
        if (!DecisionCanSubmit) return;
        if (!_sources.Add(methodId)) _sources.Remove(methodId);
        _previewBowFlip = cardId; RefreshPaymentSelection();
    }

    // A cost the player chooses is exactly the Gold they contribute (within the engine's range).
    private void SyncPaymentAmount()
    {
        if (_chosen is not { Command: { } command } chosen || chosen.Type == "chooseOpponentPayment" || QuoteFor(chosen) is not { Kind: GoldCostKind.Variable } quote) return;
        var amount = Math.Clamp(PaidGold(quote), quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0);
        if (command.GoldAmount != amount) _chosen = chosen with { Command = command with { GoldAmount = amount } };
    }

    private void RefreshPaymentSelection()
    {
        SyncPaymentAmount();
        _previewBows = PreviewBowedCards();
        RenderBoard(); RenderInspector(); Highlight();
    }

    private void UpdateGoldMeter(int paid, int? cost) => _goldMeter.Present(paid, cost);

    private bool PaymentSelectionReady(int required, GoldQuote quote)
    {
        var methods = PaymentMethods(quote);
        // Coverage is presentation only; the server validates and commits the whole command.
        return _sources.All(id => methods.Any(m => m.Id == id && PaymentMethodUnderstood(m))) &&
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
                    Text = PaymentMethodUnderstood(method) ? method.Label : "Additional payment choice unavailable",
                    ButtonPressed = _sources.Contains(method.Id),
                    Disabled = !DecisionCanSubmit || !PaymentMethodUnderstood(method),
                    CustomMinimumSize = new(0, 40),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    TooltipText = PaymentMethodUnderstood(method) ? method.Label : "Additional payment choice unavailable",
                    AccessibilityName = method.Label
                };
                check.SetMeta("payment_source_id", method.Id);
                check.AddThemeFontOverride("font", GD.Load<FontFile>("res://Art/Fonts/Caudex-Regular.ttf"));
                check.AddThemeFontSizeOverride("font_size", 16);
                check.Toggled += on =>
                {
                    if (!DecisionCanSubmit || !PaymentMethodUnderstood(method) || _chosen is null || QuoteFor(_chosen) != quote) return;
                    if (on) _sources.Add(method.Id); else _sources.Remove(method.Id);
                    _previewBowFlip = method.Kind == "bow" ? method.CardId : null; RefreshPaymentSelection();
                    _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>()
                        .FirstOrDefault(c => c.GetMeta("payment_source_id", "").AsString() == method.Id)?.CallDeferred(Control.MethodName.GrabFocus);
                };
                check.MouseEntered += () => HoverPaymentCard(method.CardId); check.MouseExited += () => HoverPaymentCard(null);
                check.FocusEntered += () => HoverPaymentCard(method.CardId); check.FocusExited += () => HoverPaymentCard(null);
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
        foreach (var method in picked.Where(m => m.Kind is "destroy" or "discount" or "produce" or "reaction" or "reduce"))
            lines.AddChild(Palette.Wrapped(method.Label, 15, Palette.PaperDim));
        lines.AddChild(Palette.Wrapped("Sources are committed only when you confirm.", 14, Palette.PaperDim));
    }
}
