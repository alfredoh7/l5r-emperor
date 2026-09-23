#!/usr/bin/env python3
"""Build docs/CARD_INVENTORY.xlsx: every card in the reference catalogue
(content/deck-pool.json), one row each, with its Type, its own primary
printing's Set (read directly from content/oracle/emperor-catalogue.json --
deck-pool.json itself carries no set/printing info, so this is not threaded
through that schema, only read alongside it here), whether it has been wired
up (published into CardCatalog via any content/published/*.json wave), and --
for cards a wave audit has actually triaged -- a Blocker note naming the
specific engine gap keeping it unwired (curated by hand in the BLOCKERS dict
below; blank for anything not yet triaged this way).

Run this after any wave lands (a new content/published/*-wave-*.json file, or a
change to the pinned lion-crane-v1/vanilla-wave-1 pool) to keep the workbook in
sync -- it is regenerated from scratch each time, not hand-edited. Add to
BLOCKERS as each future wave's own audit produces new per-card determinations.

Usage: python3 tools/content/build_card_inventory.py
"""
from __future__ import annotations

import glob
import json
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.table import Table, TableStyleInfo

ROOT = Path(__file__).resolve().parents[2]
DECK_POOL = ROOT / "content/deck-pool.json"
EMPEROR_CATALOGUE = ROOT / "content/oracle/emperor-catalogue.json"
PUBLISHED_DIR = ROOT / "content/published"
OUTPUT = ROOT / "docs/CARD_INVENTORY.xlsx"

TYPE_ORDER = ["Stronghold", "Personality", "Follower", "Item", "Spell", "Strategy",
              "Holding", "Event", "Celestial", "Region", "Ring"]

