using Wooly.Tui.Rendering;

namespace Wooly.Tui.Views;

/// <summary>One thing a <see cref="HangingList" /> has on offer, as what it is offered for has made it (#576).</summary>
/// <param name="Row">What it says on its row, given whether it is the picked one.</param>
/// <param name="Columns">How many columns what it says takes, which is what the list is sized to.</param>
/// <param name="Writes">What picking it writes in place of the word, asked only when it is picked.</param>
internal sealed record HangingOffer(Func<bool, IEnumerable<Span>> Row, int Columns, Func<string> Writes);
