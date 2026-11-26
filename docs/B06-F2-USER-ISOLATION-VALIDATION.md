# SeenIt — B06-F2 · Validação de isolamento e privacidade

25/11/2026 · America/Sao_Paulo · **B06-F2 EVIDÊNCIAS INSUFICIENTES**

## 1. Objetivo

Verificar isolamento entre duas contas sintéticas na API real e avaliar a privacidade da votação de personagens para 5 a 10 participantes. A cobertura executada sustenta separação nos fluxos testados. Ainda faltam testes essenciais para aprovar isolamento e privacidade de todo o beta.

## 2. Estado inicial do Git

`main`, `HEAD` e a referência local `origin/main` em `e73d48b3552eb71fb99bb0259681a5d9f8f293f3`. A única entrada da raiz era o relatório B06-F1 não rastreado. Foram inventariadas 12 branches locais, as referências de acompanhamento e duas worktrees. `maintenance-phase1` estava em `fbf8a7fdec341fbef65ca5fb2f9e889eaf2ec290`, com seis arquivos rastreados modificados e nove arquivos não rastreados. O inventário por SHA-256 cobriu 255 arquivos rastreados ou não ignorados na raiz, incluindo B06-F1, e 161 na manutenção. O estado remoto ao vivo ficou sem consulta.

## 3. Histórico relevante B06-F1

F03 registrou cobertura insuficiente entre duas contas. F04 registrou contagens e percentuais agregados sem limiar mínimo. F07 registrou que logout local, troca de senha e exclusão não revogam tokens assinados já emitidos. Os testes B03 de PostgreSQL são evidência histórica para concorrência e isolamento parcial. [B06-F1](B06-F1-BETA-SECURITY-READINESS.md), [B03](B03-DATA-INTEGRITY.md).

## 4. Ambiente isolado

Cópia temporária externa ao checkout, SDK .NET 10.0.301, restore com cache local, `RegistrationFactory` com SQLite em memória, configuração JWT sintética e execução do entry point real. As duas contas dirigidas tiveram GUIDs distintos e compartilharam IDs externos apenas para exercitar as consultas. O teste dirigido usou um cliente HTTP simulado que cancela a operação com erro se uma chamada externa não prevista ocorrer. O histórico `DataIntegrityChecks` usa fixture de catálogo. `AvatarChecks` usa dublês de persistência/Storage. A suíte de cadastro também usa Kestrel em loopback. O daemon Docker esteve indisponível, portanto não houve PostgreSQL descartável identificado. Durante o primeiro uso do SDK em seu diretório temporário, o CLI informou instalação automática de certificado HTTPS de desenvolvimento. O estado prévio dos certificados não foi inventariado. Não foi solicitado confiar no certificado.

## 5. Mapeamento de recursos

| Recurso | Rotas e implementação | Identidade |
| --- | --- | --- |
| Perfil, nome e e-mail | GET/PATCH `/api/profile`, `ProfileController.GetProfile/UpdateProfile`. E-mail é retornado ao titular. Mudança de e-mail não está exposta. | `CurrentUserService.UserId` e filtro pelo ID da conta |
| Senha e conta | PUT `/api/profile/password`, DELETE `/api/profile`, `ChangePassword/ExcluirConta` | JWT e senha atual. A exclusão usa ID, avatar e hash esperados |
| Avatar | PUT/DELETE `/api/profile/avatar`, `AvatarProfileService`, `AvatarPaths`, `IAvatarReferenceStore` | Caminho gerado pelo servidor, GUID do titular e referência condicional. A URL do objeto pode ser pública |
| Biblioteca, status e favoritos | GET/POST `/api/my-series`, PUT `/{externalSeriesId}/status` e `/favorite`, DELETE `/{externalSeriesId}`, `Program.cs:291–435` | Consulta combina titular da sessão e identificador externo |
| Episódios, sentimentos, personagens favoritos | GET `/api/episodios/series/{id}`, PUT `/{id}/assistido`, PUT/DELETE de temporada sob `/api/episodios/series` | `TryGetUserId` da claim e filtros por titular em `EpisodioAssistidoService`. Catálogo valida os vínculos externos |
| Estatísticas e tempo | GET `/api/profile/stats` e `/watch-time`, `GetStats` e `WatchTimeService.GetAsync` | Contagens/histórico filtrados pelo titular. Cache de duração do catálogo |
| Votação | GET `/api/episodios/{externalEpisodeId}/votacao-personagens` | JWT dá acesso. A agregação inclui todos os titulares |

