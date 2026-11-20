using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;
using RoofShape = BadBuilding.Generation.RoofShape;

namespace BadBuilding.Tests;

/// <summary>The house family (D26): cottage, kostka, modern, semi-detached, terraced.</summary>
public class HouseTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(Ring outer, string type = "house", int? year = null, int? levels = null, ulong seed = 7, IReadOnlyList<Ring>? nb = null) => new()
    {
        Id = $"test:house:{type}:{year}:{levels}:{seed}", Seed = seed, Outer = outer, Type = type, StartYear = year, Levels = levels,
        Streets = [new StreetLine([new(-40, 15), new(40, 15)])], Neighbours = nb ?? [],
    };

    [Theory]
    [InlineData(1925, "Cottage")]
    [InlineData(1972, "Kostka")]
    [InlineData(2012, "Modern")]
    public void The_build_year_picks_the_family(int year, string family)
    {
        var b = BuildingGenerator.Generate(Req(year == 1925 ? Rect(-7, -3.5f, 7, 3.5f) : Rect(-5, -5, 5, 5), year: year));
        Assert.Equal(House.Name, b.Plan!.Style);
        Assert.StartsWith(family.ToLowerInvariant(), b.Plan.Palette);
    }

    [Fact]
    public void A_cottage_has_one_storey_a_steep_gable_and_small_windows()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-7, -3.5f, 7, 3.5f), year: 1925));
        Assert.Equal(1, b.Plan!.Levels.Value);
        Assert.Equal(RoofShape.Gabled, b.Plan.RoofShape);
        Assert.True(b.Plan.RoofRise > 2.8f, $"rise {b.Plan.RoofRise:0.0}");
        Assert.Contains(b.Modules, m => m.Type == "chimney");
    }

    [Fact]
    public void A_kostka_climbs_steps_to_a_raised_door_and_has_basement_windows()
    {
        var b = BuildingGenerator.Generate(Req(Rect(-5, -5, 5, 5), year: 1972));
        Assert.True(b.Plan!.Datum - b.Plan.GroundLow >= 0.9f, "a high plinth");
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("steps"));
        // glass below the floor: the basement windows
        Assert.Contains(b.Modules, m => m.Mesh.Groups.TryGetValue("glass", out var g) && g.Any(i => m.Mesh.Positions[i].Y < b.Plan.Datum - 0.1f));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("band"));
    }

    [Fact]
    public void The_door_of_a_semi_detached_half_keeps_off_the_shared_wall()
    {
        // the other half against the east wall (x = 5)
        var b = BuildingGenerator.Generate(Req(Rect(-5, -4, 5, 4), "semidetached_house", nb: [Rect(5, -4, 15, 4)]));
        Assert.Equal(House.Name, b.Plan!.Style);
        Assert.StartsWith("semidetached", b.Plan.Palette);
        var door = Assert.Single(b.Markers, m => m.Kind == "entrance");
        Assert.True(door.Position.X < 4.5f, $"door at {door.Position}");
        // the shared wall is blind: no glass on it
        Assert.DoesNotContain(b.Modules, m => m.Mesh.Groups.TryGetValue("glass", out var g) && g.Any(i => MathF.Abs(m.Mesh.Positions[i].X - 5) < 0.2f && m.Mesh.Normals[i].X > 0.9f));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Every_house_stays_within_its_footprint_and_its_details_within_a_metre(ulong seed)
    {
        var outer = new Ring([new(-7, -4), new(7, -4), new(7, 10), new(1, 10), new(1, 4), new(-7, 4)]);
        var b = BuildingGenerator.Generate(Req(outer, "detached", seed: seed));
        Assert.Equal(House.Name, b.Plan!.Style);
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out at {q}");
            }
    }

    [Fact]
    public void An_untyped_building_of_house_size_is_a_house_but_a_walled_in_one_is_not()
    {
        Assert.Equal(House.Name, BuildingGenerator.Generate(Req(Rect(-5, -4, 5, 4), "yes")).Plan!.Style);
        var walled = BuildingGenerator.Generate(Req(Rect(-5, -4, 5, 4), "yes", nb: [Rect(5, -4, 15, 4)]));
        Assert.NotEqual(House.Name, walled.Plan!.Style);
    }

    [Fact]
    public void Houses_come_in_variety()
    {
        // twenty seeds on the same plain rectangle: at least three families and four palettes
        var looks = Enumerable.Range(1, 20).Select(s => BuildingGenerator.Generate(Req(Rect(-6, -4.5f, 6, 4.5f), "detached", seed: (ulong)s)).Plan!.Palette).ToList();
        Assert.True(looks.Select(p => p.Split(',')[0]).Distinct().Count() >= 2, string.Join(" | ", looks));
        Assert.True(looks.Distinct().Count() >= 6, string.Join(" | ", looks));
    }

    [Fact]
    public void Three_mapped_levels_under_a_gable_are_two_floors_and_an_attic()
    {
        // Grądy, Sienkiewicza 7: building:levels=3, height 10, roof:height 4.2
        var b = BuildingGenerator.Generate(Req(Rect(-6, -4.5f, 6, 4.5f), levels: 3) with { Height = 10f, Roof = "gabled", RoofHeight = 4.2f });
        Assert.Equal(2, b.Plan!.Levels.Value);
        // a window up in the gable
        Assert.Contains(b.Modules, m => m.Type == "roof" && m.Mesh.Groups.TryGetValue("glass", out var g) && g.Any(i => m.Mesh.Positions[i].Y > b.Plan.Datum + 2 * b.Plan.StoreyHeight));
    }

    // ---- Podlasie (D40) ------------------------------------------------------------------------------------------------

    private static BuildingRequest Podlasie(ulong seed) => Req(Rect(-3.5f, -18, 3.5f, -6), levels: 1, seed: seed) with { Preset = House.Podlasie };

    [Fact]
    public void In_podlasie_most_long_one_storey_houses_are_wooden_cottages()
    {
        int cottages = 0, n = 40;
        for (ulong s = 1; s <= (ulong)n; s++)
        {
            var b = BuildingGenerator.Generate(Podlasie(s));
            if (b.Plan!.Palette.StartsWith("cottage, podlasie")) cottages++;
        }
        Assert.InRange(cottages, n * 6 / 10, n);
    }

    [Fact]
    public void Without_the_preset_cottages_stay_mazowsze_cottages()
    {
        for (ulong s = 1; s <= 20; s++)
            Assert.DoesNotContain("podlasie", BuildingGenerator.Generate(Podlasie(s) with { Preset = null }).Plan!.Palette);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    public void A_podlasie_house_keeps_its_details_within_a_metre_and_its_door_on_a_long_side(ulong seed)
    {
        var req = Podlasie(seed);
        var b = BuildingGenerator.Generate(req);
        var outer = req.Outer;
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out at {q}");
            }
        if (!b.Plan!.Palette.StartsWith("cottage, podlasie")) return;
        // the gable (a short wall, x = ±3.5 m) faces the street; the door is on a long wall (x = ±3.5)
        var door = b.Markers.First(k => k.Kind == "entrance");
        Assert.True(MathF.Abs(MathF.Abs(door.Position.X) - 3.5f) < 0.2f, $"door at {door.Position}");
        // the gable's dress: battens or barge boards in trim, and shutters or surrounds
        Assert.Contains(b.Modules, m => m.Type == "roof" && m.Mesh.Groups.ContainsKey("trim"));
        Assert.Contains(b.Modules, m => m.Mesh.Groups.ContainsKey("corner"));
    }
}
