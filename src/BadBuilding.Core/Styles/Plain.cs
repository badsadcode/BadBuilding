using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The fallback for everything the dedicated styles do not cover yet: plastered walls, a window per bay per storey on
/// open walls, one door, a flat roof with a low parapet. Outbuildings, garages and sheds get fewer, smaller openings and
/// no plinth. It stands in until houses, kamienice and shops get their own styles (roadmap, milestone 2).
/// </summary>
public sealed class Plain : IStyle
{
    public const string Name = "plain";
    public string Id => Name;
    public float Plinth => 0.3f;
    public float RoofExtra => 0.3f;

    private static bool Utility(string? t) => t is "outbuilding" or "farm_auxiliary" or "garage" or "garages" or "shed" or "barn"
        or "sty" or "stable" or "greenhouse" or "roof" or "hut" or "allotment_house" or "warehouse" or "industrial";

    public int InferLevels(BuildingGenerator.Context c)
    {
        var t = c.Request.Type;
        if (Utility(t)) return 1;
        if (t is "house" or "detached" or "semidetached_house" or "bungalow" or "terrace")
            return c.Site.Area > 90f ? c.PlanRng.Weighted<int>([(1, 1), (2, 3)]) : 1;
        if (t is "apartments" or "residential") return c.Site.Area > 250f ? 4 : 3;
        if (c.Site.Area < 25f) return 1;
        return c.Site.Area > 200f ? 2 : c.PlanRng.Weighted<int>([(1, 1), (2, 2)]);
    }

    public float StoreyHeight(BuildingGenerator.Context c) => c.Request.Type switch
    {
        "warehouse" or "industrial" => 6f,
        "retail" or "commercial" or "supermarket" => 4.2f,
        _ when Utility(c.Request.Type) => 2.8f,
        _ => 3.0f,
    };

    private static readonly (string Name, string Wall, string Roof)[] Palettes =
    [
        ("white", "e6e2d8", "4a4744"), ("cream", "e8dcbc", "5a4a42"), ("grey", "b4b2ac", "3f3f3f"),
        ("ochre", "d9b67a", "4f3b33"), ("brick", "8c4a36", "3c3633"), ("sage", "c9cfb4", "45443f"),
    ];

    public void Materials(BuildingGenerator.Context c)
    {
        bool utility = Utility(c.Request.Type);
        var p = c.Look.Pick(Palettes);
        if (utility && c.Look.Chance(0.5)) p = Palettes[4];
        c.Plan = c.Plan with { Palette = p.Name };
        c.Building.Plan = c.Plan;
        var m = c.Building.Materials;
        m["wall"] = Kit.Mat("wall", p.Wall, 0.9f);
        m["wall_alt"] = Kit.Mat("wall_alt", p.Wall, 0.9f);
        m["plinth"] = Kit.Mat("plinth", "6a655e", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "8a857c", 0.9f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["door"] = Kit.Mat("door", "4a3a2c", 0.7f);
        m["roof"] = Kit.Mat("roof", p.Roof, 0.95f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        bool utility = Utility(c.Request.Type);
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel;
        float bay = utility ? 4.5f : 3.2f;
        bool thin = c.Site.Depth < 2.2f || c.Site.Small;
        var doors = thin ? [] : Entrances.Place(c, _ => 1, bay);

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var cuts = c.Passages.Select(p => p.Cut(ei)).OfType<(float, float)>().ToList();
            var pass = c.Passages.FirstOrDefault(p => p.Cut(ei) != null);
            float cutLow = pass?.Floor ?? 0, cutHigh = pass?.Ceiling ?? 0;
            Kit.WallCut(wall.Mesh, "plinth", f, 0, len, plan.GroundLow - 0.5f, datum, cuts, cutLow, cutHigh);
            bool noWindows = thin || e.Exposure is Exposure.Party or Exposure.Sliver || len < 1.5f;
            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bw = len / bays;
            var doorBays = doors.Where(d => d.Edge == ei).Select(d => Math.Clamp((int)(d.U / bw), 0, bays - 1)).ToHashSet();
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    if (s == 0 && Kit.Overlaps(cuts, u0, u1)) { Kit.WallCut(wall.Mesh, "wall", f, u0, u1, y0, y1, cuts, cutLow, cutHigh); continue; }
                    if (s == 0 && doorBays.Contains(bi))
                    {
                        bool wide = c.Request.Type is "garage" or "garages";
                        float hw = wide ? MathF.Min(1.3f, bw * 0.45f) : 0.5f;
                        Kit.WallWithOpening(wall.Mesh, "wall", "reveal", "door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + (wide ? 2.2f : 2.1f), 0.15f);
                        b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                        continue;
                    }
                    if (noWindows || (utility && bi % 2 == 1) || bw < 1.4f) { Kit.Wall(wall.Mesh, "wall", f, u0, u1, y0, y1); continue; }
                    float ww = utility ? 0.45f : MathF.Min(0.65f, bw * 0.3f);
                    float sill = utility ? sh - 1.0f : 0.9f, head = utility ? sh - 0.4f : MathF.Min(sh - 0.4f, 2.3f);
                    Kit.WallWithOpening(wall.Mesh, "wall", "reveal", "glass", f, u0, u1, y0, y1, uc - ww, uc + ww, y0 + sill, y0 + head, 0.15f);
                }
            }
            Kit.Wall(wall.Mesh, "wall_alt", f, 0, len, top, top + 0.1f);
        }
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
        Kit.Parapet(roof.Mesh, "wall_alt", c.Site.Outer, top + 0.1f, top + 0.4f, 0.2f);
        foreach (var h in c.Site.Holes) Kit.Parapet(roof.Mesh, "wall_alt", h, top + 0.1f, top + 0.4f, 0.2f);
    }
}
