# B06-A1 — Diagnóstico de Data Protection, HTTPS e proxy reverso

Data: 18/11/2026. Escopo exclusivamente diagnóstico; nenhuma alternativa implementada.

## 1. Objetivo

Investigar os quatro warnings relatados pelo operador, distinguir segurança, operação e informação, e preparar B06-A2 sem alterar código, configuração ou infraestrutura.

## 2. Contexto

SeenIt: Angular 21 na Vercel, API ASP.NET Core .NET 10 em Docker no Render, PostgreSQL 17 no Supabase e autenticação JWT Bearer. Segundo o operador, deploy B05 concluído e login, cadastro, perfil, upload/substituição de avatar, catálogo e biblioteca aprovados. Isso é evidência funcional fornecida, sem reprodução nesta etapa.

Os quatro textos de warning são evidências fornecidas, sem timestamps, correlação por requisição ou logs completos. Não demonstram, por si, exploração, indisponibilidade ou tráfego público sem TLS.

## 3. Ambiente analisado

- Estado inicial: `main`, árvore limpa (`git status --short --branch`: `## main...origin/main`).
- `git fetch origin` concluído; primeira tentativa bloqueada pelo sandbox em `.git/FETCH_HEAD`, repetida com autorização.
- `HEAD` e `origin/main`: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`; divergência `0 0` após fetch.
- Branch criada: `security/b06-a1-hardening-diagnosis`, no mesmo SHA.
- `main`, branches B05 e demais branches preservadas. Worktree `maintenance-phase1` preservada em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`.
- Projeto `net10.0`; Dockerfile usa imagens SDK/ASP.NET `10.0-noble` fixadas por digest. Versões 10.0.404/10.0.15 são informadas pelo contexto B05; imagens não foram construídas ou executadas para reconfirmá-las.
- O build Docker substitui `appsettings.json` pelo sanitizado `backend/docker/runtime-settings.json`, contendo apenas TVmaze e logging. Portanto, não se deve tratar o appsettings local como configuração efetiva de produção.

Não foram consultados painéis, APIs ou armazenamento de Render, Vercel ou Supabase. Consultou-se apenas documentação pública oficial dos fornecedores, sem chamadas ao serviço de produção.

## 4. Pipeline ASP.NET Core

Evidência: `backend/SeenIt.Api/Program.cs:159–176` (referências ao SHA analisado). Ordem de entrada:

1. `UseForwardedHeaders`, somente se houver endereços em `ReverseProxy:KnownProxies`.
2. `UseWhen`: executa `UseHttpsRedirection` quando o caminho não é `/health`, comparação sem distinção de caixa. A branch retorna à pipeline se não finalizar a resposta.
3. `UseStaticFiles` — pode atender arquivos e encerrar antes das etapas seguintes.
4. `IntegrityMiddleware` — envolve o restante e traduz exceções específicas; ver `Services/DataIntegrity.cs:52`.
5. `UseRouting` — seleciona endpoint e seus metadados.
6. `UseCors("Frontend")` — pode encerrar preflight.
7. `UseAuthentication` — estabelece identidade JWT.
8. `UseRateLimiter` — utiliza endpoint e identidade já disponíveis.
9. `UseAuthorization`.
10. Execução do endpoint selecionado: controllers ou Minimal API. `/health` é anônimo e tem rate limiting desabilitado.

`MapControllers`, `MapGet`, grupos e `MapOpenApi` registram endpoints, não representam middlewares executados na posição textual do mapeamento. OpenAPI só é mapeado em Development. Com as chamadas explícitas de routing, autenticação e autorização, o modelo WebApplication não implica uma segunda execução automática dessas mesmas etapas.

Não há duplicação explícita de HTTPS redirection ou forwarded headers. A ordem forwarded headers → redirection é adequada quando o primeiro está ativo e confia no peer real. Há redundância de configuração de binding, descrita abaixo. CORS precede autenticação/autorização e limiter precede autorização; são escolhas existentes, sem alteração nesta etapa. Arquivos estáticos ficam fora dessas proteções posteriores, fato contextual, sem nova classificação de vulnerabilidade neste diagnóstico.

