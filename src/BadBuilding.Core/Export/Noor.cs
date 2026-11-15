using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Export;

/// <summary>
/// Noor Interchange v0.2 (D14, D15; github.com/badsadcode/NoorEngine, docs/interchange/NOOR_INTERCHANGE.md): the asset's
/// stable identity, the vocabulary BadBuilding's parts and markers map onto, and the <c>&lt;name&gt;.noor.json</c>
/// manifest. Everything Noor-specific inside the GLB sits under <c>extras.noor</c> with <c>v = 1</c>.
/// </summary>
public static class Noor
{
    public const string Interchange = "0.2";
    public const int BlockVersion = 1;
    public const string Kind = "building";

    /// <summary>BadBuilding's fixed namespace for name-based (v5) asset ids (spec 4: one per tool, written in its docs):
    /// the same building id gives the same UUID everywhere. Never change it: every derived id depends on it.</summary>
    public static readonly Guid Namespace = new("6f1d6a52-2c1e-4b8e-9a43-5b0d2e7c9f10");

    /// <summary>
    /// The asset's identity: the request's own <see cref="BuildingRequest.AssetId"/> when given (e.g. read back from an
    /// earlier recipe), otherwise a UUID derived from the building id, so regenerating the same building keeps it.
    /// </summary>
    public static Guid AssetId(BuildingRequest r) => r.AssetId ?? UuidV5(Namespace, r.Id);

    /// <summary>RFC 4122 name-based UUID (SHA-1, version 5).</summary>
    public static Guid UuidV5(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray();
        SwapOrder(nsBytes);
        var data = nsBytes.Concat(Encoding.UTF8.GetBytes(name)).ToArray();
        var hash = SHA1.HashData(data);
        var b = new byte[16];
        Array.Copy(hash, b, 16);
        b[6] = (byte)((b[6] & 0x0F) | 0x50);
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        SwapOrder(b);
        return new Guid(b);
    }

    /// <summary>.NET stores the first three Guid fields little-endian; RFC 4122 is big-endian.</summary>
    private static void SwapOrder(byte[] g)
    {
        (g[0], g[3]) = (g[3], g[0]); (g[1], g[2]) = (g[2], g[1]);
        (g[4], g[5]) = (g[5], g[4]); (g[6], g[7]) = (g[7], g[6]);
    }

    /// <summary>Lower-case UUID text, as the schema wants it.</summary>
    public static string Text(Guid id) => id.ToString("D");

    /// <summary>
    /// The asset's pivot (spec section 2: bottom centre of the footprint, ground at y = 0): the centre of the outline's
    /// bounding box at the lowest ground under the walls. Building space minus this is asset space.
    /// </summary>
    public static Vector3 Pivot(Building b)
    {
        var pts = b.Request.Outer.Points;
        if (pts.Count == 0) return Vector3.Zero;
        float x = (pts.Min(p => p.X) + pts.Max(p => p.X)) * 0.5f, z = (pts.Min(p => p.Y) + pts.Max(p => p.Y)) * 0.5f;
        float y = b.Plan?.GroundLow ?? (b.Request.Ground.Count > 0 ? b.Request.Ground.Min() : 0f);
        return new Vector3(MathF.Round(x, 3), MathF.Round(y, 3), MathF.Round(z, 3));
    }

    /// <summary>Spec 3.5 part vocabulary for a module type (the original type stays in <c>props.type</c>).</summary>
    public static string? Part(string type) => type switch
    {
        "wall" or "passage" => "wall",
        "roof" => "roof",
        "entrance" or "shop_entrance" or "gate" or "wicket" => "door",
        "balconies" => "railing",
        "mass" => null,   // the collision node, not a part
        _ => "detail",    // facade_trim, panel_joints, roof_access and anything new
    };

    /// <summary>
    /// Spec 3.4 (v0.2): ways in are <c>entrance</c> markers with a kind (street door main, yard door courtyard, shop,
    /// wicket in a closed gate, roof access roof); the two ends of a passage through the ground floor are paired
    /// <c>passage</c> markers (kind gate for a brama, corridor for a walk-through) with the other end's id, the width
    /// and the clear height. No <c>door</c> markers yet: BadBuilding does not model openable leaves (D15).
    /// </summary>
    public static (string Marker, string Kind, Dictionary<string, object> Fields) MarkerOf(Building b, Marker m)
    {
        switch (m.Kind)
        {
            case "entrance": return ("entrance", FacesCourtyard(b, m) ? "courtyard" : "main", []);
            case "shop_entrance": return ("entrance", "shop", []);
            case "wicket": return ("entrance", "wicket", []);
            case "roof_access": return ("entrance", "roof", []);
            case "passage":
            {
                // ids "<building>/passage.e<edge>.street" and ".courtyard"; the passage module is "<building>/passage.e<edge>"
                int dot = m.Id.LastIndexOf('.');
                string baseId = m.Id[..dot], end = m.Id[(dot + 1)..];
                var fields = new Dictionary<string, object> { ["pair"] = baseId + (end == "street" ? ".courtyard" : ".street") };
                var module = b.Modules.FirstOrDefault(x => x.Id == baseId && x.Type == "passage");
                string kind = "gate";
                if (module?.Extras is { } ex)
                {
                    if (ex.TryGetValue("kind", out var k) && k as string != "gate") kind = "corridor";
                    if (ex.TryGetValue("width", out var w)) fields["width"] = w;
                    if (ex.TryGetValue("clear_height", out var h)) fields["height"] = h;
                }
                return ("passage", kind, fields);
            }
            default: return ("entrance", "main", []);
        }
    }

