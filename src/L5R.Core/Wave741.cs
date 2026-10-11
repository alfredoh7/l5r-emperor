namespace L5R.Core;

// Issue #741, wave batch 10: the created-card, Fortification and small-effect cards, wired on the merged P15 (Fortification Holdings), P22 (created cards, copies, grants, exchanges, transfers) and P24 (switch, adjacent
// move, Attack scope, shuffles, cost cards, the three-stat token). Every legality and every effect is decided in Core. Ability ids are "w741-". Readings for Mendon to confirm or correct are in
// docs/decisions/2026-10-08-wave-batch-10-created-cards-and-small-effects.md. Waves are those of tools/content/package_wave_741.py.
//
//   Kitsune Merihiko - exp    "Your Nonhuman Spirits have the ability, 'Battle, Bow: Ranged 3 Attack.'"                                                       a P22 filter grant; w741-merihiko-ranged
//   Kitsune Yuribara          "Reaction: After Yuribara enters play: Create a 0F/2C/3PH ... Butterfly ... with the traits ..."                                 w741-yuribara-create; the Butterflies' two traits (P22 trait, P11-shaped forced target)
//   Ninube Tsukau             "After Tsukau enters play: Lose 3 Honor." / "Ninja Open: Once per game, target a non-Unique Human Personality. ... copies ..."     EntryHonorLoss 3; w741-tsukau-copy (P22 CopyStat, permanent BeginCopy)
//   Shinjo Kinto - exp2       "Battle: Target two Personalities at different locations controlled by the same player. Switch their locations. ..."             P24 SwitchLocations; w741-kinto-switch
//   Tamori Tsuchiya           "Battle: If Tsuchiya is defending, destroy one of his Spells: Give him Stalwart and a Force bonus equal to his current Force."   w741-tsuchiya-stalwart (P24 cost card)
//   Watersteel Sword          "Water Open: Target a non-Unique Weapon attached to this Personality. Copy an ability on it ..., to this Weapon (until the turn ends)."   w741-watersteel-copy (BeginCopy on an attachment)
//   Befriend the Tides        "Water Battle, Bow: Move this Shugenja from the current battlefield to an adjacent battlefield."                                 P24 Move(Wielder, AdjacentUnresolved)
//   Constructing the Self     "Air Limited, Bow: This Spell may remain bowed. Create a 0F Illusion ... with this Shugenja's Chi and Personal Honor. After the next time this game this Spell straightens or leaves play, remove the Illusion."   P22 current values and a removal watch; choose-straighten
//   Koan's Whisper            "Void Limited, Bow: Reduce a Province's strength to 0. If its player has two or more Provinces, you may remove five Koan cards you control from the game to destroy it."   two modes of one action: w741-whisper-reduce, w741-whisper-destroy
//   A Doomseeker's Tomb       "Limited, 1 Gold: Give your target Personality a +1F/+1C/+1PH token."                                                            P24 three-stat token
//   Harmony                   "Open: Choose your performing Monk: Permanently give him the trait, 'You may bow this card instead of bowing a Ring to pay a cost.'"   P24 GrantBowInsteadOfRing
//   Kitsuki Judgment          "Limited: Choose your performing unbowed Magistrate, target a Human Personality, and pay Gold equal to his unit's total Gold Cost, +8 unless ...: He commits seppuku."   TargetUnitTotal plus a P2 cost term
//   Overwhelming Chaos        "Open: Each player shuffles his hand into his deck, then draws as many cards as he shuffled into his deck."                       P24 ShuffleHandsAndRedraw
//   Shifting Waves            "Battle: Choose your performing unbowed Henshin and target a Personality: Switch the target's Force and Chi."                     P22 ExchangeStats
//   Sorrowful Prayer          "Limited: Shuffle one to three target non-Unique Strategies in your discard pile into your deck. Remove this card from the game."  P24 ShuffleIntoDeck
//   Spiritual Coalescence     "Battle/Open: Destroy your performing Spirit Personality and target another Spirit Personality: ... permanent Force bonus ... permanently copies ... You may take an additional action."   permanent modifier and grants
//   Strength in the Earth     "Earth Battle: Target your unbowed Monk. Melee Attack with strength equal to his Force, which may target a card in the Defender's home."   P24 AttackSpec (DefenderHome)
//   Unexpected Discovery      "Battle: Move your target opposed Personality (at the current battlefield) to an adjacent battlefield."                           P24 Move(Target, AdjacentUnresolved)
//   Unsettling Gathering      "Limited, X: Target an attachment with Gold Cost equal to or lower than the amount paid. Transfer it to any player's target Personality."   P22 TransferAttachment with a change of control
//   A Moving Battle           "Before this battle resolves, you may move one of your opposed attacking Personalities at this battlefield to an adjacent, unresolved battlefield." / Battle: the generic Terrain rule   a before-resolution trait of the Terrain (answered through beforeBattleOptional, like Lakeside Retreat); P24 MoveSpec adjacent unresolved
//   The Tower of Vigilance    "Engage, Bow: If you have any units at this battlefield, create a 0F/2C/2PH Samurai - Guard Personality there. Remove him from the game after this battle ends."   P15 Fortification; a created Guard
//
// Not wired, with a blocker note (tools/content/pe_wave_741_blockers.json): see that file.
public static class Wave741Abilities
{
    public const string MerihikoRanged = "w741-merihiko-ranged", YuribaraCreate = "w741-yuribara-create", TsukauCopy = "w741-tsukau-copy", KintoSwitch = "w741-kinto-switch", TsuchiyaStalwart = "w741-tsuchiya-stalwart",
        WatersteelCopy = "w741-watersteel-copy", TidesMove = "w741-tides-move", SelfCreate = "w741-self-create", WhisperReduce = "w741-whisper-reduce", WhisperDestroy = "w741-whisper-destroy",
        TombToken = "w741-tomb-token", Harmony = "w741-harmony", Judgment = "w741-judgment", Chaos = "w741-chaos", ShiftingWaves = "w741-shifting-waves", Prayer = "w741-prayer", Coalescence = "w741-coalescence",
        StrengthEarth = "w741-strength-earth", Discovery = "w741-discovery", Gathering = "w741-gathering", TowerEngage = "w741-tower-engage", MovingBattle = "w741-moving-battle";

