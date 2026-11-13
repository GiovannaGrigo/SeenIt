# B05-B1.5 — Validação funcional local

Registro consolidado em 13/11/2026 (America/Sao_Paulo), na etapa B05-B1.6. Branch: `b05-b1-angular-security`.

**Conclusão: APROVADO LOCALMENTE**, com base nos testes automatizados registrados no B05-B1.4 e na aprovação dos dez cenários manuais comunicada pelo operador. A atualização permanece exclusivamente no Angular 21.

## 1. Origem, data e ambiente da validação manual

O operador informou que executou os dez cenários abaixo em navegador e aprovou todos. **O Codex não executou esses testes manuais**; este relatório registra o resultado informado pelo operador.

Ambiente informado: aplicação SeenIt em ambiente local, acessada por navegador. Navegador e versão, sistema operacional do operador, URL/porta local, dispositivos, dimensões de viewport e data/hora exatas da execução manual não foram informados. A aprovação foi comunicada e registrada nesta etapa em 13/11/2026; isso não estabelece um horário ou uma data de execução manual mais específicos.

O ambiente automatizado documentado separadamente no B05-B1.4 é Windows, Node 24.16.0 e npm 11.13.0. Essas informações não devem ser tomadas como confirmação do navegador ou dispositivo utilizado pelo operador.

## 2. Cenários manuais informados pelo operador

| Nº | Cenário | Resultado informado |
| --- | --- | --- |
| 1 | Autenticação e redirecionamento | APROVADO |
| 2 | Cadastro e validações | APROVADO |
| 3 | Exploração e detalhes das séries | APROVADO |
| 4 | Biblioteca e alteração de status | APROVADO |
| 5 | Episódios, sentimentos e personagem favorito | APROVADO |
| 6 | Perfil e estatísticas | APROVADO |
| 7 | Componentes PrimeNG | APROVADO |
| 8 | Temas claro e escuro | APROVADO |
| 9 | Responsividade | APROVADO |
| 10 | Console do navegador sem novos erros | APROVADO |

Esses resultados encerram a pendência de validação funcional manual local apontada no relatório de aplicação. Não foram fornecidos logs de navegador, capturas de tela ou roteiro detalhado por ação; não se atribui ao operador uma cobertura além dos cenários comunicados.

## 3. Verificações automatizadas anteriores

Conforme [B05-B14-ANGULAR-APPLICATION.md](B05-B14-ANGULAR-APPLICATION.md), o Codex executou após aplicar a candidata C:

| Verificação | Resultado registrado |
| --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund | Código 0; sem recalcular o lockfile e sem scripts de instalação |
| npm ls --all | Código 0; sem conflitos obrigatórios de peers |
| npm audit --json --ignore-scripts | Consulta válida, código 1 por 16 pacotes moderados |
| npm run build | Código 0 na execução com permissão de leitura necessária |
| TypeScript: tsc --noEmit -p tsconfig.app.json | Código 0 |
| npm run check:rate-limiting | Código 0 |
| node checks/library.cjs | Código 0 |
| node checks/watch-time.cjs | Código 0 |
| Assets locais referenciados no HTML/CSS e cópia de public | Conferência concluída sem arquivos ausentes |

Os checks usam código/métodos reais com doubles locais e não são uma suíte end-to-end em navegador. O script `ng test` existe, mas não há alvo test/runner em angular.json; o spec de EpisodioModal não foi executado. Esses limites continuam válidos, mesmo após a aprovação manual do operador.

Nesta consolidação não foram repetidos build, instalação ou auditoria, pois os dois arquivos aplicados permanecem com os hashes aprovados e não houve nova mudança de dependências ou código. A conferência pré-commit verificou também as versões instaladas. Os números de segurança abaixo referem-se à auditoria do B05-B1.4, não a uma nova consulta nesta etapa.

## 4. Versões aprovadas e instaladas

