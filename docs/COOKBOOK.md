# BadBuilding — Architectural Recipe Cookbook

**A design catalogue for Sage to compare with the current BadBuilding repository and select what is missing.**  
**Prepared for:** Bad Ideas Works / Baltic Drive / other future projects  
**Scope:** Engine-agnostic, procedural *buildings* and architectural identities.  
**Status:** A catalogue of candidate recipes and design rules — **not** a claim that any particular feature is absent and **not** an instruction to implement everything.

> **Sage, please read this first:** Examine BadBuilding's **current code, tests, gallery and decisions** before choosing work. Search for existing styles, subtypes, roof geometry, prefabs, features, and planned additions. Mark every entry **Implemented / Partial / Missing / Not relevant**. Improve existing generators instead of duplicating them. This cookbook deliberately includes families that may already exist, so it can serve as a durable reference. **You choose what is missing and what is worth doing next.** Keep existing working behavior and compatible export contracts. This is *not* an order to create a new architectural engine or to rewrite the library.

---

## 1. What a 'recipe' means

A **building type** answers *what is this building used for?* A **style** answers *when, where, and how was it built?* A **recipe** binds those together with constraints on the footprint, massing, facades, roof, openings, site, and optional interior.

A convincing building must not be selected from a giant list of random meshes. Its design follows a readable sequence:

**site + evidence → function → massing → roof → floor rhythm → facade openings → entrances → materials → details → export + validation**

Each recipe should define (where relevant):

1. **Eligibility / evidence:** OSM `building=*`, `amenity=*`, `shop=*`, `office=*`, `building:architecture`, `start_date`, storeys, roof information, footprint, urban/rural context. If evidence is missing, infer conservatively and record the inference.
2. **Form:** single block, elongated slab, courtyard, L/U/T footprint, wings, tower-on-podium, attached row, hall, shed, compound. *Do not expand a mapped footprint just to fit a prettier building.*
3. **Scale:** typical storeys, floor heights and proportions (ranges, not exact historical standards).
4. **Facade logic:** which side faces the street, yard, service access or a party wall; bay rhythm; windows; balconies; stairs; doors.
5. **Roof logic:** plausible family and exceptions; mapped roof tags take priority; use honest fallback for a complex footprint.
6. **Materials:** structural and surface roles, limited era/regional palettes, repair/renovation state. Avoid random colour on every panel.
7. **Functional signals:** shops at street level, warehouse loading at service side, firehouse vehicle bays, school entrances, farm doors, etc.
8. **Variation:** a few coherent subfamilies and stage-seeded options. A reroll must remain recognizable.
9. **Validation:** silhouette, doors/access, openings, terrain, collision, footprint compliance, LOD/cost, and future interior feasibility.

### Important distinctions

- **Missing *type* vs missing *style***: One modern office can have an international glass look or a brutalist concrete look. Do not need two independent footprint pipelines for those.
- **Recipe vs landmark**: A railway station family is procedural. A specific famous station, church, monument or Palace of Culture is usually a hand-authored override or specially constrained recipe.
- **Exterior first**: The cookbook describes visible exteriors, with optional interior clues. Do **not** imply actual floor-plan support until it exists.
- **Not a random building zoo**: An apartment block should still look like a home, a warehouse like a warehouse, a church like a church.
- **Not a legal building-code specification**: Dimensions below are visual/gameplay guidelines, subject to actual geometry and testing.

## 2. Review checklist for Sage

Before implementing an item:

- [ ] Find the closest existing style/recipe/feature (check source and tests, not just the README).
- [ ] Decide: **already supported**, **needs enhancement**, **new recipe**, **reusable module**, or **handcrafted override**.
- [ ] Check OSM/adapter data actually reaching the generator; do not build a style that never gets selected.
- [ ] Preserve explicit map data above inference; mark unknown age, roof, type and material as inferred.
- [ ] Add at least one real input footprint and one synthetic stress case per new *structural* family.
- [ ] Test a seed gallery of several examples and an unlabelled silhouette/fingerprint check.
- [ ] Check wall-sharing, entrances, slopes, courtyards, odd footprints, and distant LODs.
- [ ] Profile generation and streaming cost in Baltic Drive; large urban scenes matter more than one glorious showcase.
- [ ] If a feature belongs in a shared component (roof, window, staircase marker, loading dock), implement it there rather than cloning it across recipes.
- [ ] Update the catalogue's status and notes after each completed family.

**Suggested status convention:** `implemented`, `partial`, `missing`, `deferred`, `landmark-only`. Leave it to Sage to fill; no statuses below assert absence.

---

# PART A — BUILDING-TYPE RECIPES

Numbers are stable cookbook IDs, not development priorities. Every recipe is a visual/functional description, not a ready-to-use JSON schema.

## A1. Detached homes, villages, small structures

