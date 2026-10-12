using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #737, wave batch 6: the after-event window and period watch cards, wired on the merged P6 (after-event facts, AfterFacts.cs), P7 (period stamps and negation watches, PeriodWatches.cs) and P8
// (action-effect watch, ActionEffectWatch.cs) primitives, with P4 (the End Phase draw rule), the Consecration watch and the straighten holds for the rest. Every legality and every effect is decided in Core.
// Ability ids are "w737-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-6-after-event-windows-and-watches.md. Waves are those of tools/content/package_wave_737.py.
//
//   Akumasa                   "After Akumasa enters play, lose 2 Honor." / "Open: Target another player's Personality who has a Madness token. After the target attacks this turn, draw a card."   EntryHonorLoss; w737-akumasa-watch (an AssignmentWatch)
//   Asahina Shigemitsu - exp  "Open, bow this card: Target one or two of your Provinces. After the first time this turn a Personality attacks one of those Provinces, gain 2 Honor."   w737-shigemitsu-watch (Consecration's watch)
//   Hida Tadama               "After the first time each phase Tadama is destroyed, bring him into play in your home, ignoring costs, and bow him."   a left-play trait (P6) with a phase stamp (P7)
//   Hiruma Ikeuchi            "Before the first time each battle any of Ikeuchi's Followers are destroyed by another player's card's effect: If you have Reconnaissance, negate the destruction of one such Follower."   a standing watch (P7)
//   Iweko Shibatsu            "Spider Clan players ignore Shibatsu's Honor Requirement. Will not gain a Clan Alignment." / "After Shibatsu enters play, gain 3 Honor." / "After Shibatsu leaves play, lose 10 Honor." / "Limited, bow: Gain 1 Honor."   HonorRequirementWaiverClan; a gain block (P10); an entry trait; a left-play trait (P6); w737-shibatsu-honor
//   Moto Taigo - exp          "After Taigo enters play, lose 3 Honor." / "Interrupt, bow this card: If the action moves any units, you may destroy a target card without attachments in one of them."   EntryHonorLoss; w737-taigo-interrupt (P8 moved watch)
//   Susumu Kuroko             "After you Proclaim Susumu Takuan, gain 2 Honor." / "Political Limited, bow this card: Take the Imperial Favor."   a proclaimed trait (P6); w737-kuroko-favor
//   Yogo Katsuta              "After Katsuta enters play, lose 2 Honor." / "After a Personality commits seppuku, his controller loses 1 Honor."   EntryHonorLoss; a seppuku trait (P6)
//   Reverse Fate              "Void Interrupt, bow this card: Give the first card discarded by the action +1FV or -1FV."   w737-reverse-fate-plus / -minus (P8 discarded watch)
//   The Crystal Tears         "Battle: Destroy a Terrain (if able). Put this Terrain into play. You may target and bow a Personality with 4 or more Force or a Fallen, Ninja, or Shadowlands card. After this Terrain is destroyed, straighten the target."   w737-crystal-tears-play; a terrain-destroyed trait (P6)
//   Magistrate's Stipend      "Bow this card: Produce 2 Gold, or 3 Gold if paying for a Magistrate which may only pay for that card." / "Reaction: After you announce a non-Iaijutsu action, choose your performing Magistrate to give him +1C until the action ends."   a conditional production; w737-stipend-announce (P6 announce window)
//   An End to Hostilities     "Limited: Until the game ends, each player draws two cards instead of one at the end of his turn if he put no other cards in his hand that turn."   a P4 end-of-turn draw rule
//   Unseasonable Weather      "Until your next turn begins, after each time a Personality is assigned to attack, negate his straightening during his controller's next Straighten Phase (after he assigns)."   an Event kept in play; an assigned-to-attack trait (P6) setting a straighten hold
//   Natsu-Togumara's Guidance "After this card enters play: You may choose a Personality. While this card remains in play, battle resolution does not bow cards in that Personality's unit." / "Battle: ... Move him to the current battlefield. If he moved, straighten his unit."   a Celestial entry choice (P6); w737-natsu-move
//   Verdant Jungle            "Your Nonhuman Personalities enter play from this Province for 1 less Gold." / "Reaction, bow this card: Before an action resolves, negate its effects that move any Personalities into an attacking army at this Province's battlefield."   an entry cost term (P2); w737-jungle-interrupt (P8 move gate)
//
// Not wired, with a blocker note (tools/content/pe_wave_737_blockers.json): Koan's Robes (a Force bonus counted by Koan cards in the unit and a waiver of the bow cost on the host's Spells: no stat term nor cost waiver of that shape is merged)
// and Imperial Deployment ("prevent him assigning" has no immunity kind, and "an attack is declared" has no fact).
// Issue #835: Imperial Deployment was wired afterwards in Wave835.cs, on Mendon's rulings of 2026-10-11 (docs/decisions/2026-10-11-twelve-ruled-cards.md); the "not wired" note here is the state when this wave merged.
public static class Wave737Abilities
{
    public const string AkumasaWatch = "w737-akumasa-watch", ShigemitsuWatch = "w737-shigemitsu-watch", ShibatsuHonor = "w737-shibatsu-honor", TaigoInterrupt = "w737-taigo-interrupt", KurokoFavor = "w737-kuroko-favor",
        ReverseFatePlus = "w737-reverse-fate-plus", ReverseFateMinus = "w737-reverse-fate-minus", CrystalTearsPlay = "w737-crystal-tears-play", StipendAnnounce = "w737-stipend-announce", NatsuMove = "w737-natsu-move",
        JungleInterrupt = "w737-jungle-interrupt";

