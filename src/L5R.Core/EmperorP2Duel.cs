namespace L5R.Core;

// Issue #503, pass group P2: the duel cards (EmperorP2.cs has the group's header).
//   * A FOCUS REPLACED BY A DUEL STAT BONUS (Kakita Gonji: "Before you focus: Once per duel, instead of focusing that time, you may give your Crane Clan Personality in the duel +4 to his duel stat until
//     the duel ends."): offered as a Focus command with Choice GonjiFocusChoice; the bonus is the duel's own (PendingDuel.StatBonuses, Reserve Weapon's). Unlike Reserve Weapon's older reading it counts
//     against the four-focus cap, as the core rules say of "traits/actions that substitute 'instead of focusing'" (P2State.FocusSubstitutions).
public sealed partial class MatchEngine
{
    public const string GonjiFocusChoice = "p2-gonji";

    private string FocusSubstitutionKey(int playerId) => $"{_state.PendingDuel?.DuelId}|{playerId}";

    /// <summary>How many focuses <paramref name="playerId"/> replaced this duel (counted against the cap of four).</summary>
    private int P2FocusSubstitutions(int playerId) => _state.PendingDuel is null ? 0 : P2.FocusSubstitutions.GetValueOrDefault(FocusSubstitutionKey(playerId));

    private CardInstance DuellistOf(PendingDuel duel, int playerId) => Card(playerId == duel.ChallengerPlayerId ? duel.ChallengerId : duel.ChallengedId);

