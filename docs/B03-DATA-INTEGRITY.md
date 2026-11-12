# B03-A — Integridade de dados

Data: 11/11/2026. Escopo: biblioteca, episódios, sentimentos, personagens e votação. Sem migration, reconciliação, acesso ao banco/Supabase de produção, deploy, commit ou push.

## 1. Diagnóstico e limites

Referências abaixo apontam para os arquivos após a correção; descrevem também o comportamento anterior nesses caminhos.

| Caminho | Problema identificado antes da correção | Impacto |
| --- | --- | --- |
| `backend/SeenIt.Api/Program.cs:318` | Cadastro validava apenas ID positivo, nome não vazio e status; não limitava todos os textos nem verificava existência da série | Exceções PostgreSQL por tamanho/NUL; metadados e URLs arbitrários |
| `backend/SeenIt.Api/Program.cs:268` | Busca sem máximo; rotas do catálogo sem validação uniforme de IDs; filtro de biblioteca aceitava enum numérico indefinido | Chamadas externas desnecessárias e entradas inválidas |
| `backend/SeenIt.Api/DTOs/MarcarEpisodioAssistidoRequest.cs:5` e `Services/EpisodioAssistidoService.cs:89` | DTO sem limites suficientes; serviço confiava em série, temporada, número, nome e personagem do cliente | Associações falsas e gravações rejeitadas pelo banco |
| `backend/SeenIt.Api/Services/EpisodioAssistidoService.cs:149` | `Distinct()` e fallback de null ocultavam duplicatas/null; enum não era verificado | Dados inválidos convertidos silenciosamente em coleções aparentemente válidas |
| `backend/SeenIt.Api/Services/EpisodioAssistidoService.cs:271` | Agrupamento usava ID + nome persistido pelo cliente | Fragmentação de votos, nomes arbitrários e votos em personagens inexistentes |
| `backend/SeenIt.Api/Services/TvMazeService.cs:13` | Combinava elenco principal e convidados sem verificar série/episódio | Mistura de séries e aceitação de personagens de episódios alheios |
| `backend/SeenIt.Api/Services/EpisodioAssistidoService.cs:15`, `:30`, `:89` | Salvava episódios antes da chamada externa de atualização de status | Falha externa após gravação e status inconsistente |
| `backend/SeenIt.Api/External/TvMazeClient.cs:53`, `:76` | Respostas externas sem verificação estrutural suficiente; null virava lista vazia | Erros não controlados ou catálogo indisponível interpretado como ausência de dados |

### Schema PostgreSQL no repositório

`Data/AppDbContext.cs:27`, `:42`, `:45`, migration `Migrations/20260915120902_InitialPostgres.cs:38` e snapshot `Migrations/AppDbContextModelSnapshot.cs:45` concordam:

| Coluna | Tipo/limite | Null |
| --- | --- | --- |
| UserSeries.Name | character varying(240) | não |
| UserSeries.PosterUrl | character varying(600) | sim |
| UserSeries.Summary | character varying(3000) | sim |
| EpisodiosAssistidos.NomeEpisodio | character varying(240) | não |
| EpisodiosAssistidos.PersonagemFavoritoNome | character varying(200) | não |
| IDs externos, temporada, número, status e sentimento | integer | não |

Os índices únicos são usuário+série, usuário+episódio e a chave composta episódio assistido+sentimento. Não existem FKs para o catálogo externo nem CHECKs de enums/IDs. `scripts/banco.sql` contém apenas o histórico de migrations, não um schema alternativo completo.

Esta análise confirma o schema versionado, não o schema físico implantado: não foi consultado PostgreSQL de produção. Não se deve presumir ausência de divergência operacional.

## 2. Revisão independente e correções

| Problema confirmado | Correção e referência |
| --- | --- |
| Leitura e diff de sentimentos sem proteção; status calculado sobre leituras concorrentes | `Services/SeriesMutation.cs:16`, `Services/EpisodioAssistidoService.cs:107`: transação e advisory lock antes da primeira leitura do agregado |
| Inserções concorrentes podiam provocar 500 | `Services/DataIntegrity.cs:59`: conflitos esperados retornam 409, sem detalhes SQL nem repetição automática |
| Exceções novas escapavam no tempo assistido | `Controllers/ProfileController.cs:64`: 404 para recurso removido; 503 para catálogo indisponível/malformado; entrada de fuso continua 400 |
| 404 de elenco interpretado como indisponibilidade | `Services/TvMazeService.cs:68`, `External/TvMazeClient.cs`: `CatalogNotFoundException`, invalidação dos recursos relacionados e resposta 404 |
| Consultas simultâneas duplicavam episódios/elenco | `External/CatalogCache.cs:19`: consulta compartilhada por recurso; elenco principal por série, convidados por episódio |
| Modal não tinha recuperação explícita de conflito | `frontend/src/app/shared/episodio-modal/episodio-modal.component.ts:209`: “Atualizar e revisar” faz apenas leituras, preservando escolhas |

