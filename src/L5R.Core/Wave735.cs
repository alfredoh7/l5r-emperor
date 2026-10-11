using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #735, wave batch 4: the keyword-rule, Home and Tireless cards, wired on the merged P10 (keyword rules, Clan alignment), P16 (Home) and P2 (entry cost terms). Every legality and every effect is decided in Core.
// Ability ids are "w735-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-4-keyword-rules-home-and-tireless.md. Waves are those of tools/content/package_wave_735.py.
//
//   Chuda Hikano              "After Hikano enters play, lose 1 Honor." / "Home Battle, bow: Straighten your target Oni." / "Maho Limited: Destroy a target Ikoma Ayumu. Destroy Hikano."   EntryHonorLoss 1; P16 Home; two destroys
//   Chun                      "Human Personalities in Chun's army have Naval."                                                                                  P10 Army-scope rule
//   Daidoji Gensai            "Home Battle: If Gensai would be opposed, move him to the current battlefield. After he moves, you may discard a card to make a Ranged Attack ..."   two actions (move; move, discard, attack)
//   Doji Tatsuzo              "Political Open: Target a Personality: While he is at the current battlefield (later this turn), you may take a Battle action even if you control no units there. ..."   a presence grant
//   The Shakash - exp         "While you control three or fewer Personalities, your other Nonhuman Personalities and Followers enter play for 1 less Gold." / "Home Battle: Move your target Personality at any location to The Shakash's location."   P2 entry term; P16 Home
//   The Sorrow                "While you control a Courtier, The Sorrow's printed ability is Battle/Limited." / "Battle: Give a target Personality -4F."       the printed Battle action, and a Limited twin offered while you control a Courtier
//   Tsiang, the Benevolent    "Tsiang enters play ignoring Gold Cost if you control the Ring of Water." / "Home Water Battle, bow: Straighten one or two target cards in units."   P2 entry term; P16 Home
//   Elephant Cavalry          "Will only attach to a Human. Cards in this unit have Cavalry." / "Melee and Ranged Attacks targeting cards in this unit have -2 strength."   AttachRequiresKeyword; P10 Unit rule; AttackDefense data
//   Long-Range Scouts         "Home Battle/Open: Transfer this card to your target Personality."                                                                Field Messenger's transfer, with P16 Home
//   Uji's Saboteurs           "This Personality has Naval while he is Crane Clan." / "Battle: Target an enemy attachment: Destroy it."                           P10 Unit rule; a destroy
//   Anchored in Earth         "Earth Remote Battle, bow: Give your target defending Spirit +3F and Elite."                                                     SourceUnitMayBeAway
//   Chikushudo's Infusion     "This Shugenja has Cavalry while he is Shadowlands." / "Open, bow: Give a target Nonhuman card Cavalry."                          P10 Unit rule; a keyword grant
//   Drawing on All Things     "This Shugenja has the element keywords of every Ring you control." / "Open, bow: Bow your target unbowed Ring. Straighten this Shugenja."   P10 Unit rule
//   Scorching Lash            "This Shugenja has Duelist while he is Fire." / "Battle/Open: Remove a non-Ring card in any discard pile from the game, and target a Personality: Give him +3F or -3F."   P10 Unit rule; P24 cost card; two actions
//   Suikihime's Sanction      "Limited: Permanently give your target non-Loyal, face-up Personality in or out of play your Clan Alignment."                       P10 GrantClanAlignment
//   The Inevitable Grasp of Conquest  "Your Personalities have Conqueror." / "Battle: Destroy one of your Provinces. Other players' cards will not destroy your Personalities this battle. Discard this Celestial."   P10 Controller rule; P12 immunity
//
// Not wired, with a blocker note (tools/content/pe_wave_735_blockers.json): see that file.
public static class Wave735Abilities
{
    public const string HikanoStraighten = "w735-hikano-straighten", HikanoMaho = "w735-hikano-maho", GensaiMove = "w735-gensai-move", GensaiAttack = "w735-gensai-attack", TatsuzoGrant = "w735-tatsuzo-grant",
        ShakashMove = "w735-shakash-move", SorrowBattle = "w735-sorrow-battle", SorrowLimited = "w735-sorrow-limited", TsiangStraighten = "w735-tsiang-straighten", ScoutsTransfer = "w735-scouts-transfer",
        UjiDestroy = "w735-uji-destroy", AnchoredBoost = "w735-anchored-boost", InfusionCavalry = "w735-infusion-cavalry", DrawingBow = "w735-drawing-bow", LashPlus = "w735-lash-plus", LashMinus = "w735-lash-minus",
        SanctionGrant = "w735-sanction-grant", GraspDestroy = "w735-grasp-destroy";

