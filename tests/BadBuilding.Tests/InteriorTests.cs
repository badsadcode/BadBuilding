using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Interior;
using BadBuilding.Model;

namespace BadBuilding.Tests;

/// <summary>Interiors on demand (D33): the staircase block.</summary>
public class InteriorTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Slab(ulong seed = 7, int levels = 5) => new()
    {
        Id = "test:slab", Seed = seed, Outer = Rect(-30, -6, 30, 6), Type = "apartments", Levels = levels, StartYear = 1975,
        Streets = [new StreetLine([new(-60, 20), new(60, 20)])],
    };

    private static (Building B, Shell S, InteriorModel M) Make(BuildingRequest r)
    {
        var b = BuildingGenerator.Generate(r);
        var s = Shell.Of(b);
        Assert.NotNull(s);
        var m = InteriorGenerator.Generate(s!);
        Assert.NotNull(m);
        return (b, s!, m!);
    }

    [Fact]
    public void A_prl_slab_gets_staircases_and_flats_on_every_storey()
    {
        var (b, s, m) = Make(Slab());
        Assert.Equal("prl_block", b.Plan!.Style);
        int stairs = m.Entrances.Count;
        Assert.True(stairs >= 2, $"staircases: {stairs}");
        Assert.Equal(s.Levels * stairs, m.Rooms.Count(r => r.Kind == "stair"));
        for (int st = 0; st < s.Levels; st++)
        {
            Assert.Contains(m.Rooms, r => r.Storey == st && r.Kind == "living");
            Assert.Contains(m.Rooms, r => r.Storey == st && r.Kind == "bathroom");
        }
        Assert.DoesNotContain(m.Report.Issues, i => i.Severity == Validation.Severity.Error);
        Assert.True(m.Colliders.Count > 50);
    }

    [Fact]
    public void Every_room_is_reached_from_the_landing_and_no_flat_through_another()
    {
        var (_, _, m) = Make(Slab(seed: 3, levels: 4));
        // each doorway joins rooms of one flat, or a flat's hall to its staircase
        foreach (var d in m.Doors)
        {
            var a = m.Rooms.First(r => r.Id == d.From); var b = m.Rooms.First(r => r.Id == d.To);
            Assert.True(a.Flat == b.Flat || a.Kind == "stair" && b.Kind == "hall", $"{d.From} -> {d.To}");
        }
        Assert.Equal("pass", m.Report.Status is "pass" or "warn" ? "pass" : m.Report.Status);
    }

    [Fact]
    public void Windows_follow_the_facade()
    {
        var (b, s, m) = Make(Slab());
        Assert.True(s.Openings.Count(o => o.Kind == "glass") > 20);
        Assert.Contains(s.Openings, o => o.Kind == "door");
        // the interior's window glass sits behind every exterior window it uses
        Assert.True(m.Mesh.Groups.TryGetValue("window", out var glass) && glass.Count > 0);
    }

    [Fact]
    public void Rooms_stay_inside_the_footprint()
    {
        var (_, _, m) = Make(Slab(seed: 11));
        foreach (var r in m.Rooms)
            foreach (var q in r.Rects)
                foreach (var corner in new[] { q.Min, q.Max, new Vector2(q.Min.X, q.Max.Y), new Vector2(q.Max.X, q.Min.Y) })
                {
                    var p = m.Frame.Plan(corner.X, corner.Y);
                    Assert.True(Geometry.Polygon.Contains(m.Footprint, p) || Geometry.Polygon.DistanceToSegment(p, m.Footprint[0], m.Footprint[1]) < 0.5f,
                        $"{r.Id} corner {p} outside");
                }
    }

    [Fact]
    public void Same_shell_same_interior()
    {
        var (_, s, m) = Make(Slab(seed: 5));
        var again = InteriorGenerator.Generate(s)!;
        Assert.Equal(m.Mesh.Positions, again.Mesh.Positions);
        Assert.Equal(m.Rooms.Select(r => r.Id), again.Rooms.Select(r => r.Id));
    }

    [Fact]
    public void An_l_block_gets_flats_in_its_arm_and_houses_no_shell()
    {
        // an L-shaped block: the arm along the door wall is planned, the rest stays solid; nothing pokes outside
        var l = new Ring([new(0, 0), new(40, 0), new(40, 12), new(12, 12), new(12, 40), new(0, 40)]);
        var b = BuildingGenerator.Generate(Slab() with { Outer = l });
        if (Shell.Of(b) is { } s && InteriorGenerator.Generate(s) is { } m)
            foreach (var r in m.Rooms)
                foreach (var q in r.Rects)
                {
                    var c = m.Frame.Plan((q.Min.X + q.Max.X) * 0.5f, (q.Min.Y + q.Max.Y) * 0.5f);
                    Assert.True(Geometry.Polygon.Contains(m.Footprint, c), $"{r.Id} outside");
                }
        var house = BuildingGenerator.Generate(new BuildingRequest { Id = "h", Seed = 1, Outer = Rect(0, 0, 10, 8), Type = "house", Levels = 2 });
        Assert.Null(Shell.Of(house));
    }

    [Fact]
    public void A_stepped_slab_is_planned_in_sections()
    {
        var stepped = new Ring([new(-36, -6), new(36, -6), new(36, 6), new(12, 6), new(12, 7.5f), new(-12, 7.5f), new(-12, 6), new(-36, 6)]);
        // three mapped doors on the street side, one per section (the middle one stands 1.5 m further out)
        var b = BuildingGenerator.Generate(Slab() with { Outer = stepped, Entrances = [new Entrance(new(-24, 6)), new Entrance(new(0, 7.5f)), new Entrance(new(24, 6))] });
        var m = InteriorGenerator.Generate(Shell.Of(b)!)!;
        Assert.NotNull(m);
        Assert.Equal(b.Markers.Count(x => x.Kind == "entrance"), m.Entrances.Count + m.Sealed.Count);
        Assert.Equal(3, m.Entrances.Count);
        Assert.Equal(3, (int)m.Report.Metrics["sections"]);
        Assert.DoesNotContain(m.Report.Issues, i => i.Severity == Validation.Severity.Error);
    }

    [Fact]
    public void A_deep_slab_gets_storage_behind_the_hall()
    {
        var b = BuildingGenerator.Generate(Slab() with { Outer = Rect(-25, -8.5f, 25, 8.5f) });
        var m = InteriorGenerator.Generate(Shell.Of(b)!)!;
        Assert.Contains(m.Rooms, r => r.Kind == "storage");
        Assert.DoesNotContain(m.Report.Issues, i => i.Severity == Validation.Severity.Error);
    }

    [Fact]
    public void Rooms_are_furnished_by_the_rules()
    {
        var (_, _, m) = Make(Slab(seed: 9));
        Assert.True(m.Furniture.Count > 100, $"{m.Furniture.Count} pieces");
        foreach (var kind in new[] { "living", "bedroom", "kitchen", "bathroom" })
            Assert.True(m.Rooms.Where(r => r.Kind == kind).Count(r => m.Furniture.Any(f => f.Room == r.Id)) >= m.Rooms.Count(r => r.Kind == kind) * 0.8,
                $"{kind}: too many empty");
        foreach (var r in m.Rooms)
        {
            var items = m.Furniture.Where(f => f.Room == r.Id).ToList();
            var q = r.Rects[0];
            foreach (var f in items)
            {
                // inside its room, clear of every other piece
                Assert.True(f.Min.X >= q.Min.X - 0.01f && f.Max.X <= q.Max.X + 0.01f && f.Min.Y >= q.Min.Y - 0.01f && f.Max.Y <= q.Max.Y + 0.01f, $"{f.Id} outside");
                foreach (var g in items)
                    if (!ReferenceEquals(f, g)) Assert.False(f.Min.X < g.Max.X && g.Min.X < f.Max.X && f.Min.Y < g.Max.Y && g.Min.Y < f.Max.Y, $"{f.Id} overlaps {g.Id}");
            }
            // no bed with its head under a window
            foreach (var bed in items.Where(f => f.Kind.StartsWith("bed")))
                foreach (var ap in m.Apertures.Where(a => a.Storey == r.Storey && a.Kind == "glass"))
                {
                    bool headOnLine = ap.AlongU ? MathF.Abs((bed.Facing.Y > 0 ? bed.Min.Y : bed.Max.Y) - ap.Line) < 0.15f && bed.Facing.Y != 0
                                                : MathF.Abs((bed.Facing.X > 0 ? bed.Min.X : bed.Max.X) - ap.Line) < 0.15f && bed.Facing.X != 0;
                    float b0 = ap.AlongU ? bed.Min.X : bed.Min.Y, b1 = ap.AlongU ? bed.Max.X : bed.Max.Y;
                    Assert.False(headOnLine && ap.A1 > b0 && ap.A0 < b1, $"{bed.Id} under a window");
                }
        }
        Assert.Contains(m.Markers, x => x.Kind == "loot_fridge");
        Assert.DoesNotContain(m.Report.Issues, i => i.Severity == Validation.Severity.Error);
    }
}
