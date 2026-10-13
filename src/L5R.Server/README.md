# Private match server

The ASP.NET Core server owns the rules engine and all hidden information. Each room has two persistent seats, player `0` and player `1`. The starter currently assigns Crane to player 0 and Lion to player 1; clients should read clan labels from each player's view. It runs the foundation starter scenario, whose content limitations are included in every match view. It does not claim full Emperor card support.

## Run locally

From the repository root, using its pinned .NET 10 SDK:

```sh
dotnet run --project src/L5R.Server
```

The default URL is `http://127.0.0.1:5210`. Set `ASPNETCORE_URLS` to change binding and `L5R__StoragePath` to change the room storage directory. HTTP is intended for trusted local development. The server does not provision infrastructure or a domain.

## Client protocol

All JSON uses camelCase. `GET /health` returns `status`, `protocolVersion`, and `contentVersion`.

1. `POST /rooms` creates a room and reserves player 0. No request body is required.
2. Share the returned eight-character `roomCode`. `POST /rooms/{roomCode}/join` reserves player 1; further joins receive HTTP 409.
3. Each reservation returns `{roomCode, playerId, seatToken, protocolVersion, contentVersion}`. Keep that seat's token locally for reconnection. The server stores only its SHA-256 hash. Do not share tokens or put them in URLs.
4. Connect a WebSocket to `/rooms/{roomCode}/play` with `Authorization: Bearer <seatToken>`.
5. Send the hello below within ten seconds, using the versions returned by `/health` or the reservation.

```json
{"type":"hello","protocolVersion":1,"contentVersion":"emperor-lion-crane-0.4.0"}
```

The server then sends an independent state to each connected player:

```text
{type:"state", roomCode, playerId, sequence, view, actions,
 playersConnected:[boolean,boolean], protocolVersion, contentVersion}
```

`view` is the engine's redacted `MatchView`, including public events and implementation limitations. `actions` contains contextual actions, payment quotes and suggested commands. An action's `command` is already safe to send inside the envelope below: it has no `playerId` or `expectedVersion` fields. It may be null when the interface must collect a target or another selection. Use `sequence` from the most recent state; it is the authoritative game version.

```json
{
  "type":"command",
  "commandId":"0b4c3d57-0b3c-4f8d-ad99-9f0de165ea71",
  "expectedVersion":0,
  "command":{"type":"pass"}
}
```

Supported command fields are `type`, `cardId`, `sourceIds`, `province`, `clanDiscount`, `clanAllegiance`, `assignments`, `cardIds`, and `targetId`. Exact legal combinations come from `actions` and the core rules engine. Unknown fields are rejected. The authenticated seat supplies the actor; clients cannot nominate another player.

Each syntactically valid envelope receives:

```text
{type:"commandResult", commandId, accepted, duplicate, sequence,
 error:null | {code,message}}
```

An accepted command is persisted before acknowledgment and followed by updated state messages for both connected seats. Rejected commands leave game state unchanged. `stale_version` is followed by the latest state; refresh the UI and request a new user choice. Do not automatically replay a stale intent using a new version.

If a connection drops before its acknowledgment, resend exactly the same envelope and command UUID after reconnecting. A recognized accepted retry returns `accepted:true, duplicate:true`; its reply sequence is the version at which that command was originally accepted, followed by a current state message. Reusing that UUID with another payload returns `command_id_reused`. The latest 1,024 accepted receipts are retained per room; older retries still cannot replay a change because their expected version is stale.

Malformed JSON and handshake errors use `{type:"error", error:{code,message}, protocolVersion, contentVersion}`. Messages are limited to 32 KiB. A new connection with the same seat token replaces the previous socket. Connection/presence changes do not increment the game version. Room creation and joining are limited to 20 requests per minute per direct client IP.

## Persistence and deployment

Each private `*.room.json` contains an atomic recovery snapshot with the full accepted-command replay journal, token hashes, and recent command receipts. It is never available through an HTTP route. Secret seeds, deck order, private cards, and replay records never appear in public state envelopes. Unix-created directories and room files are restricted to the server user.

Writes use a temporary file, disk flush, and atomic replacement. The engine commits in memory only after persistence succeeds. Startup replays every room and fails explicitly on incompatible or malformed storage. This prototype reconstructs a trial engine from its journal before every command to preserve transactional behavior; add engine snapshots when match length and profiling justify the optimization. Run one server process per storage directory; multi-process clustering is not implemented.

The deployment examples provide a non-root container, persistent Docker volume, and a localhost-only published port:

```sh
docker compose -f deploy/compose.yaml up --build -d
```

For private internet play, place a TLS reverse proxy in front of port 5210. `deploy/Caddyfile.example` shows the WebSocket-compatible route; replace its example hostname with the actual DNS name. Desktop clients then use `https://` and `wss://`. This is deployment configuration, not a live deployment. Room codes invite one guest, and possession of a seat token is the authentication mechanism; there are no public accounts or matchmaking services. Back up the private room volume before changing content versions.

## Verification

```sh
dotnet run --project tests/L5R.Server.Tests
```

The dependency-free integration executable starts real ephemeral Kestrel servers and two WebSocket clients. It verifies private views, token-bound actors, incompatible version rejection, duplicate and stale commands, server restart/reconnection, concurrent changes, persistence rollback, malformed envelopes, and message-size limits.

## Published Lion and Crane profile

`POST /rooms?scenario=lion-crane-v1` creates the published 40/40 match. Omitting the query retains the training default. Unknown scenarios fail before reservation or persistence. Seat 0 is Crane; seat 1 is Lion, who takes the first turn. The selected scenario is stored in the replay.

A `useAbility` command includes `cardId` (absent for rulebook Favor actions), `abilityId`, optional `performerId`, `targetId`, `cardIds` (ordered Border Keep selection), and payment `sourceIds`. `chooseShinden` includes `choice: "bow"` or `"honor"`; only the targeted Personality's controller may answer. Actor and expected version remain bound by the authenticated envelope. Favor ownership and straightening restrictions appear in the projected view.

Content version is `emperor-lion-crane-0.4.0`. Replays and rooms saved under another content version are deliberately rejected as incompatible: the server logs a warning, skips those room files (they stay on disk, untouched) and starts without them. There is no silent migration of saved match semantics.
