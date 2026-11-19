// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Russian (`ru`).
class AppLocalizationsRu extends AppLocalizations {
  AppLocalizationsRu([String locale = 'ru']) : super(locale);

  @override
  String get appTitle => 'OneBase';

  @override
  String get navHome => 'Главная';

  @override
  String get navAnalytics => 'Аналитика';

  @override
  String get navTasks => 'Задачи';

  @override
  String get navAi => 'Консультант';

  @override
  String get navProfile => 'Профиль';

  @override
  String get actionRetry => 'Повторить';

  @override
  String get actionRefresh => 'Обновить';

  @override
  String get actionAll => 'Все';

  @override
  String get actionClose => 'Закрыть';

  @override
  String get actionCancel => 'Отмена';

  @override
  String get stateLoading => 'Загрузка…';

  @override
  String get stateEmpty => 'Нет данных';

  @override
  String get offlineBanner => 'Нет сети — показаны сохранённые данные';

  @override
  String get syncedJustNow => 'Обновлено только что';

  @override
  String syncedMinutes(int n) {
    return 'Обновлено $n мин назад';
  }

  @override
  String syncedHours(int n) {
    return 'Обновлено $n ч назад';
  }

  @override
  String syncedAt(String date) {
    return 'Обновлено $date';
  }

  @override
  String get comingSoonTitle => 'Раздел в разработке';

  @override
  String comingSoonMessage(int stage) {
    return 'Появится на этапе $stage плана мобильного приложения.';
  }

  @override
  String get errorNetwork => 'Нет соединения с сервером. Проверьте интернет.';

  @override
  String get errorTimeout => 'Сервер долго не отвечает. Попробуйте ещё раз.';

  @override
  String get errorUnauthorized => 'Сеанс закрыт. Войдите снова.';

  @override
  String get errorForbidden => 'Нет доступа к этому разделу.';

  @override
  String get errorNotFound => 'Не найдено.';

  @override
  String get errorRateLimited => 'Слишком много попыток. Подождите минуту.';

  @override
  String get errorServer => 'Ошибка на сервере. Попробуйте позже.';

  @override
  String get errorInvalidResponse =>
      'Сервер ответил неожиданно. Обновите приложение.';

  @override
  String get errorUnknown => 'Что-то пошло не так.';

  @override
  String get errorNotConfigured =>
      'Адрес сервера не задан при сборке приложения.';

  @override
  String get profileAppearance => 'Оформление';

  @override
  String get themeSystem => 'Как в системе';

  @override
  String get themeLight => 'Светлая';

  @override
  String get themeDark => 'Тёмная';

  @override
  String get language => 'Язык';

  @override
  String get profileAbout => 'О приложении';

  @override
  String profileVersion(String version) {
    return 'Версия $version';
  }

  @override
  String get profileServer => 'Сервер';

  @override
  String get unitThousand => 'тыс.';

  @override
  String get unitMillion => 'млн';

  @override
  String get unitBillion => 'млрд';

  @override
  String get unitSum => 'сум';

  @override
  String get unitKg => 'кг';

  @override
  String get unitTon => 'т';

  @override
  String get loginTitle => 'Вход в OneBase';

  @override
  String get loginSubtitle => 'Та же учётная запись, что и на сайте OneBase.';

  @override
  String get loginField => 'Логин или почта';

  @override
  String get passwordField => 'Пароль';

  @override
  String get loginButton => 'Войти';

  @override
  String get loginInvalid => 'Неверный логин или пароль.';

  @override
  String get loginRequired => 'Введите логин и пароль.';

  @override
  String get showPassword => 'Показать пароль';

  @override
  String get hidePassword => 'Скрыть пароль';

  @override
  String get sessionExpired =>
      'Сеанс закрыт: вы вышли на другом устройстве, сменили пароль или доступ отключили. Войдите снова.';

  @override
  String get splashChecking => 'Проверяем вход…';

  @override
  String get restoreFailedTitle => 'Не удалось проверить вход';

  @override
  String get logout => 'Выйти';

  @override
  String get logoutConfirmTitle => 'Выйти из OneBase?';

  @override
  String get logoutConfirmMessage =>
      'Сеанс на этом телефоне закроется, сохранённые данные удалятся.';

  @override
  String get offlineProfile =>
      'Нет связи с сервером — профиль сохранён на телефоне.';

  @override
  String get actionDetails => 'Подробнее';

  @override
  String get actionOpen => 'Открыть';

  @override
  String periodRange(String from, String to) {
    return '$from — $to';
  }

  @override
  String get greetingMorning => 'Доброе утро';

  @override
  String get greetingDay => 'Добрый день';

  @override
  String get greetingEvening => 'Добрый вечер';

  @override
  String get greetingNight => 'Доброй ночи';

  @override
  String greetingName(String greeting, String name) {
    return '$greeting, $name';
  }

  @override
  String get periodTitle => 'Период';

  @override
  String get periodLatest => 'Последние данные';

  @override
  String get periodLatestHint =>
      'Месяц последних данных продаж; финансы — с начала месяца';

  @override
  String get organizationTitle => 'Организация 1С';

  @override
  String get organizationAll => 'Все организации';

  @override
  String get dashboardNothing =>
      'Для вашей роли на главной пока только события. Разделы открывает администратор.';

  @override
  String get salesTitle => 'Продажи';

  @override
  String salesTitleMonth(String month) {
    return 'Продажи · $month';
  }

  @override
  String get kpiRevenue => 'Выручка';

  @override
  String get kpiSold => 'Продано';

  @override
  String get kpiClients => 'Клиенты (АКБ)';

  @override
  String get kpiPlan => 'План месяца';

  @override
  String get kpiNoPlan => 'План не задан';

  @override
  String kpiThrough(String date) {
    return 'данные по $date';
  }

  @override
  String kpiForecast(String value) {
    return 'прогноз $value';
  }

  @override
  String kpiPerAgent(String value, String target) {
    return '$value на ТП · цель $target';
  }

  @override
  String get vsSameDays => 'к тем же дням прошлого месяца';

  @override
  String get vsSameDaysTitle => 'К тем же дням прошлого месяца';

  @override
  String get salesTrendTitle => 'Динамика продаж';

