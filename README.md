# fx-remittance-api

A small but realistic backend for an international money transfer service, written in C# / ASP.NET Core (.NET 10).

It covers the part of a remittance system I find people most often get subtly wrong: pricing a transfer, locking that price for the customer, and making sure a retried request never sends money twice.

I work on exchange and remittance systems day to day, so the rules here (three-decimal dinars, quote expiry, refusing to price on stale rates, one quote = one transfer) come from real requirements rather than a tutorial. All data and corridors in this repo are made up.

## What it does

- **Corridors** – each send/receive currency pair has its own margin, min/max limits and tiered fee schedule, loaded from `appsettings.json`.
- **Quotes** – price a transfer either by *send amount* ("I want to send 250 KWD") or by *receive amount* ("my family must get exactly 100,000 INR"). Quotes expire after 10 minutes by default.
- **Correct rounding per currency** – KWD, BHD and OMR use 3 decimals, JPY uses none, everything else 2. In receive mode the send side is rounded *up*, so the recipient never ends up a few paise short.
- **Stale rate protection** – if the latest rate for a corridor is older than the configured limit, the API refuses to quote instead of quietly using old data.
- **Transfers with a proper state machine** – `Created → Funded → SentToPartner → Paid`, with `Cancelled` / `Failed` branches. Illegal jumps (e.g. Created → Paid) are rejected and every change is kept in the transfer history.
- **Idempotency keys** – `POST /transfers` requires an `Idempotency-Key` header. Same key + same body returns the original response; same key + different body is rejected. Mobile apps on bad networks retry a lot, and this is what stops duplicate transfers.
- **Problem Details errors** – every business error comes back as RFC 7807 JSON with a stable `code` field (`rate_stale`, `quote_expired`, `amount_above_limit`, ...), so clients can branch on it without parsing text.
- **Two storage options** – in-memory (default, zero setup) or PostgreSQL via Npgsql, switched by a connection string.

## Project layout

```
src/
  Remittance.Core                    domain: money, rates, fee schedules, quotes, transfers (no dependencies)
  Remittance.Infrastructure          in-memory stores, corridor configuration
  Remittance.Infrastructure.Postgres Npgsql stores (plain SQL, no ORM)
  Remittance.Api                     minimal API endpoints, error handling, OpenAPI
tests/
  Remittance.Core.Tests              unit tests for pricing, rounding and transfer rules
  Remittance.Api.Tests               end-to-end tests through the HTTP pipeline
db/
  001_init.sql                       Postgres schema
```

The domain project has no package references at all. Pricing and transfer rules are plain C# and easy to test, and storage or transport can change without touching them.

## Running it

**Quickest (in-memory):**

```bash
dotnet run --project src/Remittance.Api
```

The API listens on `http://localhost:5080`. The OpenAPI document is at `/openapi/v1.json`, and `requests.http` has ready-made calls for VS Code / Rider.

**With PostgreSQL:**

```bash
docker compose up --build
```

This starts Postgres, applies `db/001_init.sql` and runs the API on `http://localhost:8080`.

**Tests:**

```bash
dotnet test FxRemittance.slnx
```

## Example

```http
POST /quotes
{ "source": "KWD", "target": "INR", "amount": 250, "mode": "Send" }
```

```json
{
  "id": "0844ad98-2db7-4734-8639-ef7c6b27cf53",
  "corridor": "KWD-INR",
  "sendAmount":    { "amount": 250,      "currency": "KWD" },
  "fee":           { "amount": 1.500,    "currency": "KWD" },
  "totalToPay":    { "amount": 251.500,  "currency": "KWD" },
  "customerRate": 283.6876,
  "receiveAmount": { "amount": 70921.90, "currency": "INR" },
  "expiresAt": "2026-10-04T01:46:05Z"
}
```

Then create the transfer with the quote id and an `Idempotency-Key` header. Back-office endpoints (rate updates, status changes) need an `X-Api-Key` header. It's a simple shared secret to keep the demo self-contained. A real deployment would put them behind proper identity.

## Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/corridors` | supported pairs, limits, fee tiers |
| GET | `/rates/` | latest rate per pair |
| PUT | `/rates/{from}/{to}` | treasury rate update (API key) |
| POST | `/quotes` | price a transfer |
| GET | `/quotes/{id}` | |
| POST | `/transfers` | requires `Idempotency-Key` |
| GET | `/transfers/{id}` | includes status history |
| POST | `/transfers/{id}/status` | move a transfer forward (API key) |
| GET | `/health` | |

## Things I'd add next in a production system

- Outbox table + background worker to push funded transfers to the payout partner
- AML / sanctions screening hook before `Funded`
- Per-customer daily and monthly limits
- Rate feed adapter (treasury system or a market data provider) instead of manual `PUT`
- Proper auth (OAuth2 / API keys per partner) and audit log

## License

MIT
