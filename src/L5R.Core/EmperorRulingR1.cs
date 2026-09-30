namespace L5R.Core;

// Emperor Edition rulings pass R1 (issue #492): BATTLES WITHOUT A PROVINCE (Mendon's ruling, verbatim in docs/rules/EMPEROR_EDITION_CORE_RULES.md, "Battles without a province").
//
// A battlefield is normally one of the Defender's provinces (index 0..3, the battle mats' four lanes). This pass adds ADDITIONAL battlefields, numbered from FirstAdditionalBattlefield (100) in creation order:
//   * an additional battlefield may be ASSOCIATED WITH A PROVINCE (Two Fronts: two battlefields at one province) -- it is at that province while the province stands, and becomes a battlefield
//     WITHOUT a province the moment the province is destroyed (the first front destroys it: the second battle is fought without one);
//   * or be created with no province at all (Shinobi Assault, Chikushudo's Trickery, Surprise Attack).
//   * Any battlefield, primary or additional, is "without a province" whenever its province is destroyed, so a province destroyed by some other effect before the battle engages does the same.
// A battle without a province resolves with the battle resolution engine as normal EXCEPT that no province may be destroyed (so nothing that triggers on destroying a province can trigger) and
// there is no province strength; it has NO ADJACENT battlefields (AdjacentBattlefields); its Reconnaissance is its own (RulingR1State.AdditionalReconnaissance) since no province holds it.
//
// A BATTLE FOUGHT INSIDE THE ACTION PHASE (Shinobi Assault, Chikushudo's Trickery: "Fight a battle there"): the action pauses, the battle is fought by the Attacker its card names (ActivePlayerId is
// swapped for its length, the Retribution convention) and the Action Phase resumes -- with the action's own tail (CompleteAbilityResolution) -- once it ends (AsideBattle).
//
// "Unless stated otherwise, the player who declared the attack phase is the attacker" (Mendon): a Retribution's player declares its additional Attack Phase (already so), Chikushudo's Trickery names its
// Attacker (the other player), and Surprise Attack does not declare an attack at all -- the Attacker and Defender stay the battle's current ones.
public sealed record BattlefieldView(int Index, int? Province);

/// <summary>The battle a card fights inside the Action Phase, and the action that is finished when it is over.</summary>
public sealed class AsideBattle
{
    /// <summary>Whose turn it is (ActivePlayerId is restored to him when the battle ends).</summary>
    public int TurnPlayerId { get; set; }
    public int Battlefield { get; set; }
    public int AttackerId { get; set; }
    /// <summary>The action that opened the battle, kept whole so it completes exactly as it would have (the paused-action record every deciding window uses).</summary>
    public PendingEffectChoice Paused { get; set; } = new();
    /// <summary>Honor the action's player loses once the battle is over ("Fight a battle there ... Lose 5 Honor.": Shinobi Assault); 0 for none.</summary>
    public int LoseHonorAfter { get; set; }
}

public sealed class RulingR1State
{
    /// <summary>The additional battlefields of the Attack Phase (or of a battle fought inside the Action Phase): index (100 upward) to the Defender's province it is associated with, -1 for none.</summary>
    public Dictionary<int, int> Additional { get; set; } = [];
    /// <summary>Reconnaissance at an additional battlefield (a province holds only its own battlefield's), per player.</summary>
    public Dictionary<int, bool[]> AdditionalReconnaissance { get; set; } = [];
    /// <summary>Battlefields all movement to which is negated (Chikushudo's Trickery).</summary>
    public List<int> MovementNegated { get; set; } = [];
    /// <summary>Cards that "will not be destroyed during the battle's Combat Segment" (Chikushudo's Trickery's attacker): lifted when the Resolution Segment begins.</summary>
    public List<string> CombatShields { get; set; } = [];
    public AsideBattle? Aside { get; set; }
    /// <summary>The Perfect Moment: the additional Attack Phase it declared is in progress (null otherwise).</summary>
    public PerfectMomentState? PerfectMoment { get; set; }
    /// <summary>Two Fronts: the players' provinces marked "create two battlefields associated with it instead of one" for their next normal Attack Phase of the given turn.</summary>
    public List<TwoFrontsMark> TwoFronts { get; set; } = [];
}

