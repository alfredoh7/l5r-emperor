namespace L5R.Core;

// Personality wave 121 (issue #622, "tf-personalities"): fifteen Personalities whose primary printing is Twenty Festivals, wired from their earliest Emperor-era scan and its PRINTED text.
// Keyword-only cards (Misora, Aibako, Iguchi, Sakiko), Nairuko (force-with-spell-2) and Yokohime (masumi-bow) reuse existing engine surface and are only packaged. This file carries the rest:
//   Bayushi Mifuyu   "Battle: Give a target enemy Personality -3F."                                       tfp-mifuyu-weaken
//   Daigotsu Ryuko   "Battle, Bow: Melee 2 Attack." (+ "After Ryuko enters play, lose 1 Honor." = EntryHonorLoss)   tfp-ryuko-melee
//   Toritaka Iabuchi "Battle, Bow: Ranged 2 Attack."                                                         tfp-iabuchi-ranged
//   Isawa Ikariya    "Fire Battle: Ranged Attack with strength equal to the number of Spells in this unit."   tfp-ikariya-attack ("Fire Battle" is a plain label, as Moshi Ikako's)
//   Kaiu Gorobei     "Battle: Discard a card to bow a target enemy card with Force equal to or lower than the discarded card's Focus Value."   tfp-gorobei-bow
//   Moto Paikao      "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value."  tfp-paikao-weaken
//   Lao-she          "Home Battle: If Lao-she would be opposed, move him to the current battlefield."         tfp-laoshe-move (Absent: a Home action may be taken from home)
//   Kakita Izumiko   "After Izumiko wins a duel, gain 1 Honor." -- a mandatory trait (no "Reaction:" label), read where the duel resolves (ApplyTfPersonalitiesDuelWin).
//   Soshi Kodanshi   "After Kodanshi enters play from a province: Create a 0F/2C/0PH Scorpion Clan - Ninja Personality." -- a mandatory trait (no "Reaction:" label), applied with the other
//                    mandatory entry effects; the Ninja is the published Created-only scan (oracle-l5r-10200) printed as Scorpion Clan, which its scan shows.
// The two discard-a-card cards name the discarded card in a cost-only "hand" slot (Isawa Koiso's shape) listed BEFORE the target, so the target's legality can read it.
public static class TfPersonalitiesAbilities
{
    public const string MifuyuWeaken = "tfp-mifuyu-weaken";
    public const string RyukoMelee = "tfp-ryuko-melee";
    public const string IabuchiRanged = "tfp-iabuchi-ranged";
    public const string IkariyaAttack = "tfp-ikariya-attack";
    public const string GorobeiBow = "tfp-gorobei-bow";
    public const string PaikaoWeaken = "tfp-paikao-weaken";
    public const string LaosheMove = "tfp-laoshe-move";
    public const string IzumikoEffect = "tfp-izumiko-duel-honor";
    public const string KodanshiEffect = "tfp-kodanshi-entry-create";
    /// <summary>An enemy Personality or Follower whose Force is at most the Focus Value of the card chosen in the "discard" slot.</summary>
    public const string EnemyForceLteDiscardFocus = "tfp-enemy-force-lte-discard-focus";
    public const string KodanshiNinjaProxy = "oracle-l5r-10200";

