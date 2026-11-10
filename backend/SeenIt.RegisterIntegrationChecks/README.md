# Cadastro: checks integrados locais

Executar na raiz do repositório:

```powershell
dotnet run --project backend/SeenIt.Checks/SeenIt.Checks.csproj
dotnet run --project backend/SeenIt.RegisterIntegrationChecks/SeenIt.RegisterIntegrationChecks.csproj
dotnet build backend/SeenIt.Api/SeenIt.Api.csproj
git diff --check
```

O primeiro projeto preserva os 66 casos HTTP anteriores e acrescenta seis regressões: NUL no início/meio/fim do nome, acentos, apóstrofo e hífen (72 casos). Mantém também os checks existentes de classificação e tempo assistido.

O segundo projeto inicia o entry point real da API (`Program.cs`) usando `WebApplicationFactory<TokenService>` e Kestrel em porta local dinâmica. `TokenService` é apenas um marcador da assembly da API, não um endpoint alternativo. Não há cópia do handler nem mudança na lógica de autenticação. A raiz da API é informada por atributo gerado no build, pois o repositório não contém arquivo de solution.

São 73 casos HTTP integrados. A factory usa ambiente Production com configuração sintética, substitui Npgsql por SQLite exclusivamente em memória, observa comandos SQL e tentativas de SaveChanges e envolve o PasswordHasher original com contador. O JWT real continua sendo gerado com chave exclusivamente de teste. Storage é substituído por implementação isolada e qualquer criação de cliente HTTP externo falha imediatamente. Não são usadas credenciais reais, migrations ou conexões Supabase. `EnsureCreated` cria apenas o schema vazio em memória; os 16 usuários criados desaparecem ao encerrar a conexão.

Para cada requisição inválida, são comparados os contadores antes/depois: nenhum comando SQL (inclusive consulta), tentativa de gravação ou hash pode ocorrer. Cadastros válidos precisam consultar, gerar um hash e salvar uma vez; os testes conferem o registro persistido, a normalização, a verificação da senha original, o status 201, Location e campos JSON. E-mail duplicado deve retornar 409 com a mensagem existente, sem hash ou gravação. A contagem final de usuários também é conferida.

Kestrel é necessário porque o TestServer propaga ao cliente as BadHttpRequestExceptions do binder, enquanto Kestrel as traduz para respostas HTTP. O cliente usa HTTP exclusivamente local; esses testes não certificam TLS, proxy ou configuração de hospedagem. Nenhuma validação TLS de produção é modificada. Os cenários de streaming enviam corpo de tamanho desconhecido em fragmentos com pausas, incluindo o limite exato e o excesso de um byte.

## Caracteres textuais e limites da evidência

- Nome com NUL recebe 400 e erro no campo `name`, antes de consultar/hash/gravar. O caractere é rejeitado, nunca removido silenciosamente.
- E-mail com NUL no início/meio/fim já é rejeitado por `char.IsControl`; o comportamento é verificado pelo endpoint real.
- Senha com NUL é preservada e aceita quando satisfaz as demais regras: somente o hash gerado pelo PasswordHasher é persistido. O teste confere esse hash com a senha exata. Não se aplica à senha uma restrição destinada a colunas de texto que armazenam o nome/e-mail.
- Acentos, apóstrofo, hífen, nomes chineses/árabes e acento combinante são aceitos. Outros controles não NUL são cobertos sem criar uma proibição geral. Surrogate UTF-16 isolado no JSON resulta em 400 pelo binder.
- A [documentação PostgreSQL sobre tipos textuais](https://www.postgresql.org/docs/current/datatype-character.html) exclui o caractere de código zero. Não foi identificada razão para proibir indiscriminadamente outros controles ou alfabetos em banco UTF-8. Encoding/configuração efetivos do PostgreSQL não são verificados por SQLite.

SQLite não substitui uma futura integração com PostgreSQL isolado para comportamento específico de encoding, erros do provedor, concorrência e constraints. Os testes atuais comprovam o pipeline real de cadastro e seus efeitos observáveis com dependências isoladas. Limitação de concorrência, rate limiting, cold start e demais achados da auditoria permanecem fora do escopo B02. Aprovar esta alteração não equivale a aprovar integralmente o lançamento público.
