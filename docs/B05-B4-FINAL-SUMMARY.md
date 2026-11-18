# B05-B4 — Consolidação e encerramento documental

Data: 18/11/2026. Projeto: SeenIt. Escopo desta etapa: verificar a integração, sincronizar `main` e documentar o encerramento, sem novas correções.

## 1. Objetivo da correção

Atualizar exclusivamente as duas imagens base Docker para incorporar SDK 10.0.404 e ASP.NET/.NET 10.0.15, corrigindo vulnerabilidades prioritárias sem alterar código funcional, contratos, dependências NuGet, Angular ou migrations. O encerramento não representa garantia de segurança absoluta.

## 2. Diagnóstico inicial

O Dockerfile utilizava as tags móveis `10.0-noble` sem digests fixados. A inspeção inicial de SDK/runtime Windows não comprovava o conteúdo da imagem Linux. A imagem histórica `seenit-api:render-check` continha ASP.NET/.NET 10.0.14 e tinha proveniência diferente do HEAD analisado; também não continha a CA adicionada posteriormente. Por isso, não foi utilizada como controle equivalente do código atual.

Após disponibilização do Docker e autenticação do Scout, a auditoria local confirmou 36 vulnerabilidades: 0 Critical, 4 High, 29 Medium e 3 Low. O inventário e as condições de aplicabilidade estão nos relatórios anteriores.

## 3. CVEs prioritárias

As cinco CVEs dos frameworks corrigidas em 10.0.15 foram CVE-2026-114149, CVE-2026-114146, CVE-2026-114147, CVE-2026-109475 e CVE-2026-109476. A CVE-2026-109518 do APT foi corrigida com a versão 2.8.4ubuntu0.1 incorporada à base.

O parser multipart era relevante para o upload de avatar (`PUT /api/profile/avatar`, `IFormFile`); autenticação, limite de tamanho e rate limiting não substituem a correção do parser. Não foi executada exploração ofensiva. Não foi identificado uso de SignalR/Redis ou HttpListener na API. O processamento de forwarded headers e a aplicabilidade efetiva dependem da configuração real do proxy.

As severidades foram preservadas por fonte: Scout classificava CVE-2026-114146 como High, enquanto Microsoft a classificava como Critical; CVE-2026-109475 aparecia como Medium no Scout e High na Microsoft. A tabela agregada deste documento usa a classificação do Scout.

## 4. Alterações aplicadas

A alteração técnica integrada limita-se às duas instruções `FROM` de `backend/SeenIt.Api/Dockerfile`. O restante dos estágios, checks, usuário, certificados e comportamento da aplicação foi preservado. Os outros quatro arquivos do PR são relatórios.

Não houve atualização NuGet, alteração Angular, migration ou mudança em `Program.cs`. A comparação do merge com seu primeiro pai confirmou exatamente os cinco arquivos relacionados na seção 9, sem alterações inesperadas em código.

## 5. Digests utilizados

Manifestos aprovados para Linux amd64, confirmados pelo Codex no Dockerfile da `main` integrada:

| Base | Referência fixada | Versão verificada localmente na implementação |
| --- | --- | --- |
| SDK | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` | SDK 10.0.404 |
| ASP.NET | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5` | Microsoft.NETCore.App e Microsoft.AspNetCore.App 10.0.15 |

São digests de manifestos amd64, não os digests dos índices multiplataforma. A tag legível não atualiza automaticamente uma referência fixada por digest.

## 6. Experimento isolado

O B4.4 comparou controle e candidata a partir do mesmo código `2cd74b978c4700a545475f7e4fdb7aff0cb3a07f`, em contextos temporários fora do repositório. Somente as duas bases diferiam. O controle utilizava SDK 10.0.403 e frameworks 10.0.14; a candidata, SDK 10.0.404 e frameworks 10.0.15.

Os dois builds e seus checks passaram. A comparação evitou atribuir à troca de base diferenças de código, CA ou configuração da imagem histórica. O relatório do experimento preserva versões, IDs, hashes, comandos e limitações. Os artefatos temporários citados podem ser descartados pelo sistema; não se afirma sua retenção permanente.

## 7. Resultados Docker Scout

