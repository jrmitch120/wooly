# A direct message is a post with an audience, a conversation is what holds them, and a reply never outruns what it answers

Direct messages (#27) look like a fifth feature and are mostly a rearrangement of three that exist. Four decisions come
out of building them that way, and the last of them changes how every reply is published, not only the private ones.

**Sending a direct message is publishing a post, and there is no second way to compose one.** `IDirectMessages` lists
conversations, shows one, and marks one read; it has no `Send`, and that absence is the design rather than a gap.
Mastodon has no send-a-message endpoint — a direct message is a post whose visibility is `direct`, delivered to the
accounts its text mentions — so `dm send` is `PostComposeCommand` with two questions answered for the user: the audience
is direct, and the recipient is written into the text by `DirectMessage.To`. Everything else is inherited whole, which
is what makes `dm send alice@hachyderm.io "look" --cw spoilers --media cat.png` work without anybody having thought
about it. The alternative — a `Send` on the messages port taking a recipient and a string — would be a second composer,
and the second composer is the one that turns out not to support polls, or alt text, or whatever is added next.

What the reuse costs is one small piece of surgery: `--visibility` moved off `PostComposeSettings` and onto a new
`PostPublishSettings` that `post create` and `post reply` take instead. `dm send` inherits the composing without
inheriting the choice, because an option offered where only one value is possible is an option somebody will pass
another value to. Under strict parsing (ADR-0006) `dm send --visibility public` is now turned down by the parser rather
than accepted and ignored.

**A conversation is a noun of its own, with an id that is not a post's.** `dm` is a branch rather than a corner of
`post` because what a user wants of their messages is a list of who is talking to them, which no timeline answers. The
id a conversation carries is what `dm show` and `dm read` take, and it is emphatically not the id of any post in it —
marking a conversation read by the id of the post in it clears nothing, and both the help text and
`SingleConversationSettings` say so out loud.

Showing one has a shape forced on it: Mastodon serves no single conversation by id, only the list of them. So the list
is walked until the id turns up, and no further than 200 conversations — far enough for anything a user is realistically
naming, short enough that a typo costs a handful of calls rather than an account's whole history. `PagedReading` grew a
`stopWhen` for it, which is the same walk it already did with a different reason to stop; the alternative, asking for
all 200 and searching the result, would spend five calls to find something on the first page. A rate limit part way down
the walk is re-thrown as itself rather than reported as "no such conversation", because telling a user their id is wrong
when what happened is that the looking stopped sends them checking a value that was right.

That ceiling is a real edge: `dm list --limit 300` can print a conversation `dm show` then refuses, since `--limit` has
no ceiling of its own. Rather than pretend otherwise, `UnknownConversationException` says how far it looked instead of
claiming the id does not exist. Raising the ceiling only moves the edge; removing it would let one mistyped id page an
account's entire history.

**What `dm show` shows is the thread the conversation's last post is in, which is not always all of it.** A conversation
carries only its last post, so the thread comes from that post's context: one call to find the conversation, one to read
what was said in it. But an instance groups a conversation by *who is in it*, not by what answers what — so two messages
to the same account that each answer nothing are one conversation holding two unrelated roots, and the context of the
newest reaches only its own. The API has no call for "the posts of conversation X", and the alternatives are worse:
reading the whole direct timeline and matching participants would be several calls and a guess at what belongs. The
newest thread is what a reader wants nearly always, an older root is still reachable through `post show`, and this is
recorded here rather than left for somebody to discover.

**Reading a conversation does not mark it read.** `dm show` leaves the unread mark exactly as it found it and `dm read`
is what takes it off. A client that cleared the mark on the way past would make "what have I not read" unanswerable for
anything that looked afterwards — including a script that lists conversations, shows each one, and then cannot tell
which of them were new.

**A reply is never published wider than the post it answers, and that takes a read before the write.** This is the
decision with reach beyond this ticket. Mastodon does not enforce it: the API takes whatever visibility a request names,
whatever the request is answering. So `post reply` on a direct message, composed at an account's own default, publishes
a private conversation to the world — which is not a mistake anybody can take back, and is exactly what #27's
"visibility forced to direct" is asking to prevent. `PostAuthor.Publish` therefore reads the answered post first, the
same trade `Edit` makes and for the same kind of reason: what the request must not lose is only knowable from the thing
being answered.

Three cases fall out, and the difference between the last two is why `PostDraft` gained a `VisibilityChosen` alongside
its `Visibility`:

- Nothing said: the reply goes out at the answered post's visibility. Leaving it to the account's own default on the
  instance is not available, because that default is a value this client cannot see and might be wider.
- A standing preference too wide for it: narrowed to fit, without comment. Refusing would leave a profile whose
  `default_visibility` is `public` unable to answer a direct message at all.
- A `--visibility` typed on the invocation that is too wide: refused (`WiderReplyException`, a usage error). Narrowing
  an explicit ask would publish something other than what was asked for, and under a pipe the sentence saying so is read
  by nothing — an exit code is what a script can act on.

Generalising past `direct` is deliberate. A reply to a followers-only post that goes out public leaks it just as
completely, and a rule with one exception in it is a rule somebody will find the second exception to. The ordering this
relies on — that `PostVisibility` lists its four from widest to narrowest — is stated once, in `PostAudience`, so a
member inserted in the middle of that enum breaks there and nowhere else.

## Consequences

Every reply now costs one extra call to the instance. A post that answers nothing pays nothing, so `post create` and
`dm send` are unchanged; `post reply` is two calls where it was one. That is the price of the guarantee, and there is no
cheaper way to it — the visibility of the answered post is not on the command line and not in the config file.

`default_visibility` no longer decides a reply on its own; it can only narrow one. This is a real change to a
documented preference, and it is the right way round: the preference exists so a careful poster need not type
`--visibility` every time, and a careful poster is not asking to widen somebody else's thread.

One thing this deliberately does not do: **nothing here deletes or mutes a conversation.** Mastodon offers both, #27
asks for neither, and a `dm delete` that removed a thread from the list while leaving the posts in it standing would be
a verb whose meaning nobody could guess. If it is wanted, it wants its own ticket and its own word.
