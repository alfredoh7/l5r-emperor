using System.Collections.ObjectModel;

namespace L5R.Core;

// Performer is set only when the ability separately selects a performing card before its target
// (e.g. "Choose your performing unbowed Personality and target..."); most abilities leave it null
// and use only their source card and Target. Trigger identifies what opens a Reaction-timing
// ability's window; so far only "own-enters-play" exists (see Purchase()'s post-entry window).
// UsageKey lets two catalog entries that are really one printed ability with a choice of effects
// (e.g. "bow or straighten him") share a single once-per-turn use, instead of each half getting
// its own independent use by virtue of being a separate dictionary key; defaults to the entry's
// own id, which is every other ability's actual behavior.
// RequiredKeyword narrows a keyword-agnostic Target/Performer category (e.g. "own-personality") to
// only cards carrying that printed or granted keyword -- the generic shape behind "Choose your
// performing/target [Keyword] Personality" (core rules: a keyword with no mechanical rule of its own
// still satisfies this kind of condition on another card, in addition to any mechanical ability it
// does carry). See "own-personality-with-keyword" below.
public sealed record AbilityDefinition(string Timing, string Label, string Target, bool PlayerAbility = false, string? Performer = null, string? Trigger = null, string? UsageKey = null, string? RequiredKeyword = null);

