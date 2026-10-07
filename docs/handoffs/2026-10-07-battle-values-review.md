# Battle value rails — review evidence

Issue [#709](https://github.com/alfredoh7/l5r-emperor/issues/709), candidate [#710](https://github.com/alfredoh7/l5r-emperor/pull/710). Base `a36b9200e93ab4c20a8ccb3abb43481eb153beb6`; initial ownership head `57d078db8229112e2fe6e7a2f493526f1e9a5059` plus implementation working files. The four governance-lite reviews were independent, read-only source reviews at that checkpoint, not human rules acceptance. Later corrections were verified by the primary agent and focused checks.

## Approved visual and public contract

Alfredo chose A's rails and C's Force hierarchy, with both rails inside the blood border. Actual approved A pixels are retained under `assets/ui/battle-values/`, with exact crop coordinates and hashes in `provenance.json`; only blank center runs stretch. The C hierarchy uses an existing blank gold coin and live text. The court reserves separate card space above/below. Reduced motion, full scans, physical attachments and normal table zoom remain intact.

| New view member | Authority and viewers | Client handling |
| --- | --- | --- |
| `BattlefieldView.Values` | Optional Core projection, identical public summary for either authenticated seat; current engaged field only, absent after match end | Rails absent for legacy/omitted values; no draft-assignment totals |
| `AttackerId`, `DefenderId` | Core battle roles, including existing aside-battle Attacker selection | Role labels follow IDs; vertical placement follows viewer |
| `AttackerForce`, `DefenderForce` | Existing resolution `ArmyForces`, including its registered terms, redirects and army bonuses | Displays supplied integers, never adds card Force in UI |
| `AttackerUnits`, `DefenderUnits` | Existing Personality-only `Army` enumeration, including bowed roots | Smaller Units count; attachments are not additional units |
| `ProvinceStrength` | Existing `EffectiveProvinceStrength` and authoritative province association | Badge beside actual defender province; absent for open/destroyed province |

No new choice, command, payment/target payload, persisted state or production card/seed/hash meaning. Optional wire additions need no ContentVersion/storage migration. Prepared local fixtures deliberately use `FromTestState` / `test-only`; their unsupported replay export is disabled with an explanation. Ordinary practice replay export remains available. Normal room creation never offers these review fixtures.

## Four review roles and reconciled findings

- **Rules versus core:** no new mechanics defect identified. Traced existing battle-role, force-resolution and province-association helpers. A historical September 8 bowed-host decision differs from rules-lead #216's September 13 correction; current Core follows independent ready Follower contribution. This feature reuses the later implementation and does not establish a new ruling.
- **Hidden information:** no actionable leak identified. Only scalar public totals/roles are added; existing private hand, choice, AI, reconnect and recovery boundaries remain unchanged.
- **Negation and edge cases:** found a summary/strength badge surviving concession. Fixed by the Core `WinnerId` gate and client battle-active guard; new Core, session and native concession checks cover it. Existing terms/negation helpers remain authoritative.
- **Client contract:** found export of an unreplayable prepared fixture. Fixed in session and HUD; transport regression checks review export is unavailable, then normal Practice export is restored. Added real protocol-envelope round trips for both seats and legacy omission.

The rules, privacy and edge reviewers recorded mandatory governance/context reads, relevant rules/ruling decisions, the new Core/client/tests and focused large-file excerpts. Privacy additionally read protocol, room manager/store/server and AI projection paths. Rules inspected fixture definition/provenance records and real scans for Hakuseki, Dainagon, Shika and Veteran Skirmishers. Contract review traced fixture creation, replay creation/export and client handling; its full per-file coverage list is not retained in this consolidated record. No reviewer read all large engine/card catalogues or every unchanged stat hook; no reviewer performed Windows graphics or expert human playtesting. Native artwork and execution were inspected separately by the primary agent.

Remaining focused coverage gaps: hidden-state substitution and an engaged authenticated reconnect dedicated to this new summary; explicit Attacker 1/aside-battle and associated additional-front summary fixtures; individual Elite/Deep Roots/negation/control-transfer/last-redirect-beneficiary cases. Existing helper suites cover many of these calculations; reuse does not turn those into new direct-summary regressions. These are disclosed coverage limits, not reproduced defects or new rulings.

## Actual validation

- `bash tools/check.sh --skip-client`: all requested checks passed. This full run preceded the final concession guard; subsequent focused Core and transport runs cover that correction.
- Focused Core `battle-values` suite: 6/6 pass, including concession, exact resolution totals, bowed ready Followers, redirects/bonuses, other-court omission, empty/open/destroyed strength, legacy wire and opponent-hand omission.
- Client transport executable: 19/19 pass, including protocol JSON and fixture export/normal-practice recovery.
- `bash tools/check-battle-values-ui.sh`: native Mac compilation and checks; six prepared real-engine scenes from both seats, border/card/attachment clearance, texture lifetime after GC, review controls, zoom/card input, Force-changing action, concession and two resized/reduced-motion cases. Evidence under ignored `artifacts/battle-values-review/`.
- `bash tools/check-battle-presentation.sh`: eight existing native layout/assignment/defense/casualty/fall cases pass at 1920×1080 and 1280×800.
- `python3 -m unittest discover -s tools/previews -p 'test_*.py'`: 12/12 pass, including multipart assembly and companion validation.
- Clickable source-linked Review app generated and launched on Mac; prepared scenario visible. Native mouse selection of the bowed scenario verified through computer-use (synthetic PopupMenu keys were not a valid Mac input path). Latest app refreshed for this isolated checkout.

Windows export/runtime/launcher evidence is added with the versioned preview receipt. Windows CI is not graphical playtesting. Mendon's mechanics/playability review is still required, and this candidate must not merge automatically.

## Windows delivery candidate

Game source is `faa2613f9d6f49ed33d017095155176090b9c3a5`. Delivery tooling and a formatting-only fixture correction follow on this branch; they do not change the packaged game behavior. The strict Windows exporter completed its runtime, .NET assembly, resources and source-omission checks.

The exact complete preview ZIP is 2,598,982,649 bytes, above GitHub's per-asset limit. Two immutable ZIP assets retain every original byte: extract both into the same destination and click Start Review. The bundled Windows helper validates the parts and original archive, assembles the isolated payload once, then opens the prepared scene. `previews/published/battle-values.json` records both asset hashes plus the original archive's checksum. No build tools or runtime download are needed.

The private prerelease is staged with validation pending. Automated Windows CI must verify both hashes, real archive assembly, exported runtime, inner launchers and the actual outer launcher before the publisher sends the single authorized Mendon notification. Native Windows graphical playtesting and expert mechanics acceptance remain separate; no automatic merge.

The first delivery run (`37619224853`, tooling `4142d960`) verified the battle package's exact archive assembly, real exported executable and inner/outer launchers on Windows, including paths containing spaces, `&` and `!`. Eight packages passed before the sequential 16-receipt job hit its 15-minute deadline while validating historical previews; no battle-package defect was reported. The workflow now distributes every receipt across four parallel Windows groups, with an aggregate success check requiring all four. Full rerun is pending; the publisher still requires that aggregate check before notification.
