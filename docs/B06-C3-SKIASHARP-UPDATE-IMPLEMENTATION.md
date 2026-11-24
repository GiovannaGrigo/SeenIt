# B06-C3 — Implementação controlada do SkiaSharp 4.155.2

Data: **24/11/2026**, America/Sao_Paulo. Referência: `400a0dfa74109723336fd9de89d119d5a7bb4284`.

**B06-C3 IMPLEMENTAÇÃO VALIDADA**, exclusivamente local. Duas referências NuGet atualizadas; sem commit, push, PR, merge, deploy ou alteração de produção.

## 1. Objetivo

Aplicar ao csproj original a atualização coordenada SkiaSharp/SkiaSharp.NativeAssets.Linux.NoDependencies **4.155.0 → 4.155.2** e validar novamente a publicação do Dockerfile real. Nenhuma mudança em SDK, ASP.NET, Dockerfile, código, autenticação, frontend ou testes permanentes. Resultados novos desta etapa são separados das referências históricas B06-C2.

## 2. Estado inicial do Git

Branch `main`; HEAD e referência local `origin/main`: **`400a0dfa74109723336fd9de89d119d5a7bb4284`**. Uma consulta nova `git ls-remote origin refs/heads/main` retornou o mesmo SHA: a referência local estava atualizada nessa consulta. Sem fetch, pull ou atualização automática da branch.

Pendências iniciais exclusivamente B06-C1 e B06-C2 não rastreados. Nenhum arquivo funcional modificado antes da implementação. Snapshot inicial de **242 arquivos** da árvore principal e **161** de maintenance-phase1, incluindo entradas não rastreadas não ignoradas, com SHA-256, HEAD/status, branches e worktrees. Caches ignorados não são inventário integral do disco.

Worktree principal neste repositório; `.worktrees/maintenance-phase1` em branch maintenance-phase1, HEAD **`fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`**, com as seis fontes modificadas e entradas não rastreadas preexistentes preservadas. Também registradas e preservadas as branches b05-b1-angular-security, fix/guest-route-guard, security/b05-b2-dependency-review, security/b05-b3-sqlite-review, security/b05-b4-container-review, security/b06-a1-hardening-diagnosis e security/b06-b4-docker-runtime.

Evidências auxiliares novas: **`%TEMP%/seenit-b06-c3-20261124-f4954499`**. Snapshot `repository-before.json` SHA-256 **`34faf0da95a4d3d8e4b38efa166c1dd48cbfc69f2062f4ee6f0beaba5676135f`**.

## 3. Referências B06-C1/C2

O [B06-C2](B06-C2-SKIASHARP-UPDATE-EXPERIMENT.md) foi lido integralmente: duas referências aprovadas, transitivos macOS/Win32, proveniência, native candidato, testes, falhas iniciais dos harnesses, recursos, backports, APNG e rollback. O [B06-C1](B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md) mantém a distinção entre versões internas medidas, versões de fonte e alcance de CVEs.

Preservados os encerramentos [B06-A](B06-A-FINAL-SUMMARY.md) e [B06-B](B06-B-FINAL-SUMMARY.md). Os 17 alertas e métricas do experimento anterior foram tratados como referência histórica até as execuções novas abaixo. A CVE-2026-46675 do libpng não foi considerada corrigida pela atualização NuGet.

## 4. Diff exato

Único arquivo funcional/rastreado modificado: **`backend/SeenIt.Api/SeenIt.Api.csproj`**.

```diff
-    <PackageReference Include="SkiaSharp" Version="4.155.0" />
-    <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.155.0" />
+    <PackageReference Include="SkiaSharp" Version="4.155.2" />
+    <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.155.2" />
```

Edição por bytes nas duas posições de versão, sem reformatar XML. Preservados **UTF-8 com BOM EF-BB-BF**, quebras de linha, tamanho **1394 bytes**, demais propriedades/referências/comentários. Diff imediato: somente duas linhas substituídas, nenhuma referência adicionada/removida. `git diff --check` aprovado. O arquivo resultante é idêntico por hash ao csproj da candidata B06-C2.

