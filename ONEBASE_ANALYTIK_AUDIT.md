# OneBase Analytik — Фаза 1: полный аудит и архитектура

Дата аудита: 11.11.2026. База: локальная копия `onebase` (PostgreSQL 16.15), читалась только запросами чтения (`default_transaction_read_only`).
Код: ветка `macbook-pro` (коммит 680f4f1 + незакоммиченная работа параллельной сессии — см. §13).

Документ отвечает на вопросы фазы 1: что есть в OneBase (модули, таблицы, связи, источники, метрики, AI), чего нет, и как на этом построить
раздел «Аналитик» (OneBase Intelligence), не дублируя существующее.

---

## 1. Система в цифрах

| Слой | Что есть |
|---|---|
| Backend | .NET 10, ASP.NET Core, EF Core (Npgsql); 5 проектов (Domain, Application, Infrastructure, AI, Api), ~151 тыс. строк C# без миграций |
| API | 50 контроллеров, 705 HTTP-методов; авторизация — JWT + `[HasPermission]`, права перечитываются из БД (кэш 1 мин) |
| БД | PostgreSQL 16: 14 схем, 231 таблица, 0 представлений, 242 внешних ключа, 100 миграций EF |
| Хранилища | Redis (кэш продаж, кэш ответов AI), MinIO (файлы), Qdrant (векторы базы знаний) |
| Frontend | Next.js 16.3.6, React 19.3, Tailwind 4.3, TypeScript 5.9; BFF `/bff/*` с httpOnly-cookie; без библиотеки графиков (SVG/div вручную) |
| Языки UI | ru (исходный), uz (латиница), uz-Cyrl (генерируется из uz); английского интерфейса нет |
| Тесты | xUnit: OneBase.AI.Tests (~120 файлов, БД-тесты на отдельной временной базе `ONEBASE_TEST_POSTGRES`), Sales.Tests, Files.Tests; фронтенд — `node --test src/lib/*.test.mjs` |

## 2. Карта модулей

| Модуль (схема) | Что хранит | Ключевые сервисы и расчёты | Источник истины |
|---|---|---|---|
| Продажи (`linko`, `sales`) | Зеркало Linko: заказы (350 тыс.), строки (1,22 млн), возвраты, визиты (702 тыс.), точки (51 тыс.), товары (315), склады и остатки дилеров, первичка (перемещения), оплаты точек, прайсы, акции, планы ТП; OneBase: оргструктура (направления, регионы), планы РОП/«Завод»/первички, P&L дилеров, реестры взаиморасчётов | `SalesAnalytics*` (в памяти, по месяцу): обзор, республика, направления, регионы, агенты, планы, KPI, проблемные агенты, ассортимент, «кто не купил»; прогноз на конец месяца `SalesMath.Forecast = факт / прошедшие дни × дни месяца`; `StockService` (дни покрытия, дефицит < 15 дн., излишек > 30 дн., заказ до 15 дн.), `PrimaryService`, `SkuSalesService` (ABC), `PnlService`, `SettlementsService` (акт сверки, DSO) | Linko (факт), Google Sheets (планы, P&L, реестры дилеров), расчёт |
| Sales Base (`field`) | Полевая команда: участники, маршруты, точки маршрута, визиты, задачи, рекомендации AI | `FieldDashboardService` | OneBase |
| Снабжение и склад сырья (`procurement`, 40 таблиц) | Номенклатура (545), остатки (`StockBalances`: OnHand/Reserved), движения (знаковые), документы, партии со сроками, резервы заявок, заявки, согласования, заказы поставщикам, тендеры, приезды машин, контроль качества, поставщики (126, `LeadTimeDays`), рейтинг | `ProcurementStockService` (доступно, минимумы — расход за 30 дн.), `ProcurementAnalyticsService`, `ProcurementInsightService` (история цен, расходы, поставщики), `ProcurementArrivalService.ExpectedAsync` (ожидаемые поставки), `ProcurementReminderService` (ниже минимума) | OneBase (+ журнал склада из Google Sheets) |
| Производство (`production`) | Техкарты (рецептуры с этапами, нормами и потерями), производственные задания (план выпуска и снимок потребности), смены (выпуск, расход, брак), виды брака, импорт техкарт из Excel (318 карт, 6951 строка) | `ProductionCalc.Plan` (потребность с выходом этапов и потерями), `ProductionRecipeService.RequirementAsync` (нехватка = max(0, нужно − доступно)), `ProductionStock.AvailableAsync`, `ProductionAnalyticsService` (норма vs факт, дни запаса, рост цен), `ProductionCost` (себестоимость по партиям) | OneBase |
| HR (`hr`, `recruitment`) | Сотрудники Verifix (1313), события прихода/ухода, табели, справочники; подбор: вакансии, кандидаты, отклики, собеседования, HH | `HrService` (обзор, табель, отклонения, лидеры), сервисы подбора | Verifix, OneBase, HH |
| Задачи и календарь (`work`) | Задачи отделов, проекты, комментарии, уведомления, события календаря | `WorkTaskService` (видимость по отделам) | OneBase |
| Telegram (`telegram`) | Аккаунты (сессии MTProto), диалоги, исходящие, автоотчёты, рассылки | Отчёты, outbox с идемпотентностью | OneBase |
| AI (`ai`) | Агенты, гранты, чаты, сообщения, память, находки (`Alerts`), учёт (`Usage`), журнал, отчёты, база знаний, Data Manager, модели и сравнения | см. §7 | OneBase |
| Мотивация (`battle`, `incentive`) | Цели «битвы», программы мотивации | Карточки в Telegram | OneBase |
| Цифровой двойник (`twin`) | Объекты 3D-территории, привязки к отделам и складам | `TwinService.SnapshotAsync` | OneBase |
| Общие (`public`) | Пользователи, роли, права, отделы, файлы и версии, аудит, системный журнал, интеграции, источники данных Google Sheets | — | OneBase |

