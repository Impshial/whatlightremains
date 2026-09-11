# Foundation Verification Report

This report describes the acceptance matrix for the authoritative-aperture, traversal, and room-power milestone in Unity `6000.5.8f1`. The results below were recorded from regeneration and automated verification on 2026-09-11.

## Current verification status

| Check | Current-revision result | Expected evidence |
| --- | ---: | --- |
| Foundation asset regeneration | Passed | `FoundationBuilder.BuildAll` completed with no compile or generator errors |
| Runtime and test assembly compilation | Passed | Unity imported all four project assemblies with no C# errors |
| EditMode suite | Passed — 51/51 | Fresh in-editor run on 2026-09-11 |
| PlayMode suite | Passed — 13/13 | Fresh in-editor run on 2026-09-11, including rotated-doorway clearance and asymmetric prompt regression |
| Windows x64 development build | Passed | `Build/Windows/WhatLightRemains.exe` (201,761,098 total build bytes) |
| 1920×1080 visual pass | Manual | Baseline presentation was previously captured; the new rotated traversal/seam/power behavior needs hands-on confirmation |
| 1280×720 visual pass | Manual | Current-revision UI scaling and contextual traversal prompt need hands-on confirmation |
| Built-player startup/cursor smoke | Manual | The current Windows player built successfully but was not interactively launched by this verification run |
| Full interactive controls walkthrough | Manual | Walk, sprint, jump, rotate, place, elevated traversal, power toggling, and opacity-drag checks still require hands-on input |
| Build-excluded validation fixture | Manual | Independent room gravity/lighting, transmitted light, and opaque obstruction checks |

## Automated coverage

The EditMode suite verifies:

- exact 8 m interior dimensions, external boundary thickness, rotated room gravity, and independent per-instance settings;
- all 24 right-handed cardinal `RoomOrientation` bases and exact local-Y/local-Z quarter turns;
- three-dimensional `Vector3Int` occupancy, volume-center/root conversion, exact anchors, stale-candidate rejection, and no accumulated drift;
- all four Z-axis orientations for side placement, existing-room aperture authority, side-to-side and side-to-ceiling projection, ceiling-to-side projection, centered ceiling-to-ceiling openings, and the absolute floor-contact veto;
- reciprocal shared-face connection data and loop-closing side connections;
- one shared segmented boundary around each exact aperture, immediate split base rails, reciprocal external-geometry ownership, and no full-face collider across an opening;
- the generated room's closed ceiling plus four legacy edge variants, exact 2 m × 2.4 m doorway and 2.4 m square ceiling apertures, and complete absence of ladder components or geometry;
- the custom local-Y capsule, kinematic Rigidbody, independent yaw/pitch pivots, 3 m/s walk, 6 m/s sprint, traversal motion ownership, and 0.35-second gravity alignment;
- normalized arbitrary-gravity movement and jump math;
- full renderer-only ghost construction, separate 2% glass/10% structure/16% floor opacity bands, alpha blending, outline, gravity arrow, and absence of colliders, lights, or room scripts;
- normalized (`±1`) and legacy Windows (`±120`) mouse-wheel ticks producing one signed quarter-turn, plus `Ctrl`/`Ctrl+Alt` rotation prompt text and generated keyboard/mouse bindings;
- exactly eight room strips, distributed strip emitters, four side-door and four ceiling-door frame variants, and doorway frame glow/cast-light power at 50% of strip power;
- independent room power defaults and toggles, unlit-but-present strip meshes, debug-light override precedence, and generated shared-frame side ownership;
- HD floor/glass resources, full 0–1 glass-opacity range, URP Forward+, camera stack, hotbar, and Foundation scene wiring.

The PlayMode suite verifies:

- 3 m/s walking and equal cardinal/diagonal speed;
- 6 m/s sprinting and equal cardinal/diagonal speed;
- approximately 1 m jump apex, grounded-only jump, landing, ceiling collision, and sustained wall containment;
- side-door traversal and `PlayerRoomTracker` ownership transfer;
- a differently oriented side-room crossing that clears the shared frame before gravity alignment, lands under destination gravity, restores normal movement, suppresses `E` on the floor-level side, and exposes the exact prompt from the elevated return side;
- Create-mode entry/exit, bottom-center rotation hint visibility, full nonphysical ghost geometry, filled floor, and gravity arrow;
- exact snapped side and ceiling candidates, floor rejection, one-click placement, reciprocal doorways, and preview cleanup;
- independent created-room gravity with no duplicate player, camera, or HUD;
- traversal-controller scene wiring, room handoff support, collision-safe gravity alignment, and movement ownership during assisted or automatic transitions;
- current and future glass-opacity propagation;
- cursor release/recapture behavior.

## Required hands-on walkthrough

