# Battle placards (replaces the court rails of #710)

Issue #709, PR #710. Mendon replaced Alfredo's proposed A+C court rails with this layout; the rails code and ornament art were removed, so there is a single presentation.

## Behaviour

1. **Maneuvers, battle selection and battle** (`BattleActive`): each undestroyed *defending* province carries a Province number and Strength placard under its card, using the engine's effective Strength. Pressing it runs `SelectProvince`, the same path as pressing the province: it picks the battlefield in Battle Selection and assigns the selected unit during Infantry/Cavalry/Defense assignment. A gold edge marks a battlefield the engine currently offers.
2. **Engaged battle**: a fixed stack on the overlay plane (`_clanHud`) at the centre of the left edge, top to bottom: the opponent's placard (clan, Attacker/Defender, Force, units), the Province placard (number, Strength), then the viewing player's placard. A battle with no province omits the middle placard. All three share one size, scale, left edge and column grid.
3. **Frame**: the clan placards' own chalkboard and brass frame (`Art/ClanPlacard/frame.png`, nine-patch). A native check fails if any text or icon crosses the frame border.
4. **Province Destroyed icon** (`Art/BattleValues/province-destroyed.png`, from Mendon's supplied icon): shown in the Province placard, in the column of the Units counts above and below, when the attacker would destroy the province if both players passed.

## Rule and contract

The client does no arithmetic. `BattleValuesView` gains `ProvinceFalls` (optional, default false), computed in `CurrentBattleValues` from resolution's own test: attacker Force **exceeds** defender Force plus effective Province Strength (`af > df + strength`), and none of the bars resolution applies (Aramasu's Legacy, Perfect Moment, Rend the Soul) holds. Strength 7 against defender Force 3 needs an attacking Force of 11. Published only for the current battlefield, like the other values.

## Reading for Mendon

"If both players pass" means the current Force totals with no further Battle actions, as at resolution; a later Force change updates the flag, so it is live rather than a prediction. "The active player's placard" was read as the **viewing** player's (bottom), the opponent's on top.

## Review fixtures

The review scenario list adds "Attacker force far ahead", "Armor off · exactly short" and "Armor on · province falls". The **Toggle armor** button attaches or removes Light Armor on the Crane Personality; Force changes by 3 and the katana appears and disappears. It reloads the fixture; Attachments cannot be played during a battle.

## Evidence limits

Native Godot checks ran on Windows only. No Mac preview and no Windows graphical playability review were done. Maneuver-phase placards share the battle-phase code path but only engaged scenes were captured.
