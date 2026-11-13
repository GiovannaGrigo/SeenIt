# SeenIt — B03-B2.2: preparação operacional do Supabase

Data: 2026-11-12. Conclusão: **APROVADO PARA DIAGNÓSTICO OPERACIONAL**.

Evolução local: [B03-B2.3](B03-B23-PUBLIC-TEMP-PROFILE.md) revisa exclusivamente o bloqueio de TEMP descrito abaixo. Após essa revisão, TEMP não delegável somente de PUBLIC no banco esperado pode ser aceito; grants diretos/memberships, CREATE e demais excessos continuam bloqueados. As descrições seguintes do perfil B2.1 permanecem históricas; autorizações e gates operacionais continuam necessários.

Este parecer aprova o procedimento documental para um diagnóstico futuro autorizado. Não autoriza conexão, criação de identidade ou Q1–Q4. A viabilidade da identidade restrita e a compatibilidade real com o pooler permanecem não confirmadas. PUBLIC incompatível é bloqueador da auditoria atual, não motivo para enfraquecer o executor.

Nesta etapa foram inspecionados arquivos locais e documentação pública oficial. Nenhuma consulta foi executada, nenhum Supabase foi acessado e nenhum segredo foi solicitado. Não houve mudança no executor, API, frontend, banco, maintenance-phase1, deploy, commit ou push. Evidências anteriores de 43/47 assertivas e três testes de volume são históricas, não foram repetidas e não demonstram compatibilidade com produção.

## 1. Implementação e pré-requisitos

Base: [plano B03-B](B03-B-HISTORICAL-INTEGRITY-PLAN.md), [B2](B03-B2-READ-ONLY-AUDIT.md), [B2.1](B03-B21-REMOTE-AUDIT.md), `RemoteConfiguration.cs`, `RemoteTarget.cs`, `Privileges.sql`, `Program.cs`, `AuditRunner.cs`, `SchemaContract.cs` e `Sql/Q0.sql` sob `tools/SeenIt.HistoricalAudit*`.

SDK .NET 10 e dependência Npgsql 10.0.3; artefato revisado/buildado localmente sem iniciar a API. Servidor deve ser PostgreSQL **17** (17.11 foi a versão local testada). Exigir operador designado, janela de baixo uso, limites de conexão disponíveis, relógio correto, acesso de rede autorizado, CA obtida de fonte oficial e diretório local restrito para configuração/resultados. Não executar os harnesses RemoteChecks/Checks ou scripts de preparação no Supabase.

| Entrada | Contrato efetivo |
| --- | --- |
| Host | `Host` explícito no JSON; DNS único ou IPv4; sem URI, lista, socket ou IPv6 literal. Usar DNS exato do painel para validar hostname TLS. |
| Porta/banco | `Port` explícita entre 1024–65535; Session Pooler 5432. `Database` explícito, identificador simples de até 63 caracteres; conferir no painel, não assumir. |
| Usuário de conexão | `ConnectionUser`: papel ou `papel.projectref`; sufixo aceito pelo código: 8–40 caracteres alfanuméricos minúsculos. |
| Identidade esperada | `ExpectedSessionUser` = `ExpectedCurrentUser`, padrão `seenit_audit_[a-z0-9_]{1,40}`. No servidor ambos devem ser o papel sem project-ref; banco também é comparado. |
| Exclusividade | `ExclusiveAuditIdentity=true` é declaração operacional obrigatória; não comprova exclusividade por si só. |
| Credencial | Somente variável de processo `SEENIT_AUDIT_REMOTE_PASSWORD`, mínimo 24 caracteres; senha exclusiva aleatória, nunca credencial API, postgres ou service_role. Não colocar em JSON, argumento, transcript ou relatório. |
| CA | `RootCertificate`: caminho absoluto existente. A validação offline confere existência, não conteúdo, cadeia ou validade. |
| TLS | Builder fixa VerifyFull, CA explícita, GSS Disable; não há opção de downgrade ou callback permissivo. Identidade TLS é o hostname; identidade SQL é verificada separadamente. |

JSON ilustrativo inoperante (substituições futuras somente em arquivo local restrito):

