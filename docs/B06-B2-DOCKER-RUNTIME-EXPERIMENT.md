# B06-B2 — Experimento isolado de atualização da imagem ASP.NET

Data: **19/11/2026**, America/Sao_Paulo. Projeto SeenIt. Código de referência: `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`.

**Decisão: candidata aprovada localmente**, dentro da cobertura e limitações descritas. As imagens finais construídas neste experimento apresentaram **24 → 17 alertas Scout**, sem CVEs novos na candidata. Nenhuma atualização permanente foi aplicada.

## 1. Objetivo

Verificar a renovação exclusiva do digest ASP.NET Runtime Noble, mantendo código, contexto, SDK, dependências e configurações iguais entre controle e candidata. Exercitar as diferenças de glibc/Perl com checks nativos, integração HTTP, JWT, HTTPS B06-A e smoke real das duas imagens Linux/amd64.

Foram lidos os relatórios [experimento B05-B4](B05-B4-CONTAINER-BASE-EXPERIMENT.md), [fechamento B05-B4](B05-B4-FINAL-SUMMARY.md), [fechamento B06-A](B06-A-FINAL-SUMMARY.md) e [inventário B06-B1](B06-B1-DOCKER-VULNERABILITY-INVENTORY.md). Os resultados abaixo são novos; não são reapresentação dos testes históricos nem scan de produção.

## 2. Estado inicial do Git

`main`, HEAD e referência local `origin/main` confirmados em `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`. `git status --short --branch` mostrou exclusivamente `?? docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md`. Nenhuma alteração inesperada; nenhum fetch, atualização de branch ou checkout foi necessário.

Worktrees existentes:

| Caminho | Branch | HEAD |
| --- | --- | --- |
| `C:/Users/giovana.grigolato/Downloads/seenit` | main | `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91` |
| `C:/Users/giovana.grigolato/Downloads/seenit/.worktrees/maintenance-phase1` | maintenance-phase1 | `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290` |

O snapshot inicial registrou status, heads, branches e SHA-256 de **237 arquivos da árvore principal** e **161 arquivos de maintenance-phase1**, obtidos da listagem Git de rastreados e não rastreados não ignorados. Inclui o B06-B1 e os arquivos pendentes de maintenance. Não inclui caches/binários ignorados pelo Git. A lista completa ficou em `repository-before.json`, fora do repositório.

Pendências preexistentes de maintenance preservadas: seis arquivos rastreados em AppDbContext, Program, AvatarProfileService, IAvatarReferenceStore, RateLimitingRegressionChecks e RegistrationFactory; diretórios não rastreados Maintenance, MaintenanceAdmin, MaintenanceChecks, operations e relatórios MAINTENANCE-PHASE1/RENDER-FREE-OPERATIONS, conforme também registrado no B06-B1. Nenhum arquivo desse worktree foi editado.

Hashes iniciais relevantes, SHA-256 dos bytes da árvore de trabalho:

| Arquivo | Hash |
| --- | --- |
| `backend/SeenIt.Api/Dockerfile` | `525a125804cde3159934026aef55ebcb01ecd1495d5d52254705cb49b5fdfbbc` |
| `backend/SeenIt.Api/Program.cs` | `b2b73ed98b7953b061ccb15cc4207bc5588628c015d073a01ef089fdeeac5812` |
| `backend/SeenIt.Api/SeenIt.Api.csproj` | `79040af396b449b56c780853c5feb435a5a4f07bbcb4acdbc5c1e2f48f7ca382` |
| `backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj` | `23c6e45d1225f8eaf8aee4b6ed0ab21b5867b980a420a7915c8ab9db61d5a194` |
| `backend/SeenIt.Checks/SeenIt.Checks.csproj` | `dfe06df79f0d828bdc77afaa9e5c1946d778658b78635211eebc693fc1993915` |
| `backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj` | `4603b6daeb5c816c563cb2c1b27b5cdb468eea897553e0572d0b97efe34dfe9a` |
| `docs/B05-B4-CONTAINER-BASE-EXPERIMENT.md` | `da2b0287534a4bdddc6a17672fe297f2176faf696a5b24205b47f006ba52d55b` |
| `docs/B05-B4-FINAL-SUMMARY.md` | `169ce5bb9a3b3dffa1e39db63796978065e2f42b9f73c0d7e533461a79aa5b6f` |
| `docs/B06-A-FINAL-SUMMARY.md` | `d759c9fe2f65f1586e0ad490dca2da653f4229078b1a5508eb7728f2b0e17c73` |
| `docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md` | `6de9e327eae8577dcc5dd0a5781a9fa9ffbcd9288a2fb76b325576ae0aa3c57c` |

## 3. Digests utilizados

