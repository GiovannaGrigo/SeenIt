# B05-B4.4 — Experimento isolado de renovação das bases Docker

## 1. Objetivo e resultado

Comparar controle e candidata com o **mesmo código do HEAD**, alterando intencionalmente apenas as referências FROM de SDK e ASP.NET em cópias temporárias. Experimento realizado em **17/11/2026**, America/Sao_Paulo.

**Resultado: candidata aprovada para uma futura intervenção restrita às bases.** Ambos os builds, checks, smoke tests e testes complementares passaram. Os seis achados prioritários foram corrigidos por versão instalada e ausência no scan da candidata. Scout: **36 → 23 resultados**, **4 → 0 High**, nenhuma CVE nova detectada. Permanecem **20 Medium e 3 Low**, documentados abaixo. Isso não aprova produção, não demonstra ausência de vulnerabilidades e não altera a imagem atualmente publicada.

## 2. Ambiente experimental

Branch `security/b05-b4-container-review`, HEAD `2cd74b978c4700a545475f7e4fdb7aff0cb3a07f`. Estado inicial esperado: somente B05-B4.1, B4.2 e B4.3 não rastreados. Nenhuma alteração inesperada.

Docker Desktop 4.97.0, Engine/CLI 29.9.2, linux/amd64, contexto desktop-linux. Scout 1.27.0 disponível com autenticação preexistente; scans concluíram sem login novo. PowerShell foi usado para orquestração; Node apenas para comparar JSON e preparar fixture temporária. Nenhuma ferramenta instalada.

Diretório externo ao Git: `%TEMP%/seenit-b05-b44-6545104934894deea38ef44b279f6490/`, doravante `EXP`. As duas raízes independentes são `EXP/control` e `EXP/candidate`. Artefatos, logs, SBOMs, SARIFs e harness ficaram somente em EXP.

## 3. Isolamento e preservação

`git archive HEAD` gerou um ZIP com 67 arquivos selecionados: fontes .cs, csproj e Dockerfile dos três projetos do build, `.dockerignore`, runtime-settings sanitizado, script de smoke versionado e CA pública prevista. Não foram copiados .env, appsettings locais, User Secrets, diretórios bin/obj, dados reais, credenciais ou chaves privadas. O ZIP foi extraído em controle e copiado fisicamente para candidata, sem symlinks.

As regras `.dockerignore` do HEAD foram preservadas nas duas cópias. Todos os demais comandos do Dockerfile ficaram intactos. Comparação de hashes entre as cópias mostrou diferença **somente no Dockerfile**, cujo diff contém apenas os dois FROM. Os 65 outros arquivos, além do .dockerignore, não foram editados. Nenhum build arg secreto.

Builds acessaram fontes públicas MCR/NuGet. Checks existentes usam HTTP/banco simulados. Containers de smoke e harness rodaram com **`--network none`**, sem `-p`, sem montar diretórios originais. O harness recebeu somente seu diretório temporário em leitura. A API usou porta interna 18081 e configuração inteiramente sintética; `/health` não acessa banco/Storage.

Nenhuma imagem/container preexistente foi alterada ou removida. A imagem `seenit-api:render-check` preservou o ID `sha256:75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28`. Foram adicionadas referências locais de bases públicas por digest, novas tags experimentais e imagens auxiliares de diagnóstico/build. Containers temporários foram removidos após encerramento; os dois containers preexistentes continuaram parados. Sem prune ou limpeza de imagens.

## 4. Código-fonte e hashes

Controle e candidata usam exclusivamente o HEAD informado; a imagem histórica de outra revisão **não foi usada como controle causal**. O controle foi reconstruído com Dockerfile atual, incluindo CA e permissões atuais.

| Evidência SHA-256 | Valor |
| --- | --- |
| ZIP de fontes selecionadas | `C337B272159CF0F307815221DF0C3C2EAB147D35201EC87C698FB908E3D8E4E8` |
| Manifesto source-hashes.json dos arquivos extraídos, antes das edições FROM | `18305A1FEDBF833B9274BD98036ADEA0D6CF3CD0815D9001B09ADE3AAFA46EA9` |
| Dockerfile original | `B43C9E1634B6DA7F4CADA3C1C375D8A2F71E8B7753E3B91532AE18CBC56AC22D` |
| .dockerignore original | `2690E1C926E6C3E468317A2356471BBEC30D628A83383B11730436CFACC4A415` |
| SeenIt.Api.csproj original | `79040AF396B449B56C780853C5FEB435A5A4F07BBCB4ACDBC5C1E2F48F7CA382` |

