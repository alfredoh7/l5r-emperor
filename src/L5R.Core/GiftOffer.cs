namespace L5R.Core;

// Issue #821: the gift step of Naoharu's Gift, as a primitive. "Show a card in your hand and choose another player: You offer it as a gift. He may put it in his hand; if he did, ... Otherwise, ..."
//
// What is shared (Mendon, 2026-10-10: "Satoru uses the exact same mechanism of show a hand card to opponent and they may put it in their hand; the only difference is the outcome"):
//   * ShowGift      the card is shown to the other player (the public shown-cards record, never a card id in the log);
//   * the decision  the other player alone chooses to put it in his hand or to refuse it (the Either/Or decision: GiftOptions), judged against the same rules (The Scorpion Wall is Finished:
//                   a card may not be put into his hand, so only the refusal is legal and it is taken);
//   * TransferGift  an accepted gift moves from the giver's hand to his: his to play, and it returns to its owner's pile when it leaves his hand (OriginalOwnerId).
// What differs is the outcome: Naoharu's Gift (an Event, EmperorRemainingFEvents.cs) gives the giver Honor equal to the card's Focus Value, or has him discard it and draw cards; Ikoma Satoru draws two
// cards when it is taken and otherwise discards it and Melee Attacks (strength = its Focus Value) a Follower or a Personality without Followers in the other player's home.
public sealed partial class MatchEngine
{
    /// <summary>The gift was taken: it leaves the giver's hand for the other player's. False when it may not be put into his hand (nothing moves).</summary>
    private bool TransferGift(PlayerState giver, PlayerState taker, CardInstance gift)
    {
        if (P2CardIntoHandBlocked(taker.Id)) return false; // issue #503: The Scorpion Wall is Finished
        giver.Hand.Remove(gift.Id); taker.Hand.Add(gift.Id);
        gift.OriginalOwnerId ??= giver.Id; gift.OwnerId = taker.Id; // his to play; it returns to its owner's pile when it leaves his hand
        NoteCardIntoHand(taker.Id);
        return true;
    }

    /// <summary>Shows the gift to the other player and offers it: the other player decides (Either/Or <paramref name="specId"/>). True when a decision, or a further one it chained into, is still open.</summary>
    private bool BeginGift(string specId, CardInstance source, PlayerState giver, CardInstance gift, HashSet<string>? bowedBefore)
    {
        RecordShown(giver.Id, gift, source, "Hand", "gift");
        Emit("giftOffered", $"{Definition(source).Name}: {giver.Name} shows {Definition(gift).Name} and offers it as a gift.", giver.Id);
        var opened = OpenEitherOr(specId, source, giver.Id, 1 - giver.Id, gift, "action", bowedBefore);
        return opened || _state.PendingEitherOr is not null || _state.PendingAttack is not null; // a refusal that was the only legal answer already chained into the Attack's target
    }

    private EitherOrOption[] GiftOptions(PendingEitherOr p, CardInstance gift, PlayerState giver)
    {
        var name = Definition(gift).Name;
        var blocked = P2CardIntoHandBlocked(p.DeciderPlayerId);
        return
        [
            new("accept", $"Put {name} in your hand", p.SpecId == Wave821Abilities.SadahakoSpec ? $"{giver.Name} gains Honor equal to its Focus Value ({Definition(gift).Focus})." : $"{giver.Name} draws two cards.", EitherOrKind.Accept, 0, !blocked, blocked ? "A card may not be put into your hand." : null),
            new("refuse", "Refuse the gift", p.SpecId == Wave821Abilities.SadahakoSpec ? $"{giver.Name} discards {name} and draws three cards."
                : $"{giver.Name} discards {name} and makes a Melee Attack with strength {Definition(gift).Focus} on a Follower or a Personality without Followers in your home.", EitherOrKind.Avoid, 0, true, null)
        ];
    }

