using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Garages, garage rows, allotment huts and shacks (D28, Noor's cookbook R011–R014; Tom: "garages, small shacks"):
/// * <b>garage</b> (R013): a single box, a mono-pitch, low gable or flat roof, an up-and-over door toward the road;
///   PRL sheet metal ("blaszak", ribbed, green, grey, blue or rusty), brick or plaster;
/// * <b>garage row</b> (R014, garaże): a run of bays along the side facing the access road, each with its own door in
///   its own colour, brick piers between them, a tar-paper roof falling to the back;
/// * <b>allotment hut</b> (R011, altanka działkowa): small and painted (planks, boards or plaster in green, blue,
///   brown, cream), a gable or mono-pitch roof, a door under a porch on half, a window with a frame;
/// * <b>shack</b> (R012): planks or sheet patched with whatever was at hand, a mono-pitch roof of rusty sheet, a door,
///   maybe a window.
/// </summary>
public sealed class Garages : IStyle
{
    public const string Name = "garages";
    public string Id => Name;

    public enum Kind { Garage, GarageRow, Allotment, Shack }
    public Kind Type { get; }
    private string _skin = "wall";
    private bool _porch;

    public Garages(BuildingGenerator.Context c) => Type = KindOf(c);

    public static bool Fits(BuildingGenerator.Context c) => !c.Request.Raised && c.Site.Holes.Count == 0
        && c.Request.Type is "garage" or "garages" or "shed" or "hut" or "allotment_house";

    public static Kind KindOf(BuildingGenerator.Context c)
    {
        var r = Roofs.MinRect(c.Site.Outer);
        float ratio = r.HalfL / MathF.Max(r.HalfW, 0.1f), a = c.Site.Area;
        switch (c.Request.Type)
        {
            case "garages": return a > 30f ? Kind.GarageRow : Kind.Garage;
            case "garage": return a > 45f && ratio >= 2f ? Kind.GarageRow : Kind.Garage;
            case "allotment_house": return Kind.Allotment;
            default: return Rng.Stream(c.Request.Seed, "garages.kind").Weighted<Kind>([(Kind.Shack, 6), (Kind.Allotment, 4)]);
        }
    }

    public float Plinth => Type == Kind.Allotment ? 0.25f : 0.1f;
    public float RoofExtra => 0.25f;
    public int InferLevels(BuildingGenerator.Context c) => 1;
    public float StoreyHeight(BuildingGenerator.Context c) => Type is Kind.Garage or Kind.GarageRow ? 2.5f : 2.3f;

    public string InferRoof(BuildingGenerator.Context c) => Type switch
    {
        Kind.GarageRow => "skillion",
        Kind.Garage => c.PlanRng.Weighted<string>([("skillion", 6), ("gabled", 2), ("flat", 2)]),
        Kind.Allotment => c.PlanRng.Weighted<string>([("gabled", 6), ("skillion", 4)]),
        _ => "skillion",
    };

    public float RoofPitch(BuildingGenerator.Context c) => Type switch
    {
        Kind.Allotment => c.PlanRng.Range(28f, 40f),
        Kind.Garage => c.PlanRng.Range(5f, 20f),
        Kind.GarageRow => c.PlanRng.Range(3f, 6f),
        _ => c.PlanRng.Range(8f, 15f),
    };

    private static readonly string[] DoorColours = ["4d6e4a", "3e5f86", "7a3a2c", "8f9294", "5a4a3a", "a8a29a", "6e7a3a", "2f3438"];

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        string wall, roof, name;
        switch (Type)
        {
            case Kind.Garage or Kind.GarageRow:
                var s = L.Weighted<string>([("blaszak", Type == Kind.Garage ? 4 : 3), ("brick", 3), ("plaster", 3)]);
                name = s;
                (_skin, wall) = s switch
                {
                    "blaszak" => ("cladding", L.Weighted<string>([("4d6e4a", 3), ("8f9294", 3), ("3e5f86", 2), ("7a4a32", 2), ("a8a29a", 1)])),
                    "brick" => ("brick", L.Weighted<string>([("7d4232", 3), ("8a5038", 2)])),
                    _ => ("wall", L.Weighted<string>([("b4b0a6", 3), ("cfc8b6", 2), ("9e9b93", 2)])),
                };
                roof = Type == Kind.GarageRow ? L.Weighted<string>([("3a3a38", 4), ("4a4844", 2)]) : L.Weighted<string>([("3a3a38", 3), ("7b7b74", 2), ("7a4a32", 2)]);
                break;
            case Kind.Allotment:
                var a = L.Weighted<string>([("planks", 5), ("boards", 2), ("plaster", 3)]);
                _skin = a == "planks" ? "timber" : a == "boards" ? "boards" : "wall";
                wall = a == "plaster" ? L.Weighted<string>([("e8e2cc", 2), ("dcd3b0", 1), ("cfd8c0", 1)])
                    : L.Weighted<string>([("4d6e4a", 3), ("4a6a8a", 2), ("6a4a32", 3), ("8a6a3a", 1), ("57534c", 1)]);
                name = $"{a}";
                roof = L.Weighted<string>([("7b7b74", 3), ("3a3a38", 2), ("6e3326", 2), ("4d5e48", 1)]);
                _porch = L.Chance(0.5);
                break;
            default:
                _skin = L.Chance(0.6) ? "timber" : "cladding";
                wall = _skin == "timber" ? L.Weighted<string>([("4c3f33", 3), ("57534c", 2), ("5e4b39", 2)]) : L.Weighted<string>([("7a4a32", 3), ("8f9294", 2), ("6e6a5e", 1)]);
                name = _skin == "timber" ? "patched planks" : "patched sheet";
                roof = L.Weighted<string>([("7a4a32", 4), ("6e3326", 2), ("7b7b74", 2)]);
                break;
        }
        c.Plan = c.Plan with { Palette = $"{Type.ToString().ToLowerInvariant()}, {name}" };
        c.Building.Plan = c.Plan;
        m[_skin] = Kit.Mat(_skin, wall, 0.9f);
        m["wall"] = Kit.Mat("wall", wall, 0.9f);
        m["plinth"] = Kit.Mat("plinth", "6a655e", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "6e6a62", 0.9f);
        m["glass"] = Kit.Mat("glass", "2f3a40", 0.1f, 0.1f);
        m["frame"] = Kit.Mat("frame", L.Weighted<string>([("e6e2d8", 2), ("4a3a2c", 1), ("3d5b3a", 1)]), 0.7f);
        m["door"] = Kit.Mat("door", L.Pick(DoorColours), 0.7f);
        for (int i = 0; i < DoorColours.Length; i++) m[$"door{i}"] = Kit.Mat($"door{i}", DoorColours[i], 0.6f, 0.3f);
        m["pier"] = Kit.Mat("pier", "7d4232", 0.95f);
        m["fascia"] = Kit.Mat("fascia", "5a5a56", 0.9f);
        m["roof"] = Kit.Mat("roof", roof, 0.9f);
        m["soffit"] = Kit.Mat("soffit", "4f453a", 0.95f);
        m["post"] = Kit.Mat("post", "5a4a38", 0.9f);
        // a shack's patches: sheet and planks of other colours
        m["patch_sheet"] = Kit.Mat("patch_sheet", L.Weighted<string>([("7a4a32", 2), ("8f9294", 1), ("4d6e4a", 1)]), 0.8f);
        m["patch_planks"] = Kit.Mat("patch_planks", L.Weighted<string>([("6a5640", 2), ("3f362c", 1)]), 0.95f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        var rect = plan.RoofRect;
        bool slope = plan.RoofShape == RoofShape.Skillion;
        // the door side: facing the road (street exposure), else the longest open wall
        var open = plan.Edges.Select((e, i) => (e, i)).Where(x => x.e.Ring == 0 && x.e.Exposure is not (Exposure.Party or Exposure.Sliver) && x.e.Length >= 1.8f).ToList();
        var front = Type == Kind.GarageRow
            ? open.OrderByDescending(x => x.e.Length * (x.e.Exposure == Exposure.Street ? 1.5f : 1f)).FirstOrDefault()
            : open.OrderByDescending(x => (x.e.Exposure == Exposure.Street ? 100f : 0f) + x.e.Length).FirstOrDefault();
        // a mono-pitch roof is high over the front and falls to the back (a garage row's water runs off behind)
        Vector2 frontOut = front.e?.Out ?? rect.V;
        float span = MathF.Max(0.5f, c.Site.Outer.Max(p => Vector2.Dot(p, -frontOut)) - c.Site.Outer.Min(p => Vector2.Dot(p, -frontOut)));
        float back0 = c.Site.Outer.Min(p => Vector2.Dot(p, -frontOut));
        float RoofAt(Vector2 p) => slope ? top + plan.RoofRise * (1f - Math.Clamp((Vector2.Dot(p, -frontOut) - back0) / span, 0f, 1f)) : top;

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.e{e.Ring}.{e.Index}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            bool isFront = front.e != null && ei == front.i;
            var openings = new List<(float U0, float U1, float Y0, float Y1, string Infill, bool Door)>();
            if (isFront)
            {
                switch (Type)
                {
                    case Kind.GarageRow:
                        int n = Math.Max(1, (int)MathF.Round(len / 3.1f));
                        float bw = len / n;
                        var rng = Rng.Stream(c.Request.Seed, "garages.doors");
                        for (int k = 0; k < n; k++)
                        {
                            float uc = (k + 0.5f) * bw, hw = MathF.Min(1.15f, bw * 0.5f - 0.2f);
                            openings.Add((uc - hw, uc + hw, datum, datum + MathF.Min(2.1f, top - datum - 0.2f), $"door{rng.Int(0, DoorColours.Length - 1)}", true));
                            if (k > 0) Kit.Box(trim.Mesh, "pier", f, k * bw - 0.2f, k * bw + 0.2f, ground - 0.2f, top, 0, 0.12f);
                        }
                        Kit.Box(trim.Mesh, "fascia", f, 0, len, top - 0.25f, top + 0.05f, 0, 0.1f);
                        break;
                    case Kind.Garage:
                        float gw = MathF.Min(1.2f, len * 0.5f - 0.3f);
                        if (gw > 0.6f) openings.Add((len * 0.5f - gw, len * 0.5f + gw, datum, datum + MathF.Min(2.1f, top - datum - 0.15f), "door", true));
                        break;
                    default:
                        float du = len > 3.5f ? len * 0.3f : len * 0.5f;
                        openings.Add((du - 0.4f, du + 0.4f, datum, datum + MathF.Min(1.95f, top - datum - 0.1f), "door", true));
                        if (len > 2.6f && (Type == Kind.Allotment || c.Detail.Chance(0.5)))
                        {
                            float wu = len > 3.5f ? len * 0.72f : -1f;
                            if (wu > 0) openings.Add((wu - 0.4f, wu + 0.4f, datum + 0.95f, datum + 1.6f, "glass", false));
                        }
                        break;
                }
            }
            else if (Type == Kind.Allotment && len >= 2.4f && e.Exposure is not (Exposure.Party or Exposure.Sliver) && c.Detail.Chance(0.5))
                openings.Add((len * 0.5f - 0.35f, len * 0.5f + 0.35f, datum + 0.95f, datum + 1.55f, "glass", false));

            float at0 = 0;
            foreach (var o in openings.OrderBy(o => o.U0))
            {
                Kit.Wall(wall.Mesh, _skin, f, at0, o.U0, datum, top);
                Kit.WallWithOpening(wall.Mesh, _skin, "reveal", o.Infill, f, o.U0, o.U1, datum, top, o.U0, o.U1, o.Y0, o.Y1, o.Door ? 0.08f : 0.08f);
                if (o.Door) b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{(int)(o.U0 * 10)}"), "entrance", f.P((o.U0 + o.U1) * 0.5f, o.Y0, 0.05f), f.Out));
                else
                {
                    float mid = (o.U0 + o.U1) * 0.5f;
                    Kit.Box(trim.Mesh, "frame", f, mid - 0.025f, mid + 0.025f, o.Y0, o.Y1, -0.07f, -0.04f);
                    Kit.Box(trim.Mesh, "frame", f, o.U0 - 0.05f, o.U1 + 0.05f, o.Y0 - 0.05f, o.Y0, 0, 0.06f);
                }
                at0 = o.U1;
            }
            Kit.Wall(wall.Mesh, _skin, f, at0, len, datum, top);
            if (slope) Kit.WallTop(wall.Mesh, _skin, f, len, top, RoofAt(e.A), RoofAt(e.B));
            if (plan.RoofShape == RoofShape.Flat) Kit.Wall(wall.Mesh, _skin, f, 0, len, top, top + 0.12f);

            // a shack's patches: a sheet or some planks nailed over the wall
            if (Type == Kind.Shack && len >= 1.5f)
            {
                var rng = Rng.Stream(c.Request.Seed, $"garages.patch.{ei}");
                int np = rng.Int(0, 2);
                for (int k = 0; k < np; k++)
                {
                    float pw = rng.Range(0.6f, MathF.Min(1.6f, len * 0.5f)), pu = rng.Range(0.1f, MathF.Max(0.11f, len - pw - 0.1f));
                    float py = datum + rng.Range(0.1f, 0.9f), ph = rng.Range(0.6f, MathF.Max(0.61f, top - py - 0.1f));
                    if (openings.Any(o => pu + pw > o.U0 && pu < o.U1)) continue;
                    Kit.Box(trim.Mesh, rng.Chance(0.5) ? "patch_sheet" : "patch_planks", f, pu, pu + pw, py, MathF.Min(top, py + ph), 0, 0.025f);
                }
            }
        }

        // the allotment hut's porch: a little roof on two posts over the door
        if (_porch && front.e != null && front.e.Length >= 2.2f)
        {
            var f = Kit.Frame.Of(front.e);
            var pm = b.Add(c.Id("porch"), "porch", ModuleTier.Secondary).Mesh;
            float u0 = 0.1f, u1 = MathF.Min(front.e.Length - 0.1f, 2.6f), y = top - 0.1f;
            Kit.Box(pm, "roof", f, u0 - 0.1f, u1 + 0.1f, y, y + 0.08f, 0, 1.0f);
            Kit.Box(pm, "post", f, u0, u0 + 0.1f, datum - 0.2f, y, 0.85f, 0.95f);
            Kit.Box(pm, "post", f, u1 - 0.1f, u1, datum - 0.2f, y, 0.85f, 0.95f);
            Kit.Box(pm, "post", f, u0 - 0.05f, u1 + 0.05f, datum - 0.05f, datum, 0, 1.0f);
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        switch (plan.RoofShape)
        {
            case RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal:
                Roofs.Pitched(roof.Mesh, "roof", "soffit", _skin, plan, top, 0.25f * plan.Eaves);
                Kit.Slab(roof.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
                break;
            case RoofShape.Skillion:
                Roofs.Sloped(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, p => RoofAt(p) + 0.05f);
                break;
            default:
                Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.12f, top + 0.1f, sides: false);
                break;
        }
    }
}
