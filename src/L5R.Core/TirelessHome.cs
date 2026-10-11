namespace L5R.Core;

// Issue #672, primitive P16: Tireless and Home as ability data (Coils of Madness supplement, rule 9).
//
//   TIRELESS  "the action may be taken even while the card is bowed" -- AbilityDefinition.Tireless, read by ValidateAbility's bowed-source rule beside the older per-id list (which is
//             left exactly as it was: no existing card changes). It waives that rule and nothing else: a Tireless action with a printed "bow this card" cost still cannot be taken while
//             the card is bowed, because a bowed card cannot be bowed to pay (ValidateTirelessHome).
//   HOME      "the action may be taken even while the card is at home rather than at the current battlefield" -- AbilityDefinition.Home, read by the two source-at-the-battlefield
//             checks of ValidateAbility through HomeWaivesSourceLocation. Unlike SourceUnitMayBeAway ("even if this card is not at the current battlefield": anywhere), Home waives the
//             checks only while the source's unit IS at home; a unit at another battlefield is not at home. The acting player still needs a unit at the current battlefield unless
//             the action is Absent or moves a Personality to the battle (MovesToBattle, the rules lead's 2026-09-19 ruling).
//   GRANTED HOME  "Give Home to one or more Spells in this unit" (Koan's Scroll Satchel): Home is an action keyword and an action inherits its card's keywords (core rules, Abilities;
//             ActionCarriesKeyword), so the grant is an ordinary keyword grant of "Home" on the Spell (GrantKeyword, GrantDuration.Turn -- R-001: until the turn ends). It needs no
//             state of its own: it is delayed, negated, shown and removed at the end of the turn exactly as every granted keyword is, and HomeWaivesSourceLocation reads it.
//
// Not built here (no demonstration card is wired for them): TimingOverrides (The Sorrow) and PresenceGrants (Doji Tatsuzo). See docs/decisions/2026-10-06-672-p16-tireless-home.md.
//
// The demonstration cards (personality-wave-129, spell-wave-54, item-wave-47), each wired from the PRINTED text of its scan:
//   Goju Obayashi (oracle-l5r-10877)          "After Obayashi enters play, lose 2 Honor." (EntryHonorLoss) / "Ninja Tireless Battle: Move Obayashi home."
//   Moshi Sasako - exp (oracle-l5r-10643)     "Tireless Battle: Straighten Sasako."
//   Shinjo Kinto - exp (oracle-l5r-10674)     "Home Battle: Move Kinto home or, if he would be opposed, to the current battlefield." -- one printed action with two destinations,
//                                             offered as two abilities that share one use per turn (Shosuro Hawado's shape).
//   Daigotsu Kanpeki - exp2 (oracle-l5r-10876) "Tireless Open: Take the Imperial Favor."
//   My Ancestor's Steed (oracle-l5r-10705)    "Home Water Battle, bow: Move your target Spirit Personality at any location to the current battlefield."
//   Koan's Scroll Satchel (oracle-l5r-10526)  "Open, bow: Give Home to one or more Spells in this unit, up to the number of Koan cards in this unit."
public static class TirelessHomeAbilities
{
    public const string ObayashiMoveHome = "obayashi-move-home";
    public const string SasakoStraighten = "sasako-exp-straighten";
    public const string KintoMoveHome = "kinto-exp-move-home", KintoMoveBattle = "kinto-exp-move-battle", KintoUsage = "kinto-exp-move";
    public const string KanpekiFavor = "kanpeki-exp2-favor";
    public const string SteedMove = "ancestors-steed-move";
    public const string SatchelHome = "koan-satchel-home";
    /// <summary>The Satchel's slot category: a Spell in the Satchel's own unit.</summary>
    public const string SpellInThisUnit = "p16-spell-in-this-unit";
    public const string SatchelSlot = "spells";
    /// <summary>The largest number of Spells the Satchel's slot can name; the real limit is the number of Koan cards in its unit, checked when the action is validated.</summary>
    public const int SatchelSlotMax = 20;
    /// <summary>The keyword a card's actions inherit: "Home actions may be taken from home".</summary>
    public const string HomeKeyword = "Home";
    /// <summary>A fixture ability on no published card ("Tireless Battle, bow this card: Give this card +1F"): the rule that Tireless does not let a bowed card pay a bow cost
    /// has no demonstration card of its own, so the regression grants this to a training Personality.</summary>
    public const string TrainingTirelessBow = "training-tireless-bow";

