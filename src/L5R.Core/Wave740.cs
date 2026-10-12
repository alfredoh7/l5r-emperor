using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #740, wave batch 9: the duel and rulebook-modifier cards, wired on the merged P17 (an additional action scoped to named abilities), P18 (duel rules: an exact focus count, a refusal condition, the
// substitute duelist) and P19 (rulebook modifiers: Tactical Advantage's amount and timing, Equip from the discard pile, Equip (Battle) waivers, Favor (Battle)'s Absent and any target, card-free timing
// grants). Every legality and every effect is decided in Core. Ability ids are "w740-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-9-duels-and-rulebook-modifiers.md.
// Waves are those of tools/content/package_wave_740.py.
//
//   Akodo Kano - exp          "Kano's Force bonuses from the rulebook Tactical Advantage action are increased by 2."                                          a P19 amount (+2) for Tactical Advantage when he performs it
//   Doji Hakuseki - exp3      "After you Proclaim another Personality: Gain 1 Honor." / "Iaijutsu Battle: ... Bow the duel's loser. Destroy the duel's loser if the duel was against The Dark Naga."   a P6 Proclaimed trait; w740-hakuseki-duel
//   Ikoma Ayumu - exp         "Will not gain Fallen or Shadowlands." / "The rulebook Tactical Advantage is an Open action for you if you target Ayumu with it."   a P10 gain block on entry; a P19 timing (Battle/Open) when he performs it
//   Mirumoto Kalen - exp      "Fire Battle: Ranged 3 Attack. You may take an additional action to use Kalen's second printed ability." / "Fire Battle, bow: Ranged 3 Attack."   w740-kalen-attack / w740-kalen-attack2; a P17 grant scoped to the second
//   Seppun Ryota              "Reaction: Before an action resolves that targeted your Personality at Ryota's location: He enters any duel created by its effects instead of the target."   P18's substitute duelist
//   Yoritomo Harumi           "Once per battle, Harumi may perform the Equip Battle action even if bowed and even if unopposed." / "Battle: Target an enemy Follower or Personality without Followers: ..."   a P19 exemption, once per battle; w740-harumi-attack
//   Frontier Farmer           "You may Equip this Follower from your discard pile."                                                                          a P19 source pile
//   Fortune's Charm           "As a Focus Effect, the duel's winner may take an additional action." / "-2 strength to Ranged and Melee Attacks targeting this Personality." / "Before another player's card destroys this Personality during the Action Phase, negate the destruction and destroy this Item."   w740-charm-focus; the Attack defense fields; a P7 standing watch
//   Focus on the Flame        "Fire Iaijutsu Battle, bow: This Shugenja challenges a target enemy Personality. During the duel, if this Shugenja is Fire, he has Duelist. Bow the duel's loser. Destroy this Spell."   w740-flame-duel; a P10 keyword rule read only in that duel
//   A Magistrate Falls        "Iaijutsu Limited, 2 Gold: Your target unbowed Personality challenges a target Personality. The challenge may be refused unless exactly one of the targets is Fallen or The Dark Naga. Destroy the duel's loser."   w740-magistrate-duel; a P18 refusal condition
//   Demonstrating Technique   "Iaijutsu Political Limited: ... Each Personality must focus exactly once, if possible. The winner draws a card. Dishonor the loser."   w740-demonstrating-duel; P18's exact focus count
//   Practicing Kata           "You may Equip as a Battle/Open action once per turn." / "Limited: Put this Kata into play. Discard all your other Kata in play."   a Kata; a P19 card-free once-per-turn timing grant while it is in play
//   Harbingers of War         "Until the game ends, equipping Followers and Items from your hand with a base Gold Cost of 1 to 4 is a Battle/Open action for you and, after the first time each turn you do so, draw a card."   a game-long P19 timing grant narrowed to those cards; a draw after the equip
//   Hantei's Guidance         "The rulebook's Favor Political Battle action has Absent for you, and you may target any Personality with it." / "Political Interrupt: Discard this Celestial. Negate a Political action."   a P19 Absent and any-target; w740-hantei-negate
//
// Issue #835: The Empire's Foe Exposed was wired afterwards in Wave835.cs, on Mendon's rulings of 2026-10-11 (docs/decisions/2026-10-11-twelve-ruled-cards.md); the "not wired" note here is the state when this wave merged.
// Not wired, with a blocker note (tools/content/pe_wave_740_blockers.json): The Empire's Foe Exposed (an Event whose duel is issued as it is revealed: no merged path starts a duel from an Event's reveal).
public static class Wave740Abilities
{
    public const string HakusekiDuel = "w740-hakuseki-duel", KalenAttack = "w740-kalen-attack", KalenAttack2 = "w740-kalen-attack2", RyotaSubstitute = "w740-ryota-substitute", HarumiAttack = "w740-harumi-attack",
        CharmFocus = "w740-charm-focus", FlameDuel = "w740-flame-duel", MagistrateDuel = "w740-magistrate-duel", DemonstratingDuel = "w740-demonstrating-duel", KataEnter = "w740-kata-enter",
        HanteiNegate = "w740-hantei-negate";

