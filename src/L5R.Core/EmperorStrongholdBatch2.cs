using System.Text.RegularExpressions;

namespace L5R.Core;

// Emperor Edition Stronghold pass (issue #486), batch 2 (stronghold-wave-18): the execution/validation helpers of the seven Strongholds this batch wires -- Palace of the Breaking
// Dawn, Pillars of Virtue, Shiro Chugo, Steel Soul Dojo, Suitengu's Torch, Temple of Purity and The Aerie. The declarative data (ability ids, targets, performers) is in
// CardAbilities.cs beside each card's printed text; each helper below is documented at its use.
//
//   * A RONIN ENTRY BENEFIT (Palace of the Breaking Dawn): a term of every entry price (QuotePurchase, EquipGoldCost) and a waiver of the Honor Requirement wherever one is checked.
//   * "EFFECTS WILL NOT BE NEGATED OR DELAYED" for ONE announced action (Suitengu's Torch), chosen in the "after you announce an action" window (Tsuruchi Yakusho's), and a Force
//     penalty that will not be negated (Temple of Purity): both feed the shared "cannot be negated" questions Yoritomo Hiromi's trait already answers (HiromiUnnegatable, ActionProtected).
//   * A REDUCTION OF EVERY HONOR GAIN AND LOSS from one action's effects (Shiro Chugo), read where Honor changes (GainHonor).
//   * A FIRST-PER-PHASE TRAIT AFTER A KEYWORD ACTION (Steel Soul Dojo's card draw).
public static class EmperorStrongholdBatch2Triggers
{
    /// <summary>"After you announce a Thunder Battle action" (Suitengu's Torch): a Reaction played in the announce window (Tsuruchi Yakusho's).</summary>
    public const string TorchAnnounce = "own-thunder-battle-action-announced";
}

public sealed partial class MatchEngine
{
    private const string PalaceEffect = "palace-breaking-dawn";
    private const string SteelSoulEffect = "steel-soul-dojo";
    private const string TorchAnnounceTrigger = EmperorStrongholdBatch2Triggers.TorchAnnounce;

    // ---- the identity of the action being resolved -----------------------------------------------------------------------------------------------------------------------
    // "player|ability|card": enough to tell one announced action from the next without holding on to a command object (the state is copied on every rejected command, so a
    // reference would not survive). A marker is replaced when the next non-Reaction action is announced (ContinueUseAbility), so a repeat of the same action is never marked by an
    // earlier one.
    private static string ActionKey(GameCommand command) => $"{command.PlayerId}|{command.AbilityId}|{command.CardId}";

    // The action whose effects are being applied now: an ability's synchronous resolution, the deferred Attack under way, or the duel a Battle action issued.
    private string? CurrentActionKey()
    {
        return CurrentActionFacts(out var key, out _) ? key : null; // EmperorRulingR4Honor.cs: the ability being resolved, the Attack or duel it started, or the paused action being finished; never a Focus Effect
    }

    private void ClearActionMarkersForNewAction(string? abilityId)
    {
        if (abilityId is null || !AbilityCatalog.All.TryGetValue(abilityId, out var ability) || ability.Timing == "Reaction") return;
        if (_honorWindowsDone > 0) return; // R4 (issue #492): the action's replay after an Honor-change window keeps the marks the Reactions played in it set.
        _state.UnnegatableActionKey = null;
        _state.HonorReducedActionKey = null; _state.UnimpeachableActionKey = null; _state.UnimpeachablePlayerId = null;
        _state.RemC.RedirectActionKey = null; _state.RemC.NegateLossesActionKey = null; // Emperor Edition remaining-cards pass, group C (issue #492): Rhetoric's and Solid Defense's marks.
    }

    // ---- Suitengu's Torch: "Its effects will not be negated or delayed" ------------------------------------------------------------------------------------------------
    private bool CurrentActionUnnegatable() => _state.UnnegatableActionKey is { } key && key == CurrentActionKey();

    // Whether a command about to resolve is the one Suitengu's Torch protected, or is Temple of Purity's own Force penalty (a whole action that cannot be negated: the penalty is its
    // effect, and negating the action would negate the penalty).
    private bool CommandUnnegatable(GameCommand? command) =>
        command?.AbilityId is { } id && (id == "temple-purity-weaken" || _state.UnnegatableActionKey == ActionKey(command) || MechEUnnegatable(id)); // issue #501: The Heir's Wrath

