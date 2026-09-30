using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private string? _opponentPaymentKey;
    private bool OpponentPaymentActive => View.WinnerId is null && View.PendingChoice?.Type == "opponentPayment" && View.PendingOpponentPayment is not null;

    private bool RenderOpponentPayment()
    {
        var key = OpponentPaymentActive ? $"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{DecisionCanSubmit}" : null;
        if (key != _opponentPaymentKey)
        {
            if (_chosen?.Type == "chooseOpponentPayment") { _chosen = null; _sources.Clear(); }
            _opponentPaymentKey = key;
        }
        if (!OpponentPaymentActive || PresentationBusy) return false;
        var payment = View.PendingOpponentPayment!;
        var own = payment.ViewerDecides && payment.DeciderPlayerId == View.ViewerId && View.PriorityPlayerId == View.ViewerId;
        var offered = own ? Actions.SingleOrDefault(a => a.Type == "chooseOpponentPayment" && a.Enabled && a.Command is not null) : null;
        // Merely opening this decision must never send the default zero-Gold command.
        if (_chosen is not { Type: "chooseOpponentPayment" } && offered is not null && DecisionCanSubmit)
        {
            _chosen = offered;
            _sources.Clear();
            foreach (var id in offered.Command!.SourceIds ?? []) _sources.Add(id);
        }
        _guide.Visible = false;
        var heading = Palette.Text(payment.SourceName.ToUpperInvariant(), 27, title: true);
        heading.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(heading);
        var actor = View.Players.Single(p => p.Id == payment.ActingPlayerId).Name;
        var decider = View.Players.Single(p => p.Id == payment.DeciderPlayerId).Name;
        _sidebarHeader.AddChild(Palette.Wrapped($"Action: {actor} · Decision: {decider}", 15, Palette.PaperDim));
        _sidebarHeader.AddChild(new HSeparator());

        var source = FindCard(payment.SourceCardId);
        if (source?.Definition is null && CardCatalog.Cards.TryGetValue(payment.SourceDefinitionId, out var definition))
            source = DeckSearchPanel.Printed(definition, payment.ActingPlayerId);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); _inspector.AddChild(row);
        if (source?.Definition is not null)
        {
            var face = new DecisionSourceFace(source, 172); row.AddChild(face);
            face.Pressed += () => { var (_, body) = Dialog(payment.SourceName, 720, 720); body.AddChild(CardPreview.Face(source, 510)); body.AddChild(Palette.Wrapped(source.Definition.Text, 17)); };
        }
        var context = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        context.AddThemeConstantOverride("separation", 10); row.AddChild(context);
        context.AddChild(Palette.Wrapped(own ? "Opponent paid" : "You paid", 17));
        context.AddChild(PaymentGoldAmount(payment.FirstAmount, 27));
        context.AddChild(Palette.Wrapped(payment.Detail, 15, Palette.PaperDim));
        _inspector.AddChild(new HSeparator());
        if (!own)
        {
            _inspector.AddChild(Palette.Wrapped($"{decider} is deciding how much Gold to pay.", 23, Palette.Crane));
            _inspector.AddChild(Palette.Wrapped("Their amount and sources remain private until they confirm.", 17, Palette.PaperDim));
            if (!_session.Online && !_autoOpponent)
                _decisionActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
            return true;
        }
        if (_chosen is not { Type: "chooseOpponentPayment", Command: { } command } selected || offered is null)
        {
            _inspector.AddChild(Palette.Wrapped("Your payment is paused until the connection is ready.", 18, Palette.PaperDim));
            return true;
        }
        var (required, ready) = RenderGoldPanel(selected);
        _paymentConfirm = SidebarSkin.Button(required == 0 ? "Pay no Gold" : $"Pay {required} Gold", () =>
        {
            if (!DecisionCanSubmit || key != _opponentPaymentKey || !OpponentPaymentActive ||
                View.PendingOpponentPayment?.ViewerDecides != true || !ready ||
                !Actions.Any(a => a.Enabled && a.Type == command.Type && a.Command?.CardId == command.CardId && a.Command?.ExpectedVersion == command.ExpectedVersion)) return;
            _ = Submit(command with { SourceIds = required > 0 ? _sources.ToArray() : [] });
        }, true);
        _paymentConfirm.Name = "OpponentPaymentConfirm";
        _paymentConfirm.Disabled = !ready || !DecisionCanSubmit; _decisionActions.AddChild(_paymentConfirm);
        if (required == 0) _decisionActions.AddChild(Palette.Wrapped("Nothing is paid until you confirm.", 14, Palette.PaperDim));
        return true;
    }

    private static Control PaymentGoldAmount(int amount, int size)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/Payments/plain-gold-coin.png"),
            CustomMinimumSize = new(size + 3, size + 3), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
        row.AddChild(Palette.Text($"{amount} Gold", size, Palette.Gold));
        return row;
    }

    private void RenderOpponentAmount(int amount, GoldQuote quote)
    {
        var title = Palette.Text("Your payment", 21); title.HorizontalAlignment = HorizontalAlignment.Center; _inspector.AddChild(title);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); _inspector.AddChild(row);
        Button Adjust(string text, string name, int next, bool disabled)
        {
            var button = SidebarSkin.Button(text, () =>
            {
                if (!DecisionCanSubmit) return;
                ChooseAmount(next);
                var replacement = _inspector.FindChild(name, true, false) as Button;
                if (replacement is { Disabled: false }) replacement.CallDeferred(Control.MethodName.GrabFocus);
                else _paymentConfirm?.CallDeferred(Control.MethodName.GrabFocus);
            });
            button.Name = name; button.Disabled = disabled || !DecisionCanSubmit;
            button.CustomMinimumSize = new(text == "Max" ? 56 : 44, 48);
            button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            button.AddThemeFontSizeOverride("font_size", text == "Max" ? 16 : 26);
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            {
                var style = SidebarSkin.Surface(true, tint: state == "disabled" ? new Color(.55f, .55f, .55f) : Colors.White);
                style.ContentMarginLeft = style.ContentMarginRight = 6;
                style.ContentMarginTop = style.ContentMarginBottom = 6;
                button.AddThemeStyleboxOverride(state, style);
            }
            button.AccessibilityName = text == "−" ? "Pay one less Gold" : text == "+" ? "Pay one more Gold" : "Pay the maximum Gold";
            return button;
        }
        row.AddChild(Adjust("−", "OpponentPaymentLess", amount - 1, amount <= (quote.MinAmount ?? 0)));
        var amountBox = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var amountStyle = SidebarSkin.Inset(); amountStyle.ContentMarginTop = amountStyle.ContentMarginBottom = 6;
        amountBox.AddThemeStyleboxOverride("panel", amountStyle);
        var value = Palette.Text(amount.ToString(), 27); value.HorizontalAlignment = HorizontalAlignment.Center; amountBox.AddChild(value); row.AddChild(amountBox);
        row.AddChild(Adjust("+", "OpponentPaymentMore", amount + 1, amount >= quote.MaxAmount));
        row.AddChild(Adjust("Max", "OpponentPaymentMax", quote.MaxAmount ?? amount, amount >= quote.MaxAmount));
        var bounds = Palette.Text($"{quote.MinAmount ?? 0}–{quote.MaxAmount ?? 0} Gold · paying 0 is allowed", 15, Palette.PaperDim);
        bounds.HorizontalAlignment = HorizontalAlignment.Center; _inspector.AddChild(bounds);
        // Outcomes are private, engine-authored text. Never infer Honor, winners or caps here.
        if (View.PendingOpponentPayment?.Outcomes?.TryGetValue(amount, out var outcome) == true)
        {
            var result = Palette.Wrapped(outcome, 21, Palette.Crane); result.Name = "OpponentPaymentOutcome";
            result.HorizontalAlignment = HorizontalAlignment.Center; _inspector.AddChild(result);
        }
        _inspector.AddChild(new HSeparator());
    }

    private void RenderOpponentSources(int required)
    {
        var header = new HBoxContainer(); _inspector.AddChild(header);
        var title = Palette.Text("Sources to bow", 20); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(title);
        var payments = PaymentsFor(QuoteFor(_chosen!)!, required);
        if (payments.Length > 1)
        {
            var suggestions = new OptionButton { FitToLongestItem = false, ClipText = true, CustomMinimumSize = new(152, 34) };
            for (var i = 0; i < payments.Length; i++) suggestions.AddItem(i == 0 ? "Suggested" : "Alternative " + i);
            suggestions.AccessibilityName = "Choose engine-suggested payment sources";
            suggestions.Selected = Array.FindIndex(payments, p => p.SourceIds.ToHashSet().SetEquals(_sources));
            suggestions.ItemSelected += i =>
            {
                if (!DecisionCanSubmit) return;
                _sources.Clear(); foreach (var id in payments[(int)i].SourceIds) _sources.Add(id);
                RenderInspector(); Highlight();
            };
            var style = SidebarSkin.Surface(true); style.ContentMarginLeft = style.ContentMarginRight = 10;
            style.ContentMarginTop = style.ContentMarginBottom = 4; suggestions.AddThemeStyleboxOverride("normal", style);
            suggestions.AddThemeFontSizeOverride("font_size", 15); header.AddChild(suggestions);
        }
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 171), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        var grid = new HBoxContainer(); grid.AddThemeConstantOverride("separation", 8); scroll.AddChild(grid); _inspector.AddChild(scroll);
        foreach (var card in Self.Home.Concat(Self.Army).Append(Self.Stronghold).Where(c => Production(c, Self) > 0).OrderByDescending(c => _sources.Contains(c.Id)).ThenBy(c => c.Bowed))
        {
            var face = new PaymentSourceFace(card, Production(card, Self), _sources.Contains(card.Id)) { Disabled = card.Bowed || !DecisionCanSubmit };
            face.Pressed += () =>
            {
                if (!DecisionCanSubmit || card.Bowed) return;
                if (!_sources.Add(card.Id)) _sources.Remove(card.Id);
                RenderInspector(); Highlight();
                _inspector.FindChildren("*", "Button", true, false).OfType<PaymentSourceFace>().FirstOrDefault(b => b.Card.Id == card.Id)?.CallDeferred(Control.MethodName.GrabFocus);
            };
            grid.AddChild(face);
        }
        var produced = SelectedProduction();
        _decisionActions.AddChild(Palette.Wrapped($"{produced} produced / {required} required" +
            (produced > required ? $" · {produced - required} excess lost" : produced < required ? " · Choose more ready sources" : " · Exact payment"), 17,
            produced < required ? Palette.Error : Palette.Gold));
    }
}
