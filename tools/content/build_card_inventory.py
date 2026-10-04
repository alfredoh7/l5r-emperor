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
    "oracle-l5r-10892": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142).",
    "oracle-l5r-10682": "Needs Home-timing generalization plus hypothetical-opposition movement.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
    # Honor and Treachery pass (issue #458): Bayushi Nitoshi - inexperienced (10248). "Battle/Limited: Look at the top card of your Fate deck. If it is
    # an attachment, you may attach it to one of your Personalities at Nitoshi's location, paying all costs. If it is a Strategy, you may take an
    # additional action to play it (if legal)." LookSpec (issue #whichever built the peek primitive) only supports the "look, then optionally discard/
    # keep/reorder" shapes; this needs a genuinely new "peek the top card, branch on its own type, and either attach it in place (paying its costs) or
    # grant an additional action to play it, leaving it on top if declined" primitive -- none of the three outcomes (conditional attach-in-place from a
    # peeked deck position, conditional additional-action grant keyed on the peeked card's type, "stays on top until then") exists today.
    "oracle-l5r-10248": "Bayushi Nitoshi - inexperienced (Honor and Treachery; the scan: \"Battle/Limited: Look at the top card of your Fate deck. If it is an attachment, you may attach it to one of your Personalities at Nitoshi's location, paying all costs. If it is a Strategy, you may take an additional action to play it (if legal).\"). Re-verified after issue #503: the look (DeckLook), the Battle/Limited split (two ids sharing one use, Kakita Tadanobu's shape) and a card-scoped additional-action grant exist, and the attach half is buildable as a mid-resolution attach with payment (Versatile Farm's ChoosePayment stage, AttachAction's RequireAttachable). Still missing: PLAYING A CARD FROM THE TOP OF THE FATE DECK. Every play path assumes the source Strategy is in the hand or (Discipline, Bofana/Tezuka grants) a discard pile: AbilitySourceZoneAllows' Strategy branch and QueryActions' source list (FateDeck cards are never candidates), the Terrain \"playStrategy\" command (hand only), SpendHandPlayedCard / the CompleteAbilityResolution and CompleteNegatedAction tails (they discard from the hand; a card left in PlayerState.FateDeck would be duplicated), PaymentSubjectFor (Play vs Action), and the grant would have to be RESTRICTED to that card (a Tag rule in RequireRestrictedBurstAllows / PerformerGrantMatches, which today ignore ScopeCardIds). The client view also has no way to show an action on a card that sits face-down in a deck. Suggested build: a FromDeckTop grant (AdditionalActionGrant flag) mirroring DiscardPlayGrant through those five places, the card leaving the deck list as the action is announced.",
    "oracle-l5r-10679": "Needs \"grant keyword to whole unit\" (the passive Attack-strength defense half is now built -- Destrier/Singh Remnants -- and directly reusable here).",
    # Dueling/Focus Effect reevaluation (issue #435): Dueling (#204) and Focus Effects (issue #419's FocusEffectChoice.cs) now exist, so the eight Before the Dawn cards once
    # blocked on "Dueling/Focus Effects (not started at all)" were re-read against the engine as it now stands. Three were fully wirable: Hiromi's Vassals and Shamsir share a
    # "you may attach this card to one of your Personalities, ignoring Gold cost" Focus Effect, generalized as FocusEffectChoice.cs's third choice kind; Home Guard's mandatory
    # +3F Focus Effect, +3F-while-defending static and Battle bow-and-Honor-gain each reuse an existing shape outright. No remaining blocker note for any of the three.
    # oracle-l5r-2520 (Field Messenger) and oracle-l5r-2598 (Flanking Unit): re-evaluated for the Embers of War pass (issue #460, batch 6) now that the attachment
    # Transfer mechanic this note asked for exists (AttachmentTransfers.cs, Honor and Treachery pass issue #458). Both are now wired.
    "oracle-l5r-10681": "Its ordinary Equip is allowed from YOUR discard pile (\"You may Equip this Follower from your discard pile\"). The attach-from-a-discard-pile primitive exists (AttachAction.cs, issue #337) for an ability that TARGETS a card in a pile, but here the SOURCE card in the pile is the attachment (AttachSource.Self is hand-only) and the action is the ordinary Limited Equip command, not an ability -- not built.",
    "oracle-l5r-2722": "Needs a compound multi-keyword attach restriction (AttachRequiresKeyword only holds one today).",
    "oracle-l5r-10336": "Needs card creation (its army-scoped keyword-boost half is otherwise wirable).",
    "oracle-l5r-10588": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    "oracle-l5r-10683": "Needs a per-card, permanent \"this Personality may not attack\" attachment flag (Utaku Chikako's own restriction, issue #186, is a player-and-turn-scoped flag, not directly reusable here) plus a self-destroying Limited ability. Dishonor-as-an-action is now built (Justice of the Crane) and directly reusable for its own Dishonor half.",
    "oracle-l5r-10898": "Needs a persistent one-time destruction shield.",
    "oracle-l5r-10900": "Needs an attachment Transfer mechanic plus Home-timing generalization.",
    "oracle-l5r-10518": "Focus 4, not 1 -- outside the #192 wave's own scope. The top-of-Fate-deck discard primitive now exists (Aberrations, issue #192), but this needs a \"whose deck\" target choice (\"any Fate deck\", not just your own) plus a variable-strength Melee Attack keyed off the discarded card's Focus Value, not a Force modifier.",
    "oracle-l5r-10337": "Needs a way for a TRAIT to negate the announced Political action's remaining effects, plus a Political-action classification (the flat +1PH half is separately wirable). The \"first time each phase\" half now exists: the once-per-phase limit (PhaseLimits.cs) and the announce-time targeting trait hook (Daigotsu Bofana, MatchEngine.OpenForcedDiscardForTargetedTrait).",
    "oracle-l5r-10519": "The Engage window and first-action claim now exist (issue #310; a Follower's 'Engage:' is a Reaction to trigger after-engaging, claim: MatchEngine.ClaimFirstOpportunity, conditional on being the Attacker). Still needs: a Follower-sourced Engage action scoped to 'this battlefield' (the Follower's own unit) and its optional 'you may target ... -3F' effect target; Reconnaissance for the acting side is settled (MatchEngine.GrantReconnaissanceAtCurrentBattlefield serves an Attacker at the enemy's province and a Defender at their own, see docs/decisions/2026-09-18-battle-timing-primitives.md decision 5).",
    "oracle-l5r-10587": "Needs a compound Gold-plus-card-discard cost shape (Kharmic keyword's own reminder).",
    "oracle-l5r-10517": "\"You may Equip this Follower as a Battle action\" is now a general capability (AttachSource.Self, issue #337: Fading Shadows attaches itself from hand to a performing Ninja). Shinobi Vassal differs in that the wielder is any Ninja Personality chosen as a target, and its own Battle action destroys a target enemy card without attachments and then the Follower itself -- deferred, not wired in #337.",
    "oracle-l5r-10338": "Needs a generic ability-usage-limit-reset mechanism.",
    "oracle-l5r-10677": "The Engage window now exists (issue #310); its 'Engage: Give a target enemy Personality or Follower -2F' is a Reaction to trigger after-engaging. Still needs: the same target category costly-feud uses (enemy-force), validated as 'at the current battlefield' by hand (a Reaction is not a Battle action, so the shared Battle-target check does not apply), and the Follower as source of a reaction (in play, attached).",
    "oracle-l5r-9966": "Holding creation is built (issue #406: Charter, oracle-l5r-10806, published created-only). Needs a general Reaction trigger \"after a battle resolves, if this card's army destroyed a province\" (the only after-battle window is the Rings' own recruit window) and destroying this Follower as the cost.",
    "oracle-l5r-10339": "Needs a conditional-keyword-by-the-card's-own-clan mechanism (the attachment-destroy half is separately wirable).",
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
    "oracle-l5r-10376": "Needs a movement-negation passive for a Terrain (the Terrain-play and additional-Battle-action halves are separately wirable).",
    "oracle-l5r-10941": "Needs a self-replacing Terrain variant (\"destroy a Terrain if able, then play this one\") plus an ongoing army-wide Force passive tied to being in play as Terrain.",
    "oracle-l5r-3767": "Its Follower may come from your hand OR a discard pile, paying 1 less Gold. The attach-from-a-discard-pile primitive (issue #337) covers the discard half (AttachCost(-1, 0)); a hand-or-discard pile choice (AttachPile has discard values only) is the piece left.",
    "oracle-l5r-10735": "The general \"before this battle resolves\" window now exists (issue #310). Still needs its own effect: a MANDATORY one-target choice ('straighten a Follower or Personality') -- beforeBattleOptional offers decline-or-target; a mandatory-target variant is a small addition; the Terrain-play half is separately wirable.",
    "oracle-l5r-10943": "Needs a self-replacing Terrain variant plus an ongoing army-wide Force passive tied to being in play as Terrain (same two gaps as oracle-l5r-10941).",
    "oracle-l5r-10551": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900/4118).",
    "oracle-l5r-5046": "Needs a keyword-conditional cost EXEMPTION (bow the performer unless it has a given keyword) -- distinct from every existing keyword-conditional EFFECT.",
    "oracle-l5r-10386": "Needs a dual-target selection with a relational constraint (\"not sharing a Clan alignment\"). The multi-target shape (issue #336) makes the two targets independent slots, each judged on its own; a constraint BETWEEN the two chosen targets is still not expressible.",
    "oracle-l5r-10388": "Needs a \"count copies of this same card played this battle\" mechanism (novel; the additional-Battle-action half is separately wirable).",
    "oracle-l5r-10718": "Refreshed: the \"delayed post-destruction straighten\" is a delayed effect of a card that is no longer in play, which the store negates by rule (a card that left play has nothing to be affected); it needs a straighten of another card triggered by this card's destruction, plus the self-replacing Terrain variant (unchanged). NO LOCAL SCAN.",
    "oracle-l5r-10958": "Needs a \"Political\" Reaction timing (not built; the plain Ranged 1 Attack half is separately wirable).",
    "oracle-l5r-10699": "The ability-copying primitive now exists (CardCopy.cs, issue #338: copy one ability that does not itself copy abilities, with Duration \"Turn\" for a copy \"this turn\"). What is still missing is this Spell's own target category (a Spell at this battlefield or a Thunder Spell at any location) and copying onto a Spell receiver during a Thunder Battle action.",
    "oracle-l5r-10960": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900).",
    "oracle-l5r-10963": "Needs an \"Interrupt\" timing (not built; the plain Melee 2 Attack half is separately wirable).",

    # Lion Clan Personality review (issue #198): the 49 unwired Lion Clan Personalities left after
    # wiring Personality wave 8's 17 cards, triaged against every primitive built through wave 8.
    "oracle-l5r-10849": "Needs a modifier hook into the existing Tactical Advantage rulebook action's own Force-bonus amount (a generic action-amount-modifier mechanism doesn't exist yet).",
    "oracle-l5r-10850": "Needs a 'retrieve a specific card from your discard pile into hand' primitive (distinct from deck search; not built).",
    "oracle-l5r-10638": "Needs a 'will not gain [keyword]' immunity flag, and a per-performer Timing override on the shared Tactical Advantage rulebook action -- neither built.",
    "oracle-l5r-10639": "Invest itself is built, but this variant needs 'refill the specific province this card entered from, face-up' -- a targeted, face-up province refill tied to entry-provenance (existing refills are face-down and untargeted).",
    "oracle-l5r-10460": "The Open Cavalry-grant is directly wirable (reuses the existing generic GrantKeyword primitive); blocked only by 'will not join a Unicorn Clan player' -- a recruitment clan-exclusion rule, not built.",
    "oracle-l5r-10291": "Needs a reveal-a-hand-card-and-offer-to-another-player mechanism with an opponent's accept/reject choice, plus a Home-targeted Attack (existing Attacks only target the current battlefield).",
    # Ikoma Shizuka (oracle-l5r-10292) published in personality-wave-27 (issue #312), the first card wired on the
    # deck search primitive (DeckSearch.cs) -- her former note ("Needs deck search ... and Regions (neither built)")
    # was stale on the Regions half: Regions have been built since region-wave-1. No remaining blocker note for her.
    "oracle-l5r-10461": "Ring of Earth is now published (ring-wave-1); 'Earth Battle' is very likely a plain printed-keyword classification too, following Moshi Ikako's own 'Fire Battle' precedent (oracle-l5r-10857, confirmed issue #194) -- not re-verified against this specific card's own scan yet. Still needs a conditional Gold-Cost waiver tied to Ring control before wiring Jikoji's own army-wide Force bonus (the waiver itself: no current Gold discount is conditional on board state, only clan-alignment-based or a fixed printed amount).",
    # Kitsu Akai (oracle-l5r-4371) published in personality-wave-17 (issue #257) once card creation
    # itself landed (MatchEngine.CreatePersonality) -- his own Limited half creates the Lion Ancestor
    # (oracle-l5r-9785, also published there, CardDefinition.CreatedOnly) his Battle half uses as a
    # Performer. No remaining blocker note for him.
    # Kitsu Kagako (oracle-l5r-4382): wired in personality-wave-67 (Forgotten Legacy pass, issue #467, batch 1) as kagako-create; its old "now unblocked" note is gone.
    # Kitsu Miro (oracle-l5r-4386) unblocked in the Embers of War pass (issue #460, batch 2): the stale note below claimed a
    # "Home Battle" Timing and creation at the current battlefield were both still missing. Neither ever needed its own new
    # primitive: Absent (Coils of Madness follow-up) already lets a Battle-timing source act without controlling a unit at
    # the battlefield ("Home actions can be taken from home" is exactly that), and CreatedCardSpec.AtBattlefield/
    # UntilBattleEnds (issue #394) already exist -- proven by Kitsu Akai's own identical 2F/2C/3PH Lion Ancestor token
    # (oracle-l5r-9785), which Miro's own miro-create ability resolves to unchanged. See personality-wave-63.
    "oracle-l5r-10293": "The deck search primitive now exists (issue #312) and its criteria can express \"a non-Unique Lion Clan Shugenja\" from the Dynasty deck, but the effect is \"refill this province with him, face-up\" (a search effect the primitive does not have yet) and the ability must be usable from a face-up province card whose own cost is discarding her and paying 2 Gold.",
    "oracle-l5r-4396": "Needs a per-player 'controls an Ancestor' Honor-gain/loss sweep referencing each player's own starting Family Honor baseline (not tracked as a stat) plus an imposed choice against another player's own card state.",
    "oracle-l5r-10852": "Needs Interrupt timing (not started); card creation is built (issue #387, the Lion Ancestor scan is published).",
    "oracle-l5r-10294": "Needs a 'this card's Force counts twice during battle resolution' hook (no existing mechanism double-counts one specific card in a resolution total), and a personality-level 'will not attach Items' restriction (the existing 'Will only/not attach to X' restriction gates the ATTACHMENT side, not a blanket type-level refusal from the host).",
    "oracle-l5r-10464": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142) -- specifically a temporary, Gold-production-only control grant over another player's Holding, without a full ownership change.",
    # Dragon Clan Personality review (issue #207): the remaining 35 of 79 catalogue Dragon Clan
    # Personalities left unwired after wave 9. The "after a duel resolves" Reaction trigger noted
    # here originally (own-won-duel, issue #209) has since landed and published three of these five
    # (Kitsuki Daisuke-exp, Kitsuki Horume's own second ability, Kitsuki Jakuei) in wave 10 -- see
    # their own now-updated/removed entries below. Mirumoto Yozo, Experienced 2's own broader "after
    # an Iaijutsu action resolves" generalization is also since published (IsIaijutsuAction, see his
    # own now-removed entry below); the "Tactical/Bushido-Virtue/Kiho/Recon 'was this an [X] action'"
    # classification-trigger gap this once cited is itself now closed too (ActionCarriesKeyword,
    # issue #321, plus the after-action-resolved Reaction window, issue #437 -- Matsu Rika, #449).
    "oracle-l5r-10451": "Unchanged: the conditional zero-cost Purchase gate tied to a named Ring and the 'increase the Honor gained from your next Proclaim this turn' effect (a player-level modifier on a future gain, not a delayed effect on a card). NO LOCAL SCAN.",
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
    "oracle-l5r-10522": "Created copies exist (issue #398: a real copy of the target, marked as created). Still needs a second target (bow one of your unbowed Personalities, copy another), the Item removing itself from the game as a cost, a lock \"will not straighten, attack, or defend until after your next turn begins\" on the second target, and removing the copy after your next turn begins (a created card removed at the start of its owner's next turn is not built). Rulings needed from Mendon: who controls the copy, where it enters, and how \"ignoring costs and restrictions\" treats a Unique target.",

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
    # Daidoji Takichi (oracle-l5r-1679): wired in personality-wave-67 (Forgotten Legacy pass, issue #467, batch 1) as takichi-move; its old note is gone.
    # Hiruma Nikaru (oracle-l5r-3310): wired in personality-wave-67 (Forgotten Legacy pass, issue #467, batch 1) as nikaru-bow-lock; its old note is gone.
    "oracle-l5r-7587": "The multi-target slots exist (issue #336: one to three Personalities controlled by other players) and the command keeps the order they were chosen in, which is what its -1F, -2F, -3F assignment reads; not yet wired.",
    "oracle-l5r-10143": "Not blocked since issue #336: two independent targets (your unbowed attacking Personality; an enemy Personality at any location) and the move of the second to the current battlefield (MoveSpec target/current-battlefield), plus the straighten; not yet wired.",
    "oracle-l5r-10529": "The multi-target slots exist (issue #336: one or two target enemy cards, a Min 1 / Max 2 slot); still needs the printed Fallen/maximum-Personal-Honor static and the bow cost; not yet wired.",
    "oracle-l5r-10716": "The multi-target slots exist (issue #336: one or two target opposed Personalities); still needs the count of different Clan alignments at the battlefield; not yet wired.",
    "oracle-l5r-10721": "Not blocked since issue #336: one to three of your target defending Personalities, +2F -- a Min 1 / Max 3 slot; not yet wired.",
    "oracle-l5r-10947": "Mercantile Conflict (Discipline, issue #353): the Discipline flag and the additive Discipline cost exist, and its first effect (bow your target unbowed Courtier, Political Open) is wirable, but \"the next two Honor losses from your cards (this turn) are increased by 1 each\" needs an Honor-loss-increase primitive -- a per-player, per-turn counter consulted at every Honor loss (GainHonor with a negative amount) that raises the first two by 1; nothing like it exists. Left unwired until that primitive is built.",
    # Before the Dawn (issue #431): the 123 cards that had no abilities were each read against the engine; 68 are wired (personality-wave-43/45/46/47, strategy-wave-43/44, item-wave-8,
    # spell-wave-16/17, follower-wave-20/21/22, holding-wave-6, celestial-wave-4, region-wave-5) and these 55 are not, for the gap each names. Where a note says a half is buildable,
    # the card stays unwired because the other half is not: a card is never wired with a printed ability missing.
    # Action-effect tracking (issue #437): MatchState.LastResolvedAction (ActionEffectTracking.cs) now records which cards an action destroyed
    # or moved (not a bow: no single choke point narrates every one the way DestroyUnits/MoveHome already do), read by a new "after an action
    # resolved" Reaction window. Measure of Devotion and Revenge, both blocked on exactly this, are now wired; Hida Defenders and Akodo Kobi's
    # own Reaction are each narrower now but not fully unblocked -- see their own updated notes below.
    # Seeds of Decay pass (issue #453), batch 1: strategy-wave-47 (twenty Strategies), stronghold-wave-12 (The Castle of Order) and celestial-wave-5 (Megumi's Guidance)
    # published Ceaseless Vigil (10139), Ruthless Onslaught (10164), Metsubushi (10158), Shifting Terrain (10168), Exchange of Letters (10147), Martial Manipulation (10157),
    # Defensive Blockade (10142), Shield Formation (10167), Embracing Battle (10146), Military Promotion (10159), Unexpected Obstacle (10177), The Arrow's Eye (10172),
    # Vengeful Dismissal (10180), Sticks and Stones (10170), Two Stings (10176), Wide Valley (10182), Grappling the Snake (10150), Sound the Retreat (10169), Requesting
    # Reinforcements (10163), Advanced Intelligence (10135), The Castle of Order (10185) and Megumi's Guidance (10028); the old notes on 10150, 10164, 10176 and 10182 are gone.
    # What they built: the standing redirect (StandingRedirects.cs: a watch that rewrites another player's next matching action as it is announced), a destruction shield that
    # outlives an action (CardInstance.DestructionShieldTurn), a per-battle Force non-contribution stamp, assignment while bowed, a keyword-keyed Stronghold Honor-loss immunity
    # and Wide Valley's trait in DestroyUnits/DestroyAttachment. The three below stay unwired:
    # Seeds of Decay pass (issue #453), batch 2: the four cards of the batch that stay unwired. The batch's own primitives (SeedsOfDecayBatch2.cs: timed province
    # strength, assignment/destruction watches, action allowances, the before-Proclaim window and Proclaim locks, permanent keyword removal, the Honor-loss record on
    # a resolved action) do not reach these; each note names the exact gap and the half that IS buildable today.
    # Seeds of Decay pass (issue #453), batch 4: the seven cards of the batch still unwired (17 wired: personality-wave-58/60, holding-wave-7, region-wave-6, event-wave-5, follower-wave-26).
    # Seeds of Decay pass (issue #453), batch 5: the two cards of the batch left unwired (all nineteen others are wired in item-wave-11, spell-wave-18 and follower-wave-25).
    # Seeds of Decay pass (issue #453), batch 3: two Personalities left unwired, each on one precise gap.
    # Embers of War pass (issue #460), batch 2: 12 of the batch's 25 Personalities are wired in personality-wave-63; these 13 stay blocked.
    # Embers of War pass, batch 3 (issue #460): eleven Strategies left unwired (ten others of the same batch are wired in strategy-wave-52).
    "oracle-l5r-3739": "Inexorable (Embers of War): \"Reaction: After another player announces a Reaction during the Combat Segment: Its effects will not negate other effects.\" Re-checked after issue #503. The trigger is now reachable (group P3 built announce windows that ask more than one watcher per announcement, EmperorP3Priority.cs, and the window could be opened for the OTHER player's hand Strategy when the announced action is a Reaction in a Combat Segment), but the effect is not: \"its effects will not negate other effects\" needs one engine-wide notion of 'a negation made by this action's effects' that can be switched off for one resolving action. Negations live in a dozen unrelated places -- MatchState.NegatedEffects (per card and kind), the whole-action flags (DeferredActionNegated, PendingAttack.Negated, AnnouncedActionNegated, RemANegatesHeldAction, HeldProclaim.Negated, the P3 Event negation), standing card fields that negate later effects (StraightenHolds, MovementLocks, TargetShield, ForceModifiersNegatedTurn, Stalwart-style per-turn stamps), the R4 Honor marks (RemC.NegateLossesActionKey, HonorReducedActionKey, P3 NegatedHonorGains), Fujita's Follower shields and the delay store's negated entries -- and no action records which of them it set. A snapshot-and-revert of a hand-picked list would silently miss the rest, so the card would be partial. Needs: a single NegationLedger that every negation writes through (kind, target, source action key), so an action marked 'non-negating' (Inexorable) has its entries dropped as it resolves; then Inexorable is an announce-window Reaction of the other player that marks the announced Reaction's key.",
    "oracle-l5r-6059": "Its Open half (\"put one or two target Followers in your discard pile into your hand\") is an ordinary multi-target discard-to-hand pick (TargetSlots' own Min 1 Max 2 shape) and ready to wire on its own. Its Focus Effect (\"before your Personality in this duel is destroyed for losing it, you may destroy one of his Followers to negate his destruction\") needs a Focus-Effect-TIMED hook into duel-loss destruction specifically -- Wide Valley's own \"negate a destruction inside a unit\" trait (issue #453 batch 1) is a standing always-on rule folded into DestroyUnits/DestroyAttachment, not a live in-the-moment CHOICE offered only at Focus Effect resolution before one specific destruction happens; ApplyDuelFocusEffect (Dueling.cs) runs each focused card's own effect in order but has no shape yet for \"conditionally pre-empt the very destruction the duel loss is about to cause.\" The card's own single printed ability id keeps both halves together rather than splitting.",
    # Embers of War pass (issue #460), batch 4: strategy-wave-53 wired 8 of the batch's 24 Strategies (Sheathing the Sword, Tactical Trap,
    # Unstoppable Strike, You Are Unworthy, The Cycle of Vengeance, Stuck in Rokugan, Thick Marsh, Stand as the Mountain); these 16 stay blocked,
    # each on a precise gap. A new enemy-personality-at-any-location Target category (CardAbilities.cs) resolved the oracle-l5r-6727/5941
    # cross-batch blocker; oracle-l5r-5941 (Phantom Visage, a parallel batch's card) still needs its own re-check against the category by its
    # own batch, since it may not have landed yet when this pass ran.
    "oracle-l5r-9008": "Needs the same pre-resolution Interrupt window as oracle-l5r-6265 (\"Negate your target Commander's movement from the action's effects\" before another player's Battle action resolves) -- see that entry for the exact gap. The Battle half (\"Take an additional action to use a printed ability on your target Follower, even if it is bowed or the ability has been used the maximum number of times this turn\") is close to buildable on its own (Shiba Tsukimi's RefreshBattleAbilities clears a per-turn use stamp; the bowed-source exemption is a literal ability-id list in CardAbilities.cs), but the card's own identity is the Interrupt, so it stays blocked whole.",
    # Embers of War pass (issue #460), batch 5: six of the batch's twelve Spells, one Celestial and two Strongholds stay unwired; eleven cards are wired
    # (spell-wave-20, stronghold-wave-14, region-wave-7). The Distant Reaches's old note (issue #336's Slots primitive) is gone now that it is wired.
    # Embers of War pass (issue #460, batch 6): 11 of the batch's 24 cards stay unwired. Thirteen (item-wave-12, holding-wave-9, follower-wave-28,
    # personality-wave-64) are now wired -- see docs/decisions (batch 6 summary) for the full list.
    "oracle-l5r-7869": "Needs an \"after the first time each turn you bow this Holding\" Reaction trigger: no existing Trigger fires off a card's OWN bowing (own-enters-play/own-won-duel/before-any-action-resolves are the nearest shapes, none of which watch a bow of the source itself), and a once-per-turn stamp distinct from AbilityUses (a Reaction's own per-turn cap already exists, but nothing currently calls into a Reaction dispatcher from inside Bow()/BowSource cost application at all).",
    "oracle-l5r-9997": "Proxy (a Ronin Samurai, 2F/2C): same orphan-reference gap as oracle-l5r-9995/9996 -- no creating card found (Dubious Resources, the one Ronin-creating card located in this search, creates a 2F Ronin FOLLOWER attached to a Personality, oracle-l5r-10145, a different card from this 2F/2C Ronin Samurai PERSONALITY Proxy).",
    # Embers of War pass, batch 1 (issue #460): 26 candidate Personalities re-checked against the engine as it now stands. Five are wired
    # (personality-wave-62: Akodo Uehara, Daigotsu Takayasu - exp, Goju Gochiso, Kaiu Onizuka, Kaiu Tojikana -- their own printed abilities all
    # reduce to existing primitives: EntryHonorLoss, a fixed/computed-strength Attack (Ebizo/Chiyo's own shape), enemy-card-without-attachments
    # (Masumi's own shape), Ohsuki's Sparrow's own conditional-Honor-gain Require, Kitsuki Miroken's own opposing-army Force conditional, and
    # Isawa Tamaki's own top-1 LookSpec). The remaining 21 are not wired; each note below is current as of this pass, not carried over unread.
    # Forgotten Legacy pass (issue #467), batch 2: 17 of the batch's 20 cards are wired (personality-wave-66, follower-wave-29, stronghold-wave-15; the
    # stale notes of The Vengeful, Matsu Koyama and Togashi Osawa were removed -- the bowed-source exemption list, a one-shot unit penalty and
    # personality-at-source-location cover them). These three stay blocked.
    # Forgotten Legacy pass (issue #467), batch 4: strategy-wave-55 wired 10 of the batch's 16 Strategies (Heart of Darkness, Intimidation, Never Beyond My Reach,
    # Pearls & Spirits, Returning Home, Rise of the Dark Naga, Seeking the Truth, Siege Volley, Steadfast Defense, Sundering Strike -- the stale notes for
    # oracle-l5r-3790 and oracle-l5r-5533 removed); these six stay unwired.
    "oracle-l5r-5648": "Oblivious. NO SCAN AVAILABLE (not in the Forgotten Legacy art under assets/reference/oracle/forgotten-legacy/webp, and the Oracle image host is blocked by this sandbox), so it cannot be published whatever its mechanics. Mechanically it is close to wirable but was not built for an unpublishable card: \"Bow your two performing Nonhuman Personalities\" is a Performing TargetSlot of exactly two (Redoubled Attack/Dislodge the Foe's performing-slot shape) with a new own-unbowed-Nonhuman category, bowed in the effect case (ActionCost's Performer subject names only the single command.PerformerId, so a two-card bow cost is not declarable); \"a card without attachments in a unit and with a lower Gold Cost than the combined Gold Cost of your Nonhumans\" needs a new Target category comparing CardGoldCost(target) with the SUM over the performing slot (enemy-personality-gold-cost-lte-performer compares one performer, and only Personalities).",
    "oracle-l5r-9208": "Wanted for Questioning (issue #503, P6, re-checked against its scan): 'Battle: Choose your performing unbowed Magistrate and target an enemy Personality: Your Magistrate takes him into custody. If he is dishonorable, destroy his attachments. Move him home.' 'Custody' is still undefined: docs/rules/EMPEROR_EDITION_CORE_RULES.md has no entry for it (grep finds none) and the engine has no custody state. Rules-lane question for Mendon: is 'takes him into custody' flavour (then the card is buildable at once: an unbowed-Magistrate performer at the battle, an enemy-personality target, destroy his attachments if dishonorable, MoveHome), or a mechanic (what it does, how long it lasts, how it ends)?",
    # Forgotten Legacy pass (issue #467), batch 1: 16 candidates (15 Personalities and the Proxy Hisano's Guide). Twelve are wired in personality-wave-67
    # (Asahina Shigemitsu, Asako Kaitoko, Bayushi Higaonna, Bayushi Toshimo, Daidoji Takichi, Daigotsu Gyoken, Daigotsu Murota, Doji Hakuseki, Doji Rengetsu,
    # Hiruma Nikaru, Iuchi Abodan, Kitsu Kagako); these four stay unwired and unpublished.
    "oracle-l5r-551": "Asako Karachu: SCAN UNAVAILABLE -- no Forgotten Legacy scan exists in this checkout's art pack and the Oracle image host is blocked by the sandbox's network policy, so the card cannot be published. Mechanically wirable as it stands: \"After Karachu bows: Straighten him\" is a mandatory triggered trait with exactly Ronin Ambushers' shape (an Effect read by ApplyAfterCommandTraits, SeedsOfDecayBatch5.cs: \"After this card bows: Straighten it\", minus Ambushers' Terrain-conditional straighten hold) -- one Effect id and one line in ApplyAfterCommandTraits once a verified scan is available.",
    # Forgotten Legacy pass (issue #467), batch 3: 14 Strategies (A Game of Dice .. Grateful Reward) re-checked against the engine as it now stands. Ten are
    # wired in strategy-wave-54 (A Game of Dice, A Yojimbo's Duty, Accidental Confession, Broken Alliance, Cast Aside the Weak, Consideration, Creating Order,
    # Duel of Haiku, Entrenched Position, Feint & Strike -- each published with its scan's own text; ForgottenLegacyBatch3.cs). The four below are not.
    # The Shadow's Embrace pass (issue #476), batch 2: 23 Personalities (Kitsu Sorano .. Zlkyt). Nineteen are wired in personality-wave-75 (ShadowsEmbraceBatch2.cs;
    # the stale notes of Kitsu Sorano - exp, Kitsuki Yoyugi and Shugorei were removed -- CreatedCardSpec.Count, the overlay's own entry window, the single-ability lock
    # of DisabledAbilities and ProvinceDestroyedSinceTurnEnd cover them). These four stay unwired and unpublished.
    # Forgotten Legacy pass (issue #467), batch 5: all 15 cards wired (spell-wave-21, holding-wave-11, item-wave-13, event-wave-6, celestial-wave-6,
    # region-wave-8), so no note of this batch stands. Cursed Relic's old note (oracle-l5r-1607, "Chi as a repeatable payable cost") was removed: CostVerbs.ChiPenalty
    # (AbilityCosts.cs) now pays "give this Personality -1C" in full or not at all, and the ordinary 0-Chi rule (ApplyModifier) destroys a wielder it empties.
    # The Shadow's Embrace pass (issue #476), batch 5: 18 cards (10 Spells, 8 Items). Fifteen are wired (spell-wave-22: Beseech Sakkaku, Channel the Moon,
    # Purify the Soul, Set the Fields Aflame, Thunder's Stamina, Touch the Emptiness, Ward of Air, Your Heart's Enemy; item-wave-14: Blood of the Preserver,
    # Celestial Sword of the Crab/Crane/Phoenix, The String of the Colonies, The Victor, Vanquisher's Armor). The three below each create a card whose scan is not
    # available here; each is otherwise wirable with existing primitives, as its note says.
    # The Shadow's Embrace pass (issue #476), batch 6: 23 cards (7 Holdings, 5 Followers, 5 Events, 3 Strongholds, 2 Celestials, 1 Region) re-checked against the engine
    # as it now stands. Eighteen are wired (ShadowsEmbraceBatch6.cs): holding-wave-12 (Fortress of Lidless Eyes, Platinum Mine, Sheltered Port, Small-Time Bully,
    # Versatile Farm, Whispering Archive), follower-wave-30 (Clearing Crew, Elephant Caravan, Shieldman, Wasteland Scout -- their four stale notes removed: the delayed
    # End Phase draw, Elephant Caravan's enemy-card-force-lte-self, Shieldman's assignment/move refusal and after-action Reaction, and Wasteland Scout's Follower +1F
    # sweep are all built now), event-wave-7 (Discovering the Temple, On, Star-Crossed Lovers, Wartime Preparations), stronghold-wave-16 (Bishamon's Tower, Twin Forks
    # City) and celestial-wave-7 (The Moon's Imperative, The Sun's Aspiration). The five below are not.
    "oracle-l5r-9797": "Humble Farm (issue #503, P6, re-checked): its own scan is local; blocked only by its created plain 2F Ashigaru Follower. No local Proxy fits (Fledgling Ashigaru is 0F, Mounted Ashigaru 1F Cavalry with no local scan, Ronin Follower 2F Ronin, Infantry Follower 2F with no keyword); docs/requests/created-card-scans-needed.md row B3 (ashigaru-2f.jpg) is unticked. Otherwise GoldProduction 2 plus Ashigaru Recruits' Battle/Open create-and-attach with a Sacrifice cost.",
    "oracle-l5r-9870": "Lookout (scan text: \"Will only attach to a Ninja. Reaction: After you announce a Ranged Attack action, destroy this card: One Ranged Attack from that action may target a card in the Defender's home.\"; the catalogue's Ninja Interrupt wording is the later Oracle text). \"Will only attach to a Ninja\" is AttachRequiresKeyword and the announce window exists (ResolveOrDeferAnnounceReaction, Tsuruchi Yakusho's own-ranged-action-announced), but the effect cannot be built on it: an Attack's target is fixed and judged when the action is ANNOUNCED (RequireLegalAttackTarget, AttackTargeting.cs: 'InPlay && AtBattle && enemy'), before the announce window opens, so a Reaction taken after the announcement cannot widen that target to a card in the Defender's home -- the Attack would need its target chosen (or re-chosen) after the announce Reactions, which the deferred-Attack pipeline does not do. The announce window is also opened only for a watcher at the current battlefield (Yakusho's printed condition) and for one watcher at a time (FirstOrDefault).",
    # The Shadow's Embrace pass (issue #476), batch 1: 23 Personalities (Agasha Kurou .. Kazehime), none with an earlier note. Twenty-two are wired in
    # personality-wave-74 (ShadowsEmbraceBatch1.cs): Agasha Kurou, Asahina Keigo, Asako Hayate, Asako Jirou, Atosa, Bayushi Misaki, Bayushi Tarou, Bayushi Waru,
    # Chiisai, Chuda Hitaka, Daidoji Sosuke, Daigotsu Arakan, Daigotsu Rafu, Goju Bunoro, Hida Bakari, Hiruma Hikazu, Hiruma Nikaru (oracle-l5r-9806), Iuchi Haruma,
    # Kaiu Fumiko, Kakita Ichigiku, Kakita Tadanobu, Kazehime. The one below is not.
    # The Shadow's Embrace pass (issue #476), batch 3: 21 Strategies (Burning Fury .. One Shout) re-checked against the engine as it now stands. Nineteen are wired in
    # strategy-wave-56 (ShadowsEmbraceBatch3.cs); the old notes of Crowded Streets (oracle-l5r-9900, "after an action resolves that a Personality performed" -- now the
    # after-action-resolved point's LastResolvedAction.PerformerId) and Kuon's Legacy (oracle-l5r-9913, "not yet wired") were removed. The two below are not.
    # The Shadow's Embrace pass (issue #476), batch 4: 20 Strategies (Overcome Adversity .. Watchers in the Dark) re-checked against the engine as it now stands.
    # Seventeen are wired in strategy-wave-57 (Overcome Adversity, Paralyzing Touch, Preserving Beauty, Running Battle, Serenity in Air, Social Grace, Spiritual
    # Manipulation, Subtle Games, Sudden Movement, Tactical Assault, The Game Has Changed, The Lesson of Earth, Theological Indecision, Thuggish Techniques, Unstable
    # Ground, Void Tattoo, Watchers in the Dark -- each published with its scan's own text; ShadowsEmbraceBatch4.cs). The stale notes of Paralyzing Touch (the
    # "delayed negation of a FUTURE action" is now MatchState.ParalyzingTouchWatches) and The Lesson of Earth were removed. The three below are not wired.
    # Emperor Edition Personality pass (issue #477): the seven Personalities the pass could not wire.
    # Emperor Edition Stronghold pass (issue #486): the one Stronghold the pass left unwired.
    # Emperor Edition remaining-cards pass (issue #492): the cards the pass left unwired, one determination each.
    "oracle-l5r-5979": "Plains of Otosan Uchi (issue #503, P6, re-checked): still blocked on the two-card province. Every province is one card slot (ProvinceState.CardId): the Events Phase reveal, Purchase and Reserve recruits, Refill (now also the face-up refills of Susaiken / Distant Expansion and Ietsuno's refill trigger, EmperorP6.cs), DestroyProvince, the private province sights and MatchView.ProvinceView all assume one card. Mendon (2026-09-25): a province must be able to hold two Dynasty cards; the view/client second slot is requested from Alfredo in issue #499 (not done: ProvinceView has no second card). The engine side (a second slot's reveal, refill, purchase and destruction, the owner's choice of which slot goes first, 'do not reveal the second card in the Events Phase this card enters play', and the 10-Honor loss before the province is destroyed) should follow once the view lane is agreed, so a published card does not show only half of its province.",
    # Emperor Edition rulings pass (issue #494): blockers refreshed or added by the rulings groups.
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

    # Second table: the cards that remain Unwired, broken out by Set (rows) and card Type (columns).
    unwired: dict[str, dict[str, int]] = {}
    for _id, _name, card_set, t, _clan, status, _blocker in rows:
        if status == "Unwired":
            counts = unwired.setdefault(card_set or "(no set)", {})
            counts[t] = counts.get(t, 0) + 1
    unwired_types = [t for t in TYPE_ORDER if any(t in c for c in unwired.values())]
    unwired_types += sorted({t for c in unwired.values() for t in c} - set(unwired_types))
    summary.append([])
    summary.append(["Unwired cards by Set and Type"])
    summary[summary.max_row][0].font = Font(bold=True, size=12)
    summary.append(["Set", *unwired_types, "TOTAL"])
    for cell in summary[summary.max_row]:
        cell.font = Font(bold=True)
    for card_set in sorted(unwired, key=lambda k: (-sum(unwired[k].values()), k)):
        counts = unwired[card_set]
        summary.append([card_set, *[counts.get(t, 0) or None for t in unwired_types], sum(counts.values())])
    summary.append(["TOTAL", *[sum(c.get(t, 0) for c in unwired.values()) for t in unwired_types], sum(sum(c.values()) for c in unwired.values())])
    for cell in summary[summary.max_row]:
        cell.font = Font(bold=True)
    summary.column_dimensions["A"].width = 30
    for index in range(2, max(5, len(unwired_types) + 3)):
        summary.column_dimensions[get_column_letter(index)].width = 13

    sheet = wb.create_sheet("Cards")
    categories_path = root / "tools" / "content" / "blocker_categories.json"
    category_data = json.loads(categories_path.read_text(encoding="utf-8")) if categories_path.exists() else {"categories": [], "cards": {}}
    category_of = category_data["cards"]
    header = ["Id", "Name", "Set", "Type", "Clan", "Status", "Blocker", "Blocker Category"]
    sheet.append(header)
    header_fill = PatternFill(start_color="FFD9D9D9", end_color="FFD9D9D9", fill_type="solid")
    for cell in sheet[1]:
        cell.font = Font(bold=True)
        cell.fill = header_fill
    for row in rows:
        sheet.append([*row, category_of.get(row[0], "") if row[5] == "Unwired" else ""])
    last_col = get_column_letter(len(header))
    table = Table(displayName="CardInventory", ref=f"A1:{last_col}{len(rows) + 1}")
    table.tableStyleInfo = TableStyleInfo(name="TableStyleMedium2", showRowStripes=True)
    sheet.add_table(table)
    widths = {"A": 16, "B": 34, "C": 18, "D": 14, "E": 16, "F": 10, "G": 70, "H": 16}
    for col, width in widths.items():
        sheet.column_dimensions[col].width = width
    sheet.freeze_panes = "A2"

    if category_data["categories"]:
        cat_sheet = wb.create_sheet("Blocker Categories")
        cat_sheet.append(["Code", "Category", "Cards", "What is missing"])
        for cell in cat_sheet[1]:
            cell.font = Font(bold=True)
            cell.fill = header_fill
        for entry in category_data["categories"]:
            count = sum(1 for code in category_of.values() if code == entry["code"])
            cat_sheet.append([entry["code"], entry["title"], count, entry["description"]])
        for col, width in {"A": 12, "B": 60, "C": 8, "D": 110}.items():
            cat_sheet.column_dimensions[col].width = width
        cat_sheet.freeze_panes = "A2"

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    return dict(cards=len(rows), wired=total_wired, output=str(OUTPUT.relative_to(root)))


if __name__ == "__main__":
    print(json.dumps(build()))
