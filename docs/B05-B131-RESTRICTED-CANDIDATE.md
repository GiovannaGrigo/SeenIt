# B05-B1.3.1 — Candidata restrita Angular 21

Data: 13/11/2026 (America/Sao_Paulo). Branch: `b05-b1-angular-security`.

**CANDIDATA C APROVADA PARA APLICAÇÃO CONTROLADA.** A candidata satisfaz os critérios desta etapa: Angular exclusivamente 21, grafo obrigatório consistente, nenhuma vulnerabilidade crítica/alta na auditoria consultada, versões visuais e tslib originais preservadas, e diff menor que B. Esta aprovação autoriza uma recomendação técnica para a próxima etapa; não significa aplicação realizada ou aprovação de build, interface, commit ou deploy.

## 1. Artefatos e preservação

Candidata C criada fora do repositório, sem node_modules:

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b131-C-aVG9Oq
```

Origem B preservada:

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b12-rp5t1I\B-without-lock
```

Nenhuma instalação ou atualização foi executada no projeto original. O único arquivo novo no repositório nesta etapa é este relatório. Todos os relatórios anteriores foram preservados, assim como frontend/package.json, package-lock.json e node_modules. maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Nenhuma operação foi realizada em seu worktree. Sem código funcional, API, Docker, banco, Supabase, commit, push ou deploy.

## 2. Versões originais e mudanças de B

Foram lidos os manifestos e lockfiles original/B e conferidas todas as cópias dos onze pacotes solicitados. A tabela completa por caminho aparece no apêndice A.

B alterou apenas quatro dessas entradas: themes 3.0.1 → 3.1.1, styled da raiz 1.0.0 → 1.2.0, utils da raiz 0.8.2 → 0.8.3 e tslib 2.8.1 → 2.9.1. As demais cópias visuais, incluindo as aninhadas usadas pelo PrimeNG, CDK, RxJS e TypeScript, não haviam mudado.

C preserva **todas as versões solicitadas**, em seus caminhos originais. Version, resolved e integrity dessas entradas coincidem com o lockfile original. Nenhuma versão desse conjunto precisou ser atualizada para viabilizar Angular 21.2.26 / ferramentas 21.2.30.

## 3. Compatibilidade publicada

Antes da resolução, foram consultados com `npm view <pacote>@<versão> dependencies peerDependencies peerDependenciesMeta engines --json`: primeng 21.1.10; themes 3.0.1; styled 1.0.0 e 0.7.4; styles 2.0.3; utils 0.8.2, 0.7.2 e 0.6.4; motion 0.0.10; primeicons 8.0.2; CDK 21.2.14; RxJS 7.8.2; tslib 2.8.1; TypeScript 5.9.3; build 21.2.30 e compiler-cli 21.2.26.

| Pai publicado | Requisito relevante | Escolha C |
| --- | --- | --- |
| themes 3.0.1 | styled ^1.0.0 | styled 1.0.0 |
| styled 1.0.0 | utils ^0.8.0; license-manager ^1.0.0 | utils 0.8.2; license-manager 1.1.0 |
| primeng 21.1.10 | styled ^0.7.4; styles ^2.0.3; utils ^0.7.2; motion ^0.0.10; tslib ^2.3.0 | Todas as versões originais, com cópias aninhadas quando necessárias |
| styles 2.0.3 | styled ^0.7.4 | styled 0.7.4 aninhado |
| styled 0.7.4 | utils ^0.6.1 | utils 0.6.4 aninhado |
| motion 0.0.10 | utils ^0.6.3 | utils 0.6.4 aninhado |
| primeng 21.1.10, peers | CDK ^21.0.0; core ^21.0.7; common/forms/router/platform-browser ^21.0.0; RxJS ^6.0.0 ou ^7.8.1 | CDK 21.2.14; framework 21.2.26; RxJS 7.8.2 |
| CDK 21.2.14, peers | common/core/platform-browser ^21.0.0 ou ^22.0.0; RxJS ^6.5.3 ou ^7.4.0 | Exclusivamente a alternativa Angular 21; RxJS 7.8.2 |
| CDK 21.2.14 | tslib ^2.3.0 | 2.8.1 |
| RxJS 7.8.2 | tslib ^2.1.0 | 2.8.1 |
| build 21.2.30 | TypeScript >=5.9 <6.0; peer tslib ^2.3.0; framework/compiler-cli ^21.0.0 | TypeScript 5.9.3; tslib 2.8.1; framework/compiler-cli 21.2.26 |
| compiler-cli 21.2.26 | compiler 21.2.26; TypeScript >=5.9 <6.1; tslib ^2.3.0 | compiler 21.2.26; TypeScript 5.9.3; tslib 2.8.1 |

