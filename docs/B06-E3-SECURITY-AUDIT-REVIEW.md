# SeenIt — B06-E3 · Revisão da auditoria local de segurança

Referência de operação: 24/11/2026, America/Sao_Paulo.

## 1. Objetivo

Verificar se o coletor B06-E2 é confiável, operável e reproduzível em um ciclo mensal sob demanda. A revisão inclui contrato de saída, cobertura, efeitos colaterais e capacidade de triagem dos relatórios.

## 2. Estado inicial do Git

`main`, `HEAD`, `origin/main` e a referência remota consultada correspondiam a `9f34bcab1b1e9a693f6c1dd7edb854752dddc90c`. O status expandido mostrou os quatro arquivos B06-E1/B06-E2 esperados e nenhuma alteração rastreada. Foram registrados 248 hashes rastreados, quatro hashes não rastreados, 11 branches locais, referências remotas e duas worktrees. `maintenance-phase1` estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com os seis arquivos rastreados modificados, nove não rastreados e os 161 hashes históricos esperados.

| Arquivo preexistente | SHA-256 inicial |
|---|---|
| B06-E1-SECURITY-MONITORING-DIAGNOSIS.md | `50048c72e7fe502f126f57bde2f536ad8474ca3efc7fb5755b85c824e1756c88` |
| B06-E2-SECURITY-AUDIT-IMPLEMENTATION.md | `c100954a373051979c7c70fd1dd68293476383c52214612a08e4d5397aeaceef` |
| SECURITY-MONITORING-RUNBOOK.md | `2a516c6fdb41d10aae2829637de23ccc412cc1a36237059011ef8ef74b31a1e` |
| Invoke-SecurityAudit.ps1 | `fac99f98291d2e22777a2253eaa33ab98c7dbd3ded120a6d55d6205b15e577e0` |

O inventário Docker por leitura mostrou 29 imagens, dois containers, quatro volumes e 276 entradas de cache. O cache foi verificado de forma agregada.

## 3. Revisão dos requisitos

| Requisito | Avaliação |
|---|---|
| Operação mensal e opção offline | Um comando padrão. Escopos explícitos, modo offline e Scout opcional. |
| Informação de auditoria | SHA, branch, estado local, versões, fontes e cobertura no JSON. Relatórios B06-E2 foram relidos nesta etapa e os pontos de informação faltante foram corrigidos. |
| Separação de achado, falha e cobertura | Contrato de quatro códigos mantido. Uma falha concreta de validação do schema NuGet foi reproduzida e corrigida. |
| Preservação | Restore e npm isolados, saídas únicas, limpeza restrita e conferência dos hashes do checkout. |
| Triagem automatizada | O coletor registra fatos de feed. Comparação, urgência, aplicabilidade e confirmação de correções permanecem decisões do operador. |
| Limites observáveis | Planos de contas, validade de dados de produção e presença de todos os nativos estão fora da coleta. Os relatórios devem ser interpretados por fonte. |

## 4. Análise do script

Revisados parâmetros, vínculo com a raiz do script, resolução Git sem fetch, processos externos com argumentos separados por quoting Windows, JSON, UTF-8 dos relatórios e diretórios de execução exclusivos. O parser Windows PowerShell 5.1 usa JavaScriptSerializer para o lock npm, cujo objeto tem uma chave vazia válida. PowerShell 7 usa ConvertFrom-Json com AsHashtable nesse caso. A suíte de 5.1 usou saída com espaço, Unicode, ampersand e colchetes. O ciclo real usou a mesma classe de caminho no PowerShell 7. Os relatórios identificam estado modified quando há adições não rastreadas.

## 5. Segurança do coletor

Os processos recebem argumentos fixos ou validados. A coleta interpreta objetos JSON e preserva apenas os campos necessários, sem interpolá-los como comandos. A leitura do MCR exige HTTPS e mantém validação TLS. O npm usa `--ignore-scripts`, registry público e configuração isolada. O restore avalia projetos do checkout e exige sua origem confiável. O coletor evita herdar opções abrangentes de Git, npm e .NET, usa área de cache própria e omite diagnósticos brutos. Scout pode usar o acesso Docker já configurado localmente para leitura e consulta. A operação não precisa de acesso GitHub, Render ou Vercel com escrita.

