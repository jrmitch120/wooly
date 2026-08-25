# Testing seams: IMastodonClient primary, HttpMessageHandler and a live instance secondary, TUI shell driven without a terminal

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

**What is retracted.** Two claims, and nothing else in this ADR. The first is "The TUI shell itself (screens,
keybindings, rendering, focus handling) is manually smoke-tested, not automated ... that investment isn't worth it for
v1" — three of those four are automated; rendering is the one that survives, in the narrowed form below. The second is
the clause in Consequences reading "Anyone tempted to add headless TUI automation ... should treat that as a deviation
from this decision worth re-raising". The rest of that sentence — the same warning about faking every test at the
`HttpMessageHandler` level — stands as written.

**The title is retracted with them**, which is the one place this amendment edits the ADR above rather than appending
to it. It ended "TUI shell untested", and a heading is what a reader takes on trust without reading down to the
amendments; leaving the withdrawn half of this decision as the document's own headline is the failure that raised the
ticket. It now ends "TUI shell driven without a terminal". Nothing else above this line is touched.

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
to reconsider when the shell settles". It got the shape right and the parts wrong. No test uses the injector, and none
touches `IApplication` at all — that interface stays where `Program.cs` and `TerminalHost` are, on the production side
of `IShellHost`, which is the whole of what that port is for. Nor does any test start an application: a `ShellWindow`
is a view, and constructing one, calling `Layout()` and raising a `NewKeyDownEvent` needs nothing hosting it. The time
is faked too, but by a hand-written `TimeProvider` rather than Terminal.Gui's own. So the investment this ADR priced —
a Terminal.Gui application harness — was never built. What stands in for it is two files of fake, neither a hundred
lines, which is the same order as the port fakes this ADR already calls cheap.

**What driving it caught that the ports could not.** ADR-0015's first amendment tells the story: a reply's
"answering" block was painted for as long as that ADR had existed and covered by the editor on every frame, so it had
never once been seen. What belongs here is the part about seams rather than about compose. The ports say nothing about
a view laid over another view, and neither does a test of the rows themselves, which passes just as happily over an
editor drawn on top of them — being painted is not being visible, and nothing beneath a window can tell the
difference. The first fix was wrong in the same silent way, leaving the editor's `Y` at 1, because the position is a
`Pos.Func` read only on a layout pass. `ShellComposeLayoutTests` asserts `Frame.Y` after that pass, which is where
both mistakes become sayable at all.

Driving a real window also pins keys that somebody would otherwise reasonably "fix". `k` is the next post and `j` the
one before it, the opposite way round from vim and asked for deliberately (`docs/tui-shell.md`), which is precisely
the kind of thing a well-meaning correction reverses. `ShellKeyTests` presses `Key.K` at a real `ShellWindow`; a test
that calls `Walk(1)` proves nothing about what a reader pressed. That is a guard rather than a catch, and it is worth
the money for the same reason — the ports cannot see a keyboard.

**Where the line falls now.** Pixels are still not asserted, and what is retracted is only "the shell is not driven".
ADR-0014's rule that role selection is the assertable half of rendering is kept exactly as it was: `RoleTests` walks
the role contract and asserts which **role** a thing takes, which is what would have caught `Role.Poll` sitting
themed and documented with nothing drawing it; `NoHardCodedColourTests` scans the TUI sources for a constructed
colour outside the theme, with one named exception for the file that turns a photograph into cells (ADR-0016).
Neither looks at what a cell ends up holding. The instinct underneath this ADR's original sentence — that asserting
drawing is not worth the money — survives intact. What changed is that "which key leaves the shell in which state"
turned out not to be a question about drawing.

**What is still deliberately not done.** No mocking library — Consequences above still holds unchanged, and driving a
window did nothing to reopen it. Every fake the TUI suite uses is hand-written and a few lines long, because the ports
it fakes are one or two calls deep, which is the condition that decision rests on. `HttpMessageHandler` fakes stay
where this ADR and ADR-0006 put them: thin adapters over Mastonet, and the cross-cutting HTTP layer. Driving a window
is not licence to widen them, and does not tempt anyone to — a shell reaches an instance through `Wooly.Core` ports,
so there is no HTTP underneath a TUI test to fake in the first place.
