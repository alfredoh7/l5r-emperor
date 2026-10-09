namespace L5R.Core;

// Seeds of Decay pass (issue #453), batch 5: the helpers behind O-Yoroi Troops, Ronin Ambushers, Aka-beko, Blades of the Black Dragon, the
// Celestial Swords of the Mantis and the Scorpion (Experienced), Hato-gurama, Kama, Ki-Uso, Moksha Patam, Okesa-goma, Sandalwood Incense,
// Immolation, Master of Beasts, Nature's Wrath, Peace of the Kami, Scent of Dishonor, Extend the Soul's Boundaries and The Earth's Strength.
//
// Three small general primitives live here:
//   * AFTER-COMMAND TRAITS (AfterCommandTraitSnapshot / ApplyAfterCommandTraits): a trait printed "After this card bows" or "After this card
//     attaches" is mandatory and has no single choke point to hook (over a hundred call sites set Bowed = true; attachments enter play by half a
//     dozen paths). Execute snapshots every card's bowed state and host before the command and, once the command has run (its own reaction
//     windows included, since a trait resolves before any Reaction is played), applies the trait to every card whose state changed. Ronin
//     Ambushers' straighten-and-hold and Kama's dishonor are the first two; Aka-beko's purge of the penalties already on its new wielder rides the
//     same attach delta.
//   * "FROM OTHER PLAYERS' CARDS/ACTIONS" (EffectFromOtherPlayer): an applied modifier, token, lock or grant names its source; when that source is a
//     card it is another player's when its controller differs from the affected card's, and when it is a bare ability id the acting player of the
//     running command decides. Aka-beko (an ongoing negation of new Force/Chi penalties) and Peace of the Kami (a one-shot purge) both read it.
//   * A KEYWORD-CONDITIONAL "may perform this card's Battle action even if it is in your home" (AbilityDefinition.SourceUnitMayBeAwayIfWielder):
//     SourceUnitMayBeAway, granted only while the wielder has the printed keyword (Nature's Wrath: Earth; Peace of the Kami: Water).
public sealed partial class MatchEngine
{
    // ---- after-command traits ----

    /// <summary>Per card: was it bowed, and what was it attached to, before the command ran.</summary>
    private Dictionary<string, (bool Bowed, string? AttachedTo)> AfterCommandTraitSnapshot() =>
        _state.Cards.Values.ToDictionary(c => c.Id, c => (c.Bowed, c.AttachedTo), StringComparer.Ordinal);

    private void ApplyAfterCommandTraits(Dictionary<string, (bool Bowed, string? AttachedTo)> before)
    {
        foreach (var card in _state.Cards.Values.Where(InPlay).ToArray())
        {
            var effect = Definition(card).Effect;
            var was = before.TryGetValue(card.Id, out var b) ? b : (Bowed: false, AttachedTo: (string?)null);
            P3AfterCommandTrait(card, was.Bowed); // Vigilant Riders (issue #503): "After this card bows: Straighten it."
            ApplyRemainingGAfterCommandTraits(card, was.Bowed); // Blade of Perfection (issue #492 group G): "After this card bows: Straighten it."
            // Ronin Ambushers (oracle-l5r-10108): "After this card bows: Straighten it and, if you do not control a Terrain, negate its (future)
            // straightening until the phase ends." A straighten hold released when the phase changes (StraightenPoint.PhaseEnds) is the (future)
            // negation; a hold from an earlier bow this phase refuses the straighten itself, exactly as printed.
            if (effect == "ronin-ambushers-rebound" && card.Bowed && !was.Bowed)
            {
                Straighten(card);
                if (!ControlsKeyword(card.OwnerId, "Terrain")) AddStraightenHold(card, Definition(card).Name, null, StraightenPoint.PhaseEnds);
            }
            if (card.Zone != Zone.Attached || card.AttachedTo is null || card.AttachedTo == was.AttachedTo || !_state.Cards.TryGetValue(card.AttachedTo, out var wielder)) continue;
            // Kama (oracle-l5r-10116): "After this card attaches (or transfers): Dishonor this Personality unless he is Mantis Clan."
            if (effect == "kama-dishonor-unless-mantis" && !HasClanAffiliation(wielder, "Mantis"))
            {
                Dishonor(wielder);
                Emit("dishonored", $"{Definition(wielder).Name} is dishonored: he wields a Kama and is not Mantis Clan.", wielder.OwnerId, wielder.Id);
            }
            // Aka-beko (oracle-l5r-10110): the "current" half of "Negate all (current and new) Force and Chi penalties to this Personality from other
            // players' actions" -- the penalties already on the wielder from other players' cards are removed the moment it attaches; the "new" half is
            // AkaBekoNegates below, read by ApplyModifier and CreateToken.
            if (effect == "aka-beko-negate-penalties") PurgeOtherPlayersStatPenalties(wielder);
            // Gift Armor (issue #492 group G): the "current" half of "Negate all current and new Chi penalties".
            if (effect == GiftArmorEffect) PurgeChiPenalties(wielder);
        }
    }

