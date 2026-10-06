using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PERMANENT REMOVAL OF A CARD'S TRAITS AND ABILITIES (issue #667, the "removal" cluster of the Promotional-Emperor blocked cards): Jade Embrace (spell-wave-53), Sea & Sky (spell-wave-53) and
// the removal half of Through the Breach (strategy-wave-115, blocked on the Breached Oni scan; the primitive and its ability are built and tested).
//
// Mendon's ruling (issue #667): "Probably easiest to apply a per-turn block that has an arbitrarily large number of turns ... but, if this doesn't already exist, then removal would be a temporary
// removal of the ability (if the card leaves play and returns, this ability would reset like other abilities)." The engine had only the per-turn lock (MatchState.DisabledAbilities) and the whole-card
// removal of Unsettling Doubts (EmperorP5State.AbilitiesRemoved). This is the general primitive both of Mendon's alternatives ask for, one record per (card, key):
//
//   * KEY = the UsageKey of one ability (one printed ability split over several catalogue entries is removed whole, as DisableAbility's), TraitKey (the card's Effect, its static/triggered traits),
//     or AllKey (every ability and trait: Sea & Sky). A removal holds exactly while the card stays in play: the sweep after every command (SweepRemovedAbilities) drops the records of a card that
//     left play, so a card that returns has all of its printed text again, "like other abilities".
//   * HOW IT TAKES EFFECT: the card's per-instance printed definition (CardInstance.Printed, read by Definition everywhere) is rewritten without the removed abilities / with Effect "none", so every
//     reader -- QueryActions, reactions, static traits -- sees the card without them; AbilityIdsOf additionally filters granted and static-granted abilities by the same keys. The baseline (the
//     printed Effect and Abilities at the first removal) is kept so the sweep can give them back. A card's Keywords are not traits and stay (core rules, Reading the Cards).
//   * THE CHOICE: "Remove one of his abilities" / "one of its non-Gold-producing traits or abilities" is an engine-listed effectChoice (the decision Doji Hakuseki's lock uses, which the client and the
//     bots already answer); one candidate is applied without asking, none does nothing. Sea & Sky's "Either destroy it or permanently remove its traits and abilities" is the same decision with two
//     fixed options.
//
// READINGS (Mendon to confirm; none is a confirmed ruling): "his abilities" (Jade Embrace) are the Personality's own abilities of every timing, printed or granted, never his keywords or the abilities of
// his attachments; Gold production is a Holding's printed production and Gold-source abilities (Through the Breach leaves it); a removal is a card effect on a card in play and is not negated by anything;
// restoring the baseline on leaving play overwrites a later permanent copy (PermanentCopy) the card made while its abilities were removed.
// ======================================================================================================================================================================================

/// <summary>One key of a card whose abilities or traits were permanently removed while it stays in play.</summary>
public sealed record RemovedAbility(string CardId, string Key);

/// <summary>The printed Effect and Abilities a card had when its first removal was applied, given back when it leaves play.</summary>
public sealed record RemovedBaseline(string CardId, string Effect, string[]? Abilities);

/// <summary>What the removal primitive keeps between commands.</summary>
public sealed class AbilityRemovalState
{
    public List<RemovedAbility> Entries { get; set; } = [];
    public List<RemovedBaseline> Baselines { get; set; } = [];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(1773)]
    public AbilityRemovalState Removal { get; set; } = new();
}

public static class AbilityRemovalAbilities
{
    public const string JadeEmbrace = "pe-jade-embrace-remove", SeaAndSky = "pe-sea-and-sky-remove", ThroughTheBreach = "pe-through-the-breach-remove", BreachCreate = "pe-through-the-breach-create";

    /// <summary>Removal keys: every ability and trait of the card; the card's Effect (its traits).</summary>
    public const string AllKey = "*", TraitKey = "trait";

    /// <summary>Target category: a Holding in play with the Castle keyword (any player's).</summary>
    public const string CastleHolding = "pe-castle-holding";