| Papel | Referência explícita |
| --- | --- |
| SDK, igual em ambos | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` |
| ASP.NET controle | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5` |
| ASP.NET candidata | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde` |

`docker buildx imagetools inspect` confirmou ambos os manifestos ASP.NET por digest. Pull explícito `--platform linux/amd64`, inspeção do Engine e execução isolada `--network none` confirmaram linux/amd64, Ubuntu 24.04.5 LTS/Noble e frameworks Microsoft.NETCore.App/Microsoft.AspNetCore.App **10.0.15** em ambos. Na candidata, dpkg confirmou libc6 **2.39-0ubuntu8.12**, perl-base **5.38.2-3.2ubuntu0.7** e libicu74 **74.2-1ubuntu3.1**, antes do build.

Esses dois digests ASP.NET são **manifestos de plataforma**, não índices multiplataforma. O SDK fixado permaneceu 10.0.404, confirmado em container de compilação pela saída de `dotnet --info`. Nenhuma tag flutuante foi usada para substituir uma base especificada. O frontend Dockerfile já existente `docker/dockerfile:1` resolveu o mesmo digest `sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e` nos dois builds; não foi alterado.

## 4. Metodologia

Área exclusiva **fora do repositório**:

`%TEMP%/seenit-b06-b2-20261119-6d3a810e`

O caminho expandido observado foi `C:/Users/GIOVAN~1.GRI/AppData/Local/Temp/seenit-b06-b2-20261119-6d3a810e`. Temporários estão sujeitos à retenção do sistema.

Foram lidos os blobs do HEAD com Git, sem usar arquivos modificados de maintenance. Prepararam-se 74 arquivos de origem: C#/csproj dos projetos necessários, `.dockerignore`, configuração runtime sanitizada e CA pública. Os projetos da API, AvatarChecks e CoreChecks compartilham **um único contexto imutável**; RegisterIntegrationChecks foi colocado em árvore separada para testes. Arquivos excluídos pelo contexto original, como appsettings locais, launchSettings, scripts SQL, ferramentas e documentação, não foram copiados para o contexto experimental. Não há outros arquivos necessários ao build omitidos da allowlist versionada.

Cada Dockerfile fica fora desse contexto compartilhado. Fontes e arquivos de configuração são os mesmos nos dois builds; somente o digest ASP.NET difere. `source-hashes.json` registra SHA-256 dos blobs copiados. Não foram copiados User Secrets, credenciais reais ou configuração de produção.

Comando de build, repetido para control/candidate com arquivos e tags correspondentes:

```text
docker build --platform linux/amd64 --no-cache --progress plain \
  --file <EXP>/Dockerfile.control \
  --tag seenit-api:b06-b2-control-6d3a810e <EXP>/context
