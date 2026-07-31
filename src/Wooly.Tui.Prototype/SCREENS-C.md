# The C family: one design, four ways to choose a destination

The rail stays, the right-hand column is gone, and a post or an account is something you *drill into* from a
list item and `esc` back out of. What differs between these four is only how you pick a destination — because a
rail you walk through fetches everything you walk past.

The bottom-right corner of every shell counts fetches. Same journey in each: **Home → Follow requests**.

| | Model | Keys for that journey | Fetches |
|---|---|---|---|
| C·0 | Cycle (today's C) | `tab` ×6 | **6, five thrown away** |
| C·1 | Highlight then enter | `tab` `j`×6 `⏎` | **1** |
| C·2 | Direct keys | `q` | **1** |
| C·3 | Jump list | `g` `fol` `⏎` | **1** |

## C·0 — Cycle: what you have now

Six tabs from Home to Follow requests. Every step asked the instance for a timeline nobody wanted to read, and
five of the six answers were thrown away when the next tab overtook them. This is the jerk you were worried about,
and it is worse than it looks: each discarded fetch still spent rate-limit quota.
```
   Home             follow requests
   Local           ▌Maria Ochoa @maria@fosstodon.org                                         ○ 12m
   Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based
   #dotnet         ▌application model made the whole shell testable — no more static state
────────────────── ▌leaking between test runs.
   Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
   Direct messa…1  ▌
▸  Follow reque…2   Ben Whitlock @ben@hachyderm.io                                           ○ 41m
   Search           sixel in 2026 and it still comes down to whether your multiplexer passes the
──────────────────  escape through 🙃
   @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                                         ○ 1h
                    drew the little sheep again. he is thinking about federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

──────────────────  Kev @kev@mas.to                                                           ○ 1h
 213/300 left       ⚠ instance politics, long
 tab/shift-tab destination · j/k post · ⏎ read · a author · esc back      fetches 6 · 5 thrown away
 PROTOTYPE  ◀ F9   C0 — Rail · cycle   F10 ▶                                 F1 notes · Ctrl-Q quit
```

## C·1 — Highlight, then enter

`tab` puts a cursor on the rail, `j`/`k` walk it for free, `⏎` commits, `esc` gives up. The rail can show you
where you are going (`▶`) separately from where you are (`▸`) — nothing is fetched until you say so.
```
▸  Home             home
   Local           ▌Maria Ochoa @maria@fosstodon.org                                         ○ 12m
   Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based
   #dotnet         ▌application model made the whole shell testable — no more static state
────────────────── ▌leaking between test runs.
  ▶Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
   Direct messa…1  ▌
   Follow reque…2   Ben Whitlock @ben@hachyderm.io                                           ○ 41m
   Search           sixel in 2026 and it still comes down to whether your multiplexer passes the
──────────────────  escape through 🙃
   @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                                         ○ 1h
                    drew the little sheep again. he is thinking about federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

──────────────────  Kev @kev@mas.to                                                           ○ 1h
 213/300 left       ⚠ instance politics, long
 j/k walk the rail · ⏎ go there · esc back to the feed                                    fetches 0
 PROTOTYPE  ◀ F9   C1 — Rail · highlight then enter   F10 ▶                  F1 notes · Ctrl-Q quit
```

Same six steps, then enter — one fetch:
```
   Home             follow requests
   Local           ▌Maria Ochoa @maria@fosstodon.org                                         ○ 12m
   Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based
   #dotnet         ▌application model made the whole shell testable — no more static state
────────────────── ▌leaking between test runs.
   Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
   Direct messa…1  ▌
▸  Follow reque…2   Ben Whitlock @ben@hachyderm.io                                           ○ 41m
   Search           sixel in 2026 and it still comes down to whether your multiplexer passes the
──────────────────  escape through 🙃
   @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                                         ○ 1h
                    drew the little sheep again. he is thinking about federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

──────────────────  Kev @kev@mas.to                                                           ○ 1h
 213/300 left       ⚠ instance politics, long
 tab the rail · j/k post · ⏎ read · a author · esc back                                   fetches 1
 PROTOTYPE  ◀ F9   C1 — Rail · highlight then enter   F10 ▶                  F1 notes · Ctrl-Q quit
```

## C·2 — Direct keys

The rail stops being a cursor and becomes a legend: every entry wears the key that goes to it, and nothing is ever
passed through. The catch is the alphabet — `r` already means reply, so follow requests had to take `q`, and every
destination added later has to find a letter nobody is using.
```
 1 Home             notifications
 2 Local           ▌Maria Ochoa @maria@fosstodon.org                                         ○ 12m
 3 Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based
 4 #dotnet         ▌application model made the whole shell testable — no more static state
────────────────── ▌leaking between test runs.
▸n Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
 d Direct messa…1  ▌
 q Follow reque…2   Ben Whitlock @ben@hachyderm.io                                           ○ 41m
 s Search           sixel in 2026 and it still comes down to whether your multiplexer passes the
──────────────────  escape through 🙃
 p @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                                         ○ 1h
                    drew the little sheep again. he is thinking about federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

──────────────────  Kev @kev@mas.to                                                           ○ 1h
 213/300 left       ⚠ instance politics, long
 1-4 timelines · n notifs · d dms · q requests · s search · p profile                     fetches 1
 PROTOTYPE  ◀ F9   C2 — Rail · direct keys   F10 ▶                           F1 notes · Ctrl-Q quit
```

## C·3 — Jump list

The rail becomes a display: where you are, what is waiting. You get anywhere with `g` and enough of the name.
Nothing is passed through, nothing needs its own letter, and it still works at forty destinations.
```
▸  Home             home
   Local           ▌Maria Ochoa @maria@fosstodon.org                                         ○ 12m
   Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based
   #dotnet         ▌a jump to: dir▏                                 — no more static state
────────────────── ▌l   Direct messages  (1 waiting)
   Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
   Direct messa…1  ▌
   Follow reque…2   Ben Whitlock @ben@hachyderm.io                                           ○ 41m
   Search           sixel in 2026 and it still comes down to whether your multiplexer passes the
──────────────────  escape through 🙃
   @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered as coloured blocks
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                                         ○ 1h
                    drew the little sheep again. he is thinking about federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a network diagram
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

──────────────────  Kev @kev@mas.to                                                           ○ 1h
 213/300 left       ⚠ instance politics, long
 type to narrow · ⏎ go · esc close                                                        fetches 0
 PROTOTYPE  ◀ F9   C3 — Rail · jump list   F10 ▶                             F1 notes · Ctrl-Q quit
```

## Drilling in, in all four

`⏎` on a list item opens the post with its replies; `a` opens whoever wrote it; `esc` walks back up. The
breadcrumb along the top is the stack you are standing on.

### The post
```
▸1 Home             home › post by @ben
 2 Local
 3 Federated         Ben Whitlock  @ben@hachyderm.io
 4 #dotnet           41m ago · public
──────────────────
 n Notifications4    sixel in 2026 and it still comes down to whether your multiplexer passes the
 d Direct messa…1    escape through 🙃
 q Follow reque…2
 s Search            ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
──────────────────   ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
 p @jeff             alt: Screenshot of a terminal showing an image rendered as coloured blocks

                     ↺ 3 boosts   ★ 21 favorites   ↩ 11 replies

                     ── replies ──
                     ben@hachyderm.io
                       does it do sixel or is that still on the pile

──────────────────   hazel@mastodon.art
 213/300 left          the instance-based model is the bit I keep telling people about
 1-4 timelines · n notifs · d dms · q requests · s search · p profile                     fetches 0
 PROTOTYPE  ◀ F9   C2 — Rail · direct keys   F10 ▶                           F1 notes · Ctrl-Q quit
```

### The author, from inside the post
```
▸1 Home             home › post by @ben › @ben@hachyderm.io
 2 Local
 3 Federated         Ben Whitlock
 4 #dotnet           @ben@hachyderm.io
──────────────────
 n Notifications4    Writes about terminals, .NET and the slow art of making a CLI feel designed …
 d Direct messa…1
 q Follow reque…2    412 posts · 388 following · 1,204 followers
 s Search            follows you · you follow them
──────────────────
 p @jeff             [f] unfollow    [m] mute    [b] block

                     ── their posts ──

                     41m  sixel in 2026 and it still comes down to whether your multiplexer pass…

                     esc back

──────────────────
 213/300 left
 1-4 timelines · n notifs · d dms · q requests · s search · p profile                     fetches 0
 PROTOTYPE  ◀ F9   C2 — Rail · direct keys   F10 ▶                           F1 notes · Ctrl-Q quit
```

### 80×24

The feed gets everything the right-hand column used to take, which is what makes the narrow terminal survivable
this time — compare against C in SCREENS.md, where the feed was 37 columns.
```
▸  Home             home
   Local           ▌Maria Ochoa @maria@fosstodon.org                     ○ 12m
   Federated       ▌Finally shipped the terminal client rewrite. Terminal.Gui
   #dotnet         ▌v2's instance-based application model made the whole
────────────────── ▌shell testable — no more static state leaking between
   Notifications4  ▌↺ 12   ★ 34   ↩ 5    ⏎ read · a author
   Direct messa…1  ▌
   Follow reque…2   Ben Whitlock @ben@hachyderm.io                       ○ 41m
   Search           sixel in 2026 and it still comes down to whether your
──────────────────  multiplexer passes the escape through 🙃
   @jeff            ▒▒▒▒ Screenshot of a terminal showing an image rendered…
                    ↺ 3   ★ 21   ↩ 11    ⏎ read · a author

                    ↺Jeff · Hazel @hazel@mastodon.art                     ○ 1h
                    drew the little sheep again. he is thinking about
                    federation.
                    ▒▒▒▒ A cartoon sheep in a wool jumper looking at a netw…
                    ↺ 340   ★ 1290   ↩ 44    ⏎ read · a author

                    Kev @kev@mas.to                                       ○ 1h
──────────────────  ⚠ instance politics, long
 213/300 left       ↺ 8   ★ 16   ↩ 62    ⏎ read · a author
 tab the rail · j/k post · ⏎ read · a author · esc back               fetches 0
 PROTOTYPE  ◀ F9   C1 — Rail · highlight then enter   F10 ▶
```
