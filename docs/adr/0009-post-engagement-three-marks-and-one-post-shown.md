# Three marks that are on or off, a boost that answers about the post it carries, and one way a post is written

Post engagement (#23) adds seven command-line verbs — `post boost`/`unboost`, `favorite`/`unfavorite`, `pin`/`unpin`,
and `post show` — over what is, underneath, three switches and a read. Four decisions keep it that size.

**Three marks that are on or off, not six acts.** `PostMark` names `Boost`, `Favorite` and `Pin`, and
`IPostEngagement.Mark` takes the mark and whether it is `wanted`. Un-boosting is not its own thing to do; it is boosting
undone. Modelling the six verbs as six methods would put the same shape through the port, the adapter, the fake and the
CLI six times over, and each copy is a chance for `unfavorite` to grow a behaviour `favorite` does not have — a
different report, a different exit code, a different answer to being asked twice. As three marks, the six commands are
six one-line classes over one base, the adapter is one `switch` from `(mark, wanted)` to Mastodon's six endpoints, and
what the user is told comes from one table. It is also the shape a TUI keybinding wants, where the mark is fixed by the
key and the state is whatever the post is not currently in.

**Boosting answers about the post the caller named.** Mastodon's `POST /statuses/:id/reblog` does not hand back the post
that was boosted — it hands back the boost, a post of the booster's own with an id of its own, carrying the original in
its `reblog` field. Taken at face value, `post boost 110` would report `Boosted 114`, and `post boost 110 --json` would
write an id that no other command in this client names that post by. So the port unwraps it, and `Mark` returns the post
the caller asked about, as it now stands. Nothing above the adapter has to know that boosting is the one mark that makes
a post. `post show` does not unwrap: a reader who named a boost by its own id is shown the boost, the same way a timeline
shows one, because there they named the thing they got.

**The instance settles whose post it is and what is already on it.** Nothing here reads the post first to find out
whether it is already boosted, or whether it is the account's own and therefore pinnable. Asking would cost a round trip
to reach a worse answer than the instance's own — one that can be stale by the time it is acted on, and that duplicates
rules (which posts may be pinned, how many) that vary by instance and change without this client hearing about it. So
the marks are sent, and a refusal is reported in the instance's own words through ADR-0006's one handler.

That includes what asking twice means, which Mastodon does not answer the same way for all three: boosting something
already boosted and favoriting something already favorited both pass, where pinning something already pinned is refused
outright. This client does not smooth that over — swallowing the refusal would mean holding a copy of each instance's
rules and getting them wrong quietly, and a script that wants "pinned either way" can read the refusal and decide, which
is more than it could do if the refusal had been eaten here.

**A post is written down in one place, whichever command printed it.** The post itself — who wrote it, when, its warning,
its text, its counts — is `PostReport.Write`'s, and `post show` and `timeline home` both ask it for the same post. The
rendering moved out of `TimelineReport`, which now only decides that posts come one after another with a blank line
between them. ADR-0008 gave the same argument for
`PostDocument` and `--json`; this is that argument for the output people read, and the reason is the same. Two spellings
of a post is how the id column comes to be bold on a timeline and plain on its own, or how a content warning comes to be
honoured in one place and printed past in the other. `--json` on the marking commands is the same `PostDocument` again,
so `post favorite 110 --json` and `post show 110 --json` describe one post one way.

## Consequences

One rendering does not mean one output. `post show` prints the post's web address underneath the shared block, and a
timeline does not: a timeline is read down, where one address per post is a line of noise on every one of them, and a
post asked for by id is being looked at, where the address is the one thing that cannot be worked out from what is on
screen. That difference is a second method on `PostReport` that a timeline does not call, over the same block — which is
the shape any later difference should take too, rather than a second idea of what a post looks like.

`(mark, wanted)` is switched on twice — once in `PostEngagement.Apply` for the endpoint, once in `PostReport.Did` for
the word the user reads — and that is deliberate rather than a duplication waiting to be collapsed. The two answer
different questions on opposite sides of the port, and the only ways to have one switch are to put user-facing English
in the core layer or Mastodon's endpoint names in the CLI's output. A fourth mark therefore costs an edit in both,
which is the price of neither layer knowing the other's vocabulary; the compiler does not force that second edit, so the
theory in `PostEngagementCommandTests` naming all six verbs is what catches a mark that was added but never given words.

Unwrapping a boost throws away the boost's own id and address, and nothing hands them back. Retracting a boost does not
need them — `post unboost` takes the id of the post that was boosted, not of the boost — so nothing in this ticket
misses them. A script that wants to link to or delete its own boost has no way to name it, and that is the case that
would justify a `boostId` on the JSON a boost answers with.

Reading one post sits on `IPostEngagement` rather than in a port of its own. It is the same answer every mark gives —
the post as it now stands — reached through the same client over the same endpoint family, and a separate port returning
the same `Post` would be a seam with no decision behind it. If a later ticket gives single-post reading something to
decide of its own (a thread, a context, an edit history), that is when it earns its own port.

`Post` still carries no "you have boosted this" flag. The API sends `reblogged`, `favourited` and `pinned` on every
status, and every one of the commands here already returns a post that has just been made to carry the mark asked for,
so nothing in this ticket has a question those fields would answer. The screens that will (#33, #28 — a TUI that has to
draw a post's current state before anyone presses a key) are where adding them belongs, together with what a timeline's
`--json` should say when the field is absent because nobody is signed in.
