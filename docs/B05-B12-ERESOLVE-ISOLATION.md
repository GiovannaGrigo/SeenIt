# B05-B1.2 — Isolamento do ERESOLVE do Angular 21

Data: 13/11/2026 (America/Sao_Paulo). Branch: `b05-b1-angular-security`.

**CAUSA IDENTIFICADA:** o ERESOLVE é reproduzido ao resolver a atualização a partir do lockfile antigo, mesmo sem node_modules. Com o mesmo manifesto e sem o lockfile antigo, o npm resolve e valida a árvore. A causa operacional foi isolada à resolução com o lockfile existente no npm 11.13.0; o mecanismo interno exato do Arborist não foi determinado.

## 1. Configuração e controles

Node 24.16.0; npm 11.13.0; Windows. Dois diretórios fora do repositório, criados exclusivamente para o diagnóstico:

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b12-rp5t1I\A-with-original-lock
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b12-rp5t1I\B-without-lock
```

Os dois receberam cópias idênticas do package.json original, alteradas apenas nos dez pacotes Angular diretos: framework/compiler-cli 21.2.26, CLI/build 21.2.30. Mantidas as seções dependencies/devDependencies e todas as demais declarações. O cenário A recebeu também cópia byte a byte do lockfile original. O cenário B começou sem lockfile. Nenhum recebeu node_modules; sua ausência foi confirmada depois das resoluções.

Não há package.json na raiz nem declaração de workspaces no manifesto do frontend. Não foram encontrados .npmrc no frontend ou na raiz. Os caminhos userconfig e globalconfig consultados não contêm arquivos; não há variáveis de ambiente NPM_CONFIG_* na execução. A configuração builtin define apenas o prefix de instalação global. Não houve configuração local que precisasse ser copiada.

Configurações efetivas verificadas no contexto original e no cenário B:

| Configuração | Valor |
| --- | --- |
| registry | https://registry.npmjs.org/ |
| legacy-peer-deps | false |
| strict-peer-deps | false |
| install-strategy | hoisted |
| workspaces | null |

Os cenários usaram o mesmo executável npm e o mesmo ambiente. O acesso ao registro foi autorizado fora do sandbox. Uma tentativa de validar B dentro do sandbox recebeu acesso negado ao diretório temporário; a validação foi repetida com autorização, sem repetir a instalação.

## 2. Comandos executados

Preparação por Node: criação dos diretórios com mkdtemp, leitura/cópia dos manifestos e ajuste das dez versões somente nas cópias. Inspeção de Git, configurações npm, versões Node/npm e hashes dos arquivos originais.

Em cada cenário, uma única tentativa:

```text
npm install --package-lock-only --ignore-scripts --no-audit --no-fund
```

Saída padrão e erros guardados em `resolution.log`; código de saída guardado em `exit-code.txt` no respectivo diretório. Não foram utilizados force, legacy-peer-deps ou overrides.

Validação adicional apenas em B:

```text
npm ls --all --package-lock-only --json
```

Árvore guardada em `B-without-lock/resolved-tree.json`, código em `tree-exit-code.txt`. Uma análise local dos dois lockfiles gerou `comparison.json` no diretório pai temporário, com versões selecionadas, todas as entradas alteradas/adicionadas/removidas e conferência semver dos peers. A análise usou a biblioteca semver já distribuída com o npm, sem instalar ferramentas.

## 3. Resultados

| Cenário | Entrada | Código | Resultado |
| --- | --- | ---: | --- |
| A | Manifesto alinhado + lockfile antigo, sem node_modules | 1 | ERESOLVE |
| B | Mesmo manifesto, sem lockfile e sem node_modules | 0 | `up to date in 30s`; lockfile gerado |
| Validação B | npm ls --all --package-lock-only --json | 0 | Árvore enumerada sem erros |

### A — Aresta conflitante e evidência completa

O relatório completo de ERESOLVE foi lido. Evidências locais:

```text
%LOCALAPPDATA%/npm-cache/_logs/2026-11-13T18_20_23_363Z-eresolve-report.txt
%LOCALAPPDATA%/npm-cache/_logs/2026-11-13T18_20_23_363Z-debug-0.log
```

Mensagem: `ERESOLVE could not resolve`. O resolvedor informa `Found: @angular/animations@21.2.23`, apesar de o manifesto solicitar 21.2.26. A árvore antiga inclui:

- platform-browser 21.2.23 exige animations **21.2.23** como peer opcional exato;
- forms e router 21.2.23 exigem platform-browser **21.2.23**;
- animations solicitado 21.2.26 exige core **21.2.26**;
- build antigo 21.2.24 e CDK 21.2.14 aparecem como pais compatíveis por intervalo, sem exigir esse patch antigo.

O npm tenta compatibilizar o conjunto antigo travado com o conjunto novo solicitado e falha na aresta de animations/core. O lockfile A permaneceu idêntico ao original após a falha. Não há novas versões resolvidas com sucesso no cenário A; seu lockfile mantém as versões antigas.

### B — Versões efetivamente resolvidas

| Pacote | Original / A | B |
| --- | --- | --- |
| @angular/animations | 21.2.23 | 21.2.26 |
| @angular/common | 21.2.23 | 21.2.26 |
| @angular/compiler | 21.2.23 | 21.2.26 |
| @angular/core | 21.2.23 | 21.2.26 |
| @angular/forms | 21.2.23 | 21.2.26 |
| @angular/platform-browser | 21.2.23 | 21.2.26 |
| @angular/router | 21.2.23 | 21.2.26 |
| @angular/compiler-cli | 21.2.23 | 21.2.26 |
| @angular/cli | 21.2.24 | 21.2.30 |
| @angular/build | 21.2.24 | 21.2.30 |
| @angular/cdk | 21.2.14 | 21.2.14 |
| primeng | 21.1.10 | 21.1.10 |
| typescript | 5.9.3 | 5.9.3 |
| rxjs | 7.8.2 | 7.8.2 |
| piscina | 5.2.0 | 5.3.2 |
| vite | 7.3.6 | 7.3.7 |
| undici (raiz) | 7.29.1 | 7.30.1 |
| undici (sob node-gyp) | 6.28.1 | 6.29.1 |
| @modelcontextprotocol/sdk | 1.30.0 | 1.32.1 |
| zod | 4.3.6 | 4.6.6 |
| proxy-addr | 2.0.7 | 2.0.8 |

B contém 527 entradas de pacotes além da raiz. A conferência dos peers declarados encontrou **zero incompatibilidades e zero peers obrigatórios ausentes**; 35 arestas de peers opcionais não instalados foram contabilizadas separadamente. npm ls validou a árvore do lockfile, não uma instalação física. Nenhum pacote `@angular/*` em B está fora da major 21.

A resolução nova alterou ou adicionou **118 entradas**, removendo **duas** em relação ao lockfile original. As duas removidas são `node_modules/@napi-rs/lzma-linux-x64-gnu` e `node_modules/@primeui/license-manager`. Entre os caminhos presentes nos dois lockfiles, houve mudanças major transitivas em content-disposition (1.1.0 → 2.0.1) e parse5/node_modules/entities (8.1.0 → 9.0.0). Não foram impostas por override; resultaram da resolução normal dos pais e precisam de revisão antes de eventual adoção. O inventário completo está no comparison.json temporário.

## 4. Interpretação e limites

O lockfile antigo é suficiente para reproduzir o conflito neste ambiente sem node_modules. Portanto, a presença da instalação antiga não é necessária para esse ERESOLVE. O cenário B demonstra que as declarações propostas admitem uma árvore resolvível no mesmo npm, preservando PrimeNG/CDK/TypeScript nas versões verificadas.

O achado não prova corrupção do lockfile nem identifica um bug específico do npm: ainda não foi isolado qual comportamento interno de atualização do grafo provoca a retenção dos peers antigos. Não foi testada outra versão do npm, nem editadas seletivamente entradas do lockfile. Essas hipóteses internas não impedem a conclusão operacional A versus B.

## 5. Solução recomendada, riscos e próxima ação segura

Recomenda-se preparar em uma próxima etapa uma atualização controlada do grafo a partir de uma resolução consistente, revisando o diff completo antes de aplicar qualquer arquivo ao projeto. O lockfile B serve como evidência de viabilidade, não como substituto automaticamente aprovado. Deve-se buscar minimizar mudanças transitivas e explicar as inevitáveis; preservar PrimeNG, CDK e TypeScript; manter todos os pacotes oficiais Angular na major 21.

A próxima ação segura é revisar o comparison.json e definir uma estratégia de atualização do lockfile original que evite reter o conjunto antigo de peers. Uma eventual regeneração controlada só deve ocorrer em uma etapa de correção explicitamente autorizada, preservando a cópia original e comparando o grafo. Após aplicação, seriam necessários instalação física, npm ls --all, npm audit, build, verificação de TypeScript/Angular, budgets e checks automatizados existentes.

Riscos: resolução nova amplia o diff, inclui transitivos com major alterada e pode mudar artefatos de build. Resolução de peers bem-sucedida não comprova compatibilidade funcional, build, testes ou ausência de vulnerabilidades. Não foram executados audit, build ou testes nesta etapa de isolamento; nenhuma redução de vulnerabilidades é alegada.

## 6. Preservação do projeto

Nenhum resultado temporário foi copiado para o frontend. Nenhum comando de instalação foi executado no frontend nesta etapa; seu node_modules não foi modificado pelas operações realizadas. Manifesto, lockfile e relatórios anteriores apresentaram os mesmos hashes SHA256 antes e depois:

| Arquivo | SHA256 |
| --- | --- |
| frontend/package.json | 776C745A58B0F17F3063D8EA896F4F1B0F31D99F404B8859943A815009042F08 |
| frontend/package-lock.json | 2D2B3D0D9CEF6FCE4DC52C8169FF20FA6BF683191BCC1D269F68C2BA0CFA52AF |
| docs/B05-DEPENDENCY-SECURITY-REVIEW.md | 525A7FA01C4B1983FD74EECC89B933B84AE337130717AD47A3DAB8280E1165D6 |
| docs/B05-B1-ANGULAR-SECURITY-FIX.md | 67401A31C5787EFEF404AF87A22C380C44353B8A142EDBCBE108ECBF3F5E888C |

maintenance-phase1 permaneceu em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, sem operações em seu worktree. Sem alterações de código Angular, API ou banco, sem commit, push ou deploy. O único arquivo novo desta etapa no repositório é este relatório. Os diretórios temporários foram preservados para inspeção.

**CAUSA IDENTIFICADA. Diagnóstico encerrado; nenhuma correção adicional iniciada.**
