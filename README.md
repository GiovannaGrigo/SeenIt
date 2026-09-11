# SeenIt

MVP de catálogo pessoal de séries com frontend Angular 21 e API C#/.NET 10.

## Funcionalidades

- Cadastro e login com senha armazenada por hash e autenticação JWT.
- Pesquisa de séries pelo catálogo público da TVmaze.
- Detalhes da série e episódios agrupados por temporada.
- Lista individual por usuário com os status `Para assistir`, `Assistindo` e `Finalizada`.
- Alteração de status e remoção da lista.

## Estrutura

```text
seenit/
├── frontend/             Angular 21, standalone components e signals
└── backend/SeenIt.Api/   Minimal API .NET 10, EF Core, SQLite e JWT
```

O frontend nunca chama a TVmaze diretamente. A API SeenIt funciona como uma camada anticorrupção: adapta o contrato externo, remove HTML das descrições e mantém o provedor substituível.

## Executar

### Backend

Requisitos: .NET SDK 10.

```bash
cd backend/SeenIt.Api
dotnet restore
dotnet run --urls "https://localhost:7042;http://localhost:5042"
```

Antes de publicar, substitua `Jwt:Key` por um segredo forte vindo de variável de ambiente ou cofre de segredos. O banco `seenit.db` é criado automaticamente no primeiro uso para facilitar o desenvolvimento local.

### Frontend

Requisitos: Node.js compatível com Angular 21.

```bash
cd frontend
npm install
npm start
```

Acesse `http://localhost:4200`, crie sua conta e pesquise uma série.

## Endpoints principais

| Método | Rota | Autenticação | Finalidade |
|---|---|---:|---|
| POST | `/api/auth/register` | Não | Criar conta |
| POST | `/api/auth/login` | Não | Obter JWT |
| GET | `/api/series/search?q=` | Não | Pesquisar séries |
| GET | `/api/series/{id}` | Não | Obter detalhes |
| GET | `/api/series/{id}/episodes` | Não | Listar episódios |
| GET | `/api/my-series` | Sim | Obter lista do usuário |
| POST | `/api/my-series` | Sim | Adicionar ou atualizar série |
| PUT | `/api/my-series/{id}/status` | Sim | Alterar status |
| DELETE | `/api/my-series/{id}` | Sim | Remover da lista |

## Evolução recomendada

Para produção, troque `EnsureCreated` por migrations do EF Core, mova a chave JWT para secrets, configure refresh tokens e rate limiting, e considere cachear respostas da TVmaze. Se houver necessidade de marcar episódios individualmente, acrescente `UserEpisodeProgress` sem misturar esse progresso com o status geral da série.
