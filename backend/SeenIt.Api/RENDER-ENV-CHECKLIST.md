# Checklist de ambiente de produção — Render / Docker

Análise estática em 09/11/2026. Nenhum deploy, acesso ao banco, geração de credenciais ou execução de migrations foi realizado. Os valores reais devem existir somente no ambiente protegido do serviço. Este documento complementa `RENDER.md`; não comprova a infraestrutura remota nem repete como resultado desta análise os testes anteriormente registrados naquele arquivo.

## Variáveis no Render

“Sim” indica valor necessário no runtime; quando há default na imagem, o cadastro manual é dispensável. “Operacional” indica configuração necessária para a integração segura, embora a ausência não impeça o processo de iniciar.

| Variável no Render | Obrigatória | Sensível | Finalidade | Origem do valor |
| ------------------ | ----------- | -------- | ---------- | --------------- |
| `ASPNETCORE_ENVIRONMENT` | Sim; imagem já define `Production` | Não | Ambiente ASP.NET Core | Dockerfile; manter `Production` |
| `DOTNET_ENVIRONMENT` | Sim; imagem já define `Production` | Não | Ambiente do host; manter coerência com ASPNETCORE_ENVIRONMENT | Dockerfile; manter `Production` |
| `PORT` | Sim no container; default `10000` | Não | Binding HTTP em `0.0.0.0`, inteiro 1–65535 | Render ou default da imagem; usar porta acima de 1023 com usuário `app` |
| `ConnectionStrings__DefaultConnection` | Sim; cadastrar | Sim | PostgreSQL via Npgsql / EF Core | Supabase, painel Connect: host, porta, database, usuário e senha do banco; configurar TLS |
| `Jwt__Key` | Sim; cadastrar | Sim | Assinar e validar JWT HS256 | Segredo próprio novo, gerado criptograficamente fora do repositório; Base64 de pelo menos 32 bytes aleatórios |
| `Jwt__Issuer` | Sim; cadastrar | Não | Identificador de emissor usado na emissão e validação | Decisão do responsável pela autenticação; manter valor estável e consistente |
| `Jwt__Audience` | Sim; cadastrar | Não | Identificador de público usado na emissão e validação | Decisão do responsável pela autenticação; manter valor estável e consistente |
| `Jwt__ExpirationMinutes` | Sim; cadastrar | Não | Validade dos tokens, inteiro positivo | Política de sessão definida pelo responsável |
| `SupabaseStorage__Url` | Sim; cadastrar | Não | Endpoint e URLs públicas de avatares | URL raiz HTTPS do projeto Supabase correto |
| `SupabaseStorage__SecretKey` | Sim; cadastrar | Sim | Operações backend de upload e remoção | Secret API Key `sb_secret_` do mesmo projeto Supabase; não usar senha PostgreSQL, anon/publishable ou JWT service_role legado |
| `SupabaseStorage__Bucket` | Não; default `avatars` | Não | Bucket dos avatares | Código exige exatamente `avatars`; cadastrar explicitamente se desejado |
| `SupabaseStorage__Enabled` | Não; omitir | Não | Opt-in de Storage fora de Production | Default `false`; em Production o código força `true`, mesmo se configurado `false` |
| `Cors__AllowedOrigins__0` | Operacional para Angular em outra origem | Não | Origem HTTPS autorizada pelo navegador | Domínio confirmado do frontend; sem caminho, wildcard ou barra final |
| `Cors__AllowedOrigins__1`, `__2`, … | Se houver outras origens aprovadas | Não | Origens adicionais explícitas | Lista aprovada de domínios do frontend, uma origem por índice consecutivo |
| `AllowedHosts` | Operacional para restringir hosts | Não | Filtro de Host do ASP.NET Core | Hostnames confirmados da API, inclusive os necessários ao health check; separados por `;`, sem esquema, caminho ou porta |
| `ReverseProxy__KnownProxies__0` | Operacional; pendente de confirmação da topologia | Não | Confiar em X-Forwarded-For e X-Forwarded-Proto do proxy imediato | IP remoto confiável efetivamente visto pelo Kestrel, confirmado com Render |
| `ReverseProxy__KnownProxies__1`, `__2`, … | Se existirem outros proxies imediatos aprovados | Não | IPs alternativos confiáveis | Infraestrutura confirmada; não representa configuração de múltiplos saltos |
| `TvMaze__BaseUrl` | Não cadastrar; já presente na imagem | Não | Base URL do `TvMazeClient` | `backend/docker/runtime-settings.json`: `https://api.tvmaze.com/` |
| `Logging__LogLevel__Default` | Não | Não | Nível geral de logs | Configuração sanitizada: `Information` |
| `Logging__LogLevel__Microsoft.AspNetCore` | Não | Não | Nível dos logs ASP.NET Core | Configuração sanitizada: `Warning` |
| `ASPNETCORE_HTTP_PORTS` | Não cadastrar; imagem define `10000` | Não | Default HTTP do runtime | Dockerfile; `UseUrls` com `PORT` tem precedência |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Não; omitir | Não | Atalho de forwarded headers rejeitado pela aplicação se `true` | Nenhum valor necessário; não habilitar |