| Pacote/grupo | Versão |
| --- | --- |
| @angular/animations, common, compiler, core, forms, platform-browser e router | 21.2.26 |
| @angular/compiler-cli | 21.2.26 |
| @angular/cli e @angular/build | 21.2.30 |
| @angular/cdk | 21.2.14 |
| primeng | 21.1.10 |
| @primeuix/themes | 3.0.1 |
| @primeuix/styled, raiz | 1.0.0 |
| @primeuix/styles | 2.0.3 |
| @primeuix/utils, raiz | 0.8.2 |
| @primeuix/motion | 0.0.10 |
| primeicons | 8.0.2 |
| rxjs | 7.8.2 |
| tslib | 2.8.1 |
| typescript | 5.9.3 |

Preservadas as cópias aninhadas de styled 0.7.4 e utils 0.6.4/0.7.2, conforme o inventário do B05-B131. PrimeNG, PrimeUIX e tslib continuam nas versões originais aprovadas. Nenhum pacote oficial @angular/* verificado no lockfile e na instalação está na major 22 ou superior.

Hashes SHA256 do par conferido antes dos commits:

```text
frontend/package.json
1fe6d9183849cf494b6236a21842bd09dfe7720dea483eba54267b57444fbe38

frontend/package-lock.json
32a9d1bf185e2868c055d449bc6ca1b18a23b361ac6b1fbbd8bb06c5a6ae9e9e
```

O Git pode normalizar fins de linha ao gravar os arquivos nos commits. Os hashes acima identificam os arquivos locais aprovados; normalização de fins de linha não é uma nova resolução de dependências.

Na revisão do staged da documentação, git diff --cached --check identificou uma linha em branco excedente no fim do relatório B05-A. Foi removida somente essa linha, sem alteração do conteúdo do diagnóstico. O hash do arquivo B05-A muda por esse ajuste de whitespace; os demais relatórios anteriores foram mantidos sem edição.

## 5. Segurança residual e aviso de build

| Severidade de pacotes npm sinalizados | B05-A | B05-B1.4 |
| --- | ---: | ---: |
| Crítica | 3 | 0 |
| Alta | 9 | 0 |
| Moderada | 20 | 16 |
| Total | 32 | 16 |

São pacotes sinalizados, incluindo propagação aos pais, e não falhas independentes. Os 16 moderados permanecem na cadeia dev do CLI: @angular/cli, @npmcli/package-json, @npmcli/run-script, @sigstore/sign, @sigstore/tuf, @tufjs/models, cacache, glob, ignore-walk, make-fetch-happen, minimatch, npm-packlist, npm-registry-fetch, pacote, sigstore e tuf-js. As causas diretas são glob/minimatch, descritas nos relatórios anteriores. Nenhum pacote prod foi sinalizado na auditoria completa registrada; isso não constitui certificação de segurança.

Não foi seguida a recomendação automática de migrar o CLI para Angular 22. Permanecem também os riscos preexistentes do runtime PrimeUIX antigo, documentados no B05-B131, fora da contagem npm. A aprovação funcional manual não elimina esses riscos.

O build gerou bundle inicial de 542,61 kB, abaixo dos budgets de aviso de 1 MB e erro de 2 MB. Emitiu aviso não bloqueante em `profile.component.scss`: 8,39 kB, 390 bytes acima do aviso de 8 kB e abaixo do limite de erro de 16 kB. Nenhum budget, SCSS ou layout foi alterado para ocultar o aviso. O aviso de depreciação de @angular/animations também permanece registrado, sem migração de funcionalidade.

## 6. Limitações e conclusão local

Os cenários manuais representam o relato do operador, sem reprodução independente pelo Codex. Não comprovam cobertura de todos os navegadores, tamanhos de tela, estados de dados, acessibilidade, carga ou casos de segurança. Testes automatizados locais e aprovação manual não validam por si só uma instalação Linux/CI ou um artefato publicado. Nenhum deploy foi executado nesta consolidação.

Com esses limites e os riscos residuais registrados, a atualização é **aprovada localmente para consolidação nos dois commits solicitados**. Não há alteração funcional de TypeScript/HTML/CSS nesta etapa. maintenance-phase1 continua isolada e preservada, inclusive suas alterações locais preexistentes. Este registro não autoriza nem executa push, merge com main ou publicação.
