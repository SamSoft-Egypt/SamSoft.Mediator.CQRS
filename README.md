# SamSoft.Mediator.CQRS

A high-performance .NET CQRS mediator inspired by MediatR, with Result-typed commands/queries, pipeline behaviors, and Microsoft.Extensions.DependencyInjection integration.

**Current version:** 1.4.0 · **Targets:** `net8.0;net9.0;net10.0` · **License:** MIT

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

## Quick start

```csharp
services.AddMediatorService(options =>
{
    options.Lifetime = ServiceLifetime.Scoped; // Scoped | Singleton | Transient
    options.RegisterServicesFromAssembly(typeof(MyHandler).Assembly);

    // Optional built-ins
    options.RegisterTimeoutBehavior = true;
    options.RegisterPrePostProcessorBehavior = true;
    options.RegisterValidationBehavior = true; // commands + queries → Result.Failure on invalid input
    options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(10);

    // Custom / additional open-generic behaviors
    options.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
});
```

Convenience overload (scans assemblies and enables timeout + pre/post behaviors):

```csharp
services.AddMediatorService(typeof(MyHandler).Assembly);
```

`AddMediatorCQRS(...)` remains available as a compatibility alias that forwards to `AddMediatorService`.

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
| `Parallel` (default) | Handlers run concurrently via `Task.WhenAll` |
| `Sequential` | Handlers run one-by-one; stops on first exception |

Override per notification with `[NotificationPublishStrategy(...)]`, or set `options.DefaultNotificationPublishStrategy`.

---

## Pipeline behaviors

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
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

Register open generics with:

```csharp
services.AddOpenBehavior(typeof(LoggingBehavior<,>));
// or
options.AddOpenBehavior(typeof(LoggingBehavior<,>));
```

### Built-in behaviors

| Behavior | Notes |
|----------|--------|
| `ValidationBehavior<,>` | FluentValidation for **commands and queries**; failures return `Result.Failure` (`Error.Validation`) with `Error.Metadata` as `PropertyName → string[]` — not thrown. |
| `TimeoutBehavior<,>` | Cancels the handler via linked `CancellationTokenSource` when `TimeoutSettings` elapses |
| `PrePostProcessorBehavior<,>` | Runs `IRequestPreProcessor<>` / `IRequestPostProcessor<,>` |
| `LoggingPipelineBehavior<,>` / `AdvancedLoggingBehavior<,>` | `ILogger`-based logging |

Prefer `Microsoft.Extensions.Logging.ILogger<T>` for logging. `IMediatorLogger` is obsolete and is not used by `Mediator`.

```csharp
var result = await mediator.Send(new CreateUserCommand());
if (result.IsFailure &&
    ValidationErrors.TryGet(result.Error, out var fieldErrors))
{
    // result.Error.Code == ValidationBehaviorConstants.ValidationFailureErrorCode
    // fieldErrors: PropertyName + ErrorMessage (from Error.Metadata)
}
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
| [CI](.github/workflows/ci.yml) | Push / PR to `main` | Restore, build, test, pack (Ubuntu + Windows) |
| [CD](.github/workflows/cd.yml) | Tag `v*.*.*`, GitHub Release, or manual | Test, pack, publish to NuGet.org, attach assets to the GitHub Release |

### Publish a release

```bash
# 1. Bump Version in SamSoft.Mediator.CQRS.csproj (optional if CD overrides from tag)
# 2. Commit, then tag and push:
git tag v1.4.0
git push origin v1.4.0
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
