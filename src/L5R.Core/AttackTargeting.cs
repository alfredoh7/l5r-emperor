namespace L5R.Core;

/// <summary>
/// The generalized Attack-vs-Followers exception (P1, issue: Bayushi Kahoku - exp). Core rules: a Ranged
/// or Melee Attack targets "a Follower or a Personality WITHOUT Followers in the current enemy army", so
/// a Personality with an attached Follower is immune to a direct Attack -- attack the Follower instead --
/// UNLESS the card producing the Attack, or another card modifying it, explicitly says the Attack MAY
/// target a Personality with attached Followers. Only Follower attachments block direct targeting (an
/// Item or Spell never does). This record is one such explicit permission, in whichever of its five
/// printed shapes:
///   (a) printed on the attacking ability itself, unconditional  -- AbilityDefinition.FollowerException = new("Ranged")
///   (b) conditional on game state (Reconnaissance)              -- new("Ranged", Condition: Reconnaissance)
///   (c) conditional on the TARGET's status (P2)                 -- new("Ranged", TargetIs: TargetStatus.IsDishonorable)
///   (d) a source-scoped static trait ("Ranged Attacks from actions performed by X may target ...") --
///       an entry in MatchEngine.StaticAttackFollowerExceptions keyed by the card's Effect; it applies to
///       whatever ability X performs and never to an Attack performed by any other card.
///   (e) granted by ANOTHER card before the Attack is declared -- an AttackTargetGrant created by
///       MatchEngine.GrantAttackFollowerException (Yoritomo Ebumi's shape; see that method for exactly
///       what a grant can and cannot do).
/// Kind names which Attack kinds it covers: "Ranged", "Melee" or "Both" (Melee Attacks follow identical
/// rules but are not Ranged Attacks, so a card may reach one, the other or both).
/// </summary>
public sealed record AttackFollowerException(string Kind = "Both", string? Condition = null, TargetStatus? TargetIs = null)
{
    /// <summary>Condition: the acting player has Reconnaissance at the current battlefield.</summary>
    public const string Reconnaissance = "reconnaissance";
}

/// <summary>Shape (e): a standing permission a card's action leaves behind for the player's next Attack
/// action of the covered Kind this battle. Battle-scoped and one-shot -- see MatchEngine.GrantAttackFollowerException.</summary>
public sealed record AttackTargetGrant(int PlayerId, AttackFollowerException Exception, string? SourceCardId = null);

public sealed partial class MatchEngine
{
    // ---- P2: the generalized target-status predicate (see TargetStatus) -----------------------------------
    // Any listed term is sufficient; a card satisfying several terms is still exactly one match. The honor
    // terms use the engine's single existing notion of (dis)honor -- CardInstance.Dishonorable on a
    // Personality -- so they always agree with the dishonorable-personality/honorable-personality Target
    // categories and every other `.Dishonorable` reader. Keywords go through the live Keyword() check
    // (printed or granted); Clan terms through HasClanAffiliation; ClanOrKeyword is the "Shadowlands"
    // case (Clan alignment OR the printed keyword Rikate/Takasho/Keppo/Daigotsu Kendo carry instead).
    internal bool MatchesTargetStatus(CardInstance card, TargetStatus status)
    {
        var personality = Definition(card).Type == CardType.Personality;
        if (status.Honorable && personality && !card.Dishonorable) return true;
        if (status.Dishonorable && personality && card.Dishonorable) return true;
        if (status.Keywords is { } keywords && keywords.Any(k => Keyword(card, k))) return true;
        if (status.Clans is { } clans && clans.Any(c => HasClanAffiliation(card, c))) return true;
        if (status.ClanOrKeyword is { } either && either.Any(n => HasClanAffiliation(card, n) || Keyword(card, n))) return true;
        return false;
    }

