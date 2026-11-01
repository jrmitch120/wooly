using Wooly.Core.Posts;

namespace Wooly.Tui.Media;

/// <summary>
///     A picture the TUI draws in place: which one it is, and where its pixels are fetched from.
/// </summary>
/// <remarks>
///     The key the whole picture path turns on — what is held, what is sent for, and what a box on a row stands in
///     for. Named separately from what it was made of because two different things are drawn now and only one of them
///     is an <c>Attachment</c> in this project's vocabulary (GLOSSARY.md): a post's picture hangs off the post, and an
///     author's avatar hangs off the author. Keying on <see cref="PostMedia" /> and passing an avatar off as one would
///     have made "something attached to a post" mean something else wherever the avatar went.
/// </remarks>
/// <param name="Id">
///     What tells one picture from another, for as long as it is held. Namespaced by where it came from, so that an
///     attachment's id and an account's handle cannot collide in the one cache they share.
/// </param>
/// <param name="Address">Where the pixels are, which is what is actually fetched.</param>
public sealed record Drawn(string Id, string Address)
{
    /// <summary>
    ///     The largest box this picture is ever drawn in, in cells, or <see langword="null" /> where that is as wide as
    ///     the window and as tall as the post screen allows — which is every picture but an avatar.
    /// </summary>
    /// <remarks>
    ///     What <see cref="Pictures" /> decodes it to, so that a picture is never held larger than it can be drawn
    ///     (ADR-0025). An avatar's box is fixed, and an instance serves it at hundreds of pixels square: decoded to
    ///     the window instead, each one would cost hundreds of kilobytes to draw a few dozen pixels. Its columns are
    ///     the cell's to say, so it is held as wide as the header's box can be on any cell
    ///     (<see cref="Rendering.Avatar.MostHeaderColumns" />), which costs a square avatar nothing: fitted, it is held
    ///     at the header's height.
    /// </remarks>
    public (int Columns, int Rows)? Largest { get; init; }

    /// <summary>
    ///     Whether this is held apart from the timeline's pictures, for as long as a frame wants it and no longer: an
    ///     attachment's original on the Picture screen (ADR-0034).
    /// </summary>
    /// <remarks>
    ///     <see cref="Pictures" /> counts it against neither of its budgets and lets nothing else go to make room for
    ///     it, lets go of it the first frame that does not want it, and reads up to
    ///     <see cref="Pictures.MostBytesHeldApart" /> of it rather than <see cref="Pictures.MostBytes" /> — one post's
    ///     few pictures, held only while its Picture screen is open, can afford what a feed of them could not.
    /// </remarks>
    public bool HeldApart { get; init; }

    /// <summary>
    ///     Whether this is a custom emoji's still picture, which the ways that draw through a box draw from boxes and
    ///     cuts of their own (ADR-0035): a page holds dozens of them, and in the boxes and cuts sized for a few large
    ///     pictures they would leave an attachment undrawn and its cuts thrown away.
    /// </summary>
    public bool IsCustomEmoji { get; init; }

    /// <summary>
    ///     How this takes the box it is drawn in once it is here (<see cref="Rendering.Inset.Fill" />): fitted whole
    ///     for an avatar and a custom emoji, and cropped from the middle for every other picture.
    /// </summary>
    internal BoxFill Fill { get; init; }

    /// <summary>
    ///     An attachment on a post, at the smaller copy where the instance offered one: a feed holds dozens of
    ///     pictures, and holding each at full size would cost the memory of a few of them and fetching each somebody's
    ///     data allowance. The original is fetched only for the Picture screen (<see cref="Original" />, ADR-0034) —
    ///     except where the instance offered no smaller copy, and the original is all there is to draw here too.
    /// </summary>
    /// <remarks>
    ///     Falling back to the attachment's own file is only ever a still picture's fallback, and the caller is what
    ///     holds that: <see cref="PostMedia.IsDrawable" /> is false for a video or an animation the instance offered no
    ///     preview of, so nothing reaches here that would send for a whole video only to fail to decode it (#110).
    /// </remarks>
    public static Drawn Attached(PostMedia media) => new(media.Id, media.Preview ?? media.Url);

    /// <summary>
    ///     An attachment's original, as the Picture screen draws it over the window (ADR-0034): named apart from the
    ///     smaller copy <see cref="Attached" /> names, so that the two are held as two pictures and the smaller copy
    ///     stays held, for the timeline, after the original is let go; held apart (<see cref="HeldApart" />); and
    ///     decoded no larger than the window it is drawn in.
    /// </summary>
    /// <param name="media">The attachment, as the post carries it.</param>
    /// <param name="columns">How many columns the Picture screen has for it.</param>
    /// <param name="rows">How many rows the Picture screen has for it, less its description's rows.</param>
    public static Drawn Original(PostMedia media, int columns, int rows) => new($"original:{media.Id}", media.Url)
    {
        Largest = (columns, rows),
        HeldApart = true,
    };