    // The cards (oracle ids).
    public const string Kano = "oracle-l5r-10849", Hakuseki = "oracle-l5r-10445", Ayumu = "oracle-l5r-10638", Kalen = "oracle-l5r-10845", Ryota = "oracle-l5r-10324", Harumi = "oracle-l5r-10298",
        Farmer = "oracle-l5r-10681", Charm = "oracle-l5r-10904", Flame = "oracle-l5r-10710", Magistrate = "oracle-l5r-10559", Demonstrating = "oracle-l5r-10933", Kata = "oracle-l5r-10731",
        Harbingers = "oracle-l5r-10260", Hantei = "oracle-l5r-10417";

    // Catalogue Effects (a card names its own traits with one).
    public const string KanoEffect = "w740-kano", HakusekiEffect = "w740-hakuseki", AyumuEffect = "w740-ayumu", HarumiEffect = "w740-harumi", FarmerEffect = "w740-farmer", CharmEffect = "w740-charm",
        FlameEffect = "w740-flame", KataEffect = "w740-kata", HarbingersEffect = "w740-harbingers", HanteiEffect = "w740-hantei";

    /// <summary>Target category of Harumi's Battle action: an enemy Follower, or an enemy Personality without Followers.</summary>
    public const string FollowerOrBarePersonality = "w740-follower-or-bare-personality";

    /// <summary>A Magistrate Falls' RefusableWhen key: refusable unless exactly one of the two duelists is Fallen or The Dark Naga.</summary>
    public const string MagistrateRefusable = "w740-magistrate-refusable";

    /// <summary>The timing-grant filter of Harbingers of War: a Follower or an Item with a printed Gold Cost of 1 to 4.</summary>
    public const string HarbingersFilter = "w740-harbingers-equip";

    public const string DarkNaga = "The Dark Naga";

