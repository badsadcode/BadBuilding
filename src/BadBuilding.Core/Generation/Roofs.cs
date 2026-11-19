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
        => Pitched(m, roofRole, soffitRole, gableRole, r, shape, eave, rise, overhang, true, true);

    /// <summary>
    /// The same, with each end chosen: <paramref name="outerStart"/> / <paramref name="outerEnd"/> false for an end that
    /// runs into another wing's roof (D25): no hip and no overhang there, and a gable wall that stays hidden under the
    /// other roof.
    /// </summary>
    public static void Pitched(Mesh m, string roofRole, string soffitRole, string gableRole, Rect r, RoofShape shape, float eave, float rise, float overhang, bool outerStart, bool outerEnd)
    {
        float L = r.HalfL, W = r.HalfW, o = overhang, slope = rise / MathF.Max(W, 0.01f);
        float lo = eave - o * slope, top = eave + rise;
        Vector3 P(float u, float v, float y) { var q = r.At(u, v); return new Vector3(q.X, y, q.Y); }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // the upper side, and the same plane seen from below under the eaves
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            if (n.Y < 0) n = -n;
            m.AddQuad(roofRole, a, b, c, d, n);
            m.AddQuad(soffitRole, a, b, c, d, -n);
        }
        bool hip = shape is RoofShape.Hipped or RoofShape.Pyramidal;
        // each end: where the eaves stop (e0/e1) and where the ridge stops (r0/r1)
        float e0 = outerStart ? -L - o : -L, e1 = outerEnd ? L + o : L;
        bool hip0 = hip && outerStart, hip1 = hip && outerEnd;
        float r0 = hip0 ? -L + W : e0, r1 = hip1 ? L - W : e1;
        if (shape == RoofShape.Pyramidal || r0 > r1) r0 = r1 = (r0 + r1) * 0.5f;   // pyramidal, or a square hipped wing
        Face(P(e0, -W - o, lo), P(e1, -W - o, lo), P(r1, 0, top), P(r0, 0, top));
        Face(P(e0, W + o, lo), P(e1, W + o, lo), P(r1, 0, top), P(r0, 0, top));
        var up = Vector3.UnitY;
        foreach (var (isHip, e, rr, s) in new[] { (hip0, e0, r0, -1f), (hip1, e1, r1, 1f) })
        {
            if (isHip)
            {
                m.AddPolygon(roofRole, [P(e, -W - o, lo), P(e, W + o, lo), P(rr, 0, top)], up);
                m.AddPolygon(soffitRole, [P(e, -W - o, lo), P(e, W + o, lo), P(rr, 0, top)], -up);
            }
            else
            {
                float gu = s * L;
                var outward = new Vector3(r.U.X, 0, r.U.Y) * s;
                m.AddPolygon(gableRole, [P(gu, -W, eave), P(gu, W, eave), P(gu, 0, top)], outward);
            }
        }
    }

    /// <summary>One rectangular wing of a pitched roof (D25): its rectangle, rise, and which ends are outer ends.</summary>
    public readonly record struct Wing(Rect Rect, float Rise, bool OuterStart, bool OuterEnd);

    /// <summary>Draws the plan's pitched roof: its wings when the footprint was split into them, else one rectangle.</summary>
    public static void Pitched(Mesh m, string roofRole, string soffitRole, string gableRole, Model.BuildingPlan plan, float eave, float overhang)
    {
        if (plan.RoofWings.Count == 0) { Pitched(m, roofRole, soffitRole, gableRole, plan.RoofRect, plan.RoofShape, eave, plan.RoofRise, overhang); return; }
        var shape = plan.RoofShape == RoofShape.Pyramidal ? RoofShape.Hipped : plan.RoofShape;
        foreach (var w in plan.RoofWings)
            Pitched(m, roofRole, soffitRole, gableRole, w.Rect, shape, eave, w.Rise, overhang, w.OuterStart, w.OuterEnd);
    }

    /// <summary>
    /// Splits an L-, T-, U- or otherwise rectilinear footprint into rectangular wings for a pitched roof (D25, Noor's
    /// cookbook RF08: 30 % of Mazowsze's houses got a flat roof). The footprint is laid on its own grid (the minimum
    /// rectangle's axes, cut at every corner); a few maximal rectangles cover its cells. The widest is the main wing;
    /// a wing crossing it is cut back to the main ridge, so its roof runs into the main slopes in valleys. Null when
    /// the footprint isn't rectilinear enough, has more than four wings, or a wing narrower than 2 m.
    /// </summary>
    public static List<Wing>? Wings(IReadOnlyList<Vector2> ring, Rect frame, float pitchDeg, float? mainRise)
    {
        // the outline in the frame (u along U, v along V)
        var uv = ring.Select(p => new Vector2(Vector2.Dot(p - frame.Centre, frame.U), Vector2.Dot(p - frame.Centre, frame.V))).ToList();
        static List<float> Cuts(IEnumerable<float> xs)
        {
            var c = new List<float>();
            foreach (var x in xs.OrderBy(x => x)) if (c.Count == 0 || x - c[^1] > 0.4f) c.Add(x); else c[^1] = (c[^1] + x) * 0.5f;
            return c;
        }
        var us = Cuts(uv.Select(p => p.X)); var vs = Cuts(uv.Select(p => p.Y));
        int nu = us.Count - 1, nv = vs.Count - 1;
        if (nu < 1 || nv < 1 || nu > 8 || nv > 8) return null;
        var inside = new bool[nu, nv];
        float cellArea = 0;
        for (int i = 0; i < nu; i++)
            for (int k = 0; k < nv; k++)
                if (inside[i, k] = Polygon.Contains(uv, new Vector2((us[i] + us[i + 1]) * 0.5f, (vs[k] + vs[k + 1]) * 0.5f)))
                    cellArea += (us[i + 1] - us[i]) * (vs[k + 1] - vs[k]);
        float area = Polygon.Area(uv);
        if (area < 1f || MathF.Abs(cellArea - area) / area > 0.06f) return null;   // corners that aren't square

        // maximal rectangles of inside cells, greedily, each the biggest one holding a still uncovered cell
        bool All(int i0, int i1, int k0, int k1)
        {
            for (int i = i0; i <= i1; i++) for (int k = k0; k <= k1; k++) if (!inside[i, k]) return false;
            return true;
        }
        var covered = new bool[nu, nv];
        var boxes = new List<(int i0, int i1, int k0, int k1)>();
        for (int ci = 0; ci < nu; ci++)
            for (int ck = 0; ck < nv; ck++)
            {
                if (!inside[ci, ck] || covered[ci, ck]) continue;
                var best = (i0: ci, i1: ci, k0: ck, k1: ck); float bestA = -1;
                for (int i0 = 0; i0 <= ci; i0++)
                    for (int i1 = ci; i1 < nu; i1++)
                        for (int k0 = 0; k0 <= ck; k0++)
                            for (int k1 = ck; k1 < nv; k1++)
                            {
                                float a = (us[i1 + 1] - us[i0]) * (vs[k1 + 1] - vs[k0]);
                                if (a > bestA && All(i0, i1, k0, k1)) { bestA = a; best = (i0, i1, k0, k1); }
                            }
                boxes.Add(best);
                if (boxes.Count > 4) return null;
                for (int i = best.i0; i <= best.i1; i++) for (int k = best.k0; k <= best.k1; k++) covered[i, k] = true;
            }

        // as (u0, u1, v0, v1) boxes; the main wing is the widest (the tallest ridge), then the biggest
        var bx = boxes.Select(b => (u0: us[b.i0], u1: us[b.i1 + 1], v0: vs[b.k0], v1: vs[b.k1 + 1])).ToList();
        static float Narrow((float u0, float u1, float v0, float v1) b) => MathF.Min(b.u1 - b.u0, b.v1 - b.v0);
        if (bx.Any(b => Narrow(b) < 2f)) return null;
        var main = bx.OrderByDescending(Narrow).ThenByDescending(b => (b.u1 - b.u0) * (b.v1 - b.v0)).First();
        float mainHalfW = Narrow(main) * 0.5f;
        float riseMain = mainRise ?? mainHalfW * MathF.Tan(pitchDeg * MathF.PI / 180f);
        bool mainAlongU = main.u1 - main.u0 >= main.v1 - main.v0;
        float mainRidgeU = (main.u0 + main.u1) * 0.5f, mainRidgeV = (main.v0 + main.v1) * 0.5f;

        var wings = new List<Wing>();
        foreach (var b0 in bx)
        {
            var b = b0;
            bool isMain = b == main;
            bool alongU = b.u1 - b.u0 >= b.v1 - b.v0;
            bool outerS = true, outerE = true;   // the ends at the low / high end of the wing's long axis
            if (!isMain && alongU != mainAlongU)
            {
                // crossing the main wing: cut back to its ridge, and that end runs into its roof
                if (alongU)
                {
                    if (b.u0 < main.u0 + 0.1f && b.u1 > main.u0 + 0.1f && b.u1 <= main.u1 + 0.1f) { b.u1 = mainRidgeU; outerE = false; }
                    else if (b.u1 > main.u1 - 0.1f && b.u0 < main.u1 - 0.1f && b.u0 >= main.u0 - 0.1f) { b.u0 = mainRidgeU; outerS = false; }
                }
                else
                {
                    if (b.v0 < main.v0 + 0.1f && b.v1 > main.v0 + 0.1f && b.v1 <= main.v1 + 0.1f) { b.v1 = mainRidgeV; outerE = false; }
                    else if (b.v1 > main.v1 - 0.1f && b.v0 < main.v1 - 0.1f && b.v0 >= main.v0 - 0.1f) { b.v0 = mainRidgeV; outerS = false; }
                }
            }
            float lenU = b.u1 - b.u0, lenV = b.v1 - b.v0;
            var c = frame.Centre + frame.U * ((b.u0 + b.u1) * 0.5f) + frame.V * ((b.v0 + b.v1) * 0.5f);
            // a wing along V gets U = +V, so its start is still its low end
            var rect = alongU ? new Rect(c, frame.U, frame.V, lenU * 0.5f, lenV * 0.5f, 1f) : new Rect(c, frame.V, -frame.U, lenV * 0.5f, lenU * 0.5f, 1f);
            wings.Add(new Wing(rect, riseMain * (rect.HalfW / mainHalfW), outerS, outerE));
        }
        return wings;
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