SHA-256 csproj: antes **`79040af396b449b56c780853c5feb435a5a4f07bbcb4acdbc5c1e2f48f7ca382`**; depois **`f8cd7eb891e0d714bd8219e474e190c3a9e693c3074307355791e6d89de78561`**.

## 5. Resolução de dependências

Restore novo executado dentro do Docker build para API, AvatarChecks e CoreChecks. `project.assets.json` resultante foi exportado do estágio build; `SeenIt.Api.deps.json` foi lido da imagem final. Não foi usado o obj antigo do host Windows como evidência da resolução nova. Nenhum lockfile ou configuração permanente adicional foi alterado.

| Pacote | Versão resolvida B06-C3 | Relação com o estado inicial |
| --- | --- | --- |
| SkiaSharp | **4.155.2** | Direto, atualizado de 4.155.0 |
| SkiaSharp.NativeAssets.Linux.NoDependencies | **4.155.2** | Direto, atualizado de 4.155.0 |
| SkiaSharp.NativeAssets.macOS | **4.155.2** | Transitivo necessário, atualizado de 4.155.0 |
| SkiaSharp.NativeAssets.Win32 | **4.155.2** | Transitivo necessário, atualizado de 4.155.0 |

Grafo completo da API igual ao da candidata B06-C2; nenhum outro pacote mudou. TFM **net10.0**, publicação framework-dependent, RID **linux-x64**, runtimeTarget `.NETCoreApp,Version=v10.0/linux-x64`. Os transitivos macOS/Win32 aparecem no restore geral, mas não como entradas no deps final Linux.

Binding: **AssemblyVersion 4.155.0.0**, preservado pelo fornecedor, **FileVersion 4.155.2.0**, ProductVersion 4.155.2 com commit **bae2bd1738735d78625e81cc51c9e60240b6cbd5**. AssemblyVersion inalterado não constitui mistura com pacote antigo. O deps seleciona ambos os pacotes 4.155.2 e o native linux-x64. FileVersion nativo genérico 0.0.0.0 não identifica o motor.

## 6. Identidade da imagem final

Dockerfile original preservado, SHA-256 **`078a25c4c04ee235e844226dcd97883b067925e3797df3ec5f6796afebc7bf5e`**. Contexto sanitizado temporário preparado do código atual, com a única alteração da seção 4. Arquivos fonte do contexto iguais aos da candidata B06-C2. Sem segredos, bin/obj locais, configurações do desenvolvedor ou frontend no contexto Docker; CA pública e runtime-settings versionados preservados.

Bases mantidas:

- SDK `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625`.
- ASP.NET `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde`.

Tag exclusiva **`seenit-api:b06-c3-validation-f4954499`**. ID/índice local **`sha256:5e552e9974d0a3ce7281d3cca4465aa0268a729b1292115b3fe1ebff564eac8c`**; manifesto final de plataforma no log **`sha256:8d51cad015049d23d90839408544ee7d1775d98ac737a6ff5d0b0c253b5f00ee`**; configuração **`sha256:1787cc729b8e04e5a5568c1939254b454b5008c25e29d7c8d3a92656ba5c385f`**.

Confirmados linux/amd64, SDK de build **10.0.404** por execução no estágio build, Ubuntu **24.04.5 LTS/Noble**, Microsoft.NETCore.App e Microsoft.AspNetCore.App **10.0.15** no smoke novo. User **app/UID 1654**, porta **10000**, entrypoint **`dotnet SeenIt.Api.dll`**. Nenhuma imagem histórica sobrescrita.

## 7. Hashes dos binários

| Artefato final B06-C3 | SHA-256 |
| --- | --- |
| **libSkiaSharp.so** | **`0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef`** |
| SkiaSharp.dll | `d1a77d5f16c95e42e58d05edea34019fe7cfcea3d10aa22e0a4d75b4a4a45dd8` |
| SeenIt.Api.dll | `63b81c9f6da07ec8ce27ada5fa78b3e8e948b9075911efdff822cc4d00506c88` |