```

Ambos usaram `--no-cache`. Os logs comprovam execução efetiva de restore, publish, auditoria NuGet, RUN de verify e validação de CA, em vez de atribuir aprovação a cache. Os únicos CACHED visíveis referem-se a frontend/metadados/WORKDIR, não ao RUN obrigatório de checks.

Ferramentas: Docker CLI/Engine 29.9.2, Desktop 4.97.0, contexto desktop-linux; Scout **v1.27.0**, commit `1be68946eb316438e64568a2a8f1d4858ba9ba21`; Node.js do host apenas para preparação, comparação e hashes. Não houve teste funcional .NET executado no Windows: compilações e testes funcionais foram **Linux/amd64**.

As suítes suplementares foram compiladas com o mesmo SDK fixado, em container separado, a partir das fontes copiadas do HEAD. Seus artefatos foram montados temporariamente nas **imagens finais** para execução não-root e sem rede. Os arquivos da publicação final da respectiva imagem foram sobrepostos aos diretórios temporários dos harnesses, garantindo uso do `SeenIt.Api.dll`, Skia e dependências publicados no experimento. Nenhuma dessas montagens entrou na imagem final. SQLite existe somente nos harnesses, com banco em memória; não se executaram migrations.

## 5. Diferença exata entre Dockerfiles experimentais

Controle foi obtido do Dockerfile versionado no HEAD. Os bytes LF do blob diferem dos CRLF da árvore de trabalho; após normalizar somente quebras de linha, controle e arquivo original são idênticos. O arquivo original não foi escrito.

```diff
-FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5 AS runtime-base
+FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde AS runtime-base
```

Uma única ocorrência foi substituída. A substituição inversa reconstrói integralmente o controle, verificada antes dos builds.

| Dockerfile temporário | SHA-256 |
| --- | --- |
| `Dockerfile.control` | `7484d2ed5f8f9954ac91304a3da5422ad750e155f299cebd505f4f0c14a4de4c` |
| `Dockerfile.candidate` | `15456a50c473d54a969fef98367b59adc3641a5e1e82beb3a7bd10dc0b937d65` |

## 6. Resultados dos builds

| Verificação realmente executada | Controle | Candidata |
| --- | --- | --- |
| Build completo, exit | 0, aprovado | 0, aprovado |
| Duração observada do comando | 43,89 s | 79,13 s |
| Restore API/AvatarChecks/CoreChecks | Aprovado | Aprovado |
| Publish Release linux-x64 | Aprovado | Aprovado |
| Auditoria NuGet dos três projetos | Nenhum pacote vulnerável informado nas fontes consultadas | Igual |
| Comparação dos binários Skia API/checks | Aprovada | Aprovada |
| ldd e ausência de not found | Aprovados | Aprovados |
| AvatarChecks | 16 grupos aprovados | 16 grupos aprovados |
| CoreChecks | Watch time, classificação/cache/retry e 72 casos HTTP de cadastro aprovados | Igual |
| Marcador obrigatório de verify | Criado e copiado | Criado e copiado |
| CA pública, leitura como app e modo 0444 | Aprovados | Aprovados |

Não foram identificados warnings de compilação ou erros nos builds completos. Não houve atualização de referências NuGet nem SDK. Durações incluem diferenças de trabalho/extração/contenção local; **não são benchmark de desempenho** da aplicação. A compilação suplementar também terminou com exit 0 e auditoria NuGet habilitada.

## 7. Identidade das imagens

| Campo | Controle | Candidata |
| --- | --- | --- |
| Tag exclusiva | `seenit-api:b06-b2-control-6d3a810e` | `seenit-api:b06-b2-candidate-6d3a810e` |
| ID devolvido pelo Engine / índice final | `sha256:21337739b8e721425c675a745f1a24ca25f62a0f4b4953a73557c1e9114f4f6f` | `sha256:855583c75c1698ff3d29f4c335bdaec2dfcddbcd9c5e12a0a103cbd2d439cfde` |
| Manifesto final de plataforma no log BuildKit | `sha256:c8d5dc46a35817b4ae19568b9c485ebf5c413d3db2c0dac2dff85d0e2ca6abe1` | `sha256:e2aab6c223416725c138360f4377394fd8f2b493fbe028c3179c6a6419cc9a5a` |
| Config digest no log BuildKit | `sha256:642a148d6ebcfad37b567e6b048c924d97e242e88546ae825fb1e9c726cf4547` | `sha256:84115a9f23e08c1e65ead8f0002cc2aa887cf5aa5429ebd39de6a1342eb55293` |
| Criação UTC | 19/11/2026 17:23:21 | 19/11/2026 17:24:39 |
| Criação São Paulo | 19/11/2026 14:23:21 | 19/11/2026 14:24:39 |
| Plataforma | linux/amd64 | linux/amd64 |
| .NET / ASP.NET medidos | 10.0.15 / 10.0.15 | 10.0.15 / 10.0.15 |
| Usuário / UID | app / 1654 | app / 1654 |
| Entry point | dotnet SeenIt.Api.dll | Igual |
| Porta declarada e exercitada | 10000/tcp | 10000/tcp |

Os índices finais incluem atestação BuildKit; os diferentes tipos de digest acima não são intercambiáveis. Imagens foram construídas e mantidas **somente localmente**, sem publicação. A atestação não foi validada como assinatura de origem.

## 8. Comparação de pacotes e conteúdo

| Componente | Controle | Candidata |
| --- | --- | --- |
| Ubuntu | 24.04.5 LTS / Noble | Igual |
| libc6 e libc-bin | 2.39-0ubuntu8.9 | **2.39-0ubuntu8.12** |
| perl-base | 5.38.2-3.2ubuntu0.6 | **5.38.2-3.2ubuntu0.7** |
| libicu74 | 74.2-1ubuntu3.1 | Igual |
| libstdc++6 | 14.2.0-4ubuntu2~24.04.1 | Igual |
| libssl3t64 | 3.0.13-0ubuntu3.16 | Igual |
| SkiaSharp / NativeAssets | 4.155.0 | Igual |
| SDK na imagem final | Ausente | Ausente |
| `/checks` e executáveis AvatarChecks/CoreChecks | Ausentes | Ausentes |
| SQLite de testes na imagem final | Não identificado no inventário/conteúdo verificado | Igual |

A comparação integral das listagens dpkg detectou **somente três pacotes binários com versão diferente**: libc6, libc-bin e perl-base. SBOM inclui também os registros dos pacotes-fonte glibc e perl; são cinco registros purl com versão alterada, sem contar a identidade OCI da própria imagem. Os 236 registros SPDX de cada imagem não são 236 pacotes distintos indexados pelo scanner.

Foram comparados **70 arquivos regulares em `/app`**, exportados de cada imagem: mesmos caminhos e SHA-256, **zero diferenças**. Isso inclui publicação, CA, marcador e configuração sanitizada. A API é o mesmo binário nas duas imagens, gerado do código do HEAD, não apenas a mesma versão nominal.

| Arquivo em ambas | SHA-256 |
| --- | --- |
| `/app/SeenIt.Api.dll` | `6b5502da068e98d7a1059fdbf77e13a896252d63db8092fd4512a862c6c5e9b6` |
| `/app/libSkiaSharp.so` | `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e` |

BuildHost-net472/netcore e auxiliares continuam presentes como na publicação atual; não se afirmou que todas as ferramentas de design estão ausentes. Não foram removidos ou corrigidos nesta etapa. Ausência do SDK completo não equivale à ausência desses auxiliares.

As permissões efetivas do smoke confirmaram `/app` e `/app/certs` root:root 0755, sem escrita por app; CA pública de 1367 bytes, root:root 0444, legível e não gravável; diretório DataProtection-Keys app:app, gravável pelo usuário. `HostConfig` observado em ambos: `Privileged=false`, `CapAdd=null`, `PortBindings={}`. Nenhuma capability/permissão foi ampliada.

## 9. Testes nativos

Os checks do Dockerfile rodaram no estágio verify de cada base. Suplementos rodaram nas duas **imagens finais**, como app, com rede none e binários publicados exatos montados no harness.

| Cobertura | Controle | Candidata |
| --- | --- | --- |
| JPEG/PNG/WebP reais, detecção efetiva | Passou | Passou |
| Resize de 1600×800 para 1024×512, reencode WebP e descarte de payload anexado | Passou | Passou |
| Vazio, >5 MiB, MIME falso, formato não permitido, corrompido/truncado | Rejeições esperadas | Iguais |
| Lado >8192 pixels | Rejeitado | Rejeitado |
| 4001×4000, acima de 16 milhões de pixels | Rejeitado por limite de pixels | Igual |
| Fixture WebP sintética validada pelo SKCodec com dois frames | Rejeitada especificamente por animação | Igual |
| Semáforo de dois decoders: ocupação, espera e cancelamento | Passou | Passou |
| Devolução de permissões e nova conversão após cancelamento/rejeição | Passou, capacidade restaurada a 2 | Igual |
| `ldd /app/libSkiaSharp.so` | Sem not found | Sem not found |

Resolução nativa incluiu libc, libstdc++, libm, libpthread, libdl, librt, libgcc e loader amd64. A glibc nova foi exercitada pelo processamento real de imagens. O teste do semáforo usa reflexão somente no harness temporário, sem alterar a API. Não mede pico real de carga/memória nem prova ausência de todo vazamento nativo. Nenhuma mudança em SkiaSharp ou codecs.

## 10. Testes funcionais

Execuções finais, todas com exit **0 em ambas as imagens**:

| Suíte/cobertura | Resultado por imagem |
| --- | --- |
| RegisterIntegrationChecks — cadastro HTTP real via TestServer | **73 casos**, 16 usuários somente em SQLite memória; rejeições sem comandos/saves/hashes |
| Resiliência de catálogo | Single-flight/cache limitados, cancelamento independente, recuperação, elenco parcial, recurso removido/renomeado; 404/429/500/timeout/conteúdo inválido |
| Integridade B03 | Limites, Unicode/NUL, binding, IDs, associações/votos, atualizações e biblioteca; fake HTTP e ausência de escrita em rejeições |
| Rate limiting | Budgets, independência, JWT, headers falsificados, efeitos colaterais, JSON, retry, health e concorrência/liberação |
| Regressões permanentes | 20 logins sobrepostos, contabilização global, isolamento de janelas, perfil simultâneo, cancelamento/erros e CORS |
| AvatarChecks no verify | URLs e caminhos oficiais Storage, falhas sanitizadas, upload/delete simulados, substituição/compensação, conflitos concorrentes, remoção, cancelamento e contratos |
| Harness EntryPointChecks suplementar | **174/174 asserções**, seis perfis e 72 casos de request; detalhamento na seção 12 |
| Harness ExtraAvatar | Três grupos suplementares aprovados, seção 9 |

O entry point real da API é exercitado via RegistrationFactory existente. O harness suplementar reutiliza o teste B06-A7, com caminhos Linux e um caso adicional de **JWT expirado corretamente assinado**, rejeitado com 401 nos seis perfis. Tokens válidos acessaram perfil; malformados/ausentes permaneceram 401 nos cenários sem redirect; login/cadastro também foram exercitados pelas suítes existentes. Não se alterou JWT nem configuração funcional da aplicação.

Os doubles proíbem HTTP externo e substituem banco por SQLite em memória. A execução `--postgres-only` não foi acionada. Não há validação de PostgreSQL real ou Supabase real; não se declara cobertura de integrações externas além dos cenários simulados existentes.

### Preparação dos harnesses e tentativas não aprovadas

O primeiro smoke chegou ao health, mas sua nova asserção de HostConfig interpretou objeto vazio de portas como verdadeiro. A primeira reconstrução do script temporário gerou erro de sintaxe, detectado antes de executar Docker. O script foi regenerado, validado pelo parser PowerShell e os smokes completos repetidos, aprovados em ambos. Nenhuma mudança na aplicação ou imagem foi necessária.

A primeira execução suplementar de integração parou no **controle**, antes da cobertura HTTP, por `DllNotFoundException` de e_sqlite3: o diretório de harness publicado não continha a biblioteca nativa SQLite. O diagnóstico confirmou que os assets Linux do pacote já fixado **SQLitePCLRaw.lib.e_sqlite3 2.1.12** existiam. Recuperou-se **essa mesma versão** por restore do csproj copiado e montou-se o arquivo apenas nos diretórios temporários de RegisterIntegrationChecks/EntryPointChecks de ambos os lados. Hash do `libe_sqlite3.so` utilizado: `b0f5c48026fedcf9f05cf31c6fadf9e1e001ca59cd85aa68b8b6814e52c18a07`.

Não houve upgrade, mudança de csproj ou instalação de SQLite nas imagens finais. O erro inicial está preservado em `control-SeenIt.RegisterIntegrationChecks.log`; o diagnóstico em `sqlite-harness-diagnostic.log`; as execuções válidas usam sufixo `-assembled.log`/`.exit`. Não se contou a tentativa incompleta como teste aprovado ou regressão introduzida pela candidata. Não se atribui causa definitiva ao mecanismo de publish além da ausência observada na montagem do harness.

## 11. Smoke da API

Para cada imagem, container novo com entry point original, rede **none**, sem publicação de portas e somente configurações sintéticas. JWT aleatório de teste, conexão local inacessível e chave Storage fictícia, sem credenciais reais. Requisição TCP interna por bash a `127.0.0.1:10000`, sem expor serviço ao host ou publicamente.

| Verificação | Controle | Candidata |
| --- | --- | --- |
| Ambiente | Production | Production |
| Binding medido | `http://0.0.0.0:10000` | Igual |
| GET /health | HTTP 200, `{"status":"ok"}` | Igual |
| Usuário efetivo | app, UID/GID 1654 | Igual |
| Sem privilégio adicional/port binding | Confirmado por inspect | Igual |
| SDK, checks e nativos | Verificações da seção 8 aprovadas | Iguais |
| Valores sintéticos sensíveis nos logs | Não encontrados entre os valores testados | Igual |
| Encerramento | docker stop, exit 0, sem OOM, shutdown registrado | Igual |

