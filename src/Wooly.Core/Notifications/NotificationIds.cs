namespace Wooly.Core.Notifications;

/// <summary>How two notification ids order: as the numbers Mastodon makes them, though they travel as strings.</summary>
public static class NotificationIds
{
    /// <summary>Whether <paramref name="id" /> is newer than <paramref name="than" />: longer, or later at the same length.</summary>
    public static bool Newer(string id, string than) =>
        id.Length != than.Length ? id.Length > than.Length : string.CompareOrdinal(id, than) > 0;
}
