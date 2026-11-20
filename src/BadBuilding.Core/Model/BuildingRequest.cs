using System.Numerics;

namespace BadBuilding.Model;

/// <summary>Where a value came from (D3): mapped in OSM, given by the caller, or chosen by the generator.</summary>
public enum Source { Osm, Request, Inferred }

/// <summary>A value and where it came from.</summary>
public readonly record struct Sourced<T>(T Value, Source Source);

/// <summary>A footprint ring in plan, metres, +X east, +Z south (D1). The last point is not repeated.</summary>
public sealed record Ring(IReadOnlyList<Vector2> Points)
{
    /// <summary>For a neighbour that is another part of the same building: how high it starts and ends (m above the
    /// ground, like <see cref="BuildingRequest.MinHeight"/> and <see cref="BuildingRequest.Height"/>). A part only makes
    /// a party wall where it covers this one's height: a podium doesn't blind the tower over it (D20). Null for
    /// separate buildings: there a lower neighbour leaves a bare firewall above it, as in any Warsaw street.</summary>
    public float? PartMin { get; init; }
    public float? PartTop { get; init; }
    /// <summary>For a separate neighbouring building: its height when known (D31: a long wall against a much lower
    /// neighbour, like a shop pavilion along a block's front, is a facade above it, not a firewall).</summary>
    public float? Top { get; init; }
}

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

    /// <summary>The asset's stable identity (Noor Interchange, D14). Null: derived from <see cref="Id"/>, or kept from
    /// an earlier recipe when the files are saved over an earlier export.</summary>
    public Guid? AssetId { get; init; }

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
    /// <summary>OSM roof:height (m, eaves to ridge), when mapped.</summary>
    public float? RoofHeight { get; init; }
    /// <summary>A part that starts in the air (OSM <c>min_height</c>, D19): its walls begin this far above the ground
    /// (<see cref="Height"/> stays the height of its top); no plinth, no lobby, no passages; a closed underside.</summary>
    public float? MinHeight { get; init; }
    public bool Raised => MinHeight is > 0.05f;
    public string? HouseNumber { get; init; }
    public IReadOnlyList<Entrance> Entrances { get; init; } = [];

    /// <summary>OSM start_date (year built), when mapped. Evidence for a style's age (D10); missing is not evidence.</summary>
    public int? StartYear { get; init; }
    /// <summary>Listed or marked historic in OSM (heritage=*, historic=building).</summary>
    public bool Heritage { get; init; }
    /// <summary>OSM building:architecture, when mapped.</summary>
    public string? Architecture { get; init; }
    /// <summary>The wall material when known ("brick", "sandstone", "stone", "concrete", "wood", "glass"…); evidence for
    /// a style (D31: sandstone on a big block of the early fifties' size is socialist realism).</summary>
    public string? Material { get; init; }
    /// <summary>OSM religion and denomination, when mapped: evidence for a place of worship's style (D41: an orthodox
    /// church is a cerkiew).</summary>
    public string? Religion { get; init; }
    public string? Denomination { get; init; }
    /// <summary>A regional or alternate-world preset that changes which styles are chosen (cookbook part D), such as
    /// "brutalist" (D32: the Brutalist Warsaw that never was). Null: the styles go by the evidence.</summary>
    public string? Preset { get; init; }
    /// <summary>Mapped shops (shop=* and shop-like amenity nodes) in or at the building, plan points. Null = not known;
    /// when given, they decide where shop fronts go instead of a guess (D10).</summary>
    public IReadOnlyList<Vector2>? Shops { get; init; }
    /// <summary>The building itself carries a shop=* tag.</summary>
    public bool ShopOnBuilding { get; init; }

    /// <summary>Footprints of nearby buildings (outer rings): party walls and slivers (D4).</summary>
    public IReadOnlyList<Ring> Neighbours { get; init; } = [];
    public IReadOnlyList<StreetLine> Streets { get; init; } = [];

    /// <summary>Ground height (m) at each point of <see cref="Outer"/>, in order; empty = flat ground at 0 (D5).</summary>
    public IReadOnlyList<float> Ground { get; init; } = [];

    /// <summary>Where the facts came from: true when they are mapped OSM data (else "request").</summary>
    public bool FromOsm { get; init; } = true;

    /// <summary>Force a style ("prl_block", "kamienica", "plain"), otherwise chosen from the facts.</summary>
    public string? Style { get; init; }

    /// <summary>Replaces the seed for one stage only ("plan", "facade", "detail", "look"): reroll that stage, keep the rest.</summary>
    public IReadOnlyDictionary<string, ulong> StageSeeds { get; init; } = new Dictionary<string, ulong>();
}
