SELECT e."Id" AS registro_opaco
FROM "{{schema}}"."EpisodiosAssistidos" e
WHERE EXISTS (SELECT 1 FROM "{{schema}}"."EpisodiosAssistidosSentimentos" f
              WHERE f."EpisodioAssistidoId" = e."Id"
                AND f."Sentimento" NOT IN (1,2,3,4,5))
ORDER BY e."Id"
LIMIT 20;