    // Target categories of this file (TargetAllows).
    public const string EnemyMadnessPersonality = "w737-enemy-madness-personality", OwnPersonalityAnyLocation = "w737-own-personality-any-location", CrystalTearsTarget = "w737-crystal-tears-target";
    public const string CrystalTearsSlot = "target";

    // The cards (oracle ids).
    public const string Akumasa = "oracle-l5r-10495", Shigemitsu = "oracle-l5r-10624", Tadama = "oracle-l5r-10618", Ikeuchi = "oracle-l5r-10273", Shibatsu = "oracle-l5r-10882", Taigo = "oracle-l5r-10510",
        Kuroko = "oracle-l5r-10489", Katsuta = "oracle-l5r-10482", ReverseFate = "oracle-l5r-10919", CrystalTears = "oracle-l5r-10718", Stipend = "oracle-l5r-4795", EndToHostilities = "oracle-l5r-10424",
        UnseasonableWeather = "oracle-l5r-10263", Natsu = "oracle-l5r-5518", VerdantJungle = "oracle-l5r-9142";

    // Catalogue Effects (a card names its own traits with one).
    public const string TadamaEffect = "w737-tadama", IkeuchiEffect = "w737-ikeuchi", ShibatsuEffect = "w737-shibatsu", KurokoEffect = "w737-kuroko", KatsutaEffect = "w737-katsuta", StipendEffect = "w737-stipend",
        EndToHostilitiesEffect = "w737-end-to-hostilities", WeatherEffect = "w737-unseasonable-weather", NatsuEffect = "w737-natsu", JungleEffect = "w737-verdant-jungle";

    public const string AkumasaWatchKind = "w737-akumasa", TaigoCallback = "w737-taigo-cb", FatePlusCallback = "w737-fate-plus-cb", FateMinusCallback = "w737-fate-minus-cb", JungleCallback = "w737-jungle-gate";

    /// <summary>The target categories that are exempt from "a Battle target must be at the current battlefield" (their own location rule is part of the category).</summary>
    public static bool IsAnyLocationTarget(string category) => category == OwnPersonalityAnyLocation;

    /// <summary>The Terrain whose own ability puts it into play (never the generic playStrategy Terrain rule).</summary>
    public static bool IsTerrainPlayAbility(string abilityId) => abilityId == CrystalTearsPlay;

