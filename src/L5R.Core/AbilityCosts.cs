namespace L5R.Core;

// The non-Gold costs of an action, declared as data (issue #338, Mendon's rulings that a NEGATED action still pays all of its costs and only its
// effects are skipped, and that paying costs is ATOMIC). An ability's costs used to be statements at the top of its execution case
// ("source!.Bowed = true; ... effects"), which cannot be told apart from the effects that follow, so a whole-action negation
// (ActionNegation.cs) could pay only the costs the engine charged generically (Gold, the Imperial Favor discard, a CostOption).
// AbilityDefinition.Costs is the one place the rest are now written: each ActionCost is one verb applied to one card (or, for LoseHonor, to
// the acting player), PayAbilityCosts applies them all at one point -- in RunAbilityEffects just before the ability's effects, so the "newly bowed"
// snapshot Togashi Hizumi/Tamori Ruya read still counts a cost bow, exactly as when the bow was the case's first statement -- and
// ResolveNegatedAction applies the same list and nothing else. An ability whose cost is not declared here still pays it in its case, and so
// still does not pay it when negated; docs/decisions/2026-09-19-copy-and-either-or-primitives.md lists those ability ids.
//
// A cost that is conditional on something read at resolution, or that feeds a value the effect uses (Improved Defenses discards the top card of
// the Fate deck and adds its Focus Value), is deliberately NOT declared: a half-declared cost would be paid on negation but not its effect.
//
// ATOMIC (Mendon): before anything is paid, every cost the action would pay -- the chosen CostOption and each declared cost -- is asked
// "is this payment blocked by a negation?" (CostPaymentBlockedByNegation, the one predicate below: Hida Mochitoko's negation of a bow or a destruction
// of a card paid as a cost). If ANY is blocked the ENTIRE action is negated: nothing is paid (no Gold, no bow, no Honor loss) and no effect happens, though the
// action was still taken (its use is spent, a Strategy played from hand is discarded). If none is blocked, every cost is paid and then the effects
// apply -- or are skipped when the action's effects are negated. An Honor loss that is an EFFECT of the action (not a cost) is negated with the
// other effects; one that is a cost (CostVerbs.LoseHonor) is paid even when the effects are negated.

/// <summary>The verbs an ActionCost may apply. Bow and Destroy are CostVerb's (the alternative-cost verbs); these are the others a printed
/// cost uses.</summary>
public static class CostVerbs
{
    /// <summary>Discard the chosen card from its owner's hand ("Discard a card:").</summary>
    public const string Discard = "discard";
    /// <summary>Remove the subject from the game ("Remove this card from the game:", "remove a target dead Personality ...").</summary>
    public const string Remove = "remove";
    /// <summary>Destroy the subject as the acting player's own cost ("Destroy this Follower to ...", "destroy your performing Personality"),
    /// through the ordinary destruction path (a Personality with its unit, a Follower/Item/Spell alone).</summary>
    public const string Sacrifice = "sacrifice";
    /// <summary>Destroy one token of ActionCost.Token on the subject ("Destroy a Fire token on Torazu:").</summary>
    public const string DestroyToken = "destroy-token";
    /// <summary>Put the subject on the top or bottom of its owner's deck, as the command's DeckPosition says.</summary>
    public const string ToDeck = "to-deck";
    /// <summary>The acting player loses ActionCost.Amount Honor ("Lose 2 Honor:"). Paid like any cost, so a negation of the action's effects does
    /// not stop it.</summary>
    public const string LoseHonor = "lose-honor";
    /// <summary>Discard the subject from play into its owner's discard pile ("discard this card:" on a Celestial): a Dynasty card to the Dynasty discard pile, a Fate card to the Fate one.</summary>
    public const string DiscardFromPlay = "discard-from-play";
    /// <summary>The acting player's leftmost province that has not been destroyed is destroyed ("destroy your leftmost province:"). It names no card.</summary>
    public const string DestroyLeftmostProvince = "destroy-leftmost-province";
    /// <summary>Bow the subject and every unbowed card in its unit ("bow all unbowed cards in his unit", Assault in the Jungle, Forgotten Legacy pass issue #467).</summary>
    public const string BowUnit = "bow-unit";
    /// <summary>Give the subject -Amount Chi ("give this Personality -1C:", Cursed Relic, issue #467): payable only when he has that much Chi to lose (core rules,
    /// Pay Costs); a Personality it leaves at 0 Chi is destroyed by the ordinary Chi rule (ApplyModifier).</summary>
    public const string ChiPenalty = "chi-penalty";
    /// <summary>Shuffle the acting player's hand (the card played from it excepted) into his Fate deck (Face of Ninube, Emperor Edition remaining-cards pass, group A, issue #492); the count is what the action then draws.</summary>
    public const string ShuffleHand = "shuffle-hand";
}

