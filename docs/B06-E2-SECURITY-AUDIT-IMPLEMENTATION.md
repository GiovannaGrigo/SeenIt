# SeenIt — B06-E2 · Implementação da auditoria local de segurança

Referência de operação: 24/11/2026, America/Sao_Paulo.

## 1. Objetivo

Entregar um coletor local de leitura sob demanda e um runbook de operação. O B06-E1 permanece como referência para a revisão de 17 registros Scout, a proveniência dos codecs e a triagem de infraestrutura. Nenhuma integração ou alteração funcional foi feita nesta etapa.

## 2. Estado inicial do Git

Antes da primeira escrita, main, HEAD, origin/main e refs/heads/main no GitHub correspondiam a 9f34bcab1b1e9a693f6c1dd7edb854752dddc90c. A única entrada não rastreada era docs/B06-E1-SECURITY-MONITORING-DIAGNOSIS.md. Foram registrados os 248 SHA-256 de arquivos preexistentes rastreados e o SHA-256 do relatório B06-E1. O inventário incluiu 11 branches locais, as referências remotas e as duas worktrees.

A manutenção estava no HEAD fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290, com seis modificações rastreadas e nove arquivos não rastreados conhecidos. Os 161 SHA-256 coincidiam com o snapshot histórico. Docker retornou 29 referências de imagem no inventário, dois containers, quatro volumes e 276 entradas de cache. A comparação de cache foi agregada.

## 3. Contrato e parâmetros

| Parâmetro | Contrato |
|---|---|
| OutputDirectory | Obrigatório. Diretório pai local e fora das worktrees, com subdiretório exclusivo por execução. |
| Scope | All por padrão. Aceita Npm, NuGet e Mcr, inclusive combinados. |
| IncludeScout e ImageId | Scout por opção explícita, com ID sha256 local completo. Imagem ausente, ferramenta, autorização ou entitlement insuficientes resultam em cobertura parcial. |
| Offline | Executa só a identificação local e a geração dos relatórios. Toda fonte selecionada que depende de rede fica skipped. |
| TimeoutSeconds | 120 segundos por processo por padrão. Aceita de 1 a 900. |

O coletor exige Windows e PowerShell 5.1 ou posterior. A execução observada usou Windows PowerShell 5.1. O alias pwsh estava presente, mas não abriu no sandbox local. A execução no PowerShell 7 ficou sem teste direto.

## 4. Arquitetura do script

O script identifica o checkout e as worktrees via Git, valida saída e temporários, executa cada fonte isoladamente e normaliza os resultados em audit.json (schemaVersion 1) e audit.md. As ferramentas são invocadas diretamente, com captura separada de stdout/stderr e sem repassar diagnósticos brutos. O relatório registra ID, horários ISO 8601, branch, HEAD, status Git, sistema, versões observadas, recorte e feed por fonte. Um estado modified indica que a execução inclui a árvore local, inclusive arquivos não rastreados.

O restore e o npm audit escrevem apenas na área temporária exclusiva da execução. Os dois relatórios ficam na pasta exclusiva de saída. A limpeza usa caminho longo no Windows e verifica a referência exata do diretório que o coletor criou. scratchCleanup registra o resultado. Se a limpeza falhar, uma execução de outra forma completa passa a partial.

## 5. Fontes consultadas

| Fonte | Verificação |
|---|---|
| registry.npmjs.org | Advisory feed sobre os grafos completo e sem dev do lockfile frontend. |
| api.nuget.org | Restore de quatro projetos e auditoria de diretas e transitivas. |
| MCR | Índices das duas tags .NET e manifestos/configs Linux/amd64. |
| Serviço de advisories Docker Scout | Scan opcional sobre a imagem histórica B06-D3 identificada. |
| Git local | Identidade, worktrees, status e versão Git. O coletor não faz fetch. |

GHSA/MSRC, Canonical, libpng, demais codecs e a configuração GitHub permanecem na revisão manual. O estado GitHub foi confirmado no B06-E1, fora das fontes deste coletor.

## 6. Auditoria npm

O coletor copia package.json e package-lock.json para duas pastas isoladas, fixa o registry público, usa cache próprio e desativa scripts e a notificação de versão. Executa npm audit com JSON e package-lock-only nos dois recortes. Retém contagens por severidade, advisories, versões resolvidas no lock e sugestões de correção quando presentes. Código 1 nativo e JSON válido com achados são tratados como findings. Erro do registry, dados inconsistentes e JSON inválido impedem sucesso.

## 7. Auditoria NuGet

Foi confirmado o caminho backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj. Os quatro projetos são copiados mantendo suas referências relativas. O coletor usa .NET 10, NuGet.org e restore Linux-x64 com auditoria de transitivas. Verifica o target net10.0/linux-x64 em project.assets.json e executa dotnet package list com JSON, --no-restore, --vulnerable e --include-transitive. O relatório guarda códigos por projeto, lista resolvida e cobertura. Avisos de vulnerabilidade do restore que o package list não corrobore deixam o projeto parcial. Os volumes de restore observados nesta etapa correspondem a resolução nova.

