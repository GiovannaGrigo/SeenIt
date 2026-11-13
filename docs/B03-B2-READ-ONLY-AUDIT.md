# SeenIt — B03-B2: auditoria histórica READ-ONLY local

Data: 2026-11-12. Decisão: **APROVADO PARA REVISÃO**.

Evolução B03-B2.1: consultar [preparação segura do modo remoto](B03-B21-REMOTE-AUDIT.md). O LocalTarget permanece restrito ao ambiente sintético; o executor compartilhado agora tem Q0 reforçada, QFinal e novo contrato de relatório. As contagens/formatos abaixo e o JSON B03-B2 são evidência histórica da versão anterior, preservada; os relatórios B03-B21 documentam a validação atual. Nenhuma evolução autoriza acesso ao Supabase real.

Implementação e validação locais do [planejamento B03-B1](B03-B-HISTORICAL-INTEGRITY-PLAN.md). Nenhum acesso a Supabase, TVmaze, Render ou Vercel. Não houve alteração de aplicação, entidades, migrations, frontend ou endpoints, nem reconciliação, commit, push ou deploy. A branch inspecionada permaneceu `main`; `.worktrees/`, preexistente, não foi acessada ou alterada. O documento B03-B1 preexistente foi preservado.

## Arquitetura

`tools/SeenIt.HistoricalAudit` é um console .NET 10 independente, com Npgsql 10.0.3 e SQL embutido no assembly. Não referencia a API, não carrega appsettings, User Secrets ou variáveis de conexão e não contém HttpClient. Não recebe SQL, connection string, usuário, senha, database ou schema pela CLI. O único destino é o banco sintético `seenit_b03b2`, com papel fixo `b03b2_reader` e senha de fixture; host deve ser o literal `127.0.0.1` ou `::1`, sem DNS, URLs, múltiplos hosts ou sockets. Porta padrão 15433, configurável entre 1024 e 65535. O container do procedimento é publicado somente em IPv4 loopback.

Cada lote abre uma conexão sem pooling, inicia `REPEATABLE READ READ ONLY`, confirma `SHOW transaction_read_only`, verifica a versão PostgreSQL 17 e as permissões restritas do papel, executa Q0 e só então lê os dados do lote. Termina com ROLLBACK; falhas também provocam rollback/fechamento. Q0 é repetida em cada lote para conferir a estrutura no mesmo snapshot dos dados. Divergência interrompe a CLI, sem adaptar ou reparar o schema.

SQL é selecionado por allowlist interna Q0–Q5; não há mecanismo de execução de SQL fornecido pelo operador. O schema alternativo e o modo de diagnóstico de drift são internos, acessíveis ao projeto de checks via `InternalsVisibleTo`, sem opção de CLI. A CLI normal só audita `public`. Q6/Q7 existem apenas no assembly de checks e usam VALUES sintéticos.

`tools/SeenIt.HistoricalAudit.Checks` é outro processo. Somente ele tem credencial administrativa sintética e prepara os schemas/fixtures; usa DDL direto de fixture, sem migrations e sem inicializar a API. A preparação recusa um banco já ocupado ou papel já existente. Nenhuma limpeza/substituição automática de bancos existentes. Os testes de falhas e concorrência podem escrever ou bloquear apenas suas fixtures locais; essas operações são do harness de teste, nunca do executor de auditoria.

## Arquivos

| Caminho | Responsabilidade |
| --- | --- |
| `tools/SeenIt.HistoricalAudit/SeenIt.HistoricalAudit.csproj` | Console independente e SQL como recursos embutidos |
| `tools/SeenIt.HistoricalAudit/Program.cs` | CLI restrita, cancelamento, JSON e códigos de saída |
| `tools/SeenIt.HistoricalAudit/LocalTarget.cs` | Destino local e credenciais exclusivamente sintéticas |
| `tools/SeenIt.HistoricalAudit/AuditRunner.cs` | Lotes READ ONLY, preflight, consultas e falhas sanitizadas |
| `tools/SeenIt.HistoricalAudit/SchemaContract.cs` | Contrato estático das três migrations, comparado como multiconjunto |
| `tools/SeenIt.HistoricalAudit/Report.cs` | Agregados, metadados de cobertura, categorias e limitações |
| `tools/SeenIt.HistoricalAudit/Sql/Q0.sql` até `Q5.sql` | Consultas fixas, sem DML/DDL |
| `tools/SeenIt.HistoricalAudit.Checks/SeenIt.HistoricalAudit.Checks.csproj` e `Program.cs` | Processo separado de preparação e verificação |
| `tools/SeenIt.HistoricalAudit.Checks/Schema.sql`, `Valid.sql`, `Invalid.sql` | Estrutura e dados sintéticos |
| `tools/SeenIt.HistoricalAudit.Checks/Q6.sql`, `Q7.sql` | Comparação com catálogo e elenco sintéticos completos |
| `tools/historical-audit/Start-Local.ps1`, `Stop-Local.ps1` | Container exclusivo; parada verifica nome/label e usa ID exato |
| `docs/B03-B2-LOCAL-AUDIT-REPORT.json` | Evidência agregada gerada pelos checks |
| Este documento | Procedimento, resultados e limitações |

