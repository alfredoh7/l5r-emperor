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
// RequireNoAttachments layers "without attachments" (core rules: a boolean, not a count -- see
// HasAttachments) onto whichever Target category is chosen, instead of a separate category per
// combination (e.g. "enemy-force" + RequireNoAttachments, rather than a new "enemy-force-no-
// attachments" string) -- reusable across every existing category a printed "without attachments"
// card might otherwise need.
public sealed record AbilityDefinition(string Timing, string Label, string Target, bool PlayerAbility = false, string? Performer = null, string? Trigger = null, string? UsageKey = null, string? RequiredKeyword = null, bool RequireNoAttachments = false);

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
        // Utaku Chikako (issue #186): two independent effects -- the Honor gain and "may not declare
        // an attack this turn" both apply regardless of the other (see the execution case). Limited
        // is inherently restricted to the acting player's own active turn already, so the user's own
        // "if used when it is not their turn" clause -- honor gain unaffected either way -- describes
        // a case this engine's Limited timing already makes structurally unreachable.
        ["utaku-chikako-decline"] = new("Limited", "Gain 2 Honor. You may not declare an attack this turn.", "none"),
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
        // Justice of the Crane: "If this Personality has the printed Magistrate keyword, you may take
        // an additional action" -- checked against the WIELDER's own printed Keywords specifically
        // (PrintedKeyword), not Keyword()'s printed-or-granted blend, since Justice of the Crane's own
        // AttachGrantsKeyword:"Magistrate" would otherwise make the condition trivially always true.
        // Deferred when first wired (#179/#180), before the Additional Action primitive existed (#181).
        ["justice-of-crane-dishonor"] = new("Battle", "Dishonor a target enemy Personality", "enemy-personality"),
        ["again-boost"] = new("Battle", "Again!: Straighten your performing Kensai and his Weapons; their Battle abilities may be used again this turn; you may take an additional Battle action",
            "none", Performer: "own-personality-with-keyword-at-battle", RequiredKeyword: "Kensai"),
        // "From cards in his unit" -- unlike Again!'s unscoped grant, these two are only usable by the
        // Kensai himself or one of his own attached Followers/Items (see the execution case below).
        ["two-hands-grant"] = new("Battle", "Choose your performing Kensai: take two additional Battle actions from cards in his unit; other actions will not grant new additional Battle actions this battle",
            "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeyword: "Kensai"),
        ["effortless-counterattack-negate"] = new("Reaction", "Before an additional Battle action resolves: negate its effects", "none", Trigger: "before-additional-action-resolves"),
        ["effortless-counterattack-move"] = new("Battle", "Target your performing unbowed Personality: move home a target Personality without attachments",
            "personality", Performer: "own-unbowed-personality-at-battle", RequireNoAttachments: true),
        // Ranged/Melee Attack (core rules Glossary): targeting a Follower or a Personality without
        // Followers, Force <= strength, is resolved at the moment the effect happens rather than
        // required up front -- the action stays legal to take with no eligible target in sight.
        ["carpenter-castle-siege"] = new("Battle", "Bow a Castle/Siege card · Ranged 5 Attack", "attack", Performer: "own-castle-or-siege"),
        ["unparalleled-sword-attack"] = new("Battle", "Melee 4 Attack", "attack"),
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
        // Generic "without attachments" filter (issue #148): layers onto an existing category
        // (here "enemy-force") rather than needing its own -- matches Thunderous Legion/Elite
        // Sentry's exact printed shape ("Target an enemy card without attachments: Destroy it").
        ["training-destroy-no-attachments"] = new("Battle", "Target an enemy card without attachments: Destroy it", "enemy-force", RequireNoAttachments: true),
        // Province adjacency training fixture (Moto Gunso's shape, simplified to one target).
        ["training-adjacent-boost"] = new("Battle", "Target a Personality at an adjacent battlefield: +2F", "personality-at-adjacent-battlefield"),
        // Ashigaru Elite's shape: tireless (see the usage-dedupe exemption below) and stacking --
        // each use bows a different unbowed Farm and increments a per-battle counter (MatchState.AshigaruBoost)
        // read by PersonalityForce/FollowerForce for any Ashigaru-keyword card the performer controls
        // at this battlefield.
        ["training-ashigaru-boost"] = new("Battle", "Bow an unbowed Farm you control: each Ashigaru Personality/Follower you control at this battlefield gains +1F for the remainder of the battle", "own-unbowed-farm"),
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
        // Training-only: exercises Again!'s "Battle abilities on his Weapons may be used a second
        // time this turn" -- a Weapon carrying its own printed Battle attack (no published one is
        // wired yet with a plain attack this simple; Unparalleled Sword's own is used for the real-card
        // combination instead of this fixture where a specific card matters).
        ["training-weapon-attack"] = new("Battle", "Melee 2 Attack", "attack"),
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
        // opposed-at-battle checks Type==Personality). The immediate optional follow-up action is an
        // Additional Action grant (issue #181) scoped to the just-attached Spell, opened in the
        // execution case below -- not the bespoke spellFollowUp PendingChoice this used to be.
        // Reserve (core rules, issue #184): a Fate card with this keyword may ALSO be attached via
        // Equip (Battle), following the exact same rules as a Spell (still only to an opposed unbowed
        // Shugenja) -- Item/Follower/Spell alike, whichever the printed keyword happens to be on. The
        // one difference (a non-Spell Reserve attachment never grants the bonus Additional Action for
        // its own Battle ability) is applied in the execution case below, not here.
        ["equip-battle"] = new("Battle", "Equip (Battle): Attach a Spell or a Reserve Fate card to your opposed unbowed Shugenja, paying all costs", "spell-or-reserve-in-hand", PlayerAbility: true, Performer: "own-unbowed-shugenja-opposed-at-battle"),
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
        ["spawn-of-vritra-attack"] = new("Battle", "Ranged 6 Attack", "attack"),
        ["horseback-warriors-attack"] = new("Battle", "Melee 4 Attack, or Melee 5 Attack if the target is Infantry", "attack"),
        ["khol-regulars-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["khol-regulars-reaction"] = new("Reaction", "If this card is at the current battlefield, after a Ranged Attack is targeted: +2 strength", "none", Trigger: "before-attack-resolves"),
        ["bounty-hunter-bow"] = new("Battle", "Bow a target attachment or a target dishonorable Personality", "attachment-or-dishonorable-personality"),
        ["rumormonger-penalty"] = new("Open", "Bow this card and target a Personality controlled by a player who controls a dishonorable Personality: Force penalty equal to his base Personal Honor", "personality-controller-controls-dishonorable"),
        ["daifuke-swap-force"] = new("Open", "Target another player's Follower or Personality: switch the target's and this Follower's Force", "enemy-force"),
        // Moto Gunso's shape: not a single-target ability at all -- every qualifying Follower gets the
        // bonus at once (see the execution case), reusing province adjacency (AdjacentProvinces).
        ["moto-gunso-boost"] = new("Battle", "Give each of your other Followers at the current battlefield and at each adjacent battlefield +1F", "none"),
        ["elite-sentry-destroy"] = new("Battle", "Destroy a target enemy card without attachments", "enemy-force", RequireNoAttachments: true),
        // Tireless (see the usage-dedupe exemption): straightens the Follower's OWN unit (its wielder
        // and every attachment), not a separately-chosen target -- Target "none", resolved from source.
        ["elite-sentry-straighten"] = new("BattleOrOpen", "Straighten this Follower's unit", "none"),
        ["furugaro-charge"] = new("Limited", "Destroy this Follower to target a Fudo Personality: give him two Charging tokens", "own-personality-with-keyword", RequiredKeyword: "Fudo"),
        ["panku-madness"] = new("Limited", "Give a target card a Madness token", "any-card"),
        ["soshi-conditional-bow"] = new("BattleOrOpen", "Target a Personality: if it is a Combat Segment, bow him; if he is dishonorable, his controller loses 1 Honor", "personality"),
        ["unseen-assassin-strike"] = new("Limited", "Bow this Follower to destroy a target Follower; destroy this Follower and lose 2 Honor", "enemy-follower"),
        ["asahina-straighten"] = new("BattleOrOpen", "Target your bowed Personality: straighten him and gain 1 Honor", "own-bowed"),
        // The real Ashigaru Elite, sharing the exact mechanism the training fixture demonstrated.
        ["ashigaru-elite-boost"] = new("Battle", "Bow an unbowed Farm you control: each Ashigaru Personality/Follower you control at this battlefield gains +1F for the remainder of the battle", "own-unbowed-farm"),
        // Kharmic keyword's own repeatable action (not tied to any specific card): pay 2 Gold, discard
        // a Kharmic card you control. A Dynasty card is replaced with a fresh, immediately face-up
        // Dynasty card (see the execution case); a Fate card's discard is followed by an extra draw.
        ["kharmic-discard"] = new("Limited", "Pay 2 Gold, discard a Kharmic card you control: a Dynasty card is replaced face-up; a Fate card is followed by an extra draw", "own-kharmic-card", PlayerAbility: true),
        // Rings (issue #194): each real Ring has two abilities -- its own recruit action (moves it
        // from hand into play, gated by its printed entry condition) and its printed in-play action
        // (also usable directly from hand, discarding the Ring instead of bowing it -- "you may
        // discard this Ring from your hand to use its ability without cost"). Recruitment for the
        // four Reaction-shaped Rings below is granted as an Additional Battle Action (GrantRingRecruit)
        // rather than a forced mid-resolution interrupt -- the user's own simplification.
        ["ring-of-air-recruit"] = new("Battle", "Play after you resolve two or more Favor actions in one turn", "none"),
        ["ring-of-air-straighten"] = new("BattleOrOpen", "Air Repeatable Battle/Open, bow: Straighten your target Personality or attachment", "personality-or-attachment"),
        ["ring-of-water-recruit"] = new("Battle", "Play after a battle resolves in which you played a Terrain, destroyed a Terrain, and destroyed any cards or provinces during resolution", "none"),
        // Simplified from the printed "Absent Repeatable Battle" (issue #194): the no-presence-
        // required ("Absent") half is not modeled this pass, and only the "move home" half of the
        // dual move effect is wired -- the "or move him to the current battlefield if he would be
        // opposed" half is deferred. See docs/ROADMAP.md.
        ["ring-of-water-move-home"] = new("Battle", "Move your target Personality home", "own-personality"),
        // Ring of the Void (issue #194): a Limited ability (not a granted Additional Action) -- see
        // its own Require (two or fewer Rings in play) -- whose cost is discarding the player's
        // entire hand (except the Void ring itself).
        ["ring-of-void-recruit"] = new("Limited", "Play if you have two or fewer Rings in play. Discard your hand", "none"),
        ["ring-of-void-draw"] = new("Limited", "Void Limited, bow: Draw a card. If you now have more cards in your hand than any other player, discard a card", "none"),
        ["false-ring-of-air-recruit"] = new("Battle", "After you win a battle in which there was ever a unit on the opposing side, you may put this Ring into play and lose 2 Honor", "none"),
        ["false-ring-of-air-force"] = new("Battle", "Absent Battle, bow: Give a target Personality -3F", "personality"),
        // Not a Reaction/grant at all (issue #194) -- a standing condition, usable any time it holds,
        // like an ordinary Battle ability. Simplified from "Absent Battle" the same way as above.
        ["false-ring-of-water-recruit"] = new("Battle", "If the current battlefield's Province Strength is 11 or greater, put this Ring into play and lose 2 Honor", "none"),
        ["false-ring-of-water-attack"] = new("Battle", "Ranged 3 Attack", "attack"),
        // Moshi Ikako (issue #194's own review of previously Ring-blocked cards): "Fire Battle" is a
        // plain printed-keyword classification (same treatment as Kiho/Recon), not a Ring-gated
        // Timing as an earlier blocker note speculated -- confirmed now that Rings are actually
        // built. Zero new primitives: a plain Ranged 1 Attack.
        ["moshi-ikako-attack"] = new("Battle", "Fire Battle: Ranged 1 Attack", "attack"),
        // Scholarly Hermit (issue #194's own review): needed only a Ring-type check in a discard
        // pile, now trivial. New Target "own-ring-in-discard".
        ["scholarly-hermit-search"] = new("Limited", "Bow: Put a Ring from your discard pile into your hand. Destroy this Follower.", "own-ring-in-discard"),
        // Training-only: exercises Discipline (issue #184) -- played from a discard pile, paying the
        // card's own printed Gold cost, then removed from the game (see the generic Timing=="Discipline"
        // handling: zone check, cost, and post-resolution removal all key off this one Timing value).
        ["training-discipline-ability"] = new("Discipline", "Discipline: pay this card's Gold cost, gain 1 Honor, then remove it from the game", "none"),
        // Training-only: exercises Stalwart (issue #184) -- a per-instance (not per-player) enemy-bow
        // ability, so two separate source instances can each bow the same Stalwart target once this
        // turn, isolating the keyword's own negation from the ordinary once-per-turn dedupe.
        ["training-bow-enemy"] = new("Open", "Training: bow a target enemy Personality", "enemy-personality"),
        // Training-only: exercises Stalwart's "never the card's own controller" exclusion -- "personality"
        // allows either side, unlike training-bow-enemy above.
        ["training-bow-any"] = new("Open", "Training: bow a target Personality, own or enemy", "personality"),
        // Singh Remnants' own Reaction: unlike "own-enters-play" (the entering card's own ability),
        // this fires when a DIFFERENT card the player controls (a Personality) enters play, letting
        // Singh Remnants -- sitting in hand or a discard pile -- attach itself. Target "none": the
        // Personality is implicit (the entryReaction window's own CardId), not separately chosen.
        ["singh-remnants-attach"] = new("Reaction", "After your Personality enters play, if this card is in your hand or discard pile: attach it to him, paying all costs. Gain 1 Honor.", "none", Trigger: "any-personality-enters-play"),
        ["training-personality-entry-watch"] = new("Reaction", "Training: after a Personality enters play, attach this card to it, gain 1 Honor", "none", Trigger: "any-personality-enters-play"),
        // Clay Kshatriya's Invest: reuses the same own-enters-play Reaction/entryReaction window every
        // other "after entering play" ability already shares -- genuinely optional (the player may
        // decline), unlike a mandatory entry effect, so no new window shape is needed.
        ["clay-kshatriya-invest"] = new("Reaction", "Invest 5 Gold: give this Follower two +1F tokens", "none", Trigger: "own-enters-play"),
        ["training-invest"] = new("Reaction", "Training: Invest 3 Gold: give this Follower a +1F token", "none", Trigger: "own-enters-play"),
        ["aulus-entry-honor-loss"] = new("Reaction", "After entering play, lose 2 Honor", "none", Trigger: "own-enters-play"),
        ["panku-entry-honor-loss"] = new("Reaction", "After entering play, lose 1 Honor", "none", Trigger: "own-enters-play"),
        ["thunderous-legion-destroy"] = new("Battle", "Destroy a target enemy card without attachments", "enemy-force", RequireNoAttachments: true),
        ["brothers-great-lake-bow"] = new("Battle", "Bow a target enemy card", "enemy-force"),
    });
}

