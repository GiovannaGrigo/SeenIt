# SeenIt — B03-B: relatório final da auditoria histórica de produção

Data de elaboração: 2026-11-13.

**Parecer: nenhuma inconsistência histórica comprovada nas verificações Q1–Q4 informadas pelo operador. Nenhuma correção de dados recomendada neste momento.**

## 1. Contexto, objetivo e origem das evidências

Conforme o contexto fornecido pelo operador, o B03-A está publicado no Render Free e passou nos testes pós-deploy. O B03-B implementou uma ferramenta independente de auditoria histórica, validada localmente com PostgreSQL 17, para identificar duplicidades, registros órfãos, violações de domínio e sinais de identidade inconsistente, preservando estados históricos válidos.

Posteriormente, o operador executou manualmente consultas agregadas READ ONLY no SQL Editor do Supabase de produção. **Todas as evidências de produção deste relatório foram fornecidas pelo operador, sem execução independente pelo Codex.** A validação local da ferramenta não equivale à validação independente da execução manual ou do ambiente de produção.

Este documento consolida os resultados comunicados. A interpretação das categorias usa os arquivos locais `tools/SeenIt.HistoricalAudit/Sql/Q1.sql` a `Q4.sql`, o [planejamento B03-B](B03-B-HISTORICAL-INTEGRITY-PLAN.md) e a [documentação da validação local](B03-B2-READ-ONLY-AUDIT.md). Não foi fornecida aqui uma captura integral das consultas e respectivas saídas de produção, nem data/hora ou identificação de snapshot das execuções.

Na elaboração deste relatório houve somente leitura de arquivos locais e criação deste documento. Não houve acesso ao Supabase, novas consultas de banco, modificação de dados, migrations, criação de endpoints ou jobs, alteração de API ou frontend, deploy, commit, push ou alteração de `maintenance-phase1`.

## 2. Escopo concluído e critérios de interpretação

| Consulta | Escopo informado | Unidade e interpretação |
| --- | --- | --- |
| Q1 | Seis categorias de duplicidades e órfãos | Duplicidades contam grupos; órfãos contam linhas. Achados seriam evidências de inconsistência referencial ou de unicidade dos dados examinados. |
| Q2 | Treze categorias de validação de domínio e triagem | Contagens de linhas ou episódios conforme a categoria. Domínios inequívocos diferem de suspeitas de texto, URL ou data que exigiriam contexto. |
| Q3-A | Histórico sem biblioteca e classificação anime desconhecida | Controles de estados válidos; valores positivos não significam corrupção. |
| Q3-B | Distribuição de biblioteca por status e presença de histórico | Conta vínculos de biblioteca, sem calcular progresso canônico. |
| Q4 | Três categorias de identidade de episódios | Conta grupos suspeitos de associação interna divergente; eventual achado exigiria revisão e comparação externa. |

**Válido** é um estado permitido pelo modelo e pelos fluxos atuais. **Suspeito** é um sinal que exige contexto adicional e não autoriza correção automática. **Inconsistência comprovada** exige evidência suficiente de violação da regra aplicável; nenhuma foi demonstrada pelos resultados Q1–Q4 comunicados. As contagens de categorias distintas não devem ser somadas como se fossem registros únicos afetados.

## 3. Q1 — Duplicidades e registros órfãos

O operador confirmou seis categorias, todas com resultado **0**.

| Categoria | Resultado | Interpretação |
| --- | ---: | --- |
| `H01_episodio_grupos` | 0 | Nenhum grupo duplicado por usuário e ID externo de episódio. |
| `H01_biblioteca_grupos` | 0 | Nenhum grupo duplicado por usuário e ID externo de série. |
| `H01_sentimento_grupos` | 0 | Nenhum grupo duplicado por registro de episódio assistido e valor de sentimento. |
| `H02_episodio_sem_usuario` | 0 | Nenhum registro de episódio assistido sem usuário existente. |
| `H02_biblioteca_sem_usuario` | 0 | Nenhum vínculo de biblioteca sem usuário existente. |
| `H02_sentimento_sem_episodio` | 0 | Nenhum sentimento sem registro de episódio assistido existente. |

Esses resultados não certificam a presença ou validade das constraints e índices em produção; essa garantia depende da conferência estrutural Q0 integral. Referências a usuários existentes também não provam autoria correta.

