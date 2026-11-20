using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The Orthodox church, the cerkiew (D41), only where the map says so: a church whose OSM <c>denomination</c> is
/// orthodox (orthodox, polish_orthodox, russian_orthodox, …). Built as Podlasie's village churches stand (from
/// photographs of Podlaskie's cerkwie), on the mapped footprint, oriented east:
/// * the body: walls on the outline under a gabled roof along its length; tall windows in white frames, the door at the
///   west end;
/// * over the nave, a raised octagonal drum with an onion dome; over the sanctuary at the east end, a small onion;
/// * at the west end, a square tower over the entrance, rising past the ridge, under a tented spire or a small onion;
/// * on every dome and spire, the eight-pointed Orthodox cross (three bars, the lowest slanted).
/// Wooden (boards painted blue, sky blue, pale blue-grey, green, white, or plain honey wood) when mapped as wood, and
/// most often in Podlasie (the "podlasie" preset, D40); else masonry (yellow brick, white or cream plaster).
/// Domes in the roof's colour, gilded, blue or dark. Everything inside the footprint but the details (≤ 1 m).
/// </summary>
public sealed class Orthodox : IStyle
{
    public const string Name = "orthodox_church";
    public string Id => Name;

    private readonly bool _wood;
    private readonly bool _big;

    public Orthodox(BuildingGenerator.Context c)
    {
        var r = c.Request;
        _big = c.Site.Area > 450f;
        _wood = r.Material switch
        {
            "wood" => true,
            "brick" or "stone" or "concrete" or "plaster" or "sandstone" => false,
            _ => !_big && Rng.Stream(r.Seed, "orthodox.material").Chance(r.Preset == House.Podlasie ? 0.75 : 0.35),
        };
    }

    public static bool IsOrthodox(string? denomination) => denomination != null && denomination.Contains("orthodox", StringComparison.OrdinalIgnoreCase);

