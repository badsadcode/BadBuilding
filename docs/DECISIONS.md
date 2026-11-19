# BadBuilding — decisions

Newest last. Each: what was decided, and why.

## D1 — An engine-agnostic .NET 8 library, Y up, metres, +X east, +Z south
The core has no dependencies beyond the base library and `System.Numerics`, so Baltic Drive (Godot 4, .NET 8), a
future NoorMD bench or any other tool can use it. .NET 8 rather than BadShip's 10 because Godot 4's C# targets 8.
Plan coordinates are metres with +X east and +Z south (north is −Z), Y up: glTF's right-handed axes, and the same as
Baltic Drive's world, so footprints go in without flipping.

## D2 — Lifted from BadShip, not referenced
`Rng` (xoshiro256** via SplitMix64, per-stage streams), `Mesh`, `Aabb`, the validation report and the hand-written GLB
writer are copied from BadShip 0.31 (`../BadShip`, same author) and adapted; BadShip itself is not referenced, so both
projects keep moving independently (Noor: "BadShip and BadLevel continue independently"). The GLB writer is generic
over a plain model of modules, markers and materials.

## D3 — Every attribute says where it came from
A building's levels, height, roof, style and palette carry `osm` (mapped), `request` (given by the caller) or
`inferred` (chosen by the generator). Most of Mazowsze's buildings have no mapped roof, material or colour; the
output must never pretend an inferred look is real-world information (Noor §3).

## D4 — Facade exposure from context, with tolerances
Each outline edge is classified before any facade is built: **party** (a neighbouring footprint runs along it within
0.6 m for at least half its length — never a single near-equal coordinate), **sliver** (a neighbour faces it within 4 m),
**street** (a street line lies within 25 m in front of it), **courtyard** (an edge of a hole), else **side**. Party walls
get no openings; slivers get no windows or balconies. Missing context degrades to "side", which is always safe.

## D5 — The ground sets a datum
Ground heights may be given per outline vertex. The ground floor sits at the highest of them plus the style's plinth;
the plinth and walls reach down to the lowest, so on a slope the building stands on an exposed base instead of
floating or sinking. Without heights the ground is flat at 0.

## D6 — Reject loudly, fall back safely
Footprints are cleaned (duplicate and collinear points, orientation), then checked: under 3 points, under 4 m² area, a
self-intersecting outline, or a part thinner than a room are reported in the validation report. A footprint that cannot
carry the requested style falls back to the plain style; one that cannot carry anything yields an empty building with
an error, never geometry outside the outline (Noor §6).

