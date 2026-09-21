# Full-game validation

Validated on the existing Unity 6000.5.8f1 project, Windows, Intel i7-5820K at 3.30 GHz, RTX 3070 Ti and 65,436 MB RAM. Seed 73129 and the recorded planet/surface parameters were used for repeatable scenarios. This record distinguishes actual simulation, rendered observation and deliberately granted development scenarios.

## Connected game loop

The bot-built opening used 526 Metal, 315 Minerals and 94 Components to construct 70 structures, including physical utility/corridor connections. No instant construction or material grants were used. Construction completed on day 6.95, the first six-person flight arrived on day 8.14, and the second on day 9.33. All twelve residents remained healthy through day 15.33; food increased from 387.5 to 469.0 over six day/night cycles. Pressure split/reconnection conserved finite stored air. The complete authoritative snapshot matched bit-for-bit after disk publication/read.

Connected scenarios exercised physical import/export cargo, one-time charges/refunds/payment, saves during freight travel, recruitment into orbit, protected civilian construction and return, saves mid-EVA, machine-only industrial eligibility, committed production batches, bot-built miners, power interruption, parts-based maintenance and replacement without resetting reserves. Corrupt/version-mismatched saves, failed writes and interrupted temporary files preserved the previous valid backup.

A hospitality development branch granted 100 research points and 350 Metal / 250 Minerals / 100 Components, then used actual bots and hauling to build its district. Six staff arrived by flight; six visitors used services, spent exactly 356 credits from their wallets, departed alive and released their beds. Reputation reached 60.6.

An accelerated terraforming branch supplied real reservoirs/buffers and funding. It stopped without inputs, respected rate caps and completed in 71.45 nominal game years. This was an accelerated branch check, not a manually played seventy-year settlement. No mandatory end was triggered.

## Terrain, recovery and lifecycle

- The actual saved scene flow ran MainMenu → PlanetSelection → LandingSiteSelection → arrival. Watched arrival and skips at 0.5 and 6 seconds produced identical supplies, twelve machines, one ship/apron and 48 orbital sleepers. Queued cinematic saves restored deployed state. Three successive sessions cleaned up generated terrain and runtime objects.
- Load cancellation retained the active colony, clock speed and identity. Reconstruction restores paused and consumes no offline time.
- A 36-tile adjacent region generated in 18.34 seconds; six shared borders matched exactly. Cancel/retry committed ownership and its price once. A carrier physically moved 30 Metal across the boundary after a save; existing geography and depletion stayed fixed.
- 1,600 retained water-mask queries agreed with geographic samples. Worn EVA equipment consumed one component and preserved servicing progress through save/load. Actual attended profession training survived meals/sleep and reconstruction. A disabled forestry machine was towed, charged and retained cargo. Missing nested tables and non-finite values were rejected.
- Training was tuned to eight colony hours of attendance (40 simulation seconds), halved by Civic Development. Actual elapsed time includes travel and needs. The original twenty-four-hour attendance target was too slow in the integrated settlement.

## Performance measurements

Representative full surface generation: 26–44 seconds, depending on cache/run; the lifecycle rerun measured 29.12 seconds of numerical generation. These measurements exclude planet generation and Unity resource upload. Connected save reconstruction measured 40.17 seconds before scene/resource upload, including planet recreation.

The stress fixture grants development resources and places 200 residents, 100 visitors, 150 machines, 530 structures and 177,130 indexed natural resources across two regions. It is a performance/conservation fixture, not an opening-balance scenario. A 0.1-second simulation tick averaged 22.06 ms, p95 65.35 ms and maximum 140.63 ms. Full snapshot publication/read took 0.61 seconds for 2,041,139 bytes. This measurement preceded the final attendance and remote-charge safeguards.

Actual Editor rendering at 2560 × 1440, 1× simulation, 60 FPS cap, three 300-frame samples:

