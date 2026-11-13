# SeenIt — B03-B1: planejamento da auditoria de integridade histórica

Data: 2026-11-12. Parecer: **APROVADO PARA IMPLEMENTAR AUDITORIA READ-ONLY**.

Este parecer aprova o planejamento e a implementação futura da auditoria, não sua execução em produção nem qualquer reconciliação. Nesta etapa foram lidos somente arquivos locais. Nenhum banco, serviço externo ou dado real foi acessado; SQL, migrations, testes de banco, correções e deploy não foram executados. Somente este documento foi criado. O checkout inspecionado está em `main`; a branch experimental `maintenance-phase1` não foi alterada. A pasta `.worktrees/` já estava não rastreada e não foi modificada.

## 1. Base técnica e inventário

Fontes primárias no repositório (caminhos relativos à raiz):

- `backend/SeenIt.Api/Domain/Models/*.cs` e `Domain/Enuns/SentimentoEpisodioEnum.cs`: entidades e enums.
- `backend/SeenIt.Api/Data/AppDbContext.cs`, `Migrations/AppDbContextModelSnapshot.cs` e as três migrations abaixo: schema esperado.
- `backend/SeenIt.Api/Program.cs`: rotas da biblioteca, autenticação e atualização explícita de status.
- `backend/SeenIt.Api/Services/{DataIntegrity,SeriesMutation,EpisodioAssistidoService,CurrentUserService,WatchTimeService,WatchTimeCalculator,TvMazeService}.cs`: regras e agregações.
- `backend/SeenIt.Api/Controllers/{SeriesController,EpisodiosAssistidosController}.cs`, DTOs e `External/{TvMazeClient,CatalogCache}.cs`: entrada e catálogo.
- `backend/SeenIt.RegisterIntegrationChecks/{DataIntegrityChecks,PostgresIntegrityChecks,CatalogResilienceChecks}.cs` e `docs/B03-DATA-INTEGRITY.md`: cobertura existente, consultada sem executar testes.

### Schema esperado pelo código

| Entidade / tabela | Chaves, relacionamentos e índices | Tipos, nulabilidade e limites relevantes |
| --- | --- | --- |
| `User` / `Users` | PK `Id` UUID; índice único `IX_Users_Email` sobre `Email` | `Name` varchar(120), `Email` varchar(200), `PasswordHash` text e `CreatedAtUtc` timestamptz obrigatórios; `AvatarFileName` varchar(200) nullable |
| `UserSeries` / `UserSeries` | PK `Id` UUID; único `IX_UserSeries_UserId_ExternalSeriesId`; FK `UserId → Users.Id`, delete cascade | `ExternalSeriesId`, `Status` integer; `Name` varchar(240) obrigatório; `PosterUrl` varchar(600) e `Summary` varchar(3000) nullable; `IsFavorite` boolean obrigatório; `IsAnime` boolean nullable; `AddedAtUtc`, `UpdatedAtUtc` timestamptz obrigatórios |
| `EpisodioAssistido` / `EpisodiosAssistidos` | PK `Id` UUID; único `IX_EpisodiosAssistidos_UserId_ExternalEpisodeId`; FK `UserId → Users.Id`, delete cascade | IDs externos, temporada, número e personagem integer obrigatórios; `NomeEpisodio` varchar(240), `PersonagemFavoritoNome` varchar(200) obrigatórios; `AssistidoEmUtc` timestamptz obrigatório |
| `EpisodioAssistidoSentimento` / `EpisodiosAssistidosSentimentos` | PK composta `(EpisodioAssistidoId, Sentimento)`; FK ao episódio assistido com delete cascade | ID UUID e sentimento integer obrigatórios |

Os campos acima são NOT NULL salvo indicação expressa. `UserId` é UUID obrigatório nas duas tabelas dependentes. Inicializadores C# de UUID/data/status não equivalem a defaults SQL. A migration de favorito adiciona default SQL `false`; a classificação anime aceita `null`.

Migrations existentes:

1. `20260915120902_InitialPostgres`: quatro tabelas, PKs, três FKs em cascata e três índices únicos.
2. `20261105172758_AddSeriesFavorites`: `UserSeries.IsFavorite`, obrigatório, default false.
3. `20261106114244_AddSeriesAnimeClassification`: `UserSeries.IsAnime`, nullable.

Não há CHECKs de IDs positivos, enums, datas, conteúdo não vazio, URL ou status agregado. Não há FK entre episódios assistidos e biblioteca, nem tabelas persistidas de séries/episódios/personagens canônicos. Não existe entidade de voto nem de personagem favorito: a escolha está no próprio episódio assistido; a votação é calculada sobre essas linhas. Não há índice único por posição `(usuário, série, temporada, número)`, nem por personagem. O mesmo episódio pode ser assistido por vários usuários; o mesmo personagem pode receber votos desses usuários.

As garantias físicas pressupõem schema conforme as migrations e constraints/índices ativos e válidos. A leitura do código não certifica o schema efetivamente instalado. Unicidade de e-mail é a do valor armazenado, não uma promessa de comparação normalizada/case-insensitive; esta auditoria não consulta e-mails, hashes, nomes de usuários, avatares ou credenciais.

## 2. Regras atuais e alcance do B03-A