## D7 — Every courtyard gets a way in
Tom and Noor caught it on the first gallery: a courtyard block with no way into its yard ("residents must arrive by
helicopter"). Holes of 40 m² and more are courtyards; smaller ones are light wells and are left alone. For each
courtyard, three straight rays are cast in from every open outer wall (not party or sliver), and the passage goes where
all three reach the same courtyard wall first, nearly square to it, within 25 m. Street walls score three times higher,
short passages and wall middles score better. A courtyard of 150 m² or more under a storey tall enough gets a **gate
passage** (brama przejazdowa: 3 m wide, up to 3.6 m clear, two gate leaves standing open); otherwise a 1.8 m
**pedestrian passage**. The passage is a real opening: the plinth and ground-storey walls are cut, it has side walls,
a ceiling and paving, markers at both ends, and the collision mass (LOD2) is carved so you can walk or drive through.
Every courtyard also gets a staircase door on its longest wall, and doors keep clear of passages. Validation checks
access (Noor's rule): a courtyard with only a staircase door is a warning (`courtyard.sealed`), one with neither is an
error (`courtyard.unreachable`). Noor's third case, a footprint already open on one side, is not a hole and needs
nothing.

## D8 — Noor's answers of 14 November 2026 (docs/NOOR_SAGE.md, Q1–Q5)
- **Gates (Q1):** the seed decides, half open and half closed; a closed gate has a framed wicket door (furtka) in one
  leaf. Closed is a *state*, not a wall: the passage, its markers and the carved collision stay, the gate module says
  `state: closed` and the wicket has its own marker, so a courtyard behind a closed gate is still reachable and a game
  can open and close gates later (Q1 option c, survival yards).
- **Yard doors (Q2):** a street staircase whose back wall is a courtyard wall (within 16 m, roughly parallel) gets a
  through door into the yard, in the yard wall's own bay; every courtyard still has at least one door. Not every
  courtyard-facing stairwell gets one automatically.
- **Courtyard or light well (Q4):** 40 m² stays the default, with context: a mapped entrance onto a hole makes it a
  courtyard at any size, and a hole narrower than 4 m (the short side of the rectangle with its area and perimeter) is
  a shaft for light and air, however big.
- **Next style (Q3):** kamienica, street facade against yard facade, gate passages, shop ground floors, varied
  cornices and roofs, bays first, and *not* every tenement equally ornate: controlled variation within the family,
  proportions and composition before ornament.
- **Licence (Q5):** none for now; MIT is Noor's preference if Tom releases it. Before choosing, check the terms of what
  was lifted from BadShip and BadLevel (both Tom's).
- **Two kinds of success:** *technical validity* (meshes, footprints, openings, reachable spaces, LODs, performance)
  and *architectural plausibility* (no stair to nowhere, no impossible entrances, no windows into a neighbour). Both are
  checked; the second by architectural rules in validation and by visual review in the gallery and NOOR_SAGE.md.

## D9 — The kamienica style, first cut
Built from Noor's Q3 brief (D8), proportions and composition before ornament. A building with a party wall, 2–8
storeys and not a house becomes a kamienica (before the PRL check: blocks of flats stand free, tenements stand in a
frontage). 3.5 m storeys on a 0.5 m stone plinth, one window axis per 3.1 m bay. Street walls: tall windows (raised on
the ground floor), surrounds, a string course over the ground floor, a cornice and a 0.9 m attic; shop fronts on the
ground floor of about 70 % of them (display window with a door, a sign board, one in five shuttered); a tall street door
when there is no brama. Yard and side walls: plain plaster, smaller windows, service doors, staircase windows between
floors. Party walls: bare brick firewalls rising past the roof, with chimney stacks. Variation inside the family:
seven plaster palettes (one sooty, never renovated) and three ornament levels from the seed (plain, modest, rich with a
header over the first-floor windows, a balcony with an iron railing and a deep two-step cornice). Kamienica yards of
60 m² or more get a gate passage (150 m² for PRL blocks). Roofs are flat behind the attic for now (Q6 to Noor).
Found on the way: a neighbour merely in line with a wall (the next house's front, continuing the frontage) counted as
a narrow gap in front of it; a gap now only counts when the neighbour is really in front.

## D10 — Noor's answers on the kamienica (Q6–Q10), and evidence before confidence
Noor asked for these to be read as guidance, not a backlog; the main priority stays a reliable generator for real OSM
footprints. Two of them are about exactly that, so they are built now:
- **Evidence (Q8):** a party wall and 2–8 storeys say "town building", not "pre-war tenement". The request now carries
  what OSM knows: `start_date`, `heritage`, `building:architecture`. Mapped after 1945 → not a kamienica. Mapped before
  1945, listed, or with a historic architecture tag → the full family, rich ones included. Nothing mapped → still the
  kamienica family (the street-and-yard logic holds for any frontage house) but never rich: plain or modest only, and
  marked "no age evidence". Missing data is never taken as evidence of age.
- **Mapped shops first (Q9):** shop=* nodes (and shop-like amenities: pharmacy, café, bank, kantor…) and a shop tag on
  the building are passed in; when there are mapped shops, the shop fronts go in the bays near them; only without shop
  data does the 70 % guess apply. A shuttered shop is still a shop, with its own marker.
- Recorded for later, not built: roofs (Q6: mono-pitch to the yard hidden behind the attic first, an occasional
  mansard with dormers on rich ones, towers and domes only on chosen buildings, never at random; roofs must respect
  courtyards, odd footprints and the neighbours' roof lines); an arched brama (Q7: a real geometric opening with
  collision clearance, chosen by style and age rather than ornament alone); shop frequency by street character and
  shop types (Q9); optional firewall decals, bare by default, sometimes old adverts, never fake adverts on identifiable
  real buildings (Q10).
- Watch (Noor): windows, doors and bays must leave room for believable floor plans; regular window spacing is not
  worth an impossible plan.

## D11 — Interiors at game scale, invented, inside the real footprint (Tom, 15 Nov 2026)
Interiors (milestone 3) use a game-scale profile: doors at least 1.1–1.2 m wide and 2.2 m high, hallways 1.5 m, stairs
1.3 m, ceilings 2.7 m, sized from the game's character (a 1.8 m capsule, 0.4 m radius, plus room for a third-person
camera), with a fit test in validation from the street door to every room. A realistic profile stays available. The
footprint never grows: the extra space comes out of the rooms (fewer, roomier ones), and only buildings without a
mapped height may grow a little taller. Layouts are invented, believable but not the real buildings' plans (Tom: "less
rooms and of course inaccurate"). To be put to Noor with the scale numbers.
Fallback (Tom, 15 Nov): fill every building as fully as possible, but where a part cannot get a believable interior,
seal it as games do: a door that stays locked for good, with nothing behind it, marked as such in the metadata so a
game knows not to send the player there. Balcony kind (projecting, loggia or a mix) is a believable choice per
building, not a copy of the real one.

## D12 — Noor's guidance on the family plan (15 Nov 2026, friendly chat via Tom)
- **After roofs: single-family houses**, including houses extended over time (an older core, a newer extension with
  its own roof height, a garage added later) and houses that respect the terrain (walkout basement, raised entrance,
  stepped foundation). Then warehouses and industrial buildings.
- **Game scale (D11) approved.** Also test corners, turning through doors, stairs and landings, not only straight
  corridors; keep character-collision checks and third-person camera-clearance checks separate. Locked doors only as a
  last resort, and navigation and gameplay must know the space is closed.
- **BadStructures order:** beam bridges, then footbridges (stairs, ramps, railings, accessibility), then underpasses
  and pedestrian tunnels; later brick railway viaducts, steel trusses, concrete arches. Principle: *a bridge is
  defined by what it connects and what it must avoid*: supports respect clearance, terrain and what passes beneath,
  never placed at fixed intervals in a road or on a track.
- **Brutalist Warsaw:** a real vocabulary, not a concrete texture: monolithic residential, civic (overhangs,
  cantilevers, monumental stairs), industrial (exposed frames, ventilation towers), futurist (elevated passages, fins,
  projections); varied concrete (board-formed, precast panels, smooth poured, staining, exposed aggregate). First
  gallery test: a PRL slab, a point tower, an office and a small house in the same alternate Warsaw. Style never
  changes function: a brutalist house still works as a house. (One inappropriate golden dome reserved, for science.)
- Order stays: roofs, bugs, then the rest.

## D13 — Roofs (Noor Q6)
`Generation/Roofs.cs`. Gabled, hipped and pyramidal roofs sit on the footprint's minimum-area rectangle (rotating
calipers over the hull) when the footprint fills at least 86 % of it and has no courtyard: eaves at the top of the
walls, the planes running 0.25–0.45 m past them (dropping with the slope) and seen from below as a soffit, gable walls
closing a gabled roof's ends, hips rising at the same slope as the long sides. Anything less rectangular falls back to
flat and says so (`roof.complex_footprint`); L-shaped and extended houses (Noor) are the next step, as wings with
their own roofs. The mono-pitched roof (skillion) is a sloped surface over any footprint, from a height function: a
kamienica's falls from its main street front to the yard (about 7°, at most 2.2 m), the street attic rises 0.3 m
above its high edge so it stays hidden, yard walls rise to meet it and firewalls follow it 0.6 m higher, chimneys
on top. The roof is chosen in the plan, before the storeys: a mapped roof:shape wins (gambrel and mansard drawn
gabled, half-hipped hipped; domes and the rest flat, reported: the Chairwoman's embargo still stands), otherwise the
style decides (PRL flat; kamienica skillion; plain: houses gabled or hipped, barns and sheds gabled, an unnamed
building of house size taken for a house, the rest flat). The rise is roof:height when mapped, else from the style's
pitch (houses 33–42°, barns and utility 20–28°); a mapped height includes the roof, so the walls get the rest.
Pitched roofs get tile, slate or sheet colours of their own.

## D14 — Exports follow Noor Interchange v0.1 (Tom, 15 Nov 2026)

Tom asked for BadBuilding's exports to follow Noor Interchange v0.1 (NoorEngine `docs/interchange/NOOR_INTERCHANGE.md`,
draft of 15 November 2026), without renaming the project yet.
- **Manifest:** every export also writes `<name>.noor.json`: id, kind `building`, generator, recipe (file, seed,
  sha256), created, space, bounds (asset space), files with hashes, triangles per LOD, collision `mesh`, marker
  counts, tags (style, roof shape, OSM type) and provenance (osm, request, inferred, D3). It validates against the
  schema; a copy lives in `docs/interchange/noor.asset.schema.json` and the tests check every field against it.
  BadBuilding's own extras sit under `badbuilding` (the building id, the pivot, the validation status).
  `validation.gltf_validator` is `not_run`: the Khronos validator isn't wired in yet.
- **Identity:** the recipe now starts with `id` (a UUID), `seed` and `version`. The id is the request's `AssetId` if
  given, else the one in a recipe already saved under that name (regenerating keeps it), else a name-based UUID (v5)
  from the building id, so the same OSM building gets the same id on any machine without a stored file.
- **Pivot:** the spec puts a building's origin at the bottom centre of its footprint with the ground at y = 0. The
  GLBs are shifted by the pivot (the footprint's box centre at the lowest ground under the walls); the in-memory
  `Building` and `metadata.json` stay in building (plan) space, so Baltic Drive, which uses the meshes directly, is
  unchanged.
- **extras.noor:** every node's extras now hold only `{"noor": {"v": 1, ...}}`. The root has the asset id, kind and lod;
  parts have `part` from the spec's vocabulary (wall and passage → wall, roof → roof, entrances, gates and wickets →
  door, balconies → railing, trim, joints and roof rooms → detail), their module id, and the old extras (type, tier,
  parent, lods, exposure, gate state...) under `props`.
- **Markers:** the spec has no new marker types without a spec change, so all of BadBuilding's markers become
  `entrance` with a `kind`: street doors `main`, courtyard doors `service`, shop doors `main` (tag `shop`), the two ends
  of a gate passage and the wicket `gate` (tags `passage`, `wicket`), the roof access `hatch` (tag `roof_access`). The
  old kind stays in `props.badbuilding_kind`. Node names are `Marker_Entrance_<local id>`.
- **Collision:** LOD0 now also carries the bare mass as `<mass id>-col` with `noor.collision = "mesh"`. LOD2 is
  unchanged (the mass as a drawn mesh).
- **Kept as before:** the three GLB files, recipe, metadata and validation JSON, byte-identical GLBs for the same
  request. Readable JSON escaping now ("+Y", not an escaped plus).

**Open, for Noor and Tom:**
1. The spec wants LODs as sibling nodes `<base>_LOD0..n` in one GLB; BadBuilding still writes three files (listed in
   the manifest as role `other`). Folding them into one GLB is a follow-up.
2. In Godot, a `-col` suffix keeps the mesh visible and adds collision (`-colonly` drops the mesh), so the mass would
   be drawn over the facades. The spec says "-col, not rendered". The gallery hides these nodes by
   `extras.noor.collision`; an engine importer should do the same, or the spec could switch to `-colonly`.

## D15 — Noor Interchange v0.2 (Sage of NoorEngine's answers, via Tom, 15 Nov 2026)

The three open points of D14 went to the NoorEngine session; the answers became Noor Interchange v0.2 (NoorEngine
commit bb92bc8, spec section 8). BadBuilding now writes `"noor_interchange": "0.2"`:
- **Collision:** `extras.noor.collision` is the rule (importers never render such a node). The name hint is now
  Godot's collision-only suffix: `<mass id>-colonly`. Checked: Godot's importer makes it a static body with no
  visible mesh.
- **LODs:** three files stay, now the canonical form: the manifest lists `.lod1.glb` and `.lod2.glb` with role `lod`
  and their `lod` number (the schema requires it).
- **Markers:** `entrance` kinds `main` (street door), `courtyard` (yard door), `shop`, `wicket` (the small door in a
  closed gate), `roof` (roof access). The two ends of a passage through the ground floor are `passage` markers, each
  naming the other in `pair`, kind `gate` (brama) or `corridor` (a walk-through), with `width` and `height` (clear
  height). `props.badbuilding_kind` stays as extra information. No `door` markers yet: the spec wants them only for
  modelled, openable leaves (the gate wings would qualify once they are separate nodes that can swing).
- **Id namespace:** `6f1d6a52-2c1e-4b8e-9a43-5b0d2e7c9f10`, fixed for BadBuilding and written in the README and the
  manifest (`badbuilding.id_namespace`).
- **Tool block:** `badbuilding` (becomes `noorarchitect` after the rename).
- **Still open:** wire in the Khronos glTF validator (`gltf_validator` is `not_run`) before NoorEngine's ADR-023 is
  accepted.
The schema copy in `docs/interchange/` is v0.2. The tests' small schema checker learned `allOf` and `if/then`.

## D16 — A generic Godot importer for Noor Interchange (Tom, 15 Nov 2026)

`godot/addons/noor_interchange/noor_post_import.gd`, an `EditorScenePostImport` script with no BadBuilding-only
logic, so any Noor generator's GLBs can use it. It lives here for now, beside the first exporter and its test
samples; it can move to NoorEngine or a shared addon repository later without changes.
- **Collision:** any node with `extras.noor.collision` becomes a hidden StaticBody3D with a shape from its mesh
  (trimesh, convex, or a fitted box, sphere or capsule), and every visual under it is removed. The extras flag is the
  rule; the name suffixes (`-colonly`, `-convcolonly`, v0.1 `-col`) are only a fallback.
- **Markers:** every `extras.noor.marker` node becomes a Marker3D. Not only the types Tom listed: any type in the
  spec's table, since readers must not drop data. It gets metadata `noor` and `noor_marker`, and persistent groups
  `noor_markers` and `noor_marker_<type>`.
- **Identity:** the manifest beside the GLB gives the root `noor_asset_id`, `noor_kind`, `noor_name` and so on.
  `<name>.lodN.glb` reads `<name>.noor.json` and records `noor_lod`. Without a manifest, an `id` on a top-level
  node is used.
- **Names stay readable** (spec 3.6): a replaced node takes its predecessor's name. The first test run caught
  Godot's "@Marker3D@123" auto-names.
- **Test:** `tools/godot_import_test.py` imports a BadBuilding courtyard block (with its LOD1), a hand-made generic
  glTF covering every case, and that glTF without a manifest, headlessly through the real Godot importer, then checks
  them (332 checks with Godot 4.7.2). Not part of `dotnet test`, because it needs a Godot executable.
Baltic Drive is unaffected: it builds buildings in memory and imports no GLBs.

## D17 — The industrial style: factories and warehouses in three eras (Tom, 18 Nov 2026)

Tom's order for the missing types: industrial and warehouses, then offices and skyscrapers, buildings in parts,
socialist realism; and added brutalism (for future projects as well), garages, small shacks and cottages. For
factories he picked "mixed by age".

- **Who:** OSM types industrial, warehouse, manufacture, factory, works, hangar, depot take the style, small sheds
  too (the type says what it is).
- **Era:** the mapped start year (before 1945 brick, to 1990 concrete, after metal); without one, from the size and
  the seed: over 4,000 m² mostly metal sheds; 800–4,000 m² a mix; small ones lean concrete and brick.
- **Metal** (logistics park): ribbed cladding (surface role `cladding`) over a 0.8 m concrete plinth, a high ribbon of
  windows on 60 %, flat or shallow gabled roofs (8–13°), a thin edge.
- **Concrete** (PRL): grey panels with a column every 6 m, a high ribbon of windows, flat or saw-tooth roofs.
- **Brick** (pre-war): role `brick`, a 4.5 m bay grid of pilasters, a tall window per bay and storey, a cornice band,
  gabled (18–25°), flat or saw-tooth roofs; small works can have two or three storeys; half of those over 700 m²
  have a brick chimney 14–26 m above the roof, tapered in three stages, standing inside the footprint.
- **Docks** (concrete and metal): on the longest open wall that doesn't face the street, a run of roller doors (role
  `roller`, 3 m wide, 3.6 m high) with a canopy over them.
- **Saw-tooth roofs:** teeth across the hall every ~9 m, a sloped solid face and a vertical glazed one, on nearly
  rectangular halls only.
- **Export within the spec (Noor Interchange v0.2):** loading docks go out as `entrance` kind `courtyard`
  (`props.badbuilding_kind = loading_dock`); the chimney's top marker isn't exported. Two new kinds proposed to Noor
  (NOOR_SAGE.md Q12) instead of exporting things as what they aren't.
- Slight departures from "nothing outside the footprint": pilasters and columns stand 0.18 m proud, dock canopies 2.2
  m out (as entrance canopies already did).
Tests: eras by start year, the types that take the style, docks away from the street, chimneys inside the footprint,
export vocabulary, footprint bounds. In Baltic Drive (D63) the game's building shader draws the three new surface
roles as ribbed metal, brick courses and roller-door slats.

## D18 — The office style: curtain walls, ribbons and grids; towers with masts (Tom: "Yes, offices", 18 Nov 2026)

Second on Tom's list after industrial (D17).

- **Who:** OSM type `office`; `commercial` from 13 m (or 4 levels); untyped (`yes`) from 60 m (or 18 levels), which
  is how most Warsaw towers are mapped. Never when the start year is before 1945 or the building is listed: that is
  a kamienica's evidence.
- **Facade by era:** before 1990 a concrete grid (2.4 m bays, deep reveals) or ribbon windows; 1990–2001 ribbons or
  curtain glass; from 2002 curtain glass. Without a year, by height: 60 m and over mostly curtain (8 in 10), lower
  ones a mix. The curtain is one surface (role `curtain`): the panels, mullions and floor bands are the renderer's
  job (a pattern, not geometry), which keeps a 200 m tower at a few hundred triangles.
- **Ground floor:** street walls get a glazed lobby up to 5 m with mullions, doors with canopies and entrance markers.
  Party and sliver walls stay blank.
- **Roof:** flat, a parapet; towers a crown 2.5–6 m high (curtain or the second wall colour); one to four plant
  boxes; from 140 m a mast 15–40 m with a `beacon` marker at the top (not exported: no such kind in the spec).
- Storey 3.6 m.
Tests: who is an office, era → facade, a lobby door toward the street and plant on the roof, a mast on a 180 m tower.

## D19 — Parts in the air: `MinHeight` (18 Nov 2026)

Warsaw's towers are mapped as stacks of `building:part`s (Varso: 11 parts up to 274 m), many starting high up
(`min_height`). A request now carries `MinHeight`; a request with one (over 5 cm) is **raised**:
- its floor (`Datum`) is the ground's highest point plus `MinHeight`, no plinth, no passages, no doors or lobby;
- the walls run from there to the top, and a soffit slab (role `soffit`, or `wall`) closes the underside.
Tests: a part from 193 to 240 m stays above 192 m everywhere, has a soffit and no entrance.

## D20 — Parts of one building only make party walls where they cover each other (18 Nov 2026)

Seen in Baltic Drive: a 71 m office tower was blank on every side, because a 10 m podium part and an 81 m part on top
of it were counted as neighbours abutting it, and an abutted wall is a party wall from ground to roof. A neighbour
`Ring` can now say it is **a part of the same building** and how high it stands (`PartMin`, `PartTop`); such a part
only makes a party wall when it covers this one's height (its top within 4 m of ours and its bottom within 4 m of
ours). So a podium no longer blinds the tower over it, and a part on top no longer blinds the one below. The walls
of the tower below the podium's roof are hidden inside the podium. Separate buildings (no heights given) work as
before: a lower neighbour still leaves a bare firewall above it, as in any Warsaw street.
Test: a tower with a 10 m podium part on the east is glazed there; with a separate neighbour it is a party wall.

