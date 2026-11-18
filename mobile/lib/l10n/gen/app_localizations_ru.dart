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
  String get navAi => 'AI';

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
  String get deptTitle => 'Подразделения';

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
  String hrSynced(String when) {
    return 'Verifix: $when';
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
}
