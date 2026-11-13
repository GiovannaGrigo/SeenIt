# SeenIt — B03-B2.3: perfil PUBLIC TEMPORARY

Data: 2026-11-12. Decisão: **APROVADO PARA REVISÃO** (evidências locais abaixo; sem autorização de auditoria real).

## Contexto e escopo

O responsável informou diagnóstico autorizado de produção: quatro tabelas presentes, RLS/FORCE RLS desabilitados, PUBLIC com CONNECT/TEMPORARY no banco postgres e USAGE em public. Esses resultados são entrada fornecida pelo usuário, não foram reconsultados nesta etapa e não demonstram ausência de outros privilégios. Nenhum Supabase foi acessado. A adaptação é local; não revoga PUBLIC nem altera papéis reais, schema/dados SeenIt, API, frontend ou maintenance-phase1. Não houve deploy, commit, push ou reconciliação.

Foram inspecionados `RemoteConfiguration.cs`, `RemoteTarget.cs`, `Privileges.sql`, `AuditRunner.cs`, SQL Q0–Q4, `SchemaContract.cs` e a suíte RemoteChecks. O antigo bloqueio de todo TEMP passa a uma exceção por origem de ACL; não há opção de CLI que desative a política.

## Regra implementada

`Privileges.sql` mantém as 12 categorias de preflight. `database_acl` expande `pg_database.datacl` com `aclexplode`, usando `acldefault('d', datdba)` quando a ACL é nula. ACL vazia e ACL nula têm significados distintos. Helpers como `has_database_privilege` continuam verificando acesso efetivo; sozinhos não distinguem origem PUBLIC/direta/membership.

No banco corrente, TEMP só é aceito quando existe ACL `grantee=0`, `privilege_type=TEMPORARY`, sem grant option. Grant TEMP ao próprio auditor ou a qualquer papel membro direto/transitivo bloqueia **mesmo se redundante com PUBLIC**. A enumeração conservadora usa `pg_has_role(...,'MEMBER')`, incluindo grupos NOINHERIT e memberships utilizáveis por SET ROLE; não depende apenas de USAGE/herança automática. Grant option direto também bloqueia. Ausência total de TEMP continua válida.

O banco corrente é confiável somente porque `RemoteTarget.VerifyAsync` verifica primeiro `current_database()` contra o banco explícito da configuração e `session_user/current_user` contra a identidade dedicada. O SQL não aceita um nome alternativo fornecido por entrada SQL. Executá-lo isoladamente não comprova essa comparação: o procedimento manual deve verificar identidade e banco antes de interpretar suas categorias.

CREATE e ownership do banco continuam bloqueados. Em **outros bancos conectáveis** (`datallowconn=true`), CONNECT ou TEMP efetivos bloqueiam; a verificação anterior de CONNECT foi ampliada para TEMP, evitando que a exceção se estenda a outro banco mesmo quando não há CONNECT. Bancos não conectáveis não são inspecionados internamente e não passam a receber autorização de acesso.

| Perfil | Resultado |
| --- | --- |
| Sem TEMP, demais privilégios mínimos | Aceito |
| TEMP somente de PUBLIC no banco esperado, sem grant option | Aceito |
| PUBLIC TEMP + grant TEMP direto, com ou sem grant option | Bloqueado |
| TEMP de grupo direto/transitivo, INHERIT ou NOINHERIT | Bloqueado |
| TEMP direto sem PUBLIC | Bloqueado |
| CREATE, ownership ou privilégio administrativo | Bloqueado |
| CONNECT/TEMP efetivo em outro banco conectável | Bloqueado |
| Escrita persistente, SELECT excedente ou EXECUTE próprio | Bloqueado |

A origem via PUBLIC é um limite de política operacional, não uma redução do poder efetivo de TEMP: um grant direto redundante conferiria a mesma capacidade. A distinção detecta provisionamento fora do perfil aprovado. Rotinas próprias continuam todas bloqueadas por EXECUTE/ownership, incluindo SECURITY INVOKER, SECURITY DEFINER, extensões e rotinas não internas em pg_catalog; não se tenta avaliar dinamicamente se uma delas é segura.

