using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #674, THE "SPELLS-RINGS" CLUSTER (spell-wave-55, ring-wave-3): fifteen Spells and Rings of Coils of Madness, Torn Asunder, Gates of Chaos and Aftermath wired from the PRINTED text of
// their scans (the scan wins over the catalogue text; where the scan prints no keyword line the card carries none, and its element is the keyword of its own action: "Air Open", "Fire Battle").
//
//   SPELLS  Among the Clouds, Banished from the Realm, Blistering Rain, Blood Wave, Cast Aside Appearances, Flame Lash, Hidden Power, Mystical Augmentation, Searing Siege, Soul of Earth,
//           Study the Wheel, Tempest of Air, The Tao of Fudo, The Thunderer's Protection.
//   RINGS   False Ring of Fire, False Ring of the Void.
//
// Everything rides on existing mechanisms (declared costs, Slots, the target categories, the sequenced second Attack of Thunder's Wrath, the R4 Honor-change window of Shiro Chugo, the
// Ring-recruit grant of the other False Rings, the targeted-by-action Reaction window). The small shared edits are listed in docs/decisions/2026-10-06-674-spells-rings.md.
//
// READINGS (each is also recorded in the decision document; none is a confirmed ruling):
//   * "Bow" in a printed cost is the bow icon: bow THIS card (the Spell or the Ring). "Bow this Shugenja" printed in the effects (Among the Clouds, Tempest of Air, Hidden Power) is an effect,
//     and the "If this Shugenja is unbowed" that precedes it is a condition of the action: the action may not be taken while he is bowed (an action that could do nothing is never offered).
//   * Cast Aside Appearances: "If this Shugenja is Human, permanently give him ..." conditions only that sentence (Coils of Madness rule 6: every other sentence is a plain effect), so a Shugenja who
//     is not Human still has the Spell destroyed and loses 1 Honor. "Nonhuman" is given as a keyword; he keeps whatever Human keyword he printed.
//   * Hidden Power puts the chosen Spell into the hand (it is not a target of a targeted-by-action Reaction: nothing in a discard pile is), then bows the Shugenja and removes the Spell.
//   * Blood Wave: the sacrificed Personality or Follower is the cost card ("your target ... destroy"), chosen with the action and never the wielder himself; its Force is read AFTER its destruction
//     (Torn Asunder ruling: it excludes the Force of its Items), halved, rounded down. The first Attack's target rides on the action, the second is chosen once the first has resolved
//     (Thunder's Wrath's sequenced target); a reaction that negates the action negates both Attacks.
//   * Study the Wheel's Kharmic lasts until the end of the turn (R-001); the card may be an opposing province's (a card in a Province), turned face-up if it was not.
//   * The Thunderer's Protection is offered only when the action has a performer to bow, and bows performers once the action has resolved (never when the action never resolves).
//   * False Ring of Fire's Interrupt is the Honor-change window of Mendon's R4 ruling (Shiro Chugo's), for any player's action; False Ring of the Void's "after a battle resolves where you destroyed a
//     Province" grants its recruit like every other False Ring, and its Engage "will not move this turn" is the lock of Ranged-Attack-style movement refusals (home and to a battlefield, not assignment).
// ======================================================================================================================================================================================

/// <summary>What the cluster keeps between commands.</summary>
public sealed class WlSpellsRingsState
{
    /// <summary>Blood Wave: the strength of both Attacks (half the sacrificed card's Force, rounded down), kept for the second Attack's own strength.</summary>
    public int BloodWaveStrength { get; set; }

    /// <summary>The Thunderer's Protection: performers to bow once the named action has resolved.</summary>
    public List<WlPerformerBow> PerformerBows { get; set; } = [];
}

/// <summary>"Bow the action's performers after the action resolves": the action's key (player|ability|card), the player whose Reaction it was, and the performers.</summary>
public sealed record WlPerformerBow(string ActionKey, int PlayerId, string[] CardIds);

public sealed partial class MatchState
{
    [JsonPropertyOrder(21000)]
    public WlSpellsRingsState WlSpellsRings { get; set; } = new();
}

