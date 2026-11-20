using System.Numerics;
using BadBuilding.Generation;
using BadBuilding.Geometry;
using BadBuilding.Model;
using BadBuilding.Validation;

namespace BadBuilding.Interior;

/// <summary>
/// Interiors on demand (milestone 3, D33), first archetype: the staircase block (klatkowiec). Each entrance on the
/// main wall gets a staircase core repeated on every storey (a landing at the door, a dog-leg stair with a half
/// landing at the back), and two flats per landing, one on each side, reached from the landing only. A flat is a hall
/// with a short corridor, the bathroom and kitchen on the entrance side, the living room and bedrooms on the far side
/// (the left flat also gets the room behind the staircase). Game scale (D11): doors 1.1 × 2.15 m, halls 1.5 m, stairs
/// 1.3 m wide, about 2.6 m to the ceiling.
/// <para>
/// The facade was built first, so the interior takes its windows: every opening the exterior made is cut through the
/// interior's outer walls where it is, and the rooms fall where they fall behind them. Layouts are invented (D11):
/// believable, not the real building's plan. Footprints this archetype can't fill (not close to a rectangle, too
/// shallow or deep) get no interior and their doors stay shut.
/// </para>
/// </summary>
public static class InteriorGenerator
{
    public const string Version = "0.1.0";

    // game scale (D11)
    public const float DoorWidth = 1.3f, DoorHeight = 2.3f, SmallDoor = 1.1f;
    private const float Half = 0.07f;          // half a partition's thickness
    private const float OuterT = 0.3f;         // the outer wall, inside the facade line
    private const float GlassAt = 0.21f;       // the interior's window glass, behind the facade (the exterior's is at 0.2)
    private const float Slab = 0.2f;
    private const float Inset = 0.012f;        // the interior's openings, inside the exterior's (no cracks, no flicker)
    // roomy (Tom, 20 Nov: "very cramped"): a 3.8 m staircase with 1.75 m flights, 2.1 m halls, 1.9 m corridors;
    // the landing, flights and half landing shrink toward the tight sizes only where the block is too shallow
    private const float Bath = 2.5f;
    private const float Corridor = 1.9f, RoomTarget = 5f, MinRoom = 3.2f;
    // narrow blocks (D37): where the roomy staircase would leave no flat beside it, the tight one and a narrower hall
    private static readonly (float Core, float Hall, float MinFlat) RoomyPlan = (1.9f, 2.1f, 4.6f), TightPlan = (1.45f, 1.6f, 3.6f);
    private static readonly (float Landing, float Run, float Half) Roomy = (2.0f, 2.7f, 1.9f), Tight = (1.3f, 2.3f, 1.3f);

    /// <summary>The interior of a building, or null when its footprint doesn't suit a staircase block.</summary>
    public static InteriorModel? Generate(Shell shell) => Generate(shell, out _);

    /// <summary>As <see cref="Generate(Shell)"/>, saying why not when there's no interior.</summary>
    public static InteriorModel? Generate(Shell shell, out string why)
    {
        var w = new Work(shell);
        var m = w.Run();
        why = w.Why;
        return m;
    }

    private sealed record RoomPlan(string Id, string Kind, string? Flat, List<(Vector2 Min, Vector2 Max)> Rects);
    private sealed record DoorPlan(bool AlongU, float C, float Mid, float Width, float Height, string A, string B, bool Leaf);
    private sealed record Hole(float A0, float A1, float Y0, float Y1, string Kind);

    private sealed class Work(Shell s)
    {
        public string Why = "";
        private Frame _f;
        private float _uMin, _uMax, _depth, _u0, _u1, _v0, _v1, _dc;
        private readonly List<float> _cores = [];
        private int _coreBase, _sections;
        private float _vF;                                    // the section's front line (v)
        private float Landing, StairRun, HalfLanding;          // this section's staircase (roomy where it fits)
        private float CoreHalf, Hall, MinFlat;                 // this section's staircase width, hall, narrowest flat
        private readonly List<(int Side, float A0, float A1, float Y0, float Y1, string Kind)> _segOpen = [];
        private InteriorModel _m = null!;
        private Rng _look = null!;
        private string _wallpaperA = "", _wallpaperB = "";
        // per storey while building
        private List<RoomPlan> _rooms = [];
        private List<DoorPlan> _doors = [];

        private float F(int st) => s.Datum + st * s.StoreyHeight;
        private float C(int st) => F(st) + s.StoreyHeight - Slab;