## 4. Q2 — Validação de domínio

O operador confirmou treze categorias, todas com resultado **0**.

| Categoria | Resultado | Condição procurada e interpretação |
| --- | ---: | --- |
| `H03_biblioteca_id` | 0 | ID externo de série não positivo na biblioteca; violação do domínio atual. |
| `H03_status` | 0 | Status fora de 1, 2 e 3; violação do domínio atual. |
| `H03_episodio_ids_posicao` | 0 | ID externo de série/episódio, temporada ou número não positivo; candidato a revisão de identidade e domínio. |
| `H03_personagem_negativo` | 0 | ID de personagem negativo; zero é ausência válida de escolha. |
| `H04_sentimento_enum` | 0 | Sentimento fora de 1 a 5; valor não suportado pelo domínio atual. |
| `H04_episodios_mais_cinco` | 0 | Episódio assistido com mais de cinco sentimentos. |
| `H07_zero_com_nome` | 0 | Personagem zero acompanhado de nome não vazio; incoerência entre ausência de escolha e texto. |
| `H07_positivo_nome_branco` | 0 | Personagem positivo com nome branco na triagem SQL. Não verifica pertença ao elenco. |
| `H12_serie_nome_branco` | 0 | Nome de série branco na triagem SQL. |
| `H12_episodio_nome_branco` | 0 | Nome de episódio branco na triagem SQL. |
| `H12_poster_triagem` | 0 | Poster não nulo fora do padrão SQL de HTTPS/hosts TVMaze permitidos; seria sinal para revisão de URL. |
| `H12_data_assistido` | 0 | Data assistida não finita ou futura em relação ao momento da consulta. |
| `H12_datas_biblioteca` | 0 | Datas não finitas, futuras ou atualização anterior à inclusão na biblioteca. |

Não foram reportadas violações ou suspeitas nesses predicados. As triagens SQL de texto e URL não equivalem à validação completa Unicode/URI da aplicação. Datas seriam sinais contextuais e não justificariam normalização automática.

## 5. Q3-A — Histórico válido e os oito episódios sem biblioteca

| Categoria | Resultado informado | Interpretação |
| --- | ---: | --- |
| `H10_historico_sem_biblioteca_valido` | 8 | Oito registros de episódios assistidos sem vínculo de biblioteca para o mesmo usuário e série; estado válido. |
| `H14_anime_desconhecido_valido` | 0 | Nenhum vínculo com `IsAnime` nulo; o valor nulo também seria válido se encontrado. |

Os oito registros **não são órfãos referenciais**: o modelo não exige vínculo entre episódio assistido e biblioteca. Excluir uma série da biblioteca preserva o histórico, e marcar episódios sem vínculo também é permitido. Essas são explicações possíveis pelos fluxos existentes, sem evidência para atribuir qualquer uma delas aos registros observados.

A contagem representa oito linhas de histórico, não necessariamente oito séries, usuários ou IDs externos distintos. Não há base para recriar vínculos, excluir episódios, mudar status ou inferir intenção do usuário. Q1 não reportou episódios sem usuário.

## 6. Q3-B — Distribuição dos 25 vínculos de biblioteca

| Status | Significado | Presença de histórico | Vínculos informados |
| --- | --- | --- | ---: |
| 1 | InList | Com histórico | 1 |
| 1 | InList | Sem histórico | 2 |
| 2 | Watching | Com histórico | 4 |
| 3 | Finished | Com histórico | 18 |
| **Total** | | | **25** |

A soma informada é 1 + 2 + 4 + 18 = **25 vínculos**: três em InList, quatro em Watching e dezoito em Finished; 23 com histórico e dois sem histórico. O resumo recebido não discrimina a quantidade de episódios de cada vínculo.

InList com histórico é permitido, pois o status pode ser definido manualmente preservando episódios anteriores. InList sem histórico é compatível com uma série adicionada à lista. Watching com histórico é compatível com acompanhamento, mas não comprova progresso parcial diante do catálogo externo. Finished com histórico é compatível com conclusão declarada, mas não comprova cobertura de todos os episódios atuais do TVMaze: o catálogo pode mudar.

