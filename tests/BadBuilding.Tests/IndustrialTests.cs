using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>The industrial style (D17): eras by start year, docks on the yard side, a chimney inside the footprint.</summary>
public class IndustrialTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Hall(int? year, float w = 80, float d = 40, ulong seed = 3, string type = "industrial") => new()
    {
        Id = $"test:hall:{year}:{seed}", Seed = seed, Outer = Rect(-w / 2, -d / 2, w / 2, d / 2), Type = type, StartYear = year,
        Streets = [new StreetLine([new(-120, d / 2 + 15), new(120, d / 2 + 15)])],
    };

    private static IEnumerable<string> Roles(Building b) => b.Modules.SelectMany(m => m.Mesh.Groups.Keys).Distinct();

    [Theory]
    [InlineData(1905, "brick")]
    [InlineData(1972, "concrete")]
    [InlineData(2008, "cladding")]
    public void The_start_year_picks_the_era(int year, string skin)
    {
        var b = BuildingGenerator.Generate(Hall(year));
        Assert.Equal(Industrial.Name, b.Plan!.Style);
        Assert.Contains(skin, Roles(b));
    }

    [Fact]
    public void Warehouses_and_factories_use_the_style()
    {
        foreach (var t in new[] { "warehouse", "manufacture", "factory" })
            Assert.Equal(Industrial.Name, BuildingGenerator.Generate(Hall(null, type: t)).Plan!.Style);
    }

    [Fact]
    public void A_modern_hall_gets_loading_docks_away_from_the_street()
    {
        var b = BuildingGenerator.Generate(Hall(2010));
        var docks = b.Markers.Where(m => m.Kind == "loading_dock").ToList();
        Assert.NotEmpty(docks);
        // the street runs along the north (+z); docks on the long south wall face away from it
        Assert.All(docks, m => Assert.True(m.Direction.Z < -0.5f, $"dock facing {m.Direction}"));
    }

    [Fact]
    public void A_chimney_stands_inside_the_footprint()
    {
        int found = 0;
        for (ulong s = 0; s < 30; s++)
        {
            var b = BuildingGenerator.Generate(Hall(1900, 50, 30, s));
            var top = b.Markers.FirstOrDefault(m => m.Kind == "chimney");
            if (top == null) continue;
            found++;
            Assert.InRange(top.Position.X, -25f, 25f);
            Assert.InRange(top.Position.Z, -15f, 15f);
            Assert.True(top.Position.Y > b.Plan!.Datum + 15f, "a chimney well above the hall");
        }
        Assert.True(found > 5, $"about half of big brick works have a chimney ({found}/30)");
    }

    [Fact]
    public void Exports_stay_in_the_spec_vocabulary()
    {
        // docks go out as yard-side entrances, the chimney top stays inside BadBuilding (D17, Noor Interchange v0.2)
        for (ulong s = 0; s < 20; s++)
        {
            var b = BuildingGenerator.Generate(Hall(1900, 50, 30, s));
            Assert.DoesNotContain(b.Markers.Where(Export.Noor.Exported), m => m.Kind == "chimney");
        }
        var hall = BuildingGenerator.Generate(Hall(2010));
        var dock = hall.Markers.First(m => m.Kind == "loading_dock");
        Assert.Equal(("entrance", "courtyard"), (Export.Noor.MarkerOf(hall, dock).Marker, Export.Noor.MarkerOf(hall, dock).Kind));
    }

    [Fact]
    public void Nothing_is_built_outside_the_footprint_except_canopies_and_parapets()
    {
        var b = BuildingGenerator.Generate(Hall(2010, 60, 30));
        foreach (var m in b.Modules.Where(m => m.Type is not ("canopy" or "mass")))
            foreach (var p in m.Mesh.Positions)
            {
                Assert.InRange(p.X, -30.5f, 30.5f);
                Assert.InRange(p.Z, -15.5f, 15.5f);
            }
    }
}
