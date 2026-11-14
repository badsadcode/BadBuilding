using System.Numerics;

namespace BadBuilding.Model;

/// <summary>Where a value came from (D3): mapped in OSM, given by the caller, or chosen by the generator.</summary>
public enum Source { Osm, Request, Inferred }

/// <summary>A value and where it came from.</summary>
public readonly record struct Sourced<T>(T Value, Source Source);

/// <summary>A footprint ring in plan, metres, +X east, +Z south (D1). The last point is not repeated.</summary>
public sealed record Ring(IReadOnlyList<Vector2> Points);

/// <summary>A street centre line near the building (plan points), with its half width: decides which walls face the street.</summary>
public sealed record StreetLine(IReadOnlyList<Vector2> Points, float HalfWidth = 4f);

/// <summary>A door mapped on the outline (OSM entrance=* nodes): where it is and what kind.</summary>
public sealed record Entrance(Vector2 Position, string Kind = "main");

/// <summary>
/// Everything known about one building. Only <see cref="Outer"/> and <see cref="Seed"/> are required; every other fact
/// is optional and, when missing, inferred (and marked so in the result, D3). Context (neighbours, streets, ground)
/// is what lets the facade make sense on its street (D4, D5).
/// </summary>
public sealed record BuildingRequest
{
    /// <summary>Identifies the building (e.g. "osm:w123456"): ids of generated parts start with it.</summary>
    public string Id { get; init; } = "building";

    /// <summary>The seed (e.g. the OSM id): the same request and seed give the same building.</summary>
    public required ulong Seed { get; init; }

    public required Ring Outer { get; init; }
    public IReadOnlyList<Ring> Holes { get; init; } = [];

    /// <summary>OSM building=* (house, apartments, residential, yes, retail, ...).</summary>
    public string? Type { get; init; }
    public int? Levels { get; init; }
    public float? Height { get; init; }
    /// <summary>OSM roof:shape (flat, gabled, hipped, ...).</summary>
    public string? Roof { get; init; }
    public string? HouseNumber { get; init; }
    public IReadOnlyList<Entrance> Entrances { get; init; } = [];

    /// <summary>Footprints of nearby buildings (outer rings): party walls and slivers (D4).</summary>
    public IReadOnlyList<Ring> Neighbours { get; init; } = [];
    public IReadOnlyList<StreetLine> Streets { get; init; } = [];

    /// <summary>Ground height (m) at each point of <see cref="Outer"/>, in order; empty = flat ground at 0 (D5).</summary>
    public IReadOnlyList<float> Ground { get; init; } = [];

    /// <summary>Where the facts came from: true when they are mapped OSM data (else "request").</summary>
    public bool FromOsm { get; init; } = true;

    /// <summary>Force a style ("prl_block", "plain"), otherwise chosen from the facts.</summary>
    public string? Style { get; init; }

    /// <summary>Replaces the seed for one stage only ("plan", "facade", "detail", "look"): reroll that stage, keep the rest.</summary>
    public IReadOnlyDictionary<string, ulong> StageSeeds { get; init; } = new Dictionary<string, ulong>();
}
