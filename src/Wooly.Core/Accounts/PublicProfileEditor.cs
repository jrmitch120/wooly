using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Accounts;

/// <summary>
///     Reads the own account's public profile off <c>verify_credentials</c>'s <c>source</c> and saves a change to it
///     with <c>PATCH /api/v1/accounts/update_credentials</c> (#404, ADR-0028).
///     <para>
///         Both by hand through <see cref="RawMastodonCall" /> rather than through Mastonet. Its account has no
///         <c>source</c> fields to read the bio as written from, and its <c>UpdateCredentials</c> cannot send
///         <c>bot</c> — and a save split into Mastonet's call and a second one of this client's own for that one flag
///         could be half taken, the name saved and the flag refused. One call is all or nothing, and says a refusal
///         in the instance's own words. The pictures, which go in the same request (ADR-0028), will join it there.
///     </para>
/// </summary>
public sealed class PublicProfileEditor(IHttpClientFactory httpClientFactory) : IPublicProfileEditor
{
    /// <summary>How many custom fields Mastodon keeps, all of which a save that touches any of them sends.</summary>
    private const int FieldCount = 4;

    /// <inheritdoc />
    public async Task<PublicProfile> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            "api/v1/accounts/verify_credentials",
            content: null,
            cancellationToken);

        var body = await Answered(profile, response, PublicProfileRefusedException.Reading, cancellationToken);
        var wire = JsonSerializer.Deserialize<CredentialsWire>(body) ?? throw new JsonException("No account.");
        var source = wire.Source;

        return new PublicProfile
        {
            DisplayName = wire.DisplayName ?? "",

            // As written. An instance too old or too odd to say what was written has nothing here to start an edit
            // from but the flattened bio, and saving that back is what ADR-0028 refuses — so it starts from nothing.
            Bio = source?.Note ?? "",
            Fields = source?.Fields?.Select(field => new PublicProfileField(field.Name ?? "", field.Value ?? ""))
                           .ToList() ?? [],
            AvatarUrl = MastodonWire.SaidOrNothing(wire.Avatar),
            HeaderUrl = MastodonWire.SaidOrNothing(wire.Header),
            IsLocked = wire.Locked ?? false,
            IsBot = wire.Bot ?? false,

            // The account's own setting is source's; the top-level one is what everyone else is shown, and is null on
            // an account that never chose.
            IsDiscoverable = source?.Discoverable ?? wire.Discoverable ?? false,
            Account = AccountWire.ToAccount(Account(body), profile.Instance),
        };
    }

    /// <inheritdoc />
    public async Task<Account> Save(
        ActiveProfile profile,
        PublicProfileChange change,
        CancellationToken cancellationToken)
    {
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Patch,
            "api/v1/accounts/update_credentials",
            new FormUrlEncodedContent(Sent(change)),
            cancellationToken);

        var body = await Answered(profile, response, PublicProfileRefusedException.Saving, cancellationToken);

        return AccountWire.ToAccount(Account(body), profile.Instance);
    }

    /// <summary>What <paramref name="change" /> names, as the form fields the endpoint takes, and nothing it leaves.</summary>
    private static IEnumerable<KeyValuePair<string, string>> Sent(PublicProfileChange change)
    {
        if (change.DisplayName is { } displayName)
        {
            yield return new("display_name", displayName);
        }

        if (change.Bio is { } bio)
        {
            yield return new("note", bio);
        }

        if (change.IsLocked is { } locked)
        {
            yield return new("locked", Said(locked));
        }

        if (change.IsBot is { } bot)
        {
            yield return new("bot", Said(bot));
        }

        if (change.IsDiscoverable is { } discoverable)
        {
            yield return new("discoverable", Said(discoverable));
        }

        if (change.Fields is { } fields)
        {
            // Every row, the unwanted ones blank: Mastodon keeps exactly the rows a save names with a label, so a
            // blank is how one is dropped — and with none named at all, how they are all dropped.
            for (var row = 0; row < FieldCount; row++)
            {
                var field = row < fields.Count ? fields[row] : new PublicProfileField("", "");

                yield return new($"fields_attributes[{row}][name]", field.Label);
                yield return new($"fields_attributes[{row}][value]", field.Value);
            }
        }
    }

    private static string Said(bool value) => value ? "true" : "false";

    /// <summary>
    ///     The body of <paramref name="response" /> where it is a success; otherwise what it was, as something a front
    ///     end can say.
    /// </summary>
    /// <exception cref="InstanceFailedException">The instance failed to answer — a <c>5xx</c>.</exception>
    /// <exception cref="PublicProfileRefusedException">Any other refusal, by way of <paramref name="refused" />.</exception>
    private static async Task<string> Answered(
        ActiveProfile profile,
        HttpResponseMessage response,
        Func<string?, PublicProfileRefusedException> refused,
        CancellationToken cancellationToken)
    {
        if ((int)response.StatusCode >= 500)
        {
            throw new InstanceFailedException(profile.Instance, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw refused(await InstanceError.Reason(response, cancellationToken));
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static WireAccount Account(string body) =>
        JsonSerializer.Deserialize<WireAccount>(body) ?? throw new JsonException("No account.");

    /// <summary>The part of the account's own description of itself that Mastonet's account does not carry.</summary>
    private sealed record CredentialsWire
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; init; }

        [JsonPropertyName("header")]
        public string? Header { get; init; }

        [JsonPropertyName("locked")]
        public bool? Locked { get; init; }

        [JsonPropertyName("bot")]
        public bool? Bot { get; init; }

        [JsonPropertyName("discoverable")]
        public bool? Discoverable { get; init; }

        [JsonPropertyName("source")]
        public SourceWire? Source { get; init; }
    }

    private sealed record SourceWire
    {
        [JsonPropertyName("note")]
        public string? Note { get; init; }

        [JsonPropertyName("fields")]
        public IReadOnlyList<FieldWire>? Fields { get; init; }

        [JsonPropertyName("discoverable")]
        public bool? Discoverable { get; init; }
    }

    private sealed record FieldWire
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }
    }
}
