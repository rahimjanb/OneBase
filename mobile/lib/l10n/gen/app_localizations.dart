import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_ru.dart';
import 'app_localizations_uz.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'gen/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('ru'),
    Locale('uz'),
  ];

  /// No description provided for @appTitle.
  ///
  /// In ru, this message translates to:
  /// **'OneBase'**
  String get appTitle;

  /// No description provided for @navHome.
  ///
  /// In ru, this message translates to:
  /// **'Главная'**
  String get navHome;

  /// No description provided for @navAnalytics.
  ///
  /// In ru, this message translates to:
  /// **'Аналитика'**
  String get navAnalytics;

  /// No description provided for @navTasks.
  ///
  /// In ru, this message translates to:
  /// **'Задачи'**
  String get navTasks;

  /// No description provided for @navAi.
  ///
  /// In ru, this message translates to:
  /// **'AI'**
  String get navAi;

  /// No description provided for @navProfile.
  ///
  /// In ru, this message translates to:
  /// **'Профиль'**
  String get navProfile;

  /// No description provided for @actionRetry.
  ///
  /// In ru, this message translates to:
  /// **'Повторить'**
  String get actionRetry;

  /// No description provided for @actionRefresh.
  ///
  /// In ru, this message translates to:
  /// **'Обновить'**
  String get actionRefresh;

  /// No description provided for @actionAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get actionAll;

  /// No description provided for @actionClose.
  ///
  /// In ru, this message translates to:
  /// **'Закрыть'**
  String get actionClose;

  /// No description provided for @actionCancel.
  ///
  /// In ru, this message translates to:
  /// **'Отмена'**
  String get actionCancel;

  /// No description provided for @stateLoading.
  ///
  /// In ru, this message translates to:
  /// **'Загрузка…'**
  String get stateLoading;

  /// No description provided for @stateEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Нет данных'**
  String get stateEmpty;

  /// No description provided for @offlineBanner.
  ///
  /// In ru, this message translates to:
  /// **'Нет сети — показаны сохранённые данные'**
  String get offlineBanner;

  /// No description provided for @syncedJustNow.
  ///
  /// In ru, this message translates to:
  /// **'Обновлено только что'**
  String get syncedJustNow;

  /// No description provided for @syncedMinutes.
  ///
  /// In ru, this message translates to:
  /// **'Обновлено {n} мин назад'**
  String syncedMinutes(int n);

  /// No description provided for @syncedHours.
  ///
  /// In ru, this message translates to:
  /// **'Обновлено {n} ч назад'**
  String syncedHours(int n);

  /// No description provided for @syncedAt.
  ///
  /// In ru, this message translates to:
  /// **'Обновлено {date}'**
  String syncedAt(String date);

  /// No description provided for @comingSoonTitle.
  ///
  /// In ru, this message translates to:
  /// **'Раздел в разработке'**
  String get comingSoonTitle;

  /// No description provided for @comingSoonMessage.
  ///
  /// In ru, this message translates to:
  /// **'Появится на этапе {stage} плана мобильного приложения.'**
  String comingSoonMessage(int stage);

  /// No description provided for @errorNetwork.
  ///
  /// In ru, this message translates to:
  /// **'Нет соединения с сервером. Проверьте интернет.'**
  String get errorNetwork;

  /// No description provided for @errorTimeout.
  ///
  /// In ru, this message translates to:
  /// **'Сервер долго не отвечает. Попробуйте ещё раз.'**
  String get errorTimeout;

  /// No description provided for @errorUnauthorized.
  ///
  /// In ru, this message translates to:
  /// **'Сеанс закрыт. Войдите снова.'**
  String get errorUnauthorized;

  /// No description provided for @errorForbidden.
  ///
  /// In ru, this message translates to:
  /// **'Нет доступа к этому разделу.'**
  String get errorForbidden;

  /// No description provided for @errorNotFound.
  ///
  /// In ru, this message translates to:
  /// **'Не найдено.'**
  String get errorNotFound;

  /// No description provided for @errorRateLimited.
  ///
  /// In ru, this message translates to:
  /// **'Слишком много попыток. Подождите минуту.'**
  String get errorRateLimited;

  /// No description provided for @errorServer.
  ///
  /// In ru, this message translates to:
  /// **'Ошибка на сервере. Попробуйте позже.'**
  String get errorServer;

  /// No description provided for @errorInvalidResponse.
  ///
  /// In ru, this message translates to:
  /// **'Сервер ответил неожиданно. Обновите приложение.'**
  String get errorInvalidResponse;

  /// No description provided for @errorUnknown.
  ///
  /// In ru, this message translates to:
  /// **'Что-то пошло не так.'**
  String get errorUnknown;

  /// No description provided for @errorNotConfigured.
  ///
  /// In ru, this message translates to:
  /// **'Адрес сервера не задан при сборке приложения.'**
  String get errorNotConfigured;

  /// No description provided for @profileAppearance.
  ///
  /// In ru, this message translates to:
  /// **'Оформление'**
  String get profileAppearance;

  /// No description provided for @themeSystem.
  ///
  /// In ru, this message translates to:
  /// **'Как в системе'**
  String get themeSystem;

  /// No description provided for @themeLight.
  ///
  /// In ru, this message translates to:
  /// **'Светлая'**
  String get themeLight;

  /// No description provided for @themeDark.
  ///
  /// In ru, this message translates to:
  /// **'Тёмная'**
  String get themeDark;

  /// No description provided for @language.
  ///
  /// In ru, this message translates to:
  /// **'Язык'**
  String get language;

  /// No description provided for @profileAbout.
  ///
  /// In ru, this message translates to:
  /// **'О приложении'**
  String get profileAbout;

  /// No description provided for @profileVersion.
  ///
  /// In ru, this message translates to:
  /// **'Версия {version}'**
  String profileVersion(String version);

  /// No description provided for @profileServer.
  ///
  /// In ru, this message translates to:
  /// **'Сервер'**
  String get profileServer;

  /// No description provided for @unitThousand.
  ///
  /// In ru, this message translates to:
  /// **'тыс.'**
  String get unitThousand;

  /// No description provided for @unitMillion.
  ///
  /// In ru, this message translates to:
  /// **'млн'**
  String get unitMillion;

  /// No description provided for @unitBillion.
  ///
  /// In ru, this message translates to:
  /// **'млрд'**
  String get unitBillion;

  /// No description provided for @unitSum.
  ///
  /// In ru, this message translates to:
  /// **'сум'**
  String get unitSum;

  /// No description provided for @unitKg.
  ///
  /// In ru, this message translates to:
  /// **'кг'**
  String get unitKg;

  /// No description provided for @unitTon.
  ///
  /// In ru, this message translates to:
  /// **'т'**
  String get unitTon;

  /// No description provided for @loginTitle.
  ///
  /// In ru, this message translates to:
  /// **'Вход в OneBase'**
  String get loginTitle;

  /// No description provided for @loginSubtitle.
  ///
  /// In ru, this message translates to:
  /// **'Та же учётная запись, что и на сайте OneBase.'**
  String get loginSubtitle;

  /// No description provided for @loginField.
  ///
  /// In ru, this message translates to:
  /// **'Логин или почта'**
  String get loginField;

  /// No description provided for @passwordField.
  ///
  /// In ru, this message translates to:
  /// **'Пароль'**
  String get passwordField;

  /// No description provided for @loginButton.
  ///
  /// In ru, this message translates to:
  /// **'Войти'**
  String get loginButton;

  /// No description provided for @loginInvalid.
  ///
  /// In ru, this message translates to:
  /// **'Неверный логин или пароль.'**
  String get loginInvalid;

  /// No description provided for @loginRequired.
  ///
  /// In ru, this message translates to:
  /// **'Введите логин и пароль.'**
  String get loginRequired;

  /// No description provided for @showPassword.
  ///
  /// In ru, this message translates to:
  /// **'Показать пароль'**
  String get showPassword;

  /// No description provided for @hidePassword.
  ///
  /// In ru, this message translates to:
  /// **'Скрыть пароль'**
  String get hidePassword;

  /// No description provided for @sessionExpired.
  ///
  /// In ru, this message translates to:
  /// **'Сеанс закрыт: вы вышли на другом устройстве, сменили пароль или доступ отключили. Войдите снова.'**
  String get sessionExpired;

  /// No description provided for @splashChecking.
  ///
  /// In ru, this message translates to:
  /// **'Проверяем вход…'**
  String get splashChecking;

  /// No description provided for @restoreFailedTitle.
  ///
  /// In ru, this message translates to:
  /// **'Не удалось проверить вход'**
  String get restoreFailedTitle;

  /// No description provided for @logout.
  ///
  /// In ru, this message translates to:
  /// **'Выйти'**
  String get logout;

  /// No description provided for @logoutConfirmTitle.
  ///
  /// In ru, this message translates to:
  /// **'Выйти из OneBase?'**
  String get logoutConfirmTitle;

  /// No description provided for @logoutConfirmMessage.
  ///
  /// In ru, this message translates to:
  /// **'Сеанс на этом телефоне закроется, сохранённые данные удалятся.'**
  String get logoutConfirmMessage;

  /// No description provided for @offlineProfile.
  ///
  /// In ru, this message translates to:
  /// **'Нет связи с сервером — профиль сохранён на телефоне.'**
  String get offlineProfile;

  /// No description provided for @actionDetails.
  ///
  /// In ru, this message translates to:
  /// **'Подробнее'**
  String get actionDetails;

  /// No description provided for @actionOpen.
  ///
  /// In ru, this message translates to:
  /// **'Открыть'**
  String get actionOpen;

  /// No description provided for @periodRange.
  ///
  /// In ru, this message translates to:
  /// **'{from} — {to}'**
  String periodRange(String from, String to);

  /// No description provided for @greetingMorning.
  ///
  /// In ru, this message translates to:
  /// **'Доброе утро'**
  String get greetingMorning;

  /// No description provided for @greetingDay.
  ///
  /// In ru, this message translates to:
  /// **'Добрый день'**
  String get greetingDay;

  /// No description provided for @greetingEvening.
  ///
  /// In ru, this message translates to:
  /// **'Добрый вечер'**
  String get greetingEvening;

  /// No description provided for @greetingNight.
  ///
  /// In ru, this message translates to:
  /// **'Доброй ночи'**
  String get greetingNight;

  /// No description provided for @greetingName.
  ///
  /// In ru, this message translates to:
  /// **'{greeting}, {name}'**
  String greetingName(String greeting, String name);

  /// No description provided for @periodTitle.
  ///
  /// In ru, this message translates to:
  /// **'Период'**
  String get periodTitle;

  /// No description provided for @periodLatest.
  ///
  /// In ru, this message translates to:
  /// **'Последние данные'**
  String get periodLatest;

  /// No description provided for @periodLatestHint.
  ///
  /// In ru, this message translates to:
  /// **'Месяц последних данных продаж; финансы — с начала месяца'**
  String get periodLatestHint;

  /// No description provided for @organizationTitle.
  ///
  /// In ru, this message translates to:
  /// **'Организация 1С'**
  String get organizationTitle;

  /// No description provided for @organizationAll.
  ///
  /// In ru, this message translates to:
  /// **'Все организации'**
  String get organizationAll;

  /// No description provided for @dashboardNothing.
  ///
  /// In ru, this message translates to:
  /// **'Для вашей роли на главной пока только события. Разделы открывает администратор.'**
  String get dashboardNothing;

  /// No description provided for @salesTitle.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get salesTitle;

  /// No description provided for @salesTitleMonth.
  ///
  /// In ru, this message translates to:
  /// **'Продажи · {month}'**
  String salesTitleMonth(String month);

  /// No description provided for @kpiRevenue.
  ///
  /// In ru, this message translates to:
  /// **'Выручка'**
  String get kpiRevenue;

  /// No description provided for @kpiSold.
  ///
  /// In ru, this message translates to:
  /// **'Продано'**
  String get kpiSold;

  /// No description provided for @kpiClients.
  ///
  /// In ru, this message translates to:
  /// **'Клиенты (АКБ)'**
  String get kpiClients;

  /// No description provided for @kpiPlan.
  ///
  /// In ru, this message translates to:
  /// **'План месяца'**
  String get kpiPlan;

  /// No description provided for @kpiNoPlan.
  ///
  /// In ru, this message translates to:
  /// **'План не задан'**
  String get kpiNoPlan;

  /// No description provided for @kpiThrough.
  ///
  /// In ru, this message translates to:
  /// **'данные по {date}'**
  String kpiThrough(String date);

  /// No description provided for @kpiForecast.
  ///
  /// In ru, this message translates to:
  /// **'прогноз {value}'**
  String kpiForecast(String value);

  /// No description provided for @kpiPerAgent.
  ///
  /// In ru, this message translates to:
  /// **'{value} на ТП · цель {target}'**
  String kpiPerAgent(String value, String target);

  /// No description provided for @vsSameDays.
  ///
  /// In ru, this message translates to:
  /// **'к тем же дням прошлого месяца'**
  String get vsSameDays;

  /// No description provided for @vsSameDaysTitle.
  ///
  /// In ru, this message translates to:
  /// **'К тем же дням прошлого месяца'**
  String get vsSameDaysTitle;

  /// No description provided for @salesTrendTitle.
  ///
  /// In ru, this message translates to:
  /// **'Динамика продаж'**
  String get salesTrendTitle;

  /// No description provided for @salesTrendCaption.
  ///
  /// In ru, this message translates to:
  /// **'{month}: {total} по дням'**
  String salesTrendCaption(String month, String total);

  /// No description provided for @salesTrendSemantics.
  ///
  /// In ru, this message translates to:
  /// **'График продаж по дням за {month}, всего {total}'**
  String salesTrendSemantics(String month, String total);

  /// No description provided for @salesTrendToday.
  ///
  /// In ru, this message translates to:
  /// **'Светлый столбик — сегодняшний неполный день.'**
  String get salesTrendToday;

  /// No description provided for @salesTrendEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Продаж за период нет.'**
  String get salesTrendEmpty;

  /// No description provided for @detailVolume.
  ///
  /// In ru, this message translates to:
  /// **'Объём и план'**
  String get detailVolume;

  /// No description provided for @detailMoney.
  ///
  /// In ru, this message translates to:
  /// **'Деньги'**
  String get detailMoney;

  /// No description provided for @detailClients.
  ///
  /// In ru, this message translates to:
  /// **'Клиенты и визиты'**
  String get detailClients;

  /// No description provided for @detailByDay.
  ///
  /// In ru, this message translates to:
  /// **'По дням'**
  String get detailByDay;

  /// No description provided for @rowPlan.
  ///
  /// In ru, this message translates to:
  /// **'План'**
  String get rowPlan;

  /// No description provided for @rowForecast.
  ///
  /// In ru, this message translates to:
  /// **'Прогноз на конец месяца'**
  String get rowForecast;

  /// No description provided for @rowForecastPlan.
  ///
  /// In ru, this message translates to:
  /// **'{value} плана'**
  String rowForecastPlan(String value);

  /// No description provided for @rowPace.
  ///
  /// In ru, this message translates to:
  /// **'Темп в день'**
  String get rowPace;

  /// No description provided for @rowRequired.
  ///
  /// In ru, this message translates to:
  /// **'Нужно в день до плана'**
  String get rowRequired;

  /// No description provided for @rowRevenuePerOutlet.
  ///
  /// In ru, this message translates to:
  /// **'Выручка на точку'**
  String get rowRevenuePerOutlet;

  /// No description provided for @rowAgents.
  ///
  /// In ru, this message translates to:
  /// **'Активные ТП'**
  String get rowAgents;

  /// No description provided for @rowAkbPerAgent.
  ///
  /// In ru, this message translates to:
  /// **'Клиенты на ТП'**
  String get rowAkbPerAgent;

  /// No description provided for @rowVisits.
  ///
  /// In ru, this message translates to:
  /// **'Визиты'**
  String get rowVisits;

  /// No description provided for @rowVisitsNoOrder.
  ///
  /// In ru, this message translates to:
  /// **'Визиты без заказа'**
  String get rowVisitsNoOrder;

  /// No description provided for @rowConversion.
  ///
  /// In ru, this message translates to:
  /// **'Конверсия визитов'**
  String get rowConversion;

  /// No description provided for @rowFlags.
  ///
  /// In ru, this message translates to:
  /// **'Сигналы по ТП'**
  String get rowFlags;

  /// No description provided for @rowFlagsHint.
  ///
  /// In ru, this message translates to:
  /// **'{critical} критичных · {risk} с риском'**
  String rowFlagsHint(int critical, int risk);

  /// No description provided for @rowTarget.
  ///
  /// In ru, this message translates to:
  /// **'цель {value}'**
  String rowTarget(String value);

  /// No description provided for @rowOfTarget.
  ///
  /// In ru, this message translates to:
  /// **'{value} от цели'**
  String rowOfTarget(String value);

  /// No description provided for @financeTitle.
  ///
  /// In ru, this message translates to:
  /// **'Финансы · 1С'**
  String get financeTitle;

  /// No description provided for @financeNoData.
  ///
  /// In ru, this message translates to:
  /// **'Данных 1С пока нет'**
  String get financeNoData;

  /// No description provided for @financeNetRevenue.
  ///
  /// In ru, this message translates to:
  /// **'Чистая выручка'**
  String get financeNetRevenue;

  /// No description provided for @financeExpenses.
  ///
  /// In ru, this message translates to:
  /// **'Расходы'**
  String get financeExpenses;

  /// No description provided for @financeNetProfit.
  ///
  /// In ru, this message translates to:
  /// **'Чистая прибыль'**
  String get financeNetProfit;

  /// No description provided for @financeTrendTitle.
  ///
  /// In ru, this message translates to:
  /// **'Выручка и расходы за 12 месяцев'**
  String get financeTrendTitle;

  /// No description provided for @financePnl.
  ///
  /// In ru, this message translates to:
  /// **'Прибыли и убытки'**
  String get financePnl;

  /// No description provided for @financePrevious.
  ///
  /// In ru, this message translates to:
  /// **'прошлый период: {value}'**
  String financePrevious(String value);

  /// No description provided for @financeBalances.
  ///
  /// In ru, this message translates to:
  /// **'Деньги и долги'**
  String get financeBalances;

  /// No description provided for @financeCash.
  ///
  /// In ru, this message translates to:
  /// **'Деньги на счетах и в кассе'**
  String get financeCash;

  /// No description provided for @financeReceivables.
  ///
  /// In ru, this message translates to:
  /// **'Дебиторская задолженность'**
  String get financeReceivables;

  /// No description provided for @financePayables.
  ///
  /// In ru, this message translates to:
  /// **'Кредиторская задолженность'**
  String get financePayables;

  /// No description provided for @financeByMonth.
  ///
  /// In ru, this message translates to:
  /// **'По месяцам'**
  String get financeByMonth;

  /// No description provided for @deptTitle.
  ///
  /// In ru, this message translates to:
  /// **'Подразделения'**
  String get deptTitle;

  /// No description provided for @hrAttendance.
  ///
  /// In ru, this message translates to:
  /// **'Явка сегодня'**
  String get hrAttendance;

  /// No description provided for @hrPresentOf.
  ///
  /// In ru, this message translates to:
  /// **'{present} из {scheduled} по графику'**
  String hrPresentOf(String present, String scheduled);

  /// No description provided for @hrLate.
  ///
  /// In ru, this message translates to:
  /// **'Опоздали'**
  String get hrLate;

  /// No description provided for @hrAbsent.
  ///
  /// In ru, this message translates to:
  /// **'Нет на месте'**
  String get hrAbsent;

  /// No description provided for @hrOnLeave.
  ///
  /// In ru, this message translates to:
  /// **'В отпуске'**
  String get hrOnLeave;

  /// No description provided for @hrCompare.
  ///
  /// In ru, this message translates to:
  /// **'В прошлый рабочий день в это время — {value}'**
  String hrCompare(String value);

  /// No description provided for @hrNoSchedule.
  ///
  /// In ru, this message translates to:
  /// **'Сегодня по графику никого нет.'**
  String get hrNoSchedule;

  /// No description provided for @hrDivisionsAll.
  ///
  /// In ru, this message translates to:
  /// **'Все подразделения'**
  String get hrDivisionsAll;

  /// No description provided for @hrSynced.
  ///
  /// In ru, this message translates to:
  /// **'Verifix: {when}'**
  String hrSynced(String when);

  /// No description provided for @tasksOpen.
  ///
  /// In ru, this message translates to:
  /// **'Открыто'**
  String get tasksOpen;

  /// No description provided for @tasksInProgress.
  ///
  /// In ru, this message translates to:
  /// **'В работе'**
  String get tasksInProgress;

  /// No description provided for @tasksReview.
  ///
  /// In ru, this message translates to:
  /// **'На проверке'**
  String get tasksReview;

  /// No description provided for @tasksOverdue.
  ///
  /// In ru, this message translates to:
  /// **'Просрочено'**
  String get tasksOverdue;

  /// No description provided for @tasksMine.
  ///
  /// In ru, this message translates to:
  /// **'Мои ближайшие'**
  String get tasksMine;

  /// No description provided for @tasksNoUpcoming.
  ///
  /// In ru, this message translates to:
  /// **'Ближайших задач нет.'**
  String get tasksNoUpcoming;

  /// No description provided for @tasksDue.
  ///
  /// In ru, this message translates to:
  /// **'до {date}'**
  String tasksDue(String date);

  /// No description provided for @tasksDepartments.
  ///
  /// In ru, this message translates to:
  /// **'Отделы'**
  String get tasksDepartments;

  /// No description provided for @tasksDeptLine.
  ///
  /// In ru, this message translates to:
  /// **'{open} открыто · {overdue} просрочено'**
  String tasksDeptLine(int open, int overdue);

  /// No description provided for @aiTitle.
  ///
  /// In ru, this message translates to:
  /// **'Фокус дня · AI'**
  String get aiTitle;

  /// No description provided for @aiCritical.
  ///
  /// In ru, this message translates to:
  /// **'Критично'**
  String get aiCritical;

  /// No description provided for @aiProblems.
  ///
  /// In ru, this message translates to:
  /// **'Проблемы'**
  String get aiProblems;

  /// No description provided for @aiOpportunities.
  ///
  /// In ru, this message translates to:
  /// **'Возможности'**
  String get aiOpportunities;

  /// No description provided for @aiNone.
  ///
  /// In ru, this message translates to:
  /// **'AI не нашёл проблем.'**
  String get aiNone;

  /// No description provided for @aiChecked.
  ///
  /// In ru, this message translates to:
  /// **'Проверено {when}'**
  String aiChecked(String when);

  /// No description provided for @aiWhatToDo.
  ///
  /// In ru, this message translates to:
  /// **'Что сделать'**
  String get aiWhatToDo;

  /// No description provided for @aiAll.
  ///
  /// In ru, this message translates to:
  /// **'Все находки AI'**
  String get aiAll;

  /// No description provided for @severityCritical.
  ///
  /// In ru, this message translates to:
  /// **'Критично'**
  String get severityCritical;

  /// No description provided for @severityWarning.
  ///
  /// In ru, this message translates to:
  /// **'Внимание'**
  String get severityWarning;

  /// No description provided for @severityOpportunity.
  ///
  /// In ru, this message translates to:
  /// **'Возможность'**
  String get severityOpportunity;

  /// No description provided for @eventsTitle.
  ///
  /// In ru, this message translates to:
  /// **'Последние события'**
  String get eventsTitle;

  /// No description provided for @eventsEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Событий пока нет.'**
  String get eventsEmpty;

  /// No description provided for @eventsUnread.
  ///
  /// In ru, this message translates to:
  /// **'{n} непрочитанных'**
  String eventsUnread(int n);

  /// No description provided for @agoJustNow.
  ///
  /// In ru, this message translates to:
  /// **'только что'**
  String get agoJustNow;

  /// No description provided for @agoMinutes.
  ///
  /// In ru, this message translates to:
  /// **'{n} мин назад'**
  String agoMinutes(int n);

  /// No description provided for @agoHours.
  ///
  /// In ru, this message translates to:
  /// **'{n} ч назад'**
  String agoHours(int n);

  /// No description provided for @agoYesterday.
  ///
  /// In ru, this message translates to:
  /// **'вчера, {time}'**
  String agoYesterday(String time);

  /// No description provided for @rowExecution.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение плана'**
  String get rowExecution;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['ru', 'uz'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'ru':
      return AppLocalizationsRu();
    case 'uz':
      return AppLocalizationsUz();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
