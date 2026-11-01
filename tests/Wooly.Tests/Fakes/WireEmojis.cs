namespace Wooly.Tests.Fakes;

/// <summary>
///     The <c>emojis</c> array an instance lists on a status, an account or a poll, for the wire-seam tests that read
///     one back (ADR-0005, ADR-0035).
/// </summary>
internal static class WireEmojis
{
    /// <summary>
    ///     The custom emoji given, as the wire serves them: each picture at an address of its own, and again as a still
    ///     beside it (<c>-static</c> here), which is the one an animated emoji is drawn at.
    /// </summary>
    public static string Json(params (string Shortcode, string Url)[] emojis) =>
        "[" + string.Join(
            ",",
            emojis.Select(emoji =>
                $$"""
                  {
                    "shortcode": "{{emoji.Shortcode}}",
                    "url": "{{emoji.Url}}",
                    "static_url": "{{emoji.Url[..emoji.Url.LastIndexOf('.')]}}-static.png",
                    "visible_in_picker": true
                  }
                  """)) + "]";
}