## 3. Каталог базы данных

### 3.1. Схемы

| Схема | Таблиц | Размер | Наполнение (оценка `reltuples`) |
|---|---|---|---|
| linko | 26 | 387 MB | OrderLines 1,22 млн · Visits 702 тыс. · Orders 350 тыс. · Markets 51 тыс. · OrderReturnLines 53 тыс. · Payments 39 тыс. · StockTransferLines 37 тыс. · ProductBalances 11 тыс. · KpiPlans 7,7 тыс. |
| hr | 6 | 20 MB | AttendanceEvents 27 тыс. · TimesheetDays 11 тыс. · Employees 1,3 тыс. · SyncRuns 1,6 тыс. |
| field | 13 | 13 MB | CustomerStats 51 тыс. |
| procurement | 40 | 8 MB | StockImportRows 17 тыс. · Items 545 · StockBalances 545 · StockMovements 376 · Suppliers 126 |
| production | 15 | 4 MB | RecipeImportLines 6,9 тыс. · RecipeImportRecipes 318 · Recipes 7 · Orders 2 · Shifts 1 |
| sales | 13 | 2,4 MB | StaffPlans 5,2 тыс. · StaffIndicators 1 тыс.; RegionPlans и FactoryPlans пусты в этой копии |
| ai | 25 | 1,3 MB | — |
| public | 24 | 1,6 MB | — |
| recruitment | 25 | 0,7 MB | — |
| telegram | 25 | 0,6 MB | — |
| work | 14 | 0,7 MB | — |
| battle, incentive, twin | 5 | < 0,2 MB | — |

Полный каталог (таблица → колонки, типы, ключи, связи, назначение) строит сам Аналитик (фаза 2) — он же обновляет его при изменениях схемы,
поэтому в документ не переписывается вручную.

### 3.2. Связи

Внешние ключи (242) по направлениям: procurement→procurement 69, procurement→public 23, public→public 18, production→production 17,
telegram→telegram 17, work→public 17, recruitment→recruitment 16, production→procurement 11, production→public 10, field→field 10,
work→work 7, ai→ai 6, sales→sales 3, **linko→linko 3**, остальные — по 1–2.

**Главное наблюдение:** у `linko` (самые большие данные) внешних ключей всего 3 (`OrderLines→Orders`, `OrderReturnLines→OrderReturns`,
`StockTransferLines→StockTransfers`). Остальные связи — только по имени колонки и данным:

| Колонка | Ведёт на | Как понятно |
|---|---|---|
| `Orders.MarketId`, `OrderReturns.MarketId`, `Payments.MarketId`, `Visits.MarketId`, `MarketUsers.MarketId` | `linko.Markets.Id` | имя |
| `Orders.AgentId`, `OrderReturns.AgentId`, `Markets.ResponsibleAgentId` | `linko.Users.Id` | только бизнес-правило (агент — пользователь Linko) |
| `Visits.UserId`, `Payments.UserId`, `KpiPlans.UserId`, `MarketUsers.UserId` | `linko.Users.Id` (не `public.Users`!) | та же схема |
| `OrderLines.ProductId`, `OrderReturnLines.ProductId`, `StockTransferLines.ProductId`, `ProductBalances.ProductId`, `PriceListItems.ProductId`, `PromotionProducts.ProductId`, `Reservations.ProductId` | `linko.Products.Id` | имя |
| `Products.TypeId` | `linko.ProductTypes.Id` | имя таблицы + стем |
| `ProductTypes.ParentId`, `Borders.ParentId` | та же таблица | самоссылка |
| `StockTransfers.FromStockId/ToStockId`, `ProductBalances.StockId`, `Reservations.StockId` | `linko.Stocks.Id` | суффикс |
| `Orders.BranchId`, `Markets.BranchId` | филиал Linko = `sales.Regions.LinkoBranchId` (старые филиалы сливаются, `Sales/OldBranches.cs`) | только бизнес-правило |
| `production.Recipes.LinkoProductId` | `linko.Products.Id` | имя, без FK |

Вывод для движка: связи нужно (1) читать из `pg_constraint`, (2) выводить по именам колонок и (3) **проверять на данных** (доля значений-сирот
по выборке) — и только потом использовать для соединений. Бизнес-связи, которые по имени не вывести (`AgentId → linko.Users`,
`BranchId → sales.Regions.LinkoBranchId`), задаются семантическими описаниями.

### 3.3. Чувствительные данные

Колонки, которые аналитический движок не должен отдавать никогда: `public.Users.PasswordHash`, все `Protected*` (ключи AI-провайдеров,
секреты интеграций, сессии и телефоны Telegram, токены HH, пароли файловых подключений), `*TokenHash`, `CodeHash`, `StateHash`, `AccessHash`,
`work.PushKeys.ProtectedPrivateKey`. Персональные данные (телефон, e-mail, адрес, паспорт, username Telegram) — `public.Users`, `hr.Employees`,
`field.Customers`, `field.Members`, `recruitment.*`, `telegram.*` — скрываются в общем движке (их показывают только профильные экраны по своим
правилам). Целиком закрыты: `public.DataProtectionKeys`, `UserSessions`, `FileLocks`, `FileConnections`, `FileUserLinks`, `Integrations`,
`DataSourceConnections`, `telegram.Accounts`, `LinkCodes`, переписка Telegram, `recruitment.HhConnection`, `work.PushKeys/PushSubscriptions`,
`ai.Providers`, `ai.ClaudeSwitchBackup`, личные чаты и память AI (`ai.Conversations`, `Messages`, `Memory`, `DataConversations`, `DataMessages`),
`__EFMigrationsHistory`.

## 4. Метрики, которые уже считаются

- **Продажи:** факт (кг, сумы), план (РОП / «Завод» / сумма планов ТП), выполнение, прогноз на конец месяца, АКБ, вес на точку, страйк,
  визиты с заказом и без, «по маршруту / вне / без визита», средний чек, проблемные агенты (к медиане региона), ABC SKU, «кто не купил»,
  возвраты, экспорт, акции, KPI Linko, первичка против плана, остатки дилеров (дни покрытия, дефицит, излишек, рекомендуемый заказ), аутсток.
- **Финансы:** оплаты точек (Linko), P&L дилеров (выручка, себестоимость, валовая прибыль, маржа, EBIT, ROS, EAT), акт сверки, DSO, просрочка
  дилеров. **Нет:** общих доходов и расходов компании, бюджетов, кредиторки, денежного потока, оплат поставщикам (отложены до модуля «Финансы»).
- **Снабжение:** доступно = OnHand − Reserved − просрочено − на контроле − заблокировано; ниже минимума; ожидаемые поставки и просрочки; расходы
  на закупки; история цен; рейтинг поставщика (0–100); KPI сотрудников снабжения; средний срок поставки.
- **Производство:** потребность по техкарте (выход этапов, потери), нехватка под задание, норма vs факт расхода, перерасход, дни запаса сырья,
  рост цен сырья, себестоимость по партиям. **Нет:** «сколько можно произвести из остатков», себестоимости техкарты по ценам, мощностей,
  загрузки, прогноза.
