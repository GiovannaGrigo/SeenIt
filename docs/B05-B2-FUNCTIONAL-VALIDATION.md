# B05-B2.4 — Validação funcional manual

Registro em 16/11/2026 (America/Sao_Paulo), durante a revisão B05-B2.5. A data e os horários exatos de execução dos testes não foram informados pelo operador; a data deste registro não é uma atribuição de data aos testes.

## Ambiente e origem dos resultados

Validação no ambiente local do SeenIt, na branch `security/b05-b2-dependency-review`, após a aplicação controlada B05-B2.3. Angular Framework 21.2.26, CLI/Build 21.2.30, PrimeNG 21.1.10, @primeuix/themes 3.0.1, TypeScript 5.9.3 e tslib 2.8.1. Não foram informados navegador, resolução, URLs locais ou demais detalhes da sessão manual.

Os resultados abaixo foram **informados pelo operador**, sem reprodução independente pelo Codex. Não foram fornecidas capturas de tela, gravações, logs de Console/Network ou horários de execução. Nenhuma evidência adicional é presumida.

## Cenários informados

| Nº | Cenário | Resultado informado |
| --- | --- | --- |
| 1 | Login e autenticação | APROVADO |
| 2 | GuestGuard | APROVADO |
| 3 | Logout | APROVADO |
| 4 | Cadastro | APROVADO |
| 5 | Exploração | APROVADO |
| 6 | Minha lista | APROVADO |
| 7 | Episódios | APROVADO |
| 8 | Perfil | APROVADO |
| 9 | Temas | APROVADO |
| 10 | Responsividade | APROVADO |
| 11 | Console e Network | APROVADO |

**Total informado: 11 aprovados, 0 reprovados.** Segundo o operador, não foram observados erros em Console e Network durante essa validação. O alcance dessa declaração se limita aos cenários e à sessão por ele observados.

## Relação com as verificações técnicas

O diagnóstico [B05-B2.1](B05-B2-DEPENDENCY-DIAGNOSIS.md) registra as duas causas independentes dos 16 alertas moderados originais. O [experimento B05-B2.2](B05-B2-OVERRIDE-EXPERIMENT.md) registra a validação isolada Windows/Linux e os limites dos overrides. Esses documentos descrevem o estado de suas respectivas etapas; suas referências ao frontend ainda sem overrides são históricas.

Em B05-B2.3, os overrides foram aplicados exclusivamente a frontend/package.json e frontend/package-lock.json. Foram aprovados npm ci com scripts desabilitados, npm ls, auditoria sem alertas, TypeScript sem emissão, build, os três checks existentes e fixtures locais das ferramentas. Os 24 arquivos do bundle foram idênticos ao controle por SHA-256. Esses resultados técnicos foram executados pelo Codex e não constituem reprodução dos testes manuais acima.

## Limitações e riscos remanescentes

A aprovação manual informada não equivale a teste end-to-end automatizado, auditoria de segurança, inspeção do artefato publicado ou validação de todas as operações das ferramentas transitivas. Os overrides ultrapassam os ranges dos pais e precisam ser reavaliados em atualizações futuras. Permanece o aviso conhecido de budget de profile.component.scss. Ausência de alertas npm não garante ausência de falhas.

Este documento não registra merge, deploy ou validação de produção. Nenhuma alteração funcional, de API ou banco foi necessária para registrar os resultados.
