using Godot;
using L5R.Core;

namespace L5R.Client;

// Scripted native verification of the new payment sources (issue #667, Profits and Coin Pouch), run by tools/check-gold-payment.sh as --capture-mode=gold-profits[-confirm]. It equips a 2 Gold Item
// through the real payment panel and asserts what a player sees and what the engine then agrees to:
//   * each Profits in play is its own "Use Profits" choice (a source that produces -1 Gold), and a Profits with Wealth tokens is also a Gold source;
//   * the Coin Pouch is a +1 Gold choice beside a Holding;
//   * pointing at a choice highlights the card it belongs to (the specific Profits);
//   * confirming "Use Profits" with enough other Gold equips the Item and puts a Wealth token on that Profits.
public partial class Main
{
    private void GoldProfitsCapture(string stage, long version)
    {
        var item = Self.Hand.Single(c => c.Definition?.Id == "training-item");
        SelectCard(item);
        var option = Actions.Single(a => a.Type == "equip" && a.CardId == item.Id && a.Enabled);
        Choose(option);
        var host = option.TargetIds!.First(id => FindCard(id)?.Definition?.Id == "training-crane-samurai");
        ChooseTarget(host);
        var quote = QuoteFor(_chosen!) ?? throw new InvalidOperationException("Gold payment UI smoke failed: the equip action has no quote.");
        var methods = quote.Sources ?? [];
        var reduce = methods.Where(m => m.Kind == "reduce").ToArray();
        var gold = methods.Where(m => m.Label.Contains("discard it, produce", StringComparison.Ordinal)).ToArray();
        var pouch = methods.Where(m => m.Label.Contains("a Holding bowed in this payment", StringComparison.Ordinal)).ToArray();
        GoldRequire(reduce.Length == 2 && reduce.All(m => m.Produced == -1) && reduce.Select(m => m.CardId).Distinct().Count() == 2, "each Profits in play is its own 'Use Profits' choice producing -1 Gold.");
        GoldRequire(gold.Length == 1 && gold[0].Produced == 2, "only the Profits with two Wealth tokens is a Gold source, worth 2.");
        GoldRequire(pouch.Length == 1 && pouch[0].Produced == 1, "the Coin Pouch is a +1 Gold choice.");
        GoldRequire(GoldPanelText().Contains("PAYMENT SOURCES"), "the payment panel lists no sources.");

        // Hover: the choice highlights its own card, and moving away releases it.
        var checks = _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().ToArray();
        var useBox = checks.First(c => c.GetMeta("payment_source_id", "").AsString() == reduce[0].Id);
        useBox.EmitSignal(Control.SignalName.MouseEntered);
        GoldRequire(_tiles.Single(t => t.Card.Id == reduce[0].CardId).Highlighted && !_tiles.Single(t => t.Card.Id == reduce[1].CardId).Highlighted, "pointing at 'Use Profits' did not highlight that Profits alone.");
        useBox.EmitSignal(Control.SignalName.MouseExited);
        GoldRequire(!_tiles.Single(t => t.Card.Id == reduce[0].CardId).Highlighted, "the highlight was not released.");

        var suggested = _sources.ToArray();
        GoldRequire(!suggested.Any(id => reduce.Any(m => m.Id == id)), "the engine suggested giving up Gold with Profits.");
        // Choosing and confirming run on later idle steps (the harness re-renders the board right after this method; the checkbox's own Toggled handler also moves focus on the idle step after it).
        var profits = reduce[0].CardId;
        Callable.From(() => GoldProfitsToggle(stage, version, item.Id, profits, reduce[0].Id, methods)).CallDeferred();
    }

    private void GoldProfitsToggle(string stage, long version, string itemId, string profitsId, string reduceId, PaymentSource[] methods)
    {
        try
        {
            // Choose "Use Profits" through the real checkbox, beside the engine's suggested Holding(s): the panel still covers the cost, so Confirm stays enabled.
            // A Holding bowed for 2 Gold covers the cost alone, so giving up 1 Gold needs a second Holding: add one the way the other Gold probes do, then use the real checkbox for Profits.
            var covered = methods.Where(m => _sources.Contains(m.Id)).Sum(m => m.Produced);
            if (covered - 1 < 2) { _sources.Add(methods.First(m => m.Kind == "bow" && !_sources.Contains(m.Id)).Id); RenderInspector(); Highlight(); }
            var box = _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().First(c => c.GetMeta("payment_source_id", "").AsString() == reduceId);
            box.ButtonPressed = true;
            GoldRequire(_sources.Contains(reduceId), "toggling 'Use Profits' did not choose it.");
            GoldRequire(SelectedProduction() >= 2 && _paymentConfirm is { Disabled: false }, "the panel ignored the reduction when judging coverage, or Confirm stayed disabled.");
            GoldRequire(View.Version == version, "choosing sources changed the match.");
            if (stage != "confirm") { GD.Print("UI_SMOKE_PASS: Profits and Coin Pouch rendered as payment choices, hover highlighted the specific Profits, Use Profits chosen through the real checkbox; stage=''."); return; }
            Callable.From(() =>
            {
                try
                {
                    _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
                    _uiQueue.Clear(); Render();
                    GoldRequire(View.Version == version + 1, "the engine rejected the confirmed payment.");
                    GoldRequire(FindCard(itemId) is { Zone: Zone.Attached }, "the Item did not attach.");
                    GoldRequire(FindCard(profitsId) is { } used && used.Tokens?.Count(t => t.Type == "Wealth") == 1 && used.Zone == Zone.Home, "the used Profits did not gain exactly one Wealth token.");
                    GD.Print("UI_SMOKE_PASS: Profits and Coin Pouch rendered as payment choices, hover highlighted the specific Profits, Use Profits chosen through the real checkbox and confirmed; stage='confirm'.");
                }
                catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
            }).CallDeferred();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