## Nomes .NET, defaults e consumidores

O provider padrão de ambiente converte `__` em `:`; índices numéricos representam arrays. Não existe carregamento automático de arquivo `.env` no código. As variáveis precisam estar no ambiente do processo.

| Chave esperada pelo .NET | Consumidor | Default e comportamento |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `Program.cs`, validação Production e `UseNpgsql`; `Data/AppDbContext.cs` recebe opções | Vazia no appsettings local; ausente no sanitizado. Não há fallback de banco ou seleção de `SupabaseTest` |
| `Jwt:Key` | `Program.cs` e `Services/TokenService.cs` | Sem default; Production valida Base64, mínimo 32 bytes e rejeita bytes todos iguais |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `Program.cs` e `Services/TokenService.cs` | Defaults locais `SeenIt.Api`, `SeenIt.Web`, `120`; **não estão na imagem**. Issuer e audience são identificadores, não precisam ser URLs |
| `SupabaseStorage:Enabled`, `:Url`, `:SecretKey`, `:Bucket` | `Services/Avatars/AvatarStorageOptions.cs`, `AvatarServiceRegistration.cs`, `AvatarStorageService.cs`, `SupabaseAvatarStorageService.cs` via `IConfiguration` / `IOptions<AvatarStorageOptions>` | `false`, string vazia, string vazia, `avatars`; Production força Enabled e valida no startup |
| `Cors:AllowedOrigins` | `Program.cs`, policy `Frontend` | Array vazio. Só Development acrescenta localhost:4200. Vazio em Production não autoriza CORS; não impede startup |
| `ReverseProxy:KnownProxies` | `Program.cs` | Array vazio, middleware desabilitado. Só aceita IPs explícitos; rejeita endereços Any e não aceita CIDR |
| `AllowedHosts` | Host filtering do ASP.NET Core via `WebApplication.CreateBuilder` | Local `*`; não copiado para imagem. Sem configuração restritiva, não há allowlist efetiva |
| `TvMaze:BaseUrl` | `Program.cs`, HttpClient de `External/TvMazeClient.cs` | HTTPS público presente no JSON sanitizado. O outro cliente, `ITvMazeService`/`TvMazeService`, usa URL fixa no Program.cs; alterar esta chave não altera ambos |
| `Logging:LogLevel:Default`, `Logging:LogLevel:Microsoft.AspNetCore` | Logging padrão do host | `Information` e `Warning`, inclusive na imagem |
| `PORT` | `Environment.GetEnvironmentVariable` em `Program.cs` | Imagem `10000`; valor inválido interrompe startup |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `Environment.GetEnvironmentVariable` em `Program.cs` | Ausente; `true` causa erro deliberado |
| `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_HTTP_PORTS` | Host/runtime ASP.NET Core | Defaults explícitos na imagem. Com `WebApplication.CreateBuilder`, DOTNET_ENVIRONMENT prevalece para o ambiente; manter ambos em Production |

