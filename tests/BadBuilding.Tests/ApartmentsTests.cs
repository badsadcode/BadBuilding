using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Contemporary blocks of flats (D30).</summary>
public class ApartmentsTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(Ring outer, int? year, int levels = 6, ulong seed = 8) => new()
    {
        Id = $"test:apartments:{year}:{levels}:{seed}", Seed = seed, Outer = outer, Type = "apartments", Levels = levels, StartYear = year,
        Streets = [new StreetLine([new(-80, 20), new(80, 20)])],
    };

    [Fact]
    public void The_year_decides_between_contemporary_and_prl()
    {
        Assert.Equal(Apartments.Name, BuildingGenerator.Generate(Req(Rect(-25, -8, 25, 8), 2015)).Plan!.Style);
        Assert.Equal(PrlBlock.Name, BuildingGenerator.Generate(Req(Rect(-25, -6, 25, 6), 1978, 5)).Plan!.Style);
    }

    [Fact]
    public void Without_a_year_the_shape_leans_one_way_or_the_other()
    {
        // deep, seven storeys: mostly contemporary; a 12 m slab of five: mostly PRL
        int Count(Ring outer, int levels) => Enumerable.Range(1, 30).Count(s => BuildingGenerator.Generate(Req(outer, null, levels, (ulong)s)).Plan!.Style == Apartments.Name);
        Assert.True(Count(Rect(-25, -8, 25, 8), 7) >= 18);
        Assert.True(Count(Rect(-25, -6, 25, 6), 5) <= 8);
    }

    [Fact]
    public void A_contemporary_block_has_glass_railings_entrances_and_stays_near_its_footprint()
    {
        var outer = Rect(-25, -8, 25, 8);
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var b = BuildingGenerator.Generate(Req(outer, 2018, 6, seed));
            Assert.Contains(b.Markers, m => m.Kind == "entrance");
            Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("rail_glass"));
            foreach (var m in b.Modules)
                foreach (var p in m.Mesh.Positions)
                    Assert.True(MathF.Abs(p.X) <= 26.55f && MathF.Abs(p.Z) <= 9.55f, $"{m.Id} at {p}");
        }
    }
}
