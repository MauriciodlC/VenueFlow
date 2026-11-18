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

1. creates `infra/compose/.env` from `.env.example` with a random PostgreSQL password, if it does not exist yet (the file is git-ignored);
2. starts PostgreSQL and Redis and waits until both are healthy;
3. runs `dotnet run --project services/api/src/VenueFlow.Api -- seed`, which applies pending EF Core migrations, inserts the demo venues and exits.

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

There are no user accounts yet: sign-in arrives with the host listing work in week 2. The plan for demo accounts, following `docs/domain/personas.md`:

- one **guest**, one **host** that owns both demo venues, one **staff** member assigned to Riverside Makers Studio and one **admin**;
- created by the same `seed` command, with fixed ids and `@example.test` addresses that cannot receive mail;
- passwords are never committed; they will come from the local `.env`.

## Tests

```bash
cd services/api && dotnet test VenueFlow.sln          # domain, seed, schema and API hosting tests
pnpm install && pnpm test                             # Vue component tests
```

The schema tests (`tests/VenueFlow.Tests/Persistence/SchemaIntegrityTests.cs`) check, without a database, that the EF Core model and the migration snapshot keep the table names, column limits, the unique room-name index, the capacity check constraint and the venue → room cascade. CI also runs `dotnet ef migrations has-pending-model-changes` so a model change cannot ship without its migration.

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