    // "Until the phase ends" (StraightenPoint.PhaseEnds): the Attack-declaration-through-battle-resolution stretch is one phase (the War Weariness convention, SetPhase), so a
    // hold made in one battle is not released by the selection of the next.
    private static int PhaseFamily(Phase phase) => phase switch
    {
        Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle => 100,
        _ => (int)phase
    };

    private void PurgeOtherPlayersStatPenalties(CardInstance card)
    {
        var removed = card.Modifiers.RemoveAll(m => m.Stat is "Force" or "Chi" && m.Amount < 0 && EffectFromOtherPlayer(m.Source, card));
        foreach (var token in card.Tokens.Where(t => t.Stat is "Force" or "Chi" && t.Amount < 0 && EffectFromOtherPlayer(t.Source, card)).ToArray())
        { DestroyToken(card, token); removed++; }
        if (removed > 0) Emit("penaltiesNegated", $"{Definition(card).Name}: {removed} Force/Chi penalt{(removed == 1 ? "y" : "ies")} from other players' actions negated (Aka-beko).", card.OwnerId, card.Id);
    }

    // ---- "from other players' cards/actions" ----

    // The source of an applied effect (a card id, or a bare ability id) belongs to another player than `card`'s controller: a card by its controller,
    // an ability id by the acting player of the command being run.
    private bool EffectFromOtherPlayer(string source, CardInstance card)
    {
        if (_state.Cards.TryGetValue(source, out var sourceCard)) return sourceCard.OwnerId != card.OwnerId;
        return _runningCommand is { } command && command.PlayerId != card.OwnerId;
    }

    /// <summary>Aka-beko's "new" half: a Force or Chi penalty from another player's action is not applied to a Personality wielding it.</summary>
    private bool AkaBekoNegates(CardInstance card, string stat, int amount, string source) =>
        stat is "Force" or "Chi" && amount < 0 && Attachments(card.Id).Any(a => Definition(a).Effect == "aka-beko-negate-penalties") && EffectFromOtherPlayer(source, card);

    // Peace of the Kami (oracle-l5r-10130): "Negate all current effects on it from other players' cards" -- every modifier, token, granted keyword,
    // action lock and movement lock whose source is another player's card is removed. A card's dishonorable status and a timed straighten hold name no
    // source card, so neither is touched (see the batch report's readings).
    private void NegateOtherPlayersEffects(CardInstance card)
    {
        CleanseForceCaps(card, otherPlayersOnly: true); // issue #768 (ForceCaps.cs): a Force cap from another player's card is a current effect on it
        var removed = card.Modifiers.RemoveAll(m => EffectFromOtherPlayer(m.Source, card));
        foreach (var token in card.Tokens.Where(t => EffectFromOtherPlayer(t.Source, card)).ToArray()) { DestroyToken(card, token); removed++; }
        removed += card.GrantedKeywords.RemoveAll(g => EffectFromOtherPlayer(g.Source, card));
        removed += card.ActionLocks.RemoveAll(l => EffectFromOtherPlayer(l.Source, card));
        removed += card.MovementLocks.RemoveAll(l => EffectFromOtherPlayer(l.Source, card));
        Emit("effectsNegated", $"{Definition(card).Name}: every current effect from other players' cards is negated ({removed} removed).", card.OwnerId, card.Id);
    }

    // ---- Ki-Uso ----

    // Ki-Uso (oracle-l5r-10117): "Effects of Political actions this Personality performs will not be negated." A Protection (Takeshi's shape) scoped to
    // Political actions, read live from the attachment rather than granted, so it ends with the Item. Every "is this action protected?" question
    // asks this instead of Protected(performer) alone.
    private bool ActionProtected(GameCommand? command)
    {
        if (command?.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return false;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        // A rulebook Favor action (Lobby, favor-cycle...) is a player ability: no card performs it, so no card can protect it (found by the bot soak, issue #642).
        if (source is null && ability.Performer is null) return HiromiProtects(command);
        var performer = ActionPerformer(ability, command, source);
        return Protected(performer) || HiromiProtects(command) // Yoritomo Hiromi (issue #477)
            || Attachments(performer.Id).Any(a => Definition(a).Effect == "ki-uso-protect-political") && ActionCarriesKeyword(ability, source, "Political");
    }

    // ---- keyword-conditional "even if it is in your home" ----

    private bool SourceMayBeAway(AbilityDefinition ability, CardInstance? source) =>
        ability.SourceUnitMayBeAway
        || ability.SourceUnitMayBeAwayIfWielder is { } keyword && source is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out var host) && Keyword(host, keyword);

