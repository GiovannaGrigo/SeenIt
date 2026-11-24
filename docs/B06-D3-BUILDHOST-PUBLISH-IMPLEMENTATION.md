# B06-D3 — Implementação controlada da exclusão dos BuildHosts

## 1. Objetivo

Aplicar ao projeto original o target aprovado no B06-D2 e validar a publicação Linux/amd64 pelo Dockerfile original. Em 24 de novembro de 2026, a implementação passou na validação local. A imagem nova contém 24 arquivos em `/app`, todos idênticos aos da candidata do B06-D2.

## 2. Estado inicial do Git

`main`, `origin/main` local e a consulta à referência remota coincidiram em `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c`. A principal tinha apenas os relatórios B06-D1 e B06-D2 não rastreados. Foram inventariados dez branches locais, duas worktrees e 246 arquivos da principal. A manutenção estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290` e correspondia ao inventário anterior de 161 SHA-256, seis arquivos rastreados modificados e nove entradas não rastreadas.

## 3. Referências B06-D1 e B06-D2

B06-D1 identificou os dois diretórios vindos da cadeia EF Design 10.0.0 → Workspaces.MSBuild 4.14.0 → contentFiles. B06-D2 demonstrou em controle e candidata que excluir seus caminhos relativos de publish removia 46 arquivos com os outputs de desenvolvimento preservados. Os testes, scans e medições abaixo foram repetidos no B06-D3. O controle e a candidata B06-D2 foram reinspecionados localmente, sem reconstruí-los. [B06-D1](B06-D1-BUILDHOST-ROSLYN-DIAGNOSIS.md) · [B06-D2](B06-D2-BUILDHOST-PUBLISH-EXPERIMENT.md)

## 4. Diff aplicado

`backend/SeenIt.Api/SeenIt.Api.csproj` é o único arquivo rastreado modificado. O arquivo original tinha SHA-256 `f8cd7eb891e0d714bd8219e474e190c3a9e693c3074307355791e6d89de78561`, igual ao controle experimental. A versão aplicada tem SHA-256 `6754d4950f047f10a609ef8502ba8c8397f2b7f899ea764c47516393c1bac45a`, igual byte a byte à candidata aprovada. BOM UTF-8 e padrão de quebras de linha preservados. O diff foi conferido contra o relatório antes da escrita e `git diff --check` passou.

```diff
@@ -20,4 +20,9 @@
     </PackageReference>
     <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
   </ItemGroup>
+  <Target Name="ExcludeBuildHostsFromApiPublish" AfterTargets="ComputeFilesToPublish">
+    <ItemGroup>
+      <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" Condition="$([System.Text.RegularExpressions.Regex]::IsMatch('%(ResolvedFileToPublish.RelativePath)', '^BuildHost-(netcore|net472)[/\\]'))" />
+    </ItemGroup>
+  </Target>
 </Project>
