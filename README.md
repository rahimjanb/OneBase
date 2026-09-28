# OneBase

Корпоративная платформа с AI-сотрудниками: единое пространство для данных, файлов и процессов компании, где AI-агенты отделов работают под управлением AI Director — через контролируемые инструменты, с правами доступа, аудитом и подтверждением человеком.

## Стек

| Слой | Технологии |
|---|---|
| Backend | C#, .NET 10, ASP.NET Core Web API, EF Core, JWT, RBAC |
| Данные | PostgreSQL 16 (бизнес-данные, метаданные), Redis 7 (оперативное состояние, очереди), Qdrant (векторы, RAG, память), MinIO (файлы) |
| Frontend | Next.js, React, TypeScript, Tailwind CSS |
| AI | LLM API, AI Director + агенты отделов, Tool Registry, Human Approval, AI Audit Log, RAG |
| Инфраструктура | Docker Compose, Nginx, Cloudflare Tunnel, Ubuntu |

## Основной принцип

Бизнес-логика — в PostgreSQL, файлы — в MinIO, оперативное состояние и очереди — в Redis, знания и семантическая память — в Qdrant.
**AI никогда не ходит в базу напрямую**: только через `Tool Registry` → `ToolExecutor` (Permission Layer), который проверяет грант агента, отправляет критические действия на Human Approval и пишет всё в аудит.

```
Пользователь → AI Director → HR / Sales / Production / Finance / Supply / Marketing AI
                                 │
                                 └─ вызов инструмента → ToolExecutor
                                        ├─ нет гранта → отказ + аудит
                                        ├─ критическое → ApprovalRequest → человек одобряет → выполнение
                                        └─ иначе → выполнение + аудит
```

## Структура

```
backend/
  src/
    OneBase.Domain/          сущности: пользователи, роли, отделы, папки/файлы/версии, права, аудит, AI
    OneBase.Application/     интерфейсы (IAppDbContext, IFileStorage, IVectorStore, IAuditLogger), коды разрешений
    OneBase.AI/              AI Director, агенты, Tool Registry, ToolExecutor, Human Approval, память
    OneBase.Infrastructure/  EF Core + PostgreSQL, Redis, MinIO, Qdrant, аудит, сидинг
    OneBase.Api/             Web API: JWT, RBAC-политики, контроллеры
frontend/                    Next.js + TypeScript + Tailwind
infra/nginx/                 reverse proxy: /api → backend, / → frontend
docker-compose.yml           весь стек; cloudflared — в профиле "tunnel"
```

## Быстрый старт (разработка)

Нужны: .NET 10 SDK, Node.js 20+, Docker.

```bash
cp .env.example .env                                   # и поменять пароли
docker compose up -d postgres redis qdrant minio       # инфраструктура

cd backend && dotnet run --project src/OneBase.Api     # API на http://localhost:5080
cd frontend && npm install && npm run dev              # UI на http://localhost:3000
```

При старте API применяет миграции и создаёт отделы, роль `Admin` и администратора из `Seed:AdminEmail` / `Seed:AdminPassword`.

Проверка:

```bash
curl -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@onebase.local","password":"change_me_admin"}'
```

## Продакшен

```bash
docker compose up -d --build                    # всё за Nginx на :80
docker compose --profile tunnel up -d           # + Cloudflare Tunnel (нужен CLOUDFLARE_TUNNEL_TOKEN)
```

## API

| Метод | Путь | Разрешение |
|---|---|---|
| POST | `/api/auth/login` | — |
| GET | `/api/auth/me` | авторизован |
| GET | `/api/agents` | авторизован |
| POST | `/api/agents/director/tasks` | `ai.agents.run` |
| GET | `/api/approvals?status=Pending` | `ai.approvals.decide` |
| POST | `/api/approvals/{id}/approve` · `/reject` | `ai.approvals.decide` |
| GET | `/api/audit` | `audit.read` |
| GET | `/health` | — |

## Миграции

```bash
cd backend
dotnet tool restore        # dotnet-ef из dotnet-tools.json
dotnet ef migrations add <Name> -p src/OneBase.Infrastructure -s src/OneBase.Api -o Persistence/Migrations
```

## Дальше

- [ ] Реализация `ILlmClient` для выбранного LLM-провайдера и embeddings
- [ ] `IAgentMemory`: working memory в Redis, episodic/semantic в Qdrant
- [ ] RAG: индексация документов из MinIO в Qdrant
- [ ] Files API: папки, загрузка, версии, права на папки/файлы
- [ ] Бизнес-инструменты агентов (заявки, счета, закупки…) с грантами и флагом `IsCritical`
- [ ] Event Bus, Workflow Engine, фоновые задачи, триггеры агентов
- [ ] UI: вход, файлы, чат с AI Director, очередь подтверждений, журнал аудита