O Dockerfile também define `HOME=/home/app` para diretório gravável de Data Protection; não é configuração de negócio nem precisa ser cadastrado no Render. Não foram encontrados outros consumidores de `IConfiguration`, `IOptions<T>` ou `Environment.GetEnvironmentVariable` no código da API.

## Como obter e cadastrar valores

1. Usar `.env.render.example` somente como referência de nomes. Seus marcadores são intencionalmente inutilizáveis. Não importar o modelo sem substituir/remover linhas conforme este checklist e não gravar valores reais nele.
2. No painel do serviço Render, em **Environment**, adicionar os nomes exatos da tabela e os valores reais nos campos protegidos. Não usar build args, Dockerfile, frontend ou Git para segredos. Salvar somente quando isso não disparar deploy; não escolher uma ação de salvar/rebuild/deploy durante esta preparação. Se o painel exigir deploy para efetivar alterações, deixar esse passo para a publicação autorizada.
3. Conferir os dois ambientes como Production e a porta escolhida. Os defaults já estão na imagem. Manter Root Directory na raiz, Dockerfile `backend/SeenIt.Api/Dockerfile`, contexto `.`, comando padrão da imagem, health `/health` e pre-deploy vazio. Manter auto-deploy desativado.
4. Em Supabase **Connect**, selecionar conexão direta ou pooler conforme conectividade da região Render (IPv4/IPv6), obter parâmetros e montar formato Npgsql `Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull`. Não colar uma URI `postgresql://` como se fosse a string Npgsql. Senhas com caracteres especiais exigem quoting conforme Npgsql. Não reutilizar Secret API Key como senha do banco.
5. TLS precisa ser explícito na conexão de produção: o código só exige string não vazia e não valida SSL. Preferir `VerifyFull`, que exige certificado válido e hostname correspondente; conferir cadeia de confiança do endpoint escolhido. Se for necessária CA específica, sua disponibilização no container permanece pendente. `Require` exige criptografia mas não valida a identidade do servidor; não tratar como equivalente a `VerifyFull`. Não usar `Disable`, `Prefer` ou bypass de validação para resolver falhas de certificado.
6. Obter URL e Secret API Key do projeto Supabase em suas configurações de API. O validador exige raiz HTTPS, porta 443, host com referência de 10–32 caracteres alfanuméricos minúsculos em `.supabase.co`, sem caminho/query/fragmento/credenciais. A chave deve corresponder ao formato `sb_secret_` aceito pelo código. Confirmar bucket público `avatars`, WebP permitido e limite 5 MiB. Essas propriedades do bucket não são criadas pela API.
7. Definir política JWT e obter segredo próprio novo por mecanismo criptográfico seguro. Não gerar ou registrar a chave em logs. A aplicação valida o Base64 decodificado, mas usa a **string Base64 em UTF-8** para HS256 tanto ao emitir quanto ao validar; não mudar esse contrato ao cadastrar. Trocar a chave invalida tokens existentes.
8. Obter domínio estável HTTPS do frontend e domínio da API. Cadastrar a origem do frontend em CORS e os hostnames da API em AllowedHosts. O `apiUrl` do Angular precisa ser a URL HTTPS da API com `/api`; é configuração de build do frontend, não uma variável lida pela API.

## Segurança e limites verificados

