using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// Offices and towers (D18, Tom: "modern offices and skyscrapers"). Who: OSM office, or commercial of four storeys or
/// more (13 m+; lower ones are shops and halls), or anything untagged 60 m or taller without a residential type. Three
/// facades, from the start year when mapped, else from height and the seed:
/// * <b>curtain</b>: a glass curtain wall, one surface per wall (role <c>curtain</c>; the panels, mullions and floor
///   bands are drawn by the engine's shader, so a 200 m tower stays a few hundred triangles);
/// * <b>ribbon</b>: bands of glass and solid panels storey by storey (1960s–2000s);
/// * <b>grid</b>: a concrete frame with a deep window per bay (PRL offices).
/// Every one: a glazed lobby on the street side (a taller ground storey, the door with a canopy), a flat roof with a
/// parapet and plant rooms; towers 60 m+ a crown (the parapet raised into a screen), 140 m+ a mast.
/// </summary>
public sealed class Office : IStyle
{
    public const string Name = "office";
    public string Id => Name;
    public float Plinth => 0.1f;
    public float RoofExtra => 0.6f;

    public enum Facade { Curtain, Ribbon, Grid }

    public static bool Fits(BuildingGenerator.Context c)
    {
        var r = c.Request;
        if (r.StartYear is < 1945 || r.Heritage) return false;   // old and listed: a kamienica's evidence, not an office
        float h = r.Height ?? (r.Levels is { } l ? l * 3.6f : 0f);
        if (r.Type is "office") return true;
        if (r.Type is "commercial" && (h >= 13f || r.Levels is >= 4)) return true;
        if (r.Type is null or "yes" && (h >= 60f || r.Levels is >= 18)) return true;
        return false;
    }

    public static Facade FacadeOf(BuildingGenerator.Context c)
    {
        var rng = Rng.Stream(c.Request.Seed, "office.facade");
        if (c.Request.StartYear is { } y)
            return y < 1990 ? rng.Weighted<Facade>([(Facade.Grid, 3), (Facade.Ribbon, 2)]) : y < 2002 ? rng.Weighted<Facade>([(Facade.Ribbon, 2), (Facade.Curtain, 2)]) : Facade.Curtain;
        float h = c.Request.Height ?? 20f;
        return h >= 60 ? rng.Weighted<Facade>([(Facade.Curtain, 8), (Facade.Grid, 2)])
             : rng.Weighted<Facade>([(Facade.Curtain, 4), (Facade.Ribbon, 4), (Facade.Grid, 2)]);
    }

    private static bool Tower(BuildingGenerator.Context c) => (c.Request.Height ?? 0) >= 60f || c.Request.Levels is >= 18;

    public int InferLevels(BuildingGenerator.Context c) => c.Site.Area > 1500 ? c.PlanRng.Int(5, 9) : c.PlanRng.Int(4, 7);
    public float StoreyHeight(BuildingGenerator.Context c) => 3.6f;
    public string InferRoof(BuildingGenerator.Context c) => "flat";
    public float RoofPitch(BuildingGenerator.Context c) => 0f;