## 8. Consulta MCR

curl solicita apenas os manifestos e seus config blobs por HTTPS. A consulta valida o header de digest do índice, a existência de um filho Linux/amd64, o digest do filho e a arquitetura e versões no config. A comparação é contra o pin escrito no Dockerfile. Ausência do filho esperado ou de metadados impede sucesso. Uma diferença de digest gera evidência para comparação posterior de pacotes.

## 9. Scout opcional

O coletor verifica versão do plugin, resposta do Engine e a identidade e arquitetura da imagem local. Invoca docker scout cves com local:// e SARIF usando cache isolado. Normaliza IDs, severidades e, quando disponíveis, pacote, versão e correção informada pelo feed. A identidade é da imagem local. A verificação de entitlement se limita à capacidade de completar essa operação. A conta e seus limites gerais não foram auditados. Uma versão nativa incorporada precisa de verificação separada.

## 10. Estados e códigos de saída

| Código | Estado global | Critério |
|---|---|---|
| 0 | success | Escopo selecionado concluído sem achados. |
| 1 | findings | Escopo selecionado concluído com achados. |
| 2 | partial | Cobertura incompleta, Offline, alguma fonte válida combinada com falha ou limpeza retida. hasFindings preserva achados. |
| 3 | failed | Nenhum resultado parcial ou válido entre as fontes selecionadas, ou falha operacional na preparação ou gravação. |

Estados de fonte: success, findings, partial, failed e skipped. Fonte não selecionada fica fora do cálculo. Fonte selecionada e pulada por Offline conta como cobertura incompleta.

## 11. Timeouts

As chamadas externas têm prazo por processo e nenhuma repetição implementada pelo coletor. O npm usa zero novas tentativas configuradas. O runner coleta as duas saídas em paralelo, encerra o processo que excedeu o tempo e registra a falha. A tentativa de encerrar descendentes usa a API do PowerShell 7 ou taskkill e fallback de encerramento do processo no Windows PowerShell. TLS foi mantido em todas as consultas.

## 12. Testes sintéticos

O harness temporário executou 13 cenários com relatórios analisados. O timeout usou um processo Node inofensivo que esperaria 15 s, com limite de 2 s por comando. Os demais resultados externos foram simulados com fixtures, sem introdução de dependências vulneráveis.

| Cenário | Saída verificada |
|---|---|
| npm sem achados | 0, duas fontes success. |
| Achado simulado em PrimeNG, código npm 1 | 1, versão do lock, GHSA e correção na saída. |
| E503, ferramenta ausente, JSON inválido | 3 em cada caso. O diagnóstico sintético com credencial não apareceu na saída. |
| Timeout | 3, dois timeouts registrados. Caso completo em aproximadamente 5 s. |
| Achado e JSON inválido em recortes distintos | 2, achado preservado. |
| Offline | 2, cinco recortes selecionados skipped e nenhuma consulta. |
| Scout sem solicitação | skipped, fora do resultado global. |
| Scout solicitado, imagem ausente | 2, scan parcial e resultados npm preservados. |
| Índice MCR sem AMD64 | 2, dois resultados parciais. |
| npm código 1 sem achados e código 7 inesperado | 3 nos dois cenários. |
| Restore parcial NuGet | 2, três projetos válidos e um erro simulado. Limpeza do temporário com arquivo de caminho longo. |

O teste reutilizou o mesmo diretório pai com arquivo sentinela e 13 saídas independentes. Confirmou os hashes dos manifestos, lock e Dockerfile e a rejeição de saída dentro do checkout. JSON e Markdown foram validados em todas as execuções.

## 13. Resultados das duas rodadas reais

As duas rodadas consultaram as fontes públicas em Windows NT 10.0.26300.0 e PowerShell 5.1.26100.9818. Os relatórios registraram main e o HEAD esperado, com árvore modified pelas adições documentais e do próprio coletor.

| Fonte | Rodada 1 | Rodada 2 |
|---|---|---|
| npm completo | success · 0 | success · 0 |
| npm sem dev | success · 0 | success · 0 |
| NuGet | success · 4/4 projetos, 0 achados | success · 4/4 projetos, 0 achados |
| MCR SDK | success · pin difere do índice e do filho AMD64 atuais | success · mesmos digests e versões da rodada 1 |
| MCR ASP.NET | success · pin difere do índice e do filho AMD64 atuais | success · mesmos digests e versões da rodada 1 |
| Scout | skipped · fora do escopo | findings · 17 ocorrências, 13 Medium e 4 Low |
| **Global** | **success · código 0** | **findings · código 1** |

Node 24.16.0, npm 11.13.0, SDK local .NET 10.0.301, curl 8.22.0 e Scout 1.28.0 foram reportados. Os códigos de restore e auditoria NuGet foram 0 para cada um dos quatro projetos nas duas rodadas. O target Linux-x64 resolveu 51 entradas de pacote da API, 20 de AvatarChecks, 20 de CoreChecks e 30 de RegisterIntegrationChecks. Os números são por grafo e podem incluir pacotes em mais de um projeto.

