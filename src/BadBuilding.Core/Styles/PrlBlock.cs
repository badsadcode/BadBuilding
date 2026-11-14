using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The PRL panel block (blok z wielkiej płyty), 1960s–80s Warsaw. Character (Noor §3): a regular panel grid with its
/// joints showing, one window per panel bay repeated storey after storey, balconies stacked in columns, a vertical strip
/// of glass blocks over each entrance where the staircase climbs, a canopy over every door, a flat roof with a low
/// parapet and a lift machine room above each staircase. Variation (palette, balcony rhythm, which bays get balconies)
/// never changes those rules, so a reroll is still recognisably a PRL block.
/// </summary>
public sealed class PrlBlock : IStyle
{
    public const string Name = "prl_block";
    public string Id => Name;
    public float Plinth => 0.9f;
    public float RoofExtra => 0.6f;
    private const float Bay = 3.6f, DefaultStorey = 2.8f;

    /// <summary>Apartments of 4+ storeys (or a typical slab footprint) on a footprint deep enough for flats.</summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        var t = c.Request.Type;
        bool housing = t is null or "yes" or "apartments" or "residential" or "dormitory";
        if (!housing || c.Site.Depth < 8f || c.Site.Depth > 22f) return false;
        if (c.Request.Levels is { } lv) return lv >= 4;
        // unmapped storeys: a long, deep footprint in a block of flats' size range
        return t is "apartments" or "residential" && c.Site.Area > 350f;
    }

    public int InferLevels(BuildingGenerator.Context c) => c.PlanRng.Weighted<int>([(4, 2), (5, 4), (10, 2), (11, 1)]);
    public float StoreyHeight(BuildingGenerator.Context c) => DefaultStorey;

    private static readonly (string Name, string Wall, string Alt, string Plinth, string Front)[] Palettes =
    [
        ("concrete", "9a9890", "8c8a83", "5f5d58", "b5b3ab"),       // bare 1970s panels
        ("thermo_cream", "e3d6b4", "c9b48c", "6f6a62", "d98d5f"),  // 2000s insulation, cream with terracotta balconies
        ("thermo_green", "d8dcc4", "9fb38a", "64665d", "7f9a6c"),
        ("thermo_blue", "d4dde3", "8ea6bc", "60656b", "6f8fae"),
        ("thermo_peach", "eed3bd", "d6a07f", "6c645d", "c27a58"),
    ];

    public void Materials(BuildingGenerator.Context c)
    {
        var p = c.Look.Weighted<(string, string, string, string, string)>(Palettes.Select((x, i) => (x, i == 0 ? 3.0 : 1.0)).ToList());
        c.Plan = c.Plan with { Palette = p.Item1 };
        c.Building.Plan = c.Plan;
        var m = c.Building.Materials;
        m["wall"] = Kit.Mat("wall", p.Item2, 0.9f);
        m["wall_alt"] = Kit.Mat("wall_alt", p.Item3, 0.9f);
        m["plinth"] = Kit.Mat("plinth", p.Item4, 0.95f);
        m["balcony_front"] = Kit.Mat("balcony_front", p.Item5, 0.85f);
        m["seam"] = Kit.Mat("seam", "4a4945", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "7c7a74", 0.9f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["glass_block"] = Kit.Mat("glass_block", "aebfc4", 0.35f);
        m["door"] = Kit.Mat("door", "3b3a36", 0.6f, 0.3f);
        m["concrete"] = Kit.Mat("concrete", "8b8984", 0.95f);
        m["roof"] = Kit.Mat("roof", "3a3a3a", 0.95f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float datum = plan.Datum, top = c.RoofLevel;
        // the storey height the plan settled on (a mapped height can stretch it)
        float Sh = plan.StoreyHeight;
        var doors = Entrances.Place(c, e => Math.Max(1, (int)MathF.Round(e.Length / 26f)), Bay);
        // balcony rhythm, one choice for the whole block: every bay, every other bay, or pairs
        int rhythm = c.Facade.Weighted<int>([(1, 1), (2, 3), (3, 1)]);
        bool groundBalconies = c.Facade.Chance(0.15);

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            string tag = $"e{e.Ring}.{e.Index}";
            var wall = b.Add(c.Id($"wall.{tag}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var detail = b.Add(c.Id($"detail.{tag}"), "panel_joints", ModuleTier.Tertiary, wall.Id, lods: 0b001);
            Module? balconies = null;

            // plinth, from below the lowest ground to the ground floor
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, plan.GroundLow - 0.5f, datum, 0.04f);
            if (len < 0.4f) { Kit.Wall(wall.Mesh, "wall", f, 0, len, datum, top); continue; }

            bool blank = e.Exposure is Exposure.Party;
            bool noWindows = blank || e.Exposure is Exposure.Sliver;
            int bays = Math.Max(1, (int)MathF.Round(len / Bay));
            float bw = len / bays;
            var stairBays = doors.Where(d => d.Edge == ei).Select(d => Math.Clamp((int)(d.U / bw), 0, bays - 1)).ToHashSet();
            bool balconyEdge = !noWindows && e.Exposure != Exposure.Courtyard && len >= 10f;

            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                bool stair = stairBays.Contains(bi);
                bool balcony = balconyEdge && !stair && bi > 0 && bi < bays - 1 && (bi % rhythm == 0) && !stairBays.Contains(bi - 1) && !stairBays.Contains(bi + 1);
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * Sh, y1 = y0 + Sh;
                    string role = (s % 2 == 0) ? "wall" : "wall";   // panels alike; seams make the grid
                    if (noWindows) { Kit.Wall(wall.Mesh, role, f, u0, u1, y0, y1); continue; }
                    if (stair)
                    {
                        if (s == 0)
                        {
                            // the entrance: a double door, a canopy, a step
                            Kit.WallWithOpening(wall.Mesh, role, "reveal", "door", f, u0, u1, y0, y1, uc - 0.8f, uc + 0.8f, y0, y0 + 2.3f, 0.25f);
                            Kit.Box(wall.Mesh, "concrete", f, uc - 1.3f, uc + 1.3f, y0 + 2.5f, y0 + 2.65f, 0, 1.4f);
                            Kit.Box(wall.Mesh, "concrete", f, uc - 1.0f, uc + 1.0f, plan.GroundLow - 0.3f, y0, 0, 1.2f);
                            var at = f.P(uc, y0, 0.05f);
                            b.Markers.Add(new Marker(c.Id($"entrance.{tag}.{bi}"), "entrance", at, f.Out));
                        }
                        else
                        {
                            // the staircase window sits half a storey down: the landing between floors
                            Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass_block", f, u0, u1, y0, y1, uc - 0.55f, uc + 0.55f, y0 - 0.5f + 0.9f, y0 + 1.7f, 0.12f);
                        }
                        continue;
                    }
                    if (balcony && (s > 0 || groundBalconies))
                    {
                        // balcony door and window: one wide opening down to the floor
                        Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass", f, u0, u1, y0, y1, uc - 1.25f, uc + 1.25f, y0 + 0.05f, y0 + 2.2f, 0.2f);
                        balconies ??= b.Add(c.Id($"balconies.{tag}"), "balconies", ModuleTier.Secondary, wall.Id);
                        float bu0 = uc - MathF.Min(1.6f, bw * 0.48f), bu1 = uc + MathF.Min(1.6f, bw * 0.48f);
                        Kit.Box(balconies.Mesh, "concrete", f, bu0, bu1, y0 - 0.15f, y0, 0, 1.25f);                 // slab
                        Kit.Box(balconies.Mesh, "balcony_front", f, bu0, bu1, y0, y0 + 1.0f, 1.15f, 1.25f);         // front
                        Kit.Box(balconies.Mesh, "balcony_front", f, bu0, bu0 + 0.08f, y0, y0 + 1.0f, 0, 1.15f);     // sides
                        Kit.Box(balconies.Mesh, "balcony_front", f, bu1 - 0.08f, bu1, y0, y0 + 1.0f, 0, 1.15f);
                        continue;
                    }
                    Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass", f, u0, u1, y0, y1, uc - 0.75f, uc + 0.75f, y0 + 0.9f, y0 + 2.35f, 0.2f);
                }
                // vertical panel joint at the bay's left edge
                if (bi > 0 && !blank) Kit.Box(detail.Mesh, "seam", f, u0 - 0.025f, u0 + 0.025f, datum, top, 0, 0.02f);
            }
            // horizontal joints at every floor line
            if (!blank)
                for (int s = 1; s < levels; s++)
                    Kit.Box(detail.Mesh, "seam", f, 0, len, datum + s * Sh - 0.025f, datum + s * Sh + 0.025f, 0, 0.02f);
            // the attic band above the top storey
            Kit.Wall(wall.Mesh, "wall_alt", f, 0, len, top, top + 0.15f);
        }

        // roof: slab, parapet, and a lift machine room behind each entrance
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.15f, sides: false);
        Kit.Parapet(roof.Mesh, "wall_alt", c.Site.Outer, top + 0.15f, top + 0.75f, 0.25f);
        foreach (var h in c.Site.Holes) Kit.Parapet(roof.Mesh, "wall_alt", h, top + 0.15f, top + 0.75f, 0.25f);
        foreach (var d in doors)
        {
            var e = plan.Edges[d.Edge];
            var f = Kit.Frame.Of(e);
            Kit.Box(roof.Mesh, "wall_alt", f, d.U - 1.4f, d.U + 1.4f, top + 0.15f, top + 2.6f, -4.6f, -1.4f);
            b.Markers.Add(new Marker(c.Id($"roof_access.{d.Edge}.{(int)d.U}"), "roof_access", f.P(d.U, top + 0.15f, -3f), Vector3.UnitY));
        }
    }
}
