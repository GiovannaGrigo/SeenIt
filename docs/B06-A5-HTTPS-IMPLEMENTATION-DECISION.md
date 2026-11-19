# B06-A5 — Fechamento das pendências e decisão de implementação HTTPS

Data: 19/11/2026. **Decisão: IMPLEMENTAÇÃO CONDICIONAL.** Experimento aprovado localmente; não há autorização para implementar ou alterar configuração remota nesta etapa.

## 1. Contexto e estado inicial

Branch confirmada: `security/b06-a1-hardening-diagnosis`. HEAD e referência local origin/main: `09e9c40f85d7a020804ee13cc3a014b84d7d81ef`. A igualdade é com a referência local; não houve nova consulta remota ou fetch.

Estado inicial: exclusivamente os quatro relatórios B06-A1/A2/A3/A4 não rastreados; nenhum arquivo rastreado modificado na árvore principal. Os quatro foram localizados e preservados. Nenhuma divergência inesperada.

Worktree maintenance-phase1 preservada em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seus seis arquivos modificados e seis entradas não rastreadas preexistentes. Snapshot de hashes SHA-256 de 233 arquivos preexistentes da árvore principal e 161 arquivos rastreados/não ignorados da worktree de manutenção. Nenhuma branch/worktree foi criada ou excluída.

## 2. Evidências existentes

- B06-A2: 29/29 testes isolados aprovados; valida a política de forwarded headers e limites do redirect em ambiente sintético.
- B06-A4: 308/308 verificações aprovadas; padrão ausente/false equivalente ao anterior, opção true sem redirect adicional na simulação, JWT e headers preservados no escopo testado. URLs locais/legadas HTTP permaneceram possíveis.
- B06-A3: o operador informou a ausência no Environment do Render de KnownProxies, do atalho automático, PORT, ASPNETCORE_URLS e ASPNETCORE_HTTPS_PORT. Não foram solicitados valores de segredos.
- B05-B4-FINAL-SUMMARY, seções 9–13: integração do PR #5, relato de Deploy Latest Commit/Live/Production/porta 10000 e testes funcionais aprovados pelo operador, com limitação expressa de identidade do artefato.
- Código e Git locais: modelo de usuários, migrations, serviços de avatar, configuração do frontend e identidade do Dockerfile entre os dois commits citados foram conferidos nesta etapa.

Nenhum teste anterior foi repetido. Não houve novo experimento, build, execução da API, SQL ou conexão a serviços de produção.

## 3. Identidade do deploy: o que se sabe e o que falta

| Referência | Evidência local | O que permite concluir |
| --- | --- | --- |
| `0720353be82f9af2a8d5f80240d57839121d844e` | Merge do PR #5; comparação com primeiro pai mostra duas instruções FROM alteradas e quatro relatórios | Commit da integração técnica das bases corrigidas |
| `09e9c40f85d7a020804ee13cc3a014b84d7d81ef` | Único commit posterior no intervalo; acrescenta `docs/B05-B4-FINAL-SUMMARY.md` | Fechamento documental, sem nova correção funcional |
| Dockerfile nos dois commits | Mesmo blob Git: `a0f7860374f7b5cbc1f17f5270c1223e695881ba` | Ambos contêm o mesmo Dockerfile com digests fixados |
| Árvores backend/frontend entre os commits | `git diff --quiet 0720353 09e9c40 -- backend frontend`, exit 0 | Sem diferença funcional nessas árvores |
| Deploy Latest Commit do PR #5 | Relato do operador, já documentado em B05-B4 | Evidência operacional consistente com publicação após a integração; não fornece sozinho SHA/digest executados |

As bases corrigidas registradas são SDK `sha256:ddbaedfc8b774389d91a511af08b77296f2c7a8d15d86f6214d2a87166933625` e ASP.NET `sha256:05acd0ca475e2130832bab441d28a7dd31041129bad28eac7928151fd73cd2c5`. São digests das bases, não da imagem final da API. As versões verificadas localmente em B05 não são medições do runtime remoto.

Portanto, se o deploy usou qualquer um desses dois commits com esse Dockerfile, a distinção documental não altera a correção das bases. Isso reduz a incerteza sobre o conteúdo candidato, mas **não identifica exatamente o artefato implantado**: ainda faltam o registro do deploy ativo associado ao SHA, contexto/Dockerfile utilizados e, se disponível, digest/ID da imagem final e versão do runtime. Não presumir que origin/main ou o botão Latest Commit indiquem retrospectivamente o deploy atualmente ativo.

