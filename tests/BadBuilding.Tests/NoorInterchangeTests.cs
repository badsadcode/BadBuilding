using System.Text.Json;
using System.Text.RegularExpressions;
using BadBuilding.Export;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Tests;

/// <summary>Noor Interchange v0.1 (D14): the manifest, the stable id, extras.noor, markers, collision.</summary>
public class NoorInterchangeTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    /// <summary>A courtyard block well away from the origin: street doors, yard doors, a gate passage, a roof.</summary>
    private static BuildingRequest Court(string id = "test:noor_court") => new()
    {
        Id = id, Seed = 3, Outer = Rect(75, 175, 125, 225), Holes = [new Ring([new(90, 190), new(90, 210), new(110, 210), new(110, 190)])],
        Type = "apartments", Levels = 5, Streets = [new StreetLine([new(40, 240), new(160, 240)])],
    };

    private static readonly DateTimeOffset Created = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "badbuilding-noor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static JsonElement GltfJson(byte[] glb)
    {
        int len = BitConverter.ToInt32(glb, 12);
        return JsonDocument.Parse(glb.AsMemory(20, len)).RootElement.Clone();
    }

    private static IEnumerable<JsonElement> Nodes(JsonElement gltf) => gltf.GetProperty("nodes").EnumerateArray();

    private static JsonElement? NoorOf(JsonElement node) =>
        node.TryGetProperty("extras", out var e) && e.TryGetProperty("noor", out var n) ? n : null;

    [Fact]
    public void Save_writes_every_old_file_and_a_manifest_that_validates_against_the_schema()
    {
        var dir = TempDir();
        var b = BuildingGenerator.Generate(Court());
        BuildingFiles.Save(b, dir, "court", Created);
        foreach (var ext in new[] { ".glb", ".lod1.glb", ".lod2.glb", ".recipe.json", ".metadata.json", ".validation.json", ".noor.json" })
            Assert.True(File.Exists(Path.Combine(dir, "court" + ext)), ext);

        var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "noor.asset.schema.json"))).RootElement;
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "court.noor.json"))).RootElement;
        var errors = MiniSchema.Validate(schema, manifest);
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        Assert.Equal("0.1", manifest.GetProperty("noor_interchange").GetString());
        Assert.Equal("building", manifest.GetProperty("kind").GetString());
        Assert.Equal("2026-11-15T12:00:00Z", manifest.GetProperty("created").GetString());
        Assert.Equal("mesh", manifest.GetProperty("collision").GetString());
        Assert.True(manifest.GetProperty("markers").GetProperty("entrance").GetInt32() >= 3);
        // the listed hashes are the files' own
        foreach (var f in manifest.GetProperty("files").EnumerateArray())
            if (f.TryGetProperty("sha256", out var sha))
                Assert.Equal(sha.GetString(), Noor.Sha256(File.ReadAllBytes(Path.Combine(dir, f.GetProperty("path").GetString()!))));
    }

    [Fact]
    public void The_mini_schema_checker_rejects_a_broken_manifest()
    {
        var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "noor.asset.schema.json"))).RootElement;
        var bad = JsonDocument.Parse("""{ "noor_interchange": "1.0", "id": "not-a-uuid", "name": "x", "kind": "spaceship", "generator": { "tool": "Blender" }, "created": "now", "space": { "up": "+Z", "forward": "+Z", "handedness": "right", "units": "meters" }, "files": [] }""").RootElement;
        var errors = MiniSchema.Validate(schema, bad);
        foreach (var where in new[] { "noor_interchange", "id", "kind", "generator", "space.up", "files" })
            Assert.Contains(errors, e => e.StartsWith(where));
    }

    [Fact]
    public void The_recipe_keeps_a_stable_uuid_across_regeneration()
    {
        var dir = TempDir();
        var id1 = BuildingFiles.Save(BuildingGenerator.Generate(Court()), dir, "court", Created);
        var recipe = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "court.recipe.json"))).RootElement;
        Assert.Equal(Noor.Text(id1), recipe.GetProperty("id").GetString());
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", recipe.GetProperty("id").GetString());
        Assert.Equal(3UL, recipe.GetProperty("seed").GetUInt64());
        Assert.Equal(BuildingGenerator.Version, recipe.GetProperty("version").GetString());

        // the same building again, elsewhere: the same id (derived from the building id)
        Assert.Equal(id1, BuildingFiles.Save(BuildingGenerator.Generate(Court()), TempDir(), "court", Created));
        // regenerated over the old files with a renamed building: the recipe's id is kept
        Assert.Equal(id1, BuildingFiles.Save(BuildingGenerator.Generate(Court("test:renamed")), dir, "court", Created));
        // an explicit id wins
        var mine = Guid.Parse("4c1f7b2e-9a3d-4e51-8c0b-2f6d1e9a7b33");
        Assert.Equal(mine, BuildingFiles.Save(BuildingGenerator.Generate(Court() with { AssetId = mine }), dir, "court", Created));
        // and the GLB carries it on its root
        var root = Nodes(GltfJson(File.ReadAllBytes(Path.Combine(dir, "court.glb")))).First();
        Assert.Equal(Noor.Text(mine), NoorOf(root)!.Value.GetProperty("id").GetString());
        // different buildings, different ids
        Assert.NotEqual(Noor.AssetId(Court("a")), Noor.AssetId(Court("b")));
    }

    [Fact]
    public void Markers_are_entrances_in_extras_noor()
    {
        var b = BuildingGenerator.Generate(Court());
        var gltf = GltfJson(BuildingFiles.Lod(b, 0));
        var markers = Nodes(gltf).Where(n => NoorOf(n)?.TryGetProperty("marker", out _) == true).ToList();
        Assert.Equal(b.Markers.Count, markers.Count);
        var kinds = new List<string>();
        foreach (var m in markers)
        {
            var noor = NoorOf(m)!.Value;
            Assert.Equal(1, noor.GetProperty("v").GetInt32());
            Assert.Equal("entrance", noor.GetProperty("marker").GetString());
            Assert.False(string.IsNullOrEmpty(noor.GetProperty("id").GetString()));
            Assert.False(m.TryGetProperty("mesh", out _));                       // markers are empty nodes
            Assert.StartsWith("Marker_Entrance_", m.GetProperty("name").GetString());
            Assert.Equal(["noor"], m.GetProperty("extras").EnumerateObject().Select(p => p.Name));   // nothing left outside noor
            kinds.Add(noor.GetProperty("kind").GetString()!);
        }
        Assert.All(kinds, k => Assert.Contains(k, new[] { "main", "service", "gate", "hatch" }));
        Assert.Contains("main", kinds);
        Assert.Contains("gate", kinds);      // the passage into the yard
    }

    [Fact]
    public void Parts_use_the_spec_vocabulary_and_keep_their_old_extras_in_props()
    {
        var b = BuildingGenerator.Generate(Court());
        var gltf = GltfJson(BuildingFiles.Lod(b, 0));
        string[] vocabulary = ["wall", "floor", "ceiling", "roof", "window", "door", "stairs", "railing", "pillar", "trunk", "branch", "leaves", "hull", "deck", "detail", "terrain"];
        var parts = Nodes(gltf).Where(n => NoorOf(n)?.TryGetProperty("part", out _) == true).ToList();
        Assert.NotEmpty(parts);
        foreach (var n in parts)
        {
            var noor = NoorOf(n)!.Value;
            Assert.Contains(noor.GetProperty("part").GetString(), vocabulary);
            Assert.True(noor.GetProperty("props").TryGetProperty("type", out _));
            Assert.Equal(["noor"], n.GetProperty("extras").EnumerateObject().Select(p => p.Name));
        }
        // a wall keeps its exposure, a gate its state
        Assert.Contains(parts, n => NoorOf(n)!.Value.GetProperty("part").GetString() == "wall"
            && NoorOf(n)!.Value.GetProperty("props").TryGetProperty("exposure", out _));
        Assert.Contains(parts, n => NoorOf(n)!.Value.GetProperty("part").GetString() == "roof");
    }

    [Fact]
    public void Lod0_carries_the_mass_as_a_col_node_and_lod2_stays_the_plain_mass()
    {
        var b = BuildingGenerator.Generate(Court());
        var lod0 = Nodes(GltfJson(BuildingFiles.Lod(b, 0))).ToList();
        var col = lod0.Where(n => n.GetProperty("name").GetString()!.EndsWith("-col")).ToList();
        Assert.Single(col);
        Assert.Equal("mesh", NoorOf(col[0])!.Value.GetProperty("collision").GetString());
        Assert.True(col[0].TryGetProperty("mesh", out _));
        Assert.False(NoorOf(col[0])!.Value.TryGetProperty("part", out _));

        var lod2 = Nodes(GltfJson(BuildingFiles.Lod(b, 2))).ToList();
        Assert.DoesNotContain(lod2, n => n.GetProperty("name").GetString()!.EndsWith("-col"));
        Assert.Contains(lod2, n => NoorOf(n)?.GetProperty("props").TryGetProperty("type", out var t) == true && t.GetString() == "mass");
        Assert.Equal(2, NoorOf(lod2[0])!.Value.GetProperty("lod").GetInt32());
        // the markers and the collision only in the detailed file
        Assert.DoesNotContain(lod2, n => NoorOf(n)?.TryGetProperty("marker", out _) == true);
    }

    [Fact]
    public void The_asset_origin_is_the_bottom_centre_of_the_footprint()
    {
        var dir = TempDir();
        BuildingFiles.Save(BuildingGenerator.Generate(Court()), dir, "court", Created);
        var m = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "court.noor.json"))).RootElement;
        var min = m.GetProperty("bounds").GetProperty("min").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        var max = m.GetProperty("bounds").GetProperty("max").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        // the footprint (75..125, 175..225) is centred on the origin, the ground at y = 0 (foundations go a little below)
        Assert.Equal(0, (min[0] + max[0]) * 0.5f, 0.6f);
        Assert.Equal(0, (min[2] + max[2]) * 0.5f, 0.6f);
        Assert.InRange(min[1], -1f, 0f);
        var pivot = m.GetProperty("badbuilding").GetProperty("pivot_in_building_space").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        Assert.Equal([100f, 0f, 200f], pivot);
        // the GLB's root has scale 1 and no transform: top-level parts sit around the origin
        var root = Nodes(GltfJson(File.ReadAllBytes(Path.Combine(dir, "court.glb")))).First();
        Assert.False(root.TryGetProperty("scale", out _));
        Assert.False(root.TryGetProperty("translation", out _));
    }

    [Fact]
    public void The_glb_is_still_byte_identical_for_the_same_request()
    {
        Assert.Equal(BuildingFiles.Lod(BuildingGenerator.Generate(Court()), 0), BuildingFiles.Lod(BuildingGenerator.Generate(Court()), 0));
    }
}

