# B05-B2.2 — Experimento isolado de overrides transitivos

Data: **16/11/2026 (America/Sao_Paulo)**. Branch: `security/b05-b2-dependency-review`. Base: `2d3e1b1386bdf04b7f520dabb721486806c8dd33`.

## 1. Objetivo e estado inicial

Avaliar experimentalmente glob 14.0.6 e minimatch 11.0.8 sob Angular CLI 21, sem aplicar a candidata ao frontend real. O diagnóstico B05-B2.1 estava presente como arquivo não rastreado; foi preservado. Não havia outras alterações na worktree principal. Não foi criado commit nem executado push.

Ambiente Windows x64: Node 24.16.0, npm 11.13.0; Angular Framework/compiler-cli 21.2.26; CLI/Build 21.2.30; PrimeNG 21.1.10; themes 3.0.1; TypeScript 5.9.3; tslib 2.8.1.

Hashes SHA-256 dos originais, registrados antes do experimento e conferidos depois:

| Arquivo | SHA-256 |
| --- | --- |
| frontend/package.json | `1be4219791c470c3d25d9f65adf56c12a4e690437439f9aa6ecc246db27dc036` |
| frontend/package-lock.json | `016a0cb37de8a4d036b7d0199c95138b76c63cfcdc60eaa954c5b6c976d608d9` |
| docs/B05-B2-DEPENDENCY-DIAGNOSIS.md | `c08329fe321629c16ca2bba21f043fda94ce0bdbd005ae825649e43ea5158c98` |

