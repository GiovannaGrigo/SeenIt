# B06-D2 — Experimento isolado de exclusão dos BuildHosts

## 1. Objetivo

Medir o efeito de excluir exclusivamente os dois diretórios BuildHost da publicação Release da API, mantendo o grafo NuGet, os outputs de desenvolvimento e o tooling de migrations. Experimento realizado em 24 de novembro de 2026. **A candidata passou localmente.** A remoção de 46 arquivos reduziu o conjunto das camadas OCI comprimidas em **1.640.452 bytes (1,44%)** e preservou os hashes de todos os arquivos restantes da imagem.

## 2. Estado inicial do Git

`main`, `origin/main` local e a referência remota consultada apontavam para `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c`. O único caminho não rastreado da principal era `docs/B06-D1-BUILDHOST-ROSLYN-DIAGNOSIS.md`. Foram registrados dez branches locais, duas worktrees, 245 hashes SHA-256 na principal e 161 na manutenção. `maintenance-phase1` estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seis arquivos rastreados modificados e nove entradas não rastreadas esperadas.

## 3. Referências B06-D1

O diagnóstico rastreou `Microsoft.EntityFrameworkCore.Design 10.0.0 → Microsoft.CodeAnalysis.Workspaces.MSBuild 4.14.0 → contentFiles` com `copyToOutput=true`. O inventário histórico continha 25 arquivos net472 e 21 netcore, 4.225.767 bytes. Os hosts de versão informacional 4.14.0-3.25262.10 e seus auxiliares eram conteúdo publicado, fora do `SeenIt.Api.deps.json`. O SDK, os compiladores e a DLL de EF Design estavam ausentes da imagem final. O diagnóstico não confirmou CVE aplicável às versões verificadas nem uma chamada aos hosts nos fluxos examinados. A observação de startup/health foi amostral. Exclusão por etapa de publish foi a alternativa proposta para preservar o tooling. [B06-D1](B06-D1-BUILDHOST-ROSLYN-DIAGNOSIS.md)

B06-B2/B3 fundamentaram os digests e a validação Docker. B06-C3 e B06-C-FINAL sustentaram a referência SkiaSharp 4.155.2 e ofereceram harnesses reutilizáveis. Os números nas seções 8–20 abaixo são de execuções novas. [B06-B2](B06-B2-DOCKER-RUNTIME-EXPERIMENT.md) · [B06-B3](B06-B3-DOCKER-RUNTIME-IMPLEMENTATION.md) · [B06-C3](B06-C3-SKIASHARP-UPDATE-IMPLEMENTATION.md) · [B06-C final](B06-C-FINAL-SUMMARY.md)

## 4. Metodologia

Construção A/B Linux/amd64 sem cache para as duas imagens finais. Build, restore e publish ocorreram no Dockerfile original. Após o build, foi exportado o estágio `build` de cada cenário reaproveitando os passos do cache criado pela respectiva construção. A comparação calculou SHA-256 em `/out/api` e `/app`, conferiu as listas de arquivos e diretórios e leu o manifesto OCI de plataforma do export local. As suítes Docker e suplementares rodaram nos dois cenários.

## 5. Isolamento

`git archive HEAD` selecionou os quatro projetos do backend, `.dockerignore`, configuração sintética de runtime e CA pública. As duas cópias de 84 arquivos foram extraídas em `%TEMP%/seenit-b06-d2-b3ae7d6-c529ef`. A comparação antes de considerar outputs bin/obj identificou somente o csproj experimental. O contexto efetivo respeitou as exclusões originais para arquivos locais de configuração, manifestos de ferramenta e artefatos. A sessão final de tooling Linux montou apenas a ferramenta restaurada e não teve rede. Os ensaios de runtime usaram namespaces sem acesso externo, sem mapeamento de portas, e PostgreSQL descartável em loopback quando necessário.

## 6. Diff experimental

Único diff, no `backend/SeenIt.Api/SeenIt.Api.csproj` da cópia candidata.

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

## 7. Mecanismo de exclusão

