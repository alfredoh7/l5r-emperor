namespace L5R.Core;

// Issue #653 (Promotional-Emperor pass): Holdings (holding-wave-51) and Regions (region-wave-22) of the Promotional-Emperor set, each wired from the PRINTED text of its scan (read by eye).
//
// Existing primitives used as they are:
//   Family Dojo, General's Hatamoto, Retired Sensei          Battle actions that add Force (the generic modifier; Retired Sensei's "Even if this card is bowed" is the bowed-source exemption).
//   Emerald Dojo, Ivory Dojo, Ruby Dojo, Turquoise Court      Open / Political Limited bows and straightens (one or two target slots); Turquoise Court's "gain or lose" is two abilities sharing one use,
//                                                              the printed choice taken when the action is announced (Togashi Hizumi's province +4/-4 shape), over a player slot.
//   Topaz Dojo                                                 a two-stat token (+1F/+1C Training, CreateTwoStatToken) on a Personality whose printed text is empty.
//   Obsidian Dueling Grounds, Second City Dojo, Forgotten Outpost, Public Room, Jade Temple, Jungle Stockade, Den of Iniquity   effects of their own below.
//   Border Market (Region)                                    the attach-from-discard primitive (AttachSpec, AttachAction.cs): Border Market was the named "one AttachSpec away" card.
//   Interesting Plains (Region), Quicksand, Renewed Lands, Ambush Pass   a Region's Reaction on a Personality entering from a province (Kaiu Village's trigger), a Battle action that destroys the Region,
//                                                              the entry trait of a Farm (ApplyMandatoryEntryEffects' hook), and a static Force bonus (PersonalityForce).
//
// Small shared edits (each one line, named in the report): ConditionalProductionOf and GoldProduced (Bandit's Sanctuary, Jungle Stockade, Plantation), the bowed-source exemption in ValidateAbility
// (Retired Sensei), ApplyMandatoryEntryEffects (Bandit's Sanctuary, Renewed Lands), P2HandDiscardBlocked (Sacred Temples), WillNotBeDestroyed and GainHonor (Den of Iniquity).
public static class PromoEmperorHoldingsAbilities
{
    public const string FamilyDojoBoost = "pe-family-dojo-boost", HatamotoBoost = "pe-generals-hatamoto-boost", RetiredSenseiBoost = "pe-retired-sensei-boost",
        EmeraldStraighten = "pe-emerald-dojo-straighten", IvoryBow = "pe-ivory-dojo-bow", RubyDishonor = "pe-ruby-dojo-dishonor", TopazTraining = "pe-topaz-dojo-training",
        TurquoiseGain = "pe-turquoise-court-gain", TurquoiseLose = "pe-turquoise-court-lose", ObsidianTransform = "pe-obsidian-dueling-grounds",
        SecondCityDojoDraw = "pe-second-city-dojo-draw", OutpostHonor = "pe-forgotten-outpost-honor", PublicRoomSake = "pe-public-room-sake", JadeTempleStraighten = "pe-jade-temple-straighten",
        StockadeTransfer = "pe-jungle-stockade-transfer", DenShame = "pe-den-of-iniquity-shame",
        BorderMarketAttach = "pe-border-market-attach", PlainsRefill = "pe-interesting-plains-refill", QuicksandDestroy = "pe-quicksand-destroy";

    // Effects (data keys on the card, never prose).
    public const string BanditsSanctuaryEffect = "pe-bandits-sanctuary", StockadeEffect = "pe-jungle-stockade-gold", PlantationEffect = "pe-plantation-stronghold", SacredTemplesEffect = "pe-sacred-temples",
        DenEffect = "pe-den-of-iniquity", AmbushPassEffect = "pe-ambush-pass-force", RenewedLandsEffect = "pe-renewed-lands";

