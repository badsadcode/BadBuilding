using System.Numerics;

namespace BadBuilding.Geometry;

/// <summary>
/// Plan-view polygon tools on (x, z) points in metres (+X east, +Z south, D1). Rings are closed implicitly (the last
/// point is not repeated). The outer ring of a footprint is kept counter-clockwise as seen from above (+Y), holes
/// clockwise.
/// </summary>
public static class Polygon
{
    /// <summary>Signed area, positive when counter-clockwise seen from above. (With +Z south, "seen from above" flips
    /// the usual x–y sign: a ring going east, then north (−Z), then west is counter-clockwise.)</summary>
    public static float SignedArea(IReadOnlyList<Vector2> ring)
    {
        double a = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            a += (double)ring[j].X * ring[i].Y - (double)ring[i].X * ring[j].Y;
        return (float)(-a * 0.5);
    }

    public static float Area(IReadOnlyList<Vector2> ring) => MathF.Abs(SignedArea(ring));

    public static float Perimeter(IReadOnlyList<Vector2> ring)
    {
        float p = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++) p += Vector2.Distance(ring[j], ring[i]);
        return p;
    }

    /// <summary>
    /// Drops repeated points, a repeated closing point and points (nearly) on the line between their neighbours, and
    /// orients the ring (counter-clockwise when <paramref name="ccw"/>).
    /// </summary>
    public static List<Vector2> Clean(IReadOnlyList<Vector2> input, bool ccw, float minEdge = 0.05f, float maxDeviation = 0.03f)
    {
        var pts = new List<Vector2>(input.Count);
        foreach (var p in input)
            if (pts.Count == 0 || Vector2.Distance(pts[^1], p) > minEdge) pts.Add(p);
        while (pts.Count > 1 && Vector2.Distance(pts[0], pts[^1]) <= minEdge) pts.RemoveAt(pts.Count - 1);
        bool changed = true;
        while (changed && pts.Count > 3)
        {
            changed = false;
            for (int i = 0; i < pts.Count && pts.Count > 3; i++)
            {
                var a = pts[(i + pts.Count - 1) % pts.Count];
                var b = pts[i];
                var c = pts[(i + 1) % pts.Count];
                if (DistanceToSegment(b, a, c) < maxDeviation) { pts.RemoveAt(i); changed = true; break; }
            }
        }
        if (pts.Count >= 3 && (SignedArea(pts) > 0) != ccw) pts.Reverse();
        return pts;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) => Vector2.Distance(p, ClosestOnSegment(p, a, b, out _));

    public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        t = l2 > 1e-12f ? Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f) : 0f;
        return a + ab * t;
    }

    /// <summary>True when two edges of the ring cross (not counting neighbours sharing a point).</summary>
    public static bool SelfIntersects(IReadOnlyList<Vector2> ring)
    {
        int n = ring.Count;
        for (int i = 0; i < n; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % n];
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                if (SegmentsCross(a, b, ring[j], ring[(j + 1) % n])) return true;
            }
        }
        return false;
    }

    public static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
        return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0)) && MathF.Abs(d1 - d2) > 1e-9f && MathF.Abs(d3 - d4) > 1e-9f;
    }

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static bool Contains(IReadOnlyList<Vector2> ring, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y) &&
                p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                inside = !inside;
        return inside;
    }

    /// <summary>
    /// Triangulates an outer ring with holes (ear clipping after bridging each hole to the outer ring), returning
    /// triangles as index triples into the concatenated points (outer first, then the holes in order).
    /// Output triangles are counter-clockwise seen from above.
    /// </summary>
    public static List<int> Triangulate(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>>? holes = null)
    {
        var pts = new List<Vector2>(outer);
        var ring = new List<int>();
        for (int i = 0; i < outer.Count; i++) ring.Add(i);
        if (SignedArea(outer) < 0) ring.Reverse();
        if (holes != null)
        {
            // bridge the holes in order of their rightmost point, largest x first (the classic approach)
            var hs = new List<(int start, int count, float maxX)>();
            foreach (var h in holes)
            {
                if (h.Count < 3) continue;
                int start = pts.Count;
                pts.AddRange(h);
                hs.Add((start, h.Count, h.Max(p => p.X)));
            }
            foreach (var (start, count, _) in hs.OrderByDescending(h => h.maxX))
            {
                var hole = Enumerable.Range(start, count).ToList();
                // holes must run clockwise (seen from above)
                if (SignedArea(hole.Select(i => pts[i]).ToList()) > 0) hole.Reverse();
                int hm = hole.OrderByDescending(i => pts[i].X).First();
                int bridge = FindBridge(pts, ring, pts[hm]);
                if (bridge < 0) continue;
                int pos = ring.IndexOf(bridge);
                int hs0 = hole.IndexOf(hm);
                var insert = new List<int>();
                for (int k = 0; k <= count; k++) insert.Add(hole[(hs0 + k) % count]);
                insert.Add(bridge);
                ring.InsertRange(pos + 1, insert);
            }
        }
        var tris = new List<int>();
        var idx = new List<int>(ring);
        int guard = idx.Count * idx.Count + 10;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                if (!IsEar(pts, idx, a, b, c)) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
            {
                // degenerate leftovers (touching points): drop the flattest vertex and go on
                int worst = 0; float best = float.MaxValue;
                for (int i = 0; i < idx.Count; i++)
                {
                    float ar = MathF.Abs(TriArea(pts[idx[(i + idx.Count - 1) % idx.Count]], pts[idx[i]], pts[idx[(i + 1) % idx.Count]]));
                    if (ar < best) { best = ar; worst = i; }
                }
                idx.RemoveAt(worst);
            }
        }
        if (idx.Count == 3 && TriArea(pts[idx[0]], pts[idx[1]], pts[idx[2]]) > 0) tris.AddRange(idx);
        return tris;
    }

    /// <summary>All points of the outer ring and the holes, in the order <see cref="Triangulate"/> indexes them.</summary>
    public static List<Vector2> Concat(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>>? holes)
    {
        var all = new List<Vector2>(outer);
        if (holes != null) foreach (var h in holes) if (h.Count >= 3) all.AddRange(h);
        return all;
    }

    /// <summary>Counter-clockwise (seen from above) area of triangle abc, positive when convex for a CCW ring.</summary>
    private static float TriArea(Vector2 a, Vector2 b, Vector2 c) => -Cross(b - a, c - a) * 0.5f;

    private static bool IsEar(List<Vector2> pts, List<int> idx, int a, int b, int c)
    {
        if (TriArea(pts[a], pts[b], pts[c]) <= 1e-7f) return false;
        foreach (int p in idx)
        {
            if (p == a || p == b || p == c) continue;
            var q = pts[p];
            if (q == pts[a] || q == pts[b] || q == pts[c]) continue;   // bridge duplicates
            if (InTriangle(q, pts[a], pts[b], pts[c])) return false;
        }
        return true;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = TriArea(p, a, b), d2 = TriArea(p, b, c), d3 = TriArea(p, c, a);
        return d1 >= -1e-7f && d2 >= -1e-7f && d3 >= -1e-7f;
    }

    /// <summary>A vertex of the ring visible from <paramref name="h"/> to the east (the hole bridge).</summary>
    private static int FindBridge(List<Vector2> pts, List<int> ring, Vector2 h)
    {
        int best = -1; float bestD = float.MaxValue;
        for (int k = 0; k < ring.Count; k++)
        {
            var p = pts[ring[k]];
            if (p.X < h.X) continue;
            float d = Vector2.DistanceSquared(p, h);
            if (d >= bestD) continue;
            // the segment h–p must not cross any ring edge
            bool blocked = false;
            for (int e = 0; e < ring.Count && !blocked; e++)
            {
                var a = pts[ring[e]]; var b = pts[ring[(e + 1) % ring.Count]];
                if (a == p || b == p) continue;
                if (SegmentsCross(h, p, a, b)) blocked = true;
            }
            if (!blocked) { best = ring[k]; bestD = d; }
        }
        return best;
    }
}
