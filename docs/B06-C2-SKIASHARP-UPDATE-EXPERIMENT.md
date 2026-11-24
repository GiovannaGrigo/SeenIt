# B06-C2 — Experimento isolado de atualização do SkiaSharp

Data: **24/11/2026**, America/Sao_Paulo. Referência: `400a0dfa74109723336fd9de89d119d5a7bb4284`.

**B06-C2 CANDIDATA APROVADA LOCALMENTE**, para a comparação limitada documentada abaixo. Nenhuma atualização permanente aplicada; sem commit, push, PR ou deploy. A aprovação não significa ausência de vulnerabilidades nem correção da CVE libpng.

## 1. Objetivo

Comparar controle SkiaSharp/NativeAssets Linux NoDependencies **4.155.0** e candidata **4.155.2**, com o mesmo código, Dockerfile, digests, configurações e testes. Separar evidência de scanner, correções na fonte e comportamento funcional. Todos os resultados apresentados como novos foram executados nesta etapa.

## 2. Estado inicial do Git

Branch `main`; HEAD e referência local `origin/main` em `400a0dfa74109723336fd9de89d119d5a7bb4284`. Única pendência inicial: `?? docs/B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md`. Sem fetch ou atualização automática de main.

Worktrees: principal e `.worktrees/maintenance-phase1`. A manutenção continuou em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seis fontes modificadas e seus diretórios/arquivos não rastreados anteriores. Branches locais registradas: main, maintenance-phase1, b05-b1-angular-security, fix/guest-route-guard, security/b05-b2-dependency-review, security/b05-b3-sqlite-review, security/b05-b4-container-review, security/b06-a1-hardening-diagnosis e security/b06-b4-docker-runtime.

Snapshot inicial: **241 arquivos** da árvore principal, incluindo B06-C1, e **161** da manutenção; SHA-256, HEAD, status, refs e worktrees. Arquivos ignorados não compõem inventário integral do disco. `repository-before.json` SHA-256: `b9846d7cef85b9429d467f371415c14d2aa9012e30f1008bfad2d199a327c22a`.

## 3. Referências B06-C1

O [B06-C1](B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md) foi lido integralmente. Mantidas suas distinções entre versão interna, versão de fonte, componente afetado e alcance no avatar. Referências históricas: [B06-B2](B06-B2-DOCKER-RUNTIME-EXPERIMENT.md), [B06-B3](B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md), [encerramento Docker](B06-B-FINAL-SUMMARY.md) e [encerramento HTTPS](B06-A-FINAL-SUMMARY.md).

O avatar continua com entrada até 5 MiB, requisição até esse valor +64 KiB, MIME JPEG/PNG/WebP confrontado com formato real, lados até 8192, área até 16 milhões de pixels, rejeição de FrameCount >1, saída WebP até 1024 no maior lado. SKCodec.Create antecede verificações de dimensões/animação; gate interno de dois decoders; categoria HTTP Avatar padrão com uma execução concorrente. CancellationToken não interrompe preemptivamente GetPixels/Encode. Nenhuma regra foi modificada.

## 4. Metodologia e isolamento

Área exclusiva: **`%TEMP%/seenit-b06-c2-20261124-a4ce8526`**, fora do repositório. Duas cópias sanitizadas dos arquivos backend rastreados e `.dockerignore`, sem bin/obj locais, configurações appsettings do desenvolvedor, segredos, scripts locais ou frontend no contexto Docker. As fontes copiadas foram confrontadas com o estado inicial; nenhuma fonte funcional original tinha alteração. A CA pública e o runtime-settings versionados foram preservados. O frontend original não foi tocado.

Docker CLI/Engine **29.9.3**, Linux/x86-64; SDK fixado **10.0.404**, runtime final **10.0.15**. Docker estava disponível fora do sandbox; a primeira tentativa no sandbox retornou acesso negado. Não foi iniciado/reiniciado serviço do usuário. O filesystem do Engine reportou aproximadamente **929 GiB disponíveis**, suficiente para as duas construções; nenhuma limpeza realizada.

Builds com `--platform linux/amd64 --no-cache --progress plain`, mesmo Dockerfile, tags novas. Bases iguais:

