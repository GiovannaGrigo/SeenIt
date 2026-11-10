# Backend SeenIt no Render com Docker

Preparação local, sem deploy. As três migrations já foram aplicadas no Supabase e **não** fazem parte do build, entrypoint ou pre-deploy command. O frontend permanece em serviço separado na Vercel.

## Arquivos e contexto

- Dockerfile: `backend/SeenIt.Api/Dockerfile`.
- Build context: raiz do repositório (`.`), não `backend/SeenIt.Api`.
- `.dockerignore` da raiz usa allowlist para API, dois projetos de verificações e configuração sanitizada do container. Frontend, artifacts, bin/obj, node_modules, uploads, configurações locais, `.env`, certificados e secrets ficam fora do contexto, exceto a CA pública `backend/docker/certs/prod-ca-2021.crt`.
- `backend/docker/runtime-settings.json` contém somente URL pública do TVmaze e níveis de logging. É copiado como `/app/appsettings.json`. **Nenhum `appsettings*.json` local é enviado ao build**, inclusive o arquivo com a conexão local.
- O Dockerfile não declara `ARG` de segredo e não carrega User Secrets. O Render pode disponibilizar variáveis de ambiente também como build arguments: não adicionar referências a elas no Dockerfile. Não cadastrar secret files dentro dos caminhos permitidos do backend.

## Imagem Linux e verificações

São usadas as imagens oficiais `mcr.microsoft.com/dotnet/sdk:10.0-noble` e `mcr.microsoft.com/dotnet/aspnet:10.0-noble` (Ubuntu/glibc), com publish Release, RID `linux-x64`, framework-dependent, sem apphost ou trimming. Construir para `linux/amd64`. A imagem final contém apenas o publish da API, a configuração sanitizada e um marcador de verificações; não contém SDK, fontes ou executáveis de teste.

O usuário final é `app` (não root). O código permanece somente para leitura pelo usuário da aplicação; `/home/app/.aspnet/DataProtection-Keys` é gravável por ele. O diretório vazio `/app/wwwroot` existe para o middleware estático. Avatares em Production vão para Supabase Storage, sem gravação no filesystem da imagem. As chaves de Data Protection são efêmeras no container; JWT continua usando a chave explícita de configuração. Se futuramente forem usados cookies/proteção de dados persistentes, definir armazenamento compartilhado de chaves antes de escalar.

O estágio `verify` roda sobre a **mesma imagem de runtime** da API. Compara os binários SkiaSharp de testes e publish, executa `ldd` em `libSkiaSharp.so` (falha se existir `not found`) e roda os testes existentes:

- `SeenIt.AvatarChecks`: decodifica JPEG/PNG/WebP reais e converte para WebP, além dos testes de validação, Storage HTTP simulado, persistência, concorrência e contratos.
- `SeenIt.Checks`: tempo assistido, fusos horários e classificação de séries com HTTP simulado.

A imagem Ubuntu já fornece libc, libm, libdl, libpthread, runtime C/C++, certificados CA, ICU e tzdata. Os assets `SkiaSharp.NativeAssets.Linux.NoDependencies` não exigem fontconfig para a conversão de bitmaps usada aqui. A verificação real, não apenas essa expectativa, deve passar. Não trocar por Alpine/musl ou imagem chiseled sem nova validação.

O estágio final depende do marcador gerado por `verify`, portanto o build completo executa as verificações. Restaurar e publicar também executa a auditoria normal do NuGet; há ainda listagem explícita de vulnerabilidades nos três projetos. `dotnet package list --vulnerable` é informativo: saída zero não é uma política automática de reprovação por advisory. Revisar seu resultado antes de publicar. Atualizar/revalidar imagens base e pacotes nativos; NuGetAudit não audita todos os componentes do sistema operacional/Skia nativo. Para releases reproduzíveis, registrar e fixar digests verificados das imagens oficiais, atualizando-os sob controle.

Comandos, na raiz do repositório:

```powershell
docker build --platform linux/amd64 -t seenit-api:render-check -f backend/SeenIt.Api/Dockerfile .
powershell -NoProfile -File backend/docker/Test-Container.ps1 -Image seenit-api:render-check
```

