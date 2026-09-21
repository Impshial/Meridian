# Meridian: complete game implementation prompt

Work in `C:\Users\impsh\Documents\Unity\Meridian`, connected to `https://github.com/Impshial/Meridian`. Extend the existing Unity project into a complete, playable colony management game. Implement the systems in this prompt, connect them into one functioning game, validate the result, and produce a Windows build. This is an implementation request, not a request for a design document or another series of prompts.

Use internal checkpoints to manage the work, but continue through the full scope. Keep a concise implementation ledger in the repository so you can resume after context compaction. Complete functional systems before polishing secondary visuals. Clean, original placeholder models and effects are acceptable; disconnected demonstrations, placeholder buttons, and menus whose advertised functions are unimplemented are not completion.

## 1. Inspect and preserve the current project

Read the applicable repository instructions, inspect the working tree and current implementation, and use the existing Unity version, render pipeline, input system, build configuration, and Git workflow. Preserve unrelated work. Do not recreate the project, downgrade it, or replace working systems without a concrete need. The local checkout may be newer than this prompt; inspect it before deciding what requires implementation.

The reviewed repository and latest supplied work log reached commit `18a44c815644403540478a95c3d78ab89cefacb4`. At that baseline:

- Unity is `6000.5.8f1`, with URP `17.5.0`, Input System `1.20.0`, and uGUI `2.5.0`.
- The main menu, procedural planet selection, and landing-site survey already work.
- The initial surface is **6 km by 6 km**, consisting of 36 one-kilometer terrain tiles. Preserve that scale, the improved ground textures, realistic tree groves, water, buildable plateaus, and corrected camera behavior.
- The planet uses its improved 4096 by 2048 appearance maps and additional procedural detail. Its current zoom range is approximately 45% to 210% of viewport height. Preserve the ocean improvements and close-view clarity.
- Landing-site generation has bounded parallel numerical work, meaningful progress, a BACK TO PLANET cancellation action, cleanup of stale results, and an independent 180-second recovery watchdog. The supplied log reports 43 seconds for a representative test that previously took 117 seconds. Treat this as a comparison case, not a guarantee for every machine or seed. The exact original indefinite stall was not reproduced.
- LAND HERE currently records confirmation and ends the prototype flow. This prompt extends that point into arrival and the actual game.

