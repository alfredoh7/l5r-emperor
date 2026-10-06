using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// TORN ASUNDER PERSONALITIES (issue #674, cluster "pers-torn", personality-wave-131): twenty-one Personalities wired from their own scans with EXISTING mechanisms only. Where each card's pieces are:
//
//   Asahina Akahiko   "Melee and Ranged Attacks targeting Akahiko have -4 strength."                       CardDefinition.AttackDefenseKind "Both" / -4 / "Self" (Destrier's data)
//                     "Battle: Target an enemy Personality: Move him and Akahiko home. Gain 1 Honor."     wlpt-akahiko-home: Move(SourceAndTarget, Home), then the Honor
//   Asahina Nanae     "Battle/Open: Even if Nanae is bowed, target a Personality: You may straighten ..."  wlpt-nanae-jade: an engine-listed effect choice (BeginListedEffectChoice) of four options
//   Asako Izuna       "Political Limited: Take the Imperial Favor." / "Battle/Limited: ... bow the target"   noboru-exp2-favor, and Asako Tsunefusa's two-slot bow with its Honor choice (a card in a unit)
//   Asako Kyuudo, Kaiu Nagai   "+3F and Elite while defending" / "has Elite while defending"             Keyword() grants Elite while the card defends; Kyuudo's +3F is a PersonalityForce term
//   Asako Suzukaze    "Target a Holding: It produces 1 additional Gold ... (this turn)"                     a turn-long GoldProduction modifier (The Moon's Imperative's), only on a Holding that produces
//   Bayushi Wakui, Riku   the two redirecting Reactions                                                     the TargetedByAction trigger and RedirectAction (Mirumoto Ezuno's)
//   Daidoji Ibara     "Move Ibara to oppose it. If she now opposes it ..."                                  a MovesToBattle move of the source, then the effect when she stands opposite the target
//   Doji Iza, Mirumoto Kyoshiro, Riku   the three "duels him. Destroy the duel's loser"                     IssuesDuel (Dueling.cs resolves it), a named-target category for the two Open duels
//   Fubuko            "Take an additional action to play a Kiho Strategy in your discard pile ..."          Daigotsu Bofana - exp's grant (GrantAdditionalAction fromDiscard) naming Fubuko as the performer
//   Iuchi Shaocheng   "After Shaocheng enters play: Turn a card in a province face-up ..."                 an entry-play Reaction (the engine's model of an entry trait with a target) on "card-in-province"
//   Moshi Ira         "Reaction: After an action targets a card in this unit: Give the performer -3F ..."   a TargetedByAction Reaction that notes the held action; the -3F lands when it is truly done
//   Shiba Tetsaka     "Open: Bow your performing Shugenja: Give Tetsaka Cavalry or Naval."                  two Open abilities that share one use (Mahatsu's shape)
//   Shinjo Sihung     "Reaction: After your Tactical action resolves: Give a Personality who performed it +2F."   an after-action-resolved Reaction (Shinjo Jong-Ho's Tactical test)
//   Shosuro Makiko    "Political Open: Bow Makiko: Take the Imperial Favor." / "Political Reaction: Before an action resolves, bow Makiko: Reduce all Honor gains from it by 2."
//                                                                                                        makiko-favor, and Shiro Chugo's before-an-Honor-change window with a gains-only mark
//   Soshi Shinoko     "Open: Target a Personality: Give him Brash"                                          a keyword grant for the turn (R-001)
//   Utaku Suying      "After a battle resolution in which Suying's army destroyed a defending army or province: Gain 2 Honor."  a trait read where the resolution settles (Shiba's Guidance's place)
//   Yasuki Daito      "Limited: Bow Daito or pay 4 Gold, and target a Personality: ... Negate the target's straightening until after the next Straighten Phase ends."
//                                                                                                        two Limited abilities (bow / 4 Gold) that share one use, and a timed straighten hold
//   Yoritomo Tansen   "Battle: Ranged 3 Attack or give the current battlefield's province -3 strength."    two Battle abilities that share one use: an Attack (Gakku's shape) and a province strength change
//
// Every reading is recorded in docs/decisions/2026-10-06-674-pers-torn.md.
// ======================================================================================================================================================================================