### BB-R001 — Traditional Mazovian cottage / *chałupa*
**Look:** 1–1.5 storeys; elongated small footprint; steep gabled roof; simple plaster or horizontal timber; small irregularly spaced windows; shallow entrance porch; modest chimney. **Variations:** whitewashed masonry, weathered timber, renovated tile roof, patched metal roof. **Avoid:** huge glazing, gratuitous balconies, flat city-block roof. **Hints:** `house`, `detached`, rural location, small footprint, sometimes historic material tags.

### BB-R002 — Interwar suburban house
**Look:** 1–2 storeys with pitched/hipped roof, pronounced eaves, small porch, compact bay windows; plaster and brick details. **Variations:** modest modernist cube vs pitched-roof suburban; keep forms era-coherent. **Hints:** `house`/`detached`, pre-1945 evidence or explicit style selection.

### BB-R003 — Post-war Polish *kostka*
**Look:** Characteristic compact, nearly cubic two-storey house; flat or shallow-hip roof; strong horizontal ground/first-floor split; raised entrance and basement windows; modest balcony/loggia. **Variations:** original grey plaster, painted stucco, modern insulated renovation. **Hints:** detached house, compact squarish footprint, mid/late 20th-century evidence.

### BB-R004 — Contemporary detached house
**Look:** 1–2 storeys, restrained facade, broad glazing toward garden, smaller street windows; gabled, hipped or flat roof depending on chosen subfamily; integrated garage only where footprint/parts support it. **Variations:** contemporary barn-house, minimalist cubic, suburban pitched-roof. **Hints:** residential house; age if available.

### BB-R005 — Semi-detached house / *bliźniak*
**Look:** Two attached dwellings with mirrored or related window/door patterns, party wall without openings; paired porch/garage decisions. **Variations:** symmetrical half, asymmetric extensions, shared pitched roof. **Avoid:** generating an entrance facing the party wall. **Hints:** `semidetached_house`, attached adjacency.

### BB-R006 — Terraced / row house / *szeregowiec*
**Look:** Narrow attached frontage repeated over multiple units, deep plots, shared walls, individual front doors; 2–3 storeys; private garden side. **Variations:** 1990s plaster row, modern flat-roof row, older brick terrace. **Hints:** `terrace`, rows of touching narrow footprints.

### BB-R007 — Pre-war villa
**Look:** Larger detached mass, hipped/mansard/gabled roof, bays and terrace, architectural entrance, limited decorative stone/plaster; set within a garden. **Variations:** eclectic, classical, early modernist; only use historically rich features with suitable evidence or explicit preset. **Hints:** `villa`, heritage/age/style.

### BB-R008 — Modern villa
**Look:** Broad horizontal masses, optional staggered volumes, garden-facing glass, balconies/terraces, flat or low-slope roofs. **Constraint:** terraces and upper overhangs must be coherent with mapped footprint and any building parts. **Hints:** `villa`, detached, explicit contemporary style.

### BB-R009 — Extended house / mixed-age house
**Look:** Original core plus side wing, changed roof pitch, garage add-on or different finish showing later construction. **Variation:** one or two controlled extensions, not random patchwork. **Constraint:** use actual polygon recesses/parts where present; otherwise keep extensions inside the provided footprint.

### BB-R010 — Small urban infill house
**Look:** Narrow frontage between neighbours; blank party walls; windows concentrated street/yard side; deeper roof shape; one entrance and compact storefront only if mapped. **Hint:** small dense footprint with party walls. **Avoid:** detached-house balconies against neighbours.

### BB-R011 — Allotment garden hut / *altanka działkowa*
**Look:** Very small timber/plaster/corrugated shelter, 1 storey, pitched or low roof, small door, 0–2 small windows, porch optional. **Variations:** tidy hobby shed, patched improvised hut. **Hints:** `allotment_house`, `hut`, `shed` in allotment context.

### BB-R012 — Rural shack / improvised lean-to
**Look:** Tiny asymmetrical timber and corrugated-metal structure; simple roof with believable drainage; mismatched panels in restrained palette. **Use:** outbuildings, survival-game worlds. **Avoid:** pretending a tiny footprint contains a full dwelling.

### BB-R013 — Single private garage
**Look:** One low vehicle opening on accessible side, almost no windows, flat or pitched roof, brick/plaster/metal door. **Hint:** `garage` adjacent to residential context. **Validation:** vehicle clearance and drivable approach.

### BB-R014 — Garage row / *garaże blaszane*
**Look:** Repeating narrow garage bays, individual metal up-and-over doors, low sloping or flat roof; brick, sheet metal or mixed. **Hints:** `garages`, long shallow footprint. **Avoid:** doors on inaccessible walls or many floors.

## A2. Apartment and urban housing

### BB-R015 — PRL slab block / *klatkowiec*
**Look:** Long rectangular volume, several vertical stairwell cores, regular panel grid, balcony/loggia side opposite main entrances, plain gable ends. **Variants:** bare concrete, insulated renovations, accent-painted ends. **Keep:** a slab must not read like a generic office. **Hint:** `apartments`, long/deep footprint, post-war evidence.

