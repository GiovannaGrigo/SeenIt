# B06-A4 — Experimento isolado de redirecionamento HTTPS no Render

Data: 19/11/2026. Resultado: **APROVADO APENAS LOCALMENTE**. Nenhuma alteração funcional aplicada ao repositório ou ao Render.

## 1. Objetivo

Avaliar uma opção explícita para delegar HTTP → HTTPS à borda da hospedagem, mantendo o comportamento anterior por padrão e sem ampliar confiança em forwarded headers. Separar o efeito sobre redirects do efeito sobre Scheme e URLs de avatar. A falta de comprovação dos ingressos permite somente simulação, não aprovação para aplicação permanente.

## 2. Estado inicial e preservação

- Branch: `security/b06-a1-hardening-diagnosis`.
- HEAD e referência local `origin/main`: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`; divergência `0 0`. Não foi consultado o remoto nem executado fetch nesta etapa.
- Git inicial: somente B06-A1, B06-A2 e B06-A3 não rastreados. Nenhum arquivo rastreado modificado na árvore principal; nenhuma divergência inesperada.
- `maintenance-phase1`: HEAD `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, worktree existente com seis arquivos modificados e seis entradas não rastreadas preexistentes. Preservação abrange esse estado, não uma alegação de árvore limpa.
- Snapshot SHA-256 de 232 arquivos preexistentes da árvore principal e 161 arquivos rastreados/não ignorados de maintenance-phase1, com HEADs e status. Nenhuma branch/worktree criada, excluída ou alterada.

Hashes de referência dos arquivos mais relevantes:

| Arquivo | SHA-256 |
| --- | --- |
| `backend/SeenIt.Api/Program.cs` | `65C6DB2E1A19A177E1DC3478DBFB759F7807822572FD4C65331DC55EBDCEE587` |
| `backend/SeenIt.Api/Dockerfile` | `525A125804CDE3159934026AEF55EBCB01ECD1495D5D52254705CB49B5FDFBBC` |
| `backend/docker/runtime-settings.json` | `79663ADA6F808534D06EED66F746C9FB1BBA2D3415333A07B53D4F8AB6FC09A3` |
| B06-A1 | `1355F5D081CDAFED1995A5BA31F51B9438B16438CF881D89631B6F10A5F3572F` |
| B06-A2 | `16DF614397A966165AF7EC22E992573963E768DBFD5C34C90FD26D89490B021D` |
| B06-A3 | `5E9659778810791D8018F1017549621971730FDF3BAC475ED6FDFB78FCFBA8D9` |

## 3. Evidências disponíveis e pré-condições

B06-A3 incorporou o relato do operador: não há KnownProxies, atalho automático de forwarded headers, PORT, ASPNETCORE_URLS ou ASPNETCORE_HTTPS_PORT cadastrados no painel. Não foram solicitados valores de segredos. Essa evidência não equivale a inspeção do ambiente completo do processo.

O código confirma que lista vazia impede a chamada da aplicação a UseForwardedHeaders. UseHttpsRedirection continua ativo fora de `/health`. A imagem define PORT e HTTP_PORTS como 10000; Program.cs usa PORT para binding HTTP. O JSON sanitizado publicado não configura proxies nem porta HTTPS. Portanto, ausência de PORT no painel não impede o listener na porta 10000.

