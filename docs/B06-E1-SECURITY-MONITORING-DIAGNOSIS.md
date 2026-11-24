# SeenIt — B06-E1 · Diagnóstico de monitoramento de segurança

Data de referência: 24/11/2026, America/Sao_Paulo. **Verificado** indica observação direta nesta etapa. **Histórico** indica evidência de B06-A/B/C/D, inclusive relatos do operador quando identificados. **Proposta** indica ação futura. **Sem confirmação** indica limite da evidência.

## 1. Objetivo

Definir uma rotina de baixo custo para detectar vulnerabilidades e correções no código entregue ao navegador, no backend, na imagem Linux/amd64 e nos codecs incorporados, com revisão humana antes de qualquer atualização. Escopo investigativo e documental.

## 2. Estado inicial do Git

`main`, `HEAD`, a referência local `origin/main` e `refs/heads/main` consultada por `git ls-remote` apontavam para `9f34bcab1b1e9a693f6c1dd7edb854752dddc90c`. A árvore principal estava limpa. Foram registrados os SHAs de 11 branches locais e referências remotas, duas worktrees e os SHA-256 de 248 arquivos da árvore principal. As branches locais registradas foram `main`, `maintenance-phase1`, `b05-b1-angular-security`, `fix/guest-route-guard`, `security/b05-b2-dependency-review`, `security/b05-b3-sqlite-review`, `security/b05-b4-container-review`, `security/b06-a1-hardening-diagnosis`, `security/b06-b4-docker-runtime`, `security/b06-c4-skiasharp-update` e `security/b06-d4-buildhost-publish`.

