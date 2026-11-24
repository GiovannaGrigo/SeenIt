# B06-C — Encerramento da atualização do SkiaSharp

24/11/2026 · Horário de Brasília

## 1. Objetivo

Consolidar o diagnóstico, o experimento, a implementação, a integração e as verificações operacionais da atualização coordenada para SkiaSharp 4.155.2. O B06-C6 verificou o GitHub, o conteúdo integrado e os artefatos locais preservados. As verificações operacionais são atribuídas ao operador.

## 2. Histórico B06-C1 a B06-C6

| Etapa | Resultado |
| --- | --- |
| [B06-C1](B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md) | Identificou codecs do 4.155.0, analisou a superfície de parsing e recomendou testar os seis backports do 4.155.2. |
| [B06-C2](B06-C2-SKIASHARP-UPDATE-EXPERIMENT.md) | Comparou 4.155.0 e 4.155.2 em Linux/amd64 e aprovou a candidata localmente. |
| [B06-C3](B06-C3-SKIASHARP-UPDATE-IMPLEMENTATION.md) | Aplicou duas versões no projeto original e revalidou a imagem produzida pelo Dockerfile real. |
| B06-C4 | Versionou a atualização e os três relatórios em dois commits e abriu o PR #8. |
| B06-C5 | O operador informou reconstrução e deploy no Render, Vercel Ready e verificações funcionais em produção. |
| B06-C6 | Confirmou a integração no GitHub, sincronizou a main por fast-forward e consolidou as evidências neste relatório. |

## 3. Estado inicial do Git

A worktree principal estava limpa em `security/b06-c4-skiasharp-update`, HEAD `c1216db6d4d69cab6abaa5bc98df614487b3d1b1`. Antes do fetch, `main` e a referência local `origin/main` apontavam para `400a0dfa74109723336fd9de89d119d5a7bb4284`.

