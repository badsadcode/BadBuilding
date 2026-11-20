using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Styles;

namespace BadBuilding.Tests;

/// <summary>Socialist realism (D31).</summary>
public class SocRealismTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    // a perimeter block 80 x 60 with a courtyard, an avenue (half width 14 m) along the north (+z)
    private static BuildingRequest Block(ulong seed = 3, int? year = null, float avenue = 14f) => new()
    {
        Id = $"test:socrealism:{seed}:{year}:{avenue}", Seed = seed, Type = "apartments", Levels = 7, StartYear = year,
        Outer = Rect(-40, -30, 40, 30), Holes = [Rect(-26, -16, 26, 16)],
        Streets = [new StreetLine([new(-120, 48), new(120, 48)], avenue)],
    };

    [Fact]
    public void A_dated_block_of_1952_is_socialist_realism()
        => Assert.Equal(SocRealism.Name, BuildingGenerator.Generate(Block(year: 1952)).Plan!.Style);

    [Fact]
    public void Undated_perimeter_blocks_on_an_avenue_are_often_but_not_always()
    {
        int n = Enumerable.Range(1, 30).Count(s => BuildingGenerator.Generate(Block((ulong)s)).Plan!.Style == SocRealism.Name);
        Assert.InRange(n, 10, 27);
        // on a narrow street, never
        Assert.DoesNotContain(Enumerable.Range(1, 10), s => BuildingGenerator.Generate(Block((ulong)s, avenue: 4f)).Plan!.Style == SocRealism.Name);
    }

    [Fact]
    public void The_street_front_is_rich_and_the_courtyard_plain()
    {
        var b = BuildingGenerator.Generate(Block(year: 1952));
        Assert.Contains(b.Markers, m => m.Kind == "shop_entrance" && m.Direction.Z > 0.9f);
        // a cornice and an attic balustrade over the street front; nothing stone on the courtyard walls
        var stone = b.Modules.SelectMany(m => m.Mesh.Groups.TryGetValue("stone", out var g) ? g.Select(i => m.Mesh.Positions[i]) : []).ToList();
        Assert.Contains(stone, p => p.Z > 29.5f && p.Y > 24f);
        Assert.DoesNotContain(stone, p => MathF.Abs(p.X) < 25f && MathF.Abs(p.Z) < 15f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(8)]
    public void It_stays_within_its_footprint_but_for_the_cornice(ulong seed)
    {
        var b = BuildingGenerator.Generate(Block(seed, 1952));
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
                Assert.True(MathF.Abs(p.X) <= 40.85f && MathF.Abs(p.Z) <= 30.85f, $"{m.Id} at {p}");
    }
}