| Regra | PostgreSQL | Aplicação / B03-A e limites |
| --- | --- | --- |
| Um vínculo de biblioteca por usuário/série | Índice único, já na migration inicial | Cadastro atualiza vínculo existente sob transação |
| Um episódio assistido por usuário/ID externo do episódio | Índice único, já na migration inicial | B03-A serializa mutações cooperativas e trata conflito 409; não criou essa unicidade |
| Sem usuário inexistente e sem sentimento com episódio inexistente | FKs em cascata | Filtros por usuário autenticado; FK não prova que o dono foi atribuído corretamente |
| Sem sentimento repetido no mesmo registro | PK composta | Coleção não null, sem repetição, no máximo cinco, valores 1–5; conjunto substituído atomicamente |
| Domínio de IDs/enums/textos | Tipos, NOT NULL e varchar; sem CHECK semântico | IDs de série/episódio > 0; temporada/número > 0; personagem >= 0; status 1–3; textos válidos por escalares Unicode, sem NUL; nomes de série/episódio não brancos |
| Poster | Só tamanho/nulabilidade | HTTPS, host exato `static.tvmaze.com` ou `static1.tvmaze.com`, porta padrão, sem userinfo, fragmento e espaços externos |
| Episódio pertence à série e posição corresponde | Não garantido | Comparação com lista validada TVmaze; nome persistido vem do catálogo |
| Personagem pertence ao contexto | Não garantido | Positivo deve pertencer à união do elenco principal da série com convidados do episódio validado; nome canônico. Não exige aparição efetiva do elenco principal em cada episódio |
| Escolha ausente | NOT NULL permite vazio | ID zero e nome vazio são válidos; lotes geram episódios sem personagem nem sentimentos |
| Atualização episódio/sentimentos/status | Transações fornecem atomicidade, mas banco não calcula status | B03-A une alterações e cálculo do estado pendente em um commit, protegido por chave usuário/série |
| Status | Apenas integer | `InList=1`, `Watching=2`, `Finished=3`; detalhes abaixo |
| Votos | Unicidade do episódio por usuário limita a uma escolha | Agrupa por ID, nunca pelo nome histórico; exige série canônica e personagem presente no elenco validado; ignora personagem zero/negativo e escolhas excluídas do catálogo |
| Isolamento de usuários | FKs não são autorização nem RLS configurada neste modelo | JWT/claims e filtros explícitos nas consultas pessoais; votação é agregado entre usuários por desenho |

### Status não é uma igualdade global com contagem

`AtualizarStatusSerieAsync` recalcula somente quando existe vínculo na biblioteca e o catálogo de episódios não é vazio. Considera apenas IDs presentes no catálogo daquela série e combina persistidos com inserções/exclusões pendentes. Zero assistidos implica InList, todos implica Finished e subconjunto não vazio implica Watching.

Entretanto, POST da biblioteca e PUT de status aceitam InList ou Watching diretamente, preservando episódios anteriores. Finished adiciona todos os episódios do catálogo disponível naquele momento, sem sobrescrever sentimentos, personagem ou data de registros já existentes. Catálogo vazio pode acompanhar Finished; o recálculo automático com catálogo vazio retorna sem mudar status. Novos episódios podem ser publicados depois da conclusão, inclusive ainda não exibidos: não existe filtro por data de exibição no cálculo atual. Portanto:

- InList com histórico, Watching sem histórico ou Watching com todos vistos são estados permitidos pelos fluxos atuais, não corrupção comprovada.
- Finished com episódios hoje faltantes é candidato a revisão, não prova de falha atômica antiga: o catálogo pode ter crescido ou mudado.
- Excluir a série da biblioteca remove somente `UserSeries`; o histórico permanece. Marcar episódios sem vínculo também é permitido. Isso não é órfão nem mistura entre usuários.
- `IsFavorite` independe de status. `IsAnime=null` representa classificação desconhecida/pendente e é válido.

### Proteções introduzidas e limites residuais

B03-A passou a validar associações canônicas, domínios de entrada e elenco; a substituir sentimentos como conjunto; a coordenar escritores por advisory lock transacional estável derivado de usuário/série; a calcular status incluindo alterações pendentes; a garantir rollback do agregado e a devolver 409 sem retry automático. Falhas temporárias de catálogo não autorizam escrita; 404 e indisponibilidade têm tratamento distinto. Nomes divergentes não dividem a votação atual e votos históricos removidos do elenco são preservados no histórico.

O lock é cooperativo: SQL direto e escritor antigo não o respeitam. Não há ETag/versão de formulário; uma escrita posterior deliberada pode substituir escolhas. O GET `/api/my-series` pode persistir `IsAnime` quando null, fora de `SeriesMutation`: não usar endpoints da aplicação como mecanismo desta auditoria. B03-A não saneia dados prévios nem fornece trilha de alterações. Os registros não guardam data de criação do sentimento, origem do status ou data da última escolha do personagem. `UpdatedAtUtc` da biblioteca também muda por favorito e não prova qual ação ocorreu; não inferir corte pré/pós-B03-A por esse campo. Uma fotografia do banco não comprova atomicidade passada, falha de isolamento ou intenção do usuário.

## 3. Hipóteses, severidade e reconciliação futura

Classes: **C** violação de constraint esperada; **D** violação do domínio atual; **L** legado/estado válido; **E** divergência externa; **M** revisão manual/inconclusivo. Severidade é do achado confirmado: crítica = identidade/isolamento comprovadamente violado; alta = histórico ou execução funcional comprometida; média = resultados/estado potencialmente incorretos; baixa = apresentação; informativa = situação válida. Hipóteses não recebem automaticamente a severidade máxima.

