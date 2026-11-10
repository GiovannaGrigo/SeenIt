# B01-A — Rate limiting e recuperação de erros

Implementação e ajustes locais em 2026-11-10. Status: **APROVADO PARA REVISÃO**,
sem aprovação automática para deploy. Não houve deploy, migrations, acesso ao
Supabase real, alterações de configurações no Render/Vercel, commit ou push.
B02, contratos de sucesso e regras JWT foram preservados.

## Diagnóstico e decisão técnica

A composição anterior colocava a janela global antes das concorrências e a janela
específica no limitador de endpoint. No ASP.NET Core 10, uma aquisição rejeitada
é tentada novamente pela via assíncrona, mesmo com fila zero. Descartar a lease de
janela fixa não devolve a unidade consumida. Por isso vinte logins sobrepostos,
com dezoito rejeições de concorrência, consumiam 38 unidades globais.

A solução usa somente limitadores nativos e uma única cadeia GlobalLimiter:

1. Concorrência geral do processo: 8, sem fila.
2. Concorrência da categoria selecionada pelos metadados do endpoint, sem fila.
3. Janela da política: pública compartilhada ou por usuário autenticado.
4. Janela geral do processo: 240 admissões por minuto.

As políticas nomeadas registradas em AddPolicy selecionam a categoria; seu
limitador de endpoint é GetNoLimiter. A janela específica efetiva fica no passo 3.
Isso é intencional: não pode existir um gate falível de endpoint depois da janela
geral, pois reintroduziria a cobrança dupla. As regras de seleção permanecem em
RequireRateLimiting/EnableRateLimiting, inclusive nos controllers.

Uma rejeição específica ocorre antes da janela geral e não a consome. Toda
requisição que atravessa a cadeia, inclusive rotas inexistentes e pedidos que
posteriormente resultam em 400/401/500, precisa de uma unidade geral. Não há
reembolso, contador próprio, bypass baseado em headers ou implementação própria
de RateLimiter. Exceções e cancelamentos liberam concorrência ao encerrar o
pipeline; não restituem cotas por período.

**Mudança semântica explícita:** o orçamento geral contabiliza admissões após os
gates de capacidade e política, em vez de todas as tentativas HTTP. Rejeições
precoces não fazem hashing, acesso ao banco, processamento de imagem ou upload,
mas ainda custam roteamento, autenticação e resposta. Controle de tráfego bruto
antes desses passos depende de proteção na borda (B01-B).

A cadeia não é uma transação: quando a janela geral já está esgotada, a janela
específica anterior ainda pode ser consumida nas duas tentativas. Isso pode reduzir
a cota específica temporariamente, mas não aumenta admissões nem a capacidade
geral. É uma limitação conservadora aceita da composição nativa; não há tentativa
de reembolso. O teste permanente demonstra a correção da cobrança geral.

Referências técnicas:
- https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
- https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Middleware/RateLimiting/src/RateLimitingMiddleware.cs

## Políticas efetivas

Janelas fixas de 60 segundos. Todas as filas: QueueLimit = 0.
Concorrência é compartilhada por categoria no processo, inclusive entre usuários.

| Política | Requisições/minuto | Concorrência da categoria | Aplicação |
| --- | ---: | ---: | --- |
| login | 20 globais | 2 | POST /api/auth/login |
| register | 5 globais | 1 | POST /api/auth/register |
| avatar | 4 por usuário | 1 | PUT /api/profile/avatar |
| mutation | 30 por usuário | 2 | PATCH /api/profile; PUT /api/profile/password; DELETE /api/profile; DELETE /api/profile/avatar; POST, PUT e DELETE /api/my-series; PUT e DELETE /api/episodios |
| read | 20 por usuário | 2 | GET /api/profile e /api/profile/stats |
| expensive | 20 por usuário | 2 | GET /api/profile/watch-time; GET /api/my-series; GET /api/episodios/series/{id}; GET /api/episodios/{id}/votacao-personagens; GET /api/series/{seriesId}/episodios/{episodeId}/personagens |
| catalog | 90 globais | 3 | GET /api/series/search, /api/series/{id}, /api/series/{id}/episodes |

