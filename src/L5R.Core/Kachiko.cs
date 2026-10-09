namespace L5R.Core;

// Issue #778: Bayushi Kachiko (oracle-l5r-10481, Coils of Madness; profile personality-wave-302, tools/content/package_personality_wave_302.py). Deferred by #734 until Mendon read "attacks" (2026-10-09):
//
//   "Personalities and Followers have -1F while they are Fallen, Nonhuman, or attacking you."
//   "Political Open, bow: After the next time this turn a target Personality attacks, his controller loses 2 Honor."
//
//   Static -1F   PersonalityForce / FollowerForce: any Personality or Follower, either player's, that is Fallen or Nonhuman or whose unit is
//                attacking and whose controller is not Kachiko's controller, for each Kachiko in play. A Follower stands where its Personality does.
//   "Attacks"    read broadly: the first time this turn the target is assigned to an attacking army in the Maneuvers Segment (or assigned to an attacking army by an effect), or joins an attacking army by moving
//                or being moved to a battlefield, its controller loses 2 Honor. The watch is spent by that first time and expires with the turn. It is an AssignmentWatch, fired from the existing assignment
//                sites and from the two movement sites Consecration already uses (FireKachikoJoinWatches).
public static class KachikoAbilities
{
    public const string Effect = "kachiko", Watch = "kachiko-watch", WatchKind = "kachiko", Target = "kachiko-target";
    public const int HonorLoss = 2;
}

public sealed partial class MatchEngine
{
    // "A target Personality": any Personality in play, either player's.
    private bool KachikoTargetAllows(TargetAllowsContext context) =>
        context.ability.Target == KachikoAbilities.Target && InPlay(context.target) && Definition(context.target).Type == CardType.Personality;

    // -1F for each Kachiko in play: the card is Fallen or Nonhuman, or its unit is attacking the Kachiko's controller.
    private int KachikoForce(CardInstance card)
    {
        var definition = Definition(card);
        if (definition.Type != CardType.Personality && definition.Type != CardType.Follower) return 0;
        var unit = definition.Type == CardType.Follower ? WielderOf(card) : card;
        if (unit is null || !InPlay(card)) return 0;
        var fallenOrNonhuman = Keyword(card, "Fallen") || Keyword(card, "Nonhuman");
        var attacking = IsAttacking(unit);
        if (!fallenOrNonhuman && !attacking) return 0;
        var total = 0;
        foreach (var kachiko in _state.Cards.Values)
            if (InPlay(kachiko) && Definition(kachiko).Effect == KachikoAbilities.Effect && (fallenOrNonhuman || unit.OwnerId != kachiko.OwnerId))
                total--;
        return total;
    }

    // "After the next time this turn he joins an attacking army": the Attacker's Personality moves, or is moved, to a battlefield it was not at. Assignment is fired by FireAssignedToAttackWatches.
    private void FireKachikoJoinWatches(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || card.OwnerId != _state.ActivePlayerId) return;
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Kind == KachikoAbilities.WatchKind && w.Turn == _state.TurnNumber).ToArray())
        {
            _state.AssignmentWatches.Remove(watch);
            ResolveAssignmentWatch(watch, card, "joined an attacking army");
        }
    }

    private void ResolveKachikoWatch(AssignmentWatch watch, CardInstance card, string what)
    {
        Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
        GainHonor(card.OwnerId, -KachikoAbilities.HonorLoss, watch.Source);
    }

    private bool? RunKachiko(RunHookContext c)
    {
        if (c.id != KachikoAbilities.Watch) return null;
        _state.AssignmentWatches.Add(new(c.target!.Id, KachikoAbilities.WatchKind, c.player.Id, _state.TurnNumber, Definition(c.source!).Name));
        Emit("watchSet", $"{Definition(c.source!).Name}: after {Definition(c.target).Name} next attacks this turn, his controller loses {KachikoAbilities.HonorLoss} Honor.", c.player.Id, c.target.Id);
        return false;
    }

    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    internal static void RegisterKachikoHooks()
    {
        EngineHooks.Abilities.Register("KachikoAbilities", 15700, static abilities =>
        {
            abilities[KachikoAbilities.Watch] = new("Open", "Political Open, bow this card: After the next time this turn a target Personality attacks, his controller loses 2 Honor", KachikoAbilities.Target,
                ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
            return abilities;
        });
        EngineHooks.TargetAllows.Register("KachikoTargetAllows", 15700, static (engine, context) => engine.KachikoTargetAllows(context));
        EngineHooks.PersonalityForce.Register("KachikoPersonalityForce", 15700, static (engine, context) => engine.KachikoForce(context.card));
        EngineHooks.FollowerForce.Register("KachikoFollowerForce", 15700, static (engine, context) => engine.KachikoForce(context.card));
        EngineHooks.RunEffects.Register("KachikoRunEffects", 15700, static (engine, context) => engine.RunKachiko(context));
    }
}
