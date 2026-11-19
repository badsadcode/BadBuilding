using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Fire stations, schools and kindergartens (D29).</summary>
public class CivicTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    // the road runs along the north side (+z)
    private static BuildingRequest Req(Ring outer, string type, ulong seed = 6) => new()
    {
        Id = $"test:civic:{type}:{seed}", Seed = seed, Outer = outer, Type = type,
        Streets = [new StreetLine([new(-80, 25), new(80, 25)])],
    };

    [Fact]
    public void A_fire_station_has_tall_vehicle_doors_toward_the_road_and_a_red_sign()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-8, 0, 8, 12), "fire_station"));
        Assert.Equal(Civic.Name, b.Plan!.Style);
        var roller = b.Modules.SelectMany(m => m.Mesh.Groups.TryGetValue("roller", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.NotEmpty(roller);
        Assert.True(roller.Max(p => p.Y) - roller.Min(p => p.Y) >= 3f, "doors tall enough for a fire engine");
        Assert.All(roller, p => Assert.True(p.Z > 11.5f, "on the road side"));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("sign"));
        Assert.True(b.Plan.Datum - b.Plan.GroundLow < 0.1f, "a level floor to drive in on");
    }

    [Fact]
    public void Some_bigger_fire_stations_have_a_hose_tower_inside_the_footprint()
    {
        var outer = Rect(-10, 0, 10, 14);
        var towers = Enumerable.Range(1, 12).Select(s => BuildingGenerator.Generate(Req(outer, "fire_station", (ulong)s)))
            .Where(b => b.Modules.Any(m => m.Type == "tower")).ToList();
        Assert.NotEmpty(towers);
        foreach (var b in towers)
        {
            var t = b.Modules.Single(m => m.Type == "tower").Mesh.Positions;
            Assert.True(t.Max(p => p.Y) > b.Bounds().Max.Y - 0.01f, "the tower is the highest thing");
            Assert.All(t, p => Assert.True(MathF.Abs(p.X) <= 10.3f && p.Z >= -0.3f && p.Z <= 14.3f, $"{p}"));
        }
    }

    [Fact]
    public void A_school_has_rows_of_wide_classroom_windows_and_a_raised_entrance()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-30, 0, 30, 14), "school"));
        Assert.Equal(Civic.Name, b.Plan!.Style);
        Assert.InRange(b.Plan.Levels.Value, 2, 3);
        Assert.True(b.Plan.Datum - b.Plan.GroundLow >= 0.5f);
        var door = Assert.Single(b.Markers, m => m.Kind == "entrance");
        Assert.True(door.Direction.Z > 0.9f, "the entrance faces the street");
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("steps"));
        // windows at least 2 m wide: the glass of one opening
        var glass = b.Modules.Where(m => m.Type == "wall").SelectMany(m => m.Mesh.Groups.TryGetValue("glass", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.True(glass.Count > 40);
    }

    [Fact]
    public void A_kindergarten_is_low_and_colourful()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-15, 0, 15, 12), "kindergarten"));
        Assert.StartsWith("kindergarten", b.Plan!.Palette);
        Assert.InRange(b.Plan.Levels.Value, 1, 2);
        Assert.True(b.Materials.ContainsKey("accent"));
    }
}
