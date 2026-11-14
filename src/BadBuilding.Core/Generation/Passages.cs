using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;
using BadBuilding.Validation;

namespace BadBuilding.Generation;

/// <summary>
/// A way through the ground floor from an outer wall into a courtyard (D7): a gate passage (brama przejazdowa, wide and
/// high enough for a car) or a pedestrian passage. In plan it runs straight from <see cref="OuterEdge"/> at
/// <see cref="OuterU"/> inward to <see cref="HoleEdge"/>; its sides end where they meet the courtyard wall.
/// </summary>
public sealed record Passage(string Kind, int OuterEdge, float OuterU, int HoleEdge, float HoleU, float HalfWidth,
    float Floor, float Ceiling, Vector2 LeftOut, Vector2 RightOut, Vector2 LeftIn, Vector2 RightIn)
{
    public bool IsGate => Kind == "gate";
    public Vector2 Mid => (LeftOut + RightOut) * 0.5f;
    public float Depth => Vector2.Distance(Mid, (LeftIn + RightIn) * 0.5f);

    /// <summary>The u range the passage cuts out of wall edge <paramref name="edge"/>, if it cuts that edge.</summary>
    public (float U0, float U1)? Cut(int edge) => edge == OuterEdge ? (OuterU - HalfWidth, OuterU + HalfWidth)
        : edge == HoleEdge ? (HoleU - HalfWidth, HoleU + HalfWidth) : null;
}

/// <summary>
/// Courtyard access (Noor: "how does anyone get into the courtyard?"). Every hole big enough to be a courtyard gets one
/// straight passage through the shortest open stretch of building, preferring street walls; a hole too small to walk
/// into is a light well and is left alone. Built before the facade so doors and windows keep clear of it.
/// </summary>
public static class Passages
{
    /// <summary>Holes under this are light wells, not courtyards, unless a mapped entrance opens onto them (D8).</summary>
    public const float LightWell = 40f;
    /// <summary>A hole narrower than this (the short side of a rectangle of the same area and perimeter) is a shaft for light and air.</summary>
    public const float ShaftWidth = 4f;

    /// <summary>
    /// Whether hole <paramref name="h"/> is a courtyard people go into, or a light well / shaft (D8, Noor Q4): a mapped
    /// entrance on its wall makes it a courtyard at any size; otherwise it needs 40 m² and a width of 4 m.
    /// </summary>
    public static bool IsCourtyard(BuildingGenerator.Context c, int h, out string why)
    {
        var ring = c.Site.Holes[h];
        float area = Polygon.Area(ring), half = Polygon.Perimeter(ring) * 0.5f;
        // the short side of the rectangle with this area and perimeter: exact for rectangles, near enough for the rest
        float width = (half - MathF.Sqrt(MathF.Max(0f, half * half - 4f * area))) * 0.5f;
        bool mapped = c.Request.Entrances.Any(e => Enumerable.Range(0, ring.Count)
            .Any(i => Polygon.DistanceToSegment(e.Position, ring[i], ring[(i + 1) % ring.Count]) < Entrances.Snap));
        if (mapped) { why = "a mapped entrance opens onto it"; return true; }
        if (area < LightWell) { why = $"{area:0} m², a light well"; return false; }
        if (width < ShaftWidth) { why = $"only ~{width:0.0} m wide, a shaft"; return false; }
        why = $"{area:0} m²";
        return true;
    }
    public const float GateHalf = 1.5f, WalkHalf = 0.9f, MaxDepth = 25f;

