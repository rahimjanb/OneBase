# Интеграция 1С:Бухгалтерия → OneBase Finance — аудит (этап 1)

Дата: 17.11.2026. Ветка `macbook-pro`. Цель — понять, что в OneBase уже есть и на что опирать интеграцию 1С, чтобы не
создать дублирующих модулей.

## 1. Что уже реализовано

| Область | Что есть | Где |
|---|---|---|
| Отдел «Финансы» | отдел `finance`, роль «Финансы», право `finance.read`, раздел пользователя «Финансы», AI-сотрудник «Finance AI» | `DbSeeder.cs:23`, `SystemRoles.cs:94`, `Permissions.cs:26`, `UserSections.cs:36`, `AgentDefaults.cs:95` |
| Финансовые данные | только оплаты точек из Linko, P&L дилеров и акт сверки из Google Sheets, DSO | `linko.Payments`, `sales.PnlValues`, `sales.DealerLedger`, `Sales/Settlements` |
| Экран финансов | «Аналитик → Финансы» (`/analytik/finance`): оплаты Linko и закупки; страницы `/finance` нет | `app/(app)/analytik/finance` |
| Подключения к внешним системам | таблица `public."Integrations"` (код, отдел, адрес, зашифрованный секрет, `SettingsJson`, результат проверки, `DataSourceUrl` против смешивания серверов) | `IntegrationConnection.cs`, `VerifixSettingsStore.cs` |
| Шифрование секретов | ASP.NET Data Protection, ключи в БД, назначения в `ProtectionPurposes` | `ProtectionPurposes.cs`, `RecoveryCheckService.cs` |
| Синхронизации | Linko (продажи), Verifix (HR), Google Sheets; у каждой свой координатор | `LinkoSyncCoordinator.cs`, `HrSyncCoordinator.cs`, `DataSourceService.cs` |
| Защита от двойного запуска | Verifix: строка `Running` в журнале + уникальный частичный индекс, пульс, закрытие зависших | `HrSyncCoordinator.cs:165`, `HrModel.cs:89` |
| Журнал синхронизаций | `hr."SyncRuns"` (с экраном журнала), `sales."LinkoSyncRuns"` | `HrEntities.cs:231`, `Linko.cs:217` |
| Повторы | свои циклы повторов с нарастающей паузой и `Retry-After` (Polly в проекте нет) | `LinkoClient.SendAsync`, `VerifixClient.SendAsync` |
| Ошибочные записи | `DataSourceConflict` (по отпечатку), очередь заданий подбора с `Failed` | `DataSources.cs:116`, `RecruitmentJobs.cs` |
| Права | `[HasPermission]`, права в коде (`Permissions.All`), роли в коде, разделы пользователя | `PermissionAuthorization.cs`, `SystemRoles.cs` |
| Аудит | `IAuditLogger.LogAsync(actorType, actorId, action, entity…)`, действия `integration.<код>.<глагол>` | `AuditLogger.cs`, `IntegrationsController.cs` |
| Журнал ошибок | `SystemLog` с источниками и `LogScrubber` (секреты вырезаются) | `SystemLogging.cs` |
| Режим «копия для проверки» | `Recovery:Isolated`: внешние службы не запускаются, HTTP-клиенты блокируются | `RecoveryIsolation.cs` |
| AI-инструменты | `DataTool` (метаданные `_meta`: фильтры, источник, период, полнота), права и уровень инструмента, источники знаний, обоснование чисел (Grounding) | `DataTool.cs`, `ToolExecutor.cs`, `KnowledgeSources.cs` |
| Свежесть данных для AI | `DataReadiness` / `DataKeeper` (Linko, Verifix) | `DataReadiness.cs`, `AnalytikSourceSync.cs` |
| Миграции | EF Core, применяются при старте API (`Database:MigrateOnStartup`), серии по модулям, свои схемы PostgreSQL | `Program.cs:86`, `Migrations/` |
| Docker | `docker-compose.yml` — он же production (`ASPNETCORE_ENVIRONMENT=Production`); сервисы `postgres`, `redis`, `qdrant`, `minio`, `api` (порт 8080 внутри сети), `web`, `nginx`, `cloudflared` (профиль `tunnel`) | `docker-compose.yml`, `deploy/update.sh` |
| Резервные копии | `deploy/backup.sh`, `onebase-backup.sh`, `restore-db.sh`, аварийное восстановление `deploy/dr` | `deploy/` |

## 2. Чего нет

- Бухгалтерских данных: плана счетов, проводок, остатков и оборотов, документов 1С, договоров с контрагентами,
  **курсов валют** (сейчас валютная выручка Linko считается нулём — «курса нет»), себестоимости, кредиторки, движения денег.
