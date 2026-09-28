namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 2: the ability data, static rules and execution cases of the twelve Personalities wired in personality-wave-69
// (Hida Horu, Hida Kisada, Hida Komatsu, Hida Takeuchi, Hida Watari, Kaiu Esumi, Kaiu Hideaki, Kuni Shinoda, Toritaka Shishido, Ikoma Shinju, Matsu Hana, Matsu Yuuto).
// The declarative ability data lives here (added to AbilityCatalog.All by one hook) so the shared file only carries one-line hooks. Each card's printed text and reading is at
// its entry below. Akodo Makotai (Tactical Limited: Create a 2F Follower and attach it to Makotai) stays unwired: see tools/content/ee_batch2_blockers.json.
//
// New primitives:
//   * ATTACHED-FROM-HAND RECORD (AttachedFollowerKind): a Follower attached from a hand is recorded on the action that did it (CurrentActionAffected), keyed by the host, so
//     "After the resolution of an action that attached a Follower from your hand to Takeuchi" reads LastResolvedAction. Equip is a universal player action with no catalog
//     entry, so it finishes through FinishEquipTracking (the after-action window) itself, after any entry Reaction of the attached card has been answered.
//   * TIMED PROVINCE STRENGTH THAT ENDS WITH THE COMBAT SEGMENT (ProvinceStrengthEffect.UntilBattle): Kaiu Hideaki's "until the battle's resolution begins".
//   * A STATIC UNIT-WIDE RANGED-ATTACK IMMUNITY (Matsu Yuuto), a STATIC RANGED-ATTACK BONUS BY ARMY (Toritaka Shishido), a STATIC KEYWORD GRANT TO A SOLE FOLLOWER
//     (Hida Watari) and a STATIC "NO FORCE IN A CROWDED ARMY" (Hida Horu), each read where the rule they modify is decided.
public static class EmperorPersonalityBatch2Abilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorPersonalityBatch2(this Dictionary<string, AbilityDefinition> all)
    {
        // Hida Kisada (Emperor) (oracle-l5r-3127): "Reaction: When another player's Battle action would target Kisada: He outmaneuvers it. Choose another Personality you control at
        // Kisada's location. The action targets the Personality instead, if legal." Castle of Order's shape (a targeted-by-action redirect naming one of the reactor's own cards at the
        // affected card's location), the affected card being Kisada himself. "Battle: Target an enemy card with lower Force: Destroy it." is Hida Kaiji's own target category, destroying.
        all["kisada-redirect"] = new("Reaction", "When another player's Battle action would target Kisada: choose another Personality you control at his location. The action targets it instead, if legal", "redirect-target-own-personality-at-source-location",
            Trigger: ReactionTriggers.TargetedByAction);
        all["kisada-destroy"] = new("Battle", "Target an enemy card with lower Force: Destroy it", "enemy-force-lt-self");
        // Hida Komatsu (oracle-l5r-3130): "Battle: Target an enemy Follower or Personality with lower Force and no attachments: Bow it." (Kaiji's category with RequireNoAttachments.)
        // "Battle: Target an enemy attachment: Destroy it." is Akodo Michitsu's own printed action.
        all["komatsu-bow"] = new("Battle", "Target an enemy Follower or Personality with lower Force and no attachments: Bow it", "enemy-force-lt-self", RequireNoAttachments: true);
        all["komatsu-destroy-attachment"] = new("Battle", "Target an enemy attachment: Destroy it", "enemy-attachment");
        // Hida Takeuchi (oracle-l5r-3200): "Reaction: After the resolution of an action that attached a Follower from your hand to Takeuchi: Draw a card." The after-action-resolved
        // window, reading the attach the action recorded (MatchEngine.NoteAttachedFollowerFromHand).
        all["takeuchi-draw"] = new("Reaction", "After the resolution of an action that attached a Follower from your hand to Takeuchi: Draw a card", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Hida Watari (oracle-l5r-3227): "While Watari has exactly one Follower, that Follower has Cavalry." is his Effect (Keyword()). "Fear Battle: Target an enemy unit: Bow all
        // Followers with 3 or lower Force in it." A unit is targeted through its Personality (core rules, Units); the Followers are bowed by the effect, not targeted.
        all["watari-fear-bow"] = new("Battle", "Fear Battle: Target an enemy unit: Bow all Followers with 3 or lower Force in it", "enemy-personality", ActionKeywords: ["Fear"]);
        // Kaiu Esumi (oracle-l5r-4109): "Battle: Ranged 4 Attack. If this targeted a card, give the current battlefield's province +2 or -2 strength." One printed action with a choice
        // between +2 and -2: two catalog entries sharing one use (UsageKey), the choice made when the action is announced (Mitoshi's bow-or-straighten shape). The province change
        // is an effect of the Attack's TARGETING (AttackTargetedACard), like Carpenter Castle's Honor: it applies whether the Attack hits, is negated or redirected, and never
        // when no card was targeted.
        all["esumi-attack-raise"] = new("Battle", "Ranged 4 Attack. If this targeted a card, give the current battlefield's province +2 strength", "attack", UsageKey: "esumi-attack");
        all["esumi-attack-lower"] = new("Battle", "Ranged 4 Attack. If this targeted a card, give the current battlefield's province -2 strength", "attack", UsageKey: "esumi-attack");
        // Kaiu Hideaki (oracle-l5r-4115): "Reaction: After engaging at Hideaki's battlefield: Increase its province's strength by 3 until the battle's resolution begins."
        all["hideaki-province"] = new("Reaction", "After engaging at Hideaki's battlefield: Increase its province's strength by 3 until the battle's resolution begins", "none", Trigger: "after-engaging-at-own-battlefield");
        // Kuni Shinoda (oracle-l5r-4587): "Battle: If any enemy units are at the current battlefield and Shinoda is at another battlefield: You may target another Crab Clan
        // Personality at Shinoda's battlefield. Move Shinoda and the target, if any, to the current battlefield." Both conditions sit before the effects colon, so they are
        // requirements to take the action (ValidateEmperorPersonalityBatch2); the optional target is a slot of 0 to 1 cards; the move is a Move spec (source and the target, if any,
        // to the current battlefield), an action that moves Personalities to the battle (MovesToBattle: Absent, the source and target away from it).
        all["shinoda-move"] = new("Battle", "If any enemy units are at the current battlefield and Shinoda is at another battlefield: You may target another Crab Clan Personality at Shinoda's battlefield. Move Shinoda and the target, if any, to the current battlefield", "slots",
            Slots: [new("target", "crab-personality-at-source-battlefield", "Another Crab Clan Personality at Shinoda's battlefield (optional)", 0, 1)],
            Move: new(MoveSpec.SourceAndTarget, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Ikoma Shinju (oracle-l5r-3638): "Battle: Target a Terrain and an enemy card without attachments: Destroy both targets." Two independent slots, the enemy card first so it is
        // the action's primary target (the card a targeting reaction and a destruction negation read).
        all["shinju-destroy"] = new("Battle", "Target a Terrain and an enemy card without attachments: Destroy both targets", "slots",
            Slots: [new("enemy", "enemy-card-without-attachments", "An enemy card without attachments"), new("terrain", "terrain-at-battle", "A Terrain")]);
        // Matsu Hana (oracle-l5r-4916): "Fear Battle: Target an enemy unit: Bow each card in it with equal or lower Force than Hana. Gain 1 Honor for each card this bowed."
        all["hana-bow"] = new("Battle", "Fear Battle: Target an enemy unit: Bow each card in it with equal or lower Force than Hana. Gain 1 Honor for each card this bowed", "enemy-personality", ActionKeywords: ["Fear"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private const string HoruEffect = "horu-crowded-army-no-force";
    private const string WatariEffect = "watari-sole-follower-cavalry";
    private const string ShishidoEffect = "shishido-ranged-bonus";
    private const string YuutoEffect = "yuuto-unit-ranged-immune";
    // The resolved-action facts an Equip finishes with (Equip is a universal player action, not a catalog entry).
    private const string EquipActionId = "equip";
    private const string AttachedFollowerKindPrefix = "attached-follower-from-hand:";

    // ---- Hida Horu: "Horu does not contribute Force to his army during battle resolution while there are two or more units in his army." ---------------------------------
    // Read where a resolution totals an army (ArmyForces): a unit is a Personality (with everything attached), so the army array's length is its unit count. Only Horu's own
    // Force is withheld -- what is attached to him is a separate card that still contributes.
    private bool HoruContributesNothing(CardInstance card, CardInstance[] army) => Definition(card).Effect == HoruEffect && army.Length >= 2;

    // ---- Hida Watari: "While Watari has exactly one Follower, that Follower has Cavalry." (a live grant read by Keyword()) ----------------------------------------------------
    private bool WatariGrantsCavalry(CardInstance follower) =>
        follower.Zone == Zone.Attached && Definition(follower).Type == CardType.Follower && follower.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host)
        && Definition(host).Effect == WatariEffect && Attachments(host.Id).Count(a => Definition(a).Type == CardType.Follower) == 1;

    // ---- Matsu Yuuto: "Ranged Attacks may not target cards in this unit." -------------------------------------------------------------------------------------------------
    private bool UnitImmuneToRangedAttacks(CardInstance target) => _state.Cards.TryGetValue(UnitHostId(target), out var head) && Definition(head).Effect == YuutoEffect;

    // ---- Toritaka Shishido: "Ranged Attacks from actions performed by cards in Shishido's army have +1 strength." ------------------------------------------------------------
    // Read with the Attack's strength (EffectiveAttackStrength). The performer is the card the action is performed by (an Item's or Spell's: its wielder); a card is in an army
    // when its unit stands on the same side of the same battlefield as Shishido, so it counts for a Follower's Attack too. One +1 for each Shishido in that army.
    private int ShishidoRangedBonus(string abilityId, string? performerId)
    {
        if (AttackKind(abilityId) != "Ranged" || performerId is null || !_state.Cards.TryGetValue(performerId, out var performer)) return 0;
        if (Definition(performer).Type is not (CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell)) return 0;
        if (BattlefieldParent(ActingPersonality(performer)) is not { } unit) return 0;
        return _state.Cards.Values.Count(c => c.OwnerId == unit.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == unit.ProvinceIndex
            && Definition(c).Type == CardType.Personality && Definition(c).Effect == ShishidoEffect);
    }

    // ---- Kaiu Esumi / Kaiu Hideaki: the current battlefield's province (the Defender's) ------------------------------------------------------------------------------------
    private void AttackTargetedACardForBatch2(string abilityId, string? targetId)
    {
        if (targetId is null || CurrentProvinceIndex() is not int province) return;
        var amount = abilityId switch { "esumi-attack-raise" => 2, "esumi-attack-lower" => -2, _ => 0 };
        if (amount != 0) GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, province, amount, "Kaiu Esumi");
    }

    private void GiveProvinceStrengthUntilResolution(int playerId, int provinceIndex, int amount, string source)
    {
        _state.ProvinceStrengthEffects.Add(new(playerId, provinceIndex, ProvinceEffectFlat, amount, _state.TurnNumber, source, _state.BattleNumber));
        Emit("provinceStrengthRaised", $"{source}: {Player(playerId).Name}'s province {provinceIndex + 1} has {amount:+#;-#;0} strength until the battle's resolution begins.", playerId);
    }

    // "Until the battle's resolution begins": the top of ResolveBattle, once both players have passed.
    private void ExpireUntilResolutionProvinceStrength() => _state.ProvinceStrengthEffects.RemoveAll(e => e.UntilBattle is not null);

    // ---- Hida Kisada: the redirect ---------------------------------------------------------------------------------------------------------------------------------------
    // The reactor's own other Personalities at Kisada's location (his own battlefield, or home) that the held action accepts as its target instead of him.
    private IEnumerable<CardInstance> KisadaRedirectCandidates(CardInstance kisada, ReactionSubject subject) =>
        _state.Cards.Values.Where(c => c.OwnerId == kisada.OwnerId && c.Id != kisada.Id && c.Id != subject.TargetId && Definition(c).Type == CardType.Personality && InPlay(c)
                && SameLocation(c, kisada) && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Kuni Shinoda: the conditions of taking the action -----------------------------------------------------------------------------------------------------------------
    private void ValidateEmperorPersonalityBatch2(string id, PlayerState player, CardInstance? source)
    {
        if (id != "shinoda-move") return;
        var current = _state.CurrentBattleProvince ?? throw new RuleViolation("There is no current battlefield.");
        Require(Army(1 - player.Id, current).Any(), "Shinoda's move needs enemy units at the current battlefield.");
        Require(source is { Zone: Zone.Battlefield } shinoda && shinoda.ProvinceIndex != current, "Shinoda must be at another battlefield.");
    }

    // ---- Takeuchi: an attach from a hand, recorded on the action that made it -------------------------------------------------------------------------------------------------
    // Called at every hand-to-attachment site that can attach a Follower; only a Follower is recorded. The record names the HOST (the card the Follower now belongs to) and
    // whose hand it came from.
    private void NoteAttachedFollowerFromHand(CardInstance attachment, CardInstance host)
    {
        if (Definition(attachment).Type == CardType.Follower) RecordActionEffect(host.Id, AttachedFollowerKindPrefix + attachment.OwnerId);
    }

    private bool ActionAttachedFollowerFromHandTo(CardInstance host, int handOwnerId) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.CardId == host.Id && a.Kind == AttachedFollowerKindPrefix + handOwnerId);

    // Equip finishes here: when the action attached a Follower from the hand, its facts are snapshotted and the after-action-resolved window opens for whoever holds a Reaction
    // to it. Returns true when the window opened (the caller returns without ceding priority).
    private bool FinishEquipTracking(int playerId, int closesTo)
    {
        var attached = _state.CurrentActionAffected.Where(a => a.Kind.StartsWith(AttachedFollowerKindPrefix, StringComparison.Ordinal)).ToList();
        if (attached.Count == 0) { _state.CurrentActionAffected = []; return false; }
        var affected = _state.CurrentActionAffected;
        _state.LastResolvedAction = new ResolvedActionFacts { AbilityId = EquipActionId, PlayerId = playerId, SourceId = null, Affected = affected, HonorLosers = _state.CurrentActionHonorLosers };
        _state.CurrentActionAffected = []; _state.CurrentActionHonorLosers = [];
        return OpenAfterActionResolvedWindow(closesTo);
    }

    // The entry Reaction window an attached Follower opened has closed: the Equip (or attach-from-hand action) it belonged to is now finished.
    private void FinishPendingAttachTracking()
    {
        if (_state.PendingChoice is not null || !_state.CurrentActionAffected.Any(a => a.Kind.StartsWith(AttachedFollowerKindPrefix, StringComparison.Ordinal))) return;
        var owner = int.Parse(_state.CurrentActionAffected.First(a => a.Kind.StartsWith(AttachedFollowerKindPrefix, StringComparison.Ordinal)).Kind[AttachedFollowerKindPrefix.Length..]);
        FinishEquipTracking(owner, 1 - owner);
    }

    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Returns true when the action is paused (the caller returns without finishing it).
    private bool RunEmperorPersonalityBatch2Effect(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            case "kisada-redirect": RedirectAction(HeldAction(), target!.Id, source!); return false;
            case "kisada-destroy": DestroyUnitCard(target!, player.Id); return false;
            case "komatsu-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "komatsu-destroy-attachment": DestroyAttachment(target!); return false;
            case "takeuchi-draw": Draw(player); return false;
            case "watari-fear-bow":
                foreach (var follower in Attachments(target!.Id).Where(a => Definition(a).Type == CardType.Follower && CardForce(a) <= 3).ToArray())
                    if (!StalwartNegatesBow(follower, player.Id)) follower.Bowed = true;
                return false;
            case "esumi-attack-raise": case "esumi-attack-lower":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case "hideaki-province":
                if (CurrentProvinceIndex() is int province) GiveProvinceStrengthUntilResolution(1 - _state.ActivePlayerId, province, 3, "Kaiu Hideaki");
                return false;
            case "shinoda-move": ExecuteMove(command, ability, source, target); return false;
            case "shinju-destroy":
            {
                var enemy = SlotCards(command, "enemy").Single(); var terrain = SlotCards(command, "terrain").Single();
                DestroyUnitCard(enemy, player.Id);
                DiscardTerrain(terrain); player.TerrainDestroyedThisBattle = true; // Ring of Water's own recruitment condition (issue #194).
                return false;
            }
            case "hana-bow": HanaBow(source!, target!, player); return false;
            default: throw new RuleViolation("Unknown ability.");
        }
    }

    // Matsu Hana: every card in the targeted unit (the Personality and his Followers) with equal or lower Force than Hana's is bowed; she gains 1 Honor for each card THIS bowed (one
    // already bowed, or whose bowing was negated, is not counted). A gain for a dishonorable performer's controller rehonors her instead (core rules, Dishonor).
    private void HanaBow(CardInstance hana, CardInstance target, PlayerState player)
    {
        var limit = CardForce(hana);
        var bowed = 0;
        foreach (var card in Attachments(target.Id).Where(a => Definition(a).Type == CardType.Follower).Prepend(target).ToArray())
        {
            if (CardForce(card) > limit || card.Bowed) continue;
            if (StalwartNegatesBow(card, player.Id)) continue;
            card.Bowed = true; bowed++;
        }
        if (bowed > 0) GainHonorFromPerformer(hana, bowed, Definition(hana).Name);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorPersonalityBatch2_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorPersonalityBatch2", 100, static abilities => abilities.AddEmperorPersonalityBatch2());
    }
}
