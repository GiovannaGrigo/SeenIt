# B06-A2 — Validação segura de HTTPS e Forwarded Headers

Data: 19/11/2026. Escopo: diagnóstico, documentação e testes sintéticos locais. Nenhuma correção implementada.

## 1. Objetivo

Determinar como o SeenIt restaura o esquema original e limita a confiança nos proxies, reproduzir os cenários A–G sem dependências reais e identificar a menor ação segura para B06-A3. O comportamento local pode ser validado; a configuração efetiva do serviço no Render permanece não comprovada.

### Estado inicial e preservação

- Branch atual: `security/b06-a1-hardening-diagnosis`; nenhuma branch criada.
- HEAD/base: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`.
- `git status --porcelain=v1`: somente `?? docs/B06-A1-DATAPROTECTION-HTTPS-DIAGNOSIS.md`. Esse relatório já existia e foi preservado sem edição.
- HEAD, main e referência local origin/main no mesmo SHA; `git rev-list --left-right --count HEAD...origin/main`: `0 0`.
- `git ls-remote origin refs/heads/main` confirmou o mesmo SHA diretamente no remoto. A primeira tentativa encontrou bloqueio de rede do sandbox; a repetição autorizada concluiu. Não houve fetch, alteração de referências ou do histórico.
- `maintenance-phase1` em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, worktree `.worktrees/maintenance-phase1`, já estava suja: seis arquivos modificados e seis entradas não rastreadas, entre diretórios e documentos. Portanto, intacta significa preservar esse estado preexistente, não alegar árvore limpa. Seu código não foi usado como base desta investigação.
- Capturados SHA-256 de 230 arquivos da árvore principal (rastreados e B06-A1) e 161 arquivos da worktree de manutenção (rastreados e não ignorados), além do status desta worktree. Conferência final registrada na seção 14.

## 2. Configuração atual

Evidências do código no SHA acima:

| Item | Comportamento comprovado |
| --- | --- |
| `Program.cs:22–28` | `PORT`, se presente, deve ser inteiro entre 1 e 65535; aplica `UseUrls(http://0.0.0.0:{PORT})` |
| `Program.cs:67–95` | Rejeita ativação automática ampla e prepara confiança explícita |
| `Program.cs:159–176` | Forwarded headers antes de HTTPS; `/health` excluído do redirect |
| Dockerfile | `PORT=10000`, `ASPNETCORE_HTTP_PORTS=10000`, ambientes Production; imagens .NET fixadas por digest |
| `backend/docker/runtime-settings.json` | JSON sanitizado com TVmaze/logging, substitui appsettings no build Docker; não contém lista de proxies nem porta HTTPS |
| appsettings local | `ReverseProxy:KnownProxies=[]`, `AllowedHosts=*`; não comprova esses valores em produção |
| launchSettings local | Development com HTTPS localhost:7042 e HTTP localhost:5042; não configura a imagem Render |
| HTTPS da aplicação | Não há `AddHttpsRedirection` com porta explícita, listener TLS explícito ou `UseHsts` no código inspecionado |

Variáveis relevantes: `ReverseProxy__KnownProxies__0` e índices adicionais; `ASPNETCORE_FORWARDEDHEADERS_ENABLED`; `PORT`; `ASPNETCORE_HTTP_PORTS`; `ASPNETCORE_URLS`; `ASPNETCORE_HTTPS_PORT` (porta de destino do redirect, não listener); `ASPNETCORE_HTTPS_PORTS` (binding); `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT`; `AllowedHosts`. Overrides remotos, argumentos e configurações Kestrel não foram inspecionados. A API não carrega `.env.render.example` automaticamente.

`UseUrls` explica a precedência sobre HTTP_PORTS. Essa redundância não demonstra conflito de portas nem exige correção para HTTPS público.

## 3. Topologia Render documentada

O Render documenta recepção de HTTPS na borda, terminação TLS e encaminhamento HTTP ao processo em `PORT`; recomenda usar `X-Forwarded-Proto` para conhecer o esquema original. Também realiza o redirect HTTP público → HTTPS. [Tutorial oficial Render](https://render.com/tutorials/web-service-vs-static-site/web-services), [TLS gerenciado](https://render.com/docs/tls), [port binding](https://render.com/docs/web-services#port-binding).

A documentação geral menciona Cloudflare e balanceadores, além de `X-Forwarded-For`. Isso não determina os valores, a quantidade de entradas ou a sanitização dos headers desta implantação. [Artigo oficial Render](https://render.com/articles/how-render-handles-ddos-attacks).

Documentação existente no repositório:

- `backend/SeenIt.Api/RENDER.md:72–79,197–205`: PORT, HTTP interno, TLS externo, confiança explícita e pendência de comprovação da topologia.
- `backend/SeenIt.Api/RENDER-ENV-CHECKLIST.md:26–31,71–81`: peer imediato, IPs alternativos, um salto, sanitização e risco de `/health` passar sem validar esquema.
- `.env.render.example:24–34`: placeholder de proxy imediato; não fornece um IP utilizável.
- B06-A1: warnings e hipóteses, sem observação por requisição em produção.

Esses documentos descrevem requisitos e arquitetura esperada. Nenhum fornece inventário verificado dos peers atuais, contrato de estabilidade ou configuração efetivamente aplicada no Render.

## 4. Fluxo de HTTPS

```text
Cliente HTTP público ──> borda Render ──> redirect para HTTPS público
Cliente HTTPS público ──> borda Render [terminação TLS]
                       ──> encaminhamento HTTP para Kestrel em PORT
                       ──> ForwardedHeaders, se lista explícita não vazia
                            peer confiável: restaura Scheme/RemoteIp de headers
                            peer desconhecido: não aplica os headers
                       ──> HTTPS redirection, exceto caminho /health
                       ──> static files → IntegrityMiddleware → routing → CORS
                       ──> autenticação JWT → rate limiter → autorização → endpoint
```

Os pontos intermediários do Render são abstraídos neste fluxo; não representam uma contagem comprovada de saltos. Os mapeamentos de endpoints não antecipam sua execução. Static files e preflights podem encerrar antes da autenticação.

A ordem no código é adequada. Quando Scheme é `https`, o redirect prossegue sem resposta de redirecionamento. Quando é `http`, o middleware usa uma porta HTTPS conhecida para gerar 307 por padrão; sem porta descoberta, registra warning e deixa a requisição seguir. [Microsoft: HTTPS e reverse proxy](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0).

Para o ingresso público padrão, a borda Render já responde pelo redirect. Manter uma proteção na aplicação pode ser justificável por outros ingressos ou portabilidade, mas exige esquema correto e destino externo correto. Configurar apenas 443 com proxy desconhecido pode fazer cada HTTPS externo retornar outro redirect para a mesma URL. Não se instalou certificado nem se presumiu necessário habilitar TLS no Kestrel.

## 5. Configuração de forwarded headers

| Aspecto | Configuração atual |
| --- | --- |
| Condição de ativação | Array `ReverseProxy:KnownProxies` convertido em IPs, com comprimento maior que zero |
| `ForwardedHeaders` | `XForwardedFor \| XForwardedProto` |
| Nomes | Padrões `X-Forwarded-For` e `X-Forwarded-Proto` |
| Proxies | Limpa `KnownProxies`; adiciona somente IPs configurados e IPv4 mapeados para IPv6 |
| Redes | Limpa `KnownIPNetworks` (.NET 10); nenhuma rede fica autorizada. Não configura a API antiga `KnownNetworks` |
| Limite | `ForwardLimit=1`, processa o valor mais à direita |
| Host | `X-Forwarded-Host` não habilitado; não altera Request.Host por esse header |
| Simetria | `RequireHeaderSymmetry` não configurado; padrão false. Proto sem For pode ser aceito de peer confiável |
| Validação de entrada | Rejeita IP inválido e endereços Any IPv4/IPv6; não aceita CIDR. Não atesta por si a propriedade operacional do IP |
| Atalho automático | Startup falha se `ASPNETCORE_FORWARDEDHEADERS_ENABLED` for `true`, ignorando caixa |

Lista vazia desabilita a chamada da aplicação ao middleware, em vez de habilitar confiança em qualquer origem. Acrescentar vários IPs à lista permite peers imediatos alternativos; não aumenta o número de saltos processados.

A Microsoft documenta processamento da direita para a esquerda, limite de saltos e verificação do peer. [Proxy/load balancer .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0). O endurecimento ignora headers de proxies desconhecidos. [Mudança oficial Microsoft](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0).

## 6. Segurança da política de confiança

A política do código restringe a aceitação aos IPs enumerados e não apresentou bypass nos cenários locais. Entretanto, somente a configuração efetiva e o controle da rede podem demonstrar que esses IPs pertencem exclusivamente aos proxies autorizados.

O limite de um salto impede consumir um valor forjado à esquerda quando há valor correto à direita. Não valida a veracidade dos dados fornecidos pelo peer confiável. Um teste de controle demonstrou que um proxy autorizado que repasse um único XFF adulterado consegue alterar RemoteIpAddress: é o limite de confiança esperado, que exige comprovar sanitização na infraestrutura.

Peer desconhecido mantém Scheme/IP de transporte. A biblioteca ainda pode deixar os headers originais disponíveis no request; nenhum consumidor deve interpretá-los manualmente como confiáveis. `X-Forwarded-Host` foi injetado em todos os testes e não alterou Host; isso não equivale a validar o header HTTP Host original ou a configuração remota de AllowedHosts.

Não ampliar confiança para todas as origens, não usar `0.0.0.0/0`, não limpar ambas as listas sem repovoar proxies, não desativar verificação e não ativar o atalho automático. IP privado, endereço público de DNS ou IP de saída não são evidências de identidade do proxy imediato.

## 7. Cenários locais testados

Harness descartável exclusivamente fora do repositório, com ASP.NET Core TestServer em memória, sem sockets de aplicação, banco, storage, TVmaze ou API de produção. Usou pacotes já existentes no cache: TestHost 10.0.7 e JwtBearer 10.0.0; restore com fontes NuGet vazias e auditoria remota desativada somente no projeto temporário. SDK 10.0.301; shared runtime ASP.NET Core/.NET 10.0.9 no Windows.

Foram extraídos literalmente do Program.cs os blocos de configuração dos proxies e de autenticação/autorização. O harness reproduz forwarded headers → exceção de health/redirect → routing → autenticação → autorização. Define apenas endpoints temporários `/probe`, `/private` e `/health` no processo de teste. Data Protection do harness usa provider efêmero para não persistir chaves locais.

O mecanismo TestHost já está disponível nos checks existentes. Optou-se pelo harness dirigido porque a suíte existente usa Kestrel, cenários de banco e arquivos e não permite atribuir arbitrariamente o peer do transporte a cada cenário. Não se alteraram nem se executaram os testes existentes. Os resultados abaixo validam os middlewares e opções extraídas; não certificam toda a API, os endpoints reais ou o runtime Linux da imagem.

Convenções sintéticas: P=`192.0.2.10` (único proxy confiável); U=`198.51.100.10` (peer desconhecido); C=`198.51.100.20` (cliente); F=`203.0.113.99` (IP forjado); P2=`192.0.2.11` (outro salto, não autorizado). Todos de faixas documentais. Transporte inicial sempre HTTP; Host `api.example.test`; `X-Forwarded-Host: attacker.example.test` em todos os casos. Coluna porta indica destino HTTPS 443 configurado somente no harness, sem listener TLS.

| Caso | Entrada: peer; Proto; For | Porta | Scheme / RemoteIp resultantes | HTTP | Warning de porta |
| --- | --- | --- | --- | --- | --- |
| A-port | U; ausente; ausente | 443 | http / U | 307 | Não |
| A-no-port | U; ausente; ausente | Ausente | http / U | 200 | Sim |
| B-trusted | P; https; C | 443 | https / C | 200 | Não |
| C-untrusted | U; https; F | 443 | http / U | 307 | Não |
| D-forged-left | P; https; F, C | 443 | https / C | 200 | Não |
| D-untrusted-for | U; https; F | Ausente | http / U | 200 | Sim |
| D-unsanitized-trusted | P; https; F | 443 | https / F | 200 | Não |
| E-multiple | P; http, https; F, C | 443 | https / C | 200 | Não |
| E-last-http | P; https, http; F, C | 443 | http / C | 307 | Não |
| E-two-hops | P; https, http; C, P2 | 443 | http / P2 | 307 | Não |
| F-known-no-port | P; https; C | Ausente | https / C | 200 | Não |
| G-unknown-no-port | U; https; C | Ausente | http / U | 200 | Sim |
| disabled | P; https; C; lista vazia | 443 | http / P | 307 | Não |
| mapped-ipv4 | ::ffff:192.0.2.10; https; C | 443 | https / C | 200 | Não |
| proto-only-known | P; https; ausente | 443 | https / P | 200 | Não |
| proto-only-unknown | U; https; ausente | 443 | http / U | 307 | Não |
| invalid-for | P; https; not-an-ip | 443 | http / P | 307 | Não |
| health | U; sem headers; /health | 443 | http / U | 200 | Não |
| health-uppercase | U; sem headers; /HEALTH | 443 | http / U | 200 | Não |
| health-trailing | U; sem headers; /health/ | 443 | http / U | 307 | Não |

Em todo 307, Location foi conferido contra `https://api.example.test` + caminho + `?synthetic=1`, sem aceitar Host encaminhado. Nos demais casos, Location permaneceu vazio. Em E-multiple, sobraram os valores à esquerda nos headers; foram consumidos somente os valores à direita. Nos casos confiáveis com Proto aplicado, X-Original-Proto registrou `http`.

Autenticação: endpoint sintético `/private` com RequireAuthorization, mesmas opções de validação JWT do código, chave e identidade descartáveis. Não foram registrados os tokens.

| Caso | Transporte reconhecido | JWT | HTTP |
| --- | --- | --- | --- |
| auth-no-token | https por P | Ausente | 401 |
| auth-invalid | https por P | Malformado | 401 |
| auth-valid | https por P | Assinatura, issuer, audience e lifetime válidos | 200 |
| auth-wrong-issuer | https por P | Issuer divergente | 401 |
| auth-wrong-audience | https por P | Audience divergente | 401 |
| auth-wrong-signature | https por P | Outra chave | 401 |
| auth-expired | https por P | Expirado há uma hora | 401 |
| auth-forged-proto-no-port | http por U; sem porta de redirect | Ausente; Proto forjado | 401, warning |
| auth-redirect-precedes | http por U; porta 443 | Ausente; Proto forjado | 307 antes da autenticação |

Um 307 anterior ao endpoint não equivale a autorização concedida. O controle negativo sem porta mostra que headers forjados não autenticam uma requisição.

## 8. Resultados

**Execução final: 29/29 cenários aprovados; código de saída 0; runtime 10.0.9.**

Os cenários reproduzem: reconhecimento de HTTPS por peer autorizado; rejeição de headers de peer desconhecido; processamento de somente um salto; aceitação de IPv4 mapeado; Host encaminhado ignorado; limites de sanitização; comportamento de health; ausência de bypass JWT nos casos ensaiados.

Sem porta HTTPS, o warning é acompanhado de continuidade da pipeline. Isso explica por que um esquema percebido incorretamente pode coexistir com funcionamento aparente. O 200 é do endpoint anônimo sintético, não uma medição de endpoint real nem prova de HTTPS público.

Com porta 443 e Scheme incorreto, o redirect é reproduzível. A eventual repetição através da borda é um risco condicionado à topologia, não um loop de produção observado. Não foi comprovada falha explorável nem configuração indevida na implantação real.

## 9. Comportamentos não confirmados

Antes de qualquer alteração, o operador deverá fornecer evidências sanitizadas, sem tokens, cookies, Authorization, dados pessoais ou conteúdo de chaves:

1. Identidade do artefato implantado: commit, digest da imagem e versão efetiva do runtime.
2. Inventário apenas das configurações pertinentes: proxies conhecidos, PORT, bindings HTTP/HTTPS, porta de redirect, ambiente, AllowedHosts e presença/valor do atalho automático; não enviar dump de todas as variáveis.
3. Confirmação do Render sobre IP(s)/família do peer imediato visto pelo Kestrel, estabilidade entre reinícios/deploys/escala e exclusividade desses endereços para proxies autorizados.
4. Diagrama dos ingressos público e privado, CDNs/proxies adicionais, health checks e eventuais rotas que contornem a borda.
5. Contrato da plataforma sobre remoção/sobrescrita/append de XFF e Proto, ordem e número de valores. Exemplos estruturais anonimizados, usando IPs documentais, sem IPs reais de clientes.
6. Evidência obtida pelo operador, em ambiente autorizado, de Scheme e peer antes/depois do middleware, caminho sanitizado e status/Location por correlação; nenhuma criação de endpoint diagnóstico público permanente.
7. Confirmação de enforcement HTTP → HTTPS em cada domínio/ingresso público e tratamento esperado do tráfego privado. Resumo sem dados de usuários das verificações de redirect e ausência de loops.

Documentação geral do fornecedor não substitui essas evidências. Nenhum painel do Render, Vercel, Supabase ou endpoint de produção foi acessado nesta etapa.

## 10. Riscos

- Lista vazia ou peer legítimo ausente: Scheme permanece HTTP e IP permanece do transporte; o warning pode ser consequência. Não é prova da causa remota específica.
- Acrescentar 443 sem restaurar Scheme: loop potencial e indisponibilidade.
- Confiar em endereço incorreto ou proxy sem sanitização: spoofing passa a ocorrer dentro da fronteira autorizada.
- Topologia com múltiplos saltos: o valor direito pode descrever outro proxy ou HTTP interno, não o cliente original. Aumentar ForwardLimit sozinho não resolve.
- HTTP por ingresso alternativo: redirect sem porta não o bloqueia; é necessário esclarecer proteção e necessidade desse ingresso.
- URLs de avatares locais/legados usam Scheme/Host/PathBase (`AvatarStorageService.cs:54–59`); esquema incorreto pode produzir URLs HTTP. Avatares novos usam URL pública do storage.
- AllowedHosts efetivo não confirmado. Ignorar X-Forwarded-Host não certifica validação do Host original.

O rate limiter atual não particiona por IP; não se atribui a XFF incorreto um bypass de limite por IP inexistente.

## 11. Alternativas de correção — propostas, não executadas

### A. Corrigir somente a lista de peers, se comprovadamente ausente/incorreta

- Problema: não reconhecimento de um proxy legítimo em topologia compatível com um salto.
- Arquivos: nenhum arquivo funcional se o contrato atual for suficiente; configuração operacional `ReverseProxy__KnownProxies__N` no serviço, somente em etapa futura autorizada.
- Mudança mínima: cadastrar exclusivamente os IPs imediatos confirmados, mantendo ForwardLimit=1 e os headers atuais.
- Riscos/Render: endereços instáveis causam regressão; endereço compartilhado indevidamente amplia confiança. Depende de garantia de estabilidade e sanitização do Render.
- Testes: B/C/D/E/F/G, famílias IPv4/IPv6, rota de health, JWT e URLs legadas; depois validação de infraestrutura autorizada.
- Aceitação: HTTPS público reconhecido como https; peer desconhecido não aplica headers; nenhum loop; autenticação preservada; lista operacional documentada.
- Rollback: restaurar exatamente o inventário anterior das configurações; revalidar disponibilidade. Não ampliar a lista como mitigação emergencial.

### B. Manter redirect na aplicação com destino externo explícito

- Problema: requisito demonstrado de redirect na aplicação, cuja porta não pode ser descoberta no hosting HTTP interno.
- Arquivos: eventualmente `Program.cs` para `AddHttpsRedirection` com HttpsPort externo ou configuração de runtime `ASPNETCORE_HTTPS_PORT`; documentação operacional correspondente. Nenhuma dessas opções foi aplicada.
- Mudança mínima: definir destino externo correto somente depois de comprovar confiança/esquema e necessidade; não criar listener/certificado interno.
- Riscos/Render: redundância com a borda; 443 isolado pode introduzir loop. Redirect não protege dados já enviados em HTTP e não fornece TLS para acessos internos diretos.
- Testes: A–G com/sem porta, repetição através de proxy sintético, domínio/porta externa, health e JWT.
- Aceitação: requisição já HTTPS não redireciona; HTTP elegível redireciona uma vez ao destino correto; nenhum bypass de autorização nem loop.
- Rollback: remover a nova opção/restaurar configuração e versão anteriores, preservando enforcement na borda confirmado previamente.

### C. Condicionar redirect ao modelo de hospedagem com enforcement na borda

- Problema: redundância do redirect interno, se a borda comprovadamente cobrir todos os ingressos relevantes.
- Arquivos: possível alteração mínima em `Program.cs` para condicionar UseHttpsRedirection e documentação/configuração operacional. Dockerfile e frontend não precisam mudar por essa decisão.
- Mudança mínima: condicionar o redirect no perfil com TLS terminado na borda; preservar a opção onde a aplicação efetivamente precisar dela. Não remover globalmente por causa de warning.
- Riscos/Render: outro ingresso pode servir HTTP sem enforcement; perda de proteção fora do Render. Esquema correto continua necessário para URLs, mesmo sem redirect.
- Testes: cobertura de ingressos, headers forjados, URLs legadas, health, JWT e execução fora desse perfil.
- Aceitação: HTTPS público imposto na borda; nenhum ingresso desprotegido aceito por acidente; Scheme correto; comportamento fora do Render definido.
- Rollback: restaurar o comportamento anterior e a configuração correspondente; evitar habilitar um redirect com porta explícita e Scheme incorreto.

Se a topologia exigir mais saltos ou não permitir uma lista estável e exclusiva, nenhuma alternativa acima deve ser aplicada mecanicamente. B06-A3 deverá primeiro definir uma fronteira de confiança verificável com o fornecedor; eventual proxy controlado ou revisão de integração exige projeto e autorização próprios.

## 12. Recomendação técnica

Preservar a implementação nesta etapa. A política do código é restritiva; sua adequação à infraestrutura permanece pendente. Não há fundamento para mudar código apenas para apagar o warning.

A menor correção candidata é A, exclusivamente se evidências confirmarem lista ausente/incorreta e topologia compatível. Se Scheme já estiver correto, correlacionar o warning com tráfego HTTP interno antes de mudar qualquer coisa. Escolher B ou C somente a partir do requisito de enforcement e do inventário de ingressos; a documentação Render favorece responsabilidade na borda para o ingresso público padrão.

Data Protection fica separado: mantém-se B06-A1 como referência. Não surgiu novo consumidor funcional identificado na inspeção dirigida; não se executou nova auditoria exaustiva de consumidores. A configuração efêmera do harness não é alteração do produto. Nenhuma persistência de chaves proposta ou implementada aqui; JWT permanece independente do key ring conforme o diagnóstico anterior.

## 13. Próxima etapa B06-A3

Receber o conjunto sanitizado da seção 9 e decidir se existe falha de configuração demonstrável. Se existir, preparar a menor mudança concreta, sua matriz de testes e rollback conforme a alternativa aplicável. Validar primeiro em ambiente isolado com a topologia confirmada; submeter qualquer alteração remota/deploy à autorização específica dessa próxima etapa. Se não existir falha, documentar a causa do tráfego que originou o warning e manter o comportamento.

B06-A3 não deve ativar confiança global, trocar JWT, misturar persistência de Data Protection nem alterar maintenance-phase1.

## 14. Evidências e limitações

Comandos de leitura: branch/status/log, inventário de branches/worktrees, comparação HEAD...origin/main, ls-remote somente leitura, leitura dirigida e buscas por proxy, HTTPS, autenticação, Scheme e consumidores de Data Protection. Não houve build ou execução da API do repositório.

Harness e artefatos retidos somente no diretório temporário indicado abaixo, sem credenciais reais. Para reproduzir enquanto existir: `dotnet restore <diretório>/Harness.csproj --configfile <diretório>/NuGet.Config` e `dotnet run --project <diretório>/Harness.csproj --no-restore`. O arquivo source-blocks.json guarda os blocos extraídos do código; Program.cs contém os casos e asserções. Requer SDK .NET 10 e os pacotes já disponíveis no cache; o NuGet.Config não contém fontes remotas.

As execuções preliminares foram interrompidas no caso de lista desabilitada porque o harness havia representado a lista vazia por um índice nulo, que o binder converteu em entrada inválida. Duas tentativas de substituir essa entrada não a removeram do arquivo; a remoção literal foi conferida antes da execução final. Corrigida exclusivamente a fixture externa para omitir a chave; os blocos do produto não foram alterados. O resultado de referência é a execução completa posterior.

Não há garantia de equivalência entre runtime Windows 10.0.9 testado e imagem Linux efetivamente publicada. As versões de imagem registradas em B05/B06-A1 são evidência histórica. Os testes não medem TLS real, disponibilidade pública, políticas da borda, rate limiting completo ou todas as rotas da API. Fontes oficiais foram consultadas em 19/11/2026; o conteúdo público consultado pode mudar.

Artefatos externos desta execução: `C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b06-a2-a981597da95140f1b2b60ff907fce27a`.

- `Program.cs`: SHA-256 `8D42A9E199B4065C98E7F2EBAC40E6F1B9D9137E808A65068A15C21B3C90D4D1`.
- `source-blocks.json`: SHA-256 `AB8FEF4086ECBA84CAE3090176A48A916A8883968B0D502558C44FE1A76A8E31`.
- Resultados: `bin/Debug/net10.0/results.json`, SHA-256 `59F8C91B527C96F2038EF536E9E3B8DDB9C15E7EC357D8634A946B1135E61A19`; 29 registros aprovados; processo concluído com exit 0.
- Integridade final: 230/230 arquivos preexistentes da árvore principal e 161/161 arquivos verificados de maintenance-phase1 com SHA-256 preservado; status e referência da worktree de manutenção inalterados. Inclui B06-A1 e relatórios B05 da árvore principal.
- Única criação no repositório: este relatório. Nenhum código, configuração funcional, teste existente, dependência, migration ou frontend alterado. Sem commit, push, PR ou deploy.

**B06-A2 VALIDAÇÃO PARCIAL** — matriz local concluída; adequação da configuração efetiva do Render depende das evidências operacionais da seção 9.
