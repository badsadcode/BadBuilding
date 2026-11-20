using System.Numerics;
using BadBuilding.Model;

namespace BadBuilding.Interior;

/// <summary>An opening in the facade as the exterior built it (D33): its two ends in plan (on the wall line), its
/// height range, and what fills it ("glass", "door", "glass_block", "shopfront").</summary>
public sealed record Opening(Vector2 A, Vector2 B, float Y0, float Y1, string Kind);

/// <summary>An entrance door of the exterior: where it stands (on the wall, at the floor) and which way is out.</summary>
public sealed record EntranceDoor(string Id, Vector3 Position, Vector3 Out);

/// <summary>A balcony of the exterior (D35): the middle of its door on the facade line at the floor, which way is out,
/// its width along the wall and how far it stands out (0: a door out to the garden on the ground floor).</summary>
public sealed record BalconySite(Vector3 Position, Vector3 Out, float Width, float Depth);

/// <summary>
/// What an interior needs to know about a building (D33), kept small so a game can hold one for every building near
/// the player and plan the rooms only when somebody walks in: the outline, the storeys, the entrance doors and every
/// window and door opening the facade made (the interior follows the facade: the window handshake runs this way round
/// because the exteriors are generated first and far more often). Building space: plan x, z, height y.
/// </summary>
public sealed record Shell
{
    public required string Id { get; init; }
    public required ulong Seed { get; init; }
    public required string Style { get; init; }
    public string? Type { get; init; }
    public required IReadOnlyList<Vector2> Outer { get; init; }
    public required float Datum { get; init; }
    public required float GroundLow { get; init; }
    public required float StoreyHeight { get; init; }
    public required int Levels { get; init; }
    public required IReadOnlyList<EntranceDoor> Entrances { get; init; }
    public required IReadOnlyList<Opening> Openings { get; init; }
    public IReadOnlyList<BalconySite> Balconies { get; init; } = [];

    /// <summary>The styles whose buildings get interiors so far (D33: blocks of flats with staircases).</summary>
    public static readonly HashSet<string> Styles = ["prl_block", "apartments"];

    /// <summary>The shell of a generated building, or null when it can't have an interior yet (style, holes, a part
    /// in the air, no entrance, a single storey).</summary>
    public static Shell? Of(Building b)
    {
        var plan = b.Plan;
        if (plan == null || !Styles.Contains(plan.Style) || b.Request.Holes.Count > 0 || b.Request.Raised) return null;
        if (plan.Levels.Value < 2) return null;
        var doors = b.Markers.Where(m => m.Kind == "entrance").Select(m => new EntranceDoor(m.Id, m.Position, m.Direction)).ToList();
        if (doors.Count == 0) return null;
        return new Shell
        {
            Id = b.Request.Id,
            Seed = b.Request.Seed,
            Style = plan.Style,
            Type = b.Request.Type,
            Outer = plan.Edges.Where(e => e.Ring == 0).Select(e => e.A).ToList(),
            Datum = plan.Datum,
            GroundLow = plan.GroundLow,
            StoreyHeight = plan.StoreyHeight,
            Levels = plan.Levels.Value,
            Entrances = doors,
            Openings = OpeningsOf(b),
            Balconies = b.Markers.Where(m => m.Kind == "balcony").Select(m => new BalconySite(m.Position, m.Direction, m.Size.X, m.Size.Y)).ToList(),
        };
    }

    private static readonly HashSet<string> Fills = ["glass", "door", "glass_block", "shopfront", "balcony_door"];

    /// <summary>Every opening in the walls, read back from the infill quads the facade drew (Kit.WallWithOpening puts
    /// one quad of glass or door at the back of each hole): its corners give the hole's extent.</summary>
    public static List<Opening> OpeningsOf(Building b)
    {
        var res = new List<Opening>();
        var edges = b.Plan?.Edges ?? [];
        foreach (var m in b.Modules)
        {
            if (m.Type != "wall") continue;
            var mesh = m.Mesh;
            foreach (var (role, idx) in mesh.Groups)
            {
                if (!Fills.Contains(role)) continue;
                // a quad is a fan from its first vertex: its two triangles share their smallest index
                var quads = new Dictionary<int, (Vector3 Min, Vector3 Max, Vector3 N)>();
                for (int t = 0; t + 2 < idx.Count; t += 3)
                {
                    int k = Math.Min(idx[t], Math.Min(idx[t + 1], idx[t + 2]));
                    var q = quads.TryGetValue(k, out var e) ? e : (new Vector3(float.MaxValue), new Vector3(float.MinValue), mesh.Normals[idx[t]]);
                    for (int c = 0; c < 3; c++) { var p = mesh.Positions[idx[t + c]]; q.Item1 = Vector3.Min(q.Item1, p); q.Item2 = Vector3.Max(q.Item2, p); }
                    quads[k] = q;
                }
                foreach (var (min, max, n) in quads.Values)
                {
                    if (MathF.Abs(n.Y) > 0.3f || max.Y - min.Y < 0.3f) continue;
                    var n2 = Vector2.Normalize(new Vector2(n.X, n.Z));
                    var mid = new Vector2((min.X + max.X) * 0.5f, (min.Z + max.Z) * 0.5f);
                    // the wall it belongs to: facing the same way, within a deep reveal of its line
                    WallEdge? best = null; float bestD = 0.7f;
                    foreach (var e in edges)
                    {
                        if (Vector2.Dot(e.Out, n2) < 0.9f) continue;
                        float d = Geometry.Polygon.DistanceToSegment(mid, e.A, e.B);
                        if (d < bestD) { bestD = d; best = e; }
                    }
                    if (best == null) continue;
                    // its ends along the wall, put back on the wall line
                    float ta = Vector2.Dot(new Vector2(min.X, min.Z) - best.A, best.Dir), tb = Vector2.Dot(new Vector2(max.X, max.Z) - best.A, best.Dir);
                    float t0 = Math.Clamp(MathF.Min(ta, tb), 0, best.Length), t1 = Math.Clamp(MathF.Max(ta, tb), 0, best.Length);
                    if (t1 - t0 < 0.2f) continue;
                    res.Add(new Opening(best.A + best.Dir * t0, best.A + best.Dir * t1, min.Y, max.Y, role));
                }
            }
        }
        return res;
    }
}
