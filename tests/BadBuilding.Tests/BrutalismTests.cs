using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Brutalism and the "brutalist" preset (D32).</summary>
public class BrutalismTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Req(string type, ulong seed = 4, int? year = null, string? arch = null, string? preset = null, string? material = null, bool heritage = false) => new()
    {
        Id = $"test:brutal:{type}:{seed}:{year}:{arch}:{preset}:{material}:{heritage}", Seed = seed, Type = type, Levels = 6,
        Outer = Rect(-30, -9, 30, 9), StartYear = year, Architecture = arch, Preset = preset, Material = material, Heritage = heritage,
        Streets = [new StreetLine([new(-80, 25), new(80, 25)])],
    };

    [Fact]
    public void An_architecture_tag_makes_it_brutalist()
        => Assert.Equal(Brutalism.Name, BuildingGenerator.Generate(Req("yes", arch: "brutalism")).Plan!.Style);

    [Fact]
    public void Concrete_civic_buildings_of_the_seventies_are_often_brutalist_flats_never_without_the_preset()
    {
        int civic = Enumerable.Range(1, 30).Count(s => BuildingGenerator.Generate(Req("university", (ulong)s, 1974, material: "concrete")).Plan!.Style == Brutalism.Name);
        Assert.InRange(civic, 8, 26);
        Assert.DoesNotContain(Enumerable.Range(1, 15), s => BuildingGenerator.Generate(Req("apartments", (ulong)s, 1974)).Plan!.Style == Brutalism.Name);
    }

    [Fact]
    public void The_preset_turns_the_city_brutalist_but_keeps_listed_buildings_and_houses()
    {
        Assert.Equal(Brutalism.Name, BuildingGenerator.Generate(Req("apartments", preset: Brutalism.Preset)).Plan!.Style);
        Assert.NotEqual(Brutalism.Name, BuildingGenerator.Generate(Req("apartments", preset: Brutalism.Preset, heritage: true)).Plan!.Style);
        var house = Req("house", preset: Brutalism.Preset) with { Outer = Rect(-6, -4, 6, 4), Levels = 2 };
        Assert.NotEqual(Brutalism.Name, BuildingGenerator.Generate(house).Plan!.Style);
    }

    [Fact]
    public void Every_grammar_turns_up_and_stays_within_a_metre_of_the_footprint()
    {
        var seen = new HashSet<string>();
        for (ulong s = 1; s <= 40; s++)
        {
            var b = BuildingGenerator.Generate(Req("office", s, preset: Brutalism.Preset));
            Assert.Equal(Brutalism.Name, b.Plan!.Style);
            seen.Add(b.Plan.Palette.Split(',')[1].Trim());
            Assert.Contains(b.Markers, m => m.Kind == "entrance");
            foreach (var m in b.Modules)
                foreach (var p in m.Mesh.Positions)
                    Assert.True(MathF.Abs(p.X) <= 31.05f && MathF.Abs(p.Z) <= 10.05f, $"{b.Plan.Palette}: {m.Id} at {p}");
        }
        Assert.Equal(5, seen.Count);
    }
}
