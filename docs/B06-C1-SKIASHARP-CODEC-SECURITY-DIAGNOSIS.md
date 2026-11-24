# B06-C1 — Diagnóstico de segurança do SkiaSharp e codecs nativos

Data da investigação: **24/11/2026**, America/Sao_Paulo. Escopo exclusivamente investigativo; nenhuma correção aplicada.

## 1. Objetivo

Relacionar pacotes resolvidos, biblioteca publicada, codecs e operações efetivamente expostas pelo avatar. Ausência de alertas no Scout ou NuGet não certifica bibliotecas estáticas. Este documento distingue presença de componente afetado de exploração pelo endpoint.

Resultado principal: SkiaSharp e NativeAssets continuam em **4.155.0**. Foram identificados libjpeg-turbo **3.1.4.1**, libpng **1.6.58** e revisão de fonte libwebp **1.6.0**. Existe advisory para libpng 1.6.58, mas sua sequência vulnerável é excluída pelo leitor progressivo examinado. Existem seis backports de segurança documentados no SkiaSharp 4.155.2; o alcance de todos os caminhos internos não foi demonstrado. Recomenda-se um experimento coordenado nessa versão de manutenção, sem presumir correção do libpng.

## 2. Estado do Git

Estado inicial conferido antes de criar este arquivo:

- Branch `main`, limpa.
- HEAD, `main` e referência local `origin/main`: `400a0dfa74109723336fd9de89d119d5a7bb4284`.
- Não houve fetch; a igualdade com `origin/main` refere-se à referência local, não a uma consulta nova do servidor.
- Worktree principal neste repositório e worktree `.worktrees/maintenance-phase1`, branch `maintenance-phase1`, HEAD `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`.
- A worktree de manutenção já tinha alterações locais: seis fontes modificadas e arquivos/diretórios não rastreados. Foram inventariados e preservados, sem tentativa de limpeza.
- Branches adicionais preservadas: `b05-b1-angular-security`, `fix/guest-route-guard`, `security/b05-b2-dependency-review`, `security/b05-b3-sqlite-review`, `security/b05-b4-container-review`, `security/b06-a1-hardening-diagnosis` e `security/b06-b4-docker-runtime`.

Snapshot inicial em `%TEMP%/seenit-b06-c1-20261124-582a19f7/repository-before.json`: **240 arquivos** da árvore principal e **161 arquivos** da manutenção, rastreados/não rastreados não ignorados, com SHA-256, HEAD, status, refs locais e worktrees. Caches ignorados não constituem um inventário integral do disco.

| Arquivo relevante preservado | SHA-256 |
| --- | --- |
| `backend/SeenIt.Api/SeenIt.Api.csproj` | `79040af396b449b56c780853c5feb435a5a4f07bbcb4acdbc5c1e2f48f7ca382` |
| `backend/SeenIt.Api/Program.cs` | `b2b73ed98b7953b061ccb15cc4207bc5588628c015d073a01ef089fdeeac5812` |
| `backend/SeenIt.Api/Services/Avatars/AvatarImageProcessor.cs` | `878fe5cdf5e7e90457d178f16e37fac72c379e5237220b6b4d49fae54e598695` |
| `backend/SeenIt.Api/RateLimiting/AbuseProtection.cs` | `7652be237695a84feb7b8b4fcdd1e6c3d37bda73763db5ce4db8efd82d6295e4` |
| `backend/SeenIt.Api/Dockerfile` | `078a25c4c04ee235e844226dcd97883b067925e3797df3ec5f6796afebc7bf5e` |

## 3. Histórico relevante

Foram revisados os relatórios B05-B4 de diagnóstico, SBOM, CVEs e experimento de bases, além de [B06-B1](B06-B1-DOCKER-VULNERABILITY-INVENTORY.md), [B06-B2](B06-B2-DOCKER-RUNTIME-EXPERIMENT.md), [B06-B3](B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md) e [encerramento B06-B](B06-B-FINAL-SUMMARY.md).

O B06-B trocou somente a base ASP.NET, preservando SDK, fontes e SkiaSharp. O resultado local histórico foi **24 → 17 alertas**, sendo 13 Medium e 4 Low na imagem final; não representa uma nova varredura nesta etapa. O hash nativo permaneceu igual. A integração ocorreu pelo PR #7, merge `0afd3233debd2781b61baa97ccfd738c2dc530fb`, seguido do relatório final no HEAD atual.

Resultados históricos: `ldd` sem bibliotecas ausentes; 16 grupos AvatarChecks; CoreChecks com 72 casos HTTP de cadastro; 73 casos integrados; 174 asserções JWT/HTTPS; JPEG/PNG/WebP reais, animação WebP rejeitada, limites de pixels e gate, reencode e smoke como usuário não-root na porta 10000. **Esses testes não foram reexecutados no B06-C1.** A validação operacional de produção registrada no encerramento B06-B também não foi repetida.

## 4. Inventário dos pacotes

Leitura do csproj, `obj/project.assets.json` existente, nuspecs já preservados e publicação B06-B3; nenhum restore realizado.