        public InteriorModel? Run()
        {
            var ring = s.Outer;
            int n = ring.Count;
            if (n < 4) return null;
            // the main wall: the one with the most entrances on it (then the longest)
            int bestEdge = -1, bestCount = 0; float bestLen = 0;
            for (int i = 0; i < n; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                if (len < 6f) continue;
                var dir = (b - a) / len; var outN = new Vector2(-dir.Y, dir.X);
                int count = s.Entrances.Count(e => Polygon.DistanceToSegment(new Vector2(e.Position.X, e.Position.Z), a, b) < 0.6f
                                                   && Vector2.Dot(new Vector2(e.Out.X, e.Out.Z), outN) > 0.9f);
                if (count > bestCount || count == bestCount && count > 0 && len > bestLen) { bestEdge = i; bestCount = count; bestLen = len; }
            }
            if (bestEdge < 0) { Why = "no entrance on a wall of 6 m or more"; return null; }
            var ea = ring[bestEdge]; var eb = ring[(bestEdge + 1) % n];
            var d = Vector2.Normalize(eb - ea);
            _f = new Frame(ea, d, new Vector2(d.Y, -d.X));

            // the rectangle the interior fills (D33): along the door wall, as far as the outline keeps its front on
            // that line and stays deep enough, and only as deep as its shallowest part. Stepped slabs give up the steps
            // (rooms there are blind at the back, the space behind is left solid) instead of having no interior at all.
            var local = ring.Select(p => _f.Local(p)).ToList();
            float uLo = local.Min(p => p.U), uHi = local.Max(p => p.U);
            const float step = 0.5f;
            int ns = Math.Max(1, (int)((uHi - uLo) / step));
            var lo = new float[ns]; var hi = new float[ns];
            for (int k = 0; k < ns; k++)
            {
                float u = uLo + (k + 0.5f) * (uHi - uLo) / ns;
                var xs = new List<float>();
                for (int i = 0; i < n; i++)
                {
                    var (pa, pb) = (local[i], local[(i + 1) % n]);
                    if ((pa.U <= u) == (pb.U <= u)) continue;
                    xs.Add(pa.V + (u - pa.U) / (pb.U - pa.U) * (pb.V - pa.V));
                }
                xs.Sort();
                // the front-most piece of the outline across this line: its front and back
                lo[k] = float.MaxValue; hi[k] = float.MinValue;
                if (xs.Count >= 2) { lo[k] = xs[0]; hi[k] = xs[1]; }
            }
            float cell = (uHi - uLo) / ns;
            float Su(int k) => uLo + (k + 0.5f) * cell;
            // sections: runs of samples deep enough whose front stays on one line (a stepped slab gives several)
            var runs = new List<(int A, int B)>();
            for (int k = 0; k < ns;)
            {
                if (hi[k] - lo[k] < 8.5f) { k++; continue; }
                int j = k;
                while (j + 1 < ns && hi[j + 1] - lo[j + 1] >= 8.5f && MathF.Abs(lo[j + 1] - lo[k]) <= 0.35f) j++;
                runs.Add((k, j));
                k = j + 1;
            }

            _m = new InteriorModel
            {
                Id = $"{s.Id}/interior",
                Frame = _f,
                Footprint = ring,
                Floor = s.Datum,
                Top = F(s.Levels),
                StoreyHeight = s.StoreyHeight,
                Levels = s.Levels,
            };
            _look = Rng.Stream(s.Seed, "interior");
            Materials();

            var used = new HashSet<EntranceDoor>();
            var whys = new List<string>();
            foreach (var (ra, rb) in runs)
            {
                // the ends: the outline's own ends when the run reaches them, else half a sample in
                _uMin = ra == 0 ? uLo : Su(ra) - cell * 0.5f + 0.05f;
                _uMax = rb == ns - 1 ? uHi : Su(rb) + cell * 0.5f - 0.05f;
                _vF = Enumerable.Range(ra, rb - ra + 1).Max(k => lo[k]);
                _depth = Enumerable.Range(ra, rb - ra + 1).Min(k => hi[k]);
                if (_depth - _vF > 20f || _uMax - _uMin < 8f) { whys.Add($"a section {_uMax - _uMin:0.0} x {_depth - _vF:0.0} m"); continue; }
                _u0 = _uMin + OuterT; _u1 = _uMax - OuterT; _v0 = _vF + OuterT; _v1 = _depth - OuterT;
                // the staircase as roomy as the depth allows, leaving at least 3.4 m of rooms behind it
                float room = _v1 - 3.4f - _v0, want = Roomy.Landing + Roomy.Run + Roomy.Half, least = Tight.Landing + Tight.Run + Tight.Half;
                float t = Math.Clamp((room - least) / (want - least), 0f, 1f);
                Landing = Tight.Landing + (Roomy.Landing - Tight.Landing) * t;
                StairRun = Tight.Run + (Roomy.Run - Tight.Run) * t;
                HalfLanding = Tight.Half + (Roomy.Half - Tight.Half) * t;
                _dc = _v0 + Landing + StairRun + HalfLanding;

                // the staircases: the entrances on this section's front, far enough apart and clear of the ends
                (CoreHalf, Hall, MinFlat) = TightPlan;
                _cores.Clear();
                var mine = new List<EntranceDoor>();
                foreach (var e in s.Entrances.OrderBy(e => _f.Local(new Vector2(e.Position.X, e.Position.Z)).U))
                {
                    var (u, v) = _f.Local(new Vector2(e.Position.X, e.Position.Z));
                    bool onFront = MathF.Abs(v - _vF) < 0.6f && Vector2.Dot(new Vector2(e.Out.X, e.Out.Z), -_f.In) > 0.9f && u > _uMin && u < _uMax;
                    if (!onFront) continue;
                    bool fits = (u - CoreHalf - MinFlat >= _u0 - 0.01f || u - CoreHalf >= _u0 + 0.5f) && u + CoreHalf <= _u1 - 0.5f;
                    if (fits && (_cores.Count == 0 || u - _cores[^1] >= 7.5f)) { _cores.Add(u); mine.Add(e); }
                }
                if (_cores.Count == 0) { whys.Add($"no staircase fits in a {_uMax - _uMin:0.0} m section"); continue; }
                // roomy unless a flat beside some staircase would be too narrow for it
                {
                    var bnd = new List<float> { _u0 };
                    for (int i = 1; i < _cores.Count; i++) bnd.Add((_cores[i - 1] + _cores[i]) * 0.5f);
                    bnd.Add(_u1);
                    bool roomy = true;
                    for (int i = 0; i < _cores.Count; i++)
                        foreach (float wdt in new[] { _cores[i] - RoomyPlan.Core - bnd[i], bnd[i + 1] - _cores[i] - RoomyPlan.Core })
                            if (wdt < RoomyPlan.MinFlat && wdt + RoomyPlan.Core - TightPlan.Core >= TightPlan.MinFlat) roomy = false;
                    (CoreHalf, Hall, MinFlat) = roomy ? RoomyPlan : TightPlan;
                }
                foreach (var e in mine) { _m.Entrances.Add(e); used.Add(e); }

                // the facade's openings, by the side of the section they're on
                _segOpen.Clear();
                foreach (var o in s.Openings)
                {
                    var (ua, va) = _f.Local(o.A); var (ub, vb) = _f.Local(o.B);
                    float um = (ua + ub) * 0.5f, vm = (va + vb) * 0.5f;
                    bool inU = um > _uMin - 0.1f && um < _uMax + 0.1f, inV = vm > _vF - 0.1f && vm < _depth + 0.1f;
                    int side = inU && MathF.Abs(vm - _vF) < 0.7f ? 0 : inU && MathF.Abs(vm - _depth) < 0.7f ? 1
                             : inV && MathF.Abs(um - _uMin) < 0.7f ? 2 : inV && MathF.Abs(um - _uMax) < 0.7f ? 3 : -1;
                    if (side < 0) continue;
                    float a0 = side < 2 ? MathF.Min(ua, ub) : MathF.Min(va, vb), a1 = side < 2 ? MathF.Max(ua, ub) : MathF.Max(va, vb);
                    // a door with no staircase behind it stays shut: drawn as a door, solid
                    string kind = o.Kind;
                    // a balcony door is a way out onto its balcony, when the exterior says there is one (D35)
                    if (kind == "balcony_door" && !s.Balconies.Any(bs => !bs.Loggia && Vector2.Distance(new Vector2(bs.Position.X, bs.Position.Z), (o.A + o.B) * 0.5f) < 0.8f && MathF.Abs(bs.Position.Y - o.Y0) < 0.3f)) kind = "glass";
                    if (kind == "door" && !mine.Any(e => Vector2.Distance(new Vector2(e.Position.X, e.Position.Z), (o.A + o.B) * 0.5f) < 1.2f)) kind = "shut";
                    _segOpen.Add((side, a0, a1, o.Y0, o.Y1, kind));
                }
                // loggias (D36): a recess in the wall from the floor to 0.3 m under the next, the glass at its back
                foreach (var lg in s.Balconies.Where(b => b.Loggia))
                {
                    var (lu, lv) = _f.Local(new Vector2(lg.Position.X, lg.Position.Z));
                    bool inU = lu > _uMin && lu < _uMax, inV = lv > _vF && lv < _depth;
                    int side = inU && MathF.Abs(lv - _vF) < 0.4f ? 0 : inU && MathF.Abs(lv - _depth) < 0.4f ? 1
                             : inV && MathF.Abs(lu - _uMin) < 0.4f ? 2 : inV && MathF.Abs(lu - _uMax) < 0.4f ? 3 : -1;
                    if (side < 0) continue;
                    float along = side < 2 ? lu : lv;
                    _segOpen.Add((side, along - lg.Width * 0.5f, along + lg.Width * 0.5f, lg.Position.Y, lg.Position.Y + s.StoreyHeight - 0.3f, "loggia"));
                }
                _m.Openings.AddRange(_segOpen);

                for (int st = 0; st < s.Levels; st++) Storey(st);
                Approach();
                _coreBase += _cores.Count;
                _sections++;
            }
            foreach (var e in s.Entrances) if (!used.Contains(e)) _m.Sealed.Add(e);
            if (_m.Entrances.Count == 0)
            {
                Why = runs.Count == 0 ? $"footprint {uHi - uLo:0.0} m long: nowhere 8.5 m deep behind a door wall" : string.Join("; ", whys);
                return null;
            }
            Validate();
            return _m;
        }

        // ---- materials ----------------------------------------------------------------------------------------------