    /// <summary>A door on a courtyard wall is the service door; on the street it is the main one.</summary>
    private static bool FacesCourtyard(Building b, Marker m)
    {
        if (b.Plan == null || b.Plan.Edges.Count == 0) return false;
        var p = new Vector2(m.Position.X, m.Position.Z);
        var e = b.Plan.Edges.MinBy(e => Geometry.Polygon.DistanceToSegment(p, e.A, e.B))!;
        return e.Exposure == Exposure.Courtyard || e.Ring > 0;
    }

    public static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>One file of the export as the manifest lists it (<paramref name="Lod"/> for role <c>lod</c>).</summary>
    public sealed record FileEntry(string Path, string Role, string? Sha256, int? Lod = null);

    /// <summary>
    /// The manifest (<c>&lt;name&gt;.noor.json</c>, spec section 4). <paramref name="lodTriangles"/> are LOD0..2;
    /// The bounds are in asset space (the pivot at the origin).
    /// </summary>
    public static Dictionary<string, object?> Manifest(Building b, Guid id, string name, IReadOnlyList<FileEntry> files, string recipeFile,
        string recipeSha, IReadOnlyList<int> lodTriangles, DateTimeOffset created)
    {
        var bounds = b.Bounds();
        var pivot = Pivot(b);
        var markers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in b.Markers)
        {
            var (marker, _, _) = MarkerOf(b, m);
            markers[marker] = markers.GetValueOrDefault(marker) + 1;
        }
        var tags = new List<string>();
        if (b.Plan != null) { tags.Add(b.Plan.Style); tags.Add($"roof.{b.Plan.RoofShape.ToString().ToLowerInvariant()}"); }
        if (b.Request.Type is { Length: > 0 } t) tags.Add($"osm.{t}");
        var manifest = new Dictionary<string, object?>
        {
            ["noor_interchange"] = Interchange,
            ["id"] = Text(id),
            ["name"] = name,
            ["kind"] = Kind,
            ["generator"] = new Dictionary<string, object> { ["tool"] = "BadBuilding", ["version"] = BuildingGenerator.Version },
            ["recipe"] = new Dictionary<string, object> { ["file"] = recipeFile, ["seed"] = b.Request.Seed, ["sha256"] = recipeSha },
            ["created"] = created.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["space"] = new Dictionary<string, object> { ["up"] = "+Y", ["forward"] = "+Z", ["handedness"] = "right", ["units"] = "meters" },
            ["bounds"] = bounds.IsEmpty ? null : new Dictionary<string, object>
            {
                ["min"] = Round(bounds.Min - pivot), ["max"] = Round(bounds.Max - pivot),
            },
            ["files"] = files.Select(f =>
            {
                var d = new Dictionary<string, object> { ["path"] = f.Path, ["role"] = f.Role };
                if (f.Sha256 != null) d["sha256"] = f.Sha256;
                if (f.Lod != null) d["lod"] = f.Lod.Value;
                return d;
            }).ToList(),
            ["lods"] = lodTriangles.Select((n, i) => new Dictionary<string, object> { ["level"] = i, ["triangles"] = n }).ToList(),
            ["collision"] = b.Modules.Any(m => m.Type == "mass" && !m.Mesh.IsEmpty) ? "mesh" : "none",
            ["markers"] = markers,
            ["tags"] = tags,
            ["provenance"] = Provenance(b),
            ["validation"] = new Dictionary<string, object> { ["gltf_validator"] = "not_run" },
            // BadBuilding's own: where building (plan) space's origin sits, so callers that placed the footprint can put
            // the asset back (asset = building - pivot); the LOD files are separate GLBs for now (D14)
            ["badbuilding"] = new Dictionary<string, object?>
            {
                ["building_id"] = b.Request.Id,
                ["id_namespace"] = Text(Namespace),
                ["pivot_in_building_space"] = Round(pivot),
                ["status"] = b.Report.Status.ToString().ToLowerInvariant(),
            },
        };
        // absent, not null: the schema types every field it knows (a building with no plan has no bounds or provenance)
        foreach (var k in manifest.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) manifest.Remove(k);
        return manifest;
    }

    /// <summary>Which resolved values came from OSM, the request, or were guessed (D3).</summary>
    private static Dictionary<string, List<string>>? Provenance(Building b)
    {
        var p = b.Plan;
        if (p == null) return null;
        var d = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string what, Source s)
        {
            var key = s.ToString().ToLowerInvariant();
            if (!d.TryGetValue(key, out var l)) d[key] = l = [];
            l.Add(what);
        }
        Add("levels", p.Levels.Source);
        Add("height", p.Height.Source);
        Add("roof", p.Roof.Source);
        Add("palette", Source.Inferred);
        Add("style", b.Request.Style != null ? Source.Request : Source.Inferred);
        return d.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static float[] Round(Vector3 v) => [MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3)];
}
