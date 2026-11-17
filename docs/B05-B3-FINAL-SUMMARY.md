# B05-B3 — Encerramento técnico da correção SQLite

Data: 17/11/2026, America/Sao_Paulo. Etapa: B05-B3.6.

**Resultado: B05-B3 PRONTO PARA ENCERRAMENTO.** O PR #4 foi integrado, a `main` local foi sincronizada por fast-forward e o operador confirmou a validação funcional pós-merge sem problemas relatados. Nesta etapa, o único arquivo criado é este relatório; não houve alteração de código/dependências, commit, push ou deploy.

## 1. Objetivo, histórico e fontes

O B05-B3 corrigiu uma vulnerabilidade na biblioteca SQLite nativa usada pelos testes de integração, com a menor alteração aprovada e preservando as dependências da aplicação.

| Etapa | Resultado |
| --- | --- |
| B05-B3.1 | Diagnóstico da origem transitiva e do alcance da vulnerabilidade. |
| B05-B3.2 | Experimento isolado da candidata aprovado em Windows x64 e Linux x64. |
| B05-B3.3 | Aplicação da referência no projeto original, com checks, auditoria e publish local inspecionado. |
| B05-B3.4 | Revisão final, repetição das validações Windows, dois commits separados e criação do PR #4. |
| B05-B3.5 | Merge realizado pelo operador. |
| B05-B3.6 | Confirmação do merge, sincronização da main e documentação do encerramento. |

Fontes versionadas preservadas: [diagnóstico B05-B3.1](B05-B3-SQLITE-SECURITY-DIAGNOSIS.md) e [experimento B05-B3.2](B05-B3-SQLITE-PATCH-EXPERIMENT.md). Seus registros de ausência de implementação descrevem aquelas etapas históricas; a aplicação posterior ocorreu em B05-B3.3. As evidências locais das etapas B05-B3.3/B05-B3.4 e os resultados desta sessão complementam esses documentos.

## 2. Vulnerabilidade e correção aplicada

[GHSA-2m69-gcr7-jv3q / CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) foi reportado pelo NuGet como High para `SQLitePCLRaw.lib.e_sqlite3 <= 2.1.11`. O problema de corrupção de memória envolve excesso de termos agregados em consultas; para essa CVE, o motor exigido é SQLite >= 3.50.2.

Origem anterior:

```text
SeenIt.RegisterIntegrationChecks
└─ Microsoft.EntityFrameworkCore.Sqlite 10.0.0
   └─ SQLitePCLRaw.bundle_e_sqlite3 2.1.11
      └─ SQLitePCLRaw.lib.e_sqlite3 2.1.11 → motor SQLite 3.49.1
```

A correção adicionou exclusivamente em `backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj`:

```xml
<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3" Version="2.1.12" PrivateAssets="all" />
```

O pacote corrigido é **SQLitePCLRaw.lib.e_sqlite3 2.1.12**; o motor nativo efetivamente carregado é **SQLite 3.53.3**. São números de versão distintos. O probe isolado utilizou os manifests reais do build de integração, abriu `Data Source=:memory:` e executou `SELECT sqlite_version();`, confirmou banco em memória e CRUD, e identificou a biblioteca nativa esperada em Windows e Linux, sem DllNotFoundException ou EntryPointNotFoundException.

Somente a lib nativa passou de 2.1.11 para 2.1.12 no grafo de integração. Bundle, provider e core SQLitePCLRaw permaneceram em 2.1.11; Microsoft.EntityFrameworkCore.Sqlite permaneceu em 10.0.0 e Microsoft.AspNetCore.Mvc.Testing em 10.0.7. `PrivateAssets="all"` foi mantido; ele restringe propagação a consumidores, sem impedir carga ou auditoria no próprio executável.

## 3. Preservação do escopo

A comparação da base `f370c58d8f01dc646f04a0f92f778ae7b2878115` com a integração mostra somente uma linha no csproj e os dois relatórios anteriores. A referência direta à lib aparece exclusivamente nesse csproj entre os projetos versionados.