    // The cards (oracle ids).
    public const string Merihiko = "oracle-l5r-10855", Yuribara = "oracle-l5r-10296", Tsukau = "oracle-l5r-10319", Kinto = "oracle-l5r-10888", Tsuchiya = "oracle-l5r-10288", Watersteel = "oracle-l5r-10689",
        Tides = "oracle-l5r-10708", TheSelf = "oracle-l5r-10700", Whisper = "oracle-l5r-10540", Tomb = "oracle-l5r-10739", HarmonyCard = "oracle-l5r-10377", JudgmentCard = "oracle-l5r-10382", ChaosCard = "oracle-l5r-10549",
        Waves = "oracle-l5r-10392", PrayerCard = "oracle-l5r-10745", CoalescenceCard = "oracle-l5r-10393", Earth = "oracle-l5r-10556", DiscoveryCard = "oracle-l5r-10730", GatheringCard = "oracle-l5r-10711",
        Tower = "oracle-l5r-10612", MovingBattleCard = "oracle-l5r-10925";

    // The created cards' scans (published ahead of their creators by issue #677).
    public const string Butterflies = "oracle-l5r-10808", MinorIllusion = "oracle-l5r-11341", TowerGuard = "oracle-l5r-11345";

    // Catalogue Effects (a card names its own traits with one).
    public const string MerihikoEffect = "w741-merihiko", ButterfliesEffect = "w741-butterflies";

    // Target categories.
    public const string NonUniqueHuman = "w741-nonunique-human-personality", HumanPersonality = "w741-human-personality", OtherSpirit = "w741-other-spirit", WeaponOfWielder = "w741-weapon-of-wielder";

    public const string ButterfliesText = "Other player's Battle actions must target a Butterfly Personality at this battlefield, if legal. After another player's Battle action targets this card: Destroy this card";

