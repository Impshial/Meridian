# Meridian: Unity project foundation and main menu

Implement this milestone in Unity on my Windows machine. Create the actual project, configure Git, construct the main menu, and validate the result in Unity. Complete the work rather than returning another plan or setup instructions for me to perform.

## 1. Project location and milestone

- Game title and Unity product name: **Meridian**.
- Existing empty target folder: `C:\Users\impsh\Documents\Unity\Meridian`.
- Existing repository: `https://github.com/Impshial/Meridian`.
- Git remote: `https://github.com/Impshial/Meridian.git`.
- Initial branch: `main`.
- Platform: Windows desktop, 64-bit.
- Project type: **3D**, with an eventual overhead isometric view of a three-dimensional colony and landscape.

The Unity project root and repository root must both be the exact folder above. `Assets`, `Packages`, `ProjectSettings`, and `.git` belong directly inside that folder. Do not create a nested `Meridian\Meridian` project.

This milestone consists only of a properly configured Unity project and its startup title/main menu screen. The provided artwork serves as the splash/title background, and the menu appears directly over it. A separate timed splash sequence is unnecessary.

Context for future work: Meridian is an exoplanet colony builder. The player manages construction and resources indirectly from an orbital command ship. Robots and drones prepare the settlement before colonists awaken from hibernation. Later development includes survival, terraforming, growth, and attracting visitors. The eventual gameplay camera is overhead/isometric. This context explains the project direction; none of those systems should be implemented in this milestone.

## 2. Match the existing Unity setup

Inspect my existing Unity projects under `C:\Users\impsh\Documents\Unity` before creating Meridian. Locate real Unity roots by their `ProjectSettings\ProjectVersion.txt` files. Use **Sublevel** and **What Light Remains**, where available, as configuration references. Read any applicable `AGENTS.md` instructions before modifying the target project.

The following baseline was verified from the committed Sublevel project. Previous What Light Remains records also identify the same editor version:

| Setting | Verified baseline |
| --- | --- |
| Unity Editor | `6000.5.8f1`, Unity 6.5 |
| Editor revision | `5cb7df797b7d` |
| Project behavior | 3D |
| Render pipeline package | `com.unity.render-pipelines.universal` version `17.5.0` |
| Color space | Linear |
| Active Input Handling | Input Manager (Old), serialized as `activeInputHandler: 0` |
| Asset serialization | Force Text, serialized as `m_SerializationMode: 2` |
| Version control mode | Visible Meta Files |
| Default desktop dimensions | 1920 × 1080 |
| Fullscreen mode | Fullscreen Window |
| Graphics API list | No explicit override in the committed player settings |