# Per-card blocker notes, curated by hand (never inferred automatically) -- currently only the 77
# Followers left unwired after Wave 3 (issue #174), found by auditing each against the engine
# primitives available at the time. A handful turned out not to be blocked at all, just deferred
# for scope; those say so explicitly rather than pretending there's a technical gap. Extend this
# dict as future waves triage other Types.
BLOCKERS: dict[str, str] = {
    "oracle-l5r-10891": "Card creation is built (issue #387), but no local scan is a 1F Ronin Follower (Ronin Follower, oracle-l5r-10203, is 2F): an open question in docs/decisions/2026-09-20-created-cards-from-proxy-scans.md.",
    "oracle-l5r-683": "Needs a new Reaction trigger: \"after a battle resolution ends.\"",
    "oracle-l5r-10892": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142).",
    "oracle-l5r-10682": "Needs Home-timing generalization plus hypothetical-opposition movement.",
    "oracle-l5r-9867": "Needs a delayed/scheduled effect across turn boundaries.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
    # Honor and Treachery pass (issue #458): Bayushi Nitoshi - inexperienced (10248). "Battle/Limited: Look at the top card of your Fate deck. If it is
    # an attachment, you may attach it to one of your Personalities at Nitoshi's location, paying all costs. If it is a Strategy, you may take an
    # additional action to play it (if legal)." LookSpec (issue #whichever built the peek primitive) only supports the "look, then optionally discard/
    # keep/reorder" shapes; this needs a genuinely new "peek the top card, branch on its own type, and either attach it in place (paying its costs) or
    # grant an additional action to play it, leaving it on top if declined" primitive -- none of the three outcomes (conditional attach-in-place from a
    # peeked deck position, conditional additional-action grant keyed on the peeked card's type, "stays on top until then") exists today.
    "oracle-l5r-10248": "Needs a \"peek the top card of a deck, branch on its type, and either attach it in place (paying costs) or grant an additional action to play it, else leave it on top\" primitive -- LookSpec only covers look-then-discard/keep/reorder shapes, not a type-conditional attach-or-grant off a peeked (not drawn) card.",
    # Elephant Caravan (9868): the old note claimed a printed Attack-vs-attachments override (deferred in #158), but the card prints
    # no Attack at all ("Fear Battle: Target an enemy card with equal or lower Force. If it is bowed or an attachment, destroy it.
    # Bow it."). The real Follower-exception family (Kahoku - exp, Kosoko - exp, Ebumi, Strategic Strike, Toritaka Chokichi,
    # Ikiryo) is now generalized as AttackFollowerException (personality-wave-24). Whatever keeps this Follower unwired is not P1/P2.
    "oracle-l5r-9868": "Not blocked by the Attack-vs-attachments override after all (that note was stale: the card prints no Attack). Untriaged for a Follower wave; enemy-card-force-lte-self already matches its target.",
    "oracle-l5r-10679": "Needs \"grant keyword to whole unit\" (the passive Attack-strength defense half is now built -- Destrier/Singh Remnants -- and directly reusable here).",
    "oracle-l5r-10680": "The deck search primitive now exists (issue #312: a searched deck, criteria, show, effect, shuffle), but this card searches the discard pile FIRST and then the deck (\"search your discard pile, then deck, for another Enslaved Djinn\") -- a multi-source search, and a discard-pile candidate list, are not built.",
    "oracle-l5r-2520": "Needs an attachment Transfer mechanic.",
    # Dueling/Focus Effect reevaluation (issue #435): Dueling (#204) and Focus Effects (issue #419's FocusEffectChoice.cs) now exist, so the eight Before the Dawn cards once
    # blocked on "Dueling/Focus Effects (not started at all)" were re-read against the engine as it now stands. Three were fully wirable: Hiromi's Vassals and Shamsir share a
    # "you may attach this card to one of your Personalities, ignoring Gold cost" Focus Effect, generalized as FocusEffectChoice.cs's third choice kind; Home Guard's mandatory
    # +3F Focus Effect, +3F-while-defending static and Battle bow-and-Honor-gain each reuse an existing shape outright. No remaining blocker note for any of the three.
    "oracle-l5r-2598": "Needs an attachment Transfer mechanic (same gap as oracle-l5r-2520/10900): its Focus Effect is \"you may transfer a Follower from your Personality in this duel to another of your Personalities.\" The Battle half (move a target Personality at the current battlefield home or to an adjacent battlefield) is separately wirable.",
    "oracle-l5r-10681": "Its ordinary Equip is allowed from YOUR discard pile (\"You may Equip this Follower from your discard pile\"). The attach-from-a-discard-pile primitive exists (AttachAction.cs, issue #337) for an ability that TARGETS a card in a pile, but here the SOURCE card in the pile is the attachment (AttachSource.Self is hand-only) and the action is the ordinary Limited Equip command, not an ability -- not built.",
    "oracle-l5r-2722": "Needs a compound multi-keyword attach restriction (AttachRequiresKeyword only holds one today).",
    "oracle-l5r-10336": "Needs card creation (its army-scoped keyword-boost half is otherwise wirable).",
    "oracle-l5r-10588": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    "oracle-l5r-3589": "The Attack-vs-attachments override now exists (AttackFollowerException, personality-wave-24: an unconditional printed exception is one FollowerException: new(\"Ranged\")). Still needs \"last attached to\" tracking after detachment (the strength is that Personality's Chi, and the Honor loss and dishonor act on him).",
    "oracle-l5r-3611": "Needs a performer/wielder-keyed Attack-strength axis (already a known gap elsewhere).",
    "oracle-l5r-4118": "Needs a unit-Force-total comparison target (existing categories only compare individual card Force).",
    "oracle-l5r-10683": "Needs a per-card, permanent \"this Personality may not attack\" attachment flag (Utaku Chikako's own restriction, issue #186, is a player-and-turn-scoped flag, not directly reusable here) plus a self-destroying Limited ability. Dishonor-as-an-action is now built (Justice of the Crane) and directly reusable for its own Dishonor half.",
    "oracle-l5r-4699": "Needs a targeting immunity keyed off the acting Personality's own Gold Cost.",
    "oracle-l5r-10898": "Needs a persistent one-time destruction shield.",
    "oracle-l5r-10900": "Needs an attachment Transfer mechanic plus Home-timing generalization.",
    "oracle-l5r-9870": "Needs an \"Interrupt\" timing plus Attack targeting into the Defender's home.",
    "oracle-l5r-10518": "Focus 4, not 1 -- outside the #192 wave's own scope. The top-of-Fate-deck discard primitive now exists (Aberrations, issue #192), but this needs a \"whose deck\" target choice (\"any Fate deck\", not just your own) plus a variable-strength Melee Attack keyed off the discarded card's Focus Value, not a Force modifier.",
    "oracle-l5r-5329": "Needs an ad hoc \"move this Personality into battle\" mechanic, distinct from normal Maneuvers assignment.",
    "oracle-l5r-10337": "Needs a way for a TRAIT to negate the announced Political action's remaining effects, plus a Political-action classification (the flat +1PH half is separately wirable). The \"first time each phase\" half now exists: the once-per-phase limit (PhaseLimits.cs) and the announce-time targeting trait hook (Daigotsu Bofana, MatchEngine.OpenForcedDiscardForTargetedTrait).",
    "oracle-l5r-10519": "The Engage window and first-action claim now exist (issue #310; a Follower's 'Engage:' is a Reaction to trigger after-engaging, claim: MatchEngine.ClaimFirstOpportunity, conditional on being the Attacker). Still needs: a Follower-sourced Engage action scoped to 'this battlefield' (the Follower's own unit) and its optional 'you may target ... -3F' effect target; Reconnaissance for the acting side is settled (MatchEngine.GrantReconnaissanceAtCurrentBattlefield serves an Attacker at the enemy's province and a Defender at their own, see docs/decisions/2026-09-18-battle-timing-primitives.md decision 5).",
    "oracle-l5r-10587": "Needs a compound Gold-plus-card-discard cost shape (Kharmic keyword's own reminder).",
    "oracle-l5r-9873": "Needs a Maneuvers-assignment restriction and a new Reaction trigger (the passive Attack-strength defense half is now built and directly reusable).",
    "oracle-l5r-10517": "\"You may Equip this Follower as a Battle action\" is now a general capability (AttachSource.Self, issue #337: Fading Shadows attaches itself from hand to a performing Ninja). Shinobi Vassal differs in that the wielder is any Ninja Personality chosen as a target, and its own Battle action destroys a target enemy card without attachments and then the Follower itself -- deferred, not wired in #337.",
    "oracle-l5r-10338": "Needs a generic ability-usage-limit-reset mechanism.",
    "oracle-l5r-7776": "The action-lock it needed now exists (ActionLock.cs, issue #336): \"may not perform actions\" for the rest of the turn, blocking every action the card or its attachments perform, still targetable. Nothing else is known to block it (the Dragon-only attach is AttachRequiresKeyword, -4F is ApplyModifier, its target is the existing enemy-force category); not yet wired.",
    "oracle-l5r-10677": "The Engage window now exists (issue #310); its 'Engage: Give a target enemy Personality or Follower -2F' is a Reaction to trigger after-engaging. Still needs: the same target category costly-feud uses (enemy-force), validated as 'at the current battlefield' by hand (a Reaction is not a Battle action, so the shared Battle-target check does not apply), and the Follower as source of a reaction (in play, attached).",
    "oracle-l5r-8409": "Needs a \"usable even while bowed\" exception on a second ability (already a known gap elsewhere).",
    "oracle-l5r-9966": "Holding creation is built (issue #406: Charter, oracle-l5r-10806, published created-only). Needs a general Reaction trigger \"after a battle resolves, if this card's army destroyed a province\" (the only after-battle window is the Rings' own recruit window) and destroying this Follower as the cost.",
    "oracle-l5r-8837": "Needs an alternate Attack comparison axis against Gold Cost instead of Force.",
    "oracle-l5r-10339": "Needs a conditional-keyword-by-the-card's-own-clan mechanism (the attachment-destroy half is separately wirable).",
    "oracle-l5r-9874": "The Engage window now exists (issue #310); its 'Recon Reaction: After engaging at this card's battlefield' is Trigger after-engaging-at-own-battlefield (a Follower's own battlefield is its host's) with ActionKeywords [\"Recon\"]. Still needs: 'give your other Followers there +1F' (a per-Follower ApplyModifier sweep over the unit's siblings and the army's other Followers) and the Reconnaissance grant (already built for either side: MatchEngine.GrantReconnaissanceAtCurrentBattlefield, see docs/decisions/2026-09-18-battle-timing-primitives.md decision 5).",
    "oracle-l5r-9681": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    # Reserve (issue #184): the primitive itself (Dynasty absent-battle recruit; Fate Equip (Battle)
    # attachment, no bonus Additional Action for non-Spells) is built and demonstrated on Hida Kurabi,
    # Matsu Ryohei, and Floating Reserve (all pure reminder text). These carry Reserve too, but each
    # also needs something else first.
    "oracle-l5r-10870": "Needs an \"Absent Interrupt\" mechanic (Bayushi Wateru's own printed ability, on top of Reserve).",
    "oracle-l5r-10874": "Needs a Follower-Equip Battle ability with no Shugenja/opposed requirement (Daigotsu Chiboshi's own \"Equip a Follower to Chiboshi\"; the attach primitive of issue #337 needs a hand pile for it), plus tying its own Additional Action grant to a Reserve recruitment specifically.",
    "oracle-l5r-10862": "Needs a \"count of Spells in this unit\" Attack-strength axis (Isawa Ikariya's own Ranged Attack).",
    "oracle-l5r-10842": "Needs conditional Gold-cost reduction (Kakita Ujirou's own \"enters play for N less Gold if...\").",
    # Tamori Shinji (oracle-l5r-10846) published in personality-wave-12 (issue #216) -- his own
    # narrow "enters play for 4 less Gold if you are the Defender" now applies specifically via
    # RecruitReserve (QuotePurchase's own new viaReserve parameter), not the fully general
    # persistent Gold-cost-modifier primitive a future card like A Fugitive Apprehended would need.
    "oracle-l5r-10890": "Needs an \"Absent Engage\" mechanic (Utaku Sakiko's own printed ability, on top of Destined/Reserve).",
    # Moshi Ikako (oracle-l5r-10857) published in personality-wave-14, and Scholarly Hermit
    # (oracle-l5r-10684) in follower-wave-9, once their own scan images became available locally via
    # the WebP working-copy pipeline (issue #200) -- see issue #196, now resolved. Moshi Ikako's own
    # local catalogue "stats" block did not match her actual scan (cost/chi/force were each wrong);
    # hand-corrected against the image directly, see package_personality_wave_14.py's own
    # STATS_OVERRIDE.
    "oracle-l5r-10300": "Needs an \"actions must target this card if legal\" forced-redirection rule (Yoritomo Naoto's own Battle ability), on top of its own Additional Action grant. The legality half now exists (ActionAcceptsTarget, issue #335 -- \"if legal\" is that check); the standing hook that applies it to every action is the gap.",
    # Rings (issue #194): CardType.Ring, recruitment as a granted Additional Battle Action,
    # Enlightenment victory. Ring of Air, Ring of Water, Ring of the Void, False Ring of Air, and
    # False Ring of Water (oracle-l5r-6333/6339/6338/10531/10535) published in ring-wave-1 once their
    # own scan images became available locally (issue #196, now resolved via the WebP working-copy
    # pipeline, issue #200).
    #
    # Revalidated against the now-available scans (a follow-up to issue #194/#196): Ring of Earth and
    # Ring of Fire's own blocker notes below were themselves based on mismatched/incomplete scraped
    # text, the same class of problem issue #195 flagged for Ring of Fire specifically -- corrected
    # here now that the actual printed text is directly readable.
    # Ring of Earth and Ring of Fire (oracle-l5r-6334/6335) published in ring-wave-1 once their own
    # scans became available -- their earlier blocker notes were themselves based on mismatched
    # scraped text (Earth's own supposed "Interrupt timing" and Fire's own supposed duel requirement
    # were both artifacts of a wrong printing's text, the same class of problem issue #195 originally
    # flagged for Fire specifically). No remaining blocker note for either.
    "oracle-l5r-10533": "Needs an Interrupt timing (not built) for its own printed action ('Interrupt: Reduce all Honor gains and losses from an action by 2'). Its own recruitment condition ('if your maximum hand size is 10 or more') is no longer blocked -- MaxHandSize (issue #223) already exposes this per player.",
    "oracle-l5r-10534": "The Engage window now exists (issue #310) for its own printed 'Engage, bow: A target Personality at any location will not move this turn' (a Ring, in play, bow cost). Still needs: a 'will not move this turn' restriction (no such durable movement block exists) and its target at any location; its recruitment condition is otherwise wirable.",

    # Focus-1 Battle-action wave audit (issue #192): every unwired Focus-1 card printing a Battle
    # (or Battle-prefixed/Battle/Open) action, triaged against the primitives available at the time.
    # Three turned out wirable with zero or one small new primitive and were wired instead of listed
    # here: Sankaku-Yari (oracle-l5r-6469), Pearl of Embers (oracle-l5r-5902), Ritual Preparation
    # (oracle-l5r-10714) -- see Item Wave 2 / Strategy Wave 4. Several more below are genuinely close
    # (noted "Not blocked; deferred for scope") but were left for a future pass to keep this wave's
    # own scope bounded.
    "oracle-l5r-634": "Needs a Retainer mechanic, movement to any location (not just home), and a province-card discard (three separate gaps).",
    "oracle-l5r-1151": "Needs an attachment Transfer mechanic (issue #435 reevaluation; same gap as oracle-l5r-2520/10900/2598): its Focus Effect is \"you may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location.\" Its Battle half also needs two live-tracked conditions not built anywhere else: \"you have not put cards into your hand this phase\" and \"you have attached a Spell this turn.\"",
    "oracle-l5r-9900": "Needs a new Reaction trigger: \"after an action resolves that a Personality performed\" (the Terrain-play and additional-Battle-action halves are separately wirable).",
    "oracle-l5r-1607": "Needs Chi as a repeatable payable cost resource (distinct from a Force/Stat modifier), interacting correctly with the existing Chi<=0 death rule.",
    "oracle-l5r-2419": "The general \"before this battle resolves\" window now exists (issue #310: a Terrain's trait is an AbilityCatalog entry of Timing BeforeResolution, applied once both players pass, ordered by the turn player). Still needs its own effect: TWO independent optional choices ('you may choose and straighten a Samurai ... and you may choose and bow a non-Samurai') -- the optional window (beforeBattleOptional) names one target today; the Terrain-play half is separately wirable.",
    "oracle-l5r-2613": "Needs an Absent-Battle-timed Terrain-play variant (the existing one is Battle-timed only) plus a province-strength passive.",
    "oracle-l5r-10376": "Needs a movement-negation passive for a Terrain (the Terrain-play and additional-Battle-action halves are separately wirable).",
    "oracle-l5r-2972": "The general \"before this battle's resolution\" window now exists (issue #310). Still needs its own effect: the controller chooses one unit at the current battlefield to move home, and may then bow it (a mandatory unit-choice window followed by an optional bow); the Terrain-play half is separately wirable.",
    "oracle-l5r-3016": "The general \"before this battle resolves\" window now exists (issue #310); this Terrain's trait is a mandatory sweep (+1F each of your Human Personalities and Followers at the battlefield, -1F each Nonhuman one there) -- the shape training-before-resolution-boost already proves -- so it is nearly unblocked (Human = no Nonhuman keyword); the Terrain-play half is separately wirable.",
    "oracle-l5r-10941": "Needs a self-replacing Terrain variant (\"destroy a Terrain if able, then play this one\") plus an ongoing army-wide Force passive tied to being in play as Terrain.",
    "oracle-l5r-3767": "Its Follower may come from your hand OR a discard pile, paying 1 less Gold. The attach-from-a-discard-pile primitive (issue #337) covers the discard half (AttachCost(-1, 0)); a hand-or-discard pile choice (AttachPile has discard values only) is the piece left.",
    "oracle-l5r-3790": "The move to a destination the ability's own rule defines (MoveSpec, issue #336) now moves an enemy Personality to the current battlefield and can move the chosen performer with it (Movers performer-and-target). Still needs a target category for \"another player's Personality not at the current battlefield\" and the \"straighten each unit that this moved\" follow-up.",
    "oracle-l5r-10735": "The general \"before this battle resolves\" window now exists (issue #310). Still needs its own effect: a MANDATORY one-target choice ('straighten a Follower or Personality') -- beforeBattleOptional offers decline-or-target; a mandatory-target variant is a small addition; the Terrain-play half is separately wirable.",
    "oracle-l5r-10943": "Needs a self-replacing Terrain variant plus an ongoing army-wide Force passive tied to being in play as Terrain (same two gaps as oracle-l5r-10941).",
    "oracle-l5r-10551": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900/4118).",
    "oracle-l5r-5046": "Needs a keyword-conditional cost EXEMPTION (bow the performer unless it has a given keyword) -- distinct from every existing keyword-conditional EFFECT.",
    "oracle-l5r-5444": "Needs a \"count this card's own keywords, excluding a named pattern\" mechanism (novel).",
    "oracle-l5r-5533": "Needs an exemption from the generic Battle-timing army-presence requirement (the adjacent-battlefield Performer shape itself, Moto Gunso's own, already exists).",
    "oracle-l5r-10386": "Needs a dual-target selection with a relational constraint (\"not sharing a Clan alignment\"). The multi-target shape (issue #336) makes the two targets independent slots, each judged on its own; a constraint BETWEEN the two chosen targets is still not expressible.",
    "oracle-l5r-9919": "Needs a delayed negation of a FUTURE action (issue #435 reevaluation; same gap as oracle-l5r-2580): its Focus Effect is \"after this duel ends, negate the effects of the next action performed by its winner\" -- every negation the engine has applies to an action already being held in a reaction window, none waits on a card until it next acts. The Battle half (a Monk performer, move an enemy Personality to a different battlefield, conditional bow) is separately wirable.",
    "oracle-l5r-5928": "Its Reaction (\"after another player's action targets your Personality: Discard him\") is now expressible through the targeted-by-action trigger (issue #335, played from hand: see the decision note's Known limits); the Battle half needs card creation (a temporary token Personality) -- a known gap.",
    "oracle-l5r-5941": "Needs a non-AtBattle-gated compound Performer category for BattleOrOpen abilities (every existing keyword-Performer category requires the current battlefield).",
    "oracle-l5r-10388": "Needs a \"count copies of this same card played this battle\" mechanism (novel; the additional-Battle-action half is separately wirable).",
    "oracle-l5r-6034": "Needs a Gold-Cost comparison Target category (same gap as oracle-l5r-24).",
    "oracle-l5r-6727": "Needs a non-AtBattle-gated Performer category (\"at any location\", same gap as oracle-l5r-5941).",
    "oracle-l5r-7435": "Needs an attachment Transfer mechanic (issue #435 reevaluation; same gap as oracle-l5r-2520/10900/2598/1151): its Focus Effect is \"you may transfer an attachment from your Personality in this duel to another of your Personalities.\" The Battle half (an enemy card without attachments, bow it, destroy it if this Personality is a Berserker) is separately wirable.",
    "oracle-l5r-10132": "Needs the delayed revival: \"Remove this Shugenja from the game; his attachments stay attached. Before this turn ends, put his unit into play, ignoring costs, entering-play effects, and restrictions.\" RemoveFromGame detaches and discards nothing of a Personality's attachments today only because callers never remove an attached host; a removed unit kept whole (host plus attachments, out of play), and an end-of-turn entry of that whole unit that skips Gold, Honor Requirements, entry Reactions/traits and attach restrictions, are not built (PendingEndPhaseRecruits re-enters one dead Personality by the ordinary recruit path). The cost half (\"destroy this card unless this Shugenja is Void\") is CostVerbs.Sacrifice with UnlessKeyword and is buildable now.",
    "oracle-l5r-7995": "Needs a delayed duel-loss-consequence mechanism (issue #435 reevaluation): its Focus Effect is \"you may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins\" -- genuinely novel duel-resolution-timing plumbing (Strength of the Spider, oracle-l5r-8362, is blocked on the identical clause and deliberately deferred rather than invented for one card; see CardAbilities.cs). The Battle half (a Mantis Clan performer, move to the current battlefield, an additional Battle action) is separately wirable.",
    "oracle-l5r-10718": "Needs a self-replacing Terrain variant plus a delayed post-destruction straighten (two gaps, same self-replace gap as oracle-l5r-10941).",
    "oracle-l5r-8188": "Needs a new trigger keyed to passing instead of a Battle action (novel; the Terrain-play half is separately wirable).",
    "oracle-l5r-10958": "Needs a \"Political\" Reaction timing (not built; the plain Ranged 1 Attack half is separately wirable).",
    "oracle-l5r-8311": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900) plus a per-card override of \"a bowed card contributes no Force.\"",
    "oracle-l5r-10699": "The ability-copying primitive now exists (CardCopy.cs, issue #338: copy one ability that does not itself copy abilities, with Duration \"Turn\" for a copy \"this turn\"). What is still missing is this Spell's own target category (a Spell at this battlefield or a Thunder Spell at any location) and copying onto a Spell receiver during a Thunder Battle action.",
    "oracle-l5r-10960": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900).",
    "oracle-l5r-10963": "Needs an \"Interrupt\" timing (not built; the plain Melee 2 Attack half is separately wirable).",

    # Lion Clan Personality review (issue #198): the 49 unwired Lion Clan Personalities left after
    # wiring Personality wave 8's 17 cards, triaged against every primitive built through wave 8.
    "oracle-l5r-10849": "Needs a modifier hook into the existing Tactical Advantage rulebook action's own Force-bonus amount (a generic action-amount-modifier mechanism doesn't exist yet).",
    "oracle-l5r-241": "Card creation is built (issue #387): the 2F Follower it creates is the Infantry Follower scan (oracle-l5r-9738), not yet published as a created-only Follower wave. Remaining: that wave and this card's Creates ability. 'Tactical Limited' itself maps onto the existing Limited Timing per the established Political/Tactical precedent, so is not itself a blocker.",
    "oracle-l5r-10241": "Needs a broad, repeatable 'before another player's action resolves' Reaction trigger (a before-resolution window keyed to enemy actions, not built).",
    "oracle-l5r-294": "Akodo Tamisu. Her second sentence -- 'After your Tactical, Bushido Virtue, or Terrain action resolves: Give a Lion Clan Personality +1F' -- is now classifiable: the after-action-resolved Reaction window (issue #437) plus ActionCarriesKeyword's own source-card-keyword fallback (issue #321) read Tactical/Bushido Virtue/Terrain the same way Matsu Rika's self-buff does (issue #449), a three-way ActionCarriesKeyword check away rather than a new trigger family. Still needs a 'choose a target Lion Clan Personality' shape for a Reaction whose printed wording is mandatory once triggered (Rika's own self-only case sidesteps this), and separately her entry effect 'discard all your other Sensei Personalities' (recurs on Ikoma Aimi's base printing) -- neither built yet.",
    "oracle-l5r-305": "The Phoenix-opposition Force bonus is directly wirable (reuses this wave's own clan-conditional attacking-vs-dragon-bonus pattern). Its Limited ability is now expressible: the look-at-the-top-N-cards primitive (issue #322, DeckLook.cs) covers 'look at the top Fate card; leave it, discard it, or put it at the bottom' as a LookSpec (N=1, own Fate, discard 0-1, bottom 0-1) -- unblocked, deliberately not wired in that PR.",
    "oracle-l5r-10850": "Needs a 'retrieve a specific card from your discard pile into hand' primitive (distinct from deck search; not built).",
    "oracle-l5r-10638": "Needs a 'will not gain [keyword]' immunity flag, and a per-performer Timing override on the shared Tactical Advantage rulebook action -- neither built.",
    "oracle-l5r-3607": "Needs generalization of the existing move-home primitives (Incapacitated/Block are battle-resolution-specific, tied to attacker/defender roles) into a standalone 'move this targeted Personality home' Battle-ability effect, usable as one branch of a dual choice alongside a Force bonus.",
    "oracle-l5r-10639": "Invest itself is built, but this variant needs 'refill the specific province this card entered from, face-up' -- a targeted, face-up province refill tied to entry-provenance (existing refills are face-down and untargeted).",
    "oracle-l5r-10460": "The Open Cavalry-grant is directly wirable (reuses the existing generic GrantKeyword primitive); blocked only by 'will not join a Unicorn Clan player' -- a recruitment clan-exclusion rule, not built.",
    "oracle-l5r-10291": "Needs a reveal-a-hand-card-and-offer-to-another-player mechanism with an opponent's accept/reject choice, plus a Home-targeted Attack (existing Attacks only target the current battlefield).",
    "oracle-l5r-3637": "Needs an optional-discard-for-attack-bonus primitive (a Ranged Attack whose strength depends on an optional cost paid at the same time, plus a card-type-conditional follow-up draw) -- not built.",
    "oracle-l5r-3638": "Now expressible on the multi-target shape (TargetSlots.cs, issue #336): two slots, terrain-at-battle and enemy-card-without-attachments, both existing categories; not yet wired.",
    # Ikoma Shizuka (oracle-l5r-10292) published in personality-wave-27 (issue #312), the first card wired on the
    # deck search primitive (DeckSearch.cs) -- her former note ("Needs deck search ... and Regions (neither built)")
    # was stale on the Regions half: Regions have been built since region-wave-1. No remaining blocker note for her.
    "oracle-l5r-3660": "The Battle Ranged 4 Attack is directly wirable; blocked by its own passive -- a global 'all your Ranged Attacks get +1 strength while you control a Terrain' scope, distinct from a single card's own conditional Attack strength (existing kamina-exp-attack/shuji-attack-style bonuses only affect their own card's attack, not every Attack the controller makes).",
    "oracle-l5r-10223": "The Engage window and first-action claim now exist (issue #310; Recon Reaction, Trigger after-engaging-at-own-battlefield, claim via ClaimFirstOpportunity). Still needs: the claim's printed restriction 'which must be a Terrain action' -- FirstActionRestriction already binds a first action to a performer (Naval Invasion) or to passing (Rebuilding Kyuden Hida); a required-keyword variant (an action from a Terrain-keyword card, including the playStrategy Terrain path) is the one new field.",
    "oracle-l5r-10461": "Ring of Earth is now published (ring-wave-1); 'Earth Battle' is very likely a plain printed-keyword classification too, following Moshi Ikako's own 'Fire Battle' precedent (oracle-l5r-10857, confirmed issue #194) -- not re-verified against this specific card's own scan yet. Still needs a conditional Gold-Cost waiver tied to Ring control before wiring Jikoji's own army-wide Force bonus (the waiver itself: no current Gold discount is conditional on board state, only clan-alignment-based or a fixed printed amount).",
    # Kitsu Akai (oracle-l5r-4371) published in personality-wave-17 (issue #257) once card creation
    # itself landed (MatchEngine.CreatePersonality) -- his own Limited half creates the Lion Ancestor
    # (oracle-l5r-9785, also published there, CardDefinition.CreatedOnly) his Battle half uses as a
    # Performer. No remaining blocker note for him.
    "oracle-l5r-4382": "Now unblocked (issue #387, not wired there): its Reaction is a Creates ability (the Lion Ancestor scan is published) plus gaining 1 Honor. See docs/decisions/2026-09-20-created-cards-from-proxy-scans.md.",
    # Kitsu Miro (oracle-l5r-4386) unblocked in the Embers of War pass (issue #460, batch 2): the stale note below claimed a
    # "Home Battle" Timing and creation at the current battlefield were both still missing. Neither ever needed its own new
    # primitive: Absent (Coils of Madness follow-up) already lets a Battle-timing source act without controlling a unit at
    # the battlefield ("Home actions can be taken from home" is exactly that), and CreatedCardSpec.AtBattlefield/
    # UntilBattleEnds (issue #394) already exist -- proven by Kitsu Akai's own identical 2F/2C/3PH Lion Ancestor token
    # (oracle-l5r-9785), which Miro's own miro-create ability resolves to unchanged. See personality-wave-63.
    "oracle-l5r-10293": "The deck search primitive now exists (issue #312) and its criteria can express \"a non-Unique Lion Clan Shugenja\" from the Dynasty deck, but the effect is \"refill this province with him, face-up\" (a search effect the primitive does not have yet) and the ability must be usable from a face-up province card whose own cost is discarding her and paying 2 Gold.",
    "oracle-l5r-9826": "Card creation is built (issue #387) but creates one card per action: two created Lion Ancestors need a count on CreatedCardSpec; 'overlays' as an alternate entry-trigger condition (distinct from ordinary Purchase) is not yet verified as built.",
    "oracle-l5r-4396": "Needs a per-player 'controls an Ancestor' Honor-gain/loss sweep referencing each player's own starting Family Honor baseline (not tracked as a stat) plus an imposed choice against another player's own card state.",
    "oracle-l5r-10852": "Needs Interrupt timing (not started); card creation is built (issue #387, the Lion Ancestor scan is published).",
    "oracle-l5r-4397": "Card creation is built (issue #387); still needs a turn-spanning \"lost Honor from another player's action since your last turn began\" tracked condition, and a one-to-four selection whose MAXIMUM depends on that condition (the multi-target slots, issue #336, have a fixed Min..Max per slot; a condition-dependent Max is the remaining gap).",
    "oracle-l5r-4916": "Needs a unit-scoped 'bow every card in the target unit meeting a Force comparison' primitive (existing unit/army-scoped primitives only apply static Force modifiers, none perform a bow across a filtered set of cards); the per-card Honor gain has direct precedent (BattleHonor).",
    "oracle-l5r-4944": "Needs an activated (not passive/static) 'apply a Force penalty to every card in the target unit, then bow any now at 0' primitive -- the existing unit-scoped Force primitives (UnitForceBoostPrimitive, ArmyBattleMaidenBoostPrimitive) are passive/continuous, not one-shot activated effects across a filtered unit.",
    "oracle-l5r-10294": "Needs a 'this card's Force counts twice during battle resolution' hook (no existing mechanism double-counts one specific card in a resolution total), and a personality-level 'will not attach Items' restriction (the existing 'Will only/not attach to X' restriction gates the ATTACHMENT side, not a blanket type-level refusal from the host).",
    "oracle-l5r-5010": "Needs a targeting-immunity flag against a specific Attack kind (Ranged) scoped to a whole unit -- distinct from the existing passive Attack-strength defense (Destrier/Singh Remnants), which reduces effective strength rather than making the target illegal outright.",
    "oracle-l5r-10464": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142) -- specifically a temporary, Gold-production-only control grant over another player's Holding, without a full ownership change.",
    "oracle-l5r-9828": "The payment-time alternate Gold source now exists (AlternateGoldSource in GoldQuotes.cs, issue #338: \"bow this card: produce N Gold\" chosen inside the payment, as Yoritomo Sakuma does). Zlkyt needs it extended with a sacrifice chosen inside the payment: \"destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost\" (a target selected as part of the payment, destroyed as the cost, its cost read as the amount).",
    # Dragon Clan Personality review (issue #207): the remaining 35 of 79 catalogue Dragon Clan
    # Personalities left unwired after wave 9. The "after a duel resolves" Reaction trigger noted
    # here originally (own-won-duel, issue #209) has since landed and published three of these five
    # (Kitsuki Daisuke-exp, Kitsuki Horume's own second ability, Kitsuki Jakuei) in wave 10 -- see
    # their own now-updated/removed entries below. Mirumoto Yozo, Experienced 2's own broader "after
    # an Iaijutsu action resolves" generalization is also since published (IsIaijutsuAction, see his
    # own now-removed entry below); the "Tactical/Bushido-Virtue/Kiho/Recon 'was this an [X] action'"
    # classification-trigger gap this once cited is itself now closed too (ActionCarriesKeyword,
    # issue #321, plus the after-action-resolved Reaction window, issue #437 -- Matsu Rika, #449).
    "oracle-l5r-10451": "Needs a conditional zero-cost Purchase gate tied to controlling a specific named Ring, and a delayed 'increase the Honor gained from your next Proclaim this turn' effect -- neither exists; every current Gold discount is either clan-alignment-based (Clan Discount) or a fixed printed amount (Tamori Shinji's own still-unbuilt case below), never conditional on board state.",
    "oracle-l5r-10452": "The look-at-the-top-N-cards primitive (issue #322) covers 'look at the top three cards of your Fate deck, show an Iaijutsu Strategy and put it into your hand, put the remaining cards back in any order' (a show disposition with criteria, exactly Bayushi Kahoku exp2's shape). Still needs the follow-up 'then discard a card' (the same after-effect step Ikoma Shizuka's Terrain half has in the deck search) and an Iaijutsu Strategy criteria check -- not wired in that PR.",
    # Kitsuki Daisuke, Experienced and Kitsuki Jakuei published in wave 10 (issue #209) once the
    # after-duel-won Reaction trigger landed. Kitsuki Horume's own second ability (gain 1 Honor
    # after winning a duel) published there too, and his first ability (the "choose a player, he
    # gains or loses 1 Honor" entering-play Reaction) was unblocked immediately afterward by the
    # new "choose a player" targeting primitive (issue #210) -- both of his abilities are wired now,
    # no remaining blocker note for him.
    # Kitsuki Minori (oracle-l5r-10052) published in personality-wave-13 (issue #223) once the
    # Overconfident keyword mechanic landed -- her own conditional keyword-suppression ("does not
    # have Overconfident while any other player is Lion Clan") is wired too, as a subtractive live
    # Keyword() check. No remaining blocker note for her.
    "oracle-l5r-4446": "Needs a new Reaction trigger for 'after an action targets a Samurai' (a card-targeted, not action-classified, trigger -- a different shape from every existing Reaction window) and a duel-refusal override tied to a specific pending challenge -- both novel.",
    "oracle-l5r-9817": "Needs an ability-suppression mechanism ('a target Personality's printed abilities may not be used') -- distinct from the existing keyword-suppression gap (Ikoma Aimi's own base printing, issue #198) and not built either.",
    # Mirumoto Hikaro (oracle-l5r-5095) published in personality-wave-15 (issue #207 follow-up) once
    # the Sensei keyword mechanic and Dueling's own discard-pile Focus extension both landed -- no
    # remaining blocker note for her.
    "oracle-l5r-10845": "Needs an Additional Action grant that re-triggers a second, differently-costed printed ability on the same performer -- buildable in principle by reusing the existing Additional Action primitive, but deferred this pass for scope.",
    # Mirumoto Katagi (oracle-l5r-5112) published in personality-wave-12 (issue #216) -- his own
    # Non-Unique-Weapon Gold discount is wired (Equip's own cost formula). His own "weapons attached
    # to Katagi will not be transferred" clause is still a no-op: no attachment-Transfer mechanic
    # exists anywhere in the engine yet, and building one purely to give this one clause something
    # to block (with zero real card performing an actual Transfer) would be speculative
    # infrastructure with no observable, testable behavior -- the same reasoning already applied to
    # Mirumoto Rokai/Tamori Tamoko's own still-unbuilt negation interrupt below.
    "oracle-l5r-10631": "Unblocked by the attach-from-a-discard-pile primitive (issue #337): \"Battle, [Gold]: Equip a target non-Unique Weapon in your discard pile to Katagi\" is one ability definition -- Attach: AttachSpec(Target, Own pile, Wielder Source, [Item], Keywords [Weapon], Excluded [Unique]) with Cost: GoldCostRule.AttachCost(), exactly the training-attach-weapon-discount shape at full cost (his from-hand discount does not apply to a discard-pile attach). Deferred for scope, not wired in #337.",
    "oracle-l5r-10284": "Needs a generic 'ignore this card's own bowed status' exemption for Maneuvers assignment AND Performer selection together -- narrower exemptions exist per-ability (the 'Even if bowed' id list), but this card's own exemption is unconditional and would touch both of those shared checks broadly for a single card; deferred this pass to limit blast radius.",
    "oracle-l5r-10285": "Needs a named-target restriction on a duel's own Target category (a self-performer duel against one specific enemy Personality by name, not any enemy) -- a small addition (mirrors AttachRequiresName's own shape) not attempted this pass.",
    # Mirumoto Rokai (oracle-l5r-9819) published in personality-wave-15 (issue #207 follow-up) once
    # a new "beforeActionReaction" interrupt landed -- a curated, extendable per-ability-id
    # classification (DestroysTarget/MoveOrStraightenKind in CardAbilities.cs), not the fully general
    # "any arbitrary action's own named effect" mechanism speculated here; see the primitive itself
    # for the exact scope. No remaining blocker note for him.
    # Mirumoto Yozo, Experienced 2 (oracle-l5r-10287) published in the same wave -- the Sensei
    # mechanic and a new IsIaijutsuAction classification (every currently self-performer duel-issuing
    # ability) both landed too. No remaining blocker note for him.
    "oracle-l5r-9820": "Its 'no province destroyed since your last turn ended' flag now exists (MatchState.ProvinceDestroyedSinceTurnEnd, issue #299 -- set by MatchEngine.DestroyProvince, cleared at the end of THAT player's own turn in FinishTurn, not StartTurn, which would erase a destruction that happened on the opponent's turn just before it); this card itself has not been re-audited or wired yet. Its own static '-2F on your turn' half has direct precedent and was not blocked, just not prioritized.",
    "oracle-l5r-7780": "Needs an exception to the shared Battle-timing AtBattle requirement (bowing itself 'even if not at the current battlefield') -- buildable as a one-off id exception, the same shape Mirumoto Michi/Togashi Rikyou use in wave 9, but deferred this pass to limit how many such exceptions land in one wave; its own province-Strength raise-or-lower effect directly reuses The Courtesy of the Crane's own precedent and is not itself blocked.",
    # Tamori Ruya (oracle-l5r-7789) published in personality-wave-12 (issue #216) -- the broad
    # "after your own action bows a Personality" Reaction trigger landed as own-action-bowed-
    # personality, narrower than oracle-l5r-3083's own still-unbuilt "affects an opposing
    # Personality" (a genuinely broader trigger, not resolved by this).
    # Tamori Shosei (oracle-l5r-7797) published in personality-wave-16 (issue #207 follow-up) once
    # the general "own-source-or-unbowed-holding-with-keyword" cost-choice Performer category
    # landed -- bow Shosei or one of his controller's own Temple Holdings. No remaining blocker note
    # for him.
    # Tamori Takeshi (oracle-l5r-10055) published in personality-wave-11 (issue #212) once Protection
    # itself landed -- no remaining blocker note for him.
    # Tamori Tamoko's own SECOND ability needs the same general "before an arbitrary resolving
    # action, negate one of its own named effects" interrupt as Mirumoto Rokai above (here: movement
    # or straightening, not destruction) -- Protection itself would already respect it once that
    # interrupt point exists, same as Rokai's own case.
    # Tamori Tamoko (oracle-l5r-10056) published in personality-wave-15 (issue #207 follow-up) --
    # her own Open action (a new turn-stamped CardInstance.AssignedToAttackTurn flag, checked in
    # EndTurn) and her own Reaction (the same beforeActionReaction interrupt as Rokai above, but
    # movement/straightening, no cost, no "another player's" restriction) both landed. No remaining
    # blocker note for her.
    "oracle-l5r-10288": "Needs a 'destroy one of my own attached cards as a cost' cost-target category, combined with a temporary Stalwart grant and a self-Force-copy modifier -- each piece individually has precedent (DestroyAttachment, turn-duration GrantKeyword, ApplyModifier), but the three-part combination was deferred this pass for scope.",
    # Tamori Yayu (oracle-l5r-7806) published in personality-wave-12 (issue #216) -- the
    # bow-or-destroy alternate-cost-choice shape landed as a dual-catalog UsageKey pair
    # (yayu-bow-attack/yayu-destroy-attack), the same shape as jakuei-bow/jakuei-bow-favor; his own
    # conditional Cavalry-keyword grant ("while it is not your turn") reuses the existing
    # conditional-Force-bonus pattern's own IsAttacking-style check and was wired alongside it.
    # Togashi Hizumi (oracle-l5r-8521) published in personality-wave-12 (issue #216) -- his own
    # Reaction half landed as bowed-personality-at-own-battlefield; his Battle half (raise/lower
    # the current battlefield's province strength) reuses The Courtesy of the Crane's own precedent
    # directly.
    # Togashi Mitsu (oracle-l5r-10455) published in personality-wave-16 (issue #207 follow-up) once
    # ResolveOrDeferAttack gained a new strengthOverride parameter -- destroy zero or more of his own
    # Fire tokens (named via command.CardIds), Ranged Attack strength equal to the number destroyed.
    # No remaining blocker note for him.
    # Togashi Noboru (oracle-l5r-10058) published in personality-wave-12 (issue #216) once the
    # Brash keyword and Force-redirection-to-an-enemy-army mechanisms both landed -- no remaining
    # blocker note for him.
    "oracle-l5r-8565": "Needs a 'same location as source' targeting axis for a non-battle-restricted (BattleOrOpen) ability -- every existing 'at the same battlefield' check assumes Battle timing's own blanket AtBattle requirement, which BattleOrOpen deliberately carries none of.",
    # Togashi Sakata (oracle-l5r-8570) published in personality-wave-16 (issue #207 follow-up) once
    # AttackStrength/EffectiveAttackStrength gained a new sourceId parameter, reading his own live
    # Sake token count through the pre-existing TokenCount helper; his own dual Fire-or-Sake token
    # choice is a UsageKey-shared catalog pair (sakata-fire-token/sakata-sake-token), the same shape
    # as yayu-bow-attack/yayu-destroy-attack. No remaining blocker note for him.
    "oracle-l5r-10457": "Needs a genuine 'while another player's action is currently resolving' state flag, distinct from the simpler 'while it is not your turn' condition already used elsewhere (Tamori Yayu) -- a simplified reading would just reuse that existing pattern, but wasn't attempted here to avoid mischaracterizing the printed text.",
    # Xijkt (oracle-l5r-9415) published in personality-wave-16 (issue #207 follow-up) once the
    # own-source-or-unbowed-holding-with-keyword cost-choice Performer category landed (bow Xijkt or
    # one of his controller's own Mines), alongside a narrow, additive-only Cavalry-eligibility
    # override (Assign's own new isDefender parameter, gated on Effect=="cavalry-if-defender") rather
    # than broadening the shared IsCavalryUnit logic itself. No remaining blocker note for him.

    # Second City pass 2 (issue #264, 2026-09-21): every Second City card still unwired was re-checked against the engine as it now stands. 29 were wired (the list is in
    # docs/decisions/2026-09-21-second-city-pass.md; Mendon's rulings the same day unblocked five of them) and their notes removed; the rest were rewritten, because several old blockers
    # named gaps that later work closed (Open-timed keyword Performers, #322 look, #336 Gold-cost stat and action lock, #337 alternative costs, #387/#399 created cards and copies, #388
    # grants). What is left falls into five families: (1) a decision made by the OTHER player inside your action (Dragon's Breath Castle, Know Their Minds, Scandalous Rumors); (2) windows after
    # a battle's resolution or end (Redoubled Attack, Tales of Dishonor, Never Safe); (3) straighten and movement locks that span turns (Hibernation, Visitation, A Quiet Transaction, Strength of
    # the Tsunami); (4) Terrain statics (Ancient Feud, Hasty Fortifications, Shiotome Plains); (5) single gaps, each named on its own card.
    # Issues #415-#420 (2026-09-21, Mendon's rulings): a general primitive was built for each family and the 22 cards of this pass were wired on them -- straighten holds and movement locks
    # (#415), stat-based Gold costs and conditional production (#416), the after-resolution window (#417), province strength maximum and Terrain statics (#418), the other player decides, a
    # paused Focus Effect and sequenced targets (#419), permanent copying, Kata, target bars and dead Personalities as discarded (#420). Blessings of the Shi-Tien Yen-Wang (1040) is the one
    # Second City card of the pass left unwired; its note below says what it still needs.
    # Second City unwired-card audit (issue #264): the final rest-batch pass also finalized two
    # cards the triage had left uncertain, and found one Region the triage flagged BUILDABLE* to
    # actually be blocked -- see docs/decisions/ for the full audit.

    # Second City unwired-card audit, non-Personality rest batch (follow-up to issue #264): the 39
    # remaining unwired Strategy/Item/Celestial/Spell/Holding/Region cards, triaged against every
    # primitive available at the time. oracle-l5r-7524 (Strategic Sacrifice) turned out fully
    # buildable -- a plain-Battle Siege Performer (already a common shape), a routine new
    # Force-vs-province-strength Target category, and the existing bow-or-straighten dual pattern --
    # and was left out of this dict entirely rather than documented as blocked.
    "oracle-l5r-1040": "Needs (a) a 'put a dead Personality on the top of its owner's deck' primitive -- ToOwnerDeck is written for cards leaving play, and nothing offers it a Zone.Dead card; (b) a shield that outlives an action, 'negate the next card effect that bows, destroys, or moves him' (NegatedEffects belong to one held action and are dropped with it); and (c) the count of dead Personalities in all players' discard piles for the Force bonus (trivial).",
    "oracle-l5r-10522": "Created copies exist (issue #398: a real copy of the target, marked as created). Still needs a second target (bow one of your unbowed Personalities, copy another), the Item removing itself from the game as a cost, a lock \"will not straighten, attack, or defend until after your next turn begins\" on the second target, and removing the copy after your next turn begins (a created card removed at the start of its owner's next turn is not built). Rulings needed from Mendon: who controls the copy, where it enters, and how \"ignoring costs and restrictions\" treats a Unique target.",
    "oracle-l5r-9939": "A partial copy with computed Force: the same base Chi, Personal Honor and keywords as the target Paragon and a base Force equal to the target's Personal Honor, removed after the battle. Chi, Personal Honor and Force are per-instance values (issue #402) and removal after the battle exists (issue #394); copying a keyword set is not built. Rulings needed from Mendon: where the Personality is created (home or the current battlefield) and which scan it is (no generic Personality scan exists).",

    # Batch F of the "Not blocked; deferred for scope" audit (issue #301) published twelve Personalities from this
    # rest-batch in personality-wave-22 -- Agasha Kodo (142), Asahina Munefusa (505), Asako Rikate (576), Bayushi Nori
    # (857), Hida Mimori (3150), Isawa Kojiro (3850), Isawa Mitsuko (3866), Shosuro Hawado (7103), Shosuro Rin (7151),
    # Soshi Neiru (7360), Sutigu (7676), Yoritomo Minori (9605) -- and one Region, Shinden Shorai (6857), in region-wave-3:
    # every one of their old notes is gone. None of the cards those notes described turned out already published.
    # Second City unwired Personality audit (35-card rest-batch, follow-up to issue #264): every
    # unwired Second City Personality left after the main #264 pass, triaged against every primitive
    # available at the time, including the Dueling/own-won-duel, Ring, and "choose a player"
    # primitives that landed during #264 itself.
    # personality-wave-24 published Bayushi Kahoku - exp (807; its old note is gone -- the source-scoped, dishonorable-only Attack-vs-Followers
    # exception is now the generalized AttackFollowerException) together with five more Personalities whose only gap was that exception or
    # the generalized target-status predicate (TargetStatus): Tsuruchi Kosoko - exp (9831), Yoritomo Ebumi (9832), Kakita Kazan (4185),
    # Hiruma Nitani (3311) and Kuni Shinoda - exp (10276). None of the five had a BLOCKERS entry.
    # personality-wave-31 and follower-wave-17 (issue #337) published Kitsu Fukui (4375), Moto Jun-Ni (5337), Mirumoto Yumaru (5181), Hiruma Tensin (3332) and
    # Fading Shadows (2437); their old notes are gone. Their primitives -- the alternative cost (AlternativeCosts.cs), attach-from-a-discard-pile / attach-self
    # (AttachAction.cs), the conditional effect choice (EffectChoice.cs) and the Follower Performer -- narrow the notes of the cards named below, none of which was wired.
    # stronghold-wave-5 (issue #352) published Foothills Keep (2626): its Battle/Open attach is the attach primitive (AttachAction.cs) and its passive
    # "Equipping Weapons is a Battle/Open action for you" is the first player-level timing grant (TimingGrants.cs); its old note is gone. The same grant is
    # one table row away from Glory of the Shogun (2824) and Harbingers of War (10260), neither of which had a note (both are Events that must first stay in
    # play as an ongoing effect, and each adds a draw after the first such equip each turn).
    # personality-wave-30 (issue #335) published Mirumoto Ezuno (5086), Goju Yurishi (2862), Yasuki Jinn-Kuen - exp2 (9445) and Hida Mochitoko - exp (3152), and
    # item-wave-4 published Blade of Champions (1004); their old notes are gone. What they built: the targeted-by-action Reaction family (redirect an action to
    # another card, re-check a target that moved and negate its effects, several effect kinds negated at once), the per-turn use limit
    # (AbilityDefinition.UsesPerTurn), and -- after Mendon's rulings -- Attacks that need no card target (or target the current province), a cost-block predicate
    # (CostPaymentBlockedByNegation) and once-per-action Bofana judging. "Negate the action's effects" as a WHOLE is agent F's ActionNegation.cs (Sakuma, Jinn-Kuen); the
    # per-card negation here is for "negate its effects on him". The notes below on cards that needed only those pieces say what is left of each.
    # personality-wave-29 (issue #333) published Daigotsu Bofana (1706) and Daigotsu Bofana - exp (10315); the old note on 1706 is gone.
    # Its "broad after-another-player's-action-targets-this-card trigger" was already built for Akodo Kakihara (a Reaction window); Bofana's
    # is a mandatory TRAIT and fires when the action is announced (MatchEngine.AnnounceAbility), and his once-per-PHASE dedupe is now the
    # general phase limit (PhaseLimits.cs). Exp Bofana's Dark Virtue play needed no large primitive: an additional action scoped to one
    # discard-pile card (AdditionalActionGrant.FromDiscard). Neither card had a BLOCKERS entry for 10315.
    # personality-wave-32 and strategy-wave-21 (issue #336) published Daigotsu Hotako - exp3 (1726), Doji Yoshitada (2179), Sea Troll Predator (6522),
    # Utaku Liu-Xeung - exp (9085), Bonds of Coin (1092), Gold and Steel (2865) and Fear Me! (2493); their old notes are gone. The four primitives they
    # needed exist now: the action-lock (ActionLock.cs), the multi-target slots (TargetSlots.cs), the move to a destination the ability's own rule
    # defines (MoveDestinations.cs) and the stat-penalty hook (StatPenalties.cs), plus the Clan entry discount on QuotePurchase (ClanEntryDiscount.cs).
    # Cards the issue #336 primitives leave nothing (or little) to build for:
    "oracle-l5r-1679": "Not blocked since issue #336: its Battle action is Sea Troll Predator's verbatim (predator-move, MoveSpec source-and-target to an adjacent unresolved battlefield); not yet wired.",
    "oracle-l5r-3310": "Not blocked since issue #336: \"Bow him. He may not perform actions.\" is a bow plus the action-lock (ActionLock.cs); not yet wired.",
    "oracle-l5r-5532": "The action-lock exists (issue #336); this one lasts \"until this battle ends\" -- it needs a battle-scoped duration on ActionLock, plus reducing a unit's Force to 0.",
    "oracle-l5r-6077": "The move of an enemy Personality to the current battlefield exists (MoveSpec target/current-battlefield, issue #336); still needs its Magistrate performer and the status-conditional straighten; not yet wired.",
    "oracle-l5r-7587": "The multi-target slots exist (issue #336: one to three Personalities controlled by other players) and the command keeps the order they were chosen in, which is what its -1F, -2F, -3F assignment reads; not yet wired.",
    "oracle-l5r-8030": "The multi-target slots exist (issue #336: one or two target attachments); a Region Open action with a bow cost; not yet wired.",
    "oracle-l5r-8262": "Its Battle half (one to three enemy dishonorable Personalities moved home) is a multi-target slot now (issue #336); its Reaction half is the still-unbuilt \"negate unless the affected player pays a cost\" mechanism.",
    "oracle-l5r-9913": "The action-lock exists (issue #336) for its \"He may not perform actions\"; not yet wired (Battle/Open with a base-Force performer condition, +3F, straighten, an optional bow of a card at his location).",
    "oracle-l5r-9936": "The move of another player's Personality to the current battlefield exists (MoveSpec target/current-battlefield, issue #336); still needs its Monk performer and the Earth-conditional straighten; not yet wired.",
    "oracle-l5r-10143": "Not blocked since issue #336: two independent targets (your unbowed attacking Personality; an enemy Personality at any location) and the move of the second to the current battlefield (MoveSpec target/current-battlefield), plus the straighten; not yet wired.",
    "oracle-l5r-10529": "The multi-target slots exist (issue #336: one or two target enemy cards, a Min 1 / Max 2 slot); still needs the printed Fallen/maximum-Personal-Honor static and the bow cost; not yet wired.",
    "oracle-l5r-10716": "The multi-target slots exist (issue #336: one or two target opposed Personalities); still needs the count of different Clan alignments at the battlefield; not yet wired.",
    "oracle-l5r-10721": "Not blocked since issue #336: one to three of your target defending Personalities, +2F -- a Min 1 / Max 3 slot; not yet wired.",
    "oracle-l5r-10947": "Mercantile Conflict (Discipline, issue #353): the Discipline flag and the additive Discipline cost exist, and its first effect (bow your target unbowed Courtier, Political Open) is wirable, but \"the next two Honor losses from your cards (this turn) are increased by 1 each\" needs an Honor-loss-increase primitive -- a per-player, per-turn counter consulted at every Honor loss (GainHonor with a negative amount) that raises the first two by 1; nothing like it exists. Left unwired until that primitive is built.",
    # Before the Dawn (issue #431): the 123 cards that had no abilities were each read against the engine; 68 are wired (personality-wave-43/45/46/47, strategy-wave-43/44, item-wave-8,
    # spell-wave-16/17, follower-wave-20/21/22, holding-wave-6, celestial-wave-4, region-wave-5) and these 55 are not, for the gap each names. Where a note says a half is buildable,
    # the card stays unwired because the other half is not: a card is never wired with a printed ability missing.
    "oracle-l5r-6": "Needs a Focus Effect that substitutes one of your Yojimbo Personalities for your Personality in the duel (the duel's participants are fixed when it is issued), and Revenge tokens (+1F tokens of a named kind, three at once) given after another player's action destroys one of your Courtiers or Shugenja.",
    # Action-effect tracking (issue #437): MatchState.LastResolvedAction (ActionEffectTracking.cs) now records which cards an action destroyed
    # or moved (not a bow: no single choke point narrates every one the way DestroyUnits/MoveHome already do), read by a new "after an action
    # resolved" Reaction window. Measure of Devotion and Revenge, both blocked on exactly this, are now wired; Hida Defenders and Akodo Kobi's
    # own Reaction are each narrower now but not fully unblocked -- see their own updated notes below.
    "oracle-l5r-340": "A copy: the created-copy primitive exists (CreatedCardSpec.CopyOfTarget, Mendon's ruling that a copy is a created copy of the target card), but this card copies all the abilities except those that copy abilities, and the created Personality is removed \"when your next turn begins\" (created cards leave with the battle or never; Events expire on ExpiresAtTurnsTaken). The Artisan performer bowed as its cost and the non-Unique target are ordinary categories.",
    "oracle-l5r-599": "Ambiguous, and needs a cost that depends on the phase: \"bow him unless this is the Attack Phase\" is an exemption from the cost by phase (CostExempt knows a keyword, not a phase), together with \"lose 3 Honor\" and a target with lower Force than the performer. The reading of \"to bow a target Personality\" is Mendon's to confirm.",
    "oracle-l5r-967": "Needs an additional Battle action scoped by ability classification (\"a Limited or Open Recon ability\" used as a Battle action) that must target the current battlefield's Province, with a draw after its resolution: AdditionalActionGrant is scoped by card ids, not classification.",
    "oracle-l5r-1164": "Needs \"+1F and the ability 'Battle: Ranged 4 Attack' while in an army with one or more of the others\": a bonus and a granted ability that last while a condition on the army holds, for two or three performing Samurai at once (a conditional duration re-judged live, as Kaiu Nakano's).",
    "oracle-l5r-1747": "Needs a start-of-turn trait for a card in play: after the Provinces are revealed and their triggered traits have resolved, its controller may remove a Dark Virtue in their hand or discard pile from the game, and if none is removed Miki is destroyed. The Events Phase reveal (ContinueEventsReveal) resolves the Events and Regions in Provinces and the ongoing Events only; a card in play has no such trigger, and the optional-choice-else-destroy needs a decision in the middle of that reveal, as Harsh Choices does.",
    "oracle-l5r-1783": "Needs the removal \"when the turn ends\" of a card brought into play: bringing a dead Personality out of any player's dead pile under the acting player's control exists (BringDeadPersonalityIntoPlay, Goju Plot; TakeControl and OriginalOwnerId for a card taken from an enemy pile), but nothing removes a card from the game at the end of the turn (created cards leave with the battle, CreatedUntilBattleEndsIds; Events expire on ExpiresAtTurnsTaken). Also ignores the Honor Requirement, and \"After Yajinden enters play: Lose 4 Honor\" is EntryHonorLoss.",
    "oracle-l5r-1983": "Needs a Kata that gives its controller a Reaction: \"Before you gain Honor during your turn, bow your target performing Courtier to negate the gain and choose a player who loses 2 Honor\". Kata exist (KataCards.cs) and a granted player ability does not; no Reaction fires before the acting player gains Honor (only the opponent-payment fixture negates an Honor gain).",
    "oracle-l5r-2035": "Needs an opponent-chooses-or-random discard: \"the enemy leader discards a card of his choice, or a random card if you have Reconnaissance\". The choice is a decider decision (DeciderChoice, Tarao); a random card from a hidden hand is not: nothing draws a random hidden card during a game.",
    "oracle-l5r-2236": "Needs \"Negate his next straightening this game\": a one-shot negation that persists until it is used. The negations that exist last the turn (StraightenBlockedTurn) or hold every attempt until a named moment (StraightenHolds). Its static half (\"While this Shugenja is Earth, your provinces have +1 strength\") would be one more StaticProvinceBonuses entry.",
    "oracle-l5r-2331": "Needs an additional cost of entering play: \"either remove a card in your hand from the game or destroy one of your Sake tokens\" is paid as part of the Recruit, with a choice between two costs, and the entry path (QuotePurchase, Purchase) knows Gold, Honor Requirements and discounts only. Sake tokens exist (Togashi Sakata).",
    "oracle-l5r-2363": "Needs a Reaction \"after the resolution of an action that moved a Personality to the current battlefield\" that names the Personality moved: the after-move window (OpenMoveReactionWindow) serves Legion of the Bat alone. Its static half (\"While this Shugenja is Water, other players' provinces have -1 strength\") would be one more StaticProvinceBonuses entry.",
    "oracle-l5r-2580": "Needs \"negate the effects of the next action performed by the targeted card (this turn)\": a negation of a FUTURE action, waiting on the card until it acts. The negations that exist apply to the action being held in a reaction window.",
    "oracle-l5r-2652": "Needs a Reaction \"before an Event resolves: negate its effects\": an Event resolves when it is revealed (ResolveEvent) with no window before it. Its Battle half (move him home, straighten his unit if he moved) is buildable.",
    "oracle-l5r-2738": "Needs \"bow your two performing Nonhumans\" as a cost: Slots with Performing exist, but a cost cannot name the cards of a performing slot (CostSubject knows the single performer), and \"if your current army has more units than the current enemy army\" is a comparison of unit counts.",
    "oracle-l5r-3010": "Needs to know that a card \"was not in play when this Combat Segment began\": nothing records when a card entered play (the same gap as oracle-l5r-10335).",
    "oracle-l5r-3083": "Needs a Reaction fired \"after the resolution of your action that bowed, destroyed, or moved a Personality who has opposed this card at the current battlefield\": the destroyed/moved half is now recorded (MatchState.LastResolvedAction, issue #437), but a bow is not -- unlike DestroyUnits/MoveHome, no single choke point narrates every one of the hundred-plus call sites that set CardInstance.Bowed directly, so the printed \"bowed, destroyed, or moved\" cannot be checked in full yet.",
    "oracle-l5r-3093": "Needs a before-action Reaction that is not tied to a target (\"before the resolution of another player's action, if Fujita is at the current battlefield\") that negates the destruction of every Follower of yours the action would destroy. NegatedEffects entries are per known card, and DestroyUnits discards a destroyed unit's attachments unconditionally, so whether a Follower lost with its destroyed host counts as \"destroyed from the action's effects\", and what negating it would leave attached to a dead host, is unsettled: a reading for Mendon.",
    "oracle-l5r-3242": "Needs the Province a card came from: \"After Yamadera enters play from a province: Refill it face-up\". A purchased card leaves its Province and the Province is refilled face-down at once (Refill), and nothing records where a card in play entered from. Her Battle action (an enemy card with lower Force: bow it) is buildable on its own.",
    "oracle-l5r-3514": "\"You must declare an attack each turn and assign this Personality to attack if possible\" is an obligation the rules have no place for: nothing forces an attack declaration or an assignment. Its Reaction (after an action targets this Personality: negate his movement) would be buildable like Kota's.",
    "oracle-l5r-3595": "Needs a Reaction fired when you announce a Tactical action (not after it resolves), even if Ayumu is bowed: reactions to an own action exist only after resolution (AfterOwnActionTraits, OpenBattleActionReactionWindow) or to another player's action (before-action, targeted-by-action).",
    "oracle-l5r-3639": "Needs two independent targets for an attach: a Follower in your hand and a Personality of yours to receive it. AttachSpec takes its wielder from the source or the chosen performer (AttachWielder) and its card from a single target; Slots (issue #336) give two targets but an ability cannot both carry Slots and read the attached card from one of them. The hand pile itself is built (AttachPile.OwnHand: Ebara, Hailung).",
    "oracle-l5r-3813": "Needs a Reaction to a Battle action targeting your Paragon that ignores the action's costs and negates its effects retroactively, and gives \"that player\" an additional Battle action: costs already paid are not unpaid, and an additional action is granted to the acting player only.",
    "oracle-l5r-3891": "Needs a Reaction to an action targeting one of your OTHER Personalities at Sakonoko's location, with the cost \"bow Sakonoko or one of his Spells\". The targeted-by-action family fits the trigger (Kota, Taishuu, Rokai), but the reaction window lists one option per reacting card and target: it carries no choice of which card pays the cost (a Performer or CostOption choice is offered for actions, not inside a reaction window).",
    "oracle-l5r-4127": "Needs a Force bonus that lasts \"while he is in an army and while all Personalities in that army are Siege\": a conditional duration re-judged from the army's composition. Force bonuses from actions are fixed amounts for the turn, and static conditional ones are Effect-keyed to their own card.",
    "oracle-l5r-4206": "Needs \"Your Stronghold's abilities have Tireless\": Tireless (usable while bowed) is a hard-coded list of ability ids (Tsudoken, Dairuko, Red Crane Dojo), so it cannot be granted to another card's abilities. Its Favor half (discard the Imperial Favor: give each of your Provinces +2 strength) is a Favor ability like Shunya's.",
    "oracle-l5r-5106": "Needs Additional-Action grants scoped by required ability classification (Iaijutsu) and by a specific performer rather than by source card id (AdditionalActionGrant.ScopeCardIds cannot express the first), and \"If you don't, bow him\": a consequence when the additional action is not taken (a burst that is passed or forfeited).",
    "oracle-l5r-5534": "Needs Equip to accept a target in the discard pile: \"until your turn three turns from now begins, you may target Followers in your discard pile with the Equip rulebook action\" changes the rulebook action for a period; ongoing Events expire (ExpiresAtTurnsTaken) but do not change Equip.",
    "oracle-l5r-5605": "Needs a Reaction fired when an attacking player announces a Battle action before the Defender's first opportunity to act or pass, and, for its Open half, \"after the next time (this turn) you lose Honor from another player's card effect\" give three +1F Revenge tokens: a delayed trigger on a future Honor loss.",
    "oracle-l5r-5706": "Needs a tracked \"has the enemy leader taken a Battle action during this battle\" (novel state) and additional Battle actions granted \"after this one\" with \"you will not take any other additional Battle actions this battle\".",
    "oracle-l5r-6142": "Two static rules with no representation: \"Will only enter play from a province\" (an entry restriction by where the card comes from: Equip, Recruit and created entries are separate paths and nothing records the origin) and \"may not perform Battle actions while your current army has higher total Force than the current enemy army\" (a live army-Force comparison as a restriction on actions). \"After Raido enters play: Lose 6 Honor\" is EntryHonorLoss.",
    "oracle-l5r-6490": "Needs \"until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor\": the only such watch is Tsujiken's on a bow (TsujikenWatches, hard-wired), and it is not a general \"after an ability on it resolves\" trigger.",
    "oracle-l5r-6529": "Needs an alternate Gold source that does not bow: \"Reaction: When paying a Gold Cost, produce 1 Gold (this can be used together with bowing this card to produce Gold)\" while its printed \":bow: Produce 2 Gold\" is an ordinary production (AlternateGoldSource bows the card).",
    "oracle-l5r-6559": "Needs the first sentence, \"You may attach this card from your discard pile to your Void Shugenja\": a standing permission on the Equip rulebook action (Equip from a discard pile) rather than an action of its own, restricted to a Void Shugenja. The Open half (bow this card: give another player's Personality Kolat, Ninja or Shadowlands) is Izuna's three abilities sharing one use.",
    "oracle-l5r-6766": "Needs an ability usable from the dead pile (\"Even if Jouta is dead\": FromDiscard covers a card in a discard pile, not a Personality in Zone.Dead) whose Ranged Attack is made by a chosen Shugenja who \"may be at any location unless Jouta is dead\": a location rule that depends on the source's own state.",
    "oracle-l5r-7177": "Needs \"you have the first opportunity to take a Battle action in this battle, which must be performed by Toson\": a priority override for the first Battle action after an engage, with a forced performer for that one action. The Reaction after engaging exists (Detained), and so does discarding a card of the Province, but neither gives the opportunity or restricts who takes it.",
    "oracle-l5r-7197": "Half of it exists: negating current Force penalties is NegateForcePenalties (Tireless Efforts). Needs \"if he has a Weapon, negate the next card effect that moves him home or bows him\": a one-shot protection against a kind of effect that persists until used (Protections are all-or-nothing for the turn).",
    "oracle-l5r-7427": "Buildable as a Spirit (GrantSpirit) except its granted ability, \"Ninja Battle: Target two Personalities controlled by the same player at different locations: Switch their locations\": nothing swaps the locations of two Personalities.",
    "oracle-l5r-7494": "Needs a Holding that copies another player's Holding: \"copies one of its printed abilities that does not itself copy abilities, one of its Gold-producing traits, or its Gold Production stat\". Permanent copying is built for Personalities and Spells (PermanentCopy.cs), not Holdings, and a Holding's stat is not copyable.",
    "oracle-l5r-7588": "Needs \"an additional action appropriate to this phase\": an additional action is only ever a Battle action, and this is also an Open one in the Action Phase. Straightening all your Rings and taking a Ring from the discard pile are ordinary effects.",
    "oracle-l5r-7890": "Needs two performing Personalities with different Clan alignments and a cost of bowing ONE of them (a chosen one of two performers): Slots with Performing exist, but a cost cannot name a chosen card of a performing slot. \"Give your provinces +2 strength\" is buildable (Genji).",
    "oracle-l5r-7954": "Needs \"after the resolution of any Battle action, give each of your Nonhumans who performed it a +1F Frenzy token\" for two turns: the performers of a resolved Battle action are not recorded, and the token is a new kind.",
    "oracle-l5r-8268": "Needs the opposing player's offer to \"show you his hand\" (hidden information the acting player may ask for), a discard from it chosen by the acting player, and a lock on the rulebook seppuku action for that Personality.",
    "oracle-l5r-8371": "Needs \"You may destroy this card; if you do, discard the card you looked at\": a look (LookSpec, \"any Fate deck\") whose discard is conditional on destroying the Spell itself, a cost tied to a disposition. Its static half (\"While this Shugenja is Void, your maximum hand size is increased by 1\") would be an Effect read by MaxHandSize, as Taishuu's is.",
    "oracle-l5r-8633": "Needs two effects: destroying a Region of your choice when it is revealed (a decision in the reveal) and \"negate all movement to and from battlefields\" until your next turn begins (a movement lock on every card, present and future, rather than on chosen cards: MovementLocks are per card).",
    "oracle-l5r-8772": "\"Choose your performing Ninja, and destroy this card or discard it from your hand: Ranged 3 Attack\" is an Item used from a hand: ValidateAbility's source-zone rule lets only a Strategy or a Ring be the source of an action from a hand (an Item is in play or attached). \"Will only attach to a Ninja\" exists (AttachRequiresKeyword).",
    "oracle-l5r-9100": "Needs a Reaction to \"a Personality assigns or moves, if he is now opposing Tairu\": there is no trigger on assignment or on movement of any Personality (only the after-move window Legion of the Bat uses, for enemy Infantry moved to its own battlefield).",
    "oracle-l5r-9158": "Needs an \"after this card bows\" trigger that straightens it (a trait, not a Reaction): the after-bow windows are for Personalities (Hizumi, Ruya) and for a sibling Follower (Nikutai). The second ability (negate this card's destruction from an action that targets a card in this unit) is expressible with the targeted-by-action trigger and NegateEffects(card, \"destroy\").",
    "oracle-l5r-9623": "Needs control of another player's Personality who is IN PLAY: TakeControl exists for attachments and for cards taken from a discard or dead pile, not for a Personality in play. Also needs giving him back \"after the next time this game Sachina leaves play\" (a delayed effect tied to a card leaving play). The cost (his unit's total Gold Cost plus four) is TargetUnitTotal, and once per game is OncePerGame.",
    "oracle-l5r-9630": "Needs an alternate Gold source that does not bow: \"Reaction: When paying an action's Gold cost: Produce 3 Gold\" (AlternateGoldSource and ConditionalProduction both bow the card when chosen as a source, and Singh's Reaction has no bow cost), limited to actions. His Battle half (pay Gold equal to an enemy card's Gold Cost: destroy it, the card without attachments) is TargetCurrent.",
    # Seeds of Decay pass (issue #453), batch 1: strategy-wave-47 (twenty Strategies), stronghold-wave-12 (The Castle of Order) and celestial-wave-5 (Megumi's Guidance)
    # published Ceaseless Vigil (10139), Ruthless Onslaught (10164), Metsubushi (10158), Shifting Terrain (10168), Exchange of Letters (10147), Martial Manipulation (10157),
    # Defensive Blockade (10142), Shield Formation (10167), Embracing Battle (10146), Military Promotion (10159), Unexpected Obstacle (10177), The Arrow's Eye (10172),
    # Vengeful Dismissal (10180), Sticks and Stones (10170), Two Stings (10176), Wide Valley (10182), Grappling the Snake (10150), Sound the Retreat (10169), Requesting
    # Reinforcements (10163), Advanced Intelligence (10135), The Castle of Order (10185) and Megumi's Guidance (10028); the old notes on 10150, 10164, 10176 and 10182 are gone.
    # What they built: the standing redirect (StandingRedirects.cs: a watch that rewrites another player's next matching action as it is announced), a destruction shield that
    # outlives an action (CardInstance.DestructionShieldTurn), a per-battle Force non-contribution stamp, assignment while bowed, a keyword-keyed Stronghold Honor-loss immunity
    # and Wide Valley's trait in DestroyUnits/DestroyAttachment. The three below stay unwired:
    "oracle-l5r-10155": "Learning. Three gaps: (a) a Battle Strategy in ANOTHER player's discard pile played by its non-owner (the FromDiscard additional-action grant, AbilitySourceZoneAllows and AbilityIdsOf all read the source's own controller; nothing lets a player play an opponent's card from the opponent's pile, then remove it from the game); (b) \"its abilities may be used an additional time this turn\" -- a per-card raise of the per-turn use limit (AbilityDefinition.UsesPerTurn is data on the ability, and the once-per-turn stamp lives on the card; no per-instance override exists); (c) the keyword copy (\"copies a keyword on an enemy Personality\") is CardCopy.cs's copyKeyword step and IS buildable. The performer half is ordinary.",
    "oracle-l5r-10173": "Turning Away. Its Battle half is buildable now (Absent, a Monk performer at any location -- own-unbowed-personality-with-keyword-at-any-location -- bowing a target Ring as the cost, and Ring of Earth's +3/-3 province-strength choice on the current battlefield's province), but its Focus Effect, \"You may discard your Personality in this duel (ending the duel without resolution)\", needs a duel that ends WITHOUT resolving: in this engine the Focus Effects of a duel are applied after ResolveDuel has already computed the winner and applied the outcome (Dueling.cs), so there is no point at which a Focus Effect can discard a duelist and stop the resolution. Left unwired until a pre-resolution Focus Effect stage exists.",
    "oracle-l5r-10184": "Aramasu's Legacy. Needs an additional Attack Phase created by a Limited action (the turn's phase sequence is fixed: Straighten, Events, Action, Attack, Dynasty, End; nothing inserts a second Attack Phase), during which battle resolution destroys no defending armies or provinces and bows none of the acting player's Mantis Clan Magistrates or attachments (resolution-time exemptions scoped to one phase instance), then \"if you won any of its battles (even against no units at resolution)\" a bow of a Personality or Holding whose straightening is negated until after the next Straighten Phase ends (the scan; the catalogue says \"until after your next turn begins\" -- StraightenHolds can express that half).",
    # Seeds of Decay pass (issue #453), batch 2: the four cards of the batch that stay unwired. The batch's own primitives (SeedsOfDecayBatch2.cs: timed province
    # strength, assignment/destruction watches, action allowances, the before-Proclaim window and Proclaim locks, permanent keyword removal, the Honor-loss record on
    # a resolved action) do not reach these; each note names the exact gap and the half that IS buildable today.
    "oracle-l5r-10137": "Needs control of another player's Personality who is IN PLAY (\"Take control of him\"), deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md: TakeControl exists for attachments and for cards taken from a discard or dead pile only), and \"give control of him to his owner before the turn ends\" (a timed return of control). Buildable now: the target (\"enemy Personality whose unit's total Gold cost is 10 or lower\" is TargetUnitTotal's shape), the dishonor, and destroying his Followers whose Honor Requirement he then fails (CardPersonalHonor against Follower.HonorRequirement).",
    "oracle-l5r-10161": "Needs control of your own Personality given to another player while he stays in play (\"Give control of him to another player\"), deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md), and a granted TRAIT with its own Straighten-Phase decision (\"After a Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase\" -- a trait is not an ability, and GrantAbility grants abilities only; the straighten hold itself is StraightenHolds' StraightenPhaseEnds shape). The performer (\"your performing unbowed Personality with the base Ninja keyword\") is an ordinary keyword performer read from the printed keywords.",
    "oracle-l5r-10178": "Its Open half needs two things: where a Ring's last entry into play came from (\"a Ring which did not last enter play by its own text\" -- the engine puts a Ring into play only through its own recruit ability, so no provenance is recorded and today no Ring could ever qualify), and a permanent removal of a card's abilities (ActionLock lasts a turn or a phase; nothing blanks a card's printed abilities for the game). Buildable now: its Limited half (\"target one or more cards in your provinces: discard them\" is the provinces Target; \"refill the provinces face-up from the bottom of your deck\" needs only a bottom-of-deck variant of Refill, which is top-of-deck today).",
    "oracle-l5r-10094": "Needs a province GAINED (\"gain a province to the left of your leftmost province\": a player's Provinces list is fixed at setup and only ever destroyed, never extended -- Mining Apparatus' shape, not built) and its later destruction \"after the next time this game Suikihime leaves play\" (a leaves-play watch for the game). Buildable now: \"may not assign, and will not move, to battlefields\" (a standing MovementLock of Assign|ToBattlefield, today turn-scoped only), \"will not become dishonorable\" (a Dishonor() immunity keyed on the Effect), \"other players' Political actions may not target Suikihime\" (a TargetBar by action keyword and acting player) and the once-per-game, no-other-player-controls-The-Forgotten-Temple condition on the Reaction.",
    # Seeds of Decay pass (issue #453), batch 4: the seven cards of the batch still unwired (17 wired: personality-wave-58/60, holding-wave-7, region-wave-6, event-wave-5, follower-wave-26).
    "oracle-l5r-10067": "Needs a Reaction lockout: \"After Thunder Dragon performs an action: Other players may not take Reactions until it ends\" -- no primitive closes the other player's side of a reaction window (the before-action, attack, bow, move, after-battle-action and entry windows all offer either player their Reactions) for the duration of one pending action. The two statics ARE buildable now: \"Your Mantis Clan Personalities have +1F\" is one more controller-scoped static term of PersonalityForce (Zansho's zansho-follower-force shape, StaticKeywordRules.cs), and \"have the ability Battle: Target an enemy card without unbowed attachments: Bow it\" is a controller-scoped static ability grant read where AbilityIdsOf joins granted abilities (the category is enemy-card-without-unbowed-attachments, already built).",
    "oracle-l5r-10086": "Needs three things: (1) a Battle action taken by a DEAD card from the dead pile (\"If Yutaka is dead ... Bring him into play at the current battlefield\"): AbilityDefinition.FromDiscard admits a source in Zone.FateDiscard/DynastyDiscard only, never Zone.Dead, and no recruit cost basis prices the SOURCE's own recruit (GoldCostRule.TargetRecruit prices a target); (2) \"paying 3 less Gold if you have taken a Dark Virtue action this phase\": no per-player record of the action keywords taken this phase (AfterOwnActionTraits tracks per-card stat bonuses, not a phase flag); (3) \"Yutaka may not perform Bushido Virtue actions\" is a performer-side action-keyword ban -- a small check in RequireCanPerform (ActionLock.cs) on ActionCarriesKeyword(ability, source, \"Bushido Virtue\") keyed by his Effect, not built because the card cannot be wired whole. \"If he would be opposed\" is Army(1 - player, current battlefield).Any(). His entry Honor loss is EntryHonorLoss 1.",
    "oracle-l5r-10087": "Needs giving control of an in-play Personality to another player (\"After Asuka enters play: You may give control of her to another player\"): control transfer of a Personality in play is deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md; TakeControl covers attachments and cards brought from a discard or dead pile). The other two halves are buildable: \"may not assign, and will not move, into a defending army\" is an assignment/movement restriction keyed on her Effect (a check in AssignDefense plus a MovementLock kind for moves to a defending side), and \"After a Straighten Phase begins: Choose your bowed non-Spider Clan Personality. Negate his straightening this phase\" is a StraightenHold (StraightenPoint.StraightenPhaseEnds, the Unseasonable Weather shape already listed in StraightenHolds.cs) on a Personality her controller chooses at the Straighten phase -- a decision at that phase, which no card opens yet.",
    "oracle-l5r-10029": "Needs a permanent per-province mark (\"choose a Province. Permanently give it -2 strength ... Until the game ends, after a Region attaches to it, destroy the Region, and after a Holding enters play from it, give the Holding a -1GP token\"): a province-level state that survives the Event (ProvinceState has no such record), a choice of province at reveal (Harsh Choices' PendingChoice shape, now also used by Tested Land and Offering Reverence), a hook in AttachRegionToProvince and one where a Holding leaves a province to enter play, and a Gold-production token on a Holding (CardToken carries Force/Chi/Honor stats; GoldProduced reads no tokens). The immediate halves (-2 strength: province.Strength -= 2; destroy the attached Region: DestroyRegion) are ordinary.",
    "oracle-l5r-10030": "Needs \"the rulebook Cavalry player ability\", which the rules document does not define: under Emperor Edition Cavalry is the Cavalry Maneuvers segment (a unit assignment, not a player ability that targets Personalities). Until the rules lane names the ability meant (the Ivory Edition Cavalry player ability, presumably), \"Personalities may not be targeted by\" it cannot be given a reading. The duration, \"until your second turn from now begins\", is the existing ExpiresAtTurnsTaken shape (Times of Strife).",
    "oracle-l5r-10032": "Needs a PLAYER ability granted for a bounded time (\"until your second turn from now begins, you have the ability, Limited: Put a target Ring in your hand into play. While it remains in play, it does not count towards an Enlightenment Victory\"): TimingGrants.cs grants a player a timing, not an ability, and no per-player granted-ability list exists; plus a per-Ring flag that excludes it from the Enlightenment check (the check reads only the Fudo keyword) and a way to put a Ring from hand into play outside its own printed action. The duration itself is the ExpiresAtTurnsTaken shape.",
    "oracle-l5r-10120": "Needs \"Gain a province to the left of your leftmost province\": provinces are a fixed list of four per player (ProvinceState.Index 0..3 is read everywhere as a position: battlefields, assignments, the view); adding a province at index -1 or reindexing is not built. The cost is buildable: \"Remove this card and any number of Holdings you control with total base Gold Cost of 10 or higher from the game\" is a 0..N slot of own Holdings with a total printed-cost condition on the selection (a slot-level aggregate condition, a small extension of TargetSlots) plus RemoveSource.",
    # Seeds of Decay pass (issue #453), batch 5: the two cards of the batch left unwired (all nineteen others are wired in item-wave-11, spell-wave-18 and follower-wave-25).
    "oracle-l5r-10112": "Needs a search of the discard pile FIRST and then the deck for a Ring (\"search your discard pile, then deck, for a Ring. Show it. Put it into play or your hand\"): DeckSearch searches one deck per option (Dynasty or Fate), never a discard pile, and has no put-into-play effect for a Ring (only ToHand/ToHandThenDiscard/AttachRegion), the same gap as oracle-l5r-10680. Also needs a per-instance \"does not count towards an Enlightenment Victory\" mark on the Ring put into play (the victory check reads only the Fudo keyword). Its other three lines are buildable now: \"Will only attach to a Dragon Clan Personality\" (AttachRequiresKeyword), \"+1F for each Dragon Clan Personality you control\" (celestial-sword-force-mantis/-scorpion's shape) and the entry Honor gain (celestial-sword-mantis-honor's shape).",
    # Seeds of Decay pass (issue #453), batch 3: two Personalities left unwired, each on one precise gap.
    "oracle-l5r-10100": "Needs the per-Tactician cap the Glossary states (\"each individual Tactician may perform at most one Tactical action per turn\"), which the engine does not enforce (Program.cs's own Tactical Advantage test lets one Tactician perform it twice in a turn), so \"Taigo may perform up to three Tactical actions per turn\" has no cap to raise. His Tactical Battle action (bow a target enemy Personality; if he is Infantry, you may target and destroy a card without attachments in his unit) is buildable: Kaiji/Iaimiko's optional collateral slot with a not-Cavalry condition, ActionKeywords Tactical.",
    "oracle-l5r-10076": "Needs \"You may take an additional action\" for the Open half: an Additional Action grant is only ever consumed by a Battle action (ContinueUseAbility), so in the Action Phase nothing can take the granted action (the gap oracle-l5r-7588 names). The standing watch (\"after the next time this turn you announce an action on one of Momoko's Spells, ignore the action's bowing costs\" -- the scan prints \"non-destruction costs\"; the catalogue text carries the October 5th erratum) is buildable as a per-card turn stamp read by CostExempt (AbilityCosts.cs) for a Bow cost whose source is a Spell attached to her, and the Battle half's additional action is the ordinary grant.",
    # Embers of War pass (issue #460), batch 2: 12 of the batch's 25 Personalities are wired in personality-wave-63; these 13 stay blocked.
    "oracle-l5r-4167": "Kakita Gonji: \"Before you focus: Once per duel, instead of focusing that time, you may give your Crane Clan Personality in the duel +4 to his duel stat until the duel ends\" needs a REPLACEMENT for the Focus decision itself, inside the duel's own Focusing stage (Dueling.cs's ResolveDuelCommand only ever accepts \"focus\" or \"strike\" from the turn player; there is no alternative-to-focusing effect a card can offer there, unlike a Focus EFFECT, which applies only after the duel already ends). His Sensei-entry trait (discard-other-sensei) is trivial and could ship alone, but this pass follows the established convention of wiring a card whole or not at all.",
    "oracle-l5r-4179": "Kakita Kae: \"After a Battle action targets Kae, you may discard the Imperial Favor to negate Kae's destruction, bowing, and movement from the action's effects\" needs a targeted-action Reaction whose COST is discarding the Imperial Favor specifically (every existing negation Reaction with a Favor-adjacent cost -- Yoritomo Sakuma's own EitherOr -- pays Gold, never the Favor itself) that negates a bundle of THREE effect kinds (destroy/bow/move) on the reacting card alone, the same three-kind bundle Jinn-Kuen's own blanket negation already proves buildable, but keyed to a brand new cost verb the ActionCost/CostVerbs enums do not carry (Favor discard is currently only ever a printed ability COST via ActionNegation.cs's own hardcoded path, not offered as a Reaction's own payable option). Her plain \"Battle: Move Kae home\" is trivial in isolation.",
    "oracle-l5r-4189": "Kakita Kenta: \"Iaijutsu Reaction: After an action resolves during which Kenta won a duel, target a dishonorable enemy Personality at the current battlefield: Move him home\" is the first own-won-duel Reaction with a real Target. DuelPresentation.cs's QueryDuelActions -- the SEPARATE listing path every duelWonReaction/duelReaction window uses instead of the ordinary QueryActions/AddReaction machinery -- is hardcoded to untargeted commands only (\"All currently supported duel reactions are untargeted\", its own comment; every existing own-won-duel/own-personality-won-duel card, Daisuke/Horume/Jakuei/Maratai, prints Target \"none\"). Extending that listing to enumerate legal targets for a targeted duel-won Reaction is the one piece needed; not attempted here given how heavily the live duel system is already tested.",
    "oracle-l5r-4462": "Kitsune Haruki: \"Reaction: After Haruki enters play: Create a 2F/2C/3PH Mantis Clan · Nonhuman · Deer · Spirit Personality with the ability, 'Limited: If another player has higher Family Honor than you: Gain 2 Honor'\" needs its own token PUBLISHED as a CreatedOnly scan first -- CreatedCards.Resolve only ever matches against CardDefinition.CreatedOnly cards already in the catalogue (Kitsu Akai's own Lion Ancestor, oracle-l5r-9785, is the precedent), and no such Deer token exists yet. Its own real printed proxy card, \"Haruki's Deer\" (oracle-l5r-9999, same set, type Proxy, text verified to match verbatim), has a receipt but no downloaded scan; publishing it as a CreatedOnly Personality (overriding its oracle \"Proxy\" type field, which the CardType enum does not carry) is a small follow-up, not attempted in this batch to avoid mis-publishing a new card under time pressure.",
    "oracle-l5r-4806": "Makito - experienced: \"Maho Battle: Target an enemy Personality with equal or lower Chi: Destroy a card without attachments in his unit; this destruction will not be negated or delayed\" needs a per-EFFECT (not per-action) unconditional destruction-cannot-be-negated-or-delayed flag. Protection (issue #212, ActionNegation.cs) is the closest primitive, but it protects an ACTION performed by a Protected card from negation entirely -- there is no way yet to mark one single destruction effect, on a card the acting player does not control and is not itself Protected, as unnegatable while the rest of the same ability could still be negated. \"+1F while a Phoenix Clan player controls him\" and \"+1F after a card is destroyed\" are both buildable now (the Effect-keyed PersonalityForce term Rokku's own rokku-lion-force-bonus proves, and AfterOwnActionTraits' existing shape), but the card is not wired whole while the Maho Battle clause is missing.",
    "oracle-l5r-5361": "Moto Rani - experienced: \"Water Reaction: When paying for a Spell Equipping to Rani, remove a dead Personality in your discard pile from the game to reduce the Spell's Gold Cost by the Personality's Chi\" needs a brand new hook into the Equip Gold-cost pipeline (EquipGoldCost/QuoteEquipGold, GoldQuotes.cs) that lets a Reaction played AT PAYMENT TIME apply a discount sized by a card removed from the game as its own cost -- every existing Equip discount (Katagi's wielder-dependent term, ClanEntryDiscount) is a fixed or static-condition term computed automatically, never a player-chosen action taken mid-quote. Her own \"Water Tireless Battle: Straighten Rani. You may move her home\" is trivial in isolation (Tireless/Absent plus an optional MoveHome), but is not wired alone.",
    "oracle-l5r-6653": "Shadow Dragon - experienced 2: \"Battle: Target an enemy Personality: Permanently give him Ninja and Shadowlands. Take control of him. Return control of him to his owner after the next time this game this ability is announced\" needs control transfer of an in-play Personality, which is deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md is explicit that TakeControl covers only attachments and cards brought from a discard/dead pile, never a live Personality already in play) -- a large, deliberately deferred system, plus a delayed \"until this exact ability is next announced\" reversion watch that does not exist anywhere yet. \"Lose 5 Honor\" on entry, \"abilities will not be copied\" (a copy-immunity flag; no card currently copies an ABILITY rather than a keyword/stat) and the dead-self-recruit Open half are individually small, but the card's one defining ability is the control-transfer clause.",
    "oracle-l5r-6883": "Shinjo Hayan: \"After engaging: Once per turn, you may assign your (unbowed) Unicorn Clan Personality in your home to the current battlefield\" needs a player-triggered ASSIGNMENT outside the normal Attack-phase Assign command: every existing Assign path (AssignAttackers/AssignDefense) runs once, at battle declaration, choosing every unit for every battlefield at once -- there is no primitive for a single Reaction-timed effect to assign one more Personality from home to an already-engaged battlefield afterward (MovesToBattle moves a card there directly, bypassing \"assignment\" as a rules concept entirely, which is not the same thing an Assign-based card like this one needs). His Sensei-entry trait is trivial in isolation but not wired alone.",
    "oracle-l5r-6934": "Shinjo Sarang: \"Battle: Choose your performing Personality, and target your Follower at any location: Transfer it to the Personality. Straighten the Follower if the Personality is opposed or Spider Clan\" needs a new own-follower-any-location Target category (every existing Follower target category location-restricts to the source's own unit, the current battlefield, or a specific named relationship -- Daigotsu Yamato's own-follower-at-any-location is the closest precedent but still requires CanTransferTo AND an AtAnyLocation exemption keyed to acting-as-Battle, both already narrower than \"any location, unconditionally\") plus a conditional straighten keyed on the CHOSEN PERFORMER's own state (opposed, or Spider Clan) rather than the target's, a combination not yet proven together.",
    "oracle-l5r-7110": "Shosuro Ietsuno: \"After a card from a deck refills Ietsuno's battlefield's province: Look at it. You may switch it with a card owned by the same player in another province\" needs a brand new province-refill hook -- DeckLook.cs's own Look primitive only ever looks at the top N cards of a deck as part of an ability's own effect, never as a standing watch that fires automatically whenever a SPECIFIC province (his own battlefield's) refills from a reveal, and \"switch it with a card owned by the same player in another province\" (a face-down province-to-province swap, same owner, either Dynasty or Fate) has no existing primitive at all. Her plain \"Battle: Target an enemy card without attachments: Bow it\" is trivial in isolation but not wired alone.",
    "oracle-l5r-8613": "Tokiko - experienced: \"Battle/Open: Destroy Tokiko and target your dead Personality: Bring him into play in your home or at the current battlefield, paying all costs but paying 7 less Gold\" needs a player-CHOSEN recruit destination: every existing dead-Personality recruit (Essence of Undeath's own-dead-personality precedent, \"at this Shugenja's location\"; At Any Cost, \"at the battlefield the action was played at\") recruits at a location fixed by the source's OWN position, never a home-or-battlefield choice independent of where the destroyed source itself was. GoldCostRule.TargetRecruit(modifier: -7, floor: 0) already prices the discount exactly; only the destination choice is missing.",
    "oracle-l5r-8878": "Tsuruchi Shichiro: \"After a Ranged Attack action resolves that your Mantis Clan Personality performed: Once per battle, you may move him home\" needs AfterActionReactionTriggers.AfterActionResolved narrowed to (a) specifically a RANGED Attack (every existing AfterActionResolved reader checks what was DESTROYED, never the acting ability's own AttackKind) and (b) the ACTOR's own Mantis Clan keyword rather than a fixed card -- a combination not yet proven, though each half has a partial precedent (AttackKind(facts.AbilityId) reads the kind; ActionCarriesKeyword-style clan checks exist for other triggers). His Sensei-entry trait is trivial in isolation but not wired alone.",
    "oracle-l5r-9083": "Utaku Lishan: \"While Lishan is attacking, she has +1F and +1PH\" is buildable now (PersonalityForce's own attacking-one Effect term, plus a matching StaticPersonalHonorBonus term). \"Reaction: After you announce a Battle action, if Lishan is at the current battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Lishan's Personal Honor\" needs the announce-reaction window (ResolveOrDeferAnnounceReaction, CardAbilities.cs) generalized past its own hardcoded Ranged-only gate (\"only ever offered when the about-to-execute ability is classified Ranged\", its own doc comment; Tsuruchi Yakusho is its one existing user) to ANY Battle action, which the same comment says a future card could do but nothing has yet -- not attempted here given the window's shared, heavily-tested role in every Ranged Attack. The card is not wired for its static bonus alone, following this pass's whole-card convention.",
}