`Test-Container.ps1` inicia a imagem final em Production, como não root, sem SDK e com `--network none`. Usa somente configuração sintética de runtime e uma chave JWT aleatória descartável; não lê User Secrets, não possui credenciais reais e não pode acessar Supabase. Faz GET HTTP real a `/health` por loopback dentro do container, usando a porta alternativa 18081 para comprovar o override `PORT`; verifica o binding `0.0.0.0`. Remove apenas seu próprio container e restaura as variáveis do processo ao terminar. Não são necessários curl, SDK ou utilitário de teste na imagem final.

A porta pode ser testada explicitamente com `-Port 10000` ou outro valor acima de 1023. O script não é um teste integrado de banco/Storage e não valida a infraestrutura real de proxy do Render.

### Resultado da validação local desta preparação

| Verificação | Resultado |
|---|---|
| Build Docker completo, Release `linux-x64` | aprovado, imagem `seenit-api:render-check` |
| Plataforma e usuário | `linux/amd64`, `app`, UID 1654 |
| SkiaSharp no runtime Ubuntu | decodificação JPEG/PNG/WebP e conversão WebP reais aprovadas; 16 grupos de AvatarChecks passaram |
| Dependências nativas | `ldd` resolveu libc, libm, libdl, libpthread, librt, libstdc++ e libgcc_s, sem `not found` |
| Testes existentes | tempo assistido/fusos e classificação de séries aprovados no container |
| Auditoria NuGet | nenhum pacote vulnerável nos três projetos, no host e no build Linux |
| Smoke da imagem final | Production, sem SDK, não root, `--network none`, somente configuração sintética |
| Override de `PORT` | binding real `http://0.0.0.0:18081` confirmado |
| GET `/health` no Linux | HTTP 200, `{"status":"ok"}` |
| Configurações e fotos locais | somente `appsettings.json` sanitizado, sem connection strings/chaves; wwwroot sem arquivos locais |
| Limpeza | container temporário removido; variáveis do processo restauradas |

Identificador local observado: `sha256:75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28`. A imagem foi construída localmente e não foi enviada para registry nem publicada. Nenhuma credencial real, banco ou Storage foi utilizado nessas verificações. Repetir o build e o smoke test após mudanças de imagem/pacotes; esse resultado não valida a topologia do Render.

## Configuração específica no painel Render

Quando houver autorização para o primeiro deploy:

| Campo | Configuração |
|---|---|
| Tipo | Web Service |
| Language / Runtime | Docker |
| Root Directory | vazio: raiz do repositório |
| Dockerfile Path | `backend/SeenIt.Api/Dockerfile` |
| Docker Build Context Directory | `.` |
| Docker Command | vazio, para usar `ENTRYPOINT ["dotnet", "SeenIt.Api.dll"]` |
| Build / Start Command | sem comandos adicionais; Dockerfile faz build/publish e entrypoint inicia a API |
| Pre-Deploy Command | vazio; nenhuma migration automática |
| Health Check Path | `/health` |
| Porta | `PORT=10000`, ou o valor fornecido/configurado no serviço |
| Auto-Deploy | manter desabilitado até concluir as pendências e autorizar a publicação |

Opcionalmente configurar build filters para `backend/SeenIt.Api/**`, `backend/SeenIt.AvatarChecks/**`, `backend/SeenIt.Checks/**`, `backend/docker/**` e `.dockerignore`, evitando rebuild por mudanças exclusivas no Angular. Não mudar o Root Directory para a API: os outros projetos e a configuração ficariam inacessíveis ao build.

O Dockerfile define HTTP na porta 10000. `Program.cs` já lê `PORT`, valida 1–65535 e usa `http://0.0.0.0:{PORT}`; esse código não foi alterado. `EXPOSE` é metadado, não faz publicação de porta nem limita um override. O TLS externo é terminado pelo Render. Não instalar certificado de desenvolvimento nem habilitar HTTPS interno com certificados locais.

