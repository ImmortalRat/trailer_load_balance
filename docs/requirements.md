# Trailer Load Balance — Requirements & Design Rationale

This document records the research and engineering reasoning behind the app, so future trailer
profiles and features can be added consistently. It was produced by: (1) parallel web-research
passes on the specific trailer, rooftop A/C weights, and towing/parking attitude conventions,
then (2) a synthesis pass that resolved conflicts in that research and derived the exact numbers
and formulas used by the app.

## 1. Product scope

- .NET 10, Blazor Server (interactive server rendering) - simplest deployment for a home-LAN app,
  full C# on the server, no WASM payload, negligible latency on a local network.
- No auth, no scaling, no observability stack - single container, docker compose, LAN-only.
- Trailer profiles are static JSON files bundled under `Data/TrailerProfiles/`, loaded once at
  startup (`TrailerProfileService`). Adding or changing a trailer requires editing/adding a JSON
  file and redeploying - there is intentionally no in-app profile designer.
- One bundled profile: **2012 Coleman by Dutchmen M-15BH** (serial pattern `CTS15BH`).

## 2. Physics/geometry decision: 2D vs. 3D

**Decision: store 3D positions (X, Y, Z) but the app only renders a 2D top-down floor plan.**

Reasoning:

- Travel trailers are towed **level** (hitch-maker guidance: level, or up to ~1.75 in nose-down
  over the trailer's length - well under 1°; nose-up is discouraged) and are expected to be
  **level when parked** (RV appliances, esp. absorption fridges, and plumbing require it; the
  industry-standard tongue-weight measurement procedure also requires a level trailer on level
  ground). So the *common case* is exactly tilt = 0.
- At tilt = 0 the load-balance calculation is **exactly** the textbook flat 2D moment balance
  along the trailer's length (X only) - height (Z) drops out of the math completely. A 2D model
  would be sufficient if the trailer were always level.
- However, real home driveways commonly slope **6-11°**, and small single-axle trailers are
  routinely parked on them without leveling blocks. The user wanted an adjustable tilt control
  for exactly this scenario.
- Once tilt is non-zero, an item's **height above the floor** changes its effective horizontal
  lever arm relative to the hitch/axle supports (rigid-body rotation - see §5). This effect is
  large enough to matter: in the bundled profile, going from level to a realistic 6° driveway
  tilt swings tongue weight from ~364 lb to ~440 lb (+21%) - enough to walk the trailer out of
  the recommended 10-15% tongue-weight band. A pure 2D (X-only) model would silently give wrong
  answers the moment tilt is non-zero.
- **Y (side-to-side) does not enter the calculation at all.** With one lumped axle reaction
  (single axle, or a tandem group treated as one support - see §5.7), the fore-aft moment balance
  is independent of lateral position. Y exists purely for layout/rendering and drag-and-drop
  bounds checking, not physics. This is why the UI stays a 2D top-down view even though the data
  model carries a Z axis.

Net effect: cargo is drawn as flat rectangles in a top-down view; each cargo item and each
equipment item carries an X/Y/Z position, but Z (and the trailer's own floor/ball heights above
the *ground*) only affect the numbers once the user moves the tilt slider off zero.

## 3. Tilt angle control

| Property | Value | Why |
|---|---|---|
| Default | 0.0° (level) | Towing and parking are both expected level; this is the "correct" baseline. |
| Range | −12.0° to +12.0° | Real driveways run 6-11°; ±12° covers the worst realistic case in both directions with headroom. |
| Step | 0.5° | Finer than a user can judge by eye. |
| Sign convention | Positive = nose-down / tongue-low (normal towing attitude). Negative = nose-up. | Matches hitch-maker guidance framing ("level, or nose slightly down"). |
| Display | Degrees + equivalent grade %, plus a caption on towing vs. parking targets. | Keeps both a precise number and an intuitive "like a driveway" framing. |

Modeling assumption stated in the UI: the tilt control represents an off-level *parked* scenario
(wheels chocked/braked, weight resting on the axle and a jack or the hitch), not a mid-tow dynamic
condition - which is consistent with how tongue weight is normally measured and reasoned about.

## 4. 2012 Coleman by Dutchmen M-15BH — exact profile values

### 4.1 Research summary (as gathered)

Public RV spec databases (RVGuide and similar) do not carry 2012-specific data for this exact
floorplan; the closest available years are 2013-2017. Where the 2012 model's own numbers weren't
publishable, the app uses the nearest verifiable year's data plus targeted engineering estimates,
each labeled below. Key confirmed facts:

- GVWR 3800 lb, dry weight 2715 lb, and a 317 lb dry/factory tongue weight all come from the same
  (2013) data set and are mutually consistent (317/2715 ≈ 11.7%, in the normal dry-trailer range).
- GVWR (3800) − GAWR (3500) = 300 lb, an internally consistent single-axle rating margin (the
  tongue must carry ≥300 lb at full GVWR or the axle is over-rated before the GVWR limit binds) -
  this corroborates single axle / 3500 lb GAWR as given.
- Overall length (ball to bumper) is consistently 225 in across nearby years; "15" in "M-15BH"
  implies an 18 ft (216in-ish)... resolved as **180 in body + 45 in tongue = 225 in overall**,
  which also lands inside the industry-typical 36-48 in tongue-length band for small trailers.
- Exterior height figures split into two clusters (104 in vs. 118.8 in) across nearby years -
  resolved as **bare roof (104 in) vs. with-factory-A/C (118.8 in)**, since the gap (14.8 in)
  matches a rooftop A/C shroud height almost exactly.
- Floorplan (confirmed): front U-dinette/convertible bed → mid kitchen/galley → rear bunks +
  closet + bathroom. This ordering informed the dry-structure mass distribution (§4.2) and the
  A/C's front-of-roof placement (over the dinette, the largest clear roof span).
- Rooftop A/C weight: no single "correct" model is documented for this specific trailer, so the
  app estimates from six real 13,500-BTU-class rooftop units (the common size for a small
  single-axle TT): roof-only weights of 68.2/77/77.6/79.5/90/100 lb, mean ≈ 82 lb, plus an
  estimated ~13 lb interior ceiling assembly → **95 lb installed default** (user-configurable,
  40-150 lb range). A listing mentioning an "8,000 BTU" unit for this trailer was rejected as
  implausible (no mainstream rooftop RV A/C is made that small); sizing to the 13,500 BTU class
  is the conservative (heavier) choice.

### 4.2 Axle position — the critical missing number

No frame diagram exists publicly for this model, so axle position was **derived**, not sourced,
by building a component-level dry-mass table for a small bunkhouse trailer (frame, floor, shell,
dinette, galley, bunks, bathroom, appliances, tanks, axle/suspension - see the synthesis notes)
and solving the moment-balance equation `TongueWeight = DryWeight·(AxleX − CgX)/AxleX` for the
axle position that reproduces the known 317 lb dry tongue weight. Result: **AxleX = 159.0 in**
from the hitch ball (114 in aft of the front wall, 63% of the 180 in body - a typical position
for a trailer with mid/rear-loaded galley and bathroom mass). This is an engineering estimate;
if the actual door placard or a frame diagram ever becomes available, replace this value.

The dry structure's effective center of gravity (X and height) is **not stored** - it's derived
at load time from `factoryTongueWeightLb` so it always self-consistently reproduces the
manufacturer's number at level tilt (see `LoadCalculationService.DryStructureCenterOfGravity`).

### 4.3 Bundled profile values and provenance

| Field | Value | Basis |
|---|---:|---|
| Body length | 180.0 in | Derived: 225 in overall − 45 in tongue; matches "15" in M-15BH. |
| Body width | 96.0 in | Sourced (2013-2017 spec sheets). |
| Interior height | 78.0 in | Estimate (display only): 104 exterior − floor/roof structure. |
| Interior width | 91.0 in | Estimate: 96 − wall thickness. Bounds-checking only. |
| Tongue length | 45.0 in | Derived (225 overall − 180 body); within typical 36-48 in band. |
| Floor height above ground | 22.0 in | Estimate: wheel radius + suspension + frame + deck. |
| Ball height above ground | 18.0 in | Estimate: typical coupler height for a 3500 lb single-axle TT. |
| Dry CG height above floor | 19.0 in | Estimate from a component mass/height table (frame & axle low, shell & cabinetry mid). |
| GVWR | 3800 lb | Given, corroborated by 2013 spec sheets. |
| GAWR | 3500 lb | Given, corroborated by a 2011 Transport Canada recall notice for this model family. |
| Dry weight | 2715 lb | Given, matches 2013 spec sheets. |
| Factory (dry) tongue weight | 317 lb | Sourced, same 2013 data set as GVWR/dry weight (internally consistent). |
| Advisory max tongue weight | 500 lb | Estimate (~13% of GVWR); warning only, not a hard limit. |
| Axle position from hitch | 159.0 in | **Derived** (§4.2) - no source diagram exists. |
| Axle rating | 3500 lb | = GAWR (single axle). |
| Roof A/C position (X, Y, Z) | 80, 0, 88 in | X: estimate (front-of-roof, sourced qualitatively). Y: centered (sourced). Z: derived from the 104in/118.8in height pair. |
| Roof A/C default weight | 95 lb | Estimate: mean of six 13,500-BTU-class units (82 lb) + ~13 lb ceiling assembly. Configurable 40-150 lb. |
| Default tilt | 0.0° | Trailers are towed/stored level. |
| Tilt range | −12° to +12° | Covers realistic driveway slopes both directions. |

Full JSON: `src/TrailerLoadBalance.Web/Data/TrailerProfiles/coleman-dutchmen-2012-m15bh.json`.

## 5. Load calculation model

Coordinate frame: **X** = inches aft of the hitch ball (0 = ball). **Y** = inches from the
trailer's longitudinal centerline (not used in the calculation - layout only). **Z** = inches
above the trailer **floor** (0 = floor). For rectangular cargo, X/Y is the item's front-left
(min-corner) edge; for equipment (a point mass), X/Y/Z is the item's center of mass directly.

The trailer is modeled as a rigid body supported at two points: the **hitch coupler** and the
**axle group** (for a single axle, just that axle; for a future multi-axle profile, the
rating-weighted centroid of all axles - see §5.7). Tilting the trailer rotates the whole body
about the axle group's ground contact point (wheels stay put; tongue height changes to create
the tilt).

