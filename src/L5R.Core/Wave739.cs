using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #739, wave batch 8: the zone, search and decision cards, wired on the merged P20 (search extensions: the other player chooses, a Celestial found anywhere, SearchDeck.Hand...), P21 (cards under
// a host, abilities taken from a province, play history, the province lock), P22 (overlay onto a Personality) and P25 (a multi-pick Either/Or and the cross-constrained target slot). Every legality and
// every effect is decided in Core. Ability ids are "w739-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-8-zones-search-and-decisions.md. Waves are those of
// tools/content/package_wave_739.py.
//
//   Asako Usohara            "Usohara's abilities will not be copied." / "Limited, bow: Search your Dynasty discard pile, then deck, for a Fudo Celestial. Put it into play." / "Limited: Put a Fudo Ring from your hand into play ..."   an ordered P20 search (CelestialIntoPlay); the Ring enters by its own text (WlRings.cs)
//   Ikoma Jeiku             "Invest 1: Refill the Province Jeiku entered play from face-up."                                                                 an Invest (an enter-play trait, as Ryo's refill)
//   Iuchi Wattu             "Limited: If Wattu is face-up in your Province, put him in your discard pile, honorably dead. If there are no other dead copies of Wattu there, refill the Province face-up."   FromProvince (P21)
//   Iweko Seiken            "will not be dishonored or gain a Clan Alignment" / "Interrupt: After you Recruit Seiken, Recruit a non-Unique Samurai with lower Gold Cost, ignoring costs." / "Political Open, bow: Take the Imperial Favor."   P11 NoDishonor; a P10 gain block on entry; an entry Reaction; the Favor
//   Kitsu Miwa              "Limited: If Miwa is face-up in your province, discard her and pay 2 Gold: Search your Dynasty deck for a non-Unique Lion Clan Shugenja. Refill this province with him, face-up."   FromProvince (P21); a province-discard cost; a P20 search ending in this file's refill
//   Yoritomo Mikaru         "Limited, *: Recruit a target Holding in your Province (entering play bowed), for 1 less Gold."                                  RecruitFromProvince (P21), the recruit price less 1
//   Disciples of Master Coin "Open: If this card did not enter play this turn, bow it and target a Holding with equal or lower Gold Cost: Straighten it."     EnteredPlayTurn (P21)
//   Blood of the Preserver - exp  "Maho Battle/Open, *: Target your dead Personality. Bring him into play if his Gold Cost is equal to or less than the amount paid. Lose 2 Honor. Destroy this Item."   a variable Gold payment
//   Paneki's Mask           "Limited: If this card is in your hand, target your dead Personality who was in play during your last turn: Bring him into play, ignoring costs and restrictions, and permanently give him Shadowlands * Undead. Attach this card to him (you must pay its cost). Lose 3 Honor."   InPlayDuringLastTurn (P21)
//   Poison the Cup          "This Shugenja has Shadowlands." / "Maho Limited, bow: Remove a target token from a Personality and give him a -1C Plague token. Lose 1 Honor."   a P10 keyword rule; a P25 multi-pick Either/Or over the target's tokens
//   Unbound Essence         "Limited: Bow this Shugenja, destroy this card, and target a Personality ...: Show cards from the top of the target's controller's Dynasty deck until you show a Personality ... overlay him on the target."   P22's overlay
//   Now We Are Enemies      "Battle: Target two Personalities in the same army that do not share a Clan alignment: Give each target -3F."                    P25's cross-constrained target slot
//   Hidden Weapons          "You may Equip Weapons from under this Holding as a Battle/Limited action." / "Limited: Put a Weapon in your hand under this Holding (face-down)."   cards under a host (P21)
//   Jade Pearl Inn          "Interrupt: After you Recruit this Holding from a Province, search your Dynasty deck for a non-Unique, Gold-producing Holding with a printed Gold Cost of 1 and refill the Province face-up with it." / "Bow: Produce 3 Gold."   a P20 search ending in this file's refill; GoldProduction 3
//   The Tachikaze           "After an action resolves whose effects destroyed your Follower with a Gold Cost of 1 or more: You may put it face-up under this card." / "Battle/Limited: Any number of times per turn, target your Personality: Attach a Follower from under this card to him, paying all costs."   cards under a host (P21)
//   Dark Audience           "Limited: Search your Fate deck for two attachments with different titles. Show them. Another player chooses one of them and removes it from the game. Put the other attachment in your hand."   a P20 search (a set of two, the other player chooses)
//   Kokure's Lands          "This province does not hold Dynasty cards." / "Limited, bow: Draw a card."                                                    the province lock (P21)
public static class Wave739Abilities
{
    public const string UsoharaSearch = "w739-usohara-search", UsoharaRing = "w739-usohara-ring", JeikuInvest = "w739-jeiku-invest", WattuDead = "w739-wattu-dead", SeikenRecruit = "w739-seiken-recruit",
        SeikenFavor = "w739-seiken-favor", MiwaSearch = "w739-miwa-search", MikaruRecruit = "w739-mikaru-recruit", DisciplesStraighten = "w739-disciples-straighten", BloodReanimate = "w739-blood-reanimate",
        MaskReanimate = "w739-mask-reanimate", PoisonCup = "w739-poison-cup", UnboundEssence = "w739-unbound-essence", NowEnemies = "w739-now-enemies", HiddenBattle = "w739-hidden-battle",
        HiddenLimited = "w739-hidden-limited", HiddenStore = "w739-hidden-store", InnSearch = "w739-inn-search", TachikazeCollect = "w739-tachikaze-collect", TachikazeBattle = "w739-tachikaze-battle",
        TachikazeLimited = "w739-tachikaze-limited", KokureDraw = "w739-kokure-draw";

