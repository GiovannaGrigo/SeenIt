# B06-B3 — Implementação controlada do ASP.NET Runtime Docker

Data: **23/11/2026**, America/Sao_Paulo. HEAD de referência: `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`.

**Implementação validada localmente.** Alteração funcional limitada ao digest ASP.NET de um FROM. Build sem cache, testes Linux/amd64 e scan da nova imagem final aprovados. Resultado Scout medido nesta etapa: **17 alertas — 0 Critical, 0 High, 13 Medium e 4 Low**. Nenhum commit, push, PR ou deploy realizado.

## 1. Objetivo

Aplicar ao Dockerfile original o digest aprovado no B06-B2 e validar a imagem realmente produzida por esse arquivo, preservando SDK, código, dependências, configurações e worktrees. A aprovação se limita ao ambiente local e às verificações executadas.

## 2. Estado inicial do Git

Branch `main`; HEAD e referência local `origin/main` em `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`. Status inicial exclusivamente:

```text
?? docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md
?? docs/B06-B2-DOCKER-RUNTIME-EXPERIMENT.md
```

Não houve divergência inesperada, fetch, atualização de branch ou checkout. Branches e worktrees foram registrados antes de editar. Snapshot SHA-256 de **238 arquivos preexistentes da árvore principal** e **161 arquivos de maintenance-phase1**, incluindo rastreados e não rastreados não ignorados, salvo em TEMP. Caches/binários ignorados não compõem esse inventário.

| Arquivo relevante | SHA-256 inicial, preservado exceto o Dockerfile autorizado |
| --- | --- |
| `backend/SeenIt.Api/Dockerfile` | `525a125804cde3159934026aef55ebcb01ecd1495d5d52254705cb49b5fdfbbc` |
| `backend/SeenIt.Api/Program.cs` | `b2b73ed98b7953b061ccb15cc4207bc5588628c015d073a01ef089fdeeac5812` |
| `backend/SeenIt.Api/SeenIt.Api.csproj` | `79040af396b449b56c780853c5feb435a5a4f07bbcb4acdbc5c1e2f48f7ca382` |
| `docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md` | `6de9e327eae8577dcc5dd0a5781a9fa9ffbcd9288a2fb76b325576ae0aa3c57c` |
| `docs/B06-B2-DOCKER-RUNTIME-EXPERIMENT.md` | `9c2f428f334e62413a91b2007434a3a228ffa40fd452d1efae75492ff46d7432` |

Demais csproj, relatórios, frontend, configurações e arquivos pendentes também constam do snapshot integral, sem copiar conteúdos sensíveis para o relatório.

## 3. Evidências B06-B1/B2

O [B06-B1](B06-B1-DOCKER-VULNERABILITY-INVENTORY.md) registrou 24 alertas na imagem final histórica B05-B4, sete corrigíveis via base nova. São dados de 19/11/2026, não contagem atual de produção.

O [B06-B2](B06-B2-DOCKER-RUNTIME-EXPERIMENT.md), lido integralmente, comparou controle/candidata do mesmo código e SDK. Mediu 24 → 17, sem novos CVEs exclusivos da candidata; verificou glibc/Perl, 70 arquivos publicados idênticos, build/verify, 16 grupos AvatarChecks, CoreChecks, 73 casos integrados e 174 asserções JWT/HTTPS, smoke não-root/10000 e testes nativos. A candidata foi aprovada somente localmente, com rollback pelo digest anterior e limitações de integrações reais, codecs estáticos, proxy e Data Protection.

Não se repetiu a investigação completa de advisories do B06-B1. Suas limitações de aplicabilidade não foram convertidas em ausência de risco. Nesta etapa os resultados funcionais e o scan foram executados novamente sobre a imagem B06-B3.

## 4. Digest original

Runtime substituído:

```text
mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5
```

SDK mantido integralmente:

```text
mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625
```

`dotnet --version` executado em container do SDK exato, sem rede, retornou **10.0.404**. O SDK de build não foi instalado na imagem final nem atualizado.

