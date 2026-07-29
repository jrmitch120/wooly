namespace Wooly.Core.Errors;

/// <summary>
///     A failure the user is meant to read, not debug: the message is written to stderr as-is, with no stack trace.
///     Anything thrown that is <em>not</em> a <see cref="WoolyException" /> is treated as a defect in this client
///     rather than an expected outcome, so front ends can present the two differently.
/// </summary>
public abstract class WoolyException : Exception
{
    protected WoolyException(string message)
        : base(message)
    {
    }

    protected WoolyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
