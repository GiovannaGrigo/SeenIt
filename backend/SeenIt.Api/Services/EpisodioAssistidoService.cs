using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.DTOs.Episodios;
using SeenIt.Api.External;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Services;

public sealed class EpisodioAssistidoService(AppDbContext dbContext, TvMazeClient tvMazeClient, ITvMazeService characters) : IEpisodioAssistidoService
{
    public async Task<IReadOnlyList<EpisodioAssistidoResponse>> DesmarcarTemporadaAsync(
        Guid userId, int externalSeriesId, int temporada, CancellationToken cancellationToken)
    {
        var catalog = await tvMazeClient.GetEpisodesAsync(externalSeriesId, cancellationToken);
        await using var transaction = await SeriesMutation.BeginAsync(dbContext, userId, externalSeriesId, cancellationToken);
        var registros = await dbContext.EpisodiosAssistidos
            .Include(x => x.Sentimentos)
            .Where(x => x.UserId == userId && x.ExternalSeriesId == externalSeriesId && x.Temporada == temporada)
            .ToListAsync(cancellationToken);

        dbContext.EpisodiosAssistidos.RemoveRange(registros);
        await AtualizarStatusSerieAsync(userId, externalSeriesId, cancellationToken, catalog);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ObterPorSerieAsync(userId, externalSeriesId, cancellationToken);
    }

    public async Task<IReadOnlyList<EpisodioAssistidoResponse>> MarcarTemporadaComoAssistidaAsync(
        Guid userId, int externalSeriesId, int temporada, CancellationToken cancellationToken)
    {
        var episodios = await tvMazeClient.GetEpisodesAsync(externalSeriesId, cancellationToken);
        await using var transaction = await SeriesMutation.BeginAsync(dbContext, userId, externalSeriesId, cancellationToken);
        var idsAssistidos = (await dbContext.EpisodiosAssistidos
            .Where(x => x.UserId == userId && x.ExternalSeriesId == externalSeriesId)
            .Select(x => x.ExternalEpisodeId)
            .ToListAsync(cancellationToken)).ToHashSet();

        DataIntegrity.Require(episodios.Any(x => x.Season == temporada), "Temporada inexistente.");
        foreach (var episodio in episodios.Where(x => x.Season == temporada))
        {
            if (!idsAssistidos.Add(episodio.Id))
                continue;

            dbContext.EpisodiosAssistidos.Add(new EpisodioAssistido
            {
                UserId = userId,
                ExternalSeriesId = externalSeriesId,
                ExternalEpisodeId = episodio.Id,
                Temporada = episodio.Season,
                NumeroEpisodio = episodio.Number,
                NomeEpisodio = episodio.Name
            });
        }

        await AtualizarStatusSerieAsync(userId, externalSeriesId, cancellationToken, episodios);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ObterPorSerieAsync(userId, externalSeriesId, cancellationToken);
    }

    public async Task PrepararTodosComoAssistidosAsync(Guid userId, int externalSeriesId, IReadOnlyCollection<SeenIt.Api.DTOs.EpisodeResponse> episodios, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("Caller must own the aggregate transaction.");
        var idsAssistidos = (await dbContext.EpisodiosAssistidos
            .Where(x => x.UserId == userId && x.ExternalSeriesId == externalSeriesId)
            .Select(x => x.ExternalEpisodeId)
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var episodio in episodios)
        {
            if (!idsAssistidos.Add(episodio.Id))
                continue;

            dbContext.EpisodiosAssistidos.Add(new EpisodioAssistido
            {
                UserId = userId,
                ExternalSeriesId = externalSeriesId,
                ExternalEpisodeId = episodio.Id,
                Temporada = episodio.Season,
                NumeroEpisodio = episodio.Number,
                NomeEpisodio = episodio.Name
            });
        }
    }

