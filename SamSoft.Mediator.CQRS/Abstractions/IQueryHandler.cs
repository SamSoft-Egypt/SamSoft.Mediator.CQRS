namespace SamSoft.Mediator.CQRS.Abstractions;

///// <summary>
///// Handler for a query that returns a value.
///// </summary>
///// <typeparam name="TQuery">The type of the query.</typeparam>
///// <typeparam name="TResponse">The type of the response.</typeparam>
//public interface IQueryHandler<TQuery, TResponse>
//    where TQuery : IQuery<TResponse>
//{
//    /// <summary>
//    /// Handles the query and returns a value.
//    /// </summary>
//    /// <param name="query">The query instance.</param>
//    /// <param name="cancellationToken">A cancellation token.</param>
//    /// <returns>A <see cref="Result{TResponse}"/> representing the outcome and value.</returns>
//    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken = default);
//}

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandlerBase<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;