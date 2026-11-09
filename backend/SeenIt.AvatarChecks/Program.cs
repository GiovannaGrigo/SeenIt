using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeenIt.Api.Services.Avatars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Controllers;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SkiaSharp;
using System.Net;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Decode actual JPEG/PNG/WebP, resize, strip trailing payload, emit WebP", ImageChecks),
    ("Reject empty, oversize, spoofed MIME, corrupted and excessive dimensions", InvalidImageChecks),
    ("Validate Supabase settings, production required and development opt-in", OptionsChecks),
    ("Official upload/delete URLs, apikey only, no upsert, bounded owned paths", HttpChecks),
    ("Storage failures sanitized; no deletion on conflict; unknown upload compensation", StorageFailureChecks),
    ("Shared public URL, legacy compatibility, traversal and cross-user rejection", UrlChecks),
    ("Replacement persists before deleting old object", ReplacementChecks),
    ("Failed persistence compensates uploaded object and preserves old", PersistenceFailureChecks),
    ("Ambiguous commit preserves referenced object; unknown DB preserves both", AmbiguousCommitChecks),
    ("Failed old-object deletion keeps new reference and emits cleanup event", CleanupFailureChecks),
    ("Concurrent replacement conflicts without deleting winner", ConflictChecks),
    ("Avatar removal persists null before deletion; failure explicit", RemovalChecks),
    ("Account deletion performs conditional database deletion before Storage cleanup", AccountChecks),
    ("Cancelled request still compensates new upload with independent cleanup token", CancellationChecks),
    ("Development local storage stays offline and legacy files are never removed", LocalChecks),
    ("JWT/form contracts and varchar(200) preserved; no pending model changes", ContractChecks)
};
foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine("PASS " + test.Name);
}
Console.WriteLine($"Avatar checks: {tests.Length} groups passed; no real HTTP, database, User Secrets or Supabase access.");

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
static byte[] ImageBytes(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int width = 20, int height = 10)
{
    using var bitmap = new SKBitmap(width, height);
    bitmap.Erase(SKColors.Blue);
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(format, 90);
    return encoded.ToArray();
}
static IFormFile File(byte[]? bytes = null, string mime = "image/png", string name = "untrusted-name.exe")
{
    bytes ??= ImageBytes();
    return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "avatar", name) { Headers = new HeaderDictionary(), ContentType = mime };
}
static AvatarStorageOptions Settings() => new() { Enabled = true, Url = "https://abcdefghijklmnopqrst.supabase.co", SecretKey = "sb_secret_FAKE_TEST_KEY_NOT_A_CREDENTIAL", Bucket = "avatars" };
static async Task ImageChecks()
{
    var processor = new AvatarImageProcessor();
    foreach (var item in new[] { (SKEncodedImageFormat.Png,"image/png"), (SKEncodedImageFormat.Jpeg,"image/jpeg"), (SKEncodedImageFormat.Webp,"image/webp") })
    {
        var bytes = ImageBytes(item.Item1, 1600, 800).Concat("UNTRUSTED_TRAILING_PAYLOAD"u8.ToArray()).ToArray();
        var result = await processor.ProcessAsync(File(bytes,item.Item2), default);
        using var data = SKData.CreateCopy(result.Bytes);
        using var codec = SKCodec.Create(data);
        Check(codec.EncodedFormat == SKEncodedImageFormat.Webp && codec.Info.Width == 1024 && codec.Info.Height == 512, "Expected actual resized WebP");
        Check(!System.Text.Encoding.UTF8.GetString(result.Bytes).Contains("UNTRUSTED_TRAILING_PAYLOAD"), "Re-encoding must discard appended payload");
    }
}
static async Task InvalidImageChecks()
{
    var p = new AvatarImageProcessor();
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(null,default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File([]),default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File(new byte[AvatarImageProcessor.MaxFileBytes+1]),default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File("not an image"u8.ToArray()),default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File(mime:"image/jpeg"),default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File(mime:"image/svg+xml"),default));
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File(ImageBytes(width:8193,height:1)),default));
    var truncated=ImageBytes();
    await Throws<InvalidAvatarException>(()=>p.ProcessAsync(File(truncated[..(truncated.Length/2)]),default));
    Check(AvatarImageProcessor.MaxRequestBytes > AvatarImageProcessor.MaxFileBytes, "Multipart needs overhead margin");
}
static async Task OptionsChecks()
{
    var settings=Settings(); Check(settings.IsValid(),"Valid settings rejected");
    foreach(var url in new[]{"http://abcdefghijklmnopqrst.supabase.co","https://localhost","https://evil.example","https://abcdefghijklmnopqrst.supabase.co.evil.example","https://abcdefghijklmnopqrst.supabase.co/path","https://abcdefghijklmnopqrst.supabase.co?key=value","https://u:p@abcdefghijklmnopqrst.supabase.co","https://abcdefghijklmnopqrst.supabase.co:444"})
    { settings.Url=url; Check(!settings.IsValid(),"Unsafe URL accepted"); }
    settings=Settings(); settings.SecretKey="eyJnot-a-secret-key"; Check(!settings.IsValid(),"JWT must not be accepted as sb_secret key");
    settings=Settings(); settings.Bucket="../avatars"; Check(!settings.IsValid(),"Unsafe bucket accepted");
    Check(new AvatarStorageOptions().IsValid(),"Development must work offline without keys");
    foreach(var environment in new[]{"Development","Production"})
    {
        var services=new ServiceCollection(); services.AddLogging();
        services.AddAvatarStorage(new ConfigurationBuilder().Build(),new TestEnvironment { EnvironmentName=environment });
        using var provider=services.BuildServiceProvider();
        if(environment=="Production") await Throws<OptionsValidationException>(()=>Task.FromResult(provider.GetRequiredService<IOptions<AvatarStorageOptions>>().Value));
        else Check(!provider.GetRequiredService<IOptions<AvatarStorageOptions>>().Value.Enabled,"Development should not access Supabase implicitly");
    }
}
static async Task HttpChecks()
{
    using var f=new Fixture();
    var user=Guid.NewGuid();
    var path=await f.Storage.UploadAsync(user,new ValidatedAvatar(ImageBytes(SKEncodedImageFormat.Webp)),default);
    var upload=f.Handler.Calls.Single();
    Check(upload.Method=="POST" && upload.Url==Settings().Url+"/storage/v1/object/avatars/"+path,"Wrong official upload endpoint");
    Check(upload.ApiKey==Settings().SecretKey && upload.Authorization is null && upload.Upsert=="false" && upload.ContentType=="image/webp","Wrong authentication/upsert/content type");
    Check(AvatarPaths.IsOwned(user,path) && path.Length<=200 && path.EndsWith(".webp"),"Wrong persisted relative path");
    Check(!upload.Url.Contains(Settings().SecretKey),"Key in URL");
    await f.Storage.DeleteAsync(user,path,default);
    var deletion=f.Handler.Calls.Last();
    Check(deletion.Method=="DELETE" && deletion.Url==Settings().Url+"/storage/v1/object/avatars","Wrong official delete endpoint");
    using var body=JsonDocument.Parse(deletion.Body);
    Check(body.RootElement.GetProperty("prefixes")[0].GetString()==path,"Wrong delete prefixes");
    var count=f.Handler.Calls.Count;
    await Throws<AvatarStorageException>(()=>f.Storage.DeleteAsync(Guid.NewGuid(),path,default));
    await Throws<AvatarStorageException>(()=>f.Storage.DeleteAsync(user,"../victim.webp",default));
    Check(f.Handler.Calls.Count==count,"Unsafe deletion must never reach HTTP");
}
static async Task StorageFailureChecks()
{
    foreach(var status in new[]{HttpStatusCode.Unauthorized,HttpStatusCode.BadRequest,HttpStatusCode.Conflict,HttpStatusCode.Redirect})
    {
        using var f=new Fixture(); f.Handler.Response=(_,_)=>Task.FromResult(new HttpResponseMessage(status) { Content=new StringContent(Settings().SecretKey) });
        await Throws<AvatarStorageException>(()=>f.Storage.UploadAsync(Guid.NewGuid(),new ValidatedAvatar([1]),default));
        Check(f.Handler.Calls.Count==1,"No cleanup of possibly pre-existing objects on rejection/conflict/redirect");
    }
    using(var f=new Fixture())
    {
        f.Handler.Response=(r,_)=>r.Method==HttpMethod.Post ? throw new HttpRequestException(Settings().SecretKey) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        try { await f.Storage.UploadAsync(Guid.NewGuid(),new ValidatedAvatar([1]),default); }
        catch(AvatarStorageException e) { Check(!e.ToString().Contains(Settings().SecretKey),"Storage error leaked key"); }
        Check(f.Handler.Calls.Select(c=>c.Method).SequenceEqual(new[]{"POST","DELETE"}),"Unknown upload outcome needs bounded compensation");
    }
    using(var f=new Fixture())
    {
        f.Handler.Response=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        await f.Storage.DeleteAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/00000000000000000000000000000000.webp",default);
    }
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
        f.Handler.Response=(r,_)=>Task.FromResult(new HttpResponseMessage(r.Method==HttpMethod.Post?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK));
        await Throws<AvatarStorageException>(()=>f.Profile.ReplaceAsync(user,old,File(),default));
        Check(f.Repository.Path==old && !f.Events.Contains("DB-CAS"),"Failed upload must never update reference");
    }
}
static Task UrlChecks()
{
    using var f=new Fixture();
    var user=Guid.NewGuid(); var path=AvatarPaths.New(user);
    var router=new AvatarStorageService(f.Storage,Options.Create(Settings()),new TestEnvironment(),NullLogger<AvatarStorageService>.Instance);
    var context=new DefaultHttpContext(); context.Request.Scheme="https"; context.Request.Host=new HostString("seenit.example"); context.Request.PathBase="/api-root";
    Check(router.GetPublicUrl(user,path,context.Request)==Settings().Url+"/storage/v1/object/public/avatars/"+path,"Incorrect public URL");
    Check(router.GetPublicUrl(user,"01234567890123456789012345678901.jpg",context.Request)=="https://seenit.example/api-root/uploads/profiles/01234567890123456789012345678901.jpg","Legacy must retain local URL");
    Check(router.GetPublicUrl(user,AvatarPaths.New(user,local:true),context.Request)!.StartsWith("https://seenit.example/api-root/uploads/profiles/local/"),"Local URL should remain local");
    foreach(var invalid in new[]{"../victim.jpg","https://evil.example/image.jpg",AvatarPaths.New(Guid.NewGuid()),user+"/../victim.webp",path+"?x=y",new string('a',201)})
        Check(router.GetPublicUrl(user,invalid,context.Request) is null,"Invalid/cross-user URL accepted");
    Check(router.GetPublicUrl(user,null,context.Request) is null,"Null avatar URL");
    return Task.CompletedTask;
}
static async Task ReplacementChecks()
{
    using var f=new Fixture(); var user=Guid.NewGuid(); f.Repository.Path=AvatarPaths.New(user); var old=f.Repository.Path;
    var newPath=await f.Profile.ReplaceAsync(user,old,File(),default);
    Check(f.Repository.Path==newPath && f.Events.SequenceEqual(new[]{"POST","DB-CAS","DELETE"}),"Wrong replacement ordering");
    Check(f.Handler.Calls.Last().Body.Contains(old!),"Wrong old object deletion");
}
static async Task PersistenceFailureChecks()
{
    using var f=new Fixture(); var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old; f.Repository.FailWrite=true;
    await Throws<IOException>(()=>f.Profile.ReplaceAsync(user,old,File(),default));
    Check(f.Repository.Path==old && f.Events.SequenceEqual(new[]{"POST","DB-CAS","DB-READ","DELETE"}),"Persistence failure compensation ordering");
    Check(!f.Handler.Calls.Last().Body.Contains(old),"Old object must survive persistence failure");
}
static async Task AmbiguousCommitChecks()
{
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); f.Repository.FailAfterCommit=true;
        await Throws<IOException>(()=>f.Profile.ReplaceAsync(user,null,File(),default));
        Check(f.Repository.Path is not null && f.Handler.Calls.Count==1,"Never delete newly committed object after ambiguous failure");
    }
    using(var f=new Fixture())
    {
        f.Repository.FailWrite=true; f.Repository.FailRead=true;
        await Throws<IOException>(()=>f.Profile.ReplaceAsync(Guid.NewGuid(),null,File(),default));
        Check(f.Handler.Calls.Count==1 && f.Logger.Messages.Any(m=>m.Contains("DatabaseOutcomeUnknown")),"Unknown persistence outcome must preserve object and request cleanup verification");
    }
}
static async Task CleanupFailureChecks()
{
    using var f=new Fixture(); var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
    f.Handler.Response=(r,_)=>Task.FromResult(new HttpResponseMessage(r.Method==HttpMethod.Delete?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK));
    var path=await f.Profile.ReplaceAsync(user,old,File(),default);
    Check(f.Repository.Path==path && f.Handler.Calls.Count==2,"Failed old-object deletion must preserve committed new avatar");
    Check(f.Logger.Messages.Any(m=>m.Contains("AvatarCleanupRequired")) && f.Logger.Messages.All(m=>!m.Contains(Settings().SecretKey)),"Safe cleanup event missing");
}
static async Task ConflictChecks()
{
    using var f=new Fixture(); var user=Guid.NewGuid(); var stale=AvatarPaths.New(user); var winner=AvatarPaths.New(user); f.Repository.Path=winner;
    await Throws<AvatarConflictException>(()=>f.Profile.ReplaceAsync(user,stale,File(),default));
    Check(f.Repository.Path==winner && f.Handler.Calls.Count==2 && !f.Handler.Calls.Last().Body.Contains(winner) && !f.Handler.Calls.Last().Body.Contains(stale),"Concurrent winner must survive; only failed contender cleaned");
    f.Handler.Calls.Clear();
    await Throws<AvatarConflictException>(()=>f.Profile.RemoveAsync(user,stale,default));
    Check(f.Handler.Calls.Count==0 && f.Repository.Path==winner,"Concurrent delete must not delete winner");
}
static async Task RemovalChecks()
{
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
        await f.Profile.RemoveAsync(user,old,default);
        Check(f.Repository.Path is null && f.Events.SequenceEqual(new[]{"DB-CAS","DELETE"}),"Delete reference before object");
    }
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old; f.Repository.FailWrite=true;
        await Throws<IOException>(()=>f.Profile.RemoveAsync(user,old,default));
        Check(f.Repository.Path==old && f.Handler.Calls.Count==0,"Failed null persistence must not delete object");
    }
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
        f.Handler.Response=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await f.Profile.RemoveAsync(user,old,default);
        Check(f.Repository.Path is null && f.Logger.Messages.Any(m=>m.Contains("AvatarCleanupRequired")),"Deletion failure must keep null reference and log cleanup");
    }
}
static async Task AccountChecks()
{
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
        await f.Profile.DeleteAccountAsync(user,old,"fake-hash",default);
        Check(!f.Repository.Exists && f.Events.SequenceEqual(new[]{"DB-DELETE-ACCOUNT","DELETE"}),"Delete account before avatar cleanup");
    }
    using(var f=new Fixture())
    {
        f.Repository.Path=AvatarPaths.New(Guid.NewGuid());
        await Throws<AvatarConflictException>(()=>f.Profile.DeleteAccountAsync(Guid.NewGuid(),null,"fake-hash",default));
        Check(f.Repository.Exists && f.Handler.Calls.Count==0,"Concurrent account change must preserve account/avatar");
    }
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old; f.Repository.FailWrite=true;
        await Throws<IOException>(()=>f.Profile.DeleteAccountAsync(user,old,"fake-hash",default));
        Check(f.Repository.Exists && f.Handler.Calls.Count==0,"Failed account persistence must not remove avatar");
    }
    using(var f=new Fixture())
    {
        var user=Guid.NewGuid(); var old=AvatarPaths.New(user); f.Repository.Path=old;
        f.Handler.Response=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await f.Profile.DeleteAccountAsync(user,old,"fake-hash",default);
        Check(!f.Repository.Exists && f.Logger.Messages.Any(m=>m.Contains("AvatarCleanupRequired")),"Failed Storage cleanup must not reverse account deletion");
    }
}
static async Task CancellationChecks()
{
    using var f=new Fixture(); f.Repository.CancelWrite=true;
    using var cancellation=new CancellationTokenSource();
    f.Repository.BeforeWrite=()=>cancellation.Cancel();
    await Throws<OperationCanceledException>(()=>f.Profile.ReplaceAsync(Guid.NewGuid(),null,File(),cancellation.Token));
    Check(cancellation.IsCancellationRequested && f.Events.SequenceEqual(new[]{"POST","DB-CAS","DB-READ","DELETE"}),"Cleanup should not reuse cancelled request token");
}
static async Task LocalChecks()
{
    using var f=new Fixture(); var environment=new TestEnvironment();
    Directory.CreateDirectory(environment.WebRootPath);
    try
    {
        var router=new AvatarStorageService(f.Storage,Options.Create(new AvatarStorageOptions()),environment,NullLogger<AvatarStorageService>.Instance);
        var user=Guid.NewGuid(); var image=new ValidatedAvatar(ImageBytes(SKEncodedImageFormat.Webp));
        var path=await router.UploadAsync(user,image,default);
        var full=Path.Combine(environment.WebRootPath,"uploads","profiles",path.Replace('/',Path.DirectorySeparatorChar));
        Check(System.IO.File.Exists(full) && f.Handler.Calls.Count==0,"Development must remain offline");
        await Throws<AvatarStorageException>(()=>router.DeleteAsync(Guid.NewGuid(),path,default));
        Check(System.IO.File.Exists(full),"Other user must not delete local avatar");
        await router.DeleteAsync(user,path,default); Check(!System.IO.File.Exists(full),"Local owned avatar removal");
        await router.DeleteAsync(user,AvatarPaths.New(user),default);
        Check(f.Handler.Calls.Count==0,"Offline mode must defer remote deletions without accessing Supabase");
        var legacy="00000000000000000000000000000000.jpg";
        var legacyPath=Path.Combine(environment.WebRootPath,"uploads","profiles",legacy);
        await System.IO.File.WriteAllBytesAsync(legacyPath,[1]);
        await router.DeleteAsync(user,legacy,default);
        Check(System.IO.File.Exists(legacyPath),"Legacy image must never be deleted implicitly");
    }
    finally { Directory.Delete(environment.ContentRootPath,recursive:true); }
}
static Task ContractChecks()
{
    var controller=typeof(ProfileController);
    Check(Attribute.IsDefined(controller,typeof(AuthorizeAttribute)),"Profile must preserve JWT authorization");
    Check(controller.GetCustomAttributes(typeof(RouteAttribute),true).Cast<RouteAttribute>().Single().Template=="api/profile","Profile route changed");
    var upload=controller.GetMethod(nameof(ProfileController.UpdateAvatar))!;
    Check(upload.GetCustomAttributes(typeof(HttpPutAttribute),true).Cast<HttpPutAttribute>().Single().Template=="avatar","Upload method/route changed");
    Check(upload.GetParameters()[0].Name=="avatar" && upload.GetParameters()[0].ParameterType==typeof(IFormFile) && Attribute.IsDefined(upload.GetParameters()[0],typeof(FromFormAttribute)),"Angular multipart contract changed");
    Check(((IRequestSizeLimitMetadata)upload.GetCustomAttributes(typeof(RequestSizeLimitAttribute),true).Single()).MaxRequestBodySize==AvatarImageProcessor.MaxRequestBytes,"Multipart overhead not configured");
    Check(controller.GetMethod(nameof(ProfileController.DeleteAvatar))!.GetCustomAttributes(typeof(HttpDeleteAttribute),true).Cast<HttpDeleteAttribute>().Single().Template=="avatar","Delete method/route changed");
    using var database=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused_model_only").Options);
    Check(database.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.AvatarFileName))!.GetMaxLength()==200,"Avatar field limit changed");
    Check(!database.Database.HasPendingModelChanges(),"Model snapshot drift; no migrations should be necessary");
    return Task.CompletedTask;
}

