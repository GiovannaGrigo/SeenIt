# B05-B1 / B05-B1.1 — Correção controlada Angular: bloqueio ERESOLVE

Data: 13/11/2026 (America/Sao_Paulo). Branch: `b05-b1-angular-security`.

**Resultado: AJUSTES NECESSÁRIOS. Nenhuma atualização de dependências foi concluída.**

## Preparação e preservação

A branch foi criada a partir da main em `9c4fe3761c83bed72955b1d257d84e3c4836fa8b` no B05-B1. No início do B05-B1.1, a única alteração era o relatório B05-A não rastreado. Seu conteúdo foi preservado: SHA256 `525A7FA01C4B1983FD74EECC89B933B84AE337130717AD47A3DAB8280E1165D6`.

`maintenance-phase1` permaneceu em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`; seu worktree separado não foi alterado. Nenhuma operação na API, banco, Dockerfile ou Supabase. Sem commit, push ou deploy.

## Etapa 1 — Diagnóstico sem alterações

Inspecionados o manifesto, todas as entradas `@angular/*` do lockfile e seus manifestos instalados. `npm ls --all --json` retornou código 0 antes de qualquer edição. Node 24.16.0 e npm 11.13.0.

Foram lidos integralmente o relatório original de resolução e os trechos de resolução/erro do log npm:

- `%LOCALAPPDATA%/npm-cache/_logs/2026-11-13T18_11_36_279Z-eresolve-report.txt`
- `%LOCALAPPDATA%/npm-cache/_logs/2026-11-13T18_11_36_279Z-debug-0.log`

O conflito concreto reportado é entre o conjunto antigo e o solicitado: `@angular/platform-browser@21.2.23` declara peer opcional **exato** de `@angular/animations@21.2.23`; animations 21.2.26 exige **exatamente** core 21.2.26. Forms e router antigos também exigem platform-browser 21.2.23. Peer opcional não permite ignorar a versão quando o pacote está presente.

O resolvedor Arborist falhou em `PlaceDep.failPeerConflict` enquanto construía a árvore ideal, ainda exibindo platform-browser 21.2.23. Isso identifica a aresta conflitante, mas não demonstra uma incompatibilidade intrínseca entre as versões finais desejadas nem prova um defeito específico do npm. Não foi isolado se o fator determinante é a estratégia incremental, o lockfile ou outro comportamento interno do resolvedor. PrimeNG e CDK não exigem a versão antiga exata.

## Etapa 2 — Combinação verificada no registro npm

Consultados `peerDependencies` e `peerDependenciesMeta` de todos os dez pacotes diretos Angular desejados, CDK instalado e PrimeNG instalado, com `npm view <pacote>@<versão> ... --json`.

| Pacote | Original e final efetivo | Proposta não instalada |
| --- | --- | --- |
| @angular/animations | 21.2.23 | 21.2.26 |
| @angular/common | 21.2.23 | 21.2.26 |
| @angular/compiler | 21.2.23 | 21.2.26 |
| @angular/core | 21.2.23 | 21.2.26 |
| @angular/forms | 21.2.23 | 21.2.26 |
| @angular/platform-browser | 21.2.23 | 21.2.26 |
| @angular/router | 21.2.23 | 21.2.26 |
| @angular/compiler-cli | 21.2.23 | 21.2.26 |
| @angular/build | 21.2.24 | 21.2.30 |
| @angular/cli | 21.2.24 | 21.2.30 |
| @angular/cdk (peer instalado) | 21.2.14 | preservar 21.2.14 |
| primeng | 21.1.10 | preservar 21.1.10 |
| typescript | 5.9.3 | preservar 5.9.3 |

- Framework: os peers exatos entre core/common/forms/platform-browser/router/animations devem convergir em 21.2.26. Compiler-cli exige compiler 21.2.26; core aceita compiler opcional exatamente 21.2.26.
- Build 21.2.30 aceita framework/compiler-cli `^21.0.0` e TypeScript `>=5.9 <6.0`. Compiler-cli 21.2.26 aceita TypeScript `>=5.9 <6.1`; a interseção inclui 5.9.3.
- CDK 21.2.14 aceita common/core/platform-browser `^21.0.0 || ^22.0.0`; foi considerada exclusivamente a alternativa 21. Não é necessário alinhar seu patch ao framework.
- PrimeNG 21.1.10 aceita CDK `^21.0.0`, core `^21.0.7`, demais peers Angular `^21.0.0`; RxJS instalado 7.8.2 satisfaz os peers.
- CLI 21.2.30 não retornou peers. Peers opcionais de SSR, platform-server, service-worker, localize, runners e outros não foram adicionados: não são dependências instaladas deste projeto.

A combinação satisfaz os peers publicados consultados. Isso não equivale a uma instalação validada. O router proposto 21.2.26 estaria fora das três faixas do B05-A (`<21.2.24` e `<21.2.25`); o efetivamente instalado continua vulnerável segundo a auditoria.

## Etapa 3 — Tentativa controlada e interrupção

Depois da conferência de peers, todas as dez dependências diretas Angular foram temporariamente fixadas no package.json nas versões propostas, preservando dependencies/devDependencies. Em seguida:

```text
cd frontend
npm install --package-lock-only --ignore-scripts --no-audit --no-fund
```

Código 1, `ERESOLVE`: o resolvedor novamente manteve platform-browser 21.2.23 e seu peer de animations 21.2.23 na árvore candidata, confrontando animations/core solicitados em 21.2.26. Evidência local:

`%LOCALAPPDATA%/npm-cache/_logs/2026-11-13T18_15_58_826Z-eresolve-report.txt`.

Conforme a exigência de interromper se a combinação continuasse incompatível na resolução, nenhuma nova estratégia de instalação foi tentada. A edição temporária do manifesto foi revertida ao conteúdo de HEAD. O lockfile não foi excluído nem modificado; node_modules não foi removido. Não houve uso de force, legacy-peer-deps, overrides ou migração major.

## Etapa 4 — Validação e comparação

| Verificação | Resultado |
| --- | --- |
| npm ls --all --json antes da edição | Código 0, árvore instalada enumerada |
| npm install --package-lock-only | Código 1, ERESOLVE; instalação não realizada |
| npm audit --json --ignore-scripts após restaurar manifesto | Consulta concluída; 32 pacotes sinalizados |
| npm run build / compilação Angular | Não executado nesta etapa: não houve atualização bem-sucedida |
| Verificação TypeScript / budgets | Não executada nesta etapa |
| Testes automatizados | Não executados nesta etapa |
| Compatibilidade PrimeNG/CDK | Peers publicados compatíveis; validação de build pós-atualização pendente |

| Severidade | Antes (B05-A e B05-B1) | Depois B05-B1.1 |
| --- | ---: | ---: |
| Crítica | 3 | 3 |
| Alta | 9 | 9 |
| Moderada | 20 | 20 |
| Total | 32 | 32 |

Nenhuma vulnerabilidade foi corrigida. A contagem é de pacotes sinalizados, incluindo propagação aos pais, e não de falhas independentes. Persistem todos os achados do B05-A, incluindo piscina, vite, os dois ramos de undici, MCP SDK, zod e proxy-addr. Ver o inventário completo em [B05-A](B05-DEPENDENCY-SECURITY-REVIEW.md).

Produção/aplicação: permanece o alerta de router, cujos três advisories do B05-A descrevem SSR; o projeto é SPA sem SSR e não foi demonstrado caminho de exploração na SPA. Build/desenvolvimento: permanecem os outros 31 pacotes sinalizados. Esses dois escopos compartilham a cadeia de ferramentas; não representam duas contagens independentes. As pendências podem afetar desenvolvedores/CI conforme uso e exposição. Não há nova alegação de segurança do artefato publicado.

## Riscos residuais, impacto e continuidade

Impacto esperado em produção: nenhum, pois manifesto, lockfile, código funcional e dependências instaladas foram preservados. Todos os pacotes oficiais `@angular/*` enumerados no lockfile e na instalação continuam na major 21; nenhum pacote Angular 22 ou superior foi instalado.

Rollback da tentativa já concluído: manifesto restaurado; lockfile e árvore instalada preservados. O único arquivo novo desta etapa é este relatório. Não é necessário resetar a branch nem tocar maintenance-phase1.

Para uma próxima etapa, é necessário isolar a resolução em uma cópia temporária de manifesto e lockfile, sem node_modules, para distinguir o estado instalado do lockfile; depois avaliar uma atualização explicitamente direcionada do conjunto de peers no resolvedor. Essa investigação não foi executada após o segundo ERESOLVE, respeitando a interrupção solicitada. Ainda serão necessários instalação bem-sucedida, npm ls, audit, build, TypeScript, budgets e checks existentes antes da aprovação.

**AJUSTES NECESSÁRIOS.**
