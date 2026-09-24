namespace L5R.Core;

// Forgotten Legacy pass (issue #467), batch 2: the execution cases and the small static rules of the Personalities, the Follower and the Stronghold wired in
// personality-wave-66, follower-wave-29 and stronghold-wave-15. Each card's printed text and reading is at its AbilityCatalog entry (CardAbilities.cs); this file
// holds the helpers, so the shared files only carry one-line hooks. Mirrors EmbersOfWarBatch2.cs's own shape.
public sealed partial class MatchEngine
{
    private const string XiaoFollowerForceEffect = "xiao-army-follower-force";
    private const string SasakoNoResolutionBowEffect = "sasako-unit-no-resolution-bow";
    private const string ShakashDiscountEffect = "shakash-nonhuman-discount";
    private const string SleeplessDiscountEffect = "sleepless-discount";
    private const string JiYunDishonorImmunityEffect = "jiyun-dishonor-immunity";
    private const string ForgottenTempleEffect = "forgotten-temple";

    // Returns true when the action is paused (an Attack's reaction window, an Either/Or decision): the caller returns without finishing it.
    private bool RunForgottenLegacyBatch2Effect(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // The Vengeful: its own Ranged 7 Attack, performed by the Follower itself (Khol Regulars' shape).
            case "vengeful-attack": return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // "If this Personality is Nonhuman: Straighten this unit" -- the Personality the Follower is attached to, and every card attached to him.
            case "vengeful-straighten":
            {
                if (source!.AttachedTo is string hostId && Card(hostId) is { } host && Keyword(host, "Nonhuman")) StraightenUnit(host);
                else Emit("effectFailed", $"{Definition(source).Name}: its Personality is not Nonhuman, so nothing is straightened.", player.Id, source.Id);
                return false;
            }
            case "koyama-weaken": KoyamaWeakenUnit(source!, target!, id, player.Id); return false;
            case "xiao-destroy": DestroyTargetCard(target!, player.Id); return false;
            case "shiho-return": ShihoReturn(source!, target!); return false;
            case "sawaken-weaken": ApplyModifier(target!, source!.Id, id, "Force", -5); return false;
            case "junpei-base-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (Definition(target!).Type == CardType.Personality)
                {
                    target!.MovementLocks.Add(new MovementLock(source!.Id, _state.TurnNumber, MovementKinds.Home | MovementKinds.ToBattlefield, NextOnly: true));
                    Emit("movementLocked", $"{Definition(target).Name}'s next movement this turn is negated ({Definition(source).Name}).", target.OwnerId, target.Id);
                }
                return false;
            case "makiko-favor":
                _state.FavorPlayerId = player.Id;
                Emit("favorTaken", $"{player.Name} took the Imperial Favor ({Definition(source!).Name}).", player.Id, source!.Id);
                return false;
            case "shakash-bow": case "osawa-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "shakash-move-home": ExecuteMove(command, ability, source, target); return false;
            case "sleepless-boost":
                foreach (var nonhuman in _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Follower
                             && Keyword(c, "Nonhuman") && AtBattle(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                    ApplyModifier(nonhuman, source!.Id, id, "Force", 1);
                return false;
            case "osawa-straighten": case "yamazaki-straighten": Straighten(target!); return false;
            // The bowed Follower is the Attack's performer (Tensin's shape).
            case "chokichi-attack": return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId!, consumedGrant);
            case "haruhiko-attack": return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // The target's controller decides (Makoto's shape).
            case "daiki-demand": return OpenEitherOr(EitherOrSpecs.DaikiPayLoseOrLock, source!, player.Id, target!.OwnerId, target, "action");
            case "forgotten-temple-refill": RefillProvinceWithDiscardedPersonality(player, target!, command.Province!.Value, Definition(source!).Name); return false;
            default: throw new RuleViolation("Unknown ability.");
        }
    }

