namespace Wooly.Tui.Shell;

/// <summary>
///     The bell for one re-read of the Inbox badges, however many of them it reads, which rings at most once (#399). A
///     poll reading all three is one re-read; so is a stream event re-reading the one or two counts it touches (#401) —
///     a direct message is a mention and a conversation, and one message is one sound.
/// </summary>
/// <param name="ring">What ringing is, said once by whoever made this — the shell, which knows the preference.</param>
internal sealed class BellOnce(Action ring)
{
    private bool _rang;

    /// <summary>
    ///     A badge this re-read read came back higher than the last count of it: the bell, unless an earlier one in the
    ///     same re-read already rang it.
    /// </summary>
    public void Rose()
    {
        if (_rang)
        {
            return;
        }

        _rang = true;
        ring();
    }
}
