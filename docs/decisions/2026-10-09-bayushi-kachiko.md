# Bayushi Kachiko (#778)

Date: 2026-10-09. Card: Bayushi Kachiko, `oracle-l5r-10481` (Coils of Madness, Experienced CoM), profile `personality-wave-302`. Engine: `src/L5R.Core/Kachiko.cs`. Tests: `kachiko` (5).

Deferred by #734 until "attacks" was read. Mendon's reading (2026-10-09, his words in the task): read her ability broadly. The first time the target Personality is assigned to an attacking army during Maneuvers, or joins an attacking army by moving or being moved to a battlefield, its controller loses 2 Honor. The effect expires during the End Phase of the turn it was activated.

## What is wired

- **Printed text:** "Personalities and Followers have -1F while they are Fallen, Nonhuman, or attacking you." / "Political Open, bow: After the next time this turn a target Personality attacks, his controller loses 2 Honor."
- **Open (`kachiko-watch`):** Political Open that bows Kachiko; the target is any Personality in play, either player's. It sets an `AssignmentWatch` of kind `kachiko` on the target for the current turn.
- **"Attacks":** fires from the existing assignment sites (`FireAssignedToAttackWatches`: the Maneuvers Segment and an effect that assigns to an attacking army) and from the two movement sites Consecration already uses (`MoveToBattlefield`, `SwitchRelocate`) through `FireKachikoJoinWatches`. The watch is spent by the first time. A watch of an earlier turn never fires, which is the turn-end expiry.
- **Static -1F:** `PersonalityForce` / `FollowerForce` hooks, once for each Kachiko in play: a Personality or Follower that is Fallen or Nonhuman (its own keywords), or whose unit is attacking and whose controller is not Kachiko's controller. A Follower is "attacking" when its Personality is.

## Readings for Mendon to confirm or correct

1. **"Joins an attacking army" is the Attacker's Personality arriving at a battlefield it was not at**, from home or from another battlefield, by an effect (`MoveToBattlefield`, the switch/relocate path). A Personality that enters play at a battlefield (a Reserve recruit) is entering play, not moving, and does not fire the watch, the same line Consecration draws.
2. **A Defender's Personality moving to a battlefield loses nothing:** the army he joins is a defending one.
3. **The watch is not cleared by Kachiko leaving play;** once set it stays for the turn.
4. **The -1F is the card's own keywords:** a Follower with Fallen/Nonhuman of its own is reduced; one whose Personality is Fallen is not, unless it is attacking. Her own controller's attackers are not "attacking you".

## Art pack

The new scan (`oracle-l5r-10481.jpg`) is covered by `catalogue-art-issue778-20261008-r1` (4,628 images, 618,172,096 bytes, SHA-256 `1f789082a5ecfa635ac91f37fd4f7c153d333535387e862e22b1937167032c07`), built with `--base-archive` from `catalogue-art-issue768-20261008-r2` (4,627 images). Native visual and playability review has not been done.