```

## 5. Funcionamento do target

A regra executa depois de `ComputeFilesToPublish`. Ela usa `ResolvedFileToPublish.RelativePath` e exige o nome exato de um dos dois diretórios na raiz, seguido de barra ou contrabarra. A avaliação isolada `dotnet msbuild -t:ComputeFilesToPublish -getItem:ResolvedFileToPublish` confirmou **66 → 20 itens** em relação ao controle. Os únicos 46 itens desaparecidos estavam sob os dois diretórios. As contagens do publish efetivo são **68 → 22**, incluindo arquivos fora dessa consulta direta de itens. O build Debug reteve os 46 arquivos.

## 6. Integridade de dependências

Os objetos `targets` de `project.assets.json` para `net10.0` e `net10.0/linux-x64` coincidiram com o controle Linux, com 51 entradas em cada alvo. Referências, versões, `PrivateAssets` e `IncludeAssets` permanecem iguais. `SeenIt.Api.deps.json` conserva o SHA-256 da candidata B06-D2. Permanecem EF Core/Npgsql 10.0.0, SkiaSharp e NativeAssets.Linux.NoDependencies 4.155.2. O deps auxiliar do BuildHost desapareceu junto com seu diretório.

## 7. Identidade da imagem

- Tag local `seenit-api:seenit-b06-d3-b3ae7d6-67eb11`.
- Índice `sha256:87ff7408e413bddbd5238ad11ed22b6462be04e83100e7298e410c68c5d46a6a`, manifesto Linux/amd64 `sha256:0c868e971e28b9887b4821cf26f74d390be8739165eeefef9e5f6082030e964c`.
- SDK 10.0.404 no build e shared frameworks .NET/ASP.NET 10.0.15 na final.
- Usuário `app`, UID/GID 1654, porta 10000/tcp, entrypoint original `dotnet SeenIt.Api.dll`.
- CA legível em 0444, marcador verify presente, sem SDK nem diretórios BuildHost no runtime.

## 8. Arquivos publicados

A leitura direta dos estágios e das imagens mostrou 68 → 22 arquivos em `/out/api`, 70 → 24 arquivos em `/app` e nenhum diretório BuildHost residual. Nos dois pares, a única diferença de caminhos foi a ausência de 25 arquivos net472 e 21 netcore. A CA e o marcador são as duas adições do estágio final ao publish.

## 9. Hashes comparados

Os 22 arquivos publicados coincidiram entre `/out/api` e `/app`. Os 24 arquivos finais coincidiram em caminho, tamanho e SHA-256 com a candidata B06-D2 e com os mesmos caminhos presentes no controle. Caminhos relativos a `/app`.

| Arquivo | Bytes | SHA-256 |
|---|---:|---|
| `.container-checks-passed` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `Microsoft.AspNetCore.Authentication.JwtBearer.dll` | 52536 | `05494def3c48b22a43367dfc1bcb1fe03ac560cd140763023844a84012adb215` |
| `Microsoft.AspNetCore.OpenApi.dll` | 194872 | `889e7c608091f27bdcc641efc92c22f85dfc5334ef50ddbaa5badb0e3a527c0f` |
| `Microsoft.EntityFrameworkCore.Abstractions.dll` | 34848 | `ce76bce9dcc06fc6e22d6c4abebb4ea816a0079b10485c5c080d6ea9f0fd818e` |
| `Microsoft.EntityFrameworkCore.Relational.dll` | 2190368 | `68803fa0a71d4a9d58325ddde1d1709451094a20b01cbdb3b73d69f42d115197` |
| `Microsoft.EntityFrameworkCore.dll` | 2821152 | `785aaebff28acdafc80f4fd2a7521392bb67afd8069b89b3aa8d7c5627f49c60` |
| `Microsoft.IdentityModel.Abstractions.dll` | 19488 | `c3b97d1b1c42faa95bb92eabb7511ca61e89075388f122197040fcfdfb492013` |
| `Microsoft.IdentityModel.JsonWebTokens.dll` | 143392 | `e85d3d751b4254771f59ff8a14444ef701ba65efb84e312281d675c44536da71` |
| `Microsoft.IdentityModel.Logging.dll` | 36784 | `32ebeeb2163db6aa0a81659ff0b590894d78350c50c459196b6cc33c98ba21f3` |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect.dll` | 137144 | `d1f7dbbb71cbf69f5242a74e43b632610ff5dca913f164d1dcf53b2f6e0046ac` |
| `Microsoft.IdentityModel.Protocols.dll` | 40880 | `7c8702a7f6c019d962faf4b8af2da751dc32af9b6be998688e774394fb5f8730` |
| `Microsoft.IdentityModel.Tokens.dll` | 329248 | `b2dacc5120971efceb60f6febfe9685fe5a509ddb803c45956a63fc36e67631b` |
| `Microsoft.OpenApi.dll` | 486752 | `64497b0a954493014d15494bb89f09dbed059b13efe64cdf008d5f0f6bf197e5` |
| `Npgsql.EntityFrameworkCore.PostgreSQL.dll` | 676864 | `0b8ca4484e12f1cd119fc6b4711015ba6ce2364f9890504f6f4f13036c2075bf` |
| `Npgsql.dll` | 1477632 | `9d8db086e9eda5ff20c4a7f83fa3eae7689198f57277ff9ceea1ee3526f5fcf8` |
| `SeenIt.Api.deps.json` | 12179 | `03b7f83a87d0a5048af7e8b5a95df5749d9a757aa2895ae04b5038564d7b2da5` |
| `SeenIt.Api.dll` | 284160 | `63b81c9f6da07ec8ce27ada5fa78b3e8e948b9075911efdff822cc4d00506c88` |
| `SeenIt.Api.runtimeconfig.json` | 540 | `eff38a9750f7cca2407b1d639a963d56ffea92088e6da971bbbfa74e3dcd7224` |
| `SeenIt.Api.staticwebassets.endpoints.json` | 53 | `c1686417e8d5c31bba969f12b6f3d2b35e6609adb5ca42f212261a3a02771aac` |
| `SkiaSharp.dll` | 560440 | `d1a77d5f16c95e42e58d05edea34019fe7cfcea3d10aa22e0a4d75b4a4a45dd8` |
| `System.IdentityModel.Tokens.Jwt.dll` | 90032 | `83218e29958e265b4eebce62cda6d69d41e7bf1898b7fa21ed52102675f6dfe3` |
| `appsettings.json` | 190 | `75d288131ba1bf81f8f3e0f72ba24684809ffd9ceef0fdddda42212ac0538866` |
| `certs/prod-ca-2021.crt` | 1390 | `1dcaafbf6fda7f21e34ff35825c1a1354408ea11e5839157e690af851c73453a` |
| `libSkiaSharp.so` | 12289656 | `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef` |