    // ---- Kama ----

    // The printed Kama (oracle-l5r-10116) prints no "Kama" keyword; its Effect marks it as the Weapon that raises its wielder's Weapon cap to two.
    private bool IsKama(CardInstance card) => Keyword(card, "Kama") || Definition(card).Effect == "kama-dishonor-unless-mantis";

    // ---- Celestial Swords ----

    // "This card has +1F for each <Clan> Personality you control" -- the wielder's controller's Personalities in play with that Clan alignment, the wielder included.
    private int ControlledClanPersonalities(int playerId, string clan) =>
        _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && HasClanAffiliation(c, clan));

    // Celestial Sword of the Scorpion - exp (oracle-l5r-10114): the conditional destroy after the optional province discard.
    private void ScorpionSwordResolve(GameCommand command, CardInstance target, bool discardFirst)
    {
        var discardedPersonality = false;
        if (discardFirst && CurrentBattlefieldProvince() is { CardId: { } cardId })
        {
            discardedPersonality = Definition(Card(cardId)).Type == CardType.Personality;
            DiscardCurrentProvinceCard();
        }
        if ((discardedPersonality || ControlsKeyword(command.PlayerId, "Courtier")) && InPlay(target)) DestroyTargetCard(target, command.PlayerId);
    }

    // ---- Moksha Patam ----

    // "After an action resolves, if a player gained Honor from its effects: Give this Personality a +1F token" -- every in-play Moksha Patam, whoever
    // controls it, read at the one point an action is truly done (FinishActionEffectTracking). The flag is set by GainHonor and reset when an action begins.
    private void ApplyMokshaPatamTokens()
    {
        if (!_state.CurrentActionHonorGained) return;
        _state.CurrentActionHonorGained = false;
        foreach (var item in _state.Cards.Values.Where(c => c.Zone == Zone.Attached && Definition(c).Effect == "moksha-patam-token").ToArray())
            if (item.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host)) CreateToken(host, "Moksha", item.Id, "Force", 1);
    }

    // "While this card is at the current battlefield, negate his movement to it": a movement lock that holds only while the Item stands at that battlefield.
    private bool MovementLockHolds(MovementLock lockRule)
    {
        if (lockRule.WhileCardAtBattlefield is null) return true;
        return _state.CurrentBattleProvince == lockRule.Battlefield && _state.Cards.TryGetValue(lockRule.WhileCardAtBattlefield, out var holder) && AtBattle(holder) && holder.ProvinceIndex == lockRule.Battlefield;
    }

    // ---- The Earth's Strength ----

    // The Personalities a deferred action would move: its Move spec's movers (the issue #336 shape) or, for the many abilities whose case moves their own
    // target home, that target (MoveOrStraightenKind "move").
    private IEnumerable<string> DeferredActionMovers(GameCommand command)
    {
        foreach (var id in ActionMovers(command)) yield return id;
        if (command.AbilityId is not null && MoveOrStraightenKind(command.AbilityId) == "move")
            foreach (var id in ReactionWindowTargets(command).OfType<string>()) yield return id;
    }
    private IEnumerable<string> DeferredActionDefendingMovers(GameCommand command) =>
        DeferredActionMovers(command).Distinct(StringComparer.Ordinal).Where(id => _state.Cards.TryGetValue(id, out var c) && Definition(c).Type == CardType.Personality && IsDefendingUnit(c));

    // ---- Extend the Soul's Boundaries ----

    // "Shuffle him and his attachments back into their owners' decks": each card goes on its owner's deck of its own kind, and every deck touched is shuffled.
    private void ShuffleUnitIntoOwnersDecks(CardInstance personality)
    {
        var touched = new HashSet<(int Owner, string Deck)>();
        foreach (var attachment in Attachments(personality.Id).ToArray())
        {
            ToOwnerDeck(attachment, "Top");
            touched.Add((attachment.OwnerId, SearchDeck.Fate));
        }
        ToOwnerDeck(personality, "Top");
        touched.Add((personality.OwnerId, Definition(personality).Type == CardType.Personality ? SearchDeck.Dynasty : SearchDeck.Fate));
        foreach (var (owner, deck) in touched) ShuffleDeck(Player(owner), deck);
    }

    // "This card enters play paying 1 less Gold for each Ring you control."
    private int RingEntryDiscount(CardDefinition def, int playerId) =>
        def.Effect == "discount-per-ring" ? _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Ring) : 0;
}