public static class WlPersTornAbilities
{
    public const string AkahikoHome = "wlpt-akahiko-home";
    public const string NanaeJade = "wlpt-nanae-jade";
    public const string IzunaBowBattle = "wlpt-izuna-bow-battle", IzunaBowLimited = "wlpt-izuna-bow-limited";
    public const string SuzukazeGold = "wlpt-suzukaze-gold";
    public const string WakuiRedirect = "wlpt-wakui-redirect";
    public const string IbaraOppose = "wlpt-ibara-oppose";
    public const string IzaDuel = "wlpt-iza-duel", KyoshiroDuel = "wlpt-kyoshiro-duel", RikuDuel = "wlpt-riku-duel";
    public const string FubukoKiho = "wlpt-fubuko-kiho";
    public const string ShaochengTurn = "wlpt-shaocheng-turn";
    public const string IraWeaken = "wlpt-ira-weaken";
    public const string RikuRedirect = "wlpt-riku-redirect";
    public const string TetsakaCavalry = "wlpt-tetsaka-cavalry", TetsakaNaval = "wlpt-tetsaka-naval";
    public const string SihungBoost = "wlpt-sihung-boost";
    public const string MakikoReduce = "wlpt-makiko-reduce";
    public const string ShinokoBrash = "wlpt-shinoko-brash";
    public const string DaitoBow = "wlpt-daito-bow", DaitoGold = "wlpt-daito-gold";
    public const string TansenRanged = "wlpt-tansen-ranged", TansenProvince = "wlpt-tansen-province";

    public const string KyuudoEffect = "wlpt-kyuudo", NagaiEffect = "wlpt-nagai", SuyingEffect = "wlpt-suying";

    /// <summary>The target categories this pass adds (TargetAllows): another player's card in a unit, and another player's Personality of a given title.</summary>
    public const string EnemyUnitCard = "wlpt-enemy-unit-card", EnemyKyoshiro = "wlpt-enemy-kyoshiro", EnemyIza = "wlpt-enemy-iza";

    /// <summary>The branch effects of Nanae's choice (ApplyBranchEffect's arm).</summary>
    public const string EffectStraighten = "wlpt:straighten", EffectWeaken = "wlpt:weaken", EffectBowShadowlands = "wlpt:bow-shadowlands";

    public const string KyoshiroTitle = "Mirumoto Kyoshiro", IzaTitle = "Doji Iza", TamahimeTitle = "Tamahime";

    /// <summary>Shaocheng's entry trait is modelled as an entry-play Reaction (the engine's convention for a trait with a target); a Reaction-ban must not touch it (R-020, TraitReactions).</summary>
    public static bool IsTrait(string abilityId) => abilityId == ShaochengTurn;

    /// <summary>"Even if Nanae is bowed" (ValidateAbility's list).</summary>
    public static bool EvenIfBowed(string abilityId) => abilityId == NanaeJade;

    public static readonly EffectOption[] NanaeOptions =
    [
        new("straighten", "Straighten him", [EffectStraighten, EffectBowShadowlands]),
        new("weaken", "Give him -4F", [EffectWeaken, EffectBowShadowlands]),
        new("both", "Straighten him and give him -4F", [EffectStraighten, EffectWeaken, EffectBowShadowlands]),
        new("neither", "Do neither", [EffectBowShadowlands]),
    ];

