using System.Text.RegularExpressions;

namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group C (strategy-wave-60): the Strategies of the Emperor Edition reference set that were still unwired, re-checked against the
// engine as it stands. The declarative ability data is in EmperorRemainingCAbilities (added to AbilityCatalog.All by one hook); each card's printed text (the scan's, read by eye) and the
// reading taken are documented at its entry. The engine helpers below are documented where they are used; the shared files gained only small additive hooks, each marked "issue #492".
//
// New primitives:
//   * STRATEGY REACTIONS "before an action resolves" PLAYED FROM THE HAND (The Height of Courage, Rhetoric, Solid Defense): BeforeActionReactionCandidates also lists Strategies in a hand, so
//     the alternating before-action window opens for a Strategy holder exactly as it does for an in-play watcher (either player may react to any action -- Rhetoric's own text names no
//     "another player's"), and lists them with their performers;
//   * A REDIRECT OF THE ACTION'S HONOR CHANGE (Rhetoric) and a NEGATION OF ITS HONOR LOSSES (Solid Defense), both marks on the held action read where Honor changes (GainHonor);
//   * A REDUCTION OF EVERY HONOR GAIN outside the Attack Phase until its controller's next turn begins (Rumors Travel), read there too;
//   * AN ADDITIONAL ACTION THE NAMED PERFORMER MUST PERFORM (Spinning Heel Kick): AdditionalActionGrant.PerformerId.
public sealed class EmperorRemainingCState
{
    /// <summary>Rumors Travel: the players whose Limited action is in force until THEIR next turn begins, one entry per copy played (each reduces every gain by 1).</summary>
    public List<int> RumorsTravelOwners { get; set; } = [];
    /// <summary>Rhetoric: the action whose Honor change is redirected (EmperorStrongholdBatch2.ActionKey), to whom, by which Courtier's Chi, and whether it has happened.</summary>
    public string? RedirectActionKey { get; set; }
    public int RedirectToPlayer { get; set; }
    public string? RedirectCourtierId { get; set; }
    public bool RedirectSpent { get; set; }
    /// <summary>Solid Defense: the action whose Honor losses are negated.</summary>
    public string? NegateLossesActionKey { get; set; }
}

