WITH catalogo(serie_id, episodio_id) AS (VALUES (101,1001), (101,1002)),
escopo(serie_id, catalogo_completo) AS (VALUES (101,true)),
progresso AS (
 SELECT s."Id", s."Status", count(DISTINCT c.episodio_id) AS total,
        count(DISTINCT e."ExternalEpisodeId") AS vistos
 FROM "{{schema}}"."UserSeries" s
 JOIN escopo x ON x.serie_id = s."ExternalSeriesId" AND x.catalogo_completo
 LEFT JOIN catalogo c ON c.serie_id = s."ExternalSeriesId"
 LEFT JOIN "{{schema}}"."EpisodiosAssistidos" e
   ON e."UserId" = s."UserId" AND e."ExternalSeriesId" = c.serie_id
  AND e."ExternalEpisodeId" = c.episodio_id
 GROUP BY s."Id", s."Status"
), esperado AS (
 SELECT *, CASE WHEN total = 0 THEN NULL WHEN vistos = 0 THEN 1
                WHEN vistos = total THEN 3 ELSE 2 END AS derivado
 FROM progresso
)
SELECT "Status", derivado, count(*) AS vinculos
FROM esperado GROUP BY "Status", derivado ORDER BY "Status", derivado;

WITH contexto(serie_id, episodio_id, elenco_completo) AS (
 VALUES (101,1001,true), (101,1002,true)
), elenco(serie_id, episodio_id, personagem_id) AS (
 VALUES (101,1001,7001), (101,1002,7001), (101,1002,7002)
)
SELECT count(*) AS escolhas_positivas_comparadas,
       count(*) FILTER (WHERE EXISTS (
         SELECT 1 FROM elenco c WHERE c.serie_id = e."ExternalSeriesId"
           AND c.episodio_id = e."ExternalEpisodeId"
           AND c.personagem_id = e."ExternalCharacterId")) AS votos_elegiveis,
       count(*) FILTER (WHERE NOT EXISTS (
         SELECT 1 FROM elenco c WHERE c.serie_id = e."ExternalSeriesId"
           AND c.episodio_id = e."ExternalEpisodeId"
           AND c.personagem_id = e."ExternalCharacterId")) AS fora_do_elenco_atual
FROM "{{schema}}"."EpisodiosAssistidos" e
JOIN contexto x ON x.serie_id = e."ExternalSeriesId"
               AND x.episodio_id = e."ExternalEpisodeId" AND x.elenco_completo
WHERE e."ExternalCharacterId" > 0;
