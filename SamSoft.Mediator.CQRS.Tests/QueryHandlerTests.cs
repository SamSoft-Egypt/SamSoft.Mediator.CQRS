using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

public class QueryHandlerTests
{
    [Fact]
    public async Task MyQueryHandler_Returns_Success_For_Valid_Id()
    {
        var services = new ServiceCollection();
        services.AddMediatorService();
        services.AddTransient<IQueryHandler<MyQuery, string>, MyQueryHandler>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new MyQuery { Id = 1 };
        var result = await mediator.Send(query, TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Value for 1", result.Value);
    }

    [Fact]
    public async Task MyQueryHandler_Returns_Failure_For_Invalid_Id()
    {
        var services = new ServiceCollection();
        services.AddMediatorService();
        services.AddTransient<IQueryHandler<MyQuery, string>, MyQueryHandler>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new MyQuery { Id = 0 };
        var result = await mediator.Send(query, TestCancel.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid Id", result.Error.Message);
    }
}
