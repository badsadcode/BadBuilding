@tool
extends EditorScenePostImport
## Godot 4 post-import script for Noor Interchange v0.2 GLBs (any Noor tool, not only BadBuilding).
##
## Spec: github.com/badsadcode/NoorEngine, docs/interchange/NOOR_INTERCHANGE.md.
## Set it as the scene's import script: Import dock → "Import Script" → this file (or `import_script/path` in the
## `.glb.import` file). It does three things:
##
## 1. **Collision (spec 3.5):** a node whose `extras.noor.collision` is set is never rendered. It becomes a
##    StaticBody3D with a CollisionShape3D built from its mesh (`mesh` → trimesh, `convex` → convex hull, `box` /
##    `sphere` / `capsule` → that primitive fitted to the mesh bounds), and every visual under it is removed. Nodes
##    Godot already turned into collision through the name suffix (`-colonly`, `-convcolonly`, and v0.1's `-col`)
##    end up the same way.
## 2. **Markers (spec 3.4):** every node with `extras.noor.marker` (entrance, door, passage, spawn.*, pickup, light,
##    and any other type in the table) becomes a Marker3D at the same place and orientation (+Z forward), with
##    metadata `noor` (the whole block), `noor_marker` (the type), and persistent groups `noor_markers` and
##    `noor_marker_<type>` (dots become underscores: `noor_marker_spawn_enemy`).
## 3. **Identity (spec 4):** the scene root gets metadata `noor_asset_id`, `noor_kind`, `noor_name`,
##    `noor_interchange` and `noor_generator` from the manifest next to the GLB (`<name>.noor.json`; for
##    `<name>.lod<n>.glb` the same manifest, plus `noor_lod`). Without a manifest it falls back to an `id` in a
##    top-level node's `extras.noor`.
##
## Readers ignore what they don't know (spec 3.3): unknown fields stay in the metadata untouched.

const COLLISION_SUFFIXES := ["-colonly", "-convcolonly", "-col"]
const COLLISION_TYPES := ["mesh", "convex", "box", "sphere", "capsule"]

var _root: Node
var _markers := 0
var _collisions := 0


func _post_import(scene: Node) -> Object:
	_root = scene
	_markers = 0
	_collisions = 0
	_stamp_identity(scene)
	# depth-first over a snapshot: nodes get replaced on the way
	for node in _all_nodes(scene):
		if not is_instance_valid(node) or node == scene:
			continue
		var noor := noor_of(node)
		if noor.has("collision") or _is_collision_by_name(node):
			_make_collision(node, noor)
		elif noor.has("marker"):
			_make_marker(node, noor)
	scene.set_meta("noor_markers", _markers)
	scene.set_meta("noor_collision_nodes", _collisions)
	return scene


## The `extras.noor` block Godot imported as node metadata (an empty Dictionary when there is none).
static func noor_of(node: Node) -> Dictionary:
	if not node.has_meta("extras"):
		return {}
	var extras = node.get_meta("extras")
	if typeof(extras) != TYPE_DICTIONARY or not extras.has("noor"):
		return {}
	var noor = extras["noor"]
	return noor if typeof(noor) == TYPE_DICTIONARY else {}


func _all_nodes(n: Node) -> Array[Node]:
	var out: Array[Node] = [n]
	for c in n.get_children():
		out.append_array(_all_nodes(c))
	return out


# ---- identity ---------------------------------------------------------------------------------------------------

func _stamp_identity(scene: Node) -> void:
	var source := get_source_file()
	var lod := 0
	var base := source.get_basename()  # "res://x/court.lod1"
	var m := RegEx.create_from_string("\\.lod(\\d+)$").search(base)
	if m:
		lod = int(m.get_string(1))
		base = base.substr(0, m.get_start())
	scene.set_meta("noor_lod", lod)
	var manifest_path := base + ".noor.json"
	var manifest = null
	if FileAccess.file_exists(manifest_path):
		manifest = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
	if typeof(manifest) == TYPE_DICTIONARY:
		scene.set_meta("noor_manifest", manifest_path)
		for key in ["id", "kind", "name", "noor_interchange"]:
			if manifest.has(key):
				scene.set_meta("noor_asset_id" if key == "id" else ("noor_" + key if key != "noor_interchange" else key), manifest[key])
		if manifest.has("generator") and typeof(manifest["generator"]) == TYPE_DICTIONARY:
			scene.set_meta("noor_generator", manifest["generator"])
		return
	# no manifest: an id on a top-level node (the glTF scene's root nodes)
	for c in scene.get_children():
		var noor := noor_of(c)
		if noor.has("id") and _looks_like_uuid(str(noor["id"])):
			scene.set_meta("noor_asset_id", str(noor["id"]))
			if noor.has("kind"):
				scene.set_meta("noor_kind", noor["kind"])
			return
	push_warning("Noor import: no manifest at %s and no asset id in the GLB" % manifest_path)


