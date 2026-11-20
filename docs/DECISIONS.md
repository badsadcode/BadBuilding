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

## D27 — Farm buildings and outbuildings (Tom: "Farm buildings", 19 Nov 2026)

Half a million of Mazowsze's buildings (outbuilding 288,000, farm_auxiliary 262,000, plus barns, cowsheds, stables,
sties, greenhouses) were the generic style's flat boxes. Style `Farm`, five kinds (cookbook R072–R077):
- **outbuilding** (under 45 m², or by chance in between): one storey of 2.5 m, a mono-pitch roof (half), a low gable
  or flat one; a door (a wider double one on the bigger), a small window on six in ten;
- **barn** (stodoła): walls 3.6–4.8 m, gable 30–40°, wrota up to 4.2 × 3.8 m in vertical boards with a frame and
  braces on a long side (and the opposite one on half), a hay-loft door up in a gable; brick and block barns get
  boarded gables;
- **livestock** (cowshed, stable, sty; obora, chlewnia): low and long, a row of small windows high on the long
  walls every 2.6 m, one or two doors, one or two ventilation stacks on the ridge over 60 m²;
- **hall** (over 450–600 m²): ribbed metal on a 0.6 m concrete plinth, an 8–14° gable, one to three roller doors,
  a ribbon of high windows;
- **greenhouse**: milky panes on ribs every 1.2 m, walls and a 25–32° roof.
Types decide first (greenhouse; cowshed, stable, sty → livestock; barn), then size and shape (over 450 m² mostly
halls; long ones barns or livestock; small ones outbuildings). Skins: brick, grey block, whitewash, horizontal planks
(outbuildings), vertical boards (barns, nearly half); roofs eternit (most), green, red-brown, dark or rusty sheet.
Tests: type and size pick the kind; the barn's doors, the cowshed's high windows and vent, the mono-pitch roof, the
greenhouse; footprint bounds.
Looked at in Baltic Drive: Zielanówek (Tłuszcz), Serock.

## D28 — Garages, garage rows, allotment huts and shacks (Tom: "Yes, garages", 19 Nov 2026)

