# B05-A — Diagnóstico de segurança das dependências

Data: 13/11/2026 (America/Sao_Paulo). Referência local: `9c4fe3761c83bed72955b1d257d84e3c4836fa8b`.

## 1. Resultado e critérios de contagem

Diagnóstico, sem aplicação de correções. A árvore local estava limpa no início. As conclusões sobre produção são inferidas do código e do Dockerfile versionados; não foi inspecionado o artefato efetivamente publicado em Vercel/Render.

| Escopo | Crítica | Alta | Moderada | Baixa | Total |
| --- | ---: | ---: | ---: | ---: | ---: |
| npm: pacotes sinalizados, incluindo propagação aos pais | 3 | 9 | 20 | 0 | 32 |
| NuGet: pacotes vulneráveis distintos nos oito projetos | 0 | 1 | 0 | 0 | 1 |
| Total de pacotes sinalizados entre os ecossistemas | 3 | 10 | 20 | 0 | 33 |

Os 32 resultados npm **não são 32 falhas independentes**: por exemplo, o alerta crítico de piscina propaga para @angular/build. Há 50 advisories GHSA distintos na saída npm, deduplicados por URL, com {"high":17,"moderate":27,"low":4,"critical":2}. Essa segunda métrica inclui advisories baixos dentro de pacotes cuja severidade máxima é maior. A severidade da tabela principal reproduz o fornecedor da auditoria, sem reclassificar pela exposição.

- Frontend publicado: @angular/router é a única dependência de aplicação sinalizada; seus três alertas descrevem DoS em SSR. Não há SSR configurado no projeto inspecionado. Não foi demonstrado um caminho de exploração desses alertas na SPA publicada.
- Build/desenvolvimento frontend: 31 pacotes sinalizados. Risco real para desenvolvedores e CI depende de entradas maliciosas, funcionalidades utilizadas e exposição de servidores locais; comprometer o build pode afetar o artefato futuro.
- API: nenhum pacote sinalizado na consulta NuGet do projeto da API. Isso não certifica ausência de falhas, especialmente em bibliotecas nativas, runtime e sistema operacional.
- Testes de integração: SQLitePCLRaw.lib.e_sqlite3 2.1.11, alta, GHSA-2m69-gcr7-jv3q / CVE-2025-6965. Não integra a imagem final definida no Dockerfile.
- Ferramentas administrativas HistoricalAudit: nenhum alerta retornado; não integram a imagem.

## 2. Metodologia, comandos e limitações operacionais

Inspeção de frontend/package.json, frontend/package-lock.json, package-lock.json da raiz, angular.json, vercel.json, todos os oito .csproj, project.assets.json existentes, Dockerfile, .dockerignore e pontos de uso no código. Consultas públicas de advisories; nenhum acesso ao Supabase ou aos serviços publicados.

Ambiente observado: Node 24.16.0; npm 11.13.0; SDK .NET 10.0.301; host/runtime local .NET e ASP.NET Core 10.0.9, Windows x64. Essas versões locais não identificam as versões implantadas. Docker CLI 29.9.2 disponível; aviso de acesso negado à configuração local do Docker ao consultar a versão.

Comandos executados (caminhos relativos à raiz, salvo indicação):

```text
git status --short
git rev-parse HEAD
git diff --stat
rg --files / rg -n (manifestos, referências, configuração e usos)
node --version
npm --version
dotnet --info
dotnet package list --help
cd frontend
npm audit --json --ignore-scripts
npm ls --all --json
cd ..
dotnet package list --project <cada um dos 8 .csproj> --no-restore --vulnerable --include-transitive --format json
docker --version
```

Leituras auxiliares com PowerShell e Node extraíram versões, grafos e classificação dev do lockfile/assets. npm ls --all --json terminou com código 0: a árvore instalada pôde ser enumerada; as 16 dependências diretas coincidem com o lockfile. A saída inicial extensa foi truncada na interface; inventário completo do lockfile está no apêndice. A auditoria npm terminou com código 1 por vulnerabilidades, após consulta bem-sucedida.

A primeira tentativa npm falhou ao acessar registry.npmjs.org; a primeira tentativa NuGet falhou por bloqueio de socket em api.nuget.org:443. Ambas foram repetidas com permissão de rede e tiveram retorno válido. Uma execução auxiliar na raiz retornou zero vulnerabilidades porque o lockfile da raiz está vazio: **esse resultado foi descartado para a avaliação do frontend**. Não foi tratado como evidência de segurança.

Foi usado --no-restore para evitar regenerar obj, resolver novas versões ou escrever dependências. A análise NuGet se refere aos assets já existentes, confrontados com as referências dos projetos; não equivale a uma restauração limpa linux-x64 nem comprova o conteúdo do container implantado. Não foram executados restore, install, build, testes, ferramentas administrativas, migrations, scan de container, pull de imagens ou deploy. Nenhuma prova de conceito foi executada.

## 3. Inventário e dependências diretas

O lockfile da raiz usa formato 3 e tem packages vazio. O manifesto operacional do frontend está em frontend/. Seu lockfile formato 3 contém 527 entradas de pacotes, além da raiz; o npm informa 27 prod e 500 dev, com 127 opcionais e 4 peer (categorias que não devem ser somadas novamente).

| Pacote frontend direto | Intervalo declarado | Versão no lockfile e instalada | Escopo |
| --- | --- | --- | --- |
| @angular/animations | 21.2.23 | 21.2.23 | Aplicação |
| @angular/common | ^21.0.0 | 21.2.23 | Aplicação |
| @angular/compiler | ^21.0.0 | 21.2.23 | Aplicação |
| @angular/core | ^21.0.0 | 21.2.23 | Aplicação |
| @angular/forms | ^21.0.0 | 21.2.23 | Aplicação |
| @angular/platform-browser | ^21.0.0 | 21.2.23 | Aplicação |
| @angular/router | ^21.0.0 | 21.2.23 | Aplicação |
| @primeuix/themes | ^3.0.1 | 3.0.1 | Aplicação |
| primeicons | ^8.0.2 | 8.0.2 | Aplicação |
| primeng | ^21.1.10 | 21.1.10 | Aplicação |
| rxjs | ~7.8.2 | 7.8.2 | Aplicação |
| tslib | ^2.8.1 | 2.8.1 | Aplicação |
| @angular/build | ^21.0.0 | 21.2.24 | Desenvolvimento/build |
| @angular/cli | ^21.0.0 | 21.2.24 | Desenvolvimento/build |
| @angular/compiler-cli | ^21.0.0 | 21.2.23 | Desenvolvimento/build |
| typescript | ~5.9.0 | 5.9.3 | Desenvolvimento/build |

Todos os projetos .NET usam net10.0:

| Projeto | Função | Referências de projeto | Resultado da consulta de vulneráveis |
| --- | --- | --- | --- |
| SeenIt.Api | API publicada | Nenhuma | Nenhum pacote retornado |
| SeenIt.AvatarChecks | Checks de avatar; executado no estágio verify | Api | Nenhum pacote retornado |
| SeenIt.Checks | Checks principais; executado no estágio verify | Api | Nenhum pacote retornado |
| SeenIt.RegisterIntegrationChecks | Integração, WebApplicationFactory e SQLite em memória | Api | SQLitePCLRaw.lib.e_sqlite3 2.1.11 (alta) |
| SeenIt.HistoricalAudit | Ferramenta administrativa | Nenhuma | Nenhum pacote retornado |
| SeenIt.HistoricalAudit.Checks | Verificação da ferramenta | HistoricalAudit | Nenhum pacote retornado |
| SeenIt.HistoricalAudit.Remote | Ferramenta administrativa remota | HistoricalAudit | Nenhum pacote retornado |
| SeenIt.HistoricalAudit.RemoteChecks | Verificação da ferramenta remota | HistoricalAudit.Remote | Nenhum pacote retornado |

API: SkiaSharp e NativeAssets.Linux.NoDependencies 4.155.0; JwtBearer e AspNetCore.OpenApi 10.0.0; Microsoft.OpenApi 2.7.5; System.Security.Cryptography.Pkcs 9.0.22; EFCore.Design 10.0.0; Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0. O provider resolve Npgsql **10.0.0** na API, enquanto HistoricalAudit referencia diretamente **10.0.3**: não confundir as versões.

RegisterIntegrationChecks adiciona diretamente Microsoft.AspNetCore.Mvc.Testing 10.0.7 e Microsoft.EntityFrameworkCore.Sqlite 10.0.0. AvatarChecks/Checks só referenciam a API. Os outros projetos tools herdam Npgsql 10.0.3 de HistoricalAudit. O apêndice contém versões resolvidas e indicação de direto/transitivo em cada projeto.

## 4. Achados npm: pacote, introdução e correção

As versões abaixo são as do lockfile auditado. D = dependência direta; T = transitiva. A coluna introdução lista pais imediatos no lockfile; os caminhos partem predominantemente de @angular/build ou @angular/cli. Todos os resultados têm fixAvailable=true no npm; isso significa que o resolvedor identifica um caminho de correção, não que atualizar apenas o pai sempre resolverá tudo. Não foi simulado install nem aplicado override.

Risco de regressão padrão: **baixo a moderado** para patch compatível, sujeito a build/checks; **moderado a alto** quando o pai fixa versão ou é necessária mudança major de transitivo. Os mínimos indicados por limites de intervalos devem ser confirmados nas releases e na resolução de B05-B. Para pais sem GHSA próprio, atualizar o grafo até eliminar os advisories propagados.

