namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION FOLLOW-UPS (issue #498): REND THE SOUL (oracle-l5r-6260) -- both halves, and the two general primitives they needed.
//
//   * A FOCUS EFFECT THAT ADJUSTS A FOCUSED CARD'S FOCUS VALUE for the duel's totals (DuelFocusValues). A duel's totals are each side's duel stat plus the Focus Values of its focused cards; a Focus Effect that
//     changes a Focus Value resolves before the totals, in the order the active player chose for the duel's Focus Effects (PendingDuel.OrderedFocusEffects). Two kinds: ADD to cards (Duel to First Blood:
//     "+1 Focus Value to each of your focused cards", which used to be a side total of its own) and SET a card's value (Rend the Soul: "Raise this card's Focus Value to 5" -- Mendon's ruling: a set-to,
//     not an add). Applied in order, so a First Blood resolved after Rend the Soul still adds its +1 to the Rend the Soul (5 + 1), one resolved before it does not (set to 5).
//   * A DUEL OUTCOME KIND "THE WINNER LOSES TOO" (the duel becomes a tie: no winner, both Personalities lose and each takes the loser's consequences): Rend the Soul's "If your Personality wins this duel, he
//     loses instead (the other Personality still also loses)". Applied in ResolveDuel the moment the winner is known (after the totals and the Duelist tie-break), BEFORE the R2 "delay the effects of losing"
//     questions and Honor Never Falls (which now see both losers), before the duel's own outcome (applied to both losers) and before the other Focus Effects (which see no winner). If his Personality loses,
//     nothing changes.
//   * THE BATTLE HALF: "Choose your performing opposed Personality: Give him +5F. If the army he is in during this battle's resolution wins, this battle is tied instead. You will not destroy the province in
//     this battle's resolution." A per-battle mark (MatchState.RendTheSoulMarks, keyed by the battle's number, so it lapses with the battle like Nerve Strike's lock) read in ContinueResolveBattle right after
//     the totals are compared: if a marked Personality is in the army that would win, the battle resolves as the existing tie (each side destroys the other army, the tie's Honor; with an empty side, a tie at
//     zero has no outcome at all); and the marking player destroys no province in this battle's resolution.
// ======================================================================================================================================================================================

/// <summary>Rend the Soul's Battle half: <see cref="PlayerId"/> marked <see cref="PersonalityId"/> in the battle numbered <see cref="Battle"/>.</summary>
public sealed class RendTheSoulMark
{
    public int PlayerId { get; set; }
    public string PersonalityId { get; set; } = "";
    public int Battle { get; set; }
    public string SourceCardId { get; set; } = "";
}

public sealed partial class MatchEngine
{
    // ---- the Focus Value primitive ----

    /// <summary>Each focused card's Focus Value for the duel's totals: its printed Focus, then every Focus Effect that adjusts Focus Values, in the duel's Focus Effect order.</summary>
    private Dictionary<string, int> DuelFocusValues(PendingDuel duel)
    {
        var values = duel.ChallengerFocused.Concat(duel.ChallengedFocused).ToDictionary(id => id, id => Definition(Card(id)).Focus, StringComparer.Ordinal);
        foreach (var focusedId in duel.OrderedFocusEffects)
        {
            if (!values.ContainsKey(focusedId)) continue;
            switch (FocusEffectAbilityOf(focusedId))
            {
                // Duel to First Blood: "Give +1 Focus Value to each of your focused cards."
                case "first-blood-focus":
                    foreach (var id in duel.ChallengerFocused.Contains(focusedId) ? duel.ChallengerFocused : duel.ChallengedFocused) values[id] += 1;
                    break;
                // Rend the Soul: "Raise this card's Focus Value to 5" (set to 5).
                case EmperorFollowupsAbilities.RendFocus:
                    values[focusedId] = 5;
                    break;
            }
        }
        return values;
    }

    /// <summary>The sum of one side's focused cards' Focus Values (DuelFocusValues).</summary>
    private int FocusedTotal(PendingDuel duel, List<string> focused)
    {
        var values = DuelFocusValues(duel);
        return focused.Sum(id => values.TryGetValue(id, out var value) ? value : Definition(Card(id)).Focus);
    }

    // ---- "If your Personality wins this duel, he loses instead (the other Personality still also loses)" ----

    /// <summary>The Rend the Soul among the duel's Focus Effects whose owner is the winner's controller, or null.</summary>
    private CardInstance? RendTheSoulOfWinner(PendingDuel duel, string winnerId)
    {
        var winnerOwner = Card(winnerId).OwnerId;
        return duel.OrderedFocusEffects.Select(Card).FirstOrDefault(c => c.OwnerId == winnerOwner && FocusEffectAbilityOf(c.Id) == EmperorFollowupsAbilities.RendFocus);
    }

    // ---- the Battle half ----

    private void RendTheSoulBattle(GameCommand command, PlayerState player, CardInstance source)
    {
        var him = Card(command.PerformerId!);
        ApplyModifier(him, source.Id, EmperorFollowupsAbilities.RendBattle, "Force", 5);
        _state.RendTheSoulMarks.RemoveAll(m => m.Battle != _state.BattleNumber); // a mark lapses with its battle
        _state.RendTheSoulMarks.Add(new RendTheSoulMark { PlayerId = player.Id, PersonalityId = him.Id, Battle = _state.BattleNumber, SourceCardId = source.Id });
        Emit("rendTheSoulMarked", $"{Definition(source).Name}: if the army {Definition(him).Name} is in wins this battle's resolution, the battle is tied instead; {player.Name} will not destroy the province in it.", player.Id, him.Id);
    }

    private IEnumerable<RendTheSoulMark> CurrentRendMarks() => _state.RendTheSoulMarks.Where(m => m.Battle == _state.BattleNumber);

    /// <summary>ContinueResolveBattle's hook, right after the totals are compared: true when a marked Personality is in the army that would win, so the battle is tied instead.</summary>
    private bool RendTheSoulTiesBattle(CardInstance[] attackers, CardInstance[] defenders, bool attackerWins, bool defenderWins)
    {
        if (!attackerWins && !defenderWins) return false;
        var winning = attackerWins ? attackers : defenders;
        var mark = CurrentRendMarks().FirstOrDefault(m => winning.Any(c => c.Id == m.PersonalityId));
        if (mark is null) return false;
        var marked = Card(mark.PersonalityId);
        Emit("battleTiedInstead", $"Rend the Soul: the army {Definition(marked).Name} is in would win this battle's resolution, so the battle is tied instead.", mark.PlayerId, marked.Id);
        return true;
    }

    /// <summary>"You will not destroy the province in this battle's resolution": true when <paramref name="attacker"/> marked a Personality with Rend the Soul in this battle.</summary>
    private bool RendBarsProvinceDestruction(int attacker)
    {
        if (!CurrentRendMarks().Any(m => m.PlayerId == attacker)) return false;
        Emit("provinceDestructionBarred", $"Rend the Soul: {Player(attacker).Name} will not destroy the province in this battle's resolution.", attacker);
        return true;
    }
}
