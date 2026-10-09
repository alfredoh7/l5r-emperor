using Godot;
using L5R.Core;

namespace L5R.Client;

// Scripted native verification of the generalized Gold payment panel (issue #303). Each --capture-mode=gold-*
// run starts a real engine fixture (MatchEngine.CreateGoldPaymentShowcase), then drives the panel exactly as a
// player would -- pressing the real action, tile, target-picker, amount and confirm controls -- and asserts
// what the engine and the panel then agree on before the harness saves its screenshot:
//   gold-fixed[-confirm]     an ability with a fixed cost that used to have no panel (Kharmic, 2 Gold)
//   gold-derived[-swapped|-confirm]  a target-derived cost: changing the target swaps in that target's quote
//   gold-variable[-raised|-confirm]  a player-chosen amount with its own bounds and re-suggested sources
//   gold-profits[-confirm]   Profits' "Use Profits" choice, its Wealth-token Gold source and the Coin Pouch, hover-highlighted (issue #667)
public partial class Main
{
    private IEnumerable<Button> GoldButtons(Node? root = null) => (root ?? _inspector).FindChildren("*", "Button", true, false).OfType<Button>();
    private string[] GoldPanelText() => _inspector.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToArray();

    private static void GoldRequire(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Gold payment UI smoke failed: " + message);
    }

    private void PressGold(Func<Button, bool> match, string what, Node? root = null)
    {
        var button = GoldButtons(root).FirstOrDefault(match) ?? throw new InvalidOperationException($"Gold payment UI smoke failed: no {what} control (buttons: {string.Join(" | ", GoldButtons(root).Select(b => b.Text))}).");
        GoldRequire(!button.Disabled, $"the {what} control is disabled.");
        button.EmitSignal(Button.SignalName.Pressed);
    }

    // Pays the way a player does: clicks each suggested holding on the table (issue #731). The hold-over from the engine's suggestion is a probe convenience only.
    private void GoldClickHoldings(IEnumerable<string> methodIds)
    {
        var methods = PaymentMethods(QuoteFor(_chosen!)!);
        foreach (var id in methodIds)
        {
            var method = methods.First(m => m.Id == id);
            SelectCard(FindCard(method.CardId)!);
            GoldRequire(_sources.Contains(id), $"clicking the table holding for {id} did not select it.");
            GoldRequire(_tiles.Single(t => t.Card.Id == method.CardId).Card.Bowed, "a payment holding was not drawn bowed.");
        }
    }

    private void GoldRequireMeter(int paid, int? cost, string what)
        => GoldRequire(_goldMeter.Visible && _goldMeter.Paid == paid && _goldMeter.Cost == cost, $"{what}: the coin meter shows {(_goldMeter.Visible ? _goldMeter.Paid + "/" + _goldMeter.Cost : "nothing")}, not {paid}/{cost}.");

    private void PrepareGoldPaymentCapture()
    {
        var parts = _captureMode.Split('-');
        var kind = parts[1]; var stage = parts.Length > 2 ? parts[2] : "";
        _uiQueue.Clear(); _selected = null; _chosen = null; Render();
        var version = View.Version;
        switch (kind)
        {
            case "fixed": GoldFixedCapture(stage, version); break;
            case "derived": GoldDerivedCapture(stage, version); break;
            case "variable": GoldVariableCapture(stage, version); break;
            case "profits": GoldProfitsCapture(stage, version); break; // issue #667 (Main.GoldProfitsCapture.cs)
            default: throw new InvalidOperationException("Unknown gold capture mode " + _captureMode);
        }
        RenderBoard(); ResetTableCamera();
    }

