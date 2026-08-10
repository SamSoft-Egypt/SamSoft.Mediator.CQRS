// See https://aka.ms/new-console-template for more information
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Query;

public record MyQuery(string Name) : IQuery<string>;