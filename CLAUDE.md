# FundManagement Circle Integration POC

Angular 22 + .NET 10 + PostgreSQL + Dapper. Clean Architecture + CQRS. Circle sandbox integration.

---

## Quick Start

```bash
# Backend
dotnet restore && dotnet build
dotnet run --project src/FundManagement.Api          # runs migrations on startup

# Frontend
cd frontend && npm install
ng serve                                   # http://localhost:4200

# Database (Docker)
docker run -d --name ifs-pg \
  -e POSTGRES_PASSWORD=localdev \
  -e POSTGRES_DB=ifs_poc \
  -p 5432:5432 postgres:16

# E2E Tests
npx playwright install                     # first time
npx playwright test
npx playwright codegen http://localhost:4200
```

---

## Stack

| Layer | Tech |
|-------|------|
| Frontend | Angular 22 — standalone, signals, signal stores, `@if/@for/@switch` |
| Backend | .NET 10 — ASP.NET Core Minimal APIs, MediatR CQRS |
| ORM | Dapper — raw SQL, `NpgsqlConnection` via DI |
| Database | PostgreSQL 16, DbUp migrations |
| Integration | Circle APIs (sandbox) — Circle MCP + Skills |
| Docs | context7 MCP — Angular/Dapper/.NET live docs |
| E2E | Playwright CLI |

---

## Project Structure

```
test-project1/
  src/
    FundManagement.Domain/          entities, value objects
    FundManagement.Application/     commands, queries, handlers, interfaces
    FundManagement.Infrastructure/  Dapper repos, Circle HTTP client
    FundManagement.Api/             Minimal API endpoints, DI root
  frontend/              Angular 22
  migrations/            DbUp SQL scripts
  e2e/                   Playwright specs
  .claude/docs/          Reference docs (load on demand)
```

---

## Core Rules

1. **Balances via ledger only** — never `account.Balance += amount`
2. **Ledger is append-only** — no updates/deletes; corrections = reversal entries
3. **Idempotency everywhere** — check CircleEventId/PaymentIntentId/PayoutId before processing
4. **No EF Core** — Dapper + raw SQL only
5. **No business logic in Api layer** — dispatch to MediatR only
6. **Dependencies point inward** — Infrastructure never referenced by Domain/Application
7. **Never commit secrets** — use `dotnet user-secrets` / env vars

---

## Behavior (Karpathy Rules)

**Think before coding.** State assumptions explicitly. If uncertain, ask. Surface tradeoffs — don't pick silently.

**Simplicity first.** Min code. No speculative features, no abstractions for single-use, no error handling for impossible scenarios.

**Surgical changes.** Touch only what you must. Don't "improve" adjacent code. Match existing style. Every changed line must trace to request.

**Goal-driven execution.** Define verifiable success criteria before implementing. Multi-step tasks: brief plan + verify steps.

---

## Reference Docs (load when needed)

- @.claude/docs/architecture.md — Clean Architecture layers, CQRS structure, MediatR + Dapper DI wiring
- @.claude/docs/domain.md — Entity shapes, source-of-truth table
- @.claude/docs/circle-apis.md — Circle API endpoints, MCP + Skills usage rules
- @.claude/docs/scenarios.md — Business flows, required screens
- @.claude/docs/conventions.md — Balance rules, idempotency, webhook security, env vars, logging