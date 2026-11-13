# B05-B1.3 — Revisão da árvore B e do lockfile

Data: 13/11/2026 (America/Sao_Paulo). Branch: `b05-b1-angular-security`.

**Parecer: AJUSTES NECESSÁRIOS para adotar o lockfile B integralmente.** A atualização Angular proposta resolve peers e reduz os alertas, mas a resolução limpa também atualizou o tema usado pela aplicação. Recomenda-se preparar uma candidata com escopo menor e preservar o conjunto visual original, salvo justificativa e validação explícitas. Nenhuma correção foi aplicada nesta etapa.

## 1. Evidências, método e limites

Lidos comparison.json, os lockfiles original e B completos, o manifesto temporário e o código de configuração PrimeNG. O inventário foi conferido com os dois lockfiles, incluindo diferenças de metadados não capturadas pelo comparison.json, que compara versões/caminhos. Consultados metadados npm e fontes oficiais dos mantenedores para as mudanças relevantes.

Diretório preservado fora do repositório:

```text
C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b05-b12-rp5t1I
```

Entradas: `comparison.json`, `A-with-original-lock/package-lock.json`, `B-without-lock/package.json`, `B-without-lock/package-lock.json`. A cópia A é idêntica ao lockfile original.

No cenário B foi executado somente o comando de auditoria solicitado:

```text
npm audit --json --ignore-scripts
```

Saída preservada em `B-without-lock/audit-b13.json`; código **1**, registrado em `audit-b13-exit.txt`, por vulnerabilidades encontradas, não por falha de consulta. Node 24.16.0, npm 11.13.0. Não foi executada instalação física, build ou teste funcional. A validação `npm ls --all --package-lock-only` com código 0 vem do B05-B1.2.

Nesta revisão, uma conferência adicional das arestas dependencies, optionalDependencies, peerDependencies e devDependencies da raiz, resolvidas pelos caminhos do lockfile e comparadas por semver, encontrou **zero intervalos incompatíveis e zero dependências obrigatórias ausentes**. Todos os engines.node declarados aceitam o Node local. Isso não substitui validação de build nem comprova o runtime usado pela Vercel.

## 2. Comparação e classificação

Há **116 atualizações de versão, duas entradas adicionadas e duas removidas**: 118 entradas alteradas/adicionadas, como informado anteriormente. Ambas as árvores têm 527 entradas além da raiz. Há também **17 diferenças de metadados libc em versões inalteradas**, ausentes da contagem do comparison.json. A configuração da raiz muda nas dez versões Angular solicitadas.

As categorias abaixo são cumulativas: uma atualização pode ser Angular, corrigir um alerta e continuar semanticamente compatível. A classificação individual de todas as 120 diferenças de versão/presença está no apêndice A; o apêndice B cobre os 17 metadados adicionais.

| Categoria | Critério e interpretação |
| --- | --- |
| C1 — Angular | Dez pacotes diretos solicitados e quatro ferramentas relacionadas: devkit architect/core/schematics e schematics/angular. Versões de architect 0.2102.x representam a linha das ferramentas Angular 21, não migração de framework. |
| C2 — Segurança | Pacote antes sinalizado que deixou de ser sinalizado na auditoria B; inclui alertas propagados aos pais. C2-R marca correção publicada pelo mantenedor fora da contagem npm, no parse5/SAX. |
| C3 — Transitiva compatível | Atualização transitiva patch/minor aceita pelos intervalos declarados dos pais na candidata. Indica compatibilidade declarada, não prova ausência de regressão. |
| C4 — Major | content-disposition e a cópia de entities sob parse5. Não são majors impostas por override: os pais novos exigem essas linhas. |
| C5 — Adição | Novo caminho no lockfile; content-type já existia em outros caminhos, enquanto rollup-android-x64 é novo pacote de plataforma. |
| C6 — Remoção | Pacote deixou de ser solicitado pelos pais selecionados; análise específica abaixo. |
| C7 — Atenção | Alteração de runtime visual, API transitiva major, comportamento de parser/HTTP ou seleção nativa; exige avaliação adicional apropriada ao escopo. |

Nem todas as mudanças são necessárias à atualização Angular. O resolvedor atualizou versões aceitas por intervalos amplos, incluindo as dependências diretas **@primeuix/themes 3.0.1 → 3.1.1 e tslib 2.8.1 → 2.9.1**, além dos dez Angular. A declaração desses dois pacotes não mudou, mas a versão resolvida mudou. Themes pode afetar a interface; tslib altera helpers usados no runtime. Preservá-los deve ser avaliado em uma candidata posterior.

Mudanças de Babel, caches, terminais, tabelas de browsers e binários opcionais decorrem de intervalos dos pais resolvidos novamente; não há evidência de que todas sejam exigidas para sanar o ERESOLVE. Versões fixadas pelos novos CLI/build, como piscina, vite, undici, MCP SDK e zod, têm ligação direta com o objetivo. O apêndice registra o pai e o intervalo exatos para distinguir essas situações.