**Lever arm** (world/ground-frame horizontal distance from the axle contact patch) for a mass at
floor-relative `(x, z)`:

```
u(x, z) = (axleX − x)·cos(tilt) + (z + floorHeightAboveGround)·sin(tilt)
```

**Hitch's own lever arm** (the ball sits `ballHeightAboveGround` above the ground, not the floor):

```
u_hitch = axleX·cos(tilt) + ballHeightAboveGround·sin(tilt)
```

**Dry structure**, modeled as one point mass, positioned so it reproduces the known/estimated
factory tongue weight at level:

```
dryCgX = axleX · (1 − factoryTongueWeightLb / dryWeightLb)   // fallback 12% if unknown
dryCgZ = dryCgHeightAboveFloorIn
```

**Moment sum** (dry structure + installed, non-dry-weight-included equipment + cargo, cargo using
its geometric center):

```
M = dryWeightLb · u(dryCgX, dryCgZ)
  + Σ_equipment  w_e · u(e.X, e.Z)          // only if installed and not already counted in dry weight
  + Σ_cargo      w_c · u(c.X + c.Length/2, c.Z + c.Height/2)
```

**Results:**

```
TongueWeightLb      = M / u_hitch
TotalWeightLb        = dryWeightLb + Σ w_equipment + Σ w_cargo
TotalAxleLoadLb       = TotalWeightLb − TongueWeightLb
TongueWeightPercent   = 100 · TongueWeightLb / TotalWeightLb
```

