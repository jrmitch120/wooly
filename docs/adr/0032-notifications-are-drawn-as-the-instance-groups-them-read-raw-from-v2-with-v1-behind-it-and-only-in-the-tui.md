# Notifications are drawn as the instance groups them, read raw from v2 with v1 behind it, and only in the TUI

Eight favorites of one post were eight rows on the TUI's Notifications screen, each with the same post drawn whole
underneath: "Alice favorited your post" eight times over, media and all (#448). What mattered — who, and how many —
was buried under copies of a post the reader wrote and already knew. Mastodon 4.3 added grouped notifications, which
the web app draws as one row per **Notification group**. The TUI now does the same (#453).

**The instance does the grouping, never this client.** The TUI reads `GET /api/v2/notifications` and sends no
`grouped_types`, so what is grouped is the instance's default: favorites and boosts of one post, and follows, grouped;
a mention never. This is ADR-0010's rule carried over. Grouping on the way in by this client's own choice would be the
same kind of thing as filtering on the way in: a decision about what the account's notifications are, made somewhere
the instance and every other client of the account cannot see. Grouping client-side would also need a key of this
client's own, and a group whose key the instance does not know cannot be dismissed whole, nor its actors listed. The
instance's key includes the hour (#449: `favourite-<post>-<hours since epoch>`), so favorites of one post that land in
different hours are different groups. They are drawn as different rows: merging them by post on screen would be this
client grouping after all.

**The calls are raw, through `RawMastodonCall`, as the read marker's are.** Mastonet 3.1.3 knows only `/api/v1`
notifications. The grouped read is written against the wire #449 pinned on a real instance — accounts and posts sent
once alongside the groups, and `most_recent_notification_id` a JSON number where every other id is a string — and the
accounts and posts in it are read as Mastonet's own entities, so they cross into this project through the same
mappings every other account and post does. The read pages by the instance's `next` link through the same
`PagedReading` loop every list uses, which now also takes a page read around Mastonet (`Page<T>`). It asks for 40
groups a page, the documented default, and never takes a short page for the end where a `next` link says otherwise.

**v1 stands behind it.** An instance older than 4.3 answers the grouped read's first page with a 404, the same signal
`InstanceLimits` already takes from `/api/v2/instance`. Its notifications are then read through `/api/v1`, each one a
group of one under the key Mastodon gives a notification it does not group, `ungrouped-<id>`, so the screen draws every
instance the same way. The 404 is learned once per profile for the session, in the adapter: every later read goes
straight to v1 rather than asking again. A profile's instance is not expected to be upgraded under a running session,
and if it is, the next session finds out.

**Only the TUI groups.** `notification list`, `dismiss` and `clear` read and print one notification at a time, and
`--json` is unchanged to the byte. A script reading `--json` reads notifications, by their ids; groups would change what
an item is under it. The badge also goes on counting notifications against the read marker (ADR-0027) rather than the
v2 `unread_count`, which counts groups: a group is drawn `unread` when its newest notification is newer than the marker,
since anything newer than the marker in a group makes its newest newer still.

**The layout is #450's A7b.** A group about the reader's own post, or with no post, is headed by its names line: two
actors named and the rest counted by the instance's count (`Alice, Bob and 6 others`), then the kind's icon and
sentence together in the kind's colour (`★ favorited your post`), then the unread mark and the age of the newest
notification at the right. The icon carries the kind on a terminal with no colour, as a post's marks do: `★` a
favorite, `↺` a boost, `@` a mention, `+` a follow, `•` a kind this client has no word for. The four that are a post's
marks too take those marks' roles. A follow and an unfamiliar kind take the account handle's role (`BylineHandle`): a
follow is an account arriving, and a kind with no word of its own has no mark to borrow a colour from.

A post drawn whole by its **Actor** — a mention, or a post from somebody the reader asked to be told about — is headed
by the kind alone (`@ mentioned you`), with no name and no age, because the byline under it already says who and when,
with the actor's face. Which posts those are is decided by who wrote the post against the group's actor, not by the
kind, so a kind this client has no word for falls under the same rule. **This departs from ADR-0010 for the TUI only**:
ADR-0010 accepted the account named twice on a mention, once in the notification's line and once in the post's
header, as the price of a post looking the same everywhere. A7b keeps that price's purpose and drops the price. The
post's byline is untouched — audience mark and time included — so the post still looks exactly as on any feed. Lifting
the time up onto the kind line was tried in the prototype and reverted: the post no longer looked as it does on a
timeline, and its audience mark read as the notification's. The CLI is unchanged and still names the account twice.

## Consequences

A group's actors are accounts (`Wooly.Core.Accounts.Account`), not an address and a name, so that later work can open
one by its id and draw its face. The instance sends a sample of a group's actors, up to eight; the count is the
instance's and is never inferred from the sample.

Tickets after #453 build on this: the avatar strip under the names line (#454), `a` opening a group's actor or its
actors screen (#451, #455), the mouse (#456), dismissing a group whole by its key (#457), and an arrival joining its
group live (#458). Until #455, `a` on a group of several opens its most recent actor. Until #457, `d` dismisses a group's newest notification, which is the whole of a group of one. A
notification that arrives live joins its group: the re-read that counts it reads the first page of groups too, while
Notifications is in front, and a group on screen takes the instance's new count and actors rather than this client
adding them up (#458).

The grouped read's `unread_count` and `expand_accounts=partial_avatars` are not used. The first counts the wrong thing
for the badge; the second could make the avatar strip cheaper, and is #454's to weigh.
