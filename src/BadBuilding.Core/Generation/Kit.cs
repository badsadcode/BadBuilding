using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Generation;

/// <summary>
/// Geometry pieces every style uses, in a wall's own frame: u along the wall from its first corner, y up, w outward
/// (positive in front of the facade). Openings are recessed (reveals, a sill line) so windows read as holes in the wall,
/// not stickers on it.
/// </summary>
public static class Kit
{
    /// <summary>A wall's frame: the point at u = 0, y = 0 and its unit axes.</summary>
    public readonly record struct Frame(Vector3 Origin, Vector3 U, Vector3 Out)
    {
        public Vector3 P(float u, float y, float w = 0f) => Origin + U * u + Vector3.UnitY * y + Out * w;

        public static Frame Of(WallEdge e)
        {
            var d = e.Dir; var o = e.Out;
            return new Frame(new Vector3(e.A.X, 0, e.A.Y), new Vector3(d.X, 0, d.Y), new Vector3(o.X, 0, o.Y));
        }
    }

    /// <summary>A plain rectangle of wall from (u0, y0) to (u1, y1), facing out.</summary>
    public static void Wall(Mesh m, string role, Frame f, float u0, float u1, float y0, float y1, float w = 0f)
    {
        if (u1 - u0 < 1e-3f || y1 - y0 < 1e-3f) return;
        m.AddQuad(role, f.P(u0, y0, w), f.P(u1, y0, w), f.P(u1, y1, w), f.P(u0, y1, w), f.Out);
    }

    /// <summary>
    /// A rectangle of wall with one rectangular opening (ou0..ou1, oy0..oy1) cut through it: the wall around the hole,
    /// four reveals going <paramref name="depth"/> back, and the infill (glass, a door) at the back.
    /// </summary>
    public static void WallWithOpening(Mesh m, string wallRole, string revealRole, string infillRole, Frame f,
        float u0, float u1, float y0, float y1, float ou0, float ou1, float oy0, float oy1, float depth)
    {
        ou0 = Math.Clamp(ou0, u0, u1); ou1 = Math.Clamp(ou1, u0, u1);
        oy0 = Math.Clamp(oy0, y0, y1); oy1 = Math.Clamp(oy1, y0, y1);
        if (ou1 - ou0 < 0.05f || oy1 - oy0 < 0.05f) { Wall(m, wallRole, f, u0, u1, y0, y1); return; }
        Wall(m, wallRole, f, u0, u1, y0, oy0);          // below
        Wall(m, wallRole, f, u0, u1, oy1, y1);          // above
        Wall(m, wallRole, f, u0, ou0, oy0, oy1);        // left
        Wall(m, wallRole, f, ou1, u1, oy0, oy1);        // right
        var back = -depth;
        // reveals, facing into the opening
        m.AddQuad(revealRole, f.P(ou0, oy0, 0), f.P(ou0, oy0, back), f.P(ou1, oy0, back), f.P(ou1, oy0, 0), Vector3.UnitY);           // sill
        m.AddQuad(revealRole, f.P(ou0, oy1, 0), f.P(ou1, oy1, 0), f.P(ou1, oy1, back), f.P(ou0, oy1, back), -Vector3.UnitY);          // head
        m.AddQuad(revealRole, f.P(ou0, oy0, 0), f.P(ou0, oy1, 0), f.P(ou0, oy1, back), f.P(ou0, oy0, back), f.U);                     // left jamb
        m.AddQuad(revealRole, f.P(ou1, oy0, 0), f.P(ou1, oy0, back), f.P(ou1, oy1, back), f.P(ou1, oy1, 0), -f.U);                    // right jamb
        m.AddQuad(infillRole, f.P(ou0, oy0, back), f.P(ou1, oy0, back), f.P(ou1, oy1, back), f.P(ou0, oy1, back), f.Out);
    }

    /// <summary>
    /// A rectangle of wall with open cuts (passages) through it: each cut spans its u range from oy0 to oy1 and is left
    /// open, no reveal or infill (the passage brings its own sides). Cuts outside the rectangle are ignored.
    /// </summary>
    public static void WallCut(Mesh m, string role, Frame f, float u0, float u1, float y0, float y1,
        IEnumerable<(float U0, float U1)> cuts, float oy0, float oy1)
    {
        oy0 = Math.Clamp(oy0, y0, y1); oy1 = Math.Clamp(oy1, y0, y1);
        var open = cuts.Select(k => (A: MathF.Max(k.U0, u0), B: MathF.Min(k.U1, u1))).Where(k => k.B - k.A > 0.01f).OrderBy(k => k.A).ToList();
        if (open.Count == 0 || oy1 - oy0 < 0.01f) { Wall(m, role, f, u0, u1, y0, y1); return; }
        Wall(m, role, f, u0, u1, y0, oy0);
        Wall(m, role, f, u0, u1, oy1, y1);
        float u = u0;
        foreach (var (a, b) in open) { Wall(m, role, f, u, a, oy0, oy1); u = MathF.Max(u, b); }
        Wall(m, role, f, u, u1, oy0, oy1);
    }

