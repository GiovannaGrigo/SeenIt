# B05-B4.1 — Diagnóstico de segurança Docker e runtime .NET

## 1. Objetivo

Avaliar imagem, runtime e dependências do backend sem corrigir código, atualizar pacotes ou alterar infraestrutura. Diagnóstico realizado em **17/11/2026**, referência de data America/Sao_Paulo. O resultado não certifica segurança nem equivalência com produção.

## 2. Ambiente analisado

- `main` inicialmente limpa; `git fetch origin` concluído. `HEAD` e `origin/main`: `2cd74b978c4700a545475f7e4fdb7aff0cb3a07f`, exatamente a referência solicitada.
- Branch criada: `security/b05-b4-container-review`.
- Windows x64; SDK local 10.0.301; runtimes locais Microsoft.NETCore.App e Microsoft.AspNetCore.App 10.0.9. Essas versões são do host Windows, não da imagem Linux.
- `maintenance-phase1` preservada em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seu worktree separado. Demais branches preservadas.
- Nenhum AGENTS.md encontrado na busca do workspace. Nenhuma instalação, build, restore, container novo, commit, push, migration, acesso a Supabase ou painel Render/Vercel.

Foram lidos Dockerfile, `.dockerignore`, os quatro csproj relevantes, `Program.cs`, processamento/storage de avatares, configuração sanitizada, CA pública e documentação Render/B05. O inventário NuGet usa `backend/SeenIt.Api/obj/project.assets.json` preexistente, confirmado por listagem sem restore; não é inventário de um publish Linux atual.

## 3. Dockerfile e imagens base

| Item | Evidência no repositório |
| --- | --- |
| Build | `mcr.microsoft.com/dotnet/sdk:10.0-noble` |
| Runtime | `mcr.microsoft.com/dotnet/aspnet:10.0-noble` |
| Frontend Dockerfile | `docker/dockerfile:1` |
| Sistema alvo | Ubuntu Noble 24.04, família glibc, inferido da tag; `/etc/os-release` não inspecionado |
| Arquitetura | restore/publish `linux-x64`; documentação orienta `--platform linux/amd64`; FROM não fixa plataforma |
| Digests | Não fixados no Dockerfile; não resolvidos nesta etapa |
| Estágios | build → runtime-base → verify → final |
| Publicação | Release, framework-dependent, sem apphost, sem trimming; símbolos da API desabilitados |

`final` deriva de `runtime-base`, não de `verify`; copia somente `/out/api`, marcador dos checks e CA pública. SDK, fontes e `/checks` não são copiados por essas instruções. O build exige marcador criado após AvatarChecks/CoreChecks, comparação dos binários Skia e `ldd` sem dependências ausentes. São controles definidos no código, não checks reexecutados nesta sessão.

Tags móveis recebem atualizações quando resolvidas novamente, mas não atualizam containers já publicados. Não há garantia de atualização só por rebuild com cache. `RENDER.md` recomenda atualização/revalidação e digests; não foi comprovada automação ou periodicidade. O identificador histórico local `sha256:75239e6e66ef184b6057fd61eb197ec6797d0207bbe93d7cf24925d145fcea28` consta nesse documento; não foi inspecionado, não é digest confirmado das bases e não identifica produção nesta revisão.

## 4. Runtime .NET e NuGet

`net10.0`, `--self-contained false`: o shared framework vem da imagem ASP.NET. A versão exata do patch Linux e do SDK de build permanece desconhecida. Pacote 10.0.0 no csproj não significa runtime 10.0.0 instalado. Não há global.json encontrado; o SDK local também não determina o SDK da tag Docker.

Dependências diretas confirmadas no csproj e assets locais:

| Pacote | Versão |
| --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 |
| Microsoft.AspNetCore.OpenApi | 10.0.0 |
| Microsoft.OpenApi | 2.7.5 |
| Microsoft.EntityFrameworkCore.Design | 10.0.0 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 |
| SkiaSharp | 4.155.0 |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.155.0 |
| System.Security.Cryptography.Pkcs | 9.0.22 |