    public async Task<EpisodioAssistidoResponse> MarcarComoAssistidoAsync(
        Guid userId,
        int externalEpisodeId,
        MarcarEpisodioAssistidoRequest request,
        CancellationToken cancellationToken)
    {
        DataIntegrity.Episode(externalEpisodeId, request);
        var catalog = await tvMazeClient.GetEpisodesAsync(request.ExternalSeriesId, cancellationToken);
        var canonical = catalog.SingleOrDefault(x => x.Id == externalEpisodeId);
        DataIntegrity.Require(canonical is not null, "Episódio não pertence à série.");
        DataIntegrity.Require(canonical!.Season == request.Temporada && canonical.Number == request.NumeroEpisodio,
            "Temporada ou número de episódio incompatível.");
        string characterName = string.Empty;
        if (request.ExternalCharacterId > 0)
        {
            var cast = await characters.ObterPersonagensDoEpisodioAsync(request.ExternalSeriesId, externalEpisodeId, cancellationToken);
            var character = cast.SingleOrDefault(x => x.ExternalCharacterId == request.ExternalCharacterId);
            DataIntegrity.Require(character is not null, "Personagem não pertence à série/episódio.");
            characterName = character!.Nome;
        }
        await using var transaction = await SeriesMutation.BeginAsync(dbContext, userId, request.ExternalSeriesId, cancellationToken);
        var episodioAssistido =
            await dbContext.EpisodiosAssistidos
                .Include(x => x.Sentimentos)
                .SingleOrDefaultAsync(
                    x =>
                        x.UserId == userId &&
                        x.ExternalEpisodeId ==
                            externalEpisodeId,
                    cancellationToken);

        if (episodioAssistido is null)
        {
            episodioAssistido = new EpisodioAssistido
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExternalEpisodeId =
                    externalEpisodeId,
                ExternalSeriesId =
                    request.ExternalSeriesId,
                AssistidoEmUtc = DateTime.UtcNow
            };

            dbContext.EpisodiosAssistidos.Add(
                episodioAssistido);
        }

        episodioAssistido.ExternalSeriesId =
            request.ExternalSeriesId;

        episodioAssistido.Temporada =
            request.Temporada;

        episodioAssistido.NumeroEpisodio =
            request.NumeroEpisodio;

        episodioAssistido.NomeEpisodio =
            canonical.Name;

        episodioAssistido.ExternalCharacterId =
            request.ExternalCharacterId;

        episodioAssistido.PersonagemFavoritoNome =
            characterName;

        var sentimentosSolicitados =
            (request.Sentimentos ?? [])
                .Distinct()
                .ToHashSet();

        var sentimentosAtuais =
            episodioAssistido.Sentimentos
                .Select(x => x.Sentimento)
                .ToHashSet();

        var sentimentosParaRemover =
            episodioAssistido.Sentimentos
                .Where(x =>
                    !sentimentosSolicitados.Contains(
                        x.Sentimento))
                .ToList();

        foreach (
            var sentimento in sentimentosParaRemover
        )
        {
            episodioAssistido.Sentimentos.Remove(
                sentimento);

            dbContext.Remove(sentimento);
        }

        foreach (
            var sentimento in
            sentimentosSolicitados.Except(
                sentimentosAtuais)
        )
        {
            episodioAssistido.Sentimentos.Add(
                new EpisodioAssistidoSentimento
                {
                    Sentimento = sentimento
                });
        }