    private bool ThunderBattleAction(GameCommand command, AbilityDefinition ability)
    {
        if (command.AbilityId is null || !IsBattleAction(command.AbilityId) || ability.Timing == "Reaction") return false;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        return ActionCarriesKeyword(ability, source, "Thunder");
    }

    // The card that may react to this announcement: Tsuruchi Yakusho's (a Ranged Battle action, the watcher at the battle) or Suitengu's Torch (a Thunder Battle action of its
    // controller's, a Reaction not yet used this turn).
    private CardInstance? FindAnnounceWatcher(GameCommand command, AbilityDefinition ability)
    {
        var ranged = ability.Timing == "Battle" && AttackKind(command.AbilityId!) == "Ranged";
        var thunder = ThunderBattleAction(command, ability);
        var inPlay = !ranged && !thunder ? null : _state.Cards.Values.FirstOrDefault(c => c.OwnerId == command.PlayerId && InPlay(c) && !c.Bowed
            && AbilityIdsOf(c).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction"
                && (ranged && d.Trigger == "own-ranged-action-announced" && (AtBattle(c) || a == TfGearAbilities.LookoutDestroy /* issue #622: Lookout reacts from anywhere in play */) || thunder && d.Trigger == TorchAnnounceTrigger && Unspent(c, a))));
        return inPlay ?? NoHidingPlaceWatcher(command) ?? P3AnnounceWatcher(command); // No Hiding Place (issue #498): a Strategy in the announcing player's hand (EmperorFollowupsAttack.cs); Ikoma Ayumu, Utaku Lishan (issue #503).
    }

    // The rest of an announced action once its announce window has closed (Suitengu's Torch, Tsuruchi Yakusho): the "before an action resolves" windows every announced action
    // passes (ContinueUseAbility's remaining steps), then its effects. Before the Torch the announce window went straight to the effects, so a Ranged Attack announced with
    // Yakusho's reaction open skipped them; the steps are the ones ContinueUseAbility takes after its announce-time windows, in the same order.
    private void ContinueAnnouncedAction(GameCommand pending)
    {
        if (ResolveOrDeferBeforeActionReaction(pending)) return;
        if (ResolveOrDeferUnimpeachableName(pending, pending.AbilityId)) return;
        if (NegateEveryAction) { ResolveNegatedAction(pending); return; }
        ExecuteAbilityEffects(pending);
    }

    // ---- Palace of the Breaking Dawn: "Your Ronin cards enter play paying 1 less Gold and ignoring Honor Requirements" ------------------------------------------------------
    private bool ControlsPalace(int playerId) => Definition(Card(Player(playerId).StrongholdId)).Effect == PalaceEffect;

    // A term of every entry price: a Personality recruited (QuotePurchase) and a Follower attached (EquipGoldCost).
    private int RoninEntryDiscount(CardInstance entering, int playerId) => Keyword(entering, "Ronin") && ControlsPalace(playerId) ? 1 : 0;

    private bool RoninIgnoresHonorRequirement(CardInstance card, int playerId) => Keyword(card, "Ronin") && ControlsPalace(playerId);

    // AttachmentLegalTarget's Follower Honor Requirement: read against the Personality's controller (an attachment goes to its own controller's Personality).
    private bool RoninFollowerIgnoresHonorRequirement(CardDefinition attachmentDef, CardInstance wielder) =>
        attachmentDef.Keywords?.Contains("Ronin") == true && ControlsPalace(wielder.OwnerId);

    // ---- Steel Soul Dojo: "After the resolution of the first Dark Virtue action you took each phase: Draw a card" ------------------------------------------------------------
    // A trait (no designator), so mandatory and resolved before any Reaction (FireAfterOwnActionTraits' hook). The phases are the rulebook's (RulesPhase: the whole Attack-
    // declaration-through-battle stretch is one). A negated Dark Virtue action was still taken, so it is the first of its phase too (NoteSteelSoulNegated) and draws nothing.
    private const string SteelSoulPhaseKey = "steel-soul-draw-phase";

    private bool SteelSoulFirstDarkVirtueThisPhase(AbilityDefinition ability, int playerId, CardInstance? source, out CardInstance dojo)
    {
        dojo = Card(Player(playerId).StrongholdId);
        if (Definition(dojo).Effect != SteelSoulEffect || !IsAction(ability) || !ActionCarriesKeyword(ability, source, "Dark Virtue")) return false;
        var phaseKey = _state.TurnNumber * 10 + RulesPhase(_state.Phase);
        if (dojo.AbilityUses.TryGetValue(SteelSoulPhaseKey, out var last) && last == phaseKey) return false;
        dojo.AbilityUses[SteelSoulPhaseKey] = phaseKey;
        return true;
    }

    private void FireSteelSoulTrait(AbilityDefinition ability, int playerId, CardInstance? source)
    {
        if (!SteelSoulFirstDarkVirtueThisPhase(ability, playerId, source, out var dojo)) return;
        Emit("actionTraitFired", $"{Definition(dojo).Name}: after the first Dark Virtue action this phase, {Player(playerId).Name} draws a card.", playerId, dojo.Id);
        Draw(Player(playerId));
    }

    private void NoteSteelSoulNegated(AbilityDefinition ability, int playerId, CardInstance? source) => SteelSoulFirstDarkVirtueThisPhase(ability, playerId, source, out _);

    // ---- Pillars of Virtue: "After the resolution of a Kiho Battle action you took: Take an additional Battle action" ----------------------------------------------------------
    // The Reaction's own condition (AfterActionResolvedApplies): a Battle action (a Battle/Open one taken in a battle) of the player's own, carrying Kiho on the ability or on its card.
    private bool PillarsKihoBattleActionTaken(ResolvedActionFacts facts, int playerId) =>
        facts.PlayerId == playerId && IsBattleAction(facts.AbilityId)
        && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Kiho");

    // ---- Shiro Chugo: "Before an action's resolution, choose your performing unbowed Courtier: Reduce any Honor gain or loss from the action's effects by 2" -------------------
    // Played in the window before an Honor change from an action's effects (Mendon's R4 ruling, EmperorRulingR4Honor.cs); it used to read the action's printed label to guess whether one would happen.

    // GainHonor's hook: the Honor gained or lost by the marked action's effects is reduced by 2, to at most 0 (a gain of 1 becomes 0, a loss of 3 becomes a loss of 1).
    private int ChugoReducedHonorChange(int playerId, int amount, string reason)
    {
        if (amount == 0 || _state.HonorReducedActionKey is not { } key || key != CurrentActionKey()) return amount;
        var reduced = amount > 0 ? Math.Max(0, amount - 2) : Math.Min(0, amount + 2);
        Emit("honorChangeReduced", $"{Player(playerId).Name}'s Honor {(amount > 0 ? "gain" : "loss")} from {reason} was reduced by 2 (Shiro Chugo){(reduced == 0 ? " to 0" : "")}.", playerId);
        return reduced;
    }

    // The performers a before-action Reaction with a performer to choose may name, for the listing (the validation of the chosen one is ValidateAbility's).
    private IEnumerable<CardInstance> BeforeActionReactionPerformers(int playerId, AbilityDefinition reaction) => reaction.Performer switch
    {
        "own-unbowed-personality-with-keyword-at-any-location" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && !c.Bowed
            && HasKeywords(c, reaction.RequiredKeywords)).OrderBy(c => c.Id, StringComparer.Ordinal),
        // Ninja Tricks (Emperor Edition remaining-cards pass, issue #492, group B): "your performing Ninja Personality at the current battlefield".
        "own-unbowed-personality-with-keyword-explicitly-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c)
            && HasKeywords(c, reaction.RequiredKeywords)).OrderBy(c => c.Id, StringComparer.Ordinal),
        _ => EmperorRemainingCBeforeActionPerformers(playerId, reaction), // Emperor Edition remaining-cards pass, group C (issue #492): The Height of Courage.
    };

    // ---- Temple of Purity: "Give him -4F, which will not be negated" ------------------------------------------------------------------------------------------------------
    // Added directly: none of ApplyModifier's negations (Tireless Efforts, Creating Order, Aka-beko, a card's "all" negation) can catch it. The stat-penalty watchers are still told.
    private void ApplyUnnegatableModifier(CardInstance card, string source, string effect, string stat, int amount)
    {
        card.Modifiers.Add(new(source, effect, stat, amount));
        NoteStatPenalty(card, source, effect, stat, amount);
    }

    // The condition of Temple's second sentence, judged when it resolves: the Personality is Kolat, Ninja or Shadowlands, its player controls an Inquisitor, and it is not his turn.
    private bool TempleHonorConditionMet(CardInstance source, CardInstance? target) =>
        target is not null && MatchesTargetStatus(target, TargetStatus.KolatNinjaOrShadowlands) && ControlsKeyword(source.OwnerId, "Inquisitor") && _state.ActivePlayerId != source.OwnerId;

    // ---- The Aerie: "Your Samurai duels the Personality. Bow and dishonor the duel's loser. The winner's controller gains 1 Honor" ------------------------------------------------
    // Called from ApplyDuelOutcome: the winner's controller gains the Honor (a tie has no winner), every loser is bowed and dishonored.
    private void AerieDuelOutcome(string? winnerId, IEnumerable<string> loserIds, int challengerPlayerId)
    {
        if (winnerId is not null) GainHonor(Card(winnerId).OwnerId, 1, "The Aerie");
        foreach (var id in loserIds)
        {
            if (!InPlay(Card(id))) continue;
            if (!StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
            Dishonor(Card(id));
        }
    }

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------
    // True when the action paused (a Conditional choice, an Attack's reaction window) and finishes from there.
    private bool RunEmperorStrongholdBatch2Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // The Golden Plains: Reconnaissance, then the Plain of Gold is created at the battlefield (made inside the Reaction, so no entry window opens over it).
            case "golden-plains-recon":
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                if (_state.CurrentBattleProvince is int plainsProvince && !_state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == plainsProvince && Keyword(c, "Terrain")))
                    CreateInPlay(player.Id, CreatedCards.Resolve(ability.Creates!), Zone.Battlefield, plainsProvince, false, " at the battlefield", true, null);
                else Emit("createFailed", "This battlefield already has a Terrain in play: the Plain of Gold has no room.", player.Id, source?.Id);
                return false;
            // Plain of Gold: the Terrain is destroyed, and an additional Battle action to play the chosen Terrain from the hand is granted for that card alone.
            case "plain-gold-play-terrain":
                DiscardTerrain(source!);
                GrantAdditionalAction(player.Id, [target!.Id]);
                return false;
            // Palace of the Breaking Dawn: the chosen opposed Ronin Personality.
            case "palace-dawn-boost":
                ApplyModifier(Card(command.PerformerId!), source!.Id, id, "Force", 2);
                return false;
            // Pillars of Virtue.
            case "pillars-virtue-additional-action":
                GrantAdditionalAction(player.Id, null);
                return false;
            // Shiro Chugo's Reaction: the held action's Honor changes are reduced (the mark is read by GainHonor while that action resolves).
            case "chugo-honor-reduce":
                if (_state.PendingHonorChange is { } held) _state.HonorReducedActionKey = held.ActionKey;
                Emit("honorChangeReduction", $"{Definition(source!).Name}: every Honor gain or loss from the action's effects is reduced by 2.", player.Id, source!.Id);
                return false;
            // Shiro Chugo's Melee 4 Attack (+1 strength if the performer is Loyal, AttackStrength).
            case "chugo-melee":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // Suitengu's Torch's Reaction: the announced action's effects will not be negated or delayed.
            case "torch-protect":
                if (_state.PendingAnnouncedAction is { } announced) _state.UnnegatableActionKey = ActionKey(announced);
                Emit("actionProtected", $"{Definition(source!).Name}: the announced action's effects will not be negated or delayed.", player.Id, source!.Id);
                return false;
            // Suitengu's Torch's Battle action.
            case "torch-move":
                MoveHome(target!, false);
                return false;
            // Temple of Purity: the Force penalty first, then the player choice when its condition holds.
            case "temple-purity-weaken":
                ApplyUnnegatableModifier(target!, source!.Id, id, "Force", -4);
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            default:
                return false;
        }
    }
}
