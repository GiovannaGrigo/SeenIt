# B05-B2.1 — Diagnóstico das dependências moderadas restantes

## 1. Objetivo, contexto e preparação

Investigação realizada em **16/11/2026 (America/Sao_Paulo)**, mantendo Angular 21 e sem implementar correções. Base: `2d3e1b1386bdf04b7f520dabb721486806c8dd33`, encerramentos B05-B1 e GuestGuard informados pelo operador.

A main estava limpa. `git fetch origin` concluiu com código 0 após repetição fora do sandbox (a primeira tentativa não conseguiu escrever FETCH_HEAD). Depois do fetch, main e origin/main apontavam para a referência acima; `git rev-list --left-right --count main...origin/main` retornou `0 0`. Criada a branch `security/b05-b2-dependency-review`; a primeira tentativa foi bloqueada pela permissão de escrita em .git e a repetição autorizada concluiu.

Preservadas as branches b05-b1-angular-security, fix/guest-route-guard, main e maintenance-phase1. Nenhuma escrita foi realizada na worktree maintenance-phase1, que permanece no commit `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Suas alterações locais de manutenção são preexistentes e não foram limpas. A única criação de arquivo no repositório nesta etapa é este relatório; não houve commit ou push.

Foram inspecionados package.json, package-lock.json e angular.json do frontend e os relatórios B05-DEPENDENCY-SECURITY-REVIEW, B05-B14-ANGULAR-APPLICATION e B05-B1-FINAL-SUMMARY. Seus resultados antigos foram tratados como históricos, não como auditoria atual.

## 2. Versões atuais

| Grupo/pacote | Versão |
| --- | --- |
| Angular animations/common/compiler/core/forms/platform-browser/router e compiler-cli | 21.2.26 |
| Angular CLI / Build | 21.2.30 |
| PrimeNG / @primeuix/themes | 21.1.10 / 3.0.1 |
| TypeScript / tslib | 5.9.3 / 2.8.1 |
| Node / npm locais | 24.16.0 / 11.13.0 |

As dependências diretas Angular, PrimeNG, themes, TypeScript e tslib estão fixadas no manifesto. Nenhum manifesto ou lockfile foi alterado.

## 3. Auditoria atualizada

Executado `npm audit --json` em frontend. A primeira consulta falhou por acesso ao endpoint; foi repetida com autorização fora do sandbox e retornou JSON válido, auditReportVersion 2, código **1 por vulnerabilidades**.

| Severidade | Pacotes sinalizados |
| --- | ---: |
| Crítica | 0 |
| Alta | 0 |
| Moderada | 16 |
| Baixa / informativa | 0 / 0 |
| Total | 16 |

Metadados de dependências: prod 27, dev 501, optional 127, peer 4, total 528. As categorias se sobrepõem. O resultado coincide com o histórico, mas foi confirmado por nova consulta. São **dois advisories independentes**, não 16 falhas independentes. Todos os 16 nós têm `dev: true` no lockfile; apenas o CLI é dependência direta. Nenhum pacote de produção foi sinalizado no audit completo.

| Pacote instalado | Versão | Via informado pelo audit | Faixa agregada sinalizada pelo npm |
| --- | --- | --- | --- |
| @angular/cli (direto) | 21.2.30 | pacote | 17.0.0-next.0 - 22.1.0-rc.0 |
| @npmcli/package-json | 7.0.5 | glob | 3.1.0 - 8.0.0 |
| @npmcli/run-script | 10.0.4 | @npmcli/package-json | 7.0.4 - 10.0.4 |
| @sigstore/sign | 4.1.1 | make-fetch-happen | 2.0.0 - 4.1.1 |
| @sigstore/tuf | 4.0.2 | tuf-js | * |
| @tufjs/models | 4.1.0 | minimatch | >=1.0.1 |
| cacache | 20.0.4 | glob | 17.0.5 - 21.0.1 |
| glob | 13.0.7 | advisory próprio e minimatch | 9.0.0 - 14.0.1 |
| ignore-walk | 8.0.0 | minimatch | >=6.0.2 |
| make-fetch-happen | 15.0.6 | cacache | 13.0.0 - 15.0.6 |
| minimatch | 10.2.6 | advisory próprio | 7.0.0 - 11.0.0 |
| npm-packlist | 10.0.4 | ignore-walk | >=8.0.1 |
| npm-registry-fetch | 19.1.1 | make-fetch-happen | 16.0.0 - 19.1.1 |
| pacote | 21.5.1 | package-json, run-script, cacache, packlist, registry-fetch, sigstore | >=17.0.0 |
| sigstore | 4.1.1 | @sigstore/sign e @sigstore/tuf | >=1.6.0 |
| tuf-js | 4.1.0 | @tufjs/models e make-fetch-happen | >=1.1.2 |

Todas as linhas têm severidade moderate. As faixas dos pais são metadados de propagação do npm, não advisories próprios nem indicação de que toda versão futura estará vulnerável. Para todos os resultados, fixAvailable sugere CLI 22.2.7, isSemVerMajor=true. Essa sugestão foi rejeitada por estar fora do escopo Angular 21.

## 4. Advisories e condições

| Causa | Referência | Faixa vulnerável | Primeira corrigida |
| --- | --- | --- | --- |
| glob: cancelamento não interrompe travessia | [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), CVE-2026-118773, CWE-400 | >=9.0.0 <=14.0.1 | 14.0.2 |
| minimatch: amplificação no pré-processamento | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249), CVE-2026-118764, CWE-405 | >=7.0.0 <11.0.1 | 11.0.1 |

glob pode continuar consumindo I/O/CPU depois do aborto. A relevância depende de padrões ou árvores influenciados por atacante e uso de AbortSignal como limite de trabalho. minimatch pode consumir CPU/memória com padrões controlados por atacante quando optimizationLevel >=2. São problemas de disponibilidade. Não foi demonstrado que esses caminhos e opções sejam alcançáveis por entradas externas no SeenIt. Nenhuma prova de conceito foi executada.

## 5. Árvore e restrições exatas

`npm ls glob minimatch --all`, `npm explain glob` e `npm explain minimatch` concluíram com código 0. Árvore instalada, deduplicada:

```text
@angular/cli@21.2.30 -> pacote@21.5.1 (fixo)
  -> @npmcli/package-json@7.0.5 -> glob@13.0.7 -> minimatch@10.2.6
  -> @npmcli/run-script@10.0.4 -> @npmcli/package-json@7.0.5
  -> cacache@20.0.4 -> glob@13.0.7
  -> npm-packlist@10.0.4 -> ignore-walk@8.0.0 -> minimatch@10.2.6
  -> npm-registry-fetch@19.1.1 -> make-fetch-happen@15.0.6 -> cacache@20.0.4
  -> sigstore@4.1.1
     -> @sigstore/sign@4.1.1 -> make-fetch-happen@15.0.6
     -> @sigstore/tuf@4.0.2 -> tuf-js@4.1.0
        -> make-fetch-happen@15.0.6
        -> @tufjs/models@4.1.0 -> minimatch@10.2.6
