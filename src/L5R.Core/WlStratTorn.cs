using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// STRATEGY WAVE 118 (issue #674, cluster "strat-torn"): nineteen Torn Asunder Strategies the #668 assessment marked WIRE, each read from its own scan. Every printed line is built on an existing
// mechanism (slots, performers, costs, delayed effects, Either/Or and decider decisions, the removal primitive); the one-line hooks this file asks of shared engine files are listed in
// docs/decisions/2026-10-06-674-strat-torn.md.
//
//   * A THREAT ENACTED (10360)  Political Limited: performing unbowed non-Duelist Courtier + a Personality with equal or lower Personal Honor + discard the Imperial Favor: destroy a non-Unique
//     card without attachments in his unit, then dishonor him (if he is still in play). The card is chosen with the command as the action resolves (the engine lists the choices: Makito's shape).
//   * AMBUSH TACTICS (10361)    Battle: bow the performing Personality: Ranged 3 Attack, +2 strength if the performer is a Scout; "you may move him home" is the second of two actions sharing a use
//     (The Otoro Estate's shape), taken after the Attack (AfterWlStratTornAttack).
//   * BAMBOO TATTOO (10363) / CALLING THE DARKNESS (10365): permanent Stalwart; three +1F tokens, permanent Shadowlands, a straightening owed before the turn ends, and 2 Honor lost.
//   * COURTEOUS GIFT (10367)    Bow a performing Samurai, a face-up province card: the other player may draw; the card costs 2 less this turn (4 less if he drew). Either/Or for the draw.
//   * EYE OF THE SWORD (10371)  Iaijutsu Battle duel: the winner gets two +1F tokens, the loser is destroyed (Dueling.cs ApplyDuelOutcome's case).
//   * FOREST COVER (10372)      Recon Reaction after engaging: Reconnaissance there; a second action (same use) also attaches a Follower from hand or discard pile, paying all costs, and then
//     queues an additional End Phase draw.
//   * FORGOTTEN TEACHINGS (10373) the removal primitive's all-key for the turn (restored as the turn ends: ResolveWlStratTornTurnEnd).
//   * IMPROPER PAPERS (10379)   "Before an action resolves, target a Personality: negate his movement" -- The Height of Courage's window, with a target instead of a performer (a Strategy in hand).
//   * IMPRUDENT MISSTEP (10380), IN THE RIGHT (10381), MARTYR'S CALL (10383), TACTICAL SACRIFICE (10397), THE LAST MOVE (10398), TOUCH OF THE NIGHT (10400): slots, costs and modifiers.
//   * OVERPOWER (10387), THE PASSING OF TRADITION (10399): no Attack Phase on a first or second turn (the player's attack lock); a discarded Personality put into a province (Scavenging Party's
//     refill); permanently copying a keyword (an Either/Or choice).
//   * SCANDALOUS GOSSIP (10391) Open from the discard pile: the chosen player must choose and bow one of his unbowed Personalities (a decider choice), then gain 1 Honor.
//   * SPOILS OF EXPLORATION (10394) a Kata: Limited puts it into play; in play it draws a card after a resolution in which its controller destroyed an army or a province (a trait, like Shiba's Guidance).
// ======================================================================================================================================================================================

/// <summary>What the cluster keeps between commands: the Personalities whose traits and abilities Forgotten Teachings removed for this turn.</summary>
public sealed class WlStratTornState
{
    /// <summary>Card ids owed their traits and abilities back as the turn ends.</summary>
    public List<string> ForgottenTeachings { get; set; } = [];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(20700)]
    public WlStratTornState WlStratTorn { get; set; } = new();
}

