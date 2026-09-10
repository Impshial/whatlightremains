# Foundation Verification Report

The player room-creation milestone has passed its generated-asset, EditMode, PlayMode, Windows-build, and automated visual checks in Unity `6000.5.8f1`. A subjective hands-on walkthrough remains appropriate for feel and final art approval.

## Current status

All generated prefabs, materials, input actions, and scenes were rebuilt from `FoundationBuilder.BuildAll` before the final recorded test runs.

| Check | Result | Evidence |
| --- | ---: | --- |
| EditMode | Passed — 25/25 | `TestResults/editmode-room-creation.xml` |
| PlayMode | Passed — 10/10 | `TestResults/playmode-room-creation.xml` |
| Regeneration regression | Passed | The final EditMode and PlayMode suites ran after rebuilding every generated asset |
| Windows x64 development build | Passed | `Build/Windows/WhatLightRemains.exe`; Unity reported a 201,560,766-byte successful build output |
| Create-mode visual, 1920×1080 | Passed | `Artifacts/RoomCreation/creation-1920x1080.png` |
| Create-mode visual, 1280×720 | Passed | `Artifacts/RoomCreation/creation-1280x720.png` |
| Built-player startup and cursor smoke | Passed | `Artifacts/RoomCreation/cursor-smoke.log`; release and recapture both reported `True` |
| Build-excluded validation fixture | Not rebuilt for this milestone | Its authored scene and existing regression assertions remain intact |

The final captures show the complete snapped green cube outline through the starting room's glass, the exact Create-mode instruction at bottom left, the bottom-center hotbar, upper-right opacity control, floor material, viewmodel, room strips, and base rails at both target resolutions.

## Room-creation architecture

- Normal gameplay starts with exactly one registered room at logical cell `(0, 0)`. All four side boundaries are sealed and have no neighbor references.
- `CubeRoomClusterGenerator` is the authoritative room registry and occupancy map. Candidate transforms are derived from the primary room transform and exact 8 m integer cell offsets, preventing accumulated spacing drift.
- Every side wall exposes four canonical local-space snap anchors. Candidate validation checks all four corresponding points within `0.001 m` and verifies opposing face normals.
- `RoomPlacementTargeting` analytically intersects the gameplay camera ray with the current room's logical bounds. The first boundary decides the result, so floors, ceilings, and connected doorways cannot fall through to distant geometry.
- `RoomGhostPreview` creates one dedicated 12-edge wireframe with no room component, collider, trigger, gravity behavior, light, or shadow contribution. It is hidden immediately when targeting becomes invalid and destroyed on cancellation, success, focus loss, disable, or teardown.
- `RoomCreationController` owns the single Create-mode state and synchronizes the preview and instruction label. A placement click re-targets and revalidates immediately before commit.
- Successful placement instantiates the existing operational room prefab, copies the source room's initial gravity strength into an independent component, registers its occupied cell, and recomputes every cardinal shared-face connection. Multi-neighbor and four-neighbor gap fills open reciprocal doorways on every touching face; corner contact does not connect.
- Glass opacity remains shader-global, so rooms created after a slider change immediately use that value and continue receiving future changes without material mutation or renderer rescans.
- `FoundationBuilder` generates the C and left-click actions, preview material, player controller reference, HUD prompt, one-room scene configuration, and all updated prefabs/scenes idempotently.

## Automated coverage

EditMode tests cover the existing room dimensions, rotated gravity, lighting/material independence, doorway geometry, glass and floor assets, opacity range, strip emitters, cameras, HUD, input bindings, and scene wiring. New placement coverage additionally verifies:

- exact candidates and four-anchor alignment on all four starting walls;
- deterministic current-room boundary targeting and floor/ceiling rejection;
- occupied-cell rejection and absence of corner-only connections;
- reciprocal two-neighbor and surrounded four-neighbor gap connections;
- exact alignment across a 12-room chain with no cumulative transform drift;
- generated prefab/player/HUD/input/preview-material integration;
- one-room startup after a full regeneration.

PlayMode tests cover movement, normalized diagonal speed, jump apex, grounded-only jumping, landing, ceiling handling, sealed-wall containment, cursor state transitions, newly constructed doorway traversal, and room-ownership transfer. New runtime coverage additionally verifies:

- Create-mode entry and exact HUD text;
- one snapped, nonphysical, non-lighting ghost;
- one-click placement producing exactly one operational room and then cleaning up the ghost;
- invalid floor aim and connected-boundary rejection without leaving Create mode;
- targeting recomputation after `CurrentRoom` changes;
- reciprocal doorway creation, independent gravity, and no duplicate player/camera/HUD;
- current and future opacity propagation to newly created rooms.

## In-game walkthrough

1. Enter Play Mode in `Foundation.unity`; confirm the player begins in one enclosed 8 m room and the bottom-left label reads `Press C to create a room`.
2. Press `C` while the cursor is captured. Movement, look, and jump remain active, and the label changes to `Left-Click to finalize placement`.
3. Look at the center of an unconnected side wall. A fully snapped green cube outline appears outside the glass. Aim at the floor, ceiling, or open space to verify it disappears without leaving Create mode.
4. Aim back at the wall and left-click once. Exactly one real room appears, the ghost is destroyed, Create mode exits, and a centered doorway opens through the shared boundary.
5. Walk through the doorway. Once room ownership transfers, press `C` and add another room from the new current room.
6. To exercise multi-neighbor connection manually, build a U-shaped path around an empty grid cell, enter an adjacent room, and fill the gap. Every complete shared side face should open; merely touching a corner should not.
7. Press `Escape` during Create mode. The ghost is removed, normal text returns, and the cursor remains available for the opacity slider. Left-click away from the slider to recapture it.

## Manual-only checks and limitations

- Physical keyboard/mouse feel, a full 360-degree walkthrough, and subjective approval of mouse sensitivity, floor detail, glass subtlety, strip balance, doorway presentation, and FOV remain human polish checks.
- Saving/loading constructed layouts, deletion, undo, rotation controls, vertical stacking, costs, power requirements, inventory consumption, and arbitrary room caps are intentionally outside this milestone.
- All player-created rooms currently share the source orientation and initial gravity direction. Independent gravity values are supported, but capsule/camera reorientation across rotated-room thresholds remains deferred.
- Glass transmission is an intentionally nonphysical URP approximation. It does not provide caustics, bounced light, physical absorption, refraction of later transparent objects, or accurate glass shadows.
- Each room retains 28 distributed, shadow-free spot emitters embedded in its strips. Forward+ avoids the old per-object light limit, but very large player-built layouts will eventually require visibility-aware light budgeting.
- The green preview is a lightweight unlit line representation. It deliberately contains no filled panes, floor, doorway preview, collision, gravity, or illumination.
- The validation fixture was not rebuilt for this milestone. Its separate-room lighting, transmitted-light receiver, and opaque-blocker visual checks remain available for a future lighting-focused pass.
- Development captures and the executable include Unity's Development Build behavior by design.
