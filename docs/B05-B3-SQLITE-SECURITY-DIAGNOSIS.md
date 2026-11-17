# B05-B3.1 — Diagnóstico de segurança das dependências de testes .NET

## 1. Escopo e preparação

Diagnóstico em 16/11/2026 (America/Sao_Paulo), sem atualização de dependências, execução da aplicação ou acesso a bancos. Único arquivo criado: este relatório.

- `main` inicialmente limpa; `git fetch origin` executado com sucesso após liberação da restrição do sandbox sobre `.git/FETCH_HEAD`.
- `HEAD`, `main` e `origin/main`: `f370c58d8f01dc646f04a0f92f778ae7b2878115`.
- Branch criada: `security/b05-b3-sqlite-review`.
- Branches existentes preservadas. `maintenance-phase1` permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, no worktree preexistente. Nenhum comando foi executado dentro desse worktree.
- Sem commit, push, deploy, alteração de código, comentários de código, migrations, Docker ou Render. B05-B1/B2 não foram reabertos.

## 2. Ambiente .NET e inventário

Windows x64, SDK selecionado `10.0.301`, MSBuild `18.6.4`, host/runtime .NET e ASP.NET Core `10.0.9`. Também instalados SDK `6.0.428` e runtimes `6.0.36`, que não são o alvo dos projetos. Todos os oito projetos usam `net10.0`, nullable e implicit usings habilitados. API usa `Microsoft.NET.Sdk.Web`; os outros usam `Microsoft.NET.Sdk` e `OutputType=Exe`.

Não foram encontrados na árvore principal versionada arquivos `.sln`/`.slnx`, `global.json`, `Directory.Packages.props`, `Directory.Build.props`, `NuGet.Config`/`nuget.config` ou `packages.lock.json` do NuGet. A busca física adicional em `backend` e `tools` também não encontrou esses arquivos. Os `package-lock.json` da raiz/frontend são npm. Fonte efetiva informada por `dotnet nuget list source`: somente `https://api.nuget.org/v3/index.json`, habilitada. Não se infere ausência de configuração NuGet de usuário/máquina.

| Projeto (caminho relativo) | Finalidade / referências diretas |
| --- | --- |
| `backend/SeenIt.Api` | Produção. SkiaSharp e NativeAssets.Linux.NoDependencies `4.155.0`; JwtBearer e OpenApi ASP.NET `10.0.0`; Microsoft.OpenApi `2.7.5`; System.Security.Cryptography.Pkcs `9.0.22` (PrivateAssets=all); EF Core Design `10.0.0` (PrivateAssets=all); Npgsql.EntityFrameworkCore.PostgreSQL `10.0.0` |
| `backend/SeenIt.AvatarChecks` | Executável de verificações; ProjectReference à API e FrameworkReference Microsoft.AspNetCore.App; nenhum PackageReference próprio |
| `backend/SeenIt.Checks` | Executável de verificações; ProjectReference à API e FrameworkReference Microsoft.AspNetCore.App; nenhum PackageReference próprio |
| `backend/SeenIt.RegisterIntegrationChecks` | ProjectReference à API; Microsoft.AspNetCore.Mvc.Testing `10.0.7`; Microsoft.EntityFrameworkCore.Sqlite `10.0.0`; FrameworkReference Microsoft.AspNetCore.App |
| `tools/SeenIt.HistoricalAudit` | Ferramenta de auditoria; Npgsql `10.0.3` |
| `tools/SeenIt.HistoricalAudit.Checks` | ProjectReference a HistoricalAudit; sem PackageReference próprio |
| `tools/SeenIt.HistoricalAudit.Remote` | ProjectReference a HistoricalAudit; sem PackageReference próprio |
| `tools/SeenIt.HistoricalAudit.RemoteChecks` | ProjectReference a HistoricalAudit.Remote; sem PackageReference próprio |