    public static Dictionary<string, AbilityDefinition> AddWlPersTorn(this Dictionary<string, AbilityDefinition> all)
    {
        all[AkahikoHome] = new("Battle", "Target an enemy Personality: Move him and Akahiko home. Gain 1 Honor", "enemy-personality", Move: new(MoveSpec.SourceAndTarget, MoveSpec.Home));
        all[NanaeJade] = new("BattleOrOpen", "Even if Nanae is bowed, target a Personality: You may straighten him. You may give him -4F. Bow him if he is Shadowlands", "personality");
        // Asako Izuna - exp: Tsunefusa's two-slot bow with its conditional Honor choice, over "another player's card in a unit"; one use between the Battle and the Limited half.
        TargetSlot[] IzunaSlots() =>
        [
            new("target", EnemyUnitCard, "Another player's card in a unit to bow"),
            new("honor", "player", "If it is Kolat, Ninja, or Shadowlands: a player gains or loses 2 Honor", 0, 0,
                Resolution: new(BranchCondition.TargetKolatNinjaOrShadowlands, HonorChoice.Options(2), HonorChoice.None))
        ];
        all[IzunaBowBattle] = new("Battle", "Battle/Limited: Target another player's card in a unit: Bow the target. If it is Kolat, Ninja, or Shadowlands, choose a player who gains or loses 2 Honor", "slots",
            UsageKey: "wlpt-izuna-bow", Slots: IzunaSlots());
        all[IzunaBowLimited] = new("Limited", "Battle/Limited: Target another player's card in a unit: Bow the target. If it is Kolat, Ninja, or Shadowlands, choose a player who gains or loses 2 Honor", "slots",
            UsageKey: "wlpt-izuna-bow", Slots: IzunaSlots());
        all[SuzukazeGold] = new("Limited", "Target a Holding: It produces 1 additional Gold when it bows to produce Gold (this turn)", "any-holding");
        all[WakuiRedirect] = new("Reaction", "When another player would target a Magistrate at Wakui's battlefield with an action: The action targets Wakui instead, if legal", "none", Trigger: ReactionTriggers.TargetedByAction);
        all[IbaraOppose] = new("Battle", "Target an enemy Personality or Follower: Move Ibara to oppose it. If she now opposes it, give it -3F and, if it is Mantis Clan, bow it", "enemy-personality-or-follower",
            MovesToBattle: true, Move: new(MoveSpec.Source, MoveSpec.CurrentBattlefield));
        all[IzaDuel] = new("Open", "Target another player's Mirumoto Kyoshiro: Iza duels him. Destroy the duel's loser", EnemyKyoshiro, IssuesDuel: true);
        all[KyoshiroDuel] = new("Open", "Target another player's Doji Iza: Kyoshiro duels her. Destroy the duel's loser", EnemyIza, IssuesDuel: true);
        all[RikuRedirect] = new("Reaction", "When another player's action would target Tamahime: It targets Riku instead, if legal", "none", Trigger: ReactionTriggers.TargetedByAction);
        all[RikuDuel] = new("Battle", "Iaijutsu Battle: Target an enemy Personality: Riku duels him. Destroy the duel's loser", "enemy-personality", IssuesDuel: true, ActionKeywords: ["Iaijutsu"]);
        all[FubukoKiho] = new("Battle", "Target a Kiho Strategy in your discard pile: Take an additional action to play it for one of its Battle actions, which Fubuko must perform. After it resolves, remove the Strategy from the game",
            "own-kiho-strategy-in-discard");
        all[ShaochengTurn] = new("Reaction", "After Shaocheng enters play: Turn a card in a province face-up. If it is a non-Unique Personality, discard him and he becomes honorably dead", "card-in-province", Trigger: "own-enters-play");
        all[IraWeaken] = new("Reaction", "After an action targets a card in this unit: Give the action's performer (if any) -3F after the action resolves", "none", Trigger: ReactionTriggers.TargetedByAction);
        all[TetsakaCavalry] = new("Open", "Bow your performing Shugenja: Give Tetsaka Cavalry", "none", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Shugenja"],
            Costs: ActionCost.BowPerformer, UsageKey: "wlpt-tetsaka-keyword");
        all[TetsakaNaval] = new("Open", "Bow your performing Shugenja: Give Tetsaka Naval", "none", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Shugenja"],
            Costs: ActionCost.BowPerformer, UsageKey: "wlpt-tetsaka-keyword");
        all[SihungBoost] = new("Reaction", "After your Tactical action resolves: Give a Personality who performed it +2F", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        all[MakikoReduce] = new("Reaction", "Political Reaction: Before an action resolves, bow Makiko: Reduce all Honor gains from it by 2", "none", Trigger: HonorChangeReactions.Trigger,
            Costs: ActionCost.BowSource, ActionKeywords: ["Political"]);
        all[ShinokoBrash] = new("Open", "Target a Personality: Give him Brash", "personality");
        all[DaitoBow] = new("Limited", "Bow Daito and target a Personality: Daito bribes him. Negate the target's straightening until after the next Straighten Phase ends", "personality",
            Costs: ActionCost.BowSource, UsageKey: "wlpt-daito-bribe");
        all[DaitoGold] = new("Limited", "Pay 4 Gold and target a Personality: Daito bribes him. Negate the target's straightening until after the next Straighten Phase ends", "personality",
            Cost: GoldCostRule.Fixed(4), UsageKey: "wlpt-daito-bribe");
        all[TansenRanged] = new("Battle", "Ranged 3 Attack", "attack", UsageKey: "wlpt-tansen");
        all[TansenProvince] = new("Battle", "Give the current battlefield's province -3 strength", "none", UsageKey: "wlpt-tansen");
        return all;
    }
}

/// <summary>Moshi Ira's note of a held action whose performer is owed -3F once the action has resolved.</summary>
public sealed class WlPersTornIraNote
{
    public string IraId { get; set; } = "";
    public string PerformerId { get; set; } = "";
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    public string? CardId { get; set; }
    public int Turn { get; set; }
}

/// <summary>The pass's marks: Ira's owed penalties and the action whose Honor gains Makiko reduces. A nondecision record: nothing here is a pending choice.</summary>
public sealed class WlPersTornState
{
    public List<WlPersTornIraNote> IraNotes { get; set; } = [];
    public string? HonorGainReducedActionKey { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(20400)]
    public WlPersTornState WlPersTorn { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // ---- statics: Elite and +3F while defending ----------------------------------------------------------------------------------------------------------------------------------

    // "While defending": a Personality standing at a battlefield for the player who is not the Attacker.
    private bool WlPersTornDefending(CardInstance card) =>
        InPlay(card) && card.Zone == Zone.Battlefield && card.OwnerId != _state.ActivePlayerId && Definition(card).Type == CardType.Personality;

    // Keyword()'s term: Kyuudo and Nagai have Elite while they defend (so EliteEffect, and every other Elite check, sees it live).
    private bool WlPersTornGrantsKeyword(CardInstance card, string keyword) =>
        keyword == "Elite" && Definition(card).Effect is WlPersTornAbilities.KyuudoEffect or WlPersTornAbilities.NagaiEffect && WlPersTornDefending(card);

    // Kyuudo: +3F while defending.
    private int WlPersTornForce(CardInstance card) =>
        Definition(card).Effect == WlPersTornAbilities.KyuudoEffect && WlPersTornDefending(card) ? 3 : 0;

    // ---- targets -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool WlPersTornTargetAllows(string category, CardInstance target, PlayerState player) => category switch
    {
        // "another player's card in a unit": a Personality, or a Follower, Item or Spell attached to one, controlled by another player.
        WlPersTornAbilities.EnemyUnitCard => target.OwnerId != player.Id && (Definition(target).Type == CardType.Personality
            || target.Zone == Zone.Attached && Definition(target).Type is CardType.Follower or CardType.Item or CardType.Spell),
        WlPersTornAbilities.EnemyKyoshiro => WlPersTornNamedEnemy(target, player, WlPersTornAbilities.KyoshiroTitle),
        WlPersTornAbilities.EnemyIza => WlPersTornNamedEnemy(target, player, WlPersTornAbilities.IzaTitle),
        _ => false
    };

    private bool WlPersTornNamedEnemy(CardInstance target, PlayerState player, string title) =>
        InPlay(target) && target.OwnerId != player.Id && Definition(target).Type == CardType.Personality && Definition(target).Name == title;

    // ---- Reactions to a targeted action: Wakui's and Riku's redirects, Ira's penalty ----------------------------------------------------------------------------------------------

    private bool WlPersTornTargetedReaction(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var command = subject.Command;
        switch (abilityId)
        {
            // Another player's action would target a Magistrate at Wakui's battlefield, and is legal against Wakui.
            case WlPersTornAbilities.WakuiRedirect:
                return command.PlayerId != card.OwnerId && card.Zone == Zone.Battlefield && affected.Id != card.Id && Definition(affected).Type == CardType.Personality
                    && Keyword(affected, "Magistrate") && SameLocation(card, affected) && ActionAcceptsTarget(subject, card.Id);
            // Another player's action would target Tamahime (either printing, anyone's), and is legal against Riku.
            case WlPersTornAbilities.RikuRedirect:
                return command.PlayerId != card.OwnerId && affected.Id != card.Id && Definition(affected).Type == CardType.Personality
                    && Definition(affected).Name == WlPersTornAbilities.TamahimeTitle && ActionAcceptsTarget(subject, card.Id);
            // An action targets a card in Ira's unit (Ira himself or an attachment on him) and has a performing Personality to weaken.
            case WlPersTornAbilities.IraWeaken:
                return UnitHostId(affected) == card.Id && ActionPerformingPersonality(command) is not null;
            default:
                return false;
        }
    }

    // ---- Moshi Ira: "after the action resolves" ----------------------------------------------------------------------------------------------------------------------------------

    // FinishActionEffectTracking's term: an action truly done pays the -3F Ira's Reaction recorded against it. Notes of earlier turns (an action that never finished: it was negated whole) lapse.
    private void WlPersTornActionDone(string abilityId, int playerId, string? sourceId)
    {
        var notes = _state.WlPersTorn.IraNotes;
        if (notes.Count == 0) return;
        notes.RemoveAll(n => n.Turn != _state.TurnNumber);
        foreach (var note in notes.Where(n => n.AbilityId == abilityId && n.PlayerId == playerId && n.CardId == sourceId).ToArray())
        {
            notes.Remove(note);
            if (!_state.Cards.TryGetValue(note.PerformerId, out var performer) || !InPlay(performer) || !_state.Cards.TryGetValue(note.IraId, out var ira)) continue;
            Emit("iraPenalty", $"{Definition(ira).Name}: {Definition(performer).Name} has -3F after the action resolved.", ira.OwnerId, performer.Id);
            ApplyModifier(performer, ira.Id, WlPersTornAbilities.IraWeaken, "Force", -3);
        }
    }

    // ---- after-action Reactions: Sihung -------------------------------------------------------------------------------------------------------------------------------------------

    // AfterActionResolvedApplies' arm; null when the Reaction is not this pass's. Sihung: the resolved action was this player's own Tactical action and a Personality performed it.
    private bool? WlPersTornAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId != WlPersTornAbilities.SihungBoost) return null;
        if (source.OwnerId != playerId || facts.PlayerId != playerId || facts.PerformerId is null || !AbilityCatalog.All.TryGetValue(facts.AbilityId, out var acting)) return false;
        if (!_state.Cards.TryGetValue(facts.PerformerId, out var performer) || !InPlay(performer) || Definition(performer).Type != CardType.Personality) return false;
        return ActionCarriesKeyword(acting, facts.SourceId is null ? null : Card(facts.SourceId), "Tactical");
    }