    // The cards (oracle ids).
    public const string Hikano = "oracle-l5r-10665", Chun = "oracle-l5r-10321", Gensai = "oracle-l5r-10838", Tatsuzo = "oracle-l5r-10283", Shakash = "oracle-l5r-10502", Sorrow = "oracle-l5r-10873",
        Tsiang = "oracle-l5r-10512", Elephant = "oracle-l5r-10679", Scouts = "oracle-l5r-10900", Uji = "oracle-l5r-10339", Anchored = "oracle-l5r-10346", Infusion = "oracle-l5r-10915",
        Drawing = "oracle-l5r-10916", Lash = "oracle-l5r-10353", Sanction = "oracle-l5r-10557", Grasp = "oracle-l5r-10817";

    // Catalogue Effects (a card names its own traits with one).
    public const string ChunEffect = "w735-chun", ShakashEffect = "w735-shakash", ElephantEffect = "w735-elephant", UjiEffect = "w735-uji", InfusionEffect = "w735-infusion", DrawingEffect = "w735-drawing",
        LashEffect = "w735-lash", GraspEffect = "w735-grasp";

    // Target categories.
    public const string AyumuTarget = "w735-ikoma-ayumu", NonhumanCard = "w735-nonhuman-card", SanctionTarget = "w735-non-loyal-face-up-personality";

    public static Dictionary<string, AbilityDefinition> AddWave735(this Dictionary<string, AbilityDefinition> all)
    {
        all[HikanoStraighten] = new("Battle", "Home Battle, bow: Straighten your target Oni", "own-personality", TargetMustBe: new(Keywords: ["Oni"]), Costs: ActionCost.BowSource, Home: true);
        all[HikanoMaho] = new("Limited", "Maho Limited: Destroy a target Ikoma Ayumu. Destroy Hikano", AyumuTarget, ActionKeywords: ["Maho"]);
        all[GensaiMove] = new("Battle", "Home Battle: If Gensai would be opposed, move him to the current battlefield", "none", MovesToBattle: true, Move: new(MoveSpec.Source, MoveSpec.CurrentBattlefield), Home: true);
        all[GensaiAttack] = new("Battle", "Home Battle: If Gensai would be opposed, move him to the current battlefield. After he moves, discard a card to make a Ranged Attack with strength equal to the card's Focus Value", "attack",
            MovesToBattle: true, Move: new(MoveSpec.Source, MoveSpec.CurrentBattlefield), Home: true, CostOptions: [AlternativeDiscard.Option]);
        all[TatsuzoGrant] = new("Open", "Political Open: Target a Personality: While he is at the current battlefield (later this turn), you may take a Battle action even if you control no units there. After that action ends, end this action's effects on him",
            "personality", ActionKeywords: ["Political"]);
        all[ShakashMove] = new("Battle", "Home Battle: Move your target Personality at any location to The Shakash's location", "personality-at-any-location", Home: true);
        all[SorrowBattle] = new("Battle", "Battle: Give a target Personality -4F", "personality");
        all[SorrowLimited] = new("Limited", "Limited (while you control a Courtier): Give a target Personality -4F", "personality");
        all[TsiangStraighten] = new("Battle", "Home Water Battle, bow: Straighten one or two target cards in units", "slots", Slots: [new("cards", "any-unit-card", "One or two cards in units", 1, 2)], Costs: ActionCost.BowSource,
            ActionKeywords: ["Water"], Home: true);
        all[ScoutsTransfer] = new("BattleOrOpen", "Home Battle/Open: Transfer this Follower to your target Personality", "own-personality", Home: true);
        all[UjiDestroy] = new("Battle", "Battle: Target an enemy attachment: Destroy it", "enemy-attachment");
        all[AnchoredBoost] = new("Battle", "Earth Remote Battle, bow: Give your target defending Spirit +3F and Elite", "own-defending-personality", TargetMustBe: new(Keywords: ["Spirit"]), Costs: ActionCost.BowSource,
            ActionKeywords: ["Earth"], SourceUnitMayBeAway: true);
        all[InfusionCavalry] = new("Open", "Open, bow: Give a target Nonhuman card Cavalry", NonhumanCard, Costs: ActionCost.BowSource);
        all[DrawingBow] = new("Open", "Open, bow: Bow your target unbowed Ring. Straighten this Shugenja", "own-unbowed-ring", Costs: ActionCost.BowSource);
        var (discardSlot, removeDiscard) = EffectPrimitiveCosts.RemoveFromAnyDiscard(1, exceptType: CardType.Ring);
        all[LashPlus] = new("BattleOrOpen", "Battle/Open: Remove a non-Ring card in any discard pile from the game, and target a Personality: Give him +3F", "slots",
            Slots: [new("personality", "personality", "A Personality"), discardSlot], Costs: [removeDiscard]);
        all[LashMinus] = new("BattleOrOpen", "Battle/Open: Remove a non-Ring card in any discard pile from the game, and target a Personality: Give him -3F", "slots",
            Slots: [new("personality", "personality", "A Personality"), discardSlot], Costs: [removeDiscard]);
        all[SanctionGrant] = new("Limited", "Limited: Permanently give your target non-Loyal, face-up Personality in or out of play your Clan Alignment", SanctionTarget);
        all[GraspDestroy] = new("Battle", "Battle: Destroy one of your Provinces. Other players' cards will not destroy your Personalities this battle. Discard this Celestial", "slots",
            Slots: [new("province", TargetSlotKinds.Province, "One of your Provinces") { OwnProvincesOnly = true }]);
        return all;
    }
}