## 5. Digest atualizado e conferência prévia

```text
mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde
```

Antes da alteração, `docker buildx imagetools inspect` confirmou disponibilidade e identidade do manifesto de plataforma aprovado. Execução em container novo `--platform linux/amd64 --network none` confirmou x86_64, Ubuntu 24.04.5 LTS/Noble, Microsoft.NETCore.App **10.0.15**, Microsoft.AspNetCore.App **10.0.15**, libc6 **2.39-0ubuntu8.12** e perl-base **5.38.2-3.2ubuntu0.7**.

Não foi escolhido outro manifesto, tag flutuante ou distribuição. O digest acima é de plataforma; não é índice multiplataforma nem ID da imagem final da API.

## 6. Diff exato

Único arquivo funcional modificado: `backend/SeenIt.Api/Dockerfile`, linha 36.

```diff
-FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5 AS runtime-base
+FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde AS runtime-base
```

A substituição inversa reconstrói byte a byte o arquivo inicial. Foram preservados CRLF, SDK, comentários e todas as outras linhas. SHA-256 após a alteração: `078a25c4c04ee235e844226dcd97883b067925e3797df3ec5f6796afebc7bf5e`.

Revisão imediata: `git diff -- backend/SeenIt.Api/Dockerfile` mostrou somente essa linha; `git diff --numstat` retornou `1 1`; `git diff --check` passou. Nenhuma alteração adicional foi necessária para os testes.

## 7. Identidade da imagem final

| Campo | Medido no B06-B3 |
| --- | --- |
| Tag diagnóstica exclusiva | `seenit-api:b06-b3-validation-a740f09b` |
| ID do Engine / índice final | `sha256:2bfa34591ffc829dc20ca21cd1b1f2abacb57078a3ec338d909c1498dcff2c15` |
| Manifesto final de plataforma, log BuildKit | `sha256:987306bee5a472dafa7afecbcc5c04ed1d8b2ac16fcde9383fe8fd3085f8c145` |
| Config digest, log BuildKit | `sha256:5b23472d9ccaa52be1d57d7ece96abadbabbdcc129e28276da1890a4b3fbc411` |
| Criação UTC | `2026-11-23T18:01:55.823424447Z` |
| Criação São Paulo | 23/11/2026 15:01:55 |
| Plataforma | linux/amd64 |
| Sistema | Ubuntu 24.04.5 LTS / Noble |
| .NET / ASP.NET Runtime | 10.0.15 / 10.0.15 |
| Usuário / UID / entrypoint | app / 1654 / dotnet SeenIt.Api.dll |
| Porta | 10000/tcp, também exercitada no smoke |

A imagem B06-B2 candidata ainda existe, ID `sha256:855583c75c1698ff3d29f4c335bdaec2dfcddbcd9c5e12a0a103cbd2d439cfde`. IDs diferentes entre builds não indicam regressão. Índice, manifesto de plataforma e config digest identificam objetos diferentes. Atestação BuildKit não foi validada como assinatura criptográfica.

## 8. Resultado do build

Área de evidências fora do repositório:

`%TEMP%/seenit-b06-b3-20261123-a740f09b`

O contexto sanitizado B06-B2 foi copiado para essa área. Os **74 arquivos de origem** do inventário anterior foram conferidos contra os blobs do HEAD e os arquivos temporários; nenhuma diferença. Os três projetos de build compartilham o mesmo contexto anterior; testes de integração permanecem fora da imagem. Arquivos locais de segredos, frontend, SQL e appsettings reais não foram copiados.

O comando usou **o Dockerfile original modificado**, por caminho absoluto, não uma cópia candidata:

```text
docker build --platform linux/amd64 --no-cache --progress plain \
  --file C:/Users/giovana.grigolato/Downloads/seenit/backend/SeenIt.Api/Dockerfile \
  --tag seenit-api:b06-b3-validation-a740f09b <EXP>/context
```

