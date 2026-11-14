using System.Numerics;
using BadBuilding.Geometry;

namespace BadBuilding.Model;

/// <summary>Primary parts make the mass (walls, roof), secondary ones are facade elements, tertiary ones small detail.</summary>
public enum ModuleTier { Primary, Secondary, Tertiary }

/// <summary>One named part of the building with its own mesh (building space: plan x, z and height y).</summary>
public sealed class Module
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public ModuleTier Tier { get; init; }
    public string? ParentId { get; init; }
    public Mesh Mesh { get; } = new();
    public Dictionary<string, object>? Extras { get; set; }
    /// <summary>Which levels of detail draw this part: bit 0 = LOD0 (full), bit 1 = LOD1, bit 2 = LOD2 (the bare mass).</summary>
    public int Lods { get; init; } = 0b011;
    public Aabb Bounds => Mesh.Bounds();
}

/// <summary>A functional point with a direction: doors, the roof access, the entrance to a stairwell.</summary>
public sealed record Marker(string Id, string Kind, Vector3 Position, Vector3 Direction);

/// <summary>A material by role, with linear colour, roughness and metalness (and opacity for glass).</summary>
public sealed record MaterialDef(string Role, Vector3 Color, float Roughness, float Metallic, float Alpha = 1f);

/// <summary>How a wall edge meets the world (D4).</summary>
public enum Exposure { Street, Courtyard, Side, Party, Sliver }

/// <summary>One edge of the outline, classified before any facade is built.</summary>
public sealed record WallEdge(int Ring, int Index, Vector2 A, Vector2 B, Exposure Exposure)
{
    public float Length => Vector2.Distance(A, B);
    /// <summary>Unit direction along the wall (A to B).</summary>
    public Vector2 Dir => Vector2.Normalize(B - A);
    /// <summary>Outward unit normal in plan (away from the building).</summary>
    public Vector2 Out => new(-Dir.Y, Dir.X);
}

/// <summary>Every choice the generator made, with where the facts came from (D3).</summary>
public sealed record BuildingPlan
{
    public required string Style { get; init; }
    public required Sourced<int> Levels { get; init; }
    public required Sourced<float> Height { get; init; }
    public required Sourced<string> Roof { get; init; }
    public required string Palette { get; init; }
    public float StoreyHeight { get; init; }
    /// <summary>Height of the ground floor (the datum, D5) and of the lowest ground under the walls.</summary>
    public float Datum { get; init; }
    public float GroundLow { get; init; }
    public IReadOnlyList<WallEdge> Edges { get; init; } = [];
}

/// <summary>The generated building: its parts, markers, materials, the plan and the validation report.</summary>
public sealed class Building
{
    public required BuildingRequest Request { get; init; }
    public BuildingPlan? Plan { get; set; }
    public List<Module> Modules { get; } = [];
    public List<Marker> Markers { get; } = [];
    public Dictionary<string, MaterialDef> Materials { get; } = new(StringComparer.Ordinal);
    public Validation.ValidationReport Report { get; } = new();

    public Module Add(string id, string type, ModuleTier tier, string? parent = null, int lods = 0b011)
    {
        var m = new Module { Id = id, Type = type, Tier = tier, ParentId = parent, Lods = lods };
        Modules.Add(m);
        return m;
    }

    public Aabb Bounds()
    {
        var b = Aabb.Empty;
        foreach (var m in Modules) b = b.Union(m.Bounds);
        return b;
    }

    public int TriangleCount => Modules.Sum(m => m.Mesh.TriangleCount);
}
