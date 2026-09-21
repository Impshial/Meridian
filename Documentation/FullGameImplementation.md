# Full-game implementation ledger

The full-game implementation, connected acceptance checks and Windows build are delivered. The normal player is at Builds/Windows/Meridian.exe. See the validation record for evidence and material limits.

## Implemented

- Serializable stable identities and one simulation clock; data-driven catalog with 43 structures, recipes, research and balance.
- Skippable ship descent and idempotent deployment: six work robots, two forestry bots, four drones, exact supplies, ship/apron, and 48 sleepers retained in orbit.
- Finite inventories and reservations, physical hauling, construction, clearing, refunds, repairs, machine recovery and return-energy budgeting. Protected civilian building work; industrial construction and service remain machine-only.
- Separate pressure, power and water networks; finite air/energy/water, needs, staffing, actual tending, worn equipment service and attended training.
- Physical personnel and cargo flights, orbital recruitment, visitor travel/services/wallets/departure, research, events, reputation and funded long-term planetary restoration.
- Neighboring-region generation, ownership, shared geographic frame, persistent depletion, expanded camera bounds, bounded detailed terrain and entity views.
- Checked atomic saves/backups, exact double timestamps, paused reconstruction, cancellation/failure recovery, autosaves and safe session transitions.
- Imported supplied model prefabs, procedural spacecraft/utility kit/interiors/bipeds, dome Frames controls, portraits, effects and synthesized audio.
- Runtime inspectors and management panels, captured window dragging/resizing/docking/tabs, layouts, settings, save browser, guide, working menu actions and preserved camera controls.

## Acceptance evidence

See [FullGameValidation.md](FullGameValidation.md) for measured results, actual observations and limitations. Passing logs are under ignored `Logs/`:

- `ColonyValidation`: authority, manifest, conservation, industry eligibility and bot construction.
- `ColonyIntegratedValidation`: complete bot-built opening, two personnel flights, twelve healthy people through six subsequent days, production and exact disk snapshot comparison. Rerun after charge safeguards passed.
- `ColonySystemsValidation`: trade, EVA, production/miner service, persistence failures and accelerated terraforming. Rerun after charge/room safeguards passed.
- `ColonyProgressionValidation`: actual visitors, adjacent region, border continuity and saved cross-border hauling.
- `ColonyRecoveryValidation`: terrain water mask, equipment service, attended training, disabled-machine recovery and invalid-save rejection.
- `ColonySafeguardValidation`: remote return-energy budget, replacement flight accommodation, and outdoor-farm environment/tending/input contracts.
- `ColonyLifecycleValidation`: actual saved-scene flow, watched and early/late skipped arrival, queued saves, three sequential sessions and cancelled reconstruction.
- `ColonyPlayValidation`: runtime GUI event harness, simultaneous inspectors, scaled gestures, docking/undocking/splitters, modal and wheel ownership, E rotation, focus loss, Frames invariance and resolution reachability.
- `ColonyStressValidation` and `ColonyFrameValidation`: 200 residents, 100 visitors, 150 machines, 530 structures, two regions; measured time/memory and bounded unload/reload.

Native Windows pointer automation is unavailable on this host. UI gesture checks use the actual UI in an Editor event harness; this is not native OS input validation. Stress rendering averages 26–28 ms with occasional ~100 ms spikes on the measured hardware, not steady 60 FPS. Audio is implemented but subjective playback quality is not certified.

## Final changes from acceptance

The arrival camera now follows the descending ship instead of staring at empty ground. Snapshot status includes its initial frame of preview preparation. Cleared trees are filtered before restored terrain upload. Portrait rendering runs outside GUI callbacks. Covered interiors are hidden to reduce rendering cost. Staffing is gathered before requests to remove actor-order dependencies. Training was tuned to eight colony hours of attendance. Input Handling is Both for runtime IMGUI; existing Input System controls and all package versions remain unchanged.

## Delivery workflow

`Meridian > Colony > Build Windows Player` produces the normal `Builds/Windows/Meridian.exe` from the three saved scenes. An ignored, separately compiled acceptance player is allowed to load fixtures and capture smoke evidence; its symbol is absent from the normal build. Local saves, fixtures, diagnostics, captures and build outputs stay ignored. Commit required `.meta` files and the six supplied asset imports through the established main-branch workflow; do not force-push.

Windows verification completed: normal x64 build has zero errors/warnings; visible acceptance run passed in 78.72 seconds with no logged errors. Screenshot review caught and resolved missing player terrain by preserving Unity runtime-terrain build resources in an inactive serialized support component. Normal startup contains no test harness.
