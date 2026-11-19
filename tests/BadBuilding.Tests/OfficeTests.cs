using System.Numerics;
using BadBuilding.Generation;
using RoofShape = BadBuilding.Generation.RoofShape;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Offices and towers (D18) and parts that start in the air (D19).</summary>
public class OfficeTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(string type, float height, float? min = null, ulong seed = 5, int? year = null) => new()
    {
        Id = $"test:office:{type}:{height}:{min}:{seed}", Seed = seed, Outer = Rect(-20, -12, 20, 12), Type = type, Height = height, MinHeight = min,
        StartYear = year, Streets = [new StreetLine([new(-60, 25), new(60, 25)])],
    };

    private static IEnumerable<string> Roles(Building b) => b.Modules.SelectMany(m => m.Mesh.Groups.Keys).Distinct();

    [Theory]
    [InlineData("office", 18f, true)]
    [InlineData("commercial", 20f, true)]
    [InlineData("commercial", 8f, false)]
    [InlineData("yes", 120f, true)]
    [InlineData("yes", 20f, false)]
    public void Who_is_an_office(string type, float height, bool office)
        => Assert.Equal(office, BuildingGenerator.Generate(Req(type, height)).Plan!.Style == Office.Name);

    [Theory]
    [InlineData(1975, "glass")]       // grid or ribbon: windows of glass, no curtain
    [InlineData(2015, "curtain")]
    public void The_era_picks_the_facade(int year, string role)
    {
        var b = BuildingGenerator.Generate(Req("office", 30, year: year));
        Assert.Contains(role, Roles(b));
        if (year < 1990) Assert.DoesNotContain("curtain", Roles(b));
    }

    [Fact]
    public void An_office_has_a_lobby_door_on_the_street_and_plant_on_the_roof()
    {
        var b = BuildingGenerator.Generate(Req("office", 30));
        Assert.Contains(b.Markers, m => m.Kind == "entrance" && m.Direction.Z > 0.5f);   // the street is to the north (+z)
        Assert.Contains(b.Modules, m => m.Type == "plant");
    }

    [Fact]
    public void A_tall_tower_gets_a_mast()
    {
        var b = BuildingGenerator.Generate(Req("yes", 180));
        Assert.Contains(b.Modules, m => m.Type == "mast");
        Assert.True(b.Bounds().Max.Y > 180, "the mast stands over the roof");
    }

    [Fact]
    public void A_raised_part_starts_at_its_min_height_with_a_closed_underside()
    {
        var b = BuildingGenerator.Generate(Req("yes", 240, min: 193));
        Assert.True(b.Plan!.Datum >= 192.9f, $"floor at {b.Plan.Datum}");
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
                Assert.True(p.Y > 192f, $"{m.Id} reaches down to {p.Y:0.0}");
        Assert.Contains(b.Modules, m => m.Type == "soffit");
        Assert.DoesNotContain(b.Markers, m => m.Kind == "entrance");   // no lobby up there
    }

    [Fact]
    public void A_low_podium_part_does_not_blind_the_tower_over_it()
    {
        // a podium against the tower's east wall (x = 20): as a part of the same building 10 m high it leaves the
        // tower's wall glazed above it; as a separate building just as high it is a party wall (a bare firewall)
        var podium = Rect(20, -12, 50, 12);
        bool Glazed(Ring n)
        {
            var b = BuildingGenerator.Generate(Req("yes", 120, year: 2015) with { Neighbours = [n] });
            // the east wall's module: glazed with the curtain, or left bare as a party wall
            var east = b.Modules.Single(m => m.Type == "wall" && m.Mesh.Positions.All(p => MathF.Abs(p.X - 20) < 0.3f));
            return east.Mesh.Groups.ContainsKey("curtain");
        }
        Assert.True(Glazed(podium with { PartMin = 0, PartTop = 10 }), "the tower's wall over a low podium part");
        Assert.False(Glazed(podium), "a separate neighbour still makes a party wall");
    }

    [Fact]
    public void A_part_under_another_part_gets_a_flat_roof()
    {
        // the base of a tower mapped with a hipped roof, the tower standing on it from 8 m (Tom's dump on Fangora)
        var tower = Rect(-20, -12, 20, 12) with { PartMin = 8, PartTop = 26 };
        var b = BuildingGenerator.Generate(Req("yes", 8) with { Roof = "hipped", RoofHeight = 3.4f, Neighbours = [tower] });
        Assert.Equal(RoofShape.Flat, b.Plan!.RoofShape);
        Assert.Contains(b.Report.Issues, i => i.Code == "roof.capped");
    }

    [Fact]
    public void A_roof_only_part_stays_under_its_mapped_top_with_no_eaves()
    {
        // 25 to 32 m with a 7 m gabled roof, against another part of the building (Al. Jerozolimskie)
        var next = Rect(20, -12, 50, 12) with { PartMin = 0, PartTop = 32 };
        var b = BuildingGenerator.Generate(Req("yes", 32, min: 25) with { Roof = "gabled", RoofHeight = 7f, Neighbours = [next] });
        Assert.True(b.Bounds().Max.Y <= 32.05f, $"top at {b.Bounds().Max.Y:0.00}");
        Assert.Equal(0f, b.Plan!.Eaves);
        Assert.True(b.Bounds().Max.X <= 20.05f, "nothing pokes out past the footprint");
    }

    [Fact]
    public void A_raised_part_is_never_a_kamienica()
    {
        var b = BuildingGenerator.Generate(Req("apartments", 21, min: 15) with { Neighbours = [Rect(20, -12, 50, 12)] });
        Assert.NotEqual(Kamienica.Name, b.Plan!.Style);
    }
}
