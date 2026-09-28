namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 4 (personality-wave-71): the helpers behind the batch's Personalities. The declarative data is in CardAbilities.cs (each entry carries the
// card's printed text and the reading it takes). The small primitives built here, each documented at its own use:
//   * AN ALTERNATE GOLD SOURCE WHOSE AMOUNT IS A DISCARDED CARD'S FOCUS VALUE (Asahina Keigo): the payer names the hand card each such source discards (GameCommand.PaymentDiscards);
//   * A ONE-SHOT DESTRUCTION BARRIER THAT MOVES THE PERSONALITY HOME (Asahina Michiru): CardInstance.BarrierTurn, read by DestructionShielded;
//   * THE TARGET'S CONTROLLER MAY DISHONOR HIM, ELSE AN ABILITY LOCK UNTIL THE ACTING PLAYER'S NEXT TURN BEGINS (Kakita Genshi): an Either/Or that chains into a listed effect choice, the lock
//     carrying its own expiry (DisabledAbility.UntilPlayerId);
//   * A PERMANENT-WHILE-BOWED HOLD (Bayushi Rentatsu): CardInstance.RentatsuHeld -- the straightening becomes optional (the declineStraighten choice) and its controller's turn begins with a
//     choice of a player who loses 2 Honor;
//   * A TWICE-PER-TURN WAIVER OF THE COST OF BOWING A PERSONALITY (Soshi Yorimi), read where declared costs are planned and paid (AbilityCosts.cs);
//   * Political abilities as the only ones a copy may take (Asahina Kitiaru, CardCopy.cs).
public sealed partial class MatchEngine
{
    private const string Batch4DisableUntilEffect = "e4-disable-until:";
    private const string YorimiWaiverEffect = "yorimi-bow-waiver";
    private const string YorimiWaiverKey = "yorimi-bow-waiver";
    // "Twice per turn": the ordinary per-turn use limit, kept on Yorimi's own use stamps under a key of its own.
    private static readonly AbilityDefinition YorimiWaiverLimit = new("Open", "Twice per turn, ignore the cost of bowing Yorimi", "none", UsesPerTurn: 2, UsageKey: YorimiWaiverKey);

    // ---- validation ----

    private void ValidateEmperorBatch4(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Bayushi Waru: "If you control a Courtier or this is a Combat Segment" -- the Limited half is never in a Combat Segment, so it needs a Courtier of yours.
        if (id == "waru-bow-limited") Require(ControlsKeyword(player.Id, "Courtier"), "You must control a Courtier to take Waru's Limited action.");
        // Kakita Tadanobu: "discard the Imperial Favor" is a cost, so the Favor must be his controller's to discard (or a Favor token, Kyuden Otomo - exp; ValidateAbility asks every Favor discard).
    }

    // ---- target categories (the validation switch's arms) ----

    // Shosuro Tanzaki: "another Personality" -- any Personality but the source.
    private bool Batch4OtherPersonality(CardInstance target, CardInstance? source) => Definition(target).Type == CardType.Personality && source is not null && target.Id != source.Id;

    // Bayushi Nitoshi's Limited: "a different one of your Personalities" -- a Personality of the acting player's other than the one already chosen as the target.
    private bool Batch4VictimOtherThanTarget(CardInstance candidate, GameCommand command, int playerId) =>
        Definition(candidate).Type == CardType.Personality && candidate.OwnerId == playerId && (SlotCards(command, "target").FirstOrDefault() is not { } chosen || chosen.Id != candidate.Id);

    // Bayushi Nitoshi's Battle: "a dishonorable Personality or a card without attachments in a unit" -- a card in a unit is a Personality or any card attached to him (issue #498),
    // and only one with no attachments counts for the second half (a Personality with a Follower, Item or Spell is a legal target only while dishonorable).
    private bool Batch4DishonorableOrUnattachedUnitCard(CardInstance target)
    {
        // Issue #498 (Makito's reading): the unit is everything attached to the Personality, so an Item or a Spell without attachments counts as well as a Follower.
        return Definition(target).Type == CardType.Personality && target.Dishonorable || IsUnitCardWithoutAttachments(target);
    }

    // ---- effects ----