/// <summary>One cost: <paramref name="Verb"/> (CostVerb.Bow or CostVerbs.*) applied to <paramref name="Subject"/> (CostSubject.*: the ability's
/// source, its chosen performer, or its chosen target). <paramref name="UnlessInHand"/>: the Ring's "bow this card OR discard it from your hand" --
/// nothing is paid when the source is played from the hand (the hand play is spent as any hand-played card is). <paramref name="Amount"/> is the
/// Honor of LoseHonor. <paramref name="UnlessKeyword"/>: "...unless he is Void" -- the cost is not paid when this Personality (the source, or the Personality an attached source
/// is attached to) has that keyword.</summary>
public sealed record ActionCost(string Verb, string Subject = CostSubject.Source, bool UnlessInHand = false, string? Token = null, int Amount = 0, string? UnlessKeyword = null)
{
    /// <summary>"Bow this Shugenja" (the wielder of the Spell the ability is on).</summary>
    public static ActionCost[] BowWielder { get; } = [new(CostVerb.Bow, CostSubject.Wielder)];
    /// <summary>"Bow this Shugenja unless he is [keyword]": nothing is paid, and nothing need be unbowed, when the wielder has the keyword (Control Your Destiny).</summary>
    public static ActionCost[] BowWielderUnless(string keyword) => [new(CostVerb.Bow, CostSubject.Wielder, UnlessKeyword: keyword)];
    /// <summary>"Bow this card" / "Bow Sakuma".</summary>
    public static ActionCost[] BowSource { get; } = [new(CostVerb.Bow)];
    /// <summary>"Bow your performing X".</summary>
    public static ActionCost[] BowPerformer { get; } = [new(CostVerb.Bow, CostSubject.Performer)];
    /// <summary>"Bow one of your Farms" (the chosen target).</summary>
    public static ActionCost[] BowTarget { get; } = [new(CostVerb.Bow, CostSubject.Target)];
    /// <summary>"Bow this card or discard it from your hand": the Ring shape.</summary>
    public static ActionCost[] BowSourceUnlessInHand { get; } = [new(CostVerb.Bow, CostSubject.Source, UnlessInHand: true)];
    /// <summary>"Discard a card" (the chosen hand card).</summary>
    public static ActionCost[] DiscardTarget { get; } = [new(CostVerbs.Discard, CostSubject.Target)];
    /// <summary>"Remove [the target] from the game".</summary>
    public static ActionCost[] RemoveTarget { get; } = [new(CostVerbs.Remove, CostSubject.Target)];
    /// <summary>"Remove this card from the game".</summary>
    public static ActionCost[] RemoveSource { get; } = [new(CostVerbs.Remove)];
    /// <summary>"Bow your Stronghold" (Bonds of Coin's Favor half).</summary>
    public static ActionCost[] BowStronghold { get; } = [new(CostVerb.Bow, CostSubject.Stronghold)];
    /// <summary>"Lose N Honor".</summary>
    public static ActionCost[] LoseHonor(int amount) => [new(CostVerbs.LoseHonor, Amount: amount)];
}

public sealed partial class MatchEngine
{
    /// <summary>Test seam for the "is this cost payment blocked by a negation?" question: a fake predicate stands in for the negation that will
    /// answer it (issue #338). Never set outside tests.</summary>
    internal Func<ActionCost, CardInstance?, bool>? CostBlockHook { get; set; }

    /// <summary>Test seam (issue #351): every announced action is resolved as a whole-action negation (ActionNegation.cs). Never set outside tests.</summary>
    internal bool NegateEveryAction { get; set; }