## 10. Tamanho e compressão

Medições novas da imagem B06-D3 e do controle B06-D2 local reinspecionado. Valores em bytes.

| Medição | Controle | B06-D3 | Redução |
|---|---:|---:|---:|
| Publish lógico | 26.104.977 | 21.879.210 | 4.225.767 |
| Arquivos regulares em `/app` | 26.106.367 | 21.880.600 | 4.225.767 |
| Arquivos regulares no filesystem visível | 259.431.098 | 255.205.331 | 4.225.767 |
| Tars descomprimidos das camadas OCI | 276.101.632 | 271.820.288 | 4.281.344 |
| Blobs gzip das camadas OCI | 113.945.418 | 112.304.997 | 1.640.421 (1,44%) |
| Blob gzip do COPY da API | 9.976.553 | 8.336.136 | 1.640.417 |
| Campo `.Size` do Engine | 400.760.331 | 394.692.134 | 6.068.197 |

Foram comparados os descritores e tamanhos reais dos blobs no export `docker save`. Os tars descomprimidos foram medidos pelo `ISIZE` gzip, com camadas abaixo de 4 GiB. A soma de gzip exclui manifests e atestações. Nenhum tráfego a registry foi medido. A diferença de 31 bytes comprimidos em relação à candidata B06-D2 está nos blobs recriados pelo build, apesar de igualdade dos conteúdos em `/app` e dos tamanhos tar.

## 11. Build e verify

Build sem cache executado em Linux/amd64 usando arquivos do `git archive HEAD` e apenas o csproj original atualizado sobreposto à cópia sanitizada. Dockerfile SHA-256 `078a25c4c04ee235e844226dcd97883b067925e3797df3ec5f6796afebc7bf5e`. Bases fixas `sdk:10.0-noble@sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` e `aspnet:10.0-noble@sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde`. Restore, publish Release, verify, SkiaSharp em `cmp` e `ldd`, AvatarChecks, CoreChecks e validação da CA passaram. A exportação posterior do estágio `build` reutilizou o cache da construção para inspeção. A primeira consulta MSBuild direta falhou em filesystem read-only e passou em container com escrita efêmera.

