using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Contemporary blocks of flats (D30, Noor's cookbook R025–R026, "deweloperka"): what Warsaw has built since the
/// nineties, until now drawn as PRL slabs. Rendered walls (white, light grey, warm white, beige, greige) with a
/// second finish on some bays or on the top floor (graphite panels, wood-look boards, brick slips); big windows and
/// French doors; balconies with glass railings standing out, or loggias cut into the wall; on the street side often
/// a glazed ground floor of shops and services; entrances under canopies; a flat roof behind a parapet.
/// When the build year isn't mapped (most of the time), the evidence is the shape: a deeper footprint, an irregular
/// outline and six to nine storeys lean contemporary; four, five, ten or eleven storeys on a plain 10–13 m slab lean
/// PRL (D30 in the decisions has the numbers).
/// </summary>
public sealed class Apartments : IStyle
{
    public const string Name = "apartments";
    public string Id => Name;

    private enum Accent { None, TimberColumns, GraphiteColumns, TopFloor, BrickBase }
    private Accent _accent;
    private bool _loggias, _shops;
    private int _pattern;   // balcony every nth bay

    public static bool Housing(string? t) => t is "apartments" or "residential" or "yes" or null or "dormitory";

    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (r.Raised || !Housing(r.Type) || c.Site.Small || c.Site.Depth < 9f || c.Site.Depth > 30f) return false;
        if (r.StartYear is { } y) return y >= 1992;
        if (r.Heritage) return false;
        int? lv = r.Levels ?? (r.Height is { } h and > 2f ? (int)MathF.Round((h - 1f) / 3f) : null);
        if (r.Type is "yes" or null && lv is null or < 4) return false;
        if (lv is < 3 or > 14) return false;
        if (r.Type is "apartments" or "residential" && lv is null && c.Site.Area < 300f) return false;
        // a walled-in building without a year stays a tenement's or a block's
        if (c.Site.Edges.Any(e => e.Exposure == Exposure.Party)) return false;
        return Rng.Stream(r.Seed, "apartments.era").Chance(ContemporaryOdds(c, lv));
    }

    /// <summary>How likely an undated block of flats is contemporary rather than PRL, from its shape.</summary>
    public static double ContemporaryOdds(BuildingGenerator.Context c, int? levels)
    {
        double s = 0.3;
        float depth = c.Site.Depth;
        if (depth > 14.5f) s += 0.25; else if (depth < 12.5f) s -= 0.1;
        if (Roofs.MinRect(c.Site.Outer).Fill < 0.86f) s += 0.2;
        if (levels is >= 6 and <= 9) s += 0.25;
        if (levels is 4 or 5 or 10 or 11) s -= 0.12;
        if (c.Site.Outer.Count > 10) s += 0.1;
        return Math.Clamp(s, 0.05, 0.9);
    }

    public float Plinth => 0.45f;
    public float RoofExtra => 0.6f;
    public int InferLevels(BuildingGenerator.Context c) => c.PlanRng.Weighted<int>([(4, 1), (5, 3), (6, 4), (7, 3), (8, 2)]);
    public float StoreyHeight(BuildingGenerator.Context c) => 3.0f;
    public string InferRoof(BuildingGenerator.Context c) => "flat";
    public float RoofPitch(BuildingGenerator.Context c) => 10f;

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        var wall = L.Weighted<(string, string)>([(("white", "eeede8"), 4), (("light grey", "d7d6d1"), 3), (("warm white", "ece6da"), 3), (("beige", "ddd3c0"), 2), (("greige", "c9c3b8"), 2)]);
        _accent = L.Weighted<Accent>([(Accent.TimberColumns, 3), (Accent.GraphiteColumns, 3), (Accent.TopFloor, 3), (Accent.BrickBase, 1), (Accent.None, 1)]);
        _loggias = L.Chance(0.4);
        _shops = L.Chance(0.55);
        _pattern = L.Weighted<int>([(1, 1), (2, 3), (3, 3)]);
        string accent = _accent switch
        {
            Accent.TimberColumns => L.Weighted<string>([("9a6a42", 2), ("7e5636", 2), ("b08050", 1)]),
            Accent.BrickBase => L.Weighted<string>([("8a4a35", 2), ("6e3f30", 1)]),
            _ => L.Weighted<string>([("4a4c50", 3), ("3a3c3f", 2), ("6a6c70", 1)]),
        };
        c.Plan = c.Plan with { Palette = $"contemporary, {wall.Item1}, {_accent switch { Accent.TimberColumns => "wood-look bays", Accent.GraphiteColumns => "graphite bays", Accent.TopFloor => "graphite top floor", Accent.BrickBase => "brick base", _ => "plain" }}, {(_loggias ? "loggias" : "balconies")}{(_shops ? ", shops" : "")}" };
        c.Building.Plan = c.Plan;
        m["wall"] = Kit.Mat("wall", wall.Item2, 0.9f);
        m["wall_alt"] = Kit.Mat("wall_alt", wall.Item2, 0.9f);
        string accentRole = AccentRole;
        m[accentRole] = Kit.Mat(accentRole, accent, accentRole == "timber" ? 0.8f : 0.7f);
        m["accent"] = Kit.Mat("accent", accent, 0.7f);
        m["plinth"] = Kit.Mat("plinth", L.Weighted<string>([("5e5f61", 2), ("8a8984", 1)]), 0.9f);
        m["reveal"] = Kit.Mat("reveal", wall.Item2, 0.9f);
        m["glass"] = Kit.Mat("glass", "26323a", 0.06f, 0.1f);
        m["balcony_door"] = Kit.Mat("balcony_door", "26323a", 0.06f, 0.1f);   // glass you can open (D35)
        m["shopfront"] = Kit.Mat("shopfront", "2f3d46", 0.05f, 0.1f);
        m["frame"] = Kit.Mat("frame", L.Weighted<string>([("3a3d40", 5), ("eceae4", 1)]), 0.5f);
        m["door"] = Kit.Mat("door", "3a3d40", 0.6f);
        m["canopy"] = Kit.Mat("canopy", "4a4c50", 0.6f);
        m["concrete"] = Kit.Mat("concrete", "b5b3ad", 0.9f);
        m["rail_glass"] = Kit.Mat("rail_glass", "cdd6d9", 0.1f, 0.1f);   // frosted, light: it reads as glass in front of a dark room
        m["railing"] = Kit.Mat("railing", "3a3d40", 0.4f, 0.7f);
        m["roof"] = Kit.Mat("roof", "5c5d5f", 0.9f);
        m["plant"] = Kit.Mat("plant", "8f9294", 0.6f, 0.4f);
    }

    private string AccentRole => _accent switch { Accent.TimberColumns => "timber", Accent.BrickBase => "brick", _ => "panel" };

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        const float bay = 3.1f;
        var doors = Entrances.Place(c, e => Math.Max(1, (int)(e.Length / 28f)), bay);
        string accent = AccentRole;

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Module? balconies = null;
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            bool blind = e.Exposure is Exposure.Party or Exposure.Sliver || len < 2f;
            if (blind) { Kit.Wall(wall.Mesh, "wall", f, 0, len, datum, top); continue; }
            bool street = e.Exposure == Exposure.Street;
            bool shopFloor = _shops && street && len >= 9f && e.Ring == 0;

            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bw = len / bays;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                // a column of the second finish: every fourth bay from the second, on long walls
                bool column = _accent is Accent.TimberColumns or Accent.GraphiteColumns && bays >= 4 && bi % 4 == 1;
                bool balconyBay = bays >= 2 && bi % _pattern == (_pattern == 1 ? 0 : 1) && bw >= 2.6f && !column;
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    bool topFloor = s == levels - 1 && levels >= 4;
                    string body = column || (topFloor && _accent == Accent.TopFloor) || (s == 0 && _accent == Accent.BrickBase) ? accent : "wall";
                    var door = doors.Where(d => d.Edge == ei && d.U >= u0 && d.U < u1).Select(d => (DoorSite?)d).FirstOrDefault();
                    if (s == 0 && door != null)
                    {
                        float hw = MathF.Min(0.9f, bw * 0.5f - 0.2f);
                        Kit.WallWithOpening(wall.Mesh, body, "reveal", "door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + 2.5f, 0.25f);
                        b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                        Kit.Box(trim.Mesh, "canopy", f, uc - hw - 0.6f, uc + hw + 0.6f, y0 + 2.75f, y0 + 2.9f, 0, 1.2f);
                        continue;
                    }
                    if (s == 0 && shopFloor)
                    {
                        // a shop or service unit: glass nearly from the pavement to the slab
                        Kit.WallWithOpening(wall.Mesh, body, "reveal", "shopfront", f, u0, u1, y0, y1, u0 + 0.15f, u1 - 0.15f, y0 + 0.1f, y1 - 0.45f, 0.2f);
                        Kit.Box(trim.Mesh, "frame", f, uc - 0.03f, uc + 0.03f, y0 + 0.1f, y1 - 0.45f, -0.17f, -0.13f);
                        b.Markers.Add(new Marker(c.Id($"shop.e{e.Ring}.{e.Index}.{bi}"), "shop_entrance", f.P(uc, y0, 0.05f), f.Out));
                        continue;
                    }
                    if (balconyBay && s > 0 || balconyBay && s == 0 && !street)
                    {
                        if (_loggias)
                        {
                            Loggia(wall.Mesh, trim.Mesh, f, body, u0, u1, y0, y1, uc, bw);
                            // where it is, for interiors (D36): the middle of its mouth on the facade, at the floor
                            b.Markers.Add(new Marker(c.Id($"loggia.e{e.Ring}.{e.Index}.{bi}.{s}"), "loggia", f.P(uc, y0, 0), f.Out, new Vector2(u1 - u0 - 0.5f, LoggiaDepth)));
                        }
                        else
                        {
                            float hw = MathF.Min(0.8f, bw * 0.3f);
                            // (no middle mullion: the door opens for someone walking out, D35)
                            Kit.WallWithOpening(wall.Mesh, body, "reveal", "balcony_door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + 2.4f, 0.15f);
                            if (s > 0) { balconies ??= b.Add(c.Id($"balconies.e{e.Ring}.{e.Index}"), "balconies", ModuleTier.Secondary, wall.Id); Balcony(balconies.Mesh, f, u0, u1, y0, bw); }
                            // a balcony (or on the ground floor, a door out to the garden: no depth)
                            b.Markers.Add(new Marker(c.Id($"balcony.e{e.Ring}.{e.Index}.{bi}.{s}"), "balcony", f.P(uc, y0, 0), f.Out, new Vector2(u1 - u0 - 0.2f, s > 0 ? 1.4f : 0f)));
                        }
                        continue;
                    }
                    // a big window; on the top floor in graphite a wider one
                    float ww = MathF.Min(topFloor ? 1.1f : 0.9f, bw * 0.34f), sill = topFloor ? 0.5f : 0.75f;
                    Kit.WallWithOpening(wall.Mesh, body, "reveal", "glass", f, u0, u1, y0, y1, uc - ww, uc + ww, y0 + sill, y0 + 2.45f, 0.15f);
                    Kit.Box(trim.Mesh, "frame", f, uc - 0.03f, uc + 0.03f, y0 + sill, y0 + 2.45f, -0.13f, -0.09f);
                    // a French balcony (a glass guard in front of the low window)
                    if (sill < 0.6f) Kit.Box(trim.Mesh, "rail_glass", f, uc - ww, uc + ww, y0 + sill, y0 + 1.0f, 0, 0.03f);
                }
            }
            // the slab edge over the ground floor, and the parapet's coping
            Kit.Box(trim.Mesh, "accent", f, 0, len, datum + sh - 0.12f, datum + sh + 0.08f, 0, 0.04f);
            Kit.Wall(wall.Mesh, _accent == Accent.TopFloor ? accent : "wall_alt", f, 0, len, top, top + 0.6f);
            Kit.Box(trim.Mesh, "railing", f, 0, len, top + 0.6f, top + 0.66f, -0.05f, 0.05f);
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.05f, sides: false);
        // plant on the roof: a lift overrun and a box or two of ventilation, inside the footprint
        var r = Roofs.MinRect(c.Site.Outer);
        int n = 1 + c.Detail.Int(0, 1);
        for (int k = 0; k < n; k++)
        {
            var at = r.At(r.HalfL * c.Detail.Range(-0.5f, 0.5f), r.HalfW * c.Detail.Range(-0.3f, 0.3f));
            float hx = c.Detail.Range(1.0f, 1.6f), hz = c.Detail.Range(1.0f, 1.6f);
            if (!Polygon.Contains(c.Site.Outer, at + new Vector2(hx, hz)) || !Polygon.Contains(c.Site.Outer, at - new Vector2(hx, hz))) continue;
            Kit.BoxXZ(roof.Mesh, k == 0 ? "wall_alt" : "plant", new Vector3(at.X - hx, top, at.Y - hz), new Vector3(at.X + hx, top + (k == 0 ? 2.2f : 1.2f), at.Y + hz));
        }
    }

    /// <summary>A balcony standing out 1.4 m: a slab, a glass railing on a slim metal top rail.</summary>
    private static void Balcony(Mesh m, Kit.Frame f, float u0, float u1, float y0, float bw)
    {
        float a = u0 + 0.1f, z = u1 - 0.1f;
        const float d = 1.4f;
        Kit.Box(m, "concrete", f, a, z, y0 - 0.2f, y0, 0, d);
        Kit.Box(m, "rail_glass", f, a, z, y0, y0 + 1.05f, d - 0.04f, d);
        Kit.Box(m, "rail_glass", f, a, a + 0.03f, y0, y0 + 1.05f, 0.05f, d);
        Kit.Box(m, "rail_glass", f, z - 0.03f, z, y0, y0 + 1.05f, 0.05f, d);
        Kit.Box(m, "railing", f, a, z, y0 + 1.05f, y0 + 1.1f, d - 0.05f, d + 0.01f);
    }

    /// <summary>A loggia cut 1.3 m into the wall: the window wall at the back, a glass guard at the front.</summary>
    public const float LoggiaDepth = 1.3f;

    private static void Loggia(Mesh wall, Mesh trim, Kit.Frame f, string body, float u0, float u1, float y0, float y1, float uc, float bw)
    {
        float a = u0 + 0.25f, z = u1 - 0.25f;
        // the glass wall at the back opens (D36): someone inside can step out into the loggia
        Kit.WallWithOpening(wall, body, body, "balcony_door", f, u0, u1, y0, y1, a, z, y0, y1 - 0.3f, LoggiaDepth);
        Kit.Box(trim, "rail_glass", f, a, z, y0, y0 + 1.05f, -0.08f, -0.05f);
        Kit.Box(trim, "railing", f, a, z, y0 + 1.05f, y0 + 1.1f, -0.09f, -0.04f);
    }
}
