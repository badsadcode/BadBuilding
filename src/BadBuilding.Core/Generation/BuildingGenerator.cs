using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;
using BadBuilding.Styles;
using BadBuilding.Validation;

namespace BadBuilding.Generation;

/// <summary>
/// Footprint in, building out. Stages, each with its own random stream so rerolling one leaves the rest alone (D2):
/// site (no randomness: cleanup, exposure, datum) → plan ("plan": style, storeys, roof) → look ("look": palette) →
/// facade ("facade": bays, balconies, entrances) → detail ("detail": seams, small parts) → mass and validation.
/// </summary>
public static class BuildingGenerator
{
    public const string Version = "0.1.0";

    public static Building Generate(BuildingRequest r)
    {
        var b = new Building { Request = r };
        var site = Site.Analyse(r, b.Report);
        if (!site.Usable) return b;

        var ctx = new Context(r, site, b);
        var style = ChooseStyle(ctx);
        var plan = MakePlan(ctx, style);
        b.Plan = plan;
        ctx.Plan = plan;
        if (!site.Small && !r.Raised) ctx.Passages.AddRange(Passages.Plan(ctx, style.Plinth, style is Kamienica ? 60f : 150f));
        style.Materials(ctx);
        style.Build(ctx);
        if (r.Raised)
        {
            // the underside of a part in the air (D19)
            var under = b.Add(ctx.Id("soffit"), "soffit", ModuleTier.Primary);
            Kit.Slab(under.Mesh, b.Materials.ContainsKey("soffit") ? "soffit" : "wall", site.Outer, site.Holes, plan.Datum - 0.05f, plan.Datum, sides: false);
        }
        foreach (var p in ctx.Passages)
        {
            string side = b.Materials.ContainsKey("concrete") ? "concrete" : "reveal";
            Passages.Build(ctx, p, side, side, "plinth", "door", "reveal");
        }
        AddMass(ctx);
        Validate(ctx);
        return b;
    }

    /// <summary>Shared state for one generation run: request, site, output, plan and the stage streams.</summary>
    public sealed class Context(BuildingRequest r, Site site, Building b)
    {
        public BuildingRequest Request { get; } = r;
        public Site Site { get; } = site;
        public Building Building { get; } = b;
        public BuildingPlan Plan { get; set; } = null!;
        public Rng PlanRng { get; } = Stage(r, "plan");
        public Rng Look { get; } = Stage(r, "look");
        public Rng Facade { get; } = Stage(r, "facade");
        public Rng Detail { get; } = Stage(r, "detail");
        /// <summary>Ways into the courtyards, planned before the facade (D7).</summary>
        public List<Passage> Passages { get; } = [];
        public float RoofLevel => Plan.Datum + Plan.Levels.Value * Plan.StoreyHeight;
        public string Id(string part) => $"{Request.Id}/{part}";

        private static Rng Stage(BuildingRequest r, string stage) =>
            r.StageSeeds.TryGetValue(stage, out var s) ? Rng.Stream(s, stage) : Rng.Stream(r.Seed, stage);
    }

    private static IStyle ChooseStyle(Context c)
    {
        var r = c.Request;
        if (r.Style is { } forced)
            return forced switch
            {
                PrlBlock.Name => new PrlBlock(),
                Plain.Name => new Plain(),
                Kamienica.Name => new Kamienica(),
                Industrial.Name => new Industrial(),
                Office.Name => new Office(),
                _ => throw new ArgumentException($"Unknown style '{forced}' (styles: {PrlBlock.Name}, {Kamienica.Name}, {Industrial.Name}, {Office.Name}, {Plain.Name})."),
            };
        if (Industrial.Fits(c)) return new Industrial();   // (D17) small sheds too: the type says what it is
        if (!c.Site.Small && Office.Fits(c)) return new Office();   // (D18)
        if (!c.Site.Small && Kamienica.Fits(c)) return new Kamienica();
        if (!c.Site.Small && PrlBlock.Fits(c)) return new PrlBlock();
        return new Plain();
    }

