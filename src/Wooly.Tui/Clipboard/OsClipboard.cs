using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Wooly.Tui.Clipboard;

/// <summary>
///     This machine's clipboard, read by whatever the operating system offers to read it with — not by the terminal,
///     which only ever pastes text (#380): the system scripting bridge on macOS, <c>wl-paste</c> and then <c>xclip</c>
///     on Linux, and PowerShell on Windows. None of them comes with Git, and none is assumed to.
/// </summary>
/// <remarks>
///     The part no test can have: every decision about what a paste does is the shell's, made on what this answers.
///     Anything that goes wrong reading is taken as text, which leaves the paste to the field as it always was.
/// </remarks>
public sealed class OsClipboard : IClipboard
{
    /// <summary>
    ///     How long a tool is waited on before it is given up on and the paste left to the field: read on the drawing
    ///     thread (<see cref="IClipboard" />), so a tool that hangs must not hang Wooly with it.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     macOS's, through JavaScript for Automation and AppKit's pasteboard (checked by hand in #374): copied files'
    ///     paths, one to a line after <c>F:</c>; else a PNG — or a TIFF, which is what most apps copy a picture as,
    ///     turned into one — written to the path it is given, and <c>P</c>; else nothing, which is text.
    /// </summary>
    /// <remarks>Files first: Finder puts each copied file's icon on the pasteboard as a picture too.</remarks>
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
          if (png && !png.isNil()) { png.writeToFileAtomically(target, true); return 'P'; }
          var tiff = pb.dataForType('public.tiff');
          if (tiff && !tiff.isNil()) {
            var rep = $.NSBitmapImageRep.imageRepWithData(tiff);
            rep.representationUsingTypeProperties($.NSBitmapImageFileTypePNG, $.NSDictionary.dictionary).writeToFileAtomically(target, true);
            return 'P';
          }
          return '';
        }
        """;

    /// <inheritdoc />
    public Clipped Read()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Mac();
        }

        return OperatingSystem.IsWindows() ? Windows() : Linux();
    }

    /// <summary>macOS: <see cref="MacScript" /> run by <c>osascript</c>, which every Mac has.</summary>
    private static Clipped Mac() =>
        Through(
            (folder, picture) =>
            {
                var script = Path.Combine(folder, "clipboard.js");
                File.WriteAllText(script, MacScript);

                return Run("osascript", "-l", "JavaScript", script, picture);
            },
            "osascript");

    /// <summary>
    ///     Windows: Windows PowerShell, which every Windows has, reading the clipboard through Windows Forms — which
    ///     wants a single-threaded apartment, hence <c>-STA</c>. Copied files' paths after <c>F:</c>, else a picture
    ///     saved as a PNG to the path it is given, and <c>P</c>.
    /// </summary>
    private static Clipped Windows() =>
        Through(
            (_, picture) => Run(
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-STA",
                "-Command",
                $$"""
                  [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
                  Add-Type -AssemblyName System.Windows.Forms
                  Add-Type -AssemblyName System.Drawing
                  if ([System.Windows.Forms.Clipboard]::ContainsFileDropList()) {
                    foreach ($f in [System.Windows.Forms.Clipboard]::GetFileDropList()) { 'F:' + $f }
                    exit
                  }
                  $i = [System.Windows.Forms.Clipboard]::GetImage()
                  if ($i) { $i.Save('{{picture.Replace("'", "''", StringComparison.Ordinal)}}', [System.Drawing.Imaging.ImageFormat]::Png); 'P' }
                  """),
            "PowerShell");

    /// <summary>
    ///     Linux: <c>wl-paste</c> under Wayland, else <c>xclip</c> — under X, or under Wayland through XWayland — asked
    ///     first which types the clipboard holds and then for the one wanted: copied files as a <c>text/uri-list</c>,
    ///     which is how every file manager copies them, else a PNG.
    /// </summary>
    private static Clipped Linux()
    {
        Ran? types = null;
        Func<string, Ran?> read = _ => null;

        if (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 })
        {
            types = Run("wl-paste", "--list-types");
            read = type => Run("wl-paste", "--no-newline", "--type", type);
        }

        if (types is null)
        {
            types = Run("xclip", "-selection", "clipboard", "-t", "TARGETS", "-o");
            read = type => Run("xclip", "-selection", "clipboard", "-t", type, "-o");
        }

        if (types is null)
        {
            return new Clipped.NoTool("Pasting a picture or files needs wl-paste or xclip installed.");
        }

        // A tool that is there but read nothing — an empty clipboard, or no display to read one from, as over SSH — is
        // a paste for the field.
        if (types.ExitCode != 0)
        {
            return new Clipped.Text();
        }

        var held = types.Said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (held.Contains("text/uri-list") && read("text/uri-list") is { ExitCode: 0 } listed)
        {
            List<string> files =
            [
                .. listed.Said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => !line.StartsWith('#'))
                    .Select(line => Uri.TryCreate(line, UriKind.Absolute, out var address) && address.IsFile
                        ? address.LocalPath
                        : null)
                    .OfType<string>(),
            ];

            if (files.Count > 0)
            {
                return new Clipped.Files(files);
            }
        }

        if (held.Contains("image/png") && read("image/png") is { ExitCode: 0, Output.Length: > 0 } picture)
        {
            return new Clipped.Picture(picture.Output);
        }

        return new Clipped.Text();
    }

    /// <summary>
    ///     What a tool that writes a picture to a file says the clipboard holds: <paramref name="run" /> handed a folder
    ///     of its own to work in and the path to write a picture to, and answering <c>F:</c> lines, <c>P</c>, or
    ///     anything else for text. The folder goes with the read, the picture's bytes having been taken out of it.
    /// </summary>
    private static Clipped Through(Func<string, string, Ran?> run, string tool)
    {
        var folder = Directory.CreateTempSubdirectory("wooly-clipboard-").FullName;

        try
        {
            var picture = Path.Combine(folder, "clipboard.png");

            if (run(folder, picture) is not { } ran)
            {
                return new Clipped.NoTool($"Pasting a picture or files needs {tool}, which isn't there.");
            }

            if (ran.ExitCode != 0)
            {
                return new Clipped.Text();
            }

            var lines = ran.Said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (lines.Length > 0 && lines.All(line => line.StartsWith("F:", StringComparison.Ordinal)))
            {
                return new Clipped.Files([.. lines.Select(line => line[2..])]);
            }

            return lines is ["P"] && File.Exists(picture)
                ? new Clipped.Picture(File.ReadAllBytes(picture))
                : new Clipped.Text();
        }
        catch (IOException)
        {
            return new Clipped.Text();
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Left for the system's own clearing of its temporary folder.
            }
        }
    }

    /// <summary>
    ///     Runs <paramref name="program" /> with <paramref name="arguments" />, each passed whole, and waits — no longer
    ///     than <see cref="Patience" /> — for what it writes.
    /// </summary>
    /// <returns>
    ///     What it wrote and how it ended — one given up on as having failed — or <see langword="null" /> where there is
    ///     no such program, which is the one thing worth telling the author about.
    /// </returns>
    private static Ran? Run(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process? process;

        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;
        }

        if (process is null)
        {
            return null;
        }

        using (process)
        {
            using var output = new MemoryStream();

            // Both read as they come, since a tool that fills the pipe nobody is reading waits on it for ever.
            var reading = process.StandardOutput.BaseStream.CopyToAsync(output);
            _ = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(Patience) || !reading.Wait(Patience))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It ended on its own meanwhile.
                }

                return new Ran(-1, []);
            }

            return new Ran(process.ExitCode, output.ToArray());
        }
    }

    /// <summary>How a tool ended, and what it wrote.</summary>
    private sealed record Ran(int ExitCode, byte[] Output)
    {
        /// <summary>What it wrote, read as text — past the byte-order mark Windows PowerShell may start UTF-8 with.</summary>
        public string Said => Encoding.UTF8.GetString(Output).TrimStart('\uFEFF');
    }
}