    // The EFFECT-condition role: does `target` satisfy the status the ability's own text conditions its
    // effect on (AbilityDefinition.IfTarget)? False for no target, an unknown ability, or an ability that
    // declares no such condition -- the caller decides what "no condition" means for its own effect.
    internal bool EffectConditionMet(string abilityId, CardInstance? target) =>
        target is not null && AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.IfTarget is { } status && MatchesTargetStatus(target, status);

    // ---- P1: the Attack-vs-Followers exception ------------------------------------------------------------
    // (d) Source-scoped static traits, keyed by CardDefinition.Effect. The exception belongs to the card
    // carrying the Effect and applies to Attacks from actions PERFORMED BY THAT CARD only -- the performer is
    // what is compared (a Spell/Item action is performed by its wielder), so it can never leak to an Attack
    // performed by anyone else. Bayushi Kahoku - exp: "Ranged Attacks from actions performed by Kahoku may
    // target dishonorable Personalities with attached Followers."
    private static readonly IReadOnlyDictionary<string, AttackFollowerException> StaticAttackFollowerExceptions = new Dictionary<string, AttackFollowerException>
    {
        ["ranged-attacks-may-target-dishonorable-with-followers"] = new("Ranged", TargetIs: TargetStatus.IsDishonorable),
    };

