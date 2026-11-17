# B05-B3.2 — Experimento isolado de correção da vulnerabilidade SQLite

Data: 16/11/2026, America/Sao_Paulo.

**Resultado: aprovar experimentalmente a candidata A — referência direta a SQLitePCLRaw.lib.e_sqlite3 2.1.12 exclusivamente no projeto de integração.** O motor efetivamente carregado foi SQLite **3.53.3**, em Windows x64 e Linux x64. Nenhuma correção foi aplicada ao repositório original.

## 1. Ambiente, preparação e isolamento

- Branch original confirmada: `security/b05-b3-sqlite-review`.
- Base/HEAD: `f370c58d8f01dc646f04a0f92f778ae7b2878115`.
- O status inicial continha somente `docs/B05-B3-SQLITE-SECURITY-DIAGNOSIS.md` não rastreado, resultado esperado da etapa anterior. Nenhuma alteração inesperada foi identificada.
- O [diagnóstico B05-B3.1](B05-B3-SQLITE-SECURITY-DIAGNOSIS.md) foi preservado integralmente.
- `maintenance-phase1` permaneceu em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, no worktree já existente. Não foi executado comando dentro dele.
- Foram registrados SHA-256 dos oito `.csproj` e dos demais arquivos versionados de `backend`/`tools`, além do diagnóstico: **121 arquivos originais**, sem alterações de hash ao final. Os hashes dos projetos estão no anexo.

Diretório temporário, fora do Git:

```text
C:\Users\giovana.grigolato\AppData\Local\Temp\seenit-b05-b32-2e2ba40e0d004acf84bd0f8c0217d6d4
├─ control
├─ candidate-a
├─ linux-a
├─ engine-probe
├─ packages-inspection
├─ local-feed
├─ nuget-packages / nuget-http-cache
├─ linux-nuget-packages / linux-http-cache
└─ api-publish
```

As cópias são arquivos independentes, sem symlinks. Foram copiados os `.cs`/`.csproj` dos quatro projetos backend necessários aos checks, mantendo caminhos relativos e migrations inalteradas. Para auditar os quatro projetos de ferramentas foram copiados apenas seus `.csproj` e recursos SQL; as ferramentas não foram executadas. Não foram copiados `appsettings*`, launch settings, `.env`, segredos, credenciais, certificados, configurações ou arquivos de conexão de produção. As configurações sintéticas já presentes no código dos checks foram preservadas.

No Windows, um launcher temporário criou processos .NET com ambiente limpo, perfil/AppData temporários, cache NuGet separado e ambiente ASP.NET Production. Não herdou variáveis de conexão, tokens, proxies ou configurações da aplicação. Os testes foram executados no sandbox padrão, após revisão dos mocks e conexões; somente restore/auditoria/downloads precisaram de acesso externo autorizado. Não houve execução de `--postgres-only`, de ferramentas de auditoria de banco ou da API publicada.

No Linux, somente o diretório temporário foi montado. Restore consultou NuGet; **builds, probe e todos os checks foram executados em outro contêiner com `--network none`**, sem portas publicadas ou montagem do repositório original. O loopback do contêiner continuou disponível para Kestrel. Nenhum serviço de banco foi instalado/iniciado.

| Ambiente | SDK / MSBuild | Runtime / plataforma |
| --- | --- | --- |
| Windows | .NET SDK 10.0.301 / MSBuild 18.6.4 | .NET e ASP.NET Core 10.0.9; Windows x64 |
| Linux descartável | .NET SDK 10.0.404 / MSBuild 18.9.15 | .NET e ASP.NET Core 10.0.15; Ubuntu 24.04, linux-x64 |

Imagem Linux: `mcr.microsoft.com/dotnet/sdk:10.0-noble`, digest `sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb`. A imagem foi baixada para o Docker já disponível; não se instalou serviço permanente. Os SDKs/runtimes não são idênticos entre sistemas, limitação registrada na seção 12.

## 2. Pacotes e versões avaliados oficialmente

