# OneBase Mobile — аудит API (этап 0)

Дата: 19.11.2026. Ветка `macbook-pro`, коммит 1381137. Полный список эндпоинтов (853 в 61 контроллере, с правами) —
[API_ENDPOINTS.md](API_ENDPOINTS.md). Замеры — на локальном dev API (:5081, данные dev-базы), только GET-запросы.

## 1. Главное

- **Отдельный бэкенд для мобильного клиента не нужен.** Всё, что нужно Dashboard, входу, профилю, задачам, уведомлениям,
  консультанту, продажам, финансам, складу и производству, уже есть в `/api/*`. Ответы — JSON в camelCase, enum — строками.
- **Вход — JWT Bearer** (`POST /api/auth/login`). Токен живёт 10 лет, но привязан к сеансу `UserSession`. Сеанс закрывается на
  сервере: при выходе и при смене пароля. Права и роли сервер перечитывает из БД на каждом запросе (кэш 1 мин), поэтому новые права
  действуют без повторного входа. **Refresh-токена нет — и он не нужен:** обновлять нечего, отзыв серверный.
  Мобильный клиент хранит токен в Keychain/Keystore, на 401 закрывает сеанс, а при возврате в приложение перечитывает `/api/auth/me`.
- **Веб ходит через BFF** (`/bff/api/*` в Next.js, токен в httpOnly-cookie). Мобильный клиент ходит в `/api/*` напрямую с
  `Authorization: Bearer`: nginx отдаёт `/api/` наружу (`infra/nginx/nginx.conf`, `location /api/ → api:8080`), а `/api/internal/` закрыт.
- **Арендаторов (tenants) нет:** одна компания на установку. «Организации» есть только в Финансах — это организации 1С
  (`GET /api/finance/organizations`, параметр `organization`). Выбор организации в приложении — фильтр финансовых блоков, а не смена арендатора.
- **Realtime нет:** ни SignalR, ни WebSocket. Поток есть только у консультанта (`text/event-stream`). Веб опрашивает счётчики
  уведомлений раз в 30 секунд, пока вкладка на экране.
- **Push — только Web Push (VAPID).** Для FCM/APNs на сервере не хватает регистрации токенов устройств и отправителя. Это расширение
  для этапа 11; нужен проект Firebase и ключ службы от владельца.
- **Тяжёлый запрос — один:** `/api/sales/republic` (234 КБ, 1,4 с при первом расчёте месяца, дальше из кэша сервера). Веб-«Обзор»
  собирает недельные тренды в Next.js примерно из 20 запросов (12 отчётов HR по неделям + месяцы продаж). На телефоне так делать нельзя
  (см. п. 8).

## 2. Устройство бэкенда

| Проект | Что внутри |
|---|---|
| `OneBase.Api` | ASP.NET Core (.NET 10): контроллеры, JWT, права (`[HasPermission]`), лимиты частоты, WebDAV файлов |
| `OneBase.Application` | Сервисы разделов: Sales, Finance, Hr, Recruitment, Work (задачи, уведомления), Procurement, Production, Analytik, Agents |
| `OneBase.AI` | AI-консультант и агенты через `IAiGateway` (Claude), проверка ответа (grounding), голос |
| `OneBase.Infrastructure` | EF Core + PostgreSQL, MinIO, Redis (кэш продаж), Qdrant, Telegram, Verifix, Push (VAPID) |
| `OneBase.Domain` | Сущности |
| `OneBase.Finance.Integration.*` | Служба 1С → OneBase (отдельный контейнер) |

База одна (PostgreSQL, схемы по разделам). Миграции EF применяются при старте API.

## 3. Аутентификация и сеанс

| Что | Как устроено | Для мобильного клиента |
|---|---|---|
| Вход | `POST /api/auth/login` `{login, password}` (логин или почта; поле `email` — прежнее имя) → `{accessToken, expiresAt}`; 401 — неверно | Экран входа; `User-Agent` попадает в сеанс — передаём `OneBase-Mobile/<версия> (<ОС>)` |
| Лимит | 10 попыток в минуту с одного IP (политика `login`) | Обрабатываем 429: «Слишком много попыток, подождите минуту» |
| Токен | JWT HS256, срок 10 лет, claims `sub`, `sid` (сеанс), `login`, `name`, `role`, `perm`, `dept` | Только в `flutter_secure_storage`; в логи — никогда |
| Отзыв | `POST /api/auth/logout` закрывает сеанс; смена пароля закрывает все сеансы; отключённый пользователь → 401 | Любой 401 → сеанс закрыт → очистить токен и кэш → экран входа с причиной |
| Профиль | `GET /api/auth/me` — имя, должность, отдел, роли, **permissions**, `home` (overview / my_day / work), `salesScope`, `hrAccess`, `departments`, `canOpenSettings` | Источник прав для навигации; кешируется для запуска без сети |
| Refresh | Нет (по замыслу) | «Обновление» = перечитать `/me` при старте и возврате в приложение |
| Организация | Нет арендаторов | Фильтр организации 1С в финансовых блоках, если организаций больше одной |
| Язык | `Accept-Language: ru|uz` → тексты ошибок и подписи сервера (`UiLanguage`) | Заголовок из выбранного языка приложения |