Warnings comuns preservados: Data Protection em diretório sem persistência garantida; ausência de encryptor XML; precedência de URLS sobre HTTP_PORTS. Não são novos efeitos observados da candidata nem foram corrigidos. O teste de logs é direcionado aos valores fornecidos, não auditoria exaustiva de segredos. Health comprova inicialização e contrato básico, **não banco ou Storage**.

Somente containers novos desta atividade foram removidos após seus testes. Imagens experimentais e históricas foram preservadas.

## 12. Validação da configuração HTTPS

EntryPointChecks rodou o Program.cs real por WebApplicationFactory dentro das duas imagens finais. Perfis sintéticos:

| Perfil | Opção | Porta HTTPS sintética | Resultado em ambos |
| --- | --- | --- | --- |
| absent | Ausente | Ausente | Comportamento anterior; health 200; um warning de descoberta de porta |
| false | false | Ausente | Equivalente a absent; health 200; um warning |
| edge | true | Ausente | Sem redirect adicional, zero warnings de redirect |
| absent-port | Ausente | 8443 | Redirect 307 esperado fora de health |
| false-port | false | 8443 | Equivalente a absent-port |
| edge-port | true | 8443 | Nenhum redirect adicional, inclusive com porta configurada |

**174/174 asserções por imagem**, seis perfis, 72 requests na matriz, além das verificações de bodies e simulações. Incluíram health/case/trailing slash, token ausente/inválido/expirado/válido, perfil, esquema HTTPS, rota inexistente e headers X-Forwarded-* falsificados.

