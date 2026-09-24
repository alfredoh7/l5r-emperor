namespace L5R.Core;

// The Shadow's Embrace pass (issue #476), batch 2: the execution cases and the small static rules of the Personalities wired in personality-wave-75. Each card's
// printed text and reading is at its AbilityCatalog entry (CardAbilities.cs); this file holds the helpers, so the shared files only carry one-line hooks.
// Mirrors ForgottenLegacyBatch2.cs's own shape.
//
//   * A HOST-SIDE ATTACH REFUSAL (HostRefusesAttachment): "Will not attach cards" (Longtooth, Niiro no Oni) and "Will not attach Followers or Items" (Natsumi) --
//     the Personality refuses the attachment, not the attachment the Personality. Read inside AttachmentAllowed, which every attach funnel already asks (Equip,
//     Equip (Battle), an attach from hand or a discard pile, a transfer, a Focus Effect attach, a created attachment's host), so no funnel can disagree.
//   * YOYUGI'S LOCK: every printed ("base") ability of the target may not be used this turn -- Doji Hakuseki's single-ability lock (MatchState.DisabledAbilities)
//     applied to each of them.
//   * YAMAZAKI'S NEGATION: a once-per-phase (PhaseLimits.cs) GainHonor hook, read after every other reduction, on a loss of exactly 1.
//   * KANAHASHI'S GOLD COST: a CardGoldCost term on other players' Personalities and attachments in play.
//   * QUELSA'S DISCOUNT: a term in Equip's price (EquipGoldCost, which an attach-from-a-discard-pile ability also reads) and in the printed-cost price of Equip
//     (Battle) and Low Market, when the Spell goes to Quelsa.
public sealed partial class MatchEngine
{
    private const string RefusesAllAttachmentsEffect = "se2-refuses-all-attachments";
    private const string RefusesFollowersAndItemsEffect = "se2-refuses-followers-and-items";
    private const string MrikaEffect = "se2-mrika";
    private const string QuelsaEffect = "se2-quelsa";
    private const string ShirouForceEffect = "se2-shirou-force";
    private const string ShugoreiForceEffect = "se2-shugorei-force";
    private const string YamazakiNegateEffect = "se2-yamazaki-negate";
    private const string KanahashiGoldCostEffect = "se2-kanahashi-gold-cost";

    // Returns true when the action is paused (an Attack's reaction window): the caller returns without finishing it.
    private bool RunShadowsEmbraceBatch2Effect(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            case "yoyugi-lock": case "yoyugi-lock-free": LockPrintedAbilities(target!, Definition(source!).Name); return false;
            case "longtooth-destroy": DestroyTargetCard(target!, player.Id); return false;
            case "rukia-return":
                ReturnToHand(target!);
                GrantAdditionalAction(player.Id, null);
                return false;
            // Moto Morio: the chosen Follower performs the Attack (Toritaka Chokichi's shape); its strength is the Follower's Force (ShadowsEmbraceBatch2AttackStrength).
            case "morio-attack": return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId!, consumedGrant);
            case "niiro-attack": case "shirou-attack": return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            case "jongho-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "minhee-return": ReturnToHand(target!); return false;
            case "minhee-destroy": MinHeeDestroy(command, player); return false;
            case "nobu-move-home": case "ikke-move-home": ExecuteMove(command, ability, source, target); return false;
            case "kanahashi-move-home":
                ExecuteMove(command, ability, source, target);
                // "If he is Crane Clan, bow him." -- an effect of its own, whether or not the move happened.
                if (HasClanAffiliation(target!, "Crane") && InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "ryoken-dishonor": Dishonor(target!, player.Id); return false;
            // "The enemy leader loses 2 Honor": the other leader of the battle just resolved.
            case "ryoken-honor-loss": GainHonor(1 - player.Id, -2, Definition(source!).Name, source); return false;
            case "shugorei-honor": GainHonorFromPerformer(source!, 2, Definition(source!).Name); return false;
            case "yamazaki-exp-threaten":
            {
                // "Yamazaki threatens him" is flavour (core rules, Abilities); "Straighten him." Then, "if Yamazaki has a Weapon" as the action resolves, the optional bow.
                foreach (var threatened in SlotCards(command, "target").ToArray()) Straighten(threatened);
                if (SlotCards(command, "bow").FirstOrDefault() is { } bowed && HasWeapon(source!) && InPlay(bowed) && !StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                return false;
            }
            // Yasuki Ikke: the held action's target is the Merchant it targeted (TargetedActionReactionApplies).
            case "ikke-negate":
            {
                var held = HeldAction();
                if (held.TargetId is { } merchantId) NegateAllEffectsOn(Card(merchantId));
                return false;
            }
            case "ikke-honor-loss": GainHonor(HeldAction().Command.PlayerId, -2, Definition(source!).Name, source); return false;
            default: throw new RuleViolation("Unknown ability.");
        }
    }

