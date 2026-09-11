# SeenIt

O SeenIt é um projeto pessoal para organizar as séries que estou assistindo ou pretendo assistir.

A aplicação permite pesquisar séries utilizando os dados da TVmaze, visualizar temporadas e episódios e adicionar séries a uma lista pessoal.

## Tecnologias utilizadas

### Frontend

- Angular 21
- TypeScript
- Standalone Components
- Signals

### Backend

- C# e .NET 10
- Entity Framework Core
- SQLite
- Autenticação com JWT

## O que é possível fazer

- Criar uma conta e entrar na aplicação.
- Pesquisar séries pelo catálogo da TVmaze.
- Visualizar informações da série.
- Consultar episódios separados por temporada.
- Adicionar séries à lista pessoal.
- Definir o status como:
  - Para assistir
  - Assistindo
  - Finalizada
- Alterar o status de uma série.
- Remover uma série da lista.

## Estrutura do projeto

```text
seenit/
├── frontend/             Aplicação Angular
└── backend/SeenIt.Api/   API .NET
```

As requisições para a TVmaze são feitas pelo backend. Dessa forma, o frontend depende apenas da API do SeenIt, e o formato dos dados retornados pela TVmaze é adaptado antes de chegar à aplicação.

## Como executar

### Backend

É necessário ter o .NET SDK 10 instalado.

```bash
cd backend/SeenIt.Api
dotnet restore
dotnet run --urls "https://localhost:7042;http://localhost:5042"
```

O banco SQLite `seenit.db` será criado automaticamente na primeira execução.

A chave utilizada para gerar os tokens JWT está configurada para o ambiente de desenvolvimento. Antes de publicar a aplicação, ela deve ser substituída por uma chave segura armazenada em variável de ambiente ou em um gerenciador de segredos.

### Frontend

É necessário utilizar uma versão do Node.js compatível com o Angular 21.

```bash
cd frontend
npm install
npm start
```

Depois, acesse:

```text
http://localhost:4200
```

Crie uma conta, faça login e pesquise uma série para adicioná-la à sua lista.

## Endpoints

| Método | Rota | Requer autenticação | Descrição |
|---|---|---:|---|
| POST | `/api/auth/register` | Não | Cria uma conta |
| POST | `/api/auth/login` | Não | Realiza o login e retorna o JWT |
| GET | `/api/series/search?q=` | Não | Pesquisa séries |
| GET | `/api/series/{id}` | Não | Retorna os detalhes de uma série |
| GET | `/api/series/{id}/episodes` | Não | Retorna os episódios da série |
| GET | `/api/my-series` | Sim | Retorna a lista do usuário |
| POST | `/api/my-series` | Sim | Adiciona ou atualiza uma série na lista |
| PUT | `/api/my-series/{id}/status` | Sim | Altera o status da série |
| DELETE | `/api/my-series/{id}` | Sim | Remove a série da lista |

## Próximos passos

Algumas melhorias que ainda podem ser adicionadas ao projeto:

- Marcação individual de episódios assistidos.
- Refresh token.
- Cache das consultas realizadas na TVmaze.
- Rate limiting na API.
- Uso de migrations para controlar as alterações do banco em produção.
