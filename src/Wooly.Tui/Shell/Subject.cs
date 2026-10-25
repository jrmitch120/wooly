using Wooly.Core.Accounts;
using Wooly.Core.Discovery;
using Wooly.Core.Errors;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;
using Entry = Wooly.Tui.Shell.Destination;
using Person = Wooly.Core.Accounts.Account;

namespace Wooly.Tui.Shell;

/// <summary>
///     What a screen that is read is read from: a destination on the rail, a post's thread, an account, one side of an
///     account's follows, a notification group's actors, a hashtag walked to, or a conversation (GLOSSARY.md, #233).
/// </summary>
/// <remarks>
///     A closed set of values, each of which carries its own read and builds its own screen, and says whether what it
///     read is held for a while and how it reads on. Everything else about bringing a screen up — the stale rule, the
///     cache, the placeholder, the rail's badge, when to read on — is <see cref="Arrival" />'s, and is the same for all
///     seven.
///     <para>
///         Compared by value, which is what the cache is keyed by and what <c>g</c> asks again: two follow lists of the
///         same side of the same account are the same subject however the account was last answered about, and a rail
///         destination is the same subject whatever its badge says.
///     </para>
/// </remarks>
public abstract record Subject
{
    /// <summary>
    ///     How many people a follow list asks for at a time: the most Mastodon serves from a list of accounts in one
    ///     call, so the most there is any point asking it for (#180).
    /// </summary>
    private const int FollowsPage = 80;

    /// <summary>
    ///     How many of a notification group's actors the actors screen asks for at a time: the most Mastodon serves from
    ///     a group's accounts in one call (#455).
    /// </summary>
    private const int ActorsPage = 80;

    private Subject()
    {
    }

    /// <summary>
    ///     Whether what it read is held for <see cref="ShellTiming.CacheFor" />, so that coming back to it inside that
    ///     costs no fetch. The rail's destinations and follow lists, and nothing else (ADR-0014, #180).
    /// </summary>
    public virtual bool Cached => false;

    /// <summary>Reads it, given the ports, the profile and the enquiry the reading is put under.</summary>
    /// <param name="ask">What every call it makes is put through.</param>
    /// <param name="ports">What it is read through.</param>
    /// <param name="profile">Who is asking.</param>
    /// <param name="standing">
    ///     The screen already up for it where one is — its placeholder, or the screen the rest of a held list is read
    ///     into — which a subject read into a screen rather than into a new one fills.
    /// </param>
    internal abstract Task<Found> Read(Enquiry.Ask ask, ShellPorts ports, ActiveProfile profile, Screen? standing);

    /// <summary>
    ///     <b>Reads on</b> from <paramref name="from" />: what stands behind the last thing its screen holds, as an answer
    ///     that adds it to that screen (#469). Asked only of a subject whose screen carries a
    ///     <see cref="Screens.ReadingOn" />, which only an answer that reads on gives it.
    /// </summary>
    /// <param name="ask">What every call it makes is put through.</param>
    /// <param name="ports">What it is read through.</param>
    /// <param name="profile">Who is asking.</param>
    /// <param name="standing">
    ///     The screen being read on, which a subject that reads on through what its screen holds reads on from — an
    ///     account's, whose posts are asked for by the id it was answered with (#464).
    /// </param>
    /// <param name="from">Where the instance said to read on from.</param>
    internal virtual Task<Found> ReadOn(
        Enquiry.Ask ask,
        ShellPorts ports,
        ActiveProfile profile,
        Screen standing,
        Onward from) =>
        throw new InvalidOperationException($"{GetType().Name} does not read on, and its screen was never told to.");

    /// <summary>
    ///     The screen that stands for it at once on <paramref name="move" />, before anything has been read — or
    ///     <see langword="null" /> where its screen appears only when its answer lands.
    /// </summary>
    internal virtual Screen? Placeholder(Move move, ActiveProfile profile) => null;

    /// <summary>
    ///     One of the rail's destinations that reads a list: the four timelines, notifications, direct messages,
    ///     follow requests and Discover.
    /// </summary>
    /// <remarks>
    ///     Told apart by <see cref="Kind" /> alone. The rail's own entry is what it is read and drawn with — the
    ///     timeline a hashtag destination reads, the label its crumb says — and is carried where the rail is arriving,
    ///     but a badge moving changes the entry and not which destination it is; so the cache is keyed by the one and
    ///     a change made anywhere can forget a destination by naming its kind (#233).
    /// </remarks>
    public sealed record Destination : Subject
    {
        private readonly Entry? _entry;

        private readonly SessionMarkers? _sessionMarkers;

        /// <summary>The destination <paramref name="kind" /> names, which is enough to forget what it held.</summary>
        public Destination(DestinationKind kind) => Kind = kind;

        /// <summary>The rail's own entry, which is what is read and drawn.</summary>
        /// <param name="entry">The rail's own entry.</param>
        /// <param name="sessionMarkers">
        ///     Where an account with no read marker counts its notifications from this session, which arriving at
        ///     Notifications counts against as the badge did (story 8).
        /// </param>
        public Destination(Entry entry, SessionMarkers sessionMarkers)
            : this(entry.Kind)
        {
            _entry = entry;
            _sessionMarkers = sessionMarkers;
        }