Style `Garages` (cookbook R011–R014; Tom's "garages, small shacks"), for the types garage, garages, shed, hut and
allotment_house:
- **garage**: one box, a mono-pitch (most), low gable or flat roof, a 2.4 m up-and-over door on the side facing
  the road; skins: PRL sheet metal (blaszak: green, grey, blue, rust-brown), brick or plaster;
- **garage row** (garages over 30 m², or a long garage over 45 m²): a door per ~3.1 m bay along the side facing the
  access road, each door its own colour, brick piers between them, a fascia, a tar-paper roof falling to the back;
- **allotment hut**: painted planks, boards or plaster (green, blue, brown, cream), a gable (most) or mono-pitch
  roof, a door and a framed window, a porch on two posts on half;
- **shack** (sheds and huts, six in ten): planks or sheet with patches of other sheet and planks nailed over,
  a mono-pitch roof of rusty sheet, a door, maybe a window.
Tests: a garage row's door per bay toward the road in more than one colour; a single garage's wide door; an
allotment hut's door and window; footprint bounds for every kind.
Looked at in Baltic Drive: garage rows at Ksawerów, the allotment gardens by Górnicza (Praga-Południe).

## D29 — Fire stations, schools and kindergartens (Tom: "Fire stations, schools", 19 Nov 2026)

Style `Civic` (cookbook R044, R049), for the types fire_station, school, kindergarten (Baltic Drive also gives an
untyped building with amenity=fire_station or amenity=school these types, its D72):
- **fire station** (remiza OSP; 1,470 in Mazowsze with the amenity ones): storeys of 3.9 m (the ground floor holds
  the engines), a level floor (5 cm plinth); on the side toward the road one to three vehicle doors 3.4 × 3.5 m every
  4.6 m (red, sometimes white), a staff door beside them, a red sign band over the doors; windows upstairs; on six in
  ten of those over 120 m² a 3 m square hose-drying tower inside the footprint at the end away from the doors, 4–6 m
  over the roof, with slit windows and a pyramid cap (the type itself is the evidence for the tower);
- **school**: two or three storeys of 3.6 m on a 0.6 m raised ground floor, a classroom window per 3.6 m bay (up to
  2.6 m wide, two mullions and a transom), the main entrance up steps under a wide canopy with a glazed light over the
  door; the PRL "tysiąclatka" look (grey or white plaster, brick plinth) or renovated (pale walls, one accent colour on
  the bands, parapet and entrance), mostly flat roofs with a parapet;
- **kindergarten**: one or two storeys, smaller windows set lower, cheerful walls and frames, an accent colour.
No gym hall is invented where the footprint doesn't show one.
Tests: the fire station's tall doors toward the road, sign, level floor, and hose towers inside the footprint; the
school's levels, raised entrance toward the street and windows; the kindergarten's height and accent.
Looked at in Baltic Drive: the fire station on Limanowskiego in Grodzisk Mazowiecki, a school in Osiedle Jantar.

## D30 — Contemporary blocks of flats (Tom: "Yes, apartments", 19 Nov 2026)

Warsaw's new estates ("deweloperka", cookbook R025–R026) were drawn as PRL slabs. Style `Apartments`:
- rendered walls (white, light grey, warm white, beige, greige) and a second finish on some: wood-look boards on every
  fourth bay, graphite panels on every fourth bay, a graphite top floor, or a brick-slip ground floor;
- big windows (up to 1.8 m, a mullion), low ones with a glass guard (a French balcony) on the top floor;
- balconies standing out 1.4 m with frosted glass railings on a metal top rail on every second or third bay (every
  bay on a few), or loggias cut 1.3 m into the wall (four in ten);
- on the street side, on a little over half, a glazed ground floor of shop and service units (shop_entrance markers);
- entrances under canopies, one per ~28 m of the main wall; a slab edge over the ground floor; a flat roof behind a
  0.6 m parapet with a metal coping, a lift overrun and a ventilation box.
**Which blocks:** a mapped year decides (from 1992 contemporary, before PRL). Without one (most), the shape: odds of
0.3, +0.25 for a footprint deeper than 14.5 m (−0.1 under 12.5 m), +0.2 for an outline that isn't nearly a
rectangle, +0.1 for more than ten corners, +0.25 for six to nine storeys, −0.12 for four, five, ten or eleven
(the PRL standards), clamped to 0.05–0.9 and rolled once per building. Walled-in undated buildings stay with the
tenement and block styles; listed ones never qualify.
Tests: the year decides; undated deep seven-storey blocks are mostly contemporary and 12 m five-storey slabs mostly
PRL; railings, entrances and footprint bounds over five seeds. The PRL test block is now dated (1975).
Looked at in Baltic Drive: the new estates by Szamoty (Ursus/Piastów).

## D31 — Socialist realism, and party walls that need a neighbour behind them (Tom: "Bad building time", 20 Nov 2026)

Style `SocRealism` (cookbook R019, S09; MDM, Plac Konstytucji, Marszałkowska), tuned on Tom's Street View photos of
Marszałkowska 34 and 49 and Plac Konstytucji 5:
- one even grey sandstone (grey, pale, warm grey, weathered); the courtyard side plain render;
- a rusticated base of two storeys on blocks of six or more (one below): shops with big glass between stone piers,
  doors under stone lintels; on some long fronts an **arcade** 3 m deep behind **paired columns with capitals**,
  steps up to it, the shop fronts at the back, closed ends;
- above the base **windows in pairs** with a stone surround round the pair, a sill and a cap (heavier on the first
  floor over the base); **small stone balconies with balustrades** every third bay on two rows; colossal pilasters
  every second bay on some; relief panels on others;
- string courses over the base and under the top floor, a deep cornice, and an attic **balustrade with pedestals**.
**Evidence**, in order: an architecture tag; a mapped year 1949–1956; **stone or sandstone as the mapped material**
(new request fact `Material`, D23's vocabulary growing when a family needs it: MDM's pieces are tagged sandstone),
which also lets pieces in the air take the style (no base, shops or arcade up there); otherwise a block of five to
nine storeys, over 900 m², facing an avenue (a street of half width 5.5 m or more: dual carriageways come as two
ways), a perimeter block or a long frontage: always when listed (MDM is listed and mostly undated), else by a roll
shared by the district (the caller's "district" seed), so a quarter goes one way. A listed undated building under
seven storeys and 1,500 m² stays the kamienica's.
**Fixes found on Plac Konstytucji** (they apply to every style):
- storeys never overrule a part's mapped span (a 24–28 m piece tagged with the whole block's 9 levels stacked 21 m
  of wall);
- parts of a building make party walls by height **overlap** (a piece on top of another isn't blinded by it);
- **a party wall needs a neighbour on its outside** (checked 0.8 m out at three points along it) and not also on its
  inside (footprints that overlap on the map are behind nothing); and a wall of 20 m or more against a separate
  neighbour under 60 % of its height is a facade above it (a shop pavilion along a block's front), while short sides
  keep their firewalls. Neighbour rings carry a separate building's height (`Ring.Top`).
Tests: a 1952 block is socialist realism; undated avenue blocks often, never on a narrow street; a rich front and a
plain courtyard; footprint bounds; a top piece keeps its span and isn't blinded; a low pavilion leaves a facade, a
tall neighbour a firewall.
The blank wall left on the east of Plac Konstytucji was the game's doing (an inner piece drawn too); fixed there, Baltic Drive D78.

## D32 — Brutalism, and the "brutalist" preset (Tom: "Brutalism next", 20 Nov 2026)

Style `Brutalism` (cookbook S12–S14) with five facade grammars chosen per building: **fins** (deep vertical fins
between bays), **bands** (heavy spandrel bands over ribbon windows), **waffle** (an egg-crate grid, each window at the
back of its own box), **pilotis** (the ground floor glazed and set 2.5 m back behind columns, so the mass above reads
as cantilevered while staying inside the footprint), **towers** (blank stair and lift shafts at the ends of the main
wall, rising 3.5 m over the roof). Always: board-marked concrete (role `concrete_board`, `fin`), windows 0.5 m deep,
blank concrete ends on walls under 6.5 m, a 1.2 m fascia, a recessed entrance under a heavy canopy, a plant room.
Five concrete greys. Function is kept: it only dresses offices, civic buildings and (under the preset) flats.
**Evidence:** an architecture tag; a listed concrete civic building of 1962–1985 (a monument of its time); a dated
concrete civic or office building of 1962–1985 (by the district's roll, 55 %); an undated concrete one over 600 m²
and three storeys (30 %). In Mazowsze's data this is rare (one listed 1970s office near Warsaw's centre).
**Preset "brutalist"** (`BuildingRequest.Preset`, new; cookbook part D, P05 "Brutalist Warsaw"): the alternate city.
Offices, civic buildings and blocks of flats over 250 m² (untyped ones over 400 m²) take the style, except: listed
buildings, anything mapped before 1945 or since 1990, towers over 60 m (the glass skyline stays), stone, sandstone
and brick buildings (MDM and the old city were there first), and walled-in street rows. Houses, farms, garages and
churches never change.
Tests: the tag; seventies concrete civic often, flats never without the preset; the preset turns a block brutalist
but not a listed one or a house; all five grammars over forty seeds within a metre of the footprint.
Looked at in Baltic Drive with the preset: Ksawerów's office blocks (bands and blank ends), Plac Konstytucji (MDM
untouched).

## D33 — Interiors on demand, first archetype: the staircase block (Tom: "Interiors", 20 Nov 2026)
Milestone 3 starts with the commonest Warsaw building, the block of flats with staircases (klatkowiec), for the
`prl_block` and `apartments` styles. Two new pieces, both in `Interior/`:
* `Shell.Of(building)`: what an interior needs, small enough for a game to keep one for every block near the player
  (outline, storeys, datum, entrance doors, and every opening the facade made, read back from the infill quads that
  `Kit.WallWithOpening` puts at the back of each hole). Exteriors are generated first and far more often, so the
  window handshake runs this way round: the interior takes the facade's windows where they are.
* `InteriorGenerator.Generate(shell)`: the plan and its geometry. The wall with the most doors sets the frame. The
  outline is sampled across that wall; runs where its front stays on one line and it is 8.5–20 m deep become
  sections (a stepped slab gives several; steps and slanted ends are left solid). Each door on a section's front
  gets a staircase repeated on every storey: a landing at the door, a dog-leg stair (two flights of about 8 steps,
  a spine wall between them, a half landing at the back), a rail on the top floor. Two flats per landing, reached
  from the landing only: a hall with a short corridor, bathroom and kitchen on the door side, living room and
  bedrooms on the far side; the left flat also takes the room behind the staircase. Deep blocks (over 7.5 m behind
  the staircase) get a corridor along the back rooms and storage between, the hall running straight through.
  Flats narrower than 3.5 m stay sealed voids; doors with no staircase behind them stay shut (D11's fallback).
* Game scale (D11): doors 1.1 × 2.15 m, halls 1.5 m, stairs 1.33 m wide, ceilings at storey height minus 0.2 m.
* Output (`InteriorModel`): one mesh by material role (the staircase's oil-painted lamperia and whitewash, terrazzo,
  plaster, two wallpapers, kitchen paint, tiles, parquet, lino; colours seeded per building; open door leaves),
  physics boxes (walls and window glass solid, flights as ramps through the step nosings, a step and a ramp up to
  each street door from the lowest ground, jambs beside it), rooms with stable ids
  (`<building>/interior/s<storey>/c<staircase>/<L|R>/<kind><n>`), doorways, room markers, and a report:
  every room reachable from the street door (doorways and stairs), no flat through another.
* Layouts are invented, believable, never the real buildings' plans (D11). Not yet: kamienica, corridor blocks, point
  towers with flats all round the core, shops on the ground floor, balconies you can step onto, furniture, loot.
Tests: staircases and flats on every storey, reachability, windows following the facade, rooms inside the outline,
same shell same interior, L-blocks planned in their arm, stepped slabs in sections, storage in deep slabs. The CLI
`interior` command writes a plan as JSON (`InteriorModel.PlanJson`).

## D34 — Roomier interiors (Tom: "very cramped", 20 Nov 2026)
After walking in (Baltic Drive D81): the staircase is 3.8 m wide (flights 1.75 m, was 1.33), with a 2 m landing, a
2.7 m run and a 1.9 m half landing where the block is deep enough (they shrink back toward the old 1.3/2.3/1.3 m
only when fewer than 3.4 m of rooms would be left behind them). Halls 2.1 m (was 1.5), corridors 1.9 m (1.4),
bathrooms 2.5 m (1.9), bedrooms at least 3.2 m (2.6) and about 5 m (4.2), doors 1.3 × 2.3 m (1.1 × 2.15; bathroom and
storage doors 1.1 m), flats under 4.6 m left out (3.5). Deep blocks keep 5.5 m back rooms. Fewer, bigger rooms, as
D11 wanted. Window glass is a little more visible (it read as an open hole). Ceilings stay at the facade's storey
height.

## D35 — Balconies you can step onto, and openings without cracks (Tom, 20 Nov 2026)
Tom's dumps from inside: "balconies are not balconies", "gaps near the windows and the balcony".
* The PRL block and contemporary flats give their balcony doors their own infill role, `balcony_door` (glass that
  opens), and leave a `balcony` marker at each: the door's middle on the facade at the floor, out, and
  `Marker.Size` = (width along the wall, depth out; 0 for a ground-floor door out to the garden). Balcony markers stay
  out of the Noor export (no such kind in the spec; to propose). The contemporary French door lost its middle mullion.
* The shell carries the balconies; the interior makes their doors doorways (no pane, open in the physics), gives each
  balcony a slab and railings 1.15 m high for physics (the exterior draws it), and lists them (`InteriorModel.Balconies`).
* Openings: the interior's holes sit 1.2 cm inside the exterior's and their reveals run out to the facade line, in
  front of the exterior's own. They stopped 0.21 m in before, and the contemporary blocks' reveals end at 0.15 m: a
  crack round every window and balcony door.
* Not yet: loggias (their glass is 1.3 m in, behind the interior's outer wall, which stays blind there).

## D36 — Loggias you can step into (Tom: "make the loggias work too", 20 Nov 2026)
* The contemporary flats' loggias leave a `loggia` marker (the middle of the mouth on the facade at the floor, out,
  Size = width and 1.3 m depth); their glass back wall is `balcony_door` (it opens) and lost its middle mullion. Kept
  out of the Noor export, like balconies.
* The interior steps its outer wall back round each loggia: the room's wall is the glass at the loggia's back
  (passable), with the room's paint above it to the ceiling; the recess's sides get faces towards the rooms beside it;
  room walls that would cross the loggia's open air stop at its back; the loggia gets a concrete floor over the room's,
  and physics: its floor, a front rail 1.15 m high, and side walls. Loggias are listed with the balconies (`Loggia`).
* Also: the jambs of windows and doors in the outer walls faced into the wall; they face into the opening now.
* Not yet: a loggia that falls in front of a staircase opens off the stairwell.

## D37 — No loggias in front of staircases; easier doorsteps; narrow blocks keep their flats (Tom, 20 Nov 2026)
* Contemporary flats: no loggia or balcony door in a bay that a staircase stands behind (within 2.1 m of a door on
  that wall, on every storey); the stairwell gets ordinary windows, as the PRL block already did. At Skaryszew: 0 of
  33 loggias in front of the staircase (one opened off the stairwell before).
* The way up to each street door: the landing is 2.6 m wide with ramps down its front and both sides (coming at the
  door at an angle you met the landing's side, up to 0.9 m high); the jambs beside the door are on the wall line only
  (0.12 m, were 0.6 m and caught you).
* The roomy sizes of D34 had left narrow blocks with no flats at all (a 13 m point block: 9 rooms, all stairwell): a
  section now takes the tight staircase (2.9 m) and a 1.6 m hall when the roomy one would leave a flat beside it
  narrower than 4.6 m but the tight one wouldn't. That block has its 63 rooms back.

## D38 — NoorFengShui: furniture placed the way people place it (Tom and Noor: "we need NoorFengShui", 20 Nov 2026)
`Interior/NoorFengShui.cs`, run at the end of `InteriorGenerator.Generate` (switch: `InteriorGenerator.Furnish`).
* Programmes by room kind: bedroom (a double bed if the room is 3 m wide, else a single; a bedside table each side;
  a wardrobe; a desk with its chair over 10 m²; a bookshelf over 14 m²), living room (sofa, the TV unit on the wall it
  faces, a coffee table between, bookshelf, armchair, a dining table with chairs from 18 m²), kitchen (a counter along
  the longest free wall with the sink under a window and the cooker away from it, wall cupboards not over windows, the
  fridge at its end, a table with chairs from 7 m²), bathroom (a bath, or a shower if it won't fit; toilet; basin with
  a mirror cabinet; washing machine), hall (shoe cabinet, coat rack), storage (shelves).
* Rules: against a wall, back to it; never in a doorway, the swing before it, a balcony door or before a loggia's glass;
  tall pieces never before a window, low ones only under a sill at least their height; a bed's head never under a
  window. After each piece the floor is flooded at walking width (0.1 m cells, 0.3 m half-width from the wall gap):
  every door must reach every other and the front of every piece you use, else the piece goes elsewhere or not at all.
* Built from boxes in seeded colours (`f_wood`, `f_fabric`, `f_kitchen`...), one physics box per piece, and a
  `loot_<kind>` marker where things could be kept (drawer, wardrobe, fridge, cupboard, medicine, shelf).
* The interior records its outer openings per room (`InteriorModel.Apertures`); openings that only clip a room's edge
  block that wall but are not its doors (one was, with its front outside the room, and the kitchen stayed empty).
* Placeholder looks: the boxes are for swapping to real models by kind later (Synty or hand-made), Tom's call.
Test: furniture in at least 80 % of living rooms, bedrooms, kitchens and bathrooms; every piece inside its room and
clear of the others; no bed under a window; loot markers. A 15-storey block plans in ~0.3 s on a worker.

## D39 — Furniture shapes kept apart, so a game can swap in models (Tom, 20 Nov 2026)
Tom: "I want to be able to swap the assets with any model". Each `FurnitureItem` now carries its own generated shape
(`Shape`, building space, by material role) instead of adding it to the interior's mesh, and says whether it stands
against a wall (`AgainstWall`). A game draws the shape for the kinds it has no model for and places its own model on
the footprint for the rest; the physics boxes and loot markers stay the library's.

## D40 — Podlasie's wooden houses: the "podlasie" preset (Baltic Drive's second region; Tom: "Wooden villages first", 20 Nov)
Podlaskie's villages are the region's signature (the Land of Open Shutters: Soce, Trześcianka, Puchły; Czyże).
From photographs (Wikimedia Commons) and the Wikipedia article on the decoration of wooden houses in Podlachia:
* **What it is:** with `Preset = "podlasie"` the house family's cottage becomes the Podlasie wooden house:
  - walls of horizontal boards (`timber`) in honey varnish, ox-blood, brown, ochre, weathered grey, now and then blue,
    green or yellow;
  - the gable boarded in a chevron (battens parallel to the rakes, a white strip up the middle), above a little skirt
    roof, with white barge boards cut into teeth under the rakes; the attic window kept;
  - white window surrounds with a carved crown (nadokiennik: a board with a peak and two small ones), panelled
    shutters held open (a white board with two panels in blue, sky, green, brown or ochre), white corner boards;
  - the door on a long side (the gable faces the street), often under a porch (ganek) with posts and its own little
    boarded gable; roofs in red-brown, grey eternit, dark, green or red sheet.
* **Commoner cottages:** in Podlasie long one-storey footprints are cottages 78 % of the time (else modern or kostka),
  squarer small ones 55 %; a mapped year before 1970 is a cottage there (1945 elsewhere). Anywhere, a footprint
  mapped `building:material=wood` that isn't two storeys is a cottage.
* **Within a metre:** every piece (skirt, porch roof, shutters) stays within 1 m of the walls, as for every detail.
* **Cost:** about 1,700–2,400 triangles per house (a plain cottage ~550, a modern house ~650).
* `godot/test/shoot.gd`: photographs exported GLBs in a row (loads them at run time, hides the "-colonly" mass).
Tests: most podlasie houses are cottages; no podlasie dress without the preset; details within a metre and the door
on a long side (140 pass). Gallery samples `synthetic_podlasie` (10 seeds). Tom (20 Nov): "this looks amazing... good enough"; the look
stands. Possible later: glazed verandas, fences and benches.

## D41 — The Orthodox church (cerkiew), only on the map's word (Tom: "Orthodox churches next", 20 Nov)
A new style, `orthodox_church`, for a church, chapel or "religious" building whose OSM `denomination` contains
"orthodox" **and** whose `religion` is christian or unmapped (`denomination=orthodox` also tags Orthodox Judaism:
a synagogue in the data says `religion=jewish`, and stays a synagogue). No evidence, no onion domes (Tom's rule for
Podlaskie: onion domes only with OSM evidence). From photographs of Podlaskie's cerkwie (Wikimedia Commons):
* the body on the mapped outline under a gabled roof along its length (wings for a cross-shaped plan, a flat roof for
  an outline too ragged for one); the door in the most west-facing wall (churches face east), with steps;
* a raised octagonal drum over the nave with a band of small windows, a cornice and an onion dome (a 12-sided lathe);
  a small drum and onion over the sanctuary at the east end; at the west end a square tower over the entrance,
  rising past the ridge with a belfry, under a tented spire or a small onion on a little drum;
* the eight-pointed Orthodox cross, gilded, on every dome and spire (three bars, the lowest slanted);
* wooden (`timber`) when mapped as wood, else 75 % in Podlasie (the preset) and 35 % elsewhere, never when big
  (> 450 m²): painted blue (most often), sky blue, pale blue-grey, green, white, or honey wood, white corner boards
  and white surrounds peaked over house-sized windows; masonry: yellow brick, white, cream or pink plaster with tall
  windows; roofs silver-grey, green, blue or dark; domes like the roof, gilded, blue or dark.
* `BuildingRequest.Religion` and `.Denomination` (and the recipe now records Material, Denomination and Preset).
About 2,300–2,900 triangles. Tests: three orthodox denominations are cerkwie; no denomination or roman_catholic is
not; an orthodox synagogue is not; onion and cross above the ridge, within a metre, the door at the west; mostly
wooden in Podlasie; deterministic (151 pass). Not yet: churches mapped in parts (towers and domes as
building:part, like Warsaw's cathedral) keep the old meshes; Catholic churches; the Tatar mosques.

## D42 — Mosques: Podlasie's Tatar mosques, and modern ones (Tom: "Tatar mosques", 20 Nov)
A `mosque` style for a building the map calls a mosque: `religion=muslim` on a mosque, place of worship or untyped
building, or `building=mosque` with no religion said (Warsaw's Wilanów mosque is tagged only that); not an office
(`office=religion`) or a tomb. From photographs of the two Tatar mosques that stand (Wikimedia Commons):
* **long** (Kruszyniany's kind, a footprint at least 1.25 times as long as wide): vertical boards painted green (or
  grey-green), a gabled roof; at the front end two tall square towers on the corners with arched windows, a white
  cornice and small dark cupolas; an arched (barrel) porch at the door; a small cupola on the ridge behind the
  middle; pointed arches over the side windows;
* **square** (Bohoniki's kind): horizontal boards in plain wood (or weathered grey, or green), a hipped roof, an
  octagonal lantern in the middle with small windows and a dark dome, a little gabled porch with posts;
* **modern** (not historic, not mapped as wood, not small in Podlasie): white, cream or sand plaster, a dome on a
  drum over the hall (green, gilded, white or blue), and on a mosque over 200 m² a slim minaret at the back corner
  with a balcony and a pointed cap;
* every dome and cupola has a gilded finial: a spike, two balls and a crescent opening upward.
Tatar when historic (heritage), mapped as wood, or under 400 m² in Podlasie. The front: the street end for the long
kind, the street side for the others; the door in the middle bay of that wall, windows beside it.
1,600–3,500 triangles. Tests: the two kinds by footprint, the map's word (building=mosque counts; a church, an office
or an untyped building doesn't), finials, within a metre, a minaret on a big modern one (157 pass).
The Orthodox style's drum and pyramid helpers are now shared (internal).
