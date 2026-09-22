# B3 firelight — first motion milestone

Alfredo accepted native milestone 4 and asked to continue after animation was identified as the next pass. This preserves the approved sparse hall and all Meshy geometry. No new jobs, furniture, scenery, sound, smoke or camera movement are added.

`TableArtStage` already owns the presentation clock and reduced-motion preference. `WarRoomStage` now forwards both to `FortressHall`. Every practical has its own fixed phase offset; the two tabletop lights use phases 0 and 1.8 to match the established cloth, frame and card treatment. The actual point light, window emission and soft halation receive the same energy. No random state advances and no shader `TIME` are used. Flame deformation grows toward the tip and reserves space at the top of its quad so the wick stays anchored and the moving tip does not clip. The brazier uses its five existing tongues with differing shapes and a slightly quicker rhythm.

The M4 floor EXR remains byte-for-byte unchanged. A smoothly weighted local energy delta modulates only its warm illumination near practicals; the cool ambient term and contact AO stay steady. This is a restrained artistic approximation over the combined bake, not individually baked light transport or moving bounced shadows. Native direct light and existing shadow maps respond to the firelight. Lantern bodies remain still. The halo premultiplies its radial falloff into colour and discards the negligible outer tail, preventing an additive rectangle at close zoom.

Reduced motion forces light energy to the accepted baseline, disables deformation and fixes all phase inputs, producing identical render bytes at different clock times. Toggling it off resumes animation. The native review probe checks visible motion, exact freeze and resumption; the live gameplay suite also verifies the actual settings path.

## Native review

Open `tools/blender/Review War Room.command`: keys 1–7 select composition, overview, joinery, floor, table lantern, room lantern and brazier; M toggles motion, R shows the approved source. Normal play uses `Play Latest.command` and the existing accessibility setting.

`bash tools/check-war-room-motion.sh` records and validates all four room views; a view name can restrict it to one. It requires FFmpeg for video encoding and the native graphical desktop. Incomplete recordings fail instead of encoding a partial clip.

`WarRoomReview.tscn -- --clean --room-view=composition --room-check-motion --room-film=/absolute/output --room-frames=192` records 192 uncomposited native frames at 24 evenly sampled animation phases per second. FFmpeg only encodes those frames. These clips demonstrate motion and rendering, not real-time gameplay FPS. The gallery includes the actual live-board capture and the prior accepted static/source references.

The first animation milestone is a review point, not a claim of final AAA or pixel-identical fidelity. Native Windows graphical playtesting, sustained profiling and final human motion acceptance remain separate.

## Iteration notes

Early captures exposed overly uniform brazier tongues and a rectangular halo edge in a zoomed live view. Both were corrected and the final films/native suite were regenerated. One early tabletop recording ended at 122 frames without a completion marker; it is excluded from final evidence. The new capture wrapper requires the complete frame count and success markers. Draft frame folders remain ignored local diagnostics.
