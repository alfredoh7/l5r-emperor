---
name: art-pack-release
description: Build, verify, and when requested publish a versioned private L5R catalogue artwork pack with matching manifests, release receipts, coverage, and client installation checks.
---

# Release a catalogue art pack

This is the publisher workflow for catalogue card scans, not UI concept art or a desktop game release. Follow AGENTS.md; read docs/CATALOGUE_VIEWER.md's publisher instructions and docs/PITFALLS.md's scan/cache/coverage notes. A request to publish/release authorizes that action; a build-only request does not. Preserve existing private releases and repository visibility.

## Build a complete candidate

Inspect content/catalogue-art-pack.json, src/L5R.Client/Content/catalogue-art.json, catalogue-printings.json and every published scan. Include newly merged main content so concurrent card passes cannot make the replacement pack stale. Record the old tag/hash/image count and choose a new versioned tag; never overwrite an old asset/tag to make old receipts resolve to new bytes.

Use the maintained publisher, which verifies original receipts and encodes WebP quality 85:

```sh
python tools/content/build_catalogue_art.py --cache <cache> --output <output>/L5R-Catalogue-WebP85.zip --source <original-checkout> --tag <new-tag> --cwebp <encoder>
```

Pillow is pinned in tools/content/requirements.txt; cwebp must be available. --source can repeat. Keep cache/output off OneDrive or other sync folders on Windows; reuse verified cache after interruption. The builder writes both generated JSON metadata files, preserves source dimensions, and fails incomplete packs. Do not hand-edit manifests/receipts or alter original scan hashes. Keep the ZIP and cache out of Git.

Validate the local ZIP with `python tools/ensure_catalogue_art.py --archive <output>/L5R-Catalogue-WebP85.zip`, then `python tools/ensure_catalogue_art.py --verify-only`. Run content tests and appropriate client build/import checks. Compare archive SHA-256, byte size, manifest coverage and image count with the generated receipt. Read tools/ensure_catalogue_art.py to distinguish coverage validation from successful installation. Automated import is not native visual/playability acceptance.

## Publish when included in the request

Check the target tag/release first with gh release view; an existing release must be inspected rather than replaced. Recheck coverage against current main before tagging: a concurrent card pass may require a new candidate. Commit the verified source inputs and the two matching generated JSON metadata files so the release can target their exact source commit. Create a new draft release in the existing private repository targeting that commit, upload the named ZIP, and verify the uploaded asset's size and digest through the GitHub API (download/hash it if the API supplies no digest). Do not use --clobber. If upload fails, inspect the draft and resume only the missing asset instead of creating duplicate releases.

Publish the verified draft, then use the generated receipt to test a clean cached/download installation. If content subsequently changes, build a new version rather than changing the bytes under this tag. A publication request needs this verified usable release, not just a local ZIP. If publication was not requested, deliver the verified local candidate and state that it remains unpublished; do not solicit redundant approval for already authorized release work.

Finish the shared handoff/PR flow with old/new tag, source commit, image count, byte count, SHA-256, private release link, coverage/install/build results and any outstanding native review. Commit receipt/manifest changes, not the archive or credentials. Do not message another person unless the task or explicitly invoked project workflow authorizes it.