## D21 — Noor's cookbook as the reference catalogue; reviewed against the code and the map (19 Nov 2026)

Tom brought Noor's architectural cookbook (`docs/COOKBOOK.md`, 78 building recipes, 25 styles, shared massing, roof
and facade ingredients) as a guide for what's missing, with "a few different variations for each building" and the
current level of detail kept. Sage's review is in `docs/COOKBOOK_REVIEW.md`: every entry marked implemented, partial,
missing, deferred or landmark-only from the source and tests, and weighed against a count of the real map
(2.59 M buildings in Mazowsze). Findings that change the order of work:
- only 1,457 buildings have a `start_date` and 31 a `building:architecture`: style has to come from type, size,
  shape and context; the year is a bonus;
- 30 % of houses are L- or T-shaped and get a flat roof, because pitched roofs need a nearly rectangular footprint;
- Baltic Drive folds OSM types into eleven kinds, so schools, garages, greenhouses, fire stations and offices can't
  be told apart in the library yet.
The cookbook is a catalogue, not a backlog: families are taken from it one at a time, each with variations, tests
and a status update in the review.

## D22 — Game first, the general library after (Tom, 19 Nov 2026)

BadBuilding stays what it was meant to be: procedural buildings from a footprint, for many projects, with many types
and styles in the end. The order of work is set by Baltic Drive: the types and styles its map needs, Mazowsze first,
then what the other Baltic countries bring (they have styles Mazowsze doesn't). When the game is done, more general
types and styles for other projects. Not every style exists in Mazowsze, and none is built just to tick off the
cookbook. What keeps the library general meanwhile: types and styles chosen from the request's facts (no game
code in the library), regional choices kept in one place so a region preset can change them later (cookbook
part D), and the Noor Interchange export.

