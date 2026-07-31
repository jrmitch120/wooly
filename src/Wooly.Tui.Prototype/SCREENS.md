# The four shells, drawn

Captured from the running prototype with `shot.py` (a pty at a fixed size, keys injected through Terminal.Gui's
test input injector, the frame replayed as text). Colour is lost here — run it for that.

```
python3 src/Wooly.Tui.Prototype/shot.py c 100 30 'Tab,Tab,Tab,Tab,Tab,Tab'
```

## A — Tabbed reader · 100×30
```
 Wooly — jeff@hachyderm.io   ·   home
─▸Home ─ Local ─ Federated ─ #dotnet ────────────────────────────────────────────── tab / shift-tab
▌ Maria Ochoa  @maria@fosstodon.org                                                          ○ 12m
▌  Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application model
▌  made the whole shell testable — no more static state leaking between test runs.
▌  ↺ 12   ★ 34   ↩ 5
▌──────────────────────────────────────────────────────────────────────────────────────────────────
  Ben Whitlock  @ben@hachyderm.io                                                            ○ 41m
   sixel in 2026 and it still comes down to whether your multiplexer passes the escape through 🙃
   ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
   ↺ 3   ★ 21   ↩ 11
 ──────────────────────────────────────────────────────────────────────────────────────────────────
   ↺ Jeff Mitchell boosted
  Hazel  @hazel@mastodon.art                                                                  ○ 1h
   drew the little sheep again. he is thinking about federation.
   ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
   ↺ 340   ★ 1290   ↩ 44
 ──────────────────────────────────────────────────────────────────────────────────────────────────
  Kev  @kev@mas.to                                                                            ○ 1h
   ⚠ instance politics, long  — press x to read
   ↺ 8   ★ 16   ↩ 62
 ──────────────────────────────────────────────────────────────────────────────────────────────────
  Sam ⚡  @sam@chaos.social                                                                    ○ 2h
   which do you actually use day to day?
   ██████████████████  412  tmux
   ███···············   88  zellij
   █·················   31  screen
   ███████████·······  260  neither, I have 40 windows
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ n  Notifs │ m  DMs
 PROTOTYPE  ◀ F9   A — Tabbed reader   F10 ▶                                 F1 notes · Ctrl-Q quit
```

### A — notifications, as a modal over the timeline (`n`)
```
 Wooly — jeff@hachyderm.io   ·   home
─▸Home ─ Local ─ Federated ─ #dotnet ────────────────────────────────────────────── tab / shift-tab
▌ Maria Ochoa  @maria@fosstodon.org                                                          ○ 12m
▌  Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application model
▌  made the whole shell testable — no more static state leaking between test runs.
▌  ↺ 12   ★ 34   ↩┏┥Notifications (modal)┝━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
▌─────────────────┃mention   Maria Ochoa     8m  @jeff does Wooly do sixel yet…┃───────────────────
  Ben Whitlock  @b┃favorite  Theo           33m  Pinned: I write about .NET, t…┃             ○ 41m
   sixel in 2026 a┃    follow    Priya           2h  started following you     ┃escape through 🙃
   ▒▒▒▒ Screenshot┃boost     Ben Whitlock    5h  Pinned: I write about .NET, t…┃
   ↺ 3   ★ 21   ↩ ┃                                                            ┃
 ─────────────────┃                  d dismiss · D clear all                   ┃───────────────────
   ↺ Jeff Mitchell┃                                                            ┃
  Hazel  @hazel@ma┃                        ⟦► Close ◄⟧▖                        ┃              ○ 1h
   drew the little┃                        ▝▀▀▀▀▀▀▀▀▀▀▘                        ┃
   ▒▒▒▒ A cartoon ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
   ↺ 340   ★ 1290   ↩ 44
 ──────────────────────────────────────────────────────────────────────────────────────────────────
  Kev  @kev@mas.to                                                                            ○ 1h
   ⚠ instance politics, long  — press x to read
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ n  Notifs │ m  DMs
 PROTOTYPE  ◀ F9   A — Tabbed reader   F10 ▶                                 F1 notes · Ctrl-Q quit
```

