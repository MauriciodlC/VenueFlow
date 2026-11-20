# Getting started

Everything runs locally against synthetic data. Nothing here needs real customer details, cards or API keys.

## Prerequisites

| Tool | Version | Used for |
|---|---|---|
| Docker with the Compose plugin | recent | PostgreSQL 17 (pgvector, btree_gist) and Redis 7.4 |
| .NET SDK | as pinned in `global.json` (9.0) | API, migrations, seed, xUnit tests |
| Node.js + pnpm | Node from `.nvmrc`, pnpm via `corepack enable` | Vue web app |

## One-command setup

```bash
scripts/dev.sh setup
```

This:

1. creates `infra/compose/.env` from `.env.example` with random PostgreSQL and demo-account passwords, if it does not exist yet (the file is git-ignored);
2. starts PostgreSQL and Redis and waits until both are healthy;
3. runs `dotnet run --project services/api/src/VenueFlow.Api -- seed`, which applies pending EF Core migrations, inserts the demo accounts and venues and exits.

Running it again is safe: containers that are already up stay up, applied migrations are skipped and existing demo venues are left alone.

Then, in two terminals:

```bash
scripts/dev.sh api   # http://127.0.0.1:5080 — /health/live, /health/ready, /api/info, /openapi/v1.json
scripts/dev.sh web   # http://127.0.0.1:5173 — proxies /api and /health to the API
```

`scripts/dev.sh api` exports `ConnectionStrings__Postgres` and `ConnectionStrings__Redis` from `infra/compose/.env`, so the API always uses the same password as the containers.

To start from scratch, run `scripts/dev.sh reset` (asks before deleting the database volume), then `scripts/dev.sh setup` again.

## Demo data

### Venues

The seed lives in `services/api/src/VenueFlow.Infrastructure/Seeding/DemoVenues.cs`. It builds the venues through the domain factories (`Venue.Create`, `Venue.AddRoom`), so seeded rows obey the same rules as host input: IANA timezone ids only, capacity 1–1000, unique room names per venue.

| Venue | Timezone | Rooms (capacity) |
|---|---|---|
| Riverside Makers Studio | `Europe/Madrid` (observes daylight-saving time) | Workshop Hall (24), Quiet Room (8) |
| Harbor Coworking Loft | `America/Bogota` (UTC−05:00 all year) | Main Loft (40), Meeting Pod (6) |

The two zones are chosen on purpose: one changes offset twice a year and one never does, so schedule and DST work can be demonstrated on real seed rows.

Ids are fixed GUIDs, which makes the seed idempotent and lets tests and demo scripts refer to the same rows. The seeder only inserts venues whose id is missing; it never updates or deletes. When the seed changes, reset the database.

### Accounts

`seed` also creates one account per role from `DemoAccounts.cs`, with fixed ids and `@example.test` addresses that cannot receive mail:

| Email | Role | Notes |
|---|---|---|
| `guest@example.test` | guest | |
| `host@example.test` | host | owns both demo venues |
| `staff@example.test` | staff | |
| `admin@example.test` | admin | |
| `other-host@example.test` | host | owns nothing; used to check one host cannot see another's venues |

All of them share one password, `DEMO_PASSWORD` in `infra/compose/.env`. `scripts/dev.sh` generates a random one the first time (and appends one to an older `.env` that lacks it); it is passed to the API as `Demo__Password` and only used when an account is created. Passwords are hashed with ASP.NET Core Identity's PBKDF2 hasher; the plain value is never stored or committed. Existing accounts are never changed by the seed, so after changing `DEMO_PASSWORD` run `scripts/dev.sh reset` and `setup` again.

The demo venues are created as drafts, completed, submitted and approved through the domain model, so they start **Published**.

### Upgrading a week-1 database

Migration `HostAccountsAndVenueDrafts` gives every venue an owning host. Week-1 databases only contain the ownerless demo venues, so the migration deletes them; `scripts/dev.sh setup` then recreates them with their owner.

## Signing in

Open http://127.0.0.1:5173/sign-in, pick a demo account (fills in the email) and enter `DEMO_PASSWORD`. The API keeps the session in an HttpOnly, `SameSite=Strict` cookie named `venueflow.session` (8 hours, sliding); the web app never sees the cookie value and asks `GET /api/auth/me` who is signed in.

| Endpoint | Who | What |
|---|---|---|
| `POST /api/auth/login` | anyone | email + password → session cookie; wrong email and wrong password get the same `401 invalid_credentials` |
| `POST /api/auth/logout` | anyone | ends the session |
| `GET /api/auth/me` | signed in | the current user |
| `GET /api/host/venues` | host | the caller's venues, any status |
| `POST /api/host/venues` | host | new draft from a name and IANA timezone → `201` + `ETag` |
| `GET /api/host/venues/{id}` | owner | the draft + `ETag` |
| `PUT /api/host/venues/{id}` | owner | replace details and rooms; needs `If-Match` |
| `GET /api/host/venues/{id}/preview` | owner | the guest view (no street address) + fields still missing |
| `POST /api/host/venues/{id}/submit` | owner | Draft → InReview; needs `If-Match` |

Another host's venue answers `404`, the same as an unknown id. Writes without `If-Match` get `428`; a stale ETag gets `412 revision_mismatch`, so two editors cannot silently overwrite each other. Errors are Problem Details with a stable `code` (e.g. `venue_incomplete`, `venue_not_editable`).

## Tests

```bash
cd services/api && dotnet test VenueFlow.sln          # domain, seed, schema, auth and host-venue API tests
pnpm install && pnpm test                             # Vue component and unit tests
```

The schema tests (`tests/VenueFlow.Tests/Persistence/SchemaIntegrityTests.cs`) check, without a database, that the EF Core model and the migration snapshot keep the table names, column limits, the unique room-name and email indexes, the capacity, role and status check constraints, the venue → room cascade, the venue → host foreign key and the revision concurrency token. The auth and host-venue API tests run the real endpoints against an EF Core in-memory database holding the demo seed (`tests/VenueFlow.Tests/Api/SeededApiFactory.cs`); database constraints are not enforced there. CI also runs `dotnet ef migrations has-pending-model-changes` so a model change cannot ship without its migration.

## Manual steps

Running migrations without the seed, e.g. against another database:

```bash
dotnet tool restore
VENUEFLOW_POSTGRES="Host=127.0.0.1;Port=5432;Database=venueflow;Username=venueflow;Password=<password>" \
  dotnet ef database update \
    --project services/api/src/VenueFlow.Infrastructure \
    --startup-project services/api/src/VenueFlow.Api
```

See `infra/compose/README.md` for details on the local services.
