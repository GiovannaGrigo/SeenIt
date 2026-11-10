using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeenIt.Api.Data;
using SeenIt.Api.DTOs;

internal static class RegistrationIntegrationChecks
{
    public static async Task RunAsync()
    {
        await using var factory = new RegistrationFactory();
        // Kestrel preserves production handling of BadHttpRequestException from BindAsync.
        // TestServer instead propagates these exceptions directly to its HttpClient.
        factory.UseKestrel(0);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.BaseAddress = new Uri(factory.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Check(db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite", "Only isolated SQLite may run.");
            await db.Database.EnsureCreatedAsync(); // Empty in-memory database only; no migrations.
        }
        var count = 0;
        var successes = 0;
        (int Commands, int Saves, int Hashes) Snapshot() => (factory.Database.Commands, factory.Database.Saves, factory.Hasher.HashCalls);
        string Json(string? name = "Ana", string? email = "ana@example.com", string? password = "password") => JsonSerializer.Serialize(new { name, email, password });

        async Task Reject(string label, string json, HttpStatusCode expected = HttpStatusCode.BadRequest,
            string contentType = "application/json", bool streaming = false, string? field = null)
        {
            var before = Snapshot();
            using HttpContent content = streaming ? new FragmentedContent(Encoding.UTF8.GetBytes(json)) : new StringContent(json, Encoding.UTF8, contentType);
            if (streaming) content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var response = await client.PostAsync("/api/auth/register", content);
            Check(response.StatusCode == expected, $"{label}: expected {(int)expected}, got {(int)response.StatusCode}.");
            Check(Snapshot() == before, $"{label}: rejected request queried the database, saved or hashed.");
            if (field is not null)
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Check(body.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"{label}: missing validation error for {field}.");
            }
            count++;
        }

        async Task<string> Create(string name = "Ana", string password = "password", string? suppliedEmail = null, bool streaming = false, int padding = 0)
        {
            var email = suppliedEmail ?? $"fixture{successes}@example.com";
            var json = Json(name, email, password);
            json += new string(' ', padding == 0 ? 0 : padding - Encoding.UTF8.GetByteCount(json));
            var before = Snapshot();
            using HttpContent content = streaming ? new FragmentedContent(Encoding.UTF8.GetBytes(json)) : new StringContent(json, Encoding.UTF8, "application/json");
            if (streaming) content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await client.PostAsync("/api/auth/register", content);
            Check(response.StatusCode == HttpStatusCode.Created, $"Valid registration: expected 201, got {(int)response.StatusCode}.");
            var after = Snapshot();
            Check(after.Commands > before.Commands && after.Saves == before.Saves + 1 && after.Hashes == before.Hashes + 1,
                "Real registration must query, hash once and save once.");
            Check(response.Headers.Location?.ToString() == "/api/auth/me", "Location changed.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Check(root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "expiresAtUtc", "token", "user" }), "AuthResponse JSON changed.");
            Check(root.GetProperty("user").EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "avatarUrl", "email", "id", "name" }), "UserResponse JSON changed.");
            var auth = root.Deserialize<AuthResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(auth.Token.Split('.').Length == 3 && auth.ExpiresAtUtc > DateTime.UtcNow && auth.User.AvatarUrl is null, "Actual JWT/expiry/avatar response invalid.");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email.Trim().ToLowerInvariant());
            Check(stored.Name == name.Trim() && auth.User.Name == stored.Name && auth.User.Email == stored.Email && auth.User.Id == stored.Id, "Normalization/persisted response changed.");
            Check(factory.Hasher.VerifyHashedPassword(stored, stored.PasswordHash, password) != PasswordVerificationResult.Failed,
                "Exact supplied password must verify against the stored ASP.NET Core hash.");
            if (password != password.Trim())
                Check(factory.Hasher.VerifyHashedPassword(stored, stored.PasswordHash, password.Trim()) == PasswordVerificationResult.Failed, "Password was silently trimmed.");
            successes++;
            count++;
            return email.Trim().ToLowerInvariant();
        }

        foreach (var field in new[] { "name", "email", "password" })
        {
            var values = new Dictionary<string, object?> { ["name"] = "Ana", ["email"] = "ana@example.com", ["password"] = "password" };
            values.Remove(field);
            await Reject($"missing {field}", JsonSerializer.Serialize(values), field: field);
            foreach (var invalid in new object?[] { null, "", "   ", 123, true, new[] { "text" }, new { text = "value" } })
            {
                values[field] = invalid;
                await Reject($"invalid {field}", JsonSerializer.Serialize(values), field: invalid is null or string ? field : null);
            }
        }
        foreach (var name in new[] { "Ana\0Silva", "\0Ana", "Ana\0" })
            await Reject("NUL name", Json(name: name), field: "name");
        foreach (var email in new[] { "ana\0@example.com", "\0ana@example.com", "ana@example.com\0" })
            await Reject("NUL email", Json(email: email), field: "email");
        foreach (var email in new[] { "invalid", "a@", "Ana <ana@example.com>", "a@example.com (Ana)", "\"ana\"@example.com", "a@@example.com", "a@example..com", "a@example.com\r\nBcc: b@example.com" })
            await Reject("malformed email", Json(email: email), field: "email");
        await Reject("name 121", Json(name: new string('a', 121)), field: "name");
        var email200 = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + ".example";
        await Reject("email 201", Json(email: email200 + "x"), field: "email");
        await Reject("password 7", Json(password: new string('p', 7)), field: "password");
        await Reject("password 129", Json(password: new string('p', 129)), field: "password");
        await Reject("password whitespace 8", Json(password: new string(' ', 8)), field: "password");
        await Reject("password whitespace 128", Json(password: new string(' ', 128)), field: "password");
        foreach (var json in new[] { "", "{", "null", "[]", "123", "{}", "{\"name\":", "{\"name\":\"Ana\",}", "{\"name\":\"\\uD800\",\"email\":\"ana@example.com\",\"password\":\"password\"}" })
            await Reject("malformed/missing JSON", json);
        await Reject("wrong media type", Json(), HttpStatusCode.UnsupportedMediaType, contentType: "text/plain");
        var valid = Json();
        var overLimit = valid + new string(' ', RegisterRequest.MaxBodyBytes + 1 - Encoding.UTF8.GetByteCount(valid));
        await Reject("body too large", overLimit, HttpStatusCode.RequestEntityTooLarge);
        await Reject("streaming body too large", overLimit, HttpStatusCode.RequestEntityTooLarge, streaming: true);

        foreach (var name in new[] { "João da Conceição", "D'Ávila", "Anne-Marie", "李 明", "أمل", "Jose\u0301", "Ana\u0001Silva\u001f\u007f", new string('a', 120) })
            await Create(name);
        await Create(suppliedEmail: email200);
        await Create(password: new string('p', 128));
        await Create(password: "pass\0word"); // Only the hash is persisted, not raw password text.
        await Create("  Ana Silva  ", "  correct horse  ", "  NORMALIZED@EXAMPLE.COM  ");
        await Create(streaming: true);
        await Create(padding: RegisterRequest.MaxBodyBytes);
        await Create(streaming: true, padding: RegisterRequest.MaxBodyBytes);

        var duplicateEmail = await Create();
        var beforeDuplicate = Snapshot();
        using (var response = await client.PostAsync("/api/auth/register", new StringContent(Json(email: duplicateEmail.ToUpperInvariant()), Encoding.UTF8, "application/json")))
        {
            Check(response.StatusCode == HttpStatusCode.Conflict, "Duplicate email must remain 409.");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check(body.RootElement.GetProperty("message").GetString() == "Já existe uma conta com este e-mail.", "Duplicate email response changed.");
            Check(factory.Database.Commands > beforeDuplicate.Commands && factory.Database.Saves == beforeDuplicate.Saves && factory.Hasher.HashCalls == beforeDuplicate.Hashes,
                "Duplicate email must query but not hash or save.");
            count++;
        }
        using (var scope = factory.Services.CreateScope())
            Check(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync() == successes, "Unexpected user writes or duplicate rows.");
        Console.WriteLine($"Real registration integration checks passed: {count} HTTP cases, {successes} persisted users; invalid requests executed zero DB commands, saves and hashes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Unknown length plus small delayed writes exercises incremental body reading;
    // transport framing itself remains covered by the existing Kestrel checks.
    private sealed class FragmentedContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            for (var offset = 0; offset < bytes.Length; offset += 257)
            {
                await stream.WriteAsync(bytes.AsMemory(offset, Math.Min(257, bytes.Length - offset)));
                if (offset + 257 < bytes.Length)
                {
                    await stream.FlushAsync();
                    await Task.Delay(1);
                }
            }
        }
    }
}
