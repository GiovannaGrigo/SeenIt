# B06-A3 — Revisão operacional de HTTPS e proxy no Render

Data: 19/11/2026. Etapa exclusivamente diagnóstica. Nenhuma alteração funcional ou remota executada.

## 1. Conclusão e decisão técnica

As novas evidências tornam provável uma causa concreta para o warning: a aplicação recebe HTTP interno do Render, não ativa seu middleware de forwarded headers por falta de proxies configurados e executa UseHttpsRedirection sem destino HTTPS disponível. Esse conjunto explica o aviso sem demonstrar ausência de TLS público, bypass de autenticação ou exploração.

A classificação principal é **operacional**, com **impacto funcional potencial na geração de URLs de avatares locais/legados**. O warning de precedência de HTTP_PORTS por URLs é informativo e explicado pelo Dockerfile/código. Não se deve classificar todo o problema como meramente cosmético, nem afirmar vulnerabilidade pela ausência de KnownProxies.

**Menor ação segura agora: preservar código e configurações.** Não há peer autorizado comprovado para cadastrar, nem inventário suficiente dos ingressos e avatares legados para aprovar uma mudança funcional. Para B06-A4, a alternativa preferencial de simplificação é condicionar o redirect ao modelo de hospedagem com enforcement na borda, após confirmar seus critérios. Ela não depende de descobrir IPs de proxy se o aplicativo não precisar restaurar o esquema original. Sua adoção não corrigiria, sozinha, URLs legadas HTTP.

## 2. Estado do Git e preservação

- Branch confirmada: `security/b06-a1-hardening-diagnosis`.
- HEAD: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`.
- Estado inicial: apenas B06-A1 e B06-A2 não rastreados; nenhum arquivo rastreado modificado na árvore principal.
- `maintenance-phase1`: referência e worktree em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com alterações preexistentes em seis arquivos e seis entradas não rastreadas. Preservar significa manter esse conteúdo e estado; não se declara essa worktree limpa.
- Capturados hashes SHA-256 de 231 arquivos preexistentes da árvore principal, incluindo B06-A1, B06-A2 e B05, e 161 arquivos rastreados/não ignorados da worktree de manutenção. A verificação final compara também HEADs e status.
- Nenhuma criação/exclusão de branch ou worktree. Não houve fetch, commit, push, PR, deploy ou alteração de histórico nesta etapa.

O caminho solicitado com `\.md` foi interpretado como a extensão normal `.md`, sem criar diretório adicional.

## 3. Evidências fornecidas pelo operador

O operador consultou diretamente a seção Environment do serviço no Render e informou a presença de:

`AllowedHosts`, `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`, `Cors__AllowedOrigins__0`, `Jwt__Audience`, `Jwt__ExpirationMinutes`, `Jwt__Issuer`, `Jwt__Key`, `SupabaseStorage__SecretKey` e `SupabaseStorage__Url`.

Informou também ausência, nessa seção, de:

- `ReverseProxy__KnownProxies__0` e demais índices;
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED`;
- `PORT`;
- `ASPNETCORE_URLS`;
- `ASPNETCORE_HTTPS_PORT`.

São evidências declaradas pelo operador sobre o painel, incorporadas sem acesso independente à produção. Não foram fornecidos, solicitados ou lidos valores de segredos. Presença de uma variável não comprova seu valor ou validade. Ausência no painel não comprova ausência no processo: a imagem e a plataforma também fornecem ambiente.

B06-A2 continua sendo a referência dos 29/29 testes isolados aprovados. Esses testes não foram repetidos nem convertidos em validação da infraestrutura real.

## 4. Configuração efetiva inferível

As inferências abaixo pressupõem que o serviço execute a imagem construída pelo Dockerfile versionado, sem outros arquivos, argumentos ou provedores de configuração não informados. A identidade do artefato remoto não foi inspecionada.

