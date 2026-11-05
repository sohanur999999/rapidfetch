using System.Diagnostics;

namespace RapidFetch.Core.Engine;

/// <summary>
/// Shared token bucket. Callers await <see cref="AcquireAsync"/> before reading a chunk,
/// which keeps the aggregate transfer rate at or below the configured ceiling.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _tokens;
    private double _rate;
    private long _lastTickMs;

    public RateLimiter(long bytesPerSecond)
    {
        _rate = Math.Max(0, bytesPerSecond);
        _tokens = _rate;
        _lastTickMs = 0;
    }

    public bool Enabled => Volatile.Read(ref _rate) > 0;

    public long BytesPerSecond => (long)Volatile.Read(ref _rate);

    public void Update(long bytesPerSecond)
    {
        lock (_gate)
        {
            Refill();
            _rate = Math.Max(0, bytesPerSecond);
            if (_tokens > _rate) _tokens = _rate;
        }
    }

    private void Refill()
    {
        long now = _clock.ElapsedMilliseconds;
        double elapsed = (now - _lastTickMs) / 1000.0;
        _lastTickMs = now;
        if (elapsed <= 0) return;
        _tokens = Math.Min(_rate, _tokens + elapsed * _rate);
    }

    public async Task AcquireAsync(int bytes, CancellationToken token)
    {
        if (!Enabled || bytes <= 0) return;

        while (true)
        {
            double waitSeconds;
            lock (_gate)
            {
                Refill();
                if (_tokens >= bytes)
                {
                    _tokens -= bytes;
                    return;
                }
                waitSeconds = (_rate > 0) ? (bytes - _tokens) / _rate : 0.05;
            }

            var delay = TimeSpan.FromSeconds(Math.Clamp(waitSeconds, 0.005, 0.25));
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }
}