A saída e o temporário passam por validação de caminho local, worktrees, separadores, links/junctions e formatos ambíguos. A rotina de limpeza verifica o caminho exato criado para aquela execução antes da exclusão e usa suporte de Windows a caminhos longos. Em caso de retenção, o relatório traz `scratchCleanup` e deixa de ser completo. Não foram encontrados usos de Invoke-Expression, comandos de instalação global, atualização, push, build, pull ou deploy.

## 6. Tratamento de erros e timeouts

stdout e stderr são lidos de forma assíncrona, com prazo por chamada. O coletor descarta a saída de um processo que excede o prazo, mesmo que já tenha escrito um JSON completo. A tentativa de encerramento da árvore usa a API nativa no PowerShell 7 e taskkill com prazo de dois segundos em 5.1, seguido de fallback para o processo pai. A checagem não garante encerramento de um descendente já desacoplado do processo observado. Os comandos efetivamente auditados concluíram ou usaram o caminho testado de timeout.

## 7. Códigos de saída

| Código | Significado |
|---|---|
| 0 | Todas as fontes selecionadas concluíram sem achados. |
| 1 | Todas as fontes selecionadas concluíram e houve achados. |
| 2 | Cobertura parcial. Achados válidos podem coexistir com essa condição. |
| 3 | Falha operacional ou nenhuma fonte selecionada retornou evidência válida ou parcial. |

Fontes não selecionadas são skipped e ficam fora da decisão. Offline pula as fontes que precisariam de rede e retorna 2. O teste combinou findings com partial, success com failed e timeout após emissão de dados. Nenhum desses casos produziu 0 ou 1.

## 8. Cobertura das fontes

- **npm.** Completo e sem dev no lock frontend, sem instalar pacotes. Código 1 passa como achado somente com JSON válido e achados coerentes. Falha de rede e schema inválido são erros separados.
- **NuGet.** Quatro projetos, restore novo isolado, target net10.0/linux-x64 e leitura de diretas/transitivas. Saída JSON valida a forma de frameworks, grupos e pacotes com advisories quando presentes. Warnings de vulnerabilidade do restore são considerados tanto em stdout quanto em stderr para a reconciliação.
- **MCR.** Requisições HTTPS aos índices, manifestos e configs. A leitura compara pin, índice e manifesto Linux/amd64 sem pull. Ausência de AMD64 ficou partial e erro de rede ficou failed.
- **Scout.** Somente com opt-in e ID integral da imagem local. Usa local://, SARIF e cache isolado. Valida resultados estruturados e registra ocorrências com pacote/versão quando disponíveis. A aceitação da operação comprova apenas o acesso para aquela chamada. Coleta por pacote-fonte não prova presença do binário vulnerável nem cobre todos os codecs estáticos.

## 9. Qualidade do JSON e Markdown

As duas rodadas históricas B06-E2 foram relidas. Seus pares mostravam SHA, escopo, estado global, resumo por fonte e achados. A rodada Scout registrava no JSON o ID de imagem, mas omitia-o no Markdown, assim como os códigos por fonte. O resumo agora traz ambos quando disponíveis. O JSON permanece a evidência detalhada para códigos por projeto, versões, erros e comparação.

O operador consegue identificar conclusão, falhas de cobertura e achados de feed. Para reconhecer IDs novos, persistentes ou ausentes, deve comparar execuções equivalentes e depois confirmar pacote, versão, correção e alcance. A classificação é manual e a ausência de alerta no scanner não encerra automaticamente uma pendência.

## 10. Testes sintéticos

