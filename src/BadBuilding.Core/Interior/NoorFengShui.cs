using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Interior;

/// <summary>
/// NoorFengShui (Tom and Noor, 20 Nov 2026; D38): furniture for the flats, placed the way people place it. Every room
/// gets a programme by its kind (a bedroom: bed, bedside tables, wardrobe, desk; a kitchen: fridge, a counter with
/// sink and cooker, a table...), and every piece obeys a few rules:
/// <list type="bullet">
/// <item>it stands against a wall, its back to it (tables and the coffee table excepted);</item>
/// <item>never in a doorway or the swing in front of one, a balcony door or a loggia's glass;</item>
/// <item>tall pieces (wardrobes, fridges, shelves) never in front of a window; low ones only under a sill high enough;
/// the head of a bed never under a window;</item>
/// <item>the TV faces the sofa, the coffee table sits between them, the sink goes under the kitchen window if there is
/// one, the desk by a window;</item>
/// <item>and after each piece the floor is flooded at walking width: every door must still reach every other door and
/// the front of every piece you'd use. A piece that would cut the room off is not placed.</item>
/// </list>
/// The pieces are simple generated shapes in seeded colours, each one also a physics box and, where something could
/// be kept in it (drawers, wardrobes, the fridge, cupboards, shelves), a "loot" marker for a game.
/// </summary>
public static class NoorFengShui
{
    public const string Version = "0.1.0";

    private const float Grid = 0.1f, Body = 0.3f;          // the flood: cell size, half the walking width (with the wall gap)
    private const float WallGap = 0.08f;                   // pieces stand this far in from the walls' centre lines

    private enum Block { Door, Window }
    private sealed record EdgeBlock(float A0, float A1, Block Kind, float Sill);
    private sealed record Rect(float U0, float V0, float U1, float V1)
    {
        public bool Overlaps(Rect o, float gap = 0f) => U0 < o.U1 + gap && o.U0 < U1 + gap && V0 < o.V1 + gap && o.V0 < V1 + gap;
        public Rect Grow(float g) => new(U0 - g, V0 - g, U1 + g, V1 + g);
        public bool Contains(float u, float v) => u >= U0 && u <= U1 && v >= V0 && v <= V1;
        public Vector2 Centre => new((U0 + U1) * 0.5f, (V0 + V1) * 0.5f);
        public float W => U1 - U0;
        public float H => V1 - V0;
    }

    /// <summary>A piece placed in a room: its footprint, height, front direction, and whether its front must stay reachable.</summary>
    private sealed record Spot(string Kind, Rect R, float H, Vector2 Facing, int Edge, bool Access);

    private sealed class RoomWork
    {
        public required Room Room;
        public required Rect Raw, Area;
        public readonly List<EdgeBlock>[] Edges = [[], [], [], []];
        public readonly List<Rect> Keep = [];
        public readonly List<Vector2> Doors = [];
        public readonly List<Spot> Placed = [];
        public required Rng Rng;
        public float Floor => Room.Floor;
        public float EdgeLine(int e) => e switch { 0 => Area.V0, 1 => Area.V1, 2 => Area.U0, _ => Area.U1 };
        public (float Lo, float Hi) EdgeSpan(int e) => e < 2 ? (Area.U0, Area.U1) : (Area.V0, Area.V1);
        public static Vector2 Facing(int e) => e switch { 0 => new(0, 1), 1 => new(0, -1), 2 => new(1, 0), _ => new(-1, 0) };
    }

    /// <summary>Furnishes every flat's rooms in the model (not the staircases).</summary>
    public static void Furnish(InteriorModel m)
    {
        Materials(m);
        int n = 0;
        foreach (var room in m.Rooms)
        {
            if (room.Kind == "stair") continue;
            var w = Prepare(m, room);
            if (w == null) continue;
            switch (room.Kind)
            {
                case "bedroom": Bedroom(w); break;
                case "living": Living(w); break;
                case "kitchen": Kitchen(w); break;
                case "bathroom": Bathroom(w); break;
                case "hall": Hall(w); break;
                case "storage": Storage(w); break;
            }
            foreach (var p in w.Placed) Build(m, w, p, n++);
        }
        m.Report.Metrics["furniture"] = m.Furniture.Count;
    }

    // ---- the room: its floor, walls, doors and windows ---------------------------------------------------------