Os DTOs públicos, entidades, migrations, configurações de autenticação/JWT/CORS e políticas B01-A não foram alterados. A interface interna de marcação completa agora recebe a coleção de episódios já verificada, evitando chamadas HTTP dentro da transação.

## 3. Concorrência e atomicidade

O agregado protegido é **usuário + série**: sua linha `UserSeries` (inclusive quando ainda não existe), os episódios assistidos daquele usuário/série e os sentimentos desses episódios. Marcação individual, marcação/desmarcação de temporada, cadastro da série, atualização de status, favorito e remoção da biblioteca adquirem a mesma chave antes de consultar o estado que irão alterar.

### Estratégia escolhida

1. Validar entrada e obter episódios/elenco do TVmaze **antes** da transação.
2. Abrir transação PostgreSQL `ReadCommitted`.
3. Adquirir `pg_try_advisory_xact_lock` com chave bigint derivada dos primeiros 64 bits de SHA-256 de `seenit:b03:{userId:D}:{seriesId}`, com formatação invariável. A chave não depende de string.GetHashCode nem do processo. Colisão de hash causaria conflito conservador, não mistura de dados.
4. Se a chave estiver ocupada, devolver 409 sem aguardar a outra transação. Não existem fila nem retry automático de escrita.
5. Ler o estado atual, aplicar o conjunto exato de sentimentos, calcular status com persistidos + alterações pendentes, executar SaveChanges e Commit. Descartar a transação em qualquer falha faz rollback.

O PostgreSQL possui o bloqueio, portanto duas instâncias da API usando conexões diferentes respeitam a mesma proteção. Usuários distintos têm chaves distintas. Todas as consultas de escrita continuam filtradas pelo usuário autenticado. Uma única chave é adquirida por transação, sem ordem variável de múltiplos locks. `lock_timeout=1s` e `statement_timeout=5s` são locais à transação para limitar esperas em outras linhas/SQL; não alteram configurações remotas. Esses tempos são por espera/comando, não um SLA global da operação.

Uma segunda operação concorrente rejeitada **não é aplicada depois da primeira**. O usuário pode ler o estado e enviar uma nova operação explicitamente. Operações que começam sua fase de banco depois de um commit leem esse novo estado. O contrato não inclui versão de formulário/ETag: não detecta edição antiga mantida aberta por muito tempo, fora de uma disputa simultânea de transações.

### Alternativas avaliadas

- **Concorrência otimista com token/xmin:** protege linhas existentes se todo escritor atualizar/verificar a versão, mas exige configurar o modelo, cobrir alterações apenas nos sentimentos, linhas ainda ausentes e o cálculo agregado de status. Um token retornado ao Angular também mudaria o protocolo de escrita. Não foi necessário nesta etapa.
- **Bloqueio de linha FOR UPDATE:** uma linha `UserSeries` pode não existir, pois a marcação sem inclusão na biblioteca já era permitida. Bloquear `Users` seria mais amplo do que necessário.
- **Serializable:** pode proteger contra write skew quando todas as transações relevantes participam, mas exige tratar falhas de serialização e abortos. O advisory lock não bloqueante fornece rejeição explícita por agregado e permite `ReadCommitted` após obter exclusividade.
- **SemaphoreSlim em memória:** não protege entre instâncias; não é usado para integridade de escrita.
- **Retry automático:** foi rejeitado para impedir sobrescrita silenciosa de uma escolha concorrente. O rollback é seguido de 409, não de execução automática sobre um novo estado.

O middleware trata `MutationConflictException`, `DbUpdateConcurrencyException` e SQLSTATE PostgreSQL `23505` (unicidade), `40001` (serialização), `40P01` (deadlock), `55P03` (lock timeout) e `57014` (limite de comando) como 409 nas rotas B03. Cancelamento do próprio cliente não é transformado em conflito. Demais erros de banco não são mascarados como validação ou falha do TVmaze.

**Não há migration necessária.** Não foi adicionado token ou coluna. SQLite usa transação em testes funcionais, mas não implementa o advisory lock e não demonstra esta garantia de concorrência.