| Elemento | Fonte no repositório e evidência nova | Comportamento inferível |
| --- | --- | --- |
| Proxies conhecidos | `Program.cs:71–95`; nenhuma variável no painel; JSON sanitizado não define a seção | Array vazio pela expressão de fallback `?? []` |
| Ativação do middleware | `Program.cs:162`: somente se `proxyAddresses.Length > 0` | A aplicação não chama UseForwardedHeaders |
| Atalho automático | Ausente no painel e sem definição no Dockerfile; código rejeita valor true | Nenhuma ativação ampla é indicada pelas fontes examinadas |
| Porta | Dockerfile:37–40 define `PORT=10000` e `ASPNETCORE_HTTP_PORTS=10000` | Processo possui PORT mesmo sem cadastro manual no painel |
| Binding | `Program.cs:22–28` lê PORT diretamente e chama UseUrls | Esperado `http://0.0.0.0:10000`, salvo override de plataforma |
| Ambiente | Dockerfile define também `DOTNET_ENVIRONMENT=Production` | Production esperado, sob as mesmas premissas; o nome ASPNETCORE_ENVIRONMENT no painel sozinho não comprova o valor |
| Porta HTTPS de redirect | Sem configuração no código/JSON sanitizado; variável ausente no painel | Nenhum destino explícito conhecido |
| Listener TLS | Nenhum endpoint TLS explícito no código ou imagem examinados | HTTP interno é o modelo esperado |
| Redirecionamento | UseWhen fora de `/health`, sem distinção de caixa | Continua habilitado para outros caminhos, independentemente do ambiente |
| Host/CORS | Nomes presentes no painel | Valores e adequação não verificados; não restauram Scheme nem autorizam proxies |

O build Docker copia `backend/docker/runtime-settings.json` para `/out/api/appsettings.json`; esse arquivo contém apenas TVmaze e logging. Portanto, nem o appsettings local nem sua seção vazia devem ser confundidos com o JSON efetivamente publicado. Em ambos os casos examinados, sem override de proxies, o resultado é lista vazia. `.env.render.example` tem placeholders públicos e não é carregado automaticamente. `launchSettings.json` é configuração de execução local, não habilita HTTPS no container.

Quando a lista está vazia, não se executa o bloco que limpa e repovoa as opções de confiança, nem a chamada da aplicação ao middleware. Isso **não** significa middleware ativo aceitando todos os proxies. X-Forwarded-Proto e X-Forwarded-For não alteram Scheme/IP por esse mecanismo. Se ativado futuramente, o bloco mantém apenas IPs explícitos, remove redes padrão, aceita For/Proto, ignora Forwarded-Host e limita a um salto.

### Por que a porta 10000 funciona sem cadastro no painel

