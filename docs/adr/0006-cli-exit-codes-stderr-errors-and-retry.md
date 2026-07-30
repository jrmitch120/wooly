# Reserved CLI exit codes, errors on stderr, bounded retries, and fail-fast rate limiting

Every command needs the same failure behavior, and no command should have to reimplement it. Four decisions cover it, all applied centrally rather than per command.

**Exit codes are a small reserved set** (`Wooly.Cli.ExitCode`): `0` success, `1` general error, `2` usage error, `3` authentication error, `4` network error, `5` rate-limited. Scripts branch on these, so the numbers are part of the CLI's public contract — new codes may be appended, existing ones are never renumbered or repurposed. `3` is reserved ahead of the authentication work so it doesn't have to be carved out of an already-published range later.

**Errors go to stderr, results go to stdout.** Commands never write their own error text; they throw, and one exception handler registered on the `CommandApp` renders the failure to a stderr-backed console and maps its type to an exit code. That keeps the two streams separable — piping stdout into another tool can never pick up error text — and means the mapping from failure to exit code lives in exactly one readable place. Failures meant for the user derive from `WoolyException` and are printed as a plain message; anything else is a defect in this client and is printed with a stack trace so it can be reported.

**Transient network faults are retried twice, ~250ms then ~750ms apart**, in a delegating handler on the shared `HttpClient`, so retries apply to every call either front end makes. Only a failure to reach the instance is retried. An HTTP response is never retried, not even a 5xx: the instance already received the request, and resending it could publish a post twice. Nor is a cancellation — `HttpClient` hands the handler a token already linked to its own timeout, so by the time a cancellation arrives the handler cannot tell a caller's Ctrl-C from `HttpClient.Timeout` elapsing, and the timeout budget covers the whole send anyway, leaving a retry none of it. The backoff is expressed as the list of waits (`RetryPolicy`), so "how many retries" and "how long between them" are one value rather than a count plus a formula.

**A rate limit fails fast and is never waited out inside the shared layer.** A `429` becomes a `RateLimitedException` carrying the instance and, when the instance says so, the moment the limit resets. The CLI reports it and exits `5` so automation never hangs on a silent wait; the TUI is expected to read the reset time and run its own visible countdown. Putting a wait inside the handler would take that choice away from both front ends.

**Parsing is strict: an option this client does not have is a usage error** (#20). Spectre.Console.Cli's default is to collect an unrecognized option as a leftover argument and run the command anyway, which answers a user's mistaken expectation with silence — most pointedly `--password`, which ADR-0004 rules out and which would otherwise be swallowed while a browser opened instead. Strict parsing makes every such option exit `2` with the option named.

**These behaviors are tested at the `HttpMessageHandler` seam, deliberately against ADR-0005's default.** ADR-0005 makes `IMastodonClient` the primary seam and keeps HTTP-level fakes narrow, and that still holds for command logic. It cannot hold here: retry, the translation of a `429`, and the exit code each produces all live *below* `IMastodonClient`, so faking that interface would skip the code under test entirely. The exception is scoped to this cross-cutting layer and to the one end-to-end test that proves a command inherits it — it is not licence to fake HTTP for command logic.

## Consequences

Retry is deliberately blind to idempotency — it never retries a response, so it never has to reason about which verbs are safe to repeat. If a later ticket wants 5xx retries for read commands, that trade has to be reopened here first, together with how a retried `POST` avoids duplicating a post.

`AuthenticationError` has no producer yet; the authentication ticket is expected to claim it. An exit code with no thrower is the intended state for a reserved scheme, not an oversight.