### BB-R016 — PRL point tower / *punktowiec*
**Look:** Compact tower, one primary circulation core, windows and balcony/loggia bays on several faces; roof machine room; modest vestibule/canopy. **Hints:** residential tower, roughly square footprint, many storeys. **Distinct from:** R015's long multi-stair slab.

### BB-R017 — PRL corridor block / *korytarzowiec*
**Look:** Longer slab with regular corridor-end windows, repeated entrances/core, restrained facade pattern; balcony placement follows plan logic rather than randomly every bay. **Hint:** `dormitory`, `apartments`, explicit subtype or suitable context.

### BB-R018 — 1950s brick housing block
**Look:** 3–5 storeys, masonry proportions, pitched/low hipped roof, repeated punched windows, modest cornice and stairwell entrance. **Style:** early post-war functional masonry, not the late PRL panel grid.

### BB-R019 — Socialist-realist residential frontage
**Look:** Monumental axial facade, tall ground floor, rhythmically repeated stone/plaster bays, heavy cornice, arcade only if site allows, occasional sculpture/niche. **Hints:** early 1950s, explicit socialist-realism architecture tag or deliberate alternate-city preset. **Avoid:** using ornate front on every anonymous block.

### BB-R020 — Modest historic *kamienica*
**Look:** Street-facing windows with traditional vertical rhythm, restrained plaster, a proper front entrance; plain courtyard/back wall, firewalls beside neighbours; low ornament. **Hint:** party walls and appropriate site. **Avoid:** asserting historic age without evidence.

### BB-R021 — Ornate pre-war *kamienica*
**Look:** Hierarchical facade: raised ground floor, tall first-floor windows, string courses, cornice, occasional balcony; brama/courtyard where possible; street shops only when plausible. **Hints:** mapped age/heritage/style or explicit preset. **Avoid:** every window getting identical random decoration.

### BB-R022 — Corner *kamienica*
**Look:** Two street facades with a strong corner treatment: rounded/chamfered corner only if geometry allows, corner shops, coherent cornice lines, courtyard on inner side. **Validation:** no projected corner mass outside mapped footprint unless intentionally permitted by the system.

### BB-R023 — Courtyard tenement / perimeter block
**Look:** Street frontage and plain service yard, passage/gate sized for access, coherent internal walls; staircase entry from street or courtyard. **Variants:** one open side, enclosed courtyard, lightwell. **Validation:** reachable yard, no windows into party walls.

### BB-R024 — Post-war infill apartment block
**Look:** In a historic street row but made from simpler post-war facade vocabulary; storey rhythm may differ from neighbours without masquerading as a pre-war tenement. **Hints:** row-site, known later construction or uncertainty handled conservatively.

### BB-R025 — Contemporary low/mid-rise apartments
**Look:** 3–8 storeys, regular balconies/loggias, horizontal window bands or punched bays, restrained plinth and rooftop equipment; contemporary palettes. **Variants:** courtyard block, free-standing slab, corner urban block. **Validation:** balconies must not clash with neighbors or roads.

### BB-R026 — Contemporary residential tower
**Look:** Compact tower over base, strong vertical facade grid, repetitive but varied balcony stacks, roof/mechanical crown; pedestrian entrance at street level. **Constraint:** only when height/storeys support tower typology.

### BB-R027 — Student dormitory / *akademik*
**Look:** Long repetitive room-window rhythm, prominent shared entrance, stair/utility strips, few private balconies; 4–12+ storeys according to site. **Hint:** `dormitory`, university context.

### BB-R028 — Residential converted factory
**Look:** Preserved brick industrial shell cues with new window groups and balconies, loft-style glazing; original structural bay rhythm remains visible. **Use:** explicit adaptive-reuse context or authoring preset, not random assignment to existing factories.

## A3. Shops, offices, hotels and everyday services

### BB-R029 — Street-level shopfront module
**Look:** One or several glazed bays with dedicated door, sign band and possible shutters, always attached to a compatible ground-floor facade. **Not a standalone architecture family.** **Hints:** mapped `shop=*`/compatible `amenity=*`, street frontage.

### BB-R030 — Neighbourhood kiosk
**Look:** Tiny low building with visible serving window, signage panel, single service door and flat/low roof. **Hints:** `retail`, `kiosk`, suitable small footprints. **Avoid:** full office-style windows.

### BB-R031 — PRL retail pavilion
**Look:** Single/two-storey shallow mass, flat roof/canopy, broad shop windows, strong ground-level frontage, blank/service back. **Variants:** original metal-and-glass, renovated plaster.

### BB-R032 — Supermarket / discount grocery
**Look:** Big single-storey commercial box, public glazed entrance, mostly opaque selling walls, delivery/service doors at rear, simple parapet roof. **Hints:** `supermarket`, `retail`, large shallow footprint. **Validation:** separate customer and loading sides.

### BB-R033 — Shopping centre / mall
**Look:** Large multi-volume low/mid rise, main entrance atrium or marked frontage, few small punched facade windows, service/loading backs, roof plants. **Use:** only adequately large footprints; do not generate a skyscraper from a mall tag.

