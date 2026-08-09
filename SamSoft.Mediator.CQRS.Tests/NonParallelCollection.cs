namespace SamSoft.Mediator.CQRS.Tests;

/// <summary>
/// Disables parallel execution for tests that share static trackers or mutable handler state.
/// </summary>
[CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
public sealed class NonParallelCollection;
