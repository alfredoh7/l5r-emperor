using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #674, CLUSTER "ITEMS-MISC" (item-wave-48): Divination Bowl, Kage's Teachings, Khalimpeh-jiak, Koan's Staff, Parangu, Renyu's Wrath, Sturdy Armor and Translated Story of Shaharazad,
// wired from the printed text of their scans (docs/decisions/2026-10-06-674-items-misc.md lists the readings). The cluster's Followers are WlItemsMiscFollowers.cs, its Events
// WlItemsMiscEvents.cs and its Celestials WlItemsMiscCelestials.cs; the state they share is WlItemsMiscState below.
//
//   DIVINATION BOWL        two Limited actions that bow it: the look is Public Records' (LookCatalog.PublicRecords: the top three of your own Fate deck, back in any order), the other draws a card
//                          and destroys the Item.
//   KAGE'S TEACHINGS       "This Personality has Kolat" is AttachGrantsKeyword; "Open: If it is not your turn, bow this card and choose a player: He chooses and bows one of his unbowed Personalities"
//                          is a decider choice (DeciderChoice.cs): the chosen player decides, among his unbowed Personalities in play.
//   KHALIMPEH-JIAK         "Engage, bow: Give a target enemy Personality or Follower -2F": an Engage is a Reaction to "after engaging" (the Coils of Madness supplement).
//   KOAN'S STAFF           a Ranged Attack whose strength is 3 plus one for each other Koan card (the card's own keyword) in the wielder's unit.
//   PARANGU                only Kharmic's reminder text: the universal Kharmic action (CardAbilities, "kharmic-discard") is its rule.
//   RENYU'S WRATH          the destroy is Brawl's (a Region at any location, or a Terrain); the Ranged 4 Attack gets +1 strength while its Personality is Siege or Earth.
//   STURDY ARMOR           "Three times per turn" is UsesPerTurn 3 on a Battle action that gives the wielder a +1F modifier (until the turn ends, R-001).
//   SHAHARAZAD             a +1F token on the Item, which conveys to its wielder like any Item's Force modifier (RemAItemForceModifiers).
// ======================================================================================================================================================================================

public static class WlItemsMiscItemAbilities
{
    public const string BowlLook = "wl-bowl-look", BowlDraw = "wl-bowl-draw", KageBow = "wl-kage-bow", KhalimpehPenalty = "wl-khalimpeh-penalty", KoanAttack = "wl-koan-attack",
        RenyuDestroy = "wl-renyu-destroy", RenyuRanged = "wl-renyu-ranged", SturdyBoost = "wl-sturdy-boost", ShaharazadToken = "wl-shaharazad-token";
    public const string KoansStaff = "oracle-l5r-10527";

    public static Dictionary<string, AbilityDefinition> AddWlItemsMiscItems(this Dictionary<string, AbilityDefinition> all)
    {
        // Divination Bowl (oracle-l5r-10690): "Limited, bow: Look at the top three cards of your Fate deck. Put them back in any order." / "Limited, bow: Draw a card. Destroy this Item."
        all[BowlLook] = new("Limited", "Bow this card: Look at the top three cards of your Fate deck. Put them back in any order", "none", Costs: ActionCost.BowSource, Look: LookCatalog.PublicRecords);
        all[BowlDraw] = new("Limited", "Bow this card: Draw a card. Destroy this Item", "none", Costs: ActionCost.BowSource);
        // Kage's Teachings (oracle-l5r-10341): "Open: If it is not your turn, bow this card and choose a player: He chooses and bows one of his unbowed Personalities."
        all[KageBow] = new("Open", "If it is not your turn, bow this card and choose a player: He chooses and bows one of his unbowed Personalities", "player", Costs: ActionCost.BowSource);
        // Khalimpeh-jiak (oracle-l5r-10905): "Engage, bow: Give a target enemy Personality or Follower -2F."
        all[KhalimpehPenalty] = new("Reaction", "Engage, bow this card: Give a target enemy Personality or Follower -2F", "enemy-force", Trigger: "after-engaging", Costs: ActionCost.BowSource);
        // Koan's Staff (oracle-l5r-10527): "Fire Battle, bow: Ranged 3 Attack with +1 strength for each other Koan card in this unit."
        all[KoanAttack] = new("Battle", "Fire Battle, bow this card: Ranged 3 Attack with +1 strength for each other Koan card in this unit", "attack", ActionKeywords: ["Fire"], Costs: ActionCost.BowSource);
        // Renyu's Wrath (oracle-l5r-10343): "Battle: Bow this card and target a Region (at any location) or a Terrain: Destroy it." / "Battle: Bow this card: Ranged 4 Attack, with +1 strength if this Personality is Siege or Earth."
        all[RenyuDestroy] = new("Battle", "Bow this card and target a Region (at any location) or a Terrain: Destroy it", "region-or-terrain", Costs: ActionCost.BowSource);
        all[RenyuRanged] = new("Battle", "Bow this card: Ranged 4 Attack, with +1 strength if this Personality is Siege or Earth", "attack", Costs: ActionCost.BowSource);
        // Sturdy Armor (oracle-l5r-10344): "Battle: Three times per turn: Give this Personality +1F."
        all[SturdyBoost] = new("Battle", "Three times per turn: Give this Personality +1F", "none", UsesPerTurn: 3);
        // Translated Story of Shaharazad (oracle-l5r-10698): "Limited, bow: Give this Item a +1F token."
        all[ShaharazadToken] = new("Limited", "Bow this card: Give this Item a +1F token", "none", Costs: ActionCost.BowSource);
        return all;
    }
}

