using System.Text.Json;
using System.Text.Json.Serialization;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Export;

/// <summary>
/// Everything one building writes: <c>.glb</c> (LOD0), <c>.lod1.glb</c>, <c>.lod2.glb</c> (the bare mass, also the
/// collision shape), <c>.recipe.json</c> (the request and every resolved choice with its source, D3),
/// <c>.metadata.json</c> (axes, bounds, edges and their exposure, markers, materials) and <c>.validation.json</c>.
/// </summary>
public static class BuildingFiles
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static byte[] Lod(Building b, int level) =>
        GlbWriter.Write(b, m => (m.Lods & (1 << level)) != 0, markers: level == 0);

    public static void Save(Building b, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        string P(string ext) => Path.Combine(directory, name + ext);
        File.WriteAllBytes(P(".glb"), Lod(b, 0));
        File.WriteAllBytes(P(".lod1.glb"), Lod(b, 1));
        File.WriteAllBytes(P(".lod2.glb"), Lod(b, 2));
        File.WriteAllText(P(".recipe.json"), JsonSerializer.Serialize(Recipe(b), Json));
        File.WriteAllText(P(".metadata.json"), JsonSerializer.Serialize(Metadata(b), Json));
        File.WriteAllText(P(".validation.json"), JsonSerializer.Serialize(Validation(b), Json));
    }

    public static object Recipe(Building b)
    {
        var r = b.Request;
        var p = b.Plan;
        return new
        {
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
            Bounds = new { Min = bounds.MinArray(), Max = bounds.MaxArray() },
            Edges = b.Plan?.Edges.Select(e => new { e.Ring, e.Index, A = new[] { e.A.X, e.A.Y }, B = new[] { e.B.X, e.B.Y }, Length = MathF.Round(e.Length, 2), e.Exposure }),
            Modules = b.Modules.Select(m => new { m.Id, m.Type, Tier = m.Tier, m.ParentId, m.Lods, Triangles = m.Mesh.TriangleCount }),
            Markers = b.Markers.Select(m => new { m.Id, m.Kind, Position = new[] { m.Position.X, m.Position.Y, m.Position.Z }, Direction = new[] { m.Direction.X, m.Direction.Y, m.Direction.Z } }),
            Materials = b.Materials.Values.Select(m => new { m.Role, Color = new[] { m.Color.X, m.Color.Y, m.Color.Z }, m.Roughness, m.Metallic, m.Alpha }),
        };
    }

    public static object Validation(Building b) => new { b.Report.Status, b.Report.Issues, b.Report.Metrics };
}
