# SeenIt — B03-B2.1: preparação segura da auditoria remota

Data: 2026-11-12. Decisão: **APROVADO PARA REVISÃO**.

Implementação validada exclusivamente contra PostgreSQL 17.11 local, com dados, identidades e certificados sintéticos. Não houve conexão ao Supabase, criação de roles reais, consumo de TVmaze, migration, reconciliação, deploy, commit ou push. API, frontend, `LocalTarget.cs` e o worktree `maintenance-phase1` foram preservados. A branch permaneceu `main`; alterações preexistentes foram mantidas.

## Arquitetura e arquivos

| Projeto/arquivo | Alteração |
| --- | --- |
| `tools/SeenIt.HistoricalAudit/LocalTarget.cs` | Intacto: loopback, database/credenciais sintéticos, sem destino genérico |
| `tools/SeenIt.HistoricalAudit/IAuditTarget.cs` | Novo contrato interno de conexão/verificação; não recebe SQL |
| `tools/SeenIt.HistoricalAudit/AuditRunner.cs` | Compartilha somente consultas fixas, lotes READ ONLY e tratamento de falhas; acrescenta QFinal, tempos de SQL/metadata/denominadores, rollback confirmado e ordenação determinística |
| `tools/SeenIt.HistoricalAudit/Report.cs` | Semântica inicial/final/completude explícita; declaração separada de garantias e evidências |
| `tools/SeenIt.HistoricalAudit/Sql/Q0.sql`, `SchemaContract.cs` | Triggers internos de FK, collation/opclasses e configuração dos índices |
| `tools/SeenIt.HistoricalAudit/SeenIt.HistoricalAudit.csproj` | Acesso interno controlado aos novos projetos |
| `tools/SeenIt.HistoricalAudit.Remote/` | Novo console separado: configuração restrita, TLS VerifyFull e preflight de identidade/privilégios em SQL fixo embutido |
| `tools/SeenIt.HistoricalAudit.RemoteChecks/` | Novo harness exclusivo de preparação/testes/volume local; certificados gerados com .NET, sem instalar CA no trust store global |
| `tools/SeenIt.HistoricalAudit.Checks/Program.cs` | Conserva a suíte anterior; grava relatório de regressão B03-B2.1 separado do histórico B03-B2 |
| `tools/historical-audit/{Start-TlsLocal.ps1,Stop-TlsLocal.ps1,tls-start.sh,.gitignore}` | Containers exclusivos válido/expirado; certificados e segredo sintéticos ignorados pelo Git |
| `docs/B03-B21-LOCAL-REGRESSION-REPORT.json` | Evidência das 43 assertivas anteriores com o executor atualizado |
| `docs/B03-B21-TLS-AUDIT-REPORT.json` | Evidência da suíte de 47 assertivas TLS/identidade/privilégios/estrutura/falhas |
| `docs/B03-B21-VOLUME-AUDIT-REPORT.json` | Volume, tempos por lote/consulta, repetibilidade e fingerprints |
| `docs/B03-B2-READ-ONLY-AUDIT.md` | Nota apontando para a evolução B03-B2.1; relatório B03-B2 original permanece histórico |

O console remoto referencia o núcleo de consultas, não o projeto da API. Não há HttpClient, endpoints, migrations ou SQL livre. Q6/Q7 continuam somente no projeto de checks original, com catálogo sintético. Q5 permanece opcional e limitada a 20 UUIDs no console local; nesta versão o console remoto não oferece Q5 e o núcleo rejeita tentativas internas de habilitá-la para um destino remoto.

## Contrato remoto e conexão

O executável remoto exige `--enable-remote` e `--configuration=<arquivo>`. Sem `--execute`, apenas valida o formato da configuração e a existência do arquivo de CA: **não tenta conexão, não valida o handshake e não precisa de senha**. Não há execução automática ou seleção implícita de destino.

A configuração contém exatamente estas propriedades; propriedades desconhecidas, inclusive `TrustServerCertificate`, são rejeitadas:

```json
{
  "Host": "database-audit.example.invalid",
  "Port": 5432,
  "Database": "postgres",
  "ConnectionUser": "seenit_audit_reader",
  "ExpectedSessionUser": "seenit_audit_reader",
  "ExpectedCurrentUser": "seenit_audit_reader",
  "RootCertificate": "C:/audit/project-ca.crt",
  "ExclusiveAuditIdentity": true
}
```

