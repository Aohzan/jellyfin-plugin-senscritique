using System.IO;
using System.Reflection;

namespace Jellyfin.Plugin.SensCritique.Tests;

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