O `ENV PORT=10000` da imagem já fornece a variável ao processo; o Render pode fornecer seu próprio valor. O programa usa o valor efetivo, valida 1–65535 e faz binding HTTP em todas as interfaces. `EXPOSE 10000` é apenas metadado e não é a causa do listener. A plataforma documenta 10000 como porta padrão de Web Services. [Variáveis padrão Render](https://render.com/docs/environment-variables), [port binding](https://render.com/docs/web-services#port-binding).

`UseUrls` tem precedência sobre HTTP_PORTS; ambos indicarem 10000 não elimina o log de precedência. Não é necessário criar ASPNETCORE_URLS no painel para que o programa configure URLs. [Kestrel: prioridades de configuração](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0).

## 5. HTTPS esperado e causa provável dos warnings

O Render documenta terminação TLS na borda, HTTP até o processo e encaminhamento do esquema original por X-Forwarded-Proto; documenta também redirect HTTP público → HTTPS. [Tutorial oficial Render](https://render.com/tutorials/web-service-vs-static-site/web-services), [TLS gerenciado](https://render.com/docs/tls).

```text
HTTPS público → borda Render [TLS] → HTTP interno → Kestrel
                                                → forwarded headers desativado
                                                → Request.Scheme = http esperado
                                                → redirect fora de /health
                                                → sem porta HTTPS: warning e continuidade
                                                → autenticação/autorização/endpoint
HTTP público → redirect na borda → HTTPS público
```

Esse fluxo distingue transporte recebido pelo Kestrel do transporte público. Scheme=http no backend não comprova que o cliente usou HTTP. RemoteIpAddress tende a ser o peer imediato, não o cliente original. Os valores concretos ainda não foram medidos nesta implantação.

O warning `Failed to determine the https port for redirect` é compatível com uma requisição HTTP interna fora de `/health` sem porta HTTPS descobrível. Nessa condição, o middleware não redireciona e permite prosseguir. Não bloqueia HTTP por si e não garante o enforcement de ingressos alternativos. Sem correlação de caminho/peer/horário, não é possível atribuir o evento histórico a uma requisição específica. A ausência de porta também explica por que não há necessariamente loop apesar de Scheme incorreto. [Microsoft: comportamento sem porta e proxy](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0).

Configurar 443 isoladamente mudaria esse comportamento: o HTTPS público voltaria a chegar como HTTP interno e poderia receber redirect para a própria URL pública repetidamente. Essa não é uma correção recomendada. Não se recomenda certificado ou listener TLS interno para resolver este aviso.

## 6. Necessidade real dos forwarded headers e impacto funcional

| Funcionalidade atual | Precisa do esquema/IP original? | Evidência e limite |
| --- | --- | --- |
| Emissão/validação JWT | Não para as operações examinadas | Chave, assinatura, issuer, audience e lifetime locais; não usa IP nem Scheme para validar token |
| Autorização das rotas protegidas | Não para a política atual examinada | Identidade JWT e RequireAuthorization; não depende de XFF |
| Rate limiting | Não precisa do IP original | Partições públicas, de usuário e do servidor em `RateLimiting/AbuseProtection.cs`; sem particionamento por RemoteIpAddress |
| Catálogo/biblioteca | Nenhuma dependência direta identificada | Não foi encontrado uso do Scheme/IP original nesses serviços |
| Novos uploads de avatar em Production | URL não depende de Request.Scheme | `AvatarServiceRegistration.cs` força Storage habilitado em Production; URL configurada é validada como HTTPS |
| Avatar armazenado no Supabase | Não para construir a URL pública | `SupabaseAvatarStorageService.cs:16–19` usa a origem configurada e o caminho do objeto; sem Scheme/Host do request |
| Sem avatar | Não | Retorno null |
| Avatar legado ou local reconhecido | Precisa de uma origem pública correta, não necessariamente do header | `AvatarStorageService.cs:54–59` usa Request.Scheme, Host e PathBase; ramo legado permanece mesmo em Production |
| Redirect da própria aplicação | Precisa reconhecer HTTPS original quando atrás de TLS termination | Caso seja mantido com destino explícito, Scheme correto evita redirect indevido |

O código de geração de URL legado é alcançado por respostas de cadastro/login/perfil, conforme o caminho de avatar disponível. Exemplo puramente sintético: sob HTTP interno, um caminho legado válido pode gerar `http://api.example.test/uploads/profiles/<arquivo>`. O frontend associa avatarUrl a src de imagem; URLs HTTP podem causar incompatibilidade de conteúdo misto ou falha de carregamento em páginas HTTPS. Não há confirmação de usuários/arquivos legados existentes, resposta pública HTTP observada ou incidente. Não foram acessados banco, Storage ou usuários reais.

Assim, **X-Forwarded-For não é requisito funcional identificado hoje**. X-Forwarded-Proto é útil para restaurar a semântica do request e necessário para o modelo atual de URLs legadas, caso existam, ou para redirect interno com destino configurado. Não é requisito dos fluxos JWT e das URLs novas de Storage por si só. Nenhuma necessidade identificada justifica aceitar cabeçalhos de todas as origens.

Uma origem pública canônica HTTPS validada seria alternativa futura para URLs legadas sem depender do Scheme recebido. Isso exigiria contrato/configuração e alteração própria; não existe essa opção no código atual. Forçar Scheme globalmente a https, copiar header arbitrário ou tornar URLs relativas sem avaliar o frontend de outra origem não são soluções mínimas seguras.

## 7. Fronteira de confiança verificável

Para restaurar headers com segurança, é preciso obter de fontes operacionais autorizadas:

1. Identidade do peer imediato que realmente abre a conexão com Kestrel, incluindo família IP e representação IPv4 mapeada.
2. Confirmação do fornecedor de que esse peer é controlado/autorizado, com estabilidade ou inventário de alternância e exclusividade de uso apropriada.
3. Contrato sobre sanitização e ordem de XFF/Proto: remoção, sobrescrita ou append, valores provenientes do cliente e número de saltos até o dado original.
4. Cobertura dos ingressos público, privado, health e eventuais proxies extras; caminhos que possam contornar a borda.

Uma observação pontual de IP em log ajuda a identificar o peer, mas não comprova estabilidade, propriedade ou sanitização. DNS público, ranges de saída do serviço, qualquer IP RFC1918 ou todos os endereços internos não substituem esse contrato. Cadastrar vários peers não muda ForwardLimit=1 nem resolve uma cadeia que exija mais saltos.

A Microsoft orienta verificar os peers conhecidos e limitar o processamento. [Forwarded headers .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0). As fontes Render consultadas não estabeleceram uma lista estável e exclusiva aplicável a esta implantação; não se conclui que tal garantia inexista, apenas que não foi obtida.

Se não houver garantia de confiança adequada, manter o processamento desativado é preferível a inventar endereços. Caso uma necessidade funcional seja comprovada, avaliar com o fornecedor uma integração com fronteira controlada e isolamento de rede verificável. Isso seria outro desenho, não autorização para adicionar proxy ou infraestrutura nesta fase.

Não é necessário conhecer o IP do proxy para decidir delegar redirect à borda, desde que seus ingressos/enforcement sejam comprovados. Os requisitos de confiança dos headers e de responsabilidade pelo redirect são relacionados, mas distintos.

## 8. Matriz de alternativas

| Alternativa | Resolve | Mantém pendente / riscos | Pré-condição e menor escopo futuro | Decisão |
| --- | --- | --- | --- | --- |
| A. Manter configuração | Preserva comportamento e rejeita processamento não autorizado | Warning persiste; Scheme segue HTTP; legado pode produzir URL HTTP | Sem alteração; registrar limites e conferir existência de dependências afetadas | Recomendada agora, sem afirmar segurança integral da implantação |
| B. Cadastrar KnownProxies comprovados | Permite restaurar Scheme/IP pelo mecanismo existente | IP instável/incorreto ou proxy sem sanitização introduz risco; um salto pode ser insuficiente; redirect em HTTP interno legítimo ainda pode avisar | Garantias da seção 7; configuração operacional restrita, sem mudança de código se topologia compatível | Não aprovada com evidências atuais; não necessária só para IP original |
| C. Condicionar UseHttpsRedirection ao modelo de hospedagem | Remove tentativa redundante de redirect no perfil com TLS/redirect na borda | Não corrige Scheme nem URLs legadas; um ingresso sem enforcement pode permanecer HTTP | Confirmar ingressos, artefato e cobertura da borda; menor alteração futura em Program.cs e documentação/configuração do perfil | Candidata preferencial para B06-A4 se critérios atendidos |
| D. Origem pública canônica para URLs legadas, se necessárias | Permite URLs HTTPS estáveis sem confiar em headers | Nova configuração/validação e manutenção de domínio; não restaura esquema/IP para outras necessidades | Confirmar uso legado e preservar contrato de URL absoluta; alteração localizada no serviço de avatar e configuração | Avaliar apenas com necessidade funcional demonstrada; não implementar nesta etapa |

### Pode condicionar o redirect ao ambiente?

Sim, **como consequência de um contrato de hospedagem**, não apenas do rótulo Production. A Microsoft admite dispensar o middleware quando o proxy já cuida de HTTPS e redirect. [Microsoft: HTTPS atrás de proxy](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0). Isso é compatível com a responsabilidade pública documentada pelo Render.

Um simples `!IsProduction()` desativaria o middleware também em outra hospedagem Production; `IsDevelopment()` restringiria arbitrariamente o comportamento a Development. Nenhum desses rótulos comprova TLS na borda. Preferir um perfil operacional explícito ou opção com redirect mantido por padrão, cuja desativação exija declarar enforcement externo. O nome e a implementação exatos deverão ser definidos em B06-A4, se autorizado; nenhuma variável desse tipo é lida hoje.

Se o projeto estabelecer formalmente que todo Production corresponde exclusivamente ao ingresso padrão do Render, uma condição por ambiente pode ser suficiente para esse contrato restrito, desde que se documente a perda de portabilidade e se teste a saída desse modelo. Esse contrato ainda não foi fornecido.

Não se recomenda remover o middleware globalmente, habilitar confiança ampla, usar 0.0.0.0/0, suprimir o log ou fixar 443 como atalho. Delegar redirect à borda não dispensa tratar uma necessidade real de URLs corretas.

## 9. Correção mínima recomendada e evidências restantes

**Nesta revisão: nenhuma alteração funcional.** O diagnóstico do warning está suficientemente explicado para classificá-lo e preparar uma decisão; a liberação de uma correção exige fechar apenas as pendências pertinentes à alternativa escolhida.

Para considerar C em B06-A4, obter confirmação sanitizada de:

- commit/digest do artefato executado e correspondência ao Dockerfile/código analisados;
- domínios e ingressos utilizados, incluindo tráfego privado, e cobertura do enforcement HTTP → HTTPS na borda;
- ausência de requisito de redirect no próprio backend nesses ingressos, ou tratamento definido para as exceções;
- existência ou inexistência de avatares locais/legados e outras dependências do esquema original, mediante informação agregada do operador, sem dados de usuários ou acesso a banco nesta etapa.

Se a última pendência comprovar necessidade de Scheme original, escolher B somente depois das garantias da seção 7; se não forem obtidas, comparar D para o caso específico de URLs. Remover redirect não fecha essa necessidade. Se não houver legado e todos os demais fluxos continuarem independentes de Scheme/IP, é justificável manter forwarded headers desativado e delegar o redirect ao Render no perfil confirmado.

Não exigir novos testes públicos para esta etapa nem solicitar tokens, connection strings, cookies ou chaves. Correlação sanitizada do warning por horário/caminho seria útil para confirmar a causa histórica, mas não é pré-condição para reconhecer que a configuração inferida produz esse comportamento.

## 10. Testes necessários e critérios de aceitação de B06-A4

Nenhum novo teste de execução foi necessário para este relatório documental: reaproveitados os resultados B06-A2 e feita revisão estática dirigida. Não se executou a API nem se acessou produção.

Se C for escolhida futuramente:

- Perfil com borda: HTTP interno não gera redirect da aplicação nem warning de porta; headers forjados continuam sem alterar Scheme/IP; JWT inválido/ausente retorna 401 em endpoint protegido.
- Perfil sem borda: comportamento de redirect preservado com porta HTTPS sintética configurada; HTTPS reconhecido não redireciona. Não mudar o padrão apenas para eliminar warning.
- Health mantém contrato anônimo/HTTP interno, inclusive sua exceção de caminho já existente.
- Login, cadastro, perfil, catálogo e biblioteca preservam contratos em testes isolados; nenhum acesso externo real.
- URLs: null sem avatar; HTTPS do Storage independente do Scheme; ramo legado testado explicitamente e resolvido ou mantido como limitação aceita, sem alegar que C o corrigiu.
- Proxy sintético com terminação TLS representa passagem HTTPS externo → HTTP interno e repetição de requisições, demonstrando ausência de loops.

Se B for escolhida, repetir a matriz A–G, IPv4/IPv6, peers conhecidos/desconhecidos, Proto sem For, cabeçalhos adulterados e múltiplos valores de acordo com a topologia comprovada. Não elevar ForwardLimit sem mapear e validar a cadeia. Se D for escolhida, validar origem canônica HTTPS, rejeição de origem inválida e independência de Host/Proto forjados, preservando o contrato do frontend.

A aceitação de qualquer mudança inclui ausência de confiança global, autenticação preservada e responsabilidade documentada por todos os ingressos. Verificação real da borda, se necessária, depende de autorização futura e não é coberta por testes locais.

## 11. Estratégia de rollback

Nenhuma correção foi aplicada; não há rollback operacional a executar agora.

Para uma futura C: restaurar a condição anterior de UseHttpsRedirection e o perfil/configuração anteriores; não acrescentar destino 443 durante o rollback. Revalidar health, disponibilidade, ausência de loops e JWT. A volta do warning pode ser esperada e não autoriza ampliar confiança.

Para uma futura B: restaurar exatamente a lista anterior (no cenário informado, ausente) e revalidar o comportamento documentado de Scheme/URLs. Não remover restrições de confiança como mitigação. Para D: restaurar geração/configuração anteriores e aceitar explicitamente os efeitos conhecidos sobre URLs legadas até solução posterior.

Antes de qualquer implantação futura autorizada, registrar versão e configurações anteriores sem segredos e definir critérios de reversão: loop, degradação de health, falha de autenticação, esquema/URL inesperados ou aceitação de header de origem não autorizada. Nenhum comando de reset, clean, rebase ou force é necessário para esse plano.

## 12. Recomendação para B06-A4 e limitações finais

B06-A4 deverá fechar os critérios da seção 9 e apresentar uma mudança concreta mínima somente se justificada e autorizada. A ordem preferida é: decidir se a aplicação realmente precisa do esquema público; formalizar enforcement na borda; então preparar C, ou tratar a dependência funcional por B/D sem confiança arbitrária. Caso continuem faltando evidências essenciais, manter a implementação e registrar o impedimento específico.

B06-A1 permanece referência separada para Data Protection. Nenhuma nova evidência de consumidor foi estabelecida nesta revisão; não se propõe persistência de chaves e não se mistura key ring com JWT/proxy.

Fontes oficiais foram consultadas em 19/11/2026. A documentação descreve a plataforma, não comprova a configuração desta implantação. O relato do operador reduz a incerteza sobre as variáveis cadastradas, mas não comprova ambiente completo do processo, artefato, ingressos ou headers efetivos. Não foram acessados serviços de produção, Render/Vercel/Supabase, painéis ou armazenamento, nem solicitados segredos.

Verificações locais: branch, status, HEADs/worktrees; leitura de Program.cs, Dockerfile, JSON sanitizado e local, exemplos/checklist Render, geração/validação de URLs de avatar, TokenService, rate limiter, usos de Scheme/IP/headers e consumo de avatarUrl no frontend. B06-A1/B06-A2 não foram editados. Conferência final SHA-256: 231/231 arquivos preexistentes da árvore principal e 161/161 arquivos verificados de maintenance-phase1 preservados; HEADs e status preexistentes preservados. A única criação desta etapa é este relatório.

**B06-A3 REVISÃO CONCLUÍDA** — decisão diagnóstica concluída; alteração e validação da infraestrutura continuam condicionadas às evidências e à autorização da próxima etapa.
