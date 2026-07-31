# TUI shell prototype — throwaway

**The question:** what shape is the Wooly TUI? Not "what colour is the timeline" — the shape of the *shell*, which is
the thing #28 settles and #29 (notifications / search / account / follow-requests) and #30 (DMs) then inherit. Once the
shell says how you get to a second screen, every later screen is that answer repeated.

Two rounds live here. **Round one** is A–D below: four structurally different shells. **Round two** is C·0–C·3
([SCREENS-C.md](SCREENS-C.md)): the rail kept, the right-hand column dropped, drill-down added, and four answers to
*how you choose a destination without fetching everything you walk past*. Switch between all of them live with
**F9 / F10**.

```
dotnet run --project src/Wooly.Tui.Prototype              # opens A
dotnet run --project src/Wooly.Tui.Prototype -- -v c      # opens C
dotnet run --project src/Wooly.Tui.Prototype -- -v c1     # opens the rail with a deferred cursor
```

Nothing here talks to an instance. Every post, notification, conversation and follow request is fake, every action says
what it would have done and does nothing, and there is no auth, no config and no persistence. The loud magenta bar on
the bottom row is the prototype's own switcher, not a thing the real TUI would have.

If you would rather read than run: **[SCREENS.md](SCREENS.md)** has every shell drawn as text, at 100×30 and at 80×24.

This project is deliberately **not** in `Wooly.slnx`, so it never builds, tests or ships with the solution.

## The four

| | Shell | How you reach a second screen | The bet |
|---|---|---|---|
| **A** | Tabbed reader | Modal window over the timeline | Familiarity. Tabs across the top, one column of cards, `n` / `m` / `/` open notifications, DMs and search *over* what you were reading. |
| **B** | Split reading pane | The right-hand pane swaps | A mail reader. A dense one-line index on the left, the whole post on the right — the pane is where a thread, an account or a compose form opens, so nothing is modal. |
| **C** | Workspace rail | A rail entry, always visible | Everything on screen at once. Destinations down the left carrying their own unread counts, feed in the middle, who-wrote-this and where-you-stand on the right, quota in the corner. |
| **D** | Command bar | You type it | One vocabulary. No chrome but two rows; `j`/`k`/`b`/`f` for the common things and `:timeline federated`, `:boost 3` — the CLI's own verbs — for the rest. |

## Round two: the C family

C won on its rail and lost on its right-hand column, so round two keeps the rail, gives the feed the whole width, and
makes a post or an account something you **drill into** from a list item (`⏎` reads it, `a` opens the author, `esc`
walks back up a breadcrumb).

They differ in one thing only: **how a destination is chosen without paying for the ones you pass through.** C·0 is
the anchored answer (ADR-0014) — cycling kept, and the cost removed by waiting for the rail to be still rather than by
asking the user for an extra keystroke. Every shell counts fetches in the bottom-right corner, the
fake instance answers in 450ms, and an answer overtaken before it lands is thrown away — so the cost of a selection
model is a number on screen rather than a feeling.

| | Model | Home → Follow requests | Fetches | Where it hurts |
|---|---|---|---|---|
| **C·0** | Cycle, settling — **the anchor** (ADR-0014) | `tab` ×6 | **1** | Nothing, once it waits 180ms for the rail to be still. Asking on every keypress instead cost 6 with 5 discarded, which is what the other three were measured against. |
| **C·1** | Highlight, then enter | `tab` `j`×6 `⏎` | **1** | Two cursors to explain: `▸` where you are, `▶` where you are going. The rail holds the keyboard while it has the cursor. |
| **C·2** | Direct keys | `q` | **1** | The alphabet. `r` is already reply, so requests took `q` — and every destination added later needs a spare letter. |
| **C·3** | Jump list — `g`, then type | `g` `fol` `⏎` | **1** | One extra keystroke, and the only shape that has to be taught. Scales past nine destinations, which the other three do not. |

`shot.py` will replay any of it: `python3 src/Wooly.Tui.Prototype/shot.py c1 100 24 'Tab,j,j,j,j,j,j,Enter'`.

## What to look at

1. **Where does the second screen go?** This is the whole decision. A hides the timeline to show you notifications; B
   and C never do; D shows you whatever you asked for. #29 and #30 are three more screens each — imagine them here.
