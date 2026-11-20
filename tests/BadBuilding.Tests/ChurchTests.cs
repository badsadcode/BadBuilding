using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Catholic and other Western churches (D43): the kinds, who is one, the apse, the tower, within a metre.</summary>
public class ChurchTests
{
    private static Ring Rect(float hx, float hz) => new([new(-hx, -hz), new(hx, -hz), new(hx, hz), new(-hx, hz)]);
    private static readonly Ring Apse = new([new(-15, -7), new(11, -7), new(15, -3), new(15, 3), new(11, 7), new(-15, 7)]);

    private static BuildingRequest Req(Ring outer, string type = "church", string? religion = "christian", string? denomination = "roman_catholic",
        int? year = null, string? material = null, ulong seed = 9) => new()
    {
        Id = $"test:church:{type}:{religion}:{denomination}:{year}:{material}:{seed}", Seed = seed, Outer = outer, Type = type,
        Religion = religion, Denomination = denomination, StartYear = year, Material = material,
        Streets = [new StreetLine([new(-40, 15), new(40, 15)])],
    };

    [Theory]
    [InlineData(1760, null, "baroque")]
    [InlineData(1985, null, "modern")]
    [InlineData(null, "wood", "wooden")]
    [InlineData(null, "brick", "gothic")]
    public void The_year_or_material_picks_the_kind(int? year, string? material, string kind)
    {
        var b = BuildingGenerator.Generate(Req(Rect(16, 7), year: year, material: material));
        Assert.Equal(Church.Name, b.Plan!.Style);
        Assert.StartsWith($"church, {kind}", b.Plan.Palette);
    }

    [Fact]
    public void A_small_one_is_a_chapel()
    {
        var b = BuildingGenerator.Generate(Req(Rect(3.5f, 2.5f), type: "chapel"));
        Assert.StartsWith("church, chapel", b.Plan!.Palette);
    }

    [Fact]
    public void Orthodox_mosques_and_synagogues_are_not_this_church()
    {
        Assert.Equal(Orthodox.Name, BuildingGenerator.Generate(Req(Rect(9, 4.5f), denomination: "orthodox")).Plan!.Style);
        Assert.NotEqual(Church.Name, BuildingGenerator.Generate(Req(Rect(9, 4.5f), type: "synagogue", religion: "jewish", denomination: "orthodox")).Plan!.Style);
        Assert.NotEqual(Church.Name, BuildingGenerator.Generate(Req(Rect(9, 4.5f), type: "religious", religion: "jewish", denomination: null)).Plan!.Style);
        // an untyped building is a church only when the map says it's Christian
        Assert.NotEqual(Church.Name, BuildingGenerator.Generate(Req(Rect(9, 4.5f), type: "yes", religion: null, denomination: null)).Plan!.Style);
    }

    [Theory]
    [InlineData(1905, false)]
    [InlineData(1760, false)]
    [InlineData(1985, false)]
    [InlineData(1910, true)]
    public void It_has_a_tower_with_a_cross_and_stays_within_a_metre(int year, bool apse)
    {
        var req = Req(apse ? Apse : Rect(16, 7), year: year);
        var b = BuildingGenerator.Generate(req);
        Assert.Equal(Church.Name, b.Plan!.Style);
        var tower = b.Modules.Single(m => m.Type == "tower");
        Assert.True(tower.Mesh.Groups.ContainsKey("cross"));
        float roofTop = b.Modules.Where(m => m.Type == "roof").SelectMany(m => m.Mesh.Positions).Max(p => p.Y);
        Assert.True(tower.Mesh.Positions.Max(p => p.Y) > roofTop + 3f);
        var door = b.Markers.First(k => k.Kind == "entrance");
        Assert.True(door.Position.X < -14f, $"door at {door.Position}");
        var outer = req.Outer;
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out at {q}");
            }
    }
}
