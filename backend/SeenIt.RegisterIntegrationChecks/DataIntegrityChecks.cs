using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.Services;

internal static class DataIntegrityChecks
{
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    public static async Task RunAsync()
    {
        var http = new CatalogHttp();
        await using var factory = new RegistrationFactory
        {
            Settings = new() { ["RateLimiting:Mutation:PerMinute"] = "10000", ["RateLimiting:Catalog:PerMinute"] = "10000", ["RateLimiting:Expensive:PerMinute"] = "10000" },
            ExtraServices = services => { services.RemoveAll<IHttpClientFactory>(); services.AddSingleton<IHttpClientFactory>(http); }
        };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Name = "Local", Email = "b03@example.com", PasswordHash = "unused" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<TokenService>().Create(user).Token);
        }
        JsonObject Series() => new() { ["externalSeriesId"] = 1, ["name"] = new string('a',240), ["posterUrl"] = "https://static.tvmaze.com/uploads/images/medium_portrait/1/1.jpg", ["summary"] = new string('b',3000), ["status"] = 1 };
        JsonObject Episode() => new() { ["externalSeriesId"] = 1, ["temporada"] = 1, ["numeroEpisodio"] = 1, ["nomeEpisodio"] = "Unicode 🎬 日本語", ["externalCharacterId"] = 7, ["personagemFavoritoNome"] = "Forged", ["sentimentos"] = new JsonArray(1,2) };
        async Task Expect(HttpMethod method, string path, string body, HttpStatusCode status, bool noWrite = true)
        {
            var saves = factory.Database.Saves;
            using var response = await client.SendAsync(new HttpRequestMessage(method,path) { Content = new StringContent(body,Encoding.UTF8,"application/json") });
            Check(response.StatusCode == status, $"{path}: expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            if (noWrite) Check(saves == factory.Database.Saves, "Rejected request wrote data");
        }
        Check((await client.GetAsync("/api/series/search?q=" + new string('x',201))).StatusCode == HttpStatusCode.BadRequest,"Search limit");
        Check((await client.GetAsync("/api/my-series/?status=99")).StatusCode == HttpStatusCode.BadRequest,"Filter enum");
        foreach (var body in new[] { "null", "{", "[]", "{\"externalSeriesId\":{}}" })
            await Expect(HttpMethod.Post,"/api/my-series/",body,HttpStatusCode.BadRequest);
        foreach (var (field,value) in new (string,JsonNode?)[] {
            ("name",JsonValue.Create(new string('x',241))), ("summary",JsonValue.Create(new string('x',3001))),
            ("posterUrl",JsonValue.Create("http://static.tvmaze.com/a")), ("posterUrl",JsonValue.Create("https://static.tvmaze.com.evil/a")),
            ("name",JsonValue.Create("nul\0")), ("externalSeriesId",JsonValue.Create(0)), ("externalSeriesId",JsonValue.Create(-1)),
            ("status",JsonValue.Create(99)), ("name",null) })
        { var r=Series(); r[field]=value; await Expect(HttpMethod.Post,"/api/my-series/",r.ToJsonString(),HttpStatusCode.BadRequest); }
        await Expect(HttpMethod.Post,"/api/my-series/",Series().ToJsonString(),HttpStatusCode.Created,false);
        foreach (var (field,value) in new (string,JsonNode?)[] {
            ("externalSeriesId",JsonValue.Create(2)), ("externalCharacterId",JsonValue.Create(99)), ("externalCharacterId",JsonValue.Create(-1)),
            ("nomeEpisodio",JsonValue.Create(new string('x',241))), ("personagemFavoritoNome",JsonValue.Create("bad\0")),
            ("sentimentos",new JsonArray(1,1)), ("sentimentos",new JsonArray(6)), ("sentimentos",new JsonArray(1,2,3,4,5,1)),
            ("sentimentos",null), ("sentimentos",JsonValue.Create("Feliz")), ("temporada",JsonValue.Create(0)) })
        { var r=Episode(); r[field]=value; await Expect(HttpMethod.Put,"/api/episodios/10/assistido",r.ToJsonString(),HttpStatusCode.BadRequest); }
        foreach (var body in new[] { "null", "{", "[]", "{\"externalSeriesId\":\"bad\"}" })
            await Expect(HttpMethod.Put,"/api/episodios/10/assistido",body,HttpStatusCode.BadRequest);
        await Expect(HttpMethod.Put,"/api/episodios/0/assistido",Episode().ToJsonString(),HttpStatusCode.BadRequest);
        await Expect(HttpMethod.Put,"/api/episodios/999/assistido",Episode().ToJsonString(),HttpStatusCode.BadRequest);
        await Expect(HttpMethod.Put,"/api/episodios/10/assistido",Episode().ToJsonString(),HttpStatusCode.OK,false);
        var saved = await client.GetFromJsonAsync<JsonArray>("/api/episodios/series/1");
        Check(saved![0]!["personagemFavoritoNome"]!.GetValue<string>() == "Canonical", "Forged name persisted");
        var update = Episode(); update["sentimentos"] = new JsonArray(5);
        await Expect(HttpMethod.Put,"/api/episodios/10/assistido",update.ToJsonString(),HttpStatusCode.OK,false);
        var calls = http.Calls;
        await Expect(HttpMethod.Put,"/api/episodios/series/1/temporadas/1/assistida","",HttpStatusCode.OK,false);
        Check(http.Calls == calls, "Repeated episode/cast lookups bypassed cache");
        await Expect(HttpMethod.Put,"/api/episodios/series/1/temporadas/9/assistida","",HttpStatusCode.BadRequest);
        saved = await client.GetFromJsonAsync<JsonArray>("/api/episodios/series/1");
        Check(saved!.Count == 1 && saved[0]!["sentimentos"]!.AsArray().Count == 1 && saved[0]!["personagemFavoritoNome"]!.GetValue<string>() == "Canonical", "Batch overwrote existing episode choice");
        // Inject legacy fragmentation without altering the current user's record.
        using (var scope = factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user=new User { Name="Legacy", Email="legacy@example.com", PasswordHash="unused" }; db.Users.Add(user);
            db.EpisodiosAssistidos.Add(new() { UserId=user.Id, ExternalSeriesId=1, ExternalEpisodeId=10, Temporada=1, NumeroEpisodio=1, NomeEpisodio="Old", ExternalCharacterId=7, PersonagemFavoritoNome="Different" });
            await db.SaveChangesAsync();
        }
        var votes=await client.GetFromJsonAsync<JsonArray>("/api/episodios/10/votacao-personagens");
        Check(votes!.Count==1 && votes[0]!["totalVotos"]!.GetValue<int>()==2 && votes[0]!["personagemNome"]!.GetValue<string>()=="Canonical", "Canonical aggregation failed");
        await Expect(HttpMethod.Delete,"/api/episodios/series/1/temporadas/1/assistida","",HttpStatusCode.OK,false);
        var library = await client.GetFromJsonAsync<JsonArray>("/api/my-series/");
        Check(library![0]!["status"]!.GetValue<int>() == 1, "Atomic unwatch status failed");
        foreach (var failure in new[] { "429", "503", "timeout", "json", "null", "invalid" })
        {
            using (var scope = factory.Services.CreateScope())
                { var cache = scope.ServiceProvider.GetRequiredService<SeenIt.Api.External.CatalogCache>(); cache.Invalidate("show-cast:1"); cache.Invalidate("guest-cast:10"); }
            http.CastFailure = failure;
            await Expect(HttpMethod.Put,"/api/episodios/10/assistido",Episode().ToJsonString(),HttpStatusCode.ServiceUnavailable);
        }
        http.CastFailure = "";
        foreach (var failure in new[] { "429", "503", "timeout", "json", "null", "invalid" })
        {
            http.Failure=failure;
            var r=Series(); r["externalSeriesId"]=3;
            await Expect(HttpMethod.Post,"/api/my-series/",r.ToJsonString(),HttpStatusCode.ServiceUnavailable);
            var e=Episode(); e["externalSeriesId"]=3;
            await Expect(HttpMethod.Put,"/api/episodios/30/assistido",e.ToJsonString(),HttpStatusCode.ServiceUnavailable);
        }
        http.Failure="404";
        var missing=Series(); missing["externalSeriesId"]=3;
        await Expect(HttpMethod.Post,"/api/my-series/",missing.ToJsonString(),HttpStatusCode.BadRequest);
        foreach (var limit in new[] { 200,240,600,3000 })
        {
            Check(DataIntegrity.Text(string.Concat(Enumerable.Repeat("🎬",limit)),limit),"Unicode boundary");
            Check(!DataIntegrity.Text(new string('a',limit+1),limit),"Over limit");
        }
        Check(!DataIntegrity.Text("\ud800",240) && !DataIntegrity.Text("\0",240),"Invalid text");
        var image = "https://static1.tvmaze.com/";
        Check(DataIntegrity.Image(image + new string('x',600-image.Length)) && !DataIntegrity.Image(image + new string('x',601-image.Length)),"Image length boundary");
        foreach (var url in new[] { "https://user@static.tvmaze.com/a", "https://static.tvmaze.com:8443/a", "javascript:alert(1)", "https://evil.example/a", "https://static.tvmaze.com/a#b" })
            Check(!DataIntegrity.Image(url),"Image origin restriction");
        Console.WriteLine("PASS: B03 boundaries, Unicode/NUL, binding, IDs, emotions, associations, canonical votes, updates, external failures and no writes on rejection (SQLite + fake HTTP).");
    }

    private sealed class CatalogHttp : HttpMessageHandler, IHttpClientFactory
    {
        public string Failure = "";
        public string CastFailure = "";
        public int Calls;
        public HttpClient CreateClient(string name) => new(this,false) { BaseAddress=new Uri("https://catalog.invalid/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var path=request.RequestUri!.AbsolutePath;
            Calls++;
            if (path.EndsWith("cast") && CastFailure != "")
            {
                if (CastFailure == "timeout") throw new TaskCanceledException("synthetic timeout");
                if (int.TryParse(CastFailure,out var status)) return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
                return Response(CastFailure switch { "json" => "{", "null" => "null", _ => "[{\"character\":{\"id\":0}}]" });
            }
            if (path.Contains("/3"))
            {
                if (Failure=="timeout") throw new TaskCanceledException("synthetic timeout");
                if (int.TryParse(Failure,out var code)) return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code));
                return Response(Failure switch { "json"=>"{", "null"=>"null", _=>"{}" });
            }
            if (path.EndsWith("guestcast")) return Response("[]");
            if (path.EndsWith("cast")) return Response("[{\"character\":{\"id\":7,\"name\":\"Canonical\"}}]");
            if (path.StartsWith("/episodes/")) return Response("{\"id\":10,\"_embedded\":{\"show\":{\"id\":1}}}");
            if (path.EndsWith("episodes")) return Response(path.Contains("/2/") ? "[]" : "[{\"id\":10,\"season\":1,\"number\":1,\"name\":\"Canonical episode\"}]");
            return Response("{\"id\":1,\"name\":\"Catalog\",\"genres\":[]}");
        }
        private static Task<HttpResponseMessage> Response(string body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(body,Encoding.UTF8,"application/json") });
    }
}