        await AtualizarStatusSerieAsync(
            userId,
            request.ExternalSeriesId,
            cancellationToken, catalog);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapearResponse(episodioAssistido);
    }

    private async Task AtualizarStatusSerieAsync(Guid userId, int externalSeriesId, CancellationToken cancellationToken, IReadOnlyCollection<SeenIt.Api.DTOs.EpisodeResponse> episodiosDaSerie)
    {
        var serie = await dbContext.UserSeries
            .SingleOrDefaultAsync(
                x =>
                    x.UserId == userId &&
                    x.ExternalSeriesId == externalSeriesId,
                cancellationToken);

        if (serie is null)
            return;

        var idsEpisodios = episodiosDaSerie
            .Select(x => x.Id)
            .ToArray();

        if (idsEpisodios.Length == 0)
            return;

        var savedIds =
            await dbContext.EpisodiosAssistidos
                .Where(x =>
                    x.UserId == userId &&
                    x.ExternalSeriesId == externalSeriesId &&
                    idsEpisodios.Contains(x.ExternalEpisodeId))
                .Select(x => x.ExternalEpisodeId)
                .Distinct()
                .ToListAsync(cancellationToken);
        var watched = savedIds.ToHashSet();
        foreach (var entry in dbContext.ChangeTracker.Entries<EpisodioAssistido>()
            .Where(e => e.Entity.UserId == userId && e.Entity.ExternalSeriesId == externalSeriesId))
        {
            if (entry.State == EntityState.Deleted) watched.Remove(entry.Entity.ExternalEpisodeId);
            else if (idsEpisodios.Contains(entry.Entity.ExternalEpisodeId)) watched.Add(entry.Entity.ExternalEpisodeId);
        }
        var totalAssistidos = watched.Count;

        var novoStatus =
            totalAssistidos == 0
                ? SeriesStatus.InList
                : totalAssistidos == idsEpisodios.Length
                ? SeriesStatus.Finished
                : SeriesStatus.Watching;

        if (serie.Status == novoStatus)
            return;

        serie.Status = novoStatus;
        serie.UpdatedAtUtc = DateTime.UtcNow;

    }

    public async Task<IReadOnlyList<EpisodioAssistidoResponse>> ObterPorSerieAsync(
        Guid userId,
        int externalSeriesId,
        CancellationToken cancellationToken)
    {
        var episodios =
            await dbContext.EpisodiosAssistidos
                .AsNoTracking()
                .Include(x => x.Sentimentos)
                .Where(x =>
                    x.UserId == userId &&
                    x.ExternalSeriesId ==
                        externalSeriesId)
                .OrderBy(x => x.Temporada)
                .ThenBy(x => x.NumeroEpisodio)
                .ToListAsync(cancellationToken);

        return episodios
            .Select(MapearResponse)
            .ToList();
    }

    public async Task<IReadOnlyList<VotacaoPersonagemResponse>> ObterVotacaoAsync(
            int externalEpisodeId,
            CancellationToken cancellationToken)
    {
        var episode = await tvMazeClient.GetEpisodeSeriesIdAsync(externalEpisodeId, cancellationToken);
        var cast = await characters.ObterPersonagensDoEpisodioAsync(episode, externalEpisodeId, cancellationToken);
        var names = cast.ToDictionary(x => x.ExternalCharacterId, x => x.Nome);
        var rows = await dbContext.EpisodiosAssistidos.AsNoTracking()
            .Where(x => x.ExternalEpisodeId == externalEpisodeId && x.ExternalSeriesId == episode && x.ExternalCharacterId > 0)
            .GroupBy(x => x.ExternalCharacterId)
            .Select(g => new { ExternalCharacterId = g.Key, TotalVotos = g.Count() }).ToListAsync(cancellationToken);
        var votos = rows.Where(x => names.ContainsKey(x.ExternalCharacterId))
            .Select(x => new { x.ExternalCharacterId, PersonagemNome = names[x.ExternalCharacterId], x.TotalVotos })
            .OrderByDescending(x => x.TotalVotos).ThenBy(x => x.PersonagemNome).ToList();

        var totalGeralVotos =
            votos.Sum(voto => voto.TotalVotos);

        if (totalGeralVotos == 0)
            return [];

        return votos
            .Select(voto => new VotacaoPersonagemResponse
            {
                ExternalCharacterId =
                    voto.ExternalCharacterId,

                PersonagemNome =
                    voto.PersonagemNome,

                TotalVotos =
                    voto.TotalVotos,

                Porcentagem = Math.Round(
                    voto.TotalVotos * 100m / totalGeralVotos,
                    2)
            })
            .ToList();
    }

    private static EpisodioAssistidoResponse MapearResponse(
        EpisodioAssistido episodio)
    {
        return new EpisodioAssistidoResponse
        {
            ExternalEpisodeId =
                episodio.ExternalEpisodeId,

            ExternalSeriesId =
                episodio.ExternalSeriesId,

            Temporada =
                episodio.Temporada,

            NumeroEpisodio =
                episodio.NumeroEpisodio,

            NomeEpisodio =
                episodio.NomeEpisodio,

            Sentimentos =
                episodio.Sentimentos
                    .Select(x => x.Sentimento)
                    .ToArray(),

            ExternalCharacterId =
                episodio.ExternalCharacterId,

            PersonagemFavoritoNome =
                episodio.PersonagemFavoritoNome,

            AssistidoEmUtc =
                episodio.AssistidoEmUtc
        };
    }
}