DTOs pessoais revisados não delegam a identificação do titular ao frontend. [Perfil](../backend/SeenIt.Api/Controllers/ProfileController.cs), [identidade](../backend/SeenIt.Api/Services/CurrentUserService.cs), [episódios](../backend/SeenIt.Api/Services/EpisodioAssistidoService.cs).

## 6. Matriz de testes

**31 cenários ou grupos executados, 30 APROVADOS, 0 REPROVADOS conclusivos, 1 INCONCLUSIVO e 9 NÃO EXECUTADOS.** Os nove testes F2 são individuais. H01–H04 e H05 são suítes preexistentes contadas uma vez cada. A01–A16 são os 16 grupos identificados no check de avatar. P01 é um grupo de cálculo offline. O total não acumula subcasos nem tentativas repetidas.

| ID | Recurso, operação e conta | Esperado | Observado e evidência | Estado e limite |
| --- | --- | --- | --- | --- |
| F2-01 | A/B consultam perfil | 200, perfil e e-mail do titular | Cada resposta continha ID e e-mail próprios | APROVADO · SQLite |
| F2-02 | A muda nome, B consulta o próprio | Nome de A muda, B mantém o seu | PATCH aceito e GETs separados | APROVADO · SQLite |
| F2-03 | A troca senha | 204 e somente hash de A muda | Verificação direta dos dois hashes no banco descartável | APROVADO · Ciclo do bearer anterior fora do teste |
| F2-04 | A/B consultam biblioteca com o mesmo ID externo | Listas e filtros por titular | A e B receberam entradas e estados distintos. Filtro sem correspondência retornou lista vazia | APROVADO · Séries semeadas |
| F2-05 | A favorita série e A/B leem estatísticas | Favorito e contagem só em A | Respostas de biblioteca e stats divergiram conforme esperado | APROVADO · Cobre favorito e contagem |
| F2-06 | A/B consultam o mesmo ID externo de episódio | Cada conta vê sua marcação | Nomes sintéticos distintos retornaram apenas aos respectivos titulares | APROVADO · Leitura, linhas semeadas |
| F2-07 | A remove série compartilhada por ID externo | 204, B preservado e histórico de A permanece | GETs confirmaram lista vazia em A, série em B e episódio em A | APROVADO · Sequencial |
| F2-08 | Cliente sem bearer ou com token malformado consulta perfil | 401 para ambos | 401 nos dois casos | APROVADO · Sem outros casos criptográficos |
| F2-09 | A faz login com senha nova e exclui conta. B consulta biblioteca | 204, persistência de B intacta, conta e episódios de A removidos | Consultas EF e HTTP confirmaram conta/episódios de B e a biblioteca. Conta e episódios de A ausentes | APROVADO · Conta de teste sem avatar |
| H01 | Catálogo e tempo assistido, fixtures | Cache e falha controlada | `CatalogResilienceChecks` emitiu PASS | APROVADO · Sem comparação A/B para watch-time |
| H02 | Série, episódio, sentimentos, temporada e votação, A e linha de segunda conta | Integridade e votos canônicos | `DataIntegrityChecks` emitiu PASS. Inclui atualização de sentimentos, lote e contagem 2 no agregado | APROVADO · Segunda conta sem sessão no cenário de votos |
| H03 | Cadastro com contas sintéticas | Validação e efeitos em banco | `RegistrationIntegrationChecks` passou 73 subcasos HTTP | APROVADO · Um grupo na matriz |
| H04 | Respostas HTTP e limites, identidades sintéticas | Orçamento, isolamento de janelas e erros | `RateLimitingChecks` emitiu PASS | APROVADO · Escopo da suíte |
| H05 | Concorrência de login e regressões relacionadas, fixtures | Suíte consistente | Primeira execução falhou em `Two logins entered verification`. Repetição isolada passou | INCONCLUSIVO · Resultado instável, sem prova de falha de propriedade |
| A01–A04 | Avatar, formatos, validações, settings e contrato HTTP, fixtures | Aceitar formatos previstos e aplicar limites e caminhos próprios | Quatro grupos PASS do `AvatarChecks` | APROVADO · Serviço/dublês |
| A05–A08 | Avatar, erro de Storage, caminho, ordem de substituição e compensação | Limpar com segurança e preservar referência | Quatro grupos PASS, inclusive rejeição de caminho de terceiro | APROVADO · Sem verificação HTTP com duas contas |
| A09–A12 | Avatar, commit ambíguo, falha de limpeza, concorrência e remoção | Preservar referência do vencedor e sinalizar pendências | Quatro grupos PASS. Objeto anterior pode permanecer após falha de limpeza | APROVADO · Dublês |
| A13–A16 | Avatar, exclusão de conta, cancelamento, legado e contrato | Ordem correta, limpeza e compatibilidade | Quatro grupos PASS | APROVADO · Sem bucket real |
| P01 | Privacidade, cálculo com 1, 2, 5 e 10 votantes sintéticos | Quantificar efeito de um voto | 100%, 50%, 20% e 10% para um voto, respectivamente | APROVADO como cálculo · Privacidade reprovada na avaliação de desenho |
| N01 | A/B adicionam e alteram status da mesma série via HTTP | Separação após todas as escritas | F2 semeou as linhas e verificou filtros e remoção | NÃO EXECUTADO · POST/PUT de status A/B |
| N02 | Escritas A/B em episódios, sentimentos, personagens e temporadas | Operações independentes | H02 cobriu efeitos do titular, F2 cobriu leitura separada | NÃO EXECUTADO · Escritas concorrentes/alternadas entre contas |
| N03 | Upload/substituição/exclusão HTTP de avatar entre A/B | Referência e objeto separados | A01–A16 cobrem serviços e falhas | NÃO EXECUTADO · Matriz HTTP completa |
| N04 | Watch-time com históricos diferentes de A/B | Tempo individual | H01 cobriu falhas do catálogo, filtro confirmado por leitura estática | NÃO EXECUTADO · Comparação HTTP A/B |
| N05 | Expiração, assinatura, issuer e audience | Rejeição consistente | Configuração revisada e F2-08 só cobriu ausente/malformado | NÃO EXECUTADO · Matriz JWT adicional |
| N06 | Bearer anterior após troca ou exclusão | Comportamento conforme política de sessão | F2 renovou a autenticação antes da exclusão. Política atual revisada no código | NÃO EXECUTADO · Sessão anterior |
| N07 | PostgreSQL e concorrência de status/temporada/exclusão | Locks e efeitos independentes | Sem ambiente descartável PostgreSQL nesta rodada | NÃO EXECUTADO · B03 histórico |
| N08 | Votação HTTP longitudinal com 2 e 5–10 contas sintéticas | Verificar garantia de privacidade | H02 revalidou agregação. P01 modelou tamanho de grupo offline | NÃO EXECUTADO · Sem ensaio longitudinal HTTP |
| N09 | Exclusão A/B com sentimentos e mídia vinculados | Cascata de sentimentos e limpeza de avatar preservando B | F2-09 teve episódios sem avatar. A13 usou dublês | NÃO EXECUTADO · Cascata/Storage end-to-end |