Transitivos locais incluem EF Core/Abstractions/Relational 10.0.0, Npgsql 10.0.0, IdentityModel/JWT 8.0.1 e Newtonsoft.Json 13.0.3. O ramo de design inclui Humanizer.Core 2.14.1, Microsoft.Build 17.7.2, Build Framework/Tasks/Utilities 17.14.28, Roslyn 4.14.0, Mono.TextTemplating 3.0.0, System.CodeDom/Composition/Configuration 9.0.0 e MetadataLoadContext 7.0.0, entre outros.

**Possível excesso no publish:** EF Design tem `PrivateAssets=all`, mas inclui explicitamente assets `runtime`. Os assets locais atribuem runtime DLLs a EF Design, Microsoft.Build e Pkcs. PrivateAssets limita propagação aos consumidores; não prova exclusão do publish da própria API. Confirmar DLLs e `.deps.json` finais antes de afirmar que ferramentas de design estão ausentes ou remover referências. A presença das DLLs, por si, não demonstra execução remota de MSBuild.

## 5. Bibliotecas nativas e isolamento de testes

O componente nativo esperado é `/app/libSkiaSharp.so` de NativeAssets Linux NoDependencies 4.155.0. O nome NoDependencies não elimina dependências de libc/C++ ou código de codecs embutido. A base é documentada como fornecedora de libc, libm, libdl, libpthread, runtime C/C++, ICU, tzdata e certificados CA; versões de pacotes deb, OpenSSL e encadeamento real não foram medidos. Não se deve inventar versões ou atribuir CVEs Ubuntu sem inventário.

O grafo local também contém NativeAssets macOS/Win32 4.155.0. Isso não comprova presença dos respectivos binários no publish linux-x64. Skia/codecs estaticamente incorporados podem escapar a um scanner de pacotes do SO; é necessário relacionar o binário efetivo ao upstream e suas correções.

O processamento recebe bytes de usuário: SKCodec cria o decoder antes da validação de dimensões/formato real. Limites existentes: 5 MiB, 8192 por lado, 16 milhões de pixels, duas decodificações simultâneas, rejeição de animação e saída reduzida para até 1024 por lado. Mitigam consumo e formatos; não eliminam bugs no parser nativo. O bitmap de 16 milhões de pixels RGBA exige aproximadamente 64 MB, além de cópias, codec e saída; memória/CPU do serviço não foram aferidas.

`SeenIt.RegisterIntegrationChecks` referencia a API, e não o inverso. Seu SQLite explícito é `SQLitePCLRaw.lib.e_sqlite3` 2.1.12. O projeto não entra na allowlist do contexto nem nos COPY/restore/publish Docker. Os dois checks presentes no build referenciam apenas a API e não declaram SQLite. Não há SQLite no grafo local da API. Portanto, está comprovado **o isolamento pelo caminho de build inspecionado**; ausência física de SQLite na imagem, inclusive componentes eventualmente vindos da base, ainda exige SBOM. A CVE histórica de SQLite do B05-B3 não foi reclassificada como falha de produção.

## 6. Ferramentas disponíveis

| Ferramenta | Resultado |
| --- | --- |
| Docker CLI | 29.9.2, windows/amd64 |
| Docker daemon | Indisponível: pipe ausente; confirmado fora do sandbox no contexto desktop-linux |
| Docker Scout | 1.27.0; `version`, ajuda de cves e sbom disponíveis |
| Scout cves | Tentativa `local://seenit-api:render-check --platform linux/amd64` bloqueada por exigência de login; nenhuma autenticação realizada |
| Scout SBOM | Capacidade equivalente a gerador de SBOM identificada na ajuda; SBOM da imagem não gerada |
| Trivy / Grype / Syft | Não encontrados no PATH; isso não prova ausência em todo o disco |
| docker sbom | Subcomando não listado; `--version` retornou versão do Docker e não comprova plugin SBOM |
| .NET CLI | SDK 10.0.301; listagem/auditoria sem restore disponível |

Não foram iniciados Docker Desktop, serviços ou login, nem instaladas ferramentas.

## 7. Resultado da auditoria

Comandos principais e interpretação:

```text
git status --porcelain=v1
git fetch origin
git rev-parse HEAD origin/main
git switch -c security/b05-b4-container-review
docker version
docker image ls --no-trunc
docker scout version
docker scout cves local://seenit-api:render-check --platform linux/amd64
dotnet package list --project backend/SeenIt.Api/SeenIt.Api.csproj --no-restore --include-transitive --format json
dotnet package list --project backend/SeenIt.Api/SeenIt.Api.csproj --no-restore --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json
```

