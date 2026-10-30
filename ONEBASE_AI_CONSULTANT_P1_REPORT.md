# AI-консультант OneBase: P1 — полнота данных

Дата: 30.10.2026. Основание: [ONEBASE_AI_CONSULTANT_AUDIT.md](ONEBASE_AI_CONSULTANT_AUDIT.md), P0–P0.2 завершены и не переписывались.

Изменения сделаны в существующей архитектуре: цикл сотрудника `AgentRunner`, инструменты `DataTool`, сервисы «Продаж» и HR.

Не тронуто:
- production, модели, AI Gateway, `.env`, пароли и секреты;
- права доступа и схема БД; миграций нет;
- `ConsultantPrompts`, `AgentDefaults`, текст `AgentRunner.SystemPrompt`;
- лимит шагов `MaxSteps = 8`.

Платных вызовов не было, данные и Docker volumes не удалялись, в git ничего не отправлялось.

Что меняется в тексте, который видит модель, — раздел 9.

---

## 1. Итог

| Задача | Было | Стало |
|---|---|---|
| 1. Большие результаты | Результат длиннее 40 000 символов обрезался посередине JSON — модель получала битый JSON без конца списков | Результат остаётся валидным JSON: урезаются строки списков с конца, в `_pagination` — `total_count`, `returned_count`, `offset`, `has_more`, `next_offset`. Полный результат хранится на время анализа, следующие строки — инструмент `get_more_rows` из того же снимка, без нового запроса. Общий бюджет контекста анализа — 200 000 символов |
| 2. `Take(N)` | 42 списка резались до первых N без признака, что это выборка | 50 мест через `TakeTop`, ещё 16 — через `args.Top` и `args.Count`. Всё урезанное записано в `_meta.lists`, `_meta.complete = false` с подсказкой. Аргумент `all_rows` у каждого инструмента данных отдаёт списки целиком (дальше — страницами). Остальные `Take` разобраны в разделе 3 |
| 3. Периоды продаж | `get_visits` и `get_sales_days` — в пределах одного месяца. Несколько месяцев — вызов на каждый месяц, разницу считала модель, АКБ складывалась по месяцам | `get_sales_period`: любой отрезок до 366 дней одним вызовом — месяц, квартал, полугодие, год, неделя через границу месяца, «с 25.09 по 10.10». Разбивка по месяцам, неделям или дням, регионы и ТП. Сравнение с прошлым периодом, годом назад или любым периодом; разница и % считаются кодом. АКБ периода — точки за весь период, не сумма АКБ месяцев. `Coverage` — месяцы без данных и по какой день есть данные |
| 4. HR | Для AI — не больше 62 дней, как у страниц | Квартал (92 дня) — одним вызовом HrService. Табель, отклонения и карточка сотрудника — до года, частями по 92 дня: части без пересечений и пропусков, результаты объединяются. Страницы и API — по-прежнему 62 дня |
| 5. Шаги | Повтор вызова снова запрашивал данные; несколько месяцев — несколько вызовов | `MaxSteps = 8` не менялся. Повтор того же вызова (те же аргументы в любом порядке) не выполняет инструмент, а ссылается на прошлый результат. Период до года — один вызов. Страницы — без запроса к данным |
| 6. Метаданные | Нет | `_meta` у каждого JSON-результата инструмента данных: `tool`, `filters`, `sources` (название, период), `units`, `complete`, `lists`, `note`. Top-N помечен `complete = false` |
| 7. Тесты | — | 41 новый тест (раздел 7). Все тесты — 1211 из 1211 с PostgreSQL |

---

## 2. Архитектурные решения

### 2.1 Большие результаты: `ToolResultPager` (новый)

- **Один экземпляр на запуск сотрудника.** Создаётся в `AgentRunner.RunAsync`, хранит полные результаты в памяти под `result_id` (`r1`, `r2`, …). Между запусками ничего не хранится, в БД не пишется.
- **Fit.** Результат помещается в бюджет — уходит как есть.
  - Не помещается только из-за экранирования кириллицы (`\uXXXX`) — уходит компактной записью целиком, без страниц.
  - JSON — урезается: строки удаляются с конца списка с наибольшим *собственным* весом (длина списка минус вложенные списки). У регионов с длинными списками ТП урезаются ТП внутри регионов, а строки регионов с их итогами остаются. У длинного списка с короткими вложенными урезается сам список.
  - Итоги и суммы результата не трогаются: сервисы считают их по всем строкам.
  - Не JSON — сокращается по границе строки с пометкой, сколько показано.