public static class WlStratTornAbilities
{
    public const string ThreatEnacted = "wl-threat-enacted", AmbushAttack = "wl-ambush-attack", AmbushAttackMove = "wl-ambush-attack-move", BambooStalwart = "wl-bamboo-stalwart",
        CallingDarkness = "wl-calling-darkness", CourteousGift = "wl-courteous-gift", EyeDuel = "wl-eye-sword-duel", ForestCoverRecon = "wl-forest-cover-recon", ForestCoverAttach = "wl-forest-cover-attach",
        ForgottenTeachings = "wl-forgotten-teachings", ImproperPapers = "wl-improper-papers", ImprudentMisstep = "wl-imprudent-misstep", InTheRight = "wl-in-the-right", MartyrsCall = "wl-martyrs-call",
        OverpowerOpen = "wl-overpower-open", OverpowerLimited = "wl-overpower-limited", ScandalousGossip = "wl-scandalous-gossip", SpoilsEnter = "wl-spoils-enter",
        TacticalSacrifice = "wl-tactical-sacrifice", LastMove = "wl-last-move", PassingCopy = "wl-passing-copy", PassingLimited = "wl-passing-limited", TouchOfTheNight = "wl-touch-night";

    /// <summary>Spoils of Exploration's trait (the card's Effect): read where a battle's resolution ends.</summary>
    public const string SpoilsEffect = "wl-spoils-draw";

    // Performer and target categories (each judged by the hooks below; a slot's category is judged the same way).
    public const string NonDuelistCourtier = "wl-own-unbowed-non-duelist-courtier";
    public const string HonorAtMostPerformer = "wl-personality-honor-at-most-performer";
    public const string UnitCardInTargetUnit = "wl-card-in-target-unit-without-attachments";
    public const string AttackingCard = "wl-attacking-card";
    public const string OwnOtherPersonality = "wl-own-other-personality";
    public const string PerformerFollower = "wl-performer-follower";
    public const string SacrificeVictim = "wl-sacrifice-victim";
    public const string EnemySensei = "wl-enemy-sensei";

