# SamSoft.Mediator.CQRS

A high-performance .NET CQRS mediator inspired by MediatR, with Result-typed commands/queries, pipeline behaviors, and Microsoft.Extensions.DependencyInjection integration.

**Current version:** 1.6.0 · **Targets:** `net8.0;net9.0;net10.0` · **License:** MIT

---

## Features

- CQRS abstractions for commands, queries, and notifications
- Automatic handler and FluentValidation discovery from assemblies
- Open-generic pipeline behaviors (logging, validation, timeout, pre/post processors)
- Notification publish strategies: **Parallel** (default) and **Sequential**
- Configurable mediator lifetime (`Scoped` by default)
- Cached request wrappers for low-overhead dispatch

---

## Install

```bash
dotnet add package SamSoft.Mediator.CQRS
```

---

## Registration

There is **one** entry point: `AddMediatorService`. Configure everything through `MediatorOptions`.

### Recommended (full control)

```csharp
services.AddLogging(); // required if you enable logging behaviors

services.AddMediatorService(options =>
{
    // 1) Where to find handlers / FluentValidation validators
    options.RegisterServicesFromAssembly(typeof(MyHandler).Assembly);
    // options.RegisterServicesFromAssemblies(asm1, asm2);

    // 2) Mediator DI lifetime
    options.Lifetime = ServiceLifetime.Scoped; // Scoped | Singleton | Transient

    // 3) Notifications
    options.DefaultNotificationPublishStrategy = NotificationPublishStrategy.Parallel;

    // 4) Built-in pipeline behaviors (all false by default)
    options.RegisterTimeoutBehavior = true;
    options.RegisterPrePostProcessorBehavior = true;
    options.RegisterValidationBehavior = true;
    options.RegisterLoggingBehavior = true;

    // 5) Timeout duration (used when RegisterTimeoutBehavior = true)
    options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(10);

    // 6) Custom open-generic pipeline / processors
    options.AddOpenBehavior(typeof(AdvancedLoggingBehavior<,>));
    options.AddRequestPreProcessor(typeof(MyPreProcessor<>));
    options.AddRequestPostProcessor(typeof(MyPostProcessor<,>));
});
```

### Convenience overload

Scans assemblies and turns **timeout + pre/post** on (validation/logging stay off unless you use the options overload):

```csharp
services.AddMediatorService(typeof(MyHandler).Assembly);
// or, scan the calling assembly:
services.AddMediatorService();
```

### Manual handlers (no assembly scan)

```csharp
services.AddMediatorService(options =>
{
    options.RegisterHandlersFromCallingAssembly = false;
    // Register handlers yourself:
    // services.AddTransient<ICommandHandler<MyCommand, string>, MyHandler>();
});
```

---

## `MediatorOptions` reference

Every property below is applied by `AddMediatorService`.

### Core

| Option | Default | How to use |
|--------|---------|------------|
| `Lifetime` | `Scoped` | DI lifetime for `IMediator`. Use `Scoped` in ASP.NET Core. |
| `DefaultNotificationPublishStrategy` | `Parallel` | Default for `Publish` when the notification has no `[NotificationPublishStrategy]`. |
| `TimeoutSettings.Timeout` | `5 seconds` | Max duration for `TimeoutBehavior`. Only meaningful when `RegisterTimeoutBehavior = true`. |

### Assembly scanning

| Option / method | Default | How to use |
|-----------------|---------|------------|
| `RegisterServicesFromAssembly(assembly)` | — | Scan one assembly for handlers + FluentValidation validators. |
| `RegisterServicesFromAssemblies(...)` | — | Scan several assemblies. |
| `AssembliesToRegister` | empty | List filled by the methods above (usually you don’t edit it directly). |
| `RegisterHandlersFromCallingAssembly` | `true` | If no assemblies were registered, scan the **calling** assembly. Set `false` when you register handlers manually. |

**Notes**

- Scanning registers command/query/notification handlers and FluentValidation validators.
- **One** command/query handler per request type — duplicates throw at registration.
- **Many** notification handlers per notification type are allowed.

### Built-in pipeline flags

All default to `false` (opt-in).

| Option | Registers | When to enable |
|--------|-----------|----------------|
| `RegisterTimeoutBehavior` | `TimeoutBehavior<,>` | Cancel long-running handlers after `TimeoutSettings.Timeout`. |
| `RegisterPrePostProcessorBehavior` | `PrePostProcessorBehavior<,>` | Run `IRequestPreProcessor<>` / `IRequestPostProcessor<,>` around the handler. |
| `RegisterValidationBehavior` | `ValidationBehavior<,>` | Run FluentValidation; failures return `Result.Failure` (not exceptions). |
| `RegisterLoggingBehavior` | `LoggingPipelineBehavior<,>` | Log request type + duration; **payloads only at Debug**. |

#### Pipeline order

When Timeout + PrePost + Validation + Logging are all enabled:

```text
Timeout (outer)
  → PrePost
    → Validation
      → Logging
        → handler
```

Behaviors you add with `AddOpenBehavior` **inside** `configure` are registered **before** those builtins, so they sit **outside** (first custom = outermost).

### Custom behaviors and processors

| Method | Registers | Example |
|--------|-----------|---------|
| `AddOpenBehavior(typeof(MyBehavior<,>))` | Open-generic `IPipelineBehavior<,>` | Auth, caching, transaction, `AdvancedLoggingBehavior<,>` |
| `AddRequestPreProcessor(typeof(MyPre<>))` | Open-generic `IRequestPreProcessor<>` | Normalize input, load tenant |
| `AddRequestPostProcessor(typeof(MyPost<,>))` | Open-generic `IRequestPostProcessor<,>` | Publish domain events after success |

