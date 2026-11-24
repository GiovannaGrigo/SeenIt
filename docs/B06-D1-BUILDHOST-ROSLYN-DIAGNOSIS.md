# B06-D1 — Diagnóstico de BuildHost, Roslyn e dependências de design-time

## 1. Objetivo

Determinar por que ferramentas de design-time chegam à imagem da API e se há fundamento para reduzir o artefato. Diagnóstico realizado em 24 de novembro de 2026. A inspeção confirma **46 arquivos BuildHost, 4.225.767 bytes (4,03 MiB)** no runtime local de referência. Os hosts vieram do publish e ficaram fora do grafo de execução da API. O próximo passo proposto é um experimento de exclusão restrito à publicação.

## 2. Estado inicial do Git

`main` limpa em `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c`, igual a `origin/main` local e à referência consultada no GitHub na primeira tentativa. O estado local foi reconfirmado na retomada. Havia dez branches locais e duas worktrees. Foram coletados SHA-256 de 244 arquivos rastreados na principal e 161 arquivos na manutenção, incluindo não rastreados não ignorados.

A worktree `.worktrees/maintenance-phase1` estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`. Os seis arquivos modificados eram `Data/AppDbContext.cs`, `Program.cs`, `Services/Avatars/AvatarProfileService.cs` e `Services/Avatars/IAvatarReferenceStore.cs` na API, e `RateLimitingRegressionChecks.cs` e `RegistrationFactory.cs` nos checks de integração. As nove entradas não rastreadas se agrupavam em `Maintenance/`, `SeenIt.MaintenanceAdmin/`, `SeenIt.MaintenanceChecks/`, `docs/MAINTENANCE-PHASE1.md`, `docs/RENDER-FREE-OPERATIONS.md` e `operations/`. O estado e os 161 hashes coincidiram com o snapshot histórico `%TEMP%/seenit-b06-c6-before.json`.

## 3. Histórico relevante

- B05-B4 identificou os dois hosts, Locator e bibliotecas auxiliares na imagem histórica `render-check`. O SBOM e a inspeção registraram a ausência de SDK, EF Design DLL e checks no artefato. Os 36 alertas do scan posteriormente autenticado incluíam Ubuntu e shared frameworks.
- B06-B1 consolidou versões, camadas e riscos de base. B06-B2 comparou controle e candidata alterando a base ASP.NET. B06-B3 confirmou integridade dos 70 arquivos em relação à candidata do B06-B2, ausência do SDK e dos checks na final e permanência dos BuildHosts.
- B06-B-FINAL registrou queda de 24 para 17 alertas Scout na comparação B06-B2, com sete alertas de base corrigidos. Os 17 residuais envolviam componentes da base, com limitações de alcance, correções e cobertura.
- B06-C3 validou SkiaSharp e seu native Linux 4.155.2. Os 70 arquivos tinham os mesmos hashes da candidata B06-C2. B06-C-FINAL registrou 176 pacotes indexados, 0 críticos, 0 altos, 13 médios, 4 baixos, e auditoria NuGet sem vulnerabilidades reportadas naqueles projetos. Riscos residuais incluem a cobertura parcial dos codecs ligados estaticamente e itens já documentados na base.

Esses resultados de testes e scanners são históricos. A coleta B06-D1 comparou novamente os arquivos e consultou o feed público do NuGet.

## 4. Inventário de pacotes

Versões do `project.assets.json` exportado do B06-C3, alvo `net10.0/linux-x64`, complementadas pelo cache NuGet local, manifests e assemblies do export de `/app`.

| Componente | Versão efetiva e função | Imagem final inspecionada |
|---|---|---|
| Microsoft.EntityFrameworkCore.Design | 10.0.0, scaffolding e ferramentas EF | DLL ausente |
| Microsoft.CodeAnalysis.Common, CSharp, CSharp.Workspaces, Workspaces.Common, Workspaces.MSBuild | 4.14.0, compilador e workspaces usados por tooling | Assemblies principais ausentes |
| Microsoft.CodeAnalysis.Analyzers | 3.11.0, análise de build | Ausente |
| Microsoft.Build; Framework; Tasks.Core; Utilities.Core | 17.7.2 para o pacote principal; 17.14.28 para os outros três | DLLs desses quatro pacotes ausentes |
| Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost | 4.14.0-3.25262.10, versão informacional e deps próprio | Host .NET e .NET Framework presentes |
| Microsoft.Build.Locator | 1.6.10, localização do toolset | Uma cópia por host |
| Newtonsoft.Json; System.CommandLine; System.Collections.Immutable | 13.0.3; 2.0.0-beta4.24528.1; 9.0.0 | Cópias dos hosts presentes |
| Microsoft.IO.Redist | ProductVersion 6.0.32 | Somente no host net472 |
| System.Buffers, Memory, Numerics.Vectors, Runtime.CompilerServices.Unsafe, Threading.Tasks.Extensions | Bibliotecas auxiliares empacotadas no net472. Unsafe informa 6.0.0. Para as outras, a versão NuGet exata do binário net472 não foi comprovada | Presentes no net472 |
| Mono.TextTemplating; System.Security.Cryptography.Pkcs | 3.0.0; pin NuGet 9.0.22 | Nenhuma das DLLs veio do publish. Pkcs existe no shared framework ASP.NET 10.0.15 |
| dotnet-ef | 10.0.0, manifesto local de ferramentas | Manifesto e executável ausentes |
| EF Core, Abstractions, Relational; Npgsql e provider EF | 10.0.0, persistência da API | Presentes no deps da API e em `/app` |

O deps do BuildHost netcore também registra dependências de análise internas como Microsoft.VisualStudio.Threading.Analyzers 17.13.2 e Roslyn.Diagnostics.Analyzers. As DLLs correspondentes não foram encontradas no runtime. A busca por nomes no grafo e na imagem não encontrou Microsoft.CodeAnalysis.BuildTasks nem Microsoft.EntityFrameworkCore.Tools.

## 5. Dependências diretas e transitivas

`SeenIt.Api.csproj` referencia EF Design diretamente, com `PrivateAssets=all` e `IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`. O pacote está no grafo próprio da API. `PrivateAssets` controla propagação aos consumidores da referência e o projeto ainda admite uso de conteúdo conforme os metadados de cada aresta. O pin privado Pkcs 9.0.22 sobrepõe solicitações inferiores do grafo de design. O provider Npgsql é referência direta e traz o EF Core de runtime. AvatarChecks, CoreChecks (`SeenIt.Checks`) e RegisterIntegrationChecks referenciam a API. Seus outputs locais contêm cópias dos hosts. O projeto responsável pelo conteúdo da imagem final é a API.

EF Design pede CSharp, CSharp.Workspaces e Workspaces.MSBuild 4.14.0. Workspaces.MSBuild pede Workspaces.Common e as dependências MSBuild. EF Design também pede Framework, Tasks.Core e Utilities.Core 17.14.28, prevalecendo sobre os mínimos 17.7.2 declarados pelo workspace para esses três. O pacote principal Microsoft.Build resolve em 17.7.2. O csproj não define `ExcludeAssets`, `RuntimeIdentifier` nem `CopyLocalLockFileAssemblies`. O RID Linux vem do Dockerfile.

## 6. Origem de BuildHost/Roslyn

A cadeia é `SeenIt.Api → Microsoft.EntityFrameworkCore.Design 10.0.0 → Microsoft.CodeAnalysis.Workspaces.MSBuild 4.14.0 → contentFiles/any/any/BuildHost-*`. No nuspec de Workspaces.MSBuild, os arquivos declaram `buildAction=Content` e `copyToOutput=true`. O alvo Linux do assets registra `copyToOutput` e `outputPath` para os diretórios. A versão dos hosts aponta para a revisão Roslyn `8edf7bcd4f1594c3d68a6a567469f41dbd33dd1b`, que consta no nuspec do pacote. O host netcore declara net6.0 com roll-forward Major, enquanto o net472 destina-se ao .NET Framework. São processos auxiliares para avaliação de projetos via MSBuildWorkspace, com seus próprios binários e bibliotecas. A compilação normal usa o compilador do SDK. O tooling EF pode usar workspace e geração de código, especialmente fluxos avançados de design-time, o que exige preservar os outputs de desenvolvimento. [Relato upstream Roslyn](https://github.com/dotnet/roslyn/issues/80127) · [Discussão EF Core](https://github.com/dotnet/efcore/issues/37032)

## 7. Análise do Dockerfile

- `build` usa SDK Noble fixado por digest, restaura API, AvatarChecks e CoreChecks para linux-x64, publica Release framework-dependent em `/out/api`, `/out/avatar-checks` e `/out/core-checks`. Publish sem apphost e sem trimming. API sem símbolos no publish. O arquivo de configuração do container substitui o appsettings publicado. O estágio contém a auditoria NuGet transitiva.
- `runtime-base` usa ASP.NET Noble fixado por digest, configura Production, porta 10000 e usuário `app`.
- `verify` copia a API e os dois checks, compara SkiaSharp gerenciado e nativo, valida `ldd`, executa os checks e gera marcador. RegisterIntegrationChecks fica fora do Dockerfile.
- `final` copia a saída inteira `/out/api/` de `build`, o marcador de `verify` e a CA explícita. Valida a CA como `app` e inicia `dotnet SeenIt.Api.dll`. `.dockerignore` exclui bin/obj e o manifesto dotnet-tools. O COPY final segue o resultado selecionado pelo publish da API.

## 8. Comparação build, publish e runtime

| Ponto de observação | Evidência |
|---|---|
| A — SDK/build | SDK 10.0.404 no estágio histórico `seenit-api:b06-c3-build-evidence-f4954499` (`sha256:c477a2951cffdd0d372566f4216b167a8e367acdff760e3b61ad9ad55d5ea1d5`). `csc.dll` e `VBCSCompiler.dll` em `/usr/share/dotnet/sdk/10.0.404/Roslyn/bincore/`. |
| B — intermediário | Cópias net472 e netcore em `backend/SeenIt.Api/bin/Release/net10.0/`, Debug e em outputs locais de checks. Esses artefatos ignorados podem ter origem ou restore mais antigos. |
| C — publish | `/out/api` do estágio histórico contém 68 arquivos, dos quais 46 BuildHost. |
| D — final | `/app` contém os mesmos 68, com SHA-256 idêntico para cada caminho, mais a CA e o marcador. Não há SDK na listagem de `dotnet --list-sdks`. Shared frameworks são Microsoft.NETCore.App e Microsoft.AspNetCore.App 10.0.15. Busca no filesystem e no PATH não encontrou `csc`, `VBCSCompiler`, `msbuild`, `mono` ou `dotnet-ef`. |
| E — processo | Após startup e health, `/proc/<pid>/maps` registrou API, EF, Npgsql, dependências JWT/OpenAPI e outros assemblies. Nenhum mapeamento BuildHost, Roslyn ou Microsoft.Build identificado na amostra. |

A tag histórica `seenit-api:b06-c3-validation-f4954499` retornou ID `sha256:5e552e9974d0a3ce7281d3cca4465aa0268a729b1292115b3fe1ebff564eac8c`, Linux/amd64, usuário `app`, porta 10000 e entrypoint esperados. O ID coincide com B06-C3. A revisão curta de origem da tag não está resolvível no Git local. Foram confrontados os 77 arquivos do contexto arquivado no B06-C3 com a árvore atual, sem diferença de conteúdo após normalizar CRLF/LF nos arquivos textuais discrepantes. A imagem continua identificada como validação histórica com contexto equivalente, sem atribuição de build ao HEAD exato ou ao Render.

## 9. Inventário dos 70 arquivos encontrados

Caminhos relativos a `/app`. SHA-256 calculado no container e conferido com o export histórico usado para os tamanhos. O par BuildHost contém 25 arquivos net472 e 21 netcore, incluindo 26 satélites do System.CommandLine. O deps da API registra EF/Npgsql, SkiaSharp e demais dependências funcionais; o deps próprio do host registra Locator e dependências auxiliares.

| Caminho | Bytes | SHA-256 |
|---|---:|---|
| `.container-checks-passed` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `appsettings.json` | 179 | `79663ada6f808534d06eed66f746c9fb1bba2d3415333a07b53d4f8ab6fc09a3` |
| `BuildHost-net472/cs/System.CommandLine.resources.dll` | 18992 | `0613e63dea0dfc46b8a090ee41ca0ac93902fdf876107fe116697f57ff997ecb` |
| `BuildHost-net472/de/System.CommandLine.resources.dll` | 18984 | `6f77e87cdd5729da8ef9f4f7ded1424667ad81711e0ff80c264707a522daab6a` |
| `BuildHost-net472/es/System.CommandLine.resources.dll` | 18992 | `99d6cfb2a639bec41c755dfa1942ca42b23e3272411392468d5d21ce8582daa8` |
| `BuildHost-net472/fr/System.CommandLine.resources.dll` | 18976 | `3f5b7616477a792b1f58b820527bab0b85b621398bf027840368d5325c44e158` |
| `BuildHost-net472/it/System.CommandLine.resources.dll` | 18992 | `70b1cd23221e5f467bb39d5ede5b3b6668766a0a659428254b208d5930528c67` |
| `BuildHost-net472/ja/System.CommandLine.resources.dll` | 19016 | `a60f1b1b73bf8227dafcb1fd187580c9dcb05d26ccfc0d0fcde1a684e5c7b5fb` |
| `BuildHost-net472/ko/System.CommandLine.resources.dll` | 19016 | `14c738c5c21287e3232032565be6bb45ccdfa81bcafe14cbd2ec27fca60b2bf2` |
| `BuildHost-net472/Microsoft.Build.Locator.dll` | 34848 | `1d4222a33857c46f3f0e3c8e59e2adc06f6c98d9072802c7cfd8e0dfda74847b` |
| `BuildHost-net472/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe` | 446344 | `bea27af3980b0fdfbf302423d55553498a0241f43524356025d477902ba982f5` |
| `BuildHost-net472/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe.config` | 2901 | `43169e9dbfdd4d06d2d4f0f89c9794d43453b096bb9ae6a275c5e3739383d925` |
| `BuildHost-net472/Microsoft.IO.Redist.dll` | 131232 | `6d0b0a4ab6fca41c97b0993f053bc8ea5ee965a8ff5a2ed42d15131633752d5f` |
| `BuildHost-net472/Newtonsoft.Json.dll` | 722304 | `bcb1dd23d554d3a4e17b2c0f08761fc571857de37291b9bdd8c91788be3aa842` |
| `BuildHost-net472/pl/System.CommandLine.resources.dll` | 19000 | `29d28dc053aeb5d2c320f15331be4c7efa4cb1643e5f8ead92a2e145ebd92e2b` |
| `BuildHost-net472/pt-BR/System.CommandLine.resources.dll` | 18976 | `7ffb47c41090d7b9e4da2f2c74557c186604822e904bf029683b2b565746af96` |
| `BuildHost-net472/ru/System.CommandLine.resources.dll` | 19512 | `cda0d35f61f5d60b29f6b2dd8ba8ab8f65b0ef8976df04f0657f90c1bd572592` |
| `BuildHost-net472/System.Buffers.dll` | 20856 | `accccfbe45d9f08ffeed9916e37b33e98c65be012cfff6e7fa7b67210ce1fefb` |
| `BuildHost-net472/System.Collections.Immutable.dll` | 259360 | `77d705fe51f224d77b25fc06ede39e2921f08b46a961a027d6ee49a4bb19c127` |
| `BuildHost-net472/System.CommandLine.dll` | 148504 | `ef4bc45850128e2351bdae75ae736a62a2ba0e6a3dca75b5a8bf2b0bc7deac3d` |
| `BuildHost-net472/System.Memory.dll` | 142240 | `bf3fb84664f4097f1a8a9bc71a51dcf8cf1a905d4080a4d290da1730866e856f` |
| `BuildHost-net472/System.Numerics.Vectors.dll` | 115856 | `1d3ef8698281e7cf7371d1554afef5872b39f96c26da772210a33da041ba1183` |
| `BuildHost-net472/System.Runtime.CompilerServices.Unsafe.dll` | 18024 | `37768488e8ef45729bc7d9a2677633c6450042975bb96516e186da6cb9cd0dcf` |
| `BuildHost-net472/System.Threading.Tasks.Extensions.dll` | 25984 | `4f81ffd0dc7204db75afc35ea4291769b07c440592f28894260eea76626a23c6` |
| `BuildHost-net472/tr/System.CommandLine.resources.dll` | 18992 | `06f12a7373d17d8e4c70dad2d3c1b8280a69242d39be2e86c691e0819306a7d8` |
| `BuildHost-net472/zh-Hans/System.CommandLine.resources.dll` | 18976 | `a4c168c6a73d9755ddc72c541ba6bcccc1aa2a8040b84e3eae1900e23d9752e1` |
| `BuildHost-net472/zh-Hant/System.CommandLine.resources.dll` | 19016 | `dacb3b425faff62c7bc1854e27ab8133a28aa4a8a98185bbf06c0b24fd82046a` |
| `BuildHost-netcore/cs/System.CommandLine.resources.dll` | 18992 | `0613e63dea0dfc46b8a090ee41ca0ac93902fdf876107fe116697f57ff997ecb` |
| `BuildHost-netcore/de/System.CommandLine.resources.dll` | 18984 | `6f77e87cdd5729da8ef9f4f7ded1424667ad81711e0ff80c264707a522daab6a` |
| `BuildHost-netcore/es/System.CommandLine.resources.dll` | 18992 | `99d6cfb2a639bec41c755dfa1942ca42b23e3272411392468d5d21ce8582daa8` |
| `BuildHost-netcore/fr/System.CommandLine.resources.dll` | 18976 | `3f5b7616477a792b1f58b820527bab0b85b621398bf027840368d5325c44e158` |
| `BuildHost-netcore/it/System.CommandLine.resources.dll` | 18992 | `70b1cd23221e5f467bb39d5ede5b3b6668766a0a659428254b208d5930528c67` |
| `BuildHost-netcore/ja/System.CommandLine.resources.dll` | 19016 | `a60f1b1b73bf8227dafcb1fd187580c9dcb05d26ccfc0d0fcde1a684e5c7b5fb` |
| `BuildHost-netcore/ko/System.CommandLine.resources.dll` | 19016 | `14c738c5c21287e3232032565be6bb45ccdfa81bcafe14cbd2ec27fca60b2bf2` |
| `BuildHost-netcore/Microsoft.Build.Locator.dll` | 43568 | `0adbe65d87bafc46e9ae7fbd4dc40ada05315c5dde6709eef578233fb3f4f62a` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.deps.json` | 11309 | `a63d1edce0775c67277e25d5c65864adefa18d5eca36a536ea68afb2431cdc6c` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` | 442664 | `5542b6865727d36db432c63c1a34795cfb80472b29258305fb786fda38297a12` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll.config` | 33939 | `73b9a880c39b4f2713fa5118e3222488dd04e124627d316632baa7ef27691312` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.runtimeconfig.json` | 282 | `dfbed91ad9b788c4258f80e515ee663025847200bc04817dab0a9632da4de809` |
| `BuildHost-netcore/Newtonsoft.Json.dll` | 722808 | `265b086e198e2c8473aaf0d2bf0ab6123022431a6e89e181e9d75bdcd1735be0` |
| `BuildHost-netcore/pl/System.CommandLine.resources.dll` | 19000 | `29d28dc053aeb5d2c320f15331be4c7efa4cb1643e5f8ead92a2e145ebd92e2b` |
| `BuildHost-netcore/pt-BR/System.CommandLine.resources.dll` | 18976 | `7ffb47c41090d7b9e4da2f2c74557c186604822e904bf029683b2b565746af96` |
| `BuildHost-netcore/ru/System.CommandLine.resources.dll` | 19512 | `cda0d35f61f5d60b29f6b2dd8ba8ab8f65b0ef8976df04f0657f90c1bd572592` |
| `BuildHost-netcore/System.Collections.Immutable.dll` | 259360 | `4743e42b28f1cfbc9c37b5121bc6e55e630d36afb4c2d0ad3c310daf2c56ad37` |
| `BuildHost-netcore/System.CommandLine.dll` | 148504 | `ef4bc45850128e2351bdae75ae736a62a2ba0e6a3dca75b5a8bf2b0bc7deac3d` |
| `BuildHost-netcore/tr/System.CommandLine.resources.dll` | 18992 | `06f12a7373d17d8e4c70dad2d3c1b8280a69242d39be2e86c691e0819306a7d8` |
| `BuildHost-netcore/zh-Hans/System.CommandLine.resources.dll` | 18976 | `a4c168c6a73d9755ddc72c541ba6bcccc1aa2a8040b84e3eae1900e23d9752e1` |
| `BuildHost-netcore/zh-Hant/System.CommandLine.resources.dll` | 19016 | `dacb3b425faff62c7bc1854e27ab8133a28aa4a8a98185bbf06c0b24fd82046a` |
| `certs/prod-ca-2021.crt` | 1367 | `700723581420dd1ac98fd7e9ac529f0ef210eadcaf87fc868a3ad7d114c2f3b7` |
| `libSkiaSharp.so` | 12289656 | `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef` |
| `Microsoft.AspNetCore.Authentication.JwtBearer.dll` | 52536 | `05494def3c48b22a43367dfc1bcb1fe03ac560cd140763023844a84012adb215` |
| `Microsoft.AspNetCore.OpenApi.dll` | 194872 | `889e7c608091f27bdcc641efc92c22f85dfc5334ef50ddbaa5badb0e3a527c0f` |
| `Microsoft.EntityFrameworkCore.Abstractions.dll` | 34848 | `ce76bce9dcc06fc6e22d6c4abebb4ea816a0079b10485c5c080d6ea9f0fd818e` |
| `Microsoft.EntityFrameworkCore.dll` | 2821152 | `785aaebff28acdafc80f4fd2a7521392bb67afd8069b89b3aa8d7c5627f49c60` |
| `Microsoft.EntityFrameworkCore.Relational.dll` | 2190368 | `68803fa0a71d4a9d58325ddde1d1709451094a20b01cbdb3b73d69f42d115197` |
| `Microsoft.IdentityModel.Abstractions.dll` | 19488 | `c3b97d1b1c42faa95bb92eabb7511ca61e89075388f122197040fcfdfb492013` |
| `Microsoft.IdentityModel.JsonWebTokens.dll` | 143392 | `e85d3d751b4254771f59ff8a14444ef701ba65efb84e312281d675c44536da71` |
| `Microsoft.IdentityModel.Logging.dll` | 36784 | `32ebeeb2163db6aa0a81659ff0b590894d78350c50c459196b6cc33c98ba21f3` |
| `Microsoft.IdentityModel.Protocols.dll` | 40880 | `7c8702a7f6c019d962faf4b8af2da751dc32af9b6be998688e774394fb5f8730` |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect.dll` | 137144 | `d1f7dbbb71cbf69f5242a74e43b632610ff5dca913f164d1dcf53b2f6e0046ac` |
| `Microsoft.IdentityModel.Tokens.dll` | 329248 | `b2dacc5120971efceb60f6febfe9685fe5a509ddb803c45956a63fc36e67631b` |
| `Microsoft.OpenApi.dll` | 486752 | `64497b0a954493014d15494bb89f09dbed059b13efe64cdf008d5f0f6bf197e5` |
| `Npgsql.dll` | 1477632 | `9d8db086e9eda5ff20c4a7f83fa3eae7689198f57277ff9ceea1ee3526f5fcf8` |
| `Npgsql.EntityFrameworkCore.PostgreSQL.dll` | 676864 | `0b8ca4484e12f1cd119fc6b4711015ba6ce2364f9890504f6f4f13036c2075bf` |
| `SeenIt.Api.deps.json` | 12179 | `03b7f83a87d0a5048af7e8b5a95df5749d9a757aa2895ae04b5038564d7b2da5` |
| `SeenIt.Api.dll` | 284160 | `63b81c9f6da07ec8ce27ada5fa78b3e8e948b9075911efdff822cc4d00506c88` |
| `SeenIt.Api.runtimeconfig.json` | 540 | `eff38a9750f7cca2407b1d639a963d56ffea92088e6da971bbbfa74e3dcd7224` |
| `SeenIt.Api.staticwebassets.endpoints.json` | 53 | `c1686417e8d5c31bba969f12b6f3d2b35e6609adb5ca42f212261a3a02771aac` |
| `SkiaSharp.dll` | 560440 | `d1a77d5f16c95e42e58d05edea34019fe7cfcea3d10aa22e0a4d75b4a4a45dd8` |
| `System.IdentityModel.Tokens.Jwt.dll` | 90032 | `83218e29958e265b4eebce62cda6d69d41e7bf1898b7fa21ed52102675f6dfe3` |