Os checks são executáveis com asserções próprias, sem xUnit, NUnit, MSTest ou Microsoft.NET.Test.Sdk declarados. Integração utiliza WebApplicationFactory, ASP.NET Core TestHost e Kestrel para cenários HTTP. O inventário completo de pacotes resolvidos, inclusive transitivos, está no anexo.

## 3. Resultado da auditoria atual

Executado para cada um dos oito `.csproj`, com o SDK 10:

```powershell
dotnet package list --project <projeto.csproj> --include-transitive --vulnerable --format json
dotnet nuget why backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj SQLitePCLRaw.lib.e_sqlite3
dotnet nuget list source
```

A primeira tentativa de consulta NuGet no sandbox encontrou bloqueio de socket; a repetição autorizada fora dele consultou nuget.org e terminou com código 0. A auditoria pode efetuar restore implícito e atualizar caches/`obj`; não foi comandado upgrade ou alteração de versão. O resultado abaixo é da execução com acesso à fonte, não da tentativa bloqueada.

| Projeto | Resultado NuGet (diretos + transitivos) |
| --- | --- |
| SeenIt.Api | Nenhum pacote vulnerável reportado |
| SeenIt.AvatarChecks | Nenhum pacote vulnerável reportado |
| SeenIt.Checks | Nenhum pacote vulnerável reportado |
| SeenIt.RegisterIntegrationChecks | **SQLitePCLRaw.lib.e_sqlite3 2.1.11 — High — GHSA-2m69-gcr7-jv3q** (transitivo) |
| SeenIt.HistoricalAudit | Nenhum pacote vulnerável reportado |
| SeenIt.HistoricalAudit.Checks | Nenhum pacote vulnerável reportado |
| SeenIt.HistoricalAudit.Remote | Nenhum pacote vulnerável reportado |
| SeenIt.HistoricalAudit.RemoteChecks | Nenhum pacote vulnerável reportado |

O alerta histórico continua presente, agora confirmado por consulta atual. “Nenhum reportado” descreve a base NuGet consultada e não certifica ausência universal de falhas.

## 4. Árvore de dependências e artefatos

Grafo confirmado por `dotnet nuget why` e `obj/project.assets.json`:

```text
SeenIt.RegisterIntegrationChecks (net10.0)
├─ SeenIt.Api (ProjectReference)
│  └─ Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 → Npgsql 10.0.0
├─ Microsoft.AspNetCore.Mvc.Testing 10.0.7
└─ Microsoft.EntityFrameworkCore.Sqlite 10.0.0 [direto responsável]
   ├─ SQLitePCLRaw.bundle_e_sqlite3 2.1.11
   │  ├─ SQLitePCLRaw.lib.e_sqlite3 2.1.11 [vulnerável; código nativo]
   │  └─ SQLitePCLRaw.provider.e_sqlite3 2.1.11
   │     └─ SQLitePCLRaw.core 2.1.11
   ├─ SQLitePCLRaw.core 2.1.11
   └─ Microsoft.EntityFrameworkCore.Sqlite.Core 10.0.0
      ├─ Microsoft.Data.Sqlite.Core 10.0.0 → SQLitePCLRaw.core 2.1.11
      └─ Microsoft.EntityFrameworkCore.Relational 10.0.0
```

Somente RegisterIntegrationChecks resolve pacotes SQLite nos oito `project.assets.json`. API/AvatarChecks/Checks resolvem EF Core e provider PostgreSQL `10.0.0`; as quatro ferramentas resolvem Npgsql `10.0.3`.

No build local de integração (`bin/Debug/net10.0`) há Microsoft.Data.Sqlite.dll, Microsoft.EntityFrameworkCore.Sqlite.dll, assemblies SQLitePCLRaw e assets nativos por RID, inclusive `runtimes/win-x64/native/e_sqlite3.dll` e `runtimes/linux-x64/native/libe_sqlite3.so`. A dependência não é apenas metadado de desenvolvimento: é carregada ao abrir a conexão dos checks. Um publish desse projeto deve ser tratado como portador da biblioteca até verificação do artefato específico.

