namespace L5R.Core;

// Primitive P9 (issue #672; docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md, docs/decisions/2026-10-06-672-p9-stat-terms.md): static stat terms as data.
//
// A card's always-on "has +nF while ..." / "+nF for each ..." trait was a per-card branch in PersonalityForce, FollowerForce or a feature's Force callback. Here it is a record:
// a StatTermSet names the card Effect that carries it, and holds
//   * StatTerm          (Scope, Stat, Amount, AnyOf, PerCount): Amount (times PerCount's live count, when there is one) is added to the stat while any of the AnyOf
//                       conditions holds (no condition: always). Scope Self adds to the card's own Force or Chi; Scope Army adds to the total Force of the army the
//                       card is in, when the armies are totalled (ArmyForces);
//   * ContributionRule  (Multiplier, AnyOf): the Personality's own contribution to its army's total is multiplied while a condition holds ("contributes his Force twice").
// Terms are evaluated live, from the state as it is when the stat is asked: nothing is stored on the card, so nothing has to be swept when a condition stops holding.
//
// Sets are registered on EngineHooks.StatTerms by the feature that owns the cards (StatTermCards.cs is the first); the catalogue is built once, when first read.
// Three additive channels were added for the readers that had none: EngineHooks.FollowerForce (FollowerForce), EngineHooks.PersonalityChi (PersonalityChi) and
// EngineHooks.ArmyForce (ArmyForces). Personality Force terms ride the existing EngineHooks.PersonalityForce channel.
//
// Only the conditions and counters a wired card uses exist; the closed sets grow with the cards that need them. Not built in this pass (see the decision document):
// stat caps, Personal Honor terms, the unit/controller/all/opposing scopes, Battle and WhileCondition modifier durations, ComputedBonus.
//
// Chi: a Personality at 0 Chi is destroyed, and that rule is enforced where a Chi-changing effect is APPLIED (ApplyModifier, the detach paths). A live term is never
// "applied", so a term that could by itself take a Personality to 0 Chi would bypass the rule: the catalogue refuses a Chi term whose Amount is not positive. A positive
// per-count Chi term can still shrink when what it counts leaves (a Weapon is destroyed); every counter that exists counts attachments, whose removal already re-reads
// the host's Chi (DestroyAttachment, ReturnToHand, the detach paths), exactly as it must for an Item's printed Chi.

internal enum TermStat { Force, Chi }

internal enum TermScope
{
    /// <summary>The card that carries the term.</summary>
    Self,
    /// <summary>The total Force of the army the card is in, when the armies are totalled.</summary>
    Army,
}

internal enum TermConditionKind
{
    /// <summary>The card (its unit) is attacking.</summary>
    Attacking,
    /// <summary>An opposing Personality or Follower has lower Force than the card (its Force without this term).</summary>
    OpposingLowerForce,
    /// <summary>The card's controller controls a Personality with the keyword Arg.</summary>
    ControlsPersonalityWithKeyword,
    /// <summary>The card's controller has a dead Personality in his discard pile.</summary>
    DeadPersonalityInDiscard,
    /// <summary>The card is at a battlefield and the enemy leader controls a card with the keyword Arg.</summary>
    EnemyLeaderControlsKeyword,
    /// <summary>The card is at a battlefield and the enemy leader has the Imperial Favor.</summary>
    EnemyLeaderHasFavor,
    /// <summary>Another player is of the Clan Arg.</summary>
    OtherPlayerIsClan,
}

internal readonly record struct TermCondition(TermConditionKind Kind, string? Arg = null);

internal enum TermCounterKind
{
    /// <summary>The card's own attachments with the keyword Arg.</summary>
    AttachedWithKeyword,
    /// <summary>The tokens of type Arg on opposing cards (the cards of the enemy units at the card's battlefield).</summary>
    OpposingTokens,
}

internal readonly record struct TermCounter(TermCounterKind Kind, string Arg);

internal sealed record StatTerm(string Id, TermScope Scope, TermStat Stat, int Amount, TermCondition[]? AnyOf = null, TermCounter? PerCount = null);

internal sealed record ContributionRule(string Id, int Multiplier, TermCondition[]? AnyOf = null);

/// <summary>The terms and the contribution rule of one card Effect.</summary>
internal sealed record StatTermSet(string Effect, StatTerm[] Terms, ContributionRule? Contribution = null);

internal static partial class EngineHooks
{
    /// <summary>Added to a Follower's own Force (FollowerForce): 0 when the registrant has nothing for the card.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.FollowerForceContext, int>> FollowerForce = new();
    /// <summary>Added to a Personality's Chi (PersonalityChi): 0 when the registrant has nothing for the card. A registrant must never return a value that can by itself leave a Personality in play at 0 Chi.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.PersonalityChiContext, int>> PersonalityChi = new();
    /// <summary>Added to an army's total Force when the armies are totalled (ArmyForces), once for the Attacker's army and once for the Defender's.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.ArmyForceContext, int>> ArmyForce = new();
    /// <summary>The stat-term sets of a feature's cards (a catalogue channel, like Looks and Searches).</summary>
    internal static readonly OrderedHooks<Func<IEnumerable<StatTermSet>>> StatTerms = new();
}

