using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     The calls a compose screen's pending attachments make to the instance (ADR-0026): each file sent up as it is
///     attached (#375), and each description sent once there is an attachment to put it on (#377) — every answer handed
///     back on the drawing thread, for the shell to feed into the screen, which holds what it is told and reaches nothing
///     itself (ADR-0015).
/// </summary>
/// <remarks>
///     Not through the enquiry: an upload goes on while the author writes, and its progress, its processing and any
///     refusal are the row's to say rather than the status row's — nor does it land only while the screen it was sent
///     from is on top, since a screen pushed over the draft does not stop it. Never retried here (ADR-0006): a dropped
///     connection is said on the row, and trying again is the author's. Called off with the screen (<see cref="LetGo" />).
///     <para>
///         <strong>Every call ends in an answer.</strong> Whatever a call fails with — a refusal, a dropped connection,
///         a client that gave up waiting, or something nothing here expected — ends as a refused row saying what it was,
///         or as a description said to have failed, and never as a row left going up or a send left waiting on one
///         (review of #372). Only a call this called off itself ends in nothing, its screen having gone.
///     </para>
/// </remarks>
/// <param name="author">Where a file is sent up and described.</param>
/// <param name="host">The drawing thread, which every answer is handed back on.</param>
internal sealed class AttachmentCalls(IPostAuthor author, IShellHost host)
{
    /// <summary>
    ///     What calls off the calls each compose screen still has going, called when the screen leaves the stack — what
    ///     was sent already is left for the instance to clear away.
    /// </summary>
    private readonly Dictionary<ComposeScreen, CancellationTokenSource> _going = [];

    /// <summary>The pending attachments whose description is on its way to the instance (#377).</summary>
    private readonly HashSet<ComposeAttachment> _describing = [];

    /// <summary>Where an attachment has got to on its way up, heard on the drawing thread.</summary>
    public event Action<ComposeScreen, ComposeAttachment, AttachmentState>? Heard;

    /// <summary>The instance took a description for an attachment, heard on the drawing thread: the one it took.</summary>
    public event Action<ComposeScreen, ComposeAttachment, string>? Told;

    /// <summary>A description did not reach the instance, heard on the drawing thread: why, in words to say.</summary>
    public event Action<ComposeScreen, ComposeAttachment, string>? Untold;

    /// <summary>
    ///     Sends <paramref name="attachment" /> up as <paramref name="profile" />, telling <see cref="Heard" /> where it
    ///     has got to as it hears, and how it ended.
    /// </summary>
    public async Task SendUp(ActiveProfile profile, ComposeScreen compose, ComposeAttachment attachment)
    {
        if (!_going.TryGetValue(compose, out var going))
        {
            _going[compose] = going = new CancellationTokenSource();
        }

        var token = going.Token;
        var progress = new Reporting(this, compose, attachment);
        AttachmentState landed;

        try
        {
            landed = new AttachmentState.Ready(await author.Attach(profile, attachment.Path, progress, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception failed)
        {
            landed = Refusal(failed);
        }

        Hear(compose, attachment, landed);
    }

    /// <summary>
    ///     Sends <paramref name="attachment" />'s description to the instance as <paramref name="profile" />, where it is
    ///     there to put it on, holds another than the instance has, and has none on its way already — one at a time for
    ///     each attachment, so that the last one written is the last one sent. <see cref="Told" /> or
    ///     <see cref="Untold" /> says how it went.
    /// </summary>
    public async Task Describe(ActiveProfile profile, ComposeScreen compose, ComposeAttachment attachment)
    {
        if (attachment is not { Untold: true, State: AttachmentState.Ready(var pending) }
            || !_going.TryGetValue(compose, out var going)
            || !_describing.Add(attachment))
        {
            return;
        }

        var token = going.Token;
        var description = attachment.Saying;

        try
        {
            await author.Describe(profile, pending.Id, description, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            host.OnUiThread(() => _describing.Remove(attachment));

            return;
        }
        catch (Exception failed)
        {
            var why = failed is AttachmentRefusedException { Reason: { } reason } ? reason : failed.Message;

            host.OnUiThread(() =>
            {
                _describing.Remove(attachment);
                Untold?.Invoke(compose, attachment, why);
            });

            return;
        }

        host.OnUiThread(() =>
        {
            _describing.Remove(attachment);
            Told?.Invoke(compose, attachment, description);
        });
    }

    /// <summary>Calls off whatever <paramref name="compose" /> still has going, as it leaves the stack (#375).</summary>
    public void LetGo(ComposeScreen compose)
    {
        if (_going.Remove(compose, out var going))
        {
            going.Cancel();
            going.Dispose();
        }
    }

    /// <summary>Tells <see cref="Heard" /> where <paramref name="attachment" /> has got to, on the drawing thread.</summary>
    private void Hear(ComposeScreen compose, ComposeAttachment attachment, AttachmentState state) =>
        host.OnUiThread(() => Heard?.Invoke(compose, attachment, state));

    /// <summary>
    ///     What a row says of an upload that failed with <paramref name="failed" />, and whether it offers a retry — only
    ///     where sending it again could mend it (#378): a dropped connection, a rate limit, which passes, an instance
    ///     that failed to answer, or a call the client gave up waiting on. A file the instance refused, and anything
    ///     nothing here expected, are said as they are with none.
    /// </summary>
    private static AttachmentState.Refused Refusal(Exception failed) => failed switch
    {
        AttachmentRefusedException refused => new AttachmentState.Refused(refused.Reason ?? "refused by the instance"),
        TransientNetworkException => new AttachmentState.Refused("connection lost", Retryable: true),
        RateLimitedException => new AttachmentState.Refused("rate limited", Retryable: true),
        InstanceFailedException instance => new AttachmentState.Refused(
            $"instance failed ({(int)instance.Status})",
            Retryable: true),
        OperationCanceledException => new AttachmentState.Refused("timed out", Retryable: true),
        _ => new AttachmentState.Refused(failed.Message),
    };

    /// <summary>
    ///     An upload's progress, handed onto the drawing thread as it is reported from wherever the HTTP stack is
    ///     (<see cref="IShellHost.OnUiThread" />) — not <see cref="Progress{T}" />, which posts to a synchronisation
    ///     context a terminal does not have.
    /// </summary>
    private sealed class Reporting(AttachmentCalls calls, ComposeScreen compose, ComposeAttachment attachment)
        : IProgress<AttachmentProgress>
    {
        public void Report(AttachmentProgress value)
        {
            AttachmentState state = value is AttachmentProgress.Sending(var done)
                ? new AttachmentState.Sending(done)
                : new AttachmentState.Processing();

            calls.Hear(compose, attachment, state);
        }
    }
}