- **`_pagination`:** `result_id`, `truncated`, `lists_truncated`, до 12 урезанных списков (путь вида `Agents` или `Regions[0].Agents`, `total_count`, `returned_count`, `offset`, `has_more`, `next_offset`) и подсказка `next`. Запас под метаданные — 2400 символов.
- **`get_more_rows(result_id, path, offset, limit)`** — строки подряд с `offset` из того же снимка, не длиннее бюджета, хотя бы одна строка.
  - Без повторов и пропусков: страница начинается с `next_offset` прошлой.
  - Неизвестный `result_id` — ошибка `not_found`, неверный путь — `invalid_arguments`. Пустые данные в этих случаях не отдаются.
- **Бюджет контекста.** `ContextBudgetChars = 200 000` на анализ. Каждый следующий результат получает остаток, но не больше 40 000 (как раньше) и не меньше 8 000 символов (`AgentRunner.Budget`).
- **Повтор вызова.** Ключ — инструмент и канонический JSON аргументов: порядок свойств и пробелы по краям строк не важны. Повтор отдаёт `{repeat: true, result_id, message}` и не вызывает инструмент.
- **Grounding и salvage не теряют строк.** Проверка чисел (`EvidenceCheck`) и итог после сбоя получают полный результат инструмента до урезания.
- **Кэш ответов** (`ConsultantAnswerCache`) не кэширует вызовы `get_more_rows`.

### 2.2 Списки и `_meta`: `DataTool`, `ToolArgs`

- `ToolArgs.Top(source, n, name)` и расширение `TakeTop(args, n, name)` отдают первые `Cap(n)` строк и записывают, сколько строк всего.
- `ToolArgs.Count(name, total, returned)` — для списков, урезанных запросом к БД, когда общее число известно (`CountAsync` или уже посчитанный `Found`).
- `all_rows` добавляется в схему каждого инструмента данных (`AgentRunner.WithAllRows`): с ним `Cap(n)` — все строки, как у отчёта Excel. Дальше большой результат урезает и пагинирует `ToolResultPager`.
- `DataTool.WithMeta` добавляет `_meta` в JSON-объект результата:
  - `tool`, `filters` — аргументы как есть;
  - `sources` — название и период каждого источника;
  - `units`, `complete`, `lists[]` (`name`, `total_count`, `returned_count`, `truncated`, `has_more`);
  - `note` — при неполной выборке: «Показана выборка… Это не весь список: для всех строк — `all_rows = true`, для сумм и количества — итоги результата».
- Ошибки и не-JSON результаты `WithMeta` не трогает.

### 2.3 Периоды: `PeriodRanges` (новый)

- **`DateRange(From, To)`** — обе границы входят, даты — по Ташкенту.
- **`Parse`:**
  - `2026-09` — месяц; `2026-Q3` / `2026-К3` — квартал;
  - `2026-H1` / `2026-П1` — полугодие; `2026` — год;
  - `2026-09-25..2026-10-10` (также `—`, `–`, `-`, `/`, пробел) — отрезок; перепутанные границы меняются местами;
  - нераспознанное — `null`: период не угадывается.
- **`ByMonth`, `ByWeek`, `Chunks(range, maxDays)`.** `ByWeek` — недели ISO с понедельника по воскресенье, обрезанные отрезком; неделя через границу месяца — одна часть.
- **`EnsureContiguous(parts, whole)`** — части идут подряд и покрывают отрезок ровно. Пересечение, пропуск или неполное покрытие — исключение. Вызывается движком продаж, разбиением HR и слиянием табелей.
- **`Previous`:** для целых месяцев — те же месяцы раньше (Q3 → Q2, март → февраль с 28 днями), иначе — отрезок той же длины сразу перед периодом.
- **`YearAgo`:** год назад; 29 февраля → 28-е.
- **`MaxDays = 366`.**

### 2.4 Продажи за период: `SalesPeriodEngine` и `get_sales_period` (новые)

- Считают из тех же расчётов, что страницы «Продаж»: `SalesAnalytics.RouteSplit` каждого месяца. Правила те же:
  - факт — заказы минус возвраты по дате приёмки;
  - без «Завода»;
  - те же ТП, регионы и направления.
