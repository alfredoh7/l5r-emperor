using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #835: twelve cards Mendon's rulings of 2026-10-11 (third batch) unblocked (docs/decisions/2026-10-11-twelve-ruled-cards.md; rulings R-063..R-074 in docs/rules/RULINGS.md). Ability ids are "w835-".
//
//   A Cleansing Breath       "Battle/Open: Target your Personality at any location. He centers himself. Negate other players' cards' effects that prevent him assigning, moving, or performing actions. If he is
//                            a Kensai, straighten his unit."   "He centers himself." is flavor text and does nothing. A Prevention immunity for the turn; the Kensai's unit straightens.
//   Imperial Deployment      "Open: Choose your performing Personality: ... Negate other players' cards' effects that prevent him assigning. After the next time this turn an attack is declared, straighten
//                            his unit."   An AssignPrevention immunity (assigning only: movement stays barred) and a mandatory straighten when the attack is declared this turn.
//   Warded Premises          "Before this battle's resolution: Negate all Force bonuses from actions to cards at this battlefield. / Battle: Even if you control no units at the current battlefield, if there
//                            is no Terrain in play there: Put this card into play there."   Only the bonuses actions gave (not permanent ones, attachments' or tokens').
//   Duty Over All Things     "Battle, :gstar:: If he would be opposed, Recruit your target performing discarded Yojimbo, paying 3 less Gold if you control a Courtier or Shugenja."   A discarded (not dead)
//                            Yojimbo, recruited at the current battlefield when an enemy Personality stands there.
//   Pierce the Tapestry      "You may include up to seven copies of this card in your deck. / Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Give it -3F. If it has no
//                            attachments and you have played two or more other Pierce the Tapestry this battle, destroy it. If this did not destroy it, you may take an additional Battle action to play a
//                            Pierce the Tapestry."   A per-player, per-battle count of plays and an additional action scoped to the ability. Oracle of the Void prints the name "Pierce the Tapestery".
public static class Wave835Abilities
{
    public const string CleansingBreath = "w835-cleansing-breath", ImperialDeployment = "w835-imperial-deployment", WardedNegate = "w835-warded-negate", WardedPlay = "w835-warded-play",
        DutyDiscount = "w835-duty-discount", DutyFull = "w835-duty-full", Pierce = "w835-pierce";
    public const string YojimboDiscard = "w835-discarded-yojimbo";
    public const string CleansingBreathCard = "oracle-l5r-10358", ImperialDeploymentCard = "oracle-l5r-10378", WardedPremisesCard = "oracle-l5r-10404", DutyCard = "oracle-l5r-10370", PierceCard = "oracle-l5r-10388";

    /// <summary>The Gold Duty Over All Things takes off the recruit when its player controls a Courtier or a Shugenja.</summary>
    public const int DutyDiscountAmount = 3;
    /// <summary>"two or more other Pierce the Tapestry played this battle".</summary>
    public const int PierceOthersNeeded = 2;

    public static bool IsTerrainPlayAbility(string abilityId) => abilityId == WardedPlay;

    public static Dictionary<string, AbilityDefinition> AddWave835(this Dictionary<string, AbilityDefinition> all)
    {
        all[CleansingBreath] = new("BattleOrOpen", "Battle/Open: Target your Personality at any location. Negate other players' cards' effects that prevent him assigning, moving, or performing actions. If he is a Kensai, straighten his unit",
            Wave736Abilities.OwnPersonalityAnyLocation);
        all[ImperialDeployment] = new("Open", "Open: Choose your performing Personality: Negate other players' cards' effects that prevent him assigning. After the next time this turn an attack is declared, straighten his unit",
            "none", Performer: "own-personality");
        all[WardedNegate] = new("BeforeResolution", "Before this battle's resolution: Negate all Force bonuses from actions to cards at this battlefield", "none", Trigger: "before-battle-resolves");
        all[WardedPlay] = new("Battle", "Battle: Even if you control no units at the current battlefield, if there is no Terrain in play there: Put this card into play there", "none", Absent: true);
        all[DutyDiscount] = new("Battle", "Battle: If he would be opposed, Recruit your target performing discarded Yojimbo, paying 3 less Gold if you control a Courtier or Shugenja", YojimboDiscard,
            Cost: GoldCostRule.TargetRecruit(-DutyDiscountAmount, floor: 0));
        all[DutyFull] = new("Battle", "Battle: If he would be opposed, Recruit your target performing discarded Yojimbo", YojimboDiscard, Cost: GoldCostRule.TargetRecruit());
        all[Pierce] = new("Battle", "Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Give it -3F. If it has no attachments and you have played two or more other Pierce the Tapestry this battle, destroy it. If this did not destroy it, you may take an additional Battle action to play a Pierce the Tapestry",
            "enemy-personality-or-follower", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"]);
        return all;
    }
}

/// <summary>An Imperial Deployment waiting for the next attack declared this turn: his unit then straightens.</summary>
public sealed class DeploymentWatch
{
    public string EventId { get; set; } = "";
    public string PersonalityId { get; set; } = "";
    public int PlayerId { get; set; }
    public int Turn { get; set; }
}

public sealed class Wave835State
{
    public List<DeploymentWatch> Deployments { get; set; } = [];
}

public sealed partial class MatchState
{
    // 23600-23699 is reserved for issue #835.
    [JsonPropertyOrder(23600)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Wave835State? Wave835 { get; set; }
}

public sealed partial class MatchEngine
{
    private Wave835State W835 => _state.Wave835 ??= new();