Resultados locais registrados em 17/11/2026, com Docker Scout 1.27; não foi executado novo scan nesta etapa documental:

| Medida | Controle | Candidata / validação da aplicação |
| --- | ---: | ---: |
| Total de vulnerabilidades | 36 | 23 |
| Critical | 0 | 0 |
| High | 4 | 0 |
| Medium | 29 | 20 |
| Low | 3 | 3 |
| Pacotes vulneráveis | 14 | 10 |
| Pacotes indexados | 176 | 176 |
| Registros SPDX | 236 | 236 |

Foram removidas 13 ocorrências, sem novas ocorrências na comparação: cinco dos frameworks, uma do APT e sete do PCRE2 (CVE-2026-103111, 86145, 89156, 89157, 89158, 89160 e 89161). PCRE2 passou a 10.42-4ubuntu2.2.

A imagem de validação B4.5 foi `seenit-api:b05-b45-validation-20261117`, ID `sha256:c4a5d1d07d8e7b929456e900f64f11ca703eed151785bd326c14a2b714f15f04`. Seu scan manteve as mesmas 23 ocorrências da candidata. O resultado depende do conteúdo e da base de vulnerabilidades consultados naquela data.

## 8. Checks e testes locais

Evidências locais das etapas de experimento e aplicação, e não testes novamente executados no encerramento:

- Build completo com restore, verify, 16 grupos AvatarChecks e CoreChecks de watchtime, cache/retry de anime e 72 casos HTTP de cadastro aprovados; auditoria NuGet sem alertas nos três projetos verificados.
- JPEG, PNG e WebP reais, resize/reencode, entradas inválidas e limites de upload verificados. Harness complementar validou rejeição de WebP animado e bloqueio/cancelamento/liberação do semáforo de decodificação. A primeira fixture APNG inválida foi substituída e não foi contada como regressão nem como aprovação da API.
- `ldd` resolveu as dependências nativas sem `not found`; CA com permissão 0444; execução não-root UID 1654; `/app` sem escrita pelo usuário da aplicação; SDK, executáveis de checks e SQLite de testes ausentes da imagem final.
- Smoke local em Production, rede desabilitada, sem portas publicadas e com valores sintéticos: escuta interna em 18081, `/health` HTTP 200, ausência dos valores sintéticos nos logs e encerramento normal com código 0, sem OOM.

O smoke não validou banco, storage, proxy real ou serviços externos. `/health` não consulta banco/storage; seu HTTP 200 não comprova essas integrações. A etapa B4.8 verificou Git, arquivos integrados e consistência documental, sem rebuild ou novos testes funcionais.

## 9. PR #5 e commits