Os **70 arquivos publicados são idênticos por SHA-256 à candidata B06-C2**. O native esperado foi confirmado sem divergência: **12.289.656 bytes**, ELF64 little-endian, machine 62/x86-64, SONAME **`libSkiaSharp.so.155.0.0`**. Getters executados: milestone 155, increment 0, iguais à candidata de manutenção.

DT_NEEDED preservado: libstdc++.so.6, libpthread.so.0, libdl.so.2, libm.so.6, libc.so.6, ld-linux-x86-64.so.2, librt.so.1. Ldd novo no verify/smoke resolveu essas bibliotecas e libgcc_s.so.1 transitiva, **sem not found**. Não há DT_NEEDED separado de JPEG/PNG/WebP. Decodificação e encode reais executados; sem erro ABI, TypeLoadException, DllNotFoundException ou EntryPointNotFoundException nas novas execuções.

## 8. Codecs e versões

Identidade de todo o native com B06-C2 permite reaproveitar a análise de proveniência, sem transformar resultados funcionais antigos em novos. Strings/símbolos e ELF foram inspecionados novamente nesta etapa.

| Componente | Evidência B06-C3 |
| --- | --- |
| libjpeg-turbo | String explícita **3.1.4.1**; mesmo asset/pin B06-C2 |
| libpng | Fingerprint **1.6.58**; mesmo asset/pin B06-C2 |
| libwebp | Símbolos decode/encode presentes; **1.6.0 declarada na fonte vinculada ao pacote**, sem getter interno confirmado |
| GIF/Wuffs | Símbolos presentes; versão interna exata permanece desconhecida |
| BMP/ICO/WBMP | Símbolos presentes; fixtures disfarçadas rejeitadas novamente |
| Outros auxiliares | Inventário/versões internos continuam parciais |

Pins da fonte vinculada, inalterados segundo B06-C2: JPEG `9217719d3a58633923b096af4c1d50d304768a64`, PNG `3061454d980de7d53608f594194cfac722721d2a`, WebP `4fa21912338357f89e4fd51cf2368325b59e9bd9`, Wuffs `e3f919ccfe3ef542cfc983a82146070258fb57f8`. Não se declara libpng corrigida nem versões internas desconhecidas por suposição.

## 9. Verificação dos backports