    // The cards (oracle ids).
    public const string Usohara = "oracle-l5r-10493", Jeiku = "oracle-l5r-10639", Wattu = "oracle-l5r-10886", Seiken = "oracle-l5r-10881", Miwa = "oracle-l5r-10293", Mikaru = "oracle-l5r-10645",
        Disciples = "oracle-l5r-10335", Blood = "oracle-l5r-10521", Mask = "oracle-l5r-10342", Poison = "oracle-l5r-10702", Unbound = "oracle-l5r-10356", NowWeAreEnemies = "oracle-l5r-10386",
        HiddenWeapons = "oracle-l5r-10574", JadePearlInn = "oracle-l5r-10826", Tachikaze = "oracle-l5r-10270", DarkAudience = "oracle-l5r-10420", Kokure = "oracle-l5r-10331";

    // Catalogue Effects (a card names its own traits with one).
    public const string SeikenEffect = "w739-seiken", PoisonEffect = "w739-poison-cup", DarkAudienceEffect = "w739-dark-audience", KokureEffect = "w739-kokure";

    /// <summary>This file's search disposition: "refill this province with him, face-up". The card found replaces whatever refilled the province (a face-down refill goes back into the deck, which the search shuffles).</summary>
    public const string RefillProvince = "w739-refill-province";

    /// <summary>The cost verb of Miwa: she is discarded from her province.</summary>
    public const string ProvinceDiscardCost = "w739-province-discard";

    /// <summary>The Either/Or spec of Poison the Cup's token choice.</summary>
    public const string PoisonCupSpec = "w739-poison-cup-token";

    /// <summary>Jeiku's Invest has no "Reaction:" designator: a trait modelled with Reaction timing (Mendon's R4 ruling; TraitReactions.Is asks this).</summary>
    public static bool IsTrait(string abilityId) => abilityId == JeikuInvest;

    private static readonly SearchCriteria FudoCelestial = new("Fudo Celestial", [CardType.Celestial], ["Fudo"]);

    /// <summary>Usohara: "Search your Dynasty discard pile, then deck, for a Fudo Celestial. Put it into play." Ordered (R4): the first place that holds one.</summary>
    public static SearchSpec UsoharaSearchSpec { get; } = new("w739-usohara", "Search your Dynasty discard pile, then deck, for a Fudo Celestial",
        [new(SearchDeck.DynastyDiscard, FudoCelestial, SearchEffect.CelestialIntoPlay), new(SearchDeck.Dynasty, FudoCelestial, SearchEffect.CelestialIntoPlay)], Ordered: true);

    public static SearchSpec MiwaSearchSpec { get; } = new("w739-miwa", "Search your Dynasty deck for a non-Unique Lion Clan Shugenja",
        [new(SearchDeck.Dynasty, new("non-Unique Lion Clan Shugenja", [CardType.Personality], ["Shugenja", "Lion Clan"], NonUnique: true), RefillProvince)]);

    public static SearchSpec InnSearchSpec { get; } = new("w739-inn", "Search your Dynasty deck for a non-Unique, Gold-producing Holding with a printed Gold Cost of 1",
        [new(SearchDeck.Dynasty, new("non-Unique, Gold-producing Holding with a printed Gold Cost of 1", [CardType.Holding], NonUnique: true, GoldProducing: true, Comparisons: [new("GoldCost", "==", 1)]), RefillProvince)]);

