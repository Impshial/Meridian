# Procedural planet selection

Milestone 2 targets naturalistic orbital geography and balanced habitable planets with approximately 25–45% continental land coverage. The background is black, the camera is fixed, and the only screen controls are BACK and disabled CONTINUE. There is no surface/colony stage.

## Controls and transitions

NEW COLONY blocks repeat activation and fades the complete menu to black over 0.5 seconds. A new seed is generated for that visit, numeric generation runs on a cancellable worker, and Unity creates the completed mesh, collider, maps, and material on the main thread. The completed screen fades in over 0.5 seconds; pointer/submit release gates activation. Opening PlanetSelection directly in Play Mode follows the same hidden-generation entry.

Left-drag rotates the planet around camera-relative axes; no camera transform changes, idle rotation, pan, inertia, or keyboard viewing controls are used. A 7-pixel threshold at 1080p scales with viewport height and tracks maximum excursion. UI owns an entire gesture begun on a button. Focus loss clears captured input. Scroll uses Input System 1.20's normalized ticks, with a 120-unit divisor only if native Windows scroll mode is explicitly selected.

Zoom changes perspective field of view. Framing starts at 63% of viewport height, with a nominal 45–80% range constrained by aspect ratio, bottom controls, and the raised flag's envelope. All limits apply again on resolution changes. Wheel input over UI is ignored. The camera stays at (0, 0, -4), looking toward the origin.

A legitimate click samples the visible globe collider and converts the hit into planet-local coordinates. All land, including snow and mountains, is eligible. Ocean, lake, river, frozen water, and background clicks preserve the existing selection. One amber 3D flag inserts its pole over 0.45 seconds, remains attached during rotation/animation, and uses normal depth occlusion. A replacement click cancels and restarts its planting animation.

BACK fades out, unloads the visit, and fades in the original menu. It is unavailable during generation/transitions. CONTINUE has no callback and remains disabled. On generation failure, the transition returns to MainMenu and logs the cause.

## Geographic representation and budget

- Generator version: `meridian-planet-1`. Reproducibility requires the same seed, captured `PlanetParameters`, and generator version; no dependency on Unity's global random state or frame timing.
- Geography: closed subdivided icosphere graph, level 6, 40,962 samples / 81,920 graph faces. Its graph is CPU data, not rendered terrain.
- Rendering and collision: level-4 icosphere, 2,562 vertices / 5,120 triangles. Relief is capped at 0.5% of the unit radius by default. Forests are regional surface detail, not tree objects.
- Maps: three 1024 × 512 RGBA32 textures: land color with bathymetry in alpha, object-space normals, and packed water/classification/temperature. Six MiB GPU pixel payload total; no mip chains. CPU geographic fields and map arrays are retained for shared sampling and future continuity.
- The shader samples maps at LOD 0 with bilinear filtering, Repeat U and Clamp V. Picking implements the same texel-center bilinear water mask and 0.5 threshold. Thus visible blue/icy water does not use a separate sea-level-only picking rule.
- `PlanetData` owns the seed, parameters, elevation, moisture, temperature, terrain biomes, filled drainage heights, downstream links, accumulation, lakes/rivers, and raster fields. `PlanetData.Sample(localUnitDirection)` is the common geographic interface. `PlanetSelection` retains the seed/version, normalized local direction, and sampled geographic character in memory only.

Continents combine domain-warped 3D noise and a seed-dependent sea-level quantile. Interior ridge belts add mountains above broad low-relief land. Moisture, temperature, latitude, and elevation determine biome blends. A stable priority flood resolves drainage over depressions; reverse flow accumulation identifies connected river paths. Large bounded depressions become lakes. Deterministic basin/climate corrections supply missing feature examples, followed by drainage recalculation where needed. This is an orbital approximation, not an erosion or climate simulator.

River width has a map-resolution minimum (approximately 1.8 equatorial texels across), so channels remain connected and recognizable at orbital zoom. It is a symbolic overview width, not a physical river-width measurement. The inspected seeds generated in roughly 6–8 seconds in the editor and 9–14 seconds in the Windows player on the validation machine. The black generation interval lasts until completion; there is no additional fixed wait or loading screen UI. Generation latency is a current prototype limitation.

## Coordinates and future terrain

North is +Y. Longitude zero points along +Z, and positive longitude turns toward +X. For a normalized local direction, longitude = atan2(x, z), latitude = asin(y), U = longitude / (2π) + 0.5, and V = latitude / π + 0.5. U wraps. Exact north/south directions are shared by all longitudes at the poles. The spherical graph has ordinary neighbours across the longitude seam and near both poles; rasterization uses the same directions as the shader and picker.

Selection is a general region, not an exact dropship/building footprint. Broad plains, valley floors, and low-relief interiors preserve room for future connected buildable terrain. A later local generator must consume this geography to retain coastlines, watercourses, terrain character, and vegetation while providing usable colony space. No local map dimensions, construction tests, or measured flat-area guarantee exist in this milestone.

## Ownership and authoring

The scene owns its generation task, data, interaction state, and flag. Unloading cancels pending work and releases generated mesh, collider references, textures, and material. The temporary transition overlay survives only the scene swap/fade. Editor authoring and validation stay under `Assets/_Meridian/Editor`; the player has no editor service or network listener.

The original foundation authoring command is guarded against overwriting milestone 2. The planet authoring command incrementally wires the existing NEW COLONY button and creates this milestone's saved assets. Neither authoring command runs automatically when opening or playing the project.
