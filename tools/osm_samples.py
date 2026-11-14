#!/usr/bin/env python3
"""Cut a test set of real building footprints, with their context, out of an OSM .pbf (needs pyosmium).

    python tools/osm_samples.py <region.osm.pbf> samples/warsaw.json

Picks buildings inside a Warsaw box that match the difficult cases from docs/RECON_NOOR.md §6 (long PRL slab, L-block,
courtyard, touching row, wedge, house, outbuilding, tiny) and writes each with its neighbours within 60 m, street centre
lines within 60 m and mapped entrances, in local metres around the building (+X east, +Z south). Synthetic stress
shapes and a sloped-lot variant are added by the CLI, labelled as such.

Map data (c) OpenStreetMap contributors, ODbL 1.0: the output carries that notice.
"""
import json
import math
import sys

import osmium

BOX = (20.95, 52.19, 21.06, 52.245)       # lon/lat: Ochota, Śródmieście, Mokotów, Powiśle
R = 6378137.0


def local(lon, lat, lon0, lat0):
    x = math.radians(lon - lon0) * R * math.cos(math.radians(lat0))
    z = -math.radians(lat - lat0) * R
    return [round(x, 2), round(z, 2)]


def area(ring):
    a = 0.0
    for i in range(len(ring)):
        x1, y1 = ring[i - 1]
        x2, y2 = ring[i]
        a += x1 * y2 - x2 * y1
    return abs(a) * 0.5


class Collect(osmium.SimpleHandler):
    def __init__(self):
        super().__init__()
        self.buildings = []
        self.entrances = []
        self.streets = []

    def inside(self, lon, lat, pad=0.0):
        return BOX[0] - pad <= lon <= BOX[2] + pad and BOX[1] - pad <= lat <= BOX[3] + pad

    def node(self, n):
        if 'entrance' in n.tags and n.location.valid() and self.inside(n.location.lon, n.location.lat, 0.002):
            self.entrances.append((n.location.lon, n.location.lat, n.tags.get('entrance')))

    def way(self, w):
        hw = w.tags.get('highway')
        if hw in ('primary', 'secondary', 'tertiary', 'residential', 'unclassified', 'living_street', 'service', 'trunk'):
            try:
                pts = [(nd.lon, nd.lat) for nd in w.nodes]
            except osmium.InvalidLocationError:
                return
            if any(self.inside(lo, la, 0.002) for lo, la in pts):
                self.streets.append((hw, pts))

    def area(self, a):
        b = a.tags.get('building')
        if not b or b == 'no':
            return
        for outer in a.outer_rings():
            pts = [(p.lon, p.lat) for p in outer]
            if not pts or not self.inside(pts[0][0], pts[0][1]):
                continue
            holes = [[(p.lon, p.lat) for p in h] for h in a.inner_rings(outer)]
            lv = a.tags.get('building:levels')
            self.buildings.append({
                'osm': ('w' if a.from_way() else 'r') + str(a.orig_id()),
                'type': b, 'levels': int(lv) if lv and lv.isdigit() else None,
                'height': a.tags.get('height'), 'roof': a.tags.get('roof:shape'),
                'housenumber': a.tags.get('addr:housenumber'), 'outer': pts[:-1], 'holes': [h[:-1] for h in holes],
            })
            break