/// <summary>Doji Tatsuzo's grant: the player may take a Battle action without a unit at the current battlefield while the Personality is there, this turn, once.</summary>
public sealed class Wave735PresenceGrant
{
    public int PlayerId { get; set; }
    public string TargetId { get; set; } = "";
    public int Turn { get; set; }
}

public sealed partial class MatchState
{
    // 22980-22989 is reserved for issue #735. Omitted while unused, so a match that never used it serializes exactly as before.
    [JsonPropertyOrder(22980)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave735PresenceGrant>? Wave735Presence { get; set; }
    /// <summary>The ids of the immunities The Inevitable Grasp of Conquest gave for the battle under way; they end with it.</summary>
    [JsonPropertyOrder(22981)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Wave735GraspImmunities { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- Doji Tatsuzo's presence grant ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // "While he is at the current battlefield (later this turn), you may take a Battle action even if you control no units there." Read by ValidateAbility's presence check.
    internal bool Wave735WaivesPresence(int playerId) =>
        _state.Wave735Presence is { Count: > 0 } grants && grants.Any(g => g.PlayerId == playerId && g.Turn == _state.TurnNumber && _state.Cards.TryGetValue(g.TargetId, out var target) && InPlay(target) && AtBattle(target));

    // "After that action ends, end this action's effects on him": the grant is spent by the first Battle action the player takes while he controls no unit at the battlefield.
    private void Wave735AfterAction(ResolvedActionFacts facts)
    {
        if (_state.Wave735Presence is not { Count: > 0 } grants || !AbilityCatalog.All.TryGetValue(facts.AbilityId, out var ability)) return;
        var acting = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
        if (!acting || _state.CurrentBattleProvince is not int province || Army(facts.PlayerId, province).Any()) return;
        var spent = grants.FirstOrDefault(g => g.PlayerId == facts.PlayerId && g.Turn == _state.TurnNumber && _state.Cards.TryGetValue(g.TargetId, out var t) && InPlay(t) && AtBattle(t));
        if (spent is null) return;
        grants.Remove(spent);
        if (grants.Count == 0) _state.Wave735Presence = null;
    }

    // ---- The Inevitable Grasp of Conquest: its immunity lasts this battle ---------------------------------------------------------------------------------------------------------------------

    // Called when a battle's province closes out (FinishBattleResolution).
    internal void Wave735EndBattle()
    {
        if (_state.Wave735GraspImmunities is not { } ids) return;
        foreach (var id in ids) RemoveImmunity(id);
        _state.Wave735GraspImmunities = null;
    }

    // ---- target categories -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave735TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            case Wave735Abilities.AyumuTarget:
                return InPlay(target) && Definition(target).Type == CardType.Personality && Definition(target).Name.StartsWith("Ikoma Ayumu", StringComparison.Ordinal);
            case Wave735Abilities.NonhumanCard:
                return InPlay(target) && Keyword(target, "Nonhuman");
            default:
                return false;
        }
    }

    // Suikihime's Sanction names a face-up Personality in a Province as well as one in play, which the in-play default never lists. Null: not this card's category.
    private bool Wave735SanctionTargetAllows(CardInstance target) =>
        Definition(target).Type == CardType.Personality && !Keyword(target, "Loyal") && (InPlay(target) || target.Zone == Zone.Province && target.FaceUp);

    private string?[]? Wave735Candidates(string category) => category != Wave735Abilities.SanctionTarget ? null
        : _state.Cards.Values.Where(Wave735SanctionTargetAllows).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave735Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        switch (id)
        {
            // "If Gensai would be opposed": an enemy unit is at the current battlefield.
            case Wave735Abilities.GensaiMove:
            case Wave735Abilities.GensaiAttack:
                Require(_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any(), "Gensai would not be opposed: there are no enemy units at the current battlefield.");
                break;
            // "Your target Personality at any location".
            case Wave735Abilities.ShakashMove:
                Require(command.TargetId is null || !_state.Cards.TryGetValue(command.TargetId, out var mover) || mover.OwnerId == player.Id, "Target your own Personality.");
                break;
            // "While you control a Courtier, The Sorrow's printed ability is Battle/Limited."
            case Wave735Abilities.SorrowLimited:
                Require(_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Courtier")), "You control no Courtier.");
                break;
            case Wave735Abilities.ScoutsTransfer:
                if (source is not null && command.TargetId is { } hostId) Require(CanTransferTo(source, Card(hostId)), $"{Definition(source).Name} cannot be transferred to {Definition(Card(hostId)).Name}.");
                break;
            case Wave735Abilities.GraspDestroy:
                {
                    var chosen = command.Selections?.FirstOrDefault(s => s.Slot == "province");
                    Require(chosen?.ProvinceOwner == player.Id && chosen.Province is int index && Player(player.Id).Provinces.Any(p => p.Index == index && !p.Destroyed), "Choose one of your own Provinces.");
                    break;
                }
        }
    }

    // ---- entry costs ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave735Entry(EntryCostContext context)
    {
        var (card, payer) = (context.Card, context.PayerId);
        // Tsiang: "enters play ignoring Gold Cost if you control the Ring of Water."
        if (card.DefinitionId == Wave735Abilities.Tsiang && context.Kind is EntryCostKind.Recruit or EntryCostKind.Reserve && ControlsRing(payer, "Ring of Water")) yield return new(0, "Ring of Water", IgnoreCost: true);
        // The Shakash: "While you control three or fewer Personalities, your other Nonhuman Personalities and Followers enter play for 1 less Gold."
        var type = Definition(card).Type;
        if (type is not (CardType.Personality or CardType.Follower) || !Keyword(card, "Nonhuman")) yield break;
        var shakash = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == payer && InPlay(c) && c.DefinitionId == Wave735Abilities.Shakash && c.Id != card.Id);
        if (shakash is null) yield break;
        if (type == CardType.Personality ? context.Kind is EntryCostKind.Recruit or EntryCostKind.Reserve : context.Kind == EntryCostKind.Equip)
            if (_state.Cards.Values.Count(c => c.OwnerId == payer && InPlay(c) && Definition(c).Type == CardType.Personality) <= 3) yield return new(-1, "The Shakash");
    }