## 4. Validações preservadas e contratos HTTP

- Textos: limites do schema acima, sem NUL/surrogate inválido, contagem por valor escalar Unicode; sem truncamento. Nome de série/episódio obrigatório. Resumo/poster opcionais aceitam null.
- Poster: HTTPS, até 600 caracteres, host exato `static.tvmaze.com` ou `static1.tvmaze.com`, porta padrão, sem credenciais, fragmento ou espaços externos. Sem download pelo backend nem conversão silenciosa de HTTP.
- IDs de série/episódio positivos; temporada/número positivos e coerentes com o catálogo; busca limitada a 200 caracteres, com mínimo de dois após Trim.
- Status InList=1, Watching=2, Finished=3. Sentimentos Feliz=1, Triste=2, Raiva=3, Comovido=4, Engracado=5: no máximo cinco, sem repetição, enum desconhecido ou null explícito. Coleção omitida mantém o default vazio existente.
- Personagem zero permanece ausência de favorito para marcação simples/lotes. Positivos precisam estar no elenco principal da série ou nos convidados do episódio validado. Nome de personagem do cliente tem limite 200 e nunca define o nome persistido ou o grupo da votação.
- JSON malformado/null/tipos errados e entradas inválidas retornam 400 antes de SaveChanges. Regras de episódio já assistido preservadas: lotes não sobrescrevem sentimentos, personagem ou data; escolha individual pode ser atualizada.
- Sucessos mantêm os formatos existentes, incluindo 201 no cadastro novo, 200 na atualização/marcação e os campos da votação/watch-time.

## 5. Catálogo indisponível, recursos removidos e cache

### Respostas

| Situação | Comportamento |
| --- | --- |
| Associação ausente numa resposta válida ou entrada inválida | 400, sem gravação |
| Detalhes de série inexistente | Consulta pública: 404. Cadastro que referencia série inexistente: 400, preservando o contrato existente |
| 404 de lista de episódios, episódio ou elenco/convidados | 404 controlado; não é indisponibilidade temporária |
| Série histórica removida durante watch-time | 404 com mensagem genérica; não culpa a entrada do usuário, não remove histórico |
| Timeout, conexão, 429, 5xx ou resposta inválida/null do TVmaze | 503 controlado; nenhuma associação é presumida válida |
| Disputa de escrita/unicidade | 409 controlado, rollback, sem retry automático |

O perfil trata erros do catálogo no controller, que conhece a diferença entre o fuso informado pelo usuário e a série recuperada do histórico. A lista de durações de seis horas já existente continua sendo utilizada; uma remoção pode ser observada apenas quando esse cache expirar. Sucesso do watch-time não mudou.

404 observado invalida recursos relacionados conhecidos: show, episódios e elenco principal por série; relação episódio/série e convidados por episódio. Invalidação também marca consultas em andamento para que uma resposta anterior não repovoe o cache depois de uma remoção observada. Não se servem respostas parciais de elenco. A próxima tentativa precisa consultar os recursos invalidados novamente. Falha temporária durante essa consulta retorna 503, nunca autorização permissiva.

Renomeações usam o nome confiável atual após expiração/invalidação. Personagem removido de um elenco válido é rejeitado em novas escolhas e excluído da votação pública. Votos antigos continuam no histórico pessoal, sem UPDATE/DELETE automático. A agregação permanece por ID, filtrando a série correta e IDs presentes no elenco validado; nomes divergentes do mesmo ID não fragmentam votos. Totais/denominadores podem mudar quando registros inválidos deixam de ser contados.

### Compartilhamento e limites

`CatalogCache` é singleton e usa MemoryCache **dedicado**, sem mudar o cache compartilhado de anime/watch-time nem políticas B01-A. Recursos: `show:{id}`, `episodes:{seriesId}`, `episode-show:{id}`, `show-cast:{seriesId}`, `guest-cast:{episodeId}`.

