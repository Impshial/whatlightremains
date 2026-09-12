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
- `Delete`: enter Delete mode; aim through the first face of the current room at a directly adjacent non-primary room
- Right click on a red-tinted room: delete that room and rebuild the remaining shared boundaries
- `M`: open or close the live 3D world map, initially centered on the player and aligned to the player's current room orientation
- Map controls: left-drag orbits the current focal point, middle-drag moves/pans the map, RMB levels the room under the center reticle, and the mouse wheel zooms. Orbit uses a small hysteresis zone to suppress wobble while still allowing horizontal/vertical axis changes during the same drag
- `C` while the map is open: smoothly recenter on the player without changing the current orbit or zoom
- `R` while the map is open: restore the exact player-centered view captured when the map opened
- `Escape`: close the map when it is open; otherwise cancel Create/Delete mode first, or open Pause from default gameplay
- Pause menu: resume, reset the generated world, return to the main menu, or quit to desktop

The gameplay cursor remains captured. Free mouse control is available only on the main menu, Pause menu, and 3D map interface.

The bottom-center Create-mode hint shows `Hold Ctrl to Rotate`, changes to the active mouse-wheel axis while the modifier is held, and disappears after leaving Create mode. Gameplay Create/Delete prompts are hidden while the map is open. Normal gameplay uses a larger plus reticle; map mode replaces it with a compact circular marker on the exact room-selection ray used by RMB.

## Player vitals and shared oxygen

The upper-left HUD has three 72-pixel circular indicators, stacked with 28-pixel left / 24-pixel top margins at the 1920×1080 Canvas reference size. Health is muted red with a heart, Hunger is amber with utensils, and Oxygen is cyan with an air symbol. The colored ring shows the current fraction clockwise from twelve o'clock, exposing the dark track as it empties. Centered white icons identify the reserves; there are no labels or numbers beside them. These circles follow the user's visual reference and subsequent clarification instead of the labeled horizontal bars described in the milestone document.

`PlayerVitals` on `Player.prefab` owns Health and Hunger. `WorldOxygenReserve` on the scene's **World Session** root owns the single shared Oxygen reserve, including disconnected rooms. Every value starts at **100 / 100**. Hunger means food remaining: full is fully fed and zero is empty. Oxygen is an abstract world supply with a placeholder capacity of 100, independent of room count, volume, and connectivity.

Inspector starting values and maxima are captured when the scene session initializes. Runtime `Current`, `Max`, and `Normalized` properties are read-only. Maxima must be finite and positive; malformed configuration repairs to 100. Nonfinite starting configuration repairs to full, and other starting values clamp to capacity. Inspector edits made during a running session apply to the next session. There are no runtime capacity upgrades.

All Add/Remove methods require finite nonnegative amounts and throw `ArgumentOutOfRangeException` for invalid input without changing state. Set methods accept any finite value and clamp it to `[0, maximum]`; NaN and infinities throw. Effective changes emit one `Action<float, float>` event with `(current, maximum)`: `HealthChanged`, `HungerChanged`, or `OxygenChanged`. No-op calls emit nothing. `ResetToStartingValues()` restores each owner's captured starting configuration.

Future systems can use explicitly supplied references:

```csharp
// Example only: no gameplay consumer runs these calls in this milestone.
void ApplySupplies(PlayerVitals player, WorldOxygenReserve world)
{
    player.RemoveHealth(12.5f); // AddHealth / SetHealth are also available.
    player.AddHunger(20f);     // RemoveHunger / SetHunger are also available.
    world.RemoveOxygen(0.25f); // AddOxygen / SetOxygen are also available.
}
// Read HealthCurrent, HealthMax, HealthNormalized; HungerCurrent, HungerMax,
// HungerNormalized; CurrentOxygen, MaxOxygen, OxygenNormalized.
```

`VitalsHudView` has explicit scene references to both owners. Enabling or rebinding reads their current snapshot and subscribes once; disabling or destroying the view removes its subscriptions. Gauges show in ordinary gameplay, Create/Delete, and traversal, and hide during map and Pause. The screen-space UI remains upright under rotated gravity and works without room lighting. Its graphics never block pointer input.

Direct Foundation play, New Game, and Reset World create fresh scene-owned values. Pause/map retain them; returning to MainMenu destroys the session. There are no persistent vitals globals, automatic drains, healing, damage, starvation, suffocation, death, gameplay penalties, or debug controls. Existing movement and construction remain available at zero.

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
- Every room has one fixed prefab-local West Device Wall. A new preview initializes its discrete yaw so that wall appears on the placement frame's left while preserving the source room's gravity; later room rotations carry the same wall with the cube. Its 10%-opaque smoked glass, dark metal frame, four rails, and 4 × 4 mounting-stud grid are clipped around authoritative apertures. The logical anchors provide atomic, physical-bounds-aware reservations for future devices, while their colored overlay remains hidden outside the room ghost.
- Regular room glass is authored at a fixed 5% opacity. Delete mode adds a reversible transparent red wall tint without outline geometry or changing opacity.
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
- `Hotbar.prefab` contains the eight-slot presentation hotbar, contextual creation/deletion hints, and the Pause menu. The UI remains readable independently of room lighting.
- The 3D map uses a separate perspective camera to render the live room hierarchy—including current apertures, separators, lighting, rotations, and Device Wall hardware—rather than maintaining a duplicate map model. A depth-priority cyan arrow identifies the player even through room geometry.
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