## B — Split reading pane · 100×30
```
 Wooly  jeff@hachyderm.io  │  Home  (tab switches)                                    quota 213/300
▌12m    ★ maria        Finally shipped the t… │
 41m   ▒  ben          sixel in 2026 and it … │ Maria Ochoa
  1h ↺ ▒  hazel        drew the little sheep… │ @maria@fosstodon.org
  1h  ⚠   kev          instance politics, lo… │ 12m ago · public
  2h      sam          which do you actually… │
  3h      jeff         Pinned: I write about… │ Finally shipped the terminal client rewrite.
  4h      rin          端末でマストドンを読む │ Terminal.Gui v2's instance-based application model
  6h      ops          Maintenance window to… │ made the whole shell testable — no more static
  8h      dana         followers-only: inter… │ state leaking between test runs.
 15h    ★ theo         a client that is good… │
  1d      lu           here is the mp4 of th… │
  1d      gil          hot take: the reason … │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │
                                              │ ↺ 12 boosts   ★ 34 favorites   ↩ 5 replies
                                              │
                                              │
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ a  Account
 PROTOTYPE  ◀ F9   B — Split reading pane   F10 ▶                            F1 notes · Ctrl-Q quit
```

### B — a post further down the index (`j` ×6)
```
 Wooly  jeff@hachyderm.io  │  Home  (tab switches)                                    quota 213/300
 12m    ★ maria        Finally shipped the t… │
 41m   ▒  ben          sixel in 2026 and it … │ りん
  1h ↺ ▒  hazel        drew the little sheep… │ @rin@mstdn.jp
  1h  ⚠   kev          instance politics, lo… │ 4h ago · public
  2h      sam          which do you actually… │
  3h      jeff         Pinned: I write about… │ 端末でマストドンを読むのが好き。フォントさえ合えばぜal client rewrite.
▌ 4h      rin          端末でマストドンを読む │
  6h      ops          Maintenance window to… │
  8h      dana         followers-only: inter… │
 15h    ★ theo         a client that is good… │
  1d      lu           here is the mp4 of th… │
  1d      gil          hot take: the reason … │
                                              │
                                              │
                                              │
                                              │
                                              │ ↺ 5 boosts   ★ 30 favorites   ↩ 2 replies
                                              │
                                              │
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ a  Account
 PROTOTYPE  ◀ F9   B — Split reading pane   F10 ▶                            F1 notes · Ctrl-Q quit
```

## C — Workspace rail · 100×30
```
▸Home              ▌Maria Ochoa @maria                                 12m   │
 Local             ▌Finally shipped the terminal client rewrite.             │ Maria Ochoa
 Federated         ▌Terminal.Gui v2's instance-based application model       │ @maria@fosstodon.org
 #dotnet           ▌↺12 ★34 ↩5                                               │
────────────────── ▌                                                         │ follows you
 Notifications  4   Ben Whitlock @ben                                  41m   │ you follow them
 Direct messa…  1   sixel in 2026 and it still comes down to whether your    │
 Follow reque…  2   multiplexer passes the escape through 🙃                 │ [F] unfollow
 Search             ▒▒▒▒ Screenshot of a terminal showing an image rend…     │ [M] mute
──────────────────  ↺3 ★21 ↩11                                               │ [B] block
 @jeff                                                                       │
                    ↺Jeff Hazel @hazel                                  1h   │ ── this post ──
                    drew the little sheep again. he is thinking about        │ 12m · public
                    federation.                                              │ ↺ 12
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a …     │ ★ 34
                    ↺340 ★1290 ↩44                                           │ ↩ 5
                                                                             │
                    Kev @kev                                            1h   │
                    ⚠ instance politics, long                                │
                    ↺8 ★16 ↩62                                               │
                                                                             │
                    Sam ⚡ @sam                                          2h  │
                    which do you actually use day to day?                    │
                    ↺1 ★4 ↩9                                                 │
                                                                             │
                    Jeff Mitchell @jeff                                 3h   │
──────────────────  Pinned: I write about .NET, terminals and the slow       │
 213/300 left       art of making a CLI feel like it was designed on         │
 tab destination · j/k post · c compose · r reply · b boost · f fav · d delete
 PROTOTYPE  ◀ F9   C — Workspace rail   F10 ▶                                F1 notes · Ctrl-Q quit
```

### C — direct messages, in the same window (rail: `Tab` ×6)
```
 Home               FOLLOW REQUESTS                                          │
 Local                                                                       │ Maria Ochoa
 Federated          Priya  @priya@mastodon.social                            │ @maria@fosstodon.org
 #dotnet               infra, cats, 3 posts                                  │
──────────────────     [a]ccept   [r]eject                                   │ follows you
 Notifications  4                                                            │ you follow them
 Direct messa…  1   ‌  @nobody@spam.example                                  │
▸Follow reque…  2      no posts, no avatar, joined today                     │ [F] unfollow
 Search                [a]ccept   [r]eject                                   │ [M] mute
──────────────────                                                           │ [B] block
 @jeff                                                                       │
                                                                             │ ── this post ──
                                                                             │ 12m · public
                                                                             │ ↺ 12
                                                                             │ ★ 34
                                                                             │ ↩ 5
                                                                             │
                                                                             │
──────────────────                                                           │
 213/300 left                                                                │
 tab destination · j/k post · c compose · r reply · b boost · f fav · d delete
 PROTOTYPE  ◀ F9   C — Workspace rail   F10 ▶                                F1 notes · Ctrl-Q quit
```

