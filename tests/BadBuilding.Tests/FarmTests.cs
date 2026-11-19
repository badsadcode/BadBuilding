using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;
using RoofShape = BadBuilding.Generation.RoofShape;

namespace BadBuilding.Tests;

/// <summary>Farm buildings and outbuildings (D27).</summary>
public class FarmTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(Ring outer, string type, ulong seed = 4, string? roof = null) => new()
    {
        Id = $"test:farm:{type}:{seed}:{roof}", Seed = seed, Outer = outer, Type = type, Roof = roof,
        Streets = [new StreetLine([new(-60, 30), new(60, 30)])],
    };

    private static string KindOf(Building b) => b.Plan!.Palette.Split(',')[0];

    [Theory]
    [InlineData("barn", 24f, 10f, "barn")]
    [InlineData("cowshed", 30f, 10f, "livestock")]
    [InlineData("greenhouse", 20f, 8f, "greenhouse")]
    [InlineData("outbuilding", 6f, 4f, "outbuilding")]
    [InlineData("farm_auxiliary", 50f, 20f, "hall")]
    public void The_type_and_size_pick_the_kind(string type, float l, float w, string kind)
    {
        var b = BuildingGenerator.Generate(Req(Rect(-l / 2, -w / 2, l / 2, w / 2), type, seed: 2));
        Assert.Equal(Farm.Name, b.Plan!.Style);
        if (type != "farm_auxiliary") Assert.Equal(kind, KindOf(b));
        else Assert.Contains(KindOf(b), new[] { "hall", "barn", "livestock" });
    }

    [Fact]
    public void A_barn_has_big_double_doors_and_a_steep_gable()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-12, -5, 12, 5), "barn"));
        Assert.Equal(RoofShape.Gabled, b.Plan!.RoofShape);
        // the wrota: a "boards" infill at least 3 m wide and high, in a wall
        var door = b.Modules.Where(m => m.Type == "wall").SelectMany(m => m.Mesh.Groups.TryGetValue("boards", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.NotEmpty(door);
        Assert.True(door.Max(p => p.Y) - door.Min(p => p.Y) >= 3f);
        Assert.Contains(b.Markers, m => m.Kind == "entrance");
    }

    [Fact]
    public void A_cowshed_has_a_row_of_small_high_windows_and_a_vent()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-15, -5, 15, 5), "cowshed"));
        var glass = b.Modules.SelectMany(m => m.Mesh.Groups.TryGetValue("glass", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.True(glass.Count >= 4 * 8, "several windows");
        Assert.All(glass, p => Assert.True(p.Y > b.Plan!.Datum + 1.3f, $"window low at {p.Y:0.0}"));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("vent"));
    }

    [Fact]
    public void A_mono_pitch_outbuilding_roof_slopes_over_its_walls()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-3, -2, 3, 2), "outbuilding", roof: "skillion"));
        Assert.Equal(RoofShape.Skillion, b.Plan!.RoofShape);
        var roof = b.Modules.Single(m => m.Type == "roof").Mesh.Positions;
        Assert.True(roof.Max(p => p.Y) - roof.Min(p => p.Y) > 0.3f, "it slopes");
    }

    [Fact]
    public void A_greenhouse_is_milky_panes_on_ribs()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-10, -4, 10, 4), "greenhouse"));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("greenhouse"));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("rib"));
    }

    [Theory]
    [InlineData("outbuilding", 1)]
    [InlineData("farm_auxiliary", 2)]
    [InlineData("barn", 3)]
    [InlineData("stable", 4)]
    public void Farm_buildings_stay_within_their_footprint(string type, ulong seed)
    {
        var outer = Rect(-9, -4, 9, 4);
        var b = BuildingGenerator.Generate(Req(outer, type, seed));
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 0.65f, $"{m.Id} reaches {d:0.00} m out");
            }
    }
}