maintenance-phase1 permanece na worktree separada, commit `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com suas alterações locais preexistentes. Nenhum comando de escrita, instalação ou teste foi direcionado a ela. A comparação de git status --porcelain antes/depois foi idêntica; não foi feita comparação byte a byte de toda essa worktree. As branches existentes foram mantidas.

## 2. Isolamento e evidências

Diretório externo ao repositório:

```text
C:\Users\giovana.grigolato\AppData\Local\Temp\seenit-b05-b22-a5ecb3436b434092bf125d8372208c86
```

`candidate` contém cópias de package.json, package-lock.json, angular.json, tsconfig.json, tsconfig.app.json, src, public e checks. `control` contém os mesmos arquivos originais, sem overrides, para comparação. `linux` contém cópia separada da candidata, sem node_modules do Windows. Não foram copiados .git, .env, credenciais, configuração de serviços nem a API. Nenhum symlink foi usado para apontar ao frontend original; a inspeção de reparse points em src/public/checks da candidata não encontrou links.

Todos os comandos de resolução, instalação, compilação e testes foram executados nessas cópias. Os testes auxiliares e fixtures ficaram somente no temporário. Na branch, a única criação desta etapa é este relatório. A candidata e seu lockfile não foram copiados de volta.

Evidências locais incluem baseline.json, lock-changes.json, verification.json, npm-ci.log, npm-audit.json, npm-ls.json, candidate-build.log, control-ci.log, control-build.log, linux-validation.log, linux-comparison.json, linux/npm-ls-linux.json e scripts auxiliares. Alguns resultados também foram registrados na saída dos comandos da sessão. O temporário não é um artefato versionado ou armazenamento durável.

## 3. Revalidação das versões e advisories

Consultas `npm view` ao registro, nesta etapa: @angular/cli@21, glob@13, minimatch@10, glob@14.0.6 e minimatch@11.0.8, com version, dependencies e engines. CLI 21 continua terminando em **21.2.30**, glob 13 em **13.0.7**, minimatch 10 em **10.2.6**. Não foi encontrado patch oficial dentro dos ranges atuais. As candidatas exatas continuam publicadas.

Os advisories consultados confirmam glob corrigido a partir de [14.0.2 — GHSA-3g3m-2fjr-6pw4 / CVE-2026-118773](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) e minimatch corrigido a partir de [11.0.1 — GHSA-g3hv-75wg-m249 / CVE-2026-118764](https://github.com/advisories/GHSA-g3hv-75wg-m249). A auditoria da árvore candidata também foi consultada para identificar outros alertas, além desses dois conhecidos.

## 4. Configuração experimental

A [documentação oficial do npm 11 sobre overrides](https://docs.npmjs.com/cli/v11/configuring-npm/package-json/#overrides) foi examinada antes da configuração. Ela permite restrições por ancestral, inclusive aninhadas e qualificadas por versão. Apenas a cópia temporária recebeu:

```json
{
  "overrides": {
    "@angular/cli@21.2.30": {
      "pacote@21.5.1": {
        "glob": "14.0.6",
        "minimatch": "11.0.8"
      }
    }
  }
}
```

Isso restringe a substituição aos descendentes de pacote 21.5.1 sob CLI 21.2.30. Não há override global de glob/minimatch nem alteração das versões diretas. A restrição cobre package-json/cacache e os ramos adicionais ignore-walk/models, independentemente da deduplicação física dos módulos.

## 5. Resolução e diferença do lockfile

Em candidate:

```text
npm install --package-lock-only --ignore-scripts --no-audit --no-fund
npm ci --ignore-scripts --no-audit --no-fund
npm ls --all --json
npm explain glob
npm explain minimatch
npm audit --json
```

Resolução: código 0. npm ci Windows: código 0, 424 pacotes adicionados. npm ls completo: código 0, sem conflitos obrigatórios de peers, dependências inválidas ou ausentes. Os explains confirmaram os overrides nos quatro ramos relevantes. Não foram usados force, legacy-peer-deps, audit fix ou atualização ampla. Lifecycle scripts de dependências foram desabilitados; isso não impede os comandos de build/check explicitamente executados.

Comparação estrutural completa dos campos de cada entrada do lock: somente quatro entradas diferem; raiz e todas as demais entradas permanecem iguais.

| Entrada | Original | Candidata | Motivo |
| --- | --- | --- | --- |
| node_modules/glob | 13.0.7 | 14.0.6 | Override restrito |
| node_modules/minimatch | 10.2.6 | 11.0.8 | Override restrito |
| node_modules/path-scurry | 2.0.2 | 3.0.0 | glob 14 exige ^3.0.0 |
| node_modules/@isaacs/nonrecursive-glob-match | ausente | 1.1.0 | minimatch 11 exige ^1.1.0 |

O diff textual experimental tem **21 adições e 13 remoções**. A major adicional path-scurry foi identificada e avaliada como parte necessária da mudança de glob, não como atualização independente. O matcher adicional também pertence à implementação candidata. Nenhum outro upgrade, mudança de peer ou alteração direta foi aceito silenciosamente.

Árvore resultante, com os ancestrais intermediários detalhados no B05-B2.1:

```text
@angular/cli@21.2.30 -> pacote@21.5.1
  -> @npmcli/package-json@7.0.5 -> glob@14.0.6
  -> @npmcli/run-script@10.0.4 -> @npmcli/package-json@7.0.5
  -> cacache@20.0.4 -> glob@14.0.6
  -> npm-packlist@10.0.4 -> ignore-walk@8.0.0 -> minimatch@11.0.8
  -> sigstore@4.1.1 -> @sigstore/tuf@4.0.2 -> tuf-js@4.1.0
     -> @tufjs/models@4.1.0 -> minimatch@11.0.8
