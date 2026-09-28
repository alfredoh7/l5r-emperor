namespace L5R.Core;

// Forgotten Legacy pass (issue #467), batch 1: the execution/validation helpers of the twelve Personalities this batch wires (personality-wave-67) and
// the small general primitives they needed. The declarative data (ability ids, targets, costs) is in CardAbilities.cs; each primitive expires by
// comparison with the turn number, never by a sweep.
//
//   * A SINGLE-ABILITY LOCK (MatchState.DisabledAbilities): "Choose one of his abilities; it may not be used" (Doji Hakuseki, Doji Rengetsu) -- unlike
//     the action lock (ActionLock.cs, "he may not perform actions"), one printed ability of one card, for the turn. Read where every action is
//     judged performable (RequireCanPerform) and where an alternate Gold source is offered (AlternateGoldSource). A printed ability split into
//     several catalog entries (a shared UsageKey: "bow or straighten him") is one ability, locked whole.
//   * ENGINE-LISTED EFFECT CHOICES (PendingEffectChoice.Options): a choice made at resolution whose options depend on the game -- which of his
//     abilities, which card of his unit -- through the same effectChoice decision Mirumoto Yumaru's fixed branch uses (EffectChoice.cs). Nothing is
//     asked when there is one option (it simply happens) or none.
//   * "HAS PERFORMED A [keyword] ACTION THIS TURN" (MatchState.PerformedKeywordActions): recorded for the performing Personality as an action begins
//     to resolve (or is negated: a negated action was still performed), read by Daigotsu Murota's target legality.
//   * A DELAYED "BEFORE IT ENDS" HONOR CHECK (MatchState.ProvincesHeldHonorChecks): Asahina Shigemitsu's gain on another player's turn if none of
//     his controller's provinces were destroyed that turn, drained in EndTurn beside Tamori Tamoko's.
//   * A COST ON A SLOT (CostSubject.Slot, AlternativeCosts.cs): Iuchi Abodan's "choose a Personality in a discard pile, remove him from the game",
//     a declared cost (so a negated action still pays it) on a card the action names beside its target.
//   * AN ATTACH TO A TARGETED PERSONALITY (AttachWielder.PersonalitySlot and AttachSpec.IgnoreHonorRequirement, AttachAction.cs): Daigotsu Gyoken's
//     "Target your Personality and a non-Unique attachment in any player's discard pile: Attach it to the Personality, paying all costs and ignoring
//     Honor Requirements" -- Moto Jun-Ni's attach from a discard pile, with the receiving Personality a target slot rather than the source or a
//     performer, judged once on the whole selection (ValidateAbility).

/// <summary>One printed ability (by UsageKey, else id) of one card that may not be used on turn <paramref name="Turn"/>.</summary>
public sealed record DisabledAbility(string CardId, string UsageKey, int Turn, string Source, int? UntilPlayerId = null, int UntilTurnsTaken = 0);

/// <summary>Asahina Shigemitsu: <paramref name="PlayerId"/> gains 1 Honor before turn <paramref name="Turn"/> ends if none of his provinces was destroyed that turn.</summary>
public sealed record ProvincesHeldHonorCheck(int PlayerId, string SourceCardId, int Turn);

/// <summary>The Personality <paramref name="CardId"/> performed an action carrying <paramref name="Keyword"/> on turn <paramref name="Turn"/>.</summary>
public sealed record PerformedKeywordAction(string CardId, string Keyword, int Turn);

public sealed partial class MatchEngine
{
    private const string Batch1DestroyCardEffect = "fl-destroy-card:";
    private const string Batch1DisableAbilityEffect = "fl-disable-ability:";
    // The action keywords whose performance is recorded ("unless Murota has performed a Dark Virtue action this turn").
    private static readonly string[] RecordedPerformedKeywords = ["Dark Virtue"];

