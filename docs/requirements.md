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
- **FloorZone** (profile-level, not user data): a labeled reference rectangle (`Name, Kind
  ("room" | "fixture"), XMinIn/XMaxIn/YMinIn/YMaxIn`) drawn on the floor plan so the user can see
  what's already built into the trailer (dinette, galley, bunks, bathroom, and a refrigerator
  fixture within the galley) versus open floor. Visual reference only - cargo is not blocked from
  being placed over a zone, since real cargo often does go on/under/in furniture. See §8.2 for the
  bundled profile's zone data and its sourcing.
- **Wheel/axle rendering geometry** (profile-level): `WheelWidthIn`, `WheelDiameterIn`,
  `TrackWidthHalfIn` (half the hub-to-hub distance). Purely visual (not used in the load
  calculation) - lets the floor plan show actual wheel positions next to the axle centerline.

## 7. Scope decisions (beyond the sourced/derived numbers above)

A few implementation choices were made to keep the first version focused on what was explicitly
asked for. Some were later revisited based on user testing - see §8.

- **Cargo bounds** are enforced (drag is clamped to the trailer's usable floor area, from a
  `cargoBounds` field on the profile). Interior room/fixture zones are now rendered too (§8.2),
  but remain visual-only - not enforced as hard collision.
- **Resizing** cargo is via the numeric property editor, not drag-handles on the floor plan.
- Cargo height is fixed at 12 in per the brief ("for now"); the field exists per-item so a future
  predefined cargo type can carry its own height.

## 8. Post-launch fixes and additions (from user testing)

### 8.1 Circuit-killing localStorage bug

**Symptom reported:** cargo couldn't be added by clicking the palette button or by dragging -
the page appeared completely unresponsive to input.

**Root cause:** in a browser with `localStorage` blocked (private/incognito windows, strict
privacy settings, some mobile/corporate browser configurations), `localStorage.getItem`/`setItem`
*throw* rather than silently no-op. That exception propagated up through the JS interop call into
`RestoreStateAsync`, called from `OnAfterRenderAsync` - an unhandled exception in a Blazor Server
lifecycle method tears down the entire SignalR circuit. Once the circuit is gone, the page is
still visible (it was already rendered) but permanently inert: no click, no drag, nothing calls
back to the server anymore. This was confirmed by reproducing it directly (stubbing
`window.localStorage` to throw a `SecurityError`, matching what real browsers do) and observing
the exact symptom, then confirming the fix resolves it.

**Fix:** persistence is a nice-to-have, not core functionality, so every layer that touches it now
degrades gracefully instead of propagating:
- `app.js`'s `saveState`/`loadState`/`registerSync` wrap `localStorage` access in `try/catch`,
  logging a console warning and returning `null`/no-op on failure instead of throwing.
