namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION MECHANICS, GROUP C (issue #501): Mendon's specification items 11-15, 20, 21 and 27 -- costs, entry costs and attack shapes.
//
//   * A SECOND TERRAIN (item 11, The Marshalling Fields): "After you announce a Terrain action: Once per battle, you may ignore any of its restrictions that are based on another Terrain being in
//     play." The one-Terrain-per-battlefield restriction (PlayStrategy, the listing, Unstable Ground's "if there is no Terrain in play") is waived once per battle for the Stronghold's controller
//     (MarshallingFieldsWaiverAvailable / SpendMarshallingFieldsWaiver); the second Terrain enters play as normal. Spent, it is spent for the battle: a destroyed Terrain frees no new slot.
//     "After the first time each turn one of your Terrain actions resolves: Draw a card." is a trait (no designator), so mandatory (FireMarshallingFieldsDraw).
//   * AN OPTIONAL COST THAT FEEDS THE EFFECT (items 12 and 14, Daigotsu Kazuko, Ikoma Shika - exp): the paid and unpaid ways are two abilities sharing one use; the card the paid way spends rides
//     on GameCommand.CardIds (Daigotsu Ishibashi's shown-card idiom), one offered action per candidate, and it is a declared cost (CostSubject.Chosen: a negated action still pays it).
//   * AN ADDITIONAL COST OF ENTERING PLAY (item 13, En'you): "As an additional cost of En'you entering play, either remove a card in your hand from the game or destroy one of your Sake tokens."
//     Paid at every entry of a Personality into play (Purchase, the Reserve recruit, EnterPlayFromRecruit's recruits, created cards); unpayable, the card cannot enter (QuotePurchase refuses
//     it). With more than one way to pay the player is asked: the command is set aside, the decision opened ("entryCostDecision", answered by "entryCostChoice"), and the command replayed with
//     the answer (Ten Thousand Temples' replay; the answers ride on MatchState.EntryCostAnswers so an Honor-loss replay keeps them). With one way it is paid without asking.
//   * "AFTER ANOTHER PLAYER'S ACTION TARGETS ONE OF YOUR PERSONALITIES" (item 15, Isawa Sakonoko): the common trigger is ReactionTriggers.TargetedByAction (TargetedActionReactions.cs), offered
//     in the before-action window and the Attack window; Sakonoko uses it with Hundred-Fold Cut's two-ways-to-pay shape (bow Sakonoko, or bow one of his Spells as the Reaction's target).
//   * A PHASE-DEPENDENT BOW COST (item 20, Ascending the Ranks): the Battle action does not bow the Paragon, the Open action does (two abilities sharing one use).
//   * MULTIPLE PERFORMERS (items 21 and 27): performing slots (TargetSlot.Performing). Fury of a Mob's condition reads UnitsOnSide, the per-side unit count of a battlefield (live, so always
//     current); Temple of the Seven Fortunes' two performers share no Clan alignment and the one chosen for the "bowed" slot is bowed (CostSubject.Slot).
// ======================================================================================================================================================================================
public static class EmperorMechCAbilities
{
    public const string MarshallingFieldsEffect = "marshalling-fields";
    public const string EnyouEntryCostEffect = "enyou-additional-entry-cost";
    public const string KazukoMelee = "kazuko-fear-melee", KazukoMeleeDestroy = "kazuko-fear-melee-destroy";
    public const string ShikaRanged = "shika-ranged", ShikaRangedDiscard = "shika-ranged-discard";
    public const string SakonokoNegate = "sakonoko-negate", SakonokoNegateSpell = "sakonoko-negate-spell";
    public const string AscendingBattle = "ascending-ranks-battle", AscendingOpen = "ascending-ranks-open";
    public const string FuryOfAMob = "fury-of-a-mob-destroy";
    public const string TempleOfSevenFortunes = "temple-seven-fortunes-honor";
    // Target categories.
    public const string LowerForceThanPerformer = "mechc-personality-lower-force-than-performer";
    public const string SakonokoSpell = "mechc-sakonoko-unbowed-spell";
    public const string NonhumanPerformer = "mechc-own-unbowed-nonhuman-at-battle";
    public const string TemplePerformer = "mechc-temple-performer", TemplePartner = "mechc-temple-partner";

    public static Dictionary<string, AbilityDefinition> AddEmperorMechC(this Dictionary<string, AbilityDefinition> all)
    {
        // Daigotsu Kazuko (oracle-l5r-1734): "Fear Battle: You may destroy one of Kazuko's Followers: Melee 3 Attack, or Melee 6 Attack if you destroyed a Follower." (Her "After Kazuko enters play:
        // Lose 1 Honor." is her EntryHonorLoss.) The two ways share one use; the Follower destroyed rides on CardIds (one offered action per Follower of hers).
        all[KazukoMelee] = new("Battle", "Fear Battle: Melee 3 Attack", "attack", UsageKey: "kazuko-fear", ActionKeywords: ["Fear"]);
        all[KazukoMeleeDestroy] = new("Battle", "Fear Battle: Destroy one of Kazuko's Followers: Melee 6 Attack", "attack", UsageKey: "kazuko-fear", ActionKeywords: ["Fear"],
            Costs: [new(CostVerbs.Sacrifice, CostSubject.Chosen)]);
        // Ikoma Shika - exp (oracle-l5r-3637): "Battle: You may discard a card: Ranged 4 Attack, with +2 strength if you discarded a card; if that card was a Terrain, draw a card."
        all[ShikaRanged] = new("Battle", "Ranged 4 Attack", "attack", UsageKey: "shika-ranged");
        all[ShikaRangedDiscard] = new("Battle", "Discard a card: Ranged 6 Attack (4, +2 as you discarded a card); if that card was a Terrain, draw a card", "attack", UsageKey: "shika-ranged",
            Costs: [new(CostVerbs.Discard, CostSubject.Chosen)]);
        // Isawa Sakonoko (oracle-l5r-3891): "Reaction: After another player's action targets one of your other Personalities at Sakonoko's location, bow Sakonoko or one of his Spells: Negate the
        // destruction of the Personality from the action's effects." The common targeted-by-action trigger; the two ways to pay share one use (Hundred-Fold Cut's shape).
        all[SakonokoNegate] = new("Reaction", "After another player's action targets one of your other Personalities at Sakonoko's location, bow Sakonoko: Negate the destruction of the Personality from the action's effects",
            "none", Trigger: ReactionTriggers.TargetedByAction, UsageKey: "sakonoko-negate", Costs: ActionCost.BowSource);
        all[SakonokoNegateSpell] = new("Reaction", "After another player's action targets one of your other Personalities at Sakonoko's location, bow one of his Spells: Negate the destruction of the Personality from the action's effects",
            SakonokoSpell, Trigger: ReactionTriggers.TargetedByAction, UsageKey: "sakonoko-negate", Costs: ActionCost.BowTarget);
        // Ascending the Ranks (oracle-l5r-599, the scan): "Battle/Open: Choose your performing Paragon, bow him unless this is the Attack Phase, and target a Personality with lower Force: Bow him.
        // Lose 3 Honor." Mendon (item 20): during the battle phase the Paragon is not bowed, during the Action Phase he is. One printed action, two catalog entries sharing its use.
        all[AscendingBattle] = new("Battle", "Choose your performing Paragon and target a Personality with lower Force: Bow him. Lose 3 Honor", LowerForceThanPerformer,
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"], UsageKey: "ascending-the-ranks");
        all[AscendingOpen] = new("Open", "Choose your performing Paragon, bow him (this is not the Attack Phase), and target a Personality with lower Force: Bow him. Lose 3 Honor", LowerForceThanPerformer,
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Paragon"], UsageKey: "ascending-the-ranks", Costs: ActionCost.BowPerformer);
        // Fury of a Mob (oracle-l5r-2738): "Battle: If your current army has more units than the current enemy army, bow your two performing Nonhumans and target an enemy unit: Destroy it."
        all[FuryOfAMob] = new("Battle", "If your current army has more units than the current enemy army, bow your two performing Nonhumans and target an enemy unit: Destroy it", "slots",
            Costs: [new(CostVerb.Bow, EmperorRemainingCAbilities.AllPerformingSlots)],
            Slots: [new("performers", NonhumanPerformer, "Your two performing unbowed Nonhumans at the current battlefield", 2, 2, Performing: true), new("target", "enemy-unit", "An enemy unit")]);
        // Temple of the Seven Fortunes (oracle-l5r-7890): "Open: If it is not your turn, choose your two performing unbowed Personalities with Clan alignments that do not share a Clan alignment, and
        // bow one of them: Gain 1 Honor. Give your provinces +2 strength." Two performing slots; the one in "bowed" is bowed as the cost.
        all[TempleOfSevenFortunes] = new("Open", "If it is not your turn, choose your two performing unbowed Personalities with Clan alignments that do not share a Clan alignment, and bow one of them: Gain 1 Honor. Give your provinces +2 strength",
            "slots", Costs: [new(CostVerb.Bow, CostSubject.Slot("bowed"))],
            Slots: [new("bowed", TemplePerformer, "Your performing unbowed Personality with a Clan alignment, to bow", 1, 1, Performing: true),
                new("other", TemplePartner, "Your other performing unbowed Personality, sharing no Clan alignment with the first", 1, 1, Performing: true)]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- hooks into the shared switches (CardAbilities.cs) ------------------------------------------------------------------------------------------------------------------------------

    private static int? MechCAttackStrength(string abilityId) => abilityId switch
    {
        EmperorMechCAbilities.KazukoMelee => 3,
        EmperorMechCAbilities.KazukoMeleeDestroy => 6,
        EmperorMechCAbilities.ShikaRanged => 4,
        EmperorMechCAbilities.ShikaRangedDiscard => 6,
        _ => null
    };

    private static string? MechCAttackKind(string abilityId) => abilityId switch
    {
        EmperorMechCAbilities.KazukoMelee or EmperorMechCAbilities.KazukoMeleeDestroy => "Melee",
        EmperorMechCAbilities.ShikaRanged or EmperorMechCAbilities.ShikaRangedDiscard => "Ranged",
        _ => null
    };

    private static bool MechCDestroysTarget(string abilityId) => abilityId == EmperorMechCAbilities.FuryOfAMob;
    private static bool MechCBowsTarget(string abilityId) => abilityId is EmperorMechCAbilities.AscendingBattle or EmperorMechCAbilities.AscendingOpen;

    // The abilities whose paid way names the card it spends on CardIds.
    private static bool MechCCarriesCostCard(string abilityId) => abilityId is EmperorMechCAbilities.KazukoMeleeDestroy or EmperorMechCAbilities.ShikaRangedDiscard;

    // The cards the paid way may spend, one offered action each (QueryActions); null for any other ability.
    private string?[]? MechCCostCardChoices(string abilityId, PlayerState player, CardInstance? source) => abilityId switch
    {
        EmperorMechCAbilities.KazukoMeleeDestroy => source is null ? [null] : KazukoFollowers(source).Select(c => (string?)c.Id).DefaultIfEmpty(null).ToArray(),
        EmperorMechCAbilities.ShikaRangedDiscard => player.Hand.Where(h => h != source?.Id).Select(h => (string?)h).DefaultIfEmpty(null).ToArray(),
        _ => null
    };

    private static string? MechCCostCardVerb(string abilityId) => abilityId switch
    {
        EmperorMechCAbilities.KazukoMeleeDestroy => "destroys",
        EmperorMechCAbilities.ShikaRangedDiscard => "discards",
        _ => null
    };

    private IEnumerable<CardInstance> KazukoFollowers(CardInstance kazuko) =>
        Attachments(kazuko.Id).Where(a => Definition(a).Type == CardType.Follower && InPlay(a)).OrderBy(a => a.Id, StringComparer.Ordinal);

    private CardInstance? ChosenCostCard(GameCommand command) =>
        command.CardIds is [var id] && _state.Cards.TryGetValue(id, out var card) ? card : null;

    // Target categories (ValidateAbility's default chain; slot candidates are judged through it too).
    private bool EmperorMechCTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        switch (category)
        {
            // Ascending the Ranks: "a Personality with lower Force" than the performing Paragon.
            case EmperorMechCAbilities.LowerForceThanPerformer:
                return personality && InPlay(target) && command.PerformerId is { } paragonId && _state.Cards.TryGetValue(paragonId, out var paragon) && CardForce(target) < CardForce(paragon);
            // Sakonoko: one of his own unbowed Spells.
            case EmperorMechCAbilities.SakonokoSpell:
                return source is not null && Definition(target).Type == CardType.Spell && target.Zone == Zone.Attached && target.AttachedTo == source.Id && !target.Bowed && target.OwnerId == player.Id;
            // Fury of a Mob: "your two performing Nonhumans" -- unbowed Nonhuman Personalities or Followers of yours at the current battlefield (they are bowed as the cost).
            case EmperorMechCAbilities.NonhumanPerformer:
                return target.OwnerId == player.Id && InPlay(target) && !target.Bowed && Keyword(target, "Nonhuman")
                    && (personality || Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached) && AtBattle(target);
            // Temple of the Seven Fortunes: an unbowed Personality of yours with a Clan alignment (the "bowed" slot); the partner (the "other" slot) is another one that shares no Clan alignment
            // with the card chosen for the "bowed" slot (the relation is symmetric, so checking it once for the pair is enough).
            case EmperorMechCAbilities.TemplePerformer:
                return personality && target.OwnerId == player.Id && InPlay(target) && !target.Bowed && ClanAlignments(target).Length > 0;
            case EmperorMechCAbilities.TemplePartner:
            {
                if (!personality || target.OwnerId != player.Id || !InPlay(target) || target.Bowed || ClanAlignments(target).Length == 0) return false;
                var alignments = ClanAlignments(target);
                return SlotCards(command, "bowed").All(first => first.Id != target.Id && !ClanAlignments(first).Intersect(alignments, StringComparer.Ordinal).Any());
            }
            default:
                return false;
        }
    }

    // The per-card conditions of taking the action (ValidateAbility; also run for each slot candidate).
    private void ValidateEmperorMechC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case EmperorMechCAbilities.KazukoMelee:
            case EmperorMechCAbilities.ShikaRanged:
                Require(command.CardIds is null or { Length: 0 }, "This way of taking the action spends no card.");
                break;
            // "You may destroy one of Kazuko's Followers": one Follower attached to her.
            case EmperorMechCAbilities.KazukoMeleeDestroy:
                Require(ChosenCostCard(command) is { } follower && source is not null && KazukoFollowers(source).Any(f => f.Id == follower.Id), "Choose one of Kazuko's Followers to destroy.");
                break;
            // "You may discard a card": a card in the acting player's hand.
            case EmperorMechCAbilities.ShikaRangedDiscard:
                Require(ChosenCostCard(command) is { } discard && discard.Zone == Zone.Hand && player.Hand.Contains(discard.Id) && discard.Id != source?.Id, "Choose a card in your hand to discard.");
                break;
            // Fury of a Mob: "If your current army has more units than the current enemy army".
            case EmperorMechCAbilities.FuryOfAMob:
                Require(_state.CurrentBattleProvince is int battlefield && UnitsOnSide(player.Id, battlefield) > UnitsOnSide(1 - player.Id, battlefield),
                    "Your current army must have more units than the current enemy army.");
                break;
            // Temple of the Seven Fortunes: "If it is not your turn".
            case EmperorMechCAbilities.TempleOfSevenFortunes:
                Require(_state.ActivePlayerId != player.Id, "Temple of the Seven Fortunes: only when it is not your turn.");
                break;
        }
    }

    /// <summary>The number of units on one side of a battlefield (Mendon's item 21: the per-side unit count metric). A unit is a Personality and everything attached to him, so this is the
    /// number of that player's Personalities there; it is read live, so it is always current as units arrive, leave or are destroyed.</summary>
    internal int UnitsOnSide(int playerId, int battlefield) => Army(playerId, battlefield).Count();

    // The execution switch's hook: null when the id is not this group's; false when its effects are done; true when an Attack's window opened and the action waits.
    private bool? RunEmperorMechC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // The Follower was destroyed as the cost (paid before the effects); the Attack's strength is the way's own.
            case EmperorMechCAbilities.KazukoMelee:
            case EmperorMechCAbilities.KazukoMeleeDestroy:
            case EmperorMechCAbilities.ShikaRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // The card was discarded as the cost; "if that card was a Terrain, draw a card" is read off the card discarded, and drawn as the action resolves, before the Attack's window.
            case EmperorMechCAbilities.ShikaRangedDiscard:
                if (ChosenCostCard(command) is { } discarded && Keyword(discarded, "Terrain"))
                {
                    Emit("shikaTerrainDraw", $"{Definition(source!).Name}: the discarded card was a Terrain -- draw a card.", player.Id, source!.Id);
                    Draw(player);
                }
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // Sakonoko: the cost (Sakonoko or his Spell) was bowed; the held action's destruction of the targeted Personality is negated.
            case EmperorMechCAbilities.SakonokoNegate:
            case EmperorMechCAbilities.SakonokoNegateSpell:
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            // Ascending the Ranks: bow him (a bow prevention stops it card by card), then the Honor loss, an effect of the action.
            case EmperorMechCAbilities.AscendingBattle:
            case EmperorMechCAbilities.AscendingOpen:
                if (target is not null && InPlay(target)) BowByEffect(target, player.Id);
                GainHonor(player.Id, -3, Definition(source!).Name, source);
                return false;
            // Fury of a Mob: the two Nonhumans were bowed as the cost; the enemy unit is destroyed.
            case EmperorMechCAbilities.FuryOfAMob:
                if (target is not null && InPlay(target)) DestroyTargetCard(target, player.Id);
                return false;
            // Temple of the Seven Fortunes: one performer was bowed as the cost; +1 Honor and +2 strength to each of the player's provinces (until the end of the turn: a province strength effect lasts
            // until the end of the turn unless the text says it is permanent, Mendon's R3 ruling).
            case EmperorMechCAbilities.TempleOfSevenFortunes:
            {
                var name = Definition(source!).Name;
                GainHonor(player.Id, 1, name, source);
                foreach (var province in player.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Index).ToArray()) GiveProvinceStrengthThisTurn(player.Id, province.Index, 2, name);
                return false;
            }
            default:
                return null;
        }
    }

    // ---- Isawa Sakonoko (the common targeted-by-action trigger) -------------------------------------------------------------------------------------------------------------------------

    // "After another player's action targets one of your other Personalities at Sakonoko's location" -- and the action could destroy him (an Attack always could, a destroying ability is on the
    // curated list): a Reaction is offered only when it could do something (the targeted-by-action convention). The Spell way needs an unbowed Spell of his.
    private bool MechCTargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        if (abilityId is not (EmperorMechCAbilities.SakonokoNegate or EmperorMechCAbilities.SakonokoNegateSpell)) return false;
        var applies = subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && affected.Id != card.Id && Definition(affected).Type == CardType.Personality
            && SameLocation(card, affected) && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!));
        return applies && (abilityId == EmperorMechCAbilities.SakonokoNegate || SakonokoSpells(card).Any());
    }

    private IEnumerable<CardInstance> SakonokoSpells(CardInstance sakonoko) =>
        Attachments(sakonoko.Id).Where(a => Definition(a).Type == CardType.Spell && !a.Bowed).OrderBy(a => a.Id, StringComparer.Ordinal);

    // One offered Reaction per Spell that may be bowed to pay.
    private IEnumerable<(string? TargetId, string? Label)>? MechCTargetedReactionChoices(CardInstance card, string abilityId) =>
        abilityId == EmperorMechCAbilities.SakonokoNegateSpell
            ? SakonokoSpells(card).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})")).ToArray()
            : null;

    // ---- The Marshalling Fields -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string MarshallingWaiverKey = "marshalling-fields-battle", MarshallingDrawKey = "marshalling-fields-draw";

    private CardInstance? MarshallingFieldsOf(int playerId) =>
        _state.Cards.TryGetValue(Player(playerId).StrongholdId, out var stronghold) && stronghold.Zone == Zone.Stronghold && Definition(stronghold).Effect == EmperorMechCAbilities.MarshallingFieldsEffect ? stronghold : null;

    /// <summary>True when the player's Marshalling Fields may still waive, this battle, a Terrain action's restriction based on another Terrain being in play.</summary>
    private bool MarshallingFieldsWaiverAvailable(int playerId) =>
        MarshallingFieldsOf(playerId) is { } fields && !(fields.AbilityUses.TryGetValue(MarshallingWaiverKey, out var battle) && battle == _state.BattleNumber);

    /// <summary>The waiver is used: its once-per-battle use is spent.</summary>
    private void SpendMarshallingFieldsWaiver(int playerId, CardInstance terrain)
    {
        var fields = MarshallingFieldsOf(playerId)!;
        fields.AbilityUses[MarshallingWaiverKey] = _state.BattleNumber;
        Emit("terrainRestrictionIgnored", $"{Definition(fields).Name}: {Player(playerId).Name} ignores the restriction of another Terrain being in play -- {Definition(terrain).Name} enters play beside it (once per battle).", playerId, fields.Id);
    }

    /// <summary>"After the first time each turn one of your Terrain actions resolves: Draw a card." A trait: mandatory, once per turn.</summary>
    private void FireMarshallingFieldsDraw(int playerId)
    {
        if (MarshallingFieldsOf(playerId) is not { } fields) return;
        if (fields.AbilityUses.TryGetValue(MarshallingDrawKey, out var turn) && turn == _state.TurnNumber) return;
        fields.AbilityUses[MarshallingDrawKey] = _state.TurnNumber;
        Emit("marshallingFieldsDraw", $"{Definition(fields).Name}: {Player(playerId).Name}'s first Terrain action this turn resolved -- draw a card.", playerId, fields.Id);
        Draw(Player(playerId));
    }

    // FireAfterOwnActionTraits' hook: an action that carries the Terrain keyword (inherited from its Terrain card, or printed) resolved.
    private void FireMechCAfterOwnActionTraits(AbilityDefinition ability, int actingPlayerId, CardInstance? source)
    {
        if (ActionCarriesKeyword(ability, source, "Terrain")) FireMarshallingFieldsDraw(actingPlayerId);
    }

    // ---- En'you: an additional cost of entering play ----------------------------------------------------------------------------------------------------------------------------------

    private sealed class EntryCostDecisionNeeded(int playerId, string cardId) : Exception("An additional cost of entering play awaits a decision.")
    {
        public int PlayerId { get; } = playerId; public string CardId { get; } = cardId;
    }

    // How many entry-cost decisions this run of a command has reached (the answers are MatchState.EntryCostAnswers, in the order they arise).
    private int _entryCostOccurrence;

    private static bool HasAdditionalEntryCost(CardInstance card) => Definition(card).Effect == EmperorMechCAbilities.EnyouEntryCostEffect;

    /// <summary>The ways the player could pay the card's additional cost of entering play now: "remove:&lt;hand card&gt;" (a card in his hand, removed from the game) or "sake:&lt;token id&gt;" (one of
    /// his Sake tokens, destroyed), each with its label.</summary>
    internal (string Key, string Label)[] AdditionalEntryCostOptions(int playerId, CardInstance card)
    {
        if (!HasAdditionalEntryCost(card)) return [];
        var player = Player(playerId);
        var hand = player.Hand.Where(h => h != card.Id && _state.Cards.ContainsKey(h)).Select(h => ($"remove:{h}", $"Remove {Definition(Card(h)).Name} ({h}) in your hand from the game"));
        var sake = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c)).OrderBy(c => c.Id, StringComparer.Ordinal)
            .SelectMany(c => c.Tokens.Where(t => t.Type == "Sake").Select(t => ($"sake:{t.Id}", $"Destroy a Sake token on {Definition(c).Name} ({c.Id})")));
        return hand.Concat(sake).ToArray();
    }

    // QuotePurchase's hook: an additional cost that cannot be paid bars the entry.
    private string? AdditionalEntryCostUnpayable(CardInstance card, int playerId) =>
        HasAdditionalEntryCost(card) && AdditionalEntryCostOptions(playerId, card).Length == 0
            ? $"{Definition(card).Name}'s additional cost of entering play cannot be paid: you have no card in your hand to remove from the game and no Sake token to destroy." : null;

    /// <summary>Pays the card's additional cost of entering play, as it enters. One way to pay is paid at once; with several the player is asked (the command is set aside and replayed with the
    /// answer); with none the entry is refused.</summary>
    private void PayAdditionalEntryCost(int playerId, CardInstance card)
    {
        if (!HasAdditionalEntryCost(card)) return;
        var options = AdditionalEntryCostOptions(playerId, card);
        Require(options.Length > 0, AdditionalEntryCostUnpayable(card, playerId) ?? "The additional cost of entering play cannot be paid.");
        string key;
        if (options.Length == 1) key = options[0].Key;
        else
        {
            var index = _entryCostOccurrence++;
            if (index >= _state.EntryCostAnswers.Count) throw new EntryCostDecisionNeeded(playerId, card.Id);
            key = _state.EntryCostAnswers[index];
            Require(options.Any(o => o.Key == key), "That way of paying the additional cost of entering play is no longer available.");
        }
        var player = Player(playerId);
        var name = Definition(card).Name;
        if (key.StartsWith("remove:", StringComparison.Ordinal))
        {
            var removed = Card(key["remove:".Length..]);
            player.Hand.Remove(removed.Id);
            RemoveFromGame(removed);
            Emit("entryCostPaid", $"{player.Name} removed {Definition(removed).Name} in their hand from the game: the additional cost of {name} entering play.", playerId, card.Id);
        }
        else
        {
            var tokenId = key["sake:".Length..];
            var holder = _state.Cards.Values.First(c => c.Tokens.Any(t => t.Id == tokenId));
            DestroyToken(holder, holder.Tokens.First(t => t.Id == tokenId));
            Emit("entryCostPaid", $"{player.Name} destroyed a Sake token on {Definition(holder).Name}: the additional cost of {name} entering play.", playerId, card.Id);
        }
    }

    // The decision's offered answers (QueryActions).
    private IEnumerable<ActionOption> EntryCostDecisionActions(int playerId)
    {
        if (_state.PendingEntryCostDecision is not { } pending || pending.PlayerId != playerId || !_state.Cards.TryGetValue(pending.CardId, out var card)) yield break;
        var name = Definition(card).Name;
        foreach (var (key, label) in pending.Options)
        {
            var candidate = key.StartsWith("remove:", StringComparison.Ordinal) ? key["remove:".Length..] :
                _state.Cards.Values.FirstOrDefault(c => c.OwnerId == playerId && InPlay(c) && c.Tokens.Any(t => t.Type == "Sake" && "sake:" + t.Id == key))?.Id;
            yield return new("entryCostChoice", $"{name} enters play: {label}", true, null,
                new GameCommand("entryCostChoice", playerId, _state.Version, CardId: card.Id, Choice: key), card.Id,
                DecisionCandidateIds: candidate is null ? [] : [candidate],
                DecisionLabel: key.StartsWith("remove:", StringComparison.Ordinal) ? "Remove this card from the game" : "Destroy this Sake token");
        }
    }

    // Execute's catch: the command reached an entry cost with more than one way to pay and no answer yet -- nothing it did is kept, the player is asked, and the command waits whole.
    private CommandResult SetAsideForEntryCost(EntryCostDecisionNeeded need, GameCommand command, GameCommand execute, MatchState pendingBase)
    {
        _paymentDiscards = [];
        _state = pendingBase;
        var card = Card(need.CardId);
        var options = AdditionalEntryCostOptions(need.PlayerId, card);
        pendingBase.PendingEntryCostDecision = new PendingEntryCostDecision
        {
            Command = execute, PlayerId = need.PlayerId, CardId = need.CardId, Options = options.Select(o => new EntryCostOption(o.Key, o.Label)).ToList(),
            PriorChoice = pendingBase.PendingChoice, PriorPriority = pendingBase.PriorityPlayerId, PriorPasses = pendingBase.ConsecutivePasses,
            HonorDecisions = _honorLossDecisions.ToList(), HonorWindowsDone = _honorWindowsDone
        };
        pendingBase.PendingChoice = new("entryCostDecision", need.PlayerId, need.CardId, $"{Definition(card).Name}: as an additional cost of entering play, remove a card in your hand from the game or destroy one of your Sake tokens.");
        pendingBase.PriorityPlayerId = need.PlayerId; pendingBase.ConsecutivePasses = 0;
        var decisionEvents = _state.Events.Count;
        Emit("entryCostDecision", $"{Player(need.PlayerId).Name} chooses how to pay {Definition(card).Name}'s additional cost of entering play.", need.PlayerId, need.CardId);
        _state.Version++;
        _commands.Add(Copy(command));
        _honorLossDecisions = [];
        return new(true, null, _state.Version, _state.Events.Skip(decisionEvents).ToArray());
    }

    // Execute's answer path: the command set aside is replayed from the state it started in, with this answer after the ones given so far. Null when the answer is accepted (the caller replays
    // PendingEntryCostDecision.Command); the rejection otherwise.
    private string? AnswerEntryCost(GameCommand command, MatchState original, out MatchState? cleanBase, out GameCommand? replay)
    {
        cleanBase = null; replay = null;
        if (original.PendingEntryCostDecision is not { } decision) return "No additional cost of entering play is being decided.";
        if (command.PlayerId != decision.PlayerId || decision.Options.All(o => o.Key != command.Choice)) return "Choose one of the offered ways to pay the additional cost of entering play.";
        cleanBase = Copy(original);
        cleanBase.PendingEntryCostDecision = null; cleanBase.PendingChoice = decision.PriorChoice; cleanBase.PriorityPlayerId = decision.PriorPriority; cleanBase.ConsecutivePasses = decision.PriorPasses;
        cleanBase.EntryCostAnswers.Add(command.Choice!);
        _state = Copy(cleanBase);
        _honorLossDecisions = decision.HonorDecisions.ToList();
        _honorWindowsDone = decision.HonorWindowsDone;
        replay = decision.Command;
        return null;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorMechCHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorMechC", 2100, static abilities => abilities.AddEmperorMechC());
        EngineHooks.AttackStrengthAfter.Register("MechCAttackStrength", 100, static (engine, context) => MechCAttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("MechCAttackKind", 100, static context => MechCAttackKind(context.abilityId));
        EngineHooks.TargetAllows.Register("EmperorMechCTargetAllows", 1100, static (engine, context) => engine.EmperorMechCTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.DestroysTarget.Register("MechCDestroysTarget", 300, static context => MechCDestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("MechCBowsTarget", 400, static context => MechCBowsTarget(context.abilityId));
        EngineHooks.TargetedReaction.Register("MechCTargetedReactionApplies", 500, static (engine, context) => engine.MechCTargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateEarly.Register("ValidateEmperorMechC", 1300, static (engine, context) => engine.ValidateEmperorMechC(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorMechC", 1900, static (engine, context) => engine.RunEmperorMechC(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant));
    }
}
