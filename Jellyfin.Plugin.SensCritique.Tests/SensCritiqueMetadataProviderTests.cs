using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SensCritique.Api;
using Jellyfin.Plugin.SensCritique.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SensCritique.Tests;

public sealed class SensCritiqueMetadataProviderTests : IDisposable
{
    private const string DuneSearch =
        """
        {"data":{"results":{"hits":{"items":[
          {"product":{"id":24698928,"title":"Dune","originalTitle":null,"yearOfProduction":2021,"dateRelease":"2021-10-01","rating":7.3,"stats":{"ratingCount":56322}},"fields":{"year":2021}},
          {"product":{"id":434214,"title":"Dune","originalTitle":null,"yearOfProduction":1985,"dateRelease":"1984-12-14","rating":6,"stats":{"ratingCount":24355}},"fields":{"year":1984}}
        ]}}}}
        """;

    private const string GodfatherProduct =
        """
        {"data":{"product":{"id":408443,"title":"Le Parrain","originalTitle":"The Godfather","yearOfProduction":1972,"dateRelease":"1972-03-24","rating":8.4,"stats":{"ratingCount":77155}}}}
        """;

    private readonly FakeHandler _handler = new();
    private readonly SensCritiqueClient _client;
    private readonly SensCritiqueMetadataProvider _provider;

    public SensCritiqueMetadataProviderTests()
    {
        _client = new SensCritiqueClient(new FakeHttpClientFactory(_handler));
        _provider = new SensCritiqueMetadataProvider(_client, NullLogger<SensCritiqueMetadataProvider>.Instance);
    }

    [Fact]
    public async Task FetchAsync_SetsRatingAndIdOfTheMatch()
    {
        _handler.Respond = _ => DuneSearch;
        var movie = new Movie { Name = "Dune", ProductionYear = 2021, CommunityRating = 7.8f };

        var updateType = await FetchAsync(movie);

        Assert.Equal(ItemUpdateType.MetadataDownload, updateType);
        Assert.Equal(7.3f, movie.CommunityRating);
        Assert.Equal("24698928", movie.GetProviderId(Plugin.ProviderName));
    }

    [Fact]
    public async Task FetchAsync_LooksUpStoredIdWithoutSearching()
    {
        _handler.Respond = _ => GodfatherProduct;
        var movie = new Movie { Name = "Le Parrain", ProductionYear = 1972, CommunityRating = 8.7f };
        movie.SetProviderId(Plugin.ProviderName, "408443");

        await FetchAsync(movie);

        Assert.Equal(8.4f, movie.CommunityRating);
        Assert.All(_handler.Queries, query => Assert.Contains("query Product", query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchAsync_SearchesAgainWhenStoredIdIsGone()
    {
        _handler.Respond = body => body.Contains("query Product", StringComparison.Ordinal) ? """{"data":{"product":null}}""" : DuneSearch;
        var movie = new Movie { Name = "Dune", ProductionYear = 2021, CommunityRating = 7.8f };
        movie.SetProviderId(Plugin.ProviderName, "1");

        await FetchAsync(movie);

        Assert.Equal("24698928", movie.GetProviderId(Plugin.ProviderName));
        Assert.Equal(7.3f, movie.CommunityRating);
    }

    [Fact]
    public async Task FetchAsync_KeepsItemWithoutMatch()
    {
        _handler.Respond = _ => DuneSearch;
        var movie = new Movie { Name = "Dune", ProductionYear = 2000, CommunityRating = 7.8f };

        var updateType = await FetchAsync(movie);

        Assert.Equal(ItemUpdateType.None, updateType);
        Assert.Equal(7.8f, movie.CommunityRating);
        Assert.Null(movie.GetProviderId(Plugin.ProviderName));
    }

    [Fact]
    public async Task FetchAsync_KeepsRatingOfUnratedWork()
    {
        _handler.Respond = _ => DuneSearch.Replace("\"rating\":7.3", "\"rating\":null", StringComparison.Ordinal);
        var movie = new Movie { Name = "Dune", ProductionYear = 2021, CommunityRating = 7.8f };

        await FetchAsync(movie);

        Assert.Equal(7.8f, movie.CommunityRating);
        Assert.Equal("24698928", movie.GetProviderId(Plugin.ProviderName));
    }

    [Fact]
    public async Task FetchAsync_KeepsItemWhenApiFails()
    {
        _handler.Status = HttpStatusCode.InternalServerError;
        var movie = new Movie { Name = "Dune", ProductionYear = 2021, CommunityRating = 7.8f };

        var updateType = await FetchAsync(movie);

        Assert.Equal(ItemUpdateType.None, updateType);
        Assert.Equal(7.8f, movie.CommunityRating);
    }

    [Fact]
    public async Task FetchAsync_ReportsNoChangeWhenUpToDate()
    {
        _handler.Respond = _ => GodfatherProduct;
        var movie = new Movie { Name = "Le Parrain", ProductionYear = 1972, CommunityRating = 8.4f };
        movie.SetProviderId(Plugin.ProviderName, "408443");

        Assert.Equal(ItemUpdateType.None, await FetchAsync(movie));
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }

    private Task<ItemUpdateType> FetchAsync(Movie movie)
        => _provider.FetchAsync(movie, new MetadataRefreshOptions(Stub<IDirectoryService>.Create()), CancellationToken.None);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<string, string> Respond { get; set; } = _ => "{}";

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public List<string> Queries { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Queries.Add(body);
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Status == HttpStatusCode.OK ? Respond(body) : string.Empty, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
