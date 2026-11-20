using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Brutalism (D32, Noor's cookbook S12–S14 and the "Brutalist Warsaw" preset P05; Tom's list): raw board-marked
/// concrete, deep shadows, heavy masses. One family with five facade grammars, chosen per building:
/// * <b>fins</b>: deep vertical concrete fins between the bays, windows recessed between them;
/// * <b>bands</b>: heavy horizontal spandrel bands standing out over ribbons of deep-set windows;
/// * <b>waffle</b>: an egg-crate grid of concrete frames, each window at the back of its own deep box;
/// * <b>pilotis</b>: the upper mass on columns, the ground floor glazed and set back 2.5 m (reads as a cantilever
///   while staying inside the footprint), bands above;
/// * <b>towers</b>: blank concrete stair and lift shafts at the ends of the main wall, rising over the roof, with
///   fins between them.
/// Always: a flat roof behind a deep concrete fascia, blank concrete end walls on narrow sides, a recessed entrance
/// under a heavy canopy. The function stays the function (a brutalist hospital is still a hospital): this style only
/// dresses offices, civic buildings and, under the preset, blocks of flats.
/// Evidence: an architecture tag; or the preset "brutalist" (the alternate city); or a concrete civic or office
/// building of 1962–1985; or (a district's roll) an undated concrete one of some size.
/// </summary>
public sealed class Brutalism : IStyle
{
    public const string Name = "brutalism";
    public const string Preset = "brutalist";
    public string Id => Name;

    public enum Grammar { Fins, Bands, Waffle, Pilotis, Towers }
    public Grammar Facade { get; private set; }