Fetch e criação da branch exigiram escrita autorizada fora do sandbox em `.git`. Docker foi repetido fora do sandbox para eliminar a ambiguidade do bloqueio de configuração; o daemon continuou ausente. A consulta NuGet inicial falhou por socket bloqueado e foi repetida com autorização fora do sandbox. A execução válida retornou código 0 e texto: projeto sem pacotes vulneráveis considerando as fontes atuais. O JSON de vulnerabilidades omitiu frameworks; a listagem completa confirmou `net10.0` e os pacotes, e a saída textual confirmou a interpretação. Não foi executado restore.

Fonte NuGet pública: `https://api.nuget.org/v3/index.json`, consulta em 17/11/2026. Revisão pública complementar dos advisories oficiais Microsoft/SkiaSharp na mesma data. Não foram registrados timestamps internos da base NuGet ou comprovado refresh integral de cache; não se declara uma versão específica da base. O Scout consultou metadados de atualização e anunciou 1.29.0; não retornou inventário ou CVEs.

**Resultado:** nenhum advisory reportado na consulta NuGet do grafo local da API. Auditoria de imagem/SO/shared framework não concluída por indisponibilidade das ferramentas operacionais. Não há contagem confiável de CVEs da imagem.

## 8. Vulnerabilidades identificadas e triagem

| Severidade | Confirmadas no container | Resultado |
| --- | --- | --- |
| Críticas | Não determinado | Sem scan da imagem |
| Altas | Não determinado | Sem scan; exemplos condicionais abaixo |
| Moderadas | Não determinado | Sem scan da imagem |
| Baixas | Não determinado | Sem scan da imagem |
| Sem classificação | Não determinado | Lacunas de inventário não são CVEs |

Nenhuma CVE foi confirmada como instalada e explorável no container. Os dois exemplos oficiais abaixo orientam a próxima coleta; não são uma lista exaustiva dos advisories .NET nem entram em uma contagem de falhas do SeenIt.

