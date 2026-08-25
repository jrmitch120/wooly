# Testing seams: IMastodonClient primary, HttpMessageHandler and a live instance secondary, TUI shell untested

API-dependent code needs a small number of deliberately chosen test seams rather than testing at every layer. The primary seam is `IMastodonClient`/`IAuthenticationClient` (see ADR-0001) — interface-level fakes carry the bulk of unit tests for command and business logic, including whatever a TUI screen invokes. `HttpMessageHandler`-level fakes are reserved narrowly for tests that guard Mastonet's JSON deserialization against edge-case payloads (unicode, empty fields, pagination headers), which interface fakes can't catch.

**Command logic reaches Mastonet through a narrow port, not through `IMastodonClient` directly.** `Mastonet.IMastodonClient` is Mastodon's whole REST surface — 111 members — and this project takes no mocking library, so "an interface-level fake" of it is not a thing a test can reasonably write. What makes the primary seam workable is that each feature gets a small interface of its own over the fat client (`IAccessTokenVerifier` is the first), and command tests fake *that*. Those ports are one or two calls deep, so hand-written fakes for them stay a few lines long.

That leaves the thin adapters behind those ports — the classes that do call Mastonet — with only one seam beneath them, and it is `HttpMessageHandler`. Testing an adapter there is inside the allowance above rather than an exception to it: what an adapter does is turn Mastonet's deserialized response into a domain value, so scripting the payload is how that mapping is observed at all. The narrowness that matters is the same as before — this covers adapters over Mastonet's interfaces, and it is still not licence to fake HTTP for command logic, which has a port to fake instead. A small integration suite runs against a real, dockerized Mastodon instance for the core read/write paths (auth, timeline fetch, post, boost/favorite) to catch drift between Mastonet's models and the live API — run in CI via docker-compose, in a separate filtered category so it doesn't slow the default test run. The TUI shell itself (screens, keybindings, rendering, focus handling) is manually smoke-tested, not automated, even though Terminal.Gui v2's instance-based `IApplication` makes headless driving possible — that investment isn't worth it for v1 since all the logic the shell calls is already covered through the primary seam.

## Consequences

Anyone tempted to add headless TUI automation, or to fake every test at the `HttpMessageHandler` level "to be thorough," should treat that as a deviation from this decision worth re-raising, not a default extension of test coverage.

A feature whose logic genuinely cannot sit behind a narrow port — so that a test would have to stand in for `IMastodonClient` itself — is the case that reopens the no-mocking-library question. Adding one (NSubstitute) would be the answer then, together with a rule for when it is used instead of a hand-written fake; a second faking idiom adopted without that rule is how a test suite stops having one way to do things. Until such a feature turns up, the ports keep the primary seam cheap and this stays undecided on purpose.

## Amendment: the shell is driven after all, and what drives it (ticket #149)

This ADR priced headless TUI automation and declined it. The price turned out to be much lower than the quote, and
`tests/Wooly.Tests/Tui/` is now one of the larger folders in the suite. The seam it settled on is worth recording,
because an agent reading the paragraph above is currently told that the tests already in the repo are a deviation and
that adding to them needs re-raising first.

**What is retracted.** Two sentences, and nothing else in this ADR. The first is "The TUI shell itself (screens,
keybindings, rendering, focus handling) is manually smoke-tested, not automated" — three of those four are automated;
rendering is the one that survives, in the narrowed form below. The second is the clause in Consequences reading
"Anyone tempted to add headless TUI automation ... should treat that as a deviation from this decision worth
re-raising". The rest of that sentence — the same warning about faking every test at the `HttpMessageHandler` level —
stands as written.

**The primary seam did not move.** `AShell` builds the whole shell over the `Wooly.Core` ports, the same ports
`tests/Wooly.Tests/Cli` fakes, because the TUI is a second front end over them rather than a second way of reaching an
instance. That is the part of this ADR that held, and it is why almost every shell test needs nothing terminal-shaped
at all: `Pressing` composes `Keymap.Means` with `Shell.Do`, so a test can press a key at a shell that has no window.

**Two seams are new.** `IShellHost` is the terminal's two services reduced to two methods — wait, and get back onto
the thread that draws — and `FakeShellHost` is a test saying when time passes. That is what makes the rail's settle
window and the rate-limit countdown assertable rather than slept through, alongside a `TimeProvider` fake for the
clock. Below it, `ShellWindow` itself is constructed directly, given a width and a height, laid out and sent real
`NewKeyDownEvent` presses.

**It cost less than this ADR assumed, and less than ADR-0014 assumed.** ADR-0014's testing paragraph anticipated the
reconsideration and named the mechanism: `IApplication.GetInputInjector()` and a virtual time provider, "cheap enough
to reconsider when the shell settles". Half of that is right — the time provider is fake. The injector is not used,
and neither is `IApplication`, a driver, or a run loop. A `ShellWindow` is a view: constructing one, calling
`Layout()` and raising a key event needs no application hosting it. The investment this ADR declined was a
Terminal.Gui application harness, and it was never built.

**What driving it caught that the ports could not.** ADR-0015 has said since it was written that a reply draws what it
is answering above the editor. It was painted and never seen: the editor is a separate view laid over the same rows,
opaque, covering the block on every frame — visible to nobody, because the only thing the block does is be seen. The
ports say nothing about that, and neither does a test of the rows themselves, which passes over an editor drawn on top
of them. The first fix was wrong in the same silent way, leaving the editor's `Y` at 1 because the position is a
`Pos.Func` read only on a layout pass. `ShellComposeLayoutTests` asserts `Frame.Y` after that pass, which is the only
place either mistake shows up. Driving a real window also pins the keys somebody would otherwise reasonably "fix":
`k` is the next post and `j` the one before it, deliberately the opposite way round from vim, and `ShellKeyTests`
presses `Key.K` rather than calling `Walk(1)` — a test that says `Walk(1)` proves nothing about what a reader pressed.

**Where the line falls now.** Pixels are still not asserted, and the retraction is only of "the shell is not driven".
ADR-0014's rule that role selection is the assertable half of rendering is kept as it was: `RoleTests` walks the
contract and asserts which **role** a thing takes — the test that caught `Role.Poll` themed, documented and drawn by
nothing — and `NoHardCodedColourTests` scans the sources for a constructed `Attribute`, `Color` or `StandardColor`
outside the theme. Neither looks at a cell. The instinct underneath this ADR's original sentence, that asserting
drawing is not worth the money, survives intact; what changed is that "which key leaves the shell in which state" was
never really a question about drawing.

**What is still deliberately not done.** No mocking library. Every fake in the TUI suite is hand-written and a few
lines long, for the reason above: the ports are one or two calls deep, and a second faking idiom adopted without a
rule for when to use it is how a suite stops having one way to do things. `HttpMessageHandler` fakes stay where this
ADR and ADR-0006 put them — thin adapters over Mastonet, and the cross-cutting HTTP layer — and driving a window is
not licence to widen them. The shell reaches an instance through `Wooly.Core` ports; there is no HTTP under a TUI test
to fake.
