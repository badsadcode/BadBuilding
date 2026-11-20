using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>The mosque (D42): on the map's word; Kruszyniany's kind and Bohoniki's by the footprint; finials; within a
/// metre of the walls.</summary>
public class MosqueTests
{
    private static Ring Rect(float hx, float hz) => new([new(-hx, -hz), new(hx, -hz), new(hx, hz), new(-hx, hz)]);

    private static BuildingRequest Req(Ring outer, string type = "mosque", string? religion = "muslim", bool heritage = true, ulong seed = 5) => new()
    {
        Id = $"test:mosque:{type}:{religion}:{heritage}:{seed}", Seed = seed, Outer = outer, Type = type, Religion = religion, Heritage = heritage,
        Preset = House.Podlasie, Streets = [new StreetLine([new(-40, 15), new(40, 15)])],
    };

    [Fact]
    public void A_long_historic_mosque_is_kruszynianys_kind_and_a_square_one_bohonikis()
    {
        var l = BuildingGenerator.Generate(Req(Rect(8.5f, 6)));
        var s = BuildingGenerator.Generate(Req(Rect(7, 6.5f)));
        Assert.Equal(Mosque.Name, l.Plan!.Style);
        Assert.StartsWith("mosque, long", l.Plan.Palette);
        Assert.StartsWith("mosque, square", s.Plan!.Palette);
        Assert.Equal(RoofShape.Hipped, s.Plan.RoofShape);
    }

    [Fact]
    public void The_map_must_say_so()
    {
        Assert.Equal(Mosque.Name, BuildingGenerator.Generate(Req(Rect(7, 6.5f), religion: null)).Plan!.Style);   // building=mosque
        Assert.NotEqual(Mosque.Name, BuildingGenerator.Generate(Req(Rect(7, 6.5f), type: "church", religion: "christian")).Plan!.Style);
        Assert.NotEqual(Mosque.Name, BuildingGenerator.Generate(Req(Rect(7, 6.5f), type: "office")).Plan!.Style);
        Assert.NotEqual(Mosque.Name, BuildingGenerator.Generate(Req(Rect(7, 6.5f), type: "yes", religion: null)).Plan!.Style);
    }

    [Theory]
    [InlineData(8.5f, 6f, true)]
    [InlineData(7f, 6.5f, true)]
    [InlineData(17f, 9.5f, false)]
    public void It_has_finials_and_stays_within_a_metre(float hx, float hz, bool heritage)
    {
        var req = Req(Rect(hx, hz), heritage: heritage) with { Preset = heritage ? House.Podlasie : null };
        var b = BuildingGenerator.Generate(req);
        Assert.Equal(Mosque.Name, b.Plan!.Style);
        var domes = b.Modules.Single(m => m.Type == "domes");
        Assert.True(domes.Mesh.Groups.ContainsKey("finial"));
        Assert.True(domes.Mesh.Groups.ContainsKey("dome"));
        Assert.Contains(b.Markers, k => k.Kind == "entrance");
        var outer = req.Outer;
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out at {q}");
            }
    }

    [Fact]
    public void A_big_modern_mosque_has_a_minaret()
    {
        var b = BuildingGenerator.Generate(Req(Rect(17, 9.5f), heritage: false) with { Preset = null });
        Assert.StartsWith("mosque, modern", b.Plan!.Palette);
        float top = b.Modules.Single(m => m.Type == "domes").Mesh.Positions.Max(p => p.Y);
        Assert.True(top > b.Plan.Datum + 15f, $"the minaret's top at {top:0.0} m");
    }
}
