using Wooly.Core.Timelines;

namespace Wooly.Tests.Core;

/// <summary>A <b>Tag set</b> as a value: its tags made bare, said once each, and held to the cap (ADR-0033).</summary>
public class TagSetTests
{
    [Fact]
    public void Of_MakesEachTagBare_AndDropsARepeat_KeepingTheOrderWritten()
    {
        var set = TagSet.Of("dotnet", ["#dotnet", "csharp", "dotnet", "#fsharp", "csharp"]);

        Assert.Equal("dotnet", set.Name);
        Assert.Equal(["dotnet", "csharp", "fsharp"], set.Tags);
    }

    /// <summary>An instance reads a tag whatever its case, so a tag written twice in two cases is one tag.</summary>
    [Fact]
    public void Of_DropsARepeatWrittenInAnotherCase_KeepingTheFirstSpelling()
    {
        Assert.Equal(["DotNet", "csharp"], TagSet.Of("dotnet", ["DotNet", "csharp", "#dotnet"]).Tags);
    }

    [Fact]
    public void TwoSetsOfTheSameNameAndTags_AreTheSameSet()
    {
        Assert.Equal(TagSet.Of("dotnet", ["dotnet", "csharp"]), TagSet.Of("dotnet", ["#dotnet", "csharp"]));
        Assert.NotEqual(TagSet.Of("dotnet", ["dotnet", "csharp"]), TagSet.Of("dotnet", ["csharp", "dotnet"]));
        Assert.NotEqual(TagSet.Of("dotnet", ["dotnet"]), TagSet.Of("csharp", ["dotnet"]));
        Assert.Equal(
            Timeline.Of(TagSet.Of("dotnet", ["dotnet", "csharp"])),
            Timeline.Of(TagSet.Of("dotnet", ["dotnet", "csharp"])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_RefusesASetWithNoName(string name)
    {
        Assert.Throws<ArgumentException>(() => TagSet.Of(name, ["cats"]));
    }

    [Fact]
    public void Of_RefusesASetWithNoTags()
    {
        Assert.Throws<ArgumentException>(() => TagSet.Of("cats", []));
    }

    [Fact]
    public void Of_RefusesATagThatIsNotOneWord()
    {
        Assert.Throws<ArgumentException>(() => TagSet.Of("cats", ["cats", "big cats"]));
    }

    /// <summary>Twelve tags is three asks of the instance at most, which is where the set stops (ADR-0033).</summary>
    [Fact]
    public void Of_TakesTwelveTags_AndRefusesAThirteenth()
    {
        Assert.Equal(12, TagSet.Of("many", Tags(12)).Tags.Count);
        Assert.Throws<ArgumentException>(() => TagSet.Of("many", Tags(13)));
    }

    /// <summary>A repeat is dropped before the tags are counted, so writing one twice costs nothing.</summary>
    [Fact]
    public void Of_CountsTheTagsAfterTheRepeatsAreDropped()
    {
        Assert.Equal(12, TagSet.Of("many", [.. Tags(12), "#t1"]).Tags.Count);
    }

    [Fact]
    public void ATimelineOfASet_IsDescribedByTheSetsName()
    {
        Assert.Equal("the dotnet tag set", Timeline.Of(TagSet.Of("dotnet", ["dotnet"])).Description);
    }

    private static string[] Tags(int count) => [.. Enumerable.Range(1, count).Select(n => $"t{n}")];
}