| ID / hipótese e regra | Classe; detecção | Risco funcional; severidade | Automação futura segura? / TVmaze / revisão humana / perda |
| --- | --- | --- | --- |
| H01 Duplicação de PK, episódio por usuário, biblioteca por usuário/série ou sentimento por episódio/valor | C; Q0/Q1. Impossível no schema válido esperado | `SingleOrDefault` falha, dupla contagem, estado ambíguo; alta | Não deduplicar arbitrariamente; TVmaze não resolve sentimentos/data/identidade; humana sim; perda alta ao mesclar/excluir |
| H02 Episódio/biblioteca sem usuário ou sentimento sem episódio | C; Q0/Q1 | Histórico inacessível ou sentimento perdido; alta | Sem dono comprovado não reassociar; TVmaze não ajuda; humana sim; perda alta |
| H03 IDs de série/episódio <= 0, temporada/número <= 0, personagem < 0, status fora 1–3 | D; Q2 | Rejeição de atualização, catálogo indisponível, UI/status inválido; alta para identidade/status, média para posição | Não inferir identidade por nome/posição; TVmaze sim para catálogo, não enum de status; humana sim; perda alta se apagar/trocar identidade. Especial/não numerado antigo pode ser E/M, não deve ser excluído por regra mais nova |
| H04 Sentimento fora 1–5 ou mais de cinco por episódio | D; Q2 | Enum não suportado e apresentação incorreta; média | Sem mapeamento histórico comprovado não converter; TVmaze não; humana sim; perda de preferência se remover |
| H05 Episódio associado à série/temporada/número incorretos | D/E/M; Q4 e Q6 | Histórico na série errada, status/watch-time/voto distorcidos; alta se associação confirmada | TVmaze sim; humana sim, especialmente remapeamento; não mover automaticamente; perda alta de contexto e risco de colisão de unicidade |
| H06 Sentimento ligado a outro episódio real | M; Q1 só detecta órfão; Q6 contextualiza pai incorreto | Preferência exibida em contexto errado; alta se confirmado | FK válida não revela intenção; TVmaze não prova dono da emoção; humana sim; perda alta. Nenhum detector SQL conclusivo para duas referências válidas trocadas |
| H07 Personagem positivo fora do elenco principal + convidados ou nome incompatível com ID; zero com nome preenchido | D/E/M; Q2/Q6/Q7 | Voto oculto hoje, favorito errado, nome antigo; média, alta se contexto incorreto confirmado | TVmaze sim para positivos; revisão sim; renomeação sozinha é baixa/E. Não zerar voto; perda alta da escolha original |
| H08 Voto repetido/inválido | C/D/E; Q1/Q6/Q7; voto é a linha assistida | Inflação antiga, denominador diferente ou voto hoje filtrado; média/alta | Não existe tabela de votação a limpar; TVmaze sim; revisão sim para qualquer descarte; perda alta |
| H09 Status diverge do derivado do catálogo atual | L/E/M; Q3/Q7 | Lista/filtros podem não representar progresso esperado; média após confirmar defeito | Não recalcular em massa: status manual e catálogo mutável; TVmaze sim e humana sim; perda da intenção manual, embora episódios preservados |
| H10 Episódio sem vínculo na biblioteca, favorito sem sentimentos ou personagem zero/nome vazio | L; Q3 e Q2 | Nenhum por si; informativa | Sem correção; TVmaze não necessário para ausência de vínculo; humana só se houver outra evidência; exclusão causaria perda alta |
| H11 Recurso externo removido/renumerado, metadados divergentes ou duração indisponível | E/M; Q6/Q7 e comparação de snapshots | Watch-time pode ficar incompleto ou retornar 404; voto filtrado; alta para falha do perfil, baixa para nomes | TVmaze sim; humana sim; não eliminar histórico por 404. Perda alta se excluir, baixa apenas para metadados com original preservado |
| H12 Textos brancos, poster fora da política atual, datas não finitas/futuras/invertidas | D ou M; Q2 | UI, carregamento de imagem, cálculos de datas; média para data não finita, baixa para apresentação | Classificar antes de alterar; TVmaze só para metadados; humana sim; perda de metadados/data se substituir. URL antiga não comprova corrupção nem ataque |
| H13 Registro pertence a outro usuário válido | M; Q1 não resolve, Q4 não prova autoria | Exposição/alteração de histórico; crítica somente com evidência independente | Não transferir por semelhança entre contas; TVmaze não; humana obrigatória e investigação de autorização separada; perda/exposição alta |
| H14 Classificação anime desconhecida ou diferente do catálogo atual | L/E; Q3 e comparação de gêneros | Filtros/classificação; informativa/baixa | Null não corrigir como erro; TVmaze sim para divergência, humana para política histórica; baixo risco em metadado com versão original |

Nenhuma hipótese autoriza correção nesta etapa. Mesmo onde um valor canônico permitiria atualização determinística de metadados, a segurança depende de política aprovada, preservação do original e catálogo versionado. Nenhuma correção automática de identidade, autoria, votos, sentimentos ou status está aprovada.

## 4. Consultas propostas — somente leitura

