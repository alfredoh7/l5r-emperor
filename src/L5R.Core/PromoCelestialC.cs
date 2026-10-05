namespace L5R.Core;

// Promotional-Celestial, group C (holding-wave-9301, region-wave-9302, follower-wave-9303, personality-wave-9304): eight cards wired from the PRINTED text of their scans.
//
//   Blue Tanuki Bar           "Limited: Bow this card and target a Personality: Give him a +1F Sake token" -- the token primitive (Sakata, Dragon Tattoo).
//   Deadlands                 "After this card enters play: Lose 4 Honor" (EntryHonorLoss, applied where a Region attaches to its province) and "Limited: If there are any cards in this province,
//                             discard this card: Discard a card from the province, refilling it face-up with an Undead Personality in your discard pile" -- Scavenging Party's refill, the
//                             Region's own province, the Region discarded as the cost. "In your discard pile" is read as "your discarded Personality" (a dead one included, Scavenging Party's ruling).
//   The Ivory Coast           "Battle: Choose your performing Personality and bow this card: Move him home. Straighten his unit if he moved" -- Forging Destiny's move, the Region bowed as the cost.
//   War-ravaged Plain         "Limited: Discard this card and target a Region: Destroy it" -- the Region discarded as the cost, then DestroyRegion (Brawl's, Set the Fields Aflame's removal).
//   Recruitment Officer       "Reaction: After engaging at a battlefield where you control one or more units, bow this card: Create a 2F/2C/2PH Ronin * Samurai Personality there. Remove him
//                             from the game when this turn ends" -- Fruitless Search's create with Perplexing Guests' turn-end removal; the scan is Fresh Recruit (oracle-l5r-9997).
//   The Imperial Treasury     "Produce 1 Gold, plus 1 for each non-Unique Holding you control" (GoldProduced's term); "Enters play paying 1 less Gold if you are a Mantis Clan player" is the
//                             shared mantis-clan-discount Effect.
//   Fudo Cultist              "Attaches to a Nonhuman Fudo Personality ignoring Gold cost" -- the attach PRICE only (no restriction), as every "Attaches to X paying less" card is wired
//                             (Record of Failure, Maga-yari, Purify the Soul); "+1F for each other Fudo card you control" -- FollowerForce's term.
//   Yotsu Ueda's Chosen       "Attaches to Yotsu Ueda paying 2 less Gold" (the same price-only reading) and "Battle: Target an attachment: Bow or straighten it" -- Hama's bow/straighten pair.
public static class PromoCelestialCAbilities
{
    public const string TanukiSake = "pcc-tanuki-sake", DeadlandsRefill = "pcc-deadlands-refill", IvoryCoastHome = "pcc-ivory-coast-home", WarRavagedDestroy = "pcc-war-ravaged-destroy",
        RecruitmentOfficer = "pcc-recruitment-officer", YotsuBow = "pcc-yotsu-bow", YotsuStraighten = "pcc-yotsu-straighten";
    public const string FudoCultistEffect = "pcc-fudo-cultist", YotsuChosenEffect = "pcc-yotsu-chosen";
    public const string ImperialTreasuryId = "oracle-l5r-8150", FreshRecruitId = "oracle-l5r-9997";

    public static Dictionary<string, AbilityDefinition> AddPromoCelestialC(this Dictionary<string, AbilityDefinition> all)
    {
        all[TanukiSake] = new("Limited", "Bow this card and target a Personality: Give him a +1F Sake token", "personality", Costs: ActionCost.BowSource);
        // The province is the Region's own (captured before the Region is discarded as the cost), so the player names no province.
        all[DeadlandsRefill] = new("Limited", "If there are any cards in this province, discard this card: Discard a card from the province, refilling it face-up with an Undead Personality in your discard pile",
            "own-discarded-personality-with-keyword", RequiredKeywords: ["Undead"], Costs: [new(CostVerbs.DiscardFromPlay)]);
        all[IvoryCoastHome] = new("Battle", "Choose your performing Personality and bow this card: Move him home. Straighten his unit if he moved", "none",
            Performer: "own-personality-at-battle", Costs: ActionCost.BowSource);
        all[WarRavagedDestroy] = new("Limited", "Discard this card and target a Region: Destroy it", "region", Costs: [new(CostVerbs.DiscardFromPlay)]);
        all[RecruitmentOfficer] = new("Reaction", "After engaging at a battlefield where you control one or more units, bow this card: Create a 2F/2C/2PH Ronin • Samurai Personality there. Remove him from the game when this turn ends",
            "none", Trigger: "after-engaging", Costs: ActionCost.BowSource,
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 2, Keywords: ["Ronin", "Samurai"], ProxyId: FreshRecruitId, AtBattlefield: true));
        // One printed action with a choice of effect: the two entries share one use (Hama's, Isul's shape).
        all[YotsuBow] = new("Battle", "Target an attachment: Bow it", "any-attachment", UsageKey: "pcc-yotsu-toggle");
        all[YotsuStraighten] = new("Battle", "Target an attachment: Straighten it", "any-attachment", UsageKey: "pcc-yotsu-toggle");
        return all;
    }
}

public sealed partial class MatchEngine
{
    // Deadlands' province, read from the Region before it is discarded as the cost (-1: none).
    private int _pccDeadlandsProvince = -1;