        /// <summary>Which of the rail's ten it is.</summary>
        public DestinationKind Kind { get; }

        /// <inheritdoc />
        public override bool Cached => true;

        /// <summary>The rail's entry, which only a subject the rail arrived at carries.</summary>
        private Entry Entry => _entry ?? throw new InvalidOperationException(
            $"The {Kind} destination is read from the rail's own entry, which this subject was not given.");

        /// <summary>The session markers, which only a subject the rail arrived at carries.</summary>
        private SessionMarkers SessionMarkers => _sessionMarkers ?? throw new InvalidOperationException(
            $"The {Kind} destination is counted with the session's markers, which this subject was not given.");

        /// <inheritdoc />
        public bool Equals(Destination? other) => other is not null && other.Kind == Kind;

        /// <inheritdoc />
        public override int GetHashCode() => Kind.GetHashCode();

        /// <inheritdoc />
        internal override Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing) =>
            Table().Read(ask, ports, profile, Entry, token => new InboxCount(ports, profile, SessionMarkers, token));

        /// <inheritdoc />
        internal override Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen standing,
            Onward from) =>
            Table().ReadOn(ask, ports, profile, from);

        /// <inheritdoc />
        /// <remarks>
        ///     Only on arriving: what was on screen is about somewhere else, which is exactly what is not true of a
        ///     refresh — so there, what is showing stands until a fresher copy of it is ready to take its place, and a
        ///     refresh a rate limit or a failure ends is a notice over the list the reader was reading (#84).
        /// </remarks>
        internal override Screen? Placeholder(Move move, ActiveProfile profile) =>
            move == Move.Arrive ? Table().Empty() : null;

        /// <summary>
        ///     What this destination reads and what that becomes, which is the same table however the reader got
        ///     here (#100).
        /// </summary>
        private Listing Table()
        {
            var entry = Entry;

            // The four timeline destinations are one read with a different timeline in it, and which timeline that is
            // the entry already says — so there is one arm here rather than four saying the same thing about a
            // different scope.
            if (entry.Timeline is { } timeline)
            {
                return new Listing<Post>(
                    Reads: (ports, profile, token) =>
                        ports.Timelines.Read(profile, timeline, Arrival.PostsWanted, token),

                    // Refreshed, because this is the timeline as a destination arrived at: a tag walked to is the same
                    // screen and is not one, so which it is comes from who built it (#84).
                    Becomes: (posts, notice, _) => new FeedScreen(entry, posts, notice, refreshes: true),
                    WhenEmpty: Arrival.NothingOn(timeline),

                    // A timeline carries no badge, which is something this destination says rather than a step its
                    // arrival is missing.
                    Counting: null,
                    Carries: post => post,

                    // All four, the same way: a timeline is a timeline whichever scope is in it (#464).
                    ReadsOn: TimelineReadsOn(timeline));
            }

            return entry.Kind switch
            {
                DestinationKind.Notifications => new Listing<NotificationGroup>(
                    // Grouped as the instance groups them, never as this client would (ADR-0032).
                    Reads: (ports, profile, token) =>
                        ports.Notifications.ReadGroups(profile, Arrival.CountedAtMost, token),
                    Becomes: (waiting, notice, unread) => new NotificationsScreen(waiting, notice, unread),
                    WhenEmpty: "Nothing is waiting for you.",
                    // Counted apart from the list it is drawn beside: what is unread is what is newer than the read
                    // marker, which the list does not say (ADR-0027). Arriving reads them, so the count moves the
                    // marker too, and hands the screen the rows that were unread for this visit (#397).
                    Counting: (count, shown) => count.Arrived(shown),
                    Carries: group => group.Post,

                    // As many again as the first read, read the way it was: grouped, or each a group of one where the
                    // instance cannot group. Never counted: what is behind the groups held is behind the read marker
                    // too, so neither the badge nor the marker is anything reading on moves (#465, ADR-0027).
                    ReadsOn: new OnwardRead<NotificationGroup>((ports, profile, from, token) =>
                        ports.Notifications.ReadGroupsOn(profile, from, Arrival.CountedAtMost, token))),

                // The one destination whose read is two calls: the requests, and where the profile stands with each
                // person asking — inside this destination's own read, which is what makes a refresh re-ask for free,
                // and the same read a re-read of its count makes (#204).
                DestinationKind.Requests => new Listing<Person>(
                    Reads: InboxCount.Requests,
                    Becomes: (asking, notice, _) => new FollowRequestsScreen(asking, notice),
                    WhenEmpty: "Nobody is waiting to follow you.",
                    Counting: (count, asking) => count.Arrived(asking),
                    Carries: _ => null,
                    ReadsOn: new OnwardRead<Person>(InboxCount.RequestsOn, Recounts: InboxCount.OffListReadOn)),

                // The one destination that reads a list through a port with no Fetch on it: there is no paging here
                // and so nothing for a rate limit to stop part way — the read either answers or throws, and the
                // enquiry turns a throw into the shell's notice. Said as a complete fetch so that one table serves
                // this too.
                DestinationKind.Discover => new Listing<Suggestion>(
                    Reads: async (ports, profile, token) =>
                        Fetch<Suggestion>.Complete(
                            await ports.Suggestions.Read(profile, Arrival.CountedAtMost, token)),
                    Becomes: (suggested, notice, _) => new DiscoverScreen(suggested, notice),
                    WhenEmpty: "Nobody suggested.",

                    // No badge, and said here rather than left out: nothing on this screen is waiting for anybody,
                    // the same as Search.
                    Counting: null,
                    Carries: _ => null),

                DestinationKind.Messages => new Listing<Core.Conversations.Conversation>(
                    Reads: InboxCount.Conversations,
                    Becomes: (written, notice, _) => new DirectMessagesScreen(written, notice),
                    WhenEmpty: "No direct conversations yet.",

                    // The badge counts the conversations with something unread in them, and counts them off the list
                    // it is drawn beside — so the rail cannot say two over a list of one.
                    Counting: (count, written) => count.Arrived(written),
                    Carries: conversation => conversation.Latest,
                    ReadsOn: new OnwardRead<Core.Conversations.Conversation>(
                        InboxCount.ConversationsOn,
                        Recounts: InboxCount.OffListReadOn)),

                // Said out loud rather than quietly doing nothing: a destination that reads no list — the profile's
                // own account, the prompt, the hashtag nobody has named — arrives some other way, and landing here is
                // a destination nobody said what to do with.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(entry),
                    entry.Kind,
                    "Not a destination that reads a list."),
            };
        }
    }

    /// <summary>
    ///     A post and the thread around it — what it answers and what has been said in answer to it — asked about the
    ///     post itself where what is in hand is a boost of it, since a boost stands in the same thread as the post it
    ///     carries.
    /// </summary>
    /// <param name="Post">The post as it was picked, which is what the screen is about.</param>
    public sealed record Thread(Post Post) : Subject
    {
        /// <summary>The post the thread is read around, which is what tells two threads apart.</summary>
        private Post About => Post.Boosted ?? Post;

        /// <inheritdoc />
        public bool Equals(Thread? other) => other is not null && other.About.Id == About.Id;

        /// <inheritdoc />
        public override int GetHashCode() => About.Id.GetHashCode(StringComparison.Ordinal);

        /// <inheritdoc />
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var thread = await ask.Of(token => ports.Engagement.Thread(profile, About.Id, token));

            return new Found.Drawn(() => new PostScreen(Post, thread));
        }
    }

    /// <summary>
    ///     An account: who they are, what they have posted, what they have pinned, and which of the people the reader
    ///     follows follow them too.
    /// </summary>
    /// <param name="Address">Whose account.</param>
    /// <param name="WithReplies">
    ///     Whether their timeline is read with their replies in. Opening an account never asks for it — the
    ///     screen-reader reasoning in ADR-0019 is the default — and only <c>s</c>, and a <c>g</c> on the screen
    ///     <c>s</c> widened, do (#229).
    /// </param>
    public sealed record Account(AccountAddress Address, bool WithReplies) : Subject
    {
        /// <summary>
        ///     <paramref name="person" />'s account screen, opened as anybody is opened from a list of people: without
        ///     their replies, as <c>a</c> opens an author.
        /// </summary>
        public static Account Of(Core.Accounts.Account person) =>
            new(AccountAddress.Parse(person.Address), WithReplies: false);

        /// <inheritdoc />
        /// <remarks>
        ///     Four calls under one enquiry, so it is checked once at the end rather than after each: what matters is
        ///     whether the reader is still where they were when they asked, not how far the answer got.
        ///     <para>
        ///         The pinned run is read by naming the account rather than by marking the posts already in hand,
        ///         because an instance reports a post's own pin mark only to whoever wrote it — and it is read
        ///         <em>before</em> familiar followers, being content where the other is one decorative row, so it takes
        ///         the better odds against a rate limit (#182).
        ///     </para>
        ///     <para>
        ///         Familiar followers is asked <em>last</em>, and is the one of the four that answers rather than
        ///         throws where the instance refuses it: it decorates a single row, so a rate limit reached here leaves
        ///         the whole screen standing with that row missing rather than taking the account and its posts down
        ///         with it (ADR-0012's amendment).
        ///     </para>
        /// </remarks>
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var account = await ask.Of(token => ports.Accounts.Show(profile, Address, token));

            // The three reads that follow travel on the resolution this one just made rather than on the address it
            // was made from, which is what makes the arrival one lookup instead of three (ADR-0012's second
            // amendment). It is always the id Show answered with and never one the caller arrived holding: a refresh
            // is the one command meaning "check this is still true", so it must be the one command that can correct a
            // wrong id.
            var whose = NamedAccount.Resolved(account);

            var posts = await ask.Of(token =>
                ports.Timelines.Read(
                    profile,
                    WithReplies ? Timeline.WithReplies(whose) : Timeline.By(whose),
                    Arrival.PostsWanted,
                    token));

            var pinned = await ask.Of(token =>
                ports.Timelines.Read(profile, Timeline.Pinned(whose), Arrival.PostsWanted, token));

            var familiar = await ask.Of(token => ports.Accounts.FamiliarFollowers(profile, account.Id, token));

            // A rate limit that stopped this read is a question that went unput rather than an account with nothing
            // pinned, and the screen says which of the two it was. Nothing is salvaged from a stopped one: a pinned
            // run is a single page in the account's own order, so what a limit stops it holds none of, and a count
            // drawn over part of one would head a run the instance never finished listing.
            var pins = pinned.IsComplete ? pinned.Items : null;

            // Dropped from the timeline rather than from the pinned run, and dropped here rather than on the screen,
            // so the two lists reach it disjoint and it cannot disagree with itself about which run a post is in. A
            // recent pinned normal post can be in both (#182), and with replies read in, so can a recent pinned reply
            // (#229).
            var pinnedIds = (pins ?? []).Select(post => post.Id).ToHashSet(StringComparer.Ordinal);

            return new AccountRead(
                account,
                [.. posts.Items.Where(post => !pinnedIds.Contains(post.Id))],
                pins,
                familiar,
                WithReplies,
                profile.SignsInAs(account.Address),
                posts.Onward);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Their posts alone or their <b>Posts and replies</b>, whichever run the screen is showing, and never the
        ///     pinned run, which is one page in the account's own order (#464). Asked by the id the screen's account was
        ///     answered with, as the first read's three calls after <c>Show</c> are, so reading on costs no lookup of
        ///     its own (ADR-0012's second amendment).
        /// </remarks>
        internal override Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen standing,
            Onward from)
        {
            var whose = NamedAccount.Resolved(((AccountScreen)standing).Account);

            return TimelineReadsOn(WithReplies ? Timeline.WithReplies(whose) : Timeline.By(whose))
                .From(ask, ports, profile, from);
        }
    }

    /// <summary>
    ///     One side of an account's follows: a list held whole where it is short enough, and browsed by <b>reading
    ///     on</b> where it is not — which of the two settled off the counts the account already carries (#180, #467).
    /// </summary>
    /// <remarks>
    ///     Told apart by whose list it is and which side, since the account itself is answered about afresh every time
    ///     somebody reads it and a tie put on changes it — neither of which changes whose list this is.
    /// </remarks>
    /// <param name="Whose">
    ///     The account whose list it is, as the screen it was opened from was holding them — which is where both
    ///     counts come from, and so where the mode comes from.
    /// </param>
    /// <param name="Side">Which side of their follows.</param>
    public sealed record Follows(Person Whose, FollowSide Side) : Subject
    {
        /// <inheritdoc />
        /// <remarks>Only a list read whole and not cut short is ever handed back (see <c>Filled.Held</c>).</remarks>
        public override bool Cached => true;

        /// <inheritdoc />
        public bool Equals(Follows? other) => other is not null && other.Whose.Id == Whose.Id && other.Side == Side;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Whose.Id, Side);

        /// <inheritdoc />
        /// <remarks>
        ///     On every move: the reader is looking at the list they opened while it fills rather than at the screen
        ///     they left, and a refresh or a swap is a fresh screen — the filter gone and the pick back at the top.
        /// </remarks>
        internal override Screen? Placeholder(Move move, ActiveProfile profile) =>
            new FollowsScreen(Whose, Side, profile.SignsInAs(Whose.Address));

        /// <inheritdoc />
        /// <remarks>
        ///     The first page, on either kind of list. A browsed one goes on from there by reading on
        ///     (<see cref="ReadOn" />); a list held whole is read again for the rest of it in one ask rather than 24
        ///     more, re-read to a longer limit — so what comes back holds the page before it, and only the tail of it
        ///     is new.
        ///     <para>
        ///         Neither side of a follow list carries a standing, Mastodon sending one only from the relationship
        ///         endpoints, so it is asked for separately — once for the page rather than once a row. It answers
        ///         with nothing where it was refused, and the rows are drawn silent: a row saying there is no tie
        ///         because the asking failed would be the one dishonest thing on the screen.
        ///     </para>
        /// </remarks>
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var screen = (FollowsScreen)standing!;
            var already = screen.Read;
            var wanted = already == 0 ? FollowsPage : (int)screen.Total;

            // The screen is holding the account whose list this is, id and all, so the list is asked for by naming
            // it rather than by handing back the address it was read from and paying to arrive at the same id again.
            var fetch = await ask.Of(token => ports.Accounts.List(
                profile,
                Side,
                NamedAccount.Resolved(Whose),
                wanted,
                token));

            var read = fetch.Items.Skip(already).ToList();

            var stood = new List<Person>(read.Count);

            // A page of ids at a time, never one query naming everybody: the endpoint takes many ids and not
            // unboundedly many, and a held list is read to its whole length in one ask — so the two are chunked apart
            // rather than one following the other's size (#180).
            foreach (var page in read.Chunk(FollowsPage))
            {
                stood.AddRange(await ask.Of(token => ports.StoodOrSilent(profile, page, token)));
            }

            return new Filled([.. fetch.Items.Take(already), .. stood], fetch.StoppedBy, wanted, fetch.Onward);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Only a browsed list is asked: the next page from where the last read stopped, and where the reader stands
        ///     with whoever is on it — the people already on screen are never asked about again (#467).
        /// </remarks>
        internal override Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen standing,
            Onward from) =>
            PeopleReadOn((ports, profile, at, token) => ports.Accounts.ListOn(
                    profile,
                    Side,
                    NamedAccount.Resolved(Whose),
                    at,
                    FollowsPage,
                    token))
                .From(ask, ports, profile, from);

        /// <summary>
        ///     What one read of a follow list came back with: everyone the instance has listed so far, whether a rate
        ///     limit stopped it part way, how many it was asked for, and where to read on from.
        /// </summary>
        /// <param name="People">Everyone read so far, whoever is new among them carrying their standing.</param>
        /// <param name="StoppedBy">The rate limit that cut the read short, or nothing where none did.</param>
        /// <param name="Wanted">How many were asked for, which is what says whether the instance filled the ask.</param>
        /// <param name="Onward">Where a browsed list reads on from, or nothing where the instance has no more.</param>
        private sealed record Filled(
            IReadOnlyList<Person> People,
            RateLimitedException? StoppedBy,
            int Wanted,
            Onward? Onward)
            : Found
        {
            /// <inheritdoc />
            /// <remarks>
            ///     Read into the screen standing for the list, which is already up. A browsed list reads on from here,
            ///     and a held one never does: the rest of it is read at once instead (<see cref="ReadsRestAtOnce" />).
            /// </remarks>
            public override Screen Becomes(Screen? standing)
            {
                var screen = (FollowsScreen)standing!;

                screen.Arrived(
                    People,
                    More(screen),
                    Arrival.Emptiness(People.Count, screen.Nobody, of: null, StoppedBy));

                if (!screen.Holds)
                {
                    screen.ReadingOn = new ReadingOn(Onward);
                }

                return screen;
            }

            /// <inheritdoc />
            /// <remarks>
            ///     Only a list that was read whole is worth handing back later; a page of a browsed one is not what is
            ///     there.
            /// </remarks>
            public override Found? Held(Screen screen) =>
                screen is FollowsScreen { Holds: true } follows && StoppedBy is null && !More(follows) ? this : null;

            /// <inheritdoc />
            /// <remarks>
            ///     A list held whole goes on to read the rest at once, which is what streaming in behind the reader
            ///     is; a browsed one stops here and reads on as the page nears the end of what arrived.
            /// </remarks>
            public override bool ReadsRestAtOnce(Screen screen) =>
                screen is FollowsScreen { Holds: true } follows && More(follows);

            /// <inheritdoc />
            public override bool HoldsAccount(string accountId) => People.Any(person => person.Id == accountId);

            /// <summary>
            ///     Whether there is more of a held list to come: only where the instance filled the ask and the list is
            ///     longer than what is in hand. A short page is the end of the list, and a rate limit is the end of the
            ///     reading. A browsed list's is where it reads on from, which only the instance can say.
            /// </summary>
            private bool More(FollowsScreen screen) =>
                StoppedBy is null && People.Count >= Wanted && People.Count < screen.Total;
        }
    }

    /// <summary>
    ///     Every <b>Actor</b> in one notification group, read from the group's own accounts so that it is exactly the
    ///     group the row counted (#455). Read on as the page nears the end, the way a follow list too long to hold is
    ///     browsed (#467).
    /// </summary>
    /// <remarks>
    ///     Never held: a group grows as notifications arrive and shrinks as they are dismissed, so what was read a
    ///     minute ago is not worth handing back. Compared by the group's key alone, which is what the instance names it
    ///     by however many it held when its row was read.
    /// </remarks>
    /// <param name="Group">The group whose actors these are, as the Notifications screen was holding it.</param>
    public sealed record Actors(NotificationGroup Group) : Subject
    {
        /// <inheritdoc />
        public bool Equals(Actors? other) => other is not null && other.Group.Key == Group.Key;

        /// <inheritdoc />
        public override int GetHashCode() => Group.Key.GetHashCode(StringComparison.Ordinal);

        /// <inheritdoc />
        /// <remarks>On every move, as a follow list's: the reader is looking at the list they opened while it fills.</remarks>
        internal override Screen? Placeholder(Move move, ActiveProfile profile) => new ActorsScreen(Group);

        /// <inheritdoc />
        /// <remarks>
        ///     The first page of the group, and where the reader stands with everyone on it — once for the page, as a
        ///     follow list asks it. The rest is read on from where this stopped (<see cref="ReadOn" />).
        /// </remarks>
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var fetch = await ask.Of(token =>
                ports.Notifications.ReadGroupActors(profile, Group.Key, ActorsPage, token));
            var stood = await ask.Of(token => ports.StoodOrSilent(profile, fetch.Items, token));

            return new Filled(stood, fetch.StoppedBy, fetch.Onward);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     The next page from where the last read stopped, and where the reader stands with whoever is on it — the
        ///     actors already on screen are never asked about again (#467).
        /// </remarks>
        internal override Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen standing,
            Onward from) =>
            PeopleReadOn((ports, profile, at, token) =>
                    ports.Notifications.ReadGroupActorsOn(profile, Group.Key, at, ActorsPage, token))
                .From(ask, ports, profile, from);

        /// <summary>What the first read of a group's actors came back with, and where to read on from.</summary>
        /// <param name="People">Everyone read, each carrying their standing where the instance answered.</param>
        /// <param name="StoppedBy">The rate limit that cut the read short, or nothing where none did.</param>
        /// <param name="Onward">Where to read on from, or nothing where the instance has no more of the group.</param>
        private sealed record Filled(IReadOnlyList<Person> People, RateLimitedException? StoppedBy, Onward? Onward)
            : Found
        {
            /// <inheritdoc />
            /// <remarks>Read into the screen standing for the group, which is already up, and read on from there.</remarks>
            public override Screen Becomes(Screen? standing)
            {
                var screen = (ActorsScreen)standing!;

                screen.Arrived(People, Arrival.Emptiness(People.Count, ActorsScreen.Nobody, of: null, StoppedBy));
                screen.ReadingOn = new ReadingOn(Onward);

                return screen;
            }
        }
    }

    /// <summary>
    ///     A hashtag's timeline walked to from a search or a reference, as a screen on the stack — the rail's own
    ///     hashtag destination left alone, that being a setting the reader wrote down rather than something a keypress
    ///     changes.
    /// </summary>
    /// <param name="Hashtag">Which tag, as it was named.</param>
    public sealed record Tag(string Hashtag) : Subject
    {
        /// <inheritdoc />
        /// <remarks>
        ///     What an empty tag is told is the same sentence the rail's own timelines are told, and is said in the one
        ///     place.
        /// </remarks>
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var tag = Timeline.Tag(Hashtag);
            var posts = await ask.Of(token => ports.Timelines.Read(profile, tag, Arrival.PostsWanted, token));

            // A destination of its own rather than the rail's, so that the breadcrumb says which tag this is without
            // the rail's own hashtag entry changing under a reader who did not ask it to.
            var showing = new Entry(DestinationKind.Hashtag, $"#{Hashtag}", tag);

            return new Found.Drawn(() => new FeedScreen(
                showing,
                posts.Items,
                Arrival.Emptiness(posts.Items.Count, Arrival.NothingOn(tag), tag.Description, posts.StoppedBy))
            {
                // Read on as the rail's own hashtag is, being the same timeline walked to another way (#464).
                ReadingOn = new ReadingOn(posts.Onward),
            });
        }

        /// <inheritdoc />
        internal override Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen standing,
            Onward from) =>
            TimelineReadsOn(Timeline.Tag(Hashtag)).From(ask, ports, profile, from);
    }

    /// <summary>
    ///     One conversation and what was said in it — the thread its last post is in, oldest first. Named by the
    ///     conversation's own id, which is not the id of any post in it (GLOSSARY.md).
    /// </summary>
    /// <param name="Id">The conversation's own id.</param>
    public sealed record Conversation(string Id) : Subject
    {
        /// <inheritdoc />
        /// <remarks>
        ///     Reading one does not mark it read (ADR-0013): a client that cleared the mark on the way past would make
        ///     "what have I not read" unanswerable for anything that looked afterwards.
        /// </remarks>
        internal override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Screen? standing)
        {
            var thread = await ask.Of(token => ports.Messages.Show(profile, Id, token));

            return new Found.Drawn(() => new ConversationScreen(thread));
        }
    }

    /// <summary>
    ///     What a destination reads and what that becomes, as the four things that differ between them and nothing
    ///     else — with what the list is of left out of the reading, so that one table serves the five kinds of thing
    ///     a destination can hold.
    /// </summary>
    private abstract record Listing
    {
        /// <summary>What the destination is before anything has arrived, which an arrival puts up at once.</summary>
        public abstract Screen Empty();

        /// <summary>Reads it.</summary>
        /// <param name="counting">Its badge's count, under whichever token the enquiry puts that count with.</param>
        public abstract Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Entry entry,
            Func<CancellationToken, InboxCount> counting);

        /// <summary>Reads on from <paramref name="from" />, which only a destination that reads on is asked.</summary>
        public abstract Task<Found> ReadOn(Enquiry.Ask ask, ShellPorts ports, ActiveProfile profile, Onward from);
    }

    /// <summary>
    ///     How a list reads on (#469): what is asked for, and how its badge is counted again once what that brought is
    ///     under what the screen holds — which is the screen's own to put there (<see cref="IReadsOn{T}" />).
    /// </summary>
    /// <param name="Reads">What the instance is asked for, from where the last read said to read on from.</param>
    /// <param name="Recounts">
    ///     The destination's badge, counted again off the whole list its screen now holds and whether that is whole —
    ///     as every read of a destination counted off its list sets it (#466) — or <see langword="null" /> where the
    ///     badge is not counted off the list: a timeline carries none, and Notifications' is counted against the read
    ///     marker, which reading on never moves.
    /// </param>
    private sealed record OnwardRead<T>(
        Func<ShellPorts, ActiveProfile, Onward, CancellationToken, Task<Fetch<T>>> Reads,
        Func<IReadOnlyList<T>, bool, Found.Badge>? Recounts = null)
    {
        /// <summary>Reads on from <paramref name="from" />, as an answer that adds what came back to the screen asked from.</summary>
        public async Task<Found> From(Enquiry.Ask ask, ShellPorts ports, ActiveProfile profile, Onward from) =>
            new ReadOnto<T>(this, await ask.ReadOn(from, (at, token) => Reads(ports, profile, at, token)));
    }

    /// <summary>
    ///     How a list of people reads on (#467): what <paramref name="reads" /> brings, each carrying where the profile
    ///     stands with them — asked in the one batched call, and only of the people that read brought, never again of
    ///     those already on screen. The standing answers with nothing where it was refused, and the rows are drawn
    ///     silent, as on the first read.
    /// </summary>
    private static OnwardRead<Person> PeopleReadOn(
        Func<ShellPorts, ActiveProfile, Onward, CancellationToken, Task<Fetch<Person>>> reads) =>
        new(async (ports, profile, at, token) =>
        {
            var fetch = await reads(ports, profile, at, token);

            return fetch with { Items = await ports.StoodOrSilent(profile, fetch.Items, token) };
        });

    /// <summary>
    ///     How <paramref name="timeline" /> reads on: the rail's four timelines, a hashtag walked to and an account's
    ///     posts alike, whichever screen is showing it (#464).
    /// </summary>
    private static OnwardRead<Post> TimelineReadsOn(Timeline timeline) =>
        new((ports, profile, from, token) =>
            ports.Timelines.ReadOn(profile, timeline, from, Arrival.PostsWanted, token));

    /// <inheritdoc cref="Listing" />
    /// <param name="Reads">What the instance is asked for.</param>
    /// <param name="Becomes">
    ///     What the answer is on screen, given what came back, what there is to say about it, and which of it to draw
    ///     unread for this visit (<see cref="InboxCount.Tally.Unread" />).
    /// </param>
    /// <param name="WhenEmpty">What a reader is told where nothing came back.</param>
    /// <param name="Counting">
    ///     How this destination's badge is counted, given what the list read came back with — off that list, or by a
    ///     read of its own, as <see cref="InboxCount" /> says — or <see langword="null" /> where it carries no badge,
    ///     which a timeline says here rather than leaving the count out somewhere else.
    /// </param>
    /// <param name="Carries">
    ///     The post a row carries, or <see langword="null" /> where it carries none — which is what says whether a post
    ///     changing makes the list stale (#234).
    /// </param>
    /// <param name="ReadsOn">
    ///     How its list reads on, or <see langword="null" /> where it does not — which leaves its screen with nothing to
    ///     read on from, so it is never asked (#469).
    /// </param>
    private sealed record Listing<T>(
        Func<ShellPorts, ActiveProfile, CancellationToken, Task<Fetch<T>>> Reads,
        Func<IReadOnlyList<T>, string?, IReadOnlySet<string>, Screen> Becomes,
        string WhenEmpty,
        Func<InboxCount, IReadOnlyList<T>, Task<InboxCount.Tally>>? Counting,
        Func<T, Post?> Carries,
        OnwardRead<T>? ReadsOn = null) : Listing
    {
        /// <inheritdoc />
        public override Screen Empty() => Becomes([], null, InboxCount.Tally.NoneUnread);

        /// <inheritdoc />
        public override async Task<Found> Read(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Entry entry,
            Func<CancellationToken, InboxCount> counting)
        {
            var fetch = await ask.Of(token => Reads(ports, profile, token));

            return new Listed<T>(this, entry, fetch, await Counted(ask, counting, fetch.Items));
        }

        /// <inheritdoc />
        public override async Task<Found> ReadOn(
            Enquiry.Ask ask,
            ShellPorts ports,
            ActiveProfile profile,
            Onward from)
        {
            var reading = ReadsOn ?? throw new InvalidOperationException("This destination's list does not read on.");

            return await reading.From(ask, ports, profile, from);
        }

        /// <summary>
        ///     The badge, read the way <see cref="InboxCount" /> says — or <see langword="null" /> where the destination
        ///     carries none, or where a count read apart from the list could not be read: the list arrived, and a
        ///     badge is the least of what is on screen, so the one it already had stands (#396).
        /// </summary>
        private async Task<InboxCount.Tally?> Counted(
            Enquiry.Ask ask,
            Func<CancellationToken, InboxCount> count,
            IReadOnlyList<T> items)
        {
            if (Counting is not { } counting)
            {
                return null;
            }

            try
            {
                return await ask.Of(token => counting(count(token), items));
            }
            catch (WoolyException)
            {
                return null;
            }
        }
    }

    /// <summary>What one read of a destination came back with.</summary>
    /// <param name="How">What the destination is.</param>
    /// <param name="Entry">The rail's entry for it, whose timeline names what the list is of.</param>
    /// <param name="Fetch">What came back, and whether a rate limit cut it short.</param>
    /// <param name="Waiting">
    ///     What the destination's badge says, read alongside it, and which rows are unread for this visit; null where
    ///     it carries none.
    /// </param>
    private sealed record Listed<T>(Listing<T> How, Entry Entry, Fetch<T> Fetch, InboxCount.Tally? Waiting) : Found
    {
        /// <inheritdoc />
        /// <remarks>
        ///     What the list is of is the destination's own to say, and only a timeline has a name worth putting in
        ///     the sentence — so it is read off the entry rather than being a fifth thing a destination states.
        /// </remarks>
        public override Screen Becomes(Screen? standing)
        {
            var screen = How.Becomes(
                Fetch.Items,
                Arrival.Emptiness(Fetch.Items.Count, How.WhenEmpty, Entry.Timeline?.Description, Fetch.StoppedBy),
                Waiting?.Unread ?? InboxCount.Tally.NoneUnread);

            if (How.ReadsOn is not null)
            {
                screen.ReadingOn = new ReadingOn(Fetch.Onward);
            }

            return screen;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Read with the same read the screen is, and counted the way <see cref="InboxCount" /> counts it when the
        ///     shell opens — so the rail cannot say four over a list of three, nor say one thing on opening and
        ///     another on arriving.
        /// </remarks>
        public override Found.Badge? Counted(Screen became) => Waiting?.Badge;

        /// <inheritdoc />
        /// <remarks>
        ///     What was held is an answer that will cost nothing, and nothing will have cut it short — and coming back
        ///     to it is another visit, by which time what was unread on this one has been read (#397).
        /// </remarks>
        public override Found Held(Screen screen) =>
            this with
            {
                // No longer stopped, and with where to read on from kept, so a list held reads on from where it was
                // read to rather than being drawn whole.
                Fetch = Fetch with { StoppedBy = null },
                Waiting = Waiting is { } waiting ? waiting with { Unread = InboxCount.Tally.NoneUnread } : null,
            };

        /// <inheritdoc />
        /// <remarks>
        ///     Under what was read first, in the order it arrived, with where the read on said to go on from — so a list
        ///     held whole once its read on found nothing behind it is held whole. Everything the change table asks of a
        ///     held list is asked of these items, so a post reached by reading on is reached by a change too.
        ///     <para>
        ///         And with the badge the read on counted off the whole list, where it counted one, so a list arrived at
        ///         again says what it said as the reader left it rather than what its first read could (#466).
        ///     </para>
        /// </remarks>
        public override Found? Holding(Found readOn, Screen became) =>
            readOn is ReadOnto<T> onto
                ? this with
                {
                    Fetch = Fetch with { Items = [.. Fetch.Items, .. onto.Fetch.Items], Onward = onto.Fetch.Onward },
                    Waiting = Waiting is { } waiting && onto.Counted(became) is { } recounted
                        ? waiting with { Badge = recounted }
                        : Waiting,
                }
                : null;

        /// <inheritdoc />
        public override bool HoldsPost(string postId) =>
            Fetch.Items.Any(item => How.Carries(item) is { } post && PostChange.Names(post, postId));

        /// <inheritdoc />
        public override bool HoldsPostsBy(string account) =>
            Fetch.Items.Any(item => How.Carries(item) is { } post
                && new[] { post, post.Boosted }.Any(
                    by => string.Equals(by?.Account, account, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     What one read on of a subject came back with — a destination's list, a follow list, a group's actors — which
    ///     is added to the screen it was asked from.
    /// </summary>
    /// <param name="How">How the subject reads on.</param>
    /// <param name="Fetch">What came back, and where to read on from next unless the list is now whole.</param>
    private sealed record ReadOnto<T>(OnwardRead<T> How, Fetch<T> Fetch) : Found
    {
        /// <inheritdoc />
        /// <remarks>Read into the screen it was asked from, which is already up — so it is not put up again.</remarks>
        public override Screen Becomes(Screen? standing)
        {
            if (standing is not IReadsOn<T> list)
            {
                throw new InvalidOperationException("A read on lands on the list it was asked from.");
            }

            list.ReadOn(Fetch.Items);
            standing.ReadingOn?.Arrived(Fetch.Onward);

            // A read a rate limit stopped said what had arrived, which stops being so once this brings anything: the
            // notice goes, rather than standing over a list it no longer describes (#460).
            if (Fetch.Items.Count > 0)
            {
                standing.Notice = null;
            }

            return standing;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Where the destination counts so, off the whole list the screen holds, not off what this read brought:
        ///     what was put on top live and what was answered since are as much the list as anything read on.
        /// </remarks>
        public override Badge? Counted(Screen became) =>
            became is IReadsOn<T> list ? How.Recounts?.Invoke(list.Items, Fetch.IsWhole) : null;
    }
}
