# SeenIt — B06-E · Encerramento do monitoramento de segurança

Data de referência: 24/11/2026, America/Sao_Paulo. As verificações desta etapa são distinguidas dos resultados históricos, dos relatos do operador e das propostas futuras.

## 1. Objetivo

Consolidar a auditoria local de segurança e confirmar que a versão aprovada foi integrada à `main`.

## 2. Histórico B06-E1 a E6

| Etapa | Resultado |
|---|---|
| E1 | Diagnóstico de dependências, fontes de avisos, Docker, codecs e lacunas operacionais. |
| E2 | Implementação do coletor local e do runbook, com testes e duas execuções reais. |
| E3 | Revisão técnica, tratamento reforçado de JSON NuGet e de timeouts, Markdown ampliado e simulação mensal. |
| E4 | Dois commits, publicação da branch e abertura do PR #10 mediante autorizações distintas. |
| E5 | Revisão pré-merge. PR apto para integração manual, com checks Vercel bem-sucedidos. |
| E6 | Confirmação da integração no GitHub, atualização local por fast-forward e consolidação documental. |

## 3. Estado inicial do Git

A worktree principal estava limpa, na branch `security/b06-e4-security-monitoring`, HEAD `2be05122abf1b4c4106c25e94f324ef659cfa20c`. `main` e `origin/main` locais apontavam para `9f34bcab1b1e9a693f6c1dd7edb854752dddc90c`. Foram registrados os 253 hashes da principal, as branches e as duas worktrees. A manutenção estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seis arquivos rastreados modificados, nove não rastreados e 161 hashes coincidentes com o snapshot histórico.

## 4. PR #10 e commits

