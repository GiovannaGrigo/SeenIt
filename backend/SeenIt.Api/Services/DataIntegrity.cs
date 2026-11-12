using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeenIt.Api.DTOs;
using SeenIt.Api.DTOs.Episodios;

namespace SeenIt.Api.Services;

public sealed class IntegrityException(string message) : Exception(message);
public sealed class CatalogNotFoundException : Exception;
public sealed class CatalogException(string message) : Exception(message);

public static class DataIntegrity
{
    // PostgreSQL varchar counts Unicode scalar values, not UTF-16 code units.
    public static bool Text(string? value, int limit, bool required = false)
    {
        if (value is null) return !required;
        if (required && string.IsNullOrWhiteSpace(value)) return false;
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\0' || char.IsLowSurrogate(c)) return false;
            if (char.IsHighSurrogate(c) && (++i >= value.Length || !char.IsLowSurrogate(value[i]))) return false;
            if (++count > limit) return false;
        }
        return true;
    }

    public static bool Image(string? value) => value is null ||
        (Text(value, 600) && value == value.Trim() &&
         Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
         uri.IsDefaultPort && uri.UserInfo == "" && uri.Fragment == "" &&
         (uri.Host == "static.tvmaze.com" || uri.Host == "static1.tvmaze.com"));

    public static void Require(bool condition, string message = "Dados informados inválidos.")
    {
        if (!condition) throw new IntegrityException(message);
    }

    public static void Series(AddSeriesRequest r) => Require(r.ExternalSeriesId > 0 &&
        Enum.IsDefined(r.Status) && Text(r.Name, 240, true) && Text(r.Summary, 3000) && Image(r.PosterUrl));

    public static void Episode(int id, MarcarEpisodioAssistidoRequest r) => Require(id > 0 &&
        r.ExternalSeriesId > 0 && r.Temporada > 0 && r.NumeroEpisodio > 0 &&
        Text(r.NomeEpisodio, 240, true) && Text(r.PersonagemFavoritoNome, 200) &&
        r.ExternalCharacterId >= 0 && r.Sentimentos is not null &&
        r.Sentimentos.Count <= Enum.GetValues<SentimentoEpisodio>().Length &&
        r.Sentimentos.All(x => Enum.IsDefined(x)) && r.Sentimentos.Distinct().Count() == r.Sentimentos.Count);
}

public sealed class IntegrityMiddleware(RequestDelegate next)
{
    private static bool IsB03(HttpContext c) => c.Request.Path.StartsWithSegments("/api/series") || c.Request.Path.StartsWithSegments("/api/my-series") || c.Request.Path.StartsWithSegments("/api/episodios");

    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception error) when (IsB03(context) && !context.RequestAborted.IsCancellationRequested && (error is MutationConflictException or DbUpdateConcurrencyException ||
            error.GetBaseException() is PostgresException pg && pg.SqlState is "23505" or "40001" or "40P01" or "55P03" or "57014"))
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(new { message = "Os dados foram alterados ou estão em uso. Atualize o estado e tente novamente." });
        }
        catch (CatalogNotFoundException)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { message = "Recurso não encontrado no catálogo." });
        }
        catch (IntegrityException e)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = e.Message });
        }
        catch (Exception e) when ((context.Request.Path.StartsWithSegments("/api/series") ||
            context.Request.Path.StartsWithSegments("/api/my-series") || context.Request.Path.StartsWithSegments("/api/episodios")) &&
            (e is CatalogException or HttpRequestException or System.Text.Json.JsonException ||
            e is OperationCanceledException && !context.RequestAborted.IsCancellationRequested))
        {
            context.Response.StatusCode = 503;
            context.Response.Headers.RetryAfter = "30";
            await context.Response.WriteAsJsonAsync(new { message = "Catálogo temporariamente indisponível. Tente novamente mais tarde." });
        }
    }
}