- Uma consulta em andamento por chave por instância. Elenco principal é reutilizado entre episódios; não é duplicado em um cache combinado por episódio.
- TTL de cinco minutos; até 512 entradas; orçamento de tamanho de 16 MiB contabilizado pelo JSON serializado, com até 2 MiB por entrada. O orçamento não representa uma medida exata do heap; estruturas gerenciadas acrescentam overhead. Respostas maiores podem ser consumidas, mas não são retidas nesse cache.
- Até 32 consultas distintas em andamento e 128 consumidores aguardando no total. Acima disso, rejeição controlada com 503, sem fila ilimitada.
- Cada consumidor pode cancelar sua espera independentemente. Cancelar o primeiro não cancela consumidores restantes. A consulta compartilhada usa seu próprio timeout de 20s; os HttpClients continuam com timeout de 15s por chamada e recebem o token compartilhado. Se todos cancelarem, trabalho já iniciado pode terminar/cachear, dentro desses limites.
- Exceções, 404 e null não são armazenados como respostas válidas. A tarefa é removida após falha e uma tentativa posterior pode consultar novamente.
- Cache e compartilhamento são locais à instância; não há deduplicação distribuída entre servidores. A integridade de banco, por outro lado, é coordenada pelo PostgreSQL.
- Não existem retries novos de HTTP ou de escrita. O 503 B03 continua com `Retry-After: 30`, uma orientação local sem promessa de recuperação nesse prazo. O controller de watch-time preserva sua resposta 503 controlada.