static func _looks_like_uuid(s: String) -> bool:
	return RegEx.create_from_string("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$").search(s) != null


# ---- collision --------------------------------------------------------------------------------------------------

func _is_collision_by_name(node: Node) -> bool:
	var n := String(node.name)
	for s in COLLISION_SUFFIXES:
		if n.ends_with(s):
			return true
	return false


func _make_collision(node: Node, noor: Dictionary) -> void:
	var kind := str(noor.get("collision", ""))
	if not kind in COLLISION_TYPES:
		kind = "convex" if String(node.name).ends_with("-convcolonly") else "mesh"
	# the geometry: the node's own mesh, or a mesh among its descendants
	var mesh: Mesh = null
	if node is MeshInstance3D:
		mesh = node.mesh
	if mesh == null:
		for d in node.find_children("*", "MeshInstance3D", true, false):
			mesh = (d as MeshInstance3D).mesh
			if mesh:
				break
	var shapes: Array[CollisionShape3D] = []
	if mesh:
		var cs := CollisionShape3D.new()
		cs.name = "Shape"
		cs.shape = _shape_for(mesh, kind)
		if kind in ["box", "sphere", "capsule"]:
			cs.position = mesh.get_aabb().get_center()
		shapes.append(cs)
	else:
		# Godot already built the shapes (from a name suffix): keep them
		for d in node.find_children("*", "CollisionShape3D", true, false):
			shapes.append(d)
	var body := StaticBody3D.new()
	body.name = node.name
	if node is Node3D:
		body.transform = (node as Node3D).transform
	for k in node.get_meta_list():
		body.set_meta(k, node.get_meta(k))
	body.set_meta("noor_collision", kind)
	body.visible = false   # spec 3.5: never rendered (shapes still collide)
	var parent := node.get_parent()
	var index := node.get_index()
	for s in shapes:
		var keep: Transform3D = s.global_transform if s.is_inside_tree() else s.transform
		if s.get_parent():
			s.get_parent().remove_child(s)
		body.add_child(s)
		if s.is_inside_tree():
			s.global_transform = keep
	_replace(node, body, parent, index)
	_collisions += 1


func _shape_for(mesh: Mesh, kind: String) -> Shape3D:
	var aabb := mesh.get_aabb()
	match kind:
		"convex":
			return mesh.create_convex_shape(true, false)
		"box":
			var b := BoxShape3D.new()
			b.size = aabb.size
			return b
		"sphere":
			var s := SphereShape3D.new()
			s.radius = max(aabb.size.x, aabb.size.y, aabb.size.z) * 0.5
			return s
		"capsule":
			var c := CapsuleShape3D.new()
			c.radius = max(aabb.size.x, aabb.size.z) * 0.5
			c.height = max(aabb.size.y, c.radius * 2.0)
			return c
		_:
			return mesh.create_trimesh_shape()


# ---- markers ----------------------------------------------------------------------------------------------------

func _make_marker(node: Node, noor: Dictionary) -> void:
	var type := str(noor["marker"])
	var marker := Marker3D.new()
	marker.name = node.name
	if node is Node3D:
		marker.transform = (node as Node3D).transform
	for k in node.get_meta_list():
		marker.set_meta(k, node.get_meta(k))
	marker.set_meta("noor", noor)
	marker.set_meta("noor_marker", type)
	marker.add_to_group("noor_markers", true)
	marker.add_to_group("noor_marker_" + type.replace(".", "_"), true)
	_replace(node, marker, node.get_parent(), node.get_index())
	_markers += 1


# ---- tree surgery -----------------------------------------------------------------------------------------------

## Puts `fresh` where `old` was (same parent, same index), moves `old`'s remaining children (minus visuals for a
## collision body) under it, and frees `old`.
func _replace(old: Node, fresh: Node, parent: Node, index: int) -> void:
	var keep_name := old.name
	for c in old.get_children():
		if fresh is CollisionObject3D and (c is VisualInstance3D or c is CollisionObject3D):
			continue   # visuals go; collision Godot made itself was already harvested
		old.remove_child(c)
		fresh.add_child(c)
	# the old node leaves first, so the new one can take its readable name (spec 3.6)
	parent.remove_child(old)
	old.free()
	parent.add_child(fresh)
	parent.move_child(fresh, index)
	fresh.name = keep_name
	_own(fresh)


func _own(n: Node) -> void:
	n.owner = _root
	for c in n.get_children():
		_own(c)
