using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The "edit public profile" form (#406, ADR-0028): opened from <b>You</b> with <c>E</c>, read as written under an
///     enquiry, and saved as changed. Through the shell over a fake public profile, so what is asserted is what was
///     drawn and what the port was asked to send.
/// </summary>
public class PublicProfileFormTests
{
    /// <summary>The account the profile every test acts as signs in as, which is who <b>You</b> reads.</summary>
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>A shell whose instance answers about the profile's own account, standing on <b>You</b>.</summary>
    private static async Task<Shell> OnYou(AShell built)
    {
        var shell = await built.Opened();

        shell.Rail.GoTo(DestinationKind.Profile);
        built.Host.Settle();

        return shell;
    }

    private static AShell Built(PublicProfile? read = null) => new()
    {
        Accounts = FakeAccountRelationships.Holding(Me),
        PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(account: Me)),
    };

    /// <summary>The form, opened with <c>E</c> on <b>You</b> and read.</summary>
    private static async Task<(Shell Shell, PublicProfileScreen Form)> Editing(AShell built)
    {
        var shell = await OnYou(built);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        return (shell, Assert.IsType<PublicProfileScreen>(shell.Screen));
    }

    /// <summary>Walks the form down to <paramref name="part" />, as the arrows do.</summary>
    private static void WalkTo(Shell shell, PublicProfileScreen form, PublicProfilePart part)
    {
        while (form.Typing.Part != part)
        {
            Assert.True(shell.Press(ShellKey.Down));
        }
    }

    /// <summary><c>E</c> on <b>You</b> reads the public profile as written, then puts the form up over it.</summary>
    [Fact]
    public async Task CapitalE_OnYou_ReadsThePublicProfileAndOpensTheForm()
    {
        var built = Built();

        var (shell, _) = await Editing(built);

        Assert.Equal(["personal"], built.PublicProfile.Reads);
        Assert.Equal("Edit public profile", shell.Screen.Crumb);
        Assert.Equal(2, shell.Depth);
    }

    /// <summary>
    ///     The form reads at 61 columns, top to bottom: the display name, the bio as written, the avatar and the header,
    ///     four custom-field rows, and the three toggles — each toggle's state carried by its box, so it reads with no
    ///     colour, and each picture saying it is the current one where no picture can be drawn (#407).
    /// </summary>
    [Fact]
    public async Task TheForm_ReadsTopToBottomAt61Columns()
    {
        var read = APublicProfile.With(
            displayName: "Jeff",
            bio: "Writes Wooly.",
            fields: [new PublicProfileField("Site", "https://wooly.example")],
            isLocked: false,
            isBot: true,
            isDiscoverable: true,
            account: Me);

        var (_, form) = await Editing(Built(read));

        var drawn = AShell.Drawn(form).Select(row => row.TrimEnd()).Where(row => row.Length > 0).ToList();

        Assert.Equal(
            [
                "   Name  Jeff",
                "    Bio  Writes Wooly.",
                " Avatar  current",
                " Header  current",
                " Fields  Site              https://wooly.example",
                "         label             value",
                "         label             value",
                "         label             value",
                "         □ Locked · approve followers by hand",
                "         ■ Bot · posts automatically",
                "         ■ Discoverable · in the directory and suggestions",
            ],
            drawn);
    }

    /// <summary>
    ///     In a room too short for the whole form, the rows drawn are never more than the room, and whichever field the
    ///     walk is on is among them, where the form says it goes — the bio keeping its rows, and the form moving only as
    ///     far as the walk needs it to (review of #403).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InAShortRoom_TheWalkedFieldIsAlwaysDrawn(bool pictures)
    {
        var (shell, form) = await Editing(Built());
        var drawing = new Drawing(61, AShell.Now, new FakePictures(), pictures ? ARaster.Sixel() : Raster.None, Height: 10);
        var room = new System.Drawing.Size(61, 10);

        void InView()
        {
            var lines = form.Lines(drawing);
            var at = form.At(form.Typing, room, drawing.Raster);

            Assert.True(lines.Count <= 10, $"{lines.Count} rows in a room of 10");
            Assert.False(at.IsEmpty, $"{form.Typing} is nowhere");
            Assert.InRange(at.Top, 0, 9);
            Assert.InRange(at.Bottom, 1, 10);
            Assert.StartsWith("▌", lines[at.Top].Text, StringComparison.Ordinal);
        }

        InView();

        while (form.Typing.Part != PublicProfilePart.Discoverable)
        {
            shell.Press(ShellKey.Down);
            InView();
        }

        while (form.Typing.Part != PublicProfilePart.DisplayName)
        {
            shell.Press(ShellKey.Up);
            InView();
        }
    }

    /// <summary>
    ///     <c>↓</c> walks the form top to bottom — the display name, the bio, the avatar and the header, each custom
    ///     field's label then its value, the three toggles — and stops at the bottom rather than coming round; <c>↑</c> walks back and stops at the top.
    /// </summary>
    [Fact]
    public async Task TheArrows_WalkTheFormWithoutWrapping()
    {
        var (shell, form) = await Editing(Built());
        var walked = new List<PublicProfileStop> { form.Typing };

        for (var press = 0; press < 16; press++)
        {
            shell.Press(ShellKey.Down);
            walked.Add(form.Typing);
        }

        Assert.Equal(
            [
                new(PublicProfilePart.DisplayName), new(PublicProfilePart.Bio),
                new(PublicProfilePart.Avatar), new(PublicProfilePart.Header),
                new(PublicProfilePart.Label, 0), new(PublicProfilePart.Value, 0),
                new(PublicProfilePart.Label, 1), new(PublicProfilePart.Value, 1),
                new(PublicProfilePart.Label, 2), new(PublicProfilePart.Value, 2),
                new(PublicProfilePart.Label, 3), new(PublicProfilePart.Value, 3),
                new(PublicProfilePart.Locked), new(PublicProfilePart.Bot), new(PublicProfilePart.Discoverable),
                new(PublicProfilePart.Discoverable), new(PublicProfilePart.Discoverable),
            ],
            walked);

        for (var press = 0; press < 20; press++)
        {
            shell.Press(ShellKey.Up);
        }

        Assert.Equal(new PublicProfileStop(PublicProfilePart.DisplayName), form.Typing);
    }

    /// <summary>
    ///     The status row offers the walk only the ways it goes, as compose's does: <c>↓</c> at the top, <c>↑</c> at
    ///     the bottom, both between.
    /// </summary>
    [Fact]
    public async Task TheStatusRow_OffersTheWalkOnlyTheWaysItGoes()
    {
        var (shell, form) = await Editing(Built());

        Assert.Contains(shell.Keys, key => key is { Key: "↓", Does: "field" });

        shell.Press(ShellKey.Down);

        Assert.Contains(shell.Keys, key => key is { Key: "↑↓", Does: "field" });

        while (form.Typing.Part != PublicProfilePart.Discoverable)
        {
            shell.Press(ShellKey.Down);
        }

        Assert.Contains(shell.Keys, key => key is { Key: "↑", Does: "field" });
    }

    /// <summary>A toggle walked to flips with <c>space</c> and again with <c>⏎</c>, and its box says which way it is.</summary>
    [Fact]
    public async Task SpaceAndEnter_FlipTheToggleWalkedTo()
    {
        var (shell, form) = await Editing(Built(APublicProfile.With(isBot: false, account: Me)));

        WalkTo(shell, form, PublicProfilePart.Bot);

        Assert.True(shell.Press(ShellKey.Space));
        Assert.True(form.IsBot);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("■ Bot", StringComparison.Ordinal));

        Assert.True(shell.Press(ShellKey.Enter));
        Assert.False(form.IsBot);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("□ Bot", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>ctrl-s</c> sends only what changed: a display name typed and a toggle flipped, and nothing about the bio,
    ///     the custom fields or the toggles left alone.
    /// </summary>
    [Fact]
    public async Task CtrlS_SendsOnlyWhatChanged()
    {
        var built = Built(APublicProfile.With(displayName: "Jeff", isBot: false, account: Me));
        var (shell, form) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));
        WalkTo(shell, form, PublicProfilePart.Bot);
        shell.Press(ShellKey.Space);

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        Assert.Equal([new PublicProfileChange { DisplayName = "Jeff M", IsBot = true }], built.PublicProfile.Saves);
    }

    /// <summary>
    ///     A custom field changed sends every row the form holds as the account's fields, since Mastodon replaces them
    ///     as a set — and a row emptied is left out of them, which is what removes it.
    /// </summary>
    [Fact]
    public async Task ACustomFieldChanged_SendsThemAllAndAnEmptiedRowRemovesItsField()
    {
        var read = APublicProfile.With(
            fields: [new PublicProfileField("Site", "https://wooly.example"), new PublicProfileField("Pronouns", "he/him")],
            account: Me);
        var built = Built(read);
        var (shell, _) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteLabel(0, ""));
        shell.ChangePublicProfile(editing => editing.RewriteValue(0, ""));
        shell.ChangePublicProfile(editing => editing.RewriteLabel(2, "Home"));
        shell.ChangePublicProfile(editing => editing.RewriteValue(2, "Kansas"));

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        var sent = Assert.Single(built.PublicProfile.Saves);

        Assert.Equal([new PublicProfileField("Pronouns", "he/him"), new PublicProfileField("Home", "Kansas")], sent.Fields);
        Assert.Equal(new PublicProfileChange { Fields = sent.Fields }, sent);
    }

    /// <summary>
    ///     A bio is compared line ending for line ending alike, so an editor writing its lines back its own way sends
    ///     nothing — and a save with nothing changed sends nothing, and says so.
    /// </summary>
    [Fact]
    public async Task NothingChanged_SendsNothingAndSaysSo()
    {
        var built = Built(APublicProfile.With(bio: "One\r\nTwo", account: Me));
        var (shell, form) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteBio("One\nTwo"));

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        Assert.Empty(built.PublicProfile.Saves);
        Assert.Same(form, shell.Screen);
        Assert.Equal("Nothing has changed to save.", shell.Notice);
    }

    /// <summary>
    ///     A save takes the form off and lands back on <b>You</b>, read again so that it shows the public profile as the
    ///     instance now has it with no refresh asked for, and the status row says it was saved.
    /// </summary>
    [Fact]
    public async Task ASave_LandsBackOnYouReadAgain()
    {
        var renamed = Me with { Author = "Jeff M" };
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me), saved: renamed);
        var (shell, _) = await Editing(built);
        var readsBefore = built.Accounts.Reads.Count;

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));
        built.Accounts.NowShowing(renamed);

        shell.Press(ShellKey.CtrlS);
        built.Host.Settle();

        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(1, shell.Depth);
        Assert.Equal(readsBefore + 1, built.Accounts.Reads.Count);
        Assert.Equal("Jeff M", you.Account.Author);
        Assert.Contains(AShell.Drawn(you), row => row.Contains("Jeff M", StringComparison.Ordinal));
        Assert.Equal("Public profile saved.", shell.Notice);
    }

    /// <summary>
    ///     A follow list held a moment ago with the own account on it is read again after a save rather than handed back
    ///     showing the public profile as it was — the own account reached from it, as from anywhere, offering <c>E</c>.
    /// </summary>
    [Fact]
    public async Task ASave_ForgetsACachedFollowListTheOwnAccountIsOn()
    {
        var ben = AnAccount.With(id: "42", address: "ben@hachyderm.io");
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(ben, Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me)),
        };
        var shell = await built.Opened();

        await shell.OpenAuthor();
        built.Host.Drain();
        shell.Press(ShellKey.W);
        built.Host.Drain();

        built.Accounts.NowShowing(Me);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();
        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));
        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        shell.Back();
        shell.Back();
        built.Accounts.NowShowing(ben);

        var asked = built.Accounts.Lists.Count(list => list.Side is not null);

        shell.Press(ShellKey.W);
        built.Host.Drain();

        Assert.Equal(asked + 1, built.Accounts.Lists.Count(list => list.Side is not null));
    }

    /// <summary>
    ///     A timeline held a moment ago with one of the own account's posts on it is read again after a save, rather
    ///     than handed back with the post's byline saying the public profile as it was (review of #403).
    /// </summary>
    [Fact]
    public async Task ASave_ForgetsACachedTimelineCarryingTheOwnAccountsPost()
    {
        var built = Built();
        built.Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: Me.Address, author: "Jeff"));
        var (shell, _) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));
        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        var asked = built.Timelines.Reads.Count(read => read.Timeline == Wooly.Core.Timelines.Timeline.Home);

        shell.Rail.GoTo(DestinationKind.Home);
        built.Host.Settle();

        Assert.Equal(asked + 1, built.Timelines.Reads.Count(read => read.Timeline == Wooly.Core.Timelines.Timeline.Home));
    }

    /// <summary>
    ///     The own account somewhere further down the stack — <b>You</b>, under a follow list it was reached again from
    ///     — shows the public profile as saved when it is come back to, whichever copy of it the form was opened from
    ///     (review of #403).
    /// </summary>
    [Fact]
    public async Task ASave_ReachesTheOwnAccountFurtherDownTheStack()
    {
        var renamed = Me with { Author = "Jeff M" };
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me, Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me), saved: renamed),
        };
        var shell = await OnYou(built);

        shell.Press(ShellKey.W);
        built.Host.Drain();
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
        Assert.Equal(3, shell.Depth);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();
        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));
        built.Accounts.NowShowing(renamed);
        shell.Press(ShellKey.CtrlS);
        built.Host.Settle();

        shell.Back();
        shell.Back();

        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(1, shell.Depth);
        Assert.Equal("Jeff M", you.Account.Author);
    }

    /// <summary>
    ///     A save the instance refuses keeps the form open with every edit in it, and the status row says why in the
    ///     instance's own words — until an edit, which is doing what it asked, takes the notice down.
    /// </summary>
    [Fact]
    public async Task ARefusedSave_KeepsEveryEditAndSaysWhy()
    {
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Refusing(
            PublicProfileRefusedException.Saving("Display name is too long (maximum is 30 characters)"),
            APublicProfile.With(account: Me));
        var (shell, form) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff the very long display name indeed"));
        WalkTo(shell, form, PublicProfilePart.Locked);
        shell.Press(ShellKey.Space);

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        Assert.Same(form, shell.Screen);
        Assert.Equal("Jeff the very long display name indeed", form.DisplayName);
        Assert.True(form.IsLocked);
        Assert.Equal(
            "The instance would not take that change to your public profile: Display name is too long (maximum is 30 characters)",
            shell.Notice);
        Assert.True(shell.NoticeIsError);

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff"));

        Assert.Null(shell.Notice);
    }

    /// <summary>An untouched form leaves on <c>esc</c> without asking — a toggle flipped and flipped back included.</summary>
    [Fact]
    public async Task Esc_OnAnUntouchedForm_LeavesWithoutAsking()
    {
        var (shell, form) = await Editing(Built());

        WalkTo(shell, form, PublicProfilePart.Bot);
        shell.Press(ShellKey.Space);
        shell.Press(ShellKey.Space);

        shell.Press(ShellKey.Escape);

        Assert.Null(shell.Asking);
        Assert.IsType<AccountScreen>(shell.Screen);
    }

    /// <summary>
    ///     A touched form asks <c>Discard these changes? y / n</c> before any way out — <c>esc</c>, or <c>tab</c> along
    ///     the rail — and declining keeps the form with every edit, where <c>y</c> goes.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.Tab)]
    [InlineData(ShellKey.CtrlP)]
    public async Task ATouchedForm_AsksBeforeEveryWayOut(ShellKey way)
    {
        var built = Built();
        var (shell, form) = await Editing(built);

        shell.ChangePublicProfile(editing => editing.RewriteDisplayName("Jeff M"));

        shell.Press(way);

        Assert.Equal("Discard these changes?", shell.Asking?.Ask);
        Assert.True(shell.Asking?.YesOrNo);

        // n is no key of the shell's, and is a no as anything but y is.
        await shell.Answer(pressed: null);

        Assert.Same(form, shell.Screen);
        Assert.Equal("Jeff M", form.DisplayName);

        shell.Press(way);
        await shell.Answer(ShellKey.Y);
        built.Host.Settle();

        Assert.DoesNotContain(form.Crumb, shell.Crumbs);
    }

    /// <summary>
    ///     <b>You</b>'s header block ends on an "edit public profile" row, walked to with <c>k</c> as the next thing after
    ///     the header, which <c>⏎</c> opens the form from — so the form can be found without already knowing <c>E</c>.
    /// </summary>
    [Fact]
    public async Task TheEditRow_IsWalkedToAndOpensTheFormOnEnter()
    {
        var built = Built();
        var shell = await OnYou(built);
        var you = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Contains(AShell.Drawn(you), row => row.Contains("Edit public profile · E", StringComparison.Ordinal));

        shell.Move(1);

        Assert.True(you.OnTheEditRow);
        Assert.Contains(shell.Keys, key => key is { Key: "⏎", Does: "edit public profile" });

        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.IsType<PublicProfileScreen>(shell.Screen);
    }

    /// <summary>A click on the "edit public profile" row opens the form, as <c>⏎</c> on it does.</summary>
    [Fact]
    public async Task AClickOnTheEditRow_OpensTheForm()
    {
        var built = Built();
        var shell = await OnYou(built);
        var row = AShell.Drawn(shell.Screen).ToList().FindIndex(drawn => drawn.Contains("Edit public profile", StringComparison.Ordinal));
        var line = shell.Screen.Lines(new Wooly.Tui.Rendering.Drawing(61, AShell.Now))[row];

        Assert.True(shell.Click(line.Item, part: null, chorded: false));
        built.Host.Drain();

        Assert.IsType<PublicProfileScreen>(shell.Screen);
    }

    /// <summary>Somebody else's header block has no such row, and walks straight from the header to their first post.</summary>
    [Fact]
    public async Task SomebodyElsesAccount_HasNoEditRow()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")) };
        var shell = await OnYou(built);
        var theirs = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.DoesNotContain(AShell.Drawn(theirs), row => row.Contains("Edit public profile", StringComparison.Ordinal));

        shell.Move(1);

        Assert.NotNull(theirs.Picked);
    }

    /// <summary><b>You</b> offers <c>E</c> on its status row, worded as ADR-0028 words the form.</summary>
    [Fact]
    public async Task You_OffersCapitalE()
    {
        var shell = await OnYou(Built());

        Assert.Contains(shell.Keys, key => key is { Key: "E", Does: "edit public profile" });
    }

    /// <summary>
    ///     Anybody else's account neither offers <c>E</c> nor answers it, as <c>p</c>, <c>e</c> and <c>d</c> are off
    ///     somebody else's post: their public profile cannot be changed from here.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesAccount_NeitherOffersNorAnswersCapitalE()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")) };
        var shell = await OnYou(built);

        Assert.DoesNotContain(shell.Keys, key => key.Key == "E");

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        Assert.Empty(built.PublicProfile.Reads);
        Assert.IsType<AccountScreen>(shell.Screen);
    }

    /// <summary>A read that fails opens no form, and the status row says why in the instance's words.</summary>
    [Fact]
    public async Task AFailedRead_OpensNothingAndSaysWhy()
    {
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Unreadable(PublicProfileRefusedException.Reading("Not today"));
        var shell = await OnYou(built);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        Assert.IsType<AccountScreen>(shell.Screen);
        Assert.Equal("The instance would not show your public profile: Not today", shell.Notice);
        Assert.True(shell.NoticeIsError);
    }
}
