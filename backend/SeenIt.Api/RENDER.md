# Preparação para Render

Nenhum deploy ou migration é executado pela inicialização. Ainda é necessário preparar o artefato/container .NET 10 e uma etapa controlada de migrations antes da publicação.

## Variáveis de ambiente

Exemplo documental: os marcadores abaixo NÃO são valores válidos e precisam ser substituídos no painel do serviço. Arquivos `.env` não são carregados automaticamente pelo ASP.NET Core.

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<CONEXAO_POSTGRESQL_COM_TLS>
Jwt__Key=<BASE64_DE_PELO_MENOS_32_BYTES_CRIPTOGRAFICAMENTE_ALEATORIOS>
Jwt__Issuer=<IDENTIFICADOR_DO_EMISSOR>
Jwt__Audience=<IDENTIFICADOR_DO_PUBLICO>
Jwt__ExpirationMinutes=<INTEIRO_POSITIVO_EM_MINUTOS>
Cors__AllowedOrigins__0=<ORIGEM_HTTPS_EXATA_DO_FRONTEND_SEM_BARRA_FINAL>
ReverseProxy__KnownProxies__0=<IP_DO_PROXY_IMEDIATO_CONFIRMADO_PELO_PROVEDOR>
```

Em Production, conexão não vazia e todos os parâmetros JWT são obrigatórios. Issuer, audience e expiração têm defaults em `appsettings.json`; confirme seus valores para produção. A chave não tem default: deve ser um segredo novo gerado com um gerador criptográfico, usando pelo menos 32 bytes aleatórios codificados em Base64. Não reutilize a chave local nem uma chave que já esteve no Git. A validação rejeita formato/tamanho inválidos e bytes todos iguais; nenhum teste de uma string consegue comprovar a entropia ou a origem aleatória. A geração segura continua obrigatória.

Para preservar o contrato de assinatura existente, a string Base64 é usada como texto UTF-8 no HS256, exatamente como as chaves anteriores; ela não é decodificada na assinatura. A decodificação em Production serve apenas à validação do formato e tamanho. User Secrets e a chave local não são alterados.

Não há domínio fictício de Vercel/Render nem credenciais neste exemplo. Configure a conexão Npgsql conforme o endpoint PostgreSQL/Supabase escolhido, com TLS e validação de certificado adequados, sem desabilitá-la.

CORS sem origens configuradas não autoriza nenhuma origem em Production. Configure a origem HTTPS exata quando ela existir. HTTP, curingas, caminhos, credenciais, query strings e barras finais são rejeitados. Acrescente outras origens explicitamente com índices `__1`, `__2`, etc.; não há wildcard de previews. Em Development, `http://localhost:4200` é sempre preservada.

## Porta e execução local

`PORT`, quando fornecida pelo Render, define a escuta HTTP em `0.0.0.0` naquela porta (1–65535). Não coloque marcadores de exemplo nessa variável. Sem `PORT`, a aplicação mantém a configuração normal de URLs, inclusive o HTTPS/HTTP de `Properties/launchSettings.json` ao executar `dotnet run` localmente. Não deixe `PORT` definida no terminal local se quiser usar esse perfil.

`GET /health` é anônimo e retorna `200` com `{"status":"ok"}`, inclusive via HTTP interno. Não acessa o PostgreSQL e não comprova a disponibilidade do banco. Somente esse caminho é excluído do redirecionamento HTTPS; os demais preservam o middleware existente.

## Reverse proxy e TLS: pendência antes de publicar

O Render termina TLS no balanceador e encaminha HTTP para a aplicação. É necessário confirmar os IPs do proxy imediatamente conectado à API, a cadeia de saltos e como o provedor normaliza `X-Forwarded-Proto` e `X-Forwarded-For`. Não inferir esses IPs a partir do DNS público, dos IPs de saída do serviço ou de cabeçalhos enviados por clientes.

Somente quando `ReverseProxy:KnownProxies` contém IPs explícitos o middleware de forwarded headers é ativado. A lista de confiança fica restrita a esses IPs (incluindo a representação IPv4 mapeada em IPv6); não são liberadas redes inteiras. Apenas `X-Forwarded-Proto` e `X-Forwarded-For` são processados, com limite de um salto, antes do redirecionamento HTTPS, dos arquivos estáticos e da geração de URLs. `X-Forwarded-Host` não é aceito.

Sem essa lista, os cabeçalhos são ignorados. Não habilite `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: a aplicação rejeita essa opção por ela ativar confiança ampla no framework. Configure também `AllowedHosts` com o host público confirmado antes de publicar; atualmente o default continua `*` para preservar o funcionamento local.

Após confirmar a topologia, valide que a API reconhece o protocolo HTTPS externo, gera URLs HTTPS e não redireciona em loop. Se houver múltiplos saltos ou endereços variáveis sem uma lista confiável, essa configuração ainda é insuficiente: revisar a topologia com o provedor antes do deploy, sem aceitar qualquer proxy nem forçar `Request.Scheme` a partir de cabeçalhos não confiáveis.

Referências: [Render Web Services](https://render.com/docs/web-services), [ASP.NET Core: proxies e balanceadores](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

## Verificação posterior

Executar builds e testes em homologação; aplicar migrations separadamente; validar CORS positivo/negativo, `/health`, login e geração de URLs através do proxy real. O armazenamento local de avatares permanece inalterado e requer uma solução persistente antes de habilitar uploads no Render.
