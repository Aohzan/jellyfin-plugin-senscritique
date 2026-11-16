using System;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.SensCritique;

/// <summary>
/// Replaces the community rating of movies and series with the SensCritique one.
/// </summary>
public class Plugin : BasePlugin
{
    /// <summary>
    /// Name of the metadata provider, also used as the provider id key stored on items.
    /// </summary>
    public const string ProviderName = "SensCritique";

    /// <inheritdoc />
    public override string Name => ProviderName;

    /// <inheritdoc />
    public override string Description => "Community ratings from SensCritique for movies and series.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a6bebeff-c0d4-4549-b068-d6038ae7b53f");
}
