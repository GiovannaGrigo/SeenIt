using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.Services;
using SeenIt.Api.Services.Avatars;

// TokenService is only an assembly marker: WebApplicationFactory executes the API's
// actual entry point (Program.cs), including its middleware and route registration.
internal sealed class RegistrationFactory : WebApplicationFactory<TokenService>
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:");
    public DatabaseProbe Database { get; } = new();
    public ObservedHasher Hasher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // Synthetic settings override ambient configuration; no production secrets required.
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(Enumerable.Range(1, 32).Select(n => (byte)n).ToArray()));
        builder.UseSetting("Jwt:Issuer", "SeenIt.LocalChecks");
        builder.UseSetting("Jwt:Audience", "SeenIt.LocalChecks.Client");
        builder.UseSetting("Jwt:ExpirationMinutes", "120");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "unused-replaced-by-sqlite");
        builder.UseSetting("SupabaseStorage:Url", "https://localchecksonly.supabase.co");
        builder.UseSetting("SupabaseStorage:SecretKey", "sb_secret_synthetic_local_checks_only");
        builder.UseSetting("SupabaseStorage:Bucket", "avatars");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            Connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(Connection).AddInterceptors(Database));
            services.RemoveAll<IPasswordHasher<User>>();
            services.AddSingleton<IPasswordHasher<User>>(Hasher);
            services.RemoveAll<IAvatarStorageService>();
            services.AddSingleton<IAvatarStorageService, IsolatedAvatars>();
            // Fail closed if a path unexpectedly tries to access an external HTTP service.
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory, NoExternalHttp>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Connection.DisposeAsync();
    }

    private sealed class NoExternalHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("External HTTP is forbidden in registration checks.");
    }

    private sealed class IsolatedAvatars : IAvatarStorageService
    {
        public string? GetPublicUrl(Guid userId, string? path, Microsoft.AspNetCore.Http.HttpRequest request) => null;
        public Task<string> UploadAsync(Guid userId, ValidatedAvatar image, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected upload.");
        public Task DeleteAsync(Guid userId, string path, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected deletion.");
    }
}

internal sealed class ObservedHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> inner = new();
    public int HashCalls { get; private set; }
    public string HashPassword(User user, string password) { HashCalls++; return inner.HashPassword(user, password); }
    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) =>
        inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
}

internal sealed class DatabaseProbe : DbCommandInterceptor, ISaveChangesInterceptor
{
    public int Commands { get; private set; }
    public int Saves { get; private set; }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Commands++; return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Commands++; return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Commands++; return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Commands++; return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Commands++; return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Commands++; return ValueTask.FromResult(result); }
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) { Saves++; return result; }
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Saves++; return ValueTask.FromResult(result); }
}