`original-hashes.json` e `report-hashes.json` registram hashes pré-experimento dos arquivos originais e relatórios. Conferência final: **zero arquivos originais alterados**, inclusive os três relatórios anteriores e .dockerignore. Os bytes extraídos são os do Git/HEAD; a representação de finais de linha no checkout Windows não foi usada para alterar o conteúdo experimental.

## 5. Bases verificadas

Manifestos e configurações foram consultados antes do build. As referências históricas da atestação eram **índices multiarch**; foram resolvidas para manifestos linux/amd64 e usadas imutavelmente nas cópias.

| Base | Controle | Candidata |
| --- | --- | --- |
| SDK | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:32ac63f80a3dfe48cf91ec1de80b0f394e37e353ee367c7fa87991c2111d22f4` | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` |
| ASP.NET | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:aa89a25c13f60a1df1018742e6e7fecd8d93cba469f3d079de410ff1390634ac` | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5` |
| Índice SDK | `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e` | `sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb` |
| Índice ASP.NET | `sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c` | `sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e` |
| SDK instalado | 10.0.403 | 10.0.404 |
| Shared frameworks | .NET e ASP.NET 10.0.14 | .NET e ASP.NET 10.0.15 |
| Distribuição | Ubuntu Noble | Ubuntu Noble |

Metadados `created` das bases: controle SDK 05/11/2026 22:59:52 UTC, ASP.NET 22:59:02 UTC; candidata SDK 10/11/2026 16:03:50 UTC, ASP.NET 16:01:27 UTC. São datas de criação da configuração, não timestamps de push/publicação confirmados no registry. As tags móveis públicas continuavam resolvendo para os índices candidatos acima na consulta de 17/11/2026. Nenhuma outra distribuição foi selecionada.

## 6. Identidade das imagens experimentais

| Campo | Controle | Candidata |
| --- | --- | --- |
| Tag temporária | `seenit-api:b05-b44-control-65451049` | `seenit-api:b05-b44-candidate-65451049` |
| ID/índice OCI local | `sha256:bb6f61410945cf4467f0828ef52d62a99a244e42d2568da061bb9876c364e2c9` | `sha256:2dd082136d827ca590b6476411949f813b280371713a28ac3c3c4b68af30da0a` |
| Manifesto linux/amd64 | `sha256:b987e2a4475e78181118b72be21a54f5508d24fc1c8df736d9925f40029f516c` | `sha256:b5930aadd0e591fbda4dc94478ff9bd503a2eae732ccbdad609ad69a3abf0e94` |
| Config OCI | `sha256:02941518b1b35d526c575ed6afa968ead0f94d95d7ee24397b66a576ce334c7a` | `sha256:816bb3dc9f7978662db04c37b7c065b20dcb222d06d5fdc8688d9b4458e44a04` |
| Created UTC | 17/11/2026 14:09:22 | 17/11/2026 14:09:45 |
| Plataforma | linux/amd64 | linux/amd64 |
| Tamanho informado pelo Engine | 378.115.933 bytes | 378.208.190 bytes |

Nenhuma publicação em registry. As tags auxiliares `seenit-api:b05-b44-build-control-65451049` e `seenit-api:b05-b44-build-candidate-65451049` exportam o estágio build para executar o harness offline; contêm SDK e fontes por desenho e **não são imagens finais da API**.

## 7. Versões instaladas e diferenças de pacotes

`dotnet --list-runtimes`, `/etc/os-release` e `dpkg-query` foram executados nas duas imagens finais. `dotnet --version` e runtimes também foram verificados em containers sem rede dos SDKs com os mesmos digests usados no estágio build.