```json
{
  "Host": "session-pooler.example.invalid",
  "Port": 5432,
  "Database": "postgres",
  "ConnectionUser": "seenit_audit_b22.exampleprojectref",
  "ExpectedSessionUser": "seenit_audit_b22",
  "ExpectedCurrentUser": "seenit_audit_b22",
  "RootCertificate": "C:/audit/supabase-ca.crt",
  "ExclusiveAuditIdentity": true
}
```

`--enable-remote --configuration=<arquivo>` sem `--execute` só valida formato e existência da CA, sem senha/conexão. Não valida DNS, TLS, privilégios ou Q0. Com `--execute`, a CLI inicia **Q0, Q1, Q2, Q3, Q4 e QFinal**; não há flags de connectivity-only ou Q0-only. Nunca usar `--execute` para as etapas 4 e 5 abaixo. Elas usam cliente operacional revisado, sem criar nova funcionalidade no executor. psql pode testar transporte e SQL; não comprova comportamento do Npgsql. Para chamar a compatibilidade Npgsql de validada, exigir posteriormente uma prova limitada revisada com Npgsql 10.0.3 e o mesmo builder (ou ajuste mínimo separado e autorizado); até lá registrá-la como pendente.

O executor desliga pooling Npgsql, retries e paralelismo no cliente; o pooler do serviço continua existindo. Uma conexão por lote, snapshot REPEATABLE READ próprio e rollback; não existe snapshot global. Timeouts: conexão 3 s, comando/statement 5 s, lock 1 s, idle-in-transaction 10 s, lote 12 s, CLI 120 s; rollback 5 s e cancelamento Npgsql 1000 ms. `Options` envia search_path/timeout no startup: aceitar o formato de usuário não prova que o pooler propaga essas opções. Se rejeitadas/ignoradas, interromper; não remover verificações silenciosamente.

## 2. Diagnóstico SQL proposto — não executado

Arquivo [B03-B22-SUPABASE-DIAGNOSTIC.sql](B03-B22-SUPABASE-DIAGNOSTIC.sql): apenas SELECTs de metadados e controle de transação READ ONLY. Sem valores pessoais, pg_authid, hashes, chamadas a rotinas de aplicação, DDL ou DML. Revisar todos os result sets; erro, timeout ou resultado ausente significa inconclusivo, nunca zero. No cliente futuro desabilitar autocommit de continuação após erro, ativar stop-on-error, limitar tempo total a 12 s e garantir fechamento/rollback em finally. Não repetir automaticamente.

Primeira passagem pelo operador autorizado, usando uma sessão de gestão já aprovada: versão, quatro tabelas/colunas/RLS, ACLs PUBLIC atuais e defaults de objetos futuros. O auditor **não** recebe credencial dessa sessão. O painel SQL pode operar como postgres; isso não serve de evidência dos privilégios do auditor. Não abrir uma conexão de executor como postgres para realizar diagnóstico.

Após provisionamento autorizado, segunda passagem autenticada como o próprio auditor: executar o mesmo diagnóstico e, em outra transação curta READ ONLY, o arquivo original [Privileges.sql](../tools/SeenIt.HistoricalAudit.Remote/Privileges.sql), sem adaptação. Ele usa current_user; executá-lo como administrador prova apenas o perfil do administrador. Exigir exatamente 12 categorias e todos os valores zero. Conferir CONNECT explicitamente, pois o preflight o pressupõe pela conexão. Falta de acesso a um catálogo não autoriza GRANT de administração.

Cobertura das 12 categorias: atributos/memberships administrativos e ADMIN OPTION; CREATE/TEMP/ownership no banco e CONNECT em outro banco conectável; CREATE/ownership de schemas; escritas e ownership de relações, inclusive coluna e MAINTAIN; SELECT excedente/grant option; SELECT necessário; sequências; EXECUTE/ownership de rotinas próprias (inclusive extensões e OID >=16384 em pg_catalog); large objects; ACLs administrativas de parâmetros; servidores/wrappers estrangeiros; tablespaces/tipos. Catálogos internos continuam legíveis: a allowlist não promete invisibilidade dos metadados ou sandbox para SQL arbitrário.

