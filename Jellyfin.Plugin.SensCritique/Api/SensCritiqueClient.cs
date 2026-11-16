using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.SensCritique.Api;

/// <summary>
/// Client for the GraphQL API behind senscritique.com. It is undocumented: the queries below are
/// the ones the website and other clients (SearXNG, Letterboxd-Extras) send.
/// </summary>
public sealed class SensCritiqueClient : IDisposable
{
    private const string Endpoint = "https://apollo.senscritique.com/";
    private const string ProductFields = "id title originalTitle yearOfProduction dateRelease rating stats { ratingCount }";
    private const int SearchPageSize = 10;

    private const string SearchQuery =
        "query Search($query: String, $filters: [SKFiltersSet], $page: SKPageInput) {"
        + " results(query: $query, filters: $filters) { hits(page: $page) { items { ... on ResultHit {"
        + " product { " + ProductFields + " } fields { year } } } } } }";

    private const string ProductQuery = "query Product($id: Int!) { product(id: $id) { " + ProductFields + " } }";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RequestThrottle _throttle = new(TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// Initializes a new instance of the <see cref="SensCritiqueClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public SensCritiqueClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Searches works of one universe by title.
    /// </summary>
    /// <param name="text">Title to search.</param>
    /// <param name="universe">Movies or series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first results, most relevant first.</returns>
    public async Task<IReadOnlyList<SensCritiqueProduct>> SearchAsync(string text, SensCritiqueUniverse universe, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object>
        {
            ["query"] = text,
            ["filters"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["identifier"] = "universe",
                    ["value"] = universe == SensCritiqueUniverse.Movie ? "movie" : "tvShow",
                },
            },
            ["page"] = new Dictionary<string, int> { ["from"] = 0, ["size"] = SearchPageSize },
        };

        using var document = await PostAsync(SearchQuery, variables, cancellationToken).ConfigureAwait(false);
        var items = document.RootElement.GetProperty("data").GetProperty("results").GetProperty("hits").GetProperty("items");
        var products = new List<SensCritiqueProduct>();
        foreach (var hit in items.EnumerateArray())
        {
            if (hit.TryGetProperty("product", out var product) && product.ValueKind == JsonValueKind.Object)
            {
                var searchYear = hit.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object
                    ? GetInt(fields, "year")
                    : null;
                products.Add(ParseProduct(product, searchYear));
            }
        }

        return products;
    }

    /// <summary>
    /// Gets a work by its SensCritique id.
    /// </summary>
    /// <param name="id">SensCritique id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work, or null when the id does not exist.</returns>
    public async Task<SensCritiqueProduct?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        using var document = await PostAsync(ProductQuery, new Dictionary<string, object> { ["id"] = id }, cancellationToken).ConfigureAwait(false);
        var product = document.RootElement.GetProperty("data").GetProperty("product");
        return product.ValueKind == JsonValueKind.Object ? ParseProduct(product, null) : null;
    }

    /// <inheritdoc />
    public void Dispose() => _throttle.Dispose();

    private Task<JsonDocument> PostAsync(string query, Dictionary<string, object> variables, CancellationToken cancellationToken)
    {
        return _throttle.RunAsync(
            async () =>
            {
                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                using var response = await client.PostAsJsonAsync(Endpoint, new { query, variables }, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (document.RootElement.TryGetProperty("errors", out var errors))
                    {
                        var message = errors.GetRawText();
                        document.Dispose();
                        throw new HttpRequestException("SensCritique API error: " + message);
                    }

                    return document;
                }
            },
            cancellationToken);
    }

    private static SensCritiqueProduct ParseProduct(JsonElement product, int? searchYear)
    {
        var ratingCount = product.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object
            ? GetInt(stats, "ratingCount") ?? 0
            : 0;

        return new SensCritiqueProduct(
            product.GetProperty("id").GetInt32(),
            GetString(product, "title"),
            GetString(product, "originalTitle"),
            GetInt(product, "yearOfProduction"),
            GetString(product, "dateRelease"),
            searchYear,
            product.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Number ? rating.GetDouble() : null,
            ratingCount);
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
}
