#!/usr/bin/env bash
# Local development helper for VenueFlow.
#
#   scripts/dev.sh setup   start PostgreSQL + Redis, apply migrations, seed demo accounts and venues
#   scripts/dev.sh api     run the API on http://127.0.0.1:5080 against the local services
#   scripts/dev.sh web     install web dependencies and run the Vue dev server
#   scripts/dev.sh e2e     run the Playwright tests against the running API (extra args go to Playwright)
#   scripts/dev.sh reset   delete the local database volume (asks first)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="$root/infra/compose/compose.yaml"
env_file="$root/infra/compose/.env"
api_project="$root/services/api/src/VenueFlow.Api"

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: '$1' not found; install $2 first (see docs/setup/getting-started.md)" >&2
    exit 1
  fi
}

random_secret() {
  od -An -N18 -tx1 /dev/urandom | tr -d ' \n'
}

# Creates infra/compose/.env on first run with a random PostgreSQL password and
# demo-account password, then exports the settings the API reads. Older .env
# files without DEMO_PASSWORD get one appended.
load_env() {
  if [[ ! -f "$env_file" ]]; then
    sed -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$(random_secret)/" \
      -e "s/^DEMO_PASSWORD=.*/DEMO_PASSWORD=$(random_secret)/" \
      "$env_file.example" >"$env_file"
    chmod 600 "$env_file"
    echo "Created infra/compose/.env with random PostgreSQL and demo-account passwords."
  elif ! grep -q '^DEMO_PASSWORD=' "$env_file"; then
    echo "DEMO_PASSWORD=$(random_secret)" >>"$env_file"
    echo "Added a random DEMO_PASSWORD to infra/compose/.env."
  fi

  set -a
  # shellcheck source=/dev/null
  source "$env_file"
  set +a

  export ConnectionStrings__Postgres="Host=127.0.0.1;Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__Redis="127.0.0.1:${REDIS_PORT:-6379}"
  export Demo__Password="${DEMO_PASSWORD:-}"
}

compose() {
  docker compose -f "$compose_file" --env-file "$env_file" "$@"
}

cmd_setup() {
  need docker "Docker with the Compose plugin"
  need dotnet "the .NET SDK pinned in global.json"
  load_env
  echo "Starting PostgreSQL and Redis..."
  compose up -d --wait
  echo "Applying migrations and seeding demo accounts and venues..."
  dotnet run --project "$api_project" -- seed
  echo "Done. Demo accounts (guest|host|staff|admin|other-host)@example.test use DEMO_PASSWORD from infra/compose/.env."
  echo "Next: scripts/dev.sh api (and scripts/dev.sh web in a second terminal)."
}

cmd_api() {
  need dotnet "the .NET SDK pinned in global.json"
  load_env
  exec dotnet run --project "$api_project"
}

cmd_web() {
  need pnpm "pnpm (corepack enable)"
  cd "$root"
  pnpm install
  exec pnpm dev:web
}

cmd_e2e() {
  need pnpm "pnpm (corepack enable)"
  need curl "curl"
  load_env
  if ! curl -fsS http://127.0.0.1:5080/health/live >/dev/null 2>&1; then
    echo "error: the API is not answering on http://127.0.0.1:5080; run scripts/dev.sh setup, then scripts/dev.sh api in another terminal" >&2
    exit 1
  fi
  cd "$root"
  pnpm install
  pnpm --filter @venueflow/web exec playwright install chromium
  # Playwright starts the Vue dev server itself unless one is already running.
  E2E_DEMO_PASSWORD="$DEMO_PASSWORD" exec pnpm --filter @venueflow/web e2e "$@"
}

cmd_reset() {
  need docker "Docker with the Compose plugin"
  load_env
  read -r -p "Delete the local VenueFlow database and Redis data? [y/N] " answer
  if [[ "$answer" == [yY] ]]; then
    compose down -v
    echo "Removed. Run scripts/dev.sh setup to start again."
  fi
}

case "${1:-}" in
  setup) cmd_setup ;;
  api) cmd_api ;;
  web) cmd_web ;;
  e2e)
    shift
    cmd_e2e "$@"
    ;;
  reset) cmd_reset ;;
  *)
    sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