    private static RoomWork? Prepare(InteriorModel m, Room room)
    {
        var q = room.Rects[0];
        var raw = new Rect(q.Min.X, q.Min.Y, q.Max.X, q.Max.Y);
        var area = raw.Grow(-WallGap);
        if (area.W < 1.0f || area.H < 1.0f) return null;
        var w = new RoomWork { Room = room, Raw = raw, Area = area, Rng = Rng.Stream(m.Seed ^ Rng.Fnv1a(room.Id), "fengshui") };
        float RawLine(int e) => e switch { 0 => raw.V0, 1 => raw.V1, 2 => raw.U0, _ => raw.U1 };

        // doorways on this room's edges: the opening, the swing in front, a point to reach
        foreach (var d in m.Doors)
        {
            if (d.From != room.Id && d.To != room.Id) continue;
            var (u, v) = m.Frame.Local(new Vector2(d.Position.X, d.Position.Z));
            bool alongU = MathF.Abs(Vector3.Dot(d.Normal, m.Frame.AxisV)) > 0.5f;
            for (int e = 0; e < 4; e++)
            {
                if (alongU != e < 2) continue;
                float line = RawLine(e), across = alongU ? v : u, along = alongU ? u : v;
                var (lo, hi) = e < 2 ? (raw.U0, raw.U1) : (raw.V0, raw.V1);
                if (MathF.Abs(across - line) > 0.15f || along < lo - 0.05f || along > hi + 0.05f) continue;
                AddOpening(w, e, along - d.Width * 0.5f, along + d.Width * 0.5f, d.Width + 0.15f);
            }
        }
        // the outer wall's openings: windows, balcony doors, a loggia's glass
        foreach (var ap in m.Apertures)
        {
            if (ap.Storey != room.Storey) continue;
            for (int e = 0; e < 4; e++)
            {
                if (ap.AlongU != e < 2 || MathF.Abs(ap.Line - RawLine(e)) > 0.06f) continue;
                var (lo, hi) = e < 2 ? (raw.U0, raw.U1) : (raw.V0, raw.V1);
                if (ap.A1 <= lo || ap.A0 >= hi) continue;
                // an opening mostly in the next room only blocks this room's bit of wall (D38: it counted as a door
                // whose front was outside the room, and nothing could be placed)
                float c0 = MathF.Max(ap.A0, lo), c1 = MathF.Min(ap.A1, hi);
                if (ap.Kind is not ("glass" or "glass_block" or "shut") && c1 - c0 < 0.7f) { w.Edges[e].Add(new EdgeBlock(c0, c1, Block.Door, 0)); continue; }
                switch (ap.Kind)
                {
                    case "glass" or "glass_block" or "shut":
                        w.Edges[e].Add(new EdgeBlock(ap.A0, ap.A1, Block.Window, ap.Y0 - room.Floor));
                        break;
                    case "loggia":
                    {
                        // the loggia cuts into the room up to its glass; keep that band and the space before the glass free
                        float deep = MathF.Abs(ap.Back - ap.Line) + 1.0f;
                        w.Keep.Add(Band(w, e, ap.A0 - 0.1f, ap.A1 + 0.1f, deep));
                        w.Edges[e].Add(new EdgeBlock(ap.A0, ap.A1, Block.Door, 0));
                        w.Doors.Add(Front(w, e, (c0 + c1) * 0.5f, MathF.Abs(ap.Back - ap.Line) + 0.45f));
                        break;
                    }
                    default:   // a door or a balcony door
                        AddOpening(w, e, ap.A0, ap.A1, 1.1f, (c0 + c1) * 0.5f);
                        break;
                }
            }
        }
        return w;
    }

    private static void AddOpening(RoomWork w, int e, float a0, float a1, float swing, float? mid = null)
    {
        w.Edges[e].Add(new EdgeBlock(a0, a1, Block.Door, 0));
        w.Keep.Add(Band(w, e, a0 - 0.1f, a1 + 0.1f, swing));
        w.Doors.Add(Front(w, e, mid ?? (a0 + a1) * 0.5f, 0.45f));
    }

    /// <summary>A strip along edge <paramref name="e"/> from <paramref name="a0"/> to <paramref name="a1"/>,
    /// <paramref name="deep"/> into the room.</summary>
    private static Rect Band(RoomWork w, int e, float a0, float a1, float deep) => e switch
    {
        0 => new Rect(a0, w.Raw.V0 - 0.1f, a1, w.Raw.V0 + deep),
        1 => new Rect(a0, w.Raw.V1 - deep, a1, w.Raw.V1 + 0.1f),
        2 => new Rect(w.Raw.U0 - 0.1f, a0, w.Raw.U0 + deep, a1),
        _ => new Rect(w.Raw.U1 - deep, a0, w.Raw.U1 + 0.1f, a1),
    };

    private static Vector2 Front(RoomWork w, int e, float along, float deep) => e switch
    {
        0 => new(along, w.Raw.V0 + deep),
        1 => new(along, w.Raw.V1 - deep),
        2 => new(w.Raw.U0 + deep, along),
        _ => new(w.Raw.U1 - deep, along),
    };

    // ---- placing ------------------------------------------------------------------------------------------------

