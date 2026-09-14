using System.Security.Claims;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<TvMazeClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TvMaze:BaseUrl"]!);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SeenIt/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não foi configurada.");

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
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

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
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
        token.Token, token.ExpiresAtUtc, new UserResponse(user.Id, user.Name, user.Email)));
});

auth.MapPost("/login", async (
    LoginRequest request,
    AppDbContext database,
    IPasswordHasher<User> hasher,
    TokenService tokens,
    CancellationToken cancellationToken) =>
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

    return Results.Ok(
        new AuthResponse(
            token.Token,
            token.ExpiresAtUtc,
            new UserResponse(
                user.Id,
                user.Name,
                user.Email
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
    CancellationToken cancellationToken) =>
{
    var query = database.UserSeries.AsNoTracking().Where(x => x.UserId == currentUser.UserId);
    if (status.HasValue) query = query.Where(x => x.Status == status.Value);

    var items = await query.OrderByDescending(x => x.UpdatedAtUtc)
        .Select(x => new UserSeriesResponse(x.Id, x.ExternalSeriesId, x.Name, x.PosterUrl,
            x.Summary, x.Status, x.AddedAtUtc, x.UpdatedAtUtc))
        .ToListAsync(cancellationToken);
    return Results.Ok(items);
});

mySeries.MapPost("/", async (
    AddSeriesRequest request,
    CurrentUserService currentUser,
    AppDbContext database,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Status) || request.ExternalSeriesId <= 0 || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { message = "Dados da série inválidos." });

    var existing = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == request.ExternalSeriesId,
        cancellationToken);

    if (existing is not null)
    {
        existing.Status = request.Status;
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
        Status = request.Status
    };
    database.UserSeries.Add(item);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/my-series/{item.ExternalSeriesId}", ToResponse(item));
});

mySeries.MapPut("/{externalSeriesId:int}/status", async (
    int externalSeriesId,
    UpdateSeriesStatusRequest request,
    CurrentUserService currentUser,
    AppDbContext database,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Status))
        return Results.BadRequest(new { message = "Status de série inválido." });

    var item = await database.UserSeries.SingleOrDefaultAsync(
        x => x.UserId == currentUser.UserId && x.ExternalSeriesId == externalSeriesId,
        cancellationToken);
    if (item is null) return Results.NotFound();

    item.Status = request.Status;
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
    x.Status, x.AddedAtUtc, x.UpdatedAtUtc);
