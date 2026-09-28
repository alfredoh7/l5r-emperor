namespace L5R.Core;

// Issue #503, pass group P2: Shosuro Keirei - exp (oracle-l5r-9845, The Shadow's Embrace; the scan): "Battle: Move Keirei from the current battlefield to a different battlefield. You may target and give a
// Personality at Keirei's new or previous location two -1F/-1C Poison tokens."
//   * A MOVE TO ANY OTHER BATTLEFIELD (MoveSpec.AnyOther): every battlefield of the Attack Phase but the current one -- the unresolved ones and those already fought (recorded as each battle engages,
//     P2.FoughtBattlefields, for the Attack Phase under way: its turn and its Attacker) -- chosen with the action (GameCommand.Province), as every chosen destination is.
//   * A TARGET CHOSEN AFTER THE MOVE ("You may target"): the targeting is in the effects, so it is made as the action resolves, once Keirei has moved (or not), among the Personalities (either
//     player's) at his new or previous location: a DeciderChoice of the acting player's (spec KeireiPoisonSpec), zero or one card.
//   * A TOKEN WITH TWO STATS (CardToken.Stat2/Amount2, read by StatModifier): each Poison token is one -1F/-1C token. Its penalties are negated stat by stat (Tireless Efforts' Force penalties, Gift
//     Armor's Chi penalties, Creating Order, Aka-beko): a negated stat is left off the token, and a token with both negated is not given; a Chi that reaches 0 destroys the Personality.
public sealed partial class MatchEngine
{
    private string P2PhaseKey() => $"{_state.TurnNumber}|{_state.ActivePlayerId}";

    // EngageBattle: this Attack Phase's battlefield that is now fought.
    private void NoteP2BattlefieldFought(int battlefield)
    {
        if (P2.FoughtPhaseKey != P2PhaseKey()) { P2.FoughtPhaseKey = P2PhaseKey(); P2.FoughtBattlefields = []; }
        if (!P2.FoughtBattlefields.Contains(battlefield)) P2.FoughtBattlefields.Add(battlefield);
    }

    /// <summary>The battlefields of the Attack Phase under way other than <paramref name="current"/>: the unresolved ones and the ones already fought.</summary>
    private int[] P2OtherBattlefields(int current) =>
        _state.UnresolvedBattlefields.Concat(P2.FoughtPhaseKey == P2PhaseKey() ? P2.FoughtBattlefields : []).Where(b => b != current).Distinct().Order().ToArray();

