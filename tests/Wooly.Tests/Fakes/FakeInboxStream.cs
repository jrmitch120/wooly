using Wooly.Core.Inbox;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     The user stream without the instance (#401). Every connection a front end opens is kept, so a test can say what
///     comes over it — it came up, a notification or a conversation arrived, it dropped — or that the instance would
///     not stream at all. Unless a test says otherwise, a connection never comes up and never drops: it hangs, the way a
///     stream that will not answer does, which leaves the shell polling as it did before there was a stream.
/// </summary>
internal sealed class FakeInboxStream : IInboxStream
{
    /// <summary>Every connection asked for, in order, the open one last.</summary>
    public List<Listening> Listens { get; } = [];

    /// <summary>Whether a connection asked for comes up at once, rather than when a test says so.</summary>
    public bool ConnectsAtOnce { get; set; }

    /// <summary>Whether a connection asked for fails at once, as an instance that will not stream does.</summary>
    public bool Refuses { get; set; }

    /// <summary>The last connection asked for.</summary>
    public Listening Latest => Listens[^1];

    public Task Listen(ActiveProfile profile, Action<InboxEvent> heard, CancellationToken cancellationToken)
    {
        var listening = new Listening(profile, heard, cancellationToken);

        Listens.Add(listening);

        if (Refuses)
        {
            listening.Refuse();
        }
        else if (ConnectsAtOnce)
        {
            listening.Connect();
        }

        return listening.Ended;
    }

    /// <summary>The last connection comes up.</summary>
    public void Connect() => Latest.Connect();

    /// <summary>A notification of <paramref name="kind" /> arrives on the last connection.</summary>
    public void Notify(NotificationKind kind) => Latest.Hear(new InboxEvent.Notified(kind));

    /// <summary>A conversation arrives or changes on the last connection.</summary>
    public void Converse() => Latest.Hear(new InboxEvent.Conversed());

    /// <summary>The last connection drops.</summary>
    public void Drop() => Latest.Drop();

    /// <summary>One connection: whose it is, and what a test can make it say.</summary>
    internal sealed class Listening
    {
        private readonly Action<InboxEvent> _heard;
        private readonly TaskCompletionSource _ended = new();

        public Listening(ActiveProfile profile, Action<InboxEvent> heard, CancellationToken cancellationToken)
        {
            Profile = profile;
            _heard = heard;
            Token = cancellationToken;

            cancellationToken.Register(() => _ended.TrySetCanceled(cancellationToken));
        }

        /// <summary>Who it was opened as.</summary>
        public ActiveProfile Profile { get; }

        /// <summary>What closes it.</summary>
        public CancellationToken Token { get; }

        /// <summary>Whether whoever opened it has closed it.</summary>
        public bool Closed => Token.IsCancellationRequested;

        /// <summary>Ends when the connection does.</summary>
        public Task Ended => _ended.Task;

        public void Connect() => Hear(new InboxEvent.Connected());

        public void Hear(InboxEvent heard) => _heard(heard);

        public void Drop() => _ended.TrySetResult();

        public void Refuse() =>
            _ended.TrySetException(new HttpRequestException("The instance would not stream."));
    }
}