    // ---- targets ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Your discarded (not dead) Yojimbo": a Personality of the player's in his Dynasty or Fate discard pile (Zone.Dead is the dead pile: Dark Virtue's At Any Cost takes those).
    private bool Wave835YojimboCandidate(int playerId, CardInstance c) =>
        c.OwnerId == playerId && Definition(c).Type == CardType.Personality && Keyword(c, "Yojimbo") && c.Zone is Zone.DynastyDiscard or Zone.FateDiscard;

    internal static bool IsWave835OutOfPlayTarget(string category) => category == Wave835Abilities.YojimboDiscard;

    private string?[]? Wave835Candidates(string category, int playerId) => category != Wave835Abilities.YojimboDiscard ? null
        : _state.Cards.Values.Where(c => Wave835YojimboCandidate(playerId, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private bool ControlsCourtierOrShugenja(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && (Keyword(c, "Courtier") || Keyword(c, "Shugenja")));

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave835Validate(ValidationHookContext context)
    {
        var (id, command, player) = (context.id, context.command, context.player);
        switch (id)
        {
            // "If he would be opposed": an enemy Personality stands at the battlefield the Yojimbo would enter (Mendon: judged when the card is played).
            case Wave835Abilities.DutyDiscount:
            case Wave835Abilities.DutyFull:
                Require(_state.CurrentBattleProvince is int battlefield && _state.Cards.Values.Any(c => c.OwnerId != player.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Definition(c).Type == CardType.Personality),
                    "No enemy Personality is at the current battlefield, so he would not be opposed.");
                // The same printed action, in two prices: three less Gold when the player controls a Courtier or Shugenja, the plain recruit otherwise.
                Require((id == Wave835Abilities.DutyDiscount) == ControlsCourtierOrShugenja(player.Id), id == Wave835Abilities.DutyDiscount ? "You control no Courtier or Shugenja: the recruit is not discounted." : "You control a Courtier or Shugenja: the recruit is three Gold cheaper.");
                if (command.TargetId is { } yojimboId && _state.Cards.TryGetValue(yojimboId, out var yojimbo))
                {
                    Require(Wave835YojimboCandidate(player.Id, yojimbo), "Choose a Yojimbo Personality of yours in your discard pile (not dead).");
                    var quote = QuotePurchase(player.Id, yojimbo.Id, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                }
                break;
            // Warded Premises' second ability: "if there is no Terrain in play there", from the hand.
            case Wave835Abilities.WardedPlay:
                Require(context.source is { Zone: Zone.Hand }, "Warded Premises is put into play from your hand.");
                Require(!TerrainAtCurrentBattlefield() || MarshallingFieldsWaiverAvailable(player.Id), "There is already a Terrain in play at the current battlefield.");
                break;
        }
    }

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave835(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            // "He centers himself." is flavor text (Mendon). The negation is a Prevention immunity until the end of the turn; "If he is a Kensai, straighten his unit."
            case Wave835Abilities.CleansingBreath:
                GrantImmunity(target!, ImmunityKind.Prevention);
                Emit("immunityGranted", $"{Definition(target!).Name}: other players' cards' effects that prevent him assigning, moving or performing actions are negated this turn.", player.Id, target!.Id);
                if (Keyword(target!, "Kensai")) StraightenUnit(target!);
                return false;
            // Only the effects that stop him assigning are negated (he may still be barred from moving); the straightening is a mandatory watch fired when an attack is declared this turn.
            case Wave835Abilities.ImperialDeployment:
                {
                    var deployed = Card(command.PerformerId!);
                    GrantImmunity(deployed, ImmunityKind.AssignPrevention);
                    W835.Deployments.RemoveAll(w => w.Turn != _state.TurnNumber);
                    W835.Deployments.Add(new DeploymentWatch { EventId = source!.Id, PersonalityId = deployed.Id, PlayerId = player.Id, Turn = _state.TurnNumber });
                    Emit("immunityGranted", $"{Definition(deployed).Name}: other players' cards' effects that prevent him assigning are negated this turn; his unit straightens after the next attack is declared.", player.Id, deployed.Id);
                    return false;
                }
            case Wave835Abilities.WardedPlay:
                if (TerrainAtCurrentBattlefield()) SpendMarshallingFieldsWaiver(player.Id, source!);
                PutTerrainIntoPlay(player, source!);
                return false;
            case Wave835Abilities.DutyDiscount:
            case Wave835Abilities.DutyFull:
                {
                    var yojimbo = target!;
                    var battlefield = _state.CurrentBattleProvince!.Value;
                    var quote = QuotePurchase(player.Id, yojimbo.Id, fromDeadPile: true);
                    Require(quote.Error is null && Definition(yojimbo).Type == CardType.Personality, quote.Error ?? "");
                    SpendHandPlayedCard(player, source);
                    return BringDeadPersonalityIntoPlay(player, yojimbo, Definition(source!).Name, battlefield);
                }
            case Wave835Abilities.Pierce:
                {
                    // "you have played two or more OTHER Pierce the Tapestry this battle": the count of this player's plays before this one.
                    var key = "w835-pierce:" + player.Id;
                    var others = PeriodCountOf(key, Period.Battle);
                    AddPeriodCount(key, Period.Battle);
                    ApplyModifier(target!, source!.Id, id, "Force", -3);
                    var destroyed = false;
                    if (InPlay(target!) && others >= Wave835Abilities.PierceOthersNeeded && !Attachments(target!.Id).Any())
                    {
                        if (Definition(target).Type == CardType.Follower) DestroyAttachment(target);
                        else DestroyUnits([target], false, player.Id);
                        destroyed = !InPlay(target);
                    }
                    if (!destroyed) GrantAdditionalAction(player.Id, null, scopeAbilityIds: [Wave835Abilities.Pierce]);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Imperial Deployment: the mandatory straightening ----------------------------------------------------------------------------------------------------------------------------

    // Run when an attack is declared (the normal declaration and both additional Attack Phases): "After the next time this turn an attack is declared, straighten his unit." Mandatory, and refused only
    // by what refuses any straightening -- an effect that prevents his next straightening, or says he will not straighten (Straighten's own gate). Spent by the declaration whether it straightens or not.
    private void Wave835AttackDeclared()
    {
        if (_state.Wave835 is not { Deployments.Count: > 0 } w) return;
        foreach (var watch in w.Deployments.Where(d => d.Turn == _state.TurnNumber).ToArray())
        {
            w.Deployments.Remove(watch);
            if (!_state.Cards.TryGetValue(watch.PersonalityId, out var deployed) || !InPlay(deployed)) continue;
            Emit("deploymentStraightens", $"{Definition(deployed).Name}'s unit straightens: an attack was declared after Imperial Deployment.", watch.PlayerId, deployed.Id);
            StraightenBowedUnit(deployed);
        }
        w.Deployments.RemoveAll(d => d.Turn != _state.TurnNumber);
    }

    // ---- Warded Premises: the before-resolution trait -----------------------------------------------------------------------------------------------------------------------------

    // "Negate all Force bonuses from actions to cards at this battlefield." Mendon: only the bonuses actions gave -- a Permanent bonus, a bonus from an attachment and a bonus from a token stay, and so does a
    // bonus from a card or token that an action attached. The bonuses that are Modifiers of an action ability, and not permanent, are removed.
    private void Wave835WardedNegate(CardInstance terrain)
    {
        var battlefield = terrain.ProvinceIndex;
        var removed = 0;
        foreach (var card in _state.Cards.Values.Where(c => c.ProvinceIndex == battlefield && (c.Zone == Zone.Battlefield || c.Zone == Zone.Attached)).ToArray())
            removed += card.Modifiers.RemoveAll(m => m.Stat == "Force" && m.Amount > 0 && !m.Permanent && AbilityCatalog.All.TryGetValue(m.Effect, out var giving) && IsAction(giving));
        Emit("forceBonusesNegated", $"{Definition(terrain).Name}: the Force bonuses from actions to cards at this battlefield are negated ({removed} removed).", terrain.OwnerId, terrain.Id);
    }

    // ---- registration -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave835Hooks()
    {
        EngineHooks.Abilities.Register("Wave835Abilities", 13835, static abilities => abilities.AddWave835());
        EngineHooks.ValidateEarly.Register("Wave835Validate", 13835, static (engine, context) => engine.Wave835Validate(context));
        EngineHooks.RunEffects.Register("Wave835RunEffects", 13835, static (engine, context) => engine.RunWave835(context));
    }
}
