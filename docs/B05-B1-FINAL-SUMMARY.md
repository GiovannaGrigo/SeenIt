# B05-B1.8 — Encerramento da atualização de segurança Angular 21

Data: 16/11/2026 (America/Sao_Paulo).

**B05-B1 PRONTO PARA ENCERRAMENTO.** Integração confirmada no GitHub e main local sincronizada por fast-forward. Este relatório permanece local, sem commit ou push.

## 1. Objetivo e integração

Atualizar as dependências de segurança do frontend mantendo exclusivamente Angular 21, preservando PrimeNG, PrimeUIX e tslib e evitando mudanças funcionais, de API ou banco.

- PR: https://github.com/GiovannaGrigo/SeenIt/pull/1
- Base/compare: main ← b05-b1-angular-security.
- Estado consultado no GitHub: MERGED, em 16/11/2026 às 09:14:33 (America/Sao_Paulo).
- Commit de integração e SHA final da main: `4746d85cfa506bf77f42ceeba8bb6920748f19c8`.
- Commits originais confirmados como ancestrais de origin/main: `f80e45fbd056cae53b67b351bdbc0e44c40927fc` (dependências) e `8ef7b33ea4c4ac2ffeb2f514c77d5219ca8663f2` (documentação).
- Main anterior: `9c4fe3761c83bed72955b1d257d84e3c4836fa8b`. Atualização local por `git pull --ff-only origin main`, sem merge local, reset ou force.

## 2. Versões anteriores e finais

Versões efetivamente resolvidas, conforme os registros B05-B1.4/B05-B1.5 e conferência atual de manifesto, lockfile e manifestos instalados:

| Pacote/grupo | Antes | Final |
| --- | --- | --- |
| @angular/animations, common, compiler, core, forms, platform-browser, router | 21.2.23 | 21.2.26 |
| @angular/compiler-cli | 21.2.23 | 21.2.26 |
| @angular/cli e @angular/build | 21.2.24 | 21.2.30 |
| @angular/cdk | 21.2.14 | 21.2.14 |
| primeng | 21.1.10 | 21.1.10 |
| @primeuix/themes | 3.0.1 | 3.0.1 |
| tslib | 2.8.1 | 2.8.1 |
| typescript | 5.9.3 | 5.9.3 |