Pergunta operacional: **qual SHA aparece no deploy atualmente Live, e o registro de build desse deploy confirma `backend/SeenIt.Api/Dockerfile` com o contexto esperado? Há digest da imagem final ou identificação do runtime nesse mesmo registro?** Basta evidência sanitizada de identificação, sem dump de ambiente ou segredos. Nenhum acesso ao Render foi feito para obter isso.

## 4. Inventário dos ingressos

| Ingresso/caminho | Situação comprovável | Responsabilidade e evidência | Informação faltante |
| --- | --- | --- | --- |
| Domínio público Render | `frontend/src/environments/environment.production.ts:3` referencia `https://seenit-api-cvbc.onrender.com/api` | Para o ingresso público padrão, TLS e redirect devem ser da borda Render, segundo documentação oficial | Confirmar que esse é o domínio usado pelo frontend implantado e que pertence ao serviço ativo analisado |
| Domínios personalizados | Nenhum domínio personalizado da API foi identificado nas evidências examinadas | Se configurados no Render, a plataforma documenta TLS/redirect também nesses domínios | Existem? Se sim, quais e passam exclusivamente pela borda correspondente? |
| Acesso privado entre serviços | Não foi comprovado para SeenIt | Não assumir enforcement público sobre tráfego privado; a política depende de rede e consumidores | Há algum consumidor que use endereço interno/porta diretamente? Qual política protege esse trajeto? |
| Health | `/health` existe, permite HTTP e é anônimo; documentação local recomenda esse caminho | Liveness interno é exceção intencional ao redirect, não prova de TLS público; plataforma permite probes TCP ou HTTP | O serviço ativo usa probe TCP ou HTTP? Se HTTP, qual caminho/Host? |
| Proxies/CDNs adicionais | Nenhum adicional confirmado para a implantação | Quem controla cada hop deve definir sanitização e enforcement | Existe proxy/CDN próprio antes ou depois do ingresso público padrão? |
| Acesso direto que contorne a borda | Não demonstrado; bind HTTP em 0.0.0.0:PORT não prova exposição pública direta | Exige inventário real de rede, sem inferir que qualquer cliente externo pode acessar a porta | Existe rota alternativa ao backend, interna ou externa, além dos caminhos acima? |

O endereço público foi obtido de arquivo versionado, sem resolução DNS ou request. Checklist antigo que menciona placeholder `.invalid` não deve prevalecer sobre o conteúdo atual desse arquivo. Isso tampouco comprova qual frontend foi publicado.

As rotas `/api/auth`, `/api/series`, `/api/my-series`, `/api/profile` e `/api/episodios` identificam funcionalidades; não constituem evidência de ingressos de rede independentes.

Pergunta operacional consolidada: **confirma o domínio acima e informa se há domínio personalizado, consumidor privado, proxy adicional ou outro caminho de acesso? Para cada caminho existente, quem exige HTTPS ou qual exceção interna foi deliberadamente adotada?** Ausência dos caminhos opcionais pode ser respondida explicitamente como “não existe”; não se afirma sua existência por hipótese.

## 5. Responsabilidade por HTTPS