Evidências locais de origem. [Factory](../backend/SeenIt.RegisterIntegrationChecks/RegistrationFactory.cs), [integridade HTTP](../backend/SeenIt.RegisterIntegrationChecks/DataIntegrityChecks.cs), [avatares](../backend/SeenIt.AvatarChecks/Program.cs), [regressões](../backend/SeenIt.RegisterIntegrationChecks/RateLimitingRegressionChecks.cs).

## 7. Resultados de perfil

F2-01–03 e F2-09 demonstraram consultas de titular, atualização de nome, troca de senha e exclusão com efeitos isolados no banco descartável. Não há rota que aceite ID de terceiro para perfil, e-mail ou senha.

## 8. Resultados de biblioteca

F2-04/05/07 comprovaram filtro, favorito, remoção e preservação de B com ID externo idêntico. Remover uma série da biblioteca preservou o histórico de episódios de A. POST e mudanças de status de ambas as contas permanecem pendentes.

## 9. Resultados de episódios

F2-06 confirmou filtros de leitura. H02 cobriu escrita e leitura de sentimentos, personagem canônico, marcação/desmarcação de temporada e falhas do catálogo no harness original. Falta repetir as escritas e os lotes sob duas sessões para o mesmo ID externo.

## 10. Resultados de avatares

