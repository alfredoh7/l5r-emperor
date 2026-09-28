# Data-driven published waves (Rung 4a, issue #509)

Phase A replaces the three per-wave registrations with MSBuild and startup discovery.
The live Lion/Crane snapshot and frozen test-deck snapshot remain explicit. The frozen
snapshot contributes deck lists only, never a second set of catalogue definitions.

## Order and duplicate semantics

`CardCatalog.Build` previously concatenated synthetic fixtures, the live snapshot,
then 306 waves and called `ToDictionary(c => c.Id, StringComparer.Ordinal)`.
Duplicate IDs throw; there is no newest-wins override. ID lookups therefore do not
choose between printings. Enumeration does matter: client review probes use `First`
by name/type, and the dictionary retains insertion order on the pinned runtime.
This refactor preserves the entire prefix, including JSON card order inside each wave.

`PublishedWaveOrder.txt` is a frozen compatibility record, not a registration list.
Never append new waves to it. Existing waves keep their exact positions. Additional
waves follow the frozen prefix, sorted by type using ordinal comparison and then by
numeric wave number. The merge regression adds wave 10 before wave 2 to verify this.
Missing or duplicate legacy entries fail startup instead of silently dropping content.

## Resources and compatibility

The csproj embeds `*-wave-*.json`, excludes `*-sources.json`, and computes logical
names as `L5R.Published.%(Filename)%(Extension)`. For example,
`L5R.Published.PersonalityWave2.json` becomes
`L5R.Published.personality-wave-2.json`. All existing loader resource literals change
with it. The two explicit snapshot names stay unchanged. The published-count guard
already discovers embedded snapshots by profile, and manifests by file enumeration.

Existing named properties used by tests/client code remain. Legacy validation helpers
still execute, with internal-only dependencies private. Unused simple registrations
are removed. A startup cache ensures compatibility properties and discovery share the
same snapshot. New waves go through the common profile, implementation, duplicate-ID
and deck checks. No reflection or discovery is performed during gameplay.

## Contributor workflow