  @override
  String salesTrendCaption(String month, String total) {
    return '$month: $total по дням';
  }

  @override
  String salesTrendSemantics(String month, String total) {
    return 'График продаж по дням за $month, всего $total';
  }

  @override
  String get salesTrendToday => 'Светлый столбик — сегодняшний неполный день.';

  @override
  String get salesTrendEmpty => 'Продаж за период нет.';

  @override
  String get detailVolume => 'Объём и план';

  @override
  String get detailMoney => 'Деньги';

  @override
  String get detailClients => 'Клиенты и визиты';

  @override
  String get detailByDay => 'По дням';

  @override
  String get rowPlan => 'План';

  @override
  String get rowForecast => 'Прогноз на конец месяца';

  @override
  String rowForecastPlan(String value) {
    return '$value плана';
  }

  @override
  String get rowPace => 'Темп в день';

  @override
  String get rowRequired => 'Нужно в день до плана';

  @override
  String get rowRevenuePerOutlet => 'Выручка на точку';

  @override
  String get rowAgents => 'Активные ТП';

  @override
  String get rowAkbPerAgent => 'Клиенты на ТП';

  @override
  String get rowVisits => 'Визиты';

  @override
  String get rowVisitsNoOrder => 'Визиты без заказа';

  @override
  String get rowConversion => 'Конверсия визитов';

  @override
  String get rowFlags => 'Сигналы по ТП';

  @override
  String rowFlagsHint(int critical, int risk) {
    return '$critical критичных · $risk с риском';
  }

  @override
  String rowTarget(String value) {
    return 'цель $value';
  }

  @override
  String rowOfTarget(String value) {
    return '$value от цели';
  }

  @override
  String get financeTitle => 'Финансы · 1С';

  @override
  String get financeNoData => 'Данных 1С пока нет';

  @override
  String get financeNetRevenue => 'Чистая выручка';

  @override
  String get financeExpenses => 'Расходы';

  @override
  String get financeNetProfit => 'Чистая прибыль';

  @override
  String get financeTrendTitle => 'Выручка и расходы за 12 месяцев';

  @override
  String get financePnl => 'Прибыли и убытки';

  @override
  String financePrevious(String value) {
    return 'прошлый период: $value';
  }

  @override
  String get financeBalances => 'Деньги и долги';

  @override
  String get financeCash => 'Деньги на счетах и в кассе';

  @override
  String get financeReceivables => 'Дебиторская задолженность';

  @override
  String get financePayables => 'Кредиторская задолженность';

  @override
  String get financeByMonth => 'По месяцам';

  @override
  String get deptTitle => 'Отделы';

  @override
  String get hrAttendance => 'Явка сегодня';

  @override
  String hrPresentOf(String present, String scheduled) {
    return '$present из $scheduled по графику';
  }

  @override
  String get hrLate => 'Опоздали';

  @override
  String get hrAbsent => 'Нет на месте';

  @override
  String get hrOnLeave => 'В отпуске';

  @override
  String hrCompare(String value) {
    return 'В прошлый рабочий день в это время — $value';
  }

  @override
  String get hrNoSchedule => 'Сегодня по графику никого нет.';

  @override
  String get hrDivisionsAll => 'Все подразделения';

  @override
  String hrSynced(String time) {
    return 'Verifix: $time';
  }

  @override
  String get tasksOpen => 'Открыто';

  @override
  String get tasksInProgress => 'В работе';

  @override
  String get tasksReview => 'На проверке';

  @override
  String get tasksOverdue => 'Просрочено';

  @override
  String get tasksMine => 'Мои ближайшие';

  @override
  String get tasksNoUpcoming => 'Ближайших задач нет.';

  @override
  String tasksDue(String date) {
    return 'до $date';
  }

  @override
  String get tasksDepartments => 'Отделы';

  @override
  String tasksDeptLine(int open, int overdue) {
    return '$open открыто · $overdue просрочено';
  }

  @override
  String get aiTitle => 'Фокус дня · AI';

  @override
  String get aiCritical => 'Критично';

  @override
  String get aiProblems => 'Проблемы';

  @override
  String get aiOpportunities => 'Возможности';

  @override
  String get aiNone => 'AI не нашёл проблем.';

  @override
  String aiChecked(String when) {
    return 'Проверено $when';
  }

  @override
  String get aiWhatToDo => 'Что сделать';

  @override
  String get aiAll => 'Все находки AI';

  @override
  String get severityCritical => 'Критично';

  @override
  String get severityWarning => 'Внимание';

  @override
  String get severityOpportunity => 'Возможность';

  @override
  String get eventsTitle => 'Последние события';

  @override
  String get eventsEmpty => 'Событий пока нет.';

  @override
  String eventsUnread(int n) {
    return '$n непрочитанных';
  }

  @override
  String get agoJustNow => 'только что';

  @override
  String agoMinutes(int n) {
    return '$n мин назад';
  }

  @override
  String agoHours(int n) {
    return '$n ч назад';
  }

  @override
  String agoYesterday(String time) {
    return 'вчера, $time';
  }

  @override
  String get rowExecution => 'Выполнение плана';

  @override
  String get moduleDepartments => 'Отделы';

  @override
  String get moduleSales => 'Клиенты';

  @override
  String get moduleFinance => 'Финансы';

  @override
  String get moduleProduction => 'Производство';

  @override
  String get moduleWarehouse => 'Склад';

  @override
  String get moduleRequests => 'Заявки';

  @override
  String get moduleHr => 'HR';

  @override
  String get moduleInsights => 'AI-отчёт';

  @override
  String get moduleNotifications => 'Уведомления';

  @override
  String get moduleSettings => 'Настройки';

  @override
  String get navOpenMenu => 'Открыть меню разделов';

  @override
  String get navCloseMenu => 'Закрыть меню';

  @override
  String get navSections => 'Основные разделы';

  @override
  String get searchTitle => 'Поиск';

  @override
  String notificationsUnread(int n) {
    return 'Уведомления: $n непрочитанных';
  }

  @override
  String get homePlanDone => 'План выполнен на';

  @override
  String get homeAttendanceToday => 'Явка сегодня';

  @override
  String get homeAllDeptsOk => 'все отделы в норме';