        private void Materials()
        {
            var mm = _m.Materials;
            // the staircase's oil-painted lower wall (lamperia) and whitewash above, a terrazzo floor
            string paint = _look.Weighted<string>([("6f8f7a", 3), ("7d93a8", 2), ("a89a78", 2), ("8a6f5a", 1), ("9aa36f", 1)]);
            mm["stair_paint"] = Kit.Mat("stair_paint", paint, 0.45f);
            mm["stair_wall"] = Kit.Mat("stair_wall", "e4e1d8", 0.95f);
            mm["terrazzo"] = Kit.Mat("terrazzo", "8e8a82", 0.7f);
            mm["stair"] = Kit.Mat("stair", "7b776f", 0.75f);
            mm["rail"] = Kit.Mat("rail", "3e4a45", 0.5f, 0.4f);
            // flats: plaster, wallpaper in two shades, tiles in the bathroom, parquet, lino
            mm["plaster"] = Kit.Mat("plaster", "e8e3d6", 0.95f);
            _wallpaperA = "wallpaper"; _wallpaperB = "wallpaper_b";
            mm["wallpaper"] = Kit.Mat("wallpaper", _look.Weighted<string>([("d9cfae", 2), ("c9d3c0", 2), ("d8c2b0", 2), ("bfc8d0", 1), ("d6b98f", 1)]), 0.9f);
            mm["wallpaper_b"] = Kit.Mat("wallpaper_b", _look.Weighted<string>([("e3d9c0", 2), ("b9c6b4", 1), ("cbb5a4", 2), ("c3ccd6", 1), ("a7b49c", 1)]), 0.9f);
            mm["kitchen"] = Kit.Mat("kitchen", _look.Weighted<string>([("e9e0b8", 2), ("dfe6d8", 1), ("e6d3b8", 1)]), 0.8f);
            mm["tile"] = Kit.Mat("tile", _look.Weighted<string>([("e9ece8", 3), ("bcd2d8", 2), ("d9cba8", 1), ("9fb7a4", 1)]), 0.3f);
            mm["parquet"] = Kit.Mat("parquet", _look.Weighted<string>([("8a6142", 3), ("9c7450", 2), ("6e4c35", 1)]), 0.6f);
            mm["lino"] = Kit.Mat("lino", _look.Weighted<string>([("7f7a68", 2), ("6c7766", 1), ("8a7660", 1)]), 0.55f);
            mm["floor_tile"] = Kit.Mat("floor_tile", _look.Weighted<string>([("b8b0a2", 2), ("8f5a46", 1), ("9aa1a3", 1)]), 0.4f);
            mm["ceiling"] = Kit.Mat("ceiling", "eeece6", 0.95f);
            mm["door_leaf"] = Kit.Mat("door_leaf", _look.Weighted<string>([("9a7a55", 2), ("d9d4c6", 1), ("6b5240", 1)]), 0.6f);
            mm["window"] = Kit.Mat("window", "9fb9c8", 0.04f, 0.15f, 0.32f);
            mm["glass_block"] = Kit.Mat("glass_block", "c9d8da", 0.3f, 0f, 0.55f);
            mm["loggia_floor"] = Kit.Mat("loggia_floor", "8b8984", 0.9f);
        }

        private string WallRole(string? kind, string? flat) => kind switch
        {
            null => "",
            "stair" => "lamperia",
            "hall" => "plaster",
            "bathroom" => "tile",
            "kitchen" => "kitchen",
            "storage" => "plaster",
            "bedroom" => flat != null && (Rng.Fnv1a(flat) & 1) == 1 ? _wallpaperB : _wallpaperA,
            _ => _wallpaperA,
        };

        private static string FloorRole(string kind) => kind switch
        {
            "stair" => "terrazzo",
            "hall" => "lino",
            "bathroom" or "kitchen" => "floor_tile",
            _ => "parquet",
        };

        // ---- planning ---------------------------------------------------------------------------------------------

        private void Storey(int st)
        {
            _rooms = [];
            _doors = [];
            var bounds = new List<float> { _u0 };
            for (int i = 1; i < _cores.Count; i++) bounds.Add((_cores[i - 1] + _cores[i]) * 0.5f);
            bounds.Add(_u1);
            for (int i = 0; i < _cores.Count; i++)
            {
                float e = _cores[i];
                string core = $"{_m.Id}/s{st}/c{_coreBase + i}/stair";
                _rooms.Add(new RoomPlan(core, "stair", null, [(new Vector2(e - CoreHalf, _v0), new Vector2(e + CoreHalf, _dc))]));
                Flat(st, _coreBase + i, e, -1, e - CoreHalf - bounds[i], core);
                Flat(st, _coreBase + i, e, +1, bounds[i + 1] - (e + CoreHalf), core);
            }
            Build(st);
        }

        /// <summary>One flat beside a staircase: <paramref name="side"/> −1 left (it also takes the room behind the
        /// staircase), +1 right; <paramref name="width"/> from the staircase wall to the section's edge.</summary>
        private void Flat(int st, int ci, float e, int side, float width, string core)
        {
            if (width < MinFlat) return;   // too narrow: left as a sealed void (no door to it)
            string id = $"{_m.Id}/s{st}/c{ci}/{(side < 0 ? "L" : "R")}";
            float cw = e + side * CoreHalf, a = _dc, k = side < 0 ? 2 * CoreHalf : 0f;
            float U(float x) => cw + side * x;
            (Vector2, Vector2) R(float x0, float x1, float v0, float v1) => (new Vector2(MathF.Min(U(x0), U(x1)), v0), new Vector2(MathF.Max(U(x0), U(x1)), v1));
            var rooms = new List<RoomPlan>();
            RoomPlan Add(string kind, int no, params (Vector2, Vector2)[] rects)
            {
                var r = new RoomPlan($"{id}/{kind}{no}", kind, id, rects.ToList());
                rooms.Add(r);
                return r;
            }

            bool corridor = width - Hall >= 2.6f;
            // a deep block (more than 7.5 m behind the staircase): the back rooms keep 5 m, a corridor runs along them,
            // and what's left between is storage, reached from that corridor; the hall goes straight through to it
            float back = _v1 - a;
            bool deep = back > 8.5f;
            float vb = deep ? _v1 - 5.5f : a;                       // where the back rooms start
            float vs = deep ? vb - Corridor : a;                  // where the back corridor starts
            bool storage = deep && vs - a >= 1.8f;
            if (deep && !storage) vs = a;
            var hallRects = new List<(Vector2, Vector2)> { R(0, Hall, _v0, deep ? vs : a) };
            if (corridor) hallRects.Add(R(Hall, width, a - Corridor, a));
            if (deep) hallRects.Add(R(-k, width, vs, vb));
            var hall = Add("hall", 0, hallRects.ToArray());
            if (storage)
            {
                int sn = 0;
                foreach (var (x0, x1) in new[] { (-k, 0f), (Hall, width) })
                {
                    if (x1 - x0 < 1.6f) continue;
                    var st2 = Add("storage", sn++, R(x0, x1, a, vs));
                    _doors.Add(new DoorPlan(true, vs, U(x0 + MathF.Min(0.9f, (x1 - x0) * 0.5f)), SmallDoor, DoorHeight, hall.Id, st2.Id, true));
                }
            }
            // the way in from the landing, a door leaf swung into the hall
            _doors.Add(new DoorPlan(false, cw, _v0 + Landing * 0.5f, MathF.Min(DoorWidth, Landing - 0.2f), DoorHeight, core, hall.Id, true));
            if (corridor)
            {
                float restW = width - Hall;
                if (restW >= Bath + 2.2f)
                {
                    var bath = Add("bathroom", 0, R(Hall, Hall + Bath, _v0, a - Corridor));
                    var kitchen = Add("kitchen", 0, R(Hall + Bath, width, _v0, a - Corridor));
                    _doors.Add(new DoorPlan(true, a - Corridor, U(Hall + Bath * 0.5f), SmallDoor, DoorHeight, hall.Id, bath.Id, true));
                    _doors.Add(new DoorPlan(true, a - Corridor, U(Hall + Bath + MathF.Min(1.3f, (width - Hall - Bath) * 0.5f)), DoorWidth, DoorHeight, hall.Id, kitchen.Id, false));
                }
                else
                {
                    var bath = Add("bathroom", 0, R(Hall, width, _v0, a - Corridor));
                    _doors.Add(new DoorPlan(true, a - Corridor, U(Hall + MathF.Min(1.0f, restW * 0.5f)), SmallDoor, DoorHeight, hall.Id, bath.Id, true));
                }
            }
            else
            {
                var bath = Add("bathroom", 0, R(Hall, width, _v0, a));
                _doors.Add(new DoorPlan(false, U(Hall), (_v0 + a) * 0.5f, SmallDoor, DoorHeight, hall.Id, bath.Id, true));
            }

            // the far side: living room (on the hall) and bedrooms
            float t = width + k;
            int nr = Math.Clamp((int)MathF.Round(t / RoomTarget), 1, 3);
            var cuts = new List<float>();
            for (int j = 1; j < nr; j++)
            {
                float x = -k + t * j / nr;
                if (x > -0.2f && x < Hall + 0.2f) x = Hall + 0.2f;     // the living room keeps the hall's door
                if (x - (cuts.Count > 0 ? cuts[^1] : -k) < MinRoom || width - x < MinRoom) continue;
                cuts.Add(x);
            }
            var xs = new List<float> { -k };
            xs.AddRange(cuts);
            xs.Add(width);
            var far = new List<RoomPlan>();
            int bed = 0;
            for (int j = 0; j + 1 < xs.Count; j++)
            {
                bool living = xs[j] <= Hall * 0.5f && xs[j + 1] >= Hall * 0.5f;
                far.Add(Add(living ? "living" : "bedroom", living ? 0 : bed++, R(xs[j], xs[j + 1], vb, _v1)));
            }
            if (deep)
            {
                // every back room opens onto the back corridor
                for (int j = 0; j < far.Count; j++)
                    _doors.Add(new DoorPlan(true, vb, U((xs[j] + xs[j + 1]) * 0.5f), DoorWidth, DoorHeight, hall.Id, far[j].Id, true));
                _rooms.AddRange(rooms);
                return;
            }
            for (int j = 0; j < far.Count; j++)
            {
                float x0 = xs[j], x1 = xs[j + 1];
                var r = far[j];
                if (r.Kind == "living") { _doors.Add(new DoorPlan(true, a, U(Hall * 0.5f), DoorWidth, DoorHeight, hall.Id, r.Id, true)); continue; }
                float o0 = MathF.Max(x0, Hall), o1 = MathF.Min(x1, width);
                if (corridor && o1 - o0 >= DoorWidth + 0.4f) { _doors.Add(new DoorPlan(true, a, U((o0 + o1) * 0.5f), DoorWidth, DoorHeight, hall.Id, r.Id, true)); continue; }
                // behind the staircase (or no corridor): through the neighbour nearer the hall
                var nb = x1 <= Hall * 0.5f ? far[j + 1] : far[j - 1];
                float line = x1 <= Hall * 0.5f ? x1 : x0;
                _doors.Add(new DoorPlan(false, U(line), (a + _v1) * 0.5f, DoorWidth, DoorHeight, nb.Id, r.Id, true));
            }
            _rooms.AddRange(rooms);
        }