Não houve alteração de confiança: KnownProxies permaneceu vazio nesses perfis, e headers falsificados não mudaram Scheme/IP/Host observados. Ausente e false mantiveram equivalência. No cenário sintético de terminação TLS, edge-port respondeu diretamente 200, sem loop. O perfil legado absent-port **reproduziu os três redirects repetidos esperados da fixture** quando o esquema interno permanece HTTP; isso demonstra a condição anterior, não uma regressão nova. Não se afirma ausência de loops em toda configuração possível.

Nenhuma inspeção de proxy Render ou enforcement real na borda foi feita. A aprovação local não valida configurações remotas nem autoriza modificá-las.

## 13. Comparação do Docker Scout

Mesma versão Scout 1.27.0, mesma janela de execução e mesmos parâmetros, em referências **imutáveis das duas imagens finais novas**. Ajuda de quickview/cves/sbom consultada antes das opções adicionais. Sem filtros, ignore-base, supressões, exceções novas ou inclusão de estágios SDK.

Para cada ID da seção 7:

```text
docker scout quickview --output <EXP>/<lado>-quickview.txt local://sha256:<ID>
docker scout cves --details --locations --output <EXP>/<lado>-cves.txt local://sha256:<ID>
docker scout cves --format sarif --output <EXP>/<lado>-cves.sarif.json local://sha256:<ID>
docker scout sbom --format spdx --output <EXP>/<lado>-sbom.spdx.json local://sha256:<ID>
```