| Componente na imagem final | Controle | Candidata |
| --- | --- | --- |
| Microsoft.NETCore.App | 10.0.14 | **10.0.15**, nenhuma 10.0.14 listada |
| Microsoft.AspNetCore.App | 10.0.14 | **10.0.15**, nenhuma 10.0.14 listada |
| Ubuntu | 24.04.5 LTS, noble | 24.04.5 LTS, noble |
| apt / libapt-pkg6.0t64 | 2.8.3 | **2.8.4ubuntu0.1** |
| libpcre2-8-0 / fonte pcre2 | 10.42-4ubuntu2.1 | **10.42-4ubuntu2.2** |
| libc6 / fonte glibc | 2.39-0ubuntu8.9 | 2.39-0ubuntu8.9 |
| perl-base / fonte perl | 5.38.2-3.2ubuntu0.6 | **5.38.2-3.2ubuntu0.6** |
| libssl3t64 | 3.0.13-0ubuntu3.16 | Igual |
| libicu74 | 74.2-1ubuntu3.1 | Igual |
| libstdc++6 | 14.2.0-4ubuntu2~24.04.1 | Igual |
| SkiaSharp e NativeAssets | 4.155.0 | Igual |

O SDK candidato contém Perl 5.38.2-3.2ubuntu0.7, mas **isso não atualiza o perl-base do runtime ASP.NET**. Essa distinção foi comprovada por consultas separadas e explica a permanência da CVE Perl na candidata final.

Comparação dos purls dos SBOMs, normalizando somente versão no identificador: seis registros com versão alterada (apt, libapt, pcre2, libpcre2, shared framework .NET e ASP.NET). Nenhuma mudança de versão de dependência NuGet própria da API foi detectada nessa comparação. BuildHost permaneceu.

## 8. Builds e checks existentes

Comando, aplicado separadamente às duas raízes e tags:

```text
docker build --platform linux/amd64 --progress plain --no-cache --provenance=mode=max -t <tag-experimental> -f <copia>/backend/SeenIt.Api/Dockerfile <copia>
```

Os builds completos retornaram **0**, sem alterar restore, publish, auditoria ou verify do Dockerfile. Restore da API levou aproximadamente 14 minutos em cada build. Diagnósticos separados mostraram endpoint público disponível, assinatura de pacote Microsoft válida e download nativo de 62,7 MB lento (2,4 MB em 35 s, timeout diagnóstico). Os builds principais não foram interrompidos e concluíram; não houve desabilitação de assinatura, auditoria ou troca de feed.

| Verificação no build | Controle | Candidata |
| --- | --- | --- |
| Restore/publish dos três projetos | Passou | Passou |
| NuGet vulnerable/include-transitive | Nenhum pacote vulnerável reportado nos três projetos | Igual |
| cmp de libSkiaSharp.so e SkiaSharp.dll API/checks | Passou | Passou |
| ldd sem dependências ausentes | Passou | Passou |
| AvatarChecks | **16 grupos aprovados** | **16 grupos aprovados** |
| CoreChecks: watch time | Passou | Passou |
| CoreChecks: classificação anime/cache/retries | Passou | Passou |
| CoreChecks: validação de registro | **72 casos HTTP aprovados** | **72 casos HTTP aprovados** |
| Marcador obrigatório de verify | Presente | Presente |
| CA lida por app, modo 0444 | Passou | Passou |

Ausência de alerta NuGet não substituiu o scan de SO/shared frameworks. Nenhum banco ou Storage real foi utilizado pelos checks.

## 9. SBOM e auditoria Scout independentes

Para cada imagem nova, `docker scout sbom local://<tag> --format spdx` e `docker scout cves local://<tag> --platform linux/amd64` em SARIF e texto com locations. Sem supressões, ignore ou alteração de políticas.

| Métrica | Controle | Candidata |
| --- | ---: | ---: |
| Pacotes indexados pelo Scout | 176 | 176 |
| Registros packages no SPDX 2.3 | 236 | 236 |
| Pacotes vulneráveis | 14 | 10 |
| Critical | 0 | 0 |
| High | 4 | **0** |
| Medium | 29 | **20** |
| Low | 3 | **3** |
| Unknown | 0 | 0 |
| Total de resultados | 36 | **23** |

