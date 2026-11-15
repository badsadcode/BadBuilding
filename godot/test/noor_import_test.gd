extends SceneTree
## Headless checks of addons/noor_interchange/noor_post_import.gd on the scenes tools/godot_import_test.py imported.
## Run: godot --headless --path godot -s test/noor_import_test.gd   (after --import). Exit code 0 = all passed.

var failures := 0
var checks := 0


func check(ok: bool, what: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("FAIL: ", what)


func _init() -> void:
	var expected = JSON.parse_string(FileAccess.get_file_as_string("res://test/samples/expected.json"))
	check(typeof(expected) == TYPE_DICTIONARY, "expected.json readable")
	if typeof(expected) == TYPE_DICTIONARY:
		for file in expected.keys():
			check_scene(file, expected[file])
	print("noor import test: %d checks, %d failed" % [checks, failures])
	quit(1 if failures > 0 else 0)


func check_scene(file: String, want: Dictionary) -> void:
	var packed = load("res://test/samples/" + file)
	check(packed is PackedScene, file + " imported as a scene")
	if not packed is PackedScene:
		return
	var root: Node = packed.instantiate()
	var tag := file + ": "

	# identity on the root
	check(root.get_meta("noor_asset_id", "") == want["asset_id"], tag + "asset id %s, want %s" % [root.get_meta("noor_asset_id", ""), want["asset_id"]])
	check(root.get_meta("noor_kind", "") == want["kind"], tag + "kind")
	check(root.get_meta("noor_lod", -1) == want.get("lod", 0), tag + "lod")

	# collision: present as bodies, nothing under them drawn, no collision-flagged node left drawable
	var bodies := 0
	for n in root.find_children("*", "", true, false):
		var noor := _noor(n)
		if noor.has("collision"):
			bodies += 1
			check(n is StaticBody3D, tag + "%s with noor.collision is a StaticBody3D" % n.name)
			check(not (n as Node3D).visible, tag + "%s is hidden" % n.name)
			check(n.find_children("*", "VisualInstance3D", true, false).is_empty(), tag + "%s has nothing to draw" % n.name)
			check(not n.find_children("*", "CollisionShape3D", true, false).is_empty(), tag + "%s has a shape" % n.name)
		if n is VisualInstance3D:
			var p := n
			while p != null and p != root:
				check(not _noor(p).has("collision"), tag + "%s draws inside a collision node" % n.name)
				p = p.get_parent()
	check(bodies == want["collision_nodes"], tag + "%d collision nodes, want %d" % [bodies, want["collision_nodes"]])
	check(root.get_meta("noor_collision_nodes", -1) == want["collision_nodes"], tag + "collision count on the root")

	# replaced nodes keep their readable names (spec 3.6), never Godot's "@Marker3D@123"
	for n in root.find_children("*", "", true, false):
		check(not String(n.name).begins_with("@"), tag + "auto-generated name %s" % n.name)

	# markers: Marker3D with their data, grouped per type
	var counts := {}
	for n in root.find_children("*", "", true, false):
		if _noor(n).has("marker"):
			check(n is Marker3D, tag + "%s is a Marker3D" % n.name)
			check(n.get_meta("noor_marker", "") == _noor(n)["marker"], tag + "%s noor_marker meta" % n.name)
			check(n.get_meta("noor", {}).has("id"), tag + "%s keeps its id" % n.name)
			var type: String = _noor(n)["marker"]
			check(n.is_in_group("noor_markers") and n.is_in_group("noor_marker_" + type.replace(".", "_")), tag + "%s groups" % n.name)
			counts[type] = counts.get(type, 0) + 1
	for type in want["markers"].keys():
		check(counts.get(type, 0) == int(want["markers"][type]), tag + "%s markers: %d, want %d" % [type, counts.get(type, 0), want["markers"][type]])
	check(counts.size() == want["markers"].size(), tag + "marker types %s" % str(counts.keys()))

	# paired passages point at each other
	var passages := {}
	for n in root.find_children("*", "Marker3D", true, false):
		if n.get_meta("noor_marker", "") == "passage":
			passages[n.get_meta("noor")["id"]] = n.get_meta("noor")
	for id in passages:
		check(passages.has(passages[id]["pair"]) and passages[passages[id]["pair"]]["pair"] == id, tag + "passage %s is paired" % id)

	# a marker keeps its place: the first one's position matches the glTF (written by the runner)
	if want.has("marker_at"):
		var m := root.find_child(want["marker_at"]["name"], true, false) as Node3D
		check(m != null and m.position.distance_to(Vector3(want["marker_at"]["pos"][0], want["marker_at"]["pos"][1], want["marker_at"]["pos"][2])) < 0.01,
			tag + "marker %s position" % want["marker_at"]["name"])
	root.free()


func _noor(n: Node) -> Dictionary:
	if not n.has_meta("extras"):
		return {}
	var e = n.get_meta("extras")
	return e["noor"] if typeof(e) == TYPE_DICTIONARY and e.has("noor") and typeof(e["noor"]) == TYPE_DICTIONARY else {}
