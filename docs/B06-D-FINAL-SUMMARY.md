# SeenIt — B06-D | Encerramento de BuildHost/Roslyn

## 1. Objetivo

Consolidar o diagnóstico, a exclusão seletiva dos BuildHosts da publicação da API, a integração e a validação operacional reportada em 24/11/2026.

## 2. Histórico B06-D1 a B06-D6

| Etapa | Resultado |
| --- | --- |
| [D1 — Diagnóstico](B06-D1-BUILDHOST-ROSLYN-DIAGNOSIS.md) | Identificou a origem dos 46 arquivos e examinou o carregamento no runtime. |
| [D2 — Experimento](B06-D2-BUILDHOST-PUBLISH-EXPERIMENT.md) | Comparou controle e candidata Linux/amd64 e aprovou a exclusão em publish. |
| [D3 — Implementação](B06-D3-BUILDHOST-PUBLISH-IMPLEMENTATION.md) | Aplicou o target aprovado ao projeto original e reproduziu os resultados. |
| D4 — PR | Submeteu a alteração do projeto e os três relatórios à main. |
| D5 — Integração e operação | Revisou o PR e recebeu do operador as evidências de deploy e validação no Render. |
| D6 — Encerramento | Conferiu Git, conteúdo integrado, preservação do ambiente e documentação. |

## 3. Estado inicial do Git

A worktree principal estava limpa em `security/b06-d4-buildhost-publish`, HEAD `e25cbd04677b128711e8c4682fb2e331bb521f5b`. As referências locais `main` e `origin/main` estavam em `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c` antes do fetch. O inventário inicial registrou 11 branches locais, duas worktrees e SHA-256 de 247 arquivos na principal. A manutenção tinha seis arquivos rastreados modificados e nove arquivos não rastreados, todos esperados.

## 4. PR #9 e commits