- Любого кода 1С. Единственное упоминание — текст «1С не подключена» для AI (`CapabilityRegistry.cs:68`).
- Входа «служба → API» (машинного токена). Есть только JWT пользователей и `X-Recovery-Token` для проверки копии.
- Отдельных служб (контейнеров) кроме API: все синхронизации живут внутри API.
- Разделения данных по компаниям: OneBase однокомпанийный, `TenantId` нет. Разграничение — права, отделы и область продаж.
  Для 1С это значит: изоляция — по **организациям 1С** внутри одной базы плюс права OneBase.
- Отдельного `docker-compose.prod.yml`: production работает из `docker-compose.yml`.

## 3. Что переиспользуем

| Что | Как |
|---|---|
| `public."Integrations"` | строка `onec`, отдел `finance`: несекретные настройки (пауза, интервал, размер пакета, доступ AI, принятая база 1С) в `SettingsJson`; хэш ключа службы; `DataSourceUrl` — какая база 1С загружена |
| Схема журнала Verifix | `onec."SyncRuns"` со статусом `Running` + уникальный частичный индекс + пульс + закрытие зависших |
| Права | `finance.read` — смотреть финансы и спрашивать AI; `integrations.manage` — управлять подключением 1С |
| Аудит | `integration.onec.*` в `AuditLogs` |
| Экран интеграций | `/settings/integrations/finance/onec` по образцу Verifix (`VerifixSettings.tsx`) |
| AI | наследники `DataTool`, источник знаний `finance.accounting`, AI-сотрудник «Финансы» и аналитик финансов получают инструменты через существующий механизм выдачи |
| Свежесть | новый источник `onec` в `DataReadiness`, время синхронизации — в ключ кэша ответов консультанта |
| Миграции | серия `Finance1_…`, схема `onec` |

## 4. Решения по архитектуре

1. **Служба `OneBase.Finance.Integration.Worker` — отдельный процесс и отдельный Docker-сервис** (как требует задание).
   Она читает 1С, проверяет и приводит данные к формату OneBase и отправляет пакеты во **внутренний API OneBase**.
   В PostgreSQL служба не пишет.
2. **Внутренний API** `/api/internal/onec/*` в существующем API: вход по ключу службы (`X-OneBase-Integration-Token`,
   в базе — только SHA-256 хэш), закрыт в nginx (404 снаружи) и отклоняет запросы, пришедшие через nginx.
3. **Учётные данные 1С живут только у службы** (переменные окружения `.env` → `OneC__*`). API их не хранит и не видит;
   страница настроек показывает адрес, порт, HTTPS, сертификат и результат авторизации со слов службы — без пароля.
4. **Состояние синхронизации — в БД OneBase** (запуски, курсоры, пакеты, ошибки, историческая загрузка, команды
   администратора). Служба без состояния: перезапуск продолжает с последнего сохранённого курсора.
5. **Курсор сдвигается в той же транзакции, что и запись пакета** — пакет либо записан вместе с курсором, либо нет ничего.
   Повтор пакета (тот же ключ пакета) не применяется второй раз.
6. **Схема `onec`** — факты 1С отдельно от данных OneBase. Управленческие планы, бюджеты и прогнозы OneBase хранятся
   в своих модулях и в эти таблицы не пишут; правки фактов 1С возможны только импортом из 1С.
7. **Уникальность**: `SourceSystem + SourceOrganizationId + SourceEntityType + SourceEntityId` у каждой загруженной записи.
8. **Мультивалютность**: исходная сумма и валюта, курс и кратность, дата и источник курса, сумма в валюте учёта —
   всё как в 1С, без пересчёта.
9. **AI**: только чтение, только у пользователей с `finance.read`, и только если администратор включил «AI может читать
   данные 1С» (по умолчанию выключено — данные уходят во внешнюю модель). Персональные субконто (сотрудники, физлица)
   AI не получает.
10. **Тестовый источник** `OneCMockDataProvider` — правдоподобные данные 1С (две организации, план счетов НСБУ №21,
    UZS/USD/EUR/RUB, реализация, поступления, касса, банк, производство). Демо-данные помечены и не смешиваются с данными
    настоящей базы: смена базы 1С останавливает загрузку до решения администратора.

## 5. Какие файлы изменятся