Esse é um exemplo documental não operacional, sem segredo nem endpoint real. O formato aceita hostname DNS único ou IPv4, sem URL, múltiplos hosts ou socket; IPv6 literal não é suportado pelo contrato desta versão. Porta, database e identidades são explícitos. O nome da identidade precisa começar com `seenit_audit_`; o wire username pode ser o mesmo papel ou `<papel>.<project-ref>` para um futuro pooler. `ExpectedSessionUser` e `ExpectedCurrentUser` precisam coincidir; depois da conexão são comparados a `session_user`, `current_user` e `current_database()` no servidor. Seria bloqueada uma sessão que mudasse de papel ou apontasse a outro banco.

`ExclusiveAuditIdentity=true` registra uma condição de provisionamento obrigatória; não é prova criptográfica de que ninguém reutiliza a conta. Cabe ao provisionamento independente assegurar exclusividade e não usar identidade/segredo da API, proprietário, `postgres`, `service_role` ou administrador.

A senha vem exclusivamente de `SEENIT_AUDIT_REMOTE_PASSWORD`, deve ter pelo menos 24 caracteres e não consta do JSON, código remoto, argumentos ou relatório. A CLI remove variáveis PG de conexão/certificado do próprio processo antes de conectar, para evitar defaults implícitos da configuração da aplicação. Remove também a variável dedicada após lê-la; isso não altera variáveis do processo chamador. Usar processo operacional limpo e gerenciador de segredos com acesso restrito; uma variável não elimina riscos de inspeção de memória/ambiente por administradores do host.

A conexão é construída internamente com `SSL Mode=VerifyFull`, CA explícita e `GSS Encryption Mode=Disable`. Não há callback permissivo, Trust Server Certificate ou opção de downgrade. Um Open bem-sucedido nessa configuração constitui a confirmação TLS fornecida pelo Npgsql; testes negativos verificam certificado expirado, CA incorreta e hostname incorreto. `TlsVerified` não é uma inspeção independente do tráfego. Não há acesso a configurações de produção da API.

Cada lote usa uma única conexão, sem pooling, paralelismo no cliente ou retry automático, em `REPEATABLE READ READ ONLY`. Confirma READ ONLY antes dos SELECTs de preflight. Além da identidade, o remoto confere isolamento, search_path, timeouts e `session_replication_role=origin`. Fail closed para ausência de qualquer requisito. Otimização paralela interna do PostgreSQL não constitui múltiplas conexões do executor.

## Privilégios efetivos e cobertura da prova

`Privileges.sql` contém 12 verificações agregadas. Os helpers de privilégios PostgreSQL consideram grants diretos, efetivos por herança e PUBLIC; a checagem de papéis também considera memberships potencialmente utilizáveis por SET ROLE, mesmo sem herança automática. Nenhum grant/revoke é executado pelo console de auditoria.

Allowlist de leitura de dados:

| Tabela | Colunas permitidas |
| --- | --- |
| `Users` | Somente `Id` |
| `UserSeries` | `Id`, `UserId`, `ExternalSeriesId`, `Name`, `PosterUrl`, `Status`, `IsAnime`, `AddedAtUtc`, `UpdatedAtUtc` |
| `EpisodiosAssistidos` | As dez colunas atuais usadas nas consultas |
| `EpisodiosAssistidosSentimentos` | `EpisodioAssistidoId`, `Sentimento` |

São necessários CONNECT no banco explicitado e USAGE em `public`. Não são necessários SELECT em dados pessoais de Users, Summary/IsFavorite, auth/storage, outras tabelas, sequências, views intermediárias ou rotinas próprias.

Bloqueiam a auditoria:

- Atributos superuser, CREATEDB, CREATEROLE, REPLICATION, BYPASSRLS; memberships administrativas, pg_read_all_data/pg_write_all_data/pg_monitor e ADMIN OPTION.
- CREATE/TEMP no banco, ownership do banco e CONNECT efetivo a outro banco conectável. O perfil é deliberadamente conservador, inclusive para permissões não exercidas pelas consultas fixas.
- CREATE/ownership em schemas; ownership ou privilégios INSERT/UPDATE/DELETE/TRUNCATE/REFERENCES/TRIGGER/**MAINTAIN** em relações, inclusive grants por coluna.
- SELECT fora da allowlist, SELECT WITH GRANT OPTION ou falta de qualquer coluna necessária. Um grant de tabela inteira que exponha colunas adicionais também é bloqueado.
- Acesso/ownership de sequências, large objects, foreign servers/wrappers, tablespaces ou ownership de tipos; privilégios administrativos explícitos registrados em `pg_parameter_acl`.
- EXECUTE efetivo ou ownership de qualquer rotina própria, não apenas SECURITY DEFINER. Rotinas próprias inseridas em pg_catalog também são incluídas usando o limite de OID normal do PostgreSQL 17. Nenhuma delas é chamada para verificar seu efeito.

A enumeração usa catálogos/ACLs e funções internas de consulta, sem pg_authid, hashes de autenticação ou SELECT de valores pessoais. Se um catálogo/função de inspeção não estiver acessível ou a quantidade esperada de verificações não puder ser confirmada, retorna `privilege_coverage_inconclusive` e bloqueia antes dos dados. Falha de conexão, timeout e cancelamento também tornam a auditoria incompleta, sem inventar zero.

**Limites explícitos:** essa é uma prova do perfil catalogado no banco conectado e do acesso a outros bancos, não uma certificação universal de todas as ações SQL possíveis ou de um cluster confiável. Funções internas padrão do PostgreSQL continuam disponíveis; segurança depende também das consultas fixas, sem entrada SQL do operador. Não se afirma sandbox de SQL arbitrário. Não se enumera o conteúdo de outros bancos: qualquer CONNECT a eles bloqueia o perfil. Privilégios podem mudar depois da fotografia; são verificados novamente por lote, sem locks administrativos.

Em um Supabase compartilhado, PUBLIC pode conceder TEMP, CONNECT e EXECUTE em funções próprias/extensões. A identidade pode, portanto, ser legitimamente **bloqueada** por este perfil. Revogar diretamente da conta não elimina grants de PUBLIC. Não se aplica `REVOKE ... FROM PUBLIC` ao ambiente compartilhado para forçar aprovação. Qualquer alternativa precisa de revisão e autorização próprias; não há ampliação automática de privilégios nem relaxamento de Q0.

## Q0 reforçada e relatório

Q0 agora compara 60 assinaturas no schema sintético conforme: 4 tabelas, 29 colunas, 7 constraints, 7 índices, 12 triggers internos de FK e 1 default. Além dos atributos anteriores, verifica função interna esperada de cada trigger FK, evento, habilitação `O` e deferrabilidade. Índices incluem collation por chave, vetor de opções, immediate/live/checkxmin e opclasses exatas. Nome da collation da coluna e collation do índice são comparados separadamente. Uma diferença produz drift, nunca DDL de reparo.

O relatório novo diferencia:

| Campo | Significado |
| --- | --- |
| `SchemaValidatedInitially` | Q0 inicial; null se não foi possível verificar |
| `SchemaConformantAtEnd` | QFinal independente ao término; true só se executado com sucesso. Falha que impede confirmação final deixa null; drift confirmado deixa false |
| `AuditComplete` | Todos os lotes previstos concluídos |
| `CompletedBatches` | Quantidade de lotes concluídos |
| `OmittedBatches` | Lotes não iniciados após falha |
| `InterruptedQueries` | Lotes iniciados que não concluíram; não afirma que seu SQL chegou ao servidor |
| `ReadOnlyVerified` | Confirmação READ ONLY de todos os lotes tentados, não sinônimo de auditoria completa |
| `ExecutorDeclaration` | Declaração do código; não prova independente de ausência de escrita |
| `DatabaseGuarantees` / flags por lote | Identidade, privilégios, TLS, READ ONLY e ACK de rollback |
| `IndependentTestEvidence` | Null no executor; evidência independente fica no envelope do harness local |

QFinal reutiliza a consulta fixa Q0 em outro lote curto. Cada lote também verifica Q0 antes dos dados; não existe snapshot global entre lotes. Os aliases antigos Complete/SchemaValidated/AllBatchesReadOnly existem apenas internamente para os checks e não são serializados no novo JSON.

Rollback tem deadline próprio de 5 s; falha de limpeza é separada em `CleanupFailure` sem mascarar a causa original. ACK não confirmado impede sucesso. Na falha do lote, contagens/findings/distribuição/evidência ficam null e próximos lotes são omitidos. Limites de conexão 3 s, comando/statement 5 s, lock 1 s, idle-in-transaction 10 s, lote 12 s e CLI 120 s foram preservados. Metadados/result sets continuam limitados. Não há truncamento silencioso para produzir sucesso.

Categorias são ordenadas explicitamente antes da serialização. O teste de volume revelou que a ordem de UNION ALL pode mudar conforme o plano paralelo; os valores eram corretos, mas o relatório não era determinístico. A ordenação foi corrigida no executor sem modificar as regras ou reduzir o dataset. Severidades/controles continuam conforme B03-B1: status manual e histórico sem biblioteca não viram corrupção.

## Infraestrutura sintética e reprodução

Pré-requisitos: SDK .NET 10, Npgsql 10.0.3 para restore, Docker e acesso funcional ao provedor criptográfico do sistema. No sandbox Windows desta sessão, o Schannel retornou SEC_E_NO_CREDENTIALS; as validações TLS passaram fora do sandbox com aprovação, sem reduzir a validação de certificados.

Containers exclusivos:

- `seenit-b03b2-local`: regressão original, porta loopback 15433.
- `seenit-b03b21-tls`: TLS válido, porta loopback 15434.
- `seenit-b03b21-expired`: certificado expirado, porta loopback 15435.

Imagem `postgres:17-alpine`, servidor observado 17.11. TLS usa CA de teste e SAN DNS `localhost`, sem SAN IP: conectar por 127.0.0.1 é o teste negativo de hostname. CA e senha são geradas localmente; chave da CA não é salva. Certificados/chaves do servidor e segredo sintético ficam em `.synthetic-tls/`, ignorada pelo Git. O certificado válido expira em dez dias; nenhum certificado real é usado/instalado. Não há volumes de dados nomeados; mounts de certificados/scripts são read-only e dados descartáveis são removidos com os containers `--rm`.

Da raiz do repositório, **somente no ambiente sintético**:

```powershell
dotnet restore tools/SeenIt.HistoricalAudit.RemoteChecks
dotnet build tools/SeenIt.HistoricalAudit.RemoteChecks --no-restore
dotnet build tools/SeenIt.HistoricalAudit.Checks

& ./tools/historical-audit/Start-Local.ps1
docker exec seenit-b03b2-local pg_isready -U postgres -d seenit_b03b2
dotnet run --project tools/SeenIt.HistoricalAudit.Checks --no-build -- --prepare-only
dotnet run --project tools/SeenIt.HistoricalAudit.Checks --no-build -- --verify-only

dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --certificates
& ./tools/historical-audit/Start-TlsLocal.ps1
docker exec seenit-b03b21-tls pg_isready -U postgres -d seenit_b03b21
docker exec seenit-b03b21-expired pg_isready -U postgres -d seenit_b03b21

dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --prepare-only
dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --verify-only
dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --volume

& ./tools/historical-audit/Stop-TlsLocal.ps1
& ./tools/historical-audit/Stop-Local.ps1
```

Esperar pg_isready retornar 0 e conferir `$LASTEXITCODE` após cada comando. Os scripts Start recusam nomes ocupados; Stop confere nome/label e usa o ID exato. Não reutilizam/removem containers de outros projetos. Preparação recusa banco ocupado. A geração de certificados recusa diretório não vazio; para outro ciclo, tratar manualmente somente os artefatos sintéticos desse diretório após parar os containers, sem sobrescrever certificados existentes.

`--prepare-only` e `--volume` são ações administrativas do harness, nunca do executor remoto. O primeiro cria schema/roles sintéticos separados do auditor e grants mínimos. Revogações de PUBLIC nesse harness atingem exclusivamente os containers novos e descartáveis; esse código não deve ser executado no Supabase. Variantes estruturais de FK/índices usam schema alternativo com transação/rollback, sem enfraquecer o schema principal. Volume pode ser repetido no dataset sintético de tamanho conhecido, sem inserir novamente nem apagar dados.

Para demonstrar a CLI remota **somente contra o alvo TLS local gerado**, antes de parar o container:

```powershell
$configPath = 'tools/historical-audit/.synthetic-tls/remote.local.json'
$syntheticConfig = Get-Content $configPath -Raw | ConvertFrom-Json
if ($syntheticConfig.Host -ne 'localhost' -or $syntheticConfig.Port -ne 15434 -or $syntheticConfig.Database -ne 'seenit_b03b21') {
    throw 'Este procedimento aceita somente o alvo sintético local.'
}
# Validação estrutural, sem conexão:
dotnet run --project tools/SeenIt.HistoricalAudit.Remote --no-build -- --enable-remote --configuration=$configPath
try {
    $env:SEENIT_AUDIT_REMOTE_PASSWORD = [IO.File]::ReadAllText((Join-Path $PWD 'tools/historical-audit/.synthetic-tls/reader.secret'))
    dotnet run --project tools/SeenIt.HistoricalAudit.Remote --no-build -- --enable-remote --execute --configuration=$configPath
} finally {
    Remove-Item Env:\SEENIT_AUDIT_REMOTE_PASSWORD -ErrorAction SilentlyContinue
}
```

Sem `--enable-remote`, execução recusada. Sem `--execute`, não conecta. Com execução explícita, exit code 0 significa auditoria completa, não zero achados; 2 significa rejeição/incompletude. Senhas, SQL livre e parâmetros de TLS não são aceitos pela CLI. O arquivo de configuração local não deve ser substituído por uma configuração de produção neste procedimento.

## Evidência e testes

- **43 assertivas de regressão local** preservadas, incluindo Q5, Q6/Q7 sintéticos, leituras sem efeitos, concorrência/snapshot e recusa de escrita.
- **47 assertivas TLS**: certificado válido, hostname incorreto, CA incorreta/malformada, certificado expirado, credencial incorreta, configuração sem execução, identidade efetiva, privilégios mínimos/excessivos, SELECT por tabela/coluna/PUBLIC, herança segura/excessiva, ADMIN OPTION, BYPASSRLS/pg_monitor, MAINTAIN/TEMP, outro banco, sequências, schemas, EXECUTE próprio/SECURITY DEFINER inclusive pg_catalog, catálogo inacessível, FK trigger/collation/opções de índice, timeout, cancelamento prévio/ativo e rollback.
- Fingerprints das linhas completas e assinaturas estruturais comparados antes/depois; serialização das assinaturas usa propriedades explícitas, não campos de ValueTuple omitidos pelo JSON. Intervenções do harness são revertidas antes de comparar. Essa evidência não depende apenas de COUNT.
- Relatórios compartilhados não incluem segredo, nomes/e-mails/canários sintéticos nem UUID de Q5. Falhas são códigos sanitizados, sem mensagem bruta do servidor ou connection string.
- Relatórios separados preservam a evidência histórica B03-B2 e distinguem execução padrão, regressão e medição de volume.

A CLI remota também foi executada separadamente, exclusivamente contra localhost:15434: exit code 0, seis lotes concluídos, READ ONLY, schema inicial/final e rollback confirmados. Ao concluir, os três containers próprios foram encerrados pelos scripts de identidade/label/ID; recursos de outros projetos foram preservados. Artefatos sintéticos TLS permanecem somente no diretório ignorado pelo Git.

Volume preparado: **1.002 usuários, 10.006 vínculos, 200.008 episódios e 400.006 sentimentos**. Duas execuções sobre a mesma fixture têm contagens/distribuições idênticas e fingerprints inalterados. O relatório de volume registra `DurationMs`, `QueryDurationMs`, `MetadataDurationMs`, `DenominatorDurationMs`, falhas/timeouts e os lotes ordenados por tempo de consulta. Não houve aumento automático de limites. Tempos locais incluem ambiente Docker e Schannel e não predizem custo no banco Supabase compartilhado.

Medição final desta sessão, sem timeouts de volume:

| Consulta | SQL de auditoria | Lote completo, incluindo conexão/preflight/denominadores |
| --- | --- | --- |
| Q4 | 245 ms | 1.895 ms |
| Q1 | 205 ms | 1.910 ms |
| Q2 | 126 ms | 1.843 ms |
| Q3 | 80 ms | 1.770 ms |
| Q0 | 19 ms | 1.805 ms |
| QFinal | 19 ms | 1.652 ms |

Q4 e Q1 foram as consultas de dados mais custosas nessa amostra. O lote completo inclui custos distintos do SQL, portanto não deve ser interpretado como custo puro da consulta. Os três checks de volume passaram: completude dentro dos limites, repetibilidade e fingerprints inalterados.

## Procedimento futuro no Supabase — ainda não autorizado

1. Revisar este diff, relatórios, artefato e versão. Aprovação local não dá autorização de rede/produção. Nenhum comando de provisionamento real foi executado ou preparado para execução automática.
2. Administração independente define identidade exclusivamente de auditoria, senha forte nova, prazo/encerramento de acesso, endpoint/projeto/banco esperados e colunas necessárias. Não usar credenciais da API nem papel proprietário. Conferir permissões efetivas de PUBLIC antes de decidir se o perfil pode ser atendido sem afetar aplicações.
3. Obter hostname e CA apropriados por canal oficial. Registrar configuração sem segredos e conferir identidade esperada; fornecer segredo por canal separado. TLS VerifyFull obrigatório, sem trust override, túnel para contornar o LocalTarget ou fallback inseguro.
4. Se escolher Session Pooler, usar o endpoint oficial de session mode e wire username `<papel>.<project-ref>`, com identidade efetiva esperada igual ao papel. **Não foi simulado um pooler nesta etapa**: retenção de estado, parâmetros de startup, timeouts, TLS do endpoint e cancelamento no Supavisor continuam não validados. Não declarar compatibilidade operacional com Session Pooler por causa dos testes de conexão direta.
5. Validar metadados/RLS/versão com identidade restrita em uma etapa de escopo autorizado. Se faltar acesso a catálogos, parar como inconclusivo, sem conceder superuser, pg_read_all_data ou monitoramento amplo. Se houver RLS/trigger/estrutura diferente, parar para revisão, sem desabilitar RLS, conceder BYPASSRLS ou mudar o contrato automaticamente.
6. Planejar janela de baixa carga e evitar deploys, DDL/grants e incidentes. Um responsável acompanha métricas já disponíveis de latência/erros/CPU/I/O/conexões e pode cancelar. Uma conexão, consultas sequenciais e limites atuais; não executar se a carga for relevante. As varreduras podem consumir recursos mesmo com relatórios pequenos.
7. Definir autorização de execução separada, retenção restrita de relatórios agregados e critérios de interrupção. Q5 remota permanece indisponível; qualquer evidência individual exige outra decisão de escopo. Não criar job recorrente, endpoint ou retry automático.
8. Encerrar sessões, expirar/revogar somente o acesso dedicado aprovado e revisar cobertura/limitações. Nenhum achado dispara reconciliação ou exclusão.

Referências oficiais: [conexões e Session Pooler Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres), [TLS Npgsql](https://www.npgsql.org/doc/security.html), [privilégios PostgreSQL 17](https://www.postgresql.org/docs/17/ddl-priv.html), [pg_index](https://www.postgresql.org/docs/17/catalog-pg-index.html), [pg_trigger](https://www.postgresql.org/docs/17/catalog-pg-trigger.html).

## Limitações e decisão

Não há prova sobre o schema físico, RLS, versão, carga, grants ou certificados do Supabase real. A política de privilégios é intencionalmente estrita e pode bloquear um banco compartilhado normal por grants PUBLIC; isso deve produzir revisão, nunca revogação global automática. Built-ins PostgreSQL e administração do host/cluster fazem parte da base confiável, não há certificação de isolamento para SQL arbitrário. Snapshots e privilégios são conferidos por lote, sem garantia de imutabilidade após a última leitura. Não há catálogo histórico, prova de intenção/status/autoria ou reconciliação.

**APROVADO PARA REVISÃO**, restrito à implementação e evidência local. **A conclusão não autoriza acessar ou auditar o Supabase de produção.**
