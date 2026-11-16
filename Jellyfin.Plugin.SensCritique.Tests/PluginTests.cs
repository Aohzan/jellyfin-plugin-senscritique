using System;
using System.IO;
using System.Reflection;
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

    /// <summary>
    /// Interface stub returning a temporary directory for every string member and null otherwise.
    /// </summary>
    /// <typeparam name="T">Interface to stub.</typeparam>
    public class Stub<T> : DispatchProxy
        where T : class
    {
        public static T Create() => DispatchProxy.Create<T, Stub<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.ReturnType == typeof(string) ? Path.GetTempPath() : null;
    }
}