public sealed partial class MatchEngine
{
    private static bool InPlay(CardInstance card) => card.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Attached;
    private static bool Keyword(CardInstance card, string keyword) => (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword)).Contains(keyword, StringComparer.Ordinal);
    // Printed only, never a granted keyword -- Justice of the Crane's own text draws this exact
    // distinction ("has the PRINTED Magistrate keyword"), since it also grants Magistrate itself and
    // would otherwise always satisfy a plain Keyword() check.
    private static bool PrintedKeyword(CardInstance card, string keyword) => (Definition(card).Keywords ?? []).Contains(keyword, StringComparer.Ordinal);
    // Stalwart (core rules, issue #184): "the first time on a turn an enemy's effect would bow a card
    // with this keyword, the bowing (and only the bowing) is negated" -- a targeted ability's OWN
    // effects (Force penalties, Honor loss, and so on) still apply in full; only the .Bowed=true this
    // call would have made is skipped. Never negates the card's own controller bowing it (a cost, or
    // its own ability) -- that is never "an enemy's effect" to begin with. Reuses AbilityUses' own
    // per-card, per-TurnNumber convention (a synthetic "stalwart-negate" key) rather than a new field:
    // TurnNumber already increments at the end of EVERY player's own turn (FinishTurn), which is
    // exactly "resets at each player's end phase, regardless of whose turn it is."
    private bool StalwartNegatesBow(CardInstance card, int actingPlayerId)
    {
        if (card.OwnerId == actingPlayerId || !Keyword(card, "Stalwart")) return false;
        if (card.AbilityUses.TryGetValue("stalwart-negate", out var used) && used == _state.TurnNumber) return false;
        card.AbilityUses["stalwart-negate"] = _state.TurnNumber;
        Emit("stalwartNegated", $"{Definition(card).Name}'s Stalwart negates the bow.", card.OwnerId, card.Id);
        return true;
    }
    // Most Personalities have one printed Clan (the CardDefinition.Clan field); a few carry more than
    // one alignment (Clans overrides it when present). Loyal restricts recruitment to a Stronghold
    // whose Clan is among these.
    private static string[] ClanAlignments(CardInstance card) => Definition(card).Clans ?? [Definition(card).Clan];
    // "Attachment": lasts while the granting attachment stays attached (cleared in DestroyAttachment).
    // "Turn": cleared at FinishTurn, the default for a keyword granted by an action's effect.
    // "Permanent": only when the granting card's text says so; never auto-cleared.
    // A card already carrying a keyword (printed or previously granted) gains no redundant second
    // grant of it -- membership is boolean, so a duplicate entry would only ever complicate cleanup
    // (e.g. two Naval-granting attachments on the same Personality, one later detached) for no effect.
    private static void GrantKeyword(CardInstance card, string keyword, string source, string duration) { if (!Keyword(card, keyword)) card.GrantedKeywords.Add(new(keyword, source, duration)); }
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
        "unparalleled-sword-attack" => 4,
        "shinjo-eun-sahng-attack" => 4,
        "shosuro-konishi-attack" or "tsuruchi-kosoko-attack" => 5,
        "stalking-tiger-attack" => 6,
        "tsuruchi-isas-attack" => 3,
        "bayushi-kahoku-attack" => 3 + (target is not null && target.Dishonorable ? 2 : 0),
        "asako-ume-attack" => 4,
        "training-honor-cost-attack" => 3,
        "training-weapon-attack" => 2,
        "cavalry-flankers-attack" => 2,
        "dark-naga-scouts-attack" => 3,
        "moshi-ikako-attack" => 1,
        "murderous-thieves-attack" => 3,
        "horse-archers-attack" or "hiruma-sniper-attack" => 4,
        "spawn-of-vritra-attack" => 6,
        "khol-regulars-attack" => 4,
        // Horseback Warriors: Melee 4, or 5 against a target without the Cavalry keyword -- "Infantry"
        // has no printed keyword of its own (core rules Glossary), it just means not Cavalry.
        "horseback-warriors-attack" => 4 + (target is not null && !Keyword(target, "Cavalry") ? 1 : 0),
        "false-ring-of-water-attack" => 3,
        _ => 0
    };
    // AttackStrength plus every passive defense modifier applying to this specific target (Destrier/
    // Singh Remnants/Elephant Cavalry's shape -- see CardDefinition.AttackDefenseKind). Used at both
    // declare-time validation and the strength baked into PendingAttack, so a Reaction's own further
    // adjustment always stacks on top of this, never bypasses it.
    private int EffectiveAttackStrength(string abilityId, PlayerState player, CardInstance? target) =>
        AttackStrength(abilityId, player, target) + (target is null ? 0 : AttackDefenseModifier(target, AttackKind(abilityId)));
    // Sums every card in `target`'s own unit (its parent Personality, plus every attachment) granting
    // a passive Attack-strength modifier of this `kind` that reaches `target` specifically: Scope
    // "Self" (protects only the granter itself), "Wielder" (an Item's default -- conveys to the
    // Personality it's attached to, not the Item itself), or "Unit" (reaches every card in the unit,
    // including the granter).
    private int AttackDefenseModifier(CardInstance target, string kind)
    {
        var parent = target.Zone == Zone.Attached ? Card(target.AttachedTo!) : target;
        return new[] { parent }.Concat(Attachments(parent.Id)).Sum(granter =>
        {
            var def = Definition(granter);
            if (def.AttackDefenseKind is null || def.AttackDefenseKind != "Both" && def.AttackDefenseKind != kind) return 0;
            if (def.AttackDefenseScope == "Unit") return def.AttackDefenseAmount;
            var grantedTo = def.AttackDefenseScope == "Wielder" && granter.Zone == Zone.Attached ? Card(granter.AttachedTo!) : granter;
            return grantedTo.Id == target.Id ? def.AttackDefenseAmount : 0;
        });
    }
    // Cards that affect only Ranged Attacks (or only Melee) reference this; the two kinds otherwise
    // share the same resolution pathway (core rules Glossary — Ranged Attack/Melee Attack).
    private static string AttackKind(string abilityId) => abilityId switch
    {
        "carpenter-castle-siege" or "monastery-fire-attack" or "torazu-fire-attack" => "Ranged",
        "shiro-shiba-melee" or "stalking-tiger-attack" or "unparalleled-sword-attack" or "training-weapon-attack" => "Melee",
        "shinjo-eun-sahng-attack" or "shosuro-konishi-attack" or "tsuruchi-kosoko-attack" or "tsuruchi-isas-attack" or "bayushi-kahoku-attack" or "asako-ume-attack" or "training-honor-cost-attack" => "Ranged",
        "cavalry-flankers-attack" or "murderous-thieves-attack" => "Melee",
        "dark-naga-scouts-attack" or "horse-archers-attack" or "hiruma-sniper-attack" => "Ranged",
        "moshi-ikako-attack" => "Ranged",
        "spawn-of-vritra-attack" or "khol-regulars-attack" => "Ranged",
        "horseback-warriors-attack" => "Melee",
        "false-ring-of-water-attack" => "Ranged",
        _ => ""
    };
    // General "in play" Reaction primitive: a printed Reaction whose card must actually be in play
    // (recruited or attached -- Personality, Item, Follower, Spell, Holding, Region, Ring, Celestial
    // alike, whichever InPlay's own Zone check already covers) and unbowed to ever matter. There is
    // no need to evaluate a trigger's own (possibly elaborate) condition, or pause play offering it,
    // when the card carrying it isn't even in play -- this cheap existence check comes first and
    // gates whether a reaction window needs to open at all. `before-attack-resolves` is the first
    // caller; every future in-play trigger (e.g. Legion of the Bat's own "after an action moves an
    // enemy Infantry Personality to this card's battlefield," once that move-resolved hook exists)
    // reuses this directly instead of a bespoke AnyXReactionAvailable check.
    private bool AnyInPlayReactionAvailable(string trigger) => _state.Cards.Values.Any(c => InPlay(c) && !c.Bowed &&
        (Definition(c).Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == trigger &&
            (!c.AbilityUses.TryGetValue(id, out var used) || used != _state.TurnNumber)));
    // No kind-restricted in-play Reaction exists yet (see AttackKind); once one does, filter by
    // attack.Kind here rather than inside AnyInPlayReactionAvailable itself.
    private bool AnyAttackReactionAvailable() => AnyInPlayReactionAvailable("before-attack-resolves");
    // General "out of play" Reaction primitive: some printed Reactions are played FROM outside play
    // (hand or a discard pile) to interrupt something -- Singh Remnants attaching itself as a
    // Personality enters play is the first case; Effortless Counterattack negating an action taken
    // as part of an additional action (Again!, or Equip (Battle)'s immediate Spell follow-up) is a
    // future one. Unlike an in-play Reaction, there is no "is the card even in play" shortcut to take
    // first -- whether one applies always needs checking when the specific triggering event occurs;
    // callers open a window whenever their own trigger condition occurs, gated by this existence
    // check (not per-card AbilityUses, since a played reaction leaves hand/discard and can't recur).
    private bool AnyOutOfPlayReactionAvailable(int playerId, string trigger) => _state.Cards.Values.Any(c =>
        c.OwnerId == playerId && c.Zone is Zone.Hand or Zone.FateDiscard &&
        (Definition(c).Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == trigger));
    private bool AnyPersonalityEntryReactionAvailable(int playerId) => AnyOutOfPlayReactionAvailable(playerId, "any-personality-enters-play");
    // Additional Action (issue #181): a grant is a Battle action the named player may take without
    // ceding priority to their opponent first -- Again!'s shape (ScopeCardIds null: any of the
    // player's own cards) or Two Hands' shape (a specific set: "from cards in his unit"). Two Hands'
    // own restriction blocks future GRANTS, not the use of ones already sitting here.
    private void GrantAdditionalAction(int playerId, string[]? scopeCardIds)
    {
        if (_state.NoNewAdditionalActionsThisBattle) return;
        _state.AdditionalActions.Add(new(playerId, scopeCardIds));
        Emit("additionalActionGranted", $"{Player(playerId).Name} may take an additional Battle action.", playerId);
    }
    // Ring recruitment (issue #194): rather than a forced mid-resolution interrupt, a satisfied
    // condition grants an Additional Battle Action scoped to that specific Ring card in hand (the
    // user's own simplification) -- consumed like any other Additional Action, including the same
    // Reaction opportunity for something like Effortless Counterattack to negate it. Idempotent: a
    // Ring already granted this battle is not granted again.
    private void GrantRingRecruit(PlayerState player, bool condition, string recruitAbilityId)
    {
        if (!condition) return;
        var ring = player.Hand.Select(Card).FirstOrDefault(c => (Definition(c).Abilities ?? []).Contains(recruitAbilityId));
        if (ring is null) return;
        if (!_state.AdditionalActions.Any(g => g.PlayerId == player.Id && g.ScopeCardIds is { } ids && ids.Contains(ring.Id)))
            GrantAdditionalAction(player.Id, [ring.Id]);
    }
    // Ring of Water's own recruitment condition (issue #194): "played a Terrain, destroyed a
    // Terrain, and destroyed any cards or provinces during resolution" -- the first two are this
    // player's own battle-scoped flags; `destroyedSomething` is this specific resolution's own fact,
    // passed in by the caller (no "win" requirement, unlike False Ring of Air).
    private void CheckRingOfWaterRecruit(PlayerState player, bool destroyedSomething) =>
        GrantRingRecruit(player, player.TerrainPlayedThisBattle && player.TerrainDestroyedThisBattle && destroyedSomething, "ring-of-water-recruit");
    // Removes and returns whether an available grant matches this specific use (unscoped, or scoped
    // to include cardId) -- the single check deciding whether a Battle-timed useAbility is an
    // ordinary action or one taken "out of order" as an Additional Action (see UseAbility).
    private bool ConsumeAdditionalActionGrant(int playerId, string? cardId)
    {
        var grant = _state.AdditionalActions.FirstOrDefault(g => g.PlayerId == playerId && (g.ScopeCardIds is null || cardId is not null && g.ScopeCardIds.Contains(cardId)));
        if (grant is null) return false;
        _state.AdditionalActions.Remove(grant);
        return true;
    }
    // Effortless Counterattack's shape: "before the resolution of a Battle action taken as an
    // additional action, negate its effects" -- an out-of-play Reaction (played from hand), same
    // defer-then-resolve shape as the Attack primitive (ResolveOrDeferAttack). Negation itself is
    // just never executing the deferred command: the "additionalActionReaction" case in Apply()
    // clears PendingAdditionalAction whenever the Reaction's own effect (effortless-counterattack-
    // negate) fires, so the closing check below finds nothing left to resolve.
    private void ResolveOrDeferAdditionalAction(GameCommand command)
    {
        if (!AnyOutOfPlayReactionAvailable(1 - command.PlayerId, "before-additional-action-resolves"))
        {
            ExecuteAbilityEffects(command, consumedGrant: true);
            return;
        }
        _state.PendingAdditionalAction = command;
        _state.PendingChoice = new("additionalActionReaction", command.PlayerId, command.CardId, "An additional action is pending: react or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = 1 - command.PlayerId;
        Emit("reactionWindow", "Additional-action reaction window opened.", command.PlayerId);
    }
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
    // additional (issue #181): was this specific Attack itself taken as a consumed Additional Action
    // grant? The immediate (non-deferred) branch below needs nothing extra -- it falls through to
    // ExecuteAbilityEffects' own tail, which already knows. Only the deferred branch must carry it
    // across to ResolveAttack, called later once the reaction window closes.
    private bool ResolveOrDeferAttack(string abilityId, PlayerState attacker, string? targetId, string? sourceId = null, bool additional = false)
    {
        var strength = EffectiveAttackStrength(abilityId, attacker, targetId is null ? null : Card(targetId));
        if (!AnyAttackReactionAvailable())
        {
            ResolveAttackNow(abilityId, attacker.Id, targetId, strength, sourceId);
            return false;
        }
        _state.PendingAttack = new PendingAttack { AbilityId = abilityId, PlayerId = attacker.Id, SourceId = sourceId, TargetId = targetId, Strength = strength, Kind = AttackKind(abilityId), Additional = additional };
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
        // Same "an unrelated action forfeits dangling grants" rule as ExecuteAbilityEffects' own tail.
        if (!attack.Additional) _state.AdditionalActions.RemoveAll(g => g.PlayerId == attack.PlayerId);
        NextAction(attack.PlayerId, attack.Additional && _state.AdditionalActions.Any(g => g.PlayerId == attack.PlayerId));
    }
    private bool IsAttacking(CardInstance card) => card.Zone == Zone.Battlefield && card.OwnerId == _state.ActivePlayerId;
    // "Defending": at the current battlefield and owned by the non-active player -- unlike IsAttacking
    // above (which only ever needs the card's own Zone, since it's always called on a Personality),
    // this resolves through an attachment's parent first, since a Follower's own Zone is Attached,
    // never Battlefield.
    private bool IsDefendingUnit(CardInstance card)
    {
        var unit = card.Zone == Zone.Attached ? Card(card.AttachedTo) : card;
        return AtBattle(unit) && unit.OwnerId != _state.ActivePlayerId;
    }
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
    private int CardForce(CardInstance card) => Definition(card).Type switch
    {
        CardType.Personality => PersonalityForce(card),
        CardType.Follower => FollowerForce(card),
        _ => Math.Max(0, Definition(card).Force + StatModifier(card, "Force"))
    };
    private int PersonalityChi(CardInstance card) => Definition(card).Chi + StatModifier(card, "Chi") + Attachments(card.Id).Where(c => Definition(c).Type == CardType.Item).Sum(c => Definition(c).Chi);

    // Shared by action discovery and execution: an offered target obeys exactly the same constraints.
    private void ValidateAbility(GameCommand command)
    {
        var id = command.AbilityId ?? "";
        Require(AbilityCatalog.All.TryGetValue(id, out var ability), "Unknown ability.");
        Require(ability!.Timing switch {
            "Limited" => _state.Phase == Phase.Action && command.PlayerId == _state.ActivePlayerId,
            "Open" => _state.Phase == Phase.Action,
            // Discipline (core rules, issue #184): played from a discard pile, not in play -- see the
            // source-zone check below (Zone.DynastyDiscard/FateDiscard, not InPlay). No printed timing
            // restriction of its own beyond that, so both phases a card could plausibly reach a
            // discard pile and still matter in are allowed, like BattleOrOpen.
            "Discipline" => _state.Phase is Phase.Action or Phase.Battle,
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
                // Singh Remnants' shape: the reacting card is NOT the one that entered (no CardId
                // match), just watching for any of the player's own Personalities entering play --
                // "proclaim" combines this same window with a Proclaim-eligible recruit's own reaction.
                "any-personality-enters-play" => _state.PendingChoice.Type is "proclaim" or "entryReaction" && command.PlayerId == _state.PendingChoice.PlayerId
                    && Definition(Card(_state.PendingChoice.CardId!)).Type == CardType.Personality,
                "before-attack-resolves" => _state.PendingChoice.Type == "attackReaction",
                "before-additional-action-resolves" => _state.PendingChoice.Type == "additionalActionReaction",
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
            // entering-play Reaction, must still be usable from there. Singh Remnants' shape is the
            // opposite of ordinary: its whole point is reacting from OUTSIDE play (hand or discard).
            Require(ability.Trigger == "any-personality-enters-play" ? source.Zone is Zone.Hand or Zone.FateDiscard
                : ability.Timing == "Discipline" ? source.Zone is Zone.FateDiscard or Zone.DynastyDiscard
                // A Ring (issue #194) may use its printed action from hand too -- "you may discard
                // this Ring from your hand to use its ability without cost" -- in addition to the
                // ordinary in-play case once recruited (ExecuteAbilityEffects' own tail discards it
                // afterward when used this way, mirroring the Strategy case just below).
                : def.Type is CardType.Strategy or CardType.Ring ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || InPlay(source) : InPlay(source),
                "The ability's source is not in the required zone.");
            Require(!source.Bowed || id == "shinden-choice", "Abilities on bowed cards cannot be used.");
            uses = source.AbilityUses;
        }
        // "Any number of times per turn" (Tactical Advantage, Equip (Battle)), and "tireless" (Ashigaru
        // Elite -- explicitly stacks with other uses of the action -- and Elite Sentry's straighten):
        // exempt from the once-per-turn dedupe entirely -- each one's own cost/requirements (a
        // different unbowed Farm each time, for Ashigaru Elite) are the only real limit.
        var usageKey = ability.UsageKey ?? id;
        Require(id is "tactical-advantage" or "equip-battle" or "training-ashigaru-boost" or "ashigaru-elite-boost" or "elite-sentry-straighten" or "kharmic-discard"
            // "Repeatable" (issue #194): Ring of Air/Water's own printed keyword on their in-play
            // actions, same convention as Tactical Advantage/Equip (Battle) above.
            or "ring-of-air-straighten" or "ring-of-water-move-home"
            || !uses.TryGetValue(usageKey, out var used) || used != _state.TurnNumber && id != "border-game", "This ability has already been used within its limit.");
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
        // Ring recruitment (issue #194): each of these three is only ever playable as the specific
        // Additional Battle Action already granted for it (GrantRingRecruit) -- never as an ordinary
        // ability -- since the underlying historical condition (Favor actions this turn, a Terrain
        // played/destroyed this battle, an opposing unit ever present in a won battle) is not itself
        // re-checked here. This also naturally makes the grant, not a separate flag, the single
        // source of truth for "was this satisfied."
        if (id is "ring-of-air-recruit" or "ring-of-water-recruit" or "false-ring-of-air-recruit")
            Require(_state.AdditionalActions.Any(g => g.PlayerId == player.Id && g.ScopeCardIds is { Length: 1 } grantIds && grantIds[0] == source!.Id),
                "This Ring's recruitment condition has not been granted yet.");
        // Ring of the Void's own entry condition (issue #194): a Limited ability (not a granted
        // Additional Action), gated only by how many Rings this player already controls.
        if (id == "ring-of-void-recruit")
            Require(_state.Cards.Values.Count(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Ring) <= 2,
                "You may only play this Ring with two or fewer Rings in play.");
        // False Ring of Water's own entry condition (issue #194): a standing check, not a Reaction --
        // usable any time it holds, like an ordinary Battle ability. The current battlefield's own
        // province always belongs to the Defender.
        if (id == "false-ring-of-water-recruit")
            Require(Player(1 - _state.ActivePlayerId).Provinces.Single(p => p.Index == _state.CurrentBattleProvince!.Value).Strength >= 11,
                "The current battlefield's Province Strength must be 11 or greater.");
        // Torazu's own ability, on Torazu, needs no separate Performer -- the shared Battle presence
        // check above already requires the source itself (a Personality) to be at this battlefield.
        if (id == "torazu-fire-attack") Require(HasToken(source!, "Fire"), "Torazu needs a Fire token of his own to attack.");
        if (id is "horibe-bow" or "horibe-straighten")
            Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Horibe must be opposed to use this ability.");
        if (id is "heavy-chargers-boost") Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Heavy Chargers must be opposed to use this ability.");
        // Khol Regulars: "If this card is at the current battlefield, after a Ranged Attack is
        // targeted" -- both narrow the generic before-attack-resolves Reaction window this card shares
        // with every other attack-boosting Reaction.
        if (id == "khol-regulars-reaction") Require(AtBattle(source!) && _state.PendingAttack!.Kind == "Ranged", "Khol Regulars must be at the current battlefield, reacting to a Ranged Attack.");
        if (id == "cavalry-flankers-attack") Require(player.Id == _state.ActivePlayerId, "Only the Attacker may use this ability.");
        if (id == "hiruma-sniper-attack") Require(HasReconnaissance(player.Id, 1 - player.Id, _state.CurrentBattleProvince!.Value), "Hiruma Sniper requires Reconnaissance at the current battlefield.");
        // Singh Remnants' shape: attach to the just-entered Personality (the entryReaction's own
        // CardId), following the same restrictions Equip() enforces -- Honor Requirement included.
        if (ability.Trigger == "any-personality-enters-play")
        {
            var enteredPersonality = Card(_state.PendingChoice!.CardId!);
            var attachDef = Definition(source!);
            Require(attachDef.AttachRequiresKeyword is null || KeywordOrHuman(enteredPersonality, attachDef.AttachRequiresKeyword), $"Will only attach to a {attachDef.AttachRequiresKeyword} Personality.");
            Require(attachDef.AttachExcludesKeyword is null || !KeywordOrHuman(enteredPersonality, attachDef.AttachExcludesKeyword), $"Will not attach to a {attachDef.AttachExcludesKeyword} Personality.");
            Require(attachDef.AttachRequiresName is null || Definition(enteredPersonality).Name == attachDef.AttachRequiresName, $"Only attaches to {attachDef.AttachRequiresName}.");
            Require(attachDef.HonorRequirement is null || CardPersonalHonor(enteredPersonality) >= attachDef.HonorRequirement, "The Personality does not meet this Follower's Personal Honor Requirement.");
            Require(AttachmentAllowed(source!, enteredPersonality), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
            // Gold cost (attachDef.GoldCost) is validated once, generically, in the shared cost
            // computation below -- not duplicated here.
        }
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
                // Again!'s shape: also no "unbowed" qualifier -- the whole point is straightening an
                // already-bowed Kensai. Generic on RequiredKeyword like the unbowed variant above.
                "own-personality-with-keyword-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) &&
                    ability.RequiredKeyword is not null && Keyword(performer, ability.RequiredKeyword),
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
                    // Core rules: a Personality with any attachment (not just a Follower) is immune to
                    // a Ranged/Melee Attack, unless a specific card prints an exception (e.g. "which
                    // may target a Personality with attached Followers if you have Reconnaissance") --
                    // no such exception is wired up yet, since no imported Attack needs one.
                    Require(type == CardType.Follower || type == CardType.Personality && !HasAttachments(target),
                        "Attack a Follower or a Personality without attachments.");
                    // Seasoned Ashigaru's shape: an immunity to one specific Attack kind only, checked
                    // against the ability's own AttackKind (already tracked per ability id). Effect, not
                    // Keywords -- this is a synthetic marker for the card's own unique text, not a
                    // printed keyword other cards could reference.
                    Require(AttackKind(id) != "Melee" || Definition(target).Effect != "melee-attack-immune", "Melee Attacks may not target this card.");
                    Require(CardForce(target) <= EffectiveAttackStrength(id, player, target), "That card's Force is above the Attack's strength.");
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
                else if (ability.Target == "spell-or-reserve-in-hand")
                {
                    Require(target.OwnerId == player.Id && target.Zone == Zone.Hand && (type == CardType.Spell || Keyword(target, "Reserve")), "Choose a Spell, or a Reserve Fate card, from your hand.");
                    Require(AttachmentLegalTarget(Definition(target), Card(command.PerformerId!)), "This attachment will not attach to that Shugenja.");
                    Require(AttachmentAllowed(target, Card(command.PerformerId!)), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
                }
                // Kharmic's own repeatable action: a Kharmic-keyword card the player controls, either a
                // Fate card in hand or a face-up Dynasty card in one of their own provinces -- neither
                // zone is "in play", so this needs its own branch, same shape as "hand" above. Face-up
                // matches DiscardProvince's own existing rule for discarding a province card at all.
                else if (ability.Target == "own-kharmic-card")
                    Require(target.OwnerId == player.Id && Keyword(target, "Kharmic")
                        && (target.Zone == Zone.Hand || target.Zone == Zone.Province && target.FaceUp),
                        "Discard a Kharmic card you control, from your hand or a face-up province.");
                // Scholarly Hermit's own ability (issue #194): a Ring in the player's own Fate
                // discard pile -- not in play, so this needs its own branch too.
                else if (ability.Target == "own-ring-in-discard")
                    Require(target.OwnerId == player.Id && Definition(target).Type == CardType.Ring && target.Zone == Zone.FateDiscard,
                        "Choose a Ring from your Fate discard pile.");
                else
                {
                    Require(InPlay(target), "Target a card in play.");
                    // Two deliberate exceptions to "every Battle-timing target must be at the current
                    // battlefield": "personality-at-adjacent-battlefield" (Moto Gunso's shape -- the
                    // whole point is a target away from the current battlefield) and "own-unbowed-farm"
                    // (Ashigaru Elite's cost-target, a Farm to bow, lives in Zone.Home).
                    if (ability.Timing == "Battle" && ability.Target is not ("personality-at-adjacent-battlefield" or "own-unbowed-farm")) Require(AtBattle(target), "Target or performer must be at the current battlefield.");
                    var personality = type == CardType.Personality;
                    var own = target.OwnerId == player.Id;
                    Require((!ability.RequireNoAttachments || !HasAttachments(target)) && ability.Target switch {
                        "shinden" => personality && !own && !target.Bowed,
                        "air" => personality && Keyword(target, "Air"),
                        "personality" => personality,
                        // Ashigaru Elite's cost-target: an unbowed Farm-keyword Holding the performer
                        // controls. Bowing it (in the execution switch) pays the cost; the effect
                        // itself is a counter increment, not applied to this target at all.
                        "own-unbowed-farm" => type == CardType.Holding && own && !target.Bowed && Keyword(target, "Farm"),
                        "own-personality" => personality && own,
                        // Ring of Air (issue #194): "your target Personality or attachment" -- own
                        // only, either half of a unit (unlike own-unit-card elsewhere, this also
                        // includes Spells, since a Spell is straightenable just like any attachment).
                        "personality-or-attachment" => own && (personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell),
                        // Generic "Choose your performing/target [Keyword] Personality" -- for a
                        // keyword with no mechanical rule of its own (e.g. Berserker), or in addition
                        // to one a keyword already has (e.g. Tactician): see AbilityDefinition.RequiredKeyword.
                        "own-personality-with-keyword" => personality && own && ability.RequiredKeyword is not null && Keyword(target, ability.RequiredKeyword),
                        "enemy-personality" => personality && !own,
                        "own-bowed" => personality && own && target.Bowed,
                        "own-opposed" => personality && own && Army(1-player.Id, _state.CurrentBattleProvince!.Value).Any(),
                        "enemy-force" => !own && type is CardType.Personality or CardType.Follower,
                        "enemy-follower" => !own && type == CardType.Follower,
                        "any-card" => true,
                        "attacking" => personality && target.OwnerId == _state.ActivePlayerId,
                        "defending" => personality && target.OwnerId != _state.ActivePlayerId,
                        "enemy-attacking" => personality && !own && target.OwnerId == _state.ActivePlayerId,
                        "enemy-attachment" => !own && target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        "attachment" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        "lobby" => personality && own && !target.Bowed && CardPersonalHonor(target) >= 1,
                        "fewer-or-equal-followers-than-performer" => personality &&
                            Attachments(target.Id).Count(a => Definition(a).Type == CardType.Follower) <= Attachments(command.PerformerId!).Count(a => Definition(a).Type == CardType.Follower),
                        "lower-or-equal-honor-than-performer" => personality && CardPersonalHonor(target) <= CardPersonalHonor(Card(command.PerformerId!)),
                        "own-personality-with-token" => personality && own && target.Tokens.Count > 0,
                        "own-tattooed-personality" => personality && own && Keyword(target, "Tattooed"),
                        "personality-lower-honor-than-self" => personality && CardPersonalHonor(target) < CardPersonalHonor(source!),
                        "enemy-personality-fewer-weapons-than-self" => personality && !own &&
                            Attachments(target.Id).Count(a => Keyword(a, "Weapon")) < Attachments(source!.Id).Count(a => Keyword(a, "Weapon")),
                        "terrain-at-battle" => type == CardType.Strategy && Keyword(target, "Terrain"),
                        // Province adjacency (Moto Gunso's shape: "...at the current battlefield and
                        // at each adjacent battlefield"): the target's own province, not the current
                        // one, must be adjacent to it. See AdjacentProvinces.
                        "personality-at-adjacent-battlefield" => personality && target.Zone == Zone.Battlefield &&
                            AdjacentProvinces(target.OwnerId, _state.CurrentBattleProvince!.Value).Contains(target.ProvinceIndex ?? -1),
                        // Compared against the chosen Performer (a separately-selected card), unlike
                        // "-than-self" categories above which compare against the ability's own source.
                        "enemy-personality-chi-lte-performer" => personality && !own && Definition(target).Chi <= Definition(Card(command.PerformerId!)).Chi,
                        "enemy-force-lower-than-performer" => !own && type is CardType.Personality or CardType.Follower && CardForce(target) < CardForce(Card(command.PerformerId!)),
                        // Bounty Hunter: "a target attachment or a target dishonorable Personality" --
                        // an OR of two otherwise-unrelated existing categories.
                        "attachment-or-dishonorable-personality" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell
                            || personality && target.Dishonorable,
                        // Rumormonger: any Personality controlled by a player who controls at least one
                        // Dishonorable Personality (not necessarily this target itself).
                        "personality-controller-controls-dishonorable" => personality &&
                            _state.Cards.Values.Any(c => c.OwnerId == target.OwnerId && Definition(c).Type == CardType.Personality && InPlay(c) && c.Dishonorable),
                        _ => false
                    }, "That card does not meet this ability's target or performer requirements.");
                }
            }
        }
        // Equip (Battle) has no source card of its own -- its cost is the targeted Spell's, paid on
        // attachment, not a Strategy's own GoldCost.
        var cost = source is not null && Definition(source).Type == CardType.Strategy ? Definition(source).GoldCost
            : ability.Target == "spell-or-reserve-in-hand" && command.TargetId is not null ? Definition(Card(command.TargetId)).GoldCost
            // Singh Remnants' shape: attaching itself, paying all costs -- its own GoldCost, already
            // separately validated above against the just-entered Personality's own restrictions.
            : ability.Trigger == "any-personality-enters-play" ? Definition(source!).GoldCost
            // Discipline (core rules, issue #184): "typically... a Gold cost" -- the card's own
            // printed GoldCost, same convention as a Strategy card played from hand.
            : ability.Timing == "Discipline" ? Definition(source!).GoldCost
            : id == "kharmic-discard" ? 2
            // Invest (core rules): a separate, optional, standalone payment made only as the card
            // enters play (already enforced by reusing the own-enters-play Reaction/entryReaction
            // window, which only stays open for that one entry) -- never rolled over from the
            // card's own recruit/attach cost. Each Invest card's own amount is hardcoded per ability
            // id here, same convention as Kharmic's fixed 2 Gold.
            : id == "clay-kshatriya-invest" ? 5
            : id == "training-invest" ? 3 : 0;
        ValidatePayment(player.Id, cost, command.SourceIds);
    }

    private void UseAbility(GameCommand command)
    {
        ValidateAbility(command);
        var ability = AbilityCatalog.All[command.AbilityId!];
        // Additional Action (issue #181): a Battle-timed use that matches one of this player's
        // currently available grants is taken "out of order" -- deferred through the same
        // before-additional-action-resolves Reaction check the Attack primitive already uses (see
        // ResolveOrDeferAttack), so a card like Effortless Counterattack gets a genuine chance to
        // negate it before its effects apply. A use that does not match any grant (or is not a
        // Battle ability at all -- a Reaction played mid-window, say) is an ordinary action.
        if (ability.Timing == "Battle" && ConsumeAdditionalActionGrant(command.PlayerId, command.CardId))
        {
            ResolveOrDeferAdditionalAction(command);
            return;
        }
        ExecuteAbilityEffects(command);
    }
    // consumedGrant (issue #181): true when the caller above already removed a matching Additional
    // Action grant for this use. Threaded through so the tail below knows whether to keep priority
    // with the same player instead of ceding it, on top of whatever this ability's own effects do
    // (Again!/Two Hands themselves add fresh grants directly in the switch, detected generically by
    // comparing the grant count before and after rather than a separate special case per card).
    private void ExecuteAbilityEffects(GameCommand command, bool consumedGrant = false)
    {
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
        // Snapshotted by reference (not just a count): a switch case that itself grants fresh actions
        // (Again!/Two Hands/equip-battle) must never accidentally spare an unrelated PRE-EXISTING
        // grant from forfeiture below just because the total count happens to end up higher.
        var preExistingGrants = _state.AdditionalActions.Where(g => g.PlayerId == player.Id).ToList();
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
            case "paid-bow": case "unorthodox-attack": case "chokehold": case "immovable-object-bow": case "naga-remnants-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "veteran-advisor-draw": Draw(player); break;
            case "hired-legion-draw": source!.Bowed = true; Draw(player); break;
            case "akodo-regulars-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", Definition(target!).Focus); break;
            case "heavy-chargers-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", 3); break;
            case "spitting-llama-force": ApplyModifier(target!, source!.Id, id, "Force", -3); break;
            case "training-adjacent-boost": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "blind-rage": ApplyModifier(Card(command.PerformerId!), id, id, "Force", 5); GainHonor(player.Id, -2, "Blind Rage"); break;
            case "shunya-honor": GainHonorFromPerformer(source!, 2, "Doji Shunya"); break;
            case "utaku-chikako-decline": GainHonorFromPerformer(source!, 2, "Utaku Chikako"); player.MayNotAttackThisTurn = true; break;
            case "kouki-force": source!.Bowed = true; ApplyModifier(target!, source.Id, id, "Force", -3); break;
            case "prepared-death": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "costly-feud": ApplyModifier(target!, source!.Id, id, "Force", -3); if (CardForce(target!) == 0 && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "defensive-stance": Straighten(target!); ApplyModifier(target!, source!.Id, id, "Force", 3); break;
            case "crushing-force": ApplyModifier(target!, source!.Id, id, "Force", 3); break;
            case "crushing-destroy": case "never-afraid": DestroyAttachment(target!); break;
            case "incapacitated": case "favor-battle": MoveHome(target!, false); break;
            case "block-supply": MoveHome(target!, !StalwartNegatesBow(target!, player.Id)); break;
            case "lobby": target!.Bowed = true; _state.FavorPlayerId = player.Id; Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id); break;
            case "favor-cycle": DiscardFate(player, target!); Draw(player); break;
            case "journeys-end-move": MoveHome(target!, false); break;
            case "shamate-move": MoveHome(target!, false); if (target!.OwnerId != player.Id) GainHonorFromPerformer(Card(command.PerformerId!), 2, "Shamate Keep"); break;
            case "law-of-darkness-weaken": ApplyModifier(target!, source!.Id, id, "Force", -4); if (target!.Dishonorable && !StalwartNegatesBow(target, player.Id)) target.Bowed = true; break;
            case "justice-of-crane-dishonor":
                Dishonor(target!);
                // "If this Personality has the printed Magistrate keyword" (issue #186): checked as
                // though the wielder carried no attachments/permanent effects granting it -- exactly
                // what PrintedKeyword already does (it reads only Definition.Keywords, never
                // GrantedKeywords, so Justice of the Crane's own AttachGrantsKeyword grant of
                // Magistrate to this same wielder never satisfies this check).
                if (PrintedKeyword(Card(source!.AttachedTo!), "Magistrate")) GrantAdditionalAction(player.Id, null);
                break;
            case "again-boost":
            {
                var kensai = Card(command.PerformerId!);
                Straighten(kensai);
                foreach (var weapon in Attachments(kensai.Id).Where(a => Keyword(a, "Weapon")))
                {
                    Straighten(weapon);
                    foreach (var weaponAbilityId in Definition(weapon).Abilities ?? [])
                        if (AbilityCatalog.All.TryGetValue(weaponAbilityId, out var weaponAbility) && weaponAbility.Timing == "Battle")
                            weapon.AbilityUses.Remove(weaponAbility.UsageKey ?? weaponAbilityId);
                }
                GrantAdditionalAction(player.Id, null);
                break;
            }
            case "two-hands-grant":
            {
                var kensai = Card(command.PerformerId!);
                var unit = new[] { kensai.Id }.Concat(Attachments(kensai.Id).Select(a => a.Id)).ToArray();
                GrantAdditionalAction(player.Id, unit);
                GrantAdditionalAction(player.Id, unit);
                _state.NoNewAdditionalActionsThisBattle = true;
                break;
            }
            case "effortless-counterattack-negate": _state.PendingAdditionalAction = null; break;
            case "training-discipline-ability": GainHonor(player.Id, 1, "Training Discipline"); break;
            case "training-bow-enemy": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "training-bow-any": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "effortless-counterattack-move": MoveHome(target!, false); break;
            case "training-honor-strike": GainHonor(target!.OwnerId, -2, "Training strike", source); break;
            case "training-destroy-terrain": DiscardTerrain(target!); player.TerrainDestroyedThisBattle=true; break; // Ring of Water's own recruitment condition (issue #194)
            case "carpenter-castle-siege":
                Card(command.PerformerId).Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            case "shiro-shiba-melee": if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return; break;
            case "training-create-token": CreateToken(target!, "Training", id, "Force", 1); break;
            case "training-move-token": MoveToken(target!, target!.Tokens[0], Card(command.PerformerId!)); break;
            case "training-destroy-token": DestroyToken(target!, target!.Tokens[0]); break;
            case "monastery-fire-token": CreateToken(target!, "Fire", id, "Force", 1); break;
            case "monastery-fire-attack":
            {
                var performer = Card(command.PerformerId!);
                DestroyToken(performer, performer.Tokens.First(t => t.Type == "Fire"));
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            }
            case "training-honor-loss-entry": GainHonor(player.Id, -2, "Training entry Honor loss", source); break;
            case "aulus-entry-honor-loss": GainHonor(player.Id, -2, $"{Definition(source!).Name} entering play", source); break;
            case "panku-entry-honor-loss": GainHonor(player.Id, -1, $"{Definition(source!).Name} entering play", source); break;
            case "training-item-entry-reaction": case "training-terrain-entry-reaction": GainHonor(player.Id, 1, "Training entry reaction", source); break;
            case "meiyu-fire-tokens": case "torazu-fire-tokens":
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                break;
            case "torazu-fire-attack":
                DestroyToken(source!, source!.Tokens.First(t => t.Type == "Fire"));
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            case "shinjo-eun-sahng-attack": case "shosuro-konishi-attack": case "tsuruchi-kosoko-attack": case "stalking-tiger-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            case "tsuruchi-isas-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, source.Id, consumedGrant)) return;
                break;
            case "false-ring-of-water-attack":
                if (source!.Zone != Zone.Hand) source.Bowed = true; // the discard-from-hand case is handled generically in the tail below
                if (ResolveOrDeferAttack(id, player, target?.Id, source.Id, consumedGrant)) return;
                break;
            case "bayushi-kahoku-attack": case "asako-ume-attack":
            case "dark-naga-scouts-attack": case "horse-archers-attack": case "hiruma-sniper-attack":
            case "spawn-of-vritra-attack": case "horseback-warriors-attack": case "khol-regulars-attack": case "unparalleled-sword-attack": case "training-weapon-attack": case "moshi-ikako-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            case "khol-regulars-reaction": _state.PendingAttack!.Strength += 2; break;
            case "cavalry-flankers-attack": case "murderous-thieves-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
                break;
            case "training-honor-cost-attack":
                // The Honor cost is paid up front, like the Gold cost at the top of UseAbility --
                // not contingent on the Attack actually resolving or hitting.
                GainHonor(player.Id, -2, "Training Honor-cost Attack", source);
                if (ResolveOrDeferAttack(id, player, target?.Id, additional: consumedGrant)) return;
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
                var attachment = target!;
                Pay(player.Id, attachment, Definition(attachment).GoldCost, command.SourceIds);
                player.Hand.Remove(attachment.Id);
                attachment.Zone = Zone.Attached; attachment.AttachedTo = performer.Id; attachment.FaceUp = true; attachment.ProvinceIndex = performer.ProvinceIndex;
                // Reserve (issue #184): a non-Spell attachment can carry AttachGrantsKeyword too (a
                // Spell never has, so this was never needed before) -- same as the ordinary Equip().
                if (Definition(attachment).AttachGrantsKeyword is string granted) GrantKeyword(performer, granted, attachment.Id, "Attachment");
                Emit("equipped", $"{Definition(attachment).Name} attached to {Definition(performer).Name}.", player.Id, attachment.Id);
                // A Spell's own "after entering play" Reaction (if any) takes priority over the
                // Battle-ability follow-up below; no published Spell needs both yet.
                if (OpenEntryReactionWindow(player.Id, attachment, 1 - player.Id)) return;
                // "You may take an additional action to use a battle ability on that spell" -- Equip
                // (Battle)'s own shape of Additional Action (issue #181), scoped to just the newly-
                // attached card. This is the user's own running example of the mechanic: unlike the
                // old bespoke "act once or decline" window it replaces, it is now subject to the same
                // before-additional-action-resolves Reaction (Effortless Counterattack) and the same
                // Two-Hands-style "no new grants this battle" restriction as any other grant. Reserve
                // (issue #184): a non-Spell Reserve attachment specifically does NOT get this bonus
                // grant for its own Battle ability -- only Spells do (the Reserve keyword's own printed
                // text draws this exact distinction). Nothing to grant if there is no Battle ability at
                // all either.
                if (Definition(attachment).Type == CardType.Spell && (Definition(attachment).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var attachAbility) && attachAbility.Timing == "Battle"))
                    GrantAdditionalAction(player.Id, [attachment.Id]);
                break;
            }
            case "reiyu-force-penalty": ApplyModifier(target!, source!.Id, id, "Force", -CardForce(source)); break;
            case "training-spell-force-boost": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "horibe-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "horibe-straighten": Straighten(target!); break;
            case "atsumichi-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "atsumichi-straighten": Straighten(target!); break;
            case "tetsuo-destroy-attachment": DestroyAttachment(target!); break;
            case "tetsuo-destroy-weaker-weaponed": case "training-destroy-no-attachments": DestroyUnits([target!], true); break;
            // Bowing the target Farm IS the cost; the effect is the counter increment, not applied to
            // the Farm itself. Consumed by PersonalityForce/FollowerForce (Keyword "Ashigaru" + AtBattle).
            case "training-ashigaru-boost": case "ashigaru-elite-boost": target!.Bowed = true; _state.AshigaruBoost[player.Id]++; break;
            case "shunsuke-make-dead": MakeDead(target!, true); break;
            case "training-make-dead": MakeDead(target!, true); break;
            case "training-attack-boost": _state.PendingAttack!.Strength += 2; break;
            case "training-attack-negate": _state.PendingAttack!.Strength = 0; break;
            case "bounty-hunter-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "rumormonger-penalty":
                source!.Bowed = true;
                ApplyModifier(target!, source.Id, id, "Force", -CardPersonalHonor(target!));
                break;
            case "daifuke-swap-force":
            {
                var sourceForce = CardForce(source!); var targetForce = CardForce(target!);
                ApplyModifier(target!, source!.Id, id, "Force", sourceForce - targetForce);
                ApplyModifier(source, target!.Id, id, "Force", targetForce - sourceForce);
                break;
            }
            case "moto-gunso-boost":
            {
                var battlefields = new List<int> { _state.CurrentBattleProvince!.Value };
                battlefields.AddRange(AdjacentProvinces(player.Id, _state.CurrentBattleProvince!.Value));
                foreach (var province in battlefields.Distinct())
                    foreach (var personality in Army(player.Id, province))
                        foreach (var follower in Attachments(personality.Id).Where(c => c.Id != source!.Id && Definition(c).Type == CardType.Follower))
                            ApplyModifier(follower, source!.Id, id, "Force", 1);
                break;
            }
            case "elite-sentry-destroy": case "thunderous-legion-destroy": DestroyUnits([target!], true); break;
            case "brothers-great-lake-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "elite-sentry-straighten":
            {
                var personality = Card(source!.AttachedTo!);
                Straighten(personality);
                foreach (var attachment in Attachments(personality.Id)) Straighten(attachment);
                break;
            }
            case "furugaro-charge":
                DestroyUnits([source!], false);
                CreateToken(target!, "Charging", id);
                CreateToken(target!, "Charging", id);
                break;
            case "panku-madness": CreateToken(target!, "Madness", id); break;
            // Rings (issue #194): every recruit case shares the same shape -- leave hand, enter play
            // face up. False Rings additionally lose 2 Honor (their own printed cost); the three
            // real Rings do not.
            case "ring-of-air-recruit": case "ring-of-water-recruit":
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true;
                break;
            case "false-ring-of-air-recruit": case "false-ring-of-water-recruit":
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true;
                GainHonor(player.Id, -2, Definition(source).Name);
                break;
            case "ring-of-void-recruit":
                foreach (var handId in player.Hand.Where(h => h != source!.Id).ToArray()) DiscardFate(player, Card(handId));
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true;
                break;
            case "ring-of-air-straighten": Straighten(target!); if (source!.Zone != Zone.Hand) source.Bowed = true; break;
            case "ring-of-water-move-home": MoveHome(target!, false); if (source!.Zone != Zone.Hand) source.Bowed = true; break;
            case "false-ring-of-air-force": ApplyModifier(target!, source!.Id, id, "Force", -3); if (source.Zone != Zone.Hand) source.Bowed = true; break;
            case "scholarly-hermit-search":
                source!.Bowed = true;
                player.Hand.Add(target!.Id); target.Zone = Zone.Hand; target.FaceUp = true;
                DestroyUnits([source], false);
                break;
            case "ring-of-void-draw":
                if (source!.Zone != Zone.Hand) source.Bowed = true;
                Draw(player);
                // Two-player simplification (issue #194): "more cards in hand than any other player"
                // is exactly "more than the one opponent" here. Which card to discard is the player's
                // own choice in the printed text; auto-discarding the just-drawn card is a documented
                // simplification rather than a new sub-choice mechanism.
                if (player.Hand.Count > Player(1 - player.Id).Hand.Count) DiscardFate(player, Card(player.Hand[^1]));
                break;
            case "soshi-conditional-bow":
                if (_state.Phase == Phase.Battle && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (target!.Dishonorable) GainHonor(target.OwnerId, -1, "Soshi House Guard", source);
                break;
            case "unseen-assassin-strike":
                // Limited (Action phase), not battle -- combatDestruction stays false for both.
                source!.Bowed = true;
                DestroyUnits([target!], false);
                DestroyUnits([source], false);
                GainHonor(player.Id, -2, "Unseen Assassins", source);
                break;
            case "asahina-straighten":
                Straighten(target!);
                GainHonor(player.Id, 1, "Asahina House Guard", source);
                break;
            case "singh-remnants-attach": case "training-personality-entry-watch":
            {
                var enteredPersonality = Card(_state.PendingChoice!.CardId!);
                var attachDef = Definition(source!);
                Pay(player.Id, source!, attachDef.GoldCost, command.SourceIds);
                if (source!.Zone == Zone.Hand) player.Hand.Remove(source.Id);
                source.Zone = Zone.Attached; source.AttachedTo = enteredPersonality.Id; source.FaceUp = true;
                if (attachDef.AttachGrantsKeyword is string granted) GrantKeyword(enteredPersonality, granted, source.Id, "Attachment");
                GainHonor(player.Id, 1, attachDef.Name, source);
                Emit("equipped", $"{attachDef.Name} attached to {Definition(enteredPersonality).Name}.", player.Id, source.Id);
                break;
            }
            case "clay-kshatriya-invest":
                Pay(player.Id, source!, 5, command.SourceIds);
                CreateToken(source!, "Invest", id, "Force", 1);
                CreateToken(source!, "Invest", id, "Force", 1);
                break;
            case "training-invest":
                Pay(player.Id, source!, 3, command.SourceIds);
                CreateToken(source!, "Invest", id, "Force", 1);
                break;
            case "kharmic-discard":
            {
                Pay(player.Id, target!, 2, command.SourceIds);
                if (target!.Zone == Zone.Hand)
                {
                    player.Hand.Remove(target.Id); target.Zone = Zone.FateDiscard; target.FaceUp = true;
                    Draw(player);
                }
                else
                {
                    var province = player.Provinces.Single(p => p.CardId == target.Id);
                    target.Zone = Zone.DynastyDiscard; target.FaceUp = true; target.ProvinceIndex = null;
                    province.CardId = null;
                    Refill(player, province);
                    // Unlike a normal end-of-turn refill (stays face down until the next Events Phase),
                    // Kharmic's own replacement is immediately face up.
                    if (province.CardId is string newId) Card(newId).FaceUp = true;
                }
                break;
            }
            default: throw new RuleViolation("Ability effect is not implemented.");
        }
        // Ring of Air's own recruitment condition (issue #194): "after you resolve two or more Favor
        // actions in one turn" -- every Favor-consuming ability shares this one shared Require
        // (see above), so the counter and the grant-check are shared here too, rather than
        // duplicated across each of the three execution cases.
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle")
        {
            player.FavorActionsThisTurn++;
            GrantRingRecruit(player, player.FavorActionsThisTurn >= 2, "ring-of-air-recruit");
        }
        if (opponentHonorBeforeBattleAction is int before && Player(target!.OwnerId).Honor < before && Definition(Card(player.StrongholdId)).Effect == "law-of-darkness-punish")
            // The source is the acting player's own Stronghold, never the losing player's -- "cards
            // you own" in a Honor-loss-immunity passive could never reach it regardless.
            GainHonor(target.OwnerId, -1, "Law of Darkness Dojo", Card(player.StrongholdId));
        // A Terrain's own ability (e.g. an entering-play Reaction) runs from Zone.Battlefield, where
        // it stays -- only the normal one-shot, played-from-hand Strategy case discards afterward.
        // A Ring's own recruit ability already moves it out of Zone.Hand within its own execution
        // case above, so it never falls into this branch -- only "discard from hand to use the
        // printed action without cost" (issue #194) does.
        if (source is not null && Definition(source).Type is CardType.Strategy or CardType.Ring && source.Zone == Zone.Hand) DiscardFate(player, source);
        // Discipline (core rules, issue #184): "played and resolved and then removed from the game" --
        // always, whether or not its own effect above did anything else with the card.
        if (ability.Timing == "Discipline") RemoveFromGame(source!);
        // Additional Action (issue #181): an ordinary action that did not itself consume a grant
        // forfeits every one of this player's PRE-EXISTING grants outright (same as explicitly
        // passing, see MatchEngine.Pass) -- "resolve them to completion before resuming the normal
        // turn order" means a normal action ends the burst, it does not just quietly skip past it.
        // A fresh grant this same action creates (Again!/Two Hands/equip-battle, above) is untouched
        // by this -- it was never in preExistingGrants -- and still keeps priority with the player.
        if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
        NextAction(player.Id, _state.AdditionalActions.Any(g => g.PlayerId == player.Id));
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
    // Removed from Game (core rules Glossary, issue #184): terminal and permanent -- unlike
    // Dead/DynastyDiscard/FateDiscard, a card here can never be targeted or used again under any
    // circumstance. Both Fate and Dynasty cards mix into this single zone. Discipline's own cost is
    // paid separately, at the point of activation (see the generic cost-computation block) -- this is
    // only the actual removal, run once the ability it triggered has fully resolved.
    private void RemoveFromGame(CardInstance card)
    {
        if (card.AttachedTo is string parentId) Card(parentId).GrantedKeywords.RemoveAll(g => g.Source == card.Id && g.Duration == "Attachment");
        card.Zone = Zone.RemovedFromGame; card.AttachedTo = null; card.ProvinceIndex = null; card.FaceUp = true; card.Tokens.Clear();
        Emit("removedFromGame", $"{Definition(card).Name} was removed from the game.", card.OwnerId, card.Id);
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
        DestroyedDuringBattleBonus(card);
        ExpendableDraw(card);
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

    private IEnumerable<ActionOption> AbilityActions(int playerId)
    {
        var player = Player(playerId);
        // Discipline (issue #184): a discard-pile card is only a candidate source at all if it carries
        // a Discipline-timed ability -- ValidateAbility's own zone check (called per candidate below)
        // is still the real gatekeeper, this is just which cards get considered in the first place.
        var sources = _state.Cards.Values.Where(c => c.OwnerId == playerId && (InPlay(c) || c.Zone == Zone.Hand ||
                c.Zone is Zone.FateDiscard or Zone.DynastyDiscard && (Definition(c).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Discipline")))
            .SelectMany(c => (Definition(c).Abilities ?? []).Select(id => (Card: (CardInstance?)c, Id: id))).ToList();
        if (!Definition(Card(player.StrongholdId)).ScenarioOnly)
            sources.AddRange(AbilityCatalog.All.Where(p => p.Value.PlayerAbility).Select(p => (Card: (CardInstance?)null, Id: p.Key)));
        var inPlay = _state.Cards.Values.Where(InPlay).Select(c => c.Id).ToArray();
        foreach (var (source, id) in sources)
        {
            var ability = AbilityCatalog.All[id];
            var phaseOk = ability.Timing switch { "Battle" => _state.Phase == Phase.Battle, "BattleOrOpen" or "Discipline" => _state.Phase is Phase.Action or Phase.Battle, _ => _state.Phase == Phase.Action };
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
            // without an eligible Shugenja or a Spell/Reserve card in hand: it is a standing player
            // ability with no source card of its own to anchor a disabled entry to.
            if (id == "equip-battle" && (performers.Length == 0 || !player.Hand.Any(h => Definition(Card(h)).Type == CardType.Spell || Keyword(Card(h), "Reserve")))) continue;
            if (performers.Length == 0) performers = [null];
            foreach (var performer in performers)
            {
                var cost = source is not null && Definition(source).Type == CardType.Strategy ? Definition(source).GoldCost : 0;
                var payment = PaymentOptions(playerId, cost).FirstOrDefault();
                var provinces = player.Provinces.Where(p => !p.Destroyed && p.CardId is not null).Select(p => p.CardId!).ToArray();
                var prototype = new GameCommand("useAbility", playerId, _state.Version, source?.Id, payment?.SourceIds,
                    CardIds: ability.Target == "provinces" ? provinces : null, AbilityId: id, PerformerId: performer);
                string?[] candidates = ability.Target switch { "none" or "provinces" => new string?[] {null}, "hand" => player.Hand.ToArray(),
                    "spell-or-reserve-in-hand" => player.Hand.Where(h => Definition(Card(h)).Type == CardType.Spell || Keyword(Card(h), "Reserve")).Cast<string?>().ToArray(),
                    "attack" => inPlay.Cast<string?>().Append(null).ToArray(),
                    "discarded-personality" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone is Zone.DynastyDiscard or Zone.FateDiscard).Select(c => (string?)c.Id).ToArray(),
                    _ => inPlay };
                var legal = new List<GameCommand>(); string? error = null;
                foreach (var target in candidates)
                {
                    // Equip (Battle)'s cost is the targeted Spell/Reserve card's own GoldCost, not a
                    // fixed per-performer cost -- each candidate needs its own payment recomputed.
                    var command = ability.Target == "spell-or-reserve-in-hand" && target is not null
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