`GET /health` é anônimo e retorna `200 {"status":"ok"}` inclusive via HTTP interno, sem redirecionar. É somente liveness: não consulta PostgreSQL/Storage e não comprova prontidão desses serviços. A configuração `AllowedHosts` deve incluir o hostname efetivamente usado pelo health check (domínio confirmado e/ou subdomínio onrender.com).

## Variáveis de ambiente de runtime

Cadastrar valores reais somente no gerenciador de segredos/ambiente do serviço, nunca no Git, Dockerfile, build args, logs ou frontend. Os marcadores abaixo não são valores utilizáveis:

```text
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
PORT=10000
ConnectionStrings__DefaultConnection=<CONEXAO_SUPABASE_NPGSQL_COM_TLS>
Jwt__Key=<BASE64_DE_32_OU_MAIS_BYTES_ALEATORIOS_NOVOS>
Jwt__Issuer=<EMISSOR_CONFIRMADO>
Jwt__Audience=<PUBLICO_CONFIRMADO>
Jwt__ExpirationMinutes=<INTEIRO_POSITIVO>
SupabaseStorage__Url=https://<PROJECT_REF>.supabase.co
SupabaseStorage__SecretKey=<SB_SECRET_SOMENTE_BACKEND>
SupabaseStorage__Bucket=avatars
Cors__AllowedOrigins__0=https://<HOST_CONFIRMADO_DO_FRONTEND_VERCEL>
AllowedHosts=<HOST_CONFIRMADO_DA_API>
ReverseProxy__KnownProxies__0=<IP_CONFIRMADO_DO_PROXY_IMEDIATO>
```

Em Production todos os parâmetros JWT, conexão não vazia e Supabase Storage válido são obrigatórios. A chave JWT deve ser nova, gerada criptograficamente, Base64 de pelo menos 32 bytes. A API usa essa string como texto UTF-8 no HS256, preservando o contrato existente; a decodificação em Production serve apenas à validação. Não reutilizar a chave local nem confundi-la com a Secret API Key do Supabase.

O container não contém os defaults locais de issuer/audience/expiração: preencher essas variáveis explicitamente. `SupabaseStorage__Enabled` não é necessário em Production, pois o código exige Storage persistente. A Secret API Key `sb_secret_` é enviada apenas em `apikey`, nunca como JWT ou URL. O bucket `avatars` precisa aceitar WebP e ter o limite esperado de 5 MiB. Rever [AVATAR_STORAGE.md](AVATAR_STORAGE.md), incluindo migração conservadora dos arquivos legados e monitoramento de limpeza pendente.

No Render, `ConnectionStrings__DefaultConnection` é a configuração exclusiva do **processo remoto**. Não alterar a chave local no arquivo ou nos User Secrets, nem presumir que `SupabaseTest` será selecionada. O Dockerfile não conhece nenhuma dessas conexões. Nenhuma conexão real é usada no build ou smoke test.

### TLS do Supabase Session Pooler (preparação em 10/11/2026)

O provider `Npgsql.EntityFrameworkCore.PostgreSQL` é 10.0.0. `Program.cs` passa
`GetConnectionString("DefaultConnection")` diretamente para `UseNpgsql`, sem
callback personalizado de certificados. A mensagem de erro sobre callback pode
vir da validação interna do driver; sozinha, não distingue CA ausente, cadeia
incompleta, hostname incorreto ou certificado expirado. A inclusão abaixo resolve
a disponibilidade da CA na imagem; o handshake real ainda precisa ser comprovado.

O arquivo público baixado do Supabase foi colocado em
`backend/docker/certs/prod-ca-2021.crt`. Metadados conferidos com OpenSSL:

- Subject e issuer: `Supabase Root 2021 CA`, organização `Supabase Inc`.
- CA:TRUE; uso de assinatura de certificados e CRLs; nenhum bloco de chave privada.
- Validade: 28/04/2021 10:56:53 UTC a 26/04/2031 10:56:53 UTC.
- Fingerprint SHA-256 do certificado:
  `80:70:25:AD:50:D4:ED:21:9D:2C:9C:7D:29:9C:00:4F:82:4E:B0:0C:F7:F6:5A:FE:F6:07:D0:7B:72:E6:CA:FA`.