Resultado: **exit 0**, duração observada **63,12 s**, sem warnings de compilação/erros identificados. Restore dos três projetos, publish Release, auditoria NuGet, RUN de verify com ldd/AvatarChecks/CoreChecks, marcador e validação da CA foram efetivamente executados. WORKDIR/metadados em cache não foram contados como execução dos checks.

Auditoria NuGet no Dockerfile informou ausência de pacotes vulneráveis nos três projetos nas fontes consultadas; não substitui o scan Docker. Nenhuma versão de dependência foi alterada e nenhum apt upgrade foi executado.

Ambiente atual: Docker CLI/Engine **29.9.3**, Desktop **4.99.0**, contexto desktop-linux, diferentes dos 29.9.2/4.97.0 históricos. O frontend versionado `docker/dockerfile:1`, inalterado no arquivo, resolveu agora `sha256:572cebbc2b15e6f84c077742952674889cb63cd5e9b80abdffcbe587b6f19183`, diferente do B06-B2. Não foi feita instalação/atualização de ferramentas nesta etapa. A comparação de conteúdo e testes abaixo foi usada em vez de presumir igualdade pelo ambiente anterior.

## 9. Testes executados

| Execução nova | Resultado no B06-B3 |
| --- | --- |
| AvatarChecks no verify Linux/amd64 | **16 grupos aprovados** |
| CoreChecks no verify Linux/amd64 | Watch time, classificação/cache/retry e **72 casos HTTP de cadastro** aprovados |
| RegisterIntegrationChecks na imagem final | **73 casos HTTP integrados de cadastro**, 16 usuários somente em SQLite memória; rejeições sem comandos/saves/hashes |
| Demais grupos da integração | Catálogo, integridade/biblioteca, rate limiting, JWT, login, perfil, spoofing, concorrência/liberação e CORS aprovados |
| EntryPointChecks na imagem final | **174/174 asserções**, seis perfis, 72 requests da matriz, zero falhas |
| ExtraAvatar na imagem final | **Três grupos suplementares aprovados**: animação, gate/cancelamento e limite de pixels |
| Smoke Kestrel real | Production, porta 10000, health 200, shutdown exit 0 |

As três execuções suplementares retornaram exit **0**. Seus binários de teste, já compilados no B06-B2 com o SDK fixado, foram reutilizados em diretórios temporários próprios; **os resultados foram executados novamente**, não reutilizados. A publicação da API em cada diretório de teste foi verificada arquivo a arquivo contra os 70 arquivos da nova imagem, sem diferenças. Não se alterou teste permanente nem se afirmou uma compilação nova dos harnesses.

Todas as execuções funcionais .NET foram **dentro do Linux/amd64**, com usuário app e rede none. O host Windows executou somente Docker, scripts de preparação, comparações e hashes. TestServer utiliza o entry point real, banco SQLite em memória e doubles que proíbem HTTP externo. Não foi acionado `--postgres-only`, nem executada migration. SQLite nativo 2.1.12 permaneceu somente no harness montado, sem instalação na imagem final.

Catálogo/biblioteca foram exercitados pelas suítes de resiliência/integridade; respostas de erro, timeout, cancelamento e recuperação usaram HTTP falso. AvatarChecks cobriu contratos/URLs Storage, upload/delete simulados, substituição, compensação e conflitos. Isso não comprova PostgreSQL/Supabase reais.

## 10. Segurança e permissões

Verificações na imagem final: usuário **app**, UID/GID **1654**; entrypoint original; Production; `/app` e `/app/certs` root:root 0755 e sem escrita por app; CA pública de 1367 bytes, root:root **0444**, legível e não gravável; diretório DataProtection-Keys acessível/gravação como app; marcador obrigatório de verify presente.

`dotnet --list-sdks` vazio e diretório SDK ausente; `/checks`, executáveis SeenIt.Checks/AvatarChecks e SQLite de testes ausentes do conteúdo final verificado. BuildHost-net472/netcore continua presente como na candidata anterior; não foi confundido com SDK completo nem removido fora do escopo B06-D.