Права — коды из `OneBase.Application.Security.Permissions`: `sales.read`, `finance.read`, `hr.read`, `work.tasks`, `ai.agents.run`,
`ai.approvals.decide`, `analytik.use`, `production.read`, `procurement.read`, `procurement.warehouse`, `quality.check`, `field.use`
и другие (полный список — в файле). Сервер проверяет каждый запрос сам, мобильный клиент лишь прячет недоступное.

## 4. Разделы: что готово

Время — при тёплом кэше сервера, dev-база.

| Раздел | Эндпоинты | Право | Готовность для мобильного | Замер |
|---|---|---|---|---|
| Профиль / сеанс | `/api/auth/me`, `/api/auth/logout`, `/api/me/workday`, `/api/me/tasks`, `/api/me/calendar`, `/api/me/documents`, `/api/me/ai-plan` | вход | ✅ готово | 45–85 мс |
| Продажи (вторичка) | `/api/sales/overview`, `/api/sales/months`, `/api/sales/republic`, `/api/sales/directions/{id}`, `/api/sales/regions/{id}`, `/api/sales/agents`, `/api/sales/stores/{id}`, `/api/sales/problems`, `/api/sales/plans`, `/api/sales/categories/{id}`, `/api/sales/products/{id}` | `sales.read` + охват (организация / РМ / СВ / ТП) | ✅ готово; `republic` — только уровень организации; тяжёлый | overview 0,1–0,3 с; republic 234 КБ, 0,4–1,4 с |
| Первичка, склад ГП, P&L продаж | `/api/sales/primary*`, `/api/sales/stock*`, `/api/sales/pnl` | `sales.read`, `finance.read` | ✅ готово (таблицы — адаптировать под экран) | — |
| Финансы (1С) | `/api/finance/overview`, `pnl`, `cash`, `settlements`, `accounts`, `entries`, `documents/{id}`, `operations`, `anomalies`, `organizations` | `finance.read` | ✅ готово; **данных 1С пока нет** (`meta.hasData=false`, `meta.note`) → пустые состояния | 30–70 мс |
| Аналитик (данные) | `/api/analytik/*` (каталог, запросы, профили, находки, симулятор) | `analytik.use` | ⚠️ рассчитано на десктоп; на телефон — находки и KPI | — |
| AI-находки | `/api/ai/alerts` — сводка (проблемы / критичные / возможности по разделам) + список | `ai.agents.run` | ✅ готово | 90 мс |
| AI-консультант | `POST /api/ai/chat` (поток SSE: `start`, `progress`, `draft`, `done`, `error`), `/api/ai/conversations[/{id}]`, `/api/ai/reports/{id}`, `/api/ai/status`, голос `/api/ai/voice/transcribe`, `/api/ai/messages/{id}/speech` | `ai.agents.run`; лимит 20 в минуту | ✅ готово (поток читается Dio `ResponseType.stream`) | — |
| Задачи | `/api/work/overview`, `/api/work/tasks` (+ создание, статус, комментарии, чек-лист, вложения multipart), `/api/work/assignees`, `/api/work/projects` | `work.tasks` | ✅ готово | 230 мс |
| Уведомления | `/api/notifications?filter&cursor&take`, `/api/notifications/counts`, `…/{id}/read` | вход | ✅ готово (курсорная пагинация) | 10–20 мс |
| Согласования | `/api/approvals`, `…/{id}/approve`, `…/reject` | `ai.approvals.decide` | ✅ готово | — |
| HR | `/api/hr/overview` (явка дня, подразделения, синхронизация Verifix), `employees`, `employees/{id}`, `attendance`, `timesheet`, `reports`, `leaders` | `hr.read` или руководитель (сервис проверяет) | ✅ готово | 190 мс |
| Подбор | `/api/hr/recruitment/*` | `hr.recruitment.*` | ⚠️ большой раздел, на телефон — позже | — |
| Производство | `/api/production/overview` (склад, контроль, отклонения норм, согласования), `orders`, `shifts`, `recipes`, `reports/{kind}` | `production.*` | ✅ готово | 140 мс |
| Склад сырья | `/api/procurement/stock`, `stock/summary`, `movements`, `warehouses`, операции `receipt / write-off / issue / transfer / inventory` | `procurement.*` | ✅ готово; операции — без оптимистичного UI | 100 мс |
| Отделы | `/api/settings/departments`, `/api/work/departments` | вход | ✅ готово | 20 мс |
| Календарь | `/api/calendar*` | вход | ✅ готово | — |
| Файлы | `/api/files/*` (папки отделов, загрузка multipart, скачивание) | `files.*` | ✅ готово | — |
| Полевая команда | `/api/field/*` (маршруты, визиты, задачи ТП) — сейчас PWA `/field` | `field.use` | ✅ готово; отдельный сценарий для ТП | — |
| Push | `/api/push/key`, `/api/push/subscriptions` — **только Web Push (VAPID)** | вход | ❌ FCM/APNs нет | — |
| Realtime | — | — | ❌ нет SignalR/WebSocket | — |