SBOMs gerados em 17/11/2026 **14:10:05 UTC** e **14:10:24 UTC**, respectivamente. Os 236 registros incluem reconhecimentos por fonte/binário; não são 236 componentes únicos. Scout 1.27.0 consultou suas fontes GitHub/Ubuntu; não expôs timestamp/identificador integral da base de vulnerabilidades. As consultas próximas no tempo e o mesmo scanner reduzem, mas não eliminam, a possibilidade de mudança do feed entre consultas.

## 10. CVEs removidas, novas e remanescentes

**13 removidas no comparativo por CVE+pacote, nenhuma nova detectada**:

- ASP.NET/.NET: CVE-2026-114149, CVE-2026-114146, CVE-2026-114147, CVE-2026-109475, CVE-2026-109476 — frameworks 10.0.14 → 10.0.15.
- APT: CVE-2026-109518 — apt 2.8.3 → 2.8.4ubuntu0.1.
- PCRE2: CVE-2026-103111, CVE-2026-86145, CVE-2026-89156, CVE-2026-89157, CVE-2026-89158, CVE-2026-89160, CVE-2026-89161 — pacote 10.42-4ubuntu2.1 → 10.42-4ubuntu2.2.

A origem dessas remoções corresponde às bases, com pacotes instalados alterados e código/dependências da API preservados. Não se atribuem diferenças de IDs, timestamps ou compilação à segurança. SDK e runtime foram renovados em conjunto como variável experimental; não se isolou o efeito do compilador do SDK em um segundo experimento.

**23 resultados remanescentes**, todos já presentes no controle:

| Pacote da candidata | CVEs remanescentes | Scout | Patch informado / aplicabilidade resumida |
| --- | --- | --- | --- |
| glibc 2.39-0ubuntu8.9 | CVE-2026-18374, CVE-2026-84243, CVE-2026-8674, CVE-2026-86805, CVE-2026-95818, CVE-2026-97399 | 6 Medium | Scout indica 2.39-0ubuntu8.12. Condições distintas: fopen/ccs controlado; LANGUAGE/gettext; configuração DNS; loader AT_SECURE/setuid. A última descreve Power8, diferente de amd64. Sem exploração demonstrada; DNS/nativos justificam acompanhamento. |
| perl 5.38.2-3.2ubuntu0.6 | CVE-2026-82560 | Medium | Scout indica 5.38.2-3.2ubuntu0.7. Exige formatação de POD hostil; não identificada na API. Patch disponível não foi instalado neste experimento. |
| gnutls28 3.8.3-1.1ubuntu3.6 | CVE-2026-67693, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209, CVE-2026-95210 | 6 Medium | Sem patch informado; condições de certificados/constraints e uso da biblioteca não demonstrados no caminho da API. Parte das descrições é contestada pelo upstream. CVE-2026-95209 contém descrição de identificador rejeitado, conforme B4.3; manter resultado bruto sem tratá-lo como falha confirmada. |
| dash 0.5.12-6ubuntu5 | CVE-2026-102473, CVE-2026-102474 | 2 Medium | Sem patch informado; padrões/escapes controlados em shell. API usa entrypoint exec, sem caminho de invocação identificado. |
| tar 1.35+dfsg-3ubuntu0.4 | CVE-2026-18477, CVE-2026-18508 | 2 Medium | Sem patch informado; restore/extração de arquivo e condições de filesystem não fazem parte dos fluxos revisados da API. |
| dpkg 1.22.6ubuntu6.6 | CVE-2026-2219 | Medium | Sem patch informado; .deb zstd malformado processado por dpkg-deb, não pelo entrypoint. |
| util-linux 2.39.3-9ubuntu6.6 | CVE-2026-76642 | Medium | Sem patch informado; mount helper/hooks e privilégios necessários, não demonstrados no processo da API. |
| systemd 255.4-1ubuntu8.18 | CVE-2026-121876 | Medium | Sem patch informado; cenário de VM confidencial/GPT/host malicioso, não comprovado no container dotnet. |
| systemd 255.4-1ubuntu8.18 | CVE-2026-40228 | Low | Sem patch informado; journald/ForwardToWall e terminais específicos; não é entrypoint da API. |
| shadow 1:4.13+dfsg1-4ubuntu3.2 | CVE-2024-56433 | Low | Sem patch informado; colisão subuid com usuários de rede/NFS exige configuração não demonstrada. |
| icu 74.2-1ubuntu3.1 | CVE-2025-5222 | Low | Sem patch informado; ferramenta genrb e entrada hostil. ICU presente não prova execução de genrb. |