    public static Dictionary<string, AbilityDefinition> AddWave740(this Dictionary<string, AbilityDefinition> all)
    {
        all[HakusekiDuel] = new("Battle", "Iaijutsu Battle: Hakuseki challenges a target enemy Personality. Bow the duel's loser. Destroy the duel's loser if the duel was against The Dark Naga", "enemy-personality",
            IssuesDuel: true, ActionKeywords: ["Iaijutsu"]);
        all[KalenAttack] = new("Battle", "Fire Battle: Ranged 3 Attack. You may take an additional action to use Kalen's second printed ability", "attack", ActionKeywords: ["Fire"]);
        all[KalenAttack2] = new("Battle", "Fire Battle, bow this card: Ranged 3 Attack", "attack", ActionKeywords: ["Fire"], Costs: ActionCost.BowSource);
        all[HarumiAttack] = new("Battle", "Battle: Target an enemy Follower or Personality without Followers: Give it -5F. Gain 2 Honor. If its Force is now 0 or you control Yoritomo Hiromi, destroy it. If it is still in play, you may take an additional Battle action",
            FollowerOrBarePersonality);
        all[CharmFocus] = new("FocusEffect", "As a Focus Effect: The duel's winner may take an additional action", "none");
        all[FlameDuel] = new("Battle", "Fire Iaijutsu Battle, bow this card: This Shugenja challenges a target enemy Personality. During the duel, if this Shugenja is Fire, he has Duelist. Bow the duel's loser. Destroy this Spell",
            "enemy-personality", Performer: "own-wielder", IssuesDuel: true, ActionKeywords: ["Fire", "Iaijutsu"], Costs: ActionCost.BowSource);
        all[MagistrateDuel] = new("Limited", "Iaijutsu Limited, 2 Gold: Your target unbowed Personality challenges a target Personality. The challenge may be refused unless exactly one of the targets is Fallen or The Dark Naga. Destroy the duel's loser",
            "personality", Performer: "own-unbowed-personality", IssuesDuel: true, DuelRefusable: true, Cost: GoldCostRule.Fixed(2), ActionKeywords: ["Iaijutsu"]);
        all[DemonstratingDuel] = new("Limited", "Iaijutsu Political Limited: Your target unbowed Personality challenges another player's target Personality. Each Personality must focus exactly once, if possible. The winner draws a card. Dishonor the loser",
            "enemy-personality", Performer: "own-unbowed-personality", IssuesDuel: true, ActionKeywords: ["Iaijutsu", "Political"]);
        all[KataEnter] = new("Limited", "Limited: Put this Kata into play. Discard all your other Kata in play", "none");
        all[HanteiNegate] = new("Reaction", "Political Interrupt, discard this Celestial: Negate a Political action", "none", Trigger: ReactionTriggers.TargetedByAction, Costs: [new(CostVerbs.DiscardFromPlay)], ActionKeywords: ["Political"]);
        return all;
    }
}