    // Every Kakita Gonji of the focusing player's (in play, bowed or not: a trait) not yet used this duel, when his Personality in the duel is Crane Clan.
    private IEnumerable<CardInstance> GonjiFocusSources(int playerId, PendingDuel duel)
    {
        if (!HasClanAffiliation(DuellistOf(duel, playerId), "Crane")) return [];
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == EmperorP2Abilities.GonjiEffect && !P2.GonjiUses.Contains($"{duel.DuelId}|{c.Id}"))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    }

    private void GonjiFocus(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var list = command.PlayerId == duel.ChallengerPlayerId ? duel.ChallengerFocused : duel.ChallengedFocused;
        Require(list.Count + P2FocusSubstitutions(command.PlayerId) < 4, "You have already focused four times this duel and must Strike.");
        var gonji = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var chosen) ? chosen : null;
        Require(gonji is not null && GonjiFocusSources(command.PlayerId, duel).Any(g => g.Id == gonji.Id), "Choose your Kakita Gonji (once per duel, for your Crane Clan Personality in the duel).");
        var duellist = DuellistOf(duel, command.PlayerId);
        duel.StatBonuses[duellist.Id] = duel.StatBonuses.GetValueOrDefault(duellist.Id) + 4;
        P2.GonjiUses.Add($"{duel.DuelId}|{gonji!.Id}");
        P2.FocusSubstitutions[FocusSubstitutionKey(command.PlayerId)] = P2FocusSubstitutions(command.PlayerId) + 1;
        Emit("duelGonji", $"{Player(command.PlayerId).Name} gave {Definition(duellist).Name} +4 to his duel stat instead of focusing ({Definition(gonji).Name}).", command.PlayerId, gonji.Id);
        duel.TurnPlayerId = 1 - command.PlayerId;
        _state.PriorityPlayerId = duel.TurnPlayerId;
    }

    // ---- Focus Effects before the totals -------------------------------------------------------------------------------------------------------------------------------------------------------------

    public const string P2FocusChoiceStage = "P2FocusChoice";

    private static bool IsP2PreTotalFocusEffect(string abilityId) => abilityId is EmperorP2Abilities.KillingIntentFocus or EmperorP2Abilities.ChampionFocus or EmperorP2Abilities.TurningAwayFocus;

    private string? P2PreTotalFocusAbility(string focusedId) => (Definition(Card(focusedId)).Abilities ?? []).FirstOrDefault(IsP2PreTotalFocusEffect);

    // Asks the next pre-total Focus Effect's owner (from the ordered index `from`); false when there is none left and the duel may be totalled.
    private bool BeginP2PreTotalFocusEffects(int from)
    {
        var duel = _state.PendingDuel!;
        for (var i = from; i < duel.OrderedFocusEffects.Count; i++)
        {
            var focusedId = duel.OrderedFocusEffects[i];
            if (P2PreTotalFocusAbility(focusedId) is not { } abilityId) continue;
            var owner = Card(focusedId).OwnerId;
            if (!P2PreTotalFocusOptions(abilityId, owner, duel).Any()) { Emit("focusEffectNothing", $"{Definition(Card(focusedId)).Name}: nothing to choose.", owner, focusedId); continue; }
            P2.PreFocusIndex = i;
            duel.Stage = P2FocusChoiceStage;
            _state.PriorityPlayerId = owner;
            _state.ConsecutivePasses = 0;
            Emit("focusEffectChoice", $"{Definition(Card(focusedId)).Name}: {Player(owner).Name} may use its Focus Effect.", owner, focusedId);
            return true;
        }
        return false;
    }

    // The "yes" answers a pre-total Focus Effect offers its owner now: a TargetId (a Yojimbo) or null.
    private IEnumerable<string?> P2PreTotalFocusOptions(string abilityId, int owner, PendingDuel duel)
    {
        var mine = DuellistOf(duel, owner);
        switch (abilityId)
        {
            case EmperorP2Abilities.KillingIntentFocus:
                if (InPlay(mine) && mine.Zone == Zone.Battlefield) yield return null;
                break;
            case EmperorP2Abilities.ChampionFocus:
                foreach (var yojimbo in _state.Cards.Values.Where(c => c.OwnerId == owner && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Yojimbo")
                    && c.Id != duel.ChallengerId && c.Id != duel.ChallengedId).OrderBy(c => c.Id, StringComparer.Ordinal))
                    yield return yojimbo.Id;
                break;
            case EmperorP2Abilities.TurningAwayFocus:
                if (InPlay(mine)) yield return null;
                break;
        }
    }

    private IEnumerable<(GameCommand Command, string Label)> P2FocusChoiceActions(int playerId)
    {
        var duel = _state.PendingDuel;
        if (duel is null || duel.Stage != P2FocusChoiceStage || P2.PreFocusIndex >= duel.OrderedFocusEffects.Count) yield break;
        var focusedId = duel.OrderedFocusEffects[P2.PreFocusIndex];
        if (Card(focusedId).OwnerId != playerId || P2PreTotalFocusAbility(focusedId) is not { } abilityId) yield break;
        var name = Definition(Card(focusedId)).Name;
        foreach (var option in P2PreTotalFocusOptions(abilityId, playerId, duel))
            yield return (new GameCommand("p2FocusChoice", playerId, _state.Version, CardId: focusedId, TargetId: option, Choice: "yes"),
                abilityId switch
                {
                    EmperorP2Abilities.KillingIntentFocus => $"{name} · use his base Force as his duel stat",
                    EmperorP2Abilities.ChampionFocus => $"{name} · substitute {Definition(Card(option!)).Name} ({option})",
                    _ => $"{name} · discard your Personality (the duel ends without resolution)"
                });
        yield return (new GameCommand("p2FocusChoice", playerId, _state.Version, CardId: focusedId, Choice: "decline"), $"{name} · decline");
    }

    private void ResolveP2FocusChoice(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var index = P2.PreFocusIndex;
        var focusedId = duel.OrderedFocusEffects[index];
        var owner = Card(focusedId).OwnerId;
        Require(command.Type == "p2FocusChoice" && command.PlayerId == owner, "Answer the Focus Effect first.");
        Require(command.Choice is "yes" or "decline", "Choose whether to use the Focus Effect.");
        var abilityId = P2PreTotalFocusAbility(focusedId)!;
        if (command.Choice == "yes")
        {
            var options = P2PreTotalFocusOptions(abilityId, owner, duel).ToArray();
            Require(options.Contains(command.TargetId), "That choice is not offered.");
            var mine = DuellistOf(duel, owner);
            var name = Definition(Card(focusedId)).Name;
            var wasInFocusEffect = _resolvingFocusEffect; _resolvingFocusEffect = true; // a Focus Effect is not an action (Mendon, R4)
            try
            {
                switch (abilityId)
                {
                    case EmperorP2Abilities.KillingIntentFocus:
                        P2.DuelStatBaseForce.Add($"{duel.DuelId}|{mine.Id}");
                        Emit("duelStatBaseForce", $"{name}: {Definition(mine).Name} uses his base Force ({Definition(mine).Force}) as his duel stat.", owner, mine.Id);
                        break;
                    case EmperorP2Abilities.ChampionFocus:
                    {
                        var yojimbo = Card(command.TargetId);
                        if (duel.ChallengerId == mine.Id) duel.ChallengerId = yojimbo.Id; else duel.ChallengedId = yojimbo.Id;
                        var challenger = Card(duel.ChallengerId); var challenged = Card(duel.ChallengedId);
                        if (_state.LastDuel is { } record)
                            _state.LastDuel = record with { Challenger = ViewCard(challenger), Challenged = ViewCard(challenged),
                                Attachments = Attachments(challenger.Id).Concat(Attachments(challenged.Id)).Select(c => ViewCard(c)).ToArray() };
                        Emit("duelSubstituted", $"{Definition(yojimbo).Name} enters the duel instead of {Definition(mine).Name} ({name}).", owner, yojimbo.Id);
                        break;
                    }
                    case EmperorP2Abilities.TurningAwayFocus:
                        DiscardPersonalityFromPlay(mine, name);
                        EndDuelWithoutResolution(duel, $"{name}: {Definition(mine).Name} was discarded; the duel ends without resolution.");
                        return;
                }
            }
            finally { _resolvingFocusEffect = wasInFocusEffect; }
        }
        else Emit("focusEffectDeclined", $"{Player(owner).Name} declined {Definition(Card(focusedId)).Name}'s Focus Effect.", owner, focusedId);
        duel.Stage = "OrderingEffects";
        if (BeginP2PreTotalFocusEffects(index + 1)) return;
        ResolveDuel();
    }

    // Turning Away: the duel ends here -- no totals, no winner or loser, no outcome, no other Focus Effect; every focused card is spent and the duel's action is over.
    private void EndDuelWithoutResolution(PendingDuel duel, string message)
    {
        Emit("duelEndedWithoutResolution", message, duel.ChallengerPlayerId);
        var start = _state.Events.Count - 1;
        foreach (var id in duel.ChallengerFocused.Concat(duel.ChallengedFocused))
        {
            var card = Card(id);
            if (card.Zone != Zone.Focused) continue;
            if (duel.DiscardFocusedIds.Contains(id)) RemoveFromGame(card); else card.Zone = Zone.FateDiscard;
        }
        if (_state.LastDuel is { } record)
            _state.LastDuel = record with { Result = new("Ended", null, [], 0, 0, 0, 0, duel.OrderedFocusEffects.ToArray(), _state.Events.Skip(start).ToArray(), [], _state.Version + 1, _state.Phase, _state.PriorityPlayerId, "Ended without resolution") };
        var challengerPlayerId = duel.ChallengerPlayerId;
        _state.PendingDuel = null;
        if (FinishActionEffectTracking(duel.AbilityId, challengerPlayerId, duel.SourceCardId, challengerPlayerId, duel.ChallengerId)) return;
        NextAction(challengerPlayerId);
    }

    // Killing Intent: his base Force as his duel stat (DuelStatValue's hook); null when not chosen.
    private int? P2DuelStat(CardInstance card) =>
        _state.PendingDuel is { } duel && P2.DuelStatBaseForce.Contains($"{duel.DuelId}|{card.Id}") ? Definition(card).Force : null;
}