Todos são novos arquivos. Bin/obj são artefatos locais ignorados pelo Git. Nenhum arquivo de produção foi modificado.

## Consultas e interpretação

| Consulta | Cobertura implementada | Classificação |
| --- | --- | --- |
| Q0 | Quatro tabelas ordinárias persistentes; 29 colunas com tipo/nulabilidade/tamanho/identity/generated/collation; sete constraints PK/FK; sete índices; default false do favorito | Drift estrutural, interrupção para revisão |
| Q0 adicional | Ordem das chaves, alvo/schema das FKs, cascade e opções update/match, validação/deferrabilidade; unicidade, validade/ready, ausência de predicados/expressões/INCLUDE, btree e opclasses padrão; rejeita RLS, triggers de usuário, rules e partições não esperadas | Não transforma estrutura desconhecida em garantia |
| Q1 | Duplicações de episódio por usuário, vínculo por usuário/série e sentimento por episódio/valor; três tipos de órfãos | C, alta se confirmado; duplicações em grupos, órfãos em linhas |
| Q2 | IDs/posição, status, personagem negativo, sentimentos fora 1–5 ou >5, ausência/nome de personagem incoerentes, nomes brancos, poster e datas | D para domínios inequívocos; M para triagens/contexto; sem correção |
| Q3 | Histórico sem biblioteca, anime null, distribuição de status por quantidade local assistida e indicador de histórico vazio | L, informativa; não gera regra de corrupção por status |
| Q4 | Mesmo ID de episódio em séries/posições diferentes; vários IDs na mesma posição por usuário/série | M, grupos suspeitos; depende de revisão/catálogo |
| Q5 opcional | UUID de registros com sentimento inválido; somente após agregado não zero, razão codificada e LIMIT 20 | Evidência excepcional, desabilitada por padrão |
| Q6/Q7 checks | Catálogo completo, remoção/renumeração, progresso candidato, elenco principal + convidados, voto fora do elenco e incompletude simulada | E/M ou controles L; nunca consulta externa real |

O Q3 inclui a quantidade local de episódios na distribuição, além do booleano previsto no planejamento. Não usa quantidade local para inferir total canônico ou status obrigatório. Q0 compara estrutura e não exige os nomes textuais gerados de constraints ou a ordem física das colunas. Não lê conteúdo de `Users` além de `Id`; o papel recebe SELECT apenas nessa coluna. Unicidade de e-mail é conferida por metadados, sem consultar endereços.

## Segurança e limites operacionais

- Papel sem superuser, CREATEDB, CREATEROLE, REPLICATION, BYPASSRLS ou herança/membership; sem CREATE/TEMP de banco, CREATE no schema ou privilégios de escrita/ownership nas relações de usuário. O preflight também detecta concessões de escrita por coluna. A preparação revoga privilégios de PUBLIC no banco e schema.
- `default_transaction_read_only=on` no papel, além de READ ONLY explícito por lote. Mesmo uma sessão READ WRITE não consegue UPDATE com esse papel.
- `search_path=pg_catalog` e referências qualificadas a tabelas; Q0 exige tipos e collations esperados. Sem funções próprias, SQL livre, HTTP via SQL ou calls à aplicação.
- Conexão: 3 s; statement timeout e command timeout: 5 s; lock timeout: 1 s; idle-in-transaction: 10 s; deadline de lote: 12 s; deadline da CLI: 120 s. Ctrl+C cancela. Cancelamento/timeout deixam lote incompleto, contagens null e próximos lotes não executados. Rollback/fechamento faz a limpeza.
- Metadados limitados a 512 linhas e resultados a 256 linhas por result set; Q5 limitado a 20 UUIDs. Os agregados fazem varreduras, mas têm limites de tempo; nenhum índice é criado para acelerá-los.
- JSON contém contagens, classes, unidades, severidades e metadados operacionais. Não imprime mensagens brutas de exceção, SQL, argumentos, connection strings ou parâmetros. Falhas têm códigos fixos. Evidência opcional exige reason code, não texto livre que possa conter dados pessoais.
- SELECT normal usa AccessShare e snapshot MVCC. Cada lote tem snapshot próprio; não se declara snapshot global entre Q1–Q4.