O GitHub retornou **MERGED**, de `security/b06-e4-security-monitoring` para `main`, às 16:50:11 de 24/11/2026 no horário de São Paulo. O PR lista os dois commits aprovados e exatamente cinco arquivos adicionados. [PR #10](https://github.com/GiovannaGrigo/SeenIt/pull/10)

| Commit | SHA |
|---|---|
| Implementação | `654032522170bec9227bcaa9ae548c545ff60e6e` |
| Documentação | `2be05122abf1b4c4106c25e94f324ef659cfa20c` |

## 5. SHA completo da integração

`ee78a4e666d4e4ac4bc5e7ef7ca031c265f3032b` era o SHA indicado pelo PR e pela `main` remota. Os pais são a `main` anterior e a ponta aprovada. O objeto de árvore `1ad9aa73d166725912aaac4340c922261be4727f` coincide integralmente com o da ponta aprovada. Após fetch, comprovação de ancestralidade e comparação das árvores, a referência local `main` foi avançada por fast-forward e selecionada. `HEAD`, `main` e `origin/main` passaram a apontar para o SHA integrado.

## 6. Arquivos adicionados

- `tools/security/Invoke-SecurityAudit.ps1`
- `docs/SECURITY-MONITORING-RUNBOOK.md`
- `docs/B06-E1-SECURITY-MONITORING-DIAGNOSIS.md`
- `docs/B06-E2-SECURITY-AUDIT-IMPLEMENTATION.md`
- `docs/B06-E3-SECURITY-AUDIT-REVIEW.md`

O diff integrado contém somente essas adições. API, frontend, dependências, Dockerfile e fluxo de publicação preservados. O coletor fica fora do contexto Docker e do build normal do frontend, conforme revisão E5.

## 7. Arquitetura do coletor

Um script PowerShell de execução manual identifica o checkout por branch e SHA, registra a condição da árvore e coleta cada fonte em escopo isolado. Os processos externos têm timeout. A limpeza é restrita ao temporário exclusivo criado pela rodada, incluindo caminhos longos. Cada execução emite `audit.json` e `audit.md` em uma pasta própria fora do checkout. No Scout, o relatório identifica a imagem local e a arquitetura.

## 8. Contrato de execução

`OutputDirectory` é obrigatório. `Scope` seleciona `All`, `Npm`, `NuGet` ou `Mcr`. `IncludeScout` habilita a coleta opcional com `ImageId` completo. `Offline` registra as fontes que exigem rede como puladas. `TimeoutSeconds` permite ajustar o prazo por processo. [Runbook](SECURITY-MONITORING-RUNBOOK.md)

## 9. Códigos de saída

| Código | Significado |
|---|---|
| 0 | Auditoria completa sem achados. |
| 1 | Auditoria completa com achados. |
| 2 | Cobertura parcial, inclusive no modo offline. |
| 3 | Falha operacional. |

Falhas de feed, JSON inválido ou malformado e timeout impedem um resultado completo e limpo. Achados válidos continuam registrados quando outra fonte falha. Fontes fora do escopo ficam identificadas como puladas.

## 10. Auditorias npm/NuGet

O npm verifica o lockfile completo e o recorte sem dependências de desenvolvimento. O NuGet restaura em ambiente isolado e consulta dependências diretas e transitivas de API, AvatarChecks, CoreChecks e RegisterIntegrationChecks para `net10.0/linux-x64`. Os grafos consultados nas etapas históricas retornaram zero achados.

## 11. Monitoramento MCR

Consulta em leitura os índices e manifestos Linux/amd64 de SDK e ASP.NET, seus digests e as versões declaradas. As consultas históricas confirmaram digests atuais diferentes dos pins e versões declaradas de SDK 10.0.404 e runtime 10.0.15. A comparação controlada de pacotes ainda está pendente. Diferença de digest, por si só, não comprova correção de CVE.

## 12. Docker Scout opcional

O Scout roda sob solicitação, sobre um ID local explícito. Os scans da imagem histórica B06-D3 registraram os mesmos 17 IDs, com 13 Medium e quatro Low. Essa contagem representa correspondências do feed. A aplicabilidade depende da presença do componente, do fluxo e dos avisos do fornecedor.

## 13. Testes sintéticos

E2 aprovou 13 cenários no Windows PowerShell 5.1. E3 aprovou 18 cenários, incluindo JSON NuGet estruturalmente malformado, combinação de achados com cobertura parcial e timeout após emissão de dados. Também confirmou caminhos longos e retenção de evidência em diretórios novos. A etapa E6 não repetiu auditorias ou testes pesados.

## 14. Execuções reais

| Evidência histórica | Resultado |
|---|---|
| E2, duas rodadas em Windows PowerShell 5.1 | npm/NuGet sem achados, MCR válido e Scout opcional com 17 registros históricos. |
| E3, mensal padrão em PowerShell 7.7 | Código 0 em 29,63 s, com 25.877 B de relatórios. |
| E3, MCR + Scout em PowerShell 7.7 | Código 1 em 22,92 s, com 15.109 B de relatórios e os mesmos 17 IDs. |

A limpeza foi confirmada nas execuções finais. O pico de armazenamento temporário ficou sem medição. [Implementação E2](B06-E2-SECURITY-AUDIT-IMPLEMENTATION.md), [Revisão E3](B06-E3-SECURITY-AUDIT-REVIEW.md)

## 15. Segurança do coletor

Consultas públicas, configuração e caches isolados e TLS padrão. O coletor não executa conteúdo de advisories, não atualiza pacotes e não instala ferramentas globalmente. Os diagnósticos brutos são omitidos dos relatórios. O restore pressupõe checkout confiável. O Scout pode utilizar o acesso Docker de leitura já configurado. Esta entrega não adiciona secrets, workflows, agendamentos ou correções automáticas.

## 16. Runbook e cadência

- **Semanal.** Revisar advisories relevantes e exploração conhecida.
- **Mensal.** Executar a auditoria sob demanda e guardar relatórios fora do checkout.
- **Após alerta crítico.** Confirmar exposição e correção, com experimento isolado quando necessário.
- **Após deploy relevante.** Conferir SHA, health, logs e smoke funcional.

A comparação com a fotografia anterior e a decisão de prioridade são manuais. [Runbook](SECURITY-MONITORING-RUNBOOK.md)

## 17. Evidências operacionais

O operador informou que a Vercel concluiu o deploy automático com status **Ready** e que o Render usa deploy manual. Relatou operação normal, resposta esperada de `/health` e funcionamento geral. A opção de dispensar novo deploy manual do backend foi intencional, pois o coletor e os documentos ficam fora da publicação da API. Esta etapa não testou esses serviços diretamente. O SHA do backend em execução ficou sem confirmação. O SHA integrado foi verificado no GitHub nesta etapa, sem atribuir essa confirmação ao operador.

## 18. Riscos residuais

- Os 17 registros do Scout incluem divergências de feed e backports. A versão histórica de `dpkg` já contém a correção da CVE-2026-2219 segundo a Canonical. O CNA rejeitou CVE-2026-95209. Esses casos exigem conciliação antes de priorizar mudanças.
- Libpng 1.6.58 foi observada no asset incorporado. A correção upstream de CVE-2026-46675 está em 1.6.59 e sua incorporação ao asset usado segue sem comprovação. O leitor progressivo examinado ficou fora da condição específica do advisory. JPEG, WebP, GIF/Wuffs e auxiliares exigem revisão de fonte e binário.
- Os warnings de Data Protection sobre persistência e proteção do key ring permanecem na revisão operacional. [Diagnóstico E1](B06-E1-SECURITY-MONITORING-DIAGNOSIS.md)

## 19. Limitações

A cobertura de codecs estáticos é parcial. A imagem Render em produção não foi inventariada diretamente, e a versão Node usada na Vercel ficou sem confirmação direta. Digests candidatos do MCR ainda precisam de comparação controlada de conteúdo. Zero achados nos grafos consultados e a contagem do Scout descrevem seus respectivos recortes e fontes.

## 20. Rollback

Se surgir defeito na execução manual, suspender o uso, preservar os relatórios e corrigir em branch isolada. A retirada do coletor e do runbook pode ser feita por procedimento Git autorizado. O coletor não altera o runtime da API. Nenhum rollback foi realizado nesta etapa.

## 21. Preservação do repositório

A árvore funcional e os cinco arquivos aprovados foram preservados. A branch de implementação e as demais branches e worktrees foram mantidas. `maintenance-phase1` preservou HEAD, status e os 161 hashes. Os inventários Docker de imagens, containers e volumes e os totais de cache coincidiram entre as leituras. A única entrada nova não rastreada na principal é este relatório. O E6 não criou commit, push, merge, deploy, workflow ou automação.

## 22. Próximos passos

No B06-E7, revisar este relatório e decidir seu versionamento mediante autorização. Manter a primeira rodada mensal sob demanda e registrar diferenças de cobertura, custo operacional e aplicabilidade antes de reconsiderar agendamento.

## 23. Conclusão

PR #10 integrado, árvore aprovada preservada e `main` local sincronizada. O coletor está disponível para uso manual conforme o runbook. A cobertura de produção e as pendências de codecs continuam como verificações separadas.

**B06-E PRONTO PARA ENCERRAMENTO**
