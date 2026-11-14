using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Generation;

/// <summary>A door on a wall: which edge, how far along it (m), and whether it was mapped or inferred.</summary>
public readonly record struct DoorSite(int Edge, float U, Source Source, string Kind);

/// <summary>
/// Where the doors go. Mapped entrances (OSM entrance=* nodes) are snapped onto the nearest outer wall that can take a
/// door (not a party wall or a sliver). Without any, the style says how many it wants and they go on the best wall: the
/// longest street-facing one, else the longest open one, spread evenly and snapped to bay centres. Every courtyard also
/// gets a door on its longest wall (D7). Doors keep clear of passages.
/// </summary>
public static class Entrances
{
    public const float Snap = 3f;

    public static List<DoorSite> Place(BuildingGenerator.Context c, Func<WallEdge, int> wanted, float bay)
    {
        var edges = c.Site.Edges;
        var doors = new List<DoorSite>();
        bool Free(int edge, float u) => !Passages.Blocks(c.Passages, edge, u, bay * 0.5f) && !doors.Any(o => o.Edge == edge && MathF.Abs(o.U - u) < 2.5f);
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
            // a mapped entrance on a passage is the passage itself
            if (!Free(best, bu)) continue;
            doors.Add(new DoorSite(best, bu, c.Request.FromOsm ? Source.Osm : Source.Request, e.Kind));
        }
        if (doors.Count == 0)
        {
            int pick = -1; float score = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                var w = edges[i];
                if (w.Ring != 0 || w.Exposure is Exposure.Party or Exposure.Sliver || w.Length < 2.5f) continue;
                float s = w.Length * (w.Exposure == Exposure.Street ? 3f : 1f);
                if (s > score) { score = s; pick = i; }
            }
            if (pick >= 0)
            {
                var edge = edges[pick];
                int n = Math.Max(1, wanted(edge));
                for (int k = 0; k < n; k++) AddInBay(edge, pick, (k + 0.5f) / n);
            }
        }
        // a door from every courtyard into the building: a staircase opening onto the yard
        for (int h = 0; h < c.Site.Holes.Count; h++)
        {
            if (Polygon.Area(c.Site.Holes[h]) < Passages.LightWell) continue;
            int pick = -1; float longest = 0;
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].Ring == h + 1 && edges[i].Length > longest) { longest = edges[i].Length; pick = i; }
            if (pick >= 0 && longest >= 2.5f) AddInBay(edges[pick], pick, 0.5f, "courtyard");
        }
        return doors;

        // a door in the bay at fraction f along the edge, or the nearest free bay to it
        void AddInBay(WallEdge edge, int ei, float f, string kind = "main")
        {
            int bays = Math.Max(1, (int)MathF.Round(edge.Length / bay));
            float bw = edge.Length / bays;
            int start = Math.Clamp((int)(f * bays), 0, bays - 1);
            for (int step = 0; step < bays * 2; step++)
            {
                int bi = start + (step % 2 == 0 ? step / 2 : -(step / 2 + 1));
                if (bi < 0 || bi >= bays) continue;
                float u = (bi + 0.5f) * bw;
                if (!Free(ei, u)) continue;
                doors.Add(new DoorSite(ei, u, Source.Inferred, kind));
                return;
            }
        }
    }
}
