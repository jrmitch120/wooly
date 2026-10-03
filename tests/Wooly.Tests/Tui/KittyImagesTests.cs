using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Kitty graphics protocol's sequences, as <see cref="KittyImages" /> writes them (ADR-0022). Whether a real
///     terminal draws what they say is the manual smoke test; that they say it is here.
/// </summary>
public class KittyImagesTests
{
    private const string Esc = "\u001b";

    /// <summary>
    ///     A PNG goes in 4096-character chunks of base64, each saying whether more follows, and then a virtual placement
    ///     over the box's cells — every command quiet, so the terminal answers none of them.
    /// </summary>
    [Fact]
    public void Transmit_SendsThePngInChunksAndThenAVirtualPlacement()
    {
        var written = new List<string>();
        var png = new byte[4000];

        new KittyImages(written.Add).Transmit(7, png, columns: 12, rows: 5);

        var commands = Assert.Single(written).Split(Esc + "\\", StringSplitOptions.RemoveEmptyEntries);
        var payload = Convert.ToBase64String(png);

        Assert.Equal(3, commands.Length);
        Assert.Equal($"{Esc}_Ga=t,f=100,t=d,i=7,q=2,m=1;{payload[..4096]}", commands[0]);
        Assert.Equal($"{Esc}_Gm=0;{payload[4096..]}", commands[1]);
        Assert.Equal($"{Esc}_Ga=p,U=1,i=7,c=12,r=5,q=2", commands[2]);
    }

    [Fact]
    public void Forget_FreesOneImageAndForgetAllEveryOne()
    {
        var written = new List<string>();
        var images = new KittyImages(written.Add);

        images.Forget(7);
        images.ForgetAll();

        Assert.Equal([$"{Esc}_Ga=d,d=I,i=7,q=2{Esc}\\", $"{Esc}_Ga=d,d=A,q=2{Esc}\\"], written);
    }
}
