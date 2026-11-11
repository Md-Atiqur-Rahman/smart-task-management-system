# ADR 0018: Live events over SSE, fanned out through Redis pub/sub

- Status: Accepted
- Date: 2026-11-11
- Amends: ADR 0013 (scope of the Redis exception)

## Context

Users need live pop-ups and a real-time dashboard (PRD 13). The API runs as
several instances behind a reverse proxy (PRD 7), so the instance that saves a
change is often not the instance that holds the recipient's open connection.

Transport options: Server-Sent Events (one-way, plain HTTP, built into ASP.NET
Core 10 as `TypedResults.ServerSentEvents`), SignalR (two-way; needs its own
backplane package for several instances), polling (not live). The developer
chose SSE: the server only pushes, and the client already uses HTTP for every
action.

Fan-out options: a database poll by every instance (delay, load), a message
broker (new infrastructure), Redis pub/sub (Redis is already required, ADR 0013;
StackExchange.Redis already referenced).

## Decision

- One SSE endpoint per user session, `GET /api/notifications/stream`, behind the
  normal authentication pipeline. The client reads it with `fetch` and the bearer
  header; the token is never placed in the URL.
- The stream ends when the access token expires and when the token version
  check fails at a heartbeat; the client reconnects with a new token.
- Every instance subscribes to one Redis channel and keeps an in-memory registry
  of its own connections. Live events are published to that channel only after
  the database commit.
- **ADR 0013 amendment:** the Redis exception now also covers this pub/sub
  channel. It carries no state: nothing is stored, and a missed message is never
  replayed.
- **Redis down: fail open** for live events. Commands still succeed; stored
  notifications and the email outbox are unaffected; clients catch up on their
  next fetch.

## Consequences

- Live events are hints: at-most-once, unordered across instances; the client
  re-fetches the list, the badge or the dashboard.
- Long-lived connections hold one request slot each; the reverse proxy must not
  buffer `text/event-stream` (README note).
- No new package.
