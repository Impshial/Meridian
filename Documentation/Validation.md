# Validation

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