### BB-R034 — Mid-century masonry office
**Look:** Regular rows of medium-sized windows, strong structural bay grid, 4–8 storeys, modest entrance canopy/stone plinth, flat roof.

### BB-R035 — Late-20th-century office block
**Look:** Dark glass ribbons alternating with spandrels, repetitive office modules, flat roof/parapet, prominent ground entry; aluminium/stone/concrete palette.

### BB-R036 — Modern curtain-wall office
**Look:** Continuous glazed facade system with mullion grid, glazed lobby, service/core zones, plant rooms; setbacks/wing transitions only when massing supports them. **Hints:** `office`, `commercial`, suitable height/era.

### BB-R037 — Mixed-use office + retail podium
**Look:** Distinct street-facing retail ground floors, office floors above, service/loading access on different side, roof plant. **Constraint:** explicit or plausible use mix; internal floor height changes should be coherent.

### BB-R038 — Generic office skyscraper
**Look:** Tall slender shaft, optional podium, rational curtain-wall grid, setbacks/crown appropriate to height. **Variants:** flat rectangular, tapered, stepped. **Avoid:** procedural copy of a real landmark. **Hint:** mapped tall commercial/office.

### BB-R039 — Mid-rise hotel
**Look:** Repeated narrow guest-room windows, strong reception/entrance, optional lobby canopy, service side, flat or pitched roof depending on era; fewer office-style huge windows.

### BB-R040 — Restaurant / cafe building
**Look:** Entrance and street-facing dining windows, possible terrace only where site/asset space supports it; service door distinct from customer entrance. **Hint:** standalone restaurant use; shopfront module for restaurants inside tenements.

### BB-R041 — Petrol station / service canopy
**Look:** Low convenience-shop volume; forecourt canopy over pumps as a separate asset/part where mapped or explicitly authored; accessible road approach. **Do not** infer free-floating canopies from a shop box alone. **Hints:** `amenity=fuel`, `building=service`.

### BB-R042 — Automotive repair shop
**Look:** Wide vehicle service doors, narrower staff entrance, small office windows, flat/shallow industrial roof; shop-front optional. **Hint:** vehicle repair service, workshop.

### BB-R043 — Local service strip
**Look:** Series of small storefront bays across a ground-level block, consistent sign height but varied shops; delivery and pedestrian separation. **Use:** suburban retail rows.

## A4. Public, civic, educational, medical and cultural

### BB-R044 — Primary / secondary school
**Look:** Long teaching wings with generous, rhythmic classroom windows; clearly identifiable main entrance; gym wing with higher volume if footprint/parts allow. **Hint:** `school`, `education`. **Avoid:** all-glass office style and fake full-height gym on a narrow one-wing footprint.

### BB-R045 — University / campus building
**Look:** Larger repetitive teaching/research wings, emphasized central entrance, multiple stair cores, public lobby. **Variants:** historic masonry, PRL modernist, contemporary academic.

### BB-R046 — Hospital
**Look:** Larger multi-wing composition, regular patient windows, service cores, protected public entrance and separate logistics/ambulance access where geometry allows. **Variants:** older masonry, modern clinical block. **Hint:** hospital.

### BB-R047 — Clinic / medical centre
**Look:** Small/mid-rise accessible entrance, regular window pattern, clean restrained facade, signage; distinct from hospital massing. **Hint:** clinic, doctors, healthcare.

### BB-R048 — Town hall / municipal office
**Look:** Public entrance and civic hierarchy: prominent central/corner portal, balanced window rhythm; possible clock or tower only with enough evidence or explicit preset. **Styles:** classical, modernist, contemporary.

### BB-R049 — Fire station
**Look:** One or more tall vehicle-bay doors facing the street/yard access, personnel wing with windows, optional training tower as separate part. **Validation:** trucks must be able to leave, no party-wall garage doors.

### BB-R050 — Police / public safety station
**Look:** Secure-looking main entrance, office windows, parking/service access, restrained official facade. **Do not** exaggerate fortress motifs unless explicit fiction preset.

### BB-R051 — Library
**Look:** Public entrance and reading-room windows, architectural focal facade, modest service side. **Variants:** old masonry with tall windows, mid-century low pavilion, contemporary glazed hall.

### BB-R052 — Cultural centre / community hall
**Look:** Larger hall mass alongside smaller support volumes, inviting entrance, selective glazed lobby; stage/service doors at rear.

### BB-R053 — Cinema / theatre
**Look:** Tall mostly blank auditorium volume, smaller foyer/lobby with glass/signage, service loading rear, marquee optional by era. **Avoid:** office window grid on auditorium walls.

### BB-R054 — Sports hall / gymnasium
**Look:** Broad long-span hall, limited high windows, substantial roof span, public entrance at attached lower lobby. **Hint:** sports centre, school gym, arena.

