using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Accounts;

namespace Wooly.Cli.Commands;

/// <summary>
///     What <c>account edit</c> can change about the own account's public profile (#404). Every option is something to
///     change, and leaving one off leaves that part as it is — so a command naming none asks for nothing, and is turned
///     down before anything is read or sent.
/// </summary>
internal sealed class AccountEditSettings : ProfileScopedSettings
{
    /// <summary>What reads the bio from stdin rather than the command line.</summary>
    public const string FromStandardInput = "-";

    /// <summary>How many custom fields Mastodon keeps.</summary>
    private const int MostFields = 4;

    [CommandOption("--display-name <NAME>")]
    [Description("The name to be shown as.")]
    public string? DisplayName { get; init; }

    [CommandOption("--bio <TEXT>")]
    [Description("What you say about yourself. --bio=- reads it from stdin; --bio \"\" removes it.")]
    public string? Bio { get; init; }

    [CommandOption("--locked <BOOL>")]
    [Description("true to approve followers by hand, false to let anyone follow.")]
    public bool? Locked { get; init; }

    [CommandOption("--bot <BOOL>")]
    [Description("true to say the account posts automatically, false to say it does not.")]
    public bool? Bot { get; init; }

    [CommandOption("--discoverable <BOOL>")]
    [Description("true to be suggested and listed in the directory, false not to be.")]
    public bool? Discoverable { get; init; }

    [CommandOption("--field <NAME=VALUE>")]
    [Description("A custom field, as 'Name=Value'. Repeat for up to 4; any --field replaces all of them.")]
    public string[] Fields { get; init; } = [];

    [CommandOption("--clear-fields")]
    [Description("Remove every custom field.")]
    public bool ClearFields { get; init; }

    [CommandOption("--json")]
    [Description("Write the account as JSON, for another program to read.")]
    public bool Json { get; init; }

    /// <summary>Whether the bio is to be read from stdin rather than taken as typed.</summary>
    public bool BioFromStandardInput => Bio == FromStandardInput;

    /// <summary>
    ///     What these settings ask to change, with the bio as <paramref name="bio" /> where it was typed as
    ///     <see cref="FromStandardInput" />.
    /// </summary>
    /// <param name="bio">What stdin said, or <see langword="null" /> where the bio was not to be read from it.</param>
    public PublicProfileChange ToChange(string? bio) => new()
    {
        DisplayName = DisplayName,
        Bio = BioFromStandardInput ? bio : Bio,
        Fields = ClearFields ? [] : Fields.Length > 0 ? [.. Fields.Select(ToField)] : null,
        IsLocked = Locked,
        IsBot = Bot,
        IsDiscoverable = Discoverable,
    };

    public override ValidationResult Validate()
    {
        var shared = base.Validate();

        if (!shared.Successful)
        {
            return shared;
        }

        if (DisplayName is null && Bio is null && Locked is null && Bot is null && Discoverable is null
            && Fields.Length == 0 && !ClearFields)
        {
            return ValidationResult.Error(
                "Say what to change: --display-name, --bio, --locked, --bot, --discoverable, --field or --clear-fields.");
        }

        if (ClearFields && Fields.Length > 0)
        {
            return ValidationResult.Error("--clear-fields removes every field, so it cannot be given with --field.");
        }

        if (Fields.Length > MostFields)
        {
            return ValidationResult.Error($"An account has at most {MostFields} custom fields, and {Fields.Length} were given.");
        }

        return Fields.FirstOrDefault(field => field.IndexOf('=') < 1) is { } malformed
            ? ValidationResult.Error($"'{malformed}' is not a field: write it as 'Name=Value'.")
            : ValidationResult.Success();
    }

    /// <summary>One <c>--field</c>, divided at its first <c>=</c>, since a value may be an address carrying its own.</summary>
    private static PublicProfileField ToField(string field)
    {
        var equals = field.IndexOf('=');

        return new PublicProfileField(field[..equals], field[(equals + 1)..]);
    }
}
