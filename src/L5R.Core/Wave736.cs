using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #736, wave batch 5: the card-restriction and immunity cards, wired on the merged P11 (card restrictions and target bars) and P12 (immunity ledger) primitives, with P21 (refill hook), P3-era search
// placement and the Event keep-in-play machinery for the rest. Every legality and every negation is decided in Core. Ability ids are "w736-". Readings for Mendon to confirm or correct are in
// docs/decisions/2026-10-08-wave-batch-5-card-restrictions-and-immunities.md. Waves are those of tools/content/package_wave_736.py.
//
//   Susumu Haikaro            "Negate Haikaro's destruction from battle resolution."                                    standing DestroyInResolution immunity (Self)
//   The Earth's Armor         "While this Shugenja is defending, other players' cards will not bow him."                standing Bow immunity on the Spell's host (new scope AttachedHost), while he defends
//   Thunder Bow               "Negate other players' cards' effects that prevent this Personality performing actions." / "Battle/Open: Target your Personality at any location: Transfer this card to him."   Prevention immunity on the host; w736-bow-transfer
//   Path to Inner Peace       "Battle/Open: Bow this card and target a Personality: Negate all Force penalties on him and other players' cards' effects that prevent him performing actions."   w736-path-peace
//   Harbor of Kalani's Landing "Negate Personalities' movement to or from this battlefield." / "Battle: ... Put this card into play there. You may take an additional Battle action."   standing MovementAtBattlefield; w736-harbor-play
//   Oracle of the Void - exp  "Negate this card's destruction." / "Limited, bow: The next two cards in provinces you refill (this turn) may be refilled face-up." / "Limited, bow: Draw a card."   standing Destroy immunity; w736-oracle-refill, w736-oracle-draw
//   P'an Ku's Madness         "Limited: Give a target card a Madness token." / "Open: Discard this Celestial. A target Personality with a Madness token cannot attack or defend."   w736-madness-token, w736-madness-bar
//   Rampant Paranoia          "Limited: Until your next turn begins, other players' cards will not destroy your Personalities in a defending army."   an Event kept in play; standing Destroy immunity (Event carrier)
//   Ninube Onchi - exp        "After Onchi enters play, lose 2 Honor." / "Actions from cards in other players' units may not target Onchi."   EntryHonorLoss; a static target bar
//   Otomo Suikihime - exp     "After Suikihime enters play, lose 3 Honor." / "Other players' Political actions may not target Suikihime. Suikihime may not attack." / the Fallen search   EntryHonorLoss; a static bar and NoAttack; w736-suikihime-search
//   Kamalakar's Harem         "This Personality may not attack." / "Limited, bow: Dishonor a target Human Personality. Destroy this Follower."   a live NoAttack on the host; w736-harem-dishonor
//   Map of the Northern Colonies "Other players' Terrains will not destroy your Terrains at this Item's battlefield."   standing TerrainDestroy immunity
//   Tankoji                   "Battle, bow: Actions from cards in a target Personality's unit may not target cards in this unit."   w736-tankoji-bar (a P11 target bar on the wielder's unit)
//   Yoritomo Naoto - exp      "Battle: Target an enemy Personality: Actions he or his Followers perform must target Naoto, if legal. You may take an additional Battle action."   w736-naoto-taunt (a P11 forced rule)
//
// Not wired, with a blocker note (tools/content/pe_wave_736_blockers.json; Isawa Ryuzo and Hida Kaiji - exp were wired in Wave821.cs, issue #821, once Mendon ruled the army-assignment primitive): A Cleansing Breath ("centers himself" has no definition in the repository), Asako Kaitoko - exp (a maximum Force: no stat cap exists),
// P'an Ku - exp (control with a timed return and a seppuku bar), Koan's Jingasa (a Spell limit by
// attachment count and a cost waiver on the host's Spells) and Bonds of Hospitality (a straighten bar tied to a card in play and a granted discard).
public static class Wave736Abilities
{
    public const string BowTransfer = "w736-bow-transfer", PathPeace = "w736-path-peace", HarborPlay = "w736-harbor-play", OracleRefill = "w736-oracle-refill", OracleDraw = "w736-oracle-draw",
        MadnessToken = "w736-madness-token", MadnessBar = "w736-madness-bar", SuikihimeSearch = "w736-suikihime-search", HaremDishonor = "w736-harem-dishonor", TankojiBar = "w736-tankoji-bar",
        NaotoTaunt = "w736-naoto-taunt";