Os blocos Q0–Q7 são propostas para implementação e validação local; não foram executados. Assumem schema `public` conforme migrations. Se o schema real diferir, interromper e revisar, sem adaptar por DDL. Usar conexão dedicada sem credenciais da API, papel já provisionado com apenas leitura e sem poderes administrativos. O cliente não deve registrar connection string, parâmetros sensíveis ou payloads. Sem `SELECT *`, exportação de tabelas pessoais, chamadas de funções próprias/desconhecidas, HTTP via SQL, `EXPLAIN ANALYZE`, tabelas temporárias, `SELECT INTO`, CTE modificadora ou locks explícitos/advisory. As funções usadas abaixo são funções de catálogo/expressão de leitura do PostgreSQL.

Envelope para cada lote curto (BEGIN/ROLLBACK são controle de transação; todas as consultas de dados são SELECT). Usar timeouts de comando/espera no cliente ou conexão, sem alterar configuração do servidor. Não executar todos os blocos numa transação longa em ambiente compartilhado.

```sql
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SHOW transaction_read_only;
-- Executar os SELECTs aprovados do lote, sem escritas intermediárias.
ROLLBACK;
```

O `SHOW` apenas verifica a propriedade da sessão. Implementação futura deve recusar execução se não for read-only. SELECT comum usa locks normais de leitura (AccessShare); não há promessa de zero locks, mas não há lock administrativo nem `FOR UPDATE`. MVCC fornece visão consistente no lote; lotes separados podem observar estados diferentes.

### Q0 — Verificar estrutura antes de interpretar achados

Não selecionar definições arbitrárias de funções/defaults nem histórico de SQL. Comparar os metadados retornados ao inventário, incluindo colunas, ordem das chaves, cascade, unicidade, validade e ausência de predicado/expressão nos únicos esperados. Ausência de uma FK/índice não prova que há linhas inválidas: reduz a garantia. Não executar migrations para aproximar o schema.

```sql
SELECT table_name, column_name, data_type, udt_name, is_nullable,
       character_maximum_length, ordinal_position
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('Users', 'UserSeries', 'EpisodiosAssistidos',
                     'EpisodiosAssistidosSentimentos')
ORDER BY table_name, ordinal_position;

SELECT r.relname AS tabela, c.conname, c.contype, c.convalidated,
       c.condeferrable, c.condeferred, c.confdeltype,
       pg_catalog.pg_get_constraintdef(c.oid) AS definicao
FROM pg_catalog.pg_constraint c
JOIN pg_catalog.pg_class r ON r.oid = c.conrelid
JOIN pg_catalog.pg_namespace n ON n.oid = r.relnamespace
WHERE n.nspname = 'public'
  AND r.relname IN ('Users', 'UserSeries', 'EpisodiosAssistidos',
                    'EpisodiosAssistidosSentimentos')
  AND c.contype IN ('p', 'f', 'u')
ORDER BY r.relname, c.conname;

SELECT r.relname AS tabela, x.relname AS indice,
       i.indisunique, i.indisprimary, i.indisvalid, i.indisready,
       i.indpred IS NOT NULL AS parcial,
       i.indexprs IS NOT NULL AS expressao,
       pg_catalog.pg_get_indexdef(i.indexrelid) AS definicao
FROM pg_catalog.pg_index i
JOIN pg_catalog.pg_class r ON r.oid = i.indrelid
JOIN pg_catalog.pg_class x ON x.oid = i.indexrelid
JOIN pg_catalog.pg_namespace n ON n.oid = r.relnamespace
WHERE n.nspname = 'public'
  AND r.relname IN ('Users', 'UserSeries', 'EpisodiosAssistidos',
                    'EpisodiosAssistidosSentimentos')
ORDER BY r.relname, x.relname;
```

### Q1 — Duplicações e órfãos estruturais

Cada resultado é contagem agregada: duplicações contam **grupos**, órfãos contam **linhas**. Não somar essas unidades como total de registros afetados. PKs/NOT NULL restantes são aferidos em Q0; só ampliar varreduras físicas se houver drift. A unicidade de e-mail é inspecionada somente por metadados, sem ler seu conteúdo.

```sql
SELECT 'H01_episodio_grupos' AS categoria, count(*) AS quantidade
FROM (SELECT 1 FROM public."EpisodiosAssistidos"
      GROUP BY "UserId", "ExternalEpisodeId" HAVING count(*) > 1) d
UNION ALL
SELECT 'H01_biblioteca_grupos', count(*)
FROM (SELECT 1 FROM public."UserSeries"
      GROUP BY "UserId", "ExternalSeriesId" HAVING count(*) > 1) d
UNION ALL
SELECT 'H01_sentimento_grupos', count(*)
FROM (SELECT 1 FROM public."EpisodiosAssistidosSentimentos"
      GROUP BY "EpisodioAssistidoId", "Sentimento" HAVING count(*) > 1) d
UNION ALL
SELECT 'H02_episodio_sem_usuario', count(*)
FROM public."EpisodiosAssistidos" e
WHERE NOT EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = e."UserId")
UNION ALL
SELECT 'H02_biblioteca_sem_usuario', count(*)
FROM public."UserSeries" s
WHERE NOT EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = s."UserId")
UNION ALL
SELECT 'H02_sentimento_sem_episodio', count(*)
FROM public."EpisodiosAssistidosSentimentos" f
WHERE NOT EXISTS (SELECT 1 FROM public."EpisodiosAssistidos" e
                  WHERE e."Id" = f."EpisodioAssistidoId");
```

