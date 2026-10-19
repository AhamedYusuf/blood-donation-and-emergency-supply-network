# PulsePoint: Blood Donation and Emergency Supply Network

PulsePoint connects hospitals and blood banks that need blood with donors who can give it. When a staff member raises a blood request, a team of AI agents checks stock, finds and ranks nearby compatible donors, and filters them by medical eligibility. The plan then waits for a staff member to approve it before any donor is contacted.

It is a full-stack system with an ASP.NET Core API, a PostgreSQL database, a Python agent service built on LangGraph, a React web app and a Flutter mobile app.

SE3090 group project, Group AI-39, 2026.

## Live deployment

| Part | Link |
| --- | --- |
| Web app | https://blood-donation-web-2o3n.onrender.com |
| API (Swagger) | https://blood-donation-api-h3cp.onrender.com/swagger |
| API health | https://blood-donation-api-h3cp.onrender.com/health |
| Agent service docs | https://blood-donation-agent.onrender.com/docs |
| Agent service health | https://blood-donation-agent.onrender.com/health |

Everything runs on Render's free plan. Services sleep after about 15 minutes without traffic, so the first request after a quiet period can take 20 to 50 seconds. Open both health links first to wake them.

## What it does

| Role | Main app | What they can do |
| --- | --- | --- |
| Donor | Flutter mobile app (and donor pages on the web) | Register a donor profile, check eligibility, see blood requests they can help with, book, accept, decline and reschedule donation appointments, receive notifications |
| Staff | React web app (and request screens on mobile) | Raise blood requests, follow and approve agent workflows, manage their organization's inventory, complete appointments |
| Admin | React web app | Everything staff can do, plus verify donors, manage organizations and invite staff |

A blood request goes through these steps:

1. Staff create a request (blood type, units, urgency, location).
2. The Coordinator agent runs three specialist agents: **Stock-Check** (is there enough stock?), **Matching and Dispatch** in search mode (which compatible donors are nearby, ranked?) and **Eligibility Validation** (who may donate today?).
3. The workflow pauses for human approval. Staff approve, reject or ask for a revision.
4. On approval, the Matching and Dispatch agent books an appointment for each eligible donor and notifies them in the app and by push.
5. The donor accepts. When they have donated, staff complete the appointment, which adds the units to inventory and starts the donor's 90-day wait.

Every agent step is stored in the database with its input and output, so each decision can be audited.

## Architecture

```
  React web app            Flutter mobile app
  (staff, admin)           (donors, staff)
        \                       /
         \   HTTPS + JWT       /
          v                   v
        +-----------------------+   start / resume    +------------------------+
        |   ASP.NET Core API    | ------------------> |  Agent service         |
        |   all business rules  |                     |  FastAPI + LangGraph   |
        |   and data access     | <------------------ |  Coordinator + agents  |
        +-----------------------+  /api/internal/*    +------------------------+
                  |                (shared secret)
                  v
            PostgreSQL                Firebase Cloud Messaging (push)
```

- Only the API reads and writes the database.
- The Python agents hold no business rules. Each one checks its input and calls one internal API endpoint, and the C# code makes the decision. This keeps every rule in one place, covered by the backend tests.
- The apps call `/api/*` with a JWT. The agent service calls `/api/internal/*` with a shared secret in the `X-Internal-Secret` header.
- The agents decide with fixed rules, not a language model, so the same request always produces the same plan.

## Tech stack

