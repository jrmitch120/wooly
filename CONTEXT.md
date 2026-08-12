# Mastodon CLI Client

A cross-platform (.NET) terminal client for Mastodon — a scriptable CLI command surface plus an interactive TUI, sharing one API/auth/config layer.

## Language

**Post**:
A single unit of user-authored content on Mastodon (text, media, poll, content warning). The API's wire format calls this a `status`; older community usage calls it a "toot". This project always says "post" in the spec, domain code, and CLI command/output text — `status`/`toot` may still appear at the literal API-wire-format layer (e.g. deserializing the API's `status` JSON field) but never in user-facing language or domain vocabulary.
_Avoid_: status, toot

**Attachment**:
Something on a post besides its text — a picture, an animation, a video, a sound. Two records rather than one, because
the same subject is a different thing at each end: on its way up it is a file on this machine with a description
(`MediaAttachment`), and read back off an instance it is an id, a kind, an address and a description (`PostMedia`).
Only a still picture is **drawn**, and only in a TUI on a terminal that speaks sixel or the Kitty graphics protocol;
everything else is **linked** — its address, and what its author said it shows — on the CLI and the TUI alike
(ADR-0016).
_Avoid_: media file, image (where the kind has not been settled)

**Drawn**:
A picture the TUI paints in place, said as the thing rather than the adjective: which one it is, and where its pixels
are fetched from (`Drawn`). What the TUI's whole picture path turns on — what is held, what is sent for, and what a box
on a row stands in for — because two different things are painted and only one of them is an **Attachment**. A post's
picture hangs off the post; an author's avatar hangs off the author, and calling the second one an attachment would
have made "something on a post besides its text" mean something else wherever an avatar went. Nothing outside a TUI
has one: the CLI **links** everything (ADR-0016).
_Avoid_: image, media (for the avatar half of this)

**Boost**:
Re-sharing another account's post to your own followers. The API calls this a `reblog`. This project always says "boost" in user-facing language and domain code; `reblog` may still appear as the literal API field name at the wire layer.
_Avoid_: reblog, repost, retweet

**Favorite**:
Marking a post as liked, without re-sharing it. The API spells this `favourite` (British spelling). This project uses the US spelling "favorite" everywhere outside the literal API-wire layer.
_Avoid_: favourite, like

**Notification**:
Something an instance tells an account happened to it: somebody mentioned it, followed it, boosted or favorited one of
its posts. This project names those four and passes any other kind on under the instance's own word for it (ADR-0010).
A notification is distinct from the post it is about — it has an id of its own, which is what dismisses it.

**Direct message**:
A post whose visibility is direct, which Mastodon delivers only to the accounts its text mentions. Not a separate kind
of thing from a post and not composed by a separate path — `dm send` is post authoring with the audience settled and the
recipient written into the text (ADR-0013). "DM" is the CLI's noun for the branch and nothing else.
_Avoid_: private message, PM

**Conversation**:
One thread of direct messages, as the instance keeps it: who it is with, whether anything in it is unread, and what was
last said. It carries an id of its own, which is what shows it and what marks it read — distinct from the id of any
**post** in it, and the two are not interchangeable.

**Instance**:
The Mastodon server a given account is registered on, identified by its domain (e.g. `mastodon.social`). Mastodon's own newer UI/docs increasingly say "server", but this project uses "instance" throughout for consistency with the wider ecosystem (other clients, API error messages) and to avoid ambiguity with "server" as hostname or client-server architecture.
_Avoid_: server (when referring to a Mastodon instance)

**Account**:
A user's identity on a specific instance, addressed as `username@instance` when referenced from outside its home instance. Distinct from a local CLI **profile** (below).

**Tie**:
One of the three things the profile's own account can have with another and undo again: following it, blocking it, or
muting it. Each is on or off rather than an act of its own, so `unfollow` is a follow taken off rather than a fourth
thing to do. Distinct from **standing**, which is what the instance reports the ties currently are.

**Follow request**:
A follow waiting for the account being followed to accept it, which is what following a **locked** account — one that
approves its followers by hand — leaves behind instead of a follow. Named by the id of the account that asked, which is
what accepting or rejecting one takes.

**Standing**:
Where the profile's own account stands with another one: whether it follows it, has a **follow request** waiting with
it, is followed by it, and whether it has blocked or muted it. An instance sends this only where it is asked, so an
account may carry none — which says the question was not put, not that the answers are all no.

