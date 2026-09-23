namespace L5R.Core;

// Moshi Ino (oracle-l5r-10066, issue #453 batch 3): "Battle: Ino summons a storm. After the next action not performed by a Thunder Personality resolves this
// battle, the player who took it chooses to discard a card or bow the performer. You may take an additional Battle action."
//
// A standing watch on Ino herself (her AbilityUses, under a key no ability uses: the number of the battle it was summoned in), asked at the one point every action
// -- an ability, a deferred Attack, a duel -- is truly done (FinishActionEffectTracking, ActionEffectTracking.cs), once no Reaction window opened over that action.
// The performer is the action's chosen performer (the running command's PerformerId), else the card the action is on (an Item or Spell acts through its wielder, a
// Follower for itself); an action with no performer at all (a Strategy with none, the Imperial Favor) is "not performed by a Thunder Personality" too. The choice is
// the general Either/Or (EitherOr.cs) handed to the player who took the action: "discard" queues a forced discard he answers next (PendingForcedDiscards), "bow"
// bows the performer where it stands. Her own summoning action never trips her own storm. The storm lapses with the battle.
public sealed partial class MatchEngine
{
    private const string InoStormBattleKey = "trait:ino-storm-battle";

    private void ArmInoStorm(CardInstance ino)
    {
        ino.AbilityUses[InoStormBattleKey] = _state.BattleNumber;
        Emit("stormSummoned", $"{Definition(ino).Name} summons a storm: after the next action not performed by a Thunder Personality resolves this battle, the player who took it must discard a card or bow the performer.", ino.OwnerId, ino.Id);
    }

    private bool InoStormArmed(CardInstance card) => card.AbilityUses.TryGetValue(InoStormBattleKey, out var battle) && battle == _state.BattleNumber;

    // The card that performed the just-resolved action, for "not performed by a Thunder Personality" and "bow the performer".
    private CardInstance? PerformerOfResolvedAction(ResolvedActionFacts facts)
    {
        if (_runningCommand is { } running && running.AbilityId == facts.AbilityId && running.PlayerId == facts.PlayerId && running.PerformerId is { } performerId
            && _state.Cards.TryGetValue(performerId, out var chosen)) return ActingPersonality(chosen);
        if (facts.SourceId is null || !_state.Cards.TryGetValue(facts.SourceId, out var source)) return null;
        return Definition(source).Type switch
        {
            CardType.Personality or CardType.Follower => source,
            CardType.Item or CardType.Spell => source.Zone == Zone.Attached && source.AttachedTo is { } wielderId ? Card(wielderId) : null,
            _ => null
        };
    }

    // True when a decision is now open (the caller returns; FinishEitherOr resumes). An automatically-answered choice that queued a discard opens that discard here.
    private bool OpenInoStorm(int closesTo)
    {
        if (_state.LastResolvedAction is not { } facts || _state.Phase != Phase.Battle) return false;
        var ino = _state.Cards.Values.Where(c => InPlay(c) && InoStormArmed(c) && !(facts.AbilityId == "ino-storm" && facts.SourceId == c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (ino is null) return false;
        var performer = PerformerOfResolvedAction(facts);
        if (performer is not null && Definition(performer).Type == CardType.Personality && Keyword(performer, "Thunder")) return false;
        ino.AbilityUses.Remove(InoStormBattleKey);
        Emit("stormBreaks", $"{Definition(ino).Name}'s storm breaks over {Player(facts.PlayerId).Name}'s action.", ino.OwnerId, ino.Id);
        var queued = _state.PendingForcedDiscards.Count;
        if (OpenEitherOr(EitherOrSpecs.InoStorm, ino, facts.PlayerId, facts.PlayerId, performer is not null && InPlay(performer) ? performer : null, "action", null, stayWithActor: closesTo == facts.PlayerId)) return true;
        return _state.PendingForcedDiscards.Count > queued && OpenOverconfidentDrawWindow(closesTo);
    }

    private EitherOrOption[] InoStormOptions(PendingEitherOr p, CardInstance? performer)
    {
        var name = performer is null ? "the performer" : Definition(performer).Name;
        var canBow = performer is not null && InPlay(performer);
        return [
            new("discard", "Discard a card", "You discard a card of your choice from your hand.", EitherOrKind.Choose, 0, Player(p.DeciderPlayerId).Hand.Count > 0, "You have no card to discard."),
            new("bow", $"Bow {name}", $"{name} is bowed.", EitherOrKind.Choose, 0, canBow, "The action had no performer in play to bow.")
        ];
    }

    private void ApplyInoStorm(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? performer)
    {
        var source = Card(p.SourceCardId);
        if (option.Key == "discard")
        {
            _state.PendingForcedDiscards.Add(new(decider.Id, $"{Definition(source).Name}'s storm"));
            Emit("eitherOrChosen", $"{decider.Name} chose to discard a card ({Definition(source).Name}'s storm).", decider.Id, source.Id);
            return;
        }
        if (performer is not null && InPlay(performer)) performer.Bowed = true;
        Emit("eitherOrChosen", $"{decider.Name} chose to bow {(performer is null ? "the performer" : Definition(performer).Name)} ({Definition(source).Name}'s storm).", decider.Id, performer?.Id ?? source.Id);
    }
}
