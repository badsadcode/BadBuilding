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
