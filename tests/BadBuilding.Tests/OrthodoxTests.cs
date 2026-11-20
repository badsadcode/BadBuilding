using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>The Orthodox church (D41): only on the map's word, Christian only, domes and crosses, within its footprint.</summary>
public class OrthodoxTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(ulong seed = 3, string? denomination = "orthodox", string? religion = "christian", string? preset = House.Podlasie, string type = "church") => new()
    {
        Id = $"test:orthodox:{seed}:{denomination}:{religion}", Seed = seed, Outer = Rect(-9, -4.5f, 9, 4.5f), Type = type,
        Religion = religion, Denomination = denomination, Preset = preset,
        Streets = [new StreetLine([new(-40, 15), new(40, 15)])],
    };

    [Theory]
    [InlineData("orthodox")]
    [InlineData("polish_orthodox")]
    [InlineData("russian_orthodox")]
    public void An_orthodox_church_is_a_cerkiew(string denomination)
    {
        var b = BuildingGenerator.Generate(Req(denomination: denomination));
        Assert.Equal(Orthodox.Name, b.Plan!.Style);
        Assert.StartsWith("orthodox church", b.Plan.Palette);
    }

    [Fact]
    public void Without_the_maps_word_a_church_is_not_a_cerkiew()
    {
        Assert.NotEqual(Orthodox.Name, BuildingGenerator.Generate(Req(denomination: null)).Plan!.Style);
        Assert.NotEqual(Orthodox.Name, BuildingGenerator.Generate(Req(denomination: "roman_catholic")).Plan!.Style);
    }

    [Fact]
    public void An_orthodox_synagogue_is_not_a_cerkiew()
    {
        // denomination=orthodox is also Orthodox Judaism
        var b = BuildingGenerator.Generate(Req(religion: "jewish", type: "synagogue"));
        Assert.NotEqual(Orthodox.Name, b.Plan!.Style);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void It_has_an_onion_and_a_cross_over_the_roof_and_stays_within_a_metre(ulong seed)
    {
        var req = Req(seed);
        var b = BuildingGenerator.Generate(req);
        var domes = b.Modules.Single(m => m.Type == "domes");
        Assert.True(domes.Mesh.Groups.ContainsKey("dome"));
        Assert.True(domes.Mesh.Groups.ContainsKey("cross"));
        float ridge = b.Modules.Where(m => m.Type == "roof").SelectMany(m => m.Mesh.Positions).Max(p => p.Y);
        float tip = domes.Mesh.Positions.Max(p => p.Y);
        Assert.True(tip > ridge + 2f, $"the dome's cross at {tip:0.0} m, the ridge at {ridge:0.0} m");
        var outer = req.Outer;
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                var q = new Vector2(p.X, p.Z);
                float d = Geometry.Polygon.Contains(outer.Points, q) ? 0 : outer.Points.Select((a, i) => Geometry.Polygon.DistanceToSegment(q, a, outer.Points[(i + 1) % outer.Points.Count])).Min();
                Assert.True(d <= 1.05f, $"{m.Id} reaches {d:0.00} m out at {q}");
            }
        // the door at the west end
        var door = b.Markers.First(k => k.Kind == "entrance");
        Assert.True(door.Position.X < -8f, $"door at {door.Position}");
    }

    [Fact]
    public void In_podlasie_most_are_wooden_and_the_same_seed_builds_the_same_church()
    {
        int wood = 0;
        for (ulong s = 1; s <= 30; s++)
            if (BuildingGenerator.Generate(Req(s)).Plan!.Palette.Contains("wooden")) wood++;
        Assert.InRange(wood, 15, 30);
        var a = BuildingGenerator.Generate(Req(7));
        var b = BuildingGenerator.Generate(Req(7));
        Assert.Equal(a.Plan!.Palette, b.Plan!.Palette);
        Assert.Equal(a.Modules.Sum(m => m.Mesh.TriangleCount), b.Modules.Sum(m => m.Mesh.TriangleCount));
    }
}
