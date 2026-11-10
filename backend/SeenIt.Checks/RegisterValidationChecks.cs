using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SeenIt.Api.DTOs;
using SeenIt.Api.Services;

internal static class RegisterValidationChecks
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var handlerCalls = 0;
        RegisterRequest? accepted = null;
        // Same binder/filter and argument position as production. The continuation is the
        // boundary containing all DB queries, hashing and writes; no real provider is registered.
        app.MapPost("/api/auth/register", (RegisterRequest request) =>
        {
            handlerCalls++;
            accepted = request;
            return Results.Created("/api/auth/me", new AuthResponse("fixture-token", DateTime.UnixEpoch,
                new UserResponse(Guid.Empty, request.Name!, request.Email!, null)));
        }).AddEndpointFilter<RegisterValidationFilter>();
        await app.StartAsync();
        using var client = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        var count = 0;
        async Task Expect(string label, string json, HttpStatusCode expected, bool chunked = false,
            string contentType = "application/json")
        {
            var before = handlerCalls;
            using HttpContent content = chunked
                ? new ChunkedContent(Encoding.UTF8.GetBytes(json))
                : new StringContent(json, Encoding.UTF8, contentType);
            if (chunked) content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var response = await client.PostAsync("/api/auth/register", content);
            if (response.StatusCode != expected)
                throw new InvalidOperationException($"{label}: expected {(int)expected}, got {(int)response.StatusCode}.");
            if (handlerCalls != before + (expected == HttpStatusCode.Created ? 1 : 0))
                throw new InvalidOperationException($"{label}: rejected input reached the DB/hash/write boundary.");
            if (expected == HttpStatusCode.Created)
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = body.RootElement;
                if (!root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "expiresAtUtc", "token", "user" }) ||
                    !root.GetProperty("user").EnumerateObject().Select(p => p.Name).Order()
                        .SequenceEqual(new[] { "avatarUrl", "email", "id", "name" }) ||
                    response.Headers.Location?.ToString() != "/api/auth/me")
                    throw new InvalidOperationException($"{label}: success contract changed.");
            }
            count++;
        }
        string Json(string? name = "Ana", string? email = "ana@example.com", string? password = "password") =>
            JsonSerializer.Serialize(new { name, email, password });

        foreach (var field in new[] { "name", "email", "password" })
        {
            var values = new Dictionary<string, object?> { ["name"] = "Ana", ["email"] = "ana@example.com", ["password"] = "password" };
            values.Remove(field);
            await Expect($"missing {field}", JsonSerializer.Serialize(values), HttpStatusCode.BadRequest);
            foreach (var invalid in new object?[] { null, "", "   ", 123, true, new[] { "value" }, new { value = "text" } })
            {
                values[field] = invalid;
                await Expect($"invalid {field} ({invalid?.GetType().Name ?? "null"})", JsonSerializer.Serialize(values), HttpStatusCode.BadRequest);
            }
        }
        await Expect("name 120", Json(name: new string('a', 120)), HttpStatusCode.Created);
        await Expect("name 121", Json(name: new string('a', 121)), HttpStatusCode.BadRequest);
        var email200 = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + ".example";
        if (email200.Length != 200) throw new InvalidOperationException("Email boundary fixture must be 200 characters.");
        await Expect("email 200", Json(email: email200), HttpStatusCode.Created);
        await Expect("email 201", Json(email: email200 + "x"), HttpStatusCode.BadRequest);
        foreach (var size in new[] { 7, 8, 128, 129 })
            await Expect($"password {size}", Json(password: new string('p', size)),
                size is 8 or 128 ? HttpStatusCode.Created : HttpStatusCode.BadRequest);
        foreach (var password in new[] { "        ", new string(' ', 128), "\t\r\n     ", "\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0" })
            await Expect("whitespace password", Json(password: password), HttpStatusCode.BadRequest);
        foreach (var email in new[] { "invalid", "a@", "@example.com", "a@@example.com", "a b@example.com",
            "Ana <ana@example.com>", "<ana@example.com>", "ana@example.com (Ana)", "a@example.com,b@example.com",
            "\"ana\"@example.com", "a@localhost", "a@-example.com", "a@example..com", ".a@example.com", "a..b@example.com", "a@example.com\r\nBcc: b@example.com" })
            await Expect("malformed email", Json(email: email), HttpStatusCode.BadRequest);
        foreach (var json in new[] { "", "{", "null", "[]", "123", "{}", "{\"name\":", "{\"name\":\"Ana\",}" })
            await Expect("invalid JSON/body", json, HttpStatusCode.BadRequest);
        await Expect("content type", Json(), HttpStatusCode.UnsupportedMediaType, contentType: "text/plain");
        var valid = Json();
        var exact = valid + new string(' ', RegisterRequest.MaxBodyBytes - Encoding.UTF8.GetByteCount(valid));
        await Expect("exact body limit", exact, HttpStatusCode.Created);
        await Expect("body limit + 1", exact + " ", HttpStatusCode.RequestEntityTooLarge);
        await Expect("chunked body limit + 1", exact + " ", HttpStatusCode.RequestEntityTooLarge, chunked: true);
        await Expect("chunked valid", Json(), HttpStatusCode.Created, chunked: true);
        const string passwordUnchanged = "  correct horse  ";
        await Expect("normalized valid", Json("  Ana Silva  ", "  ANA+tag@EXAMPLE.COM  ", passwordUnchanged), HttpStatusCode.Created);
        if (accepted is not { Name: "Ana Silva", Email: "ana+tag@example.com", Password: passwordUnchanged })
            throw new InvalidOperationException("Identifier normalization/password preservation failed.");
        foreach (var name in new[] { "Ana\0Silva", "\0Ana", "Ana\0" })
            await Expect("NUL in name", Json(name: name), HttpStatusCode.BadRequest);
        foreach (var name in new[] { "João da Conceição", "D'Ávila", "Anne-Marie" })
            await Expect("legitimate name", Json(name: name), HttpStatusCode.Created);
        await app.StopAsync();
        Console.WriteLine($"Register validation checks passed: {count} HTTP cases; rejected bodies never reached the DB/hash/write continuation.");
    }

    private sealed class ChunkedContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
