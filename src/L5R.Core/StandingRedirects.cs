namespace L5R.Core;

// Standing redirects (Seeds of Decay pass, issue #453, batch 1). A one-shot Reaction redirect (Ezuno, Blade of Champions) retargets an action that is
// already held in a reaction window; these three cards instead set up a WATCH that rewrites a later action of another player's the moment it is announced,
// before any reaction window opens for it:
//   - Ceaseless Vigil: "The next time (this turn) another player's action would target one of your Personalities, it targets the Yojimbo instead, if legal."
//   - Ruthless Onslaught: "The next time this battle another player's Battle action would target one of your cards, it targets your performer, if legal."
//   - Metsubushi: "The next time this battle the enemy leader takes a Battle action, if it can legally target the Ninja, it must target him."
// "If legal" is the one definition of a legal target the redirects already use: would ValidateAbility accept the same command aimed at the new card
// (AbilityRejection -- an Attack's target rule, a Target category's own conditions, the Gold the acting player committed). The watch is spent by the
// next matching action whether or not the redirect turns out legal ("the next time"); a watch whose card has left play, or whose turn or battle is over,
// lapses by itself. A multi-target action is judged and retargeted on the first card of its up-front slots (RetargetSlotAction), a single-target one on
// its TargetId; Metsubushi's watch also reaches an action that names no card yet (an Attack with no target), which then targets the Ninja if it may.
public static class StandingRedirectKinds
{
    /// <summary>Another player's action (any Timing) targeting one of the owner's Personalities.</summary>
    public const string OwnPersonalities = "own-personalities";
    /// <summary>Another player's Battle action targeting one of the owner's cards.</summary>
    public const string OwnCardsBattleAction = "own-cards-battle";
    /// <summary>Another player's next Battle action, whatever it targets: it must target the card if it legally can.</summary>
    public const string MustTarget = "must-target";
}

public sealed partial class MatchEngine
{
    private void AddStandingRedirect(string kind, int ownerId, CardInstance to, bool thisBattle, string sourceName)
    {
        _state.StandingRedirects.Add(new StandingRedirect(kind, ownerId, to.Id, _state.TurnNumber, thisBattle ? _state.BattleNumber : null, sourceName));
        Emit("standingRedirect", kind == StandingRedirectKinds.MustTarget
            ? $"{sourceName}: the next time this battle the enemy leader takes a Battle action, it must target {Definition(to).Name} if it legally can."
            : $"{sourceName}: the next time {(thisBattle ? "this battle another player's Battle action" : "this turn another player's action")} would target one of {Player(ownerId).Name}'s {(kind == StandingRedirectKinds.OwnPersonalities ? "Personalities" : "cards")}, it targets {Definition(to).Name} instead, if legal.",
            ownerId, to.Id);
    }

    private bool StandingRedirectInForce(StandingRedirect redirect) =>
        redirect.Turn == _state.TurnNumber && (redirect.Battle is null || redirect.Battle == _state.BattleNumber && _state.Phase == Phase.Battle)
        && _state.Cards.TryGetValue(redirect.ToCardId, out var to) && InPlay(to);

    // The card an announced action targets, as the reaction windows see it (the first card of a multi-target action's up-front slots).
    private static string? AnnouncedActionTarget(GameCommand command) => ReactionWindowTargets(command).FirstOrDefault(t => t is not null);

    private static GameCommand? RetargetAnnouncedAction(GameCommand command, AbilityDefinition ability, string? oldTargetId, string newTargetId)
    {
        if (ability.Slots is not null) return oldTargetId is null ? null : RetargetSlotAction(command, oldTargetId, newTargetId);
        return command with { TargetId = newTargetId };
    }

    /// <summary>Applies every standing redirect of another player's that this just-announced action matches, spending each; returns the command as it now stands.</summary>
    private GameCommand ApplyStandingRedirects(GameCommand command)
    {
        _state.StandingRedirects.RemoveAll(r => !StandingRedirectInForce(r));
        if (_state.StandingRedirects.Count == 0 || command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return command;
        var battleAction = IsBattleAction(command);
        foreach (var redirect in _state.StandingRedirects.Where(r => r.OwnerId != command.PlayerId).ToArray())
        {
            var currentTargetId = AnnouncedActionTarget(command);
            var currentTarget = currentTargetId is not null && _state.Cards.TryGetValue(currentTargetId, out var c) ? c : null;
            var applies = redirect.Kind switch
            {
                StandingRedirectKinds.OwnPersonalities => currentTarget is not null && currentTarget.OwnerId == redirect.OwnerId && Definition(currentTarget).Type == CardType.Personality && InPlay(currentTarget),
                StandingRedirectKinds.OwnCardsBattleAction => battleAction && currentTarget is not null && currentTarget.OwnerId == redirect.OwnerId && InPlay(currentTarget),
                StandingRedirectKinds.MustTarget => battleAction,
                _ => false
            };
            if (!applies) continue;
            _state.StandingRedirects.Remove(redirect);
            var to = Card(redirect.ToCardId);
            if (currentTargetId == to.Id) continue;
            var retargeted = RetargetAnnouncedAction(command, ability, currentTargetId, to.Id);
            var rejection = retargeted is null ? "The action names no card to redirect." : AbilityRejection(retargeted);
            if (rejection is not null)
            {
                Emit("standingRedirectIllegal", $"{redirect.SourceName}: the action cannot legally target {Definition(to).Name}, so it is not redirected ({rejection}).", redirect.OwnerId, to.Id);
                continue;
            }
            command = retargeted!;
            Emit("actionRedirected", $"{redirect.SourceName}: the action now targets {Definition(to).Name}{(currentTarget is null ? "" : $" instead of {Definition(currentTarget).Name}")}.", redirect.OwnerId, to.Id);
        }
        return command;
    }
}