## D23 — Not tied to OSM (Tom, 19 Nov 2026)

OSM is the first source, not the only one. Today the request speaks a slightly OSM-flavoured language (`Type` takes
OSM `building=*` values, `Roof` OSM `roof:shape` values, `FromOsm` marks provenance). The rule from here on: the
request is BadBuilding's own vocabulary, and each data source gets an adapter that translates into it, on the
caller's side (Baltic Drive's map adapter is the OSM one). Other likely sources: national registries (Poland's
BDOT10k and EGiB, Estonia's EHR, Latvia's and Lithuania's cadastres), Overture Maps, hand authoring in an editor.
Some of them carry exactly what OSM lacks (build years, use codes, storeys, materials). The vocabulary grows when
a family needs a fact: new request fields or new values with a written meaning, never a raw tag dictionary the
styles read directly. OSM values stay accepted as aliases, so nothing breaks. Not done yet: renaming
`FromOsm` to a source name and documenting the type and roof values as BadBuilding's own list. Both happen with the
next change that touches the request (carrying the real type through, cookbook review step 1).

## D24 — Parts that sit on parts, roofs that fit the mapped height (Tom's dumps, 19 Nov 2026)

- **A part under another part has a flat roof.** When a part of the same building starts within 1 m of this one's
  top and covers at least half of it, this roof is that part's floor: flat, whatever the map says (`roof.capped` in
  the report). On Fangora a tower stood on a base mapped with a hipped roof, and floated over the hip with a gap.