Somente esse caminho foi liberado ao final do `.dockerignore`; os bloqueios de
outros `.crt`, `.cer`, `.pem`, `.key`, `.pfx`, `.p12`, `.env` e configurações
sensíveis continuam ativos. Não colocar outros arquivos nesse diretório.
O estágio final copia a CA para `/app/certs/prod-ca-2021.crt` com modo `0444`
(root proprietário; leitura para `app`, sem escrita). O diretório criado pelo
runtime-base é criado explicitamente com modo `0755` antes de `USER app`;
não depender da criação implícita pelo `COPY --chmod=0444`, que pode deixar
diretórios novos sem travessia. O `RUN` de validação, herdando `USER app`,
mostra identidade, permissões dos diretórios, modo, proprietário e tamanho do
certificado; exige arquivo regular não vazio, legível e sem escrita, com `0444`.
Não é necessário instalar essa CA globalmente no sistema operacional.

No painel Render, editar **o valor** de `ConnectionStrings__DefaultConnection`,
preservando usuário, senha, database e demais parâmetros operacionais existentes.
Usar a sintaxe Npgsql, com este modelo sem segredos:

```text
Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=<DATABASE_ATUAL>;Username=<USUARIO_SESSION_POOLER_ATUAL>;Password=<SENHA_ATUAL>;SSL Mode=VerifyFull;Root Certificate=/app/certs/prod-ca-2021.crt
```

Conferir os parâmetros no painel Connect > Session pooler do projeto correto.
Não duplicar chaves de TLS; substituir eventual caminho antigo de Root Certificate.
Manter `VerifyFull`; não adicionar bypass de validação. Senhas com caracteres
especiais precisam do quoting/escaping próprio de connection strings Npgsql.
Não copiar o valor real para documentação, terminal, logs ou build arguments.

