using System;

namespace LocaGuest.Emailing.Internal;

internal static class RetryPolicy
{
    // Backoff schedule by attempt number (1-based):
    // 1m, 5m, 15m, 1h, 6h, 24h (then 24h)
    internal static TimeSpan ComputeDelay(int attempt)
    {
        return attempt switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromHours(1),
            5 => TimeSpan.FromHours(6),
            _ => TimeSpan.FromHours(24),
        };
    }
}
