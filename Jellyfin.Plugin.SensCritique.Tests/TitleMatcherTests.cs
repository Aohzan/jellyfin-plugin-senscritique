using Jellyfin.Plugin.SensCritique.Api;
using Jellyfin.Plugin.SensCritique.Matching;
using Xunit;

namespace Jellyfin.Plugin.SensCritique.Tests;

public class TitleMatcherTests
{
    // Search results for "Dune" (movie universe), trimmed.
    private static readonly SensCritiqueProduct[] _dune =
    [
        new(24698928, "Dune", null, 2021, "2021-10-01", 2021, 7.3, 56322),
        new(434214, "Dune", null, 1985, "1984-12-14", 1984, 6.0, 24355),
        new(45424060, "Dune - Deuxième partie", "Dune: Part Two", 2024, "2024-03-01", 2024, 7.6, 37545),
    ];

    [Theory]
    [InlineData("Alien, le huitième passager", "alien le huitieme passager")]
    [InlineData("Alien - Le Huitième Passager", "alien le huitieme passager")]
    [InlineData("2001 : L'Odyssée de l'espace", "2001 l odyssee de l espace")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Normalize_IgnoresCaseAccentsAndPunctuation(string? title, string expected)
    {
        Assert.Equal(expected, TitleMatcher.Normalize(title));
    }

    [Theory]
    [InlineData(2021, 24698928)]
    [InlineData(1984, 434214)]
    [InlineData(1985, 434214)]
    public void FindBestMatch_UsesYearToPickAmongSameTitles(int year, int expectedId)
    {
        Assert.Equal(expectedId, TitleMatcher.FindBestMatch(_dune, ["Dune"], year, isSeries: false)?.Id);
    }

    [Fact]
    public void FindBestMatch_MatchesOriginalTitle()
    {
        var match = TitleMatcher.FindBestMatch(_dune, ["Dune : Deuxième Partie", "Dune: Part Two"], 2024, isSeries: false);

        Assert.Equal(45424060, match?.Id);
    }

    [Fact]
    public void FindBestMatch_RejectsMovieTooFarInTime()
    {
        Assert.Null(TitleMatcher.FindBestMatch(_dune, ["Dune"], 2000, isSeries: false));
    }

    [Fact]
    public void FindBestMatch_RejectsMovieWithoutYear()
    {
        Assert.Null(TitleMatcher.FindBestMatch(_dune, ["Dune"], null, isSeries: false));
    }

    [Fact]
    public void FindBestMatch_RejectsDifferentTitle()
    {
        Assert.Null(TitleMatcher.FindBestMatch(_dune, ["Dune Messiah"], 2026, isSeries: false));
    }

    [Fact]
    public void FindBestMatch_SeriesToleratesLateFrenchRelease()
    {
        // SensCritique only knows the French release (2015) of a series first aired in 2013.
        SensCritiqueProduct[] candidates =
        [
            new(7936810, "Peaky Blinders", null, null, "2015-03-12", 2015, 7.9, 44158),
            new(45446717, "Peaky Blinders - L’Immortel", "Peaky Blinders: The Immortal Man", 2026, null, 2026, 5.4, 3112),
        ];

        Assert.Equal(7936810, TitleMatcher.FindBestMatch(candidates, ["Peaky Blinders"], 2013, isSeries: true)?.Id);
    }

    [Fact]
    public void FindBestMatch_IgnoresDisambiguationSuffixAndKeepsYearCheck()
    {
        SensCritiqueProduct[] candidates =
        [
            new(42234, "The Office (US)", null, 2005, "2005-03-24", 2005, 8.0, 28476),
            new(114319, "The Office (UK)", null, 2001, "2001-07-01", 2001, 8.1, 4210),
        ];

        Assert.Equal(42234, TitleMatcher.FindBestMatch(candidates, ["The Office"], 2005, isSeries: true)?.Id);
        Assert.Equal(114319, TitleMatcher.FindBestMatch(candidates, ["The Office"], 2001, isSeries: true)?.Id);
    }

    [Fact]
    public void FindBestMatch_IgnoresParenthesizedPartOfItemTitle()
    {
        SensCritiqueProduct[] candidates = [new(44729546, "RRR", "Ratham Ranam Roudhiram", 2022, "2022-03-25", 2022, 7.2, 4000)];

        Assert.Equal(44729546, TitleMatcher.FindBestMatch(candidates, ["RRR (Roudram Ranam Rudhiram )"], 2022, isSeries: false)?.Id);
    }

    [Fact]
    public void FindBestMatch_SeriesWithoutAnyYearIsAccepted()
    {
        SensCritiqueProduct[] candidates = [new(271673, "Rome", null, null, null, null, 7.8, 18943)];

        Assert.Equal(271673, TitleMatcher.FindBestMatch(candidates, ["Rome"], 2005, isSeries: true)?.Id);
    }

    [Fact]
    public void FindBestMatch_PrefersMostRatedAtSameDistance()
    {
        SensCritiqueProduct[] candidates =
        [
            new(1, "Avatar", null, 2009, null, null, 5.0, 12),
            new(2, "Avatar", null, 2009, null, null, 6.7, 90000),
        ];

        Assert.Equal(2, TitleMatcher.FindBestMatch(candidates, ["Avatar"], 2009, isSeries: false)?.Id);
    }
}