## 12. Testes de imagem

AvatarChecks reportou **16 grupos aprovados**. ExtraAvatar aprovou **três grupos** de teste, incluindo WebP animado, gate com cancelamento e limite de 16 MP. CodecExperiment reportou **37 registros** com sucesso, incluindo JPEG, PNG, WebP, transparência, EXIF, resize, reencode, limites de 5 MiB/dimensões/pixels, MIME falseado, truncamento/corrupção, APNG tratado como PNG estático, cancelamento e recuperação. O hash nativo coincide com a referência exigida e o deps retém 4.155.2. Não houve mudança no código de processamento.

## 13. Testes da API

CoreChecks passou tempo assistido, classificação de anime e **72 casos HTTP** de cadastro. RegisterIntegrationChecks passou catálogo e biblioteca, integridade, rate limiting, concorrência, cancelamento, headers falsificados, login e perfil. A suíte registrou **73 casos HTTP de cadastro, 16 usuários persistidos em SQLite em memória** e o ensaio de **20 logins sobrepostos**. EntryPointChecks passou **174/174 asserções**, **72 requisições** e **seis perfis**, cobrindo a opção de edge ausente, false e true, com e sem porta sintética, autenticação e redirects. AvatarChecks cobriu upload e substituição simulados, falhas e compensação de Storage.

Os harnesses suplementares históricos foram executados de novo. A cópia temporária foi recomposta com DLLs de teste das versões exatas dos deps, incluindo Mvc.Testing/TestHost 10.0.7 e SQLite nativo Linux 2.1.12. A primeira tentativa falhou por dependência gerenciada ausente. A próxima revelou a ausência do nativo de SQLite. O ensaio completo foi repetido após repor ambos em temporário e passou. Antes de executar, as DLLs e o nativo da aplicação foram copiados da nova imagem e conferidos byte a byte no diretório efetivo dos testes. Os harnesses permanentes e históricos ficaram intactos.

## 14. PostgreSQL

O modo `--postgres-only` passou em PostgreSQL **17.11** descartável, localhost, credenciais sintéticas, sem portas publicadas. Via Npgsql e EF Core, validou DbContext, mapeamentos, consultas, inserts, updates, transações, rollback após SQL, isolamento, concorrência, retry, erro real 23505 e conflito HTTP 409. O schema foi criado pelo fixture com `EnsureCreated`. Nenhuma migration foi aplicada. Houve aviso sobre falta de `libgssapi_krb5.so.2`; a autenticação por senha funcionou e GSS ficou fora do ensaio.

## 15. Tooling EF

O SDK Linux 10.0.404 construiu Debug sem warnings/erros, com **46 arquivos BuildHost**. Após restaurar uma cópia temporária completa do dotnet-ef **10.0.0**, a ferramenta rodou com rede desligada, descobriu `SeenIt.Api.Data.AppDbContext` e Npgsql, listou offline as três migrations `InitialPostgres`, `AddSeriesFavorites` e `AddSeriesAnimeClassification` e confirmou ausência de alterações pendentes no modelo. O script idempotente tem SHA-256 `823349a79b009fb21b9503d841c5bfbb9b8355cabdb37876abb6f05e50df7a17`, igual ao controle B06-D2. O primeiro ensaio localizava a versão do SDK e os hosts Debug, mas a cópia antiga do dotnet-ef estava incompleta. O resultado aprovado é o da ferramenta restaurada, com código 0.

## 16. Smoke e HTTPS

