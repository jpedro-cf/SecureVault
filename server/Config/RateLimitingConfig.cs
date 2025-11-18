using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Config;

public static class RateLimitingConfig
{
    private const string PublicTokenBucketPolicy = "public_limiter";
    private const string UserTokenBucketPolicy = "user_limiter";

    public static void AddRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.AddPolicy(PublicTokenBucketPolicy, httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: "public",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 100,
                        TokensPerPeriod = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        AutoReplenishment = true
                    }));
            
            options.AddPolicy(UserTokenBucketPolicy, httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? "anonymous",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 40,
                        TokensPerPeriod = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        AutoReplenishment = true
                    }));
        });
    }
}