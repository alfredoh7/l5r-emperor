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
    "oracle-l5r-10105": "Needs an \"after being targeted\" Reaction trigger, plus card creation and attachment transfer.",
    "oracle-l5r-9867": "Needs a delayed/scheduled effect across turn boundaries.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-1992": "The Follower Performer category now exists (own-unbowed-follower-in-source-unit, issue #337: Hiruma Tensin), but this card's performer is \"one of your Followers or Personalities in this army\" (any unit card of either type, bowed as the cost) and its target is \"a Follower or Personality in this army\": neither category is built.",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
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
    "oracle-l5r-10107": "The in-play \"after another player's action targets a card in this unit: negate the action's effects on this card\" Reaction exists (Champion of Thunder's champion-of-thunder-negate, own-unit-member target, the blanket \"all\" negation; issue #278) and so does the trigger family it now shares (issue #335, TargetedActionReactions.cs). Directly wirable as a second carrier of champion-of-thunder-negate; not yet wired.",
    "oracle-l5r-10337": "Needs a way for a TRAIT to negate the announced Political action's remaining effects, plus a Political-action classification (the flat +1PH half is separately wirable). The \"first time each phase\" half now exists: the once-per-phase limit (PhaseLimits.cs) and the announce-time targeting trait hook (Daigotsu Bofana, MatchEngine.OpenForcedDiscardForTargetedTrait).",
    "oracle-l5r-10519": "The Engage window and first-action claim now exist (issue #310; a Follower's 'Engage:' is a Reaction to trigger after-engaging, claim: MatchEngine.ClaimFirstOpportunity, conditional on being the Attacker). Still needs: a Follower-sourced Engage action scoped to 'this battlefield' (the Follower's own unit) and its optional 'you may target ... -3F' effect target; Reconnaissance for the acting side is settled (MatchEngine.GrantReconnaissanceAtCurrentBattlefield serves an Attacker at the enemy's province and a Defender at their own, see docs/decisions/2026-09-18-battle-timing-primitives.md decision 5).",
    "oracle-l5r-10587": "Needs a compound Gold-plus-card-discard cost shape (Kharmic keyword's own reminder).",
    "oracle-l5r-10108": "Needs an \"after this card bows\" Reaction trigger plus negating its own future straightening.",
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
    "oracle-l5r-10126": "Rings themselves are now built (issue #194); that alone does not unblock this card. Its own scan (\"Extend the Soul's Boundaries\") shows: enters play paying 1 less Gold per Ring controlled (a new variable, count-scaled discount -- every existing discount is either clan-alignment-based or a fixed printed amount, never scaled by a live count); Battle: destroy this card, bow the performing Shugenja, destroy him unless he is Void, target an enemy Personality whose controller may shuffle him and his attachments back into their owners' decks (needs a shuffle-into-deck primitive, not built); if he did not choose this, the acting player takes two additional Battle actions (a grant conditioned on the TARGET's own opponent choice, a novel shape). Three separate unbuilt pieces, not one.",
    "oracle-l5r-2419": "The general \"before this battle resolves\" window now exists (issue #310: a Terrain's trait is an AbilityCatalog entry of Timing BeforeResolution, applied once both players pass, ordered by the turn player). Still needs its own effect: TWO independent optional choices ('you may choose and straighten a Samurai ... and you may choose and bow a non-Samurai') -- the optional window (beforeBattleOptional) names one target today; the Terrain-play half is separately wirable.",
    "oracle-l5r-2613": "Needs an Absent-Battle-timed Terrain-play variant (the existing one is Battle-timed only) plus a province-strength passive.",
    "oracle-l5r-10150": "Its \"after the next time (this turn) an action targets him\" is a standing watch set up by the Battle action (not a printed in-play Reaction), then a choice between negating the action's effects on the Monk (NegateAllEffectsOn exists, issue #335) or bowing the unit of a performer after the action resolves; the additional-Battle-action half is separately wirable. The standing-watch shape and the either/or choice are the gaps.",
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
    "oracle-l5r-10164": "Needs an \"actions must target this card if legal\" forced-redirection rule (same gap as oracle-l5r-10300): a standing effect that rewrites every other player's Battle action for the rest of the battle. The legality half now exists (ActionAcceptsTarget, issue #335 -- \"if legal\" is that check); the standing hook that applies it to every action is the gap.",
    "oracle-l5r-6727": "Needs a non-AtBattle-gated Performer category (\"at any location\", same gap as oracle-l5r-5941).",
    "oracle-l5r-7435": "Needs an attachment Transfer mechanic (issue #435 reevaluation; same gap as oracle-l5r-2520/10900/2598/1151): its Focus Effect is \"you may transfer an attachment from your Personality in this duel to another of your Personalities.\" The Battle half (an enemy card without attachments, bow it, destroy it if this Personality is a Berserker) is separately wirable.",
    "oracle-l5r-10132": "Needs a delayed, multi-step revival mechanic (\"before this turn ends, put a removed unit into play ignoring costs\") -- not built.",
    "oracle-l5r-7995": "Needs a delayed duel-loss-consequence mechanism (issue #435 reevaluation): its Focus Effect is \"you may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins\" -- genuinely novel duel-resolution-timing plumbing (Strength of the Spider, oracle-l5r-8362, is blocked on the identical clause and deliberately deferred rather than invented for one card; see CardAbilities.cs). The Battle half (a Mantis Clan performer, move to the current battlefield, an additional Battle action) is separately wirable.",
    "oracle-l5r-10718": "Needs a self-replacing Terrain variant plus a delayed post-destruction straighten (two gaps, same self-replace gap as oracle-l5r-10941).",
    "oracle-l5r-8188": "Needs a new trigger keyed to passing instead of a Battle action (novel; the Terrain-play half is separately wirable).",
    "oracle-l5r-10958": "Needs a \"Political\" Reaction timing (not built; the plain Ranged 1 Attack half is separately wirable).",
    "oracle-l5r-8311": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900) plus a per-card override of \"a bowed card contributes no Force.\"",
    "oracle-l5r-10699": "The ability-copying primitive now exists (CardCopy.cs, issue #338: copy one ability that does not itself copy abilities, with Duration \"Turn\" for a copy \"this turn\"). What is still missing is this Spell's own target category (a Spell at this battlefield or a Thunder Spell at any location) and copying onto a Spell receiver during a Thunder Battle action.",
    "oracle-l5r-10960": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900).",
    "oracle-l5r-10176": "Needs a discard-pile-usable variant distinct from Discipline (its own printed ability, not a Discipline-keyword card).",
    "oracle-l5r-10963": "Needs an \"Interrupt\" timing (not built; the plain Melee 2 Attack half is separately wirable).",
    "oracle-l5r-10182": "Needs a \"before an action's effect destroys a card, negate\" Reaction trigger (same gap as oracle-l5r-2530; the Terrain-play half is separately wirable).",

    # Lion Clan Personality review (issue #198): the 49 unwired Lion Clan Personalities left after
    # wiring Personality wave 8's 17 cards, triaged against every primitive built through wave 8.
    "oracle-l5r-10059": "Needs a modifiable Personal Honor stat (current PersonalHonor is a fixed printed value with no StatModifier equivalent), a 'Dark Virtue' action classification, and an Honor-Victory-threshold modifier -- none built.",
    "oracle-l5r-10849": "Needs a modifier hook into the existing Tactical Advantage rulebook action's own Force-bonus amount (a generic action-amount-modifier mechanism doesn't exist yet).",
    "oracle-l5r-241": "Card creation is built (issue #387): the 2F Follower it creates is the Infantry Follower scan (oracle-l5r-9738), not yet published as a created-only Follower wave. Remaining: that wave and this card's Creates ability. 'Tactical Limited' itself maps onto the existing Limited Timing per the established Political/Tactical precedent, so is not itself a blocker.",
    "oracle-l5r-10241": "Needs a broad, repeatable 'before another player's action resolves' Reaction trigger (a before-resolution window keyed to enemy actions, not built).",
    "oracle-l5r-294": "Akodo Tamisu. Her second sentence -- 'After your Tactical, Bushido Virtue, or Terrain action resolves: Give a Lion Clan Personality +1F' -- is now classifiable: the after-action-resolved Reaction window (issue #437) plus ActionCarriesKeyword's own source-card-keyword fallback (issue #321) read Tactical/Bushido Virtue/Terrain the same way Matsu Rika's self-buff does (issue #449), a three-way ActionCarriesKeyword check away rather than a new trigger family. Still needs a 'choose a target Lion Clan Personality' shape for a Reaction whose printed wording is mandatory once triggered (Rika's own self-only case sidesteps this), and separately her entry effect 'discard all your other Sensei Personalities' (recurs on Ikoma Aimi's base printing) -- neither built yet.",
    "oracle-l5r-305": "The Phoenix-opposition Force bonus is directly wirable (reuses this wave's own clan-conditional attacking-vs-dragon-bonus pattern). Its Limited ability is now expressible: the look-at-the-top-N-cards primitive (issue #322, DeckLook.cs) covers 'look at the top Fate card; leave it, discard it, or put it at the bottom' as a LookSpec (N=1, own Fate, discard 0-1, bottom 0-1) -- unblocked, deliberately not wired in that PR.",
    "oracle-l5r-10850": "Needs a 'retrieve a specific card from your discard pile into hand' primitive (distinct from deck search; not built).",
    "oracle-l5r-10061": "Needs a 'discard all your other Sensei Personalities' entry effect (recurs on Akodo Tamisu) and a keyword-suppression mechanism (grant Courtier while stripping six named keywords from every Lion Clan Personality you control) -- keyword suppression not built.",
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
    "oracle-l5r-4386": "Card creation is built (issue #387) for a Personality created at home; still needs creation directly at the current battlefield (removed from the game when the battle ends: CreatedUntilBattleEndsIds is the precedent) and a 'Home Battle' Timing (performable from Home, unlike ordinary Battle actions which require AtBattle).",
    "oracle-l5r-10293": "The deck search primitive now exists (issue #312) and its criteria can express \"a non-Unique Lion Clan Shugenja\" from the Dynasty deck, but the effect is \"refill this province with him, face-up\" (a search effect the primitive does not have yet) and the ability must be usable from a face-up province card whose own cost is discarding her and paying 2 Gold.",
    "oracle-l5r-9826": "Card creation is built (issue #387) but creates one card per action: two created Lion Ancestors need a count on CreatedCardSpec; 'overlays' as an alternate entry-trigger condition (distinct from ordinary Purchase) is not yet verified as built.",
    "oracle-l5r-4396": "Needs a per-player 'controls an Ancestor' Honor-gain/loss sweep referencing each player's own starting Family Honor baseline (not tracked as a stat) plus an imposed choice against another player's own card state.",
    "oracle-l5r-10852": "Needs Interrupt timing (not started); card creation is built (issue #387, the Lion Ancestor scan is published).",
    "oracle-l5r-4397": "Card creation is built (issue #387); still needs a turn-spanning \"lost Honor from another player's action since your last turn began\" tracked condition, and a one-to-four selection whose MAXIMUM depends on that condition (the multi-target slots, issue #336, have a fixed Min..Max per slot; a condition-dependent Max is the remaining gap).",
    "oracle-l5r-4916": "Needs a unit-scoped 'bow every card in the target unit meeting a Force comparison' primitive (existing unit/army-scoped primitives only apply static Force modifiers, none perform a bow across a filtered set of cards); the per-card Honor gain has direct precedent (BattleHonor).",
    "oracle-l5r-4944": "Needs an activated (not passive/static) 'apply a Force penalty to every card in the target unit, then bow any now at 0' primitive -- the existing unit-scoped Force primitives (UnitForceBoostPrimitive, ArmyBattleMaidenBoostPrimitive) are passive/continuous, not one-shot activated effects across a filtered unit.",
    "oracle-l5r-10064": "Needs an army-wide activated Force bonus where the amount varies per target (each card's own Personal Honor) -- existing army/unit-boost primitives only grant a flat, identical bonus to every qualifying card.",
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
    "oracle-l5r-10080": "Its target-status half is built (TargetStatus.IsDishonorable, an IfTarget condition) but the printed \"+3 strength\" is a province-strength stat change the core rules end with the turn, while every province-strength change in the engine (Improved Defenses, Ring of Earth, Yogo Adi, Dueling) is permanent -- needs a timed province-strength modifier first.",
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
    "oracle-l5r-10035": "The lock (Card.ActionLocks) and every modifier (with IsStatPenalty classifying the current Force penalties) are data that can be removed now (issue #336); the negating action itself is not written.",
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
    "oracle-l5r-298": "Needs Bushido Virtue as a filterable Strategy classification, and the additional action to play it \"for one of its Battle, Limited, or Open actions, as appropriate to the current phase\": the general \"take an additional action to play a discard-pile card, then remove it from the game\" is built for Daigotsu Bofana - exp's Dark Virtue (AdditionalActionGrant.FromDiscard), but an additional action is only ever a Battle action. Its \"discard a card\" cost is the ordinary Discard cost.",
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