public static class WlSpellsRingsAbilities
{
    public const string AmongClouds = "wl-among-the-clouds-cavalry", Banished = "wl-banished-expendable", RainPenalty = "wl-blistering-rain-penalty",
        BloodWave = "wl-blood-wave-attack", BloodWaveSecond = "wl-blood-wave-attack-second", CastAside = "wl-cast-aside-appearances", CastAsideBow = "wl-cast-aside-bow",
        FlameLash = "wl-flame-lash-destroy", HiddenPower = "wl-hidden-power-return", MysticalAugmentation = "wl-mystical-augmentation-boost", SearingSiege = "wl-searing-siege-attack",
        SoulOfEarth = "wl-soul-of-earth-boost", StudyWheel = "wl-study-the-wheel-kharmic", TempestOfAir = "wl-tempest-of-air-penalty",
        TaoStraighten = "wl-tao-of-fudo-straighten", TaoHand = "wl-tao-of-fudo-hand", ThunderersBow = "wl-thunderers-protection-bow",
        FalseFireRecruit = "wl-false-ring-of-fire-recruit", FalseFireReduce = "wl-false-ring-of-fire-reduce",
        FalseVoidRecruit = "wl-false-ring-of-void-recruit", FalseVoidLock = "wl-false-ring-of-void-lock";

    /// <summary>Target categories: a Personality with Force equal to or less than this Shugenja's Chi (Among the Clouds); your Spirit card in play (Banished from the Realm); an enemy card with lower Force
    /// than the Personality that has the ability (Cast Aside Appearances' granted action).</summary>
    public const string ForceLteWielderChi = "wl-personality-force-lte-wielder-chi", OwnSpirit = "wl-own-spirit", EnemyCardForceLtSelf = "wl-enemy-card-force-lt-self";

    /// <summary>The Effect key of The Thunderer's Protection's static "+4F".</summary>
    public const string ThunderersEffect = "wl-thunderers-protection";