## 5. Web API → функция мобильного приложения

| Мобильная функция (этап) | API | Статус |
|---|---|---|
| Вход, выход, восстановление сеанса (2) | `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` | готово |
| Права и навигация (2) | `GET /api/auth/me` → `permissions`, `home`, `salesScope`, `hrAccess` | готово |
| Выбор организации (2, 3) | `GET /api/finance/organizations` → параметр `organization` финансовых запросов | готово (сейчас список пуст) |
| Dashboard: выручка, продано, клиенты (АКБ), выполнение плана (3) | `GET /api/sales/overview?year&month` → `kpi` | готово |
| Dashboard: доходы, расходы, чистая прибыль (3) | `GET /api/finance/overview?from&to&organization` → `pnl` (`net_revenue`, `net_profit`), `months[].expenses` | готово, данных 1С нет |
| Dashboard: динамика продаж (3) | `GET /api/sales/republic?year&month` → `calendar.totalDays`, `sameDays` | готово, тяжёлый (п. 8) |
| Dashboard: финансовая динамика (3) | `GET /api/finance/overview` → `months[]` (12 мес.: выручка, расходы, прибыль) | готово |
| Dashboard: подразделения (3) | `GET /api/hr/overview` (явка, подразделения), `GET /api/work/overview` → `departments` | готово |
| Dashboard: последние события (3) | `GET /api/notifications?take=5` | готово |
| Dashboard: активные задачи (3) | `GET /api/work/overview` → счётчики + `myUpcoming` | готово |
| Dashboard: AI Insights (3) | `GET /api/ai/alerts` | готово |
| Dashboard: период (3) | `GET /api/sales/months`; финансы — `from/to` | готово |
| Аналитика продаж (5) | `/api/sales/overview`, `republic`, `directions/{id}`, `regions/{id}`, `agents`, `plans`, `kpi` | готово |
| Финансовая аналитика (5, 8) | `/api/finance/pnl`, `cash`, `settlements`, `anomalies` | готово |
| Производственная аналитика (5, 9) | `/api/production/overview`, `/api/production/reports/{kind}` | готово |
| Аналитика отделов (5) | `/api/hr/overview`, `/api/hr/reports`, `/api/work/overview` | готово |
| AI-консультант (6) | `/api/ai/chat` (SSE), `/api/ai/conversations`, `/api/ai/reports/{id}` | готово |
| Продажи / CRM (7) | `/api/sales/stores/{id}`, `agents/{id}`, `problems`, `products/{id}`; создание клиентов и сделок в OneBase **нет** (источник — Linko) | чтение готово, запись — нет |
| Финансы (8) | `/api/finance/*` (только чтение факта 1С; платежей в OneBase нет — отложено до модуля «Финансы») | чтение готово |
| Производство / склад (9) | `/api/production/*`, `/api/procurement/stock*`, `movements` | готово |
| HR / задачи (10) | `/api/hr/*`, `/api/work/*` | готово |
| Уведомления (11) | `/api/notifications*` + **нужен** FCM | частично |
| Профиль / настройки (12) | `/api/auth/me`, `/api/me/*`, `/api/me/telegram` | готово |

## 6. Что кешировать на устройстве