## Execução reproduzível

Pré-requisitos: Docker Desktop ativo, SDK .NET 10 e dependência Npgsql disponível para restore. Executar da raiz do repositório, em ambiente local autorizado. Senhas abaixo são somente de fixture, públicas por desenho, e nunca devem ser reutilizadas para contas reais.

```powershell
# Cria um container novo. Recusa nome já ocupado; não reutiliza outros containers.
& ./tools/historical-audit/Start-Local.ps1

# Esperar o banco estar pronto. Este comando é local, sem credenciais reais.
docker exec seenit-b03b2-local pg_isready -U postgres -d seenit_b03b2

dotnet restore tools/SeenIt.HistoricalAudit.Checks
dotnet build tools/SeenIt.HistoricalAudit.Checks --no-restore

# Processo de preparação: somente num banco vazio dedicado.
dotnet run --project tools/SeenIt.HistoricalAudit.Checks --no-build -- --prepare-only

# Processo de validação: produz docs/B03-B2-LOCAL-AUDIT-REPORT.json.
dotnet run --project tools/SeenIt.HistoricalAudit.Checks --no-build -- --verify-only

# Processo de auditoria: apenas consultas READ ONLY, agregado JSON no stdout.
dotnet run --project tools/SeenIt.HistoricalAudit --no-build

# Opcional: evidências pseudônimas limitadas, para investigação justificada.
dotnet run --project tools/SeenIt.HistoricalAudit --no-build -- --evidence=invalid-feelings --justification=investigate-invalid-feelings

# Verifica propriedade e para somente este container; --rm remove o container sem volumes.
& ./tools/historical-audit/Stop-Local.ps1
```

Conferir `$LASTEXITCODE` após cada comando .NET/Docker antes de continuar. Restore/build/checks devem retornar 0. CLI retorna 0 quando todos os lotes terminaram, inclusive quando há achados; retorna 2 quando a auditoria foi rejeitada, incompleta ou falhou. Não confundir sucesso operacional com ausência de achados. Não redirecionar saídas de Q5 para relatórios compartilhados; UUID é pseudônimo vinculável, com acesso restrito e descarte após a investigação. O relatório padrão e o relatório de checks não contêm Q5 individual.

O container se chama `seenit-b03b2-local`, label `seenit.audit=b03b2`, imagem `postgres:17-alpine`, porta `127.0.0.1:15433`, sem volumes. O banco é exclusivo do B03-B2. Uma nova reprodução completa exige parar esse container próprio e iniciar outro; não executar preparação sobre um banco existente. `--verify-only` pode ser repetido no mesmo dataset. Não há argumentos de database/credencial/configuração para apontar ao Supabase.

## Validação realizada

PostgreSQL reportou **17.11**. Build dos dois consoles passou sem avisos/erros. A suíte local passou com **43 assertivas**. O [relatório JSON](B03-B2-LOCAL-AUDIT-REPORT.json) registra versão, duração, cobertura por lote, estrutura, resultados, checks e consultas não executadas com motivos.

A CLI padrão também foi executada separadamente: exit code 0, cinco lotes completos, estrutura validada e READ ONLY confirmado. Solicitações de URL externa ou Q5 sem justificativa retornaram exit code 2. Ao concluir, o container de teste foi identificado por nome/label/ID e encerrado; `--rm` o removeu, sem tocar em outros containers.

No schema principal sintético foram avaliados 2 usuários (somente contagem/ID), 6 vínculos, 8 episódios assistidos e 6 sentimentos. Q0 validou 48 objetos/assinaturas: 4 tabelas, 29 colunas, 7 constraints, 7 índices e 1 default. Categorias podem se sobrepor e usam unidades diferentes; não somar seus achados.

| Resultado principal | Quantidade esperada e observada |
| --- | --- |
| Q1 duplicações/órfãos no schema principal | Zero em todas as seis categorias |
| Q2 ID de biblioteca, status, ID/posição de episódio, personagem negativo | 1 em cada categoria |
| Q2 sentimento inválido e episódio com >5 sentimentos | 1 em cada categoria |
| Q2 zero com nome e positivo com nome branco | 1 em cada categoria |
| Q2 nome branco de série, episódio e poster suspeito | 1 em cada categoria |
| Q2 data assistida suspeita | 2 |
| Q2 datas de biblioteca suspeitas | 1 |
| Q3 histórico sem biblioteca / anime desconhecido | 2 / 1; estados válidos, não corrupção |
| Q4 várias séries / várias posições / posição com múltiplos IDs | 1 / 2 / 1 grupos |
| Q6 catálogo sintético: comparados / ausentes / posição divergente | 5 / 1 / 1 |
| Q7 escolhas positivas comparadas / elegíveis / fora do elenco | 4 / 3 / 1 |

