using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The "edit public profile" form (#406, ADR-0028), asked of drafting (ADR-0029, #422): opened from <b>You</b> with
///     <c>E</c>, read as written under an enquiry, walked, flipped and saved as changed — over a fake public profile, so
///     what is asserted is what is on the stack, what the form holds and draws, and what the port was asked to send.
/// </summary>
public class DraftingPublicProfileFormTests
{
    /// <summary>The account the profile every test acts as signs in as, which is who <b>You</b> reads.</summary>
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>Drafting over <b>You</b>, whose public profile reads as <paramref name="read" />.</summary>
    private static ADrafting OnYou(PublicProfile? read = null) =>
        OnYou(FakePublicProfileEditor.Holding(read ?? APublicProfile.With(account: Me)));

    private static ADrafting OnYou(FakePublicProfileEditor publicProfile) => new ADrafting
    {
        Bottom = new AccountScreen(Me, [], pinned: null, isOwnAccount: true),
        Fakes =
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = publicProfile,
        },
    }.Build();

    /// <summary>The form, opened with <c>E</c> on <b>You</b> and read.</summary>
    private static PublicProfileScreen Editing(ADrafting drafting)
    {
        drafting.Press(ShellKey.CapitalE);
        drafting.Fakes.Host.Drain();

        return Assert.IsType<PublicProfileScreen>(drafting.Stack.Top);
    }

    /// <summary>Walks the form down to <paramref name="part" />, as the arrows do.</summary>
    private static void WalkTo(ADrafting drafting, PublicProfileScreen form, PublicProfilePart part)
    {
        while (form.Typing.Part != part)
        {
            Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Down));
        }
    }

    /// <summary>A change to the form, as its view makes one: through drafting.</summary>
    private static DraftChange Change(ADrafting drafting, Func<PublicProfileScreen, DraftChange> change) =>
        drafting.Drafting.Edit(change);

    /// <summary><c>E</c> on <b>You</b> reads the public profile as written, then puts the form up over it.</summary>
    [Fact]
    public void CapitalE_OnYou_ReadsThePublicProfileAndOpensTheForm()
    {
        var drafting = OnYou();

        Editing(drafting);

        Assert.Equal(["personal"], drafting.Fakes.PublicProfile.Reads);
        Assert.Equal("Edit public profile", drafting.Stack.Top.Crumb);
        Assert.Equal(2, drafting.Stack.Screens.Count);
    }

    /// <summary>A read that fails opens no form, and the status row says why in the instance's words.</summary>
    [Fact]
    public void AFailedRead_OpensNothingAndSaysWhy()
    {
        var drafting = OnYou(FakePublicProfileEditor.Unreadable(PublicProfileRefusedException.Reading("Not today")));

        drafting.Press(ShellKey.CapitalE);
        drafting.Fakes.Host.Drain();

        Assert.IsType<AccountScreen>(drafting.Stack.Top);
        Assert.Equal("The instance would not show your public profile: Not today", drafting.Notice);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>Anybody else's account does not answer <c>E</c>: their public profile cannot be changed from here.</summary>
    [Fact]
    public void SomebodyElsesAccount_DoesNotAnswerCapitalE()
    {
        var drafting = new ADrafting
        {
            Bottom = new AccountScreen(AnAccount.With(address: "ben@hachyderm.io"), [], pinned: null),
        }.Build();

        drafting.Drafting.Do(Verb.EditPublicProfile);
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.PublicProfile.Reads);
        Assert.IsType<AccountScreen>(drafting.Stack.Top);
    }

    /// <summary>
    ///     The form reads at 61 columns, top to bottom: the display name, the avatar and the header, four custom-field
    ///     rows, and the three toggles — each toggle's state carried by its box, so it reads with no colour, and each
    ///     picture saying what <c>⏎</c> does to it where no picture can be drawn (#407) — then the bio as written between
    ///     two hairlines, as compose's post is, and its count under them against the instance's limit.
    /// </summary>
    [Fact]
    public void TheForm_ReadsTopToBottomAt61Columns()
    {
        var read = APublicProfile.With(
            displayName: "Jeff",
            bio: "Writes Wooly.",
            fields: [new PublicProfileField("Site", "https://wooly.example")],
            isLocked: false,
            isBot: true,
            isDiscoverable: true,
            account: Me);

        var form = Editing(OnYou(read));

        var drawn = AShell.Drawn(form).Select(row => row.TrimEnd()).Where(row => row.Length > 0).ToList();

        Assert.Equal(
            [
                "   Name  Jeff",
                " Avatar  change…",
                " Header  change…",
                " Fields  Site              https://wooly.example",
                "         label             value",
                "         label             value",
                "         label             value",
                "         □ Locked · approve followers by hand",
                "         ■ Bot · posts automatically",
                "         ■ Discoverable · in the directory and suggestions",
                " " + new string('─', 57),
                " Writes Wooly.",
                " " + new string('─', 57),
                new string(' ', 50) + "13 / 500",
            ],
            drawn);
    }

    /// <summary>
    ///     Nothing is marked with a bar: a field the walk is in says so with its caret, as compose's do, and a picture or
    ///     a toggle — which have no caret — with its value lit as compose's Media and To are.
    /// </summary>
    [Fact]
    public void TheWalkedRow_IsLitRatherThanBarred()
    {
        var drafting = OnYou();
        var form = Editing(drafting);

        Assert.DoesNotContain(form.Lines(new Drawing(61, AShell.Now)), line => line.Text.Contains('▌'));

        WalkTo(drafting, form, PublicProfilePart.Avatar);

        Assert.Contains(Row(form, "Avatar").Spans, span => span is { Text: "change…", Role: Role.SelectedText });
        Assert.DoesNotContain(Row(form, "Header").Spans, span => span.Role == Role.SelectedText);

        WalkTo(drafting, form, PublicProfilePart.Bot);

        Assert.Contains(Row(form, "Bot").Spans, span => span is { Text: "□ Bot", Role: Role.SelectedText });
        Assert.DoesNotContain(Row(form, "Locked").Spans, span => span.Role == Role.SelectedText);
        Assert.DoesNotContain(form.Lines(new Drawing(61, AShell.Now)), line => line.Text.Contains('▌'));
    }

    private static Line Row(PublicProfileScreen form, string holding) =>
        form.Lines(new Drawing(61, AShell.Now)).First(line => line.Text.Contains(holding, StringComparison.Ordinal));

    /// <summary>
    ///     In a room too short for the whole form, the rows drawn are never more than the room, and whichever field the
    ///     walk is on is among them, where the form says it goes — the bio keeping its rows, and the form moving only as
    ///     far as the walk needs it to (review of #403).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InAShortRoom_TheWalkedFieldIsAlwaysDrawn(bool pictures)
    {
        var drafting = OnYou();
        var form = Editing(drafting);
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
            Assert.DoesNotContain("▌", lines[at.Top].Text, StringComparison.Ordinal);
        }

        InView();

        while (form.Typing.Part != PublicProfilePart.Bio)
        {
            drafting.Press(ShellKey.Down);
            InView();
        }

        while (form.Typing.Part != PublicProfilePart.DisplayName)
        {
            drafting.Press(ShellKey.Up);
            InView();
        }
    }

    /// <summary>
    ///     <c>↓</c> walks the form top to bottom — the display name, the avatar and the header, down the custom fields'
    ///     labels, the three toggles, and the bio at the foot, as compose's post is — and stops at the bottom rather
    ///     than coming round; <c>↑</c> walks back and stops at the top.
    /// </summary>
    [Fact]
    public void TheArrows_WalkTheFormWithoutWrapping()
    {
        var drafting = OnYou();
        var form = Editing(drafting);
        var walked = new List<PublicProfileStop> { form.Typing };

        for (var press = 0; press < 12; press++)
        {
            drafting.Press(ShellKey.Down);
            walked.Add(form.Typing);
        }

        Assert.Equal(
            [
                new(PublicProfilePart.DisplayName),
                new(PublicProfilePart.Avatar), new(PublicProfilePart.Header),
                new(PublicProfilePart.Label, 0), new(PublicProfilePart.Label, 1),
                new(PublicProfilePart.Label, 2), new(PublicProfilePart.Label, 3),
                new(PublicProfilePart.Locked), new(PublicProfilePart.Bot), new(PublicProfilePart.Discoverable),
                new(PublicProfilePart.Bio), new(PublicProfilePart.Bio), new(PublicProfilePart.Bio),
            ],
            walked);

        for (var press = 0; press < 20; press++)
        {
            drafting.Press(ShellKey.Up);
        }

        Assert.Equal(new PublicProfileStop(PublicProfilePart.DisplayName), form.Typing);
    }

    /// <summary>
    ///     A custom field's label and value sit side by side, so <c>→</c> and <c>←</c> cross between them and <c>↑</c>
    ///     and <c>↓</c> keep to the column: down the values from a value, and out of the rows to the pictures above and
    ///     the toggles below. <c>→</c> goes nowhere from a value, nor <c>←</c> from a label.
    /// </summary>
    [Fact]
    public void ACustomFieldsLabelAndValue_AreCrossedSideways()
    {
        var drafting = OnYou();
        var form = Editing(drafting);

        WalkTo(drafting, form, PublicProfilePart.Label);

        Assert.Equal(DraftChange.None, Change(drafting, editing => editing.Across(-1)));
        _ = Change(drafting, editing => editing.Across(1));
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Value, 0), form.Typing);
        Assert.Equal(DraftChange.None, Change(drafting, editing => editing.Across(1)));

        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Value, 2), form.Typing);

        _ = Change(drafting, editing => editing.Across(-1));
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Label, 2), form.Typing);
        _ = Change(drafting, editing => editing.Across(1));

        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Locked), form.Typing);

        drafting.Press(ShellKey.Up);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Label, 3), form.Typing);

        _ = Change(drafting, editing => editing.Across(1));
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Up);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Value, 1), form.Typing);

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Up);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Header), form.Typing);
    }

    /// <summary>A toggle walked to flips with <c>space</c> and again with <c>⏎</c>, and its box says which way it is.</summary>
    [Fact]
    public void SpaceAndEnter_FlipTheToggleWalkedTo()
    {
        var drafting = OnYou(APublicProfile.With(isBot: false, account: Me));
        var form = Editing(drafting);

        WalkTo(drafting, form, PublicProfilePart.Bot);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Space));
        Assert.True(form.IsBot);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("■ Bot", StringComparison.Ordinal));

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Enter));
        Assert.False(form.IsBot);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("□ Bot", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>ctrl-s</c> sends only what changed: a display name typed and a toggle flipped, and nothing about the bio,
    ///     the custom fields or the toggles left alone.
    /// </summary>
    [Fact]
    public void CtrlS_SendsOnlyWhatChanged()
    {
        var drafting = OnYou(APublicProfile.With(displayName: "Jeff", isBot: false, account: Me));
        var form = Editing(drafting);

        Change(drafting, editing => editing.RewriteDisplayName("Jeff M"));
        WalkTo(drafting, form, PublicProfilePart.Bot);
        drafting.Press(ShellKey.Space);

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        Assert.Equal([new PublicProfileChange { DisplayName = "Jeff M", IsBot = true }], drafting.Fakes.PublicProfile.Saves);
        Assert.DoesNotContain(form, drafting.Stack.Screens);
    }

    /// <summary>
    ///     A custom field changed sends every row the form holds as the account's fields, since Mastodon replaces them
    ///     as a set — and a row emptied is left out of them, which is what removes it.
    /// </summary>
    [Fact]
    public void ACustomFieldChanged_SendsThemAllAndAnEmptiedRowRemovesItsField()
    {
        var read = APublicProfile.With(
            fields: [new PublicProfileField("Site", "https://wooly.example"), new PublicProfileField("Pronouns", "he/him")],
            account: Me);
        var drafting = OnYou(read);

        Editing(drafting);

        Change(drafting, editing => editing.RewriteLabel(0, ""));
        Change(drafting, editing => editing.RewriteValue(0, ""));
        Change(drafting, editing => editing.RewriteLabel(2, "Home"));
        Change(drafting, editing => editing.RewriteValue(2, "Kansas"));

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        var sent = Assert.Single(drafting.Fakes.PublicProfile.Saves);

        Assert.Equal([new PublicProfileField("Pronouns", "he/him"), new PublicProfileField("Home", "Kansas")], sent.Fields);
        Assert.Equal(new PublicProfileChange { Fields = sent.Fields }, sent);
    }

    /// <summary>
    ///     A bio is compared line ending for line ending alike, so an editor writing its lines back its own way sends
    ///     nothing — and a save with nothing changed sends nothing, and says so.
    /// </summary>
    [Fact]
    public void NothingChanged_SendsNothingAndSaysSo()
    {
        var drafting = OnYou(APublicProfile.With(bio: "One\r\nTwo", account: Me));
        var form = Editing(drafting);

        Change(drafting, editing => editing.RewriteBio("One\nTwo"));

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.PublicProfile.Saves);
        Assert.Same(form, drafting.Stack.Top);
        Assert.Equal("Nothing has changed to save.", drafting.Notice);
    }

    /// <summary>
    ///     A save the instance refuses keeps the form open with every edit in it, and the status row says why in the
    ///     instance's own words — until an edit, which is doing what it asked, takes the notice down.
    /// </summary>
    [Fact]
    public void ARefusedSave_KeepsEveryEditAndSaysWhy()
    {
        var drafting = OnYou(FakePublicProfileEditor.Refusing(
            PublicProfileRefusedException.Saving("Display name is too long (maximum is 30 characters)"),
            APublicProfile.With(account: Me)));
        var form = Editing(drafting);

        Change(drafting, editing => editing.RewriteDisplayName("Jeff the very long display name indeed"));
        WalkTo(drafting, form, PublicProfilePart.Locked);
        drafting.Press(ShellKey.Space);

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        Assert.Same(form, drafting.Stack.Top);
        Assert.Equal("Jeff the very long display name indeed", form.DisplayName);
        Assert.True(form.IsLocked);
        Assert.Equal(
            "The instance would not take that change to your public profile: Display name is too long (maximum is 30 characters)",
            drafting.Notice);
        Assert.True(drafting.NoticeIsError);

        Change(drafting, editing => editing.RewriteDisplayName("Jeff"));

        Assert.Null(drafting.Notice);
    }
}
