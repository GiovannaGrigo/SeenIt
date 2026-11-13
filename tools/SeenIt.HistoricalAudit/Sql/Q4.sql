SELECT 'H05_episodios_com_varias_series' AS categoria, count(*) AS grupos
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidos"
      GROUP BY "ExternalEpisodeId" HAVING count(DISTINCT "ExternalSeriesId") > 1) d
UNION ALL
SELECT 'H05_episodios_com_varias_posicoes', count(*)
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidos"
      GROUP BY "ExternalEpisodeId"
      HAVING count(DISTINCT ("Temporada", "NumeroEpisodio")) > 1) d
UNION ALL
SELECT 'H05_mesma_posicao_varios_ids', count(*)
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidos"
      GROUP BY "UserId", "ExternalSeriesId", "Temporada", "NumeroEpisodio"
      HAVING count(DISTINCT "ExternalEpisodeId") > 1) d;