def load_published_cards() -> dict[str, dict]:
    """Id -> the full published CardDefinition dict, for every card published into any
    content/published/*.json wave. Doubles as the wired-id set (its own keys) and as the source row
    data for a wired card that build_deck_pool.py's own EXCLUDED_TYPES={"Proxy"} filter correctly
    leaves out of deck-pool.json entirely (a created-only card, Proxy-typed in the raw oracle schema,
    like the Lion Ancestor -- it was never a deck-buildable reference card, but it IS a wired card
    this inventory must still show a row for)."""
    cards: dict[str, dict] = {}
    for path in sorted(PUBLISHED_DIR.glob("*.json")):
        if path.name.endswith("-sources.json"):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        for card in data.get("Cards") or []:
            cards[card["Id"]] = card
    return cards


def load_sets() -> dict[str, str]:
    """Id -> its own primary printing's Set name(s), read directly from
    content/oracle/emperor-catalogue.json (the same source build_deck_pool.py itself reads, but that
    script's own output schema carries no set/printing field to reuse here)."""
    data = json.loads(EMPEROR_CATALOGUE.read_text(encoding="utf-8"))
    sets: dict[str, str] = {}
    for card in data["cards"]:
        printings = card.get("printings") or []
        primary = next((p for p in printings if p.get("id") == card.get("current_primary_printing_id")), None) \
            or (printings[0] if printings else None)
        card_sets = (primary.get("sets") or []) if primary else []
        if card_sets:
            sets[card["id"]] = ", ".join(card_sets)
    return sets