glob@14.0.6 -> minimatch@11.0.8 + path-scurry@3.0.0
minimatch@11.0.8 -> @isaacs/nonrecursive-glob-match@1.1.0
```

Não restaram entradas vulneráveis glob/minimatch no lock candidato. Os pacotes hoisted continuaram marcados como desenvolvimento. A configuração não foi validada para outras versões futuras de CLI/pacote.

## 6. Auditoria da candidata

Consulta válida npm audit, código **0**, auditReportVersion 2:

| Severidade | Diagnóstico original B05-B2.1 | Candidata |
| --- | ---: | ---: |
| Crítica | 0 | 0 |
| Alta | 0 | 0 |
| Moderada | 16 | 0 |
| Baixa / informativa | 0 / 0 | 0 / 0 |
| Total de pacotes sinalizados | 16 | 0 |

Metadados da candidata: prod 27, dev 502, optional 127, peer 4, total 529; categorias sobrepostas. A redução corresponde à retirada de duas causas e sua propagação aos pais. Não foi identificado novo advisory na consulta. O resultado refere-se ao lock experimental, não ao frontend real, que permanece com a árvore original. Zero alertas conhecidos não certifica ausência de falhas.

## 7. Engines, peers e preservação das bibliotecas

glob 14.0.6 e path-scurry 3.0.0 aceitam Node `18 || 20 || >=22`; minimatch 11.0.8 exige `20 || >=22`. O matcher novo não declara engine Node no lock consultado. Todos os engines Node declarados no lock candidato foram confrontados via semver com Node 24.16.0: nenhuma incompatibilidade. A exigência de minimatch deixa de aceitar Node 18, já fora do suporte do CLI atual. Os ranges suportados pelo CLI continuam `^20.19.0 || ^22.12.0 || >=24.0.0`.

Não há novos peers nas quatro entradas alteradas. Os demais metadados de peers permanecem iguais; npm ls completo passou. Isso demonstra uma resolução consistente com os overrides, não uma certificação upstream das majors substituídas.

Inventário protegido, incluindo cópias aninhadas, conferido por caminho, versão, integrity e hash do package.json instalado; depois comparado também por lista de arquivos, tamanho e SHA-256 de cada arquivo entre original e candidata Windows:

| Pacote e localização relativa a node_modules | Versão |
| --- | --- |
| primeng | 21.1.10 |
| @primeuix/themes | 3.0.1 |
| @primeuix/motion | 0.0.10 |
| @primeuix/styled | 1.0.0 |
| @primeuix/styles | 2.0.3 |
| @primeuix/utils | 0.8.2 |
| @primeuix/motion/node_modules/@primeuix/utils | 0.6.4 |
| @primeuix/styles/node_modules/@primeuix/styled | 0.7.4 |
| @primeuix/styles/node_modules/@primeuix/utils | 0.6.4 |
| primeng/node_modules/@primeuix/styled | 0.7.4 |
| primeng/node_modules/@primeuix/styled/node_modules/@primeuix/utils | 0.6.4 |
| primeng/node_modules/@primeuix/utils | 0.7.2 |
| typescript | 5.9.3 |
| tslib | 2.8.1 |

Os **14 registros são idênticos**, inclusive conteúdo instalado, sem alteração de PrimeNG/PrimeUIX, TypeScript ou tslib. Todas as entradas oficiais @angular/* foram preservadas pelo diff estrutural do lock; continuam na major 21.

## 8. Testes Windows e comportamento das ferramentas

| Verificação | Resultado |
| --- | --- |
| TypeScript --noEmit -p tsconfig.app.json | Código 0 |
| npm run build | Código 0 |
| npm run check:rate-limiting | Código 0 |
| node checks/library.cjs | Código 0 |
| node checks/watch-time.cjs | Código 0 |
| ng version | Código 0; CLI 21.2.30 / Framework 21.2.26 |
| ng generate component experiment-smoke --dry-run --skip-tests | Código 0; nenhuma escrita de componente |
| Fixtures locais de glob/minimatch/cache/pacote/TUF | Código 0 após adequações do teste descritas abaixo |

O teste auxiliar usa módulos resolvidos a partir do CLI/pacote instalado e apenas dados locais pequenos. Cobertura:

- glob assíncrono, síncrono, stream e exclusão por ignore; cancelamento com sinal previamente abortado, sem teste de estresse;
- minimatch com optimizationLevel 0, 1 e 2, exemplos positivos/negativos, braces e extglob;
- ignore-walk com .npmignore, exercitando exclusão de arquivo;
- cacache put/get/verify em cache descartável, incluindo o caminho de glob da verificação;
- pacote com empacotamento de diretório, leitura de manifesto e extração de tarball local, offline e ignoreScripts; conferência de conteúdo incluído/excluído;
- @tufjs/models com matching de caminho delegado, aceitando lib/a.js e rejeitando caminho fora do padrão e profundidade adicional.

A primeira execução da fixture assumia separadores POSIX nas saídas glob do Windows; a comparação foi normalizada. A segunda não fornecia tree/Arborist, exigência explícita da API DirFetcher de pacote; foi fornecida árvore explícita sem dependências para a fixture. Não foram falhas de build/resolução da candidata. Nenhum pacote extra foi instalado para contornar essas situações. A fixture corrigida passou.

Não foram exercitados servidores MCP, assinatura/publicação real de pacotes, downloads de repositórios não confiáveis, autenticação de registro ou toda a cadeia sigstore/TUF online. O teste TUF é limitado ao caminho de matching afetado; o teste de aborto não reproduz a corrida de cancelamento do advisory. Build aprovado não foi tratado como prova suficiente de toda a cadeia.

## 9. Bundle, assets e avisos

A cópia control foi instalada com npm ci (código 0, 423 pacotes) e compilada com o lock original (código 0). Comparação com a candidata Windows:

- 24 arquivos no diretório browser em cada build;
- 1.564.757 bytes no total em cada diretório;
- nomes, tamanhos e SHA-256 de todos os 24 arquivos idênticos, incluindo HTML, JavaScript, CSS, fontes/ícones e asset público;
- bundle inicial 543,66 kB, estimativa transferida 127,34 kB em ambos;
- somente o aviso conhecido de profile.component.scss: 8,39 kB, 390 bytes acima do aviso de 8 kB; nenhum erro de budget.

O tamanho histórico anterior ao GuestGuard não foi usado como controle. Ambos os builds desta comparação partem do mesmo código atual. A igualdade dos artefatos indica ausência de alteração do bundle entregue ao navegador pelo override nesse experimento, sem substituir validação funcional em navegador. O aviso de instalação de @angular/animations deprecado também permanece; o link de documentação v22 não representa instalação de Angular 22.

## 10. Linux

WSL e Docker foram detectados. WSL Ubuntu não apresentou runtime Node Linux instalado; o npm encontrado no PATH era o executável Windows, por isso não foi utilizado como validação Linux. Foi baixada a imagem oficial `node:24.16.0-bookworm-slim`, digest `sha256:2c87ef9bd3c6a3bd4b472b4bec2ce9d16354b0c574f736c476489d09f560a203`.

Um container Linux x86_64 descartável, Node 24.16.0 e npm 11.13.0, montou **somente linux/** do temporário em /work. Nenhuma porta, segredo ou diretório do repositório original foi exposto ao container. O container foi removido automaticamente ao terminar; imagem e arquivos temporários permanecem disponíveis.

Sequência com interrupção em qualquer erro (`set -eu`), concluída com código 0:

| Verificação Linux | Resultado |
| --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund | Código 0; 423 pacotes instalados, sem reutilizar node_modules Windows |
| npm ls --all --json | Código 0 |
| TypeScript sem emissão | Código 0 |
| Build de produção | Código 0; mesmo aviso CSS conhecido |
| Checks rate-limiting, library e watch-time | Todos aprovados |
| Fixtures glob/minimatch/ignore-walk/cacache/pacote/TUF | Todas aprovadas |
| ng generate component em dry-run | Código 0; nenhuma escrita de componente |

O lock Linux após npm ci é byte a byte igual ao lock candidato Windows. Os 14 manifestos instalados protegidos têm hashes iguais aos registrados no original. A comparação de todos os arquivos do bundle Linux com a candidata Windows não encontrou diferenças de SHA-256; consequentemente também coincide com o controle Windows. A contagem de pacotes físicos varia entre plataformas; a árvore lógica foi validada e o lock não mudou.

Isso demonstra reprodutibilidade em Linux Debian com o runtime escolhido. **Não é uma execução na Vercel**, nem reproduz necessariamente sua distribuição, versão Node efetiva, variáveis, cache, permissões, rede ou configuração de instalação. Não houve deploy ou consulta às configurações reais da Vercel. A auditoria consolidada da seção 6 foi executada no Windows sobre o mesmo lock, não repetida no container.

## 11. Riscos e limitações

As versões ultrapassam os ranges declarados pelos pais, portanto a responsabilidade de validar compatibilidade passa ao projeto. A resolução consistente não significa que os mantenedores do CLI/pacote suportem oficialmente essas combinações. Mudanças futuras de padrões, cache, diretórios, entradas de pacotes ou versões de ferramentas podem revelar diferenças não cobertas pelas fixtures. path-scurry também muda de major e minimatch passa a usar um matcher adicional.

Não houve teste manual de UI, navegador end-to-end, execução do spec sem runner configurado, validação do artefato publicado ou teste de consumo adversarial. Scripts de instalação das dependências ficaram desabilitados; os resultados não validam sua habilitação irrestrita. Configurações/segredos do ambiente Vercel real não foram consultados. A auditoria e os metadados podem mudar após esta data.

## 12. Rollback e preservação

Nenhum rollback do projeto original é necessário: não foi aplicada alteração de dependência ou código. Para rejeitar a candidata, basta não copiar seus manifestos/lock para o repositório. Os temporários permanecem disponíveis para revisão; nenhuma branch ou worktree foi excluída. Qualquer limpeza futura deve se limitar ao diretório experimental verificado.

Em B05-B2.3, registrar novamente hashes e inventário antes de qualquer aplicação. Se uma aplicação futura for revertida, restaurar apenas os arquivos de dependências daquela etapa a partir da base aprovada e reinstalar o lock original; não resetar a branch inteira nem tocar maintenance-phase1. Esse procedimento é plano, não comando executado.

## 13. Recomendação para B05-B2.3

**Aprovar a candidata para aplicação controlada e validação em B05-B2.3**, mantendo estes overrides exatos e restritos, se esse for o escopo autorizado da próxima etapa. A recomendação se apoia em resolução e peers consistentes, auditoria sem alertas, quatro entradas alteradas no lock, preservação das bibliotecas protegidas, bundles idênticos e testes de ferramentas aprovados em Windows/Linux.

Antes de aplicar, repetir a consulta a patches oficiais e advisories, confirmar a base Git, preservar hashes/inventário e revisar se o runtime de CI atende aos engines. Aplicar somente manifesto/lock revisados na etapa própria, repetir npm ci, audit, ls, build e checks, conferir inventário Prime completo e realizar smoke tests funcionais/visuais. Rejeitar alterações adicionais inesperadas e qualquer alta/crítica nova. Acompanhar correção upstream para retirar os overrides quando possível.

Essa aprovação experimental não equivale a aprovação de produção ou deploy, nem elimina as limitações da seção 11. O frontend original continua sem overrides e sem a correção aplicada. Nenhum commit, push, acesso ao Supabase, alteração de API/banco ou deploy foi realizado.

## 14. Conferência final

Os hashes dos três arquivos originais da seção 1 foram reconfirmados iguais. A worktree principal contém apenas os dois relatórios não rastreados: o diagnóstico anterior preservado e este documento novo. Nenhum arquivo já versionado foi alterado; git diff --check passou. Branch atual e worktrees foram preservadas.

**B05-B2.2 EXPERIMENTO CONCLUÍDO**