- `PersistenceService` (C#) additionally wraps every JS interop call in `try/catch` for
  `JSException`/`JSDisconnectedException`/`OperationCanceledException`, as defense in depth against
  any other interop failure mode.
- Both `OnAfterRenderAsync` call sites (`Home.razor`'s state restore, `TrailerFloorPlan.razor`'s
  drag-interaction init) now catch those same exception types around their JS interop calls, so a
  failure there degrades to "this session isn't persisted" / "drag isn't interactive" rather than
  killing the whole page.

### 8.2 Interior floor plan zones

The room order established in the original research (front dinette → mid galley → rear bunks +
bathroom) is confirmed but no dimensioned floor-plan diagram is publicly available for this model
(checked RV spec databases, dealer listings, and brochure archives again specifically for this).
Zone boundaries are therefore **proportional estimates**, not sourced measurements, split across
the 180 in body (starting at X=45, the front wall) in the room order confirmed by research:

| Zone | X range (in, from hitch) | Basis |
|---|---|---|
| Dinette (converts to bed) | 45–99 (54 in deep) | Proportional estimate; U-dinette spans full width per research. |
| Galley | 99–153 (54 in) | Proportional estimate. |
| Bunks + closet | 153–201 (48 in) | Proportional estimate; the derived axle position (159 in, §4.2) falls inside this zone, consistent with a rear-loaded mass distribution. |
| Bathroom | 201–225 (24 in) | Proportional estimate; matches rear-most position from research. |
| Refrigerator (fixture) | 99–123 in (galley), road-side wall | Estimate: ~24×24 in footprint, placed along the wall opposite an assumed curb-side entry door (common RV convention - entry door side for this specific unit was not found in research). |

These are rendered as labeled, low-opacity background regions (rooms) and a darker fixture
rectangle (fridge) - purely for spatial reference, not enforced as placement limits. If a factory
floor-plan diagram is ever obtained, replace these estimates with sourced boundaries.

### 8.3 Wheel/axle rendering fix

The floor plan originally rendered the axle line and the roof A/C marker at `tongueLengthIn +
PositionFromHitchIn` / `tongueLengthIn + eq.XIn`. This double-counted the tongue length: both
`AxleSpec.PositionFromHitchIn` and `EquipmentItem.XIn` are already defined (and used by
`LoadCalculationService`) as absolute distances from the hitch ball, the same coordinate origin
the rest of the SVG uses (X=0 at the hitch). The bug shifted the axle/wheels and the A/C marker 45
in too far toward the rear - visually placing the A/C over the galley instead of the dinette,
contradicting §4.3's sourced placement. Fixed by using the fields directly with no added offset;
covered by the existing coordinate-frame documentation in the `TrailerProfile` doc comment, which
was correct - only the rendering code had the bug.

Wheels are drawn as two rectangles straddling the axle line at `±TrackWidthHalfIn` from
centerline, sized `WheelWidthIn × WheelDiameterIn` (§6).

### 8.4 Drag-from-palette-to-canvas

User testing showed the expectation was to *drag* a palette item directly onto the trailer to
place it, not just click it. First attempt used native HTML5 drag-and-drop (`draggable="true"` +
`dragstart`/`dragover`/`drop`) - this let the drag start but the drop never landed. Root cause:
Safari (and other browsers to varying degrees) does not reliably populate `dataTransfer.types`
during `dragover`, so a conditional `preventDefault()` gated on checking those types silently
never runs, and per the HTML5 DnD spec a `drop` event only fires if `dragover` called
`preventDefault()`. This is a known cross-browser rough edge with the native API, not something
worth working around per-browser.

**Fixed by dropping native HTML5 DnD entirely** and reusing the same Pointer Events mechanism
already proven for repositioning cargo on the canvas (§6, `pointerdown`/`pointermove`/`pointerup`,
bound once on `document` since palette buttons live outside the floor plan's own container). A
plain click/tap (no movement past a small pixel threshold) is left untouched so Blazor's normal
`@onclick` "click to add" keeps working as a fallback; once the pointer moves past the threshold,
a small floating icon "ghost" follows the cursor and the floor plan gets an outline highlight
while hovered, and on release - if released over the floor plan - the drop position (inches) and
catalog id are sent to Blazor via the same `OnCatalogDrop` callback as before. No native
`DataTransfer` API is used at any point. The dropped item is centered under the cursor and clamped
to the profile's cargo bounds.

### 8.5 The actual root cause: `_framework/blazor.web.js` missing from the Docker image

None of the fixes above (8.1, 8.3, 8.4) were the real problem. User testing after each fix kept
hitting the same wall - eventually a browser console error surfaced it directly: a 404 for
`/_framework/blazor.web.js`, the script that boots the entire Blazor Server client runtime. If it
can't load, the SignalR circuit never starts and the page is permanently inert - no click, no
drag, no slider does anything - which retroactively explains every symptom reported across all
three rounds of "testing" (they were all downstream of the same missing file, not separate bugs).

The first hypothesis (browser caching a stale HTML page referencing an old content-hashed
filename after a redeploy, §8.1's `Cache-Control` fix) was plausible and worth fixing regardless,
but the user did a full clean rebuild (`--no-cache` + `--force-recreate`) and a hard refresh and
still hit the identical 404 - ruling out staleness as *the* cause here.

**Confirmed root cause** (reproduced directly by standing up a real Docker daemon and running the
actual multi-stage build): the original `Dockerfile` ran `dotnet restore` *without* `-c Release`,
then `dotnet publish -c Release --no-restore` in a later layer:

```dockerfile
RUN dotnet restore TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj
...
RUN dotnet publish TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj -c Release -o /app --no-restore
```

Because the two commands target different configurations, and `--no-restore` skips re-resolving
anything, the Release-specific static web asset generation - which is what copies
`wwwroot/_framework/blazor.web.js` (sourced from the `Microsoft.AspNetCore.App.internal.assets`
NuGet package) into the publish output - was not reliably produced. This was verified two ways:
(1) a full real `docker compose build` produced a working, running image whose
`wwwroot/_framework/` directory was completely absent; (2) an A/B test with an offline NuGet
cache mounted (to control for network variability) showed the split
restore-then-`--no-restore`-publish sequence still reaching for the network and failing even with
every package already available locally, while a *single* `dotnet publish -c Release` (implicit,
correctly-configured restore) succeeded from the same offline cache and produced a complete
`_framework/` directory every time.

**Fix:** the Dockerfile now runs one `dotnet publish -c Release` with no separate restore step, so
restore always resolves for the exact configuration being published. This trades a small amount of
Docker layer-caching (restore can no longer be cached independently of full source changes) for
a build that doesn't silently ship without a working UI - the right trade for a small, infrequently
rebuilt home-server app. Re-verified end-to-end against a real container: click-to-add,
palette-drag-to-add, tilt slider, and equipment weight override were all tested via a live
browser against the actual built image and confirmed working.
