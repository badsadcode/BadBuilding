// Lifted from BadShip 0.31 (github.com/badsadcode/BadShip, same author), docs/DECISIONS.md D2.
using System.Numerics;
using System.Text;
using System.Text.Json;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Export;

/// <summary>
/// glTF 2.0 binary (.glb), written by hand (the format is small and this keeps the core dependency-free).
/// One node per module, nested by attachment, each with its pivot at its bounding-box centre; one primitive per
/// material role; functional markers as empty nodes whose +Z points along the marker's direction. Y up, metres, +X east,
/// +Z south (D1). Lifted from BadShip's writer and made generic (D2).
/// Noor Interchange v0.1 (D14): the asset's origin is the bottom centre of the footprint (<see cref="Noor.Pivot"/>),
/// all semantic data sits in <c>extras.noor</c> (parts, markers as <c>entrance</c>), and LOD0 carries the bare mass as
/// a <c>-colonly</c> collision node (v0.2: the extras flag is the rule, the name the hint for Godot).
/// </summary>
public static class GlbWriter
{
    private const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;
    private const int Float = 5126, UnsignedInt = 5125, UnsignedShort = 5123;

    /// <param name="ship">The building to write (the name is BadShip's, where this writer came from).</param>
    /// <param name="include">Which modules to write (all when null): how LODs are made.</param>
    /// <param name="markers">Whether to write the functional marker nodes.</param>
    /// <param name="lod">The level of detail this file is (written into the root's <c>extras.noor</c>).</param>
    /// <param name="collision">Whether to add the mass modules not otherwise included as <c>-colonly</c> collision nodes.</param>
    /// <param name="assetId">The asset's identity (default: <see cref="Noor.AssetId"/>).</param>
    public static byte[] Write(Building ship, Func<Module, bool>? include = null, bool markers = true, int lod = 0, bool collision = false, Guid? assetId = null)
    {
        var pivot = Noor.Pivot(ship);
        var bin = new MemoryStream();
        var bufferViews = new List<object>();
        var accessors = new List<object>();
        var meshes = new List<object>();
        var nodes = new List<Dictionary<string, object>>();
        var children = new List<List<int>>();
        var materials = new List<object>();
        var materialIndex = new Dictionary<string, int>();
        var images = new List<object>();
        var textures = new List<object>();
        var usesEmissiveStrength = false;

        int View(byte[] data, int? target)
        {
            while (bin.Length % 4 != 0) bin.WriteByte(0);
            var offset = (int)bin.Length;
            bin.Write(data);
            var v = new Dictionary<string, object> { ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = data.Length };
            if (target != null) v["target"] = target.Value;
            bufferViews.Add(v);
            return bufferViews.Count - 1;
        }

        int Accessor(int view, int componentType, int count, string type, float[]? min = null, float[]? max = null)
        {
            var a = new Dictionary<string, object> { ["bufferView"] = view, ["componentType"] = componentType, ["count"] = count, ["type"] = type };
            if (min != null) a["min"] = min;
            if (max != null) a["max"] = max;
            accessors.Add(a);
            return accessors.Count - 1;
        }

        int Material(string role)
        {
            if (materialIndex.TryGetValue(role, out var i)) return i;
            var d = ship.Materials.TryGetValue(role, out var md) ? md : new MaterialDef(role, new Vector3(0.8f), 0.9f, 0f);
            var mat = new Dictionary<string, object>
            {
                ["name"] = role,
                ["pbrMetallicRoughness"] = new Dictionary<string, object>
                {
                    ["baseColorFactor"] = new[] { d.Color.X, d.Color.Y, d.Color.Z, d.Alpha },
                    ["metallicFactor"] = d.Metallic,
                    ["roughnessFactor"] = d.Roughness,
                },
            };
            if (d.Alpha < 1f) mat["alphaMode"] = "BLEND";
            materials.Add(mat);
            return materialIndex[role] = materials.Count - 1;
        }

        int WriteMesh(Geometry.Mesh mesh, Vector3 origin, string name)
        {
            var n = mesh.Positions.Count;
            var pos = new float[n * 3];
            var nor = new float[n * 3];
            var uv = new float[n * 2];
            var min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            var max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (var i = 0; i < n; i++)
            {
                var p = mesh.Positions[i] - origin;
                pos[i * 3] = p.X;
                pos[i * 3 + 1] = p.Y;
                pos[i * 3 + 2] = p.Z;
                for (var k = 0; k < 3; k++)
                {
                    min[k] = MathF.Min(min[k], pos[i * 3 + k]);
                    max[k] = MathF.Max(max[k], pos[i * 3 + k]);
                }
                var q = Vector3.Normalize(mesh.Normals[i]);
                nor[i * 3] = q.X;
                nor[i * 3 + 1] = q.Y;
                nor[i * 3 + 2] = q.Z;
                uv[i * 2] = mesh.Uvs[i].X;
                uv[i * 2 + 1] = mesh.Uvs[i].Y;
            }
            var posAcc = Accessor(View(Bytes(pos), ArrayBuffer), Float, n, "VEC3", min, max);
            var norAcc = Accessor(View(Bytes(nor), ArrayBuffer), Float, n, "VEC3");
            var uvAcc = Accessor(View(Bytes(uv), ArrayBuffer), Float, n, "VEC2");
            var prims = new List<object>();
            foreach (var role in mesh.Groups.Keys)
            {
                var idx = mesh.Groups[role];
                if (idx.Count == 0) continue;
                int idxAcc;
                if (n < 65536)
                {
                    var data = new byte[idx.Count * 2];
                    for (var i = 0; i < idx.Count; i++) BitConverter.TryWriteBytes(data.AsSpan(i * 2), (ushort)idx[i]);
                    idxAcc = Accessor(View(data, ElementArrayBuffer), UnsignedShort, idx.Count, "SCALAR");
                }
                else
                {
                    var data = new byte[idx.Count * 4];
                    for (var i = 0; i < idx.Count; i++) BitConverter.TryWriteBytes(data.AsSpan(i * 4), (uint)idx[i]);
                    idxAcc = Accessor(View(data, ElementArrayBuffer), UnsignedInt, idx.Count, "SCALAR");
                }
                prims.Add(new Dictionary<string, object>
                {
                    ["attributes"] = new Dictionary<string, int> { ["POSITION"] = posAcc, ["NORMAL"] = norAcc, ["TEXCOORD_0"] = uvAcc },
                    ["indices"] = idxAcc,
                    ["material"] = Material(role),
                });
            }
            meshes.Add(new Dictionary<string, object> { ["name"] = name, ["primitives"] = prims });
            return meshes.Count - 1;
        }

        int AddNode(Dictionary<string, object> node)
        {
            nodes.Add(node);
            children.Add([]);
            return nodes.Count - 1;
        }

        var r = ship.Request;
        var root = AddNode(new Dictionary<string, object>
        {
            ["name"] = r.Id,
            ["extras"] = Extras(new Dictionary<string, object?>
            {
                ["id"] = Noor.Text(assetId ?? Noor.AssetId(r)),
                ["kind"] = Noor.Kind,
                ["lod"] = lod,
                ["props"] = new Dictionary<string, object?>
                {
                    ["building_id"] = r.Id,
                    ["generator"] = "BadBuilding",
                    ["generator_version"] = BuildingGenerator.Version,
                    ["seed"] = r.Seed,
                    ["style"] = ship.Plan?.Style ?? "none",
                    ["east"] = "+X",
                    ["south"] = "+Z",
                    ["pivot_in_building_space"] = new[] { pivot.X, pivot.Y, pivot.Z },
                },
            }),
        });

        var nodeOf = new Dictionary<string, int>();
        var originOf = new Dictionary<string, Vector3>();
        foreach (var m in ParentsFirst(ship.Modules))
        {
            bool col = false;
            if (include != null && !include(m))
            {
                // the bare mass doubles as the collision shape (spec 3.5): a "-colonly" node in the detailed file
                if (!(collision && m.Type == "mass" && !m.Mesh.IsEmpty)) continue;
                col = true;
            }
            var parentNode = m.ParentId != null && nodeOf.TryGetValue(m.ParentId, out var pn) ? pn : root;
            // top-level parts are placed relative to the asset's pivot (bottom centre of the footprint)
            var parentOrigin = m.ParentId != null && originOf.TryGetValue(m.ParentId, out var po) ? po : pivot;
            var origin = m.Mesh.IsEmpty ? parentOrigin : Snap(m.Bounds.Center);
            var t = origin - parentOrigin;
            var props = new Dictionary<string, object?>
            {
                ["type"] = m.Type,
                ["tier"] = m.Tier.ToString().ToLowerInvariant(),
                ["parent"] = m.ParentId,
                ["lods"] = m.Lods,
            };
            if (m.Extras != null)
                foreach (var (k, v) in m.Extras) props[k] = v;
            var noor = new Dictionary<string, object?> { ["id"] = m.Id };
            if (col) noor["collision"] = "mesh";
            else if (Noor.Part(m.Type) is { } part) noor["part"] = part;
            noor["props"] = props;
            var name = col ? m.Id + "-colonly" : m.Id;
            var node = new Dictionary<string, object>
            {
                ["name"] = name,
                ["extras"] = Extras(noor),
            };
            if (t != Vector3.Zero) node["translation"] = new[] { t.X, t.Y, t.Z };
            if (!m.Mesh.IsEmpty) node["mesh"] = WriteMesh(m.Mesh, origin, name);
            var idx = AddNode(node);
            children[parentNode].Add(idx);
            nodeOf[m.Id] = idx;
            originOf[m.Id] = origin;
        }

        var exported = ship.Markers.Where(Noor.Exported).ToList();
        var markersNode = markers && exported.Count > 0 ? AddNode(new Dictionary<string, object> { ["name"] = "Markers" }) : -1;
        if (markersNode >= 0) children[root].Add(markersNode);
        foreach (var mk in markersNode >= 0 ? exported : [])
        {
            var q = RotationFromZ(mk.Direction);
            var (marker, kind, fields) = Noor.MarkerOf(ship, mk);
            var p = mk.Position - pivot;
            var noor = new Dictionary<string, object?> { ["marker"] = marker, ["id"] = mk.Id, ["kind"] = kind };
            foreach (var (k, v) in fields) noor[k] = v;
            noor["props"] = new Dictionary<string, object?>
            {
                ["badbuilding_kind"] = mk.Kind,
                ["direction"] = new[] { mk.Direction.X, mk.Direction.Y, mk.Direction.Z },
            };
            var node = new Dictionary<string, object>
            {
                ["name"] = (marker == "passage" ? "Marker_Passage_" : "Marker_Entrance_") + Local(mk.Id),
                ["translation"] = new[] { p.X, p.Y, p.Z },
                ["extras"] = Extras(noor),
            };
            if (q != Quaternion.Identity) node["rotation"] = new[] { q.X, q.Y, q.Z, q.W };
            children[markersNode].Add(AddNode(node));
        }

        for (var i = 0; i < nodes.Count; i++)
            if (children[i].Count > 0) nodes[i]["children"] = children[i];

        while (bin.Length % 4 != 0) bin.WriteByte(0);
        var gltf = new Dictionary<string, object>
        {
            ["asset"] = new { version = "2.0", generator = $"BadBuilding {BuildingGenerator.Version}" },
            ["scene"] = 0,
            ["scenes"] = new[] { new { name = "Building", nodes = new[] { root } } },
            ["nodes"] = nodes,
            ["meshes"] = meshes,
            ["materials"] = materials,
            ["accessors"] = accessors,
            ["bufferViews"] = bufferViews,
            ["buffers"] = new[] { new { byteLength = (int)bin.Length } },
        };
        if (usesEmissiveStrength) gltf["extensionsUsed"] = new[] { "KHR_materials_emissive_strength" };
        if (images.Count > 0)
        {
            gltf["images"] = images;
            gltf["textures"] = textures;
            gltf["samplers"] = new[] { new Dictionary<string, object> { ["magFilter"] = 9729, ["minFilter"] = 9987, ["wrapS"] = 33071, ["wrapT"] = 33071 } };
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(gltf, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var jsonPad = (4 - json.Length % 4) % 4;
        var binBytes = bin.ToArray();
        var total = 12 + 8 + json.Length + jsonPad + 8 + binBytes.Length;
        using var outStream = new MemoryStream(total);
        using var w = new BinaryWriter(outStream);
        w.Write(0x46546C67u); // "glTF"
        w.Write(2u);
        w.Write((uint)total);
        w.Write((uint)(json.Length + jsonPad));
        w.Write(0x4E4F534Au); // "JSON"
        w.Write(json);
        for (var i = 0; i < jsonPad; i++) w.Write((byte)' ');
        w.Write((uint)binBytes.Length);
        w.Write(0x004E4942u); // "BIN\0"
        w.Write(binBytes);
        w.Flush();
        return outStream.ToArray();
    }

    /// <summary>Modules ordered so every parent comes before its children.</summary>
    internal static List<Module> ParentsFirst(IReadOnlyList<Module> modules)
    {
        var ids = modules.Select(m => m.Id).ToHashSet();
        var done = new HashSet<string>();
        var result = new List<Module>();
        var pending = modules.ToList();
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var m in pending.ToList())
            {
                if (m.ParentId is null || !ids.Contains(m.ParentId) || done.Contains(m.ParentId))
                {
                    result.Add(m);
                    done.Add(m.Id);
                    pending.Remove(m);
                    progressed = true;
                }
            }
            if (!progressed) { result.AddRange(pending); break; } // parent loops: validation reports them
        }
        return result;
    }

    /// <summary><c>extras</c> holding only the versioned Noor block (spec 3.3).</summary>
    private static Dictionary<string, object> Extras(Dictionary<string, object?> noor)
    {
        var block = new Dictionary<string, object?> { ["v"] = Noor.BlockVersion };
        foreach (var (k, v) in noor) block[k] = v;
        return new Dictionary<string, object> { ["noor"] = block };
    }

    /// <summary>A marker id without the building prefix, for readable node names ("entrance.e0.3.2").</summary>
    private static string Local(string id) => id[(id.LastIndexOf('/') + 1)..];

    private static Vector3 Snap(Vector3 v) => new(MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3));

    /// <summary>The rotation that turns +Z onto <paramref name="dir"/>.</summary>
    internal static Quaternion RotationFromZ(Vector3 dir)
    {
        var d = Vector3.Normalize(dir);
        var dot = Vector3.Dot(Vector3.UnitZ, d);
        if (dot > 0.99999f) return Quaternion.Identity;
        if (dot < -0.99999f) return new Quaternion(0, 1, 0, 0);
        var axis = Vector3.Cross(Vector3.UnitZ, d);
        return Quaternion.Normalize(new Quaternion(axis, 1 + dot));
    }

    private static byte[] Bytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
