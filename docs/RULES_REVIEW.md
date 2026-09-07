# Emperor rules-review playtest

The purpose of this playtest is to find discrepancies in the **gameplay loop, timing, card effects and bookkeeping**, not to judge another art pass. The reviewer does not need to use an AI or write code.

## Start playing

Accept the private repository invitation. Open **Releases** and download the Mac or Windows game ZIP from the current development checkpoint. Extract the ZIP completely. On Windows, keep the executable, `.pck` and .NET directory together. On Mac, launch the extracted app. These are private development packages; the Mac package is locally signed and not notarized, and the Windows build is not publisher-signed. Report a launch failure as an issue rather than assuming the package ran successfully.

Choose **Practice**, select the published Lion or Crane deck, and try both Clans. You can use the practice opponent or the pass-device flow. A separate SDK/editor installation is unnecessary for these downloads. To build from source, use `AI_HANDOFF.md`.

The first review target is the frozen `lion-crane-v1` profile, not the entire Emperor card pool. Read `docs/rules/LION_CRANE_COVERAGE.md` for deck contents, selected printings and explicit historical decisions. Current Oracle wording may differ from the chosen period scan.

## What to examine

- Starting setup, first-player effects, Bamboo Harvesters' delayed straightening, and Border Keep refresh choices/order.
- Turn phases and action priority: who may act, passing, reaction/choice windows, once-per-turn/game limits and reset timing.
- Gold production and payment: source bowing, conditional production, costs, excess, Clan purchase options, and optional Proclaim. Gold is produced to pay a cost; it is not an accruing bank balance.
- Entering play, Holdings/Personalities, unit attachments, Armor/Weapon limits, and independent bow states.
- Each card's actions and traits: performer, legal targets, presence/location, costs, optional decisions, duration and destruction triggers.
- Attack/defense assignments, movement home, returning units/attachments, battle selection, action passes, Force accounting, province destruction and battle Honor.
- Favor actions, opponent choices such as Shinden Asahina, end-of-turn cleanup, hand/draw/discard flow, Honor/dishonor consequences and reachable victories.
- Whether the player can complete a legal action without manual bookkeeping or guessing what the controls require.

No card in the current two lists introduces the complete set of Duel, Spell, Event, Cavalry, Region or Ring mechanics. Record unsupported broader mechanics as future scope rather than claiming those catalogue cards were implemented.

## Report a discrepancy

Open an issue using **Rules / gameplay discrepancy**. Include:

1. Build/version, OS, Clan, mode and whether it is reproducible.
2. Exact card names and selected printings; turn, phase, priority and relevant board state.
3. The actions taken, costs paid, targets chosen and any pending choice.
4. What happened versus what Emperor rules should do.
5. Rulebook page/section, card scan or historical erratum supporting the expectation. If uncertain, label it an interpretation question.
6. A screenshot, short clip, or exported replay if available. Do not include seat tokens or room storage. A replay can contain private cards, so share it only within this private review group.

One interaction per issue is easiest to reproduce and verify. Balance opinions and historical legality questions should be distinguished from a rules-engine defect.

## Useful first sessions

1. One complete game as Lion and one as Crane, noting every confusing or incorrect transition.
2. A targeted run through Border Keep, payment/Proclaim, Shinden choices, Favor, attachments and battle movement.
3. A second-person match with both players checking priority and hidden information.
4. On Windows, explicitly record launch, card readability, input and full-game behavior. An export or CI result does not establish this.

The repository does not host an online match server. Local testing instructions are in the top-level README; an external private Mac/Windows session needs the separately planned server deployment.