    // Target categories of this file (TargetAllows).
    public const string OwnPersonalityAnyLocation = "w736-own-personality-any-location", HumanPersonality = "w736-human-personality", MadnessPersonality = "w736-personality-with-madness";

    // The cards (oracle ids).
    public const string Haikaro = "oracle-l5r-10664", EarthsArmor = "oracle-l5r-10922", ThunderBow = "oracle-l5r-10345", PathToInnerPeace = "oracle-l5r-10351", Harbor = "oracle-l5r-10376",
        Oracle = "oracle-l5r-10408", PanKusMadness = "oracle-l5r-10418", RampantParanoia = "oracle-l5r-10421", Onchi = "oracle-l5r-10662", Suikihime = "oracle-l5r-10496", Harem = "oracle-l5r-10683",
        MapOfColonies = "oracle-l5r-10694", Tankoji = "oracle-l5r-10909", Naoto = "oracle-l5r-10300";

    public const string RampantParanoiaEffect = "w736-rampant-paranoia";

    /// <summary>The target categories that are exempt from "a Battle target must be at the current battlefield" (their own location rule is part of the category).</summary>
    public static bool IsAnyLocationTarget(string category) => category == OwnPersonalityAnyLocation;

    /// <summary>The Terrain whose own ability puts it into play (never the generic playStrategy Terrain rule): Harbor of Kalani's Landing's Battle action also grants an additional Battle action.</summary>
    public static bool IsTerrainPlayAbility(string abilityId) => abilityId == HarborPlay;

    // "Search your Dynasty deck for a Fallen Personality. Put him into one of your Provinces face-up, discarding any card currently there."
    public static readonly SearchSpec FallenSearch = new("w736-fallen-personality", "Search your Dynasty deck for a Fallen Personality and put him into one of your Provinces face-up",
        [new SearchDeckOption(SearchDeck.Dynasty, new SearchCriteria("Fallen Personality", Types: [CardType.Personality], Keywords: ["Fallen"]), SearchEffect.ToProvinceFaceUp)]);

    public static Dictionary<string, AbilityDefinition> AddWave736(this Dictionary<string, AbilityDefinition> all)
    {
        all[BowTransfer] = new("BattleOrOpen", "Battle/Open: Target your Personality at any location: Transfer this card to him", OwnPersonalityAnyLocation);
        all[PathPeace] = new("BattleOrOpen", "Battle/Open, bow this card: Target a Personality: Negate all Force penalties on him and other players' cards' effects that prevent him performing actions", "personality",
            Costs: ActionCost.BowSource);
        all[HarborPlay] = new("Battle", "Battle: If there is no Terrain in play at the current battlefield: Put this card into play there. You may take an additional Battle action", "none");
        all[OracleRefill] = new("Limited", "Limited, bow this card: The next two cards in provinces you refill (this turn) may be refilled face-up", "none", Costs: ActionCost.BowSource);
        all[OracleDraw] = new("Limited", "Limited, bow this card: Draw a card", "none", Costs: ActionCost.BowSource);
        all[MadnessToken] = new("Limited", "Limited: Give a target card a Madness token", "any-card");
        all[MadnessBar] = new("Open", "Open: Discard this Celestial. A target Personality with a Madness token cannot attack or defend", MadnessPersonality);
        all[SuikihimeSearch] = new("Limited", "Limited, bow this card: Search your Dynasty deck for a Fallen Personality. Put him into one of your Provinces face-up, discarding any card currently there", "none",
            Costs: ActionCost.BowSource, Search: FallenSearch);
        all[HaremDishonor] = new("Limited", "Limited, bow this card: Dishonor a target Human Personality. Destroy this Follower", HumanPersonality, Costs: ActionCost.BowSource);
        all[TankojiBar] = new("Battle", "Battle, bow this card: Actions from cards in a target Personality's unit may not target cards in this unit", "personality", Costs: ActionCost.BowSource);
        all[NaotoTaunt] = new("Battle", "Battle: Target an enemy Personality: Actions he or his Followers perform must target Naoto, if legal. You may take an additional Battle action", "enemy-personality");
        return all;
    }
}