sealed record Call(string Method,string Url,string? ApiKey,string? Authorization,string? Upsert,string? ContentType,string Body);
sealed class FakeHandler(List<string> events) : HttpMessageHandler
{
    public List<Call> Calls { get; }=[];
    public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Response { get; set; }=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Add(request.Method.Method);
        Calls.Add(new(request.Method.Method,request.RequestUri!.ToString(),request.Headers.TryGetValues("apikey",out var keys)?keys.Single():null,request.Headers.Authorization?.ToString(),request.Headers.TryGetValues("x-upsert",out var upserts)?upserts.Single():null,request.Content?.Headers.ContentType?.MediaType,request.Method==HttpMethod.Delete&&request.Content is not null?await request.Content.ReadAsStringAsync(cancellationToken):""));
        return await Response(request,cancellationToken);
    }
}
sealed class ClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)=>client;
}
sealed class FakeReferences(List<string> events) : IAvatarReferenceStore
{
    public string? Path { get; set; }
    public bool Exists { get; private set; }=true;
    public bool FailWrite { get; set; }
    public bool FailAfterCommit { get; set; }
    public bool FailRead { get; set; }
    public bool CancelWrite { get; set; }
    public Action? BeforeWrite { get; set; }
    public Task<bool> CompareExchangeAsync(Guid userId,string? expected,string? replacement,CancellationToken cancellationToken)
    {
        events.Add("DB-CAS");
        BeforeWrite?.Invoke();
        if(FailWrite) throw new IOException("simulated persistence failure");
        if(CancelWrite) throw new OperationCanceledException();
        if(!Exists||Path!=expected) return Task.FromResult(false);
        Path=replacement;
        if(FailAfterCommit) throw new IOException("simulated ambiguous commit");
        return Task.FromResult(true);
    }
    public Task<bool> IsReferencedAsync(Guid userId,string path,CancellationToken cancellationToken)
    {
        events.Add("DB-READ");
        if(FailRead) throw new IOException("simulated read failure");
        return Task.FromResult(Exists&&Path==path);
    }
    public Task<bool> DeleteAccountAsync(Guid userId,string? expectedAvatar,string expectedPasswordHash,CancellationToken cancellationToken)
    {
        events.Add("DB-DELETE-ACCOUNT");
        if(FailWrite) throw new IOException("simulated delete failure");
        if(!Exists||Path!=expectedAvatar||expectedPasswordHash!="fake-hash") return Task.FromResult(false);
        Exists=false; return Task.FromResult(true);
    }
}
sealed class CaptureLogger<T> : ILogger<T>
{
    public List<string> Messages { get; }=[];
    public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
    public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>Messages.Add(formatter(state,exception));
}
sealed class Fixture : IDisposable
{
    public List<string> Events { get; }=[];
    public FakeHandler Handler { get; }
    public HttpClient Client { get; }
    public FakeReferences Repository { get; }
    public SupabaseAvatarStorageService Storage { get; }
    public AvatarProfileService Profile { get; }
    public CaptureLogger<AvatarProfileService> Logger { get; }=new();
    public Fixture()
    {
        Handler=new FakeHandler(Events); Client=new HttpClient(Handler); Repository=new FakeReferences(Events);
        Storage=new SupabaseAvatarStorageService(new ClientFactory(Client),Options.Create(new AvatarStorageOptions { Enabled=true,Url="https://abcdefghijklmnopqrst.supabase.co",SecretKey="sb_secret_FAKE_TEST_KEY_NOT_A_CREDENTIAL",Bucket="avatars" }),NullLogger<SupabaseAvatarStorageService>.Instance);
        var router=new AvatarStorageService(Storage,Options.Create(new AvatarStorageOptions { Enabled=true }),new TestEnvironment(),NullLogger<AvatarStorageService>.Instance);
        Profile=new AvatarProfileService(new AvatarImageProcessor(),router,Repository,Logger);
    }
    public void Dispose()=>Client.Dispose();
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; }="Development";
    public string ApplicationName { get; set; }="SeenIt.AvatarChecks";
    public string ContentRootPath { get; set; }=Path.Combine(Path.GetTempPath(),"seenit-avatar-checks-"+Guid.NewGuid().ToString("N"));
    public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    public string WebRootPath { get => Path.Combine(ContentRootPath,"wwwroot"); set { } }
    public IFileProvider WebRootFileProvider { get; set; }=new NullFileProvider();
}
