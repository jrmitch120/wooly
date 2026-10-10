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
arrives while the shell is running (#401).

**Counting stays a capped read since the marker.** `INotificationInbox.ReadUnread` reads the marker, then the
notifications above it, up to the same 40 a screen asks for, carrying `since_id` onto every page because the instance's
link to a next page names only where it starts. A read that comes back full is at least that many and the badge draws
`40+`, which is a property of the badge rather than a second number; the same holds for the other two badges. Mastodon
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
own bell once, unless the user has turned it off. (#398–#401.)

## Consequences

ADR-0010's "this client only clears" no longer holds for the TUI: it reads the marker, and on arriving at Notifications
moves it. The CLI is unchanged — `notification list` neither reads nor moves the marker, and has no unread flag.

Arriving at Notifications is one read more than it was — the marker and what is above it, besides the list. A count that
cannot be read leaves the badge as it was rather than failing the arrival, the same rule the shell's opening counts
already follow.

The no-marker rule cannot be tested live: Mastodon cannot delete a marker, so a test account that has had one never gets
back to having none. It is held at the fake seams.