Também foram comprovados:

- Schema totalmente válido com zero achados C/D/M nas consultas locais, preservando estados manuais e controles L.
- Fixture isolada com constraints ausentes: a CLI para em Q0. O caminho interno de checks, rotulado não conforme, verifica 2 grupos duplicados de episódios, 2 de biblioteca, 1 de sentimento e 1 órfão em cada uma das três categorias. Schema principal não foi enfraquecido.
- Drift de tabela ausente, tamanho/tipo, nulabilidade e índice parcial adicional. As variantes são transacionais no schema alternativo, com rollback.
- READ ONLY confirmado em todos os lotes; UPDATE recusado com SQLSTATE 25006; UPDATE em READ WRITE recusado por permissão 42501; DDL recusado; SELECT de e-mail recusado por privilégio.
- Hosts externos/URLs/sockets/multi-host e SQL livre rejeitados antes de execução. Papel com concessão UPDATE acidental é recusado pelo preflight; a concessão de teste é revogada em finally.
- Timeout real do servidor, cancelamento de SELECT, falha/timeout de conexão em listener loopback controlado e timeout de lock no executor real. Resultados incompletos não se tornam zero; lotes posteriores são omitidos.
- Snapshot REPEATABLE READ mantém resultado após commit concorrente do harness; nova transação observa o commit. A alteração de teste é restaurada.
- Duas execuções retornam os mesmos counts, distribuição e status estrutural, excluindo timestamps/durações da comparação.
- Fingerprints completos das linhas sintéticas e metadados de relações/constraints iguais antes/depois das auditorias e após restauração das intervenções de teste. Não é só comparação de quantidade de linhas.
- Q5 desligada por padrão e UUIDs limitados; fixture de 25 candidatos confirma LIMIT 20.
- Catálogo/elenco incompletos simulados excluídos da comparação; ausência não inferida de falha. Principal e convidados são aceitos; nome histórico diferente não fragmenta voto.
- Canary strings de nomes, títulos, e-mails, hashes e senhas sintéticos ausentes do JSON compartilhado.

## Falsos positivos evitados

Episódios sem biblioteca, zero/nome vazio, sentimentos vazios ou cinco válidos e anime null são válidos. InList com episódios e Watching com nenhum/todos os episódios não são corrupção. Finished com catálogo vazio ou aumentado só gera contexto de revisão nos checks; não prova falha atômica passada. Um personagem do elenco principal não precisa aparecer nos convidados de cada episódio. Votos de usuários diferentes para o mesmo personagem são válidos. Nome antigo com ID correto não implica identidade diferente. Múltiplos IDs na mesma posição continuam suspeita, não regra de unicidade do banco.

## Riscos residuais e escopo não executado

Não há auditoria real nem conclusão sobre dados publicados. Q5 não integra a execução padrão. Q6/Q7 não fazem parte do executor normal e nenhum catálogo real foi consumido; indisponibilidade é simulada por flags de completude, sem teste de transporte HTTP. SQL textual de nomes/URL é triagem, não equivalência completa a `DataIntegrity.Text/Image`; revisões Unicode/URI finais continuam pendentes se um achado concreto justificar. Datas suspeitas não autorizam normalização.

Não se detecta troca de dono entre dois usuários válidos, emoção ligada ao episódio errado com FKs válidas ou intenção histórica de status. Não há catálogo histórico versionado ou trilha de mutações. Q0 é conferência conservadora do contrato local; não é auditoria completa de segurança, de configuração PostgreSQL, de todas as opções de índice ou de collation do cluster.

Loopback reduz o alcance de rede, mas não prova sozinho que uma porta não é um túnel. O procedimento exige container dedicado, rótulo e credenciais sintéticas; não configurar túneis para ambientes reais. As senhas de fixture não oferecem autenticação segura fora deste ambiente descartável. Privilégios/estrutura podem mudar entre lotes; cada lote repete preflight/Q0. Grandes datasets podem atingir timeout e ficam inconclusivos. O teste local não mede custo em Supabase/Render Free.

Nenhuma reconciliação ou exclusão foi implementada. Qualquer futura execução em dados reais exige autorização e escopo separados. **A aprovação local não autoriza executar a auditoria no Supabase de produção.**
