using System.Net;

namespace Wooly.Core.Errors;

/// <summary>
///     An instance was reached and failed to answer — a <c>5xx</c>: down for a moment, overloaded, or behind a proxy that
///     lost it. Distinct from <see cref="AttachmentRefusedException" />, which is the instance declining what it was
///     sent, because this says nothing about what was sent: the same call made again later may well go through.
/// </summary>
/// <remarks>
///     Raised where a front end has something to offer on it — the TUI's pending attachments, whose row offers a retry
///     for it (review of #372) — and by the raw reads #598 added, search, trends and the link timeline, so that each says
///     a failure to answer as the TUI's enquiry can say it, and the same way as the other two (#598's review). Not for
///     every call: most leave a bare failure to be reported as the defect it would be anywhere a retry is not the
///     author's to ask for.
/// </remarks>
/// <param name="instance">The instance that failed, e.g. <c>mastodon.social</c>.</param>
/// <param name="status">What it answered with.</param>
/// <param name="reason">
///     What the instance said of it, where it said anything readable — a translator out of quota or rate-limited is a
///     <c>503</c> whose words are the only place the reader learns which (#614).
/// </param>
public sealed class InstanceFailedException(string instance, HttpStatusCode status, string? reason = null)
    : WoolyException(reason is null
        ? $"{instance} failed to answer ({(int)status})."
        : $"{instance} failed to answer ({(int)status}): {reason}")
{
    /// <summary>What the instance answered with.</summary>
    public HttpStatusCode Status { get; } = status;

    /// <summary>What the instance said of it, in its own words, or nothing where it said nothing readable.</summary>
    public string? Reason { get; } = reason;
}