    public void Materials(BuildingGenerator.Context c)
    {
        var f = FacadeOf(c);
        var m = c.Building.Materials;
        string glass = c.Look.Weighted<string>([("31414f", 4), ("2c3b38", 2), ("3b4a5c", 3), ("4a4136", 1), ("1f2a33", 2), ("6f8a99", 1)]);
        string panel = f switch
        {
            Facade.Grid => c.Look.Weighted<string>([("a8a49b", 3), ("8f8b84", 2), ("c2bcae", 1)]),
            Facade.Ribbon => c.Look.Weighted<string>([("d8d6d0", 2), ("9aa0a6", 2), ("5d6670", 1), ("b8b0a0", 1)]),
            _ => c.Look.Weighted<string>([("5a6168", 2), ("8a9097", 2), ("2f3438", 1)]),
        };
        c.Plan = c.Plan with { Palette = $"{f.ToString().ToLowerInvariant()}-{glass}" };
        c.Building.Plan = c.Plan;
        m["wall"] = Kit.Mat("wall", panel, 0.8f);
        m["wall_alt"] = Kit.Mat("wall_alt", panel, 0.8f);
        m["curtain"] = Kit.Mat("curtain", glass, 0.08f, 0.3f);
        m["glass"] = Kit.Mat("glass", glass, 0.08f, 0.2f);
        m["reveal"] = Kit.Mat("reveal", "6a6c6e", 0.8f);
        m["frame"] = Kit.Mat("frame", "3a3e42", 0.5f, 0.6f);
        m["plinth"] = Kit.Mat("plinth", "4c4e50", 0.9f);
        m["door"] = Kit.Mat("door", "2a3034", 0.4f, 0.5f);
        m["roof"] = Kit.Mat("roof", "5a5a58", 0.9f);
        m["plant"] = Kit.Mat("plant", "8c9296", 0.55f, 0.4f);
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        var facade = FacadeOf(c);
        int levels = plan.Levels.Value;
        float sh = plan.StoreyHeight, datum = plan.Datum, top = c.RoofLevel;
        float lobbyTop = datum + MathF.Min(5f, MathF.Max(sh, (top - datum) * 0.3f));
        var doors = c.Site.Small || c.Request.Raised ? [] : Entrances.Place(c, e => e.Exposure == Exposure.Street ? 1 : 0, 6f);

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            var wall = b.Add(c.Id($"wall.e{e.Ring}.{e.Index}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2), ["facade"] = facade.ToString().ToLowerInvariant() };
            Kit.Wall(wall.Mesh, "plinth", f, 0, len, plan.GroundLow - 0.5f, datum);
            bool blank = e.Exposure is Exposure.Party or Exposure.Sliver || len < 2f;
            if (blank) { Kit.Wall(wall.Mesh, "wall", f, 0, len, datum, top); continue; }

            // the lobby: glass from the ground on the street side, a door with a canopy
            var myDoors = doors.Where(d => d.Edge == ei).Select(d => d.U).ToList();
            bool street = e.Exposure == Exposure.Street && !c.Request.Raised;   // a part in the air has no lobby
            float upper = street ? lobbyTop : datum;
            if (street)
            {
                float u = 0;
                foreach (var du in myDoors.OrderBy(x => x))
                {
                    float d0 = MathF.Max(u, du - 1.2f), d1 = MathF.Min(len, du + 1.2f);
                    Lobby(wall.Mesh, f, u, d0, datum, lobbyTop);
                    Kit.WallWithOpening(wall.Mesh, "frame", "reveal", "door", f, d0, d1, datum, lobbyTop, d0 + 0.1f, d1 - 0.1f, datum, datum + 2.6f, 0.3f);
                    var canopy = b.Add(c.Id($"canopy.e{e.Ring}.{e.Index}.{du:0}"), "canopy", ModuleTier.Secondary, wall.Id);
                    Kit.Box(canopy.Mesh, "frame", f, d0 - 0.8f, d1 + 0.8f, datum + 3.1f, datum + 3.35f, 0, 2.0f);
                    b.Markers.Add(new Marker(c.Id($"entrance.e{e.Ring}.{e.Index}.{du:0}"), "entrance", f.P(du, datum, 0.05f), f.Out));
                    u = d1;
                }
                Lobby(wall.Mesh, f, u, len, datum, lobbyTop);
            }

            switch (facade)
            {
                case Facade.Curtain:
                    Kit.Wall(wall.Mesh, "curtain", f, 0, len, upper, top);
                    break;
                case Facade.Ribbon:
                    for (float y = upper; y < top - 0.05f; y += sh)
                    {
                        float y1 = MathF.Min(top, y + sh);
                        Kit.Wall(wall.Mesh, "wall", f, 0, len, y, y + MathF.Min(1.0f, y1 - y));
                        if (y + 1.0f < y1) Kit.Wall(wall.Mesh, "glass", f, 0, len, y + 1.0f, MathF.Max(y + 1.0f, y1 - 0.25f), -0.12f);
                        if (y1 - 0.25f > y + 1.0f) Kit.Wall(wall.Mesh, "wall", f, 0, len, y1 - 0.25f, y1);
                        // the recessed glass band's edges
                        Kit.Wall(wall.Mesh, "frame", f, 0, len, y + 1.0f, y + 1.04f, -0.01f);
                    }
                    break;
                default:
                    int bays = Math.Max(1, (int)MathF.Round(len / 2.4f));
                    float bw = len / bays;
                    for (int bi = 0; bi < bays; bi++)
                        for (float y = upper; y < top - 0.05f; y += sh)
                        {
                            float y1 = MathF.Min(top, y + sh);
                            Kit.WallWithOpening(wall.Mesh, "wall", "reveal", "glass", f, bi * bw, (bi + 1) * bw, y, y1,
                                bi * bw + 0.35f, (bi + 1) * bw - 0.35f, y + 0.85f, y1 - 0.45f, 0.35f);
                        }
                    break;
            }
        }

        // ---- roof: parapet (a crown on towers), plant rooms, a mast on the tallest ----
        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.05f, sides: false);
        bool tower = Tower(c);
        float crown = tower ? c.Look.Range(2.5f, 6f) : 0.9f;
        Kit.Parapet(roof.Mesh, facade == Facade.Curtain && tower ? "curtain" : "wall_alt", c.Site.Outer, top + 0.05f, top + crown, tower ? 0.3f : 0.25f);
        var r = Roofs.MinRect(c.Site.Outer);
        var rng = Rng.Stream(c.Request.Seed, "office.plant");
        int boxes = Math.Clamp((int)(c.Site.Area / 400), 1, 4);
        for (int k = 0; k < boxes; k++)
        {
            float hw = MathF.Min(r.HalfL * 0.6f, rng.Range(2f, 5f)), hd = MathF.Min(r.HalfW * 0.6f, rng.Range(1.5f, 4f));
            var p = r.At(rng.Range(-r.HalfL + hw + 1, r.HalfL - hw - 1), rng.Range(-r.HalfW + hd + 1, r.HalfW - hd - 1));
            if (!Inside(c.Site.Outer, p)) continue;
            float ph = rng.Range(2.2f, 4f);
            var plant = b.Add(c.Id($"plant.{k}"), "plant", ModuleTier.Secondary, roof.Id);
            var u = new Vector3(r.U.X, 0, r.U.Y); var v = new Vector3(r.V.X, 0, r.V.Y);
            var f = new Kit.Frame(new Vector3(p.X, 0, p.Y) - u * hw + v * hd, u, v);
            Kit.Box(plant.Mesh, "plant", f, 0, hw * 2, top + 0.05f, top + ph, -hd * 2, 0);
        }
        if ((c.Request.Height ?? 0) >= 140f)
        {
            var mast = b.Add(c.Id("mast"), "mast", ModuleTier.Secondary, roof.Id);
            var cp = r.Centre;
            float mh = c.Look.Range(15f, 40f);
            Kit.BoxXZ(mast.Mesh, "frame", new Vector3(cp.X - 0.6f, top, cp.Y - 0.6f), new Vector3(cp.X + 0.6f, top + mh, cp.Y + 0.6f));
            b.Markers.Add(new Marker(c.Id("mast.top"), "beacon", new Vector3(cp.X, top + mh, cp.Y), Vector3.UnitY));
        }
    }

    /// <summary>The lobby's glass between u0 and u1, with slim mullions every 1.5 m.</summary>
    private static void Lobby(Mesh m, Kit.Frame f, float u0, float u1, float y0, float y1)
    {
        if (u1 - u0 < 0.05f) return;
        Kit.Wall(m, "glass", f, u0, u1, y0, y1, -0.2f);
        Kit.Wall(m, "frame", f, u0, u1, y1 - 0.25f, y1);   // the lintel band
        int n = Math.Max(1, (int)((u1 - u0) / 1.5f));
        for (int k = 0; k <= n; k++)
        {
            float u = u0 + (u1 - u0) * k / n;
            Kit.Box(m, "frame", f, MathF.Max(u0, u - 0.04f), MathF.Min(u1, u + 0.04f), y0, y1, -0.2f, -0.12f);
        }
    }

    private static bool Inside(IReadOnlyList<Vector2> ring, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y) && p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                inside = !inside;
        return inside;
    }
}