    public static Dictionary<string, AbilityDefinition> AddTfPersonalities(this Dictionary<string, AbilityDefinition> all)
    {
        all[MifuyuWeaken] = new("Battle", "Give a target enemy Personality -3F", "enemy-personality");
        all[RyukoMelee] = new("Battle", "Bow this card: Melee 2 Attack", "attack", Costs: ActionCost.BowSource);
        all[IabuchiRanged] = new("Battle", "Bow this card: Ranged 2 Attack", "attack", Costs: ActionCost.BowSource);
        all[IkariyaAttack] = new("Battle", "Fire Battle: Ranged Attack with strength equal to the number of Spells in this unit", "attack");
        all[GorobeiBow] = new("Battle", "Discard a card to bow a target enemy card with Force equal to or lower than the discarded card's Focus Value", "slots",
            Slots: [new("discard", "hand", "A card from your hand, to discard (its Focus Value limits the target's Force)") { CostOnly = true },
                new("target", EnemyForceLteDiscardFocus, "An enemy card with Force equal to or lower than the discarded card's Focus Value")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        all[PaikaoWeaken] = new("Battle", "Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value", "slots",
            Slots: [new("discard", "hand", "A card from your hand, to discard (its Focus Value is the Force penalty)") { CostOnly = true },
                new("target", "enemy-force", "An enemy Personality or Follower")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        all[LaosheMove] = new("Battle", "Home Battle: If Lao-she would be opposed, move him to the current battlefield", "none", Absent: true);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private static readonly CreatedCardSpec KodanshiNinja = new(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 0, Keywords: ["Ninja"], ProxyId: TfPersonalitiesAbilities.KodanshiNinjaProxy);

    // ---- target legality ----

    private bool TfPersonalitiesTargetAllows(string category, GameCommand command, PlayerState player, CardInstance target)
    {
        if (category != TfPersonalitiesAbilities.EnemyForceLteDiscardFocus) return false;
        // "an enemy card with Force": a Personality or Follower of another player. Until the discard is chosen the slot lists nothing (the discard slot is listed first).
        if (target.OwnerId == player.Id || Definition(target).Type is not (CardType.Personality or CardType.Follower)) return false;
        return SlotCards(command, "discard").FirstOrDefault() is { } discarded && CardForce(target) <= FocusValueOf(discarded);
    }

    private void ValidateTfPersonalities(string id, GameCommand command, PlayerState player)
    {
        // "If Lao-she would be opposed": the current battlefield holds an enemy unit for him to stand against.
        if (id == TfPersonalitiesAbilities.LaosheMove)
            Require(_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any(), "Lao-she would not be opposed: there are no enemy units at the current battlefield.");
    }

    // ---- effects ----

    private bool? RunTfPersonalities(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            case TfPersonalitiesAbilities.MifuyuWeaken:
                ApplyModifier(target!, source!.Id, id, "Force", -3);
                return false;
            case TfPersonalitiesAbilities.RyukoMelee or TfPersonalitiesAbilities.IabuchiRanged or TfPersonalitiesAbilities.IkariyaAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case TfPersonalitiesAbilities.GorobeiBow:
                // The discard was paid as a cost; the target is bowed unless something negates the bow.
                if (InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case TfPersonalitiesAbilities.PaikaoWeaken:
                // The penalty is the discarded card's Focus Value (the card is in the discard pile now, its definition unchanged).
                ApplyModifier(target!, source!.Id, id, "Force", -FocusValueOf(SlotCards(command, "discard").Single()));
                return false;
            case TfPersonalitiesAbilities.LaosheMove:
                // "Move him to the current battlefield": nothing to do if he already stands there.
                if (_state.CurrentBattleProvince is int battlefield && !(source!.Zone == Zone.Battlefield && source.ProvinceIndex == battlefield)) MoveToBattlefield(source, battlefield);
                return false;
            default:
                return null;
        }
    }

    private int? TfPersonalitiesAttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        TfPersonalitiesAbilities.RyukoMelee => 2,
        TfPersonalitiesAbilities.IabuchiRanged => 2,
        // "the number of Spells in this unit": Spells attach to the Personality, the unit's host.
        TfPersonalitiesAbilities.IkariyaAttack => sourceId is null ? 0 : Attachments(UnitHostId(Card(sourceId))).Count(c => Definition(c).Type == CardType.Spell),
        _ => null
    };

    private static string? TfPersonalitiesAttackKind(string abilityId) => abilityId switch
    {
        TfPersonalitiesAbilities.RyukoMelee => "Melee",
        TfPersonalitiesAbilities.IabuchiRanged or TfPersonalitiesAbilities.IkariyaAttack => "Ranged",
        _ => null
    };

    // ---- mandatory traits ----

    // Soshi Kodanshi: a trait run with the card's other mandatory entry effects (ApplyMandatoryEntryEffects). Made inside the entry, so the Ninja opens no window of its own.
    private void ApplyTfPersonalitiesEntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != TfPersonalitiesAbilities.KodanshiEffect || !EnteredFromProvince(card)) return;
        var scan = CreatedCards.Resolve(KodanshiNinja);
        // The proxy scan prints "Scorpion Clan - Ninja"; its published keyword list lacks the clan, so the created card is printed as a Scorpion Clan Ninja.
        var printed = scan with { Clan = "Scorpion", Keywords = ["Scorpion Clan", "Ninja"] };
        CreatePersonality(playerId, scan, true, null, out _, printed);
    }

    // Kakita Izumiko: "After Izumiko wins a duel, gain 1 Honor." -- called where the duel's outcome has applied (beside the other winner traits), once per duel she wins.
    private void ApplyTfPersonalitiesDuelWin(string? winnerId)
    {
        if (winnerId is null || !_state.Cards.TryGetValue(winnerId, out var winner) || !InPlay(winner) || Definition(winner).Effect != TfPersonalitiesAbilities.IzumikoEffect) return;
        GainHonor(winner.OwnerId, 1, $"{Definition(winner).Name} winning a duel", winner);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfPersonalitiesHooks()
    {
        EngineHooks.Abilities.Register("AddTfPersonalities", 11500, static abilities => abilities.AddTfPersonalities());
        EngineHooks.RunEffects.Register("RunTfPersonalities", 11500, static (engine, context) => engine.RunTfPersonalities(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant));
        EngineHooks.TargetAllows.Register("TfPersonalitiesTargetAllows", 11500, static (engine, context) => engine.TfPersonalitiesTargetAllows(context.ability.Target, context.command, context.player, context.target));
        EngineHooks.ValidateMiddle.Register("ValidateTfPersonalities", 11500, static (engine, context) => engine.ValidateTfPersonalities(context.id, context.command, context.player));
        EngineHooks.AttackStrengthBefore.Register("TfPersonalitiesAttackStrength", 11500, static (engine, context) => engine.TfPersonalitiesAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("TfPersonalitiesAttackKind", 11500, static context => TfPersonalitiesAttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("TfPersonalitiesBowsTarget", 11500, static context => context.abilityId == TfPersonalitiesAbilities.GorobeiBow);
    }
}