    private void GoldFixedCapture(string stage, long version)
    {
        // Kharmic is a player ability listed in the inspector; before this change the engine never offered it enabled.
        PressGold(b => b.Text.StartsWith("Pay 2 Gold, discard a Kharmic", StringComparison.Ordinal), "Kharmic action");
        GoldRequire(_chosen is { Type: "useAbility" } && QuoteFor(_chosen) is { Kind: GoldCostKind.Fixed, Cost: 2 }, "Kharmic did not open with its fixed 2 Gold quote.");
        GoldPickFirstTarget();
        var text = GoldPanelText();
        GoldRequire(text.Contains("PAYMENT · FIXED COST") && text.Contains("2 Gold"), "the panel does not name the fixed cost.");
        GoldRequire(GoldButtons(_inspector).Any(b => b is CheckBox), "the panel lists no Gold sources to bow.");
        GoldRequire(_sources.Count == 0 && _paymentConfirm is { Disabled: true, Visible: false }, "the payment opened pre-selected, or Confirm showed before the cost was covered.");
        GoldRequireMeter(0, 2, "fresh payment");
        GoldClickHoldings(QuoteFor(_chosen!)!.Payments[0].SourceIds);
        GoldRequire(SelectedProduction() >= 2 && _paymentConfirm is { Disabled: false, Visible: true }, "the clicked holdings did not cover the cost and reveal Confirm.");
        GoldRequireMeter(SelectedProduction(), 2, "covered payment");
        GoldRequire(View.Version == version, "opening the panel changed the match.");
        // Clicking a bowed holding on the table straightens it, takes its Gold off the numerator and hides Confirm again.
        var chosen = _sources.ToArray();
        var bowedCard = PaymentMethods(QuoteFor(_chosen!)!).First(m => m.Id == chosen[0]).CardId;
        SelectCard(_tiles.Single(t => t.Card.Id == bowedCard).Card);
        GoldRequire(!_sources.Contains(chosen[0]) && _goldMeter.Paid < 2 && !_tiles.Single(t => t.Card.Id == bowedCard).Card.Bowed, "straightening a source did not lower the numerator and unbow the holding.");
        GoldRequire(_paymentConfirm is { Disabled: true, Visible: false }, "Confirm stayed offered below the cost.");
        GoldClickHoldings([chosen[0]]);
        GoldRequire(_paymentConfirm is { Disabled: false, Visible: true }, "Confirm did not return once the cost was covered again.");
        if (stage == "confirm")
        {
            var paying = _sources.ToArray();
            _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
            _uiQueue.Clear(); Render();
            GoldRequire(View.Version == version + 1, "the engine rejected the confirmed Kharmic payment.");
            GoldRequire(paying.All(id => FindCard(id)?.Bowed == true), "the chosen sources did not bow.");
        }
        GD.Print($"UI_SMOKE_PASS: Kharmic's fixed 2 Gold cost opened the shared payment panel through its real button; suggested sources covered it; confirmed={stage == "confirm"}.");
    }

    // Issue #581: a targeted action starts on "Choose a target…". The capture picks through the real picker, as a player would.
    private void GoldPickFirstTarget()
    {
        if (_chosen?.TargetIds is not { Length: > 0 } || _chosen.Command?.TargetId is not null) return;
        _inspector.GetChildren().OfType<CardChoiceList>().First().Choose(0); // issue #635: the list of legal cards
    }

    private void GoldDerivedCapture(string stage, long version)
    {
        PressGold(b => b.Text.Contains("Equip (Battle)", StringComparison.Ordinal), "Equip (Battle) action");
        var action = _chosen!;
        GoldRequire(action.TargetGold is { Count: >= 3 } && action.TargetGold.Values.Select(q => q.Cost).Distinct().Count() >= 3, "Equip (Battle) did not ship distinct per-target quotes.");
        // Issue #581: the client picks no target for the player. Nothing is priced and Confirm waits until one is chosen through the real picker (item 0 is "Choose a target…").
        GoldRequire(action.Command!.TargetId is null && _paymentConfirm is { Disabled: true } && !GoldPanelText().Contains("PAYMENT"), "a target-priced action chose or priced a target before the player picked one.");
        var firstTarget = action.TargetIds![0];
        _inspector.FindChildren("*", "VBoxContainer", true, false).OfType<CardChoiceList>().First().Choose(0); // issue #635: the list of legal cards replaced the drop-down
        action = _chosen!; var firstCost = QuoteFor(action)!.Cost;
        GoldRequire(action.Command!.TargetId == firstTarget, "the picker did not select the first legal target.");
        GoldRequire(GoldPanelText().Contains("PAYMENT · COST FROM YOUR TARGET") && GoldPanelText().Contains($"{firstCost} Gold"), "the panel does not show the chosen target's cost.");
        if (stage != "")
        {
            // Change the target through the real picker: the quote, the amount and the suggested sources all follow, offline.
            var costliest = action.TargetGold!.OrderByDescending(p => p.Value.Cost).First();
            GoldRequire(costliest.Key != firstTarget && costliest.Value.Cost != firstCost, "the fixture has no differently priced target to swap to.");
            _inspector.FindChildren("*", "VBoxContainer", true, false).OfType<CardChoiceList>().First().Choose(Array.IndexOf(action.TargetIds!, costliest.Key));
            GoldRequire(_chosen!.Command!.TargetId == costliest.Key && QuoteFor(_chosen)!.Cost == costliest.Value.Cost, "changing the target did not swap in its own quote.");
            GoldRequire(GoldPanelText().Contains($"{costliest.Value.Cost} Gold"), "the panel does not show the new target's cost.");
            GoldRequireMeter(0, costliest.Value.Cost, "retargeted payment");
            GoldClickHoldings(costliest.Value.Payments[0].SourceIds);
            GoldRequire(SelectedProduction() >= costliest.Value.Cost, "clicked holdings did not cover the new target's quote.");
            GoldRequire(View.Version == version, "retargeting changed the match.");
            if (stage == "confirm")
            {
                var paying = _sources.ToArray(); var spell = costliest.Key;
                _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
                _uiQueue.Clear(); Render();
                GoldRequire(View.Version == version + 1, "the engine rejected the confirmed Equip (Battle) payment.");
                GoldRequire(FindCardAnywhere(spell) is { Zone: Zone.Attached }, "the chosen Spell did not attach.");
                GoldRequire(paying.All(id => FindCard(id)?.Bowed == true), "the chosen sources did not bow.");
            }
        }
        GD.Print($"UI_SMOKE_PASS: Equip (Battle) shipped per-target quotes; retargeting swapped quote and sources without a round trip; stage='{stage}'.");
    }

