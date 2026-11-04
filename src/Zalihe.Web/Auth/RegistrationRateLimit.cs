using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

/// <summary>
/// Limits registration per client address. Every request can send an email to an address anyone types, and
/// too many of those would hurt the sender's reputation. Counted in memory, which is enough for one instance.
/// </summary>
public static class RegistrationRateLimit
{
    public const string Policy = "registration";
    private const string TooManyRequests = "auth.too_many_requests";

    public static IServiceCollection AddRegistrationRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var perHour = configuration.GetValue("RateLimiting:RegistrationPerHour", 30);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) =>
                new ValueTask(ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, TooManyRequests, ct));
            // Requests over a network always have an address; only in-process test clients don't.
            options.AddPolicy(Policy, httpContext => httpContext.Connection.RemoteIpAddress is { } address
                ? RateLimitPartition.GetFixedWindowLimiter(address.ToString(),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1) })
                : RateLimitPartition.GetNoLimiter("no-address"));
        });
    }
}
