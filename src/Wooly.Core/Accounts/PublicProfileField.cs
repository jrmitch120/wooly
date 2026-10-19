namespace Wooly.Core.Accounts;

/// <summary>
///     One custom field as the account wrote it, and as it is sent back: a label and what it says, with none of the
///     HTML an instance serves it as, nor the verification an instance stamps on it (<see cref="AccountField" />).
/// </summary>
/// <param name="Label">What the account calls the row.</param>
/// <param name="Value">What the row says.</param>
public sealed record PublicProfileField(string Label, string Value);
