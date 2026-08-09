namespace SamSoft.Mediator.CQRS.Tests;

/// <summary>
/// Convenience access to the xUnit v3 test cancellation token (xUnit1051).
/// </summary>
internal static class TestCancel
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens)
    {
        var all = new CancellationToken[tokens.Length + 1];
        all[0] = Token;
        tokens.CopyTo(all, 1);
        return CancellationTokenSource.CreateLinkedTokenSource(all);
    }
}