    // True when `target` is a Personality with at least one attached FOLLOWER -- a Follower CARD or a Follower TOKEN (Mendon, 2026-09-24: both shield their Personality from Ranged and Melee Attacks).
    // An Item or Spell attachment never counts (A Close Call's premise -- an Armor-wearing Personality is a legal Attack target); they are attachments only for "without attachments" effects
    // (HasAttachments), and so is a Weapon or Armor token.
    private bool IsFollowedPersonality(CardInstance target) =>
        Definition(target).Type == CardType.Personality
        && (Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower) || target.Tokens.Any(t => t.Type == "Follower"));

    private bool HasReconnaissanceAtCurrentBattlefield(int playerId) =>
        _state.CurrentBattleProvince is int province && HasReconnaissance(playerId, 1 - _state.ActivePlayerId, province);

    // May an Attack from `abilityId`, performed by `performer` for `playerId`, target this (followed)
    // Personality? True when ANY exception applies: printed on the ability (a-c), a static trait of the
    // performing card (d), or a standing grant of this player (e).
    internal bool AttackMayTargetFollowedPersonality(string abilityId, int playerId, CardInstance? performer, CardInstance target)
    {
        var kind = AttackKind(abilityId);
        if (kind == "") return false;
        if (AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.FollowerException is { } printed && AttackFollowerExceptionApplies(printed, kind, playerId, target)) return true;
        if (performer is not null && StaticAttackFollowerExceptions.TryGetValue(Definition(performer).Effect, out var trait) && AttackFollowerExceptionApplies(trait, kind, playerId, target)) return true;
        return _state.AttackTargetGrants.Any(g => g.PlayerId == playerId && AttackFollowerExceptionApplies(g.Exception, kind, playerId, target));
    }

    private bool AttackFollowerExceptionApplies(AttackFollowerException exception, string attackKind, int playerId, CardInstance target)
    {
        if (exception.Kind != "Both" && exception.Kind != attackKind) return false;
        var conditionMet = exception.Condition switch
        {
            null => true,
            AttackFollowerException.Reconnaissance => HasReconnaissanceAtCurrentBattlefield(playerId),
            _ => throw new InvalidOperationException($"Unknown Attack exception condition '{exception.Condition}'.")
        };
        return conditionMet && (exception.TargetIs is null || MatchesTargetStatus(target, exception.TargetIs));
    }

    // (e) THE HOOK for an exception granted by ANOTHER card (an action, Strategy or Reaction "modifying" a
    // later Attack). What it can do: leave a standing permission -- covering `exception.Kind` -- that makes
    // this player's NEXT Attack action of that kind (any ability whose AttackKind matches, from any card
    // the player controls) able to declare a Personality with attached Followers as its target, subject to
    // the exception's own Condition/TargetIs. The grant is consumed when that Attack action executes
    // (whether or not the exception was actually needed) and expires with the battle
    // (FinishBattleResolution). Yoritomo Ebumi is the first card wired through it.
    // What it cannot do: an Attack's target is chosen and validated ONCE, at declaration (the useAbility
    // command carries it), and PendingAttack.TargetId is fixed from then on. So a grant must exist BEFORE
    // the Attack is declared. A Reaction that fires after the declaration -- inside the announce window
    // (Tsuruchi Yakusho's), which opens after validation, or the before-attack-resolves window -- can
    // change the Attack's strength and its target's Force or status (which the resolution-time comparison
    // re-reads) but can neither legalize a target the declaration already rejected nor re-target a
    // pending Attack; a card such as "That Ranged Attack may target a card in the Defender's home" needs a
    // re-selection step in that window, which does not exist. Legality is not re-checked at resolution:
    // a target that was legal when declared stays the Attack's target even if the exception's own
    // condition (say Kahoku's "dishonorable") stops holding before it resolves.
    internal void GrantAttackFollowerException(int playerId, AttackFollowerException exception, string? sourceCardId = null)
    {
        _state.AttackTargetGrants.Add(new(playerId, exception, sourceCardId));
        Emit("attackExceptionGranted", $"{Player(playerId).Name}'s next {(exception.Kind == "Both" ? "Ranged or Melee" : exception.Kind)} Attack action may target a Personality with attached Followers.", playerId, sourceCardId);
    }

    // Called once per executing Attack action (ExecuteAbilityEffects): every standing grant of this player
    // that covers this Attack's kind is spent -- "your next Ranged Attack action" is used up by the next
    // such action whether or not its target needed the exception.
    private void ConsumeAttackTargetGrants(int playerId, string attackKind) =>
        _state.AttackTargetGrants.RemoveAll(g => g.PlayerId == playerId && (g.Exception.Kind == "Both" || g.Exception.Kind == attackKind));

    // DECISION POINT (open rules question, see docs/decisions/2026-09-18-attack-followers-exception-and-target-status.md):
    // when an exception lets an Attack target a Personality WITH Followers, which Force is compared to the
    // Attack's strength -- the Personality's OWN Force (CardForce: printed + modifiers + Item/Spell bonuses,
    // never his Followers'), or the whole UNIT's Force total (Personality plus each attached Follower)? The
    // core rules define an Attack as "If its Force is <= X, destroy it" about the targeted card, and define
    // "Unit Force total" only for battle resolution, so the literal reading -- the targeted Personality's
    // own Force -- is what is wired. Every caller that compares an Attack's target to its strength goes
    // through this one method (declaration-time legality and resolution-time destruction), so a ruling for
    // the unit total is a one-line change here. For every target that needs no exception (a Follower, or a
    // Personality with no Followers) the two readings are identical, so nothing else depends on it.
    private int AttackComparedForce(CardInstance target) => CardForce(target);

    // "Ranged 6 Attack that you compare against Gold Cost" (Tsuruchi Samuru, Emperor Edition Personality pass, issue #477): core rules -- if an effect compares a stat other than Force,
    // that stat is compared to the Attack's strength instead. Every caller that compares an Attack's target to its strength asks here.
    private int AttackComparedValue(string abilityId, CardInstance target) => abilityId == "samuru-attack" ? CardGoldCost(target) : AttackComparedForce(target, abilityId);

    // An Attack that targets the current battlefield's province (issue #335): no card is targeted, so no reaction to a targeted Attack applies and nothing here compares
    // Force. The only such Attack today is the training fixture; a printed card adds its own case.
    private void ResolveProvinceAttack(string abilityId, int provinceIndex, int strength)
    {
        if (abilityId != "training-province-attack") return;
        var owner = 1 - _state.ActivePlayerId;
        var province = Player(owner).Provinces.Single(p => p.Index == provinceIndex);
        province.Strength = Math.Max(0, province.Strength - strength);
        Emit("provinceAttacked", $"{Player(owner).Name}'s province {provinceIndex + 1} lost {strength} strength to a Ranged Attack.", owner);
    }

    // Declaration-time legality of ONE Attack target (shared by every "attack"-category ability's own
    // TargetId and by an ability that carries its Attack target on CardIds, e.g. Bayushi Kahoku - exp,
    // whose TargetId is the separate -4F target). forcePenaltyTargetId/forcePenalty predict a Force
    // penalty the SAME action applies to a target before its Attack resolves ("Give him -4F. Ranged 3
    // Attack." may Attack the very Personality it just weakened).
    private void RequireLegalAttackTarget(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance target,
        string? forcePenaltyTargetId = null, int forcePenalty = 0)
    {
        Require(InPlay(target) && AtBattle(target) && target.OwnerId != player.Id, "Target a card in the current enemy army.");
        var type = Definition(target).Type;
        // Core rules: a Personality with an attached FOLLOWER is immune to a Ranged/Melee Attack (attack the
        // Follower instead), unless the Attack is explicitly allowed to target one -- an exception printed
        // on the ability, a static trait of its performer, or a standing grant (AttackFollowerException).
        // An Item/Spell attachment grants no such immunity -- confirmed by A Close Call's own printed
        // premise ("after a Personality with an ARMOR is targeted by a Melee or Ranged Attack"), which
        // only makes sense if an Armor-wearing Personality is a legal Attack target in the first place.
        // Only a Follower attachment actually blocks direct targeting.
        var performer = ability.Performer is not null && command.PerformerId is not null ? ActingPersonality(Card(command.PerformerId))
            : source is null ? null : ActingPersonality(source);
        Require(type == CardType.Follower || type == CardType.Personality && (!IsFollowedPersonality(target) || AttackMayTargetFollowedPersonality(id, player.Id, performer, target)),
            "Attack a Follower or a Personality without an attached Follower.");
        // Seasoned Ashigaru's shape: an immunity to one specific Attack kind only, checked against the
        // ability's own AttackKind (already tracked per ability id). Effect, not Keywords -- this is a
        // synthetic marker for the card's own unique text, not a printed keyword other cards could reference.
        Require(AttackKind(id) != "Melee" || Definition(target).Effect != "melee-attack-immune", "Melee Attacks may not target this card.");
        // Chiisai (The Shadow's Embrace pass, issue #476 batch 1): "Melee Attacks may not target Chiisai's Followers."
        Require(AttackKind(id) != "Melee" || !IsChiisaisFollower(target), "Melee Attacks may not target Chiisai's Followers.");
        // Matsu Yuuto (Emperor Edition Personality pass, issue #477 batch 2): "Ranged Attacks may not target cards in this unit."
        Require(AttackKind(id) != "Ranged" || !UnitImmuneToRangedAttacks(target), "Ranged Attacks may not target cards in this unit.");
        // Strength of the Divine (issue #287): the first "attack" Target ability with its own separate
        // Performer -- sourceId here must be the chosen Performer (the card whose live Force/Personal
        // Honor the strength formula reads), not the ability's own source card, the same distinction
        // ExecuteAbilityEffects' own case already draws for it. Every existing "attack" ability without a
        // Performer of its own falls back to the source card.
        // Core rules (Ranged/Melee Attack, confirmed by Mendon 2026-09-24): "Target a Follower or a Personality without Followers in the current enemy army. If its Force is <= X, destroy it." A target whose
        // Force (or compared stat) is above the strength is still a LEGAL target; the comparison happens as the Attack resolves (ResolveAttackNow), and it simply fails to destroy it. The strength and any
        // predicted Force penalty therefore no longer take part in target legality (forcePenaltyTargetId/forcePenalty are kept for the callers that pass them).
    }
}
