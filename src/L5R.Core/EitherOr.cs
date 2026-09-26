namespace L5R.Core;

// The opposing player's Either/Or choice (issue #338): a card whose text hands a decision to ANOTHER player -- "the player may pay 2 Gold.
// If he did not, negate the action's effects" (Yoritomo Sakuma), "his controller may choose to bow him. If he did not choose this, he
// must pass..." (Unfortunate Hesitation) -- and only then finishes. One general pending decision serves all of them:
//
//   * the engine builds the OPTIONS (key, label, what taking it costs or does, whether it is legal right now and, if not, why), never
//     the client; there can be two or more (EitherOrOptions);
//   * the decision belongs to the DECIDING player, who holds priority and receives the option list with payment quotes, and is offered
//     one action per option (QueryActions); the OTHER player sees the same decision read-only (who is deciding, what is on offer) and is
//     told to wait. Nothing in it is hidden information: what is offered is fixed by the cards in play, and both players see the result;
//   * an option whose cost cannot be paid is listed disabled with the reason, and when only ONE option is legal there is no decision:
//     it is taken automatically and announced in the log (an "Either/Or" whose "Or" is impossible);
//   * the action that opened it finishes, in the same order it would have, once the decision is answered. Two ways in: "action" (the
//     choice ends an action already played, Unfortunate Hesitation: FinishEitherOr runs the ordinary tail) and "window" (the choice is a
//     Reaction's own effect inside an open reaction window, Sakuma: the window is suspended and restored, then the ordinary "a reaction
//     was played" priority flip follows).
//
// Every reading is in docs/decisions/2026-09-19-copy-and-either-or-primitives.md. The bespoke choices that predate it (shinden, preserving
// honor, Shosuro Rin's shame, Asako Rikate's detach) are the same idea built one card at a time; migrating them is a follow-up.

/// <summary>The specs a decision can carry: which card asked, and so which options it offers and what each does. Named here so the
/// engine, its tests and the wire agree.</summary>
public static class EitherOrSpecs
{
    public const string SakumaPayOrNegate = "sakuma-pay-or-negate";
    public const string UnfortunateHesitation = "unfortunate-hesitation";
    /// <summary>A three-option fixture (never in a playable deck): pay 2 Gold, lose 2 Honor, or let the acting player gain 2 Honor.</summary>
    public const string TrainingThreeWay = "training-three-way";
    /// <summary>Extend the Soul's Boundaries (issue #453 batch 5): "His controller may shuffle him and his attachments back into their owners' decks. If he did not choose
    /// this, take two additional Battle actions."</summary>
    public const string ExtendSoulShuffle = "extend-soul-shuffle";
    /// <summary>Moshi Ino's storm (issue #453, batch 3): the player who took the action chooses to discard a card or bow its performer (MoshiInoStorm.cs).</summary>
    public const string InoStorm = "ino-storm";
    /// <summary>Yasuki Makoto (issue #460, batch 2): "His controller may pay 4 Gold. If he does, he loses 1 Honor. If he does not, dishonor the
    /// target and move him home." -- the same "the AFFECTED card's controller decides" shape Extend the Soul's Boundaries already established.</summary>
    public const string MakotoPayOrDishonor = "makoto-pay-or-dishonor";
    /// <summary>Yasuki Daiki (Forgotten Legacy pass, issue #467 batch 2): "His controller chooses to either pay 4 Gold, lose 2 Honor, or do neither. If he did not pay 4 Gold
    /// or lose 2 Honor, the Personality may not be assigned, and will not move, into attacking armies." -- Makoto's "the affected card's controller decides", three ways.</summary>
    public const string DaikiPayLoseOrLock = "daiki-pay-lose-or-lock";
    /// <summary>Kitsuki Methods (The Shadow's Embrace pass, issue #476 batch 3): "That player may choose to discard a card at random from his hand. If he did not choose this, either
    /// gain 2 Honor, or the target's controller loses Honor equal to his base Personal Honor." -- the target's controller decides first; a refusal hands the second choice
    /// (KitsukiMethodsReward) to the acting player, chained (ShadowsEmbraceBatch3.cs).</summary>
    public const string KitsukiMethodsDiscard = "kitsuki-methods-discard";
    public const string KitsukiMethodsReward = "kitsuki-methods-reward";
    /// <summary>Crowded Streets (The Shadow's Embrace pass, issue #476 batch 3): "After an action resolves that one or more of your Personalities at the current battlefield performed:
    /// You may move any of those performers home." -- the Terrain's controller decides; the resolved action's own tail then carries on (Origin "after-action").</summary>
    public const string CrowdedStreetsMoveHome = "crowded-streets-move-home";
    /// <summary>Forge Your Soul (The Shadow's Embrace pass, issue #476 batch 3): "You may attach one of them that is a Weapon to him, paying 4 less Gold. You may shuffle your
    /// deck." -- the attach (one option per shown Weapon, each with its own Gold cost), then the shuffle, chained; the shuffle's finish draws for a Kensai who showed no Weapon.</summary>
    public const string ForgeYourSoulAttach = "forge-your-soul-attach";
    public const string ForgeYourSoulShuffle = "forge-your-soul-shuffle";
    public const string ForgeYourSoulShuffleNoWeapon = "forge-your-soul-shuffle-no-weapon";
    /// <summary>Kakita Genshi (Emperor Edition Personality pass, issue #477 batch 4): "His controller may dishonor him. If he did not choose to dishonor him, gain 1 Honor and choose one of the
    /// Personality's abilities; it may not be used until your next turn begins." -- the affected card's controller decides (Yasuki Makoto's shape); a refusal chains into the acting player's choice.</summary>
    public const string GenshiShame = "genshi-shame";
    /// <summary>Yasuki Tanimura (Emperor Edition Personality pass, issue #477 batch 6): "The player may pay 4 Gold. If he does not pay the Gold, he loses 3 Honor." -- the decider is the ACTING player.</summary>
    public const string TanimuraPayOrLose = "tanimura-pay-or-lose";
    /// <summary>Sacrifice of Pawns (Emperor Edition remaining-cards pass, group C, issue #492): "His controller may destroy the target. If he did not choose this, search your Dynasty deck for a
    /// Personality and bring him into play, paying all costs but paying two more Gold." -- the target's controller decides; a refusal opens the acting player's search (EmperorRemainingC.cs).</summary>
    public const string SacrificeOfPawns = "sacrifice-of-pawns";
    /// <summary>The Power of a Word (Emperor Edition remaining-cards pass, issue #492, group D): "The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects." -- the decider is the ACTING player.</summary>
    public const string PowerOfWordLoseOrNegate = "power-word-lose-or-negate";
    /// <summary>The Shadow Court (issue #492): "draw a card, and choose a player, who gains or loses 2 Honor" -- the watch's controller decides. The Sincerity of the Dragon: "choose a player, who discards a card".</summary>
    public const string ShadowCourtHonor = "shadow-court-honor";
    public const string SincerityDiscard = "sincerity-discard";
}

