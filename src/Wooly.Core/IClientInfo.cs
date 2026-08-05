namespace Wooly.Core;

/// <summary>
///     What build is running. Carries no name, because a name here would have to be one of two different things: what
///     an instance knows this client as (<see cref="WoolyClient.Name" />) or what a user typed to start it, which is
///     the front end's own. The version is the one answer that is the same whoever asks.
/// </summary>
public interface IClientInfo
{
    /// <summary>The client's release version, e.g. <c>0.1.0</c>.</summary>
    string Version { get; }
}