- **Как считается период:**
  - отрезок режется по месяцам, части проверяются `EnsureContiguous`;
  - каждая часть обрезается по `DataThrough` своего месяца;
  - месяц, которого нет в загруженных, и дни после последней загрузки попадают в `Missing`, `Complete = false`;
  - заказы, выручка, кг и возвраты частей складываются.
- **АКБ не складывается.** Новый `SalesMath.MarketKg` / `SalesAnalytics.MarketKg` отдаёт чистый вес по точкам (без точек без ID и без каналов, как `ActiveMarkets`). Веса частей складываются, АКБ — точки с суммой больше нуля. Так же считаются АКБ недели через границу месяца, строки регионов и ТП. Точка, вернувшая в октябре всё купленное в сентябре, в АКБ периода не входит.
- **Рефакторинг `RouteSplit`** (`ClipToMonth`, `ScopeRegions`, `CountedAgents`, `RegionLines`) — только вынос общих частей для `MarketKg`. Результаты `RouteSplit` прежние: 433 теста «Продаж» проходят.
- **`get_sales_period`:**
  - аргументы: `period` или `from`/`to`, `compare` (`previous` / `year_ago` / период), `group` (`month` / `week` / `day`), `region`, `direction`, `agent`, `agents`;
  - выдаёт `Period`, `Scope`, `Coverage`, `Total`, `Months` с `VsPrevious…Pct`, `Weeks`, `Days`, `Regions`, `Agents`, `Compare` с `Delta` и `Note`;
  - `Weeks`, `Days`, `Regions`, `Agents` — через `args.Top`, то есть учтены в `_meta`;
  - источники — основной период и период сравнения.
- **Область** (регион, ТП) определяется по месяцу конца периода, если он загружен, иначе — по последнему загруженному месяцу.
- **Сообщение `get_visits` / `get_sales_days`** при отрезке через границу месяца теперь называет `get_sales_period`.

### 2.5 HR до года: `HrPeriods` (новый)

- **HrService.** `TimesheetAsync`, `DeviationsAsync` и `EmployeeAsync` получили необязательный параметр `maxDays`. По умолчанию — `MaxPeriodDays = 62`: контроллеры его не передают, так что предел страниц и API прежний. AI передаёт `MaxReportDays = 92` — тот же предел, что у «Отчётов» и выгрузки Excel. Больше 92 сервис не принимает (`Math.Min`).
- **`HrPeriods.Split(from, to)`:**
  - до 92 дней — одна часть;
  - до 366 — части по 92 дня (`Chunks` + `EnsureContiguous`);
  - длиннее года — ошибка «Период — не больше 366 дней (год)…»: P0 классифицирует её как «период вне доступного»;
  - без `from` — один вызов, умолчание периода остаётся у HrService.
- **Части считаются по очереди** (`EachAsync`): один `DbContext` на запрос.
- **`HrPeriods.Merge` для табелей:**
  - даты подряд — проверяется число и уникальность;
  - строка сотрудника — сумма итогов частей и буквы дней подряд;
  - в части, где сотрудника нет (ещё не принят или уже уволен), — пустые буквы, как «не работал» у сервиса;
  - число букв каждой строки сверяется с числом дней части;
  - имя, подразделение и должность — из последней части;
  - итог — сумма итогов частей;
  - синхронизация — из последней части.
- **`HrPeriods.Merge` для отклонений:**
  - склейка, порядок как у сервиса (новые дни сверху, внутри дня — по ФИО);
  - случай вне своей части — исключение.
- **Карточка сотрудника** до года:
  - дни частей склеиваются, итоги складываются;
  - у уволенного период по умолчанию кончается днём увольнения, как в сервисе.
- **Суммирование безопасно:** `HrSummary` — счётчики дней и минут, части не пересекаются.
- **Учёт списков:** `employees`, `cases`, `employeesList` и списки дня (`absentList`, `lateList`, `onLeaveList`, `unknownList`) учитываются в `_meta`.

---

## 3. Ревизия `Take(N)` в инструментах