### Q2 — Domínio local e sinais de revisão

Categorias podem se sobrepor. Textos retornam apenas contagens, nunca conteúdo. O regex de branco é triagem e não reproduz exatamente `string.IsNullOrWhiteSpace` em todo Unicode; validação final deve reproduzir `DataIntegrity.Text` em memória no executor futuro, sem logar texto. A triagem de poster aceita sintaxe restrita e pode sinalizar URLs equivalentes aceitas pelo parser .NET; resultado não é violação conclusiva. Tamanho e nulabilidade são garantias de Q0; PostgreSQL textual não armazena NUL ou Unicode inválido aceito como texto UTF-8 normal.

```sql
SELECT 'H03_biblioteca_id' AS categoria, count(*) AS quantidade
FROM public."UserSeries" WHERE "ExternalSeriesId" <= 0
UNION ALL
SELECT 'H03_status', count(*) FROM public."UserSeries" WHERE "Status" NOT IN (1,2,3)
UNION ALL
SELECT 'H03_episodio_ids_posicao', count(*) FROM public."EpisodiosAssistidos"
WHERE "ExternalSeriesId" <= 0 OR "ExternalEpisodeId" <= 0
   OR "Temporada" <= 0 OR "NumeroEpisodio" <= 0
UNION ALL
SELECT 'H03_personagem_negativo', count(*) FROM public."EpisodiosAssistidos"
WHERE "ExternalCharacterId" < 0
UNION ALL
SELECT 'H04_sentimento_enum', count(*) FROM public."EpisodiosAssistidosSentimentos"
WHERE "Sentimento" NOT IN (1,2,3,4,5)
UNION ALL
SELECT 'H04_episodios_mais_cinco', count(*)
FROM (SELECT 1 FROM public."EpisodiosAssistidosSentimentos"
      GROUP BY "EpisodioAssistidoId" HAVING count(*) > 5) d
UNION ALL
SELECT 'H07_zero_com_nome', count(*) FROM public."EpisodiosAssistidos"
WHERE "ExternalCharacterId" = 0 AND "PersonagemFavoritoNome" <> ''
UNION ALL
SELECT 'H07_positivo_nome_branco', count(*) FROM public."EpisodiosAssistidos"
WHERE "ExternalCharacterId" > 0 AND "PersonagemFavoritoNome" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_serie_nome_branco', count(*) FROM public."UserSeries"
WHERE "Name" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_episodio_nome_branco', count(*) FROM public."EpisodiosAssistidos"
WHERE "NomeEpisodio" ~ '^[[:space:]]*$'
UNION ALL
SELECT 'H12_poster_triagem', count(*) FROM public."UserSeries"
WHERE "PosterUrl" IS NOT NULL AND
 "PosterUrl" !~ '^https://(static|static1)[.]tvmaze[.]com(:443)?([/?][^#[:space:]]*)?$'
UNION ALL
SELECT 'H12_data_assistido', count(*) FROM public."EpisodiosAssistidos"
WHERE NOT isfinite("AssistidoEmUtc") OR "AssistidoEmUtc" > CURRENT_TIMESTAMP
UNION ALL
SELECT 'H12_datas_biblioteca', count(*) FROM public."UserSeries"
WHERE NOT isfinite("AddedAtUtc") OR NOT isfinite("UpdatedAtUtc")
   OR "UpdatedAtUtc" < "AddedAtUtc" OR "AddedAtUtc" > CURRENT_TIMESTAMP
   OR "UpdatedAtUtc" > CURRENT_TIMESTAMP;
```

Datas futuras/invertidas exigem revisão de relógios/importação; não implicam fraude nem autorizam redefinir para agora. Datas anteriores à criação da conta podem refletir importação e não são regra rígida documentada.

### Q3 — Controles negativos e sinais fracos de status

```sql
SELECT 'H10_historico_sem_biblioteca_valido' AS categoria, count(*) AS quantidade
FROM public."EpisodiosAssistidos" e
WHERE NOT EXISTS (SELECT 1 FROM public."UserSeries" s
  WHERE s."UserId" = e."UserId" AND s."ExternalSeriesId" = e."ExternalSeriesId")
UNION ALL
SELECT 'H14_anime_desconhecido_valido', count(*) FROM public."UserSeries"
WHERE "IsAnime" IS NULL;

WITH progresso AS (
 SELECT s."Id", s."Status", count(e."Id") AS assistidos_locais
 FROM public."UserSeries" s
 LEFT JOIN public."EpisodiosAssistidos" e
   ON e."UserId" = s."UserId" AND e."ExternalSeriesId" = s."ExternalSeriesId"
 GROUP BY s."Id", s."Status"
)
SELECT "Status", assistidos_locais = 0 AS sem_historico, count(*) AS vinculos
FROM progresso GROUP BY "Status", assistidos_locais = 0
ORDER BY "Status", sem_historico;
```

Q3 não sabe o total do catálogo nem se os IDs assistidos são canônicos. Serve para distribuição e testes de falsos positivos, não para decidir status correto.

### Q4 — Divergências internas de identidade externa