- **A pitched roof never rises past the mapped height.** Where the mapped span leaves less than 2.4 m of wall under
  the roof (a "roof-only" part from 25 to 32 m with a 7 m roof), the roof is lowered to fit; under 0.5 m it goes flat.
  And the storeys now fill the walls exactly (before, a storey rounded up could lift the roof 0.6 m past the top).
- **No eaves on parts.** A part with other parts around it gets no eaves overhang (`BuildingPlan.Eaves` = 0): its
  eaves poked through a neighbouring part's wall as a red stripe on Al. Jerozolimskie.
- **No kamienica high on a tower.** A raised part never takes the kamienica style: its brick firewall tops, cut to
  the slope of its roof, stuck through the walls of the parts above (the Błękitny Wieżowiec on Plac Bankowy).
Tests: a capped base gets a flat roof; a roof-only part stays under its top with no eaves; a raised part is never a
kamienica.
- **Fewer, smaller chimney stacks on firewalls** (addendum, same day): a kamienica's party wall only carries stacks
  when it is a real tenement's firewall (3+ storeys, 6 m+ long), one per ~10 m, 0.8 × 0.6 m. Two-storey buildings on
  Daniłowiczowska had a row of big brick boxes on every short wall.

## D25 — Pitched roofs on L-, T- and U-shaped footprints, in wings (19 Nov 2026)

