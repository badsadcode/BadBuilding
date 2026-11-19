using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;
using BadBuilding.Geometry;

namespace BadBuilding.Styles;

/// <summary>
/// Houses (D26, Noor's cookbook R001–R006): the family Mazowsze is mostly made of. Five families, chosen from the type,
/// the build year when mapped, and the footprint's shape and size:
/// * <b>cottage</b> (chałupa, R001): one storey, a steep gable roof, small windows; whitewashed with painted window
///   trims (blue, green), or dark weathered timber planks; boarded gables, sometimes open shutters;
/// * <b>kostka</b> (PRL cube, R003): a nearly square two-storey block on a high plinth with basement windows, a low
///   hipped or flat roof, a band between the floors, a raised entrance up steps under a canopy, often a balcony;
///   grey dash plaster ("baranek"), beige, or pastel insulation;
/// * <b>modern</b> (R004): one or two storeys, clean eaves, big windows toward the garden (floor-length downstairs),
///   anthracite frames, sometimes a garage door on the street side;
/// * <b>semi-detached</b> and <b>terraced</b> (bliźniak, szeregowiec, R005–R006): the modern or kostka look on a unit
///   that shares one or two walls, the door kept away from them.
/// Everything inside the footprint, as with every style; the details stand at most 1 m proud (canopies, steps,
/// balconies, sills).
/// </summary>
public sealed class House : IStyle
{
    public const string Name = "house";
    public string Id => Name;

    public enum Family { Cottage, Kostka, Modern, SemiDetached, Terraced }
    public Family Kind { get; }
    /// <summary>The look the shared-wall families borrow (kostka or modern).</summary>
    private readonly Family _look;

    public House(BuildingGenerator.Context c)
    {
        Kind = FamilyOf(c);
        _look = Kind is Family.SemiDetached or Family.Terraced
            ? (c.Request.StartYear is >= 1945 and < 1990 || Rng.Stream(c.Request.Seed, "house.look").Chance(0.45) ? Family.Kostka : Family.Modern)
            : Kind;
    }

    public static bool HouseType(string? t) => t is "house" or "detached" or "semidetached_house" or "terrace" or "bungalow"
        or "cabin" or "villa" or "farm";

