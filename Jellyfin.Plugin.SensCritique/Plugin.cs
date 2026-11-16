using System;
using Jellyfin.Plugin.SensCritique.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SensCritique;

/// <summary>
/// Replaces the community rating of movies and series with the SensCritique one.
/// </summary>
/// <remarks>
/// Derives from <see cref="BasePlugin{TConfigurationType}"/> although the plugin has no settings:
/// only its constructor sets <see cref="BasePlugin.Version"/>, which Jellyfin reads right after
/// creating the plugin and fails on when null.
/// </remarks>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Name of the metadata provider, also used as the provider id key stored on items.
    /// </summary>
    public const string ProviderName = "SensCritique";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => ProviderName;

    /// <inheritdoc />
    public override string Description => "Community ratings from SensCritique for movies and series. Not affiliated with SensCritique.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a6bebeff-c0d4-4549-b068-d6038ae7b53f");
}