| Componente | Natureza / resolução |
| --- | --- |
| `SkiaSharp` | Direto, **4.155.0**, compilação `ref/net10.0/SkiaSharp.dll`, runtime `lib/net10.0/SkiaSharp.dll` |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | Direto, **4.155.0**, asset selecionado `runtimes/linux-x64/native/libSkiaSharp.so` |
| `SkiaSharp.NativeAssets.macOS` | Transitivo de SkiaSharp, **4.155.0**, no grafo de desenvolvimento |
| `SkiaSharp.NativeAssets.Win32` | Transitivo de SkiaSharp, **4.155.0**, no grafo de desenvolvimento |

O grupo net10.0 do nuspec gerenciado depende de macOS/Win32, ambos 4.155.0; não depende de System.Memory nesse TFM. O nuspec Linux NoDependencies não declara dependências NuGet nesse grupo. Isso não significa ausência de dependências ELF ou de código incorporado.

Outras referências diretas da API: JwtBearer/OpenApi 10.0.0, Microsoft.OpenApi 2.7.5, Pkcs 9.0.22, EF Design 10.0.0 e Npgsql EF 10.0.0. Não são codecs transitivos do SkiaSharp. AvatarChecks referencia a API, herdando esses pacotes; não fornece um segundo motor de imagens.

Na publicação preservada, `SeenIt.Api.deps.json` declara `.NETCoreApp,Version=v10.0/linux-x64`, SkiaSharp/NativeAssets Linux 4.155.0 e assembly/fileVersion gerenciado 4.155.0.0. Não inclui as entradas macOS/Win32 do grafo do host. O fileVersion nativo 0.0.0.0 desse JSON **não identifica a versão do motor nem de codecs**. O pacote Linux também contém outros RIDs; o alvo examinado é glibc/linux-x64, não musl ou ARM.

