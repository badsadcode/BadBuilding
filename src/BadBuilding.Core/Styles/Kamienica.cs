using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;

namespace BadBuilding.Styles;

/// <summary>
/// The kamienica: a pre-war Warsaw tenement in a street frontage (D9, Noor's brief in NOOR_SAGE.md Q3). Proportions and
/// composition first: tall storeys, one window axis per bay repeated storey after storey, a stone plinth, a string
/// course over the ground floor and a cornice under a street attic. The street front and the yard back are two
/// different buildings in character: the street gets surrounds, shop fronts and a proper entrance; the yard gets plain
/// plaster, smaller practical windows and service doors. Party walls are blank brick firewalls with chimney stacks.
/// <para>
/// Not every kamienica is equally ornate: each building gets an ornament level (plain and renovated, modest, or rich)
/// and a palette from the seed, so a street of them varies within one family.
/// </para>
/// </summary>
public sealed class Kamienica : IStyle
{
    public const string Name = "kamienica";
    public string Id => Name;
    public float Plinth => 0.5f;
    public float RoofExtra => 0.9f;
    private const float Bay = 3.1f, DefaultStorey = 3.5f;

    /// <summary>
    /// A walled-in town building: at least one party wall, 2–8 storeys, not a house, deep enough for rooms, and not
    /// mapped as built after the war (D10: a post-war infill also touches its neighbours).
    /// </summary>
    public static bool Fits(BuildingGenerator.Context c)
    {
        if (c.Request.StartYear is >= 1945) return false;
        if (c.Request.Raised) return false;   // a tenement is a whole building from the street up, not a piece high on a tower (D24)
        var t = c.Request.Type;
        bool kind = t is null or "yes" or "apartments" or "residential" or "commercial" or "retail" or "office" or "hotel";
        if (!kind || c.Site.Depth < 6f || c.Site.Depth > 30f) return false;
        if (c.Request.Levels is { } lv && (lv < 2 || lv > 8)) return false;
        return c.Site.Edges.Any(e => e.Exposure == Exposure.Party);
    }

    /// <summary>
    /// Evidence that this is really a pre-war tenement (Noor Q8): a mapped build year before 1945, a heritage tag or a
    /// mapped architecture. Missing data is not evidence: without it the building is a modest town house of the family,
    /// never a rich one confidently invented (D10).
    /// </summary>
    public static bool Historic(BuildingRequest r) =>
        r.StartYear is < 1945 || r.Heritage || r.Architecture is { Length: > 0 } a && a is not ("modern" or "brutalist" or "socialist_realism" or "contemporary");

    public int InferLevels(BuildingGenerator.Context c) => c.PlanRng.Weighted<int>([(3, 1), (4, 3), (5, 3), (6, 1)]);
    public float StoreyHeight(BuildingGenerator.Context c) => DefaultStorey;
    /// <summary>Noor Q6: a mono-pitched roof falling from the street to the yard, hidden behind the street attic.</summary>
    public string InferRoof(BuildingGenerator.Context c) => "skillion";
    public float RoofPitch(BuildingGenerator.Context c) => 7f;

    private static readonly (string Name, string Plaster, string Trim, string Yard)[] Palettes =
    [
        ("cream", "e4d4ad", "f1e8d2", "cfc4ab"),
        ("ochre", "d2a65f", "ecdcb6", "c4b08c"),
        ("salmon", "d69a80", "efd8c4", "c7ad9c"),
        ("yellow", "e1c977", "f2e6c2", "cdbf95"),
        ("grey", "a9a59c", "d1ccc2", "a19d95"),
        ("sooty", "86817a", "9f9990", "7d7871"),   // never renovated
        ("white", "ebe7de", "d8d1c2", "cfcac0"),
    ];

    private static readonly string[] Signs = ["8c2f2a", "2f4f7a", "2e6a46", "6b4a2a", "c79a2a", "3a3a3a", "7a2e5a"];

    /// <summary>0 = plain (or renovated flat), 1 = modest (surrounds, string course), 2 = rich (headers, a balcony,
    /// a deep two-step cornice).</summary>
    private int _ornament;
    private bool _shops;
    private IReadOnlyList<Vector2>? _shopAt;

