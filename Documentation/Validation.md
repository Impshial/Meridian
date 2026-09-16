# Validation

## Surface relief, timber and navigation correction — 2026-09-16

Surface generator `meridian-surface-2` replaces small undulations with broad hills, shelves and a raised colony plateau. Default added landform height is 100 m with 650 m spacing; the safe plain is lifted 35 m and blends through a 220 m ramp. Sub-metre roughness is reduced. Exposed stone, lower-angle lighting and survey-distance shadows make slopes readable. Placement still checks the same actual terrain heightfield and full footprint; failure text now reports the measured slope or height difference instead of suggesting that absolute altitude is invalid.

Trees form deterministic timber groves with open ground between them. Per-tree dimensions, group IDs and wood amounts persist in the numerical data; group totals count currently generated members. Trees remain placement obstacles. Harvesting and inventory are not implemented yet.

Verification stayed focused: one representative numeric region, one brief in-engine surface/input check, saved-asset validation and a Windows build. No broad biome or repeated-transition sweep was run.

| Check | Result |
| --- | --- |
| Geography and readiness | Seed 73129, same representative region and production 513-height tiles, validation-only 1024 × 512 planet maps. **7.61 s** numeric generation; **298,100 m²** connected gentle ground; **430 m** usable interior. |
| Visible relief | Sampled dry heights **41.5–403.1 m**; local landforms contribute up to **108.6 m** over shared broad geography. **991** coarse samples exceed 12° while the protected clearing accepts all eight deployment headings. |
| Timber | **2,383 trees**, **41 groves**, **125,122 wood units**. Tree dimensions and exact aggregate totals pass; neighboring tile aggregation preserves group anchors and existing timber quantities. |
| Continuity and placement | Existing tile edge/corner, regeneration, object ownership, full-footprint, obstruction, boundary and cancellation checks pass. |
| Actual input | At 1920 × 1080, wheel changes pitch without changing heading/distance; middle-drag pans with fixed heading/pitch; Q/E each produce exactly one 45° turn. Keyboard +/- retains zoom, WASD/arrows and Home remain available. |
| Visual inspection | `Captures/Survey-Relief-And-Timber.png` and `Survey-Relief-Low-Angle.png` show the terrain slopes, plateau, grouped trees and timber labels. This surface generated numerically in **7.71 s**. |
| Windows x64 | Final three-scene build succeeds with **zero errors and warnings**, **102,790,081 bytes**, **46.80 s**. |

Logs: `Logs/SurfaceGenerationValidation.txt`, `SurveyCorrectionSmoke.txt`, `LandingSavedAssets.txt`, `PlanetBuild.txt`. These logs and captures are local ignored artifacts. The temporary input/capture runner is excluded from the player and removed from Assets before delivery. The existing planet generation, menu, water geography and globe controls remain unchanged.

## Milestone 3 — landing-site selection, 2026-09-16

Validation was deliberately kept focused at the user's request. This section supersedes earlier statements that planet CONTINUE is inert or that leaving the globe always destroys its data. Unity, packages, menu artwork, planet generation and 4K appearance defaults are preserved.

| Check | Evidence / result |
| --- | --- |
| Saved assets and compilation | MainMenu, PlanetSelection and LandingSiteSelection reopen through the saved-asset checks. The selected-region CONTINUE callback, landing prefab/settings/material references, loading/confirmation font glyphs and four 1 km / 513-height defaults pass. |
| Focused numerical check | Seed 73129, one representative inland region, validation-only 1024 × 512 planet maps and production surface resolution. Initial generation: **9.94 s**, **1,128,800 m²** connected gentle dry ground, **560 m** usable square, **7,417** stable objects. |
| Continuity and placement | Four initial tiles plus two development-only neighbors pass shared edge/corner comparisons, regeneration in a different order, exact repeated heights and object identities, unique ownership, seam/pole projection and cancellation. Full landing/deployment clearance passes eight headings; bounds and an interior obstruction reject placement. |
| Actual Play Mode flow | One complete 1920 × 1080 run from MainMenu: both immediate loading messages, actual pointer land selection, exact click centering, flag/edge representation, matching surface readiness, pointer placement, confirmation, and BACK TO PLANET with the identical planet cache, view, flag anchor and confirmed landing. No runtime errors or exceptions in this run. |
| Measured generation | The production 4K planet took **16.01 s**; surface numeric generation took **6.45 s**. That surface measured **1,133,300 m²** connected gentle land and a **560 m** interior square. These are single Editor observations, not general performance guarantees. |
| Windows x64 | The final three-scene player builds successfully with zero errors and warnings: **102,736,065 bytes**, **27.83 s**. This milestone did not repeat the earlier standalone interaction suite. |

