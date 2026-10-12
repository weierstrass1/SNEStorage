using System.Threading.RateLimiting;
using SNEStorage.Filters.Attributes;

namespace SNEStorage.Services;

public sealed class CaptchaFrequencyService : IDisposable
{
    private readonly PartitionedRateLimiter<(string UserId, CaptchaPolicy Policy)> _limiter =
        PartitionedRateLimiter.Create<
            (string UserId, CaptchaPolicy Policy),
            (string UserId, CaptchaPolicy Policy)>(key =>
            RateLimitPartition.GetSlidingWindowLimiter(key, partition =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = partition.Policy switch
                    {
                        CaptchaPolicy.After5TriesInShortTime => 5,
                        CaptchaPolicy.After10TriesInShortTime => 10,
                        _ => throw new ArgumentOutOfRangeException(nameof(partition.Policy))
                    },
                    Window = TimeSpan.FromMinutes(5),
                    SegmentsPerWindow = 10,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public bool TryAllowWithoutCaptcha(string userId, CaptchaPolicy policy)
    {
        using var lease = _limiter.AttemptAcquire((userId, policy));
        return lease.IsAcquired;
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