Reference files: [editor version](https://github.com/Impshial/sublevel/blob/main/ProjectSettings/ProjectVersion.txt), [package manifest](https://github.com/Impshial/sublevel/blob/main/Packages/manifest.json), [player settings](https://github.com/Impshial/sublevel/blob/main/ProjectSettings/ProjectSettings.asset), [editor settings](https://github.com/Impshial/sublevel/blob/main/ProjectSettings/EditorSettings.asset), and [version control settings](https://github.com/Impshial/sublevel/blob/main/ProjectSettings/VersionControlSettings.asset).

Inspect the current local references for deliberate changes since that snapshot. If both local projects now share a newer configuration, match that common configuration and document the difference. If they disagree, use the verified Sublevel baseline above for Meridian and record the decision. Do not choose the newest installed editor merely because it is available.

Also inspect the applicable Windows scripting backend, API compatibility, graphics, quality, and editor conventions. Match explicitly configured values. Where the reference retains editor defaults, use the matching editor's defaults and record the resolved configuration. Do not infer an explicit DirectX override merely from a screenshot showing DX12.

Reproduce the relevant settings in a fresh project. Do not duplicate the other game's project wholesale, carry over its cloud/project identifiers, gameplay input bindings, scene list, scripts, paid assets, or unrelated packages. Create valid Meridian render-pipeline and renderer assets with their own references. URP must be assigned in Graphics settings and all applicable quality levels, with no dangling asset GUIDs.

Use `Meridian` as the product name and C# namespace for any project scripts. Match an established developer/company name if the reference projects have one; otherwise retain the editor default rather than inventing a studio name.

## 3. Let Unity create and serialize the project

The Unity CLI is open and available if useful. Inspect the actual installed tools and running Unity processes. Use the matching editor through the available Unity integration, CLI, or Unity Hub. Do not assume a command exists without checking its help or installation.

Create the project using the compatible **Universal 3D / URP** template if it is available. If a suitable template cannot be used, let the matching Unity editor create the empty 3D project, then install/configure the matching URP package and create its assets through Unity.

Unity supports project creation with `-createProject`, targeting an existing project with `-projectPath`, and editor automation through `-executeMethod`. Use supported arguments for the installed editor, quote Windows paths correctly, and capture useful logs. The creation flag alone is not evidence that URP has been configured. [Unity 6.5 CLI reference](https://docs.unity3d.com/6000.5/Documentation/Manual/EditorCommandLineArguments.html)

Use Unity APIs and normal editor serialization to create scenes, prefabs, settings assets, and `.meta` files. Do not hand-invent scene YAML or GUIDs. If an editor setup script is useful, keep it small, place it in an `Editor` folder, and make repeat execution safe. The finished project must contain saved assets that open and run without rerunning that script.

Avoid opening the same project in two editors at once. Do not terminate unrelated Unity sessions. If a required editor, module, license, or attachment is unavailable, complete the remaining possible setup and report the exact blocker rather than silently substituting a different environment or artwork.

## 4. Use the two attached images correctly

I am supplying two images with this prompt:

| Attachment | Role |
| --- | --- |
| `6afa3417-065b-4cf6-99b6-5f289f9dd69c.png` | **Actual background.** It contains the MERIDIAN title, planet, ship, stars, and moons, with no menu options. |
| `a1d7fd58-26cf-4343-8345-9ea878c4a8b7.png` | **Visual reference only.** It shows the same composition with menu labels and an amber selection accent. |

Identify them by their appearance if attachment filenames change. Use the attachment paths actually available in this Codex session; a path from another chat's Linux workspace is not a Windows path.

Copy the clean background into `Assets/_Meridian/Art/UI/Meridian_MainMenu_Background.png`. Preserve the source pixels. The MERIDIAN lettering and its decorative divider are already baked into this image, so do not place another title or divider over them.

Keep a copy of the button reference at `Documentation/References/Meridian_MainMenu_Reference.png`, outside `Assets`, so it remains available for future design work without becoming a runtime texture. This image is a placement/style guide, not the displayed background.

Do not redraw, recolor, crop, distort, or replace the artwork. Do not recreate the planet or ship as 3D objects. Import the background as a full-resolution sRGB UI texture with no visible compression artifacts, no unnecessary mipmaps, and no power-of-two rescaling. Use a maximum texture size that preserves its actual source dimensions. Display it with neutral white tint and without lighting or post-processing that changes its appearance.

## 5. Build the startup main menu

Create and save `Assets/_Meridian/Scenes/MainMenu.unity`. Make it the first and only included startup scene in the Windows build configuration. Leave this scene open in the editor at completion.

Use actual editable Unity UI for the buttons. Prefer a Screen Space Overlay uGUI Canvas, TextMeshPro labels, and a reusable button prefab. Add only the compatible Unity UI dependencies and required font resources needed by this implementation. Keep the project's existing input-handling baseline and use the appropriate EventSystem input module. Do not migrate the project to a different input system just to display the menu.

Save the layout as a prefab, for example `Assets/_Meridian/Prefabs/UI/MainMenu.prefab`. The screen should be visible in the saved scene and adjustable in the Inspector. Do not construct the entire UI only at runtime or use a flat screenshot of the menu controls.

Create **exactly four** buttons, in this order, using these displayed labels:

1. `NEW COLONY`
2. `LOAD COLONY`
3. `SETTINGS`
4. `QUIT`

The reference image also contains CONTINUE. Omit that entry for this milestone because the requested four-button list above is authoritative. Close up the spacing so there is no empty row where CONTINUE appeared.

Match the reference's visual treatment:

- Place the vertical menu in the dark left-hand area below the existing MERIDIAN title.
- Use thin, clean uppercase lettering with generous character spacing and a warm off-white normal color.
- Keep the labels left-aligned and the row spacing consistent.
- Use a restrained amber highlight and fine bracket/underline like the NEW COLONY treatment in the reference.
- Start with NEW COLONY visually selected. Pointer hover or keyboard focus may change the visual highlight, but selection styling must stay consistent.
- Use a transparent button face and a comfortable hit area around each label. Avoid default gray Unity button boxes.
- Disable raycast blocking on decorative elements and the background so they do not intercept pointer input.
- Use a bundled, redistributable font or Unity-provided font resource. Do not introduce a paid font or an external font-service dependency.

As an approximate starting point at a 1920 × 1080 reference size, the first label begins around 6% of the image width and its center is around 27% of the image height from the top. Space the four row centers about 7–8% of the image height apart. Refine against the reference visually rather than treating these estimates as exact measurements.

Every button is a **presentation-only placeholder**. Clicking, pressing Enter, or submitting a focused button must perform no action. Leave action callbacks and persistent OnClick listeners empty. Do not create new/load/settings handlers, save files, scene transitions, placeholder popups, or click log messages. **QUIT must not call `Application.Quit` or stop editor Play Mode.** Hover, focus, and pressed-state visual feedback are the only permitted responses.

The cursor should be visible and unlocked. No keyboard shortcut may trigger a menu action. Do not add a loading screen, intro animation, music, sound effects, tooltips, resource HUD, version text, extra branding, or a settings panel.

## 6. Preserve composition at different screen sizes

Use a responsive UI layout with a 1920 × 1080 design reference. A Canvas Scaler using Scale With Screen Size provides the appropriate basis for uniform UI scaling. [Unity UI scaling reference](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html)

Keep the background and buttons within the same fitted content area. Preserve the artwork's native aspect ratio and scale that content uniformly, with button positions relative to the fitted image. On the intended 16:9 display, the artwork should occupy effectively the whole screen. For other aspect ratios, use black letterboxing or pillarboxing to preserve the complete composition and baked-in title. Never stretch the planet or crop away the title to fill the viewport.

Ensure the menu remains aligned beneath the title at 1920 × 1080, 2560 × 1440, and one non-16:9 Game view size. Do not use layout settings that scale the image and controls independently and cause their positions to drift.

This screen is a UI presentation inside a 3D URP project. Record the planned overhead/isometric gameplay camera in the README. Do not create a terrain scene or implement camera movement, zoom, rotation, selection, or other gameplay controls yet.

## 7. Git setup and repository contents

Check the target folder and remote state before initialization, since the remote could have changed after this prompt was prepared. Initialize or clone into the exact existing folder, connect `origin` to `https://github.com/Impshial/Meridian.git`, and use `main` for this initial foundation. Preserve any real files or history that already exist. Do not create another GitHub repository or force-push.

Follow the useful conventions in Sublevel's [.gitignore](https://github.com/Impshial/sublevel/blob/main/.gitignore) and [.gitattributes](https://github.com/Impshial/sublevel/blob/main/.gitattributes), adapting them for this project rather than carrying over unrelated asset-specific rules.

Track:

- `Assets/`, including Unity-generated `.meta` files for committed assets and folders.
- `Packages/manifest.json` and `Packages/packages-lock.json`.
- `ProjectSettings/`, including the editor version and build configuration.
- `.gitignore`, `.gitattributes`, `README.md`, and the relevant project documentation/reference image.

Ignore Unity caches and generated output: `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build/`, `Builds/`, captures, recordings, generated solution/project files, local IDE caches, and operating-system clutter. Do not commit build executables, Unity license material, credentials, or machine-specific logs. Never ignore all `.meta` files or the package lockfile.

Set Force Text serialization and Visible Meta Files. Use text normalization for Unity YAML and code, and binary handling for images/fonts and other binary formats. Inspect whether Git LFS is part of the current local reference conventions or is needed for an actual large asset. The committed Sublevel baseline uses ordinary binary attributes for PNGs, so these menu images do not require introducing LFS by default. If LFS is warranted by the real files or local convention, configure and verify it before staging those files; never place Unity YAML or `.meta` files in LFS.

Use the existing Git identity and authentication. After validation, review the staged files, create an initial commit such as `Initialize Meridian Unity project and main menu`, and push `main` with upstream tracking to the existing origin. If the remote now has content, integrate it normally. If authentication or a repository rule prevents pushing, retain the completed local commit and report the exact remaining step without claiming the push succeeded.

## 8. Documentation and validation

Write a concise `README.md` covering the game title, this milestone's scope, exact Unity version, render pipeline and package version, input system, target platform, configuration source, scene path, and how to open/run the project. State clearly that all four buttons intentionally have no actions. Record that gameplay will use an overhead isometric view in a 3D world.

Before committing, perform the following focused checks in the actual Unity project:

1. Let package resolution, imports, and script compilation finish. Resolve project-created errors, missing scripts, missing references, missing glyphs, and invalid URP assignments.
2. Open the saved MainMenu scene and enter Play Mode. Confirm the supplied clean background appears with its one existing title and four separate Unity UI buttons.
3. Inspect the layout at the screen sizes above. Check text clarity, button alignment, aspect preservation, highlight states, and the absence of duplicate title/menu lettering.
4. Click and keyboard-submit all four buttons. Confirm they leave the screen and application state unchanged, including QUIT.
5. Reopen the saved scene or project and verify that its assets and references persist without rerunning setup automation.
6. If the installed Windows build support is available, create one Windows 64-bit smoke-test build under the ignored `Builds/Windows/` folder. Launch it and verify that MainMenu is the startup scene and the buttons remain inert. If that build or visual check cannot be performed, state exactly what was and was not validated.
7. Review Git's staged file list and confirm that source assets, settings, package files, and `.meta` files are included while caches, logs, and build output are excluded. Commit and push, then verify the remote branch points to the resulting commit.

Capture one screenshot of the actual Unity menu after validation and make it available with the completion report. Distinguish the implemented Unity screen from the supplied design reference. A broad automated test suite is unnecessary for this static milestone.

Finish with a short report containing the project path, editor and pipeline versions, configuration source, scene path, validation performed, screenshot location, commit hash, push status, and any concrete blocker. Leave a working, saved Unity project ready for the next milestone.