### BB-R055 — Church / traditional chapel
**Look:** Nave-like tall volume, windows suited to worship space, pitched roof, chancel/entry mass. Tower, dome, spire, and crosses only when requested/grounded by data or specific architectural preset. **Use:** separate eligible subtypes, not a random steeple generator.

### BB-R056 — Modern church / religious centre
**Look:** Contemporary expressive massing, fewer but stronger openings, asymmetric roof possible; often a low ancillary volume. **Caution:** Respect actual place-of-worship tags and local religious architecture; avoid randomly assigning faith-specific symbols.

### BB-R057 — Municipal market hall
**Look:** Tall covered trading hall, large spans, side entrances, clerestory or saw-tooth roof, rational loading/service sides. **Variants:** old cast-iron/brick hall, modern steel hall.

### BB-R058 — Museum / gallery
**Look:** Public arrival hierarchy, controlled daylight, fewer or larger deliberate openings instead of uniform office bays; historic adaptive reuse vs new build treated separately.

## A5. Industrial, logistics, utilities, transport

### BB-R059 — Pre-war brick factory
**Look:** Tall brick walls, pilaster bay grid, repeated tall factory windows, occasional chimney, saw-tooth/gabled roof appropriate to span. **Hint:** `industrial`, old age or forced brick-era style.

### BB-R060 — PRL concrete factory
**Look:** Exposed concrete frame/panels, ribbon clerestory windows, utilitarian service doors, flat or saw-tooth roof, restrained roof structures.

### BB-R061 — Modern corrugated industrial hall
**Look:** Broad metal-clad hall with concrete base, high windows, a few large roller doors, shallow roof; coherent metal panel directions.

### BB-R062 — Distribution warehouse / logistics centre
**Look:** Large plain mass with long repetitive loading docks along yard-facing side, office/service corner, little glazing; large-span flat or shallow pitched roof. **Validation:** dock orientation and vehicle clearance.

### BB-R063 — Small urban workshop
**Look:** One to four industrial bays, work doors, narrow staff entrance and sparse windows, pitched or flat roof; brick/concrete/metal according to age.

### BB-R064 — Railway engine shed / roundhouse
**Look:** Wide locomotive doors, workshop windows, long structural bays; roundhouse only if footprint geometry plausibly indicates it. **Do not** build train doors on an unrelated rectangular house.

### BB-R065 — Bus/tram depot
**Look:** Repeated tall vehicle portals and long maintenance halls, roof ventilation and clerestory, staff administration wing. **Hint:** depot, transport service; validate entrance orientations.

### BB-R066 — Rail station building
**Look:** Passenger-facing entrance, ticket hall/foyer, platform-facing exits and windows; historic brick, PRL, or contemporary substyles. **Platforms/sheds may be separate assets, not fabricated from every station footprint.**

### BB-R067 — Multi-storey car park
**Look:** Exposed ventilation openings, regular floor decks, ramps indicated only where genuinely supported, limited glazing, vehicle-level circulation. **No arbitrary windows on every bay.**

### BB-R068 — Electrical substation / utility house
**Look:** Secure opaque mass, ventilation grilles, equipment doors, very few windows, flat/simple roof. **Variants:** small brick hut, large modern concrete building. **Do not** include realistic hazardous equipment placement as architectural certainty.

### BB-R069 — Water/pumping station
**Look:** Low technical volumes, service doors, ventilation, high utility windows, occasional industrial brick heritage forms. **Plant tanks and pipes are separate modules if source geometry supports them.**

### BB-R070 — Boiler house / district heating plant
**Look:** Industrial block with large service door, ventilation arrays and optional chimney/tower when footprint/height/parts justify it. **Variation:** PRL masonry/concrete vs contemporary metal.

### BB-R071 — Water tower / silo-like utility building
**Look:** Distinct elevated tank mass or tall cylindrical/prismatic storage shape; requires strong source evidence or explicit asset type. **Avoid:** guessing from a tall generic footprint alone.

## A6. Agriculture, rural industry and farmsteads

### BB-R072 — Traditional barn / *stodoła*
**Look:** Long single-volume plan, broad doors, steep-to-moderate gabled roof, timber/brick/plaster; few windows. **Hints:** barn, farm context.

### BB-R073 — Modern agricultural shed
**Look:** Large low metal hall, tall tractor doors, simple cladding, shallow pitched roof, ventilation; minimal windows.

### BB-R074 — Stable / livestock housing
**Look:** Repeated smaller ventilation windows, broad service doors, low elongated mass, pitched roof. **Avoid:** office glazing; site-use inference requires caution.

### BB-R075 — Greenhouse
**Look:** Glass/polycarbonate walls and pitched/translucent roof, repeated structural ribs; geometry and material behavior distinct from a normal solid-wall building. **Hint:** greenhouse.

### BB-R076 — Grain store / agricultural silo
**Look:** Tall storage volumes or gabled storage hall with loading aperture, minimal ordinary windows. Cylindrical silos require explicit tags or appropriate standalone geometry.