Não existe igualdade obrigatória global entre status e quantidade local assistida. Os oito registros sem biblioteca não integram a distribuição de vínculos e não devem ser somados aos 25 como se fossem a mesma unidade.

## 7. Q4 — Identidade de episódios

O operador confirmou três categorias, todas com resultado **0**.

| Categoria | Resultado | Interpretação |
| --- | ---: | --- |
| `H05_episodios_com_varias_series` | 0 | Nenhum ID externo de episódio associado internamente a mais de uma série. |
| `H05_episodios_com_varias_posicoes` | 0 | Nenhum ID externo de episódio associado internamente a mais de uma combinação de temporada e número. |
| `H05_mesma_posicao_varios_ids` | 0 | Nenhuma posição com múltiplos IDs externos para o mesmo usuário e série. |

Não foram reportados grupos suspeitos nesses critérios. Q4 verifica coerência interna: uma associação uniformemente incorreta pode passar sem achados. O resultado não comprova correspondência canônica com o TVMaze nem autoria dos registros.

## 8. Consultas não executadas e limitações

As seguintes verificações **não foram executadas em produção no escopo concluído**; ausência de execução não significa resultado zero:

| Verificação | Cobertura pendente |
| --- | --- |
| Q0 integral | Conferência completa do contrato estrutural: tabelas, colunas, tipos, nulabilidade, constraints, índices e demais condições previstas pela ferramenta. Não se afirma conformidade estrutural integral. |
| Q5 | Evidência individual limitada para investigação de candidatos; não houve extração individual informada neste escopo. |
| Q6 | Comparação com catálogo externo TVMaze para existência e identidade canônica de séries/episódios e respectivas posições. |
| Q7 | Comparações de progresso/status, elenco principal e convidados, elegibilidade de personagens/votos e demais verificações externas previstas no planejamento. |

Q6/Q7 possuem validação sintética local documentada, que não constitui comparação externa de produção. **Q0 integral e comparação externa TVMaze permanecem fora do escopo concluído.**

Limitações adicionais:

- Os resultados são agregados fornecidos pelo operador. O Codex não verificou independentemente SQL executado, permissões, transações READ ONLY, completude das saídas ou ambiente de execução.
- Não há evidência de snapshot global comum entre Q1–Q4; execuções separadas podem observar momentos distintos. O relatório não garante o estado após as consultas.
- A fotografia dos dados não comprova atomicidade passada, autoria, intenção histórica, origem do status ou ausência de falhas antigas. Referências válidas podem encobrir dono ou contexto incorretos.
- Não foram informados totais globais de usuários, episódios assistidos ou sentimentos; os 25 vínculos e os oito registros sem biblioteca não permitem inferi-los.
- Não houve auditoria completa de segurança, autorização, RLS, infraestrutura ou desempenho, nem reconstrução de catálogo histórico versionado ou trilha de mutações.
- A ausência de achados nos predicados executados não comprova integridade absoluta.

## 9. Parecer de reconciliação e recomendações futuras

**Nenhuma inconsistência histórica comprovada nas verificações Q1–Q4 informadas pelo operador. Nenhuma correção de dados recomendada neste momento.** Os oito registros sem biblioteca são válidos, e a distribuição dos 25 vínculos não demonstra defeito de status. Não há fundamento nestes resultados para deduplicar, excluir, reassociar, reclassificar ou recalcular dados em massa.

Como continuidade eventual, em escopo separado e autorizado:

1. Preservar evidência agregada com versão das consultas, data/hora, ambiente e cobertura por lote, sem credenciais ou dados pessoais desnecessários.
2. Completar Q0 integral antes de declarar conformidade estrutural de produção.
3. Planejar comparação READ ONLY com TVMaze, registrando completude e momento do catálogo; indisponibilidade ou resposta incompleta devem permanecer inconclusivas.
4. Investigar candidatos concretos antes de qualquer reconciliação. Usar Q5 somente se houver necessidade justificada; preservar valores originais e escolhas históricas em eventual plano de correção.

O B03-B fica documentado com ferramenta independente validada localmente e resultados agregados de produção Q1–Q4 fornecidos pelo operador. As verificações pendentes não são declaradas concluídas. Este parecer não autoriza reconciliação nem atribui autoria ou intenção histórica aos registros.