Container de smoke: `Privileged=false`, `CapAdd=null`, `PortBindings={}`, rede none. Não houve ampliação de capabilities/permissões. Health retornou HTTP 200 e corpo `{"status":"ok"}` em TCP interno `127.0.0.1:10000`, sem porta publicada. Binding confirmado em `http://0.0.0.0:10000`; parada normal, exit 0, sem OOM e com log de shutdown.

Configurações e credenciais exclusivamente sintéticas. Os valores sensíveis sintéticos fornecidos não apareceram nos logs testados; não se declara auditoria exaustiva de segredos. CA e appsettings sanitizado tiveram os mesmos hashes da candidata anterior.

## 11. Compatibilidade SkiaSharp

SkiaSharp/NativeAssets **4.155.0**, sem mudança. Hash final de `libSkiaSharp.so`: `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e`, igual ao B06-B2. Ldd resolveu libc, libstdc++, libm, libpthread, libdl, librt, libgcc e loader amd64, **sem not found**, no verify e no container final.

Os checks realmente decodificaram JPEG/PNG/WebP, detectaram formato, redimensionaram e reencodificaram WebP, descartando payload anexado. Rejeições verificadas para vazio, tamanho >5 MiB, MIME falso, formato inválido, corrupção/truncamento e lado >8192 pixels. O suplemento confirmou rejeição de imagem 4001×4000 por limite de 16 milhões de pixels e de WebP com dois frames confirmado pelo SKCodec.

O gate de dois decoders bloqueou o processamento enquanto ocupado, respeitou cancelamento e voltou à capacidade de dois após conversão/rejeição. Isso não equivale a benchmark de concorrência/carga ou prova de ausência de todo vazamento nativo. Não foram atualizados codecs nem investigadas novamente suas revisões estáticas; B06-C permanece separado.

## 12. JWT e HTTPS

EntryPointChecks executou novamente **174/174 asserções**, seis perfis: opção `ReverseProxy:HttpsRedirectionHandledByEdge` ausente, false e true, com e sem porta HTTPS sintética 8443. Todos preservaram health; ausente e false mantiveram equivalência; true não introduziu redirect adicional, inclusive com porta configurada.

Foram exercitados JWT válido, inválido/ausente e expirado corretamente assinado, autorização/perfil, HTTP/HTTPS, headers X-Forwarded-* falsificados, rota inexistente, bodies e simulação de terminação TLS. JWT expirado permaneceu 401. Scheme/IP/Host não foram alterados pelos headers falsificados com KnownProxies vazio. A configuração de confiança em proxies não foi modificada.

No perfil de borda true, a simulação respondeu 200 diretamente, sem loop. A fixture de comportamento legado com opção ausente e porta configurada reproduziu os três redirects esperados quando o esquema interno continua HTTP; não é regressão nova nem aprovação de qualquer topologia real.

Suítes existentes também aprovaram login/cadastro, rate limiting e 20 logins sobrepostos, contabilização global, isolamento de janelas, perfil simultâneo, erros/cancelamento e CORS. Nenhuma alteração de JWT, Program.cs ou autenticação.

## 13. Docker Scout

Versão atual **1.28.0**, commit `cfd5fa8a65edd1216b4a46e6797f1831dc40ae73`; B06-B2 usava 1.27.0. Ajuda dos comandos consultada antes das opções. Nenhuma atualização de ferramenta foi feita. Sem filtros, ignores, supressões ou inclusão de estágio SDK.

Nova imagem final analisada explicitamente por `local://sha256:2bfa34591ffc829dc20ca21cd1b1f2abacb57078a3ec338d909c1498dcff2c15`:

```text
docker scout cves --details --locations --output <EXP>/final-cves.txt <REF>
docker scout cves --format sarif --output <EXP>/final-cves.sarif.json <REF>
docker scout quickview --output <EXP>/final-quickview.txt <REF>
docker scout sbom --format spdx --output <EXP>/final-sbom.spdx.json <REF>
```