[PR #5](https://github.com/GiovannaGrigo/SeenIt/pull/5): estado **MERGED**, confirmado pelo Codex via GitHub CLI. Integração registrada em `2026-11-17T17:04:23Z` (14:04:23 de São Paulo).

| Commit | Finalidade |
| --- | --- |
| `a3ade1d3c63881264fb9af637bd42963552fe44f` | Fix: Atualiza imagens base Docker para corrigir vulnerabilidades |
| `35d999d4a8bd0fca4fd746378997cbf19b686173` | Docs: Documenta auditoria e atualização de segurança Docker |

O Codex confirmou ambos como ancestrais de `origin/main`. O PR incluiu exatamente cinco arquivos:

1. `backend/SeenIt.Api/Dockerfile` — duas linhas substituídas.
2. `docs/B05-B4-CONTAINER-SECURITY-DIAGNOSIS.md`.
3. `docs/B05-B4-CONTAINER-SBOM-ANALYSIS.md`.
4. `docs/B05-B4-CONTAINER-CVE-AUDIT.md`.
5. `docs/B05-B4-CONTAINER-BASE-EXPERIMENT.md`.

Este relatório final não integra o PR #5 e permanece sem commit.

## 10. SHA da integração e sincronização

SHA de merge: **`0720353be82f9af2a8d5f80240d57839121d844e`**.

O Codex executou `git fetch origin`, verificou árvore de trabalho limpa e ancestralidade e atualizou `main` por `git merge --ff-only origin/main`, de `2cd74b9` para `0720353`. `main`, HEAD e `origin/main` apontam para o mesmo SHA. A igualdade refere-se ao estado remoto observado neste fetch.

O Dockerfile e os quatro relatórios na `main` são idênticos aos da branch de implementação. Não foram usados reset, clean, rebase ou force.

## 11. Evidências de deploy informadas pelo operador

**Fonte: confirmação do operador, não inspeção remota do Codex.** O operador informou que executou Deploy Latest Commit no Render com sucesso, que o serviço foi declarado Live e que iniciou em Production na porta 10000.

O repositório contém configuração compatível com execução em Production e seleção da porta via `PORT`. Essa compatibilidade não comprova qual imagem ou configuração foi efetivamente executada pelo Render. Nenhum painel Render foi acessado e nenhum deploy foi executado pelo Codex nesta etapa.

## 12. Testes funcionais pós-deploy

**Aprovados e informados pelo operador:** login, cadastro, perfil, upload e substituição de avatar, catálogo e biblioteca. O Codex registra essas confirmações sem atribuir a si a execução dos testes de produção.

## 13. Limitações de verificação em produção

Não foi fornecida comprovação suficiente do SHA efetivamente implantado, do digest/ID da imagem executada ou de `dotnet --list-runtimes` dentro do Render. Não foram disponibilizados, nesta consolidação, logs remotos completos que estabeleçam essa cadeia de identidade.

O merge confirmado, o relato de Deploy Latest Commit e os testes funcionais relatados não substituem essa comprovação. SDK 10.0.404 e runtime 10.0.15 foram verificados localmente nas etapas anteriores; não são apresentados como versões independentemente verificadas no Render.

A auditoria local Docker Scout não equivale automaticamente à auditoria da imagem publicada no Render. Não há novo SBOM/scan de produção, verificação remota de secrets, persistência, proxy ou equivalência binária.

## 14. As 23 vulnerabilidades remanescentes

Inventário residual resumido do scan local, sem ocultar ocorrências por aparente baixa aplicabilidade:

| Componente / versão final observada | Medium | Low | Triagem pendente |
| --- | ---: | ---: | --- |
| glibc 2.39-0ubuntu8.9 | 6 | 0 | CVE-2026-18374, 84243, 8674, 86805, 95818, 97399; patch 8.12 indicado, não instalado |
| Perl 5.38.2-3.2ubuntu0.6 | 1 | 0 | CVE-2026-82560; patch 0.7 indicado, não instalado no runtime final |
| GnuTLS 3.8.3-1.1ubuntu3.6 | 6 | 0 | CVE-2026-67693, 88647, 88648, 95184, 95209, 95210; validar alcance e qualidade do feed |
| dash 0.5.12-6ubuntu5 | 2 | 0 | CVE-2026-102473 e 102474; cenários de shell/glob hostis |
| tar 1.35+dfsg-3ubuntu0.4 | 2 | 0 | CVE-2026-18477 e 18508; arquivos/restore hostis |
| dpkg 1.22.6ubuntu6.6 | 1 | 0 | CVE-2026-2219; pacote zstd/deb hostil |
| util-linux 2.39.3-9ubuntu6.6 | 1 | 0 | CVE-2026-76642; hooks/privilégios de montagem |
| systemd 255.4-1ubuntu8.18 | 1 | 1 | CVE-2026-121876 e 40228; cenários de VM confidencial/GPT e journald/terminais |
| shadow 1:4.13+dfsg1-4ubuntu3.2 | 0 | 1 | CVE-2024-56433; sobreposição subuid/NFS |
| ICU 74.2-1ubuntu3.1 | 0 | 1 | CVE-2025-5222; genrb |
| **Total** | **20** | **3** | **23 ocorrências** |

Os mecanismos e pré-condições completos estão no relatório do experimento e na auditoria. Vários não foram demonstrados no caminho da API; isso não equivale à inexistência de risco. CVE-2026-97399 descreve Power8, diferente do alvo amd64. CVE-2026-95209 permanecia no Scout apesar de descrição de rejeição pelo CNA; foi mantida na contagem. Ausência de patch informado pelo scanner não prova ausência de mitigação disponível.

## 15. SkiaSharp e codecs

SkiaSharp 4.155.0 foi preservado. O binário `libSkiaSharp.so` manteve o mesmo hash no experimento; os testes de decodificação/reencodificação e dependências nativas passaram.

O inventário não expôs todas as revisões dos codecs compilados estaticamente. Ausência de alerta específico não comprova segurança desses parsers. Mapear codecs e revisões upstream permanece ação separada; não se atribuem CVEs Chromium/Skia por suposição.

## 16. BuildHost e Roslyn

BuildHost-net472, BuildHost-netcore, Microsoft.Build.Locator/Roslyn e auxiliares continuam na publicação. Sua necessidade e possível remoção exigem análise e experimento próprios de publish/tooling. Não foram removidos nesta intervenção e não devem ser confundidos com SDK instalado na imagem final.

## 17. Warnings do Render

**Fonte: avisos observados e informados pelo operador; sem reprodução ou inspeção remota independente nesta etapa.** Cada item constitui pendência de investigação separada:

| Aviso | Investigação futura |
| --- | --- |
| DataProtection: chaves potencialmente não persistentes | Confirmar local de armazenamento, persistência entre deploys/reinícios e impacto sobre cookies/tokens protegidos |
| DataProtection: ausência de encryptor XML em repouso | Avaliar proteção das chaves, permissões, armazenamento e gerenciamento de chaves adequado ao ambiente |
| `HTTP_PORTS` sobrescrito por `URLS` | Confirmar precedência e binding efetivos; o repositório configura URL a partir de `PORT`, mas isso não prova o estado remoto |
| HTTPS redirection sem porta identificada | Verificar terminação TLS, cabeçalhos encaminhados e confiança no proxy real antes de propor ajuste |

Não foram alterados `Program.cs`, Data Protection, proxy, portas ou redirecionamento. Os avisos não foram suprimidos artificialmente. Não se conclui exposição ou inofensividade apenas pela presença de cada warning.

## 18. Política futura de atualização dos digests

Adotar revisão periódica das bases e revisão extraordinária diante de advisories relevantes. Cada atualização deve registrar data, origem oficial, arquitetura, digest anterior/novo, SDK/frameworks e pacotes efetivamente instalados.

Antes de integrar: comparar o mesmo código, executar build/checks e smoke, gerar SBOM e Scout, avaliar novos alertas e remanescentes e manter PR de escopo restrito. Fixar digest exige renovação explícita; não confiar na movimentação da tag. Para produção, registrar SHA e identidade da imagem implantada e correlacionar a auditoria com esse artefato.

## 19. Riscos e rollback

Permanecem as 23 ocorrências locais, limitações do inventário de codecs, conteúdo BuildHost/Roslyn, warnings e ausência de comprovação da identidade/runtime em produção. Os testes cobrem cenários definidos e não substituem testes de carga, fuzzing ou análise completa de produção.

Se necessário, planejar reversão controlada somente da alteração das bases, com novo build, checks, revisão e autorização operacional. Reverter pode reintroduzir CVEs. A imagem histórica sem a CA não é rollback equivalente automático. Nenhum rollback, commit, push ou deploy foi realizado neste encerramento.

## 20. Próximas ações de hardening e conferência final

Pendências separadas: obter evidência do SHA/digest e runtime implantados e auditar o artefato correspondente; triar os 23 remanescentes e patches efetivamente incorporados; mapear Skia/codecs; avaliar redução de BuildHost/Roslyn; investigar individualmente os quatro warnings; formalizar a renovação de digests. Nenhuma dessas ações foi implementada nesta etapa.

Conferências do Codex: PR MERGED; dois commits integrados; `main` sincronizada com `origin/main`; Dockerfile com os dois digests aprovados; quatro relatórios anteriores preservados; PR com exatamente cinco arquivos. A branch `security/b05-b4-container-review` foi preservada em `35d999d4a8bd0fca4fd746378997cbf19b686173`.

O worktree principal permanece em `main`. O worktree `.worktrees/maintenance-phase1` e sua branch foram preservados em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, sem intervenção nesta etapa. Nenhuma branch foi excluída. A única adição documental desta etapa é este arquivo, sem commit; nenhum arquivo funcional foi alterado. Não houve push, deploy, migrations ou acesso a Render, Supabase ou Vercel.

**B05-B4 PRONTO PARA ENCERRAMENTO**
