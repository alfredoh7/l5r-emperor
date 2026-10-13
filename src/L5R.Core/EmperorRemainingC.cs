using System.Text.RegularExpressions;

namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group C (strategy-wave-60): the Strategies of the Emperor Edition reference set that were still unwired, re-checked against the
// engine as it stands. The declarative ability data is in EmperorRemainingCAbilities (added to AbilityCatalog.All by one hook); each card's printed text (the scan's, read by eye) and the
// reading taken are documented at its entry. The engine helpers below are documented where they are used; the shared files gained only small additive hooks, each marked "issue #492".
//
// New primitives (each documented where it is built):
//   * STRATEGY REACTIONS "before an action resolves" PLAYED FROM THE HAND (The Height of Courage, Rhetoric, Solid Defense): BeforeActionReactionCandidates also lists Strategies in a hand, so
//     the alternating before-action window opens for a Strategy holder exactly as it does for an in-play watcher (either player may react to any action -- Rhetoric's own text names no
//     "another player's"), and lists them with their performers;
//   * A REDIRECT OF THE ACTION'S HONOR CHANGE (Rhetoric) and a NEGATION OF ITS HONOR LOSSES (Solid Defense), both marks on the held action read where Honor changes (GainHonor);
//   * A REDUCTION OF EVERY HONOR GAIN outside the Attack Phase until its controller's next turn begins (Rumors Travel), read there too;
//   * AN ADDITIONAL ACTION THE NAMED PERFORMER MUST PERFORM (Spinning Heel Kick): AdditionalActionGrant.PerformerId;
//   * A HAND STRATEGY'S "AFTER ANOTHER PLAYER'S BATTLE ACTION TARGETS YOUR ..." (Snake Tattoo, Yurishi's shape with a chosen adjacent battlefield), an ATTACK-WINDOW list of hand Reactions
//     (Sniping; the before-attack-resolves window now lists Strategies in a hand), and a FOCUS EFFECT that destroys an attachment at the battlefield (Superior Opponent, Crimson Mark's choice);
//   * A COST PAID BY EVERY CARD OF A PERFORMING SLOT (Shadows Walk: ActionCost subject AllPerformingSlots) and TAKING CONTROL OF A PERSONALITY IN PLAY (TakeControlOfPersonality: Loyal and Unique
//     refuse it, the unit changes controller, every card returns to its owner when it leaves play);
//   * A SECOND USE OF EVERY STRONGHOLD ABILITY (The Fires of War: a per-turn mark on the Stronghold read by AbilityLimits.EffectiveUsesPerTurn);
//   * ROCKY TERRAIN: Heart of Darkness's "beforeBattleOptional" choice over the attacking Personalities; THE EMPRESS' ADDRESS: an after-action and an after-battle Reaction bringing a discarded
//     Personality into play (the destroyed Personalities' locations are now on the action's record, AffectedCard.FromBattlefield);
//   * SACRIFICE OF PAWNS: an Either/Or whose refusal opens a Dynasty deck search that RECRUITS a Personality for his entry price plus 2 (SearchEffect.RecruitPersonality, QuotePurchase's fromDynastyDeck);
//   * RETRIBUTION: an ADDITIONAL ATTACK PHASE -- a window offered to the Defender after the last battle (MatchState.PendingChoice "attackPhaseEndReaction"), whose Reaction swaps the roles for
//     a whole further Attack Phase (ActivePlayerId is the new Attacker for its length and restored for the Dynasty Phase).
public sealed class EmperorRemainingCState
{
    /// <summary>Rumors Travel: the players whose Limited action is in force until THEIR next turn begins, one entry per copy played (each reduces every gain by 1).</summary>
    public List<int> RumorsTravelOwners { get; set; } = [];
    /// <summary>Rhetoric: the action whose Honor change is redirected (EmperorStrongholdBatch2.ActionKey), to whom, by which Courtier's Chi, and whether it has happened.</summary>
    public string? RedirectActionKey { get; set; }
    public int RedirectToPlayer { get; set; }
    public string? RedirectCourtierId { get; set; }
    public bool RedirectSpent { get; set; }
    /// <summary>Which of the action's Honor changes (in order, from 0) the redirect is about: Rhetoric is played "before a player gains or loses Honor", so it applies to that one change only (R4, issue #492).</summary>
    public int RedirectOccurrence { get; set; } = -1;
    /// <summary>Retribution: the player whose turn it is, while an ADDITIONAL Attack Phase is under way (-1 otherwise); the phase's Attacker is then the Defender who declared it (ActivePlayerId is swapped
    /// for its duration and restored when the last such phase ends), and how many Personalities have been assigned to attack in it.</summary>
    public int RetributionRoot { get; set; } = -1;
    public int RetributionAssigned { get; set; }
    /// <summary>Solid Defense: the action whose Honor losses are negated.</summary>
    public string? NegateLossesActionKey { get; set; }
}

public static class EmperorRemainingCAbilities
{
    /// <summary>The Reactions whose card is a Strategy played from a hand "before an action resolves" (see the file comment).</summary>
    /// <summary>Retribution's trigger: "After the end of an Attack Phase" (a window offered to the phase's Defender, MatchState.PendingChoice "attackPhaseEndReaction").</summary>
    public const string AttackPhaseEnd = "after-attack-phase-ends";

    /// <summary>A cost subject naming EVERY card of the ability's performing slot(s): "Bow one or more of your performing ..." (Shadows Walk).</summary>
    public const string AllPerformingSlots = "all-performing-slots";

