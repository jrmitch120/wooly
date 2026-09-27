using Wooly.Core.Profiles;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Every profile on this machine, a row each, marked with the one this session is <b>acting as</b> and the one
///     that is the <b>default</b> — two different things, which is why both are drawn (CONTEXT.md, ADR-0020). What
///     <c>ctrl-p</c> opens, on the stack rather than on the rail: it lists what is on this machine and fetches nothing.
/// </summary>
/// <remarks>
///     Each marker is a word rather than a colour, so the screen reads the same with none (ADR-0014).
/// </remarks>
/// <param name="profiles">The profiles set up on this machine, in the order the registry lists them.</param>
/// <param name="actingAs">The name of the profile this session is acting as.</param>
/// <param name="warning">
///     What ADR-0003 owes wherever tokens are kept in the clear, or <see langword="null" /> where they are not.
/// </param>
public sealed class ProfilesScreen(IReadOnlyList<ProfileSummary> profiles, string actingAs, string? warning)
    : Screen
{
    /// <summary>The marker on the profile this session is acting as.</summary>
    public const string ActingAs = "acting as";

    /// <summary>
    ///     The marker on the default profile, which commands act as unless told otherwise. The config file calls it
    ///     <c>current_profile</c>; on screen it is the default, which is what it does (CONTEXT.md).
    /// </summary>
    public const string Default = "default";

    private readonly Picked<ProfileSummary> _profiles = new(profiles);

    /// <inheritdoc />
    public override string Crumb => "Profiles";

    /// <inheritdoc />
    /// <remarks>
    ///     <c>⏎</c> only where the picked profile is not the one already acted as, since there it would do nothing
    ///     (#243).
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("j/k", "profile"),
        .. ToActAs is null ? Array.Empty<KeyHint>() : [new KeyHint("⏎", "act as", NeedsAPick: true)],
        new("a", "add"),
        .. PostKeys.Leaving(new KeyHint("esc", "back")),
    ];

    /// <summary>The profile picked out, or <see langword="null" /> where none is set up.</summary>
    public ProfileSummary? PickedProfile => _profiles.Out;

    /// <summary>
    ///     The name of the profile <c>⏎</c> would act as: the one picked out, unless it is the one already acted as —
    ///     or <see langword="null" /> where there is nothing for <c>⏎</c> to do.
    /// </summary>
    public string? ToActAs => _profiles.Out is { } picked && picked.Name != actingAs ? picked.Name : null;

    /// <inheritdoc />
    protected override IPicked Walking => _profiles;

    /// <summary>
    ///     Picks out the profile named <paramref name="name" />, or leaves the pick where it is if none is.
    /// </summary>
    public void Pick(string name)
    {
        var at = _profiles.All.ToList().FindIndex(profile => profile.Name == name);

        if (at >= 0)
        {
            Pick(at);
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>();

        // Aligned with the rows under it, which start past the one column the gutter takes.
        var room = Stamped.Room(drawing.Width);

        if (warning is { } said)
        {
            lines.AddRange(TextWrap.Wrap(said, room).Select(row => Line.Of($" {row}", Role.ContentWarning)));
            lines.Add(Line.Blank);
        }

        if (_profiles.Count == 0)
        {
            lines.Add(Line.Of(TextWrap.Clip(" No profiles have been set up yet.", drawing.Width), Role.Muted));

            return lines;
        }

        lines.AddRange(_profiles.Rows(drawing.Width, (profile, _, space) => Block(profile, space), Line.Blank));

        return lines;
    }

    /// <summary>
    ///     One profile: its name and whichever markers it carries on the first row, and who it signs in as on the
    ///     second — the full address, since the handle alone is two people on two instances, or the instance alone
    ///     where no account was ever established.
    /// </summary>
    private IReadOnlyList<Line> Block(ProfileSummary profile, int room)
    {
        string[] marks =
        [
            .. profile.Name == actingAs ? [ActingAs] : Array.Empty<string>(),
            .. profile.IsCurrent ? [Default] : Array.Empty<string>(),
        ];

        var marked = marks.Length == 0 ? string.Empty : $"  {string.Join(" · ", marks)}";

        // The markers are what the row is read for, so the name gives way to them rather than the other way round.
        var name = TextWrap.Clip(profile.Name, Math.Max(0, room - Glyphs.Columns(marked)));

        return
        [
            Line.Of(new Span(name, Role.BylineName), new Span(marked, Role.Muted)),
            Line.Of(
                TextWrap.Clip(profile.Account is { } account ? $"@{account}" : profile.Instance, room),
                Role.BylineHandle),
        ];
    }
}