Não se tentou usar utils 0.8.2 para atender ^0.7.2 ou ^0.6.1. As cópias aninhadas preservam os contratos separados. Também não se tentou misturar styled 1.0.0 com themes 3.1.1, que exige ^1.2.0: o par themes/styled foi preservado conjuntamente. Essa compatibilidade foi confirmada na árvore final, não inferida apenas da versão do PrimeNG.

## 4. Estratégia de resolução isolada

Foi usada uma **cópia do manifesto e lockfile B**, com a árvore Angular já alinhada. Não se reutilizou o lockfile antigo para atualizar incrementalmente Angular, nem foram editadas manualmente entradas de pacotes/integridades do lockfile. Isso evita repetir a hipótese já refutada pelo ERESOLVE e limita deriva de outros pacotes.

No manifesto C, mantidos os dez Angular de B e fixadas versões exatas de dependências já existentes:

- Framework, animations e compiler-cli: 21.2.26.
- CLI/build: 21.2.30.
- primeng: 21.1.10; themes: 3.0.1; tslib: 2.8.1; typescript: 5.9.3.
- Os intervalos originais de primeicons (^8.0.2) e RxJS (~7.8.2) foram mantidos; o lockfile reteve 8.0.2 e 7.8.2.

### Primeira resolução: constraints de preparação

Adicionadas **somente no manifesto temporário de preparação** duas constraints diretas compatíveis: styled 1.0.0 e utils 0.8.2. Elas são versões admitidas por themes/styled originais. Não são dependências adicionais propostas para o projeto final.

```text
npm install --package-lock-only --ignore-scripts --no-audit --no-fund
```

Código 0. Log em `resolution-bootstrap.log`, código em `resolution-bootstrap-exit.txt`. Cópias de evidência: `package.bootstrap.json` e `package-lock.bootstrap.json`.

### Segunda resolução: manifesto final sem dependências extras

Removidas do manifesto temporário as duas dependências diretas de preparação, mantendo o lockfile recém-resolvido. Executado novamente o mesmo comando, código 0, com log em `resolution-final.log` e código em `resolution-final-exit.txt`.

O npm reteve styled/utils originais como transitivos válidos. O manifesto final **possui exatamente os mesmos nomes de dependências diretas e seções dependencies/devDependencies do projeto original**. Não contém styled/utils diretos, overrides, force ou legacy-peer-deps. Nenhum pacote desnecessário foi promovido a dependência final para fixar o grafo.

Arquivos propostos, ainda apenas temporários: `package.json` e `package-lock.json`. `package.final.json` é uma cópia do manifesto final usado na resolução. Os arquivos bootstrap são evidência e não devem ser aplicados ao projeto.

## 5. Validação de C