| Класс | Где | Что сделано |
|---|---|---|
| **Выборка из полного списка — считается** (`TakeTop` — 50 мест, `Top`) | AlertsTool (alerts); CustomerTools (customers); DrillTools (visits, ordersWithoutVisit); FieldTools (agents); HrTools (byDivision ×2, workingByDivision, byEmployee, absentList, lateList, onLeaveList, unknownList); OutstockTool (byRegion, topProducts, core, chronic); PriceListTool (products); RouteTools (agents ×2); SalesExtraTools (skus, new, falling, countries, buyers, agents, topProducts, dealers в группах и общий); SalesSearch (skus, byRegion, products[].byRegion); SalesTools (worst, best, withoutPlanTopByFact, agents, topProducts, weakestProducts); StockReloadTool (rows); SupplyFinanceTools (deficit, overstock, notSelling); WorkFieldTools (byAssignee, agents, agentsWithoutRoute, refusals, skippedPoints, skipReasons, byAgent); WorkProjectsTool (projects); get_sales_period (weeks, days, regions, agents) | Лимиты прежние. В `_meta` — сколько всего строк, `complete = false`; `all_rows` — все строки |
| **Урезано запросом к БД — считается** (`Count`) | DrillTools: lastVisits, lastOrders (`CountAsync`), orders (сумма по итогам); HrTools: employees (`page.Total`), cases, employeesList; WorkFieldTools: tasks ×2 и recommendations (уже посчитанный `Found`) | Лимит запроса прежний, `total_count` — точный |
| **Оправдан, не скрывает данных** | Кандидаты в сообщениях о неоднозначности или ошибке (DrillTools: сотрудники ×10, контрагенты акта ×40; `WorkFieldText.List` с «и ещё N»); кандидаты поиска (`DrillLookup.StoresAsync`, CustomerTools и SalesSearch — подбор товаров, категорий, типов); справочник «доступные месяцы» (×12); примеры внутри строки при счётчике рядом (CustomerTools.Products ×5, SalesTools.LostNames ×5, SalesExtraTools.OutletNames); число месяцев из аргумента (SalesTools, `months.Take(count)`); «N самых крупных» по аргументу пользователя (CustomerTools, `largest`) | Без изменений |
| **Не учтено в `_meta`, оставлено** | RouteTools: ТП внутри каждого региона республики (до 80, рядом `AgentsTotal`); TelegramTools: журнал — последние 300 запусков, 30 проблем, 20 отправок; WorkFieldTools `AgentAsync`: visitList и SkippedPoints одного агента (рядом счётчики `VisitSummary`); MaxRouteViews | Списки «последние N» или с явным счётчиком в результате. Перечислены в рисках (раздел 10) |

Агрегация на стороне SQL: сервисы «Продаж» и HR считают итоги в памяти по всем строкам месяца или периода (данные месяца уже загружены `SalesDataLoader`). Модель получает итоги, а не строки для суммирования. В `get_more_rows` прямо сказано: для сумм и сравнения брать итоги, а не перебирать страницы.

---

## 4. Шаги (`MaxSteps = 8`)

Лимит не поднимался. Шаги экономятся так:
- Период до года и сравнение — один вызов `get_sales_period` вместо вызова на каждый месяц и `calculator` для разницы.
- HR до года — один вызов инструмента: части HrService вызывает сам инструмент, не модель.
- Повтор того же вызова не выполняет инструмент.
- Страницы `get_more_rows` — из сохранённого снимка, без запроса к данным. Подсказка в результате: для сумм и количества страницы не нужны.

---

## 5. Часовой пояс и границы

- Даты — по Ташкенту (UTC+5, без летнего времени). `GetSalesPeriodTool.Today(clock)`: 30.09.2026 19:30 UTC — это уже 01.10.2026. Совпадает с `FieldClock.Offset` и «Сегодня …» в сообщении пользователя (`AgentRunner.UserMessage`).
- Обе границы периода входят: 28.09–04.10 — 7 дней.
- Месяцы, данные и `DataThrough` — те же, что у страниц «Продаж» (`SalesDataLoader`, `SecondarySales.DataThrough`). Будущие дни периода — в `Missing`.

---

## 6. Производительность

