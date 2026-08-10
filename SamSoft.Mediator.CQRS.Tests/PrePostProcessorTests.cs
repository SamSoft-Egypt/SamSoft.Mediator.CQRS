using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;
public record PrePostTestCommand() : ICommand<string>;

public class PrePostTestHandler : ICommandHandler<PrePostTestCommand, string>
{
    public Task<Result<string>> Handle(PrePostTestCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success("ok"));
}

[Collection(nameof(NonParallelCollection))]
public class PrePostProcessorTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IRequestPreProcessor<>), typeof(TestPreProcessor<>));
        services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(TestPostProcessor<,>));
        services.AddMediatorService(assemblies: [typeof(PrePostTestHandler).Assembly]);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PreAndPostProcessors_AreCalled()
    {
        //TestPreProcessor<PrePostTestCommand>.WasCalled = false;
        //TestPostProcessor<PrePostTestCommand, Result<string>>.WasCalled = false;
        PreAndPostProcessorTracker.PostProcessorWasCalled = false;
        PreAndPostProcessorTracker.PreProcessorWasCalled = false;
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        var result = await mediator.Send(new PrePostTestCommand(), TestCancel.Token);

        Assert.True(PreAndPostProcessorTracker.PostProcessorWasCalled);
        Assert.True(PreAndPostProcessorTracker.PreProcessorWasCalled);
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }
}