2. **80×24.** Look at the bottom of SCREENS.md before deciding. C's feed drops to 37 columns and B's index preview to
   about 12 characters; A and D lose nothing. A shape that only works at 120 columns is a shape that fails on a laptop
   in a split pane.
3. **Where the rate-limit quota lives** (spec story 54). C has an obvious always-visible home for it. A, B and D each
   have to give up part of one row.
4. **Discoverability against speed.** D is the fastest to use and the only one that tells a newcomer nothing. The spec
   has no story for in-app help; if D wins, it needs one.
5. **Reading density.** B shows twelve posts and one whole post at once; A shows four posts; C shows six.

## What building it turned up

Findings, not opinions — each cost a debugging round or showed up on screen:

- **`Post` is missing what a screen needs.** No media attachments read back from an instance (`MediaAttachment` is
  upload-side only) and no viewer state — whether *this* profile has favorited, boosted or pinned it. A timeline cannot
  draw a lit star or an inline image from today's `Post`, so #28 has to widen it and **#31 is blocked on the same
  gap**. The prototype models the difference explicitly as `FeedItem` (see `Sample.cs`) — that record is the shopping
  list.
- **Terminal.Gui v2.4 can be driven headlessly.** `IApplication.GetInputInjector()` plus `VirtualTimeProvider` inject
  keys and run deterministically; `shot.py` here uses it to screenshot the shells with nobody at the keyboard. The spec
  puts headless TUI testing out of scope for v1 on the assumption it is impractical — that assumption is now cheap to
  revisit, at least for keybinding-level smoke tests.
- **A `KeyDown` handler on a container only sees what the focused view did not consume.** It is a bubble-up hook, not
  a pre-process one — `j` and `k` never reached the shell because the feed handled them first, which looked exactly
  like a dead keymap. A shell-level keymap has to be installed ahead of the focused view (`LineFeed.Intercept` here) or
  at the application level. Cost the second debugging round of this prototype.
- **Subviews draw in the order they were added, and a window draws its own content *before* them.** An overlay painted
  in the window's `OnDrawingContent` ends up underneath everything; it has to be its own view, added last.
- **A container view with `CanFocus = false` silently eats every keystroke of its children.** Keys still reach the
  window's `KeyDown`, so the shell looks alive while nothing inside it responds. The real shell should set `CanFocus`
  deliberately on every container, and take focus from `Initialized` — a `SetFocus()` in a constructor is dropped.
- **`string.Length` is not display width.** The Japanese post overflows its column in B's index (visible in SCREENS.md)
  and emoji do the same by one cell. Anything that measures or pads text has to measure in terminal cells.
- **The instance-based application reaches further than expected.** `MessageBox.Query` and friends take an
  `IApplication`, so screens need the app handed to them — worth deciding early whether that arrives by constructor
  injection alongside the `IMastodonClient` ports or by `View.GetApp()`.

## The pieces

| | |
|---|---|
| `Chrome.cs` | The variant registry, the F9/F10 switcher, and the loud bar. The only thing all four share. |
| `LineFeed.cs` | Scrolling and selection over posts of uneven height. Plumbing, not design — each variant still composes its own rows. |
| `Sample.cs` | The fake timeline, notifications, conversations and requests. `FeedItem` is where the domain gaps live. |
| `Ink.cs` | Colours, relative times, wrapping, clipping. |
| `Variants/*.cs` | One file per shell of round one. No shared layout — each is free to throw the whole thing out. |
| `Variants/RailShell.cs` | Round two's shared design: rail, drill-down stack, and the fake instance that counts what it was asked. Shared on purpose here — the whole point is that only the selection model differs. |
| `Variants/RailModes.cs` | The four selection models, one class each. |
| `shot.py` | Runs a shell in a pty of a given size, injects keys, replays the frame as text. Generates SCREENS.md. |

```
python3 src/Wooly.Tui.Prototype/shot.py c 100 30 'Tab,Tab,Tab,Tab,Tab,Tab'
```

## When it has answered

Fold the winning shape into the real `Wooly.Tui` **rewritten**, not copied — this code has no tests, no error handling
and no accessibility work. Then record the answer and why on #28, and leave this whole project on its throwaway branch.
It is the primary source for the decision; it does not belong on `main`.