| Что | Измерено / оценка |
|---|---|
| Урезание и страницы | 10 000 строк (~4,4 МБ JSON): урезание и чтение всех ~110 страниц — 228 мс. 1000 строк — 21 мс. Цикл сотрудника с 1200 строками (результат, 2 страницы, повтор, итог) — 182 мс на тестовой БД |
| Память | Полный результат хранится в памяти один раз на анализ (клон `JsonNode`) и освобождается после ответа |
| `_meta` | Один разбор и одна запись JSON на результат. Учёт списков — без дополнительных запросов, кроме трёх `CountAsync` в DrillTools (индексированные `MarketId`) |
| `get_sales_period` | Месяцы — из кэша `SalesDataLoader` (30 мин, история — 6 ч). Холодный прошлый месяц загружается, по прежним замерам, ~6 с, так что холодный год — до 12 загрузок. Каждый месяц загружается один раз за вызов, даже если месяц нужен и для периода, и для недель (тест). `MarketKg` по каждому региону и ТП считается в памяти. На тестовой БД — 690 мс на 7 вызовов |
| HR до года | До 4 последовательных вызовов HrService по 92 дня вместо одного. На тестовой БД 120 дней с независимыми проверками — 901 мс |

Числа для production — оценки: на реальных данных P1 не запускался (production не трогали).

---

## 7. Тесты

### 7.1 Двенадцать требований

| Требование | Тест |
|---|---|
| JSON больше 40 000 символов | `DataCompletenessTests.Json_over_40000_chars_stays_valid_json_with_pagination_and_untouched_totals` |
| 100, 1000, 10 000 строк | `Every_row_arrives_once_in_order_across_pages_and_sums_match(100 / 1000 / 10000)` |
| Пагинация без дублей | Тот же тест (каждый id ровно один раз и по порядку), `Nested_lists_are_paged_by_path_and_root_arrays_by_items`, `ConsultantP1DbTests.Large_result_reaches_the_model_whole_by_pages_without_duplicates_and_without_a_second_query` (настоящий цикл `AgentRunner`) |
| Правильный `total_count` | `Json_over_40000…`, `Top_n_is_marked_incomplete_with_total_count_and_all_rows_returns_everything`, `Ai_hr_tools_cover…` (`cases.total_count` = число случаев) |
| Суммы по всем страницам | `Every_row_arrives…` (сумма кг по страницам = итогу) |
| Продажи через границу месяца | `Sales_across_a_month_boundary_sum_the_parts_but_akb_counts_markets_of_the_whole_period`; `RouteToolsDbTests.Sales_period_spans_months_with_period_akb_weeks_comparison_and_coverage` (сверка с `get_sales_days` по каждой части) |
| Сравнение 2–3 месяцев | `Two_and_three_months_compare_month_by_month_and_each_month_is_loaded_once`; DB-тест: три месяца с % к прошлому, сентябрь к августу с `Delta` |
| Квартальный HR | `HrSyncDbTests.Ai_hr_tools_cover_a_quarter_in_one_call_and_longer_periods_in_parts_without_gaps_or_overlaps`: квартал — один вызов и совпадает с 46 + 46 дней напрямую; 120 дней (92 + 28) совпадают с 60 + 60 и 4 × 30 дней напрямую; буквы табеля — 120 подряд; страницы и API — по-прежнему 62 дня. `Hr_periods_up_to_a_quarter…`, `Hr_timesheets_of_parts_merge…`, `Hr_deviations_of_parts_merge…` |
| Неделя через границу месяца | `Week_across_a_month_boundary_is_one_week_with_its_own_akb`, `Span_across_a_month_boundary_splits_into_contiguous_months_and_iso_weeks`, DB-тест (неделя 28.09–04.10: АКБ 3, у частей месяцев — по 2) |
| Неполная выборка помечена | `Top_n_is_marked_incomplete…` (`complete = false`, `note` с `all_rows`); `Missing_month_and_days_after_the_data_mark_the_period_incomplete`; DB-тест: год назад без данных — `Compare.Coverage.Complete = false`, а не «ноль продаж» |
| Часовой пояс | `Today_is_taken_in_tashkent_so_late_utc_evening_is_already_the_next_month` |
| Источники после агрегации | `Top_n_is_marked_incomplete…` (`_meta.sources`, `filters`, `units`); DB-тест: источник `get_sales_period` с периодом «28.09.2026 — 04.10.2026», у сравнения — два источника |

Кроме того:
- разбор периодов: месяц, квартал, полугодие, год, отрезок, кириллица К/П, високосный год, нераспознанное — `null`;
- части без пересечений и пропусков, `Previous` / `YearAgo`, аргументы `get_sales_period`;
- `Budget`, `all_rows` в схеме, повтор вызова;
- текст без JSON, ошибки `get_more_rows`, строки регионов и ТП по периоду, АКБ.

