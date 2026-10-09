using System.Net;

namespace Wooly.Core.Errors;

/// <summary>
///     An instance was reached and failed to answer — a <c>5xx</c>: down for a moment, overloaded, or behind a proxy that
///     lost it. Distinct from <see cref="AttachmentRefusedException" />, which is the instance declining what it was
///     sent, because this says nothing about what was sent: the same call made again later may well go through.
/// </summary>
/// <remarks>
///     Raised where a front end has something to offer on it — the TUI's pending attachments, whose row offers a retry
///     for it (review of #372) — rather than for every call, most of which leave a bare failure to be reported as the
///     defect it would be anywhere a retry is not the author's to ask for.
/// </remarks>
/// <param name="instance">The instance that failed, e.g. <c>mastodon.social</c>.</param>
/// <param name="status">What it answered with.</param>
public sealed class InstanceFailedException(string instance, HttpStatusCode status)
    : WoolyException($"{instance} failed to answer ({(int)status}).")
{
    /// <summary>What the instance answered with.</summary>
    public HttpStatusCode Status { get; } = status;
}
