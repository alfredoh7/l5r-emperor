# Online play: internet table server

Reference plan for hosting the table server on the internet so game clients can play anyone, anywhere, over secure connections. **Status: planned, not built or deployed.** [PROJECT_STATUS.md](PROJECT_STATUS.md) records that no internet match server exists, and issue #73 (a real Mac–Windows match including reconnection) has not passed. This document describes the target behavior, what already exists, what must be built, and what it costs. Authoritative rules, hidden information and persistence stay in `L5R.Core` and `src/L5R.Server`; the client only submits intentions (see AGENTS.md product boundaries).

## Target behavior

1. **Public queue by default.** A player who asks for a game is paired with a waiting player. If nobody is waiting, the server opens a public room and the player waits in it. No account is needed.
2. **Private by password.** The creator can make a room private by setting a password. A private room is not offered in the queue. A guest joins it with the room code **and** the password. Passwords are stored only as a salted slow hash (Argon2id or PBKDF2), never logged, and join attempts are throttled.
3. **Secure transport.** Clients use `https://` for room calls and `wss://` for play. TLS ends at a reverse proxy in front of the container.
4. **Reconnection window of 15 minutes.** A player who drops can resume the same seat, with the same seat token, for 15 minutes. If the seat is still absent at 15 minutes, the session closes **with no result**: no winner, no loss, no rating or record change.
5. **Audit logging.** Every room connection and every action is logged (see Logging and retention).

### Session closing rules

| Situation | What happens |
|---|---|
| A player is absent for 15 minutes (a game has been played or is in progress) | Session closes with no result. The room's action log is kept under the retention rule below. |
| Only one player ever joined (nobody took the second seat within the waiting window) | Session closes. The room's logs are discarded. |
| The match ends normally | Result recorded. The log is kept under the retention rule. |

"Waiting window" for an unfilled room: use the same 15 minutes unless playtesting shows a different value is better. The 15-minute timers count real time while a seat has no live connection; reconnecting resets the clock for that seat.

## What already exists

`src/L5R.Server` (see its [README](../src/L5R.Server/README.md)) already provides:

- An ASP.NET Core server that owns the rules engine and per-seat hidden information.
- Room creation and joining (`POST /rooms`, `POST /rooms/{code}/join`) with an eight-character room code.
- Seat tokens (bearer token on the WebSocket; only a SHA-256 hash is stored) and reconnection by presenting the same token. A new connection replaces the old socket.
- Command de-duplication by command ID, so a retry after a dropped acknowledgment is safe.
- A write-ahead journal: each accepted command is persisted atomically before acknowledgment, and rooms are replayed on startup.
- Container files: `deploy/compose.yaml` (non-root user, persistent volume, port published to localhost only) and `deploy/Caddyfile.example` (TLS reverse proxy that passes WebSockets).
- A rate limit of 20 room creates or joins per minute per direct client IP.

Limits to keep in mind: one server process per storage directory (no clustering), rooms saved under an older content version are skipped at startup, and the engine rebuilds a match from its journal before every command (add engine snapshots if long games lag).

## What must be built

### Server

- **Public queue.** Rooms default to public. Add a queue that pairs a requesting player with a waiting public room (or creates one). Decide pairing keys, at least scenario/content version, so players are never matched into incompatible games. Do not list private rooms.
- **Private rooms.** Optional password at creation; required at join. Hash it, throttle attempts, and never write it to storage in plain text or to logs.
- **15-minute reconnect timer.** Track per-seat absence. On expiry, close the session with no result and notify any connected player. Reconnecting before expiry restores the seat, the current state and any suspended choice.
- **Room lifecycle and cleanup.** Close and remove rooms that expire unfilled, finish, or are abandoned, applying the closing rules above so disk does not fill with dead rooms.
- **Real client IP.** Behind the proxy every request appears to come from the proxy. Enable forwarded-header handling and trust it only from the proxy, otherwise the rate limiter and the IP log are wrong or spoofable.
- **Abuse controls.** Cap open rooms per IP and globally, throttle join and password attempts, and add an IP deny list checked on create, join and connect (see Banning).
- **Logging and retention.** See below.

### Client

- A configurable server address, defaulting to the production domain.
- "Find a game" (public queue) and "Create private game" (password) entry points, plus join-by-code with password.
- Local storage of the seat token.
- Automatic reconnect with a visible countdown to the 15-minute limit, and a clear "session closed, no result" outcome.
- Preserve the pinned Lion/Crane profile and the existing local and practice modes.

