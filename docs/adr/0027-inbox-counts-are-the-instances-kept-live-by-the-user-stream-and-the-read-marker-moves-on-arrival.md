# Inbox counts are the instance's, kept live by the user stream with polling as the fallback; the read marker moves on arrival; no marker means nothing unread

The rail's Notifications badge said 40 for any account with 40 or more notifications, whether or not one of them had
been read, because it counted every notification the first read brought back. The web UI said nothing was waiting, and
it was right: Mastodon keeps a **Read marker** per account — the id of the newest notification read — and every client
of the account shares it. ADR-0010 left the marker alone ("this client only clears") and noted that a TUI showing an
unread count would be the screen that needed it. This is that screen.

**Every Inbox badge is the instance's Unread count, never one this client keeps.** Notifications counts the
notifications newer than the marker; Direct messages counts the conversations the instance flags unread; Follow requests
counts the requests not yet answered, since a request has no read state and waits until it is answered. Each is set
outright from a read of the instance, so every client of the account agrees, a duplicate event cannot leave a badge
wrong, and a change made in another client is picked up by the next read rather than lost. Where a change cannot say how
far a count moved — a dismissed notification was only waiting if it stood above the marker — the count is read again
rather than guessed.

**No marker means nothing unread.** An account that has never had a marker has nothing this client can prove is unread.
Counting every notification it has would be the badge that always said 40 again, so it counts nothing until something
arrives while the shell is running. For that, the first count of such a profile in a session sets a **Session marker**
at the newest notification it has — or at the beginning, where it has none, so that everything after counts — kept in
the session per profile and never sent to the instance (`SessionMarkers`); every count after it reads what is newer
than that (`INotificationInbox.ReadNewer`, or the plain read from the beginning). This is the one count the client
keeps for itself, and it says only what arrived while it ran. The instance's marker is left alone: the reader has
looked at nothing. Arriving at Notifications counts against the session marker as the badge did, drawing unread what the
badge counted, and sets a real marker, after which the session marker is unused.

**Counting stays a capped read since the marker.** `INotificationInbox.ReadUnread` reads the marker, then the
notifications above it, carrying `since_id` onto every page because the instance's link to a next page names only where
it starts. The count is capped at the same 40 a screen asks for, and the read asks for one more, so that exactly 40 is
known to be all of them; more than that is at least 40 and the badge draws `40+`, which is a property of the badge
rather than a second number. The other two badges, counted off their lists, draw `40+` for a list that came back full. Mastodon
4.3's unread-count endpoint was not used: the capped read works on every instance, and the badge never needs a number
past 40. The read is every kind of notification, nothing excluded, for ADR-0010's reasons.

**One path for every badge.** The counts read when the shell opens and the count read on arriving at a destination were
two pieces of code that could disagree. `InboxCount` now says how each of the three is counted: Messages and Requests
off the list their screen draws, so arriving costs nothing extra; Notifications by its own read against the marker, the
same read on opening and on arriving.

