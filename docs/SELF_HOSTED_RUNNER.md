# Local macOS Actions runner

Issue: https://github.com/alfredoh7/l5r-emperor/issues/100

The macOS row of Desktop foundation checks uses the repository-scoped runner
`alfredo-mac-studio`, labelled `self-hosted`, `macOS`, `ARM64`, `l5r-mac-studio`.
The Windows row and Linux collaboration workflows remain GitHub-hosted.
One runner executes one job at a time. Newer desktop runs supersede older runs
on the same PR/ref. Fork PRs do not execute the desktop workflow on this Mac.

## Host and operation

- Host: Alfredo’s M1 Max Mac Studio, 32 GB RAM, macOS 26.5.2 at installation.
- Runner: `/Users/alfredo/.local/share/l5r-actions-runner`.
- Jobs use its separate `_work` checkout; they never build in the shared development worktree.
- The runner runs as Alfredo, without sudo. This separates checkouts, not OS permissions:
  trusted repository workflow code has the account’s file access. Do not enable this
  runner for public/untrusted contributions. Checkout does not retain its GitHub token.
- A per-user launchd service starts after Alfredo logs in and restarts the runner if it exits.
  After reboot/FileVault unlock, log in before expecting jobs. Logging out, shutting down,
  or manually sleeping the Mac makes jobs wait until it returns.
- While the service runs, `caffeinate -i` prevents idle system sleep but permits display sleep.
  No global power settings are changed. Stop the service to restore normal idle sleep.
- The service uses reduced CPU scheduling priority (nice 10). This is not a CPU/RAM cap.
- Python and tool caches reside under the runner directory. Each job installs/validates
  the pinned project tools and runs the same full check script as hosted macOS.
- Runner updates remain automatic. Credentials and binaries are outside Git.

## Controls

From Terminal:

```sh
cd /Users/alfredo/.local/share/l5r-actions-runner
./svc.sh status
./svc.sh stop
./svc.sh start
```

Check GitHub Settings → Actions → Runners for Online/Idle/Active status.
Logs are in `_diag` and the service log paths in the launchd plist.
Do not share raw diagnostics without checking for sensitive information.

## Recovery

If offline, start the service and check network access. Jobs queue rather than
silently falling back to paid macOS. A temporary hosted fallback requires a reviewed
workflow change: replace the macOS row’s runner labels with `[macos-14]`.
To remove: stop/uninstall the service, then remove this runner through repository
Settings → Actions → Runners before deleting its local directory.

CI is compilation and headless import/smoke validation; it does not substitute for
native graphical Mac/Windows playtesting. Running on macOS 26 also changes OS coverage
from the former hosted macOS 14 environment.