    // ---- Makiko: Honor gains of the marked action are reduced by 2 ---------------------------------------------------------------------------------------------------------------

    // HonorChangeReactionApplies' arm: Makiko is offered before an Honor GAIN from an action's effects (a loss is none of hers).
    private bool? WlPersTornHonorChangeApplies(CardInstance card, string abilityId, PendingHonorChange change) =>
        abilityId == WlPersTornAbilities.MakikoReduce ? InPlay(card) && change.Amount > 0 : null;

    // GainHonor's term, beside Shiro Chugo's: a gain the marked action causes is reduced by 2, to at most 0.
    private int WlPersTornReducedHonorGain(int playerId, int amount, string reason)
    {
        if (amount <= 0 || _state.WlPersTorn.HonorGainReducedActionKey is not { } key || key != CurrentActionKey()) return amount;
        var reduced = Math.Max(0, amount - 2);
        Emit("honorChangeReduced", $"{Player(playerId).Name}'s Honor gain from {reason} was reduced by 2 (Shosuro Makiko){(reduced == 0 ? " to 0" : "")}.", playerId);
        return reduced;
    }

    // ---- Suying: the resolution's trait ---------------------------------------------------------------------------------------------------------------------------------------------

    // "After a battle resolution in which Suying's army destroyed a defending army or province: Gain 2 Honor." Her army is the attacking army she stood in (a defending army destroys no
    // defending army); it destroyed units, or the province fell. Mandatory (no Reaction designator), once per Suying that is still in play.
    private void WlPersTornApplyResolution(CardInstance[] attackers, int attacker, int[] destroyedEnemyUnits, bool provinceDestroyed)
    {
        if (destroyedEnemyUnits[attacker] == 0 && !provinceDestroyed) return;
        foreach (var suying in attackers.Where(c => Definition(c).Effect == WlPersTornAbilities.SuyingEffect && InPlay(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            GainHonor(suying.OwnerId, 2, Definition(suying).Name, suying);
    }

    // ---- validation ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlPersTorn(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (id == WlPersTornAbilities.TansenProvince) Require(CurrentProvinceIndex() is not null, "The current battlefield has no province.");
        // Fubuko's grant plays a card from a discard pile "which Fubuko must perform": the grant that makes a Strategy playable from there names its performer, so an action by anyone else is
        // not that play (a Discipline play, which no grant makes, is untouched).
        if (source is { Zone: Zone.FateDiscard or Zone.DynastyDiscard } && ability.Timing != "Reaction" && !DisciplineFromDiscard(ability, source)
            && DiscardPlayGrant(player.Id, source) is { PerformerId: { } required })
            Require(ActionPerformingPersonality(command)?.Id == required, "The additional action must be performed by the Personality who granted it.");
    }

    // ---- effects -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWlPersTorn(RunHookContext c)
    {
        var id = c.id; var player = c.player; var source = c.source; var target = c.target; var command = c.command;
        switch (id)
        {
            case WlPersTornAbilities.AkahikoHome:
                ExecuteMove(command, c.ability, source, target);
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case WlPersTornAbilities.NanaeJade:
                return BeginListedEffectChoice(command, WlPersTornAbilities.NanaeOptions, $"{Definition(source!).Name}: you may straighten him and you may give him -4F.",
                    c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants);
            // Tsunefusa's shape: the bow always happens, the Honor choice is the follow-up when the target is Kolat, Ninja or Shadowlands.
            case WlPersTornAbilities.IzunaBowBattle:
            case WlPersTornAbilities.IzunaBowLimited:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return BeginConditionalChoice(command, c.ability, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants);
            case WlPersTornAbilities.SuzukazeGold:
                // "when it bows to produce Gold": a Holding that produces nothing has no production to add to.
                if (Definition(target!).GoldProduction > 0 || ConditionalProductionOf(target!) is not null) ApplyModifier(target!, source!.Id, id, "GoldProduction", 1);
                return false;
            case WlPersTornAbilities.WakuiRedirect:
            case WlPersTornAbilities.RikuRedirect:
                RedirectAction(HeldAction(), source!.Id, source);
                return false;
            case WlPersTornAbilities.IbaraOppose:
                {
                    ExecuteMove(command, c.ability, source, target);
                    // "If she now opposes it": she stands at the current battlefield and so does the enemy card, on the other side.
                    var opposes = InPlay(source!) && source!.Zone == Zone.Battlefield && source.ProvinceIndex == _state.CurrentBattleProvince
                        && InPlay(target!) && Card(UnitHostId(target!)) is { Zone: Zone.Battlefield } host && host.ProvinceIndex == source.ProvinceIndex && host.OwnerId != source.OwnerId;
                    if (!opposes) return false;
                    ApplyModifier(target!, source!.Id, id, "Force", -3);
                    if (HasClanAffiliation(target!, "Mantis") && InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    return false;
                }
            case WlPersTornAbilities.FubukoKiho:
                // Bofana - exp's grant (the one chosen discard-pile Strategy, one of its Battle actions, removed after it resolves), spent only by an action Fubuko performs.
                GrantAdditionalAction(player.Id, [target!.Id], fromDiscard: true, performerId: source!.Id);
                return false;
            case WlPersTornAbilities.ShaochengTurn:
                WlPersTornTurnProvinceCard(target!, source!);
                return false;
            case WlPersTornAbilities.IraWeaken:
                {
                    var held = HeldAction();
                    var performer = ActionPerformingPersonality(held.Command) ?? throw new RuleViolation("No Personality performed that action.");
                    _state.WlPersTorn.IraNotes.Add(new() { IraId = source!.Id, PerformerId = performer.Id, AbilityId = held.Command.AbilityId!, PlayerId = held.Command.PlayerId, CardId = held.Command.CardId, Turn = _state.TurnNumber });
                    Emit("iraPending", $"{Definition(source).Name}: {Definition(performer).Name} will have -3F after the action resolves.", player.Id, performer.Id);
                    return false;
                }
            case WlPersTornAbilities.TetsakaCavalry: GrantKeyword(source!, "Cavalry", source!.Id, GrantDuration.Turn); return false;
            case WlPersTornAbilities.TetsakaNaval: GrantKeyword(source!, "Naval", source!.Id, GrantDuration.Turn); return false;
            case WlPersTornAbilities.SihungBoost:
                if (_state.LastResolvedAction?.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performed) && InPlay(performed))
                    ApplyModifier(performed, source!.Id, id, "Force", 2);
                return false;
            case WlPersTornAbilities.MakikoReduce:
                if (_state.PendingHonorChange is { } held2) _state.WlPersTorn.HonorGainReducedActionKey = held2.ActionKey;
                Emit("honorChangeReduction", $"{Definition(source!).Name}: every Honor gain from the action's effects is reduced by 2.", player.Id, source!.Id);
                return false;
            case WlPersTornAbilities.ShinokoBrash:
                GrantKeyword(target!, "Brash", source!.Id, GrantDuration.Turn);
                return false;
            case WlPersTornAbilities.DaitoBow:
            case WlPersTornAbilities.DaitoGold:
                // "until after the next Straighten Phase ends": the next one to end, whoever's turn it is (no owner).
                AddStraightenHold(target!, Definition(source!).Name, null, StraightenPoint.StraightenPhaseEnds);
                return false;
            case WlPersTornAbilities.TansenRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(c.ability, command, source).Id, c.consumedGrant);
            case WlPersTornAbilities.TansenProvince:
                GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, _state.CurrentBattleProvince!.Value, -3, Definition(source!).Name);
                return false;
            default:
                return null;
        }
    }