    // THE ONE PREDICATE (Mendon's atomic-cost ruling; agent R's Hida Mochitoko rule and issue #351's extension point, folded into a single function):
    // is paying any cost of this action blocked by a negation? True when a card the action would BOW or DESTROY as a cost has had that negated in the
    // open window -- Mochitoko's "negate its bowing and destruction" (MatchState.NegatedEffects: kind "bow" blocks a bow cost, "destroy" a destruction paid as
    // a cost, "all" either; a negated destruction alone never blocks a bow). The costs asked about are every one the action pays: a chosen alternative cost
    // (CostOptions, issue #337) and each declared Costs entry, so a Follower bowed as "bow this card" is as protected as one named as the performer. When it is
    // true the ENTIRE action is negated and nothing is paid, not even an Honor-loss cost (AbortActionForBlockedCost). Asked once, before any payment, by
    // ExecuteAbilityEffects (an action that is not negated) and by ResolveNegatedAction (one that is), so payment is atomic in both flows. This is the only place
    // a new "this cost is blocked" rule is added. `CostBlockHook` is the test seam: a fake predicate asked once per cost with the card its verb would be applied
    // to (null for LoseHonor).
    private bool CostPaymentBlockedByNegation(GameCommand command)
    {
        bool Negated((ActionCost Cost, CardInstance? Subject) c) =>
            NegatableKind(c.Cost.Verb) is { } kind && c.Subject is { } subject && InPlay(subject)
            && (_state.NegatedEffects.Any(n => n.CardId == subject.Id && (n.Kind == "all" || n.Kind == kind))
                // Fields of Mercy (issue #352): once its allowance for the Personality's controller is used up, a destruction is negated, as one paid as a cost is.
                // A pure read -- it never counts a destruction (the destruction itself, if it happens, does).
                || kind == "destroy" && Definition(subject).Type == CardType.Personality && FieldsOfMercyWouldNegatePersonalityDestruction(subject));
        return PlannedCostsOf(command).Any(c => CostBlockHook?.Invoke(c.Cost, c.Subject) == true || Negated(c));
    }