**Profile**:
A named local credential/config entry in this CLI tool, pointing at one Mastodon account. A user may have multiple profiles (e.g. personal + work accounts, possibly on different instances). One profile is the "current" profile used by default; commands may override it per-invocation.
_Avoid_: account (when referring to the CLI's local credential entry, to keep it distinct from the Mastodon account itself)

**Destination**:
One of the places the TUI's rail can send you — a timeline, notifications, direct messages, follow requests, search,
the profile's own account. A destination is what carries an unread count and what costs a fetch to arrive at, which is
why choosing one is a decision of its own (ADR-0014). Distinct from a **screen**: a destination is the entry on the
rail, a screen is what the content region is showing, and drilling from a post into an account changes the screen
without changing the destination.
_Avoid_: tab, section, page

**Arrival**:
Landing on a **destination**, which is one thing however many destinations there are: whatever is in flight is
overtaken, an empty screen goes up at once, what the destination holds is drawn from what it last held or asked for
under an **enquiry**, and its unread count moves with the list it is drawn beside (#100). A destination says only what
it reads, what that becomes on screen, what an empty one is told and what it counts — a timeline saying it counts
nothing, rather than an arrival that leaves the count out. Distinct from drilling in: an arrival puts the stack back to
one **screen**, and the profile's own account is arrived at by replacing what is on it rather than pushing onto it.
_Avoid_: load, navigate (for this; a screen is opened _from_ a destination, and a destination is arrived at)

**Enquiry**:
A question put to an **instance** on a reader's behalf, which survives neither their patience nor their attention: it
waits out a rate limit where they can watch it count down, turns a failure into a notice rather than an exception, and
is dropped unread if they have arrived at another **destination** since it was sent (ADR-0014). One enquiry may put
several calls to an instance, and is overtaken, or not, as a whole.
_Avoid_: request (which is a follow request), query (which is what a search takes)

**Picked**:
Which of the things on a screen the reader has walked to with `j` and `k`, and what every key that acts on something
acts on. Distinct from the rail's **cursor** and its **selection** (ADR-0014): those are about which **destination**
you are going to, this is about what you are looking at once you are there. A screen showing an empty list has nothing
picked, which is a fact about the list rather than a place in it.
_Avoid_: cursor, selection, highlight, current row

**Reference**:
A hashtag, a mention, or an address found inside a post's text — the things `←`/`→` walk and `⏎` opens. Distinct from
**Picked**, which is which post the reader has walked to; a reference is walked *inside* the picked post, one level
in. Replaces what `BodyText` used to call a "mark," a word this project had already spent on `Post.Marks`
(boost/favorite/pin).
_Avoid_: mark (for this; reserved for boost/favorite/pin)

**Mention**:
Two things one word, told apart by which side of the wire they are on. `Post.Mentions` is everyone a post names, as
the instance resolved them (`username@instance`) and sent down with the post; a mention **Reference** is the `@maria`
a reader walked to in the text, which is only somebody in particular because the post's list says so (#85). Crossing
from the second to the first is a lookup and never a fetch — a bare handle means nothing without an instance to put
after it, and guessing this profile's own would open somebody else under somebody's name.

**Reading**:
What this reader has done to one post — asked past its content warning, walked to a **reference** inside it — carried
as one thing, keyed by which post it is (#95). Distinct from **Picked**, which is which post: a post nobody has
touched is `default`, and every post on a screen but the picked one is. Nothing to do with `Shell`'s own private
sense of the word, which is the **conversation** being read.
_Avoid_: state, reader state

**Vote**:
Casting a choice on a post's poll, Mastodon's own word for the act. Distinct from **Picked** and from toggling an
option before it is cast — the toggle and the cast key are both plain interaction words, not glossary entries;
"vote" is the one term this earns because it is the domain fact an instance records, not a keystroke describing it.

**Role**:
What a piece of the TUI is, said in a way a **theme** can answer: a byline's name, a handle, a content warning, a boost
mark, an unread count, the selected row. Views name roles and never colours, so that the same screen can be themed,
degrade to sixteen colours or to none, and be tested on which role it chose (ADR-0014). Distinct from Terminal.Gui's
own `VisualRole`, which describes what a widget is doing (`Normal`, `Focus`) rather than what a boost is.

**Theme**:
A named set of colours answering this project's **roles**, written as a table in the same TOML config file as
everything else and chosen by name. Two are built in, for dark and light terminals; a theme a user writes overrides
only the roles it names.