Os 16 grupos passaram. A rejeição de caminho de terceiro e a concorrência de substituição foram verificadas no nível do serviço. Os dublês confirmam possibilidade de objeto órfão após falha de limpeza ou resultado de persistência desconhecido. A exclusão do avatar exige reconciliação operacional de logs, referências e objetos. A leitura pública do bucket é uma decisão de produto. A política remota e o fluxo HTTP de duas contas ficaram sem verificação.

## 11. Resultados JWT

Os bearers válidos A/B levaram aos respectivos recursos. O perfil recusou ausência e credencial malformada. `Program.cs:115–130` valida assinatura, expiração, issuer e audience. `ChangePassword` troca apenas o hash e o middleware JWT não consulta uma versão de sessão ou existência da conta. Portanto, por inspeção, um bearer anterior pode continuar aceito durante a validade. Após a exclusão, perfil e avatar consultam a existência da conta, o histórico pessoal fica sujeito às cascatas e a votação exige apenas autenticação JWT. Assinatura alterada, expiração, issuer/audience divergentes e ciclo do bearer antigo seguem sem ensaio. [Program](../backend/SeenIt.Api/Program.cs#L115), [perfil](../backend/SeenIt.Api/Controllers/ProfileController.cs#L86).

## 12. Concorrência

F2 usou operações sequenciais. A11 verificou conflito de referência do avatar com dublês. H05 variou entre duas execuções. O contador de verificações do harness depende de incremento comum, hipótese de instabilidade a investigar junto com o agendamento do teste. O lock transacional de `SeriesMutation.BeginAsync` depende do PostgreSQL e não foi revalidado nesta etapa. [SeriesMutation](../backend/SeenIt.Api/Services/SeriesMutation.cs), [factory](../backend/SeenIt.RegisterIntegrationChecks/RegistrationFactory.cs).

## 13. Privacidade da votação

`ObterVotacaoPersonagens` está sob `[Authorize]`, sem exigência de que a conta tenha assistido ao episódio. `ObterVotacaoAsync` busca episódio/série e elenco, agrupa registros de todos os titulares e retorna personagem, votos e percentual com duas casas. O DTO não contém identificação dos votantes. O total de votantes pode ser menor que o tamanho do beta. Um voto isolado equivale a 100% quando é o único e ainda representa 20% entre cinco votantes e 10% entre dez. H02 observou dois votos agregados e P01 quantificou essa granularidade offline. Comparações ao longo do tempo podem revelar variações individuais, especialmente quando há contexto social sobre quem assistiu. Esse risco é condicional. A exposição das contagens é confirmada. [Controller](../backend/SeenIt.Api/Controllers/EpisodiosAssistidosController.cs#L107), [serviço](../backend/SeenIt.Api/Services/EpisodioAssistidoService.cs).

## 14. Achados confirmados

- Nenhum acesso ou mutação indevida de dado individual foi observado nos cenários executados.
- A votação agrega dados entre contas como comportamento do produto. Contagem exata, percentual e baixa cardinalidade são incompatíveis com uma expectativa de privacidade robusta neste grupo.
- Por inspeção, sessão JWT é independente da troca de senha e da exclusão. Respostas dependem de cada rota e do estado persistido.
- A limpeza de mídia após falha é eventual e pode deixar órfãos, conforme dublês de Storage.

## 15. Lacunas e testes não executados

N01–N09 descrevem as lacunas. As primeiras tentativas do harness F2 identificaram problemas da própria fixture temporária, corrigidos antes da rodada contabilizada. Não foi possível verificar paridade PostgreSQL, Storage, ingressos de produção, erros de todos os endpoints ou o conjunto completo de sessões e escritas. A oscilação H05 mantém a concorrência de login inconclusiva.

## 16. Decisão sobre isolamento

**Evidências insuficientes para aprovação completa.** Os nove testes dirigidos confirmaram separação em um subconjunto material. Os bloqueios F03 e F04 continuam abertos, por cobertura funcional incompleta e risco de privacidade da votação, respectivamente. O parecer B06-F1 para o beta permanece.

## 17. Recomendação de privacidade para beta

| Opção | Avaliação |
| --- | --- |
| A. Desabilitar agregado durante o beta | Mais simples. Retira da API e da UI a divulgação de dados coletivos. Preserva o favorito individual |
| B. Limiar mínimo | Reduz contagens esparsas. Um limiar isolado mantém risco por variações históricas e pode inviabilizar a função com poucos participantes |
| C. Omitir contagens | Percentuais finos, categorias ou mudanças ainda informam sobre o conjunto. Exige desenho e testes adicionais |
| D. Mostrar somente a própria escolha | Retém a utilidade individual sem agregado. É possível combinar com A |

Adotar A durante o beta, mantendo a própria escolha mediante D. O controle de divulgação precisa ser aplicado no servidor. A UI deve refletir a indisponibilidade.

## 18. Plano mínimo de correções

| Local e método | Problema e menor alteração futura | Teste, regressão e rollback |
| --- | --- | --- |
| `EpisodiosAssistidosController.ObterVotacaoPersonagens` e consumidor Angular | Divulgação esparsa. Desabilitar agregado por configuração nos ambientes beta e esconder a consulta na UI | Rota não devolve agregado quando desabilitada. Verificar favorito, sentimentos e atualização de episódio. Rollback por configuração após decisão de privacidade |
| `Program.cs` configuração JWT, `TokenService.Create` e `ProfileController.ChangePassword/ExcluirConta` | Sessões antigas. Definir política e, se exigida revogação imediata, validar estado/versionamento da sessão em rotas autenticadas | Matriz de expirado, validade, senha e exclusão, contas antigas e desempenho. Risco de logout global ou dependência adicional do banco. Rollback coordenado de configuração e versão, com invalidação explícita se necessário |
| `AvatarProfileService.CleanupAsync` e operação de Storage | Limpeza eventual. Definir reconciliação auditável dos eventos pendentes e legados antes de automatizar | Dublês com commit ambíguo e objeto ainda referenciado. Risco de apagar mídia vigente. Suspender reconciliador e restaurar a partir de cópia verificada se aplicável |
| Harness de integração e `RateLimitingRegressionChecks.LoginBurstAsync` | Fechar N01–N09 aplicáveis e estabilizar a medição concorrente antes de concluir sobre o serviço | Rodadas repetíveis com contadores seguros e PostgreSQL identificado. Alterações restritas a testes em etapa futura. Reverter o patch de teste se necessário |

## 19. Preservação do ambiente

Raiz em `main` no SHA inicial, nenhuma alteração rastreada. B06-F1 preservado e este relatório é a única nova entrada. Referências, worktrees e SHA-256 dos arquivos do inventário Git conferidos. A manutenção preservou 161 hashes, inclusive a comparação com o snapshot histórico, seis arquivos modificados e nove não rastreados. Docker sem daemon disponível para comparar inventário. Nenhuma produção, commit, push, PR, merge, deploy ou migration. As fixtures adicionais e builds ficaram no diretório temporário. A confirmação final de hashes cobre o inventário Git e não inclui uma nova comparação de todos os arquivos ignorados.

## 20. Conclusão

Fechar a verificação HTTP de escritas concorrentes e recursos pessoais com PostgreSQL descartável e Storage simulado. Endereçar a votação agregada antes de convidar participantes. O gate B06-F1 permanece aberto.

**B06-F2 EVIDÊNCIAS INSUFICIENTES**