| Medida efetivamente obtida | B06-B3 |
| --- | ---: |
| Critical | 0 |
| High | 0 |
| Medium | 13 |
| Low | 4 |
| Total / CVEs distintos | **17** |
| Pacotes vulneráveis / indexados | 9 / 176 |
| Registros SPDX | 236 |

Inventário dos 17 alertas, preservando a severidade Scout:

| CVEs individuais | Pacote-fonte / versão instalada | Severidade | Versão corrigida no Scout |
| --- | --- | --- | --- |
| CVE-2026-67693, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209, CVE-2026-95210 | gnutls28 3.8.3-1.1ubuntu3.6 | 6 Medium | not fixed |
| CVE-2026-102473, CVE-2026-102474 | dash 0.5.12-6ubuntu5 | 2 Medium | not fixed |
| CVE-2026-18477, CVE-2026-18508 | tar 1.35+dfsg-3ubuntu0.4 | 2 Medium | not fixed |
| CVE-2026-2219 | dpkg 1.22.6ubuntu6.6 | Medium | not fixed |
| CVE-2026-76642 | util-linux 2.39.3-9ubuntu6.6 | Medium | not fixed |
| CVE-2026-121876 | systemd 255.4-1ubuntu8.18 | Medium | not fixed |
| CVE-2026-40228 | systemd 255.4-1ubuntu8.18 | Low | not fixed |
| CVE-2024-56433 | shadow 1:4.13+dfsg1-4ubuntu3.2 | Low | not fixed |
| CVE-2025-5222 | icu 74.2-1ubuntu3.1 | Low | not fixed |
| CVE-2026-105712 | gnupg2 2.4.4-2ubuntu17.6 | Low | not fixed |

Todos foram atribuídos a pacotes-fonte Ubuntu, não ao SDK. `not fixed` significa ausência de versão corrigida informada pelo scanner, sem afirmar inexistência universal de patch. Foram preservadas as descrições contestadas/rejeitadas em GnuTLS, sem supressão. Localizações e camadas constam do texto detalhado/SARIF.

Código 0 sem opção `--exit-code` não significa zero CVEs. O serviço não expôs revisão/timestamp integral da base de vulnerabilidades. SBOM reutilizado/indexado pelo Scout não foi tratado como nova imagem ou prova de segurança completa.

## 14. Comparação com B06-B2

A candidata B06-B2 foi **reanalisada hoje pelo mesmo Scout 1.28.0**, por seu ID imutável, além da comparação com o SARIF histórico de 19/11. Isso separa diferenças de ferramenta/feed de diferenças na implementação.

| Critério | Candidata B06-B2 | Implementação B06-B3 |
| --- | --- | --- |
| SDK / .NET / ASP.NET | 10.0.404 / 10.0.15 / 10.0.15 | Iguais |
| Plataforma / base runtime | linux/amd64, digest aprovado | Iguais |
| libc6 / libc-bin | 2.39-0ubuntu8.12 | Iguais |
| perl-base | 5.38.2-3.2ubuntu0.7 | Igual |
| libicu74 | 74.2-1ubuntu3.1 | Igual |
| Pacotes binários dpkg | Inventário anterior preservado | Nenhuma versão/pacote diferente |
| Arquivos regulares /app | 70 | 70, mesmos caminhos e hashes |
| SeenIt.Api.dll SHA-256 | `6b5502da068e98d7a1059fdbf77e13a896252d63db8092fd4512a862c6c5e9b6` | Igual |
| SkiaSharp nativo | Hash da seção 11 | Igual |
| Scout histórico 19/11 | 0C / 0H / 13M / 4L | Referência, não medição nova |
| Scout executado em 23/11 | 0C / 0H / 13M / 4L | **0C / 0H / 13M / 4L** |
| CVEs exclusivos / mudanças de severidade ou correção | Nenhum na comparação atual | Nenhum |