### BB-R077 — Small farm outbuilding
**Look:** One-storey utility structure with simple door and a few vents, brick/wood/plaster, uncomplicated roof. **Use as conservative fallback** for unspecified outbuildings.

### BB-R078 — Rural shop / roadside service
**Look:** Small low shop house or standalone commercial box, modest signage and accessible street door; roof/facade adjusted to rural character rather than urban curtain wall.

---

# PART B — STYLE / ERA MODIFIERS

A style modifier changes *how an eligible building type is expressed*; it must **not silently change its function**. Use only when suggested by evidence, a regional preset, or explicit authoring choice.

| ID | Family | Defining geometry and facades | Materials / conditions |
|---|---|---|---|
| BB-S01 | Mazovian rural vernacular | Simple elongated masses, steep gables, small windows, deep eaves | timber, lime plaster, clay tile, sheet metal; rural |
| BB-S02 | Polish timber / log | Visible horizontal beams, corner joints, modest eaves | weathered wood, stone base; rural/historic |
| BB-S03 | Late 19th-century brick | Heavy wall thickness, segmental arches, pilasters and cornice | red/brown brick; old industry and civic |
| BB-S04 | Historic *kamienica* eclectic | Storey hierarchy, vertical bays, cornice, occasional ornament/balcony | plaster, stone, ironwork; require history/evidence |
| BB-S05 | Neoclassical | Symmetry and restrained pilasters/portico where footprint permits | pale stone/plaster, consistent proportions |
| BB-S06 | Art Nouveau / Secession | Organic window surrounds, asymmetric details, curved decorative motifs | plaster, ironwork; rare by evidence |
| BB-S07 | Art Deco | Stepped/parapet silhouettes, vertical piers, geometric ornament | stone, stucco, brick, metal |
| BB-S08 | Interwar functionalism | Flat roofs, banded windows, clean planes, simple balcony forms | white/cream plaster, steel, glass |
| BB-S09 | Socialist realism | Monumental bays, high plinth, cornices, arcades on suitable fronts | stone-like render, masonry; early post-war |
| BB-S10 | PRL panel prefabrication | Rectangular concrete panel grid, repetitive windows, loggias | concrete; renovated coloured insulation variants |
| BB-S11 | PRL brick/concrete functional | Clear structural bays, simple entrance, functional window bands | brick/concrete/plaster, subdued colours |
| BB-S12 | Brutalism: civic | Deep recesses, large cantilevers, monumental stairs, rough cast forms | board-formed/exposed aggregate concrete |
| BB-S13 | Brutalism: residential | Strong vertical cores, slab/stepped massing, recessed loggias | exposed/precast concrete, limited colour |
| BB-S14 | Brutalism: industrial | Heavy frame, vents and towers, raw service-driven structure | concrete with steel details |
| BB-S15 | 1980s–2000s postmodern | Contrasting facade bands, playful geometry used sparingly | render, coloured panels, glazing |
| BB-S16 | Contemporary plaster housing | Controlled window groupings, simple roof and balcony geometry | insulated plaster, muted accent colours |
| BB-S17 | Contemporary glass office | Curtain wall, mullion logic, lobby distinction, rooftop plant | glass/aluminium/stone |
| BB-S18 | Modern industrial metal | Large spans, cladding ribs, service openings, low roof | corrugated metal, concrete base |
| BB-S19 | Renovated historic | Preserves historic bay spacing/ornament; modern doors, repairs | cleaned plaster/brick, updated window colours |
| BB-S20 | Weathered / neglected | Same structure as base style; missing paint, patches, soiling | **surface/state modifier**, not new style geometry |
| BB-S21 | Nordic timber contemporary | Restrained clean pitched masses and large deliberate openings | painted wood, metal roof; explicit regional preset |
| BB-S22 | European brick urban | Brick street frontage, repeated vertical bays, stone accents | regional or authored choice |
| BB-S23 | Mediterranean plaster | Light stucco, shutters, tiled roof, deep shadow openings | use only with regional preset |
| BB-S24 | Fantasy crooked-storybook | Deliberately warped (not broken) roof/wall silhouettes, oversized timber, stylized details | game/fantasy preset only; respect navigation |
| BB-S25 | Alternate brutalist city | Apply brutalist vocab coherently to many **existing types** while preserving function/footprints | deliberate alternate-world preset |

### Style combinatorics — rules that prevent nonsense

- A **hospital** in brutalist style remains a hospital with hospital-like entrances and window rhythm.
- A **retail pavilion** in Art Deco style remains a retail pavilion, not a miniature palace.
- **Renovated** and **weathered** are state/material modifiers, not licenses to change the era or footprint.
- A source claiming `start_date=1965` should not quietly become an ornate pre-war tenement.
- Do not assign arcades, towers, onion domes, minarets, crosses, giant chimneys, or other strong identity features by random seed without eligibility or explicit intent.
- Every style should have **3–5 recognizably related variations**, not unlimited random greeble.

---

# PART C — SHARED RECIPE INGREDIENTS (REUSABLE MODULES)