| Pacote | Versão | Severidade npm | Tipo | Introdução (pais imediatos) | Correção recomendada / risco |
| --- | --- | --- | --- | --- | --- |
| @angular/build | 21.2.24 | critical | D | ver grafo npm | Atualizar coordenadamente ferramentas Angular 21; faixa afetada informada inclui até 21.2.28; revalidar >=21.2.29 e transitivos. Moderado |
| @angular/cli | 21.2.24 | high | D | ver grafo npm | Patch compatível Angular 21 e resolver MCP/zod/pacote; mínimo exato do pai não determinado. Moderado |
| @angular/router | 21.2.23 | high | D | frontend | >=21.2.25 para os três alertas; alinhar todos os @angular/* (animations está fixado). Baixo/moderado |
| @hono/node-server | 2.1.1 | moderate | T | @modelcontextprotocol/sdk | >=2.1.6. Moderado |
| @modelcontextprotocol/sdk | 1.30.0 | high | T | @angular/cli | >=1.32.0. Moderado |
| @npmcli/package-json | 7.0.5 | moderate | T | @npmcli/run-script, pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| @npmcli/run-script | 10.0.4 | moderate | T | pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| @sigstore/sign | 4.1.1 | moderate | T | sigstore | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| @sigstore/tuf | 4.0.2 | moderate | T | sigstore | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| @tufjs/models | 4.1.0 | moderate | T | tuf-js | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| body-parser | 2.3.0 | high | T | express | >=2.4.0 via express/MCP. Baixo/moderado |
| brace-expansion | 5.0.9 | high | T | minimatch | >=5.0.12. Baixo/moderado |
| cacache | 20.0.4 | moderate | T | make-fetch-happen, pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| fast-uri | 3.1.7 | moderate | T | ajv | >=3.1.9. Baixo/moderado |
| glob | 13.0.6 | moderate | T | @npmcli/package-json, cacache | >14.0.1 e corrigir minimatch; validar versão publicada e compatibilidade do pai. Moderado/alto (major) |
| hono | 4.13.7 | moderate | T | @modelcontextprotocol/sdk | >=4.13.16. Baixo/moderado |
| ignore-walk | 8.0.0 | moderate | T | npm-packlist | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| ip-address | 10.7.0 | moderate | T | express-rate-limit, socks | >10.7.3; confirmar primeira release corrigida. Baixo/moderado |
| make-fetch-happen | 15.0.6 | moderate | T | @sigstore/sign, npm-registry-fetch, tuf-js | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| minimatch | 10.2.6 | moderate | T | @tufjs/models, glob, ignore-walk | >=11.0.1; validar mudança major via pais. Moderado/alto |
| npm-packlist | 10.0.4 | moderate | T | pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| npm-registry-fetch | 19.1.1 | moderate | T | pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| pacote | 21.5.1 | moderate | T | @angular/cli | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| piscina | 5.2.0 | critical | T | @angular/build | >=5.3.2. Baixo/moderado |
| proxy-addr | 2.0.7 | critical | T | express | >=2.0.8 via express. Baixo/moderado |
| qs | 6.16.0 | moderate | T | body-parser, express | >6.16.0; confirmar primeira release corrigida. Baixo/moderado |
| sigstore | 4.1.1 | moderate | T | pacote | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| source-map-js | 1.2.1 | high | T | postcss, sass | >=1.2.2. Baixo/moderado |
| tuf-js | 4.1.0 | moderate | T | @sigstore/tuf | Corrigir transitivos via atualização dos pais compatíveis; sem GHSA próprio. Moderado |
| undici | 6.28.1, 7.29.1 | high | T | @angular/build, node-gyp | >=6.29.1 e >=7.30.1 nos dois ramos. Baixo/moderado |
| vite | 7.3.6 | high | T | @angular/build | >7.3.6 no ramo compatível; confirmar release e resolução Angular. Moderado |
| zod | 4.3.6 | high | T | @angular/cli, @modelcontextprotocol/sdk | >=4.6.6. Moderado |

### Advisories individuais retornados

Cada linha registra título, severidade publicada pelo npm e faixa afetada. URLs GHSA são a referência do identificador; CVEs não retornados pelo npm não foram presumidos. Uma mesma URL pode aparecer com dois intervalos no undici. Os pais com alertas exclusivamente propagados não recebem CVE/GHSA próprio.

#### @angular/router

- [GHSA-ff3f-86qr-9cv3](https://github.com/advisories/GHSA-ff3f-86qr-9cv3) — **high**, afetado `>=21.0.0 <21.2.24`: Angular Server-Side Rendering (SSR): Denial of Service via Numeric URL Matrix Parameters.
- [GHSA-62vg-58rm-qff7](https://github.com/advisories/GHSA-62vg-58rm-qff7) — **high**, afetado `>=21.0.0 <21.2.25`: @angular/router: Denial of Service via Unmatched Empty-Path Outlet Route Matching in Server-Side Rendering (SSR).
- [GHSA-57xq-rjx2-v5xh](https://github.com/advisories/GHSA-57xq-rjx2-v5xh) — **high**, afetado `>=21.2.0 <21.2.25`: @angular/router: Denial of Service via RouterLink Query Parameter Retention in Server-Side Rendering (SSR).

#### @hono/node-server

- [GHSA-rmxm-3fg6-px4f](https://github.com/advisories/GHSA-rmxm-3fg6-px4f) — **moderate**, afetado `>=1.19.10 <2.1.3`: @hono/node-server: `serveStatic` decodes the request path a second time, leading to bypass of middleware on static paths.
- [GHSA-qv5m-9pm6-hqp3](https://github.com/advisories/GHSA-qv5m-9pm6-hqp3) — **moderate**, afetado `>=2.0.0 <2.1.6`: @hono/node-server: Unhandled `ECONNRESET` on the socket during an asynchronous WebSocket upgrade crashes the process.
- [GHSA-jpvv-r467-5p4w](https://github.com/advisories/GHSA-jpvv-r467-5p4w) — **moderate**, afetado `>=2.0.0 <2.1.6`: @hono/node-server: Backslash in the request path is parsed as a path separator, bypassing path-based access control at a reverse proxy.

#### @modelcontextprotocol/sdk

- [GHSA-6qxp-vccf-f47h](https://github.com/advisories/GHSA-6qxp-vccf-f47h) — **high**, afetado `>=1.12.0 <1.31.0`: MCP TypeScript SDK: OAuth client could send credentials to an authorization server chosen by the MCP server.
- [GHSA-22jm-h49p-29qw](https://github.com/advisories/GHSA-22jm-h49p-29qw) — **high**, afetado `>=1.24.0 <1.32.0`: MCP TypeScript SDK: Experimental tasks were not tied to the session that created them.
- [GHSA-46jr-4wx8-v453](https://github.com/advisories/GHSA-46jr-4wx8-v453) — **high**, afetado `>=1.3.0 <1.32.0`: MCP TypeScript SDK: One HTTP request within the default size limit could exhaust server memory during validation.
- [GHSA-rvq5-wwqv-78pq](https://github.com/advisories/GHSA-rvq5-wwqv-78pq) — **moderate**, afetado `>=1.6.0 <1.32.0`: MCP TypeScript SDK: Server bearer authentication accepted tokens issued for another service.
- [GHSA-f897-62hf-6gxq](https://github.com/advisories/GHSA-f897-62hf-6gxq) — **high**, afetado `>=1.10.0 <1.30.1`: MCP TypeScript SDK: HTTP server transports and handlers did not limit request body size or JSON-RPC batch length.
- [GHSA-6prh-2h8m-c8cw](https://github.com/advisories/GHSA-6prh-2h8m-c8cw) — **moderate**, afetado `<1.32.0`: MCP TypeScript client SDK: HTTP client transports and OAuth requests followed cross-origin redirects and re-sent custom headers and request bodies.

#### body-parser

- [GHSA-27r3-jxmh-p828](https://github.com/advisories/GHSA-27r3-jxmh-p828) — **high**, afetado `>=2.0.0 <2.4.0`: body-parser vulnerable to Denial of Service via trailing bytes after a compressed request body.

#### brace-expansion

- [GHSA-q2hr-2g5m-vwhr](https://github.com/advisories/GHSA-q2hr-2g5m-vwhr) — **moderate**, afetado `>=4.0.0 <5.0.12`: brace-expansion: Quadratic-time expansion of the `{a},b}` rewrite causes CPU denial of service.
- [GHSA-qhr7-859c-m2p7](https://github.com/advisories/GHSA-qhr7-859c-m2p7) — **high**, afetado `>=4.0.0 <5.0.11`: brace-expansion: DoS via uncontrolled recursion on nested brace groups causing stack exhaustion.
- [GHSA-6j4f-fj2g-mc7p](https://github.com/advisories/GHSA-6j4f-fj2g-mc7p) — **high**, afetado `>=4.0.0 <5.0.10`: brace-expansion: DoS via uncontrolled recursion in parseCommaParts causing stack exhaustion.

#### fast-uri

- [GHSA-hrr3-gc8f-f4qj](https://github.com/advisories/GHSA-hrr3-gc8f-f4qj) — **moderate**, afetado `>=3.0.0 <3.1.8`: fast-uri vulnerable to inconsistent host case normalization via percent-encoded octets.
- [GHSA-w74x-v6f9-j3xg](https://github.com/advisories/GHSA-w74x-v6f9-j3xg) — **moderate**, afetado `>=3.0.0 <3.1.9`: fast-uri vulnerable to URI scheme spoofing via percent-encoded scheme decoding.
- [GHSA-xhq7-xmqj-769h](https://github.com/advisories/GHSA-xhq7-xmqj-769h) — **low**, afetado `>=3.0.0 <3.1.9`: fast-uri vulnerable to improper path equivalence in relative reference normalization.
- [GHSA-wjr9-j795-jxcr](https://github.com/advisories/GHSA-wjr9-j795-jxcr) — **moderate**, afetado `>=3.1.3 <3.1.9`: fast-uri vulnerable to host confusion via unchecked WHATWG host canonicalization.
- [GHSA-333x-w4r6-6crp](https://github.com/advisories/GHSA-333x-w4r6-6crp) — **low**, afetado `>=3.0.0 <3.1.9`: fast-uri vulnerable to scheme injection via an unescaped first-segment colon in suffix references.

#### glob

- [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) — **moderate**, afetado `>=9.0.0 <=14.0.1`: Glob: AbortSignal cancellation bypass allows post-timeout filesystem traversal.

#### hono

- [GHSA-5r4p-p66f-jhc7](https://github.com/advisories/GHSA-5r4p-p66f-jhc7) — **moderate**, afetado `>=4.12.4 <4.13.11`: Hono: `serveStatic` decodes the request path a second time, leading to bypass of middleware on static paths.
- [GHSA-4m76-xfj5-7975](https://github.com/advisories/GHSA-4m76-xfj5-7975) — **moderate**, afetado `<4.13.16`: Hono: `mount()` and `app.mount()` can forward a path that bypasses middleware scoped inside the mount prefix.
- [GHSA-h7mg-cq63-vhpq](https://github.com/advisories/GHSA-h7mg-cq63-vhpq) — **moderate**, afetado `>=4.1.4 <=4.13.15`: Hono: RegExpRouter skips wildcard middleware for some route sets and for paths with an encoded line terminator.
- [GHSA-3gg2-335m-rxc6](https://github.com/advisories/GHSA-3gg2-335m-rxc6) — **low**, afetado `>=3.12.0 <=4.13.15`: Hono: `css` helper does not escape a closing tag split between the template and an interpolated value, allowing HTML injection.
- [GHSA-46wx-v44x-9hh2](https://github.com/advisories/GHSA-46wx-v44x-9hh2) — **moderate**, afetado `<4.13.16`: Hono: `csrf()` does not check requests sent by hyperlink auditing (`text/ping`).
- [GHSA-4h7h-rv7c-7m2v](https://github.com/advisories/GHSA-4h7h-rv7c-7m2v) — **moderate**, afetado `>=4.5.1 <=4.13.15`: Hono: `validator('form')` skips validation for `multipart/form-data` requests with extra parameters.

#### ip-address

- [GHSA-j6r3-76f7-8jcv](https://github.com/advisories/GHSA-j6r3-76f7-8jcv) — **moderate**, afetado `<=10.7.0`: ip-address: isInSubnet() and isHostInSubnet() compare addresses of different families as if they shared an address space, allowing an allowlist check to admit an address outside its range.
- [GHSA-h3mg-xc3c-68pw](https://github.com/advisories/GHSA-h3mg-xc3c-68pw) — **moderate**, afetado `<=10.7.0`: ip-address: Address6 builds a parse diagnostic proportional to the input with no length bound, allowing a single long string to stall or crash the process.
- [GHSA-rxp7-6hxx-x8q5](https://github.com/advisories/GHSA-rxp7-6hxx-x8q5) — **moderate**, afetado `<=10.7.3`: ip-address: Address6 matches a zone identifier in quadratic time ahead of its length limit, allowing a single long string to stall the process.

#### minimatch

- [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249) — **moderate**, afetado `>=7.0.0 <11.0.1`: minimatch has potential denial of service via globparts amplification.

#### piscina

- [GHSA-67c8-pqhq-4rmx](https://github.com/advisories/GHSA-67c8-pqhq-4rmx) — **critical**, afetado `>=5.0.0 <5.3.2`: piscina: Prototype-pollution gadget in ThreadPool.options allows RCE via execArgv / loadBalancer / env.

#### proxy-addr

- [GHSA-jqcg-44mw-7w3h](https://github.com/advisories/GHSA-jqcg-44mw-7w3h) — **critical**, afetado `>=1.1.0 <2.0.8`: proxy-addr vulnerable to IP spoofing via IPv4-mapped IPv6 trust subnet.

#### qs

- [GHSA-6pq8-jhm2-2vcw](https://github.com/advisories/GHSA-6pq8-jhm2-2vcw) — **moderate**, afetado `>=6.7.0 <=6.16.0`: qs : qs.stringify with arrayFormat 'comma' throws an uncaught TypeError on array elements carrying a non-callable own toString (DoS).

#### source-map-js

- [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q) — **high**, afetado `>=1.0.0 <1.2.2`: source-map-js allows event-loop denial of service through indexed source-map section offsets.

#### undici

- [GHSA-4qv8-2p9j-2685](https://github.com/advisories/GHSA-4qv8-2p9j-2685) — **moderate**, afetado `>=7.0.0 <7.30.1`: undici vulnerable to Denial of Service via unbounded memory in the EventSource parser.
- [GHSA-4qv8-2p9j-2685](https://github.com/advisories/GHSA-4qv8-2p9j-2685) — **moderate**, afetado `>=6.5.0 <6.29.1`: undici vulnerable to Denial of Service via unbounded memory in the EventSource parser.
- [GHSA-f72v-6w45-q472](https://github.com/advisories/GHSA-f72v-6w45-q472) — **moderate**, afetado `>=7.15.0 <7.30.1`: undici vulnerable to Subresource Integrity bypass via non-lowercase algorithm tokens.
- [GHSA-xjj6-f33q-929c](https://github.com/advisories/GHSA-xjj6-f33q-929c) — **high**, afetado `>=7.18.0 <7.30.1`: undici vulnerable to Denial of Service via unhandled error on excessive content-encodings.
- [GHSA-xjj6-f33q-929c](https://github.com/advisories/GHSA-xjj6-f33q-929c) — **high**, afetado `>=6.23.0 <6.29.1`: undici vulnerable to Denial of Service via unhandled error on excessive content-encodings.
- [GHSA-4w7w-5xrc-9h26](https://github.com/advisories/GHSA-4w7w-5xrc-9h26) — **high**, afetado `>=7.0.0 <7.30.1`: undici vulnerable to cache poisoning and cross-user disclosure via cross-origin redirects.
- [GHSA-2m5v-pch6-74hx](https://github.com/advisories/GHSA-2m5v-pch6-74hx) — **high**, afetado `>=7.0.0 <7.30.1`: undici vulnerable to TLS certificate validation bypass via dropped tls options in Agent.
- [GHSA-2m5v-pch6-74hx](https://github.com/advisories/GHSA-2m5v-pch6-74hx) — **high**, afetado `>=4.0.0 <6.29.1`: undici vulnerable to TLS certificate validation bypass via dropped tls options in Agent.
- [GHSA-qrxm-fqhp-jwvm](https://github.com/advisories/GHSA-qrxm-fqhp-jwvm) — **high**, afetado `>=7.0.0 <7.30.1`: undici vulnerable to Denial of Service via TLS 1.2 certificate-changing renegotiation.
- [GHSA-qrxm-fqhp-jwvm](https://github.com/advisories/GHSA-qrxm-fqhp-jwvm) — **high**, afetado `>=4.2.1 <6.29.1`: undici vulnerable to Denial of Service via TLS 1.2 certificate-changing renegotiation.
- [GHSA-9fm3-97px-rjf6](https://github.com/advisories/GHSA-9fm3-97px-rjf6) — **moderate**, afetado `>=7.0.0 <7.30.1`: undici WebSocket client vulnerable to Denial of Service via unbounded automatic PONG buffering.
- [GHSA-9fm3-97px-rjf6](https://github.com/advisories/GHSA-9fm3-97px-rjf6) — **moderate**, afetado `>=5.15.0 <6.29.1`: undici WebSocket client vulnerable to Denial of Service via unbounded automatic PONG buffering.
- [GHSA-55wc-3pg8-jfw6](https://github.com/advisories/GHSA-55wc-3pg8-jfw6) — **moderate**, afetado `>=7.15.0 <7.30.1`: undici vulnerable to Denial of Service via unhandled exception in the decompress interceptor.

#### vite

- [GHSA-698v-4r3x-3vcv](https://github.com/advisories/GHSA-698v-4r3x-3vcv) — **moderate**, afetado `>=7.0.0 <=7.3.6`: launch-editor: escapeCmdArgs() omits % escaping on Windows.
- [GHSA-r825-w8mm-5pxw](https://github.com/advisories/GHSA-r825-w8mm-5pxw) — **moderate**, afetado `>=7.0.0 <=7.3.6`: launch-editor: Editor CLI flag misinterpretation causing arbitrary command execution for some editors.
- [GHSA-587h-7wmj-h5pm](https://github.com/advisories/GHSA-587h-7wmj-h5pm) — **high**, afetado `>=7.0.0 <=7.3.6`: launch-editor: `fs.existsSync()` existence check can be bypassed on Windows.
- [GHSA-9jrq-w75r-8gcw](https://github.com/advisories/GHSA-9jrq-w75r-8gcw) — **low**, afetado `>=7.0.0 <=7.3.6`: Vite: Cross origin script execution through Vite custom middleware.
- [GHSA-rq7h-c2jc-7f22](https://github.com/advisories/GHSA-rq7h-c2jc-7f22) — **moderate**, afetado `>=7.0.0 <=7.3.6`: Vite: Files outside `server.fs.allow` could be served when project paths match system paths on non-Windows machine.

#### zod

- [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) — **high**, afetado `<4.6.6`: Zod can exhaust memory while validating oversized constrained collections.


### Exposição e prioridade dos grupos npm

| Grupo | Condição de exploração | Exposição no SeenIt | Prioridade / regressão |
| --- | --- | --- | --- |
| piscina | Poluição prévia de Object.prototype no processo que cria workers; gadget permite execução de código | Build Angular, não navegador; não foi encontrada/demonstrada a primitiva prévia de poluição | P1: corrigir cadeia de build; patch, baixo/moderado |
| proxy-addr | Regras específicas de trust proxy com IPv4-mapped IPv6 | Servidores Node transitivos do CLI/MCP; a API usa ASP.NET Core e a SPA é estática | P1 se ferramenta exposta; senão P2; patch baixo/moderado |
| vite / launch-editor | Servidor de desenvolvimento/editor e entradas controladas; alguns cenários Windows | Máquina do desenvolvedor; não é servidor da SPA publicada | P1 para ferramentas; regressão moderada |
| undici | Respostas/redirects/TLS/WebSocket/EventSource maliciosos conforme API usada | Rede durante ferramentas/build; não é fetch do navegador nem HttpClient .NET | P1/P2; revalidar os dois ramos |
| MCP SDK, hono, node-server, body-parser, qs, zod | Servidor MCP/HTTP, OAuth, validação e middlewares específicos | Ferramentas do Angular CLI; não há servidor MCP no produto inspecionado | P2; P1 se houver uso/exposição do CLI MCP; moderado |
| glob, minimatch, brace-expansion, source-map-js, fast-uri, ip-address e pais | Padrões, mapas de fonte, URIs/IPs ou árvores controlados pelo atacante | Ferramentas/build; entradas de repositórios/pacotes não confiáveis elevam risco | P2; majors de glob/minimatch exigem cautela |
| @angular/router | URLs processadas em SSR Node/V8 | Dependência incluída na aplicação, porém configuração atual é CSR/SPA | P2 preventivo; P1 antes de ativar SSR; patch baixo/moderado |

O npm classifica piscina como critical, mas o [advisory do mantenedor](https://github.com/piscinajs/piscina/security/advisories/GHSA-67c8-pqhq-4rmx) classifica como High e identifica CVE-2026-102992. A contagem conserva critical do npm e registra a divergência. Não se deve interpretar a severidade como prova de RCE remotamente acessível no SeenIt.

## 5. Backend: NU1903 e impacto na API

Cadeia confirmada nos assets:

```text
SeenIt.RegisterIntegrationChecks
  -> Microsoft.EntityFrameworkCore.Sqlite 10.0.0 (direto)
     -> SQLitePCLRaw.bundle_e_sqlite3 2.1.11
        -> SQLitePCLRaw.lib.e_sqlite3 2.1.11
```

O mesmo grafo contém Microsoft.EntityFrameworkCore.Sqlite.Core -> Microsoft.Data.Sqlite.Core -> SQLitePCLRaw.core e o provider e_sqlite3. O NU1903 está registrado nos assets do projeto de integração e foi confirmado na consulta online do SDK.

[GHSA-2m69-gcr7-jv3q / CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q): severidade alta, CVSS 4.0 7,2. É uma corrupção de memória no SQLite anterior a 3.50.2 envolvendo termos de agregação. O advisory marca SQLitePCLRaw.lib.e_sqlite3 <=2.1.11 como afetado e **não lista versão corrigida desse pacote**. O mínimo corrigido do motor SQLite não é uma versão NuGet intercambiável.

RegistrationFactory usa SQLite :memory:, com WebApplicationFactory/TestServer, dados sintéticos e substituição dos serviços externos. Não há endpoint de SQL arbitrário identificado nessa inspeção; entradas normais do teste são expressões EF controladas. O cenário de exploração do motor não foi demonstrado. Prioridade P2 para higiene dos testes, sem urgência de incidente em produção. Atualizar apenas EF Sqlite para outro patch não garante remover o bundle vulnerável.

B05-B deve avaliar provider/bundle suportado que embarque SQLite >=3.50.2 (e sem outros advisories vigentes), ou migração dos testes para PostgreSQL descartável local. Não sugerir como solução um inexistente patch 2.1.12, nem suprimir NU1903 como correção. Risco **moderado/alto**: ABI nativa, inicialização do provider, portabilidade e diferenças de comportamento SQLite/PostgreSQL. Validar isolamento e testes de registro, integridade e concorrência em ambiente local.

A API configura UseNpgsql em Program.cs e não referencia o projeto de integração. Referências de projeto propagam da API aos checks, não no sentido inverso. SQLitePCLRaw não consta dos assets da API, AvatarChecks ou Checks e o projeto de integração não é copiado/restaurado/publicado no Dockerfile.

SkiaSharp 4.155.0 e seus assets nativos fazem parte do runtime real, decodificando uploads de avatar. Não houve alerta NuGet, porém este é um ponto real de entrada para conteúdo não confiável. Há limites de 5 MB, dimensões, 16 MP, duas decodificações simultâneas e validação de formato; não substituem correções nativas. Não foi realizado inventário de CVEs das bibliotecas incorporadas no binário libSkiaSharp.so.

Microsoft.OpenApi 2.7.5 e Pkcs 9.0.22 são pins de correções anteriores documentados no .csproj (GHSA-v5pm-xwqc-g5wc; GHSA-846f-f764-76mx e GHSA-2fmv-rv2c-59x7). Não foram novamente sinalizados pela consulta atual. MapOpenApi está condicionado a IsDevelopment; isso reduz exposição da funcionalidade em produção, mas não implica remoção do assembly do publish.

EFCore.Design e Pkcs usam PrivateAssets=all. Isso controla propagação aos consumidores, **não garante exclusão do publish da própria API**. Os assets da API incluem Pkcs como runtime e o IncludeAssets de Design inclui runtime. Tratar esses pacotes como design-time com potencial presença física no artefato até inspecionar publish/deps/SBOM. Não há vulnerabilidade nova atribuída a eles nesta auditoria.

JwtBearer, EF Core, Npgsql e SkiaSharp são dependências efetivamente utilizadas pela API. As ferramentas HistoricalAudit, inclusive Remote, são executáveis administrativos separados; nenhum deles foi executado e não há referência da API para tools.

## 6. Impacto no frontend publicado

angular.json declara entrada browser src/main.ts e não configura server/ssr; não há @angular/ssr no manifesto. vercel.json reescreve rotas para index.html. Essas evidências indicam SPA estática sem processo Node de SSR em runtime. A Vercel usa as ferramentas durante build; isso não torna Vite, MCP ou express servidores da aplicação publicada.

Os três advisories de router são de SSR. O [advisory Angular sobre parâmetros numéricos](https://github.com/angular/angular/security/advisories/GHSA-ff3f-86qr-9cv3) explicita que SPA puramente cliente não cruza a fronteira de segurança desse cenário. Os outros dois exigem processamento de rotas/RouterLink em SSR segundo os títulos e escopos retornados pela auditoria. Não foi confirmada exploração na configuração atual; manter atualização preventiva e reavaliar antes de introduzir SSR.

O frontend instalado tem Angular runtime/compiler/compiler-cli 21.2.23 e CLI/build 21.2.24. Coordenar atualização do conjunto Angular, especialmente animations fixado exatamente em 21.2.23; não aplicar mudanças majors indiscriminadas para zerar contador. PrimeNG, themes, primeicons, rxjs, tslib e TypeScript não receberam alerta no resultado consultado, o que não é garantia absoluta.

## 7. Desenvolvimento, testes e ferramentas

Os 31 pacotes npm sinalizados além de router são classificados dev no lockfile. A ausência deles no runtime publicado decorre também do modelo de publicação estática observado, não apenas do campo devDependencies. Dependências dev continuam relevantes para integridade do build e credenciais de CI; evitar servidores ng serve/CLI MCP acessíveis a terceiros e processar contribuições/pacotes não confiáveis em ambientes isolados.

AvatarChecks e Checks entram nos estágios de build e verify do Docker; não são copiados para final. RegisterIntegrationChecks/SQLite e os quatro projetos tools ficam fora inclusive dos projetos de build definidos no Dockerfile. Não foram executados checks porque esta etapa é exclusivamente de diagnóstico e eles podem realizar operações externas/locais fora do necessário.

## 8. Docker: build, runtime e limitações

| Elemento | Evidência | Avaliação |
| --- | --- | --- |
| Build | mcr.microsoft.com/dotnet/sdk:10.0-noble | SDK da linha 10.0 em Ubuntu Noble (24.04); patch/digest não fixados |
| Runtime | mcr.microsoft.com/dotnet/aspnet:10.0-noble | ASP.NET Core/.NET 10.0 e SO Ubuntu Noble; patch real não inferível pela tag |
| Publish | linux-x64, self-contained false, PublishTrimmed=false | Runtime fornecido pela imagem; dependências da API potencialmente mantidas no publish |
| Verify | runtime-base, executa AvatarChecks e Checks; compara SkiaSharp nativo | Verificações funcionais/ligação nativa, não scan completo de CVEs |
| Final | copia /out/api, marcador de verify e CA pública; USER app | Não copia SDK nem diretórios /checks; CA não é pacote NuGet ou credencial privada |
| Auditoria no build | dotnet package list --vulnerable --include-transitive | Mostra alertas, porém não há parsing/fail explícito por vulnerabilidade; não presumir bloqueio de build |

A tag 10.0-noble é mutável. Não há digest nem patch exato do SDK/runtime no Dockerfile. Não se conclui que a imagem atualmente no Render corresponde à tag atual ou ao runtime local 10.0.9. Rebuild futuro pode mudar SO/runtime e transitivos sem haver mudança no Dockerfile. Avaliar em B05-B política de atualização/pinning com manutenção periódica, sem alteração nesta etapa.

.dockerignore usa allowlist para os três projetos backend e configurações do container e exclui bin/obj, frontend e ferramentas. O SQLite dos testes de integração não tem caminho de entrada na imagem final pelo grafo observado. Mesmo checks não copiados podem afetar o build se forem comprometidos; o isolamento de estágios reduz conteúdo final, não elimina riscos da cadeia de fornecimento.

Nenhuma imagem foi baixada, construída, executada ou varrida. Não foi acessado registry de imagens nem consultado digest implantado. Logo, **quantidade de vulnerabilidades do SO, runtime de imagem e bibliotecas nativas: não determinada**, e não zero. Acesso à configuração do Docker apresentou aviso de permissão na consulta de versão; não foi necessário elevar para executar ações de container.

## 9. Plano de correção proposto — B05-B, sem execução

1. **P1 — Ferramentas frontend / integridade de build:** em branch de correção, atualizar CLI/build dentro da linha Angular suportada e resolver piscina >=5.3.2, Vite corrigido e ambos os ramos undici. Rever MCP SDK >=1.32.0 e zod >=4.6.6 se o CLI os utiliza. Verificar grafo resultante, builds de produção, budgets e checks relevantes. Não usar audit fix --force; não impor overrides majors sem validar o pai.
2. **P2 — Angular de aplicação:** alinhar pacotes @angular/* e compiler-cli em patch 21 compatível >=21.2.25 para os advisories atuais do router; combinar com versão de build cuja árvore elimine os alertas. Validar navegação, guardas, query/matrix params, formulários e PrimeNG. Prioridade sobe antes de qualquer SSR.
3. **P2 — Demais transitivos dev:** resolver Hono/node-server, express/body-parser/proxy-addr/qs, fast-uri, source-map-js, ip-address e famílias glob/minimatch/brace-expansion pelos pais. Conferir mínimos na tabela e reauditar: fixAvailable não prova compatibilidade nem eliminação de alertas futuros. Majors de glob/minimatch merecem mudanças separadas.
4. **P2 — SQLite de integração:** selecionar motor/bundle corrigido ou PostgreSQL local descartável; provar isolamento e equivalência dos checks. Não alterar a API/PostgreSQL por causa desse alerta exclusivo dos testes; não ocultar NU1903.
5. **P2 — Imagem e manutenção .NET:** obter de forma autorizada digest/SBOM do artefato, varrer OS/runtime/nativos, verificar patches Microsoft 10.0 suportados e revisar EF/Npgsql de forma coordenada. Não há advisory atual que justifique afirmar vulnerabilidade nos pacotes .NET 10.0.0 só por sua idade. Avaliar Npgsql 10.0.x da API separadamente da versão das ferramentas; testar autenticação, transações, consultas, OpenAPI dev e avatares.
6. **Validação e entrega futura:** auditar novamente todas as árvores, registrar evidências antes/depois e riscos aceitos; build/publish limpo em ambiente isolado; revisão humana do diff e plano de rollback antes de eventual deploy em etapa autorizada. Nenhuma dessas ações foi feita em B05-A.

## 10. Riscos residuais e fontes

- Bases de advisories variam no tempo e podem ter divergência, retirada ou atraso. Resultados representam a consulta desta sessão.
- Não foi comparado o conteúdo publicado com o commit; funcionamento informado pelo operador não é teste de segurança.
- NuGet foi consultado para todos os projetos, mas sem restore; inventários existentes podem refletir plataforma/local anteriores e não substituem grafo restaurado linux-x64.
- npm ls enumera a instalação existente; não houve npm ci para validar integridade completa. Pacotes opcionais de outras plataformas constam do lockfile sem necessariamente estar instalados no Windows.
- Não foi auditada configuração de secrets, infraestrutura Vercel/Render/Supabase, Postgres do provedor nem acessado banco.
- Não houve análise exaustiva de alcançabilidade ou exploração; “não demonstrado” é diferente de “impossível”.
- Sem scan de imagem/SBOM ou advisories individuais de bibliotecas nativas incorporadas, permanecem riscos não quantificados.
- Versões de correção precisam ser reconfirmadas em B05-B. Não há comprovação de regressão zero.
- Fontes: endpoints públicos de auditoria npm e NuGet; links GHSA de cada achado; [SQLite advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q), [Piscina upstream](https://github.com/piscinajs/piscina/security/advisories/GHSA-67c8-pqhq-4rmx), [Angular upstream](https://github.com/angular/angular/security/advisories/GHSA-ff3f-86qr-9cv3).

Próxima etapa sugerida: **B05-B — Correções controladas**. Apenas este relatório foi criado; sem atualização de pacotes, alteração de lockfile/.csproj/código/Dockerfile, commit, push, deploy, migrations ou acesso ao Supabase. A worktree maintenance-phase1 não foi acessada ou modificada.

## Apêndice A — Inventário NuGet resolvido por projeto

“Direto” corresponde a PackageReference no projeto; dependências de ProjectReference são marcadas separadamente. Inventário proveniente dos project.assets.json existentes.

### backend/SeenIt.Api/SeenIt.Api.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Humanizer.Core/2.14.1 | Transitivo |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Direto |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Direto |
| Microsoft.Build/17.7.2 | Transitivo |
| Microsoft.Build.Framework/17.14.28 | Transitivo |
| Microsoft.Build.Tasks.Core/17.14.28 | Transitivo |
| Microsoft.Build.Utilities.Core/17.14.28 | Transitivo |
| Microsoft.CodeAnalysis.Analyzers/3.11.0 | Transitivo |
| Microsoft.CodeAnalysis.Common/4.14.0 | Transitivo |
| Microsoft.CodeAnalysis.CSharp/4.14.0 | Transitivo |
| Microsoft.CodeAnalysis.CSharp.Workspaces/4.14.0 | Transitivo |
| Microsoft.CodeAnalysis.Workspaces.Common/4.14.0 | Transitivo |
| Microsoft.CodeAnalysis.Workspaces.MSBuild/4.14.0 | Transitivo |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Design/10.0.0 | Direto |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitivo |
| Microsoft.Extensions.DependencyModel/10.0.0 | Transitivo |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitivo |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitivo |
| Microsoft.NET.StringTools/17.14.28 | Transitivo |
| Microsoft.OpenApi/2.7.5 | Direto |
| Mono.TextTemplating/3.0.0 | Transitivo |
| Newtonsoft.Json/13.0.3 | Transitivo |
| Npgsql/10.0.0 | Transitivo |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Direto |
| SkiaSharp/4.155.0 | Direto |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Direto |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitivo |
| System.CodeDom/9.0.0 | Transitivo |
| System.Composition/9.0.0 | Transitivo |
| System.Composition.AttributedModel/9.0.0 | Transitivo |
| System.Composition.Convention/9.0.0 | Transitivo |
| System.Composition.Hosting/9.0.0 | Transitivo |
| System.Composition.Runtime/9.0.0 | Transitivo |
| System.Composition.TypedParts/9.0.0 | Transitivo |
| System.Configuration.ConfigurationManager/9.0.0 | Transitivo |
| System.Formats.Nrbf/9.0.0 | Transitivo |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitivo |
| System.Reflection.MetadataLoadContext/7.0.0 | Transitivo |
| System.Resources.Extensions/9.0.0 | Transitivo |
| System.Security.Cryptography.Pkcs/9.0.22 | Direto |
| System.Security.Cryptography.ProtectedData/9.0.0 | Transitivo |
| System.Security.Permissions/9.0.0 | Transitivo |
| System.Windows.Extensions/9.0.0 | Transitivo |

### backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitivo |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitivo |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitivo |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitivo |
| Microsoft.OpenApi/2.7.5 | Transitivo |
| Npgsql/10.0.0 | Transitivo |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitivo |
| SkiaSharp/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitivo |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitivo |
| SeenIt.Api/1.0.0 | Projeto referenciado |

### backend/SeenIt.Checks/SeenIt.Checks.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitivo |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitivo |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitivo |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitivo |
| Microsoft.OpenApi/2.7.5 | Transitivo |
| Npgsql/10.0.0 | Transitivo |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitivo |
| SkiaSharp/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitivo |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitivo |
| SeenIt.Api/1.0.0 | Projeto referenciado |

### backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer/10.0.0 | Transitivo |
| Microsoft.AspNetCore.Mvc.Testing/10.0.7 | Direto |
| Microsoft.AspNetCore.OpenApi/10.0.0 | Transitivo |
| Microsoft.AspNetCore.TestHost/10.0.7 | Transitivo |
| Microsoft.Data.Sqlite.Core/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Abstractions/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Analyzers/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Relational/10.0.0 | Transitivo |
| Microsoft.EntityFrameworkCore.Sqlite/10.0.0 | Direto |
| Microsoft.EntityFrameworkCore.Sqlite.Core/10.0.0 | Transitivo |
| Microsoft.Extensions.DependencyModel/10.0.7 | Transitivo |
| Microsoft.IdentityModel.Abstractions/8.0.1 | Transitivo |
| Microsoft.IdentityModel.JsonWebTokens/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Logging/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/8.0.1 | Transitivo |
| Microsoft.IdentityModel.Tokens/8.0.1 | Transitivo |
| Microsoft.OpenApi/2.7.5 | Transitivo |
| Npgsql/10.0.0 | Transitivo |
| Npgsql.EntityFrameworkCore.PostgreSQL/10.0.0 | Transitivo |
| SkiaSharp/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.macOS/4.155.0 | Transitivo |
| SkiaSharp.NativeAssets.Win32/4.155.0 | Transitivo |
| SQLitePCLRaw.bundle_e_sqlite3/2.1.11 | Transitivo |
| SQLitePCLRaw.core/2.1.11 | Transitivo |
| SQLitePCLRaw.lib.e_sqlite3/2.1.11 | Transitivo |
| SQLitePCLRaw.provider.e_sqlite3/2.1.11 | Transitivo |
| System.IdentityModel.Tokens.Jwt/8.0.1 | Transitivo |
| SeenIt.Api/1.0.0 | Projeto referenciado |

### tools/SeenIt.HistoricalAudit/SeenIt.HistoricalAudit.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitivo |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitivo |
| Npgsql/10.0.3 | Direto |

### tools/SeenIt.HistoricalAudit.Checks/SeenIt.HistoricalAudit.Checks.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitivo |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitivo |
| Npgsql/10.0.3 | Transitivo |
| SeenIt.HistoricalAudit/1.0.0 | Projeto referenciado |

### tools/SeenIt.HistoricalAudit.Remote/SeenIt.HistoricalAudit.Remote.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitivo |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitivo |
| Npgsql/10.0.3 | Transitivo |
| SeenIt.HistoricalAudit/1.0.0 | Projeto referenciado |

### tools/SeenIt.HistoricalAudit.RemoteChecks/SeenIt.HistoricalAudit.RemoteChecks.csproj

| Pacote/versão resolvida | Relação |
| --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0 | Transitivo |
| Microsoft.Extensions.Logging.Abstractions/10.0.0 | Transitivo |
| Npgsql/10.0.3 | Transitivo |
| SeenIt.HistoricalAudit/1.0.0 | Projeto referenciado |
| SeenIt.HistoricalAudit.Remote/1.0.0 | Projeto referenciado |

## Apêndice B — Inventário npm do lockfile

Inclui transitivos e opcionais, inclusive variantes de plataforma não necessariamente instaladas localmente. Caminhos identificam instalações aninhadas e múltiplas versões. O flag dev é o do lockfile.

| Caminho no lockfile | Versão | Escopo |
| --- | --- | --- |
| node_modules/@algolia/abtesting | 1.14.1 | Dev/build |
| node_modules/@algolia/client-abtesting | 5.48.1 | Dev/build |
| node_modules/@algolia/client-analytics | 5.48.1 | Dev/build |
| node_modules/@algolia/client-common | 5.48.1 | Dev/build |
| node_modules/@algolia/client-insights | 5.48.1 | Dev/build |
| node_modules/@algolia/client-personalization | 5.48.1 | Dev/build |
| node_modules/@algolia/client-query-suggestions | 5.48.1 | Dev/build |
| node_modules/@algolia/client-search | 5.48.1 | Dev/build |
| node_modules/@algolia/ingestion | 1.48.1 | Dev/build |
| node_modules/@algolia/monitoring | 1.48.1 | Dev/build |
| node_modules/@algolia/recommend | 5.48.1 | Dev/build |
| node_modules/@algolia/requester-browser-xhr | 5.48.1 | Dev/build |
| node_modules/@algolia/requester-fetch | 5.48.1 | Dev/build |
| node_modules/@algolia/requester-node-http | 5.48.1 | Dev/build |
| node_modules/@ampproject/remapping | 2.3.0 | Dev/build |
| node_modules/@angular-devkit/architect | 0.2102.24 | Dev/build |
| node_modules/@angular-devkit/core | 21.2.24 | Dev/build |
| node_modules/@angular-devkit/schematics | 21.2.24 | Dev/build |
| node_modules/@angular/animations | 21.2.23 | Prod |
| node_modules/@angular/build | 21.2.24 | Dev/build |
| node_modules/@angular/cdk | 21.2.14 | Prod |
| node_modules/@angular/cli | 21.2.24 | Dev/build |
| node_modules/@angular/common | 21.2.23 | Prod |
| node_modules/@angular/compiler | 21.2.23 | Prod |
| node_modules/@angular/compiler-cli | 21.2.23 | Dev/build |
| node_modules/@angular/core | 21.2.23 | Prod |
| node_modules/@angular/forms | 21.2.23 | Prod |
| node_modules/@angular/platform-browser | 21.2.23 | Prod |
| node_modules/@angular/router | 21.2.23 | Prod |
| node_modules/@babel/code-frame | 7.29.7 | Dev/build |
| node_modules/@babel/compat-data | 7.29.7 | Dev/build |
| node_modules/@babel/core | 7.29.7 | Dev/build |
| node_modules/@babel/core/node_modules/convert-source-map | 2.0.0 | Dev/build |
| node_modules/@babel/core/node_modules/semver | 6.3.1 | Dev/build |
| node_modules/@babel/generator | 7.29.8 | Dev/build |
| node_modules/@babel/helper-annotate-as-pure | 7.27.3 | Dev/build |
| node_modules/@babel/helper-compilation-targets | 7.29.7 | Dev/build |
| node_modules/@babel/helper-compilation-targets/node_modules/semver | 6.3.1 | Dev/build |
| node_modules/@babel/helper-globals | 7.29.7 | Dev/build |
| node_modules/@babel/helper-module-imports | 7.29.7 | Dev/build |
| node_modules/@babel/helper-module-transforms | 7.29.7 | Dev/build |
| node_modules/@babel/helper-split-export-declaration | 7.24.7 | Dev/build |
| node_modules/@babel/helper-string-parser | 7.29.7 | Dev/build |
| node_modules/@babel/helper-validator-identifier | 7.29.7 | Dev/build |
| node_modules/@babel/helper-validator-option | 7.29.7 | Dev/build |
| node_modules/@babel/helpers | 7.29.7 | Dev/build |
| node_modules/@babel/parser | 7.29.8 | Dev/build |
| node_modules/@babel/template | 7.29.7 | Dev/build |
| node_modules/@babel/traverse | 7.29.8 | Dev/build |
| node_modules/@babel/types | 7.29.8 | Dev/build |
| node_modules/@emnapi/core | 1.11.3 | Dev/build; opcional |
| node_modules/@emnapi/runtime | 1.11.3 | Dev/build; opcional |
| node_modules/@emnapi/wasi-threads | 1.2.3 | Dev/build; opcional |
| node_modules/@esbuild/aix-ppc64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/android-arm | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/android-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/android-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/darwin-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/darwin-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/freebsd-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/freebsd-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-arm | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-ia32 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-loong64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-mips64el | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-ppc64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-riscv64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-s390x | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/linux-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/netbsd-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/netbsd-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/openbsd-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/openbsd-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/openharmony-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/sunos-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/win32-arm64 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/win32-ia32 | 0.28.1 | Dev/build; opcional |
| node_modules/@esbuild/win32-x64 | 0.28.1 | Dev/build; opcional |
| node_modules/@gar/promise-retry | 1.0.3 | Dev/build |
| node_modules/@harperfast/extended-iterable | 1.0.3 | Dev/build; opcional |
| node_modules/@hono/node-server | 2.1.1 | Dev/build |
| node_modules/@inquirer/ansi | 1.0.2 | Dev/build |
| node_modules/@inquirer/checkbox | 4.3.2 | Dev/build |
| node_modules/@inquirer/confirm | 5.1.21 | Dev/build |
| node_modules/@inquirer/core | 10.3.2 | Dev/build |
| node_modules/@inquirer/editor | 4.2.23 | Dev/build |
| node_modules/@inquirer/expand | 4.0.23 | Dev/build |
| node_modules/@inquirer/external-editor | 1.0.3 | Dev/build |
| node_modules/@inquirer/figures | 1.0.15 | Dev/build |
| node_modules/@inquirer/input | 4.3.1 | Dev/build |
| node_modules/@inquirer/number | 3.0.23 | Dev/build |
| node_modules/@inquirer/password | 4.0.23 | Dev/build |
| node_modules/@inquirer/prompts | 7.10.1 | Dev/build |
| node_modules/@inquirer/rawlist | 4.1.11 | Dev/build |
| node_modules/@inquirer/search | 3.2.2 | Dev/build |
| node_modules/@inquirer/select | 4.4.2 | Dev/build |
| node_modules/@inquirer/type | 3.0.10 | Dev/build |
| node_modules/@isaacs/fs-minipass | 4.0.1 | Dev/build |
| node_modules/@istanbuljs/schema | 0.1.6 | Dev/build |
| node_modules/@jridgewell/gen-mapping | 0.3.13 | Dev/build |
| node_modules/@jridgewell/remapping | 2.3.5 | Dev/build |
| node_modules/@jridgewell/resolve-uri | 3.1.2 | Dev/build |
| node_modules/@jridgewell/sourcemap-codec | 1.6.0 | Dev/build |
| node_modules/@jridgewell/trace-mapping | 0.3.31 | Dev/build |
| node_modules/@listr2/prompt-adapter-inquirer | 3.0.5 | Dev/build |
| node_modules/@lmdb/lmdb-darwin-arm64 | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-darwin-x64 | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-linux-arm | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-linux-arm64 | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-linux-x64 | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-win32-arm64 | 3.5.1 | Dev/build; opcional |
| node_modules/@lmdb/lmdb-win32-x64 | 3.5.1 | Dev/build; opcional |
| node_modules/@modelcontextprotocol/sdk | 1.30.0 | Dev/build |
| node_modules/@msgpackr-extract/msgpackr-extract-darwin-arm64 | 3.0.4 | Dev/build; opcional |
| node_modules/@msgpackr-extract/msgpackr-extract-darwin-x64 | 3.0.4 | Dev/build; opcional |
| node_modules/@msgpackr-extract/msgpackr-extract-linux-arm | 3.0.4 | Dev/build; opcional |
| node_modules/@msgpackr-extract/msgpackr-extract-linux-arm64 | 3.0.4 | Dev/build; opcional |
| node_modules/@msgpackr-extract/msgpackr-extract-linux-x64 | 3.0.4 | Dev/build; opcional |
| node_modules/@msgpackr-extract/msgpackr-extract-win32-x64 | 3.0.4 | Dev/build; opcional |
| node_modules/@napi-rs/lzma-linux-x64-gnu | 1.5.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-android-arm-eabi | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-android-arm64 | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-darwin-arm64 | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-darwin-x64 | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-freebsd-x64 | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-arm-gnueabihf | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-arm64-gnu | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-arm64-musl | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-ppc64-gnu | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-riscv64-gnu | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-s390x-gnu | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-x64-gnu | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-linux-x64-musl | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-openharmony-arm64 | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-win32-arm64-msvc | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-win32-ia32-msvc | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/nice-win32-x64-msvc | 1.1.1 | Dev/build; opcional |
| node_modules/@napi-rs/wasm-runtime | 1.2.4 | Dev/build; opcional |
| node_modules/@npmcli/agent | 4.0.2 | Dev/build |
| node_modules/@npmcli/agent/node_modules/lru-cache | 11.5.2 | Dev/build |
| node_modules/@npmcli/fs | 5.0.0 | Dev/build |
| node_modules/@npmcli/git | 7.0.2 | Dev/build |
| node_modules/@npmcli/git/node_modules/isexe | 4.0.0 | Dev/build |
| node_modules/@npmcli/git/node_modules/lru-cache | 11.5.2 | Dev/build |
| node_modules/@npmcli/git/node_modules/which | 6.0.1 | Dev/build |
| node_modules/@npmcli/installed-package-contents | 4.0.0 | Dev/build |
| node_modules/@npmcli/node-gyp | 5.0.0 | Dev/build |
| node_modules/@npmcli/package-json | 7.0.5 | Dev/build |
| node_modules/@npmcli/promise-spawn | 9.0.1 | Dev/build |
| node_modules/@npmcli/promise-spawn/node_modules/isexe | 4.0.0 | Dev/build |
| node_modules/@npmcli/promise-spawn/node_modules/which | 6.0.1 | Dev/build |
| node_modules/@npmcli/redact | 4.0.0 | Dev/build |
| node_modules/@npmcli/run-script | 10.0.4 | Dev/build |
| node_modules/@oxc-project/types | 0.113.0 | Dev/build |
| node_modules/@parcel/watcher | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-android-arm64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-darwin-arm64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-darwin-x64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-freebsd-x64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-arm-glibc | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-arm-musl | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-arm64-glibc | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-arm64-musl | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-x64-glibc | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-linux-x64-musl | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-win32-arm64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher-win32-x64 | 2.6.0 | Dev/build; opcional |
| node_modules/@parcel/watcher/node_modules/node-addon-api | 7.1.1 | Dev/build; opcional |
| node_modules/@primeui/license-manager | 1.1.0 | Prod |
| node_modules/@primeuix/motion | 0.0.10 | Prod |
| node_modules/@primeuix/motion/node_modules/@primeuix/utils | 0.6.4 | Prod |
| node_modules/@primeuix/styled | 1.0.0 | Prod |
| node_modules/@primeuix/styles | 2.0.3 | Prod |
| node_modules/@primeuix/styles/node_modules/@primeuix/styled | 0.7.4 | Prod |
| node_modules/@primeuix/styles/node_modules/@primeuix/utils | 0.6.4 | Prod |
| node_modules/@primeuix/themes | 3.0.1 | Prod |
| node_modules/@primeuix/utils | 0.8.2 | Prod |
| node_modules/@rolldown/binding-android-arm64 | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-darwin-arm64 | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-darwin-x64 | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-freebsd-x64 | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-linux-arm-gnueabihf | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-linux-arm64-gnu | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-linux-arm64-musl | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-linux-x64-gnu | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-linux-x64-musl | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-openharmony-arm64 | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-wasm32-wasi | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-win32-arm64-msvc | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/binding-win32-x64-msvc | 1.0.0-rc.4 | Dev/build; opcional |
| node_modules/@rolldown/pluginutils | 1.0.0-rc.4 | Dev/build |
| node_modules/@rollup/rollup-android-arm-eabi | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-android-arm64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-darwin-arm64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-darwin-x64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-freebsd-arm64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-freebsd-x64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-arm-gnueabihf | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-arm-musleabihf | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-arm64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-arm64-musl | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-loong64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-loong64-musl | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-ppc64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-ppc64-musl | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-riscv64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-riscv64-musl | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-s390x-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-x64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-linux-x64-musl | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-openbsd-x64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-openharmony-arm64 | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-win32-arm64-msvc | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-win32-ia32-msvc | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-win32-x64-gnu | 4.63.1 | Dev/build; opcional |
| node_modules/@rollup/rollup-win32-x64-msvc | 4.63.1 | Dev/build; opcional |
| node_modules/@schematics/angular | 21.2.24 | Dev/build |
| node_modules/@sigstore/bundle | 4.0.0 | Dev/build |
| node_modules/@sigstore/core | 3.2.1 | Dev/build |
| node_modules/@sigstore/protobuf-specs | 0.5.2 | Dev/build |
| node_modules/@sigstore/sign | 4.1.1 | Dev/build |
| node_modules/@sigstore/tuf | 4.0.2 | Dev/build |
| node_modules/@sigstore/verify | 3.1.1 | Dev/build |
| node_modules/@standard-schema/spec | 1.1.0 | Prod |
| node_modules/@tufjs/canonical-json | 2.0.0 | Dev/build |
| node_modules/@tufjs/models | 4.1.0 | Dev/build |
| node_modules/@tybys/wasm-util | 0.10.3 | Dev/build; opcional |
| node_modules/@types/estree | 1.0.9 | Dev/build |
| node_modules/@vitejs/plugin-basic-ssl | 2.1.4 | Dev/build |
| node_modules/@yarnpkg/lockfile | 1.1.0 | Dev/build |
| node_modules/abbrev | 4.0.0 | Dev/build |
| node_modules/accepts | 2.0.0 | Dev/build |
| node_modules/agent-base | 7.1.4 | Dev/build |
| node_modules/ajv | 8.18.0 | Dev/build |
| node_modules/ajv-formats | 3.0.1 | Dev/build |
| node_modules/algoliasearch | 5.48.1 | Dev/build |
| node_modules/ansi-escapes | 7.3.0 | Dev/build |
| node_modules/ansi-regex | 6.3.0 | Dev/build |
| node_modules/ansi-styles | 6.2.3 | Dev/build |
| node_modules/balanced-match | 4.0.4 | Dev/build |
| node_modules/baseline-browser-mapping | 2.11.22 | Dev/build |
| node_modules/beasties | 0.4.1 | Dev/build |
| node_modules/body-parser | 2.3.0 | Dev/build |
| node_modules/body-parser/node_modules/content-type | 2.1.0 | Dev/build |
| node_modules/boolbase | 1.0.0 | Dev/build |
| node_modules/brace-expansion | 5.0.9 | Dev/build |
| node_modules/browserslist | 4.28.9 | Dev/build |
| node_modules/buffer-from | 1.1.2 | Dev/build |
| node_modules/bytes | 3.1.2 | Dev/build |
| node_modules/cacache | 20.0.4 | Dev/build |
| node_modules/cacache/node_modules/lru-cache | 11.5.2 | Dev/build |
| node_modules/call-bind-apply-helpers | 1.0.2 | Dev/build |
| node_modules/call-bound | 1.0.4 | Dev/build |
| node_modules/caniuse-lite | 1.0.30001810 | Dev/build |
| node_modules/chalk | 5.6.2 | Dev/build |
| node_modules/chardet | 2.2.0 | Dev/build |
| node_modules/chokidar | 5.0.0 | Dev/build |
| node_modules/chownr | 3.0.0 | Dev/build |
| node_modules/cli-cursor | 5.0.0 | Dev/build |
| node_modules/cli-spinners | 3.4.0 | Dev/build |
| node_modules/cli-truncate | 5.2.0 | Dev/build |
| node_modules/cli-width | 4.1.0 | Dev/build |
| node_modules/cliui | 9.0.1 | Dev/build |
| node_modules/cliui/node_modules/string-width | 7.2.0 | Dev/build |
| node_modules/cliui/node_modules/wrap-ansi | 9.0.2 | Dev/build |
| node_modules/color-convert | 2.0.1 | Dev/build |
| node_modules/color-name | 1.1.4 | Dev/build |
| node_modules/colorette | 2.0.20 | Dev/build |
| node_modules/content-disposition | 1.1.0 | Dev/build |
| node_modules/content-type | 1.0.5 | Dev/build |
| node_modules/convert-source-map | 1.9.0 | Dev/build |
| node_modules/cookie | 0.7.2 | Dev/build |
| node_modules/cookie-signature | 1.2.2 | Dev/build |
| node_modules/cors | 2.8.6 | Dev/build |
| node_modules/cross-spawn | 7.0.6 | Dev/build |
| node_modules/css-select | 6.0.0 | Dev/build |
| node_modules/css-what | 7.0.0 | Dev/build |
| node_modules/debug | 4.4.3 | Dev/build |
| node_modules/depd | 2.0.0 | Dev/build |
| node_modules/detect-libc | 2.1.2 | Dev/build; opcional |
| node_modules/dom-serializer | 2.0.0 | Dev/build |
| node_modules/domelementtype | 2.3.0 | Dev/build |
| node_modules/domhandler | 5.0.3 | Dev/build |
| node_modules/domutils | 3.2.2 | Dev/build |
| node_modules/dunder-proto | 1.0.1 | Dev/build |
| node_modules/ee-first | 1.1.1 | Dev/build |
| node_modules/electron-to-chromium | 1.5.427 | Dev/build |
| node_modules/emoji-regex | 10.6.0 | Dev/build |
| node_modules/encodeurl | 2.0.0 | Dev/build |
| node_modules/entities | 4.5.0 | Dev/build |
| node_modules/env-paths | 2.2.1 | Dev/build |
| node_modules/environment | 1.1.0 | Dev/build |
| node_modules/es-define-property | 1.0.1 | Dev/build |
| node_modules/es-errors | 1.3.0 | Dev/build |
| node_modules/es-object-atoms | 1.1.2 | Dev/build |
| node_modules/esbuild | 0.28.1 | Dev/build |
| node_modules/escalade | 3.2.0 | Dev/build |
| node_modules/escape-html | 1.0.3 | Dev/build |
| node_modules/etag | 1.8.1 | Dev/build |
| node_modules/eventemitter3 | 5.0.4 | Dev/build |
| node_modules/eventsource | 3.0.7 | Dev/build |
| node_modules/eventsource-parser | 3.1.1 | Dev/build |
| node_modules/exponential-backoff | 3.1.3 | Dev/build |
| node_modules/express | 5.2.1 | Dev/build |
| node_modules/express-rate-limit | 8.7.0 | Dev/build |
| node_modules/fast-deep-equal | 3.1.3 | Dev/build |
| node_modules/fast-uri | 3.1.7 | Dev/build |
| node_modules/fdir | 6.5.0 | Dev/build |
| node_modules/finalhandler | 2.1.1 | Dev/build |
| node_modules/forwarded | 0.2.0 | Dev/build |
| node_modules/fresh | 2.0.0 | Dev/build |
| node_modules/fs-minipass | 3.0.3 | Dev/build |
| node_modules/fsevents | 2.3.3 | Dev/build; opcional |
| node_modules/function-bind | 1.1.2 | Dev/build |
| node_modules/gensync | 1.0.0-beta.2 | Dev/build |
| node_modules/get-caller-file | 2.0.5 | Dev/build |
| node_modules/get-east-asian-width | 1.6.0 | Dev/build |
| node_modules/get-intrinsic | 1.3.0 | Dev/build |
| node_modules/get-proto | 1.0.1 | Dev/build |
| node_modules/glob | 13.0.6 | Dev/build |
| node_modules/glob-to-regexp | 0.4.1 | Dev/build |
| node_modules/gopd | 1.2.0 | Dev/build |
| node_modules/graceful-fs | 4.2.11 | Dev/build |
| node_modules/has-symbols | 1.1.0 | Dev/build |
| node_modules/hasown | 2.0.4 | Dev/build |
| node_modules/hono | 4.13.7 | Dev/build |
| node_modules/hosted-git-info | 9.0.3 | Dev/build |
| node_modules/hosted-git-info/node_modules/lru-cache | 11.5.2 | Dev/build |
| node_modules/htmlparser2 | 10.1.0 | Dev/build |
| node_modules/htmlparser2/node_modules/entities | 7.0.1 | Dev/build |
| node_modules/http-cache-semantics | 4.2.0 | Dev/build |
| node_modules/http-errors | 2.0.1 | Dev/build |
| node_modules/http-proxy-agent | 7.0.2 | Dev/build |
| node_modules/https-proxy-agent | 7.0.6 | Dev/build |
| node_modules/iconv-lite | 0.7.3 | Dev/build |
| node_modules/ignore-walk | 8.0.0 | Dev/build |
| node_modules/immutable | 5.1.9 | Dev/build |
| node_modules/inherits | 2.0.4 | Dev/build |
| node_modules/ini | 6.0.0 | Dev/build |
| node_modules/ip-address | 10.7.0 | Dev/build |
| node_modules/ipaddr.js | 1.9.1 | Dev/build |
| node_modules/is-extglob | 2.1.1 | Dev/build; opcional |
| node_modules/is-fullwidth-code-point | 5.1.0 | Dev/build |
| node_modules/is-glob | 4.0.3 | Dev/build; opcional |
| node_modules/is-interactive | 2.0.0 | Dev/build |
| node_modules/is-promise | 4.0.0 | Dev/build |
| node_modules/is-unicode-supported | 2.1.0 | Dev/build |
| node_modules/isexe | 2.0.0 | Dev/build |
| node_modules/istanbul-lib-coverage | 3.2.2 | Dev/build |
| node_modules/istanbul-lib-instrument | 6.0.3 | Dev/build |
| node_modules/jose | 6.2.12 | Dev/build |
| node_modules/js-tokens | 4.0.0 | Dev/build |
| node_modules/jsesc | 3.1.0 | Dev/build |
| node_modules/json-parse-even-better-errors | 5.0.0 | Dev/build |
| node_modules/json-schema-traverse | 1.0.0 | Dev/build |
| node_modules/json-schema-typed | 8.0.2 | Dev/build |
| node_modules/json5 | 2.2.3 | Dev/build |
| node_modules/jsonc-parser | 3.3.1 | Dev/build |
| node_modules/jsonparse | 1.3.1 | Dev/build |
| node_modules/listr2 | 9.0.5 | Dev/build |
| node_modules/listr2/node_modules/string-width | 7.2.0 | Dev/build |
| node_modules/listr2/node_modules/wrap-ansi | 9.0.2 | Dev/build |
| node_modules/lmdb | 3.5.1 | Dev/build; opcional |
| node_modules/log-symbols | 7.0.1 | Dev/build |
| node_modules/log-update | 6.1.0 | Dev/build |
| node_modules/log-update/node_modules/slice-ansi | 7.1.2 | Dev/build |
| node_modules/log-update/node_modules/string-width | 7.2.0 | Dev/build |
| node_modules/log-update/node_modules/wrap-ansi | 9.0.2 | Dev/build |
| node_modules/lru-cache | 5.1.1 | Dev/build |
| node_modules/magic-string | 0.30.21 | Dev/build |
| node_modules/make-fetch-happen | 15.0.6 | Dev/build |
| node_modules/math-intrinsics | 1.1.0 | Dev/build |
| node_modules/media-typer | 1.1.1 | Dev/build |
| node_modules/merge-descriptors | 2.0.0 | Dev/build |
| node_modules/mime-db | 1.54.0 | Dev/build |
| node_modules/mime-types | 3.0.2 | Dev/build |
| node_modules/mimic-function | 5.0.1 | Dev/build |
| node_modules/minimatch | 10.2.6 | Dev/build |
| node_modules/minipass | 7.1.3 | Dev/build |
| node_modules/minipass-collect | 2.0.1 | Dev/build |
| node_modules/minipass-fetch | 5.0.2 | Dev/build |
| node_modules/minipass-flush | 1.0.7 | Dev/build |
| node_modules/minipass-flush/node_modules/minipass | 3.3.6 | Dev/build |
| node_modules/minipass-flush/node_modules/yallist | 4.0.0 | Dev/build |
| node_modules/minipass-pipeline | 1.2.4 | Dev/build |
| node_modules/minipass-pipeline/node_modules/minipass | 3.3.6 | Dev/build |
| node_modules/minipass-pipeline/node_modules/yallist | 4.0.0 | Dev/build |
| node_modules/minipass-sized | 2.0.0 | Dev/build |
| node_modules/minizlib | 3.1.0 | Dev/build |
| node_modules/mrmime | 2.0.1 | Dev/build |
| node_modules/ms | 2.1.3 | Dev/build |
| node_modules/msgpackr | 1.12.1 | Dev/build; opcional |
| node_modules/msgpackr-extract | 3.0.4 | Dev/build; opcional |
| node_modules/mute-stream | 2.0.0 | Dev/build |
| node_modules/nanoid | 3.3.19 | Dev/build |
| node_modules/negotiator | 1.1.0 | Dev/build |
| node_modules/negotiator/node_modules/content-type | 2.1.0 | Dev/build |
| node_modules/node-addon-api | 6.1.0 | Dev/build; opcional |
| node_modules/node-gyp | 12.4.0 | Dev/build |
| node_modules/node-gyp-build-optional-packages | 5.2.2 | Dev/build; opcional |
| node_modules/node-gyp/node_modules/isexe | 4.0.0 | Dev/build |
| node_modules/node-gyp/node_modules/undici | 6.28.1 | Dev/build |
| node_modules/node-gyp/node_modules/which | 6.0.1 | Dev/build |
| node_modules/node-releases | 2.0.55 | Dev/build |
| node_modules/nopt | 9.0.0 | Dev/build |
| node_modules/npm-bundled | 5.0.0 | Dev/build |
| node_modules/npm-install-checks | 8.0.0 | Dev/build |
| node_modules/npm-normalize-package-bin | 5.0.0 | Dev/build |
| node_modules/npm-package-arg | 13.0.2 | Dev/build |
| node_modules/npm-packlist | 10.0.4 | Dev/build |
| node_modules/npm-pick-manifest | 11.0.3 | Dev/build |
| node_modules/npm-registry-fetch | 19.1.1 | Dev/build |
| node_modules/nth-check | 2.1.1 | Dev/build |
| node_modules/object-assign | 4.1.1 | Dev/build |
| node_modules/object-inspect | 1.13.4 | Dev/build |
| node_modules/on-finished | 2.4.1 | Dev/build |
| node_modules/once | 1.4.0 | Dev/build |
| node_modules/onetime | 7.0.0 | Dev/build |
| node_modules/ora | 9.3.0 | Dev/build |
| node_modules/ordered-binary | 1.6.1 | Dev/build; opcional |
| node_modules/p-map | 7.0.7 | Dev/build |
| node_modules/pacote | 21.5.1 | Dev/build |
| node_modules/parse5 | 8.0.1 | Prod |
| node_modules/parse5-html-rewriting-stream | 8.0.0 | Dev/build |
| node_modules/parse5-html-rewriting-stream/node_modules/entities | 6.0.1 | Dev/build |
| node_modules/parse5-sax-parser | 8.0.0 | Dev/build |
| node_modules/parse5/node_modules/entities | 8.1.0 | Prod |
| node_modules/parseurl | 1.3.3 | Dev/build |
| node_modules/path-key | 3.1.1 | Dev/build |
| node_modules/path-scurry | 2.0.2 | Dev/build |
| node_modules/path-scurry/node_modules/lru-cache | 11.5.2 | Dev/build |
| node_modules/path-to-regexp | 8.4.2 | Dev/build |
| node_modules/picocolors | 1.1.1 | Dev/build |
| node_modules/picomatch | 4.0.4 | Dev/build |
| node_modules/piscina | 5.2.0 | Dev/build |
| node_modules/pkce-challenge | 5.0.1 | Dev/build |
| node_modules/postcss | 8.5.28 | Dev/build |
| node_modules/postcss-media-query-parser | 0.2.3 | Dev/build |
| node_modules/postcss-safe-parser | 7.1.0 | Dev/build |
| node_modules/primeicons | 8.0.2 | Prod |
| node_modules/primeng | 21.1.10 | Prod |
| node_modules/primeng/node_modules/@primeuix/styled | 0.7.4 | Prod |
| node_modules/primeng/node_modules/@primeuix/styled/node_modules/@primeuix/utils | 0.6.4 | Prod |
| node_modules/primeng/node_modules/@primeuix/utils | 0.7.2 | Prod |
| node_modules/proc-log | 6.1.0 | Dev/build |
| node_modules/proxy-addr | 2.0.7 | Dev/build |
| node_modules/qs | 6.16.0 | Dev/build |
| node_modules/range-parser | 1.3.0 | Dev/build |
| node_modules/raw-body | 3.0.2 | Dev/build |
| node_modules/readdirp | 5.1.1 | Dev/build |
| node_modules/reflect-metadata | 0.2.2 | Dev/build |
| node_modules/require-from-string | 2.0.2 | Dev/build |
| node_modules/restore-cursor | 5.1.0 | Dev/build |
| node_modules/rfdc | 1.4.1 | Dev/build |
| node_modules/rolldown | 1.0.0-rc.4 | Dev/build |
| node_modules/rollup | 4.63.1 | Dev/build |
| node_modules/router | 2.2.0 | Dev/build |
| node_modules/rxjs | 7.8.2 | Prod |
| node_modules/safer-buffer | 2.1.2 | Dev/build |
| node_modules/sass | 1.97.3 | Dev/build |
| node_modules/sass/node_modules/chokidar | 4.0.3 | Dev/build |
| node_modules/sass/node_modules/readdirp | 4.1.2 | Dev/build |
| node_modules/semver | 7.7.4 | Dev/build |
| node_modules/send | 1.2.1 | Dev/build |
| node_modules/serve-static | 2.2.1 | Dev/build |
| node_modules/setprototypeof | 1.2.0 | Dev/build |
| node_modules/shebang-command | 2.0.0 | Dev/build |
| node_modules/shebang-regex | 3.0.0 | Dev/build |
| node_modules/side-channel | 1.1.1 | Dev/build |
| node_modules/side-channel-list | 1.0.1 | Dev/build |
| node_modules/side-channel-map | 1.0.1 | Dev/build |
| node_modules/side-channel-weakmap | 1.0.2 | Dev/build |
| node_modules/signal-exit | 4.1.0 | Dev/build |
| node_modules/sigstore | 4.1.1 | Dev/build |
| node_modules/slice-ansi | 8.0.0 | Dev/build |
| node_modules/smart-buffer | 4.2.0 | Dev/build |
| node_modules/socks | 2.8.10 | Dev/build |
| node_modules/socks-proxy-agent | 8.0.5 | Dev/build |
| node_modules/source-map | 0.7.6 | Dev/build |
| node_modules/source-map-js | 1.2.1 | Dev/build |
| node_modules/source-map-support | 0.5.21 | Dev/build |
| node_modules/source-map-support/node_modules/source-map | 0.6.1 | Dev/build |
| node_modules/spdx-exceptions | 2.5.0 | Dev/build |
| node_modules/spdx-expression-parse | 4.0.0 | Dev/build |
| node_modules/spdx-license-ids | 3.0.23 | Dev/build |
| node_modules/ssri | 13.0.1 | Dev/build |
| node_modules/statuses | 2.0.2 | Dev/build |
| node_modules/stdin-discarder | 0.3.2 | Dev/build |
| node_modules/string-width | 8.2.2 | Dev/build |
| node_modules/strip-ansi | 7.2.0 | Dev/build |
| node_modules/tar | 7.5.22 | Dev/build |
| node_modules/tar/node_modules/yallist | 5.0.0 | Dev/build |
| node_modules/tinyglobby | 0.2.15 | Dev/build |
| node_modules/toidentifier | 1.0.1 | Dev/build |
| node_modules/tslib | 2.8.1 | Prod |
| node_modules/tuf-js | 4.1.0 | Dev/build |
| node_modules/type-is | 2.1.0 | Dev/build |
| node_modules/type-is/node_modules/content-type | 2.1.0 | Dev/build |
| node_modules/typescript | 5.9.3 | Dev/build |
| node_modules/undici | 7.29.1 | Dev/build |
| node_modules/unpipe | 1.0.0 | Dev/build |
| node_modules/update-browserslist-db | 1.3.3 | Dev/build |
| node_modules/validate-npm-package-name | 7.0.2 | Dev/build |
| node_modules/vary | 1.1.2 | Dev/build |
| node_modules/vite | 7.3.6 | Dev/build |
| node_modules/watchpack | 2.5.1 | Dev/build |
| node_modules/weak-lru-cache | 1.2.2 | Dev/build; opcional |
| node_modules/which | 2.0.2 | Dev/build |
| node_modules/wrap-ansi | 6.2.0 | Dev/build |
| node_modules/wrap-ansi/node_modules/ansi-regex | 5.0.1 | Dev/build |
| node_modules/wrap-ansi/node_modules/ansi-styles | 4.3.0 | Dev/build |
| node_modules/wrap-ansi/node_modules/emoji-regex | 8.0.0 | Dev/build |
| node_modules/wrap-ansi/node_modules/is-fullwidth-code-point | 3.0.0 | Dev/build |
| node_modules/wrap-ansi/node_modules/string-width | 4.2.3 | Dev/build |
| node_modules/wrap-ansi/node_modules/strip-ansi | 6.0.1 | Dev/build |
| node_modules/wrappy | 1.0.2 | Dev/build |
| node_modules/y18n | 5.0.8 | Dev/build |
| node_modules/yallist | 3.1.1 | Dev/build |
| node_modules/yargs | 18.0.0 | Dev/build |
| node_modules/yargs-parser | 22.0.0 | Dev/build |
| node_modules/yargs/node_modules/string-width | 7.2.0 | Dev/build |
| node_modules/yoctocolors | 2.2.0 | Dev/build |
| node_modules/yoctocolors-cjs | 2.1.3 | Dev/build |
| node_modules/zod | 4.3.6 | Dev/build |
| node_modules/zod-to-json-schema | 3.25.2 | Dev/build |
