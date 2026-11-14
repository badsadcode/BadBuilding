using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;
using BadBuilding.Validation;

namespace BadBuilding.Generation;

/// <summary>
/// The building's site, worked out before anything is built: the cleaned footprint, every wall edge with its exposure
/// (D4), the ground under the outline and the datum (D5), and whether the footprint can carry a building at all (D6).
/// </summary>
public sealed class Site
{
    public List<Vector2> Outer { get; } = [];
    public List<List<Vector2>> Holes { get; } = [];
    public List<WallEdge> Edges { get; } = [];
    public float Area { get; private set; }
    /// <summary>Ground at each cleaned outer point; the highest and lowest of it.</summary>
    public float GroundHigh { get; private set; }
    public float GroundLow { get; private set; }
    /// <summary>The narrowest the building gets (twice the largest inscribed distance found by sampling): a slab block is
    /// ~10–14 m deep, a house ~8 m.</summary>
    public float Depth { get; private set; }
    public bool Usable { get; private set; }
    /// <summary>Under 4 m²: built as a plain box without openings.</summary>
    public bool Small { get; private set; }

    public const float PartyTolerance = 0.6f, PartyCover = 0.5f, SliverGap = 4f, StreetReach = 25f;

    public static Site Analyse(BuildingRequest r, ValidationReport report)
    {
        var s = new Site();
        s.Outer.AddRange(Polygon.Clean(r.Outer.Points, ccw: true));
        foreach (var h in r.Holes)
        {
            var c = Polygon.Clean(h.Points, ccw: false);
            if (c.Count >= 3 && Polygon.Area(c) >= 4f) s.Holes.Add(c);
        }
        if (s.Outer.Count < 3)
        {
            report.Add(Severity.Error, "footprint.degenerate", $"The outline has {s.Outer.Count} usable points after cleaning.", "no building");
            return s;
        }
        if (Polygon.SelfIntersects(s.Outer))
        {
            report.Add(Severity.Error, "footprint.self_intersecting", "The outline crosses itself.", "no building");
            return s;
        }
        s.Area = Polygon.Area(s.Outer) - s.Holes.Sum(h => Polygon.Area(h));
        if (Polygon.Area(s.Outer) < 0.01f)
        {
            report.Add(Severity.Error, "footprint.degenerate", "The outline encloses no area (its points lie on a line).", "no building");
            return s;
        }
        if (s.Area < 1f)
        {
            report.Add(Severity.Error, "footprint.too_small", $"The footprint covers {s.Area:0.0} m², under 1 m².", "no building");
            return s;
        }
        if (s.Area < 4f)
        {
            // a kiosk, a transformer box: a hole in the map looks worse than a plain little box inside the outline
            report.Add(Severity.Warn, "footprint.small", $"The footprint covers {s.Area:0.0} m²: a plain box without openings.", "box");
            s.Small = true;
        }
        s.Usable = true;
        s.Depth = EstimateDepth(s.Outer, s.Holes);
        if (s.Depth < 2.2f)
            report.Add(Severity.Warn, "footprint.thin", $"The footprint is only ~{s.Depth:0.0} m across: too thin for rooms.", "plain style, no openings");

        // ground at every cleaned outer point: nearest given sample (the request gives one per original point)
        var given = r.Ground;
        var src = r.Outer.Points;
        foreach (var p in s.Outer)
        {
            if (given.Count != src.Count || given.Count == 0) continue;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < src.Count; i++) { float d = Vector2.DistanceSquared(src[i], p); if (d < bd) { bd = d; best = i; } }
            s._ground.Add(given[best]);
        }
        if (s._ground.Count == 0) { s.GroundHigh = s.GroundLow = 0f; }
        else
        {
            s.GroundHigh = s._ground.Max(); s.GroundLow = s._ground.Min();
            if (s.GroundHigh - s.GroundLow > 0.5f)
                report.Add(Severity.Info, "site.slope", $"The ground falls {s.GroundHigh - s.GroundLow:0.0} m across the outline: an exposed plinth on the low side.");
        }