    // Matsu Koyama: "Reduce the Force of each Personality and Follower in it by Koyama's Personal Honor. Bow any cards in the unit whose Force is now 0." His CURRENT Personal
    // Honor (0 while he is dishonorable, core rules Dishonor), read once as the action resolves; the whole unit is penalised before any bow is judged.
    private void KoyamaWeakenUnit(CardInstance koyama, CardInstance target, string effectId, int actingPlayerId)
    {
        var penalty = CardPersonalHonor(koyama);
        var unit = Attachments(target.Id).Where(c => Definition(c).Type == CardType.Follower).Prepend(target).ToArray();
        if (penalty > 0) foreach (var card in unit) ApplyModifier(card, koyama.Id, effectId, "Force", -penalty);
        foreach (var card in unit.Where(c => InPlay(c) && CardForce(c) == 0))
            if (!StalwartNegatesBow(card, actingPlayerId)) card.Bowed = true;
    }

    // Ninube Shiho: "Put Shiho on top of your Dynasty deck (face-down)." -- a Personality leaving play takes his unit with him the same way (core rules, Units), so his own
    // attachments go on top of their owners' Fate decks. "Put all cards without attachments in a target enemy Personality's unit on the bottom of their appropriate deck." --
    // Goju House Guard's sweep, judged once for the whole unit before anything moves.
    private void ShihoReturn(CardInstance shiho, CardInstance target)
    {
        if (InPlay(shiho))
        {
            foreach (var attachment in Attachments(shiho.Id).ToArray()) ToOwnerDeck(attachment, "Top");
            ToOwnerDeck(shiho, "Top");
        }
        if (!InPlay(target)) return;
        var unit = Attachments(target.Id).Append(target).ToArray();
        foreach (var card in unit.Where(c => !HasAttachments(c)).ToArray()) ToOwnerDeck(card, "Bottom");
    }

    // The Shakash: "Enters play paying 1 less Gold for each Nonhuman you control, up to 3 less Gold." The Sleepless One: "Enters play paying 1 less Gold." A term of the one
    // price every recruit pays (QuotePurchase). The Nonhuman count is every card with the keyword in play under the recruiting player's control.
    private (int Amount, string Term) ForgottenLegacyEntryDiscount(CardInstance entering, int playerId) => Definition(entering).Effect switch
    {
        ShakashDiscountEffect => (Math.Min(3, _state.Cards.Values.Count(c => c.Id != entering.Id && c.OwnerId == playerId && InPlay(c) && Keyword(c, "Nonhuman"))), "Nonhuman you control, up to 3"),
        SleeplessDiscountEffect => (1, Definition(entering).Name),
        _ => (0, "")
    };

    // Moto Xiao: "Followers in Xiao's army have +1F." His army is his side at the battlefield he stands at (Army), so the bonus reaches each Follower whose Personality stands on
    // the same side of the same battlefield -- his own Followers included -- and nothing at home. One +1F per Xiao in that army.
    private int XiaoFollowerBoost(CardInstance follower)
    {
        if (follower.Zone != Zone.Attached || Definition(follower).Type != CardType.Follower || BattlefieldParent(follower) is not { } parent) return 0;
        return _state.Cards.Values.Count(c => c.OwnerId == parent.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == parent.ProvinceIndex
            && Definition(c).Type == CardType.Personality && Definition(c).Effect == XiaoFollowerForceEffect);
    }

    // Moshi Sasako: "Battle resolution does not bow cards in this unit." Read at the one place a battle's resolution bows the attacking units (FinishBattleResolution), for the
    // unit led by Sasako himself -- a Personality is always the head of his own unit.
    private bool SasakoUnitWillNotBow(CardInstance unitHead) => Definition(unitHead).Effect == SasakoNoResolutionBowEffect;