    private static readonly CreatedCardSpec ButterfliesSpec = new(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 3, Keywords: ["Mantis Clan", "Nonhuman", "Butterfly", "Cavalry", "Spirit"],
        AbilityText: ButterfliesText, ProxyId: Butterflies)
    { Trait = ButterfliesEffect };

    private static readonly CreatedCardSpec IllusionSpec = new(CardType.Personality, Force: 0, Keywords: ["Illusion", "Nonhuman", "Cavalry", "Shugenja"], ProxyId: MinorIllusion)
    {
        Values = [new(CreatedStat.Chi, CreatedSubject.Wielder) { Current = true }, new(CreatedStat.PersonalHonor, CreatedSubject.Wielder) { Current = true }],
        RemoveWhen = CreatedRemoval.WhenSourceStraightensOrLeaves
    };

    private static readonly CreatedCardSpec GuardSpec = new(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 2, Keywords: ["Guard", "Samurai"], ProxyId: TowerGuard, AtBattlefield: true, UntilBattleEnds: true);

    public static Dictionary<string, AbilityDefinition> AddWave741(this Dictionary<string, AbilityDefinition> all)
    {
        all[MerihikoRanged] = new("Battle", "Battle, bow: Ranged 3 Attack", "attack", Costs: ActionCost.BowSource);
        all[YuribaraCreate] = new("Reaction", "After Yuribara enters play: Create a 0F/2C/3PH Mantis Clan · Nonhuman · Butterfly · Cavalry · Spirit Personality with two traits", "none", Trigger: "own-enters-play", Creates: ButterfliesSpec);
        all[TsukauCopy] = new("Open", "Ninja Open: Once per game, target a non-Unique Human Personality. Tsukau permanently copies his Force, one of his keywords, and one of his base abilities which does not itself copy abilities",
            NonUniqueHuman, OncePerGame: true, CopiesAbilities: true, ActionKeywords: ["Ninja"]);
        all[KintoSwitch] = new("Battle", "Battle: Target two Personalities at different locations controlled by the same player. Switch their locations. After they move, straighten their units if you do not control them", "slots",
            Slots: [new("first", "personality-with-switch-partner", "A Personality"), new("second", "personality-same-controller-as-first-elsewhere", "Another Personality of the same player's, at a different location")]);
        var (spellSlot, destroySpell) = EffectPrimitiveCosts.DestroyOwnAttachment(CardType.Spell);
        all[TsuchiyaStalwart] = new("Battle", "Battle: If Tsuchiya is defending, destroy one of his Spells: Give him Stalwart and a Force bonus equal to his current Force", "slots", Slots: [spellSlot], Costs: [destroySpell]);
        all[WatersteelCopy] = new("Open", "Water Open: Target a non-Unique Weapon attached to this Personality. Copy an ability on it that does not itself copy ability, to this Weapon (until the turn ends)", WeaponOfWielder,
            CopiesAbilities: true, ActionKeywords: ["Water"]);
        all[TidesMove] = new("Battle", "Water Battle, bow: Move this Shugenja from the current battlefield to an adjacent battlefield", "none", Costs: ActionCost.BowSource, ActionKeywords: ["Water"],
            Move: new(MoveSpec.Wielder, MoveSpec.AdjacentUnresolved));
        all[SelfCreate] = new("Limited", "Air Limited, bow: This Spell may remain bowed. Create a 0F Illusion · Nonhuman · Cavalry · Shugenja Personality with this Shugenja's Chi and Personal Honor. After the next time this game this Spell straightens or leaves play, remove the Illusion from the game",
            "none", Costs: ActionCost.BowSource, ActionKeywords: ["Air"], Creates: IllusionSpec);
        var provinceSlot = new TargetSlot("province", TargetSlotKinds.Province, "A Province");
        var (koanSlot, removeKoans) = EffectPrimitiveCosts.RemoveOwnCardsWithKeyword("Koan", 5);
        all[WhisperReduce] = new("Limited", "Void Limited, bow: Reduce a Province's strength to 0", "slots", Slots: [provinceSlot], Costs: ActionCost.BowSource, ActionKeywords: ["Void"], UsageKey: "w741-whisper");
        all[WhisperDestroy] = new("Limited", "Void Limited, bow: Reduce a Province's strength to 0. If its player has two or more Provinces, remove five Koan cards you control from the game to destroy it", "slots",
            Slots: [provinceSlot, koanSlot], Costs: [.. ActionCost.BowSource, removeKoans], ActionKeywords: ["Void"], UsageKey: "w741-whisper");
        all[TombToken] = new("Limited", "Limited, 1 Gold: Give your target Personality a +1F/+1C/+1PH token", "own-personality", Cost: GoldCostRule.Fixed(1));
        all[Harmony] = new("Open", "Open: Choose your performing Monk: Permanently give him the trait, \"You may bow this card instead of bowing a Ring to pay a cost.\"", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Monk"], ActionKeywords: ["Kiho"]);
        all[Judgment] = new("Limited", "Limited: Choose your performing unbowed Magistrate, target a Human Personality, and pay Gold equal to his unit's total Gold Cost, +8 unless he is dishonorable, Kolat, Ninja, or Shadowlands: He commits seppuku",
            HumanPersonality, Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Magistrate"], Cost: GoldCostRule.TargetUnitTotal());
        all[Chaos] = new("Open", "Open: Each player shuffles his hand into his deck, then draws as many cards as he shuffled into his deck", "none");
        all[ShiftingWaves] = new("Battle", "Battle: Choose your performing unbowed Henshin and target a Personality: Switch the target's Force and Chi", "personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Henshin"], ActionKeywords: ["Kiho"]);
        all[Prayer] = new("Limited", "Limited: Shuffle one to three target non-Unique Strategies in your discard pile into your deck. Remove this card from the game", "slots",
            Slots: [new("cards", "own-nonunique-strategy-in-discard", "One to three non-Unique Strategies in your discard pile", 1, 3)], Costs: ActionCost.RemoveSource);
        all[Coalescence] = new("BattleOrOpen", "Battle/Open: Destroy your performing Spirit Personality and target another Spirit Personality: Give it a permanent Force bonus equal to the performer's Force. It permanently copies the performer's abilities which do not themselves copy abilities. You may take an additional action",
            OtherSpirit, Performer: "own-personality-with-keyword", RequiredKeywords: ["Spirit"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)], ActionKeywords: ["Kiho"]);
        all[StrengthEarth] = new("Battle", "Earth Battle: Target your unbowed Monk. Melee Attack with strength equal to his Force, which may target a card in the Defender's home", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"], ActionKeywords: ["Earth", "Kiho"], Attack: new(TargetScope: AttackTargetScope.DefenderHome));
        all[Discovery] = new("Battle", "Battle: Move your target opposed Personality (at the current battlefield) to an adjacent battlefield", "own-opposed", Move: new(MoveSpec.Target, MoveSpec.AdjacentUnresolved));
        all[Gathering] = new("Limited", "Limited, X: Target an attachment with Gold Cost equal to or lower than the amount paid. Transfer it to any player's target Personality (this may change control of the attachment)", "slots",
            Slots: [new("attachment", "attachment", "An attachment"), new("to", "personality", "Any player's Personality")], Cost: GoldCostRule.Variable());
        all[MovingBattle] = new("BeforeResolution", "Before this battle resolves, you may move one of your opposed attacking Personalities at this battlefield to an adjacent, unresolved battlefield", "none",
            Trigger: "before-battle-resolves", Move: new(MoveSpec.Target, MoveSpec.AdjacentUnresolved));
        all[TowerEngage] = new("Reaction", "Engage, bow: If you have any units at this battlefield, create a 0F/2C/2PH Samurai · Guard Personality there. Remove him from the game after this battle ends", "none",
            Trigger: "after-engaging", Costs: ActionCost.BowSource, Creates: GuardSpec);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave741IsHuman(CardInstance card) => Definition(card).Type == CardType.Personality && !Keyword(card, "Nonhuman");

    private bool Wave741IsBattleAction(AbilityDefinition ability) => ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;

    // ---- target categories ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave741TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            case Wave741Abilities.NonUniqueHuman:
                return InPlay(target) && Wave741IsHuman(target) && !Keyword(target, "Unique") && target.Id != context.source?.Id; // a card never copies its own stats (core rules, Copy)
            case Wave741Abilities.HumanPersonality:
                return InPlay(target) && Wave741IsHuman(target);
            case Wave741Abilities.OtherSpirit:
                return InPlay(target) && Definition(target).Type == CardType.Personality && Keyword(target, "Spirit") && target.Id != context.command.PerformerId;
            case Wave741Abilities.WeaponOfWielder:
                return context.source is { Zone: Zone.Attached, AttachedTo: { } hostId } && target.Zone == Zone.Attached && target.AttachedTo == hostId && target.Id != context.source.Id
                    && Keyword(target, "Weapon") && !Keyword(target, "Unique");
            default:
                return false;
        }
    }

