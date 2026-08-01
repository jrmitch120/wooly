using Wooly.Core.Accounts;

namespace Wooly.Core.Conversations;

/// <summary>
///     How a direct message says who it is for. Mastodon delivers a direct post to the accounts its text mentions and to
///     nobody else, so who a message is addressed to is part of what is written rather than a field alongside it. That
///     is the whole of what sending one adds to publishing one, and it lives here so both front ends address a message
///     the same way.
/// </summary>
public static class DirectMessage
{
    /// <summary>The text of a message to <paramref name="account" />, saying <paramref name="text" />.</summary>
    /// <remarks>
    ///     The mention leads, which is where Mastodon's own clients put it and where a reader's eye expects it. A
    ///     message with nothing written in it is the mention alone rather than a mention with a space after it — one
    ///     that carries a file and no words is a thing somebody sends.
    /// </remarks>
    public static string To(AccountAddress account, string text) => To([account], text);

    /// <summary>
    ///     The same, for a message going to more than one account — which is what answering a conversation with
    ///     several accounts in it is. Every one of them is named, in the order given, because Mastodon delivers to the
    ///     accounts the text mentions and a reply that named only the last speaker would drop the rest of the
    ///     conversation out of it.
    /// </summary>
    /// <returns>The text, or an empty string where no account was named — nobody to write to is nothing to write.</returns>
    public static string To(IReadOnlyList<AccountAddress> accounts, string text)
    {
        if (accounts.Count == 0)
        {
            return text;
        }

        var mentions = string.Join(" ", accounts.Select(account => $"@{account}"));

        return string.IsNullOrWhiteSpace(text) ? mentions : $"{mentions} {text}";
    }
}
