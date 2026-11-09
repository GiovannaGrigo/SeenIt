using System.Security.Claims;
using System.Net.Mail;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.DTOs;
using SeenIt.Api.External;
using SeenIt.Api.Services;
using System.Text.Json.Serialization;
using SeenIt.Api.Interfaces.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    if (!int.TryParse(port, out var portNumber) || portNumber is < 1 or > 65535)
        throw new InvalidOperationException("PORT deve ser uma porta válida entre 1 e 65535.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]) ||
    !int.TryParse(builder.Configuration["Jwt:ExpirationMinutes"], out var expirationMinutes) ||
    expirationMinutes <= 0)
    throw new InvalidOperationException("Configure Jwt:Key, Jwt:Issuer, Jwt:Audience e Jwt:ExpirationMinutes positivo.");

if (builder.Environment.IsProduction())
{
    // A geração aleatória é um requisito operacional; o formato e o tamanho são verificáveis aqui.
    byte[] keyBytes;
    try { keyBytes = Convert.FromBase64String(jwtKey); }
    catch (FormatException)
    {
        throw new InvalidOperationException("Em Production, Jwt:Key deve ser Base64 de pelo menos 32 bytes gerados aleatoriamente.");
    }
    if (keyBytes.Length < 32 || keyBytes.Distinct().Count() == 1)
        throw new InvalidOperationException("Em Production, Jwt:Key deve conter pelo menos 32 bytes gerados aleatoriamente.");
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection em Production.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment())
    allowedOrigins = allowedOrigins.Append("http://localhost:4200").Distinct().ToArray();
foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
        (builder.Environment.IsProduction() && uri.Scheme != Uri.UriSchemeHttps) ||
        origin.Contains('*') || !string.IsNullOrEmpty(uri.UserInfo) ||
        uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment) || origin.EndsWith('/'))
        throw new InvalidOperationException("Cors:AllowedOrigins deve conter origens explícitas sem caminho ou barra final; em Production, apenas HTTPS.");
}

// Evita a ativação automática do ASP.NET Core, que aceita cabeçalhos de qualquer proxy.
if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Não habilite ASPNETCORE_FORWARDEDHEADERS_ENABLED; configure ReverseProxy:KnownProxies explicitamente.");

var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
var proxyAddresses = knownProxies.Select(value =>
{
    if (!IPAddress.TryParse(value, out var address) ||
        address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        throw new InvalidOperationException("ReverseProxy:KnownProxies deve conter apenas endereços IP explícitos e confiáveis.");
    return address;
}).ToArray();
if (proxyAddresses.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var address in proxyAddresses)
        {
            options.KnownProxies.Add(address);
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                options.KnownProxies.Add(address.MapToIPv6());
        }
    });
}

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WatchTimeService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<TvMazeClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TvMaze:BaseUrl"]!);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SeenIt/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            NameClaimType = ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins);
    policy.AllowAnyHeader().AllowAnyMethod();
}));

builder.Services
.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

builder.Services.AddScoped<
    IEpisodioAssistidoService,
    EpisodioAssistidoService>();

builder.Services.AddHttpClient<
    ITvMazeService,
    TvMazeService>(client =>
    {
        client.BaseAddress = new Uri("https://api.tvmaze.com/");

        client.DefaultRequestHeaders.UserAgent.ParseAdd("SeenIt/1.0");
    });

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (proxyAddresses.Length > 0) app.UseForwardedHeaders();
// O health check básico também atende o tráfego HTTP interno do provedor.
app.UseWhen(context => !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseHttpsRedirection());
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

var auth = app.MapGroup("/api/auth").WithTags("Autenticação");

auth.MapPost("/register", async (
    RegisterRequest request,
    AppDbContext database,
    IPasswordHasher<User> hasher,
    TokenService tokens,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) ||
        !MailAddress.TryCreate(request.Email, out _) ||
        request.Password.Length < 8)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["register"] = ["Informe nome, e-mail válido e uma senha com pelo menos 8 caracteres."]
        });

    var email = request.Email.Trim().ToLowerInvariant();
    if (await database.Users.AnyAsync(x => x.Email == email, cancellationToken))
        return Results.Conflict(new { message = "Já existe uma conta com este e-mail." });

    var user = new User { Name = request.Name.Trim(), Email = email, PasswordHash = string.Empty };
    user.PasswordHash = hasher.HashPassword(user, request.Password);
    database.Users.Add(user);
    await database.SaveChangesAsync(cancellationToken);

    var token = tokens.Create(user);
    return Results.Created("/api/auth/me", new AuthResponse(
        token.Token, token.ExpiresAtUtc, new UserResponse(user.Id, user.Name, user.Email, user.AvatarFileName)));
});

auth.MapPost("/login", async (LoginRequest request, AppDbContext database, IPasswordHasher<User> hasher, TokenService tokens,
    HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (!MailAddress.TryCreate(request.Email, out _) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Unauthorized();
    }

    var email = request.Email
        .Trim()
        .ToLowerInvariant();

    var user = await database.Users.SingleOrDefaultAsync(
        x => x.Email == email,
        cancellationToken
    );

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var verificationResult = hasher.VerifyHashedPassword(
        user,
        user.PasswordHash,
        request.Password
    );

    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    if (verificationResult ==
        PasswordVerificationResult.SuccessRehashNeeded)
    {
        user.PasswordHash = hasher.HashPassword(
            user,
            request.Password
        );

        await database.SaveChangesAsync(cancellationToken);
    }

    var token = tokens.Create(user);

    string? avatarUrl = null;

    if (!string.IsNullOrWhiteSpace(user.AvatarFileName))
    {
        var httpRequest = httpContext.Request;

        avatarUrl =
            $"{httpRequest.Scheme}://" +
            $"{httpRequest.Host}" +
            $"{httpRequest.PathBase}" +
            $"/uploads/profiles/{Uri.EscapeDataString(user.AvatarFileName)}";
    }

    return Results.Ok(
        new AuthResponse(
            token.Token,
            token.ExpiresAtUtc,
            new UserResponse(
                user.Id,
                user.Name,
                user.Email,
                avatarUrl
            )
        )
    );
});

