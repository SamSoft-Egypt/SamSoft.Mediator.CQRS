using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class TimeoutBehaviorAdvancedTests
{
    [Fact]
    public async Task FastCommand_WithinTimeout_Succeeds()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(2);
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new FastCommand(), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("fast", result.Value);
    }

    [Fact]
    public async Task CallerCancellation_ThrowsOperationCanceled_NotTimeoutException()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(30);
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var linked = TestCancel.CreateLinkedTokenSource(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sp.GetRequiredService<IMediator>().Send(new CancelableCommand(), linked.Token));
    }
}