    public void Materials(BuildingGenerator.Context c)
    {
        var p = c.Look.Pick(Palettes);
        bool historic = Historic(c.Request);
        _ornament = !historic ? c.Look.Weighted<int>([(0, 1), (1, 1)])
            : p.Name == "sooty" ? c.Look.Weighted<int>([(1, 1), (2, 2)]) : c.Look.Weighted<int>([(0, 2), (1, 3), (2, 2)]);
        // shops: mapped shops decide where they are; without shop data a guess, more likely on a commercial building
        var mapped = c.Request.Shops;
        _shops = c.Request.ShopOnBuilding || mapped is { Count: > 0 } || (mapped is null || mapped.Count == 0) && (c.Request.Type is "commercial" or "retail" || c.Look.Chance(0.7));
        _shopAt = mapped is { Count: > 0 } && !c.Request.ShopOnBuilding ? mapped : null;
        c.Plan = c.Plan with { Palette = $"{p.Name}, ornament {_ornament}{(historic ? "" : " (no age evidence)")}{(_shops ? _shopAt != null ? ", mapped shops" : ", shops" : "")}" };
        c.Building.Plan = c.Plan;
        var m = c.Building.Materials;
        m["wall"] = Kit.Mat("wall", p.Plaster, 0.92f);
        m["wall_ground"] = Kit.Mat("wall_ground", Shade(p.Plaster, 0.88f), 0.92f);
        m["wall_yard"] = Kit.Mat("wall_yard", p.Yard, 0.95f);
        m["trim"] = Kit.Mat("trim", p.Trim, 0.85f);
        m["brick"] = Kit.Mat("brick", c.Look.Pick(new[] { "8a4b38", "7d4434", "945542" }), 0.95f);
        m["plinth"] = Kit.Mat("plinth", "6e6a63", 0.95f);
        m["reveal"] = Kit.Mat("reveal", Shade(p.Plaster, 0.75f), 0.9f);
        m["glass"] = Kit.Mat("glass", "26323b", 0.08f, 0.1f);
        m["door"] = Kit.Mat("door", c.Look.Pick(new[] { "4a3324", "3a2a20", "2f3a2e", "3b3a36" }), 0.6f);
        m["sign"] = Kit.Mat("sign", c.Look.Pick(Signs), 0.6f);
        m["shutter"] = Kit.Mat("shutter", "8f9296", 0.5f, 0.6f);
        m["metal"] = Kit.Mat("metal", "262626", 0.5f, 0.7f);
        m["roof"] = Kit.Mat("roof", "4a4c4e", 0.8f, 0.3f);
    }

    private static string Shade(string hex, float k)
    {
        var b = Convert.FromHexString(hex);
        return Convert.ToHexString(b.Select(x => (byte)Math.Clamp(x * k, 0, 255)).ToArray());
    }