- **HR:** посещаемость за день, опоздания и ранние уходы (минуты), отсутствия, табель (часы против плана, явка, пунктуальность), лидеры.
  **Нет:** ФОТ, KPI сотрудников вне продаж, текучести с причинами.
- **Задачи:** открыто, просрочено, выполнено, на проверке — по отделам и исполнителям.

## 5. Источники данных и свежесть

| Источник | Что даёт | Как часто | Где свежесть |
|---|---|---|---|
| Linko API | заказы, возвраты, визиты, точки, товары, остатки, перемещения, оплаты, прайсы, акции, планы ТП | каждые 30 мин, сверка раз в сутки в 03:00 | `linko.SyncState` (по сущностям: LastSuccessAt, LastError) |
| Google Sheets | планы РОП/«Завод»/первички, P&L, реестры дилеров (оплаты, отгрузки ГП, расходы, нормы, остатки), журнал склада | по расписанию привязки | `public.DataSourceBindings`, `DataSourceSyncRuns` |
| Verifix | сотрудники, приходы и уходы, табели | 2 / 10 мин | `hr.SyncRuns`, `hr.SyncStates` |
| HH.uz | отклики | 15 мин | `recruitment.HhConnection` |
| OneBase | снабжение, производство, задачи, оргструктура продаж | при каждой операции | — |

Владельцы показателей уже описаны в коде: `SourceOfTruth` / `SourceOfTruthService` (Application/DataSources) — Аналитик использует их, а не
заводит свою карту.

## 6. Фоновые процессы

Планировщика (Quartz/Hangfire) нет: 20 `BackgroundService` с `PeriodicTimer` и областью DI на тик (Linko, кэш продаж, Google Sheets, журнал
склада, Sales Base, напоминания задач и снабжения, Verifix, Telegram ×5, подбор ×4, системный журнал, Web Push, проактивный анализ AI, «Мой день»).
Внешние вызовы закрыты флагом изоляции (`RecoveryIsolation.AllowOutbound`). Уведомления — `UserNotifier.Notify` (колокольчик + Web Push);
повторы гасятся «колонкой-заявкой» (`…NotifiedFor`, `ExecuteUpdate … if claimed == 1`) или уникальным ключом.

## 7. AI-слой (что переиспользуется)

| Компонент | Файл | Что делает |
|---|---|---|
| AiGateway | `OneBase.AI/Gateway/AiGateway.cs` | единая точка вызова моделей: провайдер, ключ, повтор, резервная модель, лимит параллельных вызовов, бюджет (день/месяц), учёт токенов и стоимости (`ai.Usage`), поток текста; `Ai:Safety:BlockPaidCalls` / эмулятор — защита от платных вызовов |
| AiRouter | `Orchestration/AiRouter.cs` | быстрая модель выбирает сотрудников и задачи им по списку доступных; запасной вариант — ключевые слова |
| AgentOrchestrator | `Orchestration/AgentOrchestrator.cs` | сотрудники параллельно, каждый в своей области DI; повтор по плану перепроверки |
| AgentRunner | `Agents/AgentRunner.cs` | цикл инструментов (до 8 шагов), параллельные вызовы, пейджинг больших результатов, точный расчёт (`calculate`), источник истины, проверка чисел итога по данным, итог JSON (summary/findings/metrics/problems/recommendations/data_sources) |
| ToolExecutor | `Tools/ToolExecutor.cs` | Permission Layer: грант агента, открытый источник, право пользователя, Human Approval для критических, аудит каждого вызова |
| OrchestratedConsultantEngine | `Orchestration/OrchestratedConsultantEngine.cs` | вопрос → маршрутизация → сотрудники → итоговый ответ с проверкой чисел (GroundedComposer), память, отчёты Excel, перепроверка возражений (CorrectionGuard) |
| Grounding | `Grounding/*` | EvidenceSet, NumberClaims, EvidenceCheck, Calculator, CorrectionGuard, SourceOfTruthTool |
| Маршрутизация моделей | `Gateway/AiModelRouting.cs`, `AiTaskRouting.cs`, `Orchestration/AnswerComplexityRules.cs` | модель по назначению вызова и сложности (правила без вызова модели), режимы off/shadow/on |
| ModelComparisons | `Monitoring/ModelComparisons.cs` | журнал теневых сравнений и эталонных проверок моделей |
| Учёт и журнал | `Monitoring/*` | `ai.Usage` (токены 4 счётчиками, стоимость по ценам модели), `ai.AuditLogs` (вопрос, сотрудники, инструменты, источники, проверка чисел, замер этапов) |
| Кэш ответов | `Consultant/ConsultantAnswerCache.cs` | тот же первый вопрос при неизменных данных — без модели |
| Проактивный анализ | `Proactive/ProactiveAnalyzer.cs` | правила без модели раз в час → `ai.Alerts` (ключ-дедупликация, закрытие исчезнувших): падение продаж, план, первичка, проблемные агенты, дефицит/неликвид |
| Отчёты | `Reports/*`, `Infrastructure/Reports/*` | `AiReportContent` → Excel (ClosedXML) и PDF (QuestPDF) |
| База знаний | `Knowledge/KnowledgeService.cs` | MinIO + полнотекстовый поиск PostgreSQL + Qdrant; txt/md/csv/xlsx/docx (PDF не читается) |
| Язык ответа | `Consultant/AnswerLanguage.cs` | русский, узбекский латиницей и кириллицей, «другой» (английский) — ответ на языке вопроса |