Referências: [TVmaze](https://www.tvmaze.com/api), [isolamento PostgreSQL](https://www.postgresql.org/docs/current/transaction-iso.html), [advisory locks](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS), [concorrência EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). A allowlist de imagens é local e pode exigir revisão se o provedor trocar de CDN.

## 6. Testes permanentes e evidência

Testes locais usam fixtures e HTTP simulado. Nenhuma chamada de teste ao TVmaze/Render/Supabase real.

| Comando | Resultado |
| --- | --- |
| `dotnet build backend/SeenIt.Api --no-restore` | Passou, sem avisos/erros da API |
| `dotnet run --project backend/SeenIt.Checks --no-restore` | Passou: watch time, anime e 72 casos B02 |
| `dotnet run --project backend/SeenIt.RegisterIntegrationChecks --no-restore` | Passou: resiliência/cache, validações B03, B02 com 73 casos/16 usuários SQLite, B01-A e regressões |
| `dotnet run --project backend/SeenIt.RegisterIntegrationChecks --no-restore -- --postgres-only` | Passou em PostgreSQL isolado |
| `npm run check:rate-limiting` no frontend | Passou, incluindo recuperação 409 sem perda do formulário ou repetição de escrita |
| `npm run build` no frontend | Passou; aviso CSS preexistente do perfil (8,39 kB para orçamento de 8 kB) |

### PostgreSQL isolado

Container local descartável `seenit-b03-postgres`, imagem `postgres:17-alpine` (servidor reportou **17.11**), publicado somente em `127.0.0.1:15432`, banco `seenit_b03`, sem volumes. Credenciais são sintéticas de teste e não pertencem a nenhuma conta real. A fixture usa conexão loopback fixa, nunca connection string do ambiente. `EnsureCreated` cria somente o schema do banco de teste; **nenhuma migration é executada**.

Reprodução, em ambiente local autorizado:

```powershell
docker run --detach --rm --name seenit-b03-postgres --publish 127.0.0.1:15432:5432 --env POSTGRES_PASSWORD=synthetic_b03_local_only --env POSTGRES_DB=seenit_b03 postgres:17-alpine
dotnet run --project backend/SeenIt.RegisterIntegrationChecks --no-restore -- --postgres-only
docker stop seenit-b03-postgres
```

`PostgresIntegrityChecks.cs` cria duas instâncias WebApplicationFactory com service providers independentes e conexões distintas ao mesmo PostgreSQL. Gates nos interceptors tornam as disputas determinísticas, sem sleep e sem depender do escalonamento do SQLite:

- Inserção simultânea do mesmo episódio: uma gravação, concorrente 409.
- Atualizações simultâneas com sentimentos diferentes: nenhum conjunto combinado; escolha perdedora não é aplicada silenciosamente. Nova tentativa explícita substitui o conjunto inteiro.
- Duas novas marcações que completam uma série: disputa retorna 409; após retry manual, ambos episódios persistem e status é Finished.
- Outro usuário marca durante o bloqueio do primeiro, sem interferência nos dados.
- Cadastro concorrente de biblioteca ainda inexistente também recebe proteção por chave.
- Violação **real** PostgreSQL de índice único durante SaveChanges retorna 409 e faz rollback dos demais efeitos.
- Falha injetada em SavedChanges, depois do SQL e antes do Commit explícito, faz rollback de exclusões, sentimentos e status. Não é alegação de teste de queda de rede durante COMMIT.
- Nome com 240 valores escalares Unicode suplementares é persistido no varchar(240) real.

`CatalogResilienceChecks.cs` cobre consulta compartilhada com dois consumidores, cancelamento independente inclusive do primeiro, retry posterior após falha, limites de consultas/consumidores/chaves/tamanho, falha parcial de elenco, 404/429/500/timeout/resposta inválida, renomeação e remoção de personagem sem reescrita histórica e falhas do watch-time. `DataIntegrityChecks.cs` preserva limites/NUL/Unicode, IDs, enums, sentimentos, associação incorreta, nomes falsificados, votos históricos por identidade, lotes e ausência de SaveChanges em rejeições.

O teste B01-A de leases usa recursos distintos para não confundir deduplicação de catálogo com ocupação das três vagas do limiter. Mantém saturação, cancelamento e recuperação dos permits; nenhuma política de produção foi alterada.

Limitação explícita: SQLite não reproduz advisory locks, SQLSTATE, isolamento, unicidade concorrente ou semântica varchar/NUL PostgreSQL. Seus testes funcionais não são evidência dessas garantias. A evidência acima vem do PostgreSQL 17.11 local; não certifica a configuração/esquema físico da produção.

Aviso preexistente: NU1903 para `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 no projeto de testes. Não foram adicionadas dependências externas. `SeenIt.Checks` agora referencia o projeto da API, substituindo cópias compiladas por links que ficavam incompletas ao mudar dependências do cliente.

## 7. Compatibilidade e ordem de publicação futura

Os contratos de sucesso e payloads Angular permanecem iguais. O frontend já trata 409 com mensagem de atualização e não repete escritas automaticamente. O modal precisou de recuperação explícita: após 409 preserva sentimentos/personagem, bloqueia novo envio e oferece “Atualizar e revisar”. O botão consulta catálogo/votação, sem escrever e sem resetar o formulário. Falha da atualização mantém a possibilidade de tentar a leitura outra vez. Só o usuário solicita uma nova gravação. Outros fluxos preservam estado e a recuperação manual existente; a marcação de temporada já relê o estado após erro.

Backend novo aceita frontend antigo, que recebe mensagem 409 genérica, mas não possui o botão novo. Para disponibilizar toda a experiência, publicar frontend compatível antes ou junto do backend, somente após revisão e autorização.

**Não misturar escritores antigos e novos.** Advisory lock é cooperativo: uma instância antiga não o adquire. Numa publicação futura, substituir/drenar todas as instâncias antigas que atendem escritas antes de considerar a proteção de concorrência ativa. Fazer verificação controlada depois da substituição, sem testes de carga públicos. Nenhuma publicação foi feita nesta etapa.

## 8. Riscos residuais e B03-B

- Cache pode manter dados anteriormente verificados por até cinco minutos. A remoção só é observada na renovação ou após um 404 que invalide recursos relacionados. Não há cache distribuído.
- Nomes maiores que o schema ou episódios null/não numerados incompatíveis com o modelo atual continuam sendo recusados, sem truncamento. Uma entrada incompatível pode bloquear a coleção inteira. O perfil agora recebe resposta controlada, em vez de 500 por essas exceções.
- O schema continua sem CHECKs de IDs/enums e sem catálogo canônico local. Escritas SQL diretas e instâncias antigas não participam dos advisory locks.
- Não há versão de formulário/ETag nem garantia de detecção de edição antiga após o término de outra transação. A proteção cobre disputas simultâneas no banco e o conjunto agregado; o usuário pode realizar uma substituição posterior deliberada.
- Falha de conexão durante COMMIT pode deixar o resultado desconhecido para o cliente. Não existe retry automático; o estado deve ser lido antes de tentar de novo.
- Payloads em andamento e overhead de objetos existem além do orçamento serializado do cache. Limites locais não substituem B01-A, que permanece intacto.
- Metadados gerais de série do cliente continuam aceitos quando válidos e dentro do schema/allowlist; identidade de episódio/personagem/voto vem do catálogo.

**B03-B permanece pendente.** Deve inventariar, com autorização própria, IDs/enums/associações históricas inválidas, nomes divergentes e recursos removidos; comparar contagens antes/depois; distinguir inconclusivos de fraudes; propor reconciliação reversível com backup e revisão. Nenhum histórico foi reconciliado, removido ou sanitizado em massa. Avaliação futura de CHECKs, tabelas canônicas ou migrations é separada. Nenhuma migration é necessária/preparada para esta solução B03-A.

Conclusão da implementação: **APROVADO PARA REVISÃO**, com as evidências e limitações acima. Não é aprovação automática para deploy.