**Sanity check (tilt = 0):** `cos 0 = 1, sin 0 = 0` → `u = axleX − x`, `u_hitch = axleX`, so the
formula collapses to `TongueWeight = Σ w_i·(axleX − x_i) / axleX` - the standard flat 2D moment
balance, confirming Z only matters once the trailer is off-level (verified by an automated test).

### 5.7 Multi-axle split (future profiles)

The axle group is collapsed to its rating-weighted centroid X for the tongue-weight calculation
(tandem-with-equalizer suspensions are designed to distribute load between axles regardless of
where the load sits within the group). The group's total axle load then splits **proportional to
each axle's rated capacity**:

```
axle_i.LoadLb = TotalAxleLoadLb · (axle_i.RatingLb / Σ RatingLb)
```

For equal-rated tandems this is the standard 50/50 assumption. Each axle is checked against both
its own rating and the combined GAWR.

### 5.8 Warnings surfaced in the UI

| Condition | Severity |
|---|---|
| Tongue weight ≤ 0 | Error - trailer would tip back off the hitch |
| Total weight > GVWR | Error |
| Axle load > GAWR (per axle or combined) | Error |
| Tongue weight > advisory max | Warning |
| Tongue weight % outside 10-15% of total | Warning |

## 6. Cargo & equipment data model