58 инструментов данных (все только читают): продажи (get_sales, get_sales_period, get_sales_days, get_visits, get_sku_sales, get_export_sales,
find_products, get_customers, get_lapsed_customers, get_store, get_orders, get_agent, get_agent_visits, get_kpi, get_promo, get_prices,
get_primary_shipments, get_stock_reload, get_outstock, get_inventory, get_problem_agents …), финансы (get_payments, get_dealer_pnl, get_akt,
get_dso), снабжение (get_procurement_overview, get_warehouse_stock, get_low_stock_items, get_purchase_requests, get_purchase_orders,
get_overdue_deliveries, get_purchase_price_history, get_supplier_history, compare_supplier_offers, get_procurement_expenses,
get_procurement_employee_kpi, get_pending_approvals, get_suppliers), HR (get_hr_day, get_hr_employees, get_hr_employee, get_hr_deviations,
get_hr_timesheet, get_hr_leaders), поле и задачи (get_field_*, get_department_tasks, get_work_projects), прочее (get_ai_alerts, get_telegram,
search_knowledge). **Инструментов производства нет.** Произвольного запроса к базе нет — каждый инструмент знает свои таблицы.

## 8. Права и доступ к строкам

- Права — коды в `Permissions.cs` (36 кодов), роли — `SystemRoles.cs` (в т.ч. «Директор», «Аналитик», отделы, РМ, Супервайзер, Агент),
  итоговые права = роль → разделы пользователя → галочки прав (`UserSections.Effective`, `db.EffectivePermissionsAsync`). Новое право в
  существующие базы добавляется через `SystemRoles.Upgrades`.
- Доступ к строкам продаж — `SalesAccess.ScopeAsync` (новая работа параллельной сессии): РМ — регионы своих направлений, супервайзер — свои
  регионы, агент — свой `linko.Users.Id`; нет строки — вся организация. **AI сейчас не умеет фильтровать строки:** `UserPermissions.LimitSalesAsync`
  снимает `sales.read`/`finance.read` у любого пользователя без доступа ко всей организации.
- Снабжение и производство — `ProcurementContext` (отдел, руководитель, директор); задачи — `WorkContext`; HR — `HrContext`; Sales Base — `FieldScope`.
- **Мультиарендности нет** (ни одного `TenantId`): одна компания на развёртывание.

## 9. Документы

Файлы — MinIO (`public.Files`, версии, права на папки), база знаний AI (`ai.KnowledgeDocuments/Chunks`, полнотекст + Qdrant), документы
снабжения (`procurement.Documents`), резюме кандидатов (с распознаванием фото и сканов), материалы Telegram-агентов. PDF база знаний не читает.

## 10. Отчёты и панели

Главная по ролям, «Отделы», страницы продаж (вторичка, первичка, агенты, ассортимент, остатки, P&L, акт сверки, DSO, KPI, экспорт, акции),
обзор и отчёты снабжения, технолог и отчёты производства, HR-обзор и табели, Sales Base, AI Dashboard («Фокус дня»), «Использование» AI,
Telegram-автоотчёты (картинки и PDF), Excel/PDF-выгрузки. Раздел `/reports` — заглушка «скоро».

## 11. Чего в данных нет (честные ограничения Аналитика)

