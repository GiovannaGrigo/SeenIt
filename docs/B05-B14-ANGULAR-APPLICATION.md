# B05-B1.4 — Aplicação controlada da candidata C

Data: 13/11/2026 (America/Sao_Paulo). Branch: `b05-b1-angular-security`. HEAD preservado em `9c4fe3761c83bed72955b1d257d84e3c4836fa8b`.

**APROVADO PARA VALIDAÇÃO FUNCIONAL.** O par aprovado foi aplicado sem recalcular versões; instalação física, árvore instalada, auditoria, build de produção, TypeScript e três checks existentes passaram. Há um aviso não bloqueante de budget de estilo e 16 pacotes moderados residuais na cadeia de desenvolvimento. A interface ainda precisa de validação funcional/visual em navegador.

## 1. Conferência inicial e hashes

Branch conferida antes de qualquer cópia. O frontend não tinha alterações inesperadas. Os cinco relatórios anteriores estavam não rastreados e foram preservados; seus hashes foram registrados no início e comparados ao final. A worktree maintenance-phase1 tinha alterações locais preexistentes: foram registradas, sem descartá-las, e comparadas ao final por status, referência e hashes dos 15 arquivos alterados/não rastreados.

Diretório real da candidata, confirmado no relatório B05-B131 e no sistema de arquivos:

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b131-C-aVG9Oq
```

O caminho foi usado com `GIOVAN~1.GRI`, sem separador entre GIOVAN e ~1.GRI.

| Arquivo final | SHA256 esperado e verificado |
| --- | --- |
| package.json | 1fe6d9183849cf494b6236a21842bd09dfe7720dea483eba54267b57444fbe38 |
| package-lock.json | 32a9d1bf185e2868c055d449bc6ca1b18a23b361ac6b1fbbd8bb06c5a6ae9e9e |

Ambos os hashes foram conferidos no temporário, após copiar para frontend, depois de npm ci e na revisão final. Permaneceram idênticos. O lockfile não foi alterado pela instalação nem pelo build/checks.

Antes da aplicação, os hashes do manifesto e lockfile originais eram, respectivamente:

```text
776c745a58b0f17f3063d8ea896f4f1b0f31d99f404b8859943a815009042f08
2d2b3d0d9cef6fce4dc52c8169ff20fa6bf683191bcc1d269f68c2ba0cfa52af
```

## 2. Arquivos aplicados e escopo

Copiados exclusivamente `package.json` e `package-lock.json` finais de C para `frontend/`. Nenhum arquivo bootstrap, log ou comparison-C.json foi copiado para o projeto.

O conjunto de nomes de dependências diretas e sua divisão dependencies/devDependencies permanecem iguais ao original. Não há styled/utils promovidos a diretos nem overrides. Foram fixadas as dez versões Angular aprovadas e as versões existentes de PrimeNG, themes, tslib e TypeScript, conforme a candidata. Os intervalos de primeicons e RxJS permanecem como antes.

As 17 entradas visuais/runtime verificadas no lockfile, incluindo cópias aninhadas, permanecem integralmente iguais às originais. O diff do lockfile corresponde exatamente ao arquivo aprovado, já revisado em B05-B13/B05-B131: 112 alterações de versão, duas adições, uma remoção e 17 alterações apenas de metadados, excluindo a raiz. Persistem as duas majors transitivas revisadas (content-disposition e entities sob parse5), sem migrar Angular.

## 3. Instalação física

Dentro de frontend:

```text
npm ci --ignore-scripts --no-audit --no-fund
```

**Código 0**, 423 pacotes adicionados em 32 segundos no Windows. Não houve ERESOLVE, erro de integridade ou habilitação de scripts de instalação. A quantidade física não deve ser confundida com as 528 entradas do lockfile, que incluem pacotes opcionais de outras plataformas.

Aviso de instalação: `@angular/animations@21.2.26` está deprecated. O pacote aprovado foi preservado; não foi feita migração de API de animação nem atualização major. O link da mensagem aponta para documentação v22, mas isso não representa instalação de Angular 22.

Ambiente: Node 24.16.0, npm 11.13.0. Instalação acessou o registro npm com autorização de rede. Nenhum `npm install`, `audit fix`, force ou legacy-peer-deps foi utilizado nesta aplicação.

## 4. Dependências efetivamente instaladas

`npm ls --all` retornou **código 0**, sem conflitos de peers obrigatórios. Manifestos físicos das entradas Angular e do conjunto visual/runtime foram comparados às versões do lockfile. Os pacotes oficiais `@angular/*` instalados enumerados são exclusivamente major 21.

| Pacote/grupo | Antes | Instalado |
| --- | --- | --- |
| animations/common/compiler/core/forms/platform-browser/router | 21.2.23 | 21.2.26 |
| compiler-cli | 21.2.23 | 21.2.26 |
| cli/build | 21.2.24 | 21.2.30 |
| @angular/cdk | 21.2.14 | 21.2.14 |
| primeng | 21.1.10 | 21.1.10 |
| @primeuix/themes | 3.0.1 | 3.0.1 |
| @primeuix/styled, raiz | 1.0.0 | 1.0.0 |
| @primeuix/styles | 2.0.3 | 2.0.3 |
| @primeuix/utils, raiz | 0.8.2 | 0.8.2 |
| @primeuix/motion | 0.0.10 | 0.0.10 |
| primeicons | 8.0.2 | 8.0.2 |
| rxjs | 7.8.2 | 7.8.2 |
| tslib | 2.8.1 | 2.8.1 |
| typescript | 5.9.3 | 5.9.3 |

Cópias aninhadas verificadas fisicamente: styled 0.7.4 sob styles e sob primeng; utils 0.6.4 sob motion, styles e primeng/styled; utils 0.7.2 sob primeng. Nenhuma versão PrimeNG/PrimeUIX foi atualizada. Inventário por caminho em `installed-versions.json` nas evidências.

## 5. Auditoria atualizada

```text
npm audit --json --ignore-scripts
```

Consulta concluída, **código 1 por vulnerabilidades moderadas**; não foi falha de consulta. Confirmados metadados válidos antes de prosseguir ao build.

| Severidade de pacotes sinalizados | B05-A | C aplicada |
| --- | ---: | ---: |
| Crítica | 3 | 0 |
| Alta | 9 | 0 |
| Moderada | 20 | 16 |
| Baixa | 0 | 0 |
| Total | 32 | 16 |

O conjunto é o mesmo consultado na candidata C, sem novo pacote sinalizado. Contagem de pacotes, incluindo alertas propagados aos pais; não de falhas independentes.

Persistem moderados: @angular/cli, @npmcli/package-json, @npmcli/run-script, @sigstore/sign, @sigstore/tuf, @tufjs/models, cacache, glob, ignore-walk, make-fetch-happen, minimatch, npm-packlist, npm-registry-fetch, pacote, sigstore e tuf-js. As versões constam no B05-B131 e no JSON de auditoria desta etapa. As causas diretas continuam glob 13.0.7 ([GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4)) e minimatch 10.2.6 ([GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249)).

Todos esses nós são dev no lockfile. Nenhum pacote prod foi sinalizado no audit completo. Router 21.2.26 não está nas faixas vulneráveis SSR identificadas no B05-A; a aplicação continua SPA sem SSR. Build e desenvolvimento compartilham ferramentas, portanto não se devem somar novamente esses escopos. Não houve avaliação de um novo artefato implantado: nada foi publicado.

Não foi seguida a sugestão automática de CLI 22.2.7 do audit. Corrigir essa cadeia residual por major incompatível está fora do escopo e proibido nesta etapa.

## 6. Build, TypeScript, budgets e assets

```text
npm run build
```

A primeira execução no sandbox recebeu acesso negado ao ler diretórios e falhou na resolução de fontes/estilos. O mesmo comando foi repetido com permissão fora do sandbox, sem alterar qualquer arquivo de código, dependência ou configuração e sem habilitar scripts de instalação. O log da tentativa bloqueada foi preservado separadamente. A repetição concluiu com **código 0**, em 8,185 segundos de geração do bundle.

Build padrão de produção, `@angular/build:application`, com templates estritos e tsconfig.app.json. Compilação Angular/TypeScript concluída, sem erros de importações ou estilos PrimeNG. Foi executada adicionalmente a verificação TypeScript sem emissão:

```text
node node_modules/typescript/bin/tsc --noEmit -p tsconfig.app.json
```

**Código 0**, sem diagnósticos. Nenhum arquivo funcional foi alterado para contornar erros.

| Verificação de budget | Resultado |
| --- | --- |
| Bundle inicial | 542,61 kB; estimativa transferida 126,94 kB |
| Initial maximumWarning / maximumError | 1 MB / 2 MB; sem violação |
| anyComponentStyle maximumWarning / maximumError | 8 kB / 16 kB; nenhum erro bloqueante |
| Aviso emitido | profile.component.scss: 8,39 kB, excede aviso em 390 bytes |

O aviso de estilo não foi corrigido para evitar mudar escopo/layout ou budgets. O arquivo SCSS e angular.json não mudaram. Não foi realizado build da árvore antiga nesta etapa, portanto não se afirma que a emissão do aviso seja anterior à atualização.

Saída gerada em `frontend/dist/seenit-web`, com 24 arquivos no diretório browser. Conferidos: index.html; todos os arquivos locais referenciados no HTML; stylesheet global (17.580 bytes); fontes/arquivo SVG PrimeIcons referenciados no CSS; e o único asset de public, copiado com hash idêntico. Query strings/fragmentos nas URLs de fontes foram desconsiderados apenas para localizar os arquivos correspondentes no disco. Nenhum asset local referenciado nessas verificações estava ausente.

Não foi necessário executar rebuild nativo ou habilitar lifecycle scripts de dependências para esse build Windows. Validação Linux/CI permanece pendente.

## 7. Testes executados e pendentes

| Comando | Resultado e alcance |
| --- | --- |
| npm run check:rate-limiting | Código 0; regressões de modal/library, erros seguros, Retry-After, propriedade das mensagens, loading/cancelamento, login, leituras parciais e ausência de retries de escrita |
| node checks/library.cjs | Código 0; separação, favoritos, filtros, contagens, atualizações, remoção e coleção vazia |
| node checks/watch-time.cjs | Código 0; limites de formatação, singular/plural, tooltip e escala de gráfico |

São checks existentes, com métodos reais/transpilação e doubles locais; não equivalem a testes end-to-end em navegador e não fizeram chamadas à API de produção.

O script `test: ng test` existe, porém angular.json não define alvo test nem runner. O spec de EpisodioModal não foi executado por esse motivo; não foram adicionados runner, dependências ou configuração de testes nesta tarefa. Também não foram executados testes .NET, conforme escopo frontend.

Pendências para validação funcional: navegação/rotas, login, biblioteca, detalhes de série/modal, perfil/tempo assistido, componentes/ícones e temas claro/escuro em navegador. Preservar versões visuais e compilar com sucesso não prova equivalência visual pixel a pixel nem comportamento de todas as telas.

## 8. Revisão do diff e preservação final

Executados `git status --short`, `git diff --check`, `git diff --stat` e conferência automatizada dos caminhos alterados contra HEAD. Diff check sem erros. O Git avisou apenas sobre futura normalização LF → CRLF, sem mudança de hashes durante esta etapa.

Somente dois arquivos existentes versionados foram alterados:

```text
frontend/package-lock.json | 1031 linhas de diff: 572 adições, 459 remoções
frontend/package.json      |   28 linhas de diff:  14 adições,  14 remoções
Total: 586 adições e 473 remoções
```

O relatório presente é novo e não está incluído nesse stat de arquivos versionados. Os cinco relatórios anteriores continuam não rastreados, preservados byte a byte. Não há alterações funcionais TypeScript/HTML/CSS, API, Dockerfile ou configuração Angular. dist e cache Angular são artefatos locais de build, não alterações de fonte a commitar.

A worktree maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`; seu status e os hashes dos 15 arquivos locais registrados são idênticos ao início. Sua árvore não estava limpa antes e não foi limpa ou modificada nesta tarefa.

## 9. Riscos residuais e rollback

Riscos documentados: 16 moderados na cadeia CLI, aviso de budget de estilo, majors transitivas já revisadas, seleção de opcionais nativos e validação funcional/visual ainda não realizada. Permanecem os riscos preexistentes de PrimeUIX antigo fora da contagem npm, descritos no B05-B131; sua atualização não foi incluída para preservar o escopo visual aprovado.

Rollback preparado, **não executado**:

1. Restaurar exclusivamente frontend/package.json e frontend/package-lock.json a partir das cópias `original-package.json` e `original-package-lock.json` no diretório de evidências abaixo. Conferir os hashes originais da seção 1. Não resetar a branch inteira nem tocar relatórios ou maintenance-phase1.
2. Executar `npm ci --ignore-scripts --no-audit --no-fund` dentro de frontend para reinstalar exatamente a árvore original.
3. Reexecutar build/checks se for necessário validar a restauração; isso reintroduzirá os alertas conhecidos do B05-A e deve ser documentado. Não reutilizar a árvore física nova com o manifesto antigo.

## 10. Evidências locais e encerramento

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b14-KLXzsK
```

Contém backups dos dois arquivos originais, preflight.json com hashes dos relatórios e da candidata, maintenance-file-hashes.json, npm-ci.log, npm-ls.log, npm-audit.json, build-sandbox.log, build.log, typescript.log, logs dos três checks, arquivos de códigos de saída, installed-versions.json, asset-verification.json, final-preservation.json e frontend.diff completo.

Arquivos desta aplicação no repositório: frontend/package.json, frontend/package-lock.json e docs/B05-B14-ANGULAR-APPLICATION.md. Relatórios anteriores mantidos. Nenhum commit, push ou deploy foi executado.

**APROVADO PARA VALIDAÇÃO FUNCIONAL. Trabalho encerrado nesta etapa, sem publicação ou correções adicionais.**