The cookbook review (D21) found 30 % of Mazowsze's houses got a flat roof: pitched roofs needed a nearly rectangular
footprint. Now a rectilinear footprint is split into **wings**:
- laid on its own grid (the minimum rectangle's axes, cut at every corner, corners within 0.4 m merged); it counts as
  rectilinear when the grid's inside cells match its area within 6 %;
- covered by at most four maximal rectangles (greedy: the biggest holding a still uncovered cell); none narrower
  than 2 m, else flat as before;
- the **widest** wing is the main roof (the tallest ridge); a wing crossing it is cut back to the main ridge, and that
  end runs into the main roof (no hip, no overhang, its gable hidden under the main slopes), so the two meet in
  valleys; the other ends are outer ends, hipped or gabled as the roof is;
- one pitch for the whole roof: each wing's rise is the main rise scaled by its width (a mapped roof height sets the
  main rise), so narrower wings have lower ridges; the walls stop at the eaves as before.
A pyramidal roof over wings is drawn hipped. Slanted corners, courtyards and more than four wings stay flat.
`Roofs.Pitched(mesh, …, plan, eave, overhang)` draws whatever the plan holds; Plain, PRL and industrial use it.
Tests: L and T houses, gabled and hipped, roofed in two wings within the footprint plus the eaves and under the
mapped height; the stem of a T meets the bar without a hip and with a lower ridge; a slanted corner still goes flat.
Looked at in Baltic Drive: the houses of Kępa Gocławska (Marokańska, Libijska), L- and T-shaped, now with hips,
gables and valleys.

