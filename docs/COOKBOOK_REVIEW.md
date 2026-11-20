# Cookbook review: what BadBuilding has, what's missing, what to do next

Sage's pass over Noor's [architectural cookbook](COOKBOOK.md), 18–19 Nov 2026. Each entry was checked against the
source and tests (styles `Plain`, `PrlBlock`, `Kamienica`, `Industrial`, `Office`; `Roofs`, `Site`, `Entrances`,
`Passages`), not the README. Statuses: **implemented**, **partial**, **missing**, **deferred**, **landmark-only**.

## 0. What the map actually holds (Mazowsze OSM, 10 Nov 2026)

Counted from the regional extract, `building=*` on ways (2,589,462 buildings, 318,005 of them in Warsaw):

| OSM `building=` | Mazowsze | Warsaw |
|---|---:|---:|
| yes (untyped) | 1,117,949 | 28,905 |
| detached + house + semidetached_house + bungalow + terrace + residential | 741,887 | 161,105 |
| outbuilding + farm_auxiliary | 550,087 | 52,993 |
| apartments | 37,059 | 25,972 |
| garage + garages | 26,062 | 9,311 |
| retail + commercial + supermarket | 21,551 | ~9,200 |
| service | 12,183 | 4,152 |
| warehouse + industrial + manufacture | 21,822 | ~6,300 |
| allotment_house + shed + hut | 13,936 | ~4,750 |
| office | 7,349 | 4,153 |
| greenhouse | 4,659 | 1,302 |
| school + kindergarten + university + education | 5,051 | ~1,850 |
| church + chapel + presbytery | 2,487 | ~340 |
| fire_station | 1,099 | — |
| hospital | 592 | 304 |
| hotel | 794 | 268 |

Evidence beyond the type is thin: `building:levels` on 1.4 M, `roof:shape` on 19,913, `height` on 3,965,
**`start_date` on only 1,457** and `building:architecture` on 31. So era-based choices (D10, D17, D18) will almost
never see a year; style must come from type, size, shape and context, with the year as a bonus.

**Footprint shapes:** a sample of 6,000 per type, measured against the nearly-rectangle rule for pitched roofs
(`Roofs.RectangularEnough` = 0.86): **30 % of houses**, 14 % of untyped buildings, 9 % of farm buildings and 11 % of
sheds are L-, T- or otherwise irregular, and get a flat roof today. That is about 225,000 houses.

**What reaches the library from Baltic Drive:** the game's extract folds OSM types into eleven kinds (house,
apartments, commercial, industrial, farm, shed, church, civic, tower, canopy, generic), so `office`, `school`,
`kindergarten`, `garages`, `greenhouse`, `fire_station`, `hotel` and the rest arrive as `commercial`, `civic`, `shed`
or `barn`. Any type recipe below needs the raw OSM value (and `amenity`) carried through first: a change to the
game's extract and pack, then a map rebuild (~52 min).

## 1. Building types (Part A)

### A1. Homes, villages, small structures