Aplicabilidade detalhada e divergências de severidade permanecem no B4.3. As contagens acima preservam a classificação **Scout**, inclusive Medium para CVE-2026-109475 no controle apesar de Microsoft High e High para CVE-2026-114146 apesar de Microsoft Critical. Nenhuma reclassificação silenciosa ou supressão.

## 11. Avaliação dos prioritários e patches

| Achado | Controle | Candidata | Conclusão experimental |
| --- | --- | --- | --- |
| CVE-2026-114149 | Presente, ASP.NET 10.0.14 | Ausente, ASP.NET 10.0.15 instalado | **CORRIGIDA** |
| CVE-2026-114146 | Presente, ASP.NET 10.0.14 | Ausente, ASP.NET 10.0.15 instalado | **CORRIGIDA** |
| CVE-2026-114147 | Presente, ASP.NET 10.0.14 | Ausente, ASP.NET 10.0.15 instalado | **CORRIGIDA** |
| CVE-2026-109475 | Presente, .NET 10.0.14 | Ausente, .NET 10.0.15 instalado | **CORRIGIDA** |
| CVE-2026-109476 | Presente, ASP.NET 10.0.14 | Ausente, ASP.NET 10.0.15 instalado | **CORRIGIDA** |
| CVE-2026-109518 | Presente, APT 2.8.3 | Ausente, APT 2.8.4ubuntu0.1 instalado | **CORRIGIDA** |
| Sete CVEs PCRE2 acima | Presentes em 10.42-4ubuntu2.1 | Ausentes em 10.42-4ubuntu2.2 | **CORRIGIDAS** |
| Seis CVEs glibc acima | Presentes | Mesma versão, presentes | **REMANESCENTES** |
| CVE-2026-82560 Perl | Presente | Mesma versão no final, presente | **REMANESCENTE** |

CORRIGIDA significa correspondência com a versão corrigida registrada nos advisories/Scout e remoção no scan, não teste ofensivo ou afirmação sobre produção. Não há prioritário classificado NÃO DETERMINADO na imagem candidata; alcance real em produção continua não determinado.