    public void Build(BuildingGenerator.Context c)
    {
        var plan = c.Plan;
        var b = c.Building;
        int levels = plan.Levels.Value;
        float datum = plan.Datum, top = c.RoofLevel, sh = plan.StoreyHeight;
        int orn = _ornament;
        // one street door when the yard has no gate; the staircases are reached from the yard otherwise
        var doors = Entrances.Place(c, _ => 1, Bay);
        // the rich facade's balcony: the middle bay of the longest street wall, first floor (piano nobile)
        int balconyEdge = -1; float longest = 9f;
        for (int i = 0; i < plan.Edges.Count; i++)
            if (plan.Edges[i].Exposure == Exposure.Street && plan.Edges[i].Length > longest) { longest = plan.Edges[i].Length; balconyEdge = i; }

        // the roof (D13, Noor Q6): mono-pitched, highest along the main street front and falling to the yard; the
        // street attic rises high enough to hide it. Flat when the map says flat.
        bool slope = plan.RoofShape == RoofShape.Skillion;
        var front = plan.Edges.Where(e => e.Ring == 0 && e.Exposure == Exposure.Street).OrderByDescending(e => e.Length).FirstOrDefault()
            ?? plan.Edges.Where(e => e.Ring == 0 && e.Exposure != Exposure.Party).OrderByDescending(e => e.Length).FirstOrDefault()
            ?? plan.Edges[0];
        float reach = MathF.Max(1f, c.Site.Outer.Max(p => Vector2.Dot(p - front.A, -front.Out)));
        float RoofAt(Vector2 p) => slope ? top + plan.RoofRise * (1f - Math.Clamp(Vector2.Dot(p - front.A, -front.Out) / reach, 0f, 1f)) : top;
        float attic = slope ? MathF.Max(0.9f, plan.RoofRise + 0.3f) : 0.9f;

        for (int ei = 0; ei < plan.Edges.Count; ei++)
        {
            var e = plan.Edges[ei];
            var f = Kit.Frame.Of(e);
            float len = e.Length;
            string tag = $"e{e.Ring}.{e.Index}";
            var wall = b.Add(c.Id($"wall.{tag}"), "wall", ModuleTier.Primary);
            wall.Extras = new() { ["exposure"] = e.Exposure.ToString().ToLowerInvariant(), ["length"] = MathF.Round(len, 2) };
            var trim = b.Add(c.Id($"trim.{tag}"), "facade_trim", ModuleTier.Secondary, wall.Id);
            var cuts = c.Passages.Select(p => p.Cut(ei)).OfType<(float, float)>().ToList();
            var pass = c.Passages.FirstOrDefault(p => p.Cut(ei) != null);
            float cutLow = pass?.Floor ?? 0, cutHigh = pass?.Ceiling ?? 0;
            bool street = e.Exposure == Exposure.Street;
            bool party = e.Exposure == Exposure.Party;
            bool blind = party || e.Exposure == Exposure.Sliver;

            Kit.WallCut(wall.Mesh, "plinth", f, 0, len, plan.GroundLow - 0.5f, datum, cuts, cutLow, cutHigh);
            if (len < 0.4f) { Kit.Wall(wall.Mesh, party ? "brick" : "wall_yard", f, 0, len, datum, top); continue; }
            if (party)
            {
                // a firewall: bare brick from the plinth up past the roof (following its slope), with chimney stacks
                Kit.Wall(wall.Mesh, "brick", f, 0, len, datum, top);
                Kit.WallTop(wall.Mesh, "brick", f, len, top, RoofAt(e.A) + 0.6f, RoofAt(e.B) + 0.6f);
                // stacks only on a real tenement's firewall (3+ storeys, 6 m+ long), one per ~10 m, set into the wall
                // (Tom's dump on Daniłowiczowska: two-storey houses with a row of big brick boxes on every short wall)
                int stacks = levels >= 3 && len >= 6f ? Math.Max(1, (int)(len / 10f)) : 0;
                for (int si = 0; si < stacks; si++)
                {
                    float u = len * (si + 0.5f) / stacks;
                    float at = RoofAt(e.A + (e.B - e.A) * (u / len));
                    Kit.Box(trim.Mesh, "brick", f, u - 0.4f, u + 0.4f, top, at + 1.3f, -0.6f, -0.05f);
                }
                continue;
            }

            string body = street ? "wall" : "wall_yard";
            int bays = Math.Max(1, (int)MathF.Round(len / Bay));
            float bw = len / bays;
            var doorBays = doors.Where(d => d.Edge == ei).Select(d => Math.Clamp((int)(d.U / bw), 0, bays - 1)).ToHashSet();
            for (int bi = 0; bi < bays; bi++)
            {
                float u0 = bi * bw, u1 = u0 + bw, uc = (u0 + u1) * 0.5f;
                bool stair = doorBays.Contains(bi);
                for (int s = 0; s < levels; s++)
                {
                    float y0 = datum + s * sh, y1 = y0 + sh;
                    string role = s == 0 && street ? "wall_ground" : body;
                    if (s == 0 && Kit.Overlaps(cuts, u0, u1)) { Kit.WallCut(wall.Mesh, role, f, u0, u1, y0, y1, cuts, cutLow, cutHigh); continue; }
                    if (blind || bw < 1.4f) { Kit.Wall(wall.Mesh, role, f, u0, u1, y0, y1); continue; }
                    if (s == 0 && stair)
                    {
                        // street: a tall double door with a transom and a surround; yard: a plain service door
                        float hw = street ? MathF.Min(0.75f, bw * 0.3f) : 0.5f, hh = street ? MathF.Min(sh - 0.35f, 3.0f) : 2.2f;
                        Kit.WallWithOpening(wall.Mesh, role, "reveal", "door", f, u0, u1, y0, y1, uc - hw, uc + hw, y0, y0 + hh, 0.3f);
                        if (street && orn > 0) Surround(trim.Mesh, f, uc - hw, uc + hw, y0, y0 + hh, 0.2f, false);
                        Kit.Box(wall.Mesh, "plinth", f, uc - hw - 0.3f, uc + hw + 0.3f, plan.GroundLow - 0.3f, y0, 0, 0.6f);
                        b.Markers.Add(new Marker(c.Id($"entrance.{tag}.{bi}"), "entrance", f.P(uc, y0, 0.05f), f.Out));
                        continue;
                    }
                    if (s == 0 && street && _shops && (_shopAt == null || _shopAt.Any(q => Vector2.Distance(q, ToPlan(f.P(uc, 0))) < 6f)))
                    {
                        Shop(c, wall.Mesh, trim.Mesh, f, u0, u1, y0, y1, role, tag, bi);
                        continue;
                    }
                    if (!street)
                    {
                        // the yard: smaller practical windows; a staircase gets its window between floors
                        if (stair && s > 0) { Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass", f, u0, u1, y0, y1, uc - 0.45f, uc + 0.45f, y0 - 0.9f + 1.0f, y0 + 1.3f, 0.2f); continue; }
                        Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass", f, u0, u1, y0, y1, uc - 0.5f, uc + 0.5f, y0 + 0.9f, y0 + MathF.Min(sh - 0.5f, 2.4f), 0.2f);
                        continue;
                    }
                    // the street: tall windows on every axis; the ground floor ones raised above the pavement
                    float ww = MathF.Min(0.62f, bw * 0.22f);
                    float sill = s == 0 ? 1.1f : 0.85f, head = MathF.Min(sh - 0.45f, s == 0 ? 2.9f : 2.8f);
                    bool balcony = orn == 2 && s == 1 && ei == balconyEdge && bi == bays / 2 && !stair;
                    if (balcony) sill = 0.05f;
                    Kit.WallWithOpening(wall.Mesh, role, "reveal", "glass", f, u0, u1, y0, y1, uc - ww, uc + ww, y0 + sill, y0 + head, 0.25f);
                    if (orn > 0) Surround(trim.Mesh, f, uc - ww, uc + ww, y0 + sill, y0 + head, orn == 2 ? 0.16f : 0.12f, !balcony);
                    if (orn == 2 && s == 1)
                        Kit.Box(trim.Mesh, "trim", f, uc - ww - 0.3f, uc + ww + 0.3f, y0 + head + 0.16f, y0 + head + 0.36f, 0, 0.18f);   // a header over the piano nobile
                    if (balcony) Balcony(trim.Mesh, f, uc - ww - 0.6f, uc + ww + 0.6f, y0);
                }
            }

            if (street)
            {
                // a string course over the ground floor, the cornice under the attic and the attic itself.
                // Where the next wall is a street front too, each band runs on past the corner by its own depth, so
                // the bands of two walls meet at an outside corner instead of leaving a notch (Tom: gaps in the gzyms).
                var ring = plan.Edges.Where(x => x.Ring == e.Ring).ToList();
                int at = ring.IndexOf(e);
                bool prevStreet = ring[(at - 1 + ring.Count) % ring.Count].Exposure == Exposure.Street;
                bool nextStreet = ring[(at + 1) % ring.Count].Exposure == Exposure.Street;
                void Band(float y0, float y1, float w0, float w1)
                {
                    float d = MathF.Max(w1, -w0);
                    Kit.Box(trim.Mesh, "trim", f, prevStreet ? -d : 0, nextStreet ? len + d : len, y0, y1, w0, w1);
                }
                if (orn > 0 && levels > 1) Band(datum + sh - 0.12f, datum + sh + 0.12f, 0, 0.12f);
                Kit.Wall(wall.Mesh, "wall", f, 0, len, top, top + attic);
                if (orn == 0) Band(top - 0.2f, top + 0.05f, 0, 0.22f);
                else if (orn == 1) Band(top - 0.3f, top + 0.15f, 0, 0.4f);
                else
                {
                    Band(top - 0.45f, top - 0.2f, 0, 0.22f);
                    Band(top - 0.2f, top + 0.2f, 0, 0.55f);
                }
                Band(top + attic, top + attic + 0.1f, -0.25f, 0.06f);   // attic coping
            }
            else
            {
                // yard and side walls rise to meet the sloping roof, with a thin band on top
                Kit.WallTop(wall.Mesh, body, f, len, top, RoofAt(e.A) + 0.15f, RoofAt(e.B) + 0.15f);
                if (!slope) Kit.Wall(wall.Mesh, body, f, 0, len, top, top + 0.15f);
            }
        }

        var roof = b.Add(c.Id("roof"), "roof", ModuleTier.Primary);
        if (slope)
        {
            Roofs.Sloped(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, p => RoofAt(p) + 0.15f);
            return;
        }
        // a flat roof behind the attic, a low parapet round the yard side
        Kit.Slab(roof.Mesh, "roof", c.Site.Outer, c.Site.Holes, top - 0.2f, top + 0.15f, sides: false);
        Kit.Parapet(roof.Mesh, "wall_yard", c.Site.Outer, top + 0.15f, top + 0.5f, 0.2f);
        foreach (var h in c.Site.Holes) Kit.Parapet(roof.Mesh, "wall_yard", h, top + 0.15f, top + 0.5f, 0.2f);
    }

    private static Vector2 ToPlan(Vector3 p) => new(p.X, p.Z);

    /// <summary>A window surround: jambs and a lintel band round the opening, and a sill shelf below it.</summary>
    private static void Surround(Mesh m, Kit.Frame f, float a, float b, float y0, float y1, float t, bool sill)
    {
        Kit.Box(m, "trim", f, a - t, a, y0, y1, 0, 0.06f);
        Kit.Box(m, "trim", f, b, b + t, y0, y1, 0, 0.06f);
        Kit.Box(m, "trim", f, a - t, b + t, y1, y1 + t, 0, 0.07f);
        if (sill) Kit.Box(m, "trim", f, a - t - 0.06f, b + t + 0.06f, y0 - 0.1f, y0, 0, 0.1f);
    }

    /// <summary>A shallow stone balcony on brackets with an iron railing.</summary>
    private static void Balcony(Mesh m, Kit.Frame f, float a, float b, float y)
    {
        Kit.Box(m, "trim", f, a, b, y - 0.18f, y, 0, 0.8f);
        for (float u = a + 0.25f; u < b; u += (b - a - 0.5f)) Kit.Box(m, "trim", f, u - 0.1f, u + 0.1f, y - 0.6f, y - 0.18f, 0, 0.6f);
        Kit.Box(m, "metal", f, a, b, y + 0.95f, y + 1.0f, 0.74f, 0.79f);
        Kit.Box(m, "metal", f, a, a + 0.04f, y + 0.95f, y + 1.0f, 0, 0.79f);
        Kit.Box(m, "metal", f, b - 0.04f, b, y + 0.95f, y + 1.0f, 0, 0.79f);
        for (float u = a + 0.06f; u < b - 0.04f; u += 0.14f) Kit.Box(m, "metal", f, u, u + 0.02f, y, y + 0.95f, 0.75f, 0.77f);
    }

    /// <summary>A shop front filling one ground-floor bay: a display window with a door in it, a sign band above,
    /// sometimes a roller shutter pulled down.</summary>
    private void Shop(BuildingGenerator.Context c, Mesh wall, Mesh trim, Kit.Frame f, float u0, float u1, float y0, float y1, string role, string tag, int bi)
    {
        float a = u0 + 0.3f, b = u1 - 0.3f, top = y0 + MathF.Min(y1 - y0 - 0.75f, 2.8f);
        bool shut = c.Detail.Chance(0.2);
        Kit.WallWithOpening(wall, role, "reveal", shut ? "shutter" : "glass", f, u0, u1, y0, y1, a, b, y0 + (shut ? 0.0f : 0.45f), top, shut ? 0.08f : 0.15f);
        if (!shut)
        {
            // the stall riser under the glass and the door at one end of the window
            Kit.Box(wall, "door", f, a, b, y0, y0 + 0.45f, -0.15f, -0.13f);
            float d = c.Detail.Chance(0.5) ? a : b - 0.95f;
            Kit.Box(wall, "door", f, d, d + 0.95f, y0, y0 + 2.25f, -0.15f, -0.11f);
            c.Building.Markers.Add(new Marker(c.Id($"shop.{tag}.{bi}"), "shop_entrance", f.P(d + 0.47f, y0, 0.05f), f.Out));
        }
        else
        {
            Kit.Box(trim, "shutter", f, a - 0.05f, b + 0.05f, top, top + 0.3f, 0, 0.25f);   // the shutter box
            // still a shop, only closed: the way in is behind the shutter
            c.Building.Markers.Add(new Marker(c.Id($"shop.{tag}.{bi}"), "shop_entrance", f.P((a + b) * 0.5f, y0, 0.05f), f.Out));
        }
        Kit.Box(trim, "sign", f, u0 + 0.2f, u1 - 0.2f, top + (shut ? 0.32f : 0.1f), top + (shut ? 0.32f : 0.1f) + 0.4f, 0, 0.08f);
    }
}
