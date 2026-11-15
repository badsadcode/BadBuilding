# BadBuilding

A procedural **building generator** library: a footprint plus a few facts in, a whole building out, deterministic from a
seed. Engine-agnostic C# (.NET 8, no dependencies), built for Baltic Drive's Mazowsze first and meant to become a
reusable DLL for other Bad Ideas Works tools. Ideas and code lifted from
[BadShip](https://github.com/badsadcode/BadShip) and [BadLevel](https://github.com/badsadcode/BadLevel).

**Status: milestone 1 of [the roadmap](docs/ROADMAP.md).** Exteriors only: a PRL panel block style, a kamienica style and a plain fallback.
Interiors come later.

## What it does now

- **Footprint + context in:** outer ring and holes, levels, height, type, roof, mapped entrances, house number, plus
  neighbouring footprints, street lines and ground heights round the outline.
- **Reads the site first:**
  - every wall edge is classified as *street*, *side*, *courtyard*, *party* (a neighbour abuts it: no windows) or
    *sliver* (a gap too narrow to look into);
  - the ground floor sits on the high side of a sloped lot and the plinth reaches the low side.
- **Styles with grammar, not random greeble:**
  - **PRL panel block:** panel joints, one window per bay, balconies stacked in columns, glass-block staircase strips
    over each entrance, entrance canopies, flat roof with parapet and lift machine rooms.
  - **Kamienica:** the pre-war tenement in a frontage: a dressed street front (surrounds, string course, cornice,
    attic, shop fronts, a brama) against a plain yard side, brick firewalls with chimneys, three ornament levels.
  - **Plain:** the fallback for everything else.
- **Courtyards can be reached:** every courtyard gets a gate passage (brama przejazdowa) or a pedestrian passage cut
  through the ground floor, plus a staircase door; validation flags any yard nobody can get into.
- **Honest about what it guessed:** every attribute is marked `osm`, `request` or `inferred`.
- **Fails safely:** degenerate, self-intersecting, tiny or too-thin footprints are reported with a reason and a fallback,
  never built outside the outline.
- **Out, in [Noor Interchange v0.1](https://github.com/badsadcode/NoorEngine/blob/main/docs/interchange/NOOR_INTERCHANGE.md):**
  - `<name>.glb` (LOD0): origin at the bottom centre of the footprint; parts and markers described in node
    `extras.noor`; doors, shop doors, gate passages and roof hatches are `entrance` markers; the bare mass is a
    `-col` collision node;
  - `<name>.lod1.glb` and `<name>.lod2.glb` (the bare mass);
  - `<name>.noor.json`, the manifest (validates against `docs/interchange/noor.asset.schema.json`);
  - `<name>.recipe.json`, with a stable UUID `id`, the `seed` and the `version`;
  - `<name>.metadata.json` (in building space) and `<name>.validation.json`.

## Quick start

```bash
dotnet run --project src/BadBuilding.Cli -- gallery --out out/gallery
python -m http.server --directory out/gallery 8766    # then open http://localhost:8766
dotnet run --project src/BadBuilding.Cli -- generate --case l_block --out out/one
dotnet run --project src/BadBuilding.Cli -- generate --case synthetic_slab --stage-seed look=5 --out out/one
dotnet run --project src/BadBuilding.Cli -- stress --count 1000
dotnet test BadBuilding.slnx
```

The gallery shows each building in 3D beside its plan. The plan includes:
- the footprint, neighbours and streets;
- wall exposure, colour-coded;
- doors (mapped or inferred);
- the attributes with their sources;
- validation warnings.

## Test footprints

- **`samples/warsaw.json`:** real buildings from central Warsaw with their context, cut from OSM by `tools/osm_samples.py`:
  long PRL slabs, PRL blocks, L-blocks, courtyards, touching rows, wedges, houses, outbuildings and a tiny one.
  *Map data © OpenStreetMap contributors, ODbL 1.0.*
- **Synthetic stress shapes** (labelled in the gallery): a slab, an L, a courtyard block, a 25° wedge, a deep recess, the
  middle of a touching row, a wall facing a 2.5 m gap, a house on a 3 m slope, a 1.5 m strip, a bowtie and a 1 m² square.

## Conventions

- **Units and axes:** metres, +Y up, +X east, +Z south (north is −Z). The GLBs are shifted so the footprint's
  bottom centre is the origin (`pivot_in_building_space` in the manifest and on the root node puts them back).
- **Lifted, not referenced:** `Rng`, `Mesh`, `Aabb` and the GLB writer come from BadShip 0.31 (see
  [docs/DECISIONS.md](docs/DECISIONS.md)).
- **Questions for Noor:** [docs/NOOR_SAGE.md](docs/NOOR_SAGE.md), open questions and her answers.
- **Background:** the recon documents [docs/RECON_SAGE.md](docs/RECON_SAGE.md) and
  [docs/RECON_NOOR.md](docs/RECON_NOOR.md).

Licence: not chosen yet (Bad Ideas Works).
