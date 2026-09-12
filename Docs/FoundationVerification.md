# Foundation Verification Report

This report describes the acceptance matrix for the player vitals HUD and the existing Device Wall, fixed glass, deletion-tint, Pause-menu, and live 3D-map foundation in Unity `6000.5.8f1`.

## Player vitals milestone

The user's image and follow-up explicitly supersede the handoff's horizontal bars and visible label/value text: the HUD contains only three circular rings and centered heart, utensils, and air icons. Health/Hunger are on the actual player; the one shared Oxygen reserve lives on the Foundation scene's World Session root. The three defaults are 100/100. The reserve stays independent of room topology, and no gameplay system consumes or restores any value yet.

The builder creates one player owner, one scene reserve, and one three-gauge group. The existing Canvas scales a 72px ring stack at (28,24) from the upper-left; there are 14px gaps between rings. The full colored circumference contracts clockwise from twelve o'clock over a dark track. Icons remain white even when empty. Map and Pause disable the group; Create/Delete and traversal retain it. Hiding the group detaches event handlers; showing it refreshes from the live owners. New Game and Reset World reload scene-owned state, and MainMenu contains no vitals objects.

`VitalsStateTests` covers default/normalized values, fractions, finite overflow/underflow, clamped setters, rejected NaN/infinity/negative Add/Remove without mutation, exact event/no-op semantics, independent player values, invalid Inspector repairs, tiny/large positive capacities, and captured configuration/reset behavior. `GeneratedVitalsAssetTests` covers generated ownership and explicit references, exactly three radial images on the existing Canvas, icon identity and absence of visible text, no raycast targets, and MainMenu absence. `VitalsHudPlayModeTests` covers startup binding, API-to-fill updates, elapsed time and zero-value behavior, normal controls at zero, repeated map/Pause cycles, disable/rebind cleanup, room/ghost/topology/ownership/lighting independence, Reset World, and MainMenu/New Game lifecycle.

The editor-only explicit test `VitalsHudPlayModeTests.CaptureVitalsAtSupportedResolutionsAndValues` creates full, partial, empty, and dark/rotated captures at 1920×1080, 1280×720, and 2560×1080 under `Logs/VitalsHudVisuals`. It must be selected explicitly with a graphics-capable Unity test process. The captures are evidence only after they are produced and inspected; this fixture does not add gameplay scripts, buttons, or input bindings.

The previous milestone's pending-build note was stale: the live editor had already regenerated the assets, and its September 12 EditMode run passed 67/68. Its one failure was a pre-existing array assertion (`Has.Count` for `DeviceWall.Anchors`); this milestone corrects it to `Has.Length`. PlayMode validation also found that `WorldMapController` overwrote its serialized reticle with an invalid runtime `UI/Skin/Knob.psd` resource lookup; it now keeps the builder-authored circle. These fixes preserve the existing map appearance and unblock scene startup validation. Historical results are not evidence for the new vitals code. Current validation is recorded below.

## Current verification status

| Check | Current-revision result | Expected evidence |
| --- | ---: | --- |
| Foundation asset regeneration | Passed | Supported BuildAll completed in an isolated copy; Player/Hotbar/CubeRoom prefabs and Foundation/Validation scenes synchronized back with matching serialized references. CubeRoom regeneration changes internal IDs/order; unrelated material and MainMenu edits were retained |
| Runtime and test assembly compilation | Passed | Runtime, editor, EditMode, and PlayMode assemblies compile cleanly with no project warnings or C# errors |
| EditMode suite | Passed | 109/109, zero skipped; `TestResults/Vitals-EditMode.xml` |
| PlayMode suite | Partial | 13 passed, 12 failed, 1 explicit capture skipped; all 8 new vitals integration tests passed. See `TestResults/Vitals-PlayMode-Final.xml` and limitations below |
| Windows x64 development build | Passed | Fresh development player built successfully from the regenerated scenes and retained material/MainMenu assets; output synchronized to `Build/Windows/WhatLightRemains.exe`. See `Logs/Vitals-Windows-Final.log` |
| 1920×1080 visual pass | Passed | Full, partial, empty, and dark/rotated captures produced and inspected; icon-only rings stay clear of existing HUD |
| 1280×720 and 2560×1080 visual pass | Passed | All 8 additional captures inspected for scaling, depletion, icon legibility, and upright screen-space presentation |
| Built-player startup/cursor smoke | Manual | Confirm gameplay releases the cursor only for the main menu, Pause menu, and 3D map |
| Full interactive controls walkthrough | Manual | Walk, sprint, jump, rotate, place, traversal, deletion, reset, and menu transitions still require hands-on input |
| Build-excluded validation fixture | Manual | Independent room gravity/lighting, transmitted light, and opaque obstruction checks |