    /// <summary>A piece of <paramref name="width"/> along the wall, <paramref name="depth"/> into the room: tall (keeps
    /// clear of windows), or not under a window at all (a bed's head); the best spot by <paramref name="score"/>.</summary>
    private static Spot? AgainstWall(RoomWork w, string kind, float width, float depth, float h, Func<int, Rect, float> score,
        bool tall = false, bool noWindow = false, bool access = true, int onlyEdge = -1)
    {
        var cands = new List<(float S, Spot P)>();
        for (int e = 0; e < 4; e++)
        {
            if (onlyEdge >= 0 && e != onlyEdge) continue;
            var (lo, hi) = w.EdgeSpan(e);
            float across = e < 2 ? w.Area.H : w.Area.W;
            if (hi - lo < width || across < depth + 0.6f) continue;
            for (float a = lo; a <= hi - width + 1e-3f; a += 0.1f)
            {
                float a0 = a, a1 = a + width;
                if (w.Edges[e].Any(b => b.A1 > a0 && b.A0 < a1 && (b.Kind == Block.Door || tall || noWindow || h > b.Sill + 0.02f))) continue;
                var r = e switch
                {
                    0 => new Rect(a0, w.Area.V0, a1, w.Area.V0 + depth),
                    1 => new Rect(a0, w.Area.V1 - depth, a1, w.Area.V1),
                    2 => new Rect(w.Area.U0, a0, w.Area.U0 + depth, a1),
                    _ => new Rect(w.Area.U1 - depth, a0, w.Area.U1, a1),
                };
                if (w.Keep.Any(k => k.Overlaps(r)) || w.Placed.Any(p => p.R.Overlaps(r, 0.02f))) continue;
                cands.Add((score(e, r) + (float)w.Rng.NextDouble() * 0.3f, new Spot(kind, r, h, RoomWork.Facing(e), e, access)));
            }
        }
        return Take(w, cands);
    }

    /// <summary>A free-standing piece (a table): its footprint centred anywhere it fits, best by <paramref name="score"/>.</summary>
    private static Spot? FreeStanding(RoomWork w, string kind, float width, float depth, float h, Func<Rect, float> score, Vector2? facing = null)
    {
        var cands = new List<(float S, Spot P)>();
        for (float u = w.Area.U0 + width * 0.5f; u <= w.Area.U1 - width * 0.5f + 1e-3f; u += 0.15f)
            for (float v = w.Area.V0 + depth * 0.5f; v <= w.Area.V1 - depth * 0.5f + 1e-3f; v += 0.15f)
            {
                var r = new Rect(u - width * 0.5f, v - depth * 0.5f, u + width * 0.5f, v + depth * 0.5f);
                if (w.Keep.Any(k => k.Overlaps(r)) || w.Placed.Any(p => p.R.Overlaps(r, 0.05f))) continue;
                cands.Add((score(r) + (float)w.Rng.NextDouble() * 0.2f, new Spot(kind, r, h, facing ?? new Vector2(0, 1), -1, false)));
            }
        return Take(w, cands);
    }

    /// <summary>The best candidate that keeps the room walkable (tries the best few).</summary>
    private static Spot? Take(RoomWork w, List<(float S, Spot P)> cands)
    {
        foreach (var (_, p) in cands.Where(c => c.S > -100f).OrderByDescending(c => c.S).Take(12))
        {
            w.Placed.Add(p);
            if (Walkable(w)) return p;
            w.Placed.RemoveAt(w.Placed.Count - 1);
        }
        return null;
    }