Reference: [current landing-site implementation notes](https://github.com/Impshial/Meridian/blob/18a44c815644403540478a95c3d78ab89cefacb4/Documentation/LandingSiteSelection.md).

Inspect `SetupSession`, the setup record, `SurfaceSelectionController`, `SurfaceWorldData`, `SurfaceGenerator`, `SurfaceLandscape`, `SurveyCamera`, the existing transitions, and current validation tools. Extend their actual contracts. Do not invent filenames or assume the old milestone's smaller survey dimensions still apply.

Maintain immediate loading feedback from New Colony through planet generation and from Continue through landing-site generation. Preserve the latest checked-in planet-loading wording and the explicit `Loading Landing Site...` label. Progress must describe actual work. Cancellation, errors, and timeouts must return to a usable screen without retaining unwanted terrain or accepting stale completions.

## 2. Game identity and rules that must hold throughout

Meridian is a 3D colony management RTS on a surveyed exoplanet that is close to human habitability. The player commands from a ship in geosynchronous orbit. The early game establishes a settlement; the continuing game maintains, expands, improves, and promotes it as a desirable destination in the galactic network. There is no mandatory victory ending.

Use indirect control. Players place structures, designate harvesting, allocate capacity, set facility and project priorities, choose recruitment and training, and authorize arrivals. Colonists and machines select suitable work automatically. Selecting an individual provides information and relevant management controls, not right-click movement orders or a requirement to assign every job by hand.

These are explicit requirements:

1. **All colonists begin asleep aboard the orbital ship.** The initial surface dropship carries machines and supplies, with no colonists aboard. Every later addition to the surface population requires an actual dropship journey carrying those colonists from orbit.
2. **Bots perform initial construction.** After colonists with construction skills arrive, they can automatically contribute to suitable building construction, interior installation, and civilian repairs. Bots remain available and useful.
3. **Industrial equipment is worked on only by machines.** This includes its construction, installation, operation, servicing, and replacement. A construction-skilled colonist must never become eligible for an industrial job merely because they are idle or because terraforming has finished.
4. Industrial miners are physical equipment that the player must build over compatible deposits. They extract autonomously using their own machine controllers. They need power, maintenance, available reserves, and output capacity. They are not free, permanent, or indestructible resource sources.
5. Specialized forestry bots clear foliage and trees and collect the resulting resource. Ordinary transport drones do not silently gain forestry abilities.
6. Habitable buildings are **separate sealed domed modules joined by pressurized corridors**. Outdoor industrial equipment is an explicit exception to the building enclosure and corridor requirement.
7. Every relevant world object has its own properties window. Colonist windows are comprehensive. Windows are movable, resizable, closable, and dockable, and multiple object windows can remain open.
8. Colonists and mobile robots have real inventories with owned items, finite capacity, and persistent contents.
9. Each domed building has an interior-view toggle available on hover. A master control switches all buildings between full domes and structural frames with interiors visible.
10. Saving, loading, autosaving, a functioning economy, autonomous behavior, neighboring-region expansion, and the continuing colony/visitor loop must work together.

The numerical values below are initial balancing defaults. Put them in editable definitions and tune them if playtesting exposes a concrete problem. Preserve the rules above and report material balancing changes. Resolve ordinary implementation choices yourself.

## 3. Shared simulation contracts

Build a coherent simulation rather than separate feature scripts that maintain conflicting copies of the same state.

- Give worlds, regions, structures, corridor segments, deposits, natural objects, agents, shipments, jobs, inventories, and items stable identities where identity is necessary. Scene instance IDs are not persistent identities.
- Separate authoritative simulation data from Unity rendering, scene objects, UI, and animation. Use data definitions for buildings, recipes, equipment, professions, research, and events.
- Use one simulation clock with pause and 1x, 2x, and 4x speeds. A fixed simulation step around 10 Hz is a reasonable starting point. Speed changes must not double-apply time or skip resource consumption, job completion, or arrival transactions.
- Use unscaled time for UI transitions, camera input, window interaction, and the initial cinematic. Planning and changes to priorities remain available while the simulation is paused.
- Start with 120 simulation seconds per colony day and 12 colony days per game year. Label this as a deliberately compressed game calendar. Use the same calendar consistently for production rates, needs, visitor stays, maintenance, and terraforming. A colony hour is one twenty-fourth of a colony day; battery kWh calculations must use that hour, not an unrelated real-time conversion.
- World simulation begins after the initial arrival has been finalized. Do not consume surface food or age a nonexistent colony during setup and loading.
- Every stored resource has exactly one owner. Reservations restrict existing stock; they do not create more stock. Cargo in transit is not simultaneously counted in a warehouse and its carrier.
- Production, arrivals, construction completions, demolition returns, purchases, and transfers each commit once. Save/load and retry paths must preserve that property.
- Eligibility checks belong in the simulation. Hiding a button is insufficient to prevent humans from accepting industrial work.
- Expose understandable reasons when work cannot proceed: missing material, insufficient charge, no safe route, wrong profession, no airlock, full output buffer, disconnected power, or exhausted deposit.
- Preserve seeded procedural generation and save event/random-generator state. Loading must not reroll weather, terrain, deposits, traits, or pending breakdowns.

## 4. Finish the setup flow and preserve camera controls

Preserve planet rotation by left-button drag, wheel zoom, and the fixed planet camera. Keep click-versus-drag discrimination, land selection, its expanding-circle feedback, automatic centering of the selected point, and the readable planted flag. Keep the marker anchored to its selected geographic position as the planet turns. Adjust the flag's presentation for readability; when its location is behind the globe, use the existing location indicator or an equivalent readable cue without moving the geographic anchor or showing the flag through the planet.

Continue enters the existing surface survey with loading feedback. The selected location, terrain, coast, watercourses, resources, and neighboring geography must agree with the original planet data. LAND HERE becomes the explicit commitment to start the colony and its arrival cinematic.

Preserve the refined surface and colony camera controls:

| Input | Behavior |
|---|---|
| WASD / arrow keys | Ground pan relative to the view |
| Middle-button drag | Pan along screen axes without changing heading or tilt |
| Mouse wheel | Tilt the overhead view |
| Either Shift + mouse wheel | Zoom |
| Q / E | Smooth, accumulated 45-degree heading turns |
| + / - | Keyboard zoom, including existing main-keyboard and keypad support |
| Home | Initial survey focus before landing; dropship/colony center afterward |
| F | Focus the selected object during gameplay |
| R / Shift + R | Rotate the current placement preview in opposite directions |
| Space | Pause/resume during gameplay |
| 1 / 2 / 3 | 1x / 2x / 4x simulation speed |
| B / H | Construction menu / harvesting designation |
| F5 / F9 | Quicksave / quickload with protection against accidental loss |

Respect existing candidate-selection and cancellation behavior before landing. After landing, use left-click for selection or the active tool, and right-click/Esc to cancel the active tool. Esc otherwise closes the foremost dismissible window or opens the pause menu. Text entry and modal dialogs take precedence over gameplay shortcuts.

Keep the tested boundary clamping, terrain clearance, and absence of diagonal drift at survey edges. Expand movement bounds as regions are acquired. Use the current zoom limits as the baseline; if interior inspection requires closer framing, add a safe, tested extension rather than replacing the controls.

## 5. Initial arrival cinematic and permanent starting state

The first dropship carries only machines and the initial supply manifest. Sleeping colonists remain in orbit.

After LAND HERE:

1. Finalize the valid location and heading once, disable further site edits, and transition from the survey camera to a temporary low oblique cinematic view.
2. Animate a real 3D dropship descending toward the selected footprint. Reuse the existing preview dimensions and landing-clearance rules so the landed craft fits the tested site.
3. Show booster effects underneath the craft, directed downward and attached to the correct thrusters. Vary intensity through approach, deceleration, and touchdown. Add restrained engine lighting, sound, and ground-proximity dust that respects the surface.
4. Settle the landing gear on the actual terrain, shut down the boosters, open the cargo ramp, and deploy the starting machines.
5. Restore the player's overhead view and begin normal simulation with a short set of actionable opening objectives.

Target roughly 10 to 15 seconds. Provide Skip through a visible button and an appropriate key. Skipping at any point must produce exactly the same landed state, inventories, machines, and camera/input ownership as watching the entire animation. Avoid duplicate spawning from both animation events and completion callbacks.

The initial cargo dropship remains as a useful landed structure. It supplies starter storage, communications, limited electrical support, two charging positions, and a sealed connection port. It contains **no cryosleep population**. Do not offer an action that awakens people out of this surface ship.

Create a small physical personnel landing apron/gangway as part of this deployed starting installation. It is included in the initial manifest and connects through the ship's sealed port. Later dropships land there and discharge through an enclosed gangway into the pressurized network. It must have a real footprint, collision/clearance, occupancy, and properties window, not an invisible teleport location. Larger spaceports can replace its throughput later.

Once the colony has begun, returning to planet selection cannot move the same colony to a fresh site for free. New Colony starts a new world after the normal unsaved-progress handling.

## 6. Starter manifest and orbital population

Use these editable starting defaults:

| Starting asset | Amount |
|---|---:|
| Construction/work robots | 6 |
| Forestry bots | 2 |
| Transport drones | 4 |
| Metal | 600 |
| Minerals | 500 |
| Components | 180 |
| Biomass | 120 |
| Iron Ore | 160 |
| Copper Ore | 120 |
| Food | 360 |
| Medicine | 24 |
| Water in a separate ship tank | 600 |
| Reusable EVA suits | 12 |
| Reusable civilian construction toolkits | 8 |
| Credits | 20,000 |

The surface ship has at least 4,000 units of dry cargo capacity, an 800-unit water tank, 30 kW of net usable starter power, two charging positions, and communications for orbital transfers and trading. Its own essential systems are accounted for before that net output. Starting equipment and cargo are created exactly once.

Start with 48 individually identified colonists asleep aboard the orbital ship:

| Profession | Count |
|---|---:|
| Builders | 12 |
| Botanists | 8 |
| Medics | 4 |
| Scientists | 6 |
| Technicians | 6 |
| Service workers | 12 |

Give each person a name, portrait or consistent portrait placeholder, attributes, skills, and traits. The orbital roster shows those properties before the player brings them down. Orbital cryosleep is independently supported and does not drain surface utilities. These are design defaults, not a requirement to force all 48 onto the surface quickly.

The player selects a batch from the orbital roster, reviews available surface beds and life support, and requests a personnel dropship. A starting dropship carries up to six people, takes one colony day to prepare/transit, and costs 100 credits for the flight. Support a queue, initially with one flight active at a time. Warn before committing an unsafe manifest and explain missing capacity. Prevent impossible arrivals with no intact landing connection; allow understandable risk decisions about a merely low food reserve.

Track each person through Sleeping, Reserved, Waking, In Transit, and Arrived states. Reserve the actual people and their intended beds; do not create copies at destination. A canceled request before launch releases its reservations. After launch, cancellation becomes an explicit return-to-orbit action. If the surface connection becomes unsafe, hold or return the ship safely, with a visible reason and no loss or duplication of its manifest.

Show subsequent dropships descending with booster effects and docking at the landing facility. They do not repeatedly steal the gameplay camera. Passengers physically disembark through the sealed gangway and enter the colony. Surface population changes only when those real people arrive. Each flight then departs and frees the pad.

When the initial roster is insufficient, recruitment orders bring new people to the orbital ship through the galactic network. They enter the orbital roster and still require a surface dropship flight. Do not implement a recruit button that instantly spawns colonists beside a habitat. Preserve flights, manifests, reservations, and arrival progress across saves.

## 7. World resources, harvesting, and logistics

Use the existing terrain and resource records as the world source of truth. The current surface data includes trees, rocks, iron, copper, and ice, with stable object IDs, resource grouping, and tree wood quantities. Preserve those identities and extend their persistent state.

Use these core goods: Iron Ore, Copper Ore, Minerals, Biomass, Metal, Components, Food, Medicine, and Water. Credits are money; electricity and breathable-air supply are utilities. EVA suits and toolkits are reusable equipment with ownership and condition.

- Trees and usable foliage yield Biomass. A timber label can describe tree-derived biomass, but do not count the same wood in two resource ledgers.
- Rocks yield Minerals. Iron and copper deposits produce their corresponding ore. Suitable ice can yield Water through an appropriate machine process.
- Keep finite quantities, depletion state, compatible extraction methods, and output rates in each deposit's properties.
- Trees and foliage removed for construction also yield recoverable resources. Do not erase a grove's inventory merely because a blueprint overlaps it.
- Provide click and drag-area harvesting designation, clear visual selection, cancel, Normal priority, High priority, and Pause. The operation creates jobs rather than moving units directly.
- Work robots can collect suitable loose mineral resources. Forestry bots fell trees, clear foliage, collect their output into their own inventory, and either deliver it or hand it off through a valid logistics job. Transport drones carry goods between actual inventories.
- Harvesting removes the relevant visual and collision representation. Where groves are batched, update the affected batch and its selectable proxies rather than creating a heavyweight actor for every decorative leaf.
- Store depletion and removal deltas independently of visual unloading. Regenerating a tile, reopening a save, or acquiring a neighbor must not restore harvested trees or exhausted ore.

Implement storage filters, capacities, small local output buffers, hauling reservations, and reasonable production stock targets. A blocked project should identify the exact missing material and whether it is unavailable, reserved elsewhere, or in transit. Display totals in a way that distinguishes usable stock, reserved stock, and moving cargo without counting anything twice.

Ship supplies are enough to bootstrap extraction and manufacturing. Basic charging, mineral gathering, forestry, miners, smelting, component production, water, food production, and repairs must not be locked behind the people or research that depend on them.

## 8. Autonomous jobs, agents, and inventories

Use a shared job system with capabilities, reachable targets, priorities, reservations, progress, cancellation, and recovery. Avoid each actor scanning every object every frame.

| Actor | Main responsibilities |
|---|---|
| Work robot | Construction, repair, dismantling, equipment installation, industrial servicing, suitable mineral collection |
| Forestry bot | Tree felling, foliage clearance, biomass collection and delivery |
| Transport drone | Material delivery, pickup, inventory transfers, outbound trade cargo |
| Fixed industrial miner | Automatic deposit extraction using its built-in controller |
| Builder colonist | Civilian building/corridor construction, interior installation, eligible civilian repairs |
| Other colonists | Work appropriate to their profession, with training and permitted secondary civilian tasks |

Bots perform every essential opening job without humans. Once builders arrive, they join the eligible civilian construction pool automatically. Bots are not disabled or discarded at that milestone. The player can set a colony-wide civilian labor preference such as balanced or prefer colonists, but survival must not depend on per-person assignments. All industrial tasks remain machine-only, including building a replacement miner.

Mobile machines have charge, condition, movement, a job state, tools/capabilities, and inventory. They reserve reachable charging capacity and enough energy to return. Include a recovery job for stranded or disabled machines, such as a work robot delivering an emergency charge from an available supply. The starter installation needs a practical recovery route from an ordinary first power shortage.

Jobs should transition visibly through waiting, obtaining supplies, traveling, working, delivering, and completion. Charge or personal-needs interruptions preserve or release reservations appropriately. Use bounded retry/backoff and clear blocking reasons for unreachable targets. Provide emergency precedence for time-critical life-support repairs while retaining fair progress for ordinary work.

Implement genuine inventories:

- Colonists: an initial eight cargo slots with a 20-unit total carrying limit, plus equipment slots.
- Work robots: 60-unit cargo capacity; forestry bots: 80; drones: 100. Use stack limits appropriate to the item definitions.
- Agents collect and deliver real stock. Construction material does not appear in a worker's hands without leaving a source inventory.
- Personal food, drinking water, and required civilian tools are automatically replenished through reachable facilities. Equipped items still have one owner and cannot also remain available in storage.
- Inventory views show contents, amount, capacity, equipped gear, and reservations. Management actions must respect proximity or create actual transfer jobs.
- A broken robot, dropped stack, or deceased colonist leaves recoverable contents unless a specific event explicitly destroys them. Cancellation and despawning must not silently delete or duplicate cargo.

Water in tanks and carried water share one accounting model. Filling a carried container subtracts from its source. Utility displays must not also count that same water as still in the tank.

## 9. Construction, equipment placement, repair, and replacement

Provide a construction catalog grouped into housing, survival, storage/logistics, industry, utilities, services, visitors, and planetary development. Selecting an item produces a readable preview with footprint, doors, connection ports, cost, clearance, and rotation. Prefer free placement with helpful snapping to ports and neighboring structures.

Placement validates ownership of the region, slope, water, obstructions, reserved footprints, entrances, corridor clearance, and any required deposit. State why a preview is invalid. A planned building can await a corridor, but must clearly show that dependency and cannot operate as a habitable disconnected module.

The construction sequence is designation, necessary clearing, material delivery, build work, connection/commissioning, and operation. Show visible unfinished structures and progress. Forestry bots handle the vegetation-clearing prerequisite. Eligible builders and work robots handle the subsequent civilian work. Industrial equipment uses machine labor throughout.

Material delivery and labor are separate requirements. Reserve and consume the correct quantities as work commits. Never charge full construction cost again after loading a partially completed site. Pause, resume, change priority, cancel, and dismantle must work. Recover unconsumed delivered materials in full and return a configurable fraction, initially 60%, of incorporated materials on dismantling. Recovered material becomes physical cargo requiring collection.

Miners must be selected from the catalog, placed over a compatible deposit, supplied, and built. Bind each miner to the actual deposit ID and enforce its permitted footprint/extraction claim. Prevent overlapping claims from multiplying extraction or overdrawing the deposit. The miner's built-in controller then runs without a colonist or a mobile robot permanently standing at it. Service robots visit when needed; haulers collect output.

Miners and all other constructed equipment have condition and real lifecycle controls. They can stop working, be repaired with parts and labor, be dismantled, and be replaced by newly constructed equipment. Replacement must reserve the footprint, safely stop the old equipment, recover its output/cargo, and create a real new construction job with a displayed net cost. No free instant upgrade, unlimited durability, automatic free respawn, or disappearance of stored ore. An exhausted deposit stops extraction and prompts the player to relocate or dismantle its equipment.

Use reasonable site preparation to accommodate small terrain variations while preserving the existing hills, water, and broad flat building space. Do not flatten the whole survey or reshape neighboring tiles whenever something is built. Persist any local terrain modification and keep the rendered surface, placement tests, and pathfinding in agreement.

## 10. Initial construction catalog and production definitions

In the following tables, costs are **Metal / Minerals / Components**. Electricity values are initial balancing settings. All entries are actual buildable objects with corresponding properties and simulation behavior. Research gates are specified later; the opening survival chain is available immediately.

### Sealed domed modules

| Module | Cost | Power | Function |
|---|---|---:|---|
| Habitat | 30 / 20 / 6 | 3 kW | Eight assigned resident beds and personal storage |
| Canteen | 20 / 10 / 4 | 2 kW | Twelve seats, stored food, one service position |
| Greenhouse | 25 / 15 / 5 | 6 kW | Food growing, two botanist positions, water and biomass inputs |
| Clinic | 20 / 15 / 6 | 3 kW | Four treatment beds and a medic position |
| Laboratory | 30 / 20 / 8 | 5 kW | Two scientist positions generating research |
| Sanitation module | 15 / 15 / 4 | 2 kW | Hygiene facilities, nominal six water units per day |
| Leisure lounge | 20 / 15 / 4 | 3 kW | Resident recreation and one service position |
| Warehouse | 20 / 10 / 2 | 1 kW | 2,000 units of filtered dry storage |
| Control and training center | 20 / 10 / 6 | 3 kW | Technician supervision and profession training |
| Visitor reception | 35 / 20 / 10 | 4 kW | Check-in, two service positions, sealed passenger access |
| Guest lodge | 35 / 25 / 8 | 4 kW | Eight visitor beds |
| Restaurant | 25 / 20 / 6 | 4 kW | Sixteen seats, two service positions, paid visitor meals |
| Attraction dome | 40 / 30 / 10 | 6 kW | A staffed observation/exhibition attraction |
| Indoor garden | 35 / 25 / 8 | 4 kW | Recreation and botanical attraction, nominal four water/day |
| Airlock | 10 / 5 / 3 | 1 kW | Controlled exterior access with suit storage and air management |
| Pressurized corridor, per 10 m | 2 / 1 / 0 | 0.2 kW | Walkable sealed connection between compatible ports |

Place recognizable interior equipment in each module: beds, tables, grow beds, clinic stations, lab benches, storage racks, or visitor exhibits. Agents travel to sensible work and service positions. Interior visibility must reveal useful activity rather than empty floors.

### Outdoor equipment and infrastructure

| Equipment | Cost | Power or storage | Function |
|---|---|---|---|
| Stockyard | 10 / 5 / 0 | 1,000 cargo units | Machine-accessible open storage |
| Solar array | 20 / 10 / 5 | Up to 50 kW | Daylight/weather-dependent generation |
| Wind generator | 25 / 10 / 5 | Up to 40 kW | Wind-dependent generation |
| Battery bank | 12 / 8 / 6 | 400 kWh | Actual stored electricity with charge/discharge limits |
| Water well | 15 / 10 / 4 | 4 kW | Extracts up to 100 water/day from valid groundwater |
| Water tank | 10 / 10 / 2 | 1,000 water units | Connected water storage |
| Air processor | 20 / 15 / 6 | 5 kW | Nominal support for 32 people, ten water/day, air buffering |
| Charging station | 10 / 5 / 3 | Up to 4 kW | Two machine charging positions |
| Autonomous miner | 15 / 10 / 3 | 5 kW | Extracts compatible iron or copper deposits |
| Autonomous quarry | 15 / 10 / 3 | 5 kW | Extracts a suitable mineral deposit |
| Ice extractor/melter | 15 / 10 / 3 | 5 kW | Turns suitable ice reserves into water |
| Smelter | 20 / 15 / 5 | 6 kW | Produces structural metal |
| Component assembler | 25 / 20 / 8 | 8 kW | Produces manufactured components |
| Medicine fabricator | 25 / 15 / 8 | 4 kW | Produces medicine |
| Robot fabricator | 35 / 20 / 10 | 8 kW | Builds replacement or additional mobile machines |
| Repair pad | 20 / 10 / 5 | 4 kW | Machine repair and recovery support |
| Geothermal plant | 80 / 50 / 20 | 60 kW | Research-unlocked steady generation on suitable sites |
| Spaceport apron | 60 / 40 / 15 | 6 kW | Larger physical arrivals, linked to sealed reception |
| Atmosphere plant | 120 / 80 / 40 | 25 kW | Atmospheric terraforming, nominal 20 water/day |
| Climate array | 100 / 100 / 40 | 30 kW | Climate terraforming, nominal four minerals/day |
| Biosphere processor | 120 / 80 / 30 | 20 kW | Soil/water restoration, eight biomass and 20 water/day |
| Power cable, per 10 m | 1 / 0 / 0 | Network connection | Visible, inspectable electrical distribution |
| Water pipe, per 10 m | 1 / 1 / 0 | Network connection | Visible, inspectable water distribution |

Outdoor industrial equipment is built, operated, maintained, and replaced only by machines. Its power or pipe connection does not make it a habitable building. Stockyards, cable routes, pipes, landing pads, and equipment need usable interaction and collision geometry, even when their appearance is simple.

Use recipes with committed inputs, elapsed work, output capacity, and saved progress:

| Recipe | Inputs | Output | Initial active processing time |
|---|---|---|---|
| Smelt metal | 2 Iron Ore | 1 Metal | 6 simulation seconds |
| Assemble component | 2 Metal + 1 Copper Ore | 1 Component | 10 seconds |
| Fabricate medicine | 2 Biomass + 1 Component + 1 Water | 2 Medicine | 15 seconds |
| Build work robot | 20 Metal + 6 Components | 1 Work robot | 90 seconds |
| Build forestry bot | 24 Metal + 8 Components | 1 Forestry bot | 90 seconds |
| Build transport drone | 10 Metal + 4 Components | 1 Transport drone | 60 seconds |
| Make EVA suit | 6 Metal + 2 Components | 1 EVA suit | 30 seconds |
| Make toolkit | 2 Metal + 1 Component | 1 Civilian toolkit | 20 seconds |

The assembler can make suits and toolkits through selectable recipes. Equipment fabrication still uses machine controllers. A greenhouse produces up to 30 food/day with both botanist positions effectively staffed, consuming up to four biomass and 12 water/day. Crop growth continues between necessary tending visits, and these daily staffing/yield targets already assume a normal work/rest cycle. Missed tending, missing inputs, and inadequate utilities reduce yield; do not require botanists to stand at plants continuously throughout the night to reach the stated daily target. Scale inputs with actual growing output. Start personal consumption at two food and two water per person/day, then verify that a compact colony with three botanists and two greenhouses can support its first 12 residents after rest, travel, and staffing interruptions. Adjust rates if that acceptance case fails.

The initial material budget can support two solar arrays, one wind generator, one battery, one well, one air processor, one charging station, two habitats, a canteen, two greenhouses, a clinic, a lab, sanitation, a lounge, an airlock, a warehouse, a stockyard, a tank, a smelter, an assembler, two miners, 120 m of corridors, 400 m of cable, and 240 m of pipe. At the listed costs this uses **550 Metal, 334 Minerals, and 108 Components**. Treat this as a material check, then perform an actual power, access, hauling, and survival playtest. Do not assume a cost calculation proves the settlement is operational. Validate a feasible early water and industrial-supply route at the chosen site, including affordable imports where local availability is limited; do not require a visitor economy to obtain a missing bootstrap resource.

## 11. Pressurized access, utilities, and human construction

Maintain separate representations for walkable sealed connectivity, breathable-air supply, electrical distribution, and water distribution. A connected power cable does not provide a safe human walking route. Visual proximity does not count as a corridor connection.

Each habitable module is sealed independently, with corridor attachment ports and bulkheads. Corridors have actual traversable interiors and pressure state. The starting ship, arrival gangway, and airlock connect into this network. People travel through doors and corridors rather than through dome walls or unprotected terrain.

Supply and demand include population, enabled facilities, environmental conditions, and stored buffers. Power shortages reduce or stop affected equipment. Priorities should protect life support before optional attractions or manufacturing. Batteries consume and release stored energy, and split networks do not share power. Water comes from real connected sources and tanks. The air model can use understandable support capacity and reserve indicators instead of simulating individual gas species.

Give buildings a finite air reserve. Bulkheads isolate a damaged connection; disconnected occupied modules become an emergency with a readable time/buffer estimate. People evacuate automatically if a safe route exists. Do not instantly kill occupants at disconnection or let an isolated building share an unreachable air processor. Splitting and reconnecting networks must conserve their contents rather than duplicate buffers.

For ordinary movement before terraforming, colonists and visitors stay in the sealed network. To support the requested transition from robot construction to skilled human construction, implement **automatic EVA for eligible builders** as the design default:

- A builder collects a suitable suit and toolkit from an accessible facility and exits through a working airlock.
- Exterior civilian construction is accepted only when protection, environmental conditions, and a safe return route are adequate.
- Reserve enough suit support for the return trip. Retreat or suspend work when that reserve or the weather becomes unsafe.
- Keep this understandable through suit readiness and safe working duration; do not make the player manage separate chemical meters for each suit.
- Interior construction can proceed without EVA. Civilian work outside includes habitat shells and corridors. Industrial sites remain machine-only, regardless of the builder's skill or equipment.
- Suits are reusable owned equipment. They are returned or recharged through the appropriate facilities and persist with the worker if a save occurs mid-trip.

Technicians can supervise, train, and maintain eligible civilian facilities. They do not become human operators of outdoor industry. Once outdoor conditions become safe, eligible civilian travel and work can use appropriate outside routes, while the required corridor connections between buildings remain part of the colony's architecture.

## 12. Domed buildings and interior-view controls

Give enclosed buildings a coherent family of domed designs with curved panels/glazing, structural ribs, airlock/corridor ports, a floor, and real interior props. Distinguish functions by silhouette details, interior contents, and restrained accent colors. Avoid presenting the entire colony as anonymous boxes.

Separate the dome shell/panels, structural frame, interior contents, selection representation, and physical enclosure state. Support two visual modes:

- **Domes:** complete exterior shell and glazing.
- **Frames:** hide the shell panels so the frame, floor, furniture, equipment, and occupants are clearly visible from the overhead camera.

When the pointer hovers over a building, show a small, legible interior-view button anchored near that building. The pointer must be able to move onto and click it without the button disappearing. Include a tooltip and the same action in the properties window. Clicking toggles that building's view and does not issue a construction or movement command through the UI.

Add a prominent master Domes / Frames control in the colony toolbar. Applying it updates every current building, sets the default for subsequently constructed buildings, and clears existing individual overrides. A later individual toggle can override that one building until the next master action. Show a mixed state when useful so the behavior is understandable.

Changing view mode is purely visual. It cannot remove pressure, alter airtightness, expose people to weather, change navigation, grant line-of-travel through walls, or modify construction progress. Keep physical colliders and simulation boundaries valid. Selection should be able to reach visible occupants and interior objects in Frames mode, so adjust picking separately from physical collision.

Apply a consistent roof-hiding treatment to covered corridors where it improves interior visibility. Outdoor industrial equipment is unaffected by dome mode. Save the global setting and per-building overrides, and restore them correctly with the game.

## 13. Colonists, professions, needs, and staffing

Colonists are identifiable people with persistent properties, inventories, behavior, and life histories. Provide the following in each person's window:

- Name, portrait, background/age where useful, profession, skills and experience, and traits.
- Health, injuries/illness, hunger, thirst, fatigue, hygiene, recreation, morale, and a concise happiness breakdown.
- Current activity, its reason, relevant queued intention, movement destination, work eligibility, and any obstacle.
- Assigned bed/home, workplace participation, access to air and basic services, and suitable protection when outside.
- Inventory and equipment, carrying capacity, suit/tool condition, and reserved items.
- Status history such as arrival, training, injury, and recovery, plus focus-camera and relevant management actions.

Needs change over simulation time. People seek meals, drinks, sleep, hygiene, recreation, and treatment automatically through accessible facilities. Use practical thresholds and hysteresis so they do not continuously switch between almost-identical needs. Their trip and service time count toward the simulation. Inaccessible facilities do not satisfy needs remotely.

Professions influence actual capability and efficiency: builders construct; botanists grow food and manage gardens; medics treat; scientists research; technicians train/supervise and maintain eligible civilian systems; service workers run hospitality and recreation. Skills grow through work and training. A control/training center supports profession development without granting every person every capability instantly.

Let the player manage staffing positions, recruitment preferences, facility priorities, training capacity, and general work/rest policy. Avoid mandatory manual assignment of individual people to every building. Selecting a specific orbital colonist for a flight is allowed and distinct from directing their footsteps after arrival.

Health problems and extreme needs have consequences, including lost productivity, treatment demand, departure requests, and eventual death. Give warnings and time to act. Recovery and replacement recruitment remain possible if machines and infrastructure survive. A temporarily unhappy resident must not collapse the whole colony through an unexplained instant failure.

Provide a readiness summary before requesting a dropship: expected population after arrival, available beds, food/water runway, air support, power margin, and safe disembarkation. The player decides when to bring skilled builders and other personnel down. Do not automatically awaken or deliver the orbital roster as soon as the first habitat exists.

## 14. Properties windows, docking, and the colony interface

Implement a runtime window manager using the project's UI stack. These are player-facing windows in the built game, not Unity Editor inspectors.

Every meaningful selectable entity needs its own properties view: colonists, orbital colonists, mobile robots, drones, industrial machines, buildings, corridor segments, doors/airlocks where independently managed, resource deposits, trees/groves, storage, cargo pickups, dropships, landing facilities, utility connections, regions, and the orbital ship. Clicking terrain can open the relevant region/site information. Decorative particles and individual grass blades do not need artificial management panels.

Use a common inspectable contract and reusable views. Selecting an entity opens or raises its window, identified by world and entity ID. Other entity windows can stay open, allowing comparisons. Repeated selection raises the existing window instead of creating duplicates. Provide a clear pin/keep-open behavior if selection-driven replacement is also offered.

Common properties include name/type, status, location, condition, function, relevant inventory, active job or production, connections, supply/demand, priority, and a plain-language explanation of blockers. Specialize the contents:

- A miner shows deposit type, remaining reserves, extraction rate, output buffer, power, condition, service demand, and repair/replace/dismantle controls.
- A forestry bot shows its tree or clearing task, carried biomass, charge, capacity, condition, and waiting reason.
- A building shows occupants, staffing, access/pressure, utility demand, storage, output/services, condition, and interior-view mode.
- A construction site shows clearing, delivered and missing materials, work progress, assigned eligible labor, and connection prerequisites.
- A dropship shows its manifest, orbital/surface destination, flight state, ETA, pad reservation, and safe cancellation or return options.
- The orbital ship shows the actual sleeping roster, flight queue, recruitment arrivals, and available transport capacity.

Every normal properties window supports:

1. Dragging by its title bar and sensible focus/z-order.
2. Resizing from edges and corners with a usable minimum size and scrolling content.
3. Closing and reopening without deleting the underlying entity.
4. Docking to left, right, top, or bottom dock zones with a visible preview.
5. Shared tab groups when multiple windows occupy a dock, and splitters to resize dock regions.
6. Undocking by dragging its tab/title into the play area.
7. Remembered geometry and a Reset Window Layout command.

Persist the general layout in user settings and restore save-specific entity bindings by world/entity ID. If an entity no longer exists, close or clearly invalidate that window; never attach it to an unrelated object with a recycled scene ID. Clamp windows to the usable screen area after resolution or UI-scale changes.

UI consumes its entire pointer gesture and scroll input. Dragging a dock splitter must not pan the camera, a hover toggle must not select terrain beneath it, and a slider scroll must not tilt the world. Respect text-field focus, modal dialogs, and pointer capture. Support keyboard focus and readable contrast.

The colony HUD should include time/speed, credits, key stored resources, power/water/air status, population, orbital passengers, machine availability, reputation, alerts, build/harvest access, region map, and the master dome toggle. Provide fuller windows for population, orbital transfers, storage/production, utilities, research, trade, visitors, finances, terraforming, and neighboring regions. Allow the player to name/rename the colony in its overview. Keep routine views understandable; put detailed accounting in expandable sections.

Alerts are actionable: clicking focuses the entity or opens its window. Group repeated problems and prioritize immediate human danger over routine low stock. Include events such as full miner output, stranded bots, unreachable construction, insufficient beds for a flight, an isolated module, and visitor-service overload.

## 15. Production, trade, and finances

Production buildings use real recipes, inventories, power, condition, output targets, and machine controllers or the appropriate human profession. Outdoor industry always uses machines. Pause production when its output storage is full or its target is met. Preserve ingredients already committed to an active batch, and do not charge for them twice after an interruption or load.

Enable basic trade from the landed ship's communications from the beginning. Show buy/sell prices, quantity, available stock, cargo capacity, delivery time, and the resulting credit balance before confirmation. Goods arrive and depart on scheduled physical freight dropships using actual landing infrastructure. Distinguish freight, personnel, and visitor manifests while sharing coherent pad scheduling.

Use these initial buy prices per unit, with sale prices at approximately 60% of buy price:

| Good | Buy price in credits |
|---|---:|
| Iron Ore | 3 |
| Copper Ore | 4 |
| Minerals | 2 |
| Biomass | 2 |
| Metal | 8 |
| Components | 20 |
| Food | 5 |
| Medicine | 25 |
| Water | 1 |

Provide reasonable equipment and machine purchase options for recovery, priced above their raw inputs. Start freight capacity at 200 cargo units and nominal arrival time at three colony days. Charge purchases once, reserve exports from real inventories, load outgoing goods physically, and record refunds or failed orders explicitly. Do not pay for exports that are still sitting in unreserved storage. Prevent negative cargo, duplicate completion callbacks, and cancel/reload credit exploits.

Recruitment can start around 300 credits per colonist, modified modestly by skill, with an orbital arrival delay. The separate surface flight remains necessary. Initial cryosleep colonists are already part of the expedition and do not require that recruitment fee.

Maintain a transaction ledger separating imports, exports, transport, recruitment, visitor spending, and planetary program funding. Show recent cash flow and commitments. Avoid hidden recurring charges that make the machine-only opening insolvent before the player can establish income.

## 16. Visitors, services, and ongoing popularity

Implement an actual visitor economy, with people moving through the colony and using its capacity. Research and construct visitor reception, guest accommodation, food service, and an attraction. Reception must connect to a valid passenger landing facility through sealed access.

Allow the player to open/close visitor admissions, set a visitor cap, and choose simple pricing policies. Reserve available guest beds before accepting a group. Never create visitor demand with no way to admit the first visitors: an operational starting destination should receive a small baseline trickle even at modest reputation.

Initial visitor defaults:

- Groups of two to six, subject to capacity.
- Stays of two to four colony days.
- A personal budget around 200 credits.
- Lodging around ten credits/night, meals around six credits, and attraction visits around eight credits.
- Budget, standard, and premium price policies using roughly 0.75x, 1x, and 1.5x prices, with corresponding satisfaction expectations.

Visitors arrive by shuttle, check in, use assigned rooms, seek food and recreation, visit attractions, receive treatment when necessary, pay for actual services, and leave by shuttle. Maintain their inventory, needs, itinerary, wallet, and transaction history. They must not spend the same money twice or leave a ghost room reservation after departure or load.

Provide at least three meaningful destination offerings: the attraction dome's observation/exhibition service, the indoor botanical garden, and an outdoor scenic recreation area unlocked when conditions allow it. Add a buildable park/path designation for that final offering; it is an outdoor activity area rather than an unsealed habitable building. Guests cannot use it before safe access is established.

Track reputation on a 0 to 100 scale, initially around 20. A useful starting composition is visitor satisfaction 50%, resident wellbeing 20%, reliability/safety 20%, and attraction variety 10%, smoothed over time. Expose the contributing reasons. Higher reputation brings more interest, while actual beds, landing capacity, food, staffing, and utilities constrain admissions.

Popularity should create further management demands: busy arrivals require staff, meals consume food, guests use water and medical capacity, and worn services need maintenance. A successful colony should remain interesting to operate after its first stable period. Give milestone acknowledgments without ending the game or locking the player out of continued expansion.

## 17. Research and development

Implement an interactive research view with prerequisites, costs, progress, effects, and a selectable queue. Scientists working at a powered, accessible laboratory generate research; an initial target is ten points per colony day for two effectively staffed scientist positions. Saved partial progress and completed unlocks must remain consistent.

Use a compact initial tree with real effects:

| Research | Initial cost | Prerequisites | Effect |
|---|---:|---|---|
| Efficient Automation | 60 | None | Improves machine energy efficiency and job throughput |
| Advanced Agriculture | 60 | None | Improves greenhouse yield and water efficiency |
| Visitor Services | 100 | None | Reception, lodging, restaurant, attraction dome, indoor garden |
| Medical Development | 80 | None | Medicine fabrication and improved treatment |
| Regional Survey | 80 | None | Survey and acquisition of adjacent regions |
| Reliable Power | 120 | Efficient Automation | Geothermal generation and improved storage |
| Life-Support Efficiency | 100 | Advanced Agriculture | Improved air/water efficiency and buffers |
| Planetary Engineering | 200 | Reliable Power, Life-Support Efficiency, Medical Development | Terraforming facilities and program |
| Civic Development | 150 | Visitor Services | Improved hospitality capacity and advanced staff training |
| Sustainable Biosphere | 300 | Planetary Engineering, Advanced Agriculture | Outdoor crop/garden systems and improved restoration efficiency |

Research changes measurable behavior, not only a tooltip. Keep baseline robot fabrication, tool/suit fabrication, basic medicine imports, construction skills, and all opening survival facilities available without a research deadlock. Basic training is available before Civic Development; that research improves it.

## 18. Terraforming over 50 to 100 game years

Retain the nearly habitable planet premise and avoid a chemistry spreadsheet. Present three broad planetary indicators: **Atmosphere**, **Climate**, and **Soil & Water**. These are suitability scores, not literal percentages of gases or scientific measurements.

Derive starting conditions deterministically from the existing planet and survey data. Give each world two main environmental obstacles and a third condition that may already be relatively good. At least one factor must initially prevent ordinary unprotected human life outdoors. Preserve existing terrain and biome identity; do not regenerate the world to add this feature.

Each indicator explains a short set of visible consequences, such as exterior exposure, weather difficulty, water processing demand, or outdoor crop suitability. Environmental conditions influence early construction choices and utility requirements while remaining comprehensible.

After research, the atmosphere plant, climate array, and biosphere processor contribute to a coordinated planetary program. Require actual facilities, power, inputs, maintenance, and funding, initially around 2,000 credits per active program year in addition to material consumption. Display progress, current bottleneck, and a projected completion range based on recent uptime.

Balance a consistently supported program to complete its required improvements in roughly 50 to 100 game years from program initiation. One useful default is 1.4 percentage points of required work per program year for each fully supported branch, with a maximum effective rate of two percentage points/year after upgrades. A branch needing the full program then takes about 71 years at nominal support, and limited interruptions extend that duration. More plants improve reliability or reach the stated cap; mass construction must not trivially finish a century project in two years. Poorly supplied programs can take longer, which the UI should explain.

Keep program completion distinct from initial suitability. A branch already suitable can require less improvement, while at least one major branch determines the intended overall duration. Changes propagate to applicable regional conditions and visuals gradually.

Provide visible milestones: reduced exterior hazards, safer working periods, approved outdoor recreation, and productive outdoor agriculture when both atmosphere/climate and soil/water requirements are met. An outdoor field designation should consume seeds/feedstock, water where needed, and botanist work to produce food. These outdoor zones are not substitutes for sealed dwelling buildings. Keep industrial work restricted to machines after terraforming, and keep ordinary buildings connected by corridors.

The final stage opens additional opportunities and lowers certain costs. It does not end the game. Include a development-only late-game fixture or controlled time advance to validate the entire program without manually waiting decades; do not leave a debug shortcut in the ordinary player's HUD.

## 19. Maintenance, weather, and manageable disruptions

Give facilities and machines condition, wear, service requirements, and readable failures. Start with slow baseline wear and automatic maintenance requests below a configurable threshold, initially around 70% condition. Repairs consume actual parts and labor. A damaged miner pauses or loses output according to its state; replacement is an explicit paid construction operation.

Use a small set of coherent noncombat events: low solar output during storms, variable wind, equipment faults, greenhouse illness, ordinary medical demand, delayed freight, and visitor surges. They should interact with established systems and give preparation or recovery options. Provide an opening grace period against major random failures while the initial settlement is being built.

Events use simulation time and saved random state. Show warnings for developing hazards. Returning to a save should not reroll an incoming storm or repeatedly award event benefits. Severe outcomes need understandable causes; do not create arbitrary unavoidable disasters simply to force replacement purchases.

Machines and remaining infrastructure can support recovery after human losses. When no viable residents, orbital passengers, machine recovery, or recruitment route remains, provide a clear colony-failure screen with load/new-game choices. Otherwise allow continued recovery and rebuilding.

## 20. Expansion into neighboring regions

Implement functional neighboring-region expansion as part of this task. The initial owned region remains the current 6 km square with 36 terrain tiles. A colony map shows its borders, adjacent regions, survey information, cost, and acquisition state.

After Regional Survey research, allow acquisition of a directly adjacent region. Start at 2,500 credits for a first neighboring region, with a modest disclosed increase for later acquisitions. Acquire areas connected to the existing owned territory; do not silently teleport to an unrelated part of the planet. Predominantly ocean regions can be identified as unsuitable for this land-colony expansion rather than pretending all tiles are buildable.

Use the existing stable surface frame and global tile addresses. For a six-tile-wide region, adjacent regions extend the corresponding address ranges without moving the origin. The current `GenerateTile(address, cancellationToken)` numerical generation API is a starting point; extend ownership, bounds, data storage, and rendering appropriately rather than assuming the original fixed arrays already implement expansion.

Neighboring heights, rivers, coastlines, resource identities, and vegetation must agree at shared boundaries. Do not reseed an acquired region independently or add a fresh plateau that creates a cliff at the old edge. Preserve the world's broad opportunities for flat construction while respecting geographic variation and existing settlements.

Survey/generation gives visible progress, cancellation, and failure recovery. Charges and ownership changes commit once after a usable region is ready. A failed or canceled operation keeps the existing colony usable and does not charge twice on retry. Generation never reshapes the original colony or erases its depletions.

Allow construction and logistics into the acquired area. Networks, corridors, storage hubs, charging, and transport capacity must actually support distance. A mineral shipment between regions travels through the simulation and cannot instantly appear at its destination because its carrier is off camera.

Implement a bounded rendering/terrain working set as the map grows. Keep authoritative regional state and offscreen actors advancing consistently while loading only the detailed views needed near the camera and active interactions. Do not hold unlimited high-resolution terrain, colliders, and scene actors for every region indefinitely. Saving includes acquired regions, their generator versions, changed terrain, structures, depleted resources, and in-transit jobs.

## 21. Saving, loading, autosaving, and session lifecycle

Implement durable save files from the beginning of the simulation work, then extend the schema as systems are added. Saving must restore the actual colony, not regenerate a fresh version that merely resembles it.

Use `Application.persistentDataPath` for player save data and a versioned format with explicit DTOs and stable references. Keep saves out of tracked project assets. PlayerPrefs may store simple preferences, but is not the colony save system. Reference: [Unity persistent data path](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Application-persistentDataPath.html).

Provide named manual slots, a save/load list with colony name, date/time, game year, population and a small preview, overwrite/delete confirmation, quicksave, quickload, and rotating autosaves. Start with five rolling autosaves every five minutes of active real play time. Serialize writes so manual and automatic saves cannot overwrite each other's temporary files or publish older snapshots after newer ones.

Capture at least:

| Area | Persistent state |
|---|---|
| Identity and generation | Save schema/content versions, world ID, seed, generator versions and parameters, selected planet location, stable surface frame |
| Setup and arrival | Committed landing, current phase, initial deployment completion, relevant camera state |
| Regions | Owned/surveyed regions, tile addresses, resource identities, depletion/removal deltas, changed terrain |
| Structures and networks | Positions, rotations, definitions, construction progress, committed materials, condition, corridors, doors, utility connections and stored buffers |
| People | Orbital roster, cryosleep/flight/surface state, traits, professions, skills, needs, health, assignments, current intentions, personal inventory and equipment |
| Machines | Identity, type, position, charge, condition, tools, inventories, tasks and disabled/recovery state |
| Industry and logistics | Deposit binding/reserves, production inputs/output/progress, storage contents, reservations, cargo transfers, job progress |
| Space traffic | Personnel/freight/visitor manifests, flight IDs, preparation/transit/docking state, pad reservations, cancellation/return state |
| Economy and society | Credits, ledger, trade/recruitment orders, visitors and wallets, bed/service reservations, reputation and satisfaction history |
| Development | Research queue/progress/unlocks, terraforming program/indicators/rates/funding, environmental state |
| Time and events | Clock/calendar, requested speed, scheduled events, saved random state, maintenance progress |
| Presentation | Camera pose, selection, dome master mode and individual overrides, save-specific open-window bindings |

Use a consistent snapshot boundary. Apply accepted player commands, including commands entered while paused, before taking the snapshot. Snapshot authoritative data on the appropriate thread, then serialize/write immutable data without accessing Unity scene objects from a background worker. Preserve in-process transfers and construction/production input ownership without ambiguity.

Write to a temporary file, flush it, validate the completed output, and replace the destination safely while keeping a recoverable previous version. A canceled or failed write leaves the last valid save intact. Report disk/serialization errors in the UI. Corrupt or unsupported files must give a clear explanation and an available backup option; never silently replace them with an empty colony.

On load, validate the selected save and its references before discarding the current session. Reconstruct world data, deltas, structures, networks, inventories, people, machines, and flight state before enabling AI or completion callbacks. Rebuild paths from saved intentions and validate/reconstruct reservations. Release invalid claims safely without duplicating or deleting items. Resume from a paused state with the prior speed available when the player continues.

Do not apply offline hunger, decay, or travel merely because the player was away from the application. Use saved simulation time. Do not award a shipment twice, create the starter manifest again, replay the initial arrival over an existing colony, return arrived colonists to cryosleep, or put the same passenger in both orbit and a habitat.

If a save is requested during the initial cinematic, visibly queue it for the completed landing checkpoint and perform it once. Also create an initial post-landing autosave. For later flights, support saving and restoring their actual active phase, or a deterministic equivalent that preserves all timing, cargo, people, and pad commitments without skipping the journey. Saves during building work, harvesting, hauling, charging, civilian EVA, production, and region acquisition need explicit consistent handling.

Keep generator versions in the save. If an older generator cannot be reproduced, use a supported migration or stored terrain data, or explain incompatibility. Silently regenerating old developed land under a different algorithm is unacceptable.

## 22. Main menu, settings, pause, audio, and presentation

Retain Meridian's existing splash/main-menu art and visual identity. Wire the formerly inactive or partial controls to completed behavior:

- New Colony begins a fresh setup flow with immediate loading feedback and appropriate handling of an existing unsaved session.
- Main-menu Continue loads the most recent valid playable save. Keep this distinct from Continue on the planet-selection screen.
- Load Colony opens the save browser.
- Settings opens working preferences.
- Quit exits the built application cleanly, with save/discard/cancel choices when appropriate.

Provide a pause menu with resume, save, load, settings, return to main menu, and quit. Returning to menus releases the active world, jobs, workers, transient render resources, and callbacks correctly. Starting or loading a second colony in one application session must not inherit the previous colony's inventories, event handlers, agents, or windows.

Implement resolution/window mode, quality, audio levels, camera sensitivity, zoom/tilt sensitivity, optional edge scrolling, UI scale, and essential input rebinding or equivalent accessible alternatives using the existing input system. Apply and persist preferences. Offer confirmation/revert for a display change that might make the screen unusable. Keep settings controls out of the simulation data model except where they genuinely affect a chosen game rule.

Use consistent materials and a readable science-fiction visual style. Add basic construction, harvesting, machinery, footsteps, UI, warning, dropship, and ambient sound. Match sound to actual states and distance. Avoid a permanent wall of overlapping alarms. Particles and machine animations should communicate activity; lack of final bespoke art is not permission to leave functional structures visually indistinguishable.

Provide a short contextual opening guide: deploy machines, establish power and storage, secure resources, build connected habitats and life support, request the first personnel flight, and stabilize food/maintenance. Then guide toward research, additional staff, visitors, and expansion. Objectives observe real state and remain satisfied after loading. They can be dismissed, and the simulation does not depend on following a scripted order.

The first 12-person acceptance crew can consist of two builders, three botanists, one medic, two scientists, one technician, and three service workers, delivered on at least two flights. This is a playtest scenario, not a forced player selection.

## 23. Implementation sequence and engineering expectations

Use the following as internal checkpoints within this one assignment:

1. Inspect the baseline; establish shared definitions, stable IDs, simulation/session ownership, save schema, and the runtime window framework.
2. Complete initial landing, machine deployment, orbital population, personnel flights, and the safe session transition.
3. Implement inventories, reservations, harvesting, forestry bots, storage, construction, miners, utility equipment, and production.
4. Complete domed modules, corridor/airlock behavior, utilities, interior toggles, navigation, human needs, and skilled civilian construction with EVA.
5. Finish reusable properties windows, docking, global management panels, alerts, and the complete save round trip across those systems.
6. Add research, trade, recruitment, visitor economy, maintenance, environmental events, and terraforming.
7. Implement neighboring-region acquisition, travel/logistics across boundaries, and bounded rendering/streaming.
8. Finish menus, settings, audio, onboarding, balance, comprehensive integration validation, and a normal Windows build.

The order may change when dependencies warrant it, but do not stop after a checkpoint and ask for a new prompt for features already requested. Keep the implementation ledger current with completed systems, active work, unresolved dependencies, and the next concrete step. If the available Unity integration differs from a previous session, inspect and use the supported interface instead of fabricating calls.

Keep systems maintainable: data definitions for balance, narrow shared contracts, deterministic simulation where needed, a centralized save layer, and reusable inspection/window components. Avoid a single giant behavior owning every system or separate resource counters maintained independently by UI, AI, and storage. Use spatial indexing and bounded path/job work where appropriate.

Keep Unity API access on permitted threads. Existing surface workers perform numerical work; extend that pattern without touching scene objects or Unity terrain APIs from worker threads. Use cancellation and ownership checks when returning results to the main thread. Clean up native/Unity resources, subscriptions, and temporary workers on cancellation, scene changes, load failure, and shutdown.

Preserve useful existing tests and validation commands. Add tests for meaningful state contracts and concrete failure risks. Do not inflate the project with tests that simply restate a constant or check that a method exists. Keep temporary diagnostics and development fixtures separate from normal player startup.

## 24. Acceptance checks: prove that the systems work together

Use a reproducible seed and save fixtures, plus actual in-Editor or Windows-player observation. Record what ran, what passed, and any material limitation. A successful compile alone is insufficient.

### Start, construction, and population

- Start at the main menu, create/select a planet, load the same geographic surface, choose a valid site, and watch the cargo dropship land with underside booster effects.
- Repeat with skipping at different points. The exact starting machines, goods, infrastructure, and absence of surface colonists must be identical.
- Build a viable opening using only bots. Forestry bots clear a designated tree area and collect the resulting biomass. Work robots build and connect habitats, utilities, and production.
- Request two real personnel flights for a 12-person crew. Verify roster reservations, the six-person transport capacity, physical arrivals, correct orbital/surface counts, safe airlock/gangway access, and no duplicate arrivals after a load.
- After builders arrive, observe them constructing an eligible civilian structure without per-unit commands. Test their protected exterior route and return. Demonstrate that the same people cannot build, operate, service, or replace an industrial miner.
- Keep the first crew alive through several day/night cycles and a supply interruption, with actual food, water, air, power, staffing, and maintenance. Confirm that bootstrap resources and recovery options are sufficient in practice.

### Industry, inventories, and pressure

- Place and build a miner on a valid deposit. It runs with power and output room without a human or permanently assigned mobile operator. It stops for full output, loss of power, maintenance failure, or depletion with the correct explanation.
- Repair and replace a miner through real parts, labor, recovery, and new construction. Verify that the deposit reserves and stored output are not reset or duplicated.
- Check resource conservation through harvesting, hauling, personal pickup, construction delivery, production, cancel, demolition, trade, and save/load during a handoff.
- Disconnect power and pressure separately. Verify finite air reserves, isolation, warnings, automatic response, and safe reconnection. A power cable never substitutes for a corridor.
- Interrupt an agent with charge or personal needs while it owns cargo/reservations. Confirm eventual completion or safe release without permanent deadlock.

### Domes, windows, and controls

- Toggle one dome through its hover button, toggle it through its properties window, apply the master Frames control, create another building, apply Domes again, and verify the documented override behavior.
- Confirm visible interiors and occupants. Verify that toggling domes has no effect on pressure, pathfinding, protection, or construction state.
- Open a colonist, forestry bot, miner, building, and orbital-flight window simultaneously. Move, resize, close/reopen, dock, tab, split, and undock them.
- Save and restore that layout with individual dome overrides. Change resolution/UI scale and verify that windows remain reachable.
- Test pointer capture, scrolling, text entry, pause, placement, and camera controls. No world interaction should leak through window gestures.

### Economy, long-term play, and regions

- Produce and sell goods, receive a purchase, complete a research unlock, recruit into the orbital roster, and deliver the recruited person on a dropship.
- Admit actual visitors, observe their travel and service use, collect legitimate income, consume their food/water, and release their beds when they depart. Demonstrate reputation and capacity responding to service quality.
- Exercise a weather disruption, maintenance failure, and recovery. Pending event outcomes survive reload without rerolling.
- Use a development fixture to validate terraforming inputs, funding, rate caps, milestones, outdoor access, outdoor farming, and continued play after completion. State the measured/projected supported duration; do not claim a 50-year test was manually played if it was accelerated.
- Acquire a neighboring region, cross its boundary, build and transport resources there, then save/reload and unload/reload its visuals. Borders remain continuous, original structures stay fixed, and harvested resources do not return.

### Persistence and application lifecycle

- Save/load during a personnel flight, a freight transfer, a partly built dome, an active production batch, a forestry job, a civilian EVA trip, and a region-acquisition operation.
- Compare counts, inventories, reserved resources, credits, people, deposit reserves, jobs, research, program progress, and timestamps before and after reconstruction. Do not merely check that a save file exists.
- Test a corrupt save, unsupported version, canceled load, failed write path, and interrupted temporary write. Preserve the last good save and return to a usable UI.
- Start/load multiple colonies sequentially in one application run and verify that scene state, events, windows, and startup manifests do not leak between sessions.
- Re-run the relevant terrain/camera/loading checks. Preserve progress, safe cancellation, timeout recovery, deterministic tile identities, and matching borders.

Record representative generation, save/load, frame-time, and memory measurements in the actual test environment. Aim for smooth interaction at 1440p on the available test machine with a developed colony, and include a stress fixture around 200 residents, 100 visitors, and 150 mobile machines across multiple acquired regions. Profile concrete bottlenecks rather than making unmeasured performance claims. Tune simulation scheduling and the visible working set if that scale exposes avoidable stalls.

## 25. Completion and handoff

Update repository documentation with the playable controls, starting rules, main systems, editable balancing definitions, save location/schema policy, known material limitations, and validation results. Include clear progress records so another session can understand the code and continue safely.

Produce a normal Windows build that starts at Meridian's main menu and supports the complete loop: select a planet and landing site; land machines; build and maintain a connected domed colony; bring sleeping colonists down from orbit on repeated dropships; share eligible civilian construction with skilled colonists; automate industrial extraction and processing; grow food; research; trade; host visitors; terraform; expand; save; reload; and continue playing.

Use the repository's established Git process, include required Unity `.meta` files, and exclude generated caches, local saves, temporary diagnostics, and ordinary build output unless the repository explicitly tracks them. Commit coherent completed work and push through the established branch/PR policy when authorized by the project workflow. Do not force-push or overwrite unrelated user changes.

The final report should identify the implemented game loop, important controls, build location, meaningful validation performed, and any specific incomplete or blocked requirement. Distinguish actual observations from estimates. Do not call the assignment complete while a requested core system is a stub, a cosmetic demonstration, or disconnected from the simulation.

Begin by inspecting the project, then implement the complete scope above.
