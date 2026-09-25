namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 6: Kitsuki Yataku, Nishimura, Genmyo, Qalyar, Seppun Washi, Yasuki Jekku, Yasuki Tanimura, Yung, Daigotsu Ishibashi and
// Daigotsu Kanpeki - exp. Every card reuses an existing primitive; what is new is small and documented at its use:
//   * a "no refusal" negation entry on the deferred action (Yataku), read where a challenge is issued (Dueling.cs);
//   * an optional hand-card discard slot beside the target (Nishimura: the new "own-hand-kiho-or-weapon" category);
//   * an alternate Gold source that only pays for actions and does not bow (Tanimura: AbilityDefinition.PaymentActionsOnly / PaymentBows);
//   * an Either/Or handed to the ACTING player by a card that was targeted (Tanimura's Political Reaction);
//   * a Ring-entering-play trigger read off the action that resolved (Yung), and a Strategy-Battle-action trigger (Kanpeki);
//   * an Attack whose strength is the Focus Value of a Dark Virtue card shown from the hand, plus one (Ishibashi).
public sealed partial class MatchEngine
{
    // ---- Kitsuki Yataku ----

    private const string NoRefuseEffect = "no-refuse";

    // The reaction is offered against an action that targets a Samurai and would create a challenge that Samurai's controller could refuse; against any other action there is
    // nothing for it to change, so no window opens for it.
    private bool YatakuApplies(CardInstance affected, AbilityDefinition acting) =>
        Definition(affected).Type == CardType.Personality && Keyword(affected, "Samurai") && acting.IssuesDuel && acting.DuelRefusable;

    // "The Samurai's controller may not choose to refuse any challenge created by the action": recorded as a negation entry on the action (which is dropped when the action
    // has resolved, like every other), and read by IssueChallenge.
    private void YatakuForbidRefusal(CardInstance source, string affectedId)
    {
        _state.NegatedEffects.Add(new(NoRefuseEffect, affectedId));
        var affected = Card(affectedId);
        Emit("refusalForbidden", $"{Definition(source).Name}: {Player(affected.OwnerId).Name} may not refuse any challenge created by the action against {Definition(affected).Name}.", source.OwnerId, affectedId);
    }

    private bool ChallengeMayBeRefused(string challengedId) => !_state.NegatedEffects.Any(n => n.Kind == NoRefuseEffect && n.CardId == challengedId);

    // ---- Nishimura ----

    // "You may discard a Kiho or Weapon from your hand to draw a card": the optional slot, taken after the bow.
    private void NishimuraDiscardAndDraw(GameCommand command, PlayerState player)
    {
        var discard = SlotCards(command, "discard").FirstOrDefault();
        if (discard is null) return;
        DiscardFate(player, discard);
        Draw(player);
    }

    // ---- Yasuki Jekku ----

    // "Dishonor him. His controller loses 1 Honor, or 2 Honor if his base Personal Honor is 3 or greater."
    private void JekkuDishonor(CardInstance target, CardInstance source, int actingPlayerId)
    {
        var loss = Definition(target).PersonalHonor >= 3 ? 2 : 1;
        Dishonor(target, actingPlayerId);
        GainHonor(target.OwnerId, -loss, "Yasuki Jekku", source);
    }

    // ---- Yasuki Tanimura's Political Reaction ----

    // "After another player's Battle action targets Tanimura" (a targeted-by-action reaction, offered in the before-action window and the attack window alike).
    private bool TanimuraApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && affected.Id == card.Id && IsBattleAction(subject.Command);

    // "The player may pay 4 Gold. If he does not pay the Gold, he loses 3 Honor": the acting player decides (EitherOr.cs).
    private bool BeginTanimuraChoice(CardInstance source)
    {
        var acting = OpenReactionSubject()?.Command.PlayerId ?? 1 - source.OwnerId;
        return OpenEitherOr(EitherOrSpecs.TanimuraPayOrLose, source, acting, acting, source, "window");
    }

    // ---- Yung ----

