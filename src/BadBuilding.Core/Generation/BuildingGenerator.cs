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
        style.Materials(ctx);
        style.Build(ctx);
        AddMass(ctx);
        Validate(b);
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
                _ => throw new ArgumentException($"Unknown style '{forced}' (styles: {PrlBlock.Name}, {Plain.Name})."),
            };
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
        Sourced<float> height;
        if (r.Height is { } h and > 2f)
        {
            height = new Sourced<float>(h, src);
            // a mapped height without levels decides the storeys
            if (r.Levels is null) levels = new Sourced<int>(Math.Max(1, (int)MathF.Round((h - plinth) / sh)), Source.Inferred);
            else sh = Math.Clamp((h - plinth) / levels.Value, 2.4f, 6f);
        }
        else height = new Sourced<float>(plinth + levels.Value * sh + style.RoofExtra, Source.Inferred);
        Sourced<string> roof;
        if (r.Roof is { Length: > 0 } rs)
        {
            roof = new Sourced<string>(rs, src);
            if (rs != "flat")
                c.Building.Report.Add(Severity.Info, "roof.not_supported", $"Mapped roof '{rs}' is drawn flat in version {Version}.", "flat roof");
        }
        else roof = new Sourced<string>("flat", Source.Inferred);
        return new BuildingPlan
        {
            Style = style.Id, Levels = levels, Height = height, Roof = roof, Palette = "", StoreyHeight = sh,
            Datum = c.Site.GroundHigh + plinth, GroundLow = c.Site.GroundLow, Edges = c.Site.Edges,
        };
    }

    /// <summary>The bare mass (LOD2 and collision): the footprint as a prism from below the lowest ground to the roof.</summary>
    private static void AddMass(Context c)
    {
        var mod = c.Building.Add(c.Id("mass"), "mass", ModuleTier.Primary, lods: 0b100);
        Kit.Slab(mod.Mesh, "wall", c.Site.Outer, c.Site.Holes, c.Plan.GroundLow - 0.5f, c.RoofLevel + 0.6f);
    }

    private static void Validate(Building b)
    {
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
        if (b.Plan != null && b.Plan.Edges.All(e => e.Exposure is Exposure.Party or Exposure.Sliver))
            b.Report.Add(Severity.Warn, "facade.enclosed", "Every wall is a party wall or faces a sliver: no windows at all.");
    }
}
