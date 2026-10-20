using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="PublicProfileEditor" /> against the live instance, for what no scripted answer can settle (#404,
///     ADR-0028): that Mastodon takes the hand-rolled save as sent — <c>bot</c> included, which Mastonet cannot send — and
///     hands back what was written under <c>source</c> rather than the HTML it serves, and that it refuses a display name
///     longer than it allows in words of its own. And that it takes pictures in that same save, multipart (#405).
///     <para>
///         The seeded account is shared by the whole suite, so whatever a test changes it puts back as it was read, and
///         <c>locked</c> is left alone: a locked account would turn every follow another test makes into a request. The
///         pictures are the exception, having no way back short of removing them, which is not this client's yet — and no
///         other test reads them.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class PublicProfileEditorIntegrationTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>An avatar and a header go up with the display name in one save, and are what the account then shows.</summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Save_TakesPicturesInTheSameSaveAsEverythingElse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = LiveInstance.Profile;
        var editor = LiveInstance.NewServices().GetRequiredService<IPublicProfileEditor>();

        var original = await editor.Read(profile, cancellationToken);

        try
        {
            await editor.Save(
                profile,
                new PublicProfileChange
                {
                    DisplayName = "Wooly Pictured",
                    Avatar = Picture("avatar.png", 64, 64),
                    Header = Picture("header.png", 300, 100),
                },
                cancellationToken);

            var read = await editor.Read(profile, cancellationToken);

            Assert.Equal("Wooly Pictured", read.DisplayName);
            Assert.NotEqual(original.AvatarUrl, read.AvatarUrl);
            Assert.NotEqual(original.HeaderUrl, read.HeaderUrl);
        }
        finally
        {
            await editor.Save(profile, new PublicProfileChange { DisplayName = original.DisplayName }, cancellationToken);
        }
    }

    /// <summary>
    ///     Nothing about a picture is judged before it is sent (ADR-0028), so a file that only says it is one is the
    ///     instance's to turn down, in its own words — and with it the rest of the save, which is one request.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Save_SaysWhatTheInstanceRefusedAboutAPictureInItsOwnWords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = LiveInstance.Profile;
        var editor = LiveInstance.NewServices().GetRequiredService<IPublicProfileEditor>();

        var original = await editor.Read(profile, cancellationToken);

        var refused = await Assert.ThrowsAsync<PublicProfileRefusedException>(() =>
            editor.Save(
                profile,
                new PublicProfileChange
                {
                    DisplayName = "Wooly Refused",
                    Avatar = _files.WriteFile("not-a-picture.png", "plain text, whatever its name says"),
                },
                cancellationToken));

        Assert.NotNull(refused.Reason);
        Assert.Contains("Avatar", refused.Reason);
        Assert.Equal(original.DisplayName, (await editor.Read(profile, cancellationToken)).DisplayName);
    }

    /// <summary>A plain picture <paramref name="width" /> by <paramref name="height" />, as a PNG named <paramref name="name" />.</summary>
    private string Picture(string name, int width, int height)
    {
        var path = Path.Combine(_files.Path, name);

        using var picture = new Image<Rgba32>(width, height, new Rgba32(40, 120, 200));

        picture.SaveAsPng(path);

        return path;
    }
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Save_IsTakenAsSentAndReadBackAsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = LiveInstance.Profile;
        var editor = LiveInstance.NewServices().GetRequiredService<IPublicProfileEditor>();

        var original = await editor.Read(profile, cancellationToken);

        // A link and a second line, both of which the HTML an instance serves would have flattened or wrapped.
        var bio = $"Writes https://wooly.example\nrun {Guid.NewGuid():N}";
        IReadOnlyList<PublicProfileField> fields =
            [new("Site", "https://wooly.example"), new("Pronouns", "they/them")];

        try
        {
            var saved = await editor.Save(
                profile,
                new PublicProfileChange
                {
                    DisplayName = "Wooly Tester",
                    Bio = bio,
                    Fields = fields,
                    IsBot = !original.IsBot,
                    IsDiscoverable = !original.IsDiscoverable,
                },
                cancellationToken);

            Assert.Equal("Wooly Tester", saved.Author);
            Assert.Equal(!original.IsBot, saved.IsBot);

            var read = await editor.Read(profile, cancellationToken);

            Assert.Equal("Wooly Tester", read.DisplayName);
            Assert.Equal(bio, read.Bio);
            Assert.Equal(fields, read.Fields);
            Assert.Equal(!original.IsBot, read.IsBot);
            Assert.Equal(!original.IsDiscoverable, read.IsDiscoverable);

            // Fewer fields than before is the blanks doing their work: the rows a save leaves blank are dropped.
            await editor.Save(profile, new PublicProfileChange { Fields = [new("Site", "https://wooly.example")] }, cancellationToken);

            Assert.Equal(
                [new PublicProfileField("Site", "https://wooly.example")],
                (await editor.Read(profile, cancellationToken)).Fields);
        }
        finally
        {
            await editor.Save(
                profile,
                new PublicProfileChange
                {
                    DisplayName = original.DisplayName,
                    Bio = original.Bio,
                    Fields = original.Fields,
                    IsBot = original.IsBot,
                    IsDiscoverable = original.IsDiscoverable,
                },
                cancellationToken);
        }
    }

    /// <summary>
    ///     Nothing is refused before sending (ADR-0028), so the instance's own limit is what turns this down, and its
    ///     reason is what is said. Far past any limit rather than one past Mastodon's, because the limit moves: 30
    ///     characters until 4.6 made it 40.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Save_SaysWhatTheInstanceRefusedInItsOwnWords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = LiveInstance.Profile;
        var editor = LiveInstance.NewServices().GetRequiredService<IPublicProfileEditor>();

        var original = await editor.Read(profile, cancellationToken);

        var refused = await Assert.ThrowsAsync<PublicProfileRefusedException>(async () =>
        {
            try
            {
                await editor.Save(profile, new PublicProfileChange { DisplayName = new string('x', 1000) }, cancellationToken);
            }
            finally
            {
                // Put back should the instance have taken it after all, so a failure here costs no other test its name.
                if ((await editor.Read(profile, cancellationToken)).DisplayName != original.DisplayName)
                {
                    await editor.Save(profile, new PublicProfileChange { DisplayName = original.DisplayName }, cancellationToken);
                }
            }
        });

        Assert.NotNull(refused.Reason);
        Assert.Contains("Display name", refused.Reason);
    }
}