```

package-json e cacache exigem glob `^13.0.0`. glob exige minimatch `^10.2.2`; ignore-walk exige `^10.0.3`; models exige `^10.1.1`. Portanto as versões corrigidas não cabem nos intervalos atuais. Atualizar somente glob não corrige os ramos ignore-walk/models.

## 6. Impacto prático no SeenIt

O builder é @angular/build:application, entrada src/main.ts, sem SSR configurado. Os pacotes afetados pertencem à cadeia Node do CLI/pacote, destinada a obter, empacotar, percorrer e verificar pacotes; não são dependências de runtime da aplicação. A busca em frontend/src e frontend/checks não encontrou referências a glob, minimatch, pacote ou @angular/cli.

**Navegador:** não foi identificado caminho de inclusão desses pacotes no bundle pelas dependências/configuração inspecionadas. É uma inferência estática, não uma inspeção do artefato publicado ou prova por sourcemap/metafile.

**Build/CI e desenvolvimento:** há risco condicional de indisponibilidade quando ferramentas processarem padrões/árvores não confiáveis, por exemplo em entradas de pacotes ou contribuições maliciosas. O CLI ser instalado não prova que cada operação ng build execute todos esses ramos. Não foram rastreados todos os call sites nem as opções de otimização/cancelamento efetivas.

Não foi demonstrada exploração remota na SPA de produção. Isso não elimina o risco no ambiente de ferramentas nem transforma zero alertas em garantia de segurança. Não houve inspeção de CI remoto, execução de serviços, acesso ao Supabase ou deploy.

## 7. Registro npm e alternativas Angular 21

Consultas públicas `npm view` em 16/11/2026, somente leitura. Referências reproduzíveis: [CLI](https://www.npmjs.com/package/@angular/cli), [Build](https://www.npmjs.com/package/@angular/build), [glob](https://www.npmjs.com/package/glob), [minimatch](https://www.npmjs.com/package/minimatch). Foram consultados version, dependencies e engines conforme aplicável, incluindo package-json@7, cacache@20, ignore-walk@8 e models@4.

Resultados: CLI e Build major 21 terminam em 21.2.30. glob 13 termina em 13.0.7; minimatch 10 termina em 10.2.6. glob 14 tem releases até 14.0.6; minimatch 11 até 11.0.8. Não foi encontrado backport corrigido nas majors instaladas. package-json 7.0.5 e cacache 20.0.4 continuam em glob ^13; ignore-walk 8.0.0 continua em minimatch ^10. models 4.1.1 é patch disponível, mas exige minimatch ^10.2.1 e continua afetado. tuf-js fixa models 4.1.0, portanto esse patch também não é seleção livre no ramo atual.

| Alternativa atual → candidata | Compatibilidade e benefício | ERESOLVE / ferramentas | Arquivos e PrimeNG/PrimeUIX | Testes futuros |
| --- | --- | --- | --- | --- |
| Atualização somente do lock dentro dos ranges atuais | Nenhuma candidata corrigida identificada; mantém os dois advisories | Baixo risco de peers; churn transitivo sem benefício comprovado | Lock mudaria; diretas preservadas; exigir diff visual zerado | Auditoria/ls e checks se houver mudança |
| glob 13.0.7 → 14.0.2; minimatch 10.2.6 → 11.0.1 | Mínimos corrigidos, mas majors fora dos ranges dos pais | ERESOLVE de peers não é o risco principal; APIs/comportamento entre majors são risco moderado/alto | Requer lock e mecanismo de substituição; não exige mudar Angular/Prime | Validação completa da ferramenta e build |
| Overrides restritos na subárvore CLI/pacote: glob 14.0.6 e minimatch 11.0.8 | Candidatas publicadas corrigem estes advisories; não equivalem a compatibilidade validada | Risco moderado de ferramentas; resolução não simulada. Evitar override global que afete outros ramos | package.json e lock mudariam; preservar todo o inventário Prime, inclusive aninhado | Instalação isolada, audit/ls, operações CLI e plano da seção 9 |
| CLI/Build 21.2.30 → patch oficial posterior 21.x | Nenhuma versão posterior disponível na consulta; preferível se futuro patch corrigir toda a árvore | Avaliar peers/release notes quando existir; não presumir correção | Diretas e lock mudariam; preservar Prime | Plano completo e comparação do grafo |
| models 4.1.0 → 4.1.1 isoladamente | Não corrige minimatch; pai fixa versão atual | Forçar patch acrescenta risco sem resolver causa | Override/lock seriam necessários nesse ramo; sem benefício visual | Não recomendado como correção |
| CLI 21.2.30 → 22.2.7 sugerido pelo audit | Fora do escopo; não aplicar | Migração incompatível e possível conflito com stack 21 | Mudança direta/lock; risco indireto ao conjunto de peers | Não propor como B05-B2.2 |

glob 14.0.6 aceita Node `18 || 20 || >=22`; minimatch 11.0.8 aceita `20 || >=22`. Node local 24.16.0 satisfaz ambos; engines dos demais transitivos e ambiente Linux/CI precisam ser reavaliados no grafo candidato. CLI aceita Node `^20.19.0 || ^22.12.0 || >=24.0.0`. Aceitação de engines não comprova compatibilidade de API.

Não há evidência de solução automática que mantenha todos os ranges atuais e corrija os dois advisories. Não foi investigado exaustivamente cada release major dos pais para substituir pacote inteiro: seria mudança maior, com mais superfície de regressão. Overrides são apenas alternativa teórica nesta etapa; não foi escrita configuração nem simulado install. Não há promessa de zero vulnerabilidades no grafo candidato.

## 8. Recomendação de menor mudança segura

Manter a árvore atual nesta etapa. Para B05-B2.2, começar por nova consulta de patches oficiais Angular 21 e backports. Se permanecer o cenário atual, avaliar em ambiente isolado um candidato com overrides limitados à cadeia CLI/pacote para glob 14.0.6 e minimatch 11.0.8, cobrindo também ignore-walk e models. Essa é a menor superfície candidata identificada para remover as duas causas sem mudar dependências diretas de framework/UI; **não está aprovada tecnicamente antes dos testes** e exige alteração de manifesto/lock em etapa autorizada.

Não adotar override global, audit fix, migração Angular 22 ou atualização ampla somente para zerar contagem. Se a validação falhar, manter os alertas documentados e acompanhar correção upstream, sem force ou legacy-peer-deps. A decisão deve considerar o risco condicional de disponibilidade das ferramentas e o risco de regressão das majors transitivas.

## 9. Plano de testes para B05-B2.2

1. Registrar hashes e inventário integral do lock atual, incluindo PrimeNG/PrimeUIX aninhados; usar ambiente isolado sem tocar maintenance-phase1.
2. Resolver somente o candidato autorizado, conferir diff de manifesto/lock, engines, peers e todas as ocorrências glob/minimatch. Executar npm ls --all, explain e audit completo; deduplicar advisories e rejeitar novas altas/críticas. Não aceitar major Angular 22 ou mudanças Prime.
3. Validar instalação reproduzível com scripts controlados em Windows e Linux/CI. Verificar comandos CLI realmente usados, obtenção/empacotamento/cache e correspondência de padrões; build sozinho não cobre pacote/sigstore.
4. Executar build de produção, TypeScript sem emissão, check:rate-limiting, checks/library.cjs e checks/watch-time.cjs. Comparar assets, budgets e aviso preexistente de profile.component.scss. ng test não tem alvo configurado segundo B05-B14; não declarar sua execução sem corrigir essa limitação em escopo próprio.
5. Smoke tests em navegador: GuestGuard, login/redirecionamento, biblioteca, detalhes/episódios, perfil, temas claro/escuro, ícones e responsividade; sem acesso ao banco real nesta investigação. Inspecionar estatísticas do bundle para confirmar exclusão da cadeia de ferramentas.
6. Revisar diff final e rollback restrito aos arquivos da candidata antes de recomendar incorporação. Nenhum deploy automático deve ser iniciado como parte da investigação.

## 10. Limitações e encerramento

Não foram executados npm install, update, audit fix, build, testes, migrations, deploy ou acesso à API/banco/Supabase. A auditoria consulta metadados do registro e não executa análise de alcançabilidade; o grafo instalado foi conferido pelos comandos de explicação, mas não reinstalado. Não foi inspecionado bundle publicado, fluxo completo das ferramentas, configuração real de CI ou artefato Linux. ERESOLVE e compatibilidade dos overrides permanecem não verificados. Não foi realizado teste adversarial ou análise exaustiva de outros advisories nas versões candidatas.

O registro npm e os advisories estavam disponíveis após autorização de rede; nenhuma versão corrigida foi inferida sem consulta. Logs operacionais do npm podem existir no cache externo; não foram criados arquivos adicionais de evidências no repositório. Nenhuma credencial ou token foi incluído neste relatório. A worktree de manutenção foi apenas lida para identificação de estado, sem escrita; não foi feita comparação byte a byte de todos os seus arquivos antes/depois.

**B05-B2.1 DIAGNÓSTICO CONCLUÍDO**