```sql
SELECT 'H05_episodios_com_varias_series' AS categoria, count(*) AS grupos
FROM (SELECT 1 FROM public."EpisodiosAssistidos"
      GROUP BY "ExternalEpisodeId" HAVING count(DISTINCT "ExternalSeriesId") > 1) d
UNION ALL
SELECT 'H05_episodios_com_varias_posicoes', count(*)
FROM (SELECT 1 FROM public."EpisodiosAssistidos"
      GROUP BY "ExternalEpisodeId"
      HAVING count(DISTINCT ("Temporada", "NumeroEpisodio")) > 1) d
UNION ALL
SELECT 'H05_mesma_posicao_varios_ids', count(*)
FROM (SELECT 1 FROM public."EpisodiosAssistidos"
      GROUP BY "UserId", "ExternalSeriesId", "Temporada", "NumeroEpisodio"
      HAVING count(DISTINCT "ExternalEpisodeId") > 1) d;
```

Não há unicidade de posição no modelo. Episódios distintos, renumeração e revisões externas podem explicar Q4; não escolher o registro mais recente como verdade. Concordância entre usuários tampouco prova identidade correta.

### Q5 — Evidência individual excepcional

Somente após agregado não zero e justificativa de investigação. Exemplo limitado a IDs internos opacos de episódios com sentimentos inválidos, sem UserId, nomes, datas ou preferências textuais. UUID continua pseudônimo potencialmente vinculável; limitar acesso/retenção. O executor futuro deve impor limite máximo de 20 e evitar exposição em relatório compartilhado. Para outros achados, usar o mesmo padrão de predicado e apenas a PK estritamente necessária.

```sql
SELECT e."Id" AS registro_opaco
FROM public."EpisodiosAssistidos" e
WHERE EXISTS (SELECT 1 FROM public."EpisodiosAssistidosSentimentos" f
              WHERE f."EpisodioAssistidoId" = e."Id"
                AND f."Sentimento" NOT IN (1,2,3,4,5))
ORDER BY e."Id"
LIMIT 20;
```

### Q6 — Comparação externa em memória e parâmetros de leitura

Sem catálogo local, SELECT nas quatro tabelas não consegue provar associação canônica. A implementação futura deve usar fixture sintética inicialmente, sem rede. Uma fase externa posterior depende de escopo próprio: coletar somente IDs públicos distintos, sem UserId/dados pessoais; consultar TVmaze fora da transação; manter snapshot de resposta, instante, recurso, completude e resultado (ok, 404, indisponível ou inválido). Não persistir snapshot no banco SeenIt, criar tabelas ou buscar HTTP por SQL. Comparar IDs, posição, nomes, gêneros e elenco em memória. Respostas incompletas/temporárias nunca significam ausência; o mesmo snapshot deve alimentar status e votos do lote.

Exemplo executável de forma SQL com catálogo **inteiramente sintético**. Os arrays são parâmetros tipados fornecidos pelo cliente; não interpolar strings. `catalogo_completo` só pode ser true para resposta válida e integral. Cada série deve ter IDs únicos no snapshot. O exemplo compara somente a série sintética 101; ele não deve ser interpretado como cobertura do banco inteiro.

```sql
WITH catalogo(serie_id, episodio_id, temporada, numero) AS (
 VALUES (101, 1001, 1, 1), (101, 1002, 1, 2)
), escopo(serie_id, catalogo_completo) AS (VALUES (101, true)),
achados AS (
 SELECT e."Id", c.episodio_id IS NULL AS ausente,
        c.episodio_id IS NOT NULL AND
          (e."Temporada" <> c.temporada OR e."NumeroEpisodio" <> c.numero)
          AS posicao_divergente
 FROM public."EpisodiosAssistidos" e
 JOIN escopo s ON s.serie_id = e."ExternalSeriesId" AND s.catalogo_completo
 LEFT JOIN catalogo c ON c.serie_id = e."ExternalSeriesId"
                    AND c.episodio_id = e."ExternalEpisodeId"
)
SELECT count(*) AS registros_comparados,
       count(*) FILTER (WHERE ausente) AS ausentes_no_snapshot,
       count(*) FILTER (WHERE posicao_divergente) AS posicoes_divergentes
FROM achados;
```

Na versão parametrizada, o catálogo pode ser CTE de `unnest($1::integer[], $2::integer[], $3::integer[], $4::integer[])`; o cliente deve validar comprimentos iguais, domínio e ausência de duplicações antes da consulta. Ausência na lista de uma série exige, em fase própria, verificar identidade do episódio (`episodes/{id}?embed=show`) para distinguir série incorreta, remoção, especial fora da lista e divergência externa. Não usar somente nome como chave. Nome histórico diferente com ID correto é divergência de metadado, não voto distinto. `Summary`, poster e anime da biblioteca não são cópias canônicas obrigatórias: cadastro aceita metadados do cliente após validação.

### Q7 — Status candidato e votos excluídos no snapshot sintético

O elenco válido é a união de elenco principal e convidados por episódio; não basta consultar apenas convidados ou apenas elenco principal. O exemplo supõe elenco completo, inclusive lista vazia válida; uma falha de qualquer parte torna a comparação inconclusiva e deve excluir aquele episódio do escopo comparável.

