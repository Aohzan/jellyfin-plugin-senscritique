using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SensCritique.Api;

/// <summary>
/// Runs requests one at a time with a minimum gap between them. A library scan refreshes items in
/// parallel and would otherwise burst APIs that are not meant for automated clients.
/// </summary>
internal sealed class RequestThrottle : IDisposable
{
    private readonly TimeSpan _minInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextRequest = DateTime.MinValue;

    public RequestThrottle(TimeSpan minInterval)
    {
        _minInterval = minInterval;
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = _nextRequest - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            return await request().ConfigureAwait(false);
        }
        finally
        {
            _nextRequest = DateTime.UtcNow + _minInterval;
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
