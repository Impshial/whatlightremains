# What Light Remains

This repository contains the Unity 6 foundation prototype for **What Light Remains**: a playable 8 m glass room that the player can expand into a three-dimensional structure at runtime, with reusable room, arbitrary-gravity player, lighting, input, and HUD architecture.

## Open and play

1. Open this folder with Unity `6000.5.8f1`.
2. Run `Tools > What Light Remains > Build Foundation` after pulling a revision that changes generated assets.
3. Open `Assets/WhatLightRemains/Generated/Scenes/Foundation.unity`.
4. Enter Play Mode.

The current Windows development build is written to `Build/Windows/WhatLightRemains.exe`.

## Controls

- `WASD`: move at 3 m/s
- Hold left `Shift`: sprint at 6 m/s, including while the Create-mode rotation modifiers are held
- Mouse: look
- `Space`: jump approximately 1 m
- Hold `E` while aiming at the highlighted frame of an elevated opening: a fast accelerating grapple arc pulls the player to the exact center of the aperture; release before entering it to cancel
- `C`: enter or cancel Create mode while the cursor is captured
- Aim at an available side wall or ceiling in Create mode: preview an exactly snapped room
- Hold either `Ctrl` and use the mouse wheel: rotate the preview 90° per detent around the source room's local Y axis
- Hold `Ctrl` + either `Alt` and use the mouse wheel: rotate the preview 90° per detent around the source room's local Z axis
- Left click with a valid preview: create the room and configure all shared boundaries
- `Escape`: cancel Create mode and release the cursor
- Left click while released: recapture the cursor
- Glass opacity: press `Escape`, then drag the upper-right slider from 0% through 100% (3.5% default)

The bottom-center Create-mode hint shows `Hold Ctrl to Rotate`, changes to the active mouse-wheel axis while the modifier is held, and disappears after leaving Create mode. The existing bottom-left create/finalize prompt remains available.

## Room construction

`CubeRoomClusterGenerator` stores occupancy in exact `Vector3Int` cells relative to the authored primary room. Room roots are derived from cell volume centers and one of the 24 valid cardinal cube orientations, so repeated placement does not accumulate transform drift.

The primary room is the only room present at startup. In Create mode:

- Side placement supports every cardinal room orientation. When an existing room contributes a side wall, that wall supplies its centered 2 m × 2.4 m floor-level doorway and the exact world-space aperture is projected into the new room, regardless of the new room's gravity.
- The ceiling is a valid placement surface; the floor is not.
- Any contact involving a room's local Floor remains completely sealed and collidable. That sealed contact does not prevent other eligible contacts made by the same placement.
- Existing-ceiling/new-side contacts use the new side wall's normal doorway and project it into the existing ceiling. Two touching ceilings share one centered 2.4 m × 2.4 m opening.
- Each traversable connection owns one fixed authoritative aperture and one segmented physical boundary. Both rooms, preview, collision, lighting, targeting, and traversal use that same aperture; later topology rebuilds do not recenter it.
- Occupied cells, incompatible orientations, connected target faces, and atomically stale candidates are rejected.
- Any additional complete shared faces created by a placement are resolved at the same time, including loop-closing placements.

Rotation persists while Create mode remains active in the same source room. It resets when Create mode ends or the player's current room changes.

## Presentation and player architecture

- `CubeRoom.prefab` is a self-contained 8 m × 8 m × 8 m room with external 0.1 m boundaries, a 4 × 4 floor-tile texture layout, cleaner thick-glass transmission, structural floor/glass seam rails, and doorway/ceiling variants.
- `RoomGhostPreview` is a renderer-only copy of the exact room prefab. It uses separate low-opacity materials for glass, structure, and floor so internal door and frame details remain visible, plus a thin volume outline and centered 3D arrow pointing along the candidate room's local gravity direction. It contains no physics, lights, occupancy, room scripts, or gravity behavior.
- Exactly eight primary light strips remain on each room: four vertical corner strips and four ceiling-perimeter strips. Their real-time emitters are distributed within the strip geometry rather than at room center.
- Doorway frames use visible emission and embedded real-time emitters at exactly 50% of the room-strip power. Closed and non-owning boundary variants keep those lights disabled.
- `Player.prefab` uses a custom kinematic capsule aligned to its local Y axis rather than Unity's world-up `CharacterController`. A separate yaw/pitch hierarchy preserves first-person look while the physical body smoothly aligns to a new room's gravity over 0.35 seconds.
- Standard room gravity is exactly 32 ft/s² (`9.7536 m/s²`) along each room's local down direction. Jump velocity continues to derive from the active room's gravity strength.
- During gravity alignment, translation, gravity integration, and jumping pause; mouse look remains active.
- Floor-level openings remain ordinary walk-through doorways and never show an `E` prompt. When a real crossing enters a room with different gravity, the traversal controller first clears the frame in the source orientation, then aligns and settles the player inside the destination.
- Openings elevated relative to the current room's local floor can be targeted across the room and show exactly `Hold E to Traverse`. The same shared opening can therefore be walkable from one side and assisted from the other. Ladders and ladder-only triggers/input have been removed.
- Assisted traversal follows a collision-checked quadratic Bézier built from lerps. The 0.65-second approach accelerates into the opening and finishes with the capsule dead center in the clear aperture; the passage crossing runs at 12 m/s before a controlled 6 m/s landing.
- Every `CubeRoom` exposes an Inspector-visible `Power On` state plus `SetPower(bool)` and `TogglePower()`. Power is ON by default and independently controls that room's strip lights/emission and its side of shared doorway lighting without affecting gravity, collision, creation, or traversal.
- `Hotbar.prefab` contains the eight-slot presentation hotbar, Create-mode hints, and glass-opacity control. The UI remains readable independently of room lighting.
- `Foundation.unity` contains one authored primary room, one player rig, one HUD, and no generated neighbors.
- `Validation.unity` remains a build-excluded lighting and gravity fixture.

Runtime, editor, and test code is organized under `Assets/WhatLightRemains` in separate assembly definitions. Generated assets are checked in and remain playable without rerunning the builder.

## Regeneration and builds

Unity exposes these menu commands:

- `Tools > What Light Remains > Build Foundation`
- `Tools > What Light Remains > Build Windows 64`
- `Tools > What Light Remains > Build Validation Fixture (Windows 64)`

`FoundationBuilder.BuildAll` is idempotent: it updates only project-owned generated assets and required foundation settings, preserves unrelated build-scene entries, and keeps the validation scene disabled in the normal player build.

## Package versions

The manifest pins the Unity `6000.5.8f1`-compatible package set resolved by the editor: URP `17.5.0`, Input System `1.20.0`, uGUI `2.5.0`, and Test Framework `1.7.0`. These supersede the older baseline versions in the original foundation prompt because Unity 6.5 resolves the corresponding package line for this editor release.

See [Docs/FoundationVerification.md](Docs/FoundationVerification.md) for the automated coverage, hands-on matrix, and prototype limitations.
