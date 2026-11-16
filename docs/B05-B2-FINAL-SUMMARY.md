# B05-B2.7 — Encerramento técnico e documental

Data do registro: **16/11/2026 (America/Sao_Paulo)**.

**B05-B2 PRONTO PARA ENCERRAMENTO.** Integração do PR #3 confirmada no GitHub; main local sincronizada por fast-forward. Este relatório permanece local, sem commit ou push nesta etapa.

## 1. Objetivo e histórico

Corrigir os alertas moderados remanescentes na cadeia de desenvolvimento do Angular CLI, preservando Angular 21, as dependências diretas e as bibliotecas visuais, sem alteração funcional, de API, banco ou Docker.

| Etapa | Resultado e origem |
| --- | --- |
| B05-B2.1 | Diagnóstico atualizado: 16 moderadas, associadas a dois advisories independentes |
| B05-B2.2 | Experimento isolado de overrides aprovado em Windows e Linux |
| B05-B2.3 | Aplicação controlada e verificações técnicas aprovadas |
| B05-B2.4 | Onze testes funcionais locais aprovados, segundo o operador |
| B05-B2.5 | Dois commits separados publicados e PR #3 criado; verificações técnicas repetidas com sucesso |
| B05-B2.6 | Preview e produção validados pelo operador; nenhuma reprodução independente pelo Codex |
| B05-B2.7 | Integração, escopo e sincronização local confirmados; encerramento registrado |

Referências: [diagnóstico](B05-B2-DEPENDENCY-DIAGNOSIS.md), [experimento isolado](B05-B2-OVERRIDE-EXPERIMENT.md) e [validação funcional](B05-B2-FUNCTIONAL-VALIDATION.md). Os documentos anteriores descrevem o estado de suas respectivas etapas; referências neles à ausência de aplicação ou integração são históricas.

## 2. Diagnóstico e resultado histórico da auditoria

As 16 ocorrências npm eram **pacotes sinalizados**, não 16 falhas independentes: duas causas próprias e 14 alertas propagados aos pais. Todos os nós afetados eram de desenvolvimento no lockfile, sob a cadeia CLI/pacote. Não foi demonstrado caminho de exploração na SPA publicada.

| Causa independente | Versão anterior | Advisory e CVE | Primeira versão corrigida |
| --- | --- | --- | --- |
| glob: travessia continua após cancelamento | 13.0.7 | [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), CVE-2026-118773 | 14.0.2 |
| minimatch: amplificação no processamento de padrões | 10.2.6 | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249), CVE-2026-118764 | 11.0.1 |

O impacto descrito é disponibilidade das ferramentas sob condições específicas: árvores/padrões influenciados por atacante e uso de cancelamento como limite, no caso de glob; padrões não confiáveis com optimizationLevel >=2, no caso de minimatch. Instalar uma ferramenta vulnerável não comprova alcançabilidade de todo caminho afetado.

| Severidade | B05-B2.1, antes | Candidata e aplicação, depois |
| --- | ---: | ---: |
| Crítica | 0 | 0 |
| Alta | 0 | 0 |
| Moderada | 16 | 0 |
| Baixa / informativa | 0 / 0 | 0 / 0 |
| Total | 16 | 0 |

São resultados **históricos de consultas válidas**, incluindo nova execução npm audit --json na revisão final anterior aos commits. O JSON final não continha vulnerabilidades; código 0. O diagnóstico original retornou código 1 por alertas, não por falha da consulta. Não foi executada nova auditoria neste encerramento documental. Ausência de alertas conhecidos no npm não comprova ausência absoluta de vulnerabilidades.

## 3. Overrides e versões integradas

A solução foi aplicada somente à cadeia identificada, com esta propriedade no manifesto:

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

Não há override global. A restrição por ancestrais cobre glob sob @npmcli/package-json/cacache e minimatch também sob ignore-walk/@tufjs/models, mesmo com módulos deduplicados fisicamente.

| Pacote/grupo | Antes | Final na main |
| --- | --- | --- |
| Angular Framework e compiler-cli | 21.2.26 | 21.2.26 |
| Angular CLI / Build | 21.2.30 | 21.2.30 |
| @angular/cdk transitivo | 21.2.14 | 21.2.14 |
| PrimeNG | 21.1.10 | 21.1.10 |
| @primeuix/themes | 3.0.1 | 3.0.1 |
| TypeScript | 5.9.3 | 5.9.3 |
| tslib | 2.8.1 | 2.8.1 |
| glob | 13.0.7 | 14.0.6 |
| minimatch | 10.2.6 | 11.0.8 |
| path-scurry | 2.0.2 | 3.0.0 |
| @isaacs/nonrecursive-glob-match | ausente | 1.1.0 |

O inventário PrimeUIX foi preservado integralmente no lock: motion 0.0.10, styled raiz 1.0.0, styles 2.0.3, utils raiz 0.8.2; styled 0.7.4 sob styles e primeng; utils 0.6.4 sob motion, styles e primeng/styled; utils 0.7.2 sob primeng. Os caminhos completos constam no relatório experimental.

