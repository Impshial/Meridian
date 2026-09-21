# Meridian colony play guide

This guide covers the playable colony systems. See FullGameValidation.md for measured validation and known limits.

## Starting a colony

New Colony generates a planet. Select land, continue to the 6 × 6 km survey, lock a valid dropship site, and choose Land Here. The 12-second arrival can be skipped. Its completion deploys six work robots, two forestry bots, four transport drones, the permanent cargo ship, and a connected passenger apron. All 48 colonists stay asleep in orbit.

Starting cargo is 600 Metal, 500 Minerals, 180 Components, 120 Biomass, 160 Iron Ore, 120 Copper Ore, 360 Food, 24 Medicine, 600 Water, 12 reusable EVA suits and eight toolkits. Starting credit is 20,000. The ship provides 30 kW, separate water storage, dry storage, and two machine charging positions.

## Controls

| Input | Action |
| --- | --- |
| WASD / arrows / middle drag | Pan relative to the view |
| Wheel | Tilt |
| Shift + wheel / + / − | Zoom |
| Q / E | Smooth accumulated 45° turns |
| Home / F | Reset / focus selected object |
| Left click | Select or designate current tool |
| Right click / Esc | Cancel tool; close a window; open pause menu |
| R / Shift+R | Rotate construction preview |
| B / H | Build browser / harvesting designation |
| Space / 1 / 2 / 3 | Pause / 1× / 2× / 4× time |
| F5 / F9 | Quicksave / confirm quickload |

The compressed calendar uses 120 simulation seconds/day and 12 days/year. Camera and interface controls remain available while paused. Text entry and modal dialogs capture shortcuts.

## Opening settlement

Use Build to designate foundations. Bots clear resources through the same finite harvesting system, haul real materials and construct the site. Red placement outlines explain water, slope, ownership, overlapping footprints or missing research. Separate cable, pipe and corridor tools connect building ports; a cable does not carry air or permit human travel.

Establish solar/wind power with a battery, a well and tank, an air processor, two connected habitats, a canteen and two greenhouses. Add a clinic, sanitation, lounge and laboratory, plus charging/storage and an airlock. The guide panel observes these actual milestones. A twelve-person opening with three botanists has passed a six-day survival/food test using real robot construction and the starting stock.

Orbit lists sleeping colonists. Choose up to six people per 100-credit transport, nominally one day. Beds are reserved on authorization; ships hold if the reserved accommodation loses safe access. Recruitment delivers to orbit first and requires a later surface flight.

Greenhouses use physical tending visits, piped water and delivered biomass. Their bounded, saved tending reserve allows crops to grow between visits. At default skill, 18 seconds of tending supports one botanist-day of growing activity; the reserve holds at most two botanist-days. A fully supported greenhouse produces up to 30 Food/day before research. People consume two Food and two Water/day. Keep harvesting biomass and replenish industrial inputs as the initial supplies run out.

Builders with suits/toolkits can contribute to civilian sites using an operational airlock. Industry remains machine-only after terraforming. The Overview policy can prefer builders for eligible work, allowing their current needs routine to finish before allocating the civilian task to them. Bots still handle industrial work and hauling.

## Inspection and management

Click objects to open independent inspectors. Windows can float, move, resize, dock at four edges, share dock tabs, resize dock splits, and return to floating. Layout and entity bindings belong to the saved colony. Frames reveals dome interiors and covered corridors; a local override affects only that structure. The master control resets local overrides. Roof visibility never changes air, collision, construction or route state.

Drag a title to move a window and an edge/corner to resize. Drag to a highlighted screen edge to dock, or drag a dock tab back into the play area to undock. Reopening an object raises its existing inspector. Open terrain has a region inspector; Regions includes an ownership map and adjacent acquisition controls.

A staffed training center changes professions through actual attendance. The initial balance is eight colony hours (40 simulation seconds) at the desk, halved by Civic Development. Travel and needs routines extend the elapsed duration; saved progress survives those breaks. This replaces the initial 24-hour attendance requirement, which proved impractically slow in an ordinary connected settlement. Worn EVA gear is serviced at an airlock with one component and five seconds of saved labor.

Inspect buildings for material delivery, condition, utility connections, actual storage and reservations, staffing capacity, production recipes and targets, and lifecycle operations. Inspect people for needs, skills, equipment and histories. Machines expose charge, condition, cargo and current work. Resource inspectors show persistent reserves and designations. Paused orders keep owned cargo; cancelled orders release claims while keeping physical goods recoverable.

Demolition recovers unconsumed inventory and 60% of incorporated construction cost as physical cargo. Replacement dismantles the old machine and creates a paid new construction order on the same deposit. It does not refill the deposit. Production commits inputs once and waits for output space when full.

## Development

Research spends points earned by scientists. Trade has finite 200-unit freight manifests, actual cargo handling, paid imports and departure-based export payment. Visitors need suitable beds, sealed access and staffed services, carry finite wallets, consume colony supplies and leave on transports. Reputation combines visitor satisfaction (50%), resident wellbeing (20%), safety (20%) and attraction variety (10%).

Power, water and sealed-air networks are separate. Air reserves and stored battery kWh are finite. Isolation disconnects routes and supply; reconnection preserves quantities. Maintenance requires material and labor. Weather and scheduled events use saved random state.

The coordinated planetary program uses actual power, material and water support plus 2,000 credits per active program year. Default branch work rates are 1.4/year with a hard cap of two; nominal longest branch is about 71 years when continuously supported. Outdoor access requires atmosphere/climate suitability of 80; outdoor farming additionally requires soil suitability of 80. Completion leaves the colony playable.

After Regional Survey, acquire directly adjacent 6 × 6 km regions for 2,500 credits initially, increasing 15% of the base price per additional owned region. Numerical generation preserves the original frame, plateau and resource identities. Payment and ownership commit only after generation succeeds. Detailed terrain rendering is bounded to 49 nearby tiles, with nearby resource batches and actor views; numerical state remains authoritative offscreen.

## Saving and configuration

Windows saves are in `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Meridian/Saves`. Named slots, quicksave and five rotating autosaves use schema 1 / content `meridian-game-1`, checksummed immutable snapshots, serialized writes, atomic publication and a previous-version `.bak` file. The first autosave follows completed landing; later autosaves occur every 300 seconds of active real play. Requests during the cinematic are queued.

Load validates the chosen snapshot and regenerates the recorded geographic frame before replacing the current session. It restores paused with no offline time advancement. Unsupported generators and corrupt saves report errors rather than replacing developed terrain. The load browser exposes backups. Pending regional survey generation is cancelled on reconstruction with no charge or new ownership; already committed regions remain owned.

Editable balance and building/recipe/research definitions are in `Assets/_Meridian/Resources/Colony/Catalog.json`. Runtime authority is under `Scripts/Colony/Simulation`, with separate persistence, presentation and UI components. Supplied FBX assets and maps are stored in `Art/Imported`; normalized prefabs/materials are in `Resources/Colony/Models`.