    // Iuchi Abodan: whether the Personality chosen for the cost was dead, read the moment before the cost removes him from the game.
    private bool _abodanChoseDead;

    // ---- validation ----

    private void ValidateForgottenLegacyBatch1(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Daigotsu Murota: "Target an enemy card, which must have no attachments unless Murota has performed a Dark Virtue action this turn."
        if (id == "murota-bow" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var murotaTarget))
            Require(!HasAttachments(murotaTarget) || source is not null && PerformedKeywordActionThisTurn(source.Id, "Dark Virtue"),
                "The target must have no attachments unless Murota has performed a Dark Virtue action this turn.");
        // Doji Rengetsu: "Discard the Imperial Favor to target..." -- a cost, so the Favor must be his to discard (ValidateAbility asks every Favor discard, EmperorStrongholdBatch1.cs).
    }

    // ---- costs ----

    private void CaptureBeforeCostsForBatch1(string id, GameCommand command)
    {
        if (id != "abodan-move") return;
        _abodanChoseDead = SlotCards(command, "chosen").FirstOrDefault() is { Zone: Zone.Dead };
    }

    // ---- "has performed a [keyword] action this turn" ----

    private void RecordPerformedKeywordActions(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (!IsAction(ability) || ActionPerformerIdOrNull(ability, command, source) is not { } performerId) return;
        _state.PerformedKeywordActions.RemoveAll(p => p.Turn != _state.TurnNumber);
        foreach (var keyword in RecordedPerformedKeywords)
            if (ActionCarriesKeyword(ability, source, keyword) && !PerformedKeywordActionThisTurn(performerId, keyword))
                _state.PerformedKeywordActions.Add(new(performerId, keyword, _state.TurnNumber));
    }

    private bool PerformedKeywordActionThisTurn(string cardId, string keyword) =>
        _state.PerformedKeywordActions.Any(p => p.CardId == cardId && p.Keyword == keyword && p.Turn == _state.TurnNumber);

    // ---- the single-ability lock ----

    private static string AbilityUsageKey(string abilityId) => AbilityCatalog.All.TryGetValue(abilityId, out var ability) ? ability.UsageKey ?? abilityId : abilityId;

    private bool AbilityDisabled(CardInstance card, string abilityId)
    {
        var key = AbilityUsageKey(abilityId);
        return BattleAbilityLocked(card) // Yoritomo Naoto (issue #477): every ability of a card that was in the unit, until the battle ends.
            || _state.DisabledAbilities.Any(d => d.CardId == card.Id && (d.UsageKey == key || d.UsageKey == AllAbilitiesUsageKey) && DisabledAbilityHolds(d)); // AllAbilitiesUsageKey: Ninube Onchi
    }

    private void RequireAbilityNotDisabled(GameCommand command, CardInstance? source)
    {
        if (source is not null && command.AbilityId is { } abilityId && AbilityDisabled(source, abilityId))
            Require(false, $"That ability of {Definition(source).Name} may not be used this turn.");
    }

    // "One of his abilities": each printed (or granted) action of the card, a UsageKey group counted once.
    private EffectOption[] AbilityLockOptions(CardInstance card) =>
        AbilityIdsOf(card).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && IsAction(d))
            .GroupBy(AbilityUsageKey, StringComparer.Ordinal)
            .Select(g => new EffectOption(g.Key, $"{Definition(card).Name}: \"{AbilityCatalog.All[g.First()].Label}\" may not be used", [Batch1DisableAbilityEffect + g.Key]))
            .ToArray();

    private void DisableAbility(CardInstance card, string usageKey, string source)
    {
        _state.DisabledAbilities.RemoveAll(d => !DisabledAbilityHolds(d));
        _state.DisabledAbilities.Add(new(card.Id, usageKey, _state.TurnNumber, source));
        Emit("abilityDisabled", $"{source}: one of {Definition(card).Name}'s abilities may not be used this turn.", card.OwnerId, card.Id);
    }

    // ---- engine-listed effect choices ----

    // Returns true when the action paused for the acting player's choice (the caller returns); with one option it simply applies, with none nothing happens.
    private bool BeginListedEffectChoice(GameCommand command, EffectOption[] options, string prompt, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (options.Length == 0) return false;
        if (options.Length == 1)
        {
            foreach (var effect in options[0].Effects) if (!ApplyForgottenLegacyBatch1BranchEffect(effect, command)) ApplyEmperorBatch4BranchEffect(effect, command);
            return false;
        }
        _state.PendingEffectChoice = new()
        {
            Command = command, PlayerId = command.PlayerId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList(), Options = options.ToList()
        };
        _state.PendingChoice = new("effectChoice", command.PlayerId, command.CardId, prompt);
        _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    // ApplyBranchEffect's arm for this batch's listed options; false for any other effect.
    private bool ApplyForgottenLegacyBatch1BranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(Batch1DestroyCardEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[Batch1DestroyCardEffect.Length..]);
            if (InPlay(card)) DestroyTargetCard(card, command.PlayerId);
            return true;
        }
        if (effect.StartsWith(Batch1DisableAbilityEffect, StringComparison.Ordinal))
        {
            if (command.TargetId is { } targetId && _state.Cards.TryGetValue(targetId, out var target))
                DisableAbility(target, effect[Batch1DisableAbilityEffect.Length..], command.CardId is { } sourceId ? Definition(Card(sourceId)).Name : "An action");
            return true;
        }
        return false;
    }

    // ---- effects ----

    private bool RunForgottenLegacyBatch1Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Asahina Shigemitsu: +1 strength to each of his controller's provinces for the turn; on another player's turn, the delayed Honor check.
            case "shigemitsu-provinces":
                foreach (var province in player.Provinces.Where(p => !p.Destroyed)) GiveProvinceStrengthThisTurn(player.Id, province.Index, 1, Definition(source!).Name);
                if (_state.ActivePlayerId != player.Id) _state.ProvincesHeldHonorChecks.Add(new(player.Id, source!.Id, _state.TurnNumber));
                return false;
            // Asako Kaitoko: his Force set to 0; then, if he is Kolat, Ninja, or Shadowlands, a card without attachments in his unit is destroyed.
            case "kaitoko-weaken":
            {
                ApplyModifier(target!, source!.Id, id, "Force", -CardForce(target!));
                if (!InPlay(target!) || !EffectConditionMet(id, target)) return false;
                var candidates = UnitCardsWithoutAttachments(target!); // Makito's reading (issue #498): any card of the unit without attachments, not only Followers.
                var options = candidates.Select(c => new EffectOption(c.Id, $"Destroy {Definition(c).Name}", [Batch1DestroyCardEffect + c.Id])).ToArray();
                return BeginListedEffectChoice(command, options, $"{Definition(source).Name}: choose the card without attachments in {Definition(target!).Name}'s unit to destroy.",
                    consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            }
            case "higaonna-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // Bayushi Toshimo: the bow, then the turn straighten-lock if he has a Weapon or his controller controls a Courtier (read as the action resolves).
            case "toshimo-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (Attachments(source!.Id).Any(a => Keyword(a, "Weapon")) || ControlsKeyword(player.Id, "Courtier")) LockStraightening(target!);
                return false;
            case "takichi-move":
                ExecuteMove(command, ability, source, target);
                return false;
            // Daigotsu Gyoken: the additional action appropriate to this phase (Shinjo Katsuo's), then the attach -- whose entering-play window, if any, is where
            // the action pauses (Moto Jun-Ni's ExecuteAttach). The Gold was paid by PayForAbility (AttachCost).
            case "gyoken-attach":
                GrantAdditionalAction(player.Id, null);
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            case "murota-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Doji Hakuseki / Doji Rengetsu: the acting player chooses one of the target's abilities, which may not be used this turn.
            case "hakuseki-disable":
            case "rengetsu-disable":
                return BeginListedEffectChoice(command, AbilityLockOptions(target!), $"{Definition(source!).Name}: choose one of {Definition(target!).Name}'s abilities; it may not be used this turn.",
                    consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            // Hiruma Nikaru: "Bow him. He may not perform actions." -- independent sentences (a negated bow leaves the lock).
            case "nikaru-bow-lock":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                LockActions(target!, source!.Id);
                return false;
            // Iuchi Abodan: the chosen Personality was already removed (the declared cost); move the target home, and bow him if the chosen one was dead.
            case "abodan-move":
                MoveHome(target!, false);
                if (_abodanChoseDead && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Kitsu Kagako: the Lion Ancestor is created, then her controller gains 1 Honor (from an action she performed).
            case "kagako-create":
                return ExecuteCreate(ability, ability.Creates!, command, player, () => GainHonorFromPerformer(source!, 1, Definition(source!).Name));
            default:
                throw new InvalidOperationException($"Unknown Forgotten Legacy batch 1 ability '{id}'.");
        }
    }

    // Bayushi Higaonna: "If the Ranged Attack destroyed a card, its owner loses 1 Honor." The target was in play when the Attack resolved and is not now.
    private void AfterForgottenLegacyBatch1Attack(string abilityId, string? targetId, bool targetInPlayBefore, string? sourceId)
    {
        if (abilityId != "higaonna-attack" || targetId is null || !targetInPlayBefore) return;
        var destroyed = Card(targetId);
        if (InPlay(destroyed)) return;
        GainHonor(destroyed.OriginalOwnerId ?? destroyed.OwnerId, -1, "Bayushi Higaonna", sourceId is not null && _state.Cards.TryGetValue(sourceId, out var higaonna) ? higaonna : null);
    }

    // Asahina Shigemitsu: drained as the turn ends. ProvinceDestroyedSinceTurnEnd of a player whose turn it is NOT covers exactly this turn (his own
    // last turn ended as this one began).
    private void ResolveProvincesHeldHonorChecks()
    {
        foreach (var check in _state.ProvincesHeldHonorChecks.Where(c => c.Turn == _state.TurnNumber).ToArray())
        {
            if (_state.ProvinceDestroyedSinceTurnEnd[check.PlayerId]) continue;
            var shigemitsu = Card(check.SourceCardId);
            if (InPlay(shigemitsu) && shigemitsu.OwnerId == check.PlayerId) GainHonorFromPerformer(shigemitsu, 1, Definition(shigemitsu).Name);
            else GainHonor(check.PlayerId, 1, Definition(shigemitsu).Name);
        }
        _state.ProvincesHeldHonorChecks.Clear();
    }

    // Doji Hakuseki's duel: "The controller of the duel's winner gains 2 Honor. Bow the duel's loser, and destroy him if he is dishonorable." A tie has no
    // winner (both lose). The winner's controller's gain is from an action that targeted or was performed by the winner, so a dishonorable winner is
    // rehonored instead.
    private void HakusekiDuelOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        if (winnerId is not null && _state.Cards.TryGetValue(winnerId, out var winner) && InPlay(winner)) GainHonorFromPerformer(winner, 2, "Doji Hakuseki");
        foreach (var loserId in loserIds)
        {
            var loser = Card(loserId);
            if (!InPlay(loser)) continue;
            if (!StalwartNegatesBow(loser, challengerPlayerId)) loser.Bowed = true;
            if (loser.Dishonorable) DestroyUnits([loser], false, challengerPlayerId);
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterForgottenLegacyBatch1Hooks()
    {
        EngineHooks.ValidateLate.Register("ValidateForgottenLegacyBatch1", 200, static (engine, context) => engine.ValidateForgottenLegacyBatch1(context.id, context.command, context.player, context.source));
    }
}
