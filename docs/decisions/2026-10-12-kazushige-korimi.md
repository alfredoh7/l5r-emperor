# Tamori Kazushige's Open and Togashi Korimi's Interrupt (issue #859)

Status: **accepted for review**. Lane: rules. Engine: `src/L5R.Core/Wave859.cs`; the abilities are added to the already published `personality-wave-9` cards (`tools/content/package_personality_wave_9.py`).

Found by the printed-action sweep after #848: both cards had only their static +2F while defending wired.

| Card | Wired as |
|---|---|
| Tamori Kazushige (oracle-l5r-7777) | `w859-kazushige-straighten`: Bayushi Misaki's Open (another player's turn, one of the active player's Personalities) with the same watch (`MisakiWatch`, Kind `kazushige`): straightened before the phase ends; the acting player gains 2 Honor before the turn ends if he was not assigned to attack |
| Togashi Korimi (oracle-l5r-8545) | `w859-korimi-draw`: an entering-play Reaction that bows him as its cost and draws a card |

## Readings for Mendon to confirm

1. **Kazushige's "gain 2 Honor"** goes to the player who used the action (Misaki's Honor loss names the target's controller; this text names no one, so "you").
2. **Korimi's "Interrupt ... After you Recruit Korimi"** is played in the entering-play Reaction window (he is recruited by entering play), and "Bow this card" is its cost, so he must be unbowed to use it.
