WITH catalogo(serie_id, episodio_id, temporada, numero) AS (
 VALUES (101, 1001, 1, 1), (101, 1002, 1, 2)
), escopo(serie_id, catalogo_completo) AS (VALUES (101, true)),
achados AS (
 SELECT e."Id", c.episodio_id IS NULL AS ausente,
        c.episodio_id IS NOT NULL AND
          (e."Temporada" <> c.temporada OR e."NumeroEpisodio" <> c.numero)
          AS posicao_divergente
 FROM "{{schema}}"."EpisodiosAssistidos" e
 JOIN escopo s ON s.serie_id = e."ExternalSeriesId" AND s.catalogo_completo
 LEFT JOIN catalogo c ON c.serie_id = e."ExternalSeriesId"
                    AND c.episodio_id = e."ExternalEpisodeId"
)
SELECT count(*) AS registros_comparados,
       count(*) FILTER (WHERE ausente) AS ausentes_no_snapshot,
       count(*) FILTER (WHERE posicao_divergente) AS posicoes_divergentes
FROM achados;
