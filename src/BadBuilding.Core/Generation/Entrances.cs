using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Generation;

/// <summary>A door on a wall: which edge, how far along it (m), and whether it was mapped or inferred.</summary>
public readonly record struct DoorSite(int Edge, float U, Source Source, string Kind);

/// <summary>
/// Where the doors go. Mapped entrances (OSM entrance=* nodes) are snapped onto the nearest outer wall that can take a
/// door (not a party wall or a sliver). Without any, the style says how many it wants and they go on the best wall: the
/// longest street-facing one, else the longest open one, spread evenly and snapped to bay centres.
/// </summary>
public static class Entrances
{
    public const float Snap = 3f;

    public static List<DoorSite> Place(BuildingGenerator.Context c, Func<WallEdge, int> wanted, float bay)
    {
        var edges = c.Site.Edges;
        var doors = new List<DoorSite>();
        foreach (var e in c.Request.Entrances)
        {
            int best = -1; float bd = Snap, bu = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                var w = edges[i];
                if (w.Ring != 0 || w.Exposure is Exposure.Party or Exposure.Sliver) continue;
                var q = Polygon.ClosestOnSegment(e.Position, w.A, w.B, out float t);
                float d = Vector2.Distance(q, e.Position);
                if (d < bd) { bd = d; best = i; bu = t * w.Length; }
            }
            if (best < 0) continue;
            var we = edges[best];
            bu = Math.Clamp(bu, 1.2f, MathF.Max(1.2f, we.Length - 1.2f));
            if (doors.Any(o => o.Edge == best && MathF.Abs(o.U - bu) < 2.5f)) continue;
            doors.Add(new DoorSite(best, bu, c.Request.FromOsm ? Source.Osm : Source.Request, e.Kind));
        }
        if (doors.Count > 0) return doors;

        int pick = -1; float score = 0;
        for (int i = 0; i < edges.Count; i++)
        {
            var w = edges[i];
            if (w.Ring != 0 || w.Exposure is Exposure.Party or Exposure.Sliver || w.Length < 2.5f) continue;
            float s = w.Length * (w.Exposure == Exposure.Street ? 3f : 1f);
            if (s > score) { score = s; pick = i; }
        }
        if (pick < 0) return doors;
        var edge = edges[pick];
        int n = Math.Max(1, wanted(edge));
        int bays = Math.Max(1, (int)MathF.Round(edge.Length / bay));
        float bw = edge.Length / bays;
        for (int k = 0; k < n; k++)
        {
            int bi = Math.Clamp((int)((k + 0.5f) / n * bays), 0, bays - 1);
            doors.Add(new DoorSite(pick, (bi + 0.5f) * bw, Source.Inferred, "main"));
        }
        return doors;
    }
}