- SDK: `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625`.
- ASP.NET: `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde`.

As imagens finais foram exportadas por containers temporários. Testes suplementares foram compilados no SDK fixado, separadamente para cada conjunto de pacotes, e executados **na respectiva imagem final**, usuário app, rede none. Integração usa SQLite em memória e doubles que proíbem HTTP externo; não foi usado `--postgres-only`. Nenhuma migration ou serviço real de banco/Storage.

Limites dos ensaios finais: **768 MiB, 2 CPUs, 256 PIDs e timeout de 180 s** por suíte. Smoke: 512 MiB, 1 CPU, 128 PIDs, espera de health limitada e shutdown de 10 s. Compilação dos harnesses: 4 GiB, 2 CPUs, 512 PIDs, timeout de 600/900 s. Sem porta publicada ou capabilities adicionais. Não houve PoC baixado ou fuzzing; o corpus foi gerado por código próprio e fixtures controladas.

Ferramentas ELF tradicionais não estavam disponíveis no SDK examinado; foi usado parser próprio de cabeçalhos/seções/símbolos com Node disponível, além de ldd no runtime. Não houve instalação de ferramentas.

## 5. Versões controle/candidata

| Identificação | Controle | Candidata |
| --- | --- | --- |
| SkiaSharp NuGet | 4.155.0 | **4.155.2** |
| NativeAssets Linux NoDependencies | 4.155.0 | **4.155.2** |
| TFM / RID | net10.0 / linux-x64 | Igual |
| AssemblyVersion gerenciado | 4.155.0.0 | **4.155.0.0, preservado pelo fornecedor** |
| FileVersion gerenciado | 4.155.0.0 | **4.155.2.0** |
| Motor via getters nativos | milestone 155, increment 0 | milestone 155, increment 0 |
| Source commit do pacote | d25e7fa628a2576e50ecc42004cc4fd03c6657f5 | bae2bd1738735d78625e81cc51c9e60240b6cbd5 |

AssemblyVersion/SONAME/getter iguais não são evidência de reaproveitamento do native antigo: os hashes, fileVersion, deps e proveniência abaixo distinguem os artefatos. InformationalVersion da candidata inclui 4.155.2 e o commit bae2bd1.

## 6. Diff experimental

Único arquivo fonte diferente entre as duas cópias: `backend/SeenIt.Api/SeenIt.Api.csproj`, exclusivamente duas linhas; BOM e finais de linha preservados após a preparação:

```diff
-    <PackageReference Include="SkiaSharp" Version="4.155.0" />
-    <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.155.0" />
+    <PackageReference Include="SkiaSharp" Version="4.155.2" />
+    <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.155.2" />
```

Program.cs, Dockerfile, validação de avatar, JWT, rate limiting, endpoints e testes permanentes idênticos. Harnesses adicionais iguais nos dois cenários; suas correções de infraestrutura são descritas na seção 17. Não houve alteração na aplicação para obter aprovação.

## 7. Dependências resolvidas

Restore/publish efetivamente executados pelos dois Docker builds e pelas compilações isoladas. `project.assets.json` e `SeenIt.Api.deps.json` confrontados. Diferenças de pacotes no grafo da API limitadas a:

| Pacote | Controle → candidata | Motivo |
| --- | --- | --- |
| SkiaSharp | 4.155.0 → 4.155.2 | Referência direta autorizada |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.155.0 → 4.155.2 | Referência direta autorizada |
| SkiaSharp.NativeAssets.macOS | 4.155.0 → 4.155.2 | Transitivo obrigatório do binding |
| SkiaSharp.NativeAssets.Win32 | 4.155.0 → 4.155.2 | Transitivo obrigatório do binding |

Nenhum outro pacote da API mudou. MacOS/Win32 aparecem no restore geral; o deps final linux-x64 seleciona NativeAssets Linux e não inclui essas entradas. Sem mistura de versões de pacote gerenciado/Linux. O native fileVersion 0.0.0.0 no deps é metadado genérico, não versão interna do motor.

