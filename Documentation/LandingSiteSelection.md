# Landing-site selection

Milestone 3 ends with a confirmed position and heading. It does not start the dropship arrival, construction, inventory, or survival simulation. The planet's existing 4K appearance maps, water shading, and 45–210% zoom remain in use.

## Flow and controls

NEW COLONY immediately shows `Loading Exo-planet...`. An accepted globe click gives a brief screen-space pulse and rotates the captured geographic point toward the fixed camera. Land clicks select a region; water clicks can center the view without replacing that selection. Manual dragging interrupts centering. The selected flag has a separate fixed geographic anchor and camera-facing visual; its edge indicator represents a hidden or off-screen selection.

CONTINUE becomes available after selecting land. It immediately shows `Loading Landing Site...`, then waits for numerical generation, terrain, colliders, materials, camera, and controls. A failed survey offers BACK TO PLANET on the same planet. Loading text and its unscaled animation have their own presentation above the fading black overlay. Held input is released before destination interaction is enabled.

| Surface control | Action |
| --- | --- |
| WASD / arrows | Pan relative to the camera's horizontal heading |
| Wheel | Smoothly tilt the overhead view; button-owned scrolling is ignored |
| Shift + wheel | Smooth zoom in/out without changing tilt or heading; either Shift key works |
| Middle-drag | Grab and pan horizontally on the X/Z plane; heading and tilt stay unchanged |
| Q / E | Turn the camera heading by exactly −45° / +45° per press |
| + / − | Smooth keyboard zoom; the main keyboard's = key and numpad +/− are supported |
| Left-click valid ground | Lock or replace the landing candidate |
| R / Shift+R | Turn the preview ±45 degrees; revalidate a locked candidate |
| Right-click / Esc | Clear an unconfirmed candidate and resume positioning |
| Home | Restore the initial survey focus |
| LAND HERE | Revalidate and confirm the locked candidate |
| BACK TO PLANET | Restore the planet, selection, rotation, and zoom |

The surface camera has nominal 95–1,250 m zoom, a requested initial distance of 730 m, and 42–78° pitch. One aspect-dependent distance limit reserves a ground-facing frustum that fits the generated survey at every allowed heading and pitch. On the default 2 km survey at 16:9, this tightens the maximum and initial distance to approximately **500 m**. The same limit applies throughout camera turns and tilts, so the wheel does not also change zoom. Panning stays inside the survey, and the lens maintains at least 24 m terrain clearance. Confirmation freezes placement editing, leaves the marker visible, and keeps the camera and BACK TO PLANET available. Choosing another planet region clears the previous surface, candidate, and confirmation.

## Geography and coordinates

`SurfaceGenerationSettings` records the prototype defaults; `SurfaceParameters` is its serializable snapshot. The generator version is **`meridian-surface-4`**, and setup records use `meridian-setup-1`. Version 4 substantially flattens the terrain while retaining dense tree groves; its generated surface differs from earlier versions for the same planet location. Start a fresh setup to see revised geography.

One Unity unit is one metre. Each colony has a frozen tangent frame anchored to the selected unit direction: local +X is east, +Z is north, and +Y is height. With mapping radius **30,000 m**, the geographic direction at logical `(x,z)` is:

```text
normalize(anchor + east * x / mappingRadius + north * z / mappingRadius)
```

This is a gnomonic projection without visible planetary curvature. Near the exact poles, a deterministic reference axis replaces the usual east calculation; the resulting basis is recorded once. Longitude continues to wrap through planet-local directions. Planet conventions remain north +Y, longitude zero toward +Z, and positive longitude toward +X.

Physical broad elevation uses the graph elevation multiplied by **450 m**, independently of the globe's visual relief. The previous scale was 1,200 m; terrain and drainage elevations share the reduced scale. Sea level is zero. Every terrain tile shares the same height datum: minimum −400 m and 2,400 m height range. Shared graph climate supplies biome character. Broad hills and flat-topped shelves now use a configurable **12 m relief amplitude** (previously 100 m) with **650 m landform spacing**, plus much smaller fine noise. These are shape parameters, not guarantees that every hill has exactly that height or spacing; climate, neighboring forms, waterways, and colony shaping affect the final terrain.

A second landform scale adds gentle undulations with **2.5 m nominal height** (about 1.75–3.1 m, previously 13–23 m) and **210 m spacing**. It applies across dry biomes, including plateau shoulders, while preserving the protected building core and mapped shorelines. Flat gaps remain between hills; local roughness stays small. Placement limits are unchanged: the actual heightfield is flatter.

Ocean and lake membership use the planet's smoothed, bilinear surface boundary and nearest water category. Physical rivers instead follow authoritative downstream graph edges, with configurable **9–24 m full widths**; the globe's exaggerated river bands are not copied into surface waterways. Connected lake basins share a filled-drainage water elevation, and river elevations follow their drainage route. Terrain channels and clipped water meshes use this same local sampler.

## Regions, tiles, and readiness

