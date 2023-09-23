﻿using System.Net;
using System.Threading.RateLimiting;
using Squidlr.Api;

namespace Microsoft.AspNetCore.Builder;

public static class RateLimiterServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = (ctx, ct) =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Client '{RemoteIpAddress}' has reached the rate limit", ctx.HttpContext.Connection.RemoteIpAddress);
                return ValueTask.CompletedTask;
            };

            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Host.ToString(), partition =>
                        new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 60,
                            Window = TimeSpan.FromSeconds(30)
                        })),
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Host.ToString(), partition =>
                        new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 6000,
                            Window = TimeSpan.FromHours(1)
                        })));

            options.AddPolicy("Video", ctx =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Host.ToString(), partition =>
                    new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 3,
                        Window = TimeSpan.FromSeconds(30)
                    });
            });
        });

        return services;
    }
}