public static class AbilityCatalog
{
    public static IReadOnlyDictionary<string, AbilityDefinition> All { get; } = new ReadOnlyDictionary<string, AbilityDefinition>(new Dictionary<string, AbilityDefinition>
    {
        ["border-first"] = new("Limited", "First-turn province refresh", "provinces"),
        ["border-game"] = new("Limited", "Once-per-game province refresh", "provinces"),
        ["shinden-choice"] = new("Open", "Offer bowing or 1 Honor", "shinden", Performer: "shugenja"),
        ["air-straighten"] = new("Open", "Straighten an Air Personality", "air"),
        // Training fixture for the generic keyword-satisfaction primitive (Immovable Object: "Battle/
        // Open: Choose your performing Berserker: Straighten him"). Open-only here -- this engine has
        // no dual-timing ability shape yet, and demonstrating the keyword gate needs only one.
        ["training-keyword-conditional-straighten"] = new("Open", "Choose your performing Berserker: Straighten him", "own-personality-with-keyword", RequiredKeyword: "Berserker"),
        // Training fixture for the generic Performer-side keyword gate (mirrors Tactical Advantage's
        // Performer shape, but for any keyword instead of one hardcoded to Tactician).
        ["training-keyword-conditional-force-boost"] = new("Battle", "Choose your performing Berserker at the current battlefield: +2 Force", "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeyword: "Berserker"),
        ["shunya-honor"] = new("Limited", "Discard Favor · gain 2 Honor", "none"),
        ["kouki-force"] = new("Battle", "Bow Kouki · enemy −3 Force", "enemy-personality"),
        ["prepared-death"] = new("Limited", "Perform · +2 Force", "own-personality"),
        ["paid-bow"] = new("Open", "Pay 4 Gold · bow a Personality", "personality"),
        ["paid-straighten"] = new("Open", "Pay 4 Gold · straighten a Personality", "personality"),
        ["costly-feud"] = new("Battle", "Enemy −3 Force · bow at 0", "enemy-force"),
        ["incapacitated"] = new("Battle", "Move a defending Personality home", "defending"),
        ["block-supply"] = new("Battle", "Move an attacking unit home bowed", "attacking"),
        ["defensive-stance"] = new("Battle", "Perform bowed · straighten and +3 Force", "own-bowed"),
        ["crushing-force"] = new("Battle", "Opposed Personality · +3 Force", "own-opposed"),
        ["crushing-destroy"] = new("Battle", "Destroy an enemy Follower or Item", "enemy-attachment"),
        ["never-afraid"] = new("Battle", "Destroy an attachment", "attachment"),
        ["lobby"] = new("Limited", "Lobby · bow a Personality to take Favor", "lobby", true),
        ["favor-cycle"] = new("Limited", "Discard Favor and a card · draw a card", "hand", true),
        ["favor-battle"] = new("Battle", "Discard Favor · move an enemy attacker home", "enemy-attacking", true),
        ["journeys-end-move"] = new("Battle", "Move home a Personality with no more Followers than the performer", "fewer-or-equal-followers-than-performer", Performer: "own-unbowed-personality-at-battle"),
        ["shamate-move"] = new("Battle", "Move home a Personality with equal or lower Personal Honor", "lower-or-equal-honor-than-performer", Performer: "own-unbowed-personality-at-battle"),
        ["law-of-darkness-weaken"] = new("Battle", "Enemy Personality −4 Force · bow if dishonorable", "enemy-personality"),
        // Ranged/Melee Attack (core rules Glossary): targeting a Follower or a Personality without
        // Followers, Force <= strength, is resolved at the moment the effect happens rather than
        // required up front -- the action stays legal to take with no eligible target in sight.
        ["carpenter-castle-siege"] = new("Battle", "Bow a Castle/Siege card · Ranged 5 Attack", "attack", Performer: "own-castle-or-siege"),
        ["shiro-shiba-melee"] = new("Battle", "Perform · Melee 4 Attack (+2 if you control a Shugenja)", "attack", Performer: "own-unbowed-samurai-at-battle"),
        // Training-only: no published card yet reduces an opponent's Honor via a Battle action, so this
        // exercises Law of Darkness Dojo's "he loses 1 Honor" passive, which otherwise has no live trigger.
        ["training-honor-strike"] = new("Battle", "Training: enemy Personality loses 2 Honor", "enemy-personality"),
        // Training-only: exercises the token primitives (create/move/destroy) ahead of any published
        // card needing them -- several already-triaged Strongholds will, in a later wave.
        ["training-create-token"] = new("Open", "Training: give target Personality a +1F Training token", "own-personality"),
        ["training-move-token"] = new("Open", "Training: move a token from target to performer", "own-personality-with-token", Performer: "own-personality"),
        ["training-destroy-token"] = new("Open", "Training: destroy a token on target", "own-personality-with-token"),
        ["monastery-fire-token"] = new("Limited", "Target your Tattooed Personality · +1F Fire token", "own-tattooed-personality"),
        ["monastery-fire-attack"] = new("Battle", "Destroy a Fire token · Ranged 5 Attack", "attack", Performer: "own-personality-with-fire-token-at-battle"),
        // Training-only: exercises the Honor-loss-immunity primitive's "enters play, lose Honor"
        // shape (e.g. a card reading "After [X] enters play: lose N Honor") ahead of any published
        // card needing it.
        ["training-honor-loss-entry"] = new("Reaction", "After entering play, lose 2 Honor", "none", Trigger: "own-enters-play"),
        // Generalized entry-reaction fixtures (issue #148's Equip-time entry hook): unlike the
        // Personality-only fixture above (only ever exercised via Purchase), these prove the same
        // "own-enters-play" trigger now fires from Equip and from a Terrain entering play too.
        ["training-item-entry-reaction"] = new("Reaction", "After entering play, gain 1 Honor", "none", Trigger: "own-enters-play"),
        ["training-terrain-entry-reaction"] = new("Reaction", "After entering play, gain 1 Honor", "none", Trigger: "own-enters-play"),
        ["meiyu-fire-tokens"] = new("Reaction", "After entering play, gain three +1F Fire tokens", "none", Trigger: "own-enters-play"),
        ["torazu-fire-tokens"] = new("Reaction", "After entering play, gain three +1F Fire tokens", "none", Trigger: "own-enters-play"),
        ["torazu-fire-attack"] = new("Battle", "Destroy a Fire token · Ranged 3 Attack", "attack"),
        // Training-only: exercises the "a discarded Personality becomes (honorably) dead" primitive
        // ahead of any published card needing it (e.g. a future Temple of Death-style effect).
        ["training-make-dead"] = new("Open", "Training: a discarded Personality becomes honorably dead", "discarded-personality"),
        // Training-only: exercises the Ranged/Melee Attack reaction window ahead of any published
        // card needing it -- proves strength can be raised or negated by either side before resolution.
        ["training-attack-boost"] = new("Reaction", "Training: +2 to a pending attack", "none", Trigger: "before-attack-resolves"),
        ["training-attack-negate"] = new("Reaction", "Training: negate a pending attack", "none", Trigger: "before-attack-resolves"),
        // Training-only: exercises the Terrain primitive's "may be a target for battle actions that
        // target a Terrain" clause, ahead of any published card needing it.
        ["training-destroy-terrain"] = new("Battle", "Training: destroy the Terrain at this battlefield", "terrain-at-battle"),
        // Import wave 1 (issue #95): unconditional Attacks needing nothing beyond the primitive itself.
        ["shinjo-eun-sahng-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["shosuro-konishi-attack"] = new("Battle", "Ranged 5 Attack", "attack"),
        ["tsuruchi-kosoko-attack"] = new("Battle", "Ranged 5 Attack", "attack"),
        ["stalking-tiger-attack"] = new("Battle", "Melee 6 Attack", "attack"),
        // "Bow Isas: Ranged 3 Attack. Move Isas home." -- the source bows itself as its own performer
        // (no separate Performer field, same shape as kouki-force), then moves home after the Attack
        // resolves regardless of outcome (ResolveAttackNow's per-ability switch).
        ["tsuruchi-isas-attack"] = new("Battle", "Bow Isas · Ranged 3 Attack, then move him home", "attack"),
        // Import wave 2 (issue #97): the Attack primitive's first target-dependent strength.
        ["bayushi-kahoku-attack"] = new("Battle", "Ranged 3 Attack (+2 if the target is dishonorable)", "attack"),
        ["asako-ume-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        // Training-only: exercises the Honor-loss-immunity primitive's "pay Honor to use a Battle
        // ability" shape (e.g. "Battle: Lose N Honor: [Attack]") ahead of any published card needing
        // it. Played from hand like Tactical Advantage -- a Strategy card providing its own ability.
        ["training-honor-cost-attack"] = new("Battle", "Lose 2 Honor · Ranged 3 Attack", "attack"),
        // Training-only: exercises the Reconnaissance primitive (issue #93) ahead of any published
        // card needing it -- a per-player, per-province boolean that gates only part of an effect.
        ["training-gain-reconnaissance"] = new("Open", "Training: gain Reconnaissance in an enemy province", "enemy-province"),
        ["training-reconnaissance-strike"] = new("Battle", "Training: enemy Personality loses 2 Honor if you have Reconnaissance there", "enemy-personality"),
        // Tactician (core rules Glossary): every player always has this ability, regardless of what
        // they control -- "any number of times per turn" (see the ValidateAbility exemption below)
        // is the point, so its own cost (discarding a card) is the only real limit on repeated use.
        // Battle-timing by default; a card that grants an Action-segment variant would need its own
        // catalog entry reusing the same UseAbility case, not a change to this one.
        ["tactical-advantage"] = new("Battle", "Tactical Advantage: discard a card · performing Tactician +Force = its Focus Value", "hand", PlayerAbility: true, Performer: "own-tactician-at-battle"),
        // Every player always has this ability (core rules): "any number of times per turn" (see the
        // ValidateAbility exemption below), same shape as Tactical Advantage. Only a Shugenja can
        // ever hold a Spell attachment -- Follower Shugenja are excluded (own-unbowed-shugenja-
        // opposed-at-battle checks Type==Personality). The immediate optional follow-up action is a
        // separate PendingChoice ("spellFollowUp"; see Apply/QueryActions), not part of this
        // definition -- it opens after UseAbility's own case below, restricted to the just-attached
        // Spell's own Battle abilities, closing after exactly one use or a decline.
        ["equip-battle"] = new("Battle", "Equip (Battle): Attach a Spell to your opposed unbowed Shugenja, paying all costs", "spell-in-hand", PlayerAbility: true, Performer: "own-unbowed-shugenja-opposed-at-battle"),
        // Training-only: a Spell's own Battle ability, exercised only through the "equip-battle"
        // follow-up window above -- proves a just-attached Spell can immediately act.
        ["training-spell-force-boost"] = new("Battle", "Target your Personality at the current battlefield: +2 Force", "own-personality"),
        // Import wave (issue #111): Cavalry/Conqueror/Tactician-adjacent cards needing only small,
        // already-precedented extensions of existing target categories and primitives.
        ["reiyu-force-penalty"] = new("Battle", "Target enemy Force/Follower · Force penalty = Reiyu's Force", "enemy-force"),
        // "If opposed" is a per-ability presence Require (ValidateAbility), same shape as
        // torazu-fire-attack's own Fire-token Require. Bow and straighten are the same single
        // printed ability's two alternative effects (unlike paid-bow/paid-straighten, which are
        // genuinely two separate abilities on unrelated cards) -- they share one UsageKey so using
        // either spends the one use, rather than each half getting its own independent once-per-turn.
        ["horibe-bow"] = new("Battle", "If opposed · bow a target Personality", "personality", UsageKey: "horibe-bow-straighten"),
        ["horibe-straighten"] = new("Battle", "If opposed · straighten a target Personality", "personality", UsageKey: "horibe-bow-straighten"),
        ["atsumichi-bow"] = new("Battle", "Target a Personality with lower Personal Honor than Atsumichi · bow him", "personality-lower-honor-than-self", UsageKey: "atsumichi-bow-straighten"),
        ["atsumichi-straighten"] = new("Battle", "Target a Personality with lower Personal Honor than Atsumichi · straighten him", "personality-lower-honor-than-self", UsageKey: "atsumichi-bow-straighten"),
        ["tetsuo-destroy-attachment"] = new("Battle", "Target an attachment · destroy it", "attachment"),
        ["tetsuo-destroy-weaker-weaponed"] = new("Battle", "Target an enemy Personality with fewer Weapons · destroy it", "enemy-personality-fewer-weapons-than-self"),
        // "Target another player's discarded Personality" is an owner restriction on the existing
        // discarded-personality category, checked inline where that category is validated.
        ["shunsuke-make-dead"] = new("Limited", "Target another player's discarded Personality · he becomes honorably dead", "discarded-personality"),
        // Import wave: keyword-satisfaction primitive (issue #144's own worked example plus three more
        // Strategy cards needing nothing beyond it, the generic Performer/Target keyword gate, and one
        // new one-off comparison category each).
        ["blind-rage"] = new("Battle", "Choose your performing unbowed Berserker: +5F. Lose 2 Honor.", "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeyword: "Berserker"),
        ["unorthodox-attack"] = new("Battle", "Choose your performing unbowed Samurai and target an enemy card: Bow it.", "enemy-force", Performer: "own-unbowed-samurai-at-battle"),
        ["chokehold"] = new("Battle", "Choose your performing unbowed Monk and target an enemy Personality with equal or lower Chi: Bow him.", "enemy-personality-chi-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeyword: "Monk"),
        // Immovable Object's two printed abilities are independent (separate once-per-turn uses), not
        // a shared choice -- no UsageKey needed. The first is the card the user's own worked example
        // ("Choose your performing Berserker: Straighten him") is drawn from directly.
        ["immovable-object-straighten"] = new("BattleOrOpen", "Choose your performing Berserker: Straighten him.", "own-personality-with-keyword", RequiredKeyword: "Berserker"),
        ["immovable-object-bow"] = new("Battle", "Choose your performing unbowed Berserker and target an enemy Personality or Follower with lower Force: Bow it.", "enemy-force-lower-than-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeyword: "Berserker"),
        // Follower wave: cards audited against every currently-imported Follower for ones needing
        // nothing beyond already-built categories/helpers (issue #148). Each reuses an existing
        // Target category, Attack strength/kind pattern, or Force/Draw/bow helper verbatim.
        ["akodo-regulars-boost"] = new("Battle", "Discard a card: Give this card a Force bonus equal to the discarded card's Focus Value (until the turn ends)", "hand"),
        ["cavalry-flankers-attack"] = new("Battle", "If you are the Attacker: Bow this card: Melee 2 Attack", "attack"),
        ["dark-naga-scouts-attack"] = new("Battle", "Ranged 3 Attack", "attack"),
        ["horse-archers-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["murderous-thieves-attack"] = new("Battle", "Bow this card: Melee 3 Attack", "attack"),
        ["veteran-advisor-draw"] = new("Battle", "Draw a card", "none"),
        ["hired-legion-draw"] = new("Battle", "Bow this card: Draw a card", "none"),
        ["naga-remnants-bow"] = new("Battle", "Bow a target enemy attachment", "enemy-attachment"),
        ["hiruma-sniper-attack"] = new("Battle", "If you have Reconnaissance: Ranged 4 Attack", "attack"),
        ["heavy-chargers-boost"] = new("Battle", "If this card is opposed, discard a card: Give this card +3F", "hand"),
        ["spitting-llama-force"] = new("Battle", "Give a target enemy Personality or Follower -3F", "enemy-force"),
    });
}

public sealed partial class MatchEngine
{
    private static bool InPlay(CardInstance card) => card.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Attached;
    private static bool Keyword(CardInstance card, string keyword) => (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword)).Contains(keyword, StringComparer.Ordinal);
    // Most Personalities have one printed Clan (the CardDefinition.Clan field); a few carry more than
    // one alignment (Clans overrides it when present). Loyal restricts recruitment to a Stronghold
    // whose Clan is among these.
    private static string[] ClanAlignments(CardInstance card) => Definition(card).Clans ?? [Definition(card).Clan];
    // "Attachment": lasts while the granting attachment stays attached (cleared in DestroyAttachment).
    // "Turn": cleared at FinishTurn, the default for a keyword granted by an action's effect.
    // "Permanent": only when the granting card's text says so; never auto-cleared.
    private static void GrantKeyword(CardInstance card, string keyword, string source, string duration) => card.GrantedKeywords.Add(new(keyword, source, duration));
    private bool AtBattle(CardInstance card) => _state.CurrentBattleProvince is not null &&
        (card.Zone == Zone.Battlefield || card.Zone == Zone.Attached && Card(card.AttachedTo).Zone == Zone.Battlefield) && card.ProvinceIndex == _state.CurrentBattleProvince;
    private bool HasHalls(int owner) => Definition(Card(Player(owner).StrongholdId)).Effect == "halls-memory";
    private bool ControlsKeyword(int owner, string keyword) => _state.Cards.Values.Any(c => c.OwnerId == owner && InPlay(c) && Keyword(c, keyword));
    // Reconnaissance (core rules Glossary): a per-player boolean at a specific province, defaulted to
    // false, granted by an action/passive/Reaction. It gates only the portion of an ability that
    // depends on it -- the rest of that ability's effect still proceeds regardless of this check.
    // Resets to false only when the battle there resolves (see ResolveBattle).
    private bool HasReconnaissance(int playerId, int provinceOwnerId, int provinceIndex) => Player(provinceOwnerId).Provinces[provinceIndex].Reconnaissance[playerId];
    private void GrantReconnaissance(int playerId, int provinceOwnerId, int provinceIndex)
    {
        var province = Player(provinceOwnerId).Provinces[provinceIndex];
        if (province.Reconnaissance[playerId]) return;
        province.Reconnaissance[playerId] = true;
        Emit("reconnaissanceGained", $"{Player(playerId).Name} gained Reconnaissance in {Player(provinceOwnerId).Name}'s province {provinceIndex + 1}.", playerId);
    }
    // Ranged/Melee Attack strength; conditional bonuses (e.g. Shiro Shiba's Shugenja check) live here.
    // target is the candidate/chosen target, when one exists (deferred targeting means it may be
    // null); a target-dependent conditional simply falls back to its unconditional value then.
    private int AttackStrength(string abilityId, PlayerState player, CardInstance? target = null) => abilityId switch
    {
        "carpenter-castle-siege" => 5,
        "shiro-shiba-melee" => 4 + (ControlsKeyword(player.Id, "Shugenja") ? 2 : 0),
        "monastery-fire-attack" => 5,
        "torazu-fire-attack" => 3,
        "shinjo-eun-sahng-attack" => 4,
        "shosuro-konishi-attack" or "tsuruchi-kosoko-attack" => 5,
        "stalking-tiger-attack" => 6,
        "tsuruchi-isas-attack" => 3,
        "bayushi-kahoku-attack" => 3 + (target is not null && target.Dishonorable ? 2 : 0),
        "asako-ume-attack" => 4,
        "training-honor-cost-attack" => 3,
        "cavalry-flankers-attack" => 2,
        "dark-naga-scouts-attack" => 3,
        "murderous-thieves-attack" => 3,
        "horse-archers-attack" or "hiruma-sniper-attack" => 4,
        _ => 0
    };
    // Cards that affect only Ranged Attacks (or only Melee) reference this; the two kinds otherwise
    // share the same resolution pathway (core rules Glossary — Ranged Attack/Melee Attack).
    private static string AttackKind(string abilityId) => abilityId switch
    {
        "carpenter-castle-siege" or "monastery-fire-attack" or "torazu-fire-attack" => "Ranged",
        "shiro-shiba-melee" or "stalking-tiger-attack" => "Melee",
        "shinjo-eun-sahng-attack" or "shosuro-konishi-attack" or "tsuruchi-kosoko-attack" or "tsuruchi-isas-attack" or "bayushi-kahoku-attack" or "asako-ume-attack" or "training-honor-cost-attack" => "Ranged",
        "cavalry-flankers-attack" or "murderous-thieves-attack" => "Melee",
        "dark-naga-scouts-attack" or "horse-archers-attack" or "hiruma-sniper-attack" => "Ranged",
        _ => ""
    };
    // Whether opening a reaction window before resolving an attack could possibly matter right now.
    // No kind-restricted Reaction exists yet (see AttackKind); once one does, filter by attack.Kind here.
    private bool AnyAttackReactionAvailable() => _state.Cards.Values.Any(c => InPlay(c) && !c.Bowed &&
        (Definition(c).Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == "before-attack-resolves" &&
            (!c.AbilityUses.TryGetValue(id, out var used) || used != _state.TurnNumber)));
    // Steps F-G of a Ranged/Melee Attack (core rules): compare strength to the target's *current*
    // Force -- re-evaluated here, not at announce time, since a Reaction may have changed either --
    // and destroy on a hit. A strength of 0 or less, or no target ever chosen, has no effect.
    private void ResolveAttackNow(string abilityId, int playerId, string? targetId, int strength, string? sourceId = null)
    {
        if (strength > 0 && targetId is not null)
        {
            var target = Card(targetId);
            if (InPlay(target) && AtBattle(target) && CardForce(target) <= strength)
            {
                DestroyAttackTarget(target);
                if (abilityId == "carpenter-castle-siege") GainHonor(playerId, 2, "Carpenter Castle");
            }
        }
        // Tsuruchi Isas: "Move Isas home" is a second, unconditional effect of the same ability --
        // it happens whether or not the Attack actually hit, so it lives outside the hit branch above.
        if (abilityId == "tsuruchi-isas-attack" && sourceId is not null) MoveHome(Card(sourceId), false);
    }
    // Steps A-E already happened (presence, announce, performer, required targeting, costs) by the
    // time a card's own UseAbility case calls this. Returns true if a reaction window was opened --
    // the caller must `return;` so the action stays pending; false if it resolved immediately inline
    // (identical end state to before this primitive existed, when nothing can possibly react).
    // sourceId is only needed by a card whose own post-attack effect (see ResolveAttackNow) must
    // find the exact performing instance again once a deferred reaction window closes.
    private bool ResolveOrDeferAttack(string abilityId, PlayerState attacker, string? targetId, string? sourceId = null)
    {
        var strength = AttackStrength(abilityId, attacker, targetId is null ? null : Card(targetId));
        if (!AnyAttackReactionAvailable())
        {
            ResolveAttackNow(abilityId, attacker.Id, targetId, strength, sourceId);
            return false;
        }
        _state.PendingAttack = new PendingAttack { AbilityId = abilityId, PlayerId = attacker.Id, SourceId = sourceId, TargetId = targetId, Strength = strength, Kind = AttackKind(abilityId) };
        _state.PendingChoice = new("attackReaction", attacker.Id, null, "An attack is pending: react or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = 1 - attacker.Id;
        Emit("reactionWindow", "Attack reaction window opened.", attacker.Id);
        return true;
    }
    // Called once both players pass consecutively in the attackReaction window (Apply()).
    private void ResolveAttack()
    {
        var attack = _state.PendingAttack!;
        _state.PendingAttack = null;
        ResolveAttackNow(attack.AbilityId, attack.PlayerId, attack.TargetId, attack.Strength, attack.SourceId);
        NextAction(attack.PlayerId);
    }
    private bool IsAttacking(CardInstance card) => card.Zone == Zone.Battlefield && card.OwnerId == _state.ActivePlayerId;
    // Modifiers are kept in application order (not folded into one running total) so a future card
    // can negate one specific prior effect, e.g. RemoveModifiers(target, m => m.Source == someCardId).
    // Tokens contribute here too: unlike Modifiers (cleared every FinishTurn), a token's stat bonus
    // persists until the token itself is destroyed or its host leaves play (core rules Glossary — Token).
    private static int StatModifier(CardInstance card, string stat) => card.Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount) + card.Tokens.Where(t => t.Stat == stat).Sum(t => t.Amount);
    // A Personality reduced to 0 Chi is destroyed immediately, by any means — centralized here so
    // every current and future Chi-reducing effect enforces it without needing its own check.
    private void ApplyModifier(CardInstance card, string source, string effect, string stat, int amount)
    {
        card.Modifiers.Add(new(source, effect, stat, amount));
        if (stat == "Chi" && Definition(card).Type == CardType.Personality && InPlay(card) && PersonalityChi(card) <= 0) DestroyUnits([card], true);
    }
    private static void RemoveModifiers(CardInstance card, Func<AppliedModifier, bool> match) => card.Modifiers.RemoveAll(m => match(m));
    private int CardForce(CardInstance card) => Definition(card).Type == CardType.Personality ? PersonalityForce(card) : Math.Max(0, Definition(card).Force + StatModifier(card, "Force"));
    private int PersonalityChi(CardInstance card) => Definition(card).Chi + StatModifier(card, "Chi") + Attachments(card.Id).Where(c => Definition(c).Type == CardType.Item).Sum(c => Definition(c).Chi);

    // Shared by action discovery and execution: an offered target obeys exactly the same constraints.
    private void ValidateAbility(GameCommand command)
    {
        var id = command.AbilityId ?? "";
        Require(AbilityCatalog.All.TryGetValue(id, out var ability), "Unknown ability.");
        Require(ability!.Timing switch {
            "Limited" => _state.Phase == Phase.Action && command.PlayerId == _state.ActivePlayerId,
            "Open" => _state.Phase == Phase.Action,
            "Battle" => _state.Phase == Phase.Battle,
            // Printed "Battle/Open" text (e.g. Immovable Object): usable in either phase. The
            // blanket Battle-only checks below (army presence, target/performer at-battle) key off
            // the literal "Battle" string, so a BattleOrOpen ability gets neither -- correct, since
            // its own printed text carries no battlefield-presence requirement of its own.
            "BattleOrOpen" => _state.Phase is Phase.Action or Phase.Battle,
            // Legal only inside the matching reaction window: entering-play reactions are gated to
            // the exact card and its own controller (only they may ever act there); an attack
            // reaction is open to either side, already enforced by the top-level priority check.
            "Reaction" => _state.PendingChoice is not null && ability.Trigger switch
            {
                "own-enters-play" => _state.PendingChoice.Type is "proclaim" or "entryReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                "before-attack-resolves" => _state.PendingChoice.Type == "attackReaction",
                _ => false
            },
            _ => false
        }, $"{ability.Timing} timing is unavailable now.");
        var player = Player(command.PlayerId);
        CardInstance? source = null;
        Dictionary<string, int> uses;
        if (ability.PlayerAbility)
        {
            Require(command.CardId is null, "A player ability does not use a source card.");
            Require(!Definition(Card(player.StrongholdId)).ScenarioOnly, "Player abilities are enabled in the published deck profile.");
            uses = player.AbilityUses;
        }
        else
        {
            source = Card(command.CardId);
            var def = Definition(source);
            Require(source.OwnerId == player.Id && (def.Abilities ?? []).Contains(id), "This card does not provide that ability to you.");
            // A Strategy source is normally used straight from hand (the usual one-shot pattern), but
            // a Terrain stays in play afterward (Zone.Battlefield) -- its own abilities, including an
            // entering-play Reaction, must still be usable from there.
            Require(def.Type == CardType.Strategy ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || InPlay(source) : InPlay(source), "The ability's source is not in the required zone.");
            Require(!source.Bowed || id == "shinden-choice", "Abilities on bowed cards cannot be used.");
            uses = source.AbilityUses;
        }
        // "Any number of times per turn" (Tactical Advantage, Equip (Battle)): exempt from the
        // once-per-turn dedupe entirely -- each one's own cost/requirements are the only real limit.
        var usageKey = ability.UsageKey ?? id;
        Require(id is "tactical-advantage" or "equip-battle" || !uses.TryGetValue(usageKey, out var used) || used != _state.TurnNumber && id != "border-game", "This ability has already been used within its limit.");
        if (id == "border-first") Require(player.TurnsTaken == 1, "This Border Keep ability is only available on your first turn.");
        if (ability.Timing == "Battle")
        {
            Require(Army(player.Id, _state.CurrentBattleProvince!.Value).Any(), "You need a unit at the current battlefield.");
            if (source is not null && Definition(source).Type == CardType.Personality)
                Require(AtBattle(source) || HasHalls(player.Id) && Keyword(source, "Shugenja"), "The performing card must be at this battlefield.");
        }
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle")
            Require(_state.FavorPlayerId == player.Id, "You must control the Imperial Favor to discard it.");
        if (id == "lobby") Require(player.Honor > Player(1-player.Id).Honor, "Lobby requires strictly higher Family Honor than your opponent.");
        // Torazu's own ability, on Torazu, needs no separate Performer -- the shared Battle presence
        // check above already requires the source itself (a Personality) to be at this battlefield.
        if (id == "torazu-fire-attack") Require(HasToken(source!, "Fire"), "Torazu needs a Fire token of his own to attack.");
        if (id is "horibe-bow" or "horibe-straighten")
            Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Horibe must be opposed to use this ability.");
        if (id is "heavy-chargers-boost") Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Heavy Chargers must be opposed to use this ability.");
        if (id == "cavalry-flankers-attack") Require(player.Id == _state.ActivePlayerId, "Only the Attacker may use this ability.");
        if (id == "hiruma-sniper-attack") Require(HasReconnaissance(player.Id, 1 - player.Id, _state.CurrentBattleProvince!.Value), "Hiruma Sniper requires Reconnaissance at the current battlefield.");
        if (ability.Performer is not null)
        {
            var performer = command.PerformerId is null ? throw new RuleViolation("Choose a performing card.") : Card(command.PerformerId);
            Require(performer.OwnerId == player.Id && InPlay(performer), "Choose your own card to perform.");
            Require(ability.Performer switch {
                "shugenja" => Definition(performer).Type == CardType.Personality && Keyword(performer, "Shugenja") && !performer.Bowed,
                "own-unbowed-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer),
                "own-unbowed-samurai-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) && Keyword(performer, "Samurai"),
                // Generic "Choose your performing [Keyword] Personality" at battle -- for a future
                // card naming any of the keywords with no mechanical rule of its own, without adding
                // a new hardcoded Performer category each time. See AbilityDefinition.RequiredKeyword.
                "own-unbowed-personality-at-battle-with-keyword" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) &&
                    ability.RequiredKeyword is not null && Keyword(performer, ability.RequiredKeyword),
                // No "unbowed" qualifier printed -- Monastery of New Thought's performer may be bowed.
                "own-personality-with-fire-token-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && HasToken(performer, "Fire"),
                // No "unbowed" qualifier printed either -- Tactical Advantage does not bow its performer.
                "own-tactician-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && Keyword(performer, "Tactician"),
                // Equip (Battle): only a Shugenja Personality may ever hold a Spell -- a Follower with
                // the Shugenja trait is excluded by the Type check, matching the printed restriction.
                "own-unbowed-shugenja-opposed-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) &&
                    Keyword(performer, "Shugenja") && Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                // Not a "performer" in the rules sense (Carpenter Castle names no performing card) --
                // reused for the same "select one more card, separately from the effect's target" shape.
                "own-castle-or-siege" => !performer.Bowed && (Keyword(performer, "Castle") || Keyword(performer, "Siege")),
                "own-personality" => Definition(performer).Type == CardType.Personality,
                _ => false
            }, "That card does not meet this ability's performer requirement.");
        }
        else Require(command.PerformerId is null || command.PerformerId == command.TargetId && id is "prepared-death" or "defensive-stance" or "lobby", "Unexpected performing card.");
        Require(command.Choice is null, "Choices are resolved by a separate decision command.");
        if (ability.Target == "provinces")
        {
            Require(command.TargetId is null, "Select province cards together.");
            var ids = command.CardIds ?? [];
            Require(ids.Length is >=1 and <=4 && ids.Distinct().Count() == ids.Length, "Choose one or more different province cards.");
            Require(ids.All(id => _state.Cards.TryGetValue(id, out var c) && c.OwnerId == player.Id && c.Zone == Zone.Province &&
                player.Provinces.Any(p => !p.Destroyed && p.CardId == id)), "Choose cards in your provinces.");
        }
        // Reconnaissance is granted at a specific province, not a card -- selected by index
        // (GameCommand.Province, already used by selectBattle) rather than TargetId/CardIds.
        else if (ability.Target == "enemy-province")
        {
            Require(command.TargetId is null && (command.CardIds is null || command.CardIds.Length == 0), "This action targets a province, not a card.");
            Require(command.Province is int idx && idx is >= 0 and <= 3 && !Player(1 - player.Id).Provinces[idx].Destroyed,
                "Choose one of the enemy's provinces.");
        }
        else
        {
            Require(command.CardIds is null || command.CardIds.Length == 0, "This action does not select multiple cards.");
            if (ability.Target == "none") Require(command.TargetId is null, "This action has no target.");
            // Ranged/Melee Attack: unlike every other ability target, this one is not required to
            // exist for the action to be legal -- a null TargetId always validates. When a TargetId
            // is supplied, it must be a Follower or a Personality without Followers in the current
            // enemy army with Force at or below the attack's (possibly conditional) strength.
            else if (ability.Target == "attack")
            {
                if (command.TargetId is not null)
                {
                    var target = Card(command.TargetId);
                    Require(InPlay(target) && AtBattle(target) && target.OwnerId != player.Id, "Target a card in the current enemy army.");
                    var type = Definition(target).Type;
                    Require(type == CardType.Follower || type == CardType.Personality && !Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower),
                        "Attack a Follower or a Personality without Followers.");
                    Require(CardForce(target) <= AttackStrength(id, player, target), "That card's Force is above the Attack's strength.");
                }
            }
            // "A discarded Personality" (core rules Glossary: Discarded vs. Dead) is explicitly not
            // in play -- it sits in a discard pile without yet being marked dead.
            else if (ability.Target == "discarded-personality")
            {
                var target = Card(command.TargetId);
                Require(Definition(target).Type == CardType.Personality && target.Zone is Zone.DynastyDiscard or Zone.FateDiscard,
                    "Target a discarded Personality.");
                if (id == "shunsuke-make-dead") Require(target.OwnerId != player.Id, "Target another player's discarded Personality.");
            }
            else
            {
                var target = Card(command.TargetId); var type = Definition(target).Type;
                if (ability.Target == "hand") Require(target.OwnerId == player.Id && target.Zone == Zone.Hand && player.Hand.Contains(target.Id), "Discard a card from your hand.");
                else if (ability.Target == "spell-in-hand") Require(target.OwnerId == player.Id && target.Zone == Zone.Hand && type == CardType.Spell, "Choose a Spell from your hand.");
                else
                {
                    Require(InPlay(target), "Target a card in play.");
                    if (ability.Timing == "Battle") Require(AtBattle(target), "Target or performer must be at the current battlefield.");
                    var personality = type == CardType.Personality;
                    var own = target.OwnerId == player.Id;
                    Require(ability.Target switch {
                        "shinden" => personality && !own && !target.Bowed,
                        "air" => personality && Keyword(target, "Air"),
                        "personality" => personality,
                        "own-personality" => personality && own,
                        // Generic "Choose your performing/target [Keyword] Personality" -- for a
                        // keyword with no mechanical rule of its own (e.g. Berserker), or in addition
                        // to one a keyword already has (e.g. Tactician): see AbilityDefinition.RequiredKeyword.
                        "own-personality-with-keyword" => personality && own && ability.RequiredKeyword is not null && Keyword(target, ability.RequiredKeyword),
                        "enemy-personality" => personality && !own,
                        "own-bowed" => personality && own && target.Bowed,
                        "own-opposed" => personality && own && Army(1-player.Id, _state.CurrentBattleProvince!.Value).Any(),
                        "enemy-force" => !own && type is CardType.Personality or CardType.Follower,
                        "attacking" => personality && target.OwnerId == _state.ActivePlayerId,
                        "defending" => personality && target.OwnerId != _state.ActivePlayerId,
                        "enemy-attacking" => personality && !own && target.OwnerId == _state.ActivePlayerId,
                        "enemy-attachment" => !own && target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        "attachment" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        "lobby" => personality && own && !target.Bowed && Definition(target).PersonalHonor >= 1,
                        "fewer-or-equal-followers-than-performer" => personality &&
                            Attachments(target.Id).Count(a => Definition(a).Type == CardType.Follower) <= Attachments(command.PerformerId!).Count(a => Definition(a).Type == CardType.Follower),
                        "lower-or-equal-honor-than-performer" => personality && Definition(target).PersonalHonor <= Definition(Card(command.PerformerId!)).PersonalHonor,
                        "own-personality-with-token" => personality && own && target.Tokens.Count > 0,
                        "own-tattooed-personality" => personality && own && Keyword(target, "Tattooed"),
                        "personality-lower-honor-than-self" => personality && Definition(target).PersonalHonor < Definition(source!).PersonalHonor,
                        "enemy-personality-fewer-weapons-than-self" => personality && !own &&
                            Attachments(target.Id).Count(a => Keyword(a, "Weapon")) < Attachments(source!.Id).Count(a => Keyword(a, "Weapon")),
                        "terrain-at-battle" => type == CardType.Strategy && Keyword(target, "Terrain"),
                        // Compared against the chosen Performer (a separately-selected card), unlike
                        // "-than-self" categories above which compare against the ability's own source.
                        "enemy-personality-chi-lte-performer" => personality && !own && Definition(target).Chi <= Definition(Card(command.PerformerId!)).Chi,
                        "enemy-force-lower-than-performer" => !own && type is CardType.Personality or CardType.Follower && CardForce(target) < CardForce(Card(command.PerformerId!)),
                        _ => false
                    }, "That card does not meet this ability's target or performer requirements.");
                }
            }
        }
        // Equip (Battle) has no source card of its own -- its cost is the targeted Spell's, paid on
        // attachment, not a Strategy's own GoldCost.
        var cost = source is not null && Definition(source).Type == CardType.Strategy ? Definition(source).GoldCost
            : ability.Target == "spell-in-hand" && command.TargetId is not null ? Definition(Card(command.TargetId)).GoldCost : 0;
        ValidatePayment(player.Id, cost, command.SourceIds);
    }

    private void UseAbility(GameCommand command)
    {
        ValidateAbility(command);
        var id = command.AbilityId!; var ability = AbilityCatalog.All[id];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        var target = command.TargetId is null ? null : Card(command.TargetId);
        var uses = source?.AbilityUses ?? player.AbilityUses;
        uses[ability.UsageKey ?? id] = _state.TurnNumber;
        if (source is not null && Definition(source).Type == CardType.Strategy && Definition(source).GoldCost > 0)
            Pay(player.Id, source, Definition(source).GoldCost, command.SourceIds);
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle")
        {
            _state.FavorPlayerId = null;
            Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
        }
        Emit("abilityUsed", $"{player.Name}: {(source is null ? "Imperial Favor" : Definition(source).Name)} — {ability.Label}.", player.Id, source?.Id);
        // Snapshot before the effect resolves so any Battle-action Honor loss it inflicts on an
        // opponent can be detected generically afterward (Law of Darkness Dojo's passive).
        var opponentHonorBeforeBattleAction = ability.Timing == "Battle" && target is not null && target.OwnerId != player.Id ? Player(target.OwnerId).Honor : (int?)null;
        switch (id)
        {
            case "border-first": case "border-game": CycleProvinces(player, command.CardIds!); break;
            case "shinden-choice":
                Card(command.PerformerId).Bowed = true;
                _state.PendingChoice = new("shinden", target!.OwnerId, target.Id,
                    $"{Definition(target).Name} ({target.Id}): bow and prevent straightening for this turn, or let {player.Name} gain 1 Honor.", player.Id);
                _state.PriorityPlayerId = target.OwnerId; _state.ConsecutivePasses = 0;
                return; // The action does not finish until the opponent answers.
            case "air-straighten": case "paid-straighten": case "training-keyword-conditional-straighten": case "immovable-object-straighten": Straighten(target!); break;
            case "paid-bow": case "unorthodox-attack": case "chokehold": case "immovable-object-bow": case "naga-remnants-bow": target!.Bowed = true; break;
            case "veteran-advisor-draw": Draw(player); break;
            case "hired-legion-draw": source!.Bowed = true; Draw(player); break;
            case "akodo-regulars-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", Definition(target!).Focus); break;
            case "heavy-chargers-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", 3); break;
            case "spitting-llama-force": ApplyModifier(target!, source!.Id, id, "Force", -3); break;
            case "blind-rage": ApplyModifier(Card(command.PerformerId!), id, id, "Force", 5); GainHonor(player.Id, -2, "Blind Rage"); break;
            case "shunya-honor": GainHonor(player.Id, 2, "Doji Shunya"); break;
            case "kouki-force": source!.Bowed = true; ApplyModifier(target!, source.Id, id, "Force", -3); break;
            case "prepared-death": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "costly-feud": ApplyModifier(target!, source!.Id, id, "Force", -3); if (CardForce(target!) == 0) target!.Bowed = true; break;
            case "defensive-stance": Straighten(target!); ApplyModifier(target!, source!.Id, id, "Force", 3); break;
            case "crushing-force": ApplyModifier(target!, source!.Id, id, "Force", 3); break;
            case "crushing-destroy": case "never-afraid": DestroyAttachment(target!); break;
            case "incapacitated": case "favor-battle": MoveHome(target!, false); break;
            case "block-supply": MoveHome(target!, true); break;
            case "lobby": target!.Bowed = true; _state.FavorPlayerId = player.Id; Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id); break;
            case "favor-cycle": DiscardFate(player, target!); Draw(player); break;
            case "journeys-end-move": MoveHome(target!, false); break;
            case "shamate-move": MoveHome(target!, false); if (target!.OwnerId != player.Id) GainHonor(player.Id, 2, "Shamate Keep"); break;
            case "law-of-darkness-weaken": ApplyModifier(target!, source!.Id, id, "Force", -4); if (target!.Dishonorable) target.Bowed = true; break;
            case "training-honor-strike": GainHonor(target!.OwnerId, -2, "Training strike", source); break;
            case "training-destroy-terrain": DiscardTerrain(target!); break;
            case "carpenter-castle-siege":
                Card(command.PerformerId).Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "shiro-shiba-melee": if (ResolveOrDeferAttack(id, player, target?.Id)) return; break;
            case "training-create-token": CreateToken(target!, "Training", id, "Force", 1); break;
            case "training-move-token": MoveToken(target!, target!.Tokens[0], Card(command.PerformerId!)); break;
            case "training-destroy-token": DestroyToken(target!, target!.Tokens[0]); break;
            case "monastery-fire-token": CreateToken(target!, "Fire", id, "Force", 1); break;
            case "monastery-fire-attack":
            {
                var performer = Card(command.PerformerId!);
                DestroyToken(performer, performer.Tokens.First(t => t.Type == "Fire"));
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            }
            case "training-honor-loss-entry": GainHonor(player.Id, -2, "Training entry Honor loss", source); break;
            case "training-item-entry-reaction": case "training-terrain-entry-reaction": GainHonor(player.Id, 1, "Training entry reaction", source); break;
            case "meiyu-fire-tokens": case "torazu-fire-tokens":
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                break;
            case "torazu-fire-attack":
                DestroyToken(source!, source!.Tokens.First(t => t.Type == "Fire"));
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "shinjo-eun-sahng-attack": case "shosuro-konishi-attack": case "tsuruchi-kosoko-attack": case "stalking-tiger-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "tsuruchi-isas-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, source.Id)) return;
                break;
            case "bayushi-kahoku-attack": case "asako-ume-attack":
            case "dark-naga-scouts-attack": case "horse-archers-attack": case "hiruma-sniper-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "cavalry-flankers-attack": case "murderous-thieves-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "training-honor-cost-attack":
                // The Honor cost is paid up front, like the Gold cost at the top of UseAbility --
                // not contingent on the Attack actually resolving or hitting.
                GainHonor(player.Id, -2, "Training Honor-cost Attack", source);
                if (ResolveOrDeferAttack(id, player, target?.Id)) return;
                break;
            case "training-gain-reconnaissance": GrantReconnaissance(player.Id, 1 - player.Id, command.Province!.Value); break;
            case "training-reconnaissance-strike":
                if (HasReconnaissance(player.Id, target!.OwnerId, target.ProvinceIndex!.Value)) GainHonor(target.OwnerId, -2, "Training Reconnaissance strike", source);
                break;
            case "tactical-advantage":
                ApplyModifier(Card(command.PerformerId!), "tactical-advantage", "tactical-advantage", "Force", Definition(target!).Focus);
                DiscardFate(player, target!);
                break;
            case "training-keyword-conditional-force-boost": ApplyModifier(Card(command.PerformerId!), id, id, "Force", 2); break;
            case "equip-battle":
            {
                var performer = Card(command.PerformerId!);
                var spell = target!;
                Pay(player.Id, spell, Definition(spell).GoldCost, command.SourceIds);
                player.Hand.Remove(spell.Id);
                spell.Zone = Zone.Attached; spell.AttachedTo = performer.Id; spell.FaceUp = true; spell.ProvinceIndex = performer.ProvinceIndex;
                Emit("equipped", $"{Definition(spell).Name} attached to {Definition(performer).Name}.", player.Id, spell.Id);
                // A Spell's own "after entering play" Reaction (if any) takes priority over the
                // Battle-ability follow-up below; no published Spell needs both yet.
                if (OpenEntryReactionWindow(player.Id, spell, 1 - player.Id)) return;
                // "You may take an additional action to use a battle ability on that spell" -- a
                // single, bounded, same-player decision (act once, or decline), not a back-and-forth
                // reaction window. Skipping the trailing NextAction below keeps priority with the
                // same player; Apply()'s "spellFollowUp" branch closes the window after exactly one
                // useAbility or decline. Nothing to offer if the Spell has no Battle ability at all.
                if ((Definition(spell).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var spellAbility) && spellAbility.Timing == "Battle"))
                {
                    _state.PendingChoice = new("spellFollowUp", player.Id, spell.Id, "You may immediately use a Battle ability on the attached Spell, or decline.");
                    Emit("reactionWindow", "Spell follow-up window opened.", player.Id, spell.Id);
                    return;
                }
                break;
            }
            case "reiyu-force-penalty": ApplyModifier(target!, source!.Id, id, "Force", -CardForce(source)); break;
            case "training-spell-force-boost": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "horibe-bow": target!.Bowed = true; break;
            case "horibe-straighten": Straighten(target!); break;
            case "atsumichi-bow": target!.Bowed = true; break;
            case "atsumichi-straighten": Straighten(target!); break;
            case "tetsuo-destroy-attachment": DestroyAttachment(target!); break;
            case "tetsuo-destroy-weaker-weaponed": DestroyUnits([target!], true); break;
            case "shunsuke-make-dead": MakeDead(target!, true); break;
            case "training-make-dead": MakeDead(target!, true); break;
            case "training-attack-boost": _state.PendingAttack!.Strength += 2; break;
            case "training-attack-negate": _state.PendingAttack!.Strength = 0; break;
            default: throw new RuleViolation("Ability effect is not implemented.");
        }
        if (opponentHonorBeforeBattleAction is int before && Player(target!.OwnerId).Honor < before && Definition(Card(player.StrongholdId)).Effect == "law-of-darkness-punish")
            // The source is the acting player's own Stronghold, never the losing player's -- "cards
            // you own" in a Honor-loss-immunity passive could never reach it regardless.
            GainHonor(target.OwnerId, -1, "Law of Darkness Dojo", Card(player.StrongholdId));
        // A Terrain's own ability (e.g. an entering-play Reaction) runs from Zone.Battlefield, where
        // it stays -- only the normal one-shot, played-from-hand Strategy case discards afterward.
        if (source is not null && Definition(source).Type == CardType.Strategy && source.Zone == Zone.Hand) DiscardFate(player, source);
        NextAction(player.Id);
    }

    private void ResolveShinden(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseShinden" && command.PlayerId == pending.PlayerId && command.Choice is "bow" or "honor", "The targeted Personality's controller must choose bowing or Honor.");
        var card = Card(pending.CardId);
        if (command.Choice == "bow")
        {
            card.Bowed = true; card.StraightenBlockedTurn = _state.TurnNumber;
            Emit("shindenAccepted", $"{Definition(card).Name} bowed and cannot straighten this turn.", card.OwnerId, card.Id);
        }
        else GainHonor(pending.ActingPlayerId!.Value, 1, "Shinden Asahina");
        _state.PendingChoice = null;
        NextAction(pending.ActingPlayerId!.Value);
    }

    private void CycleProvinces(PlayerState player, string[] ids)
    {
        // The submitted order is the bottom-of-deck order; replacements are revealed together.
        var provinces = ids.Select(id => player.Provinces.Single(p => p.CardId == id)).ToArray();
        foreach (var id in ids)
        {
            var card = Card(id); card.Zone = Zone.DynastyDeck; card.FaceUp = false; card.ProvinceIndex = null;
            player.DynastyDeck.Add(id);
        }
        foreach (var province in provinces) province.CardId = null;
        foreach (var province in provinces.OrderBy(p=>p.Index))
        {
            Refill(player, province);
            if (province.CardId is string id) { Card(id).FaceUp = true; Emit("provinceRevealed", $"{player.Name} revealed {Definition(Card(id)).Name} in province {province.Index+1}.", player.Id, id); }
        }
    }

    private void Straighten(CardInstance card)
    {
        if (card.StraightenBlockedTurn == _state.TurnNumber || Definition(card).Effect == "skip-first-straighten" && Player(card.OwnerId).TurnsTaken < 2)
            Emit("straightenNegated", $"{Definition(card).Name} could not straighten.", card.OwnerId, card.Id);
        else card.Bowed = false;
    }
    private void MoveHome(CardInstance card, bool bow)
    {
        ReturnHome(card, bow);
        Emit("unitMovedHome", $"{Definition(card).Name}'s unit moved home{(bow ? " bowed" : "")}.", card.OwnerId, card.Id);
    }
    private void DiscardFate(PlayerState player, CardInstance card)
    {
        player.Hand.Remove(card.Id); card.Zone = Zone.FateDiscard; card.FaceUp = true;
        Emit("fateDiscarded", $"{player.Name} discarded {Definition(card).Name}.", player.Id, card.Id);
    }
    // Terrain (a Strategy played as a Battle action): leaves the battlefield either when destroyed by
    // an ability targeting it, or automatically once its printed effects have been applied during
    // battle resolution (FinishBattleResolution) -- both paths share this single removal.
    private void DiscardTerrain(CardInstance terrain)
    {
        terrain.Zone = Zone.FateDiscard; terrain.ProvinceIndex = null; terrain.FaceUp = true;
        Emit("terrainDiscarded", $"{Definition(terrain).Name} was removed from play.", terrain.OwnerId, terrain.Id);
    }
    // A Ranged/Melee Attack's target is either a lone Follower (an attachment) or a Personality with
    // no Followers attached (a full unit) -- the two existing destruction paths, chosen by type.
    private void DestroyAttackTarget(CardInstance target)
    {
        if (Definition(target).Type == CardType.Follower) DestroyAttachment(target);
        else DestroyUnits([target], true);
    }
    private void DestroyAttachment(CardInstance card)
    {
        var parent = Card(card.AttachedTo);
        card.Zone = Zone.FateDiscard; card.AttachedTo = null; card.ProvinceIndex = null; card.FaceUp = true;
        parent.GrantedKeywords.RemoveAll(g => g.Source == card.Id && g.Duration == "Attachment");
        card.Tokens.Clear();
        Emit("attachmentDestroyed", $"{Definition(card).Name} was destroyed.", card.OwnerId, card.Id);
        if (PersonalityChi(parent) <= 0) DestroyUnits([parent], true);
    }

    // Tokens (core rules Glossary — Token): create/move/destroy are the only ways a token's placement
    // changes. IDs are a persistent sequence on MatchState, not a fresh Guid, so replay stays deterministic.
    private CardToken CreateToken(CardInstance card, string type, string source, string? stat = null, int amount = 0)
    {
        var token = new CardToken($"token-{++_state.TokenSequence:D4}", type, source, stat, amount);
        card.Tokens.Add(token);
        if (stat == "Chi" && Definition(card).Type == CardType.Personality && InPlay(card) && PersonalityChi(card) <= 0) DestroyUnits([card], true);
        Emit("tokenCreated", $"{Definition(card).Name} gained a {type} token.", card.OwnerId, card.Id);
        return token;
    }
    private void MoveToken(CardInstance from, CardToken token, CardInstance to)
    {
        from.Tokens.Remove(token);
        to.Tokens.Add(token);
        Emit("tokenMoved", $"A {token.Type} token moved to {Definition(to).Name}.", to.OwnerId, to.Id);
    }
    private void DestroyToken(CardInstance card, CardToken token)
    {
        card.Tokens.Remove(token);
        Emit("tokenDestroyed", $"{Definition(card).Name}'s {token.Type} token was destroyed.", card.OwnerId, card.Id);
    }
    private static int TokenCount(CardInstance card, string type) => card.Tokens.Count(t => t.Type == type);
    private static bool HasToken(CardInstance card, string type) => card.Tokens.Any(t => t.Type == type);

    // onlyCardId restricts to exactly one card's own abilities -- used only for the spellFollowUp
    // window (Equip (Battle)'s immediate optional follow-up), which is not a normal Phase.Battle
    // offer and must not include every other card's abilities or the standing player abilities.
    private IEnumerable<ActionOption> AbilityActions(int playerId, string? onlyCardId = null)
    {
        var player = Player(playerId);
        var sources = _state.Cards.Values.Where(c => c.OwnerId == playerId && (InPlay(c) || c.Zone == Zone.Hand))
            .SelectMany(c => (Definition(c).Abilities ?? []).Select(id => (Card: (CardInstance?)c, Id: id))).ToList();
        if (onlyCardId is not null) sources = sources.Where(s => s.Card?.Id == onlyCardId).ToList();
        else if (!Definition(Card(player.StrongholdId)).ScenarioOnly)
            sources.AddRange(AbilityCatalog.All.Where(p => p.Value.PlayerAbility).Select(p => (Card: (CardInstance?)null, Id: p.Key)));
        var inPlay = _state.Cards.Values.Where(InPlay).Select(c => c.Id).ToArray();
        foreach (var (source, id) in sources)
        {
            var ability = AbilityCatalog.All[id];
            var phaseOk = ability.Timing switch { "Battle" => _state.Phase == Phase.Battle, "BattleOrOpen" => _state.Phase is Phase.Action or Phase.Battle, _ => _state.Phase == Phase.Action };
            if (!phaseOk) continue;
            if (ability.Timing == "Limited" && playerId != _state.ActivePlayerId) continue;
            string?[] performers = ability.Performer switch
            {
                "shugenja" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Keyword(c, "Shugenja") && !c.Bowed).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c)).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-samurai-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, "Samurai")).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-personality-at-battle-with-keyword" => ability.RequiredKeyword is not { } requiredKeyword ? [] :
                    _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, requiredKeyword)).Select(c => (string?)c.Id).ToArray(),
                "own-castle-or-siege" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && !c.Bowed && (Keyword(c, "Castle") || Keyword(c, "Siege"))).Select(c => (string?)c.Id).ToArray(),
                "own-personality" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => (string?)c.Id).ToArray(),
                "own-personality-with-fire-token-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && HasToken(c, "Fire")).Select(c => (string?)c.Id).ToArray(),
                "own-tactician-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && Keyword(c, "Tactician")).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-shugenja-opposed-at-battle" => !Army(1 - playerId, _state.CurrentBattleProvince ?? -1).Any() ? [] :
                    _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, "Shugenja")).Select(c => (string?)c.Id).ToArray(),
                _ => [null]
            };
            // Unlike every other Performer-based ability (still offered, disabled, with no eligible
            // performer -- see the generic fallback below), Equip (Battle) is not offered at all
            // without an eligible Shugenja or a Spell in hand: it is a standing player ability with
            // no source card of its own to anchor a disabled entry to.
            if (id == "equip-battle" && (performers.Length == 0 || !player.Hand.Any(h => Definition(Card(h)).Type == CardType.Spell))) continue;
            if (performers.Length == 0) performers = [null];
            foreach (var performer in performers)
            {
                var cost = source is not null && Definition(source).Type == CardType.Strategy ? Definition(source).GoldCost : 0;
                var payment = PaymentOptions(playerId, cost).FirstOrDefault();
                var provinces = player.Provinces.Where(p => !p.Destroyed && p.CardId is not null).Select(p => p.CardId!).ToArray();
                var prototype = new GameCommand("useAbility", playerId, _state.Version, source?.Id, payment?.SourceIds,
                    CardIds: ability.Target == "provinces" ? provinces : null, AbilityId: id, PerformerId: performer);
                string?[] candidates = ability.Target switch { "none" or "provinces" => new string?[] {null}, "hand" => player.Hand.ToArray(),
                    "spell-in-hand" => player.Hand.Where(h => Definition(Card(h)).Type == CardType.Spell).Cast<string?>().ToArray(),
                    "attack" => inPlay.Cast<string?>().Append(null).ToArray(),
                    "discarded-personality" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone is Zone.DynastyDiscard or Zone.FateDiscard).Select(c => (string?)c.Id).ToArray(),
                    _ => inPlay };
                var legal = new List<GameCommand>(); string? error = null;
                foreach (var target in candidates)
                {
                    // Equip (Battle)'s cost is the targeted Spell's own GoldCost, not a fixed
                    // per-performer cost -- each candidate needs its own payment recomputed.
                    var command = ability.Target == "spell-in-hand" && target is not null
                        ? prototype with { TargetId = target, SourceIds = PaymentOptions(playerId, Definition(Card(target)).GoldCost).FirstOrDefault()?.SourceIds }
                        : prototype with { TargetId = target };
                    try { ValidateAbility(command); legal.Add(command); }
                    catch (RuleViolation e) { error ??= e.Message; }
                }
                var label = ability.Label + (performer is null ? "" : $" · {Definition(Card(performer)).Name} ({performer})");
                yield return new("useAbility", label, legal.Count > 0, legal.Count > 0 ? null : error ?? "No legal target or performer.",
                    legal.FirstOrDefault(), source?.Id, ability.Target == "provinces" ? provinces : legal.Where(c => c.TargetId is not null).Select(c => c.TargetId!).ToArray());
            }
        }
    }
}
