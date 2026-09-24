namespace L5R.Core;

/// <summary>Which card's decision is open, and so who decides, among what, and what the choice does.</summary>
public static class DeciderSpecs
{
    /// <summary>Know Their Minds: "Choose a player. He chooses a face-up Personality in one of his provinces. Turn the Personality face-down."</summary>
    public const string KnowTheirMinds = "know-their-minds";
    /// <summary>Scandalous Rumors: "The Attacker may target any of his Personalities at the current battlefield and move them home. He loses 1 Honor for each that this did not move, then dishonors them."</summary>
    public const string ScandalousRumors = "scandalous-rumors";
    /// <summary>Dragon's Breath Castle: "The Attacker targets and bows one of his Personalities there."</summary>
    public const string DragonsBreath = "dragons-breath-bow";
    /// <summary>Yoritomo Tarao: "Choose a player. He targets one of his unbowed Personalities or Holdings or a card in his hand. If the targeted card is in his hand, discard it. Otherwise, bow it."</summary>
    public const string TaraoSacrifice = "tarao-sacrifice";
    /// <summary>Isawa Kimi - exp 2 (issue #477): "That player chooses and discards a card from his hand." The decider is the other player, who was just given a card.</summary>
    public const string KimiDiscard = "kimi-discard";
}

/// <summary>The decision another player makes inside an action (MatchState.PendingDeciderChoice). Paused is the action that opened it, kept whole so it finishes exactly as it would have.</summary>
public sealed class PendingDeciderChoice
{
    public string SpecId { get; set; } = "";
    public string SourceCardId { get; set; } = "";
    public int ActingPlayerId { get; set; }
    public int DeciderPlayerId { get; set; }
    /// <summary>The cards the decider may choose among (all of them public: face-up province cards, units at a battlefield).</summary>
    public List<string> Candidates { get; set; } = [];
    public int Min { get; set; }
    public int Max { get; set; }
    public List<string> Chosen { get; set; } = [];
    public PendingEffectChoice Paused { get; set; } = new();
}

// "Choose a player who chooses" (issue #419, Mendon's rulings of 2026-09-21): an action whose effect is a choice made by a player other than the one who took it (Know Their Minds: a
// player of the acting player's choosing; Scandalous Rumors and Dragon's Breath Castle: the Attacker, who may be either player). The action stops when it comes to that choice and hands
// it to the deciding player; the engine offers exactly the legal picks, and when the choice is made the action carries on and finishes as any action does.
//
//   * the opposing player's chance to REACT to the action (to negate it) is the before-action window every action already has: it comes before the effect resolves, so by the time the
//     choice is asked the action stands. A whole-action negation never reaches here, and a negation of a card's movement or dishonoring still applies to the effect that follows;
//   * the decider holds priority and answers with "chooseDecider": a card (TargetId) to add to what he has chosen, or Choice "done". A choice of exactly one card ends when it is made; a
//     "may" choice of any number ends on "done" (zero chosen is a legal answer);
//   * with nothing to choose among, or only the one card a mandatory choice needs, nothing is asked: it is announced in the log and the effect applies;
//   * everything about the choice is public: who decides, among which cards, and what he chose.
public sealed partial class MatchEngine
{
    private string DeciderPrompt(PendingDeciderChoice p) => p.SpecId switch
    {
        DeciderSpecs.KnowTheirMinds => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose a face-up Personality in one of your provinces. It is turned face-down.",
        DeciderSpecs.ScandalousRumors => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose any of your Personalities at this battlefield to move home. You lose 1 Honor for each that stays, and dishonor them.",
        DeciderSpecs.DragonsBreath => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your Personalities at this battlefield. It is bowed.",
        DeciderSpecs.TaraoSacrifice => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your unbowed Personalities or Holdings, or a card in your hand. A card in your hand is discarded; any other is bowed.",
        DeciderSpecs.KimiDiscard => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose a card from your hand. It is discarded.",
        _ => throw new RuleViolation("Unknown choice.")
    };

