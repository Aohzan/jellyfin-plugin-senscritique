using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.SensCritique.Api;

namespace Jellyfin.Plugin.SensCritique.Matching;

/// <summary>
/// Picks the SensCritique work matching a library item among search results.
/// </summary>
public static class TitleMatcher
{
    // SensCritique years are the production year or the French release, often one apart.
    private const int MovieYearTolerance = 1;

    // For series SensCritique often only knows the French release of the first season.
    private const int SeriesYearTolerance = 3;

    /// <summary>
    /// Finds the candidate whose French or original title equals one of the item's titles, once
    /// accents, case and punctuation are ignored, and whose year is close enough. Among several,
    /// the closest year wins, then the most rated work.
    /// </summary>
    /// <param name="candidates">Search results.</param>
    /// <param name="titles">The item's titles (name, original title).</param>
    /// <param name="year">The item's production year.</param>
    /// <param name="isSeries">Whether the item is a series, which loosens the year check.</param>
    /// <returns>The matching work, or null when none is close enough.</returns>
    public static SensCritiqueProduct? FindBestMatch(
        IEnumerable<SensCritiqueProduct> candidates,
        IEnumerable<string?> titles,
        int? year,
        bool isSeries)
    {
        var wanted = titles.SelectMany(TitleKeys).ToHashSet(StringComparer.Ordinal);
        var tolerance = isSeries ? SeriesYearTolerance : MovieYearTolerance;

        return candidates
            .Where(candidate => TitleKeys(candidate.Title).Concat(TitleKeys(candidate.OriginalTitle)).Any(wanted.Contains))
            .Select(candidate => (Product: candidate, Distance: YearDistance(candidate, year)))
            // Without a year to compare, only series are accepted: movie titles are reused too often.
            .Where(match => match.Distance is int distance ? distance <= tolerance : isSeries)
            .OrderBy(match => match.Distance ?? int.MaxValue)
            .ThenByDescending(match => match.Product.RatingCount)
            .Select(match => match.Product)
            .FirstOrDefault();
    }

    /// <summary>
    /// Lowercases a title and strips accents and punctuation, so that "Alien, le huitième passager"
    /// and "Alien - Le Huitième Passager" compare equal.
    /// </summary>
    /// <param name="value">Title.</param>
    /// <returns>The normalized title, empty for a null or blank one.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value.Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && builder.Length > 0)
            {
                builder.Append(' ');
            }

            pendingSpace = false;
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the normalized forms a title is compared by: as is, and without a trailing
    /// parenthesized part, which SensCritique adds to tell namesakes apart ("The Office (US)")
    /// and file names sometimes carry ("RRR (Roudram Ranam Rudhiram)"). The year check still
    /// separates the namesakes.
    /// </summary>
    private static IEnumerable<string> TitleKeys(string? title)
    {
        var normalized = Normalize(title);
        if (normalized.Length == 0)
        {
            yield break;
        }

        yield return normalized;

        var open = title!.LastIndexOf('(');
        if (open > 0 && title.TrimEnd().EndsWith(')'))
        {
            var stripped = Normalize(title[..open]);
            if (stripped.Length > 0 && stripped != normalized)
            {
                yield return stripped;
            }
        }
    }

    private static int? YearDistance(SensCritiqueProduct product, int? year)
    {
        if (year is not int itemYear)
        {
            return null;
        }

        int? best = null;
        foreach (var candidateYear in product.Years)
        {
            var distance = Math.Abs(candidateYear - itemYear);
            if (best is null || distance < best)
            {
                best = distance;
            }
        }

        return best;
    }
}
