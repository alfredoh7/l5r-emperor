# Archive

Retired features, kept out of the way of development but not lost. Each folder has its own note saying what was removed, what stayed, and how to restore it. Nothing here is compiled, run by CI or
read by a test.

## Procedure for archiving a feature

1. **Tag the last commit that has it**, before the removal PR: `git tag -a archive/<name> -m "<name> as of <date>"` and `git push origin archive/<name>`. The tag is the permanent copy; it needs no branch.
2. **Remove it from the app in one PR**: the menu entry, the host and its capture probes and check scripts, with the tests that exist only for it. Keep anything the shipped game still uses, even if the feature
   introduced it (shared screens, engine logic, prepared-position fixtures that other tests and reviews build on), and say which parts stayed.
3. **Move the written record here** (`docs/archive/<name>/`): design docs and, if large, the screenshot evidence, with their links fixed. Docs that describe design shared with the live game stay where they are.
4. **Write `docs/archive/<name>/README.md`**: the date and issue, the tag, what was removed, what stayed and why, what validation covered the shared parts, and the restore steps.
5. **Prove the live game is unaffected** before merging: the engine files are untouched (`git diff` shows none), the Core and AI suites pass, a seeded bot batch gives identical action lists, and the native checks
   that cover the shared parts give the same results as before the removal.

## Restoring

`git switch -c restore/<name> archive/<name>` gives the whole tree as it was. To bring pieces back into current code, `git checkout archive/<name> -- <paths>` and reconcile; or revert the removal PR if nothing
has changed around it. Check the tag still exists first: `git tag -l 'archive/*'`.

## Archived

| Feature | Archived | Tag | Note |
|---|---|---|---|
| Duel Demo (Play → Duel Demo sandbox) | 2026-10-13, #926 | `archive/duel-demo` | [duel-demo/README.md](duel-demo/README.md) |
