# What Light Remains

This repository contains the Unity 6 foundation prototype for **What Light Remains**: one authored, playable glass room that the player can expand into a connected structure at runtime, plus reusable room, player, lighting, input, and HUD architecture for later milestones.

## Open and play

1. Open this folder with Unity `6000.5.8f1`.
2. Open `Assets/WhatLightRemains/Generated/Scenes/Foundation.unity`.
3. Enter Play Mode.

The current Windows development build is available at `Build/Windows/WhatLightRemains.exe`.

Controls:

- `WASD`: move at 3 m/s
- Mouse: look
- `Space`: jump approximately 1 m
- `C`: enter or cancel Create mode while the cursor is captured
- Look at an available side wall in Create mode: preview an exactly snapped green room outline
- Left click with a valid preview: create one room and open every shared-face doorway
- `Escape`: release the cursor
- Left click while released: recapture the cursor
- Glass opacity: press `Escape`, then drag the upper-right slider (0–100%; 3.5% default)

## Foundation contents

- `CubeRoom.prefab`: a self-contained 8 m × 8 m × 8 m room with external 0.1 m boundaries, dark structural base rails at every glass/floor seam, eight housed emissive strips with brighter visible cores, and 28 shadow-free spot emitters embedded inside those strips. Four floor-level emitters sit within the vertical-strip geometry, while the remaining sources follow the vertical and ceiling strip lengths instead of projecting from the room center. The very wide 170° cones and 12 m range keep their falloff boundaries outside the playable floor.
- Runtime room construction: each play begins with exactly one sealed authored room. `CubeRoomClusterGenerator` is the authoritative logical occupancy registry; player-created rooms snap to exact 8 m grid cells without accumulated transform drift. A successful placement connects every complete shared side face, including loop-closing and multi-neighbor placements. Each pair receives matching centered 2 m × 2.4 m floor-level doorways, while one side owns the segmented glass, emissive doorway frame, split base rails, and colliders so no coplanar duplicate remains.
- Create-mode presentation: `RoomCreationController` targets only the first logical boundary of `PlayerRoomTracker.CurrentRoom`. Valid unconnected side walls show one nonphysical 12-edge green wireframe using `RoomPreview.mat`; floors, ceilings, connected walls, occupied cells, open space, and distant geometry through glass do not become placement targets. The bottom-left HUD label switches between `Press C to create a room` and `Left-Click to finalize placement`.
- Surface presentation: generated high-definition base-color and normal textures give the floor a dark mineral-composite finish. The cleaner glass uses a custom unlit transmission shader with sparse surface marks, slight adjustable translucency, and broad screen-space distortion suggesting thick glass, without receiving room lights, casting shadows, or producing reflection-like highlights. The floor artwork keeps a visible 4 × 4 slab grid across the full 8 m floor, so each pictured slab spans approximately 2 m × 2 m.
- `Player.prefab`: a CharacterController-based first-person rig, explicit starting-room assignment, 75° gameplay camera, and 55° ViewModel overlay camera.
- `Hotbar.prefab`: a presentation-only, resolution-scaling eight-slot uGUI hotbar, the Create-mode instruction label, and a compact runtime glass-opacity tuning panel. The opacity slider updates all authored and newly created room glass without modifying the shared material asset.
- `Foundation.unity`: the only enabled build scene, containing exactly one sealed authored primary room, one player rig, and one HUD. No neighbors are generated at startup.
- `Validation.unity`: a build-excluded fixture with a second rotated room, independent lighting/gravity settings, an external receiver, and an opaque shadow blocker.

The player can walk through every created doorway, and occupancy tracking transfers the active room as the capsule crosses the threshold. Placement then targets the newly occupied room. Doorway frames share the room lighting state and emit more softly than the main strips. Supporting spot emitters have shadows disabled; light budgeting will matter as player-built structures grow.

Runtime, editor, and test code is organized under `Assets/WhatLightRemains` in separate assembly definitions. Generated assets are checked in and do not require the builder at runtime.

## Regeneration and builds

Unity exposes these menu commands:

- `Tools > What Light Remains > Build Foundation`
- `Tools > What Light Remains > Build Windows 64`
- `Tools > What Light Remains > Build Validation Fixture (Windows 64)`

`FoundationBuilder.BuildAll` is safe to rerun. It updates only the project-owned generated assets and required foundation settings, preserves unrelated build-scene entries, and leaves the validation scene disabled in the normal player build.

## Package versions

The manifest pins the Unity `6000.5.8f1`-compatible package set resolved by the editor: URP `17.5.0`, Input System `1.20.0`, uGUI `2.5.0`, and Test Framework `1.7.0`. These supersede the older baseline versions named in the original plan (`17.0.1`, `1.12.0`, `2.0.0`, and `1.4.2`) because Unity 6.5 resolves the matching package line for this editor release.

See [Docs/FoundationVerification.md](Docs/FoundationVerification.md) for the current validation status, required visual checks, and known prototype limitations.