        // ---- building ---------------------------------------------------------------------------------------------

        private RoomPlan? RoomAt(float u, float v)
        {
            foreach (var r in _rooms)
                foreach (var q in r.Rects)
                    if (u > q.Min.X && u < q.Max.X && v > q.Min.Y && v < q.Max.Y) return r;
            return null;
        }

        private void Build(int st)
        {
            float f = F(st), c = C(st);
            bool top = st == s.Levels - 1;
            var mesh = _m.Mesh;

            // rooms, doorways, floors and ceilings
            foreach (var r in _rooms)
            {
                var first = r.Rects[0];
                var mid = (first.Min + first.Max) * 0.5f;
                _m.Rooms.Add(new Room(r.Id, r.Kind, st, r.Flat, r.Rects, f, _f.P(mid.X, f, mid.Y)));
                _m.Markers.Add(new Marker($"{r.Id}.centre", "room_" + r.Kind, _f.P(mid.X, f, mid.Y), Vector3.UnitY));
                foreach (var q in r.Rects)
                {
                    if (r.Kind == "stair")
                    {
                        // the landing at the door; the ground floor and the top close the whole stairwell
                        float vf = st == 0 ? q.Max.Y : _v0 + Landing, vc = top ? q.Max.Y : _v0 + Landing;
                        HQuad(FloorRole(r.Kind), q.Min.X, q.Max.X, q.Min.Y, vf, f + 0.002f, true);
                        HQuad("ceiling", q.Min.X, q.Max.X, q.Min.Y, vc, top ? c : c, false);
                        if (st > 0)   // the edge of the landing slab, seen from the stair
                            VQuad("terrazzo", true, _v0 + Landing, q.Min.X, q.Max.X, C(st - 1), f, _f.AxisV);
                        continue;
                    }
                    HQuad(FloorRole(r.Kind), q.Min.X, q.Max.X, q.Min.Y, q.Max.Y, f + 0.002f, true);
                    HQuad("ceiling", q.Min.X, q.Max.X, q.Min.Y, q.Max.Y, c, false);
                }
            }
            foreach (var dp in _doors)
            {
                var pos = dp.AlongU ? _f.P(dp.Mid, f, dp.C) : _f.P(dp.C, f, dp.Mid);
                _m.Doors.Add(new Doorway($"{dp.B}.door", dp.A, dp.B, pos, dp.AlongU ? _f.AxisV : _f.AxisU, dp.Width, dp.Height));
                if (dp.Leaf) Leaf(dp, f);
            }

            Partitions(st, f, c, top);
            OuterWalls(st, f, c, top);
            Stairs(st, f, top);
            Floors(st, f);
        }

        /// <summary>The walls between rooms: every rectangle edge inside the outer walls, merged per line, split where
        /// the rooms beside it change, left out where both sides are the same room, cut by doorways.</summary>
        private void Partitions(int st, float f, float c, bool top)
        {
            var lines = new Dictionary<(bool, int), (bool AlongU, float C, List<(float, float)> Spans)>();
            void Edge(bool alongU, float cc, float a0, float a1)
            {
                // the outer walls are built on their own
                if (alongU && (MathF.Abs(cc - _v0) < 0.01f || MathF.Abs(cc - _v1) < 0.01f)) return;
                if (!alongU && (MathF.Abs(cc - _u0) < 0.01f || MathF.Abs(cc - _u1) < 0.01f)) return;
                var key = (alongU, (int)MathF.Round(cc * 100));
                if (!lines.TryGetValue(key, out var l)) lines[key] = l = (alongU, cc, []);
                l.Spans.Add((a0, a1));
            }
            foreach (var r in _rooms)
                foreach (var q in r.Rects)
                {
                    Edge(true, q.Min.Y, q.Min.X, q.Max.X); Edge(true, q.Max.Y, q.Min.X, q.Max.X);
                    Edge(false, q.Min.X, q.Min.Y, q.Max.Y); Edge(false, q.Max.X, q.Min.Y, q.Max.Y);
                }
            foreach (var (alongU, cc, spans) in lines.Values)
            {
                // break points: span ends and every room corner along the line
                var xs = new SortedSet<float>();
                foreach (var (a0, a1) in spans) { xs.Add(a0); xs.Add(a1); }
                foreach (var r in _rooms) foreach (var q in r.Rects) { xs.Add(alongU ? q.Min.X : q.Min.Y); xs.Add(alongU ? q.Max.X : q.Max.Y); }
                foreach (var lg in Loggias(f)) { if (alongU == (lg.Side >= 2)) { xs.Add(lg.Back0); xs.Add(lg.Back1); } }
                var pts = xs.ToList();
                var doors = _doors.Where(dp => dp.AlongU == alongU && MathF.Abs(dp.C - cc) < 0.01f).ToList();
                var segs = new List<(float A0, float A1, RoomPlan? Neg, RoomPlan? Pos)?>();
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    float a0 = pts[i], a1 = pts[i + 1];
                    float am = (a0 + a1) * 0.5f;
                    if (a1 - a0 < 0.02f || !spans.Any(sp => am > sp.Item1 && am < sp.Item2)) { segs.Add(null); continue; }
                    // no room wall out in a loggia's open air (D36)
                    if (InLoggia(f, alongU ? am : cc, alongU ? cc : am)) { segs.Add(null); continue; }
                    var neg = alongU ? RoomAt(am, cc - 0.1f) : RoomAt(cc - 0.1f, am);
                    var pos = alongU ? RoomAt(am, cc + 0.1f) : RoomAt(cc + 0.1f, am);
                    segs.Add(neg == null && pos == null || neg != null && ReferenceEquals(neg, pos) ? null : (a0, a1, neg, pos));
                }
                for (int i = 0; i < segs.Count; i++)
                {
                    if (segs[i] is not { } sg) continue;
                    // where the run of wall ends, it reaches into the wall it meets (or stops with an end face)
                    float e0 = i == 0 || segs[i - 1] == null ? Half - 0.004f : 0f;
                    float e1 = i == segs.Count - 1 || segs[i + 1] == null ? Half - 0.004f : 0f;
                    bool stair = sg.Neg?.Kind == "stair" || sg.Pos?.Kind == "stair";
                    float yTop = stair && !top ? F(st + 1) : c;
                    var holes = doors.Where(dp => dp.Mid + dp.Width * 0.5f > sg.A0 && dp.Mid - dp.Width * 0.5f < sg.A1)
                                     .Select(dp => new Hole(dp.Mid - dp.Width * 0.5f, dp.Mid + dp.Width * 0.5f, f, f + dp.Height, "door")).ToList();
                    WallRun(alongU, cc - Half, cc + Half, sg.A0 - e0, sg.A1 + e1, f, yTop, holes,
                        WallRole(sg.Neg?.Kind, sg.Neg?.Flat), WallRole(sg.Pos?.Kind, sg.Pos?.Flat), f, e0 > 0, e1 > 0);
                }
            }
        }