    private static BuildingPlan MakePlan(Context c, IStyle style)
    {
        var r = c.Request;
        var src = r.FromOsm ? Source.Osm : Source.Request;
        var levels = r.Levels is { } lv and > 0 ? new Sourced<int>(Math.Clamp(lv, 1, 60), src) : new Sourced<int>(style.InferLevels(c), Source.Inferred);
        float sh = style.StoreyHeight(c);
        float plinth = style.Plinth;

        // the roof first (D13): a pitched roof's rise comes out of a mapped height
        Sourced<string> roof = r.Roof is { Length: > 0 } rs ? new Sourced<string>(rs, src) : new Sourced<string>(style.InferRoof(c), Source.Inferred);
        var shape = Roofs.Parse(roof.Value);
        if (shape == RoofShape.Flat && roof.Value is not ("flat" or ""))
            c.Building.Report.Add(Severity.Info, "roof.not_supported", $"Mapped roof '{roof.Value}' is drawn flat in version {Version}.", "flat roof");
        var rect = Roofs.MinRect(c.Site.Outer);
        if (shape is RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal && (rect.Fill < Roofs.RectangularEnough || c.Site.Holes.Count > 0))
        {
            c.Building.Report.Add(Severity.Info, "roof.complex_footprint",
                $"A {roof.Value} roof needs a nearly rectangular footprint without courtyards (this one fills {rect.Fill:P0} of its rectangle).", "flat roof");
            shape = RoofShape.Flat;
        }
        float rise = shape switch
        {
            RoofShape.Gabled or RoofShape.Hipped or RoofShape.Pyramidal => r.RoofHeight is { } rh and > 0.3f ? rh : Roofs.Rise(rect, style.RoofPitch(c)),
            RoofShape.Skillion => r.RoofHeight is { } rh2 and > 0.3f ? rh2 : MathF.Min(2.2f, 2f * rect.HalfW * MathF.Tan(style.RoofPitch(c) * MathF.PI / 180f)),
            _ => 0f,
        };
        rise = MathF.Min(rise, 9f);
        float extra = shape == RoofShape.Flat ? style.RoofExtra : rise;

        Sourced<float> height;
        float minH = r.Raised ? r.MinHeight!.Value : 0f;
        if (r.Raised) plinth = 0f;
        if (r.Height is { } h and > 2f)
        {
            height = new Sourced<float>(h, src);
            float walls = MathF.Max(2.4f, h - minH - extra * (shape == RoofShape.Flat ? 0f : 1f));
            // a mapped height without levels decides the storeys
            if (r.Levels is null) levels = new Sourced<int>(Math.Max(1, (int)MathF.Round((walls - plinth) / sh)), Source.Inferred);
            else sh = Math.Clamp((walls - plinth) / levels.Value, 2.4f, 6f);
        }
        else height = new Sourced<float>(plinth + levels.Value * sh + extra, Source.Inferred);
        return new BuildingPlan
        {
            Style = style.Id, Levels = levels, Height = height, Roof = roof, Palette = "", StoreyHeight = sh,
            RoofShape = shape, RoofRise = rise, RoofRect = rect,
            // a raised part (D19): its floor at min_height, and nothing below it (the plinth strip sits at the floor)
            Datum = c.Site.GroundHigh + (r.Raised ? minH : plinth), GroundLow = r.Raised ? c.Site.GroundHigh + minH : c.Site.GroundLow, Edges = c.Site.Edges,
        };
    }

    /// <summary>The bare mass (LOD2 and collision): the footprint as a prism from below the lowest ground to the roof.
    /// Up to the passage ceilings the footprint has each passage carved out of it, so the collision lets you through.</summary>
    private static void AddMass(Context c)
    {
        var mod = c.Building.Add(c.Id("mass"), "mass", ModuleTier.Primary, lods: 0b100);
        float bottom = c.Plan.GroundLow - 0.5f, top = c.RoofLevel + 0.6f;
        var outer = c.Site.Outer.ToList();
        var holes = c.Site.Holes.Select(h => (IReadOnlyList<Vector2>)h).ToList();
        float split = bottom;
        foreach (var p in c.Passages)
        {
            var hole = c.Site.Holes[c.Site.Edges[p.HoleEdge].Ring - 1];
            if (Carve(outer, hole, c.Site.Edges[p.HoleEdge].Index, p) is { } carved)
            {
                outer = carved;
                holes.Remove(hole);
                split = MathF.Max(split, p.Ceiling);
            }
            else c.Building.Report.Add(Severity.Warn, "mass.passage_not_carved", "A passage could not be cut out of the collision mass.", "solid mass");
        }
        if (split > bottom) Kit.Slab(mod.Mesh, "wall", outer, holes, bottom, split);
        Kit.Slab(mod.Mesh, "wall", c.Site.Outer, c.Site.Holes, split, top);
    }