    // ---- effects ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave735(RunHookContext c)
    {
        var (id, ability, command, player, source, target) = (c.id, c.ability, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case Wave735Abilities.HikanoStraighten:
                Straighten(target!);
                return false;
            case Wave735Abilities.HikanoMaho:
                // Two effects in order: the target Ikoma Ayumu is destroyed (if that is not negated), then Hikano is destroyed whatever became of it.
                DestroyTargetCard(target!, player.Id);
                if (InPlay(source!)) DestroyTargetCard(source!, player.Id);
                return false;
            case Wave735Abilities.GensaiMove:
                if (source is not null && !AtBattle(source)) ExecuteMove(command, ability, source, target);
                return false;
            case Wave735Abilities.GensaiAttack:
                {
                    // The discard was paid with the cost; "After he moves": no attack when his movement did not happen.
                    if (source is null) return false;
                    if (!AtBattle(source)) ExecuteMove(command, ability, source, target);
                    if (!AtBattle(source)) return false;
                    var focus = command.CardIds is [var discarded] && _state.Cards.TryGetValue(discarded, out var card) ? Definition(card).Focus : 0;
                    return ResolveOrDeferAttack(id, player, target?.Id, source.Id, c.consumedGrant, focus);
                }
            case Wave735Abilities.TatsuzoGrant:
                (_state.Wave735Presence ??= []).Add(new Wave735PresenceGrant { PlayerId = player.Id, TargetId = target!.Id, Turn = _state.TurnNumber });
                Emit("presenceGranted", $"{Definition(source!).Name}: {player.Name} may take a Battle action without a unit at the battlefield while {Definition(target).Name} is there.", player.Id, target.Id);
                return false;
            case Wave735Abilities.ShakashMove:
                {
                    if (source is null || target is null || !InPlay(target)) return false;
                    // "To The Shakash's location": home when he is at home, otherwise the battlefield he stands at.
                    if (source.Zone == Zone.Home) { if (target.Zone != Zone.Home) MoveHome(target, false); }
                    else if (source.Zone == Zone.Battlefield && source.ProvinceIndex is int province && !(target.Zone == Zone.Battlefield && target.ProvinceIndex == province)) TryMoveToBattlefield(target, province);
                    return false;
                }
            case Wave735Abilities.SorrowBattle:
            case Wave735Abilities.SorrowLimited:
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                return false;
            case Wave735Abilities.TsiangStraighten:
                foreach (var card in SlotCards(command, "cards").ToArray()) if (InPlay(card)) Straighten(card);
                return false;
            case Wave735Abilities.ScoutsTransfer:
                TransferAttachment(source!, target!, Definition(source!).Name);
                return false;
            case Wave735Abilities.UjiDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            case Wave735Abilities.AnchoredBoost:
                ApplyModifier(target!, source!.Id, id, "Force", 3);
                GrantKeyword(target!, "Elite", source.Id, GrantDuration.Turn);
                return false;
            case Wave735Abilities.InfusionCavalry:
                GrantKeyword(target!, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case Wave735Abilities.DrawingBow:
                BowByEffect(target!, player.Id);
                if (source is not null && WielderOf(source) is { } shugenja) Straighten(shugenja);
                return false;
            case Wave735Abilities.LashPlus:
            case Wave735Abilities.LashMinus:
                ApplyModifier(SlotCards(command, "personality").Single(), source!.Id, id, "Force", id == Wave735Abilities.LashPlus ? 3 : -3);
                return false;
            case Wave735Abilities.SanctionGrant:
                foreach (var clan in ControllerClans(player.Id)) GrantClanAlignment(target!, clan, source!.Id);
                return false;
            case Wave735Abilities.GraspDestroy:
                {
                    var chosen = command.Selections!.First(s => s.Slot == "province");
                    var province = Player(player.Id).Provinces.First(p => p.Index == chosen.Province!.Value);
                    DestroyProvince(player.Id, province);
                    // "Other players' cards will not destroy your Personalities this battle."
                    var immunity = GrantPlayerImmunity(player.Id, ImmunityKind.Destroy, ImmunityUntil.Permanent, ImmunitySource.OtherPlayers, targetType: CardType.Personality);
                    (_state.Wave735GraspImmunities ??= []).Add(immunity.Id);
                    WlItemsMiscDiscardCelestial(source!);
                    return false;
                }
            default:
                return null;
        }
    }

    private static string? Wave735AttackKind(string abilityId) => abilityId == Wave735Abilities.GensaiAttack ? "Ranged" : null;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave735Hooks()
    {
        EngineHooks.Abilities.Register("Wave735Abilities", 14100, static abilities => abilities.AddWave735());
        EngineHooks.TargetAllows.Register("Wave735TargetAllows", 14100, static (engine, context) => engine.Wave735TargetAllows(context));
        EngineHooks.ValidateLate.Register("Wave735Validate", 14100, static (engine, context) => engine.Wave735Validate(context));
        EngineHooks.RunEffects.Register("Wave735RunEffects", 14100, static (engine, context) => engine.RunWave735(context));
        EngineHooks.AttackKind.Register("Wave735AttackKind", 14100, static context => Wave735AttackKind(context.abilityId));
        EngineHooks.EntryCostTerms.Register("Wave735Entry", 14100, static context => context.Engine.Wave735Entry(context));
        EngineHooks.AfterActionTraits.Register("Wave735Presence", 14100, static (engine, facts) => engine.Wave735AfterAction(facts));

        // The P10 rules. Each reads the card's own Effect, so it applies while the card is in play.
        var isPersonality = new KeywordCondition(KeywordConditionKind.SubjectIsType, nameof(CardType.Personality));
        KeywordRuleCatalog.Register(
            // Chun: "Human Personalities in Chun's army have Naval."
            new KeywordRule("w735-chun-naval", KeywordRuleScope.Army, ["Naval"], SourceEffect: Wave735Abilities.ChunEffect, AllOf: [isPersonality, new(KeywordConditionKind.SubjectIsHuman)]),
            // Uji's Saboteurs: "This Personality has Naval while he is Crane Clan."
            new KeywordRule("w735-uji-naval", KeywordRuleScope.Unit, ["Naval"], SourceEffect: Wave735Abilities.UjiEffect, AllOf: [isPersonality, new(KeywordConditionKind.SubjectIsClan, "Crane")]),
            // Elephant Cavalry: "Cards in this unit have Cavalry."
            new KeywordRule("w735-elephant-cavalry", KeywordRuleScope.Unit, ["Cavalry"], SourceEffect: Wave735Abilities.ElephantEffect),
            // Chikushudo's Infusion: "This Shugenja has Cavalry while he is Shadowlands."
            new KeywordRule("w735-infusion-cavalry", KeywordRuleScope.Unit, ["Cavalry"], SourceEffect: Wave735Abilities.InfusionEffect, AllOf: [isPersonality, new(KeywordConditionKind.SubjectHasKeyword, "Shadowlands")]),
            // Drawing on All Things: "This Shugenja has the element keywords of every Ring you control."
            new KeywordRule("w735-drawing-elements", KeywordRuleScope.Unit, ["Air", "Earth", "Fire", "Water", "Void"], SourceEffect: Wave735Abilities.DrawingEffect,
                AllOf: [isPersonality, new(KeywordConditionKind.ControllerControlsRingWithAskedKeyword)]),
            // Scorching Lash: "This Shugenja has Duelist while he is Fire."
            new KeywordRule("w735-lash-duelist", KeywordRuleScope.Unit, ["Duelist"], SourceEffect: Wave735Abilities.LashEffect, AllOf: [isPersonality, new(KeywordConditionKind.SubjectHasKeyword, "Fire")]),
            // The Inevitable Grasp of Conquest: "Your Personalities have Conqueror."
            new KeywordRule("w735-grasp-conqueror", KeywordRuleScope.Controller, ["Conqueror"], SourceEffect: Wave735Abilities.GraspEffect, AllOf: [isPersonality]));
    }
}
