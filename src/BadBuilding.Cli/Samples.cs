using System.Numerics;
using System.Text.Json;
using BadBuilding.Model;

namespace BadBuilding.Cli;

/// <summary>A test footprint: real (from OSM, with context) or synthetic, with the case it stands for.</summary>
public sealed record Sample(string Case, bool Synthetic, string Note, BuildingRequest Request, IReadOnlyList<IReadOnlyList<Vector2>> StreetsForPlan);

/// <summary>
/// The difficult footprint set (docs/RECON_NOOR.md §6): real Warsaw buildings read from samples/warsaw.json
/// (tools/osm_samples.py) and synthetic stress shapes, clearly labelled.
/// </summary>
public static class Samples
{
    public static List<Sample> Load(string? path)
    {
        var list = new List<Sample>();
        if (path != null && File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var s in doc.RootElement.GetProperty("samples").EnumerateArray())
            {
                static List<Vector2> Ring(JsonElement e) => e.EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToList();
                string id = s.GetProperty("id").GetString()!;
                var streets = s.GetProperty("streets").EnumerateArray().Select(st => (IReadOnlyList<Vector2>)Ring(st.GetProperty("points"))).ToList();
                var req = new BuildingRequest
                {
                    Id = id,
                    Seed = Generation.Rng.Fnv1a(id),
                    Outer = new Ring(Ring(s.GetProperty("outer"))),
                    Holes = s.GetProperty("holes").EnumerateArray().Select(h => new Ring(Ring(h))).ToList(),
                    Type = s.GetProperty("type").GetString(),
                    Levels = s.GetProperty("levels").ValueKind == JsonValueKind.Number ? s.GetProperty("levels").GetInt32() : null,
                    Height = s.GetProperty("height").ValueKind == JsonValueKind.Number ? s.GetProperty("height").GetSingle() : null,
                    Roof = s.GetProperty("roof").GetString(),
                    HouseNumber = s.GetProperty("house_number").GetString(),
                    Neighbours = s.GetProperty("neighbours").EnumerateArray().Select(n => new Ring(Ring(n))).ToList(),
                    Streets = streets.Select(p => new StreetLine(p)).ToList(),
                    Entrances = s.GetProperty("entrances").EnumerateArray()
                        .Select(e => new Entrance(new Vector2(e.GetProperty("position")[0].GetSingle(), e.GetProperty("position")[1].GetSingle()), e.GetProperty("kind").GetString() ?? "main")).ToList(),
                };
                list.Add(new Sample(s.GetProperty("case").GetString()!, false, "real OSM footprint and context", req, streets));
            }
        }
        list.AddRange(Synthetic());
        return list;
    }

    private static Ring R(params float[] xz)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i + 1 < xz.Length; i += 2) pts.Add(new Vector2(xz[i], xz[i + 1]));
        return new Ring(pts);
    }

    private static BuildingRequest Req(string id, Ring outer, string? type = null, int? levels = null) => new()
    {
        Id = id, Seed = Generation.Rng.Fnv1a(id), Outer = outer, Type = type, Levels = levels, FromOsm = false,
    };

    /// <summary>Synthetic stress shapes, labelled as such in the gallery (Noor §6).</summary>
    public static IEnumerable<Sample> Synthetic()
    {
        var street = new List<Vector2> { new(-80, 20), new(80, 20) };
        IReadOnlyList<IReadOnlyList<Vector2>> S = [street];
        var st = new[] { new StreetLine(street) };
        yield return new("synthetic_slab", true, "long PRL slab, street to the south",
            Req("syn:slab", R(-60, -6, 60, -6, 60, 6, -60, 6), "apartments", 11) with { Streets = st }, S);
        yield return new("synthetic_l", true, "L-shaped block",
            Req("syn:l", R(-30, -6, 20, -6, 20, 30, 8, 30, 8, 6, -30, 6), "apartments", 5) with { Streets = st }, S);
        yield return new("synthetic_courtyard", true, "perimeter block with a courtyard hole",
            Req("syn:court", R(-25, -25, 25, -25, 25, 25, -25, 25), "apartments", 5) with { Holes = [R(-13, -13, -13, 13, 13, 13, 13, -13)], Streets = st }, S);
        yield return new("synthetic_wedge", true, "corner lot wedge with a 25° tip",
            Req("syn:wedge", R(-20, 10, 30, 10, -20, -13), "apartments", 4) with { Streets = st }, S);
        yield return new("synthetic_recess", true, "concave footprint with a deep recess",
            Req("syn:recess", R(-25, -10, -5, -10, -5, 4, 5, 4, 5, -10, 25, -10, 25, 10, -25, 10), "apartments", 5) with { Streets = st }, S);
        // three touching blocks: the middle one has two party walls
        var left = R(-36, -6, -12, -6, -12, 6, -36, 6); var right = R(12, -6, 36, -6, 36, 6, 12, 6);
        yield return new("synthetic_row", true, "middle of three touching blocks: both ends are party walls",
            Req("syn:row", R(-12, -6, 12, -6, 12, 6, -12, 6), "apartments", 5) with { Neighbours = [left, right], Streets = st }, S);
        yield return new("synthetic_sliver", true, "a neighbour 2.5 m away on the east: no windows into the gap",
            Req("syn:sliver", R(-12, -6, 12, -6, 12, 6, -12, 6), "apartments", 5) with { Neighbours = [R(14.5f, -8, 30, -8, 30, 8, 14.5f, 8)], Streets = st }, S);
        // a sloped lot: 3 m fall from north to south
        var house = R(-6, -5, 6, -5, 6, 5, -6, 5);
        yield return new("synthetic_slope", true, "house on a lot falling 3 m to the south",
            Req("syn:slope", house, "house", 2) with { Ground = [3, 3, 0, 0], Streets = st }, S);
        yield return new("synthetic_thin", true, "1.5 m wide strip: too thin for rooms",
            Req("syn:thin", R(-10, -0.75f, 10, -0.75f, 10, 0.75f, -10, 0.75f), "yes", 1), S);
        yield return new("synthetic_bowtie", true, "self-intersecting outline: must be rejected",
            Req("syn:bowtie", R(-10, -10, 10, 10, 10, -10, -10, 10), "yes", 2), S);
        yield return new("synthetic_tiny", true, "1 m² footprint: must be rejected",
            Req("syn:tiny", R(0, 0, 1, 0, 1, 1, 0, 1), "yes", 1), S);
    }
}
