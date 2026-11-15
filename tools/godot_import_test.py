"""Headless test of the Noor Interchange post-import script (godot/addons/noor_interchange/noor_post_import.gd).

    python tools/godot_import_test.py --godot "C:/path/to/Godot_v4.x_console.exe"     (or set GODOT)

Writes into godot/test/samples:
  * a BadBuilding courtyard block (synthetic_courtyard: entrances, a gate passage, the collision mass) and its LOD1
    file, exported by the CLI;
  * generic.gltf + generic.noor.json: a hand-made, tool-neutral asset with every kind of node the script handles
    (collision by v0.1 "-col", by "-colonly", by the extras flag alone; spawn.enemy, pickup, light, door, entrance
    and nav.link markers);
  * bare.gltf: the same without a manifest (the id must come from the GLB);
  * a .import file per asset pointing at the script, and expected.json for the checks.
Then imports the project headlessly and runs godot/test/noor_import_test.gd. Exit code 0 = passed.
"""
import argparse, base64, glob, json, os, shutil, struct, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join(ROOT, "godot")
SAMPLES = os.path.join(PROJECT, "test", "samples")
SCRIPT = "res://addons/noor_interchange/noor_post_import.gd"


def box_mesh_buffer():
    """A unit cube as one glTF buffer (positions, normals, indices), base64 data URI."""
    pos, nor, idx = [], [], []
    faces = [((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)), ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
             ((0, -1, 0), (1, 0, 0), (0, 0, 1)), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0))]
    for n, u, v in faces:
        base = len(pos)
        for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            pos.append(tuple(0.5 * n[k] + 0.5 * su * u[k] + 0.5 * sv * v[k] for k in range(3)))
            nor.append(n)
        idx += [base, base + 1, base + 2, base, base + 2, base + 3]
    data = b"".join(struct.pack("<3f", *p) for p in pos) + b"".join(struct.pack("<3f", *n) for n in nor)
    data += b"".join(struct.pack("<H", i) for i in idx)
    return data, len(pos), len(idx)


def generic_gltf(root_id):
    data, nv, ni = box_mesh_buffer()
    noor = lambda **kw: {"noor": dict({"v": 1}, **kw)}
    nodes = [
        {"name": "Generic", "children": list(range(1, 11)), "extras": noor(id=root_id, kind="prop")},
        {"name": "Crate", "mesh": 0, "extras": noor(part="detail")},
        {"name": "Crate-col", "mesh": 0, "extras": noor(collision="box")},          # v0.1 suffix: Godot keeps the mesh
        {"name": "Hull-colonly", "mesh": 0, "extras": noor(collision="mesh")},      # v0.2 suffix
        {"name": "Rock", "mesh": 0, "translation": [3, 0, 0], "extras": noor(collision="convex")},  # the flag alone
        {"name": "Marker_SpawnEnemy_e07", "translation": [1, 0, 2], "extras": noor(marker="spawn.enemy", id="e07", **{"class": "grunt"})},
        {"name": "Marker_Pickup_p1", "translation": [0, 1, 0], "extras": noor(marker="pickup", id="p1", amount=30, **{"class": "ammo.762"})},
        {"name": "Marker_Light_l1", "translation": [0, 3, 0], "extras": noor(marker="light", id="l1", kind="spot", color=[1, 0.9, 0.8], intensity=800)},
        {"name": "Marker_Door_d1", "extras": noor(marker="door", id="d1", width=0.9, height=2.1, opens="in")},
        {"name": "Marker_Entrance_m", "extras": noor(marker="entrance", id="m", kind="main", future_field="ignored but kept")},
        {"name": "Marker_NavLink_n1", "extras": noor(marker="nav.link", id="n1", to="e07", kind="jump")},
    ]
    return {
        "asset": {"version": "2.0", "generator": "godot_import_test.py"},
        "scene": 0, "scenes": [{"nodes": [0]}], "nodes": nodes,
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1}, "indices": 2}]}],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": nv, "type": "VEC3", "min": [-0.5] * 3, "max": [0.5] * 3},
            {"bufferView": 1, "componentType": 5126, "count": nv, "type": "VEC3"},
            {"bufferView": 2, "componentType": 5123, "count": ni, "type": "SCALAR"},
        ],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": nv * 12},
            {"buffer": 0, "byteOffset": nv * 12, "byteLength": nv * 12},
            {"buffer": 0, "byteOffset": nv * 24, "byteLength": ni * 2},
        ],
        "buffers": [{"byteLength": len(data), "uri": "data:application/octet-stream;base64," + base64.b64encode(data).decode()}],
    }


