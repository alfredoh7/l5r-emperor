# Movement preview projection

Status: implemented proposal for Mendon’s shared-contract review in draft PR #404 / issue #346. No new movement ruling and no merge authorization.

The player must see every proposed mover without client code interpreting card names or rules text. `ActionOption.Movement` is optional `MoveDestinationView(DefenderId, FixedMoverIds, TargetMoves)`: the engine names fixed source/performer units, identifies the owner of destination provinces, and tells the client whether to add the selected `Command.TargetId` as a mover. Unit attachments use the existing stack preview; their actual movement remains engine-authoritative.

`ActionOption.Provinces` remains the exact legal destination list. `GameCommand.Province` is submitted with the selected target; `Assignments` remains null. The UI deliberately starts with no selected destination and requires confirmation. Source/performer/target shapes come from `MoveSpec`, while Hawado’s existing special case projects herself only: her discarded Dynasty target is an action cost. This fixes Hawado’s missing `Provinces` projection without changing its validation or execution.

Movement and Attack province targeting are distinguished by the optional movement view, never by the presence of `Provinces` alone. Actual arrival/negation is observed in public state and events; proposed arrows are removed when the draft ends, and do not predict reactions. Existing normal/reduced-motion animation reads actual snapshots.

The server `AvailableAction` envelope forwards the new optional view. During this work the existing `AlternativeCostView` was found to be dropped by that projection; it is now forwarded as well, repairing the earlier #339 network chooser contract. A server-envelope/client-deserialization regression pins both and the outgoing Province/TargetId command. Older client payloads lacking Movement still deserialize with null. New UI and server must be used together to offer the new movement picker.

Affected: Core Models/MoveDestinations/CardAbilities (projection only), Server Protocol, client Movement/sidebar/table layout and intent rendering, and focused Core/transport/native probes. Existing movement tests plus the Hawado/Attack distinction case and the full deterministic suite pass. Native desktop input acceptance remains separate from rendering and engine tests.