    private static bool Civic(string? t) => t is "civic" or "public" or "government" or "university" or "college"
        or "hospital" or "office" or "commercial" or "theatre" or "townhall" or "courthouse" or "library" or "museum"
        or "clinic" or "police" or "dormitory" or "hotel" or "sports_hall" or "train_station" or "transportation";
    private static bool Housing(string? t) => t is "apartments" or "residential";

    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (r.Raised || c.Site.Small || c.Site.Depth < 7f) return false;
        if (r.Architecture is { } a && a.Contains("brutal")) return true;
        int? lv = r.Levels ?? (r.Height is { } h and > 2f ? (int)MathF.Round((h - 1f) / 3.3f) : null);
        // the alternate city: offices, civic buildings and blocks of flats of some size; never houses, farms, sheds,
        // churches, or listed old buildings (the kamienica keeps its street)
        if (r.Preset == Preset)
            // (the glass skyline stays: towers over 60 m and anything built since 1990 keep their own styles)
            // (and the older city stays too: stone and brick buildings, MDM's included, built before concrete took over,
            // and the walled-in street rows of tenements)
            return !r.Heritage && r.StartYear is null or (>= 1945 and < 1990) && r.Height is null or < 60f && c.Site.Area >= 250f && lv is null or >= 2
                && r.Material is not ("sandstone" or "stone" or "brick") && !c.Site.Edges.Any(e => e.Exposure == Exposure.Party)
                && (Civic(r.Type) || Housing(r.Type) || r.Type is "yes" or null && c.Site.Area >= 400f);
        if (!Civic(r.Type)) return false;
        if (r.Material is not (null or "concrete" or "reinforced_concrete")) return false;
        // a listed concrete civic building of the late modern years: a monument of its time, brutalist for certain
        if (r.Heritage) return r.StartYear is >= 1962 and <= 1985 && r.Material is "concrete" or "reinforced_concrete";
        ulong seed = r.StageSeeds.TryGetValue("district", out var d) ? d : r.Seed;
        if (r.StartYear is { } y) return y is >= 1962 and <= 1985 && Rng.Stream(seed, "brutalism").Chance(0.55);
        return r.Material is "concrete" or "reinforced_concrete" && c.Site.Area > 600f && lv is >= 3 && Rng.Stream(seed, "brutalism").Chance(0.3);
    }

    public float Plinth => 0.4f;
    public float RoofExtra => 1.2f;   // the fascia
    public int InferLevels(BuildingGenerator.Context c) => c.PlanRng.Weighted<int>([(3, 2), (4, 3), (5, 3), (7, 2), (10, 1)]);
    public float StoreyHeight(BuildingGenerator.Context c) => 3.4f;
    public string InferRoof(BuildingGenerator.Context c) => "flat";
    public float RoofPitch(BuildingGenerator.Context c) => 10f;

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        var rect = Roofs.MinRect(c.Site.Outer);
        bool longWall = rect.HalfL >= 12f;
        Facade = L.Weighted<Grammar>([(Grammar.Fins, 3), (Grammar.Bands, 3), (Grammar.Waffle, 2), (Grammar.Pilotis, c.Plan.Levels.Value >= 3 ? 2 : 0), (Grammar.Towers, longWall && c.Plan.Levels.Value >= 4 ? 2 : 0)]);
        // concrete greys: fresh-ish, weathered, warm, dark and stained
        var concrete = L.Weighted<(string, string)>([(("grey concrete", "8f8e89"), 4), (("weathered", "7e7d78"), 3), (("warm concrete", "958f84"), 2), (("dark stained", "6b6a66"), 2), (("pale concrete", "a3a19b"), 1)]);
        c.Plan = c.Plan with { Palette = $"brutalism, {Facade.ToString().ToLowerInvariant()}, {concrete.Item1}" };
        c.Building.Plan = c.Plan;
        m["concrete_board"] = Kit.Mat("concrete_board", concrete.Item2, 0.95f);
        m["wall"] = Kit.Mat("wall", concrete.Item2, 0.95f);
        m["fin"] = Kit.Mat("concrete_board", Shade(concrete.Item2, 0.94f), 0.95f) with { Role = "fin" };
        m["plinth"] = Kit.Mat("plinth", Shade(concrete.Item2, 0.75f), 0.95f);
        m["reveal"] = Kit.Mat("reveal", Shade(concrete.Item2, 0.85f), 0.95f);
        m["glass"] = Kit.Mat("glass", "222b31", 0.06f, 0.1f);
        m["shopfront"] = Kit.Mat("shopfront", "26313a", 0.05f, 0.1f);
        m["frame"] = Kit.Mat("frame", L.Weighted<string>([("2f3133", 3), ("6e5a40", 1), ("8a8c8e", 1)]), 0.5f, 0.3f);
        m["door"] = Kit.Mat("door", "2f3133", 0.6f);
        m["soffit"] = Kit.Mat("soffit", Shade(concrete.Item2, 0.7f), 0.95f);
        m["roof"] = Kit.Mat("roof", "56575a", 0.9f);
    }

    private static string Shade(string hex, float k)
    {
        int r = Convert.ToInt32(hex[..2], 16), g = Convert.ToInt32(hex[2..4], 16), b = Convert.ToInt32(hex[4..6], 16);
        static int C(float v) => Math.Clamp((int)MathF.Round(v), 0, 255);
        return $"{C(r * k):x2}{C(g * k):x2}{C(b * k):x2}";
    }

    private const string Skin = "concrete_board";

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        const float bay = 3.6f, setback = 2.5f;
        var doors = Entrances.Place(c, e => Math.Max(1, (int)(e.Length / 40f)), bay);
        // the main wall: the longest open one, street first (towers stand at its ends, pilotis run along it)
        var main = plan.Edges.Select((e, i) => (e, i)).Where(x => x.e.Ring == 0 && x.e.Exposure is not (Exposure.Party or Exposure.Sliver))
            .OrderByDescending(x => x.e.Length * (x.e.Exposure == Exposure.Street ? 1.5f : 1f)).FirstOrDefault();
        float fascia = 1.2f;

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            // the fascia: a deep band over the top of every wall
            Kit.Wall(wall.Mesh, Skin, f, 0, len, top, top + fascia);
            // blank: party walls, slivers, and the narrow ends (a brutalist block shows blank concrete ends)
            bool blind = e.Exposure is Exposure.Party or Exposure.Sliver || len < 6.5f;
            if (blind) { Kit.Wall(wall.Mesh, Skin, f, 0, len, datum, top); continue; }

            bool isMain = main.e != null && ei == main.i;
            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bw = len / bays;
            // towers: the first and last bay of the main wall are solid shafts that rise over the roof
            bool towers = Facade == Grammar.Towers && isMain && bays >= 6;
            bool pilotis = Facade == Grammar.Pilotis && isMain && bays >= 3;

            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                bool shaft = towers && (bi == 0 || bi == bays - 1);
                if (shaft)
                {
                    Kit.Wall(wall.Mesh, Skin, f, u0, u1, datum, top);
                    continue;
                }
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    var door = s == 0 ? doors.Where(d => d.Edge == ei && d.U >= u0 && d.U < u1).Select(d => (DoorSite?)d).FirstOrDefault() : null;
                    if (s == 0 && pilotis)
                    {
                        // the ground floor set back behind a column at the bay's start
                        var back = new Kit.Frame(f.Origin - f.Out * setback, f.U, f.Out);
                        Kit.WallWithOpening(wall.Mesh, Skin, "reveal", "shopfront", back, u0, u1, y0, y1, u0 + 0.3f, u1 - 0.3f, y0 + 0.05f, y1 - 0.5f, 0.15f);
                        wall.Mesh.AddQuad("soffit", f.P(u0, y1, 0), f.P(u0, y1, -setback), f.P(u1, y1, -setback), f.P(u1, y1, 0), -Vector3.UnitY);
                        if (bi > 0) Kit.Box(wall.Mesh, Skin, f, u0 - 0.35f, u0 + 0.35f, ground - 0.2f, y1, -0.7f, 0f);
                        if (door != null) b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, y0, -setback + 0.05f), f.Out));
                        continue;
                    }
                    if (door != null)
                    {
                        // a recessed entrance under a heavy canopy
                        float hw = MathF.Min(1.1f, bw * 0.5f - 0.3f);
                        Kit.WallWithOpening(wall.Mesh, Skin, "reveal", "door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + 2.6f, 0.9f);
                        b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{bi}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                        Kit.Box(trim.Mesh, "fin", f, uc - hw - 0.8f, uc + hw + 0.8f, y0 + 2.9f, y0 + 3.3f, 0, 1.0f);
                        continue;
                    }
                    Window(wall.Mesh, trim.Mesh, f, u0, u1, y0, y1, sh);
                }
                // the grammar's relief over this bay
                switch (Facade)
                {
                    case Grammar.Fins or Grammar.Towers when bi > 0 && !(towers && bi == bays - 1):
                        Kit.Box(trim.Mesh, "fin", f, u0 - 0.15f, u0 + 0.15f, datum + (pilotis ? sh : 0), top + fascia, 0, 0.85f);
                        break;
                    case Grammar.Waffle:
                        for (int s = 0; s < levels; s++)
                        {
                            float y0 = datum + s * sh, y1 = y0 + sh;
                            Kit.Box(trim.Mesh, "fin", f, u0, u0 + 0.22f, y0, y1, 0, 0.6f);
                            Kit.Box(trim.Mesh, "fin", f, u0, u1, y0, y0 + 0.22f, 0, 0.6f);
                        }
                        break;
                }
            }
            // close the ends of the set-back ground floor
            if (pilotis)
            {
                float y1 = datum + sh;
                wall.Mesh.AddQuad(Skin, f.P(0, datum, -setback), f.P(0, datum, 0), f.P(0, y1, 0), f.P(0, y1, -setback), f.U);
                wall.Mesh.AddQuad(Skin, f.P(len, datum, 0), f.P(len, datum, -setback), f.P(len, y1, -setback), f.P(len, y1, 0), -f.U);
            }
            if (Facade == Grammar.Waffle)
            {
                Kit.Box(trim.Mesh, "fin", f, len - 0.22f, len, datum, top, 0, 0.6f);
                Kit.Box(trim.Mesh, "fin", f, 0, len, top - 0.22f, top, 0, 0.6f);
            }
            // heavy horizontal bands at every floor (bands, pilotis)
            if (Facade is Grammar.Bands or Grammar.Pilotis)
                for (int s = 1; s <= levels; s++)
                {
                    float y = datum + s * sh;
                    Kit.Box(trim.Mesh, "fin", f, 0, len, y - 0.9f, y + 0.05f, 0, 0.55f);
                }
            if (towers)
            {
                // the shafts rise 3.5 m over the fascia, blank, with a slot window
                foreach (float uu in new[] { 0f, len - bw })
                {
                    Kit.Box(trim.Mesh, Skin, f, uu, uu + bw, top, top + fascia + 3.5f, -bw * 0.9f, 0.3f);
                    Kit.Box(trim.Mesh, "glass", f, uu + bw * 0.45f, uu + bw * 0.55f, datum + 1f, top - 1f, 0f, 0.02f);
                }
            }
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top + 0.3f, top + 0.4f, sides: false);
        // a plant room on the roof, set back
        var r = Roofs.MinRect(c.Site.Outer);
        var at = r.At(r.HalfL * c.Detail.Range(-0.3f, 0.3f), 0);
        float hx = MathF.Min(4f, r.HalfL * 0.3f), hz = MathF.Min(3f, r.HalfW * 0.5f);
        if (Polygon.Contains(c.Site.Outer, at + new Vector2(hx, hz)) && Polygon.Contains(c.Site.Outer, at - new Vector2(hx, hz)))
            Kit.BoxXZ(roof.Mesh, Skin, new Vector3(at.X - hx, top + 0.3f, at.Y - hz), new Vector3(at.X + hx, top + 3.2f, at.Y + hz));
    }

    /// <summary>A deep-set window: 0.5 m reveals of concrete, a dark frame with one mullion.</summary>
    private void Window(Mesh wall, Mesh trim, Kit.Frame f, float u0, float u1, float y0, float y1, float sh)
    {
        float uc = (u0 + u1) * 0.5f, bw = u1 - u0;
        float hw, sill, head;
        switch (Facade)
        {
            case Grammar.Bands or Grammar.Pilotis: hw = bw * 0.5f - 0.05f; sill = 0.95f; head = sh - 0.95f; break;   // ribbons
            case Grammar.Waffle: hw = bw * 0.5f - 0.35f; sill = 0.5f; head = sh - 0.45f; break;
            default: hw = MathF.Min(1.1f, bw * 0.5f - 0.35f); sill = 0.7f; head = sh - 0.5f; break;
        }
        float a = uc - hw, z = uc + hw, ys = y0 + sill, yh = y0 + head;
        Kit.WallWithOpening(wall, Skin, "reveal", "glass", f, u0, u1, y0, y1, a, z, ys, yh, 0.5f);
        Kit.Box(trim, "frame", f, uc - 0.04f, uc + 0.04f, ys, yh, -0.48f, -0.44f);
        Kit.Box(trim, "frame", f, a, z, ys, ys + 0.06f, -0.48f, -0.44f);
    }
}
