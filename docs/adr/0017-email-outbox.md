# ADR 0017: Email through a transactional outbox on the notification row

- Status: Accepted
- Date: 2026-11-11

## Context

Assignments send an email (PRD 13). The developer requires that an assignment
succeeds even when the mail server is down, and that failed sends are retried.
The API runs as several instances (PRD 7).

| Option | Survives restart | Email only for saved changes | New infrastructure | Rejected because |
| --- | --- | --- | --- | --- |
| A. Send inside the command | - | Yes | None | A mail outage fails the assignment |
| B. In-memory queue (`Channel<T>`) and a worker | No | Only if enqueued after commit | None | Queued emails are lost on restart or crash |
| C. Message broker (RabbitMQ, Azure Service Bus) | Yes | Needs an outbox anyway | Broker, package, container | Too much for five email types |
| D. Outbox in SQL Server, saved in the same transaction, sent by a background worker | Yes | Yes | None | **Chosen** |

## Decision

- The email delivery state lives on the `Notifications` row (status, attempts,
  next attempt, claim expiry, sent time, last error). The notification and the
  task or project change are saved in one `SaveChangesAsync`.
- A `BackgroundService` on every instance claims due rows with one conditional
  update (claim expiry), sends them with MailKit through `IEmailSender`, and marks
  them sent, or retries with exponential backoff up to a maximum, then marks them
  failed and logs an error.
- MailKit 5.0.0 (MIT, verified on NuGet 2026-11-11). Development uses Mailpit
  (`axllent/mailpit:v1.33.1`, MIT, verified on Docker Hub 2026-11-11) in Docker
  Compose. A real SMTP provider is configuration only.
- `Email:UseTls=false` only in Development and Testing (like ADR 0011).

## Consequences

- An email can be sent twice only if a worker sends it and crashes before saving
  "sent" (at-least-once). Accepted: a duplicate assignment email is harmless.
- Emails are delayed by up to the poll interval (default 10 seconds).
- Rows are deleted after the 7-day retention, pending emails included.
- One new package (MailKit) and one new development container (Mailpit).