## D26 — The house family (Tom: "Yes, houses", 19 Nov 2026)

Houses are most of Mazowsze (742,000 typed, and most of the 1.1 M untyped buildings of house size). One style,
`House`, five families (cookbook R001–R006), each with variations:
- **cottage** (chałupa): one storey, gable roof 40–48°, small windows with a cross frame; whitewashed (white, cream,
  grey or blue wash) with painted trims (blue, sky, green) on three in four, open shutters on some; or dark
  weathered **timber planks** on a third, white trims sometimes; gables always **boarded**; eternit, old clay, rusty
  or dark roofs; a chimney on the ridge. Tom picked all three traditional details.
- **kostka**: nearly square, two storeys on a 1 m plinth with basement windows, a low hipped roof (12–20°) or a flat
  one, a band between the floors, the door up steps under a canopy, a balcony on six in ten. Tom's "real-life mix":
  55 % original dash plaster (grey, beige, dun), 45 % pastel insulation.
- **modern**: one or two storeys, gable, hip or flat roof at 30–40°, anthracite (or white, brown) frames, a mullion
  instead of a cross, floor-length windows downstairs on the garden side, a garage door on the street on some; white,
  light grey, beige, or the warmer colours Polish houses come in (sand, light yellow, salmon, pale green, ochre).
