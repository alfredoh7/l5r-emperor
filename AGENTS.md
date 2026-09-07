# Instructions for coding agents

- Read `AI_HANDOFF.md`, `docs/PROJECT_STATUS.md`, and the relevant GitHub issue before continuing. The user's latest instructions take precedence over this repository guidance.
- Keep rules authority in `L5R.Core`/the server. Never implement authoritative bookkeeping or hidden state in the client.
- Preserve the pinned Emperor Lion/Crane profile; cite the selected printing/rules source for behavior changes. Reference catalogue cards are not automatically playable.
- Keep the current game UI baseline. Do not use the Impeccable skill for this project; the user explicitly rejected it. Match full card faces, physical table relationships, header font, and reduced-motion behavior.
- Use a separate branch and, for concurrent sessions, a separate worktree/clone. Inspect local changes; do not discard another contributor's work.
- Follow `CONTRIBUTING.md`: issues capture tasks, PRs capture reviewable changes, and `docs/PROJECT_STATUS.md` captures the shared milestone. Record actual test results and any incomplete work.
- Run focused checks appropriate to the change and full deterministic checks for rules/network changes. Visually inspect UI changes in the real game. Never equate export validation with native Windows or human playtesting.
- Keep credentials, private room storage, generated caches and binaries out of Git. Publish game packages and bulk research artwork through private GitHub Releases.
- Do not rewrite shared history, change repository visibility, or introduce unrelated infrastructure as a routine implementation choice.
