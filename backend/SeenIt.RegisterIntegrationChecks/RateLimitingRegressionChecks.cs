using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.Services;

internal static class RateLimitingRegressionChecks
{
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
    }

    private static HttpClient Client(RegistrationFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<string> SeedAsync(RegistrationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(); // SQLite memory only, never migrations.
        var user = new User { Name = "Local", Email = "local@example.com", PasswordHash = "synthetic" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<TokenService>().Create(user).Token;
    }

    public static async Task RunAsync()
    {
        await LoginBurstAsync();
        await SpecificWindowAsync();
        await ProfileAsync();
        await ResourceLeasesAsync();
        await CorsAsync();
        Console.WriteLine("PASS: permanent regressions (20 overlapping logins, exact global accounting, category window isolation, simultaneous profile, cancellation/error leases, CORS).");
    }

    private static async Task LoginBurstAsync()
    {
        using var hasher = new BlockingHasher();
        await using var factory = new RegistrationFactory
        {
            GlobalPerMinute = 40,
            Settings = new() { ["RateLimiting:Login:PerMinute"] = "20" },
            ExtraServices = services => { services.RemoveAll<IPasswordHasher<User>>(); services.AddSingleton<IPasswordHasher<User>>(hasher); }
        };
        using var client = Client(factory);
        await SeedAsync(factory);
        Task<HttpResponseMessage> Login() => Task.Run(() => client.PostAsJsonAsync("/api/auth/login", new { email = "local@example.com", password = "synthetic" }));
        var admitted = new[] { Login(), Login() };
        try
        {
            Check(await Task.Run(() => hasher.Entered.Wait(TimeSpan.FromSeconds(10))), "Two logins entered verification");
            var snapshot = (factory.Database.Commands, factory.Database.Saves);
            var rejected = await Task.WhenAll(Enumerable.Range(0, 18).Select(_ => Login())).WaitAsync(TimeSpan.FromSeconds(10));
            Check(rejected.All(r => r.StatusCode == HttpStatusCode.TooManyRequests && r.Headers.RetryAfter is null), "18 concurrency rejections without a queue or invented retry interval");
            Check(hasher.Verifications == 2 && snapshot == (factory.Database.Commands, factory.Database.Saves), "Rejected logins never reached DB/hasher");
        }
        finally { hasher.Release.Set(); }
        Check((await Task.WhenAll(admitted)).All(r => r.StatusCode == HttpStatusCode.OK), "Two admitted logins complete");
        // Previously 20 requests consumed 38 units. Only two reach the final global gate.
        for (var i = 0; i < 38; i++)
            Check((await client.GetAsync("/missing")).StatusCode == HttpStatusCode.NotFound, "Exactly 38 of 40 global permits remain");
        Check((await client.GetAsync("/missing")).StatusCode == HttpStatusCode.TooManyRequests, "Cannot bypass global limit");
        Check((await client.GetAsync("/health")).StatusCode == HttpStatusCode.OK, "Health exempt from global saturation");
    }

    private static async Task SpecificWindowAsync()
    {
        await using var factory = new RegistrationFactory { CheckRateLimiting = true, GlobalPerMinute = 4 };
        using var client = Client(factory);
        for (var i = 0; i < 12; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "invalid", password = "x" })
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{i}");
            var result = await client.SendAsync(request);
            Check(result.StatusCode == (i < 2 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests), "Specific window rejects independently of spoofed IP");
            if (i >= 2) Check(result.Headers.RetryAfter?.Delta == TimeSpan.FromSeconds(60), "Reliable native window metadata");
        }
        for (var i = 0; i < 2; i++)
            Check((await client.GetAsync("/missing")).StatusCode == HttpStatusCode.NotFound, "Window rejections do not spend global budget");
        Check((await client.GetAsync("/missing")).StatusCode == HttpStatusCode.TooManyRequests, "Global still enforced after category rejections");
    }

    private static async Task ProfileAsync()
    {
        var gate = new QueryGate();
        await using var factory = new RegistrationFactory
        {
            ExtraServices = services =>
            {
                services.AddDbContext<AppDbContext>(options => options.AddInterceptors(gate));
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory, LocalHttp>();
            }
        };
        using var client = Client(factory);
        var token = await SeedAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        gate.Enabled = true;
        var pending = new[] { client.GetAsync("/api/profile"), client.GetAsync("/api/profile/stats"), client.GetAsync("/api/profile/watch-time?timeZone=UTC") };
        try
        {
            await WaitUntilAsync(() => gate.Entered >= 3);
            Check((await client.GetAsync("/api/profile")).StatusCode == HttpStatusCode.TooManyRequests, "Two light reads fill only light-read capacity");
            Check((await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "x" })).StatusCode == HttpStatusCode.Unauthorized, "Login not starved by profile work");
            Check((await client.GetAsync("/health")).StatusCode == HttpStatusCode.OK, "Health independent");
        }
        finally { gate.Release.TrySetResult(); }
        Check((await Task.WhenAll(pending)).All(r => r.StatusCode == HttpStatusCode.OK), "Profile, stats and watch-time overlap successfully");
    }

    private static async Task ResourceLeasesAsync()
    {
        using var http = new LocalHttp();
        await using var factory = new RegistrationFactory
        {
            ExtraServices = services => { services.RemoveAll<IHttpClientFactory>(); services.AddSingleton<IHttpClientFactory>(http); }
        };
        using var client = Client(factory);
        http.ThrowNext = true;
        Check((await client.GetAsync("/api/series/1")).StatusCode == HttpStatusCode.ServiceUnavailable, "Synthetic catalog error returns controlled 503");
        http.Block = true;
        var cancellations = Enumerable.Range(0, 3).Select(_ => new CancellationTokenSource()).ToArray();
        // Distinct keys exercise limiter leases independently of catalog single-flight.
        var pending = cancellations.Select((cts, i) => client.GetAsync($"/api/series/{i+2}", cts.Token)).ToArray();
        await WaitUntilAsync(() => http.Calls == 4);
        Check((await client.GetAsync("/api/series/1")).StatusCode == HttpStatusCode.TooManyRequests, "Catalog category saturated without a queue");
        foreach (var cts in cancellations) cts.Cancel();
        foreach (var task in pending)
        {
            try { await task; throw new InvalidOperationException("Cancellation expected"); }
            catch (OperationCanceledException) { }
        }
        var recovered = Enumerable.Range(5, 3).Select(id => client.GetAsync($"/api/series/{id}")).ToArray();
        try { await WaitUntilAsync(() => http.Calls == 7); }
        finally { http.Release.TrySetResult(); }
        Check((await Task.WhenAll(recovered)).All(r => r.StatusCode == HttpStatusCode.OK), "All three permits recovered after exception and cancellation");
        Check((await client.GetAsync("/api/series/1")).StatusCode == HttpStatusCode.OK, "Permits recovered after success");
        foreach (var cts in cancellations) cts.Dispose();
    }

    private static async Task CorsAsync()
    {
        const string origin = "https://seenit-web-dusky.vercel.app";
        await using var factory = new RegistrationFactory { CheckRateLimiting = true, Settings = new() { ["Cors:AllowedOrigins:0"] = origin } };
        using var client = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);
        var response = await client.SendAsync(request);
        Check(response.Headers.GetValues("Access-Control-Allow-Origin").Single() == origin, "Production origin preserved explicitly");
        Check(response.Headers.GetValues("Access-Control-Expose-Headers").Single() == "Retry-After", "Retry-After exposed");
        for (var i = 0; i < 3; i++)
        {
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email = "invalid", password = "x" }) };
            login.Headers.Add("Origin", origin);
            var result = await client.SendAsync(login);
            if (i == 2)
            {
                Check(result.StatusCode == HttpStatusCode.TooManyRequests && result.Headers.RetryAfter?.Delta == TimeSpan.FromSeconds(60), "429 carries Retry-After");
                Check(result.Headers.GetValues("Access-Control-Expose-Headers").Single() == "Retry-After" && result.Headers.GetValues("Access-Control-Allow-Origin").Single() == origin, "429 exposes Retry-After to authorized origin");
            }
        }
        using var untrusted = new HttpRequestMessage(HttpMethod.Get, "/health");
        untrusted.Headers.Add("Origin", "https://untrusted.invalid");
        Check(!(await client.SendAsync(untrusted)).Headers.Contains("Access-Control-Allow-Origin"), "No wildcard or additional origins");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }

    private sealed class BlockingHasher : IPasswordHasher<User>, IDisposable
    {
        public CountdownEvent Entered { get; } = new(2);
        public ManualResetEventSlim Release { get; } = new();
        private int verifications;
        public int Verifications => Volatile.Read(ref verifications);
        public string HashPassword(User user, string password) => throw new InvalidOperationException("Unexpected hash");
        public PasswordVerificationResult VerifyHashedPassword(User user, string hash, string password)
        {
            Interlocked.Increment(ref verifications);
            Entered.Signal();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Local verification gate");
            return PasswordVerificationResult.Success;
        }
        public void Dispose() { Release.Dispose(); Entered.Dispose(); }
    }

    private sealed class QueryGate : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int entered;
        public int Entered => Volatile.Read(ref entered);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled) { Interlocked.Increment(ref entered); await Release.Task.WaitAsync(cancellationToken); }
            return result;
        }
    }

    private sealed class LocalHttp : HttpMessageHandler, IHttpClientFactory
    {
        public bool Block { get; set; }
        public bool ThrowNext { get; set; }
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("https://isolated.invalid/") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            if (Block) await Release.Task.WaitAsync(cancellationToken);
            if (ThrowNext) { ThrowNext = false; throw new HttpRequestException("Synthetic local failure"); }
            var id = int.Parse(request.RequestUri!.Segments.Last());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { id, name="Local", genres=Array.Empty<string>() }), System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