As correções de framework já confrontadas com fontes oficiais no B4.3 continuam sendo 10.0.15: [multipart](https://github.com/dotnet/announcements/issues/511), [SignalR/identidade](https://github.com/dotnet/announcements/issues/504), [SignalR/Redis](https://github.com/dotnet/announcements/issues/509), [HttpListener](https://github.com/dotnet/announcements/issues/500). Esta etapa comprova os componentes instalados e seus scans, sem repetir exploração. APT/PCRE2 foram comparados com os patches Ubuntu indicados pelo Scout, sem equiparar versões Debian/upstream.

## 12. Testes do container final

O mesmo script temporário foi executado nas duas imagens. Ele não inicia banco, não usa credenciais reais e não publica porta.

| Verificação | Controle | Candidata |
| --- | --- | --- |
| Inicialização Production com configuração sintética | Passou | Passou |
| Usuário efetivo | app, UID/GID 1654 | Igual |
| `/app` e `/app/certs` | root:root, 0755; app não grava em /app | Igual |
| CA pública | Presente, legível, root:root 0444, não gravável por app | Igual |
| DataProtection-Keys | app:app, 0755, gravável por app | Igual |
| Porta configurada | Binding 0.0.0.0:18081, só dentro de network none | Igual |
| GET /health interno | 200, corpo status ok | Igual |
| Logs do smoke | Sem chave JWT sintética, segredo Storage sintético ou string de conexão testada | Igual |
| Encerramento | docker stop com prazo de 10 s, log de shutdown, exit 0, sem OOM | Igual |
| Cleanup | Container novo removido após parada, sem force | Igual |

O teste de logs verifica especificamente os valores sintéticos fornecidos; não é certificação exaustiva de ausência de todo segredo em qualquer cenário. `/health` não valida banco/Storage/proxy Render. A lista de diretórios graváveis não é inventário completo dos caminhos da base.

## 13. Testes de avatares

Sem alterar API ou testes existentes, AvatarChecks validou nas duas bases JPEG, PNG e WebP reais, resize/reencode, remoção de payload anexado, arquivo vazio/acima de 5 MiB, MIME divergente, formato não suportado, dados corrompidos, arquivo truncado e dimensão excessiva, além dos demais contratos/storage simulados.

Dois testes adicionais iguais foram executados **fora dos contextos de build**, em harness sem NuGet externo, usando assemblies de `/out/api` do respectivo estágio build:

1. Fixture WebP animada sintética: o próprio SKCodec confirmou **dois frames** e AvatarImageProcessor rejeitou especificamente por animação. Passou nos dois.
2. Semáforo de decodificação: confirmou duas permissões; ocupou ambas, iniciou processamento válido e comprovou que ficou pendente; cancelou, devolveu as permissões e verificou nova conversão sem vazamento de permissões. Passou nos dois.

O teste do semáforo usa reflexão somente no harness para observar o gate existente; valida bloqueio/cancelamento/capacidade, não mede pico de CPU ou concorrência em produção. Os checks existentes de substituição concorrente de avatar também passaram, mas tratam outra propriedade.

A primeira tentativa complementar usou APNG construído no harness, não reconhecido por SKCodec como fixture de dois frames. O teste parou na validação da fixture em ambos, antes de testar a rejeição da aplicação. Foi substituído por WebP animado válido, sem mudança no código processador. Logs da tentativa inválida e dos dois resultados válidos foram preservados; não se contabiliza aquela falha de fixture como regressão ou aprovação da API.

Não houve teste ofensivo da CVE multipart. A confirmação obrigatória do parser corrigido é a versão ASP.NET 10.0.15 instalada e sua ausência do scan, além dos testes funcionais acima.

## 14. Dependências nativas

`libSkiaSharp.so` presente nas duas imagens, com o mesmo SHA-256: `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e`. A troca de bases não mudou o binário SkiaSharp 4.155.0.

`ldd` passou tanto em verify quanto no container final. Resolveu libc, libstdc++, libm, libpthread, libdl, librt e loader amd64, sem `not found`. JPEG/PNG/WebP foram realmente decodificados/reencodificados em ambas as bases.

Scout não expôs revisões completas dos codecs compilados estaticamente em SkiaSharp. Nenhuma CVE Chromium/Skia foi atribuída por suposição; ausência de alerta específico não prova segurança desses parsers. Mapeamento upstream dos codecs continua como investigação independente.

## 15. Conteúdo final e superfície dispensável

Em ambas as imagens finais: `dotnet --list-sdks` vazio e diretório SDK ausente; `/checks` ausente; listagem de `/app` sem executáveis SeenIt.Checks/AvatarChecks ou SQLite de testes. SkiaSharp, CA e marcador de verify presentes. Nenhuma dependência SQLite foi encontrada nos inventários Scout da API.

BuildHost-net472 e BuildHost-netcore, Microsoft.Build.Locator/Roslyn e seus arquivos auxiliares continuam presentes. Não foram removidos e não foram misturados à correção experimental das bases. Redução desse conteúdo exige experimento próprio de publish/tooling.

## 16. Limitações e evidências

Não há equivalência confirmada com Render, configurações reais, banco, proxy ou imagem publicada. Não foram feitos testes integrados de serviços externos, migrations, fuzzing/exploração ou benchmark de memória/carga. O resultado se limita aos checks e condições executados. Tag de frontend Dockerfile e metadados de build não foram convertidos em política permanente de reprodução; a mudança intencional permaneceu nas duas bases. Builds podem ter diferenças de compilador, timestamps e metadados.

Sem patch instalado para glibc/Perl no runtime final, apesar dos patches informados no Scout. Ausência de CVEs novas é relativa ao scanner/base consultados. Entrada rejeitada GnuTLS e divergências de severidade permanecem explicitamente registradas, sem suprimir contagens.

Evidências em EXP:

- `source.zip`, `source-hashes.json`, `original-hashes.json`, `report-hashes.json`, raízes control/candidate e metadados públicos das bases.
- `control-build.log`, `candidate-build.log`, ambos `.exit` 0; logs incluem verify/AvatarChecks/CoreChecks/auditoria NuGet.
- `control-image.json`, `candidate-image.json`; SBOMs SPDX, scans SARIF/texto e logs Scout separados; `comparison.json` e `compare.cjs`.
- `*-sdk-runtime.txt`, `*-runtime.txt`, `*-smoke.json`, `*-smoke-logs.txt`; script smoke temporário.
- `*-build-stage.log`, `harness/`, `*-extra-avatar.log` da fixture inválida e `*-extra-avatar-webp.log` das execuções válidas; ambos `*-pipeline.exit` 0 após os testes válidos.
- Logs diagnósticos de rede/restore/download/assinatura, incluindo timeouts dos diagnósticos, separados dos builds principais concluídos.

| Artefato | SHA-256 |
| --- | --- |
| Controle SBOM | `AAA9ECFBFF625F828D41EC7988227EC96C05F30CC2CB04C737BED0B2708F14FD` |
| Candidata SBOM | `014D39912E9F5138B3BB9B67142E0542CB5761F314B74985FF486994975F06DE` |
| Controle SARIF | `56F6C5E467DC72E46E2D4FEC2DB73EE960DB1693335DBB7F9EEB74115B619C31` |
| Candidata SARIF | `CC336B616101BEA07DC7F7FA37C4B45914448C3DA4A0541034A95BF23EC090E8` |

Temporários estão fora do repositório e podem ser descartados pelo sistema. Hash idêntico do SARIF controle ao B4.3 reflete o mesmo conjunto de regras/localizações detectadas, não identidade das imagens; os IDs das imagens são distintos e foram registrados separadamente.

## 17. Riscos e rollback

O benefício comprovado é remover as versões vulneráveis prioritárias preservando os checks executados. Riscos residuais incluem 23 resultados Scout, codecs estáticos não totalmente inventariados, conteúdo BuildHost e configurações reais não testadas. Atualizar apenas as bases não promete zerar alertas Ubuntu; não adicionar apt upgrade, troca de distribuição ou atualizações NuGet a esta intervenção sem outro experimento.

Nenhum rollback do repositório é necessário: originais não foram alterados. As tags de controle/candidata são novas e preservadas para reprodução; nenhum artefato foi publicado. Para uma futura integração, registrar os digests anteriores e novos e reverter somente a alteração de bases, com novo build e checks, se necessário. Reverter pode reintroduzir CVEs; não utilizar automaticamente a imagem histórica sem CA como rollback de produção. Não executar reset/clean/rebase/force.

## 18. Recomendação para B05-B4.5 e revisão final

Recomenda-se uma **intervenção permanente restrita aos dois FROM**, após nova autorização, usando os manifestos Noble corrigidos e política explícita de renovação. Revalidar disponibilidade dos digests, build completo, SBOM/Scout e smoke antes de qualquer publicação. Nenhuma atualização NuGet, mudança de avatar, redução de BuildHost ou correção Ubuntu adicional deve ser misturada nessa primeira intervenção.

Manter glibc/Perl e demais remanescentes em triagem separada; atualizar os registros de rejeição/qualidade do feed sem ocultar alertas. Não declarar imagem segura só por zero High no Scout. Planejar a correção adicional conforme alcance e patches efetivamente incorporados às futuras bases.

Os oito critérios foram atendidos no escopo local: frameworks corrigidos comprovados, checks sem regressão, inicialização e avatares funcionais nos cenários testados, usuário não-root, SDK/testes isolados do final e riscos remanescentes documentados.

Revisão final: somente este relatório novo é criado; três relatórios anteriores e todos os arquivos originais preservados por hash. `maintenance-phase1` permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Sem alterações funcionais/dependências/Dockerfile original, sem commit, push, PR, deploy ou acesso a serviços reais.

**B05-B4.4 EXPERIMENTO APROVADO**
