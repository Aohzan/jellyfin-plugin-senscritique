using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.SensCritique.Providers;

/// <summary>
/// Shows the SensCritique id in the metadata editor, where a wrong or missing match can be fixed by hand.
/// </summary>
public class SensCritiqueExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => Plugin.ProviderName;

    /// <inheritdoc />
    public string Key => Plugin.ProviderName;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Movie or Series;
}
