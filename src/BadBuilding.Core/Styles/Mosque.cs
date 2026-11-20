using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The mosque (D42), on the map's word (<c>religion=muslim</c> on a mosque or place of worship). Podlasie's Tatar
/// mosques are wooden village buildings, and the two that stand (Kruszyniany, Bohoniki) are the two kinds built here,
/// chosen by the footprint (from photographs, Wikimedia Commons):
/// * <b>long</b> (Kruszyniany): vertical boards painted green, a gabled roof along the length; at the front two tall
///   square towers on the corners with arched windows and small dark cupolas; an arched porch at the door; a small
///   cupola on the ridge; pointed-arch windows down the sides;
/// * <b>square</b> (Bohoniki): horizontal boards in plain wood (or grey, or green) under a hipped roof, an octagonal
///   lantern in the middle with a dark dome, and a little gabled porch;
/// every cupola and dome topped with a finial: a spike, two balls and a crescent.
/// A mosque that is neither historic nor mapped as wood, outside Podlasie, is <b>modern</b>: white or cream plaster, a
/// dome on a drum over the hall, and on a big one a slim minaret at a corner.
/// </summary>
public sealed class Mosque : IStyle
{
    public const string Name = "mosque";
    public string Id => Name;

    public enum Kind { Long, Square, Modern }
    private readonly Kind _kind;

    public Mosque(BuildingGenerator.Context c)
    {
        var r = c.Request;
        var rect = Roofs.MinRect(c.Site.Outer);
        bool tatar = r.Heritage || r.Material == "wood" || r.Preset == House.Podlasie && c.Site.Area < 400f;
        _kind = !tatar ? Kind.Modern : rect.HalfL / MathF.Max(rect.HalfW, 0.1f) >= 1.25f ? Kind.Long : Kind.Square;
    }

