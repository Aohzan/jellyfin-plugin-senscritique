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
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SensCritique.Providers;

/// <summary>
/// Remote metadata provider that only returns the community rating and the SensCritique id.
/// Jellyfin keeps, for each field, the value of the first provider that returns one: placed first
/// in a library's metadata downloaders, this provider's rating wins over TMDb's and OMDb's, and
/// they still fill every other field.
/// </summary>
public class SensCritiqueMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    private readonly SensCritiqueClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SensCritiqueMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensCritiqueMetadataProvider"/> class.
    /// </summary>
    /// <param name="client">SensCritique API client.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public SensCritiqueMetadataProvider(SensCritiqueClient client, IHttpClientFactory httpClientFactory, ILogger<SensCritiqueMetadataProvider> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => Plugin.ProviderName;

    /// <inheritdoc />
    /// <remarks>Only used by libraries without a metadata downloader order of their own.</remarks>
    public int Order => 0;

    /// <inheritdoc />
    public Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        => GetMetadataAsync<Movie>(info, SensCritiqueUniverse.Movie, cancellationToken);

    /// <inheritdoc />
    public Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        => GetMetadataAsync<Series>(info, SensCritiqueUniverse.TvShow, cancellationToken);

    /// <inheritdoc />
    /// <remarks>Identify stays with TMDb: this provider only has a rating to offer.</remarks>
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    /// <inheritdoc />
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);

    private async Task<MetadataResult<T>> GetMetadataAsync<T>(ItemLookupInfo info, SensCritiqueUniverse universe, CancellationToken cancellationToken)
        where T : BaseItem, new()
    {
        var result = new MetadataResult<T>();
        SensCritiqueProduct? product;
        try
        {
            product = await FindProductAsync(info, universe, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The other providers still run, so the item keeps TMDb's rating until the next refresh.
            _logger.LogWarning(ex, "SensCritique lookup failed for {Name} ({Year})", info.Name, info.Year);
            return result;
        }

        if (product is null)
        {
            _logger.LogInformation("No SensCritique match for {Name} ({Year}), its SensCritique id can be set in the metadata editor", info.Name, info.Year);
            return result;
        }

        result.HasMetadata = true;
        result.ResultLanguage = "fr";
        result.Item = new T
        {
            // Null for a work nobody rated yet: the next provider's rating is kept.
            CommunityRating = (float?)product.Rating,
        };
        result.Item.SetProviderId(Plugin.ProviderName, product.Id.ToString(CultureInfo.InvariantCulture));
        return result;
    }

    private async Task<SensCritiqueProduct?> FindProductAsync(ItemLookupInfo info, SensCritiqueUniverse universe, CancellationToken cancellationToken)
    {
        // An id from an earlier match, or set by hand to fix a wrong one, wins over searching again.
        if (info.TryGetProviderId(Plugin.ProviderName, out var storedId)
            && int.TryParse(storedId, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            var stored = await _client.GetProductAsync(id, cancellationToken).ConfigureAwait(false);
            if (stored is not null)
            {
                return stored;
            }

            _logger.LogWarning("SensCritique id {Id} of {Name} does not exist, searching by title", id, info.Name);
        }

        var titles = new[] { info.OriginalTitle, info.Name };
        foreach (var title in titles.OfType<string>().Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidates = await _client.SearchAsync(title, universe, cancellationToken).ConfigureAwait(false);
            var match = TitleMatcher.FindBestMatch(candidates, titles, info.Year, universe == SensCritiqueUniverse.TvShow);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