| Advisory | Pacote/faixa e correção | Origem e versão instalada | Relevância/condição/impacto |
| --- | --- | --- | --- |
| [CVE-2026-109475](https://github.com/dotnet/announcements/issues/500), High 7.5 | Microsoft.NETCore.App.Runtime.linux-x64 até 10.0.14; corrigido 10.0.15 | Shared framework da base runtime; versão Linux desconhecida | DoS por linha HTTP incompleta em HttpListener gerenciado Unix. API usa Kestrel; não foi identificado uso de HttpListener no código revisado. Aplicabilidade ao container e caminho explorável não demonstrados. |
| [CVE-2026-114155](https://github.com/dotnet/announcements/issues/502), High 7.5 | SDK dotnet-watch 10.0.300–303 e 10.0.400–403; corrigido 10.0.404 | Base SDK de build com versão desconhecida; SDK local Windows 10.0.301 não prova versão Linux | Atacante local precisa alterar arquivo temporário de solução usado por dotnet-watch. Dockerfile usa restore/publish, não watch; SDK não é herdado por final. Triagem de build, sem exposição runtime demonstrada. O advisory distingue o escopo Windows. |

A [consolidação Microsoft de novembro](https://github.com/dotnet/announcements/issues/498) exige avaliação por componente; atualizar shared framework não corrige automaticamente cada pacote independente. A [página de segurança SkiaSharp](https://github.com/mono/SkiaSharp/security) não publicou advisories na consulta; isso não garante segurança dos codecs embutidos. Não foram atribuídas CVEs Chromium/Skia ao binário sem comprovar revisão e opções compiladas.

## 9. Riscos de configuração

| Achado | Avaliação e evidência |
| --- | --- |
| Tags móveis, sem digest ou patch registrado | Risco de rastreabilidade e atualização. Imagens diferentes podem corresponder à mesma tag; imagem antiga pode manter falhas já corrigidas. |
| Auditoria NuGet informativa no Dockerfile | `dotnet package list --vulnerable` não constitui gate de reprovação por severidade; build pode continuar com alertas. Cobre NuGet, não todo SO ou Skia nativo. |
| Possíveis DLLs de design/build no publish | Assets runtime presentes no grafo; confirmar artefato final antes de propor exclusão. |
| Usuário e permissões | `USER app` herdado por verify/final; /app e /app/certs 0755, CA 0444; código copiado sem `--chown` deve permanecer root-owned por padrão. Diretório DataProtection explicitamente gravável por app. UID efetivo e permissões resultantes não medidos. |
| Isolamento do processo | Sem declaração Dockerfile de filesystem read-only, capacidades ou limites de CPU/memória. São controles de execução; ausência no Dockerfile não demonstra ausência no Render. Não há evidência de execução privilegiada. |
| Contexto e segredos | Allowlist de três projetos e configuração/CA; exclui .env, secrets, credenciais, chaves privadas, appsettings locais, bin/obj, .git e frontend. Sem ARG secreto. Allowlist por diretório ainda aceita arquivos novos não cobertos por padrões; não é scanner de segredos. |
| Portas/processo | 10000 HTTP, binding 0.0.0.0 por PORT; EXPOSE não publica porta. ENTRYPOINT exec mantém dotnet como processo principal. TLS externo é apenas contrato documentado de Render. |
| Healthcheck | Sem HEALTHCHECK Docker; `/health` existe e documentação prevê health no Render. Endpoint não consulta banco/Storage; 200 não comprova funcionamento integrado ou confiança no proxy. Não é justificativa isolada para mudança cosmética. |
| Logs | Configuração sanitizada Information/Warning; host padrão usa console. Cliente Storage remove loggers e redige apikey/Authorization. Logs de cleanup contêm UserId/ObjectPath: controlar acesso e retenção. Não foram inspecionados logs reais. |
| Proxy/TLS | Código exige KnownProxies explícitos, um salto, sem confiança ampla. Documentação deixa confirmação da topologia pendente. Risco operacional de redirecionamento/esquema se configurado incorretamente; estado real desconhecido. |

A CA pública tem sujeito Supabase Root 2021 CA, RSA 2048/SHA-256, validade observada de 28/04/2021 a 26/04/2031 e `HasPrivateKey=false`; SHA-256 do certificado registrado por certutil: `807025ad50d4ed219d2c9c7d299c004f824eb00cf7f65afef607d07b72e6cafa`. Isso não valida servidor, cadeia remota ou revogação. Não há chave privada nesse certificado. Nenhum valor secreto foi copiado ao relatório. A revisão das regras e caminhos explícitos não comprova ausência de segredos em todas as camadas históricas.

## 10. Impacto prático

Os controles estáticos reduzem exposição: processo não-root, código não intencionalmente gravável, contexto restrito, checks nativos no build e separação de SDK/testes. O maior impedimento para uma decisão de segurança é desconhecer os patches e pacotes do artefato efetivo. A entrada de imagens controladas pelo usuário torna Skia/codecs uma prioridade real; comprometimento do processo poderia alcançar os segredos de runtime e as permissões concedidas à API, mesmo sem root. Essa é uma análise de impacto potencial, sem exploração demonstrada.

## 11. Alternativas de correção futura

1. Primeiro obter imagem identificável, digests e SBOM com ferramenta já disponível e autenticada; se indisponível, propor instalação separada, sem fazê-la nesta etapa.
2. Se houver componentes vulneráveis aplicáveis, atualizar bases/pacotes em experimento isolado, comparar inventários e validar os checks. Registrar digest da variante amd64 e política de renovação; fixar digest sem renovação não resolve obsolescência.
3. Confirmar conteúdo publicado e avaliar exclusão de EF Design e ramo MSBuild/Roslyn do runtime sem quebrar tooling/migrations e sem suprimir auditoria. Não remover Pkcs de forma isolada sem reavaliar o ramo dependente.
4. Criar política objetiva de falha de auditoria por gravidade/aplicabilidade, mantendo evidências e exceções justificadas. Não trocar a base por Alpine/chiseled apenas por tamanho; requer compatibilidade glibc/Skia/ICU/CA e novos testes.
5. Validar limites e isolamento disponíveis na hospedagem; considerar filesystem somente leitura com diretórios graváveis explícitos quando suportado e necessário.

Nenhuma alternativa foi implementada.

## 12. Priorização

| Prioridade | Ação | Critério |
| --- | --- | --- |
| P1 | Resolver coleta de imagem/runtime/SBOM e scanner | Necessário para conhecer exposição real; não equivale a CVE crítica confirmada |
| P1 | Relacionar Skia/codecs e patches .NET/SO ao artefato | Processamento de entrada não confiável e serviço HTTP |
| P1 condicional | Corrigir CVEs críticas/altas aplicáveis encontradas | Ordem depende de exploração, componente alcançável e correção disponível |
| P2 | Renovação controlada das bases/digests e gate de auditoria | Evitar desatualização e builds com alertas ignorados |
| P2 | Verificar/remover ferramentas de design do publish | Reduzir superfície após comprovação e testes |
| P2 | Conferir contrato proxy, limites e logging | Relevância operacional; produção não confirmada |

## 13. Plano de testes para B05-B4.2

1. Com Docker operacional, inspecionar primeiro a imagem local existente sem rebuild: ID, RepoDigests, arquitetura, User, Entrypoint, Env sanitizado, portas e histórico com cuidado para não divulgar segredos. Não confundir ID de imagem com digest de manifesto.
2. Gerar SBOM com Scout ou ferramenta aprovada e registrar versão, fonte/data da base e alvo imutável. Auditar runtime e build separadamente; guardar CVE, pacote, versão instalada/corrigida, camada, severidade e aplicabilidade. Investigar codecs embutidos que o scanner não identifica.
3. Obter versão do shared framework, `/etc/os-release`, pacotes deb e dependências de libSkiaSharp por extração offline ou execução isolada sem rede/segredos, conforme autorização da etapa. Conferir ausência de SDK, EF tooling dispensável, checks e SQLite no final.
4. Se necessário, preparar experimento de rebuild Linux amd64 com bases atualizadas e comparação de SBOM. Isso é atividade futura; não foi feito aqui. Rodar build completo com verify, AvatarChecks/CoreChecks e smoke do script `backend/docker/Test-Container.ps1` usando apenas configuração sintética e sem banco real.
5. Revalidar JPEG/PNG/WebP, arquivos malformados/truncados, MIME divergente, dimensões, animação, concorrência e uso de memória. Conferir usuário não-root, código/CA não graváveis e apenas diretórios necessários graváveis.
6. Validar inicialização Production, porta, `/health`, desligamento por sinal e ausência de segredos nos logs; separar testes de proxy de qualquer confirmação real do Render.
7. Somente propor correção concreta após os achados. Critério de aceite: inventário reprodutível, tratamento de altos/críticos aplicáveis, exceções documentadas e regressões aprovadas. Não incluir deploy ou acesso a serviços reais sem nova autorização.

## 14. Limitações e evidências não verificadas

- Sem daemon: não foram enumeradas imagens, inspecionadas camadas, executado ldd, obtidos pacotes Ubuntu ou medido patch .NET Linux. Sem Scout autenticado: nenhum scan de imagem ou SBOM concluído.
- O assets existente tem apenas target `net10.0`, sem target RID linux-x64; não representa resolução/publish Linux refeito nesta etapa. Grafo NuGet local pode divergir de uma construção passada ou futura. Não foi reexecutada auditoria dos checks, que não são copiados ao final.
- Ausência de alertas NuGet e ausência de advisories Skia publicados não garantem ausência de falhas. Triagem pública de exemplos .NET não substitui auditoria completa de todos os patches/componentes.
- Não se confirmou ausência absoluta de segredos ou conteúdo dispensável na imagem final. Foram revisadas regras e arquivos relevantes sem despejar configurações locais sensíveis.
- Produção: desconhecidos SHA/digest implantados, patch Linux, pacotes, overrides de usuário/porta, limites/capacidades, variáveis, topologia de proxy, logs e configuração efetiva de health/TLS. Somente documentação foi usada; nenhum painel/endereço de produção foi consultado.
- Evidência histórica em RENDER.md é histórica; não foi tratada como execução atual nem prova do estado publicado.

Recomendação: **B05-B4.2 deve começar pela coleta verificável do artefato e auditoria de imagem**, seguida de experimento de correção guiado por achados; não há base para atualização indiscriminada ou declaração de imagem aprovada.

Entrega documental concluída com limitações explícitas. Nenhum arquivo de aplicação/configuração foi modificado.

**B05-B4.1 DIAGNÓSTICO CONCLUÍDO**