| Medida nova, da imagem final | Controle | Candidata | Diferença |
| --- | ---: | ---: | ---: |
| Critical | 0 | 0 | 0 |
| High | 0 | 0 | 0 |
| Medium | 20 | 13 | **−7** |
| Low | 4 | 4 | 0 |
| Total / identificadores distintos | 24 | 17 | **−7** |
| Pacotes vulneráveis | 11 | 9 | −2 |
| Pacotes indexados | 176 | 176 | 0 |
| Registros SPDX | 236 | 236 | 0 |
| Exclusivos do controle | 7 | — | — |
| Exclusivos da candidata | — | **0** | — |
| Comuns / mudanças de severidade | 17 / 0 | 17 / 0 | — |

O número 17 foi efetivamente medido na **imagem final SeenIt**, não reaproveitado do scan da base ASP.NET no B06-B1. Código 0 do Scout, sem `--exit-code`, não significa ausência de CVEs. O serviço não expôs timestamp/versão integral de sua base; foi mantida essa limitação. Severidade é a classificação Scout.

## 14. CVEs removidos e persistentes

Os sete identificadores exclusivos do controle desapareceram da candidata, com versões corrigidas confirmadas por dpkg e SBOM finais:

| CVE removida | Severidade Scout | Componente | Controle → candidata / versão corrigida indicada |
| --- | --- | --- | --- |
| CVE-2026-18374 | Medium | glibc | 2.39-0ubuntu8.9 → 2.39-0ubuntu8.12 |
| CVE-2026-84243 | Medium | glibc | Igual |
| CVE-2026-8674 | Medium | glibc | Igual |
| CVE-2026-86805 | Medium | glibc | Igual |
| CVE-2026-95818 | Medium | glibc | Igual |
| CVE-2026-97399 | Medium | glibc | Igual |
| CVE-2026-82560 | Medium | perl / perl-base | 5.38.2-3.2ubuntu0.6 → 5.38.2-3.2ubuntu0.7 |

Os 17 persistentes, **iguais em ambas**, com `not fixed` no Scout:

| Componente / versão final em ambos | CVEs comuns | Scout |
| --- | --- | --- |
| gnutls28 3.8.3-1.1ubuntu3.6 | CVE-2026-67693, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209, CVE-2026-95210 | 6 Medium |
| dash 0.5.12-6ubuntu5 | CVE-2026-102473, CVE-2026-102474 | 2 Medium |
| tar 1.35+dfsg-3ubuntu0.4 | CVE-2026-18477, CVE-2026-18508 | 2 Medium |
| dpkg 1.22.6ubuntu6.6 | CVE-2026-2219 | 1 Medium |
| util-linux 2.39.3-9ubuntu6.6 | CVE-2026-76642 | 1 Medium |
| systemd 255.4-1ubuntu8.18 | CVE-2026-121876; CVE-2026-40228 | 1 Medium + 1 Low |
| shadow 1:4.13+dfsg1-4ubuntu3.2 | CVE-2024-56433 | 1 Low |
| icu 74.2-1ubuntu3.1 | CVE-2025-5222 | 1 Low |
| gnupg2 2.4.4-2ubuntu17.6 | CVE-2026-105712 | 1 Low |

As faixas afetadas, localizações/camadas e versões corrigidas fornecidas pelo scanner estão nos relatórios detalhados/SARIF de cada lado. `not fixed` é ausência de versão corrigida informada pelo feed, não conclusão de ausência universal de patch. A descrição de rejeição de CVE-2026-95209 e disputas de aplicabilidade GnuTLS continuam visíveis, sem supressão. Não houve auditoria independente nova dos advisories além do inventário B06-B1 e dos scans deste experimento.

