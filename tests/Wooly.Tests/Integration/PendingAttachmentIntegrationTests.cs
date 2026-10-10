using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs the TUI's way of attaching against the live instance (ADR-0026, #375): a file sent up on its own as a
///     pending attachment, the instance's processing of it waited out, and a post published naming it by id — and the
///     instance refusing a post that names one it has not finished processing, which is why a send waits.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class PendingAttachmentIntegrationTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>A picture goes up, comes back ready, and goes out on the post that names it.</summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Attach_ThenPublishAttached_PublishesThePostCarryingIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = LiveInstance.NewServices().GetRequiredService<IPostAuthor>();
        var profile = LiveInstance.Profile;
        var reported = new Reported();

        var attached = await author.Attach(profile, Picture("dot.png", frames: 1), reported, cancellationToken);

        Assert.Equal(MediaKind.Image, attached.Kind);
        Assert.Contains(reported.All, report => report is AttachmentProgress.Sending { Done: 1 });

        var published = await author.PublishAttached(
            profile,
            new PostDraft
            {
                Text = string.Empty,
                Visibility = PostVisibility.Unlisted,
                VisibilityChosen = true,
                Attached = [attached],
            },
            cancellationToken);

        try
        {
            Assert.Equal(attached.Id, Assert.Single(published.Media).Id);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     A description put on a pending attachment, and then changed, is the one the published post carries (#377).
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Describe_ThenPublishAttached_CarriesTheLastDescription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = LiveInstance.NewServices().GetRequiredService<IPostAuthor>();
        var profile = LiveInstance.Profile;

        var attached = await author.Attach(profile, Picture("dot.png", frames: 1), new Reported(), cancellationToken);

        await author.Describe(profile, attached.Id, "A dot", cancellationToken);
        await author.Describe(profile, attached.Id, "A single black dot", cancellationToken);

        var published = await author.PublishAttached(
            profile,
            new PostDraft
            {
                Text = string.Empty,
                Visibility = PostVisibility.Unlisted,
                VisibilityChosen = true,
                Attached = [attached],
            },
            cancellationToken);

        try
        {
            Assert.Equal("A single black dot", Assert.Single(published.Media).Description);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>
    ///     An animation is processed after it is up — the instance turns it into a clip — and a post naming it before
    ///     that is refused; once the processing has been waited out, the same post goes out.
    /// </summary>
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task PublishAttached_IsRefusedWhileTheInstanceIsStillProcessing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = LiveInstance.NewServices().GetRequiredService<IPostAuthor>();
        var profile = LiveInstance.Profile;
        var reported = new Reported();

        var draft = new PostDraft
        {
            Text = $"wooly integration test {Guid.NewGuid():N}",
            Visibility = PostVisibility.Unlisted,
            VisibilityChosen = true,
        };

        var attaching = author.Attach(profile, Picture("blink.gif", frames: 2), reported, cancellationToken);

        // The attach is watched as well as the report: an upload the instance refuses ends it without ever reporting
        // processing, and that failure is the one worth seeing, not a wait that runs out.
        var first = await Task.WhenAny(attaching, reported.Processing.Task)
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        if (first == attaching)
        {
            await attaching;
            Assert.Skip("The instance finished processing before it answered the upload.");
        }

        var processing = await reported.Processing.Task;

        Post? early = null;

        try
        {
            // Named while the instance still has it in hand. Should this instance have finished already, there is no
            // refusal left to see.
            early = await author.PublishAttached(
                profile,
                draft with { Attached = [new PendingAttachment(processing.Id, MediaKind.Animation)] },
                cancellationToken);
        }
        catch (PostRefusedException)
        {
            // What the instance says to a post naming media it has not finished processing.
        }

        if (early is not null)
        {
            await author.Delete(profile, early.Id, cancellationToken);
            Assert.Skip("The instance finished processing before the early publish reached it.");
        }

        var attached = await attaching;
        var published = await author.PublishAttached(profile, draft with { Attached = [attached] }, cancellationToken);

        try
        {
            Assert.Equal(attached.Id, Assert.Single(published.Media).Id);
        }
        finally
        {
            await author.Delete(profile, published.Id, cancellationToken);
        }
    }

    /// <summary>A small picture of <paramref name="frames" /> frames, written as <paramref name="name" />.</summary>
    private string Picture(string name, int frames)
    {
        var path = Path.Combine(_files.Path, name);

        // An animation is drawn at 64 pixels a side: the instance turns it into a clip at a bitrate worked out from its
        // size, and at 16 that comes to under 1 kbit/s, which its encoder refuses, failing the upload (#390). 32 is
        // the smallest that goes through; 64 leaves room.
        var side = frames > 1 ? 64 : 16;

        using var picture = new Image<Rgba32>(side, side, new Rgba32(200, 40, 40));

        for (var frame = 1; frame < frames; frame++)
        {
            using var next = new Image<Rgba32>(side, side, new Rgba32(40, 40, 200));

            picture.Frames.AddFrame(next.Frames.RootFrame);
        }

        if (frames > 1)
        {
            picture.Save(path, new GifEncoder());
        }
        else
        {
            picture.SaveAsPng(path);
        }

        return path;
    }

    /// <summary>Every report an attach made, and the first that said the file was up and being processed.</summary>
    private sealed class Reported : IProgress<AttachmentProgress>
    {
        public List<AttachmentProgress> All { get; } = [];

        public TaskCompletionSource<AttachmentProgress.Processing> Processing { get; } = new();

        public void Report(AttachmentProgress value)
        {
            lock (All)
            {
                All.Add(value);
            }

            if (value is AttachmentProgress.Processing processing)
            {
                Processing.TrySetResult(processing);
            }
        }
    }
}