**The marker is read around Mastonet.** Mastonet 3.1.3 has `GetMarkers` and `SetMarkers`, but types `last_read_id` as a
number where Mastodon sends a string, so its answer cannot be read. The marker goes through `RawMastodonCall`, over the
same `HttpClient`, behind the existing port (ADR-0005, ADR-0011's way out of a gap in the library).

**Arriving at Notifications moves the marker** to the newest notification shown, so the badge clears as the reader
looks, the way it does on the web — and it is the instance's marker, so the web clears too. The rows that were unread on
arrival stay drawn unread for that visit, with a word as conversations already are (ADR-0014). Reading is never
dismissing: a read notification stays listed until it is dismissed. Messages and Requests are unchanged on arrival:
a conversation stays unread until it is opened or marked, and a request until it is answered. (#397.)

The move is asked for only where it would move past an unread notification on screen, or set a marker the account never
had: nothing unread is nothing to tell the instance, and a marker already past the newest shown would be moved back.
After a move the badge counts only what the unread read found that the list did not — what arrived between the two
reads. A move the instance turns down, or never hears for a rate limit or the network, is reported by the port as not
made rather than thrown, and the badge keeps the count it read: the marker only clears a badge, so a badge left standing
is the whole of what went wrong. The move is sent raw for the same reason the marker is read raw — `SetMarkers` reads its
answer as the marker it cannot read. "That visit" is the screen the arrival drew: a `g` refresh reads again and marks
what arrived since, and coming back to the held copy inside the cache window marks nothing, since it is another visit.

**The Inbox is kept live over the user stream, with polling as the fallback.** The shell holds one stream for the profile
acted as; an event re-reads the counts it touches, a drop reconnects with backoff while all three counts are polled every
60 seconds, and a reconnect re-reads all three. Nothing about the stream is drawn. A badge that rises rings the terminal's
own bell once, unless the user has turned it off. A badge at `40+` cannot count higher, so it has risen where the newest
thing waiting under it has changed. Rising is against the last count the instance gave, not whatever the rail draws: a
change made here that moves a badge by a known amount — a conversation marked read, a request answered — moves that
count with it, since the instance would now say the same, but under `40+` there is no known amount. `40+` is the only
badge shape that says "at least", so a badge there stays `40+` and its count is read again, and that re-read cannot ring:
what was newest may be what was just answered, and the next one moving up past the forty is nothing arriving.
Re-reads of one badge can be in flight together — a slow poll and the next, a stream gathering, a list read; each
takes its place in line as it is asked for, and one landing after a later-asked one has landed is dropped, so an older
count never overwrites a newer one nor gives the next re-read something to ring for twice. (#398–#401.)

What a re-read finds is put at the top of the destination's screen when that screen is in front, keeping the picked row
where it is on the page; on Notifications the marker is moved past it, as arriving would have, so the badge stays clear
while the reader looks and the rows are drawn unread for the visit — but only where everything unread was put on
screen. A re-read that found more unread than the 40 it counts has some past them that no screen has shown, and moving
the marker to the newest would mark them read unseen; so what was read goes on top drawn unread, the marker stays, and
the badge keeps saying `40+` until the reader arrives or refreshes, which moves it as arriving always has. Moving it
part way would not help: anything short of the oldest unread still skips the ones below. Exactly 40, all shown, moves
it. Nor is anything put on screen while the destination's own list read — an arrival or a refresh — is in flight: it is
about to draw the list and move the marker itself. Nothing else in flight holds a re-read back, and what one held back
found is not lost: the count is read again once that list read is over. It is done where every re-read lands rather
than per cause, so the poll and the stream cannot come to differ. A re-read that raises a count, or puts anything on screen,
forgets that destination's cached list: the held copy predates what arrived, and drawing it would put its cleared badge
back over the count just read. (#400.)

**The stream is connected around Mastonet's `GetUserStreaming`, and read through it.** `IInboxStream` tells a
notification with its kind, a conversation, and the connection coming up; a call ends when the connection does, and
reconnecting is the shell's. Mastonet 3.1.3's websocket streaming says nothing when it has connected — which is what
stops the poll — cannot be called off while it connects, puts the token in the address, throws on the instance closing
the connection, and dials past the named `HttpClient`'s handlers. So `InboxStream` connects its own `ClientWebSocket`
through those handlers with the token in a header, and hands each notification and conversation message to Mastonet's
`TimelineStreaming` to read, the way the marker is read around Mastonet and the rest kept behind it. The address is
the one the instance advertises (`configuration.urls.streaming`), or the instance's own. One connection carries two of
Mastodon's streams: `user` for notifications, and `direct` for conversations, which the user stream does not carry.
Only those two events are read; the home timeline the user stream also carries is not, so a post that cannot be read
never drops it.

**What the stream says is gathered before it is re-read.** A direct message arrives as two events — a mention on the
user stream, a conversation on the direct one — and raises two badges, but it is one arrival and rings once. The shell
gathers events for two seconds from the first, off its own clock, and re-reads every count they touch as one re-read.

## Consequences

ADR-0010's "this client only clears" no longer holds for the TUI: it reads the marker, and on arriving at Notifications
moves it. The CLI is unchanged — `notification list` neither reads nor moves the marker, and has no unread flag.

Arriving at Notifications is one read more than it was — the marker and what is above it, besides the list. A count that
cannot be read leaves the badge as it was rather than failing the arrival, the same rule the shell's opening counts
already follow.

The no-marker rule cannot be tested live: Mastodon cannot delete a marker, so a test account that has had one never gets
back to having none. It is held at the fake seams.
