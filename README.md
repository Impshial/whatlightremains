# What Light Remains

This repository contains the Unity 6 foundation prototype for **What Light Remains**: one authored, playable glass room that grows into a four-room cluster at runtime, plus reusable room, player, lighting, input, and HUD architecture for later milestones.

## Open and play

1. Open this folder with Unity `6000.5.8f1`.
2. Open `Assets/WhatLightRemains/Generated/Scenes/Foundation.unity`.
3. Enter Play Mode.

The current Windows development build is available at `Build/Windows/WhatLightRemains.exe`.

Controls:

- `WASD`: move at 3 m/s
- Mouse: look
- `Space`: jump approximately 1 m
- `Escape`: release the cursor
- Left click while released: recapture the cursor
- Glass opacity: press `Escape`, then drag the upper-right slider (0–100%; 3.5% default)

## Foundation contents

- `CubeRoom.prefab`: a self-contained 8 m × 8 m × 8 m room with external 0.1 m boundaries, dark structural base rails at every glass/floor seam, eight housed emissive strips with brighter visible cores, and 28 shadow-free spot emitters embedded inside those strips. Four floor-level emitters sit within the vertical-strip geometry, while the remaining sources follow the vertical and ceiling strip lengths instead of projecting from the room center. The very wide 170° cones and 12 m range keep their falloff boundaries outside the playable floor.
- Runtime room cluster: each play begins with the authored primary room and creates exactly three additional prefab instances on a fresh random, connected layout. Rooms use exact 8 m face-adjacent spacing. Every shared face becomes a matching, centered 2 m × 2.4 m floor-level doorway in both rooms; one side owns the shared segmented glass, emissive doorway frame, split base rails, and colliders so no coplanar duplicate remains.
- Surface presentation: generated high-definition base-color and normal textures give the floor a dark mineral-composite finish. The cleaner glass uses a custom unlit transmission shader with sparse surface marks, slight adjustable translucency, and broad screen-space distortion suggesting thick glass, without receiving room lights, casting shadows, or producing reflection-like highlights. The floor artwork keeps a visible 4 × 4 slab grid across the full 8 m floor, so each pictured slab spans approximately 2 m × 2 m.
- `Player.prefab`: a CharacterController-based first-person rig, explicit starting-room assignment, 75° gameplay camera, and 55° ViewModel overlay camera.
- `Hotbar.prefab`: a presentation-only, resolution-scaling eight-slot uGUI hotbar plus a compact runtime glass-opacity tuning panel. The opacity slider updates all authored and generated room glass without modifying the shared material asset.
- `Foundation.unity`: the only enabled build scene, containing one authored primary room, one player rig, and one HUD; the three neighboring rooms are instantiated when play begins.
- `Validation.unity`: a build-excluded fixture with a second rotated room, independent lighting/gravity settings, an external receiver, and an opaque shadow blocker.

The player can walk through every generated doorway, and occupancy tracking transfers the active room as the capsule crosses the threshold. Doorway frames share the room lighting state and emit more softly than the main strips. Supporting spot emitters have shadows disabled to control the cost of the four-room view.

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