    // Shaocheng: the card is turned face up; a non-Unique Personality is discarded from its province (refilled as after any discard from it) and becomes honorably dead.
    private void WlPersTornTurnProvinceCard(CardInstance card, CardInstance source)
    {
        var owner = Player(card.OwnerId);
        card.FaceUp = true;
        Emit("provinceCardTurned", $"{Definition(source).Name}: {Definition(card).Name} was turned face up in {owner.Name}'s province.", owner.Id, card.Id);
        if (Definition(card).Type != CardType.Personality || Keyword(card, "Unique")) return;
        var (province, slot) = FindProvinceSlot(owner, card.Id);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; ClearProvinceSlot(province, slot);
        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province {province.Index + 1} ({Definition(source).Name}).", owner.Id, card.Id);
        MakeDead(card, true);
        RefillProvinceSlot(owner, province, slot);
    }

    // ApplyBranchEffect's arm: Nanae's effects, applied in the printed order (straighten, -4F, bow if Shadowlands) to the command's target; false for any other effect.
    private bool ApplyWlPersTornBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith("wlpt:", StringComparison.Ordinal)) return false;
        var target = Card(command.TargetId); var source = Card(command.CardId);
        if (!InPlay(target)) return true;
        switch (effect)
        {
            case WlPersTornAbilities.EffectStraighten: Straighten(target); break;
            case WlPersTornAbilities.EffectWeaken: ApplyModifier(target, source.Id, WlPersTornAbilities.NanaeJade, "Force", -4); break;
            case WlPersTornAbilities.EffectBowShadowlands:
                if (MatchesTargetStatus(target, TargetStatus.IsShadowlands) && !StalwartNegatesBow(target, command.PlayerId)) target.Bowed = true;
                break;
        }
        return true;
    }

    // The attack Tansen's Ranged ability makes: Ranged, strength 3.
    private static int? WlPersTornAttackStrength(string abilityId) => abilityId == WlPersTornAbilities.TansenRanged ? 3 : null;

    // The kind of effect an action has on its target, for the Reactions that negate one: Akahiko moves his target home.
    private static string? WlPersTornMoveKind(string abilityId) => abilityId == WlPersTornAbilities.AkahikoHome ? "move" : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlPersTornHooks()
    {
        EngineHooks.Abilities.Register("AddWlPersTorn", 14400, static abilities => abilities.AddWlPersTorn());
        EngineHooks.RunEffects.Register("RunWlPersTorn", 14400, static (engine, context) => engine.RunWlPersTorn(context));
        EngineHooks.ValidateMiddle.Register("ValidateWlPersTorn", 14400, static (engine, context) => engine.ValidateWlPersTorn(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.TargetAllows.Register("WlPersTornTargetAllows", 14400, static (engine, context) => engine.WlPersTornTargetAllows(context.ability.Target, context.target, context.player));
        EngineHooks.TargetedReaction.Register("WlPersTornTargetedReaction", 14400, static (engine, context) => engine.WlPersTornTargetedReaction(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.PersonalityForce.Register("WlPersTornForce", 14400, static (engine, context) => engine.WlPersTornForce(context.card));
        EngineHooks.AttackKind.Register("WlPersTornAttackKind", 14400, static context => context.abilityId == WlPersTornAbilities.TansenRanged ? "Ranged" : null);
        EngineHooks.AttackStrengthBefore.Register("WlPersTornAttackStrength", 14400, static (engine, context) => WlPersTornAttackStrength(context.abilityId));
        EngineHooks.BowsTarget.Register("WlPersTornBowsTarget", 14400, static context => context.abilityId is WlPersTornAbilities.IzunaBowBattle or WlPersTornAbilities.IzunaBowLimited);
    }
}