O Render documenta terminação TLS na borda, HTTP até o backend e redirect público HTTP → HTTPS. A Microsoft admite dispensar o middleware da aplicação quando o proxy responde pelo redirect. Isso fundamenta o experimento, mas não comprova todos os ingressos desta implantação. [TLS Render](https://render.com/docs/tls), [fluxo de Web Services Render](https://render.com/tutorials/web-service-vs-static-site/web-services), [HTTPS no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0).

Rotas examinadas: health, cadastro/login, catálogo, biblioteca, perfil/avatar e episódios assistidos, incluindo os controllers. O código enumera rotas HTTP, não os caminhos de rede pelos quais são alcançadas. Nenhum inventário efetivo de domínios, ingressos privados ou bypass da borda foi fornecido.

As opções JWT continuam baseadas em assinatura, issuer, audience e lifetime. Não foi identificado uso funcional do IP original nas partições atuais de rate limiting. Request.Scheme permanece relevante no ramo de avatar local/legado e para redirect interno quando existe destino HTTPS.

## 4. Pendências operacionais

Continuam necessárias, antes de qualquer liberação permanente:

1. Commit/digest e runtime do artefato efetivamente executado, comprovando correspondência ao código analisado.
2. Inventário dos ingressos e domínios usados: público, privado, health, proxies/CDNs adicionais e qualquer acesso direto ao backend.
3. Evidência de enforcement HTTPS para todos os ingressos relevantes, ou contrato explícito e aceito para suas exceções internas.
4. Evidência agregada sobre existência de avatares locais/legados ativos, sem dados pessoais. O código permite esses registros; não demonstra sua ausência.
5. Caso se pretenda restaurar Scheme por headers, comprovação de peers, estabilidade/exclusividade, sanitização e quantidade de saltos. Essa comprovação não é necessária apenas para simular a opção de redirect.

Não houve acesso ao banco, consulta de usuários ou chamada à implantação. A documentação geral da plataforma não substitui os itens acima.

## 5. Desenho experimental

Harness descartável em diretório TEMP fora do Git, usando ASP.NET Core TestServer em memória. Não abre listeners públicos, não usa certificados ou TLS real e não inicia o entry point completo da API.

Foram extraídos literalmente do Program.cs os blocos de configuração de proxies, JWT/autorização e UseWhen/UseHttpsRedirection. A pipeline dirigida reproduz forwarded headers → redirect condicional → routing → autenticação → autorização, com endpoints exclusivamente sintéticos. Nenhum endpoint do projeto foi editado. O endpoint de health do harness reproduz status/body e metadados anônimo/DisableRateLimiting.

Seis arquivos de avatar foram copiados sem alteração e compilados no harness: AvatarStorageService, SupabaseAvatarStorageService, AvatarStorageOptions, AvatarPaths, IAvatarStorageService e AvatarImageProcessor. Os hashes das cópias coincidem com os originais. Os testes executam os métodos reais de geração de URL, não uma reimplementação de seu resultado. Upload, exclusão e processamento nativo de imagens não foram executados.

Dependências do projeto temporário: TestHost 10.0.7, JwtBearer 10.0.0 e SkiaSharp 4.155.0, versões já disponíveis no cache. Restore offline com fontes NuGet vazias e auditoria remota desativada somente nesse projeto. Nenhuma dependência do repositório foi atualizada. SDK 10.0.301; shared runtime .NET/ASP.NET Core 10.0.9, Windows x64.

JWTs e opções de Storage são sintéticos; tokens não são registrados. IHttpClientFactory foi substituído no harness por implementação que lança exceção em qualquer tentativa de criar cliente externo. As verificações registraram zero tentativas. Data Protection do processo de teste usa provider efêmero, sem alterar o produto.

IPs pertencem a faixas documentais: peer desconhecido `198.51.100.10`, proxy sintético autorizado `192.0.2.10`, cliente sintético `198.51.100.20`, IP forjado `203.0.113.99`. O IP autorizado existe somente na simulação e não é recomendação para o Render.

## 6. Alteração mínima proposta

Opção experimental: **`ReverseProxy:HttpsRedirectionHandledByEdge`**. Segue a seção ReverseProxy e a convenção PascalCase já existentes. Sua representação como variável seria `ReverseProxy__HttpsRedirectionHandledByEdge`; ela não foi criada no Render nem na configuração do projeto.

Sem opção ou com false: preserva exatamente a seleção anterior de redirect. Com true: declara responsabilidade externa e não registra esse middleware na aplicação. Não altera Scheme, IP, JWT, Host, endpoints, serviços ou opções de confiança. Não usa IsProduction como prova de borda. O efeito depende da opção explícita em qualquer ambiente; nos demais perfis sem a opção, o comportamento anterior permanece.

Trecho candidato, existente somente como artefato externo de revisão:

```csharp
var httpsRedirectionHandledByEdge = builder.Configuration.GetValue<bool>("ReverseProxy:HttpsRedirectionHandledByEdge", false);
if (!httpsRedirectionHandledByEdge)
{
    app.UseWhen(context => !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase),
        branch => branch.UseHttpsRedirection());
}
```

O diff externo troca somente as duas linhas originais pelas seis linhas acima. O parâmetro é interpretado no startup; valor inválido foi rejeitado, não convertido silenciosamente em delegação. A opção representa uma declaração do operador: não verifica TLS nem descobre a plataforma automaticamente. Padrão compatível significa manter inclusive a limitação anterior de não redirecionar quando não há porta; não transforma o padrão existente em bloqueio de HTTP.

Não foi configurada porta 443. A porta 8443 foi usada exclusivamente como destino sintético em perfis de teste para observar o middleware existente, sem configurar listener HTTPS ou propor esse destino para produção.

## 7. Testes e resultados

**Execução final: 308/308 verificações aprovadas, exit code 0.** A matriz contém 266 casos de request, mais 12 verificações HTTP de status/body de health e quatro chamadas de backend nas duas simulações de loop: 282 requests em memória. Inclui também verificações de equivalência, contagem de warnings, ausência de clientes externos e rejeição de configuração inválida.

Foram usados 12 perfis. Cada um executou 22 casos comuns: HTTP/HTTPS, três variantes de health, sete controles JWT, dois casos de headers forjados e oito casos de avatar. Dois perfis acrescentaram um caso com proxy sintético conhecido cada. A contagem de warning abaixo é por host, não por requisição: o middleware guarda a descoberta de porta.

| Perfil | Ambiente | Opção | Destino HTTPS sintético | HTTP normal na aplicação | Warnings de porta |
| --- | --- | --- | --- | --- | --- |
| baseline | Production | Implementação anterior | Ausente | Prossegue | 1 |
| candidate-default | Production | Ausente | Ausente | Prossegue | 1 |
| candidate-false | Production | false | Ausente | Prossegue | 1 |
| candidate-edge | Production | true | Ausente | Prossegue | 0 |
| candidate-local-port | Production | Ausente | 8443 | 307 | 0 |
| baseline-port | Production | Implementação anterior | 8443 | 307 | 0 |
| candidate-edge-port | Production | true | 8443 | Prossegue | 0 |
| candidate-development | Development | Ausente | 8443 | 307 | 0 |
| candidate-staging | Staging | Ausente | 8443 | 307 | 0 |
| candidate-edge-development | Development | true | Ausente | Prossegue | 0 |
| trusted-default | Production | Ausente | 8443 | 307 para peer desconhecido | 0 |
| trusted-edge | Production | true | Ausente | Prossegue | 0 |

Mapeamento dos cenários solicitados:

| Cenário | Observação e resultado |
| --- | --- |
| A — Padrão | candidate-default e candidate-false equivalentes ao baseline em todos os 22 casos, incluindo warning, Scheme, IP, Host, status, Location e URL |
| B — Borda explícita | HTTP interno sem redirect adicional nem warning, inclusive com destino sintético disponível |
| C — HTTPS reconhecido | Scheme https recebido diretamente: 200 no probe e nenhum redirect. Com proxy sintético confiável, Proto=https também restaura Scheme sem redirect |
| D — Proto falso | Peer desconhecido com Proto=https não muda Scheme=http; Proto=http não rebaixa Scheme=https recebido diretamente |
| E — For falso | RemoteIpAddress permanece o peer desconhecido; XFF não é adotado |
| F — Health | /health e /HEALTH retornam 200; JSON contém status=ok. /health/ preserva a diferença atual: com middleware e destino, 307 antes do endpoint |
| G — JWT ausente/inválido | Sem redirect anterior, endpoint sintético protegido retorna 401 para token ausente, malformado, expirado, issuer/audience divergente ou outra assinatura |
| H — JWT válido | Endpoint protegido retorna 200 quando a requisição chega à autorização; com redirect anterior, permanece 307, sem executar o endpoint |
| I — Sem borda | Destino sintético 8443 preserva 307 e Location correto, incluindo caminho, PathBase e query. Comparação candidate-local-port/baseline-port aprovada |
| J — Proxy externo simulado | Com opção true, uma passagem HTTP interna termina em 200 sem Location. Controle com implementação anterior e destino configurado repete três vezes 307 para a mesma URL externa |

O controle J usa a URI sintética `https://api.example.test:8443/probe?synthetic=1`. A camada externa é modelada como HTTPS e repassa HTTP com Proto=https; sem confiança no peer, Scheme fica HTTP. O controle para após três repetições: demonstra o risco de ciclo na simulação, não mede um loop real do Render. O candidato termina na primeira passagem.

A primeira execução completa também passou, mas emitiu CS8604 no campo de captura de Host do harness. Foi ajustada somente a captura externa com fallback vazio; a execução final recompilou sem esse warning e passou novamente. Não houve correção do produto ou regressão funcional observada.

## 8. Geração de URLs e investigação de legado

Novos uploads em Production usam Storage habilitado pelo registro existente. SupabaseAvatarStorageService constrói URLs a partir da origem validada como HTTPS, bucket e caminho de propriedade do usuário. A geração de URL não realiza chamada HTTP.

AvatarStorageService usa Request.Scheme/Host/PathBase quando o caminho satisfaz IsLegacy ou quando é local e pertence ao usuário. IsLegacy reconhece nome hexadecimal de 32 caracteres com extensão jpg/png/webp; IsLocal usa prefixo local/. O ramo legado não está condicionado a Development. Ausência de caminho retorna null.

Os fluxos de cadastro, login e respostas de perfil/avatar chamam GetPublicUrl. A presença desses ramos prova suporte a legado, não a existência ou ausência de registros ativos. Sem dados operacionais adicionais, a pendência permanece.

Resultados dos métodos reais no harness, com PathBase=/synthetic e Host=api.example.test:

| Caminho/condição | Esquema recebido | Resultado |
| --- | --- | --- |
| Objeto Supabase válido | http ou https | `https://abcdefghijklmnopqrst.supabase.co/storage/v1/object/public/avatars/<caminho-sintético>` |
| Avatar ausente | http ou https | null |
| Legado válido | http | `http://api.example.test/synthetic/uploads/profiles/<nome-legado>` |
| Local pertencente ao usuário | http | `http://api.example.test/synthetic/uploads/profiles/local/<caminho-sintético>` |
| Legado/local | https | Mesmos caminhos com esquema https |

Esses resultados foram iguais no baseline e no candidato quando o endpoint foi alcançado. Nos perfis com redirect anterior ao endpoint, a geração de URL não é executada na passagem HTTP; os mesmos casos também foram exercitados com Scheme=https. Os headers X-Forwarded-Proto/Host forjados não corrigiram nem alteraram a origem desses resultados para peers desconhecidos.

O frontend armazena/consome avatarUrl como string e a associa a src de imagem no cabeçalho e no perfil. Não foi identificada transformação própria que garanta converter http em https. Navegadores podem atualizar automaticamente imagens mistas elegíveis ou bloquear conteúdo, conforme contexto e política; isso não é garantia do contrato da aplicação. [MDN: conteúdo misto](https://developer.mozilla.org/en-US/docs/Web/Security/Defenses/Mixed_content). Não foi executado teste de navegador nesta etapa.

**O experimento não corrige URLs HTTP locais/legadas.** Não foi implementada origem canônica, alteração de serviço ou migração. A existência/necessidade desse ramo ainda impede aprovação para produção sem avaliação e aceitação explícitas.

## 9. Segurança dos headers

Em todos os requests da matriz foi injetado X-Forwarded-Host=attacker.example.test; Host permaneceu api.example.test. Nos casos de spoofing, Proto e For também foram manipulados. A opção de redirect não habilita leitura desses headers nem força Scheme=https.

Dois perfis registraram exclusivamente o proxy documental 192.0.2.10 para exercitar o middleware real com lista restrita. O peer 198.51.100.10 continuou desconhecido nesses perfis e não alterou Scheme/IP por headers. O peer autorizado restaurou Scheme=https e IP=198.51.100.20 no controle correspondente. Assim, foi testado tanto middleware ausente quanto ativo com origem desconhecida.

Não se ativou ASPNETCORE_FORWARDEDHEADERS_ENABLED, não se ampliaram redes e não se alterou ForwardLimit=1. A política restritiva segue o código original. [Microsoft: proxy e forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Ignorar X-Forwarded-Host não valida automaticamente o header HTTP Host original. AllowedHosts efetivo e sanitização da borda continuam sendo responsabilidades a confirmar. Nenhum header forjado foi aceito como solução para URLs; o código candidato apenas decide se registra o middleware de redirect.

## 10. Comparação do comportamento atual e candidato

| Aspecto | Atual | Candidato com opção ausente/false | Candidato com opção true |
| --- | --- | --- | --- |
| Redirect fora de health | Middleware ativo | Igual | Não registrado |
| HTTP sem porta HTTPS | Prossegue e emite warning | Igual | Prossegue sem esse warning |
| HTTP com destino conhecido | 307 | Igual | Prossegue |
| HTTPS reconhecido | Sem redirect | Igual | Sem redirect |
| Confiança em headers | Condicional à lista existente | Igual | Igual |
| Scheme/IP internos | Não restaurados sem proxy confiável | Igual | Igual |
| JWT | Validado ao alcançar autenticação | Igual | Igual |
| Health | Exceção existente | Igual | Endpoint preservado |
| URLs locais/legadas | Dependem de Scheme/Host | Igual | Igual, sem correção |
| Enforcement público | Depende do ingresso/borda | Igual | Explicitamente delegado; exige comprovação operacional |

A equivalência padrão foi verificada sobre resultados observados, não apenas sobre o valor da opção. Production, Development e Staging não acionam delegação por si. Um true indevido em outra hospedagem também desativaria o middleware: esse é um risco de configuração a documentar e controlar.

## 11. Riscos

- Ingresso HTTP alternativo ou privado sem enforcement: true permite prosseguir sem redirect da aplicação. A opção não impõe TLS ou bloqueia tráfego por si.
- Ausência de evidência da borda efetiva: documentação pública não prova todos os domínios, proxies e caminhos de acesso do serviço.
- URLs legadas HTTP: permanecem demonstráveis no teste; não presumir que o navegador ou a borda as resolva corretamente.
- Configuração incorreta: booleano inválido falha no startup; true válido porém injustificado é um erro operacional que o código não consegue detectar.
- Outras hospedagens: padrão preservado, mas copiar indiscriminadamente a opção true para outro ambiente elimina seu redirect.
- Ausência de forwarded headers: mantém esquema/IP do transporte; pode afetar URLs e futuras políticas que dependam do cliente/esquema original. O limiter atual não depende de IP e JWT não foi alterado.
- Autenticação: JWT preservado não equivale a transporte seguro. A camada de ingresso precisa proteger credenciais e tokens; um redirect não desfaz envio inicial em HTTP.
- Health: HTTP 200 e JSON correto não certificam segurança, banco, Storage ou integração de proxy.

Eliminar o warning significa remover uma tentativa de redirect nesse perfil; não é prova de melhoria de segurança.

## 12. Limitações e evidências do experimento

O harness não executa a API completa, seus controllers reais, banco, Storage ou frontend. CORS, arquivos estáticos, IntegrityMiddleware e rate limiter completos não integram esta matriz; a ordem dos middlewares relevantes foi reproduzida. Os metadados de health foram preservados, mas não se certifica a política completa de rate limiting por esse teste dirigido.

Não houve TLS real, sockets de aplicação, testes públicos ou consulta de usuários. Os 308 resultados não substituem integração da aplicação completa ou inspeção autorizada da borda. O runtime Windows 10.0.9 não comprova equivalência binária com a imagem Linux publicada.

Arquivos Baseline.Program.cs.txt e Candidate.Program.cs.txt contêm cópias completas para revisão do diff mínimo; não são a API completa compilada/executada neste experimento. Os blocos selecionados e as seis cópias de serviços auxiliares são os que foram compilados no harness. Não foram adicionados comentários ao código experimental; comentários herdados das cópias originais foram preservados.

Artefatos e hashes da execução final são registrados ao final desta seção. Foram mantidos exclusivamente em TEMP, fora do repositório. Reprodução, enquanto disponíveis: `dotnet restore <diretório>/Harness.csproj --configfile <diretório>/NuGet.Config` e `dotnet run --project <diretório>/Harness.csproj --no-restore`, com SDK/pacotes locais compatíveis. O NuGet.Config não tem fontes remotas. results.json contém casos, asserções e resumo; run.log não contém JWTs.


Diretório externo desta execução: `C:\Users\GIOVAN~1.GRI\AppData\Local\Temp\seenit-b06-a4-226c20be1c474ed9b480fdae61bf1462`.

| Artefato temporário | SHA-256 |
| --- | --- |
| `Program.cs` do harness | `38D70078E8B3B6681025483F8528EB4371549055379200F841B802EA9A4EEB48` |
| `Candidate.Program.cs.txt` | `8B6F7C8A11FF36A72FE8CFECE0C9099E0E8FC2A6BE9F66427A418B6EFD6C2981` |
| `candidate.diff` | `BF03966D4D9468A0543CEA3EF50A1DF75FBE1380835636C73F444830FB65452F` |
| `source-blocks.json` | `9C50802F728BC056F66605028584E2B66B997901CBD803A2CE340C7FDB0C63BD` |
| `bin/Debug/net10.0/results.json` | `388845D8BBB1F4470E66E49AAF233AB7908582848AC413C7C04526D54D921530` |
| `run.log` | `6FD2284B01D63DA809D58A9E6234F1594490EB856088F2BF2A0EFF3EA865ED11` |

Conferência final: 232/232 arquivos preexistentes da árvore principal e 161/161 arquivos verificados de maintenance-phase1 preservados por SHA-256; HEADs e estados preexistentes mantidos. Somente este relatório foi criado no repositório. `git diff --check` e a verificação de whitespace do novo relatório não apontaram erros. Os três relatórios anteriores e todos os arquivos B05 existentes permaneceram intactos.
## 13. Rollback

Não existe alteração funcional a reverter agora. O repositório mantém seu comportamento anterior. Os artefatos temporários são independentes e nenhuma branch/worktree precisa ser removida.

Em aplicação futura autorizada, omitir a opção ou voltar a false restaura o comportamento anterior no próximo startup; remover o wrapper candidato também restaura o trecho original. Registrar antes a versão e configuração anteriores, sem segredos. Qualquer mudança remota/deploy para efetivar rollback exige a autorização correspondente; nada foi executado nesta etapa.

Verificar após reversão: health, status/Location, JWT e URLs. O warning anterior pode voltar e não é motivo para habilitar confiança global ou fixar 443. Se houver porta HTTPS explícita junto de Scheme incorreto, restaurar redirect pode reintroduzir ciclo; considerar o estado completo, não só a opção isolada.

## 14. Decisão técnica

**APROVADO APENAS LOCALMENTE.** Os testes passaram, a alteração proposta é mínima, o padrão e os controles de autenticação/headers ensaiados foram preservados, e o perfil de borda encerrou a simulação sem loop. Nenhuma regressão foi observada no escopo testado.

**Não aprovado para aplicação permanente nesta etapa.** Continuam pendentes a cobertura efetiva dos ingressos, a identidade do artefato e a avaliação dos registros locais/legados. A dependência de Scheme nesses URLs não foi resolvida nem aceita operacionalmente. A ausência de falhas nos testes não comprova a topologia do Render.

Nenhuma alteração foi feita em Program.cs, Dockerfile, serviços, JWT, frontend, dependências, migrations ou variáveis remotas. Sem deploy, commit, push ou PR. Data Protection permanece fora da correção e sob o diagnóstico B06-A1.

## 15. Recomendação para B06-A5

Obter o conjunto sanitizado da seção 4, sem segredos nem dados pessoais. Documentar quem impõe HTTPS em cada ingresso e confirmar a necessidade funcional do Scheme público. Para legado, obter evidência agregada de ausência, ou avaliar e aceitar explicitamente o impacto e a solução em etapa própria; não usar o sucesso deste experimento para declarar o risco eliminado.

Somente após fechar essas condições e receber autorização específica, preparar a menor implementação e os testes integrados apropriados, mantendo a opção ausente/false por padrão, a confiança restritiva e o rollback. Se as evidências continuarem insuficientes, manter o código atual. B06-A5 não está autorizado por este relatório a acessar produção, mudar configurações ou fazer deploy.

**B06-A4 EXPERIMENTO PARCIAL**
