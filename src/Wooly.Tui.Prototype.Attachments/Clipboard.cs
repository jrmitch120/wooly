// PROTOTYPE (#374) — throwaway. The OS clipboard (per operating system, never per terminal) and dropped paths.

using System.Diagnostics;
using System.Text;

namespace Wooly.Tui.Prototype.Attachments;

internal abstract record Clipped
{
    public sealed record Files(IReadOnlyList<string> Paths) : Clipped;
    public sealed record Picture(string Path) : Clipped;
    public sealed record Text(string Value) : Clipped;
    public sealed record NoTool(string Why) : Clipped;
    public sealed record Nothing : Clipped;
}

internal static class OsClipboard
{
    private static int _pasted;

    private const string MacScript = """
        function run(argv) {
          ObjC.import('AppKit');
          var target = argv[0];
          var pb = $.NSPasteboard.generalPasteboard;
          var opts = $.NSDictionary.dictionaryWithObjectForKey($.NSNumber.numberWithBool(true), 'NSPasteboardURLReadingFileURLsOnlyKey');
          var urls = pb.readObjectsForClassesOptions($.NSArray.arrayWithObject($.NSURL), opts);
          if (urls && !urls.isNil() && urls.count > 0) {
            var out = [];
            for (var i = 0; i < urls.count; i++) out.push('F:' + urls.objectAtIndex(i).path.js);
            return out.join('\n');
          }
          var png = pb.dataForType('public.png');
          if (png && !png.isNil()) { png.writeToFileAtomically(target, true); return 'P:' + target; }
          var tiff = pb.dataForType('public.tiff');
          if (tiff && !tiff.isNil()) {
            var rep = $.NSBitmapImageRep.imageRepWithData(tiff);
            rep.representationUsingTypeProperties($.NSBitmapImageFileTypePNG, $.NSDictionary.dictionary).writeToFileAtomically(target, true);
            return 'P:' + target;
          }
          var text = pb.stringForType('public.utf8-plain-text');
          if (text && !text.isNil()) return 'T:' + text.js;
          return '';
        }
        """;

    public static Clipped Read()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wooly-374-pasted");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"pasted-{_pasted + 1}.png");

        var clipped = OperatingSystem.IsMacOS() ? Mac(target)
            : OperatingSystem.IsWindows() ? Windows(target)
            : Linux(target);

        if (clipped is Clipped.Picture) _pasted++;

        return clipped;
    }

    private static Clipped Mac(string target)
    {
        var script = Path.Combine(Path.GetTempPath(), "wooly-374-clip.js");
        File.WriteAllText(script, MacScript);

        return Parsed(Run("osascript", ["-l", "JavaScript", script, target]) ?? "");
    }

    private static Clipped Windows(string target)
    {
        var command = $$"""
            $f = Get-Clipboard -Format FileDropList -ErrorAction SilentlyContinue
            if ($f) { $f | ForEach-Object { 'F:' + $_.FullName }; exit }
            Add-Type -AssemblyName System.Windows.Forms
            $i = [System.Windows.Forms.Clipboard]::GetImage()
            if ($i) { $i.Save('{{target}}', [System.Drawing.Imaging.ImageFormat]::Png); 'P:{{target}}'; exit }
            $t = Get-Clipboard -Raw
            if ($t) { 'T:' + $t }
            """;

        return Parsed(Run("powershell", ["-NoProfile", "-STA", "-Command", command]) ?? "");
    }

    private static Clipped Linux(string target)
    {
        string[]? types = null;
        Func<string, string[]> read;

        if (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null && Run("wl-paste", ["--list-types"]) is { } wayland)
        {
            types = wayland.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            read = type => ["wl-paste", "--no-newline", "--type", type];
        }
        else if (Run("xclip", ["-selection", "clipboard", "-t", "TARGETS", "-o"]) is { } x)
        {
            types = x.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            read = type => ["xclip", "-selection", "clipboard", "-t", type, "-o"];
        }
        else
        {
            return new Clipped.NoTool("picture paste needs wl-paste or xclip");
        }

        if (types.Contains("text/uri-list") && Ran(read("text/uri-list")) is { } uris)
        {
            return new Clipped.Files([.. uris.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(uri => uri.StartsWith("file://"))
                .Select(uri => Uri.UnescapeDataString(new Uri(uri.Trim()).AbsolutePath))]);
        }

        if (types.Contains("image/png"))
        {
            var command = read("image/png");
            var start = new ProcessStartInfo(command[0], command[1..]) { RedirectStandardOutput = true };
            using var process = Process.Start(start)!;
            using var file = File.Create(target);
            process.StandardOutput.BaseStream.CopyTo(file);
            process.WaitForExit();

            return new Clipped.Picture(target);
        }

        var text = Ran(read("UTF8_STRING")) ?? Ran(read("text/plain"));

        return text is { Length: > 0 } ? new Clipped.Text(text) : new Clipped.Nothing();

        string? Ran(string[] command) => Run(command[0], command[1..]);
    }

    private static Clipped Parsed(string said)
    {
        said = said.TrimEnd('\n', '\r');

        if (said.StartsWith("F:"))
        {
            return new Clipped.Files([.. said.Split('\n').Where(line => line.StartsWith("F:")).Select(line => line[2..].TrimEnd('\r'))]);
        }

        if (said.StartsWith("P:")) return new Clipped.Picture(said[2..].Trim());
        if (said.StartsWith("T:")) return new Clipped.Text(said[2..]);

        return new Clipped.Nothing();
    }

    private static string? Run(string file, string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo(file, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var process = Process.Start(start);

            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>A paste read as dropped files: only when every word of it is an existing, accepted file.</summary>
internal static class Dropped
{
    public static IReadOnlyList<string>? Paths(string pasted)
    {
        var words = Split(pasted.Trim());

        if (words.Count == 0) return null;

        var paths = words.Select(word => word.StartsWith("file://") ? Uri.UnescapeDataString(new Uri(word).LocalPath) : word)
                         .Select(word => word.StartsWith("~/") ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), word[2..]) : word)
                         .ToList();

        return paths.All(Instance.Takes) ? paths : null;
    }

    /// <summary>Splits as a terminal quotes a drop: spaces escaped with a backslash, or a path in quotes.</summary>
    private static List<string> Split(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        char? quote = null;

        for (var at = 0; at < text.Length; at++)
        {
            var letter = text[at];

            if (quote is { } open)
            {
                if (letter == open) quote = null;
                else word.Append(letter);
            }
            else if (letter is '\'' or '"')
            {
                quote = letter;
            }
            else if (letter == '\\' && at + 1 < text.Length && !OperatingSystem.IsWindows())
            {
                word.Append(text[++at]);
            }
            else if (char.IsWhiteSpace(letter))
            {
                if (word.Length > 0) words.Add(word.ToString());
                word.Clear();
            }
            else
            {
                word.Append(letter);
            }
        }

        if (word.Length > 0) words.Add(word.ToString());

        return words;
    }
}