var series = app.MapGroup("/api/series").WithTags("Catálogo");

series.MapGet("/search", async (string q, TvMazeClient client, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        return Results.BadRequest(new { message = "Informe ao menos 2 caracteres." });
    return Results.Ok(await client.SearchAsync(q.Trim(), cancellationToken));
});

series.MapGet("/{id:int}", async (int id, TvMazeClient client, CancellationToken cancellationToken) =>
{
    var result = await client.GetByIdAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

series.MapGet("/{id:int}/episodes", async (int id, TvMazeClient client, CancellationToken cancellationToken) =>
    Results.Ok(await client.GetEpisodesAsync(id, cancellationToken)));

var mySeries = app.MapGroup("/api/my-series")
    .RequireAuthorization()
    .WithTags("Minha lista");

mySeries.MapGet("/", async (
    SeriesStatus? status,
    CurrentUserService currentUser,
    AppDbContext database,
    TvMazeClient client,
    CancellationToken cancellationToken) =>
{
    var query = database.UserSeries.Where(x => x.UserId == currentUser.UserId);
    if (status.HasValue) query = query.Where(x => x.Status == status.Value);

    var items = await query.OrderByDescending(x => x.UpdatedAtUtc)
        .ToListAsync(cancellationToken);
    foreach (var item in items.Where(x => x.IsAnime is null))
        item.IsAnime = await client.IsAnimeAsync(item.ExternalSeriesId, cancellationToken);
    if (database.ChangeTracker.HasChanges())
        await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(items.Select(ToResponse));
});

mySeries.MapPost("/", async (
    AddSeriesRequest request,
    CurrentUserService currentUser,
    AppDbContext database,
    TvMazeClient client,
    IEpisodioAssistidoService episodioAssistidoService,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Status) || request.ExternalSeriesId <= 0 || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { message = "Dados da série inválidos." });

    var existing = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == request.ExternalSeriesId,
        cancellationToken);

    if (existing is not null)
    {
        if (request.Status == SeriesStatus.Finished)
            await episodioAssistidoService.MarcarTodosComoAssistidosAsync(
                currentUser.UserId, request.ExternalSeriesId, cancellationToken);

        existing.Status = request.Status;
        existing.IsAnime ??= await client.IsAnimeAsync(request.ExternalSeriesId, cancellationToken);
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(existing));
    }

    var item = new UserSeries
    {
        UserId = currentUser.UserId,
        ExternalSeriesId = request.ExternalSeriesId,
        Name = request.Name.Trim(),
        PosterUrl = request.PosterUrl,
        Summary = request.Summary,
        Status = request.Status,
        IsAnime = await client.IsAnimeAsync(request.ExternalSeriesId, cancellationToken)
    };
    database.UserSeries.Add(item);
    if (request.Status == SeriesStatus.Finished)
        await episodioAssistidoService.MarcarTodosComoAssistidosAsync(
            currentUser.UserId, request.ExternalSeriesId, cancellationToken);

    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/my-series/{item.ExternalSeriesId}", ToResponse(item));
});

mySeries.MapPut("/{externalSeriesId:int}/status", async (
    int externalSeriesId,
    UpdateSeriesStatusRequest request,
    CurrentUserService currentUser,
    AppDbContext database,
    IEpisodioAssistidoService episodioAssistidoService,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Status))
        return Results.BadRequest(new { message = "Status de série inválido." });

    var item = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == externalSeriesId,
        cancellationToken);
    if (item is null) return Results.NotFound();

    if (request.Status == SeriesStatus.Finished)
        await episodioAssistidoService.MarcarTodosComoAssistidosAsync(
            currentUser.UserId, externalSeriesId, cancellationToken);

    item.Status = request.Status;
    item.UpdatedAtUtc = DateTime.UtcNow;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(item));
});

mySeries.MapPut("/{externalSeriesId:int}/favorite", async (
    int externalSeriesId,
    UpdateSeriesFavoriteRequest request,
    CurrentUserService currentUser,
    AppDbContext database,
    CancellationToken cancellationToken) =>
{
    var item = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == externalSeriesId,
        cancellationToken);
    if (item is null) return Results.NotFound();

    item.IsFavorite = request.IsFavorite;
    item.UpdatedAtUtc = DateTime.UtcNow;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(item));
});

mySeries.MapDelete("/{externalSeriesId:int}", async (
    int externalSeriesId,
    CurrentUserService currentUser,
    AppDbContext database,
    CancellationToken cancellationToken) =>
{
    var item = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == externalSeriesId,
        cancellationToken);
    if (item is null) return Results.NotFound();

    database.UserSeries.Remove(item);
    await database.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

app.Run();

static UserSeriesResponse ToResponse(UserSeries x) => new(
    x.Id, x.ExternalSeriesId, x.Name, x.PosterUrl, x.Summary,
    x.Status, x.AddedAtUtc, x.UpdatedAtUtc, x.IsFavorite, x.IsAnime);