/// <summary>What the "items-misc" cluster keeps between commands: Maddened Horde's last discarded Focus Value (the Attack's strength) and Hachiman's Prowess's grants.</summary>
public sealed class WlItemsMiscState
{
    /// <summary>The Focus Value of the card Maddened Horde last discarded: the strength of the Melee Attack that follows (read while the Attack is aimed and resolved).</summary>
    public int HordeFocus { get; set; }
    /// <summary>Hachiman's Prowess: the Personality each Celestial gave Elite and "May assign even if bowed", for as long as that Celestial remains in play.</summary>
    public List<WlHachimanGrant> HachimanGrants { get; set; } = [];
}

/// <summary>One Hachiman's Prowess grant: <paramref name="TargetId"/> has Elite and may assign even if bowed while <paramref name="CelestialId"/> remains in play.</summary>
public sealed record WlHachimanGrant(string CelestialId, string TargetId);

public sealed partial class MatchState
{
    // A nondecision record: nothing here is a pending choice.
    [JsonPropertyOrder(21100)]
    public WlItemsMiscState WlItemsMisc { get; set; } = new();
}

public static class WlItemsMiscSpecs
{
    /// <summary>Kage's Teachings' decider choice: "He chooses and bows one of his unbowed Personalities."</summary>
    public const string KageBow = "wl-kage-bow";
}