/// <summary>What choosing an option amounts to, for a client to word and style it: "avoid" (accept a cost or a lesser loss so the
/// consequence does not happen), "accept" (take the consequence) or "choose" (one of several equal alternatives).</summary>
public static class EitherOrKind
{
    public const string Avoid = "avoid";
    public const string Accept = "accept";
    public const string Choose = "choose";
}

/// <summary>Engine-private context of the Either/Or in progress (MatchState.PendingEitherOr).</summary>
public sealed class PendingEitherOr
{
    public string SpecId { get; set; } = "";
    /// <summary>The card whose text made the offer (Sakuma; the played Strategy).</summary>
    public string SourceCardId { get; set; } = "";
    /// <summary>The card the offer is about, if any (the targeted Personality).</summary>
    public string? AffectedCardId { get; set; }
    /// <summary>The player whose card or action it is, and the player who decides.</summary>
    public int ActingPlayerId { get; set; }
    public int DeciderPlayerId { get; set; }
    /// <summary>"action" or "window" -- see the file comment.</summary>
    public string Origin { get; set; } = "action";
    /// <summary>For a "window": the reaction window this choice interrupted, restored when it is answered.</summary>
    public PendingChoice? Suspended { get; set; }
    /// <summary>Who resumes when it is finished: for an "action", the actor (priority passes to the other player, or stays when the
    /// actor still holds an Additional Action); for a "window", the player whose reaction it was.</summary>
    public int ResumePlayerId { get; set; }
    public bool StayWithActor { get; set; }
    public List<string>? BowedBefore { get; set; }
    /// <summary>The card that performed the action, when an option reads it (Kitsuki Methods: "gain 2 Honor" is its performer's; The Shadow's Embrace pass, issue #476 batch 3).</summary>
    public string? PerformerId { get; set; }
    // Kakita Genshi (issue #477 batch 4): the whole action, so the tail every action ends with runs once the choice (and the one it chains into) is made; ChainAbilityLock is set by the
    // refusal, which owes the acting player a choice of one of the affected Personality's abilities to lock.
    public GameCommand? Command { get; set; }
    public bool ConsumedGrant { get; set; }
    public bool PlayedFromDiscard { get; set; }
    public int? OpponentHonorBefore { get; set; }
    public List<AdditionalActionGrant> PreExistingGrants { get; set; } = [];
    public bool ChainAbilityLock { get; set; }
}