  @override
  String homeDeptsAttention(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n отдела — внимание',
      many: '$n отделов — внимание',
      few: '$n отдела — внимание',
      one: '$n отдел — внимание',
    );
    return '$_temp0';
  }

  @override
  String get homeWaiting => 'Ждут вас';

  @override
  String homeAwaitingApproval(int n) {
    return 'Ждут вашего согласования: $n';
  }

  @override
  String homePickUp(int n) {
    return 'Можно получить на складе: $n';
  }

  @override
  String get homePickUpDept =>
      'Товар по заявкам отдела ждёт на складе — заберите у кладовщика.';

  @override
  String get homePickUpMine =>
      'Товар по вашим заявкам ждёт на складе — заберите у кладовщика.';

  @override
  String homeAcceptedWeek(String sum) {
    return 'Принято на склад за неделю: $sum';
  }

  @override
  String get filterPeriod7d => '7 дней';

  @override
  String get filterPeriod30d => '30 дней';

  @override
  String get filterPeriodQuarter => 'Квартал';

  @override
  String get filterPeriodYear => 'Год';

  @override
  String get filterAllDepartments => 'Все отделы';

  @override
  String filterDepartmentsN(int n) {
    return 'Отделы: $n';
  }

  @override
  String get filterApply => 'Применить';

  @override
  String get filterReset => 'Сбросить';

  @override
  String get heroSalesPlan => 'Выполнение плана продаж';

  @override
  String get heroVsSameDays => 'к тем же дням прошлого месяца';

  @override
  String get heroVsPrevDay => 'к прошлому рабочему дню';

  @override
  String get heroGoal => 'цель';

  @override
  String get heroNorm => 'норма';

  @override
  String get unitPp => 'п.п.';

  @override
  String get statForecast => 'Прогноз';

  @override
  String get statToMonthEnd => 'к концу месяца';

  @override
  String statOverdueN(int n) {
    return '$n просрочено';
  }

  @override
  String statDueWeekN(int n) {
    return '$n срок на неделе';
  }

  @override
  String get statAttendanceMonth => 'Явка за месяц';

  @override
  String get statPerMonth => 'за месяц';

  @override
  String get quickTask => 'Задача';

  @override
  String get quickAskAi => 'Спросить AI';

  @override
  String get quickExport => 'Экспорт';

  @override
  String get quickReportAi => 'Отчёт AI';

  @override
  String get focusTitle => 'Фокус дня';

  @override
  String focusDepartments(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n отдела',
      many: '$n отделов',
      few: '$n отдела',
      one: '$n отдел',
    );
    return '$_temp0';
  }

  @override
  String focusActiveTasks(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n активной задачи',
      many: '$n активных задач',
      few: '$n активные задачи',
      one: '$n активная задача',
    );
    return '$_temp0';
  }

  @override
  String get focusFallback => 'Находки AI-консультанта';

  @override
  String get focusCalm => 'Сегодня всё спокойно';

  @override
  String focusMore(int n) {
    return 'Ещё $n';
  }

  @override
  String focusAskQuestion(String list) {
    return 'Разбери находки «Фокуса дня» и подскажи, что сделать сегодня: $list';
  }

  @override
  String get focusAskEmpty => 'Что сегодня стоит проверить в компании?';

  @override
  String homeAskQuestion(String period, String scope) {
    return 'Разбери показатели компании за период «$period»$scope: что сейчас требует внимания и что сделать в первую очередь?';
  }

  @override
  String get catFinance => 'Финансы';

  @override
  String get catSales => 'Продажи';

  @override
  String get catMarketing => 'Маркетинг';

  @override
  String get catHr => 'HR';

  @override
  String get catProduction => 'Производство';

  @override
  String get catSupply => 'Снабжение';

  @override
  String get dynTitle => 'Динамика';

  @override
  String get dynMore => 'Подробнее';

  @override
  String get dynSales => 'Продажи';

  @override
  String get dynRevenue => 'Выручка';

  @override
  String get dynAttendance => 'Явка';

  @override
  String get dynAverage => 'Среднее';

  @override
  String get dynBest => 'Лучшая неделя';

  @override
  String get dynAbove => 'Выше цели';

  @override
  String dynOfTotal(int n, int total) {
    return '$n из $total';
  }

  @override
  String dynForWeeks(int n) {
    return 'За $n нед.';
  }

  @override
  String dynGoal(String n) {
    return 'цель $n%';
  }

  @override
  String dynWeekValue(int w, String value) {
    return 'Нед. $w · $value';
  }

  @override
  String dynCaption(int w, String days) {
    return 'Нед. $w — текущая, данные за $days';
  }

  @override
  String daysN(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n дня',
      many: '$n дней',
      few: '$n дня',
      one: '$n день',
    );
    return '$_temp0';
  }

  @override
  String get dynEmpty => 'Нет данных за период';

  @override
  String dynSemantics(String name, String avg, String best) {
    return '$name по неделям: среднее $avg, лучшая неделя $best';
  }

  @override
  String tasksAllN(String n) {
    return 'Все $n';
  }

  @override
  String get tasksNone => 'Нет задач за период';

  @override
  String get tasksDonePct => 'выполнено';

  @override
  String get tasksDone => 'Выполнено';

  @override
  String get tasksInWork => 'В работе';

  @override
  String get tasksOnReview => 'На проверке';

  @override
  String get tasksOverdueLabel => 'Просрочено';

  @override
  String tasksOverdueBanner(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n задачи просрочено',
      many: '$n задач просрочено',
      few: '$n задачи просрочены',
      one: '$n задача просрочена',
    );
    return '$_temp0';
  }

  @override
  String tasksOverdueIn(int n, String name) {
    return ' — $n в отделе «$name»';
  }

  @override
  String deptOkN(int n) {
    return '$n в норме';
  }

  @override
  String deptAttentionN(int n) {
    return '$n внимание';
  }

  @override
  String get deptNone => 'Нет данных по отделам';

  @override
  String get deptSalesMetric => 'Выполнение плана месяца';

  @override
  String get deptHrMetric => 'Явка с начала месяца';

  @override
  String deptWeekTrend(String n) {
    return '$n нед.';
  }

  @override
  String get statusAttention => 'Внимание';

  @override
  String get statusOk => 'В норме';

  @override
  String get reportTitle => 'Еженедельный AI-отчёт';

  @override
  String reportLine(String risks, String recs) {
    return '$risks и $recs по отделам';
  }

  @override
  String risksN(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n риска',
      many: '$n рисков',
      few: '$n риска',
      one: '$n риск',
    );
    return '$_temp0';
  }

  @override
  String recsN(int n) {
    String _temp0 = intl.Intl.pluralLogic(
      n,
      locale: localeName,
      other: '$n рекомендации',
      many: '$n рекомендаций',
      few: '$n рекомендации',
      one: '$n рекомендация',
    );
    return '$_temp0';
  }

  @override
  String get dataUpdatedNow => 'Данные обновлены только что';

  @override
  String dataUpdatedMin(int n) {
    return 'Данные обновлены $n мин назад';
  }

  @override
  String get dataPullHint => 'потяните, чтобы обновить';

  @override
  String get dataFailed => 'Не удалось обновить';

  @override
  String get dataUpdating => 'Обновляем…';

  @override
  String offlineSince(String time) {
    return 'Нет соединения · данные на $time';
  }

  @override
  String get exportTitle => 'Экспорт обзора';

  @override
  String get exportCsv => 'Excel (CSV)';

  @override
  String get exportCsvHint => 'Показатели, задачи и отделы таблицей';

  @override
  String get homeMoreSales => 'Продажи месяца';

  @override
  String get homeMoreFinance => 'Финансы 1С';

  @override
  String get homeKeyMetric => 'Ключевая метрика';

  @override
  String homeAgentsN(int n) {
    return '$n ТП в работе';
  }

  @override
  String get homeSearchHint => 'Разделы, сотрудники, клиенты';

  @override
  String get profileOpen => 'Открыть профиль';

  @override
  String get myDayTitle => 'Мой день';

  @override
  String myDayShift(String from, String to) {
    return 'Смена $from–$to';
  }

  @override
  String get myDayArrival => 'Пришли';

  @override
  String get myDayDeparture => 'Ушли';

  @override
  String get myDayWorked => 'Отработано';

  @override
  String get myDayLateWeek => 'Опозданий за неделю';

  @override
  String myDayHoursMinutes(int h, int m) {
    return '$h ч $m мин';
  }

  @override
  String get dayStatus_not_started => 'Рабочий день не начат';

  @override
  String get dayStatus_at_work => 'На работе';

  @override
  String get dayStatus_on_break => 'На перерыве';

  @override
  String get dayStatus_out => 'Вышли';

  @override
  String get dayStatus_finished => 'Рабочий день завершён';

  @override
  String get dayStatus_day_off => 'Выходной';

  @override
  String get dayStatus_on_leave => 'Отсутствие по документу';

  @override
  String get dayStatus_absent => 'Нет прихода';

  @override
  String get dayStatus_other => 'Нет данных';

  @override
  String dayLate(int n) {
    return 'Опоздание $n мин';
  }

  @override
  String get myTasksTitle => 'Мои задачи';

  @override
  String get myTasksNew => 'Новые';

  @override
  String get myTasksWaiting => 'Ждут';

  @override
  String get prefsTitle => 'Что присылать';

  @override
  String get prefsHint =>
      'Отключённые разделы исчезают из уведомлений и счётчиков на всех устройствах.';

  @override
  String get prefCat_tasks => 'Задачи';

  @override
  String get prefCat_ai => 'AI';

  @override
  String get prefCat_hr => 'HR';

  @override
  String get prefCat_sales => 'Продажи';

  @override
  String get prefCat_system => 'Система';

  @override
  String get prefsQueued => 'Сохранится, когда появится сеть';

  @override
  String prefsRejected(String error) {
    return 'Не удалось сохранить: $error';
  }

  @override
  String get telegramTitle => 'Telegram';

  @override
  String get telegramNone =>
      'Telegram не привязан. Привяжите аккаунт, чтобы AI-агент отвечал вам в Telegram с вашими правами.';

  @override
  String get telegramFull => 'полный доступ';

  @override
  String get telegramLimited => 'только отправленные данные';

  @override
  String get telegramLink => 'Привязать Telegram';

  @override
  String get telegramUnlink => 'Отвязать';

  @override
  String get telegramUnlinkConfirm =>
      'Агент перестанет отвечать вам с вашими правами до новой привязки.';

  @override
  String get telegramCodeTitle => 'Код привязки';

  @override
  String telegramCodeHint(String to) {
    return 'Отправьте этот код в Telegram: $to. Код действует 15 минут.';
  }

  @override
  String get actionCopy => 'Скопировать';

  @override
  String get copied => 'Скопировано';

  @override
  String get securityTitle => 'Безопасность';

  @override
  String get securityPassword => 'Пароль';

  @override
  String get securityPasswordHint => 'Пароль меняет администратор OneBase';

  @override
  String get securitySession => 'Сеанс';

  @override
  String get securitySessionHint =>
      'Выход закрывает сеанс только на этом телефоне';

  @override
  String get securityStorage => 'Данные на телефоне';

  @override
  String securityStorageHint(int n) {
    return 'Токен — в защищённом хранилище, сводки — в кэше приложения ($n записей)';
  }

  @override
  String get securityClearCache => 'Очистить кэш';

  @override
  String get cacheCleared => 'Кэш очищен';

  @override
  String get syncTitle => 'Синхронизация';

  @override
  String syncPending(int n) {
    return 'Ждут отправки: $n';
  }

  @override
  String get syncAllSent => 'Все изменения отправлены';

  @override
  String syncFailed(String error) {
    return 'Сервер отказал: $error';
  }

  @override
  String get syncNow => 'Отправить сейчас';

  @override
  String get profileRoles => 'Роли';

  @override
  String get profileDepartment => 'Отдел';

  @override
  String get organization1c => 'Организация 1С';

  @override
  String get langRu => 'Русский';

  @override
  String get langUz => 'O\'zbekcha';

  @override
  String get langSystem => 'Как в системе';

  @override
  String get anSales => 'Продажи';

  @override
  String get anPlanCard => 'Выполнение плана';

  @override
  String get anPlanNone => 'Плана нет';

  @override
  String get anPlanOnTrack => 'Идём по плану';

  @override
  String get anPlanClose => 'Близко к темпу';

  @override
  String get anPlanBehind => 'Отстаём от темпа';

  @override
  String get anPlanDone => 'План выполнен ✓';

  @override
  String get anPlanAlmost => 'Почти выполнен';

  @override
  String get anPlanFailed => 'План не выполнен';

  @override
  String anFactFor(String month) {
    return 'факт за $month';
  }

  @override
  String get anPace => 'Текущий темп';

  @override
  String anPerDay(String value) {
    return '$value/день';
  }

  @override
  String get anRequired => 'Нужно в день';

  @override
  String get anForecast => 'Прогноз на конец месяца';

  @override
  String get anShouldBe => 'должно быть';

  @override
  String anPlanOf(String fact, String plan) {
    return '$fact из $plan';
  }

  @override
  String get anConversion => 'Конверсия визита';

  @override
  String get anRevenuePerOutlet => 'Выручка на ТТ';

  @override
  String get anAkbPerAgent => 'АКБ на агента';

  @override
  String anTarget(String value) {
    return 'цель $value';
  }

  @override
  String anOfTarget(String pct) {
    return '$pct цели';
  }

  @override
  String get anVisitsNoOrder => 'Визиты без заказа';

  @override
  String anVisitsNoOrderOf(String n) {
    return 'из $n визитов ТП';
  }

  @override
  String anAgentsAttention(int bad, int all) {
    return '$bad из $all ТП — внимание';
  }

  @override
  String get anCritical => 'Критично';

  @override
  String get anRisk => 'Риск';

  @override
  String get anNormal => 'В норме';

  @override
  String get anRepublic => 'Республика';

  @override
  String get anMyRegions => 'Мои регионы';

  @override
  String get anDirections => 'Направления';

  @override
  String get anRegions => 'Регионы';

  @override
  String get anAgents => 'Агенты';

  @override
  String get anPlanKg => 'План, кг';

  @override
  String get anFact => 'Факт';

  @override
  String get anForecastShort => 'Прогноз';

  @override
  String get anStrike => 'Страйк';

  @override
  String get anExecution => 'Выполнение';

  @override
  String get anSameDays => 'К прошлому месяцу за те же дни';

  @override
  String get anByDays => 'Продажи по дням';

  @override
  String get anCategories => 'Категории';

  @override
  String anSkuSold(int sold, int total) {
    return 'продаётся $sold из $total SKU';
  }

  @override
  String anWeightShare(String pct) {
    return '$pct от общего веса';
  }

  @override
  String anSilent(int n) {
    return 'молчат $n';
  }

  @override
  String anLost(int n) {
    return 'пропало $n';
  }

  @override
  String get anTeam => 'Команда';

  @override
  String get anPlanFactMonths => 'План и факт по месяцам';

  @override
  String get anRevenueMix => 'Из чего складывается выручка';

  @override
  String anSupervisor(String name) {
    return 'СВР: $name';
  }

  @override
  String anDealer(String name) {
    return 'дилер: $name';
  }

  @override
  String get anVacancy => 'вакансия';

  @override
  String get anNoRemarks => 'Замечаний нет';

  @override
  String get anSearchAgents => 'Имя, ID или регион';

  @override
  String get anFilterAll => 'Все';

  @override
  String get anFilterPlanOk => 'Выполняют план';

  @override
  String get anFilterBelow => 'Ниже плана';

  @override
  String get anFilterNoPlan => 'Без плана';

  @override
  String get anFilterRemarks => 'С замечаниями';

  @override
  String get anFilterVacancy => 'Вакансии';

  @override
  String get anSortExecution => 'По выполнению';

  @override
  String get anSortFact => 'По факту, кг';

  @override
  String get anSortRevenue => 'По выручке';

  @override
  String get anSortName => 'По имени';

  @override
  String anShown(int n, int all) {
    return 'Показано $n из $all ТП';
  }

  @override
  String anAgentNoPlan(String fact) {
    return 'Факт $fact · плана ТП в Linko на месяц нет';
  }

  @override
  String get anRevenue => 'Выручка';

  @override
  String get anVisits => 'Визиты';

  @override
  String anWithOrder(int n) {
    return '$n с заказом';
  }

  @override
  String get anOrders => 'Заказы';

  @override
  String get anTempo => 'Темп';

  @override
  String anCategoriesN(int n) {
    return '$n категорий';
  }

  @override
  String anMedian(String value) {
    return 'медиана региона $value';
  }

  @override
  String get anSumPerVisit => 'Сум с визита';

  @override
  String get anAvgCheck => 'Средний чек';

  @override
  String get anKgPerOutlet => 'Вес на точку';

  @override
  String get anFindings => 'Что нашлось';

  @override
  String get anStores => 'Точки месяца';

  @override
  String get anSilentMarkets => 'Ещё не купили';

  @override
  String get anNewMarkets => 'Новые точки';

  @override
  String anPrevMonth(String value) {
    return 'прошлый месяц $value';
  }

  @override
  String get anStore => 'Точка';

  @override
  String get anStoreAgents => 'Продавали';

  @override
  String get anProducts => 'Артикулы';

  @override
  String get anCompare => 'Сравнение периодов';

  @override
  String anCompareWith(String label) {
    return 'к $label';
  }

  @override
  String get anPeriodWeek => 'Неделя';

  @override
  String get anPeriodMonth => 'Месяц';

  @override
  String get anPeriodQuarter => 'Квартал';

  @override
  String get anPeriodYear => 'Год';

  @override
  String get anModSales => 'Продажи';

  @override
  String get anModFinance => 'Финансы';

  @override
  String get anModProduction => 'Производство';

  @override
  String get anModHr => 'Персонал';

  @override
  String get anNoData => 'нет данных';

  @override
  String get anIncomplete => 'Данные неполные';

  @override
  String get anSlow => 'Считаем — до нескольких секунд';

  @override
  String get anProductionToday => 'Выпуск сегодня';

  @override
  String get anCompletion => 'Выполнение плана';

  @override
  String get anDefect => 'Брак';

  @override
  String get anActiveOrders => 'Заданий в работе';

  @override
  String get anFindingsTitle => 'Находки Аналитика';

  @override
  String anFindingsLine(int critical, int warning, int info) {
    return '$critical критично · $warning внимание · $info к сведению';
  }

  @override
  String get anMonthPick => 'Месяц';

  @override
  String get anOpenAll => 'Открыть';

  @override
  String anScopeNote(String scope) {
    return 'Ваш охват продаж: $scope';
  }

  @override
  String get crmSearch => 'Название, адрес или ID точки';

  @override
  String get crmAttAll => 'Все';

  @override
  String get crmAttDecline => 'Падение продаж';

  @override
  String get crmAttLost => 'Перестали покупать';

  @override
  String get crmAttNotVisited => 'Давно не были';

  @override
  String get crmAttNew => 'Без покупок';

  @override
  String get crmStatusActive => 'Активная';

  @override
  String get crmStatusProblem => 'Проблемная';

  @override
  String get crmStatusInactive => 'Неактивная';

  @override
  String get crmPriorityLow => 'Низкий';

  @override
  String get crmPriorityMedium => 'Средний';

  @override
  String get crmPriorityHigh => 'Высокий';

  @override
  String get crmPriorityUrgent => 'Срочно';

  @override
  String get crmSortSales => 'По продажам за 30 дней';

  @override
  String get crmSortTrend => 'Сильнее упали';

  @override
  String get crmSortLastVisit => 'Давно без визита';

  @override
  String get crmSortName => 'По названию';

  @override
  String crmSales30(String value) {
    return 'за 30 дней $value';
  }

  @override
  String crmLastVisit(String date) {
    return 'визит $date';
  }

  @override
  String get crmNoAccess =>
      'Список клиентов ведётся в Sales Base. Вас ещё не добавили в Sales Base — точки ваших ТП открываются из карточки ТП в «Аналитике».';

  @override
  String get crmMonthToDate => 'С начала месяца';

  @override
  String get crmOrders30 => 'Заказы, 30 дн.';

  @override
  String get crmVisits30 => 'Визиты, 30 дн.';

  @override
  String get crmTrend => 'Тренд';

  @override
  String get crmResponsible => 'Ответственный ТП';

  @override
  String get crmContact => 'Контакт';

  @override
  String get crmPhone => 'Телефон';

  @override
  String get crmNote => 'Заметка';

  @override
  String get crmTarget => 'План на месяц';

  @override
  String get crmMonths => 'Отгрузки по месяцам';

  @override
  String get crmOrders => 'Заказы';

  @override
  String get crmVisits => 'Визиты';

  @override
  String get crmHistory => 'История изменений';

  @override
  String get crmMonthSales => 'Продажи за месяц';

  @override
  String get crmEdit => 'Изменить';

  @override
  String get crmEditTitle => 'Карточка точки';

  @override
  String get crmSaved => 'Сохранено';

  @override
  String get crmStatus => 'Статус';

  @override
  String get crmPriority => 'Приоритет';

  @override
  String get crmLinkoReadonly =>
      'Данные Linko только для чтения: меняется карточка Sales Base (статус, приоритет, план, контакты).';

  @override
  String get actionSave => 'Сохранить';

  @override
  String get auditUpdated => 'Изменена карточка';

  @override
  String get auditAgent => 'Сменён агент';

  @override
  String get crm30d => 'За 30 дней';

  @override
  String get crm90d => 'За 90 дней';

  @override
  String anWas(String value) {
    return 'было $value';
  }

  @override
  String get finTabOverview => 'Обзор';

  @override
  String get finTabPnl => 'Доходы и расходы';

  @override
  String get finTabCash => 'Деньги';

  @override
  String get finTabDebts => 'Долги';

  @override
  String get finTabAnomalies => 'Отклонения';

  @override
  String get finPeriodMtd => 'Этот месяц';

  @override
  String get finPeriodPrev => 'Прошлый месяц';

  @override
  String get finPeriodQuarter => 'Квартал';

  @override
  String get finPeriodYear => 'С начала года';

  @override
  String get finAllOrgs => 'Все организации';

  @override
  String get finDemo => 'Демо-данные тестового источника 1С';

  @override
  String finDataThrough(String date) {
    return 'Проводки по $date';
  }

  @override
  String get finGrossProfit => 'Валовая прибыль';

  @override
  String finVsPrev(String value) {
    return 'к прошлому периоду $value';
  }

  @override
  String get finMonths => '12 месяцев';

  @override
  String get finCashTotal => 'Деньги на счетах и в кассе';

  @override
  String get finReceivables => 'Нам должны';

  @override
  String get finPayables => 'Мы должны';

  @override
  String get finInventory => 'Запасы';

  @override
  String get finExpenseGrowth => 'Расходы, которые выросли';

  @override
  String get finExpensesByAccount => 'Расходы по статьям';

  @override
  String get finExpensesByDepartment => 'Расходы по подразделениям';

  @override
  String get finInflow => 'Поступления';

  @override
  String get finOutflow => 'Платежи';

  @override
  String get finBalances => 'Остатки';

  @override
  String get finOverdue => 'Просрочено';

  @override
  String get finAdvances => 'Авансы';

  @override
  String get finBucket_not_due => 'Срок не наступил';

  @override
  String get finBucket_1_30 => 'Просрочка 1–30 дней';

  @override
  String get finBucket_31_60 => '31–60 дней';

  @override
  String get finBucket_61_90 => '61–90 дней';

  @override
  String get finBucket_over_90 => 'Больше 90 дней';

  @override
  String get finBucket_no_term => 'Срок не задан';

  @override
  String finOverdueDays(int n) {
    return 'просрочка до $n дн.';
  }

  @override
  String get finChecks => 'Что проверено';

  @override
  String get finNoAnomalies => 'Отклонений не найдено';

  @override
  String get finEntries => 'Проводки';

  @override
  String finEntriesTotal(int n, String sum) {
    return '$n проводок на $sum';
  }

  @override
  String get finDocument => 'Документ 1С';

  @override
  String get finPosted => 'Проведён';

  @override
  String get finNotPosted => 'Не проведён';

  @override
  String get finCounterparty => 'Контрагент';

  @override
  String get finContract => 'Договор';

  @override
  String get finWarehouse => 'Склад';

  @override
  String get finPurpose => 'Назначение';

  @override
  String get finLines => 'Строки';

  @override
  String get finVat => 'НДС';

  @override
  String get finAccounts => 'Счета (ОСВ)';

  @override
  String get finOpening => 'Начало';

  @override
  String get finTurnover => 'Обороты';

  @override
  String get finClosing => 'Конец';

  @override
  String get finDebit => 'Дт';

  @override
  String get finCredit => 'Кт';

  @override
  String get prTabOverview => 'Обзор';

  @override
  String get prTabOrders => 'Задания';

  @override
  String get prTabShifts => 'Смены';

  @override
  String get prGood => 'Годный выпуск';

  @override
  String get prPlan => 'План';

  @override
  String get prOpenShifts => 'Смены открыты';

  @override
  String get prClosedShifts => 'Ждут утверждения';

  @override
  String get prNormDev => 'Отклонение от нормы';

  @override
  String get prByProduct => 'Выпуск по продуктам';

  @override
  String get prIssues => 'Проблемы';

  @override
  String get prStockValue => 'Сырьё на складах';

  @override
  String get prExpiring => 'Истекает за 30 дней';

  @override
  String get prExpired => 'Просрочено';

  @override
  String get prBelowMin => 'Ниже минимума';

  @override
  String get prOverdueOrders => 'Просрочено заданий';

  @override
  String get prLowDays => 'Запаса меньше всего';

  @override
  String prDaysLeft(String n) {
    return '$n дн.';
  }

  @override
  String get prStatus_Draft => 'Черновик';

  @override
  String get prStatus_Released => 'Выпущено';

  @override
  String get prStatus_InProgress => 'В работе';

  @override
  String get prStatus_Completed => 'Закрыто';

  @override
  String get prStatus_Cancelled => 'Отменено';

  @override
  String get prFilterOpen => 'Открытые';

  @override
  String get prFilterDone => 'Закрытые';

  @override
  String get prFilterAll => 'Все';

  @override
  String prPlanned(String qty, String date) {
    return 'План $qty · на $date';
  }

  @override
  String get prOutput => 'Выпуск';

  @override
  String get prMaterials => 'Сырьё и материалы';

  @override
  String prRequired(String v) {
    return 'нужно $v';
  }

  @override
  String prIssued(String v) {
    return 'выдано $v';
  }

  @override
  String prLeft(String v) {
    return 'осталось выдать $v';
  }

  @override
  String prAvailable(String v) {
    return 'на складе $v';
  }

  @override
  String get prOverconsumed => 'перерасход';

  @override
  String get prHistory => 'История';

  @override
  String get prRelease => 'Выпустить задание';

  @override
  String get prComplete => 'Закрыть задание';

  @override
  String get prCancel => 'Отменить задание';

  @override
  String get prReceive => 'Оприходовать выпуск';

  @override
  String prOutputQty(String unit) {
    return 'Фактический выпуск, $unit';
  }

  @override
  String get prReason => 'Причина';

  @override
  String get prConfirmTitle => 'Подтвердите операцию';

  @override
  String get prConfirmRelease =>
      'Задание уйдёт в работу; нехватка сырья будет отмечена, снабжение получит уведомление.';

  @override
  String prConfirmComplete(String qty) {
    return 'Будет зафиксирован выпуск $qty и посчитан расход по норме. Отменить закрытие нельзя.';
  }

  @override
  String get prConfirmReceive => 'Выпуск будет оприходован на склад.';

  @override
  String prDone(String what) {
    return 'Готово: $what';
  }

  @override
  String get prConflict =>
      'Задание изменили — обновили данные, проверьте и повторите.';

  @override
  String prShift(int n, String date) {
    return 'Смена $n · $date';
  }

  @override
  String get prShiftStatus_Draft => 'Ввод';

  @override
  String get prShiftStatus_Closed => 'Ждёт утверждения';

  @override
  String get prShiftStatus_Approved => 'Утверждена';

  @override
  String get prShiftStatus_Superseded => 'Заменена';

  @override
  String get prShiftStatus_Cancelled => 'Отменена';

  @override
  String get whTabStock => 'Остатки';

  @override
  String get whTabMoves => 'Движения';

  @override
  String get whSearch => 'Артикул, название или штрихкод';

  @override
  String get whFilterAll => 'Все';

  @override
  String get whFilterLow => 'Ниже минимума';

  @override
  String get whFilterReserved => 'В резерве';

  @override
  String get whFilterEmpty => 'Нет остатка';

  @override
  String get whFilterExpiring => 'Истекают';

  @override
  String get whFilterExpired => 'Просрочены';

  @override
  String get whOnHand => 'Остаток';

  @override
  String get whReserved => 'Резерв';

  @override
  String get whAvailable => 'Свободно';

  @override
  String get whMin => 'Минимум';

  @override
  String get whReservations => 'Резервы по заявкам';

  @override
  String get whMovements => 'Последние движения';

  @override
  String get whOperation => 'Операция';

  @override
  String get whOpReceipt => 'Приход';

  @override
  String get whOpIssue => 'Выдача отделу';

  @override
  String get whOpWriteOff => 'Списание';

  @override
  String get whItem => 'Позиция';

  @override
  String get whPickItem => 'Выберите позицию';

  @override
  String whQuantity(String unit) {
    return 'Количество, $unit';
  }

  @override
  String get whUnitCost => 'Цена за единицу, сум';

  @override
  String get whDepartment => 'Отдел';

  @override
  String get whReceiver => 'Кто получил';

  @override
  String get whComment => 'Комментарий';

  @override
  String get whReference => 'Накладная';

  @override
  String get whReasonRequired => 'Укажите причину списания';

  @override
  String get whPost => 'Провести';

  @override
  String whConfirm(String op, String item, String qty) {
    return '$op: $item — $qty. Остаток на складе изменится.';
  }

  @override
  String get whPosted => 'Документ проведён';

  @override
  String get whNoPosts =>
      'Проводить операции на складе может только кладовщик этого склада.';

  @override
  String get whWarehouse => 'Склад';

  @override
  String get whOnline =>
      'Операции склада проводятся только при связи с сервером.';

  @override
  String get mvReceipt => 'Приход';

  @override
  String get mvIssue => 'Выдача';

  @override
  String get mvWriteOff => 'Списание';

  @override
  String get mvAdjustment => 'Инвентаризация';

  @override
  String get mvTransfer => 'Перемещение';

  @override
  String get mvReversal => 'Сторно';

  @override
  String get mvProduction => 'Производство';

  @override
  String get rqScopeMy => 'Мои';

  @override
  String get rqScopeDepartment => 'Отдела';

  @override
  String get rqScopeAll => 'Все';

  @override
  String rqItems(int n) {
    return '$n поз.';
  }

  @override
  String rqDesired(String date) {
    return 'нужно к $date';
  }

  @override
  String get tkScopeMy => 'Мои';

  @override
  String get tkScopeAll => 'Все';

  @override
  String get tkScopeDepartment => 'Отдел';

  @override
  String get tkScopeArchive => 'Архив';

  @override
  String get tkStatusOpen => 'Открытые';

  @override
  String get tkStatusOverdue => 'Просрочены';

  @override
  String get tkStatusBlocked => 'Заблокированы';

  @override
  String get tkStatusClosed => 'Закрытые';

  @override
  String get tkSearch => 'Название, описание или исполнитель';

  @override
  String get tkNew => 'Новая задача';

  @override
  String get tkSt_New => 'Новая';

  @override
  String get tkSt_InProgress => 'В работе';

  @override
  String get tkSt_Review => 'На проверке';

  @override
  String get tkSt_Done => 'Выполнена';

  @override
  String get tkSt_Cancelled => 'Отменена';

  @override
  String get tkBlocked => 'Заблокирована';

  @override
  String get tkPr_Low => 'Низкий';

  @override
  String get tkPr_Medium => 'Обычный';

  @override
  String get tkPr_High => 'Высокий';

  @override
  String get tkPr_Urgent => 'Критический';

  @override
  String get tkNoDue => 'без срока';

  @override
  String tkDue(String date) {
    return 'срок $date';
  }

  @override
  String get tkToday => 'сегодня';

  @override
  String get tkTomorrow => 'завтра';

  @override
  String get tkAnyone => 'Любой сотрудник отдела';

  @override
  String get tkAct_InProgress => 'В работу';

  @override
  String get tkAct_Review => 'Отправить на проверку';

  @override
  String get tkAct_Done => 'Завершить';

  @override
  String get tkAct_Accept => 'Принять результат';

  @override
  String get tkAct_Return => 'Вернуть на доработку';

  @override
  String get tkAct_Reopen => 'Вернуть в работу';

  @override
  String get tkAct_Cancelled => 'Отменить';

  @override
  String get tkCommentRequired => 'Напишите, что доработать';

  @override
  String get tkResult => 'Итог (необязательно)';

  @override
  String get tkDescription => 'Описание';

  @override
  String get tkChecklist => 'Чек-лист';

  @override
  String tkProgress(int n) {
    return 'Готово $n%';
  }

  @override
  String get tkComments => 'Комментарии';

  @override
  String get tkCommentHint => 'Написать комментарий';

  @override
  String get tkSend => 'Отправить';

  @override
  String get tkAttachments => 'Вложения';

  @override
  String get tkAttach => 'Прикрепить файл';

  @override
  String get tkTooBig => 'Файл больше 20 МБ';

  @override
  String get tkHistory => 'История';

  @override
  String get tkBlock => 'Заблокировать';

  @override
  String get tkUnblock => 'Снять блок';

  @override
  String get tkBlockReason => 'Что мешает';

  @override
  String get tkAssignee => 'Исполнитель';

  @override
  String get tkAuthor => 'Постановщик';

  @override
  String get tkDepartment => 'Отдел';

  @override
  String get tkProject => 'Проект';

  @override
  String get tkTitle => 'Название';

  @override
  String get tkPriority => 'Приоритет';

  @override
  String get tkDueDate => 'Срок';

  @override
  String get tkDueTime => 'Время';

  @override
  String get tkCreate => 'Поставить задачу';

  @override
  String get tkCreated => 'Задача поставлена';

  @override
  String get tkTitleRequired => 'Введите название';

  @override
  String get tkDeptRequired => 'Выберите отдел';

  @override
  String get tkCommentAdded => 'Комментарий добавлен';

  @override
  String get tkAttachmentAdded => 'Файл прикреплён';

  @override
  String get tkOpenFile => 'Открыть';

  @override
  String get tkOnlineOnly => 'Нужна связь с сервером';

  @override
  String get hrTabOverview => 'Обзор';

  @override
  String get hrTabEmployees => 'Сотрудники';

  @override
  String get hrOnTime => 'Вовремя';

  @override
  String get hrLateN => 'Опоздали';

  @override
  String get hrAbsentN => 'Отсутствуют';

  @override
  String get hrLeaveN => 'По документу';

  @override
  String get hrExpectedN => 'Ожидаются';

  @override
  String get hrUnknownN => 'Не ясно';

  @override
  String get hrActive => 'Штат';

  @override
  String get hrAvgArrival => 'Средний приход';

  @override
  String get hrAttention => 'Требует внимания';

  @override
  String get hrDivisions => 'Подразделения';

  @override
  String get hrByHour => 'Проходы по часам';

  @override
  String get hrSearchEmp => 'ФИО, телефон или ID Verifix';

  @override
  String get hrDay_Present => 'Пришёл';

  @override
  String get hrDay_Absent => 'Отсутствует';

  @override
  String get hrDay_OnLeave => 'По документу';

  @override
  String get hrDay_DayOff => 'Выходной';

  @override
  String get hrDay_Expected => 'Ожидается';

  @override
  String get hrDay_NoSchedule => 'Нет графика';

  @override
  String get hrDay_Unknown => 'Не ясно';

  @override
  String get hrDay_NotEmployed => 'Не работал';

  @override
  String hrLateMin(int n) {
    return 'опоздание $n мин';
  }

  @override
  String get hrPeriod14 => '14 дней';

  @override
  String hrSummaryPresent(int n, int plan) {
    return 'Пришёл $n из $plan';
  }

  @override
  String get hrSummaryLate => 'Опозданий';

  @override
  String get hrSummaryAbsent => 'Отсутствий';

  @override
  String get hrWorked => 'Отработано';

  @override
  String get hrDays => 'Дни';
}
