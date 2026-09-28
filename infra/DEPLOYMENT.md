# Deployment Guide (Render)

Covers Assignment 1 Section 14's requirement: a deployed ASP.NET Core
API (health + Swagger URLs), a deployed PostgreSQL database, and a
deployed React app pointed at the live API. The Python agent-service is
intentionally **not** deployed — Section 14 explicitly allows it to
"deploy or run locally as appropriate," and it needs a local Ollama
model regardless. Run it locally for the live demo; see
`agent-service/README.md` for its own startup steps.

## What's already set up in the repo

- `backend/Dockerfile` — builds and runs the ASP.NET Core API.
- `infra/render.yaml` — a Render Blueprint describing all three pieces
  (database, API, static site) as one unit.
- `Program.cs` was changed so migrations, admin-account seeding, and
  Swagger all run in **every** environment, not just `Development` —
  previously all three were gated behind `IsDevelopment()`, which would
  have silently done nothing on a real deployment (unmigrated database,
  no Swagger UI, exactly the two things Section 14 requires working).

## One-time setup

1. **Create a free Render account** at render.com, connect your GitHub
   account, and give it access to this repository.
2. **Dashboard → New → Blueprint**, select this repo, and point it at
   `infra/render.yaml`. Render will show a preview of the three
   resources it's about to create (`blood-donation-db`,
   `blood-donation-api`, `blood-donation-web`) — confirm and apply.
3. During setup Render will prompt for two values it can't safely
   default on its own (see the comments in `render.yaml`):
   - `Seed__AdminEmail` — the email for the first admin account, e.g.
     `admin@yourteam.dev`.
   - `Seed__AdminPassword` — a real password you'll actually use to log
     in as admin on the deployed instance. Pick something you'll
     remember; write it in the deployment report's test-accounts
     section (not a secret you need to protect long-term, but also not
     something to leave as a guessable default).
4. Wait for all three resources to finish their first deploy (a few
   minutes — the API build in particular takes longer the first time).

## After the first deploy — checking the frontend's API URL

Vite bakes `VITE_API_BASE_URL` into the built JS at **build time**, not
read at runtime. The plain names in `render.yaml` (`blood-donation-api`,
`blood-donation-web`) were already taken globally on Render for this
project, so it assigned random suffixes instead — this is exactly what
happened on first deploy here, and it's why login/registration failed
initially (the frontend was built pointing at a URL nothing was
listening on).

- Open the API service in the Render dashboard and copy its actual URL.
- If it doesn't match what's in `render.yaml`'s `VITE_API_BASE_URL`:
  update that value (in the file, or as a live env-var edit on the web
  service) to `<real-api-url>/api`, then trigger **Manual Deploy →
  Clear build cache & deploy** (a plain restart won't rebuild the JS,
  so the old baked-in URL would stick).

## Live deployment (this project)

- API: `https://blood-donation-api-h3cp.onrender.com`
- Web: `https://blood-donation-web-2o3n.onrender.com`

Both confirmed reachable: `/health` → `{"status":"ok"}`, `/swagger` →
200.

## Verifying the deployment

- `https://<api-url>/health` → `{"status":"ok"}`
- `https://<api-url>/swagger` → Swagger UI, all endpoints listed
- `https://<web-url>/` → login page loads, and logging in with the
  seeded admin account reaches the dashboard
- `POST https://<api-url>/api/auth/login` with the seeded admin
  credentials → 200 with a JWT

## Known limitations worth stating in the deployment report

- **Free-tier cold start**: the API service spins down after ~15
  minutes idle and takes ~30–50 seconds to wake on the next request.
  Expected and normal for a no-cost tier — mention it so evaluators
  aren't confused by the first request being slow.
- **Free Postgres expiry**: Render deletes free-tier databases 30 days
  after creation. Create it close to the submission date so the
  30-day window comfortably covers the required access period (through
  21 Oct 2026) — creating it around/after 27 Sept 2026 keeps it alive
  past that date. If it's ever recreated, `Database__*` env vars on the
  API service update automatically (they reference the database
  resource, not a typed-in value), but migrations will need to re-run
  automatically on the next deploy anyway (Program.cs calls
  `MigrateAsync()` on every startup) and the admin account will
  reseed itself.
- **Agent-service stays local**: any live agent-workflow demonstration
  runs against your local stack (local backend + local agent-service +
  local Ollama), not the deployed backend. The deployed backend's own
  `/api/internal/agent/*` routes exist and are reachable in principle,
  but nothing is configured to call them from the cloud in this setup —
  by design, per Section 14's explicit allowance.