    /// <summary>Whether u0..u1 overlaps any of the cuts.</summary>
    public static bool Overlaps(IEnumerable<(float U0, float U1)> cuts, float u0, float u1) => cuts.Any(k => k.U1 > u0 + 0.01f && k.U0 < u1 - 0.01f);

    /// <summary>A box in a wall's frame: u0..u1 along, y0..y1 up, w0..w1 out from the facade.</summary>
    public static void Box(Mesh m, string role, Frame f, float u0, float u1, float y0, float y1, float w0, float w1)
    {
        Vector3 P(float u, float y, float w) => f.P(u, y, w);
        m.AddQuad(role, P(u0, y0, w1), P(u1, y0, w1), P(u1, y1, w1), P(u0, y1, w1), f.Out);       // front
        m.AddQuad(role, P(u1, y0, w0), P(u0, y0, w0), P(u0, y1, w0), P(u1, y1, w0), -f.Out);      // back
        m.AddQuad(role, P(u0, y1, w0), P(u0, y1, w1), P(u1, y1, w1), P(u1, y1, w0), Vector3.UnitY); // top
        m.AddQuad(role, P(u0, y0, w0), P(u1, y0, w0), P(u1, y0, w1), P(u0, y0, w1), -Vector3.UnitY); // bottom
        m.AddQuad(role, P(u0, y0, w0), P(u0, y0, w1), P(u0, y1, w1), P(u0, y1, w0), -f.U);          // left
        m.AddQuad(role, P(u1, y0, w1), P(u1, y0, w0), P(u1, y1, w0), P(u1, y1, w1), f.U);           // right
    }

    /// <summary>An axis-aligned box in building space.</summary>
    public static void BoxXZ(Mesh m, string role, Vector3 min, Vector3 max)
    {
        var f = new Frame(new Vector3(min.X, 0, max.Z), Vector3.UnitX, Vector3.UnitZ);
        Box(m, role, f, 0, max.X - min.X, min.Y, max.Y, min.Z - max.Z, 0);
    }

    /// <summary>A flat slab over the footprint (outer ring with holes) between heights y0 and y1, sides included.</summary>
    public static void Slab(Mesh m, string role, IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes, float y0, float y1, bool sides = true)
    {
        var pts = Polygon.Concat(outer, holes);
        var tris = Polygon.Triangulate(outer, holes);
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            Vector3 V(int i, float y) => new(pts[i].X, y, pts[i].Y);
            m.AddPolygon(role, [V(tris[t], y1), V(tris[t + 1], y1), V(tris[t + 2], y1)], Vector3.UnitY);
            if (y1 - y0 > 1e-3f) m.AddPolygon(role, [V(tris[t], y0), V(tris[t + 1], y0), V(tris[t + 2], y0)], -Vector3.UnitY);
        }
        if (!sides || y1 - y0 < 1e-3f) return;
        void Ring(IReadOnlyList<Vector2> r)
        {
            for (int i = 0; i < r.Count; i++)
            {
                var a = r[i]; var b = r[(i + 1) % r.Count];
                var d = Vector2.Normalize(b - a);
                var o = new Vector3(-d.Y, 0, d.X);
                m.AddQuad(role, new(a.X, y0, a.Y), new(b.X, y0, b.Y), new(b.X, y1, b.Y), new(a.X, y1, a.Y), o);
            }
        }
        Ring(outer);
        foreach (var h in holes) Ring(h);
    }

    /// <summary>
    /// A parapet: a low wall of thickness <paramref name="t"/> standing inside the ring's edge from y0 to y1, with a cap.
    /// </summary>
    public static void Parapet(Mesh m, string role, IReadOnlyList<Vector2> ring, float y0, float y1, float t)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            float len = Vector2.Distance(a, b);
            if (len < 0.05f) continue;
            var e = new WallEdge(0, i, a, b, Exposure.Side);
            var f = Frame.Of(e);
            Box(m, role, f, 0, len, y0, y1, -t, 0);
        }
    }

    public static MaterialDef Mat(string role, string hex, float rough = 0.85f, float metal = 0f, float alpha = 1f)
    {
        var c = Convert.FromHexString(hex.TrimStart('#'));
        static float Lin(byte v) { float s = v / 255f; return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f); }
        return new MaterialDef(role, new Vector3(Lin(c[0]), Lin(c[1]), Lin(c[2])), rough, metal, alpha);
    }
}