Comparação das regras confirmou os mesmos identificadores, purls/versões, severidades e versões corrigidas, tanto frente ao histórico quanto à reanálise atual. O SARIF das duas imagens sob 1.28.0 teve hash idêntico; isso reflete inventário de ocorrências, **não identidade de imagem**.

Os sete alertas corrigidos no B06-B2 continuam ausentes da implementação: **CVE-2026-18374, CVE-2026-84243, CVE-2026-8674, CVE-2026-86805, CVE-2026-95818, CVE-2026-97399 e CVE-2026-82560**. Frente ao controle histórico de 24, são sete Medium a menos. O controle antigo não foi reconstruído nesta etapa.

**Nenhuma regressão funcional identificada na cobertura executada.** Igualdade de publicação e pacotes não foi usada para presumir igualdade de IDs, timestamps de camadas, atestações ou segurança absoluta.

## 15. Riscos residuais

Permanecem os 17 alertas acima, com condições de alcance/uso ainda limitadas pela evidência do B06-B1. Pacote-fonte não prova presença de todo utilitário afetado. Zero High/Critical não demonstra ausência de risco. Codecs estáticos de SkiaSharp e conteúdo BuildHost permanecem investigações próprias B06-C/B06-D.

Warnings comuns do smoke permaneceram: Data Protection sem persistência garantida, ausência de encryptor XML e precedência de URLS sobre HTTP_PORTS. Nenhum foi ocultado ou tratado como corrigido pela renovação da base. Não há avaliação remota de capacidades, kernel, mounts, DNS, proxy ou armazenamento de chaves do Render.

## 16. Limitações e evidências

Testes e imagem são locais. Não foram acessados banco ou Storage reais, Render/Vercel, variáveis remotas ou produção. Health não comprova essas integrações. TestServer e doubles não equivalem a teste completo de rede/serviço; o smoke TCP complementa somente inicialização/health. Não houve fuzzing, exploração de CVEs ou benchmark de carga/memória.

Ocorrências de preparação, sem mudança na aplicação:

- Scout inicialmente falhou ao criar arquivo porque faltava seu diretório temporário por digest. Criaram-se somente os diretórios ausentes em TEMP, sem remover caches. A nova execução concluiu e gerou os relatórios. A tentativa inválida não foi apresentada como scan bem-sucedido.
- Uma tentativa redundante de sobrepor novamente a publicação aos harnesses copiados foi negada no diretório de certificados. A verificação por SHA-256 confirmou **todos os 70 arquivos de publicação já idênticos em cada um dos três harnesses**, inclusive API/Skia/CA; nenhuma alteração de permissão ou código foi necessária. Os testes válidos executaram esses mesmos bytes na imagem B06-B3.

Evidências locais em EXP: `repository-before.json`, `source-hashes.json`, `Dockerfile.before/after`, `context/`, `source/`, `tests-artifacts/`, `build.log`, `build-result.json`, `sdk-version.txt`, `image.json`, `app/`, `publish-comparison.json`, `final-packages.txt`, `final-runtime.txt`, `final-security.txt`, `final-smoke.json`, `final-smoke-logs.txt`, logs/exit das três suítes, `tests-artifacts/EntryPointChecks/results.json`, quickview, CVEs detalhados, SARIF/SPDX, reanálise atual B06-B2, `scout-comparison.json`, `evidence-hashes.json` e conferência final de preservação. Temporários estão fora do repositório e sujeitos à retenção do sistema.