EF Core/Npgsql 10.0.0 da API, seu grafo NuGet, código funcional e Dockerfile foram preservados. Angular permanece na major 21 (`@angular/core` 21.2.26). Não houve atualização adicional de dependências, alteração funcional em API/frontend, banco, migrations, configurações de produção ou Docker, nem adição de comentários ao código. Nenhum arquivo com credenciais ou segredos foi incluído no PR.

## 4. Verificações técnicas executadas pelo Codex

Os resultados abaixo pertencem às etapas de validação anteriores ao encerramento. B05-B3.6 conferiu Git, implementação e evidências; não repetiu restore, build, auditoria ou checks após o merge.

| Verificação | Resultado registrado |
| --- | --- |
| Restore | Oito projetos aprovados em B05-B3.3; restore de integração repetido em B05-B3.4, sem avisos finais. |
| Builds Release | Três executáveis aprovados, sem avisos ou erros nas execuções finais. |
| AvatarChecks | 16 grupos aprovados. |
| Checks | Watch time, classificação anime e 72 casos HTTP de validação aprovados. |
| CatalogResilienceChecks | Aprovado. |
| DataIntegrityChecks | Aprovado. |
| RegistrationIntegrationChecks | 73 casos HTTP e 16 usuários em memória; rejeições sem comandos de banco, saves ou hashes. |
| RateLimitingChecks | Aprovado. |
| RateLimitingRegressionChecks | Aprovado, incluindo 20 logins sobrepostos. |
| Motor nativo | SQLite 3.53.3 confirmado em memória em Windows x64 e Linux x64. |

Os três executáveis foram efetivamente executados, com integração no modo padrão e suas cinco suítes; `dotnet test` não foi usado como substituto. Os checks utilizaram dados sintéticos e mocks, sem Supabase ou bancos reais. Não foi executado `--postgres-only`.

Windows x64 e Linux x64 foram aprovados em B05-B3.2 e B05-B3.3. No experimento, Linux usou contêiner sem rede externa. Na aplicação B05-B3.3, Ubuntu WSL usou cópias temporárias e namespace sem rede externa durante builds/checks, com loopback para HTTP local; os quatro grafos backend coincidiram com os do Windows. B05-B3.4 repetiu as validações localmente no Windows. Os SDKs de B05-B3.3 foram Windows 10.0.301 e Linux 10.0.110; os ambientes não eram idênticos.

## 5. Auditoria NuGet e publish local

As auditorias das etapas B05-B3.2/B05-B3.3 e a repetição final B05-B3.4 incluíram dependências transitivas e consultaram a fonte oficial NuGet. Na candidata e na implementação, nenhum alerta conhecido foi reportado nos oito projetos:

- SeenIt.Api;
- SeenIt.AvatarChecks;
- SeenIt.Checks;
- SeenIt.RegisterIntegrationChecks;
- SeenIt.HistoricalAudit;
- SeenIt.HistoricalAudit.Checks;
- SeenIt.HistoricalAudit.Remote;
- SeenIt.HistoricalAudit.RemoteChecks.

O controle vulnerável de B05-B3.2 reportava lib.e_sqlite3 2.1.11 com o advisory High. A correção não suprimiu advisories nem desabilitou auditoria. A consulta de B05-B3.3 registrou atualização da base em 17/11/2026 às 11:30:16 UTC; os resultados são relativos às bases consultadas, sem certificação universal de ausência de vulnerabilidades.

O publish local descartável da API foi inspecionado sem executar a API: 87 arquivos no experimento B05-B3.2 e 91 na aplicação B05-B3.3. Em ambos, não foram encontrados SQLitePCLRaw, Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.Sqlite, e_sqlite3.dll ou libe_sqlite3.so nos arquivos, nem referências SQLite no manifesto da API. O grafo da API permaneceu inalterado. Essa inspeção local não atesta o conteúdo de uma implantação remota.

## 6. Integração e sincronização confirmadas

