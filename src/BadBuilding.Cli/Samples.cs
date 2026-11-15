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
                    StartYear = s.TryGetProperty("start_date", out var sd) && sd.ValueKind == JsonValueKind.String
                        && sd.GetString()!.Length >= 4 && int.TryParse(sd.GetString()![..4], out int yr) ? yr : null,
                    Heritage = s.TryGetProperty("heritage", out var he) && he.ValueKind == JsonValueKind.True,
                    Architecture = s.TryGetProperty("architecture", out var ar) && ar.ValueKind == JsonValueKind.String ? ar.GetString() : null,
                    Shops = s.TryGetProperty("shops", out var sh) ? sh.EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToList() : null,
                    ShopOnBuilding = s.TryGetProperty("shop_on_building", out var sb) && sb.ValueKind == JsonValueKind.True,
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
        // kamienice: the middle of a frontage, and a corner tenement round a small yard with neighbours on two sides
        var kl = R(-38, -14, -10, -14, -10, 0, -38, 0); var kr = R(10, -14, 34, -14, 34, 0, 10, 0);
        var front = new[] { new StreetLine(new List<Vector2> { new(-80, 8), new(80, 8) }) };
        IReadOnlyList<IReadOnlyList<Vector2>> FS = [new List<Vector2> { new(-80, 8), new(80, 8) }];
        yield return new("synthetic_kamienica_row", true, "tenement in a frontage: party walls both sides, street to the south",
            Req("syn:kam_row", R(-10, -14, 10, -14, 10, 0, -10, 0), "apartments", 5) with { Neighbours = [kl, kr], Streets = front }, FS);
        var corner = new List<IReadOnlyList<Vector2>> { new List<Vector2> { new(-80, 8), new(80, 8) }, new List<Vector2> { new(8, -80), new(8, 80) } };
        yield return new("synthetic_kamienica_corner", true, "corner tenement round a 10 × 10 m yard, neighbours north and west",
            Req("syn:kam_corner", R(-26, -26, 0, -26, 0, 0, -26, 0), "apartments", 5) with
            {
                Holes = [R(-18, -18, -18, -8, -8, -8, -8, -18)],
                Neighbours = [R(-26, -50, 0, -50, 0, -26, -26, -26), R(-50, -26, -26, -26, -26, 0, -50, 0)],
                Streets = corner.Select(p => new StreetLine(p)).ToArray(),
            }, corner);
        // roofs (D13): a house with a mapped gabled roof, a farm barn, a villa with a hipped roof
        yield return new("synthetic_house_gabled", true, "detached house, mapped gabled roof",
            Req("syn:house_g", R(-5, -4, 5, -4, 5, 4, -5, 4), "house", 2) with { Roof = "gabled", Streets = st }, S);
        yield return new("synthetic_barn", true, "farm barn, roof inferred",
            Req("syn:barn", R(-12, -5, 12, -5, 12, 5, -12, 5), "barn", 1) with { Streets = st }, S);
        yield return new("synthetic_villa_hipped", true, "villa, mapped hipped roof",
            Req("syn:villa", R(-8, -6, 8, -6, 8, 6, -8, 6), "house", 2) with { Roof = "hipped", Streets = st }, S);
        yield return new("synthetic_bowtie", true, "self-intersecting outline: must be rejected",
            Req("syn:bowtie", R(-10, -10, 10, 10, 10, -10, -10, 10), "yes", 2), S);
        yield return new("synthetic_tiny", true, "0.6 m² footprint: must be rejected",
            Req("syn:tiny", R(0, 0, 0.8f, 0, 0.8f, 0.8f, 0, 0.8f), "yes", 1), S);
        yield return new("synthetic_kiosk", true, "2.25 m² kiosk: a plain box",
            Req("syn:kiosk", R(0, 0, 1.5f, 0, 1.5f, 1.5f, 0, 1.5f), "yes", 1), S);
    }
}