The explicit graphical capture passed 1/1 separately (`TestResults/Vitals-Capture.xml`), producing twelve images and contact sheets in `Logs/VitalsHudVisuals`. No capture scripts or controls are included in the game. The full PlayMode run is **not** an overall pass: one older startup test still expects two cameras although the existing map adds a third; other older failures concern movement/jump/traversal producing no displacement, creation/deletion mode entry/targeting, and map room selection. These tests need separate follow-up; this milestone does not claim a complete interactive regression pass. New vitals tests exercised API mutation, zero-state controls, room topology independence, Pause/map visibility, binding cleanup, Reset World, and New Game successfully.

Validation used a separate temporary project and the existing Unity installation. The initial isolated license IPC and incomplete package-cache failures were resolved before executed tests; live package versions and the user's editor session were preserved. Generated CubeRoom was synchronized alongside both scenes because their serialized lighting overrides reference its regenerated internal IDs; unrelated material/MainMenu edits were retained.

## Automated coverage

The EditMode suite verifies:

- exact 8 m interior dimensions, external boundary thickness, standard gravity of exactly 32 ft/s² (`9.7536 m/s²`), rotated room gravity, and independent per-instance settings;
- all 24 right-handed cardinal `RoomOrientation` bases and exact local-Y/local-Z quarter turns;
- three-dimensional `Vector3Int` occupancy, volume-center/root conversion, exact anchors, stale-candidate rejection, and no accumulated drift;
- all four Z-axis orientations for side placement, existing-room aperture authority, side-to-side and side-to-ceiling projection, ceiling-to-side projection, centered ceiling-to-ceiling openings, and the absolute floor-contact veto;
- reciprocal shared-face connection data and loop-closing side connections;
- one shared segmented boundary around each exact aperture, immediate split base rails, reciprocal external-geometry ownership, and no full-face collider across an opening;
- the generated room's closed ceiling plus four legacy edge variants, exact 2 m × 2.4 m doorway and 2.4 m square ceiling apertures, and complete absence of ladder components or geometry;
- the custom local-Y capsule, kinematic Rigidbody, independent yaw/pitch pivots, 3 m/s walk, 6 m/s sprint, traversal motion ownership, 0.35-second gravity alignment, and accelerating quadratic grapple curve with an exact aperture-center endpoint;
- normalized arbitrary-gravity movement and jump math;
- full renderer-only ghost construction, separate 2% glass/10% structure/16% floor opacity bands, alpha blending, outline, gravity arrow, and absence of colliders, lights, or room scripts;
- normalized (`±1`) and legacy Windows (`±120`) mouse-wheel ticks producing one signed quarter-turn, plus `Ctrl`/`Ctrl+Alt` rotation prompt text and generated keyboard/mouse bindings;
- exactly eight room strips, distributed strip emitters, four side-door and four ceiling-door frame variants, and doorway frame glow/cast-light power at 50% of strip power;
- independent room power defaults and toggles, unlit-but-present strip meshes, debug-light override precedence, and generated shared-frame side ownership;
- the fixed Device Wall face, initial-left orientation under rotated gravity, stable 4 × 4 anchor grid, opening-aware studs/rails, rotated multi-anchor footprints, atomic reservation/release, physical footprint overlap, and independent room occupancy;
- renderer-only ghost Device Wall hardware and sixteen pooled green/red anchor markers that follow every supported room rotation;
- fixed 5% regular glass and 10% smoked Device Wall glass, transparent red deletion tint without outline geometry, removed opacity UI/input, URP Forward+, camera stack, hotbar, and Foundation scene wiring;
- Pause-menu construction and input wiring, including mode-first Escape precedence and main-menu-safe time-scale restoration.
- live-world map camera wiring, exact opening-view reset, current-focus orbit with in-drag axis switching and hysteresis, crosshair-room in-place leveling, move/zoom math, player marker, HUD isolation, and Escape-before-Pause ownership.

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
- fixed authored glass opacity on newly placed rooms;
- Pause/resume time-scale and menu-only cursor ownership, including Create/Delete precedence.

## Required hands-on walkthrough