    // "...unless this Personality is [keyword]": "this Personality" is the source itself, or the Personality an attached source (a Spell, an Item) is attached to.
    // Forgotten Legacy pass (issue #467), batch 4: a cost whose subject is the PERFORMER ("destroy him unless he is Nonhuman", Pearls & Spirits) is exempt by
    // the performer's own keyword, not the source's -- the subject, when the caller has it, is who "he" is.
    private bool CostExempt(ActionCost cost, CardInstance? source, CardInstance? subject = null)
    {
        if (cost.UnlessKeyword is not { } keyword) return false;
        if (cost.Subject == CostSubject.Performer && subject is not null) return Keyword(subject, keyword);
        if (source is null) return false;
        var holder = source is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out var host) ? host : source;
        return Keyword(holder, keyword);
    }

    // A declared cost whose subject is the wielder ("Bow this Shugenja") needs one: the source is attached, and the wielder is unbowed unless he is exempt ("...unless he is Void").
    // The other subjects are already judged where they are chosen (the Performer category, the Target category, the bowed-source rule).
    private void ValidateWielderCosts(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        foreach (var cost in ability.Costs ?? [])
        {
            if (cost.Subject != CostSubject.Wielder) continue;
            var wielder = CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source);
            Require(wielder is not null && InPlay(wielder), "This card must be attached to a Personality.");
            if (cost.Verb == CostVerb.Bow && !CostExempt(cost, source) && !YorimiWaivesBow(ability, cost, source, wielder))
                Require(!wielder!.Bowed, $"{Definition(wielder).Name} is already bowed, so he cannot be bowed to pay this cost.");
            // Cursed Relic (issue #467): a Chi penalty is paid in full or not at all.
            if (cost.Verb == CostVerbs.ChiPenalty)
                Require(PersonalityChi(wielder!) >= cost.Amount, $"{Definition(wielder!).Name} does not have {cost.Amount} Chi to lose.");
        }
    }

    // The kind of negation ("bow" / "destroy", MatchState.NegatedEffects) that stops a cost verb; null for a verb no negation of an effect stops.
    private static string? NegatableKind(string verb) => verb switch
    {
        CostVerb.Bow or CostVerbs.BowUnit => "bow",
        CostVerb.Destroy or CostVerbs.Sacrifice => "destroy",
        _ => null
    };

    // The cards the action bows or destroys as a cost besides its Gold: none of them can also be bowed to produce that Gold (Venerable Master's "Bow and destroy this card, and pay 2 Gold").
    private IReadOnlySet<string>? CostPaidCards(GameCommand command)
    {
        var ids = PlannedCostsOf(command).Where(c => c.Cost.Verb is CostVerb.Bow or CostVerbs.Sacrifice && c.Subject is not null).Select(c => c.Subject!.Id).ToHashSet(StringComparer.Ordinal);
        return ids.Count == 0 ? null : ids;
    }

    // Every cost the action would pay (see PlannedCosts), listed safely: empty for a command that names no valid ability or option.
    private List<(ActionCost Cost, CardInstance? Subject)> PlannedCostsOf(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return [];
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        try { return PlannedCosts(ability, command, source).ToList(); }
        catch (RuleViolation) { return []; }
    }

    // The in-play cards the action would bow or destroy as its cost, and the kind of negation that would stop each: what a protective reaction (Hida
    // Mochitoko) is offered for, before the cost is paid.
    private IEnumerable<CardInstance> BowOrDestroyCostSubjects(GameCommand command) =>
        PlannedCostsOf(command).Where(c => NegatableKind(c.Cost.Verb) is not null && c.Subject is not null && InPlay(c.Subject)).Select(c => c.Subject!);

    // Every cost the action would pay besides Gold and the Imperial Favor (which nothing can block): the chosen CostOption (issue #337) first,
    // then the declared Costs, each with the card it applies to.
    private IEnumerable<(ActionCost Cost, CardInstance? Subject)> PlannedCosts(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is not null)
        {
            var option = ChosenCostOption(ability, command);
            yield return (new ActionCost(option.Verb, option.Subject), CostSubjectCard(option, ability, command, source));
        }
        foreach (var cost in ability.Costs ?? [])
        {
            if (cost.UnlessInHand && source is { Zone: Zone.Hand }) continue;
            var subject = cost.Verb is CostVerbs.LoseHonor or CostVerbs.DestroyLeftmostProvince or CostVerbs.ShuffleHand ? null : CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source);
            if (CostExempt(cost, source, subject) || YorimiWaivesBow(ability, cost, source, subject)) continue; // Soshi Yorimi (issue #477, batch 4): ignored, so not planned.
            yield return (cost, subject);
        }
    }

    // Applies every declared cost of the ability, in order. Nothing here re-validates (a reaction window between announcement and resolution may
    // have changed a subject; the cost is paid as far as it still can be, like PayCostOption). The caller has already asked CostPaymentBlockedByNegation.
    private void PayAbilityCosts(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (ability.Costs is null) return;
        foreach (var cost in ability.Costs)
        {
            if (cost.UnlessInHand && source is { Zone: Zone.Hand }) continue;
            if (cost.Verb == CostVerbs.LoseHonor) { GainHonor(player.Id, -cost.Amount, source is null ? "an action" : Definition(source).Name, source); continue; }
            if (cost.Verb == CostVerbs.ShuffleHand) { RemAShuffleHandIntoDeck(player, source); continue; }
            if (cost.Verb == CostVerbs.DestroyLeftmostProvince)
            {
                if (player.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Index).FirstOrDefault() is { } leftmost) DestroyProvince(player.Id, leftmost);
                continue;
            }
            var subject = CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source);
            if (subject is null) continue;
            if (CostExempt(cost, source, subject)) continue;
            if (YorimiWaivesBow(ability, cost, source, subject)) { SpendYorimiWaiver(subject, source is null ? "an action" : Definition(source).Name); continue; }
            switch (cost.Verb)
            {
                case CostVerb.Bow: subject.Bowed = true; break;
                case CostVerbs.Discard: DiscardFate(player, subject); break;
                // A Strategy still in its player's hand (Void Tattoo's "remove this card from the game", issue #476 batch 4) leaves the hand as well.
                case CostVerbs.Remove: Player(subject.OwnerId).Hand.Remove(subject.Id); RemoveFromGame(subject); break;
                case CostVerbs.Sacrifice:
                    if (subject.Zone == Zone.Attached) DestroyAttachment(subject); else DestroyUnits([subject], false, player.Id);
                    break;
                case CostVerbs.DiscardFromPlay:
                    subject.Zone = IsDynastyCardType(Definition(subject).Type) ? Zone.DynastyDiscard : Zone.FateDiscard;
                    subject.ProvinceIndex = null; subject.FaceUp = true;
                    Emit("discardedFromPlay", $"{Definition(subject).Name} was discarded.", subject.OwnerId, subject.Id);
                    break;
                case CostVerbs.DestroyToken: DestroyToken(subject, subject.Tokens.First(t => t.Type == cost.Token)); break;
                case CostVerbs.ToDeck: ToOwnerDeck(subject, command.DeckPosition!); break;
                case CostVerbs.BowUnit:
                    subject.Bowed = true;
                    foreach (var attached in Attachments(UnitHostId(subject)).Where(a => !a.Bowed)) attached.Bowed = true;
                    if (UnitHostId(subject) != subject.Id) Card(UnitHostId(subject)).Bowed = true;
                    break;
                case CostVerbs.ChiPenalty: ApplyModifier(subject, source?.Id ?? subject.Id, command.AbilityId!, "Chi", -cost.Amount); break;
                default: throw new InvalidOperationException($"Unknown cost verb '{cost.Verb}'.");
            }
        }
    }
}
