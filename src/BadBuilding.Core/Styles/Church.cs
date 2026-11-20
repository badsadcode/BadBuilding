using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The Catholic (and any other Western) church (D43): a church, chapel, cathedral or monastery that is Christian or
/// says nothing, and isn't Orthodox (D41). Five kinds, from the build year and material when mapped, else by chance:
/// * <b>gothic</b> (neo-Gothic red brick, 1860–1939, Mazowsze's and Podlasie's commonest): a steep roof, a tall west tower
///   with a spire (two on a big one), pointed windows between buttresses, stone-coloured trims;
/// * <b>baroque</b> (plastered, before 1800 or a monastery): white, cream or yellow walls, pilasters, round-headed windows,
///   a west tower under a bulbous helmet with a lantern;
/// * <b>modern</b> (after 1945, or concrete): light walls, tall strip windows, a low roof and a tall slender tower with a
///   big cross;
/// * <b>wooden</b> (mapped as wood, and now and then in villages): dark boards under a dark roof, a boarded tower with a
///   tented roof;
/// * <b>chapel</b> (under 60 m²): plastered, a gabled roof, a small bell turret on the ridge, a cross.
/// Oriented east: the tower and the door at the west end. A footprint with an apse gets its pitched roof over the nave's
/// rectangle (trimmed until it fits) and a flat roof over the rest. Crosses are Latin.
/// </summary>
public sealed class Church : IStyle
{
    public const string Name = "church";
    public string Id => Name;

    public enum Kind { Gothic, Baroque, Modern, Wooden, Chapel }
    public Kind Look { get; }

    public Church(BuildingGenerator.Context c)
    {
        var r = c.Request;
        var rng = Rng.Stream(r.Seed, "church.kind");
        float area = c.Site.Area;
        Look = area < 60f ? Kind.Chapel
            : r.Material == "wood" ? Kind.Wooden
            : r.Material is "brick" ? Kind.Gothic
            : r.Material is "concrete" ? Kind.Modern
            : r.Type is "monastery" or "convent" ? Kind.Baroque
            : r.StartYear is { } y ? (y < 1800 ? Kind.Baroque : y < 1945 ? (rng.Chance(0.75) ? Kind.Gothic : Kind.Baroque) : Kind.Modern)
            : r.Preset == House.Podlasie
                ? rng.Weighted<Kind>([(Kind.Gothic, 45), (Kind.Baroque, 20), (Kind.Modern, 20), (Kind.Wooden, 15)])
                : rng.Weighted<Kind>([(Kind.Gothic, 50), (Kind.Baroque, 25), (Kind.Modern, 20), (Kind.Wooden, 5)]);
    }