    public static List<Passage> Plan(BuildingGenerator.Context c, float plinth, float gateArea = 150f)
    {
        var list = new List<Passage>();
        var site = c.Site;
        var edges = site.Edges;
        float floor = c.Plan.Datum - plinth;
        float storeyTop = c.Plan.Datum + c.Plan.StoreyHeight - 0.35f;
        for (int h = 0; h < site.Holes.Count; h++)
        {
            float area = Polygon.Area(site.Holes[h]);
            if (!IsCourtyard(c, h, out var why))
            {
                c.Building.Report.Add(Severity.Info, "courtyard.light_well", $"Hole {h + 1}: {why}, no way in.");
                continue;
            }
            Passage? best = null; float bestScore = 0;
            foreach (bool gate in new[] { true, false })
            {
                float hw = gate ? GateHalf : WalkHalf;
                // a gate wants a courtyard a car can turn in and a storey tall enough
                if (gate && (area < gateArea || storeyTop - floor < 2.9f)) continue;
                float ceiling = floor + MathF.Min(gate ? 3.6f : 2.6f, storeyTop - floor);
                for (int ei = 0; ei < edges.Count; ei++)
                {
                    var e = edges[ei];
                    if (e.Ring != 0 || e.Exposure is Exposure.Party or Exposure.Sliver) continue;
                    float len = e.Length;
                    if (len < 2 * hw + 2f) continue;
                    float mult = e.Exposure == Exposure.Street ? 3f : 1f;
                    for (float u = hw + 1f; u <= len - hw - 1f; u += 0.5f)
                    {
                        var p = Cast(site, edges, ei, h, u, hw);
                        if (p is not { } hit) continue;
                        float score = mult / (hit.Depth + 5f) * (1f - 0.5f * MathF.Abs(u / len - 0.5f)) * (gate ? 1.5f : 1f);
                        if (score <= bestScore) continue;
                        bestScore = score;
                        best = new Passage(gate ? "gate" : "walk", ei, u, hit.Edge, hit.HoleU, hw, floor, ceiling,
                            hit.LeftOut, hit.RightOut, hit.LeftIn, hit.RightIn);
                    }
                }
            }
            if (best is null)
            {
                c.Building.Report.Add(Severity.Warn, "courtyard.no_passage", $"No straight way through the building into courtyard {h + 1}.", "courtyard door only");
                continue;
            }
            list.Add(best);
            c.Building.Report.Add(Severity.Info, "courtyard.passage",
                $"Courtyard {h + 1}: a {(best.IsGate ? "gate passage" : "pedestrian passage")} {best.HalfWidth * 2:0.0} m wide, {best.Depth:0.0} m long.");
        }
        return list;
    }

    private readonly record struct Hit(int Edge, float HoleU, float Depth, Vector2 LeftOut, Vector2 RightOut, Vector2 LeftIn, Vector2 RightIn);

    /// <summary>Three rays straight in from the outer wall (the middle and both sides): all must reach the same courtyard
    /// wall first, nearly square to it, without crossing anything else.</summary>
    private static Hit? Cast(Site site, List<WallEdge> edges, int from, int hole, float u, float hw)
    {
        var e = edges[from];
        var d = -e.Out;
        int target = -1; float tl = 0, tr = 0, tm = 0;
        foreach (var (off, k) in new[] { (-hw - 0.3f, 0), (hw + 0.3f, 1), (0f, 2), (-hw, 3), (hw, 4) })
        {
            var o = e.A + e.Dir * (u + off);
            int hitEdge = -1; float best = float.MaxValue;
            for (int j = 0; j < edges.Count; j++)
            {
                if (j == from) continue;
                if (Ray(o, d, edges[j].A, edges[j].B) is { } t && t > 0.05f && t < best) { best = t; hitEdge = j; }
            }
            if (hitEdge < 0 || edges[hitEdge].Ring != hole + 1 || best > MaxDepth) return null;
            if (target < 0) target = hitEdge; else if (hitEdge != target) return null;
            if (k == 3) tl = best; else if (k == 4) tr = best; else if (k == 2) tm = best;
        }
        var he = edges[target];
        if (Vector2.Dot(he.Out, d) < 0.9f) return null;      // the courtyard wall must face the passage (its outside looks into the yard), within ~25°
        var mid = e.A + e.Dir * u + d * tm;
        float hu = Vector2.Dot(mid - he.A, he.Dir);
        if (hu < hw + 0.3f || hu > he.Length - hw - 0.3f) return null;
        var lo = e.A + e.Dir * (u - hw); var ro = e.A + e.Dir * (u + hw);
        return new Hit(target, hu, tm, lo, ro, lo + d * tl, ro + d * tr);
    }

    internal static float? Ray(Vector2 o, Vector2 d, Vector2 a, Vector2 b)
    {
        var s = b - a;
        float den = Polygon.Cross(d, s);
        if (MathF.Abs(den) < 1e-6f) return null;
        var ao = a - o;
        float t = Polygon.Cross(ao, s) / den, v = Polygon.Cross(ao, d) / den;
        return v is >= 0f and <= 1f ? t : null;
    }