/// <summary>The Perfect Moment's restrictions on its player during the additional Attack Phase it declared: "you will not destroy more than one of the Defender's provinces and your Personalities may not
/// assign and will not move unless they are Paragons or Battle Maidens". Destroyed counts the provinces its player has destroyed in it.</summary>
public sealed class PerfectMomentState
{
    public int PlayerId { get; set; }
    public int Destroyed { get; set; }
}

/// <summary>Player is the marker's controller (the Attacker), Province the Defender's province, Turn the turn whose next normal Attack Phase is meant.</summary>
public sealed record TwoFrontsMark(int PlayerId, int Province, int Turn);

public sealed partial class MatchEngine
{
    /// <summary>The first battlefield index that is not the battlefield of one of the Defender's provinces (their indexes are 0 upward -- four, and more once a province is gained -- so the additional
    /// battlefields are numbered well clear of them).</summary>
    public const int FirstAdditionalBattlefield = 100;

    private RulingR1State R1 => _state.R1;

    // ---- the battlefield model ----------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Defender's province a battlefield is at, or null when it is at none: an additional battlefield created without one, or one whose province has been destroyed (Mendon's ruling).
    /// The Defender is whoever is not the Attacker (ActivePlayerId, swapped for an additional Attack Phase and a battle fought inside the Action Phase).</summary>
    private int? BattlefieldProvinceIndex(int battlefield)
    {
        var index = battlefield < FirstAdditionalBattlefield ? battlefield : R1.Additional.TryGetValue(battlefield, out var associated) ? associated : -1;
        var provinces = Player(1 - _state.ActivePlayerId).Provinces;
        return index >= 0 && index < provinces.Count && !provinces[index].Destroyed ? index : null;
    }

    private ProvinceState? BattlefieldProvince(int battlefield) => BattlefieldProvinceIndex(battlefield) is int index ? Player(1 - _state.ActivePlayerId).Provinces[index] : null;

    /// <summary>The province of the battlefield being fought, null when there is no battle or it has none.</summary>
    private ProvinceState? CurrentBattlefieldProvince() => _state.CurrentBattleProvince is int current ? BattlefieldProvince(current) : null;

    /// <summary>The current battlefield's province index, null when there is no battle or it has none. Effects that name "the current battlefield's province" do nothing without one.</summary>
    private int? CurrentProvinceIndex() => _state.CurrentBattleProvince is int current ? BattlefieldProvinceIndex(current) : null;

    /// <summary>The current battlefield's province's strength as read now; null when the battlefield has no province.</summary>
    private int? CurrentProvinceStrength() => CurrentBattlefieldProvince() is { } province ? EffectiveProvinceStrength(province) : null;

    /// <summary>Creates an additional battlefield (optionally associated with one of the Defender's provinces), adds it to the battlefields still to be fought and returns its index.</summary>
    private int CreateAdditionalBattlefield(int? associatedProvince)
    {
        var index = Math.Max(FirstAdditionalBattlefield, R1.Additional.Keys.DefaultIfEmpty(FirstAdditionalBattlefield - 1).Max() + 1);
        R1.Additional[index] = associatedProvince ?? -1;
        _state.UnresolvedBattlefields.Add(index);
        Emit("battlefieldCreated", associatedProvince is int province
            ? $"An additional battlefield was created at province {province + 1}."
            : "A battlefield was created (not at any province).");
        return index;
    }

    /// <summary>The battlefield's name in prompts and the log.</summary>
    private string BattlefieldLabel(int battlefield) =>
        battlefield < FirstAdditionalBattlefield ? $"province {battlefield + 1}"
        : R1.Additional.TryGetValue(battlefield, out var associated) && associated >= 0 ? $"the additional battlefield at province {associated + 1}"
        : "the battlefield without a province";

    /// <summary>The battlefields next to `current`: those at the provinces next to its province in the Defender's line (destroyed provinces slide together), every battlefield of each such province
    /// (an additional one at it included). A battlefield without a province has none (Mendon: "There are no adjacent provinces to a battlefield that is not at a province").</summary>
    private IEnumerable<int> AdjacentBattlefields(int current)
    {
        if (BattlefieldProvinceIndex(current) is not int province) yield break;
        foreach (var neighbour in AdjacentProvinces(1 - _state.ActivePlayerId, province))
        {
            yield return neighbour;
            foreach (var extra in R1.Additional.Where(a => a.Value == neighbour).Select(a => a.Key).Order()) yield return extra;
        }
    }