    // ---- Ikoma Satoru's outcome ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The cards a Melee Attack of Satoru's could name now: judged by the Attack's own target rules (RequireLegalAttackTarget), so a bar or an immunity removes a candidate.</summary>
    private List<CardInstance> Wave821MeleeTargets(PlayerState attacker, CardInstance source)
    {
        var ability = AbilityCatalog.All[Wave821Abilities.SatoruMelee];
        var found = new List<CardInstance>();
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId != attacker.Id && InPlay(c) && Definition(c).Type is CardType.Follower or CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var probe = new GameCommand("useAbility", attacker.Id, _state.Version, CardId: source.Id, AbilityId: Wave821Abilities.SatoruMelee, TargetId: card.Id);
            try { RequireLegalAttackTarget(Wave821Abilities.SatoruMelee, ability, probe, attacker, source, card); found.Add(card); }
            catch (RuleViolation) { }
        }
        return found;
    }

    private EitherOrOption[]? Wave821EitherOrOptions(PendingEitherOr p, CardInstance? affected, CardInstance source)
    {
        switch (p.SpecId)
        {
            case Wave821Abilities.GiftSpec or Wave821Abilities.SadahakoSpec when affected is not null:
                return GiftOptions(p, affected, Player(p.ActingPlayerId));
            case Wave821Abilities.MeleeTargetSpec:
                return Wave821MeleeTargets(Player(p.ActingPlayerId), source)
                    .Select(t => new EitherOrOption(t.Id, $"Attack {Definition(t).Name}", $"Melee Attack with strength {(affected is null ? 0 : Definition(affected).Focus)} on {Definition(t).Name}.", EitherOrKind.Choose, 0, true, null)).ToArray();
            default:
                return null;
        }
    }

    private bool Wave821ApplyEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider, CardInstance? affected)
    {
        if (p.SpecId is not (Wave821Abilities.GiftSpec or Wave821Abilities.SadahakoSpec or Wave821Abilities.MeleeTargetSpec) || affected is null) return false;
        var giver = Player(p.ActingPlayerId);
        if (p.SpecId == Wave821Abilities.MeleeTargetSpec)
        {
            // The Attack, with its strength the discarded card's Focus Value (a card in a pile keeps its printed value).
            ResolveOrDeferAttack(Wave821Abilities.SatoruMelee, giver, option.Key, source.Id, false, Definition(affected).Focus);
            return true;
        }
        var sadahako = p.SpecId == Wave821Abilities.SadahakoSpec;
        if (option.Key == "accept")
        {
            var focus = Definition(affected).Focus; // read before the card changes hands
            if (!TransferGift(giver, decider, affected)) return true; // not reached: the option is not legal then
            if (sadahako)
            {
                Emit("giftTaken", $"{decider.Name} put the gift in their hand; {giver.Name} gains {focus} Honor ({Definition(source).Name}).", decider.Id);
                GainHonor(giver.Id, focus, Definition(source).Name, source);
                return true;
            }
            Emit("giftTaken", $"{decider.Name} put the gift in their hand; {giver.Name} draws two cards ({Definition(source).Name}).", decider.Id, affected.Id);
            Draw(giver); Draw(giver);
            return true;
        }
        if (sadahako)
        {
            DiscardFate(giver, affected);
            Emit("giftRefused", $"{decider.Name} refused the gift; {giver.Name} discards {Definition(affected).Name} and draws three cards ({Definition(source).Name}).", decider.Id, affected.Id);
            Draw(giver); Draw(giver); Draw(giver);
            return true;
        }
        DiscardFate(giver, affected);
        Emit("giftRefused", $"{decider.Name} refused the gift; {giver.Name} discards {Definition(affected).Name} ({Definition(source).Name}).", decider.Id, affected.Id);
        var targets = Wave821MeleeTargets(giver, source);
        if (targets.Count == 0)
        {
            Emit("effectFailed", $"{Definition(source).Name}: there is no Follower or Personality without Followers in {decider.Name}'s home to attack.", giver.Id, source.Id);
            return true;
        }
        OpenEitherOr(Wave821Abilities.MeleeTargetSpec, source, giver.Id, giver.Id, affected, "action", p.BowedBefore);
        return true;
    }

    private static void RegisterWave821GiftHooks()
    {
        EngineHooks.EitherOrPrompt.Register("Wave821EitherOrPrompt", 13821, static (engine, p, source, affected) => p.SpecId switch
        {
            Wave821Abilities.GiftSpec or Wave821Abilities.SadahakoSpec => $"{source}: {engine.Player(p.ActingPlayerId).Name} offers you {affected} as a gift. Put it in your hand?",
            Wave821Abilities.MeleeTargetSpec => $"{source}: choose the target of the Melee Attack (strength {(p.AffectedCardId is null ? 0 : Definition(engine.Card(p.AffectedCardId)).Focus)}).",
            _ => null
        });
        EngineHooks.EitherOrOptions.Register("Wave821EitherOrOptions", 13821, static (engine, p, affected, source, payable) => engine.Wave821EitherOrOptions(p, affected, source));
        EngineHooks.ApplyEitherOr.Register("Wave821ApplyEitherOr", 13821, static (engine, p, option, source, decider, affected, sourceIds) => engine.Wave821ApplyEitherOr(p, option, source, decider, affected));
        // A refusal whose Attack waits in its reaction window leaves that window to finish the action.
        EngineHooks.FinishEitherOr.Register("Wave821FinishEitherOr", 13821, static (engine, p) => p.SpecId == Wave821Abilities.MeleeTargetSpec && engine._state.PendingAttack is not null);
    }
}