internal static class StatTermCatalog
{
    private static readonly Lazy<Dictionary<string, StatTermSet>> Built = new(Build);

    /// <summary>Every registered set, by the card Effect that carries it.</summary>
    internal static IReadOnlyDictionary<string, StatTermSet> ByEffect => Built.Value;

    private static Dictionary<string, StatTermSet> Build()
    {
        var sets = EngineHooks.Catalogue([], EngineHooks.StatTerms, static set => set.Effect);
        ValidateSets(sets.Values);
        return sets;
    }

    /// <summary>The rules every registered set must meet; a set that breaks one fails the catalogue, and so the first engine that reads a stat.</summary>
    internal static void ValidateSets(IEnumerable<StatTermSet> sets)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            if (set.Effect is "" or "none") throw new InvalidOperationException("A stat-term set needs the card Effect that carries it.");
            foreach (var term in set.Terms) Validate(set, term, ids);
            if (set.Contribution is { } rule)
            {
                if (!ids.Add(rule.Id)) throw new InvalidOperationException($"Duplicate stat-term id: {rule.Id}");
                if (rule.Multiplier < 0) throw new InvalidOperationException($"{rule.Id}: a contribution multiplier is 0 or more.");
            }
        }
    }

    private static void Validate(StatTermSet set, StatTerm term, HashSet<string> ids)
    {
        if (!ids.Add(term.Id)) throw new InvalidOperationException($"Duplicate stat-term id: {term.Id}");
        // The Chi rule above: a live term is not an applied effect, so it must not be able to take a Personality to 0 Chi on its own.
        if (term.Stat == TermStat.Chi && term.Amount <= 0)
            throw new InvalidOperationException($"{term.Id} ({set.Effect}): a static Chi term must be positive; a Chi penalty has to be applied through ApplyModifier so that Chi 0 destroys.");
        if (term.Scope == TermScope.Army && term.Stat != TermStat.Force) throw new InvalidOperationException($"{term.Id} ({set.Effect}): an army term adds to the army's total Force.");
    }
}

public sealed partial class MatchEngine
{
    internal readonly record struct FollowerForceContext(CardInstance card);
    private int RegisteredFollowerForce(CardInstance card)
    {
        var context = new FollowerForceContext(card);
        var total = 0;
        foreach (var hook in EngineHooks.FollowerForce.Items)
            total += hook(this, context);
        return total;
    }

    internal readonly record struct PersonalityChiContext(CardInstance card);
    private int RegisteredPersonalityChi(CardInstance card)
    {
        var context = new PersonalityChiContext(card);
        var total = 0;
        foreach (var hook in EngineHooks.PersonalityChi.Items)
            total += hook(this, context);
        return total;
    }

    internal readonly record struct ArmyForceContext(int playerId, CardInstance[] army, CardInstance[] enemyArmy, bool defending);
    private int RegisteredArmyForce(int playerId, CardInstance[] army, CardInstance[] enemyArmy, bool defending)
    {
        var context = new ArmyForceContext(playerId, army, enemyArmy, defending);
        var total = 0;
        foreach (var hook in EngineHooks.ArmyForce.Items)
            total += hook(this, context);
        return total;
    }

    // The terms being evaluated right now, by (card, term). A term that reads Force (OpposingLowerForce) asks for the Force of the cards around it, and theirs may ask for
    // this card's in turn (two such cards opposing each other); a term that is already being evaluated counts as 0 inside its own evaluation. That also gives the term what
    // it compares with: the card's Force WITHOUT the term. Never part of the saved state: it is empty whenever no stat is being read.
    private readonly HashSet<(string CardId, string TermId)> _statTermsEvaluating = [];

    // A card's own terms for one stat. Traits work while their card is in play (core rules, Traits), so a card in a province, a hand or a discard pile has none.
    private int StatTermTotal(CardInstance card, TermStat stat)
    {
        if (!StatTermCatalog.ByEffect.TryGetValue(Definition(card).Effect, out var set) || !InPlay(card)) return 0;
        var total = 0;
        foreach (var term in set.Terms)
            if (term.Scope == TermScope.Self && term.Stat == stat) total += StatTermValue(card, term);
        return total;
    }

    // The army terms of the Personalities of one army (ArmyForces): a term counts while its card is in the army, bowed or not -- it is a trait of the card, not Force the
    // card contributes.
    private int StatTermArmyForce(CardInstance[] army)
    {
        var total = 0;
        foreach (var card in army)
        {
            if (!StatTermCatalog.ByEffect.TryGetValue(Definition(card).Effect, out var set)) continue;
            foreach (var term in set.Terms)
                if (term.Scope == TermScope.Army && term.Stat == TermStat.Force) total += StatTermValue(card, term);
        }
        return total;
    }

    private int StatTermValue(CardInstance card, StatTerm term)
    {
        var key = (card.Id, term.Id);
        if (!_statTermsEvaluating.Add(key)) return 0;
        try
        {
            if (!StatTermConditionsHold(card, term.AnyOf)) return 0;
            return term.PerCount is { } counter ? term.Amount * StatTermCount(card, counter) : term.Amount;
        }
        finally { _statTermsEvaluating.Remove(key); }
    }

