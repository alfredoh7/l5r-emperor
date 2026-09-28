namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P4 (continued): CHOICES AT RESOLUTION THAT FEED A FURTHER STEP.
//
//   * POWER OF STRENGTH: two independent enemy targets (slots "first" and "other", each with a unit Gold Cost below the performer's Gold Cost); "Bow one of them" is the acting player's choice at
//     resolution, then the Ranged Attack -- its strength the first target's Force as the Attack is made, its only possible target the other Personality (if legal: RequireLegalAttackTarget),
//     Engulfing Flames' "second effect is an Attack" shape (a never-offered attack ability, power-of-strength-attack, through the ordinary Attack pipeline and window).
//   * ASSET DENIAL: "Move him to a different location" is two abilities sharing one use (Shosuro Hawado's split: home, or another unresolved battlefield named in GameCommand.Province); "Discard a
//     card in a province" is a choice at resolution among every card in any player's provinces (face-down ones named by position only), refilled as after any discard from a province.
//   * NO TIME FOR GAMES: Entrenched Position's trigger (the Attacker's Battle action announced before the Defender's first normal opportunity) and a watch on the next Honor loss this turn from
//     another player's card effect (GainHonor's hook).
//   * SECRET RESERVE: a Focus Effect read into a Chi duel's totals (each Weapon on the owner's Personality in the duel adds its +1C), and a per-Item transfer: after the Items straighten, the
//     acting player decides for each one in turn whether it stays or is transferred to another Personality he controls that may carry it (one listed choice per Item, EffectChoice.cs).
// ======================================================================================================================================================================================
public static class EmperorP4MoreAbilities
{
    public const string PowerOfStrength = "power-of-strength-bow", PowerOfStrengthAttack = "power-of-strength-attack";
    public const string AssetDenialDestroy = "asset-denial-destroy", AssetDenialHome = "asset-denial-home", AssetDenialBattlefield = "asset-denial-battlefield";
    public const string NoTimeNegate = "no-time-for-games-negate", NoTimeRehonor = "no-time-for-games-rehonor";
    public const string SecretReserveFocus = "secret-reserve-focus", SecretReserveStraighten = "secret-reserve-straighten";

    public const string StrongPerformer = "p4-own-unbowed-personality-base-force-7-at-battle";
    public const string EnemyUnitGoldBelowPerformer = "p4-enemy-personality-unit-gold-lt-performer-gold";
    public const string NinjaPerformer = "p4-own-ninja-no-non-ninja-followers-at-battle";
    public const string Retainer = "p4-retainer";
    public const string DishonorablePerformer = "p4-own-dishonorable-personality";
    public const string OwnItem = "p4-own-item";