[PR #4 — Fix: Corrige vulnerabilidade SQLite nos testes de integração](https://github.com/GiovannaGrigo/SeenIt/pull/4): **MERGED**, base `main`, origem `security/b05-b3-sqlite-review`. A consulta ao GitHub retornou merge em 17/11/2026 às 12:45:45 UTC (09:45:45 em America/Sao_Paulo).

SHA exato da integração: **`85addcaaa6ca1bdea18cd8e3f422666c028ceb6a`**.

Os dois commits autorizados foram confirmados como ancestrais de `origin/main`:

| Commit | Mensagem |
| --- | --- |
| `5059c8ccc839ded51bb1d6d564572fd70414ad6a` | Fix: Corrige vulnerabilidade SQLite nos testes de integração |
| `7b1648c67104ba939c608275df402002f863d773` | Docs: Documenta diagnóstico e correção experimental do SQLite |

O fetch encontrou apenas a integração esperada na main; não havia alterações locais não commitadas na árvore principal. A main local estava em `f370c58d8f01dc646f04a0f92f778ae7b2878115` e foi atualizada com `git switch main` seguido de `git pull --ff-only origin main`. `main` e `origin/main` ficaram no SHA de integração, sem divergência. A árvore integrada é idêntica à árvore final do PR.

A branch `security/b05-b3-sqlite-review` foi preservada em `7b1648c67104ba939c608275df402002f863d773`. O worktree e a branch `maintenance-phase1` foram preservados em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, inclusive suas alterações locais preexistentes. Nenhuma branch ou worktree foi excluído. Os dois relatórios históricos mantêm o mesmo conteúdo versionado; o checkout do Git aplica a configuração local de finais de linha.

## 7. Deployment e validação manual informados pelo operador

O operador confirmou que o PR #4 foi integrado, que o deployment Vercel está **Ready** e que, após o merge, acessou o SeenIt em produção e verificou seu funcionamento normal. **Validação funcional pós-merge aprovada pelo operador; nenhum problema funcional relatado.**

Essa validação manual é uma informação do operador, distinta dos checks automatizados e das inspeções locais executados pelo Codex. O Codex não realizou testes manuais em produção nem consultou Render, Vercel ou Supabase nesta etapa.

O status Ready da Vercel não comprova a publicação, a versão ou a integridade do backend no Render. A validação manual informada também não constitui inspeção dos artefatos ou dependências desse backend.

## 8. Limitações, riscos residuais e rollback

A referência direta exige manutenção e reavaliação quando o pacote de origem passar a selecionar naturalmente a lib corrigida. Os checks cobrem os fluxos existentes, não toda a superfície SQLite ou uma PoC da CVE. SQLite em memória não reproduz integralmente PostgreSQL; outras arquiteturas e plataformas não foram executadas. A auditoria é de pacotes NuGet, sem certificação de SO, runtime ou imagem remota. Evidências temporárias podem ser removidas pelo sistema operacional; os resultados essenciais estão registrados nos relatórios e neste resumo.

Se uma regressão justificar rollback em etapa futura autorizada, remover somente a referência direta adicionada e restaurar o projeto retorna à cadeia anterior, sem modificar migrations ou produção; manter os relatórios como histórico. Isso reintroduz lib.e_sqlite3 2.1.11 e o alerta alto, exigindo avaliação explícita do risco e nova validação. Não foi executado rollback nesta etapa.

## 9. Próximos passos e encerramento

Não há pendência funcional relatada pelo operador nem bloqueio técnico identificado para o encerramento do B05-B3 no escopo validado. Recomenda-se manter o acompanhamento de advisories e revalidar o override quando a cadeia de dependências mudar. Uma eventual inspeção do backend implantado exige escopo próprio; não é resultado deste trabalho.

Para B05-B4, recomenda-se definir e aprovar o escopo de revisão de imagem, runtime e dependências .NET/nativas, incluindo digest/SBOM, auditoria, patches e avaliação coordenada de EF Core/Npgsql, conforme a proposta do [encerramento B05-B1](B05-B1-FINAL-SUMMARY.md). Essa proposta não registra verificações executadas nem autoriza atualizações ou alterações de infraestrutura; nenhuma atividade B05-B4 foi executada neste encerramento.

Na etapa B05-B3.6, este relatório foi criado sem commit, conforme solicitado. Naquela etapa não houve commit, push, deploy, atualização de dependências, alteração de serviços ou uso de reset, clean, rebase ou force. A etapa B05-B3.7 autoriza exclusivamente a revisão e o versionamento deste documento, com commit e push normal para main, preservando código, dependências, infraestrutura, branches e worktrees.

**B05-B3 PRONTO PARA ENCERRAMENTO**