Os cinco `.nupkg` solicitados foram baixados diretamente de `https://api.nuget.org/v3-flatcontainer/`. Foram extraídos e inspecionados os nuspecs, dependências, targets e assets por RID. Também foram consultados os registros oficiais NuGet de cada versão e as [releases upstream](https://github.com/ericsink/SQLitePCL.raw/releases).

| Pacote | Nuspec / assets relevantes | Motor comprovado na inspeção | Tratamento |
| --- | --- | --- | --- |
| SQLitePCLRaw.lib.e_sqlite3 2.1.12 | Sem dependências gerenciadas declaradas; assets win-x64 e linux-x64 presentes; targets até net9.0 e uso validado em net10.0 | **3.53.3**, via export `sqlite3_libversion`; source ID abaixo | Selecionado para experimento A e depois validado por SQL e regressões |
| SQLitePCLRaw.lib.e_sqlite3 2.1.14 | Sem dependências gerenciadas declaradas; assets win-x64 e linux-x64 presentes | **3.54.0**, via export nativo | Inspeção somente; não promovido a outra candidata |
| SQLitePCLRaw.lib.e_sqlite3 3.53.3 | Grupos sem dependências para .NET Standard 2.0 / .NET Framework 4.7.1; assets Windows/Linux e outras plataformas | **3.53.3**, via export nativo | Inspeção somente; experimento B desnecessário |
| SQLitePCLRaw.bundle_e_sqlite3 2.1.12 | Grupo .NET Standard 2.0 exige provider.e_sqlite3 >=2.1.12 e lib.e_sqlite3 >=2.1.12; outros grupos específicos de plataforma | Não contém o motor; depende da lib selecionada | Nuspec inspecionado; não houve upgrade do bundle |
| SQLitePCLRaw.bundle_e_sqlite3 2.1.14 | Grupo .NET Standard 2.0 exige provider.e_sqlite3 >=2.1.14 e lib.e_sqlite3 >=2.1.14 | Não contém o motor | Nuspec inspecionado; não houve upgrade do bundle |

Os binários win-x64 e linux-x64 dos pacotes **lib 2.1.12 e lib 3.53.3 têm hashes SHA-256 idênticos**, respectivamente. Isso resolve a dúvida prática do diagnóstico anterior: o número do pacote de manutenção não é a versão do SQLite embarcado. O nuspec do bundle 2.1.12 realmente declara mínimo 2.1.12, mesmo que as notas upstream descrevam a manutenção em termos de SQLite 3.53.3. Não é necessário forçar a versão de pacote 3.53.3 para carregar esse motor.

Source ID de lib 2.1.12 e lib 3.53.3:

```text
2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62
```

Source ID de lib 2.1.14:

```text
2026-10-09 15:46:58 be8d059e9a49089ab2dce5ed26dd87aaf598fdc5fbe0b107c0dd758464bcd5e3
```

Nas libs 2.1.12/2.1.14 há RIDs Windows x86/x64/ARM64, Linux glibc/musl em diversas arquiteturas, macOS, Mac Catalyst e browser-wasm. Não há asset win-arm nesses dois pacotes; isso não afeta os alvos x64 desta validação. O pacote 3.53.3 também possui assets Android/iOS e arquivos estáticos Linux. Presença em arquivo não equivale a execução validada em todas essas plataformas.

Os registros de catálogo dos cinco pacotes consultados não trouxeram entradas de vulnerabilidade/depreciação. Isso é evidência auxiliar, não certificação de segurança. A aprovação usa também a auditoria atual, a versão nativa e os checks executados.

Fontes: [lib 2.1.12](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12), [lib 2.1.14](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.14), [lib 3.53.3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3), [bundle 2.1.12](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.12), [bundle 2.1.14](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14). Os arquivos efetivamente baixados, e não somente as páginas do catálogo, fundamentam os resultados de binários.

## 3. Modificação experimental

A única diferença de fonte entre `control` e `candidate-a` é uma linha adicionada ao `.csproj` **temporário** de SeenIt.RegisterIntegrationChecks:

```xml
<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3" Version="2.1.12" PrivateAssets="all" />
```

Foram preservados EF Core Sqlite 10.0.0, Npgsql/provider PostgreSQL 10.0.0, Mvc.Testing 10.0.7, bundle/provider/core SQLitePCLRaw 2.1.11, código da API, código dos testes e migrations. Não foram adicionados comentários de código. Não houve edição de `project.assets.json` ou supressão de avisos.

A referência direta é tecnicamente apropriada pela regra NuGet [direct-dependency-wins](https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution#direct-dependency-wins): o mínimo transitivo da lib era 2.1.11, compatível com a seleção 2.1.12. `PrivateAssets="all"` restringe propagação a consumidores do projeto; não impede a inclusão/carga da lib no próprio executável nem oculta o pacote da auditoria.

O probe foi criado como executável auxiliar separado em `engine-probe`, sem alterar o programa dos checks. Foi executado usando o `.deps.json` e `.runtimeconfig.json` reais do build de integração, carregando Microsoft.Data.Sqlite por reflexão. Assim, o probe utilizou os assets resolvidos pela candidata, sem introduzir uma cadeia NuGet alternativa.

## 4. Restore e avisos

O inventário original foi registrado antes dos restores. O controle isolado resolveu exatamente os mesmos IDs/versões dos oito assets originais. O controle reproduziu o **NU1903** esperado para lib 2.1.11.

O primeiro launcher excessivamente mínimo precisou receber APPDATA/LOCALAPPDATA temporários para o NuGet. Um restore inicial pela rede, lento em downloads grandes, foi interrompido exclusivamente após identificar seu processo pelo caminho temporário; nenhum processo da aplicação original foi interrompido. A repetição usou cópias de 64 `.nupkg` relevantes do cache já existente, lido sem alterações, mais o pacote candidato baixado oficialmente. O feed e os caches resultantes ficam somente no temporário.

O `NuGet.Config` temporário usa esse feed local para pacotes e `auditSources` apontando para nuget.org. A auditoria explícita também usou `--source https://api.nuget.org/v3/index.json`. Portanto, os pacotes locais não substituíram a fonte oficial de advisories.

Restore final da candidata A: **sucesso, zero avisos e erros**, sem NU1605, NU1608 ou NU1903. O restore Linux independente também teve sucesso, com a auditoria habilitada. Não foi usado `NoWarn`, `NuGetAuditSuppress` ou desativação de auditoria para obter aprovação.

Comandos principais, executados nas cópias:

```text
dotnet restore <projeto> --configfile <config-temporario> --verbosity minimal
dotnet nuget why backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj SQLitePCLRaw.lib.e_sqlite3
dotnet build <projeto> --no-restore --configuration Release
dotnet <saida-do-projeto>/SeenIt.RegisterIntegrationChecks.dll
dotnet package list --project <projeto> --no-restore --include-transitive --vulnerable --format json --source https://api.nuget.org/v3/index.json
```

## 5. Grafo NuGet efetivamente resolvido

```text
SeenIt.RegisterIntegrationChecks / net10.0
├─ SeenIt.Api (ProjectReference; grafo original preservado)
│  └─ Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 → Npgsql 10.0.0
├─ Microsoft.AspNetCore.Mvc.Testing 10.0.7
├─ SQLitePCLRaw.lib.e_sqlite3 2.1.12 [nova referência direta]
└─ Microsoft.EntityFrameworkCore.Sqlite 10.0.0
   ├─ SQLitePCLRaw.bundle_e_sqlite3 2.1.11
   │  ├─ SQLitePCLRaw.lib.e_sqlite3 2.1.12 [mesma seleção]
   │  └─ SQLitePCLRaw.provider.e_sqlite3 2.1.11
   │     └─ SQLitePCLRaw.core 2.1.11
   ├─ SQLitePCLRaw.core 2.1.11
   └─ Microsoft.EntityFrameworkCore.Sqlite.Core 10.0.0
      └─ Microsoft.Data.Sqlite.Core 10.0.0
```

O grafo completo foi registrado por `dotnet package list --include-transitive --format json` para os oito projetos candidatos. A comparação dos `project.assets.json` verificou todos os IDs/versões, além do conteúdo completo de `targets.net10.0` da API original, controle e candidata: **idêntico**. Windows e Linux resolveram a mesma cadeia SQLite acima. A combinação bundle/provider/core 2.1.11 + lib 2.1.12 é compatível nos testes executados; não se presume compatibilidade de combinações de bundles não testadas.

| Projeto | Pacotes no original / controle / candidata | Diferença candidata |
| --- | --- | --- |
| SeenIt.Api | 51 / 51 / 51 | Nenhuma |
| SeenIt.AvatarChecks | 20 / 20 / 20 | Nenhuma |
| SeenIt.Checks | 20 / 20 / 20 | Nenhuma |
| SeenIt.RegisterIntegrationChecks | 30 / 30 / 30 | Somente lib.e_sqlite3 2.1.11 → 2.1.12 |
| SeenIt.HistoricalAudit | 3 / 3 / 3 | Nenhuma |
| SeenIt.HistoricalAudit.Checks | 3 / 3 / 3 | Nenhuma |
| SeenIt.HistoricalAudit.Remote | 3 / 3 / 3 | Nenhuma |
| SeenIt.HistoricalAudit.RemoteChecks | 3 / 3 / 3 | Nenhuma |

## 6. Motor SQLite realmente carregado

O probe abriu `Data Source=:memory:`, executou `SELECT sqlite_version();`, `SELECT sqlite_source_id();`, `PRAGMA database_list`, criação de tabela, INSERT e SELECT. Conferiu o arquivo do banco vazio no pragma e listou o módulo nativo carregado no processo.

| Execução | sqlite_version() | Banco / CRUD | Módulo |
| --- | --- | --- | --- |
| Controle Windows | 3.49.1 | Memória / PASS | `control/.../runtimes/win-x64/native/e_sqlite3.dll` |
| Candidata Windows | **3.53.3** | Memória / PASS | `candidate-a/.../runtimes/win-x64/native/e_sqlite3.dll` |
| Candidata Linux sem rede | **3.53.3** | Memória / PASS | `/experiment/linux-a/.../runtimes/linux-x64/native/libe_sqlite3.so` |

O [advisory GHSA-2m69-gcr7-jv3q / CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) classifica como High o pacote <=2.1.11 e recomenda motor SQLite >=3.50.2. A [correção upstream 3.50.2](https://www.sqlite.org/releaselog/3_50_2.html) rejeita antecipadamente o excesso de termos agregados que causa o problema de memória. A candidata carrega motor superior ao mínimo; a decisão não decorre apenas do desaparecimento do advisory. O campo estruturado de versões NuGet corrigidas no GHSA ainda aparece sem preenchimento.

Hashes dos assets da candidata, iguais aos inspecionados no pacote oficial:

| Asset | SHA-256 |
| --- | --- |
| win-x64/e_sqlite3.dll | `B7385D722C83FB52142A00477A726723745916D22A555711EE89834C1111FB2E` |
| linux-x64/libe_sqlite3.so | `B0F5C48026FEDCF9F05CF31C6FADF9E1E001CA59CD85AA68B8B6814E52C18A07` |
| nupkg lib 2.1.12 | `4A22C02FFCF489792903CF263DEF9FCE27739716883F965F0B5EEFF1637430AC` |

## 7. Checks de regressão

Todos os builds necessários concluíram com sucesso. A candidata compilou com zero avisos em Windows e Linux; o build do controle de integração manteve somente o NU1903 esperado. Os três executáveis foram efetivamente executados; `dotnet test` não foi usado como substituto.

| Check | Controle Windows | Candidata Windows | Candidata Linux sem rede |
| --- | --- | --- | --- |
| AvatarChecks — 16 grupos | PASS | PASS | PASS |
| Checks — watch time e classificação anime | PASS | PASS | PASS |
| Checks — 72 casos HTTP de validação | PASS | PASS | PASS |
| CatalogResilienceChecks | PASS | PASS | PASS |
| DataIntegrityChecks | PASS | PASS | PASS |
| RegistrationIntegrationChecks — 73 casos HTTP, 16 usuários persistidos em memória | PASS | PASS | PASS |
| RateLimitingChecks | PASS | PASS | PASS |
| RateLimitingRegressionChecks — incluindo 20 logins sobrepostos | PASS | PASS | PASS |

As saídas completas dos três executáveis Windows foram comparadas entre controle e candidata: **idênticas**. Todas as cinco suítes chamadas pelo `Program.cs` padrão da integração concluíram. Solicitações inválidas de cadastro mantiveram zero comandos de banco, saves e hashes. Não houve DllNotFoundException nem EntryPointNotFoundException.

O provider permaneceu SQLite em memória. A factory continuou substituindo DbContext, storage e HTTP externo; as suítes de catálogo usaram HttpMessageHandler sintéticos. AvatarChecks usou handlers/repositórios falsos e Checks usou Kestrel em loopback, sem provider de banco real. Não foram usados Supabase, bancos reais ou o modo PostgreSQL. No Linux a impossibilidade de conexão externa foi adicionalmente imposta pelo namespace de rede do contêiner. No Windows não houve captura de pacotes: a confirmação se apoia no isolamento do processo/sandbox, revisão dos caminhos executados e mocks preservados, sem afirmar rastreamento de tráfego que não foi realizado.

## 8. Auditoria completa dos oito projetos

Foram executadas 16 consultas de vulnerabilidade: oito no controle e oito na candidata, incluindo diretos e transitivos, com fonte oficial explícita. Todas concluíram sem erro de consulta. Inventários completos também foram registrados para os oito projetos candidatos.

| Projeto | Controle | Candidata A |
| --- | --- | --- |
| SeenIt.Api | Nenhum reportado | Nenhum reportado |
| SeenIt.AvatarChecks | Nenhum reportado | Nenhum reportado |
| SeenIt.Checks | Nenhum reportado | Nenhum reportado |
| SeenIt.RegisterIntegrationChecks | lib.e_sqlite3 2.1.11 — High — GHSA-2m69-gcr7-jv3q | **Nenhum reportado** |
| SeenIt.HistoricalAudit | Nenhum reportado | Nenhum reportado |
| SeenIt.HistoricalAudit.Checks | Nenhum reportado | Nenhum reportado |
| SeenIt.HistoricalAudit.Remote | Nenhum reportado | Nenhum reportado |
| SeenIt.HistoricalAudit.RemoteChecks | Nenhum reportado | Nenhum reportado |

Nenhum novo alerta alto/crítico ou de outra severidade foi reportado na candidata. O restore registrou o índice oficial `https://api.nuget.org/v3/vulnerabilities/index.json`, base de `2026.11.14.05.29.55` e atualização `2026.11.16.17.30.06`. O cache de auditoria foi criado neste experimento e compartilhado entre as cópias Windows; não se reutilizou uma auditoria histórica como resultado atual. Ausência de alertas é relativa à base consultada.

## 9. Compatibilidade Windows/Linux

Windows x64 e Linux x64 glibc tiveram restore independente, build, abertura em memória, confirmação de versão/source ID e execução completa dos checks da candidata. O hash do `.so` carregado em Linux corresponde ao pacote inspecionado; os assets Windows/Linux estão presentes nos builds. Não foi necessário atualizar EF Core, Npgsql ou bundle.

Linux usou cache próprio e cópia de fontes própria para não sobrescrever assets gerados no Windows. Nenhum serviço permanente foi instalado e nenhum contêiner de banco foi utilizado. Outras arquiteturas, Linux musl, macOS, Android, iOS e .NET Framework não foram executados. Não se amplia a aprovação a essas plataformas.

## 10. Publish local descartável da API

Foi gerado **um único publish novo**, a partir da API intacta da candidata, sem iniciar o resultado:

```text
dotnet publish backend/SeenIt.Api/SeenIt.Api.csproj --no-restore --configuration Release --self-contained false --output <temporario>/api-publish -p:UseAppHost=false --verbosity minimal
```

Resultado: sucesso, **87 arquivos**, zero nomes de arquivos e zero correspondências em `SeenIt.Api.deps.json` para SQLitePCLRaw, Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.Sqlite e e_sqlite3 (incluindo `.dll`/`.so`).

O grafo de build da API, com seus 51 pacotes, foi comparado ao original e é idêntico. O manifesto de publish contém somente o subconjunto necessário ao runtime; a retirada de dependências de build/design/análise no publish não é mudança de versão ou introdução de dependências. A inspeção não encontrou bibliotecas novas em relação aos assets originais.

SHA-256 de `SeenIt.Api.deps.json` publicado: `BC24F10F2485B1B8AD71B5FDFA0D04D1A5DA8742A3AF8DFD9BCF7D69BE2B6F22`.

Esse publish é framework-dependent, sem RID explícito, gerado no Windows. Não equivale à construção ou inspeção da imagem Render; Dockerfile, Render e Vercel não foram alterados. A API publicada não foi executada, não houve deploy nem acesso à hospedagem.

## 11. Diferenças em relação ao controle e preservação

- Fontes: uma linha no csproj experimental de integração; todos os `.cs` e migrations copiados permaneceram iguais ao controle/original.
- Resolução: somente lib nativa 2.1.11 → 2.1.12; bundle/provider/core permaneceram 2.1.11 e todos os demais IDs/versões dos oito projetos ficaram iguais.
- Motor: controle 3.49.1 → candidata 3.53.3.
- Auditoria: um alerta alto no controle → zero alertas reportados na candidata.
- Checks Windows: saídas idênticas; candidata Linux passou nas mesmas suítes.
- API: fonte e grafo completo net10.0 preservados; publish inspecionado sem SQLite.
- Repositório original: nenhum `.csproj`, código, configuração, migration, Dockerfile ou arquivo do diagnóstico foi alterado. Nenhum restore/build/test/publish foi executado em seus projetos originais nesta etapa.

O único arquivo criado no Git durante B05-B3.2 é este relatório. Ao final, os dois relatórios B05-B3.1/B05-B3.2 permanecem não rastreados. HEAD, branches e worktrees existentes foram preservados. Não foram feitos commit, push ou PR.

## 12. Riscos, evidências e limitações

- A correção continua **somente experimental**: o projeto original ainda resolve a versão vulnerável até uma implementação autorizada de B05-B3.3.
- O override direto exige manutenção explícita; a cadeia futura deve ser reavaliada quando o pacote direto EF Core passar a selecionar uma lib corrigida naturalmente.
- Os testes cobrem os fluxos existentes, não toda a superfície de consultas SQLite ou uma prova de exploração. Não houve PoC da CVE.
- SQLite em memória não reproduz integralmente PostgreSQL, sobretudo locks, isolamento, encoding e traduções SQL. Não foi tentada substituição de provider.
- Windows e Linux foram validados em versões diferentes do SDK/runtime .NET 10; não se alega ensaio com ambientes byte a byte idênticos.
- Inspeção de libs 2.1.14/3.53.3 e bundles posteriores não equivale a uma aprovação de regressão dessas alternativas. A etapa B não foi executada porque A foi comprovada.
- A ausência de SQLite no publish local não certifica o estado de uma implantação remota, que não foi acessada.
- Diretório temporário e imagem SDK permanecem para revisão; não houve limpeza destrutiva. Arquivos temporários podem ser removidos pelo sistema operacional, por isso as conclusões, versões e hashes essenciais estão reproduzidos aqui.

Evidências brutas no temporário: `original-hashes.json`, `original-inventory.json`, `package-native-inspection.json`, `package-catalog-inspection.json`, `package-graph-comparison.json`, `api-publish-inspection.json`, logs `control-*`/`candidate-a-*` (stdout/stderr/código de saída), `linux-restore.log` e `linux-checks.log`. Os scripts e o probe também permanecem nesse diretório. Nenhum deles foi incluído no repositório.

## 13. Menor candidata aprovada

**Candidata A: SQLitePCLRaw.lib.e_sqlite3 2.1.12 como referência direta, PrivateAssets=all, somente no projeto de integração.** É a menor versão de manutenção entre as solicitadas, o menor diff avaliado e a única candidata submetida à regressão completa. O motor 3.53.3, os hashes, o grafo, a auditoria e os checks Windows/Linux comprovam sua aprovação experimental.

Não foi necessário testar um override para 3.53.3, atualizar o bundle inteiro, atualizar EF Core/Npgsql ou migrar para PostgreSQL.

## 14. Estratégia de rollback

Nenhuma reversão é necessária no repositório original: não recebeu o patch. A cópia de controle mantém a base intacta para comparação. Não copiar os csproj experimentais de volta automaticamente.

Se B05-B3.3 aplicar a referência e houver regressão, o rollback técnico consiste em retirar somente a linha adicionada e restaurar o grafo anterior, sem tocar em migrations ou configuração de produção. Isso reintroduz lib 2.1.11 e o alerta alto; deve ser tratado como retorno consciente ao risco documentado, não como correção. Esta etapa não executou esse rollback nem qualquer comando destrutivo.

## 15. Recomendação para B05-B3.3

**Aprovar a candidata A para implementação mínima em etapa posterior autorizada.** Aplicar apenas a referência direta documentada no csproj original de RegisterIntegrationChecks, após reconferir base/estado e advisories. Repetir restore, inspeção de grafo e versão nativa, os três executáveis de checks, auditoria dos oito projetos e inspeção de publish para confirmar que o resultado foi reproduzido no contexto final.

Manter EF Core, Npgsql, código da API/testes, migrations e infraestrutura intactos. Não fazer upgrade adicional sem necessidade. Futuros títulos/mensagens de commit devem ser em português brasileiro; nenhum commit, push ou PR está autorizado por este experimento.

**B05-B3.2 EXPERIMENTO CONCLUÍDO**

## Anexo A — SHA-256 dos projetos originais

Hashes registrados antes do experimento e conferidos ao final:

| Projeto | SHA-256 |
| --- | --- |
| SeenIt.Api | `79040AF396B449B56C780853C5FEB435A5A4F07BBCB4ACDBC5C1E2F48F7CA382` |
| SeenIt.AvatarChecks | `23C6E45D1225F8EAF8AEE4B6ED0AB21B5867B980A420A7915C8AB9DB61D5A194` |
| SeenIt.Checks | `DFE06DF79F0D828BDC77AFAA9E5C1946D778658B78635211EEBC693FC1993915` |
| SeenIt.RegisterIntegrationChecks | `05B93FA474B77685AD766E54917B97CF73E807C98B2BC51DCBA7A77F102A0EA5` |
| SeenIt.HistoricalAudit | `CB7621AE6BFAB33EAE1BF4A96757A4A2B978ADBC317F92BE8D095E9792FBAAED` |
| SeenIt.HistoricalAudit.Checks | `A926333D0E8A19E2422BA8761784CA13CA345E16190A956ED2CDDF53B6C67F49` |
| SeenIt.HistoricalAudit.Remote | `0B6175F81B6546D702997365568AF248EFBD16118656CA4DF6131046D894D636` |
| SeenIt.HistoricalAudit.RemoteChecks | `98C7BE3C3D4D34057557366A25BF05E91B78F6A4778D0C92C58D9CB0EC0C21EF` |

## Anexo B — Inventário completo resolvido na candidata Windows

Extraído dos assets experimentais, sem modificar os originais. Os pacotes originais/controle são os mesmos abaixo, substituindo somente lib.e_sqlite3/2.1.12 por 2.1.11 no projeto de integração. A classificação direta/transitiva consta nos inventários JSON e no diagnóstico B05-B3.1; a lib 2.1.12 passa a ser direta na candidata.

### SeenIt.Api

- `Humanizer.Core/2.14.1`
- `Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0`
- `Microsoft.AspNetCore.OpenApi/10.0.0`
- `Microsoft.Build.Framework/17.14.28`
- `Microsoft.Build.Tasks.Core/17.14.28`
- `Microsoft.Build.Utilities.Core/17.14.28`
- `Microsoft.Build/17.7.2`
- `Microsoft.CodeAnalysis.Analyzers/3.11.0`
- `Microsoft.CodeAnalysis.Common/4.14.0`
- `Microsoft.CodeAnalysis.CSharp.Workspaces/4.14.0`
- `Microsoft.CodeAnalysis.CSharp/4.14.0`
- `Microsoft.CodeAnalysis.Workspaces.Common/4.14.0`
- `Microsoft.CodeAnalysis.Workspaces.MSBuild/4.14.0`
- `Microsoft.EntityFrameworkCore.Abstractions/10.0.0`
- `Microsoft.EntityFrameworkCore.Analyzers/10.0.0`
- `Microsoft.EntityFrameworkCore.Design/10.0.0`
- `Microsoft.EntityFrameworkCore.Relational/10.0.0`
- `Microsoft.EntityFrameworkCore/10.0.0`
- `Microsoft.Extensions.DependencyModel/10.0.0`
- `Microsoft.IdentityModel.Abstractions/8.0.1`
- `Microsoft.IdentityModel.JsonWebTokens/8.0.1`
- `Microsoft.IdentityModel.Logging/8.0.1`
- `Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1`
- `Microsoft.IdentityModel.Protocols/8.0.1`
- `Microsoft.IdentityModel.Tokens/8.0.1`
- `Microsoft.NET.StringTools/17.14.28`
- `Microsoft.OpenApi/2.7.5`
- `Mono.TextTemplating/3.0.0`
- `Newtonsoft.Json/13.0.3`
- `Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0`
- `Npgsql/10.0.0`
- `SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0`
- `SkiaSharp.NativeAssets.macOS/4.155.0`
- `SkiaSharp.NativeAssets.Win32/4.155.0`
- `SkiaSharp/4.155.0`
- `System.CodeDom/9.0.0`
- `System.Composition.AttributedModel/9.0.0`
- `System.Composition.Convention/9.0.0`
- `System.Composition.Hosting/9.0.0`
- `System.Composition.Runtime/9.0.0`
- `System.Composition.TypedParts/9.0.0`
- `System.Composition/9.0.0`
- `System.Configuration.ConfigurationManager/9.0.0`
- `System.Formats.Nrbf/9.0.0`
- `System.IdentityModel.Tokens.Jwt/8.0.1`
- `System.Reflection.MetadataLoadContext/7.0.0`
- `System.Resources.Extensions/9.0.0`
- `System.Security.Cryptography.Pkcs/9.0.22`
- `System.Security.Cryptography.ProtectedData/9.0.0`
- `System.Security.Permissions/9.0.0`
- `System.Windows.Extensions/9.0.0`

### SeenIt.AvatarChecks

- `Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0`
- `Microsoft.AspNetCore.OpenApi/10.0.0`
- `Microsoft.EntityFrameworkCore.Abstractions/10.0.0`
- `Microsoft.EntityFrameworkCore.Analyzers/10.0.0`
- `Microsoft.EntityFrameworkCore.Relational/10.0.0`
- `Microsoft.EntityFrameworkCore/10.0.0`
- `Microsoft.IdentityModel.Abstractions/8.0.1`
- `Microsoft.IdentityModel.JsonWebTokens/8.0.1`
- `Microsoft.IdentityModel.Logging/8.0.1`
- `Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1`
- `Microsoft.IdentityModel.Protocols/8.0.1`
- `Microsoft.IdentityModel.Tokens/8.0.1`
- `Microsoft.OpenApi/2.7.5`
- `Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0`
- `Npgsql/10.0.0`
- `SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0`
- `SkiaSharp.NativeAssets.macOS/4.155.0`
- `SkiaSharp.NativeAssets.Win32/4.155.0`
- `SkiaSharp/4.155.0`
- `System.IdentityModel.Tokens.Jwt/8.0.1`

### SeenIt.Checks

- `Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0`
- `Microsoft.AspNetCore.OpenApi/10.0.0`
- `Microsoft.EntityFrameworkCore.Abstractions/10.0.0`
- `Microsoft.EntityFrameworkCore.Analyzers/10.0.0`
- `Microsoft.EntityFrameworkCore.Relational/10.0.0`
- `Microsoft.EntityFrameworkCore/10.0.0`
- `Microsoft.IdentityModel.Abstractions/8.0.1`
- `Microsoft.IdentityModel.JsonWebTokens/8.0.1`
- `Microsoft.IdentityModel.Logging/8.0.1`
- `Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1`
- `Microsoft.IdentityModel.Protocols/8.0.1`
- `Microsoft.IdentityModel.Tokens/8.0.1`
- `Microsoft.OpenApi/2.7.5`
- `Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0`
- `Npgsql/10.0.0`
- `SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0`
- `SkiaSharp.NativeAssets.macOS/4.155.0`
- `SkiaSharp.NativeAssets.Win32/4.155.0`
- `SkiaSharp/4.155.0`
- `System.IdentityModel.Tokens.Jwt/8.0.1`

### SeenIt.RegisterIntegrationChecks

- `Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0`
- `Microsoft.AspNetCore.Mvc.Testing/10.0.7`
- `Microsoft.AspNetCore.OpenApi/10.0.0`
- `Microsoft.AspNetCore.TestHost/10.0.7`
- `Microsoft.Data.Sqlite.Core/10.0.0`
- `Microsoft.EntityFrameworkCore.Abstractions/10.0.0`
- `Microsoft.EntityFrameworkCore.Analyzers/10.0.0`
- `Microsoft.EntityFrameworkCore.Relational/10.0.0`
- `Microsoft.EntityFrameworkCore.Sqlite.Core/10.0.0`
- `Microsoft.EntityFrameworkCore.Sqlite/10.0.0`
- `Microsoft.EntityFrameworkCore/10.0.0`
- `Microsoft.Extensions.DependencyModel/10.0.7`
- `Microsoft.IdentityModel.Abstractions/8.0.1`
- `Microsoft.IdentityModel.JsonWebTokens/8.0.1`
- `Microsoft.IdentityModel.Logging/8.0.1`
- `Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1`
- `Microsoft.IdentityModel.Protocols/8.0.1`
- `Microsoft.IdentityModel.Tokens/8.0.1`
- `Microsoft.OpenApi/2.7.5`
- `Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0`
- `Npgsql/10.0.0`
- `SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0`
- `SkiaSharp.NativeAssets.macOS/4.155.0`
- `SkiaSharp.NativeAssets.Win32/4.155.0`
- `SkiaSharp/4.155.0`
- `SQLitePCLRaw.bundle_e_sqlite3/2.1.11`
- `SQLitePCLRaw.core/2.1.11`
- `SQLitePCLRaw.lib.e_sqlite3/2.1.12`
- `SQLitePCLRaw.provider.e_sqlite3/2.1.11`
- `System.IdentityModel.Tokens.Jwt/8.0.1`

### SeenIt.HistoricalAudit

- `Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0`
- `Microsoft.Extensions.Logging.Abstractions/10.0.0`
- `Npgsql/10.0.3`

### SeenIt.HistoricalAudit.Checks

- `Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0`
- `Microsoft.Extensions.Logging.Abstractions/10.0.0`
- `Npgsql/10.0.3`

### SeenIt.HistoricalAudit.Remote

- `Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0`
- `Microsoft.Extensions.Logging.Abstractions/10.0.0`
- `Npgsql/10.0.3`

### SeenIt.HistoricalAudit.RemoteChecks

- `Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0`
- `Microsoft.Extensions.Logging.Abstractions/10.0.0`
- `Npgsql/10.0.3`