| Вопрос | Почему сейчас не посчитать | Что Аналитик скажет и сделает |
|---|---|---|
| Чистая прибыль, расходы компании, кассовый разрыв | нет модуля «Финансы» (доходы/расходы/бюджеты/кредиторка) | только выручка, оплаты точек, P&L дилеров, DSO; прибыль компании — «нет данных» |
| Спрос на сырьё от плана продаж | нет связи «категория/SKU продаж → техкарта» (`Recipe.LinkoProductId` пуст у всех) | потребность — из открытых производственных заданий и истории расхода; недостающую связь — в «Качество данных» |
| Срок поставки и цена сырья у поставщика | `Suppliers.LeadTimeDays` пуст у всех 126; цены — только в 2 приходах | срок — из факта приездов, иначе «не задан»; цена — последняя известная или «нет цены» (не выдумывается) |
| История расхода сырья | 7 выдач в производство; начальные остатки — одной инвентаризацией | прогноз расхода с пометкой «мало истории» и широким диапазоном |
| Загрузка мощностей, брак по линиям | этап «линии и мощности» технолога не построен | «не учитывается системой» |
| Себестоимость продукции | цены партий есть только у новых приходов | по техкарте × последние цены, с долей позиций без цены |

## 12. Решения по архитектуре (что переиспользуем, что добавляем)

**Переиспользуем без дублирования:** AiGateway (все вызовы моделей, бюджет, учёт), AiRouter, AgentOrchestrator, AgentRunner, ToolExecutor
(права и аудит), GroundedComposer и проверку чисел, маршрутизацию моделей, кэш и журнал AI, 58 инструментов данных, доменные сервисы
(продажи, остатки, снабжение, производство, HR), `SourceOfTruthService`, `ProactiveAnalyzer` + `ai.Alerts` (его находки — входные данные для
рисков Аналитика), Excel/PDF-писатели, `UserNotifier`, схему фоновых задач.

**Добавляем (новое, в отдельных файлах):**

```
OneBase.Domain/Analytik/                 сущности схемы analytik
OneBase.Application/Analytik/
  Catalog/   модель каталога, классификатор (модуль, назначение, чувствительность), вывод связей, политика доступа к таблицам
  Query/     типизированный план запроса, проверка, построитель SQL (только SELECT, параметры), путь соединений по графу
  Forecast/  прогнозирование (скользящее, тренд, сезонность Холта—Винтерса, бэктест, сценарии, интервалы)
  Supply/    расчёт потребности (MRP) по горизонтам 7/14/30/60/90
  Risk/      поиск отклонений (робастный z-score, сравнение периодов), правила рисков, дедупликация
  Quality/   проверки качества данных по каталогу и свежести источников
  Reports/   управленческие отчёты → AiReportContent (Excel/PDF)
  Services   панель, разделы аналитики, история
OneBase.Infrastructure/Analytik/
  PostgresSchemaReader   pg_catalog → каталог (схемы, таблицы, колонки, PK/FK, индексы, оценка строк)
  AnalytikSqlRunner      READ ONLY транзакция, statement_timeout, lock_timeout, лимит строк; отдельная строка подключения
  AnalytikWorker         фоновые циклы 30 мин / час / день / неделя / месяц
OneBase.AI/Analytik/                     инструменты движка + команда агентов «Аналитик»
OneBase.Api/Controllers/AnalytikController.cs
frontend: /analytik (+ разделы), components/analytik, lib/analytik.ts, i18n/uz/analytik.ts
```

**Схема `analytik` (одна аддитивная миграция, существующие таблицы не меняются, кроме новой колонки `ai.Conversations.Kind` со значением
по умолчанию):** `CatalogTables`, `CatalogColumns`, `CatalogRelations`, `CatalogRuns`, `Annotations` (ручные описания — переживают
переобнаружение), `Queries` (журнал каждого аналитического запроса), `Insights` (риски и отклонения с причиной/гипотезами, масштабом,
последствиями, доказательствами и отметкой уведомления), `Forecasts` (снимки прогнозов для оценки точности), `Reports`, `JobRuns`
(расписание и идемпотентность), `MetricSnapshots` (дневные агрегаты для трендов и аномалий).