| Focus | Mean / p95 / max frame time | Terrain views | Prop batches | Entity views | Unity / managed MB |
| --- | --- | --- | --- | --- | --- |
| First region | 26.31 / 59.89 / 96.64 ms | 49 | 1,639 | 539 | 987 / 907 |
| Second region | 26.99 / 61.62 / 107.19 ms | 49 | 1,446 | 430 | 1,035 / 962 |
| Return to first | 27.90 / 62.90 / 105.46 ms | 49 | 1,639 | 539 | 993 / 909 |

Earlier untuned mean frame times were 78–149 ms. Spatial/cache work, bounded nearby views and hiding covered interiors reduced them substantially. The developed stress fixture does **not** maintain steady 60 FPS; path/job spikes remain visible. Generated visual working sets unload and return to their earlier size instead of growing with every camera move.

## Presentation and input evidence

Unity captures show the descending 3D spacecraft, supplied imported models, connected settlement, visible interiors/occupants, and the rendered biped inspector portrait. Captures remain local under ignored `Captures/`. Scene generation and arrival were exercised in Play Mode.

Native Windows pointer/capture automation is unavailable on this host (capture interface error 0x80004002 and missing coordinate geometry). Runtime window gesture tests use the actual UI component hosted in an Editor event window; this checks scaled title dragging, resizing, docking, tab-undocking, splitters, layout serialization and modal capture, but is not evidence of native OS mouse delivery. Input System virtual devices exercise camera key/wheel ownership. Active Input Handling is Both because Unity IMGUI needs its native event backend; the existing Input System controls and package version remain unchanged.

## Reproduction and boundaries

Tracked checks are under `Assets/_Meridian/Editor/Colony`, with commands in `Meridian > Colony`. `Integrated`, `Systems`, `Progression`, `Recovery`, `Safeguards`, `Lifecycle`, `PlayAcceptance`, `Stress` and `MeasureViews` write their evidence under ignored `Logs/`. Generated fixtures are in ignored `Library/GameValidation`. Run async validators to completion before changing scripts or leaving Play Mode.

The game uses original procedural utility pieces, interiors, spacecraft and simple bipeds alongside the six supplied model packages. Sounds are synthesized. These are functional production systems with prototype presentation; bespoke animation/audio and broader hardware/balance coverage remain polish work. Input gestures that require native Windows automation and subjective audio playback are not claimed as manually verified.

## Windows player

The first standalone smoke completed simulation/save/cleanup, but screenshot inspection exposed missing ground rendering. Unity requires a serialized Terrain in a build scene even when gameplay creates all terrain dynamically. The landing-screen prefab now contains an inactive one-meter support Terrain and the terrain material retains instancing variants; the normal build command validates this dependency. See [Unity 6.5 runtime Terrain requirements](https://docs.unity3d.com/6000.5/Documentation/Manual/terrain-Runtime.html).

Hidden Windows launches suspended the graphics loop and could not capture images on this host. The acceptance player is launched through the desktop app surface for visible render verification; those earlier hidden attempts are not passing evidence.

Final visible Windows acceptance run: PASS in 78.72 seconds at 1920 × 1080, with no error/exception log. It opened the saved MainMenu and settings, reconstructed the twelve-person colony, rendered and visually verified textured terrain, furnished frames and a biped portrait, advanced twelve simulation seconds, saved/restored a player snapshot, and returned to the menu with no active colony runtime or generated terrain. Evidence is under `Builds/Acceptance/Smoke`; the acceptance fixture/harness is excluded from the normal player.

Final normal Windows x64 build: **Succeeded, zero errors, zero warnings**, 35.8 seconds, 165,785,732 bytes reported by Unity. Path: `Builds/Windows/Meridian.exe`. Its compiled gameplay assembly contains no `ColonyPlayerSmoke` harness. Saved menu and runtime-terrain dependency validators passed after reopening assets. Unity metadata and staged whitespace checks passed.