    // Returns true when the action paused (an Either/Or, a copy decision) and finishes from there; false when it is done and the caller finishes it as any action.
    private bool RunEmperorBatch4Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Asahina Kitiaru: copies one of the target's Political abilities that does not itself copy abilities, for the turn.
            case "kitiaru-copy":
                return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", bowedBefore, GrantDuration.Turn, "Political");
            // Asahina Michiru: the barrier is raised on the target for the turn.
            case "michiru-barrier":
                target!.BarrierTurn = _state.TurnNumber;
                Emit("barrierRaised", $"{Definition(source!).Name} raises a barrier of air around {Definition(target).Name}: the next destruction of him by another player's action this turn is negated and moves him home.", player.Id, target.Id);
                return false;
            // Kakita Genshi: the target's controller chooses (Either/Or); the tail is finished by FinishGenshi so the whole Iaijutsu action resolves as one.
            case "genshi-shame":
            {
                if (!OpenEitherOr(EitherOrSpecs.GenshiShame, source!, player.Id, target!.OwnerId, target, "action", bowedBefore)) return false;
                var pending = _state.PendingEitherOr!;
                pending.Command = command; pending.ConsumedGrant = consumedGrant; pending.PlayedFromDiscard = playedFromDiscard; pending.OpponentHonorBefore = opponentHonorBefore;
                pending.PreExistingGrants = preExistingGrants.ToList();
                return true;
            }
            case "seishi-rehonor": Rehonor(target!); return false;
            case "tadanobu-draw": Draw(player); Draw(player); return false;
            case "makoto-favor":
                TakeImperialFavor(player.Id);
                Emit("favorTaken", $"{player.Name} took the Imperial Favor ({Definition(source!).Name}).", player.Id, source!.Id);
                return false;
            // Bayushi Hurunayi: Ninja is taken off him until the turn ends (an ongoing effect with no stated duration).
            case "hurunayi-remove-ninja": RemoveKeywordsAndClans(source!, TemporaryRemovals.Turn, ["Ninja"], []); return false;
            case "mitsuo-additional-action": GrantAdditionalAction(player.Id, null); return false;
            // Bayushi Nitoshi's Limited: the destroyed Personality was the cost; the target is dishonored and his controller loses 3 Honor.
            case "nitoshi-frame":
                Dishonor(target!, player.Id);
                GainHonor(target!.OwnerId, -3, Definition(source!).Name, source);
                return false;
            case "nitoshi-destroy": DestroyTargetCard(target!, player.Id); return false;
            // Bayushi Rentatsu: the bow was the cost; from now on, while he stays bowed, his straightening is optional and each of his controller's turns begins with an Honor loss.
            case "rentatsu-hold":
                source!.RentatsuHeld = true;
                Emit("rentatsuHold", $"{Definition(source).Name} stays bowed: his straightening may be negated, and each of {player.Name}'s turns begins with a player losing 2 Honor, while he remains bowed.", player.Id, source.Id);
                return false;
            case "waru-bow-battle":
            case "waru-bow-limited":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Shosuro Kameyoi: the Honor loss is judged first, against the target as he stands (dishonorable, and his printed Personal Honor), then he is dishonored.
            case "kameyoi-shame":
                if (target!.Dishonorable) GainHonor(target.OwnerId, Definition(target).PersonalHonor >= 3 ? -2 : -1, Definition(source!).Name, source);
                Dishonor(target, player.Id);
                return false;
            case "tanzaki-boost": ApplyModifier(target!, source!.Id, id, "Force", 4); return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition batch 4 ability '{id}'.");
        }
    }

    // ---- Favor discard as a cost (Kakita Tadanobu) ----

    // (The discard itself is every Favor-discard action's shared DiscardImperialFavorCost, EmperorStrongholdBatch1.cs.)

    // ---- Michiru's barrier ----

    // DestructionShielded's first question: another player's action would destroy the Personality this turn while his barrier stands. The barrier is spent, the destruction is negated, and
    // he moves home (his unit with him; a Personality already home stays).
    private bool MichiruBarrierNegates(CardInstance card, int actingPlayerId)
    {
        if (card.BarrierTurn != _state.TurnNumber || actingPlayerId == card.OwnerId || Definition(card).Type != CardType.Personality) return false;
        card.BarrierTurn = -1;
        Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated by the barrier of air; he moves home.", card.OwnerId, card.Id);
        if (card.Zone == Zone.Battlefield)
        {
            ReturnHome(card, false);
            Emit("unitMovedHome", $"{Definition(card).Name}'s unit moved home.", card.OwnerId, card.Id);
        }
        return true;
    }

    // ---- Genshi's Either/Or, and the ability lock that follows a refusal ----

    // The end of Genshi's Either/Or: a refusal chains into the acting player's choice of one of the Personality's abilities (locked until his next turn begins); either way the action then
    // finishes through the ordinary tail (Iaijutsu traits, the bow reaction window, priority).
    private void FinishGenshi(PendingEitherOr p)
    {
        _state.PendingChoice = null;
        var command = p.Command ?? new GameCommand("useAbility", p.ActingPlayerId, _state.Version, CardId: p.SourceCardId, TargetId: p.AffectedCardId, AbilityId: "genshi-shame");
        var bowedBefore = (p.BowedBefore ?? []).ToHashSet(StringComparer.Ordinal);
        if (p.ChainAbilityLock && p.AffectedCardId is { } targetId && _state.Cards.TryGetValue(targetId, out var target) && InPlay(target))
        {
            var options = AbilityLockOptions(target).Select(o => o with { Effects = [Batch4DisableUntilEffect + o.Id] }).ToArray();
            var prompt = $"{Definition(Card(p.SourceCardId)).Name}: choose one of {Definition(target).Name}'s abilities; it may not be used until your next turn begins.";
            if (BeginListedEffectChoice(command, options, prompt, p.ConsumedGrant, p.PlayedFromDiscard, p.OpponentHonorBefore, bowedBefore, p.PreExistingGrants)) return;
        }
        CompleteAbilityResolution(command, AbilityCatalog.All[command.AbilityId!], p.ConsumedGrant, p.PlayedFromDiscard, p.OpponentHonorBefore, bowedBefore, p.PreExistingGrants);
    }

    // ApplyBranchEffect's arm for this batch's listed options; false for any other effect.
    private bool ApplyEmperorBatch4BranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(Batch4DisableUntilEffect, StringComparison.Ordinal)) return false;
        if (command.TargetId is { } targetId && _state.Cards.TryGetValue(targetId, out var target))
            DisableAbilityUntilNextTurn(target, effect[Batch4DisableUntilEffect.Length..], Definition(Card(command.CardId)).Name, command.PlayerId);
        return true;
    }

    // "Until your next turn begins": the acting player's own TurnsTaken counts only his turns, and it advances as each begins, so the lock ends when it reaches this value.
    private void DisableAbilityUntilNextTurn(CardInstance card, string usageKey, string source, int untilPlayerId)
    {
        _state.DisabledAbilities.RemoveAll(d => !DisabledAbilityHolds(d));
        _state.DisabledAbilities.Add(new(card.Id, usageKey, _state.TurnNumber, source, untilPlayerId, Player(untilPlayerId).TurnsTaken + 1));
        Emit("abilityDisabled", $"{source}: one of {Definition(card).Name}'s abilities may not be used until {Player(untilPlayerId).Name}'s next turn begins.", card.OwnerId, card.Id);
    }

    private bool DisabledAbilityHolds(DisabledAbility d) => d.UntilPlayerId is int until ? Player(until).TurnsTaken < d.UntilTurnsTaken : d.Turn == _state.TurnNumber;

    // ---- Keigo: Gold equal to the Focus Value of a discarded card ----

    // The discards the payer has named for the payment now being validated or made (source id -> hand card id).
    private Dictionary<string, string> _paymentDiscards = [];

    private void UsePaymentDiscards(GameCommand command) => _paymentDiscards = command.PaymentDiscards ?? [];

    // What a variable source produces right now: the Focus Value of the hand card named for it, or 0 when none is named or the named one cannot be discarded.
    private int PaymentDiscardFocus(CardInstance source) =>
        _paymentDiscards.TryGetValue(source.Id, out var discardId) && _state.Cards.TryGetValue(discardId, out var discard) && discard.OwnerId == source.OwnerId && discard.Zone == Zone.Hand
            ? Definition(discard).Focus : 0;

    // A source whose amount is not fixed (Keigo): never suggested (the engine cannot know what the payer would discard), only accepted when the command names the discard.
    private bool IsVariableGoldSource(CardInstance card) =>
        Definition(card).GoldProduction == 0 && AlternateGoldSource(card) is { } alternate && AbilityCatalog.All[alternate.AbilityId].PaymentDiscardsFocus;

    private void RequirePaymentDiscards(int playerId, string[] sourceIds, PaymentSubject? paying)
    {
        var named = new List<string>();
        foreach (var id in sourceIds.Where(id => IsVariableGoldSource(Card(id))))
        {
            Require(_paymentDiscards.TryGetValue(id, out var discardId) && _state.Cards.TryGetValue(discardId, out var discard) && discard.OwnerId == playerId && discard.Zone == Zone.Hand
                && Player(playerId).Hand.Contains(discardId), $"Name a card in your hand for {Definition(Card(id)).Name} to discard.");
            var discardName = _paymentDiscards[id];
            Require(paying?.Card.Id != discardName, "The card being paid for cannot also be discarded to produce its Gold.");
            Require(!named.Contains(discardName), "A card can be discarded only once.");
            named.Add(discardName);
        }
    }

    // Choosing such a source is what bows it (Pay's ordinary bow), spends its use and discards the named card.
    private void DiscardForVariableGoldSource(CardInstance source, int playerId)
    {
        if (!_paymentDiscards.TryGetValue(source.Id, out var discardId) || !_state.Cards.TryGetValue(discardId, out var discard) || discard.Zone != Zone.Hand) return;
        Emit("goldSourceDiscard", $"{Definition(source).Name} discards {Definition(discard).Name} (Focus Value {Definition(discard).Focus}).", playerId, discard.Id);
        DiscardFate(Player(discard.OwnerId), discard);
    }

    // ---- Soshi Yorimi: "Twice per turn, you may ignore the cost of bowing Yorimi, if it is the cost of a Political action or an action on a Spell" ----

    private bool IsYorimi(CardInstance? card) => card is not null && InPlay(card) && Definition(card).Effect == YorimiWaiverEffect;

    // Whether the cost's bow of this subject is one Yorimi may ignore right now: a Personality with the trait, the action Political or on a Spell, a use left this turn. A pure read (the
    // costs of one action are planned, validated and paid at separate points); the use itself is spent where the cost would have been paid.
    private bool YorimiWaivesBow(AbilityDefinition ability, ActionCost cost, CardInstance? source, CardInstance? subject)
    {
        if (cost.Verb != CostVerb.Bow || !IsYorimi(subject)) return false;
        if (!ActionCarriesKeyword(ability, source, "Political") && !(source is not null && Definition(source).Type == CardType.Spell)) return false;
        return !AbilityUseSpent(subject!.AbilityUses, YorimiWaiverLimit, YorimiWaiverKey);
    }

    private void SpendYorimiWaiver(CardInstance yorimi, string actionName)
    {
        SpendAbilityUse(yorimi.AbilityUses, YorimiWaiverLimit, YorimiWaiverKey);
        Emit("bowCostIgnored", $"{Definition(yorimi).Name}: the cost of bowing him is ignored for {actionName} (twice per turn).", yorimi.OwnerId, yorimi.Id);
    }

    // ---- Rentatsu: while he remains bowed ----

    private bool RentatsuHolds(CardInstance card) => card.RentatsuHeld && card.Bowed && InPlay(card);

    // The Straighten Phase's optional straightening (declineStraighten) is offered for a held Rentatsu too: "you may negate his straightening".
    private bool RentatsuMayStayBowed(CardInstance card) => RentatsuHolds(card) && card.Zone is Zone.Home or Zone.Stronghold or Zone.Attached && card.StraightenBlockedTurn != _state.TurnNumber;

    // The start of the turn: "after each of your turns begins, choose a player; he loses 2 Honor" for each Rentatsu still bowed. Opens the choice and returns true (the Straighten Phase
    // waits); false when there is none.
    private bool BeginRentatsuLoss(PlayerState player)
    {
        var held = _state.Cards.Values.Where(c => c.OwnerId == player.Id && RentatsuHolds(c)).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (held is null) return false;
        _state.PendingChoice = new("rentatsuLoss", player.Id, held.Id, $"{Definition(held).Name} is still bowed: choose a player who loses 2 Honor.");
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        return true;
    }

    private void ResolveRentatsuLoss(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseRentatsuLoss" && command.PlayerId == pending.PlayerId && command.TargetPlayerId is 0 or 1, "Choose the player who loses 2 Honor.");
        var rentatsu = Card(pending.CardId!);
        _state.PendingChoice = null;
        Emit("rentatsuLoss", $"{Player(command.TargetPlayerId!.Value).Name} was chosen to lose 2 Honor ({Definition(rentatsu).Name}).", command.PlayerId, rentatsu.Id);
        GainHonor(command.TargetPlayerId.Value, -2, Definition(rentatsu).Name, rentatsu);
        // The Honor loss can end the match; otherwise the turn's Straighten Phase goes on.
        if (_state.Phase == Phase.Finished) return;
        ContinueStraightenPhase(Player(pending.PlayerId));
    }

    private IEnumerable<ActionOption> RentatsuLossActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "rentatsuLoss" } pending || pending.PlayerId != playerId) yield break;
        foreach (var chosen in new[] { playerId, 1 - playerId })
            yield return new("chooseRentatsuLoss", chosen == playerId ? "You lose 2 Honor" : "Your opponent loses 2 Honor", true, null,
                new GameCommand("chooseRentatsuLoss", playerId, _state.Version, TargetPlayerId: chosen), pending.CardId);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorPersonalityBatch4_Rung4b3()
    {
        EngineHooks.ValidateEarly.Register("ValidateEmperorBatch4", 700, static (engine, context) => engine.ValidateEmperorBatch4(context.id, context.ability, context.command, context.player, context.source));
    }
}
