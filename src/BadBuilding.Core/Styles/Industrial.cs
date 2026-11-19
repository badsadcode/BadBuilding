using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Factories, warehouses and works (D17, Tom: "mixed by age"). Three eras, from the mapped start year or, without one,
/// from the footprint's size and the seed:
/// * <b>brick</b> (before 1945): red brick, a bay grid of pilasters with tall windows, a cornice band, gabled or
///   saw-tooth roofs, a tall brick chimney on bigger works; small ones can have two or three storeys;
/// * <b>concrete</b> (1945–1990): grey panel halls with a column every 6 m, a high ribbon of windows, flat or
///   saw-tooth roofs;
/// * <b>metal</b> (after 1990): ribbed cladding over a concrete plinth, a high window ribbon on some, a flat or
///   shallow gabled roof: the logistics-park shed.
/// Concrete and metal halls get loading docks on their longest wall that doesn't face the street: roller doors 3 m
/// wide with a canopy over the run. Surface roles the game's building shader draws as patterns: <c>cladding</c>
/// (ribbed metal), <c>brick</c>, <c>roller</c> (roller doors).
/// </summary>
public sealed class Industrial : IStyle
{
    public const string Name = "industrial";
    public string Id => Name;
    public float Plinth => 0.15f;
    public float RoofExtra => 0.5f;

    public enum Era { Brick, Concrete, Metal }

    public static bool Fits(BuildingGenerator.Context c) => c.Request.Type is "industrial" or "warehouse" or "manufacture"
        or "factory" or "works" or "hangar" or "storage_tank_house" or "depot";

    /// <summary>The era, the same for every call on one building.</summary>
    public static Era EraOf(BuildingGenerator.Context c)
    {
        if (c.Request.StartYear is { } y) return y < 1945 ? Era.Brick : y < 1990 ? Era.Concrete : Era.Metal;
        var rng = Rng.Stream(c.Request.Seed, "industrial.era");
        float a = c.Site.Area;
        return a > 4000 ? rng.Weighted<Era>([(Era.Metal, 7), (Era.Concrete, 3)])
             : a > 800 ? rng.Weighted<Era>([(Era.Metal, 45), (Era.Concrete, 35), (Era.Brick, 20)])
             : rng.Weighted<Era>([(Era.Concrete, 40), (Era.Brick, 35), (Era.Metal, 25)]);
    }

    /// <summary>Saw-tooth roof (decided once): old brick and PRL concrete halls on a nearly rectangular plan, wide
    /// enough for at least two teeth.</summary>
    private static bool Sawtooth(BuildingGenerator.Context c)
    {
        var era = EraOf(c);
        if (era == Era.Metal || c.Site.Holes.Count > 0) return false;
        var r = Roofs.MinRect(c.Site.Outer);
        if (r.Fill < 0.88f || r.HalfL * 2 < 18 || r.HalfW * 2 < 10) return false;
        return Rng.Stream(c.Request.Seed, "industrial.sawtooth").Chance(era == Era.Brick ? 0.35 : 0.4);
    }

    public int InferLevels(BuildingGenerator.Context c)
    {
        if (EraOf(c) == Era.Brick && c.Site.Area < 1500) return c.PlanRng.Weighted<int>([(1, 2), (2, 2), (3, 1)]);
        return 1;
    }

    public float StoreyHeight(BuildingGenerator.Context c)
    {
        if (c.Request.Levels is > 1 || (c.Request.Levels is null && c.Request.Height is null && EraOf(c) == Era.Brick && c.Site.Area < 1500)) return 4.4f;
        return EraOf(c) switch { Era.Metal => 9f, Era.Concrete => 7.5f, _ => 7f };
    }

    public string InferRoof(BuildingGenerator.Context c)
    {
        if (Sawtooth(c)) return "flat";   // built by this style over the flat roof line
        var r = Roofs.MinRect(c.Site.Outer);
        bool rect = r.Fill > 0.85f && c.Site.Holes.Count == 0;
        return EraOf(c) switch
        {
            Era.Metal => rect ? c.PlanRng.Weighted<string>([("flat", 2), ("gabled", 3)]) : "flat",
            Era.Brick => rect ? c.PlanRng.Weighted<string>([("gabled", 2), ("flat", 1)]) : "flat",
            _ => "flat",
        };
    }

