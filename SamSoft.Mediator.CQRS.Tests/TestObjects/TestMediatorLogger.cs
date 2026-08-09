#pragma warning disable CS0618 // IMediatorLogger is obsolete — kept for compatibility samples
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.Tests.TestObjects;

public sealed class TestMediatorLogger : IMediatorLogger
{
    public List<string> Infos { get; } = [];
    public List<string> Errors { get; } = [];

    public void LogInformation(string message) => Infos.Add(message);

    public void LogError(string message, Exception ex) =>
        Errors.Add($"{message}: {ex.Message}");
}
#pragma warning restore CS0618
