# B05-B4.2 — Inventário da imagem, SBOM e auditoria

## 1. Escopo e contexto

Coleta diagnóstica de 17/11/2026 (America/Sao_Paulo), sem correções. Branch `security/b05-b4-container-review`, HEAD `2cd74b978c4700a545475f7e4fdb7aff0cb3a07f`. Estado inicial: somente `docs/B05-B4-CONTAINER-SECURITY-DIAGNOSIS.md` não rastreado, conforme esperado. Nenhuma alteração inesperada.

Foi possível inventariar uma imagem **histórica local**, exportar seus arquivos sem executar containers e gerar SBOM. A auditoria automatizada de CVEs permanece bloqueada por autenticação do Scout. A imagem não representa comprovadamente o HEAD atual ou o artefato do Render.

## 2. Estado do Docker daemon

Consultas no sandbox falharam por acesso negado aos pipes/configuração. Repetidas com autorização fora do sandbox, `docker version`, `docker info`, `docker context ls` e `docker image ls --no-trunc` funcionaram:

- Docker Desktop 4.97.0 (244584), cliente e Engine 29.9.2; Engine Linux amd64.
- Contexto ativo `desktop-linux`, endpoint `npipe:////./pipe/dockerDesktopLinuxEngine`. O contexto `default` aponta para `docker_engine`.
- Backend WSL2, kernel 6.18.33.1; seccomp builtin e cgroupns indicados no daemon. Isso não descreve o isolamento do Render.
- Seis imagens locais; dois containers parados, nenhum em execução na consulta.
- Docker Desktop instalado e processos Desktop/backend ativos. `com.docker.service` aparece Stopped/Manual, mas **o Engine responde**; o estado desse serviço isoladamente não justifica iniciá-lo.

Nenhum serviço ou contexto foi iniciado, parado ou alterado. Não é necessário pedir ao operador que inicie o daemon nesta coleta; o bloqueio restante é do scanner.

## 3. Ferramentas efetivamente utilizadas

Docker CLI 29.9.2, Scout 1.27.0, Buildx 0.38.1, tar nativo Windows, PowerShell e Node 24.16.0. Scout gerou inventário local sem login; `scout cves` exige autenticação. Nenhum login foi feito, nem senha/token solicitado. Trivy, Grype, Syft, readelf, llvm-readelf e objdump não foram encontrados no PATH; não houve instalação.

Node foi usado apenas como leitor binário de ELF64 little-endian para extrair DT_NEEDED; não é scanner de vulnerabilidades. Nenhuma biblioteca exportada foi carregada ou executada. Nenhuma API foi iniciada.

## 4. Imagem identificada e procedência

| Campo | Evidência local |
| --- | --- |
| Nome/tag | `seenit-api:render-check` |
| `.Id` retornado pelo Engine | `sha256:75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28` |
| RepoDigests | `seenit-api@sha256:75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28` |
| Criação | 09/11/2026 18:00:10 UTC, aproximadamente oito dias antes da coleta |
| Plataforma | linux/amd64 |
| Tamanho exibido | 378 MB |
| Origem documentada | RENDER.md já registra esse identificador como build local sem publicação |
| Procedência interna | Atestação BuildKit/SLSA v1 incluída no export, não validada criptograficamente |

A atestação declara source `https://github.com/GiovannaGrigo/SeenIt.git`, revisão `ba3daf5bd27dccd091af67478f5699923ce90975`, início 09/11/2026 17:49:49 UTC e fim 18:00:11 UTC. A declaração tem `resolvedDependencies=false` no indicador de completude e não prova ausência de alterações locais no build. Não se atribui a imagem ao HEAD atual.

O histórico não contém COPY da CA nem criação de `/app/certs`; a listagem de todas as camadas não contém esse diretório. O Dockerfile atual contém essas instruções. Portanto, é inadequado usar esta imagem como validação do Dockerfile atual. Não foi feito rebuild.

## 5. Digests, IDs e referências móveis

O armazenamento containerd expõe `.Id` acima como um índice OCI. O export permite distinguir os objetos, evitando chamar todo SHA de config digest:

| Objeto exportado do SeenIt | SHA-256 |
| --- | --- |
| Índice OCI / `.Id` observado | `75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28` |
| Manifesto linux/amd64 | `bf91e0917199b867f3d6926f83afd9de287394245f9583103277ffc2a1cb1035` |
| Configuração OCI | `0494d566f04a533f3fc3cbdf899d6957469953c17fff7761b039e350af8272f8` |