| Verificação, executada somente no temporário | Resultado |
| --- | --- |
| npm install --package-lock-only --ignore-scripts --no-audit --no-fund, preparação | Código 0 |
| Mesmo comando após remover constraints temporárias | Código 0 |
| npm ls --all --package-lock-only | Código 0; `tree-final.log` |
| npm audit --json --ignore-scripts | Consulta válida; código 1 por 16 moderados; `audit-final.json` |
| Conferência semver de todas as arestas declaradas | Zero intervalos incompatíveis e zero dependências obrigatórias ausentes |
| Conferência manifesto versus raiz do lockfile | Dependencies/devDependencies idênticos |
| Conferência pacotes oficiais @angular/* | Todos na major 21; framework/compiler-cli 21.2.26, CLI/build 21.2.30, CDK 21.2.14 |
| Instalação física / node_modules | Não realizada; diretório node_modules ausente em C |
| Build, TypeScript, budgets e testes funcionais | Não executados nesta etapa, conforme escopo |

Os peers opcionais ausentes não foram tratados como dependências obrigatórias. A aprovação é da candidata resolvida, não de comportamento executável já verificado.

## 6. Comparação original / B / C

Contagem por **caminho de pacote**, excluindo a raiz. Atualizações de versão e metadados sem troca de versão são discriminados para não ocultar diferenças.

| Métrica contra o original | B | C |
| --- | ---: | ---: |
| Entradas adicionadas | 2 | 2 |
| Entradas removidas | 2 | 1 |
| Entradas com versão alterada | 116 | 112 |
| Entradas com apenas metadados alterados | 17 | 17 |
| Total de entradas existentes alteradas (versão + metadados) | 133 | 129 |
| Total de diferenças de pacotes (adições + remoções + alterações) | 137 | 132 |
| Mudanças de major em um mesmo caminho | 2 | 2 |
| Versões alteradas no conjunto solicitado de visual/runtime | 4 | 0 |
| Total de entradas de pacotes, além da raiz | 527 | 528 |

O original tem 527 entradas. C tem uma entrada a mais que B porque **preserva license-manager 1.1.0**, exigido pelo styled original. Isso não amplia o diff em relação ao original: recupera um pacote que B havia removido. Não foi reinstalado manualmente nem promovido a dependência direta.

Diferenças exatas B → C: themes 3.1.1 → 3.0.1; styled da raiz 1.2.0 → 1.0.0; utils da raiz 0.8.3 → 0.8.2; tslib 2.9.1 → 2.8.1; adição de license-manager 1.1.0. Nenhuma outra entrada de pacote de B mudou, inclusive metadados das demais entradas. A raiz também reflete as quatro constraints exatas nas dependências existentes.

### Mudanças residuais deliberadamente mantidas

- **content-disposition 1.1.0 → 2.0.1:** pai Express 5.3.0 exige ^2.0.1; a API do consumidor foi revisada no B05-B13. Escopo CLI/MCP, não servidor da SPA.
- **entities sob parse5 8.1.0 → 9.0.0:** pai parse5 8.1.1 exige ^9.0.0. Mantido o parser atualizado e suas correções publicadas, sem downgrade apenas para remover major transitiva. A revisão anterior descreve diferenças de escape/serialização e impacto potencial em build/CDK.
- Adições: @rollup/rollup-android-x64 4.68.3, opcional de plataforma; express/node_modules/content-type 2.1.0, exigido pelo Express atualizado.
- Remoção única: @napi-rs/lzma-linux-x64-gnu 1.5.1, que Rollup 4.68.3 deixou de declarar. Os binários principais de Rollup Linux permanecem no grafo.
- As 17 diferenças libc de opcionais nativos permanecem como B, com versões/tarballs inalterados. Requerem futura validação na plataforma Linux de CI.
- Outros patch/minor transitivos continuam como na árvore B revisada. Não se alega que todos sejam estritamente necessários ao Angular; foram preservados para limitar o escopo desta candidata à retirada da deriva visual/tslib sem uma terceira resolução global.

Inventário completo legível por máquina em `comparison-C.json` no diretório C, incluindo original → B, original → C e B → C. O apêndice A documenta todas as cópias do conjunto solicitado; o apêndice B registra as versões Angular finais.

## 7. Segurança

| Severidade de pacotes sinalizados | Original / B05-A | B | C |
| --- | ---: | ---: | ---: |
| Crítica | 3 | 0 | 0 |
| Alta | 9 | 0 | 0 |
| Moderada | 20 | 16 | 16 |
| Baixa | 0 | 0 | 0 |
| Total | 32 | 16 | 16 |

Contagem de pacotes sinalizados, incluindo alertas propagados aos pais; não são 16 falhas independentes. C tem o mesmo conjunto de pacotes sinalizados e os mesmos dois URLs GHSA de B, sem alerta novo. Não houve reintrodução de críticos/altos na auditoria consultada ao preservar o conjunto original. A sugestão automática de CLI 22.2.7 presente no audit não foi seguida.

Persistem, todos moderados e marcados dev: @angular/cli 21.2.30; @npmcli/package-json 7.0.5; @npmcli/run-script 10.0.4; @sigstore/sign 4.1.1; @sigstore/tuf 4.0.2; @tufjs/models 4.1.0; cacache 20.0.4; glob 13.0.7; ignore-walk 8.0.0; make-fetch-happen 15.0.6; minimatch 10.2.6; npm-packlist 10.0.4; npm-registry-fetch 19.1.1; pacote 21.5.1; sigstore 4.1.1; tuf-js 4.1.0.

Causas diretas: [glob GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), faixa >=9.0.0 <=14.0.1; [minimatch GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249), faixa >=7.0.0 <11.0.1. Demais alertas são propagados pela cadeia npm/CLI.

Produção/aplicação: nenhum pacote prod sinalizado no audit completo de C; router 21.2.26 permanece fora das faixas SSR relatadas no B05-A. Desenvolvimento/CI: 16 moderados residuais; build e desenvolvimento compartilham as ferramentas, sem contagem duplicada. O projeto publicado não foi alterado por este experimento.

### Risco conhecido fora da contagem npm

Preservar PrimeUIX antigo abre mão de correções publicadas nas versões posteriores, inclusive tratamento de valores de tokens CSS, conforme documentado no [B05-B13](B05-B13-LOCKFILE-REVIEW.md). O audit não é prova de ausência desse risco. A inspeção atual encontrou Aura estático e darkModeSelector .dark-theme em main.ts, sem usos de definePreset/updatePreset/usePreset/updatePrimaryPalette/updateSurfacePalette nem escrita de tokens CSS via setProperty no código TypeScript consultado. Não foi demonstrado caminho de entrada de tokens arbitrários por usuário nesse uso.

A preservação é uma redução de escopo em relação à candidata B, não uma declaração de que o runtime antigo é mais seguro. Esse risco preexistente permanece documentado; não houve alerta crítico/alto consultado nem novo caminho funcional introduzido nesta candidata. A atualização PrimeUIX pode ser tratada em etapa própria com revisão visual; esta candidata não deve ser usada para encerrar definitivamente a avaliação de segurança do tema.

## 8. Riscos de regressão e recomendação

As versões e integridades visuais foram preservadas, eliminando a deriva de pacotes visuais observada em B. Isso não garante pixel a pixel a interface após atualizar Angular e ferramentas: parser, bundler e compilador ainda mudam. Permanecem as duas majors transitivas revisadas e opcionais nativos. Riscos serão avaliados em build, budgets, checks e revisão de telas na etapa de aplicação.

Recomenda-se aplicar futuramente **o par final package.json/package-lock.json C**, após conferência de seus hashes, sem regenerar o lockfile por resolução limpa irrestrita. O lockfile preserva os transitivos originais de tema; uma atualização posterior ou remoção do lockfile pode voltar a atualizá-los pelos intervalos dos pais. Constraints temporárias/bootstrap não integram a proposta final.

Na etapa de aplicação ainda serão necessários instalação física, npm ls --all, audit atualizado, build de produção, TypeScript/Angular, budgets e testes existentes, além de compatibilidade PrimeNG em execução. Divergência ou falha nesses testes exige revisão, sem migrar major ou usar force. Não houve execução dessas ações agora.

## 9. Integridade da candidata e do original

SHA256 dos artefatos finais em C:

| Arquivo temporário | SHA256 |
| --- | --- |
| package.json | 1fe6d9183849cf494b6236a21842bd09dfe7720dea483eba54267b57444fbe38 |
| package-lock.json | 32a9d1bf185e2868c055d449bc6ca1b18a23b361ac6b1fbbd8bb06c5a6ae9e9e |
| audit-final.json | d7d8f8992a2bef3e365df30cb7de6b176fd7b7ddfbf7579fb128647b75d2c11c |

Manifesto original preservado: SHA256 `776c745a58b0f17f3063d8ea896f4f1b0f31d99f404b8859943a815009042f08`. Lockfile original preservado: SHA256 `2d2b3d0d9cef6fce4dc52c8169ff20fa6bf683191bcc1d269f68c2ba0cfa52af`. Os hashes de todos os relatórios anteriores foram comparados antes/depois desta etapa. Nenhum resultado temporário foi copiado para o frontend.


## Apêndice A — Versões exatas por caminho

Caminhos relativos a node_modules da raiz; os segmentos node_modules restantes indicam cópias aninhadas.

| Caminho | Original | B | C |
| --- | --- | --- | --- |
| @angular/cdk | 21.2.14 | 21.2.14 | 21.2.14 |
| @primeuix/motion | 0.0.10 | 0.0.10 | 0.0.10 |
| @primeuix/motion/node_modules/@primeuix/utils | 0.6.4 | 0.6.4 | 0.6.4 |
| @primeuix/styled | 1.0.0 | 1.2.0 | 1.0.0 |
| @primeuix/styles | 2.0.3 | 2.0.3 | 2.0.3 |
| @primeuix/styles/node_modules/@primeuix/styled | 0.7.4 | 0.7.4 | 0.7.4 |
| @primeuix/styles/node_modules/@primeuix/utils | 0.6.4 | 0.6.4 | 0.6.4 |
| @primeuix/themes | 3.0.1 | 3.1.1 | 3.0.1 |
| @primeuix/utils | 0.8.2 | 0.8.3 | 0.8.2 |
| primeicons | 8.0.2 | 8.0.2 | 8.0.2 |
| primeng | 21.1.10 | 21.1.10 | 21.1.10 |
| primeng/node_modules/@primeuix/styled | 0.7.4 | 0.7.4 | 0.7.4 |
| primeng/node_modules/@primeuix/styled/node_modules/@primeuix/utils | 0.6.4 | 0.6.4 | 0.6.4 |
| primeng/node_modules/@primeuix/utils | 0.7.2 | 0.7.2 | 0.7.2 |
| rxjs | 7.8.2 | 7.8.2 | 7.8.2 |
| tslib | 2.8.1 | 2.9.1 | 2.8.1 |
| typescript | 5.9.3 | 5.9.3 | 5.9.3 |

Complemento: @primeui/license-manager 1.1.0, ausente em B, foi preservado em C com version/resolved/integrity originais como dependência transitiva de styled 1.0.0.

## Apêndice B — Pacotes oficiais Angular em C

| Caminho | Versão |
| --- | --- |
| @angular/animations | 21.2.26 |
| @angular/build | 21.2.30 |
| @angular/cdk | 21.2.14 |
| @angular/cli | 21.2.30 |
| @angular/common | 21.2.26 |
| @angular/compiler | 21.2.26 |
| @angular/compiler-cli | 21.2.26 |
| @angular/core | 21.2.26 |
| @angular/forms | 21.2.26 |
| @angular/platform-browser | 21.2.26 |
| @angular/router | 21.2.26 |

**CANDIDATA C APROVADA PARA APLICAÇÃO CONTROLADA. Etapa encerrada sem aplicação automática.**
