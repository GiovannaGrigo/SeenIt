using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SeenIt.Api.RateLimiting;

public sealed class AbuseProtectionOptions
{
    public int GlobalPerMinute { get; set; } = 240;
    public int GlobalConcurrency { get; set; } = 8;
    public PolicyBudget Login { get; set; } = new(20, 2);
    public PolicyBudget Register { get; set; } = new(5, 1);
    public PolicyBudget Avatar { get; set; } = new(4, 1);
    public PolicyBudget Mutation { get; set; } = new(30, 2);
    public PolicyBudget Expensive { get; set; } = new(20, 2);
    public PolicyBudget Read { get; set; } = new(20, 2);
    public PolicyBudget Catalog { get; set; } = new(90, 3);
}

public sealed class PolicyBudget(int perMinute = 1, int concurrency = 1)
{
    public int PerMinute { get; set; } = perMinute;
    public int Concurrency { get; set; } = concurrency;
}

public static class AbuseProtection
{
    public const string Login = "login", Register = "register", Avatar = "avatar",
        Mutation = "mutation", Expensive = "expensive", Read = "read", Catalog = "catalog";

    public static IServiceCollection AddAbuseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new AbuseProtectionOptions();
        configuration.GetSection("RateLimiting").Bind(settings);
        var budgets = new Dictionary<string, PolicyBudget>
        {
            [Login] = settings.Login, [Register] = settings.Register, [Avatar] = settings.Avatar,
            [Mutation] = settings.Mutation, [Expensive] = settings.Expensive, [Read] = settings.Read, [Catalog] = settings.Catalog
        };
        if (settings.GlobalPerMinute <= 0 || settings.GlobalConcurrency <= 0 ||
            budgets.Values.Any(b => b.PerMinute <= 0 || b.Concurrency <= 0))
            throw new InvalidOperationException("RateLimiting exige limites positivos.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                response.Headers.CacheControl = "no-store";
                await response.WriteAsJsonAsync(new { message = "Muitas requisições. Tente novamente mais tarde." }, cancellationToken);
            };
            // Keep every fallible gate in this native chain, with the server window LAST.
            // ASP.NET retries acquisition after a failed attempt, even with QueueLimit=0.
            // Earlier windows are not refunded on disposal. A category rejection must
            // therefore happen before the server window, never consume it twice.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(_ => Concurrent("server", settings.GlobalConcurrency)),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var name = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                    return name is not null && budgets.TryGetValue(name, out var budget)
                        ? Concurrent(name, budget.Concurrency)
                        : RateLimitPartition.GetNoLimiter("other");
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var name = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                    if (name is null || !budgets.TryGetValue(name, out var budget))
                        return RateLimitPartition.GetNoLimiter("other");
                    var identity = name is Login or Register or Catalog ? "public" : UserPartition(context);
                    return Fixed($"{name}:{identity}", budget.PerMinute);
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(_ => Fixed("server", settings.GlobalPerMinute)));
            // Named metadata selects the gates above. No fallible endpoint limiter may
            // run after the server window, or the double-charge bug would return.
            foreach (var name in budgets.Keys)
                options.AddPolicy(name, _ => RateLimitPartition.GetNoLimiter(name));
        });
        return services;
    }

    private static string UserPartition(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var id))
            return id.ToString("N");
        return "anonymous";
    }

    private static RateLimitPartition<string> Fixed(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        });

    private static RateLimitPartition<string> Concurrent(string key, int permits) =>
        RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = permits, QueueLimit = 0
        });
}
