using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.Services;

internal static class RateLimitingChecks
{
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
    }

    public static async Task RunAsync()
    {
        await using var factory = new RegistrationFactory { CheckRateLimiting = true };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        object Registration(string email) => new { name = "Local", email, password = "password" };
        for (var i = 0; i < 2; i++)
            Check((await client.PostAsJsonAsync("/api/auth/register", Registration($"local{i}@example.com"))).StatusCode == HttpStatusCode.Created, "Within registration budget");
        var snapshot = (factory.Hasher.HashCalls, factory.Database.Saves, factory.Database.Commands);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.1");
        client.DefaultRequestHeaders.Add("Forwarded", "for=198.51.100.2");
        var rejected = await client.PostAsJsonAsync("/api/auth/register", Registration("secret@example.com"));
        Check(rejected.StatusCode == HttpStatusCode.TooManyRequests, "Register 429 despite spoofed headers");
        Check(snapshot == (factory.Hasher.HashCalls, factory.Database.Saves, factory.Database.Commands), "Rejected registration performed work");
        Check(rejected.Headers.RetryAfter?.Delta is { TotalSeconds: > 0 and <= 60 }, "Fixed window Retry-After");
        Check(await rejected.Content.ReadAsStringAsync() == "{\"message\":\"Muitas requisições. Tente novamente mais tarde.\"}", "Sanitized JSON");
        for (var i = 0; i < 2; i++)
            Check((await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "x" })).StatusCode == HttpStatusCode.Unauthorized, "Login independent from register");
        Check((await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "x" })).StatusCode == HttpStatusCode.TooManyRequests, "Login exhausted");
        var loginSnapshot = (factory.Hasher.VerifyCalls, factory.Database.Commands, factory.Database.Saves);
        Check((await client.PostAsJsonAsync("/api/auth/login", new { email = "local0@example.com", password = "password" })).StatusCode == HttpStatusCode.TooManyRequests, "Valid credentials also rejected");
        Check(loginSnapshot == (factory.Hasher.VerifyCalls, factory.Database.Commands, factory.Database.Saves), "Rejected login performed work");

        var tokens = factory.Services.GetRequiredService<IServiceScopeFactory>();
        using (var scope = tokens.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
            foreach (var user in new[] { new User { PasswordHash = "unused", Name = "A", Email = "a@example.com" }, new User { PasswordHash = "unused", Name = "B", Email = "b@example.com" } })
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.Create(user).Token);
                for (var i = 0; i < 2; i++)
                    Check((await client.DeleteAsync("/api/my-series/99")).StatusCode == HttpStatusCode.NotFound, "Independent validated user budget");
                Check((await client.DeleteAsync("/api/my-series/99")).StatusCode == HttpStatusCode.TooManyRequests, "User mutation exhausted");
            }
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.signature");
        for (var i = 0; i < 2; i++)
            Check((await client.DeleteAsync("/api/my-series/99")).StatusCode == HttpStatusCode.Unauthorized, "Invalid JWT is anonymous");
        client.DefaultRequestHeaders.Authorization = null;
        Check((await client.DeleteAsync("/api/my-series/99")).StatusCode == HttpStatusCode.TooManyRequests, "Invalid JWT and anonymous share fallback");
        Check((await client.GetAsync("/health")).StatusCode == HttpStatusCode.OK, "Health survives exhausted policies");

        await using var concurrentFactory = new RegistrationFactory { CheckRateLimiting = true };
        using var concurrentClient = concurrentFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using (var scope = concurrentFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        using var gate = new ManualResetEventSlim();
        concurrentFactory.Hasher.HashGate = gate;
        var pending = Task.Run(() => concurrentClient.PostAsJsonAsync("/api/auth/register", Registration("blocked@example.com")));
        try
        {
            await concurrentFactory.Hasher.HashEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var excess = await concurrentClient.PostAsJsonAsync("/api/auth/register", Registration("excess@example.com")).WaitAsync(TimeSpan.FromSeconds(3));
            Check(excess.StatusCode == HttpStatusCode.TooManyRequests, "Concurrency rejects immediately without queue");
            Check(excess.Headers.RetryAfter is null, "Concurrency has no reliable retry interval");
            Check(concurrentFactory.Hasher.HashCalls == 1 && concurrentFactory.Database.Saves == 0, "Concurrency rejection did not hash or save");
            Check((await concurrentClient.GetAsync("/health")).StatusCode == HttpStatusCode.OK, "Health during blocked hash");
        }
        finally { gate.Set(); }
        Check((await pending).StatusCode == HttpStatusCode.Created, "Concurrency lease completes");
        Check((await concurrentClient.PostAsJsonAsync("/api/auth/register", Registration("released@example.com"))).StatusCode == HttpStatusCode.Created, "Concurrency permit released");
        await using var globalFactory = new RegistrationFactory { GlobalPerMinute = 2 };
        using var globalClient = globalFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 2; i++)
            Check((await globalClient.GetAsync("/missing")).StatusCode == HttpStatusCode.NotFound, "Global within budget");
        Check((await globalClient.GetAsync("/missing")).StatusCode == HttpStatusCode.TooManyRequests, "Global excess");
        Check((await globalClient.GetAsync("/health")).StatusCode == HttpStatusCode.OK, "Health exempt from global exhaustion");
        Console.WriteLine("PASS: rate limiting integration (budgets, independence, JWT, spoofing, side effects, JSON, retry, health, concurrency/release).");
    }
}
