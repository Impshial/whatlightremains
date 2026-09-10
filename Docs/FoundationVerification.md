# Foundation Verification Report

This report describes the acceptance matrix for the oriented-room creation milestone in Unity `6000.5.8f1`. The results below were recorded from a clean regeneration and batch verification run on 2026-09-10.

## Current verification status

| Check | Current-revision result | Expected evidence |
| --- | ---: | --- |
| Foundation asset regeneration | Passed | `FoundationBuilder.BuildAll` completed with no compile or generator errors |
| Runtime and test assembly compilation | Passed | Unity imported all four project assemblies with no C# errors |
| EditMode suite | Passed — 48/48 | `TestResults/editmode-ladder-access.xml` |
| PlayMode suite | Passed — 13/13 | `TestResults/playmode-ladder-access.xml` |
| Windows x64 development build | Passed | `Build/Windows/WhatLightRemains.exe` (201,800,554 total build bytes) and `TestResults/windows-build-ladder-access.log` |
| 1920×1080 visual pass | Passed | `TestResults/CreationPreview-Fixed-1920x1080.png` shows the full ghost with readable internal geometry, low-opacity glass, arrow, prompts, viewmodel, and hotbar |
| 1280×720 visual pass | Passed | Built-player capture confirms scaled UI and an unobstructed screen center |
| Built-player startup/cursor smoke | Passed | Player launched the Foundation scene, released/recaptured the cursor, captured a frame, and exited with code 0 |
| Full interactive controls walkthrough | Manual | Walk, sprint, jump, rotate, place, climb, and opacity-drag checks still require hands-on input |
| Build-excluded validation fixture | Manual | Independent room gravity/lighting, transmitted light, and opaque obstruction checks |

## Automated coverage

The EditMode suite verifies:

- exact 8 m interior dimensions, external boundary thickness, rotated room gravity, and independent per-instance settings;
- all 24 right-handed cardinal `RoomOrientation` bases and exact local-Y/local-Z quarter turns;
- three-dimensional `Vector3Int` occupancy, volume-center/root conversion, exact anchors, stale-candidate rejection, and no accumulated drift;
- side-placement floor alignment, ceiling targeting, floor rejection, upright sealed stacking, side-down traversable stacking, and ceiling-down sealed stacking;
- reciprocal shared-face connection data and loop-closing side connections;
- the generated room's closed ceiling plus four edge variants, exact 2 m × 2.4 m ceiling openings, non-solid ladder art, and trigger-only ladder interaction;
- the custom local-Y capsule, kinematic Rigidbody, independent yaw/pitch pivots, 3 m/s walk, 6 m/s sprint, 0.35-second gravity alignment, 2.5 m/s ladder speed, and 0.25-second detach cooldown;
- normalized arbitrary-gravity movement and jump math;
- full renderer-only ghost construction, separate 2% glass/10% structure/16% floor opacity bands, alpha blending, outline, gravity arrow, and absence of colliders, lights, or room scripts;
- normalized (`±1`) and legacy Windows (`±120`) mouse-wheel ticks producing one signed quarter-turn, plus `Ctrl`/`Ctrl+Alt` rotation prompt text and generated keyboard/mouse bindings;
- exactly eight room strips, distributed strip emitters, four side-door and four ceiling-door frame variants, and doorway frame glow/cast-light power at 50% of strip power;
- HD floor/glass resources, full 0–1 glass-opacity range, URP Forward+, camera stack, hotbar, and Foundation scene wiring.

The PlayMode suite verifies:

- 3 m/s walking and equal cardinal/diagonal speed;
- 6 m/s sprinting and equal cardinal/diagonal speed;
- approximately 1 m jump apex, grounded-only jump, landing, ceiling collision, and sustained wall containment;
- side-door traversal and `PlayerRoomTracker` ownership transfer;
- Create-mode entry/exit, bottom-center rotation hint visibility, full nonphysical ghost geometry, filled floor, and gravity arrow;
- exact snapped side and ceiling candidates, floor rejection, one-click placement, reciprocal doorways, and preview cleanup;
- independent created-room gravity with no duplicate player, camera, or HUD;
- real PhysX-trigger discovery of generated ladders, end-to-end ceiling-ladder traversal, destination-room handoff, collision-safe 0.35-second gravity alignment, and a grounded upper exit;
- current and future glass-opacity propagation;
- cursor release/recapture behavior.