    public static Dictionary<string, AbilityDefinition> AddEmperorP4More(this Dictionary<string, AbilityDefinition> all)
    {
        // Power of Strength (oracle-l5r-6034): "Battle: Choose your performing unbowed Personality with 7 or higher base Force and target two enemy Personalities, each with unit Gold Cost lower than
        // your performer's Gold Cost: Bow one of them. Ranged Attack with strength equal to the first target's Force, which may only target the other Personality (if legal)."
        all[PowerOfStrength] = new("Battle", "Choose your performing unbowed Personality with 7 or higher base Force and target two enemy Personalities, each with unit Gold Cost lower than your performer's Gold Cost: Bow one of them. Ranged Attack with strength equal to the first target's Force, which may only target the other Personality (if legal)",
            "slots", Performer: StrongPerformer,
            Slots: [new("first", EnemyUnitGoldBelowPerformer, "The first enemy Personality"), new("other", EnemyUnitGoldBelowPerformer, "The other enemy Personality")]);
        // The Ranged Attack of Power of Strength's second effect: never offered as an action of its own.
        all[PowerOfStrengthAttack] = new("Battle", "Ranged Attack with strength equal to the first target's Force, which may only target the other Personality", "attack");
        // Asset Denial (oracle-l5r-634): "Open: Bow your performing Ninja Personality and target a Retainer: Destroy it." and "Battle: Choose your performing Ninja Personality with no non-Ninja
        // Followers at the current battlefield: Move him to a different location. If he moved, straighten his unit. Discard a card in a province."
        all[AssetDenialDestroy] = new("Open", "Bow your performing Ninja Personality and target a Retainer: Destroy it", Retainer,
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        all[AssetDenialHome] = new("Battle", "Choose your performing Ninja Personality with no non-Ninja Followers at the current battlefield: Move him to a different location (home). If he moved, straighten his unit. Discard a card in a province",
            "none", Performer: NinjaPerformer, UsageKey: "asset-denial-battle", Move: new(MoveSpec.Performer, MoveSpec.Home));
        all[AssetDenialBattlefield] = new("Battle", "Choose your performing Ninja Personality with no non-Ninja Followers at the current battlefield: Move him to a different location (another battlefield). If he moved, straighten his unit. Discard a card in a province",
            "none", Performer: NinjaPerformer, UsageKey: "asset-denial-battle", Move: new(MoveSpec.Performer, MoveSpec.AnyUnresolved));
        // No Time for Games (oracle-l5r-5605): "Reaction: After an attacking player announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects." and
        // "Open: Choose your performing dishonorable Personality: Rehonor him. After the next time (this turn) you lose Honor from another player's card effect, give him three +1F Revenge tokens."
        all[NoTimeNegate] = new("Reaction", "After an attacking player announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects", "none",
            Trigger: ForgottenLegacyBatch3Triggers.Entrenched);
        all[NoTimeRehonor] = new("Open", "Choose your performing dishonorable Personality: Rehonor him. After the next time (this turn) you lose Honor from another player's card effect, give him three +1F Revenge tokens",
            "none", Performer: DishonorablePerformer);
        // Secret Reserve (oracle-l5r-6552): "As a Focus Effect: Until this duel ends, give +1C to each Weapon attached to your Personality in this duel." and "Battle/Open: Target one or more of your
        // Items: Straighten them. You may transfer them to other Personalities you control."
        all[SecretReserveFocus] = new("FocusEffect", "As a Focus Effect: Until this duel ends, give +1C to each Weapon attached to your Personality in this duel", "none");
        all[SecretReserveStraighten] = new("BattleOrOpen", "Target one or more of your Items: Straighten them. You may transfer them to other Personalities you control", "slots",
            Slots: [new("items", OwnItem, "One or more of your Items", 1, 20)]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private const string P4PowerBowEffect = "p4-power-bow:", P4ProvinceDiscardEffect = "p4-province-discard:", P4TransferEffect = "p4-transfer:", P4KeepEffect = "p4-keep:";

    private static string? P4MoreAttackKind(string abilityId) => abilityId == EmperorP4MoreAbilities.PowerOfStrengthAttack ? "Ranged" : null;
    private static bool P4MoreBowsTarget(string abilityId) => abilityId == EmperorP4MoreAbilities.PowerOfStrength;
    private static bool P4MoreDestroysTarget(string abilityId) => abilityId == EmperorP4MoreAbilities.AssetDenialDestroy;

    private bool P4MoreTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        switch (category)
        {
            // Power of Strength: an enemy Personality whose unit's total Gold Cost is below the performer's own Gold Cost; the two slots name different Personalities.
            case EmperorP4MoreAbilities.EnemyUnitGoldBelowPerformer:
                return personality && target.OwnerId != player.Id && InPlay(target) && command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)
                    && UnitTotalGoldCost(target, out _) < CardGoldCost(performer)
                    && (command.Selections ?? []).Where(s => s.CardIds is not null).SelectMany(s => s.CardIds!).Count(id => id == target.Id) <= 1;
            // Asset Denial: "a Retainer" -- any card in play with the keyword.
            case EmperorP4MoreAbilities.Retainer:
                return InPlay(target) && Keyword(target, "Retainer");
            // Secret Reserve: "one or more of your Items".
            case EmperorP4MoreAbilities.OwnItem:
                return target.OwnerId == player.Id && InPlay(target) && Definition(target).Type == CardType.Item;
            default:
                return false;
        }
    }

    private bool? EmperorP4MorePerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        EmperorP4MoreAbilities.StrongPerformer => Definition(performer).Type == CardType.Personality && InPlay(performer) && !performer.Bowed && Definition(performer).Force >= 7
            && (!actingAsBattle || AtBattle(performer)),
        EmperorP4MoreAbilities.NinjaPerformer => Definition(performer).Type == CardType.Personality && InPlay(performer) && Keyword(performer, "Ninja") && AtBattle(performer)
            && Attachments(performer.Id).Where(a => Definition(a).Type == CardType.Follower).All(f => Keyword(f, "Ninja")),
        EmperorP4MoreAbilities.DishonorablePerformer => Definition(performer).Type == CardType.Personality && InPlay(performer) && performer.Dishonorable,
        _ => null
    };

