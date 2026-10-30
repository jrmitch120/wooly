# A tag set is the reader's destination, read four tags to an ask and merged here

The rail kept one place for one hashtag, named by `hashtag` in the config file's `[preferences]`. One tag was too few to
follow a subject by — `dotnet` without `csharp` is half of it — and the place stood on the rail whether or not a tag was
named. It is replaced by **Tag set**s: a name the reader gives to between one and twelve tags, read as one timeline of
the posts carrying any of them, written in the config file and the same for every profile.

**The rail's destinations are no longer all fixed.** ADR-0014 settled a rail of nine places and its amendment a tenth,
on the rule that a rail which grows entries later is a different rail. The nine that are everybody's stay fixed; the
Hashtag entry goes, and each tag set is a destination of its own, in a fifth **Rail group**, **Hashtags**, between
Timelines and Explore (amending ADR-0021's four). The group is drawn only when there is a set, and its entries are in
the order the file lists them, so the file is written as an ordered `[[tag_sets]]` array rather than a table whose keys
a save would sort. This is the rule broken on purpose: what is on the rail is now partly the reader's to write. What
keeps it a rail rather than a list that shifts under the reader is that sets change only when the file does, so the
rail's entries — and whether it is framed or compact on a given terminal — are fixed for a session. No cap on how many:
the rail's existing step-down to compact and then scrolled is what absorbs them, at the price that a single set makes
an 80×24 rail compact.

**A set is read four tags to an ask, and the asks are merged here.** Mastodon's tag timeline reads a union in one ask
(`/api/v1/timelines/tag/<first>?any[]=<second>&…`), but its `TagFeed` keeps only the first four of the path tag and
`any` together (`LIMIT_PER_MODE = 4`) and drops the rest without saying so. Four was too few for a set, and one ask per
tag spends the budget a tag at a time. So a set's tags, in the order written, are cut into runs of four, each run one
ask, and the answers are interleaved newest first by post id, a post that more than one run found kept once. Each run
pages on its own cursor; reading on asks again only of the runs whose posts the merged list has used up. A set of up to
four is one ask, exactly as a single tag was. The calls are raw, around Mastonet 3.1.3, whose tag timeline takes one tag
and no `any`, as ADR-0032's are.

**Twelve tags at most**, so the dearest set is three asks an arrival, known before it is written; a set of more is
refused against the file, as a `shell_width` that is not a number is. Since the instance would drop tags silently, the
cap is this client's to enforce wherever a set is read.

**One run failing fails the read.** An arrival or a read-on whose runs do not all answer shows the error as any failed
read does and keeps nothing of the runs that did answer. A merged list with one run missing looks whole while missing
posts from its middle, and a read-on from it could not say where to go on from.

## Considered options

- **One ask, four tags at most.** Cheapest and simplest, and refused because four is too few for the sets readers
  write.
- **One ask per tag, merged.** No cap, and refused because every arrival and every read-on would cost as many asks as
  the set has tags.
- **Followed hashtags or lists, on the instance.** A followed tag feeds Home rather than a timeline of its own, and a
  list holds accounts only, so neither reads a set by itself.

## Consequences

The old `hashtag` key is ignored rather than refused or carried into a set, and the next save, which rewrites the whole
file, drops it; the release notes say sets replace it. A hashtag walked to from a post or a search is unchanged: the same
single-tag timeline on the stack, not kept anywhere. Sets are the TUI's alone; `timeline tag` still reads one tag.