public sealed partial class MatchState
{
    // 22920-22929 is reserved for batch 5 of the wirable-card waves (issue #736). How many more cards each player's Oracle of the Void lets him refill face-up this turn ("player:turn" to the count),
    // omitted until it is used.
    [JsonPropertyOrder(22920)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? OracleFaceUpRefills { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly string[] Wave736HumanExclusions = ["Nonhuman"];

    /// <summary>The Personality an attached card is on, or null.</summary>
    private CardInstance? Wave736Host(CardInstance attachment) =>
        attachment.Zone == Zone.Attached && attachment.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : null;

    private bool Wave736IsHuman(CardInstance card) => Definition(card).Type == CardType.Personality && !Keyword(card, "Nonhuman");

    // ---- target legality ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave736TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            case Wave736Abilities.OwnPersonalityAnyLocation:
                return InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId == context.player.Id;
            case Wave736Abilities.HumanPersonality:
                return InPlay(target) && Wave736IsHuman(target);
            case Wave736Abilities.MadnessPersonality:
                return InPlay(target) && Definition(target).Type == CardType.Personality && HasToken(target, "Madness");
            default:
                return false;
        }
    }

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave736Validate(ValidationHookContext context)
    {
        switch (context.id)
        {
            // "Transfer this card to him": the attaching restrictions are part of what makes the target legal, checked as the action is announced (as Field Messenger's).
            case Wave736Abilities.BowTransfer when context.source is { } bow && context.command.TargetId is { } hostId:
                Require(CanTransferTo(bow, Card(hostId)), $"{Definition(bow).Name} cannot be transferred to {Definition(Card(hostId)).Name}.");
                break;
            case Wave736Abilities.HarborPlay:
                Require(context.source is { Zone: Zone.Hand }, "Harbor of Kalani's Landing is put into play from your hand.");
                Require(!TerrainAtCurrentBattlefield() || MarshallingFieldsWaiverAvailable(context.player.Id), "There is already a Terrain in play at the current battlefield.");
                break;
        }
    }

