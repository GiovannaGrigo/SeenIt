# B06-B — Consolidação e encerramento documental da segurança Docker

Data: **23/11/2026**, America/Sao_Paulo. Escopo: confirmar a integração, sincronizar main por fast-forward e consolidar evidências locais e relatos operacionais. Nenhuma nova correção, teste funcional ou ação remota de infraestrutura foi executada nesta etapa.

## 1. Objetivo

Encerrar documentalmente a atualização controlada da imagem-base ASP.NET do SeenIt, preservando a distinção entre:

- **Verificado pelo Codex nesta etapa:** Git/GitHub, merge, commits, arquivos integrados, digests no Dockerfile, integridade dos relatórios e preservação das worktrees.
- **Evidência local histórica:** builds, testes e scans executados em B06-B1/B2/B3, com datas e artefatos próprios; não repetidos no B06-B6.
- **Fornecido pelo operador:** build/deploy Render e funcionamento pós-deploy, consolidados no pedido B06-B6.
- **Inferência ou ponto não verificado:** compatibilidade entre código integrado e comportamento relatado não comprova a identidade binária nem o inventário de vulnerabilidades da imagem em produção.

## 2. Histórico B06-B1 até B06-B6

| Etapa | Resultado e fonte |
| --- | --- |
| B06-B1 | Inventário local em 19/11/2026: 24 alertas, 0 Critical, 0 High, 20 Medium e 4 Low na imagem histórica B05-B4; sete correções disponíveis via base ASP.NET |
| B06-B2 | Experimento local com código/contexto/SDK iguais: controle 24, candidata 17, sem CVEs exclusivos da candidata; checks Linux/amd64 aprovados |
| B06-B3 | Atualização de uma linha do Dockerfile original; build sem cache, testes e scan da imagem final em 23/11/2026: 17 alertas, sem regressão identificada na cobertura executada |
| B06-B4 | Branch específica, dois commits e PR #7 com quatro arquivos; Codex não realizou merge ou deploy nessa etapa |
| B06-B5 | Operador informou novo build/deploy no Render, status Live e testes funcionais pós-deploy aprovados; fatos operacionais não verificados remotamente pelo Codex |
| B06-B6 | GitHub confirmou PR #7 MERGED; main local sincronizada por fast-forward; esta consolidação criada sem commit/push/deploy |

Relatórios preservados: [B06-B1](B06-B1-DOCKER-VULNERABILITY-INVENTORY.md), [B06-B2](B06-B2-DOCKER-RUNTIME-EXPERIMENT.md) e [B06-B3](B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md). B06-B5 é documentado aqui a partir das informações operacionais fornecidas no pedido, sem criar novo relatório B5 ou reproduzir logs brutos.

## 3. Estado do Git

Estado inicial: branch `security/b06-b4-docker-runtime`, árvore limpa, HEAD `cc5eb64b4ad36c734159e483e578345032ac3b61`; main local e referência local origin/main ainda em `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91`. Branches, worktrees, status e hashes dos arquivos preexistentes foram registrados antes da sincronização.

O `git fetch origin` atualizou origin/main para `0afd3233debd2781b61baa97ccfd738c2dc530fb`. Após confirmação do merge no GitHub, ancestralidade dos dois commits, diff autorizado e árvore limpa, foram executados `git switch main` e `git merge --ff-only origin/main`. A operação foi um fast-forward local de uma integração já existente, sem criar novo commit ou realizar merge do PR pelo Codex.

Estado final: **HEAD = main = origin/main = `0afd3233debd2781b61baa97ccfd738c2dc530fb`**, divergência `0 0`. A igualdade com o remoto refere-se ao estado observado no fetch desta etapa. Único arquivo novo/não rastreado: `docs/B06-B-FINAL-SUMMARY.md`.

## 4. PR #7 e commits