Para Q0, executar futuramente o [SQL original](../tools/SeenIt.HistoricalAudit/Sql/Q0.sql) **somente após** perfil aprovado, trocando mecanicamente todas as ocorrências de `@schema` por literal `'public'` numa cópia operacional revisada (não há binding de `@schema` no psql). Esse SELECT é a prova real de disponibilidade dos metadados; a sondagem de catálogos isolada é apenas preliminar. Comparar multiconjunto completo com [SchemaContract.cs](../tools/SeenIt.HistoricalAudit/SchemaContract.cs): 4 tabelas, 29 colunas, 7 constraints, 7 índices, 12 triggers FK internos e 1 default, total 60 assinaturas. Contagem sozinha não prova conformidade. Verificar tipos, nulabilidade, collations, opclasses, ações FK, triggers, flags e default; não registrar valores de linhas. O contrato exige RLS e FORCE RLS desabilitados nas quatro tabelas e ausência de triggers próprios/rules/particionamento. Se RLS estiver habilitada, interromper: não desabilitar RLS, criar política ampla ou dar BYPASSRLS.

## 3. PUBLIC: incompatibilidade e alternativas

O PostgreSQL concede por padrão CONNECT/TEMP a PUBLIC em bancos e EXECUTE em rotinas; ACL nula exige `acldefault`, não significa ausência de grants. Os padrões efetivos do projeto Supabase são desconhecidos até o diagnóstico. Grants a anon/authenticated/service_role não são automaticamente grants de PUBLIC: distinguir grantee 0 e memberships. [Privilégios PostgreSQL 17](https://www.postgresql.org/docs/17/ddl-priv.html).

Incompatibilidades exatas com `Privileges.sql`: TEMP/CREATE no banco corrente → categoria `database`; CONNECT em qualquer outro banco conectável → `database`; EXECUTE em rotina própria/extensão → `custom_routine_access_or_ownership`; CREATE em schema → `schema`; leitura fora das 22 colunas permitidas → `excess_select_or_grant_option`; qualquer escrita → `relation_writes_or_ownership`. USAGE em public e CONNECT apenas no banco alvo são necessários e aceitos. PUBLIC pode gerar outras categorias não zero: todas bloqueiam.

Privilégios são aditivos. `NOINHERIT`, RLS, default_transaction_read_only e REVOKE da conta individual não anulam PUBLIC. Não existe DENY por papel. Mesmo rotina sem USAGE no schema pode ser sinalizada pelo preflight conservador. Não usar uma tentativa de executar função para provar segurança.

Alternativas, sujeitas a avaliação separada:

1. Se PUBLIC já for compatível, criar papel dedicado sem memberships e confirmar privilégios efetivos em sua sessão.
2. Se incompatível, usar cópia isolada autorizada com dados minimizados e política própria de PUBLIC; tratar risco de exportação e declarar que a evidência descreve a cópia, não a produção viva. Não preparar/exportar nesta etapa.
3. Revisar uma política de privilégios do projeto, com análise de impacto e autorização próprias; isso pode afetar usuários atuais e está fora desta tarefa. Não propor/aplicar REVOKE global para fazer o teste passar.
4. Qualquer revisão do perfil do executor exige desenho explícito de ameaça, justificativa por privilégio e novos testes antes de autorização; não é solução aprovada neste documento.

Se a separação exigir alterar direitos existentes ou ignorar PUBLIC, **a identidade restrita é inviável sob as restrições atuais**. Registrar bloqueio e não provisionar.

## 4. Identidade temporária — proposta, não executada

Somente após diagnóstico preliminar compatível e autorização específica de criação. Operador de gestão precisa de autoridade delegada para criar papel e conceder somente estes direitos; não conceder CREATEROLE ao auditor. Confirmar nome inexistente (se existir, parar; não reutilizar/alterar). Uma role PostgreSQL não tem expiração automática de existência: `VALID UNTIL` limita autenticação por senha, não encerra sessões.

Modelo DDL deliberadamente **não executável** até substituir banco, prazo e canal seguro de senha. Não criar arquivo com segredo. Criar inicialmente NOLOGIN; definir senha via prompt seguro de ferramenta aprovada (por exemplo `\password seenit_audit_b22` no psql de gestão, sem echo/transcript), usando material aleatório exclusivo de pelo menos 24 caracteres. Não copiar credencial da API. Aprovar previamente a janela curta em UTC.

```sql
BEGIN;
CREATE ROLE seenit_audit_b22 NOLOGIN NOINHERIT
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
  CONNECTION LIMIT 1 VALID UNTIL '<PRAZO_UTC_APROVADO>';
GRANT CONNECT ON DATABASE <BANCO_CONFIRMADO> TO seenit_audit_b22;
GRANT USAGE ON SCHEMA public TO seenit_audit_b22;
GRANT SELECT ("Id") ON public."Users" TO seenit_audit_b22;
GRANT SELECT ("Id","UserId","ExternalSeriesId","Name","PosterUrl","Status",
  "IsAnime","AddedAtUtc","UpdatedAtUtc") ON public."UserSeries" TO seenit_audit_b22;
GRANT SELECT ("Id","UserId","ExternalSeriesId","ExternalEpisodeId","Temporada",
  "NumeroEpisodio","NomeEpisodio","ExternalCharacterId","PersonagemFavoritoNome",
  "AssistidoEmUtc") ON public."EpisodiosAssistidos" TO seenit_audit_b22;
GRANT SELECT ("EpisodioAssistidoId","Sentimento")
  ON public."EpisodiosAssistidosSentimentos" TO seenit_audit_b22;
ALTER ROLE seenit_audit_b22 SET default_transaction_read_only = on;
COMMIT;
-- Fora da transação acima: definir senha por prompt seguro; conferir prazo/grants.
-- Só então, por decisão explícita do operador:
ALTER ROLE seenit_audit_b22 LOGIN;
```

Não conceder tabela inteira, ALL TABLES, grant option, sequências, funções, roles de grupo, authenticator, anon, authenticated, service_role ou roles pg_*. Não alterar DEFAULT PRIVILEGES. Não definir ownership, RLS, triggers ou constraints. Memberships zero, inclusive SET ROLE e ADMIN OPTION; NOINHERIT sozinho é insuficiente. `CONNECTION LIMIT 1` é proteção adicional e não garante ausência de backend retido no pooler. `default_transaction_read_only` é defesa operacional alterável pelo usuário, não fronteira de segurança: grants e consultas fixas continuam essenciais. Conferir ACLs novamente após criação e antes de cada uso; se grant inesperado aparecer, NOLOGIN e encerrar.

## 5. Session Pooler e configuração segura

Copiar host/porta/banco/project-ref do diálogo Connect → Session pooler; não derivar host da região. Serviço compartilhado atende IPv4 no plano Free, porta 5432 e usuário `<role>.<project-ref>`; não usar endpoint HTTP/API ou pooler transacional 6543. Session mode mantém backend associado durante a conexão; suporta estado de sessão e prepared statements. Cada lote fecha sua conexão: nenhum estado deve ser presumido no seguinte. O executor não usa recursos que dependam de sessão entre lotes. Capacidade do pool é limitada e sessões longas ocupam conexões. [Conexões Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres).

CA do painel de Database settings e hostname do endpoint devem passar validação de cadeia/validade/nome. Não usar IP no lugar do DNS, Require, VerifyCA ou TrustServerCertificate para contornar erro. TLS client→pooler não é prova independente do trecho pooler→PostgreSQL; verificar política oferecida pelo serviço. `pg_stat_ssl` mostra o trecho do backend e não substitui VerifyFull no cliente. Npgsql VerifyFull valida servidor e nome, enquanto Require apenas cifra; CA pode ser indicada por Root Certificate. Revogação de certificado não é verificada por padrão no Npgsql, e o contrato atual não habilita essa opção. [Segurança Npgsql](https://www.npgsql.org/doc/security.html).

Para teste limitado futuro em Npgsql, usar mesmo builder, senha apenas em memória/variável dedicada, uma conexão, sem pooling/retries, negociar TLS antes do SQL; abrir BEGIN REPEATABLE READ READ ONLY, consultar identidade/settings do diagnóstico e fechar com ROLLBACK confirmado. Verificar também startup `Options` (search_path=pg_catalog, statement_timeout=5s, lock_timeout=1s, idle_in_transaction_session_timeout=10s), session_replication_role=origin e isolamento. Não provar READ ONLY tentando INSERT/UPDATE. Sucesso psql precisa ser rotulado como psql; não chamar isso de validação do executor. [Parâmetros Npgsql](https://www.npgsql.org/doc/connection-string-parameters.html), [READ ONLY PostgreSQL](https://www.postgresql.org/docs/17/sql-set-transaction.html).

| Falha | Decisão |
| --- | --- |
| DNS/rede/timeout 3 s | Parar; revisar endpoint, projeto ativo, rede e capacidade. Não aumentar limites nem repetir automaticamente. |
| CA/cadeia/expiração/hostname/TLS | Parar antes de SQL; conferir CA oficial, DNS e relógio; nunca desabilitar VerifyFull. |
| Autenticação/tenant/usuário não encontrado | Conferir sufixo project-ref, LOGIN/prazo e configuração no painel, sem imprimir senha. Não tentar postgres/service_role. |
| Pool cheio/propagação/Options rejeitadas | Parar e revisar logs do pooler sanitizados; eventual nova tentativa exige decisão do operador. |
| Banco/identidade/settings divergentes | Parar; não usar SET ROLE para adaptar identidade esperada nem remover Options. |
| 42501/catálogo indisponível | Diagnóstico inconclusivo; não conceder pg_monitor ou administração como correção rápida. |
| Privilégio excedente/Q0 drift/timeout de consulta | Parar antes dos dados; não adaptar schema/allowlist nem tratar falha como zero achados. |

Logs de Supavisor distinguem falhas de autenticação, roteamento e capacidade; preservar somente código/categoria e horário, evitando connection strings e mensagens que contenham dados. [Troubleshooting Supavisor](https://supabase.com/docs/guides/database/supavisor). O executor sanitiza falhas e usa IncludeErrorDetail=false/PersistSecurityInfo=false; relatórios não contêm a senha. O processo chamador ainda deve limpar a variável em finally; a limpeza interna não altera o ambiente pai.

## 6. Sequência futura e pontos de interrupção

Nenhuma etapa desta tabela foi executada. Autorizações devem especificar alvo, operador, escopo e janela, sem segredos.

| Etapa | Condição de entrada e evidência para continuar | Interromper se |
| --- | --- | --- |
| 1. Conferência manual | Responsável confere projeto/ambiente, Session Pooler, banco, CA, versão prevista, capacidade e local seguro de resultados; registra aprovação de diagnóstico | Alvo incerto, ausência de CA confiável, janela/operador sem aprovação |
| 2. Diagnóstico somente leitura | Sessão de gestão aprovada, SQL revisado; conferir PostgreSQL 17, estrutura/RLS e ACL PUBLIC/defaults. Aprovação de diagnóstico não autoriza provisionamento | RLS incompatível, PUBLIC excedente, erro/incompletude ou impossibilidade de separação |
| 3. Criação autorizada | Aprovação distinta de DDL; papel ausente, grants de 22 colunas, sem memberships/ownership, segredo exclusivo, prazo e plano de encerramento | Permissões do operador insuficientes, nome ocupado, necessidade de mudar direitos atuais, grants inesperados |
| 4. Conectividade/TLS limitada | Aprovação da primeira conexão do auditor, config offline revisada, segredo/CA preparados; uma sessão, identidade/settings/READ ONLY/rollback confirmados; diagnóstico como auditor e Privileges.sql com 12 zeros | Qualquer falha de transporte, identidade, settings ou privilégios; prova só psql não libera Npgsql |
| 5. Q0 isolada | Perfil aprovado, SQL Q0 original e cópia literal revisada; prova Npgsql limitada resolvida antes de liberar CLI; 60 assinaturas exatamente conformes | Catálogo faltante, drift, contagem truncada, timeout ou comparação incompleta |
| 6. Q1–Q4 | **Nova aprovação explícita** dos agregados e carga; artefato e config conferidos, etapas anteriores válidas; somente agora CLI --execute, que repete Q0/preflight e inclui QFinal | Qualquer lote incompleto, perda READ ONLY/rollback, drift ou limite excedido; sem retry |
| 7. Revogação | Sempre, inclusive falha/cancelamento ou abandono antes de Q1; bloquear login, fechar sessões, retirar grants e remover papel | Sessão/dependência residual ou ownership inesperado: manter NOLOGIN e escalar ao responsável |
| 8. Revisão | Conferir AuditComplete, seis lotes, Q0/QFinal, TLS/identidade/privilégios/READ ONLY e rollback em cada lote; registrar achados e encerramento | Qualquer campo inconclusivo impede declarar auditoria completa |

Critério para autorizar **primeira conexão de gestão para diagnóstico**: etapa 1 aprovada e SQL somente leitura revisado. Critério para **primeira conexão do auditor**: etapas 1–3 aprovadas, PUBLIC preliminar compatível, identidade separada, CA/endpoint verificados, teste limitado previamente revisado e rollback/encerramento preparados. Não usar o CLI completo para descobrir se esses critérios são verdadeiros. A preparação aqui é documental: a ausência atual de um teste limitado revisado bloqueia essa conexão até sua preparação futura, sem exigir nova funcionalidade agora.

Exit code 0 do executor indica conclusão, não ausência de achados; 2 indica rejeição/incompletude. Q5 não é exposta remotamente; Q6/Q7 continuam sintéticas. Snapshot por lote, mudanças concorrentes e leituras de catálogos limitam as conclusões. READ ONLY não elimina carga/locks de leitura nem prova independente de dados inalterados; testes sintéticos anteriores não são essa prova em produção. Não inserir fingerprints/varreduras adicionais sem análise de carga e autorização.

## 7. Encerramento e revogação propostos

Operador de gestão, após autorização de criação que inclua encerramento, faz `ALTER ROLE seenit_audit_b22 NOLOGIN`, para o cliente, limpa variável dedicada no processo pai e remove material de segredo do canal temporário conforme política local. NOLOGIN/VALID UNTIL não encerram sessões existentes. Consultar somente PIDs de `pg_stat_activity` filtrados pelo papel; fechar sessões do auditor no cliente/pooler e confirmar ausência de backend ativo. Se necessário, operador autorizado pode terminar **somente PIDs revisados desse papel**, nunca conexões da API; não conceder pg_signal_backend ao auditor. Pooler pode reter conexão física após fechamento do cliente: sem confirmação do encerramento, não declarar revogação concluída.

Retirar explicitamente os mesmos grants SELECT por coluna listados na seção 4 (trocar GRANT SELECT por REVOKE SELECT e TO por FROM), USAGE em public e CONNECT no banco confirmado. Conferir ACLs/memberships/dependências novamente; sem dependências, `DROP ROLE seenit_audit_b22`. Se faltar privilégio/dependência aparecer, manter NOLOGIN e encaminhar ao responsável. Não executar DROP OWNED, REASSIGN OWNED, CASCADE ou revogações em massa: auditor não deve possuir objetos, e um desvio exige investigação. Role é de cluster: dependências em outro banco impedem drop e não autorizam acessar esse banco com o auditor.

Validar desaparecimento em pg_roles, ausência de grants ao papel e sessões remanescentes; documentar horário, operador, eventuais bloqueios e descarte da senha. Não é necessário reautenticar para testar revogação. Arquivar somente relatórios sanitizados e decisões de aprovação; restringir acesso aos metadados. Sem nova autorização, não corrigir dados nem reconciliar achados.

## 8. Parecer e revisão desta entrega

**APROVADO PARA DIAGNÓSTICO OPERACIONAL**, com execução futura condicionada aos gates acima. Supabase real, PUBLIC real, CA/Options no Session Pooler e identidade temporária ainda não foram validados. Se PUBLIC violar o perfil, a conclusão operacional daquele diagnóstico será bloqueio da auditoria no ambiente atual.

Revisão estática dos arquivos/documentação; SQL não executado e build/testes não necessários para alteração exclusivamente documental. Referências oficiais consultadas em 2026-11-12; mudanças futuras no serviço exigem reconferência. Este documento e o SQL acompanhante não constituem autorização para produção.
