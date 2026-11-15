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