## Required hands-on walkthrough

1. Enter Play Mode in `Foundation.unity`. Confirm one enclosed room, a black environment, one player/viewmodel, the bottom-left `Press C to create a room` prompt, and no bottom-center rotation text.
2. Walk with `WASD`, hold left `Shift`, and confirm sprint is approximately twice walking speed. Repeat while holding `Ctrl` and while holding `Ctrl+Alt`; sprint must remain active.
3. Press `C`. Confirm `Left-Click to finalize placement` remains bottom-left and `Hold Ctrl to Rotate` appears bottom-center.
4. Aim at an available side wall. Confirm the preview is a translucent copy of all prefab geometry—not only a wireframe—with a clearly filled floor, thin outline, no illumination/collision, and a centered arrow pointing toward the candidate floor.
5. Hold `Ctrl`. Confirm the hint becomes `Mouse Wheel: Rotate Y 90°`; scroll both directions and verify one 90° step per detent. Add `Alt`, confirm `Mouse Wheel: Rotate Z 90°`, and verify the alternate axis. Release the modifiers and confirm the idle hint returns.
6. Confirm side placement becomes invalid when rotation causes floor directions to disagree. Return to a floor-aligned orientation, place the room, and walk through the centered side doorway.
7. Re-enter Create mode in the connected room. Confirm its rotation begins from that room's orientation rather than retaining the previous room's preview state.
8. Aim at the ceiling with the preview upright. Place the room and confirm the shared boundary remains sealed.
9. In a fresh available ceiling cell, rotate the candidate until one of its sides faces downward. Place it and confirm the lower room opens at the matching ceiling edge, the upper room has a reciprocal side doorway, and a visible ladder spans the route without blocking the doorway.
10. Face the ladder and use `W`/`S` to traverse at 2.5 m/s. Press `Space` to detach and verify it cannot immediately remount during the short cooldown. Complete the climb and confirm the body/camera aligns smoothly to the destination gravity in about 0.35 seconds while mouse look remains responsive.
11. Create an inverted ceiling-down candidate above another available room. Confirm the resulting boundary is sealed.
12. Inspect an open side and ceiling doorway. Frame geometry should glow and cast visibly softer light than the main strips—exactly half power—with no center-room point-light reflection.
13. Press `Escape` during Create mode. Confirm the ghost and rotation hint disappear and the cursor is available for the opacity slider. Drag from 0% to 100%, then left-click away from the control to recapture the cursor.
14. Repeat the presentation pass at 1920×1080 and 1280×720. Check glass readability, floor/seam separation, ghost fill/arrow, prompt scaling, viewmodel lighting, and hotbar legibility.

## Known prototype boundaries

- Saving/loading constructed layouts, deletion, undo, resource costs, power routing, inventory consumption, arbitrary room caps, controller input, puzzles, threats, narrative systems, and audio are not part of this milestone.
- A floor cannot be targeted directly. Vertical traversal exists only for the side-down ceiling placement case; upright and ceiling-down stacked rooms are intentionally sealed.
- The gravity transition is a short kinematic orientation blend, not a physically simulated tumbling body. Movement, gravity integration, and jumping intentionally pause during the blend.
- Ladder visuals are intentionally non-solid. A trigger-driven traversal path owns movement while attached.
- Glass transmission remains an intentionally nonphysical URP approximation. It does not provide caustics, bounced light, physical absorption, refraction of later transparent objects, or accurate glass shadows.
- Each room retains many distributed real-time emitters. Forward+ avoids the old per-object light limit, but large player-built structures will eventually need visibility-aware light budgeting.
- The generated ghost mirrors available prefab renderer state but deliberately omits operational scripts, collision, occupancy triggers, gravity, and light sources.
- Development captures and executables include Unity Development Build behavior by design.
