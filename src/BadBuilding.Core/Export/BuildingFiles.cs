using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Export;

/// <summary>
/// Everything one building writes: <c>.glb</c> (LOD0, with the bare mass as a <c>-colonly</c> collision node),
/// <c>.lod1.glb</c>, <c>.lod2.glb</c> (the bare mass), <c>.recipe.json</c> (the asset id, seed and version, the request
/// and every resolved choice with its source, D3), <c>.metadata.json</c> (axes, bounds, edges and their exposure,
/// markers, materials, in building space), <c>.validation.json</c>, and the Noor Interchange manifest
/// <c>.noor.json</c> (D14).
/// </summary>
public static class BuildingFiles
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // a plain "+Y", not an escaped plus: these files are read by people too
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>One level of detail as a GLB; LOD0 also carries the markers and the collision mass.</summary>
    public static byte[] Lod(Building b, int level, Guid? assetId = null) =>
        GlbWriter.Write(b, m => (m.Lods & (1 << level)) != 0, markers: level == 0, lod: level, collision: level == 0, assetId: assetId);

    /// <summary>
    /// Writes the building's files. The asset id is the request's, else the one in a recipe already saved under this
    /// name (regenerating keeps the identity, spec section 4), else derived from the building id.
    /// <paramref name="created"/> goes into the manifest (default: now).
    /// </summary>
    public static Guid Save(Building b, string directory, string name, DateTimeOffset? created = null)
    {
        Directory.CreateDirectory(directory);
        string F(string ext) => name + ext;
        string P(string ext) => Path.Combine(directory, F(ext));
        var id = b.Request.AssetId ?? ReadAssetId(P(".recipe.json")) ?? Noor.AssetId(b.Request);
        var glbs = new[] { Lod(b, 0, id), Lod(b, 1, id), Lod(b, 2, id) };
        var recipe = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Recipe(b, id), Json));
        var validation = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Validation(b), Json));
        File.WriteAllBytes(P(".glb"), glbs[0]);
        File.WriteAllBytes(P(".lod1.glb"), glbs[1]);
        File.WriteAllBytes(P(".lod2.glb"), glbs[2]);
        File.WriteAllBytes(P(".recipe.json"), recipe);
        File.WriteAllText(P(".metadata.json"), JsonSerializer.Serialize(Metadata(b), Json));
        File.WriteAllBytes(P(".validation.json"), validation);
        var files = new List<Noor.FileEntry>
        {
            new(F(".glb"), "model", Noor.Sha256(glbs[0])),
            new(F(".recipe.json"), "recipe", Noor.Sha256(recipe)),
            new(F(".validation.json"), "validation", Noor.Sha256(validation)),
            new(F(".lod1.glb"), "lod", Noor.Sha256(glbs[1]), 1),
            new(F(".lod2.glb"), "lod", Noor.Sha256(glbs[2]), 2),
            new(F(".metadata.json"), "other", null),
        };
        int Tris(int level) => b.Modules.Where(m => (m.Lods & (1 << level)) != 0).Sum(m => m.Mesh.TriangleCount);
        var manifest = Noor.Manifest(b, id, name, files, F(".recipe.json"), Noor.Sha256(recipe), [Tris(0), Tris(1), Tris(2)],
            created ?? DateTimeOffset.UtcNow);
        File.WriteAllText(P(".noor.json"), JsonSerializer.Serialize(manifest, Json));
        return id;
    }

    /// <summary>The asset id stored in a recipe file, if the file exists and has one.</summary>
    public static Guid? ReadAssetId(string recipePath)
    {
        if (!File.Exists(recipePath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(recipePath));
            return doc.RootElement.TryGetProperty("id", out var v) && Guid.TryParse(v.GetString(), out var g) ? g : null;
        }
        catch (JsonException) { return null; }
    }

    public static object Recipe(Building b, Guid? assetId = null)
    {
        var r = b.Request;
        var p = b.Plan;
        return new
        {
            // Noor Interchange (D14): the identity, the seed and the version that regenerate this asset
            Id = Noor.Text(assetId ?? Noor.AssetId(r)), r.Seed, Version = BuildingGenerator.Version, NoorInterchange = Noor.Interchange,
            Generator = "BadBuilding", GeneratorVersion = BuildingGenerator.Version,
            Request = new { r.Id, r.Seed, r.Type, r.Levels, r.Height, r.Roof, r.HouseNumber, r.Style, r.FromOsm, StageSeeds = r.StageSeeds },
            Resolved = p == null ? null : new
            {
                p.Style,
                Levels = new { p.Levels.Value, Source = p.Levels.Source },
                Height = new { Value = MathF.Round(p.Height.Value, 2), Source = p.Height.Source },
                Roof = new { p.Roof.Value, Source = p.Roof.Source },
                Palette = new { Value = p.Palette, Source = Source.Inferred },
                StoreyHeight = MathF.Round(p.StoreyHeight, 2),
                Datum = MathF.Round(p.Datum, 2),
                GroundLow = MathF.Round(p.GroundLow, 2),
            },
        };
    }

    public static object Metadata(Building b)
    {
        var bounds = b.Bounds();
        return new
        {
            Axes = new { Up = "+Y", East = "+X", South = "+Z", Units = "meters" },
            // building (plan) space; the GLBs are in asset space = building space - pivot (D14)
            Pivot = new[] { Noor.Pivot(b).X, Noor.Pivot(b).Y, Noor.Pivot(b).Z },
            Bounds = new { Min = bounds.MinArray(), Max = bounds.MaxArray() },
            Edges = b.Plan?.Edges.Select(e => new { e.Ring, e.Index, A = new[] { e.A.X, e.A.Y }, B = new[] { e.B.X, e.B.Y }, Length = MathF.Round(e.Length, 2), e.Exposure }),
            Modules = b.Modules.Select(m => new { m.Id, m.Type, Tier = m.Tier, m.ParentId, m.Lods, Triangles = m.Mesh.TriangleCount }),
            Markers = b.Markers.Select(m => new { m.Id, m.Kind, Position = new[] { m.Position.X, m.Position.Y, m.Position.Z }, Direction = new[] { m.Direction.X, m.Direction.Y, m.Direction.Z } }),
            Materials = b.Materials.Values.Select(m => new { m.Role, Color = new[] { m.Color.X, m.Color.Y, m.Color.Z }, m.Roughness, m.Metallic, m.Alpha }),
        };
    }

    public static object Validation(Building b) => new { b.Report.Status, b.Report.Issues, b.Report.Metrics };
}
