using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Farm buildings and outbuildings (D27, Noor's cookbook R072–R077): half a million of them in Mazowsze
/// (outbuilding 288,000, farm_auxiliary 262,000). Five kinds, from the type and the size:
/// * <b>outbuilding</b> (budynek gospodarczy, R077): small, one storey, a mono-pitch or low gable roof, one door
///   (double and wooden on the bigger ones), a window or two;
/// * <b>barn</b> (stodoła, R072): long and tall-walled, a 30–40° gable, big double doors (wrota) on a long side,
///   a hay-loft door up in a gable, hardly a window;
/// * <b>livestock</b> (obora, chlewnia, stajnia, R074): long and low, a row of small windows high on the long walls,
///   one or two doors, a ventilation stack on the ridge;
/// * <b>hall</b> (R073): a big modern farm hall, ribbed metal over a concrete plinth, a shallow gable, roller doors;
/// * <b>greenhouse</b> (R075): milky glass or polycarbonate walls and roof on a frame of ribs.
/// Materials are the real farm mix: red brick, grey block, whitewash, timber (horizontal planks, vertical boards on
/// barns); roofs of eternit, corrugated sheet (grey, green, red-brown) and rust.
/// </summary>
public sealed class Farm : IStyle
{
    public const string Name = "farm";
    public string Id => Name;

    public enum Kind { Outbuilding, Barn, Livestock, Hall, Greenhouse }
    public Kind Type { get; }
    private enum Skin { Brick, Block, Whitewash, Planks, Boards, Metal }
    private Skin _skin;

    public Farm(BuildingGenerator.Context c) => Type = KindOf(c);

    public static bool Fits(BuildingGenerator.Context c) => !c.Request.Raised && c.Site.Holes.Count == 0 && c.Request.Type is "outbuilding"
        or "farm_auxiliary" or "barn" or "cowshed" or "stable" or "sty" or "greenhouse" or "silo_house" or "slurry_tank_house";

    public static Kind KindOf(BuildingGenerator.Context c)
    {
        var t = c.Request.Type;
        float a = c.Site.Area;
        var rect = Roofs.MinRect(c.Site.Outer);
        float ratio = rect.HalfL / MathF.Max(rect.HalfW, 0.1f);
        var rng = Rng.Stream(c.Request.Seed, "farm.kind");
        if (t == "greenhouse") return Kind.Greenhouse;
        if (t is "cowshed" or "stable" or "sty") return a > 600f ? Kind.Hall : Kind.Livestock;
        if (t == "barn") return a > 600f ? Kind.Hall : a < 35f ? Kind.Outbuilding : Kind.Barn;
        if (a < 45f) return Kind.Outbuilding;
        if (a > 450f) return rng.Weighted<Kind>([(Kind.Hall, 6), (Kind.Barn, 3), (Kind.Livestock, 1)]);
        if (ratio >= 2f) return rng.Weighted<Kind>([(Kind.Livestock, 4), (Kind.Barn, 4), (Kind.Outbuilding, 2)]);
        return rng.Weighted<Kind>([(Kind.Barn, 4), (Kind.Outbuilding, 4), (Kind.Livestock, 2)]);
    }

    public float Plinth => Type == Kind.Hall ? 0.6f : 0.2f;
    public float RoofExtra => 0.3f;
    public int InferLevels(BuildingGenerator.Context c) => 1;

    public float StoreyHeight(BuildingGenerator.Context c) => Type switch
    {
        Kind.Barn => 3.6f + MathF.Min(1.2f, c.Site.Area / 300f),
        Kind.Hall => 5.0f,
        Kind.Livestock => 2.9f,
        Kind.Greenhouse => 2.4f,
        _ => 2.5f,
    };

    public string InferRoof(BuildingGenerator.Context c) => Type switch
    {
        Kind.Outbuilding => c.PlanRng.Weighted<string>([("skillion", 5), ("gabled", 4), ("flat", 1)]),
        _ => "gabled",
    };

    public float RoofPitch(BuildingGenerator.Context c) => Type switch
    {
        Kind.Barn => c.PlanRng.Range(30f, 40f),
        Kind.Livestock => c.PlanRng.Range(18f, 28f),
        Kind.Hall => c.PlanRng.Range(8f, 14f),
        Kind.Greenhouse => c.PlanRng.Range(25f, 32f),
        _ => c.PlanRng.Range(10f, 22f),
    };

    public void Materials(BuildingGenerator.Context c)
    {
        var L = c.Look;
        var m = c.Building.Materials;
        _skin = Type switch
        {
            Kind.Hall => Skin.Metal,
            Kind.Greenhouse => Skin.Whitewash,
            Kind.Barn => L.Weighted<Skin>([(Skin.Boards, 45), (Skin.Brick, 30), (Skin.Block, 15), (Skin.Whitewash, 10)]),
            Kind.Livestock => L.Weighted<Skin>([(Skin.Brick, 45), (Skin.Whitewash, 30), (Skin.Block, 25)]),
            _ => L.Weighted<Skin>([(Skin.Brick, 35), (Skin.Block, 25), (Skin.Whitewash, 15), (Skin.Planks, 25)]),
        };
        string wall = _skin switch
        {
            Skin.Brick => L.Weighted<string>([("7d4232", 3), ("8a5038", 2), ("6b3a2c", 1)]),
            Skin.Block => L.Weighted<string>([("a4a39c", 3), ("8f8d86", 2), ("b5b0a3", 1)]),
            Skin.Whitewash => L.Weighted<string>([("e2ded2", 3), ("d6d2c4", 2), ("cfc6b0", 1)]),
            Skin.Planks or Skin.Boards => L.Weighted<string>([("4c3f33", 3), ("5e4b39", 2), ("57534c", 2)]),
            _ => L.Weighted<string>([("b9bcbe", 3), ("7f8a80", 2), ("8a3b2c", 1), ("4d6050", 1)]),
        };
        string roof = Type == Kind.Hall
            ? L.Weighted<string>([("7b7e80", 3), ("3d3f40", 2), ("6e3326", 1)])
            : L.Weighted<string>([("7b7b74", 5), ("4d5e48", 2), ("6e3326", 2), ("3d3f40", 2), ("7a4a32", 2)]);
        string skinRole = SkinRole;
        c.Plan = c.Plan with { Palette = $"{Type.ToString().ToLowerInvariant()}, {_skin.ToString().ToLowerInvariant()}" };
        c.Building.Plan = c.Plan;
        m[skinRole] = Kit.Mat(skinRole, wall, 0.95f);
        m["wall"] = Kit.Mat("wall", wall, 0.95f);
        m["plinth"] = Kit.Mat("plinth", Type == Kind.Hall ? "8f8b84" : "6a655e", 0.95f);
        m["reveal"] = Kit.Mat("reveal", "77726a", 0.9f);
        m["glass"] = Kit.Mat("glass", "2f3a40", 0.1f, 0.1f);
        m["frame"] = Kit.Mat("frame", L.Weighted<string>([("d8d4ca", 2), ("4a3a2c", 2), ("3d5b3a", 1)]), 0.7f);
        m["door"] = Kit.Mat("door", L.Weighted<string>([("4a3a2c", 3), ("5a4a38", 2), ("3d5b3a", 1), ("6e3a2a", 1)]), 0.85f);
        m["boards"] = Kit.Mat("boards", L.Weighted<string>([("4a3a2c", 3), ("5e4b39", 2), ("3d5b3a", 1)]), 0.95f);
        m["roller"] = Kit.Mat("roller", L.Weighted<string>([("b4b6b6", 2), ("7f8a80", 1)]), 0.6f);
        m["roof"] = Kit.Mat("roof", roof, 0.9f);
        m["soffit"] = Kit.Mat("soffit", "4f453a", 0.95f);
        m["vent"] = Kit.Mat("vent", "6a6a66", 0.8f);
        // greenhouses: milky glass or polycarbonate on white or galvanised ribs
        m["greenhouse"] = Kit.Mat("greenhouse", L.Weighted<string>([("dfe6e3", 3), ("cfdcd8", 2)]), 0.3f);
        m["rib"] = Kit.Mat("rib", L.Weighted<string>([("e6e6e2", 2), ("a8acae", 2)]), 0.5f);
    }

    private string SkinRole => _skin switch
    {
        Skin.Brick => "brick",
        Skin.Planks => "timber",
        Skin.Boards => "boards",
        Skin.Metal => "cladding",
        _ => "wall",
    };

    public void Build(BuildingGenerator.Context c)
    {
        if (Type == Kind.Greenhouse) { Greenhouse(c); return; }
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel, ground = plan.GroundLow;
        string skin = SkinRole;
        var rect = plan.RoofRect;
        bool slope = plan.RoofShape == RoofShape.Skillion;
        // a mono-pitch roof: high along one long side (toward the yard), falling to the other
        float RoofAt(Vector2 p)
        {
            if (!slope) return top;
            float v = Vector2.Dot(p - rect.Centre, rect.V);
            return top + plan.RoofRise * Math.Clamp((rect.HalfW - v) / (2f * rect.HalfW), 0f, 1f);
        }

        // doors: the barn's wrota and the hall's roller doors on a long side, one door elsewhere
        var open = plan.Edges.Select((e, i) => (e, i)).Where(x => x.e.Ring == 0 && x.e.Exposure is not (Exposure.Party or Exposure.Sliver) && x.e.Length >= 2f).ToList();
        var main = open.OrderByDescending(x => x.e.Length * (x.e.Exposure == Exposure.Street ? 1.3f : 1f)).FirstOrDefault();
        var second = Type == Kind.Barn && c.Detail.Chance(0.5)
            ? open.Where(x => x.i != main.i && Vector2.Dot(x.e.Out, main.e.Out) < -0.8f).OrderByDescending(x => x.e.Length).FirstOrDefault() : default;

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, ground - 0.5f, datum);
            bool blind = e.Exposure is Exposure.Party or Exposure.Sliver || len < 1.6f;
            bool isMain = main.e != null && ei == main.i, isSecond = second.e != null && ei == second.i;
            // the wall up to the eaves, with what it holds
            var openings = new List<(float U0, float U1, float Y0, float Y1, string Infill, bool Door)>();
            if (!blind)
            {
                float h = top - datum;
                if (isMain || isSecond)
                {
                    switch (Type)
                    {
                        case Kind.Barn:
                            float dw = MathF.Min(4.2f, len * 0.4f), dh = MathF.Min(h - 0.2f, 3.8f);
                            openings.Add((len * 0.5f - dw * 0.5f, len * 0.5f + dw * 0.5f, datum, datum + dh, "boards", true));
                            break;
                        case Kind.Hall:
                            int n = Math.Clamp((int)(len / 14f), 1, 3);
                            for (int k = 0; k < n; k++) { float uc = len * (k + 0.5f) / n; openings.Add((uc - 2f, uc + 2f, datum, datum + MathF.Min(4.2f, h - 0.4f), "roller", true)); }
                            break;
                        case Kind.Livestock:
                            int nd = len > 18f ? 2 : 1;
                            for (int k = 0; k < nd; k++) { float uc = len * (k + 1f) / (nd + 1f); openings.Add((uc - 0.8f, uc + 0.8f, datum, datum + 2.1f, "door", true)); }
                            break;
                        default:
                            float w2 = c.Site.Area > 25f && len > 4f ? 0.85f : 0.5f;
                            float ud = len * c.Detail.Range(0.3f, 0.7f);
                            ud = Math.Clamp(ud, w2 + 0.3f, len - w2 - 0.3f);
                            openings.Add((ud - w2, ud + w2, datum, datum + 2.0f, "door", true));
                            break;
                    }
                }
                // windows: a row of small high ones on livestock's long walls, one or two on an outbuilding
                if (Type == Kind.Livestock && len >= 6f)
                    for (float u = 1.6f; u < len - 1.2f; u += 2.6f)
                        if (!openings.Any(o => u + 0.4f > o.U0 - 0.3f && u - 0.4f < o.U1 + 0.3f))
                            openings.Add((u - 0.35f, u + 0.35f, datum + h - 1.15f, datum + h - 0.55f, "glass", false));
                if (Type == Kind.Outbuilding && len >= 3f && c.Detail.Chance(0.6))
                {
                    float u = isMain ? (openings.Count > 0 && openings[0].U0 > len * 0.5f ? len * 0.25f : len * 0.75f) : len * 0.5f;
                    if (!openings.Any(o => u + 0.4f > o.U0 - 0.3f && u - 0.4f < o.U1 + 0.3f))
                        openings.Add((u - 0.4f, u + 0.4f, datum + 1.1f, datum + 1.7f, "glass", false));
                }
                if (Type == Kind.Hall && len >= 10f)
                    for (float u = 3f; u < len - 2f; u += 6f)
                        if (!openings.Any(o => u + 1.2f > o.U0 && u - 1.2f < o.U1))
                            openings.Add((u - 1.2f, u + 1.2f, datum + h - 1.4f, datum + h - 0.6f, "glass", false));
            }
            // lay the wall out: plain stretches between openings, each opening in its own column
            float at0 = 0;
            foreach (var o in openings.OrderBy(o => o.U0))
            {
                Kit.Wall(wall.Mesh, skin, f, at0, o.U0, datum, top);
                Kit.WallWithOpening(wall.Mesh, skin, "reveal", o.Infill, f, o.U0, o.U1, datum, top, o.U0, o.U1, o.Y0, o.Y1, o.Door ? 0.12f : 0.1f);
                if (o.Door) b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{(int)o.U0}"), "entrance", f.P((o.U0 + o.U1) * 0.5f, o.Y0, 0.05f), f.Out));
                else Kit.Box(wall.Mesh, "frame", f, (o.U0 + o.U1) * 0.5f - 0.025f, (o.U0 + o.U1) * 0.5f + 0.025f, o.Y0, o.Y1, -0.09f, -0.06f);
                // a barn door: a frame and a cross brace on each leaf
                if (o.Infill == "boards")
                {
                    float mid = (o.U0 + o.U1) * 0.5f;
                    Kit.Box(wall.Mesh, "door", f, mid - 0.04f, mid + 0.04f, o.Y0, o.Y1, -0.12f, -0.06f);
                    Kit.Box(wall.Mesh, "door", f, o.U0, o.U1, o.Y0 + (o.Y1 - o.Y0) * 0.5f - 0.06f, o.Y0 + (o.Y1 - o.Y0) * 0.5f + 0.06f, -0.12f, -0.07f);
                    Kit.Box(wall.Mesh, "door", f, o.U0 - 0.1f, o.U1 + 0.1f, o.Y1, o.Y1 + 0.12f, 0, 0.06f);
                }
                at0 = o.U1;
            }
            Kit.Wall(wall.Mesh, skin, f, at0, len, datum, top);
            if (slope) Kit.WallTop(wall.Mesh, skin, f, len, top, RoofAt(e.A), RoofAt(e.B));
            if (plan.RoofShape == RoofShape.Flat) Kit.Wall(wall.Mesh, skin, f, 0, len, top, top + 0.15f);
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        switch (plan.RoofShape)
        {
            case RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal:
                float o = (Type == Kind.Hall ? 0.25f : 0.4f) * plan.Eaves;
                Roofs.Pitched(roof.Mesh, "roof", "soffit", skin == "brick" || skin == "wall" ? (Type == Kind.Barn ? "boards" : skin) : skin, plan, top, o);
                Kit.Slab(roof.Mesh, "soffit", c.Site.Outer, c.Site.Holes, top - 0.05f, top, sides: false);
                Gables(c, roof.Mesh, top);
                break;
            case RoofShape.Skillion:
                Roofs.Sloped(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, p => RoofAt(p) + 0.05f);
                break;
            default:
                Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.15f, top + 0.12f, sides: false);
                break;
        }
    }

    /// <summary>A barn's hay-loft door up in a gable; a livestock building's ventilation stack on the ridge.</summary>
    private void Gables(BuildingGenerator.Context c, Mesh m, float eave)
    {
        var plan = c.Plan;
        var r = plan.RoofWings.Count > 0 ? plan.RoofWings.MaxBy(w => w.Rise).Rect : plan.RoofRect;
        float rise = plan.RoofWings.Count > 0 ? plan.RoofWings.Max(w => w.Rise) : plan.RoofRise;
        if (Type == Kind.Barn && plan.RoofShape == RoofShape.Gabled && rise > 2.2f)
        {
            float s = c.Detail.Sign();
            var at = r.At(s * r.HalfL, 0);
            var f = new Kit.Frame(new Vector3(at.X, 0, at.Y), new Vector3(r.V.X, 0, r.V.Y) * s, new Vector3(r.U.X, 0, r.U.Y) * s);
            float hw = MathF.Min(0.6f, r.HalfW * 0.2f), y0 = eave + 0.3f, y1 = eave + MathF.Min(rise - 0.6f, 1.7f);
            Kit.Box(m, "door", f, -hw, hw, y0, y1, 0, 0.04f);
            Kit.Box(m, "door", f, -hw - 0.08f, hw + 0.08f, y1, y1 + 0.1f, 0, 0.07f);
        }
        if (Type == Kind.Livestock && plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped && c.Site.Area > 60f)
        {
            float ridge = eave + rise;
            int n = r.HalfL > 9f ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                var at = r.At(r.HalfL * (n == 1 ? 0f : k == 0 ? -0.45f : 0.45f), 0);
                Kit.BoxXZ(m, "vent", new Vector3(at.X - 0.3f, ridge - 0.4f, at.Y - 0.3f), new Vector3(at.X + 0.3f, ridge + 0.7f, at.Y + 0.3f));
                Kit.BoxXZ(m, "roof", new Vector3(at.X - 0.45f, ridge + 0.7f, at.Y - 0.45f), new Vector3(at.X + 0.45f, ridge + 0.8f, at.Y + 0.45f));
            }
        }
    }

    /// <summary>A greenhouse: milky walls and a pitched roof of the same, on ribs every 1.2 m.</summary>
    private void Greenhouse(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        float datum = plan.Datum, top = c.RoofLevel;
        var g = b.Add(c.Id("glass"), "wall", ModuleTier.Primary);
        foreach (var e in plan.Edges)
        {
            var f = Kit.Frame.Of(e);
            Kit.Wall(g.Mesh, "plinth", f, 0, e.Length, plan.GroundLow - 0.5f, datum);
            Kit.Wall(g.Mesh, "greenhouse", f, 0, e.Length, datum, top);
            int n = Math.Max(1, (int)MathF.Round(e.Length / 1.2f));
            for (int k = 0; k <= n; k++)
            {
                float u = e.Length * k / n;
                Kit.Box(g.Mesh, "rib", f, MathF.Max(0, u - 0.03f), MathF.Min(e.Length, u + 0.03f), datum, top, 0, 0.04f);
            }
            Kit.Box(g.Mesh, "rib", f, 0, e.Length, top - 0.05f, top, 0, 0.05f);
            if (e.Exposure is not (Exposure.Party or Exposure.Sliver) && e.Length > 4f && e == plan.Edges.Where(x => x.Ring == 0).MaxBy(x => x.Length))
                b.Markers.Add(new Marker(c.Id("entrance"), "entrance", f.P(e.Length * 0.5f, datum, 0.05f), f.Out));
        }
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        if (plan.RoofShape is RoofShape.Gabled or RoofShape.Hipped)
        {
            Roofs.Pitched(roof.Mesh, "greenhouse", "greenhouse", "greenhouse", plan, top, 0.05f);
            // ribs over the roof, across the ridge
            var r = plan.RoofRect;
            if (plan.RoofWings.Count == 0)
            {
                int n = Math.Max(1, (int)MathF.Round(2 * r.HalfL / 1.2f));
                for (int k = 0; k <= n; k++)
                {
                    float u = -r.HalfL + 2 * r.HalfL * k / n;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var a = r.At(u, s * r.HalfW); var z = r.At(u, 0);
                        var p0 = new Vector3(a.X, top + 0.02f, a.Y); var p1 = new Vector3(z.X, top + plan.RoofRise + 0.02f, z.Y);
                        var side = new Vector3(r.U.X, 0, r.U.Y) * 0.03f;
                        roof.Mesh.AddQuad("rib", p0 - side, p0 + side, p1 + side, p1 - side, Vector3.UnitY);
                    }
                }
            }
        }
        else Kit.Slab(roof.Mesh, "greenhouse", c.Site.Outer, c.Site.Holes, top - 0.05f, top + 0.02f, sides: false);
    }
}