## 4. Escopo do lockfile e da integração

Nesta etapa, a comparação estrutural do lock integrado com a base `2d3e1b1386bdf04b7f520dabb721486806c8dd33` confirmou exatamente quatro entradas alteradas: glob, minimatch, path-scurry e inclusão do matcher não recursivo. Todas as demais entradas, incluindo a raiz e os peers, permanecem iguais. path-scurry ^3 é dependência exigida pelo novo glob; o matcher ^1.1 é exigido pelo novo minimatch.

O manifesto difere apenas pela adição da propriedade overrides; dependências diretas não foram modificadas. O diff das dependências tem oito adições no manifesto e 21 adições/13 remoções no lockfile. Não houve atualização ampla.

O diff total integrado contém apenas cinco arquivos: os dois manifestos de dependências e os três relatórios B05-B2. São 434 adições e 13 remoções. Não há alteração funcional TypeScript/HTML/CSS, de API, banco ou Docker, nem comentários novos no código. Todos os pacotes oficiais @angular/* enumerados no lock continuam na major 21.

## 5. Verificações técnicas Windows e Linux

Ambiente técnico utilizado: Node 24.16.0 e npm 11.13.0. As execuções abaixo ocorreram nas etapas de experimento, aplicação e revisão, não foram repetidas neste encerramento:

| Verificação | Resultado histórico |
| --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund | Aprovado; instalação reproduzível com scripts de dependências desabilitados |
| npm ls --all | Aprovado, sem peers inválidos ou dependências obrigatórias ausentes |
| npm explain glob/minimatch | Overrides confirmados nos ramos esperados |
| TypeScript --noEmit -p tsconfig.app.json | Aprovado |
| npm run build | Aprovado |
| npm run check:rate-limiting | Aprovado |
| node checks/library.cjs | Aprovado |
| node checks/watch-time.cjs | Aprovado |
| git diff --check | Aprovado antes dos commits |

No experimento, também passaram fixtures locais pequenas de glob assíncrono/síncrono/stream/ignore, minimatch com níveis 0/1/2, ignore-walk, cacache, empacotamento/manifesto/extração offline por pacote e matching de delegação TUF; geração de componente CLI em dry-run não escreveu componentes. Não houve teste adversarial de consumo excessivo de recursos nem cobertura de todas as operações das ferramentas.

Linux foi validado em container Debian x86_64 com imagem oficial node:24.16.0-bookworm-slim, em cópia independente sem reutilizar node_modules Windows. Instalação, árvore, TypeScript, build, três checks e fixtures passaram. O lock permaneceu idêntico. Essa execução não reproduz integralmente a infraestrutura real da Vercel nem configurações de instalação/cache do serviço.

Antes da publicação, a comparação de conteúdo confirmou 25 registros protegidos de Angular/PrimeNG/PrimeUIX/TypeScript/tslib preservados, inclusive instalações aninhadas. Nesta etapa, sua preservação no grafo integrado foi confirmada pela comparação completa do lockfile, sem reinstalação.

## 6. Build, assets e aviso CSS

O experimento comparou o mesmo código atual com o lock original e com a candidata. Os **24 arquivos** em browser tinham nomes, tamanhos e SHA-256 idênticos, totalizando **1.564.757 bytes**. O bundle inicial foi 543,66 kB, com estimativa transferida de 127,34 kB. A comparação Linux/Windows também passou; a aplicação e a revisão final reconfirmaram identidade com o controle.

Essa igualdade se refere aos artefatos locais comparados; não comprova identidade com o artefato efetivamente publicado. Nenhum artefato de produção foi baixado ou analisado independentemente nesta etapa.

Permanece o aviso conhecido de **profile.component.scss: 8,39 kB**, 390 bytes acima do aviso de 8 kB. Não é erro bloqueante e continua fora do escopo. Não foram alterados estilos ou budgets para suprimi-lo.

## 7. Validação funcional, Preview, produção e CORS

O operador informou **11 aprovados e zero reprovados** nos testes locais: login/autenticação, GuestGuard, logout, cadastro, exploração, Minha lista, episódios, perfil, temas, responsividade e Console/Network. Informou ausência de erros observados em Console/Network. O Codex não executou nem reproduziu independentemente esses testes. Data e horários exatos da sessão não foram informados; o documento funcional registra a data de documentação, não uma data presumida dos testes.

Para B05-B2.6, o operador informou validação do Preview e de produção. Confirmou deployment de produção concluído na Vercel, SeenIt funcionando normalmente e nenhum erro observado. Não foram fornecidos neste encerramento URL/identificador do deployment ou evidências técnicas adicionais; não foram inventados cenários, horários ou capturas.

O operador também confirmou a remoção da variável CORS temporária do Preview no Render. Não foram consultados os valores reais ou realizada verificação independente do CORS posterior. Não houve alteração de Render/Vercel, chamada funcional à API, acesso ao Supabase ou novo deploy pelo Codex neste encerramento.

Assim, integração Git foi confirmada independentemente; funcionamento de Preview/produção e remoção operacional de CORS permanecem **relatos do operador**, distintos dos checks locais de build e dos testes Linux.

## 8. PR, commits e sincronização da main

- PR: [#3 — Fix: Corrige vulnerabilidades transitivas do Angular CLI](https://github.com/GiovannaGrigo/SeenIt/pull/3).
- Base/compare: main ← security/b05-b2-dependency-review.
- Estado consultado no GitHub: **MERGED**, em 16/11/2026 às 15:03:06 (America/Sao_Paulo), equivalente a 18:03:06 UTC.
- Commit de integração e SHA final de main/origin/main: **`8dba27358a238a43a07af17319fcfdf6e8cd38cb`**.
- Commit de dependências: `37844430cd03324ecf22f6f0ffa67125098a2718`.
- Commit documental: `e9028075b17e4fdce787f1bd1cd233c78352fc86`.

Ambos os commits da implementação foram confirmados como ancestrais de origin/main com git merge-base --is-ancestor. Antes da sincronização, main local estava na base `2d3e1b1386bdf04b7f520dabb721486806c8dd33`, sem commits locais exclusivos, três commits atrás da remota (incluindo o merge). A worktree principal estava limpa.

Executados git fetch origin, git switch main e **git pull --ff-only origin main**, concluído por fast-forward. Main local e origin/main passaram a apontar para o mesmo SHA acima. Não houve reset, clean, rebase ou force.

## 9. Riscos e limitações residuais

- Overrides fora dos ranges oficiais dos pais precisam de acompanhamento futuro. As validações não equivalem a suporte formal dos mantenedores; mudanças de padrões, cache, entradas ou ferramentas podem revelar diferenças adicionais. path-scurry também mudou de major.
- Auditorias são retratos da base de advisories da data consultada. Zero alertas não certifica ausência de falhas, nem substitui análise de alcançabilidade ou monitoramento.
- Build e fixtures não cobrem toda a cadeia pacote/sigstore/TUF/MCP, todos os ambientes de CI ou scripts de instalação habilitados. O cancelamento foi testado com fixture limitada, sem prova adversarial da corrida descrita no advisory.
- Checks existentes não são uma suíte end-to-end completa. O alvo ng test não estava configurado; nenhum runner ou dependência foi adicionado para contornar isso.
- Testes manuais e validação de produção são relatos do operador. Não houve inspeção independente do artefato publicado, da configuração real de CI/Vercel/Render ou do CORS final.
- Permanecem o aviso CSS conhecido e a depreciação de @angular/animations; ambos fora do escopo. Angular permanece na major 21.

## 10. Estratégia de rollback

Plano somente, **não executado**: em etapa específica, revisar e reverter exclusivamente a alteração de dependências do commit `37844430cd03324ecf22f6f0ffa67125098a2718`, preservando documentação, alterações posteriores e maintenance-phase1. A base anterior dos dois arquivos é `2d3e1b1386bdf04b7f520dabb721486806c8dd33`.

Após a reversão controlada, reinstalar com o lock anterior e scripts desabilitados, repetir auditoria, árvore, build e checks e validar manualmente. A reversão pode reintroduzir as vulnerabilidades historicamente sinalizadas como 16 moderadas. Não reverter a branch inteira ou fazer reset/clean. Nenhum rollback ou redeploy foi iniciado.

## 11. Preservação e próximos passos

A branch security/b05-b2-dependency-review permanece em `e9028075b17e4fdce787f1bd1cd233c78352fc86`. b05-b1-angular-security, fix/guest-route-guard e maintenance-phase1 também foram mantidas. A worktree maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com suas alterações locais preexistentes; nenhuma escrita foi realizada nela. A comparação final de hashes dos **161 arquivos** versionados e não rastreados não ignorados inventariados e do estado Git foi idêntica ao snapshot anterior à sincronização; arquivos ignorados não integram esse inventário.

Nesta etapa, somente este relatório é novo e não rastreado. Nenhum arquivo já versionado apresenta alteração no Git, nenhuma dependência foi modificada e os três relatórios anteriores foram preservados. O checkout reaplicou CRLF aos cinco arquivos integrados, conforme a configuração local do Git; o conteúdo, normalizado para LF, foi confirmado idêntico ao commit de implementação. Main e origin/main continuam no mesmo SHA; git diff --check passou. Não foram executados commit, push, migrations, acesso ao Supabase, alterações de API/banco, alterações de Render/Vercel ou deploy.

Próximos passos recomendados: acompanhar patches oficiais compatíveis com Angular 21 para eventualmente remover os overrides; repetir auditorias e validações quando o grafo ou a base de advisories mudar; manter os riscos condicionais das ferramentas documentados; tratar budget CSS e melhoria de testes em escopos próprios. O eventual versionamento deste relatório depende de etapa futura autorizada. Não há pendência de integração do PR #3 identificada.

**B05-B2 PRONTO PARA ENCERRAMENTO**