As demais cópias PrimeUIX, inclusive aninhadas, foram preservadas conforme o inventário B05-B1.3.1. Todos os pacotes oficiais @angular/* enumerados no lockfile e seus manifestos físicos conferidos continuam na major 21; nenhum Angular 22 foi identificado nesses conjuntos. Não houve instalação ou atualização de dependências nesta etapa.

## 3. Auditoria e validação local anteriores

Resultados históricos da auditoria B05-B1.4; não foi executada nova auditoria neste encerramento:

| Severidade | Antes | Depois |
| --- | ---: | ---: |
| Crítica | 3 | 0 |
| Alta | 9 | 0 |
| Moderada | 20 | 16 |
| Total de pacotes sinalizados | 32 | 16 |

A contagem inclui propagação de alertas aos pais e não representa falhas independentes.

Conforme [B05-B14-ANGULAR-APPLICATION.md](B05-B14-ANGULAR-APPLICATION.md), foram aprovados: npm ci com scripts desabilitados, npm ls --all, build de produção, TypeScript sem emissão e os três checks existentes (rate-limiting, library e watch-time). A auditoria retornou código 1 pelos 16 moderados, com consulta válida. Esses comandos não foram repetidos nesta etapa documental.

O operador aprovou dez cenários manuais: autenticação/redirecionamento; cadastro/validações; exploração/detalhes de séries; biblioteca/status; episódios/sentimentos/personagem; perfil/estatísticas; componentes PrimeNG; temas; responsividade; console sem novos erros. O Codex não executou esses testes manuais. Origem e limites desse relato estão em [B05-B15-FUNCTIONAL-VALIDATION.md](B05-B15-FUNCTIONAL-VALIDATION.md).

## 4. Preview Vercel, CORS e produção

Na publicação da branch e criação do PR, o GitHub informou sucesso nos checks Vercel e Vercel Preview Comments; o status da integração Vercel informou conclusão do deployment automático. Isso não equivale a uma validação funcional independente do Preview pelo Codex.

O login do Preview apresentou bloqueio CORS. No diagnóstico anterior, OPTIONS /api/auth/login retornou 204 sem Access-Control-Allow-Origin/Methods/Headers para a origem Preview, enquanto a origem de produção https://seenit-web-dusky.vercel.app recebeu 204 com origem exata, método POST e cabeçalho content-type autorizados. Não houve POST de login, credenciais ou acesso ao banco nesse diagnóstico.

A correção operacional recomendada foi acrescentar somente a origem exata do Preview a Cors__AllowedOrigins__N no ambiente Render, preservando as origens legítimas, sem wildcard, reflexão arbitrária ou alteração do backend. O operador posteriormente informou que a autorização temporária foi removida e que realizou Save and deploy no Render. O Codex não consultou as variáveis reais nem confirmou independentemente o estado posterior do CORS.

O operador informou deployment de produção concluído na Vercel, API funcionando no Render e aplicação principal acessível e funcionando. Informou também que a raiz com sessão válida redireciona corretamente para Minha lista. Esses são relatos do operador: nesta etapa não houve acesso funcional aos serviços, verificação do artefato de produção ou novo deploy.

## 5. Riscos residuais e limitações

- Permanecem 16 alertas moderados na cadeia dev do CLI, associados à cadeia glob/minimatch, conforme a auditoria histórica. A sugestão de migrar para Angular 22 não foi aplicada. A base de advisories pode mudar.
- Permanece o aviso não bloqueante de budget de profile.component.scss: 8,39 kB, acima do aviso de 8 kB e abaixo do erro de 16 kB. Há também depreciação de @angular/animations.
- Permanecem os riscos documentados do runtime PrimeUIX preservado, as majors transitivas revisadas e as diferenças possíveis entre Windows e Linux/CI.
- Os três checks não são uma suíte end-to-end. O alvo ng test não estava configurado; o spec de EpisodioModal não foi executado. Os testes manuais têm a cobertura informada pelo operador, sem reprodução independente pelo Codex.
- Funcionamento relatado e checks de deployment não certificam segurança nem demonstram equivalência entre artefato publicado e commit sem inspeção específica.

## 6. Melhoria independente: guestGuard

O operador identificou que usuários autenticados ainda conseguem acessar diretamente /login. A melhoria proposta é um guestGuard em tarefa própria, com critérios e testes específicos. Ela está fora do B05-B1; nenhum guard, rota ou comportamento funcional foi alterado para tratá-la.

## 7. Próximas etapas

Não foi encontrada definição explícita dos identificadores B05-B2, B05-B3 e B05-B4 nos relatórios existentes. A organização abaixo é uma proposta para confirmação de escopo, baseada no plano B05-A, e não registra trabalho executado ou autorização de mudanças:

| Etapa proposta | Escopo a definir e validar separadamente |
| --- | --- |
| B05-B2 | Reavaliar os transitivos dev residuais e a cadeia CLI, com auditoria atualizada e alternativas compatíveis; manter a restrição de Angular 21 enquanto vigente. |
| B05-B3 | Tratar o motor/bundle SQLite dos testes de integração, ou avaliar PostgreSQL local descartável; comprovar isolamento e equivalência dos checks sem acessar banco real. |
| B05-B4 | Revisar imagem, runtime e dependências .NET/nativas: digest/SBOM, auditoria, patches e validação coordenada de EF/Npgsql, em escopo próprio. |

Referência: seção 9 de [B05-DEPENDENCY-SECURITY-REVIEW.md](B05-DEPENDENCY-SECURITY-REVIEW.md). GuestGuard deve permanecer em melhoria funcional independente dessas correções.

## 8. Preservação e pendências de encerramento

A árvore principal estava limpa antes da sincronização. A integração altera somente frontend/package.json, frontend/package-lock.json e os sete relatórios B05 já aprovados. Não há alteração de código funcional do frontend, API ou banco no diff integrado. Nesta etapa, a única adição é este relatório.

A branch b05-b1-angular-security foi preservada em `8ef7b33ea4c4ac2ffeb2f514c77d5219ca8663f2`. maintenance-phase1 permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, em worktree separado, com suas alterações locais preexistentes preservadas; nenhuma operação de escrita foi realizada nela.

Não foram executados commit, push, exclusão de branch, migrations, acesso ao Supabase, alterações de Render/Vercel ou deploy nesta etapa. A main e origin/main apontam para o mesmo SHA de integração; o relatório fica não rastreado até uma futura etapa autorizada.

Pendências: revisão e eventual versionamento deste relatório em etapa própria; confirmação do escopo das etapas B05-B2/B3/B4; manutenção dos riscos residuais registrados. Nenhuma pendência de integração do PR #1 foi identificada. A melhoria guestGuard não bloqueia este encerramento.

**B05-B1 PRONTO PARA ENCERRAMENTO.**
