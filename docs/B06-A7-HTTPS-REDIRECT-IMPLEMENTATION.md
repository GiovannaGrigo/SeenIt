# B06-A7 — Implementação controlada do redirecionamento HTTPS

Data: 19/11/2026. **B06-A7 IMPLEMENTAÇÃO VALIDADA**. Alteração local aplicada; opção não habilitada no Render; sem deploy ou commit.

## 1. Objetivo e preparação

Permitir delegar explicitamente o redirecionamento HTTP → HTTPS à borda de hospedagem, sem alterar o comportamento padrão, autenticação, proxies ou URLs.

Branch: `security/b06-a1-hardening-diagnosis`. Estado inicial: somente os cinco relatórios B06-A1/A2/A3/A4/A5 não rastreados; nenhum arquivo rastreado modificado. `git fetch origin` concluído; primeira tentativa bloqueada em .git/FETCH_HEAD pelo sandbox, repetida com autorização. HEAD e origin/main após fetch: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`, divergência `0 0`.

Snapshot de 234 arquivos preexistentes da árvore principal e 161 arquivos rastreados/não ignorados de maintenance-phase1. Esta worktree permaneceu em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, incluindo seis arquivos modificados e seis entradas não rastreadas preexistentes. Nenhuma branch/worktree criada ou excluída.

## 2. Evidências B06-A1 até B06-A6

- A1: diagnóstico de HTTPS/proxy e Data Protection; assuntos mantidos separados.
- A2: 29/29 testes isolados, confiança restrita e comportamento do redirect.
- A3: relação entre configuração ausente de proxies, HTTP interno e warning de porta.
- A4: candidato mínimo e 308/308 verificações locais; suporte a locais/legados continuava dependente de Scheme.
- A5: implementação condicionada à identidade do deploy, inventário de ingressos e referências de avatar; consulta agregada proposta, não executada pelo Codex.
- Fechamento operacional agora fornecido no pedido A7: operador confirma deploy ativo `0720353`, único ingresso utilizado no domínio público padrão Render, HTTPS público imposto pela borda e contagens abaixo. **Não foi disponibilizado relatório B06-A6 separado; não se inventa um arquivo ou validação dessa etapa.** As novas confirmações são a evidência operacional de fechamento das pendências anteriores.

| Contagem informada pelo operador | Valor |
| --- | ---: |
| usuarios_total | 4 |
| usuarios_sem_avatar | 1 |
| usuarios_com_avatar | 3 |
| avatares_supabase | 3 |
| avatares_locais | 0 |
| avatares_legados | 0 |
| nao_classificados | 0 |

Esses números e a identidade/topologia são declarações do operador, não consulta SQL, acesso a painel ou teste de produção pelo Codex. As somas são consistentes. O merge técnico `0720353` contém o Dockerfile corrigido; `09e9c40` acrescenta somente documentação conforme A5. Nenhum digest final/runtime remoto adicional foi medido aqui.

A autorização atual permite aplicar apenas o candidato local. Não autoriza habilitar a opção no serviço remoto ou publicar a alteração.

## 3. Alteração exata aplicada

Único arquivo funcional: `backend/SeenIt.Api/Program.cs`, no ponto original de registro do redirect. Diff: seis linhas adicionadas e duas removidas, sem reorganização ou comentário novo.

```diff
-app.UseWhen(context => !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase),
-    branch => branch.UseHttpsRedirection());
+var httpsRedirectionHandledByEdge = builder.Configuration.GetValue<bool>("ReverseProxy:HttpsRedirectionHandledByEdge", false);
+if (!httpsRedirectionHandledByEdge)
+{
+    app.UseWhen(context => !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase),
+        branch => branch.UseHttpsRedirection());
+}
```

O conteúdo completo do Program.cs implementado coincide com Candidate.Program.cs.txt de B06-A4 após normalização apenas de quebras de linha. A leitura é no startup. Nenhuma chave adicionada a appsettings, runtime-settings ou Dockerfile.

ForwardedHeaders permanece antes desse ponto, com sua condição e opções originais. Static files, IntegrityMiddleware, routing, CORS, autenticação, limiter, autorização e endpoints permanecem inalterados. O comentário de health já existente foi preservado, sem novos comentários.

## 4. Comportamento padrão: ausente ou false

O default é false: o UseWhen/UseHttpsRedirection anterior continua registrado. A exceção exata de `/health`, sem distinção de caixa, é mantida. Ausência de porta HTTPS continua permitindo prosseguir com o warning anterior; o padrão não passou a bloquear HTTP.

Com destino HTTPS sintético 8443, HTTP fora de health recebe 307 com caminho/query preservados. HTTPS reconhecido não é redirecionado. A ausência da opção não foi associada a Production, Development ou outra hospedagem: o parâmetro, não o ambiente, define a delegação.

Os testes compararam ausente e false, com e sem destino sintético, incluindo autenticação, headers, health e respostas. Não se configurou porta 443.

## 5. Comportamento com true

Não registra UseHttpsRedirection da aplicação, inclusive se existir destino HTTPS de redirect configurado. HTTP interno prossegue para o restante da pipeline, que continua autenticando/autorizando normalmente. `/health` mantém endpoint e contrato.

A opção declara que a borda já impõe HTTPS; não descobre nem verifica essa garantia. Não altera Request.Scheme/RemoteIpAddress/Host, não processa headers adicionais e não amplia KnownProxies. Sem proxy configurado, o transporte HTTP interno continua percebido como http.

**A opção não foi habilitada no Render.** Até implementação e configuração futuras autorizadas no serviço, o deploy ativo informado continua com seu comportamento anterior; este trabalho não elimina warnings remotos por si só.

## 6. Testes executados

Todos os comandos foram concluídos com exit code 0. Nenhum teste existente foi editado.

| Verificação | Comando ou mecanismo | Resultado |
| --- | --- | --- |
| Build Release API | `dotnet build backend/SeenIt.Api/SeenIt.Api.csproj --configuration Release --no-restore` | Êxito, 0 warnings e 0 erros |
| AvatarChecks | `dotnet run --project backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj --configuration Release --no-restore` | 16 grupos aprovados; imagens reais, validações, URLs, persistência simulada, compensação, concorrência e contratos |
| CoreChecks | `dotnet run --project backend/SeenIt.Checks/SeenIt.Checks.csproj --configuration Release --no-restore` | Watch time, classificação/cache/retries e 72 casos HTTP de cadastro aprovados |
| Integração existente | `dotnet run --project backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj --configuration Release --no-restore` | Suíte completa aprovada: resiliência de catálogo, integridade B03, 73 casos HTTP reais de cadastro, rate limiting e regressões |
| Harness B06-A4 | Cópia preservando o original, em TEMP; execução Release | 308/308 verificações, zero falhas, 266 casos de request da matriz |
| Entry point real complementar | Projeto temporário com referência à API atual e RegistrationFactory original, sem editá-la | 168/168 verificações, zero falhas, seis perfis e 66 casos da matriz, além de bodies e simulações |
| Auditoria NuGet API | `dotnet package list --project backend/SeenIt.Api/SeenIt.Api.csproj --no-restore --vulnerable --include-transitive` | Nenhum pacote vulnerável nas fontes consultadas; tentativa em sandbox bloqueada por socket, repetida com autorização e exit 0 |
| Diff/whitespace | `git diff -- backend/SeenIt.Api/Program.cs`; `git diff --check` | Somente wrapper esperado; sem erro de whitespace |

A suíte existente de cadastro aprovou 73 casos e criou 16 usuários exclusivamente na conexão SQLite em memória; requisições inválidas não executaram comandos de banco, SaveChanges ou hash. A suíte de rate limiting incluiu JWT, spoofing, efeitos colaterais, retry, health e concorrência; regressões incluíram logins sobrepostos, contabilidade global, isolamento de janelas, perfil simultâneo, cancelamento e CORS.

## 7. Resultados complementares no entry point real

O harness A4 usa pipeline reduzida. Para cobrir essa diferença, o projeto externo EntryPoint executa o Program.cs real via WebApplicationFactory, usando a RegistrationFactory existente por referência de compilação, sem alteração. Não cria endpoints na API.

Ambiente Production sintético, Npgsql substituído por SQLite em memória, Storage e clientes externos substituídos pela factory já existente. EnsureCreated e seed de um usuário por perfil ocorrem apenas nessa memória; nenhuma migration ou alteração de banco real. JWT válido gerado pelo TokenService real, com chave sintética. HTTP externo é bloqueado pela factory. Data Protection efêmero somente no teste complementar.

| Perfil | Opção | Destino sintético | HTTP em /api/profile sem JWT | HTTP com JWT válido | Warning de porta por host |
| --- | --- | --- | --- | --- | --- |
| absent | Ausente | Ausente | 401 | 200 | 1 |
| false | false | Ausente | 401 | 200 | 1 |
| edge | true | Ausente | 401 | 200 | 0 |
| absent-port | Ausente | 8443 | 307 | 307 anterior ao endpoint | 0 |
| false-port | false | 8443 | 307 | 307 anterior ao endpoint | 0 |
| edge-port | true | 8443 | 401 | 200 | 0 |

Além desses resultados:

- JWT malformado recebe 401 quando não há redirect anterior. O usuário autenticado e o corpo do perfil real foram conferidos; requisições sem token/inválidas não consultaram banco.
- HTTPS reconhecido com JWT válido chega a `/api/profile` com 200, inclusive com Proto=http forjado vindo de peer desconhecido.
- X-Forwarded-Proto=https e X-Forwarded-For forjado não alteram Scheme=http nem RemoteIpAddress do peer sintético sem KnownProxies. X-Forwarded-Host também não substitui o Host.
- `/health` e `/HEALTH` retornam 200; body real contém status=ok. `/health/` preserva o detalhe anterior: com middleware e destino disponível, recebe 307 antes do endpoint.
- Opção efetiva e lista de proxies vazia foram conferidas nos serviços da aplicação real, não apenas presumidas pelo teste.
- Ausente/false e ausente-port/false-port produziram sequências equivalentes de status, Scheme, IP, Host e Location.
- Na simulação HTTPS externo → HTTP interno, com true e JWT válido o perfil real termina em 200 na primeira passagem, sem Location. O controle padrão com destino sintético repete três redirects à mesma URL e interrompe a simulação. Não se afirma loop observado em produção.

A4 acrescenta controles de tokens expirados, issuer/audience/assinatura incorretos, valor inválido da opção, Development/Staging, proxy sintético conhecido/desconhecido e URLs dos serviços reais de avatar. Nenhum desses IPs foi cadastrado no projeto ou no Render.

## 8. Limitações e evidências locais

Validação local em Windows x64, SDK 10.0.301 e shared runtime .NET/ASP.NET Core 10.0.9. Não foi feito build Docker, medição do runtime Linux remoto, request de produção, consulta SQL ou acesso a Supabase/Render/Vercel. O full entry point usa dependências isoladas; não certifica integrações reais de banco/Storage ou TLS real.

Os primeiros restores temporários reutilizaram a configuração offline de B06-A4, com NuGetAudit=false. Para cumprir a exigência desta implementação de não deixar verificações de segurança desabilitadas, essa configuração foi corrigida nos projetos temporários: auditoria habilitada, fonte pública NuGet e NuGetAuditMode=all. Foram concluídos restores com auditoria habilitada da API, AvatarChecks, CoreChecks, RegisterIntegrationChecks e dos dois harnesses, sem alertas de vulnerabilidade. A primeira consulta da API em sandbox emitiu NU1900 por bloqueio de rede; foi repetida com autorização e concluída sem esse warning. Não houve mudança de versões ou manifests do repositório. O restore do projeto temporário com ProjectReference também regenerou assets ignorados da API; nenhuma alteração funcional adicional. A revisão final inclui build Release novamente e consulta explícita de vulnerabilidades da API com transitivas.

O relatório de origem B06-A4 e seus artefatos foram preservados: a reexecução usou cópia nova. Evidências temporárias desta etapa são listadas abaixo; podem ser removidas pelo sistema. Não contêm credenciais reais nem tokens registrados. Não foram criados testes permanentes ou comentários novos no código de produção/temporário.

## 9. Riscos residuais

Um true indevido em hospedagem sem enforcement HTTPS elimina o redirect da aplicação; o parâmetro não certifica a borda. O uso fora do ingresso confirmado requer nova avaliação. A configuração é lida no startup, não alternada a cada requisição.

O código mantém suporte a avatares locais/legados. Os zeros informados pelo operador referem-se ao estado observado e não impedem introdução futura de referências históricas; essa alteração não corrige URLs HTTP desses ramos. Preservar a avaliação caso novos fluxos dependam do esquema original.

Não processar forwarded headers mantém Scheme/IP do transporte. Isso foi preservado deliberadamente; aceitar todas as origens ou cadastrar IP arbitrário não faz parte desta correção. Ignorar X-Forwarded-Host não valida o Host HTTP original: AllowedHosts continua com sua responsabilidade existente.

HTTP de health é intencional e seu sucesso não comprova segurança de integrações. Eliminação do warning com true não constitui, isoladamente, melhoria de segurança. Data Protection e redundância HTTP_PORTS/UseUrls permanecem assuntos separados.

## 10. Estratégia de rollback

Antes de qualquer commit/deploy futuro, a alteração local pode ser revertida editando somente o wrapper para restaurar as duas linhas anteriores, preservando todos os relatórios. Não usar reset, clean, rebase ou force.

Após implantação autorizada, omitir `ReverseProxy__HttpsRedirectionHandledByEdge` ou definir false e reiniciar/republicar conforme o procedimento operacional restaura o comportamento anterior, sem mudar JWT, proxies ou Dockerfile. Reverter apenas o wrapper também remove a funcionalidade. Qualquer ação remota exige autorização própria.

Na reversão, verificar health, JWT, status/Location e Scheme. A volta do warning pode ser esperada; não corrigir com 443 isolada. Se outro trabalho introduzir destino explícito sem restaurar Scheme, reativar redirect pode reintroduzir ciclo.

## 11. Orientação para configuração futura no Render

Somente depois de revisão/versionamento e autorização específica de implantação/configuração:

1. Identificar o commit/artefato que efetivamente contém esta alteração e implantá-lo pelo procedimento aprovado.
2. Confirmar que permanecem válidas as declarações do operador sobre único ingresso público padrão e HTTPS na borda.
3. Cadastrar `ReverseProxy__HttpsRedirectionHandledByEdge=true` apenas nesse serviço/perfil autorizado, mantendo KnownProxies e o atalho amplo inalterados. Não adicionar porta 443 ou certificado interno.
4. Verificar health, autenticação, ausência de redirects repetidos e comportamento funcional sob critérios acordados, com plano de reversão.

Essa sequência é orientação documental. **Nenhuma variável remota foi lida ou alterada pelo Codex nesta etapa, e true não foi habilitado no Render.** Nenhuma publicação está autorizada implicitamente por este relatório.

## 12. Conferência final e recomendação B06-A8

Único arquivo funcional alterado: Program.cs. Único relatório novo: B06-A7. B06-A1/A2/A3/A4/A5 e relatórios B05 preservados; não houve criação de B06-A6, mudança de testes existentes, Dockerfile, appsettings, runtime-settings, JWT, KnownProxies, serviços de avatar, frontend ou migrations.

B06-A8 deverá revisar o diff e as evidências de validação e definir a próxima ação autorizada de entrega/versionamento. Commit, push, PR, deploy e configuração no Render continuam pendentes de autorização específica. Se a topologia ou as referências de avatar mudarem antes da publicação, reavaliar as condições, sem reaproveitar os zeros como garantia permanente.

Diretório de evidências temporárias: `C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b06-a7-3a9b42833494499888fa3eda9d62b529`.

| Artefato | SHA-256 |
| --- | --- |
| `A4/bin/Release/net10.0/results.json` | `388845D8BBB1F4470E66E49AAF233AB7908582848AC413C7C04526D54D921530` |
| `EntryPoint/Program.cs` temporário | `0B63A7593FE98E77866F425BBB5AEC52AAE34C172CCC7D1E5183FB0EFF88DD84` |
| `EntryPoint/bin/Release/net10.0/results.json` | `A123B01960BAAB3367913CD7411F50D3C5811901A489201F4F450EDB939E4004` |
| `entrypoint-run.log` | `BE7D40A3433A2AEEF7F2FFFE10DC6B887CE4FDA7D7058D9AD94224C75363EE26` |

Program.cs de produção após a alteração: SHA-256 `5877DB1842B26C5909D71EEA11850A74CB9D4557D2FD4EEB79C4663AC75EDCD1`. Asserções dos dois harnesses novamente conferidas nos JSONs: 308/308 e 168/168. A consulta explícita NuGet foi concluída com auditoria habilitada e nenhuma vulnerabilidade identificada nas fontes atuais, sem atualizar pacotes.

Conferência final: 233/233 arquivos preexistentes da árvore principal, excluindo somente o Program.cs autorizado, e 161/161 arquivos verificados de maintenance-phase1 preservados por SHA-256. HEADs e estados anteriores mantidos; somente M em Program.cs e a adição de B06-A7 diferem do estado inicial. Os cinco relatórios B06 anteriores continuam não rastreados, sem edição. HEAD/origin/main continuam em `09e9c40`; manutenção continua em `fbf8a7f`. Sem commit, push, PR, migrations, acesso a banco, configuração remota ou deploy.

**B06-A7 IMPLEMENTAÇÃO VALIDADA**
