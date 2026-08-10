using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Command;

public record CheckNameCommand(string Name, string Encoded) : ICommand;
