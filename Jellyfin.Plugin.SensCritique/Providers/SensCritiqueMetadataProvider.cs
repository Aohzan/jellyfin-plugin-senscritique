using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SensCritique.Api;
using Jellyfin.Plugin.SensCritique.Matching;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SensCritique.Providers;

/// <summary>
/// Sets the community rating and the SensCritique id of movies and series.
/// </summary>
/// <remarks>
/// A custom provider runs after the metadata downloaders have filled the item, so it searches
/// SensCritique with the identified titles and year rather than the file name of a new item, and
/// its rating replaces TMDb's on every refresh that runs the providers, whatever the refresh mode.
/// Custom providers have no per-library setting: the plugin applies to every movie and series.
/// </remarks>
public class SensCritiqueMetadataProvider : ICustomMetadataProvider<Movie>, ICustomMetadataProvider<Series>
{
    private readonly SensCritiqueClient _client;
    private readonly ILogger<SensCritiqueMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensCritiqueMetadataProvider"/> class.
    /// </summary>
    /// <param name="client">SensCritique API client.</param>
    /// <param name="logger">Logger.</param>
    public SensCritiqueMetadataProvider(SensCritiqueClient client, ILogger<SensCritiqueMetadataProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => Plugin.ProviderName;

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => FetchAsync(item, SensCritiqueUniverse.Movie, cancellationToken);

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => FetchAsync(item, SensCritiqueUniverse.TvShow, cancellationToken);

    private async Task<ItemUpdateType> FetchAsync(BaseItem item, SensCritiqueUniverse universe, CancellationToken cancellationToken)
    {
        SensCritiqueProduct? product;
        try
        {
            product = await FindProductAsync(item, universe, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The item keeps the rating of the metadata downloaders until the next refresh.
            _logger.LogWarning(ex, "SensCritique lookup failed for {Name} ({Year})", item.Name, item.ProductionYear);
            return ItemUpdateType.None;
        }

        if (product is null)
        {
            _logger.LogInformation("No SensCritique match for {Name} ({Year}), its SensCritique id can be set in the metadata editor", item.Name, item.ProductionYear);
            return ItemUpdateType.None;
        }

        var changed = false;
        var id = product.Id.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(item.GetProviderId(Plugin.ProviderName), id, StringComparison.Ordinal))
        {
            item.SetProviderId(Plugin.ProviderName, id);
            changed = true;
        }

        // A work nobody rated yet keeps the rating of the metadata downloaders.
        if (product.Rating is double rating && item.CommunityRating != (float)rating)
        {
            item.CommunityRating = (float)rating;
            changed = true;
        }

        // ItemUpdateType.None is a flag of its own (1), not 0: never combine it.
        return changed ? ItemUpdateType.MetadataDownload : ItemUpdateType.None;
    }

    private async Task<SensCritiqueProduct?> FindProductAsync(BaseItem item, SensCritiqueUniverse universe, CancellationToken cancellationToken)
    {
        // An id from an earlier match, or set by hand to fix a wrong one, wins over searching again.
        if (item.TryGetProviderId(Plugin.ProviderName, out var storedId)
            && int.TryParse(storedId, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            var stored = await _client.GetProductAsync(id, cancellationToken).ConfigureAwait(false);
            if (stored is not null)
            {
                return stored;
            }

            _logger.LogWarning("SensCritique id {Id} of {Name} does not exist, searching by title", id, item.Name);
        }

        var titles = new[] { item.OriginalTitle, item.Name };
        foreach (var title in titles.OfType<string>().Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidates = await _client.SearchAsync(title, universe, cancellationToken).ConfigureAwait(false);
            var match = TitleMatcher.FindBestMatch(candidates, titles, item.ProductionYear, universe == SensCritiqueUniverse.TvShow);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