A consulta direta ao GitHub confirmou `MERGED`, origem `security/b06-d4-buildhost-publish` e destino `main`. A revisão pré-merge registrada no D5 apontou dois commits, quatro arquivos, ausência de conflitos, Vercel e Vercel Preview Comments aprovados e ausência de check obrigatório de API, cuja evidência veio do D3. [PR #9](https://github.com/GiovannaGrigo/SeenIt/pull/9)

| Commit | Conteúdo |
| --- | --- |
| `0d77b0a1be875bb15731971c52de703d7d459ea9` | Cinco linhas adicionadas em `backend/SeenIt.Api/SeenIt.Api.csproj`. |
| `e25cbd04677b128711e8c4682fb2e331bb521f5b` | Adição dos três relatórios D1, D2 e D3 listados acima. |

## 5. SHA da integração

`ccbe2c1ca948104aa1a69c270873de6ee77dd480`, integrado em 24/11/2026 às 14:44:50 (horário de Brasília). É um commit de merge de dois pais, o HEAD anterior de main e o commit documental. Ambos os commits originais estão em seu histórico. A árvore integrada coincide integralmente com o HEAD aprovado da branch de origem. O diff em relação ao primeiro pai contém exatamente os quatro arquivos declarados, sem alteração do target durante a integração. [Commit de integração](https://github.com/GiovannaGrigo/SeenIt/commit/ccbe2c1ca948104aa1a69c270873de6ee77dd480)

## 6. Alteração técnica

O csproj recebeu somente a adição de cinco linhas do target aprovado no D3. O BOM UTF-8 `EF BB BF` foi preservado. O Git registra LF no índice e CRLF na worktree Windows, com `core.autocrlf=true`. O SHA-256 na worktree é `6754d4950f047f10a609ef8502ba8c8397f2b7f899ea764c47516393c1bac45a`, igual ao aprovado.

## 7. Origem de BuildHost/Roslyn

D1 identificou o conteúdo de design-time em `contentFiles` do pacote Roslyn. Os 21 arquivos de `BuildHost-netcore` e 25 de `BuildHost-net472` estavam presentes no publish e no diretório da aplicação na imagem final histórica. O runtime não incluía o SDK completo, `csc`, `VBCSCompiler` ou a DLL principal do EF Core Design. A amostra de runtime examinada não observou uso dos hosts.

## 8. Grafo de dependências

`Microsoft.EntityFrameworkCore.Design 10.0.0 → Microsoft.CodeAnalysis.Workspaces.MSBuild 4.14.0 → contentFiles → BuildHost-netcore e BuildHost-net472`. A referência direta a EF Design permanece com `PrivateAssets=all` e `IncludeAssets` incluindo `contentfiles`. O grafo NuGet foi preservado, incluindo Npgsql EF Core PostgreSQL 10.0.0, SkiaSharp 4.155.2, NativeAssets.Linux.NoDependencies 4.155.2 e o pin privado de Pkcs 9.0.22.

## 9. Mecanismo MSBuild de exclusão

`ExcludeBuildHostsFromApiPublish` roda após `ComputeFilesToPublish` e remove de `ResolvedFileToPublish` apenas os itens cujo `RelativePath` começa com `BuildHost-netcore` ou `BuildHost-net472`, seguido de separador de diretório. O filtro atua no conjunto de publicação antes do COPY Docker. Os outputs Debug mantêm os hosts.

## 10. Integridade de arquivos e hashes

| Inventário local | Controle | Candidata D2 | Implementação D3 |
| --- | ---: | ---: | ---: |
| Publish em `/out/api` | 68 | 22 | 22 |
| Diretório `/app` da imagem final | 70 | 24 | 24 |

Os 22 arquivos retidos no publish e os 24 em `/app` foram preservados por SHA-256, inclusive entre D2 e D3. Os diretórios BuildHost estavam ausentes nos dois destinos. Permaneceram `SeenIt.Api.dll`, `SeenIt.Api.deps.json` (SHA-256 `03b7f83a87d0a5048af7e8b5a95df5749d9a757aa2895ae04b5038564d7b2da5`), EF Core, Npgsql, SkiaSharp, a CA e o marcador verify. A biblioteca `libSkiaSharp.so` manteve SHA-256 `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef`. Os relatórios D2 e D3 registram os inventários e hashes completos.

## 11. Redução lógica

Os 46 arquivos excluídos somavam **4.225.767 bytes lógicos**, aproximadamente 4,03 MiB, em ambos os comparativos. O publish é a saída selecionada pelo MSBuild. O inventário em `/app` é a visão do filesystem final da aplicação com a CA e o marcador adicionados pelo Docker.

## 12. Redução comprimida

| Etapa | Redução lógica | Redução de blobs OCI gzip | Percentual comprimido |
| --- | ---: | ---: | ---: |
| D2 | 4.225.767 bytes | 1.640.452 bytes | 1,44% |
| D3 | 4.225.767 bytes | 1.640.421 bytes | 1,44% |

A soma dos tars OCI descomprimidos caiu 4.281.344 bytes em ambos os comparativos. A diferença de 31 bytes na economia comprimida está nos blobs recriados pelo build, com tamanhos tar e conteúdo de `/app` iguais entre D2 e D3. Os valores gzip vêm dos descritores de plataforma conferidos nos exports locais e excluem manifests e atestações. Tráfego de rede e download efetivo no Render ficaram sem medição.

## 13. Testes locais B06-D2 e D3

Os relatórios registram build Docker Linux/amd64 sem cache, restore, publish Release, Docker verify, comparação de SkiaSharp e inspeção por `ldd` aprovados.

| Verificação | Resultado histórico |
| --- | --- |
| AvatarChecks | 16 grupos, incluindo JPEG, PNG, WebP e upload/substituição simulados, falhas de Storage e compensação. |
| CoreChecks | 72 casos HTTP de cadastro, além de tempo assistido e classificação de anime. |
| RegisterIntegrationChecks | 73 casos HTTP de cadastro, 16 usuários em SQLite em memória e ensaio de 20 logins sobrepostos. Cobriu login, perfil, catálogo, biblioteca, integridade, JWT, CORS, rate limiting, concorrência, cancelamento e headers falsificados. |
| EntryPointChecks | 174/174 asserções, 72 requisições em seis perfis de configuração de edge/porta, com autenticação e HTTPS. |
| ExtraAvatar e CodecExperiment | Três grupos de avatar e 37 registros de codec por cenário, incluindo WebP animado no ExtraAvatar e casos JPEG, PNG e WebP no experimento. |
| PostgreSQL | 17.11 descartável, com consultas e persistência EF Core/Npgsql, transações, rollback, concorrência, isolamento, retry e conflitos. |
| Runtime e proxy | Health HTTP 200, usuário app UID 1654, porta 10000 e encerramento sem OOM. O proxy local com TLS 1.3 apresentou health 200, perfil com JWT inválido 401 e preflight CORS 204, sem redirecionamentos. |

O D6 consultou essas evidências históricas, sem repetir as suítes.

## 14. EF Core e migrations

EF Core Design permaneceu no grafo e os 46 BuildHosts continuaram disponíveis nos outputs Debug testados. `dotnet-ef 10.0.0` descobriu `SeenIt.Api.Data.AppDbContext` com Npgsql, encontrou `InitialPostgres`, `AddSeriesFavorites` e `AddSeriesAnimeClassification` e aprovou a checagem de alterações pendentes no modelo. O SQL idempotente gerado offline em Linux coincidiu entre controle e implementação, SHA-256 `823349a79b009fb21b9503d841c5bfbb9b8355cabdb37876abb6f05e50df7a17`. O PostgreSQL descartável usou `EnsureCreated`. Nenhuma migration foi aplicada a bancos existentes. Consumidores externos que dependam de tooling de design-time no diretório publicado ficaram fora do inventário.

## 15. Docker Scout e NuGet audit

No D2, Docker Scout indexou 176 pacotes no controle e 160 na candidata, com 17 CVEs em ambos. No D3, indexou 160 pacotes e encontrou 0 Critical, 0 High, 13 Medium e 4 Low, total 17. A comparação histórica de SARIF registrou os mesmos alertas. D1 não comprovou CVE aplicável aos hosts investigados. NuGet audit com transitivos não reportou pacotes vulneráveis nas fontes consultadas para API, AvatarChecks, CoreChecks e RegisterIntegrationChecks. A cobertura de binários incorporados é parcial. A exclusão reduziu arquivos e dependências auxiliares distribuídos. As 17 CVEs do Scout continuaram presentes na medição local.

## 16. Evidências operacionais B06-D5

Segundo os logs fornecidos pelo operador em 24/11/2026, o Render preservou as bases por digest, concluiu restore de API, AvatarChecks e CoreChecks e publish Release linux-x64. A auditoria NuGet não reportou pacotes vulneráveis, SkiaSharp e `ldd` passaram, AvatarChecks aprovou 16 grupos e CoreChecks aprovou 72 casos HTTP. Os logs registraram CA 0444, usuário app UID 1654, ambiente Production, porta 10000, inicialização da aplicação e serviço Live.

O operador informou ter confirmado a correspondência do SHA implantado no Render. O valor completo do deploy não consta das evidências disponíveis. Inventário, hashes por binário e scan da imagem remota ficaram sem verificação direta.

## 17. Validação funcional em produção

**VALIDAÇÃO FUNCIONAL EM PRODUÇÃO INFORMADA PELO OPERADOR.** O operador reportou sucesso em health, cadastro, login, perfil, upload JPEG, PNG e WebP, substituição e remoção de avatar, catálogo, biblioteca, persistência e HTTPS sem loops. Também reportou ausência de novos erros nos logs durante essa rodada. A cobertura se limita aos fluxos informados.

## 18. Warnings conhecidos

No Render, os logs fornecidos registraram persistência de chaves de Data Protection sem garantia, ausência de encryptor XML e precedência de `URLS` sobre `HTTP_PORTS`, com binding efetivo em 10000. Os ensaios locais com PostgreSQL registraram, separadamente, ausência de `libgssapi_krb5.so.2`. A autenticação local por senha funcionou e GSS/Kerberos ficou fora do ensaio.

## 19. Riscos residuais e limitações

- Permanecem 17 CVEs na leitura local do Scout, com cobertura parcial de conteúdo incorporado e sem scan direto da imagem do Render.
- A imagem implantada ficou sem inventário de filesystem completo e sem SHA-256 remoto por binário. A correspondência do deploy foi informada pelo operador.
- A observação do carregamento de assemblies foi amostral. O tooling de migrations foi validado nos cenários examinados e consumidores externos de publish podem depender dos hosts.
- Permanecem os warnings de Data Protection e a pendência de libpng/CVE-2026-46675 da frente anterior. O monitoramento recorrente está por definir no B06-E.

## 20. Estratégia de rollback

Procedimento futuro, não executado.

1. Identificar o deploy estável anterior e registrar SHA e imagem, se disponíveis.
2. Preferir recuperar o artefato estável pelo Render mediante autorização.
3. Se uma reconstrução for necessária, remover exclusivamente `ExcludeBuildHostsFromApiPublish`, manter versões NuGet e digests das bases e reconstruir Linux/amd64.
4. Executar Docker verify e confirmar o retorno dos 46 BuildHosts. Em contexto equivalente, esperar 68 arquivos no publish e 70 em `/app`.
5. Reexecutar testes funcionais e realizar deploy somente mediante autorização.

## 21. Próximas frentes de hardening

Abrir B06-E para definir o monitoramento recorrente de dependências e imagens e acompanhar a pendência de libpng/CVE-2026-46675 e os avisos de Data Protection.

## 22. Preservação do repositório

O fast-forward deixou `main` e `origin/main` em `ccbe2c1ca948104aa1a69c270873de6ee77dd480`, com a worktree principal limpa antes deste relatório. A branch de implementação permaneceu no SHA documental original. As 11 branches locais e as duas worktrees foram mantidas. A manutenção permaneceu no HEAD `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com status e 161 SHA-256 iguais ao snapshot histórico `%TEMP%/seenit-b06-c6-before.json`. O inventário Docker foi consultado somente para leitura.

O diff integrado preserva Dockerfile, Program.cs, JWT, autenticação, rate limiting, HTTPS B06-A, frontend, migrations, CA e configurações de runtime. O Dockerfile continua com usuário app e porta 10000. Os digests conferidos no Dockerfile são

- `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625`
- `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde`

A conferência final reencontrou os IDs das 28 referências de imagem e dos dois contêineres. Na principal, os SHA-256 dos 247 arquivos rastreados coincidiram com o inventário inicial após normalizar para LF apenas os três relatórios históricos, que o checkout materializou com CRLF. O conteúdo desses relatórios permaneceu igual no Git. O único arquivo novo do D6 é este relatório, não rastreado. A conferência ocorreu sem novos commits, push, PR ou deploy.

## 23. Conclusão

A implementação integrada reproduz o target aprovado. Os ensaios locais demonstraram a remoção seletiva dos 46 arquivos, preservação dos artefatos remanescentes e do tooling testado e redução comprimida de 1,44%. O operador reportou deploy Live e sucesso nos fluxos de produção descritos. As limitações operacionais e de segurança permanecem registradas para acompanhamento.

**B06-D PRONTO PARA ENCERRAMENTO**