public sealed partial class MatchEngine
{
    private WlItemsMiscState WlState => _state.WlItemsMisc;

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlItemsMiscItems(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If it is not your turn".
            case WlItemsMiscItemAbilities.KageBow:
                Require(_state.ActivePlayerId != player.Id, "Kage's Teachings may be used only when it is not your turn.");
                break;
        }
    }

    // ---- Koan's Staff and Renyu's Wrath: attack strengths ---------------------------------------------------------------------------------------------------------------------------

    private int? WlItemsMiscAttackStrength(string abilityId, string? performerId)
    {
        switch (abilityId)
        {
            // "Ranged 3 Attack with +1 strength for each other Koan card in this unit": the Staff is itself a Koan card, so it is the one card not counted.
            case WlItemsMiscItemAbilities.KoanAttack:
                {
                    if (performerId is null || !_state.Cards.TryGetValue(performerId, out var wielder) || Definition(wielder).Type != CardType.Personality) return 3;
                    return 3 + UnitMembers(wielder).Count(c => Keyword(c, "Koan") && Definition(c).Id != WlItemsMiscItemAbilities.KoansStaff);
                }
            // "+1 strength if this Personality is Siege or Earth": the wielder's keyword, not the Item's own.
            case WlItemsMiscItemAbilities.RenyuRanged:
                {
                    var bonus = performerId is not null && _state.Cards.TryGetValue(performerId, out var wielder) && Definition(wielder).Type == CardType.Personality && (Keyword(wielder, "Siege") || Keyword(wielder, "Earth"));
                    return 4 + (bonus ? 1 : 0);
                }
            default: return null;
        }
    }

    private static string? WlItemsMiscAttackKind(string abilityId) => abilityId switch
    {
        WlItemsMiscItemAbilities.KoanAttack or WlItemsMiscItemAbilities.RenyuRanged => "Ranged",
        WlItemsMiscFollowerAbilities.HordeOwn or WlItemsMiscFollowerAbilities.HordeEnemy => "Melee",
        _ => null
    };

    // ---- Kage's Teachings: the decider choice ----------------------------------------------------------------------------------------------------------------------------------------

    private static bool WlItemsMiscIsDeciderSpec(string specId) => specId == WlItemsMiscSpecs.KageBow;

    private (List<string> Candidates, int Min, int Max)? WlItemsMiscDeciderOptions(string specId, int deciderId) =>
        specId != WlItemsMiscSpecs.KageBow ? null
            : (_state.Cards.Values.Where(c => c.OwnerId == deciderId && InPlay(c) && !c.Bowed && Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);

    private string? WlItemsMiscDeciderPrompt(PendingDeciderChoice p) => p.SpecId == WlItemsMiscSpecs.KageBow
        ? $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your unbowed Personalities. It is bowed." : null;

    private bool WlItemsMiscApplyDecider(PendingDeciderChoice p)
    {
        if (p.SpecId != WlItemsMiscSpecs.KageBow) return false;
        foreach (var id in p.Chosen)
        {
            var card = Card(id);
            if (!StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
        }
        return true;
    }

    // ---- execution --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this cluster's Item. false: the effects are done. true: a window opened (a look, an Attack's reaction window, a decider's choice).
    private bool? RunWlItemsMiscItems(RunHookContext context)
    {
        var (id, ability, command, player, source, target, consumedGrant) = (context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant);
        switch (id)
        {
            case WlItemsMiscItemAbilities.BowlLook:
                BeginLook(ability.Look!, source!, player.Id, "ability", context.bowedBeforeAction); // the look finishes the action (DeckLook.cs)
                return true;
            case WlItemsMiscItemAbilities.BowlDraw:
                Draw(player);
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                return false;
            case WlItemsMiscItemAbilities.KageBow:
                return BeginDeciderChoice(WlItemsMiscSpecs.KageBow, command, consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants);
            case WlItemsMiscItemAbilities.KhalimpehPenalty:
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                return false;
            case WlItemsMiscItemAbilities.KoanAttack:
            case WlItemsMiscItemAbilities.RenyuRanged:
                return ResolveOrDeferAttack(id, player, command.TargetId, ActionPerformer(ability, command, source).Id, consumedGrant);
            // Brawl's removal: a Region leaves its province; a Terrain is discarded (and counts for "destroyed a Terrain this battle").
            case WlItemsMiscItemAbilities.RenyuDestroy:
                if (target!.Zone == Zone.Region) DestroyRegion(target);
                else { DiscardTerrain(target); player.TerrainDestroyedThisBattle = true; }
                return false;
            case WlItemsMiscItemAbilities.SturdyBoost:
                if (WielderOf(source!) is { } wielder) ApplyModifier(wielder, source!.Id, id, "Force", 1);
                return false;
            case WlItemsMiscItemAbilities.ShaharazadToken:
                CreateToken(source!, "Force", id, "Force", 1);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlItemsMiscItemHooks()
    {
        EngineHooks.Abilities.Register("AddWlItemsMiscItems", 15100, static abilities => abilities.AddWlItemsMiscItems());
        EngineHooks.ValidateLate.Register("ValidateWlItemsMiscItems", 15100, static (engine, context) => engine.ValidateWlItemsMiscItems(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunWlItemsMiscItems", 15100, static (engine, context) => engine.RunWlItemsMiscItems(context));
        EngineHooks.AttackStrengthAfter.Register("WlItemsMiscAttackStrength", 15100, static (engine, context) => engine.WlItemsMiscAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("WlItemsMiscAttackKind", 15100, static context => WlItemsMiscAttackKind(context.abilityId));
        EngineHooks.DeciderOptions.Register("WlItemsMiscDeciderOptions", 15100, static (engine, specId, deciderId, command) => WlItemsMiscIsDeciderSpec(specId) ? engine.WlItemsMiscDeciderOptions(specId, deciderId) : null);
        EngineHooks.DeciderPrompt.Register("WlItemsMiscDeciderPrompt", 15100, static (engine, pending) => engine.WlItemsMiscDeciderPrompt(pending));
        EngineHooks.DeciderVerb.Register("WlItemsMiscDeciderVerb", 15100, static (engine, specId) => WlItemsMiscIsDeciderSpec(specId) ? "Bow" : null);
        EngineHooks.DeciderIdBefore.Register("WlItemsMiscDeciderIdBefore", 15100, static (engine, specId, command) => WlItemsMiscIsDeciderSpec(specId) ? command.TargetPlayerId ?? command.PlayerId : null);
        EngineHooks.ApplyDecider.Register("WlItemsMiscApplyDecider", 15100, static (engine, pending) => engine.WlItemsMiscApplyDecider(pending));
    }
}
