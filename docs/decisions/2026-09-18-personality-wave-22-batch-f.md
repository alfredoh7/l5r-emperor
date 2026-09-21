# Personality wave 22 / region-wave-3 (Batch F, the final BLOCKERS batch): new Targets, two opponent choices, a shared entry helper

Status: accepted. Lane: shared (engine rules plus the standard per-wave shipped art). Issue: #301.

## Decisions

**Several BLOCKERS claims were wrong about what already existed.** Agasha Kodo's note said a new "holding"
Target was needed; `any-holding` (Yasuki Tsujiken's) already existed and is reused verbatim. Asahina
Munefusa's note called for generalizing `own-unbowed-farm` and Shosuro Hawado's for a variant of the
owner-restricted discard categories -- both genuinely new (`own-holding-with-keyword`,
`dynasty-card-in-any-discard`). The batch brief called Rikate's Chi penalty a "first real use" of Chi as an ApplyModifier stat;
Batch C's Marshal Your Strength already applies Chi, and `ApplyModifier` already destroys a Personality
reaching 0 Chi.

**The bring-a-province-Personality-into-play sequence is now one helper.** Your Clan Needs You!
(Batch D, into the current battlefield) and Shinden Shorai (into Home) share
`MatchEngine.BringProvincePersonalityIntoPlay` -- pay, leave the province, enter play, queue
Overconfident, refill, then Proclaim / the card's own Reaction / the Overconfident draw. Purchase and
RecruitReserve keep their own inline copies (they are commands with their own priority rules). Its
Proclaim window takes an explicit resume-to player: `null` (the active player) keeps Your Clan Needs
You!'s existing behavior; Shorai passes the opponent, since a Limited Action-phase action cedes priority.

**"Not your first or second turn" is the player's own turn count.** `PlayerState.TurnsTaken` (the
counter `border-first`'s "your first turn" already reads) counts only that player's turns;
`MatchState.TurnNumber` advances once per either player's turn and is the wrong scale. Shorai requires
`TurnsTaken > 2`.

**Shorai's Lion waiver is keyed on the Personality, not the player.** `HonorRequirementWaiverClan`
(Batch E) is a data field on the *recruited* card compared with the *recruiting player's* Clan. Shorai's
is the opposite axis, supplied by the *action*, so it is a new `QuotePurchase` parameter
(`ignoreHonorRequirementIfPersonalityClan`), never set by an ordinary Purchase. `ValidateAbility` also
quotes the recruit, so an offered Shorai command is one that resolves and is priced at the recruit's
full cost ("paying all costs": no Clan discount or allegiance route).

**Munefusa follows the existing "even if not at the current battlefield" reading.** Her Battle half joins
`reinforcesFromElsewhere`, which waives both the acting player's army presence and the source's own
AtBattle check, exactly as Sanctuary of the Waves and Utaku Mai already do; only the target must be at
the battlefield. (Ring of Earth's printed "even if you control no units" is a separate clause, so a
narrower reading -- source presence waived, player presence kept -- is arguable; this batch keeps the
established precedent rather than splitting it. Worth revisiting if a rules ruling says otherwise.)
The Honor gain requires the target to have actually ended up Home, so a negated move (Tamori Tamoko)
gains nothing.

**Hawado's "a battlefield with one or more enemy units" is a chosen destination.** The single-battle
shortcut Trusting Instinct took ("a battlefield" = the current one) would make her second half a no-op,
since she must already be at the current battlefield to act. The battlefield half instead names any
battlefield not yet resolved this Battle phase (`UnresolvedBattlefields`) where the enemy has a unit, in
`command.Province`. `QueryActions` lists one action per ability with its first legal command, so the
offered command already names a valid destination; other destinations are direct-command variations
(the same limit `any-province` abilities already have). She straightens only if she really moved, and
the two halves share one `UsageKey`. "A Dynasty card in any discard pile" is a Dynasty-deck card type in
`Zone.DynastyDiscard` or `Zone.Dead` (a Personality's discard pile once dead), either player's.

**Two new dedicated opponent-choice PendingChoices, shinden's shape.** `rikateDetach` lets the target's
controller return *any subset* of the unit's attachments to hand (`CardIds`; empty declines) *before* the
-2C applies -- observable when the penalty is lethal: returned attachments are safe in hand, kept ones are
destroyed with him -- and is skipped entirely when the
unit has no attachments or Champion of Thunder's "negate all" protects the target. `rinShame` lets the
target's controller dishonor him or be bowed with his straightening negated; Stalwart negates only the
bow, so the straightening negation applies either way. Rin's own bow is a cost paid up front.
`ReturnToHand` was generalized to detach an in-play attachment (ends the host's Attachment-duration
keyword grants, clears stale Bowed/Modifiers/Tokens, destroys a host left at 0 Chi -- never a
destruction, so no Expendable draw).

**"Shadowlands" also matches the printed keyword.** Real cards (Rikate, Takasho, Keppo, Daigotsu Kendo)
print Shadowlands as a keyword beside their Clan, and the catalogue records no Shadowlands Clan for
them, so Mitsuko's "Kolat, Ninja, or Shadowlands" uses `IsShadowlands` (Clan alignment *or* keyword).
`dispensing-justice-destroy` and `winds-of-dismissal-choice` still check Clan alignment only and can
under-match those cards; left untouched here.

**Hawado's Clan is Scorpion.** The pipeline takes the catalogue's first-listed clan, which for her is
"Ninja" (a keyword here); Clan drives Proclaim, Clan discount and Loyal, so `OVERRIDE` corrects it.
Shosuro Konishi (personality-wave-3) has the same shape and still says "Ninja"; not changed here.

**Smaller pieces.** Kojiro's Naval is a live check in `Keyword()` (Tamori Yayu's Cavalry shape), never a
durable grant. Minori's bonus is `PersonalityForce`'s Effect-keyed, self-received, army-scoped count
(printed or granted Magistrate, bowed ones included, herself excluded, no attacking gate). Neiru's Open
equip is exempt from the once-per-turn dedupe like `equip-battle` and grants no Additional Action.
Sutigu's Performer is Sutigu or one of his own unbowed Weapons. `MatchEngine.HasKeyword` is an
`internal` test seam for the live conditional keyword. Ability-payment UI for Neiru/Shorai has the same
client gap Low Market and Equip (Battle) already have (the client only computes a cost for Strategy/equip).

## Affected files

`src/L5R.Core/CardAbilities.cs`, `MatchEngine.cs`, `MatchQueries.cs`, `PublishedContent.cs`,
`CardCatalog.cs`, `L5R.Core.csproj`; `content/published/personality-wave-22*.json`, `region-wave-3*.json`;
`tools/content/package_personality_wave_22.py`, `package_region_wave_3.py`;
`tests/L5R.Core.Tests/Program.cs` (`BatchF*` tests), `PublishedTests.cs`.
