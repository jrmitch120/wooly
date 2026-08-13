# Handoff: `g` refresh moves the page (#84)

Branch `issue-84-refresh`, PR #105. Three attempts at this bug have failed. All three were validated by tests that
**could not have caught it**, for one reason, given in full below.

Read "Root cause" first. It is verified by reading the code, not inferred from a failing test — no test that can fail
on it exists yet, and writing one is step 1.

## The symptom

Reported three times against a real timeline. The reporter's own words, which are exact:

> refreshing is, selecting the top most visible post on the page and moving that to the top

Repro: open home, `PgDn` a few times so the page is part way through a post, press `g`. The post at the top of the
page — which is usually showing only its tail — jumps so that it starts at the top of the page. Screenshots on PR #105
(comment thread, third report) show it: before, The Onion's video link and counts at the top; after, The Onion whole
from its byline.

Nobody has confirmed the reporter is running a build with any given commit in it. **Ask.** It is cheap and it has
never been ruled out. That said, the root cause below predicts this exact symptom for `a6533d5` and for `08388c2`
alike, so a stale build is not needed to explain it.

## Root cause

`IShellHost.OnUiThread` is implemented two ways, and the whole of the refresh path depends on which one it is:

| | Implementation | Behaviour |
|---|---|---|
| `TerminalHost` (the app) | `application.Invoke(work)` | **Queues** `work` on the main loop. Returns immediately. |
| `FakeShellHost` (every test) | `work()` | **Runs `work` inline**, before returning. |

`Enquiry.Put` ends like this (`src/Wooly.Tui/Shell/Enquiry.cs`):

```csharp
answer = await question(new Ask(this));   // the HTTP call
...
Apply(() =>                               // Apply == host.OnUiThread
{
    eitherWay?.Invoke(answer);
    if (from == _asked) ifStillHere?.Invoke(answer);
});
```

`ifStillHere` is where the refreshed screen is built and put on the stack. In the app that callback runs **after
`Put`'s task has already completed**. In tests it runs **before**. Two consequences, both fatal:

**1. `Shell.Refresh` returns `false` in the app, always.** `RefreshDestination`/`RefreshPost`/`RefreshAccount` set
their `landed`/`freshened` flag inside `ifStillHere` and return it after the `await`. In the app the flag is still
`false` at that point, so `ShellWindow.Refreshed()` never calls `_content.Resume(into)` — the entire fix in `08388c2`
is dead code in production and fully live in tests.

**2. `Restart()` runs last and wins.** The order in the app is:

1. `g` → `Refreshed()` captures `into`, awaits `_shell.Refresh(...)`.
2. `Put` queues its callback and completes. `Refreshed()` resumes, sees `false`, does nothing.
3. Main loop drains the queue → `Landed`/`Freshened` → `Shows`/`Reset` → `Changed` → `ShellWindow.Refresh()` → screen
   identity differs → `_content.Restart()`.
4. `Restart()` is `_top = Scroll.Begins(rows)` since `a6533d5` — the first row of what is picked out. What is picked
   out is the post the reader was reading, because `48bbd49` reclaims it.

Step 4 **is** the reported symptom, stated in code: *take the topmost visible post, put its first row at the top of
the page.* The reporter described the behaviour of `Restart` precisely.

In tests the same sequence is 1 → 3 → 4 → 2, so `Resume` lands last and everything passes.