See the existing [content tooling recipe](../../tools/content/README.md#publishing-a-card-wave).
Package the reviewed JSON and provenance, add the wave's art, and pin its IDs with
`python tools/content/pin_wave_ids.py --wave <type>-wave-<N>`. Commit those files and
the packager; no csproj, loader, catalogue chain, legacy-order or test-list edit is
needed. A new mechanism still needs the current engine registrations until Phase B;
this PR changes no wire format, state model or client view.

## Exact catalogue-order mapping

Synthetic fixtures remain first, followed by the live snapshot. Positions below count
only wave slots; both old and new positions are one-based. Future waves start at 307.

| Wave | Old position | New position |
|---|---:|---:|
| `vanilla-wave-1` | 1 | 1 |
| `stronghold-wave-1` | 2 | 2 |
| `stronghold-wave-2` | 3 | 3 |
| `stronghold-wave-3` | 4 | 4 |
| `personality-wave-1` | 5 | 5 |
| `personality-wave-2` | 6 | 6 |
| `personality-wave-3` | 7 | 7 |
| `follower-wave-1` | 8 | 8 |
| `personality-wave-4` | 9 | 9 |
| `personality-wave-5` | 10 | 10 |
| `strategy-wave-1` | 11 | 11 |
| `follower-wave-2` | 12 | 12 |
| `follower-wave-3` | 13 | 13 |
| `follower-wave-4` | 14 | 14 |
| `follower-wave-5` | 15 | 15 |
| `item-wave-1` | 16 | 16 |
| `strategy-wave-2` | 17 | 17 |
| `personality-wave-6` | 18 | 18 |
| `follower-wave-6` | 19 | 19 |
| `personality-wave-7` | 20 | 20 |
| `personality-wave-8` | 21 | 21 |
| `strategy-wave-3` | 22 | 22 |
| `follower-wave-7` | 23 | 23 |
| `follower-wave-8` | 24 | 24 |
| `item-wave-2` | 25 | 25 |
| `strategy-wave-4` | 26 | 26 |
| `strategy-wave-5` | 27 | 27 |
| `personality-wave-9` | 28 | 28 |
| `personality-wave-10` | 29 | 29 |
| `personality-wave-11` | 30 | 30 |
| `personality-wave-12` | 31 | 31 |
| `personality-wave-13` | 32 | 32 |
| `ring-wave-1` | 33 | 33 |
| `personality-wave-14` | 34 | 34 |
| `follower-wave-9` | 35 | 35 |
| `personality-wave-15` | 36 | 36 |
| `personality-wave-16` | 37 | 37 |
| `event-wave-1` | 38 | 38 |
| `region-wave-1` | 39 | 39 |
| `celestial-wave-1` | 40 | 40 |
| `spell-wave-1` | 41 | 41 |
| `personality-wave-17` | 42 | 42 |
| `event-wave-2` | 43 | 43 |
| `strategy-wave-6` | 44 | 44 |
| `personality-wave-18` | 45 | 45 |
| `holding-wave-1` | 46 | 46 |
| `item-wave-3` | 47 | 47 |
| `region-wave-2` | 48 | 48 |
| `spell-wave-2` | 49 | 49 |
| `strategy-wave-7` | 50 | 50 |
| `strategy-wave-8` | 51 | 51 |
| `strategy-wave-9` | 52 | 52 |
| `follower-wave-10` | 53 | 53 |
| `strategy-wave-10` | 54 | 54 |
| `spell-wave-3` | 55 | 55 |
| `personality-wave-19` | 56 | 56 |
| `follower-wave-11` | 57 | 57 |
| `strategy-wave-11` | 58 | 58 |
| `follower-wave-12` | 59 | 59 |
| `strategy-wave-12` | 60 | 60 |
| `follower-wave-13` | 61 | 61 |
| `personality-wave-20` | 62 | 62 |
| `follower-wave-14` | 63 | 63 |
| `strategy-wave-13` | 64 | 64 |
| `strategy-wave-14` | 65 | 65 |
| `strategy-wave-15` | 66 | 66 |
| `spell-wave-4` | 67 | 67 |
| `ring-wave-2` | 68 | 68 |
| `personality-wave-21` | 69 | 69 |
| `personality-wave-22` | 70 | 70 |
| `region-wave-3` | 71 | 71 |
| `strategy-wave-16` | 72 | 72 |
| `personality-wave-23` | 73 | 73 |
| `personality-wave-24` | 74 | 74 |
| `personality-wave-25` | 75 | 75 |
| `personality-wave-26` | 76 | 76 |
| `follower-wave-15` | 77 | 77 |
| `strategy-wave-18` | 78 | 78 |
| `event-wave-3` | 79 | 79 |
| `personality-wave-27` | 80 | 80 |
| `event-wave-4` | 81 | 81 |
| `personality-wave-28` | 82 | 82 |
| `strategy-wave-17` | 83 | 83 |
| `holding-wave-2` | 84 | 84 |
| `personality-wave-29` | 85 | 85 |
| `personality-wave-31` | 86 | 86 |
| `follower-wave-17` | 87 | 87 |
| `personality-wave-33` | 88 | 88 |
| `strategy-wave-22` | 89 | 89 |
| `personality-wave-30` | 90 | 90 |
| `item-wave-4` | 91 | 91 |
| `stronghold-wave-8` | 92 | 92 |
| `strategy-wave-27` | 93 | 93 |
| `spell-wave-9` | 94 | 94 |
| `personality-wave-38` | 95 | 95 |
| `stronghold-wave-5` | 96 | 96 |
| `strategy-wave-23` | 97 | 97 |
| `personality-wave-32` | 98 | 98 |
| `strategy-wave-21` | 99 | 99 |
| `strategy-wave-28` | 100 | 100 |
| `strategy-wave-29` | 101 | 101 |
| `item-wave-5` | 102 | 102 |
| `spell-wave-10` | 103 | 103 |
| `personality-wave-39` | 104 | 104 |
| `follower-wave-18` | 105 | 105 |
| `item-wave-6` | 106 | 106 |
| `holding-wave-3` | 107 | 107 |
| `personality-wave-40` | 108 | 108 |
| `celestial-wave-2` | 109 | 109 |
| `strategy-wave-31` | 110 | 110 |
| `follower-wave-19` | 111 | 111 |
| `strategy-wave-30` | 112 | 112 |
| `spell-wave-11` | 113 | 113 |
| `holding-wave-4` | 114 | 114 |
| `strategy-wave-32` | 115 | 115 |
| `personality-wave-41` | 116 | 116 |
| `strategy-wave-33` | 117 | 117 |
| `personality-wave-42` | 118 | 118 |
| `strategy-wave-34` | 119 | 119 |
| `spell-wave-12` | 120 | 120 |
| `item-wave-7` | 121 | 121 |
| `celestial-wave-3` | 122 | 122 |
| `strategy-wave-35` | 123 | 123 |
| `spell-wave-13` | 124 | 124 |
| `stronghold-wave-10` | 125 | 125 |
| `strategy-wave-36` | 126 | 126 |
| `spell-wave-14` | 127 | 127 |
| `strategy-wave-37` | 128 | 128 |
| `holding-wave-5` | 129 | 129 |
| `strategy-wave-38` | 130 | 130 |
| `region-wave-4` | 131 | 131 |
| `strategy-wave-39` | 132 | 132 |
| `strategy-wave-40` | 133 | 133 |
| `stronghold-wave-11` | 134 | 134 |
| `strategy-wave-41` | 135 | 135 |
| `spell-wave-15` | 136 | 136 |
| `strategy-wave-42` | 137 | 137 |
| `personality-wave-43` | 138 | 138 |
| `strategy-wave-43` | 139 | 139 |
| `item-wave-8` | 140 | 140 |
| `spell-wave-16` | 141 | 141 |
| `follower-wave-20` | 142 | 142 |
| `holding-wave-6` | 143 | 143 |
| `celestial-wave-4` | 144 | 144 |
| `region-wave-5` | 145 | 145 |
| `personality-wave-44` | 146 | 146 |
| `personality-wave-45` | 147 | 147 |
| `spell-wave-17` | 148 | 148 |
| `follower-wave-21` | 149 | 149 |
| `personality-wave-46` | 150 | 150 |
| `follower-wave-22` | 151 | 151 |
| `personality-wave-47` | 152 | 152 |
| `strategy-wave-44` | 153 | 153 |
| `follower-wave-23` | 154 | 154 |
| `item-wave-9` | 155 | 155 |
| `strategy-wave-45` | 156 | 156 |
| `personality-wave-48` | 157 | 157 |
| `strategy-wave-46` | 158 | 158 |
| `personality-wave-49` | 159 | 159 |
| `personality-wave-50` | 160 | 160 |
| `personality-wave-51` | 161 | 161 |
| `personality-wave-52` | 162 | 162 |
| `strategy-wave-47` | 163 | 163 |
| `stronghold-wave-12` | 164 | 164 |
| `celestial-wave-5` | 165 | 165 |
| `strategy-wave-50` | 166 | 166 |
| `personality-wave-53` | 167 | 167 |
| `follower-wave-24` | 168 | 168 |
| `personality-wave-58` | 169 | 169 |
| `personality-wave-60` | 170 | 170 |
| `holding-wave-7` | 171 | 171 |
| `region-wave-6` | 172 | 172 |
| `event-wave-5` | 173 | 173 |
| `follower-wave-26` | 174 | 174 |
| `item-wave-11` | 175 | 175 |
| `spell-wave-18` | 176 | 176 |
| `follower-wave-25` | 177 | 177 |
| `personality-wave-55` | 178 | 178 |
| `personality-wave-57` | 179 | 179 |
| `item-wave-10` | 180 | 180 |
| `personality-wave-61` | 181 | 181 |
| `holding-wave-8` | 182 | 182 |
| `spell-wave-19` | 183 | 183 |
| `strategy-wave-51` | 184 | 184 |
| `stronghold-wave-13` | 185 | 185 |
| `follower-wave-27` | 186 | 186 |
| `personality-wave-63` | 187 | 187 |
| `strategy-wave-52` | 188 | 188 |
| `strategy-wave-54` | 189 | 189 |
| `strategy-wave-53` | 190 | 190 |
| `strategy-wave-55` | 191 | 191 |
| `strategy-wave-56` | 192 | 192 |
| `strategy-wave-57` | 193 | 193 |
| `strategy-wave-58` | 194 | 194 |
| `strategy-wave-59` | 195 | 195 |
| `strategy-wave-60` | 196 | 196 |
| `strategy-wave-61` | 197 | 197 |
| `strategy-wave-63` | 198 | 198 |
| `spell-wave-20` | 199 | 199 |
| `stronghold-wave-14` | 200 | 200 |
| `region-wave-7` | 201 | 201 |
| `item-wave-12` | 202 | 202 |
| `holding-wave-9` | 203 | 203 |
| `follower-wave-28` | 204 | 204 |
| `personality-wave-64` | 205 | 205 |
| `personality-wave-62` | 206 | 206 |
| `personality-wave-66` | 207 | 207 |
| `follower-wave-29` | 208 | 208 |
| `stronghold-wave-15` | 209 | 209 |
| `personality-wave-67` | 210 | 210 |
| `personality-wave-69` | 211 | 211 |
| `personality-wave-70` | 212 | 212 |
| `personality-wave-72` | 213 | 213 |
| `personality-wave-73` | 214 | 214 |
| `spell-wave-21` | 215 | 215 |
| `holding-wave-11` | 216 | 216 |
| `item-wave-13` | 217 | 217 |
| `event-wave-6` | 218 | 218 |
| `celestial-wave-6` | 219 | 219 |
| `region-wave-8` | 220 | 220 |
| `personality-wave-65` | 221 | 221 |
| `holding-wave-10` | 222 | 222 |
| `spell-wave-22` | 223 | 223 |
| `item-wave-14` | 224 | 224 |
| `personality-wave-75` | 225 | 225 |
| `personality-wave-77` | 226 | 226 |
| `holding-wave-12` | 227 | 227 |
| `follower-wave-30` | 228 | 228 |
| `event-wave-7` | 229 | 229 |
| `stronghold-wave-16` | 230 | 230 |
| `stronghold-wave-17` | 231 | 231 |
| `stronghold-wave-19` | 232 | 232 |
| `stronghold-wave-20` | 233 | 233 |
| `celestial-wave-7` | 234 | 234 |
| `personality-wave-74` | 235 | 235 |
| `personality-wave-68` | 236 | 236 |
| `personality-wave-71` | 237 | 237 |
| `stronghold-wave-18` | 238 | 238 |
| `spell-wave-23` | 239 | 239 |
| `personality-wave-78` | 240 | 240 |
| `celestial-wave-8` | 241 | 241 |
| `holding-wave-13` | 242 | 242 |
| `region-wave-9` | 243 | 243 |
| `event-wave-8` | 244 | 244 |
| `item-wave-15` | 245 | 245 |
| `follower-wave-31` | 246 | 246 |
| `personality-wave-76` | 247 | 247 |
| `spell-wave-24` | 248 | 248 |
| `strategy-wave-62` | 249 | 249 |
| `strategy-wave-64` | 250 | 250 |
| `spell-wave-25` | 251 | 251 |
| `region-wave-10` | 252 | 252 |
| `item-wave-16` | 253 | 253 |
| `strategy-wave-65` | 254 | 254 |
| `celestial-wave-9` | 255 | 255 |
| `personality-wave-79` | 256 | 256 |
| `strategy-wave-66` | 257 | 257 |
| `follower-wave-32` | 258 | 258 |
| `item-wave-17` | 259 | 259 |
| `personality-wave-80` | 260 | 260 |
| `personality-wave-86` | 261 | 261 |
| `strategy-wave-72` | 262 | 262 |
| `strategy-wave-69` | 263 | 263 |
| `personality-wave-83` | 264 | 264 |
| `stronghold-wave-23` | 265 | 265 |
| `personality-wave-89` | 266 | 266 |
| `stronghold-wave-29` | 267 | 267 |
| `strategy-wave-75` | 268 | 268 |
| `region-wave-19` | 269 | 269 |
| `personality-wave-95` | 270 | 270 |
| `strategy-wave-81` | 271 | 271 |
| `personality-wave-96` | 272 | 272 |
| `spell-wave-26` | 273 | 273 |
| `strategy-wave-82` | 274 | 274 |
| `celestial-wave-10` | 275 | 275 |
| `holding-wave-28` | 276 | 276 |
| `personality-wave-92` | 277 | 277 |
| `strategy-wave-78` | 278 | 278 |
| `personality-wave-100` | 279 | 279 |
| `strategy-wave-86` | 280 | 280 |
| `spell-wave-30` | 281 | 281 |
| `item-wave-22` | 282 | 282 |
| `holding-wave-33` | 283 | 283 |
| `event-wave-13` | 284 | 284 |
| `personality-wave-108` | 285 | 285 |
| `strategy-wave-94` | 286 | 286 |
| `spell-wave-38` | 287 | 287 |
| `item-wave-30` | 288 | 288 |
| `event-wave-21` | 289 | 289 |
| `personality-wave-112` | 290 | 290 |
| `strategy-wave-98` | 291 | 291 |
| `item-wave-34` | 292 | 292 |
| `spell-wave-42` | 293 | 293 |
| `event-wave-25` | 294 | 294 |
| `celestial-wave-26` | 295 | 295 |
| `stronghold-wave-46` | 296 | 296 |
| `holding-wave-45` | 297 | 297 |
| `personality-wave-116` | 298 | 298 |
| `strategy-wave-102` | 299 | 299 |
| `event-wave-29` | 300 | 300 |
| `item-wave-38` | 301 | 301 |
| `spell-wave-46` | 302 | 302 |
| `personality-wave-104` | 303 | 303 |
| `spell-wave-34` | 304 | 304 |
| `strategy-wave-90` | 305 | 305 |
| `follower-wave-41` | 306 | 306 |
