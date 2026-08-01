namespace Wooly.Core.Http;

/// <summary>
///     How much of a profile's API budget an instance says is left, as of the last call made to it. What story 54's
///     indicator draws, and what makes an interactive client's spending visible before a
///     <see cref="Errors.RateLimitedException" /> makes it obvious.
/// </summary>
/// <param name="Remaining">How many more calls the instance will take before the window resets.</param>
/// <param name="Limit">How many it allows in a window, which is what makes <paramref name="Remaining" /> a fraction.</param>
/// <param name="ResetsAt">
///     When the window rolls over and the budget goes back to <paramref name="Limit" />, or <see langword="null" />
///     where the instance did not say.
/// </param>
public sealed record RateLimitQuota(int Remaining, int Limit, DateTimeOffset? ResetsAt)
{
    /// <summary>What fraction of the budget is left, between 0 and 1, for anything drawing this as a proportion.</summary>
    /// <remarks>
    ///     A limit of zero is an instance saying something this client cannot divide by; it is reported as nothing left
    ///     rather than as an error, because a quota indicator is not a place to raise one.
    /// </remarks>
    public double Fraction => Limit <= 0 ? 0 : Math.Clamp((double)Remaining / Limit, 0, 1);
}