    // "Contributes his Force twice" (ContributionRule): what the Personality contributes to his army's total, given what he would contribute by the ordinary rules. Called
    // where the armies are totalled (ArmyForces), not where a unit's Force is shown or compared, so the rule applies to the total alone.
    private int StatTermContribution(CardInstance card, int contribution) =>
        contribution != 0 && StatTermCatalog.ByEffect.TryGetValue(Definition(card).Effect, out var set) && set.Contribution is { } rule && StatTermConditionsHold(card, rule.AnyOf)
            ? contribution * rule.Multiplier
            : contribution;

    private bool StatTermConditionsHold(CardInstance card, TermCondition[]? anyOf)
    {
        if (anyOf is null || anyOf.Length == 0) return true;
        foreach (var condition in anyOf)
            if (StatTermConditionHolds(card, condition)) return true;
        return false;
    }

    // The Personality whose unit the card is in, when that unit is at a battlefield: the card itself, or an attachment's host.
    private CardInstance? StatTermUnitAtBattlefield(CardInstance card)
    {
        var unit = card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : card;
        return unit is { Zone: Zone.Battlefield, ProvinceIndex: not null } ? unit : null;
    }

    // "Opposing" (core rules Glossary): cards at the same battlefield on different sides -- the Personalities of the enemy army there and everything attached to them.
    private IEnumerable<CardInstance> StatTermOpposingCards(CardInstance card)
    {
        if (StatTermUnitAtBattlefield(card) is not { } unit) yield break;
        foreach (var enemy in Army(1 - unit.OwnerId, unit.ProvinceIndex!.Value).ToArray())
        {
            yield return enemy;
            foreach (var attachment in Attachments(enemy.Id).ToArray()) yield return attachment;
        }
    }

    private bool StatTermConditionHolds(CardInstance card, TermCondition condition)
    {
        switch (condition.Kind)
        {
            case TermConditionKind.Attacking:
                return StatTermUnitAtBattlefield(card) is { } attackingUnit && IsAttacking(attackingUnit);
            case TermConditionKind.OpposingLowerForce:
                {
                    // Read inside the term's own evaluation, so this is the card's Force without the term (see _statTermsEvaluating).
                    var own = CardForce(card);
                    foreach (var other in StatTermOpposingCards(card))
                        if (Definition(other).Type is CardType.Personality or CardType.Follower && CardForce(other) < own) return true;
                    return false;
                }
            case TermConditionKind.ControlsPersonalityWithKeyword:
                foreach (var other in _state.Cards.Values)
                    if (other.OwnerId == card.OwnerId && InPlay(other) && Definition(other).Type == CardType.Personality && Keyword(other, condition.Arg!)) return true;
                return false;
            case TermConditionKind.DeadPersonalityInDiscard:
                return DeadCount(card.OwnerId) > 0;
            // "Enemy" (core rules Glossary): the opposing player in a battle ("enemy leader") -- so there is one only while the card is at a battlefield.
            case TermConditionKind.EnemyLeaderControlsKeyword:
                return StatTermUnitAtBattlefield(card) is { } unit && ControlsKeyword(1 - unit.OwnerId, condition.Arg!);
            case TermConditionKind.EnemyLeaderHasFavor:
                return StatTermUnitAtBattlefield(card) is { } favorUnit && _state.FavorPlayerId == 1 - favorUnit.OwnerId;
            case TermConditionKind.OtherPlayerIsClan:
                foreach (var player in _state.Players)
                    if (player.Id != card.OwnerId && player.Clan == condition.Arg) return true;
                return false;
            default:
                throw new InvalidOperationException($"Unknown stat-term condition: {condition.Kind}");
        }
    }

    private int StatTermCount(CardInstance card, TermCounter counter)
    {
        var count = 0;
        switch (counter.Kind)
        {
            case TermCounterKind.AttachedWithKeyword:
                foreach (var attachment in Attachments(card.Id))
                    if (Keyword(attachment, counter.Arg)) count++;
                return count;
            case TermCounterKind.OpposingTokens:
                foreach (var other in StatTermOpposingCards(card)) count += TokenCount(other, counter.Arg);
                return count;
            default:
                throw new InvalidOperationException($"Unknown stat-term counter: {counter.Kind}");
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterStatTermHooks()
    {
        EngineHooks.PersonalityForce.Register("P9StatTermPersonalityForce", 13000, static (engine, context) => engine.StatTermTotal(context.card, TermStat.Force));
        EngineHooks.FollowerForce.Register("P9StatTermFollowerForce", 13000, static (engine, context) => engine.StatTermTotal(context.card, TermStat.Force));
        EngineHooks.PersonalityChi.Register("P9StatTermPersonalityChi", 13000, static (engine, context) => engine.StatTermTotal(context.card, TermStat.Chi));
        EngineHooks.ArmyForce.Register("P9StatTermArmyForce", 13000, static (engine, context) => engine.StatTermArmyForce(context.army));
    }
}