## 5. Configuração de portas

Evidências: `Program.cs:22–28`, `Dockerfile:37–46,83` e runtime-settings sanitizado.

| Entrada | Estado no repositório | Consequência |
| --- | --- | --- |
| `PORT` | Docker define `10000`; programa valida inteiro de 1 a 65535 | Se presente, `UseUrls("http://0.0.0.0:{PORT}")` |
| `ASPNETCORE_HTTP_PORTS` | Docker define `10000` | Configuração de menor prioridade que URLs |
| `ASPNETCORE_URLS` | Sem definição explícita no Dockerfile | `UseUrls` define a configuração de URLs sem exigir essa variável |
| `HTTPS_PORTS` | Nenhuma porta HTTPS definida pela aplicação/imagem descrita | Warning informa valor vazio naquele processo |
| `EXPOSE 10000` | Metadado Docker | Não cria listener nem publica porta por si |
| Kestrel | Sem `Listen`, `ConfigureKestrel` ou endpoint TLS explícito encontrado | HTTP interno determinado por URLs |

O warning 3 é compatível com a própria imagem: `PORT` aciona `UseUrls`, que vence `HTTP_PORTS`, mesmo quando ambos indicam 10000. A palavra `URLS` no log não prova que alguém cadastrou `ASPNETCORE_URLS` no Render. Não há evidência de duas portas concorrentes. Configurações remotas adicionais não foram inspecionadas.