Setenta arquivos em cada publicação final: **67 idênticos**, três diferentes: `SkiaSharp.dll`, `libSkiaSharp.so`, `SeenIt.Api.deps.json`. Inclusive `SeenIt.Api.dll` permaneceu igual: `63b81c9f6da07ec8ce27ada5fa78b3e8e948b9075911efdff822cc4d00506c88`.

Antes das execuções válidas, cada um dos quatro harnesses recebeu a publicação da respectiva imagem final; **todos os 70 arquivos** foram conferidos por hash em cada diretório. Os deps dos harnesses foram produzidos para o conjunto correspondente. SQLite de testes permaneceu exclusivamente nos diretórios dos harnesses.

## 8. Identidade das bibliotecas nativas

| Propriedade | Controle | Candidata |
| --- | --- | --- |
| libSkiaSharp.so bytes | 12.287.632 | **12.289.656** |
| SHA-256 native | `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e` | **`0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef`** |
| SHA-256 SkiaSharp.dll | `b462877e4dd46fc6487a7830a72955bdefd8e8c276cb95da96a484d655f4e95a` | `d1a77d5f16c95e42e58d05edea34019fe7cfcea3d10aa22e0a4d75b4a4a45dd8` |
| ELF | ELF64, little-endian, machine 62/x86-64 | Igual |
| SONAME | libSkiaSharp.so.155.0.0 | Igual |
| ldd no verify e smoke novo | Sem not found | Sem not found |

DT_NEEDED igual: libstdc++.so.6, libpthread.so.0, libdl.so.2, libm.so.6, libc.so.6, ld-linux-x86-64.so.2 e librt.so.1. Ldd resolve também libgcc_s.so.1 transitiva. Nenhuma dependência dinâmica separada de JPEG/PNG/WebP. Os pacotes Ubuntu e runtimes são iguais, incluindo libc6 2.39-0ubuntu8.12 e libstdc++6 14.2.0-4ubuntu2~24.04.1.

Foram baixados em TEMP, sem executar conteúdo, os dois nupkgs NativeAssets exatos do NuGet público. SHA-256 de cada asset linux-x64 interno **igual ao native da imagem respectiva**. Nupkg controle: `6372fc25265f3c354a59687d57486b9234e1969bbcd1ba951ab12505b791668d`; candidata: `bdf866f1adcb453769b00bd7f014d587caffca513d99e6d32392bc088cc00e15`. Nuspecs confirmam os commits da seção 5; não houve build reproduzível independente nem validação adicional de assinatura.

Getters foram chamados sobre `/app/libSkiaSharp.so` nos ensaios, e decodificação/encode realmente executados. Nenhuma TypeLoadException, EntryPointNotFoundException, DllNotFoundException de Skia ou falha ABI nas execuções finais. A falha SQLite inicial, descrita adiante, é distinta.

## 9. Codecs e versões

| Componente | Controle / candidata | Evidência e limite |
| --- | --- | --- |
| libjpeg-turbo | **3.1.4.1 / 3.1.4.1** | String explícita no binário e pin `9217719d3a58633923b096af4c1d50d304768a64` |
| libpng | **1.6.58 / 1.6.58** | Fingerprint binário e pin `3061454d980de7d53608f594194cfac722721d2a` |
| libwebp | **Fonte 1.6.0 / fonte 1.6.0** | Pin `4fa21912338357f89e4fd51cf2368325b59e9bd9`; símbolos decode/encode; sem getter interno confirmado |
| GIF/Wuffs | Presente / presente | Símbolos; mesmo pin `e3f919ccfe3ef542cfc983a82146070258fb57f8`; versão interna continua desconhecida |
| BMP/ICO/WBMP | Presente / presente | Símbolos Skia e rejeição de fixtures com MIME PNG |
| Outros auxiliares | Parcialmente identificados | Sem versão interna completa demonstrada; ausência de alerta não elimina lacuna |