    // "After one of your Rings enters play": a Ring enters play through its own recruit action (ring-of-*-recruit), so the action that just resolved is one of Yung's controller's
    // Rings' recruits, and the Ring is in play now.
    private bool YungRingEnteredPlay(ResolvedActionFacts facts, int playerId) =>
        facts.PlayerId == playerId && facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var ring)
        && Definition(ring).Type == CardType.Ring && ring.OwnerId == playerId && ring.Zone == Zone.Home && facts.AbilityId.EndsWith("-recruit", StringComparison.Ordinal)
        // Togashi's Guidance (Emperor Edition remaining-cards pass, issue #492, group E) also puts a Ring into play: "After one of your Rings enters play".
        || facts.PlayerId == playerId && facts.Affected.Any(a => a.Kind == RingEnteredKind && Card(a.CardId) is { Zone: Zone.Home } entered && entered.OwnerId == playerId);

    // ---- Daigotsu Kanpeki - exp ----

    // "After the resolution of a Battle action from a Strategy, if Kanpeki performed it": a Battle action (or a Battle/Open one taken in the Battle phase) whose source is a Strategy,
    // performed by Kanpeki himself.
    private bool KanpekiPerformedStrategyBattleAction(CardInstance kanpeki, ResolvedActionFacts facts, int playerId)
    {
        if (facts.PerformerId != kanpeki.Id || facts.PlayerId != playerId) return false;
        var timing = AbilityCatalog.All[facts.AbilityId].Timing;
        if (timing != "Battle" && !(timing == "BattleOrOpen" && _state.Phase == Phase.Battle)) return false;
        return facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var sourceCard) && Definition(sourceCard).Type == CardType.Strategy;
    }

    // ---- Daigotsu Ishibashi ----

    // The card Ishibashi shows: one Dark Virtue card in the acting player's hand, carried on the command's CardIds.
    private CardInstance? IshibashiShownCard(GameCommand command) =>
        command.CardIds is { Length: 1 } ids && _state.Cards.TryGetValue(ids[0], out var shown) ? shown : null;

    // "Ranged Attack with strength equal to the card's Focus Value plus one".
    private int IshibashiStrength(GameCommand command) => IshibashiShownCard(command) is { } shown ? Definition(shown).Focus + 1 : 0;

    // A card shown from the hand joins the public shown-cards record (nothing about the rest of the hand is revealed) and is announced.
    private void ShowHandCard(int playerId, CardInstance shown, CardInstance source)
    {
        var context = $"{Definition(source).Name}, shown from the hand";
        var eventSequence = _state.Events.Count + 1L;
        _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, playerId, shown.DefinitionId, source.Id, source.DefinitionId, "Hand", context, _state.TurnNumber, eventSequence));
        Emit("cardShown", $"{Player(playerId).Name} shows {Definition(shown).Name} ({context}).", playerId);
    }

    private IEnumerable<string> IshibashiShowableCards(PlayerState player) =>
        player.Hand.Where(h => Keyword(Card(h), "Dark Virtue")).Distinct(StringComparer.Ordinal);

    // ---- validation ----

    private void ValidateEmperorBatch6(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Genmyo: "A card never copies its own text/stats" (core rules, Copy), so the Personality he copies from is another one.
        if (id == "genmyo-copy") Require(command.TargetId is not null && command.TargetId != source?.Id, "Genmyo copies a keyword from another Personality of yours.");
        // Ishibashi: "Show a Dark Virtue card in your hand" -- exactly one, from the acting player's own hand.
        if (id == "ishibashi-attack")
        {
            var ids = command.CardIds ?? [];
            Require(ids.Length == 1 && _state.Cards.TryGetValue(ids[0], out var shown) && shown.OwnerId == player.Id && shown.Zone == Zone.Hand && player.Hand.Contains(shown.Id) && Keyword(shown, "Dark Virtue"),
                "Show a Dark Virtue card in your hand.");
        }
    }

    // ---- execution ----

    // Every effect of this batch's abilities that needs more than one line. True when the action is not finished here (a search or a decision is open) and the caller returns.
    private bool RunEmperorBatch6Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, IEnumerable<string> bowedBeforeAction)
    {
        switch (id)
        {
            case "yataku-no-refuse":
                YatakuForbidRefusal(source!, ReactionAffectedId(_state.PendingNegatableAbility!)!);
                return false;
            case "nishimura-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                NishimuraDiscardAndDraw(command, player);
                return false;
            case "genmyo-copy":
                return BeginCopy(source!, target!, id, [CopyStep.Keyword], player.Id, "ability", bowedBeforeAction, GrantDuration.Turn);
            case "qalyar-recon-plus": case "qalyar-recon-minus":
            {
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                // "Its province" is the province of the battlefield being engaged, the Defender's own.
                if (_state.CurrentBattleProvince is int battlefield)
                    GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, battlefield, id == "qalyar-recon-plus" ? 2 : -2, Definition(source!).Name);
                return false;
            }
            case "jekku-dishonor":
                JekkuDishonor(target!, source!, player.Id);
                return false;
            case "tanimura-political-pay":
                return BeginTanimuraChoice(source!);
            case "yung-search-ring":
                // A Reaction that opens a search sets the window it was played in aside; it is given back when the search is over (ReactionsAreActions.cs).
                SuspendOpenWindow();
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case "ishibashi-attack":
            {
                var shown = IshibashiShownCard(command)!;
                ShowHandCard(player.Id, shown, source!);
                var performer = ActionPerformer(ability, command, source);
                return ResolveOrDeferAttack(id, player, target?.Id, performer.Id, consumedGrant, IshibashiStrength(command));
            }
            case "kanpeki-additional-action":
                GrantAdditionalAction(player.Id, null);
                return false;
            case "kanpeki-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            default:
                throw new RuleViolation("Ability effect is not implemented.");
        }
    }
}