    public static Dictionary<string, AbilityDefinition> AddAbilityRemoval(this Dictionary<string, AbilityDefinition> all)
    {
        // Jade Embrace (oracle-l5r-9985): "Battle: Bow this card and target an enemy Personality: Remove one of his abilities. If this Shugenja is Earth, reduce the target's Force to 0. If the target is
        // an Oni, destroy it and this card."
        all[JadeEmbrace] = new("Battle", "Bow this card and target an enemy Personality: Remove one of his abilities. If this Shugenja is Earth, reduce the target's Force to 0. If the target is an Oni, destroy it and this card",
            "enemy-personality", Costs: ActionCost.BowSource);
        // Sea & Sky (oracle-l5r-9988): "Battle/Open: Destroy this card and target a Region or Terrain: Either destroy it or permanently remove its traits and abilities."
        all[SeaAndSky] = new("BattleOrOpen", "Destroy this card and target a Region or Terrain: Either destroy it or permanently remove its traits and abilities",
            "region-or-terrain", Costs: [new(CostVerbs.Sacrifice)]);
        // Through the Breach (oracle-l5r-9992), first Limited: "Target a Castle Holding: Permanently remove one of its non-Gold-producing traits or abilities."
        all[ThroughTheBreach] = new("Limited", "Target a Castle Holding: Permanently remove one of its non-Gold-producing traits or abilities", CastleHolding);
        // Through the Breach, second Limited: "If you control a Personality, pay 4 Gold: Create a 4F/2C/0PH Shadowlands - Nonhuman - Oni Personality. Lose 3 Honor." (Breached Oni, user-scan-breached-oni.)
        all[BreachCreate] = new("Limited", "If you control a Personality, pay 4 Gold: Create a 4F/2C/0PH Shadowlands - Nonhuman - Oni Personality. Lose 3 Honor", "none", Cost: GoldCostRule.Fixed(4),
            Creates: new(CardType.Personality, Force: 4, Chi: 2, PersonalHonor: 0, Keywords: ["Shadowlands", "Nonhuman", "Oni"], ProxyId: "user-scan-breached-oni"));
        return all;
    }
}

public sealed partial class MatchEngine
{
    private const string RemovalEffectPrefix = "ar:";

    private AbilityRemovalState Removal => _state.Removal;

    // ---- the record -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool AbilityKeyRemoved(CardInstance card, string abilityId)
    {
        if (Removal.Entries.Count == 0) return false;
        var key = AbilityUsageKey(abilityId);
        return Removal.Entries.Any(e => e.CardId == card.Id && (e.Key == AbilityRemovalAbilities.AllKey || e.Key == key)) && InPlay(card);
    }

    // AbilityIdsOf's hook: granted and static-granted abilities are filtered by the same keys as the printed ones.
    private IEnumerable<string> RemovedAbilitiesFilter(CardInstance card, IEnumerable<string> ids) =>
        Removal.Entries.Count == 0 ? ids : ids.Where(id => !AbilityKeyRemoved(card, id));

    private void RemoveCardAbility(CardInstance card, string key, string sourceName)
    {
        if (!InPlay(card)) return;
        if (!Removal.Baselines.Any(b => b.CardId == card.Id))
        {
            var printed = Definition(card);
            Removal.Baselines.Add(new(card.Id, printed.Effect, printed.Abilities?.ToArray()));
        }
        if (!Removal.Entries.Any(e => e.CardId == card.Id && e.Key == key)) Removal.Entries.Add(new(card.Id, key));
        ApplyRemovals(card);
        var what = key == AbilityRemovalAbilities.AllKey ? "all of its traits and abilities"
            : key == AbilityRemovalAbilities.TraitKey ? "its traits"
            : AbilityCatalog.All.Values.FirstOrDefault(a => a.UsageKey == key)?.Label is { } label ? $"the ability \"{label}\"" : "an ability";
        Emit("abilityRemoved", $"{sourceName}: {Definition(card).Name} permanently loses {what} (until it leaves play).", card.OwnerId, card.Id);
    }