Teto comum: 8 requisições simultâneas e 240 admissões por minuto. A soma dos limites
de categorias não aumenta esse teto. Nenhuma concorrência existente foi elevada.
A nova categoria read separa duas leituras limitadas de banco de operações que
fazem processamento ou HTTP externo. A abertura de perfil pode executar dois reads
e um watch-time juntos. Read mantém a cota anterior de 20/min, agora independente
de expensive. Essa separação amplia o orçamento combinado de leitura por usuário,
mas não os tetos do servidor, e evita disputa direta com as duas vagas caras.

Esses números são conservadores, não uma medição da capacidade do Render Free.
Login/cadastro globais e concorrência sem fila podem rejeitar usuários legítimos
em rajadas; vinte logins sobrepostos não significam vinte vagas simultâneas.
Pode haver até cinco requisições com hashing entre categorias (2 login + 1 cadastro
+ 2 mutações). TVmaze é usado por múltiplas categorias, e watch-time permite até
quatro chamadas externas simultâneas por requisição. O limite de requisições não
substitui um limite compartilhado por recurso nem controla seu volume interno.

## Configuração e identidade

Defaults tipados: backend/SeenIt.Api/RateLimiting/AbuseProtection.cs.
Seção RateLimiting, carregada na inicialização, com limites positivos obrigatórios:

```json
{
  "RateLimiting": {
    "GlobalPerMinute": 240,
    "GlobalConcurrency": 8,
    "Login": { "PerMinute": 20, "Concurrency": 2 },
    "Register": { "PerMinute": 5, "Concurrency": 1 },
    "Avatar": { "PerMinute": 4, "Concurrency": 1 },
    "Mutation": { "PerMinute": 30, "Concurrency": 2 },
    "Read": { "PerMinute": 20, "Concurrency": 2 },
    "Expensive": { "PerMinute": 20, "Concurrency": 2 },
    "Catalog": { "PerMinute": 90, "Concurrency": 3 }
  }
}
```

Contadores ficam em memória, reiniciam com o container e não são compartilhados
entre instâncias. Ajustes exigem reinício. Não foram alteradas variáveis remotas.
A proteção usa aquisição/liberação por requisição no middleware nativo, sem worker
próprio nem timer próprio como estratégia de proteção.

Partições autenticadas exigem Identity.IsAuthenticated e GUID de NameIdentifier/sub
após a validação JWT existente. JWT inválido e ausência de identidade válida usam
um único fallback anonymous por política. IP, X-Forwarded-For, Forwarded, email,
URL, jti e campos do corpo não são chaves. JWT válido não torna a cota pública
individual. Não houve alteração de confiança em proxies nem das regras JWT.

Ordem final: forwarded headers condicional → HTTPS (exceto health) → arquivos
estáticos → roteamento → CORS → UseAuthentication → UseRateLimiter →
UseAuthorization → binding/filtros/handlers. O filtro e o binding B02 permanecem
intactos. Um pedido não autorizado pode receber 429 antes de 401 se saturado.

GET /health permanece isento, com {"status":"ok"}, sem consultas externas.
Arquivos estáticos e preflight CORS atendidos antes do limitador também são
exceções deliberadas. OpenAPI continua apenas em Development.

## HTTP 429, CORS e Angular

Rejeições retornam HTTP 429, JSON com message genérica e Cache-Control: no-store,
sem política, identidade, informações de conta ou erros internos. Retry-After é
copiado apenas quando a lease fornece metadado confiável, com segundos inteiros
arredondados para cima. Concorrência não inventa uma previsão de liberação.

CORS expõe somente Retry-After às origens já autorizadas. A seleção das origens
permanece igual; não há wildcard ou alteração na configuração da origem de produção
https://seenit-web-dusky.vercel.app. Testes usam essa origem como fixture local.

http-error.ts interpreta segundos inteiros e HTTP-date no formato IMF-fixdate;
valores ausentes/inválidos usam orientação genérica. Não há contagem regressiva
obrigatória nem garantia de vaga após a espera. Datas HTTP antigas/obsoletas não
são usadas pelo backend e recebem o fallback seguro no frontend.

Login diferencia 401 (credenciais), 429 (aguardar e tentar novamente), 503/502/504
(indisponibilidade), status 0 (conexão) e demais falhas com textos definidos no
frontend. O corpo bruto da API não é exibido. Cadastro preserva seu tratamento de
409 e usa a mesma classificação segura para outras falhas.

HANDLE_ERROR_LOCALLY define um responsável por mensagem: componente ou interceptor.
Login/cadastro e erros inline de perfil/leitura não geram também um toast global.
Senha e exclusão de conta usam mensagens locais sanitizadas. Escritas sem erro
inline continuam usando o interceptor; não são repetidas automaticamente.

