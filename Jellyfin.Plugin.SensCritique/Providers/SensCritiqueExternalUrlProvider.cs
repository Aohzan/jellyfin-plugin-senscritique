using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SensCritique.Providers;

/// <summary>
/// Adds a SensCritique link to movies and series that have a SensCritique id.
/// </summary>
public class SensCritiqueExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => Plugin.ProviderName;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is not (Movie or Series) || !item.TryGetProviderId(Plugin.ProviderName, out var id))
        {
            yield break;
        }

        // SensCritique redirects /film/-/<id> to the canonical URL with the title slug.
        yield return string.Format(CultureInfo.InvariantCulture, "https://www.senscritique.com/{0}/-/{1}", item is Series ? "serie" : "film", id);
    }
}
