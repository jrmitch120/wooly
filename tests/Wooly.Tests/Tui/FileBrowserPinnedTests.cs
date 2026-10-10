using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The file browser's pinned rows: everything above its rule — the folder, the filter and the rule — stays at the top
///     of the content panel however far a long folder's list has scrolled, the cursor is never hidden under them, and the
///     mouse acts on what is drawn where it points, the pinned rows included. Through the drawn window, over a fresh post
///     and over a reply, both of which open it.
/// </summary>
public class FileBrowserPinnedTests : IDisposable
{
    /// <summary>The window's first row inside the content panel, under its top edge.</summary>
    private const int FolderRow = 1;

    private const int FilterRow = 2;

    private const int RuleRow = 3;

    /// <summary>How many pictures the folder holds: far more than the panel has rows for at 80 × 24.</summary>
    private const int Many = 40;

    private readonly TemporaryDirectory _files = new();

    public FileBrowserPinnedTests()
    {
        for (var at = 0; at < Many; at++)
        {
            _files.WriteFile($"picture {at:00}.png");
        }

        _files.WriteFile("notes.txt");
    }

    public void Dispose() => _files.Dispose();

    /// <summary>Walked down to the foot of a long folder, the folder, the filter and the rule are still the top three rows.</summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task WalkedDownTheRowsAboveTheRuleStayPinned(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);

        for (var at = 0; at < Many - 1; at++)
        {
            drawn.Press(Key.CursorDown);
        }

        var rows = drawn.Rows();

