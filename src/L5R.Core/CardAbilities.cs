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
// RequiredKeywords narrows a keyword-agnostic Target/Performer category (e.g. "own-personality") to
// only cards carrying ALL of the listed printed or granted keywords -- independently present, order
// irrelevant (core rules: a keyword with no mechanical rule of its own still satisfies this kind of
// condition on another card, in addition to any mechanical ability it does carry; a card may name as
// many keywords as it likes, e.g. "a Dragon Clan Kensai" or "a Crane Clan Magistrate" -- issue #190).
// A card with a keyword requirement and no satisfying candidate simply cannot be played/used, same as
// any other unmet Target/Performer condition. See "own-personality-with-keyword" below. Distinct from
// a keyword that only gates PART of an ability's effect once it's already resolving (e.g. Volcano
// Tattoo's own Fire-conditional straighten) -- that is a plain in-place Keyword() check in the
// execution switch, never folded into RequiredKeywords, since failing it negates only that portion of
// the effect rather than blocking the action outright.
// RequireNoAttachments layers "without attachments" (core rules: a boolean, not a count -- see
// HasAttachments) onto whichever Target category is chosen, instead of a separate category per
// combination (e.g. "enemy-force" + RequireNoAttachments, rather than a new "enemy-force-no-
// attachments" string) -- reusable across every existing category a printed "without attachments"
// card might otherwise need.
// Dueling (issue #204): IssuesDuel marks an ability whose own Target/Performer selection (the
// challenged and the challenger) issues a duel challenge instead of resolving inline -- generic
// setup happens once, in UseAbility, before the per-id execution switch; the SAME ability's own id
// is invoked again later, with a known winner/loser, to apply its own printed win/loss outcome
// (ApplyDuelOutcome). DuelStat names the printed duel stat ("Chi"/"Force"/"PersonalHonor"); the
// core rules default (Chi) needs no override. DuelRefusable opens an AwaitingAccept decision for the
// challenged player first; "Your performer duels the target" (no refusal language at all) leaves it
// false, matching every unrefusable duel-issuing card's own printed phrasing.
// Cost (issue #303): the ability's Gold cost RULE, the single piece of data GoldQuotes.cs interprets to
// price it -- validation, option generation, the quote shipped to the client and the actual payment all
// read this one rule. Null means "no explicit rule": a Strategy source pays its own printed cost, a
// Discipline ability played from a discard pile pays its source's published cost (the Discipline cost) and
// played from hand is free (GoldQuotes.ImplicitGoldRule); anything else is free.
// Target status (P2, see TargetStatus): IfTarget is the status the ability's own text conditions its
// EFFECT on ("destroy him if he is dishonorable, Kolat, Ninja, or Shadowlands"; "+2 strength if the target
// is dishonorable") -- read back through MatchEngine.EffectConditionMet. TargetMustBe is a target-LEGALITY
// condition layered onto whichever Target category the ability already uses, exactly like
// RequireNoAttachments ("target an enemy dishonorable Personality"). Both are any-of tests on the target.
// FollowerException (P1, see AttackFollowerException): a permission printed on this Attack ability itself
// -- "which may target a Personality with attached Followers" (unconditional, Reconnaissance-gated, or
// limited to a target status) -- letting its Ranged/Melee Attack target a Personality that still has a
// Follower attached.
// Search (issue #312): the ability's deck search, as declarative data (which deck(s), the criteria each deck's
// candidates must meet, what happens to the chosen card -- see DeckSearch.cs). A non-null Search means using the
// ability starts the shared search flow: (optional deck choice) -> candidate list -> select or pass -> show ->
// effect -> shuffle -> the action resolves. The ability's own cost (Bow Shizuka) is paid in its execution case.
// Look (issue #322): the ability's look at the top N cards of a deck, as declarative data (which deck(s), N, the dispositions
// each viewed card may take with their min/max, where the rest goes, whether the order may change -- see DeckLook.cs).
// Search and Look are the LAST parameters so both sides' positional callers stay valid.
public sealed record AbilityDefinition(string Timing, string Label, string Target, bool PlayerAbility = false, string? Performer = null, string? Trigger = null, string? UsageKey = null, string[]? RequiredKeywords = null, bool RequireNoAttachments = false,
    bool IssuesDuel = false, string DuelStat = "Chi", bool DuelRefusable = false, GoldCostRule? Cost = null,
    TargetStatus? IfTarget = null, TargetStatus? TargetMustBe = null, AttackFollowerException? FollowerException = null,
    // The ability's OWN printed action keyword(s) -- the word(s) before the designator ("Recon Reaction:", "Ninja
    // Battle:", "Political Limited:") that describe only this action, not the whole card (core rules, Abilities).
    // Until Daidoji Yuki (issue #309) every such word was a plain classification with no engine representation
    // (Ninja is read off the source CARD's keyword, Iaijutsu off a hardcoded id list, Political/Kiho/Fire nowhere);
    // this is the general tag a "was that a [keyword] action?" check reads (MatchEngine.ActionCarriesKeyword, which
    // also honours the rule that an ability inherits its card's keywords). Null for an untagged ability.
    string[]? ActionKeywords = null,
    // Discipline (Aftermath supplement; issue #331): an additional way to PLAY the card's own printed action, never a
    // Timing of its own -- Timing stays the printed verb ("Battle", "Battle/Open", ...) so its phase, presence and
    // target rules are exactly what the card prints. The flag adds one source zone and one price: from a DISCARD
    // pile the action may be played by paying the Discipline cost (the card's published GoldCost, priced by
    // GoldQuotes.ImplicitGoldRule like every other cost) and the card is removed from the game after the action
    // resolves; from HAND a Strategy is the ordinary free play (a Discipline Strategy prints no Gold Cost box of
    // its own -- the icon IS the Discipline cost), and it is discarded afterward like any Strategy. Never both
    // costs: the mode is decided by where the source card is when the action is played.
    bool Discipline = false,
    // Absent (Coils of Madness supplement, rule 9): "the action may be taken even without controlling any unit at
    // the current battlefield" -- the acting player needs no presence there, and neither does the performing card
    // (an Absent Battle action can be taken by a card that is not at the battlefield at all). It is still a Battle
    // action: Battle phase only, and its own targets/performers keep whatever location rule they print. Consumed
    // by ValidateAbility's presence Requires (the block on actingAsBattle); nothing else reads it.
    bool Absent = false,
    // Deck search / look (issues #312, #322): appended LAST, after main's ActionKeywords/Discipline/Absent, and always passed by name.
    SearchSpec? Search = null, LookSpec? Look = null,
    // Alternative cost (issue #337, AlternativeCosts.cs): the ways this ability's non-Gold cost may be paid -- "Bow or destroy your
    // performing Ancestor Personality" is two options on one card, "Bow Xijkt or one of your Mines" is one option (bow) whose subject the
    // Performer category widens. Each option is independently payable, the ability is usable when at least one is, and the chosen option
    // is part of the command (GameCommand.CostOption).
    CostOption[]? CostOptions = null,
    // Attach a card to a Personality outside the ordinary Equip command (issue #337, AttachAction.cs): from a discard pile (Moto Jun-Ni,
    // Foothills Keep) or from the source's own place in hand (Fading Shadows). The Gold cost is Cost = GoldCostRule.AttachCost(...).
    AttachSpec? Attach = null,
    // A branch on a condition evaluated at resolution, with the acting player's choice among engine-listed options in the "may" branch
    // (issue #337, EffectChoice.cs): Mirumoto Yumaru.
    ConditionalChoice? Branch = null,
    // Copying (issue #338): this ability itself copies abilities from another card (Fukuzo's), so it is never itself offered as an
    // ability to copy ("an ability that does not itself copy abilities"). See CardCopy.cs.
    bool CopiesAbilities = false,
    // Alternate Gold source (issue #338, Yoritomo Sakuma: "When paying a Gold Cost, bow Sakuma: Produce 2 Gold"): while its card is in
    // play and unbowed, it can be chosen as a payment source producing this much Gold, bowing the card; it is never a stand-alone
    // action. Used at most once a turn, like any Reaction. See AlternateGoldSource in GoldQuotes.cs.
    int? PaymentProduction = null);

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
        ["training-keyword-conditional-straighten"] = new("Open", "Choose your performing Berserker: Straighten him", "own-personality-with-keyword", RequiredKeywords: ["Berserker"]),
        // Training fixture for the generic Performer-side keyword gate (mirrors Tactical Advantage's
        // Performer shape, but for any keyword instead of one hardcoded to Tactician).
        ["training-keyword-conditional-force-boost"] = new("Battle", "Choose your performing Berserker at the current battlefield: +2 Force", "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Berserker"]),
        ["shunya-honor"] = new("Limited", "Discard Favor · gain 2 Honor", "none", ActionKeywords: ["Political"]),
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
        // Goju House Guard (issue #192): "Ninja Battle" -- "Ninja" is a plain printed-keyword
        // classification (same treatment as Recon/Kiho), not a distinct engine Timing; the ordinary
        // "Battle" timing already gates it correctly. The cost (top or bottom of its own owner's
        // Fate deck, DeckPosition) is paid in the execution switch, not the generic cost-computation
        // block, since it is never a Gold amount.
        ["goju-house-guard-return"] = new("Battle", "Put this card on the top or bottom of your Fate deck to target an enemy Personality. Put all cards without attachments in his unit on the bottom of their owner's appropriate deck.", "enemy-personality"),
        // Pearl of Embers (issue #192): zero new primitives -- bow itself as a cost, straighten its
        // own wielder. "Battle/Open" is the existing BattleOrOpen Timing verbatim.
        ["pearl-of-embers-straighten"] = new("BattleOrOpen", "Bow this card: Straighten this Personality", "none"),
        // Ritual Preparation (issue #192): "your target card in a unit" -- a own Personality OR
        // Follower, unlike every existing Target category naming only one of the two. New Target
        // "own-unit-card" (see the generic switch below), the only new piece this needed.
        ["ritual-preparation-straighten"] = new("Battle", "Straighten your target card in a unit", "own-unit-card"),
        ["again-boost"] = new("Battle", "Again!: Straighten your performing Kensai and his Weapons; their Battle abilities may be used again this turn; you may take an additional Battle action",
            "none", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Kensai"]),
        // "From cards in his unit" -- unlike Again!'s unscoped grant, these two are only usable by the
        // Kensai himself or one of his own attached Followers/Items (see the execution case below).
        ["two-hands-grant"] = new("Battle", "Choose your performing Kensai: take two additional Battle actions from cards in his unit; other actions will not grant new additional Battle actions this battle",
            "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Kensai"]),
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
        // Volcano Tattoo (issue #190): "Fire Kiho Battle" -- the printed "Kiho" is a plain
        // classification word on this card (see ROADMAP's Kiho entry), not a distinct Timing; the
        // engine's own "Battle" timing already gates it correctly. "Tattooed Monk" is the generic
        // multi-keyword Performer category (RequiredKeywords, issue #190), not a hardcoded category --
        // both keywords independently required, order irrelevant. Bow the performing Tattooed Monk
        // (cost) to target an unattached card in an enemy unit whose Personality's Chi is at or below
        // the performer's; destroying a Follower counts as "destroyed an attachment" for the
        // conditional Fire-keyword straighten -- Fire itself is NOT in RequiredKeywords: it only gates
        // part of the already-resolving effect, so its absence negates that part rather than blocking
        // the action outright (Followers are modeled as attachments throughout).
        ["volcano-tattoo-destroy"] = new("Battle", "Bow your performing Tattooed Monk: target an unattached card in an enemy unit whose Personality has Chi at or below his. Destroy it. Straighten the performer if he is Fire and this destroyed an attachment.", "enemy-unattached-unit-card-chi-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk", "Tattooed"]),
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
        // Training-only: exercises the general in-play unit-targeting Reaction primitive's own
        // multi-affected-card case ahead of any published card needing it -- Champion of Thunder's
        // own worked example ("if all Followers in its unit suffer a Force penalty, choose which
        // card to negate the effects on"). A single command targets the unit's own host Personality;
        // the effect applies to every member of that unit at once (host and every attachment alike),
        // so a watcher whose OWN unit matches gets a real choice among several simultaneously
        // affected cards, not just the one literal TargetId.
        ["training-unit-force-penalty"] = new("Open", "Training: target an enemy Personality; every card in that unit suffers -1F", "enemy-personality"),
        // Training-only: exercises the Terrain primitive's "may be a target for battle actions that
        // target a Terrain" clause, ahead of any published card needing it.
        ["training-destroy-terrain"] = new("Battle", "Training: destroy the Terrain at this battlefield", "terrain-at-battle"),
        // Import wave 1 (issue #95): unconditional Attacks needing nothing beyond the primitive itself.
        ["shinjo-eun-sahng-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["shosuro-konishi-attack"] = new("Battle", "Ranged 5 Attack", "attack"),
        ["tsuruchi-kosoko-attack"] = new("Battle", "Ranged 5 Attack", "attack"),
        // Tsuruchi Kosoko - exp (oracle-l5r-9831): "Ranged 6 Attack that may target a Personality with
        // Followers" -- the unconditional printed Follower exception (AttackFollowerException shape (a)).
        // Training fixtures for the printed Follower-exception shapes no published card exercises yet: (b) Reconnaissance-gated
        // (Strategic Strike / Toritaka Chokichi's text, both unwired) and (c) target-status-gated, here on a MELEE Attack.
        ["training-followed-recon-attack"] = new("Battle", "Training: Ranged 3 Attack, which may target a Personality with attached Followers if you have Reconnaissance", "attack", FollowerException: new("Ranged", Condition: AttackFollowerException.Reconnaissance)),
        ["training-followed-dishonorable-melee-attack"] = new("Battle", "Training: Melee 3 Attack, which may target a dishonorable Personality with attached Followers", "attack", FollowerException: new("Melee", TargetIs: TargetStatus.IsDishonorable)),
        ["tsuruchi-kosoko-exp-attack"] = new("Battle", "Ranged 6 Attack that may target a Personality with Followers", "attack", FollowerException: new("Ranged")),
        ["stalking-tiger-attack"] = new("Battle", "Melee 6 Attack", "attack"),
        // "Bow Isas: Ranged 3 Attack. Move Isas home." -- the source bows itself as its own performer
        // (no separate Performer field, same shape as kouki-force), then moves home after the Attack
        // resolves regardless of outcome (ResolveAttackNow's per-ability switch).
        ["tsuruchi-isas-attack"] = new("Battle", "Bow Isas · Ranged 3 Attack, then move him home", "attack"),
        // Import wave 2 (issue #97): the Attack primitive's first target-dependent strength.
        ["bayushi-kahoku-attack"] = new("Battle", "Ranged 3 Attack (+2 if the target is dishonorable)", "attack", IfTarget: TargetStatus.IsDishonorable),
        // Bayushi Kahoku - exp (oracle-l5r-807): "Target an enemy Personality: Give him -4F. Ranged 3
        // Attack." TargetId is the -4F target; the Attack's own (separate, optional) target rides on
        // CardIds -- see ValidateAbility's kahoku-exp-attack branch. His static "Ranged Attacks from
        // actions performed by Kahoku may target dishonorable Personalities with attached Followers" is
        // the Effect "ranged-attacks-may-target-dishonorable-with-followers" (StaticAttackFollowerExceptions).
        ["kahoku-exp-attack"] = new("Battle", "Target an enemy Personality: Give him -4F. Ranged 3 Attack", "enemy-personality"),
        // Yoritomo Ebumi (oracle-l5r-9832): the first Follower exception granted by an action for a LATER
        // Attack (AttackFollowerException shape (e)) -- see MatchEngine.GrantAttackFollowerException.
        // P2 wave-24 cards (see TargetStatus): Kakita Kazan uses the status as a target-LEGALITY layer
        // (TargetMustBe); Hiruma Nitani's +2 strength and Kuni Shinoda - exp's destroy use it as an EFFECT
        // condition (IfTarget).
        ["kazan-shame"] = new("Battle", "Target an enemy dishonorable Personality: Bow him. His controller loses 2 Honor. Gain 2 Honor", "enemy-personality", TargetMustBe: TargetStatus.IsDishonorable),
        ["hiruma-nitani-attack"] = new("Battle", "Bow Nitani unless you have Reconnaissance: Ranged 4 Attack, with +2 strength if the target is Shadowlands", "attack", IfTarget: TargetStatus.IsShadowlands),
        ["shinoda-exp-bow-destroy"] = new("Battle", "Target an enemy card: Bow it. If it is Shadowlands, destroy it", "enemy-card", IfTarget: TargetStatus.IsShadowlands),
        ["yoritomo-ebumi-grant"] = new("Battle", "One Ranged Attack from your next Ranged Attack action this battle may target a Personality with attached Followers. If you have Reconnaissance, you may take an additional Battle action", "none"),
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
        // ...and the printed "You have Reconnaissance at the current battlefield", usable by either side of a battle.
        ["training-gain-reconnaissance-here"] = new("Battle", "Training: you have Reconnaissance at the current battlefield", "none"),
        ["training-reconnaissance-strike"] = new("Battle", "Training: enemy Personality loses 2 Honor if you have Reconnaissance there", "enemy-personality"),
        // ---- Battle Phase timing (issues #309/#310): the Engage window, the first-action claim, and the general
        // "before this battle resolves" window. See docs/decisions/2026-09-18-battle-timing-primitives.md.
        // Daidoji Yuki, Experienced (oracle-l5r-1696): "Recon Reaction: After engaging at Yuki's battlefield, if all
        // your Personalities there are Scouts: You have Reconnaissance there. You have the first opportunity in
        // this battle to take a Battle action or pass." "Yuki's battlefield" is the battlefield Yuki is assigned to
        // (attacking or defending); the Scout condition sits in the constraints block, so it is a requirement to
        // take the action at all (ValidateAbility); "Recon" is a printed action keyword on the ability.
        ["yuki-engage-recon"] = new("Reaction", "Recon Reaction: After engaging at Yuki's battlefield, if all your Personalities there are Scouts: You have Reconnaissance there and the first opportunity in this battle to take a Battle action or pass", "none",
            Trigger: "after-engaging-at-own-battlefield", ActionKeywords: ["Recon"]),
        // Naval Invasion (core rules, Glossary -- Naval): the universal Reaction every player has. "After engaging,
        // once per battle, if you are the Attacker and your current army has more Naval cards than the side opposing
        // it, choose your performing Naval Personality: You have the first opportunity to take a Battle action, which
        // he must perform." Once per BATTLE, not per turn (exempt from the once-per-turn dedupe; the Engage window
        // itself enforces it), and a claim like any other: the Defender's claim overrides it.
        ["naval-invasion"] = new("Reaction", "Naval Invasion: choose your performing Naval Personality: you have the first opportunity to take a Battle action, which he must perform", "none",
            PlayerAbility: true, Performer: "own-naval-personality-at-battle", Trigger: "after-engaging"),
        // Daidoji Ebizo (oracle-l5r-1629, issue #321): "Battle: Ranged Attack with strength equal to Ebizo's Force" -- the
        // strength is his live Force (CardForce, so the trait's +1F this turn counts), read when the Attack is declared, the
        // same self-referential shape legulus-legion-attack already established. (His first printed line is a TRAIT, not an
        // ability: it is registered by his Effect, see MatchEngine.AfterOwnActionTraits.)
        ["ebizo-attack"] = new("Battle", "Ranged Attack with strength equal to Ebizo's Force", "attack"),
        // Training-only: Recon-action classification fixtures (issue #321). "Recon Open"/"Recon Battle" tag the ability itself;
        // training-plain-open is the same effect with no tag, used both as the untagged control and on Strategies whose own
        // Recon KEYWORD makes the action a Recon action (an ability inherits its card's keywords).
        ["training-recon-open"] = new("Open", "Recon Open: no effect (training)", "none", ActionKeywords: ["Recon"]),
        ["training-plain-open"] = new("Open", "Open: no effect (training)", "none"),
        ["training-recon-attack"] = new("Battle", "Recon Battle: Ranged 2 Attack (training)", "attack", ActionKeywords: ["Recon"]),
        // ...and a duel-issuing Recon action, whose resolution is the END of the duel (Dueling.ResolveDuel), not the announcement.
        ["training-recon-duel"] = new("Battle", "Recon Battle: your Personality duels a target enemy Personality (training, no outcome of its own)", "enemy-personality", IssuesDuel: true, ActionKeywords: ["Recon"]),
        // Training-only: Sneak Attack's shape (oracle-l5r-7311, not wired) -- "Engage: The Attacker has the first
        // opportunity to take a Battle action or pass in this battle." The beneficiary is the ATTACKER whoever plays it.
        ["training-engage-attacker-first"] = new("Reaction", "Engage: The Attacker has the first opportunity to take a Battle action or pass in this battle", "none", Trigger: "after-engaging"),
        // Training-only: a plain "After engaging" Reaction with a visible effect and NO claim, for the window's
        // ordering/once-only tests.
        ["training-engage-gain-honor"] = new("Reaction", "After engaging: gain 1 Honor", "none", Trigger: "after-engaging"),
        // A targeted, Political Engage claim: a Reaction is an action, so another player's Reaction can negate it like any other action.
        ["training-engage-claim-political"] = new("Reaction", "Political Engage: target an enemy Personality: the Attacker has the first opportunity to take a Battle action or pass in this battle", "enemy-personality", Trigger: "after-engaging", ActionKeywords: ["Political"]),
        // "Before a battle resolves" (Battle Phase spec 5): a delayed effect a Battle action registers, and the two printed Reactions of the same timing.
        ["training-delayed-bow"] = new("Battle", "Target an enemy Personality: before this battle resolves, bow him", "enemy-personality"),
        ["training-delayed-boost"] = new("Battle", "Target your Personality: before this battle resolves, give him +2F", "own-personality"),
        // Steady Resolve (oracle-l5r-9930): "Reaction: Before a battle resolves, choose your performing opposed Samurai Personality at the current battlefield: Give him +2F."
        ["steady-resolve-boost"] = new("Reaction", "Before a battle resolves, choose your performing opposed Samurai Personality at the current battlefield: give him +2F", "none", Performer: "own-personality-with-keyword-opposed-at-battle", Trigger: "before-battle-resolves", RequiredKeywords: ["Samurai"]),
        // Akodo Dairuko (oracle-l5r-200): "Tireless Reaction: Before the resolution of a battle at her battlefield, that battle's resolution will not bow cards in your army."
        ["dairuko-rally"] = new("Reaction", "Before the resolution of a battle at her battlefield: that battle's resolution will not bow cards in your army", "none", Trigger: "before-battle-resolves"),
        // Before This Battle Resolves (spec step 5; issue #310): a mandatory or optional trait of a card in play at
        // the battlefield, applied once both players have passed and before Force is totalled, ordered by the
        // player whose turn it is. Encircled Terrain (issue #272) is the one real card; its card names this id as
        // its Effect (see MatchEngine.BeforeResolutionAbilityId). Never a player-usable action: the Timing matches
        // no ValidateAbility arm, so a command naming one is rejected.
        ["encircled-before-battle"] = new("BeforeResolution", "Before this battle resolves, the Attacker and Defender, in the order of your choice, each choose one of their own Personalities at its battlefield; move all other Personalities there home", "none", Trigger: "before-battle-resolves"),
        ["training-before-resolution-boost"] = new("BeforeResolution", "Before this battle resolves: give each Personality of yours at this battlefield +2F", "none", Trigger: "before-battle-resolves"),
        ["training-before-resolution-send-home"] = new("BeforeResolution", "Before this battle resolves: move home each Personality at this battlefield with 3 or less Force", "none", Trigger: "before-battle-resolves"),
        ["training-before-resolution-may-bow"] = new("BeforeResolution", "Before this battle resolves: you may bow one enemy Personality at this battlefield", "none", Trigger: "before-battle-resolves"),
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
        ["equip-battle"] = new("Battle", "Equip (Battle): Attach a Spell or a Reserve Fate card to your opposed unbowed Shugenja, paying all costs", "spell-or-reserve-in-hand", PlayerAbility: true, Performer: "own-unbowed-shugenja-opposed-at-battle", Cost: GoldCostRule.TargetPrinted()),
        // Training-only: a Spell's own Battle ability, exercised only through the "equip-battle"
        // follow-up window above -- proves a just-attached Spell can immediately act.
        ["training-spell-force-boost"] = new("Battle", "Target your Personality at the current battlefield: +2 Force", "own-personality"),
        // ---- Alternative costs, attach from a discard pile, conditional effect choice, Follower performers (issue #337; see
        // docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md for every reading and the alternative to each).
        // Kitsu Fukui (oracle-l5r-4375): "Battle: Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it. Gain 1
        // Honor." The performer is the Ancestor (the printed "your performing Ancestor Personality"), not Fukui; it need not be unbowed, because
        // the destroy option does not need it to be -- each option checks for itself (AlternativeCosts.cs). The Honor gain is an action the
        // performer performs, so a dishonorable performer rehonors instead (GainHonorFromPerformer).
        ["fukui-bow"] = new("Battle", "Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it. Gain 1 Honor", "enemy-card",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ancestor"],
            CostOptions: [new("bow", "Bow the Ancestor", CostVerb.Bow), new("destroy", "Destroy the Ancestor", CostVerb.Destroy)]),
        // Moto Jun-Ni (oracle-l5r-5337): "Limited: Target a Follower in any player's discard pile: Attach it to Jun-Ni, paying all costs." (Her
        // "Followers in this unit have Cavalry" is her Effect, a live check in Keyword().)
        ["jun-ni-attach"] = new("Limited", "Target a Follower in any player's discard pile: Attach it to Jun-Ni, paying all costs", "attachment-in-discard",
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.Any, AttachWielder.Source, [CardType.Follower])),
        // Mirumoto Yumaru (oracle-l5r-5181): the conditional effect choice (EffectChoice.cs). Mendon's ruling: with a Weapon the player chooses
        // ONE of three effects; without one there is no choice and both go home. Whether she has a Weapon is read when the action resolves.
        ["yumaru-move-home"] = new("Battle", "Target an enemy Personality: If Yumaru has a Weapon, you may move her home and you may move the Personality home. Otherwise, move both of them home", "enemy-personality",
            Branch: new(BranchCondition.SourceHasWeapon,
                [new("self", "Move Yumaru home", [BranchEffect.MoveSourceHome]),
                 new("target", "Move the targeted Personality home", [BranchEffect.MoveTargetHome]),
                 new("both", "Move both home", [BranchEffect.MoveSourceHome, BranchEffect.MoveTargetHome])],
                new("both", "Move both home", [BranchEffect.MoveSourceHome, BranchEffect.MoveTargetHome]))),
        // Hiruma Tensin (oracle-l5r-3332): his first printed action ("If you have Reconnaissance: Ranged 4 Attack") is exactly the published
        // Hiruma Sniper's hiruma-sniper-attack and is that ability; the second bows a Follower in his unit, which then performs the Attack.
        ["tensin-follower-attack"] = new("Battle", "Bow your performing Follower in Tensin's unit: Ranged 4 Attack", "attack",
            Performer: "own-unbowed-follower-in-source-unit", CostOptions: [CostOption.Bow]),
        // Training fixtures for the three cost modes and the performer wielder (never on a published card): a discount ("paying 1 less Gold", floor 0), free
        // ("ignoring costs") and the Foothills Keep shape.
        ["training-attach-weapon-discount"] = new("Limited", "Training: target a non-Unique Weapon in your discard pile: attach it to this Personality, paying 1 less Gold", "attachment-in-discard",
            Cost: GoldCostRule.AttachCost(-1, 0), Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Source, [CardType.Item], ["Weapon"], ["Unique"])),
        ["training-attach-follower-free"] = new("Limited", "Training: target a Follower in any discard pile: attach it to this Personality, ignoring costs", "attachment-in-discard",
            Attach: new(AttachSource.Target, AttachPile.Any, AttachWielder.Source, [CardType.Follower])),
        ["training-attach-weapon-performer"] = new("BattleOrOpen", "Training: choose your performing Kensai and equip a target Weapon in your discard pile to him", "attachment-in-discard",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Kensai"], Cost: GoldCostRule.AttachCost(),
            Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, [CardType.Item], ["Weapon"])),
        // Training fixture: the two halves of a cost are independent -- WHICH card pays may differ per option ("Bow this card or destroy the target").
        ["training-alt-cost-mixed"] = new("Battle", "Training: bow this card or destroy the target attachment: draw a card", "attachment",
            CostOptions: [new("bow-source", "Bow this card", CostVerb.Bow, CostSubject.Source), new("destroy-target", "Destroy the target attachment", CostVerb.Destroy, CostSubject.Target)]),
        ["training-break-weapon"] = new("Reaction", "Training: before another player's action resolves, destroy the Weapons attached to its source", "none", Trigger: "before-action-resolves"),
        // Fading Shadows (oracle-l5r-2437): "Ninja Battle: If this Follower is in your hand, target your performing Ninja and Equip it to him."
        // The performing Ninja is the wielder (no separate target); the card is attached from hand through the same Equip flow and cost. The printed
        // "Ninja" before "Battle" needs no ActionKeywords tag: the card is itself a Ninja, and an ability inherits the keywords of the card it is on.
        ["fading-shadows-equip"] = new("Battle", "Ninja Battle: If this Follower is in your hand, target your performing Ninja and Equip it to him", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ninja"],
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Self, Wielder: AttachWielder.Performer)),
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
        ["blind-rage"] = new("Battle", "Choose your performing unbowed Berserker: +5F. Lose 2 Honor.", "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Berserker"]),
        ["unorthodox-attack"] = new("Battle", "Choose your performing unbowed Samurai and target an enemy card: Bow it.", "enemy-force", Performer: "own-unbowed-samurai-at-battle"),
        ["chokehold"] = new("Battle", "Choose your performing unbowed Monk and target an enemy Personality with equal or lower Chi: Bow him.", "enemy-personality-chi-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"]),
        // Immovable Object's two printed abilities are independent (separate once-per-turn uses), not
        // a shared choice -- no UsageKey needed. The first is the card the user's own worked example
        // ("Choose your performing Berserker: Straighten him") is drawn from directly.
        ["immovable-object-straighten"] = new("BattleOrOpen", "Choose your performing Berserker: Straighten him.", "own-personality-with-keyword", RequiredKeywords: ["Berserker"]),
        ["immovable-object-bow"] = new("Battle", "Choose your performing unbowed Berserker and target an enemy Personality or Follower with lower Force: Bow it.", "enemy-force-lower-than-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Berserker"]),
        // Follower wave: cards audited against every currently-imported Follower for ones needing
        // nothing beyond already-built categories/helpers (issue #148). Each reuses an existing
        // Target category, Attack strength/kind pattern, or Force/Draw/bow helper verbatim.
        ["akodo-regulars-boost"] = new("Battle", "Discard a card: Give this card a Force bonus equal to the discarded card's Focus Value (until the turn ends)", "hand"),
        // Aberrations (issue #192): same Force-from-Focus shape as Akodo Regulars above, but the
        // discarded card is the forced top card of your own Fate deck (DiscardTopOfFateDeck), not a
        // chosen card from hand -- Target "none" since no Target selection is made at all.
        ["aberrations-boost"] = new("Battle", "Discard the top card of your Fate deck: this Follower gains a Force bonus equal to its Focus Value (until the turn ends)", "none"),
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
        // Sankaku-Yari (issue #192): zero new primitives -- Horseback Warriors above already
        // established a target-keyword-conditional Attack strength.
        ["sankaku-yari-attack"] = new("Battle", "Melee 4 Attack, or Melee 5 Attack if the target is Cavalry", "attack"),
        ["khol-regulars-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["khol-regulars-reaction"] = new("Reaction", "If this card is at the current battlefield, after a Ranged Attack is targeted: +2 strength", "none", Trigger: "before-attack-resolves"),
        ["bounty-hunter-bow"] = new("Battle", "Bow a target attachment or a target dishonorable Personality", "attachment-or-dishonorable-personality"),
        ["rumormonger-penalty"] = new("Open", "Bow this card and target a Personality controlled by a player who controls a dishonorable Personality: Force penalty equal to his base Personal Honor", "personality-controller-controls-dishonorable", ActionKeywords: ["Political"]),
        ["daifuke-swap-force"] = new("Open", "Target another player's Follower or Personality: switch the target's and this Follower's Force", "enemy-force"),
        // Moto Gunso's shape: not a single-target ability at all -- every qualifying Follower gets the
        // bonus at once (see the execution case), reusing province adjacency (AdjacentProvinces).
        ["moto-gunso-boost"] = new("Battle", "Give each of your other Followers at the current battlefield and at each adjacent battlefield +1F", "none"),
        ["elite-sentry-destroy"] = new("Battle", "Destroy a target enemy card without attachments", "enemy-force", RequireNoAttachments: true),
        // Tireless (see the usage-dedupe exemption): straightens the Follower's OWN unit (its wielder
        // and every attachment), not a separately-chosen target -- Target "none", resolved from source.
        ["elite-sentry-straighten"] = new("BattleOrOpen", "Straighten this Follower's unit", "none"),
        ["furugaro-charge"] = new("Limited", "Destroy this Follower to target a Fudo Personality: give him two Charging tokens", "own-personality-with-keyword", RequiredKeywords: ["Fudo"]),
        // Hunter of Harmony (issue #190): "remove this card from the game" as a Limited ability's own
        // cost -- same shape as furugaro-charge's self-destruction above, just RemoveFromGame instead
        // of DestroyUnits. The target, a Kiho Strategy in the player's own Fate discard pile, is put
        // into hand "freshly drawn" (see ReturnToHand) rather than played directly.
        ["hunter-of-harmony-return"] = new("Limited", "Remove this card from the game: choose a Kiho Strategy in your Fate discard pile. Put it into your hand.", "own-kiho-strategy-in-discard"),
        ["panku-madness"] = new("Limited", "Give a target card a Madness token", "any-card"),
        ["soshi-conditional-bow"] = new("BattleOrOpen", "Target a Personality: if it is a Combat Segment, bow him; if he is dishonorable, his controller loses 1 Honor", "personality", ActionKeywords: ["Political"]),
        ["unseen-assassin-strike"] = new("Limited", "Bow this Follower to destroy a target Follower; destroy this Follower and lose 2 Honor", "enemy-follower"),
        ["asahina-straighten"] = new("BattleOrOpen", "Target your bowed Personality: straighten him and gain 1 Honor", "own-bowed"),
        // The real Ashigaru Elite, sharing the exact mechanism the training fixture demonstrated.
        ["ashigaru-elite-boost"] = new("Battle", "Bow an unbowed Farm you control: each Ashigaru Personality/Follower you control at this battlefield gains +1F for the remainder of the battle", "own-unbowed-farm"),
        // Kharmic keyword's own repeatable action (not tied to any specific card): pay 2 Gold, discard
        // a Kharmic card you control. A Dynasty card is replaced with a fresh, immediately face-up
        // Dynasty card (see the execution case); a Fate card's discard is followed by an extra draw.
        ["kharmic-discard"] = new("Limited", "Pay 2 Gold, discard a Kharmic card you control: a Dynasty card is replaced face-up; a Fate card is followed by an extra draw", "own-kharmic-card", PlayerAbility: true, Cost: GoldCostRule.Fixed(2)),
        // Rings (issue #194): each real Ring has two abilities -- its own recruit action (moves it
        // from hand into play, gated by its printed entry condition) and its printed in-play action
        // (also usable directly from hand, discarding the Ring instead of bowing it -- "you may
        // discard this Ring from your hand to use its ability without cost"). Recruitment for the
        // four Reaction-shaped Rings below is granted as an Additional Battle Action (GrantRingRecruit)
        // rather than a forced mid-resolution interrupt -- the user's own simplification.
        ["ring-of-air-recruit"] = new("Battle", "Play after you resolve two or more Favor actions in one turn", "none"),
        ["ring-of-air-straighten"] = new("BattleOrOpen", "Air Repeatable Battle/Open, bow: Straighten your target Personality or attachment", "personality-or-attachment"),
        ["ring-of-water-recruit"] = new("Battle", "Play after a battle resolves in which you played a Terrain, destroyed a Terrain, and destroyed any cards or provinces during resolution", "none"),
        // Simplified from the printed "Absent Repeatable Battle" (issue #194): only the "move home" half
        // of the dual move effect is wired -- the "or move him to the current battlefield if he would be
        // opposed" half is deferred. See docs/ROADMAP.md. Its "Absent" (no-presence-required) half was
        // not modeled until the general Absent flag existed (issue #331); the target must still be at the
        // current battlefield (the shared Battle-timing target rule), only the acting player's own presence
        // is waived.
        ["ring-of-water-move-home"] = new("Battle", "Move your target Personality home", "own-personality", Absent: true),
        // Ring of the Void (issue #194): a Limited ability (not a granted Additional Action) -- see
        // its own Require (two or fewer Rings in play) -- whose cost is discarding the player's
        // entire hand (except the Void ring itself).
        ["ring-of-void-recruit"] = new("Limited", "Play if you have two or fewer Rings in play. Discard your hand", "none"),
        ["ring-of-void-draw"] = new("Limited", "Void Limited, bow: Draw a card. If you now have more cards in your hand than any other player, discard a card", "none"),
        ["false-ring-of-air-recruit"] = new("Battle", "After you win a battle in which there was ever a unit on the opposing side, you may put this Ring into play and lose 2 Honor", "none"),
        // False Ring of Earth (oracle-l5r-10532, issue #295): Dueling (issue #204) and the own-won-duel
        // Reaction trigger (issue #209) cover "after a duel resolves" generally, but this Ring's own
        // condition is a per-PLAYER reaction to any duel their side wins (not a reaction carried by the
        // winning duelist card itself) -- reuses Ring recruitment's existing "grant an Additional Battle
        // Action scoped to this Ring in hand" shape (GrantRingRecruit) instead, called from ResolveDuel
        // once a winner is known (see Dueling.cs), the same simplification every other Ring recruit
        // condition already uses in place of a forced mid-resolution interrupt.
        ["false-ring-of-earth-recruit"] = new("Battle", "After you win a duel against a Personality who shared no keywords with your Personality in the duel, you may put this Ring into play and lose 2 Honor", "none"),
        // "Absent Battle" (issue #331: the Absent flag replaces the earlier simplification to plain Battle).
        ["false-ring-of-air-force"] = new("Battle", "Absent Battle, bow: Give a target Personality -3F", "personality", Absent: true),
        // Not a Reaction/grant at all (issue #194) -- a standing condition, usable any time it holds,
        // like an ordinary Battle ability. (Its printed text is a plain "Battle:", so it takes no Absent flag.)
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
        // Ring of Earth and Ring of Fire (issue #194, revalidated once their own scans became
        // available -- their earlier blocker notes were themselves based on mismatched/incomplete
        // scraped text, the same class of problem issue #195 flagged for Ring of Fire specifically).
        // "You won" (Earth) is modeled the same way False Ring of Air's own win requirement already
        // is: GrantRingRecruit only in the attackerWon/df>af branches of ResolveBattle, never the tie
        // branch -- "none of your Personalities were destroyed" holds by construction on the winning
        // side of an outright win (only DestroyUnits(the LOSING army) runs in those two branches; a
        // tie is the only resolution that ever destroys the winning side's own units too).
        ["ring-of-earth-recruit"] = new("Battle", "After the resolution of a battle you won, if there were ever any enemy units at that battlefield and none of your Personalities were destroyed during the battle, you may put this Ring into play from your hand", "none"),
        // "Even if you control no units at the current battlefield" waives the ordinary Battle-timing
        // presence check -- exactly rule 9's own gloss of Absent ("the action may be taken even without
        // controlling any unit at the current battlefield"), so it carries the Absent flag (issue #331; it
        // was an id-list entry beside Mirumoto Michi/Togashi Rikyou before, issue #207). A Ring is never a
        // Personality, so the flag's second half (no source-AtBattle Require) never applied to it either:
        // migrating changed nothing. "bow this card or discard it from your hand" is not a new cost
        // shape at all: EVERY Ring ability may already be used either from hand (discarded afterward,
        // ExecuteAbilityEffects' own tail) or in play (bowed here) -- see ValidateAbility's own Ring case. "+3 or -3" is the
        // player's own choice, the same dual-UsageKey shape as horibe-bow/horibe-straighten, applied
        // to a new "any-province" Target (an own-or-enemy generalization of the existing
        // "enemy-province" category, since this ability may target either side's own provinces).
        ["ring-of-earth-raise"] = new("Battle", "Even if you control no units at the current battlefield, bow this card or discard it from your hand: Give a province +3 strength", "any-province", UsageKey: "ring-of-earth-strength", Absent: true),
        ["ring-of-earth-lower"] = new("Battle", "Even if you control no units at the current battlefield, bow this card or discard it from your hand: Give a province -3 strength", "any-province", UsageKey: "ring-of-earth-strength", Absent: true),
        // Ring of Fire's own recruitment condition reuses the exact same "destroyed one or more
        // provinces or enemy cards in this resolution" fact Ring of Water's own CheckRingOfWaterRecruit
        // already computes at each ResolveBattle call site -- no "win" requirement, same as Ring of
        // Water, unlike Ring of Earth/False Ring of Air above.
        ["ring-of-fire-recruit"] = new("Battle", "After the resolution of a battle in which you destroyed one or more provinces or enemy cards, you may put this Ring into play from your hand", "none"),
        // "Destroy your performing Personality" needs no new Performer category -- "own-personality"
        // (no AtBattle/unbowed qualifier, same reasoning as Steel on Steel's own performer) already
        // matches the printed text exactly. "enemy-personality-chi-lte-performer" (Chokehold's own
        // Target, comparing against the PERFORMER's Chi, not the source's -- unlike
        // personality-chi-lte-self, which would wrongly compare against the Ring's own nonexistent
        // Chi) already exists too. DestroyUnits (already fully generic -- MarkDead, attachments,
        // Honor bonuses -- used identically whether the destroyed card is a battle-army member or
        // not) handles both the performer and the target; no new destroy primitive needed at all.
        ["ring-of-fire-destroy"] = new("Battle", "Bow this card or discard it from your hand, destroy your performing Personality, and target an enemy Personality with equal or lower Chi: Destroy him", "enemy-personality-chi-lte-performer", Performer: "own-personality"),
        // Mirumoto Rokai / Tamori Tamoko (issue #207 follow-up): "before an action resolves, negate
        // one of its own named effects" -- destruction (Rokai) or movement/straightening (Tamoko).
        // Target "none" for both: the affected card is auto-determined from the deferred command
        // itself (MatchState.PendingNegatableAbility), not separately chosen by the reacting player
        // -- see BeforeActionReactionApplies/ResolveOrDeferBeforeActionReaction.
        ["rokai-negate-destruction"] = new("Reaction", "After another player's action targets your Personality at Rokai's location, destroy Rokai: Negate your Personality's destruction from the action's effects.", "none", Trigger: "before-action-resolves"),
        ["tamoko-negate"] = new("Reaction", "Before an action resolves, target a Personality: Negate either his movement or straightening from the action's effects.", "none", Trigger: "before-action-resolves"),
        ["tamoko-open"] = new("Open", "If it is not your turn, target an unbowed Personality controlled by the active player: Before the turn ends, gain 1 Honor if he was not assigned to attack your provinces this turn.", "unbowed-personality-controlled-by-active-player"),
        // Champion of Thunder / Brothers of Goemon: the general in-play unit-targeting Reaction
        // primitive, reusing the same "before-action-resolves" Trigger/"beforeActionReaction" window
        // Rokai/Tamoko already opened -- see BeforeActionReactionApplies's own Champion/Brothers
        // branch for how each is actually found. Champion's own Target ("own-unit-member") lets its
        // controller choose ANY card in the unit to negate on (not auto-determined like Rokai/Tamoko),
        // matching the card owner's own ruling that "this card" means "any card in this unit" and
        // that the controller chooses when more than one card was actually affected at once.
        // Brothers' own Target stays "none" -- it never offers a choice of card, only whether to use
        // it at all (auto-determined from the deferred command's own TargetId, Rokai/Tamoko's shape).
        ["champion-of-thunder-negate"] = new("Reaction", "After another player's action targets a card in this unit: Negate the action's effects on this card.", "own-unit-member", Trigger: "before-action-resolves"),
        ["champion-of-thunder-attack"] = new("Battle", "Ranged 6 Attack", "attack"),
        ["brothers-goemon-negate"] = new("Reaction", "After another player's action targets a card in this unit, if you control any Rings: Negate the card's bowing from the action's effects.", "none", Trigger: "before-action-resolves"),
        // Akodo Kakihara (oracle-l5r-223, issue #305): "Reaction: After another player's action targets
        // Kakihara: Give him a Force bonus equal to his Personal Honor." One printed ability, two
        // catalog entries because an ability carries exactly one Trigger and this card must react in
        // two different windows: a non-Attack action goes through the general in-play targeting
        // primitive's "beforeActionReaction" window (BeforeActionReactionApplies's own Kakihara
        // branch), while a Ranged/Melee Attack has its own separately-orderable pipeline and is
        // reacted to in the existing "attackReaction" window (ResolveOrDeferAttack), Khol Regulars'
        // shape. Both share UsageKey "kakihara-force-bonus" so the one printed ability is used at most
        // once per turn across the two windows. Target "none" for both: the affected card is Kakihara
        // himself, fixed by the trigger, never a player choice.
        ["kakihara-force-bonus"] = new("Reaction", "After another player's action targets Kakihara: Give him a Force bonus equal to his Personal Honor.", "none", Trigger: "before-action-resolves", UsageKey: "kakihara-force-bonus"),
        ["training-before-action-suppress"] = new("Reaction", "Training: after another player's action targets a card of yours, bow this card and every other unbowed card of yours with a before-action reaction.", "none", Trigger: "before-action-resolves"),
        ["kakihara-attack-force-bonus"] = new("Reaction", "After another player's Ranged or Melee Attack targets Kakihara: Give him a Force bonus equal to his Personal Honor.", "none", Trigger: "before-attack-resolves", UsageKey: "kakihara-force-bonus"),
        // Lion Clan Personality review (issue #198): every ability below reuses only primitives
        // already built through issue #194. "Political"/"Tactical" as a printed-Timing prefix map
        // onto the existing Open/Battle/Limited/BattleOrOpen values verbatim (confirmed precedent,
        // issue #174's own audit of Rumormonger/Soshi House Guard's "Political Open"/"Political
        // Battle/Open" -- the SAME reasoning applies to "Tactical Open"/"Tactical Limited" here).
        ["dairuko-exp-bow"] = new("Battle", "Bow a target enemy Personality with lower Personal Honor. Destroy him if he is Unaligned.", "enemy-personality-lower-honor-than-self"),
        ["furu-bow"] = new("Battle", "Bow a target Personality with equal or lower Chi", "personality-chi-lte-self", UsageKey: "furu-bow-straighten"),
        ["furu-straighten"] = new("Battle", "Straighten a target Personality with equal or lower Chi", "personality-chi-lte-self", UsageKey: "furu-bow-straighten"),
        ["kamina-exp-attack"] = new("Battle", "Ranged 3 Attack, or Ranged 4 Attack if the target is Lion Clan or Unaligned", "attack"),
        ["kano-token"] = new("Limited", "Give Kano a Master token", "none"),
        ["kano-destroy-token"] = new("Battle", "Bow: Destroy a Master token on Kano to take two additional Battle actions", "none"),
        ["michitsu-destroy"] = new("Battle", "Destroy a target enemy attachment", "enemy-attachment"),
        // Simplified from the printed "You may discard a card to ignore the cost of bowing Kamina"
        // (issue #198) -- the discard-instead-of-bow cost alternative is not modeled this pass.
        ["kamina-boost"] = new("Open", "Bow: Give a target Personality +4F", "personality"),
        // "Tireless" (issue #198, Akodo Tsudoken's own reminder): usable even while bowed -- see the
        // bowed-exemption list below. The self-straighten happens regardless of which target choice
        // is taken, matching the printed "Even if bowed: Straighten him" applying to both halves.
        ["tsudoken-exp-bow"] = new("Battle", "Even if bowed: Straighten Tsudoken. Bow a target Personality.", "personality", UsageKey: "tsudoken-exp-bow-straighten"),
        ["tsudoken-exp-straighten"] = new("Battle", "Even if bowed: Straighten Tsudoken. Straighten a target Personality.", "personality", UsageKey: "tsudoken-exp-bow-straighten"),
        ["tsudoken-exp2-entry"] = new("Reaction", "After Tsudoken enters play, lose 3 Honor", "none", Trigger: "own-enters-play"),
        // Simplified from "Destroy Nojima if he is ever in an army with a Personality with 3+ PH"
        // (issue #198) -- a continuous army-composition check with no existing analogue (every
        // existing check is event-triggered, not continuously re-evaluated); not modeled this pass.
        ["tsudoken-exp2-straighten"] = new("BattleOrOpen", "Tireless: Straighten your target Lion Clan or Fallen Personality", "own-lion-or-fallen-personality"),
        ["nojima-entry"] = new("Reaction", "After Nojima enters play, lose 1 Honor", "none", Trigger: "own-enters-play"),
        ["aimi-exp-discard"] = new("Limited", "Bow: Discard a card. Gain Honor equal to its Focus Value, plus 1 if Shiba Iaimiko is in play.", "hand", ActionKeywords: ["Political"]),
        ["ayumi-entry"] = new("Reaction", "After Ayumi enters play, lose 2 Honor", "none", Trigger: "own-enters-play"),
        ["masumi-bow"] = new("Battle", "Target an enemy card without attachments: Bow it", "enemy-card-without-attachments"),
        // Simplified from "You may discard a Terrain from your hand or destroy a Terrain in play; if
        // you did either, draw a card" (issue #198) -- the optional Terrain-discard-for-a-draw half
        // is not modeled this pass; the printed strength/conditional-bonus half is unaffected.
        ["shuji-attack"] = new("Battle", "Ranged 4 Attack, with +1 strength if it targeted a Spider Clan Personality", "attack"),
        // Simplified from "a card in a unit" (issue #198) to an enemy unit specifically, matching the
        // scoping convention every other bow/straighten-a-unit-card ability in this catalog already
        // uses (own-unit-card is the one existing own-side exception, and it names no conditional
        // attachment waiver).
        ["tsukasa-bow"] = new("Battle", "Target an enemy card in a unit (without attachments, unless you control a Terrain): Bow it", "enemy-unit-card-terrain-conditional", UsageKey: "tsukasa-bow-straighten"),
        ["tsukasa-straighten"] = new("Battle", "Target an enemy card in a unit (without attachments, unless you control a Terrain): Straighten it", "enemy-unit-card-terrain-conditional", UsageKey: "tsukasa-bow-straighten"),
        ["arata-force-penalty"] = new("Battle", "Target an enemy Personality or Follower: Force penalty equal to Arata's Personal Honor. Gain 1 Honor.", "enemy-force"),
        // Training-only: exercises Discipline (issue #184) -- played from a discard pile, paying the
        // card's own printed Gold cost, then removed from the game. Since issue #331 Discipline is a FLAG on
        // the ability (AbilityDefinition.Discipline), not a Timing: the zone check, the cost and the post-
        // resolution removal all key off that flag plus where the source card is. The timing is the printed
        // verb the fixture stands in for ("Battle/Open" -- the widest phase window, exactly the one the old
        // Discipline timing had).
        ["training-discipline-ability"] = new("BattleOrOpen", "Discipline: pay this card's Gold cost, gain 1 Honor, then remove it from the game", "none", Discipline: true),
        // Training-only: exercises Stalwart (issue #184) -- a per-instance (not per-player) enemy-bow
        // ability, so two separate source instances can each bow the same Stalwart target once this
        // turn, isolating the keyword's own negation from the ordinary once-per-turn dedupe.
        ["training-bow-enemy"] = new("Open", "Training: bow a target enemy Personality", "enemy-personality"),
        // Togashi Hizumi / Tamori Ruya (issue #216): a Battle-timing bow, needed to exercise
        // OpenBowReactionWindow at an actual battlefield (every existing bow training fixture is
        // Open-timing, usable only during the Action phase, never while AtBattle is true).
        ["training-bow-at-battle"] = new("Battle", "Training: bow a target enemy Personality at the current battlefield", "enemy-personality"),
        // A second, independent source (own AbilityUses dedupe scope) so a test can straighten the
        // SAME target twice in one turn -- proving Straighten() clears a consumed "next
        // straightening" block rather than blocking every attempt for the rest of the turn.
        ["training-straighten-at-battle"] = new("Battle", "Training: straighten a target Personality at the current battlefield", "personality"),
        // Training-only: exercises Stalwart's "never the card's own controller" exclusion -- "personality"
        // allows either side, unlike training-bow-enemy above.
        ["training-bow-any"] = new("Open", "Training: bow a target Personality, own or enemy", "personality"),
        // Singh Remnants' own Reaction: unlike "own-enters-play" (the entering card's own ability),
        // this fires when a DIFFERENT card the player controls (a Personality) enters play, letting
        // Singh Remnants -- sitting in hand or a discard pile -- attach itself. Target "none": the
        // Personality is implicit (the entryReaction window's own CardId), not separately chosen.
        ["singh-remnants-attach"] = new("Reaction", "After your Personality enters play, if this card is in your hand or discard pile: attach it to him, paying all costs. Gain 1 Honor.", "none", Trigger: "any-personality-enters-play", Cost: GoldCostRule.SourcePrinted(), ActionKeywords: ["Political"]),
        ["training-personality-entry-watch"] = new("Reaction", "Training: after a Personality enters play, attach this card to it, gain 1 Honor", "none", Trigger: "any-personality-enters-play", Cost: GoldCostRule.SourcePrinted()),
        // Clay Kshatriya's Invest: reuses the same own-enters-play Reaction/entryReaction window every
        // other "after entering play" ability already shares -- genuinely optional (the player may
        // decline), unlike a mandatory entry effect, so no new window shape is needed.
        ["clay-kshatriya-invest"] = new("Reaction", "Invest 5 Gold: give this Follower two +1F tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(5)),
        ["training-invest"] = new("Reaction", "Training: Invest 3 Gold: give this Follower a +1F token", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(3)),
        // Generalized Gold payment (issue #303): synthetic fixtures, one per cost shape, standing in for
        // six real cards that are NOT wired yet (Exchange of Letters, Urgency, Virtuous Charity, At Any
        // Cost, A Stain Cleansed, Disgraceful Conduct -- see docs/decisions/2026-09-18-generalized-gold-
        // payment.md). Each proves that the shape prices, validates, quotes and pays through the ONE
        // GoldCostRule path, so wiring the real card is just a catalog line plus its own effect.
        ["training-urgency-draw"] = new("Limited", "Training (Urgency shape): Pay 2 Gold: draw a card", "none", Cost: GoldCostRule.Fixed(2)),
        ["training-charity-honor"] = new("Open", "Training (Virtuous Charity shape): Pay zero or more Gold: gain that much Honor", "none", Cost: GoldCostRule.Variable()),
        ["training-charity-honor-capped"] = new("Open", "Training: Pay 1 to 4 Gold: gain that much Honor", "none", Cost: GoldCostRule.Variable(1, 4)),
        ["training-disgrace-bow"] = new("Open", "Training (Disgraceful Conduct shape): Pay the target Personality or unit's total Gold cost minus 4 (minimum 0): bow him", "personality", Cost: GoldCostRule.TargetUnitTotal(-4, floor: 0)),
        ["training-stain-straighten"] = new("Open", "Training (A Stain Cleansed shape): Pay the target unit's total Gold cost plus 2: straighten him", "personality", Cost: GoldCostRule.TargetUnitTotal(2)),
        ["training-at-any-cost"] = new("Open", "Training (At Any Cost shape): Pay a dead Personality's own Gold cost: gain 1 Honor", "dead-personality-any-discard", Cost: GoldCostRule.TargetPrinted()),
        ["thunderous-legion-destroy"] = new("Battle", "Destroy a target enemy card without attachments", "enemy-force", RequireNoAttachments: true),
        ["brothers-great-lake-bow"] = new("Battle", "Bow a target enemy card", "enemy-force"),
        // Dueling (issue #204): "Your performer duels the target" (unrefusable -- no printed language
        // ever describes refusing it, unlike a card that says "issues an unrefusable duel" as a
        // stronger and equivalent statement). No duel stat is named, so it defaults to Chi. IssuesDuel
        // routes this through IssueChallenge instead of an ordinary execution case; ApplyDuelOutcome
        // is invoked with this same id, once the duel actually resolves, to destroy the loser(s).
        ["steel-on-steel-duel"] = new("Battle", "Choose your performing unbowed Personality and target an enemy Personality: Your performer duels the target. Destroy the duel's loser.", "enemy-personality", Performer: "own-unbowed-personality", IssuesDuel: true),
        // The Courtesy of the Crane (issue #204): only its own "As a Focus Effect" half is wired this
        // pass -- the printed Political Open half ("Choose another player. He may draw a card. Draw
        // two cards.") needs a new "choose a player" targeting primitive that doesn't exist yet, and
        // is a separate, later concern from Dueling itself. A FocusEffect-Timing ability is never
        // "played" -- ValidateAbility's own Timing switch never legalizes it through UseAbility at
        // all; it only ever runs through ResolveFocusEffect, in the active player's chosen order.
        ["courtesy-of-the-crane-focus"] = new("FocusEffect", "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2 strength.", "none"),
        // Training-only: a refusable challenge with no printed win/loss text of its own -- see
        // ApplyDuelOutcome's own generic -1F case, used only so a test can observe the loser(s)
        // without needing a real card's own outcome.
        ["training-duel-challenge"] = new("Battle", "Training: choose your performing unbowed Personality and target an enemy Personality: duel (refusable)", "enemy-personality", Performer: "own-unbowed-personality", IssuesDuel: true, DuelRefusable: true),
        // Dragon Clan review (issue #207): every real Dragon Clan Personality wired this pass, using
        // only already-existing primitives plus the small additions documented alongside them (the
        // self-performer duel extension, StraightenUnit, MoveToBattlefield, and a handful of new
        // Target categories -- see their own comments above).
        // Kitsuki Daisuke: "Daisuke duels him" -- no separate Performer, matching every other real
        // Iaijutsu Personality below. Simplified from its own printed text: "if he is dishonorable,
        // this movement will not be negated" is dropped -- nothing in the engine negates movement
        // yet, so the clause is currently unobservable regardless.
        ["daisuke-duel"] = new("Battle", "Iaijutsu: Target an enemy Personality: duel him. Move the duel's loser home.", "enemy-personality", IssuesDuel: true),
        ["fujimura-expose"] = new("Limited", "Bow Fujimura and target a Dishonorable Personality: his controller loses 2 Honor", "dishonorable-personality"),
        ["kinaro-exp-bow"] = new("Open", "Iaijutsu, bow: Bow a target Personality with lower Personal Honor; gain 1 Honor if you control Kakita Ichigiku", "personality-lower-honor-than-self"),
        ["nakai-exp-weaken"] = new("Open", "Give a target Personality -2F, or -3F if he is dishonorable", "personality"),
        ["nubane-bow"] = new("Open", "Target an attachment: bow it", "attachment"),
        ["hatsuto-weaken"] = new("Battle", "Give a target enemy Infantry Personality or Follower -3F", "enemy-force-non-cavalry"),
        ["houken-straighten"] = new("BattleOrOpen", "Target a Personality with a Weapon: straighten his unit", "personality-with-weapon"),
        // Mirumoto Houken, Experienced: "you may move him home, or, if he would be opposed, to the
        // current battlefield" is deterministic, not a real choice -- moving to the battlefield only
        // ever makes sense when opposed, so home is the only real fallback the rest of the time.
        // BattleOrOpen carries none of the shared Battle-timing presence checks (matching every other
        // BattleOrOpen ability already in the catalog), so no new exception is needed here.
        ["houken-exp-straighten"] = new("BattleOrOpen", "Even if bowed: straighten Houken's unit. Move him home, or to the current battlefield if he would be opposed there.", "none"),
        ["ichizo-exp-duel"] = new("Battle", "Iaijutsu: Target an enemy Personality: duel him. Destroy the duel's loser.", "enemy-personality", IssuesDuel: true),
        // Mirumoto Kojinrue: simplified cost -- always bows Kojinrue himself rather than offering a
        // choice between him and one of his Weapons, the same "drop the choice among several eligible
        // own cards" simplification used a few more times below (no generic cost-choice mechanism
        // exists yet for "bow one of N eligible cards you control").
        ["kojinrue-destroy"] = new("Battle", "Bow Kojinrue and target an enemy card without attachments: destroy it", "enemy-card-without-attachments"),
        // Xijkt (issue #207 follow-up): the FULL "choose which of several eligible own cards to bow
        // as a cost" shape (own-source-or-unbowed-holding-with-keyword), not Kojinrue's own
        // single-card simplification above.
        ["xijkt-bow"] = new("Battle", "Bow Xijkt or one of your Mines, and target an enemy card without attachments: Bow it.", "enemy-card-without-attachments", Performer: "own-source-or-unbowed-holding-with-keyword", RequiredKeywords: ["Mine"], CostOptions: [CostOption.Bow]),
        // Tamori Shosei (issue #207 follow-up): same shape as Xijkt above, but ALSO gated on
        // controlling a Temple at all (regardless of its own bowed state -- checked separately, see
        // the id=="shosei-honor" Require) before the bow-cost choice is even offered.
        ["shosei-honor"] = new("Limited", "If you control a Temple, bow Shosei or one of your Temple Holdings: Shosei prays to the Fortunes. Gain 1 Honor.", "none", Performer: "own-source-or-unbowed-holding-with-keyword", RequiredKeywords: ["Temple"], CostOptions: [CostOption.Bow]),
        // Togashi Mitsu (issue #207 follow-up): "Fire Battle" is the same plain printed-keyword
        // classification moshi-ikako-attack already established (not a Ring/keyword gate) -- his own
        // fill-up-to-five token grant is BattleOrOpen like Immovable Object, dual-timing but otherwise
        // an ordinary "none"-target ability; his Attack's own strength is a per-command choice (see
        // ResolveOrDeferAttack's strengthOverride), not a value AttackStrength's static switch holds.
        ["mitsu-fire-tokens"] = new("BattleOrOpen", "Fire Battle/Open: Give Mitsu +1F Fire tokens until he has exactly five.", "none"),
        ["mitsu-fire-attack"] = new("Battle", "Fire Battle: Destroy zero or more Fire tokens on Mitsu. Ranged Attack with strength equal to the number of tokens destroyed.", "attack"),
        // Togashi Sakata (issue #207 follow-up): "Limited: give Sakata a +1F Fire or Sake token" is
        // the UsageKey-shared choice-of-two-effects shape (yayu-bow-attack/yayu-destroy-attack,
        // horibe-bow/horibe-straighten, ...), not a new cost-choice primitive. His own Attack strength
        // (4 + Sake token count) is already wired into AttackStrength/AttackKind above.
        ["sakata-fire-token"] = new("Limited", "Give Sakata a +1F Fire token", "none", UsageKey: "sakata-token"),
        ["sakata-sake-token"] = new("Limited", "Give Sakata a +1F Sake token", "none", UsageKey: "sakata-token"),
        ["sakata-attack"] = new("Battle", "Sakata breathes fire. Ranged 4 Attack, with +1 strength for each Sake token on Sakata.", "attack"),
        ["kouzei-bow"] = new("Battle", "Target an enemy card with equal or lower Force: bow it", "enemy-card-force-lte-self"),
        // Mirumoto Michi / Togashi Rikyou below: "even if bowed" plus reinforcing a battle from
        // somewhere else entirely -- neither the acting player's own presence at the battlefield nor
        // this card's own AtBattle status can be required the normal way, since the whole point is
        // moving in from outside (see the two id-based exceptions this needs in ValidateAbility).
        ["michi-move"] = new("Battle", "Even if bowed: if opposed, move Michi to the current battlefield and straighten his unit", "none"),
        ["mitoshi-bow"] = new("Battle", "Target a Personality with lower Force than Mitoshi: bow him", "personality-force-lt-self", UsageKey: "mitoshi-bow-straighten"),
        ["mitoshi-straighten"] = new("Battle", "Target a Personality with lower Force than Mitoshi: straighten him", "personality-force-lt-self", UsageKey: "mitoshi-bow-straighten"),
        ["shikei-draw"] = new("Limited", "Shikei meditates: draw a card", "none"),
        ["shikei-duel"] = new("Battle", "Iaijutsu: Target an enemy Personality: duel him. Destroy the duel's loser.", "enemy-personality", IssuesDuel: true),
        // Mirumoto Yozo, Experienced: reuses the exact existing enemy-card-without-attachments
        // category verbatim (a Follower, or a Personality without attachments).
        ["yozo-exp-destroy"] = new("Battle", "Iaijutsu: Target an enemy Follower or Personality without attachments: destroy it (remove it from the game instead if it is a Lion Clan Personality)", "enemy-card-without-attachments"),
        ["jinai-bow"] = new("Battle", "Earth: Target an enemy card without attachments: bow it", "enemy-card-without-attachments"),
        // Tamori Muzu: the bow is conditional (attacking OR without attachments), not a targeting
        // restriction -- Target stays the bare enemy-card category so a card WITH attachments is
        // still a legal target, just not necessarily bowed by the effect.
        ["muzu-bow"] = new("Battle", "Target an enemy card: bow it if it is attacking or has no attachments; gain 1 Honor", "enemy-card"),
        // Togashi Hiroto: here the lack of attachments genuinely IS the targeting restriction (an
        // attachment itself always qualifies, having none of its own), unlike Muzu just above.
        ["hiroto-fire-tokens"] = new("Reaction", "After entering play, gain four +1F Fire tokens", "none", Trigger: "own-enters-play"),
        ["hiroto-bow"] = new("Battle", "Target an enemy card without attachments: bow it (destroy it instead if it is an attachment)", "enemy-card", RequireNoAttachments: true),
        // Togashi Kasuru: simplified -- the optional "you may discard a card to straighten Kasuru"
        // sub-choice is dropped (no generic "optional secondary cost" primitive exists yet); the core
        // "if attacking, draw a card" half is preserved.
        ["kasuru-draw"] = new("Battle", "Bow Kasuru: if he is attacking, draw a card", "none"),
        ["mihato-move"] = new("Battle", "Target an enemy Personality: move him home", "enemy-personality"),
        ["noboru-exp2-favor"] = new("Limited", "Political: take the Imperial Favor", "none", ActionKeywords: ["Political"]),
        ["noboru-shattered-star-weaken"] = new("Battle", "Fire: Bow a target enemy Personality; give Noboru a Force bonus equal to the target's Force", "enemy-personality"),
        ["noritada-invest"] = new("Reaction", "Invest 3 Gold: give this Personality two +1F Fire tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(3)),
        ["obote-boost"] = new("Limited", "Void: discard a card to give Obote a Force bonus equal to its Focus Value", "hand"),
        ["rikyou-move"] = new("Battle", "Even if bowed: if any enemy units are at the current battlefield, move Rikyou there and straighten his unit", "none"),
        // Togashi Korimi, Experienced: the second ability's own optional "you may discard a Kiho or
        // Tattoo Strategy; if you did, destroy the target" bonus is dropped, the same "optional
        // secondary cost" simplification as Kasuru above -- the bow itself is preserved.
        ["korimi-exp-bow"] = new("Battle", "Target an enemy card without unbowed attachments: bow it", "enemy-card-without-unbowed-attachments"),
        ["korimi-exp2-draw"] = new("Limited", "Draw a card", "none"),
        // "After a duel resolves" (issue #209): own-won-duel, scoped to the one specific card that
        // won. Kitsuki Daisuke, Experienced: simplified from "After AN ACTION RESOLVES during which
        // Daisuke won a duel" -- a duel is itself a complete action, and winning one is only ever
        // determined during its own resolution, so the two phrasings are the same event.
        ["daisuke-exp-draw"] = new("Reaction", "Iaijutsu: after this duel resolved, you may draw a card", "none", Trigger: "own-won-duel"),
        // Kitsuki Horume's own FIRST ability (issue #210): "Choose a player. He gains or loses 1
        // Honor" -- a dual-choice pair (same UsageKey shape as paid-bow/paid-straighten) rather than
        // a new optional-Choice command field, sharing one once-per-entry use. Horume refers to
        // himself only implicitly (his own name never actually appears in his own printed text, but
        // this is still own-enters-play -- he is the one entering).
        ["horume-gain"] = new("Reaction", "After Horume enters play: choose a player, he gains 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "horume-gain-lose"),
        ["horume-lose"] = new("Reaction", "After Horume enters play: choose a player, he loses 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "horume-gain-lose"),
        ["training-choose-player-gain"] = new("Reaction", "Training: after entering play, choose a player, he gains 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "training-choose-player"),
        ["training-choose-player-lose"] = new("Reaction", "Training: after entering play, choose a player, he loses 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "training-choose-player"),
        // Kitsuki Horume's own second ability: "He boasts of his talents" is non-functional flavor
        // text folded into the reaction, confirmed with the user and dropped.
        ["horume-honor"] = new("Reaction", "After this duel resolved, you may gain 1 Honor", "none", Trigger: "own-won-duel"),
        ["jakuei-favor"] = new("Reaction", "Political: after this duel resolved, you may take the Imperial Favor", "none", Trigger: "own-won-duel", ActionKeywords: ["Political"]),
        // Kitsuki Jakuei's own separate Political Battle ability: a dual-choice pair sharing one
        // UsageKey, the same shape as paid-bow/paid-straighten -- "you may discard the Imperial
        // Favor" becomes "which of the two abilities did you use" rather than a new optional-cost
        // command field. Unlike the rulebook favor-battle Player ability (which moves an enemy
        // ATTACKER home without bowing him), Jakuei's own bow-first-then-move-home version sends the
        // target home BOWED -- the entire reason discarding the Favor here is worth it.
        ["jakuei-bow"] = new("Battle", "Political: bow a target enemy Personality", "enemy-personality", UsageKey: "jakuei-bow-favor", ActionKeywords: ["Political"]),
        ["jakuei-bow-favor"] = new("Battle", "Political: discard the Imperial Favor · bow a target enemy Personality and move him home bowed", "enemy-personality", UsageKey: "jakuei-bow-favor", ActionKeywords: ["Political"]),
        // Tamori Takeshi (issue #212): "Takeshi helps him meditate" is non-functional flavor text
        // folded into the Open action, confirmed dropped -- the real effect is Protection, granted
        // to the target for the rest of the active player's own turn (the default duration, since
        // no "for the rest of this battle"/"permanently" is printed).
        ["takeshi-protect"] = new("Open", "Target your Personality: effects of actions the target performs will not be negated until the end of the turn", "own-personality"),
        // Togashi Noboru (issue #216): "Noboru pushes him to a new path" is non-functional flavor
        // text folded into the Battle action. No separate Performer selection -- Noboru duels/acts as
        // himself, the established self-performer convention (Performer left null).
        ["noboru-redirect"] = new("Battle", "Target an enemy Personality with lower Chi: redirect his own Force to your army for the duration of the battle", "enemy-personality-chi-lt-self"),
        // Togashi Hizumi (issue #216): "target him" is the Personality this action just bowed at
        // Hizumi's own battlefield -- see OpenBowReactionWindow/bow-reaction-candidate.
        ["hizumi-negate-straighten"] = new("Reaction", "After the resolution of an action that bowed a Personality at Hizumi's battlefield, target him: negate his next straightening this turn", "bow-reaction-candidate", Trigger: "bowed-personality-at-own-battlefield"),
        // Tamori Ruya (issue #216): "your action" -- actor-scoped, no location restriction on the bow
        // itself; her own target is unrelated to which Personality was actually bowed.
        ["ruya-bow"] = new("Reaction", "After the resolution of your action that bowed a Personality, target a Personality at the same location: bow him", "personality-at-source-location", Trigger: "own-action-bowed-personality"),
        // Tamori Yayu (issue #216): "He lobs an alchemical potion at the enemy" is non-functional
        // flavor text folded into the Battle action. The player's own choice of which cost to pay
        // (bow vs. destroy the chosen Spell) is the established dual-catalog UsageKey pair, the same
        // shape as jakuei-bow/jakuei-bow-favor -- not a new optional-cost command field.
        ["yayu-bow-attack"] = new("Battle", "Bow one of Yayu's Spells · Ranged 5 Attack", "attack", Performer: "own-spell-attached-to-source", UsageKey: "yayu-attack"),
        ["yayu-destroy-attack"] = new("Battle", "Destroy one of Yayu's Spells · Ranged 5 Attack", "attack", Performer: "own-spell-attached-to-source", UsageKey: "yayu-attack"),
        // Bayushi Mugoshi (issue #223): "Political Reaction" -- the Timing-prefix word itself is
        // never a blocker (issue #198's own confirmed precedent), so this is a plain Reaction. The
        // same "choose a player" shape Kitsuki Horume's own first ability already uses (issue #210);
        // "for not accepting the teachings of Fudo" is flavor folded into the choice, not a separate
        // condition.
        ["mugoshi-slander"] = new("Reaction", "Political: after Mugoshi enters play, choose a player: he loses 1 Honor", "player", Trigger: "own-enters-play", ActionKeywords: ["Political"]),
        // Low Market (the first Region, Embers of War): "Open: Bow this card, choose your performing
        // Personality and target an attachment in your hand: Attach it to him, paying 2 less Gold, to
        // a minimum of 1." Not PlayerAbility -- it has a real source card, unlike Equip (Battle) --
        // and not the ordinary Equip() command (which is hardcoded Limited-on-your-turn and has no
        // per-card discount hook), so it's its own Open ability with its own Target category
        // ("attachment-in-hand", broader than Equip (Battle)'s own "spell-or-reserve-in-hand": any
        // Item/Follower/Spell, mirroring the ordinary Equip() command's own type filter). Bowing
        // itself is the entire cost/limiter -- the shared "abilities on bowed cards cannot be used"
        // rule already makes this usable at most once between straightens, no separate UsageKey
        // needed, same convention as e.g. "hired-legion-draw".
        ["low-market-equip"] = new("Open", "Open: Bow this card, choose your performing Personality and target an attachment in your hand: Attach it to him, paying 2 less Gold, to a minimum of 1", "attachment-in-hand", Performer: "own-personality", Cost: GoldCostRule.TargetPrinted(-2, floor: 1)),
        // Fallow Lands (the first Region with a static trait, The Shadow's Embrace): "This province
        // has +2 strength" (EffectiveProvinceStrength, live off ProvinceState.RegionId -- applies
        // immediately on attach, without respect to presence, per its own printed wording) plus
        // "Reaction: After a Melee or Ranged Attack is targeted: Give it -2 strength" -- Khol
        // Regulars' own shape (case "khol-regulars-reaction" negates a Ranged Attack by +2), but for
        // either Attack kind, and gated on its own province (not a battlefield-zone card's ordinary
        // AtBattle()) actually being the one under attack, with its controller still having Personality
        // presence there -- see its own Require below (id == "fallow-lands-reaction"). Usable the same
        // turn Fallow Lands enters play: nothing about entry timing blocks a same-turn Reaction here.
        ["fallow-lands-reaction"] = new("Reaction", "If this Region's own province is under attack and its controller has presence there, after a Melee or Ranged Attack is targeted: -2 strength", "none", Trigger: "before-attack-resolves"),
        // Lady Matsu's Rage (the first Celestial, Embers of War): "Battle: Choose your performing
        // unbowed attacking Personality, target an enemy card, and discard this card: Bow the target.
        // After the next time (this turn) it straightens, destroy it." Her own trait ("After your
        // action resolves that destroyed any Personalities: Remove them from the game") is not a
        // separate ability entry at all -- it is wired directly into DestroyUnits' own shared choke
        // point (see its own doc comment, and ControlsCelestialEffect), live for as long as she stays
        // in Zone.Celestial. The delayed "destroy it once it straightens" half of THIS ability is
        // tracked in MatchState.PendingLadyMatsusRageWatches, checked reactively from Straighten()
        // itself, and cleared unconsumed at EndTurn -- "this turn" (the printed parenthetical) is
        // enforced structurally by that clear, not by comparing a stored turn number.
        ["lady-matsus-rage-bow-destroy"] = new("Battle", "Choose your performing unbowed attacking Personality, target an enemy card, and discard this card: Bow the target. After the next time (this turn) it straightens, destroy it.", "enemy-card", Performer: "own-unbowed-attacking-personality-at-battle"),
        // Scouring Flood (Seeds of Decay wave, the first real Spell wired): "This Personality has
        // +2F" is not a separate ability at all -- her own printed Force box now conveys to her
        // wielder unconditionally via PersonalityForce's own new Spell term (see its doc comment),
        // the same "always applies, bowed or not, every phase" shape an Item's Force box already had.
        // Her own printed Battle ability bows herself (the Spell) as its entire cost -- no Performer
        // needed, since nothing chooses which card acts, unlike Lady Matsu's Rage/The Earth's Hunger.
        ["scouring-flood-move-home"] = new("Battle", "Bow this card and target a defending Personality: Move him home", "defending"),
        // The Earth's Hunger (The Shadow's Embrace, the second Spell): "Bow this Shugenja, or bow
        // this card if he is Earth" -- see own-wielder-or-source-with-wielder-keyword's own doc
        // comment for the cost-choice shape (Xijkt's own, mirrored the other way round).
        ["earths-hunger-destroy"] = new("Battle", "Bow this Shugenja, or bow this card if he is Earth, and target an enemy card without attachments: Destroy it", "enemy-card", Performer: "own-wielder-or-source-with-wielder-keyword", RequiredKeywords: ["Earth"], RequireNoAttachments: true, CostOptions: [CostOption.Bow]),
        // Sanctuary of the Waves (oracle-l5r-10238, spell-wave-4, issue #295): the mirror-image target
        // shape of own-wielder-or-source-with-wielder-keyword above (a TARGET choice between the
        // wielder and another own Personality, gated by the wielder's own keyword, rather than a
        // Performer/cost choice) -- see own-wielder-or-personality-with-wielder-keyword's own doc
        // comment. The move-to-battlefield-if-opposed half reuses MoveToBattlefield/AtBattle exactly as
        // intervening-spirit-move/earth-flows-move already do; both of those are also this card's own
        // precedent for the exemptions below (a Personality "reinforcing" the battle is never assumed
        // to already be present).
        ["sanctuary-of-the-waves-move"] = new("Battle", "Bow this card and target this Shugenja, or, if this Shugenja is Water, you may target another of your Personalities: Move the target to the current battlefield if he would be opposed there. Straighten him if he moved", "own-wielder-or-personality-with-wielder-keyword", RequiredKeywords: ["Water"]),
        // Kitsu Akai (Second City, issue #257): the engine's first "create a card" effect --
        // MatchEngine.CreatePersonality, no target of its own (the created card is not chosen, only
        // named by the effect itself, the same "none" shape kano-token already uses for a no-target
        // Limited action).
        ["kitsu-akai-create"] = new("Limited", "Create a 2F/2C/3PH Lion Clan · Samurai · Ancestor · Spirit Personality", "none"),
        // "Bow your performing Ancestor Personality" -- own-unbowed-personality-at-battle-with-keyword
        // already covers exactly this shape (Volcano Tattoo's own generic Performer category), just
        // with a single RequiredKeywords entry instead of two. The created Lion Ancestor is the usual
        // cost-payer, but any other own Ancestor-keyword Personality at the battle qualifies too --
        // Ancestor carries no mechanical rule of its own beyond this kind of qualification check
        // (issue #257's own user spec).
        ["kitsu-akai-battle"] = new("Battle", "Bow your performing Ancestor Personality and target an enemy card without attachments: Destroy it. Gain 1 Honor", "enemy-card", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ancestor"], RequireNoAttachments: true),
        // Second City unwired-card audit (issue #264): five Strategy cards found buildable with
        // existing primitives alone. All five reuse the generic RequiredKeywords Performer shape
        // (own-unbowed-personality-at-battle-with-keyword / own-personality-with-keyword-at-battle) --
        // Magistrate/Paragon/Commander/Spirit carry no mechanical rule of their own here either,
        // exactly like Kitsu Akai's own Ancestor/Spirit precedent above.
        ["dispensing-justice-destroy"] = new("Battle", "Choose your performing unbowed Magistrate and target an enemy Personality: If he has no attachments and is dishonorable, Kolat, Ninja, or Shadowlands, destroy him. Move him home", "enemy-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Magistrate"], IfTarget: TargetStatus.DishonorableKolatNinjaOrShadowlands),
        // Fear is a printed keyword with no mechanical rule of its own (confirmed against the card's
        // own scan -- a separate keyword line, not a "Fear Battle:" compound timing prefix the way the
        // local catalogue's own text paraphrase suggested), the same as Wrathful Ancestor's below.
        ["bow-before-my-will"] = new("Battle", "Choose your performing unbowed Paragon and target an enemy card: Bow it. Negate its next straightening (this turn) if your Paragon is Shadowlands. Lose 2 Honor", "enemy-card", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Paragon"]),
        // No "unbowed" qualifier printed on the Performer -- same shape as Again!'s own precedent.
        ["surety-of-purpose"] = new("Battle", "Choose your performing Paragon and target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor", "lower-or-equal-honor-than-performer", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"]),
        ["unexpected-support"] = new("Battle", "Choose your performing unbowed Commander and target an enemy card with as many or fewer Followers than the performer: Bow it. Negate its next straightening (this turn)", "enemy-fewer-or-equal-followers-than-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Commander"]),
        // "This movement will not be negated": deliberately NOT added to MoveOrStraightenKind below,
        // so Tamori Tamoko's own curated negation interrupt (which only ever recognizes ability ids
        // listed there) can never apply to it -- exemption by omission. Champion of Thunder's own
        // "all" negation is not gated by that list, so issue #299 additionally routes this id through
        // MovementCannotBeNegated (the same shared check Shinjo Kinto's conditional clause uses).
        ["wrathful-ancestor-move"] = new("Battle", "Choose your performing unbowed Spirit and target an enemy Personality: Move him home. This movement will not be negated", "enemy-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Spirit"]),
        // Strategic Sacrifice (issue #264, Strategy wave 7): the one false-negative the BLOCKERS
        // documentation pass itself caught -- a plain Siege Performer (RequiredKeywords' own already-
        // common AtBattle-safe shape) and one new Force-vs-province-strength Target category.
        ["strategic-sacrifice-toggle"] = new("Battle", "Choose your performing unbowed Siege Personality, and target a card with Force lower than or equal to the current battlefield's province's strength in a unit: Bow or straighten the card. Gain 2 Honor", "force-lte-province-strength", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Siege"]),
        // The Weakness of Man (issue #267): the proof case for the general Battle/Open presence
        // primitive -- Open half performed from Home with no presence check, Battle half needing the
        // performer's own AtBattle presence exactly like an ordinary Battle ability.
        ["weakness-of-man-toggle"] = new("BattleOrOpen", "Choose your performing unbowed Monk and target a Personality whose Force is different from his own base Force: Bow or straighten him", "personality-force-neq-performer-base", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"]),
        // Improved Defenses (issue #267): the proof case for the "at any location" presence
        // exemption -- an ordinary "Battle"-Timing ability whose own printed Performer text softens
        // the usual at-this-battlefield requirement without removing it outright.
        ["improved-defenses-strength"] = new("Battle", "Bow your performing Artisan at any location and discard the top card of your Fate deck: Increase the current battlefield's province's strength by the card's Focus Value plus one", "none", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Artisan"]),
        // Second City unwired-card audit (issue #264), Personality wave 18: nine cards found
        // buildable with existing primitives alone -- self-printed abilities, no separate Performer
        // (Matsu Kasei needs no entry here at all: Effect="attacking-two" plus reusing the existing
        // "incapacitated" ability id verbatim, see package_personality_wave_18.py).
        ["keppo-destroy"] = new("Battle", "If you have Reconnaissance, target an enemy attachment: Destroy it", "enemy-attachment"),
        ["kuronada-destroy"] = new("Battle", "Target an enemy card without attachments: Destroy it", "enemy-card", RequireNoAttachments: true),
        ["moto-ming-gwok-attack"] = new("Battle", "If there is a dead Personality in any player's discard pile: Ranged 5 Attack", "attack"),
        ["shiba-kudome-duel"] = new("Battle", "Target an enemy Personality: Kudome duels him. Bow the duel's loser. Give Kudome a +1F Fire token if she won and you control a Shugenja", "enemy-personality", IssuesDuel: true),
        ["shikaro-attack"] = new("Battle", "Ranged 4 Attack", "attack"),
        ["shosuro-ritoru-bow"] = new("Battle", "Target an enemy card: Bow it. Destroy it if it has no attachments and you control a Courtier", "enemy-card"),
        // "You may take an additional Battle action which Takasho must perform" -- GrantAdditionalAction
        // scoped to Takasho's own id, the same shape a Ring's own recruitment grant already uses.
        ["takasho-move"] = new("Battle", "If any enemy units are at the current battlefield: Move Takasho there. You may take an additional Battle action which Takasho must perform", "none"),
        ["takasho-attack"] = new("Battle", "Ranged 5 Attack", "attack"),
        ["yogo-adi-choice"] = new("Open", "Target another player's Personality: If he is dishonorable, his controller loses 1 Honor. Otherwise, give your provinces +2 strength", "enemy-personality", ActionKeywords: ["Political"]),
        // Second City unwired-card audit (issue #264), wave of Holding/Item/Region/Spell cards found
        // buildable with existing primitives. Archery Range's own Reaction needs no AtBattle Require
        // at all (unlike Khol Regulars, a Personality, it's a Holding sitting in Zone.Home -- its
        // printed text carries no location qualifier either).
        ["archery-range-reaction"] = new("Reaction", "After a Ranged Attack is targeted: +1 strength", "none", Trigger: "before-attack-resolves"),
        ["slanderer-dishonor"] = new("Limited", "Bow this card: If you control a Courtier or Magistrate, dishonor a target Personality", "personality", ActionKeywords: ["Political"]),
        ["temple-of-hotei-honor"] = new("Limited", "Bow your performing Shugenja: Gain 1 Honor", "none", Performer: "shugenja"),
        // "Target a Ring in play or in your discard pile": the first Target category spanning BOTH
        // an ordinary in-play card and one sitting in a discard pile at once -- own-ring-in-discard's
        // own special validation branch, generalized with an OR against the ordinary InPlay(target) path.
        ["tranquil-garden-ring"] = new("Open", "Bow this card, and target a Ring in play or in your discard pile: If the Ring is in play, straighten it. Otherwise, put it into your hand and destroy this card", "ring-in-play-or-own-discard"),
        ["flesh-cutter-arrows-reaction"] = new("Reaction", "After a Ranged Attack from an action performed by this Personality targets a card without an attached Armor, bow this card: +2 strength", "none", Trigger: "before-attack-resolves"),
        // A Close Call: narrower than the general in-play unit-targeting Reaction primitive above --
        // reacts only to a Ranged/Melee Attack targeting an Armor-wearing Personality, so it reuses
        // the EXISTING before-attack-resolves Reaction window/attackReaction primitive verbatim (the
        // same shape khol-regulars-reaction above already uses), gated by its own Require (Armor
        // attached, Kind Ranged or Melee) rather than the new unit-targeting watcher. "Negate its
        // effects" reuses the exact same shape training-attack-negate already established
        // (_state.PendingAttack!.Strength = 0, still respecting Protection).
        ["a-close-call-negate"] = new("Reaction", "After a Personality with an Armor is targeted by a Melee or Ranged Attack: Negate its effects.", "none", Trigger: "before-attack-resolves"),
        ["a-close-call-boost"] = new("Battle", "Target a Personality with an attachment: Give him +2F.", "personality-with-attachment"),
        // Dual-choice pair sharing one UsageKey (horibe-bow/horibe-straighten's own shape); "Even if
        // this card is bowed" needs the existing bowed-source exemption list, and (since the source
        // is an Item, not a Personality) an explicit AtBattle Require the blanket check never gives it.
        ["fubatsu-plate-straighten"] = new("Battle", "Even if this card is bowed: Straighten this Personality", "none", UsageKey: "fubatsu-plate"),
        ["fubatsu-plate-move"] = new("Battle", "Even if this card is bowed: Move this Personality home", "none", UsageKey: "fubatsu-plate"),
        ["nightingale-blade-bow"] = new("Battle", "Bow this card and target an enemy card: Bow it. Destroy it if it is an attachment and this Personality is Dragon Clan or a Kensai", "enemy-card"),
        ["satoshi-warfans-draw"] = new("Reaction", "After this card enters play from your hand: Draw a card", "none", Trigger: "own-enters-play"),
        // Overgrown Grove: needs a new per-id Require scoping it to a battle at ITS OWN province --
        // Regions sit in Zone.Region (never AtBattle-eligible), and the blanket Battle-timing presence
        // check only auto-applies its "the source card itself must be at this battlefield" half to a
        // Personality-type source, the same gap Scouring Flood/The Earth's Hunger needed for Spells.
        ["overgrown-grove-move"] = new("Battle", "Discard the top 3 cards of your Fate deck and target an attacking Personality: Move him home", "attacking"),
        ["intervening-spirit-move"] = new("Battle", "Even if this unit is not at the current battlefield, if any enemy units are there, target your Personality: Move him there. Straighten him if he moved. If he is an Ancestor and this Shugenja is Water, you may take an additional Battle action", "own-personality"),
        ["earth-flows-move"] = new("Battle", "If you are the Defender, target a Personality: Move him to the current battlefield. Straighten his unit if he moved", "personality"),
        ["winds-of-dismissal-choice"] = new("Limited", "Bow this Shugenja and target another player's Personality: If he is dishonorable, Kolat, Ninja, or Shadowlands, his controller loses Honor equal to his base Personal Honor. If this Shugenja is Air, gain 1 Honor", "enemy-personality", IfTarget: TargetStatus.DishonorableKolatNinjaOrShadowlands),
        // Stormless Fury (issue #276 follow-up): a Reaction pair sharing one printed "after the
        // resolution of a Battle action" moment (OpenBattleActionReactionWindow), watched generically
        // rather than tied to a specific effect classification (Fallow Lands/Khol's own "before-
        // attack-resolves" narrower trigger, or Hizumi/Ruya's own "a bow just happened" one). The two
        // halves never both apply to the same physical card copy at once -- the card is either in
        // Zone.Hand (offering stormless-fury-attach) or already attached/in play (offering
        // stormless-fury-bow) -- so they safely share the one "after-battle-action-resolves" Trigger,
        // disambiguated purely by zone at offer time, the same technique bowed-personality-at-own-
        // battlefield/own-action-bowed-personality already share under "bowReaction".
        //
        // stormless-fury-attach is the general "out of play Reaction reusing Equip (Limited)'s own
        // attach mechanism" primitive: unlike ordinary Equip (a Limited action from hand, no Reaction
        // gate) or Equip (Battle) (a PlayerAbility that also grants an Additional Action on attach),
        // this attaches an out-of-play card as a genuine Reaction, deliberately WITHOUT granting that
        // Additional Action -- see AttachAsOutOfPlayReaction in MatchEngine.cs. The specific "your
        // performing Thunder Shugenja" restriction is Stormless Fury's own bespoke Require, not part
        // of the general primitive.
        ["stormless-fury-attach"] = new("Reaction", "After the resolution of a Battle action, if this card is in your hand, choose your performing Thunder Shugenja: Attach this card to him", "none", Trigger: "after-battle-action-resolves"),
        // stormless-fury-bow is an ordinary in-play Reaction on the same shared trigger -- "target a
        // Personality who performed it" is always the one just-resolved performer
        // (MatchState.BattleActionReactionPerformerId), so it needs no separate Target selection.
        // Excluded, per the card's own errata ruling, from firing against the SAME action instance
        // that triggered this copy's own stormless-fury-attach half (see
        // MatchState.BattleActionReaction1UsedCardIds and its Require below).
        ["stormless-fury-bow"] = new("Reaction", "After the resolution of a Battle action, if this Shugenja is at the current battlefield, target a Personality who performed it: Bow him", "none", Trigger: "after-battle-action-resolves"),
        // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action" Reaction
        // primitive (ResolveOrDeferAnnounceReaction in MatchEngine.cs), generalizing the exact
        // announce-time hook ExecuteAbilityEffects already uses for War Weariness/Times of Strife into
        // a real, player-usable, deferred Reaction window -- offered only when the about-to-execute
        // ability is classified Ranged by the existing AttackKind() lookup (a card is free to react to
        // ANY announced action in principle; this one's own printed text narrows it to Ranged). Her
        // own effect sets a transient immunity flag (MatchState.RangedAttackStrengthReductionImmune)
        // that every existing Attack-strength-reducing Reaction (Fallow Lands, Training's own negate)
        // now checks before applying, cleared once that specific Attack's own resolution completes
        // (ResolveAttackNow) -- never carried into a later, unrelated Attack.
        ["tsuruchi-yakusho-immunity"] = new("Reaction", "After you announce an action, if Yakusho is at the current battlefield: Ranged Attacks from the action's effects will not have their strength reduced", "none", Trigger: "own-ranged-action-announced"),
        // Legion of the Bat (issue #279): "Reaction: After the resolution of an action that moved a
        // target enemy Infantry Personality to this card's battlefield: Bow the Personality." Target
        // "none" -- the affected card is auto-determined from MoveReactionTargetId (set by
        // OpenMoveReactionWindow), not separately chosen by the reacting player, the same shape
        // Rokai/Tamoko's own before-action-resolves Reactions already use for a similarly
        // context-determined card. See MoveToBattlefield's own doc comment for what counts as "moved."
        ["legion-of-bat-bow"] = new("Reaction", "After the resolution of an action that moved a target enemy Infantry Personality to this card's battlefield: Bow the Personality.", "none", Trigger: "moved-enemy-infantry-to-own-battlefield"),
        // Nikutai (issue #279): "Reaction: Even if this card is bowed, after a Follower in this unit
        // bows: Straighten it." "It" is the sibling Follower that just bowed, not Nikutai itself --
        // confirmed directly against the card's own scan, since the local catalogue's own paraphrase
        // ("Tireless Reaction") could otherwise be misread either way. Target "none" -- the affected
        // Follower is auto-determined from NikutaiReactionTargetId (set by OpenNikutaiReactionWindow),
        // the same shape legion-of-bat-bow above uses. "Even if this card is bowed" is the plain-
        // English printing of this engine's own established "Tireless"/bowed-source exemption (see
        // ValidateAbility's own exemption list) -- Nikutai needs it because the SAME bow-sweep that
        // bows a sibling Follower could, in principle, have bowed Nikutai itself first.
        ["nikutai-straighten"] = new("Reaction", "Even if this card is bowed, after a Follower in this unit bows: Straighten it.", "none", Trigger: "sibling-follower-bowed"),
        ["nikutai-attack"] = new("Battle", "Melee 5 Attack", "attack"),
        // Yasuki Tsujiken (oracle-l5r-9469): "After Tsujiken enters play, target a Holding: Until
        // Tsujiken leaves play, after each time its controller bows it, he loses 1 Honor" -- a
        // persistent, per-instance watched-relationship trigger (MatchState.TsujikenWatches), unlike
        // every existing Reaction trigger here, which either fires once (this one too, at entry) or
        // watches a fixed relationship (the source's own unit/location) rather than a target selected
        // once and tracked indefinitely afterward. No restriction on selecting the same Holding twice
        // (a second copy of Tsujiken, or -- nothing printed forbids it -- the same Tsujiken were he to
        // somehow re-enter play) stacks a second, independent mark.
        ["tsujiken-watch-holding"] = new("Reaction", "After Tsujiken enters play, target a Holding: Until Tsujiken leaves play, after each time its controller bows it, he loses 1 Honor", "any-holding", Trigger: "own-enters-play"),
        // "Bow Tsujiken and target a Holding: Straighten it. You may bow it if its base Gold Cost is
        // 4 or lower" -- "it" in the second sentence is Tsujiken himself (bowing him is the ability's
        // own cost, normally mandatory; a cheap-Holding target makes that cost optional instead), NOT
        // the Holding, which this ability always straightens, never bows -- see the execution case.
        ["tsujiken-straighten-holding"] = new("Open", "Bow Tsujiken and target a Holding: Straighten it. You may bow it if its base Gold Cost is 4 or lower", "any-holding"),
        // Unimpeachable Name (oracle-l5r-8989): "Reaction: Before the resolution of another player's
        // action: Reduce each Honor loss from its effects by 3 (minimum 0). You may target and
        // rehonor a dishonorable Personality." An out-of-play Reaction broader than the existing
        // "before an action resolves" primitive (Mirumoto Rokai/Tamori Tamoko, above) -- that one only
        // watches a curated destroy/move/straighten Target on ITS OWN in-play card; this one is
        // offered against ANY action, played from outside play. See
        // CardAbilities.ResolveOrDeferUnimpeachableName/AbilityMayCauseHonorLoss for the (also
        // curated, documented-scope, extendable) classifier deciding when the window is worth opening
        // at all.
        ["unimpeachable-name-reaction"] = new("Reaction", "Before the resolution of another player's action: Reduce each Honor loss from its effects by 3 (minimum 0). You may target and rehonor a dishonorable Personality", "optional-dishonorable-personality", Trigger: "before-any-action-resolves"),
        ["unimpeachable-name-bow"] = new("Battle", "Bow your performing Shugenja and target an enemy card: Bow it. Gain 2 Honor", "enemy-card", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Shugenja"]),
        // Follower wave 14 (issue #285): six "Not blocked; deferred for scope" cards from
        // build_card_inventory.py's own BLOCKERS audit.
        //
        // Asako House Guard (oracle-l5r-543): "Battle/Open: Target a Personality: Set his Force
        // equal to his Chi or Personal Honor. Gain 1 Honor." The BLOCKERS note's claim of an
        // existing "dual-choice Set-Force pattern" does not name a literal SetForce primitive --
        // there isn't one (Force is always a computed StatModifier sum, never directly settable) --
        // but the underlying idiom (compute the delta needed to reach a target value, then
        // ApplyModifier that delta) is exactly daifuke-swap-force's own shape, and arata-force-
        // penalty/rumormonger-penalty already key a Force delta off a DIFFERENT stat the same way.
        // The "or" choice itself reuses the established shared-UsageKey dual-ability pattern
        // (atsumichi-bow/atsumichi-straighten, mitoshi-bow/mitoshi-straighten): two catalog entries,
        // one printed opportunity, whichever is used consumes both. Modeled directly on its own
        // sibling "House Guard" cycle card, Asahina House Guard (oracle-l5r-494, AttachRequiresKeyword
        // "Crane Clan", asahina-straighten) -- same BattleOrOpen/Target "personality"/Gain-1-Honor
        // shape, just Void/Phoenix instead of Air/Crane and Set-Force instead of Straighten.
        ["asako-house-guard-force-chi"] = new("BattleOrOpen", "Target a Personality: Set his Force equal to his Chi. Gain 1 Honor", "personality", UsageKey: "asako-house-guard-force"),
        ["asako-house-guard-force-honor"] = new("BattleOrOpen", "Target a Personality: Set his Force equal to his Personal Honor. Gain 1 Honor", "personality", UsageKey: "asako-house-guard-force"),
        // Legulus's Legion (oracle-l5r-10899): "Battle, bow: Melee Attack with strength equal to
        // this Follower's Force." AttackStrength/AttackKind already thread sourceId through to every
        // caller (ActionPerformer(...).Id, same as sakata-attack's own token-count read), so reading
        // the performer's own live CardForce at declare-time (ResolveOrDeferAttack calls
        // EffectiveAttackStrength once, before deferring) needed no new plumbing -- this resolves the
        // BLOCKERS note's own open verification question. "Bow" is a real printed cost distinct from
        // an Attack's own resolution (no card here bows its own performer automatically, confirmed by
        // nikutai-attack/sakata-attack neither doing so): modeled on kasuru-draw's own explicit
        // `source.Bowed = true` cost line, combined with nikutai-attack's own plain ResolveOrDeferAttack
        // call for the Melee half.
        ["legulus-legion-attack"] = new("Battle", "Bow this Follower: Melee Attack with strength equal to this Follower's Force", "attack"),
        // Village Guardian (oracle-l5r-9162): "Reaction: After this card enters play from your hand:
        // Draw one card for each player who has had a province destroyed this game." A genuine
        // printed "Reaction:" (unlike EntryHonorLoss's mandatory-trait shape below), so it's an
        // ordinary entry-Reaction ability (own-enters-play, same trigger as satoshi-warfans-draw),
        // just looping Draw() by MatchState.ProvinceEverDestroyed's own per-player count instead of
        // drawing exactly one card.
        ["village-guardian-draw"] = new("Reaction", "Draw one card for each player who has had a province destroyed this game", "none", Trigger: "own-enters-play"),
        // Village Raiders (oracle-l5r-9163): "After this card enters play: Lose 1 Honor. This
        // Personality has Conqueror. Battle: Melee 4 Attack." Its own scan literally prints "This
        // Personality has Conqueror" despite deck-pool.json's types being ["Follower"] -- read
        // literally, that is exactly AttachGrantsKeyword's own established shape ("This Personality
        // has X" for an attachment's wielder, Commander's Steed's family), so it needs no new Effect
        // case at all: AttachGrantsKeyword "Conqueror" plus the existing EntryHonorLoss field cover
        // both non-Battle lines. Only the plain "Battle: Melee 4 Attack" (no bow cost, unlike Legulus's
        // Legion above) needed a new ability id, modeled directly on nikutai-attack's own shape.
        ["village-raiders-attack"] = new("Battle", "Melee 4 Attack", "attack"),
        // Strategy wave 13 (issue #287): Batch B of the "Not blocked; deferred for scope" BLOCKERS
        // audit -- eight Strategy cards a prior audit confirmed buildable with existing (or
        // trivially small new) primitives. Surety of Purpose (oracle-l5r-7664), originally listed
        // among these eight, turned out to already be fully wired and published (strategy-wave-6,
        // issue #264's own Second City audit) under the existing "surety-of-purpose" ability id
        // above -- its own BLOCKERS entry was simply stale and has been removed, not re-wired here.
        // Scorn the Weak (oracle-l5r-9925): "Battle: Target an enemy Personality: Give him -5F. If
        // his Force is now 0, his controller loses 1 Honor and you may target and dishonor a
        // Personality." No Performer at all (crushing-force's own plain-Target shape), a flat -5F
        // (ApplyModifier already floors Force at 0 via CardForce/PersonalityForce's own Math.Max).
        // The trailing "you may target and dishonor" is CardIds' own established optional-second-
        // selection reuse (see ValidateAbility's own scorn-the-weak branch), the same "not required
        // to exist" idiom optional-dishonorable-personality already establishes for a single
        // TargetId, just carried on CardIds since this ability already has a required primary
        // TargetId of its own.
        ["scorn-the-weak"] = new("Battle", "Target an enemy Personality: Give him -5F. If his Force is now 0, his controller loses 1 Honor and you may target and dishonor a Personality", "enemy-personality"),
        // Strength in Terror (oracle-l5r-7538): "Battle: Choose your performing Paragon and target
        // an enemy Personality: Reduce his Force by an amount equal to your Paragon's Force. You
        // may target and destroy one of the Personality's attachments. Lose 3 Honor." No "unbowed"
        // qualifier on the Performer, the same shape surety-of-purpose already established for a
        // bare "Choose your performing Paragon." The dynamic Force reduction reads the chosen
        // Performer's own live CardForce, the same self-referential shape legulus-legion-attack
        // already established for a Follower's own Force, just against a separately-chosen
        // Performer instead of the source card itself. The optional attachment-destroy is CardIds'
        // own second-selection reuse again, restricted to one of the primary target's own attachments.
        ["strength-in-terror"] = new("Battle", "Choose your performing Paragon and target an enemy Personality: Reduce his Force by an amount equal to your Paragon's Force. You may target and destroy one of the Personality's attachments. Lose 3 Honor", "enemy-personality", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"]),
        // Strength of Purity (oracle-l5r-7546): "Battle: Choose your performing Personality with 3
        // or higher Personal Honor: Give him +3F." A numeric Personal Honor threshold Performer --
        // no existing category reads a threshold off the Performer itself (lobby's own >=1 check is
        // a plain Target category, not a Performer one), so this adds one, hardcoded to this one
        // card's own printed 3 (the same "no need to generalize for a single card" discipline
        // lobby's own hardcoded 1 already follows).
        ["strength-of-purity"] = new("Battle", "Choose your performing Personality with 3 or higher Personal Honor: Give him +3F", "none", Performer: "own-personality-honor-3-plus-at-battle"),
        // Strength of the Bear (oracle-l5r-10748, no printed Cost -- confirmed blank on its own
        // scan, matching deck-pool.json's own missing "cost" field): "Battle: Bow your target
        // unbowed Personality. Bow a target enemy card with lower Force than his. If your
        // Personality has 5 or more printed Force, straighten him." "Target" (not "performing")
        // matches Steel on Steel's own own-unbowed-personality Performer precedent exactly (no
        // AtBattle qualifier of its own). enemy-force-lower-than-performer (immovable-object-bow's
        // own Target) already compares against the chosen Performer's live Force verbatim. The
        // trailing printed-Force threshold reuses Definition(performer).Force, the same "printed,
        // not live" idiom personality-force-neq-performer-base already established.
        ["strength-of-the-bear"] = new("Battle", "Bow your target unbowed Personality. Bow a target enemy card with lower Force than his. If your Personality has 5 or more printed Force, straighten him", "enemy-force-lower-than-performer", Performer: "own-unbowed-personality"),
        // Strength of the Divine (oracle-l5r-10555, no printed Cost -- confirmed blank on its own
        // scan, same as Strength of the Bear above): "Battle: Target your opposed unbowed Paragon.
        // Give him +2F or +2PH. Melee Attack with strength equal to his Force or Personal Honor." A
        // dual choice sharing one UsageKey (Horibe's own bow/straighten shape, and Batch A's own
        // asako-house-guard-force-chi/-honor pair) -- but each branch here pairs a buff with an
        // Attack whose own strength reads that SAME branch's stat back off the just-buffed Performer
        // (monastery-fire-attack's own "read the performer's live state, then attack" shape), not a
        // Set-Force. own-unbowed-personality-with-keyword-opposed is a new Performer category:
        // own-unbowed-shugenja-opposed-at-battle's own "opposed" (enemy army present) gate,
        // generalized off its hardcoded Shugenja onto RequiredKeywords the same way
        // own-unbowed-personality-at-battle-with-keyword already generalized the plain at-battle
        // gate -- "opposed," not "performing," so no AtBattle check of its own, matching this card's
        // own "target," not "performing," phrasing.
        ["strength-of-the-divine-force"] = new("Battle", "Target your opposed unbowed Paragon: +2F. Melee Attack with strength equal to his Force", "attack", Performer: "own-unbowed-personality-with-keyword-opposed", RequiredKeywords: ["Paragon"], UsageKey: "strength-of-the-divine"),
        ["strength-of-the-divine-honor"] = new("Battle", "Target your opposed unbowed Paragon: +2PH. Melee Attack with strength equal to his Personal Honor", "attack", Performer: "own-unbowed-personality-with-keyword-opposed", RequiredKeywords: ["Paragon"], UsageKey: "strength-of-the-divine"),
        // The Strength of the Spider (oracle-l5r-8362): its own Focus Effect clause ("you may choose
        // now to delay the effects of losing this duel until after the next Dynasty Phase begins")
        // is genuinely novel duel-resolution-timing plumbing -- no other card's own printed text
        // defers a duel's OWN loss consequences this way anywhere in this catalog yet, so it is
        // deliberately deferred here, narrower in scope than the rest of this card, rather than
        // inventing new Dueling infrastructure for a single card (the same existing "Dueling/Focus
        // Effects" gap oracle-l5r-6702/7995 are already blocked on). Its own Battle half is fully
        // wired: own-unbowed-spider-personality-at-battle is a new Performer category (a CLAN check,
        // HasClanAffiliation, not a printed Keyword -- RequiredKeywords/HasKeywords cannot express
        // it), enemy-force already covers "an enemy Personality or Follower" verbatim, the dynamic
        // Force reduction reads the Performer's own live CardForce the same way strength-in-terror
        // above does, and DestroyAttackTarget already dispatches Personality-vs-Follower destruction
        // correctly for the "destroy it if it is bowed" branch.
        ["strength-of-the-spider"] = new("Battle", "Choose your performing unbowed Spider Clan Personality and target an enemy Personality or Follower: Destroy it if it is bowed. Reduce its Force by an amount equal to your Personality's Force. Bow it if its Force is now 0", "enemy-force", Performer: "own-unbowed-spider-personality-at-battle"),
        // Unexpected (oracle-l5r-9940): "Battle: Choose your performing unbowed Personality and
        // target an enemy card without attachments: Bow it. You may bow your Personality; if you
        // did, draw a card." own-unbowed-personality-at-battle (no RequiredKeywords -- no keyword
        // printed) and enemy-card-without-attachments both already exist verbatim. The optional
        // self-bow-for-a-draw is CardIds' own third reuse this wave: the performer's own id, when
        // supplied, opts in (a genuine two-outcome choice, unlike tsujiken-straighten-holding's own
        // auto-pick-the-better-outcome simplification -- staying unbowed has real value here, so
        // this needs an actual player choice, not an automatic default).
        ["unexpected"] = new("Battle", "Choose your performing unbowed Personality and target an enemy card without attachments: Bow it. You may bow your Personality; if you did, draw a card", "enemy-card-without-attachments", Performer: "own-unbowed-personality-at-battle"),
        // Strategy wave 14 (issue #289): Batch C of the "Not blocked; deferred for scope" cards from
        // build_card_inventory.py's own BLOCKERS audit. Wrathful Ancestor (oracle-l5r-9390), originally
        // assigned to this batch too, was found already fully wired and published in strategy-wave-6
        // (issue #264's own Second City audit, under the existing "wrathful-ancestor-move" ability id)
        // -- its own BLOCKERS entry was simply stale, the same "Surety of Purpose" situation Batch B
        // hit. It is NOT re-packaged here; this wave publishes the other seven.
        //
        // Banzai! (10929, Discipline :g2:): "Battle: Give your target opposed Personality +2F." Its
        // own scan confirms the plain "Give your target X" phrasing (no separate Performer), the exact
        // shape crushing-force's own-opposed Target category already established (own, opposed --
        // Army(1-player.Id, battlefield).Any(), no AtBattle check of its own). Discipline (issue #184)
        // layers a play mode on top of "using one of its actions," not in place of that action's own
        // normal timing (core rules, Aftermath supplement) -- so since issue #331 the ability's Timing is
        // simply its printed verb ("Battle": the Battle phase, with the ordinary battlefield-presence
        // gate) and Discipline is a flag on top of it. That flag replaced the old Timing "Discipline"
        // (permissive as BattleOrOpen) and the per-id narrowing Require this card needed under it.
        ["banzai"] = new("Battle", "Discipline (2). Battle: Give your target opposed Personality +2F", "own-opposed", Discipline: true),
        // Pure Intent (9922, Cost 0): its own scan reads "Choose your performing opposed Personality:
        // Give him a Force bonus equal to his Personal Honor. Gain 2 Honor" -- deck-pool.json's own
        // "Give your target opposed Personality" paraphrase was misleading; the real card uses the
        // "Choose your performing" Performer idiom (Target "none", effect on the Performer itself),
        // not a plain Target. No "unbowed" printed, so a new own-personality-opposed Performer category
        // (own, opposed, no unbowed/keyword gate) -- own-unbowed-personality-with-keyword-opposed's own
        // shape minus the unbowed/RequiredKeywords restrictions neither this card prints.
        ["pure-intent"] = new("Battle", "Choose your performing opposed Personality: Give him a Force bonus equal to his Personal Honor. Gain 2 Honor", "none", Performer: "own-personality-opposed"),
        // Marshal Your Strength (10946, no printed Cost -- confirmed blank on its own scan, same as
        // Strength of the Bear/Divine's own precedent): "Battle: Give your target Personality Force
        // and Chi bonuses equal to his Personal Honor, minus 1 if he is unopposed." own-personality
        // (plain Target, no Performer -- not "opposed" gated, since it works unopposed too, just for
        // less) and CardPersonalHonor both already exist; ApplyModifier is stat-agnostic (its own Chi
        // branch, checked live for every stat that reaches it, already covers a Chi modifier -- no
        // published card has exercised it yet, this is the first). "Unopposed" is a new inline
        // condition, the negation of the same Army(1-player.Id, battlefield).Any() check every other
        // "opposed" primitive here already reads -- no new helper needed, just the boolean flipped.
        ["marshal-your-strength"] = new("Battle", "Give your target Personality Force and Chi bonuses equal to his Personal Honor, minus 1 if he is unopposed", "own-personality"),
        // Lost in Transit (10945, Discipline :g1:): "Battle/Open: Bow a target attachment." Timing is its
        // printed verb, BattleOrOpen (issue #331: Discipline is a flag, not a timing) -- so, like every
        // Battle/Open card since issue #267, it needs no presence at all when played as an Open action in
        // the Action phase, and the ordinary battlefield-presence rule when played during a battle (the old
        // Discipline timing skipped that check in the Battle phase, which the printed verb never allowed).
        // "attachment" (a standalone Target category, no dishonorable-Personality
        // alternative) already exists verbatim -- nubane-bow's own shape (Open, "Target an attachment:
        // bow it"), not merely "trivially small" to add as the BLOCKERS note assumed; it was already
        // there.
        ["lost-in-transit-bow"] = new("BattleOrOpen", "Discipline (1). Battle/Open: Bow a target attachment", "attachment", Discipline: true),
        // Strong Defenses (9931, Cost 0, Siege): "Reaction: After a Ranged or Melee Attack is
        // targeted: Give it -3 strength.\nBattle: Choose your performing defending Personality: Give
        // him +5F. Straighten him before this battle resolves." The Reaction half mirrors Khol
        // Regulars' own shape exactly (khol-regulars-reaction's own +2, fallow-lands-reaction's own -2)
        // -- a Strategy-card Reaction played straight from hand, A Close Call's own established shape,
        // just with no Armor-attached restriction and a magnitude of -3 instead. The Battle half's own
        // scan prints no "unbowed" qualifier on "your performing defending Personality" (Straighten
        // "before this battle resolves" is itself the whole point -- the performer may well already be
        // bowed), so a new own-defending-personality-at-battle Performer category (own-unbowed-
        // attacking-personality-at-battle's own mirror image, off the existing IsDefendingUnit helper,
        // minus the unbowed restriction that helper's own attacking counterpart carries). "Before this
        // battle resolves" is simply straightening immediately, mid-resolution, well before the
        // eventual BattleForce tally -- no new timing plumbing needed.
        ["strong-defenses-reaction"] = new("Reaction", "After a Ranged or Melee Attack is targeted: -3 strength", "none", Trigger: "before-attack-resolves"),
        ["strong-defenses-boost"] = new("Battle", "Choose your performing defending Personality: Give him +5F. Straighten him before this battle resolves", "none", Performer: "own-defending-personality-at-battle"),
        // Trusting Instinct (10402, Cost 0): "Reaction: After a Ranged or Melee attack is targeted:
        // Give it -2 strength.\nBattle: Choose your performing Personality: Move him to a battlefield
        // where he would be opposed." The Reaction half is Strong Defenses' own -3 pattern again, at
        // -2 (both added to the shared Tsuruchi Yakusho strength-reduction-immunity id list below,
        // per that list's own doc comment: "a future reducing card only needs to be added to this same
        // id list"). The Battle half's own scan likewise prints no "unbowed" qualifier -- plain
        // own-personality (already exists) -- and moves him to THE current battlefield (this engine
        // resolves one battle at a time, so "a battlefield" is always the current one, Houken-exp's
        // own "if he would be opposed there" shape), gated on that destination actually being opposed
        // (a new inline Require, the same Army(1-player.Id, battlefield).Any() opposed check yet
        // again), via the existing MoveToBattlefield primitive. No straighten afterward -- unlike
        // Michi/Houken-exp, nothing printed.
        ["trusting-instinct-reaction"] = new("Reaction", "After a Ranged or Melee Attack is targeted: -2 strength", "none", Trigger: "before-attack-resolves"),
        ["trusting-instinct-move"] = new("Battle", "Choose your performing Personality: Move him to a battlefield where he would be opposed", "none", Performer: "own-personality"),
        // Visage of the Orochi (9168, Cost 0, Thunder/Fear): "Battle: Choose your performing opposed
        // unbowed Thunder Personality: Raise his Force to 6. You may target and bow an enemy card with
        // lower Force and no attachments." own-unbowed-personality-with-keyword-opposed (Strength of
        // the Divine's own new Performer category, issue #287) already covers "opposed unbowed [X]
        // Personality" verbatim with RequiredKeywords: ["Thunder"]. "Raise ... to 6" (not "Set," unlike
        // Asako House Guard's own unconditional Set-Force pattern) is Set-Force's own delta-to-target-
        // value idiom, floored at 0 so an already-6-or-higher Performer is never reduced: Math.Max(0, 6
        // - CardForce(performer)). The optional bow reuses CardIds' own established second-selection
        // idiom (Scorn the Weak/Strength in Terror/Unexpected, issue #287) rather than a new Target
        // category: its own "lower Force" comparison is read against the Performer's own POST-raise
        // Force (Math.Max(6, CardForce(performer)) at validation time, before the Set-Force effect
        // itself has actually executed) -- inlined directly in ValidateAbility's own CardIds branch,
        // the same way Strength in Terror already inlines its own attachment-ownership check there,
        // since no generic Target category composes correctly with an effect still pending within the
        // same ability.
        ["visage-of-the-orochi"] = new("Battle", "Choose your performing opposed unbowed Thunder Personality: Raise his Force to 6. You may target and bow an enemy card with lower Force and no attachments", "none", Performer: "own-unbowed-personality-with-keyword-opposed", RequiredKeywords: ["Thunder"]),
        // Strategy wave 15 / spell-wave-4 / ring-wave-2 (issue #295): Batch D of the "Not blocked;
        // deferred for scope" audit. oracle-l5r-1121 Bow Before My Will (its own BLOCKERS entry)
        // turned out already fully wired and published in strategy-wave-6 under bow-before-my-will --
        // the same stale-entry pattern Batches B and C each hit once (Surety of Purpose, Wrathful
        // Ancestor) -- so it is not re-packaged here.
        // Charge Into Danger (oracle-l5r-10366): its own scan corrects deck-pool.json's paraphrase --
        // a plain Performer ("Choose your performing Personality"), not a separately-chosen Target;
        // "assigned to... the current battlefield" is exactly AtBattle (Assign() already sets
        // Zone.Battlefield+ProvinceIndex the moment a unit is assigned there), and "would be opposed
        // there" is the same enemy-army-presence check every sibling move ability already uses. No new
        // Performer category needed: own-personality (any of the player's own in-play Personalities)
        // already covers "at any location," since every in-play Personality zone (Home or Battlefield)
        // is reachable from there.
        ["charge-into-danger-move"] = new("Battle", "Choose your performing Personality: If he assigned to, or would be opposed at, the current battlefield, move him there. If his base Force is 7 or higher, straighten him", "none", Performer: "own-personality"),
        // Consuming Weakness (oracle-l5r-1467): RemoveFromGame and Shadowlands clan-affiliation checks
        // already exist; needs a new Shadowlands-conditional Performer category (own-personality-at-
        // battle generalized off HasClanAffiliation, the same way own-unbowed-spider-personality-at-
        // battle already generalizes it for The Strength of the Spider) and a genuinely new "dead
        // Personality in any discard pile" Target (Zone.Dead, any owner) -- contrary to its own
        // BLOCKERS note, no such Target category existed yet anywhere in this file.
        ["consuming-weakness"] = new("Battle", "Choose your performing Shadowlands Personality and remove a target dead Personality in any discard pile from the game: Give your Personality +3F, or +4F if he is Undead", "dead-personality-any-discard", Performer: "own-shadowlands-personality-at-battle"),
        // Fires of Turmoil (oracle-l5r-10552): its own scan drops deck-pool.json's "in a unit"
        // qualifier from the Target text, but every existing sibling "without attachments" category is
        // already unit-scoped (Follower, or an unattached Personality) -- kept here too, as
        // unattached-unit-card, the plain (non-enemy-restricted) counterpart to Ikoma Masumi's own
        // enemy-card-without-attachments. "A Fate deck" resolves to the performing player's own,
        // exactly like Aberrations (DiscardTopOfFateDeck) -- no separate qualifier is ever printed
        // for an unqualified "a" effect like this in the existing ruleset. Cost is 0 despite the blank
        // cost medallion on both of its own Coils of Madness printings -- confirmed against
        // oracle-l5r-10555 Strength of the Divine, an already-published 0-cost Coils of Madness
        // Strategy whose own scan renders the exact same way.
        ["fires-of-turmoil"] = new("Battle", "Discard the top card of your Fate deck. Target a card without attachments. If the discarded card's Focus Value is odd, bow the target (0 is even). Otherwise, straighten it. Move its unit home if the target has a Madness token", "unattached-unit-card"),
        // Gripped by Terror (oracle-l5r-2908): an army-scoped Nonhuman-conditional Force boost, applied
        // once at resolution (a snapshot ApplyModifier loop over Army(), not a live recomputed passive
        // the way Utaku Elite Guard's own printed keyword ability works -- this is a one-shot Strategy
        // effect, not a standing in-play trait, so it reuses only the concept, not the mechanism).
        // Needs a new own-personality-at-battle Performer (no unbowed qualifier printed, no keyword
        // gate either -- the plain "at battle" variant of own-personality that did not exist yet).
        ["gripped-by-terror"] = new("Battle", "Choose your performing Personality and target an enemy Personality or Follower: Give it -4F. If your Personality is Nonhuman, give each of your Personalities at the current battlefield +1F", "enemy-force", Performer: "own-personality-at-battle"),
        // Outnumbered (oracle-l5r-10746): no Performer or Target at all -- a pure battlefield-scoped
        // "give all qualifying cards X" effect, mirroring Utaku Elite Guard's own army-scoped shape
        // conceptually (scan your own army for a condition, apply a Force delta) but resolved once, as
        // a plain foreach over Army(), the same one-shot-vs-standing-passive distinction Gripped by
        // Terror's own doc comment above draws. Cost is 0 (see Fires of Turmoil's own doc comment for
        // why its blank Gates of Chaos cost medallion still means 0 -- confirmed against oracle-l5r-
        // 10742 Looting the Ruins and oracle-l5r-10748 Strength of the Bear, two already-published
        // 0-cost Gates of Chaos Strategies with the identical blank medallion).
        ["outnumbered"] = new("Battle", "Give all your opposed Personalities with Followers at the current battlefield +1F. Give all enemy Personalities there without Followers -1F", "none"),
        // Preserving Honor (oracle-l5r-6060): the opponent-chooses-effect shape shinden-choice already
        // uses (a dedicated PendingChoice type, resolved by its own handler) -- but a genuinely new one,
        // since the two offered outcomes (bow vs. move-away) differ from shinden's own (bow vs. Honor),
        // and the "if negated, remove from game" fallback needs its own detection hook. StalwartNegatesBow
        // already RETURNS whether a bow was negated (every existing call site just discards that bool);
        // the move branch reuses MoveHome's own internal NegatesEffect("move") check directly (inlined,
        // so the negation result can be branched on) rather than calling MoveHome itself. This ability's
        // own id is deliberately NOT added to BeforeActionReactionApplies's curated lists (Rokai/
        // Tamoko/Champion of Thunder/Brothers of Goemon): unlike every existing negatable ability, the
        // specific effect kind (bow or move) here is not known until the TARGET's controller answers the
        // PendingChoice, well after that window would need to have already opened. Stalwart remains the
        // one automatic, always-live source of "the bowing... is negated" the fallback can act on today.
        ["preserving-honor"] = new("Battle", "Target an enemy Personality at the current battlefield: His controller chooses either to bow him, or to choose another location and move him there. If the bowing or movement is negated, remove the Personality from the game", "enemy-personality"),
        // Your Clan Needs You! (oracle-l5r-9687): the most complex card in this batch. "Bring a
        // Personality straight from a province into a battlefield, paying all costs" already exists
        // three times over (Purchase, RecruitReserve, CreatePersonality), all funneling into the same
        // shared entry-sequence primitives (QuotePurchase, Pay, QueueOverconfidentDraw,
        // EntryReactionAbility, OpenEntryReactionWindow, OpenOverconfidentDrawWindow) -- this is a
        // fourth caller into that same shared sequence, reusing every one of those primitives directly,
        // for ANY own Personality (no Reserve-keyword gate) mid-Battle. Its own Performer cannot use the
        // ordinary ability.Performer mechanism at all: every existing Performer category requires
        // InPlay(performer), but a province card is never InPlay -- modeled as this ability's own
        // Target instead (own-personality-in-province, a new out-of-play Target branch alongside
        // own-kiho-strategy-in-discard/own-ring-in-discard), sidestepping that assumption entirely. The
        // Gold cost is NOT the Strategy's own printed GoldCost (0) -- it is the recruited Personality's
        // own cost minus 6 (see the dedicated cost-ternary override in the generic cost-computation
        // block below); command.SourceIds is reused for that payment exactly the way Low Market/Equip
        // (Battle) already reuse it for a non-source-card payment. The post-battle "discard his
        // attachments and return him to a province face-up" half is genuinely new -- no existing
        // primitive ever places a specific card into a chosen province, so
        // MatchEngine.CheckYourClanNeedsYouReturn/ResolveYourClanNeedsYouProvince adds one, mirroring
        // CheckEarthFlowsBonus's own post-resolution-hook shape for WHEN it fires (right as
        // FinishBattleReturnHomeAndTransition begins, once every card's post-battle Zone is settled)
        // but needing its own PendingChoice (which province) since, unlike Earth Flows' unconditional
        // Honor gain, "one of your provinces" is a genuine player choice.
        ["your-clan-needs-you"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Personality in one of your provinces: Bring him into play at the current battlefield, paying all costs but paying 6 less Gold. After the battle ends, if he is in play, discard all his attachments and put him into one of your provinces, face-up, discarding any card there", "own-personality-in-province",
            Cost: GoldCostRule.TargetRecruit(-6, floor: 0)),
        // Personality wave 21 (issue #299, Batch E of the "Not blocked; deferred for scope" BLOCKERS
        // audit): nine Personalities plus the two tokens two of them create.
        //
        // Akodo Chiyo (oracle-l5r-10290): "Battle: Melee 4 Attack" -- the existing attack primitive
        // (its own AttackStrength/AttackKind entries below, per issue #289's lesson that a new
        // attack id needs BOTH or it silently resolves as a 0-strength Attack). Her printed
        // "Crab Clan players may ignore Chiyo's Honor Requirement" is not an ability at all -- see
        // CardDefinition.HonorRequirementWaiverClan.
        ["chiyo-attack"] = new("Battle", "Melee 4 Attack", "attack"),
        // Matsu Kaido (oracle-l5r-4934): "Fear Battle:" -- "Fear" is a plain printed classification
        // word here (the same treatment Ninja/Kiho/Recon already get, and the same finding Bow Before
        // My Will/Consuming Weakness reached about their own Fear text), not a distinct engine Timing;
        // nothing in the engine reads it. "Reduce its Force to 0" is the delta-to-a-target-value shape
        // asako-house-guard-force-chi already established (its own Set-Force wording), applied once,
        // not a standing "Force is 0" override.
        ["kaido-force-zero"] = new("Battle", "Fear Battle: Target an enemy Personality or Follower: Reduce its Force to 0", "enemy-force"),
        // Matsu Miura (oracle-l5r-10853): arata-force-penalty's own shape, minus Arata's Honor gain.
        ["miura-force-penalty"] = new("Battle", "Target an enemy Personality or Follower: Give it a Force penalty equal to Miura's Personal Honor", "enemy-force"),
        // Utaku Mai (oracle-l5r-9086): "You may move Mai to the current battlefield. If she is now
        // there, give the targeted card a Force penalty equal to Mai's Personal Honor." Mai need not
        // already be at the battlefield (her whole point is reinforcing it), so she is exempted from
        // the shared battlefield-presence Requires exactly the way Takasho/Intervening Spirit/
        // Sanctuary of the Waves already are (see reinforcesFromElsewhere in ValidateAbility). "You
        // may" is always taken: skipping the move can never do her controller any good (the penalty
        // needs her there), and if she is already there the move is simply a no-op.
        ["mai-move-force-penalty"] = new("Battle", "Target an enemy Personality or Follower: You may move Mai to the current battlefield. If she is now there, give the targeted card a Force penalty equal to Mai's Personal Honor", "enemy-force"),
        // Shinjo Kinto (oracle-l5r-6906): Wrathful Ancestor's own "Move him home" (MoveHome), but the
        // "will not be negated" clause is CONDITIONAL on Reconnaissance here: registered in
        // MoveOrStraightenKind below (so Tamori Tamoko's negation interrupt recognizes it as a
        // movement, unlike Wrathful Ancestor which is simply never registered), then exempted
        // per-command by MovementCannotBeNegated when the acting player has Reconnaissance at the
        // current battlefield.
        ["kinto-move"] = new("Battle", "Target an enemy Personality: Move him home. This movement will not be negated if you have Reconnaissance", "enemy-personality"),
        // Kakita Maratai (oracle-l5r-4197): "After a duel ends that your Personality won, bow Maratai:
        // Gain 2 Honor. Take the Imperial Favor." Maratai need not be the duelist -- the first Reaction
        // on the own-won-duel window that watches ANY of its controller's Personalities winning
        // (Trigger "own-personality-won-duel"), the duelWonReaction window's own analogue of the
        // own-enters-play -> any-personality-enters-play broadcast generalization. The bow is the
        // printed cost, paid in the execution case; the shared bowed-source Require already stops a
        // bowed Maratai from using it.
        ["maratai-statue"] = new("Reaction", "After a duel ends that your Personality won, bow Maratai: Gain 2 Honor. Take the Imperial Favor", "none", Trigger: "own-personality-won-duel"),
        // Kitsune Ohsuki / Kitsune Tokoru (oracle-l5r-4469 / oracle-l5r-4474): "After X enters play:
        // Create a ... Personality" -- Kitsu Akai's own kitsu-akai-create (MatchEngine.CreatePersonality,
        // issue #257) as an entering-play Reaction instead of a Limited action, so it is created from
        // inside its creator's own already-open entry window (CreatePersonality's duringEntryReaction).
        ["ohsuki-create"] = new("Reaction", "After Ohsuki enters play: Create a 0F/2C/3PH Mantis Clan · Nonhuman · Sparrow · Cavalry · Spirit Personality", "none", Trigger: "own-enters-play"),
        ["tokoru-create"] = new("Reaction", "After Tokoru enters play: Create a 4F/2C/3PH Mantis Clan · Nonhuman · Bear · Spirit Personality", "none", Trigger: "own-enters-play"),
        // Ohsuki's Sparrow (oracle-l5r-10023): "Limited: If none of your provinces were destroyed since
        // your last turn ended: Gain 3 Honor." The condition precedes the colon, so it gates USING the
        // action at all (a per-id Require in ValidateAbility, the way horibe-bow's own "If opposed"
        // and hiruma-sniper-attack's own Reconnaissance requirement do), not merely its effect --
        // MatchState.ProvinceDestroyedSinceTurnEnd.
        ["ohsuki-sparrow-honor"] = new("Limited", "If none of your provinces were destroyed since your last turn ended: Gain 3 Honor", "none"),
        // Tokoru's Bear (oracle-l5r-10025): "Battle: Target an attacking enemy card: Bow it. Destroy it
        // if it is an attachment." -- enemy-attacking-card, a new Target category (every existing
        // "attacking" category is Personality-only; this one also reaches an attacking unit's own
        // Followers/Items/Spells).
        ["tokoru-bear-bow"] = new("Battle", "Target an attacking enemy card: Bow it. Destroy it if it is an attachment", "enemy-attacking-card"),

        // Personality wave 22 / region-wave-3 (issue #301, Batch F of the "Not blocked; deferred for
        // scope" BLOCKERS audit -- the final batch).
        // Agasha Kodo (oracle-l5r-142): "Political Open: Target a Holding: Straighten it." -- "Political"
        // is a plain printed classification word on an Open action (Rumormonger/Soshi House Guard's own
        // treatment, issue #174). The Target category already existed (any-holding, Yasuki Tsujiken's).
        ["kodo-straighten"] = new("Open", "Political Open: Target a Holding: Straighten it", "any-holding", ActionKeywords: ["Political"]),
        // Asahina Munefusa (oracle-l5r-505): two independent printed actions. The Temple straighten is
        // the first Holding Target narrowed by RequiredKeywords (own-holding-with-keyword; the sibling
        // own-unbowed-farm is a hardcoded Farm check). The Battle half is exempt from BOTH the army-
        // presence and source-AtBattle Requires (reinforcesFromElsewhere in ValidateAbility): "Even if
        // Munefusa is not at the current battlefield" -- only the TARGET must be there.
        ["munefusa-straighten"] = new("Open", "Target your Temple Holding: Straighten it", "own-holding-with-keyword", RequiredKeywords: ["Temple"]),
        ["munefusa-move"] = new("Battle", "Even if Munefusa is not at the current battlefield, target an attacking Personality: Move him home. If he moved, gain 1 Honor", "attacking"),
        // Asako Rikate (oracle-l5r-576): "Maho Battle" is a plain printed classification word. The
        // target's controller may return any of the unit's attachments to hand (a "rikateDetach"
        // PendingChoice, shinden's own shape), then the Personality gets -2C either way.
        ["rikate-detach-chi"] = new("Battle", "Maho Battle: Target an enemy Personality: His controller may put any attachments in that unit into his hand. Give the Personality -2C", "enemy-personality"),
        // Bayushi Nori (oracle-l5r-857): "Bow the Personality or all of his attachments" -- a choice of
        // effects, so two catalog entries sharing one once-per-turn use (Horibe's own dual-choice shape).
        ["nori-bow-personality"] = new("Battle", "Target an enemy Personality: Bow the Personality. If he is dishonorable, his controller loses 2 Honor", "enemy-personality", UsageKey: "nori-bow"),
        ["nori-bow-attachments"] = new("Battle", "Target an enemy Personality: Bow all of his attachments. If he is dishonorable, his controller loses 2 Honor", "enemy-personality", UsageKey: "nori-bow"),
        // Hida Mimori (oracle-l5r-3150): "Target a bowed enemy card: Destroy it." -- any enemy card in a
        // unit at the battlefield (the Personality himself or one of his attachments).
        ["mimori-destroy"] = new("Battle", "Target a bowed enemy card: Destroy it", "enemy-bowed-card"),
        // Isawa Mitsuko, Experienced (oracle-l5r-3866).
        ["mitsuko-straighten"] = new("Open", "Target an Air Shugenja or a Holding: Straighten it", "air-shugenja-or-holding"),
        ["mitsuko-bow-destroy"] = new("Battle", "Target an enemy Personality: Bow him. Destroy him if he is Kolat, Ninja, or Shadowlands", "enemy-personality", IfTarget: TargetStatus.KolatNinjaOrShadowlands),
        // Shosuro Hawado (oracle-l5r-7103): "Move Hawado home or to a battlefield with one or more enemy
        // units" -- a choice of destinations, so two catalog entries sharing one once-per-turn use. Both
        // pay the same cost (remove a Dynasty card in any discard pile from the game -- the Target) and
        // both work "even if bowed" (the bowed-source exemption list in ValidateAbility). The battlefield
        // half names its destination in command.Province.
        ["hawado-move-home"] = new("Battle", "Even if Hawado is bowed, remove a Dynasty card in any discard pile from the game: Move Hawado home. If she moved, straighten her", "dynasty-card-in-any-discard", UsageKey: "hawado-move"),
        ["hawado-move-battlefield"] = new("Battle", "Even if Hawado is bowed, remove a Dynasty card in any discard pile from the game: Move Hawado to a battlefield with one or more enemy units. If she moved, straighten her", "dynasty-card-in-any-discard", UsageKey: "hawado-move"),
        // Shosuro Rin (oracle-l5r-7151): bow Rin (the cost), then the target's own controller chooses --
        // a "rinShame" PendingChoice, shinden-choice's own shape.
        ["rin-shame"] = new("Open", "Political Open: Bow Rin and target an honorable Personality: Rin shames him in a performance. The Personality's controller may dishonor him. If he did not choose this, bow him and negate his straightening (this turn)", "honorable-personality", ActionKeywords: ["Political"]),
        // Soshi Neiru (oracle-l5r-7360): "You may equip Air Spells to Neiru as an Open action." A
        // card-specific variant of equip-battle -- Open timing instead of Battle, Neiru herself as the
        // fixed wielder (no separate Performer), restricted by RequiredKeywords to Air Spells in hand.
        // Like equip-battle, exempt from the once-per-turn dedupe (no printed limit).
        ["neiru-equip"] = new("Open", "Equip an Air Spell from your hand to Neiru, paying all costs", "spell-in-hand-with-keyword", RequiredKeywords: ["Air"], Cost: GoldCostRule.TargetPrinted()),
        // Sutigu (oracle-l5r-7676): "Bow Sutigu or one of his Weapons" is a cost-choice Performer, the
        // same family as Xijkt's/Tamori Shosei's own -- here Sutigu himself or one of his own unbowed
        // Weapon-keyword attachments.
        ["sutigu-bow"] = new("Battle", "Bow Sutigu or one of his Weapons, and target an enemy card: Negate its straightening (until the turn ends) if it is an attachment. Bow it", "enemy-card",
            Performer: "own-source-or-unbowed-attached-with-keyword", RequiredKeywords: ["Weapon"], CostOptions: [CostOption.Bow]),
        // Shinden Shorai (oracle-l5r-6857, a Region): the fourth caller into the shared "bring a face-up
        // province Personality into play, paying all costs" entry sequence (BringProvincePersonalityIntoPlay).
        ["shinden-shorai-recruit"] = new("Limited", "If it is not your first or second turn: Bring a face-up Personality in this province into play, paying all costs. You may ignore the Personality's Honor Requirement if he is Lion Clan", "own-personality-in-province",
            Cost: GoldCostRule.TargetRecruit(honorWaiverClan: "Lion")),
        // Brawl (oracle-l5r-1127, issue #304): two alternative effects of one Strategy -- the paired-
        // ability shape sharing one UsageKey that Strength of the Divine/Horibe already use. The bow
        // half reuses enemy-card-without-attachments verbatim (Masumi/Unexpected/Jinai). The destroy
        // half is Battle/Open and targets a Region at ANY province or a Terrain -- a new Target
        // category, exempt from the shared "Battle-timing target must be at the current battlefield"
        // Require because a Region sits in Zone.Region, never AtBattle-eligible.
        ["brawl-bow"] = new("Battle", "Target an enemy card without attachments: Bow it", "enemy-card-without-attachments", UsageKey: "brawl"),
        ["brawl-destroy"] = new("BattleOrOpen", "Target a Region (at any province) or a Terrain: Destroy it", "region-or-terrain", UsageKey: "brawl"),
        // Deck search (issue #312). Ikoma Shizuka (oracle-l5r-10292): "Limited: Bow Shizuka: Search your Dynasty or
        // Fate deck. If you find a Region, attach it to one of your provinces, paying all costs. If you find a
        // Terrain, put it in your hand and discard a card." The cost (bow Shizuka) is paid in the execution case; the
        // rest is the SearchSpec -- a deck choice, per-deck criteria (Region only / Terrain only), per-deck effects.
        ["shizuka-search"] = new("Limited", "Bow Shizuka: search your Dynasty or Fate deck", "none", Search: SearchCatalog.IkomaShizuka),
        // Training fixtures for the search criteria vocabulary and the optional flag (see SearchCatalog).
        ["training-search-cheap"] = new("Limited", "Training: search your Fate deck for a cheap non-Unique Follower or Item", "none", Search: SearchCatalog.TrainingCheap),
        ["training-search-focus"] = new("Limited", "Training: search your Fate deck for a Strategy with Focus 2 or less", "none", Search: SearchCatalog.TrainingFocus),
        ["training-search-optional"] = new("Limited", "Training: you may search your Fate deck for a Weapon", "none", Search: SearchCatalog.TrainingOptional),
        // Look at the top N cards (issue #322). Public Records (oracle-l5r-6095, a Singular Holding): "Limited: Bow this card: Look at
        // the top three cards of your Fate deck. Put them back in any order." Reorder only; the bow is the cost.
        ["public-records-look"] = new("Limited", "Bow this card: look at the top three cards of your Fate deck and put them back in any order", "none", Look: LookCatalog.PublicRecords),
        // Isawa Tamaki (oracle-l5r-3918): "Limited: Look at the top card of your Fate deck. You may put it at the bottom of the deck."
        // The scan prints no "Void" prefix (the catalogue's "Void Limited" is a later printing's wording); Void is a plain keyword.
        ["isawa-tamaki-look"] = new("Limited", "Look at the top card of your Fate deck; you may put it at the bottom", "none", Look: LookCatalog.IsawaTamaki),
        // Banish All Doubt (oracle-l5r-690, Tactical Strategy): "Limited: Choose your performing unbowed Tactician: Look at the top four
        // cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of your deck in any order."
        ["banish-all-doubt-look"] = new("Limited", "Choose your performing unbowed Tactician: look at the top four cards of your Fate deck, put one in your hand and the rest on the bottom in any order", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Tactician"], Look: LookCatalog.BanishAllDoubt),
        // Bayushi Kahoku, Experienced 2 (oracle-l5r-10479): "Limited: Look at the top three cards of your Fate deck. You may show one of
        // them that is a Political Strategy and put it in your hand. Put the remaining cards back in any order."
        ["kahoku-exp2-look"] = new("Limited", "Look at the top three cards of your Fate deck; you may show a Political Strategy and put it in your hand; put the rest back in any order", "none", Look: LookCatalog.BayushiKahokuExp2),
        // Training fixtures for the look parameters (see LookCatalog).
        ["training-look-dynasty"] = new("Limited", "Training: look at the top two cards of your Dynasty deck", "none", Look: LookCatalog.TrainingDynasty),
        ["training-look-any"] = new("Limited", "Training: look at the top three cards of any deck", "none", Look: LookCatalog.TrainingAny),
        ["training-look-take-item"] = new("Limited", "Training: look at the top three cards of your Fate deck and take an Item", "none", Look: LookCatalog.TrainingTakeItem),
        ["training-look-fixed"] = new("Limited", "Training: look at the top three cards of your Fate deck; you may discard one; the order is fixed", "none", Look: LookCatalog.TrainingFixedOrder),
        ["training-look-show"] = new("Limited", "Training: look at the top cards of your Fate deck (the count is the caller's) and show one", "none", Look: LookCatalog.TrainingShowAny),
        // Return to Action (oracle-l5r-10953, issue #331; Aftermath): "Discipline :g2: ... Absent Battle: If you are
        // the Defender, target your Personality at home and move him to this battlefield. After he moves, straighten
        // him." The first card on the general Discipline and Absent flags. Discipline: playable from hand (the
        // ordinary free Strategy play) or, paying 2 Gold, from a discard pile and then removed from the game. Absent:
        // the Defender needs no unit at the battlefield (moving one in is the whole point). Defender-only is a per-id
        // Require in ValidateAbility; the target is a new category, own-personality-at-home (bowed or not -- the
        // straighten is part of the card, so a bowed one is legal); the move can be negated ("move" kind in
        // MoveOrStraightenKind), and he is straightened only if he really moved (see the execution case).
        ["return-to-action-move"] = new("Battle", "If you are the Defender, target your Personality at home and move him to this battlefield. After he moves, straighten him", "own-personality-at-home", Discipline: true, Absent: true),
        // Daigotsu Bofana - exp (oracle-l5r-10315, issue #333): "Battle: Target a Dark Virtue in your discard pile: Take an additional action
        // to play it for one of its Battle actions. After it resolves, remove the card from the game." The additional action is an
        // AdditionalActionGrant scoped to the chosen card with FromDiscard (see DiscardPlayGrant); the removal is PlayedFromDiscard.
        // His other printed ability, "After Bofana enters play: Lose 2 Honor and all other players must discard a card...", is his
        // Effect (bofana-entry-discard) plus EntryHonorLoss -- a mandatory entry effect, not an ability here.
        ["bofana-exp-dark-virtue"] = new("Battle", "Target a Dark Virtue in your discard pile: Take an additional action to play it for one of its Battle actions. After it resolves, remove the card from the game", "own-dark-virtue-in-discard"),

        // Training fixtures (issue #338): each copy step on its own, showing the two primitives compose independently of Fukuzo -- keyword only,
        // ability only, and an ability copied for the turn (a Duration other than "Copy" is never replaced by a later use).
        ["training-copy-keyword"] = new("Open", "Training: remove a target Personality in any discard pile from the game: copy one keyword from it", "personality-in-any-discard"),
        ["training-copy-ability"] = new("Open", "Training: remove a target Personality in any discard pile from the game: copy one ability that does not itself copy abilities from it", "personality-in-any-discard", CopiesAbilities: true),
        ["training-copy-ability-turn"] = new("Open", "Training: remove a target Personality in any discard pile from the game: copy one ability that does not itself copy abilities from it (this turn)", "personality-in-any-discard", CopiesAbilities: true),
        // Training fixture (issue #338): opens the three-option Either/Or for the opposing player.
        ["training-either-or-three"] = new("Open", "Training: your opponent chooses to pay 2 Gold, lose 2 Honor, or let you gain 2 Honor", "none"),
        // Fukuzo (oracle-l5r-2728, issue #338): "Open: Remove a target Personality in any player's discard pile from the game: Fukuzo wears
        // his skin. Until the next time this game this ability is used, Fukuzo copies one keyword, and one ability that does not itself
        // copy abilities, from the Personality." The removal is the cost (paid first); the two copies are the two independent copy
        // primitives of CardCopy.cs, composed here. CopiesAbilities: this ability is never itself offered as one to copy.
        ["fukuzo-wear-skin"] = new("Open", "Remove a target Personality in any player's discard pile from the game: copy one keyword, and one ability that does not itself copy abilities, from the Personality", "personality-in-any-discard", CopiesAbilities: true),
        // Yoritomo Sakuma (oracle-l5r-9624, issue #338). "Reaction: When paying a Gold Cost, bow Sakuma: Produce 2 Gold." is an alternate
        // Gold source, not an action: while he is in play and unbowed he can be chosen as a payment source producing 2 (GoldQuotes.cs),
        // and using him that way is what bows him. Its trigger names no window because there is none; the payment IS the moment.
        ["sakuma-produce-gold"] = new("Reaction", "When paying a Gold cost, bow this card: produce 2 Gold", "none", Trigger: "paying-gold-cost", PaymentProduction: 2),
        // "Political Reaction: After another player's Political action targets one of your Personalities, bow Sakuma: The player may pay 2
        // Gold. If he did not, negate the action's (remaining) effects." A before-action-resolves reaction (the in-play targeted-reaction
        // window, BeforeActionReactionApplies); the acting player then gets the general Either/Or choice (EitherOr.cs).
        ["sakuma-political-negate"] = new("Reaction", "After another player's Political action targets one of your Personalities, bow this card: the player may pay 2 Gold. If he did not, negate the action's remaining effects", "none", Trigger: "before-action-resolves", ActionKeywords: ["Political"]),
        // Unfortunate Hesitation (oracle-l5r-9941, issue #338): "Battle/Open: Target a Personality: His controller may choose to bow him. If he
        // did not choose this, he must pass at his next opportunity this phase to take a Battle, Limited, or Open action." The controller's
        // Either/Or (bow him, or take the forced pass -- ForcedPass.cs).
        ["unfortunate-hesitation"] = new("BattleOrOpen", "Target a Personality: his controller may choose to bow him. If he did not choose this, he must pass at his next opportunity this phase to take a Battle, Limited, or Open action", "personality"),
    });
}

public sealed partial class MatchEngine
{
    private static bool InPlay(CardInstance card) => card.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Attached or Zone.Region or Zone.Celestial;
    // Discipline (issue #331): this play of the ability is the DISCARD-PILE mode -- the source sits in a discard pile.
    // The one place that decides it, read by the zone check (ValidateAbility), the price (GoldQuotes.ImplicitGoldRule),
    // the option listing (AbilityActions) and the removal from the game once the action has resolved. A Discipline
    // ability played from anywhere else (a Strategy in hand) is the card's ordinary play: free of the Discipline cost,
    // discarded afterward like any Strategy.
    private static bool DisciplineFromDiscard(AbilityDefinition ability, CardInstance? source) =>
        ability.Discipline && source is { Zone: Zone.FateDiscard or Zone.DynastyDiscard };
    // Tamori Yayu (issue #216): "has ... Cavalry while it is not your turn" -- a LIVE conditional
    // grant, re-evaluated every time (unlike GrantedKeywords, a durable grant that persists once
    // applied); needed an instance method (no longer static) to read _state.ActivePlayerId.
    private bool Keyword(CardInstance card, string keyword)
    {
        if (KeywordRemoved(card, keyword)) return false;
        // Experienced is one levelled keyword: Experienced N satisfies a generic Experienced check.
        if (keyword == "Experienced") return ExperienceLevel(card) > 0;
        if (keyword == "Unaligned" && Definition(card).Type == CardType.Personality && ClanAlignments(card).Length == 0)
            return true;
        var printedOrGranted = (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword)).Contains(keyword, StringComparer.Ordinal);
        // Kitsuki Minori (issue #223): "does not have Overconfident (in and out of play) if any
        // other player is Lion Clan" -- a SUBTRACTIVE conditional, the opposite of Tamori Yayu's own
        // additive one below. A live check, not a one-time flag: Stronghold Clan never changes
        // mid-game in this engine, so the two are equivalent, and this avoids a new persisted field.
        if (printedOrGranted && keyword == "Overconfident" && Definition(card).Effect == "loses-overconfident-vs-lion" && Player(1 - card.OwnerId).Clan == "Lion")
            return false;
        if (printedOrGranted) return true;
        // Isawa Kojiro (oracle-l5r-3850, issue #301): "Kojiro has Naval while he has a Spell" -- the same
        // additive live conditional grant Tamori Yayu's Cavalry below is, re-evaluated on every call so
        // it appears the moment a Spell attaches and disappears the moment it leaves (destroyed,
        // returned to hand, ...), never a durable GrantedKeyword that would outlive the Spell.
        if (keyword == "Naval" && Definition(card).Effect == "naval-while-has-spell")
            return Attachments(card.Id).Any(a => Definition(a).Type == CardType.Spell);
        // Moto Jun-Ni (oracle-l5r-5337, issue #337): "Followers in this unit have Cavalry" -- live too: a Follower has Cavalry exactly while it is
        // attached to a card carrying this Effect, so it appears when it attaches (from hand, or from a discard pile by her own action) and is
        // gone the moment it leaves her unit. Never a durable GrantedKeyword, which would follow it out of her unit.
        if (keyword == "Cavalry" && card.Zone == Zone.Attached && Definition(card).Type == CardType.Follower
            && card.AttachedTo is string hostId && _state.Cards.TryGetValue(hostId, out var host) && Definition(host).Effect == "unit-followers-cavalry")
            return true;
        // Tamori Yayu (issue #216): "has ... Cavalry while it is not your turn" -- a live conditional
        // grant, re-evaluated every time (unlike GrantedKeyword's own durable one-time grant).
        return keyword == "Cavalry" && Definition(card).Effect == "cavalry-force-while-not-your-turn" && card.OwnerId != _state.ActivePlayerId;
    }
    // Shosuro Hawado (issue #301): the card types built from the Dynasty deck (as opposed to the Fate deck's
    // Strategy/Follower/Item/Spell/Ring, or a Stronghold). Only ever asked of a card in a discard pile.
    private static bool IsDynastyCardType(CardType type) => type is CardType.Personality or CardType.Holding or CardType.Event or CardType.Region or CardType.Celestial;
    // Test seam (L5R.Core.Tests has InternalsVisibleTo): the live Keyword() answer for one card, including
    // conditional grants (Isawa Kojiro's Naval, Tamori Yayu's Cavalry) no view or ability surfaces directly.
    internal bool HasKeyword(string cardId, string keyword) => Keyword(Card(cardId), keyword);
    private static bool KeywordRemoved(CardInstance card, string keyword) =>
        card.RemovedKeywords.Contains(keyword, StringComparer.Ordinal)
        || keyword.StartsWith("Experienced", StringComparison.Ordinal)
            && card.RemovedKeywords.Contains("Experienced", StringComparer.Ordinal);
    private static int ExperienceLevel(CardInstance card) => Experienced.Level(
        (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword))
            .Where(keyword => !KeywordRemoved(card, keyword)));
    // Multi-keyword requirement (issue #190): "a Dragon Clan Kensai", "a Crane Clan Magistrate" -- any
    // number of keywords, independently present, order irrelevant. A null/empty list is deliberately
    // false, not vacuously true: every RequiredKeywords-gated category exists specifically because it
    // needs at least one keyword to mean anything (same as the old single-RequiredKeyword convention).
    private bool HasKeywords(CardInstance card, string[]? keywords) => keywords is { Length: > 0 } && keywords.All(k => Keyword(card, k));
    // Printed only, never a granted keyword -- Justice of the Crane's own text draws this exact
    // distinction ("has the PRINTED Magistrate keyword"), since it also grants Magistrate itself and
    // would otherwise always satisfy a plain Keyword() check.
    private static bool PrintedKeyword(CardInstance card, string keyword) => (Definition(card).Keywords ?? []).Contains(keyword, StringComparer.Ordinal);
    // "Unit" (Champion of Thunder / Brothers of Goemon, the general in-play unit-targeting Reaction
    // primitive): a Personality plus every attachment on it (Item, Spell, Follower alike) forms one
    // army entry. A Follower/Item/Spell's own unit is resolved through its host; a Personality's own
    // unit is itself. Mirrors ActingPersonality's own Zone.Attached-to-parent resolution, for a
    // different purpose (identifying the whole unit, not just the acting Personality).
    private string UnitHostId(CardInstance card) => card.Zone == Zone.Attached && card.AttachedTo is string parentId ? parentId : card.Id;
    private IEnumerable<CardInstance> UnitMembers(CardInstance card)
    {
        var host = Card(UnitHostId(card));
        yield return host;
        foreach (var attachment in Attachments(host.Id)) yield return attachment;
    }
    // The general in-play unit-targeting Reaction primitive's own negation signal: MatchState.NegatedEffects,
    // one entry per negation a "before an action resolves" reaction has played for the deferred command
    // (issue #318: several, of different kinds, may now stack on one command). Champion of Thunder's "all"
    // negates every effect an action applies to the chosen card and is deliberately NOT consumed here: a
    // single re-invoked ability can apply more than one effect to the same chosen card (Brothers of
    // Goemon's own worked example -- a Force penalty AND a resulting bow), so "all" stays active for the
    // whole re-invocation and is cleared only once, right after ExecuteAbilityEffects(pending) returns
    // (Apply()'s own "beforeActionReaction" case does this unconditionally). A single-purpose kind ("bow"
    // for Brothers, "destroy"/"move"/"straighten" for Rokai/Tamoko) only ever matches once per
    // re-invocation, so its entry IS consumed immediately here, the original convention those three
    // already established. Entries for different cards, or of different kinds on one card, never interfere;
    // an "all" on a card is checked first, so it wins over (and leaves untouched) any narrower entry there.
    private bool NegatesEffect(CardInstance card, string kind)
    {
        if (_state.NegatedEffects.Count == 0) return false;
        if (_state.NegatedEffects.Any(n => n.CardId == card.Id && n.Kind == "all")) return true;
        var match = _state.NegatedEffects.FirstOrDefault(n => n.CardId == card.Id && n.Kind == kind);
        if (match is null) return false;
        _state.NegatedEffects.Remove(match);
        return true;
    }
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
        // Champion of Thunder / Brothers of Goemon: checked first, ahead of every reason below --
        // unlike those (all either automatic or keyed on whose card this is), this is a genuine
        // per-instance player CHOICE already made in the reaction window that just closed.
        if (NegatesEffect(card, "bow"))
        {
            Emit("bowNegated", $"{Definition(card).Name}'s bowing was negated before it resolved.", card.OwnerId, card.Id);
            return true;
        }
        if (card.OwnerId == actingPlayerId) return false;
        // Togashi Korimi, Experienced (issue #207): "while Korimi is defending, negate her bowing
        // from other players' actions" -- an always-on, unlimited version of the same negation
        // (not the once-per-turn Stalwart keyword itself), gated on defending rather than a
        // per-turn stamp.
        if (Definition(card).Effect == "negate-bow-while-defending" && IsDefendingUnit(card))
        {
            Emit("stalwartNegated", $"{Definition(card).Name} negates the bow while defending.", card.OwnerId, card.Id);
            return true;
        }
        if (!Keyword(card, "Stalwart")) return false;
        if (card.AbilityUses.TryGetValue("stalwart-negate", out var used) && used == _state.TurnNumber) return false;
        card.AbilityUses["stalwart-negate"] = _state.TurnNumber;
        Emit("stalwartNegated", $"{Definition(card).Name}'s Stalwart negates the bow.", card.OwnerId, card.Id);
        return true;
    }
    // Most Personalities have one printed Clan (the CardDefinition.Clan field); a few carry more than
    // one alignment (Clans overrides it when present). "Unaligned" means no Clan affiliation rather
    // than an affiliation of its own. Removing the last affiliation therefore gives the Personality
    // the Unaligned keyword and leaves a Loyal Personality eligible for control by any Clan.
    private static string[] ClanAlignments(CardInstance card) =>
        (Definition(card).Clans ?? [Definition(card).Clan])
            .Where(clan => clan != "Unaligned" && !card.RemovedClanAffiliations.Contains(clan, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
    private bool HasClanAffiliation(CardInstance card, string clan) => ClanAlignments(card).Contains(clan, StringComparer.Ordinal);
    private bool LoyaltyAllowsControl(CardInstance card, string clan) =>
        !Keyword(card, "Loyal") || ClanAlignments(card).Length == 0 || HasClanAffiliation(card, clan);
    // "Attachment": lasts while the granting attachment stays attached (cleared in DestroyAttachment).
    // "Turn": cleared at FinishTurn, the default for a keyword granted by an action's effect.
    // "Permanent": only when the granting card's text says so; never auto-cleared.
    // A card already carrying a keyword (printed or previously granted) gains no redundant second
    // grant of it -- membership is boolean, so a duplicate entry would only ever complicate cleanup
    // (e.g. two Naval-granting attachments on the same Personality, one later detached) for no effect.
    private void GrantKeyword(CardInstance card, string keyword, string source, string duration) { if (!Keyword(card, keyword)) card.GrantedKeywords.Add(new(keyword, source, duration)); }
    // "Protection" (issue #212, Tamori Takeshi): "effects of actions the target performs will not be
    // negated." Checked at the point a negation would otherwise apply -- the same convention
    // StalwartNegatesBow already uses (the negating card/ability is still legal to play; only its own
    // negating effect becomes a no-op) -- rather than as a Require blocking the reacting player from
    // attempting it at all.
    private static bool Protected(CardInstance card) => card.Protections.Count > 0;
    private static void GrantProtection(CardInstance card, string source, string duration) => card.Protections.Add(new(source, duration));
    // An Item/Weapon/Spell has no printed abilities of its own performer requirement -- any action it
    // provides is really performed BY its wielder (AttackDefenseModifier already does the same
    // Zone.Attached-to-parent lookup inline, for a different purpose). Every other card (a
    // Personality, a Stronghold, a played-from-hand Strategy/Ring) performs its own actions directly.
    private CardInstance ActingPersonality(CardInstance card) => card.Zone == Zone.Attached && card.AttachedTo is string parentId ? Card(parentId) : card;
    // "Location" (core rules Glossary, issue #216): there are exactly two kinds -- a battlefield
    // (associated with a province, either player's cards may share one) and Home (associated with a
    // PLAYER, not shared between the two players -- your own Home and your opponent's are different
    // locations even though both are "Zone.Home"). Tamori Ruya's own "at the same location" Target.
    private static bool SameLocation(CardInstance a, CardInstance b) =>
        a.Zone == Zone.Battlefield && b.Zone == Zone.Battlefield && a.ProvinceIndex == b.ProvinceIndex
        || a.Zone == Zone.Home && b.Zone == Zone.Home && a.OwnerId == b.OwnerId;
    // Mirumoto Rokai / Tamori Tamoko (issue #207 follow-up): "before an action resolves, negate one
    // of its own named effects" -- a curated, extendable set of ability ids (the same shape
    // AttackKind's own switch already uses) that destroy/move/straighten their own declared Target,
    // not a fully general interrupt reaching every conceivable ability (that would need touching
    // dozens of individual execution cases for no real card yet). Deliberately excluded, a known
    // scope limit rather than a silent gap: Attacks (the actual destroy happens later, inside the
    // separate deferred-Attack pipeline, not synchronously here), and any ability that moves or
    // straightens its own SOURCE/Performer rather than its Target (michi-move, rikyou-move,
    // houken-exp-straighten, tsudoken-exp-straighten's own second straighten).
    private static bool DestroysTarget(string abilityId) => abilityId is "tetsuo-destroy-weaker-weaponed" or "training-destroy-no-attachments"
        or "elite-sentry-destroy" or "thunderous-legion-destroy" or "volcano-tattoo-destroy" or "ring-of-fire-destroy"
        or "unseen-assassin-strike" or "kojinrue-destroy" or "dairuko-exp-bow" or "yozo-exp-destroy" or "dispensing-justice-destroy" or "kuronada-destroy"
        // The Strength of the Spider (issue #287): conditionally destroys its own target (the
        // "destroy it if it is bowed" branch).
        or "strength-of-the-spider"
        // Hida Mimori / Isawa Mitsuko (issue #301): Mimori destroys her target outright (a Personality
        // target reaches Rokai; an attachment target never does -- see BeforeActionReactionApplies's
        // Personality-only gate); Mitsuko conditionally destroys hers after bowing it.
        or "mimori-destroy" or "mitsuko-bow-destroy"
        // Kuni Shinoda - exp (personality-wave-24): bows, then destroys a Shadowlands target.
        or "shinoda-exp-bow-destroy";
    private static string? MoveOrStraightenKind(string abilityId) => abilityId switch
    {
        "incapacitated" or "favor-battle" or "journeys-end-move" or "shamate-move" or "effortless-counterattack-move"
            or "ring-of-water-move-home" or "mihato-move" or "block-supply"
            // Shinjo Kinto (issue #299): negatable like any other move-home ability -- except when its
            // own acting player has Reconnaissance (MovementCannotBeNegated), checked per command.
            or "kinto-move"
            // Asahina Munefusa (issue #301): "Move him home" -- an ordinary negatable move of its Target.
            or "munefusa-move"
            // Return to Action (issue #331): the first negatable move of its Target TO the battlefield -- its
            // execution case checks NegatesEffect("move") itself (MoveToBattlefield has no such check, since
            // every other move-to-battlefield ability moves its own source/performer, never a Target).
            or "return-to-action-move"
            // Mirumoto Yumaru (issue #337): one of her effects moves her target home, so the movement is negatable like any other.
            or "yumaru-move-home" => "move",
        "air-straighten" or "paid-straighten" or "training-keyword-conditional-straighten" or "immovable-object-straighten"
            or "defensive-stance" or "ritual-preparation-straighten" or "training-straighten-at-battle" or "horibe-straighten"
            or "furu-straighten" or "tsudoken-exp2-straighten" or "tsukasa-straighten" or "atsumichi-straighten"
            or "ring-of-air-straighten" or "mitoshi-straighten"
            // Isawa Mitsuko (issue #301): straightens an Air Shugenja (a Personality, so negatable) or a
            // Holding (never reaches Tamoko -- BeforeActionReactionApplies's kind check is Personality-only).
            or "mitsuko-straighten" => "straighten",
        _ => null
    };
    // Champion of Thunder / Brothers of Goemon's own narrower half (Brothers only ever negates a
    // bow, and must not even offer the reaction when the resolving effect could never bow the
    // target -- "a plain Force penalty with no bow never offers this reaction," the card owner's own
    // worked example): a curated, extendable set of ability ids whose own execution case can reach a
    // StalwartNegatesBow-guarded `target.Bowed = true`, the same "curated, not fully general" shape
    // DestroysTarget/MoveOrStraightenKind above already establish. A conditionally-bowing ability
    // (costly-feud: Force penalty always applies, bow only if the post-penalty Force is 0) still
    // counts as "bows the target" at this ability-id level even though a SPECIFIC instance might not
    // end up bowing -- offering the reaction anyway there is a harmless, unconsumed choice, not a
    // rules violation (Brothers' own negation, if used, still only ever removes a bow that would
    // otherwise have applied, via the NegatesEffect check StalwartNegatesBow now performs first).
    private static bool BowsTarget(string abilityId) => abilityId is "paid-bow" or "unorthodox-attack" or "chokehold" or "immovable-object-bow"
        or "naga-remnants-bow" or "costly-feud" or "block-supply" or "bow-before-my-will" or "surety-of-purpose" or "unexpected-support"
        or "strategic-sacrifice-toggle" or "weakness-of-man-toggle" or "shosuro-ritoru-bow" or "nightingale-blade-bow" or "law-of-darkness-weaken"
        or "training-bow-enemy" or "training-bow-at-battle" or "training-bow-any" or "horibe-bow" or "dairuko-exp-bow" or "furu-bow"
        or "tsudoken-exp-bow" or "masumi-bow" or "tsukasa-bow" or "atsumichi-bow" or "bounty-hunter-bow" or "brothers-great-lake-bow"
        or "soshi-conditional-bow" or "kinaro-exp-bow" or "nubane-bow" or "xijkt-bow" or "kouzei-bow" or "mitoshi-bow" or "jinai-bow"
        or "muzu-bow" or "hiroto-bow" or "noboru-shattered-star-weaken" or "korimi-exp-bow" or "jakuei-bow" or "jakuei-bow-favor" or "ruya-bow"
        // Strategy wave 13 (issue #287): Strength of the Bear and Unexpected both bow their own
        // target unconditionally; The Strength of the Spider bows its target conditionally (only
        // when not already destroyed by its own alternate branch) -- still counts as "bows the
        // target" at this ability-id level, the same "a specific instance might not end up bowing"
        // allowance costly-feud's own doc comment above already establishes.
        or "strength-of-the-bear" or "unexpected" or "strength-of-the-spider"
        // Brawl (issue #304): bows its enemy target unconditionally.
        or "brawl-bow"
        // Fires of Turmoil (issue #295): the same conditional-bow-or-straighten shape as Strategic
        // Sacrifice/Weakness of Man above (here, gated by the discarded Fate card's own Focus parity
        // rather than the target's current bow state) -- still counts as "bows the target."
        or "fires-of-turmoil"
        // Tokoru's Bear (issue #299): bows its own target unconditionally (the destroy-if-attachment
        // half is separate, and independent of whether the bow itself was negated).
        or "tokoru-bear-bow"
        // Bayushi Nori's Personality half, Isawa Mitsuko and Sutigu (issue #301) bow their own target
        // unconditionally. (Nori's attachments half bows the target's ATTACHMENTS, never the target
        // itself, so it is deliberately absent; Rin's bow happens later, after a choice.)
        or "nori-bow-personality" or "mitsuko-bow-destroy" or "sutigu-bow"
        // Personality wave 24: Kakita Kazan and Kuni Shinoda - exp each bow their target (Shinoda's
        // destroy rides on an effect condition only for what follows the bow).
        or "kazan-shame" or "shinoda-exp-bow-destroy"
        // Kitsu Fukui (issue #337): bows an enemy card (the performer's bow-or-destroy is a cost, never the target).
        or "fukui-bow";
    // Whether the command's TargetId is an Attack's OWN target, which reacts through the separate attackReaction
    // window instead of this one: an Attack-classified ability whose Target category is "attack". Bayushi Kahoku
    // - exp (issue #313) is Attack-classified too, but its TargetId is the separate "-4F" target (an ordinary
    // targeted effect, "enemy-personality"); its Attack's own target rides in CardIds. That TargetId IS
    // "an action targets" its card, so the reactions here apply to it -- Champion of Thunder's "all" negating
    // the -4F, Kakihara's bonus -- while the Attack's own target still reaches the attack window.
    private static bool TargetsThroughAttackPipeline(string abilityId) => AttackKind(abilityId) != "" && AbilityCatalog.All[abilityId].Target == "attack";
    // "Before an action resolves" reactions, in play (the user's own clarification: monitored live, never
    // pre-registered). Every one that applies to a just-submitted command, on EITHER player's cards, is a
    // reaction the window may offer -- issue #318 (Mendon's ruling: "if an action opens a reaction window and
    // multiple reactions could be applied, they may all be taken"), replacing the old "the first eligible
    // watcher takes the window". BeforeActionReactionApplies is the one place each card's own printed trigger
    // lives; the window (ResolveOrDeferBeforeActionReaction), its listing (MatchQueries) and ValidateAbility's
    // "before-action-resolves" case all ask it, so eligibility is re-evaluated live at every opportunity.
    // Rokai (a classified destroy) is scoped to "another player's action," "your Personality," and
    // "at Rokai's location"; Tamoko (a classified move/straighten) has no such restriction at all.
    // Champion of Thunder / Brothers of Goemon (the general in-play unit-targeting Reaction
    // primitive): "after another player's action targets a card in this unit" -- broader than either
    // above in one way (not restricted to a Personality target at all -- a Follower/Item/Spell in the
    // unit can be targeted directly too) and narrower in another (only "another player's action,"
    // matching both cards' own printed text). Champion's own "all" is offered for ANY targeted
    // command reaching this choke point, regardless of what kind of effect it turns out to apply;
    // Brothers' own narrower "bow" additionally requires BowsTarget (above) and Ring control, the
    // latter checked here -- BEFORE the reaction is even offered, exactly as the card owner's own
    // ruling specifies. Akodo Kakihara (issue #305): the trigger is only "another player's action targets
    // HIM" -- the affected card itself, never a card elsewhere in his unit. Deliberately excluded, the same
    // "known scope limit, not a silent gap" as Rokai/Tamoko's own Attack exclusion: an Attack-classified
    // ability (AttackKind non-empty) is left to the separate, already-established Attack pipeline
    // (PendingAttack/attackReaction) -- see A Close Call's own narrower negation there instead, reusing
    // that existing window directly.
    // A Reaction is an action (IsAction, ReactionsAreActions.cs): a Reaction that names a card as its target is
    // "another player's action" like any other, and the window it opens suspends the one it was played in.
    private bool BeforeActionReactionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        if (command.AbilityId is null || command.TargetId is null || !_state.Cards.TryGetValue(command.TargetId, out var affected)) return false;
        if (!InPlay(card) || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        var negatable = !MovementCannotBeNegated(command);
        switch (abilityId)
        {
            case "rokai-negate-destruction":
                return negatable && Definition(affected).Type == CardType.Personality && DestroysTarget(command.AbilityId)
                    && card.OwnerId == affected.OwnerId && command.PlayerId != card.OwnerId && SameLocation(card, affected);
            case "tamoko-negate":
                return negatable && Definition(affected).Type == CardType.Personality && MoveOrStraightenKind(command.AbilityId) is not null;
            case "champion-of-thunder-negate":
                return negatable && command.PlayerId != affected.OwnerId && !TargetsThroughAttackPipeline(command.AbilityId)
                    && card.OwnerId == affected.OwnerId && UnitHostId(card) == UnitHostId(affected);
            case "brothers-goemon-negate":
                return negatable && command.PlayerId != affected.OwnerId && !TargetsThroughAttackPipeline(command.AbilityId) && BowsTarget(command.AbilityId)
                    && card.OwnerId == affected.OwnerId && UnitHostId(card) == UnitHostId(affected) && ControlsAnyRing(card.OwnerId);
            // His reaction negates nothing, so "this movement will not be negated" does not stop him reacting
            // to being targeted (unlike every negating reaction above).
            case "kakihara-force-bonus":
                return !TargetsThroughAttackPipeline(command.AbilityId) && card.Id == affected.Id;
            // Yoritomo Sakuma (issue #338): "After another player's Political action targets one of your Personalities" --
            // negatable like the other negating reactions above (a movement that "will not be negated" gives him nothing to
            // react to), the affected card a Personality of HIS controller, the action another player's and Political
            // (ActionCarriesKeyword: the ability's own "Political" tag, or a Political card's own keyword).
            case "sakuma-political-negate":
                return negatable && Definition(affected).Type == CardType.Personality && card.OwnerId == affected.OwnerId && command.PlayerId != affected.OwnerId
                    && !TargetsThroughAttackPipeline(command.AbilityId)
                    && ActionCarriesKeyword(acting, command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var actingSource) ? actingSource : null, "Political");
            // Training fixture (issue #318): any card of the affected card's own controller, against another
            // player's action -- see its execution case.
            case "training-before-action-suppress":
                return command.PlayerId != affected.OwnerId && !TargetsThroughAttackPipeline(command.AbilityId) && card.OwnerId == affected.OwnerId;
            // Training fixture (issue #337): another player's action whose source carries a Weapon -- see its execution case.
            case "training-break-weapon":
                return command.PlayerId != card.OwnerId && command.CardId is not null && Attachments(command.CardId).Any(w => Keyword(w, "Weapon"));
            default:
                return false;
        }
    }
    // Applies AND is actually usable right now (unbowed -- ValidateAbility forbids any ability from a bowed
    // source -- and its once-per-turn use unspent under the ability's own UsageKey). This is what decides
    // whether a player holds an opportunity at all, and what the window listing offers; ValidateAbility
    // remains the authority on a submitted command.
    private bool BeforeActionReactionAvailable(CardInstance card, string abilityId, GameCommand command) =>
        !card.Bowed && BeforeActionReactionApplies(card, abilityId, command)
        && (abilityId != "kakihara-force-bonus" || KakiharaCanReact(card, command.PlayerId))
        && (!card.AbilityUses.TryGetValue(AbilityCatalog.All[abilityId].UsageKey ?? abilityId, out var used) || used != _state.TurnNumber);
    // Every before-action-resolves Reaction printed on an in-play card, on either side, paired with the card.
    private IEnumerable<(CardInstance Card, string AbilityId)> BeforeActionReactionCandidates() =>
        _state.Cards.Values.Where(InPlay).SelectMany(c => AbilityIdsOf(c)
            .Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == "before-action-resolves")
            .Select(id => (Card: c, AbilityId: id)));
    private bool OwnsBeforeActionReaction(int playerId, GameCommand command) =>
        BeforeActionReactionCandidates().Any(x => x.Card.OwnerId == playerId && BeforeActionReactionAvailable(x.Card, x.AbilityId, command));
    // "This movement will not be negated" (issue #299): the printed clause on Wrathful Ancestor and
    // Shinjo Kinto. A negating interrupt (Tamori Tamoko's move-negate, Champion of Thunder's own
    // "all") never even gets its window opened for such a command -- the movement is never offered
    // for negation at all, rather than being offered and then overridden. Wrathful Ancestor is
    // unconditional; Kinto's is conditional on his controller having Reconnaissance at the current
    // battlefield -- whichever side he fights on: an Attacker's grant sits at the Defender's province and
    // a Defender's at their own (issue #309; this note used to say a Defender never qualifies, which the
    // rules lead's Daidoji Yuki ruling overturned). Wrathful Ancestor was previously protected from Tamoko only by omission from
    // MoveOrStraightenKind, which left Champion of Thunder's own "all" negation able to catch it
    // anyway; routing it through this shared check closes that gap too.
    private bool MovementCannotBeNegated(GameCommand command) => command.AbilityId switch
    {
        "wrathful-ancestor-move" => true,
        "kinto-move" => HasReconnaissanceAtCurrentBattlefield(command.PlayerId),
        _ => false
    };
    private bool ControlsAnyRing(int playerId) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Ring);
    // Who has the first opportunity in an alternating reaction window (issue #318, Mendon's ruling: "the
    // player whose turn it is has the first opportunity to play a reaction followed by the other player,
    // alternating until both pass"): the active player when they hold a reaction they could play, otherwise
    // the other player. A player with nothing to play never holds a vacuous first opportunity, so a window
    // whose only reactions belong to one player still opens for that player, exactly as every single-reactor
    // window always did. The alternation itself after that is the engine's existing machinery: a reaction
    // played resets ConsecutivePasses and flips PriorityPlayerId (NextAction), a pass flips it too
    // (PassReaction), and two consecutive passes close the window. Reusable by any other window of this
    // shape (the attack window below, and the battle-timing Engage window, issue #310).
    private int FirstReactionOpportunity(Func<int, bool> canReact) =>
        canReact(_state.ActivePlayerId) || !canReact(1 - _state.ActivePlayerId) ? _state.ActivePlayerId : 1 - _state.ActivePlayerId;
    // Opens `choice` as an alternating reaction window (the shared opening half of that shape; see above).
    private void OpenAlternatingReactionWindow(PendingChoice choice, int firstPlayerId, string message, int? eventPlayerId = null)
    {
        _state.PendingChoice = choice;
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = firstPlayerId;
        Emit("reactionWindow", message, eventPlayerId ?? firstPlayerId);
    }
    // Defers the WHOLE command (never partially applied), the same shape ResolveOrDeferAdditionalAction
    // already uses, so MoveHome/Straighten/DestroyUnits are never even reached until the window has closed --
    // avoiding any need to "undo" an already-applied effect (which would risk double-firing an unrelated
    // hook, e.g. Overconfident's own MarkDead-triggered queue). The window offers EVERY applicable reaction of
    // both players (issue #318); it opens only if at least one is usable right now, and the command
    // re-executes when both players have passed consecutively (Apply()'s "beforeActionReaction" case).
    private bool ResolveOrDeferBeforeActionReaction(GameCommand command)
    {
        if (!OwnsBeforeActionReaction(0, command) && !OwnsBeforeActionReaction(1, command)) return false;
        SuspendOpenWindow();
        _state.PendingNegatableAbility = command;
        OpenAlternatingReactionWindow(new("beforeActionReaction", command.PlayerId, null, "Before this action resolves: either player may use a reaction, or pass."),
            FirstReactionOpportunity(p => OwnsBeforeActionReaction(p, command)), "Before-action reaction window opened.");
        return true;
    }
    // Unimpeachable Name (oracle-l5r-8989): "Before the resolution of another player's action:
    // Reduce each Honor loss from its effects by 3 (minimum 0). You may target and rehonor a
    // dishonorable Personality." An out-of-play Reaction (hand or a discard pile) broader than
    // BeforeActionReactionApplies above -- that one only watches a curated destroy/move/
    // straighten Target on an IN-PLAY card; this one is offered against ANY action. Whether an
    // action "causes an Honor loss" cannot be known before actually resolving it without re-running
    // every effect twice, so -- the same curated, extendable-by-id discipline DestroysTarget/
    // MoveOrStraightenKind above already use, not a fully general prediction -- this lists every
    // ability id whose own execution case contains a literal negative GainHonor call. A controlled
    // Dishonorable Personality is always enough to open the window on its own, independent of this
    // list, since Rehonor does not depend on the specific triggering action at all.
    private static bool AbilityMayCauseHonorLoss(string? abilityId) => abilityId is "blind-rage" or "bow-before-my-will" or "yogo-adi-choice"
        or "training-honor-strike" or "training-honor-loss-entry" or "training-honor-cost-attack" or "training-reconnaissance-strike"
        or "tsudoken-exp2-entry" or "nojima-entry" or "ayumi-entry" or "soshi-conditional-bow" or "unseen-assassin-strike" or "fujimura-expose"
        // Bayushi Nori (issue #301): "If he is dishonorable, his controller loses 2 Honor" -- both halves.
        or "nori-bow-personality" or "nori-bow-attachments";
    private bool ControlsDishonorablePersonality(int playerId) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && c.Dishonorable);
    // Gate for whether the window is even worth opening -- "if [neither a Dishonorable Personality
    // nor an Honor loss] occurs, do not trigger the reaction interrupt" (the user's own ruling).
    private bool UnimpeachableNameAvailable(int reactingPlayerId, string? abilityId) =>
        AnyOutOfPlayReactionAvailable(reactingPlayerId, "before-any-action-resolves") &&
        (ControlsDishonorablePersonality(reactingPlayerId) || AbilityMayCauseHonorLoss(abilityId));
    // Defers the WHOLE command, the same shape ResolveOrDeferBeforeActionReaction/
    // ResolveOrDeferAdditionalAction already use: nothing about the triggering action executes until
    // the reacting player (the opponent of whoever submitted it) answers.
    private bool ResolveOrDeferUnimpeachableName(GameCommand command, string? abilityId)
    {
        var reactingPlayerId = 1 - command.PlayerId;
        if (!UnimpeachableNameAvailable(reactingPlayerId, abilityId)) return false;
        SuspendOpenWindow();
        _state.PendingOutOfPlayActionReaction = command;
        _state.PendingChoice = new("unimpeachableNameReaction", reactingPlayerId, null, "Before this action resolves: you may play Unimpeachable Name, or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = reactingPlayerId;
        Emit("reactionWindow", "Out-of-play action reaction window opened.", reactingPlayerId);
        return true;
    }
    // The Personality actually performing a resolving ability, for Protection's own purposes: an
    // explicit Performer selection (command.PerformerId) when the ability has one, otherwise the
    // source card itself (resolved to its wielder, if it is an attached Item/Weapon/Spell). Used for
    // both an Attack (baked into PendingAttack.SourceId at issue time) and an Additional Action
    // (PendingAdditionalAction IS the original command, so the overload below just looks its own
    // ability up first).
    private CardInstance ActionPerformer(AbilityDefinition ability, GameCommand command, CardInstance? source) =>
        ActingPersonality(ability.Performer is not null ? Card(command.PerformerId!) : source!);
    private CardInstance ActionPerformer(GameCommand command) =>
        ActionPerformer(AbilityCatalog.All[command.AbilityId!], command, command.CardId is null ? null : Card(command.CardId));
    private bool AtBattle(CardInstance card) => _state.CurrentBattleProvince is not null &&
        (card.Zone == Zone.Battlefield || card.Zone == Zone.Attached && Card(card.AttachedTo).Zone == Zone.Battlefield) && card.ProvinceIndex == _state.CurrentBattleProvince;
    // "At any location" (issue #267): a printed presence exemption on a Battle-timing card, but not
    // an unconditional one -- the performer must still be at Home, the current battlefield, or
    // another battlefield that has not yet resolved THIS Battle phase (a Personality left behind at
    // an already-resolved battlefield, or anywhere else -- Zone.Dead, a discard pile, Zone.Region,
    // etc. -- does not qualify). UnresolvedBattlefields already includes the current battle's own
    // province until it actually resolves (see FinishBattleResolution's own Remove), so checking it
    // alone also covers "at the current battlefield" -- no separate CurrentBattleProvince comparison
    // needed.
    private bool AtAnyLocation(CardInstance card) =>
        card.Zone == Zone.Home || card.Zone == Zone.Battlefield && card.ProvinceIndex is int p && _state.UnresolvedBattlefields.Contains(p);
    private bool HasHalls(int owner) => Definition(Card(Player(owner).StrongholdId)).Effect == "halls-memory";
    private bool ControlsKeyword(int owner, string keyword) => _state.Cards.Values.Any(c => c.OwnerId == owner && InPlay(c) && Keyword(c, keyword));
    // Unique/Singular (issue #202): both restrict simultaneous in-play copies of the SAME-NAMED card
    // (any printing -- an Experienced overlay counts as the same card as its base) to one per
    // controller. Unique additionally restricts deck construction (DeckFormat.cs, unaffected here);
    // Singular does not. Checked at every entry-into-play path (Purchase, Equip) that names a card by
    // Name, not DefinitionId, matching the existing Unique convention this generalizes.
    private bool ControlsCopyOf(int owner, string name) => _state.Cards.Values.Any(c => c.OwnerId == owner && InPlay(c) && Definition(c).Name == name);
    // Reconnaissance (core rules Glossary): a per-player boolean at a specific province, defaulted to
    // false, granted by an action/passive/Reaction. It gates only the portion of an ability that
    // depends on it -- the rest of that ability's effect still proceeds regardless of this check.
    // Resets to false only when the battle there resolves (see ResolveBattle).
    // Either player may hold it at ANY battlefield (rules lead's ruling, issue #309: Daidoji Yuki grants it "regardless of
    // whether Yuki's controller is the attacker or the defender", so a Defender holds it at their OWN province -- the
    // engine's earlier assumption that a Defender can only hold it at an enemy province is gone). The real question every
    // reader asks is HasReconnaissanceAtCurrentBattlefield(player); the raw (player, province owner, index) form below is
    // only for the province-index-targeted training grant, and must never be answered with "1 - player" as the owner.
    private bool HasReconnaissance(int playerId, int provinceOwnerId, int provinceIndex) => Player(provinceOwnerId).Provinces[provinceIndex].Reconnaissance[playerId];
    // "You have Reconnaissance at the current battlefield" (Yuki, and every Recon Reaction like him): the battlefield's
    // province row is always the DEFENDER's (a battlefield only ever sits at a defending player's province), whichever
    // player is being granted it -- so the owner is 1 - ActivePlayerId for the Attacker and the Defender alike.
    private void GrantReconnaissanceAtCurrentBattlefield(int playerId)
    {
        if (_state.CurrentBattleProvince is int province) GrantReconnaissance(playerId, 1 - _state.ActivePlayerId, province);
    }
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
    // sourceId (issue #207 follow-up, Togashi Sakata): the performing card itself, needed to read a
    // live token count off it -- optional, since most Attacks here need no such thing.
    private int AttackStrength(string abilityId, PlayerState player, CardInstance? target = null, string? sourceId = null) => abilityId switch
    {
        "carpenter-castle-siege" => 5,
        // Togashi Sakata: "Ranged 4 Attack, with +1 strength for each Sake token on Sakata" -- only
        // Sake tokens count, even though his own Limited grants either Fire or Sake (both +1F alike).
        "sakata-attack" => 4 + (sourceId is not null ? TokenCount(Card(sourceId), "Sake") : 0),
        "shiro-shiba-melee" => 4 + (ControlsKeyword(player.Id, "Shugenja") ? 2 : 0),
        "monastery-fire-attack" => 5,
        "torazu-fire-attack" => 3,
        "unparalleled-sword-attack" => 4,
        // Akodo Chiyo (oracle-l5r-10290, issue #299): "Battle: Melee 4 Attack."
        "chiyo-attack" => 4,
        "shinjo-eun-sahng-attack" => 4,
        "shosuro-konishi-attack" or "tsuruchi-kosoko-attack" => 5,
        "yayu-bow-attack" or "yayu-destroy-attack" => 5,
        "stalking-tiger-attack" => 6,
        "tsuruchi-isas-attack" => 3,
        // Tsuruchi Kosoko - exp: "Ranged 6 Attack". Bayushi Kahoku - exp: "Ranged 3 Attack" (after the -4F).
        "tsuruchi-kosoko-exp-attack" => 6,
        "training-followed-recon-attack" or "training-followed-dishonorable-melee-attack" => 3,
        "kahoku-exp-attack" => 3,
        // Hiruma Nitani: "Ranged 4 Attack, with +2 strength if the target is Shadowlands" -- Clan alignment
        // OR the printed keyword (TargetStatus.IsShadowlands), read off the ability's own IfTarget.
        "hiruma-nitani-attack" => 4 + (EffectConditionMet(abilityId, target) ? 2 : 0),
        "bayushi-kahoku-attack" => 3 + (EffectConditionMet(abilityId, target) ? 2 : 0),
        "asako-ume-attack" => 4,
        "training-honor-cost-attack" => 3,
        "training-weapon-attack" => 2,
        "cavalry-flankers-attack" => 2,
        "dark-naga-scouts-attack" => 3,
        "moshi-ikako-attack" => 1,
        "kamina-exp-attack" => 3 + (target is not null && (HasClanAffiliation(target, "Lion") || Keyword(target, "Unaligned")) ? 1 : 0),
        "shuji-attack" => 4 + (target is not null && HasClanAffiliation(target, "Spider") ? 1 : 0),
        "murderous-thieves-attack" => 3,
        "horse-archers-attack" or "hiruma-sniper-attack" or "tensin-follower-attack" => 4,
        "spawn-of-vritra-attack" => 6,
        "khol-regulars-attack" => 4,
        // Horseback Warriors: Melee 4, or 5 against a target without the Cavalry keyword -- "Infantry"
        // has no printed keyword of its own (core rules Glossary), it just means not Cavalry.
        "horseback-warriors-attack" => 4 + (target is not null && !Keyword(target, "Cavalry") ? 1 : 0),
        // Sankaku-Yari (issue #192): the mirror image of Horseback Warriors above -- bonus FOR
        // Cavalry rather than against non-Cavalry.
        "sankaku-yari-attack" => 4 + (target is not null && Keyword(target, "Cavalry") ? 1 : 0),
        "false-ring-of-water-attack" => 3,
        "shikaro-attack" => 4,
        "moto-ming-gwok-attack" => 5,
        "takasho-attack" => 5,
        "champion-of-thunder-attack" => 6,
        // Nikutai (oracle-l5r-9872, issue #289): its own printed "Battle: Melee 5 Attack" -- missing
        // from this switch since wave 12 wired the shared attack-execution case for it without ever
        // adding its strength/kind here, so it silently resolved as a 0-strength Attack.
        "nikutai-attack" => 5,
        // Legulus's Legion (oracle-l5r-10899): "strength equal to this Follower's Force" -- reads
        // its own performing instance's live Force via sourceId, the same self-referential shape
        // sakata-attack already established (there, a token count; here, CardForce itself).
        "legulus-legion-attack" => sourceId is not null ? CardForce(Card(sourceId)) : 0,
        "village-raiders-attack" => 4,
        // Strength of the Divine (oracle-l5r-10555, issue #287): "Melee Attack with strength equal
        // to his Force or Personal Honor" -- sourceId here is the just-buffed Performer (not the
        // Strategy card itself), the same self-referential live-read shape legulus-legion-attack
        // already established, just reading a separately-chosen card instead of the ability's own
        // source.
        "strength-of-the-divine-force" => sourceId is not null ? CardForce(Card(sourceId)) : 0,
        // Daidoji Ebizo (issue #321): "Ranged Attack with strength equal to Ebizo's Force" -- sourceId is the performing
        // instance (Ebizo himself), read live the way legulus-legion-attack reads its own Force.
        "ebizo-attack" => sourceId is not null ? CardForce(Card(sourceId)) : 0,
        "training-recon-attack" => 2,
        "strength-of-the-divine-honor" => sourceId is not null ? CardPersonalHonor(Card(sourceId)) : 0,
        _ => 0
    };
    // AttackStrength plus every passive defense modifier applying to this specific target (Destrier/
    // Singh Remnants/Elephant Cavalry's shape -- see CardDefinition.AttackDefenseKind). Used at both
    // declare-time validation and the strength baked into PendingAttack, so a Reaction's own further
    // adjustment always stacks on top of this, never bypasses it.
    private int EffectiveAttackStrength(string abilityId, PlayerState player, CardInstance? target, string? sourceId = null) =>
        AttackStrength(abilityId, player, target, sourceId) + (target is null ? 0 : AttackDefenseModifier(target, AttackKind(abilityId)));
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
        "dark-naga-scouts-attack" or "horse-archers-attack" or "hiruma-sniper-attack" or "tensin-follower-attack" => "Ranged",
        "moshi-ikako-attack" => "Ranged",
        "kamina-exp-attack" or "shuji-attack" => "Ranged",
        "yayu-bow-attack" or "yayu-destroy-attack" => "Ranged",
        "spawn-of-vritra-attack" or "khol-regulars-attack" => "Ranged",
        "horseback-warriors-attack" or "sankaku-yari-attack" => "Melee",
        "false-ring-of-water-attack" or "sakata-attack" or "mitsu-fire-attack" => "Ranged",
        "shikaro-attack" or "moto-ming-gwok-attack" or "takasho-attack" => "Ranged",
        "champion-of-thunder-attack" => "Ranged",
        "nikutai-attack" => "Melee",
        "legulus-legion-attack" or "village-raiders-attack" or "chiyo-attack" => "Melee",
        "strength-of-the-divine-force" or "strength-of-the-divine-honor" => "Melee",
        "ebizo-attack" or "training-recon-attack" => "Ranged",
        "kahoku-exp-attack" or "tsuruchi-kosoko-exp-attack" or "hiruma-nitani-attack" or "training-followed-recon-attack" => "Ranged",
        "training-followed-dishonorable-melee-attack" => "Melee",
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
    // relevant (Akodo Kakihara, issue #305): an optional per-(card, ability) narrowing for a Reaction whose
    // OWN trigger condition is cheap to evaluate up front and would otherwise open a pointless window on
    // every Attack while the card sat in play (Kakihara only ever cares about an Attack that targets HIM);
    // every other caller omits it and keeps the existence-only behavior described above.
    private bool AnyInPlayReactionAvailable(string trigger, Func<CardInstance, string, bool>? relevant = null) => _state.Cards.Values.Any(c => InPlay(c) && !c.Bowed &&
        AbilityIdsOf(c).Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == trigger &&
            (!c.AbilityUses.TryGetValue(id, out var used) || used != _state.TurnNumber) && (relevant?.Invoke(c, id) ?? true)));
    // No kind-restricted in-play Reaction exists yet (see AttackKind); once one does, filter by
    // attack.Kind here rather than inside AnyInPlayReactionAvailable itself.
    // A Close Call (the first before-attack-resolves Reaction printed on a Strategy card): unlike
    // every prior reactor here (a Personality/Item/Follower, always InPlay-eligible by construction),
    // a Strategy's own Reaction Timing is legally usable straight from hand too (ValidateAbility's own
    // Zone Require already allows Zone.Hand for a Strategy/Ring), so this gate must also look there --
    // otherwise a hand-only reactor would never be discovered and the window would simply never open,
    // making the card unusable exactly when its own printed text needs it. AnyOutOfPlayReactionAvailable
    // already exists for this (Effortless Counterattack's shape); reused here for both players at once,
    // since an Attack reaction is open to either side, unlike that helper's own single-player callers.
    // Akodo Kakihara (issue #305) is the first in-play before-attack-resolves reactor whose own trigger is
    // "this specific card is the Attack's target": only he is narrowed by the (attacker, target) pair, so a
    // Kakihara sitting in play never opens an Attack window that could not possibly matter to him.
    // Per player (issue #318): who holds a reaction decides who has the first opportunity in the window
    // (FirstReactionOpportunity), so availability is asked of one owner at a time.
    private bool AttackReactionAvailableTo(int playerId, int attackerId, string? targetId) =>
        AnyInPlayReactionAvailable("before-attack-resolves", (c, id) => c.OwnerId == playerId
            && (id != "kakihara-attack-force-bonus" || targetId == c.Id && KakiharaCanReact(c, attackerId)))
        || AnyOutOfPlayReactionAvailable(playerId, "before-attack-resolves");
    private bool AnyAttackReactionAvailable(int attackerId, string? targetId) =>
        AttackReactionAvailableTo(0, attackerId, targetId) || AttackReactionAvailableTo(1, attackerId, targetId);
    // Akodo Kakihara (oracle-l5r-223, issue #305): "Reaction: After another player's action targets
    // Kakihara: Give him a Force bonus equal to his Personal Honor." Whether the reaction is worth offering
    // at all, shared by both windows it can open (BeforeActionReactionApplies's non-Attack branch and
    // AnyAttackReactionAvailable's Attack one): he must be in play and unbowed (ValidateAbility already
    // forbids using ANY ability from a bowed card, so this only avoids opening a window nobody could use),
    // the acting player must not be his own controller ("another player's action"), his one printed use
    // this turn must still be available (the shared "kakihara-force-bonus" UsageKey -- not the per-id key
    // AnyInPlayReactionAvailable checks, since the Attack half is a separate catalog entry), and his
    // CURRENT Personal Honor must be above 0. CardPersonalHonor already yields 0 while he is dishonorable
    // (the card owner's own ruling: a dishonored Kakihara gets +0F, "or no bonus, whichever is more
    // efficient") -- the efficient reading is chosen: no window is opened for a bonus of nothing.
    private bool KakiharaCanReact(CardInstance card, int actingPlayerId) =>
        InPlay(card) && !card.Bowed && card.OwnerId != actingPlayerId
        && AbilityIdsOf(card).Contains("kakihara-force-bonus")
        && CardPersonalHonor(card) > 0
        && (!card.AbilityUses.TryGetValue("kakihara-force-bonus", out var used) || used != _state.TurnNumber);
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
    private void GrantAdditionalAction(int playerId, string[]? scopeCardIds, bool fromDiscard = false)
    {
        if (_state.NoNewAdditionalActionsThisBattle) return;
        _state.AdditionalActions.Add(new(playerId, scopeCardIds, fromDiscard));
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
        // A grant for playing this very card from a discard pile (Daigotsu Bofana - exp) goes first: it is the permission the play
        // was validated against, and an unscoped grant must not be spent in its place.
        var grant = _state.AdditionalActions.Where(g => g.PlayerId == playerId && (g.ScopeCardIds is null || cardId is not null && g.ScopeCardIds.Contains(cardId)))
            .OrderByDescending(g => g.FromDiscard).FirstOrDefault();
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
            // AttackComparedForce is the one decision point for WHICH Force a Personality's Attack
            // strength is compared to (see AttackTargeting.cs) -- identical to CardForce for every target
            // that needs no Follower exception.
            if (InPlay(target) && AtBattle(target) && AttackComparedForce(target) <= strength)
            {
                DestroyAttackTarget(target, playerId);
                if (abilityId == "carpenter-castle-siege") GainHonor(playerId, 2, "Carpenter Castle");
            }
        }
        // Tsuruchi Isas: "Move Isas home" is a second, unconditional effect of the same ability --
        // it happens whether or not the Attack actually hit, so it lives outside the hit branch above.
        if (abilityId == "tsuruchi-isas-attack" && sourceId is not null) MoveHome(Card(sourceId), false);
        // Tsuruchi Yakusho (issue #276 follow-up): this specific Attack's own resolution has now fully
        // completed -- the single funnel every Attack resolves through, deferred or not -- so any
        // immunity granted for it is cleared here, never carried into a later, unrelated Attack.
        _state.RangedAttackStrengthReductionImmune = false;
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
    // strengthOverride (Togashi Mitsu, issue #207 follow-up): his own Attack's strength is a per-
    // command choice (how many of his own Fire tokens the player destroyed just now), not a value
    // AttackStrength's own static per-ability-id formula can express -- bypasses that formula entirely
    // when supplied, same final Strength field every other caller already relies on downstream.
    private bool ResolveOrDeferAttack(string abilityId, PlayerState attacker, string? targetId, string? sourceId = null, bool additional = false, int? strengthOverride = null)
    {
        var strength = strengthOverride ?? EffectiveAttackStrength(abilityId, attacker, targetId is null ? null : Card(targetId), sourceId);
        if (!AnyAttackReactionAvailable(attacker.Id, targetId))
        {
            ResolveAttackNow(abilityId, attacker.Id, targetId, strength, sourceId);
            return false;
        }
        _state.PendingAttack = new PendingAttack { AbilityId = abilityId, PlayerId = attacker.Id, SourceId = sourceId, TargetId = targetId, Strength = strength, Kind = AttackKind(abilityId), Additional = additional };
        // Issue #318 (Mendon's ruling): the player whose turn it is has the first opportunity to react, then the
        // other player, alternating until both pass -- the same rule as the before-action window. Previously the
        // ATTACKED side always went first; that is still what happens whenever the active player has nothing
        // they could react with (the usual case: the attacker is the active player and only reacts with
        // something of their own when they actually hold such a card).
        OpenAlternatingReactionWindow(new("attackReaction", attacker.Id, null, "An attack is pending: react or pass."),
            FirstReactionOpportunity(p => AttackReactionAvailableTo(p, attacker.Id, targetId)), "Attack reaction window opened.", attacker.Id);
        return true;
    }
    // Called once both players pass consecutively in the attackReaction window (Apply()).
    private void ResolveAttack()
    {
        var attack = _state.PendingAttack!;
        _state.PendingAttack = null;
        ResolveAttackNow(attack.AbilityId, attack.PlayerId, attack.TargetId, attack.Strength, attack.SourceId);
        // The deferred Attack is the action's resolution: "after the resolution of your [keyword] action" traits
        // fire now, not when it was announced (ExecuteAbilityEffects returned before its own funnel). The inherited
        // keywords are the ability's source card's (SourceCardId), not the performer's (SourceId).
        FireAfterOwnActionTraits(AbilityCatalog.All[attack.AbilityId], attack.PlayerId, attack.SourceCardId is null ? null : Card(attack.SourceCardId));
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
        // Champion of Thunder's own "all" (issue #278): negates every effect an action applies to the
        // chosen card, including a Force/Chi modifier -- Brothers of Goemon's own narrower "bow"
        // doesn't reach here (a Force penalty still applies in full even when only its resulting bow
        // is negated, the retroactive/partial negation the card itself needs).
        if (NegatesEffect(card, "all"))
        {
            Emit("modifierNegated", $"{Definition(card).Name}'s {stat} modifier was negated before it resolved.", card.OwnerId, card.Id);
            return;
        }
        card.Modifiers.Add(new(source, effect, stat, amount));
        if (stat == "Chi" && Definition(card).Type == CardType.Personality && InPlay(card) && PersonalityChi(card) <= 0) DestroyUnits([card], true);
    }
    private static void RemoveModifiers(CardInstance card, Func<AppliedModifier, bool> match) => card.Modifiers.RemoveAll(m => match(m));
    // "Was that a [keyword] action?" (issue #321): an action carries a keyword when the ability itself prints it before
    // its designator ("Recon Reaction:" -- AbilityDefinition.ActionKeywords) OR the card it is on has the keyword, since
    // "an ability inherits all the keywords of the card it's on" (core rules, Abilities: a Battle ability on a Ninja
    // Personality is a Ninja ability; a Recon Strategy's action is a Recon action). One general question, so Recon,
    // Ninja, Kiho, Iaijutsu, Fire ... can all ask it the same way; Apprentice Shinobi's and Mirumoto Yozo's own
    // older checks (the source card's Ninja keyword; a hardcoded Iaijutsu id list) predate it and are left as they are.
    private bool ActionCarriesKeyword(AbilityDefinition ability, CardInstance? source, string keyword) =>
        ability.ActionKeywords?.Contains(keyword, StringComparer.Ordinal) == true || source is not null && Keyword(source, keyword);
    // Traits of the form "After the resolution of your [keyword] action: Give this card +nF" (Daidoji Ebizo is the first;
    // Master Scouts is the same trait on a Follower), registered by the card's Effect. A TRAIT, not a Reaction (core
    // rules, Traits and effects): mandatory, usable while bowed, not limited to once per turn, and resolving before any
    // Reaction to the same trigger -- so it is applied here, automatically, on EVERY resolution of a matching action of
    // the card's controller, for as long as the card is in play, wherever it is (Home, a battlefield, attached). The
    // stat change is an ApplyModifier, i.e. lasts "until the end of the current turn unless a different duration is
    // stated" (core rules, Duration) and stacks with itself -- the same convention Apprentice Shinobi's own "Give this
    // card +1F" already follows.
    private static readonly Dictionary<string, (string ActionKeyword, string Stat, int Amount)> AfterOwnActionTraits = new(StringComparer.Ordinal)
    {
        // Daidoji Ebizo (oracle-l5r-1629): "After the resolution of your Recon action: Give Ebizo +1F."
        ["force-after-own-recon-action"] = ("Recon", "Force", 1),
    };
    private void FireAfterOwnActionTraits(AbilityDefinition ability, int actingPlayerId, CardInstance? source)
    {
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == actingPlayerId && InPlay(c) && AfterOwnActionTraits.ContainsKey(Definition(c).Effect))
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var (keyword, stat, amount) = AfterOwnActionTraits[Definition(card).Effect];
            if (!ActionCarriesKeyword(ability, source, keyword)) continue;
            Emit("actionTraitFired", $"{Definition(card).Name}: after the resolution of a {keyword} action, +{amount}{(stat == "Force" ? "F" : " " + stat)} (this turn).", card.OwnerId, card.Id);
            ApplyModifier(card, card.Id, Definition(card).Effect, stat, amount);
        }
    }
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
                // Dueling (issue #204): "a reaction to being challenged or entering a duel" -- open
                // to either side, same shape as before-attack-resolves.
                "before-duel-begins" => _state.PendingChoice.Type == "duelReaction",
                // Reaction to a duel's own resolution (issue #209): a duel is a single, complete
                // action from challenge to consequences -- once it fully resolves (win/loss,
                // outcomes, Focus Effects all applied), the card that WON it may react, the same
                // "after entering play" shape own-enters-play already uses (scoped to one specific
                // card by id), not open to either side the way before-duel-begins is.
                "own-won-duel" => _state.PendingChoice.Type == "duelWonReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                // Kakita Maratai (issue #299): "After a duel ends that your Personality won" -- the
                // same duelWonReaction window as own-won-duel above, but NOT scoped to the winning
                // card itself (no CardId match): any of the winner's own controller's cards carrying
                // this trigger may react, the same broadcast shape any-personality-enters-play already
                // gives own-enters-play. Whose window it is (PendingChoice.PlayerId) is always the
                // winner's own controller, so only that player's own watchers ever match.
                "own-personality-won-duel" => _state.PendingChoice.Type == "duelWonReaction" && command.PlayerId == _state.PendingChoice.PlayerId,
                // Togashi Hizumi / Tamori Ruya (issue #216): both scoped to the one specific watching
                // card by id, the same shape own-won-duel already uses -- see OpenBowReactionWindow
                // for how each is actually distinguished when opening the window.
                "bowed-personality-at-own-battlefield" or "own-action-bowed-personality" => _state.PendingChoice.Type == "bowReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                // Mirumoto Rokai / Tamori Tamoko (issue #207 follow-up): both scoped to the one
                // specific watching card by id, the same shape own-won-duel/bowed-personality-at-
                // own-battlefield already use.
                // Open to either player (issue #318): any card of the acting player's own whose printed trigger
                // applies to the deferred command right now (BeforeActionReactionApplies), not one watcher.
                "before-action-resolves" => _state.PendingChoice.Type == "beforeActionReaction" && _state.PendingNegatableAbility is { } deferredAction
                    && command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var reactingCard)
                    && BeforeActionReactionApplies(reactingCard, id, deferredAction),
                // Unimpeachable Name (oracle-l5r-8989): out-of-play, like any-personality-enters-play
                // above -- no CardId match (it is not watching a specific OTHER card's own entry, it
                // is reacting to an action), just the reacting player's own id.
                "before-any-action-resolves" => _state.PendingChoice.Type == "unimpeachableNameReaction" && command.PlayerId == _state.PendingChoice.PlayerId,
                // Stormless Fury (issue #276 follow-up): both its out-of-play attach half and its
                // in-play bow half share this one trigger, scoped to the one specific offered card by
                // id -- same shape as own-won-duel/bowed-personality-at-own-battlefield above.
                "after-battle-action-resolves" => _state.PendingChoice.Type == "battleActionReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action"
                // primitive's own window, same single-watcher shape.
                "own-ranged-action-announced" => _state.PendingChoice.Type == "announceReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                // Legion of the Bat / Nikutai (issue #279): both scoped to the one specific watching
                // card by id, the same shape as every other single-card Reaction window above -- see
                // OpenMoveReactionWindow/OpenNikutaiReactionWindow for how each is actually opened.
                "moved-enemy-infantry-to-own-battlefield" => _state.PendingChoice.Type == "moveReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                "sibling-follower-bowed" => _state.PendingChoice.Type == "nikutaiReaction"
                    && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                // Battle Phase timing (issue #310): "After engaging" (any battlefield) and "After engaging at X's
                // battlefield" -- open to either player for as long as the Engage window is (the top-level
                // priority check already enforces whose turn it is to react or pass), the same broadcast shape
                // before-attack-resolves has. The location half of the second is checked below (AtBattle(source)).
                "after-engaging" or "after-engaging-at-own-battlefield" => _state.PendingChoice.Type == "engageReaction" && _state.PendingEngage is not null,
                // "Before a battle resolves" (Steady Resolve, Akodo Dairuko): the one Reaction the before-resolution set is currently asking its owner about.
                "before-battle-resolves" => _state.PendingChoice.Type == "beforeBattleReaction" && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId,
                _ => false
            },
            _ => false
        }, $"{ability.Timing} timing is unavailable now.");
        // Battle Phase timing (issue #310): the Phase is already Battle while the Engage window is open, but no Battle
        // action may be taken until it closes (spec 4.2 precedes 4.5) -- only the Engage/"after engaging" Reactions are legal.
        Require(_state.PendingChoice?.Type != "engageReaction" || ability.Timing == "Reaction" && ability.Trigger is "after-engaging" or "after-engaging-at-own-battlefield",
            "Until the Engage window closes, only Engage actions and \"After engaging\" Reactions may be played.");
        // ...and a one-shot limit on the first-action holder's FIRST Battle action.
        CheckFirstActionRestriction(command);
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
            Require(source.OwnerId == player.Id && AbilityIdsOf(source).Contains(id), "This card does not provide that ability to you.");
            // A Strategy source is normally used straight from hand (the usual one-shot pattern), but
            // a Terrain stays in play afterward (Zone.Battlefield) -- its own abilities, including an
            // entering-play Reaction, must still be usable from there. Singh Remnants' shape is the
            // opposite of ordinary: its whole point is reacting from OUTSIDE play (hand or discard).
            // Stormless Fury (issue #276 follow-up): "after-battle-action-resolves" covers BOTH an
            // out-of-play half (stormless-fury-attach, Singh Remnants' own out-of-play shape) and an
            // ordinary in-play half (stormless-fury-bow) sharing one trigger -- broadened to accept
            // either zone here, narrowed to the exact one each half actually needs by its own bespoke
            // Require below (source.Zone==Hand for the attach half; AtBattle, which implies InPlay,
            // for the bow half).
            // Fading Shadows (issue #337): an ability that attaches its OWN card is used from the hand -- and only from there (AttachSpec.Self).
            Require(ability.Attach?.Card == AttachSource.Self ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id)
                : ability.Trigger is "any-personality-enters-play" or "before-any-action-resolves" ? source.Zone is Zone.Hand or Zone.FateDiscard
                : ability.Trigger == "after-battle-action-resolves" ? source.Zone is Zone.Hand or Zone.FateDiscard || InPlay(source)
                // A Ring (issue #194) may use its printed action from hand too -- "you may discard
                // this Ring from your hand to use its ability without cost" -- in addition to the
                // ordinary in-play case once recruited (ExecuteAbilityEffects' own tail discards it
                // afterward when used this way, mirroring the Strategy case just below).
                // A card in a discard pile is a legal source only as a Discipline play, or as the Battle action a granted additional
                // action names (Daigotsu Bofana - exp): PlayableFromDiscard.
                : def.Type is CardType.Strategy or CardType.Ring ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || InPlay(source) || PlayableFromDiscard(ability, source, player.Id) : InPlay(source) || PlayableFromDiscard(ability, source, player.Id),
                "The ability's source is not in the required zone.");
            // "Tireless"/"Even if bowed" (issue #198): Akodo Tsudoken's own two Experienced printings.
            // Dragon Clan review (issue #207) adds three more real "even if bowed" cards. Nikutai
            // (issue #279) adds a fourth: "Even if this card is bowed, after a Follower in this unit
            // bows: Straighten it."
            Require(!source.Bowed || id is "shinden-choice" or "tsudoken-exp-bow" or "tsudoken-exp-straighten" or "tsudoken-exp2-straighten"
                or "houken-exp-straighten" or "michi-move" or "rikyou-move" or "fubatsu-plate-straighten" or "fubatsu-plate-move" or "nikutai-straighten"
                // Shosuro Hawado (issue #301): "Battle: Even if Hawado is bowed, ..." -- both destination halves.
                or "hawado-move-home" or "hawado-move-battlefield"
                // Akodo Dairuko (oracle-l5r-200): "Tireless Reaction" -- the action may be taken even while the card is bowed.
                or "dairuko-rally", "Abilities on bowed cards cannot be used.");
            uses = source.AbilityUses;
        }
        // "Any number of times per turn" (Tactical Advantage, Equip (Battle)), and "tireless" (Ashigaru
        // Elite -- explicitly stacks with other uses of the action -- and Elite Sentry's straighten):
        // exempt from the once-per-turn dedupe entirely -- each one's own cost/requirements (a
        // different unbowed Farm each time, for Ashigaru Elite) are the only real limit.
        var usageKey = ability.UsageKey ?? id;
        Require(id is "tactical-advantage" or "equip-battle" or "training-ashigaru-boost" or "ashigaru-elite-boost" or "elite-sentry-straighten" or "kharmic-discard"
            // Soshi Neiru (issue #301): "You may equip Air Spells to Neiru as an Open action" carries no
            // once-per-turn limit of its own -- equip-battle's own convention; each equip's Gold cost and
            // the Armor/Weapon caps are the only real limits.
            or "neiru-equip"
            // Naval Invasion (issue #310): "once per BATTLE" -- a fresh use every battle, so the once-per-turn
            // dedupe would be wrong; PendingEngage.NavalInvasionUsed enforces the printed limit instead.
            or "naval-invasion"
            // "Repeatable" (issue #194): Ring of Air/Water's own printed keyword on their in-play
            // actions, same convention as Tactical Advantage/Equip (Battle) above.
            or "ring-of-air-straighten" or "ring-of-water-move-home"
            || !uses.TryGetValue(usageKey, out var used) || used != _state.TurnNumber && id != "border-game", "This ability has already been used within its limit.");
        if (id == "border-first") Require(player.TurnsTaken == 1, "This Border Keep ability is only available on your first turn.");
        // Battle/Open presence primitive (issue #267): a printed "Battle/Open" ability carries no
        // battlefield-presence requirement at all when used as its OPEN half (performed from Home,
        // core rules) -- but the SAME ability, used during the Battle phase, is a genuine Battle
        // action and needs exactly the same presence rule an ordinary "Battle"-Timing ability already
        // has. actingAsBattle is that single condition, reused everywhere presence is enforced below
        // (the blanket army-presence/source-AtBattle Requires here, the Target-AtBattle Require
        // further down, and every AtBattle-checking Performer category) instead of the literal
        // Timing=="Battle" string. For an ordinary "Battle"-Timing ability this is unconditionally
        // equivalent to the old check (Timing=="Battle" already implies Phase==Battle, checked above),
        // so no existing card's behavior changes.
        var actingAsBattle = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
        if (actingAsBattle)
        {
            // Absent (issue #331; Coils of Madness rule 9): the action "may be taken even without controlling any
            // unit at the current battlefield" -- an ability that PRINTS that (Absent Battle, or Ring of Earth's
            // "Even if you control no units at the current battlefield") carries AbilityDefinition.Absent and
            // needs neither the acting player's presence nor its own source card at the battlefield. It stays a
            // Battle action in every other respect (Battle phase only; its Target/Performer keep their own
            // location rules -- the shared Target-AtBattle Require below is not waived by it).
            //
            // The id list that follows is the older, narrower thing this flag did NOT absorb: self-movers that
            // "reinforce" a battle from elsewhere by an engine-side ruling rather than a printed Absent (see
            // docs/decisions/2026-09-19-discipline-and-absent-flags.md for why each stayed).
            //
            // Mirumoto Michi / Togashi Rikyou (issue #207): reinforcing a battle from elsewhere
            // entirely -- the whole point is moving in, so neither the acting player's own existing
            // presence nor this card's own AtBattle status can be required the normal way. Mirumoto Houken,
            // Experienced (issue #267's own actingAsBattle generalization surfaced this): his own "move to
            // the current battlefield if he would be opposed there" is the exact same "the resolution moves
            // the performer there" shape -- previously masked entirely, since a BattleOrOpen ability got
            // no presence check of any kind before this issue.
            var reinforcesFromElsewhere = ability.Absent || id is "michi-move" or "rikyou-move" or "takasho-move" or "intervening-spirit-move" or "houken-exp-straighten"
                // Sanctuary of the Waves (issue #295): its default target IS the wielder himself, whose
                // whole point is moving INTO the battle -- the acting player is never assumed to already
                // have presence there, same reasoning as Intervening Spirit's own membership above.
                or "sanctuary-of-the-waves-move"
                // Your Clan Needs You! (issue #295): brings a fresh Personality straight from a
                // province into the current battlefield -- the acting player may have zero presence
                // there beforehand (that is the entire point of reinforcing).
                or "your-clan-needs-you"
                // Utaku Mai (issue #299): "You may move Mai to the current battlefield" -- the acting
                // player is never assumed to already have presence there, and Mai herself need not be
                // there yet either (only the TARGET must be, per the shared Target-AtBattle Require).
                or "mai-move-force-penalty"
                // Asahina Munefusa (issue #301): "Battle: Even if Munefusa is not at the current
                // battlefield, target an attacking Personality: Move him home" -- the acting player
                // may have no presence there at all (a Defender who left the province undefended), and
                // Munefusa herself need not be there (only the TARGET must be, per the shared
                // Target-AtBattle Require below). Same reading Sanctuary of the Waves/Mai already take.
                or "munefusa-move";
            if (!reinforcesFromElsewhere) Require(Army(player.Id, _state.CurrentBattleProvince!.Value).Any(), "You need a unit at the current battlefield.");
            if (!reinforcesFromElsewhere && source is not null && Definition(source).Type == CardType.Personality)
                Require(AtBattle(source) || HasHalls(player.Id) && Keyword(source, "Shugenja"), "The performing card must be at this battlefield.");
            // The check above only ever applies to a Personality-type source -- a Spell/Item's own
            // Battle ability needs its own equivalent: AtBattle(source) already resolves an ATTACHED
            // card's own parent correctly (same helper Khol Regulars' own Reaction uses), so this is
            // exactly "the attachment's own wielder is at this battlefield."
            if (id is "scouring-flood-move-home" or "earths-hunger-destroy" or "nightingale-blade-bow" or "fubatsu-plate-straighten" or "fubatsu-plate-move" or "earth-flows-move")
                Require(AtBattle(source!), "This attachment's own wielder must be at the current battlefield.");
            // Overgrown Grove (issue #264): a Region sits in Zone.Region, never AtBattle-eligible at
            // all (its own Zone check always fails) -- needs its own province-index comparison instead.
            if (id == "overgrown-grove-move")
                Require(source!.ProvinceIndex == _state.CurrentBattleProvince, "Overgrown Grove only affects a battle at its own province.");
            // Trusting Instinct (oracle-l5r-10402, issue #289): "Move him to a battlefield where he
            // would be opposed" -- this engine resolves one battle at a time, so "a battlefield" is
            // always the current one (Houken, Experienced's own "if he would be opposed there" shape);
            // the destination must actually have enemy presence for the move to be legal at all.
            if (id == "trusting-instinct-move")
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                    "Trusting Instinct only moves your Personality to a battlefield where he would be opposed.");
            // Your Clan Needs You! (issue #295): "If any enemy units are at the current battlefield" --
            // its own printed entry condition, checked here alongside every other id-specific presence
            // Require in this same actingAsBattle block.
            if (id == "your-clan-needs-you")
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                    "Your Clan Needs You! requires enemy units at the current battlefield.");
            // Shosuro Hawado (issue #301): "Move Hawado ... to a battlefield with one or more enemy
            // units" -- the destination is a per-command choice (command.Province, the same field
            // enemy-province/any-province targets already use): any battlefield not yet resolved this
            // Battle phase (the current one included) where the enemy actually has a unit. "Home" names
            // no battlefield at all.
            if (id == "hawado-move-battlefield")
                Require(command.Province is int hawadoDestination && _state.UnresolvedBattlefields.Contains(hawadoDestination) && Army(1 - player.Id, hawadoDestination).Any(),
                    "Choose a battlefield with one or more enemy units.");
            else if (id == "hawado-move-home")
                Require(command.Province is null, "Moving home names no battlefield.");
        }
        // Return to Action (oracle-l5r-10953, issue #331): "If you are the Defender" is a condition on taking the
        // action at all (it sits before the effects colon), so it is checked here where every offer and every
        // submitted command see it. Absent waived the presence Requires above; there must still be a battle to move
        // him to.
        if (id == "return-to-action-move")
        {
            Require(_state.CurrentBattleProvince is not null, "There is no current battlefield to move him to.");
            Require(player.Id != _state.ActivePlayerId, "Only the Defender may use Return to Action.");
        }
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle" or "jakuei-bow-favor")
            Require(_state.FavorPlayerId == player.Id, "You must control the Imperial Favor to discard it.");
        if (id == "lobby") Require(player.Honor > Player(1-player.Id).Honor, "Lobby requires strictly higher Family Honor than your opponent.");
        if (id == "tamoko-open") Require(_state.ActivePlayerId != player.Id, "Only usable when it is not your own turn.");
        // Ohsuki's Sparrow (oracle-l5r-10023, issue #299): "If none of your provinces were destroyed
        // since your last turn ended" -- a printed condition on USING the action, checked here rather
        // than only when it resolves (MatchState.ProvinceDestroyedSinceTurnEnd).
        if (id == "ohsuki-sparrow-honor")
            Require(!_state.ProvinceDestroyedSinceTurnEnd[player.Id], "One of your provinces was destroyed since your last turn ended.");
        if (id == "shosei-honor") Require(_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Keyword(c, "Temple")), "You must control a Temple.");
        // Ring recruitment (issue #194): each of these three is only ever playable as the specific
        // Additional Battle Action already granted for it (GrantRingRecruit) -- never as an ordinary
        // ability -- since the underlying historical condition (Favor actions this turn, a Terrain
        // played/destroyed this battle, an opposing unit ever present in a won battle) is not itself
        // re-checked here. This also naturally makes the grant, not a separate flag, the single
        // source of truth for "was this satisfied."
        if (id is "ring-of-air-recruit" or "ring-of-water-recruit" or "false-ring-of-air-recruit" or "ring-of-earth-recruit" or "ring-of-fire-recruit" or "false-ring-of-earth-recruit")
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
            Require(EffectiveProvinceStrength(Player(1 - _state.ActivePlayerId).Provinces.Single(p => p.Index == _state.CurrentBattleProvince!.Value)) >= 11,
                "The current battlefield's Province Strength must be 11 or greater.");
        if (id == "kano-destroy-token") Require(source!.Tokens.Any(t => t.Type == "Master"), "Akodo Kano needs a Master token.");
        if (id == "dairuko-rally") Require(AtBattle(source!), "Akodo Dairuko must be at the battlefield.");
        // Torazu's own ability, on Torazu, needs no separate Performer -- the shared Battle presence
        // check above already requires the source itself (a Personality) to be at this battlefield.
        if (id == "torazu-fire-attack") Require(HasToken(source!, "Fire"), "Torazu needs a Fire token of his own to attack.");
        if (id == "moto-ming-gwok-attack") Require(DeadCount(0) > 0 || DeadCount(1) > 0, "Moto Ming-Gwok needs a dead Personality in some player's discard pile to attack.");
        if (id == "archery-range-reaction") Require(_state.PendingAttack!.Kind == "Ranged", "Archery Range only boosts a Ranged Attack.");
        if (id == "flesh-cutter-arrows-reaction")
            Require(_state.PendingAttack!.Kind == "Ranged" && _state.PendingAttack.SourceId == Card(source!.AttachedTo!).Id
                && !Attachments(_state.PendingAttack.TargetId!).Any(a => Keyword(a, "Armor")), "Flesh Cutter Arrows needs a Ranged Attack from its own wielder, targeting a card without an attached Armor.");
        if (id == "slanderer-dishonor") Require(ControlsKeyword(player.Id, "Courtier") || ControlsKeyword(player.Id, "Magistrate"), "Slanderer needs a Courtier or Magistrate in play.");
        if (id is "horibe-bow" or "horibe-straighten")
            Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Horibe must be opposed to use this ability.");
        if (id is "heavy-chargers-boost") Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Heavy Chargers must be opposed to use this ability.");
        // Khol Regulars: "If this card is at the current battlefield, after a Ranged Attack is
        // targeted" -- both narrow the generic before-attack-resolves Reaction window this card shares
        // with every other attack-boosting Reaction.
        if (id == "khol-regulars-reaction") Require(AtBattle(source!) && _state.PendingAttack!.Kind == "Ranged", "Khol Regulars must be at the current battlefield, reacting to a Ranged Attack.");
        // Akodo Kakihara (issue #305): "After another player's action targets Kakihara" -- narrows each of his
        // two windows to the deferred action/Attack that actually names HIM as its target and was issued by
        // someone other than his own controller. The Reaction is only ever worth using while his CURRENT
        // Personal Honor is above 0 (0 while dishonorable -- the card owner's own ruling: no bonus).
        if (id == "kakihara-force-bonus")
            Require(_state.PendingNegatableAbility is { } kakiharaAction && kakiharaAction.TargetId == source!.Id && kakiharaAction.PlayerId != source.OwnerId,
                "Akodo Kakihara only reacts to another player's action that targets him.");
        if (id == "kakihara-attack-force-bonus")
            Require(_state.PendingAttack is { } kakiharaAttack && kakiharaAttack.TargetId == source!.Id && kakiharaAttack.PlayerId != source.OwnerId,
                "Akodo Kakihara only reacts to another player's Attack that targets him.");
        if (id is "kakihara-force-bonus" or "kakihara-attack-force-bonus")
            Require(CardPersonalHonor(source!) > 0, "Akodo Kakihara has no Personal Honor to convert into a Force bonus.");
        // A Close Call: "after a Personality with an Armor is targeted by a Melee or Ranged Attack" --
        // both the Kind restriction and the Armor-attached restriction narrow the shared
        // before-attack-resolves Reaction window (the same shape khol-regulars-reaction/flesh-cutter-
        // arrows-reaction above already use), checked before the reaction is even allowed to fire.
        if (id == "a-close-call-negate")
            Require(_state.PendingAttack!.Kind is "Ranged" or "Melee" && Attachments(_state.PendingAttack.TargetId!).Any(a => Keyword(a, "Armor")),
                "A Close Call needs a Ranged or Melee Attack targeting a Personality with an Armor attached.");
        // Strong Defenses / Trusting Instinct (issue #289): "After a Ranged or Melee Attack is
        // targeted" -- A Close Call's own Kind restriction again, minus its Armor-attached gate (no
        // such restriction printed on either of these), each played straight from hand like A Close
        // Call itself (AnyAttackReactionAvailable's own out-of-play discovery already covers a Strategy
        // card's Reaction, per its own doc comment).
        if (id is "strong-defenses-reaction" or "trusting-instinct-reaction")
            Require(_state.PendingAttack!.Kind is "Ranged" or "Melee", "This only reacts to a Ranged or Melee Attack.");
        // Tsuruchi Yakusho (issue #276 follow-up): every existing Attack-strength-reducing Reaction is
        // blocked outright while her own immunity flag is set for this specific (Ranged) Attack's own
        // resolution -- covers both current reduction sites (Fallow Lands' -2, Training's own full
        // negate) generically, so a future reducing card only needs to be added to this same id list.
        // Strong Defenses' -3 and Trusting Instinct's -2 (issue #289) are exactly that future case.
        if (id is "fallow-lands-reaction" or "training-attack-negate" or "strong-defenses-reaction" or "trusting-instinct-reaction")
            Require(!(_state.PendingAttack!.Kind == "Ranged" && _state.RangedAttackStrengthReductionImmune),
                "This Ranged Attack is immune to strength reduction for the remainder of its resolution.");
        // Fallow Lands: unlike an ordinary Zone.Battlefield/Attached Reaction card, AtBattle() does not
        // apply -- it never moves, it just sits at its own home province forever. "The province to which
        // it is attached" is under attack only when the CURRENT active player is someone else (their own
        // opponent, i.e. the attacker) attacking that exact province index of Fallow Lands' own
        // controller -- province indices are not globally unique across players, so both halves of this
        // check are needed. "The controlling player has presence" reuses Army() (a Region itself never
        // satisfies presence, since it never sits in Zone.Battlefield).
        if (id == "fallow-lands-reaction")
            Require(_state.CurrentBattleProvince is not null && _state.ActivePlayerId != source!.OwnerId && _state.CurrentBattleProvince == source.ProvinceIndex
                && Army(source.OwnerId, source.ProvinceIndex!.Value).Any(),
                "Fallow Lands may only react to an Attack at its own province, while its controller has presence there.");
        if (id == "cavalry-flankers-attack") Require(player.Id == _state.ActivePlayerId, "Only the Attacker may use this ability.");
        if (id == "hiruma-sniper-attack") Require(HasReconnaissanceAtCurrentBattlefield(player.Id), "This action requires Reconnaissance at the current battlefield.");
        // Stormless Fury's own first Reaction: "choose your performing Thunder Shugenja" -- the just-
        // resolved action's own performer (BattleActionReactionPerformerId) must be a Thunder Shugenja
        // belonging to the SAME player who holds this card in hand; the card itself must still
        // actually be in hand (a copy already attached carries this ability id too, but offers only
        // stormless-fury-bow -- see FindBattleActionReactionCandidate).
        if (id == "stormless-fury-attach")
        {
            var performer = Card(_state.BattleActionReactionPerformerId!);
            Require(source!.Zone == Zone.Hand, "Stormless Fury may only attach itself from your hand.");
            Require(performer.OwnerId == player.Id && Keyword(performer, "Thunder") && Keyword(performer, "Shugenja"),
                "Stormless Fury only attaches to your own performing Thunder Shugenja.");
        }
        // Stormless Fury's own second Reaction: must be attached and at the current battlefield, and
        // -- the card owner's own errata ruling -- may not trigger against the same action instance
        // that triggered this copy's own stormless-fury-attach half.
        if (id == "stormless-fury-bow")
            Require(AtBattle(source!) && !_state.BattleActionReaction1UsedCardIds.Contains(source!.Id),
                "Stormless Fury must be at the current battlefield, and may not react against the same action that just attached it.");
        if (id == "tsuruchi-yakusho-immunity") Require(AtBattle(source!), "Tsuruchi Yakusho must be at the current battlefield.");
        // Battle Phase timing (issue #310): the Engage window's own per-trigger and per-card conditions.
        // "After engaging at X's battlefield": X's own battlefield is the one it is assigned to.
        if (ability.Trigger == "after-engaging-at-own-battlefield")
            Require(AtBattle(source!), "This card must be assigned to the battlefield being engaged.");
        // Daidoji Yuki: "if all your Personalities there are Scouts" sits BEFORE the effects colon, so it is a
        // requirement to take the action (core rules, constraints block), not a condition on part of the effect.
        if (id == "yuki-engage-recon")
            Require(Army(player.Id, _state.PendingEngage!.ProvinceIndex).All(c => Keyword(c, "Scout")), "All your Personalities at this battlefield must be Scouts.");
        // Naval Invasion: "if you are the Attacker and your current army has more Naval cards than the side opposing it".
        if (id == "naval-invasion")
        {
            var engage = _state.PendingEngage!;
            Require(player.Id == engage.Attacker, "Only the Attacker may use Naval Invasion.");
            Require(!engage.NavalInvasionUsed, "Naval Invasion may be used once per battle.");
            Require(NavalCardCount(engage.Attacker, engage.ProvinceIndex) > NavalCardCount(engage.Defender, engage.ProvinceIndex),
                "Your current army must have more Naval cards than the side opposing it.");
        }
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
                // Lady Matsu's Rage's own printed restriction: "your performing unbowed ATTACKING
                // Personality" -- narrower than own-unbowed-personality-at-battle, which allows either
                // side of the battle.
                "own-unbowed-attacking-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) && IsAttacking(performer),
                "own-unbowed-samurai-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) && Keyword(performer, "Samurai"),
                // Generic "Choose your performing [Keyword(s)] Personality" at battle -- for any card
                // naming one or more keywords with no mechanical rule of their own (all independently
                // required, order irrelevant -- see AbilityDefinition.RequiredKeywords), without adding
                // a new hardcoded Performer category each time. Volcano Tattoo's "Tattooed Monk" is
                // exactly this with two keywords instead of one. Battle/Open presence primitive (issue
                // #267): "at battle" here means "at battle, PROVIDED this ability is actually acting as
                // a Battle action right now" -- actingAsBattle is false for a BattleOrOpen ability's own
                // Open half, so this reduces to a plain unbowed-and-keyworded check there (performed
                // from Home, no presence at all), exactly The Weakness of Man's own printed shape. For
                // every existing "Battle"-only card this is unconditionally identical to the old
                // AtBattle(performer) check, since actingAsBattle is always true when Timing=="Battle".
                "own-unbowed-personality-at-battle-with-keyword" => Definition(performer).Type == CardType.Personality && !performer.Bowed &&
                    HasKeywords(performer, ability.RequiredKeywords) && (!actingAsBattle || AtBattle(performer)),
                // "At any location" (issue #267, Improved Defenses): a printed exemption from the
                // ordinary at-THIS-battlefield requirement, broadened instead of removed -- see
                // AtAnyLocation's own doc comment (Home, the current battlefield, or another
                // battlefield that has not yet resolved this Battle phase). Only meaningful while
                // actingAsBattle; an Open-timed use has no presence requirement at all regardless.
                "own-unbowed-personality-with-keyword-at-any-location" => Definition(performer).Type == CardType.Personality && !performer.Bowed &&
                    HasKeywords(performer, ability.RequiredKeywords) && (!actingAsBattle || AtAnyLocation(performer)),
                // No "unbowed" qualifier printed -- Monastery of New Thought's performer may be bowed.
                "own-personality-with-fire-token-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && HasToken(performer, "Fire"),
                // Again!'s shape: also no "unbowed" qualifier -- the whole point is straightening an
                // already-bowed Kensai. Generic on RequiredKeywords like the unbowed variant above.
                // Same actingAsBattle generalization as own-unbowed-personality-at-battle-with-keyword.
                "own-personality-with-keyword-at-battle" => Definition(performer).Type == CardType.Personality &&
                    HasKeywords(performer, ability.RequiredKeywords) && (!actingAsBattle || AtBattle(performer)),
                // Hiruma Tensin (issue #337): "your performing Follower in Tensin's unit" -- the first Performer category that is not a
                // Personality. A Follower of the source's own unit (the source's host, when the source is itself an attachment), unbowed
                // because bowing it is the cost. Its location is its host's, so the shared presence checks already cover it.
                "own-unbowed-follower-in-source-unit" => Definition(performer).Type == CardType.Follower && !performer.Bowed
                    && performer.Zone == Zone.Attached && performer.AttachedTo == UnitHostId(source!),
                // No "unbowed" qualifier printed either -- Tactical Advantage does not bow its performer.
                "own-tactician-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && Keyword(performer, "Tactician"),
                // Naval Invasion (issue #310): "choose your performing Naval Personality" -- printed or granted
                // Naval (Keyword() covers Suitengu's Path, Scourge of the Sea, Kojiro's live conditional), in
                // the Attacker's own army at the battlefield being engaged. Bowed is not excluded: the text says only "Naval".
                "own-naval-personality-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && Keyword(performer, "Naval"),
                // Steady Resolve: "your performing opposed [Keyword] Personality at the current battlefield" -- opposed means the enemy has a unit there; bowed is not excluded.
                "own-personality-with-keyword-opposed-at-battle" => Definition(performer).Type == CardType.Personality && AtBattle(performer) && HasKeywords(performer, ability.RequiredKeywords)
                    && Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                // Equip (Battle): only a Shugenja Personality may ever hold a Spell -- a Follower with
                // the Shugenja trait is excluded by the Type check, matching the printed restriction.
                "own-unbowed-shugenja-opposed-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && AtBattle(performer) &&
                    Keyword(performer, "Shugenja") && Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                // Not a "performer" in the rules sense (Carpenter Castle names no performing card) --
                // reused for the same "select one more card, separately from the effect's target" shape.
                "own-castle-or-siege" => !performer.Bowed && (Keyword(performer, "Castle") || Keyword(performer, "Siege")),
                // Xijkt / Tamori Shosei (issue #207 follow-up): "bow [source] or one of your [keyword]
                // Holdings" -- the general "choose which of several eligible own cards to bow as a
                // cost" shape, not the single-card simplification Mirumoto Kojinrue's own
                // kojinrue-destroy uses. No extra "must be unbowed" check for choosing the source
                // itself: the shared bowed-source Require already rejects the whole action if source
                // is bowed, matching "abilities on bowed cards cannot normally be used" regardless of
                // how the cost would be paid. If no eligible unbowed Holding exists, this naturally
                // forces the only remaining legal choice (the source itself) -- no separate "forced
                // pathway" logic needed.
                "own-source-or-unbowed-holding-with-keyword" => performer.Id == source!.Id
                    || Definition(performer).Type == CardType.Holding && !performer.Bowed && HasKeywords(performer, ability.RequiredKeywords),
                // Sutigu (oracle-l5r-7676, issue #301): "Bow Sutigu or one of his Weapons" -- the same
                // cost-choice shape as the Holding variant above, but the alternate candidate is one of
                // the SOURCE'S OWN attachments (attached to Sutigu himself, not just any of his
                // controller's cards) carrying the required keyword, and it must be unbowed to be bowed
                // as a cost. The source itself needs no unbowed check here (the shared bowed-source
                // Require already rejects the whole action if he is bowed).
                "own-source-or-unbowed-attached-with-keyword" => performer.Id == source!.Id
                    || performer.Zone == Zone.Attached && performer.AttachedTo == source.Id && !performer.Bowed && HasKeywords(performer, ability.RequiredKeywords),
                // The Earth's Hunger: "Bow this Shugenja, or bow this card if he is Earth" -- a cost
                // choice too, but shaped the other way round from Xijkt's own: the CONDITION is
                // checked against the WIELDER (the card this Spell is attached to), not against the
                // alternate candidate itself (there is no candidate-side keyword to check -- the
                // Spell is always eligible once the wielder qualifies). The wielder is always an
                // eligible choice; no "must be unbowed" check on the wielder either, same reasoning
                // as Xijkt's own comment above.
                "own-wielder-or-source-with-wielder-keyword" => performer.Id == Card(source!.AttachedTo!).Id
                    || performer.Id == source.Id && HasKeywords(Card(source.AttachedTo!), ability.RequiredKeywords),
                // Tamori Yayu (issue #216): the same "select one more card, separately from the
                // Attack's own target" shape -- here, which of Yayu's own Spells pays the cost.
                "own-spell-attached-to-source" => Definition(performer).Type == CardType.Spell && performer.AttachedTo == source!.Id,
                "own-personality" => Definition(performer).Type == CardType.Personality,
                // Steel on Steel (issue #204): no AtBattle qualifier of its own -- Dueling's own
                // Timing (Battle, here) already gates the SOURCE card's own presence; the performer's
                // only extra restriction beyond own-personality is "unbowed".
                "own-unbowed-personality" => Definition(performer).Type == CardType.Personality && !performer.Bowed,
                // Gripped by Terror (oracle-l5r-2908, issue #295): "Choose your performing Personality"
                // -- no unbowed qualifier, no keyword gate. own-personality's own at-battle variant,
                // which did not exist yet (every prior "at battle, no other restriction" Performer also
                // carried either "unbowed" or a keyword gate).
                "own-personality-at-battle" => Definition(performer).Type == CardType.Personality && (!actingAsBattle || AtBattle(performer)),
                // Consuming Weakness (oracle-l5r-1467, issue #295): own-unbowed-spider-personality-at-
                // battle's own shape, generalized off HasClanAffiliation the same way that category
                // itself generalizes AtBattle -- Shadowlands instead of Spider, and no "unbowed"
                // qualifier printed here.
                "own-shadowlands-personality-at-battle" => Definition(performer).Type == CardType.Personality &&
                    HasClanAffiliation(performer, "Shadowlands") && (!actingAsBattle || AtBattle(performer)),
                // Strength of Purity (oracle-l5r-7546, issue #287): "your performing Personality with
                // 3 or higher Personal Honor" -- a numeric Personal Honor threshold on the Performer
                // itself, hardcoded to this one card's own printed 3 (lobby's own hardcoded >=1
                // Target-side threshold is the closest existing precedent for a bare numeric gate;
                // this is the Performer-side equivalent). No "unbowed" qualifier printed.
                "own-personality-honor-3-plus-at-battle" => Definition(performer).Type == CardType.Personality &&
                    CardPersonalHonor(performer) >= 3 && (!actingAsBattle || AtBattle(performer)),
                // Strength of the Divine (oracle-l5r-10555, issue #287): "your opposed unbowed
                // Paragon" -- own-unbowed-shugenja-opposed-at-battle's own "opposed" (enemy army
                // present at the current battlefield) gate, generalized off its hardcoded Shugenja
                // onto RequiredKeywords, the same way own-unbowed-personality-at-battle-with-keyword
                // already generalized the plain at-battle gate off individual hardcoded keywords.
                // "Opposed," not "performing," so no AtBattle check of its own -- matching Crushing
                // Force's own own-opposed Target category, which checks the same Army presence
                // without an AtBattle check either.
                "own-unbowed-personality-with-keyword-opposed" => Definition(performer).Type == CardType.Personality && !performer.Bowed &&
                    HasKeywords(performer, ability.RequiredKeywords) && Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                // The Strength of the Spider (oracle-l5r-8362, issue #287): "your performing unbowed
                // Spider Clan Personality" -- a CLAN check (HasClanAffiliation), not a printed
                // Keyword, so RequiredKeywords/HasKeywords cannot express it; hardcoded to Spider,
                // the same "no need to generalize for a single card" discipline every other
                // hardcoded-single-value category here already follows (e.g. the honor-3-plus
                // category just above).
                "own-unbowed-spider-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed &&
                    HasClanAffiliation(performer, "Spider") && AtBattle(performer),
                // Pure Intent (oracle-l5r-9922, issue #289): "Choose your performing opposed
                // Personality" -- own-unbowed-personality-with-keyword-opposed's own shape, minus the
                // unbowed restriction and RequiredKeywords gate neither this card's own scan prints.
                "own-personality-opposed" => Definition(performer).Type == CardType.Personality &&
                    Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(),
                // Strong Defenses (oracle-l5r-9931, issue #289): "Choose your performing defending
                // Personality" -- own-unbowed-attacking-personality-at-battle's own mirror image, off
                // the existing IsDefendingUnit helper (which already folds in its own AtBattle check),
                // minus the unbowed restriction that helper's own attacking counterpart carries (no
                // "unbowed" printed here -- "Straighten him before this battle resolves" is the whole
                // point, so the performer may already be bowed).
                "own-defending-personality-at-battle" => Definition(performer).Type == CardType.Personality && IsDefendingUnit(performer),
                _ => false
            }, "That card does not meet this ability's performer requirement.");
        }
        else Require(command.PerformerId is null || command.PerformerId == command.TargetId && id is "prepared-death" or "defensive-stance" or "lobby", "Unexpected performing card.");
        // Alternative cost (issue #337): the way the acting player pays the non-Gold cost is part of the command, and payable.
        ValidateCostOption(ability, command, source);
        Require(command.Choice is null, "Choices are resolved by a separate decision command.");
        // Goju House Guard (issue #192): the only current DeckPosition consumer, so this stays a
        // per-id check rather than a generic AbilityDefinition flag until a second card needs it.
        Require(id == "goju-house-guard-return" ? command.DeckPosition is "Top" or "Bottom" : command.DeckPosition is null,
            id == "goju-house-guard-return" ? "Choose Top or Bottom for your Fate deck." : "Unexpected deck position.");
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
        // Ring of Earth (issue #194): "give A province +3 or -3 strength" -- either side's own,
        // unlike enemy-province above -- so which player's province, not just which index, must be
        // named. TargetPlayerId already exists for "choose a player" (issue #210); reused here purely
        // as "whose province," alongside Province as the index.
        else if (ability.Target == "any-province")
        {
            Require(command.TargetId is null && (command.CardIds is null || command.CardIds.Length == 0), "This action targets a province, not a card.");
            Require(command.TargetPlayerId is 0 or 1 && command.Province is int anyIdx && anyIdx is >= 0 and <= 3 && !Player(command.TargetPlayerId.Value).Provinces[anyIdx].Destroyed,
                "Choose a province (yours or the enemy's).");
        }
        // "Choose a player" (issue #210): a player, not a card, is the target -- either player is
        // always a legal choice (no restriction printed), so validation is just "is this a real
        // player id at all."
        else if (ability.Target == "player")
        {
            Require(command.TargetId is null && (command.CardIds is null || command.CardIds.Length == 0), "This action targets a player, not a card.");
            Require(command.TargetPlayerId is 0 or 1, "Choose a player.");
        }
        else
        {
            // Togashi Mitsu, Inexperienced (issue #207 follow-up): "destroy zero or more Fire tokens
            // on Mitsu. Ranged Attack with strength equal to the number of tokens destroyed" -- the
            // one Attack whose own strength is a per-COMMAND variable (which/how many Fire tokens the
            // player chooses to destroy right now), not a static per-ability-id/target/source formula
            // AttackStrength's own switch can express. CardIds carries the chosen token ids here
            // (tokens are not entries in _state.Cards, so this is a deliberate, narrow reuse).
            Require(id is "mitsu-fire-attack" or "scorn-the-weak" or "strength-in-terror" or "unexpected" or "visage-of-the-orochi" or "kahoku-exp-attack"
                || command.CardIds is null || command.CardIds.Length == 0, "This action does not select multiple cards.");
            // Bayushi Kahoku - exp (oracle-l5r-807): "Target an enemy Personality: Give him -4F. Ranged 3
            // Attack." Two SEPARATE selections, the Ranged Attack's own target being the second (per the
            // core rules an Attack's targeting is its own step, "not required up front"): TargetId is the
            // -4F target (a required enemy Personality, validated by the ordinary category switch below),
            // CardIds carries the optional Attack target -- the established optional-second-selection
            // idiom (Scorn the Weak, Strength in Terror). It is validated as any Attack target, including
            // Kahoku's own static Follower-immunity exception (AttackTargeting.cs), and MAY be the very
            // Personality the -4F just weakened, so the strength check sees that Force already reduced.
            if (id == "kahoku-exp-attack")
            {
                var ids = command.CardIds ?? [];
                Require(ids.Length is 0 or 1, "Kahoku's Ranged Attack may target at most one card.");
                if (ids.Length == 1) RequireLegalAttackTarget(id, ability, command, player, source, Card(ids[0]), command.TargetId, 4);
            }
            if (id == "mitsu-fire-attack")
            {
                var tokenIds = command.CardIds ?? [];
                Require(tokenIds.Distinct().Count() == tokenIds.Length && tokenIds.All(tid => source!.Tokens.Any(t => t.Id == tid && t.Type == "Fire")),
                    "Choose zero or more of Mitsu's own Fire tokens to destroy.");
            }
            // Scorn the Weak (oracle-l5r-9925, issue #287): "If his Force is now 0, ... you may
            // target and dishonor a Personality" -- CardIds carries this optional second target (any
            // Personality, either side, no further restriction printed), the same "not required to
            // exist" idiom optional-dishonorable-personality already establishes for a single
            // TargetId, just carried on CardIds since this ability already has a required primary
            // TargetId. Only offered when the primary target's Force would actually reach 0 from
            // this ability's own flat -5F (CardForce already floors at 0, so "now 0" reduces to a
            // pre-effect Force of 5 or less).
            if (id == "scorn-the-weak")
            {
                var ids = command.CardIds ?? [];
                Require(ids.Length is 0 or 1, "Scorn the Weak may target at most one additional Personality to dishonor.");
                if (ids.Length == 1)
                {
                    Require(CardForce(Card(command.TargetId!)) <= 5,
                        "You may only target and dishonor a Personality when the enemy Personality's Force would reach 0.");
                    Require(Definition(Card(ids[0])).Type == CardType.Personality, "Target a Personality to dishonor.");
                }
            }
            // Strength in Terror (oracle-l5r-7538, issue #287): "You may target and destroy one of
            // the Personality's attachments" -- CardIds' own second-selection reuse again, restricted
            // to one of the primary target's own attachments.
            if (id == "strength-in-terror")
            {
                var ids = command.CardIds ?? [];
                Require(ids.Length is 0 or 1, "Strength in Terror may target at most one attachment to destroy.");
                if (ids.Length == 1)
                {
                    var attachmentToDestroy = Card(ids[0]);
                    Require(attachmentToDestroy.Zone == Zone.Attached && attachmentToDestroy.AttachedTo == command.TargetId,
                        "Target one of the Personality's own attachments.");
                }
            }
            // Unexpected (oracle-l5r-9940, issue #287): "You may bow your Personality; if you did,
            // draw a card" -- CardIds' own third reuse this wave: the performer's own id, when
            // supplied, opts into the self-bow-for-a-draw.
            if (id == "unexpected")
            {
                var ids = command.CardIds ?? [];
                Require(ids.Length is 0 or 1 && (ids.Length == 0 || ids[0] == command.PerformerId),
                    "You may only bow your own performing Personality.");
            }
            // Visage of the Orochi (oracle-l5r-9168, issue #289): "You may target and bow an enemy
            // card with lower Force and no attachments" -- CardIds' own established optional-second-
            // selection idiom again, inlined here (rather than a new Target category) because the
            // comparison is against the Performer's own POST-"Raise to 6" Force, not its live Force at
            // validation time (the Set-Force effect itself has not executed yet) -- Math.Max(6,
            // CardForce(performer)) predicts that outcome without needing to actually apply it early.
            if (id == "visage-of-the-orochi")
            {
                var ids = command.CardIds ?? [];
                Require(ids.Length is 0 or 1, "Visage of the Orochi may target at most one enemy card to bow.");
                if (ids.Length == 1)
                {
                    var bowTarget = Card(ids[0]);
                    var raisedPerformerForce = Math.Max(6, CardForce(Card(command.PerformerId!)));
                    Require(bowTarget.OwnerId != player.Id && Definition(bowTarget).Type is CardType.Personality or CardType.Follower
                        && CardForce(bowTarget) < raisedPerformerForce && !HasAttachments(bowTarget),
                        "You may only target and bow an enemy card with lower Force than your Thunder Personality (after being raised to 6) and no attachments.");
                }
            }
            if (ability.Target == "none") Require(command.TargetId is null, "This action has no target.");
            // Ranged/Melee Attack: unlike every other ability target, this one is not required to
            // exist for the action to be legal -- a null TargetId always validates. When a TargetId
            // is supplied, it must be a Follower or a Personality without Followers in the current
            // enemy army with Force at or below the attack's (possibly conditional) strength.
            else if (ability.Target == "attack")
            {
                // The legality rules (Follower-immunity and its explicit exceptions, per-kind immunity,
                // strength) live in RequireLegalAttackTarget (AttackTargeting.cs), shared with abilities that
                // carry their Attack target on CardIds instead of TargetId (Bayushi Kahoku - exp).
                if (command.TargetId is not null) RequireLegalAttackTarget(id, ability, command, player, source, Card(command.TargetId));
            }
            // Unimpeachable Name (oracle-l5r-8989): "You MAY target and rehonor a dishonorable
            // Personality" -- an optional target, the same "not required to exist" shape as "attack"
            // above (a null TargetId always validates; a supplied one must be a real Dishonorable
            // Personality, either side, no ownership restriction printed).
            else if (ability.Target == "optional-dishonorable-personality")
            {
                if (command.TargetId is not null)
                {
                    var target = Card(command.TargetId);
                    Require(Definition(target).Type == CardType.Personality && target.Dishonorable, "Target a dishonorable Personality, or none.");
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
                    // Unique/Singular (issue #202): Equip (Battle)'s own Spell/Reserve attach is a
                    // third entry-into-play path (alongside Purchase and the ordinary Equip()) that
                    // needed this check.
                    Require(!(Keyword(target,"Unique") || Keyword(target,"Singular")) || !ControlsCopyOf(player.Id,Definition(target).Name),
                        $"You already control this {(Keyword(target,"Unique") ? "Unique" : "Singular")} card.");
                }
                // Low Market's own Target category: broader than "spell-or-reserve-in-hand" above (any
                // Item/Follower/Spell, matching the ordinary Equip() command's own type filter), reusing
                // the same shared attach-legality helpers Equip() itself uses (AttachmentLegalTarget
                // already folds in AttachRequiresKeyword/Excludes/Name AND a Follower's Honor
                // Requirement; AttachmentAllowed is the Armor/Weapon-cap check).
                else if (ability.Target == "attachment-in-hand")
                {
                    Require(target.OwnerId == player.Id && target.Zone == Zone.Hand && type is CardType.Item or CardType.Follower or CardType.Spell,
                        "Choose an Item, Follower, or Spell from your hand.");
                    Require(AttachmentLegalTarget(Definition(target), Card(command.PerformerId!)), "This attachment will not attach to that Personality.");
                    Require(AttachmentAllowed(target, Card(command.PerformerId!)), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
                    Require(!(Keyword(target,"Unique") || Keyword(target,"Singular")) || !ControlsCopyOf(player.Id,Definition(target).Name),
                        $"You already control this {(Keyword(target,"Unique") ? "Unique" : "Singular")} card.");
                }
                // Moto Jun-Ni / Foothills Keep (issue #337): a card in a discard pile the ability's AttachSpec names (whose pile, which kinds).
                // Everything else about attaching it is ValidateAttach, below.
                else if (ability.Target == "attachment-in-discard")
                {
                    var attachError = AttachTargetError(ability.Attach!, player.Id, target);
                    Require(attachError is null, attachError ?? "");
                }
                // Kharmic's own repeatable action: a Kharmic-keyword card the player controls, either a
                // Fate card in hand or a face-up Dynasty card in one of their own provinces -- neither
                // zone is "in play", so this needs its own branch, same shape as "hand" above. Face-up
                // matches DiscardProvince's own existing rule for discarding a province card at all.
                else if (ability.Target == "own-kharmic-card")
                    Require(target.OwnerId == player.Id && Keyword(target, "Kharmic")
                        && (target.Zone == Zone.Hand || target.Zone == Zone.Province && target.FaceUp),
                        "Discard a Kharmic card you control, from your hand or a face-up province.");
                // Hunter of Harmony's own ability (issue #190): a Kiho Strategy card sitting in the
                // player's own Fate discard pile -- not in play, so this needs its own branch just like
                // "own-kharmic-card" above.
                else if (ability.Target == "own-kiho-strategy-in-discard")
                    Require(target.OwnerId == player.Id && type == CardType.Strategy && Keyword(target, "Kiho") && target.Zone == Zone.FateDiscard,
                        "Choose a Kiho Strategy card from your Fate discard pile.");
                // Daigotsu Bofana - exp (issue #333): "a Dark Virtue in your discard pile" -- a Strategy carrying the
                // keyword, in the player's own Fate discard pile, that has a Battle action still playable this turn (a card with
                // none would only waste the grant).
                else if (ability.Target == "own-dark-virtue-in-discard")
                    Require(target.OwnerId == player.Id && type == CardType.Strategy && Keyword(target, "Dark Virtue") && target.Zone == Zone.FateDiscard && HasUnspentBattleAction(target),
                        "Choose a Dark Virtue with a Battle action from your Fate discard pile.");
                // Scholarly Hermit's own ability (issue #194): a Ring in the player's own Fate
                // discard pile -- not in play, so this needs its own branch too.
                else if (ability.Target == "own-ring-in-discard")
                    Require(target.OwnerId == player.Id && Definition(target).Type == CardType.Ring && target.Zone == Zone.FateDiscard,
                        "Choose a Ring from your Fate discard pile.");
                // Tranquil Garden (issue #264): the first Target spanning both an ordinary in-play
                // card (either side, matching "a Ring in play" with no "your own" qualifier) and one
                // sitting in the player's own Fate discard pile at once.
                else if (ability.Target == "ring-in-play-or-own-discard")
                    Require(Definition(target).Type == CardType.Ring && (InPlay(target) || target.OwnerId == player.Id && target.Zone == Zone.FateDiscard),
                        "Choose a Ring in play or in your own Fate discard pile.");
                // Consuming Weakness (issue #295): "a target dead Personality in any discard pile" --
                // Zone.Dead (this engine's own "discard pile" for a Personality specifically -- Zone.
                // FateDiscard/DynastyDiscard are for Fate/Dynasty cards of every OTHER type), any owner,
                // not in play, so this needs its own branch too.
                else if (ability.Target == "dead-personality-any-discard")
                    Require(type == CardType.Personality && target.Zone == Zone.Dead, "Choose a dead Personality in any discard pile.");
                // Your Clan Needs You! (issue #295): "your performing Personality in one of your
                // provinces" -- a province card is never InPlay, so this needs its own branch too, the
                // same shape own-kharmic-card/own-kiho-strategy-in-discard already use for a target
                // outside the ordinary in-play zones.
                else if (ability.Target == "own-personality-in-province")
                    Require(target.OwnerId == player.Id && type == CardType.Personality && target.Zone == Zone.Province && target.FaceUp,
                        "Choose your own face-up Personality in a province.");
                // Shosuro Hawado (issue #301): "a Dynasty card in any discard pile" -- a Dynasty-deck card
                // type (Personality, Holding, Event, Region, Celestial) sitting in EITHER player's Dynasty
                // discard pile. Zone.Dead is this engine's own "discard pile" for a Personality once it has
                // died (Zone.DynastyDiscard holds every other Dynasty type, plus a Personality merely
                // discarded from a province, not yet dead); both are that same physical pile. Not in
                // play, so it needs its own branch -- Consuming Weakness's dead-personality-any-discard
                // (Zone.Dead only, Personalities only) is the narrower sibling.
                else if (ability.Target == "dynasty-card-in-any-discard")
                    Require(IsDynastyCardType(type) && target.Zone is Zone.DynastyDiscard or Zone.Dead, "Choose a Dynasty card in any discard pile.");
                // Fukuzo (issue #338): "a target Personality in any player's discard pile" -- a Personality merely discarded
                // (Zone.DynastyDiscard) or dead (Zone.Dead), either player's: the same physical pile, and the same two zones the sibling
                // categories above name. Not in play, so it needs its own branch.
                else if (ability.Target == "personality-in-any-discard")
                    Require(type == CardType.Personality && target.Zone is Zone.DynastyDiscard or Zone.Dead, "Choose a Personality in any discard pile.");
                // Soshi Neiru (issue #301): "Air Spells" from the player's own hand -- equip-battle's
                // own spell-or-reserve-in-hand shape, narrowed by RequiredKeywords instead of hardcoding a
                // keyword, with the wielder fixed to the source card itself (no separate Performer).
                else if (ability.Target == "spell-in-hand-with-keyword")
                {
                    Require(target.OwnerId == player.Id && target.Zone == Zone.Hand && type == CardType.Spell && HasKeywords(target, ability.RequiredKeywords),
                        "Choose a Spell with the required keyword from your hand.");
                    Require(AttachmentLegalTarget(Definition(target), source!), "This Spell will not attach to that Shugenja.");
                    Require(AttachmentAllowed(target, source!), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
                    Require(!(Keyword(target, "Unique") || Keyword(target, "Singular")) || !ControlsCopyOf(player.Id, Definition(target).Name),
                        $"You already control this {(Keyword(target, "Unique") ? "Unique" : "Singular")} card.");
                }
                else
                {
                    Require(InPlay(target), "Target a card in play.");
                    // Deliberate exceptions to "every Battle-timing target must be at the current
                    // battlefield": "personality-at-adjacent-battlefield" (Moto Gunso's shape -- the
                    // whole point is a target away from the current battlefield) and "own-unbowed-farm"
                    // (Ashigaru Elite's cost-target, a Farm to bow, lives in Zone.Home) are exempt by
                    // their own dedicated Target category. Intervening Spirit / The Earth Flows (issue
                    // #264) reuse the ordinary "own-personality"/"personality" categories -- reused
                    // elsewhere with the normal AtBattle expectation (e.g. Ring of Water's own
                    // "move your target Personality home") -- so they are exempted by ability id
                    // instead: their whole point is reinforcing a Personality who is NOT there yet.
                    // actingAsBattle (issue #267): the same generalization as the blanket block above --
                    // a BattleOrOpen ability's Open half has no battlefield to validate a target
                    // against at all, so this Require applies only while genuinely acting as Battle.
                    // Brawl (issue #304): "a Region (at any province)" -- a Region sits in Zone.Region and is
                    // never AtBattle-eligible; a Terrain in play is always at the current battlefield anyway
                    // (it is discarded when its battle resolves), so exempting the whole category is exact.
                    // Return to Action (issue #331): "your Personality at HOME" is by definition not at the battlefield
                    // -- own-personality-at-home is exempt for the same reason.
                    if (actingAsBattle && ability.Target is not ("personality-at-adjacent-battlefield" or "own-unbowed-farm" or "region-or-terrain" or "own-personality-at-home")
                        && id is not ("intervening-spirit-move" or "earth-flows-move" or "sanctuary-of-the-waves-move")) Require(AtBattle(target), "Target or performer must be at the current battlefield.");
                    var personality = type == CardType.Personality;
                    var own = target.OwnerId == player.Id;
                    // TargetMustBe (P2): a target-status legality condition layered onto whichever category
                    // follows, exactly like RequireNoAttachments -- "target an enemy dishonorable Personality".
                    Require(ability.TargetMustBe is null || MatchesTargetStatus(target, ability.TargetMustBe),
                        $"The target must be {ability.TargetMustBe?.Describe()}.");
                    Require((!ability.RequireNoAttachments || !HasAttachments(target)) && ability.Target switch {
                        "shinden" => personality && !own && !target.Bowed,
                        "air" => personality && Keyword(target, "Air"),
                        "personality" => personality,
                        // Ashigaru Elite's cost-target: an unbowed Farm-keyword Holding the performer
                        // controls. Bowing it (in the execution switch) pays the cost; the effect
                        // itself is a counter increment, not applied to this target at all.
                        "own-unbowed-farm" => type == CardType.Holding && own && !target.Bowed && Keyword(target, "Farm"),
                        // Yasuki Tsujiken (oracle-l5r-9469): "target a Holding" -- no ownership or
                        // bowed-state restriction on either of his own two abilities.
                        "any-holding" => type == CardType.Holding,
                        // Asahina Munefusa (issue #301): "your Temple Holding" -- an own Holding carrying
                        // every RequiredKeywords entry (any bow state: straightening is the whole point).
                        // own-unbowed-farm above is the hardcoded Farm-only sibling.
                        "own-holding-with-keyword" => type == CardType.Holding && own && HasKeywords(target, ability.RequiredKeywords),
                        // Isawa Mitsuko (issue #301): "an Air Shugenja or a Holding" -- either side's. A
                        // Personality must carry BOTH Air and Shugenja (the existing "air" category is only
                        // the Air half).
                        "air-shugenja-or-holding" => type == CardType.Holding || personality && Keyword(target, "Air") && Keyword(target, "Shugenja"),
                        // Shosuro Rin (issue #301): "an honorable Personality" -- the plain negation of
                        // dishonorable-personality below, either side.
                        "honorable-personality" => MatchesTargetStatus(target, TargetStatus.IsHonorable),
                        "own-personality" => personality && own,
                        // Return to Action (issue #331): "target your Personality at home" -- own, in Zone.Home (a
                        // Personality out of play, at a battlefield or dead never qualifies), bowed or not: the card
                        // straightens him afterward, so a bowed one is a legal (and the best) target.
                        "own-personality-at-home" => personality && own && target.Zone == Zone.Home,
                        // Tamori Tamoko's own Open action (issue #207 follow-up): "target an unbowed
                        // Personality controlled by the active player" -- "if it is not your turn" is
                        // checked separately below (a per-id Require, not part of this Target shape).
                        "unbowed-personality-controlled-by-active-player" => personality && !target.Bowed && target.OwnerId == _state.ActivePlayerId,
                        // Ritual Preparation (issue #192): "your target card in a unit" -- either
                        // half of a unit (the Personality herself, or an attached Follower), own,
                        // in play. Items/Spells are never described as "in a unit" (core rules:
                        // that phrase is reserved for Personality/Follower combat participants).
                        "own-unit-card" => own && (personality || type == CardType.Follower),
                        // Ring of Air (issue #194): "your target Personality or attachment" -- own
                        // only, either half of a unit (unlike own-unit-card elsewhere, this also
                        // includes Spells, since a Spell is straightenable just like any attachment).
                        "personality-or-attachment" => own && (personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell),
                        // Generic "Choose your performing/target [Keyword(s)] Personality" -- for a
                        // keyword with no mechanical rule of its own (e.g. Berserker), or in addition
                        // to one a keyword already has (e.g. Tactician): see AbilityDefinition.RequiredKeywords.
                        "own-personality-with-keyword" => personality && own && HasKeywords(target, ability.RequiredKeywords),
                        // Sanctuary of the Waves (issue #295): "target this Shugenja, or, if this
                        // Shugenja is Water, you may target another of your Personalities" -- the
                        // Target-side mirror of own-wielder-or-source-with-wielder-keyword's own
                        // Performer-side cost choice: the default candidate is always the wielder
                        // himself; a DIFFERENT own Personality only qualifies once the wielder's own
                        // keyword condition holds.
                        "own-wielder-or-personality-with-wielder-keyword" => own && personality &&
                            (target.Id == Card(source!.AttachedTo!).Id || HasKeywords(Card(source.AttachedTo!), ability.RequiredKeywords)),
                        "enemy-personality" => personality && !own,
                        "own-bowed" => personality && own && target.Bowed,
                        "own-opposed" => personality && own && Army(1-player.Id, _state.CurrentBattleProvince!.Value).Any(),
                        "enemy-force" => !own && type is CardType.Personality or CardType.Follower,
                        "enemy-follower" => !own && type == CardType.Follower,
                        "any-card" => true,
                        "attacking" => personality && target.OwnerId == _state.ActivePlayerId,
                        "defending" => personality && target.OwnerId != _state.ActivePlayerId,
                        "enemy-attacking" => personality && !own && target.OwnerId == _state.ActivePlayerId,
                        // Tokoru's Bear (oracle-l5r-10025, issue #299): "an attacking enemy card" -- not
                        // just an attacking Personality (enemy-attacking above) but any card in an
                        // attacking unit: the Personality himself or one of his attached Followers/
                        // Items/Spells. "Attacking" is a property of the UNIT (its host Personality's
                        // controller is the active player), so an attachment is judged by its host,
                        // not by its own OwnerId (Cultist Infiltrator can sit on an enemy Personality).
                        // Must also be at the current battlefield -- already required for every
                        // Battle-timed target by the shared Require above, which resolves an
                        // attachment through its own parent's location.
                        "enemy-attacking-card" => !own && (personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell)
                            && Card(UnitHostId(target)).OwnerId == _state.ActivePlayerId,
                        "enemy-attachment" => !own && target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        // Hida Mimori (issue #301): "a bowed enemy card" -- a card in a unit (the Personality
                        // or one of its attached Followers/Items/Spells), enemy-owned and currently bowed.
                        // The shared Battle-timing AtBattle Require above already resolves an attachment
                        // through its own host's battlefield. The mirror image of own-bowed (Personality-only,
                        // own) below.
                        "enemy-bowed-card" => !own && target.Bowed && (personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell),
                        "attachment" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell,
                        // Champion of Thunder: "any card in this unit" -- the reacting card's own
                        // unit (source is Champion itself here), always in play by construction
                        // (every member of a unit is either the host Personality or one of its own
                        // attachments), so no further InPlay/ownership check is needed beyond the
                        // blanket one already applied above.
                        "own-unit-member" => UnitHostId(target) == UnitHostId(source!),
                        // A Close Call's own Battle half: "target a Personality with an attachment" --
                        // HasAttachments already exists for exactly this boolean (a count, not "any
                        // specific kind"), reused verbatim.
                        "personality-with-attachment" => personality && HasAttachments(target),
                        "lobby" => personality && own && !target.Bowed && CardPersonalHonor(target) >= 1,
                        "fewer-or-equal-followers-than-performer" => personality &&
                            Attachments(target.Id).Count(a => Definition(a).Type == CardType.Follower) <= Attachments(command.PerformerId!).Count(a => Definition(a).Type == CardType.Follower),
                        // Unexpected Support: the enemy-only counterpart to journeys-end-move's own
                        // category above (which targets either side, per its own printed text) -- same
                        // Follower-count comparison, just filtered to !own like every other paired
                        // "X"/"enemy-X" Target category already in this switch.
                        "enemy-fewer-or-equal-followers-than-performer" => personality && !own &&
                            Attachments(target.Id).Count(a => Definition(a).Type == CardType.Follower) <= Attachments(command.PerformerId!).Count(a => Definition(a).Type == CardType.Follower),
                        "lower-or-equal-honor-than-performer" => personality && CardPersonalHonor(target) <= CardPersonalHonor(Card(command.PerformerId!)),
                        "own-personality-with-token" => personality && own && target.Tokens.Count > 0,
                        "own-tattooed-personality" => personality && own && Keyword(target, "Tattooed"),
                        "personality-lower-honor-than-self" => personality && CardPersonalHonor(target) < CardPersonalHonor(source!),
                        // Akodo Dairuko (issue #198): the same shape as -than-self above, restricted
                        // to an enemy.
                        "enemy-personality-lower-honor-than-self" => personality && !own && CardPersonalHonor(target) < CardPersonalHonor(source!),
                        // Akodo Furu (issue #198): a bare Chi comparison against the source itself
                        // (not a separately-chosen Performer, unlike enemy-personality-chi-lte-performer),
                        // and not restricted to an enemy.
                        "personality-chi-lte-self" => personality && PersonalityChi(target) <= PersonalityChi(source!),
                        // Akodo Michitsu (issue #198) reuses the existing "enemy-attachment" category
                        // above verbatim -- no new Target needed.
                        // Akodo Tsudoken's own second Experienced printing (issue #198): own Personality,
                        // Lion Clan OR carrying the Fallen keyword.
                        "own-lion-or-fallen-personality" => own && personality && (HasClanAffiliation(target, "Lion") || Keyword(target, "Fallen")),
                        // Ikoma Masumi (issue #198): the same Follower-or-unattached-Personality shape
                        // the Ranged/Melee "attack" Target already uses, as a bare enemy-card check
                        // (no Attack strength/kind involved here, just "without attachments").
                        "enemy-card-without-attachments" => !own && (type == CardType.Follower || personality && !HasAttachments(target)),
                        // Fires of Turmoil (issue #295): the same Follower-or-unattached-Personality
                        // shape as enemy-card-without-attachments above, minus its enemy restriction --
                        // this card's own "Target a card without attachments" is printed with no
                        // ownership qualifier at all.
                        "unattached-unit-card" => type == CardType.Follower || personality && !HasAttachments(target),
                        // Ikoma Tsukasa (issue #198): the same shape as above, but the "without
                        // attachments" restriction is waived entirely while the performer's controller
                        // controls any Terrain.
                        "enemy-unit-card-terrain-conditional" => !own && (type == CardType.Follower || personality) &&
                            (ControlsKeyword(player.Id, "Terrain") || !HasAttachments(target)),
                        "enemy-personality-fewer-weapons-than-self" => personality && !own &&
                            Attachments(target.Id).Count(a => Keyword(a, "Weapon")) < Attachments(source!.Id).Count(a => Keyword(a, "Weapon")),
                        "terrain-at-battle" => type == CardType.Strategy && Keyword(target, "Terrain"),
                        // Brawl (issue #304): a Region attached to any province (either player's), or a
                        // Terrain in play. Neither restricts ownership.
                        "region-or-terrain" => type == CardType.Region && target.Zone == Zone.Region
                            || type == CardType.Strategy && target.Zone == Zone.Battlefield && Keyword(target, "Terrain"),
                        // Province adjacency (Moto Gunso's shape: "...at the current battlefield and
                        // at each adjacent battlefield"): the target's own province, not the current
                        // one, must be adjacent to it. See AdjacentProvinces.
                        "personality-at-adjacent-battlefield" => personality && target.Zone == Zone.Battlefield &&
                            AdjacentProvinces(target.OwnerId, _state.CurrentBattleProvince!.Value).Contains(target.ProvinceIndex ?? -1),
                        // Compared against the chosen Performer (a separately-selected card), unlike
                        // "-than-self" categories above which compare against the ability's own source.
                        "enemy-personality-chi-lte-performer" => personality && !own && Definition(target).Chi <= Definition(Card(command.PerformerId!)).Chi,
                        // Volcano Tattoo (issue #190): "a card without attachments in his unit" -- the
                        // same Follower-or-unattached-Personality shape the Ranged/Melee "attack" Target
                        // already uses above, but compared against the unit's own Personality's Chi
                        // (via PersonalityChi, which includes Item-attachment bonuses) rather than a
                        // Force/strength check.
                        "enemy-unattached-unit-card-chi-lte-performer" => !own && (type == CardType.Follower || personality && !HasAttachments(target)) &&
                            PersonalityChi(type == CardType.Personality ? target : Card(target.AttachedTo!)) <= PersonalityChi(Card(command.PerformerId!)),
                        "enemy-force-lower-than-performer" => !own && type is CardType.Personality or CardType.Follower && CardForce(target) < CardForce(Card(command.PerformerId!)),
                        // Bounty Hunter: "a target attachment or a target dishonorable Personality" --
                        // an OR of two otherwise-unrelated existing categories.
                        "attachment-or-dishonorable-personality" => target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell
                            || personality && target.Dishonorable,
                        // Rumormonger: any Personality controlled by a player who controls at least one
                        // Dishonorable Personality (not necessarily this target itself).
                        "personality-controller-controls-dishonorable" => personality &&
                            _state.Cards.Values.Any(c => c.OwnerId == target.OwnerId && Definition(c).Type == CardType.Personality && InPlay(c) && c.Dishonorable),
                        // Dragon Clan review (issue #207): Kitsuki Fujimura -- any Dishonorable
                        // Personality, either side.
                        "dishonorable-personality" => MatchesTargetStatus(target, TargetStatus.IsDishonorable),
                        // Mirumoto Kouzei: an enemy Personality or Follower with Force at or below
                        // the source's own current Force.
                        "enemy-card-force-lte-self" => !own && type is CardType.Personality or CardType.Follower && CardForce(target) <= CardForce(source!),
                        // Mirumoto Mitoshi: a Personality (either side) with strictly lower Force
                        // than the source's own.
                        "personality-force-lt-self" => personality && CardForce(target) < CardForce(source!),
                        // Strategic Sacrifice (issue #264): a card in a unit (either side) with Force
                        // at or below the current battlefield's own province's strength -- "the
                        // current battlefield's province" is always the Defender's own, the same
                        // lookup false-ring-of-water-recruit's own Require already uses.
                        "force-lte-province-strength" => type is CardType.Personality or CardType.Follower &&
                            CardForce(target) <= EffectiveProvinceStrength(Player(1 - _state.ActivePlayerId).Provinces.Single(p => p.Index == _state.CurrentBattleProvince!.Value)),
                        // The Weakness of Man (issue #267): "Force is different from his [the
                        // performer's] own BASE Force" -- the performer's side of the comparison is
                        // its own printed Force (Definition(...).Force), not CardForce's live value, so
                        // a Force-modifying effect on the performer never changes who is a legal target
                        // mid-battle. The target's own side is its live Force, same as every other
                        // Force-comparison Target category here.
                        "personality-force-neq-performer-base" => personality && CardForce(target) != Definition(Card(command.PerformerId!)).Force,
                        // Mirumoto Hatsuto: an enemy Personality or Follower without the Cavalry
                        // keyword -- "Infantry" (core rules Glossary) has no printed keyword of its
                        // own, it just means not Cavalry, the same convention Cavalry Maneuvers uses.
                        "enemy-force-non-cavalry" => !own && type is CardType.Personality or CardType.Follower && !Keyword(target, "Cavalry"),
                        // Togashi Noboru (issue #216): an enemy Personality with strictly lower Chi
                        // than his own -- the enemy-scoped counterpart to personality-force-lt-self.
                        "enemy-personality-chi-lt-self" => personality && !own && PersonalityChi(target) < PersonalityChi(source!),
                        // Togashi Hizumi (issue #216): "target him" -- the specific Personality this
                        // action just bowed at Hizumi's own battlefield (OpenBowReactionWindow already
                        // narrowed the candidate set to exactly that, keyed by location).
                        "bow-reaction-candidate" => personality && _state.BowReactionCandidateIds.Contains(target.Id),
                        // Tamori Yayu (issue #216): "one of Yayu's Spells" -- a Spell attached
                        // specifically to the performer (no separate Performer field -- Yayu acts as
                        // himself), restricted to Spells only, as the user's own clarification asks.
                        "own-spell-attached-to-source" => type == CardType.Spell && target.Zone == Zone.Attached && target.AttachedTo == source!.Id,
                        // Tamori Ruya (issue #216): "a Personality at the same location" as Ruya
                        // herself (source) -- either side, unrelated to which personality was actually
                        // bowed by the triggering action. "Location" is Home (associated with a
                        // player, so only that SAME player's other cards qualify there) or a
                        // battlefield (associated with a province, so either side's cards there do).
                        "personality-at-source-location" => personality && SameLocation(target, source!),
                        // Mirumoto Houken: a Personality (either side) with an attached Weapon-
                        // keyword Item.
                        "personality-with-weapon" => personality && Attachments(target.Id).Any(a => Keyword(a, "Weapon")),
                        // Togashi Hiroto / Tamori Muzu: any enemy card at all (Personality, Follower,
                        // or an attachment) -- broader than enemy-force (Personality/Follower only)
                        // or enemy-attachment (attached only); combines with the shared
                        // RequireNoAttachments flag above, which is already true by construction for
                        // an attachment (one can never itself carry a further attachment).
                        "enemy-card" => !own,
                        // Togashi Korimi, Experienced: subtly different from "without attachments" --
                        // a card with only BOWED attachments (or none at all) still qualifies.
                        "enemy-card-without-unbowed-attachments" => !own && !Attachments(target.Id).Any(a => !a.Bowed),
                        _ => false
                    }, "That card does not meet this ability's target or performer requirements.");
                }
            }
        }
        // Attach (issue #337): the wielder and the Equip legality of the attachment (see AttachAction.cs).
        if (ability.Attach is { } attachSpec) ValidateAttach(command, attachSpec, player, source);
        // Shinden Shorai (oracle-l5r-6857, issue #301): "If it is not your first or second turn" -- the
        // player's OWN turn count (PlayerState.TurnsTaken counts only that player's turns, the same
        // counter border-first's own "your first turn" reads; the global MatchState.TurnNumber advances
        // once per EITHER player's turn and is the wrong scale). "A face-up Personality in this
        // province" -- the recruit must sit in the Region's own province. The recruit's own quote
        // (Honor Requirement -- waived when the Personality itself is Lion Clan, unlike
        // HonorRequirementWaiverClan's recruiting-player-Clan check -- Unique/Singular, Loyal) is
        // validated here too, so an offered command is one that really resolves, and its cost is "all
        // costs": the Personality's full Gold cost, no Clan discount/allegiance route.
        if (id == "shinden-shorai-recruit")
        {
            Require(player.TurnsTaken > 2, "Shinden Shorai cannot be used on your first or second turn.");
            var recruit = Card(command.TargetId!);
            Require(recruit.ProvinceIndex == source!.ProvinceIndex, "Choose the face-up Personality in this Region's own province.");
            var recruitQuote = QuotePurchase(player.Id, recruit.Id, ignoreHonorRequirementIfPersonalityClan: "Lion");
            Require(recruitQuote.Error is null, recruitQuote.Error ?? "");
        }
        // The Gold cost (issue #303): every ability's price -- Equip (Battle)/Soshi Neiru's Spell cost, Low
        // Market's "2 less, to a minimum of 1", Your Clan Needs You!'s recruit cost minus 6, Shinden
        // Shorai's "all costs", Singh Remnants' own cost, a Strategy/Discipline's printed cost, Kharmic's
        // 2 and each Invest's amount -- is one GoldCostRule on the ability, interpreted in GoldQuotes.cs.
        // The same AbilityCost prices the offered payment (AbilityActions), the client's GoldQuote and the
        // charge in ExecuteAbilityEffects, so validation can never disagree with what is then paid.
        ValidateAbilityPayment(command, player.Id);
    }

    private void UseAbility(GameCommand command)
    {
        ValidateAbility(command);
        AnnounceAbility(command);
    }
    // Everything after validation. Split out (issue #333) because a trait fired by the announcement itself -- Daigotsu
    // Bofana's forced discard -- may stop here for a player decision and then carry on with this same command.
    private void AnnounceAbility(GameCommand command)
    {
        if (OpenForcedDiscardForTargetedTrait(command)) return;
        ContinueUseAbility(command);
    }
    private void ContinueUseAbility(GameCommand command)
    {
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
        // Mirumoto Rokai / Tamori Tamoko (issue #207 follow-up): checked after the Additional-Action
        // branch above, not before -- an eligible watcher whose triggering ability also happens to be
        // consumed as an Additional Action is a known, narrow scope limit this pass (see
        // BeforeActionReactionApplies's own comment), not silently mishandled.
        // Tsuruchi Yakusho (issue #276 follow-up): "after you announce an action" -- earlier than
        // beforeActionReaction below (which reacts to an already-fully-formed, about-to-apply effect),
        // matching the printed ruling that announcing "technically occurs before performers are
        // selected, costs are paid, targets are selected, and effects applied."
        // A Reaction is an action, so these windows apply to it wherever it is played -- including the Engage window: the window it
        // was played in is suspended behind them and reopened afterwards (ReactionsAreActions.cs).
        if (ResolveOrDeferAnnounceReaction(command)) return;
        if (ResolveOrDeferBeforeActionReaction(command)) return;
        // Unimpeachable Name (oracle-l5r-8989): checked after Rokai/Tamoko above, same shape as the
        // Additional-Action branch -- offered against this same command before it resolves.
        if (ResolveOrDeferUnimpeachableName(command, command.AbilityId)) return;
        ExecuteAbilityEffects(command);
    }
    // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action" Reaction
    // primitive, generalizing the same announce-time moment War Weariness/Times of Strife already
    // hook (the very top of ExecuteAbilityEffects) into a genuine, player-usable, deferred window --
    // only ever offered when the about-to-execute ability is classified Ranged by the existing
    // AttackKind() lookup (a printed narrowing on Yakusho's own text, not a limit of the primitive
    // itself, which a future card could reuse unscoped). Defers the WHOLE command, same shape as
    // ResolveOrDeferBeforeActionReaction, so nothing of the ability's own effect -- including the two
    // hardcoded announce-time checks it would otherwise trip -- runs until the window closes.
    private bool ResolveOrDeferAnnounceReaction(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Timing != "Battle"
            || AttackKind(command.AbilityId) != "Ranged") return false;
        var watcher = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == command.PlayerId && InPlay(c) && !c.Bowed && AtBattle(c)
            && AbilityIdsOf(c).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == "own-ranged-action-announced"));
        if (watcher is null) return false;
        _state.PendingAnnouncedAction = command;
        _state.PendingChoice = new("announceReaction", watcher.OwnerId, watcher.Id, "After you announce this action: you may use this card's own reaction, or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = watcher.OwnerId;
        Emit("reactionWindow", "Announce reaction window opened.", watcher.OwnerId, watcher.Id);
        return true;
    }
    // Stormless Fury (issue #276 follow-up): the general "after a Battle action resolves" primitive,
    // offering either an out-of-play attach-as-Reaction candidate (Equip (Limited)'s own attach
    // mechanism, reused as a Reaction) or, failing that, an ordinary in-play watcher -- never both at
    // once, since the two halves live in mutually exclusive zones for any one card copy.
    private (CardInstance Watcher, bool OutOfPlay)? FindBattleActionReactionCandidate(CardInstance performer)
    {
        var outOfPlay = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == performer.OwnerId && c.Zone == Zone.Hand
            && (Definition(c).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == "after-battle-action-resolves"));
        if (outOfPlay is not null) return (outOfPlay, true);
        var inPlay = _state.Cards.Values.FirstOrDefault(c => InPlay(c) && !c.Bowed && !_state.BattleActionReaction1UsedCardIds.Contains(c.Id)
            && AbilityIdsOf(c).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == "after-battle-action-resolves"));
        return inPlay is not null ? (inPlay, false) : null;
    }
    // Called from ExecuteAbilityEffects' own tail, the same spot OpenBowReactionWindow already
    // watches from -- only for an ability that is itself a genuine Battle action with an identifiable
    // performer (never for a Reaction's own resolution, already excluded by the caller).
    private bool OpenBattleActionReactionWindow(GameCommand command, AbilityDefinition ability, CardInstance? source, int closesTo)
    {
        if (ability.Timing != "Battle") return false;
        var performer = ability.Performer is not null ? (command.PerformerId is null ? null : ActingPersonality(Card(command.PerformerId)))
            : source is null ? null : ActingPersonality(source);
        if (performer is null || Definition(performer).Type != CardType.Personality) return false;
        _state.BattleActionReactionPerformerId = performer.Id;
        _state.BattleActionReaction1UsedCardIds.Clear();
        if (FindBattleActionReactionCandidate(performer) is not (CardInstance watcher, _))
        {
            _state.BattleActionReactionPerformerId = null;
            return false;
        }
        _state.PendingChoice = new("battleActionReaction", watcher.OwnerId, watcher.Id, "After the Battle action resolved: you may use this card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = watcher.OwnerId;
        Emit("reactionWindow", "Battle-action reaction window opened.", watcher.OwnerId, watcher.Id);
        return true;
    }
    // Stormless Fury's own out-of-play attach (Equip (Limited)'s own mechanism, reused as a Reaction):
    // deliberately minimal -- unlike MatchEngine.Equip, this card has no AttachRequiresKeyword/
    // AttachGrantsKeyword/HonorRequirement/AttachmentAllowed cap of its own, and its printed effect
    // never mentions paying a cost (0 Gold anyway, validated generically by ValidatePayment). No
    // Additional Action is ever granted here -- that is the entire point of this being a Reaction,
    // not Equip (Battle) -- and OpenEntryReactionWindow is deliberately not called either: Stormless
    // Fury's own printed text is a complete, closed effect ("Attach this card to him"), not a fresh
    // entering-play moment inviting a THIRD nested reaction.
    private void AttachAsOutOfPlayReaction(CardInstance card, CardInstance target, int ownerId)
    {
        Player(ownerId).Hand.Remove(card.Id);
        // Equip (Battle)'s own convention: an attachment's ProvinceIndex must be copied from its
        // wielder for AtBattle() to ever recognize it (unlike ordinary Equip(), whose target is
        // always at Zone.Home -- ProvinceIndex null either way -- this attach can land on a
        // Personality already at a battlefield).
        card.Zone = Zone.Attached; card.AttachedTo = target.Id; card.FaceUp = true; card.ProvinceIndex = target.ProvinceIndex;
        Emit("equipped", $"{Definition(card).Name} attached to {Definition(target).Name} (Reaction).", ownerId, card.Id);
    }
    // Tidy Up (core rules, step G): a Strategy or Ring played from hand is discarded once its action is
    // done. Shared by RunAbilityEffects' own tail (the ordinary, immediate path) and by
    // ExecuteAbilityEffects below (an Attack deferred behind its reaction window).
    private void SpendHandPlayedCard(PlayerState player, CardInstance? source)
    {
        if (source is not null && Definition(source).Type is CardType.Strategy or CardType.Ring && source.Zone == Zone.Hand) DiscardFate(player, source);
    }
    // Issue #320: every Attack case in RunAbilityEffects' switch does `if (ResolveOrDeferAttack(...)) return;`,
    // so an Attack that opens the attackReaction window skips the tail that discards a hand-played
    // Strategy/Ring, and ResolveAttack (run once both players pass) never discards it either -- the card
    // stayed in hand for good. Detected here rather than at each Attack case so every current and future
    // Attack (Strategic Strike, ...) is covered without boilerplate: an Attack was deferred by THIS call
    // exactly when it left a PendingAttack that was not already there. The played card is spent as the
    // window opens, like its Gold cost already is (PayForAbility) and like a hand-played Dueling Strategy
    // (IssueChallenge) -- the engine has no "being resolved" zone, and the card must not sit in hand,
    // reusable, while the opponent reacts. Step G formally follows resolution, so it is in the discard pile
    // (not yet "in limbo") while a Reaction window is open. Additional Action bookkeeping is deliberately
    // untouched: PendingAttack.Additional and ResolveAttack already carry it across the window.
    private void ExecuteAbilityEffects(GameCommand command, bool consumedGrant = false)
    {
        var pendingAttackBefore = _state.PendingAttack;
        // Asked BEFORE the effects run: a hand-played card is in a discard pile by the time the deferral below is reached.
        var fromDiscard = PlayedFromDiscard(AbilityCatalog.All[command.AbilityId!], command.CardId is null ? null : Card(command.CardId));
        var pendingDuelBefore = _state.PendingDuel;
        RunAbilityEffects(command, consumedGrant);
        if (_state.PendingDuel is { } issued && !ReferenceEquals(issued, pendingDuelBefore)) issued.SourceCardId = command.CardId;
        if (_state.PendingAttack is { } declared && !ReferenceEquals(declared, pendingAttackBefore))
        {
            declared.SourceCardId = command.CardId;
            var spent = command.CardId is null ? null : Card(command.CardId);
            SpendHandPlayedCard(Player(command.PlayerId), spent);
            // The same for a card played from a discard pile (a Discipline play, issue #331, or Daigotsu Bofana - exp's granted play,
            // issue #333): the Attack's own early return skips RunAbilityEffects' tail, so nothing there would remove it from the game.
            if (fromDiscard) RemoveFromGame(spent!);
        }
    }
    // consumedGrant (issue #181): true when the caller above already removed a matching Additional
    // Action grant for this use. Threaded through so the tail below knows whether to keep priority
    // with the same player instead of ceding it, on top of whatever this ability's own effects do
    // (Again!/Two Hands themselves add fresh grants directly in the switch, detected generically by
    // comparing the grant count before and after rather than a separate special case per card).
    private void RunAbilityEffects(GameCommand command, bool consumedGrant = false)
    {
        var id = command.AbilityId!; var ability = AbilityCatalog.All[id];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        var target = command.TargetId is null ? null : Card(command.TargetId);
        var uses = source?.AbilityUses ?? player.AbilityUses;
        uses[ability.UsageKey ?? id] = _state.TurnNumber;
        // Discipline (issue #331): decided once, up front, from where the card is BEFORE anything below moves it --
        // the discard-pile mode is the one that is removed from the game after the action resolves. So is a card played
        // from a discard pile as the additional action Daigotsu Bofana - exp grants (issue #333).
        var playedFromDiscard = PlayedFromDiscard(ability, source);
        // An Attack action executing spends this player's standing Attack-target grants of its kind
        // ("your NEXT Ranged Attack action", Yoritomo Ebumi) -- validated at declaration, spent here.
        if (AttackKind(id) is { Length: > 0 } declaredAttackKind) ConsumeAttackTargetGrants(player.Id, declaredAttackKind);
        // Times of Strife (issue #259): "after each time another player who controls no units at the
        // current battlefield announces a Battle action, he discards a card" -- checked here,
        // "announcing" the action, before its own effects below apply (matching the card's own
        // "after ... announces," not "after ... resolves"). Queued (PendingForcedDiscards),
        // drained by OpenOverconfidentDrawWindow, rather than opened as a PendingChoice directly here,
        // since this function has no safe place to defer BEFORE its own switch runs without also
        // rewriting every case's own early-return shape.
        if (ability.Timing == "Battle" && _state.CurrentBattleProvince is int cbp && !Army(player.Id, cbp).Any() && ControlsEventEffect(1 - player.Id, "times-of-strife"))
            _state.PendingForcedDiscards.Add(new(player.Id, "Times of Strife"));
        // War Weariness (issue #259): "after the resolution of the first action each phase that one
        // or more Personalities performed, bow them." Approximated as "announced" (checked here,
        // before the switch below, alongside Times of Strife's own identical announce-timed check
        // above) rather than strictly after every possible deferred-window resolution -- every
        // currently published ability either bows its own performer as part of its own cost already
        // (harmless/idempotent alongside this) or never reads its own performer's Bowed status mid-
        // resolution, so bowing here instead of strictly after is not observably different today.
        // Scoped to ExecuteAbilityEffects-dispatched abilities only (Battle/Limited/Open/etc. via
        // useAbility) -- playStrategy/equip/recruitReserve are the CONTROLLER acting generically, not
        // "a Personality performed" in the printed rules' own technical sense, so they are correctly
        // out of scope, not merely unhandled.
        if (!_state.WarWearinessBowedThisPhase && _state.Phase is Phase.Action or Phase.Battle or Phase.Dynasty
            && (ControlsEventEffect(0, "war-weariness") || ControlsEventEffect(1, "war-weariness")))
        {
            var warWearinessPerformer = ability.Performer is not null
                ? (command.PerformerId is not null ? ActingPersonality(Card(command.PerformerId)) : null)
                : (command.CardId is not null ? ActingPersonality(Card(command.CardId)) : null);
            if (warWearinessPerformer is not null && Definition(warWearinessPerformer).Type == CardType.Personality)
            {
                warWearinessPerformer.Bowed = true;
                _state.WarWearinessBowedThisPhase = true;
                Emit("warWearinessBow", $"{Definition(warWearinessPerformer).Name} bows (War Weariness).", warWearinessPerformer.OwnerId, warWearinessPerformer.Id);
            }
        }
        // The one payment for every costed ability (issue #303): exactly the AbilityCost that validation
        // priced, so no execution case below re-hardcodes (or forgets) its own Pay(...).
        PayForAbility(command, player);
        // The non-Gold cost the acting player chose (issue #337): bow or destroy the performer, paid right beside the Gold.
        PayCostOption(ability, command, player, source);
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle" or "jakuei-bow-favor")
        {
            _state.FavorPlayerId = null;
            Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
        }
        Emit("abilityUsed", $"{player.Name}: {(source is null ? "Imperial Favor" : Definition(source).Name)} — {ability.Label}.", player.Id, source?.Id);
        // Snapshot before the effect resolves so any Battle-action Honor loss it inflicts on an
        // opponent can be detected generically afterward (Law of Darkness Dojo's passive).
        var opponentHonorBeforeBattleAction = ability.Timing == "Battle" && target is not null && target.OwnerId != player.Id ? Player(target.OwnerId).Honor : (int?)null;
        // Togashi Hizumi / Tamori Ruya (issue #216): snapshotted the same way, so any Personality this
        // action newly bows can be detected generically afterward -- see OpenBowReactionWindow.
        var bowedBeforeAction = _state.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet();
        // Snapshotted by reference (not just a count): a switch case that itself grants fresh actions
        // (Again!/Two Hands/equip-battle) must never accidentally spare an unrelated PRE-EXISTING
        // grant from forfeiture below just because the total count happens to end up higher.
        var preExistingGrants = _state.AdditionalActions.Where(g => g.PlayerId == player.Id).ToList();
        // Dueling (issue #204): a challenge-issuing ability resolves generically, not through the
        // per-id switch below -- "playing" the card (paying its cost, discarding a hand-played
        // Strategy immediately) is the only part that happens right away; the printed EFFECT (the
        // duel itself) is deferred to IssueChallenge, which pauses here without ceding priority the
        // same way ResolveOrDeferAttack does, until the whole duel resolves.
        if (ability.IssuesDuel)
        {
            if (source is not null && Definition(source).Type == CardType.Strategy && source.Zone == Zone.Hand) DiscardFate(player, source);
            // A Discipline card played from a discard pile is spent the same way: out of the pile the moment it is
            // played (the duel resolves later, and the engine has no "being resolved" zone).
            if (playedFromDiscard) RemoveFromGame(source!);
            // Most Iaijutsu Personalities duel as themselves ("Kitsuki Daisuke... Daisuke duels
            // him") -- no separate Performer selection at all, unlike Steel on Steel's own chosen
            // performer. ability.Performer null means exactly this: the source card is the challenger.
            var challengerId = ability.Performer is not null ? command.PerformerId! : source!.Id;
            IssueChallenge(id, player.Id, challengerId, target!.Id, ability.DuelStat, ability.DuelRefusable);
            return;
        }
        switch (id)
        {
            case "border-first": case "border-game": CycleProvinces(player, command.CardIds!); break;
            case "shinden-choice":
                Card(command.PerformerId).Bowed = true;
                _state.PendingChoice = new("shinden", target!.OwnerId, target.Id,
                    $"{Definition(target).Name} ({target.Id}): bow and prevent straightening for this turn, or let {player.Name} gain 1 Honor.", player.Id);
                _state.PriorityPlayerId = target.OwnerId; _state.ConsecutivePasses = 0;
                return; // The action does not finish until the opponent answers.
            case "air-straighten": case "paid-straighten": case "training-keyword-conditional-straighten": case "immovable-object-straighten": case "training-stain-straighten": Straighten(target!); break;
            case "paid-bow": case "unorthodox-attack": case "chokehold": case "immovable-object-bow": case "naga-remnants-bow": case "training-disgrace-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            // Generalized Gold payment fixtures (issue #303): the Gold itself was already charged by
            // PayForAbility above; a variable-cost effect reads the amount the player actually chose.
            case "training-urgency-draw": Draw(player); break;
            case "training-charity-honor": case "training-charity-honor-capped": GainHonor(player.Id, PaidGoldAmount(command), "Training charity"); break;
            case "training-at-any-cost": GainHonor(player.Id, 1, "Training At Any Cost"); break;
            case "veteran-advisor-draw": Draw(player); break;
            case "hired-legion-draw": source!.Bowed = true; Draw(player); break;
            case "akodo-regulars-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", Definition(target!).Focus); break;
            case "aberrations-boost": if (DiscardTopOfFateDeck(player) is int focus) ApplyModifier(source!, source!.Id, id, "Force", focus); break;
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
            // Second City unwired-card audit (issue #264):
            case "dispensing-justice-destroy":
                // "If he has no attachments and is dishonorable, Kolat, Ninja, or Shadowlands" -- the ability's
                // own IfTarget status (P2): any one term is enough, and a card matching several is destroyed
                // once. Shadowlands is a Clan alignment OR the printed keyword (Rikate, Takasho, ...).
                if (!HasAttachments(target!) && EffectConditionMet(id, target))
                    DestroyUnits([target!], false, player.Id);
                else MoveHome(target!, false);
                break;
            case "bow-before-my-will":
                if (!StalwartNegatesBow(target!, player.Id))
                {
                    target!.Bowed = true;
                    if (HasClanAffiliation(Card(command.PerformerId!), "Shadowlands")) target.StraightenBlockedTurn = _state.TurnNumber;
                }
                GainHonor(player.Id, -2, "Bow Before My Will", source);
                break;
            case "surety-of-purpose":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonorFromPerformer(Card(command.PerformerId!), 2, "Surety of Purpose");
                break;
            case "unexpected-support":
                if (!StalwartNegatesBow(target!, player.Id))
                {
                    target!.Bowed = true;
                    target.StraightenBlockedTurn = _state.TurnNumber;
                }
                break;
            case "wrathful-ancestor-move": MoveHome(target!, false); break;
            case "strategic-sacrifice-toggle":
                if (target!.Bowed) Straighten(target);
                else if (!StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                GainHonor(player.Id, 2, "Strategic Sacrifice", source);
                break;
            case "weakness-of-man-toggle":
                if (target!.Bowed) Straighten(target);
                else if (!StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                break;
            case "improved-defenses-strength":
                Card(command.PerformerId!).Bowed = true;
                if (DiscardTopOfFateDeck(player) is int idFocus)
                    Player(1 - _state.ActivePlayerId).Provinces.Single(p => p.Index == _state.CurrentBattleProvince!.Value).Strength += idFocus + 1;
                break;
            // Strategy wave 13 (issue #287): Batch B of the "Not blocked; deferred for scope"
            // BLOCKERS audit. Scorn the Weak (oracle-l5r-9925): a flat -5F, then (only when the
            // target's Force is now 0, already enforced by ValidateAbility's own scorn-the-weak
            // branch above) the controller's own Honor loss and the optional dishonor of a second,
            // separately-selected Personality carried on CardIds.
            case "scorn-the-weak":
                ApplyModifier(target!, source!.Id, id, "Force", -5);
                if (CardForce(target!) == 0)
                {
                    GainHonor(target!.OwnerId, -1, "Scorn the Weak", source);
                    if (command.CardIds is { Length: 1 } scornDishonorIds) Dishonor(Card(scornDishonorIds[0]));
                }
                break;
            // Strength in Terror (oracle-l5r-7538): the chosen Paragon's own live Force reduces the
            // target's, then the optional attachment-destroy (CardIds, validated above), then the
            // flat Honor loss.
            case "strength-in-terror":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(target!, source!.Id, id, "Force", -CardForce(performer));
                if (command.CardIds is { Length: 1 } terrorAttachmentIds) DestroyAttachment(Card(terrorAttachmentIds[0]));
                GainHonor(player.Id, -3, "Strength in Terror", source);
                break;
            }
            case "strength-of-purity": ApplyModifier(Card(command.PerformerId!), source!.Id, id, "Force", 3); break;
            // Strength of the Bear (oracle-l5r-10748): bows both the chosen Performer and the
            // enemy Target (each independently Stalwart-checkable, since only the latter is
            // "an enemy's effect" from its own controller's point of view -- the Performer's own bow
            // is the ability's own printed cost/effect on your own card, same as every other self-bow
            // in this file), then straightens the Performer back if his own printed Force qualifies.
            case "strength-of-the-bear":
            {
                var performer = Card(command.PerformerId!);
                if (!StalwartNegatesBow(performer, player.Id)) performer.Bowed = true;
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (Definition(performer).Force >= 5) Straighten(performer);
                break;
            }
            // Strength of the Divine (oracle-l5r-10555): buffs the chosen (opposed, unbowed Paragon)
            // Performer first, then attacks with strength read live off that same, now-buffed
            // Performer -- monastery-fire-attack's own "performer effect, then attack read off the
            // performer" shape.
            case "strength-of-the-divine-force":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(performer, source!.Id, id, "Force", 2);
                if (ResolveOrDeferAttack(id, player, target?.Id, performer.Id, consumedGrant)) return;
                break;
            }
            case "strength-of-the-divine-honor":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(performer, source!.Id, id, "PersonalHonor", 2);
                if (ResolveOrDeferAttack(id, player, target?.Id, performer.Id, consumedGrant)) return;
                break;
            }
            // The Strength of the Spider (oracle-l5r-8362): "Destroy it if it is bowed" is an
            // alternate branch to the Force-reduction-then-conditional-bow that follows, not a
            // sequential step alongside it (the classic L5R "cripple, or outright kill if it's
            // already bowed" two-branch shape) -- DestroyAttackTarget already dispatches
            // Personality-vs-Follower destruction correctly for either kind of target.
            case "strength-of-the-spider":
            {
                var performer = Card(command.PerformerId!);
                if (target!.Bowed) DestroyAttackTarget(target, player.Id);
                else
                {
                    ApplyModifier(target, source!.Id, id, "Force", -CardForce(performer));
                    if (CardForce(target) == 0 && !StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                }
                break;
            }
            // Unexpected (oracle-l5r-9940): bows the enemy Target (an "enemy's effect," so
            // Stalwart-checkable), then the optional self-bow-for-a-draw (CardIds, validated above,
            // the performer's own choice -- a cost paid by its own controller, so no Stalwart check,
            // matching every other self-paid bow-as-cost in this file).
            case "unexpected":
            {
                var performer = Card(command.PerformerId!);
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (command.CardIds is { Length: 1 })
                {
                    performer.Bowed = true;
                    Draw(player);
                }
                break;
            }
            // Strategy wave 14 (issue #289): the seven cards' own Battle-timed execution.
            case "banzai": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
            case "pure-intent":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(performer, source!.Id, id, "Force", CardPersonalHonor(performer));
                GainHonor(player.Id, 2, "Pure Intent", source);
                break;
            }
            case "marshal-your-strength":
            {
                var unopposed = !Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any();
                var bonus = CardPersonalHonor(target!) - (unopposed ? 1 : 0);
                ApplyModifier(target!, source!.Id, id, "Force", bonus);
                ApplyModifier(target!, source!.Id, id, "Chi", bonus);
                break;
            }
            case "lost-in-transit-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "strong-defenses-boost":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(performer, source!.Id, id, "Force", 5);
                Straighten(performer);
                break;
            }
            case "trusting-instinct-move": MoveToBattlefield(Card(command.PerformerId!), _state.CurrentBattleProvince!.Value); break;
            case "visage-of-the-orochi":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(performer, source!.Id, id, "Force", Math.Max(0, 6 - CardForce(performer)));
                if (command.CardIds is { Length: 1 } orochiIds && !StalwartNegatesBow(Card(orochiIds[0]), player.Id))
                    Card(orochiIds[0]).Bowed = true;
                break;
            }
            // Batch D of the "Not blocked; deferred for scope" audit (issue #295): strategy-wave-15,
            // spell-wave-4, ring-wave-2.
            case "charge-into-danger-move":
            {
                var performer = Card(command.PerformerId!);
                if (AtBattle(performer) || Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any())
                    MoveToBattlefield(performer, _state.CurrentBattleProvince!.Value);
                if (Definition(performer).Force >= 7) Straighten(performer);
                break;
            }
            case "consuming-weakness":
            {
                var performer = Card(command.PerformerId!);
                RemoveFromGame(target!);
                ApplyModifier(performer, source!.Id, id, "Force", Keyword(performer, "Undead") ? 4 : 3);
                break;
            }
            case "fires-of-turmoil":
            {
                var discardedFocus = DiscardTopOfFateDeck(player);
                if (discardedFocus is int f && f % 2 != 0) { if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; }
                else Straighten(target!);
                if (HasToken(target!, "Madness")) MoveHome(Card(UnitHostId(target!)), false);
                break;
            }
            case "gripped-by-terror":
            {
                var performer = Card(command.PerformerId!);
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                if (Keyword(performer, "Nonhuman"))
                    foreach (var c in Army(player.Id, _state.CurrentBattleProvince!.Value))
                        ApplyModifier(c, source.Id, id, "Force", 1);
                break;
            }
            case "outnumbered":
            {
                if (Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any())
                    foreach (var c in Army(player.Id, _state.CurrentBattleProvince!.Value).Where(c => Attachments(c.Id).Any(a => Definition(a).Type == CardType.Follower)))
                        ApplyModifier(c, source!.Id, id, "Force", 1);
                foreach (var c in Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Where(c => !Attachments(c.Id).Any(a => Definition(a).Type == CardType.Follower)))
                    ApplyModifier(c, source!.Id, id, "Force", -1);
                break;
            }
            case "preserving-honor":
                _state.PendingChoice = new("preservingHonor", target!.OwnerId, target.Id,
                    $"{Definition(target).Name} ({target.Id}): bow him, or choose another location and move him there.", player.Id);
                _state.PriorityPlayerId = target.OwnerId; _state.ConsecutivePasses = 0;
                Emit("reactionWindow", "Preserving Honor: choose bow or move.", target.OwnerId, target.Id);
                return; // The action does not finish until the targeted Personality's controller answers.
            case "your-clan-needs-you":
            {
                var card = target!;
                var quote = QuotePurchase(player.Id, card.Id);
                Require(quote.Error is null && Definition(card).Type == CardType.Personality, quote.Error ?? "");
                // The shared entry sequence (issue #301: factored out once Shinden Shorai became its
                // fourth caller) -- unchanged behavior, straight into the current battlefield.
                _state.YourClanNeedsYouWatchIds.Add(card.Id);
                if (BringProvincePersonalityIntoPlay(player, card, quote, _state.CurrentBattleProvince!.Value, "Your Clan Needs You!")) return;
                break;
            }
            // Shinden Shorai (oracle-l5r-6857, issue #301): the same entry sequence, into HOME. Every
            // requirement (turn count, same province, the recruit's own quote with its Lion-Clan Honor
            // Requirement waiver) was already checked by ValidateAbility, which also priced it.
            case "shinden-shorai-recruit":
            {
                var recruit = target!;
                var recruitQuote = QuotePurchase(player.Id, recruit.Id, ignoreHonorRequirementIfPersonalityClan: "Lion");
                Require(recruitQuote.Error is null && Definition(recruit).Type == CardType.Personality, recruitQuote.Error ?? "");
                if (BringProvincePersonalityIntoPlay(player, recruit, recruitQuote, null, "Shinden Shorai", proclaimResumesTo: 1 - player.Id)) return;
                break;
            }
            case "keppo-destroy":
                if (HasReconnaissanceAtCurrentBattlefield(player.Id)) DestroyAttachment(target!);
                break;
            case "kuronada-destroy": DestroyUnits([target!], false, player.Id); break;
            case "shosuro-ritoru-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (!HasAttachments(target!) && ControlsKeyword(player.Id, "Courtier")) DestroyUnits([target!], false, player.Id);
                break;
            case "takasho-move":
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Takasho may only reinforce a battlefield with enemy units present.");
                MoveToBattlefield(source!, _state.CurrentBattleProvince!.Value);
                GrantAdditionalAction(player.Id, [source!.Id]);
                break;
            case "yogo-adi-choice":
                if (target!.Dishonorable) GainHonor(target.OwnerId, -1, "Yogo Adi", source);
                else foreach (var province in player.Provinces) province.Strength += 2;
                break;
            case "archery-range-reaction": _state.PendingAttack!.Strength += 1; break;
            case "slanderer-dishonor": source!.Bowed = true; Dishonor(target!); break;
            case "temple-of-hotei-honor": Card(command.PerformerId!).Bowed = true; GainHonor(player.Id, 1, "Temple of Hotei", source); break;
            case "tranquil-garden-ring":
                source!.Bowed = true;
                if (target!.Zone == Zone.FateDiscard) { ReturnToHand(target); DestroyUnits([source], false, player.Id); }
                else Straighten(target!);
                break;
            case "flesh-cutter-arrows-reaction": source!.Bowed = true; _state.PendingAttack!.Strength += 2; break;
            case "fubatsu-plate-straighten": Straighten(Card(source!.AttachedTo!)); break;
            case "fubatsu-plate-move": MoveHome(Card(source!.AttachedTo!), false); break;
            case "nightingale-blade-bow":
                source!.Bowed = true;
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (target!.Zone == Zone.Attached && (HasClanAffiliation(Card(source.AttachedTo!), "Dragon") || Keyword(Card(source.AttachedTo!), "Kensai")))
                    DestroyUnits([target], false, player.Id);
                break;
            case "satoshi-warfans-draw": Draw(player); break;
            case "overgrown-grove-move":
                DiscardTopOfFateDeck(player); DiscardTopOfFateDeck(player); DiscardTopOfFateDeck(player);
                MoveHome(target!, false);
                break;
            case "intervening-spirit-move":
            {
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Intervening Spirit may only move a Personality into a battlefield with enemy units present.");
                var wasAtBattle = AtBattle(target!);
                MoveToBattlefield(target!, _state.CurrentBattleProvince!.Value);
                if (!wasAtBattle) StraightenUnit(target!);
                if (Keyword(target!, "Ancestor") && Keyword(Card(source!.AttachedTo!), "Water")) GrantAdditionalAction(player.Id, null);
                break;
            }
            // Return to Action (issue #331): "move him to this battlefield. After he moves, straighten him." The move
            // is a real, negatable effect on the target (Tamori Tamoko's window, Champion of Thunder's "all" --
            // MoveOrStraightenKind); if it was negated he stays at home, and nothing is straightened: the straighten
            // is conditional on the movement having resolved. Only HE is straightened, not his unit.
            case "return-to-action-move":
                if (TryMoveToBattlefield(target!, _state.CurrentBattleProvince!.Value)) Straighten(target!);
                break;
            case "earth-flows-move":
            {
                Require(player.Id == 1 - _state.ActivePlayerId, "The Earth Flows: only the Defender may use this.");
                var wasAtBattle = AtBattle(target!);
                MoveToBattlefield(target!, _state.CurrentBattleProvince!.Value);
                if (!wasAtBattle) StraightenUnit(target!);
                break;
            }
            case "winds-of-dismissal-choice":
                Card(source!.AttachedTo!).Bowed = true;
                if (EffectConditionMet(id, target))
                    GainHonor(target!.OwnerId, -Definition(target).PersonalHonor, "Winds of Dismissal", source);
                if (Keyword(Card(source.AttachedTo!), "Air")) GainHonor(player.Id, 1, "Winds of Dismissal", source);
                break;
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
            case "goju-house-guard-return":
            {
                // Cost: Goju itself to the top or bottom of its own owner's Fate deck (controller's
                // choice). A snapshot of the target's whole unit is taken BEFORE any of it moves, so
                // "cards without attachments" is evaluated once, simultaneously -- a Personality with
                // two Followers is excluded even though removing both leaves her with none by the end.
                ToOwnerDeck(source!, command.DeckPosition!);
                var unit = Attachments(target!.Id).Append(target).ToArray();
                foreach (var card in unit.Where(c => !HasAttachments(c)).ToArray()) ToOwnerDeck(card, "Bottom");
                break;
            }
            case "pearl-of-embers-straighten": source!.Bowed = true; Straighten(Card(source.AttachedTo!)); break;
            case "ritual-preparation-straighten": Straighten(target!); break;
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
            case "effortless-counterattack-negate":
                // Protection (issue #212): a no-op against a protected performer's own action, the
                // same "still legal to play, its own effect just does nothing" shape StalwartNegatesBow
                // already uses -- PendingAdditionalAction is left set, so it resolves normally once
                // this reaction window closes (see the "additionalActionReaction" branch in Apply()).
                if (!Protected(ActionPerformer(_state.PendingAdditionalAction!)))
                {
                    _state.PendingAdditionalAction = null;
                }
                else Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", player.Id, source?.Id);
                break;
            case "training-discipline-ability": GainHonor(player.Id, 1, "Training Discipline"); break;
            case "training-bow-enemy": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "training-bow-at-battle": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "training-straighten-at-battle": Straighten(target!); break;
            case "training-bow-any": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "effortless-counterattack-move": MoveHome(target!, false); break;
            case "training-honor-strike": GainHonor(target!.OwnerId, -2, "Training strike", source); break;
            case "training-destroy-terrain": DiscardTerrain(target!); player.TerrainDestroyedThisBattle=true; break; // Ring of Water's own recruitment condition (issue #194)
            case "carpenter-castle-siege":
                Card(command.PerformerId).Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "shiro-shiba-melee": if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return; break;
            case "training-create-token": CreateToken(target!, "Training", id, "Force", 1); break;
            case "training-move-token": MoveToken(target!, target!.Tokens[0], Card(command.PerformerId!)); break;
            case "training-destroy-token": DestroyToken(target!, target!.Tokens[0]); break;
            case "monastery-fire-token": CreateToken(target!, "Fire", id, "Force", 1); break;
            case "monastery-fire-attack":
            {
                var performer = Card(command.PerformerId!);
                DestroyToken(performer, performer.Tokens.First(t => t.Type == "Fire"));
                if (ResolveOrDeferAttack(id, player, target?.Id, performer.Id, consumedGrant)) return;
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
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "mitsu-fire-tokens":
                while (TokenCount(source!, "Fire") < 5) CreateToken(source!, "Fire", id, "Force", 1);
                break;
            case "mitsu-fire-attack":
            {
                var tokenIds = command.CardIds ?? [];
                foreach (var tid in tokenIds) DestroyToken(source!, source!.Tokens.First(t => t.Id == tid));
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant, tokenIds.Length)) return;
                break;
            }
            case "sakata-fire-token": CreateToken(source!, "Fire", id, "Force", 1); break;
            case "sakata-sake-token": CreateToken(source!, "Sake", id, "Force", 1); break;
            case "sakata-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "shinjo-eun-sahng-attack": case "shosuro-konishi-attack": case "tsuruchi-kosoko-attack": case "stalking-tiger-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            // Tsuruchi Kosoko - exp (oracle-l5r-9831): a plain Ranged 6 Attack whose printed Follower
            // exception is declared on the ability itself (FollowerException) -- nothing to do here but
            // attack.
            case "tsuruchi-kosoko-exp-attack":
            case "training-followed-recon-attack": case "training-followed-dishonorable-melee-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            // Bayushi Kahoku - exp (oracle-l5r-807): "Target an enemy Personality: Give him -4F. Ranged 3
            // Attack." The -4F applies first (to TargetId), then the Attack resolves against its own
            // separately chosen target (CardIds, validated in ValidateAbility) -- which may be the very
            // Personality just weakened, whose Force the resolution-time comparison re-reads.
            case "kahoku-exp-attack":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                if (ResolveOrDeferAttack(id, player, command.CardIds is { Length: 1 } kahokuAttackIds ? kahokuAttackIds[0] : null, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            // Yoritomo Ebumi (oracle-l5r-9832): "One Ranged Attack from your next Ranged Attack action this
            // battle may target a Personality with attached Followers. If you have Reconnaissance, you may
            // take an additional Battle action." A standing grant (AttackFollowerException shape (e)),
            // then -- only with Reconnaissance at the current battlefield -- an additional Battle action.
            case "yoritomo-ebumi-grant":
                GrantAttackFollowerException(player.Id, new AttackFollowerException("Ranged"), source!.Id);
                if (HasReconnaissanceAtCurrentBattlefield(player.Id)) GrantAdditionalAction(player.Id, null);
                break;
            // Kakita Kazan (oracle-l5r-4185): the "enemy dishonorable" legality is the ability's own
            // TargetMustBe (checked in ValidateAbility); the effect is unconditional. The Honor gain is an
            // action performed by Kazan, so a dishonorable Kazan is rehonored instead (GainHonorFromPerformer).
            case "kazan-shame":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(target!.OwnerId, -2, "Kakita Kazan", source);
                GainHonorFromPerformer(source!, 2, "Kakita Kazan");
                break;
            // Hiruma Nitani (oracle-l5r-3311): "Bow Nitani unless you have Reconnaissance" is a cost paid up
            // front; the +2 strength against a Shadowlands target is AttackStrength's IfTarget condition.
            case "hiruma-nitani-attack":
                if (!HasReconnaissanceAtCurrentBattlefield(player.Id)) source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            // Kuni Shinoda - exp (oracle-l5r-10276): bow, then destroy if Shadowlands (IfTarget) -- a lone
            // attachment as an attachment, a Personality as a whole unit (mimori-destroy's own dispatch); the
            // destroy is a separate clause from the bow, so a Stalwart target whose bow was negated still dies.
            case "shinoda-exp-bow-destroy":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (EffectConditionMet(id, target))
                {
                    if (target!.Zone == Zone.Attached) DestroyAttachment(target); else DestroyUnits([target], false, player.Id);
                }
                break;
            case "tsuruchi-isas-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "false-ring-of-water-attack":
                if (source!.Zone != Zone.Hand) source.Bowed = true; // the discard-from-hand case is handled generically in the tail below
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "bayushi-kahoku-attack": case "asako-ume-attack":
            case "dark-naga-scouts-attack": case "horse-archers-attack": case "hiruma-sniper-attack":
            case "spawn-of-vritra-attack": case "horseback-warriors-attack": case "khol-regulars-attack": case "unparalleled-sword-attack": case "training-weapon-attack": case "moshi-ikako-attack": case "kamina-exp-attack": case "shuji-attack": case "sankaku-yari-attack":
            case "shikaro-attack": case "moto-ming-gwok-attack": case "takasho-attack": case "champion-of-thunder-attack": case "nikutai-attack": case "village-raiders-attack": case "chiyo-attack":
            case "ebizo-attack": case "training-recon-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            // Legulus's Legion (oracle-l5r-10899): "Battle, bow: Melee Attack with strength equal to
            // this Follower's Force" -- the bow is a printed cost, applied here before resolving the
            // Attack itself (kasuru-draw's own `source.Bowed = true` shape), not something
            // ResolveOrDeferAttack ever does on its own behalf.
            case "legulus-legion-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, source.Id, consumedGrant)) return;
                break;
            case "khol-regulars-reaction": _state.PendingAttack!.Strength += 2; break;
            case "fallow-lands-reaction": _state.PendingAttack!.Strength -= 2; break;
            case "strong-defenses-reaction": _state.PendingAttack!.Strength -= 3; break;
            case "trusting-instinct-reaction": _state.PendingAttack!.Strength -= 2; break;
            // Stormless Fury (issue #276 follow-up): Equip (Limited)'s own attach mechanism, reused
            // here as a Reaction, deliberately without granting Equip (Battle)'s Additional Action.
            case "stormless-fury-attach":
                AttachAsOutOfPlayReaction(source!, Card(_state.BattleActionReactionPerformerId!), player.Id);
                _state.BattleActionReaction1UsedCardIds.Add(source!.Id);
                break;
            case "stormless-fury-bow":
            {
                var performer = Card(_state.BattleActionReactionPerformerId!);
                if (!StalwartNegatesBow(performer, player.Id)) performer.Bowed = true;
                break;
            }
            // Tsuruchi Yakusho (issue #276 follow-up): sets the transient immunity flag every existing
            // Attack-strength-reducing Reaction now checks (see the Require block above); cleared by
            // ResolveAttackNow once the specific Ranged Attack this action produces actually resolves.
            case "tsuruchi-yakusho-immunity": _state.RangedAttackStrengthReductionImmune = true; break;
            case "low-market-equip":
            {
                source!.Bowed = true;
                var performer = Card(command.PerformerId!);
                var attachment = target!;
                player.Hand.Remove(attachment.Id);
                attachment.Zone = Zone.Attached; attachment.AttachedTo = performer.Id; attachment.FaceUp = true; attachment.ProvinceIndex = performer.ProvinceIndex;
                if (Definition(attachment).AttachGrantsKeyword is string granted) GrantKeyword(performer, granted, attachment.Id, "Attachment");
                Emit("equipped", $"{Definition(attachment).Name} attached to {Definition(performer).Name} via Low Market.", player.Id, attachment.Id);
                if (OpenEntryReactionWindow(player.Id, attachment, 1 - player.Id)) return;
                break;
            }
            case "lady-matsus-rage-bow-destroy":
            {
                // "Discard this card" is the cost -- she leaves play by discard, not destruction (her
                // own controller-scoped destroy-redirect trait no longer applies to anything once she
                // is gone, including this very card, which was never destroyed in the first place).
                source!.Zone = Zone.DynastyDiscard;
                Emit("celestialDiscarded", $"{Definition(source).Name} was discarded to use its Battle action.", player.Id, source.Id);
                if (!StalwartNegatesBow(target!, player.Id))
                {
                    target!.Bowed = true;
                    _state.PendingLadyMatsusRageWatches.Add(new(target.Id, player.Id));
                }
                break;
            }
            case "scouring-flood-move-home": source!.Bowed = true; MoveHome(target!, false); break;
            case "earths-hunger-destroy":
                DestroyUnits([target!], actingPlayerId: player.Id); // the bow is its cost option (PayCostOption)
                break;
            case "sanctuary-of-the-waves-move":
            {
                source!.Bowed = true;
                if (Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any())
                {
                    var wasAtBattle = AtBattle(target!);
                    MoveToBattlefield(target!, _state.CurrentBattleProvince!.Value);
                    // Plain Straighten, not StraightenUnit: unlike Intervening Spirit/The Earth Flows
                    // (neither of which bows itself as a cost), this card's own default target IS its
                    // own wielder -- StraightenUnit would straighten this very Spell's own attachments,
                    // undoing the bow this case just paid as a cost, since the Spell is still attached
                    // to the wielder it may be straightening.
                    if (!wasAtBattle) Straighten(target!);
                }
                break;
            }
            case "kitsu-akai-create": if (CreatePersonality(player.Id, "oracle-l5r-9785")) return; break;
            // Personality wave 21 (issue #299, Batch E). Akodo Chiyo's "chiyo-attack" shares the
            // attack-execution case below.
            // Kitsune Ohsuki / Kitsune Tokoru: created from INSIDE this Personality's own already-open
            // entry window (duringEntryReaction), so CreatePersonality never returns true here.
            case "ohsuki-create": CreatePersonality(player.Id, "oracle-l5r-10023", duringEntryReaction: true); break;
            case "tokoru-create": CreatePersonality(player.Id, "oracle-l5r-10025", duringEntryReaction: true); break;
            // Daigotsu Bofana - exp: the additional action to play the chosen Dark Virtue from the discard pile.
            case "bofana-exp-dark-virtue": GrantAdditionalAction(player.Id, [target!.Id], fromDiscard: true); break;
            case "ohsuki-sparrow-honor": GainHonor(player.Id, 3, "Ohsuki's Sparrow", source); break;
            // "Destroy it if it is an attachment" is a separate clause from "Bow it" -- a Stalwart
            // attachment whose bow was negated is still destroyed.
            case "tokoru-bear-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (target!.Zone == Zone.Attached) DestroyAttachment(target);
                break;
            case "kaido-force-zero": ApplyModifier(target!, source!.Id, id, "Force", -CardForce(target!)); break;
            case "miura-force-penalty": ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(source!)); break;
            case "mai-move-force-penalty":
                MoveToBattlefield(source!, _state.CurrentBattleProvince!.Value);
                // "If she is now there" -- always true once the move above has run (nothing can negate a
                // move TO a battlefield), but checked rather than assumed so the printed conditional
                // is what the code actually says.
                if (AtBattle(source!)) ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(source!));
                break;
            case "kinto-move": MoveHome(target!, false); break;
            case "maratai-statue":
                source!.Bowed = true;
                GainHonor(player.Id, 2, "Kakita Maratai", source);
                _state.FavorPlayerId = player.Id;
                Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id);
                break;
            // Personality wave 22 / region-wave-3 (issue #301, Batch F).
            // Straightening a Holding/Air Shugenja: the plain Straighten() choke point (Tamoko/Hizumi's
            // own negations and blocks already live inside it).
            case "kodo-straighten": case "munefusa-straighten": case "mitsuko-straighten": Straighten(target!); break;
            case "munefusa-move":
                MoveHome(target!, false);
                // "If he moved": a negated move (Tamoko's window, Champion of Thunder) leaves him at the
                // battlefield, so where he actually ended up is the honest test -- validated as at the
                // current battlefield above, so Home afterwards can only mean this action moved him.
                if (target!.Zone == Zone.Home) GainHonor(player.Id, 1, "Asahina Munefusa", source);
                break;
            case "rikate-detach-chi":
            {
                // Champion of Thunder's own "negate all effects of the action on the chosen card" (the
                // signal is set for this one re-invocation only, and this ability may pause below for the
                // controller's choice) -- nothing applies to a card it protects, so neither the attachment
                // return nor the -2C is even offered.
                if (NegatesEffect(target!, "all")) break;
                var detachable = Attachments(target!.Id).ToArray();
                if (detachable.Length > 0)
                {
                    _state.PendingChoice = new("rikateDetach", target.OwnerId, target.Id,
                        $"{Definition(target).Name} ({target.Id}): you may put any attachments in this unit into your hand. He will then get -2C.", player.Id);
                    _state.PriorityPlayerId = target.OwnerId; _state.ConsecutivePasses = 0;
                    Emit("reactionWindow", "Asako Rikate: the controller may return attachments to hand.", target.OwnerId, target.Id);
                    return; // The action does not finish until the targeted Personality's controller answers.
                }
                ApplyModifier(target, "rikate-detach-chi", "rikate-detach-chi", "Chi", -2);
                break;
            }
            case "nori-bow-personality":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (target!.Dishonorable) GainHonor(target.OwnerId, -2, "Bayushi Nori", source);
                break;
            case "nori-bow-attachments":
                // "All of his attachments" -- every card attached to the target Personality, each one
                // independently Stalwart-checkable (an attachment can carry the keyword itself).
                foreach (var attachment in Attachments(target!.Id).ToArray())
                    if (!StalwartNegatesBow(attachment, player.Id)) attachment.Bowed = true;
                if (target.Dishonorable) GainHonor(target.OwnerId, -2, "Bayushi Nori", source);
                break;
            // "Destroy it": a lone attachment is destroyed as an attachment (the same dispatch
            // DestroyAttackTarget uses), a Personality as a whole unit.
            case "mimori-destroy":
                if (target!.Zone == Zone.Attached) DestroyAttachment(target); else DestroyUnits([target], false, player.Id);
                break;
            case "mitsuko-bow-destroy":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                // "Destroy him if he is Kolat, Ninja, or Shadowlands" -- a separate clause from the bow, so
                // a Stalwart target whose bow was negated is still destroyed (tokoru-bear-bow's own shape).
                if (EffectConditionMet(id, target)) DestroyUnits([target!], false, player.Id);
                break;
            case "hawado-move-home":
            case "hawado-move-battlefield":
            {
                RemoveFromGame(target!); // the printed cost
                var wasAt = (source!.Zone, source.ProvinceIndex);
                if (id == "hawado-move-home") MoveHome(source, false);
                else MoveToBattlefield(source, command.Province!.Value);
                // "If she moved": already being at the destination (or a negated move) is no move at all.
                if ((source.Zone, source.ProvinceIndex) != wasAt) Straighten(source);
                break;
            }
            case "rin-shame":
                source!.Bowed = true; // "Bow Rin" -- the printed cost, paid up front (never Stalwart-negatable: her own card)
                _state.PendingChoice = new("rinShame", target!.OwnerId, target.Id,
                    $"{Definition(target).Name} ({target.Id}): dishonor him, or be bowed and unable to straighten this turn.", player.Id);
                _state.PriorityPlayerId = target.OwnerId; _state.ConsecutivePasses = 0;
                Emit("reactionWindow", "Shosuro Rin: the controller may dishonor him.", target.OwnerId, target.Id);
                return; // The action does not finish until the targeted Personality's controller answers.
            case "neiru-equip":
            {
                var spell = target!;
                player.Hand.Remove(spell.Id);
                spell.Zone = Zone.Attached; spell.AttachedTo = source!.Id; spell.FaceUp = true; spell.ProvinceIndex = source.ProvinceIndex;
                if (Definition(spell).AttachGrantsKeyword is string neiruGranted) GrantKeyword(source, neiruGranted, spell.Id, "Attachment");
                Emit("equipped", $"{Definition(spell).Name} attached to {Definition(source).Name}.", player.Id, spell.Id);
                // A Spell's own "after entering play" Reaction (if any); unlike equip-battle, no Additional
                // Action is granted -- this is an Open action, not a Battle one.
                if (OpenEntryReactionWindow(player.Id, spell, 1 - player.Id)) return;
                break;
            }
            case "sutigu-bow":
                // "Bow Sutigu or one of his Weapons" is the cost option (PayCostOption): never negated.
                // "Negate its straightening (until the turn ends) if it is an attachment" -- stated
                // before "Bow it," and independent of whether the bow itself is then negated.
                if (target!.Zone == Zone.Attached) target.StraightenBlockedTurn = _state.TurnNumber;
                if (!StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                break;
            case "kitsu-akai-battle":
                // No StalwartNegatesBow check -- unlike lady-matsus-rage-bow-destroy's own bow
                // (inflicted on an enemy target), this bow is the performer's own chosen cost
                // payment, the same distinction earths-hunger-destroy's own cost-bow above already
                // draws.
                Card(command.PerformerId!).Bowed = true;
                DestroyUnits([target!], actingPlayerId: player.Id);
                GainHonor(player.Id, 1, "Kitsu Akai", source);
                break;
            case "cavalry-flankers-attack": case "murderous-thieves-attack":
                source!.Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "training-honor-cost-attack":
                // The Honor cost is paid up front, like the Gold cost at the top of UseAbility --
                // not contingent on the Attack actually resolving or hitting.
                GainHonor(player.Id, -2, "Training Honor-cost Attack", source);
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "training-gain-reconnaissance": GrantReconnaissance(player.Id, 1 - player.Id, command.Province!.Value); break;
            case "training-reconnaissance-strike":
                if (HasReconnaissanceAtCurrentBattlefield(player.Id)) GainHonor(target!.OwnerId, -2, "Training Reconnaissance strike", source);
                break;
            // Training-only (issue #309): "You have Reconnaissance at the current battlefield" (a Battle action of either side).
            case "training-gain-reconnaissance-here": GrantReconnaissanceAtCurrentBattlefield(player.Id); break;
            case "tactical-advantage":
                ApplyModifier(Card(command.PerformerId!), "tactical-advantage", "tactical-advantage", "Force", Definition(target!).Focus);
                DiscardFate(player, target!);
                break;
            case "training-keyword-conditional-force-boost": ApplyModifier(Card(command.PerformerId!), id, id, "Force", 2); break;
            case "equip-battle":
            {
                var performer = Card(command.PerformerId!);
                var attachment = target!;
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
            case "training-unit-force-penalty":
                foreach (var member in UnitMembers(target!)) ApplyModifier(member, source!.Id, id, "Force", -1);
                break;
            case "horibe-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "horibe-straighten": Straighten(target!); break;
            // Lion Clan Personality review (issue #198):
            case "dairuko-exp-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (Keyword(target!, "Unaligned")) DestroyUnits([target!], false, player.Id);
                break;
            case "furu-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "furu-straighten": Straighten(target!); break;
            case "kano-token": CreateToken(source!, "Master", id); break;
            case "kano-destroy-token":
                source!.Bowed = true;
                DestroyToken(source, source.Tokens.First(t => t.Type == "Master"));
                GrantAdditionalAction(player.Id, null); GrantAdditionalAction(player.Id, null);
                break;
            case "michitsu-destroy": DestroyAttachment(target!); break;
            case "kamina-boost": source!.Bowed = true; ApplyModifier(target!, source.Id, id, "Force", 4); break;
            case "tsudoken-exp-bow": Straighten(source!); if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "tsudoken-exp-straighten": Straighten(source!); Straighten(target!); break;
            case "tsudoken-exp2-entry": GainHonor(player.Id, -3, "Akodo Tsudoken", source); break;
            case "tsudoken-exp2-straighten": Straighten(target!); break;
            case "nojima-entry": GainHonor(player.Id, -1, "Akodo Nojima", source); break;
            case "aimi-exp-discard":
            {
                source!.Bowed = true;
                var discardFocus = Definition(target!).Focus;
                DiscardFate(player, target!);
                var shibaIaimikoBonus = _state.Cards.Values.Any(c => Definition(c).Name == "Shiba Iaimiko" && InPlay(c)) ? 1 : 0;
                GainHonor(player.Id, discardFocus + shibaIaimikoBonus, "Ikoma Aimi");
                break;
            }
            case "ayumi-entry": GainHonor(player.Id, -2, "Ikoma Ayumi", source); break;
            case "masumi-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "brawl-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "brawl-destroy":
                // A Terrain shares training-destroy-terrain's removal and Ring of Water's own
                // "destroyed a Terrain" condition (issue #194); a Region leaves its province.
                if (target!.Zone == Zone.Region) DestroyRegion(target);
                else { DiscardTerrain(target); player.TerrainDestroyedThisBattle = true; }
                break;
            case "tsukasa-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "tsukasa-straighten": Straighten(target!); break;
            case "arata-force-penalty": ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(source)); GainHonor(player.Id, 1, "Matsu Arata"); break;
            case "atsumichi-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "atsumichi-straighten": Straighten(target!); break;
            case "tetsuo-destroy-attachment": DestroyAttachment(target!); break;
            case "tetsuo-destroy-weaker-weaponed": case "training-destroy-no-attachments": DestroyUnits([target!], true, player.Id); break;
            // Bowing the target Farm IS the cost; the effect is the counter increment, not applied to
            // the Farm itself. Consumed by PersonalityForce/FollowerForce (Keyword "Ashigaru" + AtBattle).
            case "training-ashigaru-boost": case "ashigaru-elite-boost": target!.Bowed = true; _state.AshigaruBoost[player.Id]++; CheckTsujikenWatch(target); break;
            case "shunsuke-make-dead": MakeDead(target!, true); break;
            case "training-make-dead": MakeDead(target!, true); break;
            case "training-attack-boost": _state.PendingAttack!.Strength += 2; break;
            case "training-attack-negate":
                // Protection (issue #212): same no-op-against-a-protected-performer shape as
                // effortless-counterattack-negate above.
                if (!Protected(Card(_state.PendingAttack!.SourceId!))) _state.PendingAttack!.Strength = 0;
                else Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", player.Id, source?.Id);
                break;
            case "a-close-call-negate":
                // Same shape as training-attack-negate above -- "negate its effects" is the whole
                // Attack, achieved by zeroing the strength ResolveAttackNow later reads (a hit needs
                // strength > 0), still respecting Protection.
                if (!Protected(Card(_state.PendingAttack!.SourceId!))) _state.PendingAttack!.Strength = 0;
                else Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", player.Id, source?.Id);
                break;
            case "a-close-call-boost": ApplyModifier(target!, source!.Id, id, "Force", 2); break;
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
            case "elite-sentry-destroy": case "thunderous-legion-destroy": DestroyUnits([target!], true, player.Id); break;
            case "brothers-great-lake-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "elite-sentry-straighten":
            {
                var personality = Card(source!.AttachedTo!);
                Straighten(personality);
                foreach (var attachment in Attachments(personality.Id)) Straighten(attachment);
                break;
            }
            case "furugaro-charge":
                DestroyUnits([source!], false, player.Id);
                CreateToken(target!, "Charging", id);
                CreateToken(target!, "Charging", id);
                break;
            case "hunter-of-harmony-return":
                RemoveFromGame(source!);
                ReturnToHand(target!);
                break;
            case "volcano-tattoo-destroy":
            {
                var performer = Card(command.PerformerId!);
                performer.Bowed = true;
                var destroyedAttachment = Definition(target!).Type == CardType.Follower;
                DestroyUnits([target!], false, player.Id);
                if (destroyedAttachment && Keyword(performer, "Fire")) Straighten(performer);
                break;
            }
            case "panku-madness": CreateToken(target!, "Madness", id); break;
            // Rings (issue #194): every recruit case shares the same shape -- leave hand, enter play
            // face up. False Rings additionally lose 2 Honor (their own printed cost); the three
            // real Rings do not.
            case "ring-of-air-recruit": case "ring-of-water-recruit": case "ring-of-earth-recruit": case "ring-of-fire-recruit":
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true;
                break;
            case "false-ring-of-air-recruit": case "false-ring-of-water-recruit": case "false-ring-of-earth-recruit":
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
            case "ring-of-earth-raise": if (source!.Zone != Zone.Hand) source.Bowed = true; Player(command.TargetPlayerId!.Value).Provinces[command.Province!.Value].Strength += 3; break;
            case "ring-of-earth-lower": if (source!.Zone != Zone.Hand) source.Bowed = true; Player(command.TargetPlayerId!.Value).Provinces[command.Province!.Value].Strength -= 3; break;
            case "ring-of-fire-destroy":
            {
                if (source!.Zone != Zone.Hand) source.Bowed = true;
                var performer = Card(command.PerformerId!);
                DestroyUnits([performer], actingPlayerId: player.Id);
                DestroyUnits([target!], actingPlayerId: player.Id);
                break;
            }
            case "rokai-negate-destruction":
            {
                // Reaction-Timed, not an Action -- Lady Matsu's Rage's own trait is scoped to "your
                // action resolves," so intentionally left without actingPlayerId here.
                var pending = _state.PendingNegatableAbility!;
                DestroyUnits([source!]); // Rokai's own cost -- destroying himself, unaffected by the negation signal set just below
                _state.NegatedEffects.Add(new("destroy", pending.TargetId!));
                break;
            }
            case "tamoko-negate":
            {
                var pending = _state.PendingNegatableAbility!;
                _state.NegatedEffects.Add(new(MoveOrStraightenKind(pending.AbilityId!)!, pending.TargetId!));
                break;
            }
            case "tamoko-open": _state.PendingTamokoHonorChecks.Add(new(target!.Id, player.Id)); break;
            // Champion of Thunder: unlike Rokai/Tamoko above, the affected card is a genuine player
            // CHOICE (this ability's own Target, "own-unit-member"), not auto-determined from the
            // deferred command -- "any card in this unit," including one of several simultaneously
            // affected by the same action. NegatedEffects "all" reaches every effect kind
            // (StalwartNegatesBow's own bow check, ApplyModifier, DestroyUnits, MoveHome, Straighten)
            // and is deliberately not consumed here (see NegatesEffect's own doc comment).
            case "champion-of-thunder-negate": _state.NegatedEffects.Add(new("all", target!.Id)); break;
            // Brothers of Goemon: auto-determined from the deferred command's own TargetId, the same
            // shape Rokai/Tamoko already use (no separate choice of card -- Brothers' own Target is
            // "none"). Ring control was already checked before this reaction was even offered (see
            // BeforeActionReactionApplies); "bow" only ever reaches StalwartNegatesBow's own
            // check, never ApplyModifier/DestroyUnits/MoveHome/Straighten -- the Force penalty in
            // costly-feud's own worked example still applies in full, only the resulting bow is
            // negated.
            case "brothers-goemon-negate":
            {
                var pending = _state.PendingNegatableAbility!;
                _state.NegatedEffects.Add(new("bow", pending.TargetId!));
                break;
            }
            // Akodo Kakihara (issue #305): "Give him a Force bonus equal to his Personal Honor." The Force
            // bonus convention is every other one-shot "give him +N Force" effect's (Banzai!, Pure Intent,
            // Marshal Your Strength, Strength of Purity): an AppliedModifier "Force" that lasts until the end
            // of the turn (cleared in FinishTurn) and is a snapshot of his Personal Honor at the moment the
            // reaction resolves, not a live link. Either window's bonus lands BEFORE the deferred action
            // or Attack actually resolves, so an Attack's own strength-versus-Force comparison
            // (ResolveAttackNow, re-read at resolution) sees the raised Force. Added to Modifiers directly
            // rather than through ApplyModifier (issue #318): ApplyModifier honors Champion of Thunder's "all"
            // negation, and that negates the ACTION's effects on a card, not another reaction played in the
            // same window -- with several reactions now takeable in either order, routing his bonus through
            // it would let a Champion played first silently cancel it.
            case "kakihara-force-bonus":
            case "kakihara-attack-force-bonus":
                source!.Modifiers.Add(new(source.Id, id, "Force", CardPersonalHonor(source)));
                break;
            // Training fixture (issue #318): "after another player's action targets a card of yours, bow this
            // card and every other unbowed card you control that has a before-action reaction" -- a reaction
            // whose own resolution removes other reactions' eligibility mid-window (a bowed source cannot be
            // used), proving the window re-evaluates at every opportunity.
            case "training-before-action-suppress":
                foreach (var other in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Id != source!.Id && InPlay(c) && !c.Bowed
                    && AbilityIdsOf(c).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == "before-action-resolves")).ToArray())
                    other.Bowed = true;
                source!.Bowed = true;
                break;
            case "scholarly-hermit-search":
                source!.Bowed = true;
                player.Hand.Add(target!.Id); target.Zone = Zone.Hand; target.FaceUp = true;
                DestroyUnits([source], false, player.Id);
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
                DestroyUnits([target!], false, player.Id);
                DestroyUnits([source], false, player.Id);
                GainHonor(player.Id, -2, "Unseen Assassins", source);
                break;
            case "asahina-straighten":
                Straighten(target!);
                GainHonor(player.Id, 1, "Asahina House Guard", source);
                break;
            // Asako House Guard (oracle-l5r-543): "Set his Force equal to his Chi or Personal
            // Honor. Gain 1 Honor" -- daifuke-swap-force's own delta-to-target-value shape, just
            // reading Chi/PersonalHonor instead of another card's Force.
            case "asako-house-guard-force-chi":
                ApplyModifier(target!, source!.Id, id, "Force", PersonalityChi(target!) - CardForce(target!));
                GainHonor(player.Id, 1, "Asako House Guard", source);
                break;
            case "asako-house-guard-force-honor":
                ApplyModifier(target!, source!.Id, id, "Force", CardPersonalHonor(target!) - CardForce(target!));
                GainHonor(player.Id, 1, "Asako House Guard", source);
                break;
            // Village Guardian (oracle-l5r-9162): "Draw one card for each player who has had a
            // province destroyed this game" -- MatchState.ProvinceEverDestroyed is set once, at
            // DestroyProvince's own single choke point, per player id.
            case "village-guardian-draw":
                for (var p = 0; p < _state.ProvinceEverDestroyed.Length; p++) if (_state.ProvinceEverDestroyed[p]) Draw(player);
                break;
            case "tsujiken-watch-holding":
                _state.TsujikenWatches.Add(new(source!.Id, target!.Id));
                Emit("tsujikenWatch", $"{Definition(source).Name} is watching {Definition(target).Name}.", player.Id, target.Id);
                break;
            case "tsujiken-straighten-holding":
                // "You may bow it [Tsujiken] if its [the target Holding's] base Gold Cost is 4 or
                // lower" -- bowing Tsujiken is his own ability's usual cost, waived (made optional)
                // for a cheap target; defaulting to the player's own better outcome (not bowing him)
                // when the waiver applies, rather than modeling a separate yes/no sub-choice. The
                // target itself is always straightened here, never bowed.
                if (Definition(target!).GoldCost > 4) source!.Bowed = true;
                Straighten(target!);
                break;
            case "unimpeachable-name-reaction":
                if (source!.Zone == Zone.Hand) player.Hand.Remove(source.Id);
                source.Zone = Zone.FateDiscard; source.FaceUp = true;
                _state.HonorLossReductionPlayerId = player.Id;
                if (target is not null) Rehonor(target);
                Emit("unimpeachableNameReaction", $"{Definition(source).Name} reduces Honor loss from this action by 3 (minimum 0){(target is not null ? $"; {Definition(target).Name} was rehonored" : "")}.", player.Id, source.Id);
                break;
            case "unimpeachable-name-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(player.Id, 2, "Unimpeachable Name", source);
                CheckTsujikenWatch(target!);
                break;
            case "singh-remnants-attach": case "training-personality-entry-watch":
            {
                var enteredPersonality = Card(_state.PendingChoice!.CardId!);
                var attachDef = Definition(source!);
                if (source!.Zone == Zone.Hand) player.Hand.Remove(source.Id);
                source.Zone = Zone.Attached; source.AttachedTo = enteredPersonality.Id; source.FaceUp = true;
                if (attachDef.AttachGrantsKeyword is string granted) GrantKeyword(enteredPersonality, granted, source.Id, "Attachment");
                GainHonor(player.Id, 1, attachDef.Name, source);
                Emit("equipped", $"{attachDef.Name} attached to {Definition(enteredPersonality).Name}.", player.Id, source.Id);
                break;
            }
            case "clay-kshatriya-invest":
                CreateToken(source!, "Invest", id, "Force", 1);
                CreateToken(source!, "Invest", id, "Force", 1);
                break;
            case "training-invest":
                CreateToken(source!, "Invest", id, "Force", 1);
                break;
            case "kharmic-discard":
            {
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
            // Dragon Clan review (issue #207).
            case "fujimura-expose":
                source!.Bowed = true;
                GainHonor(target!.OwnerId, -2, "Kitsuki Fujimura", target);
                break;
            case "kinaro-exp-bow":
                source!.Bowed = true;
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Name == "Kakita Ichigiku")) GainHonor(player.Id, 1, "Kitsuki Kinaro", source);
                break;
            case "nakai-exp-weaken": ApplyModifier(target!, source!.Id, id, "Force", target!.Dishonorable ? -3 : -2); break;
            case "nubane-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "hatsuto-weaken": ApplyModifier(target!, source!.Id, id, "Force", -3); break;
            case "houken-straighten": StraightenUnit(target!); break;
            case "houken-exp-straighten":
                StraightenUnit(source!);
                if (Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any()) MoveToBattlefield(source!, _state.CurrentBattleProvince!.Value);
                else MoveHome(source!, false);
                break;
            case "kojinrue-destroy": source!.Bowed = true; DestroyUnits([target!], actingPlayerId: player.Id); break;
            case "xijkt-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                break;
            case "shosei-honor":
                GainHonorFromPerformer(source!, 1, "Tamori Shosei");
                break;
            case "kouzei-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "michi-move":
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Michi may only reinforce a battlefield where he would be opposed.");
                MoveToBattlefield(source!, _state.CurrentBattleProvince!.Value);
                StraightenUnit(source!);
                break;
            case "mitoshi-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "mitoshi-straighten": Straighten(target!); break;
            case "shikei-draw": Draw(player); break;
            case "yozo-exp-destroy":
                if (HasClanAffiliation(target!, "Lion")) RemoveFromGame(target!); else DestroyUnits([target!], actingPlayerId: player.Id);
                break;
            case "jinai-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "muzu-bow":
                if (IsAttacking(target!) || !HasAttachments(target!)) { if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; }
                GainHonor(player.Id, 1, "Tamori Muzu", source);
                break;
            case "hiroto-fire-tokens":
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                break;
            case "hiroto-bow":
                if (target!.Zone == Zone.Attached) DestroyAttachment(target);
                else if (!StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                break;
            case "kasuru-draw":
                source!.Bowed = true;
                if (IsAttacking(source)) Draw(player);
                break;
            case "mihato-move": MoveHome(target!, false); break;
            case "noboru-exp2-favor": _state.FavorPlayerId = player.Id; break;
            case "noboru-shattered-star-weaken":
            {
                var bonus = CardForce(target!);
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                ApplyModifier(source!, source!.Id, id, "Force", bonus);
                break;
            }
            case "noritada-invest":
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                break;
            case "obote-boost": DiscardFate(player, target!); ApplyModifier(source!, source!.Id, id, "Force", Definition(target!).Focus); break;
            case "rikyou-move":
                Require(Army(1 - player.Id, _state.CurrentBattleProvince!.Value).Any(), "Rikyou may only reinforce a battlefield with enemy units present.");
                MoveToBattlefield(source!, _state.CurrentBattleProvince!.Value);
                StraightenUnit(source!);
                break;
            case "korimi-exp-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "korimi-exp2-draw": Draw(player); break;
            // "After a duel resolves" (issue #209).
            case "daisuke-exp-draw": Draw(player); break;
            case "horume-gain": case "training-choose-player-gain": ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, 1); break;
            case "horume-lose": case "training-choose-player-lose": ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, -1); break;
            case "horume-honor": GainHonor(player.Id, 1, "Kitsuki Horume", source); break;
            case "jakuei-favor": _state.FavorPlayerId = player.Id; break;
            case "jakuei-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "jakuei-bow-favor":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                ReturnHome(target!, false); // bow above is preserved -- ReturnHome(bow:false) never straightens
                Emit("unitMovedHome", $"{Definition(target!).Name}'s unit moved home bowed.", target!.OwnerId, target.Id);
                break;
            // Tamori Takeshi (issue #212): grants Protection for the rest of the active player's own
            // turn -- the default duration, matching every other un-qualified effect in the engine.
            case "takeshi-protect": GrantProtection(target!, source!.Id, "Turn"); break;
            // Togashi Noboru (issue #216): redirects the target's own Force to Noboru's own
            // controller's army for the rest of the battle -- re-checked live at ResolveBattle
            // (ArmyForces/RedirectsTo), not applied here; this just records the standing grant. A
            // repeat use against the same target simply overwrites (still only ever one entry per
            // target id, since Dictionary-style dedupe is unnecessary for a plain list this small).
            case "noboru-redirect": _state.ForceRedirects.RemoveAll(r => r.TargetId == target!.Id); _state.ForceRedirects.Add(new(target!.Id, player.Id)); break;
            // Togashi Hizumi (issue #216): negates the target's own next straightening this turn --
            // Straighten() itself clears the block the moment it actually negates one, so the target
            // may still be straightened additional times afterward if something else straightens it
            // again later the same turn.
            case "hizumi-negate-straighten": target!.StraightenBlockedTurn = _state.TurnNumber; break;
            case "ruya-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            // Legion of the Bat / Nikutai (issue #279): both Target "none" -- the affected card is
            // read from the state field OpenMoveReactionWindow/OpenNikutaiReactionWindow stashed when
            // the window opened, the same auto-determined shape rokai-negate-destruction/tamoko-negate
            // already use for MatchState.PendingNegatableAbility's own target.
            case "legion-of-bat-bow": if (!StalwartNegatesBow(Card(_state.MoveReactionTargetId!), player.Id)) Card(_state.MoveReactionTargetId!).Bowed = true; break;
            case "nikutai-straighten": Straighten(Card(_state.NikutaiReactionTargetId!)); break;
            case "yayu-bow-attack":
                Card(command.PerformerId!).Bowed = true;
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "yayu-destroy-attack":
                DestroyAttachment(Card(command.PerformerId!));
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return;
                break;
            case "mugoshi-slander": ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, -1); break;
            // Deck search (issue #312): the cost (bow Shizuka) is paid now; the search itself defers through its own
            // PendingChoice types, so -- like shinden-choice above -- the action does not finish here: FinishSearch
            // hands priority on once the deck has been shuffled.
            // Fukuzo (issue #338): the removal is the cost, paid first (and it is what takes the source out of any later reach: the
            // keyword and ability lists are a snapshot of it at this moment). Then the two independent copy steps, keyword then ability;
            // a step with nothing to offer is skipped and one with a single candidate is taken, so a decision opens only when there is
            // a real choice, and the action then finishes when it is answered (FinishCopy).
            // Yoritomo Sakuma (issue #338): bow him (the printed cost), then the acting player -- not his controller -- chooses: pay 2 Gold,
            // or the deferred action's effects are negated. He can only be in the reaction window, so the choice interrupts it: the
            // window is suspended and restored (EitherOr.cs), and when nothing needs deciding (the player cannot pay 2 Gold, or the performer
            // is Protected) the negation simply applies and this reaction finishes like any other.
            case "sakuma-political-negate":
            {
                source!.Bowed = true;
                var deferred = _state.PendingNegatableAbility!;
                if (OpenEitherOr(EitherOrSpecs.SakumaPayOrNegate, source, deferred.PlayerId, deferred.PlayerId, deferred.TargetId is null ? null : Card(deferred.TargetId), "window"))
                {
                    if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                    return;
                }
                break;
            }
            // Unfortunate Hesitation (issue #338): a hand-played Strategy is spent as it is played (the deferral skips the tail that would
            // discard it); the target's controller then chooses to bow him or take the forced pass (ForcedPass.cs). The action finishes,
            // priority passing on as after any action, once he has chosen; when he cannot bow him (already bowed) there is nothing to
            // choose and the pass is simply owed.
            case "unfortunate-hesitation":
            {
                // Champion of Thunder's "negate the action's effects on this card": the whole offer is an effect on the target, so it is not made.
                if (NegatesEffect(target!, "all"))
                {
                    Emit("effectsNegated", $"{Definition(target!).Name}: the action's effects were negated before they resolved.", target!.OwnerId, target.Id);
                    break;
                }
                SpendHandPlayedCard(player, source);
                if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                if (OpenEitherOr(EitherOrSpecs.UnfortunateHesitation, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction,
                        stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == player.Id))) return;
                break;
            }
            // Training fixture for the Either/Or primitive's three-option shape.
            case "training-either-or-three":
                if (OpenEitherOr(EitherOrSpecs.TrainingThreeWay, source!, player.Id, 1 - player.Id, null, "action", bowedBeforeAction)) return;
                break;
            case "training-copy-keyword": case "training-copy-ability": case "training-copy-ability-turn":
                RemoveFromGame(target!);
                if (BeginCopy(source!, target!, id, id == "training-copy-keyword" ? [CopyStep.Keyword] : [CopyStep.Ability], player.Id, "ability", bowedBeforeAction,
                        id == "training-copy-ability-turn" ? GrantDuration.Turn : GrantDuration.Copy)) return;
                break;
            case "fukuzo-wear-skin":
                RemoveFromGame(target!);
                if (BeginCopy(source!, target!, id, [CopyStep.Keyword, CopyStep.Ability], player.Id, "ability", bowedBeforeAction)) return;
                break;
            case "shizuka-search": source!.Bowed = true; BeginSearch(ability.Search!, source, player.Id, "ability", bowedBeforeAction); return;
            case "training-search-cheap": case "training-search-focus": case "training-search-optional":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction); return;
            // Look at the top N cards (issue #322): like a search, the look defers through its own PendingChoice types, so the
            // action does not finish here -- FinishLook hands priority on once the arrangement is submitted. A Strategy played from
            // the hand is spent as it is played (the tail that would discard it is skipped by the deferral).
            case "public-records-look": source!.Bowed = true; BeginLook(ability.Look!, source, player.Id, "ability", bowedBeforeAction); return;
            case "isawa-tamaki-look": case "kahoku-exp2-look": case "training-look-dynasty": case "training-look-any": case "training-look-take-item": case "training-look-fixed":
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction); return;
            case "training-look-show": BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction, count: 2); return;
            case "banish-all-doubt-look":
                if (source!.Zone == Zone.Hand) DiscardFate(player, source);
                BeginLook(ability.Look!, source, player.Id, "ability", bowedBeforeAction); return;
            // Battle Phase timing (issues #309/#310): the Engage window's consumers. Every claim only RECORDS the
            // request; who actually acts first is decided when the window closes (ResolveFirstOpportunity), so the
            // order claims are made in never matters and claims never stack.
            case "yuki-engage-recon":
            {
                // "You have Reconnaissance there": granted to Yuki's controller whether they are the Attacker or the Defender
                // (rules lead's ruling, verbatim: the all-Scouts condition -- checked by ValidateAbility -- is what decides the grant,
                // "regardless of whether Yuki is in an attacking or defending army"). "There" is Yuki's battlefield, which for a
                // Defender is their OWN province; the opponent gains nothing.
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                ClaimFirstOpportunity(player.Id, "Engage", source);
                break;
            }
            case "naval-invasion":
                _state.PendingEngage!.NavalInvasionUsed = true;
                _state.PendingEngage.NavalPerformerId = command.PerformerId;
                ClaimFirstOpportunity(player.Id, "Naval", Card(command.PerformerId));
                break;
            case "training-engage-attacker-first": ClaimFirstOpportunity(_state.PendingEngage!.Attacker, "Engage", source); break;
            case "training-engage-gain-honor": GainHonor(player.Id, 1, "Training engage reaction", source); break;
            case "training-engage-claim-political": ClaimFirstOpportunity(_state.PendingEngage!.Attacker, "Engage", source); break;
            case "training-delayed-bow": RegisterDelayedBattleEffect(player.Id, source, "bow", target!); break;
            case "training-delayed-boost": RegisterDelayedBattleEffect(player.Id, source, "modifier", target!, "Force", 2); break;
            case "steady-resolve-boost": ApplyModifier(Card(command.PerformerId), source!.Id, id, "Force", 2); break;
            case "dairuko-rally": if (!_state.ResolutionWillNotBowPlayerIds.Contains(player.Id)) _state.ResolutionWillNotBowPlayerIds.Add(player.Id); break;
            // Recon-action classification fixtures (issue #321): no effect of their own -- the point is what fires AFTER them.
            case "training-recon-open": case "training-plain-open": break;
            // Alternative cost / attach from a discard pile / conditional effect choice / Follower performer (issue #337).
            // Kitsu Fukui: the bow-or-destroy cost was paid by PayCostOption; "Bow it. Gain 1 Honor" is the effect, the Honor gain being an
            // action of the performing Ancestor.
            // Training fixture (issue #337): a before-action Reaction that destroys the Weapons attached to the deferred action's source, so a
            // branch that reads "has a Weapon" is read AFTER it (the condition is evaluated at resolution).
            case "training-alt-cost-mixed": Draw(player); break;
            case "training-break-weapon":
                foreach (var weapon in Attachments(_state.PendingNegatableAbility!.CardId!).Where(a => Keyword(a, "Weapon")).ToArray()) DestroyAttachment(weapon);
                break;
            case "fukui-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonorFromPerformer(Card(command.PerformerId!), 1, "Kitsu Fukui");
                break;
            // Hiruma Tensin: the performing Follower's bow was paid as its cost option; the Follower then performs the Attack.
            case "tensin-follower-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId!, consumedGrant)) return;
                break;
            default:
                // Any ability with an AttachSpec (Moto Jun-Ni, Fading Shadows, Foothills Keep and the training shapes) needs no case of its own: the Gold was
                // paid by PayForAbility, so the card now leaves its place and enters play attached. An entering-play window ends the action here.
                if (ability.Attach is { } attachSpec)
                {
                    if (ExecuteAttach(command, attachSpec, player, source, target)) return;
                    break;
                }
                // Any ability with a Branch (Mirumoto Yumaru) needs no case of its own either: the condition is read now, and when it holds the action
                // pauses for the player's choice (EffectChoice.cs) and finishes from there.
                if (ability.Branch is not null)
                {
                    if (BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants)) return;
                    break;
                }
                throw new RuleViolation("Ability effect is not implemented.");
        }
        CompleteAbilityResolution(command, ability, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
    }
    // Everything an action does once its own effects have applied (issue #337 extracted this from the end of RunAbilityEffects, unchanged,
    // so an action that pauses for a decision after its effects -- the conditional effect choice, EffectChoice.cs -- finishes exactly like
    // one that did not): the after-resolution traits, hand-played card discard, Additional Action bookkeeping, the "after this action"
    // reaction windows, and the hand-over of priority. RunAbilityEffects reaches it at the end of its switch; a case that returns early
    // owns calling it (or an equivalent tail) itself.
    private void CompleteAbilityResolution(GameCommand command, AbilityDefinition ability, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBeforeBattleAction,
        HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        var id = command.AbilityId!;
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        var target = command.TargetId is null ? null : Card(command.TargetId);
        // "After the resolution of your [keyword] action" traits (Daidoji Ebizo, issue #321): every in-play card of
        // the acting player whose Effect registers one (AfterOwnActionTraits) reacts here, the single funnel every
        // synchronously resolving ability passes through -- a deferred Attack and a duel reach the same helper from
        // their own completion points (ResolveAttack, ResolveDuel).
        FireAfterOwnActionTraits(ability, player.Id, source);
        // Apprentice Shinobi (issue #279): "After the resolution of a Ninja action: Give this card
        // +1F" -- unconditional and universal (every in-play copy, regardless of controller or
        // whether Apprentice Shinobi itself took part), checked here at the single shared execution
        // funnel every ability (Battle/Open/Limited/BattleOrOpen/Reaction/Discipline) already passes
        // through. "Ninja Battle" et al. (Goju House Guard, issue #192) is a plain printed-keyword
        // classification, not a distinct engine Timing -- the prefix itself is never retained in
        // ability.Timing, so the resolving ability's own SOURCE card carrying the Ninja keyword is
        // the only structural signal available, and the only one needed. Applies to a Reaction's own
        // resolution too (unlike OpenBowReactionWindow below): nothing about "a Ninja action
        // resolved" is specific to how the window that led to it opened. Reset for free by
        // FinishTurn's own unconditional Modifiers.Clear() sweep -- "until the end of the turn" needs
        // no separate tracked flag.
        if (source is not null && Keyword(source, "Ninja"))
            foreach (var shinobi in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == "apprentice-shinobi-ninja-bonus"))
                ApplyModifier(shinobi, shinobi.Id, "apprentice-shinobi-ninja-bonus", "Force", 1);
        // Ring of Air's own recruitment condition (issue #194): "after you resolve two or more Favor
        // actions in one turn" -- every Favor-consuming ability shares this one shared Require
        // (see above), so the counter and the grant-check are shared here too, rather than
        // duplicated across each of the three execution cases.
        if (id is "shunya-honor" or "favor-cycle" or "favor-battle" or "jakuei-bow-favor")
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
        SpendHandPlayedCard(player, source);
        // Discipline (core rules, issue #184; a flag since #331): "played and resolved and then removed from the
        // game" -- always, whether or not its own effect above did anything else with the card. Only the DISCARD-
        // PILE mode: a hand-played Discipline Strategy was just discarded by SpendHandPlayedCard above like any
        // Strategy, and stays in the discard pile (available to be replayed from there on a LATER turn: the
        // once-per-turn stamp on the card keeps it from being replayed the same turn, exactly as the Aftermath
        // supplement's own reminder says).
        if (playedFromDiscard) RemoveFromGame(source!);
        // Additional Action (issue #181): an ordinary action that did not itself consume a grant
        // forfeits every one of this player's PRE-EXISTING grants outright (same as explicitly
        // passing, see MatchEngine.Pass) -- "resolve them to completion before resuming the normal
        // turn order" means a normal action ends the burst, it does not just quietly skip past it.
        // A fresh grant this same action creates (Again!/Two Hands/equip-battle, above) is untouched
        // by this -- it was never in preExistingGrants -- and still keeps priority with the player.
        if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
        // Togashi Hizumi / Tamori Ruya (issue #216): a chance to react to this action having just bowed a
        // Personality, before handing priority back the ordinary way. A Reaction is an action too, so its own
        // resolution is checked like any other's; the window it was played in is suspended behind these.
        if (OpenBowReactionWindow(player.Id, bowedBeforeAction, 1 - player.Id)) return;
        // Stormless Fury (issue #276 follow-up): a chance to react to a Battle action having just
        // resolved with an identifiable performer (only a Battle-timed ability qualifies).
        if (OpenBattleActionReactionWindow(command, ability, source, 1 - player.Id)) return;
        // Legion of the Bat (issue #279): same "after this action" timing as OpenBowReactionWindow above.
        if (OpenMoveReactionWindow(1 - player.Id)) return;
        // Nikutai (issue #279): same "after this action" timing, reusing the SAME bowedBeforeAction
        // snapshot OpenBowReactionWindow already computed rather than a second diff pass.
        if (OpenNikutaiReactionWindow(bowedBeforeAction, 1 - player.Id)) return;
        // Overconfident (issue #223): a chance to react to anything this ability's own resolution
        // just destroyed or recruited (MarkDead/Purchase/RecruitReserve already queued it) -- but
        // only when this action is actually about to hand priority back the ordinary way. A Reaction
        // used INSIDE an already-open window (entryReaction, proclaim, bowReaction, ...) must leave
        // that window's own PendingChoice alone -- it stays open afterward by design (NextAction below
        // only ever flips PriorityPlayerId, never PendingChoice, letting the same window accept
        // another reaction or a pass) -- so this only fires when PendingChoice is null here, same as
        // every one of those windows' own eventual close funnels through PassReaction's own hook
        // instead, not this one.
        if (_state.PendingChoice is null && OpenOverconfidentDrawWindow(1 - player.Id)) return;
        NextAction(player.Id, _state.AdditionalActions.Any(g => g.PlayerId == player.Id));
    }
    // Togashi Hizumi: "after the resolution of an action that bowed a Personality AT HIZUMI'S
    // BATTLEFIELD" -- location-scoped on the trigger (open to either player's action), reacting by
    // targeting that same bowed personality (BowReactionCandidateIds) to negate its own next
    // straightening. Tamori Ruya: "after the resolution of YOUR action that bowed a Personality" --
    // actor-scoped instead (only the acting player's own Ruya reacts), no location restriction on
    // the bow itself; her own target is a Personality at RUYA'S location, unrelated to which
    // personality was actually bowed. Hizumi checked first if somehow both are eligible at once --
    // an arbitrary but deterministic tie-break, since nothing printed orders them relative to each
    // other and no real scenario needs both simultaneously.
    private static string? BowedAtBattlefieldReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "bowed-personality-at-own-battlefield");
    private static string? OwnActionBowedPersonalityReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-action-bowed-personality");
    private bool OpenBowReactionWindow(int actingPlayerId, HashSet<string> bowedBeforeAction, int closesTo)
    {
        var newlyBowed = _state.Cards.Values.Where(c => c.Bowed && !bowedBeforeAction.Contains(c.Id) && Definition(c).Type == CardType.Personality).ToArray();
        if (newlyBowed.Length == 0) return false;
        var atABattlefield = newlyBowed.Where(AtBattle).ToArray();
        var hizumi = atABattlefield.Length > 0 ? _state.Cards.Values.FirstOrDefault(c => InPlay(c) && AtBattle(c) && BowedAtBattlefieldReactionAbility(c) is { } hizumiAbility && Unspent(c, hizumiAbility) && atABattlefield.Any(nb => nb.ProvinceIndex == c.ProvinceIndex)) : null;
        if (hizumi is not null)
        {
            SuspendOpenWindow();
            _state.BowReactionCandidateIds = atABattlefield.Where(nb => nb.ProvinceIndex == hizumi.ProvinceIndex).Select(c => c.Id).ToList();
            _state.PendingChoice = new("bowReaction", hizumi.OwnerId, hizumi.Id, "After the bow resolved: you may use this card's own reaction, or pass.", closesTo);
            _state.ConsecutivePasses = 0; _state.PriorityPlayerId = hizumi.OwnerId;
            Emit("reactionWindow", "Bow-reaction window opened.", hizumi.OwnerId, hizumi.Id);
            return true;
        }
        var ruya = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == actingPlayerId && InPlay(c) && OwnActionBowedPersonalityReactionAbility(c) is { } ruyaAbility && Unspent(c, ruyaAbility));
        if (ruya is not null)
        {
            SuspendOpenWindow();
            _state.PendingChoice = new("bowReaction", ruya.OwnerId, ruya.Id, "After your own action bowed a Personality: you may use this card's own reaction, or pass.", closesTo);
            _state.ConsecutivePasses = 0; _state.PriorityPlayerId = ruya.OwnerId;
            Emit("reactionWindow", "Bow-reaction window opened.", ruya.OwnerId, ruya.Id);
            return true;
        }
        return false;
    }
    private static string? MovedEnemyInfantryReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "moved-enemy-infantry-to-own-battlefield");
    // Legion of the Bat (issue #279): unlike OpenBowReactionWindow above (a battlefield-wide search
    // across every candidate reacting card), the reacting card here is uniquely determined by
    // location -- MoveToBattlefield already queued the ONE card that just arrived (if any), so this
    // only needs to check whether an enemy Legion of the Bat happens to share its destination
    // province. "Infantry" reuses IsCavalryUnit (already the engine's own Cavalry-unit test); "enemy"
    // is relative to Legion's own controller, not the acting player, matching the printed text
    // exactly ("a target enemy Infantry Personality").
    private bool OpenMoveReactionWindow(int closesTo)
    {
        var cardId = _state.PendingMoveReactionCardId;
        _state.PendingMoveReactionCardId = null;
        if (cardId is null || !_state.Cards.ContainsKey(cardId)) return false;
        var personality = Card(cardId);
        if (Definition(personality).Type != CardType.Personality || IsCavalryUnit(personality)) return false;
        var legion = _state.Cards.Values.FirstOrDefault(c => c.Zone == Zone.Attached && c.ProvinceIndex == personality.ProvinceIndex
            && c.OwnerId != personality.OwnerId && MovedEnemyInfantryReactionAbility(c) is { } legionAbility && Unspent(c, legionAbility));
        if (legion is null) return false;
        SuspendOpenWindow();
        _state.MoveReactionTargetId = personality.Id;
        _state.PendingChoice = new("moveReaction", legion.OwnerId, legion.Id, "After the resolution of an action that moved an enemy Infantry Personality to this card's battlefield: you may use this card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0; _state.PriorityPlayerId = legion.OwnerId;
        Emit("reactionWindow", "Move-reaction window opened.", legion.OwnerId, legion.Id);
        return true;
    }
    private static string? SiblingFollowerBowedReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "sibling-follower-bowed");
    // Nikutai (issue #279): a narrow in-play Reaction scoped to Nikutai's own host unit rather than a
    // battlefield (OpenBowReactionWindow's own scope) or the acting player (Ruya's shape) -- kept as
    // its own function rather than folded into OpenBowReactionWindow, since both its own card-type
    // filter (Follower, not Personality) and its own reactor lookup (by shared host, not battlefield/
    // actor) genuinely differ from every existing case there. Reuses the SAME bowedBeforeAction
    // snapshot OpenBowReactionWindow already computes (the shared "what changed this action"
    // primitive) rather than a second diff pass -- just filtered to Followers instead of
    // Personalities. Excludes Nikutai's own bow (only ANOTHER Follower in the unit counts, per the
    // card's own controller's rule) and a Follower with no host at all (never true for an in-play
    // Follower, but AttachedTo is nullable).
    private bool OpenNikutaiReactionWindow(HashSet<string> bowedBeforeAction, int closesTo)
    {
        var newlyBowedFollowers = _state.Cards.Values.Where(c => c.Bowed && !bowedBeforeAction.Contains(c.Id) && Definition(c).Type == CardType.Follower && c.Zone == Zone.Attached).ToArray();
        if (newlyBowedFollowers.Length == 0) return false;
        var nikutai = _state.Cards.Values.FirstOrDefault(c => InPlay(c) && c.Zone == Zone.Attached && SiblingFollowerBowedReactionAbility(c) is { } nikutaiAbility && Unspent(c, nikutaiAbility)
            && newlyBowedFollowers.Any(f => f.Id != c.Id && f.AttachedTo == c.AttachedTo));
        if (nikutai is null) return false;
        SuspendOpenWindow();
        _state.NikutaiReactionTargetId = newlyBowedFollowers.First(f => f.Id != nikutai.Id && f.AttachedTo == nikutai.AttachedTo).Id;
        _state.PendingChoice = new("nikutaiReaction", nikutai.OwnerId, nikutai.Id, "Even if this card is bowed, after a Follower in this unit bowed: you may straighten it, or pass.", closesTo);
        _state.ConsecutivePasses = 0; _state.PriorityPlayerId = nikutai.OwnerId;
        Emit("reactionWindow", "Nikutai reaction window opened.", nikutai.OwnerId, nikutai.Id);
        return true;
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

    // Preserving Honor (oracle-l5r-6060, issue #295): the targeted Personality's own controller
    // chooses bow-or-move, the same shinden-choice shape (a dedicated PendingChoice type). "If the
    // bowing or movement is negated, remove the Personality from the game": StalwartNegatesBow already
    // RETURNS whether the bow was negated (every other call site just discards that bool) -- reused
    // directly here as the detection hook. The move branch inlines MoveHome's own body (ReturnHome plus
    // its NegatesEffect("move") check) rather than calling MoveHome itself, so the negation result can
    // be branched on the same way.
    private void ResolvePreservingHonor(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "choosePreservingHonor" && command.PlayerId == pending.PlayerId && command.Choice is "bow" or "move",
            "The targeted Personality's controller must choose to bow him or move him.");
        var card = Card(pending.CardId!);
        if (command.Choice == "bow")
        {
            if (StalwartNegatesBow(card, pending.ActingPlayerId!.Value)) RemoveFromGame(card);
            else card.Bowed = true;
        }
        else if (NegatesEffect(card, "move"))
        {
            Emit("moveNegated", $"{Definition(card).Name}'s movement was negated before it resolved.", card.OwnerId, card.Id);
            RemoveFromGame(card);
        }
        else
        {
            ReturnHome(card, false);
            Emit("unitMovedHome", $"{Definition(card).Name}'s unit moved home.", card.OwnerId, card.Id);
        }
        _state.PendingChoice = null;
        NextAction(pending.ActingPlayerId!.Value);
    }

    // Asako Rikate (oracle-l5r-576, issue #301): "His controller may put any attachments in that unit into
    // his hand. Give the Personality -2C." -- the targeted Personality's own controller picks ANY subset of
    // the unit's attachments (command.CardIds; empty declines), each returned to hand, and only THEN does the
    // -2C apply (a lethal penalty destroys the attachments still on him, so the controller who returned
    // them first keeps them). Shinden's own dedicated-PendingChoice shape. ApplyModifier already destroys a
    // Personality whose Chi reaches 0, and an Overconfident/Times of Strife window that resolution queued is
    // drained here the same way ExecuteAbilityEffects' own tail would have.
    private void ResolveRikateDetach(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseRikateDetach" && command.PlayerId == pending.PlayerId,
            "The targeted Personality's controller must choose which attachments to put into his hand.");
        var target = Card(pending.CardId!);
        var chosen = command.CardIds ?? [];
        var attachments = Attachments(target.Id).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        Require(chosen.Distinct(StringComparer.Ordinal).Count() == chosen.Length && chosen.All(attachments.Contains),
            "Choose only different attachments in that unit.");
        foreach (var id in chosen) ReturnToHand(Card(id));
        _state.PendingChoice = null;
        if (InPlay(target)) ApplyModifier(target, "rikate-detach-chi", "rikate-detach-chi", "Chi", -2);
        var actingPlayerId = pending.ActingPlayerId!.Value;
        if (OpenOverconfidentDrawWindow(1 - actingPlayerId)) return;
        NextAction(actingPlayerId);
    }

    // Shosuro Rin (oracle-l5r-7151, issue #301): "The Personality's controller may dishonor him. If he did
    // not choose this, bow him and negate his straightening (this turn)." The bow is an effect of an enemy
    // action like any other (Stalwart-checkable); "only the bowing" is what Stalwart negates, so the
    // straightening negation applies either way.
    private void ResolveRinShame(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseRinShame" && command.PlayerId == pending.PlayerId && command.Choice is "dishonor" or "decline",
            "The targeted Personality's controller must choose whether to dishonor him.");
        var card = Card(pending.CardId!);
        if (command.Choice == "dishonor")
        {
            Dishonor(card);
            Emit("rinShameAccepted", $"{Definition(card).Name} was dishonored.", card.OwnerId, card.Id);
        }
        else
        {
            if (!StalwartNegatesBow(card, pending.ActingPlayerId!.Value)) card.Bowed = true;
            card.StraightenBlockedTurn = _state.TurnNumber;
            Emit("rinShameDeclined", $"{Definition(card).Name} was bowed and cannot straighten this turn.", card.OwnerId, card.Id);
        }
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
        // Tamori Tamoko (issue #207 follow-up): "before an action resolves, negate ... his
        // straightening from the action's effects" -- consumed here, the single shared choke point
        // for every ability-triggered straighten (StraightenUnit calls through this too). Checked
        // before StraightenBlockedTurn below, since a negated straighten was never attempted at all
        // (unlike Hizumi's own block, which negates an attempt that DID happen).
        if (NegatesEffect(card, "straighten"))
        {
            Emit("straightenNegated", $"{Definition(card).Name}'s straightening was negated before it resolved.", card.OwnerId, card.Id);
            return;
        }
        // "Negate his NEXT straightening" (Shinden Asahina; Togashi Hizumi/Tamori Ruya, issue #216):
        // a one-shot block, not "cannot straighten this turn at all" -- consumed the moment it
        // negates an actual attempt, so a LATER straighten attempt the same turn (an ability that
        // straightens more than once) succeeds normally. skip-first-straighten is a distinct,
        // ongoing condition (a whole turn's worth of Novice restriction), never consumed this way.
        if (card.StraightenBlockedTurn == _state.TurnNumber)
        {
            card.StraightenBlockedTurn = -1;
            Emit("straightenNegated", $"{Definition(card).Name} could not straighten.", card.OwnerId, card.Id);
        }
        else if (Definition(card).Effect == "skip-first-straighten" && Player(card.OwnerId).TurnsTaken < 2)
            Emit("straightenNegated", $"{Definition(card).Name} could not straighten.", card.OwnerId, card.Id);
        else
        {
            card.Bowed = false;
            // Lady Matsu's Rage: "After the next time (this turn) it straightens, destroy it." The
            // ONLY place a card actually straightens by game effect -- see LadyMatsusRageWatch's own
            // doc comment -- so this is the correct, complete hook; nothing further needs to check it
            // elsewhere. Consumed (removed) the instant it fires.
            var watch = _state.PendingLadyMatsusRageWatches.FirstOrDefault(w => w.TargetId == card.Id);
            if (watch is not null)
            {
                _state.PendingLadyMatsusRageWatches.Remove(watch);
                Emit("ladyMatsusRageTriggered", $"{Definition(card).Name} straightened after being bowed by Lady Matsu's Rage -- destroyed.", card.OwnerId, card.Id);
                DestroyUnits([card], actingPlayerId: watch.ActingPlayerId);
            }
        }
    }
    // Dragon Clan review (issue #207): "straighten his unit" (Mirumoto Houken's own shape) --
    // straightens the card AND every attachment, unlike the single-card Straighten calls every
    // other ability uses.
    private void StraightenUnit(CardInstance card)
    {
        Straighten(card);
        foreach (var attachment in Attachments(card.Id)) Straighten(attachment);
    }
    // Wave Man (issue #279): "After an Attack Phase ends: Straighten this unit" -- automatic and
    // unconditional, fired for EVERY in-play Wave Man every time the Attack Phase ends (see
    // SetPhase), not a player-chosen target the way StraightenUnit's own callers always are. Unlike
    // StraightenUnit above, this only actually calls Straighten() on a card that IS currently bowed:
    // Straighten() itself unconditionally consumes a live StraightenBlockedTurn the moment it is
    // called at all (core to Hizumi/Ruya's own "negate his NEXT straightening" shape, which needs an
    // attempt to exist to negate), so calling it on an already-unbowed card here would spuriously
    // burn a block meant for a later, real bow-then-straighten this same turn. "card" may be the
    // Follower itself (Zone.Attached) -- resolved to its own host first, since "this unit" always
    // means the host's whole unit, never just the one Follower carrying the trait.
    private void StraightenBowedUnit(CardInstance card)
    {
        var host = card.Zone == Zone.Attached ? Card(card.AttachedTo) : card;
        if (host.Bowed) Straighten(host);
        foreach (var attachment in Attachments(host.Id)) if (attachment.Bowed) Straighten(attachment);
    }
    // A move TO a battlefield that an action's own effect applies to a TARGET (Return to Action, issue #331), so -- like
    // MoveHome's own check -- Tamori Tamoko's negation and Champion of Thunder's "all" apply; false when it was
    // negated and the card stayed put. Every other move-to-battlefield ability moves its own source or performer, which
    // no negation window ever names, so plain MoveToBattlefield has no such check.
    private bool TryMoveToBattlefield(CardInstance card, int province)
    {
        if (NegatesEffect(card, "move"))
        {
            Emit("moveNegated", $"{Definition(card).Name}'s movement was negated before it resolved.", card.OwnerId, card.Id);
            return false;
        }
        MoveToBattlefield(card, province);
        Emit("unitMovedToBattlefield", $"{Definition(card).Name}'s unit moved to the battlefield at province {province + 1}.", card.OwnerId, card.Id);
        return true;
    }
    private void MoveHome(CardInstance card, bool bow)
    {
        // Tamori Tamoko (issue #207 follow-up): "before an action resolves, negate ... his movement
        // from the action's effects" -- consumed here, not inside ReturnHome, so the end-of-battle
        // bulk return-home sweep (FinishBattleReturnHomeAndTransition) is never negatable this way --
        // that is not "an action," and Tamoko's own text is scoped to one, not a whole battle ending.
        if (NegatesEffect(card, "move"))
        {
            Emit("moveNegated", $"{Definition(card).Name}'s movement was negated before it resolved.", card.OwnerId, card.Id);
            return;
        }
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
    // A card moved from a discard pile back to hand (issue #190, e.g. Hunter of Harmony) is "treated
    // as a freshly drawn card": any once-per-turn AbilityUses stamp from an earlier use this same turn
    // is cleared, so its printed ability may be used again this turn -- unlike a card that merely stays
    // in play or a discard pile, where the stamp persists.
    // Asako Rikate (issue #301): "put any attachments in that unit into his hand" generalizes this to a card
    // still ATTACHED in play. Leaving play as a plain return (never a destruction -- no Expendable draw, no
    // destroyed-during-battle bonus) needs exactly the cleanup RemoveFromGame/DestroyAttachment already do
    // on detach (the host's "Attachment"-duration keyword grants from this card end with it), plus the fresh-
    // card reset ToOwnerDeck applies to anything that will re-enter play later: no stale AttachedTo, Bowed
    // state, modifiers, tokens or granted keywords following it back into the hand. A host whose Chi only
    // stayed positive thanks to an Item is destroyed once it leaves (DestroyAttachment's own rule).
    private void ReturnToHand(CardInstance card)
    {
        var wasAttachedTo = card.Zone == Zone.Attached && card.AttachedTo is string hostId ? Card(hostId) : null;
        if (wasAttachedTo is not null)
        {
            wasAttachedTo.GrantedKeywords.RemoveAll(g => g.Source == card.Id && g.Duration == "Attachment");
            card.AttachedTo = null; card.Bowed = false;
            card.Tokens.Clear(); card.Modifiers.Clear(); card.GrantedKeywords.Clear(); card.GrantedAbilities.Clear(); card.RemovedKeywords.Clear();
        }
        card.Zone = Zone.Hand; card.ProvinceIndex = null; card.FaceUp = true; card.AbilityUses.Clear();
        Player(card.OwnerId).Hand.Add(card.Id);
        Emit("returnedToHand", $"{Definition(card).Name} was returned to {Player(card.OwnerId).Name}'s hand.", card.OwnerId, card.Id);
        if (wasAttachedTo is not null && InPlay(wasAttachedTo) && Definition(wasAttachedTo).Type == CardType.Personality && PersonalityChi(wasAttachedTo) <= 0) DestroyUnits([wasAttachedTo], true);
    }
    // Goju House Guard (issue #192): a card sent to "the top or bottom of its owner's appropriate
    // deck" re-enters that deck as a wholly fresh instance -- unlike RemoveFromGame/MarkDead (both
    // terminal, so stale Modifiers/GrantedKeywords/Bowed/AbilityUses never matter again), this card
    // WILL be drawn/refilled and re-enter play, so every one of those must be cleared here or a
    // phantom Force boost, keyword grant, bowed state, or once-per-turn stamp would silently
    // resurface later. "Appropriate deck": a Personality goes to the Dynasty deck; a Follower/Item/
    // Spell (the only other types that can appear in a Personality's "unit") goes to the Fate deck.
    private void ToOwnerDeck(CardInstance card, string position)
    {
        if (card.AttachedTo is string parentId) Card(parentId).GrantedKeywords.RemoveAll(g => g.Source == card.Id && g.Duration == "Attachment");
        var owner = Player(card.OwnerId);
        var dynasty = Definition(card).Type == CardType.Personality;
        card.AttachedTo = null; card.ProvinceIndex = null; card.FaceUp = false; card.Bowed = false;
        card.Tokens.Clear(); card.Modifiers.Clear(); card.GrantedKeywords.Clear(); card.GrantedAbilities.Clear(); card.RemovedKeywords.Clear(); card.RemovedClanAffiliations.Clear(); card.AbilityUses.Clear();
        card.Zone = dynasty ? Zone.DynastyDeck : Zone.FateDeck;
        var deck = dynasty ? owner.DynastyDeck : owner.FateDeck;
        if (position == "Bottom") deck.Add(card.Id); else deck.Insert(0, card.Id);
        Emit("toDeck", $"{Definition(card).Name} was placed on the {position.ToLowerInvariant()} of {owner.Name}'s {(dynasty ? "Dynasty" : "Fate")} deck.", card.OwnerId, card.Id);
    }
    // Aberrations (issue #192): "Discard the top card of a Fate deck" -- unlike Draw() (top card to
    // hand), this removes it straight to the discard pile (a public zone, so no hidden-identity
    // concern the way Draw()'s own Emit has) and returns its Focus Value for the caller to apply as
    // whatever bonus the card's own text describes. Empty deck: no card, no bonus, same "nothing
    // happens" shape as Draw()'s own empty-deck branch. Interpreted as the controller's OWN Fate
    // deck -- no explicit target/enemy qualifier is printed, matching how an unqualified "a card"
    // effect defaults to the actor's own resources elsewhere in this ruleset (Border Keep's own
    // provinces, discarding from hand).
    private int? DiscardTopOfFateDeck(PlayerState player)
    {
        if (player.FateDeck.Count == 0) { Emit("deckEmpty", $"{player.Name}'s Fate deck is empty; no card discarded.", player.Id); return null; }
        var id = player.FateDeck[0]; player.FateDeck.RemoveAt(0);
        var card = Card(id); card.Zone = Zone.FateDiscard; card.FaceUp = true;
        Emit("cardDiscarded", $"{player.Name} discarded {Definition(card).Name}, the top card of the Fate deck.", player.Id, id);
        return Definition(card).Focus;
    }
    // Terrain (a Strategy played as a Battle action): leaves the battlefield either when destroyed by
    // an ability targeting it, or automatically once its printed effects have been applied during
    // battle resolution (FinishBattleResolution) -- both paths share this single removal.
    private void DiscardTerrain(CardInstance terrain)
    {
        terrain.Zone = Zone.FateDiscard; terrain.ProvinceIndex = null; terrain.FaceUp = true;
        // Fields of Mercy (oracle-l5r-2530): "the count of destroyed personalities does not start
        // until Fields of Mercy are played" -- so a LATER copy entering play must start fresh, not
        // resume where this one left off. A no-op for every other Terrain (RemoveAll matches nothing).
        _state.FieldsOfMercyWatches.RemoveAll(w => w.CardId == terrain.Id);
        Emit("terrainDiscarded", $"{Definition(terrain).Name} was removed from play.", terrain.OwnerId, terrain.Id);
    }
    // Brawl (issue #304): a Region attached to a province leaves play for its owner's Dynasty discard,
    // the same removal ResolveProvinceDestruction applies when the province itself is destroyed.
    private void DestroyRegion(CardInstance region)
    {
        foreach (var province in _state.Players.SelectMany(p => p.Provinces).Where(p => p.RegionId == region.Id)) province.RegionId = null;
        region.Zone = Zone.DynastyDiscard; region.ProvinceIndex = null; region.FaceUp = true;
        Emit("regionDestroyed", $"{Definition(region).Name} was destroyed.", region.OwnerId, region.Id);
    }
    // Fields of Mercy (oracle-l5r-2530): the first currently-in-play copy's own watch (there is
    // realistically only ever one -- at most one Terrain per battlefield -- but this does not assume
    // it). Returns true (negate) once that copy has already seen one of `card`'s own controller's
    // Personalities fully destroyed while it has been in play; otherwise records this one as the
    // first and returns false. False with no active copy at all (the common case).
    private bool FieldsOfMercyNegatesPersonalityDestruction(CardInstance card)
    {
        var watch = _state.FieldsOfMercyWatches.FirstOrDefault(w => InPlay(Card(w.CardId)));
        if (watch is null) return false;
        if (watch.DestroyedCount[card.OwnerId] >= 1) return true;
        watch.DestroyedCount[card.OwnerId]++;
        return false;
    }
    // A Ranged/Melee Attack's target is a lone Follower (an attachment), a Personality with no Followers
    // attached (a full unit), or -- only under an explicit AttackFollowerException -- a Personality WITH
    // Followers attached. The two destruction paths are chosen by type: a Personality's destruction
    // always takes its whole unit (core rules: "If the Personality leaves play, the whole unit leaves play
    // the same way"), so a followed Personality's attached Followers are destroyed with him -- each
    // discarded to the Fate discard pile as a destroyed card (DestroyUnits), Expendable draws included.
    private void DestroyAttackTarget(CardInstance target, int playerId)
    {
        if (Definition(target).Type == CardType.Follower) DestroyAttachment(target);
        else DestroyUnits([target], true, playerId);
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
    // Yasuki Tsujiken (oracle-l5r-9469): "after each time [a watched Holding]'s controller bows it,
    // he loses 1 Honor" -- for as long as Tsujiken remains in play. There is no single existing "any
    // cause bows a card" choke point in this engine (unlike Straighten(), which is one) -- a Holding
    // only ever becomes bowed via a small, enumerable set of ability effects (Ashigaru Elite's own
    // Farm-bow cost is the only one today; Tsujiken's own Open half always STRAIGHTENS its target,
    // never bows it). This is called from each of those specific sites, a documented, narrow scope
    // choice rather than a blanket refactor of every ".Bowed=true" site across the engine's combat/
    // cost paths that can never touch a Holding -- a future Holding-bowing card adds its own call
    // here, the same convention DestroysTarget/MoveOrStraightenKind already use for a curated set.
    // One Honor loss PER active mark (a Holding marked by 3 Tsujikens loses its controller 3 Honor on
    // one bow); InPlay(Card(w.TsujikenId)) is checked live rather than eagerly pruned on every call,
    // so a mark placed by a Tsujiken who has since left play (MarkDead already removes his own marks
    // outright, but this is a defensive double-check) never fires.
    private void CheckTsujikenWatch(CardInstance card)
    {
        if (Definition(card).Type != CardType.Holding) return;
        var marks = _state.TsujikenWatches.Count(w => w.HoldingId == card.Id && InPlay(Card(w.TsujikenId)));
        if (marks > 0) GainHonor(card.OwnerId, -marks, "Yasuki Tsujiken");
    }
    private static int TokenCount(CardInstance card, string type) => card.Tokens.Count(t => t.Type == type);
    private static bool HasToken(CardInstance card, string type) => card.Tokens.Any(t => t.Type == type);

    // The reason ValidateAbility would refuse this exact command right now, or null if it would accept it --
    // the same try/catch shape AbilityActions uses per candidate, for a caller (an in-window Reaction
    // listing in MatchQueries) that needs a single command's own enabled/disabled state and reason.
    private string? AbilityRejection(GameCommand command)
    {
        try { ValidateAbility(command); return null; }
        catch (RuleViolation e) { return e.Message; }
    }

    private IEnumerable<ActionOption> AbilityActions(int playerId)
    {
        var player = Player(playerId);
        // Discipline (issue #184; a flag since #331): a discard-pile card is only a candidate source at all if it
        // carries a Discipline-flagged ability -- ValidateAbility's own zone check (called per candidate below)
        // is still the real gatekeeper, this is just which cards get considered in the first place. A card in
        // HAND is always a candidate (and offered as the ordinary free play); the same card in a discard pile is
        // offered, priced and removed from the game as the Discipline play -- never both at once, since a card is
        // in one zone at a time.
        var sources = _state.Cards.Values.Where(c => c.OwnerId == playerId && (InPlay(c) || c.Zone == Zone.Hand ||
                c.Zone is Zone.FateDiscard or Zone.DynastyDiscard && ((Definition(c).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Discipline) || DiscardPlayGrant(playerId, c) is not null)))
            .SelectMany(c => AbilityIdsOf(c).Select(id => (Card: (CardInstance?)c, Id: id))).ToList();
        if (!Definition(Card(player.StrongholdId)).ScenarioOnly)
            sources.AddRange(AbilityCatalog.All.Where(p => p.Value.PlayerAbility).Select(p => (Card: (CardInstance?)null, Id: p.Key)));
        var inPlay = _state.Cards.Values.Where(InPlay).Select(c => c.Id).ToArray();
        // Battle Phase timing (issue #310): while the Engage window is open the only abilities on offer are
        // the "Engage:" / "After engaging" Reactions (the Phase is Battle, but no Battle action may be taken
        // yet); outside it those Reactions are never offered by this general listing at all.
        var inEngageWindow = _state.PendingChoice?.Type == "engageReaction" && _state.PendingEngage is not null;
        // Likewise for the before-resolution set's Reaction window: only the one card it is asking about, only its "Before a battle resolves" Reactions.
        var beforeResolutionCard = _state.PendingChoice is { Type: "beforeBattleReaction" } beforeResolution ? beforeResolution.CardId : null;
        foreach (var (source, id) in sources)
        {
            var ability = AbilityCatalog.All[id];
            var isEngageReaction = ability.Timing == "Reaction" && ability.Trigger is "after-engaging" or "after-engaging-at-own-battlefield";
            if (inEngageWindow != isEngageReaction) continue;
            if ((beforeResolutionCard is not null) != IsBeforeResolutionReaction(ability) || beforeResolutionCard is not null && source?.Id != beforeResolutionCard) continue;
            var phaseOk = inEngageWindow || beforeResolutionCard is not null ? true : ability.Timing switch { "Battle" => _state.Phase == Phase.Battle, "BattleOrOpen" => _state.Phase is Phase.Action or Phase.Battle, "BeforeResolution" => false, _ => _state.Phase == Phase.Action };
            if (!phaseOk) continue;
            if (ability.Timing == "Limited" && playerId != _state.ActivePlayerId) continue;
            // Battle/Open presence primitive (issue #267): the same actingAsBattle generalization
            // ValidateAbility itself uses, mirrored here so an offered command is never one
            // ValidateAbility would then reject (or, symmetrically, so a legal Open-phase performer
            // is actually offered instead of silently requiring AtBattle year-round).
            var actingAsBattle = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
            string?[] performers = ability.Performer switch
            {
                "shugenja" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Keyword(c, "Shugenja") && !c.Bowed).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c)).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-attacking-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && IsAttacking(c)).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-samurai-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, "Samurai")).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-personality-at-battle-with-keyword" =>
                    _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && (!actingAsBattle || AtBattle(c)) && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id).ToArray(),
                "own-unbowed-personality-with-keyword-at-any-location" =>
                    _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && !c.Bowed && (!actingAsBattle || AtAnyLocation(c)) && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id).ToArray(),
                "own-castle-or-siege" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && !c.Bowed && (Keyword(c, "Castle") || Keyword(c, "Siege"))).Select(c => (string?)c.Id).ToArray(),
                "own-source-or-unbowed-holding-with-keyword" => new[] { source?.Id }.Concat(_state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Holding && !c.Bowed && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id)).ToArray(),
                // Sutigu (issue #301): the source itself, or any of its own unbowed keyworded attachments.
                "own-source-or-unbowed-attached-with-keyword" => new[] { source?.Id }.Concat(_state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Attached && c.AttachedTo == source?.Id && !c.Bowed && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id)).ToArray(),
                // A wielder-relative ability on a Spell still in hand (The Earth's Hunger while unattached) has no
                // wielder yet: no performer, so it is offered disabled instead of Card(null) throwing out of
                // QueryActions for the whole player (found while building the Equip (Battle) payment fixture, issue #303).
                "own-wielder-or-source-with-wielder-keyword" => source?.AttachedTo is null ? []
                    : HasKeywords(Card(source.AttachedTo), ability.RequiredKeywords)
                    ? new[] { Card(source.AttachedTo).Id, source.Id } : new[] { Card(source.AttachedTo).Id },
                "own-personality" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => (string?)c.Id).ToArray(),
                "own-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && (!actingAsBattle || AtBattle(c))).Select(c => (string?)c.Id).ToArray(),
                "own-shadowlands-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && HasClanAffiliation(c, "Shadowlands") && (!actingAsBattle || AtBattle(c))).Select(c => (string?)c.Id).ToArray(),
                "own-personality-with-fire-token-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && HasToken(c, "Fire")).Select(c => (string?)c.Id).ToArray(),
                "own-naval-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && Keyword(c, "Naval")).Select(c => (string?)c.Id).ToArray(),
                "own-personality-with-keyword-opposed-at-battle" => _state.CurrentBattleProvince is not int opposedProvince || !Army(1 - playerId, opposedProvince).Any() ? []
                    : _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id).ToArray(),
                "own-tactician-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && AtBattle(c) && Keyword(c, "Tactician")).Select(c => (string?)c.Id).ToArray(),
                // Kitsu Fukui / Fading Shadows (issue #337): the generic "performing [Keyword] Personality", bowed or not. It was missing from
                // this listing, so an ability that used it (Again!'s shape) could only be played by a hand-built command.
                "own-personality-with-keyword-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && (!actingAsBattle || AtBattle(c)) && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id).ToArray(),
                // Hiruma Tensin (issue #337): the unbowed Followers of the source's own unit.
                "own-unbowed-follower-in-source-unit" => source is null ? [] : Attachments(UnitHostId(source)).Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Follower && !c.Bowed).Select(c => (string?)c.Id).ToArray(),
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
            // Alternative cost (issue #337): one offered action per way to pay when the ability has more than one (each with its own label and
            // its own validation, so an option that cannot be paid is listed disabled with the reason); an ability with a single way, or none,
            // is listed exactly as before with the choice left off the command.
            CostOption?[] costOptions = ability.CostOptions is { Length: > 1 } manyOptions ? manyOptions.Select(o => (CostOption?)o).ToArray() : [null];
            foreach (var performer in performers)
            foreach (var costOption in costOptions)
            {
                var provinces = player.Provinces.Where(p => !p.Destroyed && p.CardId is not null).Select(p => p.CardId!).ToArray();
                var prototype = new GameCommand("useAbility", playerId, _state.Version, source?.Id, null,
                    CardIds: ability.Target == "provinces" ? provinces : null, AbilityId: id, PerformerId: performer, CostOption: costOption?.Id);
                string?[] candidates = ability.Target switch { "none" or "provinces" => new string?[] {null}, "hand" => player.Hand.ToArray(),
                    "spell-or-reserve-in-hand" => player.Hand.Where(h => Definition(Card(h)).Type == CardType.Spell || Keyword(Card(h), "Reserve")).Cast<string?>().ToArray(),
                    // Low Market: broader than Equip (Battle)'s own hand filter (any Item/Follower/
                    // Spell, matching the ordinary Equip() command's own type filter).
                    "attachment-in-hand" => player.Hand.Where(h => Definition(Card(h)).Type is CardType.Item or CardType.Follower or CardType.Spell).Cast<string?>().ToArray(),
                    "attack" => inPlay.Cast<string?>().Append(null).ToArray(),
                    "discarded-personality" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone is Zone.DynastyDiscard or Zone.FateDiscard).Select(c => (string?)c.Id).ToArray(),
                    "dead-personality-any-discard" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone == Zone.Dead).Select(c => (string?)c.Id).ToArray(),
                    "own-personality-in-province" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && c.Zone == Zone.Province && c.FaceUp).Select(c => (string?)c.Id).ToArray(),
                    // Kharmic (issue #303): validated since #177 but never offered -- its target sits in the
                    // hand or a face-up province, neither of which the in-play default reaches, so the
                    // costed action could not be taken from the UI at all. Same condition ValidateAbility checks.
                    "own-kharmic-card" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Keyword(c, "Kharmic") && (c.Zone == Zone.Hand || c.Zone == Zone.Province && c.FaceUp)).Select(c => (string?)c.Id).ToArray(),
                    // Hunter of Harmony / Scholarly Hermit / Tranquil Garden (issue #317): the same gap
                    // Kharmic had -- validated, but a target in a discard pile is never in the in-play
                    // default, so the ability was never offered. Each mirrors its own Require above.
                    "own-kiho-strategy-in-discard" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Strategy && Keyword(c, "Kiho") && c.Zone == Zone.FateDiscard).Select(c => (string?)c.Id).ToArray(),
                    "own-dark-virtue-in-discard" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Strategy && Keyword(c, "Dark Virtue") && c.Zone == Zone.FateDiscard && HasUnspentBattleAction(c)).Select(c => (string?)c.Id).ToArray(),
                    "own-ring-in-discard" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Ring && c.Zone == Zone.FateDiscard).Select(c => (string?)c.Id).ToArray(),
                    "ring-in-play-or-own-discard" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Ring && (InPlay(c) || c.OwnerId == playerId && c.Zone == Zone.FateDiscard)).Select(c => (string?)c.Id).ToArray(),
                    // Shosuro Hawado / Soshi Neiru (issue #301): a Dynasty card in either player's
                    // discard pile (Zone.Dead being a Personality's own), and a matching Spell in hand.
                    "dynasty-card-in-any-discard" => _state.Cards.Values.Where(c => IsDynastyCardType(Definition(c).Type) && c.Zone is Zone.DynastyDiscard or Zone.Dead).Select(c => (string?)c.Id).ToArray(),
                    "personality-in-any-discard" => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone is Zone.DynastyDiscard or Zone.Dead).Select(c => (string?)c.Id).ToArray(),
                    "spell-in-hand-with-keyword" => player.Hand.Where(h => Definition(Card(h)).Type == CardType.Spell && HasKeywords(Card(h), ability.RequiredKeywords)).Cast<string?>().ToArray(),
                    // Moto Jun-Ni / Foothills Keep (issue #337): every card in a discard pile the ability's AttachSpec accepts.
                    "attachment-in-discard" => AttachCandidatesInDiscard(ability.Attach!, playerId).Select(c => (string?)c.Id).ToArray(),
                    _ => inPlay };
                // Shosuro Hawado's battlefield half also needs its destination (command.Province): one
                // candidate per battlefield not yet resolved this Battle phase -- ValidateAbility below
                // keeps only the ones with enemy units. Every other ability has exactly one (null).
                int?[] destinations = id == "hawado-move-battlefield" ? _state.UnresolvedBattlefields.Select(p => (int?)p).ToArray() : [null];
                var legal = new List<GameCommand>(); string? error = null;
                var quotes = new List<(GameCommand Command, GoldQuote? Quote)>();
                foreach (var (target, destination) in candidates.SelectMany(t => destinations.Select(d => (t, d))))
                {
                    // The payment offered with each candidate is the engine's own suggestion from the ONE
                    // Gold quote for that candidate (issue #303) -- fixed, target-derived (Equip (Battle),
                    // Low Market, Your Clan Needs You!, Soshi Neiru, Shinden Shorai) or variable alike, never
                    // a parallel per-ability chain. A free ability gets the empty source list, and a cost the
                    // player cannot pay gets none, so ValidateAbility below reports the shortfall exactly as
                    // it always has.
                    var command = prototype with { TargetId = target, Province = destination };
                    var quote = QuoteAbilityGold(command);
                    command = command with { SourceIds = quote is null ? [] : quote.Payments.FirstOrDefault()?.SourceIds };
                    try { ValidateAbility(command); legal.Add(command); quotes.Add((command, quote)); }
                    catch (RuleViolation e) { error ??= e.Message; }
                }
                var firstQuote = quotes.Count > 0 ? quotes[0].Quote : null;
                // A target-derived cost differs per target, so every legal target ships its own quote and
                // the client swaps between them without a round trip.
                var targetQuotes = firstQuote?.Kind == GoldCostKind.Derived
                    ? quotes.Where(q => q.Command.TargetId is not null && q.Quote is not null).GroupBy(q => q.Command.TargetId!).ToDictionary(g => g.Key, g => g.First().Quote!) : null;
                var label = ability.Label + (performer is null ? "" : $" · {Definition(Card(performer)).Name} ({performer})") + (costOption is null ? "" : $" · {costOption.Label}")
                    // The Discipline play of a card in a discard pile says what it is, next to its Gold quote (issue #331); so does the
                    // play of one granted by Daigotsu Bofana - exp (issue #333).
                    + (PlayedFromDiscard(ability, source) ? (ability.Discipline ? " · Discipline (removed from the game)" : " · from your discard pile (removed from the game)") : "")
                    // A copied ability says whose it was (issue #338), so the player can tell it from the card's own printed ones.
                    + (source is not null && !(Definition(source).Abilities ?? []).Contains(id) && source.GrantedAbilities.FirstOrDefault(g => g.AbilityId == id) is { CopiedFromName: { } copiedFrom } ? $" · copied from {copiedFrom}" : "");
                yield return new("useAbility", label, legal.Count > 0, legal.Count > 0 ? null : error ?? "No legal target or performer.",
                    legal.FirstOrDefault(), source?.Id, ability.Target == "provinces" ? provinces : legal.Where(c => c.TargetId is not null).Select(c => c.TargetId!).Distinct().ToArray(),
                    Gold: firstQuote, TargetGold: targetQuotes);
            }
        }
    }
}
