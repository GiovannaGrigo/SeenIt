using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.Services;

// Opt-in, fixed loopback-only container connection. No ambient connection strings or migrations.
internal static class PostgresIntegrityChecks
{
    static void Check(bool value,string label) { if(!value) throw new InvalidOperationException(label); }
    public static async Task RunAsync()
    {
        await using (var evidence=new NpgsqlConnection(RegistrationFactory.PostgresConnection))
        {
            await evidence.OpenAsync();
            Console.WriteLine($"PostgreSQL evidence: {evidence.PostgreSqlVersion}, loopback 127.0.0.1:15432, database seenit_b03, disposable container only.");
        }
        var probe=new MutationProbe();
        RegistrationFactory Factory()=>new()
        {
            IsolatedPostgres=true,
            Settings=new() { ["RateLimiting:Mutation:PerMinute"]="10000", ["RateLimiting:Expensive:PerMinute"]="10000" },
            ExtraServices=services=>
            {
                services.RemoveAll<IHttpClientFactory>();services.AddSingleton<IHttpClientFactory>(new CatalogResilienceChecks.FakeCatalog());
                services.AddDbContext<AppDbContext>(options=>options.AddInterceptors(probe));
            }
        };
        await using var first=Factory();await using var second=Factory();
        using var a=first.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost") });
        using var b=second.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost") });
        Guid userId,otherId;string token,otherToken;
        using(var scope=first.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await db.Database.EnsureCreatedAsync();
            var user=new User { Name="Postgres",Email=$"{Guid.NewGuid():N}@example.com",PasswordHash="unused" };
            var other=new User { Name="Other",Email=$"{Guid.NewGuid():N}@example.com",PasswordHash="unused" };
            db.Users.AddRange(user,other);
            db.UserSeries.AddRange(new UserSeries { UserId=user.Id,ExternalSeriesId=1,Name="Series",Status=SeriesStatus.InList,IsAnime=false },new UserSeries { UserId=other.Id,ExternalSeriesId=1,Name="Series",Status=SeriesStatus.InList,IsAnime=false });
            await db.SaveChangesAsync();userId=user.Id;otherId=other.Id;
            var tokens=scope.ServiceProvider.GetRequiredService<TokenService>();token=tokens.Create(user).Token;otherToken=tokens.Create(other).Token;
        }
        a.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        b.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        object Body(int episode,int feeling)=>new { externalSeriesId=1,temporada=1,numeroEpisodio=episode==10?1:2,nomeEpisodio="Requested",sentimentos=new[]{feeling},externalCharacterId=0,personagemFavoritoNome="" };
        Task<HttpResponseMessage> Put(HttpClient client,int episode,int feeling)=>client.PutAsJsonAsync($"/api/episodios/{episode}/assistido",Body(episode,feeling));
        async Task AssertFeelings(params SentimentoEpisodio[] expected)
        {
            using var scope=first.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows=await db.EpisodiosAssistidos.Include(x=>x.Sentimentos).Where(x=>x.UserId==userId&&x.ExternalEpisodeId==10).ToListAsync();
            Check(rows.Count==1&&rows[0].Sentimentos.Select(x=>x.Sentimento).Order().SequenceEqual(expected.Order()),"Unexpected merged feelings/duplicate rows");
        }
        // Deterministic overlap between separate API containers/service providers and DB connections.
        probe.Block(userId);
        var insert=Put(a,10,1);await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Check((await Put(b,10,2)).StatusCode==HttpStatusCode.Conflict,"Concurrent insertion must return 409");
            b.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",otherToken);
            Check((await Put(b,10,3)).StatusCode==HttpStatusCode.OK,"Another user blocked by aggregate lock");
        }
        finally { probe.Release.TrySetResult();b.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token); }
        Check((await insert).StatusCode==HttpStatusCode.OK,"Winning insert failed");await AssertFeelings(SentimentoEpisodio.Feliz);
        probe.Block(userId);
        var update=Put(a,10,2);await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Check((await Put(b,10,3)).StatusCode==HttpStatusCode.Conflict,"Concurrent update must return 409"); }
        finally { probe.Release.TrySetResult(); }
        Check((await update).StatusCode==HttpStatusCode.OK,"Winning update failed");await AssertFeelings(SentimentoEpisodio.Triste);
        // Manual retry after refresh is a new intentional write, never an automatic overwrite.
        Check((await Put(b,10,3)).StatusCode==HttpStatusCode.OK,"Explicit retry failed");await AssertFeelings(SentimentoEpisodio.Raiva);
        using(var scope=first.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.EpisodiosAssistidos.RemoveRange(await db.EpisodiosAssistidos.Where(x=>x.UserId==userId).ToListAsync());
            (await db.UserSeries.SingleAsync(x=>x.UserId==userId&&x.ExternalSeriesId==1)).Status=SeriesStatus.InList;
            await db.SaveChangesAsync();
        }
        probe.Block(userId);
        var beforeCompletion=Put(a,10,1);await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Check((await Put(b,11,1)).StatusCode==HttpStatusCode.Conflict,"Competing final episode must conflict"); }
        finally { probe.Release.TrySetResult(); }
        Check((await beforeCompletion).StatusCode==HttpStatusCode.OK,"First marking failed");
        Check((await Put(b,11,1)).StatusCode==HttpStatusCode.OK,"Final episode retry failed");
        using(var scope=first.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Check((await db.UserSeries.SingleAsync(x=>x.UserId==userId&&x.ExternalSeriesId==1)).Status==SeriesStatus.Finished,"Final status stale after concurrent marking");
            Check((await db.EpisodiosAssistidos.Include(x=>x.Sentimentos).SingleAsync(x=>x.UserId==otherId&&x.ExternalEpisodeId==10)).Sentimentos.Single().Sentimento==SentimentoEpisodio.Raiva,"Cross-user data changed");
        }
        // A real PostgreSQL unique violation during SaveChanges, not just an injected exception.
        probe.Duplicate=true;
        Check((await Put(a,10,5)).StatusCode==HttpStatusCode.Conflict,"23505 leaked as 500");await AssertFeelings(SentimentoEpisodio.Feliz);
        // SavedChanges interceptor runs after actual SQL but before the explicit transaction commit.
        probe.FailAfterSql=true;
        Check((await a.DeleteAsync("/api/episodios/series/1/temporadas/1/assistida")).StatusCode==HttpStatusCode.Conflict,"Failure during SaveChanges not controlled");
        Check(probe.FailureObserved,"Injected failure did not occur after SQL");await AssertFeelings(SentimentoEpisodio.Feliz);
        using(var scope=first.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Check(await db.EpisodiosAssistidos.CountAsync(x=>x.UserId==userId)==2,"Rollback lost episodes");
            Check((await db.UserSeries.SingleAsync(x=>x.UserId==userId&&x.ExternalSeriesId==1)).Status==SeriesStatus.Finished,"Rollback left partial status");
            // Exact varchar scalar boundary on real PostgreSQL, and NUL rejection before SQL.
            var row=await db.UserSeries.SingleAsync(x=>x.UserId==userId&&x.ExternalSeriesId==1);row.Name=string.Concat(Enumerable.Repeat("🎬",240));await db.SaveChangesAsync();
        }
        // An advisory key also protects a library row that does not exist yet.
        Guid newId;string newToken;
        using(var scope=first.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user=new User { Name="New aggregate",Email=$"{Guid.NewGuid():N}@example.com",PasswordHash="unused" };
            db.Users.Add(user);await db.SaveChangesAsync();newId=user.Id;
            newToken=scope.ServiceProvider.GetRequiredService<TokenService>().Create(user).Token;
        }
        a.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",newToken);
        b.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",newToken);
        probe.Block(newId);
        var body=new { externalSeriesId=1,name="New series",status=1 };
        var create=a.PostAsJsonAsync("/api/my-series/",body);await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Check((await b.PostAsJsonAsync("/api/my-series/",body)).StatusCode==HttpStatusCode.Conflict,"Absent library aggregate unprotected"); }
        finally { probe.Release.TrySetResult(); }
        Check((await create).StatusCode==HttpStatusCode.Created,"Winning library insert failed");
        Console.WriteLine("PASS PostgreSQL isolated: 2 API providers/connections; same-episode updates/inserts 409, no merged feelings; final status after explicit retry; user isolation; actual 23505->409; rollback after SQL; Unicode varchar boundary. No migrations.");
    }

    private sealed class MutationProbe : SaveChangesInterceptor
    {
        Guid blockedUser;
        public TaskCompletionSource Entered { get; private set; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; private set; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Duplicate,FailAfterSql,FailureObserved;
        public void Block(Guid user) { blockedUser=user;Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);Release=new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            var db=(AppDbContext)data.Context!;
            if(blockedUser!=Guid.Empty&&(db.ChangeTracker.Entries<EpisodioAssistido>().Any(x=>x.Entity.UserId==blockedUser)||db.ChangeTracker.Entries<UserSeries>().Any(x=>x.Entity.UserId==blockedUser)))
            { blockedUser=Guid.Empty;Entered.TrySetResult();await Release.Task.WaitAsync(ct); }
            if(Duplicate)
            {
                Duplicate=false;
                var original=db.ChangeTracker.Entries<EpisodioAssistido>().First().Entity;
                db.EpisodiosAssistidos.Add(new() { UserId=original.UserId,ExternalEpisodeId=original.ExternalEpisodeId,ExternalSeriesId=original.ExternalSeriesId,Temporada=1,NumeroEpisodio=1,NomeEpisodio="Duplicate" });
            }
            return result;
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default)
        {
            if(FailAfterSql) { FailAfterSql=false;FailureObserved=true;throw new PostgresException("Synthetic transaction failure","ERROR","ERROR","40001"); }
            return ValueTask.FromResult(result);
        }
    }
}
