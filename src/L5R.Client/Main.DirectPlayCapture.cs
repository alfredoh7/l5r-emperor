using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _directPlayStarted;

    private void ProcessDirectPlayCapture(double delta)
    {
        _captureClock += delta; if (_directPlayStarted || _captureClock < 1.5) return;
        _directPlayStarted = true; _ = CheckDirectPlay();
    }

    private IEnumerable<Button> ActionTextBlocks() => _inspector.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.HasMeta("action_text_block"));

    private async Task LoadDirectPlay(Action<MatchState> build, int seat = 0)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
        state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        build(state);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, seat);
    }

    private static void BattleSetup(MatchState state)
    {
        state.Phase = Phase.Battle; state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.BattleNumber = 1;
        SupportFixtureCard(state, 229, 0, Zone.Battlefield, 0);               // Akodo Kisho, unbowed Paragon
        SupportFixtureCard(state, 3636, 1, Zone.Battlefield, 0);              // an enemy unit at the battlefield
    }

    private async Task CheckDirectPlay()
    {
        try
        {
            // 1. One legal, automatic action: selecting the card asks to confirm, and confirming plays it with nothing else chosen.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 7862, 0, Zone.Hand); });
            var tale = View.Players[0].Hand.Single();
            var honor = View.Players[0].Honor;
            SelectCard(tale); await EventFrames(4);
            SupportRequire(_playPrompt is not null && _chosen is null, "Selecting a playable card did not ask to confirm play");
            SupportRequire(StabilizationText(_inspector).Contains("PLAY THIS CARD?") && StabilizationText(_inspector).Contains("Nothing else to choose"), "Automatic action should say nothing else needs choosing");
            var cancel = StabilizationButtons(_decisionActions).Single(b => b.Text == "Cancel"); cancel.EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_playPrompt is null && _chosen is null && View.Players[0].Honor == honor, "Cancel must back out without acting");
            SelectCard(tale); await EventFrames(2);
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Confirm play")); await WaitDecisionPresentation(); await EventFrames(6);
            SupportRequire(View.Players[0].Honor == honor + 1 && _chosen is null, "Confirming Tell the Tale did not gain 1 Honor directly");

            // 2. Two legal actions: the card lists both as clickable action text; a click asks to confirm, then continues to targets.
            await LoadDirectPlay(s => { BattleSetup(s); SupportFixtureCard(s, 61, 0, Zone.Hand); });
            var paragon = View.Players[0].Hand.Single();
            SelectCard(paragon); await EventFrames(4);
            var blocks = ActionTextBlocks().ToArray();
            SupportRequire(_playPrompt is null && blocks.Length == 2, $"Two legal actions should be offered as selectable text, found {blocks.Length}: " + string.Join(" | ", Actions.Where(x => x.CardId == paragon.Id).Select(x => $"{x.Type}/{x.Command?.AbilityId}/{x.Enabled}/{x.Reason}")));
            await SupportClick(blocks[0]); await EventFrames(2);
            SupportRequire(_playPrompt is not null && _chosen is null, "Choosing an action's text should ask to confirm that action");
            var first = PromptedChoice()!.Key;
            StabilizationButtons(_decisionActions).Single(b => b.Text == "Cancel").EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_playPrompt is null && _chosen is null, "Cancel returns to the card");
            SelectCard(paragon); await EventFrames(2);
            ActionTextBlocks().ElementAt(1).EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_playPrompt is not null && PromptedChoice()!.Key != first, "The second action's text should select the second action");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Confirm play")); await EventFrames(4);
            SupportRequire(_chosen is { Type: "useAbility" } && _playPrompt is null, "Confirming a targeted action continues into performer/target selection");

            // 3. Only one of the card's two actions is legal (no unbowed Kensai): assume it, no choice of action.
            await LoadDirectPlay(s => { BattleSetup(s); SupportFixtureCard(s, 5112, 0, Zone.Battlefield, 0, bowed: true); SupportFixtureCard(s, 5423, 0, Zone.Hand); });
            var moving = View.Players[0].Hand.Single();
            var legal = Actions.Where(a => a.Enabled && a.CardId == moving.Id).Select(a => a.Command?.AbilityId).Distinct().ToArray();
            SelectCard(moving); await EventFrames(4);
            SupportRequire(legal.Length == 1 && _playPrompt is not null && ActionTextBlocks().Count() == 0, $"With one legal action ({legal.Length}) it should be assumed");
            SupportRequire(PromptedChoice()!.Option.Command!.AbilityId == "moving-unmoving-approach", "The legal first action should be the one assumed");
            GD.Print("DIRECT_PLAY_PASS: confirm-first for an automatic action, two-action text selection with cancel, assumed single legal action.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
