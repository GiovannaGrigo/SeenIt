SELECT 'H01_episodio_grupos' AS categoria, count(*) AS quantidade
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidos"
      GROUP BY "UserId", "ExternalEpisodeId" HAVING count(*) > 1) d
UNION ALL
SELECT 'H01_biblioteca_grupos', count(*)
FROM (SELECT 1 FROM "{{schema}}"."UserSeries"
      GROUP BY "UserId", "ExternalSeriesId" HAVING count(*) > 1) d
UNION ALL
SELECT 'H01_sentimento_grupos', count(*)
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidosSentimentos"
      GROUP BY "EpisodioAssistidoId", "Sentimento" HAVING count(*) > 1) d
UNION ALL
SELECT 'H02_episodio_sem_usuario', count(*)
FROM "{{schema}}"."EpisodiosAssistidos" e
WHERE NOT EXISTS (SELECT 1 FROM "{{schema}}"."Users" u WHERE u."Id" = e."UserId")
UNION ALL
SELECT 'H02_biblioteca_sem_usuario', count(*)
FROM "{{schema}}"."UserSeries" s
WHERE NOT EXISTS (SELECT 1 FROM "{{schema}}"."Users" u WHERE u."Id" = s."UserId")
UNION ALL
SELECT 'H02_sentimento_sem_episodio', count(*)
FROM "{{schema}}"."EpisodiosAssistidosSentimentos" f
WHERE NOT EXISTS (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidos" e
                  WHERE e."Id" = f."EpisodioAssistidoId");