        s.ClassifyEdges(r);
        return s;
    }

    private readonly List<float> _ground = [];

    /// <summary>Ground height at an outer point index (0 when no ground was given).</summary>
    public float GroundAt(int i) => _ground.Count == Outer.Count ? _ground[i] : 0f;

    private void ClassifyEdges(BuildingRequest r)
    {
        var neighbourEdges = new List<(Vector2 A, Vector2 B)>();
        foreach (var n in r.Neighbours)
        {
            var c = Polygon.Clean(n.Points, ccw: true);
            for (int i = 0; i < c.Count; i++) neighbourEdges.Add((c[i], c[(i + 1) % c.Count]));
        }
        void Walk(List<Vector2> ring, int ringIndex)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Count];
                Edges.Add(new WallEdge(ringIndex, i, a, b, Classify(a, b, ringIndex > 0, neighbourEdges, r.Streets)));
            }
        }
        Walk(Outer, 0);
        for (int h = 0; h < Holes.Count; h++) Walk(Holes[h], h + 1);
    }

    private static Exposure Classify(Vector2 a, Vector2 b, bool courtyard, List<(Vector2 A, Vector2 B)> neighbours, IReadOnlyList<StreetLine> streets)
    {
        float len = Vector2.Distance(a, b);
        if (len < 0.01f) return Exposure.Side;
        var dir = (b - a) / len;
        var outward = new Vector2(-dir.Y, dir.X);
        // Party: a neighbour edge runs along this one (parallel, within the tolerance) for at least half its length.
        float covered = 0, sliver = 0;
        const int samples = 8;
        for (int k = 0; k < samples; k++)
        {
            var p = a + (b - a) * ((k + 0.5f) / samples);
            float bestAlong = float.MaxValue, bestFacing = float.MaxValue;
            foreach (var (na, nb) in neighbours)
            {
                var nd = nb - na;
                float nl = nd.Length();
                if (nl < 0.01f) continue;
                if (MathF.Abs(Polygon.Cross(dir, nd / nl)) > 0.26f) continue;   // within ~15° of parallel
                var q = Polygon.ClosestOnSegment(p, na, nb, out _);
                var off = q - p;
                float dist = off.Length();
                // only what lies in front of the wall counts (behind it is our own building)
                if (Vector2.Dot(off, outward) < -PartyTolerance) continue;
                bestAlong = MathF.Min(bestAlong, dist);
                bestFacing = MathF.Min(bestFacing, dist);
            }
            if (bestAlong <= PartyTolerance) covered++;
            else if (bestFacing <= SliverGap) sliver++;
        }
        if (covered / samples >= PartyCover) return Exposure.Party;
        if (sliver / samples >= 0.5f) return Exposure.Sliver;
        if (courtyard) return Exposure.Courtyard;
        // Street: a street line passes in front of the wall within reach.
        var mid = (a + b) * 0.5f;
        foreach (var st in streets)
            for (int i = 0; i + 1 < st.Points.Count; i++)
            {
                var q = Polygon.ClosestOnSegment(mid, st.Points[i], st.Points[i + 1], out _);
                var off = q - mid;
                if (off.Length() <= StreetReach + st.HalfWidth && Vector2.Dot(off, outward) > 0.3f * off.Length()) return Exposure.Street;
            }
        return Exposure.Side;
    }

    /// <summary>Roughly how thick the footprint is: twice the largest distance from an interior sample to the outline.</summary>
    private static float EstimateDepth(List<Vector2> outer, List<List<Vector2>> holes)
    {
        float minX = outer.Min(p => p.X), maxX = outer.Max(p => p.X), minZ = outer.Min(p => p.Y), maxZ = outer.Max(p => p.Y);
        float step = MathF.Max(0.5f, MathF.Max(maxX - minX, maxZ - minZ) / 40f);
        float best = 0;
        for (float x = minX + step * 0.5f; x < maxX; x += step)
            for (float z = minZ + step * 0.5f; z < maxZ; z += step)
            {
                var p = new Vector2(x, z);
                if (!Polygon.Contains(outer, p) || holes.Any(h => Polygon.Contains(h, p))) continue;
                float d = float.MaxValue;
                for (int i = 0; i < outer.Count; i++) d = MathF.Min(d, Polygon.DistanceToSegment(p, outer[i], outer[(i + 1) % outer.Count]));
                foreach (var h in holes)
                    for (int i = 0; i < h.Count; i++) d = MathF.Min(d, Polygon.DistanceToSegment(p, h[i], h[(i + 1) % h.Count]));
                best = MathF.Max(best, d);
            }
        return best * 2f;
    }
}