    // Rewrites the card's per-instance definition from its baseline and the keys removed from it.
    private void ApplyRemovals(CardInstance card)
    {
        var baseline = Removal.Baselines.First(b => b.CardId == card.Id);
        var keys = Removal.Entries.Where(e => e.CardId == card.Id).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var all = keys.Contains(AbilityRemovalAbilities.AllKey);
        var abilities = (baseline.Abilities ?? []).Where(id => !all && !keys.Contains(AbilityUsageKey(id))).ToArray();
        var effect = all || keys.Contains(AbilityRemovalAbilities.TraitKey) ? "none" : baseline.Effect;
        card.Printed = Definition(card) with { Abilities = abilities, Effect = effect };
    }

    // After every command: a card that left play is a new object if it ever returns, with all of its printed abilities and traits.
    private void SweepRemovedAbilities()
    {
        if (Removal.Baselines.Count == 0 && Removal.Entries.Count == 0) return;
        foreach (var baseline in Removal.Baselines.ToArray())
        {
            if (_state.Cards.TryGetValue(baseline.CardId, out var card) && InPlay(card)) continue;
            Removal.Baselines.Remove(baseline);
            Removal.Entries.RemoveAll(e => e.CardId == baseline.CardId);
            if (card?.Printed is { } printed)
            {
                var restored = printed with { Abilities = baseline.Abilities, Effect = baseline.Effect };
                card.Printed = restored == CardCatalog.Get(card.DefinitionId) ? null : restored;
            }
        }
        Removal.Entries.RemoveAll(e => !Removal.Baselines.Any(b => b.CardId == e.CardId));
    }

    internal bool AbilityRemovedForTest(string cardId, string abilityId) => AbilityKeyRemoved(Card(cardId), abilityId);

    // ---- the choices ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsGoldSourceAbility(AbilityDefinition ability) => ability.PaymentProduction is not null || ability.PaymentDiscardsFocus;

