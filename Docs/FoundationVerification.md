# Foundation Verification Report

The randomized runtime-cluster, connected-doorway, opacity-control, embedded strip-emitter, and visible base-rail revision has passed its automated Unity validation. A full human walkthrough remains pending.

## Current status

The generated assets and scenes were rebuilt in Unity `6000.5.8f1` before the recorded test runs:

| Check | Result | Evidence |
| --- | ---: | --- |
| EditMode | Passed — 19/19 | `TestResults/editmode-final.xml` |
| PlayMode | Passed — 7/7 | `TestResults/playmode-final.xml` |
| Built-player visual checks | Passed — targeted 1920×1080 | `Artifacts/Final-Room-Lighting.png` and `Artifacts/Final-Doorway-Seam.png`; the embedded floor-end illumination has no old curved cutoff, and the dedicated base rail reads continuously along the glass/floor seam |
| Windows x64 development build | Passed — Unity build pipeline | `Build/Windows/WhatLightRemains.exe`; 201,536,346-byte successful build output |
| Build-excluded validation fixture | Pending | Rebuild `Build/Validation/WhatLightRemainsValidation.exe` |
| Built-player startup | Pending | Smoke-test the rebuilt Foundation scene |
| Cursor release/recapture | Passed — automated | Covered by `TestResults/playmode-final.xml`; physical mouse/keyboard smoke check remains manual |

The EditMode suite covers rotated gravity direction and magnitude, room/lighting independence, exact 8 m room boundaries, exact eight-strip placement, the four configurable three-piece doorway variants and emissive frames, prefab ownership boundaries, surface texture import and material wiring, full-resolution URP opaque-scene sampling, the 0–100% opacity control, distributed shadow-free strip emitters, input actions, camera/HUD/scene wiring, Forward+, runtime-cluster configuration, and the separate validation fixture.

The PlayMode suite covers explicit starting-room ownership, creation of exactly three additional rooms, connected non-overlapping random layouts on the 8 m grid, doorway pairing on every shared face, single-owner boundary deduplication, open-center/solid-jamb/solid-header collision probes, player traversal and occupancy transfer, the runtime glass-opacity shader override, disabled generated-room light shadows, 3 m/s cardinal and diagonal movement, the approximately 1 m jump apex, airborne jump rejection, landing, ceiling handling, sustained sealed-wall containment, and cursor state transitions.

## Visual review

The targeted built-player captures confirm the continuous base rail and floor-end strip illumination in the regenerated room. A 1280×720 pass and full walkthrough remain manual. Full acceptance should confirm:

- Gameplay at 1920×1080 and 1280×720 starts with one authored primary room plus exactly three runtime-generated rooms, while retaining a readable floor, hand/device, and correctly scaled bottom-center hotbar against a black environment.
- Across fresh play sessions, generated rooms form a connected random layout with exact 8 m face-adjacent spacing and no overlapping grid cells.
- The 8 m floor uses generated high-definition base-color and normal detail while retaining a clearly readable 4 × 4 slab grid across its full surface.
- At every shared seam, both rooms report the connection and expose the same centered 2 m × 2.4 m doorway while exactly one room owns the segmented glass, frame, and colliders.
- Glass remains highly transmitting and free of point-light or reflection-like hotspots, with only sparse marks, adjustable slight translucency, and broad thick-glass distortion against geometry beyond it.
- The exterior view shows four continuous vertical strips and four continuous ceiling-perimeter strips, each with a visible dark housing and bright core; there are no floor-perimeter strips or proxy-light hotspots.
- The upper-right slider ranges from 0% to 100% opacity, starts at 3.5%, responds while the cursor is released, and updates every room consistently.
- Generated rooms retain readable emissive strips and direct illumination from 28 broad spot emitters embedded inside the eight visible strips. Four sources sit inside the floor ends of the vertical strips; 170° cones and 12 m ranges move visible cone/range edges beyond the room floor, and all emitter shadows remain disabled.
- Every glass/floor seam has a 0.12 m structural base rail using a dedicated mid-charcoal brushed-metal material. Connected boundaries replace the full rail with two side pieces so the 2 m doorway remains visually and physically open.
- Doorway frames emit at a lower intensity than the primary room strips and follow the owning room's lighting enabled state.
- With room lighting disabled, the emissive cores, floor, and lit viewmodel darken while the screen-space hotbar remains readable.
- In the validation fixture, light reaches the external receiver through the glass, changing the source room does not affect the rotated room, and the opaque blocker casts the expected shadow.
- The rebuilt executable launches into the intended Foundation scene and presentation.

## Manual-only and limitations

- Physical keyboard/mouse injection for WASD, mouse-look, and Space remains a manual smoke check. Automated motor/jump coverage, Input System bindings, and Escape/click cursor transitions were revalidated for this revision.
- A human 360-degree walkthrough and subjective approval of movement feel, mouse sensitivity, floor detail, glass subtlety, strip visibility, lighting balance, and FOV remain manual polish checks.
- Glass transmission is an intentionally nonphysical real-time approximation. Its custom unlit transparent pass mixes a slightly offset sample of URP's opaque-scene texture with sparse surface detail; Fresnel or specular reflections, absorption, caustics, bounced light, and physically accurate glass shadows are not implemented.
- The thick-glass effect is deliberately subtle and screen-space. It can distort opaque geometry already present in URP's camera opaque texture, but cannot refract later transparent objects and may be nearly invisible against the black void.
- All rooms use distributed, shadow-free spot emitters rather than point lights or center-aimed proxies. This is URP's real-time approximation of line emission: sources are embedded in the visible geometry and include the floor intersections. The 112-light four-room cluster still requires budgeting before larger clusters are visible.
- Runtime-generated rooms are traversable through their shared doorways. Rotated-room generation and gravity-transition reorientation remain out of scope; all rooms in the current randomized cluster share the primary room's orientation and gravity direction.
- The player consumes a rotated room's gravity vector and projects movement onto that room's horizontal plane, but physically reorienting the capsule/camera while traversing between rotated rooms is intentionally deferred with the rest of threshold traversal.
- New validation captures and the development executable will include Unity's Development Build watermark by design.
- Unity 6 automatically owns and seeds its global URP default Volume Profile. The durable no-effects configuration is enforced at the scene cameras: post-processing is disabled on both cameras, no scene Volume exists, the PC pipeline has no assigned default profile, and the renderer has no active SSAO feature.