## 10. Uso em runtime

A revisão do backend procurou referências diretas a Roslyn, compilação e scripts C#, MSBuildWorkspace, reflection e carga explícita de assemblies, execução de processos/CLI, serviços EF de design-time e migração no startup. O bootstrap registra EF Core via UseNpgsql e OpenAPI de desenvolvimento. Os caminhos examinados de entrada HTTP, persistência, JWT, avatares e catálogo não mostraram invocação de Roslyn ou CLI. O EF usa árvores de expressão e compilação de consultas do .NET, independente da presença de Roslyn. Geradores ou analisadores de código operam no build.

A observação dinâmica usou container temporário sem rede externa, sem portas publicadas, raiz somente leitura e configuração sintética por argumentos. Duas tentativas falharam na validação de Storage e uma iniciou com aviso de filesystem read-only para Data Protection. Na execução final, com tmpfs para suas chaves, `/health` retornou HTTP 200 via loopback na porta 10000. O mapa observado após a requisição não continha os hosts. A amostra é limitada a startup/health e a mapeamentos visíveis no processo, sem cobertura de todas as rotas, cargas transitórias ou formas de carga em memória.

## 11. Advisories e CVEs

Consulta atual dos arquivos do [feed oficial de vulnerabilidades do NuGet](https://api.nuget.org/v3/vulnerabilities/index.json), por IDs exatos dos componentes principais do restore e dos hosts. As faixas devolvidas para os componentes abaixo não corresponderam às versões comprovadas. A triagem usa também os avisos primários do GitHub e a revisão de presença física.

| Advisory | Faixa e correção pertinentes | Aplicabilidade |
|---|---|---|
| CVE-2025-26646 / GHSA-h4j7-5rxr-p4wc, alta no catálogo e baixa na ficha do repositório | Microsoft.Build.Tasks.Core 17.0.0–17.8.3 entre as faixas afetadas, corrigida em 17.8.29 nessa linha e 17.14.8 na linha 17.14 | Não aplicável. Grafo resolve Tasks.Core 17.14.28, sem DLL na imagem. O pré-requisito é uso de DownloadFile com entrada externa no contexto MSBuild. [Fornecedor](https://github.com/dotnet/msbuild/security/advisories/GHSA-h4j7-5rxr-p4wc) |
| CVE-2025-55247 / GHSA-w3q9-fxm7-j8fq, alta | Microsoft.Build, Tasks.Core e Utilities.Core em faixas de 17.8 a 17.14, com correção 17.14.28 nessa linha | Não aplicável às versões resolvidas segundo as faixas publicadas. Microsoft.Build 17.7.2 está fora das faixas listadas e Tasks/Core Utilities em 17.14.28. O cenário depende de processamento de entrada não confiável pelo MSBuild. [Advisory Microsoft](https://github.com/advisories/GHSA-w3q9-fxm7-j8fq) |
| CVE-2026-84008 / GHSA-846f-f764-76mx, alta, e CVE-2026-70162 / GHSA-2fmv-rv2c-59x7, moderada | Pkcs 9.0.0–9.0.20 e 10.0.0–10.0.12, corrigidas em 9.0.21 e 10.0.13 | Não aplicáveis às versões observadas. Pin 9.0.22 no restore, Pkcs do shared framework 10.0.15 na imagem. Dependem do processamento de entrada CMS maliciosa. O segundo aviso é específico de Windows. [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) · [CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) |
| CVE-2024-21907 / GHSA-5crp-9r3c-p9vr, alta | Newtonsoft.Json anterior a 13.0.1, corrigida em 13.0.1 | Não aplicável. Ambas as cópias são 13.0.3. Depende de processamento de JSON aninhado via parser afetado. [Advisory revisado](https://github.com/advisories/GHSA-5crp-9r3c-p9vr) |
| CVE-2024-38081 / GHSA-hq7w-xv5x-g34j, alta | Microsoft.IO.Redist 4.6.0-preview.18571.3 a 6.0.0, corrigida em 6.0.1 | Não aplicável ao cenário Linux observado. O binário net472 informa ProductVersion 6.0.32 e o cenário descrito pela Microsoft envolve manipulação local do instalador Visual Studio no Windows. [Advisory Microsoft](https://github.com/advisories/GHSA-hq7w-xv5x-g34j) |

Não foi encontrada correspondência no feed para EF Design, os pacotes Roslyn consultados, Locator ou System.CommandLine nas versões inventariadas. A cobertura de binários empacotados dentro de contentFiles e dos auxiliares net472 sem versão NuGet comprovada é **indeterminada**. Scout histórico serve como contexto. Este diagnóstico não reexecutou Scout nem restore/auditoria do CLI.

## 12. Aplicabilidade ao SeenIt

**Confirmado:** presença física dos hosts e bibliotecas auxiliares no artefato final local. **Potencial:** a cópia pode ser retirada da distribuição exclusiva da API, hipótese sustentada pela ausência do grafo de execução, pela revisão de código e pela observação amostral. **Não aplicável:** os advisories específicos acima, nas condições e versões examinadas. **Indeterminado:** carregamento em todos os fluxos possíveis e equivalência binária com a imagem efetivamente implantada.

O host netcore tem runtimeconfig compatível em princípio via roll-forward e `dotnet` está disponível. A capacidade de localizar um toolset utilizável nesse container não foi demonstrada. O host net472 não dispõe de Mono ou .NET Framework identificado. A existência dos executáveis auxiliares por si só não demonstra compilação dinâmica acessível por HTTP.

## 13. Superfície de ataque

Os 46 arquivos auxiliares acrescentam 4.225.767 bytes lógicos ao publish e são candidatos a exclusão do artefato de produção. A redução real da camada ou do download comprimido precisa ser medida. Retirar esses binários reduziria código distribuído e inventário de terceiros, inclusive utilitários de parsing e CLI do host. Nenhuma rota de entrada do usuário até os hosts foi identificada e nenhuma mitigação de CVE aplicável foi demonstrada. O excesso identificado pertence à seleção de conteúdo de publish, antes do COPY. O SDK no build e EF Design nas estações de desenvolvimento têm usos próprios e sua auditoria permanece independente. A mudança pode afetar tooling que use um publish da API como entrada, particularmente operações que avaliem projetos.

## 14. Alternativas

| Alternativa | Benefício, escopo e risco | Evidência, tooling e rollback |
|---|---|---|
| A — Manter | Preserva compatibilidade e mantém bytes auxiliares | Referência de controle. Reavaliar em caso de advisory ou requisito de distribuição. |
| B — Metadados NuGet | Pode mudar o fluxo de contentFiles no restore e output de desenvolvimento. PrivateAssets all já existe | Exigir inventários Debug/Release e validação do tooling EF e dependentes. Restaurar metadados originais em caso de regressão. |
| C — Seleção no publish | Restringe a mudança aos dois caminhos de conteúdo publicados. Preserva o grafo e permite medir redução | Confirmar igualdade dos 24 arquivos finais restantes e funcionamento da API/verify. Testar tooling em output de desenvolvimento; verificar consumidores do publish. Reverter a regra de publish. |
| D — Docker COPY | Poderia filtrar a imagem, mas o publish continuaria carregando os extras. Uma lista manual amplia manutenção; apagar após COPY pode reter bytes em camadas | Exigir equivalência e prova de redução no manifesto/camadas. Tooling local preservado. Rollback pelo COPY original. |
| E — Remover EF Design | Retiraria dependências do restore, com risco direto para migrations e scaffolding | Exigir evidência de que os fluxos de tooling já dispõem de uma alternativa compatível ou foram desativados. Repor referência original se falhar. |
| F — Atualizar pacote | Adequada se houver correção aplicável e compatível. Pode mudar vários componentes do grafo e tooling | Exigir faixa afetada comprovada, audit/Scout, compatibilidade de migrations e a bateria de runtime. Rollback das versões. Nenhum alvo justificável neste diagnóstico. |

## 15. Plano para B06-D2

1. Construir controle original e candidata em contexto isolado, Linux/amd64, mesmos digests e tags locais exclusivas. A candidata deve aplicar uma única alteração, a exclusão dos itens `BuildHost-net472/**` e `BuildHost-netcore/**` na saída de publish da API. Confirmar pelo log do MSBuild que a regra atinge só a seleção de publicação.
2. Executar restore, build, publish Release, estágio verify, AvatarChecks, CoreChecks e RegisterIntegrationChecks. Registrar `dotnet package list --vulnerable --include-transitive` e Docker Scout em ambos. Não usar produção nem serviços reais de PostgreSQL/Supabase.
3. Verificar non-root, porta 10000, health, JWT válido/inválido, HTTPS com proxy local e ausência de regressões da configuração. Comparar mapa ou eventos de carga nos fluxos exercitados. Validar EF Core com PostgreSQL descartável ou fixture local; cobrir registro, consultas e persistência sem apontar para bancos existentes.
4. Comparar os hashes de todos os caminhos retidos, manifests, número de arquivos, tamanho descomprimido e camada comprimida. Os únicos caminhos ausentes esperados são os 46 identificados. Conferir appsettings, CA, SkiaSharp, deps, config e marcador.
5. Verificar tooling EF 10.0.0 em output de desenvolvimento, com operações de inspeção e geração local de SQL para migrations, sem aplicar migrations. Se houver fluxo de design-time que use a saída publicada, caracterizá-lo antes de aprovar a alteração. Aprovar somente após equivalência funcional e evidência da redução. Reverter a regra candidata se houver falha ou perda de compatibilidade.

## 16. Riscos e limitações

Imagem histórica com contexto equivalente, sem rebuild do HEAD nem inspeção remota. A observação de carga é amostral. O feed NuGet é pontual e pode omitir dependências incorporadas. Tamanho lógico não equivale a tamanho comprimido. A revisão atual não é nova validação de integração, JWT, HTTPS ou banco. O B06-D2 é necessário para declarar a exclusão compatível.

## 17. Recomendação técnica

Executar B06-D2 com exclusão exclusiva em publish. O fundamento é a presença de dois hosts de design-time, 46 arquivos e 4,03 MiB lógicos, combinada com a ausência de vínculo com o grafo principal e de carga observada. Tratar o ganho como redução de distribuição e superfície potencial. Manter EF Design, o manifesto dotnet-ef, o SDK e o pin Pkcs na experiência inicial.

## 18. Preservação do repositório

Conferência de SHA-256 dos 244 arquivos rastreados da principal e dos 161 arquivos da manutenção, incluindo as nove entradas não ignoradas, com comparação ao snapshot histórico da manutenção. HEADs, status preexistente da manutenção, dez branches e duas worktrees preservados. Csproj, Dockerfile, Program.cs, SkiaSharp 4.155.2, JWT, autenticação, frontend e testes permanentes preservados. O único arquivo novo é este relatório não rastreado.

Inventário do Docker antes da inspeção e conferência posterior: 22 imagens, dois containers preexistentes parados, quatro volumes e cache de build com os mesmos totais exibidos. As sessões diagnósticas temporárias foram removidas por `--rm`. Imagens, tags, containers, volumes e caches históricos preservados. Nenhuma publicação remota, modificação funcional, alteração de configuração externa, migração, commit, push, PR, merge ou deploy.

**B06-D1 DIAGNÓSTICO CONCLUÍDO**
