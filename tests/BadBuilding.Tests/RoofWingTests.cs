using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using RoofShape = BadBuilding.Generation.RoofShape;

namespace BadBuilding.Tests;

/// <summary>Pitched roofs over L-, T- and other rectilinear footprints, in wings (D25).</summary>
public class RoofWingTests
{
    private static BuildingRequest House(Ring outer, string roof = "gabled", ulong seed = 3) => new()
    {
        Id = $"test:wings:{roof}:{seed}:{outer.Points.Count}", Seed = seed, Outer = outer, Type = "detached", Roof = roof,
        Height = 8.5f, Streets = [new StreetLine([new(-40, 20), new(40, 20)])],
    };

    // an L: a 14 x 8 wing along x and a 6 m wide wing going back from its east end (fills 72 % of its rectangle)
    private static readonly Ring L = new([new(-7, -4), new(7, -4), new(7, 10), new(1, 10), new(1, 4), new(-7, 4)]);
    // a T: a 16 x 8 bar with a 6 x 6 stem behind its middle
    private static readonly Ring T = new([new(-8, -4), new(8, -4), new(8, 4), new(3, 4), new(3, 10), new(-3, 10), new(-3, 4), new(-8, 4)]);

    [Theory]
    [InlineData("L", "gabled")]
    [InlineData("L", "hipped")]
    [InlineData("T", "gabled")]
    [InlineData("T", "hipped")]
    public void A_rectilinear_house_gets_a_pitched_roof_in_wings(string shape, string roof)
    {
        var outer = shape == "L" ? L : T;
        var b = BuildingGenerator.Generate(House(outer, roof));
        Assert.NotEqual(RoofShape.Flat, b.Plan!.RoofShape);
        Assert.Equal(2, b.Plan.RoofWings.Count);
        Assert.Contains(b.Report.Issues, i => i.Code == "roof.wings");
        // the roof stays over the footprint (plus the eaves) and under the mapped height
        var roofPts = b.Modules.Where(m => m.Type == "roof").SelectMany(m => m.Mesh.Positions).ToList();
        Assert.NotEmpty(roofPts);
        Assert.All(roofPts, p => Assert.True(p.Y <= 8.55f, $"roof at {p.Y:0.00}"));
        foreach (var p in roofPts)
        {
            var q = new Vector2(p.X, p.Z);
            float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
            Assert.True(d < 0.7f, $"roof point {q} is {d:0.00} m outside the footprint");
        }
    }

    [Fact]
    public void A_wing_that_meets_the_main_roof_has_no_hip_there()
    {
        var b = BuildingGenerator.Generate(House(T, "hipped"));
        var stem = b.Plan!.RoofWings.Single(w => w.Rect.HalfW < 3.5f);
        Assert.False(stem.OuterStart && stem.OuterEnd, "the stem runs into the bar's roof at one end");
        Assert.True(stem.Rise < b.Plan.RoofWings.Max(w => w.Rise), "the narrower wing's ridge is lower");
    }

    [Fact]
    public void A_footprint_with_slanted_corners_still_falls_back_to_flat()
    {
        var skew = new Ring([new(-7, -4), new(7, -4), new(7, 0), new(-1, 10), new(-7, 10)]);
        var b = BuildingGenerator.Generate(House(skew));
        Assert.Equal(RoofShape.Flat, b.Plan!.RoofShape);
    }
}
