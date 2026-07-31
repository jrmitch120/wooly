using Wooly.Core.Posts;

namespace Wooly.Core.Errors;

/// <summary>
///     A reply was asked to go out wider than the post it answers. Refused rather than quietly narrowed, because this
///     can only happen where the visibility was named on this invocation: a standing preference that is too wide is
///     narrowed to fit without comment, and only somebody who typed the word is told that the word cannot have it.
///     <para>
///         Refusing is the answer a script can act on. Narrowing an explicit ask would publish something other than
///         what was asked for, and under a pipe the sentence saying so is not read by anything.
///     </para>
/// </summary>
public sealed class WiderReplyException(PostVisibility asked, PostVisibility answered)
    : WoolyException(
        $"A reply cannot be published {PostVisibilityName.Of(asked)} to a post that went out "
        + $"{PostVisibilityName.Of(answered)}. Answer it as narrowly as it was said — leave the visibility unsaid and "
        + $"the reply goes out {PostVisibilityName.Of(answered)} — or say something narrower.")
{
    /// <summary>The visibility that was asked for.</summary>
    public PostVisibility Asked { get; } = asked;

    /// <summary>The visibility of the post being answered, which is the widest the reply may go out at.</summary>
    public PostVisibility Answered { get; } = answered;
}
