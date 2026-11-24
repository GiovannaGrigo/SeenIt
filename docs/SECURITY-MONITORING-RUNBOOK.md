# Runbook de monitoramento de segurança do SeenIt

## Objetivo

Detectar avisos em dependências npm e NuGet, mudanças nas imagens-base .NET e, por opção, achados em uma imagem Docker local. O coletor é executado manualmente. O operador confirma exposição e correções antes de propor mudanças.

## Pré-requisitos

- Windows com Git e Windows PowerShell 5.1 ou PowerShell 7. Use PowerShell 7 quando estiver disponível.
- Para npm, Node compatível com Angular e npm 7 ou posterior com o layout do instalador oficial para Windows. O coletor usa o npm CLI ao lado de npm.cmd, via node, em cópia temporária do manifesto e lockfile. A validação local usou Node 24.16.0 e npm 11.13.0.
- Para NuGet, SDK estável .NET 10 selecionado no ambiente isolado e acesso ao NuGet.org. O restore local usa cache temporário. A geração de certificado ASP.NET de primeira execução, a adição de ferramentas globais ao PATH, a telemetria e a atualização em segundo plano de manifestos de workloads são desabilitadas para esses processos.
- Para MCR, curl.exe 7.84 ou posterior com HTTPS e acesso ao registry público.
- Para Scout, Docker Engine acessível, plugin Scout com suporte a local:// e SARIF e acesso ao serviço de advisories. Forneça o ID local completo. O acesso ao scan depende das condições de autenticação e uso da conta Docker. O coletor não executa login.
- Use somente um checkout confiável. O restore avalia os projetos MSBuild copiados. Escolha um diretório normal no filesystem local, fora de todas as worktrees, para as evidências e para o temporário do sistema. Use os feeds públicos oficiais e a configuração TLS padrão das ferramentas.

## Como executar

Execute a partir da raiz. Substitua o diretório de saída conforme a política local de retenção. Quando disponível, use a sessão PowerShell 7. A saída padrão informa a pasta daquela execução.

~~~powershell
$audit = '.\tools\security\Invoke-SecurityAudit.ps1'
$output = Join-Path $env:LOCALAPPDATA 'SeenIt\SecurityAudit'

# Auditoria mensal padrão: npm, NuGet e MCR
& $audit -OutputDirectory $output

# Auditoria por escopo
& $audit -OutputDirectory $output -Scope Npm
& $audit -OutputDirectory $output -Scope NuGet -TimeoutSeconds 180
& $audit -OutputDirectory $output -Scope Mcr
& $audit -OutputDirectory $output -Scope Npm,Mcr

# Scout: substitua pelo ID completo de uma imagem local já identificada
$id = 'sha256:87ff7408e413bddbd5238ad11ed22b6462be04e83100e7298e410c68c5d46a6a'
& $audit -OutputDirectory $output -IncludeScout -ImageId $id

# Sem rede: gera metadados Git e registra fontes selecionadas como skipped
& $audit -OutputDirectory $output -Offline
~~~

O exemplo de ID corresponde ao artefato histórico local B06-D3. Confira a identidade antes de usá-lo em outra máquina. O escopo padrão é All. A opção IncludeScout adiciona o scan aos escopos escolhidos. Usar ImageId exige IncludeScout. Offline pula todas as fontes que dependem de serviços externos, inclusive Scout. O limite padrão é de 120 segundos **por comando**, com intervalo permitido de 1 a 900. O restore dos quatro projetos e as consultas por manifesto são operações separadas.

Cada execução grava audit.json e audit.md em um subdiretório novo sob OutputDirectory. Um diretório pai existente é aceito. Guarde os relatórios relevantes no local aprovado e aplique a retenção definida pelo operador. Reserve espaço para o restore NuGet local. Em uma execução com cache frio, o resíduo temporário medido antes da correção da limpeza totalizou cerca de 1,68 GB. O campo scratchCleanup informa a remoção ou retenção do temporário exclusivo da rodada. Se houver retenção, uma auditoria que de outro modo estaria completa passa a parcial. Saídas brutas e diagnósticos que podem conter caminhos ou credenciais ficam fora do relatório.

## Interpretação

| Código | Estado global | Significado |
|---|---|---|
| 0 | success | Todas as fontes selecionadas concluíram, com zero achados retornados. |
| 1 | findings | Cobertura selecionada concluída, com pelo menos um achado de feed. |
| 2 | partial | Há cobertura incompleta, offline ou erro em parte do escopo. Achados válidos continuam visíveis. |
| 3 | failed | Todas as fontes selecionadas falharam sem resultado parcial, ou houve falha operacional para preparar ou gravar os relatórios. |

Os estados de fonte são success, findings, partial, failed e skipped. A precedência global é falha operacional, cobertura incompleta, achados e sucesso. Fonte fora do escopo não entra no cálculo. Fonte selecionada e pulada em Offline conta como cobertura parcial. O campo hasFindings identifica achados preservados inclusive quando o estado global é partial.

O JSON usa schemaVersion 1. Os campos superiores incluem runId, startedAt, finishedAt, repository, git, host, scope, offline, timeoutSeconds, scoutRequested, imageId, state, exitCode, hasFindings, scratchCleanup e sources. Cada item de sources registra name, state, time, exitCode, tool, toolVersion, scope, feed, consulted, selected, findingCount, findings, errors, warnings, limitations e details. consulted indica tentativa de consulta. Uma contagem nula significa ausência de resultado interpretável. O exitCode da fonte pode ser nulo quando a etapa não chegou ao comando ou depende de vários comandos. NuGet registra os códigos por projeto em details.projects. Campos em details e findings dependem da fonte. O Markdown resume a mesma execução.

