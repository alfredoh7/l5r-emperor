using Godot;
using L5R.Core;

namespace L5R.Client;

// P25 (issue #723) native probe, run by tools/check-decision-chooser.sh (mode "p25"): the three decision shapes the infrastructure adds, each opened by the isolated engine showcase
// (DecisionInfraShowcase.cs) and answered through the real client controls -- a pending type the client has no screen for (rendered generically), a multi-pick Either/Or with a Done option, and a
// choice among shown cards drawn with their faces.
public partial class Main
{
    private async Task CaptureDecisionInfra()
    {
        // 1. A pending type nothing in the client names: generic buttons, nothing preselected, an explicit confirm.
        await PrepareDecisionReview("p25-unknown");
        DecisionRequire(View.PendingChoice?.Type == "p25Showcase" && GenericDecisionActive, "The unknown pending type was not rendered as a generic decision");
        DecisionRequire(DecisionButton("Confirm choice").Disabled, "The unknown decision defaulted a choice");
        var version = View.Version;
        var pick = Actions.First(a => a.Type == "p25ShowcasePick" && a.Enabled);
        DecisionRequire(Actions.Count(a => a.Type == "p25ShowcasePick") == 3, "The engine's options were not all listed");
        await SupportCapture("-unknown"); await SupportClick(DecisionButton(pick.Label));
        DecisionRequire(View.Version == version, "Selecting an option changed the game");
        await SupportClick(DecisionButton("Confirm choice")); await WaitDecisionPresentation();
        DecisionRequire(View.Version == version + 1 && View.PendingChoice is null, "The unknown decision did not resolve exactly once");
        GD.Print("P25_DECISION_PASS: unknown pending type");

        // 2. A multi-pick Either/Or: Done is listed but unavailable until a pick is made; each pick is an ordinary option and a draft until confirmed.
        await PrepareDecisionReview("p25-multi");
        DecisionRequire(EitherOrActive && View.PendingEitherOr is { } multi && multi.Options.Select(o => o.Key).SequenceEqual(["alpha", "beta", "gamma", "done"]), "The multi-pick options were not rendered");
        DecisionRequire(EitherButton("done").Disabled, "Done was available before any pick");
        await SupportCapture("-multi");
        version = View.Version;
        await PickEither("beta"); await ConfirmEither();
        DecisionRequire(View.Version == version + 1 && View.PendingEitherOr is not null, "A pick below Max must keep the decision open");
        DecisionRequire(View.PendingEitherOr!.Options.Select(o => o.Key).SequenceEqual(["alpha", "gamma", "done"]), "The picked option stayed on offer");
        DecisionRequire(!EitherButton("done").Disabled, "Done stayed unavailable after the minimum was picked");
        await SupportCapture("-multi-picked");
        await PickEither("done"); await ConfirmEither();
        DecisionRequire(View.PendingEitherOr is null && View.PendingChoice is null, "Done did not end the multi-pick decision");
        DecisionRequire(View.Events.Any(e => e.Type == "eitherOrChosen" && e.Message.Contains("beta omen")), "The picked option's effect is absent from the public log");
        GD.Print("P25_DECISION_PASS: multi-pick Either/Or");

        // 3. A choice among shown cards: each candidate's face is drawn from MatchView.ShownCards beside the option that picks it.
        await PrepareDecisionReview("p25-shown");
        DecisionRequire(_shownOpen && OpponentShows.Length == 2, "The shown cards did not open the history, as a new show does for its recipient");
        ToggleShownCards(false); await EventFrames(6); // "Minimize to return to your decision": the same faces are then drawn beside the options
        DecisionRequire(View.PendingChoice?.Type == "shownChoice" && GenericDecisionActive, "The shown choice was not rendered");
        DecisionRequire(DecisionButton("Confirm choice").Disabled, "The shown choice defaulted a card");
        DecisionRequire(View.ShownCards!.Length == 2 && Actions.Count(a => a.Type == "chooseShown") == 2, "The shown cards and their options are not both listed");
        DecisionRequire(StabilizationButtons(_inspector).Count(b => b.Text.Contains("Choose ·", StringComparison.Ordinal)) == 2, "An option for each shown card is missing");
        version = View.Version;
        await SupportCapture("-shown");
        var shownOption = Actions.Last(a => a.Type == "chooseShown");
        await SupportClick(DecisionButton(shownOption.Label));
        DecisionRequire(View.Version == version && !DecisionButton("Confirm choice").Disabled, "Selecting a shown card must be a draft");
        await SupportCapture("-shown-selected"); await SupportClick(DecisionButton("Confirm choice")); await WaitDecisionPresentation();
        DecisionRequire(View.Version == version + 1 && View.PendingChoice is null && View.Events.Any(e => e.Type == "shownApplied"), "The shown choice did not resolve exactly once");
        GD.Print("P25_DECISION_PASS: shown choice");
    }
}