```sql
WITH catalogo(serie_id, episodio_id) AS (VALUES (101,1001), (101,1002)),
escopo(serie_id, catalogo_completo) AS (VALUES (101,true)),
progresso AS (
 SELECT s."Id", s."Status", count(DISTINCT c.episodio_id) AS total,
        count(DISTINCT e."ExternalEpisodeId") AS vistos
 FROM public."UserSeries" s
 JOIN escopo x ON x.serie_id = s."ExternalSeriesId" AND x.catalogo_completo
 LEFT JOIN catalogo c ON c.serie_id = s."ExternalSeriesId"
 LEFT JOIN public."EpisodiosAssistidos" e
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
FROM public."EpisodiosAssistidos" e
JOIN contexto x ON x.serie_id = e."ExternalSeriesId"
               AND x.episodio_id = e."ExternalEpisodeId" AND x.elenco_completo
WHERE e."ExternalCharacterId" > 0;
```

`derivado=NULL` significa catálogo vazio, não erro de status. Divergência é distribuição para revisão conforme seção 2. Para votação completa, Q6 deve também contabilizar linhas cujo episódio/série não corresponde ao catálogo e que não entram no contexto de Q7. Relatar separadamente zero (ausência válida), negativos (domínio inválido), associação incorreta, elenco indisponível e personagem ausente. Q7 compara contagens brutas/elegíveis, não reproduz arredondamento de percentuais da API. Nomes diferentes para mesmo ID nunca criam novos votos; sem duplicações por usuário/episódio, o mesmo personagem escolhido por usuários distintos é válido.

## 5. Estratégia de testes futura em PostgreSQL 17 local

Esta seção é plano, não execução ou criação de artefatos de teste nesta etapa. Usar instância descartável PostgreSQL 17 exclusivamente loopback e banco sintético dedicado, sem volumes/dumps reais, sem carregar configuração/connection string da API. Provisionamento de schema e fixtures ficará separado da auditoria, por papel de preparação; sem migrations. O papel de auditoria já preparado recebe somente SELECT e usa transação READ ONLY. Não reutilizar o banco dos testes existentes sem isolamento explícito.

Matriz mínima para a implementação:

| Cenário sintético | Expectativa |
| --- | --- |
| Schema conforme modelo; dois usuários veem mesmo episódio e votam no mesmo personagem | Q1 zero; dois votos válidos, sem mistura de dados |
| Episódios sem biblioteca, personagem zero/nome vazio, nenhum sentimento; IsAnime null | Controles válidos Q3; nenhuma recomendação de exclusão |
| Status manual InList com vistos, Watching sem vistos, Watching com todos vistos | Distribuições Q3/Q7 sinalizam contexto, sem classificar como violação definitiva |
| Finished com catálogo vazio; catálogo ganha episódio após conclusão | NULL derivado no vazio; divergência externa/manual após expansão, sem correção automática |
| Valores de enum desconhecidos, IDs não positivos, personagem negativo, textos brancos | Categorias Q2 exatas, com sobreposição documentada |
| Seis sentimentos distintos incluindo desconhecido | Q2 detecta >5 e enum; cinco válidos e zero sentimentos passam |
| Episódio apontando série/posição errada; IDs distintos na mesma posição | Q4 e Q6 distinguem suspeita interna de divergência canônica |
| Personagem do elenco principal, convidado, removido, nome antigo e zero com nome | Q7 aceita principal/convidado, conta removido separadamente; nome antigo não duplica voto |
| Episódio removido, renumerado, especial, 404, 429, timeout, resposta parcial/inválida | Divergência ou inconclusivo; nenhuma ausência inferida de falha temporária |
| Datas futuras/invertidas e timestamptz infinito | Q2 classifica revisão; nenhuma normalização implícita |
| Unicode suplementar no limite e poster aceito por parser mas sinalizado pelo regex | Sem erro de tamanho por UTF-16; regex permanece apenas triagem |
| Alteração concorrente entre SELECTs de um lote | Snapshot consistente em REPEATABLE READ; lotes separados identificados |
| Evidência individual | Até 20 UUIDs; nenhum dado pessoal/segredo/texto livre no relatório/log |

Duplicações e órfãos não podem ser inseridos no schema correto. Testá-los somente em fixture estrutural alternativa isolada, preparada previamente sem as respectivas garantias, nunca removendo constraints do schema-alvo ou usando `session_replication_role`. Q0 deve detectar a diferença e o executor deve marcar os resultados como schema não conforme. Para testar PK/NOT NULL divergentes, expandir a fixture e a checagem especificamente; não declarar cobertura que Q1 não possui.

Verificação de segurança futura: auditar a lista de comandos permitidos, rejeitar DDL/DML e funções desconhecidas, conferir READ ONLY e papel restrito; provar rejeição de escrita em teste isolado separado do lote auditado. Testar execução repetida com mesmo dataset/snapshot e contagens idênticas. Comparar estado sintético antes/depois usando leitura pelo harness, sem depender só de contagem (UPDATE pode manter total). Nenhuma tarefa de auditoria pode chamar GET que atualiza anime ou serviços de mutação. Testes existentes do B03-A são evidência histórica do código, não validação deste SQL; execução sintática e assertivas de resultados são pendências obrigatórias da implementação.

## 6. Plano futuro de execução somente leitura