def main():
    pbf, out = sys.argv[1], sys.argv[2]
    h = Collect()
    h.apply_file(pbf, locations=True)
    print(f'{len(h.buildings)} buildings, {len(h.entrances)} entrances, {len(h.streets)} streets in the box', flush=True)
    lon0, lat0 = (BOX[0] + BOX[2]) / 2, (BOX[1] + BOX[3]) / 2
    for b in h.buildings:
        b['_ring'] = [local(lo, la, lon0, lat0) for lo, la in b['outer']]
        b['_area'] = area(b['_ring'])
        xs = [p[0] for p in b['_ring']]
        zs = [p[1] for p in b['_ring']]
        b['_c'] = (sum(xs) / len(xs), sum(zs) / len(zs))
        b['_span'] = max(max(xs) - min(xs), max(zs) - min(zs))

    def convexity(b):
        pts = sorted(map(tuple, b['_ring']))
        if len(pts) < 3:
            return 1.0
        def cross(o, a, c):
            return (a[0] - o[0]) * (c[1] - o[1]) - (a[1] - o[1]) * (c[0] - o[0])
        lower, upper = [], []
        for p in pts:
            while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
                lower.pop()
            lower.append(p)
        for p in reversed(pts):
            while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
                upper.pop()
            upper.append(p)
        hull = lower[:-1] + upper[:-1]
        ha = area(hull)
        return b['_area'] / ha if ha > 0 else 1.0

    def min_angle(b):
        r = b['_ring']
        best = 180.0
        for i in range(len(r)):
            a, p, c = r[i - 1], r[i], r[(i + 1) % len(r)]
            v1 = (a[0] - p[0], a[1] - p[1])
            v2 = (c[0] - p[0], c[1] - p[1])
            l1, l2 = math.hypot(*v1), math.hypot(*v2)
            if l1 < 0.5 or l2 < 0.5:
                continue
            ang = math.degrees(math.acos(max(-1, min(1, (v1[0] * v2[0] + v1[1] * v2[1]) / (l1 * l2)))))
            best = min(best, ang)
        return best

    import bisect
    order = sorted(range(len(h.buildings)), key=lambda i: h.buildings[i]['_c'][0])
    xs_sorted = [h.buildings[i]['_c'][0] for i in order]

    def near(b, radius):
        lo = bisect.bisect_left(xs_sorted, b['_c'][0] - radius - 200)
        hi = bisect.bisect_right(xs_sorted, b['_c'][0] + radius + 200)
        out = []
        for k in range(lo, hi):
            o = h.buildings[order[k]]
            if o is b:
                continue
            if math.hypot(o['_c'][0] - b['_c'][0], o['_c'][1] - b['_c'][1]) < radius + o['_span'] / 2 + b['_span'] / 2:
                out.append(o)
        return out

    def touching(b):
        n = 0
        for o in near(b, 5):
            for p in o['_ring']:
                if any(math.hypot(p[0] - q[0], p[1] - q[1]) < 0.3 for q in b['_ring']):
                    n += 1
                    break
        return n

    picks = {}
    def pick(name, cond, count=2):
        got = []
        for b in h.buildings:
            if len(got) >= count:
                break
            if b['osm'] in [x['osm'] for v in picks.values() for x in v]:
                continue
            try:
                if cond(b):
                    got.append(b)
            except Exception:
                pass
        picks[name] = got
        print(f'{name}: {len(got)}', flush=True)

    pick('long_prl_slab', lambda b: b['type'] in ('apartments', 'residential', 'yes') and (b['levels'] or 0) >= 10 and b['_span'] > 80 and convexity(b) > 0.9)
    pick('prl_block', lambda b: b['type'] in ('apartments', 'residential') and 4 <= (b['levels'] or 0) <= 5 and 40 < b['_span'] < 90 and convexity(b) > 0.9)
    pick('l_block', lambda b: b['type'] in ('apartments', 'residential') and (b['levels'] or 0) >= 4 and 0.45 < convexity(b) < 0.75 and len(b['_ring']) in (6, 7, 8))
    pick('courtyard', lambda b: len(b['holes']) >= 1 and b['_area'] > 400)
    pick('touching_row', lambda b: (b['levels'] or 0) >= 4 and touching(b) >= 2 and b['_area'] > 200, 3)
    pick('wedge', lambda b: (b['levels'] or 0) >= 3 and min_angle(b) < 40 and b['_area'] > 200)
    pick('house', lambda b: b['type'] in ('house', 'detached') and 60 < b['_area'] < 250)
    pick('outbuilding', lambda b: b['type'] in ('garage', 'garages', 'shed', 'outbuilding') and 15 < b['_area'] < 120)
    pick('tiny', lambda b: b['_area'] < 3, 1)

    samples = []
    for case, bs in picks.items():
        for b in bs:
            cx, cz = b['_c']
            def L(lon, lat):
                p = local(lon, lat, lon0, lat0)
                return [round(p[0] - cx, 2), round(p[1] - cz, 2)]
            neighbours = [[L(lo, la) for lo, la in o['outer']] for o in near(b, 60)]
            ents = []
            for lo, la, kind in h.entrances:
                p = L(lo, la)
                if abs(p[0]) < b['_span'] and abs(p[1]) < b['_span']:
                    ents.append({'position': p, 'kind': kind if kind in ('main', 'staircase', 'service') else 'main'})
            streets = []
            for hw, pts in h.streets:
                pl = [L(lo, la) for lo, la in pts]
                if any(math.hypot(*p) < 60 + b['_span'] / 2 for p in pl):
                    streets.append({'kind': hw, 'points': pl})
            samples.append({
                'case': case, 'synthetic': False, 'id': f"osm:{b['osm']}", 'type': b['type'], 'levels': b['levels'],
                'height': float(b['height']) if b['height'] and b['height'].replace('.', '', 1).isdigit() else None,
                'roof': b['roof'], 'house_number': b['housenumber'],
                'outer': [L(lo, la) for lo, la in b['outer']], 'holes': [[L(lo, la) for lo, la in hh] for hh in b['holes']],
                'neighbours': neighbours, 'streets': streets, 'entrances': ents,
            })
    with open(out, 'w', encoding='utf-8') as f:
        json.dump({'attribution': 'Map data (c) OpenStreetMap contributors, ODbL 1.0 (openstreetmap.org/copyright)',
                   'axes': 'local metres around each building, +X east, +Z south', 'samples': samples}, f, ensure_ascii=False)
    print(f'wrote {len(samples)} samples to {out}', flush=True)


if __name__ == '__main__':
    main()