A tela de série trata cada leitura do forkJoin isoladamente e aplica resultados
conforme chegam. Falha parcial não descarta dados bem-sucedidos. Há nova tentativa
manual das leituras e dos episódios assistidos; finalize encerra estados de
carregamento/escrita. Ações dependentes de dados indisponíveis aguardam a recuperação
manual, com erro e botão visíveis, em vez de manter um spinner indefinido. Perfil
e estatísticas têm estados de erro e nova tentativa independentes.

Cold start do Render não foi simulado nem medido. Pedidos podem chegar em rajada
após a retomada e receber 429/indisponibilidade. As mensagens agora distinguem essas
falhas de credenciais inválidas; isso não remove a limitação operacional.

## Testes locais

- dotnet run --project backend/SeenIt.RegisterIntegrationChecks --no-restore:
  73 casos HTTP B02, suite original de rate limiting e regressões permanentes.
  SQLite em memória, settings JWT sintéticos e HTTP externo bloqueado/substituído.
- Regressão de vinte logins sobrepostos: 2 admitidos e 18 rejeitados, com exatamente
  2 unidades globais consumidas; as 38 restantes de uma cota local de 40 são
  exercitadas até o próximo 429. Rejeições não acessam DB/hasher/gravações.
- Saturação da janela específica não consome orçamento geral; headers forjados não
  a contornam; health continua funcionando com orçamento esgotado.
- Perfil, stats e watch-time são mantidos simultaneamente em execução e terminam
  com 200; leitura leve adicional recebe 429, comprovando a separação de vagas.
- Três vagas de catálogo recuperadas após exceção, cancelamentos e sucesso.
- Retry-After e headers CORS verificados também numa resposta 429 à origem de
  produção; origem não autorizada não recebe Access-Control-Allow-Origin.
- npm run check:rate-limiting (frontend): métodos reais dos componentes/services,
  RxJS e forms reais com DI/HTTP isolados. Cobre 401/429/503/rede, sanitização,
  segundos/data/ausência de Retry-After, dono único do erro, respostas parciais,
  nova tentativa manual, finalize e ausência de repetição de escrita.
- Testes frontend não são testes E2E de navegador; npm run build verifica templates
  e tipos Angular. Build tem aviso de orçamento CSS de profile.component.scss
  (8,39 kB para aviso de 8 kB), arquivo não alterado nesta etapa.
- Regressões adicionais: SeenIt.Checks (72 casos de validação B02, watch-time e
  classificação), SeenIt.AvatarChecks (16 grupos), checks/library.cjs e
  checks/watch-time.cjs.

Nenhum teste de carga em produção, Supabase real ou resultado de desempenho do
Render é usado como evidência. O roteiro permanente está nos arquivos de checks.

## Pendências B01-B e I07

- B01-B: validar a cadeia de proxies antes de limitar por IP; proteção na borda,
  orçamento distribuído, métricas agregadas sem PII e controle de muitas contas.
- Falhas por identificador normalizado precisam de store com TTL e capacidade
  limitada, chave protegida e comportamento indistinguível para contas inexistentes.
  Dicionário ilimitado de emails inventados não foi introduzido.
- Limitar volume de séries/episódios e tarefas pendentes de watch-time: o código
  atual cria uma tarefa por série antes do SemaphoreSlim(4), sem teto próprio de
  tarefas aguardando. O middleware limita requisições, não essa expansão interna.
- Avaliar orçamento compartilhado por recurso para hashing/TVmaze, cancelamento
  cooperativo de processamento síncrono, custos de memória e duração das operações.
- Cotas globais compartilhadas permitem indisponibilidade dirigida por um agente;
  janelas fixas permitem rajadas e não oferecem justiça entre usuários.
- docs/SECURITY-RELEASE-REVIEW.md segue indisponível no checkout/histórico local
  examinado. Os textos e critérios originais de B01, E01 e I07 não foram inventados.
  Relacionar estas pendências ao achado I07 e confirmar seu encerramento exige o
  relatório original; não se declara I07 corrigido nesta etapa.

Não houve refatoração estrutural de watch-time, imagem ou catálogo, nem alteração
em outros achados da auditoria. A próxima decisão é revisão de código e dos testes,
não deploy automático.