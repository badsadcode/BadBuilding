using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Village and town public buildings (D29, Noor's cookbook R044, R049; Tom: "Fire stations, schools"):
/// * <b>fire station</b> (remiza OSP, R049: 1,099 in Mazowsze, nearly every village): one or two tall storeys, one to
///   three vehicle doors (red, sometimes white) on the side toward the road with a red sign band over them, a staff
///   door beside them, windows upstairs; on the bigger ones a square hose-drying tower standing inside the footprint;
/// * <b>school</b> (R044, 3,164 + 112): two or three tall storeys of wide classroom windows in a steady rhythm, a raised
///   ground floor, the main entrance up steps under a canopy in the middle of the street front; the PRL
///   "tysiąclatka" look (grey or white plaster, a brick plinth, flat roof) or a renovated one (insulated, pale walls
///   with a strong accent colour on the bands and the entrance);
/// * <b>kindergarten</b> (1,385): one or two storeys, smaller windows, cheerful walls and frames, a canopy over a
///   wide door.
/// No gym hall is invented on a footprint that doesn't show one (the cookbook's warning).
/// </summary>
public sealed class Civic : IStyle
{
    public const string Name = "civic";
    public string Id => Name;

    public enum Kind { FireStation, School, Kindergarten }
    public Kind Type { get; }
    private bool _renovated, _tower;

    public Civic(BuildingGenerator.Context c) => Type = c.Request.Type switch
    {
        "fire_station" => Kind.FireStation,
        "kindergarten" => Kind.Kindergarten,
        _ => Kind.School,
    };

    public static bool Fits(BuildingGenerator.Context c) => !c.Request.Raised && !c.Site.Small && c.Site.Depth >= 5f
        && c.Request.Type is "fire_station" or "school" or "kindergarten";

    public float Plinth => Type switch { Kind.School => 0.6f, Kind.Kindergarten => 0.3f, _ => 0.05f };
    public float RoofExtra => 0.4f;

    public int InferLevels(BuildingGenerator.Context c) => Type switch
    {
        Kind.FireStation => c.Site.Area > 150f ? 2 : 1,
        Kind.Kindergarten => c.Site.Area > 650f ? 2 : c.PlanRng.Weighted<int>([(1, 2), (2, 1)]),
        _ => c.Site.Area < 500f ? 2 : c.PlanRng.Weighted<int>([(2, 2), (3, 3)]),
    };

    public float StoreyHeight(BuildingGenerator.Context c) => Type switch
    {
        Kind.FireStation => 3.9f,   // the ground floor holds the fire engines
        Kind.Kindergarten => 3.3f,
        _ => 3.6f,
    };

    public string InferRoof(BuildingGenerator.Context c) => Type switch
    {
        Kind.FireStation => c.PlanRng.Weighted<string>([("gabled", 5), ("flat", 4), ("hipped", 1)]),
        Kind.Kindergarten => c.PlanRng.Weighted<string>([("hipped", 3), ("gabled", 3), ("flat", 4)]),
        _ => c.PlanRng.Weighted<string>([("flat", 6), ("hipped", 3), ("gabled", 1)]),
    };

    public float RoofPitch(BuildingGenerator.Context c) => Type switch
    {
        Kind.School => c.PlanRng.Range(16f, 24f),
        Kind.FireStation => c.PlanRng.Range(20f, 30f),
        _ => c.PlanRng.Range(22f, 32f),
    };

    private static readonly string[] Accents = ["d9822b", "5f9e4a", "3f73b5", "e3b23c", "b5423a", "8a5fa8"];

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, accent, roof, frame, name;
        switch (Type)
        {
            case Kind.FireStation:
                _renovated = L.Chance(0.5);
                wall = _renovated ? L.Weighted<string>([("efe9d8", 3), ("e6dcb4", 2), ("dfe2dc", 1)]) : L.Weighted<string>([("d8d2c2", 2), ("b9b3a4", 2), ("9e9b93", 1)]);
                accent = "b02a22";
                frame = _renovated ? "eceae4" : "5a4434";
                name = _renovated ? "renovated" : "old plaster";
                _tower = c.Site.Area > 120f && Rng.Stream(c.Request.Seed, "civic.tower").Chance(0.6);
                break;
            case Kind.Kindergarten:
                _renovated = true;
                wall = L.Weighted<string>([("f0e6c4", 3), ("e9eef0", 2), ("f2dccb", 2), ("e1ecd6", 2)]);
                accent = L.Pick(Accents);
                frame = L.Weighted<string>([("eceae4", 2), (accent, 2)]);
                name = "cheerful";
                break;
            default:
                _renovated = c.Request.StartYear is >= 1990 || L.Chance(0.6);
                wall = _renovated ? L.Weighted<string>([("efe9dc", 3), ("e9e2c8", 2), ("e3e6e2", 2)]) : L.Weighted<string>([("c8c4ba", 3), ("dad6cc", 2), ("a8a59d", 1)]);
                accent = _renovated ? L.Pick(Accents) : "8a867e";
                frame = _renovated ? "eceae4" : L.Weighted<string>([("8a6a4a", 1), ("d8d4ca", 2)]);
                name = _renovated ? "renovated" : "tysiąclatka";
                break;
        }
        roof = L.Weighted<string>([("5c5d5f", 3), ("3d3f40", 2), ("8a3b2c", 2), ("6b3a2a", 1)]);
        c.Plan = c.Plan with { Palette = $"{Type switch { Kind.FireStation => "fire station", Kind.Kindergarten => "kindergarten", _ => "school" }}, {name}" };
        c.Building.Plan = c.Plan;
        m["wall"] = Kit.Mat("wall", wall, 0.9f);
        m["wall_alt"] = Kit.Mat("wall_alt", _renovated ? accent : wall, 0.9f);
        m["accent"] = Kit.Mat("accent", accent, 0.85f);
        m["plinth"] = Kit.Mat("plinth", Type == Kind.School && !_renovated ? "7d4232" : "77736b", 0.95f);
        m["brick"] = Kit.Mat("brick", "7d4232", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "8a857c", 0.9f);
        m["glass"] = Kit.Mat("glass", "2a3640", 0.08f, 0.1f);
        m["frame"] = Kit.Mat("frame", frame, 0.6f);
        m["sill"] = Kit.Mat("sill", "8c8a84", 0.7f);
        m["door"] = Kit.Mat("door", Type == Kind.FireStation ? "5a4434" : L.Weighted<string>([("3a3d40", 2), ("6e5840", 1), (accent, 1)]), 0.7f);
        m["roller"] = Kit.Mat("roller", L.Weighted<string>([("b02a22", 5), ("e6e3dc", 1)]), 0.6f);
        m["sign"] = Kit.Mat("sign", "b02a22", 0.6f);
        m["canopy"] = Kit.Mat("canopy", "8a867e", 0.85f);
        m["steps"] = Kit.Mat("steps", "8c877e", 0.95f);
        m["railing"] = Kit.Mat("railing", "33363a", 0.5f, 0.6f);
        m["roof"] = Kit.Mat("roof", roof, 0.85f);
        m["soffit"] = Kit.Mat("soffit", "6a5a48", 0.9f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        float bay = Type switch { Kind.School => 3.6f, Kind.Kindergarten => 3.0f, _ => 3.2f };

        // the street front: where the vehicle doors (fire station) or the main entrance go
        var open = plan.Edges.Select((e, i) => (e, i)).Where(x => x.e.Ring == 0 && x.e.Exposure is not (Exposure.Party or Exposure.Sliver) && x.e.Length >= 4f).ToList();
        var front = open.OrderByDescending(x => x.e.Length * (x.e.Exposure == Exposure.Street ? 2f : 1f)).FirstOrDefault();
        var doors = Type == Kind.FireStation ? [] : Entrances.Place(c, _ => 1, bay);
        // the fire station's ground floor on its front: vehicle doors, then the staff door
        var ground0 = new List<(float U0, float U1, float Y1, string Infill)>();
        if (Type == Kind.FireStation && front.e != null)
        {
            float len = front.e.Length;
            int n = Math.Clamp((int)((len - 2.2f) / 4.6f), 1, 3);
            float dh = MathF.Min(3.5f, sh - 0.35f);
            for (int k = 0; k < n; k++) { float u0 = 1.0f + k * 4.6f; ground0.Add((u0, u0 + 3.4f, datum + dh, "roller")); }
            float su = 1.0f + n * 4.6f + 0.3f;
            if (su + 1.0f < len - 0.4f) ground0.Add((su, su + 1.0f, datum + 2.2f, "door"));
        }

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            bool blind = e.Exposure is Exposure.Party or Exposure.Sliver || len < 1.8f;
            if (blind) { Kit.Wall(wall.Mesh, "wall", f, 0, len, datum, top); continue; }
            bool isFront = front.e != null && ei == front.i;

            int startStorey = 0;
            if (isFront && ground0.Count > 0)
            {
                // the fire station's ground floor laid out by its doors
                float at0 = 0, y0 = datum, y1 = datum + sh;
                foreach (var o in ground0)
                {
                    Kit.Wall(wall.Mesh, "wall", f, at0, o.U0, y0, y1);
                    Kit.WallWithOpening(wall.Mesh, "wall", "reveal", o.Infill, f, o.U0, o.U1, y0, y1, o.U0, o.U1, y0, o.Y1, 0.15f);
                    b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{(int)(o.U0 * 10)}"), "entrance", f.P((o.U0 + o.U1) * 0.5f, y0, 0.05f), f.Out));
                    at0 = o.U1;
                }
                Kit.Wall(wall.Mesh, "wall", f, at0, len, y0, y1);
                // the red sign band over the vehicle doors
                var veh = ground0.Where(o => o.Infill == "roller").ToList();
                float signY = veh.Max(o => o.Y1) + 0.12f;
                if (signY + 0.45f < y1 + 0.3f)
                    Kit.Box(trim.Mesh, "sign", f, veh.Min(o => o.U0), veh.Max(o => o.U1), signY, MathF.Min(y1 - 0.05f, signY + 0.45f), 0, 0.06f);
                startStorey = 1;
            }

            int bays = Math.Max(1, (int)MathF.Round(len / bay));
            float bw = len / bays;
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                for (int s = startStorey; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    var door = doors.Where(d => d.Edge == ei && d.U >= u0 && d.U < u1).Select(d => (DoorSite?)d).FirstOrDefault();
                    if (s == 0 && door != null) { MainDoor(c, wall.Mesh, trim.Mesh, f, e, u0, u1, uc, y0, y1); continue; }
                    if (bw < 1.6f) { Kit.Wall(wall.Mesh, "wall", f, u0, u1, y0, y1); continue; }
                    Window(wall.Mesh, trim.Mesh, f, u0, u1, y0, y1, sh, uc, bw);
                }
            }
            // bands between the storeys (the accent on renovated schools and kindergartens)
            if (levels >= 2 && (Type != Kind.FireStation || _renovated))
                for (int s = 1; s < levels; s++) Kit.Box(trim.Mesh, _renovated ? "accent" : "wall_alt", f, 0, len, datum + s * sh - 0.2f, datum + s * sh + 0.05f, 0, 0.05f);
            if (plan.RoofShape == RoofShape.Flat)
            {
                Kit.Wall(wall.Mesh, "wall_alt", f, 0, len, top, top + 0.15f);
                Kit.Box(trim.Mesh, _renovated ? "accent" : "wall_alt", f, 0, len, top - 0.35f, top + 0.15f, 0, 0.08f);
            }
        }

        if (_tower) Tower(c, front.e, top);
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        if (plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal)
        {
            Roofs.Pitched(roof.Mesh, "roof", "soffit", "wall", plan, top, 0.5f * plan.Eaves);
            Kit.Slab(roof.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
        }
        else
        {
            Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.1f, sides: false);
            Kit.Parapet(roof.Mesh, "wall_alt", c.Site.Outer, top + 0.1f, top + 0.5f, 0.25f);
            foreach (var h in c.Site.Holes) Kit.Parapet(roof.Mesh, "wall_alt", h, top + 0.1f, top + 0.5f, 0.25f);
        }
    }

    private void Window(Mesh wall, Mesh trim, Kit.Frame f, float u0, float u1, float y0, float y1, float sh, float uc, float bw)
    {
        float hw, sill, head;
        switch (Type)
        {
            case Kind.School: hw = MathF.Min(1.3f, bw * 0.38f); sill = 0.9f; head = sh - 0.55f; break;
            case Kind.Kindergarten: hw = MathF.Min(0.85f, bw * 0.3f); sill = 0.7f; head = MathF.Min(sh - 0.5f, 2.5f); break;
            default: hw = MathF.Min(0.7f, bw * 0.3f); sill = 1.0f; head = MathF.Min(sh - 0.6f, 2.4f); break;
        }
        float a = uc - hw, z = uc + hw, ys = y0 + sill, yh = y0 + head;
        Kit.WallWithOpening(wall, "wall", "reveal", "glass", f, u0, u1, y0, y1, a, z, ys, yh, 0.18f);
        const float d0 = -0.16f, d1 = -0.12f;
        // mullions: two on the wide school windows, one elsewhere, and a transom
        int bars = Type == Kind.School ? 2 : 1;
        for (int k = 1; k <= bars; k++) { float u = a + (z - a) * k / (bars + 1); Kit.Box(trim, "frame", f, u - 0.035f, u + 0.035f, ys, yh, d0, d1); }
        float yt = yh - (yh - ys) * 0.28f;
        Kit.Box(trim, "frame", f, a, z, yt - 0.03f, yt + 0.03f, d0, d1);
        Kit.Box(trim, "sill", f, a - 0.06f, z + 0.06f, ys - 0.05f, ys, 0, 0.08f);
    }

    private void MainDoor(BuildingGenerator.Context c, Mesh wall, Mesh trim, Kit.Frame f, WallEdge e, float u0, float u1, float uc, float y0, float y1)
    {
        var b = c.Building;
        float hw = Type == Kind.School ? 0.95f : 0.8f, dh = 2.4f;
        hw = MathF.Min(hw, (u1 - u0) * 0.5f - 0.15f);
        Kit.WallWithOpening(wall, "wall", "reveal", "door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + dh, 0.2f);
        b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
        // a glass light over the door, the canopy, a surround in the accent colour
        if (y0 + dh + 0.8f < y1) Kit.WallWithOpening(trim, "accent", "reveal", "glass", f, uc - hw, uc + hw, y0 + dh, y0 + dh + 0.7f, uc - hw + 0.08f, uc + hw - 0.08f, y0 + dh + 0.1f, y0 + dh + 0.6f, 0.05f);
        Kit.Box(trim, "canopy", f, uc - hw - 0.9f, uc + hw + 0.9f, y0 + dh + 0.75f, y0 + dh + 0.95f, 0, 1.0f);
        if (_renovated) Kit.Box(trim, "accent", f, uc - hw - 0.25f, uc + hw + 0.25f, y0, y0 + dh + 0.75f, 0, 0.06f);
        // steps up the raised ground floor
        float rise = y0 - c.Plan.GroundLow;
        int n = Math.Clamp((int)MathF.Round(rise / 0.16f), 0, 5);
        for (int k = 0; k < n; k++)
        {
            float yt = y0 - k * (rise / MathF.Max(n, 1));
            Kit.Box(trim, "steps", f, uc - hw - 0.6f, uc + hw + 0.6f, c.Plan.GroundLow - 0.2f, yt, 0, MathF.Min(1.0f, 0.3f * (k + 1)));
        }
    }

    /// <summary>The fire station's hose-drying tower: a 3 m square shaft inside the footprint, at the end away from the
    /// vehicle doors, rising 4–6 m over the roof, with slit windows and a little hipped cap.</summary>
    private void Tower(BuildingGenerator.Context c, WallEdge? front, float top)
    {
        var plan = c.Plan;
        var r = plan.RoofRect;
        float rise = plan.RoofShape == RoofShape.Flat ? 0 : plan.RoofWings.Count > 0 ? plan.RoofWings.Max(w => w.Rise) : plan.RoofRise;
        // the end of the long axis farther from the front's first vehicle door
        Vector2 doorAt = front != null ? front.A + front.Dir * 2.7f : r.Centre;
        float s = Vector2.Dot(doorAt - r.Centre, r.U) > 0 ? -1f : 1f;
        const float hs = 1.5f;
        var at = r.At(s * (r.HalfL - hs - 0.3f), Math.Clamp(r.HalfW - hs - 0.3f, 0, r.HalfW) * (front != null && Vector2.Dot(front.Out, r.V) > 0 ? -1 : 1));
        foreach (var d in new[] { new Vector2(-hs, -hs), new Vector2(hs, -hs), new Vector2(hs, hs), new Vector2(-hs, hs) })
            if (!Polygon.Contains(c.Site.Outer, at + d)) return;
        var m = c.Building.Add(c.Id("tower"), "tower", ModuleTier.Primary).Mesh;
        float y1 = top + rise + c.Detail.Range(4f, 6f);
        Kit.BoxXZ(m, "wall", new Vector3(at.X - hs, plan.GroundLow - 0.3f, at.Y - hs), new Vector3(at.X + hs, y1, at.Y + hs));
        Kit.BoxXZ(m, "wall_alt", new Vector3(at.X - hs - 0.08f, y1 - 0.25f, at.Y - hs - 0.08f), new Vector3(at.X + hs + 0.08f, y1, at.Y + hs + 0.08f));
        // slit windows on each face, near the top
        foreach (var (dx, dz) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
        {
            var outv = new Vector3(dx, 0, dz);
            var u = new Vector3(-dz, 0, dx);
            var f = new Kit.Frame(new Vector3(at.X, 0, at.Y) + outv * hs, u, outv);
            Kit.Box(m, "glass", f, -0.2f, 0.2f, y1 - 2.2f, y1 - 0.8f, 0, 0.02f);
        }
        // a little pyramid cap
        var apex = new Vector3(at.X, y1 + 1.4f, at.Y);
        Vector3 C(float x, float z) => new(at.X + x, y1, at.Y + z);
        float o = hs + 0.25f;
        m.AddPolygon("roof", [C(-o, -o), C(o, -o), apex], Vector3.UnitY);
        m.AddPolygon("roof", [C(o, -o), C(o, o), apex], Vector3.UnitY);
        m.AddPolygon("roof", [C(o, o), C(-o, o), apex], Vector3.UnitY);
        m.AddPolygon("roof", [C(-o, o), C(-o, -o), apex], Vector3.UnitY);
    }
}