| Part | Technology |
| --- | --- |
| API | ASP.NET Core 8 (C#), EF Core with Npgsql, JWT bearer authentication, BCrypt, Swagger |
| Database | PostgreSQL 16, schema managed by EF Core migrations |
| Agent service | Python 3.12, FastAPI, LangGraph (state graph, `interrupt()` for human approval, PostgreSQL checkpoints) |
| Web | React 19, TypeScript, Vite, Redux Toolkit, RTK Query, React Router |
| Mobile | Flutter (Dart), Riverpod, go_router, Firebase Cloud Messaging, flutter_map |
| Testing | xUnit, pytest, flutter_test, Vitest and Testing Library |
| CI and hosting | GitHub Actions, Render (Docker web services, static site, managed PostgreSQL) |

## Repository layout

```
backend/                      ASP.NET Core API
  src/
    BloodDonationNetwork.Domain/          entities and status values
    BloodDonationNetwork.Application/     DTOs, interfaces, services, rule engines
    BloodDonationNetwork.Infrastructure/  EF Core, migrations, external clients
    BloodDonationNetwork.Api/             controllers, middleware, Program.cs
  tests/
    BloodDonationNetwork.UnitTests/
    BloodDonationNetwork.IntegrationTests/
agent-service/                Python FastAPI service
  agents/                     coordinator graph and specialist agents
  shared/internal_client.py   calls to the API's internal endpoints
  tests/
web/                          React web app
mobile/                       Flutter app
testing/
  agent-evaluation/           golden-case evaluation of the agents
  performance/                load and latency test
infra/                        render.yaml, docker-compose.yml, DEPLOYMENT.md
.github/workflows/            CI for each of the four parts
```

## Team

| Student | Name | Owns |
| --- | --- | --- |
| Student 1 | Yusuf S A (IT24103319) | Donor registry, eligibility rules, Eligibility Validation Agent |
| Student 2 | Umair M W M (IT24102527) | Blood requests, Coordinator Agent (LangGraph workflow) |
| Student 3 | Jassim M N M (IT24103137) | Inventory, organizations, Stock-Check Agent, nearby blood banks map |
| Student 4 | Ahamed M A U (IT24101779) | Donation appointments, Matching and Dispatch Agent |

## Running it locally

### Prerequisites

- .NET 8 SDK
- PostgreSQL 16 (installed locally, or with Docker)
- Python 3.12
- Node.js 20 or later
- Flutter (stable channel) for the mobile app

### 1. Database

Use a local PostgreSQL, or start one with Docker:

```bash
docker compose -f infra/docker-compose.yml up -d postgres
```

This creates the database `blood_donation` with user `postgres` and password `postgres` on port 5432. You do not need to create any tables: the API applies all migrations when it starts.

### 2. API

Create `backend/src/BloodDonationNetwork.Api/appsettings.Development.json`. This file is gitignored because it holds secrets, so every developer makes their own:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=blood_donation;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Issuer": "BloodDonationNetwork",
    "Audience": "BloodDonationNetworkUsers",
    "Key": "<a random string of at least 32 characters>"
  },
  "InternalAgentSecret": "<any long random string, also used by the agent service>",
  "AgentService": {
    "BaseUrl": "http://localhost:8001"
  },
  "LocationIq": {
    "ApiKey": "<LocationIQ API key, used to turn addresses into coordinates>"
  }
}
```

Then run:

```bash
cd backend/src/BloodDonationNetwork.Api
dotnet run
```

The API runs on http://localhost:5067, with Swagger at http://localhost:5067/swagger. On first start it creates a development admin account: `admin@blooddonation.local` with password `ChangeMe123!`. This default only applies in Development; deployed environments must set `Seed__AdminEmail` and `Seed__AdminPassword`.

Push notifications are optional. Without Firebase settings the API uses a sender that does nothing, so the in-app notification inbox still works. To enable push locally, add `"Fcm": { "ServiceAccountKeyPath": "firebase-service-account.json" }` and place the Firebase service-account file (never committed) next to `Program.cs`.

To add a database migration (needed only when you change an entity):

```bash
cd backend
BLOOD_DONATION_DB_CONNECTION="Host=localhost;Port=5432;Database=blood_donation;Username=postgres;Password=postgres" \
  dotnet ef migrations add <MigrationName> \
  --project src/BloodDonationNetwork.Infrastructure --startup-project src/BloodDonationNetwork.Api
