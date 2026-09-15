using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.DTOs.Episodios;
using SeenIt.Api.External;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Services;

public sealed class EpisodioAssistidoService(AppDbContext dbContext, TvMazeClient tvMazeClient) : IEpisodioAssistidoService
{
    public async Task<EpisodioAssistidoResponse> MarcarComoAssistidoAsync(
        Guid userId,
        int externalEpisodeId,
        MarcarEpisodioAssistidoRequest request,
        CancellationToken cancellationToken)
    {
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
            request.NomeEpisodio.Trim();

        episodioAssistido.ExternalCharacterId =
            request.ExternalCharacterId;

        episodioAssistido.PersonagemFavoritoNome =
            request.PersonagemFavoritoNome.Trim();

        var sentimentosSolicitados =
            request.Sentimentos
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

        await dbContext.SaveChangesAsync(cancellationToken);

        await AtualizarStatusSerieAsync(
            userId,
            request.ExternalSeriesId,
            cancellationToken);

        return MapearResponse(episodioAssistido);
    }

    private async Task AtualizarStatusSerieAsync(Guid userId, int externalSeriesId, CancellationToken cancellationToken)
    {
        var serie = await dbContext.UserSeries
            .SingleOrDefaultAsync(
                x =>
                    x.UserId == userId &&
                    x.ExternalSeriesId == externalSeriesId,
                cancellationToken);

        if (serie is null)
            return;

        var episodiosDaSerie =
            await tvMazeClient.GetEpisodesAsync(
                externalSeriesId,
                cancellationToken);

        var idsEpisodios = episodiosDaSerie
            .Select(x => x.Id)
            .ToArray();

        if (idsEpisodios.Length == 0)
            return;

        var totalAssistidos =
            await dbContext.EpisodiosAssistidos
                .Where(x =>
                    x.UserId == userId &&
                    x.ExternalSeriesId == externalSeriesId &&
                    idsEpisodios.Contains(x.ExternalEpisodeId))
                .Select(x => x.ExternalEpisodeId)
                .Distinct()
                .CountAsync(cancellationToken);

        var novoStatus =
            totalAssistidos == idsEpisodios.Length
                ? SeriesStatus.Finished
                : SeriesStatus.Watching;

        if (serie.Status == novoStatus)
            return;

        serie.Status = novoStatus;
        serie.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);
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
        var votos = await dbContext.EpisodiosAssistidos
            .AsNoTracking()
            .Where(episodio =>
                episodio.ExternalEpisodeId == externalEpisodeId)
            .GroupBy(episodio => new
            {
                episodio.ExternalCharacterId,
                episodio.PersonagemFavoritoNome
            })
            .Select(grupo => new
            {
                grupo.Key.ExternalCharacterId,
                PersonagemNome =
                    grupo.Key.PersonagemFavoritoNome,
                TotalVotos = grupo.Count()
            })
            .OrderByDescending(resultado =>
                resultado.TotalVotos)
            .ThenBy(resultado =>
                resultado.PersonagemNome)
            .ToListAsync(cancellationToken);

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