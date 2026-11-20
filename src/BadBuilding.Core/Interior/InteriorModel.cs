using System.Numerics;
using BadBuilding.Geometry;
using BadBuilding.Model;
using System.Globalization;
using System.Text;
using BadBuilding.Validation;

namespace BadBuilding.Interior;

/// <summary>The interior's own frame (D33): u along the entrance wall, v into the building from it, in plan.</summary>
public readonly record struct Frame(Vector2 Origin, Vector2 U, Vector2 In)
{
    public Vector2 Plan(float u, float v) => Origin + U * u + In * v;
    public Vector3 P(float u, float y, float v) { var p = Plan(u, v); return new Vector3(p.X, y, p.Y); }
    public Vector3 AxisU => new(U.X, 0, U.Y);
    public Vector3 AxisV => new(In.X, 0, In.Y);
    public (float U, float V) Local(Vector2 p) { var d = p - Origin; return (Vector2.Dot(d, U), Vector2.Dot(d, In)); }
}

/// <summary>A solid box for the game's physics, in building space: its centre, half sizes along its own axes, and
/// two of those axes (x across, y up; z = x × y). Stair flights are tilted boxes, everything else upright.</summary>
public sealed record Collider(Vector3 Centre, Vector3 Half, Vector3 AxisX, Vector3 AxisY);

/// <summary>A room: one or more rectangles in the interior frame (u, v, min and max), on a storey, in a flat or the
/// staircase. <see cref="Centre"/> is in building space, at the floor.</summary>
public sealed record Room(string Id, string Kind, int Storey, string? Flat, IReadOnlyList<(Vector2 Min, Vector2 Max)> Rects, float Floor, Vector3 Centre)
{
    public float Area => Rects.Sum(r => (r.Max.X - r.Min.X) * (r.Max.Y - r.Min.Y));
}

/// <summary>An opening in an outer wall as a room sees it (D38): on a storey, on the line of the wall's inner face
/// (v when <see cref="AlongU"/>, else u), from A0 to A1 along it, Y0 to Y1 high; kind as the facade made it ("glass",
/// "glass_block", "door", "balcony_door", "shut", "loggia"). A loggia's <see cref="Back"/> is the line of its glass
/// back wall (it cuts into the room up to there); for the rest it equals <see cref="Line"/>.</summary>
public sealed record Aperture(int Storey, bool AlongU, float Line, float A0, float A1, float Y0, float Y1, string Kind, float Back);

/// <summary>A piece of furniture (D38): its kind, room, footprint in the frame (u, v), height, the way its front faces
/// (in the frame: one of ±u, ±v), and the floor it stands on.</summary>
public sealed record FurnitureItem(string Id, string Kind, string Room, Vector2 Min, Vector2 Max, float Height, Vector2 Facing, float Floor);

/// <summary>A doorway between two rooms (or "outside"), building space: the middle of its sill, the direction it is
/// passed through, its clear width and height.</summary>
public sealed record Doorway(string Id, string From, string To, Vector3 Position, Vector3 Normal, float Width, float Height);

/// <summary>A generated interior (D33): one mesh by material role, physics boxes, rooms, doorways and markers, all in
/// the same building space as the exterior it was made for.</summary>
public sealed class InteriorModel
{
    public required string Id { get; init; }
    public required Frame Frame { get; init; }
    public required IReadOnlyList<Vector2> Footprint { get; init; }
    public float Floor { get; init; }
    public float Top { get; init; }
    public float StoreyHeight { get; init; }
    public int Levels { get; init; }
    public Mesh Mesh { get; } = new();
    public List<Collider> Colliders { get; } = [];
    public List<Room> Rooms { get; } = [];
    public List<Doorway> Doors { get; } = [];
    public List<Marker> Markers { get; } = [];
    public List<Aperture> Apertures { get; } = [];
    public List<FurnitureItem> Furniture { get; } = [];
    public ulong Seed { get; init; }
    public List<EntranceDoor> Entrances { get; } = [];
    /// <summary>Balconies you can step out onto (D35), with the storey's floor height in their position.</summary>
    public List<BalconySite> Balconies { get; } = [];
    /// <summary>Entrances the plan could not use: the door stays shut for good (D11's fallback).</summary>
    public List<EntranceDoor> Sealed { get; } = [];
    public Dictionary<string, MaterialDef> Materials { get; } = new(StringComparer.Ordinal);
    public ValidationReport Report { get; } = new();