        /// <summary>A straight wall from <paramref name="w0"/> to <paramref name="w1"/> across its line (the
        /// thickness), <paramref name="a0"/>..<paramref name="a1"/> along it, with door holes: both faces (each in the
        /// role of the room on its side, none where there's no room), the reveals of each hole, and its physics.</summary>
        private void WallRun(bool alongU, float w0, float w1, float a0, float a1, float y0, float y1, List<Hole> holes,
            string roleNeg, string rolePos, float floor, bool cap0 = false, bool cap1 = false)
        {
            var nNeg = alongU ? -_f.AxisV : -_f.AxisU;
            var cols = new SortedSet<float> { a0, a1 };
            foreach (var h in holes) { cols.Add(Math.Clamp(h.A0, a0, a1)); cols.Add(Math.Clamp(h.A1, a0, a1)); }
            var xs = cols.ToList();
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                if (x1 - x0 < 0.005f) continue;
                float xm = (x0 + x1) * 0.5f;
                var hole = holes.FirstOrDefault(h => xm > h.A0 && xm < h.A1);
                var pieces = hole == null ? [(y0, y1)] : new List<(float, float)> { (y0, hole.Y0), (hole.Y1, y1) };
                foreach (var (p0, p1) in pieces)
                {
                    if (p1 - p0 < 0.01f) continue;
                    if (roleNeg != "") Face(roleNeg, alongU, w0, x0, x1, p0, p1, nNeg, floor);
                    if (rolePos != "") Face(rolePos, alongU, w1, x0, x1, p0, p1, -nNeg, floor);
                    Box(alongU, w0, w1, x0, x1, p0, p1);
                }
                if (hole != null)
                {
                    string r = roleNeg != "" ? roleNeg : rolePos;
                    if (hole.Y1 < y1) HFace(r, alongU, w0, w1, x0, x1, hole.Y1, false);    // the lintel's underside
                }
            }
            // jambs: the wall ends either side of each hole, and the run's own ends
            string jr = roleNeg != "" ? roleNeg : rolePos;
            if (cap0) EndFace(jr, alongU, w0, w1, a0, y0, y1, -1, floor);
            if (cap1) EndFace(jr, alongU, w0, w1, a1, y0, y1, +1, floor);
            foreach (var h in holes)
            {
                if (h.A0 > a0 + 0.01f) EndFace(jr, alongU, w0, w1, h.A0, y0, h.Y1, +1, floor);
                if (h.A1 < a1 - 0.01f) EndFace(jr, alongU, w0, w1, h.A1, y0, h.Y1, -1, floor);
            }
        }

        /// <summary>The outer walls inside the facade: from the facade line in to the rooms, with every opening the
        /// exterior made cut through (glass at the back of windows, the entrance left open) and the rooms' faces.</summary>
        private void OuterWalls(int st, float f, float c, bool top)
        {
            float y0 = st == 0 ? f - 0.3f : C(st - 1), y1 = top ? F(s.Levels) : c;
            for (int side = 0; side < 4; side++)
            {
                bool alongU = side < 2;
                float facade = side switch { 0 => _vF, 1 => _depth, 2 => _uMin, _ => _uMax };
                float inner = side switch { 0 => _v0, 1 => _v1, 2 => _u0, _ => _u1 };
                float sign = side is 0 or 2 ? 1f : -1f;                       // from the facade inward
                float a0 = alongU ? _uMin : _vF, a1 = alongU ? _uMax : _depth;
                var normal = (alongU ? _f.AxisV : _f.AxisU) * sign;           // the inner face looks into the building
                var holes = _segOpen.Where(o => o.Side == side && MathF.Min(o.Y1, c) - MathF.Max(o.Y0, f) > 0.3f)
                                     // a hair inside the exterior's hole, so the interior's reveals run out to the facade in
                                     // front of the exterior's own instead of leaving a crack where they meet (D83)
                                     .Select(o => new Hole(o.A0 + Inset, o.A1 - Inset, o.Kind is "door" or "balcony_door" ? MathF.Max(o.Y0, f) : MathF.Max(o.Y0, f) + Inset, MathF.Min(o.Y1, c) - Inset, o.Kind)).ToList();
                // break at holes and room corners
                var cols = new SortedSet<float> { a0, a1 };
                foreach (var h in holes) { cols.Add(h.A0); cols.Add(h.A1); }
                foreach (var r in _rooms) foreach (var q in r.Rects) { cols.Add(alongU ? q.Min.X : q.Min.Y); cols.Add(alongU ? q.Max.X : q.Max.Y); }
                var xs = cols.Where(x => x >= a0 && x <= a1).ToList();
                for (int i = 0; i + 1 < xs.Count; i++)
                {
                    float x0 = xs[i], x1 = xs[i + 1];
                    if (x1 - x0 < 0.005f) continue;
                    float xm = (x0 + x1) * 0.5f;
                    var room = alongU ? RoomAt(xm, inner + sign * 0.1f) : RoomAt(inner + sign * 0.1f, xm);
                    string role = WallRole(room?.Kind, room?.Flat);
                    var hole = holes.FirstOrDefault(h => xm > h.A0 && xm < h.A1);
                    var pieces = hole == null ? [(y0, y1)] : new List<(float, float)> { (y0, hole.Y0), (hole.Y1, y1) };
                    foreach (var (p0, p1) in pieces)
                    {
                        if (p1 - p0 < 0.01f) continue;
                        if (role != "") Face(role, alongU, inner, x0, x1, MathF.Max(p0, f), MathF.Min(p1, c), normal, f);
                    }
                    if (hole is { Kind: "loggia" })
                    {
                        // the room's wall stands at the loggia's back, all glass (it opens, like a balcony door), and the
                        // loggia's floor is concrete, not the room's parquet; the exterior draws its sides and ceiling
                        float backL = facade + sign * (LoggiaD - 0.01f);
                        Face("window", alongU, backL, x0, x1, f, hole.Y1, normal, f);
                        if (role != "") Face(role, alongU, backL, x0, x1, hole.Y1, c, normal, f);   // over the glass, to the ceiling
                        float l0 = MathF.Min(facade, backL), l1 = MathF.Max(facade, backL);
                        if (alongU) HQuad("loggia_floor", x0, x1, l0, l1, f + 0.004f, true); else HQuad("loggia_floor", l0, l1, x0, x1, f + 0.004f, true);
                        continue;
                    }
                    if (hole == null || role == "") continue;
                    // the reveal back to the glass (or, for the entrance, to the exterior's own reveal)
                    bool door = hole.Kind is "door" or "balcony_door";
                    float back = facade + sign * GlassAt;
                    float w0 = MathF.Min(facade, inner), w1 = MathF.Max(facade, inner);      // the reveal, out to the facade
                    if (!(door && hole.Y0 <= f + 0.06f)) HFace(role, alongU, w0, w1, x0, x1, hole.Y0, true);
                    HFace(role, alongU, w0, w1, x0, x1, hole.Y1, false);
                    if (!door)
                    {
                        Face(hole.Kind switch { "glass_block" => "glass_block", "shut" => "door_leaf", _ => "window" }, alongU, back, x0, x1, hole.Y0, hole.Y1, normal, f);
                    }
                }
                // physics: the whole wall, glass and all, with only the street doors left open
                float px = a0;
                foreach (var h in holes.Where(h => h.Kind is "door" or "balcony_door" or "loggia").OrderBy(h => h.A0))
                {
                    Box(alongU, MathF.Min(facade, inner), MathF.Max(facade, inner), px, h.A0, y0, y1);
                    Box(alongU, MathF.Min(facade, inner), MathF.Max(facade, inner), h.A0, h.A1, h.Y1, y1);
                    px = h.A1;
                }
                Box(alongU, MathF.Min(facade, inner), MathF.Max(facade, inner), px, a1, y0, y1);
                foreach (var h in holes)
                {
                    var room = alongU ? RoomAt((h.A0 + h.A1) * 0.5f, inner + sign * 0.1f) : RoomAt(inner + sign * 0.1f, (h.A0 + h.A1) * 0.5f);
                    string role = WallRole(room?.Kind, room?.Flat);
                    if (role == "") continue;
                    float w0 = MathF.Min(facade, inner), w1 = MathF.Max(facade, inner);
                    if (h.Kind == "loggia")
                    {
                        // the recess's sides seen from the rooms beside it, its floor and front rail, and their physics
                        float backL = facade + sign * LoggiaD, s0 = MathF.Min(inner, backL), s1 = MathF.Max(inner, backL);
                        var left = alongU ? RoomAt(h.A0 - 0.2f, inner + sign * 0.6f) : RoomAt(inner + sign * 0.6f, h.A0 - 0.2f);
                        var right = alongU ? RoomAt(h.A1 + 0.2f, inner + sign * 0.6f) : RoomAt(inner + sign * 0.6f, h.A1 + 0.2f);
                        EndFace(WallRole(left?.Kind, left?.Flat) is { Length: > 0 } rl ? rl : role, alongU, s0, s1, h.A0, f, h.Y1, -1, f);
                        EndFace(WallRole(right?.Kind, right?.Flat) is { Length: > 0 } rr ? rr : role, alongU, s0, s1, h.A1, f, h.Y1, +1, f);
                        Box(alongU, s0, s1, h.A0 - 0.12f, h.A0, f, h.Y1);
                        Box(alongU, s0, s1, h.A1, h.A1 + 0.12f, f, h.Y1);
                        Box(alongU, MathF.Min(facade, inner), MathF.Max(facade, inner), h.A0, h.A1, f - 0.25f, f);
                        Box(alongU, MathF.Min(facade, facade + sign * 0.1f), MathF.Max(facade, facade + sign * 0.1f), h.A0, h.A1, f, f + 1.15f);
                        float mid = (h.A0 + h.A1) * 0.5f;
                        var at = alongU ? _f.P(mid, f, facade) : _f.P(facade, f, mid);
                        _m.Balconies.Add(new BalconySite(at, alongU ? _f.AxisV * -sign : _f.AxisU * -sign, h.A1 - h.A0, LoggiaD) { Loggia = true });
                        continue;
                    }
                    if (h.Kind == "balcony_door" && st >= 0) Balcony(side, facade, sign, h, f);
                    // the jambs face into the opening
                    EndFace(role, alongU, w0, w1, h.A0, h.Y0, h.Y1, +1, f);
                    EndFace(role, alongU, w0, w1, h.A1, h.Y0, h.Y1, -1, f);
                }
            }
        }

        private const float LoggiaD = 1.3f;

        /// <summary>This storey's loggias in the section (D36): the side, the span along it, and the band across it they
        /// cut out of the rooms (from the facade to their back).</summary>
        private IEnumerable<(int Side, float A0, float A1, float Back0, float Back1)> Loggias(float f)
        {
            foreach (var o in _segOpen)
            {
                if (o.Kind != "loggia" || MathF.Abs(o.Y0 - f) > 0.4f) continue;
                float facade = o.Side switch { 0 => _vF, 1 => _depth, 2 => _uMin, _ => _uMax };
                float sign = o.Side is 0 or 2 ? 1f : -1f;
                float b0 = MathF.Min(facade, facade + sign * LoggiaD), b1 = MathF.Max(facade, facade + sign * LoggiaD);
                yield return (o.Side, o.A0, o.A1, b0, b1);
            }
        }

        private bool InLoggia(float f, float u, float v)
        {
            foreach (var lg in Loggias(f))
            {
                float along = lg.Side < 2 ? u : v, across = lg.Side < 2 ? v : u;
                if (along > lg.A0 && along < lg.A1 && across > lg.Back0 - 0.01f && across < lg.Back1 + Half) return true;
            }
            return false;
        }

        /// <summary>A balcony outside a balcony door (D35): the exterior draws it; the interior gives it a floor and
        /// railings a little higher than the drawn ones, and records it so a game can let you out there.</summary>
        private void Balcony(int side, float facade, float sign, Hole h, float f)
        {
            bool alongU = side < 2;
            float mid = (h.A0 + h.A1) * 0.5f;
            var at = alongU ? _f.P(mid, f, facade) : _f.P(facade, f, mid);
            var site = s.Balconies.Where(b => !b.Loggia && MathF.Abs(b.Position.Y - f) < 0.4f).OrderBy(b => Vector2.Distance(new Vector2(b.Position.X, b.Position.Z), new Vector2(at.X, at.Z))).FirstOrDefault();
            if (site == null || Vector2.Distance(new Vector2(site.Position.X, site.Position.Z), new Vector2(at.X, at.Z)) > 0.8f) return;
            _m.Balconies.Add(site with { Position = at });
            if (site.Depth < 0.3f) return;   // a garden door: the ground is the floor
            float hw = MathF.Max(site.Width * 0.5f, (h.A1 - h.A0) * 0.5f + 0.2f), d = site.Depth, o = -sign;   // outward along the axis
            float w0 = MathF.Min(facade, facade + o * d), w1 = MathF.Max(facade, facade + o * d);
            Box(alongU, w0, w1, mid - hw, mid + hw, f - 0.25f, f);                                         // the slab
            float wf = facade + o * d;                                                                       // the front rail
            Box(alongU, MathF.Min(wf, wf - o * 0.06f), MathF.Max(wf, wf - o * 0.06f), mid - hw, mid + hw, f, f + 1.15f);
            Box(alongU, w0, w1, mid - hw - 0.06f, mid - hw, f, f + 1.15f);                                // the sides
            Box(alongU, w0, w1, mid + hw, mid + hw + 0.06f, f, f + 1.15f);
        }

        /// <summary>The staircase: a spine wall between the flights, the flight up from the landing, the half landing
        /// at the back and the flight on up to the next floor; a rail across the stairwell on the top floor.</summary>
        private void Stairs(int st, float f, bool top)
        {
            float sh = s.StoreyHeight, rise = sh * 0.5f;
            int n = Math.Max(6, (int)MathF.Round(rise / 0.17f));
            float h = rise / n, vA = _v0 + Landing, vB = _dc - HalfLanding, r = (vB - vA) / n;
            foreach (var e in _cores)
            {
                float inL = e - CoreHalf + Half, inR = e + CoreHalf - Half;
                float spineTop = top ? C(st) : F(st + 1);
                // the spine
                foreach (float w in new[] { e - Half, e + Half })
                    Face("lamperia", false, w, vA, vB, f, spineTop, w < e ? -_f.AxisU : _f.AxisU, f);
                VQuad("lamperia", true, vA, e - Half, e + Half, f, spineTop, -_f.AxisV);
                VQuad("lamperia", true, vB, e - Half, e + Half, f, spineTop, _f.AxisV);
                Box(false, e - Half, e + Half, vA, vB, f, spineTop);
                if (top)
                {
                    // nothing climbs on from the top floor: a rail keeps you out of the stairwell
                    Rail(inL, e - Half, vA, f);
                    continue;
                }
                // up from the landing (left), the half landing, on up (right)
                for (int k = 0; k < n; k++)
                {
                    float va = vA + k * r, vb = va + r, yt = f + (k + 1) * h;
                    HQuad("stair", inL, e - Half, va, vb, yt, true);
                    VQuad("stair", true, va, inL, e - Half, yt - h, yt, -_f.AxisV);
                    float wa = vB - (k + 1) * r, wb = vB - k * r, yt2 = f + rise + (k + 1) * h;
                    HQuad("stair", e + Half, inR, wa, wb, yt2, true);
                    VQuad("stair", true, wb, e + Half, inR, yt2 - h, yt2, _f.AxisV);
                }
                // soffits under both flights
                Soffit(inL, e - Half, vA, f - 0.25f, vB, f + rise - 0.25f);
                Soffit(e + Half, inR, vB, f + rise - 0.25f, vA, f + sh - 0.25f);
                // the half landing, a slab
                float hl = f + rise;
                HQuad("terrazzo", inL, inR, vB, _dc - Half, hl, true);
                HQuad("ceiling", inL, inR, vB, _dc - Half, hl - 0.25f, false);
                VQuad("terrazzo", true, vB, inL, e - Half, hl - 0.25f, hl - h, -_f.AxisV);
                VQuad("terrazzo", true, vB, e + Half, inR, hl - 0.25f, hl, -_f.AxisV);
                Box(true, vB, _dc - Half, inL, inR, hl - 0.25f, hl);
                // physics: ramps through the step nosings
                Ramp(inL, e - Half, vA - r, f, vB - r, hl);
                Ramp(e + Half, inR, vB + r, hl, vA + r, f + sh);
            }
        }

        private void Rail(float u0, float u1, float v, float f)
        {
            float t = 0.03f;
            Box(true, v - t, v + t, u0, u1, f, f + 1.0f);
            VQuad("rail", true, v - t, u0, u1, f + 0.95f, f + 1.0f, -_f.AxisV);
            VQuad("rail", true, v + t, u0, u1, f + 0.95f, f + 1.0f, _f.AxisV);
            HQuad("rail", u0, u1, v - t, v + t, f + 1.0f, true);
            for (float u = u0 + 0.12f; u < u1; u += 0.14f)
            {
                VQuad("rail", true, v - 0.01f, u - 0.01f, u + 0.01f, f, f + 0.95f, -_f.AxisV);
                VQuad("rail", true, v + 0.01f, u - 0.01f, u + 0.01f, f, f + 0.95f, _f.AxisV);
            }
        }

        /// <summary>Floor slabs for physics: the whole ground floor; above it, everything but the stairwells.</summary>
        private void Floors(int st, float f)
        {
            float t = st == 0 ? 0.3f : Slab;
            if (st == 0) { Box(true, _v0, _v1, _u0, _u1, f - t, f); return; }
            float cur = _u0;
            foreach (var e in _cores)
            {
                Box(true, _v0, _v1, cur, e - CoreHalf, f - t, f);
                Box(true, _v0, _v0 + Landing, e - CoreHalf, e + CoreHalf, f - t, f);
                Box(true, _dc, _v1, e - CoreHalf, e + CoreHalf, f - t, f);
                cur = e + CoreHalf;
            }
            Box(true, _v0, _v1, cur, _u1, f - t, f);
        }

        /// <summary>The way up to each door from the pavement (the plinth puts the ground floor up to a metre above it):
        /// a step in front and a ramp down to below the lowest ground; and jambs either side, so nobody slips into the
        /// wall beside the door while the building's solid mass is switched off for them.</summary>
        private void Approach()
        {
            float f = F(0);
            foreach (var e in _cores)
            {
                // a landing 2.6 m wide, with ramps down its front and both sides (D37: coming at the door at an angle
                // you met the landing's side, up to 0.9 m high)
                const float hw = 1.3f, deep = 1.2f;
                Box(true, _vF - deep, _vF, e - hw, e + hw, s.GroundLow - 0.6f, f);
                float drop = f - (s.GroundLow - 0.4f);
                if (drop > 0.05f)
                {
                    float run = drop / MathF.Tan(28f * MathF.PI / 180f);
                    Ramp(e - hw - run, e + hw + run, _vF - deep - run, f - drop, _vF - deep, f);
                    TiltBox(_f.P(e - hw - run, f - drop, _vF - deep * 0.5f), _f.P(e - hw, f, _vF - deep * 0.5f), deep * 0.5f, _f.AxisV);
                    TiltBox(_f.P(e + hw + run, f - drop, _vF - deep * 0.5f), _f.P(e + hw, f, _vF - deep * 0.5f), deep * 0.5f, _f.AxisV);
                }
                // jambs on the wall line only (D37: 0.6 m deep they caught you coming at the door at an angle)
                Box(true, _vF - 0.12f, _vF, e - 1.25f, e - 0.8f, f, f + 2.4f);
                Box(true, _vF - 0.12f, _vF, e + 0.8f, e + 1.25f, f, f + 2.4f);
            }
        }

        // ---- geometry helpers -------------------------------------------------------------------------------------

        /// <summary>A vertical face on a line (v = w when alongU, else u = w), from a0 to a1 along it; "lamperia" paints
        /// the lower 1.5 m above <paramref name="floor"/>.</summary>
        private void Face(string role, bool alongU, float w, float a0, float a1, float y0, float y1, Vector3 normal, float floor)
        {
            if (role == "lamperia")
            {
                float split = floor + 1.5f;
                if (y0 < split) Face("stair_paint", alongU, w, a0, a1, y0, MathF.Min(y1, split), normal, floor);
                if (y1 > split) Face("stair_wall", alongU, w, a0, a1, MathF.Max(y0, split), y1, normal, floor);
                return;
            }
            if (a1 - a0 < 1e-3f || y1 - y0 < 1e-3f) return;
            Vector3 P(float a, float y) => alongU ? _f.P(a, y, w) : _f.P(w, y, a);
            _m.Mesh.AddQuad(role, P(a0, y0), P(a1, y0), P(a1, y1), P(a0, y1), normal);
        }

        private void VQuad(string role, bool alongU, float w, float a0, float a1, float y0, float y1, Vector3 normal) => Face(role, alongU, w, a0, a1, y0, y1, normal, y0);

        /// <summary>A wall's end face at <paramref name="a"/> across its thickness, facing <paramref name="dir"/> along it.</summary>
        private void EndFace(string role, bool alongU, float w0, float w1, float a, float y0, float y1, int dir, float floor)
        {
            if (role == "lamperia")
            {
                float split = floor + 1.5f;
                if (y0 < split) EndFace("stair_paint", alongU, w0, w1, a, y0, MathF.Min(y1, split), dir, floor);
                if (y1 > split) EndFace("stair_wall", alongU, w0, w1, a, MathF.Max(y0, split), y1, dir, floor);
                return;
            }
            if (y1 - y0 < 1e-3f) return;
            var n = (alongU ? _f.AxisU : _f.AxisV) * dir;
            Vector3 P(float w, float y) => alongU ? _f.P(a, y, w) : _f.P(w, y, a);
            _m.Mesh.AddQuad(role, P(w0, y0), P(w1, y0), P(w1, y1), P(w0, y1), n);
        }

        /// <summary>A horizontal face inside a wall's thickness (a sill or a lintel's underside).</summary>
        private void HFace(string role, bool alongU, float w0, float w1, float a0, float a1, float y, bool up)
        {
            if (role == "lamperia") role = up ? "stair_paint" : "stair_wall";
            if (alongU) HQuad(role, a0, a1, w0, w1, y, up); else HQuad(role, w0, w1, a0, a1, y, up);
        }

        private void HQuad(string role, float u0, float u1, float v0, float v1, float y, bool up)
        {
            if (u1 - u0 < 1e-3f || v1 - v0 < 1e-3f) return;
            _m.Mesh.AddQuad(role, _f.P(u0, y, v0), _f.P(u1, y, v0), _f.P(u1, y, v1), _f.P(u0, y, v1), up ? Vector3.UnitY : -Vector3.UnitY);
        }

        private void Soffit(float u0, float u1, float va, float ya, float vb, float yb)
        {
            var dir = _f.AxisV * (vb - va) + Vector3.UnitY * (yb - ya);
            var n = Vector3.Normalize(Vector3.Cross(dir, _f.AxisU));
            if (n.Y > 0) n = -n;
            _m.Mesh.AddQuad("ceiling", _f.P(u0, ya, va), _f.P(u1, ya, va), _f.P(u1, yb, vb), _f.P(u0, yb, vb), n);
        }

        /// <summary>A door leaf standing open: for the way into a flat, hinged at the far end of the hole and swung
        /// against the wall inside room B.</summary>
        private void Leaf(DoorPlan dp, float f)
        {
            // which side is B: sample just beside the door's middle
            float off = 0.2f;
            var bSide = dp.AlongU ? RoomAt(dp.Mid, dp.C + off) : RoomAt(dp.C + off, dp.Mid);
            float sgn = bSide != null && bSide.Id == dp.B ? 1f : -1f;
            float hinge = dp.Mid + dp.Width * 0.5f - 0.03f, w = dp.C + sgn * Half;
            float l0 = MathF.Min(w, w + sgn * dp.Width * 0.95f), l1 = MathF.Max(w, w + sgn * dp.Width * 0.95f);
            float t0 = hinge - 0.04f, t1 = hinge;
            // the leaf is a thin box at right angles to the wall
            if (dp.AlongU) LeafBox(t0, t1, l0, l1, f, f + dp.Height - 0.02f);
            else LeafBox(l0, l1, t0, t1, f, f + dp.Height - 0.02f);
        }

        private void LeafBox(float u0, float u1, float v0, float v1, float y0, float y1)
        {
            HQuad("door_leaf", u0, u1, v0, v1, y1, true);
            VQuad("door_leaf", true, v0, u0, u1, y0, y1, -_f.AxisV);
            VQuad("door_leaf", true, v1, u0, u1, y0, y1, _f.AxisV);
            VQuad("door_leaf", false, u0, v0, v1, y0, y1, -_f.AxisU);
            VQuad("door_leaf", false, u1, v0, v1, y0, y1, _f.AxisU);
        }

        /// <summary>An upright physics box: <paramref name="w0"/>..<paramref name="w1"/> across the line, a0..a1 along.</summary>
        private void Box(bool alongU, float w0, float w1, float a0, float a1, float y0, float y1)
        {
            float u0 = alongU ? a0 : w0, u1 = alongU ? a1 : w1, v0 = alongU ? w0 : a0, v1 = alongU ? w1 : a1;
            if (u1 - u0 < 0.005f || v1 - v0 < 0.005f || y1 - y0 < 0.005f) return;
            var centre = _f.P((u0 + u1) * 0.5f, (y0 + y1) * 0.5f, (v0 + v1) * 0.5f);
            _m.Colliders.Add(new Collider(centre, new Vector3((u1 - u0) * 0.5f, (y1 - y0) * 0.5f, (v1 - v0) * 0.5f), _f.AxisU, Vector3.UnitY));
        }

        /// <summary>A tilted physics box whose top runs from (va, ya) to (vb, yb), u0..u1 across.</summary>
        private void Ramp(float u0, float u1, float va, float ya, float vb, float yb) =>
            TiltBox(_f.P((u0 + u1) * 0.5f, ya, va), _f.P((u0 + u1) * 0.5f, yb, vb), (u1 - u0) * 0.5f, _f.AxisU);

        /// <summary>A tilted physics box whose top runs from <paramref name="a"/> to <paramref name="b"/> (building
        /// space), <paramref name="halfAcross"/> to each side along <paramref name="across"/>.</summary>
        private void TiltBox(Vector3 a, Vector3 b, float halfAcross, Vector3 across)
        {
            const float t = 0.15f;
            var along = b - a; float len = along.Length();
            var z = along / len;
            var y = Vector3.Normalize(Vector3.Cross(z, across));
            if (y.Y < 0) y = -y;
            var x = Vector3.Normalize(Vector3.Cross(y, z));
            var centre = (a + b) * 0.5f - y * t;
            _m.Colliders.Add(new Collider(centre, new Vector3(halfAcross, t, len * 0.5f), x, y));
        }

        // ---- validation -------------------------------------------------------------------------------------------

        /// <summary>Every room reachable from the street door (doorways, and the stairs between storeys); every flat
        /// has a door onto a landing; no room smaller than a bathroom.</summary>
        private void Validate()
        {
            var adj = new Dictionary<string, List<string>>();
            void Link(string a, string b)
            {
                (adj.TryGetValue(a, out var la) ? la : adj[a] = []).Add(b);
                (adj.TryGetValue(b, out var lb) ? lb : adj[b] = []).Add(a);
            }
            foreach (var d in _m.Doors) Link(d.From, d.To);
            var stairs = _m.Rooms.Where(r => r.Kind == "stair").ToList();
            foreach (var r in stairs)
            {
                var up = stairs.FirstOrDefault(o => o.Storey == r.Storey + 1 && o.Id.Replace($"/s{o.Storey}/", "/") == r.Id.Replace($"/s{r.Storey}/", "/"));
                if (up != null) Link(r.Id, up.Id);
            }
            var seen = new HashSet<string>();
            var queue = new Queue<string>(stairs.Where(r => r.Storey == 0).Select(r => r.Id));
            foreach (var q in queue) seen.Add(q);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!adj.TryGetValue(cur, out var next)) continue;
                foreach (var nx in next) if (seen.Add(nx)) queue.Enqueue(nx);
            }
            int lost = _m.Rooms.Count(r => !seen.Contains(r.Id));
            if (lost > 0) _m.Report.Add(Severity.Error, "interior.unreachable", $"{lost} rooms can't be reached from the street door.");
            int tiny = _m.Rooms.Count(r => r.Area < 2.5f);
            if (tiny > 0) _m.Report.Add(Severity.Warn, "interior.tiny_room", $"{tiny} rooms under 2.5 m².");
            if (_m.Sealed.Count > 0) _m.Report.Add(Severity.Info, "interior.sealed_door", $"{_m.Sealed.Count} entrances stay shut (no staircase fits behind them).", "locked door");
            _m.Report.Metrics["rooms"] = _m.Rooms.Count;
            _m.Report.Metrics["flats"] = _m.Rooms.Select(r => r.Flat).Where(x => x != null).Distinct().Count();
            _m.Report.Metrics["staircases"] = _m.Entrances.Count;
            _m.Report.Metrics["sections"] = _sections;
            _m.Report.Metrics["colliders"] = _m.Colliders.Count;
            _m.Report.Metrics["triangles"] = _m.Mesh.TriangleCount;
        }
    }
}