    // ---- validation ----

    private void ValidateShadowsEmbraceBatch2(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Shugorei: "If you are a Dragon Clan player and none of your provinces have been destroyed since your last turn ended" (Ohsuki's Sparrow's own flag).
        if (id == "shugorei-honor")
        {
            Require(player.Clan == "Dragon", "You must be a Dragon Clan player.");
            Require(!_state.ProvinceDestroyedSinceTurnEnd[player.Id], "One of your provinces was destroyed since your last turn ended.");
        }
    }

    // The in-play Target categories of this batch (the shared category switch's fallback): false for any category that is not one of them.
    private bool ShadowsEmbraceBatch2TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var personality = type == CardType.Personality;
        return category switch
        {
            // Kitsuki Yoyugi: the two halves of one printed action, split by whether the bow is owed.
            "se2-unique-non-unicorn-personality" => personality && Keyword(target, "Unique") && !HasClanAffiliation(target, "Unicorn"),
            "se2-non-unique-or-unicorn-personality" => personality && (!Keyword(target, "Unique") || HasClanAffiliation(target, "Unicorn")),
            // Moshi Rukia: "an opposed attachment" -- an attachment whose unit stands at a battlefield where the other side has at least one unit.
            "se2-opposed-attachment" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell
                && Card(UnitHostId(target)) is { Zone: Zone.Battlefield, ProvinceIndex: int attachmentBattle } host && Army(1 - host.OwnerId, attachmentBattle).Any(),
            // Shinjo Jong-Ho: "a card opposing him with lower Force" -- a Personality or Follower on the other side of his battlefield, with less Force than his.
            "se2-card-opposing-source-with-lower-force" => source is { Zone: Zone.Battlefield, ProvinceIndex: int jongHoBattle } && type is CardType.Personality or CardType.Follower
                && Card(UnitHostId(target)) is { Zone: Zone.Battlefield } opposingHost && opposingHost.ProvinceIndex == jongHoBattle && opposingHost.OwnerId != source.OwnerId
                && CardForce(target) < CardForce(source),
            // Yamazaki - Experienced: "another Personality", and "an enemy card" only while he has a Weapon.
            "se2-personality-other-than-source" => personality && target.Id != source?.Id,
            "se2-enemy-card-if-source-has-weapon" => Card(UnitHostId(target)).OwnerId != player.Id && source is not null && HasWeapon(source),
            _ => false
        };
    }

    // The out-of-play Target categories of this batch (Shinjo Min-Hee's Followers in a discard pile and in hand).
    private static bool IsShadowsEmbraceBatch2OutOfPlayTarget(string category) => category is "se2-own-follower-in-discard" or "se2-own-follower-in-hand";

    private void ValidateShadowsEmbraceBatch2OutOfPlayTarget(string category, PlayerState player, CardInstance target)
    {
        var follower = Definition(target).Type == CardType.Follower && target.OwnerId == player.Id;
        if (category == "se2-own-follower-in-discard") Require(follower && target.Zone == Zone.FateDiscard, "Choose a Follower in your discard pile.");
        else Require(follower && target.Zone == Zone.Hand && player.Hand.Contains(target.Id), "Choose a Follower in your hand.");
    }

    private IEnumerable<CardInstance> ShadowsEmbraceBatch2OwnFollowersInDiscard(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Follower && c.Zone == Zone.FateDiscard).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- effects ----

    // Kitsuki Yoyugi: "The target's base abilities may not be used." Every printed action of the card (a UsageKey group counted once), for the turn.
    private void LockPrintedAbilities(CardInstance target, string sourceName)
    {
        var keys = (Definition(target).Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && IsAction(d)).Select(AbilityUsageKey).Distinct(StringComparer.Ordinal).ToArray();
        if (keys.Length == 0) { Emit("effectFailed", $"{sourceName}: {Definition(target).Name} has no printed abilities to lock.", target.OwnerId, target.Id); return; }
        foreach (var key in keys) DisableAbility(target, key, sourceName);
    }

    // Shinjo Min-Hee: "Destroy it. Remove it from the game if it is Dragon Clan. You may discard a Follower (from your hand); if you did, draw a card." The target is destroyed
    // through the ordinary path (the Dishonorable-death penalty and the like still apply), and a Dragon Clan card then leaves its discard pile for good; the optional Follower is
    // discarded and replaced as the action resolves.
    private void MinHeeDestroy(GameCommand command, PlayerState player)
    {
        if (SlotCards(command, "target").FirstOrDefault() is { } target && InPlay(target))
        {
            var dragon = HasClanAffiliation(target, "Dragon");
            DestroyTargetCard(target, player.Id);
            if (dragon && target.Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard) RemoveFromGame(target);
        }
        if (SlotCards(command, "follower").FirstOrDefault() is { Zone: Zone.Hand } follower && player.Hand.Contains(follower.Id))
        {
            DiscardFate(player, follower);
            Draw(player);
        }
    }

    // Shiba Kudome - Experienced: "Destroy the duel's loser. Give Kudome a +1F Fire token if she won and you control a Shugenja." A tie destroys both (both lose).
    private void KudomeExpDuelOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId);
        if (winnerId is not null && Card(winnerId) is { } winner && winner.OwnerId == challengerPlayerId && InPlay(winner) && ControlsKeyword(challengerPlayerId, "Shugenja"))
            CreateToken(winner, "Fire", "kudome-exp-duel", "Force", 1);
    }

    // ---- Attacks ----

    // Moto Morio: "Melee Attack with strength equal to its Force" -- the performing Follower's (the Attack's source) current Force. Niiro no Oni: Melee 5. Shiba Shirou: Melee 4.
    private int? ShadowsEmbraceBatch2AttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        "morio-attack" => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var follower) ? CardForce(follower) : 0,
        "niiro-attack" => 5,
        "shirou-attack" => 4,
        _ => null
    };

    // Niiro no Oni: "Give the target a -5F token (if he is still in play)." -- after the Attack resolves, hit or not.
    private void AfterShadowsEmbraceBatch2Attack(string abilityId, string? targetId, string? sourceId)
    {
        if (abilityId != "niiro-attack" || targetId is null || !_state.Cards.TryGetValue(targetId, out var target) || !InPlay(target)) return;
        CreateToken(target, "-5F", sourceId ?? abilityId, "Force", -5);
    }

    // ---- static traits ----

    private bool HasWeapon(CardInstance personality) => Attachments(personality.Id).Any(a => Keyword(a, "Weapon"));

    // Longtooth / Niiro no Oni: "Will not attach cards." Natsumi: "Will not attach Followers or Items."
    private bool HostRefusesAttachment(CardInstance attachment, CardInstance host) => Definition(host).Effect switch
    {
        RefusesAllAttachmentsEffect => true,
        RefusesFollowersAndItemsEffect => Definition(attachment).Type is CardType.Follower or CardType.Item,
        _ => false
    };

    // M'rika: "You may ignore M'rika's Loyal restriction if your Stronghold is The Forgotten Temple." -- the recruiting player's own Stronghold.
    private bool MrikaLoyalWaived(CardInstance card, int? playerId) =>
        Definition(card).Effect == MrikaEffect && playerId is int owner && Player(owner).StrongholdId is { } strongholdId
        && _state.Cards.TryGetValue(strongholdId, out var stronghold) && Definition(stronghold).Effect == ForgottenTempleEffect;

    // M'rika: "After M'rika enters play: Gain 2 Honor." A trait, so mandatory (ApplyMandatoryEntryEffects).
    private void ApplyShadowsEmbraceBatch2EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect == MrikaEffect && card.OwnerId == playerId) GainHonorFromPerformer(card, 2, Definition(card).Name);
    }

    // Quelsa: "+2F while he has a Spell." Shiba Shirou: "+2F while you control a Shugenja." Shugorei: "-2F while it is your turn." (PersonalityForce floors the total at 0.)
    private int ShadowsEmbraceBatch2Force(CardInstance card) => Definition(card).Effect switch
    {
        QuelsaEffect => Attachments(card.Id).Any(a => Definition(a).Type == CardType.Spell) ? 2 : 0,
        ShirouForceEffect => ControlsKeyword(card.OwnerId, "Shugenja") ? 2 : 0,
        ShugoreiForceEffect => card.OwnerId == _state.ActivePlayerId ? -2 : 0,
        _ => 0
    };

    // Yamazaki - Experienced: "Once per phase, before you lose exactly 1 Honor: Negate the loss." A trait of his controller's Yamazaki in play (mandatory, usable while bowed),
    // spent once per printed phase (PhaseLimits.cs).
    private bool YamazakiNegatesLoss(int playerId, string reason)
    {
        var yamazaki = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == YamazakiNegateEffect).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (yamazaki is null || !TrySpendPhaseUse(yamazaki.AbilityUses, YamazakiNegateEffect)) return false;
        Emit("honorLossNegated", $"{Definition(yamazaki).Name}: {Player(playerId).Name}'s loss of exactly 1 Honor ({reason}) was negated.", playerId, yamazaki.Id);
        return true;
    }

    // Yoritomo Kanahashi: "Other players' Personalities and attachments (in play) have -2 Gold Cost." -- per Kanahashi another player controls; an attachment is controlled by
    // its Personality's controller.
    private int KanahashiGoldCostPenalty(CardInstance card)
    {
        if (!InPlay(card)) return 0;
        var type = Definition(card).Type;
        if (!(type == CardType.Personality || card.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell)) return 0;
        var controller = card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host.OwnerId : card.OwnerId;
        return -2 * _state.Cards.Values.Count(c => c.OwnerId != controller && InPlay(c) && Definition(c).Effect == KanahashiGoldCostEffect);
    }

    // Quelsa: "Quelsa attaches Spells paying 2 less Gold." Equip's price (and an attach that reads it) when the Spell goes to Quelsa.
    private int QuelsaAttachDiscount(CardInstance attachment, CardInstance wielder) =>
        Definition(attachment).Type == CardType.Spell && Definition(wielder).Effect == QuelsaEffect ? 2 : 0;

    // The printed-cost price of Equip (Battle) and Low Market's attach, whose wielder is the performer.
    private int QuelsaSpellDiscount(GameCommand command, CardInstance attachment) =>
        command.AbilityId is "equip-battle" or "low-market-equip" && command.PerformerId is { } wielderId && _state.Cards.TryGetValue(wielderId, out var wielder)
            ? QuelsaAttachDiscount(attachment, wielder) : 0;

    // Test seams (shadows-embrace-batch-2): the Gold Cost a card reads now, and a plain Honor change through the one GainHonor funnel.
    internal int CardGoldCostForTest(string cardId) => CardGoldCost(Card(cardId));
    internal void GainHonorForTest(int playerId, int amount, string reason) => GainHonor(playerId, amount, reason);
}
