# Design: Notifications

- Status: Implemented (2026-11-11). Approved by the developer (2026-11-11),
  including every **[builder's call]** (decisions not stated by the developer).
  Where the build differed from the first draft, the text below describes what
  was built.
- Date: 2026-11-11
- Requirements: `docs/PRD.md` section 13 (and sections 2, 3, 6, 7, 9). Builds on
  the Project module (`docs/project-design.md`), the Task module
  (`docs/task-design.md`) and the Dashboard (`docs/dashboard-design.md`).
- Decisions: ADR 0017 (email outbox), ADR 0018 (SSE and Redis pub/sub).

## 1. Intent

People learn about work given to them (or taken away from them) at once,
without refreshing: a toast in the open app, a stored notification list behind
a bell icon, and an email. Open dashboards update themselves when the numbers
they show change.

Goals:

- Five notification events (section 3), each stored, pushed live and emailed.
- Real-time dashboard: a "dashboard changed" signal, the client re-fetches the
  existing dashboard endpoints (their API does not change).
- Works with several API instances behind a reverse proxy (PRD 7).
- An assignment never fails because the mail server is down; no email is lost or
  sent for a change that was rolled back.

Non-goals: the Angular UI (built later; this module ships the API only),
notification preferences or muting, notifications for status changes, edits,
deletions or comments, digests, push to mobile devices, read receipts for
email, templates in a template engine, a message broker.

Anti-goals: a live event or email for a change that was not saved; an email
sent twice because two instances picked it up; a notification visible to anyone
but its recipient; a live connection that keeps delivering events after the
user logged out or their role changed; the token in a URL.

## 2. Vocabulary

- **Notification:** one stored message for one recipient (section 4).
- **Live event:** a message pushed over the user's open SSE connection
  (section 6). Event types: `notification`, `notifications-read`,
  `dashboard-changed` and `heartbeat`.
- **Actor:** the signed-in user whose command caused the event.

## 3. Events and recipients

| # | Trigger (command) | Recipient | Type | Links to |
| --- | --- | --- | --- | --- |
| 1 | Task gets an assignee (`CreateTask` with an assignee, `AssignTask`) | New assignee | `TaskAssigned` | the task |
| 2 | Task loses or changes its assignee (`AssignTask` with another user or none) | Previous assignee | `TaskUnassigned` | nothing |
| 3 | Admin creates a project for a Project Manager (`CreateProject`) | The owner | `ProjectOwnerAssigned` | the project |
| 4 | Project owner changes (`ChangeProjectOwner`) | New owner | `ProjectOwnerAssigned` | the project |
| 5 | Project owner changes (`ChangeProjectOwner`) | Previous owner | `ProjectOwnerRemoved` | nothing |

Rules:

- **The actor is never notified about their own action** (a PM assigning a task
  to themselves, a PM creating their own project) **[builder's call]**.
- A command that changes nothing (`Assign` / `ChangeOwner` already return
  `false` for "same value") creates nothing.
- Every notification also queues one email **[builder's call]**.
- "Removed" types carry the task or project name but no link, because the
  recipient may no longer see it. The message says "removed from you", never "you
  no longer have access" (a previous owner who is assigned to a task in the
  project still sees it, PRD 3).
- Task deletion, project deletion, status changes and edits create no
  notification (non-goal); they only send `dashboard-changed` (section 6).

## 4. The Notification aggregate (Domain)

`Notification` (aggregate root, `Domain/Notifications/`), created only through
`Notification.Create(...)` with guard clauses:

| Field | Meaning |
| --- | --- |
| `Id` | `Guid` (version 7, like the other aggregates) |
| `RecipientId` | user id |
| `Type` | `NotificationType` enum (section 3); enums are stored 0-based |
| `TaskId?`, `ProjectId?` | link target; null for the "removed" types' link, but the name is kept |
| `Subject` | short text, for example the task title or project name, captured at creation (max length = the source field's max) |
| `ActorName` | who did it, captured at creation |
| `CreatedAt` | from `TimeProvider` |
| `ReadAt?` | null = unread |
| Email delivery state | `EmailStatus` (`Pending`, `Sent`, `Failed`), `EmailAttempts`, `EmailNextAttemptAt`, `EmailClaimedUntil?`, `EmailSentAt?`, `EmailLastError?` (truncated); a shadow column `EmailClaimToken` (not on the aggregate) marks which claimer owns the row |

- `MarkRead(DateTimeOffset at)`: sets `ReadAt` once; idempotent.
- Email state changes go through methods used only by the outbox worker
  (`EmailSent`, `EmailFailed(error, now, maxAttempts)`; `EmailFailed` computes
  the backoff). There is no `ClaimEmail` method: the claim is a set-based
  conditional update in Infrastructure (section 8).
- No soft delete and no `RowVersion`: notifications are deleted for real after
  the retention period, and only the recipient (`ReadAt`) and the worker (email
  fields) write them, on different columns **[builder's call]**.
- No foreign keys from `RecipientId`, `TaskId` or `ProjectId`: the table is
  log-style (rows are deleted after 7 days) and users are only soft-deleted.
- **One table is the outbox** (ADR 0017): the email state lives on the
  notification row, so the notification and its email are saved in the same
  `SaveChangesAsync` as the task or project change **[builder's call]**.

Write repository `INotificationRepository` (Domain): `Add`, `GetByIdAsync`.
Messages (English, the API's language) are rendered from `Type`, `Subject` and
`ActorName` in one place in Application, used by both the list DTO and the email
**[builder's call]**:

- `TaskAssigned`: "{Actor} assigned you the task "{Subject}"."
- `TaskUnassigned`: "{Actor} removed the task "{Subject}" from you."
- `ProjectOwnerAssigned`: "{Actor} made you the owner of the project "{Subject}"."
- `ProjectOwnerRemoved`: "{Actor} removed your ownership of the project "{Subject}"."

## 5. Creating notifications (Application)

No domain-events library (forbidden, `packages.md`). The four command handlers
(`CreateTask`, `AssignTask`, `CreateProject`, `ChangeProjectOwner`) call one
Application service explicitly before `SaveChangesAsync`:

- `IAssignmentNotifier` (Application) with methods per change, for example
  `TaskAssigneeChanged(task, previousAssigneeId, actorId)` and
  `ProjectOwnerChanged(project, previousOwnerId, actorId)`. It decides the
  recipients (section 3), loads the actor's name, and adds the `Notification`
  rows through `INotificationRepository`. It also records the live events to
  publish after the commit (section 6) **[builder's call]**.
- Live events are collected in a scoped `LiveEventBuffer`. After a successful
  `SaveChangesAsync`, `ILiveEventPublisher` flushes the buffer, with
  `CancellationToken.None` so a cancelled request cannot drop events. A failed
  save publishes nothing.
- The other task and project commands (`UpdateTask`, `ChangeTaskStatus`,
  `DeleteTask`, `DeleteProject`) record only `dashboard-changed` live events.

Domain stays unaware of notifications; `TaskItem.Assign` and
`Project.ChangeOwner` keep their signatures. The handler reads the previous
assignee or owner before calling them.

## 6. Live events: SSE and Redis pub/sub (ADR 0018)

### Endpoint

`GET /api/notifications/stream`, `[Authorize]`, `text/event-stream`, built with
ASP.NET Core 10's `TypedResults.ServerSentEvents` (verified in the ASP.NET Core 10
release notes: supported in controller-based apps; `SseItem<T>` carries event
type, id and data).

Events:

| Event type | Data | Client reaction |
| --- | --- | --- |
| `notification` | the `NotificationDto` (section 7) | toast, add to list, badge + 1 |
| `notifications-read` | none | re-fetch unread count (another tab read something) |
| `dashboard-changed` | none | re-fetch `/api/dashboard/summary` and `/upcoming` |

| `heartbeat` | empty | none (the client ignores it) |

The keep-alive is the `heartbeat` event, because `SseItem` cannot write comment
lines. It is sent when the stream opens and then every
`Notifications:HeartbeatSeconds` (default 25), so proxies keep the connection
open **[builder's call]**.

### Authentication of the stream

- The browser's `EventSource` cannot send an `Authorization` header. The
  Angular client reads the stream with `fetch` and the bearer header, like every
  other call. **The token is never put in the URL** (URLs end up in logs)
  **[builder's call]**.
- The normal pipeline authenticates the request (JWT, token-version check,
  security context).
- The server ends the stream **when the access token expires** (`exp` claim) and
  re-checks the token version on every heartbeat. A role change (token version
  bump) ends the stream at the next heartbeat, and so does an unavailable token
  store (fail closed, like the middleware). Logout does **not** end the stream at
  once: it only revokes the refresh token and does not change the token version
  (PRD 5), so the stream ends at the access token's expiry (at most
  `Jwt:AccessTokenMinutes`). The Angular client closes its own stream on logout.
  The client reconnects with a fresh token. So no stream outlives the token it
  was opened with **[builder's call]**.
- Rate limiting counts the connect request like any request.

### Who gets `dashboard-changed`

The users whose dashboard numbers can change: the task's previous and new
assignee, the project's current (and, on an owner change, previous) owner, and
every Admin. On a project deletion this includes the assignees of the project's
non-deleted tasks (one query for the distinct assignee ids), because their
counts drop too **[builder's call]**. The actor gets it too, so their other tabs
update.

| Command | Signal |
| --- | --- |
| Task create, update, status change, assign or unassign, delete | yes |
| Project create, owner change, delete | yes |
| Project name or description update | no (no number changes) |

### Fan-out across instances

- Each instance keeps an in-memory registry of its open streams: user id ->
  connections (one per tab), plus the user's role for "every Admin".
- `ILiveEventPublisher` (after a successful save) publishes one message per event to one Redis
  channel (`RedisChannel.Literal`, prefix from configuration). Every instance
  subscribes once at startup and writes the event to its matching local
  connections. API verified in the installed StackExchange.Redis 3.4.31 XML docs
  (`IConnectionMultiplexer.GetSubscriber`, `ISubscriber.PublishAsync`,
  `ISubscriber.SubscribeAsync`, `RedisChannel.Literal`). No new package.
- Message: JSON with `userIds`, `includeAdmins`, `eventType`, `data`
  **[builder's call]**.
- **Redis down: fail open.** Publishing catches the specific Redis connection
  exceptions, logs a warning and continues; the command has already succeeded,
  the notification is stored and the email is queued; the user sees it on the
  next list fetch. Redis pub/sub delivers only to connected subscribers, so a
  missed live event is never replayed **[builder's call]**. (Authenticated
  requests already fail closed with 503 while Redis is down, PRD 6.)
- Ordering or exactly-once delivery of live events is not guaranteed; the client
  treats them as hints and re-fetches.

## 7. Notification endpoints

All `[Authorize]`, only the caller's own notifications, inside the retention
window. `NotificationsController` in `src/Api/Controllers/Notifications/`.

| Endpoint | Returns |
| --- | --- |
| `GET /api/notifications` | `IReadOnlyList<NotificationDto>`, newest first, at most `Notifications:ListLimit` (default 50, range 1-200) **[builder's call]** |
| `GET /api/notifications/unread-count` | `UnreadCountDto(int Count)` for the badge |
| `POST /api/notifications/{id}/read` | 204; 404 `notifications.not_found` when it is not the caller's |
| `POST /api/notifications/read-all` | 204 |
| `GET /api/notifications/stream` | SSE (section 6) |

`NotificationDto(Guid Id, NotificationType Type, string Message, Guid? TaskId,
Guid? ProjectId, DateTimeOffset CreatedAt, bool IsRead)`. Reading publishes
`notifications-read` to the caller's own connections. 401 and 503 like the other
endpoints. Email state is never exposed.

## 8. Email: outbox worker (ADR 0017)

Code folders: `Infrastructure/Mail` and `Infrastructure/Configuration/Mail`.

- `IEmailSender` (Application): `SendAsync(EmailMessage, CancellationToken)`.
  Infrastructure implementation with **MailKit 5.0.0** (verified on NuGet
  2026-11-11: latest stable, MIT, supports net8.0 and later; depends on
  MimeKit.Core and System.Formats.Asn1). Microsoft recommends MailKit instead of
  `SmtpClient` for new code (verified on learn.microsoft.com). MailKit 5.0 has
  breaking changes against 4.x; the builder follows the 5.0 API, not 4.x samples.
- `EmailOutboxWorker` (`BackgroundService`, Infrastructure), on every instance:
  1. Every `Email:PollSeconds` (default 10), **claim** up to `Email:BatchSize`
     (default 20) pending notifications whose `EmailNextAttemptAt <= now` and
     whose claim is empty or expired, by one conditional database update that
     sets `EmailClaimedUntil = now + Email:ClaimSeconds` (default 120). Only rows
     this update changed are sent, so two instances never send the same email.
     The update and its race are covered by an integration test with two
     concurrent claimers **[builder's call]**.
  2. For each claimed row: load the recipient's current email and name, render
     the message (section 4), send.
  3. Success: `EmailStatus = Sent`. Failure (specific SMTP, socket and
     authentication exceptions): `EmailAttempts + 1`, next attempt after
     `2^attempts` minutes; after `Email:MaxAttempts` (default 5) `Failed` and an
     error log. Any unexpected per-row exception also counts as a failed
     attempt, so one bad row never blocks the others. A missing recipient counts
     as a failed attempt. A row whose claim expired mid-batch is skipped until
     the next cycle.
- Email content: subject `"{Email:FromName}: {message}"` (no project name in
  configuration), plain-text
  body with the message and, when there is a link, `Email:AppBaseUrl` +
  `Email:TaskPathTemplate` (default `/tasks/{id}`) or `Email:ProjectPathTemplate`
  (default `/projects/{id}`). The Angular app does not exist yet; the templates
  are settings so its routes can follow later without a code change
  **[builder's call]**.
- Recipient email or name is never logged in full (log the notification id).
- Emails are sent only after the outbox row is committed, so a rolled-back
  change never sends an email.

### Configuration (`EmailOptions`, section `Email`, `ValidateOnStart`)

`Host`, `Port`, `UseTls` (STARTTLS), `UserName?`, `Password?` (secret: user-secrets
or environment only), `FromAddress`, `FromName`, `AppBaseUrl`,
`TaskPathTemplate`, `ProjectPathTemplate` (must contain `{id}`), `PollSeconds`,
`BatchSize`, `ClaimSeconds`, `MaxAttempts`.

- `UseTls = false` is allowed **only in Development and Testing** (the same rule
  as `Encrypt=false`, ADR 0011); TLS certificate validation is never disabled
  **[builder's call]**.
- Development: Mailpit `axllent/mailpit:v1.33.1` (verified on Docker Hub
  2026-11-11, MIT; SMTP 1025, web UI 8025), pinned once in `.env.example`
  (`MAILPIT_IMAGE`), a `mailpit` service in `docker-compose.yml` bound to
  `127.0.0.1`, `Email__Host=mailpit` for the api container. For `dotnet run`,
  `appsettings.Development.json` points at `localhost:1025`.
- A real provider later: change `Host`, `Port`, `UseTls`, `UserName`,
  `Password` through environment variables; no code change.

## 9. Retention

- `NotificationCleanupWorker` (`BackgroundService`, Infrastructure), every
  `Notifications:CleanupIntervalMinutes` (default 60), deletes notifications with
  `CreatedAt < now - Notifications:RetentionDays` (default 7, range 1-90) with one
  set-based delete. Running on several instances at once is harmless.
- Pending or failed emails older than the retention are deleted with their row.
- List and unread count also filter by the retention window, so a row that is due
  for deletion is never shown.

## 10. Options summary

| Section:Key | Default | Range |
| --- | --- | --- |
| `Notifications:RetentionDays` | 7 | 1-90 |
| `Notifications:ListLimit` | 50 | 1-200 |
| `Notifications:HeartbeatSeconds` | 25 | 5-120 |
| `Notifications:CleanupIntervalMinutes` | 60 | 1-1440 |
| `Notifications:RedisChannel` | `notifications` | required |
| `Email:PollSeconds` | 10 | 1-300 |
| `Email:BatchSize` | 20 | 1-100 |
| `Email:ClaimSeconds` | 120 | 30-900 |
| `Email:MaxAttempts` | 5 | 1-20 |

All with `ValidateDataAnnotations` + `ValidateOnStart`; non-secret defaults in
`src/Api/appsettings.json`.

## 11. Persistence

- New table `Notifications` with a migration; indexes on
  `(RecipientId, CreatedAt)` (list, unread count) and on
  `(EmailStatus, EmailNextAttemptAt)` (worker claim) **[builder's call]**.
- `NotificationReadRepository` (`I...ReadRepository` in Application): list and
  unread count as `AsNoTracking` projections; the message is rendered in
  Application from the projected fields.
- Mark-read commands use the write repository and the aggregate method.

## 12. Tests

- Unit: `Notification.Create` guards, `MarkRead` idempotence, email state
  transitions and backoff; `IAssignmentNotifier` recipient rules for all five
  events, including "actor never notified" and "no change, no notification";
  message rendering; options validators.
- Integration (real SQL Server, Redis): each trigger command stores the right
  notifications in the same transaction (a failed save stores none); list,
  unread count, mark read, read all, other users' notifications return 404 or are
  absent; retention filter and cleanup; outbox claim race (two claimers, each row
  claimed once), retry and failure after max attempts with a fake
  `IEmailSender`; SSE stream receives `notification` and `dashboard-changed`
  through Redis, and the stream ends when the token version changes.
- The integration host disables both background workers and replaces
  `IEmailSender` with a fake; worker tests drive one worker cycle directly
  **[builder's call]**.
- No test sends a real email; the MailKit sender is exercised manually against
  Mailpit (steps in the README).
- Architecture tests stay green; endpoints declare `[Authorize]` and their
  response types.

## 13. Documents to update with this work

`docs/openapi.json`, `docs/CONTEXT.md` (Notifications built; next: AI endpoint),
`README.md` (endpoints, Mailpit, `Email` settings), `.env.example`,
`docker-compose.yml`, `docs/adr/README.md` (ADR 0017, 0018).

## 14. Left to the builder

Class and file names beyond those above, the exact SSE writer loop, how the
registry stores connections, the JSON shape of the Redis message, test names,
commit order, and the exact exception types caught around SMTP and Redis (from
the libraries' documentation, not guessed).
