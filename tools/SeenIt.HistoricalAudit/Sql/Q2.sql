SELECT 'H03_biblioteca_id' AS categoria, count(*) AS quantidade
FROM "{{schema}}"."UserSeries" WHERE "ExternalSeriesId" <= 0
UNION ALL
SELECT 'H03_status', count(*) FROM "{{schema}}"."UserSeries" WHERE "Status" NOT IN (1,2,3)
UNION ALL
SELECT 'H03_episodio_ids_posicao', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE "ExternalSeriesId" <= 0 OR "ExternalEpisodeId" <= 0
   OR "Temporada" <= 0 OR "NumeroEpisodio" <= 0
UNION ALL
SELECT 'H03_personagem_negativo', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE "ExternalCharacterId" < 0
UNION ALL
SELECT 'H04_sentimento_enum', count(*) FROM "{{schema}}"."EpisodiosAssistidosSentimentos"
WHERE "Sentimento" NOT IN (1,2,3,4,5)
UNION ALL
SELECT 'H04_episodios_mais_cinco', count(*)
FROM (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidosSentimentos"
      GROUP BY "EpisodioAssistidoId" HAVING count(*) > 5) d
UNION ALL
SELECT 'H07_zero_com_nome', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE "ExternalCharacterId" = 0 AND "PersonagemFavoritoNome" <> ''
UNION ALL
SELECT 'H07_positivo_nome_branco', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE "ExternalCharacterId" > 0 AND "PersonagemFavoritoNome" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_serie_nome_branco', count(*) FROM "{{schema}}"."UserSeries"
WHERE "Name" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_episodio_nome_branco', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE "NomeEpisodio" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_poster_triagem', count(*) FROM "{{schema}}"."UserSeries"
WHERE "PosterUrl" IS NOT NULL AND
 "PosterUrl" !~ '^https://(static|static1)[.]tvmaze[.]com(:443)?([/?][^#[:space:]]*)?$'
UNION ALL
SELECT 'H12_data_assistido', count(*) FROM "{{schema}}"."EpisodiosAssistidos"
WHERE NOT isfinite("AssistidoEmUtc") OR "AssistidoEmUtc" > CURRENT_TIMESTAMP
UNION ALL
SELECT 'H12_datas_biblioteca', count(*) FROM "{{schema}}"."UserSeries"
WHERE NOT isfinite("AddedAtUtc") OR NOT isfinite("UpdatedAtUtc")
   OR "UpdatedAtUtc" < "AddedAtUtc" OR "AddedAtUtc" > CURRENT_TIMESTAMP
   OR "UpdatedAtUtc" > CURRENT_TIMESTAMP;