### 7.2 Изменённые существующие тесты

- `HrSyncDbTests.Ai_hr_tools_answer…`: запрос HR за 294 дня ожидал отказ «62». По P1 такой запрос теперь допустим, поэтому проверка перенесена на период длиннее года (отказ «366»).
- `ConsultantP0DbTests`: в сценарную модель добавлен шаг по уже полученным сообщениям (следующая страница по `next_offset`), в тестовую базу — инструмент `zz_sales_rows`. Существующие сценарии не менялись.

### 7.3 Результаты

| Набор | С PostgreSQL (`ONEBASE_TEST_POSTGRES`, порт 5433) | Без PostgreSQL |
|---|---|---|
| OneBase.AI.Tests | 737 из 737 | 630 пройдено, 107 пропущено (БД) |
| OneBase.Sales.Tests | 433 из 433 | 433 из 433 |
| OneBase.Files.Tests | 41 из 41 | 41 из 41 |
| Frontend `npm test` / `tsc --noEmit` | 5 из 5 / без ошибок (P1 фронтенд не меняет) | — |

`dotnet build OneBase.slnx` — без ошибок и без предупреждений в изменённых файлах. Временные базы тестов удалены: в PostgreSQL только `onebase` и `postgres`.

**Нестабильный тест, не связанный с P1.** В одном из пяти полных прогонов упал `WorkTasksDbTests.Ai_dispatcher_proposes…` (`DoesNotContain("35", overdue.Title)`). Заголовок по правилу содержит название отдела со случайным hex-суффиксом теста — изредка в нём встречается «35». Ни тест, ни `TaskSuggestionService` в рабочем дереве не менялись. Остальные прогоны — 737 из 737.

---

## 8. Изменённые файлы (P1)

**Новые:**

| Файл | Что |
|---|---|
| `backend/src/OneBase.AI/Tools/ToolResultPager.cs` | Урезание по строкам списков, `_pagination`, `get_more_rows`, повтор вызова |
| `backend/src/OneBase.AI/Tools/Data/PeriodRanges.cs` | Разбор и разбиение периодов, проверка непрерывности, сравнительные периоды |
| `backend/src/OneBase.AI/Tools/Data/SalesPeriodEngine.cs` | Сложение месяцев, АКБ периода, недели, строки регионов и ТП, покрытие; `AnalyticsMonth` поверх `SalesAnalytics` |
| `backend/src/OneBase.AI/Tools/Data/SalesPeriodTool.cs` | Инструмент `get_sales_period` |
| `backend/src/OneBase.AI/Tools/Data/HrPeriods.cs` | Части HR по 92 дня, слияние табелей и отклонений |
| `backend/tests/OneBase.AI.Tests/DataCompletenessTests.cs` | 38 тестов без БД |
| `backend/tests/OneBase.AI.Tests/ConsultantP1DbTests.cs` | Цикл сотрудника с большим результатом; `RowsTestTool` |

**Изменённые:**

| Файл | Что |
|---|---|
| `backend/src/OneBase.AI/Agents/AgentRunner.cs` | Бюджет контекста, `ToolResultPager` вместо обрезки на 40 000, `get_more_rows`, повтор вызова, `all_rows` в схемах инструментов данных |
| `backend/src/OneBase.AI/Tools/Data/DataTool.cs` | `all_rows`, `_meta`, учёт списков (`Top`, `Count`, `TakeTop`) |
| `backend/src/OneBase.AI/Consultant/ConsultantAnswerCache.cs` | `get_more_rows` не кэшируется |
| `backend/src/OneBase.AI/DependencyInjection.cs` | Регистрация `get_sales_period` |
| `backend/src/OneBase.AI/Tools/Data/HrTools.cs` | До года частями, описания аргументов, учёт списков |
| `backend/src/OneBase.AI/Tools/Data/RouteTools.cs` | Учёт списков ТП; сообщение об отрезке через месяц называет `get_sales_period` |
| AlertsTool, CustomerTools, DrillTools, FieldTools, OutstockTool, PriceListTool, SalesExtraTools, SalesSearch, SalesTools, StockReloadTool, SupplyFinanceTools, WorkFieldTools, WorkProjectsTool | `Take(Cap(N))` → `TakeTop`; `Count` у списков из БД |
| `backend/src/OneBase.Application/Sales/Metrics/SalesMath.cs` | `MarketKg`; `ActiveMarkets` через него (результат прежний) |
| `backend/src/OneBase.Application/Sales/Metrics/SalesAnalytics.Route.cs` | `MarketKg(from, to, region, direction, agent)`; вынос общих частей `RouteSplit` |
| `backend/src/OneBase.Application/Hr/HrService.cs`, `HrService.Reports.cs` | Необязательный `maxDays` у `EmployeeAsync`, `DeviationsAsync`, `TimesheetAsync` (по умолчанию 62, не больше 92) |
| `backend/tests/OneBase.AI.Tests/ConsultantP0DbTests.cs`, `HrSyncDbTests.cs`, `RouteToolsDbTests.cs` | Раздел 7.2 и новые DB-тесты |