Production, UID 1654, porta 10000, CA 0444 legível, ausência de BuildHosts e SDK e health HTTP 200 confirmados. O container de smoke limitou CPU a 2 e memória a 512 MiB, teve shutdown normal e retornou exit 0 com `OOMKilled=false`. Um proxy local temporário em rede isolada testou terminação real TLS 1.3 com `HttpsRedirectionHandledByEdge=true`, health 200, perfil com JWT inválido 401 e preflight CORS 204, sem redirects. A amostra de `/proc/1/maps` mostrou os assemblies esperados, incluindo EF Core/Npgsql, sem BuildHost ou Roslyn.

## 17. Auditoria NuGet/Scout

NuGet audit novo com transitivos para API, AvatarChecks, CoreChecks e RegisterIntegrationChecks não reportou pacotes vulneráveis nas fontes consultadas. Scout 1.28.0 indexou **160 pacotes** e reportou **17 CVEs (0 Critical, 0 High, 13 Medium, 4 Low)** em nove pacotes afetados. A comparação de SARIF por ID, severidade e mensagem com a candidata B06-D2 não mostrou CVEs exclusivas. O SPDX contém 192 registros de pacote, sem BuildHost ou Microsoft.Build.Locator. A comparação por nome, versão, fornecedor e finalidade divergiu apenas no registro da tag do container.

## 18. Comparação com B06-D2

A nova construção reproduziu os 22/24 arquivos, os SHA-256 da candidata e as versões de execução. A economia lógica reproduziu os 4.225.767 bytes históricos. A economia comprimida medida nesta etapa foi 1.640.421 bytes, contra 1.640.452 no B06-D2. Os resultados de testes e auditorias do B06-D3 são de execuções novas. O controle histórico foi usado como referência física para hashes, camadas e itens MSBuild.

## 19. Riscos residuais

O scan continua apontando os 17 avisos já inventariados da imagem e não demonstra eliminação de vulnerabilidades. A observação de assemblies é amostral. Os testes de desenvolvimento cobrem o fluxo de migrations do projeto. Um consumidor externo que use o diretório publicado como ambiente de design-time pode precisar do output Debug ou de um build com o target revertido.

## 20. Limitações

Validação local em Linux/amd64, com serviços de teste e sem comparação com o artefato implantado. JWT, HTTPS, persistência e imagens foram exercitados pelos fluxos descritos. Não houve autenticação PostgreSQL por Kerberos, comunicação com Storage real nem medição de tráfego de imagem. Os scanners não abrangem integralmente componentes incorporados.

## 21. Rollback futuro

Remover somente as cinco linhas do target e reconstruir com os mesmos digests. A conferência pode usar os 68 arquivos do publish e os 70 de `/app` do controle existente. O grafo já era igual antes e depois dessa alteração.

## 22. Recomendação B06-D4

Revisar o diff e as evidências para a próxima etapa autorizada. Em eventual integração, manter a mudança limitada ao target e exigir a conferência de hashes do publish, verify, smoke e tooling EF offline no HEAD revisado.

## 23. Preservação do repositório

`main` e `origin/main` permanecem em `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c`. Apenas o csproj da API está modificado entre os rastreados. Os três relatórios B06-D1, B06-D2 e B06-D3 estão não rastreados. Os demais 245 hashes preexistentes da principal e os 161 de manutenção foram preservados, junto com HEAD/status, branches e worktrees. Dockerfile, Program.cs, JWT, frontend, migrations e testes permanentes intactos. Nenhum commit, push, PR, merge ou deploy.

Foram reencontradas as 26 referências de imagem iniciais, os dois containers, quatro volumes e as 258 entradas de cache preexistentes. Duas tags B06-D3 ficaram para reprodução local, a final e a do estágio build de evidência. Os containers temporários de teste foram encerrados. Logs, inventários, export OCI, manifests SARIF/SPDX e fontes isoladas estão em `%TEMP%/seenit-b06-d3-b3ae7d6-67eb11`.

**B06-D3 IMPLEMENTAÇÃO VALIDADA**