O escopo da redução é pacote/feed: o caso Power8 de glibc não é exploração demonstrada em amd64; pacotes-fonte Perl/ICU/GnuPG não provam presença/uso de todos os utilitários afetados. Os limites de aplicabilidade registrados no B06-B1 continuam válidos. Não se declara ausência de risco por zero High/Critical.

## 15. Regressões identificadas

**Nenhuma regressão da candidata identificada nos testes executados.** Mesmos 70 arquivos regulares publicados, versões de framework e resultados funcionais; nenhum CVE exclusivo da candidata ou mudança de severidade dos comuns.

Falhas de preparação dos harnesses foram descritas na seção 10, sem mascará-las como aprovação inicial ou atribuí-las à glibc nova. A candidata não precisou de mudança de código, dependências, permissões, codecs, SDK ou parâmetros funcionais para passar.

## 16. Limitações e evidências

- Resultados são locais, do HEAD informado, em Linux/amd64. Não comprovam equivalência com uma imagem atual do Render, configuração remota ou segurança de produção.
- Não foram acessados PostgreSQL/Supabase reais, Vercel ou Render; integração externa está limitada a doubles existentes. Não houve migrations, deploy, publicação de imagem ou alteração remota.
- Testes não são exploração de CVEs, fuzzing, benchmark de memória/carga ou análise completa dos codecs estáticos de libSkiaSharp.so. Skia/BuildHost continuam B06-C/B06-D.
- A igualdade dos arquivos publicados não significa igualdade completa de rootfs: libc6, libc-bin e perl-base mudaram de propósito. Configurações/runtime remoto e kernel não foram auditados.
- TestServer exercita middleware/endpoints reais em processo, sem substituir o smoke TCP de Kestrel. O smoke cobre somente health e startup; não transforma sucesso de doubles em aprovação de serviços reais.
- Os cenários HTTPS preservam a fronteira sem proxies conhecidos; não auditam todo ingresso real ou qualquer configuração possível. Data Protection e warnings operacionais permanecem.
- Não foi feita auditoria exaustiva de segredos. Foram usados apenas dados sintéticos e configuração sanitizada; valores sensíveis sintéticos fornecidos não apareceram nos logs testados.
- Os digests são fixos e não devem ser trocados automaticamente se deixarem de estar disponíveis. O scanner pode mudar de resultado sem mudança no artefato.

Evidências sob EXP: `Dockerfile.control/candidate`, `context/`, `tests/`, `source-hashes.json`, `repository-before.json`, `*-base-identity.txt`, `*-base-versions.txt`, `*-build.log`, `*-build-result.json`, `*-image.json`, `*-app/`, `publish-comparison.json`, `*-app-hashes.txt`, `*-packages.txt`, `*-runtime.txt`, `*-security.txt`, `*-smoke.json`, `*-smoke-logs.txt`, `compile-tests.log/.exit`, `sqlite-harness-diagnostic.log`, `*-tests/EntryPointChecks/results.json`, `*-assembled.log/.exit`, quickviews, CVEs detalhados, SARIFs, SBOMs SPDX, `scout-comparison.json` e `evidence-hashes.json`. Scripts temporários de compilação/smoke também foram preservados. Artefatos temporários não foram adicionados ao repositório.

| Evidência | SHA-256 |
| --- | --- |
| Controle SBOM | `77600e14dc8c0f5a31a538ee9afceed52147973a6364275966afeb45cd3ed7c6` |
| Candidata SBOM | `ffa4e3ca9360dc5c552fb18622030f563aab17993caad8f48bbe2c05933c0e18` |
| Controle SARIF | `9cb6ac0dc50151dde2bb2a22fa7cae2133eff9eb1ab6992d7687e2803bfefc44` |
| Candidata SARIF | `3023623697162e4a9dd8d83a7a872aca249bd298ab9e6dcef6e5570957c38200` |
| Controle build log | `1db99c47ebb49a1a5bc2ce740ea3413917dd406a41359af79be625449aece4f6` |
| Candidata build log | `1ac2a7efa160d520f45800aabb49f3401c7963fee722be8a0d614cb1aac20212` |
| Source hashes | `d9eb1aa0f768f1bd29fc08a2bc98c2dff84b9d622809ffb56402f7744f15ec0d` |
| Snapshot inicial | `7d0d9aebe3d1a0bbcb1185912a080a12d6873bf095b6da2ddb6b0c219a802bd9` |

O SARIF controle tem o mesmo hash do scan B06-B1 da imagem histórica, e o SARIF candidato coincide com o da base nova naquela coleta: o formato codificou o mesmo conjunto de regras/ocorrências, **não identidade das imagens**. Novos alvos finais e seus SBOMs/IDs estão registrados separadamente; ambos foram realmente escaneados.

## 17. Matriz de aprovação

