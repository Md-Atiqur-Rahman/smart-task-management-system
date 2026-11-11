# Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stored notifications for five assignment events, live pop-ups and a real-time dashboard over SSE (fanned out through Redis pub/sub), and email through a database outbox sent with MailKit.

**Architecture:** A `Notification` aggregate whose row is also the email outbox (ADR 0017); command handlers call one Application notifier before `SaveChangesAsync` and flush buffered live events after a successful save; Infrastructure publishes live events to Redis, every instance writes them to its own SSE connections (ADR 0018); two background workers send email and delete expired rows.

**Tech Stack:** .NET 10, ASP.NET Core (`TypedResults.ServerSentEvents`), EF Core SQL Server, StackExchange.Redis 3.4.31 (already referenced), **MailKit 5.0.0 (the only new package, Task 7)**, Mailpit `axllent/mailpit:v1.33.1` (Docker, dev only), xunit.v3, NSubstitute, Shouldly, Testcontainers.

**Spec:** `docs/notifications-design.md` (approved 2026-11-11, including every **[builder's call]**), `docs/PRD.md` section 13, ADR 0017, ADR 0018. Read the design section named in each task before starting it.

## Global Constraints

- Every rule in `CLAUDE.md`, `.claude/rules/` and the layer `CLAUDE.md` files applies; `docs/CONTEXT.md` working rules apply (No-Hallucination Policy, evidence before claims, tests first, stop on surprises). No new package except MailKit 5.0.0 in Task 7 (verify again with `dotnet package search MailKit --exact-match` before adding; CPM only). No domain-events, caching, broker or SignalR packages.
- Events (design 3): `TaskAssigned` to the new assignee; `TaskUnassigned` to the previous assignee; `ProjectOwnerAssigned` to the owner an Admin chose on create and to the new owner on change; `ProjectOwnerRemoved` to the previous owner. **The actor is never a recipient.** A command that changes nothing creates nothing.
- Messages (exact, English): `TaskAssigned` "{Actor} assigned you the task "{Subject}"."; `TaskUnassigned` "{Actor} removed the task "{Subject}" from you."; `ProjectOwnerAssigned` "{Actor} made you the owner of the project "{Subject}"."; `ProjectOwnerRemoved` "{Actor} removed your ownership of the project "{Subject}".". "Removed" types carry no `TaskId`/`ProjectId`.
- Live event types (exact): `notification` (data `NotificationDto`), `notifications-read` (no data), `dashboard-changed` (no data). Published only after a successful `SaveChangesAsync`. Redis down: publish logs a warning and the command still succeeds (fail open).
- `dashboard-changed` recipients: previous and new assignee, project owner (and previous owner on an owner change), on project deletion the assignees of its non-deleted tasks, every Admin, and the actor. Sent by task create/update/status/assign/delete and project create/owner change/delete; not by project name/description update.
- SSE: `GET /api/notifications/stream`, `[Authorize]`; token never in the URL; the stream ends at the access token's `exp` and at the first heartbeat whose token-version check is `Revoked` or `Unavailable`.
- Options (design 10), all `ValidateDataAnnotations` + `ValidateOnStart`, defaults in `src/Api/appsettings.json`: `Notifications:RetentionDays` 7 [1-90], `ListLimit` 50 [1-200], `HeartbeatSeconds` 25 [5-120], `CleanupIntervalMinutes` 60 [1-1440], `RedisChannel` "notifications" (required); `Email:PollSeconds` 10 [1-300], `BatchSize` 20 [1-100], `ClaimSeconds` 120 [30-900], `MaxAttempts` 5 [1-20].
- Email: `Email:UseTls=false` only in Development and Testing (same rule as ADR 0011); TLS certificate validation never disabled; `Email:Password` only in user-secrets or environment. Retry delay after attempt n = 2^n minutes. Recipient email and name never logged (log the notification id). Neither `COMPANY_NAME` nor `PROJECT_NAME` (with or without spaces) in `appsettings*.json`, `docker-compose.yml` or code strings: the email subject prefix is `Email:FromName`.
- Only the recipient can see, read or count a notification; only rows newer than `now - RetentionDays` are listed or counted.
- "Now" only from `TimeProvider`. Honest `?` annotations (NullGuard). Expected failures return `Result`; catch only specific exceptions.
- Integration tests share one database: assert only on fresh users created by the test. The integration host disables both background workers and uses a fake `IEmailSender`; tests without Redis use an in-process live-event publisher.
- One commit per task (review fixes as extra `fix:`/`docs:` commits, never amend), prefix `feat:` / `fix:` / `docs:`, ending with the `Co-Authored-By` line; repository-local identity; do not push.
- Tests in Release: `dotnet test --solution Datavanced.SmartTaskManagement.slnx -c Release`; Docker Desktop must run.
- When controllers or DTOs change: `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` in the same task. Model changes: a migration (command in `src/Infrastructure/CLAUDE.md`).
- **Stop at each milestone** (end of Tasks 4, 6 and 9): `bash scripts/verify.sh` in Git Bash, show the output, wait for the developer.

## Review Focus

1. The actor is also the would-be recipient (a PM assigns a task to themselves; a PM creates their own project): no notification, no email (Task 4).
2. Reassignment A -> B in one command: A gets `TaskUnassigned`, B gets `TaskAssigned`, both get `dashboard-changed`; assigning the same user again creates nothing (Task 4).
3. The save fails (stale `RowVersion` -> `concurrency.conflict`): no notification row and no live event (Task 4).
4. Another user's notification id, or one older than the retention window: read returns 404 `notifications.not_found`, list and count never include it (Task 5).
5. A user logs out or their role changes while the stream is open: the stream ends at the next heartbeat; with Redis down a task assignment still returns success and stores the notification (Tasks 3, 6).

---

## Milestone 1: Model, live events, notifications on commands

### Task 1: `Notification` aggregate

Design: sections 3 and 4.

**Files:**
- Create under `src/Domain/Notifications/`: `Notification.cs`, `NotificationType.cs`, `EmailDeliveryStatus.cs`, `NotificationErrors.cs`, `INotificationRepository.cs`
- Test: `tests/UnitTests/Domain/Notifications/NotificationTests.cs`

**Interfaces:**
- Produces:
  - `enum NotificationType { TaskAssigned = 1, TaskUnassigned = 2, ProjectOwnerAssigned = 3, ProjectOwnerRemoved = 4 }`
  - `enum EmailDeliveryStatus { Pending = 1, Sent = 2, Failed = 3 }`
  - `sealed class Notification : AggregateRoot<Guid>`: `SubjectMaxLength = 200`, `ActorNameMaxLength = User.FullNameMaxLength`, `EmailErrorMaxLength = 1000`; properties `RecipientId`, `Type`, `TaskId?`, `ProjectId?`, `Subject`, `ActorName`, `CreatedAt`, `ReadAt?`, `EmailStatus`, `EmailAttempts`, `EmailNextAttemptAt`, `EmailClaimedUntil?`, `EmailSentAt?`, `EmailLastError?` (private setters, private constructor for EF)
  - `static Notification Create(Guid recipientId, NotificationType type, Guid? taskId, Guid? projectId, string subject, string actorName, DateTimeOffset createdAt)`: guards (empty recipient; `TaskAssigned` needs `taskId`, `ProjectOwnerAssigned` needs `projectId`, "removed" types must have both null; subject/actor not blank, trimmed, max lengths); `Id = Guid.CreateVersion7()`, `EmailStatus = Pending`, `EmailAttempts = 0`, `EmailNextAttemptAt = createdAt`
  - `bool MarkRead(DateTimeOffset at)` (true only on the first call), `bool IsRead`
  - `void EmailSent(DateTimeOffset at)`; `void EmailFailed(string error, DateTimeOffset now, int maxAttempts)` (attempts + 1; error truncated to `EmailErrorMaxLength`; `Failed` when attempts reach `maxAttempts`, else `EmailNextAttemptAt = now + 2^attempts minutes`; clears `EmailClaimedUntil`)
  - `static class NotificationErrors { NotFound = Error.NotFound("notifications.not_found", ...) }` (match the existing `<Aggregate>Errors` style)
  - `interface INotificationRepository { void Add(Notification n); Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct); Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken ct); }`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Create_TaskAssigned_PendingEmailAndUnread()          // EmailStatus Pending, attempts 0, NextAttemptAt == createdAt, IsRead false
[Theory] /* TaskAssigned without taskId; ProjectOwnerAssigned without projectId; TaskUnassigned with taskId; empty recipient; blank subject */
public void Create_InvalidArguments_Throws(...)                       // ArgumentException
[Fact] public void Create_LongSubject_IsRejected()                     // 201 chars -> ArgumentException
[Fact] public void MarkRead_Twice_SecondReturnsFalseAndKeepsFirstTime()
[Fact] public void EmailFailed_BeforeMax_SchedulesBackoff()            // attempt 1 -> now + 2 min, attempt 2 -> now + 4 min, status Pending
[Fact] public void EmailFailed_AtMax_IsFailed()                        // maxAttempts 5, 5th failure -> Failed
[Fact] public void EmailFailed_LongError_IsTruncated()                 // 1500 chars -> 1000
[Fact] public void EmailSent_SetsSentAndStatus()
```

- [ ] **Step 2: Run** `dotnet test --project tests/UnitTests/UnitTests.csproj -c Release`. Expected: build fails.
- [ ] **Step 3: Implement**, following `TaskItem` and `Project` (guards via `Guard`, constructor binding by name).
- [ ] **Step 4: Run** unit and architecture tests. Expected: PASS (entity rules: no public setters).
- [ ] **Step 5: Commit** `feat: add the Notification aggregate`

### Task 2: Notification persistence

Design: sections 4 and 11.

**Files:**
- Create: `src/Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`, `src/Infrastructure/Persistence/Repositories/NotificationRepository.cs`, migration `AddNotifications` under `src/Infrastructure/Persistence/Migrations/`
- Modify: `AppDbContext` (DbSet), `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register)
- Test: `tests/IntegrationTests/Infrastructure/NotificationPersistenceTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: table `Notifications`; shadow property `EmailClaimToken` (`Guid?`) used only by the outbox claim (Task 7); indexes `(RecipientId, CreatedAt)` and `(EmailStatus, EmailNextAttemptAt)`; enums stored as `int`; string lengths from the aggregate constants. No query filter, no `RowVersion` (design 4).

- [ ] **Step 1: Write the failing tests:** `Add_Then_GetById_RoundTripsAllFields`, `ListUnread_ReturnsOnlyRecipientsUnreadSince` (other recipient, read row and row older than `since` excluded), `MarkRead_IsPersisted`.
- [ ] **Step 2: Run** integration tests. Expected: build fails.
- [ ] **Step 3: Implement**; add the migration with the command in `src/Infrastructure/CLAUDE.md`; review the generated migration (one table, two indexes, nothing else).
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: persist notifications`

### Task 3: Live event infrastructure

Design: section 6 ("Fan-out across instances"), ADR 0018.

**Files:**
- Create (Application): `src/Application/Abstractions/LiveEvents/{LiveEvent,LiveEventTypes,ILiveEventPublisher,ILiveConnections,LiveEventBuffer}.cs`, `src/Application/Features/Notifications/NotificationDto.cs`, `src/Application/Features/Notifications/NotificationsOptions.cs`, `src/Application/Configuration/Notifications/HostApplicationBuilderExtensions.cs` (`ConfigureNotifications`, called from `ConfigureApplication`)
- Create (Infrastructure): `src/Infrastructure/LiveEvents/{LiveConnectionRegistry,RedisLiveEventPublisher,RedisLiveEventSubscriber}.cs`, `src/Infrastructure/Configuration/LiveEvents/HostApplicationBuilderExtensions.cs` (`ConfigureLiveEvents`, called from `ConfigureInfrastructure` after `ConfigureRedis`)
- Create (tests): `tests/IntegrationTests/Api/TestSupport/InProcessLiveEventPublisher.cs` (follow how tests already reach Infrastructure types; Infrastructure has no `InternalsVisibleTo` today, so match the visibility the existing Redis classes use); modify `tests/IntegrationTests/Api/ApiFactory.cs` (use it unless `WithRedis`, and then also remove `RedisLiveEventSubscriber`)
- Modify: `src/Api/appsettings.json` (`Notifications` section)
- Test: `tests/IntegrationTests/Infrastructure/LiveEventFanOutTests.cs`, `tests/UnitTests/Application/Notifications/LiveEventBufferTests.cs`, `tests/UnitTests/Application/Notifications/NotificationsOptionsTests.cs`

**Interfaces:**
- Produces:
  - `sealed record NotificationDto(Guid Id, NotificationType Type, string Message, Guid? TaskId, Guid? ProjectId, DateTimeOffset CreatedAt, bool IsRead)`
  - `static class LiveEventTypes { Notification = "notification"; NotificationsRead = "notifications-read"; DashboardChanged = "dashboard-changed"; }`
  - `sealed record LiveEvent(string Type, IReadOnlyList<Guid> UserIds, bool IncludeAdmins, NotificationDto? Notification)`
  - `interface ILiveEventPublisher { Task PublishAsync(IReadOnlyList<LiveEvent> events, CancellationToken ct); }` (never throws for a Redis outage)
  - `interface ILiveConnections { ILiveConnection Open(Guid userId, Role role); }`, `interface ILiveConnection : IDisposable { ChannelReader<LiveEvent> Events { get; } }`; the registry also exposes `void Deliver(LiveEvent e)` to the subscriber and the in-process publisher (a user with several tabs gets the event on every connection; `IncludeAdmins` reaches every open Admin connection; a full or closed connection never blocks others)
  - `internal sealed class LiveEventBuffer` (scoped): `void Add(LiveEvent e)`, `Task FlushAsync(CancellationToken ct)` (publishes once, then clears; empty buffer publishes nothing)
  - `NotificationsOptions` exactly as Global Constraints
- Redis: one channel `RedisChannel.Literal(options.RedisChannel)`, JSON message; `IConnectionMultiplexer.GetSubscriber()`, `ISubscriber.PublishAsync`, `ISubscriber.SubscribeAsync` (verified in the installed 3.4.31 XML docs). Catch the same Redis exception types the existing Redis classes catch (see `RedisExceptionCoverageTests`), log a warning, continue.

- [ ] **Step 1: Write the failing tests**

```csharp
// unit
[Fact] public async Task Flush_PublishesBufferedEventsOnceThenClears()
[Fact] public async Task Flush_Empty_DoesNotPublish()
[Theory, InlineData(0), InlineData(91)] public void Options_RetentionOutOfRange_FailValidation(int days)
// integration (RedisFixture)
[Fact] public async Task Publish_ThroughRedis_ReachesEveryConnectionOfTheUser()   // two connections, both receive
[Fact] public async Task Publish_IncludeAdmins_ReachesAdminNotOtherUsers()
[Fact] public async Task Publish_RedisDown_DoesNotThrow()                         // multiplexer pointing at a closed port
```

- [ ] **Step 2: Run** unit and integration tests. Expected: build fails.
- [ ] **Step 3: Implement.** If the documented StackExchange.Redis API differs from the above, STOP and report.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add live event fan-out through Redis`

### Task 4: Notifications from commands

Design: sections 3, 4 and 5; Review Focus 1-3.

**Files:**
- Create: `src/Application/Features/Notifications/{NotificationMessages,IAssignmentNotifier,AssignmentNotifier}.cs`
- Modify the handlers of `CreateTask`, `AssignTask`, `UpdateTask`, `ChangeTaskStatus`, `DeleteTask`, `CreateProject`, `ChangeProjectOwner`, `DeleteProject`; `ITaskItemReadRepository` + `TaskItemReadRepository` (assignee ids of a project); Application DI (scoped notifier and buffer)
- Test: `tests/UnitTests/Application/Notifications/{NotificationMessagesTests,AssignmentNotifierTests}.cs`, `tests/IntegrationTests/Notifications/NotificationTriggerTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces:
  - `static class NotificationMessages { string Render(NotificationType type, string subject, string actorName); }` (exact texts in Global Constraints)
  - `interface IAssignmentNotifier`:
    - `Task TaskAssigneeChangedAsync(TaskItem task, Guid? previousAssigneeId, Guid actorId, CancellationToken ct)`
    - `Task ProjectOwnerChangedAsync(Project project, Guid? previousOwnerId, Guid actorId, CancellationToken ct)` (`previousOwnerId` null on create)
    - `Task TaskChangedAsync(TaskItem task, Guid actorId, CancellationToken ct)` (dashboard signal only)
    - `Task ProjectDeletedAsync(Project project, Guid actorId, CancellationToken ct)` (dashboard signal incl. task assignees)
  - It adds `Notification` rows through `INotificationRepository` (actor name from `IUserRepository`, time from `TimeProvider`) and `LiveEvent`s to `LiveEventBuffer`.
  - `ITaskItemReadRepository.GetAssigneeIdsAsync(Guid projectId, CancellationToken ct) : Task<IReadOnlyList<Guid>>` (distinct, non-deleted tasks).
- Handler pattern: read the previous assignee/owner before the aggregate call; call the notifier only when the aggregate reports a change (or on create/delete); `var result = await unitOfWork.SaveChangesAsync(ct); if (result.IsSuccess) await liveEvents.FlushAsync(ct); return result;`.

- [ ] **Step 1: Write the failing tests**

```csharp
// unit (NSubstitute repositories, FakeTimeProvider)
[Fact] public async Task TaskAssigned_ToOther_NotifiesAssigneeAndBuffersEvents()
[Fact] public async Task TaskAssigned_ToActor_CreatesNoNotification()                 // Review Focus 1
[Fact] public async Task TaskReassigned_NotifiesBoth_DashboardToBothOwnerAdminsActor() // Review Focus 2
[Fact] public async Task TaskUnassigned_NotifiesPreviousOnly_NoLink()
[Fact] public async Task ProjectCreatedByAdminForPm_NotifiesOwner()
[Fact] public async Task ProjectCreatedByPmForSelf_CreatesNoNotification()            // Review Focus 1
[Fact] public async Task OwnerChanged_NotifiesNewAndPreviousOwner()
[Fact] public async Task ProjectDeleted_DashboardIncludesTaskAssignees()
[Theory] /* four types */ public void Render_ReturnsExactMessage(...)
// integration (real host, AuthApi)
[Fact] public async Task AssignTask_StoresNotificationForAssignee()
[Fact] public async Task AssignSameUserAgain_StoresNothingNew()                       // Review Focus 2
[Fact] public async Task CreateTaskWithAssignee_StoresNotification()
[Fact] public async Task ChangeProjectOwner_StoresTwoNotifications()
[Fact] public async Task FailedSave_StoresNoNotificationAndPublishesNothing()         // Review Focus 3: stale RowVersion
[Fact] public async Task AssignTask_RedisDown_StillSucceeds()                         // Review Focus 5 (second half)
```

- [ ] **Step 2: Run** unit and integration tests. Expected: build fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all tests. Expected: PASS; existing task and project tests unchanged.
- [ ] **Step 5: Commit** `feat: create notifications on assignment changes`

**MILESTONE 1: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 2: Notification API and live stream

### Task 5: Notification endpoints

Design: section 7; Review Focus 4.

**Files:**
- Create under `src/Application/Features/Notifications/`: `INotificationReadRepository.cs`, `NotificationRow.cs`, `UnreadCountDto.cs`, `Queries/ListNotifications/*`, `Queries/GetUnreadCount/*`, `Commands/MarkNotificationRead/*`, `Commands/MarkAllNotificationsRead/*`
- Create: `src/Infrastructure/Persistence/ReadRepositories/NotificationReadRepository.cs`, `src/Api/Controllers/Notifications/NotificationsController.cs`
- Modify: Database DI registration, `docs/openapi.json` (regenerated)
- Test: `tests/UnitTests/Application/Notifications/NotificationQueryHandlerTests.cs`, `tests/IntegrationTests/Notifications/NotificationEndpointsTests.cs`

**Interfaces:**
- Consumes: Tasks 1-4.
- Produces:
  - `sealed record NotificationRow(Guid Id, NotificationType Type, string Subject, string ActorName, Guid? TaskId, Guid? ProjectId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt)`
  - `interface INotificationReadRepository { Task<IReadOnlyList<NotificationRow>> ListAsync(Guid recipientId, DateTimeOffset since, int limit, CancellationToken ct); Task<int> CountUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken ct); }`
  - `sealed record UnreadCountDto(int Count)`
  - Queries `ListNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>`, `GetUnreadCountQuery : IQuery<UnreadCountDto>`; commands `MarkNotificationReadCommand(Guid Id) : ICommand`, `MarkAllNotificationsReadCommand : ICommand` (both publish `notifications-read` to the caller after a successful save; nothing when nothing changed)
  - Routes: `GET api/notifications`, `GET api/notifications/unread-count`, `POST api/notifications/{id}/read` (204, 404), `POST api/notifications/read-all` (204); `[Authorize]`; 200/204/401/404/503 declared like `ProjectsController`
- `since = now - RetentionDays`; newest first; `limit = ListLimit`; message from `NotificationMessages.Render`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task List_ReturnsOnlyOwnNewestFirstWithMessage()
[Fact] public async Task List_ExcludesOlderThanRetention()                 // row inserted with CreatedAt now - 8 days
[Fact] public async Task UnreadCount_CountsOnlyOwnUnreadInWindow()
[Fact] public async Task MarkRead_OtherUsersNotification_Returns404()      // Review Focus 4, errorCode notifications.not_found
[Fact] public async Task MarkRead_ThenCount_DropsByOne()
[Fact] public async Task ReadAll_MarksEveryOwnUnread()
[Fact] public async Task Endpoints_NoToken_Return401()
```

- [ ] **Step 2: Run.** Expected: build fails / 404 for missing routes.
- [ ] **Step 3: Implement**, copying the Dashboard slice; `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add notification endpoints` (include `docs/openapi.json`)

### Task 6: SSE stream

Design: section 6 ("Endpoint", "Authentication of the stream"); Review Focus 5.

**Files:**
- Create: `src/Api/Notifications/NotificationStream.cs` (internal: builds the `IAsyncEnumerable<SseItem<...>>`)
- Modify: `NotificationsController` (`GET api/notifications/stream`), `docs/openapi.json`
- Test: `tests/IntegrationTests/Notifications/NotificationStreamTests.cs`

**Interfaces:**
- Consumes: `ILiveConnections` (Task 3), `ITokenVersionChecker`, `ISecurityContext`, `NotificationsOptions.HeartbeatSeconds`, `TimeProvider`.
- Behaviour: opens one `ILiveConnection` for the caller (disposed when the stream ends or the client disconnects); yields each `LiveEvent` as an `SseItem` with the event type from `LiveEventTypes` and the `NotificationDto` as data for `notification`; every `HeartbeatSeconds` yields a keep-alive and re-checks the token version (`Revoked` or `Unavailable` ends the stream); ends when `now >= exp` of the current token. Use `TypedResults.ServerSentEvents` (ASP.NET Core 10). Whether a comment-line keep-alive is possible with `SseItem` is UNVERIFIED: check the API docs; if not possible, send an event of type `heartbeat` with empty data and add `heartbeat` to `LiveEventTypes`.

- [ ] **Step 1: Write the failing tests** (read the response with `HttpCompletionOption.ResponseHeadersRead`)

```csharp
[Fact] public async Task Stream_ReceivesNotificationWhenTaskAssigned()     // content type text/event-stream, event: notification, message text
[Fact] public async Task Stream_ReceivesDashboardChangedOnStatusChange()
[Fact] public async Task Stream_EndsAfterLogout()                          // short HeartbeatSeconds via settings; stream completes
[Fact] public async Task Stream_EndsWhenTokenExpires()                     // FakeTimeProvider advanced past exp
[Fact] public async Task Stream_NoToken_Returns401()
[Fact] public async Task Stream_ThroughRedis_ReachesUserAcrossFactories()  // WithRedis on two ApiFactory instances: assign on one, receive on the other
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement**; regenerate `docs/openapi.json`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: stream live events over SSE`

**MILESTONE 2: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 3: Email, retention, documentation

### Task 7: Email outbox with MailKit

Design: section 8; ADR 0017.

**Files:**
- Modify: `Directory.Packages.props` (MailKit 5.0.0), `src/Infrastructure/Infrastructure.csproj`, `src/Api/appsettings.json` (`Email` without secrets), `src/Api/appsettings.Development.json` (`Host` localhost, `Port` 1025, `UseTls` false, `AppBaseUrl` `http://localhost:4200`), `.env.example` (`MAILPIT_IMAGE=axllent/mailpit:v1.33.1`, `MAIL_SMTP_PORT=1025`, `MAIL_UI_PORT=8025`, with the verification date comment), `docker-compose.yml` (`mailpit` service on `127.0.0.1`; api env `Email__Host=mailpit`, `Email__Port=1025`, `Email__UseTls=false`), `tests/IntegrationTests/Api/ApiFactory.cs` (fake sender, workers removed)
- Create: `src/Application/Abstractions/Email/{IEmailSender,EmailMessage}.cs`; `src/Infrastructure/Email/{MailKitEmailSender,EmailOutboxProcessor,EmailOutboxWorker}.cs`; `src/Infrastructure/Configuration/Email/{EmailOptions,EmailOptionsValidator,HostApplicationBuilderExtensions}.cs` (`ConfigureEmail`)
- Test: `tests/IntegrationTests/Infrastructure/{EmailOutboxTests,EmailOptionsTests}.cs`

**Interfaces:**
- Produces:
  - `sealed record EmailMessage(string ToAddress, string ToName, string Subject, string Body)`; `interface IEmailSender { Task SendAsync(EmailMessage message, CancellationToken ct); }`
  - `EmailOptions` (`SectionName = "Email"`): `Host`, `Port`, `UseTls`, `UserName?`, `Password?`, `FromAddress`, `FromName`, `AppBaseUrl`, `TaskPathTemplate` (`/tasks/{id}`), `ProjectPathTemplate` (`/projects/{id}`), `PollSeconds`, `BatchSize`, `ClaimSeconds`, `MaxAttempts`; validator rejects `UseTls=false` outside Development/Testing and templates without `{id}`
  - `EmailOutboxProcessor.ProcessBatchAsync(CancellationToken ct) : Task<int>` (one cycle; the worker calls it every `PollSeconds`)
- Claim algorithm (one cycle): select up to `BatchSize` ids with `EmailStatus == Pending && EmailNextAttemptAt <= now && (EmailClaimedUntil == null || EmailClaimedUntil < now)`; one `ExecuteUpdateAsync` over those ids **repeating the same condition** sets `EmailClaimedUntil = now + ClaimSeconds` and `EmailClaimToken = <new Guid>`; load only rows with that token; send each (recipient email and full name loaded at send time; subject `"{FromName}: {message}"`; body = message + link when the notification has one); success -> `EmailSent`, expected send failure -> `EmailFailed(error, now, MaxAttempts)` and a warning log (error log when it becomes `Failed`); save.
- Exceptions treated as "send failed": take the exact types from the MailKit 5.0 documentation (SMTP command/protocol, authentication, socket, I/O); UNVERIFIED until checked — do not guess type names.

- [ ] **Step 1: Write the failing tests** (fake `IEmailSender`)

```csharp
[Fact] public async Task Process_SendsPendingAndMarksSent()
[Fact] public async Task Process_TwoProcessorsConcurrently_EachRowSentOnce()   // 10 rows, two processors in parallel, fake records 10 sends total
[Fact] public async Task Process_SendFails_SchedulesRetryWithBackoff()
[Fact] public async Task Process_FailsMaxAttempts_IsFailedAndNotRetried()
[Fact] public async Task Process_ExpiredClaim_IsPickedUpAgain()
[Fact] public async Task Process_LinkUsesTemplate()                            // body contains AppBaseUrl + /tasks/{id}
[Fact] public void Options_UseTlsFalseInProduction_FailsValidation()
[Fact] public void Options_TemplateWithoutId_FailsValidation()
```

- [ ] **Step 2: Run.** Expected: build fails.
- [ ] **Step 3:** `dotnet package search MailKit --exact-match` (record output), add MailKit 5.0.0 via CPM, implement, then `docker compose up -d mailpit` and send one real test email by assigning a task through Swagger; describe the manual check in the report (no secrets printed).
- [ ] **Step 4: Run** all tests, then `bash scripts/verify.sh` once (its package step checks MailKit and its dependencies). Expected: PASS, no vulnerable or deprecated package.
- [ ] **Step 5: Commit** `feat: send notification emails through an outbox`

### Task 8: Retention cleanup

Design: section 9.

**Files:**
- Create: `src/Infrastructure/Notifications/{NotificationCleanup,NotificationCleanupWorker}.cs`; register in `ConfigureLiveEvents` or a new `ConfigureNotifications` (Infrastructure)
- Modify: `ApiFactory` (worker removed)
- Test: `tests/IntegrationTests/Infrastructure/NotificationCleanupTests.cs`

**Interfaces:**
- Produces: `NotificationCleanup.DeleteExpiredAsync(CancellationToken ct) : Task<int>` (one `ExecuteDeleteAsync` where `CreatedAt < now - RetentionDays`, any email status); the worker calls it every `CleanupIntervalMinutes`.

- [ ] **Step 1: Write the failing tests:** `DeleteExpired_RemovesOnlyOlderThanRetention` (rows at 8 days, exactly 7 days minus one minute, now), `DeleteExpired_RemovesPendingEmailRowsToo`, `DeleteExpired_RunTwice_IsHarmless`.
- [ ] **Step 2: Run.** Expected: build fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: delete expired notifications`

### Task 9: Documentation and final verification

Design: section 13.

**Files:**
- Modify: `docs/notifications-design.md` (Status: Implemented), `docs/CONTEXT.md` (Notifications built; next: the AI endpoint; real test count), `README.md` (notification endpoints, the SSE stream and its `fetch` + bearer requirement, Mailpit at `http://localhost:8025`, `Email` settings and how to point them at a real SMTP provider, reverse proxy must not buffer `text/event-stream`)

- [ ] **Step 1:** Update the documents; no secrets; no src changes.
- [ ] **Step 2:** Commit `docs: document Notifications`, then `bash scripts/verify.sh`. Expected: all steps pass; if the test count in CONTEXT.md differs from the run, fix it in `docs: correct the test count`.

**MILESTONE 3: show the final verify output; then the final whole-branch review, push and open a PR against `main` (developer merges manually).**