- **semi-detached** and **terraced**: the kostka (45 %) or modern look on a unit with one or two shared walls; those
  stay blind and the door goes elsewhere.
**Choice:** the type (`terrace`, `semidetached_house`) or the number of party walls first; a mapped year (before 1945
cottage, to 1990 kostka, after modern); else the footprint: near-square, 60–170 m² and not one storey → kostka or
modern; elongated, under 130 m² and not two storeys → mostly cottage; one storey under 140 m² → cottage or modern;
otherwise mostly modern. Untyped or "residential" buildings of house size (35–220 m², ≤ 2 storeys, ≤ 10.5 m) with
no party wall are houses too; a walled-in one is still a town house's job.
**Attics:** a house's mapped levels count the attic under a pitched roof (Polish mapping: building:levels=3 for two
floors and rooms in the roof), so from three mapped levels the walls hold one fewer; every outer gable tall enough
gets an attic window.
Details stay within a metre of the walls (canopies, steps, balconies, sills, shutters); everything else inside the
footprint. Tests: the year picks the family; cottage, kostka and semi-detached signatures; footprint bounds over five
seeds; variety over twenty seeds; three levels under a gable are two floors and an attic.
Looked at in Baltic Drive: Kępa Gocławska, Grądy (Błonie), Serock.