    public static Dictionary<string, AbilityDefinition> AddWlStratTorn(this Dictionary<string, AbilityDefinition> all)
    {
        // A Threat Enacted (oracle-l5r-10360): "Limited: Choose your performing unbowed non-Duelist Courtier Personality, target a Personality with equal or lower Personal Honor, and discard the
        // Imperial Favor: Destroy a non-Unique card in his unit without attachments. Dishonor him (if he is still in play)."
        all[ThreatEnacted] = new("Limited", "Political Limited: Choose your performing unbowed non-Duelist Courtier Personality, target a Personality with equal or lower Personal Honor, and discard the Imperial Favor: Destroy a non-Unique card in his unit without attachments. Dishonor him (if he is still in play)",
            HonorAtMostPerformer, Performer: NonDuelistCourtier, ActionKeywords: ["Political"], Costs: [new(CostVerbs.DiscardFavor)]);
        // Ambush Tactics (oracle-l5r-10361): "Battle: Bow your performing Personality: Ranged 3 Attack with +2 strength if your performer is a Scout. You may move him home."
        all[AmbushAttack] = new("Battle", "Bow your performing Personality: Ranged 3 Attack with +2 strength if your performer is a Scout", "attack",
            Performer: "own-unbowed-personality-at-battle", Costs: ActionCost.BowPerformer, UsageKey: "wl-ambush");
        all[AmbushAttackMove] = new("Battle", "Bow your performing Personality: Ranged 3 Attack with +2 strength if your performer is a Scout. Move him home", "attack",
            Performer: "own-unbowed-personality-at-battle", Costs: ActionCost.BowPerformer, UsageKey: "wl-ambush");
        // Bamboo Tattoo (oracle-l5r-10363): "Limited: Choose your performing unbowed Tattooed Personality: Permanently give him Stalwart."
        all[BambooStalwart] = new("Limited", "Choose your performing unbowed Tattooed Personality: Permanently give him Stalwart", "none",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Tattooed"]);
        // Calling the Darkness (oracle-l5r-10365): "Limited: Bow your performing Personality: Give him three +1F tokens and permanently give him Shadowlands. Straighten him before the turn ends. Lose 2 Honor."
        all[CallingDarkness] = new("Limited", "Bow your performing Personality: Give him three +1F tokens and permanently give him Shadowlands. Straighten him before the turn ends. Lose 2 Honor", "none",
            Performer: "own-unbowed-personality", Costs: ActionCost.BowPerformer);
        // Courteous Gift (oracle-l5r-10367): "Limited: Bow your performing Samurai, target a face-up card in a province, and choose another player: He may draw a card. Reduce the target's Gold Cost
        // (this turn) by 2, or 4 if the player drew a card." The game has two players, so "another player" is the opponent.
        all[CourteousGift] = new("Limited", "Bow your performing Samurai, target a face-up card in a province, and choose another player: He may draw a card. Reduce the target's Gold Cost (this turn) by 2, or 4 if the player drew a card", "slots",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Samurai"], Costs: ActionCost.BowPerformer,
            Slots: [new("gift", "card-in-province", "A face-up card in a province")]);
        // Eye of the Sword (oracle-l5r-10371): "Battle: Choose your performing unbowed Duelist and target an enemy Personality: Your Personality duels the target. Give the winner two +1F tokens. Destroy the duel's loser."
        all[EyeDuel] = new("Battle", "Iaijutsu Battle: Choose your performing unbowed Duelist and target an enemy Personality: Your Personality duels the target. Give the winner two +1F tokens. Destroy the duel's loser", "enemy-personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Duelist"], IssuesDuel: true, ActionKeywords: ["Iaijutsu"]);
        // Forest Cover (oracle-l5r-10372): "Reaction: After engaging, choose your performing Scout Personality at the current battlefield: You have Reconnaissance there. You may attach a Follower to him
        // from your hand or discard pile, paying all costs. If you did, draw an additional card during your next End Phase." -- the "may" is two actions sharing one use.
        all[ForestCoverRecon] = new("Reaction", "Recon Reaction: After engaging, choose your performing Scout Personality at the current battlefield: You have Reconnaissance there", "none",
            Trigger: "after-engaging", Performer: "own-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Scout"], UsageKey: "wl-forest-cover", ActionKeywords: ["Recon"]);
        all[ForestCoverAttach] = new("Reaction", "Recon Reaction: After engaging, choose your performing Scout Personality at the current battlefield and target a Follower in your hand or discard pile: You have Reconnaissance there. Attach the Follower to him, paying all costs. Draw an additional card during your next End Phase",
            "attachment-in-discard", Trigger: "after-engaging", Performer: "own-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Scout"], UsageKey: "wl-forest-cover", ActionKeywords: ["Recon"],
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.OwnHandOrDiscard, AttachWielder.Performer, [CardType.Follower]));
        // Forgotten Teachings (oracle-l5r-10373): "Open: Target another player's Sensei Personality: Remove all traits and abilities from him (this turn)."
        all[ForgottenTeachings] = new("Open", "Target another player's Sensei Personality: Remove all traits and abilities from him (this turn)", EnemySensei);
        // Improper Papers (oracle-l5r-10379): "Reaction: Before an action resolves, target a Personality: Negate his movement from the action's effects."
        all[ImproperPapers] = new("Reaction", "Before an action resolves, target a Personality: Negate his movement from the action's effects", "personality", Trigger: "before-action-resolves");
        // Imprudent Misstep (oracle-l5r-10380): "Limited: Target another player with 30 or more Family Honor: He commits a public breach of etiquette. He loses 5 Honor."
        all[ImprudentMisstep] = new("Limited", "Political Limited: Target another player with 30 or more Family Honor: He commits a public breach of etiquette. He loses 5 Honor", "player");
        // In the Right (oracle-l5r-10381): "Battle: Bow your performing Courtier at any location and target one or more attacking cards, up to half your Courtier's Chi, rounded down: Bow the targets."
        all[InTheRight] = new("Battle", "Political Battle: Bow your performing Courtier at any location and target one or more attacking cards, up to half your Courtier's Chi, rounded down: Bow the targets", "slots",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer,
            Slots: [new("targets", AttackingCard, "One or more attacking cards, up to half your Courtier's Chi (rounded down)", 1, 24)]);
        // Martyr's Call (oracle-l5r-10383): "Battle/Open: Destroy your performing Paragon and target one or two of your other Personalities: Give them +3F and Stalwart."
        all[MartyrsCall] = new("BattleOrOpen", "Destroy your performing Paragon and target one or two of your other Personalities: Give them +3F and Stalwart", "slots",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)],
            Slots: [new("targets", OwnOtherPersonality, "One or two of your other Personalities", 1, 2)]);
        // Overpower (oracle-l5r-10387): "Open: If it is the active player's first or second turn: No Attack Phase may be declared." / "Limited: Target your discarded (not dead) Personality: Put him into one
        // of your provinces, discarding any card currently there."
        all[OverpowerOpen] = new("Open", "If it is the active player's first or second turn: No Attack Phase may be declared", "none");
        all[OverpowerLimited] = new("Limited", "Target your discarded (not dead) Personality: Put him into one of your provinces, discarding any card currently there", "own-discarded-personality", ChoosesOwnProvince: true);
        // Scandalous Gossip (oracle-l5r-10391): "Open: If (and only if) this card is in your discard pile, remove it from the game and target a player: He must choose and bow one of his unbowed
        // Personalities, if possible. Gain 1 Honor." Removal from the game is the engine's own for a card played from a discard pile.
        all[ScandalousGossip] = new("Open", "Political Open: If (and only if) this card is in your discard pile, remove it from the game and target a player: He must choose and bow one of his unbowed Personalities, if possible. Gain 1 Honor", "player", FromDiscard: true);
        // Spoils of Exploration (oracle-l5r-10394): "Limited: Put this card into play. Discard all your other Kata in play." (The draw is its trait: SpoilsEffect.)
        all[SpoilsEnter] = new("Limited", "Put this card into play. Discard all your other Kata in play", "none");
        // Tactical Sacrifice (oracle-l5r-10397): "Limited: Choose your performing unbowed Commander, destroy his performing Follower, and target a Follower, or Personality without attachments, with
        // Gold Cost equal to or less than twice your Follower's Gold Cost: Destroy the target."
        all[TacticalSacrifice] = new("Limited", "Choose your performing unbowed Commander, destroy his performing Follower, and target a Follower, or Personality without attachments, with Gold Cost equal to or less than twice your Follower's Gold Cost: Destroy the target", "slots",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Commander"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Slot("follower"))],
            Slots: [new("follower", PerformerFollower, "His performing Follower, to destroy (a cost)") { CostOnly = true }, new("target", SacrificeVictim, "A Follower, or Personality without attachments, with Gold Cost at most twice the Follower's")]);
        // The Last Move (oracle-l5r-10398): "Battle: Destroy your performing unbowed Courtier and target an enemy Personality: Dishonor him. You may target and destroy a card without attachments in his unit."
        all[LastMove] = new("Battle", "Political Battle: Destroy your performing unbowed Courtier and target an enemy Personality: Dishonor him. You may target and destroy a card without attachments in his unit", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)],
            Slots: [new("target", "enemy-personality", "An enemy Personality to dishonor"), new("destroy", UnitCardInTargetUnit, "A card without attachments in his unit, to destroy (optional)", 0, 1)]);
        // The Passing of Tradition (oracle-l5r-10399): "Battle/Open: Choose your performing Personality and target your Personality: The performer permanently copies a keyword from the target, other
        // than Courtier, Samurai, or Shugenja." / "Limited: Target your discarded Personality: Put him into one of your provinces (face-up) discarding the card currently there."
        all[PassingCopy] = new("BattleOrOpen", "Choose your performing Personality and target your Personality: The performer permanently copies a keyword from the target, other than Courtier, Samurai, or Shugenja", "own-personality",
            Performer: "own-personality-at-battle");
        all[PassingLimited] = new("Limited", "Target your discarded Personality: Put him into one of your provinces (face-up) discarding the card currently there", "own-discarded-personality", ChoosesOwnProvince: true);
        // Touch of the Night (oracle-l5r-10400): "Battle: Choose your performing unbowed Ninja Personality: Give each Personality and Follower opposing him -2F."
        all[TouchOfTheNight] = new("Battle", "Choose your performing unbowed Ninja Personality: Give each Personality and Follower opposing him -2F", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Performers --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool WlPerformerAllows(AbilityDefinition ability, CardInstance performer) =>
        ability.Performer == WlStratTornAbilities.NonDuelistCourtier && Definition(performer).Type == CardType.Personality && !performer.Bowed && Keyword(performer, "Courtier") && !Keyword(performer, "Duelist");

    private string?[]? WlPerformers(AbilityDefinition ability, int playerId) => ability.Performer == WlStratTornAbilities.NonDuelistCourtier
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && WlPerformerAllows(ability, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray() : null;

    // ---- Targets (a slot's category is judged the same way) -------------------------------------------------------------------------------------------------------------------------------

    private bool WlTargetAllows(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        switch (ability.Target)
        {
            case WlStratTornAbilities.HonorAtMostPerformer:
                return personality && InPlay(target) && command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer) && CardPersonalHonor(target) <= CardPersonalHonor(performer);
            case WlStratTornAbilities.UnitCardInTargetUnit:
                return IsUnitCardWithoutAttachments(target) && SlotCards(command, "target").FirstOrDefault() is { } enemy && UnitHostId(target) == UnitHostId(enemy);
            case WlStratTornAbilities.AttackingCard:
                // "Attacking cards": the Personalities of the attacking army and the cards attached to them.
                return InPlay(target) && (personality || target.Zone == Zone.Attached && Definition(target).Type is CardType.Follower or CardType.Item or CardType.Spell)
                    && Card(UnitHostId(target)) is { } host && Definition(host).Type == CardType.Personality && IsAttacking(host) && AtBattle(host);
            case WlStratTornAbilities.OwnOtherPersonality:
                return personality && InPlay(target) && target.OwnerId == player.Id && target.Id != command.PerformerId;
            case WlStratTornAbilities.PerformerFollower:
                return Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached && target.OwnerId == player.Id && target.AttachedTo is { } hostId && hostId == command.PerformerId;
            case WlStratTornAbilities.SacrificeVictim:
                {
                    if (!InPlay(target) || SlotCards(command, "follower").FirstOrDefault() is not { } follower || target.Id == follower.Id) return false;
                    var ok = Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached || personality && !HasAttachments(target);
                    return ok && CardGoldCost(target) <= 2 * CardGoldCost(follower);
                }
            case WlStratTornAbilities.EnemySensei:
                return personality && InPlay(target) && target.OwnerId != player.Id && Keyword(target, "Sensei");
            default:
                return false;
        }
    }

    // ---- Validation (the whole command) ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlStratTorn(string id, GameCommand command, PlayerState player)
    {
        switch (id)
        {
            case WlStratTornAbilities.CourteousGift:
                Require(SlotCards(command, "gift").All(c => c.Zone == Zone.Province && c.FaceUp), "Choose a face-up card in a province.");
                break;
            case WlStratTornAbilities.ImprudentMisstep:
                Require(command.TargetPlayerId is int other && other != player.Id && Player(other).Honor >= 30, "Target another player with 30 or more Family Honor.");
                break;
            case WlStratTornAbilities.InTheRight:
                {
                    var courtier = Card(command.PerformerId!);
                    var limit = PersonalityChi(courtier) / 2;
                    Require(limit >= 1, $"{Definition(courtier).Name}'s Chi is too low to target any card.");
                    Require(SlotCards(command, "targets").Count() <= limit, $"Target at most {limit} attacking card{(limit == 1 ? "" : "s")}: half {Definition(courtier).Name}'s Chi, rounded down.");
                    break;
                }
            case WlStratTornAbilities.SpoilsEnter:
                Require(command.CardId is { } kataId && _state.Cards.TryGetValue(kataId, out var kata) && kata.Zone == Zone.Hand && player.Hand.Contains(kata.Id), "A Kata is put into play from your hand.");
                break;
            case WlStratTornAbilities.OverpowerOpen:
                Require(Player(_state.ActivePlayerId).TurnsTaken <= 2, "It is not the active player's first or second turn.");
                break;
            case WlStratTornAbilities.OverpowerLimited:
                Require(command.TargetId is { } discarded && _state.Cards.TryGetValue(discarded, out var card) && card.Zone != Zone.Dead, "Target one of your discarded (not dead) Personalities.");
                break;
        }
    }

    // ---- Attacks --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Ranged 3 Attack with +2 strength if your performer is a Scout": the performing Personality is the Attack's source.
    private int? WlAttackStrength(string abilityId, string? sourceId) =>
        abilityId is WlStratTornAbilities.AmbushAttack or WlStratTornAbilities.AmbushAttackMove ? 3 + (sourceId is not null && _state.Cards.TryGetValue(sourceId, out var performer) && Keyword(performer, "Scout") ? 2 : 0) : null;

    private static string? WlAttackKind(string abilityId) => abilityId is WlStratTornAbilities.AmbushAttack or WlStratTornAbilities.AmbushAttackMove ? "Ranged" : null;

    // ResolveAttackNow's hook: "You may move him home" (the second action), whether or not the Attack hit.
    private void AfterWlStratTornAttack(string abilityId, string? sourceId)
    {
        if (abilityId != WlStratTornAbilities.AmbushAttackMove || sourceId is null || !_state.Cards.TryGetValue(sourceId, out var performer) || !InPlay(performer) || performer.Zone != Zone.Battlefield) return;
        MoveHome(performer, false);
    }

    // The abilities that destroy / bow their target, for the protections that ask (Rokai, Brothers of Goemon).
    private static bool WlDestroysTarget(string abilityId) => abilityId == WlStratTornAbilities.TacticalSacrifice;
    private static bool WlBowsTarget(string abilityId) => abilityId == WlStratTornAbilities.InTheRight;

    // ---- Improper Papers: a Strategy in hand, answering an action that would move a Personality ------------------------------------------------------------------------------------------

    // BeforeActionReactionApplies' arm. Null: not this feature's Reaction.
    private bool? WlBeforeActionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        if (abilityId != WlStratTornAbilities.ImproperPapers) return null;
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        // The window is offered when the action would move a Personality (nothing to negate otherwise); the target is any Personality, as printed (Request Authorization's listing).
        return !MovementCannotBeNegated(command) && DeferredActionMovers(command).Any();
    }

    // ---- Turn end and battle resolution ------------------------------------------------------------------------------------------------------------------------------------------------------

    // EndTurn's hook: Forgotten Teachings' "(this turn)" ends.
    private void ResolveWlStratTornTurnEnd()
    {
        if (_state.WlStratTorn.ForgottenTeachings.Count == 0) return;
        foreach (var id in _state.WlStratTorn.ForgottenTeachings.ToArray())
        {
            _state.WlStratTorn.ForgottenTeachings.Remove(id);
            if (_state.Cards.TryGetValue(id, out var card)) RestoreRemovedAbilities(card, AbilityRemovalAbilities.AllKey);
        }
    }

    // Gives back what a key removed, leaving any other removal in force.
    private void RestoreRemovedAbilities(CardInstance card, string key)
    {
        if (Removal.Entries.RemoveAll(e => e.CardId == card.Id && e.Key == key) == 0) return;
        if (Removal.Entries.Any(e => e.CardId == card.Id)) { ApplyRemovals(card); return; }
        if (Removal.Baselines.FirstOrDefault(b => b.CardId == card.Id) is not { } baseline) return;
        Removal.Baselines.Remove(baseline);
        if (card.Printed is { } printed)
        {
            var restored = printed with { Abilities = baseline.Abilities, Effect = baseline.Effect };
            card.Printed = restored == CardCatalog.Get(card.DefinitionId) ? null : restored;
        }
        Emit("abilitiesRestored", $"{Definition(card).Name} has his traits and abilities again.", card.OwnerId, card.Id);
    }

    // The battle's resolution hook: "After a battle resolution ends in which you destroyed an army or province: Draw a card." -- a trait of Spoils of Exploration while it is in play.
    private void ApplyWlStratTornResolutionTraits(int[] destroyedEnemyUnits, int? provinceDestroyedBy)
    {
        foreach (var spoils in _state.Cards.Values.Where(c => c.Zone == Zone.Home && Definition(c).Effect == WlStratTornAbilities.SpoilsEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var owner = Player(spoils.OwnerId);
            if (destroyedEnemyUnits[owner.Id] <= 0 && provinceDestroyedBy != owner.Id) continue;
            Emit("spoilsTrait", $"{owner.Name} destroyed an army or a province in the resolution: {Definition(spoils).Name} draws a card.", owner.Id, spoils.Id);
            Draw(owner);
        }
    }

    internal void WlTurnEndForTest() => ResolveWlStratTornTurnEnd();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlStratTornHooks()
    {
        EngineHooks.Abilities.Register("AddWlStratTorn", 14700, static abilities => abilities.AddWlStratTorn());
        EngineHooks.PerformerAllows.Register("WlStratTornPerformerAllows", 14700, static (engine, context) => engine.WlPerformerAllows(context.ability, context.performer));
        EngineHooks.PerformerCandidates.Register("WlStratTornPerformers", 14700, static (engine, context) => engine.WlPerformers(context.ability, context.playerId));
        EngineHooks.TargetAllows.Register("WlStratTornTargetAllows", 14700, static (engine, context) => engine.WlTargetAllows(context.ability, context.command, context.player, context.target));
        EngineHooks.ValidateLate.Register("ValidateWlStratTorn", 14700, static (engine, context) => engine.ValidateWlStratTorn(context.id, context.command, context.player));
        EngineHooks.AttackStrengthBefore.Register("WlStratTornAttackStrength", 14700, static (engine, context) => engine.WlAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("WlStratTornAttackKind", 14700, static context => WlAttackKind(context.abilityId));
        EngineHooks.DestroysTarget.Register("WlStratTornDestroysTarget", 14700, static context => WlDestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("WlStratTornBowsTarget", 14700, static context => WlBowsTarget(context.abilityId));
        EngineHooks.RunEffects.Register("RunWlStratTorn", 14700, static (engine, context) => engine.RunWlStratTorn(context));
        EngineHooks.EitherOrPrompt.Register("WlStratTornEitherOrPrompt", 14700, static (engine, p, source, affected) => engine.WlEitherOrPrompt(p, source, affected));
        EngineHooks.EitherOrOptions.Register("WlStratTornEitherOrOptions", 14700, static (engine, p, affected, source, payable) => engine.WlEitherOrOptions(p, affected));
        EngineHooks.ApplyEitherOr.Register("WlStratTornApplyEitherOr", 14700, static (engine, p, option, source, decider, affected, sourceIds) => engine.WlApplyEitherOr(p, option, source, decider, affected));
        EngineHooks.DeciderPrompt.Register("WlStratTornDeciderPrompt", 14700, static (engine, p) => p.SpecId == WlStratTornSpecs.GossipBow ? engine.WlDeciderPrompt(p) : null);
        EngineHooks.DeciderOptions.Register("WlStratTornDeciderOptions", 14700, static (engine, specId, deciderId, command) => specId == WlStratTornSpecs.GossipBow ? engine.WlDeciderOptions(deciderId) : null);
        EngineHooks.DeciderVerb.Register("WlStratTornDeciderVerb", 14700, static (engine, specId) => specId == WlStratTornSpecs.GossipBow ? "Bow" : null);
        EngineHooks.DeciderIdBefore.Register("WlStratTornDeciderIdBefore", 14700, static (engine, specId, command) => specId == WlStratTornSpecs.GossipBow ? command.TargetPlayerId ?? command.PlayerId : null);
        EngineHooks.ApplyDecider.Register("WlStratTornApplyDecider", 14700, static (engine, p) => engine.WlApplyDecider(p));
    }
}
