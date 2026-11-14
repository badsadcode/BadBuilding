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