These are cross-cutting components for Sage to audit before adding a new type.

## C1. Massing recipes

- **M01 Single block:** footprint-following extruded volume with coherent wall edges.
- **M02 Elongated slab:** systematic repeating facade bays and multiple entrance/stair modules.
- **M03 Point tower:** compact plan, one core, multiple facade faces.
- **M04 Courtyard/perimeter:** treat internal and external faces differently; plan a usable passage when appropriate.
- **M05 Multi-wing:** L/T/U volumes and roof joins for already-mapped irregular footprints.
- **M06 Stepped/multipart:** different heights on mapped `building:part` masses, no interpenetrating internal facades, independent roofs and exposed walls.
- **M07 Podium + tower:** differentiated entrance/retail base and a narrower taller volume, only when footprint parts or explicit authoring permit it.
- **M08 Industrial hall:** tall wall, long structural bays, large openings, shallow roof and broad service side.
- **M09 Narrow row frontage:** party-wall constraints, street/yard facades, coherent neighbor cornice lines.
- **M10 Adaptive reuse:** preserve primary structural bay pattern while swapping facade/door usage.

## C2. Roof families

- **RF01** Flat roof, parapet, drainage/scuppers, optional plant rooms.
- **RF02** Gable, eaves, soffits, believable end walls.
- **RF03** Hipped and pyramidal, correct ridges/hips.
- **RF04** Skillion/mono-pitch; useful for tenements, sheds, contemporary houses.
- **RF05** Mansard/gambrel, dormers, substantial geometry (not a disguised gable).
- **RF06** Half-hip / regional variants where appropriate.
- **RF07** Saw-tooth industrial roof with clear glazed/opaque faces and true roof silhouette.
- **RF08** Complex footprint roof joins (L/U/wings), closed water-shedding geometry, no floating ridges.
- **RF09** Roof terrace with railings/doors only if the terrace is geometrically usable.
- **RF10** Strongly distinctive roofs (dome, tower, spire, barrel vault) behind explicit evidence/recipes or handmade override.

## C3. Facades and openings

- Street/courtyard/service/party/sliver exposure classes.
- Modular window families: small dwelling, tall tenement, factory clerestory, office curtain wall, storefront, glass-block stairwell, religious/civic special.
- Entrances: pedestrian main, service, shop, vehicle garage, warehouse loading, public lobby, gate/wicket, roof/basement access.
- Balcony families: projected, recessed loggia, Juliet railing, corner balcony, terrace; enough clearance and plausible interior connection.
- Functional modules: school class windows, hospital patient windows, fire-station vehicle bays, cinema blank auditorium, supermarket back-of-house, depot vehicle portals.
- Details: lintel, sill, jamb, cornice, panel seam, column, pilaster, downpipe, gutter, skylight, vent, shutter, signboard, exterior stair, porch.
- Public-side and utility-side rules should affect geometry, not just material assignment.

## C4. Site and region

- Ground sampled around footprint; door thresholds connect to sensible street/yard grades.
- Retaining walls or walk-out basements on slopes only if footprint/site handling supports them.
- Adjacent touching buildings get party walls; narrow unusable gaps should not receive windows.
- Use nearby roads and entrances to orient facades, deliveries, garages and public doors.
- Region presets can change **selection weights, palettes and subfamilies**, not move roads or alter OSM footprint data.
- OSM may contain only `building=yes`; **record uncertainty** and choose modest plausible defaults, never claim specific historic authenticity without evidence.

---

# PART D — ARCHITECTURAL PACKS / SELECTION PRESETS

These packs are **curated mixes of type + style + palette**, useful later as data rather than giant `if` chains. They are not all intended as immediate features.

### P01 — Contemporary Warsaw / Mazowsze (practical first)
PRL slabs and point towers; postwar infill; historic street frontages where supported; offices and mixed-use blocks; newer apartment buildings; villas and detached houses; warehouses and factories; garages, shops, kiosks, schools, clinics and public offices. **Priority:** density, believable frequency, performance and recognizability.

### P02 — Rural Mazowsze
Traditional cottages, postwar cubes, farmhouses, barns, utility sheds, small workshops, chapels, village schools, corner shops, greenhouses and agricultural halls. **Priority:** everyday small buildings with coherent roof silhouettes.

### P03 — Historic Polish town
Attached tenements, corner frontages, brick workshops, town hall, churches, market hall and early civic buildings. **Priority:** street/courtyard logic and period-conscious facades, not randomly lavish ornament.

### P04 — Industrial district through time
Brick factories, older warehouses, PRL concrete works, metal sheds, depots, heating plants. **Priority:** readable era transitions and honest loading/utility sides.

### P05 — Brutalist Warsaw (alternate reality)
Reuse offices, apartments, houses and civic building functions; re-dress them in strong brutalist massing/facade vocabulary without replacing the city map. **Priority:** a convincing family fingerprint, not one concrete material everywhere.