    // Target categories (TargetAllows hook).
    public const string OwnMagistrateUnbowed = "pe-own-unbowed-magistrate", UntrainedPersonality = "pe-untrained-personality", OwnSpell = "pe-own-spell",
        AttachmentOnPerformer = "pe-attachment-on-performer", EnemyDishonorable = "pe-enemy-dishonorable-personality", QuicksandTarget = "pe-quicksand-target";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorHoldings(this Dictionary<string, AbilityDefinition> all)
    {
        // Family Dojo (oracle-l5r-10573): "Battle, bow: Give each of your Followers at this battle +1F."
        all[FamilyDojoBoost] = new("Battle", "Battle, bow this card: Give each of your Followers at this battle +1F", "none", Costs: ActionCost.BowSource);
        // General's Hatamoto (oracle-l5r-10572): "Battle, bow: Give a target Personality or Follower +2F." (either player's)
        all[HatamotoBoost] = new("Battle", "Battle, bow this card: Give a target Personality or Follower +2F", "personality-or-follower", Costs: ActionCost.BowSource);
        // Retired Sensei (oracle-l5r-9955): "Battle: Even if this card is bowed, target your Personality: Give him +2F." No cost: the bowed-source exemption is PromoEmperorEvenIfBowed.
        all[RetiredSenseiBoost] = new("Battle", "Battle: Even if this card is bowed, target your Personality: Give him +2F", "own-personality");
        // The Emerald Dojo (oracle-l5r-10753): "Open, bow: Straighten your target Personality."
        all[EmeraldStraighten] = new("Open", "Open, bow this card: Straighten your target Personality", "own-personality", Costs: ActionCost.BowSource);
        // The Ivory Dojo (oracle-l5r-10754): "Open, bow: Bow your target unbowed Personality. Bow a target dishonorable Personality."
        all[IvoryBow] = new("Open", "Open, bow this card: Bow your target unbowed Personality. Bow a target dishonorable Personality", "slots", Costs: ActionCost.BowSource,
            Slots: [new("own", "own-unbowed-personality", "Your unbowed Personality to bow"), new("dishonorable", "dishonorable-personality", "A dishonorable Personality to bow")]);
        // The Ruby Dojo (oracle-l5r-10758): "Political Limited, bow: Bow your target unbowed Magistrate Personality. Dishonor a target Personality."
        all[RubyDishonor] = new("Limited", "Political Limited, bow this card: Bow your target unbowed Magistrate Personality. Dishonor a target Personality", "slots", ActionKeywords: ["Political"], Costs: ActionCost.BowSource,
            Slots: [new("magistrate", OwnMagistrateUnbowed, "Your unbowed Magistrate Personality to bow"), new("dishonor", "personality", "A Personality to dishonor")]);
        // The Topaz Dojo (oracle-l5r-10759): "Open, bow: Give a +1F/+1C Training token to a target Personality with no printed abilities and no Training tokens."
        all[TopazTraining] = new("Open", "Open, bow this card: Give a +1F/+1C Training token to a target Personality with no printed abilities and no Training tokens", UntrainedPersonality, Costs: ActionCost.BowSource);
        // The Turquoise Court (oracle-l5r-10760): "Political Limited, bow: Bow your target unbowed Personality. You, or any player if the Personality is an Artisan, gain or lose 1 Honor."
        // The printed "gain or lose" is a choice of effect, two abilities sharing one use; the player who gains or loses is a slot (an opponent only when the Personality is an Artisan).
        all[TurquoiseGain] = new("Limited", "Political Limited, bow this card: Bow your target unbowed Personality. You, or any player if the Personality is an Artisan, gain 1 Honor", "slots", UsageKey: "pe-turquoise-court",
            ActionKeywords: ["Political"], Costs: ActionCost.BowSource,
            Slots: [new("target", "own-unbowed-personality", "Your unbowed Personality to bow"), new("player", "player", "The player who gains 1 Honor")]);
        all[TurquoiseLose] = new("Limited", "Political Limited, bow this card: Bow your target unbowed Personality. You, or any player if the Personality is an Artisan, lose 1 Honor", "slots", UsageKey: "pe-turquoise-court",
            ActionKeywords: ["Political"], Costs: ActionCost.BowSource,
            Slots: [new("target", "own-unbowed-personality", "Your unbowed Personality to bow"), new("player", "player", "The player who loses 1 Honor")]);
        // The Obsidian Dueling Grounds (oracle-l5r-10756): "Limited, bow: Permanently give your target Samurai Shadowlands. Give him three +1F tokens. Destroy this Holding. Lose 4 Honor."
        all[ObsidianTransform] = new("Limited", "Limited, bow this card: Permanently give your target Samurai Shadowlands. Give him three +1F tokens. Destroy this Holding. Lose 4 Honor", "own-personality-with-keyword",
            RequiredKeywords: ["Samurai"], Costs: ActionCost.BowSource);
        // Second City Dojo (oracle-l5r-6539): "Limited: Destroy this card: Draw a card."
        all[SecondCityDojoDraw] = new("Limited", "Limited: Destroy this card: Draw a card", "none", Costs: [new(CostVerbs.Sacrifice)]);
        // Forgotten Outpost (oracle-l5r-2658): "Limited: If another player has higher Family Honor than you: Gain 1 Honor." (the condition is judged in ValidatePromoEmperorHoldings)
        all[OutpostHonor] = new("Limited", "Limited: If another player has higher Family Honor than you: Gain 1 Honor", "none");
        // Public Room (oracle-l5r-6097): "Open: Bow this card: Give each of your Sake tokens +1F."
        all[PublicRoomSake] = new("Open", "Open: Bow this card: Give each of your Sake tokens +1F", "none", Costs: ActionCost.BowSource);
        // The Jade Temple (oracle-l5r-10755): "Battle, bow: Straighten your target Spell. You may use its abilities a second time."
        all[JadeTempleStraighten] = new("Battle", "Battle, bow this card: Straighten your target Spell. You may use its abilities a second time", OwnSpell, Costs: ActionCost.BowSource);
        // Jungle Stockade (oracle-l5r-4066): "Open: Choose your two performing Personalities and target an attachment on one of them: Transfer it to the other Personality."
        all[StockadeTransfer] = new("Open", "Open: Choose your two performing Personalities and target an attachment on one of them: Transfer it to the other Personality", "slots",
            Slots: [new("performers", "own-personality", "Your two performing Personalities", 2, 2, Performing: true), new("attachment", AttachmentOnPerformer, "An attachment on one of them")]);
        // Den of Iniquity (oracle-l5r-1950): "Political Limited: Bow this card and target another player's dishonorable Personality: His controller loses Honor equal to his base Personal Honor, or 3,
        // whichever is lower. You may not declare an attack this turn."
        all[DenShame] = new("Limited", "Political Limited: Bow this card and target another player's dishonorable Personality: His controller loses Honor equal to his base Personal Honor, or 3, whichever is lower. You may not declare an attack this turn",
            EnemyDishonorable, ActionKeywords: ["Political"], Costs: ActionCost.BowSource);

        // ---- Regions (region-wave-22) ------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Border Market (oracle-l5r-1108): "Open: Bow this card, choose your performing Personality, and target a non-Unique Follower or Item in your discard pile: Attach it to your performer, paying all costs."
        all[BorderMarketAttach] = new("Open", "Open: Bow this card, choose your performing Personality, and target a non-Unique Follower or Item in your discard pile: Attach it to your performer, paying all costs", "attachment-in-discard",
            Performer: "own-personality", Cost: GoldCostRule.AttachCost(), Costs: ActionCost.BowSource,
            Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, [CardType.Follower, CardType.Item], null, ["Unique"]));
        // Interesting Plains (oracle-l5r-9961): "Reaction: After a Personality enters play from this province: Refill the province face-up."
        all[PlainsRefill] = new("Reaction", "Reaction: After a Personality enters play from this province: Refill the province face-up", "none", Trigger: EmperorRulingR3Abilities.EntersFromProvinceTrigger);
        // Quicksand (oracle-l5r-9962): "Battle: Destroy this card and target a Follower or Personality without Followers at this province's battlefield: Destroy it."
        all[QuicksandDestroy] = new("Battle", "Battle: Destroy this card and target a Follower or Personality without Followers at this province's battlefield: Destroy it", QuicksandTarget, Costs: [new(CostVerbs.DestroyRegion)]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunPromoEmperorHoldings(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoEmperorHoldingsAbilities.FamilyDojoBoost:
                foreach (var follower in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Attached && Definition(c).Type == CardType.Follower && AtBattle(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                    ApplyModifier(follower, source!.Id, id, "Force", 1);
                return false;
            case PromoEmperorHoldingsAbilities.HatamotoBoost:
                ApplyModifier(target!, source!.Id, id, "Force", 2);
                return false;
            case PromoEmperorHoldingsAbilities.RetiredSenseiBoost:
                ApplyModifier(target!, source!.Id, id, "Force", 2);
                return false;
            case PromoEmperorHoldingsAbilities.EmeraldStraighten:
                Straighten(target!);
                return false;
            case PromoEmperorHoldingsAbilities.IvoryBow:
                foreach (var bowed in SlotCards(command, "own").Concat(SlotCards(command, "dishonorable")).ToArray())
                    if (!StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                return false;
            case PromoEmperorHoldingsAbilities.RubyDishonor:
                foreach (var magistrate in SlotCards(command, "magistrate").ToArray())
                    if (!StalwartNegatesBow(magistrate, player.Id)) magistrate.Bowed = true;
                foreach (var shamed in SlotCards(command, "dishonor").ToArray()) Dishonor(shamed, player.Id);
                return false;
            case PromoEmperorHoldingsAbilities.TopazTraining:
                CreateTwoStatToken(target!, "Training", source!.Id, "Force", 1, "Chi", 1);
                return false;
            case PromoEmperorHoldingsAbilities.TurquoiseGain:
            case PromoEmperorHoldingsAbilities.TurquoiseLose:
                {
                    var bowedTarget = SlotCards(command, "target").First();
                    if (!StalwartNegatesBow(bowedTarget, player.Id)) bowedTarget.Bowed = true;
                    var who = SlotPlayer(command, "player") ?? player.Id;
                    GainHonor(who, id == PromoEmperorHoldingsAbilities.TurquoiseGain ? 1 : -1, Definition(source!).Name, source);
                    return false;
                }
            case PromoEmperorHoldingsAbilities.ObsidianTransform:
                GrantKeyword(target!, "Shadowlands", source!.Id, GrantDuration.Permanent);
                for (var n = 0; n < 3; n++) CreateToken(target!, "Force", id, "Force", 1);
                if (InPlay(source!)) DestroyUnits([source!], false, player.Id);
                GainHonor(player.Id, -4, Definition(source!).Name, source);
                return false;
            case PromoEmperorHoldingsAbilities.SecondCityDojoDraw:
                Draw(player);
                return false;
            case PromoEmperorHoldingsAbilities.OutpostHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case PromoEmperorHoldingsAbilities.PublicRoomSake:
                foreach (var holder in _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && c.Tokens.Any(t => t.Type == "Sake")).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                    foreach (var _ in holder.Tokens.Where(t => t.Type == "Sake").ToArray()) ApplyModifier(holder, source!.Id, id, "Force", 1);
                return false;
            case PromoEmperorHoldingsAbilities.JadeTempleStraighten:
                Straighten(target!);
                RefreshBattleAbilities(target!);
                Emit("secondUseGranted", $"{Definition(source!).Name}: {Definition(target!).Name}'s abilities may be used a second time this turn.", player.Id, target!.Id);
                return false;
            case PromoEmperorHoldingsAbilities.StockadeTransfer:
                {
                    var attachment = SlotCards(command, "attachment").Single();
                    var other = SlotCards(command, "performers").Single(p => p.Id != attachment.AttachedTo);
                    TransferAttachment(attachment, other, Definition(source!).Name);
                    return false;
                }
            case PromoEmperorHoldingsAbilities.DenShame:
                {
                    var loss = Math.Min(Definition(target!).PersonalHonor, 3);
                    GainHonor(target!.OwnerId, -loss, Definition(source!).Name, source);
                    player.MayNotAttackThisTurn = true;
                    return false;
                }
            case PromoEmperorHoldingsAbilities.PlainsRefill:
                RefillProvinceFaceUp(player, _state.LastProvinceEntryProvinceIndex!.Value, source!);
                return false;
            case PromoEmperorHoldingsAbilities.QuicksandDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            default:
                return null;
        }
    }

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidatePromoEmperorHoldings(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PromoEmperorHoldingsAbilities.OutpostHonor:
                Require(_state.Players.Any(p => p.Id != player.Id && p.Honor > player.Honor), "No other player has higher Family Honor than you.");
                break;
            case PromoEmperorHoldingsAbilities.TurquoiseGain:
            case PromoEmperorHoldingsAbilities.TurquoiseLose:
                // "You, or any player if the Personality is an Artisan": another player only for an Artisan.
                if (SlotPlayer(command, "player") is int who && who != player.Id && SlotCards(command, "target").FirstOrDefault() is { } bowedTarget)
                    Require(Keyword(bowedTarget, "Artisan"), "Only you gain or lose the Honor unless the Personality is an Artisan.");
                break;
            case PromoEmperorHoldingsAbilities.PlainsRefill:
                {
                    var province = player.Provinces.FirstOrDefault(p => p.RegionId == source!.Id);
                    Require(province is not null && _state.LastProvinceEntryProvinceIndex == province.Index, "The Personality did not enter play from this province.");
                    break;
                }
        }
    }

    // The target categories of this group.
    private bool PromoEmperorTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        switch (category)
        {
            case PromoEmperorHoldingsAbilities.OwnMagistrateUnbowed: return personality && own && !target.Bowed && Keyword(target, "Magistrate");
            // "no printed abilities": a Personality whose printed text is empty (every ability a card has is printed in its text); "no Training tokens".
            case PromoEmperorHoldingsAbilities.UntrainedPersonality: return personality && string.IsNullOrWhiteSpace(Definition(target).Text) && !target.Tokens.Any(t => t.Type == "Training");
            case PromoEmperorHoldingsAbilities.OwnSpell: return own && target.Zone == Zone.Attached && Definition(target).Type == CardType.Spell;
            case PromoEmperorHoldingsAbilities.EnemyDishonorable: return personality && !own && target.Dishonorable;
            case PromoEmperorHoldingsAbilities.AttachmentOnPerformer:
                {
                    if (target.Zone != Zone.Attached || Definition(target).Type is not (CardType.Follower or CardType.Item or CardType.Spell) || target.AttachedTo is not { } hostId) return false;
                    var performers = SlotCards(command, "performers").ToArray();
                    var host = performers.FirstOrDefault(p => p.Id == hostId);
                    return host is not null && performers.Length == 2 && performers.FirstOrDefault(p => p.Id != hostId) is { } other && CanTransferTo(target, other);
                }
            case PromoEmperorHoldingsAbilities.QuicksandTarget:
                // "At this province's battlefield": the current battlefield's province is the one this Region is attached to (the generic Battle presence check asks for the target to stand there).
                return source is not null && CurrentBattlefieldProvince() is { RegionId: { } regionId } && regionId == source.Id
                    && (personality && !Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower) || Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached);
            default: return false;
        }
    }