Referência da inspeção de memberships: [PostgreSQL 17: funções de informação](https://www.postgresql.org/docs/17/functions-info.html), que distingue MEMBER, USAGE e SET.

## Ameaça residual e justificativa

TEMP permite criar tabelas e objetos temporários em uma sessão READ WRITE, consumindo disco, CPU e catálogos. Objetos temporários podem sombrear nomes de relações não qualificados. A conta pode alterar seu modo de transação: `default_transaction_read_only` é defesa adicional, não restrição irrevogável. READ ONLY impede CREATE TEMP, mas admite escrita em tabelas temporárias preexistentes. Isso foi demonstrado com a conta sintética, não presumido como proteção universal. [PostgreSQL 17: SET TRANSACTION](https://www.postgresql.org/docs/17/sql-set-transaction.html), [CREATE TABLE](https://www.postgresql.org/docs/17/sql-createtable.html).

A exceção é justificável para **este executor de SQL fixo**: novas conexões não pooladas pelo cliente, BEGIN REPEATABLE READ READ ONLY antes do preflight/consultas, confirmação READ ONLY e identidade/settings por lote, tabelas de aplicação com schema explícito, consultas a catálogos qualificadas, sem execução de rotinas próprias e sem opção de SQL arbitrário. Search_path=pg_catalog sozinho não é tratado como defesa suficiente contra sombreamento de relações; a qualificação explícita é necessária. Não há CREATE TEMP na execução de auditoria.

Um invasor com a senha ou controle do cliente pode usar outro cliente e criar objetos temporários em READ WRITE. A revisão **não afirma que a identidade SQL é sandbox absoluto**. Persistem risco de exaustão de recursos, leitura das colunas autorizadas, chamadas a funções internas e limites de observabilidade do serviço. Privilégios mínimos, segredo exclusivo temporário, janela curta, limites de tempo e encerramento de sessões continuam necessários. TEMP não concede CREATE em schema persistente nem escrita nas tabelas SeenIt. Não há garantia de que uma conta comprometida ficará restrita ao caminho do executor.

Não se deve ampliar esta exceção para EXECUTE, CREATE, privilégios administrativos, ownership, SELECT excedente ou qualquer escrita persistente. Não há reparo automático de permissões, grants em produção ou mudança do contrato Q0.

## Arquivos modificados

- `tools/SeenIt.HistoricalAudit.Remote/Privileges.sql`: origem ACL e exceção limitada; rejeita TEMP em outro banco conectável.
- `tools/SeenIt.HistoricalAudit/AuditRunner.cs`: texto de `PrivilegeCoverage` identifica explicitamente o perfil B2.3 e a exceção nos relatórios remotos; formato/versão base do relatório preservados.
- `tools/SeenIt.HistoricalAudit.RemoteChecks/PublicTemporary.cs`: fixture PUBLIC isolada, casos positivos/negativos e demonstração da ameaça residual; restaura ACLs sintéticas.
- `tools/SeenIt.HistoricalAudit.RemoteChecks/Verification.cs`: substitui a expectativa antiga de rejeitar todo PUBLIC TEMP pelos novos casos; teste de statement timeout usa 100 ms apenas na sessão sintética e restaura 5 s, evitando disputa com o command timeout Npgsql também de 5 s.
- `tools/SeenIt.HistoricalAudit.RemoteChecks/Program.cs`: ação exclusivamente de teste `--verify-public-temp`; remove TEMP de outros bancos apenas na preparação do container descartável; relatório B2.3 separado, sem sobrescrever evidências B2.1.
- Este documento e nota de evolução no procedimento B2.2; relatório local B2.3 com evidências agregadas.

`RemoteTarget`, `RemoteConfiguration` e CLI remoto não precisam mudar; ordem de verificação, TLS e controles de `AuditRunner` já atendem ao limite necessário. A nova ação pertence ao harness sintético, não à CLI de auditoria; o harness continua com host loopback, portas e banco sintéticos fixos, sem destino genérico ou configuração da API.

## Verificação local e reprodução

Resultado final: **67 verificações aprovadas, exit code 0, PostgreSQL 17.11**. Build concluído com zero avisos/erros. Auditoria sintética completa nos seis lotes, resultados repetidos iguais, fingerprints integrais de dados/estrutura iguais e ACLs de banco/schema restauradas exatamente. Evidência final identifica `B03-B2.3/PUBLIC-TEMP/1`, `publicFixture.aclRestored=true` e `sessionPoolerValidated=false`. Os dois containers dedicados foram encerrados após a validação; certificados sintéticos locais preservados.

Ambiente: dois containers descartáveis `postgres:17-alpine`, nomes/labels exclusivos B2.1 e portas publicadas somente em 127.0.0.1:15434/15435. Um certificado válido e outro expirado, ambos sintéticos. Containers preexistentes de outros projetos preservados. Segredos/certificados sintéticos já existentes foram reutilizados sem exibição nem instalação de CA global.

```powershell
dotnet build tools/SeenIt.HistoricalAudit.RemoteChecks --no-restore
& ./tools/historical-audit/Start-TlsLocal.ps1
docker exec seenit-b03b21-tls pg_isready -U postgres -d seenit_b03b21
dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --prepare-only
dotnet run --project tools/SeenIt.HistoricalAudit.RemoteChecks --no-build -- --verify-public-temp
& ./tools/historical-audit/Stop-TlsLocal.ps1
```

Usar somente containers vazios e dedicados: preparação recusa banco ocupado/identidade sintética existente. A ação PUBLIC exige PostgreSQL 17, banco sintético esperado e ACLs PUBLIC de banco/schema inicialmente vazias; reproduz CONNECT/TEMP no banco e USAGE no schema antes da suíte. A restauração em finally compara ACLs com a fotografia anterior. Não usar qualquer comando de preparação em banco real.

A primeira tentativa de verificação TLS no sandbox falhou no primeiro check; fora do sandbox Windows a suíte pode usar o provedor TLS normalmente, com aprovação específica e o mesmo VerifyFull. Falhas do sandbox não justificam downgrade. O build local não modifica código da aplicação.

Durante a validação, uma execução passou 66 checks mas detectou limpeza incompleta do teste de ownership: devolver ownership do banco retirou o grant direto de CONNECT da conta sintética. A limpeza foi corrigida para repor esse grant. Na repetição, a restauração de ACL passou; o teste antigo de timeout encontrou disputa entre deadlines de servidor/cliente iguais. O teste foi corrigido para observar 57014 com prazo de servidor menor somente na sessão sintética, restaurado em finally. Nenhum limite de conexão, lote, comando ou servidor do executor foi ampliado. O relatório final deve ser considerado válido somente com a execução integral e encerramento de ACL concluídos.

O relatório [B03-B23-TLS-AUDIT-REPORT.json](B03-B23-TLS-AUDIT-REPORT.json) registra resultado, checks, PostgreSQL observado, fingerprints das quatro tabelas e assinaturas estruturais nos dois schemas sintéticos. Fingerprints cobrem integralmente valores das fixtures, inclusive colunas não acessíveis ao auditor, por sessão administrativa **sintética** independente; somente hashes comparados/booleanos aparecem como evidência, sem dados pessoais no relatório. Intervenções do harness alteram temporariamente ACL/ownership/RLS e objetos extras sintéticos, restaurados antes da comparação final. Essas escritas de teste são separadas da auditoria READ ONLY.

Cobertura: aceitação PUBLIC-only e sem TEMP; rejeição direta/grant option/membership/transitiva; outro banco; CREATE direto/PUBLIC; ownership; funções INVOKER/DEFINER e pg_catalog; SELECT pessoal/table-level/PUBLIC, UPDATE/MAINTAIN, sequências, schemas, BYPASSRLS, pg_monitor, ADMIN OPTION, falta de metadados/privilégios; Q0/RLS/índices/FK; certificados/hostname/credencial/identidade; cancelamento/timeouts/rollback; repetibilidade de resultados e fingerprints. O novo teste comprova SQLSTATE 25006 para CREATE TEMP em READ ONLY e 42501 para UPDATE persistente em READ WRITE, além da escrita permitida em tabela temporária preexistente.

## Garantias preservadas e limites operacionais

Continuam obrigatórios TLS VerifyFull/CA explícita, identidade exclusiva, banco esperado, SQL fixo, READ ONLY por lote, privilégios administrativos negados, ausência de escrita persistente, SELECT por allowlist, execução de rotina própria negada, Q0 por lote e QFinal, limites de tempo e rollback confirmado. CLI sem SQL arbitrário; Q5 não exposta remotamente, Q6/Q7 sintéticas. Nenhum privilégio global de Supabase alterado.

Resultados só valem para PostgreSQL 17 isolado e fixtures declaradas; não validam Session Pooler, CA real, rede, carga ou privilégios completos de produção. ACLs podem mudar depois do preflight; snapshot por lote não é global. PUBLIC EXECUTE ou outros excessos eventualmente encontrados no Supabase continuam bloqueadores. O diagnóstico anterior informado não substitui as 12 verificações completas como auditor.

`Pooling=false` desativa apenas o pool Npgsql, não o Supavisor. Não foi comprovado como o pooler real limpa estado/objetos temporários entre clientes; não presumir backend novo a cada conexão cliente. Exclusividade da identidade, verificação repetida de ownership/privilégios e encerramento controlado continuam necessários; não reutilizar conta/sessão de outro trabalho.

O procedimento [B2.2](B03-B22-SUPABASE-OPERATIONS.md) continua exigindo etapas autorizadas, teste limitado de conectividade/TLS e Q0 antes de Q1–Q4, e revogação/encerramento posterior. A CLI atual ainda não tem modo de conectividade/Q0 isolado: não usar `--execute` como teste limitado. Esta revisão local não autoriza primeira conexão real nem agregados em produção.
