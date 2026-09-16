# Landing-site selection

Milestone 3 ends with a confirmed position and heading. It does not start the dropship arrival, construction, inventory, or survival simulation. The planet's existing 4K appearance maps, water shading, and 45–210% zoom remain in use.

## Flow and controls

NEW COLONY immediately shows `Loading Exo-planet...`. An accepted globe click gives a brief screen-space pulse and rotates the captured geographic point toward the fixed camera. Land clicks select a region; water clicks can center the view without replacing that selection. Manual dragging interrupts centering. The selected flag has a separate fixed geographic anchor and camera-facing visual; its edge indicator represents a hidden or off-screen selection.

CONTINUE becomes available after selecting land. It immediately shows `Loading Landing Site...`, then waits for numerical generation, terrain, colliders, materials, camera, and controls. A failed survey offers BACK TO PLANET on the same planet. Loading text and its unscaled animation have their own presentation above the fading black overlay. Held input is released before destination interaction is enabled.

| Surface control | Action |
| --- | --- |
| WASD / arrows | Pan relative to the camera's horizontal heading |
| Wheel | Smooth zoom; button-owned scrolling is ignored |
| Middle-drag | Orbit and change overhead pitch |
| Left-click valid ground | Lock or replace the landing candidate |
| R / Shift+R | Turn the preview ±45 degrees; revalidate a locked candidate |
| Right-click / Esc | Clear an unconfirmed candidate and resume positioning |
| Home | Restore the initial survey focus |
| LAND HERE | Revalidate and confirm the locked candidate |
| BACK TO PLANET | Restore the planet, selection, rotation, and zoom |

The surface camera starts 730 m from its focus, with nominal 95–1,250 m zoom and 42–78° pitch. Framing and terrain clearance can further constrain its pose. Confirmation freezes placement editing, leaves the marker visible, and keeps the camera and BACK TO PLANET available. Choosing another planet region clears the previous surface, candidate, and confirmation.

## Geography and coordinates

`SurfaceGenerationSettings` records the prototype defaults; `SurfaceParameters` is its serializable snapshot. The generator version is `meridian-surface-1`, and setup records use `meridian-setup-1`.

One Unity unit is one metre. Each colony has a frozen tangent frame anchored to the selected unit direction: local +X is east, +Z is north, and +Y is height. With mapping radius **30,000 m**, the geographic direction at logical `(x,z)` is:

```text
normalize(anchor + east * x / mappingRadius + north * z / mappingRadius)
```

This is a gnomonic projection without visible planetary curvature. Near the exact poles, a deterministic reference axis replaces the usual east calculation; the resulting basis is recorded once. Longitude continues to wrap through planet-local directions. Planet conventions remain north +Y, longitude zero toward +Z, and positive longitude toward +X.

Physical broad elevation uses the graph elevation multiplied by **1,200 m**, independently of the globe's visual relief. Sea level is zero. Every terrain tile shares the same height datum: minimum −400 m and 2,400 m height range. Shared graph climate supplies biome character; deterministic local undulations add detail.

Ocean and lake membership use the planet's smoothed, bilinear surface boundary and nearest water category. Physical rivers instead follow authoritative downstream graph edges, with configurable **9–24 m full widths**; the globe's exaggerated river bands are not copied into surface waterways. Connected lake basins share a filled-drainage water elevation, and river elevations follow their drainage route. Terrain channels and clipped water meshes use this same local sampler.

## Regions, tiles, and readiness

A selected gameplay region owns the colony frame and survey state. Rendering tiles are subdivisions of that frame. The initial survey is **2 km × 2 km**, comprising four 1 km tiles at integer addresses `(-1,-1)`, `(0,-1)`, `(-1,0)`, and `(0,0)`. Each has a **513 × 513** heightmap, **128 × 128** five-layer blend map, and water geometry sampled at 257 × 257 before shoreline clipping. The layers are grass/forest soil, sand, stone, snow, and damp earth.

`SurfaceWorldData.GenerateTile(address, cancellation)` is the extension API. It returns a neighbor's numerical data without modifying the active survey or moving its origin. Heights derive from integer global sample coordinates, so shared borders are identical. Generation uses global logical coordinates and stable hashes, not a random sequence dependent on tile order. Terrain neighbors are connected after main-thread creation.

The generator searches for a dry site and shapes a fixed plain or plateau with a 260 m inner radius and 220 m transition. It protects mapped water and drainage channels. A 90 m clearing around the fixed plain center provides landing access; moving the preview never reshapes terrain.

Readiness requires at least **90,000 m²** of connected dry terrain with sampled slopes no greater than **5°**, plus a fully usable square at least **200 m** across. Measurement uses 10 m cells, shore clearance, connected components, and an all-usable interior-square calculation. This measures terrain potential: future-clearable vegetation is allowed in the wider buildable area. All trees, rocks, and deposits still obstruct the immediate landing footprint. Narrow islands or regions divided by water can fail with a recoverable message; generation does not reseed or relocate the selected region.

## Placement, objects, and ownership

Default hull dimensions are **14 × 26 m**, with **18 m rear deployment access** and **5 m safety margin**. The complete tested rectangle is therefore **24 × 54 m**, rotated with the ship. Checks sample it at up to 2 m spacing and reject water, boundaries, obstructions, slopes above 7°, or height variation above 1.8 m. Height and slope checks use the generated heightfield. The preview remains level and is revalidated on rotation and confirmation.

Objects originate from stable 18 m logical cells, with deterministic jitter and half-open tile ownership. IDs include the seed, quantized region direction, cell address, and type. Neighbor enumeration includes a margin before ownership filtering. Reloading a tile preserves object types and positions. Iron, copper, and ice are prototype resource types; extraction is not implemented.

Trees, rocks, deposits, ground textures, and the recognizable dropship are procedural placeholder art. Trees and rocks are batched into spatial meshes. Terrain uses small repeating ground textures rather than stretching the globe image over the landscape. Resource/water annotations are decorative and bounded in number.

The in-memory `SetupSession` owns the geographic data, one reusable planet mesh/material/texture cache, surface numerical data, and versioned setup record. The landing record includes the ground height at its center independently of the level ship's support elevation. This permits return to the same globe without duplicating 4K maps or requiring released appearance buffers. Surface TerrainData, meshes, materials, textures, and colliders belong to the scene and are destroyed on exit; numerical data remains for the same-region return. Workers are cancellable, and region revisions reject stale results. There is no disk save/load or general terrain-streaming system yet.

## Focused validation and limits

The focused numeric check passed for seed 73129 at direction approximately `(0.57,-0.62,0.53)`: **9.94 seconds** initial surface generation, **1,128,800 m²** connected buildable terrain, a **560 m** usable square, and **7,417** stable objects. It used validation-only 1024 × 512 planet maps and the production 513 × 513 surface resolution.

Checks covered initial and adjacent-tile borders/corners, regeneration after a different tile order, object identity/ownership, polar and seam projection, the full footprint at eight headings, boundary/obstruction rejection, and cancellation. This is one representative numerical region, not a visual or performance certification of every biome. See [Validation.md](Validation.md) for the current integration evidence. Broad manual playtesting remains with the user.

The projection and scales are prototype mappings, not an Earth-scale simulation. Local shaping adds a colony plain to the shared regional geography; it does not reproduce an erosion simulation or certify every surrounding tile for construction. The extension API establishes continuity, but expansion unlocks, distant streaming, arrival animation, and colony gameplay remain later work.