    /// <summary>The plan as JSON (rooms as rectangles in the frame, doorways, the footprint, the openings by side, the
    /// report): for the CLI's plan drawings and a game's debug dumps.</summary>
    public string PlanJson(string style)
    {
        string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append("{\"style\":\"").Append(style).Append("\",\"levels\":").Append(Levels);
        sb.Append(",\"frame\":[").Append(F(Frame.Origin.X)).Append(',').Append(F(Frame.Origin.Y)).Append(',').Append(F(Frame.U.X)).Append(',').Append(F(Frame.U.Y)).Append(']');
        sb.Append(",\"footprint\":[").Append(string.Join(",", Footprint.Select(p => { var (u, v) = Frame.Local(p); return $"[{F(u)},{F(v)}]"; }))).Append(']');
        sb.Append(",\"rooms\":[").Append(string.Join(",", Rooms.Select(r => $"{{\"id\":\"{r.Id}\",\"kind\":\"{r.Kind}\",\"storey\":{r.Storey},\"rects\":[{string.Join(",", r.Rects.Select(q => $"[{F(q.Min.X)},{F(q.Min.Y)},{F(q.Max.X)},{F(q.Max.Y)}]"))}]}}"))).Append(']');
        sb.Append(",\"doors\":[").Append(string.Join(",", Doors.Select(d => { var (u, v) = Frame.Local(new Vector2(d.Position.X, d.Position.Z)); return $"{{\"storey\":{StoreyAt(d.Position.Y + 0.1f)},\"u\":{F(u)},\"v\":{F(v)},\"w\":{F(d.Width)},\"along\":{(MathF.Abs(Vector3.Dot(d.Normal, Frame.AxisV)) > 0.5f ? "true" : "false")}}}"; }))).Append(']');
        sb.Append(",\"report\":\"").Append(string.Join("; ", Report.Issues.Select(i => i.Code + " " + i.Message)).Replace("\"", "'")).Append("\"");
        sb.Append(",\"metrics\":{").Append(string.Join(",", Report.Metrics.Select(kv => $"\"{kv.Key}\":{F((float)kv.Value)}"))).Append("}");
        sb.Append(",\"furniture\":[").Append(string.Join(",", Furniture.Select(x => $"[{StoreyAt(x.Floor + 0.1f)},{F(x.Min.X)},{F(x.Min.Y)},{F(x.Max.X)},{F(x.Max.Y)},\"{x.Kind}\"]"))).Append(']');
        sb.Append(",\"openings\":[").Append(string.Join(",", Openings.Select(o => $"[{o.Side},{F(o.A0)},{F(o.A1)},{F(o.Y0)},{F(o.Y1)},\"{o.Kind}\"]"))).Append(']');
        return sb.Append('}').ToString();
    }

    /// <summary>The facade's openings as the plan used them: side 0 the entrance wall, 1 the far wall, 2 and 3 the
    /// ends (lower and higher u); along u (0, 1) or v (2, 3).</summary>
    public List<(int Side, float A0, float A1, float Y0, float Y1, string Kind)> Openings { get; } = [];

    /// <summary>Which storey a height is on (clamped to the building's).</summary>
    public int StoreyAt(float y) => Math.Clamp((int)MathF.Floor((y - Floor + 0.3f) / StoreyHeight), 0, Math.Max(0, Levels - 1));

    /// <summary>The room a point (building space) is in, or null.</summary>
    public Room? RoomAt(Vector3 p)
    {
        int s = StoreyAt(p.Y);
        var (u, v) = Frame.Local(new Vector2(p.X, p.Z));
        foreach (var r in Rooms)
            if (r.Storey == s && r.Rects.Any(q => u >= q.Min.X && u <= q.Max.X && v >= q.Min.Y && v <= q.Max.Y)) return r;
        return null;
    }
}