1. Enter Play Mode in `Foundation.unity`. Confirm one enclosed room, a black environment, one player/viewmodel, the bottom-left `Press C to create a room` prompt, and no bottom-center rotation text.
2. Walk with `WASD`, hold left `Shift`, and confirm sprint is approximately twice walking speed. Repeat while holding `Ctrl` and while holding `Ctrl+Alt`; sprint must remain active.
3. Press `C`. Confirm `Left-Click to finalize placement` remains bottom-left and `Hold Ctrl to Rotate` appears bottom-center.
4. Aim at an available side wall. Confirm the preview is a translucent copy of all prefab geometry—not only a wireframe—with a clearly filled floor, thin outline, no illumination/collision, and a centered arrow pointing toward the candidate floor.
5. Hold `Ctrl`. Confirm the hint becomes `Mouse Wheel: Rotate Y 90°`; scroll both directions and verify one 90° step per detent. Add `Alt`, confirm `Mouse Wheel: Rotate Z 90°`, and verify the alternate axis. Release the modifiers and confirm the idle hint returns.
6. While targeting a side wall, rotate through all four Z-axis orientations and confirm the ghost remains visible. Eligible side/ceiling contacts must preview the exact projected doorway; a contact involving either room's local Floor must remain visibly sealed. Place a 90-degree room and confirm the source doorway and destination aperture occupy the same world-space opening.
7. Re-enter Create mode in the connected room. Confirm its rotation begins from that room's orientation rather than retaining the previous room's preview state.
8. Aim at the ceiling with the preview upright. Place the room and confirm the Floor contact stays sealed. In another available ceiling cell, rotate the candidate until a side faces the source ceiling; confirm the new side doorway is projected into that ceiling.
9. Create an inverted room so two Ceilings meet. Confirm one centered 2.4 m square opening, solid surrounding collision, and no ladder or invisible climb trigger.
10. Walk through the floor-level side of a rotated doorway without pressing `E`. Confirm the capsule clears the frame before it aligns to destination gravity, lands under control, and can move normally afterward.
11. Return to the same connection from its elevated side. Confirm the frame highlights and the HUD shows exactly `Hold E to Traverse`; hold `E` to cross. Release before commitment to cancel, then retry and release after commitment to confirm the short transition completes safely. Confirm a floor-level doorway never shows the prompt or accepts `E`.
12. Inspect an open side and ceiling doorway. Frame geometry should glow and cast visibly softer light than the main strips—exactly half power—with no center-room point-light reflection.
13. In Play Mode, toggle `Power On` on one room in the Inspector. Confirm its strips, real-time lights, and its facing half of every shared frame turn off immediately while adjacent room lighting remains on; restore it and confirm the prior tuning returns.
14. Press `Escape` during Create mode. Confirm the ghost and rotation hint disappear and the cursor is available for the opacity slider. Drag from 0% to 100%, then left-click away from the control to recapture the cursor.
15. Repeat the presentation pass at 1920×1080 and 1280×720. Check glass readability, immediate floor/seam separation, ghost fill/arrow, contextual prompt scaling, viewmodel lighting, and hotbar legibility.

## Known prototype boundaries

- Saving/loading constructed layouts, deletion, undo, resource costs, power routing, inventory consumption, arbitrary room caps, controller input, puzzles, threats, narrative systems, and audio are not part of this milestone.
- A floor cannot be targeted directly and every shared contact involving either room's local Floor is permanently sealed. Non-floor side contacts remain eligible even when room gravity differs.
- The gravity transition is a short collision-checked kinematic crossing, orientation blend, and controlled landing—not a physically simulated tumbling body. Movement, gravity integration, jumping, and creation intentionally yield to the traversal controller during this interval.
- `Hold E to Traverse` is intentionally asymmetric: it is available only from a side where the authoritative opening's lowest clear edge is elevated more than 0.08 m above that room's local floor. Floor-level sides remain ordinary walking routes.
- Ladders, ladder visuals, climb zones, and ladder-specific input are absent from generated assets and gameplay.
- Power is a per-room lighting state only; no supply, routing, resource, switch, or permanent power HUD is included.
- Glass transmission remains an intentionally nonphysical URP approximation. It does not provide caustics, bounced light, physical absorption, refraction of later transparent objects, or accurate glass shadows.
- Each room retains many distributed real-time emitters. Forward+ avoids the old per-object light limit, but large player-built structures will eventually need visibility-aware light budgeting.
- The generated ghost mirrors available prefab renderer state but deliberately omits operational scripts, collision, occupancy triggers, gravity, and light sources.
- Development captures and executables include Unity Development Build behavior by design.

## Implementation handoff

The rotated-room wedge came from starting capsule rotation as soon as occupancy changed, while the body was still inside the shared frame. `PlayerRoomTraversal` now restores and holds the source orientation, owns motion until the full capsule is 1.35 m into the destination, and only then runs gravity alignment and a controlled room-relative landing.

The delayed floor separator came from destroying and rebuilding passage geometry at the end of each topology refresh: the deferred destruction callback from the old passage could release ownership from the newly built boundary. Passage teardown now relinquishes its ownership before deferred destruction, and the split dark base rails are built as part of the new shared passage on the same placement commit.

Every traversable connection now stores one authoritative `RoomAperture`. The existing room supplies a side-wall doorway; an existing ceiling/new side uses the new side doorway; two ceilings use a centered square opening; and any Floor contact is sealed. The preview and committed segmented geometry use the same resolver. There are no ladder systems. Floor-level sides use ordinary walking and automatic post-clearance gravity transition, while only an elevated side can show `Hold E to Traverse` and accept assisted movement.

`CubeRoom.PowerOn`, `SetPower(bool)`, and `TogglePower()` independently control each room's strip emission/lights and its facing portion of shared doorway lighting. The serialized Inspector state defaults ON, responds during Play Mode, and remains subordinate to the existing debug lighting override without mutating shared materials.