- Nos fontes da API e appsettings analisados não foram encontrados segredos reais embutidos: conexão padrão vazia, chave JWT ausente e nenhum valor real de Storage. Não foi feita auditoria do histórico Git nem leitura de User Secrets. Fixtures sintéticas dos projetos de checks não são credenciais de produção.
- `UserSecretsId` existe no csproj, mas Production não depende de User Secrets. Docker exclui appsettings locais, `.env*`, secrets e certificados via `.dockerignore`, e substitui o appsettings publicado pelo sanitizado. Development contém somente logging. Defaults locais não devem ser considerados defaults do container.
- A Secret API Key do Storage é enviada somente no header `apikey`, sem Authorization Bearer; redirects HTTP estão desabilitados, logging automático desse cliente removido e headers sensíveis marcados para redação. A autenticação da aplicação continua sendo JWT próprio, não Supabase Auth.
- ForwardedHeaders continua restrito: ativado somente com KnownProxies explícitos, redes padrão removidas, IPv4 mapeado também aceito, somente X-Forwarded-For/Proto e limite de um salto. Não aceita X-Forwarded-Host. Não cadastrar IP público de DNS ou IP de saída como se fosse IP do proxy imediato.
- HTTPS externo termina no Render; o container recebe HTTP. Sem proxy confiável confirmado, o esquema percebido e o redirecionamento HTTPS podem estar incorretos. `/health` ignora redirecionamento e não consulta banco/Storage: HTTP 200 sozinho não valida a integração.
- Avatares novos usam URL pública do Supabase. Avatares locais/legados usam `Request.Scheme`, `Request.Host` e `PathBase`; dependem de host/proxy corretos e preservação dos arquivos antigos. Não existe `Api__BaseUrl`, `FrontendUrl`, `Storage__Path`, chave de TVmaze ou variável de imagem equivalente consumida pelo código.
- `RENDER_EXTERNAL_URL`, `RENDER_EXTERNAL_HOSTNAME` e `DATABASE_URL` não são lidas pela aplicação para preencher essas configurações. Não substituem AllowedHosts, JWT ou DefaultConnection automaticamente.
- `.env.render.example` tem aviso explícito, placeholders e domínios `.invalid`. O `.gitignore` atual ignora `.env.*` (exceto `.env.example`), portanto o novo modelo também fica ignorado. Para versioná-lo, revisar apenas esse arquivo sanitizado e adicioná-lo explicitamente com `git add -f backend/SeenIt.Api/.env.render.example`; esta análise não altera ignore nem staging. Ele continua excluído do build Docker.

## Pendências para o primeiro deploy

- [ ] Confirmar domínio HTTPS estável do Angular. O repositório não comprova uma origem publicada autorizável; `environment.production.ts` contém URL `.invalid` da API e `angular.json` seleciona esse arquivo em Production. Planejar sua substituição no frontend, fora deste trabalho.
- [ ] Confirmar hostname(s) da API e Host usado no health check para preencher AllowedHosts.
- [ ] Confirmar com Render IP(s) do proxy imediato vistos por Kestrel, estabilidade, sanitização dos cabeçalhos e topologia de um salto. Se isso não puder ser garantido, a implementação atual requer revisão antes da publicação; não ampliar confiança para qualquer proxy.
- [ ] Definir issuer, audience, expiração e obter segredo JWT novo; cadastrar conexão PostgreSQL e Secret API Key do projeto correto, somente no backend.
- [ ] Confirmar endpoint PostgreSQL, IPv4/IPv6, modo do pooler e TLS com validação de certificado; nenhuma conexão real foi testada aqui.
- [ ] Conferir bucket público, WebP, limite e arquivos legados. Não há confirmação remota de permissões ou disponibilidade nesta análise.
- [ ] Confirmar estado das migrations por processo operacional autorizado: RENDER.md registra que já foram aplicadas; não foram verificadas nem reaplicadas aqui.
- [ ] Na publicação futura autorizada, executar validação do artefato e testes de HTTPS, cabeçalhos forjados, autenticação, CORS, conexão e avatares com dados de teste apropriados. Não usar `/health` como prova de prontidão do banco/Storage.

Referências oficiais consultadas: [variáveis do Render](https://render.com/docs/environment-variables), [porta de Web Services](https://render.com/docs/web-services#port-binding), [TLS no Npgsql](https://www.npgsql.org/doc/security.html), [conexões Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres). Os defaults de infraestrutura devem ser reconfirmados antes da publicação.