    /// <summary>The flood: free floor at walking width; every door, and the front of every piece that needs it, must
    /// be reachable from the first door.</summary>
    private static bool Walkable(RoomWork w)
    {
        var a = w.Area;
        int nx = Math.Max(1, (int)MathF.Ceiling(a.W / Grid)), ny = Math.Max(1, (int)MathF.Ceiling(a.H / Grid));
        var free = new bool[nx, ny];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                float u = a.U0 + (i + 0.5f) * Grid, v = a.V0 + (j + 0.5f) * Grid;
                free[i, j] = u > a.U0 + Body - 0.05f && u < a.U1 - Body + 0.05f && v > a.V0 + Body - 0.05f && v < a.V1 - Body + 0.05f;
            }
        // each piece, grown by half the walking width, rasterised (cells whose centre it covers)
        foreach (var p in w.Placed)
        {
            var g = p.R.Grow(Body);
            int i0 = Math.Max(0, (int)MathF.Ceiling((g.U0 - a.U0) / Grid - 0.5f)), i1 = Math.Min(nx - 1, (int)MathF.Floor((g.U1 - a.U0) / Grid - 0.5f));
            int j0 = Math.Max(0, (int)MathF.Ceiling((g.V0 - a.V0) / Grid - 0.5f)), j1 = Math.Min(ny - 1, (int)MathF.Floor((g.V1 - a.V0) / Grid - 0.5f));
            for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) free[i, j] = false;
        }
        (int, int)? Cell(Vector2 p)
        {
            int i = Math.Clamp((int)((p.X - a.U0) / Grid), 0, nx - 1), j = Math.Clamp((int)((p.Y - a.V0) / Grid), 0, ny - 1);
            // the nearest free cell within 0.25 m (a point right at the edge of the walkable floor still counts)
            for (int r = 0; r <= 2; r++)
                for (int di = -r; di <= r; di++)
                    for (int dj = -r; dj <= r; dj++)
                    {
                        int x = i + di, y = j + dj;
                        if (x >= 0 && y >= 0 && x < nx && y < ny && free[x, y]) return (x, y);
                    }
            return null;
        }
        var targets = new List<Vector2>(w.Doors);
        foreach (var p in w.Placed.Where(p => p.Access))
            targets.Add(p.R.Centre + p.Facing * ((p.Edge < 2 ? p.R.H : p.R.W) * 0.5f + 0.4f));
        if (targets.Count == 0) return true;
        var cells = targets.Select(Cell).ToList();
        if (cells.Any(c => c == null)) return false;
        var seen = new bool[nx, ny];
        var q = new Queue<(int, int)>();
        q.Enqueue(cells[0]!.Value); seen[cells[0]!.Value.Item1, cells[0]!.Value.Item2] = true;
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue();
            foreach (var (x, y) in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                if (x >= 0 && y >= 0 && x < nx && y < ny && free[x, y] && !seen[x, y]) { seen[x, y] = true; q.Enqueue((x, y)); }
        }
        return cells.All(c => seen[c!.Value.Item1, c.Value.Item2]);
    }

    // ---- scoring helpers ------------------------------------------------------------------------------------------

    private static float NearestDoor(RoomWork w, Rect r) => w.Doors.Count == 0 ? 3f : w.Doors.Min(d => Vector2.Distance(d, r.Centre));
    private static bool EdgeHasDoor(RoomWork w, int e) => w.Edges[e].Any(b => b.Kind == Block.Door);
    private static bool EdgeHasWindow(RoomWork w, int e) => w.Edges[e].Any(b => b.Kind == Block.Window);
    private static float WindowNear(RoomWork w, int e, Rect r)
    {
        float mid = e < 2 ? (r.U0 + r.U1) * 0.5f : (r.V0 + r.V1) * 0.5f;
        var win = w.Edges[e].Where(b => b.Kind == Block.Window).Select(b => MathF.Abs((b.A0 + b.A1) * 0.5f - mid)).DefaultIfEmpty(99f).Min();
        return win;
    }
    private static float Corner(RoomWork w, int e, Rect r)
    {
        var (lo, hi) = w.EdgeSpan(e);
        float a0 = e < 2 ? r.U0 : r.V0, a1 = e < 2 ? r.U1 : r.V1;
        return MathF.Min(a0 - lo, hi - a1) < 0.06f ? 1f : 0f;
    }

    // ---- programmes -----------------------------------------------------------------------------------------------

    private static void Bedroom(RoomWork w)
    {
        bool wide = MathF.Min(w.Area.W, w.Area.H) >= 3.0f;
        float bw = wide ? 1.6f : 0.95f;
        // the bed: head against a wall without a window, best opposite the door, not right by it
        var bed = AgainstWall(w, wide ? "bed_double" : "bed_single", bw, 2.05f, 0.6f,
            (e, r) => NearestDoor(w, r) * 0.6f + (EdgeHasDoor(w, e) ? -1.5f : 0f) - Corner(w, e, r) * (wide ? 0.8f : -0.6f), noWindow: true);
        if (bed != null)
        {
            // a bedside table on each side it fits
            foreach (int sideSign in new[] { -1, 1 })
            {
                float a = sideSign < 0 ? (bed.Edge < 2 ? bed.R.U0 - 0.5f : bed.R.V0 - 0.5f) : (bed.Edge < 2 ? bed.R.U1 + 0.05f : bed.R.V1 + 0.05f);
                AgainstWall(w, "nightstand", 0.45f, 0.4f, 0.5f, (e, r) => MathF.Abs((e < 2 ? r.U0 : r.V0) - a) > 0.15f ? -1000f : 0f, onlyEdge: bed.Edge, access: false);
            }
        }
        AgainstWall(w, "wardrobe", w.Rng.Pick(new[] { 1.2f, 1.5f, 1.8f }), 0.6f, 2.1f, (e, r) => Corner(w, e, r) * 1.5f + NearestDoor(w, r) * 0.2f, tall: true);
        if (w.Room.Area > 10f)
            AgainstWall(w, "desk", 1.2f, 1.1f, 0.76f, (e, r) => -WindowNear(w, e, r) * 0.6f + (EdgeHasWindow(w, e) ? 1f : 0f));
        if (w.Room.Area > 14f)
            AgainstWall(w, "bookshelf", 0.9f, 0.35f, 1.9f, (e, r) => Corner(w, e, r), tall: true);
    }

    private static void Living(RoomWork w)
    {
        // the sofa against a wall without a door, with its back to it; the TV on the wall it faces
        var sofa = AgainstWall(w, "sofa", 2.0f, 0.9f, 0.85f,
            (e, r) => (EdgeHasDoor(w, e) ? -1f : 0f) + (EdgeHasWindow(w, e) ? -0.5f : 0f) + NearestDoor(w, r) * 0.3f);
        if (sofa != null)
        {
            int opp = sofa.Edge switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };
            float mid = sofa.Edge < 2 ? sofa.R.Centre.X : sofa.R.Centre.Y;
            AgainstWall(w, "tv_unit", 1.6f, 0.45f, 0.5f, (e, r) => -MathF.Abs((e < 2 ? r.Centre.X : r.Centre.Y) - mid) * 2f, onlyEdge: opp);
            // the coffee table in front of the sofa
            var front = sofa.R.Centre + sofa.Facing * ((sofa.Edge < 2 ? sofa.R.H : sofa.R.W) * 0.5f + 0.45f + 0.28f);
            bool along = sofa.Edge < 2;
            FreeStanding(w, "coffee_table", along ? 1.0f : 0.55f, along ? 0.55f : 1.0f, 0.42f, r => -Vector2.Distance(r.Centre, front) * 4f);
        }
        AgainstWall(w, "bookshelf", 0.9f, 0.35f, 1.9f, (e, r) => Corner(w, e, r) + NearestDoor(w, r) * 0.1f, tall: true);
        AgainstWall(w, "armchair", 0.85f, 0.85f, 0.8f, (e, r) => Corner(w, e, r) * 0.5f - WindowNear(w, e, r) * 0.1f);
        if (w.Room.Area >= 18f)
        {
            var centre = w.Area.Centre;
            FreeStanding(w, "dining_table", 1.4f, 0.8f + 1.0f, 0.76f, r => -Vector2.Distance(r.Centre, centre) * 0.5f);
        }
    }

    private static void Kitchen(RoomWork w)
    {
        // the counter along the longest free wall, the fridge at its end, the sink at a window
        var counter = AgainstWall(w, "counter", Math.Clamp(MathF.Max(w.Area.W, w.Area.H) - 1.4f, 1.2f, 3.0f), 0.6f, 0.9f,
            (e, r) => (e < 2 ? r.W : r.H) + (EdgeHasWindow(w, e) ? 0.8f : 0f) + Corner(w, e, r) * 0.5f);
        counter ??= AgainstWall(w, "counter", 1.2f, 0.6f, 0.9f, (e, r) => Corner(w, e, r));
        if (counter != null)
            AgainstWall(w, "fridge", 0.6f, 0.65f, 1.8f, (e, r) => -Vector2.Distance(r.Centre, counter.R.Centre) * 0.3f + Corner(w, e, r) + (e == counter.Edge ? 1f : 0f), tall: true);
        else AgainstWall(w, "fridge", 0.6f, 0.65f, 1.8f, (e, r) => Corner(w, e, r), tall: true);
        if (w.Room.Area >= 7f)
            FreeStanding(w, "kitchen_table", 0.8f, 0.8f + 1.0f, 0.76f, r => -NearestWall(w, r) * 0.5f);
    }

    private static float NearestWall(RoomWork w, Rect r) =>
        MathF.Min(MathF.Min(r.U0 - w.Area.U0, w.Area.U1 - r.U1), MathF.Min(r.V0 - w.Area.V0, w.Area.V1 - r.V1));

    private static void Bathroom(RoomWork w)
    {
        // a bath across the end if it's long enough, else a shower in a corner; the toilet, the basin, the washer
        if (AgainstWall(w, "bath", 1.6f, 0.72f, 0.55f, (e, r) => Corner(w, e, r) * 2f + NearestDoor(w, r) * 0.3f) == null)
            AgainstWall(w, "shower", 0.85f, 0.85f, 2.0f, (e, r) => Corner(w, e, r) * 2f + NearestDoor(w, r) * 0.3f, tall: true);
        AgainstWall(w, "toilet", 0.45f, 0.68f, 0.8f, (e, r) => Corner(w, e, r) * 0.5f + NearestDoor(w, r) * 0.2f);
        AgainstWall(w, "basin", 0.6f, 0.45f, 0.88f, (e, r) => -NearestDoor(w, r) * 0.1f);
        AgainstWall(w, "washer", 0.6f, 0.6f, 0.85f, (e, r) => Corner(w, e, r), access: false);
    }

    private static void Hall(RoomWork w)
    {
        AgainstWall(w, "shoe_cabinet", 0.9f, 0.35f, 0.9f, (e, r) => NearestDoor(w, r) < 1.6f ? 1f : 0f, access: false);
        AgainstWall(w, "coat_rack", 0.8f, 0.3f, 1.8f, (e, r) => NearestDoor(w, r) < 2f ? 1f : 0f, tall: true, access: false);
    }

    private static void Storage(RoomWork w)
    {
        for (int k = 0; k < 3; k++) AgainstWall(w, "shelves", 1.2f, 0.45f, 2.0f, (e, r) => Corner(w, e, r), tall: true);
    }

    // ---- building the pieces -------------------------------------------------------------------------------------

    private static void Materials(InteriorModel m)
    {
        var r = Rng.Stream(m.Seed, "fengshui.look");
        var mm = m.Materials;
        mm["f_wood"] = Kit.Mat("f_wood", r.Weighted<string>([("a07850", 3), ("c8a070", 2), ("6e4a30", 2), ("d8c8a8", 1)]), 0.6f);
        mm["f_wood_dark"] = Kit.Mat("f_wood_dark", r.Weighted<string>([("5a3c28", 2), ("3e2c20", 1), ("7a5a3a", 1)]), 0.55f);
        mm["f_fabric"] = Kit.Mat("f_fabric", r.Weighted<string>([("7a5a48", 2), ("4f6a5a", 2), ("6a6e80", 2), ("8a4a40", 1), ("b0a080", 1)]), 0.95f);
        mm["f_fabric2"] = Kit.Mat("f_fabric2", r.Weighted<string>([("a8b0b8", 2), ("c8a8a0", 1), ("9ab08a", 1), ("d0c090", 1)]), 0.95f);
        mm["f_white"] = Kit.Mat("f_white", "e8e6e0", 0.5f);
        mm["f_metal"] = Kit.Mat("f_metal", "9aa0a4", 0.35f, 0.7f);
        mm["f_black"] = Kit.Mat("f_black", "1c1c1e", 0.3f);
        mm["f_kitchen"] = Kit.Mat("f_kitchen", r.Weighted<string>([("e8e4d8", 2), ("c86040", 1), ("6a8a5a", 1), ("a8b8c8", 1), ("8a6a48", 1)]), 0.5f);
        mm["f_top"] = Kit.Mat("f_top", r.Weighted<string>([("4a4a48", 2), ("b8b0a0", 1), ("6a5040", 1)]), 0.4f);
    }

    private static readonly Dictionary<string, string> Loot = new()
    {
        ["nightstand"] = "drawer", ["wardrobe"] = "wardrobe", ["desk"] = "drawer", ["bookshelf"] = "shelf", ["tv_unit"] = "drawer",
        ["fridge"] = "fridge", ["counter"] = "cupboard", ["basin"] = "medicine", ["shoe_cabinet"] = "drawer", ["shelves"] = "shelf",
    };

    private static void Build(InteriorModel m, RoomWork w, Spot p, int n)
    {
        string id = $"{w.Room.Id}/f{n}.{p.Kind}";
        float f = w.Floor;
        var item = new FurnitureItem(id, p.Kind, w.Room.Id, new Vector2(p.R.U0, p.R.V0), new Vector2(p.R.U1, p.R.V1), p.H, p.Facing, f) { AgainstWall = p.Edge >= 0 };
        m.Furniture.Add(item);
        var c = p.R.Centre;
        // physics: one box for the piece
        m.Colliders.Add(new Collider(m.Frame.P(c.X, f + p.H * 0.5f, c.Y), new Vector3(p.R.W * 0.5f, p.H * 0.5f, p.R.H * 0.5f), m.Frame.AxisU, Vector3.UnitY));
        if (Loot.TryGetValue(p.Kind, out var loot))
        {
            var face = m.Frame.AxisU * p.Facing.X + m.Frame.AxisV * p.Facing.Y;
            m.Markers.Add(new Marker(id + ".loot", "loot_" + loot, m.Frame.P(c.X, f + MathF.Min(p.H, 1.0f), c.Y), face, new Vector2(p.R.W, p.R.H)));
        }

        // the piece's own frame: a along the wall (0..W), d out from the wall (0..D), y up from the floor
        bool alongU = p.Edge < 0 || p.Edge < 2;
        float W = alongU ? p.R.W : p.R.H, D = alongU ? p.R.H : p.R.W;
        float wallLine = p.Edge switch { 1 => p.R.V1, 3 => p.R.U1, 2 => p.R.U0, _ => p.R.V0 };
        float sgn = p.Edge is 1 or 3 ? -1f : 1f;
        void Part(string role, float a0, float a1, float d0, float d1, float y0, float y1)
        {
            float u0, u1, v0, v1;
            if (alongU) { u0 = p.R.U0 + a0; u1 = p.R.U0 + a1; v0 = wallLine + sgn * d0; v1 = wallLine + sgn * d1; }
            else { v0 = p.R.V0 + a0; v1 = p.R.V0 + a1; u0 = wallLine + sgn * d0; u1 = wallLine + sgn * d1; }
            BoxMesh(m.Frame, item.Shape, role, MathF.Min(u0, u1), MathF.Max(u0, u1), MathF.Min(v0, v1), MathF.Max(v0, v1), f + y0, f + y1);
        }
        switch (p.Kind)
        {
            case "bed_double" or "bed_single":
                Part("f_wood", 0, W, 0, 0.06f, 0, 1.0f);                     // headboard
                Part("f_wood", 0, W, 0.06f, D, 0, 0.35f);                     // frame
                Part("f_white", 0.03f, W - 0.03f, 0.08f, D - 0.03f, 0.35f, 0.52f);   // mattress
                Part("f_fabric2", 0.02f, W - 0.02f, 0.62f, D - 0.01f, 0.52f, 0.58f); // duvet
                if (W > 1.2f) { Part("f_white", 0.12f, W * 0.5f - 0.06f, 0.12f, 0.5f, 0.52f, 0.64f); Part("f_white", W * 0.5f + 0.06f, W - 0.12f, 0.12f, 0.5f, 0.52f, 0.64f); }
                else Part("f_white", 0.12f, W - 0.12f, 0.12f, 0.5f, 0.52f, 0.64f);
                break;
            case "nightstand":
                Part("f_wood", 0, W, 0, D, 0, 0.5f);
                Part("f_wood_dark", 0.05f, W - 0.05f, D, D + 0.01f, 0.32f, 0.44f);   // a drawer front
                break;
            case "wardrobe":
                Part("f_wood", 0, W, 0, D, 0, 2.1f);
                Part("f_wood_dark", W * 0.5f - 0.01f, W * 0.5f + 0.01f, D, D + 0.01f, 0.1f, 2.0f);   // the doors' meeting line
                break;
            case "desk":
            {
                const float top = 0.6f;   // the desk; its chair stands in the rest of the footprint
                Part("f_wood", 0, W, 0, top, 0.72f, 0.76f);
                Part("f_wood", 0, 0.04f, 0, top, 0, 0.72f); Part("f_wood", W - 0.4f, W, 0, top, 0, 0.72f);   // a leg and a drawer unit
                Part("f_fabric", W * 0.5f - 0.45f, W * 0.5f, top + 0.05f, top + 0.5f, 0.42f, 0.47f);
                Part("f_fabric", W * 0.5f - 0.45f, W * 0.5f, top + 0.45f, top + 0.5f, 0.47f, 0.9f);
                Part("f_metal", W * 0.5f - 0.25f, W * 0.5f - 0.2f, top + 0.25f, top + 0.3f, 0, 0.42f);
                break;
            }
            case "bookshelf" or "shelves":
            {
                string role = p.Kind == "shelves" ? "f_metal" : "f_wood";
                Part(role, 0, 0.03f, 0, D, 0, p.H); Part(role, W - 0.03f, W, 0, D, 0, p.H); Part(role, 0, W, 0, 0.02f, 0, p.H);
                for (float y = 0.05f; y < p.H; y += 0.42f) Part(role, 0.03f, W - 0.03f, 0, D, y, y + 0.03f);
                Part(role, 0, W, 0, D, p.H - 0.03f, p.H);
                if (p.Kind == "bookshelf")
                    for (float y = 0.08f; y < p.H - 0.4f; y += 0.42f) Part("f_fabric", 0.08f, W * 0.6f, 0.05f, D - 0.06f, y, y + 0.26f);   // books
                else
                    for (float y = 0.08f; y < p.H - 0.4f; y += 0.84f) Part("f_wood", 0.1f, W * 0.45f, 0.05f, D - 0.05f, y, y + 0.3f);  // boxes
                break;
            }
            case "sofa":
                Part("f_fabric", 0, W, 0, D, 0.08f, 0.42f);
                Part("f_fabric", 0, W, 0, 0.24f, 0.42f, 0.85f);
                Part("f_fabric", 0, 0.2f, 0.24f, D, 0.42f, 0.62f); Part("f_fabric", W - 0.2f, W, 0.24f, D, 0.42f, 0.62f);
                Part("f_black", 0.05f, W - 0.05f, 0.05f, D - 0.05f, 0, 0.08f);
                break;
            case "armchair":
                Part("f_fabric2", 0, W, 0, D, 0.08f, 0.42f);
                Part("f_fabric2", 0, W, 0, 0.22f, 0.42f, 0.8f);
                Part("f_fabric2", 0, 0.16f, 0.22f, D, 0.42f, 0.6f); Part("f_fabric2", W - 0.16f, W, 0.22f, D, 0.42f, 0.6f);
                break;
            case "tv_unit":
                Part("f_wood_dark", 0, W, 0, D, 0, 0.5f);
                Part("f_black", W * 0.5f - 0.55f, W * 0.5f + 0.55f, 0.15f, 0.2f, 0.52f, 1.15f);
                Part("f_black", W * 0.5f - 0.15f, W * 0.5f + 0.15f, 0.1f, 0.3f, 0.5f, 0.52f);
                break;
            case "coffee_table" or "dining_table" or "kitchen_table":
            {
                // a dining or kitchen table's footprint holds a chair on each long side (0.5 m each)
                float top = p.H, leg = 0.05f, t0 = p.Kind == "coffee_table" ? 0 : 0.5f, t1 = p.Kind == "coffee_table" ? D : D - 0.5f;
                Part("f_wood", 0, W, t0, t1, top - 0.04f, top);
                foreach (var (la, ld) in new[] { (0f, t0), (W - leg, t0), (0f, t1 - leg), (W - leg, t1 - leg) }) Part("f_wood", la, la + leg, ld, ld + leg, 0, top - 0.04f);
                if (p.Kind != "coffee_table")
                    foreach (bool near in new[] { true, false })
                    {
                        float c0 = near ? 0.02f : t1 + 0.03f;
                        Part("f_wood_dark", W * 0.5f - 0.22f, W * 0.5f + 0.22f, c0, c0 + 0.45f, 0.42f, 0.46f);
                        float back = near ? c0 : c0 + 0.41f;
                        Part("f_wood_dark", W * 0.5f - 0.22f, W * 0.5f + 0.22f, back, back + 0.04f, 0.46f, 0.9f);
                    }
                break;
            }
            case "counter":
            {
                Part("f_kitchen", 0, W, 0, D - 0.02f, 0.1f, 0.86f);
                Part("f_black", 0, W, 0.05f, D - 0.07f, 0, 0.1f);
                Part("f_top", 0, W, 0, D + 0.02f, 0.86f, 0.9f);
                // the sink under a window if there is one on this wall, else at one end; the cooker away from it
                float sinkAt = W * 0.25f;
                var win = w.Edges[p.Edge].Where(b => b.Kind == Block.Window).Select(b => (b.A0 + b.A1) * 0.5f - (alongU ? p.R.U0 : p.R.V0)).Where(x => x > 0.35f && x < W - 0.35f).ToList();
                if (win.Count > 0) sinkAt = win[0];
                Part("f_metal", sinkAt - 0.3f, sinkAt + 0.3f, 0.08f, 0.5f, 0.9f, 0.91f);
                float cook = sinkAt < W * 0.5f ? W - 0.4f : 0.4f;
                if (W >= 1.6f) { Part("f_black", cook - 0.3f, cook + 0.3f, 0.02f, 0.58f, 0.9f, 0.92f); Part("f_black", cook - 0.3f, cook + 0.3f, D - 0.02f, D, 0.15f, 0.8f); }
                // wall cupboards, not over a window
                for (float a = 0; a + 0.6f <= W + 1e-3f; a += 0.6f)
                {
                    float ga = (alongU ? p.R.U0 : p.R.V0) + a;
                    if (w.Edges[p.Edge].Any(b => b.Kind == Block.Window && b.A1 > ga && b.A0 < ga + 0.6f)) continue;
                    Part("f_kitchen", a + 0.01f, a + 0.59f, 0, 0.35f, 1.45f, 2.15f);
                }
                break;
            }
            case "fridge":
                Part("f_white", 0, W, 0, D, 0, 1.8f);
                Part("f_metal", W - 0.08f, W - 0.05f, D, D + 0.03f, 0.9f, 1.4f);
                break;
            case "bath":
                Part("f_white", 0, W, 0, 0.07f, 0, 0.55f); Part("f_white", 0, W, D - 0.07f, D, 0, 0.55f);
                Part("f_white", 0, 0.07f, 0, D, 0, 0.55f); Part("f_white", W - 0.07f, W, 0, D, 0, 0.55f);
                Part("f_white", 0.07f, W - 0.07f, 0.07f, D - 0.07f, 0, 0.12f);
                break;
            case "shower":
                Part("f_white", 0, W, 0, D, 0, 0.1f);
                Part("f_metal", W - 0.02f, W, 0, D, 0.1f, 2.0f);
                Part("f_metal", 0, W, D - 0.02f, D, 0.1f, 2.0f);
                break;
            case "toilet":
                Part("f_white", 0.02f, W - 0.02f, 0, 0.18f, 0.4f, 0.8f);      // the tank
                Part("f_white", 0.05f, W - 0.05f, 0.1f, D, 0, 0.42f);         // the bowl
                break;
            case "basin":
                Part("f_white", 0, W, 0, D, 0.8f, 0.88f);
                Part("f_white", W * 0.5f - 0.1f, W * 0.5f + 0.1f, 0, 0.2f, 0, 0.8f);
                Part("f_wood", 0.05f, W - 0.05f, 0, 0.12f, 1.2f, 1.8f);       // a mirror cabinet
                Part("f_metal", 0.08f, W - 0.08f, 0.12f, 0.13f, 1.25f, 1.75f);
                break;
            case "washer":
                Part("f_white", 0, W, 0, D, 0, 0.85f);
                Part("f_black", W * 0.5f - 0.18f, W * 0.5f + 0.18f, D, D + 0.01f, 0.25f, 0.61f);
                break;
            case "shoe_cabinet":
                Part("f_wood", 0, W, 0, D, 0, 0.9f);
                break;
            case "coat_rack":
                Part("f_metal", 0, W, 0, 0.03f, 1.6f, 1.66f);
                Part("f_wood", 0, W, 0, 0.3f, 1.7f, 1.73f);
                Part("f_fabric", 0.1f, 0.35f, 0.03f, 0.2f, 0.9f, 1.6f);       // a coat left behind
                break;
            default:
                Part("f_wood", 0, W, 0, D, 0, p.H);
                break;
        }
    }

    /// <summary>A box in the frame (no underside), faces outward.</summary>
    private static void BoxMesh(Frame F, Geometry.Mesh mesh, string role, float u0, float u1, float v0, float v1, float y0, float y1)
    {
        if (u1 - u0 < 1e-3f || v1 - v0 < 1e-3f || y1 - y0 < 1e-3f) return;
        Vector3 P(float u, float y, float v) => F.P(u, y, v);
        mesh.AddQuad(role, P(u0, y1, v0), P(u1, y1, v0), P(u1, y1, v1), P(u0, y1, v1), Vector3.UnitY);
        mesh.AddQuad(role, P(u0, y0, v0), P(u1, y0, v0), P(u1, y1, v0), P(u0, y1, v0), -F.AxisV);
        mesh.AddQuad(role, P(u0, y0, v1), P(u1, y0, v1), P(u1, y1, v1), P(u0, y1, v1), F.AxisV);
        mesh.AddQuad(role, P(u0, y0, v0), P(u0, y0, v1), P(u0, y1, v1), P(u0, y1, v0), -F.AxisU);
        mesh.AddQuad(role, P(u1, y0, v0), P(u1, y0, v1), P(u1, y1, v1), P(u1, y1, v0), F.AxisU);
    }
}