RepoDigest local não comprova publicação em registry. O manifesto e a config foram identificados nos blobs do `docker image save`, sem resolver o nome SeenIt em registry.

Consultas públicas MCR por `docker buildx imagetools inspect`, em 17/11/2026, sem pull de camadas:

| Tag móvel | Digest do índice atual | Manifesto linux/amd64 atual |
| --- | --- | --- |
| `mcr.microsoft.com/dotnet/aspnet:10.0-noble` | `sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e` | `sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5` |
| `mcr.microsoft.com/dotnet/sdk:10.0-noble` | `sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb` | `sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` |

A SDK já existe localmente: `.Id`/RepoDigest expostos pelo Engine correspondem ao índice `fe0fd4…`, linux/amd64, criação 10/11/2026 16:03:50 UTC. Não é evidência de que tenha construído o SeenIt histórico. A imagem ASP.NET não apareceu como tag na listagem local, embora suas camadas estejam incorporadas ao final histórico.

A atestação histórica declara bases resolvidas para linux/amd64: ASP.NET `sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c` e SDK `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`. São referências históricas declaradas, diferentes das tags atuais; não foram reconsultadas ou autenticadas. O Dockerfile continua sem digest fixado.

## 6. Versões efetivas de .NET e Ubuntu

| Artefato | .NET / ASP.NET | SDK | Ubuntu |
| --- | --- | --- | --- |
| SeenIt histórico local | Microsoft.NETCore.App e Microsoft.AspNetCore.App **10.0.14** | Ausente nos caminhos inventariados | `/usr/lib/os-release`: **24.04.5 LTS**, Noble, VERSION_ID 24.04 |
| SDK local `10.0-noble` | Env declara ambos 10.0.15 | Env declara **10.0.404** | Não extraído; tag/label Noble 24.04 |
| ASP.NET público atual amd64 | Config pública declara ambos **10.0.15**, criada 10/11/2026 16:01:27 UTC | Não aplicável | Label 24.04; filesystem não baixado |

Na imagem SeenIt, arquivos `.version`, diretórios shared e SBOM confirmam 10.0.14. O runtimeconfig da API solicita mínimo 10.0.0 dos dois frameworks; isso não contradiz o patch 10.0.14 disponível. Não foi executado `dotnet --info` dentro de container; versão instalada é comprovada offline, seleção efetiva de um processo não foi medida.

Exemplos de pacotes do SBOM do **SeenIt**, não das bases atuais:

| Pacote Ubuntu | Versão |
| --- | --- |
| libc6 | 2.39-0ubuntu8.9 |
| libgcc-s1 / libstdc++6 | 14.2.0-4ubuntu2~24.04.1 |
| libicu74 | 74.2-1ubuntu3.1 |
| libssl3t64 / openssl | 3.0.13-0ubuntu3.16 |
| ca-certificates | 20260816ubuntu0.24.04.2 |
| zlib1g | 1:1.3.dfsg-3.1ubuntu2.3 |
| tzdata | 2026e-0ubuntu0.24.04.1 |

O SBOM também reconhece OpenSSL genérico 3.0.13 por binário. Isso não deve apagar os sufixos Ubuntu nem as correções por backport da distribuição ao confrontar CVEs.

## 7. SBOM obtido e bloqueio de auditoria

Comando bem-sucedido, sem autenticação nova:

```text
docker scout sbom local://seenit-api:render-check --format spdx --output <TEMP>/seenit-b05-b42-sbom.spdx.json
```

Formato **SPDX-2.3**, criador `docker-scout-1.27.0`, timestamp **2026-11-17T13:11:29Z** (10:11:29 em São Paulo). Scout anunciou 176 pacotes indexados; o documento contém **236 registros em packages**, incluindo fontes/binários e reconhecimentos repetidos. Essas métricas não são contagens de componentes únicos nem de vulnerabilidades. SHA-256 do arquivo: `25FA7099325AA84498D5458C0AA418C0D5CF8939DF629B2D81E8B8AF8149F099`.

Há componentes deb Ubuntu, shared frameworks, NuGet e assemblies reconhecidos, inclusive BuildHost, Newtonsoft.Json e SkiaSharp. O inventário foi gerado a partir da imagem local, não fabricado a partir do csproj.