| Evidência | SHA-256 |
| --- | --- |
| Build log | `dc58ec81d8c2ec390cf19852e08a8298969712d610f943ffa3527f78f488be71` |
| Image inspect | `0c21fd641b3dac567c43cfe72dfbf5c717438ec7d737d84e9ced9958d9cd02dc` |
| SARIF final | `5db5206efce59f6bc7047182514afdec379895caa0dcfe34426d15d19b4a340f` |
| SARIF reanálise B06-B2 | `5db5206efce59f6bc7047182514afdec379895caa0dcfe34426d15d19b4a340f` |
| SBOM final SPDX | `1174cdd7967f6c79aee6672f89bfc52ad9938cdf62793ba2105568aad17e69b4` |
| Inventário de hashes da origem | `d9eb1aa0f768f1bd29fc08a2bc98c2dff84b9d622809ffb56402f7744f15ec0d` |
| Snapshot inicial | `9ad99394517e0e9f5fd733bcd424cbd848e56ef602e707d0439d8658041bcf30` |

## 17. Rollback futuro

Plano documental, não executado: restaurar exclusivamente o FROM ASP.NET para `sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5`, mantendo SDK/código/configuração. Validar diff de uma linha, build Linux/amd64, verify, health/porta, JWT/HTTPS e checks afetados antes de qualquer promoção.

Registrar que esse retorno reintroduz os sete alertas conhecidos. Não usar reset/clean/rebase/force nem restaurar banco/migrations como parte da troca de base. Qualquer rollback ou implantação de serviço remoto exige autorização própria.

## 18. Recomendação para B06-B4

Prosseguir com revisão final da alteração mínima e das evidências locais, preparando commit/PR somente em uma etapa que os autorize explicitamente. Preservar o digest aprovado e o SDK original; não selecionar automaticamente um manifesto mais novo. Se código, runtime ou parâmetros mudarem, revalidar os pontos afetados e escanear o artefato final correspondente.

Os critérios locais foram satisfeitos: diff mínimo, identidade da base, build e suites aprovados, Linux/amd64, não-root, porta/health, Skia/nativos, JWT/HTTPS, patches e redução Scout comprovados, sem outro arquivo funcional modificado. A conclusão não autoriza merge, publicação de imagens ou deploy.

## 19. Estado final do Git

Estado esperado e conferido ao encerramento:

```text
 M backend/SeenIt.Api/Dockerfile
?? docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md
?? docs/B06-B2-DOCKER-RUNTIME-EXPERIMENT.md
?? docs/B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md
```

B06-B1/B2 são arquivos preexistentes preservados. A única criação desta etapa no repositório é este relatório; o único arquivo rastreado alterado é o Dockerfile, com uma linha substituída. `git diff --check` aprovado. Nenhum stage/commit, push, PR ou deploy; HEAD/main/origin/main local continuam em `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`.

## 20. Preservação das worktrees

| Worktree | Branch | HEAD preservado |
| --- | --- | --- |
| `C:/Users/giovana.grigolato/Downloads/seenit` | main | `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91` |
| `C:/Users/giovana.grigolato/Downloads/seenit/.worktrees/maintenance-phase1` | maintenance-phase1 | `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290` |

Maintenance mantém seis arquivos rastreados modificados preexistentes (AppDbContext, Program, AvatarProfileService, IAvatarReferenceStore, RateLimitingRegressionChecks e RegistrationFactory) e suas entradas não rastreadas: Maintenance/, MaintenanceAdmin/, MaintenanceChecks/, MAINTENANCE-PHASE1.md, RENDER-FREE-OPERATIONS.md e operations/. Conferência por HEAD, status e **161 hashes** preserva esse estado. Na principal, **237 arquivos preexistentes não autorizados para alteração** mantêm hashes; o 238º é o Dockerfile alterado exatamente como autorizado. Branches/worktrees não foram criados, excluídos ou modificados.

Imagens B05-B4/B06-B2 e suas tags foram preservadas; a tag B06-B3 é exclusiva. Nenhuma imagem foi publicada. Somente containers novos de validação/exportação/testes foram removidos após uso. Nenhuma imagem/container/volume/cache preexistente foi removido; não houve operação destrutiva, alteração remota, acesso a produção, migration ou modificação em Program.cs, SDK, NuGet, SkiaSharp, JWT, frontend ou infraestrutura.

**B06-B3 IMPLEMENTAÇÃO VALIDADA**