## 3. Remoções

### @primeui/license-manager 1.1.0

Caminho original: dependência direta da aplicação @primeuix/themes 3.0.1 → @primeuix/styled 1.0.0 → @primeui/license-manager ^1.0.0. Não era introduzido diretamente pelo pacote primeng 21.1.10.

B seleciona themes 3.1.1 → styled 1.2.0, que declara somente @primeuix/utils ^0.8.3. O registro npm confirma essa dependência e a integridade do styled 1.2.0. Nenhuma aresta de B exige license-manager; sua remoção é coerente com esse grafo. As cópias styled 0.7.4 e utils usadas internamente por PrimeNG 21 permanecem inalteradas.

Não há importação direta desse pacote na aplicação. Não há evidência de erro por módulo ausente na árvore B. Contudo, a remoção acompanha uma atualização real do runtime de temas: não se pode concluir preservação visual apenas porque o pacote deixou de ser necessário. A ausência do pacote não altera obrigações de licenciamento por si só; esta revisão não faz interpretação jurídica.

Se a estratégia posterior preservar themes/styled originais, license-manager continuará sendo necessário por aquele grafo; não se deve removê-lo ou reinstalá-lo manualmente para obter uma contagem desejada.

### @napi-rs/lzma-linux-x64-gnu 1.5.1

O pai original era **rollup 4.63.1**, que o declarava em optionalDependencies com versão exata 1.5.1. É um pacote dev, opcional, restrito a Linux x64, não a biblioteca nativa principal de Rollup para essa plataforma.

Rollup 4.68.3 não o declara mais; o registro oficial confirma sua ausência na lista de opcionais. Nenhum outro pai de B o exige. As entradas @rollup/rollup-linux-x64-gnu e -musl continuam presentes em 4.68.3. Portanto, a remoção não é explicada apenas pelo host Windows: houve mudança na declaração do pai. Não foi identificado o commit específico que motivou a retirada do lzma; o vínculo causal observável no lockfile é a troca do conjunto de opcionais do Rollup.

Ausência esperada e sem impacto direto no PrimeNG. Risco de módulo requerido mas ausente não foi encontrado no grafo. Resta validar instalação/build no ambiente Linux de CI em etapa de aplicação, especialmente por se tratar de ferramentas nativas. Não há justificativa para adicionar esse pacote manualmente.

## 4. Mudanças major

### content-disposition 1.1.0 → 2.0.1

Caminho: @angular/cli 21.2.30 → MCP SDK 1.32.1 → express ^5.2.1, resolvido em **5.3.0** → content-disposition **^2.0.1**. Express antigo 5.2.1 aceitava ^1.0.0. Assim, manter 1.1.0 sob express 5.3.0 violaria o contrato do pai.