    public float RoofPitch(BuildingGenerator.Context c) => EraOf(c) == Era.Brick ? c.PlanRng.Range(18f, 25f) : c.PlanRng.Range(8f, 13f);

    public void Materials(BuildingGenerator.Context c)
    {
        var era = EraOf(c);
        var m = c.Building.Materials;
        string wall, roof;
        switch (era)
        {
            case Era.Metal:
                wall = c.Look.Weighted<string>([("c9ccce", 3), ("e2e4e2", 2), ("8f9aa3", 2), ("5d6d7e", 1), ("3f4a54", 1), ("b7c3cc", 1)]);
                roof = "6a6e70";
                m["cladding"] = Kit.Mat("cladding", wall, 0.55f, 0.35f);
                break;
            case Era.Concrete:
                wall = c.Look.Weighted<string>([("a9a69e", 3), ("bdb8ad", 2), ("8f8b84", 2), ("c4bfb2", 1)]);
                roof = "4a4a48";
                m["concrete"] = Kit.Mat("concrete", wall, 0.95f);
                break;
            default:
                // old factory brick: dark, brown-red, sooty (brighter reds read as plastic in the game's light)
                wall = c.Look.Weighted<string>([("5a3228", 3), ("4e2c24", 2), ("634030", 2), ("6b4a3a", 1)]);
                roof = c.Look.Weighted<string>([("3c3a38", 2), ("5a3a2e", 1), ("4c5254", 1)]);
                m["brick"] = Kit.Mat("brick", wall, 0.9f);
                break;
        }
        c.Plan = c.Plan with { Palette = $"{era.ToString().ToLowerInvariant()}-{wall}" };
        c.Building.Plan = c.Plan;
        m["wall"] = Kit.Mat("wall", wall, 0.9f);
        m["plinth"] = Kit.Mat("plinth", era == Era.Brick ? "5e4a40" : "8a8780", 0.95f);
        m["trim"] = Kit.Mat("trim", era == Era.Brick ? "b8a68c" : era == Era.Metal ? "4a5056" : "9a968e", 0.8f);
        m["reveal"] = Kit.Mat("reveal", "6e6a64", 0.9f);
        m["glass"] = Kit.Mat("glass", era == Era.Brick ? "34404a" : "4a5a66", 0.1f, 0.1f);
        m["door"] = Kit.Mat("door", "3c4248", 0.6f, 0.3f);
        m["roller"] = Kit.Mat("roller", c.Look.Weighted<string>([("b0b4b6", 3), ("6c7a88", 2), ("8a3a32", 1)]), 0.6f, 0.4f);
        m["roof"] = Kit.Mat("roof", roof, 0.9f);
        m["soffit"] = Kit.Mat("soffit", "5a5a58", 0.9f);
        m["chimney"] = Kit.Mat("chimney", "7e3e30", 0.95f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        var era = EraOf(c);
        string skin = era switch { Era.Metal => "cladding", Era.Concrete => "concrete", _ => "brick" };
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, wallH = top - datum;
        bool thin = c.Site.Depth < 3f || c.Site.Small;
        var doors = thin ? [] : Entrances.Place(c, e => e.Exposure == Exposure.Street ? 1 : 0, 4f);

        // the loading side: the longest open wall that doesn't face the street (any open wall if all do)
        int dockEdge = -1;
        if (era != Era.Brick && !thin && wallH >= 4.5f)
        {
            float best = 16f;
            foreach (bool street in new[] { false, true })
            {
                for (int i = 0; i < plan.Edges.Count; i++)
                {
                    var e = plan.Edges[i];
                    if (e.Exposure is Exposure.Party or Exposure.Sliver || (e.Exposure == Exposure.Street) != street) continue;
                    if (e.Length > best) { best = e.Length; dockEdge = i; }
                }
                if (dockEdge >= 0) break;
            }
        }

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            bool open = !(thin || e.Exposure is Exposure.Party or Exposure.Sliver || len < 2f);
            float plinthTop = era == Era.Metal ? datum + 0.8f : datum;
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, plan.GroundLow - 0.5f, plinthTop);
            var myDoors = doors.Where(d => d.Edge == ei).Select(d => d.U).ToList();

            if (era == Era.Brick) BrickWall(c, wall, f, e, len, open, myDoors, levels, sh, datum, top);
            else
            {
                // docks: a run of roller doors in the middle of the wall
                var rollers = new List<float>();
                if (ei == dockEdge)
                {
                    int n = Math.Clamp((int)((len - 6f) / 4.5f), 1, 10);
                    n = Math.Max(1, (int)(n * c.Look.Range(0.5f, 1f)));
                    float run = n * 4.5f, u0 = (len - run) * 0.5f;
                    for (int k = 0; k < n; k++) rollers.Add(u0 + 2.25f + k * 4.5f);
                }
                // the wall, in vertical strips round the doors
                var holes = rollers.Select(u => (U0: u - 1.5f, U1: u + 1.5f, Y1: plinthTop + 3.6f - (era == Era.Metal ? 0.8f : 0f), Role: "roller"))
                    .Concat(myDoors.Select(u => (U0: u - 0.55f, U1: u + 0.55f, Y1: datum + 2.2f, Role: "door")))
                    .OrderBy(h => h.U0).ToList();
                float y0 = plinthTop, ribbonLo = datum + wallH * 0.62f, ribbonHi = MathF.Min(top - 0.6f, ribbonLo + 1.1f);
                bool ribbon = open && len > 12 && wallH > 5 && (era == Era.Concrete || Rng.Stream(c.Request.Seed, "industrial.ribbon").Chance(0.6));
                float u = 0;
                foreach (var h in holes)
                {
                    if (h.U0 < u + 0.2f || h.U1 > len - 0.2f) continue;
                    SkinRun(wall.Mesh, skin, f, u, h.U0, y0, top, ribbon, ribbonLo, ribbonHi);
                    // the opening, with reveals, in a strip up to just above it; over that the wall carries on
                    float bottom = h.Role == "door" ? datum : y0, split = MathF.Min(top, h.Y1 + 0.4f);
                    Kit.WallWithOpening(wall.Mesh, skin, "reveal", h.Role, f, h.U0, h.U1, bottom, split, h.U0, h.U1, bottom, h.Y1, 0.2f);
                    SkinRun(wall.Mesh, skin, f, h.U0, h.U1, split, top, ribbon && split < ribbonLo, ribbonLo, ribbonHi);
                    b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{h.U0:0}"), h.Role == "door" ? "entrance" : "loading_dock", f.P((h.U0 + h.U1) * 0.5f, datum, 0.05f), f.Out));
                    u = h.U1;
                }
                SkinRun(wall.Mesh, skin, f, u, len, y0, top, ribbon, ribbonLo, ribbonHi);
                // a canopy over the docks
                if (rollers.Count > 0)
                {
                    var trim = b.Add(c.Id($"canopy.e{e.Ring}.{e.Index}"), "canopy", ModuleTier.Secondary, wall.Id);
                    float cy = plinthTop + 3.6f - (era == Era.Metal ? 0.8f : 0f) + 0.5f;
                    Kit.Box(trim.Mesh, "trim", f, rollers[0] - 2f, rollers[^1] + 2f, cy, cy + 0.25f, 0, 2.2f);
                }
                // concrete halls: a column every 6 m
                if (era == Era.Concrete && open)
                {
                    var cols = b.Add(c.Id($"columns.e{e.Ring}.{e.Index}"), "columns", ModuleTier.Secondary, wall.Id);
                    int nc = Math.Max(1, (int)(len / 6f));
                    for (int k = 0; k <= nc; k++)
                    {
                        float cu = k * len / nc;
                        if (holes.Any(h => cu > h.U0 - 0.3f && cu < h.U1 + 0.3f)) continue;
                        Kit.Box(cols.Mesh, "trim", f, MathF.Max(0, cu - 0.25f), MathF.Min(len, cu + 0.25f), datum, top, 0, 0.18f);
                    }
                }
            }
        }