npm registra grafo completo e recorte sem desenvolvimento. O código 1 nativo só é interpretado como achado quando o JSON é válido e consistente. Um erro de feed, contagem incoerente ou JSON inválido impede conclusão limpa. MCR informa o pin, o índice da tag, o manifesto Linux/amd64 e versões declaradas no config blob. Um digest diferente pede comparação de pacotes e verificação de avisos. JSON de restore NuGet registra a resolução Linux-x64 efetivamente examinada. Diferenças de cache e ausência de lock NuGet tornam a resolução temporal. Scout converte SARIF para achados normalizados e usa a identidade da imagem local.

Quando Git indicar modified, a execução representa o checkout com as alterações locais, inclusive arquivos não rastreados. Conferir SHA, escopo, fontes, horário e se há resultados parciais antes de usar o relatório como evidência. Falha de consulta exige registrar o motivo e repetir a fonte quando houver condições.

### Como comparar rodadas

1. Abra o JSON atual e o anterior. Confira estado, escopo, SHA, versão da ferramenta e, para o Scout, ID e arquitetura da imagem. Compare somente as fontes que tenham resultado completo nas duas rodadas. Registre falhas e recortes pulados como lacunas de cobertura.
2. Para o Scout, use os 17 IDs da seção 13 do B06-E1 como fotografia histórica. Compare a lista de IDs em sources.findings e confirme pacote e versão quando um ID se repetir. Para npm e NuGet, compare identificador, pacote e versão dentro da mesma fonte e recorte.
3. Registre quais IDs apareceram, persistiram ou deixaram de aparecer. Um ID que voltou a aparecer pode refletir mudança no feed ou no scanner. Antes de registrar uma correção, confira o advisory oficial e a versão do artefato. Reavalie a prioridade se mudou a exposição, o alcance ou a evidência de exploração. A comparação e a decisão são manuais.

## Cadência e triagem

- **Semanal.** Revisar advisories Microsoft/.NET, Ubuntu, Angular, PrimeNG, Node, SkiaSharp e os fornecedores de codecs. Consultar alertas GitHub se forem habilitados em etapa futura. Registrar a data da última revisão.
- **Mensal.** Executar a auditoria padrão sob demanda e, quando houver artefato com ID conhecido, rodar Scout. Conferir diferenças de digests e cobertura incompleta. Atualizar as pendências do B06-E1.
- **Após aviso crítico ou exploração conhecida.** Confirmar presença, alcance e exposição no mesmo dia, quando possível. Tratar risco ativo e externamente alcançável como emergencial. Priorizar alto impacto e plausibilidade de alcance, incluindo cadeia de build. Programar correções de baixa exposição e manter monitoramento para registros rejeitados, fora de alcance ou sem fix.
- **Após deploy relevante.** O operador confirma SHA e identidade do artefato quando disponível, build, verify, logs e health HTTPS. Validar os fluxos afetados com smoke autorizado. Conferir certificados e configuração de proxies, além de Node e Ready na Vercel quando pertinente.

Registrar CVE ou GHSA canônico, pacote/revisão, contexto, digest/SHA e fonte. Verificar exploração conhecida, caminho de dado externo, runtime, privilégios, fix e risco de regressão. Compartilhar uma ficha entre feeds duplicados. Um alerta de feed confirma a correspondência informada pelo feed. A aplicabilidade exige evidência do componente entregue e do fluxo que o utiliza.

## Pendências e limites

- A lista histórica de 17 registros Scout tem um ID rejeitado e um registro de dpkg cuja versão inventariada já inclui o fix Canonical. Reconciliar por pacote e versão em cada novo scan. [B06-E1, seção 13](B06-E1-SECURITY-MONITORING-DIAGNOSIS.md#13-pendências-de-segurança-conhecidas)
- Libpng 1.6.58 está incorporada ao asset SkiaSharp estudado. A correção upstream para CVE-2026-46675 está em 1.6.59. O leitor progressivo examinado ficou fora da condição específica. Revisar pins e binários quando houver release ou mudança de leitor. Confirmar JPEG, WebP, GIF/Wuffs e auxiliares por fontes e evidência binária. [Advisory libpng](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j)
- Monitorar warnings de Data Protection, consumidores de chaves, persistência e proteção em repouso, além da fronteira TLS e validade dos certificados. O coletor cobre auditoria de dependências e metadados de imagens. Revisão das configurações GitHub, Render e Vercel e da versão Node remota cabe ao operador.
- Conferir diferenças de digest por plataforma e comparar o conteúdo da base quando houver candidato. Scanner local e manifestos remotos públicos deixam a imagem efetiva do Render fora do alcance da auditoria. O script também não verifica o bundle Vercel nem valida produção.

## Tratamento de um achado

Detecção → triagem → experimento isolado → validação → PR revisado → integração autorizada → deploy autorizado → evidências. Use os gates existentes de build, verify, testes do backend e frontend conforme a superfície. Registre o artefato anterior para rollback. Aprovação, atualizações, merge e deploy são decisões humanas.