def write_import(asset):
    with open(os.path.join(SAMPLES, asset + ".import"), "w", encoding="utf-8") as f:
        f.write(f'[remap]\n\nimporter="scene"\ntype="PackedScene"\n\n[deps]\n\nsource_file="res://test/samples/{asset}"\n\n'
                f'[params]\n\nimport_script/path="{SCRIPT}"\n')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--godot", default=os.environ.get("GODOT"))
    args = ap.parse_args()
    if not args.godot:
        sys.exit("Give the Godot 4 console executable with --godot or the GODOT environment variable.")

    shutil.rmtree(SAMPLES, ignore_errors=True)
    os.makedirs(SAMPLES)
    expected = {}

    # a building from BadBuilding's own CLI
    subprocess.run(["dotnet", "run", "--project", os.path.join(ROOT, "src", "BadBuilding.Cli"), "--",
                    "generate", "--case", "synthetic_courtyard", "--out", SAMPLES], check=True, cwd=ROOT)
    manifest_path = glob.glob(os.path.join(SAMPLES, "*.noor.json"))[0]
    manifest = json.load(open(manifest_path, encoding="utf-8"))
    name = manifest["name"]
    expected[name + ".glb"] = {"asset_id": manifest["id"], "kind": "building", "collision_nodes": 1, "markers": manifest["markers"]}
    expected[name + ".lod1.glb"] = {"asset_id": manifest["id"], "kind": "building", "lod": 1, "collision_nodes": 0, "markers": {}}
    for f in glob.glob(os.path.join(SAMPLES, name + ".lod2.glb")):
        os.remove(f)   # not needed for the checks

    # a generic asset, with and without a manifest
    gltf_id, manifest_id = "11111111-2222-4333-8444-555555555555", "4c1f7b2e-9a3d-4e51-8c0b-2f6d1e9a7b33"
    for asset in ("generic", "bare"):
        with open(os.path.join(SAMPLES, asset + ".gltf"), "w", encoding="utf-8") as f:
            json.dump(generic_gltf(gltf_id), f, indent=1)
    with open(os.path.join(SAMPLES, "generic.noor.json"), "w", encoding="utf-8") as f:
        json.dump({"noor_interchange": "0.2", "id": manifest_id, "name": "Generic", "kind": "prop",
                   "generator": {"tool": "NoorTest", "version": "0"}, "created": "2026-11-15T12:00:00Z",
                   "space": {"up": "+Y", "forward": "+Z", "handedness": "right", "units": "meters"},
                   "files": [{"path": "generic.gltf", "role": "model"}]}, f, indent=1)
    markers = {"spawn.enemy": 1, "pickup": 1, "light": 1, "door": 1, "entrance": 1, "nav.link": 1}
    at = {"name": "Marker_SpawnEnemy_e07", "pos": [1, 0, 2]}
    expected["generic.gltf"] = {"asset_id": manifest_id, "kind": "prop", "collision_nodes": 3, "markers": markers, "marker_at": at}
    expected["bare.gltf"] = {"asset_id": gltf_id, "kind": "prop", "collision_nodes": 3, "markers": markers, "marker_at": at}

    for asset in expected:
        write_import(asset)
    with open(os.path.join(SAMPLES, "expected.json"), "w", encoding="utf-8") as f:
        json.dump(expected, f, indent=1)

    imp = subprocess.run([args.godot, "--headless", "--path", PROJECT, "--import"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    for line in (imp.stdout + imp.stderr).splitlines():
        if "SCRIPT ERROR" in line or "Parse Error" in line or "Noor import" in line:
            print(line)
    run = subprocess.run([args.godot, "--headless", "--path", PROJECT, "-s", "test/noor_import_test.gd"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    for line in (run.stdout + run.stderr).splitlines():
        if line.startswith("FAIL") or "noor import test" in line or "SCRIPT ERROR" in line or "Parse Error" in line:
            print(line)
    sys.exit(run.returncode)


if __name__ == "__main__":
    main()