    /// <summary>
    /// The passage itself, shared by every style: side walls, a ceiling and paving between the two cuts, and for a gate
    /// two leaves standing open against the walls just inside the street opening.
    /// </summary>
    public static void Build(BuildingGenerator.Context c, Passage p, string wallRole, string ceilingRole, string floorRole, string gateRole, string frameRole)
    {
        var b = c.Building;
        var m = b.Add(c.Id($"passage.e{p.OuterEdge}"), "passage", ModuleTier.Primary);
        m.Extras = new() { ["kind"] = p.Kind, ["width"] = MathF.Round(p.HalfWidth * 2, 2), ["clear_height"] = MathF.Round(p.Ceiling - p.Floor, 2) };
        Vector3 V(Vector2 q, float y) => new(q.X, y, q.Y);
        var across = Vector2.Normalize(p.RightOut - p.LeftOut);
        float y0 = p.Floor - 0.05f, y1 = p.Ceiling;
        // side walls face into the passage
        m.Mesh.AddQuad(wallRole, V(p.LeftOut, y0), V(p.LeftIn, y0), V(p.LeftIn, y1), V(p.LeftOut, y1), V(across, 0));
        m.Mesh.AddQuad(wallRole, V(p.RightIn, y0), V(p.RightOut, y0), V(p.RightOut, y1), V(p.RightIn, y1), V(-across, 0));
        m.Mesh.AddPolygon(ceilingRole, [V(p.LeftOut, y1), V(p.RightOut, y1), V(p.RightIn, y1), V(p.LeftIn, y1)], -Vector3.UnitY);
        m.Mesh.AddPolygon(floorRole, [V(p.LeftOut, p.Floor + 0.02f), V(p.LeftIn, p.Floor + 0.02f), V(p.RightIn, p.Floor + 0.02f), V(p.RightOut, p.Floor + 0.02f)], Vector3.UnitY);
        var inward = Vector2.Normalize(p.LeftIn - p.LeftOut);
        b.Markers.Add(new Marker(c.Id($"passage.e{p.OuterEdge}.street"), "passage", V(p.Mid, p.Floor), V(-inward, 0)));
        b.Markers.Add(new Marker(c.Id($"passage.e{p.OuterEdge}.courtyard"), "passage", V((p.LeftIn + p.RightIn) * 0.5f, p.Floor), V(inward, 0)));
        if (!p.IsGate) return;
        // Noor Q1: a mix decided by the seed, some gates open, some closed with a small door (furtka) in one leaf.
        // A closed gate is a state, not a wall: the passage, its markers and the carved collision stay, so the
        // courtyard is still reachable and a game can open and close it later (D8).
        bool closed = c.Detail.Chance(0.5);
        var gate = b.Add(c.Id($"gate.e{p.OuterEdge}"), "gate", ModuleTier.Secondary, m.Id);
        gate.Extras = new() { ["state"] = closed ? "closed" : "open", ["wicket"] = closed, ["hinge_depth"] = 0.4f };
        float leaf = p.HalfWidth, gh = MathF.Min(p.Ceiling - p.Floor - 0.1f, 3.2f);
        if (!closed)
        {
            // two leaves swung open flat against the side walls, hinged 0.4 m inside the street face
            foreach (var (hinge, side) in new[] { (p.LeftOut, 1f), (p.RightOut, -1f) })
            {
                var h0 = hinge + inward * 0.4f + across * side * 0.02f;
                var f = new Kit.Frame(V(h0, 0), V(inward, 0), V(across * side, 0));
                Kit.Box(gate.Mesh, gateRole, f, 0, leaf, p.Floor + 0.05f, p.Floor + gh, 0, 0.07f);
            }
            return;
        }
        // closed: both leaves across the opening, 0.4 m in, facing the street; a framed wicket door in the right leaf
        var fc = new Kit.Frame(V(p.LeftOut + inward * 0.47f, 0), V(across, 0), V(-inward, 0));
        float w = p.HalfWidth * 2, g0 = p.Floor + 0.05f, g1 = p.Floor + gh;
        Kit.Box(gate.Mesh, gateRole, fc, 0, w * 0.5f - 0.01f, g0, g1, 0, 0.07f);
        Kit.Box(gate.Mesh, gateRole, fc, w * 0.5f + 0.01f, w, g0, g1, 0, 0.07f);
        float d0 = w * 0.5f + 0.35f, d1 = d0 + 0.9f;
        foreach (var (u0, u1, v0, v1) in new[] { (d0 - 0.05f, d0, g0, g0 + 2.0f), (d1, d1 + 0.05f, g0, g0 + 2.0f), (d0 - 0.05f, d1 + 0.05f, g0 + 2.0f, g0 + 2.05f) })
            Kit.Box(gate.Mesh, frameRole, fc, u0, u1, v0, v1, 0.07f, 0.1f);
        b.Markers.Add(new Marker(c.Id($"gate.e{p.OuterEdge}.wicket"), "wicket", fc.P((d0 + d1) * 0.5f, p.Floor, 0.1f), V(-inward, 0)));
    }

    /// <summary>Doors keep this far (m, from the passage centre beyond its half width) from a passage on the same wall.</summary>
    public const float DoorClear = 1.2f;

    public static bool Blocks(IEnumerable<Passage> ps, int edge, float u, float doorHalf) =>
        ps.Any(p => p.Cut(edge) is { } r && u + doorHalf + DoorClear > r.U0 && u - doorHalf - DoorClear < r.U1);
}