O snapshot inicial em `%TEMP%/seenit-b06-c6-before.json` registrou dez branches locais, duas worktrees, status e SHA-256 de 243 arquivos na árvore principal e 161 na manutenção, incluindo entradas não rastreadas não ignoradas. A worktree `.worktrees/maintenance-phase1` estava no HEAD `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seis arquivos modificados e nove entradas não rastreadas.

## 4. PR #8 e commits

O GitHub retornou estado **MERGED** para `main`, às 10:17:39 de 24/11/2026 (13:17:39 UTC). O PR continha os commits abaixo. [PR #8](https://github.com/GiovannaGrigo/SeenIt/pull/8)

| Commit | SHA |
| --- | --- |
| Técnico · Fix: Atualiza SkiaSharp para incorporar correções de segurança | `1a8ae751991f29eea9db7b1428ac8011737b5f3d` |
| Documental · Docs: Documenta diagnóstico e atualização do SkiaSharp | `c1216db6d4d69cab6abaa5bc98df614487b3d1b1` |

O diff efetivo entre a base e a integração contém exatamente quatro arquivos.

- `backend/SeenIt.Api/SeenIt.Api.csproj`
- `docs/B06-C1-SKIASHARP-CODEC-SECURITY-DIAGNOSIS.md`
- `docs/B06-C2-SKIASHARP-UPDATE-EXPERIMENT.md`
- `docs/B06-C3-SKIASHARP-UPDATE-IMPLEMENTATION.md`

## 5. SHA da integração

`fd7cbe36e1decec784b32bc2470a6c78b0f723e7` é um commit de merge com dois pais, a base `400a0dfa74109723336fd9de89d119d5a7bb4284` e o topo do PR `c1216db6d4d69cab6abaa5bc98df614487b3d1b1`. Ambos os commits originais estão no histórico. A árvore integrada é idêntica à árvore no topo do PR. O GitHub retornou `mergeable: UNKNOWN` após a integração. A comparação das árvores confirmou o conteúdo sem alterações adicionais de resolução. O fetch atualizou `origin/main` para o SHA da integração. A `main` local foi sincronizada por fast-forward e ficou limpa antes da criação deste relatório.

## 6. Alteração técnica

Somente `SkiaSharp` e `SkiaSharp.NativeAssets.Linux.NoDependencies` passaram de `4.155.0` para `4.155.2`. As demais referências e propriedades do XML foram preservadas. O csproj integrado conserva BOM UTF-8 e tem SHA-256 `f8cd7eb891e0d714bd8219e474e190c3a9e693c3074307355791e6d89de78561`, igual ao arquivo validado no B06-C3.

O Dockerfile preserva os digests originais.

| Base | Referência |
| --- | --- |
| SDK | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` |
| ASP.NET | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde` |

Dockerfile, Program.cs, configuração HTTPS B06-A, JWT, rate limiting e frontend ficaram fora do diff. O PR não contém migrations.

## 7. Pacotes, versões e dependências

O restore local histórico resolveu os dois pacotes diretos e os transitivos `SkiaSharp.NativeAssets.macOS` e `SkiaSharp.NativeAssets.Win32` em 4.155.2. O restante do grafo da API permaneceu igual. A publicação `net10.0/linux-x64` seleciona somente o asset Linux. O binding registra FileVersion `4.155.2.0` e AssemblyVersion `4.155.0.0` mantida pelo fornecedor. O `deps.json` registra os dois pacotes efetivos em 4.155.2.

## 8. Hashes e proveniência dos binários

| Artefato local | SHA-256 |
| --- | --- |
| `libSkiaSharp.so` 4.155.2 | `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef` |
| `SkiaSharp.dll` 4.155.2 | `d1a77d5f16c95e42e58d05edea34019fe7cfcea3d10aa22e0a4d75b4a4a45dd8` |
| `libSkiaSharp.so` 4.155.0 | `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e` |

O B06-C6 recalculou os hashes dos dois binários 4.155.2 no export local do B06-C3. Historicamente, o B06-C2 confrontou o ELF x86-64, SONAME `libSkiaSharp.so.155.0.0`, com o asset do nupkg público linux-x64. O B06-C3 verificou igualdade de SHA-256 dos 70 arquivos publicados com a candidata B06-C2. A cadeia do nuspec e da versão informacional aponta para SkiaSharp `bae2bd1738735d78625e81cc51c9e60240b6cbd5` e Skia `e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c`. [Revisão de origem](https://github.com/mono/SkiaSharp/tree/bae2bd1738735d78625e81cc51c9e60240b6cbd5)

## 9. Codecs e versões identificados

JPEG/libjpeg-turbo 3.1.4.1 e PNG/libpng 1.6.58 foram identificados por evidência no binário. WebP/libwebp 1.6.0 é a versão declarada na revisão de fonte vinculada, com símbolos de decode e encode no asset. GIF/Wuffs e outros auxiliares têm inventário e versões internas parciais. O B06-C2 registrou os mesmos pins de JPEG, PNG, WebP e Wuffs no controle e na candidata.

## 10. Seis correções de segurança

| CVE | Correção documentada na revisão de Skia |
| --- | --- |
| CVE-2026-118340 | IDs de filtros de imagem de 64 bits |
| CVE-2026-121086 | Dimensionamento e verificações no construtor de regiões |
| CVE-2026-126912 | Verificações de limites no blitter de antialiasing analítico |
| CVE-2026-119261 | Rejeição de tipos inválidos em SkSL |
| CVE-2026-126871 | Contabilização e limitação de operações de clipping no Graphite |
| CVE-2026-126882 | Correção dos limites de raios em geometria Ganesh |

As notas do fornecedor identificam os seis backports, e o B06-C2 registrou as alterações de origem, a cadeia do pacote e a igualdade do asset com o binário validado. O contador de IDs de filtros de imagem também foi observado com oito bytes no ELF candidato. O avatar processa imagens em bitmap e não recebe programas SkSL nem cria contexto GPU. O alcance dos caminhos internos de raster não foi mapeado integralmente. A exploração efetiva pelo endpoint não foi demonstrada. [Notas SkiaSharp 4.155.2](https://mono.github.io/SkiaSharp/docs/releases/4.155.2.html) · [Comparação das revisões Skia](https://github.com/mono/skia/compare/c2d78902a56e24827a7273a4ed3461ce18780680...e6cfc7a5d6dea7e8c21a59782c672a00b1fff28c)

## 11. Resultados locais B06-C2/C3

Resultados históricos de execuções locais, com controle e candidata no B06-C2 e nova execução sobre o artefato final no B06-C3. O B06-C6 revisou essas evidências sem rerodar as suítes.

| Área | Resultado |
| --- | --- |
| Construção | Docker build Linux/amd64 sem cache, restore, Release publish e verify aprovados. SDK 10.0.404, .NET e ASP.NET 10.0.15, Ubuntu Noble. |
| Runtime | Native carregado e `ldd` sem bibliotecas ausentes. Health HTTP 200 na porta 10000, usuário app UID 1654, CA pública legível em modo 0444. |
| Suítes | 16 grupos AvatarChecks, CoreChecks com 72 casos HTTP de cadastro, RegisterIntegrationChecks com 73 casos HTTP integrados, ExtraAvatar e corpus suplementar aprovados. |
| JWT/HTTPS | EntryPointChecks com 174/174 asserções por cenário, incluindo configuração B06-A ausente, false e true, headers e redirecionamento. |
| Imagens | JPEG, PNG, WebP, transparência, EXIF 1–8, resize, dimensões e limites testados. Arquivos inválidos e WebP animado rejeitados. APNG aceito como PNG estático em ambas as versões. |
| Concorrência | Cancelamento, recuperação, chamadas paralelas e liberação do gate verificados. |

O B06-C2 registrou falhas iniciais na infraestrutura dos harnesses, incluindo CRLF no script, dependência SQLite ausente, barreira de teste com uma CPU, diretório de saída sem permissão, expectativa incorreta de FrameCount estático e cache efêmero de analyzers. As execuções finais aprovadas incorporaram os ajustes aos harnesses. No B06-C3, os harnesses suplementares foram reutilizados depois de conferir os 70 arquivos publicados e executados novamente.

## 12. Recursos e estabilidade

Ensaios limitados a 24 repetições, com 768 MiB, duas CPUs e 256 PIDs. O pico de WorkingSet foi 178,39 MiB no controle C2, 178,53 MiB na candidata C2, 179,88 MiB no primeiro ensaio C3 e 178,79 MiB na repetição isolada C3. A repetição isolada mediu 3,24 s de CPU e 3,05 s decorridos. Nela, o WorkingSet amostrado passou de 62,61 a 82,48 MiB. As execuções finais terminaram sem crash ou OOM. A duração e a variabilidade das amostras limitam conclusões sobre desempenho e retenção de longo prazo.

## 13. Docker Scout e NuGet audit

Os scans locais registraram **0 Critical, 0 High, 13 Medium, 4 Low, total de 17** em ambos os cenários B06-C2 e no B06-C3. O inventário Scout B06-C3 indexou 176 pacotes e manteve os mesmos IDs que a candidata. NuGet audit não reportou pacotes vulneráveis nos projetos auditados, incluindo transitivos. A cobertura dos scanners para código ligado estaticamente é parcial. Os seis backports foram vinculados às evidências de origem e do pacote, com contagem de alertas inalterada.

## 14. Evidências operacionais B06-C5

Em 24/11/2026, o operador informou Vercel **Ready** e deploy do último commit integrado no Render após limpeza de cache. O trecho de log descrito no material fornecido registra etapa final do Docker concluída, exportação da imagem, envio de camadas e exportação do cache. Registra ainda usuário app UID 1654, CA 0444, ambiente Production, binding HTTP interno em `0.0.0.0:10000`, `Application started` e `Your service is live`. O status Render **Live** foi também informado pelo operador.

O trecho fornecido não mostra as linhas de restore NuGet. A versão efetivamente carregada no Render não foi conferida por hash. A conclusão de implantação bem-sucedida decorre da integração confirmada, do build registrado pelo operador e das verificações operacionais relatadas.

## 15. Validação funcional em produção

O operador confirmou health, login, cadastro, perfil, upload JPEG/PNG/WebP, substituição e remoção de avatar, catálogo, biblioteca, navegação HTTPS sem loops e ausência de novos erros observados nos logs durante os testes. O Codex não executou esses checks em produção. A evidência disponível cobre os fluxos listados e não inclui atestação binária remota, auditoria Docker Scout remota, medição de RSS/CPU pelo Codex ou validação independente exaustiva dos codecs.

## 16. Warnings conhecidos

- Data Protection com persistência de chaves não garantida.
- Data Protection sem encryptor XML configurado.
- `HTTP_PORTS` sobrescrito por `URLS`, ambos associados à porta interna 10000.

O aviso `Failed to determine the https port for redirect` não apareceu no trecho fornecido. Os warnings acima permanecem registrados, sem mudança de configuração no B06-C6.

## 17. Riscos e limitações

- Libpng permanece em 1.6.58. A atualização não demonstrou correção da CVE-2026-46675. O leitor progressivo examinado no B06-C1 está fora da condição específica do advisory. [Advisory libpng](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j)
- O parser nativo é alcançado antes da conclusão das validações. APNG continua aceito como imagem estática e chamadas nativas não têm cancelamento preemptivo comprovado.
- A variação de WorkingSet e a curta duração dos ensaios limitam conclusões sobre retenção, CPU e carga. As suítes locais usaram banco em memória e Storage simulado.
- Os 17 alertas Scout referem-se à imagem local. O inventário de objetos estáticos é incompleto e não houve scan direto da imagem Render.
- Os warnings de Data Protection permanecem. BuildHost/Roslyn segue no B06-D e o monitoramento recorrente ainda precisa ser definido no B06-E.

## 18. Estratégia de rollback

Plano futuro, sem execução no B06-C6.

1. Identificar o deploy estável anterior e a imagem por digest, quando disponível.
2. Restaurar coordenadamente as duas referências para 4.155.0 e preservar os digests SDK/ASP.NET.
3. Reconstruir e executar verify Linux/amd64. Conferir o SHA-256 nativo de controle `26a5cf2cfb4529cf00caabc7e9f381ff59641c36690a6e658f8e3e4fa9ac850e` e a coerência do binding e do deps.
4. Repetir suites de avatar, integração e JWT/HTTPS.
5. Implantar mediante autorização específica e verificar health, login, perfil e imagens.

O rollback também retira os seis backports incorporados no 4.155.2.

## 19. Próximas frentes de hardening

Prosseguir com o diagnóstico BuildHost/Roslyn no B06-D. Definir no B06-E a cadência e a responsabilidade pelo monitoramento de advisories NuGet, Skia e codecs estáticos, além do inventário da imagem. Acompanhar a cadeia de origem da libpng para confirmar eventual correção no asset distribuído.

## 20. Preservação do repositório

Após o fast-forward, `main` e `origin/main` ficaram em `fd7cbe36e1decec784b32bc2470a6c78b0f723e7`. A branch de implementação conserva o SHA documental. As duas worktrees foram preservadas. A verificação comparou o HEAD, o status e os 161 hashes da manutenção. Os arquivos preexistentes na árvore principal foram confrontados com o snapshot. Nos três relatórios históricos, o checkout converteu LF para CRLF conforme a configuração local. Seus bytes coincidem com o snapshot após normalizar somente as quebras de linha, e os blobs Git permanecem idênticos.

As 22 referências de imagens e os dois containers históricos foram inventariados por ID antes e depois. A única criação na árvore de trabalho foi `docs/B06-C-FINAL-SUMMARY.md`, não rastreado. Não houve commit, push, novo PR, deploy, publicação de imagem ou alteração de configuração remota nesta etapa.

## 21. Conclusão

O PR #8 integrou as duas referências 4.155.2 e os relatórios históricos. A validação local foi aprovada no B06-C3 e o operador informou sucesso dos fluxos operacionais do B06-C5. A evidência disponível permite encerrar a frente B06-C com os riscos residuais documentados e com o hash remoto ainda sem verificação.

**B06-C PRONTO PARA ENCERRAMENTO**
