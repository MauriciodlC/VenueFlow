# ADR 0001: Vue 3 frontend and ASP.NET Core modular monolith

- **Status:** Accepted
- **Date:** 2026-11-18

## Context

VenueFlow needs a guest booking UI, a host console and an admin area, plus a backend whose main job is to never oversell capacity. The team is small, the MVP has one deployable API, and AI features (intent parsing, policy Q&A) must stay outside the booking transaction.

## Decision

- **Monorepo** with `apps/web` (frontend), `services/api` (backend), `packages/` (shared client, tokens, AI evaluation data), `infra/` and `docs/`.
- **Frontend:** Vue 3 with TypeScript, Vite, Composition API (`<script setup lang="ts">`), Vue Router for guest/host/admin areas and Pinia for session and search state. ESLint and `vue-tsc` run in CI.
- **Backend:** one ASP.NET Core API organised as a modular monolith (`Api`, `Application`, `Domain`, `Infrastructure`, `Tests` projects). EF Core with PostgreSQL migrations.
- **Data:** PostgreSQL is the only source of truth for availability, holds, bookings and prices. pgvector (same database) stores policy document embeddings later. Redis is used only as an advisory cache and must never decide availability.
- **AI:** accessed through `Microsoft.Extensions.AI` behind an adapter. Model calls happen before search or after booking, never while a database lock is held. Outputs are validated against a schema and never create inventory.
- **Async work:** a transactional outbox in PostgreSQL, relayed by a hosted background worker (hold expiry, notifications, waitlist offers).
- **Payments:** a sandbox adapter only; no card data is collected.

## Consequences

- One API to deploy and reason about; module boundaries keep later extraction (search, notifications) possible.
- Concurrency rules live in the database (locks, constraints), so they can be tested with real PostgreSQL in integration tests.
- Two toolchains (Node and .NET) in CI and local setup; Docker Compose provides PostgreSQL and Redis.
- Redis being advisory means every cached search result is re-verified on hold.

## Alternatives considered

- **Microservices from day one:** rejected; adds distributed transactions to the exact part (holds) that must be simplest.
- **Separate vector database:** rejected for the MVP; pgvector keeps tenant/venue filtering in the same SQL as the rest of the data.
- **Client-side availability checks:** rejected; the server is the only place capacity can be enforced.