Fontes de metadados: [SkiaSharp 4.155.0](https://www.nuget.org/packages/SkiaSharp/4.155.0), [NativeAssets 4.155.0](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.155.0).

## 5. Identidade do binário nativo

Artefato local preservado de B06-B3: `%TEMP%/seenit-b06-b3-20261123-a740f09b/app/libSkiaSharp.so`, correspondente a `/app/libSkiaSharp.so` na imagem histórica `seenit-api:b06-b3-validation-a740f09b`, ID `sha256:2bfa34591ffc829dc20ca21cd1b1f2abacb57078a3ec338d909c1498dcff2c15`.

- Tamanho: **12.287.632 bytes**.
- SHA-256 recalculado: **`26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e`**.
- Igualdade de bytes/hash com o asset linux-x64 do pacote 4.155.0 no cache preservado B06-B2, em `test-packages/skiasharp.nativeassets.linux.nodependencies/4.155.0/runtimes/linux-x64/native/`.
- ELF64, little-endian, `e_machine=62` (**x86-64**); SONAME **`libSkiaSharp.so.155.0.0`**.
- Inspeção estática das funções `sk_version_get_milestone`, `sk_version_get_increment` e `sk_version_get_string`: valores 155, 0 e string **155.0**. A versão NuGet continua sendo 4.155.0.

Leitura própria de cabeçalhos/seções ELF, símbolos e strings com Node disponível, sem executar o `.so` e sem ferramentas baixadas. `.symtab` e `.dynsym` estão presentes. Evidências temporárias: `elf-static.json`, `codec-symbols.json`, `codec-static-evidence.json`. Essa inspeção não equivale a um build reproduzível ou atestação independente do fornecedor.

O nuspec aponta para SkiaSharp commit **`d25e7fa628a2576e50ecc42004cc4fd03c6657f5`**, cujo submódulo é [mono/skia `c2d78902a56e24827a7273a4ed3461ce18780680`](https://github.com/mono/SkiaSharp/tree/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/externals). Essa cadeia foi consultada na API oficial, não inferida apenas pelo número m155.

## 6. Bibliotecas dinâmicas

DT_NEEDED extraído nesta etapa:

| Biblioteca | Origem / função |
| --- | --- |
| `libstdc++.so.6` | Runtime C++ Ubuntu, pacote libstdc++6 |
| `libpthread.so.0`, `libdl.so.2`, `libm.so.6`, `libc.so.6`, `librt.so.1` | glibc, pacote libc6 |
| `ld-linux-x86-64.so.2` | Loader glibc amd64 |

O `ldd` histórico em `B06-B3/final-runtime.txt` resolveu essas bibliotecas em `/lib/x86_64-linux-gnu/`, loader `/lib64/ld-linux-x86-64.so.2`, além de **libgcc_s.so.1** transitiva. Sem `not found`. Pacotes históricos: libc6 **2.39-0ubuntu8.12**, libstdc++6/libgcc-s1 **14.2.0-4ubuntu2~24.04.1**.

**Não houve novo ldd bem-sucedido:** tentativas em containers novos, sem rede e descartáveis, falharam antes de iniciar porque o pipe `dockerDesktopLinuxEngine` não existia. O Engine não estava disponível. Não foi possível observar `/proc/<pid>/maps`, o conjunto efetivamente carregado por um processo novo ou a resolução dinâmica atual. Nenhum container/imagem preexistente foi removido.

Não há DT_NEEDED de libpng, libjpeg, libwebp ou fontconfig. A combinação de símbolos internos, configuração oficial e ausência de dependência ELF sustenta incorporação estática dos codecs identificados. Atualizar um pacote Ubuntu libpng não substitui automaticamente o libpng dentro deste `.so`.

## 7. Codecs estáticos

O [build Linux da revisão do pacote](https://github.com/mono/SkiaSharp/blob/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/native/linux/build.cake) desabilita uso das versões de sistema de libjpeg-turbo, libpng, libwebp e zlib. O [DEPS exato](https://github.com/mono/skia/blob/c2d78902a56e24827a7273a4ed3461ce18780680/DEPS) fornece revisões, não prova sozinho que todo componente listado foi ligado.

| Componente | Presença / versão verificável | Alcance no avatar |
| --- | --- | --- |
| JPEG / libjpeg-turbo | Estático; `SkJpegCodec`, `jpeg_read_header`; string explícita **3.1.4.1** e revisão `9217719d3a58633923b096af4c1d50d304768a64`, correspondente à release oficial | Cabeçalho e pixels JPEG controlados pelo usuário |
| PNG / libpng | Estático; `SkPngCodec`, `png_process_data`, `png_read_end`; string **1.6.58** corroborada por `PNG_LIBPNG_VER_STRING` do pin `3061454d980de7d53608f594194cfac722721d2a` | Cabeçalho, chunks e pixels PNG; leitor progressivo |
| WebP / libwebp | Estático; símbolos `WebPDecode`, `VP8LDecode`, `WebPEncode`; fonte fixada em `4fa21912338357f89e4fd51cf2368325b59e9bd9`, cujo configure.ac declara **1.6.0** | Entrada WebP e codificação de toda saída |
| GIF / Wuffs | Símbolos `SkGifDecoder`, `wuffs_gif__decoder`; versão interna exata **desconhecida**; pin de fonte Wuffs `e3f919ccfe3ef542cfc983a82146070258fb57f8` | Possível seleção e leitura inicial antes da rejeição por formato; pixels GIF não são solicitados |
| BMP, ICO, WBMP | Símbolos `SkBmpCodec`, `SkIcoCodec`, `SkWbmpCodec`; código Skia da revisão acima, sem versão independente demonstrada | Possível análise inicial; rejeição antes de GetPixels |
| zlib e outros auxiliares | Evidência de inflate e pin zlib no DEPS; inventário individual completo e versão efetiva **não demonstrados** | Descompressão auxiliar PNG é plausível; outros consumidores não estabelecidos |

Para WebP, **1.6.0 é a versão declarada da fonte vinculada à proveniência do pacote**; não foi obtido getter de versão do binário nem execução independente que confirme a versão interna. Mantém-se essa limitação, em vez de apresentar o número como medição em runtime. JPEG/PNG têm também fingerprint no próprio artefato. Fontes: [JPEG release 3.1.4.1](https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1), [png.h fixado](https://skia.googlesource.com/third_party/libpng/+/3061454d980de7d53608f594194cfac722721d2a/png.h), [WebP configure.ac fixado](https://chromium.googlesource.com/webm/libwebp/+/4fa21912338357f89e4fd51cf2368325b59e9bd9/configure.ac).

Não foram encontrados os padrões estreitos procurados de AVIF/JPEG XL/RAW; isso não prova ausência absoluta. Não se declara acesso a todos os formatos que Skia suporta. Não foram determinadas versões de FreeType/Expat ou uma lista completa dos auxiliares incorporados.

O SkPngCodec examinado declara não suportar APNG; a fonte libpng fixada mantém handlers acTL/fcTL/fdAT nulos, sem demonstrar o fork libpng-apng. Portanto a condição FrameCount > 1 não deve ser apresentada como detecção garantida de toda entrada APNG: confirmar no B06-C2 se ela é rejeitada ou tratada como PNG estático. O reencode de um bitmap continua produzindo uma imagem estática.

## 8. Fluxo de processamento de avatar

Fontes locais: `Controllers/ProfileController.cs`, `Services/Avatars/AvatarImageProcessor.cs`, `AvatarProfileService.cs`, `SupabaseAvatarStorageService.cs`, `RateLimiting/AbuseProtection.cs` e ordenação do middleware em `Program.cs`.

1. PUT `/api/profile/avatar`, autenticado. Limite HTTP **5 MiB + 64 KiB** e multipart **5 MiB**. Identidade/usuário são verificados antes da substituição.
2. Rejeita arquivo ausente/vazio, tamanho declarado acima de **5 MiB** e MIME declarado diferente de JPEG/PNG/WebP. MIME é informação do cliente.
3. Copia o stream inteiro para memória, limitando os bytes efetivamente lidos. ReadAsync e espera no semaphore recebem CancellationToken. O buffer é criado **antes** do gate de decodificação.
4. Gate estático com **2 permissões**. `buffer.ToArray()` e `SKData.CreateCopy` criam cópias adicionais.
5. **`SKCodec.Create` entrega os bytes ao parser nativo antes de verificar formato real, dimensões e animação.** EncodedFormat precisa coincidir com o MIME permitido. Formatos não aceitos podem ser reconhecidos e parcialmente analisados nessa construção.
6. Lê Info; rejeita lados fora de 1..8192 e produto acima de **16.000.000 pixels**. Consulta FrameCount e rejeita mais de um frame. Essas medidas precedem a alocação do bitmap e GetPixels, mas seguem análise nativa inicial.
7. Aloca bitmap RGBA8888 e chama **GetPixels**, exigindo Success. Um bitmap no teto corresponde a aproximadamente **64 MB decimais**, sem contar buffers, estado dos codecs ou cópias; não é medição de pico do processo.
8. Trata orientação EXIF, escala proporcional sem ampliar, lado máximo **1024**, desenha em bitmap novo usando amostragem linear. Não há HTML/SVG/PDF, fonte, shader ou path fornecido pelo usuário nesse fluxo.
9. Verifica cancelamento antes do encode; codifica imagem nova em **WebP qualidade 85**, limita saída a 5 MiB. Não reutiliza o arquivo original. O reencode e a transferência de pixels evitam copiar diretamente chunks/metadados originais; não se afirma remoção exaustiva de todo tipo de metadado sem inspeção da saída.
10. `using` descarta SKData, SKCodec, bitmaps, canvas, imagens e dados codificados; finally libera o gate. GetPixels, desenho e Encode são síncronos nativos, sem cancelamento preemptivo por token. Uma falha nativa fatal pode encerrar o processo apesar dessas construções.
11. Só após processamento completo `AvatarProfileService` envia os bytes validados ao Storage, depois efetua compare-exchange da referência e limpeza controlada. Conflitos não substituem silenciosamente outra atualização. Storage usa `image/webp`, caminho novo por usuário e `x-upsert=false`; falhas têm limpeza com timeout separado. Nenhuma chamada real a Storage foi feita nesta etapa.

Rate limiting efetivo no código: middleware após autenticação; padrão global **8 concorrentes/240 por minuto**; política Avatar **1 concorrente na categoria inteira** e **4 por minuto por identidade**, filas `QueueLimit=0`. Valores são configuráveis; configuração efetiva remota não foi consultada. O semaphore não limita a fila por si só, mas não se conclui fila HTTP ilimitada: o limite externo de categoria padrão restringe o endpoint a uma execução por processo. Outros chamadores diretos e múltiplas réplicas precisam ser considerados separadamente.

## 9. Superfície de ataque

Usuário autenticado pode controlar bytes, MIME, compressão, dimensões declaradas, chunks, orientação e entradas inválidas de até 5 MiB. Os primeiros parsers são alcançados em SKCodec.Create; libjpeg/libpng/libwebp recebem conteúdo antes da conclusão da validação. A restrição de formatos reduz GetPixels aos três formatos permitidos, mas não elimina probing de outros decoders registrados.

Cabeçalhos/chunks e perfis de cor também merecem atenção; limites de pixels não limitam diretamente custo de toda análise de metadados. As transformações de desenho são calculadas pela aplicação e limitadas por dimensões, não recebidas como programas/path arbitrários. O processamento usa canvas sobre bitmap, **sem criação de contexto GPU**.

Autenticação, quotas, dimensões, descarte de animação e reencode reduzem exposição/recursos; não corrigem corrupção de memória de um parser. Não foi demonstrado exploit, execução remota, vazamento ou incidente. A hipótese de DoS por custo nativo não cancelável é uma avaliação técnica, não uma CVE nova.

## 10. Advisories e vulnerabilidades

Consultas de 24/11/2026: GitHub Advisory Database para NuGet SkiaSharp, APIs de advisories dos mantenedores SkiaSharp/libpng/libjpeg-turbo/libwebp, feed público NuGet, OSV por pacote/versão, notas oficiais SkiaSharp e fontes Skia/Chromium. APIs dos repositórios SkiaSharp/libjpeg-turbo/libwebp retornaram listas vazias; libpng retornou advisories. NVD foi consultada para CVE-2026-46675, mas sem conteúdo legível nessa ferramenta; não se atribui a ela score não obtido.

Feed NuGet: base atualizada em **2026-11-20T05:34:20.7684268Z**, delta em **2026-11-24T11:34:46.1586875Z**. Para SkiaSharp, a base relacionou somente GHSA-j7hp-h8jx-5ppr, faixa `[2.0.0,2.88.6)`; nenhum registro correspondente no delta ou para NoDependencies. OSV query `NuGet/SkiaSharp@4.155.0` retornou `{}`. São resultados de cobertura de feeds, não exclusão dos achados abaixo. [Índice NuGet](https://api.nuget.org/v3/vulnerabilities/index.json), [advisory WebP](https://github.com/advisories/GHSA-j7hp-h8jx-5ppr).

Classificação: **CONFIRMADO** quando componente e versão vulnerável são demonstrados; **POTENCIAL** quando há relação técnica mas falta versão/alcance; **NÃO APLICÁVEL** quando há exclusão suficiente; **INDETERMINADO** quando não foi possível estabelecer aplicabilidade. A coluna de alcance não deve ser confundida com presença de componente afetado.

| Identificador / componente | Severidade e fonte | Faixa afetada → correção | Avaliação |
| --- | --- | --- | --- |
| CVE-2026-46675 / GHSA-qvg3-h654-xq3j, libpng | Moderate, CVSS 3.1 **5.9**, advisory do mantenedor | 1.6.0..1.6.58 → **1.6.59** | **CONFIRMADO no componente/versão**; sequência vulnerável **NÃO APLICÁVEL ao leitor progressivo examinado**. Ver seção 11 |
| CVE-2023-4863 / GHSA-j7hp-h8jx-5ppr, libwebp | High, CVSS 3.1 **8.8**, GitHub Reviewed | SkiaSharp >=2.0.0,<2.88.6 → 2.88.6; libwebp corrigido em 1.3.2 | **NÃO APLICÁVEL** à faixa NuGet 4.155.0; fonte WebP 1.6.0 corrobora. Não é inferência baseada só em data |
| CVE-2026-126871, Skia Graphite clip | Correção de segurança, mantenedor SkiaSharp; score não extraído | Backport documentado em **4.155.2** | **NÃO APLICÁVEL ao avatar GPU**: esse fluxo não cria contexto Graphite. Presença exata desse código no asset não foi individualmente comprovada |
| CVE-2026-126882, Skia Ganesh rounded rectangles | Correção de segurança, mantenedor SkiaSharp; score não extraído | Backport em **4.155.2** | **NÃO APLICÁVEL ao fluxo GPU Ganesh** examinado |
| CVE-2026-118340, IDs de image filters | High, notas SkiaSharp | Backport em **4.155.2** | **POTENCIAL no motor** anterior ao backport; não há criação de image filters no código de avatar; alcance interno não traçado integralmente |
| CVE-2026-119261, SkSL invalid types | High, notas SkiaSharp | Backport em **4.155.2** | **NÃO APLICÁVEL à entrada de SkSL pelo avatar**: não recebe/compila programa do usuário; não se generaliza para outros usos do pacote |
| CVE-2026-121086, region-builder OOB write | High, notas SkiaSharp | Backport em **4.155.2** | **POTENCIAL no motor**; avatar não aceita paths/regiões arbitrários; falta prova do caminho interno necessário |
| CVE-2026-126912, analytic AA blitter heap overflow | High, notas SkiaSharp e CNA Chrome | Backport em **4.155.2** | **POTENCIAL no motor/raster**; não demonstrado disparo pelo DrawImage/transformações limitadas do avatar |

A [release 4.155.2](https://mono.github.io/SkiaSharp/docs/releases/4.155.2.html) associa explicitamente os seis IDs a backports, mantendo APIs gerenciadas, C e ABI. Isso sustenta investigar a atualização; não fornece uma faixa NuGet formal para cada CVE nem prova exploração no SeenIt. O [registro CNA de CVE-2026-126912](https://www.cve.org/CVERecord?id=2026-126912) descreve Chrome anterior a 157.0.8103.78; **números de versão Chrome não são faixas de SkiaSharp**.

Outros advisories libpng consultados, todos **NÃO APLICÁVEIS à versão 1.6.58**, por faixa/fix explícitos dos mantenedores:

| CVEs | Severidade do mantenedor | Faixa / primeira correção |
| --- | --- | --- |
| CVE-2025-64505, CVE-2025-64506 | Medium | versões anteriores a 1.6.51 → 1.6.51 |
| CVE-2025-64720, CVE-2025-65018 | High | 1.6.0..<1.6.51 → 1.6.51 |
| CVE-2025-66293 | High | 1.6.0..<1.6.52 → 1.6.52 |
| CVE-2026-22695 | Medium | 1.6.51..1.6.53 → 1.6.54 |
| CVE-2026-22801 | Medium | 1.6.26..1.6.53 → 1.6.54 |
| CVE-2026-25646 | High | até 1.6.54 → 1.6.55 |
| CVE-2026-33416 | High | 1.2.1..1.6.55 → 1.6.56 |
| CVE-2026-33636 | High | 1.6.36..1.6.55 → 1.6.56; adicionalmente ARM Neon difere de amd64 |
| CVE-2026-34757 | Medium | getter/setter afetado desde 1.0.9; correção explícita em 1.6.57, apesar de faixa genérica aberta na API |
| CVE-2026-40930 / GHSA-c4v6-gxrq-6g2x | Medium | trunk 1.8.0 e fork libpng-apng; correção trunk faf0692468; não o libpng 1.6.58 padrão identificado |

Fontes: [advisories oficiais libpng](https://github.com/pnggroup/libpng/security/advisories), [GHSA APNG](https://github.com/pnggroup/libpng/security/advisories/GHSA-c4v6-gxrq-6g2x), [histórico oficial de releases e correções](https://www.libpng.org/pub/png/libpng.html). Não há achado específico confirmado de JPEG nesta pesquisa; isso não certifica ausência de vulnerabilidades, especialmente nos auxiliares com versão desconhecida.

## 11. Evidências de aplicabilidade

**Libpng CVE-2026-46675.** PNG 1.6.58 aparece no binário e no header da fonte fixada. A fonte `pngrutil.c` fixada conserva a liberação de zowner sem o novo detach nos pontos relevantes; foi comparada ao [patch oficial aa77ef3](https://github.com/pnggroup/libpng/commit/aa77ef3). Não há evidência de backport desse patch na revisão examinada. O `.so` contém inclusive símbolo `png_read_end`, mas presença não comprova chamada.

O [advisory](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j) exige leitor sequencial que chama png_read_end antes de iniciar leitura das linhas e exclui expressamente o leitor progressivo. O [SkPngCodec da revisão exata](https://github.com/mono/skia/blob/c2d78902a56e24827a7273a4ed3461ce18780680/src/codec/SkPngCodec.cpp) usa png_set_progressive_read_fn/png_process_data, sem chamada png_read_end; o binário contém seus símbolos, sem evidência de SkPngRustCodec. Assim, a versão afetada está presente, mas **essa exploração específica não é aplicável à rota PNG progressiva examinada**. O impacto descrito pelo mantenedor é principalmente crash, não divulgação/corrupção dos bytes descomprimidos. Não foi realizado PoC.

**Skia.** Comparação oficial entre o submódulo atual e o de 4.155.2 mostra alterações em SkImageFilter, SkRegion_path, SkScan_AAAPath, parser SkSL e ClipStack Graphite. As correções existem após o source pin atual; isso não basta para declarar todos esses componentes vulneráveis alcançáveis no asset Linux. As exclusões de GPU/entrada SkSL são relativas ao fluxo lido. Casos de raster/region/filter permanecem potenciais até rastreamento ou teste isolado dirigido. Não se converte a descrição de RCE via HTML no Chrome em uma afirmação de RCE via JPEG/PNG/WebP no SeenIt.

**Lacunas.** GIF/Wuffs e auxiliares com versão não medida ficam **INDETERMINADOS** quanto a advisories individuais. Headers de formatos recusados constituem hipótese de alcance parcial, não prova de decodificação completa. Não foram aplicadas supressões ou alterados feeds/ignores.

## 12. Atualizações disponíveis

Índices públicos NuGet consultados para os dois pacotes: versões estáveis posteriores **4.155.1, 4.155.2, 4.156.0, 4.156.1, 4.156.2 e 4.157.0**. A maior estável observada é 4.157.0; há prereleases 4.158.0/4.159.0, que não são candidatas estáveis equivalentes. [Índice SkiaSharp](https://api.nuget.org/v3-flatcontainer/skiasharp/index.json), [índice NoDependencies](https://api.nuget.org/v3-flatcontainer/skiasharp.nativeassets.linux.nodependencies/index.json).

| Alternativa de versão | Mudanças e compatibilidade a verificar |
| --- | --- |
| 4.155.1 | Atualiza revisão m155; sem mudança de API gerenciada declarada. Não é por si só evidência de correção dos seis IDs de 4.155.2 |
| **4.155.2** | Seis backports de segurança explícitos; sem mudança declarada de API/ABI; menor salto de milestone. Preferida para experimento |
| 4.156.2 | m156; correções de amostragem filtrada/glyphs; maior mudança do motor |
| 4.157.0 | m157; mudanças de raster, filtros, GPU e calling convention; requisitos Metal iOS/tvOS específicos não se aplicam ao alvo Linux, mas não provam equivalência de comportamento |

Fontes: [4.155.1](https://mono.github.io/SkiaSharp/docs/releases/4.155.1.html), [4.155.2](https://mono.github.io/SkiaSharp/docs/releases/4.155.2.html), [4.156.2](https://mono.github.io/SkiaSharp/docs/releases/4.156.2.html), [4.157.0](https://mono.github.io/SkiaSharp/docs/releases/4.157.0.html). Datas de cabeçalho das notas e publicação GitHub podem diferir; não são timestamps de build local.

SkiaSharp atual possui assets net10.0, e as linhas candidatas têm pacotes NativeAssets correspondentes publicados. Não foi realizado restore/build/carregamento de candidata em .NET 10/linux-amd64; compatibilidade operacional permanece requisito de B06-C2, não resultado aprovado.

**Importante:** DEPS de **4.155.2** (Skia `e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c`) e **4.157.0** (Skia `51cb594d773b5383a45065ea3c8cbec398ad2c36`) mantêm exatamente os pins atuais de JPEG, PNG e WebP. Portanto não se declara que essas versões corrigem a CVE-2026-46675. A confirmação de qualquer correção de codec exige revisão/patch ou novo binário verificável, não apenas versão NuGet superior.

## 13. Matriz de risco

Classificação qualitativa local, sem representar CVSS novo ou estimativa de probabilidade de exploração.

| Risco | Evidência / mitigação atual | Residual e prioridade |
| --- | --- | --- |
| Corrupção de memória por decoder | Entrada externa autenticada; código nativo; tamanho/pixels/gate | Impacto potencial alto; nenhum exploit demonstrado. P2: manter acompanhamento e ensaiar manutenção |
| Libpng CVE-2026-46675 | Componente afetado; leitor progressivo excluído pelo advisory | Risco do componente confirmado, alcance específico excluído. P2 documental, revisar se leitor mudar |
| Backports Skia ausentes | Seis correções oficiais; fluxo não usa GPU/SkSL do cliente | Risco específico de raster não resolvido por revisão completa. P2: 4.155.2 coordenado em laboratório |
| Expansão e memória | 16 MP antes de GetPixels, 5 MiB de entrada, saída 1024 | Um bitmap pode ter 64 MB mais overhead; falta medição de pico. P2: medir RSS sob limites |
| Arquivos malformados/metadados | SKCodec.Create antes de formato/dimensões, frames consultados antes de decode | Headers ainda podem custar CPU/memória. P2: corpus pequeno e dirigido |
| Concorrência/cancelamento | Gate 2; categoria Avatar padrão 1 sem fila; tokens na leitura/espera | Native calls não preemptivas; quotas por processo, configuráveis. P2: medir timeout e recuperação |
| Liberação de memória | using/finally em caminhos gerenciados | Crash nativo não é contido; ausência de leak não demonstrada por teste prolongado. P2: repetição limitada |
| Codecs/auxiliares desconhecidos | Símbolos/fontes parciais; Scout não inventaria todos | INDETERMINADO para revisões não comprovadas. P2: completar inventário sem atribuir CVEs por nome |
| Armazenamento de entrada maliciosa | Só bytes reencodados após validação são enviados | Falha de parser ocorre antes do Storage; reencode não protege o decoder. P2: preservar contrato |

## 14. Alternativas de correção

Nenhuma implementada.

| Alternativa | Benefício / risco / compatibilidade | Mudanças, testes e rollback |
| --- | --- | --- |
| A. Manter 4.155.0 | Comportamento historicamente validado; conserva motor anterior aos backports e dívida de inventário | Sem mudança; reavaliar advisories. Rollback não necessário |
| B. Atualizar só SkiaSharp | Pode atualizar binding, mas deixar native Linux antigo; benefício nativo não garantido e possível incompatibilidade | Não recomendada. Exigiria prova explícita de matriz ABI e testes nativos; voltar referência anterior |
| **C. Atualizar SkiaSharp + NoDependencies juntos** | Se aplicada a 4.155.2, incorpora correções documentadas sem salto de milestone; regressões de decode/encode/RSS continuam possíveis | Candidata recomendada somente para B06-C2. Fixar ambos 4.155.2 em cópia isolada; executar seção 15; rollback de ambos 4.155.0 e imagem controle |
| D. Reforçar validações | Pode antecipar rejeição de assinatura/formato ou impor orçamento adicional; assinatura sozinha não valida imagem e duplicar parsers cria risco | Somente se testes demonstrarem lacuna concreta; preservar formatos/orientação/erros e medir custo; rollback da mudança isolada |
| E. Isolar processamento | Processo separado pode conter crash e impor timeout/memória mais fortes; acrescenta IPC, operação, latência e limpeza | Investigar se risco/medições justificarem; testes de morte/timeout/recuperação, sem produção; rollback para caminho atual avaliado |

Escolha C motivada por correções de segurança publicadas e menor mudança do motor; não por mera existência de versão mais recente. Não se recomenda substituir o codec ou compilar pacote customizado nesta etapa. A atualização continua sujeita à evidência de regressão e preserva a limitação PNG identificada.

## 15. Plano de testes B06-C2

Plano apenas; nenhuma execução com corpus malicioso nesta etapa.

1. Criar cópia temporária isolada do mesmo HEAD, controle 4.155.0 e candidata coordenada 4.155.2. Preservar bases SDK/ASP.NET por digest, código, configurações e testes; restore/output somente nessa cópia. Registrar árvore, versões, timestamps, SHA-256 e imagem por digest.
2. Comparar csproj, assets, deps, TFM/RID e conteúdo nativo. Build **linux/amd64**, carregamento de SkiaSharp/.so, versão do motor, ELF/ldd sem `not found`; comparar API/checks byte a byte no mesmo estágio final. Não misturar binding candidato e native controle.
3. Docker build final obrigando verify; usuário `app` não-root, health na porta **10000**, marker/CA, sem SDK no estágio final. Reexecutar AvatarChecks, CoreChecks, integração e EntryPointChecks, incluindo JWT/HTTPS B06-A.
4. JPEG/PNG/WebP válidos, transparência, EXIF nas oito orientações, tamanhos pequenos e limites; resize proporcional até 1024 sem ampliação, saída WebP, qualidade/tolerância visual e limite de bytes. Inspecionar chunks de saída e payload anexado; não exigir byte idêntico entre encoders sem justificativa.
5. Arquivo vazio, truncado, corrompido e malformado; MIME falso, assinatura de formato recusado e dupla extensão. Separar rejeição antes de native, rejeição por Create e por GetPixels. GIF/BMP/ICO/WBMP com MIME permitido devem ser recusados sem aceitar pixels desses formatos.
6. WebP animado/APNG e arquivo de um frame; lados 8192/8193, área 16.000.000 e acima, entrada 5 MiB/acima e saída máxima; verificar erro contratual e ausência de upload/referência modificada.
7. Concorrência HTTP com quotas atuais, invocação direta do gate, cancelamento durante leitura/espera e durante trabalho nativo. Medir RSS/CPU/tempo, recuperação após cancelamento e liberação do gate, sem afirmar interrupção preemptiva do native. Repetição limitada para detectar tendência de retenção, sem fuzzing de carga.
8. Testes dirigidos às correções Skia somente quando o caminho relevante for estabelecido; corpus de advisory libpng deve respeitar exclusão progressiva. Eventual harness sequencial separado prova o advisory, não automaticamente o endpoint. Não executar PoC baixado ou script desconhecido.
9. Entradas potencialmente perigosas somente em container/processo isolado, limites explícitos de memória/CPU/PIDs/tempo, sem credenciais, rede de produção ou volumes sensíveis. Ajustar orçamento a partir de medição de controle. Parar em crash/OOM e preservar diagnóstico sem reinício descontrolado.
10. Aceite: todos os contratos/regressões existentes passam; ldd íntegro; sem novo crash/leak/retensão relevante no corpus limitado; relatório explicita versões/patches comprovados e lacunas. Falha implica rejeitar candidata e manter controle. Nenhum push, PR, deploy ou promoção faz parte desse experimento por inferência.

## 16. Limitações

- Docker Engine indisponível; sem build, novo ldd, smoke ou mapas de bibliotecas nesta execução. O artefato estático é o preservado B06-B3, com hash novamente conferido.
- Sem acesso a produção, configuração remota efetiva, métricas reais ou Storage. Igualdade com produção não foi verificada nesta etapa.
- Sem restore, instalação de ferramentas, alteração de lockfiles, fuzzing ou execução de código de terceiros. Getters inferidos estaticamente não foram invocados.
- Pin de fonte e strings não equivalem a atestação reproduzível de cada instrução. Libwebp tem versão de fonte identificada, sem confirmação por getter; GIF/auxiliares mantêm versão interna desconhecida.
- O inventário de CVEs é limitado às consultas registradas, não prova exaustividade. Não houve fechamento integral de call graph, sanitizers ou exploração dirigida das correções de raster.
- Notas de manutenção podem mudar. Ausência de associação a NuGet nos feeds não elimina CVEs nativas; valores CVSS publicados para Chrome não foram tratados como score específico do SeenIt.

## 17. Recomendação técnica

Prosseguir, em futura etapa **B06-C2**, com experimento isolado da alternativa **C: SkiaSharp e NativeAssets Linux NoDependencies 4.155.2 coordenados**, contra controle 4.155.0. O motivo é a existência de backports de segurança documentados, mantendo m155 e API/ABI declaradas, com menor mudança do que m157. A promoção depende dos resultados da seção 15 e de decisão posterior; nada foi atualizado agora.

Manter o achado libpng 1.6.58 no inventário: sua versão está na faixa CVE-2026-46675, embora o caminho progressivo examinado exclua a condição vulnerável. Não anunciar que a candidata remove essa CVE: os pins PNG de 4.155.2/4.157.0 continuam iguais. Investigar nova revisão/patch do fornecedor se a política exigir remoção do componente afetado ou se o fluxo de leitura mudar.

Prioridades complementares: confirmar versões auxiliares, medir pico e cancelamento sob orçamento e examinar os caminhos de raster ainda potenciais. Isolamento adicional é alternativa condicionada a essa evidência, não correção aplicada nem emergência baseada em exploração comprovada.

## 18. Preservação do repositório

Única criação no repositório: **`docs/B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md`**. Fontes, csproj, Dockerfile, Program.cs, JWT, frontend, SDK, NuGet e arquivos de configuração foram preservados. Nenhuma migration, commit, stage, push, PR ou deploy; nenhuma mudança em Render/Vercel/variáveis remotas; nenhuma exclusão/reset/clean/rebase/force.

Verificação final realizada antes da entrega: comparação SHA-256 de todos os **240 arquivos preexistentes** da árvore principal e **161 da maintenance-phase1**, além de HEAD/status da manutenção, refs de branches e lista de worktrees: todos preservados. `git diff --check` aprovado; status principal exclusivamente `?? docs/B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md`. Snapshot inicial SHA-256 `1c56df2b530fb48623d49b4251d789828b92d9459b0895332cc76b0faa7b3067`. Não foram alterados recursos Docker históricos. Evidências auxiliares novas estão somente em TEMP e não integram a entrega versionada.

**B06-C1 DIAGNÓSTICO CONCLUÍDO** — investigação documentada com limitações explícitas; não constitui certificação de ausência de vulnerabilidades ou aprovação de atualização.