**3. There is a threading bug in the same place.** Terminal.Gui installs no `SynchronizationContext`, so the
continuation after `await _shell.Refresh(...)` in `ShellWindow.Refreshed()` runs on a **thread-pool thread**. It then
touches `_content` — view state — off the drawing thread. That is exactly what `OnUiThread` exists to prevent
(`Shell`'s class remark: "the two things it needs a terminal for — waiting, and getting back onto the drawing
thread"). It needs fixing whatever else changes.

## Why every test passed

`FakeShellHost.OnUiThread` runs inline, and its remark says why: *"Run where it was asked for. A test has one thread,
and it is the drawing one."* That is reasonable for asserting shell state. It is **not** a faithful model of ordering,
and every assertion about what happens after a fetch lands inherits the wrong order.

Two further gaps in the same direction:

- **No test ever draws.** `PaintedView._top` is settled inside `Rows()`, called from `OnDrawingContent`/`Settle`.
  Tests have no driver and never paint, so `_top` is only ever what `Step`/`Turn`/`Restart`/`Resume` set directly.
  The clamping and the `_following` branch that run on every real frame never run in a test.
- **The harnesses modelled the draw by hand** (`Scroll.To(lines, height, Scroll.Begins(lines))` and friends). That
  models what the code *should* do, so it agrees with the code by construction and cannot catch a wrong ordering
  between the shell and the view.

**A test that could have caught this** would drive `IShellHost.OnUiThread` the way the app does: queue the work, and
let the test drain the queue at a chosen moment. `FakeShellHost` already has this shape for `After`/`Settle`. Adding
the same for `OnUiThread` — queue by default, `Settle()` drains — would have failed all three attempts immediately.
That is the single highest-value change here and it belongs before any further fix.

## What each commit actually did

| Commit | Intent | Verdict |
|---|---|---|
| `0ce61e7` | `g` on nine screens; evict cache, re-fetch, restore pick by id | Sound. Shell-side, unaffected by the ordering bug. |
| `0b149c7` | Review answers: `Arrival.Again` so a failed refresh doesn't blank the screen; `ReadReplies` | Sound, keep. |
| `48bbd49` | Reclaim the post being read, since the arrows leave the pick behind | **Correct and still needed.** Shell-side. |
| `a6533d5` | `Restart` starts at the pick (`Scroll.Begins`) rather than row 0 | **This is what produces the reported symptom.** Wrong idea — see below. |
| `08388c2` | Keep the row within the post (`Scroll.Into` / `Resume`), gated on `Shell.Refresh` returning `true` | Right idea, **never executes in the app** (consequence 1). |

`a6533d5` should probably be reverted in substance: with `08388c2`'s offset restoration working, `Restart` has no
reason to start anywhere but row 0, and starting it at the pick is what makes a broken refresh land on the reported
symptom rather than somewhere obviously wrong. Keep `Scroll.Begins` only if `Resume` still needs it as its base.

## Suggested way in

1. **Make `FakeShellHost.OnUiThread` queue**, with an explicit drain. Expect a large number of existing tests to fail
   or need a drain call — that failure set is itself information about what else assumes inline ordering.
2. **Write the failing test** at the window seam: page down, press `g`, drain, assert `content.Into` and the topmost
   post are both unchanged. It must fail before anything is fixed.
3. **Fix the ordering.** The shape that removes the race rather than working around it: have the *shell* carry the
   fact that a refresh replaced a screen, and have `ShellWindow.Refresh()` — which already runs on the drawing thread,
   in the right order, after the screen is on the stack — decide between `Restart()` and `Resume(into)` there. That
   means the window captures `into` when `g` is pressed and holds it until the next screen change, rather than
   awaiting anything. `Shell.Refresh` can then go back to returning `Task` and `ShellWindow.Refreshed()` can go away,
   along with its off-thread view access.
   - The held `into` must be discarded if the next screen change is *not* the refresh landing (reader pressed `esc`,
     tabbed away, opened a post). Keying it to the screen instance that was showing when `g` was pressed is the
     obvious guard: apply it only if that instance is the one being replaced.
4. **Re-check the other `Apply`-boundary callers** for the same assumption. `Dismiss`, `AnswerRequest`, `MarkRead`,
   `Send`, `Delete` and `Tie` all do work inside `eitherWay`/`ifStillHere`. Nothing there returns a value across the
   boundary, so they are probably fine — but "probably" is what got us here.

## What has not been established

- Whether the reporter's build included any given commit. Ask before anything else.
- Whether `application.Invoke` ever runs inline when called from the drawing thread. The fix above is correct either
  way, but it changes how urgent the race is.
- Whether the reporter wants a refresh to keep the page still (what `08388c2` intends) or to show newly-arrived posts
  at the top. Everything so far has assumed the former, from the ticket's "the reader's pick follows the post it was
  on". It has not been confirmed with them in those words.

## Contract state

`docs/tui-shell.md` "What refresh settled" and `CONTEXT.md`'s **Place** were amended by these commits and currently
describe the *intended* behaviour of `08388c2` — including that a refresh is the exception to "the offset starts again
whenever the screen is replaced". If the approach changes, both need revisiting; the "two halves of a place" framing
is sound regardless of how the ordering is fixed.
