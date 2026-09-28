namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 5: the execution/validation helpers of the eleven Personalities personality-wave-72 wires -- Agasha Ryo, Asako Nobunori, Asako Tsunefusa,
// Asako Yorisada, Isawa Kimi - exp 2, Isawa Norimichi, Shiba Kataken, Shiba Sansesuke, Shiba Tsukimi - exp 4, Kitsu Tamasine and Tamori Kusugi. The declarative data (ability ids, targets, costs) is in
// CardAbilities.cs; the shared files gained only small additive hooks, each marked "issue #477".
//
//   * "Enters play from a Province" for a Reaction that touches that province (Ryo): MatchState.LastProvinceEntryProvinceIndex, set beside LastProvinceEntryCardId.
//   * An Attack compared against BASE Force (Nobunori): AttackComparedForce takes the ability, and for his the lower of the target's current and printed Force is compared when it has no attachments.
//   * A battle-long army Force bonus (Norimichi): MatchState.ArmyForceBonuses, read by ArmyForces while the army has any unit, expiring by comparison with the battle number.
//   * A static "+1F for Personalities sharing an element" (Kimi): EmperorBatch5Force, PersonalityForce's hook.
//   * "Put a card into a player's hand" (Kimi): NoteCardIntoHand at every site a card goes into a hand during an action, snapshotted into LastResolvedAction.HandRecipients; the discard is a
//     DeciderChoice (the other player chooses).
//   * "Lost Honor from another player's action during an Action Phase since your last turn began" (Tamasine): MatchState.HonorLostToOtherActionSinceTurnBegan.
//   * A per-battle use count (Tsukimi's "twice per battle"): two stamps on the card's own AbilityUses, like the Castle of Order's once per battle.
public sealed partial class MatchEngine
{
    private const string KimiElementForceEffect = "kimi-exp2-element-force";
    private static readonly string[] ElementKeywords = ["Air", "Earth", "Fire", "Water", "Void"];
    private const string TsukimiBattleKey = "tsukimi-reflexes:battle", TsukimiCountKey = "tsukimi-reflexes:count";

    // ---- hand and Honor tracking ----

    // A card went into a player's hand: while an action is resolving, it is one that "put a card into his hand".
    private void NoteCardIntoHand(int playerId)
    {
        NoteCardIntoHandThisPhase(playerId); // Broken Cipher (issue #501, EmperorMechD.cs): any card put into a hand, during an action or not.
        if (_runningCommand is null && _state.PendingAttack is null && _state.PendingDuel is null) return;
        if (!_state.CurrentActionHandRecipients.Contains(playerId)) _state.CurrentActionHandRecipients.Add(playerId);
    }

    // The action that just resolved cost these players Honor: in an Action Phase, another player's action's loss is remembered until the loser's next turn begins.
    private void NoteHonorLostToOtherAction(int actingPlayerId, List<int> losers)
    {
        if (_state.Phase != Phase.Action) return;
        foreach (var loser in losers)
            if (loser != actingPlayerId) { _state.HonorLostToOtherActionSinceTurnBegan[loser] = true; _state.HonorLostToOtherActionSinceTurnEnded[loser] = true; }
    }

    // ---- Nobunori: compare against base Force ----

    // "Melee 4 Attack that you may compare against base Force if the target has no attachments": the player chooses (issue #492, ruling R5) -- current Force, or, for a target without attachments,
    // its base Force (whichever it is, higher or lower than the current).
    private int AttackComparedForce(CardInstance target, string? abilityId) =>
        abilityId == "nobunori-attack" && !HasAttachments(target) && AttackCompareChoiceOf(abilityId) == "base" ? Definition(target).Force : CardForce(target);

