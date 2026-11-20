# Local services

PostgreSQL 17 (with pgvector and btree_gist) and Redis 7.4 for local development. Both ports bind to `127.0.0.1` only.

`scripts/dev.sh setup` does everything below in one step and also seeds the demo accounts and venues (see `docs/setup/getting-started.md`). The manual steps:

```bash
cd infra/compose
cp .env.example .env        # then change POSTGRES_PASSWORD and DEMO_PASSWORD
docker compose up -d --wait
```

`btree_gist` is enabled for the exclusion constraints that will stop overlapping room bookings (see `docs/domain/capacity-invariants.md`, invariant 2). The extensions are created only when the data volume is first initialised; `docker compose down -v` resets everything.

Apply the database schema from the repository root:

```bash
dotnet tool restore
VENUEFLOW_POSTGRES="Host=127.0.0.1;Port=5432;Database=venueflow;Username=venueflow;Password=<your password>" \
  dotnet ef database update \
    --project services/api/src/VenueFlow.Infrastructure \
    --startup-project services/api/src/VenueFlow.Api
```

The migration tooling reads `VENUEFLOW_POSTGRES` through the design-time factory in `VenueFlow.Infrastructure`, so it never needs the API's settings.

If you change the password in `.env`, also set `ConnectionStrings__Postgres` for the API (or edit `services/api/src/VenueFlow.Api/appsettings.Development.json`).