You can also call `services.AddOpenBehavior(...)` / `services.AddPipelineBehaviors(...)` after `AddMediatorService`.

---

## Commands, queries, and notifications

```csharp
public sealed class CreateUserCommand : ICommand<string> { /* ... */ }

internal sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, string>
{
    public Task<Result<string>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success("created"));
}

var result = await mediator.Send(new CreateUserCommand());

public sealed class UserCreatedNotification : INotification { /* ... */ }

[NotificationPublishStrategy(NotificationPublishStrategy.Sequential)]
public sealed class OrderedEvent : INotification { /* ... */ }

await mediator.Publish(new UserCreatedNotification());
```

Handlers always return `Result` / `Result<T>` from `SamSoft.Common`.

### Notification strategies

| Strategy | Behavior |
|----------|----------|
| `Parallel` (default) | Handlers run concurrently via `Task.WhenAll`; faults and cancellations are surfaced |
| `Sequential` | Handlers run one-by-one; stops on first exception; honors caller cancellation between handlers |

```csharp
// App-wide default
options.DefaultNotificationPublishStrategy = NotificationPublishStrategy.Sequential;

// Or per notification type
[NotificationPublishStrategy(NotificationPublishStrategy.Parallel)]
public sealed class FanOutEvent : INotification;
```

---

## Pipeline behaviors (custom)

```csharp
public sealed class MyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // before
        var response = await next(cancellationToken);
        // after
        return response;
    }
}
```

```csharp
options.AddOpenBehavior(typeof(MyBehavior<,>));
// or
services.AddOpenBehavior(typeof(MyBehavior<,>));
```

### Built-in behavior details

| Behavior | Notes |
|----------|--------|
| `ValidationBehavior<,>` | FluentValidation for **commands and queries**; failures → `Result.Failure` with `Error.Metadata` as `PropertyName → string[]` |
| `TimeoutBehavior<,>` | Linked `CancellationTokenSource` + `CancelAfter`; maps timeout to `TimeoutException` |
| `PrePostProcessorBehavior<,>` | Runs registered pre-processors, then handler, then post-processors |
| `LoggingPipelineBehavior<,>` | Type/duration at Information; property dump at Debug |
| `AdvancedLoggingBehavior<,>` | Structured logs; full `{@Request}` / `{@Response}` only at Debug |

Prefer `Microsoft.Extensions.Logging.ILogger<T>`. `IMediatorLogger` is obsolete and unused by `Mediator`.

### Handling validation failures in your API

Validation does **not** throw. Check `Result` after `Send`:

```csharp
var result = await mediator.Send(new CreateUserCommand());
if (result.IsFailure &&
    ValidationErrors.TryGet(result.Error, out var fieldErrors))
{
    // result.Error.Code == ValidationBehaviorConstants.ValidationFailureErrorCode
    // fieldErrors: PropertyName + ErrorMessage
    // map to 400 / ApiEnvelope
}
```

---

## End-to-end ASP.NET Core example

```csharp
builder.Services.AddLogging();
builder.Services.AddMediatorService(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
    options.RegisterServicesFromAssembly(typeof(CreateUserHandler).Assembly);

    options.RegisterValidationBehavior = true;
    options.RegisterTimeoutBehavior = true;
    options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(15);

    options.RegisterPrePostProcessorBehavior = true;
    options.AddRequestPreProcessor(typeof(CorrelationPreProcessor<>));

    options.RegisterLoggingBehavior = true; // keep log level Information in prod
});

// In a minimal API / controller:
app.MapPost("/users", async (CreateUserCommand cmd, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(cmd, ct);
    if (result.IsFailure && ValidationErrors.TryGet(result.Error, out var errors))
        return Results.BadRequest(errors);

    if (result.IsFailure)
        return Results.Problem(result.Error.Message);

    return Results.Ok(result.Value);
});
```

---

## Benchmarks

| Method | Mean | Allocated |
|--------|------|-----------|
| SamSoft_Send_Command | ~390 ns | ~376 B |
| MediatR_Send_Command | ~424 ns | ~336 B |

Figures from the in-repo BenchmarkDotNet project; re-run after upgrades for current numbers.

---

## CI / CD

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| [CI](.github/workflows/ci.yml) | Push / PR to `main` | Restore, build, test (net8/9/10), pack (Ubuntu + Windows) |
| [CD](.github/workflows/cd.yml) | Tag `v*.*.*`, GitHub Release, or manual | Test, pack, publish to NuGet.org, attach assets to the GitHub Release |

### Publish a release

```bash
# 1. Bump Version in SamSoft.Mediator.CQRS.csproj (optional if CD overrides from tag)
# 2. Commit, then tag and push:
git tag v1.6.0
git push origin v1.6.0
```

### One-time NuGet setup (Trusted Publishing)

1. On [nuget.org](https://www.nuget.org/) → account → **Trusted Publishing**, add a policy:
   - Repository owner: `hakimsameh`
   - Repository: `SamSoft.Mediator.CQRS`
   - Workflow file: `cd.yml`
2. In GitHub → **Settings → Secrets and variables → Actions**, add:
   - `NUGET_USER` — your nuget.org **username** (profile name, not email)

Alternatively, set `NUGET_API_KEY` (classic API key). CD uses Trusted Publishing when available, then falls back to `NUGET_API_KEY`.

Manual dry-run (pack only): Actions → **CD** → **Run workflow** → leave **dry_run** checked.

---

## License

MIT — see [LICENSE.txt](SamSoft.Mediator.CQRS/LICENSE.txt).

Contact: [hakimsameh70@gmail.com](mailto:hakimsameh70@gmail.com)
