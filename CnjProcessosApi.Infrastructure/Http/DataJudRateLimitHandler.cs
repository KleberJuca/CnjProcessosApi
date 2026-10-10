using System.Threading.RateLimiting;

namespace CnjProcessosApi.Infrastructure.Http;

// Registered inside retry: every attempt (including jobs) consumes the same quota.
public class DataJudRateLimitHandler(DataJudRequestLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var lease = await limiter.Limiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
            throw new CnjProcessosApi.Core.Models.DataJudException("Limite local de consultas ao DataJud atingido. Tente novamente mais tarde.", 503);
        return await base.SendAsync(request, ct);
    }
}

public sealed class DataJudRequestLimiter : IDisposable
{
    public TokenBucketRateLimiter Limiter { get; } = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriod = TimeSpan.FromMilliseconds(550),
        AutoReplenishment = true, QueueLimit = 120, QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });
    public void Dispose() => Limiter.Dispose();
}
