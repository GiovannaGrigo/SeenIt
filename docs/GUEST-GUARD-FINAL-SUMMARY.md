# GuestGuard — Resumo final

Data: 16/11/2026.

## Integração e escopo

- PR: [#2 — Fix: Corrige validação de sessão e acesso às rotas de autenticação](https://github.com/GiovannaGrigo/SeenIt/pull/2).
- Estado confirmado no GitHub: MERGED em 16/11/2026, às 11:37:34 (America/Sao_Paulo).
- Commit de implementação: `2b9c64558041adf16ce752fe9abe0e52fad5c758`.
- SHA do merge e da main sincronizada: `77f1ca4c40ef6bba51d1b8b1743372dbc1347147`.
- Base anterior: `d22286fe5c45cf14569e7b8b6ccf1e346c57ae2e`.

A árvore do merge corresponde integralmente à árvore do commit de implementação. O diff desde a base contém exclusivamente os três arquivos aprovados: `frontend/src/app/core/auth.service.ts`, `frontend/src/app/core/guest.guard.ts` e `frontend/src/app/app.routes.ts` (60 inserções, 6 remoções). Não foram adicionados comentários ao código nem alterados API, banco ou dependências. Angular permanece em 21.2.26, com CLI/Build 21.2.30.

Esta melhoria é independente do B05-B1. Seus documentos de encerramento não foram modificados.

## Problema e causa

Uma sessão ativa não impedia o acesso direto a `/login` ou `/cadastro`. Essas rotas não tinham guard para visitantes; o redirecionamento da raiz para `/minha-lista` não era aplicado a elas. Além disso, o estado de autenticação considerava apenas a presença de token e usuário, sem avaliar a expiração.

## Correção

O AuthService mantém token e usuário nos signals existentes e nas chaves `seenit_token` e `seenit_user`. A recuperação do usuário valida sua estrutura. A avaliação de `isAuthenticated()` exige token, usuário recuperado, JWT estruturalmente interpretável em Base64URL/UTF-8 e claim `exp` numérico, finito e posterior ao horário atual.

O relógio é consultado a cada chamada, sem cache temporal em computed. Sessões inválidas são limpas do armazenamento e dos signals, inclusive durante a inicialização. A limpeza idempotente usa `untracked` para sair do contexto reativo do consumidor. Não foram introduzidos HTTP, timers, dependências ou novas chaves de armazenamento.

O guestGuard é um `CanActivateFn`, obtém o AuthService por `inject()` e reutiliza exclusivamente `isAuthenticated()`. Retorna UrlTree para `/minha-lista` com sessão válida e `true` com sessão ausente ou inválida. Não realiza navegação imperativa.

Somente `/login` e `/cadastro` receberam `canActivate: [guestGuard]`. O authGuard foi preservado em `/minha-lista`, `/explorar` e `/serie/:id`, assim como os redirecionamentos da raiz e wildcard. Login bem-sucedido continua abrindo Minha lista; cadastro bem-sucedido continua abrindo Explorar. Componentes e layouts não foram alterados.

## Verificações técnicas

Executadas e aprovadas na revisão anterior ao commit:

- `node node_modules/typescript/bin/tsc -p tsconfig.app.json --noEmit`, no frontend.
- `npm run build`.
- `npm run check:rate-limiting`.
- `node checks/library.cjs`.
- `node checks/watch-time.cjs`.
- `git diff --check` e verificação do diff staged.

O build apresentou aviso de orçamento CSS em `profile.component.scss`: 8,39 kB para orçamento de aviso de 8 kB. O arquivo não foi alterado nesta melhoria.

Nas etapas de implementação, testes isolados temporários executaram o serviço com sessões sintéticas e os guards com injeção Angular e UrlTree reais. Cobriram expiração futura, passada e no limite, JWT malformado, exp ausente/inválido, usuário ausente/JSON inválido, UTF-8, passagem do tempo, limpeza reativa, logout, configuração das duas rotas e compatibilidade do destino com authGuard. Não usaram tokens reais, credenciais ou HTTP. Esses testes não foram adicionados como suíte persistente nem substituem testes completos de navegação em navegador.

O projeto possui script `ng test`, mas não possui target test configurado no angular.json. O spec existente de EpisodioModal não foi executado como parte desta melhoria. Nenhum runner foi instalado.

Nesta etapa de encerramento foram verificadas a integração, a igualdade das árvores, as rotas e as versões; os checks técnicos anteriores não foram reexecutados.

## Testes manuais

O operador informou aprovação de oito de oito testes funcionais no navegador local antes da publicação do PR. A aprovação é atribuída ao operador, sem reprodução independente pelo Codex.

A relação efetivamente informada pelo operador contém estes oito cenários:

1. Usuário autenticado acessa `/login` — APROVADO.
2. Usuário autenticado acessa `/cadastro` — APROVADO.
3. Atualização da página — APROVADO.
4. Logout — APROVADO.
5. Usuário não autenticado acessa `/cadastro` — APROVADO.
6. Login normal — APROVADO.
7. Sessão inválida — APROVADO.
8. Console e navegação — APROVADO.

Os resultados acima foram informados pelo operador e não foram executados independentemente pelo Codex. Não representam testes adicionais nesta etapa documental.

## Preview, produção e CORS

Na consulta ao GitHub durante este encerramento, o PR #2 apresentou `Vercel: SUCCESS` e `Vercel Preview Comments: SUCCESS`. O [comentário da integração](https://github.com/GiovannaGrigo/SeenIt/pull/2#issuecomment-6919343232) registra o deployment como Ready e fornece o endereço do Preview. Isso comprova o status publicado pela integração, sem constituir validação funcional independente do Preview.

O operador informou ter testado o Preview da Vercel com sucesso, sem erros observados. Após o merge, confirmou o funcionamento em produção. Posteriormente, removeu a variável CORS temporária do Render.

Após o merge, o operador confirmou deployment de produção concluído na Vercel, testes funcionais de produção aprovados e nenhum erro observado. Também confirmou a remoção da variável CORS temporária do Preview no Render e a execução de Save and deploy. Esses registros são declarações do operador; o Codex não acessou configurações reais nem realizou verificação independente desses ambientes nesta etapa.

Não houve acesso ao Supabase, alteração de Vercel/Render ou execução de deploy pelo Codex durante o encerramento.

## Sincronização local

Antes da sincronização, a branch `fix/guest-route-guard` estava limpa. Após `git fetch origin`, foram confirmados o merge e a ausência de mudanças fora do escopo. A main foi atualizada exclusivamente com `git pull --ff-only origin main`, ficando sem divergência em relação a origin/main.

A branch `fix/guest-route-guard` foi preservada no commit de implementação. A branch e worktree `maintenance-phase1` não foram alteradas; sua referência permanece em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Não foram executados reset, clean, rebase, force ou exclusões.

Este documento foi a única alteração local criada na etapa 2.5, sem commit ou publicação naquela etapa. A etapa 2.6 autoriza exclusivamente sua revisão, versionamento e publicação na main.

## Limitações e melhorias futuras

- A expiração é reconhecida na próxima avaliação, sem timer; não há atualização automática apenas pela passagem do tempo.
- A sincronização de autenticação entre abas não foi implementada.
- A validação criptográfica do JWT permanece responsabilidade da API. A avaliação local serve à navegação.
- A rota `/perfil` sem authGuard foi identificada anteriormente e deve ser analisada em tarefa separada.
- O suporte existente exige localStorage; não foi introduzida compatibilidade com ambientes sem esse armazenamento.
- A suíte isolada temporária pode ser convertida em regressões persistentes em tarefa futura, sem instalação desnecessária de runner.
- O aviso de orçamento CSS permanece fora deste escopo.

Não foi identificada pendência de integração. A aprovação funcional no Preview e em produção e a remoção do CORS temporário foram informadas pelo operador, conforme registrado acima.

GUESTGUARD ENCERRADO