Após `ComputeFilesToPublish`, a condição usa o caminho relativo de destino de `ResolvedFileToPublish`. O padrão aceita barra e contrabarra, exige o nome completo do diretório na raiz e não seleciona DLLs por nome. A seleção foi confirmada pela comparação do publish resultante. O restore continuou resolvendo 51 entradas na API e 31 no projeto de integração no comparativo Windows, com os mesmos IDs/versões. Os outputs Debug Linux e Windows continuaram contendo os 46 arquivos. A regra mantém as referências, `PrivateAssets`, `IncludeAssets` e o Dockerfile originais.

## 8. Inventário controle

Imagem local `seenit-api:seenit-b06-d2-b3ae7d6-c529ef-control`, índice `sha256:f5bcaffa65a5240875aa0aa6c1c895809a6af1b4af70b707c90d780fc091bb8c`, manifesto Linux/amd64 `sha256:8f8c1908cd2b7758323102b06523718b10e2d9e8e1bb6db2341e24ed595d1af6`. Em `/out/api`, 68 arquivos. Em `/app`, os 68 por hash, mais a CA e o marcador. Os 46 arquivos abaixo estavam em ambos, com os mesmos hashes registrados no B06-D1. Caminhos relativos à raiz de publish ou `/app`.

| Caminho removido na candidata | Bytes | SHA-256 do controle |
|---|---:|---|
| `BuildHost-net472/Microsoft.Build.Locator.dll` | 34848 | `1d4222a33857c46f3f0e3c8e59e2adc06f6c98d9072802c7cfd8e0dfda74847b` |
| `BuildHost-net472/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe` | 446344 | `bea27af3980b0fdfbf302423d55553498a0241f43524356025d477902ba982f5` |
| `BuildHost-net472/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe.config` | 2901 | `43169e9dbfdd4d06d2d4f0f89c9794d43453b096bb9ae6a275c5e3739383d925` |
| `BuildHost-net472/Microsoft.IO.Redist.dll` | 131232 | `6d0b0a4ab6fca41c97b0993f053bc8ea5ee965a8ff5a2ed42d15131633752d5f` |
| `BuildHost-net472/Newtonsoft.Json.dll` | 722304 | `bcb1dd23d554d3a4e17b2c0f08761fc571857de37291b9bdd8c91788be3aa842` |
| `BuildHost-net472/System.Buffers.dll` | 20856 | `accccfbe45d9f08ffeed9916e37b33e98c65be012cfff6e7fa7b67210ce1fefb` |
| `BuildHost-net472/System.Collections.Immutable.dll` | 259360 | `77d705fe51f224d77b25fc06ede39e2921f08b46a961a027d6ee49a4bb19c127` |
| `BuildHost-net472/System.CommandLine.dll` | 148504 | `ef4bc45850128e2351bdae75ae736a62a2ba0e6a3dca75b5a8bf2b0bc7deac3d` |
| `BuildHost-net472/System.Memory.dll` | 142240 | `bf3fb84664f4097f1a8a9bc71a51dcf8cf1a905d4080a4d290da1730866e856f` |
| `BuildHost-net472/System.Numerics.Vectors.dll` | 115856 | `1d3ef8698281e7cf7371d1554afef5872b39f96c26da772210a33da041ba1183` |
| `BuildHost-net472/System.Runtime.CompilerServices.Unsafe.dll` | 18024 | `37768488e8ef45729bc7d9a2677633c6450042975bb96516e186da6cb9cd0dcf` |
| `BuildHost-net472/System.Threading.Tasks.Extensions.dll` | 25984 | `4f81ffd0dc7204db75afc35ea4291769b07c440592f28894260eea76626a23c6` |
| `BuildHost-net472/cs/System.CommandLine.resources.dll` | 18992 | `0613e63dea0dfc46b8a090ee41ca0ac93902fdf876107fe116697f57ff997ecb` |
| `BuildHost-net472/de/System.CommandLine.resources.dll` | 18984 | `6f77e87cdd5729da8ef9f4f7ded1424667ad81711e0ff80c264707a522daab6a` |
| `BuildHost-net472/es/System.CommandLine.resources.dll` | 18992 | `99d6cfb2a639bec41c755dfa1942ca42b23e3272411392468d5d21ce8582daa8` |
| `BuildHost-net472/fr/System.CommandLine.resources.dll` | 18976 | `3f5b7616477a792b1f58b820527bab0b85b621398bf027840368d5325c44e158` |
| `BuildHost-net472/it/System.CommandLine.resources.dll` | 18992 | `70b1cd23221e5f467bb39d5ede5b3b6668766a0a659428254b208d5930528c67` |
| `BuildHost-net472/ja/System.CommandLine.resources.dll` | 19016 | `a60f1b1b73bf8227dafcb1fd187580c9dcb05d26ccfc0d0fcde1a684e5c7b5fb` |
| `BuildHost-net472/ko/System.CommandLine.resources.dll` | 19016 | `14c738c5c21287e3232032565be6bb45ccdfa81bcafe14cbd2ec27fca60b2bf2` |
| `BuildHost-net472/pl/System.CommandLine.resources.dll` | 19000 | `29d28dc053aeb5d2c320f15331be4c7efa4cb1643e5f8ead92a2e145ebd92e2b` |
| `BuildHost-net472/pt-BR/System.CommandLine.resources.dll` | 18976 | `7ffb47c41090d7b9e4da2f2c74557c186604822e904bf029683b2b565746af96` |
| `BuildHost-net472/ru/System.CommandLine.resources.dll` | 19512 | `cda0d35f61f5d60b29f6b2dd8ba8ab8f65b0ef8976df04f0657f90c1bd572592` |
| `BuildHost-net472/tr/System.CommandLine.resources.dll` | 18992 | `06f12a7373d17d8e4c70dad2d3c1b8280a69242d39be2e86c691e0819306a7d8` |
| `BuildHost-net472/zh-Hans/System.CommandLine.resources.dll` | 18976 | `a4c168c6a73d9755ddc72c541ba6bcccc1aa2a8040b84e3eae1900e23d9752e1` |
| `BuildHost-net472/zh-Hant/System.CommandLine.resources.dll` | 19016 | `dacb3b425faff62c7bc1854e27ab8133a28aa4a8a98185bbf06c0b24fd82046a` |
| `BuildHost-netcore/Microsoft.Build.Locator.dll` | 43568 | `0adbe65d87bafc46e9ae7fbd4dc40ada05315c5dde6709eef578233fb3f4f62a` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.deps.json` | 11309 | `a63d1edce0775c67277e25d5c65864adefa18d5eca36a536ea68afb2431cdc6c` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` | 442664 | `5542b6865727d36db432c63c1a34795cfb80472b29258305fb786fda38297a12` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll.config` | 33939 | `73b9a880c39b4f2713fa5118e3222488dd04e124627d316632baa7ef27691312` |
| `BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.runtimeconfig.json` | 282 | `dfbed91ad9b788c4258f80e515ee663025847200bc04817dab0a9632da4de809` |
| `BuildHost-netcore/Newtonsoft.Json.dll` | 722808 | `265b086e198e2c8473aaf0d2bf0ab6123022431a6e89e181e9d75bdcd1735be0` |
| `BuildHost-netcore/System.Collections.Immutable.dll` | 259360 | `4743e42b28f1cfbc9c37b5121bc6e55e630d36afb4c2d0ad3c310daf2c56ad37` |
| `BuildHost-netcore/System.CommandLine.dll` | 148504 | `ef4bc45850128e2351bdae75ae736a62a2ba0e6a3dca75b5a8bf2b0bc7deac3d` |
| `BuildHost-netcore/cs/System.CommandLine.resources.dll` | 18992 | `0613e63dea0dfc46b8a090ee41ca0ac93902fdf876107fe116697f57ff997ecb` |
| `BuildHost-netcore/de/System.CommandLine.resources.dll` | 18984 | `6f77e87cdd5729da8ef9f4f7ded1424667ad81711e0ff80c264707a522daab6a` |
| `BuildHost-netcore/es/System.CommandLine.resources.dll` | 18992 | `99d6cfb2a639bec41c755dfa1942ca42b23e3272411392468d5d21ce8582daa8` |
| `BuildHost-netcore/fr/System.CommandLine.resources.dll` | 18976 | `3f5b7616477a792b1f58b820527bab0b85b621398bf027840368d5325c44e158` |
| `BuildHost-netcore/it/System.CommandLine.resources.dll` | 18992 | `70b1cd23221e5f467bb39d5ede5b3b6668766a0a659428254b208d5930528c67` |
| `BuildHost-netcore/ja/System.CommandLine.resources.dll` | 19016 | `a60f1b1b73bf8227dafcb1fd187580c9dcb05d26ccfc0d0fcde1a684e5c7b5fb` |
| `BuildHost-netcore/ko/System.CommandLine.resources.dll` | 19016 | `14c738c5c21287e3232032565be6bb45ccdfa81bcafe14cbd2ec27fca60b2bf2` |
| `BuildHost-netcore/pl/System.CommandLine.resources.dll` | 19000 | `29d28dc053aeb5d2c320f15331be4c7efa4cb1643e5f8ead92a2e145ebd92e2b` |
| `BuildHost-netcore/pt-BR/System.CommandLine.resources.dll` | 18976 | `7ffb47c41090d7b9e4da2f2c74557c186604822e904bf029683b2b565746af96` |
| `BuildHost-netcore/ru/System.CommandLine.resources.dll` | 19512 | `cda0d35f61f5d60b29f6b2dd8ba8ab8f65b0ef8976df04f0657f90c1bd572592` |
| `BuildHost-netcore/tr/System.CommandLine.resources.dll` | 18992 | `06f12a7373d17d8e4c70dad2d3c1b8280a69242d39be2e86c691e0819306a7d8` |
| `BuildHost-netcore/zh-Hans/System.CommandLine.resources.dll` | 18976 | `a4c168c6a73d9755ddc72c541ba6bcccc1aa2a8040b84e3eae1900e23d9752e1` |
| `BuildHost-netcore/zh-Hant/System.CommandLine.resources.dll` | 19016 | `dacb3b425faff62c7bc1854e27ab8133a28aa4a8a98185bbf06c0b24fd82046a` |

## 9. Inventário candidata

Imagem local `seenit-api:seenit-b06-d2-b3ae7d6-c529ef-candidate`, índice `sha256:d9ad881eae074ba05897cabb9dd6ce74132289405d8bd492710fce1ce4c7c4aa`, manifesto Linux/amd64 `sha256:3dae4fe32c515f695e9f3b4f1cffa5b149da175a276b65b0a926b6236f49f554`. Em `/out/api`, 22 arquivos. Em `/app`, os 22 mais a CA e o marcador. Não há diretórios BuildHost residuais em nenhum dos dois.

| Caminho preservado | Bytes | SHA-256 idêntico no controle e na candidata |
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

## 10. Comparação de hashes

22/22 arquivos retidos no publish e 24/24 na imagem final coincidiram byte a byte. Nenhum caminho foi adicionado ou removido fora dos dois diretórios. `SeenIt.Api.dll`, sua configuração e seus manifests, `SkiaSharp.dll`, `libSkiaSharp.so`, a CA e o marcador permanecem idênticos. O hash nativo corresponde a `0366b360fa4b47f9889d20fec6c1707bfe418ff474ea71011b5738a6b0052fef`. `SeenIt.Api.deps.json` manteve SHA-256 `03b7f83a87d0a5048af7e8b5a95df5749d9a757aa2895ae04b5038564d7b2da5`. Apenas o deps auxiliar do host netcore desapareceu junto com seu diretório. O deps principal preservou SkiaSharp e NativeAssets Linux 4.155.2, EF Core/Npgsql e os demais pacotes de execução.

## 11. Redução de arquivos

- Publish da API, 68 → 22 arquivos, redução de 46 (67,65%).
- `/app`, 70 → 24 arquivos, redução de 46 (65,71%).
- Removidos 25 arquivos em BuildHost-net472 e 21 em BuildHost-netcore. Outputs de desenvolvimento mantiveram 46 em ambos.

## 12. Redução de tamanho lógico

| Medição em bytes | Controle | Candidata | Redução | Percentual |
|---|---:|---:|---:|---:|
Publish da API | 26.104.977 | 21.879.210 | 4.225.767 | 16,19% |
Arquivos regulares em `/app` | 26.106.367 | 21.880.600 | 4.225.767 | 16,19% |
Arquivos regulares no filesystem final visível, mesma travessia `find / -xdev` | 259.431.098 | 255.205.331 | 4.225.767 | 1,63% |

A diferença lógica em todas as medições é 4.225.767 bytes, cerca de 4,03 MiB. A travessia do filesystem soma tamanhos de arquivos regulares. Metadados, alocação física e camadas sobrepostas não entram nessa soma.

## 13. Redução de imagem e compressão

| Medição em bytes | Controle | Candidata | Redução | Percentual |
|---|---:|---:|---:|---:|
Camadas OCI, tar descomprimido | 276.101.632 | 271.820.288 | 4.281.344 | 1,55% |
Camadas OCI, blobs gzip | 113.945.418 | 112.304.966 | 1.640.452 | 1,44% |
Camada do COPY da API, tar descomprimido | 26.179.072 | 21.897.728 | 4.281.344 | 16,35% |
Camada do COPY da API, gzip | 9.976.553 | 8.336.104 | 1.640.449 | 16,44% |
Campo `.Size` informado pelo Engine | 400.760.331 | 394.692.103 | 6.068.228 | 1,51% |

O manifesto OCI de cada plataforma tem 12 camadas. As bases e a camada vazia final usam os mesmos blobs. O gzip da camada do marcador difere em +1 byte e o da etapa final em −4 bytes, ambos com mesmo tamanho tar. A medição gzip soma descritores dos manifests de plataforma e confere os tamanhos dos blobs no `docker save`. Os valores tar vêm do `ISIZE` dos blobs gzip, inferiores a 4 GiB por camada. O arquivo tar conjunto, com blobs comuns deduplicados e metadados, ocupou 122.337.280 bytes. Nenhuma transferência a um registry foi realizada. O efeito em downloads com cache de camadas e no armazenamento físico do daemon depende do ambiente.

## 14. Build e verify

Os dois builds finais sem cache concluíram, SDK Noble com digest `sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625`, SDK 10.0.404 e ASP.NET Noble `sha256:3a31a3c8c41f487ecab474cf6dcf152489fedca9fd145b0b49a4b9e616cd6dde`, runtime 10.0.15. Restore, publish Release linux-x64 framework-dependent, verificação do nativo com `ldd` e comparações de SkiaSharp no verify passaram. O marcador foi copiado para ambas as finais. CA de 1.390 bytes validada com modo 0444. O export posterior do estágio de build foi uma coleta com cache, sem contabilização como uma segunda validação.

## 15. Testes da API

Resultados novos em cada cenário, com as mesmas contagens no controle e na candidata.

| Suíte | Resultado por cenário |
|---|---|
| AvatarChecks, verify | 16 grupos aprovados. JPEG, PNG, WebP, resize, limites, reencode, upload e substituição simulados, falhas de Storage, compensação, concorrência e cancelamento. |
| CoreChecks, verify | Tempo assistido, classificação de anime e 72 casos HTTP de validação de cadastro. |
| RegisterIntegrationChecks, runtime | 73 casos HTTP de cadastro, 16 usuários em SQLite em memória. Suítes adicionais de login, perfil, catálogo, biblioteca, integridade, JWT, rate limiting, headers falsificados, CORS, concorrência e cancelamento passaram. O log de regressão registra 20 logins sobrepostos. |
| EntryPointChecks, runtime | 174/174 asserções e 72 requisições em seis perfis. Opção `ReverseProxy:HttpsRedirectionHandledByEdge` ausente, false e true, com e sem porta HTTPS sintética. Inclui JWT e headers falsificados. |
| ExtraAvatar, runtime | Três grupos aprovados, WebP animado, cancelamento/limite concorrente e limite de 16 MP. |
| CodecExperiment, runtime | 37 registros por cenário, sucesso. Registros incluem casos JPEG/PNG/WebP, limites, reencode e medições. |

Os quatro harnesses suplementares são executáveis do B06-C3 reutilizados em nova execução. As DLLs da aplicação e o nativo no diretório de teste foram sobrepostos pelos arquivos de cada imagem final e a identidade de `SeenIt.Api.dll` e `libSkiaSharp.so` foi verificada com `cmp` antes de executar. A primeira tentativa em ambos falhou ao mapear bibliotecas nativas de teste no tmpfs noexec. A repetição com tmpfs exec, sem mudança na aplicação, passou todas as suítes com código 0.

## 16. Testes de imagem

Ambas iniciaram com `USER app` (UID/GID 1654), ambiente Production, porta 10000 e CA legível 0444. Consultas diretas por loopback a `/health` retornaram HTTP 200. `dotnet --list-sdks` vazio. Encerramento com código 0 e `OOMKilled=false` nos dois containers. CPU 2, memória 512 MiB no smoke. Os logs registraram avisos esperados sobre Data Protection sem criptografador em tmpfs e precedência de URL/porta.

## 17. EF Core runtime

`RegisterIntegrationChecks --postgres-only` passou separadamente nos dois cenários com PostgreSQL **17.11** descartável, credenciais sintéticas, sem portas publicadas e em namespace que expunha somente loopback. A suíte criou o esquema de teste via `EnsureCreated`, inicializou o DbContext e os mapeamentos e exercitou consultas, inserts, updates, transações e rollback após SQL. Validou concorrência com dois providers/conexões, conflito 409, erro real 23505, retry explícito, isolamento de usuários e o limite Unicode varchar. O aviso sobre `libgssapi_krb5.so.2` apareceu em ambos. Autenticação PostgreSQL por senha e os fluxos testados completaram com código 0. Autenticação Kerberos/GSS não foi exercitada. Somente os containers e dados descartáveis do ensaio foram encerrados e descartados.

## 18. Tooling de migrations

O HEAD não contém arquivo de solução. Foram restaurados individualmente API, AvatarChecks, CoreChecks e RegisterIntegrationChecks nos dois contextos. O build Debug da API passou em Windows e, novamente, no SDK Linux/amd64 sem rede. Em ambos os ambientes, 46 arquivos BuildHost permaneceram em Debug por cenário. No Linux foram executados `dotnet-ef 10.0.0`, `dbcontext info`, `migrations list --no-connect`, `migrations script --idempotent` e `migrations has-pending-model-changes`. Os dois cenários descobriram `SeenIt.Api.Data.AppDbContext` com Npgsql, listaram as mesmas três migrations (`InitialPostgres`, `AddSeriesFavorites`, `AddSeriesAnimeClassification`) e passaram a checagem de modelo pendente. SQL Linux idêntico, SHA-256 `823349a79b009fb21b9503d841c5bfbb9b8355cabdb37876abb6f05e50df7a17`. O SQL Windows também foi idêntico entre seus dois cenários. Nenhum SQL de migrations foi aplicado ou executado no banco.

A busca no repositório identificou os manifestos da ferramenta e o fluxo de migrations no projeto descrito no README. Nenhum fluxo de tooling do projeto consumindo `/out/api` ou a imagem publicada foi identificado. A verificação de tooling cobre o fluxo de desenvolvimento; consumidores externos de um diretório publicado não foram inventariados.

## 19. Runtime e smoke

Amostra de `/proc/1/maps` e processos após startup/health identificou os assemblies da API, autenticação, OpenAPI, EF Core e Npgsql. Nenhum BuildHost ou Roslyn apareceu na amostra. Em uma segunda execução com `HttpsRedirectionHandledByEdge=true` e proxy confiável, foi realizado handshake real **TLS 1.3** em um proxy Node temporário, com certificado de teste confiado apenas pelo cliente. O proxy encaminhou para Kestrel via HTTP no loopback e substituiu os headers de encaminhamento. Nos dois cenários, health 200, perfil com JWT inválido 401 e preflight CORS de cadastro 204, sem redirecionamentos. A matriz EntryPointChecks cobriu os demais perfis e JWT válido/expirado com TestServer.

## 20. Docker Scout e NuGet audit

| Medição nova | Controle | Candidata |
|---|---:|---:|
| Pacotes indexados pelo Scout 1.28.0 | 176 | 160 |
| Registros em packages no SPDX | 236 | 192 |
| Critical / High / Medium / Low | 0 / 0 / 13 / 4 | 0 / 0 / 13 / 4 |
| Total de CVEs em nove pacotes afetados | 17 | 17 |
| CVEs exclusivas | 0 | 0 |
| BuildHost, Locator e respectivos auxiliares detectados no SPDX | Sim | Ausentes |

Comparação SARIF por identificador, severidade e mensagem sem diferenças. Os resultados mantêm a contagem histórica; a exclusão não mitigou nenhum alerta identificado. NuGet audit com transitivos na API, AvatarChecks, CoreChecks e RegisterIntegrationChecks não reportou pacotes vulneráveis em nenhum dos cenários e nas fontes consultadas. A auditoria de pacote e o SBOM têm cobertura incompleta para conteúdo incorporado.

## 21. Diferenças e limitações

A diferença funcional de distribuição verificada é a ausência dos dois diretórios. A redução de CVEs observada foi zero. O teste de carga de assemblies é amostral e não rastreia toda chamada transitória ou possível. Cobertura de avatares e catálogo utiliza dados sintéticos e doubles. O ensaio do provider usa PostgreSQL local real com senha, sem serviços externos. O build e os scanners requisitaram fontes públicas, mas o runtime e o tooling EF Linux observados ficaram isolados. O relatório descreve as imagens locais e seus manifestos, sem inferência sobre a imagem implantada.

## 22. Matriz de aprovação

| Critério | Resultado |
|---|---|
| Somente BuildHosts removidos, publish e final | Aprovado, 46 arquivos, nenhum diretório residual. |
| Integridade da API, deps, SkiaSharp, CA e marcador | Aprovado, 22/22 hashes em publish e 24/24 em final. |
| Build Linux/amd64, restore, Release, verify, AvatarChecks e CoreChecks | Aprovado nos dois builds. |
| Integração, concorrência, JWT, CORS e proxy TLS | Aprovado nos fluxos executados. |
| Health, non-root, porta, shutdown e ausência de OOM | Aprovado em ambos. |
| Persistência Npgsql e tooling de migrations | Aprovado em runtime PostgreSQL descartável e desenvolvimento Linux; sem modelo pendente. |
| Carregamento dos hosts | Nenhuma utilização observada na amostra e nenhuma dependência demonstrada nos testes. |
| Redução medida e auditoria documentada | Aprovado; 1.640.452 bytes gzip a menos e mesmas 17 CVEs no Scout. |

## 23. Alternativas de rollback

Para reverter em uma implementação futura, retirar apenas o target e refazer o publish com os mesmos digests, usando os 46 arquivos e hashes do controle como referência. A imagem controle local fica disponível para comparação imediata. Mudanças de metadados NuGet ou Docker COPY ampliam o escopo e não são necessárias para reproduzir o resultado medido.

## 24. Recomendação B06-D3

Considerar a aplicação controlada do mesmo target na próxima etapa autorizada. Repetir o comparativo em Linux/amd64 no HEAD de integração e conservar a validação offline do tooling e o smoke com proxy. O benefício demonstrado é uma redução de conteúdo distribuído e de tamanho comprimido, com os mesmos resultados de segurança observados pelo Scout.

## 25. Preservação do repositório

Branch `main` e `origin/main` em `b3ae7d6d1673a358285c24fcea38a3ed6b4a516c`. Os únicos caminhos não rastreados são o B06-D1 preexistente e este relatório. Os 245 hashes preexistentes da worktree principal e os 161 hashes da manutenção foram preservados. As duas worktrees, as dez branches locais, os HEADs e o status preexistente de manutenção permaneceram iguais. Dockerfile, csproj, Program.cs, SkiaSharp 4.155.2, JWT, frontend, migrations e testes originais intactos. Nenhum commit, push, PR ou deploy.

As 22 referências Docker históricas, dois containers, quatro volumes e 223 entradas de cache iniciais foram reencontrados na conferência. Permaneceram quatro imagens B06-D2 com tags exclusivas (duas finais e dois estágios) e os auxiliares em `%TEMP%/seenit-b06-d2-b3ae7d6-c529ef`, incluindo os logs de build, os logs das suítes, os manifests no export OCI, SARIF, SPDX e scripts SQL sintéticos. Os containers temporários encerraram e não aparecem no inventário final.

**B06-D2 CANDIDATA APROVADA LOCALMENTE**
