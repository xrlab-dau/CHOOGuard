# Busan Station interior digital twin — 1F, 2F majibang, bridge, over-track box, 3F

Goal (user, 2026-09-24): interior modeling of every public interior zone at the fidelity of the original
map's exterior model (official shell: textured facades, mullions, canopies, fixtures), as a digital twin.

## Zones and live boundary (audit 2026-09-24T12:4x, `zones.json`)
| Zone | Footprint | Implemented now (active) | Missing for exterior-level fidelity |
|---|---|---|---|
| 1F interior | 7,446 u² enclosure (Y0) | floor, source shell, 16 units (class b + corrections), 3 observed finish patches | public ceilings/lights, columns, corridor walls + service doors, cores/toilets/lockers, 1F↔2F escalators/elevators, shop interiors, signage, floor finish detail |
| 2F main (main building) | 8,257 u² (Y7) | generated floor, 41 unit shells + fascias, kit fixtures, ticket windows/machines, lockers, service walls, shop ceilings | hall ceiling/lighting, columns, wall finishes, detailed storefronts + interiors, wayfinding, gates/doors, corrected storefront positions (2F 복순도가/꽃들 ~19 u) |
| 2F bridge (seam) | 116 u² | floor, a wall panel, ceiling pieces | enclosure/finishes of the link, signage |
| 2F box (over-track hall) | 6,168 u² (Y7) | floor, hall roof structure (trusses, tree columns), 23 unit shells, seating, boards, 2F→3F escalators | platform gates/stairs/escalators down to platforms, finishes, storefront detail, lighting |
| 3F (box upper) | L-shaped wing (Y12.2) | slab, 8 units, rails, fixtures | storefront/interior detail, ceilings, lighting, signage |

Exterior reference (MainShell): 73,606 m² surface, 662,583 tris (9.0/m²), 54 % textured, 132 materials.

## Rules
- Know the boundary first: every build step starts from a live audit and ends with a before/after ownership diff
  (only the zone's own root may change), boundary checks (enclosure, protected owners, observed finishes,
  walked camera paths), Play-mode route walks and matched-view renders against registered frames.
- Evidence tiers on every generated object: a (registered, passes held-out), a-failed (registered, fails strict gates),
  b (topology/fit only), c (no evidence → not built). Never invent positions; appearance detail may use the
  PBR/kit library when the element's existence and position are evidenced.
- One integration owner (Main) writes the scene; lanes write code/specs/assets only and never switch scenes.
  `AgentScripts/MajibangBuilder.cs` has one editor at a time (kit lane while it runs).

## Waves
1. Kit + evidence (parallel): interior element kit (ceilings, columns, cladding, storefront system, shop interiors,
   doors, signage, circulation) in MajibangBuilder; 2F/bridge/box/3F evidence inventory + matched-view gap list;
   SY0 1F walk registration; 2F frontage reconstruction (RepairTexturePipeline).
2. Zone specs: per-zone placement specs (UVY) from evidence, checked by `interior.py`-style boundary checks.
3. Integration per zone (1F → 2F main → bridge → box → 3F): build, audit diff, walks, matched views, fix loop.