    /// <summary>
    /// The outline with a passage cut through to a courtyard: walking the outer ring (counter-clockwise), at the passage
    /// it turns in along the left side, runs round the courtyard (clockwise, solid still on the left) and comes back out
    /// along the right side. The courtyard stops being a hole.
    /// </summary>
    private static List<Vector2>? Carve(List<Vector2> ring, List<Vector2> hole, int j, Passage p)
    {
        int k = -1;
        for (int i = 0; i < ring.Count && k < 0; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            if (Polygon.DistanceToSegment(p.LeftOut, a, b) < 0.01f && Polygon.DistanceToSegment(p.RightOut, a, b) < 0.01f
                && Vector2.Distance(a, p.LeftOut) < Vector2.Distance(a, p.RightOut)) k = i;
        }
        if (k < 0) return null;
        var ha = hole[j]; var hb = hole[(j + 1) % hole.Count];
        var hd = hb - ha;
        if (Vector2.Dot(p.LeftIn - ha, hd) <= Vector2.Dot(p.RightIn - ha, hd)) return null;
        var res = new List<Vector2>();
        for (int i = 0; i <= k; i++) res.Add(ring[i]);
        res.Add(p.LeftOut); res.Add(p.LeftIn);
        for (int i = 1; i <= hole.Count; i++) res.Add(hole[(j + i) % hole.Count]);
        res.Add(p.RightIn); res.Add(p.RightOut);
        for (int i = k + 1; i < ring.Count; i++) res.Add(ring[i]);
        return res;
    }

    private static void Validate(Context c)
    {
        var b = c.Building;
        int tris = b.TriangleCount;
        b.Report.Metrics["triangles"] = tris;
        b.Report.Metrics["modules"] = b.Modules.Count;
        b.Report.Metrics["entrances"] = b.Markers.Count(m => m.Kind == "entrance");
        var bounds = b.Bounds();
        b.Report.Metrics["height"] = Math.Round(bounds.Size.Y, 2);
        if (tris > 60000) b.Report.Add(Severity.Warn, "mesh.heavy", $"{tris} triangles: heavier than a streamed building should be.");
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z))
                {
                    b.Report.Add(Severity.Error, "mesh.nan", $"Module {m.Id} has a non-finite vertex.");
                    return;
                }
        b.Report.Metrics["passages"] = c.Passages.Count;
        // courtyard access (Noor): every courtyard must be reachable from outside, not only by helicopter
        for (int h = 0; h < c.Site.Holes.Count; h++)
        {
            if (!Passages.IsCourtyard(c, h, out _)) continue;
            if (c.Passages.Any(p => c.Site.Edges[p.HoleEdge].Ring == h + 1)) continue;
            var ring = c.Site.Holes[h];
            bool door = b.Markers.Any(m => m.Kind == "entrance"
                && ring.Select((q, i) => Polygon.DistanceToSegment(new Vector2(m.Position.X, m.Position.Z), q, ring[(i + 1) % ring.Count])).Min() < 0.5f);
            if (door) b.Report.Add(Severity.Warn, "courtyard.sealed", $"Courtyard {h + 1} has no passage: reachable only through a staircase.");
            else b.Report.Add(Severity.Error, "courtyard.unreachable", $"Courtyard {h + 1} has no passage and no door: nobody can get in.");
        }
        if (b.Plan != null && b.Plan.Edges.All(e => e.Exposure is Exposure.Party or Exposure.Sliver))
            b.Report.Add(Severity.Warn, "facade.enclosed", "Every wall is a party wall or faces a sliver: no windows at all.");
    }
}