/// <summary>
/// A JSON Schema (2020-12) checker for the keywords noor.asset.schema.json uses: type, required, properties, enum,
/// const, pattern, minLength, maxLength, minimum, minItems, maxItems, items, additionalProperties, $ref (local), and
/// format date-time. Errors start with the path ("space.up: ...").
/// </summary>
public static class MiniSchema
{
    public static List<string> Validate(JsonElement schema, JsonElement value)
    {
        var errors = new List<string>();
        Check(schema, schema, value, "", errors);
        return errors;
    }

    private static void Check(JsonElement root, JsonElement s, JsonElement v, string path, List<string> errors)
    {
        string At(string msg) => (path.Length == 0 ? "(root)" : path) + ": " + msg;
        if (s.TryGetProperty("$ref", out var r))
        {
            var target = root;
            foreach (var part in r.GetString()!.TrimStart('#', '/').Split('/')) target = target.GetProperty(part);
            Check(root, target, v, path, errors);
        }
        if (s.TryGetProperty("type", out var type))
        {
            bool ok = type.GetString() switch
            {
                "object" => v.ValueKind == JsonValueKind.Object,
                "array" => v.ValueKind == JsonValueKind.Array,
                "string" => v.ValueKind == JsonValueKind.String,
                "integer" => v.ValueKind == JsonValueKind.Number && (v.TryGetInt64(out _) || v.TryGetUInt64(out _)),
                "number" => v.ValueKind == JsonValueKind.Number,
                "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => true,
            };
            if (!ok) { errors.Add(At($"expected {type.GetString()}, got {v.ValueKind}")); return; }
        }
        if (s.TryGetProperty("const", out var c) && c.GetRawText() != v.GetRawText()) errors.Add(At($"must be {c.GetRawText()}"));
        if (s.TryGetProperty("enum", out var en) && !en.EnumerateArray().Any(e => e.GetRawText() == v.GetRawText())) errors.Add(At($"{v.GetRawText()} is not one of the allowed values"));
        if (v.ValueKind == JsonValueKind.String)
        {
            var str = v.GetString()!;
            if (s.TryGetProperty("pattern", out var pat) && !Regex.IsMatch(str, pat.GetString()!)) errors.Add(At($"'{str}' does not match {pat.GetString()}"));
            if (s.TryGetProperty("minLength", out var mn) && str.Length < mn.GetInt32()) errors.Add(At("too short"));
            if (s.TryGetProperty("maxLength", out var mx) && str.Length > mx.GetInt32()) errors.Add(At("too long"));
            if (s.TryGetProperty("format", out var f) && f.GetString() == "date-time"
                && !Regex.IsMatch(str, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")) errors.Add(At($"'{str}' is not a date-time"));
        }
        if (v.ValueKind == JsonValueKind.Number && s.TryGetProperty("minimum", out var min) && v.GetDouble() < min.GetDouble()) errors.Add(At("below the minimum"));
        if (v.ValueKind == JsonValueKind.Array)
        {
            int n = v.GetArrayLength();
            if (s.TryGetProperty("minItems", out var a) && n < a.GetInt32()) errors.Add(At($"needs at least {a.GetInt32()} items"));
            if (s.TryGetProperty("maxItems", out var b) && n > b.GetInt32()) errors.Add(At($"allows at most {b.GetInt32()} items"));
            if (s.TryGetProperty("items", out var items))
            {
                int i = 0;
                foreach (var item in v.EnumerateArray()) Check(root, items, item, $"{path}[{i++}]", errors);
            }
        }
        if (v.ValueKind == JsonValueKind.Object)
        {
            if (s.TryGetProperty("required", out var req))
                foreach (var name in req.EnumerateArray().Select(x => x.GetString()!))
                    if (!v.TryGetProperty(name, out _)) errors.Add(At($"missing '{name}'"));
            s.TryGetProperty("properties", out var props);
            foreach (var p in v.EnumerateObject())
            {
                var sub = path.Length == 0 ? p.Name : $"{path}.{p.Name}";
                if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty(p.Name, out var ps)) Check(root, ps, p.Value, sub, errors);
                else if (s.TryGetProperty("additionalProperties", out var ap))
                {
                    if (ap.ValueKind == JsonValueKind.False) errors.Add(At($"unexpected '{p.Name}'"));
                    else if (ap.ValueKind == JsonValueKind.Object) Check(root, ap, p.Value, sub, errors);
                }
            }
        }
    }
}