    // Rhetoric and Solid Defense are no longer here: they are played in the window before an Honor change (Mendon's R4 ruling, EmperorRulingR4Honor.cs), not before an action whose label was guessed to change Honor.
    public static readonly string[] BeforeActionHandReactions = ["height-courage-negate"];

    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingC(this Dictionary<string, AbilityDefinition> all)
    {
        // Tell the Tale (oracle-l5r-7862): "Limited: Gain 1 Honor."
        all["tell-the-tale-honor"] = new("Limited", "Limited: Gain 1 Honor", "none");
        // Strategic Strike (oracle-l5r-7525): "Battle: Bow your performing Scout Personality: Ranged 6 Attack, which may target a Personality with attached Followers if you have Reconnaissance."
        // A named performer, bowed as the cost (no "unbowed" is printed but a bowed Scout could not pay it); the Follower exception is Reconnaissance-gated (AttackFollowerException).
        all["strategic-strike-attack"] = new("Battle", "Bow your performing Scout Personality: Ranged 6 Attack, which may target a Personality with attached Followers if you have Reconnaissance", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Scout"], Costs: ActionCost.BowPerformer, FollowerException: new("Ranged", Condition: AttackFollowerException.Reconnaissance));
        // Sniping (oracle-l5r-7312): "Reaction: After a Ranged Attack is targeted: Give it a strength bonus equal to its current strength." -- the before-attack-resolves window every Attack reaction uses.
        all["sniping-boost"] = new("Reaction", "After a Ranged Attack is targeted: Give it a strength bonus equal to its current strength", "none", Trigger: "before-attack-resolves");
        // The Height of Courage (oracle-l5r-8126): "Reaction: Before the resolution of another player's action, choose your performing Personality: Negate his movement from the action's effects."
        // The performer is a Personality of the reactor's own that the action would move (the movement negated is HIS); and "Battle: Choose your performing Personality: Give him and each of his
        // Followers +1F." (any Personality at the battlefield, bowed or not).
        all["height-courage-negate"] = new("Reaction", "Before the resolution of another player's action, choose your performing Personality: Negate his movement from the action's effects", "none",
            Performer: "own-personality", Trigger: "before-action-resolves");
        all["height-courage-boost"] = new("Battle", "Choose your performing Personality: Give him and each of his Followers +1F", "none", Performer: "own-personality-at-battle");
        // Selfless Defense (oracle-l5r-6584): "Battle: Choose your performing unbowed Yojimbo Personality and target an enemy Personality: Move him home. Bow him if you control a Courtier or
        // Shugenja." ("him" is the target throughout; the bow does not depend on his having moved.) "Battle: Destroy your performing Yojimbo and target one or more of your Courtier or Shugenja
        // Personalities: Move them home." -- the destruction is a cost (the Sacrifice verb, an ordinary destruction), so the performer cannot also be one of the targets.
        all["selfless-defense-move"] = new("Battle", "Choose your performing unbowed Yojimbo Personality and target an enemy Personality: Move him home. Bow him if you control a Courtier or Shugenja", "enemy-personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Yojimbo"], Move: new(MoveSpec.Target, MoveSpec.Home));
        all["selfless-defense-destroy"] = new("Battle", "Destroy your performing Yojimbo and target one or more of your Courtier or Shugenja Personalities: Move them home", "slots",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Yojimbo"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)],
            Slots: [new("targets", "remc-own-courtier-or-shugenja-other-than-performer", "One or more of your Courtier or Shugenja Personalities", 1, 12)]);
        // Summoned to Justice (oracle-l5r-7639, Political): "Battle: Choose your performing unbowed Courtier at any location and target an attacking Personality: Move him home. If he moved, bow
        // him. If he is dishonorable, his controller loses Honor equal to his base Personal Honor." -- "him" is the target throughout.
        all["summoned-justice-move"] = new("Battle", "Choose your performing unbowed Courtier at any location and target an attacking Personality: Move him home. If he moved, bow him. If he is dishonorable, his controller loses Honor equal to his base Personal Honor",
            "attacking-personality", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Move: new(MoveSpec.Target, MoveSpec.Home));
        // Spinning Heel Kick (oracle-l5r-7413, Air Kiho): "Battle: Choose your performing Monk: Straighten him. You may take an additional Battle action, which he must perform."
        all["spinning-heel-kick-straighten"] = new("Battle", "Choose your performing Monk: Straighten him. You may take an additional Battle action, which he must perform", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Monk"]);
        // Shameful Injury (oracle-l5r-6698): "Battle: Even if you control no units at the current battlefield, target a Personality: Negate all current and new Force bonuses and penalties on him
        // from actions." -- Creating Order's negation (ForgottenLegacyBatch3.cs) on one target; Absent (no presence needed), the target keeps its at-the-battlefield rule.
        all["shameful-injury-negate"] = new("Battle", "Even if you control no units at the current battlefield, target a Personality: Negate all current and new Force bonuses and penalties on him from actions", "personality", Absent: true);
        // Solid Defense (oracle-l5r-7329, scan: Political Reaction and no keyword line): "Political Reaction: Before the resolution of another player's action, discard the Imperial Favor unless you
        // control a Personality with 5 or higher Force: Negate any Honor losses from the action's effects." The cost is conditional, so it is two actions sharing one use: without a Personality
        // of 5 or higher Force the Favor is discarded (a Favor-discard action, EmperorStrongholdBatch1), with one nothing is. "Battle: Target a Personality without attachments: Move him home."
        all["solid-defense-negate"] = new("Reaction", "Political Reaction: Before the resolution of another player's action, if you control a Personality with 5 or higher Force: Negate any Honor losses from the action's effects", "none",
            Trigger: HonorChangeReactions.Trigger, UsageKey: "solid-defense-negate", ActionKeywords: ["Political"]);
        all["solid-defense-negate-favor"] = new("Reaction", "Political Reaction: Before the resolution of another player's action, discard the Imperial Favor: Negate any Honor losses from the action's effects", "none",
            Trigger: HonorChangeReactions.Trigger, UsageKey: "solid-defense-negate", ActionKeywords: ["Political"]);
        all["solid-defense-move"] = new("Battle", "Target a Personality without attachments: Move him home", "personality", RequireNoAttachments: true, Move: new(MoveSpec.Target, MoveSpec.Home));
        // Rhetoric (oracle-l5r-6312, Political): "Reaction: Before a player gains or loses Honor from an action's effects, bow your performing Courtier: Choose another player. If the Courtier's Chi is
        // higher than or equal to the Honor gain or loss, the chosen player instead of the original player takes the gain or loss."
        all["rhetoric-redirect"] = new("Reaction", "Before a player gains or loses Honor from an action's effects, bow your performing Courtier: Choose another player. If the Courtier's Chi is higher than or equal to the Honor gain or loss, the chosen player instead of the original player takes the gain or loss",
            "player", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Trigger: HonorChangeReactions.Trigger, Costs: ActionCost.BowPerformer);
        // Rumors Travel (oracle-l5r-6406, Political): "Limited: Until your next turn begins, before each time a player gains 1 or more Honor, reduce the gain by 1 if it is not during a battle or Attack
        // Phase." and "Battle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved." (Broken Alliance's text).
        all["rumors-travel-reduce"] = new("Limited", "Limited: Until your next turn begins, before each time a player gains 1 or more Honor, reduce the gain by 1 if it is not during a battle or Attack Phase", "none");
        all["rumors-travel-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved", "none",
            Performer: "own-personality", Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Stay Put (oracle-l5r-7474): "Battle: Choose your performing unbowed Kensai and target an enemy card without attached Weapons or Armor: Bow it. You may attach a Weapon to your Kensai from your
        // hand, paying all costs but paying 4 less Gold if he is defending." The "may" is three actions sharing one use: without the attach; with it at full price; with it 4 less while he defends
        // (the discount is not optional, Hailung's shape). The attaching ones name the Weapon first (the primary target, so its Gold cost is the attach's) and the enemy card second.
        all["stay-put-bow"] = new("Battle", "Choose your performing unbowed Kensai and target an enemy card without attached Weapons or Armor: Bow it", "remc-enemy-card-without-weapon-or-armor",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Kensai"], UsageKey: "stay-put");
        all["stay-put-bow-attach"] = new("Battle", "Choose your performing unbowed Kensai and target an enemy card without attached Weapons or Armor: Bow it. Attach a Weapon to your Kensai from your hand, paying all costs", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Kensai"], UsageKey: "stay-put", Cost: GoldCostRule.AttachCost(),
            Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Performer, [CardType.Item], ["Weapon"]),
            Slots: [new("weapon", "attachment-in-discard", "A Weapon in your hand"), new("target", "remc-enemy-card-without-weapon-or-armor", "An enemy card without attached Weapons or Armor")]);
        all["stay-put-bow-attach-discount"] = new("Battle", "Choose your performing unbowed Kensai and target an enemy card without attached Weapons or Armor: Bow it. Attach a Weapon to your Kensai from your hand, paying all costs and paying 4 less Gold, as he is defending", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Kensai"], UsageKey: "stay-put", Cost: GoldCostRule.AttachCost(-4, 0),
            Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Performer, [CardType.Item], ["Weapon"]),
            Slots: [new("weapon", "attachment-in-discard", "A Weapon in your hand"), new("target", "remc-enemy-card-without-weapon-or-armor", "An enemy card without attached Weapons or Armor")]);
        // Shadows Walk (oracle-l5r-6676, Fear Ninja): "Open: Bow one or more of your performing Shadowlands Ninja Personalities and target a card without attachments: If the card is an attachment, destroy
        // it. If it is a Personality with Chi lower than or equal to the number of performers, permanently give him Ninja and Shadowlands and take control of him. Lose 3 Honor." The performers are a
        // performing slot (Dislodge the Foe's shape) all bowed as the cost; the target is any card in play without attachments.
        all["shadows-walk-take"] = new("Open", "Bow one or more of your performing Shadowlands Ninja Personalities and target a card without attachments: If the card is an attachment, destroy it. If it is a Personality with Chi lower than or equal to the number of performers, permanently give him Ninja and Shadowlands and take control of him. Lose 3 Honor", "slots",
            Costs: [new(CostVerb.Bow, EmperorRemainingCAbilities.AllPerformingSlots)],
            Slots: [new("performers", "remc-own-unbowed-shadowlands-ninja", "Your performing Shadowlands Ninja Personalities", 1, 12, Performing: true), new("target", "remc-card-without-attachments", "A card without attachments")]);
        // The Fires of War (oracle-l5r-8084, Unique): "Battle/Open: You may use each ability on your Stronghold a second time this turn." -- a mark on the Stronghold read by the per-turn limits (AbilityLimits.cs).
        all["fires-of-war-second-use"] = new("BattleOrOpen", "You may use each ability on your Stronghold a second time this turn", "none");
        // Rocky Terrain (oracle-l5r-6374, Terrain): "Before this battle's resolution: You may bow one or two attacking Personalities at the current battlefield." -- Heart of Darkness's before-resolution choice
        // (ForgottenLegacyBatch4.cs) over the attacking Personalities; the Terrain's own "Battle: If there are no Terrains at the current battlefield: Put this card into play there." is the generic Terrain play rule.
        all["rocky-terrain-bow"] = new("BeforeResolution", "Before this battle's resolution: You may bow one or two attacking Personalities at the current battlefield", "none", Trigger: "before-battle-resolves");
        // The Empress' Address (oracle-l5r-8059, Political): "Reaction: After the resolution of an action or a battle that destroyed any of your Personalities in a defending army, bow your performing Courtier or
        // Empress and target your non-Unique Personality with your Clan alignment in your discard pile: Bring him into play (in your home), ignoring costs. Gain 2 Honor." Two Reactions (an action's and a battle's
        // resolution) sharing one use; the Personality comes from the dead or discard pile (a discarded Personality is one).
        all["empress-address-action"] = new("Reaction", "After the resolution of an action that destroyed any of your Personalities in a defending army, bow your performing Courtier or Empress and target your non-Unique Personality with your Clan alignment in your discard pile: Bring him into play (in your home), ignoring costs. Gain 2 Honor",
            "remc-own-clan-nonunique-discarded-personality", Performer: "own-unbowed-personality-with-any-keyword-at-battle", RequiredKeywords: ["Courtier", "Empress"],
            Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "empress-address", Costs: ActionCost.BowPerformer);
        all["empress-address-battle"] = new("Reaction", "After the resolution of a battle that destroyed any of your Personalities in a defending army, bow your performing Courtier or Empress and target your non-Unique Personality with your Clan alignment in your discard pile: Bring him into play (in your home), ignoring costs. Gain 2 Honor",
            "remc-own-clan-nonunique-discarded-personality", Performer: "own-unbowed-personality-with-any-keyword-at-battle", RequiredKeywords: ["Courtier", "Empress"],
            Trigger: ReactionTriggers.AfterBattleResolution, UsageKey: "empress-address", Costs: ActionCost.BowPerformer);
        // Retribution (oracle-l5r-6290): "Reaction: After the end of an Attack Phase in which you were the Defender: Declare an additional Attack Phase against that phase's Attacker, in which neither player
        // may invite allies and you may only assign one Personality. Give the Personality and each Follower in his unit +1F after he assigns." An additional Attack Phase is a whole Attack Phase with the roles
        // swapped (EmperorRemainingC.cs: the turn player is the Attacker of the ended phase's Defender for its duration); "invite allies" is a multiplayer term with no meaning between two players.
        all["retribution-declare"] = new("Reaction", "After the end of an Attack Phase in which you were the Defender: Declare an additional Attack Phase against that phase's Attacker, in which neither player may invite allies and you may only assign one Personality. Give the Personality and each Follower in his unit +1F after he assigns", "none",
            Trigger: AttackPhaseEnd);
        // Sacrifice of Pawns (oracle-l5r-6439, Political): "Limited: Destroy your performing unbowed Courtier and target another player's Personality: His controller may destroy the target. If he did not
        // choose this, search your Dynasty deck for a Personality and bring him into play, paying all costs but paying two more Gold. (Discard him if you are unable to play him.)" The destruction of the
        // Courtier is the cost (the Sacrifice verb); the target's controller decides (an Either/Or, EitherOr.cs), and a refusal starts a Dynasty deck search whose found Personality is recruited at his
        // full entry price plus 2 (SearchEffect.RecruitPersonality) or discarded when it cannot be.
        all["sacrifice-pawns-destroy"] = new("Limited", "Destroy your performing unbowed Courtier and target another player's Personality: His controller may destroy the target. If he did not choose this, search your Dynasty deck for a Personality and bring him into play, paying all costs but paying two more Gold",
            "enemy-personality", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // Snake Tattoo (oracle-l5r-7310, Tattoo): "Reaction: After another player's Battle action targets your Tattooed Personality: Move him to an adjacent battlefield. If he is no longer a legal
        // target for that action, negate its effects." Yurishi's shape (TargetedActionReactions.cs) with the move to a chosen adjacent unresolved battlefield (Flanking Unit's reading of "adjacent"),
        // played from the hand; the Personality moved is the one the held action targets.
        all["snake-tattoo-move"] = new("Reaction", "After another player's Battle action targets your Tattooed Personality: Move him to an adjacent battlefield. If he is no longer a legal target for that action, negate its effects", "none",
            Trigger: ReactionTriggers.TargetedByAction, Move: new(MoveSpec.Target, MoveSpec.AdjacentUnresolved));
        // Superior Opponent (oracle-l5r-7654, Fear Iaijutsu): "As a Focus Effect: You may destroy an attachment at the current battlefield if your Personality in this duel is there." (a choice among the
        // attachments there, Crimson Mark's shape: FocusEffectChoice.cs) and "Battle: Choose your performing unbowed Duelist and target an enemy unit: Bow each Follower and Personality in it with equal or
        // lower Force than your Duelist's Chi." -- the unit is its Personality and his Followers, each judged on its own Force.
        all["superior-opponent-focus"] = new("FocusEffect", "As a Focus Effect: You may destroy an attachment at the current battlefield if your Personality in this duel is there", "none");
        all["superior-opponent-bow"] = new("Battle", "Choose your performing unbowed Duelist and target an enemy unit: Bow each Follower and Personality in it with equal or lower Force than your Duelist's Chi", "enemy-unit",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Duelist"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // Raised once at StartTurn, cleared here (see ExpireEmperorRemainingCAtTurnStart).
    private EmperorRemainingCState RemC => _state.RemC;

    // ---- shared helpers ---------------------------------------------------------------------------------------------------------------------------------------------

    private bool ControlsPersonalityWithKeyword(int playerId, string keyword) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, keyword));

    private bool ControlsPersonalityWithForce(int playerId, int force) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && CardForce(c) >= force);

    // ---- The Strategies played from a hand "before an action resolves" ------------------------------------------------------------------------------------------------

    // The Personalities of `playerId`'s the held action would move: those its Move names, and the Personality a curated move-home or move action targets.
    private IEnumerable<CardInstance> OwnPersonalitiesActionWouldMove(int playerId, GameCommand command)
    {
        if (command.AbilityId is null) yield break;
        var ids = new HashSet<string>(ActionMovers(command), StringComparer.Ordinal);
        if (MoveOrStraightenKind(command.AbilityId) == "move")
            foreach (var target in ReactionWindowTargets(command).OfType<string>()) ids.Add(target);
        foreach (var id in ids.OrderBy(x => x, StringComparer.Ordinal))
            if (_state.Cards.TryGetValue(id, out var card) && card.OwnerId == playerId && InPlay(card) && Definition(card).Type == CardType.Personality) yield return card;
    }

    // BeforeActionReactionApplies' arm for these four (called before its target checks: an action that names no card is judged too). The card is a Strategy in its player's hand.
    private bool EmperorRemainingCBeforeActionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting) || EmperorRemainingCAbilities.BeforeActionHandReactions.Contains(command.AbilityId)) return false;
        switch (abilityId)
        {
            // Another player's action whose effects move one of the reactor's Personalities, and whose movement may be negated.
            case "height-courage-negate":
                return command.PlayerId != card.OwnerId && !MovementCannotBeNegated(command) && OwnPersonalitiesActionWouldMove(card.OwnerId, command).Any();
            default:
                return false;
        }
    }

    // BeforeActionReactionPerformers' fallback (the listing): The Height of Courage's performers are the Personalities the held action would move.
    private IEnumerable<CardInstance> EmperorRemainingCBeforeActionPerformers(int playerId, AbilityDefinition reaction) => reaction.Performer switch
    {
        "own-personality" when _state.PendingNegatableAbility is { } held => OwnPersonalitiesActionWouldMove(playerId, held),
        "own-personality" => [],
        _ => throw new InvalidOperationException($"A before-action Reaction with the performer '{reaction.Performer}' is not listed."),
    };

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorRemainingC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case "sniping-boost":
                Require(_state.PendingAttack!.Kind == "Ranged", "Sniping only reacts to a Ranged Attack.");
                break;
            // The performer is one of the Personalities the held action would move (his movement is the one negated).
            case "height-courage-negate":
                Require(_state.PendingNegatableAbility is { } held && command.PerformerId is not null && OwnPersonalitiesActionWouldMove(player.Id, held).Any(c => c.Id == command.PerformerId),
                    "Choose one of your Personalities that the action would move.");
                break;
            case "solid-defense-negate":
                Require(ControlsPersonalityWithForce(player.Id, 5), "You control no Personality with 5 or higher Force: discard the Imperial Favor instead.");
                break;
            case "solid-defense-negate-favor":
                Require(!ControlsPersonalityWithForce(player.Id, 5), "You control a Personality with 5 or higher Force: nothing is discarded.");
                break;
            case "rumors-travel-move":
                Require(_state.CurrentBattleProvince is int rumorsBattle && Army(1 - player.Id, rumorsBattle).Any(), "There are no enemy units at the current battlefield.");
                break;
            // Stay Put: the discount applies exactly while the performing Kensai is defending, and is not optional.
            case "stay-put-bow-attach" when ability.Slots is not null && command.PerformerId is not null:
                Require(!IsDefendingUnit(Card(command.PerformerId)), "The discounted attach applies while he is defending.");
                break;
            case "stay-put-bow-attach-discount" when ability.Slots is not null && command.PerformerId is not null:
                Require(IsDefendingUnit(Card(command.PerformerId)), "The discount applies only while he is defending.");
                break;
        }
    }

    // The in-play Target categories of this group (the shared category switch's fallback): false for any category that is not one of them.
    private bool EmperorRemainingCTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var personality = type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        return category switch
        {
            // Selfless Defense: "one or more of your Courtier or Shugenja Personalities" -- not the Yojimbo destroyed as the cost.
            // Stay Put: "an enemy card without attached Weapons or Armor" -- a Personality, or a Follower, Item or Spell, of the enemy's, none of whose attachments is a Weapon or an Armor.
            "remc-enemy-card-without-weapon-or-armor" => !own && (personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell)
                && !Attachments(target.Id).Any(a => Keyword(a, "Weapon") || Keyword(a, "Armor")),
            // Shadows Walk: "your performing Shadowlands Ninja Personalities" (unbowed: bowing them is the cost; Shadowlands as a Clan alignment or as the keyword the action itself grants), and "a card without attachments".
            "remc-own-unbowed-shadowlands-ninja" => personality && own && !target.Bowed && (HasClanAffiliation(target, "Shadowlands") || Keyword(target, "Shadowlands")) && Keyword(target, "Ninja"),
            "remc-card-without-attachments" => !HasAttachments(target),
            "remc-own-courtier-or-shugenja-other-than-performer" => personality && own && (Keyword(target, "Courtier") || Keyword(target, "Shugenja")) && target.Id != command.PerformerId,
            _ => false
        };
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------

    // true: the action paused (an Attack's reaction window) and finishes from there; false: it is finished here; null: not this group's.
    private bool? RunEmperorRemainingC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, HashSet<string> bowedBeforeAction)
    {
        switch (id)
        {
            // Retribution: the window it was played in closes, the Strategy is spent and the additional Attack Phase begins with its player as the Attacker.
            case "retribution-declare":
                DeclareAdditionalAttackPhase(player, source!);
                return true;
            case "tell-the-tale-honor":
                GainHonor(player.Id, 1, "Tell the Tale", source);
                return false;
            case "strategic-strike-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId, consumedGrant);
            case "sniping-boost":
                _state.PendingAttack!.Strength += _state.PendingAttack.Strength;
                Emit("attackBoosted", $"{Definition(source!).Name}: the Ranged Attack's strength is now {_state.PendingAttack.Strength}.", player.Id, source!.Id);
                return false;
            // Negates the performer's movement from the held action's effects (the "move" kind every movement of his asks about, Tamori Tamoko's).
            case "height-courage-negate":
                NegateEffects(Card(command.PerformerId!), "move");
                return false;
            case "height-courage-boost":
                {
                    var performer = Card(command.PerformerId!);
                    ApplyModifier(performer, source!.Id, id, "Force", 1);
                    foreach (var follower in Attachments(performer.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) ApplyModifier(follower, source.Id, id, "Force", 1);
                    return false;
                }
            case "selfless-defense-move":
                ExecuteMove(command, ability, source, target);
                if (InPlay(target!) && (ControlsPersonalityWithKeyword(player.Id, "Courtier") || ControlsPersonalityWithKeyword(player.Id, "Shugenja")) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "selfless-defense-destroy":
                foreach (var moved in SlotCards(command, "targets").ToArray()) if (InPlay(moved)) MoveHome(moved, false);
                return false;
            case "summoned-justice-move":
                {
                    var moved = ExecuteMove(command, ability, source, target);
                    if (moved && InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    if (InPlay(target!) && target!.Dishonorable) GainHonor(target.OwnerId, -Definition(target).PersonalHonor, Definition(source!).Name, source);
                    return false;
                }
            // The Monk straightens, and an additional Battle action is granted that HE must perform.
            case "spinning-heel-kick-straighten":
                Straighten(Card(command.PerformerId!));
                GrantAdditionalAction(player.Id, null, performerId: command.PerformerId);
                return false;
            case "shameful-injury-negate":
                NegateActionForceModifiers(target!);
                return false;
            case "solid-defense-negate":
            case "solid-defense-negate-favor":
                if (_state.PendingHonorChange is { } heldForLosses) RemC.NegateLossesActionKey = heldForLosses.ActionKey;
                Emit("honorLossesNegated", $"{Definition(source!).Name}: every Honor loss from the action's effects is negated.", player.Id, source!.Id);
                return false;
            case "solid-defense-move":
                ExecuteMove(command, ability, source, target);
                return false;
            // The chosen player takes the first Honor change of another player that the Courtier's Chi covers, from this action's effects.
            case "rhetoric-redirect":
                if (_state.PendingHonorChange is { } heldForRedirect)
                {
                    // It is about THIS Honor change (the window's occurrence), the one "before" which it was played.
                    RemC.RedirectActionKey = heldForRedirect.ActionKey; RemC.RedirectOccurrence = heldForRedirect.Occurrence;
                    RemC.RedirectToPlayer = command.TargetPlayerId!.Value; RemC.RedirectCourtierId = command.PerformerId; RemC.RedirectSpent = false;
                }
                Emit("honorRedirectQueued", $"{Definition(source!).Name}: {Player(command.TargetPlayerId!.Value).Name} will take the action's Honor change instead.", player.Id, source!.Id);
                return false;
            case "rumors-travel-reduce":
                RemC.RumorsTravelOwners.Add(player.Id);
                Emit("rumorsTravel", $"{Definition(source!).Name}: until {player.Name}'s next turn begins, each Honor gain outside a battle or Attack Phase is reduced by 1.", player.Id, source!.Id);
                return false;
            case "rumors-travel-move":
                {
                    var performer = Card(command.PerformerId!);
                    var wasAtBattle = AtBattle(performer);
                    ExecuteMove(command, ability, source, target);
                    if (!wasAtBattle && AtBattle(performer)) Straighten(performer);
                    return false;
                }
            // Sacrifice of Pawns: the Courtier's destruction was the cost. The played Strategy is spent now (the decision that follows finishes the action), then the target's controller chooses.
            case "sacrifice-pawns-destroy":
                {
                    SpendHandPlayedCard(player, source);
                    var opened = OpenEitherOr(EitherOrSpecs.SacrificeOfPawns, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction, performerId: command.PerformerId);
                    // A refusal taken automatically (the target already gone) has begun the search itself.
                    return opened || _state.PendingSearch is not null;
                }
            case "stay-put-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // The enemy card is bowed, then the Weapon (the action's primary target) is attached to the performing Kensai; an entering-play window ends the action here.
            case "stay-put-bow-attach":
            case "stay-put-bow-attach-discount":
                {
                    if (SlotCards(command, "target").FirstOrDefault() is { } bowed && InPlay(bowed) && !StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                    var opened = ExecuteAttach(command, ability.Attach!, player, source, target);
                    if (opened) SpendHandPlayedCard(player, source);
                    return opened;
                }
            case "shadows-walk-take":
                {
                    var performers = PerformingSlotCards(command, ability).Count();
                    if (target!.Zone == Zone.Attached && Definition(target).Type is CardType.Follower or CardType.Item or CardType.Spell) DestroyAttachment(target);
                    else if (Definition(target).Type == CardType.Personality && InPlay(target) && PersonalityChi(target) <= performers)
                    {
                        GrantKeyword(target, "Ninja", id, GrantDuration.Permanent);
                        GrantKeyword(target, "Shadowlands", id, GrantDuration.Permanent);
                        TakeControlOfPersonality(target, player.Id, Definition(source!).Name);
                    }
                    GainHonor(player.Id, -3, Definition(source!).Name, source);
                    return false;
                }
            case "fires-of-war-second-use":
                Card(player.StrongholdId).AbilityUses[SecondUseKey] = _state.TurnNumber;
                Emit("secondUseGranted", $"{Definition(source!).Name}: each ability on {player.Name}'s Stronghold may be used a second time this turn.", player.Id, source!.Id);
                return false;
            // The Empress' Address: the Personality returns to his controller's home, ignoring costs, and its player gains 2 Honor once he is in play.
            case "empress-address-action":
            case "empress-address-battle":
                return BringDeadPersonalityIntoPlay(player, target!, Definition(source!).Name, null, _ => GainHonor(player.Id, 2, Definition(source!).Name, source));
            // Snake Tattoo: the Personality the action targets moves to the chosen adjacent battlefield; if he is then no longer a legal target the action's effects on him are negated.
            case "snake-tattoo-move":
                {
                    var held = HeldAction();
                    var moving = Card(held.TargetId!);
                    MoveByRule(moving, ability.Move!, command.Province);
                    RecheckTarget(held, moving);
                    return false;
                }
            case "superior-opponent-bow":
                {
                    var chi = PersonalityChi(Card(command.PerformerId!));
                    foreach (var member in UnitMembers(target!).Where(m => Definition(m).Type is CardType.Personality or CardType.Follower && CardForce(m) <= chi).ToArray())
                        if (InPlay(member) && !StalwartNegatesBow(member, player.Id)) member.Bowed = true;
                    return false;
                }
            default:
                return null;
        }
    }

    // TargetedActionReactionApplies' fallback (this group's targeted-by-action Reactions).
    private bool EmperorRemainingCTargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var command = subject.Command;
        switch (abilityId)
        {
            // Snake Tattoo: another player's Battle action targets a Tattooed Personality of the reactor's, who stands at the battlefield with an adjacent one to go to.
            case "snake-tattoo-move":
                return command.PlayerId != card.OwnerId && IsBattleAction(command) && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
                    && Keyword(affected, "Tattooed") && affected.Zone == Zone.Battlefield && MoveDestinations(AbilityCatalog.All[abilityId].Move!).Length > 0;
            default:
                return false;
        }
    }

    // ---- control of a Personality in play (Shadows Walk) ------------------------------------------------------------------------------------------------------------

    // "Take control of him": a Personality (and, an attachment always being controlled by its Personality's controller, his unit) changes controller. Loyal (a Personality of another Clan) and
    // Unique/Singular (a card sharing a title with one the new controller already controls) refuse it, as they refuse any way of gaining control; an attachment of the unit the new controller
    // already has a copy of is discarded. The real owner is remembered (CardInstance.OriginalOwnerId) and every card returns to its owner's piles when it leaves play (TakeControl). A Personality at a
    // battlefield is then on the new controller's side (a card's side is its controller's), one at home in the new controller's home. Returns whether control changed.
    // Issue #501: the whole-unit control change is TransferUnitControl (EmperorMechB.cs), shared by every take, give and return; a duplicate Unique/Singular attachment is now discarded (core rules).
    private bool TakeControlOfPersonality(CardInstance personality, int controllerId, string via) =>
        TransferUnitControl(personality, controllerId, via, checkLoyal: true, fromEffect: true);

    // ---- Sacrifice of Pawns: the decision, and the recruit of the Personality the search finds ---------------------------------------------------------------------------

    private string SacrificeOfPawnsPrompt(PendingEitherOr p, string source, string affected) =>
        $"{source}: destroy {affected}, or {Player(p.ActingPlayerId).Name} searches their Dynasty deck for a Personality and brings him into play.";

    private EitherOrOption[] SacrificeOfPawnsOptions(PendingEitherOr p, CardInstance affected)
    {
        var name = Definition(affected).Name;
        return [
            new("destroy", $"Destroy {name}", $"{name} is destroyed. Nothing further happens.", EitherOrKind.Avoid, 0, InPlay(affected), $"{name} is no longer in play."),
            new("refuse", "Do not destroy him", $"{Player(p.ActingPlayerId).Name} searches their Dynasty deck for a Personality and brings him into play, paying two more Gold (he is discarded if it cannot be paid).", EitherOrKind.Accept, 0, true, null)
        ];
    }

    private void ApplySacrificeOfPawns(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance source, CardInstance? affected)
    {
        if (option.Key == "destroy")
        {
            Emit("eitherOrChosen", $"{decider.Name} chose to destroy {Definition(affected!).Name} ({Definition(source).Name}).", decider.Id, affected!.Id);
            DestroyTargetCard(affected, p.ActingPlayerId);
            return;
        }
        Emit("eitherOrChosen", $"{decider.Name} did not destroy {Definition(affected!).Name} ({Definition(source).Name}): {Player(p.ActingPlayerId).Name} searches their Dynasty deck.", decider.Id, source.Id);
        BeginSearch(SearchCatalog.SacrificeOfPawns, source, p.ActingPlayerId, "ability", p.BowedBefore);
    }

    // SearchEffect.RecruitPersonality: the found Personality is brought into play at home paying his whole entry price (QuotePurchase, as a card of the deck) plus 2. One that cannot be brought into play -- a Honor
    // Requirement, a Loyal or Unique restriction, a price nothing pays -- is discarded, as the printed parenthesis says; one that can is paid for now (declining to pay is being unable to play him).
    private void BeginPersonalityRecruit(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        var quote = QuotePurchase(player.Id, card.Id, fromDynastyDeck: true);
        var cost = quote.Cost + 2;
        if (quote.Error is null && cost == 0)
        {
            RecruitSearchedPersonality(search, card);
            return;
        }
        if (quote.Error is not null || PaymentOptions(player.Id, cost, new PaymentSubject(card, PaymentKind.Recruit)).Length == 0)
        {
            DiscardUnplayablePersonality(search, card, quote.Error ?? $"{player.Name} cannot pay {cost} Gold");
            return;
        }
        search.Stage = SearchStage.ChoosePayment;
        _state.PendingChoice = new("searchRecruitPay", search.PlayerId, search.SourceCardId, $"{Definition(Card(search.SourceCardId)).Name}: pay {cost} Gold to bring {Definition(card).Name} into play, or he is discarded.");
    }

    private void DiscardUnplayablePersonality(PendingSearch search, CardInstance card, string why)
    {
        SearchDeckList(Player(search.PlayerId), search.Deck!).Remove(card.Id);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true;
        Emit("searchRecruitFailed", $"{Definition(card).Name} was discarded: {why}.", search.PlayerId, card.Id);
        FinishPersonalityRecruit(search, null);
    }

    private void RecruitSearchedPersonality(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        EnterPlayFromRecruit(player, card, null, Definition(Card(search.SourceCardId)).Name);
        FinishPersonalityRecruit(search, card);
    }

    // The search's own finish (shuffle, end of the search) with the entering-play windows of the Personality that came into play opened before the action ends.
    private void FinishPersonalityRecruit(PendingSearch search, CardInstance? entered)
    {
        var player = Player(search.PlayerId);
        ShuffleDeck(player, search.Deck!);
        _state.PendingSearch = null; _state.PendingChoice = null;
        if (entered is not null && OpenRecruitEntryWindows(player, entered, false, null)) return;
        FinishDeferredAction(search.PlayerId, search.Origin, search.SourceCardId, search.BowedBefore);
    }

    private void ChoosePersonalityRecruitPayment(PendingSearch search, GameCommand command)
    {
        var card = Card(search.SelectedCardId);
        if (command.Choice == "decline") { DiscardUnplayablePersonality(search, card, $"{Player(search.PlayerId).Name} did not pay for him"); return; }
        var quote = QuotePurchase(search.PlayerId, card.Id, fromDynastyDeck: true);
        Pay(search.PlayerId, card, quote.Cost + 2, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Recruit));
        RecruitSearchedPersonality(search, card);
    }

    private IEnumerable<ActionOption> PersonalityRecruitPaymentActions(PendingSearch search, int playerId)
    {
        var card = Card(search.SelectedCardId);
        var priced = QuotePurchase(playerId, card.Id, fromDynastyDeck: true);
        var cost = priced.Cost + 2;
        var paying = new PaymentSubject(card, PaymentKind.Recruit);
        var quote = new GoldQuote(cost, GoldCostKind.Fixed, $"{Definition(card).Name} entry price {priced.Cost} + 2 ({Definition(Card(search.SourceCardId)).Name})", null, PaymentOptions(playerId, cost, paying), Sources: PaymentSourcesFor(playerId, paying));
        yield return new("chooseSearchPayment", $"Pay {cost} Gold: bring {Definition(card).Name} into play", quote.Payments.Length > 0, quote.Payments.Length > 0 ? null : "You cannot pay for him.",
            new GameCommand("chooseSearchPayment", playerId, _state.Version, SourceIds: quote.Payments.FirstOrDefault()?.SourceIds), card.Id, Gold: quote);
        yield return new("chooseSearchPayment", $"Do not pay: {Definition(card).Name} is discarded", true, null, new GameCommand("chooseSearchPayment", playerId, _state.Version, Choice: "decline"), card.Id);
    }

    // ---- The Fires of War: a second use of every Stronghold ability ---------------------------------------------------------------------------------------------------

    /// <summary>The mark on a Stronghold (its AbilityUses) that lets each of its abilities be used one more time on the turn it names.</summary>
    internal const string SecondUseKey = "fires-of-war:second-use";

    // ---- Rocky Terrain ------------------------------------------------------------------------------------------------------------------------------------------------

    // "Attacking Personalities at the current battlefield": the Attacker's Personalities there; a bowed one is not offered (bowing it again would do nothing), as Heart of Darkness offers none.
    private IEnumerable<CardInstance> RockyTerrainCandidates(int provinceIndex) =>
        _state.Cards.Values.Where(c => !c.Bowed && Definition(c).Type == CardType.Personality && c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && c.OwnerId == _state.ActivePlayerId)
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- The Empress' Address ---------------------------------------------------------------------------------------------------------------------------------------------

    // AfterActionResolvedApplies' fallback: the action destroyed a Personality of the reactor's that stood in a DEFENDING army (at a battlefield, on the side that is not the Attacker's).
    private bool EmperorRemainingCAfterActionApplies(string abilityId, ResolvedActionFacts facts, int playerId) => abilityId switch
    {
        "empress-address-action" => playerId != _state.ActivePlayerId && facts.Affected.Any(a => a.Kind == DestroyedKind && a.FromBattlefield is not null && Card(a.CardId) is { } destroyed
            && destroyed.OwnerId == playerId && Definition(destroyed).Type == CardType.Personality),
        _ => false
    };

    // A battle's resolution destroyed one of the reactor's Personalities in the defending army: the reactor is the Defender and the Attacker's army destroyed some of its units (every unit is led by a Personality).
    private bool EmpressAddressBattleApplies(PendingAfterResolution facts, int playerId) => facts.Attacker != playerId && facts.DestroyedEnemyUnits[facts.Attacker] > 0;

    private static bool IsEmperorRemainingCOutOfPlayTarget(string category) => category == "remc-own-clan-nonunique-discarded-personality";

    private bool EmperorRemainingCOutOfPlayTargetAllows(string category, PlayerState player, CardInstance target) =>
        category == "remc-own-clan-nonunique-discarded-personality" && Definition(target).Type == CardType.Personality && target.OwnerId == player.Id && IsDiscardedPersonality(target)
            && !Keyword(target, "Unique") && HasClanAffiliation(target, player.Clan);

    private IEnumerable<CardInstance> EmperorRemainingCOutOfPlayCandidates(string category, int playerId) =>
        _state.Cards.Values.Where(c => EmperorRemainingCOutOfPlayTargetAllows(category, Player(playerId), c)).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Superior Opponent's Focus Effect ---------------------------------------------------------------------------------------------------------------------------

    // The attachments "at the current battlefield" (on a unit standing there, either player's), when the focusing player's Personality in the duel is there.
    private List<string> SuperiorOpponentCandidates(int playerId)
    {
        if (_state.PendingDuel is not { } duel || _state.CurrentBattleProvince is not int province) return [];
        var mine = new[] { duel.ChallengerId, duel.ChallengedId }.Select(Card).FirstOrDefault(c => c.OwnerId == playerId);
        if (mine is not { Zone: Zone.Battlefield } || mine.ProvinceIndex != province) return [];
        return _state.Cards.Values.Where(a => a.Zone == Zone.Attached && a.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == province)
            .Select(a => a.Id).Order(StringComparer.Ordinal).ToList();
    }

    // ---- The additional action a named Personality must perform (Spinning Heel Kick) ------------------------------------------------------------------------------------

    // ConsumeAdditionalActionGrant's condition: a grant with a PerformerId is spent only by an action that Personality performs (a printed action is performed by the card that prints it, an
    // Item's or Spell's by its wielder, a named "performing X" by X; a Strategy that names no performer is performed by no Personality at all).
    private bool PerformerGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        (grant.PerformerId is null || command is not null && ActionPerformingPersonality(command) is { } performer && performer.Id == grant.PerformerId) && BaseAbilityGrantMatches(grant, command) && UnsafePassageGrantMatches(grant, command) // issue #496: Honor Never Falls' base abilities only; issue #622: Unsafe Passage's Follower
        && MechAGrantMatches(grant, command) && AbilityScopeGrantMatches(grant, command); // issue #721 (AdditionalActionScope.cs): a grant scoped to named abilities. issue #501 (EmperorMechA.cs): Inokuchi's Iaijutsu, Beyond the Line's Recon ability as a Battle action

    // ---- Retribution: an additional Attack Phase ----------------------------------------------------------------------------------------------------------------------

    // The Reaction played in the window after an Attack Phase ended (Retribution; The Perfect Moment, issue #492 rulings pass R1): its player -- the Defender of the phase that ended -- DECLARES an additional
    // Attack Phase and so is its Attacker (Mendon: "the player who declared the attack phase is the attacker"); the card is spent, the window closes and the Declaration Segment's battlefields exist at once.
    private void DeclareAdditionalAttackPhase(PlayerState player, CardInstance source)
    {
        SpendHandPlayedCard(player, source);
        PromoNoteAdditionalAttackPhase(source); // Hofukushu's Vengeance (issue #653): its phase has no +1F after he assigns
        _state.SuspendedWindows.RemoveAll(w => w.Choice.Type == "attackPhaseEndReaction");
        _state.PendingChoice = null;
        if (RemC.RetributionRoot < 0) RemC.RetributionRoot = _state.ActivePlayerId;
        RemC.RetributionAssigned = 0;
        _state.ActivePlayerId = player.Id;
        _state.UnresolvedBattlefields = Player(1 - player.Id).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Order().ToList();
        Emit("attackDeclared", $"{player.Name} declared an additional Attack Phase ({Definition(source).Name}): a battlefield exists at each of {Player(1 - player.Id).Name}'s provinces.", player.Id, source.Id);
        Wave835AttackDeclared(); // issue #835: Imperial Deployment
        SetPhase(Phase.InfantryDeclaration, player.Id);
    }

    // The last battle of an Attack Phase is done (CompleteBattleTransition): its Defender may declare an additional Attack Phase, in a window of his own; otherwise (or once he has declined) the phase
    // ends and the Dynasty Phase of the turn's player begins.
    private void EndAttackPhase(int attacker)
    {
        if (OpenAttackPhaseEndWindow(attacker)) return;
        FinishAttackPhaseEnd();
    }

    // The Attack Phase (or the last of its additional ones) is over: the turn's own player is the active player again for the Dynasty Phase.
    private void FinishAttackPhaseEnd()
    {
        if (FinishAramasuAttackPhase()) return; // Aramasu's Legacy (issue #501, EmperorMechA.cs): its Attack Phase ends inside the Action Phase, which resumes
        var turnPlayer = RemC.RetributionRoot >= 0 ? RemC.RetributionRoot : _state.ActivePlayerId;
        RemC.RetributionRoot = -1; RemC.RetributionAssigned = 0;
        R1.PerfectMoment = null; // its restrictions belong to that one Attack Phase (issue #492, rulings pass R1)
        _state.ActivePlayerId = turnPlayer;
        BeginPhaseAfterAttack(turnPlayer); // issue #667: the Dynasty Phase in the default order, otherwise the turn ends
    }

    // The window opens only when the Defender holds a Reaction that names it and could play it now (the same "is anything playable" test every after-window uses).
    private bool OpenAttackPhaseEndWindow(int attacker)
    {
        var defender = 1 - attacker;
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("attackPhaseEndReaction", defender, null, "After this Attack Phase ended: use a Reaction, or pass.");
        bool holds;
        try { holds = AbilityActions(defender).Any(o => o.Enabled); }
        finally { _state.PendingChoice = saved; }
        if (!holds) return false;
        _state.PendingChoice = new("attackPhaseEndReaction", defender, null, "After this Attack Phase ended: you may use a Reaction, or pass.", attacker);
        _state.PriorityPlayerId = defender; _state.ConsecutivePasses = 0;
        Emit("reactionWindow", "After-Attack-Phase window opened.", defender);
        return true;
    }

    // Apply's handler for the window: a Reaction is played, or the Defender passes and the phase ends.
    private void ResolveAttackPhaseEndCommand(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction to the end of the Attack Phase or pass.");
        if (command.Type == "useAbility") { UseAbility(command); return; }
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        _state.PendingChoice = null;
        FinishAttackPhaseEnd();
    }

    // "You may only assign one Personality" (AssignAttackers, before the assignment) and "Give the Personality and each Follower in his unit +1F after he assigns" (after).
    private void RequireRetributionAssignment(GameCommand command)
    {
        if (RetributionRestricts() && command.Assignments is { Count: > 0 } assignments)
            Require(RemC.RetributionAssigned + assignments.Count <= 1, "In an additional Attack Phase you may only assign one Personality.");
    }

    private void NoteRetributionAssignments(GameCommand command)
    {
        if (!RetributionRestricts()) return;
        foreach (var id in (command.Assignments ?? []).Keys)
        {
            RemC.RetributionAssigned++;
            if (PromoEmperorNoRetributionBonus()) continue; // Hofukushu's Vengeance (issue #653)
            var assigned = Card(id);
            ApplyModifier(assigned, id, "retribution-declare", "Force", 1);
            foreach (var follower in Attachments(assigned.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) ApplyModifier(follower, id, "retribution-declare", "Force", 1);
        }
    }

    // ---- Honor -------------------------------------------------------------------------------------------------------------------------------------------------------

    // GainHonor's first hook (issue #492): Rhetoric's redirect (the player the change lands on), then Solid Defense's negation of the action's losses, then Rumors Travel's reduction of a gain.
    // Returns the amount to apply and the player who takes it; an amount of 0 stops the change. Honor paid as a COST is not an effect of the action (_payingActionCost).
    private (int PlayerId, int Amount) EmperorRemainingCHonorChange(int playerId, int amount, string reason)
    {
        if (amount == 0) return (playerId, 0);
        if (!_payingActionCost && CurrentActionKey() is { } key)
        {
            if (RemC.RedirectActionKey == key && !RemC.RedirectSpent && (RemC.RedirectOccurrence < 0 || RemC.RedirectOccurrence == _honorChangeOccurrence - 1) && playerId != RemC.RedirectToPlayer
                && RemC.RedirectCourtierId is { } courtierId && _state.Cards.TryGetValue(courtierId, out var courtier) && InPlay(courtier) && PersonalityChi(courtier) >= Math.Abs(amount))
            {
                RemC.RedirectSpent = true;
                Emit("honorRedirected", $"{Player(RemC.RedirectToPlayer).Name} takes the Honor {(amount > 0 ? "gain" : "loss")} of {Math.Abs(amount)} from {reason} instead of {Player(playerId).Name} (Rhetoric).", RemC.RedirectToPlayer);
                playerId = RemC.RedirectToPlayer;
            }
            if (amount < 0 && RemC.NegateLossesActionKey == key && !CurrentActionUnnegatable()) // issue #501: The Heir's Wrath's loss "will not be negated"
            {
                Emit("honorLossNegated", $"{Player(playerId).Name}'s Honor loss from {reason} was negated (Solid Defense).", playerId);
                return (playerId, 0);
            }
        }
        if (amount > 0 && RulesPhase(_state.Phase) != 3)
        {
            var reduction = RemC.RumorsTravelOwners.Count;
            if (reduction > 0)
            {
                var reduced = Math.Max(0, amount - reduction);
                Emit("honorGainReduced", $"{Player(playerId).Name}'s Honor gain from {reason} was reduced by {amount - reduced} (Rumors Travel).", playerId);
                amount = reduced;
            }
        }
        return (playerId, amount);
    }

    // The Honor-loss cost of an action is paid outside its effects.
    private bool _payingActionCost;

    // StartTurn's hook: "until your next turn begins".
    private void ExpireEmperorRemainingCAtTurnStart(PlayerState player) => RemC.RumorsTravelOwners.RemoveAll(o => o == player.Id);
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredAttackPhaseEndReaction(string type, GameCommand command)
    {
        if (type != "attackPhaseEndReaction") return false;
        ResolveAttackPhaseEndCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingCHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingC", 600, static abilities => abilities.AddEmperorRemainingC());
        EngineHooks.TargetAllows.Register("EmperorRemainingCTargetAllows", 200, static (engine, context) => engine.EmperorRemainingCTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.TargetedReaction.Register("EmperorRemainingCTargetedReactionApplies", 200, static (engine, context) => engine.EmperorRemainingCTargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorRemainingC", 400, static (engine, context) => engine.ValidateEmperorRemainingC(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorRemainingC", 200, static (engine, context) => engine.RunEmperorRemainingC(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.bowedBeforeAction));
        EngineHooks.PendingChoice.Register("HandleRegisteredAttackPhaseEndReaction", 5500, static (engine, type, command) => engine.HandleRegisteredAttackPhaseEndReaction(type, command));
    }
}