### Infrastructure (owner action)

- Rent a host (a small Linux VPS with Docker is enough), buy a domain and point DNS at the host.
- Open ports 80 and 443 only. Keep the game port bound to localhost.
- Run Caddy (or another TLS proxy) in front; Let's Encrypt certificates renew automatically.
- Back up the room storage volume and the log volume.
- Add an uptime monitor on `/health`.

## Logging and retention

**What is logged.** One structured line (JSON) per event, with UTC timestamp, room code, seat, event type, client IP and, for commands, the command ID and outcome. Events:

- room created, public or private (never the password), joined, queue paired
- socket connected, disconnected, replaced by a new session, reconnected
- every command, accepted or rejected, with the rejection code
- timer expiry, session closed (with the reason), and bans applied

The accepted-command journal already allows exact replay of a match; the audit log adds connection events, rejections and IPs. Neither may contain hidden information: no deck order, hands, private cards or secret seeds.

**IP addresses.** Client IPs are logged so an address can be banned for abuse or misuse. Because strangers connect, treat these as personal data: tell players in a short privacy notice that IPs are logged for abuse prevention, restrict access to the logs, and do not use them for anything else.

**Retention.**

- Logs are kept for **one month**.
- If the host has a storage limit, keep logs up to that limit and delete the **oldest first** to make room for the newest. Configure a maximum log size and prune on a schedule and whenever the limit is reached.
- A room where a game was played keeps its action log until no further game has been played in it, and then it ages out under the one-month rule above.
- A room where only one player ever joined is discarded entirely when it closes, including its logs.
- Write logs as daily rotated files so oldest-first deletion is simple and a day can be removed whole.

Not yet decided: events that belong to no room (rate-limit hits, failed password attempts, banned-IP rejections) cannot follow the per-room rules. Suggested handling is to keep them in a separate server-wide abuse log under the same one-month and size rules, so a repeat offender can still be identified.

## Banning

Because IPs are logged, an address that abuses the service can be banned. Keep a deny list (file or config reloaded without a restart) that the server checks before creating, joining or connecting. Log each ban and each blocked attempt. Remember that IPs are shared (NAT, universities, mobile carriers), so prefer time-limited bans and review them. The proxy can also block at the edge.

## Cost estimate

Approximate; confirm current prices before buying.

| Item | Cost |
|---|---|
| Small VPS (1–2 vCPU, 1–2 GB RAM) | about $4–12/month |
| Domain | about $10–20/year |
| TLS certificate | free (Let's Encrypt via Caddy) |
| Bandwidth | negligible (small JSON messages) |
| Log and room storage | negligible; a few GB at most, bounded by the retention limit |
| Uptime monitor | free tier |

Expect roughly **$5–15 per month**. A managed container platform (Fly.io, Railway, Azure Container Apps) costs about $5–25 per month but makes the persistent volume and WebSocket behavior fiddlier. A large cloud design with a load balancer ($30–100+ per month) is not needed. One small host should handle dozens of concurrent matches. Cost grows only if concurrency grows; the likely first bottleneck is CPU from journal replay, fixed by engine snapshots rather than a bigger plan. The single-process design rules out autoscaling and scale-to-zero platforms.

## Suggested order of work

1. Server branch: reconnect timer and closing rules, audit log with IPs and retention pruning, forwarded-IP handling. This is testable locally with no hosting spend.
2. Private-room passwords and the public queue, with abuse caps and the deny list.
3. Client screens, server address setting and reconnect countdown.
4. Deploy: VPS, domain, Caddy, compose, backups, monitor.
5. Real two-machine internet match (issue #73), including a dropped connection mid-turn and the 14:59 / 15:01 boundary. Run a load test with a few dozen concurrent rooms.
6. Upgrade procedure: rooms saved under an older content version are skipped on startup, so schedule deploys when no games are active or add a drain step.

## Tests to write

- Reconnect at 14:59 resumes; absence at 15:01 closes the session with no result.
- A one-player room closes at expiry and leaves no log behind; a played room keeps its log.
- Retention: a file older than a month is deleted; a size-limit breach deletes the oldest files first.
- Public queue pairs two waiting players, never pairs incompatible content, and never lists private rooms.
- Wrong or missing password is rejected and throttled, and the password never appears in storage or logs.
- A banned IP is refused at create, join and connect.
- Logs contain no hidden information.
- The forwarded IP is honored from the proxy and ignored from anyone else.