| Base | Pin SHA-256 | Índice SHA-256 | Manifesto AMD64 SHA-256 |
|---|---|---|---|
| SDK | ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625 | 8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d | cd28130a4da76d2dc11c360b6d9f18c673d6fe15252f91c073ba358a1318cfb0 |
| ASP.NET | 3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde | aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21 | f7b58ba6e8f2cbe11c3cb58e8a5f5f2a077834095e24f21e6eb969ab4f9eb4bf |

O SDK remoto declarou 10.0.404 e ambas as configurações declararam .NET e ASP.NET 10.0.15. Os digests e as versões coincidem com a consulta registrada no B06-E1. Não houve pull para comparar os pacotes Ubuntu dos manifestos candidatos.

O Scout aceitou a operação sobre o ID local sha256:87ff7408e413bddbd5238ad11ed22b6462be04e83100e7298e410c68c5d46a6a, Linux/amd64. Os **17 IDs** coincidiram com a evidência histórica D3 e cada ocorrência incluiu pacote e versão. O Scout retornou código 0 e emitiu diagnóstico em stderr, marcado no relatório. Não houve erro de fonte nas duas rodadas. A ocorrência repetida de dpkg e o ID rejeitado permanecem sujeitos à reconciliação com fontes oficiais descrita no B06-E1.

## 14. Duração e armazenamento

| Rodada | Início ISO 8601 | Duração medida | audit.json | audit.md | Total |
|---|---|---:|---:|---:|---:|
| 1 | 2026-11-24T16:03:46.0519597-03:00 | 30,26 s | 63.878 B | 2.109 B | 65.987 B |
| 2 | 2026-11-24T16:04:18.1121744-03:00 | 50,66 s | 76.674 B | 4.270 B | 80.944 B |

A duração abrange a chamada do script, incluindo a limpeza. O total mede os arquivos finais. O pico de armazenamento temporário das rodadas finais não foi medido. Ambas registraram scratchCleanup = removed. Os pares de evidências foram preservados em diretórios distintos sob o temporário local, com runIds `20261124T190346051Z-44f244cc6e3d4046b59cd7096bf3e5ab` e `20261124T190418112Z-41286a0ef6cd4ff1a3df6a10b8510af6`.

## 15. Limitações

A auditoria identifica matches de feed. Runtime .NET compartilhado, pacotes incorporados estaticamente, bundle Vercel, configuração GitHub e identidade do artefato Render exigem evidências próprias. Os digests MCR são identificadores de manifestos. A validação do conteúdo de uma nova base exigiria inventário e testes da candidata. O restore sem lock NuGet retrata o momento da resolução. No caso de Scout, arquivos de pacotes ou binários irmãos ausentes podem gerar correspondência por pacote-fonte.

## 16. Efeitos laterais observados

Na rodada de calibração, os feeds selecionados concluíram, mas uma parte do temporário NuGet permaneceu após a limpeza devido a caminhos de até 293 caracteres. A avaliação mediu 1.680.254.577 bytes em 2.710 arquivos remanescentes. A implementação passou a remover o diretório exclusivo com suporte a caminhos longos e registrar o resultado no relatório. Foram removidos apenas dois temporários próprios identificados da calibração e do primeiro teste de timeout. As evidências de saída foram preservadas. A checagem do armazenamento de certificados ASP.NET Core de desenvolvimento e os inventários Git/Docker passaram antes e depois de cada rodada real.

## 17. Rollback

Parar de executar o coletor. Se a mudança estiver integrada futuramente, reverter os três arquivos do B06-E2 conforme o processo Git autorizado. A remoção de evidência externa pode seguir a retenção escolhida pelo operador. Nenhuma configuração de automação precisa de reversão.

## 18. Recomendação para B06-E3

Rodar um ciclo mensal com o runbook, registrar custo de restore e discrepâncias de Scout por digest e validar a triagem de um aviso real. A partir desse ciclo, decidir se há ganho em notificação ou agendamento autorizado. Reconciliar libpng, builds Render e Node Vercel como trilhas manuais de evidência.

## 19. Preservação do ambiente

Conferência final de main, HEAD, origin/main e SHA remoto em `9f34bcab1b1e9a693f6c1dd7edb854752dddc90c`. Os 248 SHA-256 dos arquivos rastreados preexistentes e o hash do B06-E1 coincidiram. O status final compreende o B06-E1 preexistente e os três novos arquivos do B06-E2, todos não rastreados. API, JWT, migrations, frontend, lockfiles, Dockerfile, SkiaSharp 4.155.2 e target BuildHosts foram preservados.

As 11 branches locais, as referências remotas e as duas worktrees coincidiram com o estado inicial. Os 161 SHA-256 históricos da manutenção coincidiram individualmente e o HEAD, as seis modificações rastreadas e os nove arquivos não rastreados permaneceram iguais. Imagens e digests, containers, volumes e totais Docker, inclusive 276 entradas de cache, coincidiram entre as leituras. Nenhum workflow, secret, ajuste remoto, commit, push, PR, merge, deploy, build de imagem ou acesso a banco de produção ocorreu.