/// <summary>One thing the deciding player may choose. Detail says in plain words what it does; Gold is the payment quote for an option
/// that costs Gold (the deciding player's own view only) and carries the engine's suggested sources.</summary>
public sealed record EitherOrOptionView(string Key, string Label, string Detail, string Kind, bool Legal, string? Reason, GoldQuote? Gold);

/// <summary>PUBLIC: both players see the decision (which card asked, whose it is, who decides, what is on offer); ViewerDecides is true
/// only in the deciding player's own view, which alone carries Gold quotes. The engine has already priced and validated every option, so
/// the client only presents them and submits the one chosen (command "chooseEitherOr", Choice = the option's Key, SourceIds for the
/// payment of a Gold option).</summary>
public sealed record EitherOrView(string ChoiceId, string SourceCardId, string SourceDefinitionId, string SourceName, int ActingPlayerId,
    int DeciderPlayerId, bool ViewerDecides, string? AffectedCardId, string? AffectedName, string Prompt, EitherOrOptionView[] Options);

public sealed partial class MatchEngine
{
    private sealed record EitherOrOption(string Key, string Label, string Detail, string Kind, int GoldCost, bool Legal, string? Reason);

    // ---- what is on offer ----

    private string EitherOrPrompt(PendingEitherOr p)
    {
        var source = Definition(Card(p.SourceCardId)).Name;
        var affected = p.AffectedCardId is null ? "" : Definition(Card(p.AffectedCardId)).Name;
        return p.SpecId switch
        {
            EitherOrSpecs.SakumaPayOrNegate => $"{source}: pay 2 Gold, or your action's remaining effects are negated.",
            EitherOrSpecs.UnfortunateHesitation => $"{source}: bow {affected}, or you must pass at your next opportunity this phase to take a Battle, Limited, or Open action.",
            EitherOrSpecs.TrainingThreeWay => $"{source}: pay 2 Gold, lose 2 Honor, or let your opponent gain 2 Honor.",
            EitherOrSpecs.ExtendSoulShuffle => $"{source}: shuffle {affected} and his attachments into their owners' decks, or your opponent takes two additional Battle actions.",
            EitherOrSpecs.InoStorm => $"{source}'s storm: discard a card, or bow {(affected == "" ? "the performer" : affected)}.",
            EitherOrSpecs.MakotoPayOrDishonor => $"{source}: pay 4 Gold and lose 1 Honor, or {affected} is dishonored and moved home.",
            EitherOrSpecs.DaikiPayLoseOrLock => $"{source}: pay 4 Gold, lose 2 Honor, or neither -- then {affected} may not be assigned, and will not move, into attacking armies this turn.",
            EitherOrSpecs.KitsukiMethodsDiscard or EitherOrSpecs.KitsukiMethodsReward or EitherOrSpecs.CrowdedStreetsMoveHome
                or EitherOrSpecs.ForgeYourSoulAttach or EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon => ShadowsEmbraceEitherOrPrompt(p, source, affected),
            EitherOrSpecs.GenshiShame => $"{source}: dishonor {affected}, or {Player(p.ActingPlayerId).Name} gains 1 Honor and locks one of his abilities until their next turn begins.",
            EitherOrSpecs.TanimuraPayOrLose => $"{source}: pay 4 Gold, or lose 3 Honor.",
            EmperorRemainingBSpecs.MercyBowOrHome => $"{source}: choose to either bow {affected} or move him home.", // Emperor Edition remaining-cards pass (issue #492), group B
            EmperorRemainingBSpecs.AncestorsDestroy => $"{source}: destroy {affected}, or discard a card at random from your hand and {Player(p.ActingPlayerId).Name} chooses a player, who gains or loses 3 Honor.",
            EmperorRemainingBSpecs.AncestorsHonor => $"{source}: choose a player, who gains or loses 3 Honor.",
            EitherOrSpecs.SacrificeOfPawns => SacrificeOfPawnsPrompt(p, source, affected),
            EitherOrSpecs.PowerOfWordLoseOrNegate => $"{source}: lose 3 Honor, or your action's effects are negated.",
            EitherOrSpecs.ShadowCourtHonor => $"{source}: choose a player who gains or loses 2 Honor.",
            EitherOrSpecs.SincerityDiscard => $"{source}: choose a player who discards a card.",
            _ when EmperorP1Specs.Is(p.SpecId) => EmperorP1EitherOrPrompt(p, source), // issue #503
            _ => P5EitherOrPrompt(p, source, affected) ?? EmperorP6EitherOrPrompt(p, source, affected) ?? RemainingGEitherOrPrompt(p, source) // issue #503: The Price of Honor (EmperorP5Zones.cs). Emperor Edition remaining-cards pass (issue #492), group G: Bandit Gang.
        };
    }