        // ---- roof ----
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        if (plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal)
        {
            Roofs.Pitched(roof.Mesh, "roof", "soffit", skin, plan.RoofRect, plan.RoofShape, top, plan.RoofRise, (era == Era.Brick ? 0.3f : 0.15f) * plan.Eaves);
            Kit.Slab(roof.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
        }
        else
        {
            Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.05f, sides: false);
            float par = era == Era.Brick ? 0.7f : era == Era.Concrete ? 0.5f : 0.25f;
            Kit.Parapet(roof.Mesh, era == Era.Brick ? "trim" : skin, c.Site.Outer, top + 0.05f, top + par, era == Era.Metal ? 0.08f : 0.25f);
            if (Sawtooth(c)) SawtoothRoof(c, roof.Mesh, top + 0.05f);
        }

        // ---- a chimney on bigger brick works ----
        if (era == Era.Brick && c.Site.Area > 700 && Rng.Stream(c.Request.Seed, "industrial.chimney").Chance(0.5))
        {
            var r = Roofs.MinRect(c.Site.Outer);
            var rng = Rng.Stream(c.Request.Seed, "industrial.chimney.at");
            for (int tries = 0; tries < 6; tries++)
            {
                var p = r.At(rng.Range(-r.HalfL + 3, r.HalfL - 3), rng.Range(-r.HalfW + 2.5f, r.HalfW - 2.5f));
                if (!Inside(c.Site.Outer, p)) continue;
                var ch = b.Add(c.Id("chimney"), "chimney", ModuleTier.Secondary);
                float h = top + rng.Range(14f, 26f), w0 = rng.Range(1.1f, 1.5f);
                // tapered in three stages, with a darker cap
                for (int s = 0; s < 3; s++)
                {
                    float wa = w0 * (1f - s * 0.15f), ya = top - 1f + (h - top + 1f) * s / 3f, yb = top - 1f + (h - top + 1f) * (s + 1) / 3f;
                    Kit.BoxXZ(ch.Mesh, "chimney", new Vector3(p.X - wa, s == 0 ? plan.GroundLow : ya, p.Y - wa), new Vector3(p.X + wa, yb, p.Y + wa));
                }
                float wc = w0 * 0.78f;
                Kit.BoxXZ(ch.Mesh, "trim", new Vector3(p.X - wc, h, p.Y - wc), new Vector3(p.X + wc, h + 0.6f, p.Y + wc));
                b.Markers.Add(new Marker(c.Id("chimney.top"), "chimney", new Vector3(p.X, h + 0.6f, p.Y), Vector3.UnitY));
                break;
            }
        }
    }

    /// <summary>A run of hall wall from u0 to u1: skin, with a high ribbon of windows if wanted.</summary>
    private static void SkinRun(Mesh m, string skin, Kit.Frame f, float u0, float u1, float y0, float top, bool ribbon, float lo, float hi)
    {
        if (u1 - u0 < 0.05f) return;
        if (!ribbon || hi <= lo || y0 >= lo) { Kit.Wall(m, skin, f, u0, u1, y0, top); return; }
        // window panes 3 m wide with mullions, a margin at each end of the wall
        Kit.Wall(m, skin, f, u0, u1, y0, lo);
        Kit.Wall(m, skin, f, u0, u1, hi, top);
        float a = u0 + 0.6f, b = u1 - 0.6f;
        if (b - a < 1f) { Kit.Wall(m, skin, f, u0, u1, lo, hi); return; }
        Kit.Wall(m, skin, f, u0, a, lo, hi);
        Kit.Wall(m, skin, f, b, u1, lo, hi);
        int panes = Math.Max(1, (int)MathF.Round((b - a) / 3f));
        float pw = (b - a) / panes;
        for (int k = 0; k < panes; k++)
            Kit.WallWithOpening(m, skin, "reveal", "glass", f, a + k * pw, a + (k + 1) * pw, lo, hi, a + k * pw + 0.08f, a + (k + 1) * pw - 0.08f, lo + 0.06f, hi - 0.06f, 0.12f);
    }

    /// <summary>Old brick: bays 4.5 m wide between pilasters, a tall window per bay per storey, a cornice band.</summary>
    private static void BrickWall(BuildingGenerator.Context c, Module wall, Kit.Frame f, WallEdge e, float len, bool open,
        List<float> doorsAt, int levels, float sh, float datum, float top)
    {
        int bays = Math.Max(1, (int)MathF.Round(len / 4.5f));
        float bw = len / bays;
        var doorBays = doorsAt.Select(u => Math.Clamp((int)(u / bw), 0, bays - 1)).ToHashSet();
        for (int bi = 0; bi < bays; bi++)
        {
            float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
            for (int s = 0; s < levels; s++)
            {
                float y0 = datum + s * sh, y1 = s == levels - 1 ? top : y0 + sh;
                if (!open || bw < 2.2f) { Kit.Wall(wall.Mesh, "brick", f, u0, u1, y0, y1); continue; }
                if (s == 0 && doorBays.Contains(bi))
                {
                    Kit.WallWithOpening(wall.Mesh, "brick", "reveal", "door", f, u0, u1, y0, y1, uc - 1.2f, uc + 1.2f, y0, y0 + 3.2f, 0.3f);
                    c.Building.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                    continue;
                }
                float ww = MathF.Min(0.85f, bw * 0.3f), sill = levels > 1 ? 0.9f : 1.2f, head = (y1 - y0) - (levels > 1 ? 0.6f : 1.0f);
                Kit.WallWithOpening(wall.Mesh, "brick", "reveal", "glass", f, u0, u1, y0, y1, uc - ww, uc + ww, y0 + sill, y0 + head, 0.25f);
            }
        }
        if (!open) return;
        var det = c.Building.Add(c.Id($"pilasters.e{e.Ring}.{e.Index}"), "pilasters", ModuleTier.Secondary, wall.Id);
        for (int k = 0; k <= bays; k++)
        {
            float pu = k * bw;
            Kit.Box(det.Mesh, "brick", f, MathF.Max(0, pu - 0.3f), MathF.Min(len, pu + 0.3f), datum, top, 0, 0.18f);
        }
        Kit.Box(det.Mesh, "trim", f, 0, len, top - 0.55f, top - 0.25f, 0, 0.28f);   // cornice band
    }

    /// <summary>A saw-tooth roof over the footprint's rectangle: teeth across the hall every 8–10 m, each a sloped
    /// solid face and a vertical glazed one, closed at both ends.</summary>
    private static void SawtoothRoof(BuildingGenerator.Context c, Mesh m, float y)
    {
        var r = Roofs.MinRect(c.Site.Outer);
        float length = r.HalfL * 2;
        int teeth = Math.Max(2, (int)MathF.Round(length / 9f));
        float tw = length / teeth, rise = MathF.Min(3.2f, tw * 0.35f);
        Vector3 P(float u, float v, float h) { var q = r.At(u, v); return new Vector3(q.X, y + h, q.Y); }
        var up = new Vector3(r.U.X, 0, r.U.Y);
        var side = new Vector3(r.V.X, 0, r.V.Y);
        float vw = r.HalfW - 0.3f;
        for (int k = 0; k < teeth; k++)
        {
            float a = -r.HalfL + k * tw + 0.15f, b = a + tw - 0.3f;
            var slopeN = Vector3.Normalize(new Vector3(0, 1, 0) * (b - a) - up * rise);
            // the sloped face rising toward b, the glazed face dropping at b
            m.AddQuad("roof", P(a, -vw, 0), P(a, vw, 0), P(b, vw, rise), P(b, -vw, rise), slopeN);
            m.AddQuad("glass", P(b, -vw, rise), P(b, vw, rise), P(b, vw, 0), P(b, -vw, 0), up);
            // gable ends
            m.AddPolygon("roof", [P(a, -vw, 0), P(b, -vw, rise), P(b, -vw, 0)], -side);
            m.AddPolygon("roof", [P(a, vw, 0), P(b, vw, 0), P(b, vw, rise)], side);
        }
    }

    private static bool Inside(IReadOnlyList<Vector2> ring, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y) && p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                inside = !inside;
        return inside;
    }
}
