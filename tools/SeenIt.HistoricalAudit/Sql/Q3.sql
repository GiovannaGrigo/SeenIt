SELECT 'H10_historico_sem_biblioteca_valido' AS categoria, count(*) AS quantidade
FROM "{{schema}}"."EpisodiosAssistidos" e
WHERE NOT EXISTS (SELECT 1 FROM "{{schema}}"."UserSeries" s
  WHERE s."UserId" = e."UserId" AND s."ExternalSeriesId" = e."ExternalSeriesId")
UNION ALL
SELECT 'H14_anime_desconhecido_valido', count(*) FROM "{{schema}}"."UserSeries"
WHERE "IsAnime" IS NULL;

WITH progresso AS (
 SELECT s."Id", s."Status", count(e."Id") AS assistidos_locais
 FROM "{{schema}}"."UserSeries" s
 LEFT JOIN "{{schema}}"."EpisodiosAssistidos" e
   ON e."UserId" = s."UserId" AND e."ExternalSeriesId" = s."ExternalSeriesId"
 GROUP BY s."Id", s."Status"
)
SELECT "Status", assistidos_locais, assistidos_locais = 0 AS sem_historico, count(*) AS vinculos
FROM progresso GROUP BY "Status", assistidos_locais
ORDER BY "Status", assistidos_locais;