1. Enter Play Mode in `Foundation.unity`. Confirm one enclosed room, a black environment, one player/viewmodel, both default lower-left mode prompts, no opacity slider, and no bottom-center rotation text.
2. Walk with `WASD`, hold left `Shift`, and confirm sprint is approximately twice walking speed. Repeat while holding `Ctrl` and while holding `Ctrl+Alt`; sprint must remain active.
3. Press `C`. Confirm `Left-Click to finalize placement` remains bottom-left, the Delete prompt hides, and rotation help appears only once a valid ghost is visible.
4. Aim at an available side wall. Confirm the preview is a translucent copy of all prefab geometry—not only a wireframe—with a clearly filled floor, thin outline, no illumination/collision, and a centered arrow pointing toward the candidate floor. Its dedicated left wall must show smoked glass, four rails, a perimeter frame, physical studs, and sixteen hollow anchor markers.
5. Hold `Ctrl`. Confirm the hint becomes `Mouse Wheel: Rotate Y 90°`; scroll both directions and verify one 90° step per detent. Add `Alt`, confirm `Mouse Wheel: Rotate Z 90°`, and verify the alternate axis. Release the modifiers and confirm the idle hint returns.
6. While targeting a side wall, rotate through all four Z-axis orientations and confirm the ghost remains visible. Eligible side/ceiling contacts must preview the exact projected doorway; a contact involving either room's local Floor must remain visibly sealed. Place a 90-degree room and confirm the source doorway and destination aperture occupy the same world-space opening.
7. Re-enter Create mode in the connected room. Confirm its rotation begins from that room's orientation rather than retaining the previous room's preview state.
8. Aim at the ceiling with the preview upright. Place the room and confirm the Floor contact stays sealed. In another available ceiling cell, rotate the candidate until a side faces the source ceiling; confirm the new side doorway is projected into that ceiling.
9. Create an inverted room so two Ceilings meet. Confirm one centered 2.4 m square opening, solid surrounding collision, and no ladder or invisible climb trigger.
10. Walk through the floor-level side of a rotated doorway without pressing `E`. Confirm the capsule clears the frame before it aligns to destination gravity, lands under control, and can move normally afterward.
11. Return to the same connection from its elevated side. Confirm the frame highlights and the HUD shows exactly `Hold E to Traverse`; hold `E` and confirm the player accelerates along a visible curve, arrives dead center in the opening, and crosses quickly. Release before commitment to cancel, then retry and release after commitment to confirm the short transition completes safely. Confirm a floor-level doorway never shows the prompt or accepts `E`.
12. Inspect an open side and ceiling doorway. Frame geometry should glow and cast visibly softer light than the main strips—exactly half power—with no center-room point-light reflection. A ceiling-edge opening must replace the crossing full strip and housing with two split light segments, leaving the aperture completely clear.
13. In Play Mode, toggle `Power On` on one room in the Inspector. Confirm its strips, real-time lights, and its facing half of every shared frame turn off immediately while adjacent room lighting remains on; restore it and confirm the prior tuning returns.
14. Press `Delete`, focus a directly adjacent non-primary room through the current room's first face, and confirm only that room's transparent glass becomes subtly red—there must be no red border. Right-click and confirm the room disappears, every touching passage is removed, remaining boundaries close immediately, and disconnected downstream rooms remain floating.
15. Press `Escape` while Create mode is active, then while Delete mode is active. Each first press must cancel the mode without unlocking the cursor or opening Pause. From default mode, press `Escape`; confirm the Pause menu unlocks the cursor and all four buttons work. Resume must recapture it, Reset World must reload a single primary room, Main Menu must load the title screen, and Quit must close a standalone player.
16. Confirm normal gameplay shows a larger plus reticle. Press `M` and verify it is replaced by a compact circular reticle while the map opens centered on the player, aligned to the player's current room up/forward orientation, and close enough to feature the local room rather than fit the entire cluster; the normal Create/Delete HUD prompts must disappear. Verify current room details, nearby apertures, separators, light strips, rotations, and Device Wall hardware are visible, and that the cyan player arrow remains readable through geometry. Left-drag must orbit the current focal point, middle-drag must move the map, and the wheel must zoom. Begin a horizontal LMB orbit, curve into a vertical motion without releasing, then return horizontally; the active axis must change after only a small intentional movement while minor perpendicular noise produces no wobble. After moving the map, press `C` and confirm the focal point quickly eases back to the player without changing orbit or zoom. After changing the view, press `R` and confirm the exact view captured when the map opened is restored. Aim the circular reticle through a tilted room, click RMB, and confirm that exact crosshair room becomes level after a quick smooth roll while the camera position, focal point, and zoom remain stationary. Escape must return directly to gameplay without also opening Pause and must restore the plus reticle.
17. Repeat the presentation pass at 1920×1080 and 1280×720. Check fixed 5% regular glass, 10% smoked Device Wall glass, hardware/aperture clipping, immediate floor/seam separation, ghost fill/arrow/markers, deletion tint, map and Pause scaling, viewmodel lighting, and hotbar legibility.