    public static SearchSpec DarkAudienceSpec { get; } = new("w739-dark-audience", "Search your Fate deck for two attachments with different titles",
        [new(SearchDeck.Fate, new("attachment", [CardType.Follower, CardType.Item, CardType.Spell], DifferentTitles: true), SearchEffect.ToHand)],
        SelectCount: new(2, 2), OtherPlayerChooses: new(SearchEffect.RemoveFromGame));

    public static Dictionary<string, AbilityDefinition> AddWave739(this Dictionary<string, AbilityDefinition> all)
    {
        all[UsoharaSearch] = new("Limited", "Limited, bow this card: Search your Dynasty discard pile, then deck, for a Fudo Celestial. Put it into play", "none", Costs: ActionCost.BowSource, Search: UsoharaSearchSpec, NotCopyable: true);
        all[UsoharaRing] = new("Limited", "Limited: Put a Fudo Ring from your hand into play. This counts as playing it by its own text. Remove Usohara from the game", "own-ring-in-hand", NotCopyable: true);
        all[JeikuInvest] = new("Reaction", "Invest 1 Gold: Refill the Province Jeiku entered play from face-up", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(1));
        all[WattuDead] = new("Limited", "Limited: If Wattu is face-up in your Province, put him in your discard pile, honorably dead. If there are no other dead copies of Wattu there, refill the Province face-up", "none", FromProvince: true);
        all[SeikenRecruit] = new("Reaction", "Interrupt: After you Recruit Seiken, Recruit a non-Unique Samurai with lower Gold Cost, ignoring costs", "card-in-province", Trigger: "own-enters-play");
        all[SeikenFavor] = new("Open", "Political Open, bow this card: Take the Imperial Favor", "none", ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
        all[MiwaSearch] = new("Limited", "Limited: If Miwa is face-up in your province, discard her and pay 2 Gold: Search your Dynasty deck for a non-Unique Lion Clan Shugenja. Refill this province with him, face-up", "none",
            FromProvince: true, Cost: GoldCostRule.Fixed(2), Costs: [new(ProvinceDiscardCost)], Search: MiwaSearchSpec);
        all[MikaruRecruit] = new("Limited", "Limited: Recruit a target Holding in your Province (entering play bowed), for 1 less Gold", "card-in-province", Cost: GoldCostRule.TargetRecruit(-1, 0));
        all[DisciplesStraighten] = new("Open", "Open: If this card did not enter play this turn, bow it and target a Holding with equal or lower Gold Cost: Straighten it", "any-holding", ActionKeywords: ["Kolat"], Costs: ActionCost.BowSource);
        all[BloodReanimate] = new("BattleOrOpen", "Maho Battle/Open: Target your dead Personality. Bring him into play if his Gold Cost is equal to or less than the amount paid. Lose 2 Honor. Destroy this Item", "own-dead-personality",
            ActionKeywords: ["Maho"], Cost: GoldCostRule.Variable());
        all[MaskReanimate] = new("Limited", "Limited: If this card is in your hand, target your dead Personality who was in play during your last turn: Bring him into play, ignoring costs and restrictions, and permanently give him Shadowlands and Undead. Attach this card to him (you must pay its cost). Lose 3 Honor",
            "own-dead-personality", Cost: GoldCostRule.SourcePrinted());
        all[PoisonCup] = new("Limited", "Maho Limited, bow this card: Remove a target token from a Personality and give him a -1C Plague token. Lose 1 Honor", "personality", ActionKeywords: ["Maho"], Costs: ActionCost.BowSource);
        all[UnboundEssence] = new("Limited", "Limited: Bow this Shugenja, destroy this card, and target a Personality who must be non-Unique unless this Shugenja is Void: Show cards from the top of the target's controller's Dynasty deck until you show a Personality or you show all cards in the deck. If you showed a Personality, overlay him on the target",
            "personality", Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)]);
        all[NowEnemies] = new("Battle", "Battle: Target two Personalities in the same army that do not share a Clan alignment: Give each target -3F", "slots",
            Slots: [new("first", "personality", "The first Personality"), new TargetSlot("second", "personality", "The second Personality, in the same army and of no Clan alignment the first shares")
                { CrossConstraint = static (engine, cards) => engine.Wave739SameArmyNoSharedClan(cards), CrossConstraintReason = "The two Personalities must be in the same army and share no Clan alignment." }]);
        var weaponSlots = new TargetSlot[] { new("attachment", "attachment-in-discard", "A Weapon under this card"), new("personality", "own-personality", "Your Personality to equip it to") };
        var equipFromUnder = new AttachSpec(AttachSource.Target, AttachPile.UnderHost, AttachWielder.PersonalitySlot, [CardType.Item], ["Weapon"]);
        all[HiddenBattle] = new("Battle", "Battle: Equip a Weapon from under this Holding to your Personality", "slots", Slots: weaponSlots, Cost: GoldCostRule.AttachCost(), Attach: equipFromUnder, UsesPerTurn: AbilityUseLimit.Unlimited, UsageKey: "w739-hidden-equip");
        all[HiddenLimited] = new("Limited", "Limited: Equip a Weapon from under this Holding to your Personality", "slots", Slots: weaponSlots, Cost: GoldCostRule.AttachCost(), Attach: equipFromUnder, UsesPerTurn: AbilityUseLimit.Unlimited, UsageKey: "w739-hidden-equip");
        all[HiddenStore] = new("Limited", "Limited: Put a Weapon in your hand under this Holding (face-down)", "hand");
        all[InnSearch] = new("Reaction", "Interrupt: After you Recruit this Holding from a Province, search your Dynasty deck for a non-Unique, Gold-producing Holding with a printed Gold Cost of 1 and refill the Province face-up with it", "none",
            Trigger: "own-enters-play", Search: InnSearchSpec, Tireless: true);
        all[TachikazeCollect] = new("Reaction", "After an action resolves whose effects destroyed your Follower with a Gold Cost of 1 or more: You may put it face-up under this card", "own-card-in-discard", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        var followerSlots = new TargetSlot[] { new("attachment", "attachment-in-discard", "A Follower under this card"), new("personality", "own-personality", "Your Personality to attach it to") };
        var attachFromUnder = new AttachSpec(AttachSource.Target, AttachPile.UnderHost, AttachWielder.PersonalitySlot, [CardType.Follower]);
        all[TachikazeBattle] = new("Battle", "Battle: Any number of times per turn, target your Personality: Attach a Follower from under this card to him, paying all costs", "slots", Slots: followerSlots, Cost: GoldCostRule.AttachCost(), Attach: attachFromUnder,
            UsesPerTurn: AbilityUseLimit.Unlimited, UsageKey: "w739-tachikaze-attach");
        all[TachikazeLimited] = new("Limited", "Limited: Any number of times per turn, target your Personality: Attach a Follower from under this card to him, paying all costs", "slots", Slots: followerSlots, Cost: GoldCostRule.AttachCost(), Attach: attachFromUnder,
            UsesPerTurn: AbilityUseLimit.Unlimited, UsageKey: "w739-tachikaze-attach");
        all[KokureDraw] = new("Limited", "Limited, bow this card: Draw a card", "none", Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchState
{
    // 22950-22959 is reserved for batch 8 of the wirable-card waves (issue #739). Omitted from the saved game until used.
    /// <summary>The province a Miwa (discarded from it) or a Jade Pearl Inn (recruited from it) will refill with the card its search finds, by the source card's id.</summary>
    [JsonPropertyOrder(22950)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Wave739Refills { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Now We Are Enemies' cross-constraint: the cards chosen so far (one or two) are Personalities of the same side at the same battlefield and share no Clan alignment.</summary>
    internal bool Wave739SameArmyNoSharedClan(IReadOnlyList<CardInstance> cards)
    {
        if (cards.Count < 2) return true;
        var (first, second) = (cards[0], cards[1]);
        if (first.OwnerId != second.OwnerId || first.Id == second.Id || !SameLocation(first, second)) return false;
        return !ClanAlignments(first).Intersect(ClanAlignments(second), StringComparer.Ordinal).Any();
    }

    /// <summary>AbilitySourceZoneAllows' hook: Paneki's Mask is used from its player's hand. Null for every other ability.</summary>
    private static bool? Wave739SourceZoneAllows(AbilityDefinition ability, CardInstance source, PlayerState player) =>
        ReferenceEquals(ability, AbilityCatalog.All[Wave739Abilities.MaskReanimate]) ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id) : null;

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave739Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        var target = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var found) ? found : null;
        switch (id)
        {
            case Wave739Abilities.UsoharaRing:
                Require(RingMayEnterPlay(player), "Brotherhood Schism: you already put a Ring into play this turn.");
                if (target is not null) Require(Keyword(target, "Fudo"), "Choose a Fudo Ring in your hand.");
                break;
            case Wave739Abilities.JeikuInvest:
                Require(EnteredFromProvince(source!), "Jeiku did not enter play from a Province.");
                break;
            case Wave739Abilities.SeikenRecruit:
                Require(EnteredFromProvince(source!), "Seiken did not enter play from a Province: he was not recruited.");
                if (target is not null)
                    Require(target.OwnerId == player.Id && target.Zone == Zone.Province && target.FaceUp && Definition(target).Type == CardType.Personality && Keyword(target, "Samurai") && !Keyword(target, "Unique")
                        && CardGoldCost(target) < CardGoldCost(source!), "Choose a face-up, non-Unique Samurai in one of your Provinces with a lower Gold Cost than Seiken's.");
                break;
            case Wave739Abilities.MikaruRecruit:
                if (target is not null) Require(target.OwnerId == player.Id && target.Zone == Zone.Province && target.FaceUp && Definition(target).Type == CardType.Holding, "Choose a face-up Holding in one of your Provinces.");
                break;
            case Wave739Abilities.DisciplesStraighten:
                Require(source!.EnteredPlayTurn != _state.TurnNumber, "This card entered play this turn.");
                if (target is not null) Require(Definition(target).Type == CardType.Holding && CardGoldCost(target) <= CardGoldCost(source), "Choose a Holding with an equal or lower Gold Cost than this Follower's.");
                break;
            case Wave739Abilities.MaskReanimate:
                Require(source!.Zone == Zone.Hand && player.Hand.Contains(source.Id), "Paneki's Mask must be in your hand.");
                Require(!ControlsCopyOf(player.Id, Definition(source).Name), "You already control this Unique card.");
                if (target is not null) Require(WasInPlayDuringLastTurn(target), "Choose a dead Personality who was in play during your last turn.");
                break;
            case Wave739Abilities.PoisonCup:
                if (target is not null) Require(InPlay(target) && Definition(target).Type == CardType.Personality && target.Tokens.Count > 0, "Choose a Personality in play with a token.");
                break;
            case Wave739Abilities.UnboundEssence:
                if (target is not null && source is { Zone: Zone.Attached, AttachedTo: { } wielderId } && _state.Cards.TryGetValue(wielderId, out var shugenja))
                {
                    Require(InPlay(target) && Definition(target).Type == CardType.Personality, "Choose a Personality in play.");
                    Require(!Keyword(target, "Unique") || Keyword(shugenja, "Void"), "The target must be non-Unique unless this Shugenja is Void.");
                }
                break;
            case Wave739Abilities.HiddenStore:
                if (target is not null) Require(Definition(target).Type == CardType.Item && Keyword(target, "Weapon"), "Choose a Weapon in your hand.");
                break;
            case Wave739Abilities.TachikazeCollect:
                if (target is not null) Require(_state.LastResolvedAction is { } facts && Wave739DestroyedFollower(target, player.Id, facts), "Choose a Follower of yours with a Gold Cost of 1 or more that the action destroyed.");
                break;
        }
    }

    private bool Wave739DestroyedFollower(CardInstance card, int playerId, ResolvedActionFacts facts) =>
        card.OwnerId == playerId && card.Zone == Zone.FateDiscard && Definition(card).Type == CardType.Follower && Definition(card).GoldCost >= 1
        && facts.Affected.Any(a => a.CardId == card.Id && a.Kind == DestroyedKind);

    /// <summary>AfterActionResolvedApplies' hook: The Tachikaze's collect Reaction is offered when the action destroyed a Follower of its controller's with a Gold Cost of 1 or more. null: not this card's.</summary>
    private bool? Wave739AfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts) =>
        abilityId == Wave739Abilities.TachikazeCollect ? facts.Affected.Any(a => a.Kind == DestroyedKind && _state.Cards.TryGetValue(a.CardId, out var card) && Wave739DestroyedFollower(card, playerId, facts)) : null;

    // ---- effects ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave739(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave739Abilities.UsoharaSearch or Wave739Abilities.InnSearch or Wave739Abilities.MiwaSearch:
                if (id == Wave739Abilities.InnSearch) _state.Wave739Refills = new() { [source!.Id] = _state.LastProvinceEntryProvinceIndex!.Value };
                BeginSearch(c.ability.Search!, source!, player.Id, "ability", c.bowedBeforeAction);
                return true;
            case Wave739Abilities.UsoharaRing:
                {
                    // "Put a Fudo Ring from your hand into play. This counts as playing it by its own text." Every Fudo Ring is a False Ring, whose own text also says "lose 2 Honor".
                    player.Hand.Remove(target!.Id);
                    target.Zone = Zone.Home; target.ProvinceIndex = null; target.FaceUp = true;
                    RingEntersPlay(player, target, true, source, id);
                    Emit("cardEnteredPlay", $"{player.Name} put {Definition(target).Name} into play ({Definition(source!).Name}), by its own text.", player.Id, target.Id);
                    if (Definition(target).Name.StartsWith("False Ring", StringComparison.Ordinal)) GainHonor(player.Id, -2, Definition(target).Name, target);
                    // "Remove Usohara from the game."
                    RemoveUnitFromGame(source!, "Asako Usohara");
                    return false;
                }
            case Wave739Abilities.JeikuInvest:
                RefillProvinceFaceUp(player, _state.LastProvinceEntryProvinceIndex!.Value, source!);
                return false;
            case Wave739Abilities.WattuDead:
                Wave739WattuDies(player, source!);
                return false;
            case Wave739Abilities.SeikenRecruit:
                return Wave739RecruitIgnoringCosts(player, target!, Definition(source!).Name);
            case Wave739Abilities.SeikenFavor:
                TakeImperialFavor(player.Id);
                Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id);
                return false;
            case Wave739Abilities.MikaruRecruit:
                return RecruitFromProvince(player, target!, bowed: true);
            case Wave739Abilities.DisciplesStraighten:
                Straighten(target!);
                return false;
            case Wave739Abilities.BloodReanimate:
                {
                    // "Bring him into play if his Gold Cost is equal to or less than the amount paid. Lose 2 Honor. Destroy this Item." The Honor and the Item go whether or not he returned.
                    var paid = PaidGoldAmount(c.command);
                    void Finish() { GainHonor(player.Id, -2, Definition(source!).Name, source); DestroyAttachment(source!); }
                    if (CardGoldCost(target!) <= paid) return BringDeadPersonalityIntoPlay(player, target!, Definition(source!).Name, null, _ => Finish());
                    Emit("effectFailed", $"{Definition(source!).Name}: {Definition(target!).Name}'s Gold Cost {CardGoldCost(target!)} is more than the {paid} Gold paid, so he stays dead.", player.Id, target!.Id);
                    Finish();
                    return false;
                }
            case Wave739Abilities.MaskReanimate:
                return BringDeadPersonalityIntoPlay(player, target!, Definition(source!).Name, null, entered =>
                {
                    GrantKeyword(entered, "Shadowlands", source!.Id, GrantDuration.Permanent);
                    GrantKeyword(entered, "Undead", source.Id, GrantDuration.Permanent);
                    // "Attach this card to him (you must pay its cost)": the Gold was paid as the action's cost.
                    if (AttachmentLegalTarget(Definition(source), entered) && AttachmentAllowed(source, entered)) PlaceAttachment(source, entered, player);
                    else Emit("effectFailed", $"{Definition(source).Name} will not attach to {Definition(entered).Name} and stays in hand.", player.Id, source.Id);
                    GainHonor(player.Id, -3, Definition(source).Name, source);
                });
            case Wave739Abilities.PoisonCup:
                return OpenEitherOr(Wave739Abilities.PoisonCupSpec, source!, player.Id, player.Id, target, "action", c.bowedBeforeAction, min: 1, max: 1);
            case Wave739Abilities.UnboundEssence:
                {
                    // "Show cards from the top of the target's controller's Dynasty deck until you show a Personality or you show all cards in the deck. If you showed a Personality, overlay him on the target."
                    CardInstance? found = null;
                    foreach (var topId in Player(target!.OwnerId).DynastyDeck.ToArray())
                    {
                        var top = Card(topId);
                        RecordShown(player.Id, top, source!, SearchDeck.Dynasty, "look");
                        if (Definition(top).Type != CardType.Personality) continue;
                        found = top;
                        break;
                    }
                    if (found is not null) OverlayOnto(target, found, Definition(source!).Name);
                    else Emit("effectFailed", $"{Definition(source!).Name}: no Personality was found.", player.Id, source!.Id);
                    return false;
                }
            case Wave739Abilities.NowEnemies:
                foreach (var enemy in SlotCards(c.command, "first").Concat(SlotCards(c.command, "second")).ToArray()) ApplyModifier(enemy, source!.Id, id, "Force", -3);
                return false;
            case Wave739Abilities.HiddenStore:
                PutUnder(target!, source!, faceUp: false);
                return false;
            case Wave739Abilities.TachikazeCollect:
                PutUnder(target!, source!, faceUp: true);
                return false;
            case Wave739Abilities.KokureDraw:
                Draw(player);
                return false;
            default:
                return null;
        }
    }

    // ---- Wattu ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave739WattuDies(PlayerState player, CardInstance wattu)
    {
        // "If there are no other dead copies of Wattu there" -- in the discard pile the dead are in -- is read before he joins them.
        var others = _state.Cards.Values.Any(c => c.Id != wattu.Id && c.OwnerId == player.Id && c.DefinitionId == wattu.DefinitionId && c.Zone == Zone.Dead);
        var (province, slot) = FindProvinceSlot(player, wattu.Id);
        ClearProvinceSlot(province, slot);
        wattu.ProvinceIndex = null; wattu.Dishonorable = false;
        MarkDead(wattu);
        Emit("personalityDied", $"{Definition(wattu).Name} put himself in {player.Name}'s discard pile, honorably dead.", player.Id, wattu.Id);
        if (!others && slot == OtosanSlotPrimary) RefillProvinceFaceUp(player, province);
        else RefillProvinceSlot(player, province, slot);
    }

    // ---- Seiken and Mikaru: a recruit from a province -------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Recruit a ..., ignoring costs": the card leaves its province, enters play at home with no Gold and no Honor Requirement, the province refills and the entering windows open (true when one did).</summary>
    private bool Wave739RecruitIgnoringCosts(PlayerState player, CardInstance card, string via)
    {
        var (province, slot) = FindProvinceSlot(player, card.Id);
        ClearProvinceSlot(province, slot);
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index;
        EnterPlayFromRecruit(player, card, null, via);
        RefillProvinceSlot(player, province, slot);
        return OpenRecruitEntryWindows(player, card, false, null);
    }

    /// <summary>Seiken's entry trait: "will not gain a Clan Alignment" (P10), a block that stands while he is in play. Called by ApplyMandatoryEntryEffects.</summary>
    private void ApplyWave739EntryEffects(CardInstance card)
    {
        if (Definition(card).Effect == Wave739Abilities.SeikenEffect) AddKeywordGainBlock(card.Id, card.Id, null, classes: ["clan"]);
    }

    // ---- Miwa and the Inn: "refill this province with him, face-up" ---------------------------------------------------------------------------------------------------------------------

    /// <summary>PayAbilityCosts' hook for the ProvinceDiscardCost verb: Miwa is discarded from her province (which refills as it does after any discard); the province is remembered for the search that follows.</summary>
    private void Wave739PayProvinceDiscard(CardInstance card)
    {
        var owner = Player(card.OwnerId);
        var (province, slot) = FindProvinceSlot(owner, card.Id);
        _state.Wave739Refills = new() { [card.Id] = province.Index };
        ClearProvinceSlot(province, slot);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true;
        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province {province.Index + 1}.", owner.Id, card.Id);
        RefillProvinceSlot(owner, province, slot);
    }

    /// <summary>The search disposition RefillProvince (DeckSearch's default branch): the card found takes the province, face-up. A card that already refilled it (face-down) goes back to the top of the Dynasty deck, which the
    /// search then shuffles: as if the refill had been replaced. True when this handled the effect.</summary>
    private bool Wave739ApplySearchEffect(PendingSearch search, CardInstance card, string effect)
    {
        if (effect != Wave739Abilities.RefillProvince) return false;
        var player = Player(search.PlayerId);
        var province = _state.Wave739Refills is { } refills && refills.Remove(search.SourceCardId, out var index) ? player.Provinces.FirstOrDefault(p => p.Index == index) : null;
        if (_state.Wave739Refills is { Count: 0 }) _state.Wave739Refills = null;
        if (province is null || province.Destroyed || HoldsNoDynasty(province))
        {
            Emit("provinceHoldsNoDynastyCards", $"{Definition(Card(search.SourceCardId)).Name}: the province cannot be refilled with {Definition(card).Name}; he stays in the deck.", player.Id);
            FinishSearch(search);
            return true;
        }
        if (province.CardId is { } displacedId && Card(displacedId) is { Zone: Zone.Province, FaceUp: false } displaced)
        {
            province.CardId = null;
            ToOwnerDeck(displaced, "Top");
        }
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        player.DynastyDeck.Remove(card.Id);
        ResetCardState(card);
        card.Zone = Zone.Province; card.ProvinceIndex = province.Index; card.FaceUp = true; card.Bowed = false;
        province.CardId = card.Id;
        Emit("provinceRefilled", $"{Definition(Card(search.SourceCardId)).Name}: {player.Name} refilled province {province.Index + 1} face up with {Definition(card).Name}.", player.Id, card.Id);
        AfterProvinceRefilledP6(player, province, faceUp: true);
        FinishSearch(search);
        return true;
    }

    // ---- Kokure's Lands ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"This Province does not hold Dynasty cards": HoldsNoDynasty's term, read while Kokure's Lands is the province's Region.</summary>
    private bool Wave739KokureHolds(ProvinceState province) =>
        province.RegionId is { } regionId && _state.Cards.TryGetValue(regionId, out var region) && region.Zone == Zone.Region && Definition(region).Effect == Wave739Abilities.KokureEffect;

    // ---- Dark Audience ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave739Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave739Abilities.DarkAudienceEffect) return null;
        BeginSearch(Wave739Abilities.DarkAudienceSpec, card, player.Id, "event");
        return true;
    }

    // ---- Poison the Cup: the token to remove ---------------------------------------------------------------------------------------------------------------------------------------

    private MatchEngine.EitherOrOption[]? Wave739EitherOrOptions(PendingEitherOr pending, CardInstance? affected)
    {
        if (pending.SpecId != Wave739Abilities.PoisonCupSpec || affected is null) return null;
        return affected.Tokens.Select(t => new EitherOrOption(t.Id, $"Remove the {t.Type} token", $"{Definition(affected).Name}'s {t.Type} token is removed.", EitherOrKind.Choose, 0, true, null)).ToArray();
    }

    private bool Wave739ApplyEitherOr(PendingEitherOr pending, EitherOrOption option, CardInstance source, CardInstance? affected)
    {
        if (pending.SpecId != Wave739Abilities.PoisonCupSpec) return false;
        if (option.Key != EitherOrDoneKey)
        {
            // "Remove a target token from a Personality": the one picked.
            if (affected is not null && affected.Tokens.FirstOrDefault(t => t.Id == option.Key) is { } token) DestroyToken(affected, token);
            return true;
        }
        // "... and give him a -1C Plague token. Lose 1 Honor."
        if (affected is not null && InPlay(affected)) CreateToken(affected, "Plague", source.Id, "Chi", -1);
        GainHonor(pending.ActingPlayerId, -1, Definition(source).Name, source);
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave739Hooks()
    {
        EngineHooks.Abilities.Register("Wave739Abilities", 13900, static abilities => abilities.AddWave739());
        EngineHooks.Searches.Register("Wave739Searches", 13900, static () => [Wave739Abilities.UsoharaSearchSpec, Wave739Abilities.MiwaSearchSpec, Wave739Abilities.InnSearchSpec, Wave739Abilities.DarkAudienceSpec]);
        EngineHooks.ValidateEarly.Register("Wave739Validate", 13900, static (engine, context) => engine.Wave739Validate(context));
        EngineHooks.RunEffects.Register("Wave739RunEffects", 13900, static (engine, context) => engine.RunWave739(context));
        EngineHooks.ResolveEvent.Register("Wave739ResolveEvent", 13900, static (engine, context) => engine.ResolveWave739Event(context.Player, context.Province, context.Card));
        EngineHooks.EitherOrPrompt.Register("Wave739EitherOrPrompt", 13900, static (engine, p, source, affected) => p.SpecId == Wave739Abilities.PoisonCupSpec ? $"{source}: choose a token to remove from {affected}." : null);
        EngineHooks.EitherOrOptions.Register("Wave739EitherOrOptions", 13900, static (engine, p, affected, source, payable) => engine.Wave739EitherOrOptions(p, affected));
        EngineHooks.ApplyEitherOr.Register("Wave739ApplyEitherOr", 13900, static (engine, p, option, source, decider, affected, sourceIds) => engine.Wave739ApplyEitherOr(p, option, source, affected));

        // Seiken: "will not be dishonored" (P11).
        CardRestrictionCatalog.Register(Wave739Abilities.Seiken, new StaticRestriction(RestrictionKind.NoDishonor));
        // Poison the Cup: "This Shugenja has Shadowlands" (P10): its host Personality, while the Spell is attached to him.
        KeywordRuleCatalog.Register(new KeywordRule("w739-poison-cup-shadowlands", KeywordRuleScope.Unit, ["Shadowlands"], SourceEffect: Wave739Abilities.PoisonEffect,
            AllOf: [new KeywordCondition(KeywordConditionKind.SubjectIsType, "Personality")]));
    }
}
