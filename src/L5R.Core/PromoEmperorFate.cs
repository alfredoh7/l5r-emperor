using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PASS (issue #653), the Strategies and Spells (strategy-wave-112, spell-wave-51). Every card was read against its Promotional-Emperor scan; the scan wins over the scraped text
// (the Emperor-era scans print "Battle/Open:" where Oracle prints another designator, and some print no keyword line the catalogue invented).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md and docs/rules/RULINGS.md; none conflicts with them):
//   * UNIQUE PERSONALITY TARGETS and the small Battle/Open effects (Legendary Rivalry, Tamori Potion, Bar Fight, Might of the Sun, Summon Fushicho) ride the existing target / cost / attack hooks.
//   * "BEFORE THIS PHASE ENDS, PUT THIS STRATEGY INTO YOUR HAND" (Elemental Adroitness): PromoEmperorState.PhaseReturns, applied where the phase family changes (SetPhase calls PromoPhaseEnds).
//   * "DRAW CARDS UNTIL YOU HAVE EXACTLY TWO" (A Glimpse of Fate): the optional discard is a hand slot (0..1), the draw counts the hand without the Strategy being played.
//
// READINGS (each is also recorded in the report; none is a confirmed ruling):
//   * Tamori Potion's "different element keyword in his unit" counts Air, Earth, Fire, Water and Void among the Personality and every card attached to him.
//   * A Glimpse of Fate's discard is an effect ("you may"), never a cost, and cannot discard the Strategy that is being played; with more than two cards left in hand it draws nothing.
//   * Might of the Sun's "bow this Shugenja unless he is Fire or Thunder" is two catalogue entries sharing one use: a Fire or Thunder Shugenja pays nothing, any other bows (void-tattoo-return's shape).
// ======================================================================================================================================================================================

/// <summary>What the Promotional-Emperor Strategies and Spells keep between commands.</summary>
public sealed partial class PromoEmperorFateState
{
    /// <summary>Elemental Adroitness: "Before this phase ends, put this Strategy into your hand" -- the card, its owner, and the phase family (as PhaseFamily numbers it) it was played in.</summary>
    public List<PromoPhaseReturn> PhaseReturns { get; set; } = [];
}

public sealed record PromoPhaseReturn(string CardId, int PlayerId, int Family);

public sealed partial class MatchState
{
    [JsonPropertyOrder(1580)]
    public PromoEmperorFateState PromoFate { get; set; } = new();
}

public static partial class PromoEmperorFateAbilities
{
    public const string LegendaryRivalry = "pe-legendary-rivalry-bow", TamoriPotion = "pe-tamori-potion-attack", BarFightAttack = "pe-bar-fight-attack",
        MightBow = "pe-might-of-the-sun-bow", MightFree = "pe-might-of-the-sun-free", Fushicho = "pe-summon-fushicho-search", Glimpse = "pe-glimpse-of-fate-draw",
        Adroitness = "pe-elemental-adroitness-straighten", FallenBow = "pe-fallen-bow", CatLook = "pe-it-was-a-cat-look", SinkholeTerrain = "pe-sinkhole-terrain", SinkholeRegion = "pe-sinkhole-region",
        TremblingEquip = "pe-trembling-earth-equip", TremblingBow = "pe-trembling-earth-bow";

    /// <summary>Target categories: a Unique Personality (either player's); a Ring in play (the out-of-play "ring-in-play-or-own-discard" listing, narrowed to play by validation).</summary>
    public const string UniquePersonality = "pe-unique-personality";

    /// <summary>The Effect key of Might of the Sun's static "+4F".</summary>
    public const string MightEffect = "pe-might-of-the-sun";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorFate(this Dictionary<string, AbilityDefinition> all)
    {
        // Legendary Rivalry (oracle-l5r-9991): "Battle/Open: Target a Unique Personality: Bow him."
        all[LegendaryRivalry] = new("BattleOrOpen", "Target a Unique Personality: Bow him", UniquePersonality);
        // Tamori Potion (oracle-l5r-7788): "Battle: Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit."
        all[TamoriPotion] = new("Battle", "Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit", "attack",
            Performer: "own-unbowed-personality-at-battle", Costs: ActionCost.BowPerformer);
        // Bar Fight (oracle-l5r-696): "Battle: Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has." (Its Focus Effect is A Simple Yari's.)
        all[BarFightAttack] = new("Battle", "Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has", "attack", Performer: "own-unbowed-personality-at-battle");
        // Might of the Sun (oracle-l5r-5046): "Battle: Bow this Shugenja unless he is Fire or Thunder and target an enemy card without attachments: Destroy it." (Its "+4F" is its Effect.)
        all[MightBow] = new("Battle", "Bow this Shugenja and target an enemy card without attachments: Destroy it", "enemy-card-without-attachments", Costs: ActionCost.BowWielder, UsageKey: "pe-might-of-the-sun");
        all[MightFree] = new("Battle", "He is Fire or Thunder: Target an enemy card without attachments: Destroy it", "enemy-card-without-attachments", UsageKey: "pe-might-of-the-sun");
        // Summon Fushicho (oracle-l5r-10414): "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for an Item, show it, and put it in your hand."
        all[Fushicho] = new("Limited", "Bow this Shugenja and destroy this card: Search your Fate deck for an Item, show it, and put it in your hand", "none",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)], Search: SearchCatalog.ImperialGift);
        // A Glimpse of Fate (oracle-l5r-10597): "Battle/Open: You may discard a card. Draw cards until you have exactly two cards in your hand." The optional discard is a hand slot.
        all[Glimpse] = new("BattleOrOpen", "You may discard a card. Draw cards until you have exactly two cards in your hand", "slots",
            Slots: [new("discard", "hand", "A card in your hand to discard (optional)", 0, 1)]);
        // Elemental Adroitness (oracle-l5r-10600): "Battle: Straighten a target Ring. Before this phase ends, put this Strategy into your hand."
        all[Adroitness] = new("Battle", "Straighten a target Ring. Before this phase ends, put this Strategy into your hand", "ring-in-play-or-own-discard");
        // Fallen (oracle-l5r-10598): "Battle/Open: Bow a target Fallen card." (Its Kharmic reminder text is the universal Kharmic ability, not an ability of the card.)
        all[FallenBow] = new("BattleOrOpen", "Bow a target Fallen card", "card-with-any-required-keyword", RequiredKeywords: ["Fallen"]);
        // It Was a Cat (oracle-l5r-10415): "Open: Bow your performing Personality or Follower: Look at the top three cards of a deck, or four cards if the performer is a Ninja. You may discard one of them.
        // Put the rest back in any order. Lose 1 Honor." (The card is itself a Ninja, so its Open action is a Ninja Open action by inheritance.)
        all[CatLook] = new("Open", "Bow your performing Personality or Follower: Look at the top three cards of a deck, or four cards if the performer is a Ninja. You may discard one of them. Put the rest back in any order. Lose 1 Honor", "none",
            Performer: "own-unbowed-personality-or-follower", Costs: ActionCost.BowPerformer, Look: ItWasACat);
        // Sinkhole (oracle-l5r-9986): "Battle: Destroy any player's Terrain or Region at the current battlefield and target a Personality: His path is blocked. Bow him or move him home." Two entries sharing one
        // use (which of the two the battlefield holds); the acting player chooses bow or move home (the conditional effect choice with nothing to test).
        all[SinkholeTerrain] = new("Battle", "Destroy the Terrain at the current battlefield and target a Personality: His path is blocked. Bow him or move him home", "personality", UsageKey: "pe-sinkhole", Branch: SinkholeBranch);
        all[SinkholeRegion] = new("Battle", "Destroy the Region at the current battlefield and target a Personality: His path is blocked. Bow him or move him home", "personality", UsageKey: "pe-sinkhole", Branch: SinkholeBranch);
        // Trembling Earth (oracle-l5r-10595): "You may Equip this Spell as a Battle action to your Earth Shugenja at any location." and "Home Earth Battle: Bow a target enemy card. Destroy this Spell."
        all[TremblingEquip] = new("Battle", "Equip this Spell as a Battle action to your Earth Shugenja at any location", "none", Performer: EmperorP5Abilities.OwnPersonalityWithKeywordAnywhere, RequiredKeywords: ["Earth", "Shugenja"],
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Self, Wielder: AttachWielder.Performer));
        all[TremblingBow] = new("Battle", "Home Earth Battle: Bow a target enemy card. Destroy this Spell", "enemy-card", ActionKeywords: ["Earth"], SourceUnitMayBeAway: true);
        return all;
    }

    private static readonly ConditionalChoice SinkholeBranch = new(BranchCondition.Always,
        [new("bow", "Bow him", [BranchEffect.BowTarget]), new("home", "Move him home", [BranchEffect.MoveTargetHome])], new("none", "Nothing happens", []));

    /// <summary>It Was a Cat: the top three cards of any of the four decks, or four; the viewer may discard one and arranges the rest.</summary>
    public static readonly LookSpec ItWasACat = new("pe-it-was-a-cat", "Look at the top three cards of a deck",
        [new("Fate"), new("Dynasty"), new("Fate", Opponent: true), new("Dynasty", Opponent: true)], 3, [new(LookDisposition.Discard, 0, 1)], Reorder: true);
}