    // "One of his abilities" (Jade Embrace) / "one of its non-Gold-producing traits or abilities" (Through the Breach): every ability of any timing, a UsageKey group counted once, and optionally the
    // card's Effect as its trait.
    private EffectOption[] RemovalOptions(CardInstance card, bool traits, bool excludeGold)
    {
        var name = Definition(card).Name;
        var options = new List<EffectOption>();
        foreach (var group in AbilityIdsOf(card).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && !(excludeGold && IsGoldSourceAbility(d))).GroupBy(AbilityUsageKey, StringComparer.Ordinal))
            options.Add(new(group.Key, $"{name}: remove \"{AbilityCatalog.All[group.First()].Label}\"", [RemovalEffectPrefix + "ability:" + group.Key]));
        var effect = Definition(card).Effect;
        if (traits && effect != "none" && !(excludeGold && IsGoldEffect(effect)))
            options.Add(new(AbilityRemovalAbilities.TraitKey, $"{name}: remove its trait ({effect})", [RemovalEffectPrefix + "trait"]));
        return options.ToArray();
    }

    // Nothing to remove: nothing happens; one candidate: applied without asking; otherwise the acting player chooses (the effectChoice decision).
    private bool BeginRemovalChoice(GameCommand command, EffectOption[] options, string prompt, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (options.Length == 0) return false;
        if (options.Length == 1)
        {
            foreach (var effect in options[0].Effects) ApplyAbilityRemovalBranchEffect(effect, command);
            return false;
        }
        return BeginListedEffectChoice(command, options, prompt, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    // ApplyBranchEffect's arm: the removal options' effects, applied to the command's target; false for any other effect.
    private bool ApplyAbilityRemovalBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(RemovalEffectPrefix, StringComparison.Ordinal)) return false;
        var kind = effect[RemovalEffectPrefix.Length..];
        if (command.TargetId is not { } targetId || !_state.Cards.TryGetValue(targetId, out var target)) return true;
        var sourceName = command.CardId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var source) ? Definition(source).Name : "An action";
        if (kind.StartsWith("ability:", StringComparison.Ordinal)) RemoveCardAbility(target, kind["ability:".Length..], sourceName);
        else if (kind == "trait") RemoveCardAbility(target, AbilityRemovalAbilities.TraitKey, sourceName);
        else if (kind == "all") RemoveCardAbility(target, AbilityRemovalAbilities.AllKey, sourceName);
        else if (kind == "destroy" && InPlay(target))
        {
            // A Region leaves its province; a Terrain shares Brawl's removal and Ring of Water's "destroyed a Terrain" condition.
            if (target.Zone == Zone.Region) DestroyRegion(target);
            else { DiscardTerrain(target); Player(command.PlayerId).TerrainDestroyedThisBattle = true; }
        }
        return true;
    }

    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool RemovalTargetAllows(string category, CardInstance target) => category switch
    {
        AbilityRemovalAbilities.CastleHolding => Definition(target).Type == CardType.Holding && InPlay(target) && Keyword(target, "Castle"),
        _ => false
    };

    // ---- execution ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this feature's. false: the effects are done. true: the action paused for the acting player's choice.
    private bool? RunAbilityRemoval(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case AbilityRemovalAbilities.JadeEmbrace:
                {
                    // "If this Shugenja is Earth, reduce the target's Force to 0." The Shugenja is the Spell's wielder.
                    if (source is { AttachedTo: { } wielderId } && _state.Cards.TryGetValue(wielderId, out var shugenja) && Keyword(shugenja, "Earth"))
                        ApplyModifier(target!, source.Id, id, "Force", -CardForce(target!));
                    // "If the target is an Oni, destroy it and this card." A destroyed Personality needs no ability removed.
                    if (Keyword(target!, "Oni"))
                    {
                        DestroyTargetCard(target!, player.Id);
                        if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                        return false;
                    }
                    return BeginRemovalChoice(command, RemovalOptions(target!, traits: false, excludeGold: false),
                        $"{Definition(source!).Name}: choose one of {Definition(target!).Name}'s abilities to remove.", consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case AbilityRemovalAbilities.SeaAndSky:
                {
                    var name = Definition(target!).Name;
                    EffectOption[] options =
                    [
                        new("destroy", $"Destroy {name}", [RemovalEffectPrefix + "destroy"]),
                        new("remove", $"Permanently remove {name}'s traits and abilities", [RemovalEffectPrefix + "all"])
                    ];
                    return BeginListedEffectChoice(command, options, $"{Definition(source!).Name}: either destroy {name} or permanently remove its traits and abilities.",
                        consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case AbilityRemovalAbilities.ThroughTheBreach:
                return BeginRemovalChoice(command, RemovalOptions(target!, traits: true, excludeGold: true),
                    $"{Definition(source!).Name}: choose one of {Definition(target!).Name}'s non-Gold-producing traits or abilities to remove.", consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case AbilityRemovalAbilities.BreachCreate:
                return ExecuteCreate(ability, ability.Creates!, command, player, () => GainHonor(player.Id, -3, Definition(source!).Name, source));
            default:
                return null;
        }
    }

    // "If you control a Personality": a Personality in play under the acting player's control.
    private void ValidateAbilityRemoval(string id, PlayerState player)
    {
        if (id == AbilityRemovalAbilities.BreachCreate)
            Require(_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality), "You control no Personality.");
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterAbilityRemovalHooks()
    {
        EngineHooks.Abilities.Register("AddAbilityRemoval", 12300, static abilities => abilities.AddAbilityRemoval());
        EngineHooks.TargetAllows.Register("AbilityRemovalTargetAllows", 12300, static (engine, context) => engine.RemovalTargetAllows(context.ability.Target, context.target));
        EngineHooks.ValidateLate.Register("ValidateAbilityRemoval", 12300, static (engine, context) => engine.ValidateAbilityRemoval(context.id, context.player));
        EngineHooks.RunEffects.Register("RunAbilityRemoval", 12300, static (engine, context) =>
            engine.RunAbilityRemoval(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard,
                context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
