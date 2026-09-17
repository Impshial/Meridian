# Meridian

Meridian is an exoplanet colony builder directed from an orbital command ship. Future gameplay will use an overhead/isometric camera in a three-dimensional colony and landscape.

Milestone 3 completes pregame landing selection. **NEW COLONY** immediately shows `Getting you an exo-planet...` and creates a fresh globe. Clicks smoothly center a region; land clicks plant a readable flag and enable **CONTINUE**. It shows `Loading Landing Site...` and opens the matching terrain survey. Lock a valid dropship position and choose **LAND HERE** to confirm. **BACK TO PLANET** restores the same globe, view and flag. LOAD COLONY, SETTINGS and QUIT remain inert placeholders. Menu brackets and amber labels appear only while the mouse hovers over an enabled button; keyboard focus does not highlight them.

## Open and run

- Open this repository root in **Unity 6000.5.8f1 (5cb7df797b7d)**, allow package imports to finish, then open `Assets/_Meridian/Scenes/MainMenu.unity` and press Play.
- Target: **Windows desktop, x86-64**. Build order: MainMenu, PlanetSelection, LandingSiteSelection.
- Rendering: **Universal Render Pipeline 17.5.0**, Linear color space, 3D editor mode.
- Input: **Input System 1.20.0** and `InputSystemUIInputModule`. The UI uses uGUI / TextMeshPro **2.5.0**.
- Screen: 1920 × 1080 default, Fullscreen Window. The complete 1672 × 941 source artwork and editable controls scale together, with black bars at other aspect ratios.

The saved layout is `Assets/_Meridian/Prefabs/UI/MainMenu.prefab`; each row uses `MenuButton.prefab` beside it. The title is part of the supplied background texture. The main menu has no separate title overlay or CONTINUE button; the planet screen supplies CONTINUE after a land selection. The source texture is uncompressed sRGB with no mipmaps or rescaling.

Configuration follows the inspected local **Sublevel** and **What Light Remains** projects, both on Unity 6000.5.8f1 / URP 17.5.0. Both have changed to Input System 1.20.0 since the supplied committed baseline; Meridian follows that shared change. Company remains `DefaultCompany`; differing quality conventions use Sublevel's six levels. See [configuration notes](Documentation/Configuration.md) and [validation](Documentation/Validation.md).

Scenes, prefabs, settings, and metadata are serialized by Unity and work without rerunning setup. `Meridian > Author Main Menu Foundation` is an explicit authoring command that regenerates the initial menu at its existing paths; do not use it to preserve later hand edits to this layout.

The saved planet scene is `Assets/_Meridian/Scenes/PlanetSelection.unity`. It works directly in Play Mode as well as through the menu. Tune generation in `Assets/_Meridian/Settings/PlanetGeneration.asset`; a development seed override reproduces geography without adding player-facing controls. The fixed camera, gesture threshold, framing limits, fades, and flag animation have Inspector settings on the scene components/prefabs. `Meridian > Author Planet Selection` explicitly rebuilds this milestone's scene and prefabs; do not rerun it over hand-edited layouts.

The globe retains 40,962 geographic samples, a 5,120-triangle render mesh, three 4096 × 2048 maps, animated water and 45–210% zoom. Color and normal maps have mip chains; the authoritative base-level boundary map keeps picking consistent. One session owns these resources across planet/surface transitions.

The initial surface is **6 km × 6 km (36 km²)**, made from 36 connected 1 km Unity Terrains with 513 × 513 heights each—**4× the previous survey area**, with unchanged terrain sample spacing. Surface generator version 6 preserves gentle terrain and irregular, seeded forest groves with continuously scattered trees instead of visible rows. Ground materials have 1024 × 1024 albedo, normal and surface maps with fine grass and mineral detail. Trees carry stable IDs and wood amounts; grove totals describe available generated members. Readiness requires at least 90,000 m² of connected gentle ground, a 200 m interior and a valid full landing/deployment footprint. Narrow or unsuitable regions recover to the planet without reseeding.

Surface controls: WASD/arrows or middle-drag pan along screen-left/right and screen-up/down, wheel tilts the overhead view, **Shift + wheel zooms**, Q/E smoothly interpolates to accumulated 45° targets, +/− also zooms, and Home resets. Left-click locks a valid site, R/Shift+R changes the ship heading, and right-click/Esc clears. Pan limits are consistent across all allowed tilts and headings at a given zoom; tilting at an edge keeps the same ground focus. The expanded survey supports approximately 1,218 m maximum distance at 16:9 and starts at up to 730 m. Panning stops in the requested direction at a boundary instead of sliding diagonally along the edge. Start a new colony to generate the larger region. Confirmation retains the marker and survey controls; dropship arrival and colony simulation come later. Settings: `Assets/_Meridian/Settings/SurfaceGeneration.asset`. Authoring: `Meridian > Author Landing Site Selection`.

Landing preparation uses up to four numerical workers and displays stage/percentage progress. **BACK TO PLANET** cancels loading; an independent three-minute timeout offers recovery if preparation stalls. One production Windows-player comparison improved from 117 seconds to 43 seconds for the same site, retaining all 36 tiles and their terrain detail.

See [landing coordinates, controls and prototype limits](Documentation/LandingSiteSelection.md), [planet representation](Documentation/PlanetSelection.md), and [validation](Documentation/Validation.md). Save/load, expansion gameplay, harvesting, construction and survival systems are not implemented.

## Repository

Track `Assets`, `Packages` (including the lockfile), and `ProjectSettings`. Unity caches, logs, local captures, and `Builds/Windows` are ignored. PNGs/fonts use ordinary Git binary handling; Git LFS is unnecessary for these assets. The supplied layout reference is stored outside runtime assets at `Documentation/References/Meridian_MainMenu_Reference.png`.

Font: Unity's bundled Liberation Sans, distributed under the SIL Open Font License included at `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