public sealed partial class MatchState
{
    // 22960-22969 is reserved for batch 9 of the wirable-card waves (issue #740). Omitted from the saved game until used.
    /// <summary>The card-free timing grant each Practicing Kata in play gave its controller (the Kata's id to the grant's id); the grant goes when the Kata leaves play.</summary>
    [JsonPropertyOrder(22960)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Wave740KataGrants { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave740Is(CardInstance? card, string effect) => card is not null && Definition(card).Effect == effect;

    private bool Wave740Is(string? cardId, string effect) => cardId is not null && _state.Cards.TryGetValue(cardId, out var card) && Wave740Is(card, effect);

    private bool Wave740Controls(int playerId, string effect) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == effect);

    // ---- P19: the rulebook modifiers ---------------------------------------------------------------------------------------------------------------------------------------------------

    private RulebookModifier? Wave740Modifier(RulebookContext context)
    {
        switch (context.AbilityId)
        {
            // Kano: "Kano's Force bonuses from the rulebook Tactical Advantage action are increased by 2." Ayumu: "The rulebook Tactical Advantage is an Open action for you if you target Ayumu with it."
            case "tactical-advantage":
                if (Wave740Is(context.PerformerId, Wave740Abilities.KanoEffect)) return new RulebookModifier(AmountDelta: 2);
                if (Wave740Is(context.PerformerId, Wave740Abilities.AyumuEffect)) return new RulebookModifier(TimingOverride: "BattleOrOpen");
                return null;
            // Harumi: "Once per battle, Harumi may perform the Equip Battle action even if bowed and even if unopposed."
            case "equip-battle":
                return Wave740Is(context.PerformerId, Wave740Abilities.HarumiEffect) && PeriodCountOf("w740-harumi:" + context.PerformerId, Period.Battle) == 0
                    ? new RulebookModifier(Exemptions: RulebookExemptions.Bowed | RulebookExemptions.Unopposed) : null;
            // Frontier Farmer: "You may Equip this Follower from your discard pile."
            case "equip":
                return Wave740Is(context.Command?.CardId, Wave740Abilities.FarmerEffect) ? new RulebookModifier(SourcePileAllowed: RulebookSourcePile.Discard) : null;
            // Hantei's Guidance: "The rulebook's Favor Political Battle action has Absent for you, and you may target any Personality with it."
            case "favor-battle":
                return Wave740Controls(context.PlayerId, Wave740Abilities.HanteiEffect) ? new RulebookModifier(Absent: true, AnyTarget: true) : null;
            default:
                return null;
        }
    }

    // Harumi's waiver is spent only when she needed it: bowed, or no enemy army at the battlefield (a use that needed no exemption leaves the one use for later).
    private void Wave740NoteEquipBattle(CardInstance performer, int playerId)
    {
        if (!Wave740Is(performer, Wave740Abilities.HarumiEffect)) return;
        var opposed = _state.CurrentBattleProvince is int province && Army(1 - playerId, province).Any();
        if (performer.Bowed || !opposed) AddPeriodCount("w740-harumi:" + performer.Id, Period.Battle);
    }

    // The filter of a card-free timing grant (Harbingers of War): a Follower or an Item with a printed Gold Cost of 1 to 4.
    private bool TimingGrantFilterAllows(string filter, CardInstance attachment) =>
        filter == Wave740Abilities.HarbingersFilter && Definition(attachment).Type is CardType.Item or CardType.Follower && Definition(attachment).GoldCost is >= 1 and <= 4;

    // Practicing Kata's grant goes when the Kata is no longer in play (discarded by another Kata, destroyed...). Read before every free grant is asked.
    private void PruneWave740KataGrants()
    {
        if (_state.Wave740KataGrants is not { Count: > 0 } grants) return;
        foreach (var (kataId, grantId) in grants.ToArray())
        {
            if (_state.Cards.TryGetValue(kataId, out var kata) && kata.Zone == Zone.Home) continue;
            RemovePlayerTimingGrant(grantId);
            grants.Remove(kataId);
        }
        if (grants.Count == 0) _state.Wave740KataGrants = null;
    }

    // Harbingers of War: "after the first time each turn you do so, draw a card". Do so: equip a Follower or Item from the hand with a printed Gold Cost of 1 to 4 once the Event has resolved.
    private void AfterWave740Equip(int playerId, CardInstance card, bool fromDiscard)
    {
        if (fromDiscard || !TimingGrantFilterAllows(Wave740Abilities.HarbingersFilter, card)) return;
        if (_state.PlayerTimingGrants?.Any(g => g.PlayerId == playerId && g.Filter == Wave740Abilities.HarbingersFilter) != true) return;
        if (PeriodCountOf("w740-harbingers:" + playerId, Period.Turn) > 0) return;
        AddPeriodCount("w740-harbingers:" + playerId, Period.Turn);
        Emit("harbingersDraw", $"Harbingers of War: {Player(playerId).Name} draws a card.", playerId, card.Id);
        Draw(Player(playerId));
    }

    // ---- entry ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Ayumu's entry trait: "will not gain Fallen or Shadowlands" (P10), a block that stands while he is in play. Called by ApplyMandatoryEntryEffects.</summary>
    private void ApplyWave740EntryEffects(CardInstance card)
    {
        if (Wave740Is(card, Wave740Abilities.AyumuEffect)) AddKeywordGainBlock(card.Id, card.Id, null, keywords: ["Fallen", "Shadowlands"]);
    }

    // ---- targets and validation -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave740TargetAllows(TargetAllowsContext context) =>
        context.ability.Target == Wave740Abilities.FollowerOrBarePersonality && InPlay(context.target) && context.target.OwnerId != context.player.Id
        && (Definition(context.target).Type == CardType.Follower && context.target.Zone == Zone.Attached
            || Definition(context.target).Type == CardType.Personality && !Attachments(context.target.Id).Any(a => Definition(a).Type == CardType.Follower));

    private void Wave740Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        switch (id)
        {
            case Wave740Abilities.KataEnter:
                break;
            case Wave740Abilities.MagistrateDuel:
                Require(command.TargetId is null || command.TargetId != command.PerformerId, "Choose a Personality other than the challenger.");
                break;
        }
    }