- **CargoItem**: `Id, Name, Color, XIn, YIn (min-corner), LengthIn, WidthIn, HeightIn (fixed at
  12in for the only current type), ZIn (bottom-face height, 0 = floor), WeightLb`. User-editable
  via a non-modal property panel (name, color, dimensions, X/Y position, weight); dragged in the
  floor plan for position.
- **EquipmentItem**: `Id, Name, XIn/YIn/ZIn (center of mass), DefaultWeightLb,
  WeightIsConfigurable, MinWeightLb/MaxWeightLb, IsIncludedInDryWeight, IsInstalledByDefault,
  Notes`. Fixed position (physically installed), configurable weight, and an installed/removed
  toggle (relevant since the A/C is likely aftermarket and not every unit of this trailer has one).
- **CargoCatalogItem**: the palette shown in the UI. Today it holds one entry (a customizable
  generic box); the model already carries icon/fixed-dimension/default-weight fields so future
  predefined cargo types (coolers, totes, bikes, ...) slot in without UI changes.
- **PersistedState**: profile ID, tilt, equipment weight overrides, equipment installed flags,
  and the full cargo list - serialized to `localStorage` (key `tlb.state.v1`) so a session
  survives a reload. Cross-tab sync uses the browser's native `storage` event (fires in other
  already-open tabs automatically) plus a `visibilitychange` listener as a fallback so switching
  back to a stale tab refreshes it.

## 7. Scope decisions (beyond the sourced/derived numbers above)

A few implementation choices were made to keep the first version focused on what was explicitly
asked for:

- **Cargo bounds** are enforced (drag is clamped to the trailer's usable floor area, from a
  `cargoBounds` field on the profile) but **obstruction zones** (e.g. wheel wells) and named
  interior "rooms" are not modeled or rendered - not requested, and collision-aware placement
  would meaningfully expand scope for a first version.
- **Adding** a cargo item is a click on the palette (which places it at a sensible default spot
  and selects it); **repositioning** is drag-and-drop on the floor plan, and **resizing** is via
  the numeric property editor. Full native-HTML5 drag-from-palette-to-canvas was considered but
  skipped - Blazor Server's `DataTransfer` support for that pattern is unreliable, and this
  simpler flow satisfies the "drag and drop to place cargo" requirement without that risk.
- Cargo height is fixed at 12 in per the brief ("for now"); the field exists per-item so a future
  predefined cargo type can carry its own height.