Foram reexecutados os 13 cenários do B06-E2 no Windows PowerShell 5.1. Estão cobertos zero achados, achado simulado, E503, ferramenta ausente, JSON inválido, timeout, mistura de estados, offline, Scout fora do escopo ou com imagem inexistente, índice sem AMD64, códigos npm inconsistentes e restore parcial. Também foram checados ambos os relatórios, saídas únicas no mesmo pai, preservação de sentinela, rejeição de saída no checkout, hashes dos manifestos e limpeza de caminhos longos.

Cinco casos adicionais passaram: findings com MCR parcial (2), MCR indisponível com npm/NuGet válidos (2), JSON NuGet com frameworks como texto (3), vulnerabilidade transitiva NuGet em JSON estruturado (1) e timeout após JSON já emitido (2). O caso de frameworks como texto foi executado antes do ajuste e reproduziu o defeito com código 0. As fixtures e seus relatórios ficaram em área temporária externa. As dependências do projeto não foram alteradas.

## 11. Simulação mensal

Em PowerShell 7.7.0, foi executado o comando padrão do runbook em diretório externo. Em seguida foi executado o recorte MCR com Scout opcional na imagem histórica `sha256:87ff7408e413bddbd5238ad11ed22b6462be04e83100e7298e410c68c5d46a6a` (Linux/amd64). Os pares JSON/Markdown concordaram com o código de saída, SHA e estado. Ambos registraram `scratchCleanup = removed`.

| Fonte | Mensal padrão | Complemento MCR + Scout |
|---|---|---|
| npm completo e sem dev | success, zero achados nos dois recortes | skipped, fora do escopo |
| NuGet | success, quatro de quatro projetos, zero achados | skipped, fora do escopo |
| MCR SDK e ASP.NET | success, digests e versões coincidem com B06-E1 | success, mesmos resultados do padrão |
| Scout | skipped, fora do escopo | findings, 17 ocorrências (13 Medium, 4 Low) |
| Global | success, código 0 | findings, código 1 |

Node 24.16.0, npm 11.13.0, SDK local .NET 10.0.301, curl 8.22.0 e Scout 1.28.0. Os novos restores Linux-x64 resolveram respectivamente 51, 20, 20 e 30 pacotes para API, AvatarChecks, CoreChecks e RegisterIntegrationChecks. Os manifestos MCR atuais seguem com SDK 10.0.404 e runtime .NET/ASP.NET 10.0.15, e seus digests diferem dos pins locais. Mudança de digest exige comparação de pacotes e advisories.

A comparação executada sobre a imagem histórica teve 17 IDs presentes no baseline B06-D3/B06-E1 e no resultado desta etapa, sem IDs adicionados ou ausentes. Isso confirma a repetição das entradas do feed para esse artefato, incluindo as divergências do registro dpkg e do ID rejeitado. Nenhum achado novo evidenciado nessa comparação exigiu escalonamento. Verificações manuais pendentes: libpng e demais codecs vinculados, correções/alcance das CVEs residuais, conteúdo dos candidatos MCR, relatório de Data Protection, Node remoto e identidade do deploy Render.

## 12. Tempo e armazenamento

| Execução | Início ISO 8601 | Tempo | audit.json | audit.md | Total |
|---|---|---:|---:|---:|---:|
| Mensal padrão | 2026-11-24T16:27:00.2510712-03:00 | 29,63 s | 23.734 B | 2.143 B | 25.877 B |
| MCR + Scout | 2026-11-24T16:27:31.5734602-03:00 | 22,92 s | 10.688 B | 4.421 B | 15.109 B |

Tempos abrangem chamada, consultas, escrita e limpeza. O espaço é a soma dos dois relatórios por execução. Evidências temporárias externas identificadas por `20261124T192700251Z-a766c1aef2534cac921fc1d2ee42c6d3` e `20261124T192731573Z-0a66829e42094d58b2d29a1e438c22d9`. A diferença de tamanho com B06-E2 também reflete a formatação JSON das versões de PowerShell.

## 13. Problemas encontrados