Notas oficiais [SkiaSharp 4.155.2](https://mono.github.io/SkiaSharp/docs/releases/4.155.2.html), consultadas novamente, vinculam seis correções. Pacote/InformationalVersion apontam ao commit **bae2bd1738735d78625e81cc51c9e60240b6cbd5**, submódulo Skia **e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c**, conforme a cadeia e [comparação de fonte registradas no B06-C2](https://github.com/mono/skia/compare/c2d78902a56e24827a7273a4ed3461ce18780680...e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c).

| CVE | Correção vinculada |
| --- | --- |
| **CVE-2026-118340** | IDs de filtros de imagem passam a 64 bits |
| **CVE-2026-121086** | Dimensionamento/verificações do construtor de regiões |
| **CVE-2026-126912** | Limites no blitter de antialiasing analítico |
| CVE-2026-119261 | Rejeição de tipos inválidos em SkSL |
| CVE-2026-126871 | Limitação/contabilização de draws de clipping Graphite |
| CVE-2026-126882 | Correção/clamp de raios em geometria Ganesh |

O native B06-C3 é byte a byte igual ao asset candidato cuja proveniência NuGet foi verificada no B06-C2. Evidência ELF nova: contador `_ZZL27next_image_filter_unique_idvE6nextID` com **8 bytes**, coerente com o patch de 64 bits; o controle B06-C2 tinha 4 bytes. Não houve build reproduzível independente, disassembly integral dos seis patches ou PoC de exploração do endpoint. O vínculo das correções não depende do Scout.

## 10. Build e verify

Build novo **`--platform linux/amd64 --no-cache --progress plain`**, Dockerfile original no contexto sanitizado. Resultado **exit 0**, duração **51,95 s**, sem warnings de compilação identificados. Docker Engine disponível; nenhum serviço reiniciado, nenhuma ferramenta/base atualizada.

| Execução nova B06-C3 | Resultado |
| --- | --- |
| Restore API/AvatarChecks/CoreChecks | Passou |
| Publish Release framework-dependent linux-x64 | Passou |
| NuGet audit dos três projetos, transitivos incluídos | Sem pacotes vulneráveis reportados |
| Verify: arquivo native, cmp API/checks, ldd | Passou, sem dependências ausentes |
| AvatarChecks | **16 grupos aprovados** |
| CoreChecks | Watch time e classificação/cache/retries aprovados; **72 casos HTTP de cadastro** |
| Validação da CA no estágio final como app | Passou |

Uma exportação adicional do estágio build, usando cache desse build novo, serviu somente para coletar `project.assets.json`, nuspec e versão SDK. Não foi contada como repetição sem cache de verify. Nenhum apt upgrade ou alteração do Dockerfile.

## 11. Testes de imagem

Harnesses B06-C2 copiados para diretórios temporários exclusivos B06-C3. Antes de usar seus resultados, **os 70 arquivos publicados foram comparados com cada um dos quatro harnesses**, sem diferenças; deps dos harnesses já referenciavam 4.155.2. Nenhum harness permanente alterado. Executáveis reutilizados, **execuções/resultados novos**, não recompilação nova declarada.

| Verificação nova sobre a imagem B06-C3 | Resultado |
| --- | --- |
| AvatarChecks no verify | 16 grupos; decode, resize/reencode, payload anexado, contratos/storage e falhas simuladas |
| ExtraAvatar | Três grupos: WebP de dois frames rejeitado; gate/cancelamento; excesso de pixels e liberação |
| CodecExperiment | **37 registros**, sucesso; inclui testes, detecção de formato, getters e medições |
| JPEG/PNG/WebP, 32×16 e 1×1 | Aceitos; saída WebP não animada e dimensões corretas |
| Transparência e cores | Pixels amostrados preservam alpha/cor nas tolerâncias |
| EXIF 1..8 | Fixture reconhecida e dimensões orientadas corretamente |
| Resize 2048×1024 | Saída 1024×512 |
| 4000×4000 (16 MP) e 8192×1 | Aceitos, saída limitada a 1024 no maior lado |
| 4001×4000 e 8193×1 | Rejeição por área/lado |
| Vazio, >5 MiB, truncado/corrompido | InvalidAvatarException, sem alteração do contrato |
| MIME falso/text/plain | Rejeição por conteúdo divergente/tipo não permitido |
| GIF/BMP/ICO/WBMP disfarçados | Rejeição por formato real divergente |
| WebP realmente animado | Rejeitado por animação |
| APNG com dois frames declarados | **Aceito como PNG estático**, FrameCount 0; saída WebP estática 8×8 |

Saída WebP estática reporta FrameCount 0, como B06-C2. APNG não foi transformado em rejeição nova e a rejeição de WebP animado não foi relaxada. O arquivo original nunca foi enviado ao Storage pelo fluxo validado. Qualidade verificada por decode/cor/alpha/dimensões, sem revisão perceptual exaustiva ou comparação de todos os pixels espelhados/rotacionados.

## 12. Recursos e estabilidade

Ensaios offline na imagem final, usuário app, **768 MiB / 2 CPUs / 256 PIDs / timeout 180 s**, mesmo corpus B06-C2. Sem PoCs desconhecidos ou fuzzing. Confirmados cancelamento de leitura/espera, janela de processamento com gate ocupado, recuperação, quatro chamadas paralelas e gate retornando a duas permissões. CancellationToken não foi apresentado como interrupção preemptiva de GetPixels/Encode.

24 repetições 1024×1024 por execução, incluindo geração de fixture, com GC/amostra de WorkingSet a cada quatro operações. Primeira execução ocorreu enquanto tarefas de inspeção/scanner também eram realizadas; o tempo maior motivou **uma repetição isolada**, preservando ambos os resultados:

| Medida | Candidata B06-C2, histórica | B06-C3 inicial | B06-C3 repetição isolada |
| --- | --- | --- | --- |
| Pico WorkingSet | 178,53 MiB | **179,88 MiB** | **178,79 MiB** |
| Mediana por repetição | 129,05 ms | **146,55 ms** | **121,85 ms** |
| CPU no trecho medido | 3357,49 ms | **3733,51 ms** | **3235,62 ms** |
| Tempo decorrido do trecho | 3154,16 ms | **3481,24 ms** | **3046,41 ms** |
| WorkingSet primeira → última amostra | 62,58 → 65,25 MiB | **64,07 → 66,65 MiB** | **62,61 → 82,48 MiB** |

Na repetição, o WorkingSet subiu até 81,17 MiB na segunda amostra e terminou em 82,48 MiB; nas últimas cinco amostras variou aproximadamente 1,31 MiB. O pico total permaneceu próximo ao B06-C2. A variação temporal inicial não persistiu; concorrência do host/allocator/JIT são possibilidades, sem profiling causal conclusivo. A identidade dos 70 arquivos também exclui diferença de código/binário entre B06-C2 candidata e B06-C3, mas não prova equivalência de toda condição externa.

Não há regressão relevante demonstrada neste ensaio limitado, nem crash/OOM: as execuções e repetição terminaram **exit 0, OOMKilled=false**. A retenção/WorkingSet variável foi registrada, não ocultada nem tratada como prova de ausência de leak. O pico inclui criação de fixtures grandes/harness, não apenas AvatarImageProcessor. Nenhuma conclusão de benchmark/SLA ou estabilidade de longo prazo.

## 13. Testes da API

RegisterIntegrationChecks executado novamente no runtime final, SQLite **somente em memória**, HTTP externo proibido por doubles e rede none. Resultado **exit 0**:

- **73 casos HTTP integrados de cadastro**, 16 usuários sintéticos; rejeições sem comandos/saves/hashes indevidos.
- Catálogo/resiliência, biblioteca/integridade, falhas/timeout/cancelamento externos simulados e recuperação aprovados.
- Login/perfil, autorização, spoofing, rate limiting e concorrência aprovados.
- Regressões permanentes: 20 logins sobrepostos, contabilização global, isolamento de categorias, perfil simultâneo, liberação de leases e **CORS** aprovados.

AvatarChecks validou upload/substituição/delete simulados, compensação, conflito, falhas e cancelamento de Storage. Nenhum PostgreSQL/Supabase real, migrations ou dados de produção. A dependência SQLite de teste permaneceu somente no harness; não entrou no conteúdo da imagem final.

## 14. JWT e HTTPS

EntryPointChecks novo: **174/174 asserções, zero falhas**, seis perfis, **72 requests** da matriz. Configuração **ReverseProxy:HttpsRedirectionHandledByEdge** ausente/false/true, com e sem porta HTTPS sintética; health, token válido/expirado, autorização, headers falsificados e terminação TLS simulada. As suítes integradas completam cobertura de JWT inválido/independência de identidade.

Ausente/false mantêm comportamento esperado; true não acrescenta redirecionamentos no perfil de borda. Testes usam entry point real via TestServer. Resultado novo em `entrypoint-results.json`; escrita feita em `/tmp/checks`, gravável por app, preservando a correção de infraestrutura B06-C2, sem ampliar privilégios.

## 15. Smoke

Container novo da imagem B06-C3: **Production**, binding interno **0.0.0.0:10000**, **GET /health HTTP 200** com `{"status":"ok"}`; usuário **app UID 1654**; CA **0444**, legível/não gravável, 1367 bytes; `/app` e `/app/certs` root:root 0755; marcador verify presente. SDK ausente do runtime final.

Rede none, **Privileged=false, CapAdd=null, PortBindings vazio**, sem porta publicada ao host. Recursos limitados a 512 MiB/1 CPU/128 PIDs. Configurações/segredos exclusivamente sintéticos, sem vazamento dos valores testados nos logs. Encerramento normal **exit 0**, sem OOM; logs sem erro inesperado de inicialização. Avisos conhecidos de HTTPS sem proxy/porta e Data Protection local não foram confundidos com regressão. Não houve deploy ou teste de configuração remota real.

## 16. Auditoria NuGet/Scout

NuGet audit novo no Docker build, sem restore do host/alteração de lockfile, informou **nenhum pacote vulnerável** nos três projetos auditados, incluindo transitivos. Limitação: ausência no feed NuGet não exclui CVEs de código estático.

Docker Scout **1.28.0**, nova análise SARIF/SPDX da imagem final B06-C3, **176 pacotes indexados**, sem filtros/ignores/supressões:

| Severidade | B06-C3 novo | B06-C2 candidata, referência |
| --- | --- | --- |
| Critical | **0** | 0 |
| High | **0** | 0 |
| Medium | **13** | 13 |
| Low | **4** | 4 |
| Total | **17** | 17 |

Mesmos 17 IDs, sem CVE nova/removida: CVE-2024-56433, CVE-2025-5222, CVE-2026-105712, CVE-2026-40228, CVE-2026-102473, CVE-2026-102474, CVE-2026-121876, CVE-2026-18477, CVE-2026-18508, CVE-2026-2219, CVE-2026-67693, CVE-2026-76642, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209 e CVE-2026-95210. São os registros de pacotes Ubuntu já detalhados no B06-C2; nenhum novo alerta Skia foi inferido ou suprimido.

Comparação dos pares nome/versão no SBOM: **somente a identificação/tag da própria imagem difere** de B06-C2 candidata; pacotes iguais. Scanner não inventaria integralmente os codecs estáticos, não valida sozinho os seis backports e não comprova correção libpng. Resultado **17 → 17**, não redução de CVEs.

## 17. Comparação com B06-C2

- Csproj aplicado igual ao experimental aprovado, sem mudança adicional de fonte.
- Grafo da API, bases, runtime, codecs identificados e **70 arquivos publicados idênticos**.
- Native/binding correspondem aos hashes aprovados; SONAME/AssemblyVersion estáveis não mascaram pacote antigo.
- Novas execuções de build/verify, quatro harnesses, smoke e scanner passaram.
- APNG estático, WebP animado rejeitado e contratos de erros/limites preservados.
- Diferenças de tempo/WorkingSet registradas na seção 12; repetição temporal não reproduziu a lentidão inicial.
- Falhas históricas dos harnesses B06-C2 não se repetiram: SQLite de teste presente, duas CPUs para teste bloqueante, resultados em diretório gravável, expectativa FrameCount estático corrigida. Nenhum ajuste da aplicação necessário.

## 18. Riscos residuais

Parsing nativo continua recebendo bytes antes da conclusão das validações de dimensões/formato/animação; limites, autenticação e quotas não corrigem toda falha de memória. Cancelamento gerenciado não preempta native. Metadados/cabeçalhos e expansão ainda merecem acompanhamento; APNG é aceito como estático.

Libpng **1.6.58 permanece**: nenhuma evidência de correção da CVE-2026-46675 neste pacote. Mantida a avaliação B06-C1 de componente em faixa afetada e exclusão da sequência vulnerável específica pelo leitor progressivo examinado. Versões internas GIF/Wuffs/auxiliares seguem parcialmente desconhecidas. Scout não certifica esses componentes. Não foi demonstrada exploração das CVEs pelo endpoint.

## 19. Limitações

Validação exclusivamente local, corpus sintético limitado, sem fuzzing/PoCs, sanitizers ou benchmark estatístico. Cancelamento observado por gate/check gerenciado, sem prova de preempção C. EXIF cobriu metadados/dimensões nas oito orientações e amostras de cores/alpha, sem revisão perceptual exaustiva.

Harnesses aprovados foram reutilizados após comparação de publicação/deps; não recompilados nesta etapa. Fonte/proveniência e identidade do native sustentam os backports, sem reconstrução reproduzível do fornecedor/disassembly completo. Versão WebP é declarada na fonte, não getter interno. WorkingSet variável e retenção curta não permitem concluir ausência de leak. Nenhum teste operacional em produção, proxy real, PostgreSQL ou Storage real.

## 20. Rollback futuro

Plano documental: reverter **as duas referências coordenadamente para 4.155.0**, preservando demais bytes, digests e configurações. Restaurar/publicar novamente e conferir native controle **`26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e`**, deps/fileVersion, verify, imagens, API, JWT/HTTPS e smoke. Para reversão operacional futura, manter imagem anterior por digest. Não misturar binding/native de pacotes diferentes ou sobrescrever tags históricas. Nenhum rollback remoto realizado.

## 21. Recomendação para B06-C4

Implementação **validada localmente**, apta a revisão final do diff, evidências e preparação do fluxo de versionamento B06-C4, quando solicitado. Não iniciar commit/push/PR/merge/deploy por inferência desta aprovação. Não incluir B06-D/BuildHost, novas validações de imagem ou outro pacote no mesmo escopo.

| Critérios de aceite | Resultado |
| --- | --- |
| 1–3. Duas referências, ambas 4.155.2, sem pacote inesperado | Passaram |
| 4–6. Dockerfile/digests/linux-amd64 e build/verify | Passaram |
| 7–8. Native carregado e ldd íntegro | Passaram |
| 9–10. Imagens e contratos atuais | Passaram |
| 11–12. Recursos/crash/OOM | Sem regressão relevante demonstrada no ensaio limitado; execuções exit 0; variações documentadas |
| 13–14. API/JWT/HTTPS | Passaram |
| 15. Non-root/health/10000 | Passaram |
| 16. Scanner | Sem diferença de CVEs/pacotes frente à candidata B06-C2 |
| 17. Nenhuma outra alteração funcional necessária | Passou |

## 22. Preservação do repositório

Estado final: `main`, HEAD e refs locais inalterados; **somente csproj rastreado modificado** nas duas versões e três relatórios C1/C2/C3 não rastreados. B06-C1/C2 preservados por hash. Snapshot de **242 arquivos principais** comparado: única diferença autorizada no csproj. **161 arquivos da manutenção**, seu HEAD/status e entradas não rastreadas preservados, assim como branches/worktrees.

Hashes preservados: Program.cs **`b2b73ed98b7953b061ccb15cc4207bc5588628c015d073a01ef089fdeeac5812`**; Dockerfile **`078a25c4c04ee235e844226dcd97883b067925e3797df3ec5f6796afebc7bf5e`**. JWT, autenticação, frontend, demais csproj, testes permanentes, configurações e migrations intactos. `git diff --check` aprovado.

Recursos Docker anteriores: **20 registros de imagens e dois containers**; todos preservados por referência/ID e estado. Somente containers novos desta etapa foram removidos após encerrar. Novas imagens de validação e de coleta do estágio build permanecem locais; nenhuma publicação, remoção de cache/volume ou sobrescrita histórica.

Nenhum stage/commit/push/PR/merge/deploy, reset/clean/rebase/force/stash, mudança de branch/worktree, acesso a produção, alteração de Render/Vercel ou configuração de ambiente permanente/remota. Configurações sintéticas foram restritas aos containers de teste autorizados.

Única documentação criada: **`docs/B06-C3-SKIASHARP-UPDATE-IMPLEMENTATION.md`**. Logs/JSON/SARIF/SPDX, hashes, inspeções, estados e comparações novos ficaram em TEMP; índice `evidence-hashes.json` e conferência `preservation-final.json`. Esses auxiliares locais não foram adicionados ao Git.

**B06-C3 IMPLEMENTAÇÃO VALIDADA**