    /// <summary>A house: a house type (not raised, under 400 m²), or an untyped or "residential" building of house size
    /// (35–220 m², at most two storeys and 10.5 m) with no party wall (a walled-in one is a town house's job).</summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (r.Raised || c.Site.Holes.Count > 0) return false;
        if (HouseType(r.Type)) return c.Site.Area < 400f;
        if (r.Type is null or "yes" or "residential")
            return c.Site.Area is >= 35f and <= 220f && r.Levels is null or <= 2 && r.Height is null or <= 10.5f
                && !c.Site.Edges.Any(e => e.Exposure == Exposure.Party);
        return false;
    }

    public static Family FamilyOf(BuildingGenerator.Context c)
    {
        var r = c.Request;
        int party = c.Site.Edges.Count(e => e.Exposure == Exposure.Party);
        if (r.Type == "terrace" || party >= 2) return Family.Terraced;
        if (r.Type == "semidetached_house" || party == 1) return Family.SemiDetached;
        var rect = Roofs.MinRect(c.Site.Outer);
        float ratio = rect.HalfL / MathF.Max(rect.HalfW, 0.1f), area = c.Site.Area;
        if (r.StartYear is { } y)
            return y < 1945 ? Family.Cottage : y < 1990 ? (ratio < 1.45f ? Family.Kostka : Family.Cottage) : Family.Modern;
        var rng = Rng.Stream(r.Seed, "house.family");
        bool oneStorey = r.Levels is 1 || r.Height is < 5.5f;
        bool twoStorey = r.Levels is >= 2 || r.Height is >= 6.5f;
        if (ratio < 1.35f && area is >= 60f and <= 170f && !oneStorey)
            return rng.Weighted<Family>([(Family.Kostka, 6), (Family.Modern, 4)]);
        if (ratio >= 1.45f && area < 130f && !twoStorey)
            return rng.Weighted<Family>([(Family.Cottage, 65), (Family.Modern, 35)]);
        if (oneStorey && area < 140f)
            return rng.Weighted<Family>([(Family.Cottage, 50), (Family.Modern, 50)]);
        if (r.Type is "cabin") return Family.Cottage;
        return rng.Weighted<Family>([(Family.Modern, 70), (Family.Kostka, 18), (Family.Cottage, 12)]);
    }

    public float Plinth => _look switch { Family.Kostka => 1.0f, Family.Cottage => 0.25f, _ => 0.35f };
    public float RoofExtra => 0.4f;

    public int InferLevels(BuildingGenerator.Context c) => _look switch
    {
        Family.Cottage => 1,
        Family.Kostka => 2,
        _ => Kind is Family.SemiDetached or Family.Terraced ? 2 : c.Site.Area > 110f ? c.PlanRng.Weighted<int>([(1, 1), (2, 3)]) : c.PlanRng.Weighted<int>([(1, 2), (2, 1)]),
    };

    public float StoreyHeight(BuildingGenerator.Context c) => _look switch { Family.Cottage => 2.7f, Family.Kostka => 2.8f, _ => 3.0f };

    public string InferRoof(BuildingGenerator.Context c) => _look switch
    {
        Family.Cottage => "gabled",
        Family.Kostka => c.PlanRng.Weighted<string>([("hipped", 7), ("flat", 3)]),
        _ => c.PlanRng.Weighted<string>([("gabled", 5), ("hipped", 3), ("flat", 2)]),
    };

    public float RoofPitch(BuildingGenerator.Context c) => _look switch
    {
        Family.Cottage => c.PlanRng.Range(40f, 48f),
        Family.Kostka => c.PlanRng.Range(12f, 20f),
        _ => c.PlanRng.Range(30f, 40f),
    };

    // ---- looks ---------------------------------------------------------------------------------------------------------

    private bool _timber, _trims, _shutters, _balcony, _garage, _band;
    private string _frame = "e8e6e0";

    private static readonly (string Name, string Hex)[] Whitewash = [("white", "ebe7dc"), ("cream", "efe5c8"), ("grey wash", "d4d6cc"), ("blue wash", "cdd6d8")];
    private static readonly (string Name, string Hex)[] Planks = [("dark planks", "4f4133"), ("brown planks", "65523f"), ("grey planks", "5a554d")];
    private static readonly (string Name, string Hex)[] Trim = [("blue", "3e6290"), ("sky", "5a86a8"), ("green", "4d6e4a"), ("white", "e8e4da")];
    private static readonly (string Name, string Hex)[] Baranek = [("grey baranek", "9e9b93"), ("beige baranek", "b9ad94"), ("dun", "a8a08c")];
    private static readonly (string Name, string Hex)[] Pastel = [("yellow", "e6d6a2"), ("green", "cdd7bd"), ("salmon", "e0c2ad"), ("blue-grey", "d3d9df"), ("white", "ebe8e1")];
    private static readonly (string Name, string Hex)[] Modern = [("white", "ecebe6"), ("light grey", "cbc9c3"), ("beige", "ddd3bf"), ("warm white", "efe9dc")];
    // the colours Polish insulated houses actually come in, less often than white
    private static readonly (string Name, string Hex)[] Warm = [("sand", "e2d3b0"), ("light yellow", "eadfae"), ("soft salmon", "e3c9b5"), ("pale green", "d5dcca"), ("light ochre", "dcc28e")];

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, roof, name;
        _band = false;
        switch (_look)
        {
            case Family.Cottage:
                _timber = L.Chance(0.35);
                var w = _timber ? L.Pick(Planks) : L.Pick(Whitewash);
                wall = w.Hex; name = w.Name;
                _trims = !_timber ? L.Chance(0.75) : L.Chance(0.4);
                var t = _timber ? L.Weighted<(string, string)>([(Trim[0], 2), (Trim[3], 3), (Trim[2], 1)]) : L.Pick(Trim[..3]);
                _frame = t.Item2;
                _shutters = _trims && L.Chance(0.3);
                if (_trims) name += $", {t.Item1} trims{(_shutters ? " and shutters" : "")}";
                // eternit (asbestos-cement), old clay, rusty or dark sheet
                roof = L.Weighted<string>([("7b7b74", 4), ("8a4a35", 3), ("6e3a2a", 2), ("3d3d3b", 2)]);
                m["boards"] = Kit.Mat("boards", _timber ? wall : L.Pick(Planks).Hex, 0.95f);
                break;
            case Family.Kostka:
                // a real-life mix (Tom): over half still in the original dash plaster, the rest insulated in pastels
                bool original = L.Chance(0.55);
                var k = original ? L.Pick(Baranek) : L.Pick(Pastel);
                wall = k.Hex; name = original ? k.Name : $"{k.Name} insulation";
                _frame = original ? L.Weighted<string>([("e6e3dc", 3), ("5a4434", 2)]) : "eceae4";
                roof = L.Weighted<string>([("5c5d5f", 3), ("7b7b74", 2), ("5a4230", 2), ("8a3b2c", 2)]);
                _band = true;
                _balcony = c.Plan.Levels.Value >= 2 && L.Chance(0.6);
                break;
            default:
                var md = L.Chance(0.45) ? L.Pick(Warm) : L.Pick(Modern);
                wall = md.Hex; name = md.Name;
                _frame = L.Weighted<string>([("3a3d40", 5), ("eceae4", 2), ("6b5440", 1)]);
                roof = L.Weighted<string>([("3b3d40", 5), ("6e2e26", 2), ("4e3b30", 2), ("8a3b2c", 1)]);
                _garage = Kind == Family.Modern && c.Site.Area > 110f && L.Chance(0.45);
                break;
        }
        c.Plan = c.Plan with { Palette = $"{Kind.ToString().ToLowerInvariant()}{(Kind != _look ? $" ({_look.ToString().ToLowerInvariant()} look)" : "")}, {name}" };
        c.Building.Plan = c.Plan;
        m["wall"] = Kit.Mat("wall", wall, 0.9f);
        if (_timber) m["timber"] = Kit.Mat("timber", wall, 0.95f);
        m["wall_alt"] = Kit.Mat("wall_alt", _look == Family.Modern ? L.Weighted<string>([("4a4c4f", 2), (wall, 3)]) : wall, 0.9f);
        m["plinth"] = Kit.Mat("plinth", _look == Family.Kostka ? L.Weighted<string>([("7a766e", 2), ("8c8478", 1), ("5e5a54", 1)]) : "6a655e", 0.95f);
        m["band"] = Kit.Mat("band", "a9a59c", 0.9f);
        m["reveal"] = Kit.Mat("reveal", _look == Family.Cottage && _trims ? _frame : "8a857c", 0.9f);
        m["frame"] = Kit.Mat("frame", _frame, 0.6f);
        m["trim"] = Kit.Mat("trim", _frame, 0.7f);
        m["sill"] = Kit.Mat("sill", _look == Family.Modern ? "3a3d40" : "8c8a84", 0.7f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["door"] = Kit.Mat("door", L.Weighted<string>([("4a3a2c", 3), ("3a3d40", 2), ("6e5840", 1), ("8a7a64", 1)]), 0.7f);
        m["roller"] = Kit.Mat("roller", L.Weighted<string>([("bdbab3", 2), ("6a5848", 1), ("3a3d40", 1)]), 0.6f);
        m["canopy"] = Kit.Mat("canopy", "8a867e", 0.85f);
        m["steps"] = Kit.Mat("steps", "8c877e", 0.95f);
        m["concrete"] = Kit.Mat("concrete", "8f8b84", 0.95f);
        m["railing"] = Kit.Mat("railing", "33363a", 0.5f, 0.6f);
        m["chimney"] = Kit.Mat("chimney", L.Weighted<string>([("7a3d2e", 3), (wall, 2)]), 0.95f);
        m["roof"] = Kit.Mat("roof", roof, 0.85f);
        m["soffit"] = Kit.Mat("soffit", _look == Family.Cottage ? "5a4a3a" : "6a5a48", 0.9f);
    }

    // ---- building ------------------------------------------------------------------------------------------------------

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        float bay = _look switch { Family.Cottage => 2.8f, Family.Kostka => 3.0f, _ => 3.3f };
        // one front door, on the street side when there is one, never on a shared wall
        var doors = Entrances.Place(c, _ => 1, bay);
        var street = plan.Edges.Where(e => e.Exposure == Exposure.Street).OrderByDescending(e => e.Length).FirstOrDefault();
        var streetOut = street?.Out ?? (doors.Count > 0 ? plan.Edges[doors[0].Edge].Out : Vector2.Zero);
        string body = _timber ? "timber" : "wall";
        var D = c.Detail;

        // the garage door (modern): the street wall's bay farthest from the front door
        int garageEdge = -1; float garageU = 0;
        if (_garage && street != null && street.Length >= 6.5f)
        {
            garageEdge = plan.Edges.ToList().IndexOf(street);
            float du = doors.FirstOrDefault(d => d.Edge == garageEdge).U;
            garageU = doors.Any(d => d.Edge == garageEdge) ? (du > street.Length * 0.5f ? 1.9f : street.Length - 1.9f) : street.Length - 1.9f;
        }

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            bool blind = e.Exposure is Exposure.Party or Exposure.Sliver || len < 1.6f;
            bool garden = streetOut != Vector2.Zero && Vector2.Dot(e.Out, streetOut) < -0.7f;

            // the plinth, with basement windows on a kostka
            if (_look == Family.Kostka && !blind && Plinth > 0.7f && len >= 3f)
            {
                int nb = Math.Max(1, (int)(len / 3.2f));
                float bw = len / nb;
                for (int k = 0; k < nb; k++)
                {
                    float uc = (k + 0.5f) * bw;
                    bool win = k % 2 == 0 && !(garageEdge == ei && MathF.Abs(uc - garageU) < 1.8f) && !doors.Any(d => d.Edge == ei && MathF.Abs(d.U - uc) < 1.2f);
                    if (win) Kit.WallWithOpening(wall.Mesh, "plinth", "reveal", "glass", f, k * bw, (k + 1) * bw, ground - 0.5f, datum, uc - 0.35f, uc + 0.35f, datum - 0.55f, datum - 0.15f, 0.12f);
                    else Kit.Wall(wall.Mesh, "plinth", f, k * bw, (k + 1) * bw, ground - 0.5f, datum);
                }
            }
            else Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);

            if (blind) { Kit.Wall(wall.Mesh, body, f, 0, len, datum, top); continue; }

            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bwid = len / bays;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bwid, u1 = u0 + bwid, uc = (u0 + u1) * 0.5f;
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    var door = doors.Where(d => d.Edge == ei && d.U >= u0 && d.U < u1).Select(d => (DoorSite?)d).FirstOrDefault();
                    if (s == 0 && door != null) { FrontDoor(c, wall.Mesh, trim.Mesh, f, e, u0, u1, door.Value.U, y0, y1); continue; }
                    if (s == 0 && ei == garageEdge && garageU >= u0 - 0.6f && garageU <= u1 + 0.6f && bwid >= 2.9f)
                    {
                        Kit.WallWithOpening(wall.Mesh, body, "reveal", "roller", f, u0, u1, y0, y1, uc - 1.25f, uc + 1.25f, y0, y0 + 2.2f, 0.12f);
                        b.Markers.Add(new Marker(c.Id($"garage.e{e.Ring}.{e.Index}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                        continue;
                    }
                    // the cottage's gable-end walls keep one small window; a kostka every bay; modern the garden side wide
                    if (bwid < 1.5f || (_look == Family.Cottage && len < 5.5f && bi != bays / 2)) { Kit.Wall(wall.Mesh, body, f, u0, u1, y0, y1); continue; }
                    Window(c, wall.Mesh, trim.Mesh, f, body, u0, u1, y0, y1, sh, s, garden);
                }
            }
            // the kostka's band between the floors, and the flat roof's edge
            if (_band && levels >= 2)
                for (int s = 1; s < levels; s++) Kit.Box(trim.Mesh, "band", f, 0, len, datum + s * sh - 0.14f, datum + s * sh + 0.04f, 0, 0.05f);
            if (plan.RoofShape == RoofShape.Flat) Kit.Wall(wall.Mesh, "wall_alt", f, 0, len, top, top + 0.1f);
        }

        Balcony(c, street, streetOut);
        Roof(c, top);
    }

    private void Window(BuildingGenerator.Context c, Mesh wall, Mesh trim, Kit.Frame f, string body, float u0, float u1, float y0, float y1, float sh, int storey, bool garden)
    {
        float uc = (u0 + u1) * 0.5f, bw = u1 - u0;
        float hw, sill, head;
        switch (_look)
        {
            case Family.Cottage: hw = MathF.Min(0.45f, bw * 0.25f); sill = 0.85f; head = MathF.Min(sh - 0.45f, 2.0f); break;
            case Family.Kostka: hw = MathF.Min(0.7f, bw * 0.3f); sill = 0.9f; head = MathF.Min(sh - 0.35f, 2.3f); break;
            default:
                bool big = garden && storey == 0;
                hw = big ? MathF.Min(1.2f, bw * 0.42f) : MathF.Min(0.65f, bw * 0.28f);
                sill = big ? 0.05f : 0.9f; head = MathF.Min(sh - 0.35f, 2.4f);
                break;
        }
        float a = uc - hw, z = uc + hw, ys = y0 + sill, yh = y0 + head;
        Kit.WallWithOpening(wall, body, "reveal", "glass", f, u0, u1, y0, y1, a, z, ys, yh, 0.15f);
        // frame bars in front of the glass: a cross on old windows, a mullion on wide ones
        float d0 = -0.13f, d1 = -0.09f;
        bool cross = _look != Family.Modern;
        Kit.Box(trim, "frame", f, uc - 0.035f, uc + 0.035f, ys, yh, d0, d1);
        if (cross) { float yt = ys + (yh - ys) * (_look == Family.Cottage ? 0.62f : 0.7f); Kit.Box(trim, "frame", f, a, z, yt - 0.03f, yt + 0.03f, d0, d1); }
        // the sill outside
        if (sill > 0.2f) Kit.Box(trim, "sill", f, a - 0.06f, z + 0.06f, ys - 0.05f, ys, 0, 0.07f);
        // the cottage's painted surround, and sometimes open shutters
        if (_look == Family.Cottage && _trims)
        {
            const float t = 0.09f;
            Kit.Box(trim, "trim", f, a - t, a, ys - 0.02f, yh + 0.02f, 0, 0.03f);
            Kit.Box(trim, "trim", f, z, z + t, ys - 0.02f, yh + 0.02f, 0, 0.03f);
            Kit.Box(trim, "trim", f, a - t, z + t, yh, yh + 0.12f, 0, 0.035f);
            if (_shutters && a - t - hw > u0 && z + t + hw < u1)
            {
                Kit.Box(trim, "trim", f, a - t - hw, a - t, ys, yh, 0.02f, 0.05f);
                Kit.Box(trim, "trim", f, z + t, z + t + hw, ys, yh, 0.02f, 0.05f);
            }
        }
    }

    private void FrontDoor(BuildingGenerator.Context c, Mesh wall, Mesh trim, Kit.Frame f, WallEdge e, float u0, float u1, float du, float y0, float y1)
    {
        var b = c.Building;
        float hw = 0.5f, dh = 2.1f;
        du = Math.Clamp(du, u0 + hw + 0.1f, u1 - hw - 0.1f);
        Kit.WallWithOpening(wall, _timber ? "timber" : "wall", "reveal", "door", f, u0, u1, y0, y1, du - hw, du + hw, y0, y0 + dh, 0.15f);
        b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}"), "entrance", f.P(du, y0, 0.05f), f.Out));
        // a canopy over it (not on every cottage)
        if (_look != Family.Cottage || c.Detail.Chance(0.4))
            Kit.Box(trim, "canopy", f, du - 0.85f, du + 0.85f, y0 + dh + 0.2f, y0 + dh + 0.3f, 0, _look == Family.Modern ? 1.0f : 0.8f);
        // steps from the ground up to the door: the kostka's raised entrance climbs its plinth
        float rise = y0 - c.Plan.GroundLow;
        int n = Math.Clamp((int)MathF.Round(rise / 0.17f), 0, 6);
        for (int k = 0; k < n; k++)
        {
            float yt = y0 - k * (rise / MathF.Max(n, 1));
            Kit.Box(trim, "steps", f, du - 0.75f, du + 0.75f, c.Plan.GroundLow - 0.2f, yt, 0, MathF.Min(1.0f, 0.3f * (k + 1)));
        }
    }

    /// <summary>The kostka's balcony: a slab and railing in the middle of the street wall, upstairs.</summary>
    private void Balcony(BuildingGenerator.Context c, WallEdge? street, Vector2 streetOut)
    {
        if (!_balcony) return;
        var e = street ?? c.Plan.Edges.Where(x => x.Exposure is not (Exposure.Party or Exposure.Sliver)).OrderByDescending(x => x.Length).FirstOrDefault();
        if (e == null || e.Length < 6f) return;
        var f = Kit.Frame.Of(e);
        var m = c.Building.Add(c.Id("balcony"), "balconies", ModuleTier.Secondary).Mesh;
        float y = c.Plan.Datum + c.Plan.StoreyHeight, uc = e.Length * 0.5f, hw = MathF.Min(1.6f, e.Length * 0.25f);
        Kit.Box(m, "concrete", f, uc - hw, uc + hw, y - 0.15f, y, 0, 0.95f);
        Kit.Box(m, "railing", f, uc - hw, uc + hw, y + 0.9f, y + 0.95f, 0.88f, 0.95f);
        for (float u = uc - hw; u <= uc + hw + 0.01f; u += hw / 4f) Kit.Box(m, "railing", f, u - 0.02f, u + 0.02f, y, y + 0.9f, 0.9f, 0.94f);
        Kit.Box(m, "railing", f, uc - hw, uc - hw + 0.04f, y, y + 0.95f, 0.05f, 0.95f);
        Kit.Box(m, "railing", f, uc + hw - 0.04f, uc + hw, y, y + 0.95f, 0.05f, 0.95f);
    }

    /// <summary>A window in each outer gable that is tall enough for a room in the roof (D26: a house's attic).</summary>
    private void AtticWindows(BuildingGenerator.Context c, Mesh m, float eave)
    {
        var plan = c.Plan;
        var wings = plan.RoofWings.Count > 0 ? plan.RoofWings : [new Roofs.Wing(plan.RoofRect, plan.RoofRise, true, true)];
        foreach (var w in wings)
        {
            if (w.Rise < 2.4f) continue;
            var r = w.Rect;
            float hw = MathF.Min(0.45f, r.HalfW * 0.25f), y0 = eave + 0.5f, y1 = eave + MathF.Min(w.Rise - 0.7f, 1.7f);
            foreach (var (outer, s) in new[] { (w.OuterStart, -1f), (w.OuterEnd, 1f) })
            {
                if (!outer) continue;
                var at = r.At(s * r.HalfL, 0);
                var u = new Vector3(r.V.X, 0, r.V.Y) * s;   // along the gable, seen from outside
                var f = new Kit.Frame(new Vector3(at.X, 0, at.Y), u, new Vector3(r.U.X, 0, r.U.Y) * s);
                Kit.Box(m, "glass", f, -hw, hw, y0, y1, 0, 0.02f);
                Kit.Box(m, "frame", f, -hw - 0.06f, hw + 0.06f, y0 - 0.06f, y0, 0, 0.05f);
                Kit.Box(m, "frame", f, -hw - 0.06f, hw + 0.06f, y1, y1 + 0.06f, 0, 0.05f);
                Kit.Box(m, "frame", f, -hw - 0.06f, -hw, y0, y1, 0, 0.05f);
                Kit.Box(m, "frame", f, hw, hw + 0.06f, y0, y1, 0, 0.05f);
                Kit.Box(m, "frame", f, -0.03f, 0.03f, y0, y1, 0, 0.04f);
            }
        }
    }

    private void Roof(BuildingGenerator.Context c, float top)
    {
        var plan = c.Plan;
        var b = c.Building;
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        bool pitched = plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal;
        float ridge;
        Vector2 at;
        if (pitched)
        {
            float o = _look switch { Family.Cottage => 0.5f, Family.Kostka => 0.55f, _ => 0.4f } * plan.Eaves;
            Roofs.Pitched(roof.Mesh, "roof", "soffit", _look == Family.Cottage ? "boards" : _timber ? "timber" : "wall", plan, top, o);
            Kit.Slab(roof.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
            if (plan.RoofShape == RoofShape.Gabled) AtticWindows(c, roof.Mesh, top);
            var main = plan.RoofWings.Count > 0 ? plan.RoofWings.MaxBy(w => w.Rise).Rect : plan.RoofRect;
            ridge = top + (plan.RoofWings.Count > 0 ? plan.RoofWings.Max(w => w.Rise) : plan.RoofRise);
            // the chimney: on the ridge, off centre (in the middle on a cottage)
            float off = _look == Family.Cottage ? 0f : main.HalfL * c.Detail.Range(-0.45f, 0.45f);
            at = main.At(MathF.Max(-main.HalfL + 0.6f, MathF.Min(main.HalfL - 0.6f, off)), 0);
        }
        else
        {
            Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
            Kit.Parapet(roof.Mesh, "wall_alt", c.Site.Outer, top + 0.1f, top + 0.35f, 0.2f);
            ridge = top + 0.1f;
            var r = Roofs.MinRect(c.Site.Outer);
            at = r.At(r.HalfL * 0.35f, 0);
        }
        if (!Geometry.Polygon.Contains(c.Site.Outer, at)) return;
        var ch = b.Add(c.Id("chimney"), "chimney", ModuleTier.Secondary);
        float hwc = _look == Family.Cottage ? 0.35f : 0.28f;
        Kit.BoxXZ(ch.Mesh, "chimney", new Vector3(at.X - hwc, top - 0.5f, at.Y - hwc), new Vector3(at.X + hwc, ridge + 0.8f, at.Y + hwc));
        Kit.BoxXZ(ch.Mesh, "chimney", new Vector3(at.X - hwc - 0.05f, ridge + 0.72f, at.Y - hwc - 0.05f), new Vector3(at.X + hwc + 0.05f, ridge + 0.84f, at.Y + hwc + 0.05f));
    }
}
