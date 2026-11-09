# Avatares: Supabase Storage e compatibilidade local

## Contratos e configuração

Permanecem `PUT /api/profile/avatar` (multipart, campo `avatar`, resposta `ProfileResponse`), `DELETE /api/profile/avatar` (204) e `DELETE /api/profile` (senha no corpo, 204), protegidos pelo JWT existente. Perfil, cadastro e login usam o mesmo `IAvatarStorageService.GetPublicUrl`. Não existe AuthController neste projeto: os endpoints de cadastro/login estão em `Program.cs`.

O banco continua escolhido exclusivamente por `ConnectionStrings:DefaultConnection`. A integração **não lê SupabaseTest**, não aplica migrations e não troca o banco conforme a opção de Storage. Ao testar o banco remoto, a substituição da conexão deve ser explícita e restrita ao processo temporário, como nos testes anteriores.

Em Development, o padrão é **local/offline**, mesmo com as três chaves já presentes nos User Secrets. Para habilitar o Storage, configure `SupabaseStorage:Enabled=true`. Com `false`, os novos objetos usam `wwwroot/uploads/profiles/local/{userId}/{guid}.webp`; URLs remotas já existentes ainda podem ser exibidas quando `Url` está configurada, mas exclusões remotas são adiadas e registradas, sem contato com Supabase.

Em Production, o Storage é **sempre obrigatório**: `Enabled` é forçado a true; configuração ausente/inválida impede a inicialização. Não há fallback para o disco em Production ou após falha do Storage.

Configurações (valores sensíveis devem ser fornecidos pelo gerenciador de segredos, nunca no Git, frontend, argumentos do processo ou logs):

| Chave | Valor esperado |
|---|---|
| `SupabaseStorage:Url` | URL raiz HTTPS do projeto: `https://<project-ref>.supabase.co` |
| `SupabaseStorage:SecretKey` | Secret API key do backend, formato `sb_secret_...` |
| `SupabaseStorage:Bucket` | `avatars` |
| `SupabaseStorage:Enabled` | `true` para opt-in em Development; Production exige Storage |

As três primeiras já existem nos User Secrets do projeto `backend/SeenIt.Api`; não são modificadas por esta implementação. A opção não sensível pode ser configurada com `dotnet user-secrets set "SupabaseStorage:Enabled" "true" --project backend/SeenIt.Api`, ou temporariamente com `SupabaseStorage__Enabled=true` no ambiente do processo. Não use `dotnet user-secrets list` em saída compartilhada. Em hospedagem, use as equivalentes `SupabaseStorage__Url`, `SupabaseStorage__SecretKey` e `SupabaseStorage__Bucket` no secret manager. Nunca copie a chave para o Angular.

São rejeitados hosts arbitrários, HTTP, portas não padrão, credenciais na URL, caminhos/query/fragmentos e buckets diferentes de `avatars`. Redirecionamentos HTTP são desabilitados. A chave é enviada **somente em `apikey`**, por requisição; não é JWT e não é enviada em `Authorization: Bearer`. Os loggers automáticos desse HttpClient são removidos e os cabeçalhos são também marcados para redação. Corpos de erro e exceções HTTP originais não são enviados ao cliente nem registrados pelo serviço.

Endpoints oficiais utilizados:

- Upload: `POST /storage/v1/object/avatars/{userId}/{guid}.webp`, bytes `image/webp`, `x-upsert: false`.
- Exclusão: `DELETE /storage/v1/object/avatars`, JSON `{"prefixes":["{userId}/{guid}.webp"]}`.
- Acesso público: `/storage/v1/object/public/avatars/{userId}/{guid}.webp`, sem chave ou token na URL.

O serviço só aceita caminhos gerados e pertencentes ao usuário autenticado; o cliente não informa o id nem o nome do objeto. O campo `User.AvatarFileName` permanece `varchar(200)` e armazena somente o caminho relativo. Bucket público significa que quem conhecer a URL poderá ver a imagem: não enviar material privado.

## Validação de imagem

SkiaSharp 4.155.0 (MIT) e seus assets nativos Linux correspondentes decodificam o conteúdo, sem confiar no nome/extensão. São aceitos JPEG, PNG e WebP coerentes com o MIME informado, até 5 MiB, 8192 pixels por lado e 16 milhões de pixels. Imagens animadas, formatos não permitidos e decodificações incompletas são rejeitados. Duas decodificações simultâneas no máximo limitam a memória do processo.

A imagem é orientada, redimensionada preservando proporção até 1024 pixels no maior lado, regravada em WebP (qualidade 85), sem carregar metadados/bytes anexados para o objeto final. Também é conferido o limite de 5 MiB após a conversão. O limite da requisição é 5 MiB + 64 KiB para o envelope multipart; cada seção mantém o limite de 5 MiB. Ajustar o proxy de hospedagem para aceitar essa margem.

O pacote nativo Linux usa glibc, apropriado a imagens Debian/Ubuntu; validar a imagem de hospedagem. Alpine/musl exige a variante nativa apropriada e não foi validado aqui. Não remover assets nativos no publish/trimming sem testar a decodificação no artefato final. Atualizações da biblioteca nativa também precisam de auditoria; NuGetAudit não substitui a verificação de CVEs do código nativo do Skia.

## Consistência, concorrência e falhas

1. Valida/reprocessa a imagem; faz upload com nome único, sem sobrescrever.
2. Atualiza somente `AvatarFileName`, usando compare-and-swap SQL por `Id` e referência anterior. Não há mudança no modelo ou snapshot.
3. Só após confirmação do banco tenta excluir o objeto anterior.