O mantenedor documenta remoção do export default em favor de create, retirada da normalização basename, mudanças de parsing e tratamento de nomes/UTF-8. São breaking changes reais da biblioteca. O código de Express 5.3.0 já chama contentDisposition.create e aplica basename, demonstrando adaptação nesse consumidor. Express também documenta alteração na forma das aspas em headers de nomes válidos. Fontes: [content-disposition releases](https://github.com/jshttp/content-disposition/releases), [Express releases](https://github.com/expressjs/express/releases) e [response.js 5.3.0](https://github.com/expressjs/express/blob/v5.3.0/lib/response.js).

O escopo é dev/CLI MCP, sem servidor Express publicado pela SPA e sem import direto no código da aplicação. Risco esperado de impacto visual é baixo; comportamento HTTP das ferramentas pode mudar. Não é uma dependência central do compilador Angular. Testes de execução das ferramentas não foram realizados.

Alternativa compatível em tese: manter Express 5.2.1, pois o MCP novo ainda aceita ^5.2.1; esse pai aceita content-disposition 1.1.0 e intervalos que permitem body-parser/proxy-addr/qs corrigidos. Isso exige candidata separada e audit/validação; não foi resolvido nem testado nesta etapa. Não é possível preservar somente o filho 1.1.0 mantendo o pai 5.3.0 sem contrariar seu intervalo. Nenhum override foi usado.

### entities 8.1.0 → 9.0.0, sob parse5

CDK 21.2.14, parse5-html-rewriting-stream 8.0.0 e parse5-sax-parser introduzem parse5. O intervalo do CDK continua ^8.0.0, mas a resolução seleciona **parse5 8.1.1**, que exige entities **^9.0.0**. Parse5 antigo 8.0.1 exigia entities ^8.0.0. A cópia não é a entities 4.5.0 da raiz nem as cópias 6.0.1 e 7.0.1 de outros parsers: essas permanecem intactas.

Entities 9 altera os decoders de streaming e remove exports de tabelas/funcionalidades depreciadas; também muda a representação de referências codificadas e o escape de atributos. Parse5 8.1.x foi adaptado para HtmlEntityDecoder e adotou o escape de < e > em atributos. O pai declara a major nova e seu código a utiliza. Fontes: [entities releases](https://github.com/fb55/entities/releases), [parse5 releases](https://github.com/inikulin/parse5/releases) e [tokenizer 8.1.1](https://github.com/inikulin/parse5/blob/v8.1.1/packages/parse5/lib/tokenizer/index.ts).

Parse5 8.1.1 também publica correções de segurança de parser/serialização sem GHSA/CVE, ausentes da contagem npm. Por isso, voltar ao parser antigo apenas para eliminar uma major transitiva não é automaticamente a opção mais segura. O escopo do lockfile inclui produção por CDK e build por reescrita de HTML; presença no lockfile prod não comprova execução ou inclusão integral no bundle.

Preservar entities 8.1.0 com parse5 8.1.1 viola ^9.0.0. Preservar parse5 8.0.1 é aceito pelo CDK e pelo rewriting-stream, mas **não** pelo SAX novo 8.0.1, que exige parse5 ^8.1.1. Seria necessário também preservar SAX 8.0.0, abrindo mão das correções publicadas. Alternativa não recomendada sem análise específica das falhas e dos consumidores. Nenhuma alteração foi realizada.

### Outras mudanças de atenção

- **content-type 2.1.0 adicionado sob Express:** já existia 1.0.5 na raiz e 2.1.0 sob body-parser. O novo caminho atende ^2.0.0 de Express 5.3.0; não é uma terceira atualização major de um mesmo caminho, mas introduz a API major 2 nesse consumidor. Express documenta diferenças de preservação de parâmetros/charset.
- **PrimeUIX:** Aura é importado em frontend/src/main.ts. Themes 3.1.1 → styled 1.2.0 → utils 0.8.3 altera a cadeia visual mesmo mantendo primeng 21.1.10. As notas oficiais descrevem correções de tokens, dark mode e helpers de camadas CSS. São mudanças adicionais ao Angular e não estão demonstradas como necessárias ao ERESOLVE. Fontes: [styled 1.1/themes 3.0.2](https://blog.primeui.dev/blog/primeuix-styled-1-1-0-and-themes-3-0-2/) e [styled 1.2/themes 3.1](https://blog.primeui.dev/blog/primeuix-styled-1-2-0-and-themes-3-1-0/). As notas citam integração com gerações mais novas de PrimeNG; não certificam o conjunto usado aqui. Não se presume que o reset novo esteja ativo: @primeuix/styles continua em 2.0.3 e a aplicação não o habilita explicitamente.
- **Rollup e opcionais:** atualização aceita pelo Vite; requer futura validação de build/budgets e seleção de binários na plataforma de destino. As mudanças libc adicionais afetam seleção de opcionais Linux; não alteram a integridade dos tarballs nem comprovam regressão.

## 5. Auditoria de segurança comparada

| Severidade | B05-A | Candidata B |
| --- | ---: | ---: |
| Crítica | 3 | 0 |
| Alta | 9 | 0 |
| Moderada | 20 | 16 |
| Baixa | 0 | 0 |
| Pacotes sinalizados | 32 | 16 |

A contagem é de **pacotes sinalizados, incluindo propagação aos pais**, não de falhas independentes. A comparação do JSON preservado de B05-B1 com o B05-A e o novo audit encontrou **zero novos pacotes e zero novos URLs de advisories**. URLs GHSA distintos presentes na auditoria: 50 → 2; os 48 ausentes são achados que deixaram de se aplicar à candidata, não 48 patches independentes implementados pelo projeto.

Deixaram de ser sinalizados: @angular/build (alertas propagados), @angular/router, @hono/node-server, @modelcontextprotocol/sdk, body-parser, brace-expansion, fast-uri, hono, ip-address, piscina, proxy-addr, qs, source-map-js, undici (ambos os caminhos corrigidos), vite e zod. Versões por caminho constam no apêndice A. Os três advisories SSR do router deixam de se aplicar a 21.2.26.

Permanecem **16 pacotes moderados**, todos marcados dev no lockfile:

| Pacote | Versão B | Origem do alerta |
| --- | --- | --- |
| @angular/cli | 21.2.30 | pacote |
| @npmcli/package-json | 7.0.5 | glob |
| @npmcli/run-script | 10.0.4 | @npmcli/package-json |
| @sigstore/sign | 4.1.1 | make-fetch-happen |
| @sigstore/tuf | 4.0.2 | tuf-js |
| @tufjs/models | 4.1.0 | minimatch |
| cacache | 20.0.4 | glob |
| glob | 13.0.7 | Advisory próprio e minimatch |
| ignore-walk | 8.0.0 | minimatch |
| make-fetch-happen | 15.0.6 | cacache |
| minimatch | 10.2.6 | Advisory próprio |
| npm-packlist | 10.0.4 | ignore-walk |
| npm-registry-fetch | 19.1.1 | make-fetch-happen |
| pacote | 21.5.1 | cadeia npm/sigstore |
| sigstore | 4.1.1 | @sigstore/sign e @sigstore/tuf |
| tuf-js | 4.1.0 | @tufjs/models e make-fetch-happen |

As duas causas diretas são [glob GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), faixa >=9.0.0 <=14.0.1, cancelamento de travessia de filesystem; e [minimatch GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249), faixa >=7.0.0 <11.0.1, amplificação com potencial DoS. Glob 13.0.7 não corrige o primeiro alerta; minimatch permaneceu 10.2.6.

O npm sugere CLI 22.2.7 como fixAvailable para essa cadeia. **Essa sugestão foi rejeitada pela restrição de permanecer no Angular 21.** Nenhum comando de correção foi executado. Não se conclui que não possa existir alternativa segura na linha 21; o resultado informa que a árvore B ainda não a resolve. Não se deve impor glob/minimatch em majors incompatíveis para zerar o audit.

### Exposição

- Produção/aplicação: B05-A sinalizava somente router, com alertas de SSR sem caminho demonstrado na SPA. Na candidata B, nenhum pacote prod está sinalizado pelo audit completo. Isso não certifica segurança da SPA e não descreve a implantação atual, que não foi alterada.
- Build: os alertas críticos/altos de build e seus transitivos deixam de aparecer. Ferramentas e parsers atualizados podem afetar futuros artefatos, exigindo build e budgets. Build/desenvolvimento compartilham o ambiente, portanto não são contagens independentes.
- Desenvolvimento/CI: os 16 moderados estão na cadeia do CLI/pacote, em operações de filesystem/empacotamento e ferramentas. A exploração depende de entradas, operações e exposição; não há servidor npm/CLI implantado na SPA estática.
- Segurança fora do npm audit: as correções publicadas de parse5/SAX e de tokens PrimeUIX justificam avaliação própria. Não foram somadas às contagens npm nem reclassificadas como GHSA conhecidos.

## 6. Compatibilidade e parecer

Framework e compiler-cli convergem em 21.2.26; CLI/build ficam em 21.2.30. CDK 21.2.14, PrimeNG 21.1.10 e TypeScript 5.9.3 são preservados. Build aceita TypeScript >=5.9 <6.0, compiler-cli >=5.9 <6.1; a interseção inclui 5.9.3. Os peers de PrimeNG/CDK aceitam o framework escolhido. **Nenhuma entrada @angular/* de B está na major 22 ou superior.**

As duas majors transitivas têm pais declaradamente preparados para elas, com evidência no código de seus mantenedores. Não constituem, por si só, impedimento à atualização Angular. Tampouco exigem reinstalar os dois pacotes removidos.

O impedimento à adoção integral sem ajustes é o aumento desnecessário do escopo visual/runtime: PrimeUIX e tslib mudaram mesmo sem relação demonstrada com o ERESOLVE. Não foi demonstrada incompatibilidade funcional, mas também não se cumpriu a garantia de preservar interface/comportamento. O audit melhorou, porém não substitui essa avaliação. A árvore é viável para investigação e validação; o arquivo B integral ainda não é a candidata mínima recomendada.

## 7. Estratégia recomendada para uma próxima etapa

1. Preparar **outra candidata temporária**, mantendo B intacto, com os dez Angular escolhidos e preservação explícita das versões não relacionadas, principalmente o conjunto themes/styled/utils original e tslib. Fixar só themes não basta: styled ^1.0.0 ainda permitiria deriva. Buscar preservar as entradas do grafo original por resolução compatível, sem overrides major ou edição cega de integridades.
2. Revalidar resolução, peers e audit. Se preservar o visual conservar problemas de segurança publicados pelos mantenedores, registrar e decidir separadamente a atualização PrimeUIX, com revisão de tema e comparação visual. Não perpetuar essas versões como solução de segurança definitiva sem análise.
3. Manter entities 9 com parse5 corrigido é preferível a um downgrade automático só para evitar mudança de major. Express/content-disposition podem ser mantidos no conjunto novo preparado pelo pai; se houver falha concreta nas ferramentas, avaliar a alternativa Express 5.2.1 em candidata separada e auditada.
4. Após revisão da candidata e autorização de aplicação, alterar manifesto/lockfile coordenadamente, instalar, executar npm ls --all, audit, build, TypeScript/Angular, budgets, checks existentes e avaliação visual PrimeNG. Validar também instalação/build na plataforma Linux usada pelo CI, sem deploy.
5. Documentar os 16 moderados residuais se persistirem dentro da linha 21, mantendo a restrição major. Não executar a recomendação automática Angular 22 do audit.

Esta lista é uma recomendação, **não foi executada**. Nenhuma nova candidata foi gerada, nenhum lockfile foi aplicado e nenhuma instalação ocorreu no projeto original nesta etapa.

## 8. Preservação

Frontend/package.json e package-lock.json mantiveram seus hashes, respectivamente `776C745A58B0F17F3063D8EA896F4F1B0F31D99F404B8859943A815009042F08` e `2D2B3D0D9CEF6FCE4DC52C8169FF20FA6BF683191BCC1D269F68C2BA0CFA52AF`. Nenhuma operação escreveu no node_modules original. Os relatórios B05-A, B05-B1 e B05-B12 foram preservados. maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, sem operações em seu worktree. Sem API, banco, Supabase, código funcional, commit, push ou deploy.

O único arquivo novo no repositório nesta etapa é este relatório. Os arquivos de evidência do audit ficaram no cenário B temporário. Os apêndices abaixo são documentação gerada a partir da leitura das árvores; não são dependências aplicadas.


## Apêndice A — Todas as diferenças de versão/presença

Caminhos relativos a node_modules da raiz; ocorrências internas de node_modules distinguem cópias aninhadas. Pais/intervalos correspondem à árvore B, ou à original nas remoções. Códigos dep/dev/opt/peer identificam o tipo da aresta. C3 se limita a compatibilidade declarada patch/minor. C2 não certifica que toda mudança no pacote seja de segurança.

| Caminho | Antes | B | Classes | Pai(s) e intervalo que permitem/exigem a escolha |
| --- | --- | --- | --- | --- |
| @angular-devkit/architect | 0.2102.24 | 0.2102.30 | C1 | @angular/build 0.2102.30 (dep); @angular/cli 0.2102.30 (dep) |
| @angular-devkit/core | 21.2.24 | 21.2.30 | C1 | @angular-devkit/architect 21.2.30 (dep); @angular-devkit/schematics 21.2.30 (dep); @angular/cli 21.2.30 (dep); @schematics/angular 21.2.30 (dep) |
| @angular-devkit/schematics | 21.2.24 | 21.2.30 | C1 | @angular/cli 21.2.30 (dep); @schematics/angular 21.2.30 (dep) |
| @angular/animations | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); @angular/platform-browser 21.2.26 (peer) |
| @angular/build | 21.2.24 | 21.2.30 | C1, C2 | ROOT 21.2.30 (dev) |
| @angular/cli | 21.2.24 | 21.2.30 | C1 | ROOT 21.2.30 (dev) |
| @angular/common | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); @angular/cdk ^21.0.0 &#124;&#124; ^22.0.0 (peer); @angular/forms 21.2.26 (peer); @angular/platform-browser 21.2.26 (peer); @angular/router 21.2.26 (peer); primeng ^21.0.0 (peer) |
| @angular/compiler | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); @angular/build ^21.0.0 (peer); @angular/compiler-cli 21.2.26 (peer); @angular/core 21.2.26 (peer) |
| @angular/compiler-cli | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dev); @angular/build ^21.0.0 (peer) |
| @angular/core | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); @angular/animations 21.2.26 (peer); @angular/build ^21.0.0 (peer); @angular/cdk ^21.0.0 &#124;&#124; ^22.0.0 (peer); @angular/common 21.2.26 (peer); @angular/forms 21.2.26 (peer); @angular/platform-browser 21.2.26 (peer); @angular/router 21.2.26 (peer); primeng ^21.0.7 (peer) |
| @angular/forms | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); primeng ^21.0.0 (peer) |
| @angular/platform-browser | 21.2.23 | 21.2.26 | C1 | ROOT 21.2.26 (dep); @angular/build ^21.0.0 (peer); @angular/cdk ^21.0.0 &#124;&#124; ^22.0.0 (peer); @angular/forms 21.2.26 (peer); @angular/router 21.2.26 (peer); primeng ^21.0.0 (peer) |
| @angular/router | 21.2.23 | 21.2.26 | C1, C2 | ROOT 21.2.26 (dep); primeng ^21.0.0 (peer) |
| @babel/core/node_modules/semver | 6.3.1 | 6.3.2 | C3 | @babel/core ^6.3.1 (dep) |
| @babel/generator | 7.29.8 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep); @babel/traverse ^7.29.14 (dep) |
| @babel/helper-compilation-targets | 7.29.7 | 7.29.13 | C3 | @babel/core ^7.29.7 (dep) |
| @babel/helper-compilation-targets/node_modules/semver | 6.3.1 | 6.3.2 | C3 | @babel/helper-compilation-targets ^6.3.1 (dep) |
| @babel/helper-module-transforms | 7.29.7 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep) |
| @babel/helper-string-parser | 7.29.7 | 7.29.14 | C3 | @babel/types ^7.29.14 (dep) |
| @babel/helpers | 7.29.7 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep) |
| @babel/parser | 7.29.8 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep); @babel/generator ^7.29.14 (dep); @babel/template ^7.29.7 (dep); @babel/traverse ^7.29.14 (dep); istanbul-lib-instrument ^7.23.9 (dep) |
| @babel/traverse | 7.29.8 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep); @babel/helper-module-imports ^7.29.7 (dep); @babel/helper-module-transforms ^7.29.14 (dep) |
| @babel/types | 7.29.8 | 7.29.14 | C3 | @babel/core ^7.29.7 (dep); @babel/generator ^7.29.14 (dep); @babel/helper-annotate-as-pure ^7.27.3 (dep); @babel/helper-module-imports ^7.29.7 (dep); @babel/helper-split-export-declaration ^7.24.7 (dep); @babel/helpers ^7.29.14 (dep); @babel/parser ^7.29.14 (dep); @babel/template ^7.29.7 (dep); @babel/traverse ^7.29.14 (dep) |
| @harperfast/extended-iterable | 1.0.3 | 1.0.5 | C3 | lmdb ^1.0.3 (dep) |
| @hono/node-server | 2.1.1 | 2.1.8 | C2, C3 | @modelcontextprotocol/sdk ^1.19.9 &#124;&#124; ^2.0.5 (dep) |
| @jridgewell/resolve-uri | 3.1.2 | 3.1.3 | C3 | @jridgewell/trace-mapping ^3.1.0 (dep) |
| @modelcontextprotocol/sdk | 1.30.0 | 1.32.1 | C2, C3 | @angular/cli 1.32.1 (dep) |
| @msgpackr-extract/msgpackr-extract-darwin-arm64 | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @msgpackr-extract/msgpackr-extract-darwin-x64 | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @msgpackr-extract/msgpackr-extract-linux-arm | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @msgpackr-extract/msgpackr-extract-linux-arm64 | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @msgpackr-extract/msgpackr-extract-linux-x64 | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @msgpackr-extract/msgpackr-extract-win32-x64 | 3.0.4 | 3.0.7 | C3 | msgpackr-extract 3.0.7 (opt) |
| @napi-rs/wasm-runtime | 1.2.4 | 1.2.7 | C3 | @rolldown/binding-wasm32-wasi ^1.1.1 (dep) |
| @npmcli/agent/node_modules/lru-cache | 11.5.2 | 11.5.3 | C3 | @npmcli/agent ^11.2.1 (dep) |
| @npmcli/git/node_modules/lru-cache | 11.5.2 | 11.5.3 | C3 | @npmcli/git ^11.2.1 (dep) |
| @npmcli/promise-spawn | 9.0.1 | 9.0.2 | C3 | @npmcli/git ^9.0.0 (dep); @npmcli/run-script ^9.0.0 (dep); pacote ^9.0.0 (dep) |
| @primeuix/styled | 1.0.0 | 1.2.0 | C3, C7 | @primeuix/themes ^1.2.0 (dep) |
| @primeuix/themes | 3.0.1 | 3.1.1 | C7 | ROOT ^3.0.1 (dep) |
| @primeuix/utils | 0.8.2 | 0.8.3 | C3, C7 | @primeuix/styled ^0.8.3 (dep) |
| @rollup/rollup-android-arm-eabi | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-android-arm64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-android-x64 | ausente | 4.68.3 | C5 | rollup 4.68.3 (opt) |
| @rollup/rollup-darwin-arm64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-darwin-x64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-freebsd-arm64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-freebsd-x64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-arm-gnueabihf | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-arm-musleabihf | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-arm64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-arm64-musl | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-loong64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-loong64-musl | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-ppc64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-ppc64-musl | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-riscv64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-riscv64-musl | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-s390x-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-x64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-linux-x64-musl | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-openbsd-x64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-openharmony-arm64 | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-win32-arm64-msvc | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-win32-ia32-msvc | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-win32-x64-gnu | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @rollup/rollup-win32-x64-msvc | 4.63.1 | 4.68.3 | C3 | rollup 4.68.3 (opt) |
| @schematics/angular | 21.2.24 | 21.2.30 | C1 | @angular/cli 21.2.30 (dep) |
| @tybys/wasm-util | 0.10.3 | 0.10.4 | C3 | @napi-rs/wasm-runtime ^0.10.3 (dep) |
| @types/estree | 1.0.9 | 1.0.10 | C3 | rollup ^1.0.10 (dep) |
| ansi-regex | 6.3.0 | 6.6.0 | C3 | strip-ansi ^6.2.2 (dep) |
| baseline-browser-mapping | 2.11.22 | 2.11.37 | C3 | browserslist ^2.11.33 (dep) |
| body-parser | 2.3.0 | 2.4.0 | C2, C3 | express ^2.3.0 (dep) |
| brace-expansion | 5.0.9 | 5.0.13 | C2, C3 | minimatch ^5.0.8 (dep) |
| browserslist | 4.28.9 | 4.29.5 | C3 | @angular/build ^4.26.0 (dep); @babel/helper-compilation-targets ^4.24.0 (dep); update-browserslist-db >= 4.21.0 (peer) |
| cacache/node_modules/lru-cache | 11.5.2 | 11.5.3 | C3 | cacache ^11.1.0 (dep) |
| caniuse-lite | 1.0.30001810 | 1.0.30001826 | C3 | browserslist ^1.0.30001819 (dep) |
| content-disposition | 1.1.0 | 2.0.1 | C4, C7 | express ^2.0.1 (dep) |
| electron-to-chromium | 1.5.427 | 1.5.485 | C3 | browserslist ^1.5.464 (dep) |
| encodeurl | 2.0.0 | 2.0.3 | C3 | express ^2.0.0 (dep); finalhandler ^2.0.0 (dep); send ^2.0.0 (dep); serve-static ^2.0.0 (dep) |
| express | 5.2.1 | 5.3.0 | C3, C7 | @modelcontextprotocol/sdk ^5.2.1 (dep); express-rate-limit >= 4.11 (peer) |
| express-rate-limit | 8.7.0 | 8.7.4 | C3 | @modelcontextprotocol/sdk ^8.2.1 (dep) |
| express/node_modules/content-type | ausente | 2.1.0 | C5, C7 | express ^2.0.0 (dep) |
| fast-uri | 3.1.7 | 3.1.9 | C2, C3 | ajv ^3.0.1 (dep) |
| get-east-asian-width | 1.6.0 | 1.7.0 | C3 | cliui/node_modules/string-width ^1.0.0 (dep); is-fullwidth-code-point ^1.3.1 (dep); listr2/node_modules/string-width ^1.0.0 (dep); log-update/node_modules/string-width ^1.0.0 (dep); string-width ^1.5.0 (dep); yargs/node_modules/string-width ^1.0.0 (dep) |
| glob | 13.0.6 | 13.0.7 | C3 | @npmcli/package-json ^13.0.0 (dep); cacache ^13.0.0 (dep) |
| graceful-fs | 4.2.11 | 4.2.12 | C3 | node-gyp ^4.2.6 (dep); watchpack ^4.1.2 (dep) |
| hono | 4.13.7 | 4.13.18 | C2, C3 | @hono/node-server ^4 &#124;&#124; ^5.0.0-0 (peer); @modelcontextprotocol/sdk ^4.11.4 (dep) |
| hosted-git-info/node_modules/lru-cache | 11.5.2 | 11.5.3 | C3 | hosted-git-info ^11.1.0 (dep) |
| http-cache-semantics | 4.2.0 | 4.3.0 | C3 | make-fetch-happen ^4.1.1 (dep) |
| immutable | 5.1.9 | 5.2.1 | C3 | sass ^5.0.2 (dep) |
| ip-address | 10.7.0 | 10.7.6 | C2, C3 | express-rate-limit ^10.2.0 (dep); socks ^10.1.1 (dep) |
| jose | 6.2.12 | 6.2.17 | C3 | @modelcontextprotocol/sdk ^6.1.3 (dep) |
| msgpackr-extract | 3.0.4 | 3.0.7 | C3 | msgpackr ^3.0.2 (opt) |
| nanoid | 3.3.19 | 3.3.20 | C3 | postcss ^3.3.20 (dep) |
| node-gyp/node_modules/undici | 6.28.1 | 6.29.1 | C2, C3 | node-gyp ^6.25.0 (dep) |
| node-releases | 2.0.55 | 2.0.64 | C3 | browserslist ^2.0.60 (dep) |
| on-finished | 2.4.1 | 2.4.2 | C3 | body-parser ^2.4.1 (dep); express ^2.4.1 (dep); finalhandler ^2.4.1 (dep); send ^2.4.1 (dep) |
| ordered-binary | 1.6.1 | 1.6.3 | C3 | lmdb ^1.5.3 (dep) |
| p-map | 7.0.7 | 7.1.1 | C3 | cacache ^7.0.2 (dep) |
| parse5 | 8.0.1 | 8.1.1 | C2-R, C3, C7 | @angular/cdk ^8.0.0 (dep); parse5-html-rewriting-stream ^8.0.0 (dep); parse5-sax-parser ^8.1.1 (dep) |
| parse5-sax-parser | 8.0.0 | 8.0.1 | C2-R, C3, C7 | parse5-html-rewriting-stream ^8.0.0 (dep) |
| parse5/node_modules/entities | 8.1.0 | 9.0.0 | C4, C7 | parse5 ^9.0.0 (dep) |
| path-scurry/node_modules/lru-cache | 11.5.2 | 11.5.3 | C3 | path-scurry ^11.0.0 (dep) |
| piscina | 5.2.0 | 5.3.2 | C2, C3 | @angular/build 5.3.2 (dep) |
| postcss | 8.5.28 | 8.5.32 | C3 | @angular/build ^8.4.0 (peer); beasties ^8.4.49 (dep); postcss-safe-parser ^8.4.31 (peer); vite ^8.5.6 (dep) |
| proxy-addr | 2.0.7 | 2.0.8 | C2, C3 | express ^2.0.8 (dep) |
| qs | 6.16.0 | 6.17.1 | C2, C3 | body-parser ^6.16.1 (dep); express ^6.16.0 (dep) |
| rollup | 4.63.1 | 4.68.3 | C3, C7 | vite ^4.43.0 (dep) |
| source-map-js | 1.2.1 | 1.2.2 | C2, C3 | postcss ^1.2.2 (dep); sass >=0.6.2 <2.0.0 (dep) |
| spdx-license-ids | 3.0.23 | 3.0.24 | C3 | spdx-expression-parse ^3.0.0 (dep) |
| string-width | 8.2.2 | 8.3.0 | C3 | cli-truncate ^8.2.0 (dep); ora ^8.1.0 (dep) |
| tar | 7.5.22 | 7.5.23 | C3 | node-gyp ^7.5.4 (dep); pacote ^7.4.3 (dep) |
| tslib | 2.8.1 | 2.9.1 | C7 | ROOT ^2.8.1 (dep); @angular/animations ^2.3.0 (dep); @angular/build ^2.3.0 (peer); @angular/cdk ^2.3.0 (dep); @angular/common ^2.3.0 (dep); @angular/compiler ^2.3.0 (dep); @angular/compiler-cli ^2.3.0 (dep); @angular/core ^2.3.0 (dep); @angular/forms ^2.3.0 (dep); @angular/platform-browser ^2.3.0 (dep); @angular/router ^2.3.0 (dep); @emnapi/core ^2.4.0 (dep); @emnapi/runtime ^2.4.0 (dep); @emnapi/wasi-threads ^2.4.0 (dep); @tybys/wasm-util ^2.4.0 (dep); primeng ^2.3.0 (dep); rxjs ^2.1.0 (dep) |
| undici | 7.29.1 | 7.30.1 | C2, C3 | @angular/build 7.30.1 (dep) |
| update-browserslist-db | 1.3.3 | 1.3.4 | C3 | browserslist ^1.3.4 (dep) |
| vite | 7.3.6 | 7.3.7 | C2, C3 | @angular/build 7.3.7 (dep); @vitejs/plugin-basic-ssl ^6.0.0 &#124;&#124; ^7.0.0 (peer) |
| weak-lru-cache | 1.2.2 | 1.2.3 | C3 | lmdb ^1.2.2 (dep) |
| zod | 4.3.6 | 4.6.6 | C2, C3 | @angular/cli 4.6.6 (dep); @modelcontextprotocol/sdk ^3.25 &#124;&#124; ^4.0 (dep); @modelcontextprotocol/sdk ^3.25 &#124;&#124; ^4.0 (peer); zod-to-json-schema ^3.25.28 &#124;&#124; ^4 (peer) |
| @napi-rs/lzma-linux-x64-gnu | 1.5.1 | ausente | C6, C7 | rollup 1.5.1 (opt) |
| @primeui/license-manager | 1.1.0 | ausente | C6, C7 | @primeuix/styled ^1.0.0 (dep) |

Total: 120 linhas; classificação cumulativa: C1=14, C2=17, C3=98, C7=13, C5=2, C4=2, C2-R=2, C6=2.

## Apêndice B — Metadados com versão inalterada

Estas 17 entradas acrescentam a restrição libc. Version, resolved e integrity permanecem iguais em cada par; não representam novos tarballs. São opcionais nativos de build/desenvolvimento. A restrição passa a selecionar glibc ou musl de forma explícita; exige futura validação Linux. Classes C3 (metadados) e C7 (seleção de plataforma).

| Caminho | Versão preservada | libc original | libc B |
| --- | --- | --- | --- |
| @napi-rs/nice-linux-arm64-gnu | 1.1.1 | ausente | ["glibc"] |
| @napi-rs/nice-linux-arm64-musl | 1.1.1 | ausente | ["musl"] |
| @napi-rs/nice-linux-ppc64-gnu | 1.1.1 | ausente | ["glibc"] |
| @napi-rs/nice-linux-riscv64-gnu | 1.1.1 | ausente | ["glibc"] |
| @napi-rs/nice-linux-s390x-gnu | 1.1.1 | ausente | ["glibc"] |
| @napi-rs/nice-linux-x64-gnu | 1.1.1 | ausente | ["glibc"] |
| @napi-rs/nice-linux-x64-musl | 1.1.1 | ausente | ["musl"] |
| @parcel/watcher-linux-arm-glibc | 2.6.0 | ausente | ["glibc"] |
| @parcel/watcher-linux-arm-musl | 2.6.0 | ausente | ["musl"] |
| @parcel/watcher-linux-arm64-glibc | 2.6.0 | ausente | ["glibc"] |
| @parcel/watcher-linux-arm64-musl | 2.6.0 | ausente | ["musl"] |
| @parcel/watcher-linux-x64-glibc | 2.6.0 | ausente | ["glibc"] |
| @parcel/watcher-linux-x64-musl | 2.6.0 | ausente | ["musl"] |
| @rolldown/binding-linux-arm64-gnu | 1.0.0-rc.4 | ausente | ["glibc"] |
| @rolldown/binding-linux-arm64-musl | 1.0.0-rc.4 | ausente | ["musl"] |
| @rolldown/binding-linux-x64-gnu | 1.0.0-rc.4 | ausente | ["glibc"] |
| @rolldown/binding-linux-x64-musl | 1.0.0-rc.4 | ausente | ["musl"] |

**AJUSTES NECESSÁRIOS. Análise encerrada, sem iniciar aplicação.**