    public static Dictionary<string, AbilityDefinition> AddWlSpellsRings(this Dictionary<string, AbilityDefinition> all)
    {
        // Among the Clouds (oracle-l5r-10542, Coils of Madness): "Air Open: If this Shugenja is unbowed, target a Personality with Force equal to or less than this Shugenja's Chi. Give the target and each
        // of his Followers Cavalry. Bow this Shugenja."
        all[AmongClouds] = new("Open", "Air Open: If this Shugenja is unbowed, target a Personality with Force equal to or less than this Shugenja's Chi. Give the target and each of his Followers Cavalry. Bow this Shugenja",
            ForceLteWielderChi, ActionKeywords: ["Air"]);
        // Banished from the Realm (oracle-l5r-10913, Aftermath): "Open, bow: Give your target Spirit Expendable (Draw a card after your Expendable card is destroyed)."
        all[Banished] = new("Open", "Open, bow this card: Give your target Spirit Expendable", OwnSpirit, Costs: ActionCost.BowSource);
        // Blistering Rain (oracle-l5r-10914, Aftermath): "Water Battle, bow: Give a target Personality or Follower a Force penalty equal to your target Personality's Chi."
        all[RainPenalty] = new("Battle", "Water Battle, bow this card: Give a target Personality or Follower a Force penalty equal to your target Personality's Chi", "slots", ActionKeywords: ["Water"], Costs: ActionCost.BowSource,
            Slots: [new("target", "personality-or-follower", "A Personality or Follower to give the Force penalty"), new("chi", "own-personality", "Your Personality, whose Chi is the penalty")]);
        // Blood Wave (oracle-l5r-10348, Torn Asunder, Maho): "Battle: Even if this unit is not at the current battlefield, bow this Shugenja and destroy your target Personality or Follower: Two consecutive
        // Ranged Attacks, each with strength equal to half your target's Force, rounded down." The sacrificed card is the cost card (CardIds); the first Attack's target is the action's, the second is chosen
        // after the first has resolved.
        all[BloodWave] = new("Battle", "Even if this unit is not at the current battlefield, bow this Shugenja and destroy your target Personality or Follower: Two consecutive Ranged Attacks, each with strength equal to half your target's Force, rounded down",
            "attack", SourceUnitMayBeAway: true, Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice, CostSubject.Chosen)]);
        all[BloodWaveSecond] = new("Battle", "The second of Blood Wave's two consecutive Ranged Attacks", "attack");
        // Cast Aside Appearances (oracle-l5r-10703, Gates of Chaos): "Earth Limited, bow: If this Shugenja is Human, permanently give him Nonhuman, +2F, and the ability, \"Battle: Bow a target enemy card
        // with lower Force.\" Destroy this Spell. Lose 1 Honor."
        all[CastAside] = new("Limited", "Earth Limited, bow this card: If this Shugenja is Human, permanently give him Nonhuman, +2F, and the ability, \"Battle: Bow a target enemy card with lower Force.\" Destroy this Spell. Lose 1 Honor",
            "none", ActionKeywords: ["Earth"], Costs: ActionCost.BowSource);
        all[CastAsideBow] = new("Battle", "Battle: Bow a target enemy card with lower Force", EnemyCardForceLtSelf);
        // Flame Lash (oracle-l5r-10539, Coils of Madness): "Fire Open, bow: Destroy a target attachment."
        all[FlameLash] = new("Open", "Fire Open, bow this card: Destroy a target attachment", "any-attachment", ActionKeywords: ["Fire"], Costs: ActionCost.BowSource);
        // Hidden Power (oracle-l5r-10536, Coils of Madness, Unique): "Limited: If this Shugenja is unbowed, put a Spell in your discard pile into your hand. Bow this Shugenja. Remove this Spell from the game."
        all[HiddenPower] = new("Limited", "If this Shugenja is unbowed, put a Spell in your discard pile into your hand. Bow this Shugenja. Remove this Spell from the game", "own-card-in-discard");
        // Mystical Augmentation (oracle-l5r-10918, Aftermath): "Void Battle, bow: Give a target Personality +1F, or +2F if this Shugenja or the Personality is Void or a Yojimbo."
        all[MysticalAugmentation] = new("Battle", "Void Battle, bow this card: Give a target Personality +1F, or +2F if this Shugenja or the Personality is Void or a Yojimbo", "personality", ActionKeywords: ["Void"], Costs: ActionCost.BowSource);
        // Searing Siege (oracle-l5r-10921, Aftermath): "Repeatable Fire Battle: Discard a card to make a Ranged Attack with strength equal to its Focus Value or this Shugenja's Chi, whichever is higher."
        all[SearingSiege] = new("Battle", "Repeatable Fire Battle: Discard a card to make a Ranged Attack with strength equal to its Focus Value or this Shugenja's Chi, whichever is higher", "attack",
            ActionKeywords: ["Fire"], UsesPerTurn: AbilityUseLimit.Unlimited, Costs: [new(CostVerbs.Discard, CostSubject.Chosen)]);
        // Soul of Earth (oracle-l5r-10538, Coils of Madness): "Earth Battle, bow: Give your target defending Personality a Force bonus equal to his Personal Honor plus 1."
        all[SoulOfEarth] = new("Battle", "Earth Battle, bow this card: Give your target defending Personality a Force bonus equal to his Personal Honor plus 1", "own-defending-personality", ActionKeywords: ["Earth"], Costs: ActionCost.BowSource);
        // Study the Wheel (oracle-l5r-10706, Gates of Chaos): "Void Limited, bow: Turn a card in a Province face-up (if needed) and give it Kharmic."
        all[StudyWheel] = new("Limited", "Void Limited, bow this card: Turn a card in a Province face-up (if needed) and give it Kharmic", "any-province", ActionKeywords: ["Void"], Costs: ActionCost.BowSource);
        // Tempest of Air (oracle-l5r-10537, Coils of Madness): "Air Open: If this Shugenja is unbowed, give a target Personality or Follower a Force penalty equal to this Shugenja's Chi. Bow this Shugenja."
        all[TempestOfAir] = new("Open", "Air Open: If this Shugenja is unbowed, give a target Personality or Follower a Force penalty equal to this Shugenja's Chi. Bow this Shugenja", "personality-or-follower", ActionKeywords: ["Air"]);
        // The Tao of Fudo (oracle-l5r-10709, Gates of Chaos): "Battle/Open: Target your Ring in your discard pile or in play. Straighten it or put it in your hand. Destroy this Spell." Two entries, one use.
        all[TaoStraighten] = new("BattleOrOpen", "Battle/Open: Target your Ring in play. Straighten it. Destroy this Spell", "ring-in-play-or-own-discard", UsageKey: "wl-tao-of-fudo");
        all[TaoHand] = new("BattleOrOpen", "Battle/Open: Target your Ring in your discard pile or in play. Put it in your hand. Destroy this Spell", "ring-in-play-or-own-discard", UsageKey: "wl-tao-of-fudo");
        // The Thunderer's Protection (oracle-l5r-10355, Torn Asunder, Armor Thunder): "This Personality has +4F." (its Effect) and "Reaction: After an action targets a card in this unit, bow this card: Bow the
        // action's performers (if any) after the action resolves."
        all[ThunderersBow] = new("Reaction", "After an action targets a card in this unit, bow this card: Bow the action's performers (if any) after the action resolves", "none", Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.BowSource);
        // False Ring of Fire (oracle-l5r-10533, Coils of Madness): "Open: If your maximum hand size is 10 or more, put this Ring into play and lose 2 Honor." and "Interrupt, bow: Reduce all Honor gains and
        // losses from an action by 2." (The Honor-change window of Shiro Chugo.)
        all[FalseFireRecruit] = new("Open", "If your maximum hand size is 10 or more, put this Ring into play and lose 2 Honor", "none");
        all[FalseFireReduce] = new("Reaction", "Interrupt, bow this card: Reduce all Honor gains and losses from an action by 2", "none", Trigger: HonorChangeReactions.Trigger, Costs: ActionCost.BowSource);
        // False Ring of the Void (oracle-l5r-10534, Coils of Madness): "After a battle resolves where you destroyed a Province, you may put this Ring into play and lose 2 Honor." (granted like the other False
        // Rings') and "Engage, bow: A target Personality at any location will not move this turn."
        all[FalseVoidRecruit] = new("Battle", "After a battle resolves where you destroyed a Province, you may put this Ring into play and lose 2 Honor", "none", Absent: true);
        all[FalseVoidLock] = new("Reaction", "Engage, bow this card: A target Personality at any location will not move this turn", "personality-at-any-location", Trigger: "after-engaging", Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private WlSpellsRingsState Wl => _state.WlSpellsRings;

    // ---- target categories ----------------------------------------------------------------------------------------------------------------------------------------------

    private bool WlTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var own = target.OwnerId == player.Id;
        switch (category)
        {
            // Among the Clouds: "a Personality with Force equal to or less than this Shugenja's Chi" -- either player's, the Shugenja himself included.
            case WlSpellsRingsAbilities.ForceLteWielderChi:
                return type == CardType.Personality && InPlay(target) && source is not null && WielderOf(source) is { } wielder && CardForce(target) <= PersonalityChi(wielder);
            // Banished from the Realm: "your target Spirit" -- a Spirit card of yours in play.
            case WlSpellsRingsAbilities.OwnSpirit:
                return own && InPlay(target) && Keyword(target, "Spirit");
            // Cast Aside Appearances' granted action: "a target enemy card with lower Force" than this Personality (a Personality or Follower has a Force to compare).
            case WlSpellsRingsAbilities.EnemyCardForceLtSelf:
                return !own && InPlay(target) && source is not null && (type == CardType.Personality || type == CardType.Follower && target.Zone == Zone.Attached) && CardForce(target) < CardForce(source);
            default:
                return false;
        }
    }

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlSpellsRings(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If this Shugenja is unbowed": the action is only taken while he is.
            case WlSpellsRingsAbilities.AmongClouds:
            case WlSpellsRingsAbilities.TempestOfAir:
                Require(source is not null && WielderOf(source) is { Bowed: false }, "This Shugenja must be unbowed.");
                break;
            // Hidden Power: an unbowed Shugenja, and a Spell in the acting player's own Fate discard pile.
            case WlSpellsRingsAbilities.HiddenPower:
                Require(source is not null && WielderOf(source) is { Bowed: false }, "This Shugenja must be unbowed.");
                Require(command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var spell) && spell.OwnerId == player.Id && spell.Zone == Zone.FateDiscard && Definition(spell).Type == CardType.Spell,
                    "Choose a Spell in your discard pile.");
                break;
            // The Tao of Fudo: "your Ring" -- in play or in your own Fate discard pile; only a Ring in play can be straightened.
            case WlSpellsRingsAbilities.TaoStraighten:
                Require(command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var inPlayRing) && inPlayRing.OwnerId == player.Id && InPlay(inPlayRing), "Choose your Ring in play.");
                break;
            case WlSpellsRingsAbilities.TaoHand:
                Require(command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var handRing) && handRing.OwnerId == player.Id && (InPlay(handRing) || handRing.Zone == Zone.FateDiscard), "Choose your Ring in play or in your discard pile.");
                break;
            // Study the Wheel: "a card in a Province": the named province holds one (face-up or face-down).
            case WlSpellsRingsAbilities.StudyWheel:
                Require(command.TargetPlayerId is int owner && command.Province is int index && owner is 0 or 1 && index >= 0 && index < Player(owner).Provinces.Count && Player(owner).Provinces[index].CardId is not null,
                    "Choose a province that holds a card.");
                break;
            // Searing Siege: "Discard a card": a card in the acting player's hand.
            case WlSpellsRingsAbilities.SearingSiege:
                Require(ChosenCostCard(command) is { } discard && discard.Zone == Zone.Hand && player.Hand.Contains(discard.Id) && discard.Id != source?.Id, "Choose a card in your hand to discard.");
                break;
            // Blood Wave: "destroy your target Personality or Follower": one of the acting player's own at the battlefield (a Battle action's target), never the wielder who performs it.
            case WlSpellsRingsAbilities.BloodWave:
                Require(ChosenCostCard(command) is { } sacrificed && sacrificed.OwnerId == player.Id && InPlay(sacrificed)
                    && (Definition(sacrificed).Type == CardType.Personality || Definition(sacrificed).Type == CardType.Follower && sacrificed.Zone == Zone.Attached)
                    && AtBattle(sacrificed) && sacrificed.Id != (source is null ? null : WielderOf(source)?.Id) && sacrificed.Id != source?.Id,
                    "Choose your own Personality or Follower at the battlefield (not the Shugenja himself) to destroy.");
                break;
            // False Ring of Fire: put into play from the hand, if the player's maximum hand size is 10 or more.
            case WlSpellsRingsAbilities.FalseFireRecruit:
                Require(source is { Zone: Zone.Hand } && player.Hand.Contains(source.Id), "A Ring is put into play from your hand.");
                Require(MaxHandSize(player.Id) >= 10, "Your maximum hand size must be 10 or more.");
                RequireWlRingMayEnterPlay(player);
                break;
            // False Ring of the Void: put into play from the hand, as the recruit granted after a battle where its player destroyed a Province.
            case WlSpellsRingsAbilities.FalseVoidRecruit:
                Require(source is { Zone: Zone.Hand } && player.Hand.Contains(source.Id), "A Ring is put into play from your hand.");
                Require(_state.AdditionalActions.Any(g => g.PlayerId == player.Id && GrantUsableNow(g) && g.ScopeCardIds is { Length: 1 } ids && ids[0] == source!.Id), "This Ring's recruitment condition has not been granted at this battle.");
                RequireWlRingMayEnterPlay(player);
                break;
        }
    }

    // Brotherhood Schism: "Rings will not enter play if their owner has already put a Ring into play this turn" (the same test every other Ring recruit asks).
    private void RequireWlRingMayEnterPlay(PlayerState player) =>
        Require(_state.RingSchismTurn != _state.TurnNumber || player.RingPutIntoPlayTurn != _state.TurnNumber, "Brotherhood Schism: you already put a Ring into play this turn.");

    // ---- attacks ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private int? WlAttackStrength(string abilityId, CardInstance? source) => abilityId switch
    {
        // Blood Wave: both Attacks have strength equal to half the sacrificed card's Force, rounded down (read once the card was destroyed).
        WlSpellsRingsAbilities.BloodWave or WlSpellsRingsAbilities.BloodWaveSecond => Wl.BloodWaveStrength,
        _ => null
    };

    private static string? WlAttackKind(string abilityId) => abilityId is WlSpellsRingsAbilities.BloodWave or WlSpellsRingsAbilities.BloodWaveSecond or WlSpellsRingsAbilities.SearingSiege ? "Ranged" : null;

    // Searing Siege's strength: the discarded card's Focus Value or this Shugenja's Chi, whichever is higher.
    private int WlSearingSiegeStrength(GameCommand command, CardInstance shugenja) =>
        Math.Max(ChosenCostCard(command) is { } discarded ? Definition(discarded).Focus : 0, PersonalityChi(shugenja));

    // ---- The Thunderer's Protection -------------------------------------------------------------------------------------------------------------------------------------

    // "+4F" for each of these Spells on the Personality.
    private int WlThunderersForce(CardInstance card) =>
        Definition(card).Type == CardType.Personality ? 4 * Attachments(card.Id).Count(a => Definition(a).Type == CardType.Spell && Definition(a).Effect == WlSpellsRingsAbilities.ThunderersEffect) : 0;

    // The performers of the held action: the named performer (or the source resolved to its wielder) and every card of a performing slot.
    private string[] WlActionPerformers(GameCommand command)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability)) return [];
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        var ids = new List<string>();
        if (ActionPerformerIdOrNull(ability, command, source) is { } performer) ids.Add(performer);
        foreach (var slot in (ability.Slots ?? []).Where(x => x.Performing))
            foreach (var card in SlotCards(command, slot.Id)) ids.Add(card.Id);
        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    // The Reaction applies when the action targets a card in this Spell's unit and has a performer to bow (no window opens for nothing).
    private bool WlTargetedReaction(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        if (abilityId != WlSpellsRingsAbilities.ThunderersBow) return false;
        return card.Zone == Zone.Attached && InPlay(affected) && UnitHostId(affected) == UnitHostId(card) && WlActionPerformers(subject.Command).Any(p => _state.Cards.TryGetValue(p, out var performer) && InPlay(performer));
    }

    // FinishActionEffectTracking's hook: the action named by a queued bow has resolved.
    private void WlAfterActionResolved(string abilityId, int playerId, string? sourceId)
    {
        if (Wl.PerformerBows.Count == 0) return;
        // A deferred Attack finishes under its performer's id, an ordinary action under its card's: the action is named by its player and ability.
        var key = $"{playerId}|{abilityId}|";
        foreach (var due in Wl.PerformerBows.Where(b => b.ActionKey.StartsWith(key, StringComparison.Ordinal)).ToArray())
        {
            Wl.PerformerBows.Remove(due);
            foreach (var id in due.CardIds)
            {
                if (!_state.Cards.TryGetValue(id, out var performer) || !InPlay(performer) || performer.Bowed) continue;
                if (StalwartNegatesBow(performer, due.PlayerId)) continue;
                performer.Bowed = true;
                Emit("bowed", $"{Definition(performer).Name} is bowed after the action resolved (The Thunderer's Protection).", performer.OwnerId, performer.Id);
            }
        }
    }

    // ---- False Rings --------------------------------------------------------------------------------------------------------------------------------------------------------

    // GainHonor's window: Reaction "before an Honor gain or loss from an action's effects" (R4) applies to any change, while the Ring is able to bow (the caller checks that).
    private bool? WlHonorChangeApplies(CardInstance card, string abilityId) => abilityId == WlSpellsRingsAbilities.FalseFireReduce ? card.Zone == Zone.Home : null;

    // After a battle resolves where its player destroyed a Province: the False Ring of the Void in his hand may be put into play.
    private void WlGrantVoidRecruit(int playerId, bool destroyedProvince) => GrantRingRecruit(Player(playerId), destroyedProvince, WlSpellsRingsAbilities.FalseVoidRecruit);

    private void WlPutRingIntoPlay(PlayerState player, CardInstance ring)
    {
        // The Honor is lost while the Ring is still in the hand: a Ring just put into play could otherwise offer to interrupt the very loss that came with it.
        GainHonor(player.Id, -2, Definition(ring).Name);
        player.Hand.Remove(ring.Id); ring.Zone = Zone.Home; ring.FaceUp = true;
        RingEntersPlay(player, ring, true, ring, WlSpellsRingsAbilities.FalseFireRecruit); // issue #699 (WlRings.cs)
    }

    // ---- Blood Wave's second Attack --------------------------------------------------------------------------------------------------------------------------------------

    // The deferred first Attack's own completion (ResolveAttack): the second target is handed over exactly as the immediate path does (Thunder's Wrath's shape).
    private bool BeginWlBloodWaveFollowUpAfterDeferredAttack(PendingAttack attack, List<AdditionalActionGrant> grantsBeforeAttack)
    {
        if (attack.AbilityId != WlSpellsRingsAbilities.BloodWave || attack.Negated || attack.Command is null || attack.SourceId is null || !_state.Cards.TryGetValue(attack.SourceId, out var shugenja) || !InPlay(shugenja)) return false;
        var bowedNow = _state.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return BeginSequencedAttackTarget(WlSpellsRingsAbilities.BloodWaveSecond, attack.Command, shugenja, attack.Additional, false, null, bowedNow, grantsBeforeAttack);
    }

    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this cluster's. false: the effects are done. true: a window opened (an Attack's reaction window, a sequenced target).
    private bool? RunWlSpellsRings(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        var wielder = source is null ? null : WielderOf(source);
        switch (id)
        {
            case WlSpellsRingsAbilities.AmongClouds:
                // "Give the target and each of his Followers Cavalry. Bow this Shugenja." (until the end of the turn, R-001)
                if (wielder is { Bowed: false })
                {
                    GrantKeyword(target!, "Cavalry", source!.Id, GrantDuration.Turn);
                    foreach (var follower in Attachments(target!.Id).Where(a => Definition(a).Type == CardType.Follower).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray())
                        GrantKeyword(follower, "Cavalry", source.Id, GrantDuration.Turn);
                    wielder.Bowed = true;
                }
                return false;
            case WlSpellsRingsAbilities.Banished:
                GrantKeyword(target!, "Expendable", source!.Id, GrantDuration.Turn);
                return false;
            case WlSpellsRingsAbilities.RainPenalty:
                {
                    var penalised = SlotCards(command, "target").FirstOrDefault();
                    var chiSource = SlotCards(command, "chi").FirstOrDefault();
                    if (penalised is not null && chiSource is not null && InPlay(penalised)) ApplyModifier(penalised, source!.Id, id, "Force", -PersonalityChi(chiSource));
                    return false;
                }
            case WlSpellsRingsAbilities.BloodWave:
                {
                    // The sacrificed card was destroyed as the cost: its Force counts after its destruction (Torn Asunder ruling), halved and rounded down.
                    var sacrificed = ChosenCostCard(command)!;
                    Wl.BloodWaveStrength = CardForce(sacrificed) / 2;
                    var shugenja = ActionPerformer(ability, command, source);
                    if (ResolveOrDeferAttack(id, player, target?.Id, shugenja.Id, consumedGrant)) return true;
                    if (!InPlay(shugenja)) return false;
                    return BeginSequencedAttackTarget(WlSpellsRingsAbilities.BloodWaveSecond, command, shugenja, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case WlSpellsRingsAbilities.CastAside:
                if (wielder is not null && Keyword(wielder, "Human"))
                {
                    GiveKeywordPermanently(wielder, "Nonhuman", source!.Id);
                    AddPermanentModifier(wielder, source.Id, id, "Force", 2);
                    GrantAbility(wielder, WlSpellsRingsAbilities.CastAsideBow, source.Id, GrantDuration.Permanent);
                    Emit("abilityGranted", $"{Definition(wielder).Name} permanently gains Battle: {AbilityCatalog.All[WlSpellsRingsAbilities.CastAsideBow].Label}", player.Id, wielder.Id);
                }
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                GainHonor(player.Id, -1, Definition(source!).Name, source);
                return false;
            case WlSpellsRingsAbilities.CastAsideBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case WlSpellsRingsAbilities.FlameLash:
                DestroyTargetCard(target!, player.Id);
                return false;
            case WlSpellsRingsAbilities.HiddenPower:
                if (wielder is { Bowed: false })
                {
                    ReturnToHand(target!);
                    wielder.Bowed = true;
                }
                if (source is { Zone: Zone.Attached }) RemoveFromGame(source);
                return false;
            case WlSpellsRingsAbilities.MysticalAugmentation:
                {
                    var strong = wielder is not null && (Keyword(wielder, "Void") || Keyword(wielder, "Yojimbo")) || Keyword(target!, "Void") || Keyword(target!, "Yojimbo");
                    ApplyModifier(target!, source!.Id, id, "Force", strong ? 2 : 1);
                    return false;
                }
            case WlSpellsRingsAbilities.SearingSiege:
                {
                    var shugenja = ActionPerformer(ability, command, source);
                    return ResolveOrDeferAttack(id, player, target?.Id, shugenja.Id, consumedGrant, WlSearingSiegeStrength(command, shugenja));
                }
            case WlSpellsRingsAbilities.SoulOfEarth:
                ApplyModifier(target!, source!.Id, id, "Force", CardPersonalHonor(target!) + 1);
                return false;
            case WlSpellsRingsAbilities.StudyWheel:
                {
                    var owner = Player(command.TargetPlayerId!.Value);
                    var province = owner.Provinces[command.Province!.Value];
                    var card = Card(province.CardId);
                    if (!card.FaceUp)
                    {
                        card.FaceUp = true;
                        Emit("provinceRevealed", $"{Definition(source!).Name}: {Definition(card).Name} in {owner.Name}'s province {province.Index + 1} is turned face-up.", player.Id, card.Id);
                    }
                    GrantKeyword(card, "Kharmic", source!.Id, GrantDuration.Turn);
                    return false;
                }
            case WlSpellsRingsAbilities.TempestOfAir:
                if (wielder is { Bowed: false })
                {
                    ApplyModifier(target!, source!.Id, id, "Force", -PersonalityChi(wielder));
                    wielder.Bowed = true;
                }
                return false;
            case WlSpellsRingsAbilities.TaoStraighten:
                Straighten(target!);
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                return false;
            case WlSpellsRingsAbilities.TaoHand:
                ReturnToHand(target!);
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                return false;
            case WlSpellsRingsAbilities.ThunderersBow:
                {
                    var held = HeldAction();
                    var performers = WlActionPerformers(held.Command);
                    if (performers.Length > 0) Wl.PerformerBows.Add(new WlPerformerBow(ActionKey(held.Command), player.Id, performers));
                    Emit("performersMarked", $"{Definition(source!).Name}: the action's performers will be bowed after it resolves.", player.Id, source!.Id);
                    return false;
                }
            case WlSpellsRingsAbilities.FalseFireRecruit:
            case WlSpellsRingsAbilities.FalseVoidRecruit:
                WlPutRingIntoPlay(player, source!);
                return false;
            case WlSpellsRingsAbilities.FalseFireReduce:
                if (_state.PendingHonorChange is { } held2) _state.HonorReducedActionKey = held2.ActionKey;
                Emit("honorChangeReduction", $"{Definition(source!).Name}: every Honor gain or loss from the action's effects is reduced by 2.", player.Id, source!.Id);
                return false;
            case WlSpellsRingsAbilities.FalseVoidLock:
                LockMovement(target!, Definition(source!).Name, MovementKinds.Home | MovementKinds.ToBattlefield);
                return false;
            default:
                return null;
        }
    }

    // ---- the hooks into the shared engine files ------------------------------------------------------------------------------------------------------------------------------

    // The abilities that carry the card their cost spends on CardIds (Ikoma Shika - exp's idiom), the cards offered for it, and the verb of the offered action's label.
    private static bool WlCarriesCostCard(string abilityId) => abilityId is WlSpellsRingsAbilities.SearingSiege or WlSpellsRingsAbilities.BloodWave;

    private string?[]? WlCostCardChoices(string abilityId, PlayerState player, CardInstance? source) => abilityId switch
    {
        WlSpellsRingsAbilities.SearingSiege => player.Hand.Where(h => h != source?.Id).Select(h => (string?)h).DefaultIfEmpty(null).ToArray(),
        // Blood Wave: one offered action per Personality or Follower of the acting player's at the battlefield, other than the wielder.
        WlSpellsRingsAbilities.BloodWave => _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && AtBattle(c) && c.Id != source?.Id && c.Id != (source is null ? null : WielderOf(source)?.Id)
            && (Definition(c).Type == CardType.Personality || Definition(c).Type == CardType.Follower && c.Zone == Zone.Attached)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).DefaultIfEmpty(null).ToArray(),
        _ => null
    };

    private static string? WlCostCardVerb(string abilityId) => abilityId switch
    {
        WlSpellsRingsAbilities.SearingSiege => "discards",
        WlSpellsRingsAbilities.BloodWave => "destroys",
        _ => null
    };

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlSpellsRingsHooks()
    {
        EngineHooks.Abilities.Register("AddWlSpellsRings", 15000, static abilities => abilities.AddWlSpellsRings());
        EngineHooks.TargetAllows.Register("WlSpellsRingsTargetAllows", 15000, static (engine, context) => engine.WlTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidateWlSpellsRings", 15000, static (engine, context) => engine.ValidateWlSpellsRings(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunWlSpellsRings", 15000, static (engine, context) => engine.RunWlSpellsRings(context.id, context.ability, context.command, context.player, context.source, context.target,
            context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.AttackStrengthAfter.Register("WlSpellsRingsAttackStrength", 15000, static (engine, context) => engine.WlAttackStrength(context.abilityId, null));
        EngineHooks.AttackKind.Register("WlSpellsRingsAttackKind", 15000, static context => WlAttackKind(context.abilityId));
        EngineHooks.PersonalityForce.Register("WlSpellsRingsForce", 15000, static (engine, context) => engine.WlThunderersForce(context.card));
        EngineHooks.BowsTarget.Register("WlSpellsRingsBowsTarget", 15000, static context => context.abilityId is WlSpellsRingsAbilities.CastAsideBow);
        EngineHooks.DestroysTarget.Register("WlSpellsRingsDestroysTarget", 15000, static context => context.abilityId is WlSpellsRingsAbilities.FlameLash);
        EngineHooks.TargetedReaction.Register("WlSpellsRingsTargetedReaction", 15000, static (engine, context) => engine.WlTargetedReaction(context.card, context.abilityId, context.subject, context.affected));
    }
}
