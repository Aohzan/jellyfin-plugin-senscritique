using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Xunit;

namespace Jellyfin.Plugin.SensCritique.Tests;

public class PluginTests
{
    [Fact]
    public void Plugin_HasVersionAndIdOnceCreated()
    {
        // Jellyfin's PluginManager reads Version right after creating the plugin and disables
        // it when that throws.
        var plugin = new Plugin(Stub<IApplicationPaths>.Create(), Stub<IXmlSerializer>.Create());

        Assert.NotNull(plugin.Version);
        Assert.Equal(Guid.Parse("a6bebeff-c0d4-4549-b068-d6038ae7b53f"), plugin.Id);
        Assert.Equal("SensCritique", plugin.Name);
    }
}