A documentação Render descreve TLS e redirect HTTP → HTTPS na borda pública. [TLS gerenciado Render](https://render.com/docs/tls). O health check pode ser TCP por padrão ou HTTP configurado; não deve ser confundido com tráfego público de usuário. [Health checks Render](https://render.com/docs/health-checks).

A política candidata delega apenas o redirect da aplicação. Não habilita TLS, não valida a borda e não restaura Scheme. Com KnownProxies vazio, o backend tende a continuar vendo HTTP interno. Isso é compatível com TLS externo e com o warning já diagnosticado, sem provar vulnerabilidade.

Para cada ingresso real, o contrato precisa distinguir acesso público protegido, tráfego privado tratado conforme risco e health interno. A evidência da plataforma sustenta o modelo esperado do domínio padrão; a declaração/inventário do operador deve confirmar que ele cobre a implantação.

## 6. Dependências de Request.Scheme

O consumidor funcional identificado é `AvatarStorageService.GetPublicUrl`: ramo legado ou local pertencente ao usuário usa Scheme, Host e PathBase para URL absoluta. Cadastro/login e respostas de perfil/avatar podem chamar esse método.

Sem avatar, retorna null. Caminho de Storage válido usa a origem HTTPS configurada, independentemente do Scheme do request. JWT e o rate limiter atual não necessitam de Scheme/IP original para as regras examinadas. O redirect interno com destino HTTPS exige Scheme correto para não redirecionar tráfego público HTTPS repetidamente.

Desligar o middleware de redirect não corrige essa dependência de URLs, e sua remoção não deve ser usada como prova de encerramento do problema.

## 7. Persistência e classificação dos avatares

Modelo: `backend/SeenIt.Api/Domain/Models/User.cs:9`, propriedade nullable `AvatarFileName`. `Data/AppDbContext.cs` expõe Users e limita a referência a 200 caracteres. A migration `20260915120902_InitialPostgres.cs` cria a tabela **`"Users"`**, coluna **`"AvatarFileName"`**, varchar(200), e **`"Id"`**, uuid; o ModelSnapshot mantém esse mapeamento.

Não existe schema explícito no mapeamento/migration examinados. A consulta abaixo usa o nome não qualificado, como o mapeamento: depende de search_path correto para a mesma tabela utilizada pela API. Não se inventa um schema remoto nem se presume sua confirmação. A coluna guarda um caminho/referência, não a URL pública completa.

| Classe | Regra do código | Consequência |
| --- | --- | --- |
| Sem avatar | null ou string somente de whitespace | GetPublicUrl retorna null |
| Legado | IsLegacy: `\A[0-9a-f]{32}\.(jpg|png|webp)\z` | Nome simples hexadecimal minúsculo; usa Scheme/Host, inclusive em Production |
| Local reconhecido | IsLocal: prefixo literal `local/`, e IsOwned válido | Usa Scheme/Host. IsLocal sozinho não comprova propriedade ou caminho válido |
| Supabase reconhecido | IsOwned válido sem prefixo local/ | `<Id no formato D>/<32 hex minúsculos>.webp`; origem HTTPS vem das opções de Storage |
| Não classificado | Referência não vazia que não satisfaz os casos anteriores | Pode resultar em null ou exigir investigação; não presumir objeto válido nem eliminar silenciosamente |

IsOwned rejeita Guid.Empty e comprimento maior que 200; quando há local/, remove esse prefixo antes de comparar o UUID do próprio usuário e o nome do objeto. Os padrões são case-sensitive. Não basta contar qualquer string com barra como Storage.

Novos uploads em Production usam Storage, mas não há migração automática que reescreva todos os AvatarFileName antigos durante deploy. O código mantém leitura de legado e retém arquivos legados em determinadas limpezas. Um redeploy não demonstra ausência dessas referências; testes funcionais de upload também não cobrem todos os usuários históricos.

Com Scheme=http, os ramos local/legado geram URL começando com http, mesmo que o cliente tenha chegado por HTTPS público. B06-A4 reproduziu isso. Não foi comprovada ocorrência real nem inexistência de registros afetados.

## 8. Consulta agregada proposta — não executada

Consulta somente leitura para PostgreSQL, baseada nos nomes reais do modelo. Retorna **uma linha de contagens**, sem nomes, IDs ou caminhos individuais. As colunas sensíveis à classificação são usadas somente internamente. A classe Supabase significa referência compatível com o serviço, não confirmação da existência de um objeto remoto.

Antes de eventual execução pelo operador em contexto autorizado: confirmar banco/schema/search_path e visibilidade autorizada de toda a população pretendida. RLS ou permissões que ocultem linhas impedem usar um zero parcial como prova de ausência global. Não desabilitar RLS para isso. A proposta pressupõe encoding UTF8 e standard_conforming_strings=on, sem alterar configurações. **Nenhum SQL, inclusive esta consulta, foi executado nesta etapa.**

```sql
WITH referencias AS (
    SELECT
        "Id"::text AS usuario,
        "AvatarFileName"::text COLLATE "C" AS caminho
    FROM "Users"
), classificacao AS (
    SELECT
        CASE
            WHEN caminho IS NULL OR btrim(
                caminho,
                U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
            ) = '' THEN 'sem_avatar'
            WHEN caminho ~ $$\A[0-9a-f]{32}[.](jpg|png|webp)\Z$$
                THEN 'legado'
            WHEN usuario <> '00000000-0000-0000-0000-000000000000'
                AND char_length(caminho) <= 200
                AND caminho ~ (
                    $$\Alocal/$$ || usuario || $$/[0-9a-f]{32}[.]webp\Z$$
                ) THEN 'local'
            WHEN usuario <> '00000000-0000-0000-0000-000000000000'
                AND char_length(caminho) <= 200
                AND caminho ~ (
                    $$\A$$ || usuario || $$/[0-9a-f]{32}[.]webp\Z$$
                ) THEN 'supabase'
            ELSE 'nao_classificado'
        END AS classe
    FROM referencias
)
SELECT
    count(*) AS usuarios_total,
    count(*) FILTER (WHERE classe = 'sem_avatar') AS usuarios_sem_avatar,
    count(*) FILTER (WHERE classe <> 'sem_avatar') AS usuarios_com_avatar,
    count(*) FILTER (WHERE classe = 'supabase') AS avatares_supabase,
    count(*) FILTER (WHERE classe = 'local') AS avatares_locais,
    count(*) FILTER (WHERE classe = 'legado') AS avatares_legados,
    count(*) FILTER (WHERE classe = 'nao_classificado') AS nao_classificados
FROM classificacao;
```

As classes são exclusivas: com_avatar = supabase + locais + legados + nao_classificados; total = sem_avatar + com_avatar. Em tabela vazia, as contagens são zero. com_avatar significa referência não vazia segundo o contrato, não prova de imagem existente/renderizável.

O whitespace explícito reproduz o conjunto documentado por Char.IsWhiteSpace usado no teste de ausência. Não se aplica trim ao caminho antes dos regex: uma referência com espaço extra deve permanecer não classificada, em vez de ser transformada artificialmente em válida. As classes reconhecidas possuem somente ASCII; char_length e comprimento UTF-16 não divergem nesses caminhos. Prefixo local/ malformado, UUID alheio, caixa incorreta e URL inteira persistida ficam não classificados.

Regex usam collation C, comparação sensível a caixa e âncoras PostgreSQL \A/\Z, equivalentes aqui ao início/fim estritos usados pelo código .NET. Dollar quoting evita dependência do escape de barras dos literais comuns. Sintaxe revisada contra documentação, sem parser ou banco executado: [regex PostgreSQL](https://www.postgresql.org/docs/current/functions-matching.html), [FILTER em agregados PostgreSQL 17](https://www.postgresql.org/docs/17/sql-expressions.html#SYNTAX-AGGREGATES), [btrim](https://www.postgresql.org/docs/17/functions-string.html), [literais Unicode](https://www.postgresql.org/docs/current/sql-syntax-lexical.html), [whitespace .NET](https://learn.microsoft.com/en-us/dotnet/api/system.char.iswhitespace?view=net-10.0).

Limitações: a classificação é da referência persistida e do código desta branch. Não verifica arquivos, objetos Supabase, propriedade operacional da origem ou disponibilidade; mudanças remotas de schema/código podem exigir revisão. Solicitar somente os sete totais e a confirmação do escopo/visibilidade, não o resultado intermediário dos CTEs. Resultado não classificado positivo exige avaliação antes de afirmar ausência de dependência histórica.

## 9. Matriz de decisão

| Cenário | Condição | Recomendação |
| --- | --- | --- |
| A | Enforcement confirmado para todos os ingressos relevantes; sem locais/legados dependentes de Scheme e sem referências desconhecidas não avaliadas | Candidata à implementação mínima, mediante autorização e identidade do artefato estabelecida |
| B | Enforcement confirmado; existem locais/legados | Avaliar impacto das URLs e aceitação operacional. A decisão de redirect pode ser independente, mas não encerra o problema de URLs |
| C | Existe ingresso HTTP sem enforcement externo apropriado | Não desativar o redirect desse ingresso sem tratamento de segurança. O padrão atual sem porta também não deve ser confundido com bloqueio de HTTP |
| D | Topologia ou referências históricas desconhecidas | Manter comportamento atual e obter somente a evidência faltante |
| E | Necessidade de Scheme público comprovada; sem proxy confiável verificável | Avaliar solução específica, como origem canônica HTTPS validada, em etapa separada; não confiar em qualquer proxy |

**Situação observada: cenário D.** A integração técnica é conhecida, mas a identidade exata do deploy, a cobertura dos ingressos e as contagens de legado/local ainda não foram comprovadas. Cenários B/C/E são condicionais; não se afirma que ocorram na produção.

## 10. Recomendação técnica e escopo mínimo

Classificação: **IMPLEMENTAÇÃO CONDICIONAL**. Não PRONTO PARA IMPLEMENTAÇÃO. A viabilidade técnica foi demonstrada; faltam confirmações operacionais delimitadas. A ação imediata é manter código/configuração, sem escolher uma mudança apenas para eliminar warnings.

O candidato B06-A4 permanece adequado ao escopo: wrapper mínimo em Program.cs, `ReverseProxy:HttpsRedirectionHandledByEdge` com default false, sem IsProduction como evidência única. Não altera KnownProxies, JWT, Dockerfile, endpoints, serviços de avatar ou frontend. A opção não existe ainda no projeto original e o true não está autorizado no Render.

Não é necessário repetir a matriz aprovada para obter essas confirmações. Se a implementação futura diferir do candidato, testes apropriados deverão cobrir a diferença; o harness não substitui integração do entry point real.

## 11. Pendências concretas ao operador

Enviar somente evidências sanitizadas:

1. **Deploy ativo:** SHA do registro Live e confirmação do Dockerfile/contexto; digest final/runtime se disponíveis, distinguindo-os dos digests das bases. Se indisponíveis, dizer exatamente qual identificação o Render fornece.
2. **Ingressos:** confirmar o domínio público versionado e existência/ausência de domínio personalizado, consumidor privado, proxy adicional e caminho alternativo. Indicar responsável por HTTPS/exceção aceita em cada caminho e tipo/caminho do health efetivo.
3. **Referências de avatar:** sete contagens agregadas da proposta, caso executada por operador autorizado em momento apropriado, com confirmação de schema/escopo/visibilidade. Alternativamente, evidência agregada equivalente. Nenhum dado de usuário ou segredo é necessário.

Essas perguntas são registradas para a próxima interação; esta etapa não solicita novas credenciais, não autoriza acesso remoto e não espera resultados presumidos. Um resultado desconhecido deve permanecer pendente.

## 12. Próxima ação mínima e B06-A6

B06-A6 deverá receber as três respostas acima e aplicar a matriz, sem novo experimento por rotina. Se A for demonstrado, ou B tiver impacto explicitamente avaliado/aceito, consolidar os critérios e apresentar o candidato para autorização de implementação. Se C/D/E persistirem, registrar a pendência específica e manter a configuração até a solução correspondente.

A decisão documental não autoriza automaticamente mudança de código, cadastro de true, deploy ou consulta ao banco. Qualquer implementação será uma etapa posterior explicitamente autorizada.

## 13. Critérios de aceite para implementação futura

- Artefato ativo relacionado de forma suficiente à base analisada, sem confundir o commit documental com uma nova correção.
- Ingressos relevantes e responsabilidade/exceções de HTTPS documentados e aceitos.
- Dependências de Scheme identificadas; ausência de legado/local corroborada, ou impacto e tratamento explicitamente aceitos. Não classificado sem explicação impede declarar o assunto resolvido.
- Mudança limitada ao candidato revisado, false por padrão; sem confiança global, porta 443 isolada, alteração JWT ou efeito indireto sobre URLs.
- Evidências B06-A2/A4 preservadas; verificar a integração mínima da implementação real quando autorizada, sobretudo padrão, perfil de borda, health, headers e autenticação, sem repetir testes desnecessariamente.
- Rollback e versão/configuração anteriores registrados, sem segredos. Eliminação de warning não é critério suficiente de segurança.

## 14. Rollback

Nada foi aplicado agora. Para o candidato futuro, remover a opção ou voltar a false no próximo startup restaura o comportamento anterior; reverter somente o wrapper também restaura as linhas originais. Nenhuma reversão de imagens Docker é necessária para desfazer a opção de redirect.

A volta do warning é compatível com a configuração anterior. Antes de reativar redirect, conferir se houve introdução de porta HTTPS explícita com Scheme ainda incorreto: esse conjunto pode reintroduzir loop. Validar health, status/Location, JWT e URLs. Registrar versão/configuração anteriores e critérios de reversão; não executar reset, clean, rebase ou force. Toda alteração remota/deploy futura depende de autorização própria.

## 15. Limitações e conferência final

Fontes locais principais: B05-B4-FINAL-SUMMARY, B06-A1–A4, histórico/diff/blob Git, Domain/Models/User.cs, Data/AppDbContext.cs, migration inicial e snapshot, AvatarPaths, AvatarStorageService, SupabaseAvatarStorageService, AvatarProfileService/IAvatarReferenceStore, frontend/src/environments/environment.production.ts, RENDER.md e checklist. Documentação pública consultada em 19/11/2026, sem acesso ao serviço SeenIt, painel Render, Supabase ou Vercel.

Não houve execução SQL, build, teste funcional, novo experimento, mudança de código/configuração, atualização de dependências, commit, push, PR ou deploy. A consulta é proposta documental, não resultado operacional. Não foram expostos dados pessoais ou segredos. B06-A1/A2/A3/A4 e os relatórios B05 foram preservados; Data Protection continua separado, sem persistência de chaves implementada.

Conferência final SHA-256: 233/233 arquivos preexistentes da árvore principal e 161/161 arquivos verificados de maintenance-phase1 intactos; HEADs e status anteriores preservados. A única adição ao estado inicial do Git é este relatório. Verificação de whitespace sem erros.

**B06-A5 IMPLEMENTAÇÃO CONDICIONAL**