A worktree `.worktrees/maintenance-phase1` apontava para `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. O status expandido confirmou seis modificações rastreadas e nove arquivos não rastreados. Os 161 SHA-256 coincidiram com o snapshot B06-C6. O inventário local Docker retornou 29 imagens, dois containers, quatro volumes e 276 entradas de cache. As 22 referências de imagem do snapshot B06-C6 foram reencontradas por ID. O inventário incluía as imagens de controle, candidata e build B06-D2 e as imagens final e build B06-D3.

## 3. Histórico B06-A/B/C/D

| Frente | Mudança e evidência histórica | Validação e rollback |
|---|---|---|
| A | Render termina TLS na borda. A opção `ReverseProxy__HttpsRedirectionHandledByEdge=true` foi aplicada segundo o operador. O código exige proxies explicitamente confiáveis para forwarded headers. Permaneceram warnings do key ring, encryptor XML e precedência de portas. | Health, HTTPS sem loops e fluxos funcionais reportados pelo operador. Em rollback autorizado, restaurar em conjunto a responsabilidade pelo redirect e a configuração compatível do deploy. [B06-A](B06-A-FINAL-SUMMARY.md) |
| B | Digest ASP.NET atualizado preservando SDK 10.0.404 e frameworks 10.0.15. Corrigiu seis achados glibc (CVE-2026-18374, 84243, 8674, 86805, 95818 e 97399) com 2.39-0ubuntu8.12 e CVE-2026-82560 com perl-base 5.38.2-3.2ubuntu0.7. O B05-B4 já havia corrigido cinco achados de frameworks, um do APT e sete do PCRE2. | Comparação de scans e testes Linux/amd64. Rollback preferencial por artefato estável. Restaurar o digest anterior só com nova validação e aceitação explícita dos achados reintroduzidos. [B06-B](B06-B-FINAL-SUMMARY.md), [B05-B4](B05-B4-FINAL-SUMMARY.md) |
| C | SkiaSharp e NativeAssets de 4.155.0 para 4.155.2 em conjunto. Seis backports documentados para Skia: CVE-2026-118340, 121086, 126912, 119261, 126871 e 126882. Libpng permaneceu em 1.6.58. | Verify, hash nativo, `ldd`, testes de JPEG/PNG/WebP, orientação EXIF, dimensões, transparência, rejeições e ensaio limitado de recursos. Rollback coordenado dos dois pacotes e verificação do hash histórico do controle, com retorno dos seis riscos mitigados. [B06-C](B06-C-FINAL-SUMMARY.md) |
| D | Target `ExcludeBuildHostsFromApiPublish` remove 46 arquivos de `BuildHost-netcore` e `BuildHost-net472`. Foram preservados 22 arquivos de publish e 24 em `/app`, inclusive o mesmo asset Skia. Economia comprimida de 1,44% no ensaio. | Docker verify, AvatarChecks (16 grupos), CoreChecks (72 HTTP), registro, EF tooling, codecs e PostgreSQL descartável, incluindo transações e concorrência. Rollback por artefato estável ou reversão isolada do target, com verify e conferência dos 46 arquivos. [B06-D](B06-D-FINAL-SUMMARY.md), [B06-D3](B06-D3-BUILDHOST-PUBLISH-IMPLEMENTATION.md) |

Os deploys Render Live e Vercel Ready e os smokes de produção são evidência histórica informada pelo operador. O SHA integrado e os hashes locais não identificam por si só o binário remoto.

## 4. Inventário atual das dependências

| Superfície | Versões e origem |
|---|---|
| API direta, verificado no csproj | `net10.0`, JwtBearer 10.0.0, Microsoft.AspNetCore.OpenApi 10.0.0, Microsoft.OpenApi 2.7.5, EF Core Design 10.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0, System.Security.Cryptography.Pkcs 9.0.22 e SkiaSharp/NativeAssets.Linux.NoDependencies 4.155.2. OpenApi e Pkcs têm pins corretivos documentados no manifesto. |
| Transitivas, grafo histórico Linux-x64 do build D3 | EF Core, Abstractions, Analyzers e Relational 10.0.0, Npgsql 10.0.0, família Microsoft.IdentityModel e System.IdentityModel.Tokens.Jwt 8.0.1. Design/build incluem Roslyn 4.14.0, analyzers 3.11.0, Microsoft.Build 17.7.2, Framework/Tasks/Utilities 17.14.28, Humanizer.Core 2.14.1, Mono.TextTemplating 3.0.0, Newtonsoft.Json 13.0.3, System.Composition 9.0.0 e ferramentas EF 10.0.0. O grafo inclui assets Win32 e macOS 4.155.2, fora da seleção publicada Linux. |
| Frontend, verificado em manifesto e lock | Angular de aplicação e compiler-cli 21.2.26, CLI e build 21.2.30, PrimeNG 21.1.10, PrimeIcons 8.0.2 resolvido, @primeuix/themes 3.0.1, RxJS 7.8.2 e tslib 2.8.1. TypeScript 5.9.3, Vite 7.3.7, esbuild 0.28.1, pacote 21.5.1, glob 14.0.6 e minimatch 11.0.8 no grafo de desenvolvimento. O lock tem 529 entradas de pacotes, 502 marcadas dev e 27 restantes, incluindo peers e variantes de plataforma. O lock na raiz tem grafo vazio. |
| Imagem final histórica D3 | Ubuntu Noble, runtime .NET/ASP.NET 10.0.15, usuário `app`, porta interna 10000, CA pública 0444, marcador verify e publicação framework-dependent. O SPDX histórico tem 192 registros (167 com purl deb), incluindo glibc, Perl, GnuTLS, OpenSSL, CA, ICU e utilitários. O SDK, os executáveis de verificação e o tooling de EF pertencem ao build/ensaio. Conferir arquivos realmente publicados antes de usar um pacote do grafo de restore como evidência de exposição. |
| Navegador | O frontend SPA entrega JavaScript compilado e assets. Código importado de Angular, PrimeNG/RxJS, estilos, temas e ícones pode compor essa entrega. A presença ou a flag do lock demonstram resolução npm, enquanto a entrega por módulo depende da saída de build e do carregamento de chunks. O bundle implantado na Vercel ficou sem inventário direto. |

Referências locais: [API](../backend/SeenIt.Api/SeenIt.Api.csproj), [Dockerfile](../backend/SeenIt.Api/Dockerfile), [manifesto frontend](../frontend/package.json), [lock frontend](../frontend/package-lock.json), [inventário D3](B06-D3-BUILDHOST-PUBLISH-IMPLEMENTATION.md).

## 5. Mecanismos já existentes

| Mecanismo | Cobertura e saída | Recorrência, acesso e manutenção |
|---|---|---|
| GitHub Advisory Database | Base pública com CVE/GHSA e faixas por ecossistema, inclui alguns avisos de software nativo. A presença real exige cruzamento com versão e artefato. | Consulta manual ou API periódica. Leitura pública, autenticação opcional conforme endpoint/limite. Revisar divergências e aliases. [GitHub Advisory Database](https://github.com/advisories) |
| OSV | Consulta pública por ecossistema e versão, lote ou SBOM. Aplica-se a diretas, transitivas ou fontes identificadas quando o ecossistema tem dados. Cobertura de código incorporado depende da identificação. | Consulta manual/lote, API pública, sem credencial no cenário proposto. Registrar resposta inconclusiva ou limite. Deduplicar com GHSA/CVE. [OSV](https://google.github.io/osv.dev/) |
| MSRC e releases .NET | Avisos oficiais Microsoft, faixa afetada e correção de runtime/SDK. Cruzar com pacote NuGet ou framework efetivo, pois são entidades distintas. | Leitura pública e revisão por release ou mensal. Sem secret. [MSRC](https://msrc.microsoft.com/update-guide), [metadados .NET 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) |
| Ubuntu Security e MCR | Canonical informa estado por pacote-fonte e distribuição. MCR identifica manifestos, plataforma e metadados da base. Cruzar versão binária Noble com o pacote-fonte e a imagem. | Leitura pública recorrente, sem secret. Validar backports e diferenças de base antes de abrir atualização. |
| SkiaSharp, Skia e codecs | Releases, advisories, submódulo, DEPS, patches e fingerprint binário. Cobre dependências estáticas mediante análise de proveniência. | Revisar manualmente a cada release pertinente ou mensalmente. Fontes públicas, sem secret. Maior esforço de triagem. [SkiaSharp releases](https://github.com/mono/SkiaSharp/releases), [libpng advisories](https://github.com/pnggroup/libpng/security/advisories) |

As próximas quatro seções cobrem os mecanismos habilitados ou historicamente executados. Fontes públicas e ferramentas locais podem integrar uma rotina recorrente por invocação manual ou agendamento autorizado. Estado de conta e limites aplicáveis são registrados abaixo.

## 6. Dependabot e GitHub

Repositório público, branch padrão `main`. Consultas REST autenticadas e de leitura confirmaram:

| Recurso | Estado | Cobertura e impacto |
|---|---|---|
| Dependabot alerts | **Desabilitado**. Endpoints exibiram explicitamente o motivo em 403/404. | Alertas de dependências diretas e transitivas reconhecidas pelo grafo e pelos advisories. Conferir a cobertura NuGet transitiva efetivamente resolvida antes de confiar nela. Não inspeciona codecs estáticos ou pacotes instalados na base. |
| Dependabot security updates | **Desabilitado**. Campo REST disabled e endpoint `enabled:false, paused:false`. | Pode abrir PR de correção quando o ecossistema permite resolver a versão. A habilitação futura exigiria autorização, revisão e triagem de PRs. |
| Dependabot version updates | Sem `.github/dependabot.yml` na branch padrão. | Configuração futura por ecossistema/diretório para npm, NuGet e Docker. PRs agendados, inclusive alterações de versão sem correção de segurança. Validar suporte efetivo ao formato tag+digest/MCR e manter a conferência manual AMD64. |
| Code scanning | Default setup `not-configured`, API retornou `no analysis found`. | CodeQL/SARIF pode detectar padrões no código C# e JavaScript/TypeScript. O escopo difere de SCA, SBOM e auditoria de codecs. Pode recorrer por Actions, com custo de setup e triagem. |
| Secret scanning | **Habilitado**, push protection **habilitado**. Lista retornou vazia. Padrões não associados a fornecedores e validity checks desabilitados. | Busca material compatível com os padrões no repositório e alerta quando há detecção, com proteção no push. Cobertura independe da árvore de pacotes e da presença de nativos. |
| GitHub Actions | **Habilitado**, ações permitidas sem pin por SHA exigido pela configuração, zero workflows registrados. | Infraestrutura para auditorias por agendamento ou disparo manual. A cobertura depende dos comandos adotados. Uma rotina de leitura pode usar `GITHUB_TOKEN` com `contents: read` e zero secrets próprios. |
| Proteção de `main` | `protected:false` e lista de regras aplicáveis vazia. | Sem novos checks obrigatórios observados. Checks externos de deploy historicamente reportados podem existir sem ser obrigatórios. |

Leitura pública de docs e repositório é distinta da leitura de configuração administrativa, feita com a credencial já disponível. Nenhum escopo foi acrescentado. [Dependabot](https://docs.github.com/en/code-security/dependabot), [Code scanning](https://docs.github.com/en/code-security/code-scanning), [Secret scanning](https://docs.github.com/en/code-security/secret-scanning).

## 7. Auditoria NuGet

O Dockerfile executa `dotnet package list --no-restore --vulnerable --include-transitive` para API, AvatarChecks e CoreChecks no build. O log histórico D3 confirma três consultas sem achados nas fontes daquela execução, e o relatório D3 inclui uma auditoria separada de RegisterIntegrationChecks. Nesta etapa, a consulta ao NuGet.org sobre cópia isolada do grafo de build D3 da API, usando SDK local 10.0.301, terminou com código 0 e sem entradas de vulnerabilidade no JSON. Isso atualiza a leitura do feed para aquele grafo, com cobertura limitada à API e à resolução histórica.

Na rotina proposta, resolver em ambiente descartável com SDK fixado, guardar TFM/RID e fontes e registrar aviso ou indisponibilidade de feed como falha de cobertura. Auditar API e checks com transitivas. Verificar os alvos de build/design separadamente da publicação, inclusive Roslyn e EF tooling. Usar advisory, faixa e caminho transitivo para confirmar. No .NET 10, revisar configuração de audit e warnings de fontes durante restore. A auditoria NuGet não substitui MSRC para frameworks compartilhados nem inventário de sistema/objetos estáticos. Sem licença ou credencial extra para NuGet.org, com custo de rede e restore. [NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

## 8. Auditoria npm

Consulta ao registry atual sobre `frontend/package-lock.json` com Node 24.16.0 e npm 11.13.0. `npm audit --json --package-lock-only --ignore-scripts` e a variante `--omit=dev` retornaram código 0 e zero vulnerabilidades. O contador bruto de dependências no JSON permaneceu 529 nas duas saídas. A classificação de exposição deve considerar caminhos do lock e a composição do bundle. As consultas usaram cache temporário isolado. Nenhum pacote foi instalado.

Rodar os dois recortes mensalmente e guardar saída e código. Interpretar erros de registry, rede ou JSON inválido como auditoria incompleta. Para achado de build, revisar se código externo ou entradas de terceiros alcançam o tooling. Para achado de runtime, conferir o bundle e o fluxo concreto de navegador. Registry público sem token no cenário atual. Considerar que auditoria envia metadados do grafo ao registry. [npm audit](https://docs.npmjs.com/cli/v11/commands/npm-audit/).

## 9. Docker Scout

O scan histórico B06-D3 com Scout 1.28.0 indexou 160 pacotes e registrou 17 CVEs em nove pacotes afetados (13 Medium e quatro Low). O SPDX tem 192 registros sob outra representação. Estudos B06-B/C/D mostram que um resultado muda com base de advisories, versão do scanner e reconhecimento de pacote. O inventário de imagens locais foi conferido nesta etapa. Nenhum scan ou build novo foi executado.

A rotina recorrente deve fixar ID/digest do artefato final Linux/amd64 e guardar SBOM e SARIF, versão da CLI, data e revisão de feed quando disponível. Comparar a imagem final com o scan anterior e considerar SDK separadamente em investigações de build. Atribuição por pacote-fonte pode sinalizar um binário irmão ausente. O Scout cobre o que identifica da imagem, incluindo pacotes de SO e gerenciados, com lacunas em ELF estático, variantes e componentes não catalogados. Verificar login/entitlement e quota antes de ligar análise contínua em registry. [B06-D3](B06-D3-BUILDHOST-PUBLISH-IMPLEMENTATION.md), [Scout plans](https://docs.docker.com/scout/subscription/).

## 10. SkiaSharp e codecs nativos

| Componente | Evidência histórica no artefato/fonte |
|---|---|
| SkiaSharp e NativeAssets Linux | NuGet 4.155.2. Submódulo Skia `e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c` reconfirmado. SHA-256 do `libSkiaSharp.so` no D3: `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef`. |
| libpng | 1.6.58 no binário e header do pin `3061454d980de7d53608f594194cfac722721d2a`, com evidência de incorporação estática. |
| libjpeg-turbo | 3.1.4.1 no binário, pin `9217719d3a58633923b096af4c1d50d304768a64`, estática. |
| libwebp | 1.6.0 declarada no pin `4fa21912338357f89e4fd51cf2368325b59e9bd9` com símbolos de decode/encode no binário. Versão interna sem medição independente. |
| GIF/Wuffs e auxiliares | Símbolos GIF/Wuffs, pin de Wuffs `e3f919ccfe3ef542cfc983a82146070258fb57f8`, zlib no DEPS e evidência de inflate. Inventário e versões internas incompletos. Probing de formato pode ocorrer antes da rejeição. |

**CVE-2026-46675.** O mantenedor confirma faixa libpng 1.6.0–1.6.58, CVSS 5,9 e correção em **1.6.59** e no trunk 1.8.0. O componente identificado no asset atual está na faixa afetada. A condição requer um uso específico do leitor sequencial no encerramento da leitura. O leitor progressivo examinado em B06-C está fora dessa condição. Não há evidência de exploração pelo endpoint de avatar ou de patch incorporado no 4.155.2. A atualização de um pacote Ubuntu libpng não altera o código estático nesse asset. [Advisory oficial](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j), [B06-C1](B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md).

A API upstream retornou v4.156.2 como release estável marcada como latest e lista vazia nos advisories de SkiaSharp. Esse resultado não prova cobertura de seus subcomponentes. O submódulo do v4.156.2 aponta para `0d6629ea961e42997af39b060c099a471b6088e1`. A leitura direta do DEPS de v4.156.2 confirmou os mesmos pins para PNG, JPEG e WebP. A tag v4.157.0 existe, com submódulo `51cb594d773b5383a45065ea3c8cbec398ad2c36`, e seu pin PNG é historicamente igual ao de 4.155.2. Registrar pacote, revisão, configuração GN, DEPS, commit corretivo e SHA do asset separadamente. Conferir arquivo nativo Linux-x64 com ferramentas de inventário de ELF e marcadores de versão, e manter diferenças de evidência explícitas. Na ocorrência de advisory relevante ou mudança de pin, revisar a seleção do decoder e repetir os testes de codec com fixtures benignas. [Release 4.156.2](https://github.com/mono/SkiaSharp/releases/tag/v4.156.2), [DEPS 4.156.2](https://github.com/mono/skia/blob/0d6629ea961e42997af39b060c099a471b6088e1/DEPS).

## 11. Imagens-base e .NET

| Base | Digest fixado no Dockerfile (SHA-256) | Índice atual da tag (SHA-256) | Manifesto atual Linux/amd64 (SHA-256) |
|---|---|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0-noble` | `ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` | `8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d` | `cd28130a4da76d2dc11c360b6d9f18c673d6fe15252f91c073ba358a1318cfb0` |
| `mcr.microsoft.com/dotnet/aspnet:10.0-noble` | `3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde` | `aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21` | `f7b58ba6e8f2cbe11c3cb58e8a5f5f2a077834095e24f21e6eb969ab4f9eb4bf` |