    // The cards the decider may choose among, and how many, for each spec.
    private (List<string> Candidates, int Min, int Max) DeciderOptions(string specId, int deciderId, GameCommand command)
    {
        switch (specId)
        {
            case DeciderSpecs.KnowTheirMinds:
                return (Player(deciderId).Provinces.Where(p => !p.Destroyed && p.CardId is not null && Card(p.CardId) is { FaceUp: true } c && Definition(c).Type == CardType.Personality)
                    .Select(p => p.CardId!).Order(StringComparer.Ordinal).ToList(), 1, 1);
            case DeciderSpecs.ScandalousRumors:
            {
                var here = _state.CurrentBattleProvince is int province ? Army(deciderId, province).Where(c => Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [];
                return (here, 0, here.Count);
            }
            case DeciderSpecs.TaraoSacrifice:
                return (_state.Cards.Values.Where(c => c.OwnerId == deciderId && (InPlay(c) && !c.Bowed && Definition(c).Type is CardType.Personality or CardType.Holding || c.Zone == Zone.Hand && Player(deciderId).Hand.Contains(c.Id)))
                    .Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);
            case DeciderSpecs.KimiDiscard:
                return (Player(deciderId).Hand.OrderBy(id => id, StringComparer.Ordinal).ToList(), 1, 1);
            case DeciderSpecs.DragonsBreath:
                return (_state.CurrentBattleProvince is int battle ? Army(deciderId, battle).Where(c => Definition(c).Type == CardType.Personality && !c.Bowed).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [], 1, 1);
            default: throw new RuleViolation("Unknown choice.");
        }
    }

    /// <summary>Called from the ability's execution case: hands the choice to its decider. Returns true when it is now open (the caller returns without finishing the action) and false when
    /// there was nothing to ask, in which case the effect has already applied and the caller finishes the action itself.</summary>
    private bool BeginDeciderChoice(string specId, GameCommand command, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore,
        HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var source = Card(command.CardId);
        var deciderId = specId is DeciderSpecs.KnowTheirMinds or DeciderSpecs.TaraoSacrifice ? command.TargetPlayerId ?? command.PlayerId
            : specId == DeciderSpecs.KimiDiscard ? 1 - command.PlayerId : _state.ActivePlayerId;
        var (candidates, min, max) = DeciderOptions(specId, deciderId, command);
        var pending = new PendingDeciderChoice
        {
            SpecId = specId, SourceCardId = source.Id, ActingPlayerId = command.PlayerId, DeciderPlayerId = deciderId, Candidates = candidates, Min = min, Max = max,
            Paused = new()
            {
                Command = command, PlayerId = command.PlayerId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
                OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList()
            }
        };
        // The played Strategy is spent as it is played: it is in the discard pile, in the open, while the other player decides.
        SpendHandPlayedCard(Player(command.PlayerId), source);
        if (candidates.Count == 0 || min == max && candidates.Count <= min)
        {
            Emit("deciderAutoResolved", candidates.Count == 0 ? $"{Definition(source).Name}: {Player(deciderId).Name} has nothing to choose." : $"{Definition(source).Name}: {Player(deciderId).Name} has one choice.", deciderId, source.Id);
            pending.Chosen = candidates.Take(max).ToList();
            ApplyDeciderChoice(pending);
            return false;
        }
        _state.PendingDeciderChoice = pending;
        SuspendOpenWindow();
        _state.PendingChoice = new("deciderChoice", deciderId, source.Id, DeciderPrompt(pending), command.PlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        Emit("deciderChoiceOffered", $"{Player(deciderId).Name} must choose ({Definition(source).Name}).", deciderId, source.Id);
        return true;
    }

    private void ResolveDeciderChoiceCommand(GameCommand command)
    {
        var pending = _state.PendingDeciderChoice ?? throw new RuleViolation("No choice is pending.");
        Require(command.Type == "chooseDecider", "Make the choice.");
        Require(command.PlayerId == pending.DeciderPlayerId, "Only the player who decides may answer.");
        if (command.Choice == "done")
            Require(pending.Chosen.Count >= pending.Min, $"Choose at least {pending.Min}.");
        else
        {
            Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId) && !pending.Chosen.Contains(command.TargetId), "Choose one of the offered cards.");
            Require(pending.Chosen.Count < pending.Max, $"Choose at most {pending.Max}.");
            pending.Chosen.Add(command.TargetId!);
            Emit("deciderChosen", $"{Player(pending.DeciderPlayerId).Name} chose {Definition(Card(command.TargetId)).Name}.", pending.DeciderPlayerId, command.TargetId);
            // A choice of exactly one card, or of everything there is, is complete as it is made.
            if (pending.Max == 1 || pending.Chosen.Count == pending.Candidates.Count) command = command with { Choice = "done" };
        }
        if (command.Choice != "done") return;
        _state.PendingChoice = null; _state.PendingDeciderChoice = null;
        ApplyDeciderChoice(pending);
        var paused = pending.Paused;
        CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore,
            paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
    }

