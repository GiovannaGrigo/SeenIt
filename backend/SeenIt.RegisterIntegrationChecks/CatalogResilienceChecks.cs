using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.External;
using SeenIt.Api.Services;

internal static class CatalogResilienceChecks
{
    static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    public static async Task RunAsync()
    {
        using var cache = new CatalogCache();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls=0;
        async Task<string> Fetch(CancellationToken ct) { Interlocked.Increment(ref calls); entered.TrySetResult(); await release.Task.WaitAsync(ct); return "shared"; }
        using var canceled = new CancellationTokenSource();
        var first = cache.GetAsync("same",Fetch,canceled.Token);
        await entered.Task;
        var second = cache.GetAsync("same",Fetch,CancellationToken.None);
        canceled.Cancel();
        try { await first; throw new Exception("Cancellation expected"); } catch (OperationCanceledException) { }
        release.SetResult();
        Check(await second=="shared" && calls==1,"Canceled owner affected another consumer");
        Check(await cache.GetAsync("same",Fetch,CancellationToken.None)=="shared" && calls==1,"Warm cache missed");
        int attempts=0;
        Task<string> Recover(CancellationToken _) => ++attempts==1 ? Task.FromException<string>(new CatalogException("synthetic")) : Task.FromResult("recovered");
        try { await cache.GetAsync("failure",Recover,CancellationToken.None); throw new Exception("Failure expected"); } catch (CatalogException) { }
        Check(await cache.GetAsync("failure",Recover,CancellationToken.None)=="recovered" && attempts==2,"Failure cached as success");
        // Admission is bounded even when callers use many distinct untrusted IDs.
        var gates = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Enumerable.Range(0,32).Select(i=>cache.GetAsync("capacity:"+i, async ct=> { await gates.Task.WaitAsync(ct); return i; },CancellationToken.None)).ToArray();
        try { await cache.GetAsync("overflow",_=>Task.FromResult(1),CancellationToken.None); throw new Exception("Capacity expected"); } catch (CatalogException) { }
        gates.SetResult(); await Task.WhenAll(pending);
        var consumerGate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumers=Enumerable.Range(0,128).Select(_=>cache.GetAsync("consumer-limit",async ct=> { await consumerGate.Task.WaitAsync(ct);return 1; },CancellationToken.None)).ToArray();
        try { await cache.GetAsync("consumer-limit",_=>Task.FromResult(1),CancellationToken.None);throw new Exception("Consumer capacity expected"); } catch(CatalogException) { }
        consumerGate.SetResult();await Task.WhenAll(consumers);
        using(var bounded=new CatalogCache())
        {
            int misses=0;
            Task<int> Count(CancellationToken _) { misses++;return Task.FromResult(1); }
            for(var i=0;i<513;i++) await bounded.GetAsync("entry:"+i,Count,CancellationToken.None);
            await bounded.GetAsync("entry:512",Count,CancellationToken.None);
            Check(misses==514,"Cache key cardinality unbounded");
            var largeCalls=0;
            Task<string> Large(CancellationToken _) { largeCalls++;return Task.FromResult(new string('x',2*1024*1024+1)); }
            await bounded.GetAsync("oversize",Large,CancellationToken.None);
            await bounded.GetAsync("oversize",Large,CancellationToken.None);
            Check(largeCalls==2,"Oversized entry cached");
        }

        var http = new FakeCatalog();
        await using var factory = new RegistrationFactory
        {
            Settings = new() { ["RateLimiting:Expensive:PerMinute"]="10000", ["RateLimiting:Mutation:PerMinute"]="10000" },
            ExtraServices = services=> { services.RemoveAll<IHttpClientFactory>(); services.AddSingleton<IHttpClientFactory>(http); }
        };
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost") });
        using (var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            var user=new User { Name="Catalog", Email="catalog@example.com", PasswordHash="unused" };db.Users.Add(user);
            db.EpisodiosAssistidos.Add(new() { UserId=user.Id, ExternalSeriesId=1, ExternalEpisodeId=10, Temporada=1, NumeroEpisodio=1, NomeEpisodio="Existing", ExternalCharacterId=7,PersonagemFavoritoNome="Historical" }); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",scope.ServiceProvider.GetRequiredService<TokenService>().Create(user).Token);
        }
        var shared=factory.Services.GetRequiredService<CatalogCache>();
        foreach (var failure in new[] { "404","429","500","timeout","invalid" })
        {
            shared.Invalidate("episodes:1");http.EpisodeFailure=failure;
            var saves=factory.Database.Saves;
            var response=await client.GetAsync("/api/profile/watch-time?timeZone=UTC");
            Check(response.StatusCode==(failure=="404"?HttpStatusCode.NotFound:HttpStatusCode.ServiceUnavailable),"Watch-time wrong catalog status: "+failure+" actual "+response.StatusCode+" "+await response.Content.ReadAsStringAsync());
            Check(factory.Database.Saves==saves,"Watch-time wrote on catalog failure");
        }
        http.EpisodeFailure="";
        // Main cast succeeds while guest cast fails: never expose a partial valid selection.
        foreach (var failure in new[] { "404","429","500","timeout","invalid" })
        {
            shared.Invalidate("guest-cast:10"); http.GuestFailure=failure;
            var result=await client.GetAsync("/api/series/1/episodios/10/personagens");
            Check(result.StatusCode==(failure=="404"?HttpStatusCode.NotFound:HttpStatusCode.ServiceUnavailable),"Partial cast incorrectly accepted: "+failure);
        }
        http.GuestFailure="";
        http.MainName="Renamed";shared.Invalidate("show-cast:1");
        var cast=await client.GetStringAsync("/api/series/1/episodios/10/personagens");
        Check(cast.Contains("Renamed"),"Trusted rename not displayed");
        var votes=await client.GetStringAsync("/api/episodios/10/votacao-personagens");
        Check(votes.Contains("Renamed")&&!votes.Contains("Historical"),"Rename did not reach canonical aggregation");
        http.CharacterRemoved=true;shared.Invalidate("show-cast:1");
        Check(await client.GetStringAsync("/api/episodios/10/votacao-personagens")=="[]","Removed character still counted");
        var before=factory.Database.Saves;
        var invalidVote=await client.PutAsJsonAsync("/api/episodios/10/assistido",new { externalSeriesId=1,temporada=1,numeroEpisodio=1,nomeEpisodio="One",externalCharacterId=7,personagemFavoritoNome="Fake",sentimentos=new[]{1} });
        Check(invalidVote.StatusCode==HttpStatusCode.BadRequest&&factory.Database.Saves==before,"Removed character accepted");
        using(var scope=factory.Services.CreateScope())
            Check((await scope.ServiceProvider.GetRequiredService<AppDbContext>().EpisodiosAssistidos.SingleAsync()).PersonagemFavoritoNome=="Historical","Historical vote was rewritten");
        http.MainFailure="404";shared.Invalidate("show-cast:1");
        Check((await client.GetAsync("/api/series/1/episodios/10/personagens")).StatusCode==HttpStatusCode.NotFound,"Removed series cast became 503");
        http.EpisodeFailure="404";
        Check((await client.GetAsync("/api/series/1/episodes")).StatusCode==HttpStatusCode.NotFound,"404 failed to invalidate episodes");
        Console.WriteLine("PASS: bounded single-flight/cache, independent cancellation, failure recovery, partial cast, removed resource, rename and watch-time 404/429/500/timeout/invalid.");
    }