    private string?[]? EmperorP4MorePerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) =>
        ability.Performer is EmperorP4MoreAbilities.StrongPerformer or EmperorP4MoreAbilities.NinjaPerformer or EmperorP4MoreAbilities.DishonorablePerformer
            ? _state.Cards.Values.Where(c => c.OwnerId == playerId && EmperorP4MorePerformerAllows(ability, c, actingAsBattle) == true).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
            : null;

    private void ValidateEmperorP4More(string id, GameCommand command)
    {
        if (id == EmperorP4MoreAbilities.PowerOfStrength && SlotCards(command, "first").FirstOrDefault() is { } first && SlotCards(command, "other").FirstOrDefault() is { } other)
            Require(first.Id != other.Id, "Power of Strength targets two different enemy Personalities.");
    }

    private bool? RunEmperorP4More(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "" : Definition(source).Name;
        switch (id)
        {
            case EmperorP4MoreAbilities.PowerOfStrength:
            {
                var first = SlotCards(command, "first").Single(); var other = SlotCards(command, "other").Single();
                var options = new[] { first, other }.Select(c => new EffectOption($"bow-{c.Id}", $"Bow {Definition(c).Name} ({c.Id})", [P4PowerBowEffect + c.Id])).ToArray();
                SpendHandPlayedCard(player, source);
                return BeginP4ListedChoice(command, player.Id, options, $"{name}: bow one of them; then a Ranged Attack with the first target's Force that may only target the other.",
                    consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            }
            case EmperorP4MoreAbilities.AssetDenialDestroy:
                if (target is not null && InPlay(target)) DestroyTargetCard(target, player.Id);
                return false;
            case EmperorP4MoreAbilities.AssetDenialHome:
            case EmperorP4MoreAbilities.AssetDenialBattlefield:
            {
                var ninja = Card(command.PerformerId!);
                var wasAt = (ninja.Zone, ninja.ProvinceIndex);
                ExecuteMove(command, ability, source, target);
                if ((ninja.Zone, ninja.ProvinceIndex) != wasAt && InPlay(ninja)) StraightenUnit(ninja);
                var options = _state.Players.SelectMany(p => p.Provinces.Where(pr => !pr.Destroyed && pr.CardId is not null).Select(pr => (Player: p, Province: pr)))
                    .Select(x => new EffectOption($"discard-{x.Player.Id}-{x.Province.Index}",
                        $"Discard the card in {x.Player.Name}'s province {x.Province.Index + 1} ({(Card(x.Province.CardId).FaceUp ? Definition(Card(x.Province.CardId)).Name : "face-down")})",
                        [$"{P4ProvinceDiscardEffect}{x.Player.Id}:{x.Province.Index}"])).ToArray();
                SpendHandPlayedCard(player, source);
                return BeginP4ListedChoice(command, player.Id, options, $"{name}: discard a card in a province.", consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            }
            case EmperorP4MoreAbilities.NoTimeNegate:
                NegateAnnouncedAction(source!);
                return false;
            case EmperorP4MoreAbilities.NoTimeRehonor:
            {
                var performer = Card(command.PerformerId!);
                Rehonor(performer);
                P4.NoTimeWatches.Add(new(performer.Id, player.Id, _state.TurnNumber, _state.BattleNumber, name));
                Emit("watchSet", $"{name}: after the next time this turn {player.Name} loses Honor from another player's card effect, {Definition(performer).Name} gets three +1F Revenge tokens.", player.Id, performer.Id);
                return false;
            }
            case EmperorP4MoreAbilities.SecretReserveStraighten:
            {
                var items = SlotCards(command, "items").Where(InPlay).ToArray();
                foreach (var item in items) Straighten(item);
                P4.TransferQueue = items.Select(i => i.Id).ToList();
                SpendHandPlayedCard(player, source);
                return OpenNextSecretReserveStep(command, player.Id, new()
                {
                    Command = command, PlayerId = player.Id, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
                    OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList()
                });
            }
            default:
                return null;
        }
    }

    // ApplyEmperorP4BranchEffect's arm for these options.
    private bool ApplyEmperorP4MoreBranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(P4PowerBowEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[P4PowerBowEffect.Length..]);
            if (InPlay(card)) BowByEffect(card, command.PlayerId);
            return true;
        }
        if (effect.StartsWith(P4ProvinceDiscardEffect, StringComparison.Ordinal))
        {
            var parts = effect[P4ProvinceDiscardEffect.Length..].Split(':');
            var owner = Player(int.Parse(parts[0]));
            var province = owner.Provinces.FirstOrDefault(p => p.Index == int.Parse(parts[1]) && !p.Destroyed);
            if (province?.CardId is not { } cardId) return true;
            var card = Card(cardId);
            card.Zone = IsDynastyCardType(Definition(card).Type) ? Zone.DynastyDiscard : Zone.FateDiscard; card.ProvinceIndex = null; card.FaceUp = true;
            province.CardId = null;
            Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province {province.Index + 1}.", owner.Id, card.Id);
            Refill(owner, province);
            return true;
        }
        if (effect.StartsWith(P4TransferEffect, StringComparison.Ordinal))
        {
            var parts = effect[P4TransferEffect.Length..].Split('>');
            var item = Card(parts[0]); var host = Card(parts[1]);
            if (CanTransferTo(item, host)) TransferAttachment(item, host, "Secret Reserve");
            return true;
        }
        return effect.StartsWith(P4KeepEffect, StringComparison.Ordinal);
    }

    // ResolveEffectChoice's continuation: a further step of the same action (the next Item of Secret Reserve, Power of Strength's Attack). True when the action waits again.
    private bool ContinueEmperorP4Choice(PendingEffectChoice pending, EffectOption option)
    {
        switch (pending.Command.AbilityId)
        {
            case EmperorP4MoreAbilities.SecretReserveStraighten:
                return OpenNextSecretReserveStep(pending.Command, pending.PlayerId, pending);
            case EmperorP4MoreAbilities.PowerOfStrength:
            {
                var command = pending.Command;
                var attacker = Player(command.PlayerId);
                var performer = Card(command.PerformerId!);
                var first = SlotCards(command, "first").Single(); var other = SlotCards(command, "other").Single();
                var ability = AbilityCatalog.All[EmperorP4MoreAbilities.PowerOfStrengthAttack];
                var probe = new GameCommand("useAbility", attacker.Id, _state.Version, command.CardId, AbilityId: EmperorP4MoreAbilities.PowerOfStrengthAttack, PerformerId: performer.Id);
                var targetId = InPlay(other) && LegalAttackTarget(EmperorP4MoreAbilities.PowerOfStrengthAttack, ability, probe, attacker, performer, other) ? other.Id : null;
                var strength = InPlay(first) ? CardForce(first) : Definition(first).Force;
                Emit("powerOfStrengthAttack", $"Power of Strength: a Ranged {strength} Attack{(targetId is null ? " with no legal target" : $" against {Definition(other).Name}")}.", attacker.Id, targetId);
                var running = _runningCommand; _runningCommand = command;
                _state.NegatedEffects = pending.Negated.ToList();
                bool deferred;
                try { deferred = ResolveOrDeferAttack(EmperorP4MoreAbilities.PowerOfStrengthAttack, attacker, targetId, performer.Id, pending.ConsumedGrant, strengthOverride: strength); }
                finally { _runningCommand = running; }
                if (!deferred) _state.NegatedEffects.Clear();
                return deferred;
            }
            default:
                return false;
        }
    }

    // Secret Reserve: the next Item that could go to another Personality its player controls; false when none is left (the action then finishes).
    private bool OpenNextSecretReserveStep(GameCommand command, int playerId, PendingEffectChoice template)
    {
        while (P4.TransferQueue.Count > 0)
        {
            var item = Card(P4.TransferQueue[0]); P4.TransferQueue.RemoveAt(0);
            if (!InPlay(item) || item.Zone != Zone.Attached) continue;
            var hosts = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && CanTransferTo(item, c))
                .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            if (hosts.Length == 0) continue;
            var options = hosts.Select(h => new EffectOption($"transfer-{item.Id}-{h.Id}", $"Transfer {Definition(item).Name} to {Definition(h).Name} ({h.Id})", [$"{P4TransferEffect}{item.Id}>{h.Id}"]))
                .Append(new EffectOption($"keep-{item.Id}", $"Keep {Definition(item).Name} on {Definition(Card(item.AttachedTo)).Name}", [P4KeepEffect + item.Id])).ToArray();
            _state.PendingEffectChoice = new()
            {
                Command = command, PlayerId = playerId, Negated = template.Negated.ToList(), ConsumedGrant = template.ConsumedGrant, PlayedFromDiscard = template.PlayedFromDiscard,
                OpponentHonorBefore = template.OpponentHonorBefore, BowedBefore = template.BowedBefore.ToList(), PreExistingGrants = template.PreExistingGrants.ToList(), Options = options.ToList()
            };
            _state.PendingChoice = new("effectChoice", playerId, command.CardId, $"Secret Reserve: you may transfer {Definition(item).Name} to another Personality you control.", command.PlayerId);
            _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
            return true;
        }
        return false;
    }

    // ---- No Time for Games: the watch on the next Honor loss from another player's card effect ------------------------------------------------------------------------------------------------

    // GainHonor's hook, after a loss was applied: "from another player's card effect" -- a card another player controls, or (with no card named) the other player's action now resolving.
    private void P4HonorLost(int playerId, CardInstance? source)
    {
        if (P4.NoTimeWatches.Count == 0) return;
        var fromOther = source is not null ? source.OwnerId != playerId : _runningCommand is { } running && running.PlayerId != playerId;
        if (!fromOther) return;
        var due = P4.NoTimeWatches.Where(w => w.PlayerId == playerId && w.Turn == _state.TurnNumber).ToArray();
        P4.NoTimeWatches.RemoveAll(w => w.Turn != _state.TurnNumber || due.Contains(w));
        foreach (var watch in due)
        {
            if (!_state.Cards.TryGetValue(watch.CardId!, out var him) || !InPlay(him)) continue;
            Emit("watchFired", $"{watch.Source}: {Player(playerId).Name} lost Honor from another player's card effect -- {Definition(him).Name} gets three +1F Revenge tokens.", playerId, him.Id);
            for (var i = 0; i < 3; i++) CreateToken(him, "Revenge", watch.Source, "Force", 1);
        }
    }

    // ---- Secret Reserve's Focus Effect ----------------------------------------------------------------------------------------------------------------------------------------------------

    // DuelStatValue's hook: in a Chi duel, +1 for each Weapon on the duelist per Secret Reserve his player focused.
    private int P4DuelStatBonus(CardInstance duelist, string stat)
    {
        if (stat != "Chi" || _state.PendingDuel is not { } duel) return 0;
        var reserves = duel.ChallengerFocused.Concat(duel.ChallengedFocused).Select(Card)
            .Count(c => c.OwnerId == duelist.OwnerId && (Definition(c).Abilities ?? []).Contains(EmperorP4MoreAbilities.SecretReserveFocus));
        return reserves == 0 ? 0 : reserves * Attachments(duelist.Id).Count(a => Keyword(a, "Weapon"));
    }

    // ---- test seams (L5R.Core.Tests has InternalsVisibleTo) ----
    internal int P4DuelStatValueForTest(string cardId, string stat) => DuelStatValue(Card(cardId), stat);
    internal void P4LoseHonorFromForTest(int playerId, int amount, string sourceCardId) => GainHonor(playerId, -amount, "test", Card(sourceCardId));
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP4MoreHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP4More", 2900, static abilities => abilities.AddEmperorP4More());
    }
}