### C — follow requests, likewise (`Tab` ×7)
```
 Home               SEARCH                                                   │
 Local                                                                       │ Maria Ochoa
 Federated          ┌────────────────────────────────────┐                   │ @maria@fosstodon.org
 #dotnet            │ sixel                              │                   │
──────────────────  └────────────────────────────────────┘                   │ follows you
 Notifications  4                                                            │ you follow them
 Direct messa…  1   accounts · hashtags · posts (tab to filter)              │
 Follow reque…  2                                                            │ [F] unfollow
▸Search                                                                      │ [M] mute
──────────────────                                                           │ [B] block
 @jeff                                                                       │
                                                                             │ ── this post ──
                                                                             │ 12m · public
                                                                             │ ↺ 12
                                                                             │ ★ 34
                                                                             │ ↩ 5
──────────────────                                                           │
 213/300 left                                                                │
 tab destination · j/k post · c compose · r reply · b boost · f fav · d delete
 PROTOTYPE  ◀ F9   C — Workspace rail   F10 ▶                                F1 notes · Ctrl-Q quit
```

## D — Command bar · 100×30
```
  1▌ maria@fosstodon.org · Maria Ochoa · 12m · public
     Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application
     model made the whole shell testable — no more static state leaking between test runs.
     ↺ 12  ★ 34  ↩ 5

  2  ben@hachyderm.io · Ben Whitlock · 41m · public
     sixel in 2026 and it still comes down to whether your multiplexer passes the escape through 🙃
     ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
     ↺ 3  ★ 21  ↩ 11

  3  ↺jeff hazel@mastodon.art · Hazel · 1h · public
     drew the little sheep again. he is thinking about federation.
     ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
     ↺ 340  ★ 1290  ↩ 44

  4  kev@mas.to · Kev · 1h · public
     ⚠ instance politics, long (:show 4)
     ↺ 8  ★ 16  ↩ 62

  5  sam@chaos.social · Sam ⚡ · 2h · public
     which do you actually use day to day?
     ·  412  tmux
     ·   88  zellij
     ·   31  screen
     ·  260  neither, I have 40 windows
     ↺ 1  ★ 4  ↩ 9

────────────────────────────────────────────────────────────────────────────────────────────────────
[home] 12 posts · post 1 · quota 213/300
 PROTOTYPE  ◀ F9   D — Command bar   F10 ▶                                   F1 notes · Ctrl-Q quit
```

### D — the command line, carrying the CLI's own verbs
```
  1  maria@fosstodon.org · Maria Ochoa · 12m · public
     Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application
     model made the whole shell testable — no more static state leaking between test runs.
     ↺ 12  ★ 34  ↩ 5

  2  ben@hachyderm.io · Ben Whitlock · 41m · public
     sixel in 2026 and it still comes down to whether your multiplexer passes the escape through 🙃
     ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
     ↺ 3  ★ 21  ↩ 11

  3▌ ↺jeff hazel@mastodon.art · Hazel · 1h · public
     drew the little sheep again. he is thinking about federation.
     ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
     ↺ 340  ★ 1290  ↩ 44

  4  kev@mas.to · Kev · 1h · public
     ⚠ instance politics, long (:show 4)
────────────────────────────────────────────────────────────────────────────────────────────────────
:timeline federated▏
 PROTOTYPE  ◀ F9   D — Command bar   F10 ▶                                   F1 notes · Ctrl-Q quit
```

## The same four in an 80×24 terminal

This is where the shapes stop being a matter of taste.

### A · 80×24
```
 Wooly — jeff@hachyderm.io   ·   home
─▸Home ─ Local ─ Federated ─ #dotnet ────────────────────────── tab / shift-tab
▌ Maria Ochoa  @maria@fosstodon.org                                      ○ 12m
▌  Finally shipped the terminal client rewrite. Terminal.Gui v2's
▌  instance-based application model made the whole shell testable — no more
▌  static state leaking between test runs.
▌  ↺ 12   ★ 34   ↩ 5
▌──────────────────────────────────────────────────────────────────────────────
  Ben Whitlock  @ben@hachyderm.io                                        ○ 41m
   sixel in 2026 and it still comes down to whether your multiplexer passes
   the escape through 🙃
   ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blo…
   ↺ 3   ★ 21   ↩ 11
 ──────────────────────────────────────────────────────────────────────────────
   ↺ Jeff Mitchell boosted
  Hazel  @hazel@mastodon.art                                              ○ 1h
   drew the little sheep again. he is thinking about federation.
   ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
   ↺ 340   ★ 1290   ↩ 44
 ──────────────────────────────────────────────────────────────────────────────
  Kev  @kev@mas.to                                                        ○ 1h
   ⚠ instance politics, long  — press x to read
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ n  Notifs │ m  DMs
 PROTOTYPE  ◀ F9   A — Tabbed reader   F10 ▶             F1 notes · Ctrl-Q quit
```