    /// <summary>A mosque the map says is one: religion=muslim on a mosque, a place of worship, or an untyped building
    /// that is one (not an office or a tomb).</summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        // religion=muslim, or a building mapped as a mosque with no religion said
        if (!(r.Religion == "muslim" || r.Type == "mosque" && r.Religion is null) || r.Raised) return false;
        if (r.Type is not ("mosque" or "religious" or "yes" or "temple" or "church")) return false;
        return c.Site.Area is >= 25f and <= 3000f && c.Site.Holes.Count == 0;
    }

    public float Plinth => 0.4f;
    public float RoofExtra => 0.3f;
    public int InferLevels(BuildingGenerator.Context c) => 1;
    public float StoreyHeight(BuildingGenerator.Context c) => _kind switch { Kind.Long => 5.2f, Kind.Square => 4.4f, _ => c.Site.Area > 300f ? 7f : 5.5f };
    public string InferRoof(BuildingGenerator.Context c) => _kind switch { Kind.Long => "gabled", Kind.Square => "hipped", _ => "flat" };
    public float RoofPitch(BuildingGenerator.Context c) => _kind == Kind.Long ? 38f : 30f;

    // ---- looks ---------------------------------------------------------------------------------------------------------

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, roof, dome, name;
        switch (_kind)
        {
            case Kind.Long:
                (name, wall) = L.Weighted<(string, string)>([(("green", "3f9a6a"), 6), (("grey-green", "8fa8a0"), 2), (("dark green", "2f6b4a"), 1)]);
                roof = L.Weighted<string>([("6b4a3a", 3), ("6e6e6a", 2)]);
                dome = "2f3134";
                m["boards"] = Kit.Mat("boards", wall, 0.9f);
                break;
            case Kind.Square:
                (name, wall) = L.Weighted<(string, string)>([(("plain wood", "b07e4a"), 5), (("weathered grey", "7d766a"), 2), (("green", "4c8f5e"), 1)]);
                roof = L.Weighted<string>([("3d3f42", 4), ("a9c79a", 1)]);
                dome = roof == "a9c79a" ? roof : "2f3134";
                m["timber"] = Kit.Mat("timber", wall, 0.9f);
                break;
            default:
                (name, wall) = L.Weighted<(string, string)>([(("white", "ece8df"), 3), (("cream", "e6dcc4"), 2), (("sand", "d8c8a4"), 1)]);
                roof = "7a7d80";
                dome = L.Weighted<string>([("3f7a5a", 3), ("c9a03a", 1), ("ece8df", 1), ("41618c", 1)]);
                break;
        }
        m["wall"] = Kit.Mat("wall", wall, 0.88f);
        m["plinth"] = Kit.Mat("plinth", _kind == Kind.Modern ? "a49f95" : "6f6a62", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "ebe8de", 0.85f);
        m["frame"] = Kit.Mat("frame", "ebe8de", 0.6f);
        m["trim"] = Kit.Mat("trim", "ebe8de", 0.7f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["door"] = Kit.Mat("door", _kind == Kind.Long ? "ebe8de" : "5a3e2a", 0.7f);
        m["roof"] = Kit.Mat("roof", roof, 0.6f, 0.3f);
        m["dome"] = Kit.Mat("dome", dome, dome == "c9a03a" ? 0.3f : 0.5f, dome == "c9a03a" ? 0.9f : 0.35f);
        m["finial"] = Kit.Mat("finial", "c9a03a", 0.3f, 0.9f);
        m["soffit"] = Kit.Mat("soffit", _kind == Kind.Modern ? "8a857c" : wall, 0.9f);
        m["steps"] = Kit.Mat("steps", "8c877e", 0.95f);
        c.Plan = c.Plan with { Palette = $"mosque, {_kind.ToString().ToLowerInvariant()}, {name}" };
        c.Building.Plan = c.Plan;
    }

    // ---- building ------------------------------------------------------------------------------------------------------

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        string body = _kind switch { Kind.Long => "boards", Kind.Square => "timber", _ => "wall" };
        var rect = plan.RoofRect;
        // the front: the street side's end of the long axis (else its west end)
        var street = plan.Edges.Where(e => e.Exposure == Exposure.Street).OrderByDescending(e => e.Length).FirstOrDefault();
        var fwd = _kind == Kind.Long
            ? (street != null ? (Vector2.Dot(street.Out, rect.U) >= 0 ? rect.U : -rect.U) : (rect.U.X <= 0 ? rect.U : -rect.U))
            : (street?.Out ?? -rect.U);
        int doorEdge = -1; float best = 0.3f;
        for (int i = 0; i < plan.Edges.Count; i++)
        {
            var e = plan.Edges[i];
            if (e.Ring != 0 || e.Length < 2f) continue;
            float d = Vector2.Dot(e.Out, fwd);
            if (d > best) { best = d; doorEdge = i; }
        }

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length, h = top - datum;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            int bays = len >= 2.6f ? Math.Max(1, (int)MathF.Round(len / 3.0f)) : 0;
            if (ei == doorEdge) bays = Math.Max(bays, 1);
            if (bays == 0) { Kit.Wall(wall.Mesh, body, f, 0, len, datum, top); continue; }
            float bw = len / bays;
            int doorBay = ei == doorEdge ? bays / 2 : -1;   // the door in the middle bay, windows either side
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                if (bi == doorBay)
                {
                    float hw = MathF.Min(0.75f, bw * 0.3f), dh = MathF.Min(2.3f, h - 0.5f);
                    Kit.WallWithOpening(wall.Mesh, body, "reveal", "door", f, u0, u1, datum, top, uc - hw, uc + hw, datum, datum + dh, 0.15f);
                    b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}"), "entrance", f.P(uc, datum, 0.05f), f.Out));
                    Porch(trim.Mesh, f, uc, hw, datum, dh, ground, body);
                    continue;
                }
                float ww = MathF.Min(0.45f, bw * 0.2f), ys = datum + 1.0f, yh = MathF.Min(datum + (_kind == Kind.Modern ? h - 0.8f : 2.6f), top - 0.5f);
                if (yh - ys < 0.8f) { Kit.Wall(wall.Mesh, body, f, u0, u1, datum, top); continue; }
                Kit.WallWithOpening(wall.Mesh, body, "reveal", "glass", f, u0, u1, datum, top, uc - ww, uc + ww, ys, yh, 0.14f);
                Kit.Box(trim.Mesh, "frame", f, uc - 0.03f, uc + 0.03f, ys, yh, -0.11f, -0.08f);
                Kit.Box(trim.Mesh, "frame", f, uc - ww, uc + ww, ys + (yh - ys) * 0.55f - 0.03f, ys + (yh - ys) * 0.55f + 0.03f, -0.11f, -0.08f);
                Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.09f, uc - ww, ys - 0.05f, yh, 0, 0.035f);
                Kit.Box(trim.Mesh, "trim", f, uc + ww, uc + ww + 0.09f, ys - 0.05f, yh, 0, 0.035f);
                Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.12f, uc + ww + 0.12f, ys - 0.1f, ys - 0.03f, 0, 0.05f);
                // Kruszyniany's pointed arches, the others a flat head
                if (_kind == Kind.Long)
                    trim.Mesh.AddPolygon("trim", [f.P(uc - ww - 0.09f, yh, 0.035f), f.P(uc + ww + 0.09f, yh, 0.035f), f.P(uc, yh + ww * 0.9f, 0.035f)], f.Out);
                else
                    Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.12f, uc + ww + 0.12f, yh, yh + 0.1f, 0, 0.04f);
            }
            if (_kind != Kind.Modern && len >= 1.2f)
            {
                Kit.Box(trim.Mesh, "trim", f, 0, 0.14f, datum, top, 0, 0.03f);
                Kit.Box(trim.Mesh, "trim", f, len - 0.14f, len, datum, top, 0, 0.03f);
            }
        }

        // the roof
        var roofM = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        float ridge = top;
        if (plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal)
        {
            Roofs.Pitched(roofM.Mesh, "roof", "soffit", body, plan, top, 0.4f * plan.Eaves);
            Kit.Slab(roofM.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
            ridge = top + (plan.RoofWings.Count > 0 ? plan.RoofWings.Max(w => w.Rise) : plan.RoofRise);
        }
        else
        {
            Kit.Slab(roofM.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
            Kit.Parapet(roofM.Mesh, "wall", c.Site.Outer, top + 0.1f, top + 0.5f, 0.2f);
            ridge = top + 0.1f;
        }

        var dm = b.Add(c.Id("domes"), "domes", ModuleTier.Secondary).Mesh;
        bool Inside(Vector2 p) => Polygon.Contains(c.Site.Outer, p);
        float HL = rect.HalfL, HW = rect.HalfW;
        switch (_kind)
        {
            case Kind.Long:
            {
                // two towers on the front corners
                float s = Math.Clamp(HW * 0.42f, 1.3f, 2.2f);
                var side = new Vector2(fwd.Y, -fwd.X);
                var frontMid = rect.Centre + fwd * HL;
                float tTop = MathF.Max(ridge + 0.8f, top + 4f);
                foreach (float sg in new[] { -1f, 1f })
                {
                    var tc = frontMid - fwd * (s * 0.5f + 0.05f) + side * sg * (HW - s * 0.5f - 0.05f);
                    var sq = Square(tc, fwd, s);
                    if (!sq.All(Inside)) continue;
                    Tower(dm, sq, tc, body, top - 0.4f, tTop, s);
                    float cy = Cupola(dm, tc, s * 0.42f, tTop, fwd);
                    Finial(dm, tc, cy, 1.0f, fwd);
                }
                // a small cupola on the ridge, behind the middle
                var rc = rect.Centre - fwd * (HL * 0.25f);
                if (Inside(rc))
                {
                    Orthodox.Octagon(dm, body, rc, 0.45f, ridge - 0.35f, ridge + 0.5f, fwd);
                    float cy = Cupola(dm, rc, 0.45f, ridge + 0.5f, fwd);
                    Finial(dm, rc, cy, 0.8f, fwd);
                }
                break;
            }
            case Kind.Square:
            {
                // the lantern in the middle of the hipped roof
                var lc = rect.Centre;
                float r = Math.Clamp(HW * 0.2f, 0.7f, 1.5f);
                float y0 = ridge - 0.5f, y1 = ridge + 1.2f + r * 0.3f;
                Orthodox.Octagon(dm, body, lc, r, y0, y1, fwd);
                for (int k = 0; k < 8; k += 2)
                {
                    var n = Orthodox.Dir(k * MathF.PI / 4f + MathF.PI / 8f, fwd);
                    var fc = new Kit.Frame(new Vector3(lc.X + n.X * r * 0.924f, 0, lc.Y + n.Y * r * 0.924f), new Vector3(-n.Y, 0, n.X), new Vector3(n.X, 0, n.Y));
                    Kit.Box(dm, "glass", fc, -0.18f, 0.18f, y1 - 0.9f, y1 - 0.25f, 0, 0.02f);
                }
                Orthodox.Octagon(dm, "trim", lc, r + 0.1f, y1, y1 + 0.12f, fwd);
                float cy = Cupola(dm, lc, r * 1.02f, y1 + 0.12f, fwd);
                Finial(dm, lc, cy, 1.1f, fwd);
                break;
            }
            default:
            {
                // a dome on a drum over the hall; on a big mosque, a minaret at a back corner
                var dc = rect.Centre;
                float r = Math.Clamp(MathF.Min(HW, HL) * 0.45f, 1.5f, 6f);
                if (OctInside(c, dc, r))
                {
                    float y1 = ridge + MathF.Max(1.0f, r * 0.5f);
                    Orthodox.Octagon(dm, "wall", dc, r, top - 0.2f, y1, fwd);
                    Orthodox.Octagon(dm, "trim", dc, r + 0.1f, y1, y1 + 0.15f, fwd);
                    float cy = Dome(dm, dc, r, y1 + 0.15f, Hemi);
                    Finial(dm, dc, cy, 1.4f, fwd);
                }
                if (c.Site.Area > 200f)
                {
                    // the corner of the footprint's rectangle farthest from the front
                    var mc = new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) }
                        .Select(k => rect.At(k.Item1 * (HL - 1.4f), k.Item2 * (HW - 1.4f)))
                        .OrderBy(p => Vector2.Dot(p - rect.Centre, fwd)).First();
                    if (OctInside(c, mc, 1.1f))
                    {
                        float mTop = top + MathF.Max(12f, (top - datum) * 2.2f);
                        Orthodox.Octagon(dm, "wall", mc, 0.9f, top - 0.2f, mTop, fwd);
                        // the balcony and its rail
                        Orthodox.Octagon(dm, "trim", mc, 1.25f, mTop - 2.4f, mTop - 2.2f, fwd);
                        Orthodox.Octagon(dm, "trim", mc, 1.2f, mTop - 1.5f, mTop - 1.4f, fwd);
                        float cy = Dome(dm, mc, 0.9f, mTop, Cone);
                        Finial(dm, mc, cy, 1.0f, fwd);
                    }
                }
                break;
            }
        }
    }

    // ---- pieces --------------------------------------------------------------------------------------------------------

    private static readonly (float R, float Y)[] Onionish = [(0.85f, 0f), (1.05f, 0.2f), (1.12f, 0.45f), (1.0f, 0.75f), (0.72f, 1.0f), (0.38f, 1.2f), (0.1f, 1.38f), (0.03f, 1.5f)];
    private static readonly (float R, float Y)[] Hemi = [(1f, 0f), (0.97f, 0.25f), (0.87f, 0.5f), (0.71f, 0.7f), (0.5f, 0.86f), (0.26f, 0.97f), (0.02f, 1f)];
    private static readonly (float R, float Y)[] Cone = [(1f, 0f), (0.6f, 0.9f), (0.25f, 1.8f), (0.03f, 2.6f)];

    private static Vector2[] Square(Vector2 c, Vector2 fwd, float s)
    {
        var side = new Vector2(fwd.Y, -fwd.X);
        float h = s * 0.5f;
        return [c + (fwd + side) * h, c + (fwd - side) * h, c + (-fwd - side) * h, c + (-fwd + side) * h];
    }

    private bool OctInside(BuildingGenerator.Context c, Vector2 centre, float r)
    {
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.PI / 4f;
            if (!Polygon.Contains(c.Site.Outer, centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r)) return false;
        }
        return true;
    }

    /// <summary>A square tower from y0 to y1 with an arched window on each face and a white cornice.</summary>
    private static void Tower(Mesh m, Vector2[] sq, Vector2 tc, string body, float y0, float y1, float s)
    {
        for (int k = 0; k < 4; k++)
        {
            var a = sq[k]; var bb = sq[(k + 1) % 4];
            var dir = Vector2.Normalize(bb - a);
            var outN = new Vector2(dir.Y, -dir.X);
            if (Vector2.Dot(outN, a - tc) < 0) outN = -outN;
            var f = new Kit.Frame(new Vector3(a.X, 0, a.Y), new Vector3(dir.X, 0, dir.Y), new Vector3(outN.X, 0, outN.Y));
            float ww = MathF.Min(0.32f, s * 0.18f), wy0 = y1 - 1.9f, wy1 = y1 - 0.7f;
            Kit.WallWithOpening(m, body, "reveal", "glass", f, 0, s, y0, y1, s * 0.5f - ww, s * 0.5f + ww, wy0, wy1, 0.1f);
            m.AddPolygon("trim", [f.P(s * 0.5f - ww - 0.07f, wy1, 0.03f), f.P(s * 0.5f + ww + 0.07f, wy1, 0.03f), f.P(s * 0.5f, wy1 + ww, 0.03f)], f.Out);
            Kit.Box(m, "trim", f, -0.05f, s + 0.05f, y1 - 0.02f, y1 + 0.12f, 0, 0.07f);
        }
    }

    /// <summary>A small dark cupola (a squat onion); returns its tip's height.</summary>
    private static float Cupola(Mesh m, Vector2 c, float r, float y0, Vector2 fwd) => Dome(m, c, r, y0, Onionish);

    private static float Dome(Mesh m, Vector2 c, float r, float y0, (float R, float Y)[] profile)
    {
        const int sides = 12;
        for (int i = 0; i + 1 < profile.Length; i++)
        {
            float ra = profile[i].R * r, rb = profile[i + 1].R * r, ya = y0 + profile[i].Y * r, yb = y0 + profile[i + 1].Y * r;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * MathF.Tau / sides, a1 = (k + 1) * MathF.Tau / sides, am = (a0 + a1) * 0.5f;
                m.AddQuad("dome",
                    new Vector3(c.X + MathF.Cos(a0) * ra, ya, c.Y + MathF.Sin(a0) * ra), new Vector3(c.X + MathF.Cos(a1) * ra, ya, c.Y + MathF.Sin(a1) * ra),
                    new Vector3(c.X + MathF.Cos(a1) * rb, yb, c.Y + MathF.Sin(a1) * rb), new Vector3(c.X + MathF.Cos(a0) * rb, yb, c.Y + MathF.Sin(a0) * rb),
                    new Vector3(MathF.Cos(am), 0.4f, MathF.Sin(am)));
            }
        }
        return y0 + profile[^1].Y * r;
    }

    /// <summary>The finial: a spike, two balls (small octagonal boxes) and a crescent opening upward.</summary>
    private static void Finial(Mesh m, Vector2 c, float y0, float h, Vector2 fwd)
    {
        var side = new Vector2(fwd.Y, -fwd.X);
        var f = new Kit.Frame(new Vector3(c.X, 0, c.Y), new Vector3(side.X, 0, side.Y), new Vector3(fwd.X, 0, fwd.Y));
        float t = MathF.Max(0.03f, h * 0.03f);
        Kit.Box(m, "finial", f, -t, t, y0, y0 + h * 0.62f, -t, t);
        foreach (float yb in new[] { y0 + h * 0.2f, y0 + h * 0.42f })
        {
            float br = h * 0.07f;
            Kit.Box(m, "finial", f, -br, br, yb - br, yb + br, -br, br);
        }
        // the crescent: an arc of seven short bars, horns up
        float cr = h * 0.2f, cyc = y0 + h * 0.62f + cr;
        for (int k = 0; k < 7; k++)
        {
            float a0 = MathF.PI * (1.15f + k * 0.1f), a1 = MathF.PI * (1.15f + (k + 1) * 0.1f);
            var p0 = f.P(MathF.Cos(a0) * cr, cyc + MathF.Sin(a0) * cr, 0); var p1 = f.P(MathF.Cos(a1) * cr, cyc + MathF.Sin(a1) * cr, 0);
            var up = Vector3.UnitY * (t * 1.6f);
            m.AddQuad("finial", p0 - up * 0.5f, p1 - up * 0.5f, p1 + up * 0.5f, p0 + up * 0.5f, f.Out);
            m.AddQuad("finial", p1 - up * 0.5f, p0 - up * 0.5f, p0 + up * 0.5f, p1 + up * 0.5f, -f.Out);
        }
    }

    /// <summary>The porch at the door: Kruszyniany's arched one, or a little gabled one with posts.</summary>
    private void Porch(Mesh m, Kit.Frame f, float du, float hw, float y0, float dh, float ground, string body)
    {
        float pw = hw + 0.55f, depth = 0.95f;
        Kit.Box(m, "steps", f, du - pw, du + pw, ground - 0.2f, y0, 0, depth);
        if (_kind == Kind.Modern)
        {
            Kit.Box(m, "trim", f, du - pw, du + pw, y0 + dh + 0.2f, y0 + dh + 0.35f, 0, depth);
            return;
        }
        float eave = y0 + dh + 0.35f;
        if (_kind == Kind.Long)
        {
            // closed sides and a barrel roof (a half octagon)
            Kit.Box(m, body, f, du - pw, du - pw + 0.1f, y0, eave, 0.05f, depth);
            Kit.Box(m, body, f, du + pw - 0.1f, du + pw, y0, eave, 0.05f, depth);
            const int n = 6;
            for (int k = 0; k < n; k++)
            {
                float a0 = MathF.PI * k / n, a1 = MathF.PI * (k + 1) / n;
                float ua = du + MathF.Cos(a0) * pw, ub = du + MathF.Cos(a1) * pw, ya = eave + MathF.Sin(a0) * pw * 0.55f, yb = eave + MathF.Sin(a1) * pw * 0.55f;
                m.AddQuad("roof", f.P(ua, ya, 0), f.P(ua, ya, depth), f.P(ub, yb, depth), f.P(ub, yb, 0), Vector3.UnitY);
                m.AddQuad("soffit", f.P(ub, yb - 0.05f, 0), f.P(ub, yb - 0.05f, depth), f.P(ua, ya - 0.05f, depth), f.P(ua, ya - 0.05f, 0), -Vector3.UnitY);
            }
            // the arched front: a half octagon of wall over the opening
            var pts = new List<Vector3>();
            for (int k = 0; k <= n; k++) { float a = MathF.PI * k / n; pts.Add(f.P(du + MathF.Cos(a) * pw, eave + MathF.Sin(a) * pw * 0.55f, depth - 0.02f)); }
            m.AddPolygon(body, pts, f.Out);
            return;
        }
        // the square kind: posts, a gable, fretwork-white edge
        foreach (float pu in new[] { du - pw + 0.08f, du + pw - 0.08f })
            Kit.Box(m, "trim", f, pu - 0.06f, pu + 0.06f, y0, eave, depth - 0.14f, depth - 0.02f);
        float ridge = eave + pw * 0.55f;
        m.AddQuad("roof", f.P(du - pw - 0.1f, eave - 0.05f, 0), f.P(du - pw - 0.1f, eave - 0.05f, depth + 0.05f), f.P(du, ridge, depth + 0.05f), f.P(du, ridge, 0), Vector3.UnitY);
        m.AddQuad("roof", f.P(du, ridge, 0), f.P(du, ridge, depth + 0.05f), f.P(du + pw + 0.1f, eave - 0.05f, depth + 0.05f), f.P(du + pw + 0.1f, eave - 0.05f, 0), Vector3.UnitY);
        m.AddPolygon(body, [f.P(du - pw, eave, depth - 0.02f), f.P(du + pw, eave, depth - 0.02f), f.P(du, ridge - 0.06f, depth - 0.02f)], f.Out);
        m.AddPolygon("trim", [f.P(du - 0.2f, eave + 0.06f, depth), f.P(du + 0.2f, eave + 0.06f, depth), f.P(du, eave + 0.3f, depth)], f.Out);
    }
}