1. Implementar executor/consultas apenas após revisão deste plano, começando por fixtures sintéticas e PostgreSQL 17 local. Produção continua fora do escopo autorizado.
2. Validar destino loopback, banco dedicado e ausência de segredos reais; separar credenciais de preparação das de auditoria. Não imprimir conexões. Verificar read-only e schema Q0 antes dos lotes.
3. Validar SQL e contagens esperadas da matriz. Medir custo local com escala sintética representativa; timeout/cancelamento no cliente. Não criar índices nem usar locks para acelerar auditoria.
4. Rodar Q1/Q2/Q3/Q4 agregados em lotes curtos, registrar versão do código, versão PostgreSQL, início/fim, isolamento, schema conferido, categoria, unidade, total avaliado, quantidade afetada e cobertura. Timeout é resultado incompleto, nunca zero.
5. Investigar primeiro C e D de identidade/enum; se houver drift físico, suspender conclusões de integridade garantida e revisar schema, sem repará-lo. Contagens por categoria não são somáveis; produzir união de PKs em memória apenas se necessário para total distinto, sem expor IDs pessoais.
6. Só abrir evidência Q5 se necessária. Guardar relatório agregado; evidência restrita, limitada e com prazo de descarte definido antes de coleta. Não registrar valores de preferências associados a pessoas.
7. Validar Q6/Q7 contra snapshots sintéticos completos. Comparação com TVmaze real e qualquer leitura de ambiente real exigem etapa de escopo e acesso separados; não são autorizadas por este parecer. Manter rede fora da transação SQL e parâmetros de IDs públicos sem dados pessoais.
8. Relatar cada categoria como confirmado, válido, divergência externa, inconclusivo ou revisão manual; indicar denominadores/cobertura e limitações, sem inferir intenção ou autoria por similaridade.
9. Se um acesso real vier a ser autorizado em outra etapa, planejar janela/custo e lotes limitados após validação local; não prometer ausência de impacto em Supabase/Render Free. Não disparar auditoria pelos endpoints, deploy, jobs, migrations ou ações de reconciliação.

## 7. Critérios para decidir se reconciliação é necessária

- C confirmado: requer investigação prioritária do schema e das evidências. Não basta relatar zero se índices/FKs esperados estiverem ausentes. Correção de estrutura e de dados são decisões futuras separadas.
- D confirmado com impacto funcional: propor plano específico, restrito aos registros afetados, com regra explícita, evidência de identidade, preservação das escolhas e validação humana quando houver ambiguidade.
- L: não reconciliar; documentar o caso válido e preservá-lo como teste de falso positivo.
- E: confirmar snapshot completo, persistência da divergência e impacto. Renomeação/remoção atual não prova invalidade original; preservar histórico pode ser a solução correta.
- M/inconclusivo: não corrigir até obter evidência suficiente; ausência de logs/catálogo histórico pode tornar a decisão irredutivelmente humana.
- Zero achados só permite afirmar “nenhum problema detectado nas regras e escopo comparados”, após cobertura completa. Não prova ausência de troca entre usuários válidos, intenção de sentimentos ou falhas atômicas passadas.

Qualquer futura proposta de reconciliação deve apresentar simulação sem escrita, quantidade e campos afetados, critérios de exclusão, proteção contra concorrência e colisão, preservação/backup autorizado, reversibilidade, revisão e comparação antes/depois por categoria. Backup, exportação e correção não fazem parte desta etapa. Não usar descarte de votos/sentimentos nem redefinição de status como padrão automático.

## 8. Riscos e limitações

O repositório descreve o schema esperado, não a configuração física de produção, RLS, privilégios, collations, triggers ou escritores externos. Nenhuma conclusão sobre esses elementos foi obtida. Q0 não é auditoria completa de segurança do PostgreSQL. Constraints existentes tornam várias hipóteses estruturalmente impossíveis em condições normais; não atribuir sua prevenção ao B03-A se já estavam na migration inicial.

Sem catálogo canônico versionado e trilha de mudanças não há prova de que um valor era inválido quando gravado. TVmaze pode mudar identidade contextual, nomes, elenco, numeração e quantidade de episódios; ausência de especial na lista consumida pela aplicação não comprova inexistência. Cache B03 pode reter catálogo por cinco minutos; anime usa cache de 24 horas e duração/watch-time de seis horas. Comparações em instantes distintos podem discordar sem corrupção.

Advisory locks não certificam dados antigos; READ ONLY não torna consulta barata. Varreduras e GROUP BY podem consumir CPU/I/O e transações prolongadas mantêm snapshots MVCC. Priorizar lotes curtos, cancelamento e cobertura explícita. Evidência opaca ainda pode ser vinculável; limitar circulação e não consultar e-mails, credenciais ou dados de perfil para resolver autoria.

Não há alegação de SQL validado em execução nem de banco auditado. O próximo marco exige comprovar sintaxe PostgreSQL 17, contagens e ausência de efeitos no ambiente sintético antes de considerar qualquer execução externa.

## 9. Parecer e prioridades

**APROVADO PARA IMPLEMENTAR AUDITORIA READ-ONLY**.

Prioridade inicial: confirmar garantias estruturais; investigar IDs/status/sentimentos fora do domínio; verificar episódio ↔ série/posição e personagem ↔ elenco e seu efeito sobre votação/watch-time. Diferenças de status precisam ser comparadas ao catálogo sem invalidar escolhas manuais. Episódios sem biblioteca, personagem ausente, sentimentos vazios, metadados antigos e anime desconhecido devem permanecer como controles de legado válido. Nenhuma inconsistência em dados reais foi constatada nesta análise e nenhuma correção está autorizada.