Consulta por `docker buildx imagetools inspect` e leitura da config de manifestos Linux/amd64 no MCR. Ambos os manifestos novos declaram .NET e ASP.NET **10.0.15** e a base SDK também declara SDK **10.0.404**, iguais às versões históricas das bases fixadas. O feed oficial identifica 10.0.15, de 10/11/2026, como release de segurança mais recente do canal consultado. A mudança de digest com as mesmas versões declaradas pode refletir alterações de SO ou de empacotamento. O diff de pacotes/CVEs dos candidatos ainda exige SBOM ou comparação controlada. Digest de índice, manifesto de plataforma e ID local têm papéis distintos.

Mensalmente e por release, resolver a tag no MCR, registrar índice e filho AMD64, conferir config e cruzar avisos MSRC e USNs Noble. Havendo benefício candidato, medir diff de pacotes versus pin, verificar se os backports esperados estão presentes e reproduzir verify antes de propor atualização. [Metadados oficiais .NET](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [Ubuntu Security Notices](https://ubuntu.com/security/notices), [Política de atualização das imagens .NET](https://github.com/dotnet/dotnet-docker#update-policy).

## 12. Frontend e Node.js

Node local 24.16.0 e npm 11.13.0 foram medidos e coincidem com a referência histórica de build local. Angular 21 aceita Node ^20.19.0, ^22.12.0 ou ^24.0.0, TypeScript >=5.9.0 <6.0.0 e RxJS ^6.5.3 ou ^7.4.0. Os peers observados de PrimeNG 21.1.10 exigem Angular 21 (core ^21.0.7) e RxJS ^6.0.0 ou ^7.8.1. O manifesto não define `engines.node` e o arquivo Vercel contém rewrites. Versão e patch do Node na Vercel permanecem sem confirmação. Conferir seleção e logs de build no painel pelo operador.

Taxonomia da triagem: browser (bundle e assets), build (Node, CLI, Vite, esbuild e transitivas) e publicação (configuração e dependências efetivamente carregadas). Cruzar npm audit e, se autorizado posteriormente, Dependabot com advisories Angular/PrimeNG e Node.js. Antes da integração, conferir peers e lock, executar `npm ci` e `npm run build` em diretório/runner isolado e usar preview/smoke autorizado para login, navegação, perfil e catálogo. Registrar estatísticas do bundle quando a exposição depender de inclusão em chunk. [Angular](https://angular.dev/reference/versions), [PrimeNG releases](https://github.com/primefaces/primeng/releases), [Node.js release schedule](https://github.com/nodejs/Release#release-schedule), [Vercel Node.js](https://vercel.com/docs/functions/runtimes/node-js/node-js-versions).

## 13. Pendências de segurança conhecidas

Cada linha corresponde a um dos 17 IDs históricos. **M/L** indicam Medium/Low no Scout D3. As versões são as do inventário histórico local por pacote-fonte Ubuntu. O estado Noble é da Canonical ou, para o ID rejeitado, do CNA. As conclusões de alcance valem para os caminhos examinados e não equivalem a teste de produção.

| ID e fonte | Pacote e versão no inventário | Scout | Correção/estado atual | Condição e alcance SeenIt |
|---|---|---|---|---|
| [CVE-2026-67693](https://ubuntu.com/security/CVE-2026-67693) | gnutls28 3.8.3-1.1ubuntu3.6 | M | Noble vulnerável, correção adiada. Upstream contesta. | KU/EKU contraditórios em validação X.509 GnuTLS. Biblioteca presente, uso pelo fluxo da API indeterminado. |
| [CVE-2026-88647](https://ubuntu.com/security/CVE-2026-88647) | gnutls28 3.8.3-1.1ubuntu3.6 | M | Noble vulnerável, correção adiada. Upstream contesta. | Combinação específica de SAN SRV otherName e fallback de Common Name. Uso do validador em runtime indeterminado. |
| [CVE-2026-88648](https://ubuntu.com/security/CVE-2026-88648) | gnutls28 3.8.3-1.1ubuntu3.6 | M | Noble vulnerável, correção adiada. Upstream contesta. | Restrições excluídas de msUPN/SRV otherName em validação X.509. Alcance indeterminado. |
| [CVE-2026-95184](https://ubuntu.com/security/CVE-2026-95184) | gnutls28 3.8.3-1.1ubuntu3.6 | M | Noble vulnerável, correção adiada. Upstream contesta. | Validação de subtree permitido de nome X.509. Alcance indeterminado. |
| [CVE-2026-95209](https://www.cve.org/CVERecord?id=CVE-2026-95209) | gnutls28 3.8.3-1.1ubuntu3.6 | M | **REJECTED** no CNA. Conservar ID como ruído histórico do feed. | Registro rejeitado. Não há condição válida atribuível pelo ID. |
| [CVE-2026-95210](https://ubuntu.com/security/CVE-2026-95210) | gnutls28 3.8.3-1.1ubuntu3.6 | M | Noble vulnerável, correção adiada. Upstream contesta. | Processamento de extensão crítica keyUsage inválida pelo GnuTLS. Caminho em runtime indeterminado. |
| [CVE-2026-102473](https://ubuntu.com/security/CVE-2026-102473) | dash 0.5.12-6ubuntu5 | M | Noble vulnerável, correção adiada. | Shell compilado sem fnmatch de libc e glob sobre nomes controláveis. Sem shell no entrypoint ou caminho identificado na API. Configuração do binário sem confirmação. |
| [CVE-2026-102474](https://ubuntu.com/security/CVE-2026-102474) | dash 0.5.12-6ubuntu5 | M | Noble vulnerável, correção adiada. | Processamento de escape Unicode por built-in de shell com entrada local controlável. Sem caminho equivalente no runtime observado. |
| [CVE-2026-18477](https://ubuntu.com/security/CVE-2026-18477) | tar 1.35+dfsg-3ubuntu0.4 | M | Noble vulnerável, sem versão corrigida indicada. | Restore incremental com influência sobre diretório e contexto de restauração. Sem restore no runtime identificado. |
| [CVE-2026-18508](https://ubuntu.com/security/CVE-2026-18508) | tar 1.35+dfsg-3ubuntu0.4 | M | Noble vulnerável, sem versão corrigida indicada. | Extração com fronteira top-level e hardlinks controláveis. O fluxo de upload examinado decodifica imagens e não extrai TAR. |
| [CVE-2026-2219](https://ubuntu.com/security/CVE-2026-2219) | dpkg 1.22.6ubuntu6.6 | M | **Corrigida na versão já inventariada**, conforme Canonical: 1.22.6ubuntu6.6. Validar versão efetiva em novo scan antes de encerrar eventual ocorrência. | Exige `dpkg-deb` descompactando pacote zstd. Esse uso não foi demonstrado no runtime. Conflito de feed, versão ou data do scanner. |
| [CVE-2026-76642](https://ubuntu.com/security/CVE-2026-76642) | util-linux 2.39.3-9ubuntu6.6 | M | Noble vulnerável, sem versão Noble corrigida indicada. | Hooks no caminho de mount, configurações e privilégios específicos. API roda como `app` e não há operações mount no fluxo examinado. Host Render fora do inventário. |
| [CVE-2026-121876](https://ubuntu.com/security/CVE-2026-121876) | systemd 255.4-1ubuntu8.18 | M | Noble vulnerável, correção adiada. | Gerador GPT no boot de guest VM confidencial. Container da API inicia com dotnet e não executa esse gerador. Não aplicável à topologia documentada da aplicação. |
| [CVE-2026-40228](https://ubuntu.com/security/CVE-2026-40228) | systemd 255.4-1ubuntu8.18 | L | Correções upstream referenciadas, Noble vulnerável com correção adiada. | Journald ativo com encaminhamento a terminais. Container sem esse caminho observado. Escopo do host não verificado. |
| [CVE-2024-56433](https://ubuntu.com/security/CVE-2024-56433) | shadow 1:4.13+dfsg1-4ubuntu3.2 | L | Noble vulnerável, correção adiada. Sem fix upstream indicado pela Canonical. | Sobreposição de subuid com UIDs da rede, acesso local e contexto como NFS. Topologia do container não demonstra essa combinação. |
| [CVE-2025-5222](https://ubuntu.com/security/CVE-2025-5222) | icu 74.2-1ubuntu3.1 | L | Patch upstream identificado, Noble vulnerável sem versão corrigida indicada. | Ferramenta `genrb` processando recursos hostis. Uso de biblioteca ICU em runtime é distinto. Caminho de `genrb` ausente na evidência histórica da imagem. |
| [CVE-2026-105712](https://ubuntu.com/security/CVE-2026-105712) | gnupg2 2.4.4-2ubuntu17.6 | L | Corrigida upstream em 2.5.19. Noble vulnerável sem backport indicado. | Extração `gpgtar` de arquivo não confiável em diretório com symlink preexistente. Histórico encontrou `gpgv`, sem `gpgtar`. Não aplicável ao conteúdo conhecido. |

Correção upstream disponível ainda pode depender de backport Noble ou nova imagem oficial. Os registros adiados e vulneráveis sem versão corrigida indicada ficam abertos para revisão. As justificativas de alcance expiram se mudar o entrypoint, caminho de upload, backend TLS, topologia ou inventário. Nenhuma exploração foi testada. A pendência libpng é adicional aos 17 e está detalhada na seção 10. [Inventário original](B06-B1-DOCKER-VULNERABILITY-INVENTORY.md), [evidência mais recente de scan](B06-D3-BUILDHOST-PUBLISH-IMPLEMENTATION.md).

## 14. Monitoramento de infraestrutura

| Tipo | Evidência a revisar | Sinal de ação |
|---|---|---|
| Vulnerabilidades | Feed de sistema/runtime e identidade da imagem entregue. | SHA/digest divergentes, patch relevante disponível ou versão exposta confirmada. |
| Operacional | Sucesso e horário do deploy, SHA, verify, inicialização, binding 10000, reinícios, erros recorrentes, GET HTTPS `/health` e smoke funcional. | Falha de inicialização, health persistente fora do esperado ou regressão de login/avatar/persistência. Health confirma resposta do processo. Smoke cobre dependências necessárias com conta de teste autorizada. |
| Configuração | Render: delegação HTTPS, proxies confiáveis, HTTP_PORTS/URLS, host público e ciclo de certificados externos. Conferir validade da CA pública distribuída separadamente. Vercel: domínio, versão Node, rewrites e status Ready. GitHub: rules, branch protection e checks obrigatórios. | Redirecionamento em loop, certificado próximo de expirar (proposta de investigar com 30 dias), alteração de ingressos, flags divergentes ou check inesperado que mude a entrega. |
| Data Protection | Logs de inicialização, consumidores de key ring, diretório/volume e proteção em repouso, acesso e rotação. O relatório A1 não identificou consumidor funcional então e JWT usa chave própria. | Surgimento de consumidor, perda de estado após restart ou mudança no armazenamento. Investigar persistência e encryptor juntos antes de decidir mudança. |

Nesta etapa não houve leitura autenticada de Render/Vercel nem teste operacional em produção. [Diagnóstico de HTTPS e Data Protection](B06-A1-DATAPROTECTION-HTTPS-DIAGNOSIS.md), [Checklist Render](../backend/SeenIt.Api/RENDER-ENV-CHECKLIST.md).

## 15. Alternativas de automação

| Alternativa | Cadência, recursos e manutenção | Saída, notificações e falsos positivos | Permissões, secrets e custo |
|---|---|---|---|
| A. Manual | Semanal para alertas, mensal para auditorias. Pouca infraestrutura e depende de disciplina do operador. | Registro local por execução. Aviso manual para achados acionáveis e triagem explícita de feeds divergentes. | Fontes públicas dispensam secrets. Leitura administrativa apenas se consultar configuração. Custo principal é tempo. |
| B. Dependabot | Alertas gerenciados e atualizações por configuração futura. Baixo custo operacional, triagem proporcional ao volume de PRs. Cobertura de ecossistemas suportados. | Alertas do GitHub e, quando ativados, PRs. Dependência dev/transitiva pode aparecer sem alcance. | Permissão de administração para habilitar. Sem secrets de registry público. Nenhum auto-merge. |
| C. Actions agendadas | Cadência mensal ou sob demanda. Restore e Docker build elevam tempo, cache e tráfego. Manutenção de versões/pins de ações. | Sumário e artefatos com retenção curta. Alertar sobre ausência de execução e erro de feed, deduplicando CVE/GHSA. | `contents: read` para consulta/checkout. Sem secrets em feeds públicos. Subida de SARIF/issue exigiria permissões adicionais e aceite separado. |
| D. Script local | Mensal sob comando do operador. Baixo consumo sem rebuild, manutenção de parse/feeds e versões da CLI. | JSON/SARIF em diretório descartável e uma ficha por achado. Erros distinguíveis do resultado limpo. | Leitura de repo, lock, feeds e evidências. Sem token se não consultar configurações privadas. |
| E. Scout recorrente | Mensal no artefato mais recente e após candidato de imagem. Aproveitar imagem existente. Build frequente é o maior consumo evitável. | SBOM/SARIF por digest. Risco de pacote-fonte, DB desatualizada e componentes estáticos omitidos. | CLI e engine local. Login/contrato depende de integração e operação escolhida. Sem push para complementar a evidência local. |
| F. Combinação | A + D, Scout mensal e revisão manual de fontes nativas. Acrescentar agendamento somente após medir cobertura e esforço. | Uma lista por ambiente com fontes agregadas. Notificação ao operador por mudança acionável e por falha da rotina. | Zero secrets novos na proposta inicial e uso dos recursos existentes. |

## 16. Custos e limitações

- GitHub Actions usa runners hospedados padrão sem cobrança de minutos em repositórios públicos. Runners de maior porte são faturáveis e armazenamento/artefatos obedecem às regras aplicáveis. Antes de adotar Actions, conferir retenção, uso, orçamento e runner escolhido. O agendamento pode atrasar e workflows agendados em repositórios públicos ficam sujeitos à desativação após 60 dias sem atividade. [Cobrança Actions](https://docs.github.com/en/billing/concepts/product-billing/github-actions), [schedule](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#schedule).
- Dependabot e funcionalidades GitHub de segurança para o repositório público têm disponibilidade documentada. Avaliar custos se a visibilidade ou plano mudar. A conta não foi inspecionada para limites de cobrança. [GitHub security features](https://docs.github.com/en/code-security/getting-started/github-security-features).
- Docker Scout Personal tem limite de até três repositórios para análise contínua, conforme documentação. O entitlement da conta ficou sem confirmação. A proposta usa auditoria local e evita inscrição de novos repositórios e publicação. Builds, downloads, cache e armazenamento têm custo de máquina e rede, mesmo sem assinatura adicional. [Docker Scout plans](https://docs.docker.com/scout/subscription/).
- Feeds públicos podem atrasar, divergir ou ficar indisponíveis. npm e NuGet consultados nesta etapa funcionaram. Versões reconciliadas com distribuição e upstream prevalecem na triagem de pacote. Rollouts Render e Vercel, planos e custos da conta ficaram sem verificação.

## 17. Cadência recomendada

- **Semanal, 15–20 min estimados.** Revisar advisories relevantes de .NET, Angular, Node, Skia/codecs e Ubuntu. Revisar Dependabot se habilitado futuramente. Conferir registros novos, mudanças de status e exploração conhecida.
- **Mensal, 45–75 min estimados fora de builds.** Rodar NuGet e npm, Scout em imagem final disponível, comparar pins MCR Linux/amd64, atualizar a ficha libpng e as pendências Noble, verificar data da última execução completa, certificados e mudanças de configuração. Medir o tempo nas duas primeiras rodadas e ajustar a cadência. Rodar build novo quando houver candidato ou quando faltarem artefatos atuais para decisão.
- **Correção crítica ou exploração conhecida.** Avaliar no mesmo dia, confirmar presença e caminho, definir mitigação e preparar mudança isolada com autorização.
- **Após deploy relevante.** Conferir SHA, versão, imagem quando visível, build/verify, health, início e logs. Executar smoke de cadastro/login, perfil, avatar, catálogo, biblioteca e HTTPS quando pertinente e autorizado. Registrar a janela observada.

## 18. Priorização de achados

| Categoria | Regra de decisão | Resposta proposta |
|---|---|---|
| Emergencial | Exploração ativa ou impacto crítico com presença e alcance plausível em entrada externa, autenticação, dados ou cadeia de build exposta. | Avaliar no mesmo dia, preparar mitigação imediata se patch indisponível e validar rollback. |
| Alta | Componente efetivamente entregue ou carregado, alcance externo possível e impacto relevante, ou build com acesso sensível. | Triagem em até dois dias úteis. Preparar experimento isolado com prioridade, considerando disponibilidade do fix e consequências operacionais. |
| Programada | Exposição limitada, alto atrito de atualização ou correção de backport/digest sem urgência comprovada. | Candidata para próxima revisão mensal. Separar famílias de mudança e medir regressão/compatibilidade. |
| Monitoramento | Registro rejeitado, caminho inaplicável documentado, alcance indeterminado com baixa exposição ou ausência de patch. | Guardar motivo, evidência, gatilho de reabertura e data de revisão. Elevar prioridade diante de nova exposição ou exploração conhecida. |

Usar CVSS e prioridade do fornecedor como entradas. Registrar evidência de versão, presença, alcance, exploração conhecida, privilégio necessário, usuário atingido, fix, complexidade, chance de regressão e impacto operacional. Consultar [CISA KEV](https://www.cisa.gov/known-exploited-vulnerabilities-catalog) quando pertinente. Ausência no catálogo não encerra a avaliação.

## 19. Fluxo de tratamento

1. Detectar e registrar fonte, timestamp, CVE/GHSA, ambiente e SHA/digest.
2. Confirmar pacote/revisão efetiva e variante. Reconciliar alerta, faixa vulnerável, backport e posição na árvore.
3. Verificar caminho de entrada, chamada/execução em build ou runtime, pré-condições, exploração conhecida e correção. Aplicar a matriz e decidir monitoramento ou experimento.
4. Com autorização, preparar experimento em ambiente isolado. Conservar baseline, alterar uma família por vez, medir inventário e resultados, testar reversão.
5. Executar gates aplicáveis: Linux/amd64 Restore/Release/verify, `ldd`, hashes e codecs, CoreChecks, registro, EF/Npgsql e PostgreSQL descartável quando pertinente. No frontend, lock, peers, build e smoke. Revisar diff e evidência antes de abrir PR.
6. Criar PR e integrar mediante aprovação humana. Validar deploy autorizado com SHA/versão, health, HTTPS, logs e smoke. Manter referência do último artefato estável.
7. Encerrar com evidência de correção ou manter aberto com motivo, responsável, data de revisão e condição para reabrir. Merge, deploy, rollback e mudanças remotas exigem decisão humana.

## 20. Arquitetura proposta

| Fonte e ferramenta | Frequência/execução | Saída e responsável |
|---|---|---|
| GHSA, OSV, avisos upstream e Dependabot se habilitado | Revisão semanal e por advisory crítico. Consultas e normalização automatizáveis. | Operador tria alertas e reconcilia aliases. |
| Manifestos e lock + NuGet/npm | Mensal, execução local sob demanda em ambiente isolado. | JSON por recorte, com SHA, fontes, versões, timestamp e resultado válido/falha. |
| Scout local, MCR e Ubuntu | Mensal e por candidato de base. Reusar imagem final identificada, buscar manifestos por leitura. | SBOM/SARIF e diferença de digests e achados. Operador confirma presença e fix Noble. |
| SkiaSharp, Skia e fornecedores de codecs | Mensal e por novo release, com análise de DEPS e bins quando necessário. | Tabela de proveniência e dívida libpng. Operador confirma alcance e validade do patch. |
| Render, Vercel, TLS e GitHub | Após deploy relevante e revisão mensal de configuração. | Ficha operacional com data, logs pertinentes e checks. Operador valida e autoriza ações. |

Usar chave canônica CVE ou GHSA conciliado + componente + contexto (build, browser, imagem ou configuração). Adicionar fontes ao mesmo achado e manter histórico de versões e digests. Enviar aviso apenas quando houver achado novo, alteração acionável de prioridade/patch, ou falha de cobertura. Em cada execução registrar sucesso, parcial, falha ou não executada por fonte e idade da última leitura válida. Na revisão mensal, conferir recibo de execução esperado. O operador responde pela fila e pela aceitação de aplicabilidade, mudança, PR, merge, deploy e rollback. Automatizar coleta e comparação depois da validação inicial.

## 21. Plano B06-E2

Proposta mínima, sujeita à autorização do operador: criar `tools/security/Invoke-SecurityAudit.ps1` para execução manual local e `docs/SECURITY-MONITORING-RUNBOOK.md` para cadência, registro e triagem. Nenhum arquivo existente precisaria ser modificado. Começar por npm audit total/produção, NuGet direto/transitivo sobre restore isolado e consulta aos manifestos MCR AMD64. Scout local opcional quando houver artefato/digest verificado, com falha explícita de cobertura se for solicitado e indisponível. Revisões de codecs, Render e GitHub ficam no checklist manual.

- **Permissões e dados.** Leitura de checkout/artefatos e acesso aos feeds públicos. Saída em diretório indicado pelo operador fora do checkout, com cache temporário separado. Sem secrets novos. Sem credencial com poder de escrita no GitHub, Render ou Vercel para a execução inicial. Validar acesso de leitura do Docker apenas para a opção Scout.
- **Falhas.** Diferenciar achado retornado, feed indisponível, ferramenta ausente e resultado malformado. Preservar resultados parciais com fonte e erro, não marcar parcial como limpo. Limitar tempo de consulta e permitir repetição da fonte que falhou.
- **Testes de aprovação.** Exercitar zero achados, advisory simulado, feed indisponível, manifesto sem AMD64, saída inválida e execução parcial com fixtures. Em execução real, conferir os mesmos SHA-256 antes/depois, presença de timestamp/SHA/digests no relatório, zero efeitos em Docker e nenhuma mudança em status Git além dos arquivos aprovados do E2. Aceitar só se dois usos demonstrarem leitura reproduzível, diagnóstico acionável e custo dentro da estimativa.
- **Custo e integração.** Medir tempo, tráfego e espaço em duas rodadas. Ficar atento à resolução NuGet sem lock, diferenças Windows/Linux, mudanças de schemas das CLIs e eventual perda de acesso ao feed. Sem implantação ou alteração do verify. O B06-E2 requer autorização antes de criar arquivos ou habilitar mecanismos.
- **Rollback.** Suspender a execução manual e reverter os dois arquivos do E2 pelo fluxo Git aprovado. Evidências externas podem seguir sua retenção normal. Sem efeito no build, publicação ou configuração de conta.

## 22. Riscos e incertezas

Imagem e bundle em produção não foram inventariados diretamente. O estado efetivo de Node na Vercel, persistência e proteção de chaves Render, consumidores de key ring em operação e plano da conta Docker Scout ficaram sem confirmação. A auditoria do grafo API usou resolução histórica copiada, com audit atual, e o Scout usa a fotografia D3. São necessárias novas evidências do artefato se houver mudança de digest, código, leitor, build ou configuração. Os manifests novos do MCR preservam versões declaradas de SDK/runtime. Seu diff de OS e a remoção efetiva de CVEs ainda não foram comprovados. Uso de GnuTLS pelo fluxo da API permanece indeterminado. O inventário de codecs estáticos ainda é parcial.

## 23. Preservação do repositório

Na conferência final, `main`, `HEAD`, `origin/main` e o SHA remoto permaneceram no SHA inicial. As 11 branches locais, as referências remotas e as duas worktrees permaneceram iguais. Os 248 SHA-256 preexistentes na principal e os 161 SHA-256, HEAD e status da manutenção coincidiram. O único arquivo não rastreado na principal é este relatório. Os IDs e referências de imagem, containers, volumes e totais do cache Docker coincidiram entre as leituras. A checagem de cache é agregada. Nenhum código de API/frontend, JWT/autenticação, migrations, manifestos, lockfiles, Dockerfile, SkiaSharp 4.155.2 ou target BuildHost foi alterado. Nenhuma configuração remota, secret, banco de produção, workflow ou automação foi alterada. Nenhum commit, push, PR, merge, migração ou deploy foi executado. Recursos Docker foram acessados por consultas de leitura. As consultas usaram caches e evidências temporárias isoladas. Na primeira execução, o SDK anunciou a instalação de certificado HTTPS de desenvolvimento. Não foi executado comando de confiança e a leitura posterior de `CurrentUser\\My` não encontrou certificado com o marcador ASP.NET Core pesquisado.

## 24. Conclusão

Iniciar com revisão semanal de avisos e auditoria mensal sob demanda, usando NuGet, npm, Scout e fontes oficiais de plataforma e codecs. A leitura atual identificou uma correção já presente na versão histórica de dpkg, um ID rejeitado e uma correção upstream de libpng ainda sem incorporação demonstrada no asset utilizado. Essas distinções devem orientar a fila antes de propor atualizações. O E2 pode começar com o coletor local e o runbook, após autorização do operador.
