namespace SamSoft.Mediator.CQRS.Logging;

/// <summary>
/// Console adapter for the obsolete <see cref="Abstractions.IMediatorLogger"/> interface.
/// </summary>
[Obsolete("Prefer ILogger<T> pipeline behaviors. Mediator no longer consumes IMediatorLogger.")]
public sealed class ConsoleMediatorLogger : Abstractions.IMediatorLogger
{
    public void LogInformation(string message) =>
        Console.WriteLine($"[INFO] {message}");

    public void LogError(string message, Exception ex) =>
        Console.WriteLine($"[ERROR] {message}: {ex}");
}