| Данные | Кэш | Срок «свежести» | Почему |
|---|---|---|---|
| Профиль `/me` (имя, права) | SQLite | при каждом запуске перечитывается | Запуск без сети, навигация по правам |
| Блоки Dashboard (KPI, ряды, события, находки) | SQLite, последний успешный ответ (только нужный срез) | 5 мин; продажи — 15 мин (сервер обновляет Linko раз в несколько минут) | Офлайн-просмотр «последних данных» с временем синхронизации |
| Список месяцев продаж, организации 1С | SQLite | 1 день | Почти не меняются |
| Уведомления (первая страница) | SQLite | 1 мин | Лента без сети |
| Токен | Keychain/Keystore | до выхода | — |
| **Не кешируем** | — | — | Пароль; тексты диалогов AI и вложения (только в памяти); персональные данные сотрудников и кандидатов (HR, подбор) |

При выходе и при входе другого пользователя кэш удаляется целиком. Записи кэша привязаны к id пользователя.

## 7. Что требует realtime

| Событие | Сейчас на сервере | Решение для мобильного клиента |
|---|---|---|
| Новые уведомления (задачи, HR, AI) | Счётчики `/api/notifications/counts`; Web Push | Push через FCM (этап 11) + обновление счётчика при возврате в приложение. Без частого опроса |
| Ответ консультанта | SSE-поток | Dio stream, отмена при уходе с экрана |
| Статусы задач, согласования | Только запрос | Pull-to-refresh + push |
| Продажи | Синхронизация Linko на сервере | Обновление по открытию и pull-to-refresh |

## 8. Тяжёлые запросы и что предложить бэкенду

Замеры (dev): `/api/sales/republic` — 234 КБ, 1,4 с при первом расчёте месяца, 0,4 с из кэша; `?metric=sum` — ещё 235 КБ.
Остальные блоки Dashboard — от 2 до 13 КБ и 10–300 мс.

Решение для этапа 3 — **бэкенд не меняем**. `republic` запрашивается один раз (кг по дням), разбирается в фоновом изоляте, в кэш
кладётся только ряд по дням (≈ 30 чисел). Блок грузится отдельно и не держит остальные.

Предлагаемые минимальные расширения (обратно совместимые, новые эндпоинты; делать по решению владельца):

| # | Расширение | Зачем | Где |
|---|---|---|---|
| P1 | `POST/DELETE /api/push/devices` (токен FCM/APNs, платформа, язык) + отправка через FCM HTTP v1 рядом с Web Push | Push на телефон (этап 11) | `PushController`, `Infrastructure/Push/PushServices.cs`; нужен проект Firebase и ключ службы |
| P2 | `GET /api/sales/daily?year&month` — кг и сумма по дням + «те же дни прошлого месяца» без таблиц регионов | В 50–100 раз меньше трафика на графике продаж | `SalesController` + кэш `SalesDataLoader` (расчёт уже есть в `CachedRepublic`) |
| P3 | `GET /api/home/trends` — недельные ряды (продажи к плану, выручка, явка), которые веб сейчас считает в Next.js (`lib/dashboard-data.ts`) | Один запрос вместо ~20; один расчёт для веба и мобильного клиента | Новый сервис в `Application` |
| P4 | Охват продаж для РМ/СВ на Dashboard: `/api/sales/my` уже есть; `republic` у них закрыт (`[SalesOrganizationOnly]`) | График продаж для РМ/СВ | Использовать `/api/sales/my` (этап 5) |

## 9. Риски и ограничения

1. **Лимит входа по IP** (10 в минуту). За CGNAT мобильного оператора много людей выходят с одного IP — массовый вход в одну минуту
   может упереться в лимит. Клиент показывает 429 понятным текстом. Если станет проблемой — разделить лимит по логину.
2. **Долгий токен на устройстве.** Защита: Keychain/Keystore, серверный отзыв сеанса. Потерянный телефон: смена пароля закрывает
   все сеансы. Отдельного «закрыть сеанс устройства» в API нет (кандидат в этап 12).
3. **CRM-операции записи** (клиенты, сделки) в OneBase отсутствуют: продажи приходят из Linko. Мобильный клиент их не придумывает,
   CRM — только чтение.
4. **Финансовые платежи** в OneBase отложены до модуля «Финансы». Мобильный раздел «Финансы» — только чтение факта 1С.
5. **Данных 1С пока нет** — финансовые блоки показывают пустое состояние с причиной от сервера (`meta.note`), а не нули.
6. **iOS-сборка для App Store** требует аккаунта Apple Developer и подписи. Здесь проверяются сборка и запуск на симуляторе.
7. **Android-лицензии SDK** на этой машине приняты не все (`flutter doctor`). Принять их — решение владельца.
