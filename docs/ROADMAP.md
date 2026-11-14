# BadBuilding — roadmap

Where the two recon notes (`RECON_SAGE.md`, `RECON_NOOR.md`, 14 November 2026) land. Baltic Drive is the proving
ground: recognisable, efficient Mazowsze buildings first, interiors on demand later. Not NoorArchitect, not a
universal architecture engine; library boundaries stay provisional (Noor, "Scope and ownership").

## Milestone 1 — exteriors that belong on the street (first cut done, 14 November 2026)

The first prototype takes from Noor's notes everything that decides whether a *plain exterior* is believable:

- [x] Repo, .NET 8 library with no dependencies, CLI, tests; deterministic seed streams and stage rerolls lifted from
      BadShip (D1, D2).
- [x] Input: footprint rings (with holes), levels, height, type, roof, mapped entrances, house number, seed; and
      **context**: neighbouring footprints, street lines, ground heights round the outline (Noor §1, §2).
- [x] Every attribute carries its source, `osm` or `inferred` (Noor §3).
- [x] **Facade exposure** per wall edge: street, courtyard, side, party (a neighbour abuts it, checked with a tolerance and
      overlap length, not a coordinate match), sliver (a gap too narrow to look into) (Noor §2).
- [x] **Terrain datum**: ground sampled at every outline vertex; the ground floor sits at the highest ground plus a
      plinth; walls and a plinth go down to the lowest ground, so nothing floats or is buried (Noor §1).
- [x] Style: PRL panel block (panel grid, window and balcony columns, stairwell glazing over each entrance, entrance
      canopies, flat roof with parapet and lift rooms) and a plain fallback style for everything else.
- [x] Safe fallback: degenerate or self-intersecting footprints are reported and get nothing; too-thin ones get the
      plain style without openings; nothing is ever built outside the footprint (Noor §6, tested on random shapes).
- [x] Difficult footprint set: real Warsaw footprints from Baltic Drive's map data (long PRL slab, L-block, courtyard,
      touching row, wedge, sloped lot, tiny) plus labelled synthetic stress shapes (Noor §6).
- [x] Gallery: exterior beside a plan per building with street direction, edge exposure, entrances, inferred
      attributes and validation warnings (Noor's gallery idea).
- [x] GLB export with semantic extras, LOD filters, collision boxes; recipe and metadata JSON.

Known gaps in milestone 1: roofs are always flat (a mapped gabled roof is reported, drawn flat); big slabs are heavy
(~35–40k triangles for 11 storeys × 120 m: merge plain wall runs, simpler far LODs); kamienica-like buildings fall to
the plain style.

Added after the first review (Tom and Noor): courtyard gate or pedestrian passages, yard doors, through-staircases,
open or closed gates, courtyard access validation (D7, D8).

## Milestone 2 — more styles, used in the game

- [x] first cut (D9); roofs, arched brama, finer ornament next (Noor Q6–Q10).
  **Kamienica first** (Noor Q3, D8): street facade with tall windows, surrounds, cornices and a distinctive entrance;
  plain yard facades with practical windows and service doors; a brama with a rectangular or arched opening; shop
  ground floors with display windows and occasional roller shutters; varied cornices, parapets, pitched or mansard
  roofs; everything on a coherent bay grid. Ornament varies across the family (lavish, plain, damaged, renovated);
  proportions and composition come before detail.
- Detached house (pitched roofs from the footprint's minimum rectangle; straight skeleton later), outbuilding/farm,
  shop/warehouse, kamienica (street/courtyard contrast, gate passage, cornice and plinth proportions).
- Style fingerprints: several seeds per style, unlabelled, still recognisable (Noor §3 test; BadLevel's fingerprint).
- Baltic Drive adapter behind a switch: tile buildings → requests, meshes → Godot, on worker threads per 128 m chunk;
  measure against today's meshing time.

### Building types still missing (Tom, 15 Nov), in the proposed order
Roofs come first, because most of these need pitched roofs.
1. **Single-family houses** (domki jednorodzinne): the most common building in Mazowsze. Gabled or hipped roofs
   from the footprint's minimum rectangle, 1–2 storeys plus an attic, porch, garage, fence line.
2. **Warehouses and sheds** (magazyny): big simple boxes, loading docks and roller doors on the yard side, few
   windows, shallow roofs, ribbed metal cladding.
3. **Industrial and factories** (zakłady): halls with saw-tooth or shallow roofs, high ribbon windows, chimneys, brick
   for the old ones (by start_date), concrete or metal for post-war ones.
4. **Villas** (wille, przedwojenne i nowe): houses with more presence — hipped or mansard roofs, a terrace, bay windows,
   a garden; pre-war ones need age evidence, like the kamienica.
5. **Skyscrapers and towers** (wieżowce, over 42 m): today drawn by the game's skyline layer. Glass curtain walls or
   concrete grids, a podium, setbacks and a crown; the landmarks (Palace of Culture and the like) stay hand-made
   overrides, not generated.

## Milestone 3 — interiors on demand

Port BadShip's interior generator (stair core and lift aligned on every storey, corridor, room programme, wall
openings, window handshake, reachability) to polygon footprints, with Noor's believability rules (§4): flats reached
from shared circulation, never through another flat; living rooms and bedrooms on windows; wet rooms inside; no
leftover islands; shops and basements with their own circulation. Stable ids for floors, flats, rooms, doors (§5).

## Later investigations (recorded, not scheduled)

- Persistent world changes kept apart from the generated initial state; OSM edits and migration (Noor §5).
- Hand-authored overrides for landmarks and chosen interiors (Tom).
- Cut-in basements and exposed basement walls on steep lots; steps and ramps to doors (Noor §1, beyond the datum).
- Kamienica rows reading as one street frontage with shared cornice lines (Noor §2 test, needs the kamienica style).
- Swapping generated modules for Synty or hand-made pieces by type (BadShip's `asset_swap.gd`).