**Безопасность запроса (порядок проверок):** право `analytik.use` → таблица есть в каталоге и не закрыта → право модуля таблицы у пользователя
(итоговые права) → колонки не чувствительные → соединения только по проверенным связям → для пользователей с ограниченной областью продаж —
предикаты области (регион / агент) или отказ, если таблицу нельзя ограничить → SQL строит код (идентификаторы из каталога в кавычках, значения
— параметрами, только SELECT) → выполнение в `BEGIN READ ONLY` с `statement_timeout` и лимитом строк → запись в `analytik.Queries`.
Модель **не пишет SQL**: она отдаёт типизированный план, SQL собирает и проверяет backend. Отдельная роль PostgreSQL только на чтение —
скрипт `deploy/analytik-readonly.sql` и строка `ConnectionStrings:AnalytikReadOnly` (без неё — основное подключение, но всё равно READ ONLY
транзакция; `.env` не меняется).

**Агенты:** команда «Аналитик» отдельно от команды консультанта: Chief Analyst (ведущий) + Sales, Production, Warehouse, Supply, Finance, HR,
BI Analyst. Тот же движок (`OrchestratedConsultantEngine`) получает параметр команды; у консультанта поведение не меняется. Аналитики получают
существующие инструменты отделов + инструменты движка (каталог, связи, запрос, профиль колонки, «всё о записи», прогноз, MRP, риски, качество,
производство). Модели — через существующую маршрутизацию: маршрутизатор — быстрая модель, сотрудники — модель сотрудников, итог — по сложности;
математика — в коде.

**Фоновые задачи без лишних денег:** 30 мин — обновление каталога (по отпечатку схемы), свежесть источников, дневные агрегаты; час — риски и
отклонения (математика); день — снимок прогнозов и точность, управленческий отчёт (цифры кодом); неделя, месяц — отчёты. Вызов модели в фоне
выключен по умолчанию (`Analytik:Narratives:Enabled = false`).

## 13. Ограничения разработки

- В дереве одновременно работает другая сессия (права продаж по областям, фото техкарт, подбор, оболочка навигации): изменены `Permissions.cs`,
  `SystemRoles.cs`, `UserRights.cs`, `IAppDbContext.cs`, `OneBaseDbContext.cs`, снимок миграций, `ModuleNav`/`MobileNav`/`Topbar`, `globals.css`.
  Аналитик пишется в новых файлах; общие файлы правятся точечно. Миграция Аналитика идёт после их `SalesUserScopes` и `RecipePhotos`.
- Сборка — `-c Release`: `bin/Debug` занят API пользователя на :5081. Живая проверка — второй API на временной базе (`onebase_e2e_*`) с
  выключенными внешними вызовами и `Ai__Safety__BlockPaidCalls=true`.
- Платные вызовы моделей при разработке не выполняются.

## 14. План реализации

| Фаза | Что | Критерий готовности |
|---|---|---|
| 2 | Data Intelligence Engine: чтение pg_catalog, классификатор модулей и чувствительности, вывод и проверка связей, каталог в `analytik.*`, отпечаток схемы и обнаружение новых таблиц/колонок, ручные описания | тест: каталог видит все 231 таблицу и 242 FK; новая таблица во временной базе появляется после обновления; связь `Orders.MarketId → Markets` выведена и подтверждена данными |
| 3 | Analytical Query Engine: типизированный план, путь соединений, построитель SQL, политика доступа, области продаж, READ ONLY выполнение, журнал | тесты: инъекции и запись невозможны, чувствительные колонки и закрытые таблицы — отказ, область региона добавляет предикат, тайм-аут срабатывает |
| 4 | Раздел «Аналитик»: права, API, пункт меню, панель, чат, «Карта данных», история | раздел открывается по праву, чат отвечает потоком через команду аналитиков |
| 5 | Аналитика отделов: продажи (существующие сервисы), склад сырья, снабжение, производство, финансы, персонал | цифры совпадают со страницами-источниками |
| 6 | Снабжение: потребность по формуле с горизонтами 7–90 дней | тест: резервы и подтверждённые поставки не вычитаются дважды; без цены и срока — «нет данных», а не число |
| 7 | Прогнозы и отклонения | бэктест на синтетике с известным трендом и сезонностью; аномалия находится, повтор не создаёт новую находку |
| 8 | Агенты: Chief + 7 специалистов, инструменты движка | маршрутизация по команде; консультант работает как раньше (его тесты зелёные) |
| 9 | Автономная работа 24/7 и уведомления | задачи идемпотентны (одна дневная за день), уведомление не повторяется |
| 10 | Отчёты (Excel/PDF), производительность, безопасность, финальная проверка | полный прогон тестов, живая проверка сценариев из ТЗ на временной базе |