    // TargetSlots' offered default for Unsettling Gathering (single-action audit, issue #833): "Gold Cost equal to or lower than the amount paid" -- the plain default pays
    // the Variable cost's minimum (0), so every attachment that costs anything was listed disabled; the default pays the first attachment's Gold Cost instead.
    private GameCommand? Wave741DefaultAmount(GameCommand command) =>
        command.AbilityId == Wave741Abilities.Gathering && command.GoldAmount is null && SlotCards(command, "attachment").FirstOrDefault() is { } attachment
            ? command with { GoldAmount = CardGoldCost(attachment) } : null;

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave741Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        switch (id)
        {
            case Wave741Abilities.TsuchiyaStalwart:
                Require(IsDefendingUnit(source!), "Tsuchiya must be defending.");
                break;
            case Wave741Abilities.WhisperDestroy:
                {
                    var chosen = command.Selections?.FirstOrDefault(s => s.Slot == "province");
                    Require(chosen?.ProvinceOwner is int owner && Player(owner).Provinces.Count(p => !p.Destroyed) >= 2, "Its player must have two or more Provinces.");
                    break;
                }
            case Wave741Abilities.Gathering:
                {
                    var attachment = SlotCards(command, "attachment").Single();
                    Require(CardGoldCost(attachment) <= PaidGoldAmount(command), $"{Definition(attachment).Name} costs more Gold than the amount paid.");
                    break;
                }
            case Wave741Abilities.TowerEngage:
                Require(_state.Fortifications is { } fortifications && fortifications.TryGetValue(source!.Id, out var guarded) && _state.PendingEngage?.ProvinceIndex == guarded,
                    "The Tower of Vigilance acts at the battlefield of the Province it guards.");
                break;
        }
        Wave741RequireButterfly(context);
    }

    // The Butterflies: "Other player's Battle actions must target a Butterfly Personality at this battlefield, if legal." A Battle action that names another card is refused when the same action could legally name
    // a Butterfly Personality at the current battlefield that belongs to another player.
    private void Wave741RequireButterfly(ValidationHookContext context)
    {
        var (command, ability) = (context.command, context.ability);
        if (command.TargetId is not { } targetId || _state.CurrentBattleProvince is not int province || !Wave741IsBattleAction(ability) || !_state.Cards.TryGetValue(targetId, out var named)) return;
        if (!_state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && Definition(c).Effect == Wave741Abilities.ButterfliesEffect)) return;
        var butterflies = _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Definition(c).Type == CardType.Personality && c.OwnerId != command.PlayerId
            && Definition(c).Effect == Wave741Abilities.ButterfliesEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (butterflies.Length == 0 || butterflies.Any(b => b.Id == named.Id) || Keyword(named, "Butterfly") && Definition(named).Type == CardType.Personality && named.Zone == Zone.Battlefield && named.ProvinceIndex == province) return;
        foreach (var butterfly in butterflies)
            Require(AbilityRejection(command with { TargetId = butterfly.Id }) is not null, $"This action must target {Definition(butterfly).Name} if it can.");
    }


    // ---- A Moving Battle: its before-resolution trait, answered through "beforeBattleOptional" ----------------------------------------------------------------------------------------

    // "One of your opposed attacking Personalities at this battlefield": the Terrain's controller is the Attacker and an enemy army opposes his Personality there.
    private IEnumerable<CardInstance> Wave741MovingBattleCandidates(int controllerId) =>
        _state.CurrentBattleProvince is int province && controllerId == _state.ActivePlayerId && Army(1 - controllerId, province).Any()
            ? Army(controllerId, province).OrderBy(c => c.Id, StringComparer.Ordinal) : [];

    private int[] Wave741MovingBattleDestinations() => MoveDestinations(AbilityCatalog.All[Wave741Abilities.MovingBattle].Move!);

    private bool IsWave741MovingBattleChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) == Wave741Abilities.MovingBattle;

    // Opens the choice, or (nothing to move, nowhere to move it) does nothing. True when the choice is open.
    private bool BeginWave741MovingBattle(CardInstance terrain)
    {
        if (!Wave741MovingBattleCandidates(terrain.OwnerId).Any() || Wave741MovingBattleDestinations().Length == 0)
        {
            Emit("beforeBattleEffect", $"{Definition(terrain).Name}: nothing can be moved.", terrain.OwnerId, terrain.Id);
            return false;
        }
        _state.PendingChoice = new("beforeBattleOptional", terrain.OwnerId, terrain.Id, $"Before this battle resolves: you may move one of your opposed attacking Personalities to an adjacent, unresolved battlefield ({Definition(terrain).Name}).");
        _state.PriorityPlayerId = terrain.OwnerId;
        return true;
    }

    // "decline", or "use" naming the Personality (TargetId) and the battlefield (Province).
    private void ResolveWave741MovingBattle(GameCommand command, int playerId)
    {
        if (command.Choice == "decline") return;
        Require(command.TargetId is not null && Wave741MovingBattleCandidates(playerId).Any(c => c.Id == command.TargetId), "Choose one of your opposed attacking Personalities at this battlefield.");
        Require(command.Province is int destination && Wave741MovingBattleDestinations().Contains(destination), "Choose an adjacent, unresolved battlefield.");
        var card = Card(command.TargetId);
        if (TryMoveToBattlefield(card, command.Province!.Value)) Emit("beforeBattleEffect", $"A Moving Battle: {Definition(card).Name} moved to province {command.Province + 1}.", playerId, card.Id);
    }

    private IEnumerable<ActionOption> Wave741MovingBattleOptions(int playerId, string? sourceId)
    {
        if (_state.PendingChoice?.PlayerId != playerId) yield break;
        yield return new("beforeBattleOptional", "Decline", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, Choice: "decline"), sourceId);
        foreach (var card in Wave741MovingBattleCandidates(playerId))
            foreach (var destination in Wave741MovingBattleDestinations())
                yield return new("beforeBattleOptional", $"Move {Definition(card).Name} to province {destination + 1}", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: card.Id, Province: destination, Choice: "use"), card.Id);
    }

    // PlayStrategy's Terrain rule: "Battle: Destroy a Terrain (if able). Put this Terrain into play" replaces the Terrain already at the battlefield.
    private static bool Wave741TerrainReplaces(CardDefinition definition) => (definition.Abilities ?? []).Contains(Wave741Abilities.MovingBattle);

    // ---- the Gold of Kitsuki Judgment ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "+8 unless he is dishonorable, Kolat, Ninja, or Shadowlands" (a P2 ability cost term read off the command's target).
    private IEnumerable<CostTerm>? Wave741CostTerms(AbilityCostContext context)
    {
        if (context.AbilityId != Wave741Abilities.Judgment || context.Command?.TargetId is not { } targetId || !_state.Cards.TryGetValue(targetId, out var target)) return null;
        var exempt = target.Dishonorable || Keyword(target, "Kolat") || Keyword(target, "Ninja") || Keyword(target, "Shadowlands");
        return exempt ? null : [new CostTerm(8, "he is not dishonorable, Kolat, Ninja or Shadowlands")];
    }

    // ---- effects ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave741(RunHookContext c)
    {
        var (id, ability, command, player, source, target) = (c.id, c.ability, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case Wave741Abilities.MerihikoRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, c.consumedGrant);
            case Wave741Abilities.StrengthEarth:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, c.consumedGrant);
            case Wave741Abilities.TsukauCopy:
                // "Tsukau permanently copies his Force, one of his keywords, and one of his base abilities which does not itself copy abilities."
                CopyStat(target!, source!, "Force", source!.Id, id, permanent: true);
                return BeginCopy(source, target!, id, [CopyStep.Keyword, CopyStep.Ability], player.Id, "ability", c.bowedBeforeAction, GrantDuration.Permanent);
            case Wave741Abilities.KintoSwitch:
                {
                    var (first, second) = (SlotCards(command, "first").Single(), SlotCards(command, "second").Single());
                    // "After they move, straighten their units if you do not control them."
                    if (SwitchLocations(first, second, Definition(source!).Name))
                        foreach (var moved in new[] { first, second }) if (moved.OwnerId != player.Id && InPlay(moved)) StraightenUnit(moved);
                    return false;
                }
            case Wave741Abilities.TsuchiyaStalwart:
                // The Spell is destroyed as the cost; "a Force bonus equal to his current Force" is read now.
                GrantKeyword(source!, "Stalwart", source!.Id, GrantDuration.Turn);
                ApplyModifier(source!, source!.Id, id, "Force", CardForce(source!));
                return false;
            case Wave741Abilities.WatersteelCopy:
                return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", c.bowedBeforeAction, GrantDuration.Turn);
            case Wave741Abilities.TidesMove:
            case Wave741Abilities.Discovery:
                ExecuteMove(command, ability, source, target);
                return false;
            case Wave741Abilities.WhisperReduce:
            case Wave741Abilities.WhisperDestroy:
                {
                    var chosen = command.Selections!.First(s => s.Slot == "province");
                    var (owner, index) = (chosen.ProvinceOwner!.Value, chosen.Province!.Value);
                    var province = Player(owner).Provinces.First(p => p.Index == index);
                    // "Reduce a Province's strength to 0": a penalty sufficient to reach 0, until the turn ends (the duration every province strength effect has).
                    GiveProvinceStrengthThisTurn(owner, index, -Math.Max(0, EffectiveProvinceStrength(province)), Definition(source!).Name);
                    if (id == Wave741Abilities.WhisperDestroy && !province.Destroyed && Player(owner).Provinces.Count(p => !p.Destroyed) >= 2) DestroyProvince(owner, province);
                    return false;
                }
            case Wave741Abilities.TombToken:
                CreateMultiStatToken(target!, "Tomb", source!.Id, ("Force", 1), ("Chi", 1), ("PersonalHonor", 1));
                return false;
            case Wave741Abilities.Harmony:
                GrantBowInsteadOfRing(Card(command.PerformerId!), Definition(source!).Name);
                return false;
            case Wave741Abilities.Judgment:
                CommitSeppuku(target!, player.Id);
                return false;
            case Wave741Abilities.Chaos:
                ShuffleHandsAndRedraw([player.Id, 1 - player.Id], source);
                return false;
            case Wave741Abilities.ShiftingWaves:
                ExchangeStats(target!, "Force", target!, "Chi", source!.Id, id);
                return false;
            case Wave741Abilities.Prayer:
                ShuffleIntoDeck(SlotCards(command, "cards"), source!);
                return false;
            case Wave741Abilities.Coalescence:
                {
                    var performer = Card(command.PerformerId!);
                    // The performer is destroyed as the cost; his Force is counted as it stood (without his Items), as the core rules' Blood Wave ruling counts a destroyed Personality's Force.
                    AddPermanentModifier(target!, source!.Id, id, "Force", CardForce(performer));
                    foreach (var abilityId in CopyableAbilityIds(performer, target!)) GrantAbility(target!, abilityId, id, GrantDuration.Permanent, performer.DefinitionId, Definition(performer).Name);
                    GrantAdditionalAction(player.Id, null);
                    return false;
                }
            case Wave741Abilities.Gathering:
                TransferAttachment(SlotCards(command, "attachment").Single(), SlotCards(command, "to").Single(), Definition(source!).Name, allowControlChange: true);
                return false;
            case Wave741Abilities.TowerEngage:
                {
                    // "If you have any units at this battlefield, create ..." -- the Guard stands where the Province's battle is fought.
                    if (_state.PendingEngage is not { } engage || !Army(player.Id, engage.ProvinceIndex).Any())
                    {
                        Emit("towerNoUnits", $"{Definition(source!).Name}: {player.Name} has no units at this battlefield, so no Guard is created.", player.Id, source!.Id);
                        return false;
                    }
                    return ExecuteCreate(ability, ability.Creates!, command, player);
                }
            default:
                return null;
        }
    }

    private static int? Wave741AttackStrength(MatchEngine engine, AttackStrengthBeforeContext context) => context.abilityId switch
    {
        Wave741Abilities.MerihikoRanged => 3,
        Wave741Abilities.StrengthEarth => context.sourceId is not null ? engine.CardForce(engine.Card(context.sourceId)) : 0,
        _ => null
    };

    private static string? Wave741AttackKind(string abilityId) => abilityId switch
    {
        Wave741Abilities.MerihikoRanged => "Ranged",
        Wave741Abilities.StrengthEarth => "Melee",
        _ => null
    };

    // The Butterflies' second trait: "After another player's Battle action targets this card: Destroy this card." Mandatory, before any Reaction to the same resolution.
    private void Wave741AfterAction(ResolvedActionFacts facts)
    {
        if (facts.TargetIds.Count == 0 || !AbilityCatalog.All.TryGetValue(facts.AbilityId, out var acting) || !Wave741IsBattleAction(acting)) return;
        foreach (var butterfly in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == Wave741Abilities.ButterfliesEffect && c.OwnerId != facts.PlayerId && facts.TargetIds.Contains(c.Id))
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            Emit("butterfliesFragile", $"{Definition(butterfly).Name} was targeted by another player's Battle action and is destroyed.", butterfly.OwnerId, butterfly.Id);
            DestroyUnits([butterfly], actingPlayerId: butterfly.OwnerId);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave741Hooks()
    {
        EngineHooks.Abilities.Register("Wave741Abilities", 14000, static abilities => abilities.AddWave741());
        EngineHooks.TargetAllows.Register("Wave741TargetAllows", 14000, static (engine, context) => engine.Wave741TargetAllows(context));
        EngineHooks.ValidateLate.Register("Wave741Validate", 14000, static (engine, context) => engine.Wave741Validate(context));
        EngineHooks.RunEffects.Register("Wave741RunEffects", 14000, static (engine, context) => engine.RunWave741(context));
        EngineHooks.AttackStrengthBefore.Register("Wave741AttackStrength", 14000, static (engine, context) => Wave741AttackStrength(engine, context));
        EngineHooks.AttackKind.Register("Wave741AttackKind", 14000, static context => Wave741AttackKind(context.abilityId));
        EngineHooks.AbilityCostTerms.Register("Wave741CostTerms", 14000, static context => context.Engine.Wave741CostTerms(context));
        EngineHooks.AfterActionTraits.Register("Wave741Butterflies", 14000, static (engine, facts) => engine.Wave741AfterAction(facts));

        // Kitsune Merihiko - exp: "Your Nonhuman Spirits have the ability, 'Battle, Bow: Ranged 3 Attack.'" -- read live while he is in play.
        CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule("w741-merihiko-spirits", FilterGrantRule.OnEffect(Wave741Abilities.MerihikoEffect), Wave741Abilities.MerihikoRanged,
            new GrantFilter(CardType.Personality, Keywords: ["Nonhuman", "Spirit"])));
    }
}