    /// <summary>The battlefields the Attacker may assign attackers to at the declaration: every one of the Defender's provinces that stands, and each additional battlefield the declaration created (Two Fronts).</summary>
    private IEnumerable<int> AttackableBattlefields(int attackerId) =>
        Player(1 - attackerId).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Concat(_state.UnresolvedBattlefields.Where(b => b >= FirstAdditionalBattlefield));

    private BattlefieldView[]? BuildBattlefieldViews()
    {
        // Defenders can remain at a resolved extra front until the Attack Phase ends.
        // Its occupied location still needs its authoritative association in both public views.
        var occupied = _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex is >= 0).Select(c => c.ProvinceIndex!.Value);
        var battlefields = _state.UnresolvedBattlefields.Concat(_state.CurrentBattleProvince is int current ? [current] : []).Concat(occupied).Distinct().Order().ToArray();
        if (battlefields.Length == 0) return null;
        return battlefields.Select(b => new BattlefieldView(b, BattlefieldProvinceIndex(b))).ToArray();
    }

    // ---- Reconnaissance ------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Reconnaissance a player holds at a battlefield: a province's own for one of the four, the additional battlefield's otherwise.</summary>
    private bool BattlefieldReconnaissance(int playerId, int provinceOwnerId, int battlefield) =>
        battlefield < FirstAdditionalBattlefield ? Player(provinceOwnerId).Provinces[battlefield].Reconnaissance[playerId]
        : R1.AdditionalReconnaissance.TryGetValue(battlefield, out var held) && held[playerId];

    private void GrantBattlefieldReconnaissance(int playerId, int provinceOwnerId, int battlefield)
    {
        if (battlefield >= FirstAdditionalBattlefield)
        {
            if (!R1.AdditionalReconnaissance.TryGetValue(battlefield, out var held)) R1.AdditionalReconnaissance[battlefield] = held = new bool[2];
            if (held[playerId]) return;
            held[playerId] = true;
            Emit("reconnaissanceGained", $"{Player(playerId).Name} gained Reconnaissance at {BattlefieldLabel(battlefield)}.", playerId);
            return;
        }
        var province = Player(provinceOwnerId).Provinces[battlefield];
        if (province.Reconnaissance[playerId]) return;
        province.Reconnaissance[playerId] = true;
        Emit("reconnaissanceGained", $"{Player(playerId).Name} gained Reconnaissance in {Player(provinceOwnerId).Name}'s province {battlefield + 1}.", playerId);
    }

    /// <summary>Reconnaissance resets to No for both players at a battlefield once its battle resolves.</summary>
    private void ResetBattlefieldReconnaissance(int battlefield, int defender)
    {
        if (battlefield < FirstAdditionalBattlefield) Player(defender).Provinces.Single(p => p.Index == battlefield).Reconnaissance = new bool[2];
        else R1.AdditionalReconnaissance.Remove(battlefield);
    }

    // ---- the additional battlefields' lifetime -------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Attack Phase (or the battle fought inside the Action Phase) is over: additional battlefields cease to exist with everything scoped to them.</summary>
    private void ClearAdditionalBattlefields()
    {
        R1.Additional.Clear(); R1.AdditionalReconnaissance.Clear(); R1.MovementNegated.Clear(); R1.CombatShields.Clear();
    }

    // ---- Two Fronts ---------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"During your next normal Attack Phase (this turn), create two battlefields associated with it instead of one": marks one of the Defender's provinces; the declaration of the
    /// player's next NORMAL Attack Phase this turn (an additional one does not count) creates a second battlefield at it (DeclareAttackPhase).</summary>
    private void MarkTwoFronts(int attackerId, int province) => R1.TwoFronts.Add(new(attackerId, province, _state.TurnNumber));

    private void CreateTwoFrontsBattlefields(int attacker)
    {
        foreach (var mark in R1.TwoFronts.Where(m => m.PlayerId == attacker && m.Turn == _state.TurnNumber).ToArray())
            if (Player(1 - attacker).Provinces is var provinces && mark.Province >= 0 && mark.Province < provinces.Count && !provinces[mark.Province].Destroyed)
                CreateAdditionalBattlefield(mark.Province);
        R1.TwoFronts.RemoveAll(m => m.Turn <= _state.TurnNumber);
    }

    // ---- movement to a battlefield, and the shield of Chikushudo's Trickery ------------------------------------------------------------------------------------------

    /// <summary>"Negate all movement to it" (Chikushudo's Trickery): MoveToBattlefield's first question.</summary>
    private bool MovementToBattlefieldNegated(CardInstance card, int battlefield)
    {
        if (P3MoveToBattlefieldNegated(card, battlefield)) { Emit("moveNegated", $"{Definition(card).Name}'s movement to the current battlefield was negated.", card.OwnerId, card.Id); return true; } // Request Authorization (issue #503)
        if (!R1.MovementNegated.Contains(battlefield)) return false;
        Emit("moveNegated", $"{Definition(card).Name}'s movement to {BattlefieldLabel(battlefield)} was negated.", card.OwnerId, card.Id);
        return true;
    }

    /// <summary>"He will not be destroyed during the battle's Combat Segment": read where a destruction is applied (WillNotBeDestroyed).</summary>
    private bool CombatSegmentShielded(CardInstance card) => R1.CombatShields.Contains(card.Id) && _state.Phase == Phase.Battle;

    /// <summary>The Combat Segment is over (ResolveBattle): the shields lift before anything is destroyed by the resolution.</summary>
    private void EndCombatSegmentShields() => R1.CombatShields.Clear();

    // ---- The Perfect Moment (an additional Attack Phase with restrictions) ---------------------------------------------------------------------------------------------------

    /// <summary>Whether the Retribution restrictions of an additional Attack Phase apply (one Personality assigned, +1F): it is one, and it is Retribution's, not The Perfect Moment's.</summary>
    private bool RetributionRestricts() => RemC.RetributionRoot >= 0 && R1.PerfectMoment is null;

    /// <summary>"Your Personalities may not assign and will not move unless they are Paragons or Battle Maidens": every movement and assignment of a Personality of the player, MovementLocked's question.</summary>
    private bool PerfectMomentLocks(CardInstance card) =>
        R1.PerfectMoment is { } moment && card.OwnerId == moment.PlayerId && Definition(card).Type == CardType.Personality && !Keyword(card, "Paragon") && !Keyword(card, "Battle Maiden");

    /// <summary>"You will not destroy more than one of the Defender's provinces": the second one survives however large the win.</summary>
    private bool PerfectMomentBarsDestruction(int attacker) => R1.PerfectMoment is { } moment && moment.PlayerId == attacker && moment.Destroyed >= 1;

    private void NotePerfectMomentDestruction(int attacker)
    {
        if (R1.PerfectMoment is { } moment && moment.PlayerId == attacker) moment.Destroyed++;
    }

    // ---- assigning a unit to a created battlefield ----------------------------------------------------------------------------------------------------------------

    /// <summary>Whether a Personality may be assigned from home to a created battlefield by an effect that names the assignment (Shinobi Assault, Chikushudo's Trickery): the Maneuvers Segment's own
    /// locks apply (a movement lock, the Shieldman's refusal to attack), no Infantry/Cavalry segment. `mayBeBowed` is "even if he is bowed".</summary>
    private bool MayBeAssignedToCreatedBattlefield(CardInstance card, bool attacking, bool mayBeBowed) =>
        InPlay(card) && card.Zone == Zone.Home && Definition(card).Type == CardType.Personality && (!card.Bowed || mayBeBowed || MayAssignBowed(card))
        && !MovementLocked(card, MovementKinds.Assign) && !(attacking && MovementLocked(card, MovementKinds.AssignAttack)) && !(attacking && UnitHasShieldman(card)) && !(!attacking && IsGojuAsuka(card)); // Goju Asuka (issue #501): never into a defending army

    /// <summary>Assigns him (and his attachments) from home to the battlefield; false, changing nothing, when he may not be assigned. The same "after he is assigned" Reactions as the Maneuvers Segment.</summary>
    private bool AssignToCreatedBattlefield(CardInstance card, int battlefield, bool attacking, bool mayBeBowed)
    {
        if (!MayBeAssignedToCreatedBattlefield(card, attacking, mayBeBowed)) return false;
        card.Zone = Zone.Battlefield; card.ProvinceIndex = battlefield;
        foreach (var attached in Attachments(card.Id)) attached.ProvinceIndex = battlefield;
        _state.AssignedThisCommand.Add(card.Id);
        if (attacking) card.AssignedToAttackTurn = _state.TurnNumber;
        Emit("unitAssigned", $"{Definition(card).Name} assigned to {BattlefieldLabel(battlefield)}.", card.OwnerId, card.Id);
        FireMiddayShadowAssigned(card);
        FireVeiledMenaceAssigned(card);
        P4Assigned(card); // The Menacing Sky (issue #503)
        if (attacking) FireConsecrationWatches(card, battlefield);
        if (attacking) FireAssignedToAttackWatches(card);
        return true;
    }

    // ---- fighting a battle inside the Action Phase ----------------------------------------------------------------------------------------------------------------------

    /// <summary>Begins the battle an Action Phase action fights at a created battlefield: the Attacker is named, the action is kept whole and finishes when the battle is over. `firstOpportunity` is the
    /// player an effect grants the first opportunity to take a Battle action or pass (a claim like any other: the Defender's own override wins), or null for the default (the Defender).</summary>
    private void BeginAsideBattle(int battlefield, int attackerId, GameCommand command, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants, int? firstOpportunity, Action assign, int loseHonorAfter = 0)
    {
        R1.Aside = new AsideBattle
        {
            TurnPlayerId = _state.ActivePlayerId, Battlefield = battlefield, AttackerId = attackerId, LoseHonorAfter = loseHonorAfter,
            Paused = new() { Command = command, PlayerId = command.PlayerId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
                OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList() }
        };
        _state.NegatedEffects.Clear();
        _state.ActivePlayerId = attackerId;
        _state.UnresolvedBattlefields = [battlefield];
        // The battle is fought inside the phase, not as a phase of its own: nothing that ends or begins a phase fires (SetPhase is bypassed).
        _state.Phase = Phase.Battle; _state.ConsecutivePasses = 0;
        assign(); // the units are assigned as the Attacker and the Defender they now are
        EngageBattle(battlefield, firstOpportunity);
    }

    /// <summary>The battle fought inside the Action Phase is over (CompleteBattleTransition): everything at its battlefield goes home (the Attacker's bowed, the Defender's not), the battlefield ceases to exist, the
    /// Action Phase resumes with its turn player and the action that fought it completes.</summary>
    private void FinishAsideBattle(int attacker)
    {
        var aside = R1.Aside!;
        foreach (var card in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == aside.Battlefield).ToArray()) ReturnHome(card, card.OwnerId == attacker);
        R1.Aside = null;
        ClearAdditionalBattlefields();
        _state.UnresolvedBattlefields = []; _state.CurrentBattleProvince = null;
        _state.ActivePlayerId = aside.TurnPlayerId;
        _state.Phase = Phase.Action; _state.ConsecutivePasses = 0; _state.PriorityPlayerId = aside.TurnPlayerId;
        Emit("phaseChanged", $"Phase: {Phase.Action}.");
        var paused = aside.Paused;
        _state.NegatedEffects.Clear();
        if (aside.LoseHonorAfter > 0) GainHonor(paused.PlayerId, -aside.LoseHonorAfter, "Fought battle", paused.Command.CardId is null ? null : Card(paused.Command.CardId));
        CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore,
            paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
    }

    /// <summary>Test seam (L5R.Core.Tests has InternalsVisibleTo): the additional battlefields and the province each is at.</summary>
    internal (int Battlefield, int? Province)[] BattlefieldsForTest() => R1.Additional.Keys.Order().Select(b => (b, BattlefieldProvinceIndex(b))).ToArray();
    internal int[] AdjacentBattlefieldsForTest(int battlefield) => AdjacentBattlefields(battlefield).ToArray();
    internal int CreateAdditionalBattlefieldForTest(int? associatedProvince) => CreateAdditionalBattlefield(associatedProvince);
    internal void MarkTwoFrontsForTest(int attackerId, int province) => MarkTwoFronts(attackerId, province);
}