Nos 12 manifestos `.deps.json` encontrados sob `backend/SeenIt.Api` (Debug, Release e diretórios locais de validações, incluindo BuildHost), houve zero correspondências a SQLitePCLRaw, Microsoft.Data.Sqlite e EntityFrameworkCore.Sqlite. Não foram encontrados `SeenIt.Api.deps.json` nem arquivos com SQLite no nome sob `artifacts`. Esses artefatos locais preexistentes são evidência auxiliar; não foram reconstruídos nem certificados como imagem em execução.

## 5. Advisory e versões afetadas

[GitHub Advisory GHSA-2m69-gcr7-jv3q / CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q): severidade **High** na classificação usada pelo NuGet, faixa afetada do pacote `<= 2.1.11`. A página ainda informa nenhuma versão NuGet corrigida no campo estruturado. Isso não permite concluir que inexista atualização: há versões posteriores publicadas, que precisam ser avaliadas pelos binários e metadados efetivos.

A falha envolve excesso de termos agregados em consulta SQLite e pode causar corrupção de memória. O motor deve ser `>= 3.50.2` para essa CVE; essa exigência é do SQLite nativo, não uma versão de pacote EF Core. O [changelog oficial SQLite 3.50.2](https://www.sqlite.org/releaselog/3_50_2.html) registra a rejeição antecipada desse excesso. A [release SQLitePCLRaw 2.1.11](https://github.com/ericsink/SQLitePCL.raw/releases/tag/v2.1.11) informa motor SQLite `3.49.1`.

[SQLitePCLRaw.lib.e_sqlite3 2.1.12](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12) já existe fora da faixa afetada; também estão listadas 2.1.13 e 2.1.14. A descrição consultada de 2.1.12 não identifica precisamente a versão do motor, portanto ausência do alerta isoladamente não basta para aprová-la. O candidato [lib.e_sqlite3 3.53.3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3) explicita que seus três primeiros números correspondem ao SQLite embarcado e mantém o nome nativo `e_sqlite3`; é uma alternativa com motor identificável acima do mínimo.

As [notas upstream](https://github.com/ericsink/SQLitePCL.raw/releases) descrevem a manutenção v2.1.12 apontando para lib 3.53.3; já os metadados NuGet consultados do [bundle 2.1.12](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.12) declaram mínimo lib 2.1.12. Há divergência entre essas fontes: não assumir que referenciar esse bundle selecionará 3.53.3. Em B05-B3.2 será necessário inspecionar o nuspec e o grafo realmente restaurado, além de `SELECT sqlite_version()`.

## 6. Impacto prático e alcance

`RegistrationFactory.cs` cria `SqliteConnection("Data Source=:memory:")`, abre a conexão e troca o DbContext Npgsql por `UseSqlite(Connection)` no caminho padrão. Usa configuração sintética, substitui storage e bloqueia chamadas HTTP externas. O ambiente chamado Production dentro da factory é configuração do teste; não constitui conexão à produção. `EnsureCreated` cria schema em memória, sem aplicar migrations. A memória é descartada ao fechar a conexão.

O `Program.cs` de integração executa por padrão CatalogResilienceChecks, DataIntegrityChecks, RegistrationIntegrationChecks, RateLimitingChecks e RateLimitingRegressionChecks, todos apoiados nessa factory SQLite. A opção explícita `--postgres-only` executa PostgresIntegrityChecks com endereço loopback e configuração descartável fixa; **não foi executada** nesta etapa. A factory ainda instancia um objeto SqliteConnection mesmo nesse modo; remover a tecnologia exige revisar essa dependência também.

Não foi identificado caminho que permita a um cliente remoto executar SQL arbitrário no SQLite desses checks. As consultas são produzidas pelo código/EF com dados sintéticos; não foi identificado carregamento de banco SQLite externo ou endpoint de SQL livre. Isso reduz a explorabilidade observada, mas a execução é nativa no processo do desenvolvedor/CI: modo em memória não elimina corrupção de memória. Código de teste ou entradas SQL não confiáveis ampliariam o risco. Nenhuma prova de exploração foi executada.

Na API, `Program.cs` seleciona apenas `UseNpgsql`. A direção da referência é checks → API; dependências exclusivas dos checks não são transmitidas no sentido inverso. O Dockerfile existente em `backend/SeenIt.Api/Dockerfile` restaura/publica API, AvatarChecks e Checks; não copia/restaura/publica RegisterIntegrationChecks. A etapa final copia `/out/api`, além do marcador das verificações e certificado. Portanto, o pipeline versionado para Render não inclui essa biblioteca SQLite. Não foi acessada a imagem implantada, configuração remota ou conta Render: a ausência na implantação real é inferência do pipeline e grafo atuais, não atestado do servidor remoto. Empacotamento manual de todos os bins/checks poderia alterar esse alcance e deve ser evitado.

## 7. Alternativas de correção

Nenhuma alternativa foi aplicada ou restaurada experimentalmente nesta etapa.

| Alternativa / versões candidatas | Alteração necessária e compatibilidade | Regressão / impacto nos testes | Benefícios e limitações |
| --- | --- | --- | --- |
| Patch da lib atual: 2.1.11 → 2.1.12 ou posterior 2.1.x (2.1.14 listada) | Referência direta somente no csproj de integração, após confirmar motor >= 3.50.2 e assets dos RIDs usados | Baixo a moderado; preserva EF 10.0.0, mas muda motor/empacotamento nativo; validar carga e resultados | Menor alteração potencial. 2.1.12 está fora da faixa GHSA, mas a versão nativa não ficou comprovada nesta consulta; não aprovar apenas por sumiço do alerta |
| Atualizar o pacote direto Microsoft.EntityFrameworkCore.Sqlite 10.0.0 → 10.0.11 | Metadados de 10.0.11 exigem Sqlite.Core >=10.0.11, bundle/core SQLitePCLRaw >=2.1.12 e diversos Microsoft.Extensions >=10.0.11; conferir EF Relational e Npgsql no grafo combinado | Moderado; atualiza mais assemblies e pode gerar desalinhamento entre API EF 10.0.0 e testes. Executar checks da API também | Resolve pela origem direta e reduz override mantido localmente; não é simples troca exclusiva do binário vulnerável nem garantia de motor sem inspeção |
| Override explícito da lib para 3.53.3, mantendo bundle/provider/core 2.1.11 | Uma referência a SQLitePCLRaw.lib.e_sqlite3 3.53.3 no projeto de integração, idealmente PrivateAssets=all. .NET Standard 2.0 compatível com net10.0; nome nativo preservado; mínimos atuais permitem versão superior | Baixo a moderado, condicionado a compatibilidade ABI/RID e smoke test. Preserva EF e provider gerenciado | Motor identificado como 3.53.3, acima do mínimo; responsabilidade explícita de manter a escolha. Mudança de número major da lib nativa não equivale a atualizar todos os pacotes SQLitePCLRaw para major 3 |
| Override do bundle 2.1.11 → 2.1.14 (ou manutenção 2.1.12) | Bundle 2.1.14 exige lib/provider >=2.1.14; sobe cadeia coordenada; confirmar motor e core resolvidos. Bundle 2.1.12 tem divergência documental acima | Moderado; mais pacotes nativos/gerenciados mudam, mas sem trocar EF | Preferível se lib isolada não for compatível; não assumir igualdade entre número do bundle e do motor. Migração ao bundle 3.0.5 envolve pacote nativo SQLite 3.53.4, sendo escopo maior |
| Substituir SQLite por PostgreSQL local descartável | Manter família Npgsql EF 10.x compatível, provisionar PostgreSQL isolado com versão explícita alinhada à produção antes de implementar; versão do servidor de produção não foi consultada. Adaptar factory, remoção da conexão SQLite, preparação e limpeza por teste | Alto para esta correção; muda lifecycle, paralelismo, disponibilidade e tempo de execução. Aproveitar conceito do modo --postgres-only sem assumir que cobre toda a suíte | Maior fidelidade ao banco real, sem Supabase. Exige infraestrutura local e revalidação das cinco suítes; sem justificativa para ser primeiro passo de patch |
| Manutenção temporária de 2.1.11 | Nenhuma alteração técnica; registrar aceite com responsável, prazo e escopo de testes | Sem regressão imediata; mantém exposição nativa e alerta | Possível contingência se candidatos falharem. Não é correção nem recomendação preferencial, pois existem candidatos publicados |

Fontes de metadados: [EF Core Sqlite 10.0.11](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.11), [bundle 2.1.14](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14), [lib 3.53.3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3). As versões indicadas são candidatas verificadas em catálogo, não promessa de que sejam as mais recentes nem de compatibilidade executada.

## 8. Compatibilidade e diferenças de comportamento

NuGet PackageReference permite referência direta para escolher uma dependência transitiva pela regra [direct-dependency-wins](https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution#direct-dependency-wins). Não usar overrides npm, binding redirects ou editar `project.assets.json`. Os mínimos do grafo atual são 2.1.11, sem limite superior registrado para a lib. Uma referência só nos checks não exige contaminar o csproj da API. `PrivateAssets=all` impede propagação a consumidores; não remove o asset nativo necessário para executar o próprio teste nem torna um pacote seguro.

SQLite e PostgreSQL não são equivalentes: traduções LINQ/SQL, tipos, comparações/collation, constraints, encoding e concorrência podem divergir. A suíte de cadastro já documenta que SQLite não certifica encoding PostgreSQL e seus erros. Em `SeriesMutation.cs`, Npgsql usa ReadCommitted, advisory lock transacional e timeouts locais; o caminho SQLite usa Serializable e não executa esses comandos PostgreSQL. Os testes SQLite não comprovam esse comportamento concorrente específico. A [orientação EF Core](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) explica essas limitações de substituição de providers.

## 9. Riscos

- Segurança: alerta alto real em processo local de testes; ausência de caminho remoto identificado não autoriza suprimir o advisory.
- Compatibilidade: atualização nativa pode mudar planner, limites e empacotamento por arquitetura. Confirmar win-x64 e linux-x64 quando Linux integrar a execução dos checks.
- Resolução: ausência de lock NuGet e divergência entre release notes e catálogo tornam essencial registrar versões efetivamente resolvidas.
- Escopo: upgrade amplo EF/Npgsql ou troca de banco pode trazer regressões desnecessárias para corrigir este achado.
- Evidência: presença nos checks foi verificada em disco; ausência no Render efetivo não foi inspecionada remotamente.

## 10. Recomendação para B05-B3.2

Priorizar **a menor mudança no csproj de RegisterIntegrationChecks que resulte em motor comprovadamente corrigido**, mantendo EF Core/Npgsql e código funcional atuais. Primeiro verificar os pacotes de manutenção 2.1.x (incluindo 2.1.14), seus nuspecs, binários e advisories; se o motor corrigido estiver comprovado e os checks passarem, preferir esse patch.

Se essa comprovação não estiver disponível ou houver problema de empacotamento, o candidato concreto seguinte é referência direta de **SQLitePCLRaw.lib.e_sqlite3 3.53.3 somente nos checks**, conservando bundle/provider/core atuais quando compatíveis. A origem upstream documenta a combinação de manutenção com essa lib; ainda é obrigatório validar o grafo e a carga nativa. Se a troca isolada não for compatível, testar bundle de manutenção coordenado antes de atualizar toda a família EF. Não selecionar apenas pela ordem numérica das versões nem silenciar NU1903.

Atualização do pacote direto EF Core Sqlite é alternativa válida, porém de maior alcance transitivo. PostgreSQL descartável deve permanecer evolução separada da estratégia de testes. Manutenção do risco só como contingência documentada, com prazo. Nenhuma correção está aprovada por teste nesta etapa de diagnóstico.

## 11. Plano de testes para B05-B3.2

1. Confirmar árvore limpa/base e escolher uma única alternativa; registrar diff restrito e versões antes/depois. Não alterar migrations, Docker ou Render para esse patch.
2. Restaurar o projeto de integração com acesso ao NuGet; inspecionar nuspec, `project.assets.json` e `dotnet nuget why`. Rejeitar downgrade/conflito ou NU1605/NU1608 e investigar novos avisos.
3. Confirmar o SQLite realmente carregado com `SELECT sqlite_version()` em conexão efêmera isolada (>=3.50.2 para esta CVE), bem como origem do asset por RID. Não confundir versão de pacote com versão do motor.
4. Reexecutar auditoria direta/transitiva nos oito projetos. Exigir desaparecimento do alerta pela troca real do pacote e não por supressão; avaliar novos advisories.
5. Compilar e executar os executáveis AvatarChecks, Checks e RegisterIntegrationChecks. No último, usar o modo padrão, cobrindo catálogo/resiliência, integridade, cadastro, rate limiting e regressões. `dotnet test` sozinho não executa essas asserções de console.
6. Verificar que os checks continuam usando SQLite em memória, sem conexões externas e sem migrations; comparar resultados e erros HTTP esperados. Validar criação/descartes de schema e persistência das asserções de ausência de escrita em rejeições.
7. Confirmar carga nativa em Windows x64 e, se os checks rodarem nesse ambiente, Linux x64 isolado; verificar DllNotFoundException/EntryPointNotFoundException e assets copiados.
8. Gerar e inspecionar um publish local isolado da API, sem execução/deploy: ausência de SQLitePCLRaw, Microsoft.Data.Sqlite e libe_sqlite3 nos arquivos e `.deps.json`. Comparar grafo da API antes/depois.
9. Não executar `--postgres-only` automaticamente. Se necessário em outra etapa autorizada, utilizar exclusivamente PostgreSQL local descartável e confirmar isolamento primeiro; nunca credenciais/DB reais.

## 12. Limitações e encerramento

Não houve PoC, atualização experimental, build/publish novo ou execução de suítes nesta etapa; não são necessários para documentar o grafo e a auditoria, e não se reivindica validação executável de candidatos. Não houve acesso a Supabase, banco real, Render ou imagem remota. SDK/runtime e banco nativo foram inventariados, mas a auditoria realizada é de pacotes NuGet, não scanner completo de runtime, SO ou imagem.

A versão do motor dos candidatos 2.1.x e a divergência de metadados precisam ser resolvidas na implementação. O advisory não preenche versão corrigida NuGet, apesar de haver lançamentos posteriores. Os dados refletem as fontes consultadas na data indicada. Futuros commits, se autorizados, devem ter mensagem em português brasileiro; nenhum foi criado aqui.

**B05-B3.1 DIAGNÓSTICO CONCLUÍDO**

## Anexo — inventário completo dos pacotes resolvidos

Extraído dos oito `obj/project.assets.json` após a auditoria. Cada entrada registra ID/versão resolvida; a classificação direta/transitiva é determinada pelos PackageReference próprios listados na seção 2. ProjectReference e FrameworkReference não são pacotes NuGet. Nenhum arquivo de assets é incluído como entrega.

### SeenIt.Api

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Humanizer.Core/2.14.1 | Transitiva |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Direta |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Direta |
| Microsoft.Build.Framework/17.14.28 | Transitiva |
| Microsoft.Build.Tasks.Core/17.14.28 | Transitiva |
| Microsoft.Build.Utilities.Core/17.14.28 | Transitiva |
| Microsoft.Build/17.7.2 | Transitiva |
| Microsoft.CodeAnalysis.Analyzers/3.11.0 | Transitiva |
| Microsoft.CodeAnalysis.Common/4.14.0 | Transitiva |
| Microsoft.CodeAnalysis.CSharp.Workspaces/4.14.0 | Transitiva |
| Microsoft.CodeAnalysis.CSharp/4.14.0 | Transitiva |
| Microsoft.CodeAnalysis.Workspaces.Common/4.14.0 | Transitiva |
| Microsoft.CodeAnalysis.Workspaces.MSBuild/4.14.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Design/10.0.0 | Direta |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitiva |
| Microsoft.Extensions.DependencyModel/10.0.0 | Transitiva |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitiva |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitiva |
| Microsoft.NET.StringTools/17.14.28 | Transitiva |
| Microsoft.OpenApi/2.7.5 | Direta |
| Mono.TextTemplating/3.0.0 | Transitiva |
| Newtonsoft.Json/13.0.3 | Transitiva |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Direta |
| Npgsql/10.0.0 | Transitiva |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Direta |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitiva |
| SkiaSharp/4.155.0 | Direta |
| System.CodeDom/9.0.0 | Transitiva |
| System.Composition.AttributedModel/9.0.0 | Transitiva |
| System.Composition.Convention/9.0.0 | Transitiva |
| System.Composition.Hosting/9.0.0 | Transitiva |
| System.Composition.Runtime/9.0.0 | Transitiva |
| System.Composition.TypedParts/9.0.0 | Transitiva |
| System.Composition/9.0.0 | Transitiva |
| System.Configuration.ConfigurationManager/9.0.0 | Transitiva |
| System.Formats.Nrbf/9.0.0 | Transitiva |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitiva |
| System.Reflection.MetadataLoadContext/7.0.0 | Transitiva |
| System.Resources.Extensions/9.0.0 | Transitiva |
| System.Security.Cryptography.Pkcs/9.0.22 | Direta |
| System.Security.Cryptography.ProtectedData/9.0.0 | Transitiva |
| System.Security.Permissions/9.0.0 | Transitiva |
| System.Windows.Extensions/9.0.0 | Transitiva |

### SeenIt.AvatarChecks

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitiva |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitiva |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitiva |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitiva |
| Microsoft.OpenApi/2.7.5 | Transitiva |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitiva |
| Npgsql/10.0.0 | Transitiva |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitiva |
| SkiaSharp/4.155.0 | Transitiva |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitiva |

### SeenIt.Checks

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitiva |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitiva |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitiva |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitiva |
| Microsoft.OpenApi/2.7.5 | Transitiva |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitiva |
| Npgsql/10.0.0 | Transitiva |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitiva |
| SkiaSharp/4.155.0 | Transitiva |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitiva |

### SeenIt.RegisterIntegrationChecks

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitiva |
| Microsoft.AspNetCore.Mvc.Testing/10.0.7 | Direta |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitiva |
| Microsoft.AspNetCore.TestHost/10.0.7 | Transitiva |
| Microsoft.Data.Sqlite.Core/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Sqlite.Core/10.0.0 | Transitiva |
| Microsoft.EntityFrameworkCore.Sqlite/10.0.0 | Direta |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitiva |
| Microsoft.Extensions.DependencyModel/10.0.7 | Transitiva |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitiva |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitiva |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitiva |
| Microsoft.OpenApi/2.7.5 | Transitiva |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitiva |
| Npgsql/10.0.0 | Transitiva |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitiva |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitiva |
| SkiaSharp/4.155.0 | Transitiva |
| SQLitePCLRaw.bundle_e_sqlite3/2.1.11 | Transitiva |
| SQLitePCLRaw.core/2.1.11 | Transitiva |
| SQLitePCLRaw.lib.e_sqlite3/2.1.11 | Transitiva |
| SQLitePCLRaw.provider.e_sqlite3/2.1.11 | Transitiva |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitiva |

### SeenIt.HistoricalAudit

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitiva |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitiva |
| Npgsql/10.0.3 | Direta |

### SeenIt.HistoricalAudit.Checks

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitiva |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitiva |
| Npgsql/10.0.3 | Transitiva |

### SeenIt.HistoricalAudit.Remote

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitiva |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitiva |
| Npgsql/10.0.3 | Transitiva |

### SeenIt.HistoricalAudit.RemoteChecks

| Pacote / versão resolvida | Relação no projeto |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitiva |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitiva |
| Npgsql/10.0.3 | Transitiva |