Conflito concorrente retorna 409 e remove somente o upload do concorrente que perdeu. Falha de persistência tenta compensar o novo objeto com um token próprio de timeout, independente do cancelamento do pedido. Se uma falha do banco puder ter ocorrido depois do commit, primeiro consulta a referência: um objeto ainda referenciado nunca é removido. Se nem essa consulta funcionar, preserva o objeto e registra `AvatarCleanupRequired` para investigação. Não existe transação distribuída Storage/PostgreSQL; um crash entre etapas pode deixar órfãos, nunca deve motivar uma exclusão cega.

Na exclusão, remove condicionalmente a referência antes do objeto. Se o Storage falhar depois de o banco confirmar, o endpoint mantém 204 e a referência nula, registrando limpeza pendente. Na exclusão da conta, o DELETE SQL também exige a referência e o hash de senha já verificados; alterações concorrentes retornam 409. As cascatas já existentes removem os dados associados, e só então o avatar é limpo. Senha/hash não são registrados.

Falhas de Storage/persistência retornam mensagens genéricas (503); imagem inválida continua 400. Banco com resultado desconhecido durante remoção/exclusão de conta preserva o arquivo e gera evento de limpeza. Monitorar e persistir operacionalmente os eventos `AvatarCleanupRequired` e `AvatarLegacyRetained`: eles contêm usuário/caminho, nunca chave ou resposta HTTP original. Não foi adicionada fila ou tabela de outbox; reconciliar órfãos após confirmar que o objeto não é referenciado no banco correto. Não executar limpeza simultânea a uma migração de referências sem coordenação. CDN pode manter imagens públicas em cache após exclusão do objeto; o nome único impede reaproveitar conteúdo antigo em um upload novo.

## Legados: preservar antes de migrar

Arquivos antigos `{guid:N}.jpg|png|webp`, sem diretório, continuam com URL da API em `/uploads/profiles/{nome}`. **Não são convertidos em URL Supabase nem apagados**, mesmo ao substituir/remover avatar ou excluir a conta: a política conservadora evita destruir arquivos preexistentes sem migração auditada. O evento `AvatarLegacyRetained` indica essa retenção. Novos arquivos locais usam prefixo `local/`, distinguindo-os de objetos remotos.

Antes de hospedar em disco efêmero, copiar os arquivos legados necessários e preservar o backup. Procedimento sugerido para uma migração futura, com autorização separada:

1. Inventariar referências e arquivos no banco/ambiente correto; verificar propriedade e referências compartilhadas. Não inferir que um arquivo sem pasta pertence a determinado usuário.
2. Para cada referência confirmada, validar e reprocessar o arquivo e enviar a um caminho novo do respectivo usuário no bucket `avatars`.
3. Conferir o objeto e sua URL; atualizar condicionalmente a referência somente se ainda for a mesma, compensando uploads que perderem a concorrência.
4. Manter arquivos originais e backups até a auditoria concluir. Arquivos ausentes devem ser reportados, não substituídos por URLs inexistentes.
5. Só excluir arquivos legados mediante inventário aprovado e confirmação de ausência de referências. Nenhuma dessas etapas é executada automaticamente pelo aplicativo.

Se Development estiver offline e remover uma referência remota, o objeto será conservado para limpeza futura. Ao alternar para Production, referências `local/` também precisam da mesma migração; a integração não promete persistência de discos efêmeros.

## Verificação automatizada e teste integrado pendente

```powershell
dotnet restore backend/SeenIt.Api/SeenIt.Api.csproj
dotnet restore backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj
dotnet build backend/SeenIt.Api/SeenIt.Api.csproj --no-restore
dotnet run --project backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj --no-restore
dotnet run --project backend/SeenIt.Checks/SeenIt.Checks.csproj --no-restore
dotnet package list --project backend/SeenIt.Api/SeenIt.Api.csproj --vulnerable --include-transitive
```

`SeenIt.AvatarChecks` segue o formato de verificações executáveis já utilizado no repositório: falhas encerram o processo com código diferente de zero. Simula HttpClient e persistência; não carrega User Secrets e não acessa banco/Storage reais. Cobre autenticação apikey sem Bearer, caminhos/URLs, opções de ambiente, imagens reais, substituição/compensação, concorrência, exclusão, falhas e retenção de legados.

Pendente, mediante autorização: iniciar uma API temporária com `SupabaseStorage__Enabled=true` e, **somente se desejado**, substituir `DefaultConnection` no ambiente desse processo pelo valor de `SupabaseTest`, nunca por argumentos. Criar um único usuário fictício, testar upload/substituição/exclusão e conferir caminho no banco e objetos no bucket; conferir perfil e login retornando a mesma URL. Testar exclusão da própria conta com avatar e confirmar limpeza. Remover só os dados dessa execução e encerrar a instância. Validar o runtime Linux e a exibição no Angular sem alterar o frontend.

Também conferir no painel: bucket público `avatars`, limite de 5 MiB, MIME `image/webp` habilitado, chave sb_secret_ vinculada ao mesmo projeto e escopo exclusivamente backend. Manter a Data API de tabelas de negócio desativada/restrita conforme a auditoria anterior; isso é independente do acesso público aos objetos do Storage.

Referências: [API keys](https://supabase.com/docs/guides/api/api-keys), [Storage HTTP API](https://supabase.com/docs/reference/self-hosting-storage/introduction), [URLs públicas](https://supabase.com/docs/guides/storage/serving/downloads), [SkiaSharp](https://github.com/mono/SkiaSharp).
