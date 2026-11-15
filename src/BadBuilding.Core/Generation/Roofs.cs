using System.Numerics;
using BadBuilding.Geometry;

namespace BadBuilding.Generation;

/// <summary>The roof shapes the library builds (D13). Anything else mapped is drawn as the nearest of these.</summary>
public enum RoofShape { Flat, Gabled, Hipped, Pyramidal, Skillion }

/// <summary>
/// Roofs (D13, Noor Q6): gabled, hipped and pyramidal roofs over the footprint's minimum-area rectangle (for footprints
/// that are nearly rectangles), with eaves overhanging the walls and gable walls closing the ends; and the mono-pitched
/// (skillion) roof as a sloped surface over any footprint, falling away from a line (a kamienica's roof falls from the
/// street to the yard, hidden behind the street attic). Walls meet a sloped roof with <see cref="Kit.WallTop"/>.
/// </summary>
public static class Roofs
{
    /// <summary>Nearly a rectangle: footprint area over its minimum rectangle's area at least this.</summary>
    public const float RectangularEnough = 0.86f;

    /// <summary>The minimum-area bounding rectangle: centre, unit axes (U along the long side) and half sizes; Fill is
    /// the footprint's share of it (1 for a rectangle).</summary>
    public readonly record struct Rect(Vector2 Centre, Vector2 U, Vector2 V, float HalfL, float HalfW, float Fill)
    {
        public Vector2 At(float u, float v) => Centre + U * u + V * v;
    }

    public static RoofShape Parse(string? s) => s switch
    {
        "gabled" or "gambrel" or "mansard" or "saltbox" => RoofShape.Gabled,
        "hipped" or "half-hipped" or "side_hipped" => RoofShape.Hipped,
        "pyramidal" => RoofShape.Pyramidal,
        "skillion" or "lean_to" or "mono_pitched" => RoofShape.Skillion,
        _ => RoofShape.Flat,   // flat, and what is not built yet (domes, onions, round): drawn flat, reported
    };

    public static Rect MinRect(IReadOnlyList<Vector2> ring)
    {
        var hull = Hull(ring);
        Rect best = default; float bestArea = float.MaxValue;
        for (int i = 0; i < hull.Count; i++)
        {
            var d = hull[(i + 1) % hull.Count] - hull[i];
            if (d.LengthSquared() < 1e-6f) continue;
            d = Vector2.Normalize(d);
            var n = new Vector2(-d.Y, d.X);
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            foreach (var p in hull)
            {
                float u = Vector2.Dot(p, d), v = Vector2.Dot(p, n);
                u0 = MathF.Min(u0, u); u1 = MathF.Max(u1, u); v0 = MathF.Min(v0, v); v1 = MathF.Max(v1, v);
            }
            float area = (u1 - u0) * (v1 - v0);
            if (area >= bestArea) continue;
            bestArea = area;
            var centre = d * ((u0 + u1) * 0.5f) + n * ((v0 + v1) * 0.5f);
            float hl = (u1 - u0) * 0.5f, hw = (v1 - v0) * 0.5f;
            best = hl >= hw ? new Rect(centre, d, n, hl, hw, 0) : new Rect(centre, n, -d, hw, hl, 0);
        }
        return best with { Fill = bestArea > 0 ? Polygon.Area(ring) / bestArea : 0 };
    }

    private static List<Vector2> Hull(IReadOnlyList<Vector2> pts)
    {
        var p = pts.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        var h = new List<Vector2>();
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        foreach (var q in p) { while (h.Count >= 2 && Cross(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var q = p[i]; while (h.Count >= lower && Cross(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    /// <summary>The rise (eave to ridge) of a pitched roof over <paramref name="r"/> at <paramref name="pitchDeg"/>.</summary>
    public static float Rise(Rect r, float pitchDeg) => r.HalfW * MathF.Tan(pitchDeg * MathF.PI / 180f);

    /// <summary>
    /// A gabled, hipped or pyramidal roof over <paramref name="r"/>: eaves at <paramref name="eave"/>, the ridge
    /// <paramref name="rise"/> higher, the roof planes running <paramref name="overhang"/> past the walls (dropping
    /// with the slope), seen from below as a soffit; gabled roofs close their ends with gable walls (<paramref name="gableRole"/>).
    /// </summary>
    public static void Pitched(Mesh m, string roofRole, string soffitRole, string gableRole, Rect r, RoofShape shape, float eave, float rise, float overhang)
    {
        float L = r.HalfL, W = r.HalfW, o = overhang, slope = rise / MathF.Max(W, 0.01f);
        float lo = eave - o * slope, top = eave + rise;
        Vector3 P(float u, float v, float y) { var q = r.At(u, v); return new Vector3(q.X, y, q.Y); }
        var up = Vector3.UnitY;
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // the upper side, and the same plane seen from below under the eaves
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            if (n.Y < 0) n = -n;
            m.AddQuad(roofRole, a, b, c, d, n);
            m.AddQuad(soffitRole, a, b, c, d, -n);
        }
        if (shape == RoofShape.Gabled)
        {
            float u0 = -L - o, u1 = L + o;
            Face(P(u0, -W - o, lo), P(u1, -W - o, lo), P(u1, 0, top), P(u0, 0, top));
            Face(P(u0, W + o, lo), P(u1, W + o, lo), P(u1, 0, top), P(u0, 0, top));
            foreach (float s in new[] { -1f, 1f })
            {
                var outward = new Vector3(r.U.X, 0, r.U.Y) * s;
                m.AddPolygon(gableRole, [P(s * L, -W, eave), P(s * L, W, eave), P(s * L, 0, top)], outward);
            }
            return;
        }
        // hipped (and pyramidal: no ridge): every plane rises from its eave at the same slope
        float ridge = shape == RoofShape.Pyramidal ? 0 : MathF.Max(0, L - W);
        Face(P(-L - o, -W - o, lo), P(L + o, -W - o, lo), P(ridge, 0, top), P(-ridge, 0, top));
        Face(P(-L - o, W + o, lo), P(L + o, W + o, lo), P(ridge, 0, top), P(-ridge, 0, top));
        var t = up;
        m.AddPolygon(roofRole, [P(-L - o, -W - o, lo), P(-L - o, W + o, lo), P(-ridge, 0, top)], t);
        m.AddPolygon(roofRole, [P(L + o, -W - o, lo), P(L + o, W + o, lo), P(ridge, 0, top)], t);
        m.AddPolygon(soffitRole, [P(-L - o, -W - o, lo), P(-L - o, W + o, lo), P(-ridge, 0, top)], -t);
        m.AddPolygon(soffitRole, [P(L + o, -W - o, lo), P(L + o, W + o, lo), P(ridge, 0, top)], -t);
    }

    /// <summary>
    /// A mono-pitched roof surface over the footprint (with its courtyards): at each point <paramref name="height"/>
    /// gives the roof's height; triangulated like the flat roof, so it fits any outline.
    /// </summary>
    public static void Sloped(Mesh m, string role, IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes, Func<Vector2, float> height)
    {
        var pts = Polygon.Concat(outer, holes);
        var tris = Polygon.Triangulate(outer, holes);
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            Vector3 V(int i) => new(pts[i].X, height(pts[i]), pts[i].Y);
            m.AddPolygon(role, [V(tris[t]), V(tris[t + 1]), V(tris[t + 2])], Vector3.UnitY);
        }
    }
}