public static class EmperorRemainingCAbilities
{
    /// <summary>The Reactions whose card is a Strategy played from a hand "before an action resolves" (see the file comment).</summary>
    public static readonly string[] BeforeActionHandReactions = ["height-courage-negate", "rhetoric-redirect", "solid-defense-negate", "solid-defense-negate-favor"];

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
            Trigger: "before-action-resolves", UsageKey: "solid-defense-negate", ActionKeywords: ["Political"]);
        all["solid-defense-negate-favor"] = new("Reaction", "Political Reaction: Before the resolution of another player's action, discard the Imperial Favor: Negate any Honor losses from the action's effects", "none",
            Trigger: "before-action-resolves", UsageKey: "solid-defense-negate", ActionKeywords: ["Political"]);
        all["solid-defense-move"] = new("Battle", "Target a Personality without attachments: Move him home", "personality", RequireNoAttachments: true, Move: new(MoveSpec.Target, MoveSpec.Home));
        // Rhetoric (oracle-l5r-6312, Political): "Reaction: Before a player gains or loses Honor from an action's effects, bow your performing Courtier: Choose another player. If the Courtier's Chi is
        // higher than or equal to the Honor gain or loss, the chosen player instead of the original player takes the gain or loss."
        all["rhetoric-redirect"] = new("Reaction", "Before a player gains or loses Honor from an action's effects, bow your performing Courtier: Choose another player. If the Courtier's Chi is higher than or equal to the Honor gain or loss, the chosen player instead of the original player takes the gain or loss",
            "player", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Trigger: "before-action-resolves", Costs: ActionCost.BowPerformer);
        // Rumors Travel (oracle-l5r-6406, Political): "Limited: Until your next turn begins, before each time a player gains 1 or more Honor, reduce the gain by 1 if it is not during a battle or Attack
        // Phase." and "Battle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved." (Broken Alliance's text).
        all["rumors-travel-reduce"] = new("Limited", "Limited: Until your next turn begins, before each time a player gains 1 or more Honor, reduce the gain by 1 if it is not during a battle or Attack Phase", "none");
        all["rumors-travel-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved", "none",
            Performer: "own-personality", Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
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

    private static readonly Regex HonorLossText = new(@"\b(loses?|lost)\b[^.;:]{0,60}\bHonor\b|\bHonor loss", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Whether an announced action may change a player's Honor / cost one Honor: read off its printed label (the printed text), the way Shiro Chugo's window is (EmperorStrongholdBatch2),
    // or off the curated list of actions whose effects lose Honor.
    private static bool ActionMayChangeHonor(AbilityDefinition acting, string abilityId) =>
        HonorChangeText.IsMatch(acting.Label) || AbilityMayCauseHonorLoss(abilityId);
    private static bool ActionMayLoseHonor(AbilityDefinition acting, string abilityId) =>
        HonorLossText.IsMatch(acting.Label) || AbilityMayCauseHonorLoss(abilityId);

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
            // An action whose effects may change a player's Honor, and an unbowed Courtier to bow.
            case "rhetoric-redirect":
                return ActionMayChangeHonor(acting, command.AbilityId) && BeforeActionReactionPerformers(card.OwnerId, AbilityCatalog.All[abilityId]).Any();
            // Another player's action whose effects may lose Honor, and a way to pay (a Personality of 5 or higher Force, or the Imperial Favor).
            case "solid-defense-negate":
                return command.PlayerId != card.OwnerId && ActionMayLoseHonor(acting, command.AbilityId) && ControlsPersonalityWithForce(card.OwnerId, 5);
            case "solid-defense-negate-favor":
                return command.PlayerId != card.OwnerId && ActionMayLoseHonor(acting, command.AbilityId) && !ControlsPersonalityWithForce(card.OwnerId, 5) && _state.FavorPlayerId == card.OwnerId;
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
            case "selfless-defense-destroy" when ability.Slots is not null:
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
            "remc-own-courtier-or-shugenja-other-than-performer" => personality && own && (Keyword(target, "Courtier") || Keyword(target, "Shugenja")) && target.Id != command.PerformerId,
            _ => false
        };
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------

    // true: the action paused (an Attack's reaction window) and finishes from there; false: it is finished here; null: not this group's.
    private bool? RunEmperorRemainingC(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
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
                if (_state.PendingNegatableAbility is { } heldForLosses) RemC.NegateLossesActionKey = ActionKey(heldForLosses);
                Emit("honorLossesNegated", $"{Definition(source!).Name}: every Honor loss from the action's effects is negated.", player.Id, source!.Id);
                return false;
            case "solid-defense-move":
                ExecuteMove(command, ability, source, target);
                return false;
            // The chosen player takes the first Honor change of another player that the Courtier's Chi covers, from this action's effects.
            case "rhetoric-redirect":
                if (_state.PendingNegatableAbility is { } heldForRedirect)
                {
                    RemC.RedirectActionKey = ActionKey(heldForRedirect); RemC.RedirectToPlayer = command.TargetPlayerId!.Value; RemC.RedirectCourtierId = command.PerformerId; RemC.RedirectSpent = false;
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
            default:
                return null;
        }
    }

    // ---- The additional action a named Personality must perform (Spinning Heel Kick) ------------------------------------------------------------------------------------

    // ConsumeAdditionalActionGrant's condition: a grant with a PerformerId is spent only by an action that Personality performs (a printed action is performed by the card that prints it, an
    // Item's or Spell's by its wielder, a named "performing X" by X; a Strategy that names no performer is performed by no Personality at all).
    private bool PerformerGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        grant.PerformerId is null || command is not null && ActionPerformingPersonality(command) is { } performer && performer.Id == grant.PerformerId;

    // ---- Honor -------------------------------------------------------------------------------------------------------------------------------------------------------

    // GainHonor's first hook (issue #492): Rhetoric's redirect (the player the change lands on), then Solid Defense's negation of the action's losses, then Rumors Travel's reduction of a gain.
    // Returns the amount to apply and the player who takes it; an amount of 0 stops the change. Honor paid as a COST is not an effect of the action (_payingActionCost).
    private (int PlayerId, int Amount) EmperorRemainingCHonorChange(int playerId, int amount, string reason)
    {
        if (amount == 0) return (playerId, 0);
        if (!_payingActionCost && CurrentActionKey() is { } key)
        {
            if (RemC.RedirectActionKey == key && !RemC.RedirectSpent && playerId != RemC.RedirectToPlayer
                && RemC.RedirectCourtierId is { } courtierId && _state.Cards.TryGetValue(courtierId, out var courtier) && InPlay(courtier) && PersonalityChi(courtier) >= Math.Abs(amount))
            {
                RemC.RedirectSpent = true;
                Emit("honorRedirected", $"{Player(RemC.RedirectToPlayer).Name} takes the Honor {(amount > 0 ? "gain" : "loss")} of {Math.Abs(amount)} from {reason} instead of {Player(playerId).Name} (Rhetoric).", RemC.RedirectToPlayer);
                playerId = RemC.RedirectToPlayer;
            }
            if (amount < 0 && RemC.NegateLossesActionKey == key)
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