### B · 80×24 — the index preview falls to about 12 characters
```
 Wooly  jeff@hachyderm.io  │  Home  (tab switches)                quota 213/300
▌12m    ★ maria        Finally shi… │
 41m   ▒  ben          sixel in 20… │ Maria Ochoa
  1h ↺ ▒  hazel        drew the li… │ @maria@fosstodon.org
  1h  ⚠   kev          instance po… │ 12m ago · public
  2h      sam          which do yo… │
  3h      jeff         Pinned: I w… │ Finally shipped the terminal client
  4h      rin          端末でマスト │ rewrite. Terminal.Gui v2's
  6h      ops          Maintenance… │ instance-based application model made
  8h      dana         followers-o… │ the whole shell testable — no more
 15h    ★ theo         a client th… │ static state leaking between test runs.
  1d      lu           here is the… │
  1d      gil          hot take: t… │
                                    │
                                    │
                                    │
                                    │
                                    │
                                    │
                                    │ ↺ 12 boosts   ★ 34 favorites   ↩ 5 repl…
                                    │
                                    │
 Enter  Thread │ c  Compose │ r  Reply │ b  Boost │ f  Fav │ a  Account
 PROTOTYPE  ◀ F9   B — Split reading pane   F10 ▶        F1 notes · Ctrl-Q quit
```

### C · 80×24 — rail and context pane leave the feed 37 columns
```
▸Home              ▌Maria Ochoa @maria             12m   │
 Local             ▌Finally shipped the terminal         │ Maria Ochoa
 Federated         ▌client rewrite. Terminal.Gui v2's    │ @maria@fosstodon.org
 #dotnet           ▌↺12 ★34 ↩5                           │
────────────────── ▌                                     │ follows you
 Notifications  4   Ben Whitlock @ben              41m   │ you follow them
 Direct messa…  1   sixel in 2026 and it still comes     │
 Follow reque…  2   down to whether your multiplexer     │ [F] unfollow
 Search             ▒▒▒▒ Screenshot of a terminal s…     │ [M] mute
──────────────────  ↺3 ★21 ↩11                           │ [B] block
 @jeff                                                   │
                    ↺Jeff Hazel @hazel              1h   │ ── this post ──
                    drew the little sheep again. he      │ 12m · public
                    is thinking about federation.        │ ↺ 12
                    ▒▒▒▒ A cartoon sheep in a wool …     │ ★ 34
                    ↺340 ★1290 ↩44                       │ ↩ 5
                                                         │
                    Kev @kev                        1h   │
                    ⚠ instance politics, long            │
                    ↺8 ★16 ↩62                           │
──────────────────                                       │
 213/300 left       Sam ⚡ @sam                      2h  │
 tab destination · j/k post · c compose · r reply · b boost · f fav · d delete
 PROTOTYPE  ◀ F9   C — Workspace rail   F10 ▶            F1 notes · Ctrl-Q quit
```

### D · 80×24 — the only one that loses nothing
```
  1▌ maria@fosstodon.org · Maria Ochoa · 12m · public
     Finally shipped the terminal client rewrite. Terminal.Gui v2's
     instance-based application model made the whole shell testable — no more
     static state leaking between test runs.
     ↺ 12  ★ 34  ↩ 5

  2  ben@hachyderm.io · Ben Whitlock · 41m · public
     sixel in 2026 and it still comes down to whether your multiplexer passes
     the escape through 🙃
     ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured bl…
     ↺ 3  ★ 21  ↩ 11

  3  ↺jeff hazel@mastodon.art · Hazel · 1h · public
     drew the little sheep again. he is thinking about federation.
     ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
     ↺ 340  ★ 1290  ↩ 44

  4  kev@mas.to · Kev · 1h · public
     ⚠ instance politics, long (:show 4)
     ↺ 8  ★ 16  ↩ 62

────────────────────────────────────────────────────────────────────────────────
[home] 12 posts · post 1 · quota 213/300
 PROTOTYPE  ◀ F9   D — Command bar   F10 ▶               F1 notes · Ctrl-Q quit
```