    /// <summary>A church (or a chapel) the map calls orthodox, big enough for a nave, standing on the ground.</summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (!IsOrthodox(r.Denomination) || r.Raised) return false;
        // Christian only: denomination=orthodox is also Orthodox Judaism (a synagogue tagged religion=jewish)
        if (r.Religion is not (null or "christian")) return false;
        if (r.Type is not ("church" or "chapel" or "cathedral" or "religious" or "yes" or "temple" or "monastery")) return false;
        return c.Site.Area is >= 30f and <= 2500f && c.Site.Holes.Count == 0;
    }

    public float Plinth => 0.4f;
    public float RoofExtra => 0.3f;
    public int InferLevels(BuildingGenerator.Context c) => 1;
    public float StoreyHeight(BuildingGenerator.Context c) => _big ? 10f : _wood ? 6.2f : 7.5f;
    public string InferRoof(BuildingGenerator.Context c) => "gabled";
    public float RoofPitch(BuildingGenerator.Context c) => _wood ? 38f : 32f;

    // ---- looks ---------------------------------------------------------------------------------------------------------

    private string _name = "";
    private bool _towerOnion;

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, name;
        if (_wood)
        {
            (name, wall) = L.Weighted<(string, string)>([(("blue", "3f6fb5"), 5), (("sky blue", "6fa8d8"), 3), (("pale blue-grey", "a9bccb"), 2),
                (("honey wood", "a8743a"), 2), (("green", "4f7a4a"), 1), (("white", "e8e6df"), 1)]);
            m["timber"] = Kit.Mat("timber", wall, 0.9f);
        }
        else
            (name, wall) = L.Weighted<(string, string)>([(("yellow brick", "c9a86a"), 3), (("white plaster", "e9e5da"), 3), (("cream plaster", "e6d6ad"), 2), (("pink plaster", "d9b3a3"), 1)]);
        string roof = L.Weighted<string>([("9aa0a3", 4), ("4c7a5a", 2), ("41618c", 1), ("3d4245", 1)]);
        string dome = L.Weighted<string>([(roof, 3), ("c9a03a", 2), ("3f6fb5", 1), ("3a3d42", 2)]);
        _towerOnion = L.Chance(0.5);
        m["wall"] = Kit.Mat("wall", wall, _wood ? 0.9f : 0.85f);
        m["plinth"] = Kit.Mat("plinth", L.Weighted<string>([("8c877e", 2), ("6f6a62", 1)]), 0.95f);
        m["reveal"] = Kit.Mat("reveal", _wood ? "ebe8de" : "d8d3c6", 0.85f);
        m["frame"] = Kit.Mat("frame", "ebe8de", 0.6f);
        m["trim"] = Kit.Mat("trim", "ebe8de", 0.7f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["louvre"] = Kit.Mat("louvre", "2b2a28", 0.9f);
        m["door"] = Kit.Mat("door", L.Weighted<string>([("5a3e2a", 2), ("3f6fb5", 1), ("ebe8de", 1)]), 0.7f);
        m["roof"] = Kit.Mat("roof", roof, 0.55f, 0.35f);
        m["dome"] = Kit.Mat("dome", dome, dome == "c9a03a" ? 0.3f : 0.5f, dome == "c9a03a" ? 0.9f : 0.35f);
        m["cross"] = Kit.Mat("cross", "d4b04a", 0.3f, 0.9f);
        m["soffit"] = Kit.Mat("soffit", _wood ? wall : "8a857c", 0.9f);
        m["steps"] = Kit.Mat("steps", "8c877e", 0.95f);
        _name = $"orthodox church, {(_wood ? "wooden" : "masonry")}, {name}, {(dome == "c9a03a" ? "gilded" : "plain")} domes";
        c.Plan = c.Plan with { Palette = _name };
        c.Building.Plan = c.Plan;
    }

    // ---- building ------------------------------------------------------------------------------------------------------

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        string body = _wood ? "timber" : "wall";
        var rect = plan.RoofRect;
        // east: the long axis's end that lies more to the east (+X)
        var east = rect.U.X >= 0 ? rect.U : -rect.U;
        Vector2 At(float a, float v) => rect.Centre + east * a + rect.V * v;
        float HL = rect.HalfL, HW = rect.HalfW;

        // the door: the edge facing west most squarely
        int doorEdge = -1; float best = 0.3f;
        for (int i = 0; i < plan.Edges.Count; i++)
        {
            var e = plan.Edges[i];
            if (e.Ring != 0 || e.Length < 2f) continue;
            float d = -Vector2.Dot(e.Out, east);
            if (d > best) { best = d; doorEdge = i; }
        }

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            float h = top - datum;
            if (ei == doorEdge)
            {
                float du = len * 0.5f, hw = MathF.Min(0.8f, len * 0.2f), dh = MathF.Min(2.6f, h - 0.6f);
                Kit.WallWithOpening(wall.Mesh, body, "reveal", "door", f, 0, len, datum, top, du - hw, du + hw, datum, datum + dh, 0.18f);
                Kit.Box(trim.Mesh, "trim", f, du - hw - 0.15f, du + hw + 0.15f, datum + dh, datum + dh + 0.18f, 0, 0.05f);
                b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}"), "entrance", f.P(du, datum, 0.05f), f.Out));
                float rise = datum - ground;
                int n = Math.Clamp((int)MathF.Round(rise / 0.17f), 0, 5);
                for (int k = 0; k < n; k++)
                    Kit.Box(trim.Mesh, "steps", f, du - hw - 0.4f, du + hw + 0.4f, ground - 0.2f, datum - k * (rise / MathF.Max(n, 1)), 0, MathF.Min(1.0f, 0.3f * (k + 1)));
                continue;
            }
            // tall windows, about every 3.2 m, on walls long enough
            int bays = len >= 2.6f ? Math.Max(1, (int)MathF.Round(len / (_wood ? 3.8f : 3.3f))) : 0;
            if (bays == 0) { Kit.Wall(wall.Mesh, body, f, 0, len, datum, top); continue; }
            float bw = len / bays;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                // wood: house-sized windows, about 2 m tall; masonry: tall ones
                float ww = MathF.Min(_wood ? 0.55f : 0.5f, bw * 0.2f);
                float ys = datum + (_wood ? MathF.Min(1.5f, h * 0.25f) : MathF.Min(1.3f, h * 0.22f));
                float yh = _wood ? MathF.Min(ys + 2.0f, top - 0.9f) : top - MathF.Max(0.9f, h * 0.16f);
                if (yh - ys < 1f) { Kit.Wall(wall.Mesh, body, f, u0, u1, datum, top); continue; }
                Kit.WallWithOpening(wall.Mesh, body, "reveal", "glass", f, u0, u1, datum, top, uc - ww, uc + ww, ys, yh, 0.15f);
                Kit.Box(trim.Mesh, "frame", f, uc - 0.03f, uc + 0.03f, ys, yh, -0.12f, -0.08f);
                Kit.Box(trim.Mesh, "frame", f, uc - ww, uc + ww, ys + (yh - ys) * 0.7f - 0.03f, ys + (yh - ys) * 0.7f + 0.03f, -0.12f, -0.08f);
                // a white surround, peaked over the window (on wood a board, on masonry a moulding)
                Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.1f, uc - ww, ys - 0.05f, yh, 0, 0.04f);
                Kit.Box(trim.Mesh, "trim", f, uc + ww, uc + ww + 0.1f, ys - 0.05f, yh, 0, 0.04f);
                Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.15f, uc + ww + 0.15f, ys - 0.12f, ys - 0.04f, 0, 0.06f);
                trim.Mesh.AddPolygon("trim", [f.P(uc - ww - 0.16f, yh, 0.045f), f.P(uc + ww + 0.16f, yh, 0.045f), f.P(uc, yh + 0.3f, 0.045f)], f.Out);
            }
            // corner boards on wood
            if (_wood && len >= 1.2f)
            {
                Kit.Box(trim.Mesh, "trim", f, 0, 0.15f, datum, top, 0, 0.03f);
                Kit.Box(trim.Mesh, "trim", f, len - 0.15f, len, datum, top, 0, 0.03f);
            }
        }

        // the roof
        var roofM = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        float ridge = top;
        if (plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped)
        {
            Roofs.Pitched(roofM.Mesh, "roof", "soffit", body, plan, top, 0.45f * plan.Eaves);
            Kit.Slab(roofM.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
            ridge = top + (plan.RoofWings.Count > 0 ? plan.RoofWings.Max(w => w.Rise) : plan.RoofRise);
        }
        else
        {
            Kit.Slab(roofM.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
            ridge = top + 0.1f;
        }

        var domes = b.Add(c.Id("domes"), "domes", ModuleTier.Secondary);
        var m = domes.Mesh;
        bool Inside(Vector2 p) => Polygon.Contains(c.Site.Outer, p);

        // over the nave: a raised octagonal drum and the big onion
        var nave = At(-HL * 0.05f, 0);
        float r = Math.Clamp(HW * 0.55f, 1.1f, _big ? 4.5f : 3.2f);
        if (Inside(nave) && OctagonInside(c, nave, r))
        {
            float drumTop = ridge + MathF.Max(1.3f, r * 0.9f);
            Octagon(m, body, nave, r, top - 0.2f, drumTop, east);
            // a band of small windows round the drum, and a cornice under the dome
            for (int k = 0; k < 8; k += 2)
            {
                float ang = k * MathF.PI / 4f + MathF.PI / 8f;
                var n = Dir(ang, east);
                var fc = new Kit.Frame(new Vector3(nave.X + n.X * r * 0.924f, 0, nave.Y + n.Y * r * 0.924f), new Vector3(-n.Y, 0, n.X), new Vector3(n.X, 0, n.Y));
                float yw = drumTop - 0.5f, hw2 = MathF.Min(0.3f, r * 0.2f);
                Kit.Box(m, "glass", fc, -hw2, hw2, yw - MathF.Min(1.1f, r * 0.6f), yw, 0, 0.02f);
                Kit.Box(m, "trim", fc, -hw2 - 0.06f, hw2 + 0.06f, yw, yw + 0.08f, 0, 0.04f);
            }
            Octagon(m, "trim", nave, r + 0.12f, drumTop, drumTop + 0.15f, east);
            float tip = Onion(m, nave, r * 0.88f, drumTop + 0.15f);
            Cross(m, nave, tip, MathF.Min(2.2f, 0.9f + r * 0.35f), east);
        }

        // over the sanctuary, east: a small drum and onion on the ridge
        var apse = At(HL - MathF.Max(1.4f, HL * 0.25f), 0);
        float r2 = Math.Clamp(HW * 0.22f, 0.5f, 1.3f);
        if (HL > 4f && Inside(apse))
        {
            float b0 = ridge - 0.4f, b1 = ridge + 0.7f;
            Octagon(m, body, apse, r2, b0, b1, east);
            float tip = Onion(m, apse, r2 * 0.95f, b1);
            Cross(m, apse, tip, 0.9f + r2 * 0.3f, east);
        }

        // west: the tower over the entrance
        float t = Math.Clamp(MathF.Min(HW * 1.3f, HL * 0.55f), 2.2f, _big ? 7f : 4.8f);
        var tc = At(-HL + t * 0.5f + 0.05f, 0);
        var tower = new[] { tc + (east + rect.V) * (t * 0.5f), tc + (east - rect.V) * (t * 0.5f), tc + (-east - rect.V) * (t * 0.5f), tc + (-east + rect.V) * (t * 0.5f) };
        if (HL > 3.5f && tower.All(Inside))
        {
            var tw = b.Add(c.Id("tower"), "tower", ModuleTier.Primary).Mesh;
            float tTop = MathF.Max(ridge + 2.4f, top + 4.5f), belfry = tTop - 1.6f;
            for (int k = 0; k < 4; k++)
            {
                var a = tower[k]; var bb = tower[(k + 1) % 4];
                var dir = Vector2.Normalize(bb - a);
                var outN = new Vector2(dir.Y, -dir.X);
                if (Vector2.Dot(outN, a - tc) < 0) outN = -outN;
                var f = new Kit.Frame(new Vector3(a.X, 0, a.Y), new Vector3(dir.X, 0, dir.Y), new Vector3(outN.X, 0, outN.Y));
                // only above the main roof where the tower stands inside the body: from the ground for the west face
                Kit.Wall(tw, body, f, 0, t, top - 0.3f, belfry);
                Kit.WallWithOpening(tw, body, "reveal", "louvre", f, 0, t, belfry, tTop, t * 0.5f - MathF.Min(0.45f, t * 0.18f), t * 0.5f + MathF.Min(0.45f, t * 0.18f), belfry + 0.2f, tTop - 0.25f, 0.12f);
                Kit.Box(tw, "trim", f, 0, t, tTop - 0.05f, tTop + 0.12f, 0, 0.08f);
                Kit.Box(tw, "trim", f, 0, t, belfry - 0.1f, belfry, 0, 0.06f);
            }
            // the tower's roof: a tented spire, or a small onion on a little drum
            if (_towerOnion)
            {
                float rr = t * 0.3f;
                PyramidRoof(m, tower, tc, tTop + 0.12f, tTop + 0.9f, 0.15f);
                Octagon(m, body, tc, rr, tTop + 0.4f, tTop + 1.3f, east);
                float tip = Onion(m, tc, rr * 0.95f, tTop + 1.3f);
                Cross(m, tc, tip, 1.0f + rr * 0.3f, east);
            }
            else
            {
                float spire = tTop + 0.12f + t * 1.1f;
                PyramidRoof(m, tower, tc, tTop + 0.12f, spire, 0.15f);
                // a small ball and the cross
                float tip = Onion(m, tc, 0.22f, spire - 0.05f);
                Cross(m, tc, tip, 1.1f, east);
            }
        }
    }

    // ---- pieces --------------------------------------------------------------------------------------------------------

    internal static Vector2 Dir(float ang, Vector2 east)
    {
        var north = new Vector2(east.Y, -east.X);
        return east * MathF.Cos(ang) + north * MathF.Sin(ang);
    }

    private static bool OctagonInside(BuildingGenerator.Context c, Vector2 centre, float r)
    {
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.PI / 4f;
            if (!Polygon.Contains(c.Site.Outer, centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r)) return false;
        }
        return true;
    }

    /// <summary>An octagonal prism (a drum), faces outward.</summary>
    internal static void Octagon(Mesh m, string role, Vector2 centre, float r, float y0, float y1, Vector2 east)
    {
        for (int k = 0; k < 8; k++)
        {
            var a = centre + Dir(k * MathF.PI / 4f, east) * r;
            var bb = centre + Dir((k + 1) * MathF.PI / 4f, east) * r;
            var mid = (a + bb) * 0.5f - centre;
            m.AddQuad(role, new Vector3(a.X, y0, a.Y), new Vector3(bb.X, y0, bb.Y), new Vector3(bb.X, y1, bb.Y), new Vector3(a.X, y1, a.Y), new Vector3(mid.X, 0, mid.Y));
        }
    }

    /// <summary>The onion dome: a lathe of 12 sides on a base of radius <paramref name="r"/>; returns the tip's height.</summary>
    private static float Onion(Mesh m, Vector2 c, float r, float y0)
    {
        // (radius × r, height × r)
        (float R, float Y)[] profile = [(0.8f, 0f), (1.0f, 0.18f), (1.18f, 0.5f), (1.16f, 0.85f), (0.95f, 1.2f), (0.6f, 1.5f), (0.28f, 1.75f), (0.1f, 1.95f), (0.04f, 2.3f)];
        const int sides = 12;
        for (int i = 0; i + 1 < profile.Length; i++)
        {
            float ra = profile[i].R * r, rb = profile[i + 1].R * r, ya = y0 + profile[i].Y * r, yb = y0 + profile[i + 1].Y * r;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * MathF.Tau / sides, a1 = (k + 1) * MathF.Tau / sides, am = (a0 + a1) * 0.5f;
                var p0 = new Vector3(c.X + MathF.Cos(a0) * ra, ya, c.Y + MathF.Sin(a0) * ra);
                var p1 = new Vector3(c.X + MathF.Cos(a1) * ra, ya, c.Y + MathF.Sin(a1) * ra);
                var p2 = new Vector3(c.X + MathF.Cos(a1) * rb, yb, c.Y + MathF.Sin(a1) * rb);
                var p3 = new Vector3(c.X + MathF.Cos(a0) * rb, yb, c.Y + MathF.Sin(a0) * rb);
                m.AddQuad("dome", p0, p1, p2, p3, new Vector3(MathF.Cos(am), (ra - rb) / MathF.Max(yb - ya, 0.01f) * 0.5f, MathF.Sin(am)));
            }
        }
        return y0 + profile[^1].Y * r;
    }

    /// <summary>A four-sided tented roof from a square's corners up to an apex, with an overhang.</summary>
    internal static void PyramidRoof(Mesh m, Vector2[] sq, Vector2 c, float y0, float apex, float overhang)
    {
        var top = new Vector3(c.X, apex, c.Y);
        for (int k = 0; k < 4; k++)
        {
            var a = sq[k] + Vector2.Normalize(sq[k] - c) * overhang; var b = sq[(k + 1) % 4] + Vector2.Normalize(sq[(k + 1) % 4] - c) * overhang;
            var mid = (a + b) * 0.5f - c;
            m.AddPolygon("dome", [new Vector3(a.X, y0, a.Y), new Vector3(b.X, y0, b.Y), top], new Vector3(mid.X, 0.5f, mid.Y));
        }
    }

    /// <summary>The eight-pointed Orthodox cross on a dome's tip: a post, a short bar above, the main bar, and the slanted
    /// foot bar, facing east-west.</summary>
    private static void Cross(Mesh m, Vector2 c, float y0, float h, Vector2 east)
    {
        var n = new Vector2(east.Y, -east.X);
        var f = new Kit.Frame(new Vector3(c.X, 0, c.Y), new Vector3(n.X, 0, n.Y), new Vector3(east.X, 0, east.Y));
        float t = MathF.Max(0.05f, h * 0.045f), w = h * 0.5f;
        Kit.Box(m, "cross", f, -t, t, y0, y0 + h, -t, t);
        Kit.Box(m, "cross", f, -w * 0.32f, w * 0.32f, y0 + h * 0.82f, y0 + h * 0.82f + 2 * t, -t, t);
        Kit.Box(m, "cross", f, -w * 0.5f, w * 0.5f, y0 + h * 0.62f, y0 + h * 0.62f + 2 * t, -t, t);
        // the slanted foot: a thin quad pair, higher on one side
        float yf = y0 + h * 0.25f, s = w * 0.3f;
        var p0 = f.P(-s, yf - s * 0.45f, t); var p1 = f.P(s, yf + s * 0.45f, t);
        m.AddQuad("cross", p0, p1, p1 + Vector3.UnitY * 2 * t, p0 + Vector3.UnitY * 2 * t, f.Out);
        var q0 = f.P(s, yf + s * 0.45f, -t); var q1 = f.P(-s, yf - s * 0.45f, -t);
        m.AddQuad("cross", q0, q1, q1 + Vector3.UnitY * 2 * t, q0 + Vector3.UnitY * 2 * t, -f.Out);
    }
}