| Файл | Изменение |
|---|---|
| `backend/OneBase.slnx` | новые проекты |
| `backend/src/OneBase.Application/Abstractions/IAppDbContext.cs`, `Persistence/OneBaseDbContext.cs` | таблицы `onec.*` |
| `backend/src/OneBase.Infrastructure/DependencyInjection.cs` | сервисы 1С и финансов |
| `backend/src/OneBase.AI/DependencyInjection.cs`, `Knowledge/KnowledgeSources.cs`, `Analytik/AnalytikAgents.cs` | AI-инструменты финансов |
| `backend/src/OneBase.Application/Analytik/Readiness/DataReadiness.cs`, `Infrastructure/Analytik/AnalytikSourceSync.cs`, `Semantic/CapabilityRegistry.cs` | свежесть данных 1С, «1С не подключена» |
| `backend/src/OneBase.AI/Consultant/ConsultantAnswerCache.cs` | время синхронизации 1С в ключе кэша |
| `backend/src/OneBase.Api/Controllers/IntegrationsController.cs` | карточка «1С» в отделе «Финансы» |
| `infra/nginx/nginx.conf` | закрыть `/api/internal/` снаружи |
| `docker-compose.yml` | сервис `onec-worker` в профиле `onec` (не запускается без явного включения) |
| `frontend/src/lib/navigation.ts`, `components/shell/sections.ts`, `Sidebar.tsx`, `MobileNav.tsx`, `RouteSkeleton.tsx` | раздел «Финансы» |
| `frontend/src/app/(app)/settings/integrations/[dept]/[code]/page.tsx` | страница «1С» |
| `frontend/src/app/(app)/departments/page.tsx`, `lib/departments-view.ts`, `lib/navigation.ts` | карточка отдела «Финансы» ведёт в раздел |
| `frontend/src/i18n/uz/index.ts` | узбекский словарь раздела |

## 6. Какие компоненты будут созданы

- `backend/src/OneBase.Finance.Integration.Contracts` — формат пакетов и протокол «служба ↔ API» (без зависимостей).
- `backend/src/OneBase.Finance.Integration.Worker` — служба: `IOneCDataProvider`, `OneCMockDataProvider`,
  `OneCHttpDataProvider`, проверка и нормализация, синхронизация (история, изменения, повторы, курсоры), клиент
  внутреннего API, диагностика сети/TLS/авторизации, `Dockerfile`.
- `backend/src/OneBase.Domain/OneC`, `Persistence/OneCModel.cs`, миграция `Finance1_OneCImport` (только новые таблицы).
- `backend/src/OneBase.Application/Finance` — запись пакетов, управление синхронизацией, финансовые отчёты.
- Контроллеры: внутренний (`/api/internal/onec`), настройки (`/api/integrations/onec`), финансы (`/api/finance`).
- AI-инструменты `finance_*`.
- Frontend: раздел `/finance` (обзор, доходы и расходы, деньги, расчёты, счета, операции) и
  «Настройки → Интеграции → Финансы → 1С» (состояние, журнал, ошибки, история).
- Тесты: `backend/tests/OneBase.Finance.Tests` (служба) и тесты записи на PostgreSQL в `OneBase.AI.Tests`.
- Документы: `docs/integrations/ONEC_API_SPECIFICATION.md`, `docs/integrations/ONEC_VENKON_QUESTIONS.md`,
  `docs/finance.md` (работа раздела и запуск службы).

## 7. Риски

| Риск | Что делаем |
|---|---|
| Сеть до 1С не работает (ping и 443 — timeout), веб-сервер 1С не опубликован | интеграция готовится на тестовом источнике; служба показывает пошаговую диагностику (TCP → TLS → авторизация → API); сетевые настройки, брандмауэр и IIS не трогаем |
| Формат API VENKON отличится от нашего проекта | 1С-форма данных изолирована в адаптере `OneCHttpDataProvider`; остальная система работает с нормализованными записями |
| В 1С нет «даты изменения» объектов | рекомендуем план обмена (`/changes`); запасной вариант описан в спецификации |
| Объём истории (сотни тысяч проводок) | пакеты по 500, загрузка по месяцам с продолжением после сбоя и сверкой количества и сумм |
| Параллельные сессии правят общие файлы (`DependencyInjection.cs`, `KnowledgeSources.cs`, словарь `uz/index.ts`, снимок модели EF) | правки точечные; миграция только с новыми таблицами; при коммите — только свои участки |
| Демо-данные попадут в рабочую базу вперемешку с настоящими | смена `infobaseId` останавливает загрузку; «Очистить данные 1С» — отдельное действие с подтверждением |
| Бухгалтерские данные уходят во внешнюю AI-модель | доступ AI выключен по умолчанию, включает администратор; только агрегаты и документы, без персональных субконто |
| Обновление production | служба в профиле `onec`: `deploy/update.sh` её не собирает и не запускает; миграция только добавляет таблицы; nginx пересоздаётся при обновлении (как обычно при изменении его конфигурации) |