1. Um objeto NuGet com campos de topo válidos e frameworks do tipo incorreto passava por resultado vazio. O falso limpo foi reproduzido em fixture de um grafo Linux-x64.
2. O Markdown do B06-E2 omitira identificação da imagem e códigos por fonte, dados necessários para triagem e comparação direta.
3. Na revisão de bordas, avisos NuGet do restore em stderr escapavam à reconciliação. A chamada auxiliar taskkill tinha duração sem limite próprio. A listagem Git separada por linhas dependia da representação de caminhos e nomes ambíguos do Windows mereciam rejeição explícita.
4. O runbook indicava quais dados comparar sem explicar o procedimento nem os pré-requisitos para interpretar diferenças entre duas fotografias.

## 14. Ajustes realizados

No coletor, foram reforçadas a verificação de schema NuGet/SARIF, a leitura dos avisos de restore, a delimitação da listagem de worktrees e a validação de nomes ambíguos. O taskkill tem prazo próprio. O Markdown inclui ID/arquitetura da imagem e código por fonte. O runbook ganhou três passos de comparação manual. O relatório B06-E2 e o diagnóstico B06-E1 foram preservados como evidência histórica.

## 15. Simplificações avaliadas

Manter a composição de um script, dois formatos de evidência e um runbook evita duplicar mecanismos de coleta. Os limites e tratamento de falhas continuam por fonte. Comparação automática com baseline, configuração externa, dashboard e notificações foram descartados para esta etapa. A complexidade adicional ficou limitada à validação de dados e ao controle de execução.

## 16. Limitações remanescentes

Sem atestação do artefato Render ou do bundle Vercel. A análise de libpng 1.6.58 incorporada, CVE-2026-46675, Data Protection, Node remoto e políticas GitHub depende de revisão manual. O restore NuGet sem lock é uma fotografia temporal e pode usar espaço temporário relevante. O pico desta execução não foi medido. Códigos e schemas podem mudar com versões futuras das CLIs. A prioridade operacional depende de alcance e exposição, além do feed.

## 17. Avaliação do runbook

O operador pode executar a coleta a partir da raiz, localizar os dois relatórios e interpretar seus códigos sem requisitos ocultos. Os exemplos usam parâmetros reais para o padrão, recortes, opção Scout e offline. As instruções também cobrem retenção, evidência incompleta, prioridade e escalonamento. A comparação manual esclarece como registrar diferenças com a fotografia do B06-E1.

## 18. Decisão sobre automação

Manter execução local sob demanda. A simulação permite uma revisão mensal com esforço medido. O retorno de um agendamento ainda deve ser avaliado com frequência de uso, qualidade da triagem e necessidade real de notificação.

## 19. Recomendação de versionamento

Para B06-E4, revisar e versionar os cinco arquivos do conjunto B06-E mediante autorização, mantendo código funcional e configurações fora do diff. Após integração autorizada, usar o runbook no ciclo mensal e comparar a evidência com esta fotografia antes de decidir qualquer ampliação.

## 20. Preservação do ambiente

O SHA de `main`, `HEAD`, `origin/main` e o SHA remoto permaneceram em `9f34bcab1b1e9a693f6c1dd7edb854752dddc90c`. Todos os 248 hashes rastreados coincidiram. Dos arquivos B06-E1/B06-E2 preexistentes, apenas coletor e runbook tiveram os ajustes desta revisão. B06-E1 e relatório B06-E2 mantiveram seus hashes. Este relatório foi o único arquivo novo.

As 11 branches locais, referências remotas, duas worktrees e os 161 SHA-256 da manutenção coincidiram. O HEAD e o status expandido de `maintenance-phase1` foram preservados. Os IDs e digests de Docker, os dois containers, os quatro volumes e os totais registrados de cache coincidiram antes e depois. A verificação de certificados ASP.NET Core de desenvolvimento não mostrou alterações entre as leituras. Nenhum commit, push, PR, merge, deploy, build, habilitação de automação ou mudança de configuração remota ocorreu.

**B06-E3 REVISÃO APROVADA**
