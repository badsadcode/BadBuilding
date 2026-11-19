using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;
using RoofShape = BadBuilding.Generation.RoofShape;

namespace BadBuilding.Tests;

/// <summary>Garages, garage rows, allotment huts and shacks (D28).</summary>
public class GaragesTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    // the access road runs along the north side (+z)
    private static BuildingRequest Req(Ring outer, string type, ulong seed = 3) => new()
    {
        Id = $"test:garages:{type}:{seed}", Seed = seed, Outer = outer, Type = type,
        Streets = [new StreetLine([new(-60, 12), new(60, 12)], 3f)],
    };

    [Fact]
    public void A_garage_row_has_a_door_per_bay_facing_the_road()
    {
        // 24 m of garages, 6 m deep
        var b = BuildingGenerator.Generate(Req(Rect(-12, 0, 12, 6), "garages"));
        Assert.Equal(Garages.Name, b.Plan!.Style);
        Assert.StartsWith("garagerow", b.Plan.Palette);
        var doors = b.Markers.Where(m => m.Kind == "entrance").ToList();
        Assert.InRange(doors.Count, 7, 9);
        Assert.All(doors, d => Assert.True(d.Direction.Z > 0.9f, "the doors face the road"));
        Assert.Equal(RoofShape.Skillion, b.Plan.RoofShape);
        // the doors aren't all one colour
        var colours = b.Modules.SelectMany(m => m.Mesh.Groups.Keys).Where(k => k.StartsWith("door") && k.Length == 5).Distinct().Count();
        Assert.True(colours >= 2, $"{colours} door colours");
    }

    [Fact]
    public void A_single_garage_has_one_wide_door_toward_the_road()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-1.5f, 0, 1.5f, 5.5f), "garage"));
        Assert.StartsWith("garage,", b.Plan!.Palette);
        var door = Assert.Single(b.Markers, m => m.Kind == "entrance");
        Assert.True(door.Direction.Z > 0.9f);
        var infill = b.Modules.SelectMany(m => m.Mesh.Groups.TryGetValue("door", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.True(infill.Max(p => p.X) - infill.Min(p => p.X) >= 2f, "wide enough for a car");
    }

    [Fact]
    public void An_allotment_hut_has_a_door_and_a_window()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-2, 0, 2, 3), "allotment_house", seed: 5));
        Assert.StartsWith("allotment", b.Plan!.Palette);
        Assert.Single(b.Markers, m => m.Kind == "entrance");
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("glass"));
    }

    [Theory]
    [InlineData("shed", 1)]
    [InlineData("shed", 2)]
    [InlineData("hut", 3)]
    [InlineData("garage", 4)]
    [InlineData("garages", 5)]
    [InlineData("allotment_house", 6)]
    public void Small_buildings_stay_within_their_footprint(string type, ulong seed)
    {
        var outer = type == "garages" ? Rect(-9, 0, 9, 6) : Rect(-2, 0, 2, 3.5f);
        var b = BuildingGenerator.Generate(Req(outer, type, seed));
        Assert.Equal(Garages.Name, b.Plan!.Style);
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out");
            }
    }
}
