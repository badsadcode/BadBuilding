# Noor Interchange importer for Godot 4

`noor_post_import.gd` is an `EditorScenePostImport` script for GLB/glTF files that follow
[Noor Interchange v0.2](https://github.com/badsadcode/NoorEngine/blob/main/docs/interchange/NOOR_INTERCHANGE.md).
It knows nothing about any one generator, so BadBuilding, NoorShip, NoorLevel and the rest can all use it.

## Use

1. Copy the `addons/noor_interchange` folder into your project.
2. For each asset, pick the script as its import script. Either:
   - select the GLB, then in the Import dock set **Import Script** to `res://addons/noor_interchange/noor_post_import.gd` and press Reimport; or
   - put `import_script/path="res://addons/noor_interchange/noor_post_import.gd"` under `[params]` in its `.glb.import` file.
3. Keep the manifest (`<name>.noor.json`) next to the GLB, so the asset id reaches the scene.

## What it does

| In the glTF | In the imported scene |
|---|---|
| A node with `extras.noor.collision` (`mesh`, `convex`, `box`, `sphere`, `capsule`), whatever its name | A hidden `StaticBody3D` with a `CollisionShape3D` built from the mesh, and nothing drawn under it. Nodes named `-colonly`, `-convcolonly` or v0.1's `-col` end up the same way. |
| A node with `extras.noor.marker` (`entrance`, `door`, `passage`, `spawn.*`, `pickup`, `light`, any other type) | A `Marker3D` with the same name and transform. Metadata: `noor` (the whole block) and `noor_marker` (the type). Persistent groups: `noor_markers` and `noor_marker_<type>`, with dots as underscores (e.g. `noor_marker_spawn_enemy`). |
| The manifest, or an `id` on a top-level node | Metadata on the scene root: `noor_asset_id`, `noor_kind`, `noor_name`, `noor_interchange`, `noor_generator`, `noor_lod`, `noor_manifest`, plus the counts `noor_markers` and `noor_collision_nodes`. |

Replaced nodes keep their names. Everything else, including fields the script doesn't know, stays in the node
metadata (`extras`).

Finding markers in game code:

```gdscript
for m in get_tree().get_nodes_in_group("noor_marker_entrance"):
    print(m.name, " ", m.get_meta("noor").get("kind"))
```

## Test

```bash
python tools/godot_import_test.py --godot "C:/path/to/Godot_v4.x_console.exe"
```

The runner writes three assets into `godot/test/samples` and imports them headlessly:
- a BadBuilding courtyard block (and its LOD1 file);
- a hand-made generic glTF that covers every case above;
- the same glTF without a manifest.

It then runs `godot/test/noor_import_test.gd`. Exit code 0 means everything passed.