    // Utaku Ji-Yun: "Negate Ji-Yun's dishonoring from other players' cards' effects." The player whose card's effect it is: the caller's, or the player of the action being
    // resolved. A dishonoring with no such player (a rule, one of her own controller's cards) is not negated.
    private bool JiYunNegatesDishonor(CardInstance card, int? byPlayerId)
    {
        if (Definition(card).Effect != JiYunDishonorImmunityEffect || card.Dishonorable) return false;
        var causer = byPlayerId ?? _runningCommand?.PlayerId;
        if (causer is not int other || other == card.OwnerId) return false;
        Emit("dishonorNegated", $"{Definition(card).Name}'s dishonoring by another player's card was negated.", card.OwnerId, card.Id);
        return true;
    }

    // Yasuki Daiki: "the Personality may not be assigned, and will not move, into attacking armies." Only the active player's Personalities ever join an attacking army (he is
    // the Attacker of every battle of his turn), so on another player's turn -- Daiki's Open action taken then -- this is A Quiet Transaction's assignment and movement lock;
    // on Daiki's own turn his target defends, and nothing he could do this turn puts him in an attacking army.
    private void DaikiLock(CardInstance target, CardInstance daiki)
    {
        if (target.OwnerId == _state.ActivePlayerId) LockMovement(target, daiki.Id, MovementKinds.Assign | MovementKinds.ToBattlefield);
        else Emit("effectFailed", $"{Definition(target).Name} cannot join an attacking army this turn anyway: his controller is not the active player.", target.OwnerId, target.Id);
    }

    // ---- The Forgotten Temple -------------------------------------------------------------------------------------------------------------------------------------------
    private bool ControlsForgottenTemple(int playerId) =>
        _state.Cards.TryGetValue(Player(playerId).StrongholdId, out var stronghold) && InPlay(stronghold) && Definition(stronghold).Effect == ForgottenTempleEffect;

    // "You may not take Political actions." -- an action is Political when its own keyword says so or its card carries the keyword (ActionCarriesKeyword); the rulebook's
    // Lobby ("Political Limited") and the Imperial Favor's abilities ("Favor Political") are Political too, though their catalogue entries carry no tag.
    private bool ForgottenTempleForbidsPolitical(string id, AbilityDefinition ability, CardInstance? source, PlayerState player) =>
        ControlsForgottenTemple(player.Id) && (id is "lobby" or "dainagon-lobby" or "favor-cycle" or "favor-battle" || ActionCarriesKeyword(ability, source, "Political"));

    // "You do not gain Honor. You do not lose Honor from cards you own. Before you lose Honor: Reduce the loss to 1, which may not be increased." Applied first in GainHonor,
    // the one funnel of every Honor change. A loss is "from" the card named as its source, or else the card of the action being resolved; the rulebook's own dishonorable-
    // death loss is from the rulebook (core rules, "From"), so it is reduced, not negated.
    private int ForgottenTempleHonorChange(int playerId, int amount, string reason, CardInstance? source)
    {
        if (amount == 0 || !ControlsForgottenTemple(playerId)) return amount;
        var name = Player(playerId).Name;
        if (amount > 0)
        {
            Emit("honorGainNegated", $"{name} does not gain Honor ({reason}; The Forgotten Temple).", playerId, source?.Id);
            return 0;
        }
        var from = reason == "Dishonorable death" ? null
            : source ?? (_runningCommand?.CardId is string runningId && _state.Cards.TryGetValue(runningId, out var running) ? running : null);
        if (from is not null && (from.OriginalOwnerId ?? from.OwnerId) == playerId)
        {
            Emit("honorLossNegated", $"{name} does not lose Honor from their own {Definition(from).Name} ({reason}; The Forgotten Temple).", playerId, from.Id);
            return 0;
        }
        if (amount < -1)
        {
            Emit("honorLossReduced", $"{name}'s Honor loss from {reason} was reduced to 1 (The Forgotten Temple).", playerId, source?.Id);
            return -1;
        }
        return amount;
    }
}
