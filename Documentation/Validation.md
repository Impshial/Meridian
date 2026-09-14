# Main menu validation

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