```

### 3. Agent service

```bash
cd agent-service
python3 -m venv .venv
source .venv/bin/activate          # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env               # set INTERNAL_AGENT_SECRET to the API's InternalAgentSecret
uvicorn main:app --reload --port 8001
```

Check it with http://localhost:8001/health. Without `CHECKPOINT_DATABASE_URL` the agent service keeps paused workflows in memory, which is fine locally; the deployed service stores them in PostgreSQL so they survive restarts.

### 4. Web app

```bash
cd web
npm ci
npm run dev
```

Open http://localhost:5173. It calls the API at http://localhost:5067/api by default; set `VITE_API_BASE_URL` to point it elsewhere.

### 5. Mobile app

```bash
cd mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5067
```

`10.0.2.2` is how the Android emulator reaches your computer's `localhost`. To build an APK against the deployed API:

```bash
flutter build apk --debug --dart-define=API_BASE_URL=https://blood-donation-api-h3cp.onrender.com
```

The APK is written to `mobile/build/app/outputs/flutter-apk/app-debug.apk`.

### Start order

PostgreSQL, then the API, then the agent service, then the web or mobile app. To see the agents work, sign in to the web app as staff or admin, create a blood request and open its Workflow Monitor.

## Testing

None of these suites need the API, the agent service or PostgreSQL to be running. Backend integration tests use EF Core on an in-memory SQLite database, and the agent, mobile and web tests use fake HTTP clients.

| Suite | Run from | Command |
| --- | --- | --- |
| Backend unit and integration (xUnit) | `backend/` | `dotnet test BloodDonationNetwork.sln` |
| Agent service (pytest) | `agent-service/` | `source .venv/bin/activate` then `python -m pytest -v` |
| Mobile (flutter_test) | `mobile/` | `flutter analyze` then `flutter test` |
| Web (Vitest) | `web/` | `npx tsc --noEmit` then `npm test` |

To run part of a suite:

```bash
dotnet test BloodDonationNetwork.sln --filter "FullyQualifiedName~DonorRankingCalculator"   # backend
python -m pytest tests/test_matching_dispatch_agent.py -v                                  # agent service
flutter test test/my_appointments_screen_test.dart                                         # mobile
npx vitest run src/App.test.tsx                                                            # web
```

### Evaluation against the running system

These two scripts need the full stack running locally (PostgreSQL, the API on port 5067, the agent service). See the README in each folder.

- `testing/agent-evaluation/run_golden_cases.py` runs eight golden cases against the real system and checks workflow status, step order, HTTP codes and database rows: the normal path, safe failure, the dispatch approval guard, prompt-injection resistance, revision and its limit of three, and an eligibility rule. Results are written to `agent_evaluation_report.md`.
- `testing/performance/run_load_test.py` measures API and agent latency under concurrent load and writes `performance_report.md`.

## Continuous integration

GitHub Actions runs four workflows on every push and pull request to `development`:

| Workflow | What it runs |
| --- | --- |
| `dotnet-ci.yml` | Restore, build (Release), `dotnet test` for the whole solution |
| `web-ci.yml` | `npm ci`, `tsc --noEmit`, Vitest, production build |
| `agent-service-ci.yml` | Install requirements, check that `main` imports, `pytest` |
| `mobile-ci.yml` | `flutter pub get`, `flutter analyze`, `flutter test` |

The Actions tab also lists `backend-ci.yml`. That was the original template workflow; it was removed on 10 September 2026 and replaced by `dotnet-ci.yml`, and GitHub keeps showing its old runs.

## Deployment

The four Render services (API, agent service, web static site and PostgreSQL) are defined in `infra/render.yaml`. Step-by-step instructions, the required environment variables and the known hosting limits are in [infra/DEPLOYMENT.md](infra/DEPLOYMENT.md).

Main environment variables (names only):

| Service | Variables |
| --- | --- |
| API | `ASPNETCORE_ENVIRONMENT`, `Database__Host`, `Database__Port`, `Database__Name`, `Database__User`, `Database__Password`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key`, `InternalAgentSecret`, `AgentService__BaseUrl`, `Seed__AdminEmail`, `Seed__AdminPassword`, `LocationIq__ApiKey`, `Fcm__ServiceAccountKeyJson` (optional) |
| Agent service | `BACKEND_BASE_URL`, `INTERNAL_AGENT_SECRET`, `CHECKPOINT_DATABASE_URL` |
| Web | `VITE_API_BASE_URL` (fixed when the site is built) |
| Mobile | `API_BASE_URL` (passed with `--dart-define` at build time) |

## Git workflow

- `development` is the integration branch. Each student works on their own branch (`dev/donor-registry`, `dev/blood-requests`, `dev/inventory`, `dev/appointments`), and shared groundwork goes on short-lived `chore/*` branches.
- Changes reach `development` through pull requests with normal merge commits. Pushed branches are never rebased or force-pushed.
- Commit messages follow `type(scope): description`, for example `feat(appointments): ...` or `fix(api): ...`.

## Known limitations

- The free Render services sleep when idle. The first request after a quiet period is slow, and a request created while the agent service is asleep may not start its workflow; use **Start Workflow** on the request's details page to start it.
- The agents use fixed rules only.
- Backend integration tests run on SQLite, so PostgreSQL-specific behaviour is only exercised by the evaluation scripts.
