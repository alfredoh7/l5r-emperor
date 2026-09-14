# Art production with Meshy

Accepted by Alfredo on 2026-09-14 after reviewing the supplied Meshy lantern. This is the default route for detailed 3D props, beginning with the duel arena in [#205](https://github.com/alfredoh7/l5r-emperor/issues/205). It supersedes the temporary manual-lantern experiment. Mechanics remain with Mendon.

**External ChatGPT image generation → Alfredo's image approval → Blender layout → quick Godot camera check → Meshy props → Blender cleanup and assembly → Godot presentation → compare and iterate.**

Codex supplies prompts; Alfredo generates and approves the source images using his chosen ChatGPT image model. Do not substitute Codex imagegen. Once an asset task and its reference are approved, the agent handles Meshy submission, progress, downloads and Blender/Godot iteration directly through the configured API. The user does not need to transfer exports manually or repeat the access setup. This standing workflow is not a request to generate a batch of unspecified assets.

## Split the work by asset

| Part | Route |
|---|---|
| Composition, camera, card clearance and scale | Simple editable Blender layout, checked early with the actual Godot card camera |
| Flat mat, painted ornament and other approved surface art | Use the actual approved pixels with correct proportions and UVs |
| Rollers, plinth, beams, rail and shoji/architectural frames | Direct Blender geometry |
| Stone lanterns, garden rocks, pine trees and decorative props | Approved individual reference → Meshy → Blender cleanup |
| Light sources, moving shadows, mist, sound and interaction | Native Godot, with reduced-motion and gameplay checks |

Generate individual props, not a whole scene with inseparable scenery, lighting or card slots. Use clear neutral references with the complete silhouette and base visible. Add separate consistent views when an asset needs them; review those views before using a multi-image route. A generated model is an interpretation of the references, not a measured scan.

## Direct access

Run from the task checkout. `tools/meshy.py` wraps the [official Meshy CLI](https://github.com/meshy-dev/meshy-cli), pinned to `@meshy-ai/cli@0.3.0`. Node.js 24+ and npm are required; dependencies live in ignored `.tools/meshy`, separate from the game build. A fresh checkout needs only the first command; this Mac's current task checkout is already installed.

```sh
python3 tools/meshy.py install
python3 tools/meshy.py balance
python3 tools/meshy.py resources
python3 tools/meshy.py image-to-3d create --help
```

On Alfredo's Mac the key is stored in the login Keychain as a generic password with service `com.l5r-emperor.meshy-api` and account `alfredoh7`. The wrapper reads it in memory and passes it to the CLI through the child process environment, never command-line arguments. It uses the production Meshy API endpoints, rejects host/key overrides and redacts the key from CLI output. Raw `api` and `auth` commands are intentionally outside this wrapper; normal asset commands and `balance` work directly.

An explicitly configured private `MESHY_API_KEY` environment variable takes precedence, for another host or account. Other contributors supply their own authorized credential; the key is not shared through Git. For rotation, update the existing Keychain item's password privately, then run `balance`. Do not paste a key into shell arguments, checked-in files, issue/PR bodies or tool output. Do not reuse the consumed one-time link. No credential file or secret-retrieval script is needed for routine use.

Setup was verified on 2026-09-14 by an authenticated [balance lookup](https://docs.meshy.ai/en/api/balance). A local generation dry run reported zero requests. No new model was submitted, and the existing lantern's provenance remains the files supplied by Alfredo. Paid submission, retrieval and import of a newly generated API asset will be verified during the next approved prop task; authentication alone is not that end-to-end check.

Credential transport, redaction, endpoint restrictions and dry-run behavior have isolated tests: `python3 -m unittest discover -s tools -p test_meshy.py -v`. They use fixtures and mocks, need no Meshy key or Node installation, and run as part of `tools/check.sh`.

## Submit, retrieve and preserve

Before generating, check balance and inspect the plan. CLI estimates are advisory; verify the [current API pricing](https://docs.meshy.ai/en/api/pricing) for the exact model and options. Prefer one prop and one candidate first. Do not silently start a batch or add paid enhancement/remesh passes. Continue authorized work without asking for the same image or task approval again.

```sh
# This is local planning only; it does not submit or spend credits.
python3 tools/meshy.py make assets/ui/duel-arena/approved-stone-lantern.png --dry-run
```

For a future approved prop, use the resource command so settings and the local operation identifier are explicit. This example is a template: replace the reference and revision name with the approved asset. Do not rerun generation for the already accepted lantern.

```sh
mkdir -p artifacts/meshy/duel-pine-v1
python3 tools/meshy.py image-to-3d create \
  --image-url assets/ui/duel-arena/approved-pine.png \
  --model-type standard --should-texture true --enable-pbr true \
  --texture-resolution 4k --target-formats glb \
  --operation-id duel-pine-v1 --async \
  --save-json artifacts/meshy/duel-pine-v1/submission.json

# Read the returned task ID; querying status does not submit another task.
python3 tools/meshy.py image-to-3d get TASK_ID

# After status is SUCCEEDED, download promptly and preserve the task response.
python3 tools/meshy.py download --resource image-to-3d --task-id TASK_ID \
  --all --output-dir artifacts/meshy/duel-pine-v1/download \
  --save-json artifacts/meshy/duel-pine-v1/task.json
```

The baseline favors textured standard geometry with PBR maps; simplify after inspecting the silhouette at the gameplay camera. It does not claim to reproduce the unknown website settings used for the supplied lantern. Reassess model/options per asset rather than chasing the largest file or cheapest mesh. The shorter `make approved.png --max-credits N --async` command also supports an estimate ceiling, but it selects its own defaults; inspect its dry run and keep the returned operation/task IDs.

Use `--async` to return after submission; check status at reasonable intervals while continuing independent work. The CLI journals submissions under its local config directory (`~/.config/meshy/operations` by default). A resource command's `--operation-id` deduplicates against that local record; it is not a server idempotency guarantee. If a response is lost or marked `submission_unknown`, reconcile with the journal and `image-to-3d list/get`. Never blindly repeat a paid create or delete the journal to force a retry.

Keep raw responses, temporary downloads and rejected variants in ignored `artifacts/meshy/<asset-revision>/`. Preserve accepted originals separately from cleanup output. For each accepted asset, commit the reference, editable source, cleaned export and a sanitized provenance receipt with:

- Asset/revision, approval reference, source-image SHA-256 and Meshy task ID.
- Submission time, CLI version, exact generation settings and returned model/version when available.
- Estimated versus observed credits, clearly distinguished; another account task may affect a balance delta.
- Downloaded filenames and SHA-256 values, source and cleaned triangle counts, texture sizes and cleanup notes.

Do not commit bearer credentials, base64 request payloads or signed download URLs. Task IDs/settings/hashes provide durable provenance after remote download links expire. Follow repository limits for bulk assets; normal game builds must not require Meshy access.

## Blender and Godot acceptance

Inspect the downloaded original in Blender before modifying it. Check silhouette, hollow regions, disconnected parts, UVs and base-color/normal/roughness maps. Establish dimensions, upright orientation and a ground-contact pivot; preserve the original before repair. The supplied lantern needed its generated solid window panels opened, so an attractive preview is not proof of usable geometry. Optimize unnecessary detail without replacing the approved material artwork. These stages follow [Meshy's Blender cleanup guidance](https://help.meshy.ai/en/articles/16100257-meshy-to-blender-cleanup-workflow).

Assemble the cleaned prop in the editable scene and export through [Godot's glTF/GLB pipeline](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/available_formats.html). Compare the native asset beside its source at close range and from the real gameplay camera, with actual cards, attachments, sidebar, zoom and reduced motion. Check both geometry and texture fidelity; record any deliberate changes. Approving a source image does not pre-approve its reconstructed model or final scene.

For the current duel arena, retain `duel-arena-v3.blend` and the supplied Meshy lantern. Garden rocks and pine silhouettes are the next candidate props after their image references are approved. Final scene atmosphere and live duel integration remain separate work.