Os DEPS foram consultados novamente nas revisões exatas [controle](https://github.com/mono/skia/blob/c2d78902a56e24827a7273a4ed3461ce18780680/DEPS) e [candidata](https://github.com/mono/skia/blob/e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c/DEPS). JPEG/PNG/WebP/Wuffs conservam os pins. A versão de fonte WebP não foi promovida a versão interna medida. Presença estática é sustentada por símbolos, fonte de build e ELF; não foi produzido SBOM completo de todos os objetos incorporados.

**CVE-2026-46675 separada:** não há evidência de correção do libpng na candidata. Mantida a classificação B06-C1: versão afetada presente, sequência específica vulnerável excluída pelo leitor progressivo examinado conforme [advisory do mantenedor](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j). Nenhum teste sequencial foi usado para atribuir vulnerabilidade ao avatar.

## 10. Evidências de backports de segurança

Cadeia: nupkg 4.155.2 → nuspec commit **bae2bd1738735d78625e81cc51c9e60240b6cbd5** → submódulo Skia **e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c** → [comparação com a revisão controle](https://github.com/mono/skia/compare/c2d78902a56e24827a7273a4ed3461ce18780680...e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c). Asset nativo da imagem corresponde byte a byte ao nupkg candidato.

| Correção oficial | Alteração de fonte observada |
| --- | --- |
| **CVE-2026-118340**, image-filter unique IDs | IDs/contador/cache passam de 32 para 64 bits em SkImageFilter e headers/cache |
| **CVE-2026-121086**, region-builder OOB | SkRegion_path amplia estimativa para subdivisões de cônicas e acrescenta verificações aritméticas |
| **CVE-2026-126912**, analytic AA blitter | SkScan_AAAPath limita intervalos e rejeita comprimentos/larguras não positivos |
| CVE-2026-119261, invalid SkSL types | Parser/declarações de variáveis rejeitam tipos inválidos |
| CVE-2026-126871, Graphite clip overflow | ClipStack contabiliza elementos pendentes e limita submissão |
| CVE-2026-126882, Ganesh geometry | GrOvalOpFactory corrige transformação/clamp dos raios |

Evidência adicional diretamente no ELF para o primeiro item: símbolo `_ZZL27next_image_filter_unique_idvE6nextID` tem **4 bytes no controle e 8 bytes na candidata**, coerente com o patch. A comparação completa foi salva em `skia-source-comparison.json`, e a inspeção em `backport-symbol-evidence.jsonl`.

As [notas oficiais 4.155.2](https://mono.github.io/SkiaSharp/docs/releases/4.155.2.html) vinculam os seis IDs aos backports e declaram API/ABI preservadas. Fonte, proveniência e binário oferecem evidência técnica da atualização; não foi exigido exploit. Não houve disassembly completo de cada um dos seis patches nem prova de exploração pelo endpoint. Exclusões GPU/SkSL e caminhos de raster potenciais do B06-C1 não foram convertidos em exploração confirmada. Scout não foi usado como prova desses patches.

## 11. Comparação de builds

| Execução nova | Controle | Candidata |
| --- | --- | --- |
| Build final sem cache | **exit 0; 77,78 s** | **exit 0; 69,26 s** |
| Restore e publish Release linux-x64 | Passaram | Passaram |
| NuGet audit API/AvatarChecks/CoreChecks | Sem pacotes vulneráveis reportados | Igual |
| Verify, cmp de native/binding, ldd | Passaram | Passaram |
| AvatarChecks | **16 grupos** | **16 grupos** |
| CoreChecks | Watch time, classificação/cache/retry e **72 casos HTTP** | Igual |
| CA no estágio final, como app | Passou | Passou |

Não foram identificados warnings de compilação nos dois Docker builds finais. Tempos de build incluem rede/cache externo de registros e concorrência do host; não são benchmark de execução da aplicação.

Tags novas e IDs locais:

- `seenit-api:b06-c2-control-a4ce8526`: `sha256:8b99d848dcbedaac1baacffb13d5c5f2b380e44a0da3c4f61c2504d5e04e690c`.
- `seenit-api:b06-c2-candidate-a4ce8526`: `sha256:2b73d2c6bdfb5d6adfbd62c6ee2d8d045efb7c1bf08de7cd28d0f1ef70221734`.

## 12. Testes de imagem

Mesmo corpus nos dois cenários; **37 registros suplementares** por execução final, incluindo medições/metadados, além dos 16 grupos AvatarChecks e três grupos ExtraAvatar.

| Caso | Controle e candidata |
| --- | --- |
| JPEG, PNG, WebP 32×16 e 1×1 | Aceitos; saída WebP válida, dimensões preservadas |
| Transparência e integridade de cores | Alpha transparente e região vermelha dentro das tolerâncias verificadas |
| EXIF orientações 1..8 | Fixture reconhecida; dimensões 40×20 ou 20×40 conforme rotação |
| 2048×1024 | Redimensionado para 1024×512 |
| 4000×4000 =16 MP | Aceito; saída 1024×1024 |
| 8192×1 | Aceito; saída 1024×1 |
| 8193×1 e 4001×4000 | InvalidAvatarException por dimensões/área |
| Vazio, acima de 5 MiB, corrompido, truncado | InvalidAvatarException com motivo correspondente |
| PNG com MIME JPEG / MIME text/plain | Rejeição por divergência / tipo não permitido |
| GIF/BMP/ICO/WBMP disfarçados de PNG | InvalidAvatarException por formato real divergente |
| WebP sintético com dois frames | ExtraAvatar confirmou dois frames e rejeição por animação |
| APNG sintético com acTL/fcTL/fdAT e dois frames declarados | **Aceito como PNG estático**, FrameCount 0; saída WebP estática 8×8 |
| Payload anexado, contratos Storage e substituição | Passaram em AvatarChecks |

Saídas estáticas WebP reportaram **FrameCount 0** em ambas as versões; não se exigiu artificialmente FrameCount=1. Mensagens, dimensões e tamanhos de saída observados coincidiram no corpus. Não se exigiu igualdade de bytes dos encoders. A inspeção visual foi automatizada por leitura de pixels/cor/alpha e dimensões; não foi uma revisão perceptual exaustiva ou comparação de todos os pixels nas oito orientações.

APNG aceito não é regressão introduzida pela candidata: o controle faz o mesmo, descartando animação na saída. Isso esclarece o contrato efetivo e deve ser documentado; rejeição estrita de qualquer APNG exigiria outro escopo. Limites de dimensão zero/malformação são cobertos pelas rejeições de parser existentes, sem inventar um decode bem-sucedido de imagem com dimensão inválida.

## 13. Testes de segurança e recursos

ExtraAvatar verificou gate de **duas permissões**, bloqueio quando ambas estão ocupadas, cancelamento de espera, recuperação e ausência de vazamento de permissões após rejeição por pixels. O corpus adicional confirmou cancelamento na leitura, recuperação posterior, quatro chamadas paralelas e gate retornando a 2.

No ensaio de cancelamento durante a janela de processamento de imagem 4000×4000, o cancelamento foi solicitado com gate ocupado; ambos terminaram com OperationCanceledException na verificação gerenciada. **Não há prova de preempção de instrução nativa nem de interrupção imediata de GetPixels/Encode.** O momento foi observado por gate, sem instrumentação de entrada/saída de cada chamada C.

Repetições limitadas: 24 operações 1024×1024, amostras após GC a cada quatro, mesmas condições por imagem:

| Medida | Controle | Candidata |
| --- | --- | --- |
| Pico WorkingSet do processo/corpus | **178,39 MiB** | **178,53 MiB** |
| WorkingSet primeira → última amostra | 62,46 → 64,98 MiB | 62,58 → 65,25 MiB |
| Mediana por repetição, inclui geração da fixture | **124,47 ms** | **129,05 ms** |
| CPU nas repetições e verificações finais | 3249,98 ms | 3357,49 ms |
| Tempo decorrido correspondente | 3060,92 ms | 3154,16 ms |

Pico aproximadamente 0,08% maior; mediana aproximadamente 3,68% maior. Não indicam regressão relevante neste ensaio curto, mas não estimam SLA ou garantem ausência de leak. O pico inclui geração de fixtures grandes e código de teste, não apenas AvatarImageProcessor. A retenção cresce poucos MiB nas duas séries; não foi interpretada como vazamento comprovado ou estabilidade de longo prazo.

Nenhum OOM/crash nativo da aplicação nas execuções finais válidas; containers terminaram exit 0/OOMKilled=false. Aborts iniciais por exceções gerenciadas dos harnesses foram interrompidos e preservados, conforme seção 17. Sem teste ofensivo de alta intensidade.

## 14. Testes da API

| Suíte executada na respectiva imagem final | Controle | Candidata |
| --- | --- | --- |
| RegisterIntegrationChecks | **exit 0** | **exit 0** |
| Cadastro real via TestServer/SQLite memória | **73 casos HTTP; 16 usuários sintéticos** | Igual |
| Catálogo, biblioteca, integridade e falhas externas simuladas | Passaram | Passaram |
| Login/perfil, JWT, spoofing, concorrência e rate limiting | Passaram | Passaram |
| Regressões permanentes: 20 logins sobrepostos, contabilização, isolamento, perfil, leases/cancelamento e CORS | Passaram com limite uniforme de 2 CPUs | Igual |
| EntryPointChecks | **174/174; 0 falhas** | **174/174; 0 falhas** |

EntryPointChecks: seis perfis, 72 requests da matriz, configuração ReverseProxy:HttpsRedirectionHandledByEdge ausente/false/true, com e sem porta HTTPS sintética. Testa token expirado, autorização, health, headers falsificados e simulação de TLS terminado no edge; true evita redirects adicionais, ausente/false preservam comportamento. Resultados em `control-entrypoint-results.json` e `candidate-entrypoint-results.json`.

AvatarChecks validou upload/delete/substituição simulados, compensação por falha/cancelamento e conflito, sem Storage real. A integração HTTP de cadastro/catálogo/biblioteca não é prova de persistência PostgreSQL ou acesso Supabase. Nenhum resultado histórico foi contado como execução nova.

## 15. Smoke e runtime

Smoke Kestrel novo nas duas imagens: Production; **HTTP 200 /health**, corpo `{"status":"ok"}`; binding interno `0.0.0.0:10000`; usuário **app UID 1654**; sem SDK na imagem; CA pública legível e não gravável, modo **0444**, 1367 bytes; marcador de verify presente; ldd íntegro; shutdown normal **exit 0**, sem OOM.

Rede none, Privileged=false, CapAdd=null e PortBindings vazio. Nenhuma publicação pública ou deploy. Configurações/segredos exclusivamente fictícios; os valores sintéticos fornecidos não apareceram nos logs verificados. Logs podem conter avisos conhecidos do ambiente sem proxy/armazenamento persistente de Data Protection; não houve erro inesperado de inicialização. Não se declara auditoria exaustiva de segredos.

## 16. Auditorias

Docker Scout instalado **1.28.0**, sem atualizar ferramenta. SARIF e SPDX gerados novamente para ambas as imagens, sem filtros/supressões. Scout indexou **176 pacotes** em cada cenário.

| Severidade | Controle | Candidata |
| --- | --- | --- |
| Critical / High | **0 / 0** | **0 / 0** |
| Medium / Low | **13 / 4** | **13 / 4** |
| Total | **17** | **17** |

Zero CVEs exclusivas de qualquer cenário; mesmas faixas/pacotes/correções reportadas. Todos os registros abaixo indicaram `not fixed` nessa consulta; isso não significa inexistência universal de patch fora do feed/distribuição:

| Pacote de origem / versão | CVEs, severidade |
| --- | --- |
| shadow 1:4.13+dfsg1-4ubuntu3.2 | CVE-2024-56433, Low |
| icu 74.2-1ubuntu3.1 | CVE-2025-5222, Low |
| gnupg2 2.4.4-2ubuntu17.6 | CVE-2026-105712, Low |
| systemd 255.4-1ubuntu8.18 | CVE-2026-40228 Low; CVE-2026-121876 Medium |
| dash 0.5.12-6ubuntu5 | CVE-2026-102473, CVE-2026-102474, Medium |
| tar 1.35+dfsg-3ubuntu0.4 | CVE-2026-18477, CVE-2026-18508, Medium |
| dpkg 1.22.6ubuntu6.6 | CVE-2026-2219, Medium |
| gnutls28 3.8.3-1.1ubuntu3.6 | CVE-2026-67693, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209, CVE-2026-95210, Medium |
| util-linux 2.39.3-9ubuntu6.6 | CVE-2026-76642, Medium |

Comparação dos pares nome/versão no SBOM: mudanças somente nos dois pacotes SkiaSharp e na identificação/tag da imagem. Não se atribui redução de alertas a este experimento: **17 → 17**. Scout continua sem inventário completo dos codecs estáticos; não comprova os seis backports nem correção PNG.

NuGet audit dos três projetos no Dockerfile e dos harnesses originais recompilados não reportou pacotes vulneráveis. Esse resultado de feed não elimina CVEs nativas discutidas nas seções 9/10. Sem apt upgrade, alteração de outra dependência da aplicação ou supressão.

## 17. Divergências e regressões

Ocorrências preservadas para não apresentar tentativas falhas como aprovação:

1. Primeiro script shell temporário tinha CRLF e falhou na sintaxe antes de compilar; normalizado para LF. Não alterou aplicação.
2. Primeira integração não encontrou `libe_sqlite3.so` no publish do harness. Foi acrescentado o asset Linux já preservado de SQLitePCLRaw 2.1.12, igual nos dois cenários, hash `b0f5c48026fedcf9f05cf31c6fadf9e1e001ca59cd85aa68b8b6814e52c18a07`. Somente infraestrutura de testes; native SQLite não entrou na imagem final. Não foi falha de carregamento Skia.
3. Integração controle limitada a uma CPU atingiu o prazo de 10 s da barreira de dois logins bloqueantes. O ensaio foi encerrado; a matriz final uniforme com duas CPUs passou nos dois cenários sem mudança de código. A limitação de uma CPU pode induzir starvation do ThreadPool do teste bloqueante; não se apresenta essa explicação como profiling conclusivo. Não é comparação suficiente para atribuir regressão à candidata.
4. EntryPointChecks executou os perfis, mas abortou ao escrever `/checks/results.json` sem permissão como app. Reexecutado copiando o mesmo harness para `/tmp/checks`, gravável pelo usuário app. Resultado final 174/174 em ambos; nenhum privilégio adicional.
5. Corpus novo inicialmente exigia FrameCount=1 para WebP estático; ambos retornam 0. A exceção gerenciada do próprio harness gerou exit 134; o ensaio foi encerrado e log preservado. Asserção corrigida para não animado (<=1), igual em ambos, sem relaxar a rejeição de WebP realmente animado. Não foi corrupção de memória nativa.
6. Uma tentativa de recompilar com --no-restore em container novo não tinha o cache efêmero de analyzers e falhou CS0006. Novo restore em cache temporário próprio, mesmas versões, permitiu recompilação. Nenhum pacote da aplicação foi substituído.

As falhas iniciais não foram apagadas nem contadas como testes aprovados. Nenhuma regressão funcional ou de recursos relevante foi observada nas execuções finais comparáveis. APNG aceito como estático é comportamento preexistente agora demonstrado.

## 18. Limitações

- Corpus sintético pequeno e execução curta; não representa todo arquivo malicioso, carga de produção, benchmark estatístico ou prova de ausência de leaks.
- Cancelamento nativo observado por janela do gate e check gerenciado; sem preempção comprovada.
- EXIF cobriu metadado e dimensões nas oito orientações, sem comparação exaustiva dos pixels espelhados/rotacionados. Cor/alpha verificados em regiões conhecidas.
- Não houve PoC de cada CVE; vínculo de patches usa notas, comparação de fonte, proveniência do pacote/hash do asset e evidência ELF adicional do contador de IDs. Não houve reprodução independente do build do fornecedor.
- WebP permanece com versão declarada de fonte; versões internas GIF/Wuffs/auxiliares parcialmente desconhecidas. Libpng não foi atualizado.
- Scanner pode ignorar objetos estáticos; o resultado NuGet/Scout não certifica segurança integral.
- Banco/Storage/proxy externos não foram acessados; somente SQLite memória, HTTP falso, TestServer e Kestrel local offline. Nenhum teste operacional em produção.
- O ensaio com uma CPU não passou na barreira bloqueante do controle; a aprovação aplica-se aos ensaios finais com orçamento documentado, não a qualquer configuração de recursos.

## 19. Matriz de aprovação

| Critério solicitado | Evidência / decisão |
| --- | --- |
| 1–2. Ambos 4.155.2, sem mistura | assets/deps/fileVersion, native igual ao asset candidato: **passou** |
| 3–6. Linux/amd64, verify, carregamento, ldd | Builds novos e runtime: **passou** |
| 7–10. Contratos/formats/limites/animações | AvatarChecks, ExtraAvatar e corpus; APNG estático igual ao controle: **passou no contrato efetivo** |
| 11–12. Crash/OOM e recursos | Execuções finais válidas exit 0, sem OOM; diferenças pequenas no ensaio limitado: **passou**, com limitações seções 13/17 |
| 13. Login/cadastro/perfil/catálogo/biblioteca | Integração completa exit 0 nos dois cenários: **passou** |
| 14. JWT/HTTPS | 174/174 por cenário, seis perfis: **passou** |
| 15. Non-root, porta e health | Dois smokes novos: **passou** |
| 16. Correções vinculadas a evidência técnica | Fonte/commit/pacote/asset e contador ELF: **passou**, sem afirmar exploração do endpoint |
| 17. Nenhuma mudança funcional necessária | Só duas referências diretas e dois transitivos SkiaSharp: **passou** |
| 18. Diferenças/limitações registradas | Seções 6–18: **passou** |

Decisão: **CANDIDATA APROVADA LOCALMENTE**, sem autorização implícita para implementar/promover e sem declarar libpng corrigido.

## 20. Estratégia de rollback futuro

Plano documental, não executado: numa implementação futura, reverter coordenadamente as duas referências para **4.155.0**. Restaurar/publicar com os mesmos digests e conferir native `26a5cf2c...ac850e`, deps e fileVersion controle; repetir verify, avatar, JWT/HTTPS e smoke antes de promoção. Manter imagem anterior por digest para reversão operacional. Não substituir somente binding ou somente native, nem sobrescrever tags históricas. Nenhum rollback remoto foi necessário neste experimento.

## 21. Recomendação para B06-C3

Uma etapa futura pode implementar **exclusivamente SkiaSharp e NativeAssets Linux NoDependencies 4.155.2 coordenados**, reproduzindo as duas linhas experimentais e os transitivos obrigatórios, com revisão do diff e regressões pertinentes. Manter SDK, ASP.NET, código e configuração. A razão é o conjunto de backports documentados com compatibilidade local demonstrada, não redução de alertas do scanner.

Preservar a observação APNG como comportamento estático preexistente e a CVE libpng como item separado. Rejeição estrita de APNG, isolamento adicional ou outras validações exigem avaliação própria. **Nenhuma implementação B06-C3 foi iniciada.**

## 22. Preservação do repositório

Única criação desta etapa no repositório: `docs/B06-C2-SKIASHARP-UPDATE-EXPERIMENT.md`. B06-C1 preservado. Conferência final de **241 hashes principais e 161 da manutenção**, HEAD/status da manutenção, refs e worktrees; nenhuma modificação funcional. Branch main e HEAD inalterados; status esperado e conferido somente os dois relatórios não rastreados.

Program.cs, Dockerfile, csproj originais, JWT, frontend, testes permanentes, configurações e migrations preservados. Nenhum commit/stage/push/PR/deploy/publicação Docker; nenhum acesso a banco/Storage de produção, alteração Render/Vercel ou variável remota. Nenhum reset, clean, rebase, force, stash, criação/exclusão de branch/worktree ou limpeza de cache/volumes.

Inventário Docker anterior: **18 registros de imagens e dois containers preexistentes**; comparação posterior confirmou todos preservados por referência/ID e estado dos containers. Novas imagens B06-C2 permanecem locais; somente containers criados para este experimento foram removidos ao terminar. Nenhum recurso histórico foi removido.

Evidências novas somente em TEMP: logs de build/restore/audit, imagens inspect, publicações, pacotes/nuspecs, graphs, ELF, comparação de fonte, estados de containers, JSON de testes, smokes e SARIF/SPDX. São evidências locais auxiliares, não anexadas ao Git. Índice de hashes em `evidence-hashes.json` na área experimental.

**B06-C2 CANDIDATA APROVADA LOCALMENTE**
