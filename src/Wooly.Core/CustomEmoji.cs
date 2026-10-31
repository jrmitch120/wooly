namespace Wooly.Core;

/// <summary>
///     A small picture an instance defines for itself, written in text as its <b>shortcode</b> between colons
///     (GLOSSARY.md's <b>Custom emoji</b>). Whatever carries text that may use one — a post, an account, a poll — lists
///     the ones it uses, and only those: a <c>:word:</c> it does not list is the words someone wrote (ADR-0035).
/// </summary>
/// <param name="Shortcode">The name it is written as, without its colons: <c>php</c> for <c>:php:</c>.</param>
/// <param name="StaticUrl">
///     Where its still picture is. The wire's other address, which animates where the emoji does, is not carried:
///     nothing in Wooly moves on its own, so an animated emoji is drawn at its still.
/// </param>
public sealed record CustomEmoji(string Shortcode, string StaticUrl)
{
    /// <summary>How it is written in text: its shortcode between colons.</summary>
    public string Written => $":{Shortcode}:";
}