`docker scout cves local://seenit-api:render-check --platform linux/amd64 --format sarif --output <TEMP>/seenit-b05-b42/cves.sarif.json` foi bloqueado por exigência de login. O arquivo SARIF não foi criado. Uma tentativa inicial usou formato `json`, não suportado por essa versão, e foi corrigida para `sarif`; a tentativa válida também ficou bloqueada. Não existe resultado de scanner para contabilizar. Não foi executado login nem usado registro de credenciais como evidência de autorização.

Alternativas locais: Trivy/Grype/Syft ausentes no PATH; tar, SBOM e leitor ELF permitem inventário, mas não substituem banco de CVEs. É necessário o operador autenticar Scout pelo fluxo oficial e autorizar a consulta, ou disponibilizar scanner aprovado com base pública atualizada. Não enviar senha/token pelo chat.

## 8. Vulnerabilidades identificadas

Confronto manual com fonte oficial, consultada em 17/11/2026:

| Advisory | Pacote instalado / origem | Corrigido | Presença e aplicabilidade | Mitigação possível |
| --- | --- | --- | --- | --- |
| [CVE-2026-109475](https://github.com/dotnet/announcements/issues/500), High, CVSS 7.5 | Microsoft.NETCore.App.Runtime.linux-x64 10.0.14, camada shared framework da base | 10.0.15 | Versão instalada está na faixa afetada; falha de stack exhaustion em HttpListener Unix com linha HTTP incompleta. API revisada usa Kestrel; uso de HttpListener não identificado no código, e exploração no SeenIt não demonstrada. | Futuramente renovar base para patch corrigido e validar; confirmar alcance do componente. Nenhuma correção aplicada. |
| [CVE-2026-114155](https://github.com/dotnet/announcements/issues/502), High, CVSS 7.5 | SDK dotnet-watch; SDK atual local 10.0.404 | 10.0.404 | SDK atual fora das faixas 10.0.300–303 e 10.0.400–403. SDK histórico de build não medido; ausente no runtime final inventariado. Requer atacante local alterando solução temporária usada pelo watch; Dockerfile não usa watch. | Manter SDK corrigido na futura construção; não atribuir ao runtime final. |

A primeira linha é **um componente instalado em versão afetada por advisory**, não uma exploração confirmada nem resultado do Scout. A segunda é triagem de exclusão para o SDK atual, não um segundo achado instalado no runtime. Não houve auditoria exaustiva de todos os advisories .NET, Ubuntu ou NuGet do artefato.

## 9. Severidade e aplicabilidade

| Severidade | Triagem manual desta coleta | Auditoria automatizada completa |
| --- | --- | --- |
| Critical | Nenhum achado confirmado nesta triagem limitada | Indeterminada |
| High | 1 correspondência instalada: CVE-2026-109475; exploração não demonstrada | Indeterminada |
| Medium | Nenhum achado confirmado nesta triagem limitada | Indeterminada |
| Low | Nenhum achado confirmado nesta triagem limitada | Indeterminada |
| Unknown | Sem achado específico classificado | Indeterminada |

Não ler as demais linhas como zero vulnerabilidades. A auditoria NuGet do B05-B4.1 refere-se ao grafo local atual e não substitui o scan desta imagem histórica. As versões das bases públicas atuais também não aprovam o artefato final nem corrigem uma imagem já construída.

## 10. SkiaSharp e bibliotecas nativas

SBOM e `.deps.json` da API histórica identificam SkiaSharp e NativeAssets.Linux.NoDependencies **4.155.0**. `/app/libSkiaSharp.so` está presente: **12.287.632 bytes**, SHA-256 `26A5CF2CFB4529CF00CAABC7E9F381FF59641C36690A6E658F8E3E4FA9AC850E`.

DT_NEEDED extraído offline do ELF: `libstdc++.so.6`, `libpthread.so.0`, `libdl.so.2`, `libm.so.6`, `libc.so.6`, `ld-linux-x86-64.so.2`, `librt.so.1`. São dependências diretas declaradas, não uma execução de ldd nem prova de resolução transitiva em processo. O inventário contém as bibliotecas glibc/C++ correspondentes; não se executou o carregador para validá-las. Não há dependência direta de fontconfig na lista.

O Scout reconheceu o pacote SkiaSharp, mas não forneceu um inventário suficiente das revisões dos codecs embutidos. Bibliotecas compiladas estaticamente podem não ser individualizadas no SBOM. Não foram atribuídas versões de libjpeg/libpng/libwebp ao binário por suposição, nem CVEs genéricas Chromium/Skia.

A [página oficial de segurança SkiaSharp](https://github.com/mono/SkiaSharp/security) não lista advisories publicados na consulta; isso não certifica os codecs. O caminho de avatares recebe bytes não confiáveis e cria SKCodec antes de conferir dimensões/formato real. Os limites atuais de tamanho/pixels/concorrência mitigam recursos, mas não eliminam bugs de parser. Não houve alteração ou teste ofensivo de avatares.

## 11. Conteúdo dispensável e isolamento

Listagem de todas as dez camadas e `.deps.json` extraído, sem execução:

- **Confirmados no final:** `/app/BuildHost-net472` e `/app/BuildHost-netcore`, Microsoft.Build.Locator 1.6.10, Roslyn Workspaces.MSBuild.BuildHost 4.14 e cópias auxiliares de Newtonsoft.Json/System.CommandLine. São oportunidades concretas de redução, dependentes de verificar a origem no publish e ausência de uso funcional.
- **Não encontrados:** `Microsoft.EntityFrameworkCore.Design.dll` e `Microsoft.Build.dll` na raiz publicada; o receio do B05-B4.1 não autoriza afirmar que essas DLLs estejam nesta imagem. Há Build Locator/BuildHost, o que é uma constatação diferente.
- **Não encontrados nos nomes de arquivos de todas as camadas:** instalação `/sdk/`, SQLite, assemblies SeenIt.Checks/AvatarChecks/RegisterIntegrationChecks e fontes `.cs`. O arquivo `.container-checks-passed` é apenas marcador de build. Ausência por nomes não exclui código incorporado/renomeado.
- API histórica `.deps.json` lista EF Core/Npgsql 10.0.0, IdentityModel 8.0.1, Microsoft.OpenApi 2.7.5, SkiaSharp 4.155.0, entre outros; não lista o projeto de integração nem SQLite.

Isso valida características do artefato histórico. O publish do HEAD atual, especialmente após ajustes de dependências, ainda deve ser inventariado separadamente. Nenhum arquivo foi removido.

## 12. Avaliação da configuração de segurança

| Verificação | Evidência e limite |
| --- | --- |
| Usuário | Config da imagem `app`; `/etc/passwd`: UID/GID 1654. Usuário de processo efetivo não medido, pois nenhum container foi iniciado. |
| Entrypoint/Cmd | Exec `dotnet SeenIt.Api.dll`, Cmd vazio, Workdir `/app`. |
| Portas | Metadado `10000/tcp`, sem publicação de porta realizada. |
| `/app` | Tar mostra root:root 0755; libSkiaSharp root:root 0744, não gravável por app. |
| Diretórios graváveis | `/home/app` app:app 0750, `.aspnet` e DataProtection-Keys app:app 0755. Não é inventário exaustivo de todos os caminhos graváveis da base. |
| Certificado específico | `/app/certs/prod-ca-2021.crt` ausente nesta imagem histórica; não é possível validar ali o modo 0444 previsto no Dockerfile atual. CA store do SO aparece no SBOM. |
| Build/runtime | SDK e testes não encontrados no final; BuildHosts auxiliares continuam presentes. |
| Health | Imagem sem Healthcheck; contrato Render documenta `/health`, sem inspeção real do serviço. |
| Privilégios | Nenhum indício de USER root na configuração final; imagem não determina opções Privileged/capabilities/mounts de um futuro container. Não foram inspecionados containers de terceiros ou produção. |
| Ambiente | Somente nomes foram listados; valores de negócio/segredos não foram despejados. Env declara versões e defaults de Production/porta; nenhum início com configuração real. |

Não houve varredura exaustiva de segredos de todas as camadas. Histórico revisado não expôs ARG secreto; a configuração atual e .dockerignore permanecem as descritas no B05-B4.1. Render, Supabase, banco e outros serviços da aplicação não foram acessados.

## 13. Limitações

O impedimento é a consulta de CVEs autenticada, não o daemon ou a geração local de SBOM. Não há scan completo de SO/.NET/nativos, data de atualização da base Scout ou total de CVEs por severidade. O inventário gerado tem limitações de reconhecimento estático e registros duplicados.

Nenhum pull, rebuild, container novo ou execução de API foi realizado. Atestação de procedência lida não é assinatura validada. O estado publicado no Render é desconhecido, inclusive digest, patch, permissões, CA, variáveis e topologia. Metadados das tags MCR atuais não substituem o inventário do SeenIt histórico. Não houve acesso ao worktree maintenance-phase1 para analisar seus arquivos; sua referência e associação ao worktree foram preservadas.

## 14. Evidências coletadas e preservação

Arquivos brutos somente fora do repositório, em `%TEMP%` (temporários locais, sujeitos a descarte pelo sistema):

| Caminho relativo a `%TEMP%` | Conteúdo |
| --- | --- |
| `seenit-b05-b42-sbom.spdx.json` | SBOM SPDX real e hash registrado acima |
| `seenit-b05-b42-image.tar` | Export da imagem local, sem execução |
| `seenit-b05-b42/` | Blobs OCI, manifest/index, listas `layer-1-files.txt` a `layer-10-files.txt` |
| `seenit-b05-b42/SeenIt.Api.deps.json` | Grafo publicado histórico extraído |
| `seenit-b05-b42/app/libSkiaSharp.so` | Binário nativo para leitura offline |
| `seenit-b05-b42/read-elf.cjs` | Leitor mínimo DT_NEEDED |
| `seenit-b05-b42/image-inspect.json`, `history.jsonl` | Metadados e histórico brutos |
| `seenit-b05-b42/aspnet-manifests.txt`, `sdk-manifests.txt` | Resolução pública atual das tags |
| `seenit-b05-b42/scout-cves-attempt.txt` | Exigência de autenticação; não é relatório CVE |

Consultas públicas realizadas em 17/11/2026: manifestos/config MCR e advisories Microsoft/SkiaSharp vinculados nas seções anteriores. Sem download ou execução de ferramentas novas.

SHA-256 do B05-B4.1 antes da coleta: `D7AB032F7A57B3DDAE96902D3DD9DC6EA29F5542596A5DB8AB230521D9A620BB`; verificado novamente ao final. Branch maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`; branches e worktrees não alterados. Nenhum arquivo rastreado modificado, nenhuma alteração funcional/configuração/dependência/migration/Dockerfile, nenhum commit/push/PR/deploy.

## 15. Riscos e prioridades

1. **P1 — Completar auditoria:** operador deve autenticar Scout por fluxo oficial e disponibilizar a sessão autorizada, ou scanner aprovado equivalente. Reexecutar consulta do artefato imutável e registrar versão/data da base, pacotes, camadas, correções e condições.
2. **P1 — Renovação orientada por evidência:** runtime histórico 10.0.14 está na faixa afetada de um advisory High; base pública atual declara 10.0.15. Planejar correção futura após completar a triagem, sem assumir exploração de HttpListener no SeenIt.
3. **P1 — Rastreabilidade:** não usar imagem histórica como aprovação do HEAD ou produção. Definir artefato de referência de uma etapa futura e comparar com o inventário atual.
4. **P2 — Nativos e superfície:** mapear revisão de codecs no libSkiaSharp; avaliar remoção dos BuildHosts comprovadamente presentes com testes de regressão.
5. **P2 — Digests e atualização:** fixação controlada e renovação de bases, incluindo SDK, com evidências e testes. Não confundir fixação com atualização automática.

## 16. Recomendações para B05-B4.3

Começar pela conclusão da auditoria, sem implementação enquanto o bloqueio impedir uma visão completa: autenticação feita pelo operador, scan do digest local preservado, classificação/triagem por componente e coleta de eventual base histórica de build. Preservar o SBOM e comparar os resultados com os advisories oficiais e backports Ubuntu.

Depois, em etapa explicitamente autorizada, preparar experimento isolado do HEAD atual com bases corrigidas, inventário antes/depois e validação dos checks, `/health`, usuário/permissões/CA e decodificação de avatares com configurações sintéticas. Verificar também que BuildHost pode ser excluído sem regressão. Não ampliar esta coleta para deploy ou acesso a banco.

**B05-B4.2 BLOQUEADO POR AMBIENTE** — inventário e SBOM obtidos; auditoria automatizada completa impedida pela autenticação do Scout. Nenhuma implementação iniciada.
