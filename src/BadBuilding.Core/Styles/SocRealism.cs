using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Socialist realism (socrealizm, 1949–1956; D31, Noor's cookbook R019 and S09, Tom's list): MDM on Marszałkowska,
/// Plac Konstytucji, the rebuilt avenues. The street front is a monument; the courtyard side is ordinary:
/// * a heavy rusticated stone base, the ground floor of shops with big glass and stone piers; on some long fronts an
///   **arcade** (the ground-floor wall set back 3 m behind stone pillars, the corners left solid);
/// * the storeys above in sandstone-coloured render, tall windows with stone surrounds and sills, a string course over
///   the base and another under the top floor;
/// * **colossal pilasters** running up the middle storeys every two bays on the richer fronts, and relief panels
///   between the first-floor windows;
/// * a deep **cornice**, and over it an attic with a balustrade hiding the flat roof;
/// * on the courtyard: plain render, plain windows.
/// Evidence: a mapped year of 1949–1956 or a socialist-realism architecture tag; with no year (nearly always), a big
/// perimeter block of five to nine storeys facing a wide avenue (D31 has the rule).
/// </summary>
public sealed class SocRealism : IStyle
{
    public const string Name = "socrealism";
    public string Id => Name;

    private bool _arcade, _pilasters, _reliefs;

    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (c.Site.Small) return false;
        if (r.Type is not ("apartments" or "residential" or "yes" or null or "commercial" or "retail" or "office")) return false;
        if (r.Architecture is { } a && (a.Contains("socialist") || a.Contains("socrealism") || a.Contains("stalinist"))) return true;
        if (r.StartYear is { } y) return y is >= 1949 and <= 1956 && Tall(r) is >= 3;
        // a piece in the air (MDM's blocks are mapped in pieces from 6 and 24 m): only on stone evidence
        if (r.Raised) return r.Material is "sandstone" or "stone";
        int? lv = Tall(r);
        // stone-clad and of the size: the material is the evidence (MDM's pieces are tagged sandstone)
        if (r.Material is "sandstone" or "stone" && lv is >= 4 and <= 10 && c.Site.Area > 300f) return true;
        // listed and undated is the kamienica's evidence (D10), unless it's bigger than any tenement: seven storeys or a
        // block over 1,500 m² (MDM is listed, and mostly undated on the map)
        if (r.Heritage && !(lv >= 7 || c.Site.Area > 1500f)) return false;
        if (lv is not (>= 5 and <= 9) || c.Site.Area < 900f || c.Site.Depth is < 10f or > 34f) return false;
        // facing an avenue: a street at least 9 m to its centre line in front of a long wall
        bool avenue = c.Site.Edges.Any(e => e.Ring == 0 && e.Exposure == Exposure.Street && e.Length >= 25f && FacesAvenue(c, e));
        if (!avenue) return false;
        // a perimeter block (courtyard) or a long frontage; then a roll, so not every big avenue block turns MDM
        bool block = c.Site.Holes.Count > 0 || c.Site.Edges.Where(e => e.Exposure == Exposure.Street).Sum(e => e.Length) > 80f;
        // the roll by district when the caller gives one, so a whole quarter goes one way, not every other block
        ulong seed = r.StageSeeds.TryGetValue("district", out var d) ? d : r.Seed;
        // a listed block of this size on an avenue is MDM-era for certain; an unlisted one by the district's roll
        return block && (r.Heritage || Rng.Stream(seed, "socrealism").Chance(0.6));
    }

    private static int? Tall(BuildingRequest r) => r.Levels ?? (r.Height is { } h and > 2f ? (int)MathF.Round((h - 1.5f) / 3.4f) : null);

    private static bool FacesAvenue(BuildingGenerator.Context c, WallEdge e)
    {
        var mid = (e.A + e.B) * 0.5f;
        foreach (var st in c.Request.Streets)
        {
            if (st.HalfWidth < 5.5f) continue;   // a four-lane carriageway; dual carriageways come as two ways
            for (int i = 0; i + 1 < st.Points.Count; i++)
            {
                var q = Polygon.ClosestOnSegment(mid, st.Points[i], st.Points[i + 1], out _);
                var off = q - mid;
                if (off.Length() <= 30f + st.HalfWidth && Vector2.Dot(off, e.Out) > 0.5f * off.Length()) return true;
            }
        }
        return false;
    }

    public float Plinth => 0.5f;
    public float RoofExtra => 1.6f;   // the cornice and the attic
    public int InferLevels(BuildingGenerator.Context c) => c.PlanRng.Weighted<int>([(5, 2), (6, 3), (7, 3), (8, 1)]);
    public float StoreyHeight(BuildingGenerator.Context c) => 3.4f;
    public string InferRoof(BuildingGenerator.Context c) => "flat";
    public float RoofPitch(BuildingGenerator.Context c) => 30f;

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        var stone = L.Weighted<(string, string)>([(("grey sandstone", "bdb4a2"), 4), (("pale sandstone", "cbc2ad"), 3), (("warm grey", "b3aa98"), 2), (("weathered", "a79e8d"), 1)]);
        _arcade = L.Chance(0.35);
        _pilasters = L.Chance(0.6);
        _reliefs = L.Chance(0.5);
        c.Plan = c.Plan with { Palette = $"socialist realism, {stone.Item1}{(_arcade ? ", arcade" : "")}{(_pilasters ? ", pilasters" : "")}{(_reliefs ? ", reliefs" : "")}" };
        c.Building.Plan = c.Plan;
        string trim = Shade(stone.Item2, 0.92f);
        m["wall"] = Kit.Mat("wall", stone.Item2, 0.9f);
        m["wall_yard"] = Kit.Mat("wall_yard", L.Weighted<string>([("cfc7b5", 2), ("bdb6a6", 1)]), 0.92f);
        m["stone"] = Kit.Mat("stone", trim, 0.85f);
        m["rustic"] = Kit.Mat("rustic", L.Weighted<string>([("8f877a", 2), ("7d776c", 1), ("9c9282", 1)]), 0.9f);
        m["relief"] = Kit.Mat("relief", Shade(stone.Item2, 1.06f), 0.85f);
        m["plinth"] = Kit.Mat("plinth", "6e685f", 0.95f);
        m["reveal"] = Kit.Mat("reveal", trim, 0.9f);
        m["glass"] = Kit.Mat("glass", "28333b", 0.08f, 0.1f);
        m["shopfront"] = Kit.Mat("shopfront", "2b3740", 0.05f, 0.1f);
        m["frame"] = Kit.Mat("frame", L.Weighted<string>([("e6e2d8", 2), ("5a4434", 1)]), 0.6f);
        m["door"] = Kit.Mat("door", "4a3a2c", 0.7f);
        m["ceiling"] = Kit.Mat("ceiling", Shade(stone.Item2, 0.8f), 0.9f);
        m["roof"] = Kit.Mat("roof", "5c5d5f", 0.9f);
    }

    private static string Shade(string hex, float k)
    {
        int r = Convert.ToInt32(hex[..2], 16), g = Convert.ToInt32(hex[2..4], 16), b = Convert.ToInt32(hex[4..6], 16);
        static int C(float v) => Math.Clamp((int)MathF.Round(v), 0, 255);
        return $"{C(r * k):x2}{C(g * k):x2}{C(b * k):x2}";
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        const float bay = 3.3f, arcadeDepth = 3f;
        var doors = Entrances.Place(c, e => Math.Max(1, (int)(e.Length / 30f)), bay);
        // the rusticated base: the ground floor, and the one above it on blocks of six storeys or more (MDM)
        bool raised = c.Request.Raised;
        int baseStoreys = raised ? 0 : levels >= 6 ? 2 : 1;
        float baseTop = datum + baseStoreys * sh;
        var balconyRows = levels >= 6 ? new[] { 2, levels - 2 } : new[] { Math.Max(1, levels - 2) };

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            var cuts = c.Passages.Select(p => p.Cut(ei)).OfType<(float, float)>().ToList();
            var pass = c.Passages.FirstOrDefault(p => p.Cut(ei) != null);
            float cutLow = pass?.Floor ?? 0, cutHigh = pass?.Ceiling ?? 0;
            Kit.WallCut(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum, cuts, cutLow, cutHigh);
            if (e.Exposure is Exposure.Party or Exposure.Sliver || len < 1.5f) { Kit.Wall(wall.Mesh, "wall_yard", f, 0, len, datum, top); continue; }

            bool front = e.Exposure == Exposure.Street && e.Ring == 0;
            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bw = len / bays;
            string body = front ? "wall" : "wall_yard";

            // ---- the ground floor (none on a piece in the air: it starts with ordinary storeys) ----
            if (!raised) {
            // an arcade on a long front: between solid corner piers, the shop wall sits 3 m back behind pillars
            bool arcade = front && _arcade && len >= 20f && cuts.Count == 0 && !doors.Any(d => d.Edge == ei);
            float a0 = arcade ? MathF.Max(3.5f, bw) : 0, a1 = arcade ? len - MathF.Max(3.5f, bw) : 0;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                if (Kit.Overlaps(cuts, u0, u1)) { Kit.WallCut(wall.Mesh, front ? "rustic" : body, f, u0, u1, datum, baseTop, cuts, cutLow, cutHigh); continue; }
                var door = doors.Where(d => d.Edge == ei && d.U >= u0 && d.U < u1).Select(d => (DoorSite?)d).FirstOrDefault();
                if (arcade && uc > a0 && uc < a1)
                {
                    // the arcade bay: a pair of columns on the facade line at the bay's start (with capitals), the shop
                    // front at the back (Tom's photo of Marszałkowska 34)
                    foreach (float cu in new[] { u0 + 0.05f, u0 + 0.75f })
                    {
                        Kit.Box(wall.Mesh, "stone", f, cu, cu + 0.5f, datum, baseTop - 1.0f, -0.6f, -0.1f);
                        Kit.Box(wall.Mesh, "stone", f, cu - 0.1f, cu + 0.6f, baseTop - 1.0f, baseTop - 0.6f, -0.7f, 0f);
                    }
                    Kit.WallWithOpening(wall.Mesh, "rustic", "stone", "shopfront", new Kit.Frame(f.Origin - f.Out * arcadeDepth, f.U, f.Out), u0, u1, datum, baseTop - 0.6f, u0 + 0.5f, u1 - 0.5f, datum + 0.2f, baseTop - 1.0f, 0.15f);
                    // the arcade's ceiling and the lintel over the pillars
                    wall.Mesh.AddQuad("ceiling", f.P(u0, baseTop - 0.6f, 0), f.P(u0, baseTop - 0.6f, -arcadeDepth), f.P(u1, baseTop - 0.6f, -arcadeDepth), f.P(u1, baseTop - 0.6f, 0), -Vector3.UnitY);
                    Kit.Wall(wall.Mesh, "rustic", f, u0, u1, baseTop - 0.6f, baseTop);
                    b.Markers.Add(new Marker(c.Id($"shop.e{e.Ring}.{e.Index}.{bi}"), "shop_entrance", f.P(uc, datum, -arcadeDepth + 0.05f), f.Out));
                    continue;
                }
                if (arcade && uc >= a1 - 0.01f && uc - bw < a1 + 0.01f && bi > 0)
                    foreach (float cu in new[] { u0 + 0.05f, u0 + 0.75f })
                    {
                        Kit.Box(wall.Mesh, "stone", f, cu, cu + 0.5f, datum, baseTop - 1.0f, -0.6f, -0.1f);
                        Kit.Box(wall.Mesh, "stone", f, cu - 0.1f, cu + 0.6f, baseTop - 1.0f, baseTop - 0.6f, -0.7f, 0f);
                    }
                if (door != null)
                {
                    float hw = MathF.Min(0.95f, bw * 0.5f - 0.25f);
                    Kit.WallWithOpening(wall.Mesh, front ? "rustic" : body, "stone", "door", f, u0, u1, datum, baseTop, uc - hw, uc + hw, datum, datum + 3.0f, 0.35f);
                    b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, datum, 0.05f), f.Out));
                    if (front) Kit.Box(trim.Mesh, "stone", f, uc - hw - 0.3f, uc + hw + 0.3f, datum + 3.0f, datum + 3.35f, 0, 0.25f);
                    continue;
                }
                if (front)
                {
                    // a shop: big glass between stone piers
                    Kit.WallWithOpening(wall.Mesh, "rustic", "stone", "shopfront", f, u0, u1, datum, baseTop, u0 + 0.45f, u1 - 0.45f, datum + 0.35f, baseTop - 0.7f, 0.25f);
                    b.Markers.Add(new Marker(c.Id($"shop.e{e.Ring}.{e.Index}.{bi}"), "shop_entrance", f.P(uc, datum, 0.05f), f.Out));
                }
                else Window(wall.Mesh, trim.Mesh, f, body, u0, u1, datum, baseTop, sh, false, false);
            }
            // close the arcade's two ends (the side walls between the facade line and the shop fronts)
            if (arcade)
            {
                for (int k = 0; k < 3; k++) Kit.Box(trim.Mesh, "rustic", f, a0, a1, ground - 0.2f, datum - k * MathF.Max(0.05f, (datum - ground) / 3f), 0, 0.35f * (k + 1));
                var inBays = Enumerable.Range(0, bays).Where(k => { float m = (k + 0.5f) * bw; return m > a0 && m < a1; }).ToList();
                if (inBays.Count > 0)
                {
                    float s0 = inBays[0] * bw, s1 = (inBays[^1] + 1) * bw;
                    wall.Mesh.AddQuad("rustic", f.P(s0, datum, 0), f.P(s0, datum, -arcadeDepth), f.P(s0, baseTop - 0.6f, -arcadeDepth), f.P(s0, baseTop - 0.6f, 0), f.U);
                    wall.Mesh.AddQuad("rustic", f.P(s1, datum, -arcadeDepth), f.P(s1, datum, 0), f.P(s1, baseTop - 0.6f, 0), f.P(s1, baseTop - 0.6f, -arcadeDepth), -f.U);
                }
            }
            }
            // rustication: horizontal joints on the base
            if (front && !raised)
                for (float y = datum + 0.45f; y < datum + sh - 0.1f; y += 0.45f)
                    Kit.Box(trim.Mesh, "rustic", f, 0, len, y - 0.03f, y, 0, 0.03f);

            // ---- the storeys above ----
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw;
                for (int s = raised ? 0 : 1; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    bool inBase = front && s < baseStoreys;
                    if (front) PairedWindows(wall.Mesh, trim.Mesh, f, inBase ? "rustic" : body, u0, u1, y0, y1, sh, s == baseStoreys);
                    else Window(wall.Mesh, trim.Mesh, f, body, u0, u1, y0, y1, sh, false, false);
                    // a small stone balcony with a balustrade: every third bay, on two rows
                    if (front && bi % 3 == 1 && bi < bays - 1 && balconyRows.Contains(s)) Balcony(trim.Mesh, f, u0, u1, y0);
                    if (front && _reliefs && s == baseStoreys && bi % 2 == 1 && bi < bays - 1 && bi % 3 != 1)
                        Kit.Box(trim.Mesh, "relief", f, u0 + 0.5f, u1 - 0.5f, y1 - 0.75f, y1 - 0.1f, 0, 0.08f);
                }
                // colossal pilasters: every second bay boundary, from the string course to the top string course
                if (front && _pilasters && bi > 0 && bi % 2 == 0 && levels >= 4)
                    Kit.Box(trim.Mesh, "stone", f, u0 - 0.3f, u0 + 0.3f, baseTop + 0.25f, datum + (levels - 1) * sh, 0, 0.22f);
            }
            if (front)
            {
                // string course over the base, another under the top floor, the cornice and the attic with balusters
                if (!raised) Kit.Box(trim.Mesh, "stone", f, 0, len, baseTop, baseTop + 0.3f, 0, 0.3f);
                if (levels >= 4) Kit.Box(trim.Mesh, "stone", f, 0, len, datum + (levels - 1) * sh - 0.05f, datum + (levels - 1) * sh + 0.2f, 0, 0.2f);
                Kit.Box(trim.Mesh, "stone", f, 0, len, top - 0.35f, top, 0, 0.35f);
                Kit.Box(trim.Mesh, "stone", f, 0, len, top, top + 0.35f, 0, 0.8f);
            }
            // the attic: a wall over the cornice; balusters on the street front
            Kit.Wall(wall.Mesh, body, f, 0, len, top, top + 1.2f);
            if (front)
            {
                Kit.Box(trim.Mesh, "stone", f, 0, len, top + 1.2f, top + 1.35f, -0.05f, 0.15f);
                for (float u = 0.3f; u < len - 0.2f; u += 0.32f)
                    Kit.Box(trim.Mesh, "stone", f, u, u + 0.12f, top + 0.4f, top + 1.2f, 0.02f, 0.12f);
                for (float u = 0.2f; u < len; u += MathF.Max(4f, len / MathF.Round(len / 6f)))
                    Kit.Box(trim.Mesh, "stone", f, u, u + 0.55f, top + 0.35f, top + 1.6f, -0.05f, 0.25f);
            }
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top + 0.5f, top + 0.6f, sides: false);
    }

    /// <summary>Two narrow windows close together in a bay, with a stone surround round the pair (MDM).</summary>
    private static void PairedWindows(Mesh wall, Mesh trim, Kit.Frame f, string body, float u0, float u1, float y0, float y1, float sh, bool nobile)
    {
        float uc = (u0 + u1) * 0.5f, bw = u1 - u0;
        float w = MathF.Min(0.55f, bw * 0.17f), gap = 0.35f;
        float sill = nobile ? 0.55f : 0.85f, head = sh - 0.5f, ys = y0 + sill, yh = y0 + head;
        float l0 = uc - gap / 2 - w, l1 = uc - gap / 2, r0 = uc + gap / 2, r1 = uc + gap / 2 + w;
        // the wall in four pieces round two openings: left of the pair, the pier between, right of the pair
        Kit.WallWithOpening(wall, body, "stone", "glass", f, u0, uc, y0, y1, l0, l1, ys, yh, 0.28f);
        Kit.WallWithOpening(wall, body, "stone", "glass", f, uc, u1, y0, y1, r0, r1, ys, yh, 0.28f);
        foreach (var (a, z) in new[] { (l0, l1), (r0, r1) })
        {
            float m = (a + z) * 0.5f, yt = yh - (yh - ys) * 0.3f;
            Kit.Box(trim, "frame", f, m - 0.03f, m + 0.03f, ys, yh, -0.26f, -0.22f);
            Kit.Box(trim, "frame", f, a, z, yt - 0.03f, yt + 0.03f, -0.26f, -0.22f);
        }
        // the surround round the pair, a sill under it and a cap (a heavier one on the first floor over the base)
        Kit.Box(trim, "stone", f, l0 - 0.14f, l0, ys, yh, 0, 0.08f);
        Kit.Box(trim, "stone", f, r1, r1 + 0.14f, ys, yh, 0, 0.08f);
        Kit.Box(trim, "stone", f, l0 - 0.2f, r1 + 0.2f, ys - 0.1f, ys, 0, 0.12f);
        Kit.Box(trim, "stone", f, l0 - 0.22f, r1 + 0.22f, yh, yh + (nobile ? 0.32f : 0.16f), 0, nobile ? 0.2f : 0.1f);
    }

    /// <summary>A small stone balcony on brackets with a balustrade, in front of a bay's pair of windows.</summary>
    private static void Balcony(Mesh m, Kit.Frame f, float u0, float u1, float y0)
    {
        float a = u0 + 0.35f, z = u1 - 0.35f;
        const float d = 0.8f;
        Kit.Box(m, "stone", f, a, z, y0 - 0.18f, y0, 0, d);
        Kit.Box(m, "stone", f, a, z, y0 + 0.85f, y0 + 0.97f, d - 0.15f, d);
        for (float u = a + 0.05f; u < z - 0.05f; u += 0.22f) Kit.Box(m, "stone", f, u, u + 0.09f, y0, y0 + 0.85f, d - 0.13f, d - 0.03f);
        Kit.Box(m, "stone", f, a, a + 0.12f, y0, y0 + 0.97f, 0, d);
        Kit.Box(m, "stone", f, z - 0.12f, z, y0, y0 + 0.97f, 0, d);
    }

    private static void Window(Mesh wall, Mesh trim, Kit.Frame f, string body, float u0, float u1, float y0, float y1, float sh, bool rich, bool nobile)
    {
        float uc = (u0 + u1) * 0.5f, bw = u1 - u0;
        float hw = MathF.Min(0.65f, bw * 0.22f), sill = nobile ? 0.6f : 0.9f, head = sh - 0.45f;
        float a = uc - hw, z = uc + hw, ys = y0 + sill, yh = y0 + head;
        Kit.WallWithOpening(wall, body, rich ? "stone" : "reveal", "glass", f, u0, u1, y0, y1, a, z, ys, yh, rich ? 0.3f : 0.18f);
        Kit.Box(trim, "frame", f, uc - 0.035f, uc + 0.035f, ys, yh, rich ? -0.28f : -0.16f, rich ? -0.24f : -0.12f);
        float yt = yh - (yh - ys) * 0.3f;
        Kit.Box(trim, "frame", f, a, z, yt - 0.03f, yt + 0.03f, rich ? -0.28f : -0.16f, rich ? -0.24f : -0.12f);
        Kit.Box(trim, rich ? "stone" : "reveal", f, a - 0.08f, z + 0.08f, ys - 0.08f, ys, 0, 0.1f);
        if (!rich) return;
        // stone surrounds; on the first floor (piano nobile) a pediment-like cap
        Kit.Box(trim, "stone", f, a - 0.15f, a, ys, yh, 0, 0.08f);
        Kit.Box(trim, "stone", f, z, z + 0.15f, ys, yh, 0, 0.08f);
        Kit.Box(trim, "stone", f, a - 0.2f, z + 0.2f, yh, yh + (nobile ? 0.3f : 0.15f), 0, nobile ? 0.18f : 0.1f);
    }
}