A selected gameplay region owns the colony frame and survey state. Rendering tiles are subdivisions of that frame. The initial survey is **2 km × 2 km**, comprising four 1 km tiles at integer addresses `(-1,-1)`, `(0,-1)`, `(-1,0)`, and `(0,0)`. Each has a **513 × 513** heightmap, **128 × 128** five-layer blend map, and water geometry sampled at 257 × 257 before shoreline clipping. The layers are grass/forest soil, sand, stone, snow, and damp earth.

`SurfaceWorldData.GenerateTile(address, cancellation)` is the extension API. It returns a neighbor's numerical data without modifying the active survey or moving its origin. Heights derive from integer global sample coordinates, so shared borders are identical. Generation uses global logical coordinates and stable hashes, not a random sequence dependent on tile order. Terrain neighbors are connected after main-thread creation.

The generator searches for a dry site and shapes a fixed plain or plateau with a **260 m inner radius**, **300 m transition**, and default **3 m rise** above the surveyed local height. This replaces the previous smaller, more elevated plateau. It protects mapped water and drainage channels. Fine variation on the plateau is reduced to keep its broad interior useful. A 90 m clearing around the fixed plain center provides landing access; moving the preview never reshapes terrain.

Readiness requires at least **90,000 m²** of connected dry terrain with sampled slopes no greater than **5°**, plus a fully usable square at least **200 m** across. Measurement uses 10 m cells, shore clearance, connected components, and an all-usable interior-square calculation. This measures terrain potential: future-clearable vegetation is allowed in the wider buildable area. All trees, rocks, and deposits still obstruct the immediate landing footprint. Narrow islands or regions divided by water can fail with a recoverable message; generation does not reseed or relocate the selected region.

## Placement, objects, and ownership

Default hull dimensions are **14 × 26 m**, with **18 m rear deployment access** and **5 m safety margin**. The complete tested rectangle is therefore **24 × 54 m**, rotated with the ship. Checks sample it at up to 2 m spacing and reject water, boundaries, obstructions, slopes above 7°, or height variation above 1.8 m. Height and slope checks use the generated heightfield. The preview remains level and is revalidated on rotation and confirmation.

Rocks and deposits originate from stable 18 m logical cells, with deterministic jitter and half-open tile ownership. IDs include the seed, quantized region direction, cell address, and type. Neighbor enumeration includes a margin before ownership filtering. Reloading a tile preserves object types and positions. Iron, copper, and ice remain prototype resource types.

Trees form biome-dependent groves with default **320 m spacing** and **125 m nominal radius**, varied deterministically per grove. Trees use an independent **11 m logical spacing**, denser than the previous 18 m spacing. Individual trees have **3–5 m radii** and **12–20 m recorded heights**. Each tree retains its own obstruction and resource identity, plus a stable grove ID and a prototype wood amount of **29–80 units**. `TreeGroveData` aggregates member counts and wood amounts through `SurfaceGenerator.CollectTreeGroves`. Totals include only the supplied, available generated members; a partially generated grove does not claim the resources of unloaded neighbors. Its group ID and logical anchor stay fixed when more members become available. This establishes resource data and survey annotations; harvesting mechanics, extraction, inventory, and resource consumption are not implemented.

Trees, rocks, deposits, ground textures, and the recognizable dropship are procedural placeholder art. Trees and rocks are batched into spatial meshes. Ground layers use seamless **1024 × 1024 albedo, normal and surface maps**, with 11 mip levels, anisotropic filtering and an 8 m repeat. The grass layer includes 28,000 tapered green and dry leaves per tile, while earth, sand, stone and snow include smaller mineral/grain features. Wrapped detail stamps and matching height-derived normals add physical surface detail without affecting placement geometry. The surface maps control occlusion and smoothness; broader soil patches also vary the terrain blend. The 15 RGBA textures use about **80 MiB** of GPU pixel data including mipmaps, up from 20 MiB. Creation yields in batches, and every texture is released with the scene. Resource/water annotations are decorative and bounded in number. Surface daylight uses a 32° directional light and lower ambient illumination to make slopes, shelves, and larger trees readable. The saved URP asset uses a 1,600 m shadow distance with four cascades.

The in-memory `SetupSession` owns the geographic data, one reusable planet mesh/material/texture cache, surface numerical data, and versioned setup record. The landing record includes the ground height at its center independently of the level ship's support elevation. This permits return to the same globe without duplicating 4K maps or requiring released appearance buffers. Surface TerrainData, meshes, materials, textures, and colliders belong to the scene and are destroyed on exit; numerical data remains for the same-region return. Workers are cancellable, and region revisions reject stale results. There is no disk save/load or general terrain-streaming system yet.

## Focused validation and limits

See [Validation.md](Validation.md) for dated checks and generation measurements. Measurements are dated by surface version; earlier version 1 and 2 results do not describe current terrain, grove counts, or performance. The focused validator covers deterministic tiles, adjacent borders/corners, object and grove identity/ownership, projection, landing-footprint rejection, and cancellation. Broad manual playtesting remains with the user; a representative numerical region does not certify every biome or camera position.

The projection and scales are prototype mappings, not an Earth-scale simulation. Local shaping adds a colony plain to the shared regional geography; it does not reproduce an erosion simulation or certify every surrounding tile for construction. The extension API establishes continuity, but expansion unlocks, distant streaming, arrival animation, and colony gameplay remain later work.