| Critério | Controle | Candidata |
| --- | --- | --- |
| Digest ASP.NET | `05acd0ca...` (completo na seção 3) | `3a31a3c8...` (completo na seção 3) |
| Digest SDK | `ddbaedfc...` | Mesmo original |
| .NET / ASP.NET Runtime | 10.0.15 / 10.0.15 | 10.0.15 / 10.0.15 |
| Linux/amd64 | Verificado | Verificado |
| Build com verify executado | Aprovado | Aprovado |
| AvatarChecks | 16 grupos | 16 grupos |
| CoreChecks | Aprovado, incluindo 72 casos HTTP | Igual |
| Integração | 73 casos de cadastro e demais suítes aprovados | Igual |
| Skia/nativos/suplementos | Aprovados | Aprovados |
| Não-root/permissões | UID 1654, aprovado | Igual |
| Health / porta | 200 / 10000 | 200 / 10000 |
| JWT válido/inválido/expirado | Aprovado | Aprovado |
| HTTPS B06-A | 174/174 asserções | 174/174 asserções |
| glibc / Perl corrigidos | Versões antigas, controle esperado | 8.12 / 0.7 confirmados |
| Scout Critical | 0 | 0 |
| Scout High | 0 | 0 |
| Scout Medium | 20 | 13 |
| Scout Low | 4 | 4 |
| CVEs exclusivos da candidata | — | 0 |
| Alteração não autorizada necessária | Nenhuma | Nenhuma |

Os 13 critérios locais de aprovação do pedido foram satisfeitos, respeitando a separação entre execução local e serviços reais não testados.

## 18. Decisão técnica

**Aprovar localmente a candidata como base para uma futura implementação mínima.** Há evidência de benefício concreto de glibc/Perl no conteúdo final, redução medida de sete Medium, código/publicação idênticos entre controle e candidata e ausência de regressão nos checks executados.

Nenhuma necessidade de atualizar SDK, NuGet ou SkiaSharp foi demonstrada para essa mudança. Não promover automaticamente a imagem experimental e não interpretar aprovação local como aprovação de deploy ou garantia de segurança absoluta.

## 19. Plano de rollback futuro

Plano documental, não executado e sem autorização implícita de deploy:

1. Preservar a referência aprovada anterior ASP.NET `sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5`, o SDK original, o commit de referência e imagens anteriores.
2. Se uma atualização permanente futura causar regressão, preparar alteração específica que restaure somente o digest ASP.NET anterior, mantendo código/configuração funcional sob revisão.
3. Rebuild Linux/amd64, verify, smoke/health/porta e testes relevantes; registrar que retornar ao digest antigo reintroduz os sete alertas conhecidos.
4. Qualquer promoção/publicação ou rollback de serviço remoto deve ser autorizado em etapa própria. Não restaurar banco ou executar migrations como parte dessa troca de base.

O experimento atual não exige rollback no repositório: o Dockerfile original nunca foi alterado.

## 20. Recomendação objetiva para B06-B3

Preparar, **quando autorizado para implementação permanente**, alteração de uma única linha do FROM ASP.NET no Dockerfile original para o manifesto linux/amd64 candidato verificado. Manter SDK `ddbaedfc...`, Noble, .NET 10, NuGet, SkiaSharp, usuário, porta e configuração funcional inalterados.

Revalidar disponibilidade/identidade do digest e estado do Git; se o código mudar, repetir os testes afetados. Associar a implementação às evidências locais deste experimento e repetir build/Scout do artefato final aprovado, sem reutilizar contagem esperada como medição. Não misturar B06-C/SkiaSharp ou B06-D/BuildHost. Commit, PR e deploy não foram realizados nem ficam automaticamente autorizados por este relatório.

## 21. Preservação do repositório

Única criação desta etapa no repositório: `docs/B06-B2-DOCKER-RUNTIME-EXPERIMENT.md`. O B06-B1 permanece como arquivo não rastreado preexistente, com hash preservado. Nenhum arquivo original, frontend, Dockerfile, Program.cs ou csproj foi escrito.

A conferência de hashes confirmou **237/237 arquivos principais preexistentes** e **161/161 arquivos maintenance-phase1** inalterados. HEAD/main/origin/main local permanecem em `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`; maintenance permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Branches, worktrees, status preexistente de maintenance e tags históricas B05-B4 foram preservados. As duas tags B06-B2 ficaram disponíveis somente localmente.

Não houve reset, clean, rebase, force, exclusão de branch/worktree, remoção de imagens/containers/volumes/caches preexistentes, commit, stage, push, PR, migration, atualização de SDK/NuGet/SkiaSharp, acesso a produção, modificação remota ou deploy. Somente containers novos de validação/compilação/testes/exportação foram removidos após uso; nenhum recurso preexistente foi removido.

**B06-B2 CANDIDATA APROVADA LOCALMENTE**