O Npgsql aceita `Root Certificate` como caminho de uma CA e `VerifyFull` verifica
cadeia e hostname ([documentação Npgsql](https://www.npgsql.org/doc/security.html)).
O Supabase documenta CA baixada do Dashboard e `verify-full` também para
[Session Pooler](https://supabase.com/docs/guides/database/psql).
Isso é compatível com a configuração proposta, mas não prova qual cadeia o
endpoint apresenta hoje. A CA raiz não comprova o hostname do servidor: é preciso
examinar o SAN do certificado folha. Se o SAN contiver `*.pooler.supabase.com`,
ele cobre `aws-0-us-east-1.pooler.supabase.com` (um único rótulo).

Validações desta alteração, sem credenciais reais nem acesso ao banco:

- Cópia comparada por SHA-256 de arquivo, idêntica ao original em Downloads.
- OpenSSL leu os metadados e validou a autoassinatura com
  `openssl verify -check_ss_sig -CAfile backend/docker/certs/prod-ca-2021.crt backend/docker/certs/prod-ca-2021.crt`.
  Isso valida a raiz isolada, não a cadeia remota nem sua procedência externa.
- Programa descartável .NET 10 usando o Npgsql 10.0.0 local confirmou parsing de
  `SslMode.VerifyFull`, `RootCertificate`, host e porta; não chamou `Open`.
- Revisão estática do Dockerfile e das regras de exclusão; `git diff --check`.
- Docker CLI disponível, mas daemon local indisponível (pipe `docker_engine`
  ausente); build da imagem alterada e smoke Linux **não executados**.
  Os resultados anteriores deste documento não validam esta nova imagem.

Pendência remota, a executar somente em etapa operacional autorizada, sem senha:

```sh
openssl s_client -starttls postgres -connect aws-0-us-east-1.pooler.supabase.com:5432 -servername aws-0-us-east-1.pooler.supabase.com -CAfile backend/docker/certs/prod-ca-2021.crt -verify_hostname aws-0-us-east-1.pooler.supabase.com -verify_return_error -showcerts </dev/null
```

Esse comando acessa o endpoint de produção para handshake TLS, embora não envie
credenciais nem consultas, por isso não foi executado aqui. Exigir verificação
sem erros, conferir SAN, intermediárias e validade; se falhar, investigar a cadeia
antes de publicar. A confirmação autenticada pelo Npgsql também fica pendente.

Para o próximo deploy: incluir os quatro arquivos desta alteração no commit;
repetir o build Linux e `Test-Container.ps1` acima com Docker disponível; realizar
a verificação remota autorizada; cadastrar a connection string no Render junto
da nova imagem (o caminho não existe na imagem antiga). Com auto-deploy desativado,
salvar a variável sem disparar deploy e então publicar manualmente o commit
validado. Manter contexto `.`, Dockerfile e pre-deploy vazio. Após publicar,
conferir `/health` e testar login com conta de teste autorizada para comprovar a
conexão autenticada. `/health` sozinho não verifica o banco. Nenhum deploy,
migration ou alteração de código de negócio foi realizado nesta preparação.

CORS precisa da origem HTTPS exata do frontend Vercel, sem caminho, wildcard ou barra final. Origens adicionais exigem índices `__1`, `__2` etc. Não autorizar automaticamente todos os previews de Vercel. Sem origens, Production não autoriza CORS.

## Forwarded headers: bloqueio operacional antes do primeiro deploy

A implementação atual foi revisada e permanece restrita: ativa `UseForwardedHeaders` apenas quando há IPs explícitos em `ReverseProxy:KnownProxies`; limpa as redes padrão e adiciona somente os IPs confirmados (inclusive representação IPv4 mapeada). Aceita apenas `X-Forwarded-For` e `X-Forwarded-Proto`, com `ForwardLimit=1`, antes do redirecionamento HTTPS. Não aceita `X-Forwarded-Host` e não confia em qualquer proxy.

Não configurar `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: a aplicação deliberadamente rejeita esse atalho amplo. Não liberar `0.0.0.0/0`, não confiar em todos os proxies e não forçar `Request.Scheme` a partir de um cabeçalho arbitrário. A origem HTTPS precisa ser reconhecida pelo middleware com um proxy confiável.

Confirmar com o Render o IP/remoto realmente visto por Kestrel, a estabilidade desse endereço, quantidade de saltos e sanitização de cabeçalhos. Não inferir esses IPs do DNS público ou dos IPs de saída do serviço. Sem isso, `/health` pode passar enquanto o restante da API reconhece o protocolo incorretamente. Se a topologia não permitir uma lista estável de IPs ou exigir vários saltos, a configuração atual não basta: revisar a integração antes de publicar, sem remover a validação por conveniência.

Validar então HTTPS externo, ausência de loops, geração de URLs legadas e descarte de `X-Forwarded-*` forjados por clientes. Nenhuma dessas verificações reais de infraestrutura é comprovada pelo smoke test local.

## Pendências antes da publicação

1. Repetir build, native checks e `/health` no artefato final de release se houver mudanças; a validação local atual está registrada acima. Se Docker estiver indisponível em uma execução futura, não presumir que um novo artefato foi validado.
2. Confirmar proxy/forwarded headers e domínio de API; cadastrar AllowedHosts e CORS da Vercel.
3. Cadastrar segredos de runtime no Render e conferir conexão TLS/projeto Supabase, bucket e políticas de acesso. Não repetir migrations já aplicadas automaticamente.
4. Confirmar que avatares legados necessários foram migrados ou preservados em armazenamento acessível; disco efêmero não é backup.
5. Validar no serviço futuro a conectividade com PostgreSQL/Storage e os fluxos reais com um usuário fictício, sob autorização, sem alterar dados existentes.
6. Revisar a auditoria NuGet e o scanner da imagem/OS, fixar os digests de release e só então autorizar o deploy.

Referências oficiais: [Docker no Render](https://render.com/docs/docker), [monorepo/contexto](https://render.com/docs/monorepo-support), [port binding](https://render.com/docs/web-services#port-binding), [health checks](https://render.com/docs/health-checks), [imagens .NET](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images), [forwarded headers ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
