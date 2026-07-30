# Four kinds named, the rest passed on under the instance's word, and one paging loop for every list

Notifications (#24) add `notification list`, `dismiss` and `clear`. Reading them is the second list this client fetches,
so ADR-0007 already settled most of it — a caller asks for a count rather than pages, a rate limit part way through is a
partial result, and `--json` is an envelope. What is new here is what a notification *is*, and what happens to the ones
this client has never heard of.

**A kind is a word, and only four of them are this project's.** Mastodon's notification `type` is an open set that grows
between releases — `poll`, `update`, `severed_relationships`, `admin.report` all arrived after the four #24 names. So
`NotificationKind` is a value with a `Name` rather than an enum: `Mention`, `Follow`, `Boost` and `Favorite` are the four
this client has words of its own for (CONTEXT.md's vocabulary, so the wire's `reblog` and `favourite` never surface), and
anything else is `NotificationKind.Reported(whatever the instance called it)`. An enum would have forced the alternative
— a catch-all member — and a catch-all member with no word behind it is a notification a user can see the existence of
and nothing else.

**Nothing is filtered on the way in.** Mastodon offers to leave kinds out of what it sends, and `NotificationInbox` asks
for all of them. The temptation is the opposite: #24 lists four kinds, Mastonet has an `excludeTypes` flag, and excluding
the rest would make every notification one this client has a sentence for. It would also hide notifications the account
really has, from a list whose whole job is to say what is waiting — and hide them past rescue, because a notification
that never appears has no id to dismiss it by, and `notification clear` would silently take away things the user was
never shown. A `poll` notification therefore reads as `alice@example.social notified you (poll)`, which is less than the
four get and considerably more than nothing. The same word goes into `--json`'s `kind`, so a script sees a word for every
notification rather than a hole where the unfamiliar ones were.

**The paging loop moved out of the timeline reader instead of being copied.** ADR-0007 argued that handing pages back to
callers would put the same loop in the CLI, the TUI and every later list command, and that each copy would get the
end-of-timeline condition slightly differently. That argument applies to a second *adapter* copying it just as much as to
a caller, and the condition in question — trust the instance's `next` link, fall back to the oldest id, stop dead on an
empty page — is exactly the subtle part. So it is now `PagedReading.Collect`, generic over what a page holds, and
`TimelineReader` and `NotificationInbox` are both a call to it plus a mapping. Each still hands back a fetch that names
its own contents (`TimelineFetch.Posts`, `NotificationFetch.Notifications`), because `Items` is the right word inside the
loop and the wrong word at every place a caller reads one.

The page size stayed with the callers rather than moving into the loop with everything else, because Mastodon does not
answer with one ceiling: a timeline serves 40 and `/api/v1/notifications` serves 30. A shared constant of 40 would make
every full page of notifications arrive short of what was asked for, and a short page is precisely what this loop reads
as the end of a list — so an account with more than thirty waiting would have been told it had thirty, and told the fetch
was complete. It survives only where an instance sends the `next` link, which is the fallback's whole reason for
existing. Each adapter therefore states its own endpoint's ceiling and passes it in.

**Clearing asks first; dismissing does not.** `notification clear` takes away a list nobody has necessarily read yet and
nothing brings it back, so a person at a terminal is asked, with `--yes` and a non-interactive console both meaning go
ahead — the bargain `post delete` struck, on the same reasoning about scripts having nobody to answer a prompt. That
bargain is now `Consent.Given` rather than a copy in each command: two commands making the same promise to the same user
in two places is how one of them comes to prompt a script, and it is the rule rather than the question that is shared —
each command still asks in its own words. Dismissing one is not asked about at all: the cost of a mistyped id there is
the single line the user had just read, and the post behind it is still where it was.

## Consequences

`NotificationKind` is compared, not switched. There is no exhaustive `switch` the compiler can check, so a fifth kind
this project decides to name — a poll it has words for, say — will not fail the build for want of a sentence in
`NotificationReport`; it will quietly fall through to the instance's own word. That is a soft landing rather than a
wrong answer, but it means the test naming all four kinds is what keeps the table honest, in the way
`PostEngagementCommandTests` does for the six marking verbs.

`NotificationListCommand.ExecuteAsync` is the same six lines as `TimelineCommand.ExecuteAsync` — resolve, read, write one
of two ways, throw whatever stopped it short — and that copy is left standing. Collapsing it needs a base class generic
over both the settings and the fetch, and the two fetches have no supertype to be generic over; giving them one, so that
four lines could live in a third place, would put an interface on two domain records to serve the CLI's convenience. The
copy is cheap to read and the tests pin both halves. A third list command is where that calculation changes, and it is
the same third command that would collapse the settings below.

`notification list` has settings of its own rather than sharing `TimelineSettings`. The two are the same two options with
the same default, and the copy is deliberate: Spectre reads `[Description]` off an attribute, so sharing them would mean
`--limit` describing itself as a count of posts in a command that counts notifications. A third list command is the point
at which a shared base with generic wording starts costing less than the copies.

A mention prints the account twice — once in the notification's own line, once in the header of the post underneath it,
where the post's own timestamp lives. That is the price of `PostReport.Write` being the only thing that knows what a post
looks like (ADR-0009), and it is the right price: a post read in a notification and the same post read on a timeline stay
the same shape.

Nothing here reads or moves Mastodon's read marker, which is the non-destructive half of the API's model — an account
can mark notifications *seen* without clearing them, and this client only clears. A TUI that shows an unread count is the
screen that would need the marker, and it can have both: dismissal stays what it is, and "seen" becomes something the
inbox tracks alongside it.

There is no `--kind` filter. The instance could do that filtering, through the same `excludeTypes` this deliberately does
not send, and a client-side filter over what came back would silently under-fill `--limit`. Whichever way a later ticket
goes, it should note that Mastodon's exclusion list only names the kinds Mastonet knows about, so an instance-side filter
cannot exclude the very kinds this ADR is careful not to drop.
