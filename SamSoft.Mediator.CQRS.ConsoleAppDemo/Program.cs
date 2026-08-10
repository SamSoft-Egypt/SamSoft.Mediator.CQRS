using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.ConsoleAppDemo;
using SamSoft.Mediator.CQRS.ConsoleAppDemo.Command;
using SamSoft.Mediator.CQRS.ConsoleAppDemo.Query;

Console.WriteLine("Hello, World!");
var services = MyRequestHandlerExtensions.CreateServices();

var sender = services.GetService<ISender>();
Console.WriteLine("Write Name:");
var name = Console.ReadLine();
var result = await sender!.Send(new MyQuery(name!));
var encodedName = string.Empty;
var encodedResult = await sender.Send(new EncodeCommand(name!));
if (encodedResult.IsSuccess)
{
    encodedName = encodedResult.Value;
}

if (result.IsSuccess)
{
    Console.WriteLine($"Request handled successfully: {result.Value}");
}
else
{
    Console.WriteLine($"Request failed: {result.Error.Message}");
}
var checkNameResult = await sender.Send(new CheckNameCommand(name!, encodedName));
if (checkNameResult.IsSuccess)
{
    Console.WriteLine("Check name succeeded.");
}
else
{
    Console.WriteLine($"Check name failed: {checkNameResult.Error.Message}");
}