    public static Dictionary<string, AbilityDefinition> AddTirelessHome(this Dictionary<string, AbilityDefinition> all)
    {
        all[ObayashiMoveHome] = new("Battle", "Ninja Tireless Battle: Move Obayashi home", "none", ActionKeywords: ["Ninja"], Move: new(MoveSpec.Source, MoveSpec.Home), Tireless: true);
        all[SasakoStraighten] = new("Battle", "Tireless Battle: Straighten Sasako", "none", Tireless: true);
        all[KintoMoveHome] = new("Battle", "Home Battle: Move Kinto home", "none", UsageKey: KintoUsage, Move: new(MoveSpec.Source, MoveSpec.Home), Home: true);
        all[KintoMoveBattle] = new("Battle", "Home Battle: If Kinto would be opposed, move him to the current battlefield", "none", UsageKey: KintoUsage, MovesToBattle: true,
            Move: new(MoveSpec.Source, MoveSpec.CurrentBattlefield), Home: true);
        all[KanpekiFavor] = new("Open", "Tireless Open: Take the Imperial Favor", "none", Tireless: true);
        // "your target Spirit Personality at any location": the any-location category with the Spirit status; "your" is checked in ValidateTirelessHome.
        all[SteedMove] = new("Battle", "Home Water Battle, bow this card: Move your target Spirit Personality at any location to the current battlefield", "personality-at-any-location",
            TargetMustBe: new(Keywords: ["Spirit"]), ActionKeywords: ["Water"], MovesToBattle: true, Costs: ActionCost.BowSource, Move: new(MoveSpec.Target, MoveSpec.CurrentBattlefield), Home: true);
        all[SatchelHome] = new("Open", "Bow this card: Give Home to one or more Spells in this unit, up to the number of Koan cards in this unit", "slots",
            Slots: [new(SatchelSlot, SpellInThisUnit, "One or more Spells in this unit, up to the number of Koan cards in this unit", 1, SatchelSlotMax)], Costs: ActionCost.BowSource);
        all[TrainingTirelessBow] = new("Battle", "Tireless Battle, bow this card: Give this card +1F", "none", Costs: ActionCost.BowSource, Tireless: true);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- the Home rule ----

    /// <summary>Home (Coils of Madness rule 9): whether the action's source need not be at the current battlefield because the action has Home -- printed on the action
    /// (AbilityDefinition.Home) or given to its card (the "Home" keyword, Koan's Scroll Satchel) -- and the source's unit is at home. Read by the two source-at-the-battlefield
    /// checks of ValidateAbility, beside SourceMayBeAway.</summary>
    private bool HomeWaivesSourceLocation(AbilityDefinition ability, CardInstance? source)
    {
        if (source is null || !InPlay(source)) return false;
        if (!ability.Home && !Keyword(source, TirelessHomeAbilities.HomeKeyword) && !Wave821GrantsHome(source) /* issue #821: Tsumaro's Illusion */) return false;
        return _state.Cards.TryGetValue(UnitHostId(source), out var unit) && unit.Zone == Zone.Home;
    }

    // The cards of the unit a card belongs to: its Personality and everything attached to him.
    private IEnumerable<CardInstance> TirelessHomeUnitCards(CardInstance card)
    {
        var hostId = UnitHostId(card);
        return _state.Cards.Values.Where(c => c.Id == hostId || c.Zone == Zone.Attached && c.AttachedTo == hostId);
    }

    // ---- target legality ----

    private bool TirelessHomeTargetAllows(string category, CardInstance? source, CardInstance target) =>
        category == TirelessHomeAbilities.SpellInThisUnit && source is { Zone: Zone.Attached } && target.Zone == Zone.Attached && Definition(target).Type == CardType.Spell
        && UnitHostId(target) == UnitHostId(source);

    // ---- validation ----

    private void ValidateTirelessHome(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Tireless waives the bowed-source rule, never a cost: a bowed card cannot be bowed to pay "bow this card". Only an ability that carries the flag is judged here, so the older
        // per-id "even if bowed" abilities keep whatever their own wiring does.
        if (ability.Tireless && source is { Bowed: true }
            && (ability.Costs ?? []).Any(c => c.Verb == CostVerb.Bow && c.Subject == CostSubject.Source && !(c.UnlessInHand && source.Zone == Zone.Hand) && !CostExempt(c, source)))
            Require(false, $"{Definition(source).Name} is already bowed, so it cannot be bowed to pay this cost.");
        switch (id)
        {
            // "if he would be opposed": an enemy unit is at the current battlefield.
            case TirelessHomeAbilities.KintoMoveBattle:
                Require(_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any(), "Kinto would not be opposed: there are no enemy units at the current battlefield.");
                break;
            // "your target Spirit Personality".
            case TirelessHomeAbilities.SteedMove:
                Require(command.TargetId is null || !_state.Cards.TryGetValue(command.TargetId, out var spirit) || spirit.OwnerId == player.Id, "Target your own Spirit Personality.");
                break;
            // "up to the number of Koan cards in this unit": the Satchel is one of them.
            case TirelessHomeAbilities.SatchelHome:
                {
                    var koans = source is null ? 0 : TirelessHomeUnitCards(source).Count(c => Keyword(c, "Koan"));
                    var chosen = command.Selections?.FirstOrDefault(s => s.Slot == TirelessHomeAbilities.SatchelSlot)?.CardIds?.Length ?? 0;
                    Require(chosen <= koans, $"Choose at most {koans} Spell{(koans == 1 ? "" : "s")}: the number of Koan cards in this unit.");
                    break;
                }
        }
    }

    // ---- effects ----

    private bool? RunTirelessHome(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case TirelessHomeAbilities.ObayashiMoveHome:
            case TirelessHomeAbilities.KintoMoveHome:
                // "Move him home": nothing moves when he is at home already (a Home action may be taken from there).
                if (source is { Zone: Zone.Battlefield }) ExecuteMove(command, ability, source, target);
                return false;
            case TirelessHomeAbilities.KintoMoveBattle:
                // "to the current battlefield": nothing moves when he stands there already.
                if (source is not null && !AtBattle(source)) ExecuteMove(command, ability, source, target);
                return false;
            case TirelessHomeAbilities.SasakoStraighten:
                Straighten(source!);
                return false;
            case TirelessHomeAbilities.KanpekiFavor:
                TakeImperialFavor(player.Id);
                Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id);
                return false;
            case TirelessHomeAbilities.SteedMove:
                if (target is not null && InPlay(target) && !AtBattle(target)) ExecuteMove(command, ability, source, target);
                return false;
            case TirelessHomeAbilities.SatchelHome:
                foreach (var spell in SlotCards(command, TirelessHomeAbilities.SatchelSlot).ToArray())
                {
                    if (!InPlay(spell)) continue;
                    GrantKeyword(spell, TirelessHomeAbilities.HomeKeyword, source!.Id, GrantDuration.Turn);
                    if (Keyword(spell, TirelessHomeAbilities.HomeKeyword))
                        Emit("keywordGranted", $"{Definition(spell).Name} has Home until the turn ends (its actions may be taken from home).", player.Id, spell.Id);
                }
                return false;
            case TirelessHomeAbilities.TrainingTirelessBow:
                ApplyModifier(source!, source!.Id, id, "Force", 1);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTirelessHomeHooks()
    {
        EngineHooks.Abilities.Register("P16AddTirelessHome", 12900, static abilities => abilities.AddTirelessHome());
        EngineHooks.RunEffects.Register("P16RunTirelessHome", 12900, static (engine, context) => engine.RunTirelessHome(context.id, context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.TargetAllows.Register("P16TirelessHomeTargetAllows", 12900, static (engine, context) => engine.TirelessHomeTargetAllows(context.ability.Target, context.source, context.target));
        EngineHooks.ValidateMiddle.Register("P16ValidateTirelessHome", 12900, static (engine, context) => engine.ValidateTirelessHome(context.id, context.ability, context.command, context.player, context.source));
    }
}