    // What the choice does. The negations a Reaction set for the action are restored while it applies, so a negated movement or bowing stays negated.
    private void ApplyDeciderChoice(PendingDeciderChoice p)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        _state.NegatedEffects = p.Paused.Negated.ToList();
        switch (p.SpecId)
        {
            case DeciderSpecs.KnowTheirMinds:
                foreach (var id in p.Chosen)
                {
                    var card = Card(id); card.FaceUp = false;
                    Emit("provinceTurnedFaceDown", $"{name}: {Definition(card).Name} was turned face-down in {Player(card.OwnerId).Name}'s province {card.ProvinceIndex + 1}.", card.OwnerId, id);
                }
                break;
            case DeciderSpecs.ScandalousRumors:
            {
                foreach (var id in p.Chosen) MoveHome(Card(id), false);
                var stayed = p.Candidates.Select(Card).Where(c => c.Zone == Zone.Battlefield).ToArray();
                if (stayed.Length > 0)
                {
                    GainHonor(p.DeciderPlayerId, -stayed.Length, name, source);
                    foreach (var card in stayed) Dishonor(card, p.ActingPlayerId);
                }
                break;
            }
            case DeciderSpecs.DragonsBreath:
                foreach (var id in p.Chosen) { var card = Card(id); if (!StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true; }
                break;
            case DeciderSpecs.KimiDiscard:
                foreach (var id in p.Chosen) { var card = Card(id); if (card.Zone == Zone.Hand) DiscardFate(Player(card.OwnerId), card); }
                break;
            case DeciderSpecs.TaraoSacrifice:
                foreach (var id in p.Chosen)
                {
                    var card = Card(id);
                    if (card.Zone == Zone.Hand) DiscardFate(Player(card.OwnerId), card);
                    else if (!StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
                }
                break;
        }
        _state.NegatedEffects.Clear();
    }

    // Dragon's Breath Castle: "After another player's Dynasty Phase begins, if he controls any Personalities and he assigned none of them to an attacking army this turn: Gain 2 Honor." A trait,
    // so it happens on its own, for each Castle its controller has in play, at the start of every Dynasty Phase that is not its controller's.
    private void DragonsBreathHonor()
    {
        var active = _state.ActivePlayerId;
        var personalities = _state.Cards.Values.Where(c => c.OwnerId == active && InPlay(c) && Definition(c).Type == CardType.Personality).ToArray();
        if (personalities.Length == 0 || personalities.Any(c => c.AssignedToAttackTurn == _state.TurnNumber)) return;
        foreach (var castle in _state.Cards.Values.Where(c => c.OwnerId != active && InPlay(c) && Definition(c).Effect == "dbc-honor").ToArray())
            GainHonor(castle.OwnerId, 2, Definition(castle).Name, castle);
    }

    // The decider's legal actions: one pick per remaining candidate, and "done" when what he has chosen is enough.
    private IEnumerable<ActionOption> DeciderChoiceActions(int playerId)
    {
        if (_state.PendingDeciderChoice is not { } p || p.DeciderPlayerId != playerId) yield break;
        var verb = p.SpecId switch { DeciderSpecs.ScandalousRumors => "Move home", DeciderSpecs.DragonsBreath => "Bow", DeciderSpecs.TaraoSacrifice => "Sacrifice", DeciderSpecs.KimiDiscard => "Discard", _ => "Turn face-down" };
        if (p.Chosen.Count < p.Max)
            foreach (var id in p.Candidates.Where(c => !p.Chosen.Contains(c)))
                yield return new("chooseDecider", $"{verb} · {Definition(Card(id)).Name}", true, null,
                    new GameCommand("chooseDecider", playerId, _state.Version, CardId: p.SourceCardId, TargetId: id), id);
        if (p.Chosen.Count >= p.Min && p.Max > 1)
            yield return new("chooseDecider", p.Chosen.Count == 0 ? "Choose none" : $"Done ({p.Chosen.Count} chosen)", true, null,
                new GameCommand("chooseDecider", playerId, _state.Version, CardId: p.SourceCardId, Choice: "done"), p.SourceCardId);
    }
}
