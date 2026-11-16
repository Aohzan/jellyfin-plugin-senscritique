using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.SensCritique.Api;

/// <summary>
/// A SensCritique work (movie or series), as returned by its GraphQL API.
/// </summary>
/// <param name="Id">SensCritique id.</param>
/// <param name="Title">French title.</param>
/// <param name="OriginalTitle">Original title, null when it is the same as the French one.</param>
/// <param name="YearOfProduction">Production year, often null for series.</param>
/// <param name="DateRelease">Release date (yyyy-MM-dd), the French one when it exists.</param>
/// <param name="SearchYear">Year SensCritique's search index uses for the work.</param>
/// <param name="Rating">Average rating out of 10, null without ratings.</param>
/// <param name="RatingCount">Number of ratings.</param>
public sealed record SensCritiqueProduct(
    int Id,
    string? Title,
    string? OriginalTitle,
    int? YearOfProduction,
    string? DateRelease,
    int? SearchYear,
    double? Rating,
    int RatingCount)
{
    /// <summary>
    /// Gets every year SensCritique associates with the work. They often disagree by one or two
    /// (production vs. French release), so matching compares an item's year with all of them.
    /// </summary>
    public IEnumerable<int> Years
    {
        get
        {
            if (YearOfProduction is int production)
            {
                yield return production;
            }

            if (SearchYear is int search)
            {
                yield return search;
            }

            if (DateRelease is { Length: >= 4 }
                && int.TryParse(DateRelease.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var release))
            {
                yield return release;
            }
        }
    }
}