    // ---- effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave736(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave736Abilities.BowTransfer:
                TransferAttachment(source!, target!, Definition(source!).Name);
                return false;
            case Wave736Abilities.PathPeace:
                // "Negate all Force penalties on him" (every penalty in force now, and none new this turn) "and other players' cards' effects that prevent him performing actions" (this turn).
                NegateForcePenalties(target!);
                GrantImmunity(target!, ImmunityKind.Prevention);
                return false;
            case Wave736Abilities.HarborPlay:
                if (TerrainAtCurrentBattlefield()) SpendMarshallingFieldsWaiver(player.Id, source!);
                PutTerrainIntoPlay(player, source!);
                GrantAdditionalAction(player.Id, null);
                return false;
            case Wave736Abilities.OracleRefill:
                (_state.OracleFaceUpRefills ??= [])[$"{player.Id}:{_state.TurnNumber}"] = 2;
                Emit("refillFaceUpGranted", $"{player.Name}: the next two cards in provinces refilled this turn are refilled face-up.", player.Id, source!.Id);
                return false;
            case Wave736Abilities.OracleDraw:
                Draw(player);
                return false;
            case Wave736Abilities.MadnessToken:
                CreateToken(target!, "Madness", id);
                return false;
            case Wave736Abilities.MadnessBar:
                // The Celestial discards itself, then the target "cannot attack or defend" for the turn (the text names no longer duration).
                source!.Zone = Zone.DynastyDiscard; source.FaceUp = true;
                Emit("celestialDiscarded", $"{Definition(source).Name} was discarded.", source.OwnerId, source.Id);
                AddCardRestriction(RestrictionKind.NoAttack, target!, note: Definition(source).Name);
                AddCardRestriction(RestrictionKind.NoDefend, target!, note: Definition(source).Name);
                return false;
            case Wave736Abilities.SuikihimeSearch:
                BeginSearch(c.ability.Search!, source!, player.Id, "ability", c.bowedBeforeAction);
                return true;
            case Wave736Abilities.HaremDishonor:
                Dishonor(target!, player.Id);
                if (source is not null && InPlay(source)) DestroyAttachment(source);
                return false;
            case Wave736Abilities.TankojiBar:
                {
                    // "Actions from cards in a target Personality's unit may not target cards in this unit": this unit is the wielder's (or the Weapon's own host's) unit.
                    var host = Wave736Host(source!);
                    if (host is null) return false;
                    AddTargetBarRule(host, new ActorSpec(UnitOfCardId: target!.Id, UnitCardsOnly: true), targetUnit: true, note: Definition(source!).Name);
                    return false;
                }
            case Wave736Abilities.NaotoTaunt:
                // "Actions he or his Followers perform must target Naoto, if legal", for the turn; then "you may take an additional Battle action".
                AddTargetBarRule(source!, new ActorSpec(UnitOfCardId: target!.Id, UnitCardsOnly: true), forced: true, note: Definition(source!).Name);
                GrantAdditionalAction(player.Id, null);
                return false;
            default:
                return null;
        }
    }

    // ---- live restrictions: Kamalakar's Harem's wielder may not attack ----------------------------------------------------------------------------------------------------------------

    private IEnumerable<StaticRestriction> Wave736Restrictions(CardInstance card)
    {
        if (Definition(card).Type == CardType.Personality && Attachments(card.Id).Any(a => a.DefinitionId == Wave736Abilities.Harem)) yield return new(RestrictionKind.NoAttack);
    }

    // ---- refill: Oracle of the Void ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? Wave736RefillFaceUp(RefillContext context)
    {
        var key = $"{context.Player.Id}:{_state.TurnNumber}";
        if (_state.OracleFaceUpRefills is not { } counts || !counts.TryGetValue(key, out var left) || left <= 0) return null;
        if (left == 1) counts.Remove(key); else counts[key] = left - 1;
        if (counts.Count == 0) _state.OracleFaceUpRefills = null;
        return true;
    }

    // ---- Events: Rampant Paranoia ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave736Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave736Abilities.RampantParanoiaEffect) return null;
        KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: other players' cards will not destroy {player.Name}'s Personalities in a defending army.", turnBeginsIn: 1);
        return false;
    }

    // ---- conditions of the standing immunities ---------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave736ArmorApplies(CardInstance spell) => Wave736Host(spell) is { } host && IsDefendingUnit(host);

    private bool Wave736ParanoiaApplies(ImmunityContext context) =>
        context.Card is { } personality && Definition(personality).Type == CardType.Personality && IsDefendingUnit(personality);

    // "Other players' Terrains will not destroy your Terrains at this Item's battlefield": the Terrain is at the battlefield of the Personality the Item is on, and the effect is another player's Terrain's.
    private bool Wave736MapApplies(ImmunityContext context, CardInstance map) =>
        Wave736Host(map) is { Zone: Zone.Battlefield, ProvinceIndex: int battlefield } && context.Card is { Zone: Zone.Battlefield } terrain && terrain.ProvinceIndex == battlefield
        && context.SourceCard is { } source && source.OwnerId != map.OwnerId;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave736Hooks()
    {
        EngineHooks.Abilities.Register("Wave736Abilities", 13600, static abilities => abilities.AddWave736());
        EngineHooks.Searches.Register("Wave736Searches", 13600, static () => [Wave736Abilities.FallenSearch]);
        EngineHooks.TargetAllows.Register("Wave736TargetAllows", 13600, static (engine, context) => engine.Wave736TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave736Validate", 13600, static (engine, context) => engine.Wave736Validate(context));
        EngineHooks.RunEffects.Register("Wave736RunEffects", 13600, static (engine, context) => engine.RunWave736(context));
        EngineHooks.CardRestrictions.Register("Wave736Restrictions", 13600, static (engine, card) => engine.Wave736Restrictions(card));
        EngineHooks.RefillFaceUp.Register("Wave736RefillFaceUp", 13600, static context => context.Engine.Wave736RefillFaceUp(context));
        EngineHooks.ResolveEvent.Register("Wave736ResolveEvent", 13600, static (engine, context) => engine.ResolveWave736Event(context.Player, context.Province, context.Card));

        // Static restrictions and target bars (P11), by card.
        CardRestrictionCatalog.RegisterTargetBars(Wave736Abilities.Onchi, new StaticTargetBar(new ActorSpec("others", UnitCardsOnly: true)));
        CardRestrictionCatalog.RegisterTargetBars(Wave736Abilities.Suikihime, new StaticTargetBar(new ActorSpec("others", ActionKeyword: "Political")));
        CardRestrictionCatalog.Register(Wave736Abilities.Suikihime, new StaticRestriction(RestrictionKind.NoAttack));

        // Standing immunities (P12).
        EngineHooks.StandingImmunities.Register("Wave736Haikaro", 13600, static () => new StandingImmunity(
            Id: "w736-haikaro", OwnerIs: static d => d.Id == Wave736Abilities.Haikaro, Kind: ImmunityKind.DestroyInResolution));
        EngineHooks.StandingImmunities.Register("Wave736EarthsArmor", 13600, static () => new StandingImmunity(
            Id: "w736-earths-armor", OwnerIs: static d => d.Id == Wave736Abilities.EarthsArmor, Kind: ImmunityKind.Bow, Scope: ImmunityScope.AttachedHost,
            Condition: static (engine, _, carrier) => engine.Wave736ArmorApplies(carrier)));
        EngineHooks.StandingImmunities.Register("Wave736ThunderBow", 13600, static () => new StandingImmunity(
            Id: "w736-thunder-bow", OwnerIs: static d => d.Id == Wave736Abilities.ThunderBow, Kind: ImmunityKind.Prevention, Scope: ImmunityScope.AttachedHost));
        EngineHooks.StandingImmunities.Register("Wave736Harbor", 13600, static () => new StandingImmunity(
            Id: "w736-harbor", OwnerIs: static d => d.Id == Wave736Abilities.Harbor, Kind: ImmunityKind.MovementAtBattlefield, Scope: ImmunityScope.Battlefield, Source: ImmunitySource.Any));
        EngineHooks.StandingImmunities.Register("Wave736Oracle", 13600, static () => new StandingImmunity(
            Id: "w736-oracle", OwnerIs: static d => d.Id == Wave736Abilities.Oracle, Kind: ImmunityKind.Destroy, Source: ImmunitySource.Any));
        EngineHooks.StandingImmunities.Register("Wave736RampantParanoia", 13600, static () => new StandingImmunity(
            Id: "w736-rampant-paranoia", OwnerIs: static d => d.Effect == Wave736Abilities.RampantParanoiaEffect, Kind: ImmunityKind.Destroy, Scope: ImmunityScope.OwnersCards, TargetType: CardType.Personality,
            Condition: static (engine, context, _) => engine.Wave736ParanoiaApplies(context), EventCarrier: true));
        EngineHooks.StandingImmunities.Register("Wave736MapOfColonies", 13600, static () => new StandingImmunity(
            Id: "w736-map-of-colonies", OwnerIs: static d => d.Id == Wave736Abilities.MapOfColonies, Kind: ImmunityKind.TerrainDestroy, Scope: ImmunityScope.OwnersCards, TargetKeyword: "Terrain",
            Source: ImmunitySource.OtherTerrains, Condition: static (engine, context, carrier) => engine.Wave736MapApplies(context, carrier)));
    }
}