        Assert.DoesNotContain(rows, row => row.Contains("picture 00", StringComparison.Ordinal));
        AssertPinned(rows);
        Assert.True(Cursor(drawn) > RuleRow);
        Assert.Contains("picture 39", rows[Cursor(drawn)], StringComparison.Ordinal);
    }

    /// <summary>The wheel scrolls the list under the pinned rows, never the pinned rows themselves.</summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task TheWheelScrollsTheListUnderThePinnedRows(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);
        var (column, row) = Find(drawn, "picture 05");

        for (var notch = 0; notch < 25; notch++)
        {
            drawn.Wheel(column, row);
        }

        var rows = drawn.Rows();

        AssertPinned(rows);
        Assert.Contains(rows, text => text.Contains("picture 39", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, text => text.Contains("picture 10", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Walking back up with <c>↑</c> from the foot of the list scrolls it so that the cursor is always on a row of the
    ///     list below the rule, all the way back to <c>..</c>.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task WalkingBackUpKeepsTheCursorBelowTheRule(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);

        for (var at = 0; at < Many - 1; at++)
        {
            drawn.Press(Key.CursorDown);
        }

        for (var at = Many - 1; at > 0; at--)
        {
            drawn.Press(Key.CursorUp);

            var rows = drawn.Rows();

            AssertPinned(rows);
            Assert.True(Cursor(drawn) > RuleRow, $"the cursor is hidden under the pinned rows:\n{string.Join('\n', rows)}");
            Assert.Contains($"picture {at - 1:00}", rows[Cursor(drawn)], StringComparison.Ordinal);
        }

        drawn.Press(Key.CursorUp);

        Assert.Contains("◂ ..", drawn.Rows()[RuleRow + 1], StringComparison.Ordinal);
        Assert.Equal(RuleRow + 1, Cursor(drawn));
    }

    /// <summary>
    ///     Wheeled down just far enough that the cursor's row would lie under the pinned rows, the next <c>↓</c> takes the
    ///     first row showing below the rule, as it would any row wheeled off the page — never a row nobody can see.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task AfterTheWheelTheNextArrowTakesARowBelowTheRule(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);
        var (column, row) = Find(drawn, "picture 05");

        // The cursor on picture 00, the list's second row; three notches put it under the rule.
        for (var notch = 0; notch < 3; notch++)
        {
            drawn.Wheel(column, row);
        }

        Assert.DoesNotContain(drawn.Rows(), text => text.Contains("▌☐ picture", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);

        Assert.True(Cursor(drawn) > RuleRow);
        Assert.Contains("picture 02", drawn.Rows()[Cursor(drawn)], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Scrolled down, a click on <c>ctrl-a every file</c> shows every file, and a click on the folder row picks
    ///     nothing — neither reaches the list row scrolled under it.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task AClickOnAPinnedRowActsOnThatRow(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);
        var (column, row) = Find(drawn, "picture 05");

        for (var notch = 0; notch < 10; notch++)
        {
            drawn.Wheel(column, row);
        }

        var cursor = Cursor(drawn);
        var (folder, folderRow) = Find(drawn, "more fit");

        drawn.Click(folder, folderRow);

        Assert.Equal(cursor, Cursor(drawn));
        Assert.DoesNotContain(drawn.Rows()[FolderRow], "▌", StringComparison.Ordinal);

        var (every, everyRow) = Find(drawn, "ctrl-a every file");

        drawn.Click(every + 3, everyRow);

        Assert.True(Browser(drawn).EveryFile);
        Assert.Contains("ctrl-a accepted only", drawn.Rows()[FilterRow], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Scrolled down, a click on a list row moves the cursor to the row clicked, a click on its box chooses it, and a
    ///     double click attaches it.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    public async Task ClicksOnTheScrolledListActOnTheRowDrawn(ComposeFor purpose)
    {
        using var drawn = await Browsing(purpose);
        var (column, row) = Find(drawn, "picture 05");

        for (var notch = 0; notch < 10; notch++)
        {
            drawn.Wheel(column, row);
        }

        var (name, nameRow) = Find(drawn, "picture 20");

        drawn.Click(name + 1, nameRow);

        Assert.Equal(nameRow, Cursor(drawn));
        Assert.Contains("▌☐ picture 20", drawn.Rows()[nameRow], StringComparison.Ordinal);

        drawn.Click(name - 2, nameRow);

        Assert.Contains("☑ picture 20", drawn.Rows()[nameRow], StringComparison.Ordinal);

        (name, nameRow) = Find(drawn, "picture 15");
        drawn.Click(name - 2, nameRow);
        drawn.DoubleClick(name + 1, nameRow);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal(["picture 20.png", "picture 15.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>Over the folder row, the filter row or the rule, the wheel scrolls the list as it does over the list.</summary>
    [Fact]
    public async Task TheWheelOverAPinnedRowScrollsTheList()
    {
        using var drawn = await Browsing(ComposeFor.Post);
        var (column, _) = Find(drawn, "type to filter");

        for (var notch = 0; notch < 25; notch++)
        {
            drawn.Wheel(column, FilterRow);
        }

        AssertPinned(drawn.Rows());
        Assert.Contains(drawn.Rows(), text => text.Contains("picture 39", StringComparison.Ordinal));
    }

    /// <summary>The folder row, the filter row and the rule, at the top of the panel.</summary>
    private void AssertPinned(string[] rows)
    {
        Assert.Contains("more fit on this post", rows[FolderRow], StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(_files.Path)[^12..], rows[FolderRow], StringComparison.Ordinal);
        Assert.Contains("type to filter", rows[FilterRow], StringComparison.Ordinal);
        Assert.Contains("──────────", rows[RuleRow], StringComparison.Ordinal);
    }

    /// <summary>The window row the cursor's selection bar is drawn on, or -1 where it is drawn on none.</summary>
    private static int Cursor(DrawnShell drawn) =>
        Array.FindIndex(drawn.Rows(), row => row.Contains(" ▌", StringComparison.Ordinal));

    private static FileBrowserScreen Browser(DrawnShell drawn) => Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);

    /// <summary>
    ///     The browser in an 80 × 24 window launched from the test's folder, opened with <c>ctrl-o</c> over a fresh post
    ///     or over a reply to the post the feed opens on.
    /// </summary>
    private async Task<DrawnShell> Browsing(ComposeFor purpose)
    {
        var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell
        {
            LaunchedFrom = _files.Path,
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        });

        ComposeRows.Open(drawn.Shell, purpose);
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        Browser(drawn);
        AssertPinned(drawn.Rows());

        return drawn;
    }

    /// <summary>Where <paramref name="text" /> is drawn in the window, on the first row with it.</summary>
    private static (int Column, int Row) Find(DrawnShell drawn, string text)
    {
        var rows = drawn.Rows();

        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].IndexOf(text, StringComparison.Ordinal) is >= 0 and var column)
            {
                return (column, row);
            }
        }

        throw new InvalidOperationException($"\"{text}\" is not drawn:\n{string.Join('\n', rows)}");
    }
}