[PR #7 — Fix: Atualiza imagem ASP.NET para reduzir vulnerabilidades Docker](https://github.com/GiovannaGrigo/SeenIt/pull/7), de `security/b06-b4-docker-runtime` para `main`, confirmado via GitHub CLI como **MERGED**.

| Commit completo | Conteúdo |
| --- | --- |
| `ddf56a542ce25a3feca6ce3497bdeb4cdb78cf6f` | Fix: Atualiza imagem ASP.NET para corrigir vulnerabilidades Docker — somente Dockerfile |
| `cc5eb64b4ad36c734159e483e578345032ac3b61` | Docs: Documenta diagnóstico e atualização de segurança Docker — três relatórios |

Os dois commits são ancestrais de origin/main, verificados com Git. Os quatro arquivos informados pelo GitHub coincidem com o diff da integração contra seu primeiro pai:

1. `backend/SeenIt.Api/Dockerfile` — uma inserção e uma remoção.
2. `docs/B06-B1-DOCKER-VULNERABILITY-INVENTORY.md`.
3. `docs/B06-B2-DOCKER-RUNTIME-EXPERIMENT.md`.
4. `docs/B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md`.

Este relatório final não integra o PR #7 e permanece sem commit.

## 5. SHA da integração

SHA confirmado pelo GitHub e pelo Git: **`0afd3233debd2781b61baa97ccfd738c2dc530fb`**.

Pais do merge: `920f2d0ffb183ce6bfa52e7bc0e2177331dd4b91` e `cc5eb64b4ad36c734159e483e578345032ac3b61`. GitHub informa integração em **23/11/2026 às 18:41:21 UTC**, correspondente a **15:41:21 de São Paulo**.

O SHA foi consultado, não deduzido do relato de deploy. A integração Git não identifica por si a imagem ou o SHA efetivamente executados no Render.

## 6. Alteração técnica aplicada

Única alteração funcional integrada: substituição do digest ASP.NET no FROM `runtime-base`:

```diff
-FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5 AS runtime-base
+FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde AS runtime-base
```

O Dockerfile integrado coincide com o commit técnico e com a implementação B06-B3. Program.cs, SDK, `.dockerignore`, runtime-settings.json, csproj, NuGet, SkiaSharp, JWT, frontend, migrations e scripts de infraestrutura não foram alterados pelo PR. A comparação completa dos caminhos do diff corrobora esse escopo; blobs de Program.cs, csproj da API, configuração sanitizada, `.dockerignore` e CA também foram conferidos diretamente contra a base anterior.

A opção B06-A `ReverseProxy:HttpsRedirectionHandledByEdge` permanece em Program.cs. A atualização da base não modifica confiança em proxies, comportamento funcional de HTTPS ou autenticação.

## 7. Versões e digests

| Base integrada | Referência fixada | Versões verificadas localmente em B06-B2/B3 |
| --- | --- | --- |
| SDK, preservado | `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` | SDK 10.0.404 |
| ASP.NET atualizado | `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde` | Microsoft.NETCore.App e Microsoft.AspNetCore.App 10.0.15 |

Noble e Linux/amd64 são o sistema/plataforma validados no experimento e na implementação. O Dockerfile mantém publish linux-x64, `USER app`, porta 10000, entrypoint `dotnet SeenIt.Api.dll`, estágios verify e a CA pública em `/app/certs/prod-ca-2021.crt`, com cópia 0444 e verificações de leitura como app. O SDK completo/checks não são copiados para a imagem final pelo fluxo validado.

| Componente no runtime final local | Antes | Depois |
| --- | --- | --- |
| libc6/libc-bin — fonte glibc | 2.39-0ubuntu8.9 | 2.39-0ubuntu8.12 |
| perl-base — fonte perl | 5.38.2-3.2ubuntu0.6 | 5.38.2-3.2ubuntu0.7 |

Versões de binários e UID 1654 foram medidas nas etapas locais anteriores; não houve nova execução de container/listagem de runtime no B06-B6 e não se apresentam como inspeção independente do Render. Manifesto de plataforma, índice da imagem final e config digest são objetos distintos.

## 8. Comparação Docker Scout

**Evidências históricas locais, não scans repetidos nesta etapa.**

| Medida | Controle B06-B2 | Candidata B06-B2 / implementação B06-B3 |
| --- | ---: | ---: |
| Critical | 0 | 0 |
| High | 0 | 0 |
| Medium | 20 | 13 |
| Low | 4 | 4 |
| Total | **24** | **17** |
| Pacotes vulneráveis | 11 | 9 |
| Pacotes indexados | 176 | 176 |

Redução medida de **sete alertas médios**, sem CVEs exclusivos da candidata ou mudanças de severidade dos comuns na comparação local. B06-B1/B2 usaram Scout 1.27.0 em 19/11/2026. B06-B3 usou Scout **1.28.0** em 23/11/2026 e reanalisou a candidata B06-B2 com essa mesma versão, obtendo a mesma lista de 17.

Imagem final B06-B3 analisada: `sha256:2bfa34591ffc829dc20ca21cd1b1f2abacb57078a3ec338d909c1498dcff2c15`, tag local `seenit-api:b06-b3-validation-a740f09b`. Não houve filtros adicionais, ignores ou supressões para produzir a redução. O scanner não informou revisão/timestamp integral de sua base.

Esses números não são uma medição atual de produção nem um scan da imagem do Render. O B06-B6 não consultou novamente o Scout.

## 9. CVEs corrigidas

Os sete alertas presentes no controle e ausentes na candidata/implementação locais, com patches confirmados no conteúdo final:

| CVE | Scout no controle | Componente / versão corrigida incorporada |
| --- | --- | --- |
| CVE-2026-18374 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-84243 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-8674 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-86805 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-95818 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-97399 | Medium | glibc 2.39-0ubuntu8.12 |
| CVE-2026-82560 | Medium | perl/perl-base 5.38.2-3.2ubuntu0.7 |

“Corrigida” refere-se à versão incorporada e ao resultado do scan local, não à demonstração de explorabilidade anterior ou de patch efetivo em produção. Por exemplo, a descrição Power8 de CVE-2026-97399 não demonstra alcance no alvo amd64. Permanecem as qualificações de aplicabilidade dos relatórios anteriores.

## 10. Alertas residuais

Inventário residual da medição B06-B3: **13 Medium e 4 Low**, sem novas correções propostas nesta consolidação.

| Componente / versão local | Identificadores residuais | Scout |
| --- | --- | --- |
| gnutls28 3.8.3-1.1ubuntu3.6 | CVE-2026-67693, CVE-2026-88647, CVE-2026-88648, CVE-2026-95184, CVE-2026-95209, CVE-2026-95210 | 6 Medium |
| dash 0.5.12-6ubuntu5 | CVE-2026-102473, CVE-2026-102474 | 2 Medium |
| tar 1.35+dfsg-3ubuntu0.4 | CVE-2026-18477, CVE-2026-18508 | 2 Medium |
| dpkg 1.22.6ubuntu6.6 | CVE-2026-2219 | Medium |
| util-linux 2.39.3-9ubuntu6.6 | CVE-2026-76642 | Medium |
| systemd 255.4-1ubuntu8.18 | CVE-2026-121876; CVE-2026-40228 | Medium + Low |
| shadow 1:4.13+dfsg1-4ubuntu3.2 | CVE-2024-56433 | Low |
| icu 74.2-1ubuntu3.1 | CVE-2025-5222 | Low |
| gnupg2 2.4.4-2ubuntu17.6 | CVE-2026-105712 | Low |

Os relatórios preservam os achados por pacote-fonte, a descrição de rejeição de CVE-2026-95209 e as disputas/limites de aplicabilidade GnuTLS. Não foram ocultados alertas por baixa utilização aparente. `not fixed` no scanner não comprova inexistência universal de patch; não houve nova pesquisa de correções no B06-B6.

## 11. Testes locais

Resultados documentados nas etapas B06-B2/B3, **não reexecutados no encerramento**:

- Build Release Linux/amd64, sem cache nos RUN de validação, restore/publish e verify obrigatório aprovados; CA legível como app, modo 0444.
- AvatarChecks: **16 grupos**; CoreChecks com watch time, classificação/cache/retry e **72 casos HTTP de validação de cadastro**.
- RegisterIntegrationChecks: **73 casos integrados de cadastro**, além de integridade, catálogo/biblioteca, login/perfil, rate limiting, headers falsificados, concorrência e cancelamento.
- EntryPointChecks: **174/174 asserções**, seis perfis e 72 requests da matriz; JWT válido/inválido/expirado, opção HTTPS ausente/false/true, health, autorização e simulação de borda.
- SkiaSharp: JPEG/PNG/WebP reais, detecção, resize/reencode, rejeições de animação/entradas inválidas, limites de tamanho/dimensões/pixels e gate/cancelamento/liberação; ldd sem dependências ausentes.
- Smoke da imagem final com rede none, dados sintéticos e sem publicação de portas: Production, usuário app UID 1654, binding interno 10000, health 200, encerramento exit 0 e sem OOM.
- Os **70 arquivos regulares publicados** no B06-B3 foram idênticos por SHA-256 aos da candidata B06-B2. SDK completo/executáveis de checks/SQLite de testes ausentes do conteúdo final verificado; auxiliares BuildHost permaneceram.

Os testes funcionais foram Linux/amd64; host Windows foi usado para orquestração e comparações. Banco SQLite em memória e doubles substituíram PostgreSQL/Storage reais. Não houve migration de produção. A aprovação local não certifica integrações remotas, fuzzing ou ausência de todas as falhas nativas.

## 12. Evidências operacionais B06-B5

**Fonte: informações fornecidas pelo operador no pedido B06-B6, sobre logs/deploy datados de 23/11/2026. Não são inspeção remota do Codex.** Nesta consolidação foi recebida a descrição textual desses fatos; não foram buscados logs ou configurações no Render nem reproduzidos logs brutos.

O operador informou novo build Docker com etapa final concluída, exportação da imagem, envio de camadas e exportação de cache. Relatou usuário app UID 1654, CA legível com permissão 0444, inicialização em Production, binding HTTP interno na porta 10000 e serviço Render **Live**.

O Dockerfile integrado e os testes locais são compatíveis com esses comportamentos. Essa compatibilidade é uma **inferência de consistência**, não prova da identidade do artefato remoto. Não se verificaram independentemente SHA implantado, digest da imagem em execução, digest-base efetivamente usado, versões internas ou configurações/capabilities remotas.

## 13. Resultados funcionais pós-deploy

**Aprovados e informados pelo operador**, sem execução remota pelo Codex nesta etapa:

- Health, login, cadastro e perfil.
- Upload e substituição de avatar.
- Catálogo e biblioteca.
- HTTPS funcionando sem loops.
- Nenhum novo erro observado nos logs durante a janela de validação relatada.

Essas confirmações registram a cobertura funcional observada pelo operador. Não significam teste exaustivo de segurança, todas as condições de erro ou monitoramento contínuo. O health isoladamente não comprova banco/Storage; os fluxos adicionais foram relatados separadamente.

## 14. Warnings conhecidos

Segundo o operador, **`Failed to determine the https port for redirect` não apareceu no trecho de logs apresentado**. Isso é compatível com a configuração HTTPS B06-A já existente; a ausência nesse conjunto de logs não garante ausência futura nem foi atribuída como correção da base ASP.NET.

Permanecem warnings já conhecidos:

1. Data Protection: diretório de chaves sem persistência garantida além do ciclo de vida do container.
2. Data Protection: ausência de encryptor XML para proteção das chaves em repouso.
3. Precedência HTTP_PORTS/URLS, compatível com seleção explícita da URL de escuta.

Não foram declarados corrigidos pela atualização do runtime, nem foram alteradas configurações para suprimi-los. Persistência/proteção efetiva de chaves e topologia de proxy não foram inspecionadas remotamente.

## 15. Limitações e riscos

- Os 17 alertas pertencem à imagem final **local** da medição B06-B3 e dependem do scanner/feed utilizados. Não há scan direto da imagem produtiva nem base para afirmar exatamente 17 vulnerabilidades verificadas no Render.
- Merge confirmado e novo build relatado são evidências relevantes, mas não equivalem a inspecionar o conteúdo da imagem em execução ou provar equivalência binária/local-remota.
- PostgreSQL e Supabase reais não integraram as suítes isoladas. O sucesso funcional pós-deploy foi informado pelo operador, sem acesso do Codex a produção.
- A ausência de Critical/High no inventário local não elimina risco. Alcance efetivo de funções/utilitários, descrições contestadas/rejeitadas e códigos estáticos continuam com limitações documentadas.
- SkiaSharp/codecs estáticos permanecem B06-C; BuildHost/Roslyn permanecem B06-D. Não foram misturados à correção de base.
- Data Protection permanece pendência independente. Mudanças de proxy/ingresso podem exigir reavaliação de HTTPS, Scheme e confiança explícita.
- Digests fixados não se atualizam automaticamente. Mudanças futuras nas bases ou no feed podem alterar o inventário; novas análises devem registrar artefato, data, versão da ferramenta e resultados medidos.
- Não foi realizada auditoria remota/exaustiva de segredos. Nenhuma credencial, configuração real ou log bruto sensível foi copiado para esta consolidação.

## 16. Estratégia de rollback

Plano futuro, **não executado**:

1. Identificar o deploy anterior estável e registrar seu vínculo a código/artefato/configuração, sem presumir identidade pelo nome da tag.
2. Avaliar restauração controlada do digest ASP.NET anterior `sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5`, mantendo SDK e código funcional inalterados.
3. Preparar alteração específica de uma linha e executar build Linux/amd64, verify e testes relevantes, com revisão antes de qualquer publicação.
4. Quando houver autorização própria para ação remota, validar health, autenticação, perfil/avatares, catálogo/biblioteca e HTTPS após o procedimento.
5. Registrar que restaurar a base anterior reintroduz os sete alertas corrigidos e reavaliar o risco versus a regressão que motivaria o retorno.

Não usar reset/clean/rebase/force, migrations ou restauração de banco como parte dessa troca. Nenhum rollback, alteração Render ou deploy foi realizado no B06-B6.

## 17. Próximas frentes de hardening

Priorizar a investigação própria **B06-C** de SkiaSharp/codecs e **B06-D** de BuildHost/Roslyn, sem supor correções antes de comprovar versão, presença, alcance e compatibilidade. Manter triagem dos 17 alertas residuais e política de renovação controlada das bases com testes e scans datados.

Registrar também **B06-E** na sequência das próximas frentes, com escopo a confirmar antes da implementação; as evidências disponíveis não definem uma correção específica para essa designação.

Tratar Data Protection separadamente, começando por consumidores/requisitos de persistência e proteção. Se for necessário comprovar inventário de produção, obter em etapa autorizada identidade do artefato em execução e análise desse artefato, sem converter o scan local em certificação remota. Não modificar configurações apenas para ocultar warnings.

## 18. Preservação do repositório

Worktrees registrados:

| Caminho | Branch ao final | HEAD ao final |
| --- | --- | --- |
| `C:/Users/giovana.grigolato/Downloads/seenit` | main | `0afd3233debd2781b61baa97ccfd738c2dc530fb` |
| `C:/Users/giovana.grigolato/Downloads/seenit/.worktrees/maintenance-phase1` | maintenance-phase1 | `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290` |

A branch `security/b06-b4-docker-runtime` permanece em `cc5eb64b4ad36c734159e483e578345032ac3b61`. As demais branches e a associação de maintenance ao seu worktree foram preservadas. Nenhum worktree foi criado, excluído ou recriado.

Maintenance mantém seus seis arquivos rastreados modificados preexistentes e as entradas não rastreadas Maintenance/, MaintenanceAdmin/, MaintenanceChecks/, MAINTENANCE-PHASE1.md, RENDER-FREE-OPERATIONS.md e operations/. A conferência final preserva **161/161 hashes**, HEAD e status dessa worktree.

Na principal, foram inventariados 239 arquivos preexistentes. Os três relatórios históricos passaram por checkout normal de Git durante switch/fast-forward, com conversão LF→CRLF na árvore de trabalho. Seus conteúdos e blobs Git são idênticos aos da implementação; a comparação normalizada confirma ausência de reescrita editorial. Os outros **236 arquivos** mantiveram seus hashes brutos.

| Relatório preservado | SHA-256 do blob Git integrado, igual ao commit documental |
| --- | --- |
| B06-B1 | `6de9e327eae8577dcc5dd0a5781a9fa9ffbcd9288a2fb76b325576ae0aa3c57c` |
| B06-B2 | `9c2f428f334e62413a91b2007434a3a228ffa40fd452d1efae75492ff46d7432` |
| B06-B3 | `66915af14170d72dce463b0b5fcd8e8094786ab74e182fdccadc830bd703851f` |

Snapshot inicial e conferência de integração/preservação foram mantidos somente em `%TEMP%/seenit-b06-b6-20261123-43e1d85a`, sujeitos à retenção do sistema. Única criação documental no repositório: **`docs/B06-B-FINAL-SUMMARY.md`**, não rastreada e sem commit. Nenhum arquivo funcional foi editado nesta etapa.

Não houve novo commit, push, PR, merge de PR, build/scan/teste funcional repetido, publicação de imagem, deploy, rollback, migration, acesso a PostgreSQL/Supabase de produção ou alteração de Render/Vercel/variáveis/infraestrutura. O único avanço de main foi o fast-forward autorizado da integração já confirmada. Nenhuma operação reset/clean/rebase/force ou exclusão de branch/worktree.

## 19. Conclusão

A integração do PR #7 foi confirmada e main está sincronizada. A mudança técnica permanece restrita ao digest ASP.NET aprovado; versões/digests e os três relatórios estão preservados. As medições locais documentam redução de 24 para 17 alertas, com testes aprovados e sem regressão identificada na cobertura executada.

O operador informou deploy Live e sucesso dos fluxos pós-deploy. Essas evidências apoiam o encerramento operacional relatado, sem substituir a identificação/inspeção da imagem em produção ou seu scan. Os 17 alertas locais, warnings e frentes B06-C/B06-D permanecem explicitamente registrados.

**B06-B PRONTO PARA ENCERRAMENTO**