    // ---- Norimichi: +5 total Force ----

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): the two armies' total Force at the current battlefield, as the battle would resolve it.
    internal (int Attacker, int Defender) ArmyForcesForTest()
    {
        var province = _state.CurrentBattleProvince!.Value; var attacker = _state.ActivePlayerId; var defender = 1 - attacker;
        return ArmyForces(Army(attacker, province).ToArray(), Army(defender, province).ToArray(), attacker, defender);
    }

    private int ArmyForceBonusFor(int playerId, CardInstance[] army) =>
        army.Length == 0 ? 0 : _state.ArmyForceBonuses.Where(b => b.PlayerId == playerId && b.Battle == _state.BattleNumber).Sum(b => b.Amount);

    // ---- Kimi: +1F for the Personalities that share an element ----

    // PersonalityForce's hook: "Your other Personalities at Kimi's location that share any element keywords with her or with any of her Spells have +1F." One +1F for each Kimi (a card
    // that qualifies for two Kimi is unusual: she is Unique).
    private int EmperorBatch5Force(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        var bonus = 0;
        foreach (var kimi in _state.Cards.Values)
        {
            if (kimi.Id == card.Id || kimi.OwnerId != card.OwnerId || !InPlay(kimi) || Definition(kimi).Effect != KimiElementForceEffect || !SameLocation(kimi, card)) continue;
            var elements = ElementKeywords.Where(e => Keyword(kimi, e) || Attachments(kimi.Id).Any(a => Definition(a).Type == CardType.Spell && Keyword(a, e)));
            if (elements.Any(e => Keyword(card, e))) bonus++;
        }
        return bonus;
    }

    // ---- validation ----

    private void ValidateEmperorBatch5(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Agasha Ryo: "from a Province" -- the recruit he last made from one.
            case "ryo-refill":
                Require(_state.LastProvinceEntryCardId == source!.Id && _state.LastProvinceEntryProvinceIndex is not null, "Ryo did not enter play from a Province.");
                break;
            // Shiba Tsukimi: twice per battle, and only a move that changes where she is.
            case "tsukimi-home":
                Require(TsukimiUsesThisBattle(source!) < 2, "Tsukimi has already used this Reaction twice this battle.");
                Require(source!.Zone == Zone.Battlefield, "Tsukimi is already at home.");
                break;
            case "tsukimi-battlefield":
                Require(TsukimiUsesThisBattle(source!) < 2, "Tsukimi has already used this Reaction twice this battle.");
                Require(_state.CurrentBattleProvince is not null && !AtBattle(source!), "Tsukimi is already at the current battlefield.");
                break;
            // Kitsu Tamasine: several Ancestors only after Honor was lost to another player's action.
            case "tamasine-boost-many":
                Require(_state.HonorLostToOtherActionSinceTurnBegan[player.Id], "You have not lost Honor from another player's action during an Action Phase since your last turn began.");
                break;
        }
    }

    private int TsukimiUsesThisBattle(CardInstance tsukimi) =>
        tsukimi.AbilityUses.TryGetValue(TsukimiBattleKey, out var battle) && battle == _state.BattleNumber ? tsukimi.AbilityUses.GetValueOrDefault(TsukimiCountKey) : 0;

    // The after-action-resolved conditions of Kimi's and Tsukimi's Reactions (AfterActionResolvedApplies).
    private bool EmperorBatch5AfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (!InPlay(source)) return false;
        switch (abilityId)
        {
            // "After the resolution of an action that put any cards into another player's hand": the other player's hand, with a card in it to discard.
            case "kimi-exp2-discard":
                return facts.HandRecipients.Any(r => r != playerId) && Player(1 - playerId).Hand.Count > 0;
            // "Twice per battle, after the resolution of another player's Battle action": Move her home, or to the current battlefield.
            case "tsukimi-home":
            case "tsukimi-battlefield":
                return facts.PlayerId != playerId && AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && Definition(source).Type == CardType.Personality
                    && TsukimiUsesThisBattle(source) < 2
                    && (abilityId == "tsukimi-home" ? source.Zone == Zone.Battlefield : _state.CurrentBattleProvince is not null && !AtBattle(source));
            default:
                return false;
        }
    }

    // Shiba Sansesuke: an action would target one of the reactor's own Shugenja at his location (not Sansesuke himself), and would be legal against Sansesuke once he is bowed (the cost).
    private bool SansesukeRedirectApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        affected.OwnerId == card.OwnerId && affected.Id != card.Id && Definition(affected).Type == CardType.Personality && Keyword(affected, "Shugenja")
        && SameLocation(card, affected) && WithReactorBowed(card, () => ActionAcceptsTarget(subject, card.Id));

    // ---- effects ----

    private bool RunEmperorBatch5Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Agasha Ryo: the province he left holds the ordinary face-down refill by now (a recruit refills at once); it is turned face-up. A province with no card (an empty deck at the
            // time) is refilled face-up if the deck has a card, and a destroyed province refills nothing.
            case "ryo-refill":
                RefillProvinceFaceUp(player, _state.LastProvinceEntryProvinceIndex!.Value, source!);
                return false;
            // Asako Tsunefusa: the chosen keyword, for the turn (an effect with no stated duration).
            case "tsunefusa-kolat": case "tsunefusa-ninja": case "tsunefusa-shadowlands":
                GrantKeyword(target!, id switch { "tsunefusa-kolat" => "Kolat", "tsunefusa-ninja" => "Ninja", _ => "Shadowlands" }, source!.Id, GrantDuration.Turn);
                return false;
            // "Bow him. If he is Kolat, Ninja, or Shadowlands, choose a player..." -- the bow always happens, the player choice is the follow-up asked after it (Doji Yoshitada's shape).
            case "tsunefusa-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case "yorisada-bow": case "kataken-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "yorisada-straighten":
                Straighten(target!);
                return false;
            // Isawa Kimi - exp 2: the other player chooses and discards a card from his hand.
            case "kimi-exp2-discard":
                return BeginDeciderChoice(DeciderSpecs.KimiDiscard, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            // Isawa Norimichi: the Shugenja's destruction from the held action is negated; and the army's +5 total Force for this battle.
            case "norimichi-negate":
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case "norimichi-army":
                _state.ArmyForceBonuses.Add(new(player.Id, 5, _state.BattleNumber));
                Emit("armyForceBonus", $"{Definition(source!).Name}: {player.Name}'s army has +5 total Force until this battle ends.", player.Id, source!.Id);
                return false;
            // Shiba Sansesuke: the action targets him instead (he was bowed as the cost).
            case "sansesuke-redirect":
                RedirectAction(HeldAction(), source!.Id, source);
                return false;
            // Shiba Tsukimi: home, or to the current battlefield; the use is counted for this battle.
            case "tsukimi-home": case "tsukimi-battlefield":
            {
                var uses = TsukimiUsesThisBattle(source!);
                source!.AbilityUses[TsukimiBattleKey] = _state.BattleNumber; source.AbilityUses[TsukimiCountKey] = uses + 1;
                // A Reaction (never a Battle action), so it needs no presence and is not an "Absent" move to the battle; it is the ordinary negatable movement.
                if (id == "tsukimi-home") MoveHome(source, false);
                else TryMoveToBattlefield(source, _state.CurrentBattleProvince!.Value);
                return false;
            }
            // Kitsu Tamasine: the Lion Ancestor; and +4F to each targeted Ancestor.
            case "tamasine-create":
                return ExecuteCreate(ability, ability.Creates!, command, player);
            case "tamasine-boost-one": case "tamasine-boost-many":
                foreach (var ancestor in SlotCards(command, "targets").ToArray()) ApplyModifier(ancestor, source!.Id, id, "Force", 4);
                return false;
            // Tamori Kusugi: the defender's province at the current battlefield, raised or lowered by 4 until end of turn (issue #492 ruling R3: nothing says permanent).
            case "kusugi-raise": case "kusugi-lower":
                GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, _state.CurrentBattleProvince!.Value, id == "kusugi-raise" ? 4 : -4, "Tamori Kusugi");
                return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition batch 5 ability '{id}'.");
        }
    }

    private void RefillProvinceFaceUp(PlayerState player, int provinceIndex, CardInstance source)
    {
        var province = player.Provinces[provinceIndex];
        if (province.Destroyed) return;
        if (province.CardId is { } existingId)
        {
            var existing = Card(existingId);
            if (existing.Zone == Zone.Province && !existing.FaceUp)
            {
                existing.FaceUp = true;
                Emit("provinceRefilled", $"{Definition(source).Name}: {player.Name}'s province {provinceIndex + 1} was refilled face up with {Definition(existing).Name}.", player.Id, existingId);
            }
            return;
        }
        if (player.DynastyDeck.Count == 0 || province.HoldsNoDynastyCards) return; // issue #503: Illusory Defense's province
        var refillId = player.DynastyDeck[0]; player.DynastyDeck.RemoveAt(0);
        var refill = Card(refillId);
        province.CardId = refillId; refill.Zone = Zone.Province; refill.ProvinceIndex = provinceIndex; refill.FaceUp = true;
        Emit("provinceRefilled", $"{Definition(source).Name}: {player.Name} refilled province {provinceIndex + 1} face up with {Definition(refill).Name}.", player.Id, refillId);
        AfterProvinceRefilledP6(player, province, faceUp: true); // issue #503: Shosuro Ietsuno
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorPersonalityBatch5_Rung4b2()
    {
        EngineHooks.PersonalityForce.Register("EmperorBatch5Force", 900, static (engine, context) => engine.EmperorBatch5Force(context.card));
    }
}