    // ValidateAbility's list of abilities that may be taken while their card is bowed: Retired Sensei's "Even if this card is bowed".
    private static bool PromoEmperorEvenIfBowed(string id) => id == PromoEmperorHoldingsAbilities.RetiredSenseiBoost;

    // ---- static -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Ambush Pass (oracle-l5r-10195): "Your Ronin have +1F." Live while the Region is attached to a province of the controller's.
    private int PromoEmperorForce(CardInstance card) =>
        InPlay(card) && Definition(card).Type == CardType.Personality && Keyword(card, "Ronin")
        && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && c.Zone == Zone.Region && Definition(c).Effect == PromoEmperorHoldingsAbilities.AmbushPassEffect) ? 1 : 0;

    // ConditionalProductionOf's hook. Bandit's Sanctuary: "Produce 2 Gold, or 3 Gold if you are paying for a Ronin" (a Ronin card bought, equipped or played, never an action).
    // Jungle Stockade: "Produce 3 Gold, or 4 Gold if paying for an attachment" (Platinum Mine's condition).
    private static ConditionalProduction? PromoEmperorConditionalProduction(CardInstance card) => Definition(card).Effect switch
    {
        PromoEmperorHoldingsAbilities.BanditsSanctuaryEffect => new(3, ProductionCondition.PayingForKeyword, "Ronin"),
        PromoEmperorHoldingsAbilities.StockadeEffect => new(4, ProductionCondition.PayingForAttachment),
        _ => null
    };

    // GoldProduced's term. Plantation (oracle-l5r-9953): "Your Stronghold produces one more Gold, to a maximum of five, when paying for a non-Personality card" -- a card bought, equipped or played
    // (Staging Grounds' reading: never an action of a card in play), each Plantation adding one while the Stronghold stays at five or fewer.
    private int PromoEmperorProductionAdjustment(CardInstance card, int playerId, PaymentSubject? paying)
    {
        if (Definition(card).Type == CardType.Holding) return PromoEmperorFarmBonus(card, playerId); // Kenro-ji-jin's Bounty (PromoEmperorHoldingsCelestials.cs)
        if (Definition(card).Type != CardType.Stronghold || paying is null || paying.Kind == PaymentKind.Action || Definition(paying.Card).Type == CardType.Personality) return 0;
        var plantations = _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Holding && Definition(c).Effect == PromoEmperorHoldingsAbilities.PlantationEffect);
        if (plantations == 0) return 0;
        var printed = Definition(card).GoldProduction;
        return Math.Max(0, Math.Min(5, printed + plantations) - printed);
    }

    internal bool HandDiscardBlockedForTest(int handOwner, int causer, bool byAction) => P2HandDiscardBlocked(handOwner, causer, byAction, "a test action");

    // Sacred Temples (oracle-l5r-10409): "Other players' actions will not discard cards from your hand" -- P2HandDiscardBlocked's protector for an action.
    private CardInstance? PromoEmperorHandProtector(int handOwner) =>
        _state.Cards.Values.FirstOrDefault(c => c.OwnerId == handOwner && InPlay(c) && Definition(c).Effect == PromoEmperorHoldingsAbilities.SacredTemplesEffect);

    // Den of Iniquity (oracle-l5r-1950): "This card will not be destroyed." (WillNotBeDestroyed's term)
    private bool PromoEmperorWillNotBeDestroyed(CardInstance card) => InPlay(card) && Definition(card).Effect == PromoEmperorHoldingsAbilities.DenEffect;

    // Den of Iniquity: "Other players will not lose Honor from your Spells; these are not negation." A loss of another player whose cause is a Spell of the Den's controller: the card named as the
    // loss's source, else the card whose action is resolving. It simply does not happen (no window, no negation to be overridden), GainHonor's first step.
    private bool PromoEmperorHonorLossPrevented(int playerId, int amount, CardInstance? source)
    {
        if (amount >= 0) return false;
        var cause = source ?? (_runningCommand?.CardId is { } runningId && _state.Cards.TryGetValue(runningId, out var running) ? running : null);
        if (cause is null || Definition(cause).Type != CardType.Spell || cause.OwnerId == playerId) return false;
        var den = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == cause.OwnerId && InPlay(c) && Definition(c).Effect == PromoEmperorHoldingsAbilities.DenEffect);
        if (den is null) return false;
        Emit("honorLossPrevented", $"{Definition(den).Name}: {Player(playerId).Name} will not lose Honor from {Definition(cause).Name}.", playerId, den.Id);
        return true;
    }

    // ---- entry traits (ApplyMandatoryEntryEffects' hook) ------------------------------------------------------------------------------------------------------------------------------

    private void ApplyPromoEmperorEntryEffects(int playerId, CardInstance card)
    {
        var def = Definition(card);
        // Bandit's Sanctuary (oracle-l5r-10187): "After this card enters play, if you have a Clan alignment: Lose 3 Honor." A trait, so mandatory.
        if (def.Effect == PromoEmperorHoldingsAbilities.BanditsSanctuaryEffect && Player(playerId).Clan != "Unaligned")
            GainHonor(playerId, -3, $"{def.Name} entering play", card);
        // Renewed Lands (oracle-l5r-10234): "After a Farm Holding enters play from this province: Give it a +1 Gold Production token and refill this province face-up." A trait.
        if (def.Type == CardType.Holding && Keyword(card, "Farm") && _state.LastProvinceEntryCardId == card.Id && _state.LastProvinceEntryProvinceIndex is int index
            && Player(playerId).Provinces[index] is { RegionId: { } regionId } && _state.Cards.TryGetValue(regionId, out var region) && region.Zone == Zone.Region
            && Definition(region).Effect == PromoEmperorHoldingsAbilities.RenewedLandsEffect)
        {
            CreateToken(card, "Gold Production", region.Id, "GoldProduction", 1);
            RefillProvinceFaceUp(Player(playerId), index, region);
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization. Priority 12400 is this group's.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorHoldingsHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorHoldings", 12400, static abilities => abilities.AddPromoEmperorHoldings());
        EngineHooks.RunEffects.Register("RunPromoEmperorHoldings", 12400, static (engine, context) => engine.RunPromoEmperorHoldings(context.id, context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidatePromoEmperorHoldings", 12400, static (engine, context) => engine.ValidatePromoEmperorHoldings(context.id, context.command, context.player, context.source));
        EngineHooks.TargetAllows.Register("PromoEmperorTargetAllows", 12400, static (engine, context) => engine.PromoEmperorTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PersonalityForce.Register("PromoEmperorForce", 12400, static (engine, context) => engine.PromoEmperorForce(context.card));
        EngineHooks.BowsTarget.Register("PromoEmperorBowsTarget", 12400, static context => context.abilityId is PromoEmperorHoldingsAbilities.IvoryBow or PromoEmperorHoldingsAbilities.RubyDishonor
            or PromoEmperorHoldingsAbilities.TurquoiseGain or PromoEmperorHoldingsAbilities.TurquoiseLose);
        EngineHooks.DestroysTarget.Register("PromoEmperorDestroysTarget", 12400, static context => context.abilityId == PromoEmperorHoldingsAbilities.QuicksandDestroy);
    }
}
