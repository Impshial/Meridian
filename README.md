# Meridian

Meridian is an exoplanet colony builder directed from an orbital command ship. Future gameplay will use an overhead/isometric camera in a three-dimensional colony and landscape.

Milestone 2 adds a procedural planet and landing-region selector. **NEW COLONY** fades into a fresh generated globe. Left-drag rotates the planet, the wheel provides limited centered zoom, and clicking land plants or moves one animated amber flag. **BACK** returns to the menu. **CONTINUE remains disabled**, even after selection; LOAD COLONY, SETTINGS, and QUIT remain inert placeholders.

## Open and run

- Open this repository root in **Unity 6000.5.8f1 (5cb7df797b7d)**, allow package imports to finish, then open `Assets/_Meridian/Scenes/MainMenu.unity` and press Play.
- Target: **Windows desktop, x86-64**. MainMenu is the first build scene, followed by PlanetSelection.
- Rendering: **Universal Render Pipeline 17.5.0**, Linear color space, 3D editor mode.
- Input: **Input System 1.20.0** and `InputSystemUIInputModule`. The UI uses uGUI / TextMeshPro **2.5.0**.
- Screen: 1920 × 1080 default, Fullscreen Window. The complete 1672 × 941 source artwork and editable controls scale together, with black bars at other aspect ratios.

The saved layout is `Assets/_Meridian/Prefabs/UI/MainMenu.prefab`; each row uses `MenuButton.prefab` beside it. The title is part of the supplied background texture. There is no separate title overlay or CONTINUE button. The source texture is uncompressed sRGB with no mipmaps or rescaling.

Configuration follows the inspected local **Sublevel** and **What Light Remains** projects, both on Unity 6000.5.8f1 / URP 17.5.0. Both have changed to Input System 1.20.0 since the supplied committed baseline; Meridian follows that shared change. Company remains `DefaultCompany`; differing quality conventions use Sublevel's six levels. See [configuration notes](Documentation/Configuration.md) and [validation](Documentation/Validation.md).

Scenes, prefabs, settings, and metadata are serialized by Unity and work without rerunning setup. `Meridian > Author Main Menu Foundation` is an explicit authoring command that regenerates the initial menu at its existing paths; do not use it to preserve later hand edits to this layout.

The saved planet scene is `Assets/_Meridian/Scenes/PlanetSelection.unity`. It works directly in Play Mode as well as through the menu. Tune generation in `Assets/_Meridian/Settings/PlanetGeneration.asset`; a development seed override reproduces geography without adding player-facing controls. The fixed camera, gesture threshold, framing limits, fades, and flag animation have Inspector settings on the scene components/prefabs. `Meridian > Author Planet Selection` explicitly rebuilds this milestone's scene and prefabs; do not rerun it over hand-edited layouts.

The globe uses 40,962 geographic samples, a 5,120-triangle render mesh, and three 1024 × 512 generated maps. All geography is generated once per visit and shared with picking. No local colony terrain, save/load, construction, or next-stage action is implemented. See [planet generation and coordinate notes](Documentation/PlanetSelection.md) and [validation](Documentation/Validation.md).

## Repository

Track `Assets`, `Packages` (including the lockfile), and `ProjectSettings`. Unity caches, logs, local captures, and `Builds/Windows` are ignored. PNGs/fonts use ordinary Git binary handling; Git LFS is unnecessary for these assets. The supplied layout reference is stored outside runtime assets at `Documentation/References/Meridian_MainMenu_Reference.png`.

Font: Unity's bundled Liberation Sans, distributed under the SIL Open Font License included at `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
