// PROTOTYPE (#374) — throwaway. The draft and its pending attachments, with uploads faked on a timer.

namespace Wooly.Tui.Prototype.Attachments;

internal enum Kind { Picture, Animation, Video, Sound }

internal abstract record State
{
    public sealed record Uploading(double Done) : State;
    public sealed record Processing : State;
    public sealed record Ready : State;
    public sealed record Refused(string Why) : State;
}

internal sealed class Attachment(string path, Kind kind, long bytes)
{
    public string Path { get; } = path;
    public string Name { get; set; } = System.IO.Path.GetFileName(path);
    public Kind Kind { get; } = kind;
    public long Bytes { get; } = bytes;
    public State State { get; set; } = new State.Uploading(0);
    public string Description { get; set; } = "";

    /// <summary>Drops the connection part-way, once — the prototype's one refusal that a retry fixes.</summary>
    public bool DropOnce { get; set; }

    public double ProcessingLeft { get; set; }

    public string KindWord => Kind switch
    {
        Kind.Picture => "picture",
        Kind.Animation => "animation",
        Kind.Video => "video",
        _ => "sound",
    };

    public string Size => Bytes switch
    {
        < 1024 => $"{Bytes} B",
        < 1024 * 1024 => $"{Bytes / 1024.0:F0} KB",
        _ => $"{Bytes / 1024.0 / 1024.0:F1} MB",
    };
}

/// <summary>What the instance accepts — Mastodon's defaults, as its configuration would say them.</summary>
internal static class Instance
{
    public const int Most = 4;
    public const int DescriptionLimit = 1500;

    private static readonly Dictionary<string, Kind> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = Kind.Picture, [".jpeg"] = Kind.Picture, [".png"] = Kind.Picture, [".webp"] = Kind.Picture,
        [".heic"] = Kind.Picture, [".heif"] = Kind.Picture, [".avif"] = Kind.Picture,
        [".gif"] = Kind.Animation,
        [".mp4"] = Kind.Video, [".m4v"] = Kind.Video, [".mov"] = Kind.Video, [".webm"] = Kind.Video,
        [".mp3"] = Kind.Sound, [".ogg"] = Kind.Sound, [".oga"] = Kind.Sound, [".wav"] = Kind.Sound,
        [".flac"] = Kind.Sound, [".opus"] = Kind.Sound, [".m4a"] = Kind.Sound, [".aac"] = Kind.Sound,
    };

    public static Kind? Of(string path) => ByExtension.TryGetValue(System.IO.Path.GetExtension(path), out var kind) ? kind : null;

    public static bool Takes(string path) => Of(path) is not null && File.Exists(path);
}

internal sealed class Draft
{
    private int _attachedThisSession;

    public List<Attachment> Items { get; } = [];

    public string Warning { get; set; } = "";

    /// <summary>The author's own toggle, kept while a warning overrides it.</summary>
    public bool SensitiveChosen { get; set; }

    public bool Locked => Warning.Trim().Length > 0;

    public bool Sensitive => Locked || SensitiveChosen;

    public event Action? Changed;

    public void Touch() => Changed?.Invoke();

    /// <summary>Attaches up to the limit; says how many were left out.</summary>
    public int Attach(IEnumerable<string> paths)
    {
        var leftOut = 0;

        foreach (var path in paths)
        {
            if (Items.Count >= Instance.Most || !File.Exists(path))
            {
                leftOut++;
                continue;
            }

            if (Instance.Of(path) is not { } kind)
            {
                // Only reachable through the browser's "every file": the instance says no once it has it.
                Items.Add(new Attachment(path, Kind.Picture, new FileInfo(path).Length)
                {
                    State = new State.Refused("not a type this instance accepts"),
                });
                continue;
            }

            _attachedThisSession++;
            Items.Add(new Attachment(path, kind, new FileInfo(path).Length) { DropOnce = _attachedThisSession == 3 });
        }

        Touch();

        return leftOut;
    }

    public void Remove(Attachment item)
    {
        Items.Remove(item);
        Touch();
    }

    public int Move(int from, int by)
    {
        var to = Math.Clamp(from + by, 0, Items.Count - 1);

        if (to == from) return from;

        var item = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, item);
        Touch();

        return to;
    }

    public void MoveTo(int from, int to)
    {
        to = Math.Clamp(to, 0, Items.Count - 1);
        if (to == from) return;
        var item = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, item);
        Touch();
    }

    public void Retry(Attachment item)
    {
        if (item.State is State.Refused && Instance.Of(item.Path) is not null)
        {
            item.State = new State.Uploading(0);
            Touch();
        }
    }

    public int Undescribed => Items.Count(item => item.Description.Trim().Length == 0);

    public int Unfinished => Items.Count(item => item.State is State.Uploading or State.Processing);

    public int Refused => Items.Count(item => item.State is State.Refused);

    /// <summary>One step of the fake uploads: about 3 MB a second, a moment of processing for anything big or moving.</summary>
    public void Tick(double seconds)
    {
        var moved = false;

        foreach (var item in Items)
        {
            switch (item.State)
            {
                case State.Uploading(var done):
                    var rate = 3.0 * 1024 * 1024 / Math.Max(item.Bytes, 1);
                    var next = Math.Min(1, done + Math.Max(rate, 0.6) * seconds);

                    if (item.DropOnce && done < 0.6 && next >= 0.6)
                    {
                        item.DropOnce = false;
                        item.State = new State.Refused("connection lost");
                    }
                    else if (next >= 1)
                    {
                        item.State = TooLarge(item) is { } why
                            ? new State.Refused(why)
                            : item.Kind is Kind.Video or Kind.Sound or Kind.Animation || item.Bytes > 4 * 1024 * 1024
                                ? new State.Processing()
                                : new State.Ready();
                        item.ProcessingLeft = item.Kind is Kind.Video ? 5 : 2.5;
                    }
                    else
                    {
                        item.State = new State.Uploading(next);
                    }

                    moved = true;
                    break;

                case State.Processing:
                    item.ProcessingLeft -= seconds;

                    if (item.ProcessingLeft <= 0)
                    {
                        item.State = new State.Ready();
                    }

                    moved = true;
                    break;
            }
        }

        if (moved) Touch();
    }

    private static string? TooLarge(Attachment item) => item.Kind switch
    {
        Kind.Picture or Kind.Animation when item.Bytes > 16L * 1024 * 1024 => "too large · pictures may be 16 MB",
        Kind.Video or Kind.Sound when item.Bytes > 99L * 1024 * 1024 => "too large · video and sound may be 99 MB",
        _ => null,
    };
}
