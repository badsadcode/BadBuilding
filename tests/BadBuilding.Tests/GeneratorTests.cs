using System.Numerics;
using BadBuilding.Export;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Tests;

public class GeneratorTests
{
    private static Ring Rect(float x0, float z0, float x1, float z1) => new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static BuildingRequest Block(string id = "test:block", ulong seed = 7) => new()
    {
        Id = id, Seed = seed, Outer = Rect(-30, -6, 30, 6), Type = "apartments", Levels = 5,
        Streets = [new StreetLine([new(-60, 20), new(60, 20)])],
    };

    [Fact]
    public void Same_request_gives_byte_identical_glb()
    {
        var a = BuildingFiles.Lod(BuildingGenerator.Generate(Block()), 0);
        var b = BuildingFiles.Lod(BuildingGenerator.Generate(Block()), 0);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_seeds_vary_the_look_but_keep_the_style()
    {
        var styles = Enumerable.Range(0, 12).Select(i => BuildingGenerator.Generate(Block(seed: (ulong)i)).Plan!).ToList();
        Assert.All(styles, p => Assert.Equal("prl_block", p.Style));
        Assert.True(styles.Select(p => p.Palette).Distinct().Count() > 1);
    }

    [Fact]
    public void Rerolling_detail_keeps_the_walls()
    {
        var a = BuildingGenerator.Generate(Block());
        var b = BuildingGenerator.Generate(Block() with { StageSeeds = new Dictionary<string, ulong> { ["look"] = 99 } });
        var wa = a.Modules.Where(m => m.Type == "wall").Select(m => m.Mesh.Positions.Count);
        var wb = b.Modules.Where(m => m.Type == "wall").Select(m => m.Mesh.Positions.Count);
        Assert.Equal(wa, wb);
    }

    [Fact]
    public void Every_attribute_says_where_it_came_from()
    {
        var p = BuildingGenerator.Generate(Block() with { Levels = null, Type = "apartments", Outer = Rect(-30, -6, 30, 6) }).Plan!;
        Assert.Equal(Source.Inferred, p.Levels.Source);
        Assert.Equal(Source.Inferred, p.Roof.Source);
        var q = BuildingGenerator.Generate(Block()).Plan!;
        Assert.Equal(Source.Osm, q.Levels.Source);
    }

    [Fact]
    public void Touching_neighbours_make_party_walls_without_windows()
    {
        var r = Block() with { Outer = Rect(-12, -6, 12, 6), Neighbours = [Rect(-36, -6, -12, 6), Rect(12, -6, 36, 6)] };
        var b = BuildingGenerator.Generate(r);
        var party = b.Plan!.Edges.Where(e => e.Exposure == Exposure.Party).ToList();
        Assert.Equal(2, party.Count);
        foreach (var e in party)
        {
            var wall = b.Modules.Single(m => m.Id == $"{r.Id}/wall.e{e.Ring}.{e.Index}");
            Assert.False(wall.Mesh.Groups.ContainsKey("glass"));
        }
        // a coordinate that merely comes close is not a party wall
        var corner = Block() with { Outer = Rect(-12, -6, 12, 6), Neighbours = [Rect(12.2f, 5.8f, 30, 20)] };
        Assert.DoesNotContain(BuildingGenerator.Generate(corner).Plan!.Edges, e => e.Exposure == Exposure.Party);
    }

    [Fact]
    public void A_wall_facing_a_narrow_gap_gets_no_windows()
    {
        var r = Block() with { Outer = Rect(-12, -6, 12, 6), Neighbours = [Rect(14.5f, -8, 30, 8)] };
        var b = BuildingGenerator.Generate(r);
        Assert.Contains(b.Plan!.Edges, e => e.Exposure == Exposure.Sliver);
    }

    [Fact]
    public void The_street_side_is_found()
    {
        var b = BuildingGenerator.Generate(Block());
        var south = b.Plan!.Edges.Single(e => e.A.Y > 5 && e.B.Y > 5);
        Assert.Equal(Exposure.Street, south.Exposure);
        Assert.Contains(b.Markers, m => m.Kind == "entrance" && m.Position.Z > 5);
    }

    [Fact]
    public void On_a_slope_the_ground_floor_sits_on_the_high_side_and_the_plinth_reaches_the_low_side()
    {
        var r = new BuildingRequest { Id = "test:slope", Seed = 1, Outer = Rect(-6, -5, 6, 5), Type = "house", Levels = 2, Ground = [3, 3, 0, 0] };
        var b = BuildingGenerator.Generate(r);
        Assert.True(b.Plan!.Datum >= 3f);
        Assert.True(b.Plan.GroundLow <= 0f);
        var lowest = b.Modules.Where(m => m.Type == "wall").SelectMany(m => m.Mesh.Positions).Min(p => p.Y);
        Assert.True(lowest <= 0f);
    }

    [Theory]
    [InlineData("tiny", new float[] { 0, 0, 0.8f, 0, 0.8f, 0.8f, 0, 0.8f }, "footprint.too_small")]
    [InlineData("bowtie", new float[] { -10, -10, 10, 10, 10, -10, -10, 10 }, "footprint.self_intersecting")]
    [InlineData("line", new float[] { 0, 0, 5, 0, 10, 0 }, "footprint.degenerate")]
    public void Bad_footprints_are_rejected_with_a_reason_and_no_geometry(string name, float[] xz, string code)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i + 1 < xz.Length; i += 2) pts.Add(new Vector2(xz[i], xz[i + 1]));
        var b = BuildingGenerator.Generate(new BuildingRequest { Id = name, Seed = 1, Outer = new Ring(pts) });
        Assert.Empty(b.Modules);
        Assert.Contains(b.Report.Issues, i => i.Code == code);
    }

    [Fact]
    public void A_kiosk_sized_footprint_becomes_a_plain_box()
    {
        var b = BuildingGenerator.Generate(new BuildingRequest { Id = "kiosk", Seed = 1, Outer = Rect(0, 0, 1.5f, 1.5f), Type = "yes" });
        Assert.NotEmpty(b.Modules);
        Assert.Contains(b.Report.Issues, i => i.Code == "footprint.small");
        Assert.DoesNotContain(b.Modules, m => m.Mesh.Groups.ContainsKey("glass"));
    }

    [Fact]
    public void A_mapped_height_stretches_the_storeys_and_the_walls_reach_the_roof()
    {
        var b = BuildingGenerator.Generate(Block() with { Levels = 10, Height = 40f });
        var wallTop = b.Modules.Where(m => m.Type == "wall").SelectMany(m => m.Mesh.Positions).Max(p => p.Y);
        Assert.True(wallTop >= 39f, $"walls end at {wallTop}");
    }

    [Fact]
    public void Nothing_is_built_outside_the_footprint_on_random_shapes()
    {
        var rng = new Rng(4242);
        for (int i = 0; i < 150; i++)
        {
            int n = rng.Int(3, 10);
            var pts = Enumerable.Range(0, n).Select(k =>
            {
                float a = k / (float)n * MathF.Tau, r = rng.Range(4, 40);
                return new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
            }).ToList();
            var b = BuildingGenerator.Generate(new BuildingRequest { Id = $"r{i}", Seed = (ulong)i, Outer = new Ring(pts), Levels = rng.Int(1, 12), Type = "apartments" });
            float minX = pts.Min(p => p.X) - 2, maxX = pts.Max(p => p.X) + 2, minZ = pts.Min(p => p.Y) - 2, maxZ = pts.Max(p => p.Y) + 2;
            foreach (var p in b.Modules.SelectMany(m => m.Mesh.Positions))
            {
                Assert.True(float.IsFinite(p.X + p.Y + p.Z));
                Assert.InRange(p.X, minX, maxX);
                Assert.InRange(p.Z, minZ, maxZ);
            }
        }
    }

    [Fact]
    public void Triangulation_covers_a_footprint_with_a_courtyard()
    {
        var outer = new List<Vector2> { new(-25, -25), new(25, -25), new(25, 25), new(-25, 25) };
        var hole = new List<Vector2> { new(-10, -10), new(-10, 10), new(10, 10), new(10, -10) };
        var tris = Polygon.Triangulate(outer, [hole]);
        var pts = Polygon.Concat(outer, [hole]);
        float area = 0;
        for (int t = 0; t + 2 < tris.Count; t += 3)
            area += Polygon.Area([pts[tris[t]], pts[tris[t + 1]], pts[tris[t + 2]]]);
        Assert.Equal(50 * 50 - 20 * 20, area, 1f);
    }

    private static BuildingRequest Court(float hole) => new()
    {
        Id = "test:court", Seed = 3, Outer = Rect(-25, -25, 25, 25), Holes = [new Ring([new(-hole, -hole), new(-hole, hole), new(hole, hole), new(hole, -hole)])],
        Type = "apartments", Levels = 5, Streets = [new StreetLine([new(-60, 40), new(60, 40)])],
    };

    [Fact]
    public void A_courtyard_gets_a_gate_from_the_street_and_a_door()
    {
        var b = BuildingGenerator.Generate(Court(13));
        var gate = b.Modules.Single(m => m.Type == "passage");
        Assert.Equal("gate", gate.Extras!["kind"]);
        // the gate opens from the street (south) side
        Assert.True(gate.Mesh.Positions.Max(p => p.Z) > 24.9f);
        Assert.Contains(b.Markers, m => m.Kind == "entrance" && MathF.Abs(m.Position.X) < 13.5f && MathF.Abs(m.Position.Z) < 13.5f);
        Assert.DoesNotContain(b.Report.Issues, i => i.Code.StartsWith("courtyard.unreachable") || i.Code == "courtyard.sealed");
        // the collision mass is open through the passage: its lowest band has the passage's corners
        var mass = b.Modules.Single(m => m.Type == "mass");
        Assert.Contains(mass.Mesh.Positions, p => p.Z > 24.9f && p.Y < 0.5f && MathF.Abs(MathF.Abs(p.X - gate.Mesh.Positions.Average(q => q.X)) - 1.5f) < 0.05f);
    }

    [Fact]
    public void A_light_well_gets_no_passage()
    {
        var b = BuildingGenerator.Generate(Court(2.5f));
        Assert.DoesNotContain(b.Modules, m => m.Type == "passage");
        Assert.Contains(b.Report.Issues, i => i.Code == "courtyard.light_well");
    }

    [Fact]
    public void Gates_are_a_mix_of_open_and_closed_and_a_closed_gate_keeps_the_yard_reachable()
    {
        var states = new HashSet<string>();
        for (ulong seed = 0; seed < 12; seed++)
        {
            var b = BuildingGenerator.Generate(Court(13) with { Seed = seed });
            var gate = b.Modules.Single(m => m.Type == "gate");
            states.Add((string)gate.Extras!["state"]);
            Assert.Single(b.Modules, m => m.Type == "passage");
            Assert.DoesNotContain(b.Report.Issues, i => i.Code is "courtyard.sealed" or "courtyard.unreachable");
            if ((string)gate.Extras["state"] == "closed") Assert.Contains(b.Markers, m => m.Kind == "wicket");
        }
        Assert.Equal(2, states.Count);
    }

    [Fact]
    public void A_narrow_shaft_is_not_a_courtyard_but_a_small_yard_with_a_mapped_door_is()
    {
        // 3 m × 30 m: 90 m², but a shaft
        var shaft = Court(13) with { Holes = [new Ring([new(-15, -1.5f), new(-15, 1.5f), new(15, 1.5f), new(15, -1.5f)])] };
        Assert.DoesNotContain(BuildingGenerator.Generate(shaft).Modules, m => m.Type == "passage");
        // 5 m × 5 m: 25 m², but a mapped entrance opens onto it
        var yard = Court(2.5f) with { Entrances = [new Entrance(new(0, -2.5f), "main")] };
        Assert.Single(BuildingGenerator.Generate(yard).Modules, m => m.Type == "passage");
    }

    [Fact]
    public void A_street_staircase_backing_onto_the_yard_gets_a_yard_door()
    {
        // a 9 m deep ring block: street doors on the south wall reach through to the yard
        var b = BuildingGenerator.Generate(Court(16));
        var yardDoors = b.Markers.Where(m => m.Kind == "entrance" && MathF.Abs(m.Position.Z - 16) < 0.5f && MathF.Abs(m.Position.X) < 16).ToList();
        var streetDoors = b.Markers.Where(m => m.Kind == "entrance" && m.Position.Z > 24.5f).ToList();
        Assert.NotEmpty(streetDoors);
        // the yard door lands in the yard wall's own bay grid, so within one bay of the street door
        Assert.All(streetDoors, s => Assert.Contains(yardDoors, y => MathF.Abs(y.Position.X - s.Position.X) < 3.6f));
    }

    private static BuildingRequest Tenement(ulong seed) => new()
    {
        Id = "test:kam", Seed = seed, Outer = Rect(-10, -14, 10, 0), Type = "apartments", Levels = 5,
        Neighbours = [Rect(-30, -14, -10, 0), Rect(10, -14, 30, 0)],
        Streets = [new StreetLine([new(-60, 8), new(60, 8)])],
    };

    [Fact]
    public void A_house_in_a_frontage_is_a_kamienica_with_a_dressed_front_and_a_plain_yard()
    {
        var b = BuildingGenerator.Generate(Tenement(1));
        Assert.Equal("kamienica", b.Plan!.Style);
        var edges = b.Plan.Edges;
        var front = edges.Single(e => e.Exposure == Exposure.Street);
        var back = edges.Single(e => e.A.Y < -13 && e.B.Y < -13);
        Assert.Equal(2, edges.Count(e => e.Exposure == Exposure.Party));
        // the next houses' front walls continue the frontage: they are not a gap in front of ours
        Assert.NotEqual(Exposure.Sliver, back.Exposure);
        Assert.True(b.Modules.Single(m => m.Id == $"test:kam/wall.e0.{front.Index}").Mesh.Groups.ContainsKey("glass"));
        Assert.True(b.Modules.Single(m => m.Id == $"test:kam/wall.e0.{back.Index}").Mesh.Groups.ContainsKey("glass"));
        Assert.Empty(b.Modules.Single(m => m.Id == $"test:kam/trim.e0.{back.Index}").Mesh.Positions);
    }

    [Fact]
    public void Pre_war_kamienice_are_not_all_equally_ornate()
    {
        var looks = Enumerable.Range(0, 16).Select(i => BuildingGenerator.Generate(Tenement((ulong)i) with { StartYear = 1905 }).Plan!.Palette).ToList();
        Assert.Contains(looks, p => p.Contains("ornament 0"));
        Assert.Contains(looks, p => p.Contains("ornament 2"));
        Assert.True(looks.Select(p => p.Split(',')[0]).Distinct().Count() >= 3);
    }

    [Fact]
    public void Without_age_evidence_a_tenement_is_never_rich_and_a_post_war_infill_is_no_kamienica()
    {
        var looks = Enumerable.Range(0, 24).Select(i => BuildingGenerator.Generate(Tenement((ulong)i)).Plan!.Palette).ToList();
        Assert.DoesNotContain(looks, p => p.Contains("ornament 2"));
        Assert.All(looks, p => Assert.Contains("no age evidence", p));
        Assert.NotEqual("kamienica", BuildingGenerator.Generate(Tenement(1) with { StartYear = 1962 }).Plan!.Style);
    }

    [Fact]
    public void Mapped_shops_decide_where_the_shop_fronts_go()
    {
        // one shop mapped at the west end of the front: only the bays near it are shops
        var b = BuildingGenerator.Generate(Tenement(3) with { StartYear = 1910, Shops = [new(-8, -1)] });
        var shops = b.Markers.Where(m => m.Kind == "shop_entrance").ToList();
        Assert.NotEmpty(shops);
        Assert.All(shops, m => Assert.True(m.Position.X < 0, $"shop at {m.Position.X}"));
    }

    [Fact]
    public void A_block_known_only_by_its_height_is_a_PRL_block()
    {
        // building=yes, no storeys, 16 storeys' worth of height: Tom's old tower kind
        var b = BuildingGenerator.Generate(Block() with { Type = "yes", Levels = null, Height = 46f });
        Assert.Equal("prl_block", b.Plan!.Style);
        Assert.InRange(b.Plan.Levels.Value, 15, 17);
        Assert.Equal(Source.Inferred, b.Plan.Levels.Source);
        // a low one stays plain
        Assert.NotEqual("prl_block", BuildingGenerator.Generate(Block() with { Type = "yes", Levels = null, Height = 7f }).Plan!.Style);
    }

    private static BuildingRequest House(string? roof = null, float? height = null) => new()
    {
        Id = "test:house", Seed = 5, Outer = Rect(-6, -4.5f, 6, 4.5f), Type = "house", Levels = 2, Roof = roof, Height = height,
        Streets = [new StreetLine([new(-30, 15), new(30, 15)])],
    };

    [Theory]
    [InlineData("gabled")]
    [InlineData("hipped")]
    public void A_house_gets_its_pitched_roof_with_the_ridge_along_the_long_side(string roof)
    {
        var b = BuildingGenerator.Generate(House(roof));
        Assert.Equal(roof == "gabled" ? RoofShape.Gabled : RoofShape.Hipped, b.Plan!.RoofShape);
        var pts = b.Modules.Single(m => m.Type == "roof").Mesh.Positions;
        float topY = pts.Max(p => p.Y);
        Assert.True(topY > b.Plan.Datum + 2 * b.Plan.StoreyHeight + 2.5f, $"ridge at {topY}");
        // the ridge runs east-west (the 12 m side), so the highest points spread along X, not Z
        var ridge = pts.Where(p => p.Y > topY - 0.01f).ToList();
        Assert.True(ridge.Max(p => p.X) - ridge.Min(p => p.X) >= ridge.Max(p => p.Z) - ridge.Min(p => p.Z));
        // eaves overhang a little, never far
        Assert.All(pts, p => Assert.True(MathF.Abs(p.X) <= 6.6f && MathF.Abs(p.Z) <= 5.1f, $"{p}"));
    }

    [Fact]
    public void An_unmapped_house_gets_a_pitched_roof_and_a_mapped_height_includes_it()
    {
        var b = BuildingGenerator.Generate(House());
        Assert.Contains(b.Plan!.RoofShape, new[] { RoofShape.Gabled, RoofShape.Hipped });
        Assert.Equal(Source.Inferred, b.Plan.Roof.Source);
        var mapped = BuildingGenerator.Generate(House("gabled", 9f));
        float top = mapped.Modules.SelectMany(m => m.Mesh.Positions).Max(p => p.Y);
        Assert.InRange(top, 8.5f, 9.6f);
    }

    [Fact]
    public void A_house_with_a_slanted_corner_falls_back_to_a_flat_roof_and_says_why()
    {
        // (an L-shaped one gets its roof in wings now, D25: RoofWingTests)
        var b = BuildingGenerator.Generate(House("gabled") with { Outer = new Ring([new(0, 0), new(14, 0), new(14, 4), new(6, 14), new(0, 14)]) });
        Assert.Equal(RoofShape.Flat, b.Plan!.RoofShape);
        Assert.Contains(b.Report.Issues, i => i.Code == "roof.complex_footprint");
    }

    [Fact]
    public void A_kamienica_roof_falls_from_the_street_to_the_yard_behind_the_attic()
    {
        var b = BuildingGenerator.Generate(Tenement(2) with { StartYear = 1900 });
        Assert.Equal(RoofShape.Skillion, b.Plan!.RoofShape);
        var roof = b.Modules.Single(m => m.Type == "roof").Mesh.Positions;
        float front = roof.Where(p => p.Z > -0.5f).Max(p => p.Y), back = roof.Where(p => p.Z < -13.5f).Max(p => p.Y);
        Assert.True(front > back + 0.5f, $"front {front}, back {back}");
        // the street attic hides the high edge
        var street = b.Plan.Edges.Single(e => e.Exposure == Exposure.Street);
        float attic = b.Modules.Single(m => m.Id == $"test:kam/wall.e0.{street.Index}").Mesh.Positions.Max(p => p.Y);
        Assert.True(attic >= front, $"attic {attic}, roof {front}");
    }

    [Fact]
    public void Glb_lods_get_lighter()
    {
        var b = BuildingGenerator.Generate(Block());
        var l0 = BuildingFiles.Lod(b, 0).Length;
        var l1 = BuildingFiles.Lod(b, 1).Length;
        var l2 = BuildingFiles.Lod(b, 2).Length;
        Assert.True(l0 > l1 && l1 > l2, $"{l0} {l1} {l2}");
    }
}