---

## 9. Что меняется в тексте, который видит модель

`ConsultantPrompts`, `AgentDefaults` (промпты отделов и консультанта) и текст `AgentRunner.SystemPrompt` в P1 не менялись. Модель видит новое только в описаниях инструментов и в результатах:
- **Новый инструмент `get_sales_period`.** Его `UsageHint` («продажи за несколько месяцев, квартал… — один вызов вместо вызова по каждому месяцу») автоматически попадает в список подсказок системного промпта сотрудника, как подсказки всех инструментов данных. Если это требует согласования, подсказку можно убрать одной строкой (`UsageHint => null`): описание инструмента останется.
- **Новый инструмент `get_more_rows`** — описание и схема.
- **Аргумент `all_rows`** — в схеме каждого инструмента данных.
- **Описания HR-инструментов:** «не больше 62 дней» → «квартал и до года — одним вызовом». Старый текст был бы неверен.
- **Сообщение об ошибке `get_visits` / `get_sales_days`** при отрезке через границу месяца называет `get_sales_period`.
- **Блоки `_meta` и `_pagination`** в результатах инструментов.

---

## 10. Ограничения и оставшиеся риски

1. **Системный промпт сотрудника** по-прежнему говорит «продажи за день, неделю или «с N по M» — `get_sales_days`», без упоминания `get_sales_period`. Подсказка нового инструмента это дополняет, а ошибка `get_sales_days` при отрезке через границу месяца направляет к `get_sales_period`. Но модель может потратить один шаг на неудачный вызов. Правка строки промпта — после отдельного согласования.
2. **Холодный год продаж** — до 12 загрузок месяцев (по ~6 с у холодного прошлого месяца). Тёплый кэш — быстро. Параллельной загрузки месяцев нет: `SalesDataLoader` и `DbContext` рассчитаны на последовательную работу.
3. **Визиты по маршруту** (`get_visits`) — по-прежнему в пределах месяца. `get_sales_period` даёт продажи, заказы и АКБ, но не разбивку «по маршруту / вне маршрута» за несколько месяцев.
4. **Область периода** (регионы, ТП) берётся по месяцу конца периода. ТП, уволенный или переведённый в середине года, считается в строке своего последнего региона. Итог республики от этого не зависит.
5. **HR до года** — до 4 последовательных вызовов HrService. Нагрузка на большом штате на production не измерена. Длиннее года — отказ.
6. **Результат больше бюджета даже после урезания** (огромная одна строка) — заглушка с `_pagination` и советом сузить фильтры. Строки такого результата всё равно доступны страницами.
7. **Снимок результата живёт до конца анализа.** `get_more_rows` в следующем вопросе диалога вернёт «результата нет — вызови инструмент заново». Это сделано намеренно: данные не устаревают незаметно.
8. **Не учтены в `_meta`** списки «последние N» с явными счётчиками (раздел 3, последняя строка). Модель видит их счётчики, но `complete` у результата для них не меняется.
9. **`all_rows` у очень больших списков:** до 5000 случаев или сотрудников HR и до 200 сотрудников справочника — прежние верхние пределы сервисов. Сверх них `_meta` честно покажет `truncated`.
10. **Нестабильный `WorkTasksDbTests`** (раздел 7.3) — стоит заменить случайный суффикс названия отдела в тесте на буквенный. Вне P1, не правился.

## 11. Требует отдельного согласования

- Строка системного промпта сотрудника о `get_sales_period` (риск 1).
- Оставить или убрать `UsageHint` нового инструмента (раздел 9).
- Отправка изменений P0–P1 в git: по условию задачи не выполнялась.