    // ---- effects ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave740(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case "equip-battle":
                // Not this file's ability: Harumi's once-per-battle waiver is spent here, then the engine's own effect runs.
                if (c.command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)) Wave740NoteEquipBattle(performer, player.Id);
                return null;
            case Wave740Abilities.KalenAttack:
            case Wave740Abilities.KalenAttack2:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(c.ability, c.command, source).Id, c.consumedGrant);
            case Wave740Abilities.HarumiAttack:
                {
                    var name = Definition(source!).Name;
                    ApplyModifier(target!, source!.Id, id, "Force", -5);
                    GainHonor(player.Id, 2, name, source);
                    // "If its Force is now 0 or you control Yoritomo Hiromi, destroy it."
                    if (InPlay(target!) && (CardForce(target!) == 0 || ControlsCopyOf(player.Id, "Yoritomo Hiromi"))) DestroyUnits([target!], actingPlayerId: player.Id);
                    // "If it is still in play, you may take an additional Battle action."
                    if (InPlay(target!)) GrantAdditionalAction(player.Id, null);
                    return false;
                }
            case Wave740Abilities.KataEnter:
                {
                    EnterPlayAsKata(player, source!);
                    (_state.Wave740KataGrants ??= [])[source!.Id] = GrantPlayerTiming(player.Id, ActionClass.EquipAny, "BattleOrOpen", oncePerTurn: true);
                    return false;
                }
            case Wave740Abilities.HanteiNegate:
                NegateHeldAction(source!);
                return false;
            default:
                return null;
        }
    }

    // ResolveAttackNow's hook: Kalen's first ability grants "an additional action to use his second printed ability", whether or not the Attack hit.
    private void AfterWave740Attack(string abilityId, int playerId, string? sourceId)
    {
        if (abilityId != Wave740Abilities.KalenAttack || sourceId is null || !_state.Cards.TryGetValue(sourceId, out var kalen) || !InPlay(kalen)) return;
        GrantAdditionalAction(playerId, [kalen.Id], scopeAbilityIds: [Wave740Abilities.KalenAttack2]);
    }

    private static int? Wave740AttackStrength(string abilityId) => abilityId is Wave740Abilities.KalenAttack or Wave740Abilities.KalenAttack2 ? 3 : null;

    private static string? Wave740AttackKind(string abilityId) => abilityId is Wave740Abilities.KalenAttack or Wave740Abilities.KalenAttack2 ? "Ranged" : null;

    // ---- duels -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave740AgainstDarkNaga(PendingDuel duel) => Definition(Card(duel.ChallengedId)).Name == Wave740Abilities.DarkNaga || Definition(Card(duel.ChallengerId)).Name == Wave740Abilities.DarkNaga;

    /// <summary>Hakuseki's duel destroys its loser when it was against The Dark Naga (TfStrategiesBDuelDestroysLoser's term).</summary>
    private bool Wave740DuelDestroysLoser(PendingDuel duel, CardInstance loser) => duel.AbilityId == Wave740Abilities.HakusekiDuel && Wave740AgainstDarkNaga(duel);

    // "exactly one of the targets is Fallen or The Dark Naga": the challenge may be refused unless this holds.
    private bool Wave740FallenOrNaga(CardInstance card) => Keyword(card, "Fallen") || HasClanAffiliation(card, "Fallen") || Definition(card).Name == Wave740Abilities.DarkNaga;

    private bool Wave740MagistrateRefusable(CardInstance challenger, CardInstance challenged) => Wave740FallenOrNaga(challenger) == Wave740FallenOrNaga(challenged);

    private void ApplyWave740DuelOutcome(string abilityId, string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        var losers = loserIds.Select(Card).ToArray();
        switch (abilityId)
        {
            case Wave740Abilities.HakusekiDuel:
                foreach (var loser in losers) if (InPlay(loser) && !StalwartNegatesBow(loser, challengerPlayerId)) loser.Bowed = true;
                if (_state.PendingDuel is { } duel && Wave740AgainstDarkNaga(duel)) DestroyUnits(losers.Where(InPlay).ToArray(), actingPlayerId: challengerPlayerId);
                break;
            case Wave740Abilities.FlameDuel:
                foreach (var loser in losers) if (InPlay(loser) && !StalwartNegatesBow(loser, challengerPlayerId)) loser.Bowed = true;
                // "Destroy this Spell."
                if (_state.PendingDuel?.SourceCardId is { } spellId && _state.Cards.TryGetValue(spellId, out var spell) && spell.Zone == Zone.Attached) DestroyAttachment(spell);
                break;
            case Wave740Abilities.MagistrateDuel:
                DestroyUnits(losers.Where(InPlay).ToArray(), actingPlayerId: challengerPlayerId);
                break;
            case Wave740Abilities.DemonstratingDuel:
                // "The winner draws a card. Dishonor the loser." A tie has no winner and both Personalities lose.
                if (winnerId is not null) Draw(Player(Card(winnerId).OwnerId));
                foreach (var loser in losers) if (InPlay(loser)) Dishonor(loser);
                break;
        }
    }

    // Fortune's Charm: "As a Focus Effect, the duel's winner may take an additional action."
    private void ApplyWave740FocusEffect(string abilityId, int? winnerPlayerId)
    {
        if (abilityId == Wave740Abilities.CharmFocus && winnerPlayerId is int winner) GrantAdditionalAction(winner, null);
    }

    // ---- Hakuseki's Proclaim trait ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave740ProclaimedFact(FactRecord fact)
    {
        // "After you Proclaim another Personality: Gain 1 Honor." One gain for each Hakuseki the Proclaiming player controls in play, other than the one Proclaimed.
        foreach (var hakuseki in _state.Cards.Values.Where(c => c.OwnerId == fact.PlayerId && InPlay(c) && Definition(c).Effect == Wave740Abilities.HakusekiEffect && c.Id != fact.CardId).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            GainHonor(fact.PlayerId, 1, Definition(hakuseki).Name, hakuseki);
    }

    // ---- Hantei's Guidance: the Political Interrupt ----------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave740PoliticalAction(ReactionSubject subject)
    {
        if (subject.IsAttack || subject.Command.AbilityId is not { } actingId || actingId == Wave740Abilities.HanteiNegate || !AbilityCatalog.All.TryGetValue(actingId, out var acting)) return false;
        return ActionCarriesKeyword(acting, subject.Command.CardId is not null && _state.Cards.TryGetValue(subject.Command.CardId, out var actingSource) ? actingSource : null, "Political");
    }

    // ---- Fortune's Charm: the destruction it negates ---------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave740CharmCovers(WatchContext context, CardInstance carrier) =>
        _state.Phase == Phase.Action && carrier.Zone == Zone.Attached && context.Target is not null && carrier.AttachedTo == context.Target.Id;

    private void Wave740CharmSpent(CardInstance carrier)
    {
        // "... negate the destruction and destroy this Item."
        if (carrier.Zone == Zone.Attached) DestroyAttachment(carrier);
    }

    // ---- Harbingers of War -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave740Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave740Abilities.HarbingersEffect) return null;
        // "Until the game ends, equipping Followers and Items from your hand with a base Gold Cost of 1 to 4 is a Battle/Open action for you ..."
        GrantPlayerTiming(player.Id, ActionClass.EquipAny, "BattleOrOpen", filter: Wave740Abilities.HarbingersFilter);
        Emit("eventResolved", $"{Definition(card).Name}: until the game ends, {player.Name} may equip Followers and Items of printed Gold Cost 1 to 4 as a Battle/Open action, and draws after the first each turn.", player.Id, card.Id);
        return DiscardResolvedEvent(player, province, card);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave740Hooks()
    {
        EngineHooks.Abilities.Register("Wave740Abilities", 13950, static abilities => abilities.AddWave740());
        EngineHooks.TargetAllows.Register("Wave740TargetAllows", 13950, static (engine, context) => engine.Wave740TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave740Validate", 13950, static (engine, context) => engine.Wave740Validate(context));
        EngineHooks.RunEffects.Register("Wave740RunEffects", 13950, static (engine, context) => engine.RunWave740(context));
        EngineHooks.ResolveEvent.Register("Wave740ResolveEvent", 13950, static (engine, context) => engine.ResolveWave740Event(context.Player, context.Province, context.Card));
        EngineHooks.RulebookModifier.Register("Wave740Rulebook", 13950, static (engine, context) => engine.Wave740Modifier(context));
        EngineHooks.AttackStrengthBefore.Register("Wave740AttackStrength", 13950, static (engine, context) => Wave740AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("Wave740AttackKind", 13950, static context => Wave740AttackKind(context.abilityId));
        FactRegistry.Trait("Wave740Proclaimed", 13950, FactKinds.Proclaimed, static (engine, fact) => engine.Wave740ProclaimedFact(fact), activeIn: static engine => engine._state.Cards.Values.Any(c => Definition(c).Effect == Wave740Abilities.HakusekiEffect));

        // Seppun Ryota: the substitute duelist (P18).
        DuelRuleRegistry.Substitute("Wave740Ryota", 13950, Wave740Abilities.RyotaSubstitute, "Reaction: Before an action resolves that targeted your Personality at Ryota's location: He enters any duel created by its effects instead of the target");
        // Demonstrating Technique: "Each Personality must focus exactly once, if possible." A Magistrate Falls: the refusal condition.
        DuelRuleRegistry.ForAbility("Wave740DemonstratingRules", 13950, Wave740Abilities.DemonstratingDuel, new DuelRules { ExactFocusEach = 1 });
        DuelRuleRegistry.ForAbility("Wave740MagistrateRules", 13950, Wave740Abilities.MagistrateDuel, new DuelRules { RefusableWhen = Wave740Abilities.MagistrateRefusable });
        DuelRuleRegistry.RefusableWhen("Wave740MagistrateRefusable", 13950, Wave740Abilities.MagistrateRefusable, static context => context.Engine.Wave740MagistrateRefusable(context.Challenger, context.Challenged));

        // Focus on the Flame: "During the duel, if this Shugenja is Fire, he has Duelist" (P10): the challenger of this ability's duel, while the Spell is attached to him.
        KeywordRuleCatalog.Register(new KeywordRule("w740-flame-duelist", KeywordRuleScope.Unit, ["Duelist"], SourceEffect: Wave740Abilities.FlameEffect,
            AllOf: [new KeywordCondition(KeywordConditionKind.SubjectIsType, "Personality"), new KeywordCondition(KeywordConditionKind.SubjectHasKeyword, "Fire"),
                new KeywordCondition(KeywordConditionKind.SubjectChallengesWithAbility, Wave740Abilities.FlameDuel)]));

        // Hantei's Guidance: a Political Interrupt to any held Political action, named or not.
        EngineHooks.TargetedReaction.Register("Wave740TargetedReaction", 13950, static (engine, context) => context.abilityId == Wave740Abilities.HanteiNegate && engine.Wave740PoliticalAction(context.subject));
        EngineHooks.UntargetedReaction.Register("Wave740UntargetedReaction", 13950, static (engine, context) => context.abilityId == Wave740Abilities.HanteiNegate && engine.Wave740PoliticalAction(context.subject));

        // Fortune's Charm (P7): before another player's card destroys the host during the Action Phase, negate the destruction and destroy the Item.
        EngineHooks.StandingWatches.Register("Wave740Charm", 13950, static () => new StandingWatch(
            Id: "w740-charm", OwnerIs: StandingWatch.OnEffect(Wave740Abilities.CharmEffect), Kind: WatchKind.Destroy, Scope: WatchScope.OwnersCards, TargetType: CardType.Personality, Source: WatchSource.OtherPlayers, Period: Period.Turn,
            Condition: static (engine, context, carrier) => engine.Wave740CharmCovers(context, carrier), OnNegated: static (engine, _, carrier) => engine.Wave740CharmSpent(carrier)));
    }
}
