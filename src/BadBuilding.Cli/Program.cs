using System.Globalization;
using System.Numerics;
using System.Text;
using BadBuilding.Export;
using BadBuilding.Generation;
using BadBuilding.Model;

namespace BadBuilding.Cli;

/// <summary>
/// badbuilding gallery [--samples samples/warsaw.json] [--out out/gallery] [--seeds 2]
///     every sample (real and synthetic) through the generator, its files, and an HTML page with a 3D view and a plan
///     per building: footprint, neighbours, streets, wall exposure, entrances, attributes with their sources, warnings.
/// badbuilding generate --samples ... --case long_prl_slab [--index 0] [--seed N] [--stage-seed facade=3] --out dir
/// badbuilding stress [--count 500]
///     random footprints: no exceptions, no non-finite vertices, nothing outside the outline.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 0) { Console.WriteLine("usage: badbuilding gallery|generate|stress [options]"); return 1; }
        var opt = Options(args.Skip(1));
        return args[0] switch
        {
            "gallery" => Gallery(opt),
            "generate" => GenerateOne(opt),
            "stress" => Stress(opt),
            _ => Fail($"unknown command {args[0]}"),
        };
    }

    private static int Fail(string msg) { Console.Error.WriteLine(msg); return 1; }

    private static Dictionary<string, List<string>> Options(IEnumerable<string> args)
    {
        var d = new Dictionary<string, List<string>>();
        string? key = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--")) { key = a[2..]; if (!d.ContainsKey(key)) d[key] = []; }
            else if (key != null) d[key].Add(a);
        }
        return d;
    }

    private static string Get(Dictionary<string, List<string>> o, string k, string def) => o.TryGetValue(k, out var v) && v.Count > 0 ? v[0] : def;

    private static Dictionary<string, ulong> StageSeeds(Dictionary<string, List<string>> o) =>
        o.TryGetValue("stage-seed", out var v) ? v.Select(s => s.Split('=')).ToDictionary(p => p[0], p => ulong.Parse(p[1])) : new();

    private static int GenerateOne(Dictionary<string, List<string>> o)
    {
        var samples = Samples.Load(Get(o, "samples", "samples/warsaw.json"));
        var c = Get(o, "case", "synthetic_slab");
        var list = samples.Where(s => s.Case == c).ToList();
        if (list.Count == 0) return Fail($"no sample case '{c}' (cases: {string.Join(", ", samples.Select(s => s.Case).Distinct())})");
        var s = list[int.Parse(Get(o, "index", "0"))];
        var req = s.Request with { StageSeeds = StageSeeds(o) };
        if (o.ContainsKey("seed")) req = req with { Seed = ulong.Parse(Get(o, "seed", "0")) };
        var b = BuildingGenerator.Generate(req);
        var outDir = Get(o, "out", "out/one");
        BuildingFiles.Save(b, outDir, Safe(req.Id));
        Console.WriteLine($"{req.Id}: {b.Plan?.Style ?? "none"}, {b.TriangleCount} triangles, {b.Report.Status} → {outDir}");
        foreach (var i in b.Report.Issues) Console.WriteLine($"  {i.Severity} {i.Code}: {i.Message}{(i.Fallback != null ? $" ({i.Fallback})" : "")}");
        return b.Report.HasErrors ? 2 : 0;
    }

    private static int Stress(Dictionary<string, List<string>> o)
    {
        int count = int.Parse(Get(o, "count", "500"));
        var rng = new Rng(12345);
        int failures = 0, errors = 0;
        for (int i = 0; i < count; i++)
        {
            int n = rng.Int(3, 12);
            float radius = rng.Range(3, 60);
            var pts = new List<Vector2>();
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * MathF.Tau + rng.Range(-0.2f, 0.2f);
                float rr = radius * rng.Range(0.4f, 1.0f);
                pts.Add(new Vector2(MathF.Cos(a) * rr * rng.Range(0.5f, 2f), MathF.Sin(a) * rr));
            }
            var req = new BuildingRequest { Id = $"stress:{i}", Seed = (ulong)i, Outer = new Ring(pts), Levels = rng.Chance(0.5) ? rng.Int(1, 16) : null, Type = rng.Pick(new[] { "yes", "apartments", "house", "garage", "retail" }) };
            try
            {
                var b = BuildingGenerator.Generate(req);
                if (b.Report.HasErrors) errors++;
                var problems = Checks.Inside(b);
                if (problems != null) { failures++; Console.WriteLine($"stress {i}: {problems}"); }
            }
            catch (Exception e) { failures++; Console.WriteLine($"stress {i}: {e.GetType().Name}: {e.Message}"); }
        }
        Console.WriteLine($"{count} footprints: {failures} failures, {errors} rejected with an error report");
        return failures > 0 ? 2 : 0;
    }

    private static string Safe(string id) => new(id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

    private static int Gallery(Dictionary<string, List<string>> o)
    {
        var samples = Samples.Load(Get(o, "samples", "samples/warsaw.json"));
        int seeds = int.Parse(Get(o, "seeds", "1"));
        var outDir = Get(o, "out", "out/gallery");
        Directory.CreateDirectory(outDir);
        var cards = new StringBuilder();
        int n = 0;
        foreach (var s in samples)
            for (int k = 0; k < seeds; k++)
            {
                var req = k == 0 ? s.Request : s.Request with { Seed = s.Request.Seed + (ulong)k * 7919 };
                var b = BuildingGenerator.Generate(req);
                string name = $"{n:000}_{Safe(s.Case)}_{k}";
                BuildingFiles.Save(b, outDir, name);
                cards.Append(Card(s, b, name, k));
                n++;
                Console.WriteLine($"{name}: {b.Plan?.Style ?? "none"}, {b.TriangleCount} tris, {b.Report.Status}");
            }
        File.WriteAllText(Path.Combine(outDir, "index.html"), Page(cards.ToString()), Encoding.UTF8);
        Console.WriteLine($"{n} buildings → {Path.Combine(outDir, "index.html")}");
        return 0;
    }

    private static string Card(Sample s, Building b, string name, int seedIndex)
    {
        var p = b.Plan;
        var sb = new StringBuilder();
        sb.Append($"<div class='card' data-glb='{name}.glb'>");
        sb.Append($"<h3>{s.Case}{(seedIndex > 0 ? $" · seed {seedIndex + 1}" : "")} <span class='{(s.Synthetic ? "syn" : "real")}'>{(s.Synthetic ? "synthetic" : "real OSM")}</span></h3>");
        sb.Append($"<div class='id'>{s.Request.Id} · {s.Note}</div>");
        sb.Append(Plan(s, b));
        sb.Append("<table>");
        string Src(Source x) => x == Source.Inferred ? "<i>inferred</i>" : x == Source.Osm ? "osm" : "request";
        if (p != null)
        {
            sb.Append($"<tr><td>style</td><td>{p.Style}</td></tr>");
            sb.Append($"<tr><td>type</td><td>{s.Request.Type ?? "—"}</td></tr>");
            sb.Append($"<tr><td>levels</td><td>{p.Levels.Value} ({Src(p.Levels.Source)})</td></tr>");
            sb.Append($"<tr><td>height</td><td>{p.Height.Value:0.0} m ({Src(p.Height.Source)})</td></tr>");
            sb.Append($"<tr><td>roof</td><td>{p.Roof.Value} ({Src(p.Roof.Source)})</td></tr>");
            sb.Append($"<tr><td>palette</td><td>{p.Palette} (<i>inferred</i>)</td></tr>");
            int mapped = s.Request.Entrances.Count;
            sb.Append($"<tr><td>entrances</td><td>{b.Markers.Count(m => m.Kind == "entrance")} ({(mapped > 0 ? $"{mapped} mapped" : "<i>inferred</i>")})</td></tr>");
            var ex = p.Edges.GroupBy(e => e.Exposure).Select(g => $"{g.Key.ToString().ToLowerInvariant()} {g.Count()}");
            sb.Append($"<tr><td>walls</td><td>{string.Join(", ", ex)}</td></tr>");
        }
        sb.Append($"<tr><td>mesh</td><td>{b.TriangleCount} triangles</td></tr>");
        sb.Append($"<tr><td>status</td><td class='st-{b.Report.Status}'>{b.Report.Status}</td></tr></table>");
        if (b.Report.Issues.Count > 0)
            sb.Append("<ul>" + string.Concat(b.Report.Issues.Select(i => $"<li class='sev-{i.Severity.ToString().ToLowerInvariant()}'>{i.Code}: {System.Net.WebUtility.HtmlEncode(i.Message)}{(i.Fallback != null ? $" → {i.Fallback}" : "")}</li>")) + "</ul>");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The plan: neighbours grey, streets, the footprint with each wall coloured by exposure, entrances.</summary>
    private static string Plan(Sample s, Building b)
    {
        var r = s.Request;
        var pts = r.Outer.Points.ToList();
        float minX = pts.Min(p => p.X) - 15, maxX = pts.Max(p => p.X) + 15, minZ = pts.Min(p => p.Y) - 15, maxZ = pts.Max(p => p.Y) + 15;
        float w = maxX - minX, h = maxZ - minZ;
        string Poly(IEnumerable<Vector2> ring) => string.Join(" ", ring.Select(p => $"{p.X:0.##},{p.Y:0.##}"));
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox='{minX:0.#} {minZ:0.#} {w:0.#} {h:0.#}' class='plan'>");
        sb.Append($"<rect x='{minX}' y='{minZ}' width='{w}' height='{h}' class='bg'/>");
        foreach (var st in s.StreetsForPlan) sb.Append($"<polyline points='{Poly(st)}' class='street' style='stroke-width:{MathF.Max(w, h) / 60f:0.##}'/>");
        foreach (var nb in r.Neighbours) sb.Append($"<polygon points='{Poly(nb.Points)}' class='nb'/>");
        sb.Append($"<polygon points='{Poly(pts)}' class='fp'/>");
        foreach (var hole in r.Holes) sb.Append($"<polygon points='{Poly(hole.Points)}' class='hole'/>");
        float sw = MathF.Max(w, h) / 90f;
        if (b.Plan != null)
            foreach (var e in b.Plan.Edges)
                sb.Append($"<line x1='{e.A.X:0.##}' y1='{e.A.Y:0.##}' x2='{e.B.X:0.##}' y2='{e.B.Y:0.##}' class='ex-{e.Exposure.ToString().ToLowerInvariant()}' style='stroke-width:{sw:0.##}'/>");
        foreach (var m in b.Modules.Where(m => m.Type == "passage"))
        {
            var ps = m.Mesh.Positions;
            float x0 = ps.Min(p => p.X), x1 = ps.Max(p => p.X), z0 = ps.Min(p => p.Z), z1 = ps.Max(p => p.Z);
            sb.Append($"<rect x='{x0:0.##}' y='{z0:0.##}' width='{x1 - x0:0.##}' height='{z1 - z0:0.##}' class='passage'/>");
        }
        foreach (var m in b.Markers.Where(m => m.Kind == "entrance"))
            sb.Append($"<circle cx='{m.Position.X:0.##}' cy='{m.Position.Z:0.##}' r='{sw * 1.6f:0.##}' class='{(r.Entrances.Count > 0 ? "door-osm" : "door-inf")}'/>");
        foreach (var e in r.Entrances) sb.Append($"<circle cx='{e.Position.X:0.##}' cy='{e.Position.Y:0.##}' r='{sw * 0.7f:0.##}' class='mapped'/>");
        // north arrow
        sb.Append($"<text x='{maxX - w * 0.06f:0.#}' y='{minZ + h * 0.1f:0.#}' style='font-size:{MathF.Max(w, h) / 18f:0.#}px' class='north'>N↑</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Page(string cards) => """
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>BadBuilding gallery</title>
<style>
:root { --bg:#f4f1ea; --card:#fffdf8; --ink:#222; --muted:#6b665c; --line:#dcd5c6; }
@media (prefers-color-scheme: dark) { :root { --bg:#16171a; --card:#202226; --ink:#ece7dc; --muted:#a49d8f; --line:#34363c; } }
body { margin:0; background:var(--bg); color:var(--ink); font:14px/1.45 system-ui, sans-serif; }
header { padding:16px 20px; } h1 { margin:0 0 4px; font-size:22px; } header p { margin:0; color:var(--muted); }
#viewer { position:sticky; top:0; height:46vh; background:#9fb3c6; z-index:2; border-bottom:1px solid var(--line); }
#viewer canvas { width:100%; height:100%; display:block; } #vlabel { position:absolute; left:12px; top:8px; color:#fff; text-shadow:0 1px 3px #000; }
.grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(300px, 1fr)); gap:14px; padding:16px 20px 40px; }
.card { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:10px 12px; cursor:pointer; }
.card.sel { outline:2px solid #e0a020; } .card h3 { margin:0; font-size:15px; } .id { color:var(--muted); font-size:12px; margin:2px 0 6px; }
.syn, .real { font-size:11px; padding:1px 6px; border-radius:9px; font-weight:600; } .syn { background:#e6d6f5; color:#4b2a6b; } .real { background:#d6efe0; color:#1f5133; }
svg.plan { width:100%; aspect-ratio:1/1; background:#fff; border-radius:4px; }
.bg { fill:#fbfaf6; } .street { fill:none; stroke:#c9c3b6; stroke-linecap:round; } .nb { fill:#d9d6cf; stroke:#b9b4aa; stroke-width:.3; }
.fp { fill:#f1e7cf; } .hole { fill:#fbfaf6; } .north { fill:#888; }
line { stroke-linecap:round; } .ex-street { stroke:#2f7fd8; } .ex-side { stroke:#7a7a7a; } .ex-courtyard { stroke:#2fa36b; } .ex-party { stroke:#d23c3c; } .ex-sliver { stroke:#e08a00; }
.passage { fill:#c9b48c; stroke:#8a6d3b; stroke-width:.3; stroke-dasharray:1 .6; } .door-osm { fill:#1d9e4a; } .door-inf { fill:#ff9b2f; } .mapped { fill:none; stroke:#000; stroke-width:.4; }
table { width:100%; font-size:12.5px; border-collapse:collapse; margin-top:6px; } td { padding:1px 4px; border-top:1px solid var(--line); } td:first-child { color:var(--muted); width:80px; }
.st-pass { color:#1d9e4a; } .st-warn { color:#d08a00; } .st-error { color:#d23c3c; font-weight:700; }
ul { margin:6px 0 0; padding-left:18px; font-size:12px; } .sev-error { color:#d23c3c; } .sev-warn { color:#b07400; } .sev-info { color:var(--muted); }
.legend span { margin-right:10px; white-space:nowrap; } .legend b { display:inline-block; width:14px; height:4px; vertical-align:middle; margin-right:4px; }
</style></head><body>
<header><h1>BadBuilding gallery</h1>
<p>Click a card to load it into the 3D view. Plan: walls by exposure — <span class="legend"><span><b style="background:#2f7fd8"></b>street</span><span><b style="background:#7a7a7a"></b>side</span><span><b style="background:#2fa36b"></b>courtyard</span><span><b style="background:#d23c3c"></b>party</span><span><b style="background:#e08a00"></b>sliver</span></span> · doors: green mapped, orange inferred · dashed tan: passage to a courtyard · grey: neighbours. Real footprints: map data © OpenStreetMap contributors (ODbL).</p></header>
<div id="viewer"><div id="vlabel">select a building</div></div>
<div class="grid">
""" + cards + """
</div>
<script type="importmap">{"imports":{"three":"https://unpkg.com/three@0.160.0/build/three.module.js","three/addons/":"https://unpkg.com/three@0.160.0/examples/jsm/"}}</script>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
const box = document.getElementById('viewer');
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.shadowMap.enabled = true;
box.appendChild(renderer.domElement);
const scene = new THREE.Scene(); scene.background = new THREE.Color(0x9fb3c6);
scene.add(new THREE.HemisphereLight(0xdfe8f0, 0x5a5040, 1.1));
const sun = new THREE.DirectionalLight(0xfff1dc, 2.2); sun.position.set(-60, 90, -40); sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048); Object.assign(sun.shadow.camera, { left: -120, right: 120, top: 120, bottom: -120, far: 400 }); scene.add(sun);
const ground = new THREE.Mesh(new THREE.PlaneGeometry(800, 800), new THREE.MeshStandardMaterial({ color: 0x7d8a5f, roughness: 1 }));
ground.rotation.x = -Math.PI / 2; ground.position.y = -0.05; ground.receiveShadow = true; scene.add(ground);
const camera = new THREE.PerspectiveCamera(45, 1, 0.5, 2000);
const controls = new OrbitControls(camera, renderer.domElement);
let current = null;
function size() { const w = box.clientWidth, h = box.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
addEventListener('resize', size); size();
const loader = new GLTFLoader();
function show(card) {
  document.querySelectorAll('.card.sel').forEach(c => c.classList.remove('sel')); card.classList.add('sel');
  document.getElementById('vlabel').textContent = card.querySelector('h3').textContent;
  loader.load(card.dataset.glb, g => {
    if (current) scene.remove(current);
    current = g.scene; current.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
    // collision nodes (Noor Interchange: extras.noor.collision, name ending in -col) are not drawn
    current.traverse(o => { if (o.userData?.noor?.collision || o.name.endsWith('-col')) o.visible = false; });
    scene.add(current);
    const b = new THREE.Box3().setFromObject(current), c = b.getCenter(new THREE.Vector3()), s = b.getSize(new THREE.Vector3()).length();
    controls.target.copy(c); camera.position.set(c.x + s * 0.7, c.y + s * 0.45, c.z + s * 0.8); controls.update();
  });
}
document.querySelectorAll('.card').forEach(c => c.addEventListener('click', () => show(c)));
const first = document.querySelector('.card'); if (first) show(first);
window.bb = { camera, controls, show: i => show(document.querySelectorAll('.card')[i]),
  look: (az, el, k) => { if (!current) return; const b = new THREE.Box3().setFromObject(current), c = b.getCenter(new THREE.Vector3()), s = b.getSize(new THREE.Vector3()).length() * k;
    camera.position.set(c.x + s * Math.cos(el) * Math.sin(az), c.y + s * Math.sin(el), c.z + s * Math.cos(el) * Math.cos(az)); controls.target.copy(c); controls.update(); } };
renderer.setAnimationLoop(() => { controls.update(); renderer.render(scene, camera); });
</script></body></html>
""";
}

/// <summary>Checks the CLI runs on every output (the unit tests run them too).</summary>
public static class Checks
{
    /// <summary>Nothing may be built far outside the footprint (D6): balconies and canopies may stand out ~1.5 m.</summary>
    public static string? Inside(Building b)
    {
        if (b.Modules.Count == 0) return null;
        var outer = b.Request.Outer.Points;
        float minX = outer.Min(p => p.X) - 2f, maxX = outer.Max(p => p.X) + 2f, minZ = outer.Min(p => p.Y) - 2f, maxZ = outer.Max(p => p.Y) + 2f;
        foreach (var m in b.Modules)
            foreach (var p in m.Mesh.Positions)
            {
                if (!float.IsFinite(p.X + p.Y + p.Z)) return $"{m.Id}: non-finite vertex";
                if (p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ) return $"{m.Id}: vertex {p} outside the footprint's box";
            }
        return null;
    }
}
