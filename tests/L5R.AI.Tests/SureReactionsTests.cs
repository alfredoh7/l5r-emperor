using L5R.AI;
using L5R.Core;

// Issue #963 (bot loop, round 4): a Reaction that does something is played, not declined by a deterministic roll. See TrainingOpponent.Policy.cs (AbilityScore).
internal static class SureReactionsTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Sure reactions: a helping Reaction the roll would decline is played with the policy on, and still declined by the roll with it off", Decision),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static void Decision()
    {
        // A Reaction ability of rank Help that is not a negation, not an entering-play Reaction and does not harm a card.
        var id = AbilityCatalog.All.Where(p => AbilityIntent.Of(p.Value) is { IsReaction: true, Negates: false, Rank: EffectRank.Help, HonorLoss: false, TakesFavor: false, DiscardsFavor: false } && p.Value.Trigger != "own-enters-play")
            .OrderBy(p => p.Key, StringComparer.Ordinal).First().Key;
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        view = view with { Phase = Phase.Action, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0 };
        // Find a card id whose roll declines it (weight .45 for Help) so the test does not depend on a lucky roll.
        var card = Enumerable.Range(0, 500).Select(i => "reaction-card-" + i).First(c => BotContext.Roll(view.Version, id, c) >= .45);
        var react = new ActionOption("useAbility", id, true, null, new("useAbility", 0, view.Version, CardId: card, AbilityId: id), card);
        var decline = new ActionOption("declineReaction", "Pass reaction", true, null, new("declineReaction", 0, view.Version));
        Check(TrainingOpponent.Choose(view, [decline, react], new BotContext()).Command?.Type == "declineReaction", "policy off: the roll declines it");
        Check(TrainingOpponent.Choose(view, [decline, react], new BotContext { UseSureReactions = true }).Command?.AbilityId == id, "policy on: the helping Reaction is played");
    }
}