    public static Dictionary<string, AbilityDefinition> AddWave737(this Dictionary<string, AbilityDefinition> all)
    {
        all[AkumasaWatch] = new("Open", "Open: Target another player's Personality who has a Madness token. After the target attacks this turn, draw a card", EnemyMadnessPersonality);
        all[ShigemitsuWatch] = new("Open", "Open, bow this card: Target one or two of your Provinces. After the first time this turn a Personality attacks one of those Provinces, gain 2 Honor", EmperorRemainingEAbilities.OwnProvincesTarget,
            Costs: ActionCost.BowSource);
        all[ShibatsuHonor] = new("Limited", "Limited, bow this card: Gain 1 Honor", "none", Costs: ActionCost.BowSource);
        all[KurokoFavor] = new("Limited", "Political Limited, bow this card: Take the Imperial Favor", "none", ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
        all[TaigoInterrupt] = new("Reaction", "Interrupt, bow this card: If the action moves any units, you may destroy a target card without attachments in one of them", "none", Trigger: ReactionTriggers.TargetedByAction,
            Costs: ActionCost.BowSource);
        all[ReverseFatePlus] = new("Reaction", "Void Interrupt, bow this card: Give the first card discarded by the action +1FV", "none", Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.BowSource, UsageKey: "w737-reverse-fate");
        all[ReverseFateMinus] = new("Reaction", "Void Interrupt, bow this card: Give the first card discarded by the action -1FV", "none", Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.BowSource, UsageKey: "w737-reverse-fate");
        all[CrystalTearsPlay] = new("Battle", "Destroy a Terrain (if able). Put this Terrain into play. You may target and bow a Personality with 4 or more Force or a Fallen, Ninja, or Shadowlands card. After this Terrain is destroyed, straighten the target", "slots",
            Slots: [new(CrystalTearsSlot, CrystalTearsTarget, "A Personality with 4 or more Force, or a Fallen, Ninja or Shadowlands card (optional)", 0, 1)]);
        all[StipendAnnounce] = new("Reaction", "Reaction: After you announce a non-Iaijutsu action, choose your performing Magistrate to give him +1C until the action ends", "none", Performer: "own-personality-with-keyword", RequiredKeywords: ["Magistrate"],
            Trigger: AnnounceTriggers.WithPerformer);
        all[NatsuMove] = new("Battle", "Battle: If there are any enemy units at the current battlefield, choose your performing Personality and discard this card: Move him to the current battlefield. If he moved, straighten his unit", OwnPersonalityAnyLocation,
            Costs: [new(CostVerbs.DiscardFromPlay)], MovesToBattle: true);
        all[JungleInterrupt] = new("Reaction", "Reaction, bow this card: Before an action resolves, negate its effects that move any Personalities into an attacking army at this Province's battlefield", "none", Trigger: ReactionTriggers.TargetedByAction,
            Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchState
{
    // 22930-22939 is reserved for batch 6 of the wirable-card waves (issue #737). Natsu-Togumara's Guidance's chosen Personality ("celestial id" to "Personality id"), omitted until it is used.
    [JsonPropertyOrder(22930)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? NatsuChoices { get; set; }

    // The Personality or card a Crystal Tears bowed ("Terrain id" to "target id"), straightened when the Terrain is destroyed; omitted until it is used.
    [JsonPropertyOrder(22931)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? CrystalTearsTargets { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Whether any card in this match (in play or not) carries the Effect: the "somebody could care" test of a trait that watches a card which may already have left play.</summary>
    private bool Wave737Has(string effect) => _state.Cards.Values.Any(c => Definition(c).Effect == effect);

    private IEnumerable<CardInstance> Wave737InPlay(string effect) =>
        _state.Cards.Values.Where(c => (InPlay(c) || c.Zone == Zone.EventInPlay) && Definition(c).Effect == effect).OrderBy(c => c.Id, StringComparer.Ordinal);

    private bool Wave737IsTakuan(CardInstance card) => Definition(card).Name.StartsWith("Susumu Takuan", StringComparison.Ordinal);

    // ---- target legality --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave737TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            case Wave737Abilities.EnemyMadnessPersonality:
                return InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId != context.player.Id && HasToken(target, "Madness");
            case Wave737Abilities.OwnPersonalityAnyLocation:
                return InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId == context.player.Id;
            // "A Personality with 4 or more Force or a Fallen, Ninja, or Shadowlands card" in play (either player's), at the battle.
            case Wave737Abilities.CrystalTearsTarget:
                return InPlay(target) && Definition(target).Type is CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell
                    && (Definition(target).Type == CardType.Personality && CardForce(target) >= 4 || Keyword(target, "Fallen") || Keyword(target, "Ninja") || HasClanAffiliation(target, "Shadowlands"))
                    && Card(UnitHostId(target)) is { Zone: Zone.Battlefield } host && AtBattle(host);
            default:
                return false;
        }
    }

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave737Validate(ValidationHookContext context)
    {
        switch (context.id)
        {
            case Wave737Abilities.NatsuMove:
                Require(_state.CurrentBattleProvince is int battlefield && Army(1 - context.player.Id, battlefield).Any(), "There are no enemy units at the current battlefield.");
                break;
            case Wave737Abilities.CrystalTearsPlay:
                Require(context.source is { Zone: Zone.Hand }, "The Crystal Tears is put into play from your hand.");
                break;
        }
    }

    // ---- effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave737(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave737Abilities.AkumasaWatch:
                _state.AssignmentWatches.Add(new(target!.Id, Wave737Abilities.AkumasaWatchKind, player.Id, _state.TurnNumber, Definition(source!).Name));
                Emit("watchSet", $"{Definition(source!).Name}: after {Definition(target).Name} attacks this turn, {player.Name} draws a card.", player.Id, target.Id);
                return false;
            case Wave737Abilities.ShigemitsuWatch:
                {
                    var chosen = (c.command.Provinces ?? (c.command.Province is int single ? [single] : [])).Distinct().Order().ToArray();
                    _state.ConsecrationWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
                    _state.ConsecrationWatches.Add(new(player.Id, chosen, _state.TurnNumber, source!.Id));
                    Emit("watchSet", $"{Definition(source).Name}: after the first time this turn a Personality attacks province {string.Join(" or ", chosen.Select(p => p + 1))}, {player.Name} gains 2 Honor.", player.Id, source.Id);
                    return false;
                }
            case Wave737Abilities.ShibatsuHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case Wave737Abilities.KurokoFavor:
                TakeImperialFavor(player.Id);
                Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id, source!.Id);
                return false;
            case Wave737Abilities.TaigoInterrupt:
                ArmActionEffectWatch(WatchKinds.Moved, Wave737Abilities.TaigoCallback, player.Id, source!.Id, atEnd: true);
                return false;
            case Wave737Abilities.ReverseFatePlus:
                ArmActionEffectWatch(WatchKinds.Discarded, Wave737Abilities.FatePlusCallback, player.Id, source!.Id);
                return false;
            case Wave737Abilities.ReverseFateMinus:
                ArmActionEffectWatch(WatchKinds.Discarded, Wave737Abilities.FateMinusCallback, player.Id, source!.Id);
                return false;
            case Wave737Abilities.JungleInterrupt:
                ArmActionEffectWatch(WatchKinds.MoveGate, Wave737Abilities.JungleCallback, player.Id, source!.Id);
                return false;
            case Wave737Abilities.StipendAnnounce:
                if (c.command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var magistrate)) ApplyUntilActionEnds(magistrate, Definition(source!).Name, id, "Chi", 1);
                return false;
            case Wave737Abilities.CrystalTearsPlay:
                {
                    if (_state.CurrentBattleProvince is not int battlefield || source!.Zone != Zone.Hand) return false;
                    // "Destroy a Terrain (if able)": the Terrain already at the battlefield is replaced.
                    foreach (var existing in _state.Cards.Values.Where(t => t.Zone == Zone.Battlefield && t.ProvinceIndex == battlefield && Keyword(t, "Terrain") && t.Id != source.Id).OrderBy(t => t.Id, StringComparer.Ordinal).Take(1).ToArray())
                        DiscardTerrain(existing);
                    PutTerrainIntoPlay(player, source);
                    // "You may target and bow": the chosen card is bowed now and straightened when this Terrain is destroyed.
                    if (SlotCards(c.command, Wave737Abilities.CrystalTearsSlot).FirstOrDefault() is { } bowed)
                    {
                        if (!StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                        (_state.CrystalTearsTargets ??= [])[source.Id] = bowed.Id;
                    }
                    return false;
                }
            case Wave737Abilities.NatsuMove:
                {
                    // The cost (discarding this Celestial) is paid; then "Move him to the current battlefield. If he moved, straighten his unit."
                    if (_state.CurrentBattleProvince is int battlefield && target is not null && InPlay(target) && TryMoveToBattlefield(target, battlefield)) StraightenUnit(target);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- entry: Shibatsu's Honor and his gain block ------------------------------------------------------------------------------------------------------------------------------------

    private void ApplyWave737EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != Wave737Abilities.ShibatsuEffect) return;
        // "Will not gain a Clan Alignment" (P10): a block that stands while he is in play. "After Shibatsu enters play, gain 3 Honor."
        AddKeywordGainBlock(card.Id, card.Id, null, classes: ["clan"]);
        GainHonor(playerId, 3, Definition(card).Name, card);
    }

    // ---- facts (P6) ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave737SeppukuFact(FactRecord fact)
    {
        // "After a Personality commits seppuku, his controller loses 1 Honor", for each Katsuta in play.
        foreach (var katsuta in Wave737InPlay(Wave737Abilities.KatsutaEffect).ToArray())
            GainHonor(fact.PlayerId, -1, Definition(katsuta).Name, katsuta);
    }

    private void Wave737ProclaimedFact(FactRecord fact)
    {
        if (fact.CardId is null || !_state.Cards.TryGetValue(fact.CardId, out var proclaimed) || !Wave737IsTakuan(proclaimed)) return;
        foreach (var kuroko in Wave737InPlay(Wave737Abilities.KurokoEffect).Where(k => k.OwnerId == fact.PlayerId).ToArray())
            GainHonor(fact.PlayerId, 2, Definition(kuroko).Name, kuroko);
    }

    private void Wave737LeftPlayFact(FactRecord fact)
    {
        if (fact.CardId is null || !_state.Cards.TryGetValue(fact.CardId, out var card)) return;
        switch (Definition(card).Effect)
        {
            // "After Shibatsu leaves play, lose 10 Honor."
            case Wave737Abilities.ShibatsuEffect:
                GainHonor(fact.PlayerId, -10, Definition(card).Name, card);
                break;
            // "After the first time each phase Tadama is destroyed, bring him into play in your home, ignoring costs, and bow him."
            case Wave737Abilities.TadamaEffect when fact.Arg == nameof(Zone.Dead) && card.Zone == Zone.Dead:
                if (!TrySpendPeriodUse(Player(card.OwnerId).AbilityUses, "w737-tadama:" + card.Id, Period.Phase)) break;
                ResetCardState(card);
                EnterPlayFromRecruit(Player(card.OwnerId), card, null, Definition(card).Name);
                card.Bowed = true;
                ApplyMandatoryEntryEffects(card.OwnerId, card);
                break;
        }
    }

    private void Wave737AssignedFact(FactRecord fact)
    {
        // Unseasonable Weather: "negate his straightening during his controller's next Straighten Phase", for each Weather in play.
        if (fact.CardId is null || !_state.Cards.TryGetValue(fact.CardId, out var attacker) || !InPlay(attacker)) return;
        foreach (var weather in Wave737InPlay(Wave737Abilities.WeatherEffect).Where(w => w.Zone == Zone.EventInPlay).ToArray())
            AddStraightenHold(attacker, Definition(weather).Name, attacker.OwnerId, StraightenPoint.StraightenPhaseEnds);
    }

    private void Wave737TerrainDestroyedFact(FactRecord fact)
    {
        // The Crystal Tears: "After this Terrain is destroyed, straighten the target."
        if (fact.CardId is null || _state.CrystalTearsTargets is not { } targets || !targets.Remove(fact.CardId, out var targetId)) return;
        if (targets.Count == 0) _state.CrystalTearsTargets = null;
        if (_state.Cards.TryGetValue(targetId, out var target) && InPlay(target)) Straighten(target);
    }

    // ---- Events: An End to Hostilities, Unseasonable Weather ------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave737Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case Wave737Abilities.EndToHostilitiesEffect:
                // "Each player draws two cards instead of one at the end of his turn if he put no other cards in his hand that turn": one more card, when the turn's hand count is zero (P4).
                AddTurnScopedRule(TurnRuleKinds.EndTurnExtraDraw, null, TurnUntil.GameEnds(), Definition(card).Name, amount: 1, counter: TurnCounters.CardsIntoHand, max: 0);
                return DiscardResolvedEvent(player, province, card);
            case Wave737Abilities.WeatherEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: after each Personality is assigned to attack, his straightening is negated during his controller's next Straighten Phase.", turnBeginsIn: 1);
                return false;
            default:
                return null;
        }
    }

    // ---- Natsu-Togumara's Guidance ----------------------------------------------------------------------------------------------------------------------------------------------------

    private CelestialEntryChoice? Wave737NatsuEntry(CelestialEntryContext context)
    {
        if (Definition(context.Celestial).Effect != Wave737Abilities.NatsuEffect) return null;
        var candidates = _state.Cards.Values.Where(p => InPlay(p) && Definition(p).Type == CardType.Personality).Select(p => p.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        return new("Natsu-Togumara's Guidance: choose a Personality; battle resolution will not bow cards in his unit", candidates,
            static (engine, entry, chosen) => { if (chosen is not null) (engine._state.NatsuChoices ??= [])[entry.Celestial.Id] = chosen; }, Optional: true);
    }

    /// <summary>"While this card remains in play, battle resolution does not bow cards in that Personality's unit": at the resolution of a battle, the chosen Personality of each Natsu-Togumara's Guidance in play who is
    /// in the returning army is named among the cards the resolution will not bow (the same list Redoubled Attack uses, cleared when the battle ends).</summary>
    private void Wave737NatsuMarksUnbowed(IEnumerable<CardInstance> returning)
    {
        if (_state.NatsuChoices is not { Count: > 0 } choices) return;
        foreach (var (celestialId, chosen) in choices)
            if (_state.Cards.TryGetValue(celestialId, out var celestial) && celestial.Zone == Zone.Celestial && returning.Any(c => c.Id == chosen) && !_state.ResolutionWillNotBowCardIds.Contains(chosen))
                _state.ResolutionWillNotBowCardIds.Add(chosen);
    }

    // ---- Magistrate's Stipend --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private ConditionalProduction? Wave737ConditionalProduction(CardInstance card) =>
        Definition(card).Effect == Wave737Abilities.StipendEffect ? new ConditionalProduction(3, ProductionCondition.PayingForKeyword, "Magistrate") : null;

    // ---- Verdant Jungle: "Your Nonhuman Personalities enter play from this Province for 1 less Gold" ------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave737Entry(EntryCostContext context)
    {
        if (context.Kind != EntryCostKind.Recruit || context.ProvinceIndex is not int province || Definition(context.Card).Type != CardType.Personality || !Keyword(context.Card, "Nonhuman")) yield break;
        if (_state.Cards.Values.Any(r => r.Zone == Zone.Region && r.OwnerId == context.PayerId && r.ProvinceIndex == province && Definition(r).Effect == Wave737Abilities.JungleEffect))
            yield return new(-1, "Verdant Jungle");
    }

    // ---- P8: which actions open the Interrupt windows ----------------------------------------------------------------------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex DiscardsTopCard = new("[Dd]iscard the top (\\d+ )?cards? of", System.Text.RegularExpressions.RegexOptions.Compiled);

    private bool Wave737ActionMoves(GameCommand command) =>
        command.AbilityId is { } abilityId && AbilityCatalog.All.TryGetValue(abilityId, out var ability)
        && (ActionMovers(command).Any() || ability.MovesToBattle || MoveOrStraightenKind(abilityId) == "move");

    // The battlefield an action moves units to: the current battlefield for a "move to the current battlefield" action, else the one the command names. Null when it moves nothing to a battlefield.
    private int? Wave737MoveDestination(GameCommand command)
    {
        if (!Wave737ActionMoves(command) || !AbilityCatalog.All.TryGetValue(command.AbilityId!, out var ability)) return null;
        if (ability.MovesToBattle || ability.Move?.Destination == MoveSpec.CurrentBattlefield) return _state.CurrentBattleProvince;
        return ability.Move?.Destination == MoveSpec.Home ? null : command.Province;
    }

    private bool Wave737ActionDiscards(GameCommand command) =>
        command.AbilityId is { } abilityId && AbilityCatalog.All.TryGetValue(abilityId, out var ability) && DiscardsTopCard.IsMatch(ability.Label);

    private bool Wave737ReactionApplies(CardInstance card, string abilityId, ReactionSubject subject)
    {
        if (subject.IsAttack) return false;
        switch (abilityId)
        {
            // "If the action moves any units": any action (either player's) that moves units.
            case Wave737Abilities.TaigoInterrupt:
                return card.Zone != Zone.Hand && Wave737ActionMoves(subject.Command);
            // "Void Interrupt": the Spell's Shugenja is Void, and the action discards a top card of a Fate deck.
            case Wave737Abilities.ReverseFatePlus:
            case Wave737Abilities.ReverseFateMinus:
                return card is { Zone: Zone.Attached, AttachedTo: { } hostId } && Keyword(Card(hostId), "Void") && Wave737ActionDiscards(subject.Command);
            // "Before an action resolves": an action that moves units to the battlefield of this Region's Province, while its owner is defending.
            case Wave737Abilities.JungleInterrupt:
                return card.Zone == Zone.Region && card.OwnerId != _state.ActivePlayerId && card.ProvinceIndex is int province && Wave737MoveDestination(subject.Command) is int destination
                    && BattlefieldProvinceIndex(destination) == province;
            default:
                return false;
        }
    }

    // ---- P8: the watches' answers ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static ActionWatchResponse Wave737TaigoAnswer(ActionWatchFact fact)
    {
        var engine = fact.Engine;
        var candidates = new List<string>();
        foreach (var moved in fact.Facts.Select(f => f.CardId).Distinct(StringComparer.Ordinal))
        {
            if (!engine._state.Cards.TryGetValue(moved, out var leader) || !engine.InPlayForWatch(leader)) continue;
            foreach (var card in new[] { leader }.Concat(engine.Attachments(leader.Id)))
                if (!engine.HasAttachments(card) && !candidates.Contains(card.Id)) candidates.Add(card.Id);
        }
        return candidates.Count == 0 ? ActionWatchResponse.Done
            : ActionWatchResponse.Ask(candidates, "Moto Taigo: destroy a card without attachments in one of the moved units?", static (e, f, chosen) =>
            {
                if (chosen is null || !e._state.Cards.TryGetValue(chosen, out var victim) || !e.InPlayForWatch(victim)) return;
                if (Definition(victim).Type == CardType.Personality) e.DestroyUnits([victim], false, f.Watch.OwnerPlayer);
                else e.DestroyAttachment(victim);
            }, optional: true);
    }

    private bool InPlayForWatch(CardInstance card) => InPlay(card);

    private static ActionWatchResponse Wave737FateAnswer(ActionWatchFact fact, int delta)
    {
        if (fact.Engine._state.Cards.TryGetValue(fact.First.CardId, out var discarded))
            fact.Engine.AddFocusModifier(discarded, fact.Watch.SourceId ?? "w737", "w737-reverse-fate", delta);
        return ActionWatchResponse.Done;
    }

    private static bool Wave737JungleGate(ActionMoveGate gate)
    {
        var engine = gate.Engine;
        if (!gate.Attacking || Definition(gate.Mover).Type != CardType.Personality || gate.Watch.SourceId is not { } regionId || !engine._state.Cards.TryGetValue(regionId, out var region)) return false;
        return region.Zone == Zone.Region && region.ProvinceIndex is int province && engine.BattlefieldProvinceIndex(gate.Battlefield) == province;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave737Hooks()
    {
        EngineHooks.Abilities.Register("Wave737Abilities", 13700, static abilities => abilities.AddWave737());
        EngineHooks.TargetAllows.Register("Wave737TargetAllows", 13700, static (engine, context) => engine.Wave737TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave737Validate", 13700, static (engine, context) => engine.Wave737Validate(context));
        EngineHooks.RunEffects.Register("Wave737RunEffects", 13700, static (engine, context) => engine.RunWave737(context));
        EngineHooks.ResolveEvent.Register("Wave737ResolveEvent", 13700, static (engine, context) => engine.ResolveWave737Event(context.Player, context.Province, context.Card));
        EngineHooks.ConditionalProductions.Register("Wave737ConditionalProductions", 13700, static (engine, card) => engine.Wave737ConditionalProduction(card));
        EngineHooks.EntryCostTerms.Register("Wave737Entry", 13700, static context => context.Engine.Wave737Entry(context));
        EngineHooks.CelestialEntryChoices.Register("Wave737Natsu", 13700, static (engine, context) => engine.Wave737NatsuEntry(context));
        EngineHooks.TargetedReaction.Register("Wave737TargetedReaction", 13700, static (engine, context) => engine.Wave737ReactionApplies(context.card, context.abilityId, context.subject));
        EngineHooks.UntargetedReaction.Register("Wave737UntargetedReaction", 13700, static (engine, context) => engine.Wave737ReactionApplies(context.card, context.abilityId, context.subject));

        // P6 traits: each watches its kind only in a match that has the card.
        FactRegistry.Trait("Wave737Seppuku", 13700, FactKinds.Seppuku, static (engine, fact) => engine.Wave737SeppukuFact(fact), activeIn: static engine => engine.Wave737Has(Wave737Abilities.KatsutaEffect));
        FactRegistry.Trait("Wave737Proclaimed", 13700, FactKinds.Proclaimed, static (engine, fact) => engine.Wave737ProclaimedFact(fact), activeIn: static engine => engine.Wave737Has(Wave737Abilities.KurokoEffect));
        FactRegistry.Trait("Wave737LeftPlay", 13700, FactKinds.LeftPlay, static (engine, fact) => engine.Wave737LeftPlayFact(fact),
            activeIn: static engine => engine.Wave737Has(Wave737Abilities.ShibatsuEffect) || engine.Wave737Has(Wave737Abilities.TadamaEffect));
        FactRegistry.Trait("Wave737Assigned", 13700, FactKinds.AssignedToAttack, static (engine, fact) => engine.Wave737AssignedFact(fact), activeIn: static engine => engine.Wave737Has(Wave737Abilities.WeatherEffect));
        FactRegistry.Trait("Wave737TerrainDestroyed", 13700, FactKinds.TerrainDestroyed, static (engine, fact) => engine.Wave737TerrainDestroyedFact(fact), activeIn: static engine => engine._state.CrystalTearsTargets is not null);

        // The announce window (P6): Magistrate's Stipend's Reaction, for a non-Iaijutsu action announced with a Magistrate performing.
        AnnounceRegistry.Register("Wave737Stipend", 13700, Wave737Abilities.StipendAnnounce, static context =>
            context.PerformerId is { } performerId && context.Engine._state.Cards.TryGetValue(performerId, out var performer) && performer.OwnerId == context.Command.PlayerId
            && context.Engine.Keyword(performer, "Magistrate") && !context.Engine.ActionCarriesKeywordForWave(context.Ability, context.Source, "Iaijutsu"));

        // P8 callbacks.
        ActionWatchRegistry.Fired("Wave737Taigo", 13700, Wave737Abilities.TaigoCallback, static fact => Wave737TaigoAnswer(fact));
        ActionWatchRegistry.Fired("Wave737FatePlus", 13700, Wave737Abilities.FatePlusCallback, static fact => Wave737FateAnswer(fact, 1));
        ActionWatchRegistry.Fired("Wave737FateMinus", 13700, Wave737Abilities.FateMinusCallback, static fact => Wave737FateAnswer(fact, -1));
        ActionWatchRegistry.Gate("Wave737Jungle", 13700, Wave737Abilities.JungleCallback, static gate => Wave737JungleGate(gate));

        // P7: Ikeuchi's Followers. "Before the first time each battle any of Ikeuchi's Followers are destroyed by another player's card's effect: If you have Reconnaissance, negate the destruction of one such Follower."
        EngineHooks.StandingWatches.Register("Wave737Ikeuchi", 13700, static () => new StandingWatch(
            Id: "w737-ikeuchi", OwnerIs: StandingWatch.OnEffect(Wave737Abilities.IkeuchiEffect), Kind: WatchKind.Destroy, Scope: WatchScope.AttachedFollowers, Source: WatchSource.OtherPlayers, Period: Period.Battle,
            Condition: static (engine, _, carrier) => engine.HasReconnaissanceAtCurrentBattlefield(carrier.OwnerId)));
    }

    internal bool ActionCarriesKeywordForWave(AbilityDefinition ability, CardInstance? source, string keyword) => ActionCarriesKeyword(ability, source, keyword);
}

/// <summary>What an untargeted action's Interrupt asks (Wave737): the card, the Reaction, and the held action.</summary>
internal readonly record struct UntargetedReactionContext(CardInstance card, string abilityId, MatchEngine.ReactionSubject subject);

internal static partial class EngineHooks
{
    /// <summary>Reactions to the held action that need no target (P8's Interrupts: the card names the action's effect, not a card it targets). Consulted when the action names no card in play.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, UntargetedReactionContext, bool>> UntargetedReaction = new();
}