    // Keirei's move, then the optional target: true when the choice is open (the action waits for it).
    private bool KeireiMoves(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance keirei, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore,
        HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var previous = keirei.ProvinceIndex;
        ExecuteMove(command, ability, keirei, null);
        P2.KeireiLocations[keirei.Id] = [.. new[] { previous, keirei.Zone == Zone.Battlefield ? keirei.ProvinceIndex : null }.OfType<int>().Distinct()];
        return BeginDeciderChoice(EmperorP2Abilities.KeireiPoisonSpec, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    private (List<string> Candidates, int Min, int Max) KeireiDeciderOptions(GameCommand command)
    {
        var locations = command.CardId is { } keireiId && P2.KeireiLocations.TryGetValue(keireiId, out var at) ? at : [];
        var candidates = _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && c.Zone == Zone.Battlefield && c.ProvinceIndex is int b && locations.Contains(b))
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        return (candidates, 0, 1);
    }

    private string KeireiDeciderPrompt(PendingDeciderChoice p) =>
        $"{Definition(Card(p.SourceCardId)).Name}: you may target a Personality at his new or previous location and give him two -1F/-1C Poison tokens.";

    private void ApplyKeireiPoison(PendingDeciderChoice p)
    {
        foreach (var id in p.Chosen)
        {
            var target = Card(id);
            for (var i = 0; i < 2 && InPlay(target); i++) CreateTwoStatToken(target, "Poison", p.SourceCardId, "Force", -1, "Chi", -1);
        }
        if (p.Chosen.Count == 0) Emit("keireiNoTarget", $"{Definition(Card(p.SourceCardId)).Name}: no Personality was targeted.", p.ActingPlayerId, p.SourceCardId);
    }

    // A token with two stats, each negated on its own (the checks CreateToken makes for a one-stat token); a Chi reaching 0 destroys the Personality.
    private void CreateTwoStatToken(CardInstance card, string type, string source, string stat, int amount, string stat2, int amount2)
    {
        if (NegatesEffect(card, "all")) { Emit("tokenNegated", $"A {type} token on {Definition(card).Name} was negated before it resolved.", card.OwnerId, card.Id); return; }
        bool Negated(string s, int a) =>
            s == "Force" && a < 0 && card.ForcePenaltiesNegatedTurn == _state.TurnNumber
            || !CurrentActionUnnegatable() && (ActionForceModifierNegated(card, s, a) || GiftArmorNegatesChiPenalty(card, s, a) || AkaBekoNegates(card, s, a, source));
        var first = Negated(stat, amount) ? null : stat;
        var second = Negated(stat2, amount2) ? null : stat2;
        if (first is null && second is null) { Emit("tokenNegated", $"A {type} token's penalties on {Definition(card).Name} were negated.", card.OwnerId, card.Id); return; }
        // The shared delay mechanism records a token by one stat: a delayed two-stat token is recorded as its stats' two tokens.
        if (first is not null && Delay(card, DelayedKinds.Token, stat: first, amount: amount, text: type, sourceId: source)) first = null;
        if (second is not null && Delay(card, DelayedKinds.Token, stat: second, amount: amount2, text: type, sourceId: source)) second = null;
        if (first is null && second is null) return;
        var token = first is not null
            ? new CardToken($"token-{++_state.TokenSequence:D4}", type, source, first, amount, second, second is null ? 0 : amount2)
            : new CardToken($"token-{++_state.TokenSequence:D4}", type, source, second, amount2);
        card.Tokens.Add(token);
        Emit("tokenCreated", $"{Definition(card).Name} gained a {type} token ({string.Join("/", new[] { first is null ? null : $"{amount:+#;-#;0}{first[0]}", second is null ? null : $"{amount2:+#;-#;0}{second[0]}" }.OfType<string>())}).", card.OwnerId, card.Id);
        if (first is not null) NoteStatPenalty(card, source, type, first, amount);
        if (second is not null) NoteStatPenalty(card, source, type, second, amount2);
        if ((first == "Chi" || second == "Chi") && Definition(card).Type == CardType.Personality && InPlay(card) && PersonalityChi(card) <= 0) DestroyUnits([card], true);
    }
}

public sealed partial class MatchEngine
{
    internal void PurgeChiPenaltiesForTest(string cardId) => PurgeChiPenalties(Card(cardId));
    internal void CleanseForcePenaltiesForTest(string cardId) => CleanseForcePenaltiesAndLocks(Card(cardId));
}

public sealed partial class MatchEngine
{
    private bool RegisteredApplyKeireiPoison(PendingDeciderChoice p)
    {
        if (!(p.SpecId == EmperorP2Abilities.KeireiPoisonSpec)) return false;
        ApplyKeireiPoison(p);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP2Keirei_Rung4b6()
    {
        EngineHooks.DeciderPrompt.Register("KeireiDeciderPrompt", 300, static (engine, p) => p.SpecId == EmperorP2Abilities.KeireiPoisonSpec ? engine.KeireiDeciderPrompt(p) : null);
        EngineHooks.DeciderOptions.Register("KeireiDeciderOptions", 300, static (engine, specId, deciderId, command) => specId == EmperorP2Abilities.KeireiPoisonSpec ? engine.KeireiDeciderOptions(command) : null);
        EngineHooks.ApplyDecider.Register("ApplyKeireiPoison", 300, static (engine, p) => engine.RegisteredApplyKeireiPoison(p));
        EngineHooks.DeciderVerb.Register("KeireiDeciderPrompt", 200, static (engine, specId) => specId == EmperorP2Abilities.KeireiPoisonSpec ? "Poison" : null);
        EngineHooks.DeciderIdAfter.Register("KeireiDeciderOptions", 200, static (engine, specId, command) => specId == EmperorP2Abilities.KeireiPoisonSpec ? command.PlayerId : null);
    }
}