    // ---- validation -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidatePromoCelestialC(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PromoCelestialCAbilities.DeadlandsRefill:
                {
                    var province = source is { ProvinceIndex: int index } ? player.Provinces.FirstOrDefault(p => p.Index == index && p.RegionId == source.Id) : null;
                    Require(province is { Destroyed: false, CardId: not null }, "There are no cards in this province.");
                    _pccDeadlandsProvince = province!.Index;
                    break;
                }
            case PromoCelestialCAbilities.WarRavagedDestroy:
                Require(command.TargetId is not null && command.TargetId != source?.Id, "Target a Region other than this one.");
                break;
            case PromoCelestialCAbilities.RecruitmentOfficer:
                Require(_state.CurrentBattleProvince is int battle && Army(player.Id, battle).Any(), "You control no units at this battlefield.");
                break;
        }
    }

    // ---- effects ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunPromoCelestialC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoCelestialCAbilities.TanukiSake:
                CreateToken(target!, "Sake", id, "Force", 1);
                return false;
            case PromoCelestialCAbilities.DeadlandsRefill:
                if (_pccDeadlandsProvince >= 0) RefillProvinceWithDiscardedPersonality(player, target!, _pccDeadlandsProvince, Definition(source!).Name);
                _pccDeadlandsProvince = -1;
                return false;
            case PromoCelestialCAbilities.IvoryCoastHome:
                {
                    var performer = Card(command.PerformerId!);
                    var wasAway = performer.Zone == Zone.Battlefield;
                    MoveHome(performer, false);
                    if (wasAway && performer.Zone == Zone.Home) StraightenBowedUnit(performer); // "Straighten his unit if he moved."
                    return false;
                }
            case PromoCelestialCAbilities.WarRavagedDestroy:
                DestroyRegion(target!);
                return false;
            case PromoCelestialCAbilities.RecruitmentOfficer:
                {
                    var name = Definition(source!).Name;
                    return ExecuteCreate(ability, ability.Creates!, command, player, () =>
                    {
                        var created = Card($"created-{_state.CreatedCardSequence:D4}");
                        ScheduleTimedRemoval(created, TimedRemovalWhen.TurnEnds, player.Id, name);
                    });
                }
            case PromoCelestialCAbilities.YotsuBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case PromoCelestialCAbilities.YotsuStraighten:
                Straighten(target!);
                return false;
            default:
                return null;
        }
    }

    // ---- Regions leaving and entering play (the shared chains' one-line hooks) --------------------------------------------------------------------------------------------------------------

    // AttachRegionToProvince's hook: "After this card enters play: Lose N Honor" on a Region (Deadlands).
    private void PromoCelestialCRegionEntered(PlayerState player, CardInstance region)
    {
        if (Definition(region).EntryHonorLoss is int loss) GainHonor(player.Id, -loss, $"{Definition(region).Name} entering play", region);
    }

    // The DiscardFromPlay cost's hook: a Region discarded from play leaves its province (Deadlands, War-ravaged Plain).
    private void PromoCelestialCRegionLeft(CardInstance subject)
    {
        if (subject.Zone != Zone.Region) return;
        foreach (var province in _state.Players.SelectMany(p => p.Provinces).Where(p => p.RegionId == subject.Id)) province.RegionId = null;
    }

    // ---- prices and stats ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // EquipGoldCost's term. Fudo Cultist: "ignoring Gold cost" on a Nonhuman Fudo Personality (the whole price). Yotsu Ueda's Chosen: "paying 2 less Gold" on Yotsu Ueda.
    private (int Amount, string Basis) PromoCelestialCAttachDiscount(CardInstance card, CardInstance wielder, int raw) => Definition(card).Effect switch
    {
        PromoCelestialCAbilities.FudoCultistEffect when Keyword(wielder, "Nonhuman") && Keyword(wielder, "Fudo") => (raw, " − all (Nonhuman Fudo Personality)"),
        PromoCelestialCAbilities.YotsuChosenEffect when Definition(wielder).Name == "Yotsu Ueda" => (2, " − 2 (Yotsu Ueda)"),
        _ => (0, "")
    };

    // FollowerForce's term: Fudo Cultist's "+1F for each other Fudo card you control".
    private int PromoCelestialCForce(CardInstance card) =>
        Definition(card).Effect == PromoCelestialCAbilities.FudoCultistEffect && InPlay(card)
            ? _state.Cards.Values.Count(c => c.Id != card.Id && c.OwnerId == card.OwnerId && InPlay(c) && Keyword(c, "Fudo")) : 0;

    // GoldProduced's term: The Imperial Treasury's "plus 1 additional Gold for each non-Unique Holding you control".
    private int PromoCelestialCProduction(CardInstance card, int playerId) =>
        Definition(card).Id == PromoCelestialCAbilities.ImperialTreasuryId
            ? _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Holding && !Keyword(c, "Unique")) : 0;

    // Test seam: the Events Phase reveal of a Region the player holds in a province (it attaches, refills the province and fires its entry traits).
    internal bool ResolveRegionForTest(int playerId, string cardId) { var card = Card(cardId); return ResolveRegion(Player(playerId), Player(playerId).Provinces[card.ProvinceIndex!.Value], card); }

    // ---- registration -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoCelestialCHooks()
    {
        EngineHooks.Abilities.Register("AddPromoCelestialC", 12930, static abilities => abilities.AddPromoCelestialC());
        EngineHooks.ValidateLate.Register("ValidatePromoCelestialC", 12930, static (engine, context) => engine.ValidatePromoCelestialC(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunPromoCelestialC", 12930, static (engine, context) => engine.RunPromoCelestialC(context.id, context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.BowsTarget.Register("PromoCelestialCBowsTarget", 12930, static context => context.abilityId == PromoCelestialCAbilities.YotsuBow);
    }
}
