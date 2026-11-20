extends SceneTree
## Photographs exported buildings (D40): loads GLB files at run time, stands them in a row, lights them and saves PNGs.
##   godot --path godot -s res://test/shoot.gd -- --glb "E:/.../out/gallery/04*_podlasie*.glb" --out C:/temp/shots [--dist 16]
## Shots: the whole row from the street side, each building from the street corner, and one close-up of the first.

var _args := {}

func _init() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--"):
			var kv := a.substr(2).split("=", true, 1)
			_args[kv[0]] = kv[1] if kv.size() > 1 else ""
	_run.call_deferred()

func _files(pattern: String) -> Array[String]:
	var dir := pattern.get_base_dir()
	var mask := pattern.get_file()
	var out: Array[String] = []
	var d := DirAccess.open(dir)
	if d == null:
		return out
	for f in d.get_files():
		if f.match(mask) and not f.contains(".lod"):
			out.append(dir.path_join(f))
	out.sort()
	return out

func _run() -> void:
	var files := _files(_args.get("glb", ""))
	var out: String = _args.get("out", "user://shots")
	DirAccess.make_dir_recursive_absolute(out)
	var spacing := float(_args.get("dist", "16"))
	var root := Node3D.new()
	get_root().add_child(root)
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	sky.sky_material = ProceduralSkyMaterial.new()
	e.sky = sky
	e.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	e.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.environment = e
	root.add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, -35, 0)
	sun.shadow_enabled = true
	root.add_child(sun)
	var ground := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(400, 400)
	ground.mesh = pm
	var gm := StandardMaterial3D.new()
	gm.albedo_color = Color(0.36, 0.45, 0.25)
	ground.material_override = gm
	ground.position.y = -0.02
	root.add_child(ground)
	var centres: Array[Vector3] = []
	var i := 0
	for f in files:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		if doc.append_from_file(f, state) != OK:
			print("shoot: could not read ", f)
			continue
		var scene := doc.generate_scene(state)
		# collision-only nodes (Noor Interchange "-colonly") are not to be seen
		for n in scene.find_children("*colonly*", "", true, false):
			n.queue_free()
		var holder := Node3D.new()
		holder.position = Vector3(i * spacing, 0, 0)
		holder.add_child(scene)
		root.add_child(holder)
		centres.append(holder.position)
		i += 1
	print("shoot: ", centres.size(), " buildings")
	var cam := Camera3D.new()
	cam.fov = 50
	root.add_child(cam)
	cam.current = true
	await _frames(10)
	# the buildings' pivot is their footprint centre; the street is to the +Z side
	var z0 := float(_args.get("pivotz", "-12"))
	var mid := Vector3((centres.size() - 1) * spacing * 0.5, 2.5, z0)
	cam.position = mid + Vector3(0, 9, 26 + centres.size() * 3.0)
	cam.look_at(mid, Vector3.UP)
	await _shot(out.path_join("row.png"))
	for k in centres.size():
		var c := centres[k] + Vector3(0, 2.4, z0)
		cam.position = c + Vector3(9, 2.6, 14)
		cam.look_at(c, Vector3.UP)
		await _shot(out.path_join("house_%02d.png" % k))
		cam.position = c + Vector3(-11, 2.0, -3)
		cam.look_at(c, Vector3.UP)
		await _shot(out.path_join("house_%02d_side.png" % k))
	if centres.size() > 0:
		var c0 := centres[0] + Vector3(0, 3.5, z0 + 6)
		cam.position = c0 + Vector3(1.5, 0.2, 6)
		cam.look_at(c0 + Vector3(0, -0.5, 0), Vector3.UP)
		await _shot(out.path_join("close.png"))
	quit()

func _frames(n: int) -> void:
	for k in n:
		await process_frame

func _shot(path: String) -> void:
	await _frames(4)
	get_root().get_texture().get_image().save_png(path)
	print("shoot: saved ", path)