# Hand-curated, same discipline as BLOCKERS: only for a card whose Set is not derivable from
# emperor-catalogue.json at all (a created-only, Proxy-typed card absent from that catalogue --
# verified directly against its own scan instead of inferred).
SET_OVERRIDE: dict[str, str] = {
    "oracle-l5r-9785": "Second City",  # Lion Ancestor (personality-wave-17, issue #257)
}


def build(root: Path = ROOT) -> dict:
    pool = json.loads(DECK_POOL.read_text(encoding="utf-8"))["cards"]
    published_cards = load_published_cards()
    wired_ids = set(published_cards)
    sets_by_id = load_sets()

    rows = []
    seen_ids: set[str] = set()
    for card in pool:
        types = card.get("types") or ["Unknown"]
        seen_ids.add(card["id"])
        rows.append((
            card["id"],
            card["name"],
            SET_OVERRIDE.get(card["id"]) or sets_by_id.get(card["id"], ""),
            types[0],
            ", ".join(card.get("clans") or []) or "Unaligned",
            "Wired" if card["id"] in wired_ids else "Unwired",
            BLOCKERS.get(card["id"], ""),
        ))
    # A wired card absent from deck-pool.json entirely -- see load_published_cards' own doc comment.
    for card_id, definition in published_cards.items():
        if card_id in seen_ids:
            continue
        rows.append((
            card_id,
            definition["Name"],
            SET_OVERRIDE.get(card_id, ""),
            definition["Type"],
            definition.get("Clan") or "Unaligned",
            "Wired",
            BLOCKERS.get(card_id, ""),
        ))
        seen_ids.add(card_id)
    rows.sort(key=lambda r: (TYPE_ORDER.index(r[3]) if r[3] in TYPE_ORDER else 99, r[1]))

    unmatched_wired = wired_ids - seen_ids
    if unmatched_wired:
        raise ValueError(f"Wired card ids not found in deck-pool.json or published defs: {sorted(unmatched_wired)}")

    wb = Workbook()

    summary = wb.active
    summary.title = "Summary"
    summary.append(["L5R Emperor -- Card Inventory"])
    summary["A1"].font = Font(bold=True, size=14)
    summary.append([f"Generated from content/deck-pool.json ({len(rows)} cards) and content/published/*.json"])
    summary.append([])
    summary.append(["Type", "Wired", "Total", "% Wired"])
    for cell in summary[4]:
        cell.font = Font(bold=True)
    by_type: dict[str, list[int, int]] = {}
    for _id, _name, _set, t, _clan, status, _blocker in rows:
        counts = by_type.setdefault(t, [0, 0])
        counts[1] += 1
        if status == "Wired":
            counts[0] += 1
    total_wired = sum(c[0] for c in by_type.values())
    total_all = sum(c[1] for c in by_type.values())
    for t in TYPE_ORDER:
        if t not in by_type:
            continue
        wired_n, total_n = by_type[t]
        summary.append([t, wired_n, total_n, f"{wired_n/total_n:.1%}"])
    summary.append(["TOTAL", total_wired, total_all, f"{total_wired/total_all:.1%}"])
    summary[summary.max_row][0].font = Font(bold=True)
    for col, width in zip("ABCD", (18, 10, 10, 10)):
        summary.column_dimensions[col].width = width

    sheet = wb.create_sheet("Cards")
    header = ["Id", "Name", "Set", "Type", "Clan", "Status", "Blocker"]
    sheet.append(header)
    header_fill = PatternFill(start_color="FFD9D9D9", end_color="FFD9D9D9", fill_type="solid")
    for cell in sheet[1]:
        cell.font = Font(bold=True)
        cell.fill = header_fill
    for row in rows:
        sheet.append(list(row))
    last_col = get_column_letter(len(header))
    table = Table(displayName="CardInventory", ref=f"A1:{last_col}{len(rows) + 1}")
    table.tableStyleInfo = TableStyleInfo(name="TableStyleMedium2", showRowStripes=True)
    sheet.add_table(table)
    widths = {"A": 16, "B": 34, "C": 18, "D": 14, "E": 16, "F": 10, "G": 70}
    for col, width in widths.items():
        sheet.column_dimensions[col].width = width
    sheet.freeze_panes = "A2"

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    return dict(cards=len(rows), wired=total_wired, output=str(OUTPUT.relative_to(root)))


if __name__ == "__main__":
    print(json.dumps(build()))