The initial survey holds four 513 × 513 float height arrays (about **4.02 MiB**) and four 128 × 128 × 5 float layer arrays (**1.25 MiB**) before Unity's own TerrainData, water, object and rendering allocations. The existing planet pixel payload remains **117.33 MiB** with **32 MiB** retained CPU classification data. Planet resources stay owned by the setup session while surveying and are released on return to MainMenu. Surface scene resources are recreated from cached numerical data when returning to the same region. No new whole-process or frame-time profiling sweep was performed.

Procedural trees, rocks, deposits and the dropship are placeholder art. The wide starting camera emphasizes surveying the region; closer wheel zoom is available. Broad biome, extreme-input, aspect-ratio and repeated-transition playtesting is left to the user. Recoverable difficult-region handling is implemented, but this focused run does not certify every coast, mountain, seam or polar selection.

Temporary local Play Mode instrumentation is removed before delivery; it is excluded from the Windows player. The saved development seed override is disabled. Local evidence is ignored by Git:

- `Logs/SurfaceGenerationValidation.txt`, `LandingSavedAssets.txt`, `LandingFlowSmoke.txt`, and `PlanetBuild.txt` contain numeric, saved-asset, flow and build results.
- `Captures/Landing-Loading-Planet.png`, `Landing-Loading-Surface.png`, `Landing-Selected-Globe.png`, `Landing-Far-Side-Indicator.png`, `Landing-Surface-Overview.png`, `Landing-Valid-Placement.png`, and `Landing-Confirmed.png` are actual Unity captures.
- `Captures/Meridian-Landing-Flow.mp4` is a short edited sequence of actual rendered frames showing the two loading presentations, globe interaction and placement; generation waits are cut and playback timing is compressed.

See [LandingSiteSelection.md](LandingSiteSelection.md) for controls, geographic projection, terrain defaults, readiness criteria, ownership and future expansion boundaries. Durable checks are available under `Meridian > Validate Surface Generation` and `Meridian > Validate Landing Site Saved Assets`.

## Terrain resolution correction — 2026-09-16

The baseline used 1024 × 512 runtime maps without mipmaps and forced LOD 0. The initial editor preview was only 1101 × 627; baseline and final captures instead use actual 1920 × 1080 and 2560 × 1440 render targets, URP render scale 1 and texture mip limit 0. Comparisons use seed 73129, saved local orientations, unchanged lighting and the full 210% zoom. Coarse graph interpolation, binary coast thresholds and resolution-dependent normal differences were additional bottlenecks. Increasing mesh density was unnecessary.

The delivered default is three freshly baked **4096 × 2048** maps. Color and object normals have 13 mip levels, trilinear filtering and anisotropy 4, using seam-aware shader gradients. The linear packed surface map stays at one level; continuous coverage is bilinear while IDs are fetched explicitly. Bounded cubic coastal interpolation, analytic normal gradients and seed-anchored procedural shading improve close detail. The freezing interval uses all 256 alpha levels to remove polar-water banding. The existing water material controls, 45/63/210% zoom, camera pose, geometry, menu and flag behavior remain intact. 8K is configurable but was not needed or benchmarked.

| Check | Evidence / result |
| --- | --- |
| Fixed geography | Seed 73129's original and corrected elevation, moisture, temperature, filled drainage, flow, downstream links, water nodes and biome nodes have identical SHA-256 `FE57A8951914CDA94A6F4AAFD1FB821BAA61799AA2CD58E7DA73E4EFF216270B`. Physical placement heights come directly from these graph fields. |
| Determinism and generation | Five fixed seeds plus an exact repeat pass feature coverage, descending drainage, wrapped longitude, uniform pole rows and cancellation. Repeat hashes include all three maps. |
| Surface checks | 4K defaults, 8K settings clamp, shared cubic edge continuity, bounded controls, unchanged graph fields across map resolutions, preserved angular river width, normalized normals, continuous boundaries, freezing precision and staging release pass. |
| Native appearance | Forest, desert, rock, snow, coast, pole and seam captures at 1080p and 1440p; another seed (42817) also captured and exercised. Native crops show finer surface detail and smoother shores. Full rotation and zoom-out recorded as 160 actual rendered frames. |
| Picking | Hardware probe compares 36,315 output pixels against actual collider hits and CPU sampling: zero interior discrepancies. One half-covered contour sample differs by 0.0501 output pixel due to raster/sampling precision, within the one-pixel antialiasing transition. Actual pointer clicks accept the dry shoreline and reject the wet shoreline. |
| Interaction | Land selection, ocean/lake/river rejection, pole/seam flags, drag ownership, wheel over UI, extreme zoom and stable camera/data/flag anchors pass. CONTINUE remains disabled. |
| Lifetime | Three editor and three Windows reentry cycles keep five generated mesh/material/texture objects while visiting and zero in MainMenu. Appearance staging arrays are null after upload; a weak-reference check confirms the previous PlanetData is collectible after BACK. No runtime errors. |