public sealed partial class MatchEngine
{
    private PromoEmperorFateState PromoFate => _state.PromoFate;
    private static readonly string[] PromoElements = ["Air", "Earth", "Fire", "Water", "Void"];

    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------------------------

    private bool PromoFateTargetAllows(string category, CardInstance target) => category switch
    {
        PromoEmperorFateAbilities.UniquePersonality => Definition(target).Type == CardType.Personality && Keyword(target, "Unique"),
        _ => false
    };

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidatePromoFate(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // The wielder's keyword decides which of the two entries is the legal one.
            case PromoEmperorFateAbilities.MightBow or PromoEmperorFateAbilities.MightFree:
                {
                    Require(source is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out _), "This Spell must be attached to a Shugenja.");
                    var wielder = Card(source!.AttachedTo);
                    var elemental = Keyword(wielder, "Fire") || Keyword(wielder, "Thunder");
                    Require(id == PromoEmperorFateAbilities.MightFree ? elemental : !elemental, elemental ? "This Shugenja is Fire or Thunder, so he does not bow." : "This Shugenja is neither Fire nor Thunder: he bows.");
                    break;
                }
            // "Target a Ring": only one in play (the listing category also names the player's own Rings in the Fate discard pile).
            case PromoEmperorFateAbilities.Adroitness when command.TargetId is { } ringId && _state.Cards.TryGetValue(ringId, out var ring):
                Require(Definition(ring).Type == CardType.Ring && InPlay(ring), "Target a Ring in play.");
                break;
            // The discard is the player's choice among the other cards in hand: never the Strategy being played.
            // Sinkhole: the Terrain or the Region the entry destroys must be at the current battlefield (a Terrain leaves when its battle ends, so any Terrain in play is at it).
            case PromoEmperorFateAbilities.SinkholeTerrain:
                Require(PromoTerrainAtBattle() is not null, "There is no Terrain at the current battlefield.");
                break;
            case PromoEmperorFateAbilities.SinkholeRegion:
                Require(PromoRegionAtBattle() is not null, "There is no Region at the current battlefield.");
                break;
            // Home Earth Battle: the Spell's Shugenja is at home or at the battlefield (a Home action may be taken from home).
            case PromoEmperorFateAbilities.TremblingBow:
                Require(source is { Zone: Zone.Attached, AttachedTo: { } spellHostId } && _state.Cards.TryGetValue(spellHostId, out var spellHost) && (spellHost.Zone == Zone.Home || AtBattle(spellHost)), "This Spell's Shugenja must be at home or at the battlefield.");
                break;
            case PromoEmperorFateAbilities.Glimpse when ability.Slots is not null:
                Require(SlotCards(command, "discard").All(card => card.Id != command.CardId), "A Strategy being played cannot be discarded to itself.");
                break;
        }
    }

    // ---- attacks ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int? PromoFateAttackStrength(string abilityId, string? performerId)
    {
        switch (abilityId)
        {
            case PromoEmperorFateAbilities.BarFightAttack:
                return 4 + (performerId is not null && _state.Cards.TryGetValue(performerId, out var fighter) ? TokenCount(fighter, "Sake") : 0);
            case PromoEmperorFateAbilities.TamoriPotion:
                {
                    if (performerId is null || !_state.Cards.TryGetValue(performerId, out var chemist)) return 4;
                    var elements = UnitMembers(chemist).SelectMany(member => PromoElements.Where(element => Keyword(member, element))).Distinct(StringComparer.Ordinal).Count();
                    return 4 + (Keyword(chemist, "Alchemist") ? 1 : 0) + elements;
                }
            default:
                return null;
        }
    }

    private static string? PromoFateAttackKind(string abilityId) => abilityId switch
    {
        PromoEmperorFateAbilities.TamoriPotion => "Ranged",
        PromoEmperorFateAbilities.BarFightAttack => "Melee",
        _ => null
    };

    // ---- Force ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Might of the Sun: "This Shugenja has +4F." A static trait of the Spell while it is attached.
    private int PromoMightForce(CardInstance card) =>
        Definition(card).Type == CardType.Personality ? 4 * Attachments(card.Id).Count(a => Definition(a).Type == CardType.Spell && Definition(a).Effect == PromoEmperorFateAbilities.MightEffect) : 0;

    // ---- Sinkhole's obstacles ------------------------------------------------------------------------------------------------------------------------------------------------

    private CardInstance? PromoTerrainAtBattle() =>
        _state.CurrentBattleProvince is int battle ? _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == battle && Keyword(c, "Terrain")).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() : null;

    private CardInstance? PromoRegionAtBattle() =>
        CurrentBattlefieldProvince() is { RegionId: { } regionId } && _state.Cards.TryGetValue(regionId, out var region) && region.Zone == Zone.Region ? region : null;

    // ---- phase end ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Elemental Adroitness: "Before this phase ends, put this Strategy into your hand." Called by SetPhase just before the phase changes; the whole Attack-through-battle stretch is one phase.
    private void PromoPhaseEnds(Phase next)
    {
        if (next == Phase.End && _state.Phase != Phase.End) PromoEndPhaseBegins(); // Tales of Valor (PromoEmperorEvents.cs)
        if (PromoFate.PhaseReturns.Count == 0 || PhaseFamily(next) == PhaseFamily(_state.Phase)) return;
        foreach (var due in PromoFate.PhaseReturns.Where(r => r.Family == PhaseFamily(_state.Phase)).ToArray())
        {
            PromoFate.PhaseReturns.Remove(due);
            if (!_state.Cards.TryGetValue(due.CardId, out var strategy) || strategy.Zone != Zone.FateDiscard) continue; // it has left the discard pile some other way
            var owner = Player(due.PlayerId);
            strategy.Zone = Zone.Hand; strategy.FaceUp = true; owner.Hand.Add(strategy.Id); ResetCardState(strategy);
            NoteCardIntoHand(owner.Id);
            Emit("returnedToHand", $"{Definition(strategy).Name} returns to {owner.Name}'s hand before the phase ends.", owner.Id, strategy.Id);
        }
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this feature's. false: the effects are done. true: a window opened (a deck search).
    private bool? RunPromoFate(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, HashSet<string> bowedBeforeAction)
    {
        switch (id)
        {
            case PromoEmperorFateAbilities.LegendaryRivalry:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case PromoEmperorFateAbilities.TamoriPotion:
            case PromoEmperorFateAbilities.BarFightAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case PromoEmperorFateAbilities.MightBow:
            case PromoEmperorFateAbilities.MightFree:
                DestroyTargetCard(target!, player.Id);
                return false;
            case PromoEmperorFateAbilities.Fushicho:
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case PromoEmperorFateAbilities.Glimpse:
                {
                    // "You may discard a card": the chosen one (an effect, so it is skipped when the action is negated). Then draw until exactly two cards are held (the Strategy in play is not one).
                    foreach (var discard in SlotCards(command, "discard").ToArray()) DiscardFate(player, discard);
                    var held = player.Hand.Count(h => h != source!.Id);
                    for (var i = held; i < 2; i++) Draw(player);
                    Emit("glimpseOfFate", $"{player.Name} holds {player.Hand.Count(h => h != source!.Id)} card(s) in hand.", player.Id, source!.Id);
                    return false;
                }
            case PromoEmperorFateAbilities.FallenBow:
                if (!IsTerrainCard(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case PromoEmperorFateAbilities.CatLook:
                {
                    var performer = Card(command.PerformerId!);
                    GainHonor(player.Id, -1, "It Was a Cat", source);
                    SpendHandPlayedCard(player, source); // a Strategy is spent as it is played: the look finishes the action
                    BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction, Keyword(performer, "Ninja") ? 4 : 3);
                    return true;
                }
            case PromoEmperorFateAbilities.SinkholeTerrain:
            case PromoEmperorFateAbilities.SinkholeRegion:
                {
                    // The obstacle is destroyed first (the Terrain through DiscardTerrain, the Region through DestroyRegion), then the acting player chooses bow or move home for the Personality.
                    if (id == PromoEmperorFateAbilities.SinkholeTerrain && PromoTerrainAtBattle() is { } terrain) { DiscardTerrain(terrain); player.TerrainDestroyedThisBattle = true; }
                    else if (id == PromoEmperorFateAbilities.SinkholeRegion && PromoRegionAtBattle() is { } region) DestroyRegion(region);
                    return BeginConditionalChoice(command, ability, consumedGrant, false, null, bowedBeforeAction, []);
                }
            case PromoEmperorFateAbilities.TremblingBow:
                if (!IsTerrainCard(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                return false;
            case PromoEmperorFateAbilities.Adroitness:
                Straighten(target!);
                if (source is not null && (PlayedFromHand(source) || source.Zone == Zone.FateDiscard))
                    PromoFate.PhaseReturns.Add(new PromoPhaseReturn(source.Id, player.Id, PhaseFamily(_state.Phase)));
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorFateHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorFate", 12100, static abilities => abilities.AddPromoEmperorFate().AddPromoEmperorFateReactions().AddPromoEmperorFateRecruits());
        EngineHooks.Looks.Register("PromoEmperorFateLooks", 12100, static () => [PromoEmperorFateAbilities.ItWasACat]);
        EngineHooks.Searches.Register("PromoEmperorFateSearches", 12100, static () => [PeaceSearch]);
        EngineHooks.TargetAllows.Register("PromoEmperorFateTargetAllows", 12100, static (engine, context) => engine.PromoFateTargetAllows(context.ability.Target, context.target)
            || engine.PromoReactionTargetAllows(context.ability.Target, context.target));
        EngineHooks.ValidateLate.Register("ValidatePromoEmperorFate", 12100, static (engine, context) =>
        {
            engine.ValidatePromoFate(context.id, context.ability, context.command, context.player, context.source);
            engine.ValidatePromoRecruit(context.id, context.ability, context.command, context.player, context.source);
            if (context.id is PromoEmperorFateAbilities.ClosingToBattle or PromoEmperorFateAbilities.ClosingHome) engine.ValidatePromoClosing(context.id, context.command, context.player);
        });
        EngineHooks.RunEffects.Register("RunPromoEmperorFate", 12100, static (engine, context) =>
            engine.RunPromoFate(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.bowedBeforeAction)
            ?? engine.RunPromoReactions(context.id, context.ability, context.command, context.player, context.source, context.target)
            ?? engine.RunPromoRecruits(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard,
                context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.AttackStrengthAfter.Register("PromoEmperorFateAttackStrength", 12100, static (engine, context) => engine.PromoFateAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("PromoEmperorFateAttackKind", 12100, static context => PromoFateAttackKind(context.abilityId));
        EngineHooks.PersonalityForce.Register("PromoEmperorFateForce", 12100, static (engine, context) => engine.PromoMightForce(context.card));
        // Interrogation reads these curated lists: set-fields-holding bows its Holding; brawl-destroy and set-fields-terrain destroy their Region / Terrain.
        EngineHooks.BowsTarget.Register("PromoEmperorFateBowsTarget", 12100, static context => context.abilityId is PromoEmperorFateAbilities.LegendaryRivalry or PromoEmperorFateAbilities.FallenBow
            or PromoEmperorFateAbilities.TremblingBow or PromoEmperorFateAbilities.SinkholeTerrain or PromoEmperorFateAbilities.SinkholeRegion or "set-fields-holding");
        EngineHooks.DestroysTarget.Register("PromoEmperorFateDestroysTarget", 12100, static context => context.abilityId is PromoEmperorFateAbilities.MightBow or PromoEmperorFateAbilities.MightFree
            or "brawl-destroy" or "set-fields-terrain");
        EngineHooks.TargetedReaction.Register("PromoEmperorFateTargetedReaction", 12100, static (engine, context) =>
            context.abilityId is PromoEmperorFateAbilities.WellProtectedBow or PromoEmperorFateAbilities.WellProtectedDestroy && engine.PromoWellProtectedApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.DeciderPrompt.Register("PromoEmperorFateDeciderPrompt", 12100, static (engine, p) => engine.PromoCharterPrompt(p));
        EngineHooks.DeciderOptions.Register("PromoEmperorFateDeciderOptions", 12100, static (engine, specId, deciderId, command) => engine.PromoCharterOptions(specId, command));
        EngineHooks.ApplyDecider.Register("PromoEmperorFateApplyDecider", 12100, static (engine, p) => engine.PromoApplyCharter(p));
        EngineHooks.DeciderIdBefore.Register("PromoEmperorFateDeciderId", 12100, static (engine, specId, command) => specId == PromoEmperorFateAbilities.CharterFirst ? command.PlayerId : null);
        EngineHooks.DeciderVerb.Register("PromoEmperorFateDeciderVerb", 12100, static (engine, specId) => PromoEmperorFateAbilities.IsCharterSpec(specId) ? "Create the Holding" : null);
    }
}