    private CardView? FindCardAnywhere(string id) => View.Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Hand).Concat(p.Discard)).FirstOrDefault(c => c.Id == id);

    private void GoldVariableCapture(string stage, long version)
    {
        var fixture = Self.Home.Single(c => c.Definition?.Id == "training-cost-fixture");
        _tiles.Single(t => t.Card.Id == fixture.Id).EmitSignal(Button.SignalName.Pressed);
        PressGold(b => b.Text.StartsWith("Training (Virtuous Charity shape)", StringComparison.Ordinal), "variable-cost action");
        GoldRequire(_chosen is not null && QuoteFor(_chosen) is { Kind: GoldCostKind.Variable, MinAmount: 0 } quote && quote.MaxAmount > 0, "the ability did not open with a variable quote.");
        var quoteNow = QuoteFor(_chosen!)!;
        GoldRequire(GoldPanelText().Contains("PAYMENT · YOU CHOOSE THE AMOUNT"), "the panel does not name a player-chosen cost.");
        GoldRequire(AmountFor(_chosen!, quoteNow) == 0 && _paymentConfirm is { Disabled: false, Visible: true }, "a variable cost is not immediately confirmable.");
        GoldRequireMeter(0, null, "variable cost with nothing bowed");
        var honor = Self.Honor;
        if (stage == "")
        { // Confirming 0 Gold asks first; "No" keeps the payment open.
            _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
            var dialog = GetChildren().OfType<ConfirmationDialog>().FirstOrDefault(d => d.DialogText.Contains("pay 0 gold", StringComparison.Ordinal));
            GoldRequire(dialog is not null && dialog.OkButtonText == "Yes" && dialog.CancelButtonText == "No" && View.Version == version, "confirming 0 Gold did not ask first.");
            dialog!.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            GoldRequire(_chosen is not null && View.Version == version, "answering No dropped the payment.");
        }
        if (stage is "raised" or "confirm")
        {
            GoldClickHoldings(quoteNow.PaymentsByAmount![4][0].SourceIds);
            GoldRequire(_chosen!.Command!.GoldAmount == 4 && GoldPanelText().Contains("PAYMENT SUMMARY"), "bowing holdings did not set the chosen amount to the Gold contributed.");
            GoldRequireMeter(4, null, "variable cost after bowing");
            // Clicking a payment-bowed holding straightens it and takes its Gold back.
            var first = quoteNow.PaymentsByAmount![4][0].SourceIds[0];
            var before = _goldMeter.Paid;
            SelectCard(_tiles.Single(t => t.Card.Id == PaymentMethods(quoteNow).First(m => m.Id == first).CardId).Card);
            GoldRequire(!_sources.Contains(first) && _goldMeter.Paid < before && _chosen.Command!.GoldAmount < 4, "clicking a bowed payment holding did not straighten it and lower the numerator.");
            GoldClickHoldings([first]);
            GoldRequire(_goldMeter.Paid == before && _chosen.Command!.GoldAmount == 4, "re-bowing the holding did not restore the Gold.");
            GoldRequire(View.Version == version, "adjusting the payment changed the match.");
            if (stage == "confirm")
            {
                var paying = _sources.ToArray();
                _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
                _uiQueue.Clear(); Render();
                GoldRequire(View.Version == version + 1, "the engine rejected the confirmed variable payment.");
                GoldRequire(Self.Honor == honor + 4, "the effect did not read the chosen amount (4).");
                GoldRequire(paying.All(id => FindCard(id)?.Bowed == true), "the chosen sources did not bow.");
            }
        }
        GD.Print($"UI_SMOKE_PASS: player-chosen Gold amount rendered, bounded by the quote, re-suggested sources and validated against the chosen amount; stage='{stage}'.");
    }
}