    /// <summary>A Western church: a church-like type, Christian or no religion said, not Orthodox.</summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (r.Raised || c.Site.Holes.Count > 0 || Orthodox.IsOrthodox(r.Denomination)) return false;
        if (r.Religion is not (null or "christian")) return false;
        bool churchy = r.Type is "church" or "chapel" or "cathedral" or "monastery" or "convent" or "basilica"
            || r.Type is "religious" or "yes" && r.Religion == "christian";
        return churchy && c.Site.Area is >= 12f and <= 4000f;
    }

    public float Plinth => 0.4f;
    public float RoofExtra => 0.3f;
    public int InferLevels(BuildingGenerator.Context c) => 1;
    public float StoreyHeight(BuildingGenerator.Context c)
    {
        float s = MathF.Sqrt(c.Site.Area);
        return Look switch
        {
            Kind.Chapel => Math.Clamp(2.4f + s * 0.25f, 3f, 4.5f),
            Kind.Wooden => Math.Clamp(3.5f + s * 0.2f, 5f, 8f),
            Kind.Modern => Math.Clamp(4f + s * 0.25f, 6f, 14f),
            _ => Math.Clamp(4.5f + s * 0.3f, 7f, 17f),
        };
    }
    public string InferRoof(BuildingGenerator.Context c) => "gabled";
    public float RoofPitch(BuildingGenerator.Context c) => Look switch { Kind.Gothic => 52f, Kind.Modern => 22f, Kind.Baroque => 42f, _ => 45f };

    // ---- looks ---------------------------------------------------------------------------------------------------------

    private string _body = "wall";

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, roof, trim, name;
        switch (Look)
        {
            case Kind.Gothic:
                (name, wall) = L.Weighted<(string, string)>([(("red brick", "8f4532"), 5), (("dark red brick", "74362a"), 2), (("orange brick", "a65a3c"), 2)]);
                roof = L.Weighted<string>([("7a3a2c", 3), ("4a4c4e", 3), ("8a4a35", 2), ("4c6a58", 1)]);
                trim = "c9bba2"; _body = "brick";
                m["brick"] = Kit.Mat("brick", wall, 0.9f);
                break;
            case Kind.Baroque:
                (name, wall) = L.Weighted<(string, string)>([(("white", "ece8df"), 3), (("cream", "e8dcbc"), 3), (("yellow", "e2c87e"), 2), (("ochre", "d4a95e"), 1)]);
                roof = L.Weighted<string>([("8a4a35", 3), ("5c6062", 2), ("4c6a58", 2)]);
                trim = wall == "ece8df" ? "d8d0c0" : "f2efe8";
                break;
            case Kind.Modern:
                (name, wall) = L.Weighted<(string, string)>([(("white", "e8e6e0"), 3), (("light grey", "c8c6c0"), 2), (("yellow brick", "c9a86a"), 1)]);
                roof = L.Weighted<string>([("5c6062", 3), ("3d3f42", 2), ("8a4a35", 1)]);
                trim = "f0eee8";
                break;
            case Kind.Wooden:
                (name, wall) = L.Weighted<(string, string)>([(("dark wood", "4f3a29"), 3), (("brown wood", "6b4d33"), 2), (("weathered", "6e665b"), 1)]);
                roof = L.Weighted<string>([("3f3a33", 3), ("55504a", 2)]);
                trim = "d8d2c4"; _body = "timber";
                m["timber"] = Kit.Mat("timber", wall, 0.95f);
                break;
            default:
                (name, wall) = L.Weighted<(string, string)>([(("white", "ece8df"), 4), (("cream", "e8dcbc"), 2), (("red brick", "8f4532"), 1)]);
                roof = L.Weighted<string>([("8a4a35", 2), ("5c6062", 2)]);
                trim = "d8d0c0";
                if (wall == "8f4532") { _body = "brick"; m["brick"] = Kit.Mat("brick", wall, 0.9f); }
                break;
        }
        m["wall"] = Kit.Mat("wall", wall, 0.88f);
        m["plinth"] = Kit.Mat("plinth", "6f6a62", 0.95f);
        m["reveal"] = Kit.Mat("reveal", trim, 0.85f);
        m["trim"] = Kit.Mat("trim", trim, 0.8f);
        m["frame"] = Kit.Mat("frame", "3a3530", 0.6f);
        m["glass"] = Kit.Mat("glass", Look == Kind.Gothic ? "33404a" : "2a3640", 0.08f, 0.1f);
        m["louvre"] = Kit.Mat("louvre", "2b2a28", 0.9f);
        m["door"] = Kit.Mat("door", L.Weighted<string>([("4a3424", 3), ("2f2a26", 1)]), 0.7f);
        m["roof"] = Kit.Mat("roof", roof, 0.7f, 0.15f);
        // spires: gothic ones dark (patinated copper, slate, black sheet), baroque helmets green or dark or like the roof
        m["spire"] = Kit.Mat("spire", Look switch
        {
            Kind.Gothic => L.Weighted<string>([("4f7562", 3), ("3d4044", 3), ("2b2c2e", 2)]),
            Kind.Baroque => L.Weighted<string>([(roof, 2), ("4c6a58", 2), ("3a3d42", 1)]),
            _ => roof,
        }, 0.55f, 0.3f);
        m["cross"] = Kit.Mat("cross", "c9a03a", 0.3f, 0.9f);
        m["soffit"] = Kit.Mat("soffit", "6a5a48", 0.9f);
        m["steps"] = Kit.Mat("steps", "8c877e", 0.95f);
        c.Plan = c.Plan with { Palette = $"church, {Look.ToString().ToLowerInvariant()}, {name}" };
        c.Building.Plan = c.Plan;
    }

    // ---- building ------------------------------------------------------------------------------------------------------

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow, h = top - datum;
        var full = plan.RoofRect;
        var east = full.U.X >= 0 ? full.U : -full.U;
        bool Inside(Vector2 p) => Polygon.Contains(c.Site.Outer, p);

        // the door: the edge facing west most squarely
        int doorEdge = -1; float best = 0.3f;
        for (int i = 0; i < plan.Edges.Count; i++)
        {
            var e = plan.Edges[i];
            if (e.Ring != 0 || e.Length < 1.8f) continue;
            float d = -Vector2.Dot(e.Out, east);
            if (d > best) { best = d; doorEdge = i; }
        }

        float bay = Look switch { Kind.Chapel => 2.4f, Kind.Wooden => 3.2f, Kind.Modern => 3.0f, _ => 4.2f };
        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            int bays = len >= 2.2f ? Math.Max(1, (int)MathF.Round(len / bay)) : 0;
            if (ei == doorEdge) bays = Math.Max(bays, 1);
            if (bays == 0) { Kit.Wall(wall.Mesh, _body, f, 0, len, datum, top); continue; }
            float bw = len / bays;
            int doorBay = ei == doorEdge ? bays / 2 : -1;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                if (bi == doorBay) { Portal(c, wall.Mesh, trim.Mesh, f, e, u0, u1, uc, datum, top, ground); continue; }
                Window(wall.Mesh, trim.Mesh, f, u0, u1, uc, datum, top, h);
                // buttresses (gothic) or pilasters (baroque) between the bays
                if (bi > 0 && Look == Kind.Gothic)
                {
                    Kit.Box(trim.Mesh, _body, f, u0 - 0.28f, u0 + 0.28f, datum - 0.2f, top - h * 0.25f, 0, 0.55f);
                    trim.Mesh.AddQuad("roof", f.P(u0 - 0.3f, top - h * 0.25f, 0.57f), f.P(u0 + 0.3f, top - h * 0.25f, 0.57f), f.P(u0 + 0.3f, top - h * 0.12f, 0), f.P(u0 - 0.3f, top - h * 0.12f, 0), Vector3.Normalize(f.Out + Vector3.UnitY));
                }
                else if (bi > 0 && Look == Kind.Baroque)
                    Kit.Box(trim.Mesh, "trim", f, u0 - 0.22f, u0 + 0.22f, datum, top, 0, 0.06f);
            }
            // a cornice under the eaves
            if (Look is Kind.Baroque or Kind.Gothic) Kit.Box(trim.Mesh, "trim", f, 0, len, top - 0.35f, top - 0.1f, 0, 0.12f);
            if (Look == Kind.Wooden && len >= 1.2f)
            {
                Kit.Box(trim.Mesh, _body, f, 0, 0.15f, datum, top, 0, 0.04f);
                Kit.Box(trim.Mesh, _body, f, len - 0.15f, len, datum, top, 0, 0.04f);
            }
        }

        // the roof: pitched over the nave's rectangle (trimmed to fit an apse), flat over anything left
        var roofM = b.Add(c.Id("roof"), "roof", ModuleTier.Primary).Mesh;
        var nave = Fit(full, east, Inside);
        float rise = MathF.Min(nave.HalfW * MathF.Tan(RoofPitch(c) * MathF.PI / 180f), 12f);
        float ridge = top + rise;
        if (nave.HalfL > 1f)
        {
            Roofs.Pitched(roofM, "roof", "soffit", _body, nave, RoofShape.Gabled, top, rise, 0.35f);
            if (nave.Fill < 0.999f || full.Fill < Roofs.RectangularEnough)
                Kit.Slab(roofM, "roof", c.Site.Outer, c.Site.Holes, top - 0.15f, top + 0.05f, sides: false);
            else Kit.Slab(roofM, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
        }
        else
        {
            Kit.Slab(roofM, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
            ridge = top + 0.1f;
        }

        var tw = b.Add(c.Id("tower"), "tower", ModuleTier.Primary).Mesh;
        var west = nave.Centre - east * nave.HalfL;
        var side = new Vector2(east.Y, -east.X);
        if (Look == Kind.Chapel)
        {
            // a small bell turret on the ridge at the west end, and a cross
            var tc = west + east * MathF.Min(1.2f, nave.HalfL * 0.4f);
            if (Inside(tc))
            {
                float s = MathF.Min(0.9f, nave.HalfW * 0.5f);
                var sq = Square(tc, east, s);
                Prism(tw, _body == "brick" ? "brick" : "wall", sq, ridge - 0.5f, ridge + 0.9f);
                Spire(tw, "spire", sq, tc, ridge + 0.9f, ridge + 0.9f + s * 1.6f);
                LatinCross(tw, tc, ridge + 0.9f + s * 1.6f, 0.9f, east);
            }
            else LatinCross(tw, nave.Centre + east * nave.HalfL * 0.9f, ridge, 0.9f, east);
            return;
        }
        float t = Math.Clamp(MathF.Min(nave.HalfW * 1.15f, nave.HalfL * 0.45f), 2.8f, 9f);
        bool twin = Look == Kind.Gothic && c.Site.Area > 900f && nave.HalfW > 8f;
        var centres = twin
            ? new[] { west + east * (t * 0.42f + 0.05f) + side * (nave.HalfW - t * 0.42f - 0.05f), west + east * (t * 0.42f + 0.05f) - side * (nave.HalfW - t * 0.42f - 0.05f) }
            : new[] { west + east * (t * 0.5f + 0.05f) };
        if (twin) t *= 0.84f;
        if (Look == Kind.Modern) t *= 0.7f;
        foreach (var tc in centres)
        {
            var sq = Square(tc, east, t);
            if (!sq.All(Inside)) continue;
            float tTop = Look switch
            {
                Kind.Gothic => ridge + MathF.Max(4f, t * 1.3f),
                Kind.Baroque => ridge + MathF.Max(3f, t * 0.9f),
                Kind.Modern => top + MathF.Max(14f, h * 1.6f),
                _ => ridge + 2.5f,
            };
            Tower(tw, sq, tc, top - 0.3f, tTop, t);
            switch (Look)
            {
                case Kind.Gothic:
                {
                    float apex = tTop + 0.15f + t * 2.6f;
                    Spire(tw, "spire", sq, tc, tTop + 0.15f, apex);
                    LatinCross(tw, tc, apex, 1.6f, east);
                    break;
                }
                case Kind.Baroque:
                {
                    // the helmet: a bulb, a lantern, a smaller bulb
                    float r = t * 0.5f;
                    float y = Bulb(tw, tc, r, tTop + 0.15f, 1.0f);
                    Orthodox.Octagon(tw, "wall", tc, r * 0.32f, y - 0.1f, y + r * 0.5f, east);
                    float y2 = Bulb(tw, tc, r * 0.36f, y + r * 0.5f, 1.2f);
                    LatinCross(tw, tc, y2, 1.2f, east);
                    break;
                }
                case Kind.Modern:
                    LatinCross(tw, tc, tTop + 0.15f, MathF.Max(3f, t * 0.9f), east);
                    break;
                default:
                {
                    float apex = tTop + 0.15f + t * 0.9f;
                    Spire(tw, "spire", sq, tc, tTop + 0.15f, apex);
                    LatinCross(tw, tc, apex, 1.1f, east);
                    break;
                }
            }
        }
        // a small cross over the east gable
        if (Look != Kind.Modern) LatinCross(tw, nave.Centre + east * (nave.HalfL - 0.3f), ridge, 1.1f, east);
    }

    // ---- pieces --------------------------------------------------------------------------------------------------------

    /// <summary>The rectangle trimmed from the east end (then the sides) until its corners stand in the footprint.</summary>
    private static Roofs.Rect Fit(Roofs.Rect r, Vector2 east, Func<Vector2, bool> inside)
    {
        var side = new Vector2(east.Y, -east.X);
        float l0 = -r.HalfL, l1 = r.HalfL, w = r.HalfW;
        bool Ok() => new[] { (l0, w), (l0, -w), (l1, w), (l1, -w) }.All(k => inside(r.Centre + east * k.Item1 + side * k.Item2 * 0.999f));
        for (int i = 0; i < 80 && !Ok(); i++)
        {
            if (l1 - l0 > 4f && i % 3 != 2) l1 -= 0.5f;
            else { w -= 0.25f; if (w < 1f) break; }
        }
        float hl = (l1 - l0) * 0.5f;
        var c = r.Centre + east * ((l0 + l1) * 0.5f);
        float fill = hl * w / MathF.Max(r.HalfL * r.HalfW, 0.01f);
        return new Roofs.Rect(c, east, side, hl, w, fill);
    }

    private void Window(Mesh wall, Mesh trim, Kit.Frame f, float u0, float u1, float uc, float datum, float top, float h)
    {
        float ww, ys, yh;
        switch (Look)
        {
            case Kind.Chapel: ww = MathF.Min(0.3f, (u1 - u0) * 0.15f); ys = datum + 1.2f; yh = MathF.Min(datum + 2.4f, top - 0.4f); break;
            case Kind.Wooden: ww = MathF.Min(0.5f, (u1 - u0) * 0.17f); ys = datum + 1.4f; yh = MathF.Min(ys + 2.2f, top - 0.6f); break;
            case Kind.Modern: ww = MathF.Min(0.35f, (u1 - u0) * 0.12f); ys = datum + 1.0f; yh = top - 0.6f; break;
            default: ww = MathF.Min(0.75f, (u1 - u0) * 0.18f); ys = datum + MathF.Max(1.6f, h * 0.22f); yh = top - MathF.Max(1.2f, h * 0.15f); break;
        }
        if (yh - ys < 0.8f) { Kit.Wall(wall, _body, f, u0, u1, datum, top); return; }
        Kit.WallWithOpening(wall, _body, "reveal", "glass", f, u0, u1, datum, top, uc - ww, uc + ww, ys, yh, 0.25f);
        Kit.Box(trim, "frame", f, uc - 0.03f, uc + 0.03f, ys, yh, -0.2f, -0.17f);
        if (Look is Kind.Gothic or Kind.Baroque)
            for (float y = ys + 0.6f; y < yh - 0.3f; y += 0.6f) Kit.Box(trim, "frame", f, uc - ww, uc + ww, y - 0.02f, y + 0.02f, -0.2f, -0.17f);
        // the head: a pointed arch (gothic, chapel), a round one (baroque, as a half octagon), none (modern, wooden)
        if (Look is Kind.Gothic or Kind.Chapel)
            trim.AddPolygon("trim", [f.P(uc - ww - 0.12f, yh, 0.05f), f.P(uc + ww + 0.12f, yh, 0.05f), f.P(uc, yh + ww * 1.3f, 0.05f)], f.Out);
        else if (Look == Kind.Baroque)
        {
            var pts = new List<Vector3>();
            for (int k = 0; k <= 6; k++) { float a = MathF.PI * k / 6; pts.Add(f.P(uc + MathF.Cos(a) * (ww + 0.1f), yh + MathF.Sin(a) * (ww + 0.1f), 0.05f)); }
            trim.AddPolygon("trim", pts, f.Out);
        }
        if (Look != Kind.Modern) Kit.Box(trim, "trim", f, uc - ww - 0.12f, uc + ww + 0.12f, ys - 0.12f, ys, 0, 0.1f);
    }

    /// <summary>The main door at the west: a tall door in a stone-coloured portal, steps, a marker.</summary>
    private void Portal(BuildingGenerator.Context c, Mesh wall, Mesh trim, Kit.Frame f, WallEdge e, float u0, float u1, float uc, float datum, float top, float ground)
    {
        float hw = Look == Kind.Chapel ? 0.55f : MathF.Min(1.1f, (u1 - u0) * 0.3f), dh = MathF.Min(Look == Kind.Chapel ? 2.2f : 3.6f, top - datum - 0.5f);
        Kit.WallWithOpening(wall, _body, "reveal", "door", f, u0, u1, datum, top, uc - hw, uc + hw, datum, datum + dh, 0.3f);
        Kit.Box(trim, "trim", f, uc - hw - 0.25f, uc - hw, datum, datum + dh, 0, 0.15f);
        Kit.Box(trim, "trim", f, uc + hw, uc + hw + 0.25f, datum, datum + dh, 0, 0.15f);
        if (Look is Kind.Gothic or Kind.Chapel)
            trim.AddPolygon("trim", [f.P(uc - hw - 0.25f, datum + dh, 0.15f), f.P(uc + hw + 0.25f, datum + dh, 0.15f), f.P(uc, datum + dh + hw * 1.2f, 0.15f)], f.Out);
        else Kit.Box(trim, "trim", f, uc - hw - 0.35f, uc + hw + 0.35f, datum + dh, datum + dh + 0.3f, 0, 0.2f);
        c.Building.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}"), "entrance", f.P(uc, datum, 0.05f), f.Out));
        float rise = datum - ground;
        int n = Math.Clamp((int)MathF.Round(rise / 0.17f), 0, 5);
        for (int k = 0; k < n; k++)
            Kit.Box(trim, "steps", f, uc - hw - 0.6f, uc + hw + 0.6f, ground - 0.2f, datum - k * (rise / MathF.Max(n, 1)), 0, MathF.Min(1.0f, 0.3f * (k + 1)));
    }

    private static Vector2[] Square(Vector2 c, Vector2 fwd, float s)
    {
        var side = new Vector2(fwd.Y, -fwd.X);
        float h = s * 0.5f;
        return [c + (fwd + side) * h, c + (fwd - side) * h, c + (-fwd - side) * h, c + (-fwd + side) * h];
    }

    private static Kit.Frame FaceFrame(Vector2 a, Vector2 b, Vector2 tc)
    {
        var dir = Vector2.Normalize(b - a);
        var outN = new Vector2(dir.Y, -dir.X);
        if (Vector2.Dot(outN, a - tc) < 0) outN = -outN;
        return new Kit.Frame(new Vector3(a.X, 0, a.Y), new Vector3(dir.X, 0, dir.Y), new Vector3(outN.X, 0, outN.Y));
    }

    private static void Prism(Mesh m, string role, Vector2[] sq, float y0, float y1)
    {
        var tc = (sq[0] + sq[2]) * 0.5f;
        for (int k = 0; k < 4; k++)
        {
            var f = FaceFrame(sq[k], sq[(k + 1) % 4], tc);
            Kit.Wall(m, role, f, 0, Vector2.Distance(sq[k], sq[(k + 1) % 4]), y0, y1);
        }
    }

    /// <summary>The tower above the roof: plain walls, a belfry with louvred openings and a cornice.</summary>
    private void Tower(Mesh m, Vector2[] sq, Vector2 tc, float y0, float y1, float s)
    {
        float belfry = y1 - MathF.Min(3.2f, (y1 - y0) * 0.35f);
        for (int k = 0; k < 4; k++)
        {
            var f = FaceFrame(sq[k], sq[(k + 1) % 4], tc);
            Kit.Wall(m, _body, f, 0, s, y0, belfry);
            float ow = MathF.Min(Look == Kind.Modern ? 0.4f : 0.6f, s * 0.18f);
            Kit.WallWithOpening(m, _body, "reveal", "louvre", f, 0, s, belfry, y1, s * 0.5f - ow, s * 0.5f + ow, belfry + 0.3f, y1 - 0.4f, 0.15f);
            if (Look is Kind.Gothic) m.AddPolygon("trim", [f.P(s * 0.5f - ow - 0.1f, y1 - 0.4f, 0.04f), f.P(s * 0.5f + ow + 0.1f, y1 - 0.4f, 0.04f), f.P(s * 0.5f, y1 - 0.4f + ow, 0.04f)], f.Out);
            // a clock face on the baroque and gothic towers' street sides: a pale disc under the belfry
            if (Look is Kind.Gothic or Kind.Baroque)
            {
                float cy = belfry - 0.9f, cr = MathF.Min(0.6f, s * 0.15f);
                var pts = new List<Vector3>();
                for (int q = 0; q < 8; q++) { float a = q * MathF.PI / 4f; pts.Add(f.P(s * 0.5f + MathF.Cos(a) * cr, cy + MathF.Sin(a) * cr, 0.04f)); }
                m.AddPolygon("trim", pts, f.Out);
            }
            if (Look != Kind.Wooden) Kit.Box(m, "trim", f, -0.05f, s + 0.05f, y1 - 0.15f, y1 + 0.15f, 0, 0.12f);
            Kit.Box(m, "trim", f, -0.02f, s + 0.02f, belfry - 0.12f, belfry, 0, 0.06f);
        }
    }

    /// <summary>A four-sided spire from the square's corners to an apex.</summary>
    private static void Spire(Mesh m, string role, Vector2[] sq, Vector2 c, float y0, float apex)
    {
        var top = new Vector3(c.X, apex, c.Y);
        for (int k = 0; k < 4; k++)
        {
            var a = sq[k]; var b = sq[(k + 1) % 4];
            var mid = (a + b) * 0.5f - c;
            m.AddPolygon(role, [new Vector3(a.X, y0, a.Y), new Vector3(b.X, y0, b.Y), top], new Vector3(mid.X, 0.4f, mid.Y));
        }
    }

    /// <summary>A baroque bulb (a lathe of 12 sides); returns its top.</summary>
    private static float Bulb(Mesh m, Vector2 c, float r, float y0, float stretch)
    {
        (float R, float Y)[] p = [(1.0f, 0f), (1.0f, 0.15f), (0.72f, 0.35f), (0.62f, 0.5f), (0.85f, 0.75f), (0.78f, 1.0f), (0.45f, 1.25f), (0.15f, 1.45f), (0.05f, 1.6f)];
        const int sides = 12;
        for (int i = 0; i + 1 < p.Length; i++)
        {
            float ra = p[i].R * r, rb = p[i + 1].R * r, ya = y0 + p[i].Y * r * stretch, yb = y0 + p[i + 1].Y * r * stretch;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * MathF.Tau / sides, a1 = (k + 1) * MathF.Tau / sides, am = (a0 + a1) * 0.5f;
                m.AddQuad("spire",
                    new Vector3(c.X + MathF.Cos(a0) * ra, ya, c.Y + MathF.Sin(a0) * ra), new Vector3(c.X + MathF.Cos(a1) * ra, ya, c.Y + MathF.Sin(a1) * ra),
                    new Vector3(c.X + MathF.Cos(a1) * rb, yb, c.Y + MathF.Sin(a1) * rb), new Vector3(c.X + MathF.Cos(a0) * rb, yb, c.Y + MathF.Sin(a0) * rb),
                    new Vector3(MathF.Cos(am), 0.3f, MathF.Sin(am)));
            }
        }
        return y0 + p[^1].Y * r * stretch;
    }

    /// <summary>A Latin cross: a post and one bar, facing east-west.</summary>
    private static void LatinCross(Mesh m, Vector2 c, float y0, float h, Vector2 east)
    {
        var n = new Vector2(east.Y, -east.X);
        var f = new Kit.Frame(new Vector3(c.X, 0, c.Y), new Vector3(n.X, 0, n.Y), new Vector3(east.X, 0, east.Y));
        float t = MathF.Max(0.05f, h * 0.05f);
        Kit.Box(m, "cross", f, -t, t, y0, y0 + h, -t, t);
        Kit.Box(m, "cross", f, -h * 0.3f, h * 0.3f, y0 + h * 0.65f, y0 + h * 0.65f + 2 * t, -t, t);
    }
}