### P06 — Contemporary European city
Glass offices, residential towers, modern apartment courtyards, shopping centres, transport hubs, hotels, clinics. **Priority:** plausible mixed-use, towers with coherent massing, office-vs-home distinction.

### P07 — Stylized / fantasy settlement (future)
Crooked cottages, timber taverns, workshop huts, castles and civic halls as authored families. **Priority:** silhouette personality and game accessibility. Keep this **separate** from the real-world Mazowsze default.

---

# PART E — EXAMPLE DATA RECIPE (CONCEPT ONLY)

**Do not paste this verbatim into current code without checking existing schema.** This illustrates the information a recipe *could* describe; Sage should map it onto the current library's actual request, style, and export model.

```json
{
  "catalogue_id": "BB-R036",
  "display_name": "Modern curtain-wall office",
  "function": "office",
  "style_family": "contemporary_glass_office",
  "eligibility": {
    "osm_building_hints": ["office", "commercial"],
    "preferred_evidence": ["office=*", "start_date", "height", "building:levels"]
  },
  "massing": {
    "families": ["single_block", "multi_wing", "podium_tower_when_parts_exist"],
    "respect_mapped_footprint": true
  },
  "facade": {
    "street": ["glazed_lobby", "curtain_wall_grid"],
    "courtyard": ["curtain_wall_grid"],
    "service": ["service_access", "reduced_glazing"],
    "party": ["blank_wall"]
  },
  "roof": ["flat_with_parapet", "screened_mechanical_zones"],
  "palette_family": ["silver_glass", "charcoal_glass", "warm_stone_glass"],
  "variation": ["mullion_spacing", "vertical_vs_horizontal_emphasis", "lobby_width"],
  "validate": ["door_reachability", "street_oriented_lobby", "no_party_wall_glass", "lod_budget"]
}
```

The actual asset/export protocol and recipe identity should remain compatible with BadBuilding/Noor Interchange and other supported export paths. Avoid coupling recipe selection to a single game engine.

---

# PART F — HOW TO REVIEW THE COOKBOOK

For Sage, a compact review table to populate *after* inspecting the actual repository:

| Recipe/group | Existing implementation | Verdict | If incomplete, smallest useful next step |
|---|---|---|---|
| R001–R014 homes/outbuildings | inspect generator, roofs, gallery | TBD | subtype or geometry fix, not duplicate generator |
| R015–R028 urban housing | inspect PRL/kamienica/plain | TBD | distinguish existing subtypes where needed |
| R029–R043 commerce/offices | inspect shops, industrial, office code | TBD | add function-specific facades and massing |
| R044–R058 civic/culture | inspect type dispatch | TBD | select common types first |
| R059–R071 industrial/transport | inspect all industrial variants | TBD | extend rather than replace shared industrial grammar |
| R072–R078 agricultural | inspect Plain and industrial | TBD | add rural identity without needless complexity |
| S01–S25 style vocabulary | inspect current style selectors/metadata | TBD | shared grammar + constrained variations |
| RF01–RF10 roof types | inspect roof generator and tests | TBD | fix shape fallback or join geometry first |
| M01–M10 massing systems | inspect site, footprint, parts | TBD | focus on missing shared capability |

## Sensible evaluation order (suggestion, not a directive)

1. **Map coverage:** What common structures still look like generic boxes in the actual Baltic Drive map? Count by type/footprint if practical.
2. **Distinctiveness:** Which missing type would make the largest visible difference to a street or skyline?
3. **Shared leverage:** Is a roof/parts/entrance fix needed by 20 recipes? That may be worth more than one standalone building.
4. **Cost and quality:** Prefer a strong, low-cost family over a fragile over-detailed one that hurts streaming.
5. **Source confidence:** Strong OSM evidence wins over guessed history. Unknown means conservative fallback.
6. **Testability:** Each accepted addition needs real and synthetic examples plus regression tests.

### Acceptance check for any newly implemented recipe

- Recognizable *without* its label, across several seeds.
- Correct basic function (hospital vs office, depot vs apartment, shop vs factory).
- Fits mapped outline/parts and makes sense with surrounding streets and neighbouring walls.
- No door to nowhere, impossible loading bay, windows through party walls, unsupported roof or unreachable courtyard.
- Reasonable material palette and regional/era coherence; mapped facts respected.
- Predictable and reproducible result with recorded provenance.
- LOD/collision/markers/exports valid for the current contracts and affordable at streamed city scale.
- Does not regress existing styles.

---

## Final note to Sage

**This is a cookbook, not a backlog.** Read what's already present, cross out recipes we have, mark partial ones, and pick missing families that make a visible difference. The goal is **a believable variety of buildings**, not the highest possible recipe count. A building should look as if people could have used it and as if it belongs to its street.

If something needs a reusable building component, make that component. If a family only needs better selections or materials, improve the existing family. If a landmark requires hand modeling, say so. And please don't replace working code just because we arrived with a shiny new cookbook.

*Bad Ideas Works: an extremely serious architectural department, supervised by a woman who keeps asking whether the stairs lead somewhere. — Noor 💜*
