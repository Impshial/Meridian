# Configuration decisions

Inspected on 2026-09-14. The requested `C:\Users\impsh\Documents\Unity` parent contained only the empty Meridian folder. Unity Hub's existing registry located the real reference roots at `F:\Development\Codex\Sublevel` and `F:\Development\Codex\what_light_remains`; both had valid `ProjectSettings/ProjectVersion.txt` files. No applicable AGENTS.md was present in Meridian or its ancestor folders.

The current committed Sublevel baseline was also inspected at revision `9c184f4c647095685910ed92c4144765571ff153`. Reference projects were read only; no gameplay assets, cloud IDs, input bindings, or scenes were copied.

| Setting | Decision |
| --- | --- |
| Editor | Both local references: 6000.5.8f1, revision 5cb7df797b7d |
| Pipeline | Both: URP 17.5.0 |
| Input | Both local references now use `activeInputHandler: 1` / Input System 1.20.0; this shared change supersedes the brief's committed Old Input baseline |
| Company | Sublevel is DefaultCompany; the other uses its game title. Retain DefaultCompany rather than transferring another game's name |
| Color / editor mode | Linear / 3D |
| Serialization / metadata | Force Text / Visible Meta Files; LF for new C# scripts and tracked text assets |
| Play Mode reload | Domain and scene reload enabled, matching Sublevel |
| Screen | 1920 × 1080, Fullscreen Window |
| Windows graphics API | Automatic; neither reference explicitly overrides the Windows API list |
| Windows backend and API compatibility | Matching editor defaults resolve to Mono (`Mono2x`) and .NET Standard 2.1 (Unity's legacy API enum name `NET_Standard_2_0`, serialized value 6) |
| Quality | References disagree (Sublevel's six levels versus What Light Remains' Mobile/PC). Use Sublevel's Very Low through Ultra conventions; desktop default Ultra |
| URP references | Meridian pipeline and renderer created through Unity APIs; assigned in Graphics and every quality level |
| Global render resources | Preserve Unity template-created global settings/default volume resources at Meridian paths; screen-space overlay UI has no post-processing |
| UI | uGUI 2.5.0 / TextMeshPro, bundled Liberation Sans, four editable prefab instances |
| Input bindings | Fresh UI-only action asset: pointer, left click, arrows, Enter/Space, gamepad D-pad/submit. No gameplay bindings |
| Git LFS | Neither reference requires PNG LFS; supplied images are small. Ordinary binary attributes used |

Unity itself created the project with its installed URP template (`com.unity.template.3d-cross-platform-17.0.14.tgz`), upgrading package versions to those matched to this editor. Unrelated template packages and sample assets were removed. No Unity or GitHub cloud project was created.

The original artwork is 1672 × 941 and preserved byte for byte. It is displayed at native aspect ratio in a fitted design surface 1920 units wide. Controls use that same surface, avoiding drift as aspect ratio changes. The Canvas Scaler uses a 1920 × 1080 design reference; the small difference from exact 16:9 is intentionally preserved.

The editor reports automatic Windows graphics APIs in this order: Direct3D12, Direct3D11. Graphics jobs and multithreaded rendering resolve to enabled. These are matching-editor defaults, not an explicit graphics API override.

The saved brief is task reference material, not a standing instruction file for future agents. Future work should follow the user's current request.
