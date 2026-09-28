namespace L5R.Core;

// Emperor Edition Stronghold pass (issue #486), batch 3 (stronghold-wave-19): The Khan's Estate, The Otoro Estate, The Shadow's Lair, The Spider's Web, The Temple of Death - experienced,
// Watchful Eye Dojo and Yasuki Palaces - experienced. The declarative ability data lives here (added to AbilityCatalog.All by one hook); each card's printed text and the reading taken
// are at its entry. The Waystation of the Path stays unwired: see tools/content/ee_sh_batch3_blockers.json.
//
// New primitives (each documented where it is used):
//   * a lock on assignment to ATTACK only (MovementKinds.AssignAttack): The Khan's Estate;
//   * a PRIVATE record of the face-down province cards a player looked at (MatchState.ProvinceSights, MatchView.ProvinceSights): The Otoro Estate;
//   * a multi-unit "move each home" whose target set is bounded by combined Force, once per battle, unnegatable while its controller has a Ninja: The Shadow's Lair;
//   * an in-play Reaction watching the entry of the controller's own Personality (trigger "own-personality-enters-play"), whose attach names the entering Personality as the wielder
//     (AttachWielder.Entered): The Spider's Web;
//   * a Duel refusal consequence and Iaijutsu Open duel from a Stronghold (Dueling.cs): Watchful Eye Dojo;
//   * a Gold reduction only while paying for a Personality (ProductionCondition.PayingForPersonality): Yasuki Palaces.
public static class EmperorStrongholdBatch3Abilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorStrongholdBatch3(this Dictionary<string, AbilityDefinition> all)
    {
        // The Khan's Estate (oracle-l5r-8182): "Tactical Limited: Choose your performing Tactician: He may not be assigned to attack this turn. Draw a card." The Tactician is chosen at
        // any location, bowed or not (no "unbowed" is printed); the lock is on assignment to an attacking army only (he may still defend), and the draw is independent of it.
        all["khans-estate-draw"] = new("Limited", "Tactical Limited: Choose your performing Tactician: He may not be assigned to attack this turn. Draw a card", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Tactician"], ActionKeywords: ["Tactical"]);
        // The Otoro Estate (oracle-l5r-8246): "You do not lose Honor from Ninja cards you own." is its Effect. "Ninja Open: Look at all face-down cards in one player's provinces." names a
        // player (either); the engine records what that player's face-down province cards are, for the looking player's view alone. "Ninja Battle: Choose your performing unbowed Ninja
        // Personality: Ranged 4 Attack. You may move your Ninja home." -- the performing Ninja is "your Ninja"; "may" is a pair of actions sharing one use.
        all["otoro-look"] = new("Open", "Ninja Open: Look at all face-down cards in one player's provinces", "player", ActionKeywords: ["Ninja"]);
        all["otoro-attack"] = new("Battle", "Ninja Battle: Choose your performing unbowed Ninja Personality: Ranged 4 Attack", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"], UsageKey: "otoro-attack", ActionKeywords: ["Ninja"]);
        all["otoro-attack-move"] = new("Battle", "Ninja Battle: Choose your performing unbowed Ninja Personality: Ranged 4 Attack. Move your Ninja home", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"], UsageKey: "otoro-attack", ActionKeywords: ["Ninja"]);
        // The Shadow's Lair (oracle-l5r-8316): "You do not lose Honor from Ninja and Fate cards you own." is its Effect. "Ninja Battle: Once per battle, even if this card is bowed and even
        // if you control no units at the current battlefield, target one or more unopposed units with combined total Force of 9 or lower, or one opposed unit: Move each target home; this
        // will not be negated or delayed if you control a Ninja." A unit is targeted through its Personality (core rules, Units); its Force is the unit's total (the Personality and his
        // Followers). Opposed is the core-rules term (the enemy has a unit at that battlefield); every unit at one battlefield is opposed or none is.
        all["shadows-lair-move"] = new("Battle", "Ninja Battle: Once per battle, target one or more unopposed units with combined total Force of 9 or lower, or one opposed unit: Move each target home", "slots",
            Absent: true, UsesPerTurn: AbilityUseLimit.Unlimited, ActionKeywords: ["Ninja"],
            Slots: [new("units", "personality", "One or more unopposed units with combined total Force of 9 or lower, or one opposed unit", 1, 12)]);
        // The Spider's Web (oracle-l5r-8352): "After your Personality enters play: You may attach a Weapon to him from your hand, paying all costs but, once per turn, paying 4 less Gold if he
        // is a Kensai." Printed with no "Reaction:" label, so a triggered trait: mandatory to trigger but the attach is a "may", not limited to once per turn and usable while the card is
        // bowed (core rules, Traits and effects). The "once per turn" is the discount's: the first attach each turn onto a Kensai costs 4 less (once a turn, and forced while it is
        // available); every other attach, and every one onto a non-Kensai, is at full price. Two actions, told apart by the wielder.
        all["spiders-web-attach"] = new("Reaction", "After your Personality enters play: attach a Weapon to him from your hand, paying all costs", "attachment-in-discard",
            Trigger: "own-personality-enters-play", UsesPerTurn: AbilityUseLimit.Unlimited, Cost: GoldCostRule.AttachCost(),
            Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Entered, [CardType.Item], ["Weapon"]));
        all["spiders-web-attach-kensai"] = new("Reaction", "After your Personality enters play: attach a Weapon to him from your hand, paying all costs but 4 less Gold if he is a Kensai (once per turn)", "attachment-in-discard",
            Trigger: "own-personality-enters-play", Cost: GoldCostRule.AttachCost(-4, 0), UsageKey: MatchEngine.SpidersWebDiscountKey,
            Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Entered, [CardType.Item], ["Weapon"]));
        // The Temple of Death - experienced (oracle-l5r-8373): "Limited: Target a discarded Personality: He becomes honorably dead." (either player's, Shunsuke's and the Training shape.)
        // "Battle/Open: If you own a dead Personality, choose your performing unbowed Shugenja and target a Personality: Straighten him. You may bow him if this is the Combat Segment."
        // "You may" is a pair of actions sharing one use; the bowing one is legal in the Battle phase (the Combat Segment) only, and bows him after he straightens.
        all["temple-of-death-dead"] = new("Limited", "Target a discarded Personality: He becomes honorably dead", "discarded-personality");
        all["temple-of-death-straighten"] = new("BattleOrOpen", "If you own a dead Personality, choose your performing unbowed Shugenja and target a Personality: Straighten him", "personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Shugenja"], UsageKey: "temple-of-death-straighten");
        all["temple-of-death-straighten-bow"] = new("BattleOrOpen", "If you own a dead Personality, choose your performing unbowed Shugenja and target a Personality: Straighten him, then bow him (Combat Segment)", "personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Shugenja"], UsageKey: "temple-of-death-straighten");
        // Watchful Eye Dojo (oracle-l5r-9246): "Iaijutsu Open: Even if this card is bowed, choose your performing unbowed Personality and target another player's Personality: Your performer
        // challenges the target. He may refuse; if he did, dishonor him and gain 1 Honor. Bow the duel's loser." Duel to First Blood's refusable challenge (the performer challenges), the
        // refusal's consequence and the loser's bow in Dueling.cs. A refusal is no duel, so there is no loser to bow.
        all["watchful-eye-duel"] = new("Open", "Iaijutsu Open: Choose your performing unbowed Personality and target another player's Personality: Your performer challenges the target. He may refuse; if he did, dishonor him and gain 1 Honor. Bow the duel's loser",
            "enemy-personality", Performer: "own-unbowed-personality", IssuesDuel: true, DuelRefusable: true, ActionKeywords: ["Iaijutsu"]);
        // Yasuki Palaces - experienced (oracle-l5r-9459): "This card produces 1 less Gold when paying for a Personality." is its Effect (ConditionalProduction). "Battle: Even if this card is
        // bowed and even if you control no units at the current battlefield, target an attacking Personality and pay 3 Gold: Move him home. If he moved, his controller loses 1 Honor."
        // Asahina Munefusa's Absent move-home of an attacking Personality, with the Honor loss going to the target's controller.
        all["yasuki-palaces-move"] = new("Battle", "Even if this card is bowed, target an attacking Personality and pay 3 Gold: Move him home. If he moved, his controller loses 1 Honor", "attacking",
            Absent: true, Cost: GoldCostRule.Fixed(3));
        // The Waystation of the Path (oracle-l5r-9267): "Reaction: After another player's action targets your performing Henshin, target another of your Personalities. Transfer his
        // attachments and tokens to the other Personality. The action's effects that would destroy your Henshin instead overlay him onto the Personality." (Mendon's ruling: "The
        // Henshin is reborn" is flavor text with no rules effect.) A targeted-by-action Reaction taking one option per other Personality of the controller's (Castle of Order's shape);
        // the performing Henshin is the Personality the action targets.
        all["waystation-reborn"] = new("Reaction", "After another player's action targets your performing Henshin, target another of your Personalities: Transfer his attachments and tokens to the other Personality. The action's effects that would destroy your Henshin instead overlay him onto the Personality",
            "own-personality-other-than-held-target", Trigger: ReactionTriggers.TargetedByAction);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- static traits ------------------------------------------------------------------------------------------------------------------------------------

    internal const string OtoroEstateEffect = "otoro-estate", ShadowsLairEffect = "shadows-lair", YasukiPalacesEffect = "yasuki-palaces-gold";

    // "You do not lose Honor from Ninja cards you own" (The Otoro Estate) and "...from Ninja and Fate cards you own" (The Shadow's Lair): read where HonorLossNegated looks at the
    // Stronghold's Effect (the source card is the losing player's own, checked by the caller).
    private bool StrongholdHonorImmunityBatch3(string strongholdEffect, CardInstance source) => strongholdEffect switch
    {
        OtoroEstateEffect => Keyword(source, "Ninja"),
        ShadowsLairEffect => Keyword(source, "Ninja") || IsFateCardType(Definition(source).Type),
        _ => false
    };

    internal bool HonorLossNegatedForTest(int playerId, string sourceCardId) => HonorLossNegated(playerId, Card(sourceCardId));

    // ---- abilities used while their card is bowed ----------------------------------------------------------------------------------------------------------

    // "Even if this card is bowed" (Shadow's Lair, Watchful Eye Dojo, Yasuki Palaces) and a triggered trait (The Spider's Web: usable while bowed, core rules, Traits and effects).
    private static bool EmperorStrongholdBatch3BowedOk(string id) =>
        id is "shadows-lair-move" or "watchful-eye-duel" or "yasuki-palaces-move" or "spiders-web-attach" or "spiders-web-attach-kensai";

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------

    private const string ShadowsLairBattleKey = "shadows-lair-move:battle";

    // A unit's total Force: the Personality and his Followers (the Force each contributes to an army, the same reading as the Force compared for an Attack).
    private int UnitTotalForce(CardInstance personality) =>
        CardForce(personality) + Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower).Sum(CardForce);

    private void ValidateEmperorStrongholdBatch3(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case "temple-of-death-straighten": case "temple-of-death-straighten-bow":
                Require(_state.Cards.Values.Any(c => c.OwnerId == player.Id && c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality), "You must own a dead Personality.");
                if (id == "temple-of-death-straighten-bow") Require(_state.Phase == Phase.Battle, "He may be bowed only in the Combat Segment.");
                break;
            case "shadows-lair-move":
            {
                // A slot's candidate is judged alone (ability.Slots is null there): a unit that is not opposed must itself have total Force of 9 or lower. The whole selection is judged below.
                if (ability.Slots is null)
                {
                    if (command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var candidate) && Definition(candidate).Type == CardType.Personality && !Opposed(candidate))
                        Require(UnitTotalForce(candidate) <= 9, "That unit's total Force is above 9.");
                    break;
                }
                Require(!(source!.AbilityUses.TryGetValue(ShadowsLairBattleKey, out var battle) && battle == _state.BattleNumber), "The Shadow's Lair may be used once per battle.");
                var units = SlotCards(command, "units").ToArray();
                Require(units.Length > 0, "Choose at least one unit.");
                if (units.Any(Opposed)) Require(units.Length == 1, "Choose one opposed unit, or unopposed units only.");
                else Require(units.Sum(UnitTotalForce) <= 9, "The units' combined total Force must be 9 or lower.");
                break;
            }
            case "spiders-web-attach": case "spiders-web-attach-kensai":
            {
                var entered = Card(_state.PendingChoice!.CardId!);
                var discountSpent = source!.AbilityUses.TryGetValue(SpidersWebDiscountKey, out var stamp) && stamp == _state.TurnNumber;
                var kensai = Keyword(entered, "Kensai");
                if (id == "spiders-web-attach-kensai") Require(kensai, "The discount is for a Kensai.");
                else Require(!kensai || discountSpent, "The first attach each turn to a Kensai costs 4 less Gold.");
                break;
            }
        }
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------

    // true: the action paused (an Attack's reaction window, an entering-play window) and finishes from there; false: it is finished here; null: not this batch's.
    private bool? RunEmperorStrongholdBatch3(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // "He may not be assigned to attack this turn. Draw a card."
            case "khans-estate-draw":
                LockMovement(Card(command.PerformerId!), Definition(source!).Name, MovementKinds.AssignAttack);
                Draw(player);
                return false;
            case "otoro-look":
                RecordProvinceSight(player, command.TargetPlayerId!.Value, source!);
                return false;
            // The performing Ninja attacks; "you may move your Ninja home" is the second action's after-Attack step (AfterEmperorStrongholdBatch3Attack).
            case "otoro-attack": case "otoro-attack-move":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case "shadows-lair-move":
                source!.AbilityUses[ShadowsLairBattleKey] = _state.BattleNumber;
                foreach (var unit in SlotCards(command, "units").ToArray()) if (InPlay(unit)) MoveHome(unit, false);
                return false;
            case "temple-of-death-dead":
                MakeDead(target!, true);
                return false;
            case "temple-of-death-straighten":
                Straighten(target!);
                return false;
            case "temple-of-death-straighten-bow":
                Straighten(target!);
                if (InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "yasuki-palaces-move":
                MoveHome(target!, false);
                // "If he moved": a negated move leaves him at the battlefield (Asahina Munefusa's own test).
                if (target!.Zone == Zone.Home) GainHonor(target.OwnerId, -1, "Yasuki Palaces", source);
                return false;
            case "spiders-web-attach": case "spiders-web-attach-kensai":
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            case "waystation-reborn":
                WaystationReborn(Card(HeldAction().TargetId!), target!, source!);
                return false;
        }
        return null;
    }

    // ResolveAttackNow's hook: "You may move your Ninja home" after The Otoro Estate's Ranged Attack, whether or not the Attack hit. The performer is the Attack's source.
    private void AfterEmperorStrongholdBatch3Attack(string abilityId, string? sourceId)
    {
        if (abilityId != "otoro-attack-move" || sourceId is null || !_state.Cards.TryGetValue(sourceId, out var ninja) || !InPlay(ninja) || ninja.Zone != Zone.Battlefield) return;
        MoveHome(ninja, false);
    }

    // ---- The Waystation of the Path ------------------------------------------------------------------------------------------------------------------------

    private const string OverlayInsteadPrefix = "overlay-instead:";

    // "After another player's action targets your performing Henshin": the Henshin is the Personality the held action targets (the performer this Reaction names), another player's action,
    // and there is another Personality of the controller's to move his things to.
    private bool WaystationApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Henshin")
        && WaystationCandidates(card, affected).Any();

    private IEnumerable<CardInstance> WaystationCandidates(CardInstance card, CardInstance henshin) =>
        _state.Cards.Values.Where(c => c.OwnerId == card.OwnerId && c.Id != henshin.Id && InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    // The Reaction's effects: his attachments (those that may still attach to the other Personality -- attaching restrictions still apply; the rest stay with him) and all his tokens are
    // transferred (AttachmentTransfers.cs, MoveToken), and the held action's destruction of the Henshin is replaced by his overlay onto the other Personality (OverlayInsteadOfDestruction).
    private void WaystationReborn(CardInstance henshin, CardInstance other, CardInstance stronghold)
    {
        foreach (var attachment in Attachments(henshin.Id).ToArray())
            if (CanTransferTo(attachment, other)) TransferAttachment(attachment, other, Definition(stronghold).Name);
        foreach (var token in henshin.Tokens.ToArray()) MoveToken(henshin, token, other);
        _state.NegatedEffects.Add(new(OverlayInsteadPrefix + other.Id, henshin.Id));
        Emit("overlayInstead", $"{Definition(stronghold).Name}: the action's destruction of {Definition(henshin).Name} will instead overlay him onto {Definition(other).Name}.", stronghold.OwnerId, henshin.Id);
    }

    // NegatesEffect's hook for "destroy": a destruction of the Henshin the held action would cause is replaced by the overlay -- his card takes the other Personality's place with all the
    // state, attachments and tokens the other Personality carries (the Experienced overlay's own transfer, TransferOverlayState), and that Personality's card leaves the game, without
    // entering play. Nothing is replaced when the other Personality is no longer there to overlay: the destruction then happens.
    private bool OverlayInsteadOfDestruction(CardInstance henshin)
    {
        var entry = _state.NegatedEffects.FirstOrDefault(n => n.CardId == henshin.Id && n.Kind.StartsWith(OverlayInsteadPrefix, StringComparison.Ordinal));
        if (entry is null) return false;
        _state.NegatedEffects.Remove(entry);
        if (!_state.Cards.TryGetValue(entry.Kind[OverlayInsteadPrefix.Length..], out var other) || !InPlay(other) || !InPlay(henshin) || other.OwnerId != henshin.OwnerId || Definition(other).Type != CardType.Personality) return false;
        var name = Definition(other).Name;
        TransferOverlayState(other, henshin);
        RemoveFromGame(other);
        Emit("personalityOverlayed", $"{Definition(henshin).Name} was overlaid onto {name} instead of being destroyed.", henshin.OwnerId, henshin.Id);
        return true;
    }

    // ---- The Spider's Web ------------------------------------------------------------------------------------------------------------------------------------

    internal const string SpidersWebDiscountKey = "spiders-web-kensai";

    // The in-play watchers of a Personality's entry: the abilities of the controller's Stronghold that react to "your Personality enters play" (The Spider's Web).
    private IEnumerable<string> StrongholdEntryWatchers(int playerId, CardInstance entered)
    {
        if (Definition(entered).Type != CardType.Personality || entered.OwnerId != playerId) yield break;
        var stronghold = Card(Player(playerId).StrongholdId);
        foreach (var abilityId in AbilityIdsOf(stronghold))
            if (AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Timing == "Reaction" && ability.Trigger == "own-personality-enters-play") yield return abilityId;
    }

    // A window is worth opening only while there is something for the watcher to attach: a Weapon in the controller's hand.
    private bool StrongholdEntryWatcherAvailable(int playerId, CardInstance entered) =>
        StrongholdEntryWatchers(playerId, entered).Any() && Player(playerId).Hand.Select(Card).Any(c => Definition(c).Type == CardType.Item && Keyword(c, "Weapon"));

    // ---- The Otoro Estate's look --------------------------------------------------------------------------------------------------------------------------

    // "Look at all face-down cards in one player's provinces": the looking player is told what each is (MatchView.ProvinceSights, private to the viewer, by province -- never a card
    // id); everyone else is told only, by a public event, that a look happened and how many cards it covered. The record lasts for the turn it was made.
    private void RecordProvinceSight(PlayerState viewer, int ownerId, CardInstance source)
    {
        _state.ProvinceSights.RemoveAll(s => s.Turn != _state.TurnNumber);
        var count = 0;
        foreach (var province in Player(ownerId).Provinces.Where(p => p.CardId is not null))
        {
            var card = Card(province.CardId!);
            if (card.FaceUp) continue;
            _state.ProvinceSights.Add(new(viewer.Id, ownerId, province.Index, card.Id, card.DefinitionId, _state.TurnNumber));
            count++;
        }
        Emit("provincesLooked", $"{viewer.Name} looks at the {count} face-down card{(count == 1 ? "" : "s")} in {(ownerId == viewer.Id ? "their own" : $"{Player(ownerId).Name}'s")} provinces ({Definition(source).Name}).", viewer.Id, source.Id);
    }

    private ProvinceSightView[]? BuildProvinceSights(int viewerId)
    {
        var sights = _state.ProvinceSights.Where(s => s.ViewerId == viewerId && s.Turn == _state.TurnNumber
            && Player(s.OwnerId).Provinces[s.Province].CardId == s.CardId && !Card(s.CardId).FaceUp).ToArray();
        return sights.Length == 0 ? null : sights.Select(s => new ProvinceSightView(s.OwnerId, s.Province, s.DefinitionId, Copy(Definition(Card(s.CardId))))).ToArray();
    }

    // ---- Watchful Eye Dojo --------------------------------------------------------------------------------------------------------------------------------

    // "He may refuse; if he did, dishonor him and gain 1 Honor": the refuser is dishonored and the challenger's player gains the Honor.
    private void WatchfulEyeRefusal(PendingDuel duel)
    {
        Dishonor(Card(duel.ChallengedId), duel.ChallengerPlayerId);
        GainHonor(duel.ChallengerPlayerId, 1, "Watchful Eye Dojo");
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorStrongholdBatch3_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorStrongholdBatch3", 300, static abilities => abilities.AddEmperorStrongholdBatch3());
    }
}
