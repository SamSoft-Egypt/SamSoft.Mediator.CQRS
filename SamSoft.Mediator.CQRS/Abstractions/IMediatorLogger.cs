namespace SamSoft.Mediator.CQRS.Abstractions;

/// <summary>
/// Optional console-oriented logger used by older samples.
/// Prefer pipeline behaviors backed by <c>Microsoft.Extensions.Logging.ILogger{T}</c>.
/// </summary>
[Obsolete("Prefer ILogger<T> pipeline behaviors (e.g. AdvancedLoggingBehavior). This interface is unused by Mediator.")]
public interface IMediatorLogger
{
    void LogInformation(string message);
    void LogError(string message, Exception ex);
}