    /// <summary>
    ///     The picture an instance chose for a link it previewed, or <see langword="null" /> where it chose none —
    ///     which is the one place a link preview's picture can be absent, so the question is answered here rather than
    ///     at whoever is about to draw it (ADR-0018).
    /// </summary>
    /// <remarks>
    ///     Named by the link's own address rather than by the picture's: the same article shared by two accounts is one
    ///     picture, however each instance spells the proxy it serves the pixels through.
    /// </remarks>
    public static Drawn? LinkPreview(LinkPreview link) =>
        link.Image is { } image ? new Drawn($"link:{link.Url}", image) : null;

    /// <summary>
    ///     An account's avatar, named by the handle and the address together: the same author's avatar is one picture,
    ///     fetched once, for a whole feed of their posts — and a new one, which the instance serves from a new address,
    ///     is a new picture rather than the old one still held under the same handle (review of #403).
    /// </summary>
    /// <param name="account">Whose avatar it is, as <c>username@instance</c>.</param>
    /// <param name="address">Where to fetch it.</param>
    public static Drawn Avatar(string account, string address) => new($"avatar:{account} {address}", address)
    {
        Largest = (Rendering.Avatar.MostHeaderColumns, Rendering.Avatar.HeaderRows),
        Fill = BoxFill.Fitted,
    };

    /// <summary>
    ///     A custom emoji, at its still picture (ADR-0035): named by that address alone, so that one custom emoji is
    ///     one picture, fetched and decoded once, for every post and name that uses it — under whichever shortcode — and
    ///     held no larger than the two cells by one it is drawn in.
    /// </summary>
    /// <param name="customEmoji">The custom emoji, as whatever uses it lists it.</param>
    public static Drawn CustomEmoji(Core.CustomEmoji customEmoji) =>
        new($"custom-emoji:{customEmoji.StaticUrl}", customEmoji.StaticUrl)
    {
        Largest = (Rendering.CustomEmojiBox.Columns, Rendering.CustomEmojiBox.Rows),
        IsCustomEmoji = true,
        Fill = BoxFill.Fitted,
    };

    /// <summary>
    ///     An account's header, as the edit public profile form draws it (#407): named by the handle and the address,
    ///     as <see cref="Avatar" /> is, and held no larger than the form's box.
    /// </summary>
    /// <param name="account">Whose header it is, as <c>username@instance</c>.</param>
    /// <param name="address">Where to fetch it.</param>
    public static Drawn HeaderPicture(string account, string address) => new($"header:{account} {address}", address)
    {
        Largest = (Rendering.PublicProfilePictures.HeaderColumns, Rendering.PublicProfilePictures.Rows),
    };

    /// <summary>
    ///     A file being attached to a post, drawn small on its row under the Media header (#382): read off the disk
    ///     rather than sent for, being on no instance until it has gone up — and wanted from the moment it is attached,
    ///     not once it has.
    /// </summary>
    /// <remarks>
    ///     Named by its path, so that a file attached, taken off and brought back is the one picture, and decoded no
    ///     larger than its row's box (<see cref="Largest" />): a camera's photograph, held at the window's width to be
    ///     drawn three cells wide, would cost the cache a feed's worth of pictures.
    /// </remarks>
    /// <param name="path">Where the file is on this machine.</param>
    public static Drawn Attaching(string path) => new($"attaching:{path}", new Uri(path).AbsoluteUri)
    {
        Largest = (Rendering.AttachmentPicture.Columns, 1),
    };

    /// <summary>
    ///     An attachment the post being edited already carries, drawn small on its row under the Media header as a file
    ///     being attached is (#381, review of #372): sent for from the instance's preview, as the feed's are, and named
    ///     apart from the feed's, since it is held no larger than the row's box (<see cref="Largest" />) where the feed
    ///     holds it at the window's width.
    /// </summary>
    /// <param name="media">The attachment, as the post carries it.</param>
    public static Drawn Kept(PostMedia media) => new($"kept:{media.Id}", media.Preview ?? media.Url)
    {
        Largest = (Rendering.AttachmentPicture.Columns, 1),
    };

    /// <summary>
    ///     A file on this machine drawn large (#382): the one under the file browser's cursor, or the one being
    ///     described. Read off the disk as a file being attached is, and named apart from it, since it is drawn far
    ///     larger here than on a row and is held at the size it is drawn.
    /// </summary>
    /// <param name="path">Where the file is on this machine.</param>
    public static Drawn OnDisk(string path) => new($"on-disk:{path}", new Uri(path).AbsoluteUri);

    /// <summary>
    ///     The blur a <b>Stand-in</b> draws of <paramref name="picture" /> while it is on its way (#349).
    /// </summary>
    /// <remarks>
    ///     Named apart from the picture it stands in for, so that everything keyed by a picture's id — a box, a Kitty
    ///     image, a sixel's crops — holds the two as two things, and lets go of the blur when the picture replaces it
    ///     rather than mistaking one for the other. Its address is the picture's and is never fetched: a blur is
    ///     decoded from the post (<see cref="Blurs" />), not sent for.
    /// </remarks>
    public static Drawn Blur(Drawn picture) => new($"blur:{picture.Id}", picture.Address);
}
