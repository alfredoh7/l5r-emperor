namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Use the same attack-kind registry as resolution. Never classify from labels or printed artwork.
    private AttackCueView? AttackCue(GameCommand? command, bool publicOnly = false, string? fallback = null, string? kind = null)
    {
        kind ??= command?.AbilityId is { } id ? AttackKind(id) : "";
        if (kind is not ("Ranged" or "Melee")) return null;
        foreach (var source in new[] { command?.PerformerId, command?.CardId, fallback })
            if (source is not null && _state.Cards.TryGetValue(source, out var card) && (!publicOnly || InPlay(card)))
                return new(kind, source);
        return null;
    }

    private AttackCueView? BuildPendingAttackCue()
    {
        if (_state.WinnerId is not null) return null;
        if (_state.PendingAttack is { } attack)
            return AttackCue(attack.Command, publicOnly: true, fallback: attack.SourceId, kind: attack.Kind);
        // Announced attacks can wait before their costs/effects execute, including a nested reaction.
        var commands = new[] { _state.PendingAnnouncedAction, _state.PendingAdditionalAction,
            _state.PendingNegatableAbility, _state.PendingOutOfPlayActionReaction, _state.ForcedDiscardContinuation }
            .Concat(_state.SuspendedWindows.AsEnumerable().Reverse().SelectMany(w =>
                new[] { w.NegatableAbility, w.OutOfPlayActionReaction, w.ForcedDiscardContinuation }));
        return commands.Select(c => AttackCue(c, publicOnly: true)).FirstOrDefault(c => c is not null);
    }
}