## Known prototype boundaries

- Saving/loading constructed layouts, deletion undo, resource costs, power routing, inventory consumption, arbitrary room caps, controller input, puzzles, threats, narrative systems, and audio are not part of this milestone.
- A floor cannot be targeted directly and every shared contact involving either room's local Floor is permanently sealed. Non-floor side contacts remain eligible even when room gravity differs.
- The gravity transition is a short collision-checked kinematic crossing, orientation blend, and controlled landing—not a physically simulated tumbling body. Movement, gravity integration, jumping, and creation intentionally yield to the traversal controller during this interval.
- `Hold E to Traverse` is intentionally asymmetric: it is available only from a side where the authoritative opening's lowest clear edge is elevated more than 0.08 m above that room's local floor. Floor-level sides remain ordinary walking routes.
- Ladders, ladder visuals, climb zones, and ladder-specific input are absent from generated assets and gameplay.
- Power is a per-room lighting state only; no supply, routing, resource, switch, or permanent power HUD is included.
- Glass transmission remains an intentionally nonphysical URP approximation. It does not provide caustics, bounced light, physical absorption, refraction of later transparent objects, or accurate glass shadows.
- Each room retains many distributed real-time emitters. Forward+ avoids the old per-object light limit, but large player-built structures will eventually need visibility-aware light budgeting.
- The generated ghost mirrors available prefab renderer state but deliberately omits operational scripts, collision, occupancy triggers, gravity, and light sources.
- Device anchors provide geometry, availability, and reservation foundations only. There are no placeable devices or equipment-placement controls yet, and the colored overlay hook remains hidden outside room creation.
- Development captures and executables include Unity Development Build behavior by design.

## Implementation handoff

The rotated-room wedge came from starting capsule rotation as soon as occupancy changed, while the body was still inside the shared frame. `PlayerRoomTraversal` now restores and holds the source orientation, owns motion until the full capsule is 1.35 m into the destination, and only then runs gravity alignment and a controlled room-relative landing.

The delayed floor separator came from destroying and rebuilding passage geometry at the end of each topology refresh: the deferred destruction callback from the old passage could release ownership from the newly built boundary. Passage teardown now relinquishes its ownership before deferred destruction, and the split dark base rails are built as part of the new shared passage on the same placement commit.

Every traversable connection now stores one authoritative `RoomAperture`. The existing room supplies a side-wall doorway; an existing ceiling/new side uses the new side doorway; two ceilings use a centered square opening; and any Floor contact is sealed. The preview and committed segmented geometry use the same resolver. There are no ladder systems. Floor-level sides use ordinary walking and automatic post-clearance gravity transition, while only an elevated side can show `Hold E to Traverse` and accept assisted movement.

`CubeRoom.PowerOn`, `SetPower(bool)`, and `TogglePower()` independently control each room's strip emission/lights and its facing portion of shared doorway lighting. The serialized Inspector state defaults ON, responds during Play Mode, and remains subordinate to the existing debug lighting override without mutating shared materials.

Standard room gravity now uses the exact 32 ft/s² conversion (`9.7536 m/s²`) rather than the rounded conventional `9.81 m/s²`. Assisted traversal captures its start and computes a bowed quadratic Bézier to the authoritative aperture center. Squared normalized time gives the pull an accelerating grapple feel; it reaches the opening in 0.65 seconds, continues through at 12 m/s, and uses a 6 m/s controlled landing while every displacement still passes through the swept capsule mover.

The dedicated Device Wall is prefab-local West. At the first valid preview target, the room keeps the source gravity basis and applies a discrete yaw until that fixed face aligns with `cross(placement direction, reference up)`, which is the viewer's left in the snapped placement frame. When placement is parallel to source up, source forward supplies the stable fallback. The wall identity is never reselected as the camera moves; all later rotations carry its glass, rails, studs, anchor coordinates, and reservations together.

Regular glass is now authored at 5% opacity and the runtime slider/global shader override have been removed. The Device Wall uses separate 10% smoked glass. Delete selection now modifies only the glass renderers through per-renderer property blocks, preserving transparency and shared materials while removing every former outline renderer. Escape is a mode-first command: Create/Delete cancel themselves before the Pause controller can release the cursor; only default gameplay can open the four-action Pause menu.
