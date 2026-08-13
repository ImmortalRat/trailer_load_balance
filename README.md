# Trailer Load Balance

A self-hosted web app for visually planning cargo placement in a camper trailer, with real-time
tongue weight and axle load calculations. Built with .NET 10 / Blazor Server for a home-server
deployment (LAN-only, no auth, single container).

See [`docs/requirements.md`](docs/requirements.md) for the research and engineering rationale
behind the physics model and the bundled trailer profile.

## Features

- Top-down floor plan of the selected trailer, with axle/wheel positions and an approximate
  interior layout (dinette, galley + fridge, bunks, bathroom) shown for reference.
- Drag a cargo item from the palette straight onto the trailer to place it, or click to add it;
  reposition by dragging, resize/rename/recolor via the property panel.
- Real-time tongue weight (lb and % of total), total weight vs. GVWR, and per-axle load vs. GAWR.
- Adjustable ground tilt (e.g. for an off-level driveway) that correctly factors cargo/equipment
  height into the load calculation.
- Configurable equipment (e.g. a rooftop A/C) with an editable weight and installed/removed toggle.
- Profile selection and cargo layout persist per browser (localStorage) and stay in sync across
  tabs.
- Bundled profile: 2012 Coleman by Dutchmen M-15BH.

## Running locally

Requires the .NET 10 SDK.

```bash
cd src
dotnet run --project TrailerLoadBalance.Web
```

Then open the URL printed in the console (defaults to `http://localhost:5254` in development).

Run the test suite:

```bash
cd src
dotnet test
```

## Running with Docker Compose

```bash
docker compose up -d --build
```

The app listens on `http://<host>:8080`. It's meant for LAN-only use behind your router/firewall
- there's no authentication, HTTPS, or multi-user isolation.

## Adding or updating a trailer profile

Trailer profiles are static JSON files under
`src/TrailerLoadBalance.Web/Data/TrailerProfiles/`, loaded at startup. There's no in-app profile
editor by design - add a new JSON file (or edit an existing one) and redeploy. See
`docs/requirements.md` §6 for the field reference, and the bundled M-15BH profile for a worked
example with sourcing notes.