| ID | Recipe | Status | Today | Smallest useful next step |
|---|---|---|---|---|
| R001 | Mazovian cottage (chałupa) | implemented (D26) | `Plain`: gabled/hipped house, plain windows | a rural house subfamily: steep gable, porch, chimney, timber or whitewash |
| R002 | Interwar suburban house | missing | `Plain` | subfamily of the house family, needs evidence or a preset |
| R003 | PRL kostka | implemented (D26) | `Plain` | **house subfamily**: near-square 2-storey cube, hipped/flat roof, raised entrance with steps, basement windows |
| R004 | Contemporary detached house | implemented (D26) | `Plain` | subfamily: bigger garden glazing, clean eaves, optional garage wing |
| R005 | Semi-detached (bliźniak) | implemented (D26) | party wall blank (D4), doors anywhere | mirrored halves: doors and porches away from the shared wall |
| R006 | Terraced row (szeregowiec) | implemented (D26) | party walls blank | per-unit doors and small porches on the street side |
| R007 | Pre-war villa | missing | — | with evidence only (rare: 1,457 start dates in the region) |
| R008 | Modern villa | missing | — | later |
| R009 | Extended / mixed-age house | missing | — | comes almost for free with L-shaped roofs (a wing at another pitch) |
| R010 | Urban infill house | partial | party walls, `Plain` | fine for now |
| R011 | Allotment hut | implemented (D28) | `Plain` shed | small-hut subfamily: porch, one window, timber/corrugated |
| R012 | Shack / lean-to | implemented (D28) | — | skillion roof, patched cladding (Tom's "small shacks") |
| R013 | Single garage | implemented (D28) | `Plain` with a wide door | door toward the road, no windows |
| R014 | Garage row (blaszaki) | implemented (D28) | `Plain`, a door or two | **one up-and-over door per bay along the long side**, mono-pitch roof (Tom's "garages") |

### A2. Urban housing

| ID | Recipe | Status | Today | Next step |
|---|---|---|---|---|
| R015 | PRL slab (klatkowiec) | implemented | `PrlBlock`: panel grid, staircase strips, balcony columns, machine rooms | — |
| R016 | Point tower (punktowiec) | partial | `PrlBlock` treats it as a slab | one core, balconies on several faces (roadmap item) |
| R017 | Corridor block | missing | — | later |
| R018 | 1950s brick block | missing | — | with socialist realism (same era) |
| R019 | Socialist realism | implemented (D31) | — | **roadmap, Tom's list**: MDM fronts, high plinth, cornice, arcades where the site allows |
| R020 | Modest kamienica | implemented | `Kamienica` ornament 0–1 | — |
| R021 | Ornate kamienica | implemented | ornament 2 with evidence only (D10) | — |
| R022 | Corner kamienica | partial | bands meet at corners | a corner treatment (chamfer is out: footprint) — corner shop, corner balcony |
| R023 | Courtyard tenement | implemented | passages and bramy (D7) | — |
| R024 | Post-war infill | partial | goes to `Plain`/`PrlBlock` | fine for now |
| R025 | Contemporary apartments | implemented (D30) | **drawn as PRL** by `PrlBlock` | a "deweloperka" subfamily: rendered plaster, glass balconies, top floor set back look, underground garage ramp later |
| R026 | Contemporary residential tower | missing | `Office` from 60 m or `PrlBlock` | with R025 |
| R027 | Dormitory | missing | `PrlBlock` | later |
| R028 | Converted factory (lofts) | deferred | — | needs authoring intent |

### A3. Shops, offices, services

| ID | Recipe | Status | Today | Next step |
|---|---|---|---|---|
| R029 | Shopfront module | partial | inside `Kamienica` only | lift into a shared module for any ground floor with mapped shops |
| R030 | Kiosk | missing | `Plain` shed | tiny: serving window, sign band |
| R031 | PRL retail pavilion | missing | `Plain` flat | glass front, canopy, blank back |
| R032 | Supermarket | partial | `Plain` flat, 4.2 m storey | glazed entrance corner, blank walls, loading at the back |
| R033 | Mall | missing | — | later |
| R034 | Mid-century office | partial | `Office` grid | — |
| R035 | Late-20th-c. office | implemented | `Office` ribbon (D18) | — |
| R036 | Curtain-wall office | implemented | `Office` curtain (D18) | — |
| R037 | Office + retail podium | missing | — | uses R029 |
| R038 | Office skyscraper | partial | towers, crowns, masts; parts (D19, D20) | setbacks within one footprint |
| R039 | Hotel | missing | `hotel` → apartments | later |
| R040–R043 | Restaurant, petrol station, car repair, service strip | missing | `Plain` | car repair fits Baltic Drive's workshop theme; later |

### A4. Public and civic

| ID | Recipe | Status | Today | Next step |
|---|---|---|---|---|
| R044 | School / kindergarten | implemented (D29) | `civic` → `Plain` | **5,051 in the region**: long wings, big classroom windows, marked entrance, gym hall if the footprint allows |
| R045–R048 | University, hospital, clinic, town hall | missing | `Plain` | later |
| R049 | Fire station (OSP) | implemented (D29) | `Plain` | **1,099, nearly every village**: tall vehicle doors to the road, hose tower if parts/height allow |
| R050–R054 | Police, library, culture, cinema, sports hall | missing | `Plain` | later |
| R055 | Church / chapel | landmark-only | Baltic Drive keeps churches classic | big churches hand-made; wayside chapels procedural later |
| R056–R058 | Modern church, market hall, museum | missing | — | later |

### A5. Industrial, utilities, transport

| ID | Recipe | Status | Today | Next step |
|---|---|---|---|---|
| R059 | Pre-war brick factory | implemented | `Industrial` brick, chimneys, saw-tooth (D17) | — |
| R060 | PRL concrete factory | implemented | `Industrial` concrete | — |
| R061 | Corrugated hall | implemented | `Industrial` metal | — |
| R062 | Logistics warehouse | partial | docks on the longest open wall | longer dock runs on big halls |
| R063 | Small workshop | partial | `Industrial` on small sites | — |
| R064–R066 | Engine shed, depot, station | missing | — | later |
| R067 | Multi-storey car park | missing | — | open decks: easy and distinctive; later |
| R068 | Substation | missing | `transformer_tower` → shed | the brick transformer tower is iconic in Polish villages; small |
| R069–R071 | Pumping station, boiler house, water tower | missing / landmark-only | — | water towers need evidence |

### A6. Agriculture

| ID | Recipe | Status | Today | Next step |
|---|---|---|---|---|
| R072 | Barn (stodoła) | implemented (D27) | `Plain` barn: gabled 20–28° | big doors on the long side, timber or brick, few windows |
| R073 | Modern farm shed | implemented (D27, hall) | `Plain` | reuse `Industrial` metal on large farm footprints |
| R074 | Stable / cowshed | implemented (D27) | `Plain` | row of small vents, doors |
| R075 | Greenhouse | implemented (D27) | `Plain` | glass walls and roof with ribs: 4,659, many in the fruit belt south of Warsaw |
| R076 | Silo | missing | tower | needs evidence |
| R077 | Farm outbuilding | implemented (D27) | `Plain` | **550,000 of them**: a door, a vent, a few material families (brick, block, timber) |
| R078 | Rural shop | partial | `Plain` | later |

## 2. Styles (Part B)

| ID | Style | Status | Where |
|---|---|---|---|
| S01 | Mazovian vernacular | missing | → R001 |
| S02 | Timber / log | missing | → R001, R011 |
| S03 | Late-19th-c. brick | implemented | `Industrial` brick |
| S04 | Kamienica eclectic | implemented | `Kamienica` |
| S05–S07 | Neoclassical, Secession, Art Deco | missing | evidence is rare (31 architecture tags) |
| S08 | Interwar functionalism | missing | later |
| S09 | Socialist realism | implemented (D31) | roadmap, Tom's list |
| S10 | PRL panel | implemented | `PrlBlock` |
| S11 | PRL functional | partial | `Industrial` concrete, `Office` grid |
| S12–S14 | Brutalism (civic, residential, industrial) | missing | Tom's list; best as a modifier over existing types (P05) |
| S15 | Postmodern | missing | later |
| S16 | Contemporary plaster housing | missing | → R025 |
| S17 | Contemporary glass office | implemented | `Office` |
| S18 | Modern industrial metal | implemented | `Industrial` |
| S19 | Renovated historic | partial | `Kamienica` renovated palettes |
| S20 | Weathered / neglected | partial | grime in Baltic Drive's shader, not in the library |
| S21–S25 | Nordic, European brick, Mediterranean, storybook, brutalist city | not relevant now | presets for other projects |

## 3. Shared ingredients (Part C)

| ID | Ingredient | Status | Notes |
|---|---|---|---|
| M01 | Single block | implemented | |
| M02 | Slab with repeating bays | implemented | `PrlBlock` |
| M03 | Point tower | partial | |
| M04 | Courtyard / perimeter | implemented | D7 passages |
| M05 | Multi-wing (L/T/U) | implemented | walls, and roofs in wings (D25) |
| M06 | Stepped / multipart | partial | parts in the air (D19), party walls by height (D20); each part still its own request |
| M07 | Podium + tower | partial | from parts only |
| M08 | Industrial hall | implemented | D17 |
| M09 | Narrow row frontage | implemented | party / sliver exposure (D4) |
| M10 | Adaptive reuse | missing | deferred |
| RF01 | Flat + parapet + plant | implemented | |
| RF02 | Gable with eaves | implemented | rectangles only |
| RF03 | Hipped / pyramidal | implemented | rectangles only |
| RF04 | Skillion | implemented | any footprint |
| RF05 | Mansard / gambrel | missing | drawn as gabled |
| RF06 | Half-hip | missing | drawn as hipped; common on Polish houses |
| RF07 | Saw-tooth | implemented | D17 |
| RF08 | Roof joins on L/T/U footprints | implemented (D25) | rectilinear footprints in up to four wings with valleys; slanted corners still flat |
| RF09 | Roof terrace | missing | |
| RF10 | Domes, spires, vaults | landmark-only | |
| C3 | Exposure classes | implemented | street, courtyard, side, party, sliver |
| C3 | Window families | partial | dwelling, tenement, factory, office, glass block; no storefront module outside the kamienica |
| C3 | Entrances | partial | main, shop, yard, gate, loading dock; no garage door rows, no vehicle bays |
| C3 | Balconies | partial | projected (PRL, kamienica); no loggias, no glass balconies |
| C4 | Ground and thresholds | implemented | D5 datum |
| C4 | Region presets | missing | later (P01–P07) |

## 4. What to do next (Sage's suggestion)

By what you see most while driving, then by shared leverage:

1. *(done, Baltic Drive D72)* **Carry the real OSM type into the library** (Baltic Drive extract → pack → adapter: the raw `building` value and
   `amenity`). Small code, one map rebuild. Without it, schools, fire stations, garages, greenhouses and offices can't
   be told apart, so every type recipe below waits on this.
2. *(done, D25)* **Roofs on L-, T- and U-shaped footprints** (RF08, M05): split the footprint into rectangles, roof each, join
   them with valleys. 225,000 houses get their roof back; every pitched-roof family after this needs it.
3. *(done, D26)* **The house family** (R001 chałupa, R003 kostka, R004 contemporary, R005–R006 paired and terraced): one `House`
   style with subfamilies chosen by footprint, size, storeys, rural or town context; 3–5 variations each; porches,
   chimneys, eaves, steps, basement windows. Houses are most of Mazowsze.
4. *(done, D27)* **Farm buildings and outbuildings** (R072 barn, R077 outbuilding, R075 greenhouse): half a million small
   buildings in every village.
5. *(done, D28)* **Garages and shacks** (R014, R013, R012, R011): Tom's list; small and quick once roofs and types are in.
6. *(done, D29)* **Village civic**: fire station (R049), school and kindergarten (R044).
7. *(done, D30)* **Contemporary apartments** (R025): Warsaw's new estates stop looking like PRL blocks.
8. Then the city styles: **socialist realism** (R019, S09), **brutalism** as a modifier over existing types (S12–S14).

Each new family keeps today's detail level: real geometry for openings, reveals, sills and porches, materials by
role, markers and LODs. It doesn't swap detail for variety.