    /// <summary>The options of a decision, in the order they are shown, each with its own legality against the position as it stands now.</summary>
    private EitherOrOption[] EitherOrOptions(PendingEitherOr p)
    {
        var source = Card(p.SourceCardId);
        var affected = p.AffectedCardId is null ? null : Card(p.AffectedCardId);
        var payable = (int cost) => PaymentOptions(p.DeciderPlayerId, cost).Length > 0;
        switch (p.SpecId)
        {
            case EitherOrSpecs.SakumaPayOrNegate:
                // An action performed by a Protected card cannot be negated (issue #212), so there is nothing to pay to avoid: that leaves one
                // option, and the choice resolves by itself.
                var cannotBeNegated = _state.PendingNegatableAbility is { } deferred && ActionProtected(deferred); // Ki-Uso too (issue #453 batch 5)
                return [
                    new("pay", "Pay 2 Gold", "Your action goes ahead: it is not negated.", EitherOrKind.Avoid, 2, payable(2) && !cannotBeNegated,
                        cannotBeNegated ? "This action cannot be negated." : "You cannot produce 2 Gold."),
                    new("negate", "Do not pay", "Your action's remaining effects are negated.", EitherOrKind.Accept, 0, true, null)
                ];
            case EitherOrSpecs.UnfortunateHesitation:
            {
                var name = Definition(affected!).Name;
                var canBow = InPlay(affected!) && !affected!.Bowed;
                return [
                    new("bow", $"Bow {name}", $"{name} bows. You are not forced to pass.", EitherOrKind.Avoid, 0, canBow, $"{name} is already bowed."),
                    new("pass", "Do not bow him", "You must pass at your next opportunity this phase to take a Battle, Limited, or Open action.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            case EitherOrSpecs.TrainingThreeWay:
                return [
                    new("pay", "Pay 2 Gold", "Nothing further happens.", EitherOrKind.Avoid, 2, payable(2), "You cannot produce 2 Gold."),
                    new("honor", "Lose 2 Honor", "You lose 2 Honor.", EitherOrKind.Avoid, 0, Player(p.DeciderPlayerId).Honor > 0, "You have no Honor to lose."),
                    new("accept", "Let your opponent gain 2 Honor", "Your opponent gains 2 Honor.", EitherOrKind.Accept, 0, true, null)
                ];
            // Extend the Soul's Boundaries (issue #453 batch 5): the shuffle is a choice only while the Personality is still in play.
            case EitherOrSpecs.ExtendSoulShuffle:
            {
                var name = Definition(affected!).Name;
                return [
                    new("shuffle", $"Shuffle {name} into the decks", $"{name} and his attachments are shuffled into their owners' decks.", EitherOrKind.Avoid, 0, InPlay(affected!), $"{name} is no longer in play."),
                    new("refuse", "Do not shuffle him", "Your opponent takes two additional Battle actions.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            case EitherOrSpecs.InoStorm:
                return InoStormOptions(p, affected);
            // Yasuki Makoto (issue #460 batch 2): the decider is the TARGET's controller (p.DeciderPlayerId, set to target.OwnerId when the
            // action opens this decision), not the acting player -- paying avoids both the Honor loss and the dishonor/move-home.
            case EitherOrSpecs.MakotoPayOrDishonor:
            {
                var name = Definition(affected!).Name;
                return [
                    new("pay", "Pay 4 Gold", "You lose 1 Honor.", EitherOrKind.Avoid, 4, payable(4), "You cannot produce 4 Gold."),
                    new("refuse", "Do not pay", $"{name} is dishonored and moved home.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            // Yasuki Daiki (issue #467 batch 2): the decider is the target's controller. Losing Honor is always possible (Family Honor may go negative).
            case EitherOrSpecs.DaikiPayLoseOrLock:
            {
                var name = Definition(affected!).Name;
                return [
                    new("pay", "Pay 4 Gold", "Nothing further happens.", EitherOrKind.Avoid, 4, payable(4), "You cannot produce 4 Gold."),
                    new("honor", "Lose 2 Honor", "Nothing further happens.", EitherOrKind.Avoid, 0, true, null),
                    new("neither", "Do neither", $"{name} may not be assigned, and will not move, into attacking armies this turn.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            case EitherOrSpecs.KitsukiMethodsDiscard: case EitherOrSpecs.KitsukiMethodsReward: case EitherOrSpecs.CrowdedStreetsMoveHome:
            case EitherOrSpecs.ForgeYourSoulAttach: case EitherOrSpecs.ForgeYourSoulShuffle: case EitherOrSpecs.ForgeYourSoulShuffleNoWeapon:
                return ShadowsEmbraceEitherOrOptions(p, affected);
            // Kakita Genshi (issue #477 batch 4): the decider is the target's controller. Dishonoring is a choice only while the Personality is still in play.
            case EitherOrSpecs.GenshiShame:
            {
                var name = Definition(affected!).Name;
                return [
                    new("dishonor", $"Dishonor {name}", $"{name} is dishonored. Nothing further happens.", EitherOrKind.Avoid, 0, InPlay(affected!), $"{name} is no longer in play."),
                    new("refuse", "Do not dishonor him", $"{Player(p.ActingPlayerId).Name} gains 1 Honor and chooses one of {name}'s abilities, which may not be used until their next turn begins.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            // Yasuki Tanimura (issue #477 batch 6): the decider is the player whose Battle action targeted him. Losing Honor is always possible (Family Honor may go negative).
            case EitherOrSpecs.SacrificeOfPawns:
                return SacrificeOfPawnsOptions(p, affected!);
            case EitherOrSpecs.TanimuraPayOrLose:
                return [
                    new("pay", "Pay 4 Gold", "Nothing further happens.", EitherOrKind.Avoid, 4, payable(4), "You cannot produce 4 Gold."),
                    new("lose", "Do not pay", "You lose 3 Honor.", EitherOrKind.Accept, 0, true, null)
                ];
            case EmperorRemainingBSpecs.MercyBowOrHome: return MercyOptions(affected); // Emperor Edition remaining-cards pass (issue #492), group B
            case EmperorRemainingBSpecs.AncestorsDestroy: case EmperorRemainingBSpecs.AncestorsHonor: return AncestorsOptions(p, affected);
            // The Power of a Word (issue #492): the decider is the player whose Battle action targeted the Courtier. Losing Honor is always possible (Family Honor may go negative).
            case EitherOrSpecs.PowerOfWordLoseOrNegate:
                return [
                    new("lose", "Lose 3 Honor", "Your action goes ahead: it is not negated.", EitherOrKind.Avoid, 0, true, null),
                    new("negate", "Do not lose the Honor", "Your action's effects are negated.", EitherOrKind.Accept, 0, true, null)
                ];
            case EitherOrSpecs.ShadowCourtHonor: case EitherOrSpecs.SincerityDiscard:
                return EmperorRemainingDEitherOrOptions(p);
            case var p1 when EmperorP1Specs.Is(p1): return EmperorP1EitherOrOptions(p); // issue #503 (EmperorP1.cs)
            default: return P5EitherOrOptions(p, affected) ?? EmperorP6EitherOrOptions(p, affected) ?? RemainingGEitherOrOptions(p, source, payable); // issue #503: The Price of Honor // Bandit Gang (issue #492 group G).
        }
    }

    // ---- opening ----

    /// <summary>Hands a decision to <paramref name="deciderId"/>. Returns true when it is now open (the caller's action does not finish
    /// until it is answered: FinishEitherOr resumes it) and false when it needed no decision -- exactly one option was legal, so it was
    /// taken and announced -- and the caller finishes the action itself. <paramref name="origin"/>: "window" suspends the reaction window
    /// that is open right now, "action" is the tail of an ordinary action (bowedBefore is the snapshot of what was already bowed).</summary>
    private bool OpenEitherOr(string specId, CardInstance source, int actingPlayerId, int deciderId, CardInstance? affected, string origin,
        IEnumerable<string>? bowedBefore = null, bool stayWithActor = false, string? performerId = null, int? resumePlayerId = null)
    {
        var pending = new PendingEitherOr
        {
            SpecId = specId, SourceCardId = source.Id, AffectedCardId = affected?.Id, ActingPlayerId = actingPlayerId, DeciderPlayerId = deciderId,
            Origin = origin, BowedBefore = bowedBefore?.ToList(), StayWithActor = stayWithActor, PerformerId = performerId,
            ResumePlayerId = resumePlayerId ?? (origin == "window" ? source.OwnerId : actingPlayerId)
        };
        var options = EitherOrOptions(pending);
        var legal = options.Where(o => o.Legal).ToArray();
        // No decision when nothing is left to choose: exactly one option is legal and it costs nothing (a costed option still needs the
        // player to pick sources), or none is (the last, the consequence, stands).
        var only = legal.Length == 1 ? legal[0] : legal.Length == 0 ? options[^1] : null;
        if (only is not null && only.GoldCost == 0)
        {
            Emit("eitherOrAutoResolved", $"{Definition(source).Name}: {Player(deciderId).Name} has no choice to make ({only.Label}: {only.Detail})", deciderId, source.Id);
            ApplyEitherOr(pending, only, null);
            return false;
        }
        pending.Suspended = origin == "window" ? _state.PendingChoice : null;
        _state.PendingEitherOr = pending;
        _state.PendingChoice = new("eitherOr", deciderId, source.Id, EitherOrPrompt(pending), actingPlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        Emit("eitherOrOffered", $"{Player(deciderId).Name} must choose ({Definition(source).Name}): {string.Join(" / ", options.Select(o => o.Label))}.", deciderId, source.Id);
        return true;
    }

    // ---- answering ----

    private void ResolveEitherOrCommand(GameCommand command)
    {
        var pending = _state.PendingEitherOr ?? throw new RuleViolation("No choice is pending.");
        Require(command.PlayerId == pending.DeciderPlayerId, "Only the player who must choose may answer.");
        Require(command.Type == "chooseEitherOr" && command.Choice is not null, "Choose one of the offered options.");
        var option = EitherOrOptions(pending).FirstOrDefault(o => o.Key == command.Choice);
        Require(option is not null, "Choose one of the offered options.");
        Require(option!.Legal, option.Reason ?? "That option is not available.");
        if (option.GoldCost > 0) ValidatePayment(pending.DeciderPlayerId, option.GoldCost, command.SourceIds);
        else Require(command.SourceIds is null or { Length: 0 }, "This option needs no Gold sources.");
        ApplyEitherOr(pending, option, command.SourceIds);
        // A decision that hands a further one to a player (Kitsuki Methods' refusal; The Shadow's Embrace pass, issue #476 batch 3): that one finishes the action when it is answered.
        if (_state.PendingEitherOr is { } chained && !ReferenceEquals(chained, pending)) return;
        FinishEitherOr(pending);
    }

    // What choosing one option does. Public events name the choice, so the other player and the log see it.
    private void ApplyEitherOr(PendingEitherOr p, EitherOrOption option, string[]? sourceIds)
    {
        var source = Card(p.SourceCardId);
        var decider = Player(p.DeciderPlayerId);
        var affected = p.AffectedCardId is null ? null : Card(p.AffectedCardId);
        switch (p.SpecId, option.Key)
        {
            case (EitherOrSpecs.SakumaPayOrNegate, "pay"):
            case (EitherOrSpecs.TrainingThreeWay, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                Emit("eitherOrChosen", $"{decider.Name} paid {option.GoldCost} Gold ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.SakumaPayOrNegate, "negate"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}).", decider.Id, source.Id);
                NegateDeferredAction(source);
                break;
            case (EitherOrSpecs.UnfortunateHesitation, "bow"):
                affected!.Bowed = true;
                Emit("eitherOrChosen", $"{decider.Name} chose to bow {Definition(affected).Name} ({Definition(source).Name}).", decider.Id, affected.Id);
                break;
            case (EitherOrSpecs.UnfortunateHesitation, "pass"):
                Emit("eitherOrChosen", $"{decider.Name} did not bow {Definition(affected!).Name} ({Definition(source).Name}).", decider.Id, source.Id);
                AddForcedPass(decider.Id, source);
                break;
            case (EitherOrSpecs.TrainingThreeWay, "honor"):
                Emit("eitherOrChosen", $"{decider.Name} chose to lose 2 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(decider.Id, -2, Definition(source).Name);
                break;
            case (EitherOrSpecs.TrainingThreeWay, "accept"):
                Emit("eitherOrChosen", $"{decider.Name} let {Player(1 - decider.Id).Name} gain 2 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(1 - decider.Id, 2, Definition(source).Name);
                break;
            // Extend the Soul's Boundaries (issue #453 batch 5).
            case (EitherOrSpecs.ExtendSoulShuffle, "shuffle"):
                Emit("eitherOrChosen", $"{decider.Name} chose to shuffle {Definition(affected!).Name} and his attachments into their owners' decks ({Definition(source).Name}).", decider.Id, affected!.Id);
                ShuffleUnitIntoOwnersDecks(affected!);
                break;
            case (EitherOrSpecs.ExtendSoulShuffle, "refuse"):
                Emit("eitherOrChosen", $"{decider.Name} did not shuffle {Definition(affected!).Name} ({Definition(source).Name}): {Player(p.ActingPlayerId).Name} takes two additional Battle actions.", decider.Id, source.Id);
                GrantAdditionalAction(p.ActingPlayerId, null);
                GrantAdditionalAction(p.ActingPlayerId, null);
                p.StayWithActor = _state.AdditionalActions.Any(g => g.PlayerId == p.ActingPlayerId && Forfeitable(g));
                break;
            case (EitherOrSpecs.InoStorm, "discard"):
            case (EitherOrSpecs.InoStorm, "bow"):
                ApplyInoStorm(p, option, decider, affected);
                break;
            // Yasuki Makoto (issue #460 batch 2).
            case (EitherOrSpecs.MakotoPayOrDishonor, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                GainHonor(decider.Id, -1, Definition(source).Name);
                Emit("eitherOrChosen", $"{decider.Name} paid 4 Gold and lost 1 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.MakotoPayOrDishonor, "refuse"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}): {Definition(affected!).Name} is dishonored and moved home.", decider.Id, affected!.Id);
                Dishonor(affected!, p.ActingPlayerId);
                MoveHome(affected!, false);
                break;
            // Yasuki Daiki (issue #467 batch 2; ForgottenLegacyBatch2.cs).
            case (EitherOrSpecs.DaikiPayLoseOrLock, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                Emit("eitherOrChosen", $"{decider.Name} paid 4 Gold ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.DaikiPayLoseOrLock, "honor"):
                Emit("eitherOrChosen", $"{decider.Name} chose to lose 2 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(decider.Id, -2, Definition(source).Name, source);
                break;
            case (EitherOrSpecs.DaikiPayLoseOrLock, "neither"):
                Emit("eitherOrChosen", $"{decider.Name} did neither ({Definition(source).Name}).", decider.Id, affected!.Id);
                DaikiLock(affected!, source);
                break;
            case (EitherOrSpecs.KitsukiMethodsDiscard, _): case (EitherOrSpecs.KitsukiMethodsReward, _): case (EitherOrSpecs.CrowdedStreetsMoveHome, _):
            case (EitherOrSpecs.ForgeYourSoulAttach, _): case (EitherOrSpecs.ForgeYourSoulShuffle, _): case (EitherOrSpecs.ForgeYourSoulShuffleNoWeapon, _):
                ApplyShadowsEmbraceEitherOr(p, option, decider, affected, sourceIds);
                break;
            // Kakita Genshi (issue #477 batch 4; EmperorPersonalityBatch4.cs).
            case (EitherOrSpecs.GenshiShame, "dishonor"):
                Emit("eitherOrChosen", $"{decider.Name} chose to dishonor {Definition(affected!).Name} ({Definition(source).Name}).", decider.Id, affected!.Id);
                Dishonor(affected!, p.ActingPlayerId);
                break;
            case (EitherOrSpecs.GenshiShame, "refuse"):
                Emit("eitherOrChosen", $"{decider.Name} did not dishonor {Definition(affected!).Name} ({Definition(source).Name}): {Player(p.ActingPlayerId).Name} gains 1 Honor.", decider.Id, source.Id);
                GainHonorFromPerformer(source, 1, Definition(source).Name);
                p.ChainAbilityLock = true;
                break;
            // Yasuki Tanimura (issue #477 batch 6).
            case (EitherOrSpecs.SacrificeOfPawns, _):
                ApplySacrificeOfPawns(p, option, decider, source, affected);
                break;
            case (EitherOrSpecs.TanimuraPayOrLose, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                Emit("eitherOrChosen", $"{decider.Name} paid 4 Gold ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.TanimuraPayOrLose, "lose"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}): {decider.Name} loses 3 Honor.", decider.Id, source.Id);
                GainHonor(decider.Id, -3, Definition(source).Name, source);
                break;
            case (EmperorRemainingBSpecs.MercyBowOrHome, _): // Emperor Edition remaining-cards pass (issue #492), group B (EmperorRemainingB.cs)
                ApplyMercy(p, option, decider, affected);
                break;
            case (EmperorRemainingBSpecs.AncestorsDestroy, _): case (EmperorRemainingBSpecs.AncestorsHonor, _):
                ApplyAncestors(p, option, decider, affected);
                break;
            // The Power of a Word (issue #492; EmperorRemainingD.cs).
            case (EitherOrSpecs.PowerOfWordLoseOrNegate, "lose"):
                Emit("eitherOrChosen", $"{decider.Name} chose to lose 3 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(decider.Id, -3, Definition(source).Name, source);
                break;
            case (EitherOrSpecs.PowerOfWordLoseOrNegate, "negate"):
                Emit("eitherOrChosen", $"{decider.Name} did not lose the Honor ({Definition(source).Name}): the action's effects are negated.", decider.Id, source.Id);
                NegateHeldActionFromEitherOr(p, source);
                break;
            case (EitherOrSpecs.ShadowCourtHonor, _): case (EitherOrSpecs.SincerityDiscard, _):
                ApplyEmperorRemainingDEitherOr(p, option, decider);
                break;
            default:
                if (ApplyEmperorP1EitherOr(p, option, source, decider)) break; // issue #503
                if (ApplyP5EitherOr(p, option, decider, affected, sourceIds)) break; // issue #503: The Price of Honor (EmperorP5Zones.cs)
                if (ApplyEmperorP6EitherOr(p, option, source, decider)) break; // issue #503: Kuni Renyu (EmperorP6.cs).
                if (ApplyRemainingGEitherOr(p, option, source, decider, sourceIds)) break; // Bandit Gang (issue #492 group G).
                throw new RuleViolation("Unknown choice.");
        }
    }

    // Every path out of an answered decision (an automatic one never opened it, so it does not come through here).
    private void FinishEitherOr(PendingEitherOr p)
    {
        _state.PendingEitherOr = null;
        if (p.SpecId == EitherOrSpecs.SacrificeOfPawns && _state.PendingSearch is not null) return; // Sacrifice of Pawns (issue #492, group C): the refusal opened a search, which finishes the action.
        if (FinishEmperorRemainingDEitherOr(p)) return; // Emperor Edition remaining-cards pass (issue #492), group D: The Shadow Court and The Sincerity of the Dragon (EmperorRemainingD.cs).
        // The Shadow's Embrace pass (issue #476), batch 3 (ShadowsEmbraceBatch3.cs): Crowded Streets' trait decision carries on with the resolved action's own tail; Forge Your
        // Soul's last decision draws and opens the attached Weapon's entering-play window first.
        if (FinishShadowsEmbraceEitherOr(p)) return;
        if (FinishRemainingGEitherOr(p)) return;
        if (FinishEmperorP1EitherOr(p)) return; // issue #503 (EmperorP1.cs) // Bandit Gang (issue #492 group G): the battle's resolution carries on.
        if (FinishP5EitherOr(p)) return; // issue #503: The Price of Honor's shown hand opened the acting player's discard choice // Bandit Gang (issue #492 group G): the battle's resolution carries on.
        if (p.SpecId == EitherOrSpecs.GenshiShame && p.Origin == "action") { FinishGenshi(p); return; } // Kakita Genshi (issue #477 batch 4): the whole Iaijutsu action finishes together.
        if (p.Origin == "window")
        {
            // A Reaction was played inside a reaction window: the window stays open and priority flips, exactly as when any Reaction
            // finishes (RunAbilityEffects' own tail).
            _state.PendingChoice = p.Suspended;
            NextAction(p.ResumePlayerId);
            return;
        }
        _state.PendingChoice = null;
        // The ordinary tail of a resolved action, as a search or a look ends it: a chance to react to it having bowed a Personality (the
        // chosen bow is one), a deferred Overconfident window, then priority on.
        if (p.BowedBefore is not null && OpenBowReactionWindow(p.ActingPlayerId, p.BowedBefore.ToHashSet(StringComparer.Ordinal), 1 - p.ActingPlayerId)) return;
        if (OpenOverconfidentDrawWindow(1 - p.ActingPlayerId)) return;
        NextAction(p.ActingPlayerId, p.StayWithActor);
    }

    // ---- what each player sees ----

    private EitherOrView? BuildEitherOrView(int viewerId)
    {
        if (_state.PendingEitherOr is not { } p || _state.PendingChoice is null) return null;
        var source = Card(p.SourceCardId);
        var decides = viewerId == p.DeciderPlayerId;
        var options = EitherOrOptions(p).Select(o => new EitherOrOptionView(o.Key, o.Label, o.Detail, o.Kind, o.Legal, o.Legal ? null : o.Reason,
            decides && o.GoldCost > 0 ? EitherOrGoldQuote(p, o) : null)).ToArray();
        return new(p.SpecId, source.Id, source.DefinitionId, Definition(source).Name, p.ActingPlayerId, p.DeciderPlayerId, decides, p.AffectedCardId,
            p.AffectedCardId is null ? null : Definition(Card(p.AffectedCardId)).Name, _state.PendingChoice.Prompt, options);
    }

    private GoldQuote EitherOrGoldQuote(PendingEitherOr p, EitherOrOption option) =>
        new(option.GoldCost, GoldCostKind.Fixed, $"{Definition(Card(p.SourceCardId)).Name}: {option.Label.ToLowerInvariant()}", null, PaymentOptions(p.DeciderPlayerId, option.GoldCost));

    // ---- the deciding player's legal actions ----

    /// <summary>One action per option, an unavailable one listed disabled with the reason and a payable Gold option carrying the engine's
    /// suggested sources, so a bot or any legal-actions-driven client can always answer. The other player has no decision.</summary>
    private IEnumerable<ActionOption> EitherOrActions(int playerId)
    {
        if (_state.PendingEitherOr is not { } p || p.DeciderPlayerId != playerId) yield break;
        foreach (var o in EitherOrOptions(p))
        {
            var command = new GameCommand("chooseEitherOr", playerId, _state.Version, CardId: p.SourceCardId, Choice: o.Key);
            GoldQuote? quote = null;
            if (o.GoldCost > 0)
            {
                quote = EitherOrGoldQuote(p, o);
                if (o.Legal) command = command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds };
            }
            yield return new("chooseEitherOr", o.Label, o.Legal, o.Legal ? null : o.Reason, o.Legal ? command : null, p.SourceCardId, Gold: quote);
        }
    }
}