    internal sealed class FakeCatalog : HttpMessageHandler, IHttpClientFactory
    {
        public string EpisodeFailure="",GuestFailure="",MainFailure="";
        public string MainName="Original";
        public bool CharacterRemoved;
        public HttpClient CreateClient(string name)=>new(this,false) { BaseAddress=new Uri("https://synthetic.invalid/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath;
            var failure=path.EndsWith("guestcast")?GuestFailure:path.EndsWith("cast")?MainFailure:EpisodeFailure;
            if(failure=="timeout") throw new TaskCanceledException("synthetic timeout");
            if(int.TryParse(failure,out var status)) return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
            var body=failure=="invalid"?"{":path.EndsWith("guestcast")?"[]":path.EndsWith("cast")?(CharacterRemoved?"[]":System.Text.Json.JsonSerializer.Serialize(new[]{new {character=new {id=7,name=MainName}}})):path.StartsWith("/episodes/")?"{\"id\":10,\"_embedded\":{\"show\":{\"id\":1}}}":!path.EndsWith("episodes")?"{\"id\":1,\"name\":\"Show\",\"genres\":[]}":
                "[{\"id\":10,\"season\":1,\"number\":1,\"name\":\"One\",\"runtime\":40},{\"id\":11,\"season\":1,\"number\":2,\"name\":\"Two\",\"runtime\":40}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(body,Encoding.UTF8,"application/json") });
        }
    }
}
