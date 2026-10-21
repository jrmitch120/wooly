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

    [CommandOption("--display-name <NAME>")]
    [Description("The name to be shown as.")]
    public string? DisplayName { get; init; }

    [CommandOption("--bio <TEXT>")]
    [Description("What you say about yourself. --bio=- reads it from stdin; --bio \"\" removes it.")]
    public string? Bio { get; init; }

    [CommandOption("--avatar <FILE>")]
    [Description("A picture to show as your avatar. The instance says which sizes and types it takes.")]
    public string? Avatar { get; init; }

    [CommandOption("--header <FILE>")]
    [Description("A picture to show across the top of your public profile. The instance says which it takes.")]
    public string? Header { get; init; }

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

    /// <summary>What these settings ask to change.</summary>
    /// <param name="piped">
    ///     Reads the bio from stdin, called only where it was typed as <see cref="FromStandardInput" />.
    /// </param>
    public PublicProfileChange ToChange(Func<string> piped) => new()
    {
        DisplayName = DisplayName,
        Bio = Bio == FromStandardInput ? piped() : Bio,
        Fields = ClearFields ? [] : Fields.Length > 0 ? [.. Fields.Select(ToField)] : null,
        IsLocked = Locked,
        IsBot = Bot,
        IsDiscoverable = Discoverable,
        Avatar = Avatar,
        Header = Header,
    };

    public override ValidationResult Validate()
    {
        var shared = base.Validate();

        if (!shared.Successful)
        {
            return shared;
        }

        if (ClearFields && Fields.Length > 0)
        {
            return ValidationResult.Error("--clear-fields removes every field, so it cannot be given with --field.");
        }

        if (Fields.Length > PublicProfileField.MastodonKeeps)
        {
            return ValidationResult.Error($"An account has at most {PublicProfileField.MastodonKeeps} custom fields, and {Fields.Length} were given.");
        }

        if (Fields.FirstOrDefault(field => field.IndexOf('=') < 1) is { } malformed)
        {
            return ValidationResult.Error($"'{malformed}' is not a field: write it as 'Name=Value'.");
        }

        // Here rather than left to the save, which would find it only after the public profile had been read. Nothing
        // else about a picture is judged: its size and type are the instance's to refuse (ADR-0028).
        if ((Missing("--avatar", Avatar) ?? Missing("--header", Header)) is { } missing)
        {
            return ValidationResult.Error(missing);
        }

        // Last, once every --field is known to divide. Stdin is not read to find out: a bio to be piped in is a bio
        // asked for, whatever arrives.
        return ToChange(piped: () => FromStandardInput).IsEmpty
            ? ValidationResult.Error(
                "Say what to change: --display-name, --bio, --avatar, --header, --locked, --bot, --discoverable, " +
                "--field or --clear-fields.")
            : ValidationResult.Success();
    }

    /// <summary>What is wrong with the picture <paramref name="option" /> names, or nothing where it is there to send.</summary>
    private static string? Missing(string option, string? picture) =>
        picture is null || File.Exists(picture) ? null : $"There is no file at '{picture}' for {option}.";

    /// <summary>One <c>--field</c>, divided at its first <c>=</c>, since a value may be an address carrying its own.</summary>
    private static PublicProfileField ToField(string field)
    {
        var equals = field.IndexOf('=');

        return new PublicProfileField(field[..equals], field[(equals + 1)..]);
    }
}
