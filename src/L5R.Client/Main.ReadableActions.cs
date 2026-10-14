using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Engine labels carry card ids ("Bow Hida Kisada (card-0123)") for the engine's own matching. A player never needs one: the label is cleaned once per snapshot, and the
    // option keeps its command and every id, so equality and selection still work on the cleaned objects (the cleaned array is cached per source array).
    private ActionOption[]? _readableSource, _readableActions;
    private ActionOption[] ReadableActions(ActionOption[] source)
    {
        if (ReferenceEquals(source, _readableSource) && _readableActions is not null) return _readableActions;
        _readableSource = source;
        return _readableActions = source.Select(o => o with
        {
            Label = BattleActionText.WithoutIds(o.Label),
            DecisionLabel = o.DecisionLabel is null ? null : BattleActionText.WithoutIds(o.DecisionLabel),
            AlternativeCost = o.AlternativeCost is null ? null : o.AlternativeCost with { Label = BattleActionText.WithoutIds(o.AlternativeCost.Label), ActionLabel = BattleActionText.WithoutIds(o.AlternativeCost.ActionLabel) }
        }).ToArray();
    }

    // The label as a placard shows it: the title of the card concerned first ("Hida Kisada · Bow"), never an id. Only the performer, the source and the target of the option are
    // considered, so a name that merely ends a sentence is left alone.
    private string Placard(ActionOption o, string? label = null)
    {
        var names = new[] { o.CardId, o.Command?.PerformerId, o.Command?.TargetId, o.AlternativeCost?.PerformerId }.Where(id => id is not null)
            .Select(id => FindCard(id!)?.Definition?.Name).OfType<string>().Distinct().ToArray();
        return BattleActionText.TitleFirst(label ?? o.Label, names);
    }

    // Performers the player can pick by clicking the card on the table, as a target can be clicked: when a drafted action can be performed by more than one of the player's
    // cards (an alternative-cost action, or a Region/Celestial action), each legal performer maps to the option that selects it. Empty when there is nothing to choose between.
    private Dictionary<string, Action> PerformerClicks()
    {
        if (ReactionPerformerClicks() is { Count: > 0 } reactionClicks) return reactionClicks;
        var clicks = new Dictionary<string, Action>();
        if (View.PendingChoice is not null || _chosen is not { CardId: { } sourceId, Command: { } command } chosen || FindCard(sourceId) is not { } source) return clicks;
        // Issue #793: a hand Strategy offered once per performing Personality (Spirit of the Monk, of the Berserker, ...) is performed by clicking a highlighted Personality, as a support card is.
        // Issue #956: so is an ability of any card in play whose performer is one of several of the player's Personalities (Bayushi Suwabe's Courtier, a Holding's performing Personality).
        if (IsSupportCard(source) || chosen.Type is "playStrategy" or "equip" or "useAbility" && chosen.AlternativeCost is null)
        {
            var choices = Actions.Where(a => a.Enabled && a.CardId == sourceId && a.Command?.AbilityId == command.AbilityId).ToArray();
            foreach (var id in choices.Select(a => a.Command!.PerformerId).OfType<string>().Distinct())
            {
                if (choices.Select(a => a.Command!.PerformerId).Distinct().Count() < 2) break;
                var next = choices.FirstOrDefault(a => a.Command!.PerformerId == id && command.TargetId is { } t && a.TargetIds?.Contains(t) == true) ?? choices.First(a => a.Command!.PerformerId == id);
                clicks[id] = () => Choose(next with { Command = next.Command! with { TargetId = command.TargetId is { } t && next.TargetIds?.Contains(t) == true ? t : next.Command.TargetId } });
            }
        }
        else if (chosen.AlternativeCost is { } cost && !_alternativeCostConfirmed && !_reviewingAlternativeCost)
        {
            var choices = Actions.Where(a => a.Enabled && a.CardId == sourceId && a.AlternativeCost?.AbilityId == cost.AbilityId).ToArray();
            if (choices.Select(a => a.AlternativeCost!.PerformerId).Distinct().Count() > 1)
                foreach (var id in choices.Select(a => a.AlternativeCost!.PerformerId).OfType<string>().Distinct())
                { var next = choices.First(a => a.AlternativeCost!.PerformerId == id); clicks[id] = () => Choose(next); }
        }
        return clicks;
    }
}
