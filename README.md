# Meridian

Meridian is a 3D exoplanet colony builder directed from an orbital command ship. Machines establish the settlement before sleeping colonists descend from orbit. Build connected domes, support people, automate industry, trade, host visitors, develop the planet and expand into neighboring regions.

**NEW COLONY** shows `Getting you an exo-planet...` and creates a fresh globe. Select land, continue to the matching 6 × 6 km survey, lock a valid site and choose **LAND HERE**. A skippable cargo arrival deploys twelve machines, supplies and a passenger apron; all 48 colonists remain in orbital cryosleep. Main-menu Continue, Load Colony, Settings and Quit are functional. Menu brackets appear only under the mouse.

Read the [play guide](Documentation/ColonyGame.md) for controls, opening settlement and save policy. See [validation evidence and limits](Documentation/FullGameValidation.md). The [implementation ledger](Documentation/FullGameImplementation.md) records delivery status; [full-game requirements](Documentation/FullGameRequirements.md) preserve the requested scope.

## Open and run

- Open in **Unity 6000.5.8f1 (5cb7df797b7d)**, allow imports to finish, open `Assets/_Meridian/Scenes/MainMenu.unity`, and press Play.
- Ready-to-run Windows build: `Builds/Windows/Meridian.exe`. Keep its adjacent data, Mono and DLL files together. Build order: MainMenu, PlanetSelection, LandingSiteSelection.
- Rendering: **URP 17.5.0**, Linear color space.
- **Input System 1.20.0** continues to drive cameras and setup menus. Active Input Handling is **Both** so colony management windows receive native immediate-mode GUI events. Setup UI remains uGUI / TextMeshPro **2.5.0**. No packages or Unity version were changed.
- `Meridian > Colony > Build Windows Player` produces `Builds/Windows/Meridian.exe` with all three scenes and ordinary main-menu startup.

The main menu preserves the supplied artwork, title and button styling. Its Continue loads the latest valid colony save; planet Continue enters the selected region. Authoring commands recreate saved scenes/prefabs and should not be rerun over hand-edited layouts.

## Controls and gameplay

WASD/arrows or middle-drag pan along screen axes. Wheel tilts; **Shift + wheel** and +/− zoom. Q/E smoothly turns in accumulated 45° steps. Home resets; F focuses the selected object. Space pauses, 1/2/3 selects time speed, B opens construction, H designates harvesting, and F5/F9 saves/loads. R/Shift+R rotates placement. Right-click/Esc cancels tools; Esc otherwise closes windows or opens the colony menu. Windows capture pointer gestures and text entry.

Build with real finite cargo and autonomous workers. Cable, pipe and sealed corridor connections have separate functions. Builders can share civilian work through airlocks; industrial equipment remains machine-only. Orbital personnel and visitors travel on physical ships. Production, research, trade, maintenance, visitor income and planetary development share one saved simulation.

The compressed calendar uses 120 simulation seconds/day and 12 days/year. Nominal supported terraforming takes about 71 game years and does not end play. Neighboring regions preserve the original geographic frame and depletion; acquired land extends camera bounds without allowing unowned corners. Detailed terrain rendering stays bounded to 49 nearby tiles.

## Data and saves

Edit balance, buildings, recipes and research in `Assets/_Meridian/Resources/Colony/Catalog.json`. Planet and surface settings remain under `Assets/_Meridian/Settings`. Runtime code separates simulation, persistence, presentation and UI under `Scripts/Colony`.

Windows saves: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Meridian/Saves`. Named slots, quicksave, backups and five rotating autosaves retain stable identities, inventories, reservations, interrupted work, traffic, events, regional development and layouts. Loading validates and reconstructs before replacing the session, restores paused, and applies no offline time.

## Geography and assets

The globe retains 40,962 geographic samples, a 5,120-triangle render mesh, three 4096 × 2048 maps, animated water and 45–210% framing. Surface generation retains 36 connected one-kilometer Terrains with 513 × 513 heights each, detailed ground maps, gentle building space, water and seeded irregular forests. Landing preparation uses bounded numerical workers, actual progress, cancellation and timeout recovery.

Six user-supplied models are imported under `Assets/_Meridian/Art/Imported` and normalized into prefabs/materials under `Resources/Colony/Models`. Original procedural spacecraft, module frames/interiors, utility pieces and simple biped colonists supplement them. Supplied model provenance is recorded in [the asset note](Documentation/ColonyAssets.md).

See [historical terrain validation](Documentation/Validation.md), [landing coordinates](Documentation/LandingSiteSelection.md), [planet representation](Documentation/PlanetSelection.md), and [configuration notes](Documentation/Configuration.md).

## Repository

Track Assets, Packages (including lockfile), ProjectSettings and documentation. Caches, logs, generated saves, local validation helpers, captures and Windows builds remain ignored. Validation commands under `Meridian > Colony` use explicit development fixtures under ignored `Library/GameValidation`; ordinary startup contains no fixture or debug HUD.

Unity's bundled Liberation Sans is distributed under the SIL Open Font License at `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`. Management windows use the installed Segoe UI font at runtime.