Measured cost on this machine (Editor timings include editor overhead; standalone frames are VSync limited):

| Measurement | Baseline / final |
| --- | --- |
| Numeric generation, seed 73129 | 5.02 seconds baseline Editor; 12.22 seconds final Editor (earlier corrected run 13.96 seconds). Final Windows entry measured 12.50 seconds. The black transition remains visible during generation. |
| 1920 × 1080, 200 steady frames | Baseline Editor mean 12.17 ms / p95 14.52 ms; final mean 7.41 ms / p95 9.55 ms. These are separate runs, not evidence of a guaranteed speedup. |
| 2560 × 1440, 200 steady frames | Final Editor mean 10.79 ms / p95 14.02 ms. |
| Windows 1920 × 1080 | Final mean 16.76 ms / p95 16.66 ms at 60 Hz VSync; not an isolated GPU timing. |
| GPU texture pixel payload | 6 MiB baseline → **117.33 MiB** final, including appearance mip chains. Unity Editor native texture allocation accounting reports 234.71 MiB. |
| CPU map arrays | 96 MiB during bake; 64 MiB of appearance buffers released after upload, 32 MiB classification retained plus fixed graph data. No full-resolution elevation/moisture or normal-height scratch arrays. |
| Windows process memory | Profiling run: menu working set 398.12 MiB; active 671.52 MiB; peak 704.15 MiB, approximately 306.03 MiB above menu. Final smoke run after ice interpolation refinement: process peak 711.75 MiB; peak managed heap 123.66 MiB. Whole-process figures include Unity and capture/test infrastructure. |
| Windows GPU process allocation | Five stable OS-counter samples: 304.27 MiB dedicated and 191.46 MiB shared. These include render targets, menu resources and driver allocations, not just planet maps. Nondevelopment-player Unity texture-profiler readings were unavailable. |

Static maps are generated once per visit with at most eight numeric workers, then uploaded on the main thread. Rotation and zoom reuse them. The GPU texture payload and retained CPU classification are substantially larger than before; a later platform budget may warrant compression or a different representation. This correction adds orbital shading detail, not local terrain or physical vegetation.

Final Windows x64 build: succeeded with zero errors and warnings, 102,595,497 bytes, 17.67 seconds. The visible final player passed entry, native 1080p rendering, input/selection and BACK cleanup with no runtime errors. The ignored smoke build includes its file-based runner only when launched with `-meridian-terrain-test`; that instrumentation is removed from Assets for delivery.

After removing temporary test assets, Unity was closed and reopened from disk. MainMenu and PlanetSelection saved-asset validators passed again, the seed override remained disabled and the default map width remained 4096. `Logs/Terrain-Final-Reopen.log` records this clean reopen.

Changed files: `PlanetGenerationSettings.cs` and `PlanetGeneration.asset` set the actual default/clamp; `PlanetGenerator.cs` refines baking and preserves river widths; `PlanetData.cs` retains authoritative classification and graph sampling while releasing staging buffers; `PlanetGlobe.cs` creates filtered mipmapped appearance resources; `PlanetSurface.shader` adds filtered detail and pixel-scale coverage; `PlanetValidation.cs` and new `PlanetSurfaceValidation.cs` cover the changed contracts. README and planet documentation describe the new budgets and representation. Packages, Unity version and menu artwork are unchanged.

Local evidence is ignored by Git:

- `Captures/Terrain-Before-After-Native.png` — full native 1080p views side by side at 210% zoom; `Terrain-Before-After-Detail.png` — unscaled 1440p crops.
- `Captures/Terrain-Before-<feature>-<resolution>.png`, `Terrain-After-<feature>-<resolution>.png` and `Terrain-Seed42817-<feature>-1920x1080.png` — native biome, coast, pole and seam captures.
- `Captures/Meridian-Terrain-Rotation.mp4` — actual rotation/zoom sequence.
- `Captures/Terrain-4K-Metrics.json`, `Terrain-4K-1440-Metrics.json`, `Terrain-Windows-Final-Entry.json`, `Terrain-Windows-Final-Profile.json`, `Terrain-Windows-Final-Process.json`, `Terrain-Windows-GPU.json` — dimensions, mip counts and measured costs.
- `Captures/Terrain-4K-Suite.json`, `Terrain-Seed42817.json`, `Terrain-Editor-Cycles.json`, `Terrain-Windows-Suite.json`, `Terrain-Windows-Cycles.json`, `Terrain-Edges-And-Collection.json` — interaction, boundary and lifetime results.
- `Captures/Terrain-Windows-Final-Suite.json`, `Terrain-Windows-Final-Cleanup.json` — final standalone regression and managed-data collection checks.
- `Logs/PlanetSurfaceValidation.txt`, `PlanetGenerationValidation.txt`, `PlanetSavedAssets.txt`, `PlanetZoomValidation.txt` and `PlanetBuild.txt` — durable checks and build evidence.

## Water and regional zoom correction — 2026-09-15

This correction supersedes the original whole-globe fit requirement and its historical framing captures below. Generator code, shared geographic sampling, packages, menu artwork, lighting, flag geometry and coordinates are unchanged.

| Check | Result |
| --- | --- |
| Actual projection | Runtime camera calculation measures 0.45 wide, 0.63 initial and 2.10 close at both 1920 × 1080 and 1280 × 1024. Close FOV is 14.09294° at distance 4 with a 1.005 terrain envelope; approximately 3.23× the previous effective 65% close view. Cropping is intentional. |
| Input | Extreme positive/negative wheel input clamps; three negative ticks produce the expected exp(-0.3) factor. Wheel over BACK and disabled CONTINUE is ignored. UI-owned drags, out-and-back globe drags and focus-loss capture reset pass. |
| Selection and flag | Seam/polar land planting, ocean/lake/river rejection, rotation during planting, stable local anchor, natural offscreen movement, far-side depth occlusion and single-marker reuse pass. Camera position/rotation and globe scale remain unchanged. |
| Water comparison | Original and corrected shaders rendered against the exact same seed-73129 map objects, globe orientation, camera and fixed key. Overview and 210% close images show broken-up liquid reflections and retained coastlines, terrain and ice. |
| Motion | A 13-second 1280 × 720 recording contains 260 actual Unity frames at 20 fps, including normalized wheel input from wide to close and a stationary close hold. Water changes during the hold without regenerated maps or mesh. |
| Lifetime | Three additional editor and three standalone BACK/reentry cycles pass. Five generated Unity resources while visiting; zero in the menu; no runtime errors. |
| Durable checks | Saved-scene validation now checks 45/63/210% settings, wheel rate, local control contrast and six water material controls. `Meridian > Validate Regional Zoom Projection` exercises the actual runtime projection method and component defaults. Original geography/gesture and menu checks remain available. |
| Windows x64 | Final build succeeded with zero errors and warnings, 102,591,175 bytes, 44.43 seconds. The final visible player repeated projection/input/selection checks and BACK during planting with no runtime errors. Fixed-seed generation measured 5.13 seconds in that run. |

Additional close captures inspect lake, river, longitude seam and polar ice. Frozen-water rejection, globe-owned release over BACK, and BACK while the flag is planting are checked separately. Local control backplates preserve button contrast over enlarged terrain. No marker resizing was needed.

Changed files: `PlanetSurface.shader` and `PlanetSurface.mat` implement/tune water; `PlanetViewingInput.cs` and `PlanetSelection.unity` update runtime and saved framing; `PlanetSelectionAuthoring.cs` retains those defaults and local button contrast when explicitly authoring; `PlanetValidation.cs` adds focused saved-asset/projection checks; README and the two planet/validation documents describe the revised behavior. Temporary test scripts and the original comparison shader are removed from Assets after testing. The ignored smoke build enables its local file-based runner only with `-meridian-water-test`.

There are no new water textures, render targets or per-frame uploads. The existing 6 MiB map payload, 5,120 triangles and 40,962 geographic samples are unchanged. The close view exposes the existing orbital map resolution and symbolic river widths; this correction does not supply local terrain or physically scaled waves.

Evidence is local and ignored by Git:

- `Captures/Water-Before-After.png`, `Water-Before-0.63.png`, `Water-After-0.63.png`, `Water-Before-2.10.png`, `Water-After-2.10.png` — identical-view shader comparisons.
- `Captures/Meridian-Water-And-Zoom.mp4` — actual Unity water animation and wide-to-close sweep.
- `Captures/Water-Editor-1920.json`, `Water-Editor-1280.json`, `Water-Editor-Cycles.json`, `Water-Editor-Details.json` — focused runtime results.
- `Captures/Water-Windows-PASS.json`, `Water-Windows-Cycles.json` — standalone results.
- `Logs/PlanetZoomValidation.txt`, `PlanetSavedAssets.txt`, `MainMenuValidation.txt`, `PlanetBuild.txt` — projection, reopened asset and build checks.

## Milestone 2 — planet selection, 2026-09-15

Validated with Unity 6000.5.8f1, URP 17.5.0 and Input System 1.20.0. The original package manifest and lockfile are unchanged.

| Check | Result |
| --- | --- |
| Saved assets | MainMenu and PlanetSelection reload successfully; exactly one NEW COLONY callback, three inert menu placeholders, one BACK callback and disabled CONTINUE without a callback |
| Final clean reopen | Temporary instrumentation removed through AssetDatabase; project reopened without it; both saved-scene checks pass again and the development seed override is disabled |
| Deterministic geography | Seeds 73129, 18041, 90210, 42817 and 61503 produce distinct map hashes; regenerating 73129 reproduces exact color/surface bytes |
| Feature coverage | All five seeds contain forests, deserts, snow, rock/mountains, plains, inland lakes and rivers; drainage links strictly descend the filled drainage surface |
| Geographic continuity | Wrapped longitude equivalence and exact uniform polar rows pass; four rotational views plus a polar view inspected for each seed |
| Land coverage | Geographic sample estimates: 33.5%, 30.6%, 29.9%, 35.9% and 43.8% respectively; these are continental coverage estimates, not construction/buildability measurements |
| Globe picking | Land clicks in forest, desert, snow, rock, polar and seam regions replace one selection; oceans, lakes, rivers and background preserve it |
| Gestures | Stationary click, maximum-excursion threshold, out-and-back drag, UI-owned gestures, release over UI and focus-loss capture reset pass |
| Flag | Raised-to-ground planting animation runs while the globe rotates; base stays fixed in local coordinates; far-side depth occlusion and single-marker replacement pass |
| Camera and zoom | Camera transform and globe center remain unchanged; sustained extreme wheel input clamps safely; wheel over UI does not zoom |
| Transitions and lifetime | Ten consecutive editor entry/BACK cycles pass with fresh seeds, empty new selections, one camera/EventSystem and five generated Unity resources per visit; zero generated resources remain in MainMenu |
| Resolutions | Actual Game view sizes 1920 × 1080, 2560 × 1440 and 1280 × 1024 verified; nominal framing plus minimum/maximum zoom captures inspected; bottom controls stay clear |
| Editor runtime | Clean interaction/lifetime run reports zero runtime errors or exceptions |
| Windows x64 build | Succeeded: zero errors, zero warnings, 102,577,776 bytes; final incremental smoke build took 48.25 seconds |
| Standalone run | Input/flag/zoom checks and ten entry/BACK cycles pass with zero runtime errors under normal desktop permissions; a visible player run also passes entry, flag selection and zoom capture checks |

The render/collision mesh has 2,562 vertices / 5,120 triangles. The CPU geography graph has 40,962 samples. Three 1024 × 512 RGBA32 maps use 6 MiB of GPU pixel payload without mipmaps. Numeric generation took 6.54–7.57 seconds in the final fixed-seed checks and 6.17–6.40 seconds during the five captured Play Mode entries. The Windows player measured 8.69–13.55 seconds across its run; generation latency remains a prototype limitation. Rendering and rotation reuse those resources.

Focused checks remain available under **Meridian → Validate Planet Saved Assets** and **Meridian → Validate Planet Generation and Gestures**. Input checks use virtual Input System devices in the actual Unity runtime. Temporary test instrumentation communicates through local files, has no network listener, and is excluded from the committed project.

The ignored Windows smoke build includes instrumentation enabled only by `-meridian-smoke-test`; a normal launch does not create it. The first restricted launch could not obtain the initial Windows mouse position; the normal-desktop rerun resolved that environment issue. Hidden-window capture was unavailable, so the visual checks were repeated in a visible game window. The final visible-run result contains no runtime errors. These intermediate launch/capture failures were not suppressed in the original logs.

Local evidence is ignored by Git:

- `Logs/PlanetGenerationValidation.txt` — seed hashes, feature counts, generation timings and focused check results.
- `Logs/PlanetSavedAssets.txt` and `Logs/MainMenuValidation.txt` — saved-reference checks.
- `Captures/Editor-PlanetSmoke-PASS.json` and `Captures/PlanetSmokeProgress.txt` — interaction/lifetime checks.
- `Captures/Planet-<seed>-View0.png` through `View3.png`, plus `Pole.png` — actual Game view rotations.
- `Captures/Planet-<seed>-Contact.png` — comparison sheets made from those captures.
- `Captures/Planet-Flag-<resolution>.png` and `Planet-ZoomIn/Out-<resolution>.png` — UI and framing checks.
- `Captures/Windows-PlanetSmoke-PASS.json`, `Windows-PlanetVisual-PASS.json`, and `Windows-Planet-Flag-1920x1080.png` — standalone results and actual rendered capture.

Known boundaries: terrain and hydrology are orbital approximations; minimum river width is deliberately symbolic for readability. Local colony terrain, exact construction footprints and a measured connected-flat-area guarantee are not part of this milestone. CONTINUE remains disabled.

## Milestone 1 — historical foundation validation

Validated in the actual Unity 6000.5.8f1 editor and a Windows x86-64 player on 2026-09-14.

| Check | Result |
| --- | --- |
| Saved scene and prefabs | MainMenu reloaded from disk successfully without rerunning the authoring command |
| Final package cleanup | Project reopened with the temporary CLI package removed; the saved-asset validator passed again |
| Runtime UI | Exactly four separate uGUI buttons, in the requested order; no CONTINUE, duplicate title, or action callbacks |
| References | No missing component scripts, menu object references, font glyphs, or URP renderer/quality assignments |
| Source artwork | Both attachments copied byte for byte; runtime background is 1672 × 941, sRGB, uncompressed, no mipmaps or NPOT rescaling |
| 1920 × 1080 | Actual Game view resolution verified, screenshot inspected; clear text and amber bracket |
| 2560 × 1440 | Actual Game view resolution verified, screenshot inspected; shared image/control scaling preserved |
| 1280 × 1024 | Actual Game view resolution verified, screenshot inspected; complete artwork with black letterboxing and aligned menu |
| Play Mode input | Pointer clicks and Enter submissions exercised on every button through Input System virtual devices; no scene/application change; arrow navigation passed |
| Cursor | Visible and unlocked |
| Windows build | Succeeded, x86-64, Mono, zero build errors; local smoke-test instrumentation only |
| Standalone startup and input | MainMenu at 1920 × 1080; four clicks and four Enter submissions passed; arrow navigation passed; QUIT left the application running |
| Standalone console | No runtime errors, exceptions, missing-script messages, or missing-glyph warnings in the successful smoke run |

The test build is ignored at `Builds/Windows/Meridian.exe`. Its local-only test runner is activated with `-meridian-smoke-test`; that runner was removed from project sources after validation and is not committed. The test build contains no Unity Pipeline runtime server. Its sole build warning stated that the optional Pipeline runtime configuration was absent and the server was disabled. The temporary CLI package was removed from the finished project.

The build was launched windowed at 1920 × 1080 for deterministic capture; the saved product default remains Fullscreen Window. The scene was exercised in Play Mode and reloaded in Edit Mode. The saved-asset check can be repeated with **Meridian → Validate Saved Main Menu**; it does not regenerate the layout.

## Actual Unity captures

Local screenshots are outside runtime assets and ignored by Git:

- `Captures/MainMenu-1920x1080.png` — actual editor Game view.
- `Captures/MainMenu-2560x1440.png` — actual editor Game view.
- `Captures/MainMenu-1280x1024.png` — actual editor Game view, letterboxed.
- `Captures/Windows-MainMenu-1920x1080.png` — actual standalone player, initial selection.
- `Captures/Windows-Settings-Focus.png` — actual standalone player, SETTINGS highlighted.
- `Captures/Windows-Smoke-PASS.json` — local standalone input check result.

These captures show the implemented Unity UI. `Documentation/References/Meridian_MainMenu_Reference.png` is the supplied design reference, not an implementation screenshot.

SHA-256 of the unchanged source background: `D0B3C54B3297859A7CB2927DB7D4E5647E4AFA99C07AAE5A57EAB07809D5D63F`.

SHA-256 of the unchanged reference: `79A59FFCDA234AC1A786587C8EA4087426DD2232AB27E5E68B01B7B33FCF34AA`.
