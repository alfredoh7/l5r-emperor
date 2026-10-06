using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Native probe for the province picker (Shrine to Nimuro: "target a province"). Real engine options drive the real sidebar. Mode: published-province-target.
public partial class Main
{
    private bool _provinceTargetCaptureStarted;
    private void ProcessProvinceTargetCapture(double delta)
    {
        _captureClock += delta; if (_provinceTargetCaptureStarted || _captureClock < 1.5) return;
        _provinceTargetCaptureStarted = true; _ = CaptureProvinceTarget();
    }
    private IEnumerable<Button> ProvinceRows() => _inspector.GetChildren().OfType<Button>().Where(b => b.HasMeta("province_target_row"));
    private async Task CaptureProvinceTarget()
    {
        try
        {
            var state = MatchEngine.CreatePublishedTestMatch(42).State;
            state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
            var shrine = SupportFixtureCard(state, 7227, 0, Zone.Home);
            var lion = SupportFixtureCard(state, 10292, 0, Zone.Home);
            lion.GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent)); lion.GrantedKeywords.Add(new("Tactician", "test-fixture", GrantDuration.Permanent));
            var decoy = SupportFixtureCard(state, 10292, 0, Zone.Hand); // a Lion Tactician that is not in play must never be offered
            decoy.GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent)); decoy.GrantedKeywords.Add(new("Tactician", "test-fixture", GrantDuration.Permanent));
            var openProvince = state.Players[1].Provinces.First(p => !p.Destroyed && p.CardId is not null);
            state.Cards[openProvince.CardId!].FaceUp = true;
            var closedProvince = state.Players[1].Provinces.First(p => !p.Destroyed && p.CardId is not null && p.Index != openProvince.Index);
            state.Cards[closedProvince.CardId!].FaceUp = false;
            var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
            await LoadSupportFixture(engine, 0);

            var offered = Actions.Where(a => a.Command?.AbilityId == "p2-shrine-nimuro-lower").ToArray();
            SupportRequire(offered.Length > 0 && offered.All(a => a.Command!.PerformerId != decoy.Id), "A Tactician outside play was offered as the performer");
            SelectCard(FindCard(shrine.Id)!); await EventFrames(2);
            var groupButtons = _inspector.GetChildren().Concat(_phaseActions.GetChildren()).OfType<Button>().Count(b => b.HasMeta("province_target_group"));
            SupportRequire(groupButtons == 1, $"Expected one grouped Shrine button, found {groupButtons}");
            Choose(offered.First(a => a.Enabled));
            SupportRequire(ProvinceTargetActive && _chosen!.Command!.Province is null, "The client chose a province for the player");
            SupportRequire(_paymentConfirm is { Disabled: true }, "Confirm was available before a province was picked");

            var rows = ProvinceRows().ToArray();
            var headings = _inspector.GetChildren().OfType<Label>().Select(l => l.Text).Where(t => t is "OPPONENT'S PROVINCES" or "YOUR PROVINCES").ToArray();
            SupportRequire(headings.SequenceEqual(["OPPONENT'S PROVINCES", "YOUR PROVINCES"]), "Provinces are not listed in the two groups: " + string.Join("|", headings));
            var opponent = View.Players.First(p => p.Id != View.ViewerId);
            var opponentRows = rows.Where(r => r.GetMeta("province_target_row").AsString().StartsWith(opponent.Id + ":", StringComparison.Ordinal)).ToArray();
            SupportRequire(opponentRows.Length == opponent.Provinces.Count(p => !p.Destroyed) && rows.Length == View.Players.Sum(p => p.Provinces.Count(q => !q.Destroyed)), "A province is missing from the list");
            var openText = opponentRows.Single(r => r.GetMeta("province_target_row").AsString() == $"{opponent.Id}:{openProvince.Index}").Text;
            var openName = engine.State.Cards[openProvince.CardId!] is { } openCard ? CardCatalog.Get(openCard.DefinitionId).Name : "";
            SupportRequire(openText.Contains($"Province {openProvince.Index + 1}", StringComparison.Ordinal) && openText.Contains(openName, StringComparison.Ordinal), "A face-up province row lacks its number and card title: " + openText);
            var closedText = opponentRows.Single(r => r.GetMeta("province_target_row").AsString() == $"{opponent.Id}:{closedProvince.Index}").Text;
            SupportRequire(closedText.Contains($"Province {closedProvince.Index + 1}", StringComparison.Ordinal) && closedText.Contains("Face down", StringComparison.Ordinal), "A face-down province row does not say so: " + closedText);

            // Hovering a row lights its province on the table; leaving it puts the table back.
            var openCards = ProvincePresentation.Cards(opponent.Provinces.Single(p => p.Index == openProvince.Index)).ToArray();
            var hoverRow = opponentRows.Single(r => r.GetMeta("province_target_row").AsString() == $"{opponent.Id}:{openProvince.Index}");
            hoverRow.EmitSignal(Control.SignalName.MouseEntered); await EventFrames(2);
            SupportRequire(_tiles.Any(t => cardsMatch(t.Card, openCards) && t.ForcedHover), "Hovering a row did not light the province's card");
            await SupportCapture("-hover");
            hoverRow.EmitSignal(Control.SignalName.MouseExited); await EventFrames(2);
            SupportRequire(!_tiles.Any(t => cardsMatch(t.Card, openCards) && t.ForcedHover), "Leaving the row left the province lit");

            // Pick one row, then change the pick on the table; only then does Confirm unlock.
            opponentRows.Single(r => r.GetMeta("province_target_row").AsString() == $"{opponent.Id}:{closedProvince.Index}").EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_chosen!.Command is { Province: { } first, TargetPlayerId: { } firstOwner } && first == closedProvince.Index && firstOwner == opponent.Id && _paymentConfirm is { Disabled: false }, "Picking a row did not select that province");
            SelectProvince(opponent.Id, openProvince.Index); await EventFrames(2);
            SupportRequire(_chosen!.Command is { Province: { } second } && second == openProvince.Index, "Clicking the province on the table did not change the pick");
            await SupportCapture("-picked");
            _paymentConfirm!.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFor(() => View.Players.First(p => p.Id != View.ViewerId).Provinces.Single(p => p.Index == openProvince.Index).Strength == 0, "The picked province was not lowered to 0");
            SupportRequire(View.Players.First(p => p.Id != View.ViewerId).Provinces.Where(p => p.Index != openProvince.Index && !p.Destroyed).All(p => p.Strength > 0), "Another province was lowered");
            GD.Print("PROVINCE_TARGET_PASS: out-of-play Tacticians not offered; one grouped action; provinces listed as Opponent's/Your with number and title or Face down; hover lights the province; the player picks, Confirm waits for the pick, and exactly that province resolves.");
            GetTree().Quit();
        }
        catch (Exception e) { try { await SupportCapture("-failure"); } catch { } GD.PushError(e.ToString()); GetTree().Quit(1); }

        static bool cardsMatch(CardView tileCard, CardView[] cards) => cards.Any(c => ReferenceEquals(c, tileCard) || c.Id.Length > 0 && c.Id == tileCard.Id);
    }
}