O Render recomenda bind em `0.0.0.0:$PORT`; portanto o binding atual é coerente com a plataforma. Não é necessário alterar porta para corrigir TLS externo. [Kestrel .NET 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0), [Render Web Services](https://render.com/docs/web-services).

## 6. Diagnóstico de HTTPS

**Código comprovado:** apenas HTTP interno configurado; redirecionamento condicional para todos os caminhos exceto `/health`; sem `AddHttpsRedirection` com porta explícita e sem `UseHsts`.

**Plataforma esperada:** o balanceador Render termina TLS público, encaminha HTTP interno e redireciona HTTP público para HTTPS. HTTP no Kestrel é compatível com essa arquitetura e não prova transporte público inseguro. [Documentação Render](https://render.com/docs/web-services), [TLS gerenciado](https://render.com/docs/tls).

**Interpretação do warning 4:** houve uma tentativa de determinar porta HTTPS sem sucesso. Nessa condição o middleware deixa a requisição seguir sem redirecionar. Isso é compatível com uma requisição vista como HTTP fora de `/health`, sem porta de destino disponível; pode envolver esquema não restaurado ou acesso HTTP interno legítimo. O log isolado não identifica caminho, peer ou origem pública. [HTTPS no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0).

**Produção ainda não confirmada:** esquema e IP vistos em cada caminho, configuração efetiva dos proxies, headers recebidos, acesso interno, política HSTS na borda e eventuais redirects. Não há evidência suficiente para afirmar que todas as requisições HTTPS estão corretamente reconhecidas, nem que todas estão incorretas.

Se a aplicação não reconhecer HTTPS e alguém apenas fixar destino 443, cada passagem externa HTTPS → HTTP interno poderá gerar novo redirect para a mesma URL: loop potencial. O warning atual de porta ausente não demonstra esse loop; a ausência de redirecionamento explica a compatibilidade com os testes funcionais aprovados, sem comprovar a causa específica.

`RequireHttpsMetadata` não é alterado; o padrão é true. Essa opção exige HTTPS para obtenção de metadados Authority/MetadataAddress, não obriga HTTPS nas chamadas recebidas. SeenIt configura issuer, audience e chave simétrica locais, sem Authority/MetadataAddress; não corrige este warning nem depende de discovery remoto. [API oficial](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions.requirehttpsmetadata?view=aspnetcore-10.0).

Sem cookies de autenticação identificados, não há política Secure de cookie de login a ajustar. HSTS da aplicação está ausente; presença na resposta pública não foi medida. HSTS é política de navegador e não substitui proteção da primeira requisição ou configuração segura da borda.

## 7. Configuração de proxy reverso

Evidência: `Program.cs:67–95,162`, checklist e `RENDER.md:199–201`.

| Aspecto | Comportamento comprovado |
| --- | --- |
| Ativação | Somente com array de proxies não vazio; local tem array vazio e runtime-settings não fornece esse array |
| Headers | Apenas `X-Forwarded-For` e `X-Forwarded-Proto` |
| Confiança | IPs explícitos; rejeita Any IPv4/IPv6 e não aceita CIDR |
| Redes | Limpa `KnownIPNetworks`, nome utilizado no .NET 10; não configura a antiga `KnownNetworks` |
| Proxies | Limpa defaults de `KnownProxies`; inclui IP informado e IPv4 mapeado para IPv6 |
| Saltos | `ForwardLimit=1`; não representa confiança irrestrita em cadeia multinível |
| Host | Não habilita `X-Forwarded-Host` |
| Atalho automático | Rejeita `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` no startup |

Quando ativo e o peer imediato é confiável, o middleware pode restaurar `Request.Scheme` a partir de Proto e `RemoteIpAddress` a partir de For. Array vazio significa que a aplicação não chama esse middleware; peer fora da lista impede aceitar seus forwarded headers. Não usar IP de saída ou IP público de DNS como substituto do peer imediato. Identificar topologia e controle de sobrescrita/remoção de headers na borda antes de decidir confiança. [Proxy no ASP.NET Core .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Os nomes e valores efetivamente enviados pelo Render para esta implantação, estabilidade do peer e quantidade de saltos continuam pendentes; não foram inferidos como configuração comprovada. Não se recomenda limpar listas para confiar em todos, nem usar `0.0.0.0/0` ou copiar Proto arbitrariamente para Scheme.

Impacto específico: `AvatarStorageService.cs:54–59` gera URLs de avatares locais/legados com Scheme/Host/PathBase. Esquema incorreto pode gerar URL HTTP e problemas de mixed content. Avatares novos usam URL pública do storage. `AbuseProtection.cs` usa partições públicas, de usuário e servidor, sem particionar por IP; não se deve afirmar que XFF errado quebra um limiter por IP inexistente.

## 8. Diagnóstico de Data Protection

O caminho relatado é `/home/app/.aspnet/DataProtection-Keys`. Docker define HOME, cria o diretório e aplica `chown -R app:app /home/app/.aspnet`, executando depois como `USER app`. Não há volume persistente ou configuração explícita de repositório/encryptor/ApplicationName no código. Ausência de volume no Dockerfile não exclui um mount externo no serviço.

O relatório anterior `B05-B4-CONTAINER-SBOM-ANALYSIS.md:159` registra `/home/app` 0750 e subdiretórios 0755, app:app. Isso é evidência histórica da imagem auditada, não medição atual de produção. O Dockerfile não fixa explicitamente o modo de DataProtection-Keys; modos finais de arquivos XML, mounts e ACLs não foram verificados. Recomenda-se acesso mínimo ao usuário da aplicação em eventual implementação.

Não existe `AddDataProtection` explícito no SeenIt, mas `AddAuthentication(...)` registra Data Protection indiretamente no framework. Isso explica a existência dos serviços sem exigir cookies no produto; não prova qual resolução específica inicializou o key ring remoto. [Código oficial ASP.NET Core v10.0.15](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.15/src/Security/Authentication/Core/src/AuthenticationServiceCollectionExtensions.cs).

O warning 1 alerta que armazenamento local no container pode desaparecer. O 2 indica ausência de encryptor XML na persistência da chave naquele evento. Não significa que tokens JWT estejam salvos em texto simples, nem prova ausência de criptografia física no host. Persistência e proteção criptográfica são decisões separadas.

No filesystem efêmero padrão do Render, redeploy/restart perde alterações locais; no simples reinício de processo dentro de um filesystem preservado, o resultado pode diferir. Discos persistentes só preservam seu mount e não são compartilhados entre múltiplas instâncias. Nenhum mount ou número de instâncias desta implantação foi confirmado. [Render Persistent Disks](https://render.com/docs/disks).

Instâncias com rings distintos não decifram payloads protegidos umas pelas outras. Perda definitiva de chaves impede recuperar esses payloads; expiração/rotação normal de chave não equivale a excluí-la. Os defaults dependem do ambiente. [Ciclo de chaves oficial](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0).

## 9. Recursos dependentes de chaves

Inspeção estática de código de produção, registros DI, controllers e frontend:

| Recurso | Uso encontrado e impacto da perda do ring |
| --- | --- |
| JWT Bearer | `TokenService` cria HS256 com bytes UTF-8 de `Jwt:Key`; validação usa a mesma configuração. Não usa Data Protection. Perda do ring isoladamente não invalida JWT; troca da chave JWT ou expiração pode invalidar, por motivo separado |
| Senhas | `IPasswordHasher<User>/PasswordHasher<User>`; sem dependência do ring encontrada |
| Cookies de autenticação | Sem AddCookie/emissão encontrada; frontend mantém JWT no localStorage |
| Antiforgery | Sem registro/uso explícito, filtro de validação ou UseAntiforgery encontrado. Upload é action de controller com IFormFile, não endpoint Minimal API de formulário que deva ser automaticamente equiparado ao mesmo caso |
| TempData / views | AddControllers, sem AddControllersWithViews/AddMvc, views ou uso de TempData identificado |
| Session | Sem AddSession/UseSession/uso identificado |
| OAuth/OIDC | Sem handlers/fluxos identificados |
| Identity token providers | Não há AddIdentity/AddDefaultIdentity/AddDefaultTokenProviders; usar PasswordHasher isolado não habilita esses recursos |
| IDataProtector customizado | Nenhuma chamada/implementação encontrada |
| Perfil, catálogo, biblioteca, avatares | Nenhuma dependência do key ring encontrada nos fluxos atuais |

Conclusão limitada ao código inspecionado: registro indireto existe, mas nenhum payload funcional do SeenIt dependente do ring foi identificado. Não há suporte para prever logout causado somente pela perda dessas chaves. Serviços registrados não equivalem a recursos efetivamente usados; confirmar novamente se novos fluxos forem introduzidos.

## 10. Avaliação dos quatro warnings

| Warning / origem | Causa provável e condição | Evidência / aplicabilidade | Impacto e severidade sugerida | Limitação / alternativa |
| --- | --- | --- | --- | --- |
| 1 — FileSystemXmlRepository | Ring no filesystem local de container; persistência externa não garantida | Diretório criado no Dockerfile e warning fornecido; pertinente à infraestrutura atual | Risco operacional baixo no SeenIt atual; maior se cookies/tokens protegidos forem adicionados | Mount remoto desconhecido; documentar aceitação atual ou persistir ring conforme necessidade real |
| 2 — XmlKeyManager | Gravação de chave sem encryptor XML configurado | Sem configuração explícita; warning fornecido | Hardening de segurança, baixo/condicional hoje; exposição do ring seria relevante para payloads Data Protection | Não prova vazamento nem ausência de criptografia do disco; ACLs, encryptor X.509/KMS e repositório protegido se necessário |
| 3 — Hosting.Diagnostics | URLs explícitas prevalecem sobre HTTP_PORTS | PORT → UseUrls e HTTP_PORTS coexistem, ambos 10000 no cenário informado | Informativo; sem vulnerabilidade ou conflito funcional demonstrado | Ambiente remoto não inspecionado; simplificar fonte de binding apenas se houver benefício verificável |
| 4 — HttpsRedirectionMiddleware | Requisição vista como HTTP e nenhuma porta HTTPS de destino descoberta | HTTP-only interno, branch HTTPS fora de health, proxy condicional | Operacional/configuração, prioridade média para esclarecer; risco de segurança condicional à efetiva borda e acessos alternativos | Não prova TLS público quebrado; confirmar confiança/esquema e decidir responsabilidade por redirect antes de corrigir |

Severidades são propostas contextuais, não CVSS ou incidentes comprovados. Não há base para classificar os quatro como vulnerabilidades automaticamente.

## 11. Riscos reais e potenciais

Comprovados no repositório: binding redundante, ring local sem configuração explícita de persistência/encryptor, redirecionamento sem porta explícita e forwarded headers condicionais. Comprovados pelo relato: os quatro avisos ocorreram e testes funcionais passaram.

Potenciais: URL HTTP de avatar legado, esquema incorreto, redirects repetidos após correção parcial, acesso HTTP alternativo se a política depender exclusivamente de uma borda contornável, incompatibilidade de payloads protegidos entre instâncias futuras e exposição de chaves se o acesso ao filesystem for comprometido. Nenhuma dessas consequências foi observada diretamente nesta etapa.

## 12. Alternativas técnicas

Data Protection: não adicionar infraestrutura apenas para silenciar log se não há payload durável consumidor. Se houver requisito, escolher armazenamento, controle de acesso e proteção em repouso juntos. PersistToFileSystem com volume não adiciona encryptor automaticamente; proteger com certificado X.509 disponível ao processo é opção portátil em Linux, com gestão e rotação da chave privada. DPAPI é opção Windows, inadequada ao container Linux atual. Providers oficiais incluem EF Core, Redis e Azure Blob/Key Vault, com custos e dependências diferentes. Nenhuma instalação ou acesso a banco nesta etapa.

`SetApplicationName` estabiliza isolamento entre réplicas/mudanças de content root; não compartilha nem cifra o ring. Manter nome igual apenas entre participantes que devam compartilhar payloads e separar ambientes. Migrar infraestrutura exige preservar chaves antigas, acesso aos certificados antigos, propósitos e discriminador compatíveis; trocar nome pode invalidar dados existentes. [Configuração oficial Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

HTTPS: preservar a ordem atual; configurar exclusivamente peers comprovados. Depois, optar por redirect na borda ou na aplicação com porta externa correta e esquema restaurado. Não adicionar certificado/listener HTTPS interno apenas para eliminar warning. Remover redirect da aplicação exige garantir enforcement de todos os ingressos e rever portabilidade, tráfego privado e desenvolvimento; não desabilitar globalmente sem essa decisão.

Portas: manter PORT → UseUrls é compatível com Render. Pode-se avaliar eliminar HTTP_PORTS redundante da imagem, lembrando defaults herdados e execução fora do Render, ou substituir a estratégia por uma única fonte que continue respeitando PORT. Não há urgência funcional demonstrada.

## 13. Comparação de soluções

| Opção | Benefício | Custo/risco | Adequação |
| --- | --- | --- | --- |
| Aceitar/documentar ring efêmero hoje | Evita infraestrutura sem consumidor identificado | Reavaliar antes de introduzir payload persistente | Razoável no escopo atual; não resolve durabilidade futura |
| Filesystem persistente + ACL + X.509 | Modelo simples de ring durável e proteção XML | Custódia/rotação de certificado, backup; disco Render limita escala | Instância única com requisito demonstrado |
| Repositório compartilhado EF Core ou Redis | Compartilha entre réplicas | Dependência, permissões e durabilidade do banco/cache; cifra separada | Crescimento com consumidores reais; Redis não durável pode perder ring |
| Blob + Key Vault | Separa armazenamento e proteção via serviço de chaves | Identidade, disponibilidade, custo e dependência Azure | Somente se estratégia de infraestrutura justificar |
| Forwarded headers restritos + redirect na aplicação | Esquema correto e controle explícito | Exige peer/topologia comprovados, porta externa e teste de loop | Viável se Render permitir confiança operacional estável |
| Enforcement na borda e redirect interno condicionado/removido | Evita dependência de descoberta de porta no Kestrel | Cobrir todos os ingressos; Scheme ainda importa para URLs | Candidato após confirmar garantias, sem confiar em headers arbitrários |
| Manter binding atual | Preserva comportamento aprovado e PORT dinâmico | Warning informativo continua | Aceitável até simplificação justificada |

## 14. Recomendações priorizadas

1. Prioridade média: esclarecer o warning HTTPS com informações sanitizadas sobre peer imediato, esquema, headers e caminho; não definir 443 isoladamente.
2. Decidir formalmente quem impõe HTTPS e quais ingressos precisam ser cobertos. Manter confiança restrita e exceção de health sob revisão operacional.
3. Prioridade baixa no produto atual: registrar ausência de consumidor funcional Data Protection; confirmar necessidade antes de escolher persistência e encryptor.
4. Antes de cookies, Identity tokens ou escala com payload protegido: implementar ring durável compartilhável, isolamento e proteção apropriados.
5. Tratar warning de portas como simplificação opcional; não bloquear deploy aprovado apenas pelo log. Não suprimir categorias de logging como substituto do diagnóstico.

## 15. Plano para B06-A2

Proposta, sem execução nesta etapa:

1. Revalidar Git e escopo autorizado. Obter do operador inventário sanitizado de nomes/configurações pertinentes, quantidade de instâncias/mounts e confirmação oficial da topologia do Render, sem tokens, cookies ou conteúdo do ring.
2. Se autorizado em B06-A2, reproduzir localmente com configuração fictícia: peer confiável versus não confiável, Proto ausente/http/https, XFF, health e endpoints comuns. Verificar Scheme, Location, número de redirects e URLs legadas, sem registrar credenciais.
3. Escolher e documentar estratégia de HTTPS; implementar mudança mínima apenas após resolver confiança. Critérios: nenhum loop, HTTPS público preservado, peer não confiável incapaz de impor esquema/IP, health operacional e CORS/JWT inalterados.
4. Decidir aceitação documentada de Data Protection efêmero ou persistência/encryptor com requisitos explícitos. Se persistir, validar ciclo de restart, réplica e recuperação com payload fictício, sem usar chaves reais.
5. Opcionalmente simplificar binding; verificar porta padrão e PORT alternativo. Comparar logs por causa, sem ocultar avisos relevantes.
6. Rever diff e executar checks proporcionais. Commit, push, PR e deploy dependem do escopo que vier a ser autorizado para B06-A2; nada disso foi feito aqui.

## 16. Evidências e limitações

Fontes locais principais: `Program.cs:22–28,67–95,115–145,159–176`; `Dockerfile:37–46`; `backend/docker/runtime-settings.json`; `Services/TokenService.cs`; `Services/Avatars/AvatarStorageService.cs:54–59`; `RateLimiting/AbuseProtection.cs`; `Controllers/ProfileController.cs`; `frontend/src/app/core/auth.service.ts`; `RENDER.md`; `RENDER-ENV-CHECKLIST.md`; relatórios B05-B4 citados. Referências de linha correspondem ao SHA inicial, sem modificação desses arquivos.

Verificações: status, fetch, comparação de SHAs, inventário de branches/worktrees, leitura dirigida e buscas estáticas por middlewares, portas, Data Protection, cookies, antiforgery, session, TempData, OAuth/OIDC, Identity providers e uso de Scheme. Consultadas fontes oficiais Microsoft/ASP.NET Core e documentação pública Render, com links junto às conclusões. Algumas URLs raw de implementação de HTTPS/MVC não foram recuperadas; conclusões correspondentes usam documentação oficial disponível e código local, sem atribuir conteúdo às consultas falhas.

Não foram executados build, containers, aplicação, testes com rede, migrations, chamadas de produção, acesso a banco, leitura de secrets, geração/movimentação de chaves, commit, push, PR ou deploy. Nenhuma configuração de produção ou permissão atual do diretório foi medida. O relatório não certifica a configuração remota: separa fatos de código, contrato documentado da plataforma e observações ainda necessárias.

Entrega exclusiva: este arquivo. Código, Dockerfile, frontend, JWT e dependências permanecem sem alteração. Branches preexistentes e worktree maintenance-phase1 preservadas.

**B06-A1 DIAGNÓSTICO CONCLUÍDO**
