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
  /// **'Консультант'**
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
  /// **'Отделы'**
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

  /// No description provided for @moduleDepartments.
  ///
  /// In ru, this message translates to:
  /// **'Отделы'**
  String get moduleDepartments;

  /// No description provided for @moduleSales.
  ///
  /// In ru, this message translates to:
  /// **'Клиенты'**
  String get moduleSales;

  /// No description provided for @moduleFinance.
  ///
  /// In ru, this message translates to:
  /// **'Финансы'**
  String get moduleFinance;

  /// No description provided for @moduleProduction.
  ///
  /// In ru, this message translates to:
  /// **'Производство'**
  String get moduleProduction;

  /// No description provided for @moduleWarehouse.
  ///
  /// In ru, this message translates to:
  /// **'Склад'**
  String get moduleWarehouse;

  /// No description provided for @moduleRequests.
  ///
  /// In ru, this message translates to:
  /// **'Заявки'**
  String get moduleRequests;

  /// No description provided for @moduleHr.
  ///
  /// In ru, this message translates to:
  /// **'HR'**
  String get moduleHr;

  /// No description provided for @moduleInsights.
  ///
  /// In ru, this message translates to:
  /// **'AI-отчёт'**
  String get moduleInsights;

  /// No description provided for @moduleNotifications.
  ///
  /// In ru, this message translates to:
  /// **'Уведомления'**
  String get moduleNotifications;

  /// No description provided for @moduleSettings.
  ///
  /// In ru, this message translates to:
  /// **'Настройки'**
  String get moduleSettings;

  /// No description provided for @navOpenMenu.
  ///
  /// In ru, this message translates to:
  /// **'Открыть меню разделов'**
  String get navOpenMenu;

  /// No description provided for @navCloseMenu.
  ///
  /// In ru, this message translates to:
  /// **'Закрыть меню'**
  String get navCloseMenu;

  /// No description provided for @navSections.
  ///
  /// In ru, this message translates to:
  /// **'Основные разделы'**
  String get navSections;

  /// No description provided for @searchTitle.
  ///
  /// In ru, this message translates to:
  /// **'Поиск'**
  String get searchTitle;

  /// No description provided for @notificationsUnread.
  ///
  /// In ru, this message translates to:
  /// **'Уведомления: {n} непрочитанных'**
  String notificationsUnread(int n);

  /// No description provided for @homePlanDone.
  ///
  /// In ru, this message translates to:
  /// **'План выполнен на'**
  String get homePlanDone;

  /// No description provided for @homeAttendanceToday.
  ///
  /// In ru, this message translates to:
  /// **'Явка сегодня'**
  String get homeAttendanceToday;

  /// No description provided for @homeAllDeptsOk.
  ///
  /// In ru, this message translates to:
  /// **'все отделы в норме'**
  String get homeAllDeptsOk;

  /// No description provided for @homeDeptsAttention.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} отдел — внимание} few{{n} отдела — внимание} many{{n} отделов — внимание} other{{n} отдела — внимание}}'**
  String homeDeptsAttention(int n);

  /// No description provided for @homeWaiting.
  ///
  /// In ru, this message translates to:
  /// **'Ждут вас'**
  String get homeWaiting;

  /// No description provided for @homeAwaitingApproval.
  ///
  /// In ru, this message translates to:
  /// **'Ждут вашего согласования: {n}'**
  String homeAwaitingApproval(int n);

  /// No description provided for @homePickUp.
  ///
  /// In ru, this message translates to:
  /// **'Можно получить на складе: {n}'**
  String homePickUp(int n);

  /// No description provided for @homePickUpDept.
  ///
  /// In ru, this message translates to:
  /// **'Товар по заявкам отдела ждёт на складе — заберите у кладовщика.'**
  String get homePickUpDept;

  /// No description provided for @homePickUpMine.
  ///
  /// In ru, this message translates to:
  /// **'Товар по вашим заявкам ждёт на складе — заберите у кладовщика.'**
  String get homePickUpMine;

  /// No description provided for @homeAcceptedWeek.
  ///
  /// In ru, this message translates to:
  /// **'Принято на склад за неделю: {sum}'**
  String homeAcceptedWeek(String sum);

  /// No description provided for @filterPeriod7d.
  ///
  /// In ru, this message translates to:
  /// **'7 дней'**
  String get filterPeriod7d;

  /// No description provided for @filterPeriod30d.
  ///
  /// In ru, this message translates to:
  /// **'30 дней'**
  String get filterPeriod30d;

  /// No description provided for @filterPeriodQuarter.
  ///
  /// In ru, this message translates to:
  /// **'Квартал'**
  String get filterPeriodQuarter;

  /// No description provided for @filterPeriodYear.
  ///
  /// In ru, this message translates to:
  /// **'Год'**
  String get filterPeriodYear;

  /// No description provided for @filterAllDepartments.
  ///
  /// In ru, this message translates to:
  /// **'Все отделы'**
  String get filterAllDepartments;

  /// No description provided for @filterDepartmentsN.
  ///
  /// In ru, this message translates to:
  /// **'Отделы: {n}'**
  String filterDepartmentsN(int n);

  /// No description provided for @filterApply.
  ///
  /// In ru, this message translates to:
  /// **'Применить'**
  String get filterApply;

  /// No description provided for @filterReset.
  ///
  /// In ru, this message translates to:
  /// **'Сбросить'**
  String get filterReset;

  /// No description provided for @heroSalesPlan.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение плана продаж'**
  String get heroSalesPlan;

  /// No description provided for @heroVsSameDays.
  ///
  /// In ru, this message translates to:
  /// **'к тем же дням прошлого месяца'**
  String get heroVsSameDays;

  /// No description provided for @heroVsPrevDay.
  ///
  /// In ru, this message translates to:
  /// **'к прошлому рабочему дню'**
  String get heroVsPrevDay;

  /// No description provided for @heroGoal.
  ///
  /// In ru, this message translates to:
  /// **'цель'**
  String get heroGoal;

  /// No description provided for @heroNorm.
  ///
  /// In ru, this message translates to:
  /// **'норма'**
  String get heroNorm;

  /// No description provided for @unitPp.
  ///
  /// In ru, this message translates to:
  /// **'п.п.'**
  String get unitPp;

  /// No description provided for @statForecast.
  ///
  /// In ru, this message translates to:
  /// **'Прогноз'**
  String get statForecast;

  /// No description provided for @statToMonthEnd.
  ///
  /// In ru, this message translates to:
  /// **'к концу месяца'**
  String get statToMonthEnd;

  /// No description provided for @statOverdueN.
  ///
  /// In ru, this message translates to:
  /// **'{n} просрочено'**
  String statOverdueN(int n);

  /// No description provided for @statDueWeekN.
  ///
  /// In ru, this message translates to:
  /// **'{n} срок на неделе'**
  String statDueWeekN(int n);

  /// No description provided for @statAttendanceMonth.
  ///
  /// In ru, this message translates to:
  /// **'Явка за месяц'**
  String get statAttendanceMonth;

  /// No description provided for @statPerMonth.
  ///
  /// In ru, this message translates to:
  /// **'за месяц'**
  String get statPerMonth;

  /// No description provided for @quickTask.
  ///
  /// In ru, this message translates to:
  /// **'Задача'**
  String get quickTask;

  /// No description provided for @quickAskAi.
  ///
  /// In ru, this message translates to:
  /// **'Спросить AI'**
  String get quickAskAi;

  /// No description provided for @quickExport.
  ///
  /// In ru, this message translates to:
  /// **'Экспорт'**
  String get quickExport;

  /// No description provided for @quickReportAi.
  ///
  /// In ru, this message translates to:
  /// **'Отчёт AI'**
  String get quickReportAi;

  /// No description provided for @focusTitle.
  ///
  /// In ru, this message translates to:
  /// **'Фокус дня'**
  String get focusTitle;

  /// No description provided for @focusDepartments.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} отдел} few{{n} отдела} many{{n} отделов} other{{n} отдела}}'**
  String focusDepartments(int n);

  /// No description provided for @focusActiveTasks.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} активная задача} few{{n} активные задачи} many{{n} активных задач} other{{n} активной задачи}}'**
  String focusActiveTasks(int n);

  /// No description provided for @focusFallback.
  ///
  /// In ru, this message translates to:
  /// **'Находки AI-консультанта'**
  String get focusFallback;

  /// No description provided for @focusCalm.
  ///
  /// In ru, this message translates to:
  /// **'Сегодня всё спокойно'**
  String get focusCalm;

  /// No description provided for @focusMore.
  ///
  /// In ru, this message translates to:
  /// **'Ещё {n}'**
  String focusMore(int n);

  /// No description provided for @focusAskQuestion.
  ///
  /// In ru, this message translates to:
  /// **'Разбери находки «Фокуса дня» и подскажи, что сделать сегодня: {list}'**
  String focusAskQuestion(String list);

  /// No description provided for @focusAskEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Что сегодня стоит проверить в компании?'**
  String get focusAskEmpty;

  /// No description provided for @homeAskQuestion.
  ///
  /// In ru, this message translates to:
  /// **'Разбери показатели компании за период «{period}»{scope}: что сейчас требует внимания и что сделать в первую очередь?'**
  String homeAskQuestion(String period, String scope);

  /// No description provided for @catFinance.
  ///
  /// In ru, this message translates to:
  /// **'Финансы'**
  String get catFinance;

  /// No description provided for @catSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get catSales;

  /// No description provided for @catMarketing.
  ///
  /// In ru, this message translates to:
  /// **'Маркетинг'**
  String get catMarketing;

  /// No description provided for @catHr.
  ///
  /// In ru, this message translates to:
  /// **'HR'**
  String get catHr;

  /// No description provided for @catProduction.
  ///
  /// In ru, this message translates to:
  /// **'Производство'**
  String get catProduction;

  /// No description provided for @catSupply.
  ///
  /// In ru, this message translates to:
  /// **'Снабжение'**
  String get catSupply;

  /// No description provided for @dynTitle.
  ///
  /// In ru, this message translates to:
  /// **'Динамика'**
  String get dynTitle;

  /// No description provided for @dynMore.
  ///
  /// In ru, this message translates to:
  /// **'Подробнее'**
  String get dynMore;

  /// No description provided for @dynSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get dynSales;

  /// No description provided for @dynRevenue.
  ///
  /// In ru, this message translates to:
  /// **'Выручка'**
  String get dynRevenue;

  /// No description provided for @dynAttendance.
  ///
  /// In ru, this message translates to:
  /// **'Явка'**
  String get dynAttendance;

  /// No description provided for @dynAverage.
  ///
  /// In ru, this message translates to:
  /// **'Среднее'**
  String get dynAverage;

  /// No description provided for @dynBest.
  ///
  /// In ru, this message translates to:
  /// **'Лучшая неделя'**
  String get dynBest;

  /// No description provided for @dynAbove.
  ///
  /// In ru, this message translates to:
  /// **'Выше цели'**
  String get dynAbove;

  /// No description provided for @dynOfTotal.
  ///
  /// In ru, this message translates to:
  /// **'{n} из {total}'**
  String dynOfTotal(int n, int total);

  /// No description provided for @dynForWeeks.
  ///
  /// In ru, this message translates to:
  /// **'За {n} нед.'**
  String dynForWeeks(int n);

  /// No description provided for @dynGoal.
  ///
  /// In ru, this message translates to:
  /// **'цель {n}%'**
  String dynGoal(String n);

  /// No description provided for @dynWeekValue.
  ///
  /// In ru, this message translates to:
  /// **'Нед. {w} · {value}'**
  String dynWeekValue(int w, String value);

  /// No description provided for @dynCaption.
  ///
  /// In ru, this message translates to:
  /// **'Нед. {w} — текущая, данные за {days}'**
  String dynCaption(int w, String days);

  /// No description provided for @daysN.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} день} few{{n} дня} many{{n} дней} other{{n} дня}}'**
  String daysN(int n);

  /// No description provided for @dynEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Нет данных за период'**
  String get dynEmpty;

  /// No description provided for @dynSemantics.
  ///
  /// In ru, this message translates to:
  /// **'{name} по неделям: среднее {avg}, лучшая неделя {best}'**
  String dynSemantics(String name, String avg, String best);

  /// No description provided for @tasksAllN.
  ///
  /// In ru, this message translates to:
  /// **'Все {n}'**
  String tasksAllN(String n);

  /// No description provided for @tasksNone.
  ///
  /// In ru, this message translates to:
  /// **'Нет задач за период'**
  String get tasksNone;

  /// No description provided for @tasksDonePct.
  ///
  /// In ru, this message translates to:
  /// **'выполнено'**
  String get tasksDonePct;

  /// No description provided for @tasksDone.
  ///
  /// In ru, this message translates to:
  /// **'Выполнено'**
  String get tasksDone;

  /// No description provided for @tasksInWork.
  ///
  /// In ru, this message translates to:
  /// **'В работе'**
  String get tasksInWork;

  /// No description provided for @tasksOnReview.
  ///
  /// In ru, this message translates to:
  /// **'На проверке'**
  String get tasksOnReview;

  /// No description provided for @tasksOverdueLabel.
  ///
  /// In ru, this message translates to:
  /// **'Просрочено'**
  String get tasksOverdueLabel;

  /// No description provided for @tasksOverdueBanner.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} задача просрочена} few{{n} задачи просрочены} many{{n} задач просрочено} other{{n} задачи просрочено}}'**
  String tasksOverdueBanner(int n);

  /// No description provided for @tasksOverdueIn.
  ///
  /// In ru, this message translates to:
  /// **' — {n} в отделе «{name}»'**
  String tasksOverdueIn(int n, String name);

  /// No description provided for @deptOkN.
  ///
  /// In ru, this message translates to:
  /// **'{n} в норме'**
  String deptOkN(int n);

  /// No description provided for @deptAttentionN.
  ///
  /// In ru, this message translates to:
  /// **'{n} внимание'**
  String deptAttentionN(int n);

  /// No description provided for @deptNone.
  ///
  /// In ru, this message translates to:
  /// **'Нет данных по отделам'**
  String get deptNone;

  /// No description provided for @deptSalesMetric.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение плана месяца'**
  String get deptSalesMetric;

  /// No description provided for @deptHrMetric.
  ///
  /// In ru, this message translates to:
  /// **'Явка с начала месяца'**
  String get deptHrMetric;

  /// No description provided for @deptWeekTrend.
  ///
  /// In ru, this message translates to:
  /// **'{n} нед.'**
  String deptWeekTrend(String n);

  /// No description provided for @statusAttention.
  ///
  /// In ru, this message translates to:
  /// **'Внимание'**
  String get statusAttention;

  /// No description provided for @statusOk.
  ///
  /// In ru, this message translates to:
  /// **'В норме'**
  String get statusOk;

  /// No description provided for @reportTitle.
  ///
  /// In ru, this message translates to:
  /// **'Еженедельный AI-отчёт'**
  String get reportTitle;

  /// No description provided for @reportLine.
  ///
  /// In ru, this message translates to:
  /// **'{risks} и {recs} по отделам'**
  String reportLine(String risks, String recs);

  /// No description provided for @risksN.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} риск} few{{n} риска} many{{n} рисков} other{{n} риска}}'**
  String risksN(int n);

  /// No description provided for @recsN.
  ///
  /// In ru, this message translates to:
  /// **'{n, plural, one{{n} рекомендация} few{{n} рекомендации} many{{n} рекомендаций} other{{n} рекомендации}}'**
  String recsN(int n);

  /// No description provided for @dataUpdatedNow.
  ///
  /// In ru, this message translates to:
  /// **'Данные обновлены только что'**
  String get dataUpdatedNow;

  /// No description provided for @dataUpdatedMin.
  ///
  /// In ru, this message translates to:
  /// **'Данные обновлены {n} мин назад'**
  String dataUpdatedMin(int n);

  /// No description provided for @dataPullHint.
  ///
  /// In ru, this message translates to:
  /// **'потяните, чтобы обновить'**
  String get dataPullHint;

  /// No description provided for @dataFailed.
  ///
  /// In ru, this message translates to:
  /// **'Не удалось обновить'**
  String get dataFailed;

  /// No description provided for @dataUpdating.
  ///
  /// In ru, this message translates to:
  /// **'Обновляем…'**
  String get dataUpdating;

  /// No description provided for @offlineSince.
  ///
  /// In ru, this message translates to:
  /// **'Нет соединения · данные на {time}'**
  String offlineSince(String time);

  /// No description provided for @exportTitle.
  ///
  /// In ru, this message translates to:
  /// **'Экспорт обзора'**
  String get exportTitle;

  /// No description provided for @exportCsv.
  ///
  /// In ru, this message translates to:
  /// **'Excel (CSV)'**
  String get exportCsv;

  /// No description provided for @exportCsvHint.
  ///
  /// In ru, this message translates to:
  /// **'Показатели, задачи и отделы таблицей'**
  String get exportCsvHint;

  /// No description provided for @homeMoreSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи месяца'**
  String get homeMoreSales;

  /// No description provided for @homeMoreFinance.
  ///
  /// In ru, this message translates to:
  /// **'Финансы 1С'**
  String get homeMoreFinance;

  /// No description provided for @homeKeyMetric.
  ///
  /// In ru, this message translates to:
  /// **'Ключевая метрика'**
  String get homeKeyMetric;

  /// No description provided for @homeAgentsN.
  ///
  /// In ru, this message translates to:
  /// **'{n} ТП в работе'**
  String homeAgentsN(int n);

  /// No description provided for @homeSearchHint.
  ///
  /// In ru, this message translates to:
  /// **'Разделы, сотрудники, клиенты'**
  String get homeSearchHint;

  /// No description provided for @profileOpen.
  ///
  /// In ru, this message translates to:
  /// **'Открыть профиль'**
  String get profileOpen;

  /// No description provided for @myDayTitle.
  ///
  /// In ru, this message translates to:
  /// **'Мой день'**
  String get myDayTitle;

  /// No description provided for @myDayShift.
  ///
  /// In ru, this message translates to:
  /// **'Смена {from}–{to}'**
  String myDayShift(String from, String to);

  /// No description provided for @myDayArrival.
  ///
  /// In ru, this message translates to:
  /// **'Пришли'**
  String get myDayArrival;

  /// No description provided for @myDayDeparture.
  ///
  /// In ru, this message translates to:
  /// **'Ушли'**
  String get myDayDeparture;

  /// No description provided for @myDayWorked.
  ///
  /// In ru, this message translates to:
  /// **'Отработано'**
  String get myDayWorked;

  /// No description provided for @myDayLateWeek.
  ///
  /// In ru, this message translates to:
  /// **'Опозданий за неделю'**
  String get myDayLateWeek;

  /// No description provided for @myDayHoursMinutes.
  ///
  /// In ru, this message translates to:
  /// **'{h} ч {m} мин'**
  String myDayHoursMinutes(int h, int m);

  /// No description provided for @dayStatus_not_started.
  ///
  /// In ru, this message translates to:
  /// **'Рабочий день не начат'**
  String get dayStatus_not_started;

  /// No description provided for @dayStatus_at_work.
  ///
  /// In ru, this message translates to:
  /// **'На работе'**
  String get dayStatus_at_work;

  /// No description provided for @dayStatus_on_break.
  ///
  /// In ru, this message translates to:
  /// **'На перерыве'**
  String get dayStatus_on_break;

  /// No description provided for @dayStatus_out.
  ///
  /// In ru, this message translates to:
  /// **'Вышли'**
  String get dayStatus_out;

  /// No description provided for @dayStatus_finished.
  ///
  /// In ru, this message translates to:
  /// **'Рабочий день завершён'**
  String get dayStatus_finished;

  /// No description provided for @dayStatus_day_off.
  ///
  /// In ru, this message translates to:
  /// **'Выходной'**
  String get dayStatus_day_off;

  /// No description provided for @dayStatus_on_leave.
  ///
  /// In ru, this message translates to:
  /// **'Отсутствие по документу'**
  String get dayStatus_on_leave;

  /// No description provided for @dayStatus_absent.
  ///
  /// In ru, this message translates to:
  /// **'Нет прихода'**
  String get dayStatus_absent;

  /// No description provided for @dayStatus_other.
  ///
  /// In ru, this message translates to:
  /// **'Нет данных'**
  String get dayStatus_other;

  /// No description provided for @dayLate.
  ///
  /// In ru, this message translates to:
  /// **'Опоздание {n} мин'**
  String dayLate(int n);

  /// No description provided for @myTasksTitle.
  ///
  /// In ru, this message translates to:
  /// **'Мои задачи'**
  String get myTasksTitle;

  /// No description provided for @myTasksNew.
  ///
  /// In ru, this message translates to:
  /// **'Новые'**
  String get myTasksNew;

  /// No description provided for @myTasksWaiting.
  ///
  /// In ru, this message translates to:
  /// **'Ждут'**
  String get myTasksWaiting;

  /// No description provided for @prefsTitle.
  ///
  /// In ru, this message translates to:
  /// **'Что присылать'**
  String get prefsTitle;

  /// No description provided for @prefsHint.
  ///
  /// In ru, this message translates to:
  /// **'Отключённые разделы исчезают из уведомлений и счётчиков на всех устройствах.'**
  String get prefsHint;

  /// No description provided for @prefCat_tasks.
  ///
  /// In ru, this message translates to:
  /// **'Задачи'**
  String get prefCat_tasks;

  /// No description provided for @prefCat_ai.
  ///
  /// In ru, this message translates to:
  /// **'AI'**
  String get prefCat_ai;

  /// No description provided for @prefCat_hr.
  ///
  /// In ru, this message translates to:
  /// **'HR'**
  String get prefCat_hr;

  /// No description provided for @prefCat_sales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get prefCat_sales;

  /// No description provided for @prefCat_system.
  ///
  /// In ru, this message translates to:
  /// **'Система'**
  String get prefCat_system;

  /// No description provided for @prefsQueued.
  ///
  /// In ru, this message translates to:
  /// **'Сохранится, когда появится сеть'**
  String get prefsQueued;

  /// No description provided for @prefsRejected.
  ///
  /// In ru, this message translates to:
  /// **'Не удалось сохранить: {error}'**
  String prefsRejected(String error);

  /// No description provided for @telegramTitle.
  ///
  /// In ru, this message translates to:
  /// **'Telegram'**
  String get telegramTitle;

  /// No description provided for @telegramNone.
  ///
  /// In ru, this message translates to:
  /// **'Telegram не привязан. Привяжите аккаунт, чтобы AI-агент отвечал вам в Telegram с вашими правами.'**
  String get telegramNone;

  /// No description provided for @telegramFull.
  ///
  /// In ru, this message translates to:
  /// **'полный доступ'**
  String get telegramFull;

  /// No description provided for @telegramLimited.
  ///
  /// In ru, this message translates to:
  /// **'только отправленные данные'**
  String get telegramLimited;

  /// No description provided for @telegramLink.
  ///
  /// In ru, this message translates to:
  /// **'Привязать Telegram'**
  String get telegramLink;

  /// No description provided for @telegramUnlink.
  ///
  /// In ru, this message translates to:
  /// **'Отвязать'**
  String get telegramUnlink;

  /// No description provided for @telegramUnlinkConfirm.
  ///
  /// In ru, this message translates to:
  /// **'Агент перестанет отвечать вам с вашими правами до новой привязки.'**
  String get telegramUnlinkConfirm;

  /// No description provided for @telegramCodeTitle.
  ///
  /// In ru, this message translates to:
  /// **'Код привязки'**
  String get telegramCodeTitle;

  /// No description provided for @telegramCodeHint.
  ///
  /// In ru, this message translates to:
  /// **'Отправьте этот код в Telegram: {to}. Код действует 15 минут.'**
  String telegramCodeHint(String to);

  /// No description provided for @actionCopy.
  ///
  /// In ru, this message translates to:
  /// **'Скопировать'**
  String get actionCopy;

  /// No description provided for @copied.
  ///
  /// In ru, this message translates to:
  /// **'Скопировано'**
  String get copied;

  /// No description provided for @securityTitle.
  ///
  /// In ru, this message translates to:
  /// **'Безопасность'**
  String get securityTitle;

  /// No description provided for @securityPassword.
  ///
  /// In ru, this message translates to:
  /// **'Пароль'**
  String get securityPassword;

  /// No description provided for @securityPasswordHint.
  ///
  /// In ru, this message translates to:
  /// **'Пароль меняет администратор OneBase'**
  String get securityPasswordHint;

  /// No description provided for @securitySession.
  ///
  /// In ru, this message translates to:
  /// **'Сеанс'**
  String get securitySession;

  /// No description provided for @securitySessionHint.
  ///
  /// In ru, this message translates to:
  /// **'Выход закрывает сеанс только на этом телефоне'**
  String get securitySessionHint;

  /// No description provided for @securityStorage.
  ///
  /// In ru, this message translates to:
  /// **'Данные на телефоне'**
  String get securityStorage;

  /// No description provided for @securityStorageHint.
  ///
  /// In ru, this message translates to:
  /// **'Токен — в защищённом хранилище, сводки — в кэше приложения ({n} записей)'**
  String securityStorageHint(int n);

  /// No description provided for @securityClearCache.
  ///
  /// In ru, this message translates to:
  /// **'Очистить кэш'**
  String get securityClearCache;

  /// No description provided for @cacheCleared.
  ///
  /// In ru, this message translates to:
  /// **'Кэш очищен'**
  String get cacheCleared;

  /// No description provided for @syncTitle.
  ///
  /// In ru, this message translates to:
  /// **'Синхронизация'**
  String get syncTitle;

  /// No description provided for @syncPending.
  ///
  /// In ru, this message translates to:
  /// **'Ждут отправки: {n}'**
  String syncPending(int n);

  /// No description provided for @syncAllSent.
  ///
  /// In ru, this message translates to:
  /// **'Все изменения отправлены'**
  String get syncAllSent;

  /// No description provided for @syncFailed.
  ///
  /// In ru, this message translates to:
  /// **'Сервер отказал: {error}'**
  String syncFailed(String error);

  /// No description provided for @syncNow.
  ///
  /// In ru, this message translates to:
  /// **'Отправить сейчас'**
  String get syncNow;

  /// No description provided for @profileRoles.
  ///
  /// In ru, this message translates to:
  /// **'Роли'**
  String get profileRoles;

  /// No description provided for @profileDepartment.
  ///
  /// In ru, this message translates to:
  /// **'Отдел'**
  String get profileDepartment;

  /// No description provided for @organization1c.
  ///
  /// In ru, this message translates to:
  /// **'Организация 1С'**
  String get organization1c;

  /// No description provided for @langRu.
  ///
  /// In ru, this message translates to:
  /// **'Русский'**
  String get langRu;

  /// No description provided for @langUz.
  ///
  /// In ru, this message translates to:
  /// **'O\'zbekcha'**
  String get langUz;

  /// No description provided for @langSystem.
  ///
  /// In ru, this message translates to:
  /// **'Как в системе'**
  String get langSystem;

  /// No description provided for @anSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get anSales;

  /// No description provided for @anPlanCard.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение плана'**
  String get anPlanCard;

  /// No description provided for @anPlanNone.
  ///
  /// In ru, this message translates to:
  /// **'Плана нет'**
  String get anPlanNone;

  /// No description provided for @anPlanOnTrack.
  ///
  /// In ru, this message translates to:
  /// **'Идём по плану'**
  String get anPlanOnTrack;

  /// No description provided for @anPlanClose.
  ///
  /// In ru, this message translates to:
  /// **'Близко к темпу'**
  String get anPlanClose;

  /// No description provided for @anPlanBehind.
  ///
  /// In ru, this message translates to:
  /// **'Отстаём от темпа'**
  String get anPlanBehind;

  /// No description provided for @anPlanDone.
  ///
  /// In ru, this message translates to:
  /// **'План выполнен ✓'**
  String get anPlanDone;

  /// No description provided for @anPlanAlmost.
  ///
  /// In ru, this message translates to:
  /// **'Почти выполнен'**
  String get anPlanAlmost;

  /// No description provided for @anPlanFailed.
  ///
  /// In ru, this message translates to:
  /// **'План не выполнен'**
  String get anPlanFailed;

  /// No description provided for @anFactFor.
  ///
  /// In ru, this message translates to:
  /// **'факт за {month}'**
  String anFactFor(String month);

  /// No description provided for @anPace.
  ///
  /// In ru, this message translates to:
  /// **'Текущий темп'**
  String get anPace;

  /// No description provided for @anPerDay.
  ///
  /// In ru, this message translates to:
  /// **'{value}/день'**
  String anPerDay(String value);

  /// No description provided for @anRequired.
  ///
  /// In ru, this message translates to:
  /// **'Нужно в день'**
  String get anRequired;

  /// No description provided for @anForecast.
  ///
  /// In ru, this message translates to:
  /// **'Прогноз на конец месяца'**
  String get anForecast;

  /// No description provided for @anShouldBe.
  ///
  /// In ru, this message translates to:
  /// **'должно быть'**
  String get anShouldBe;

  /// No description provided for @anPlanOf.
  ///
  /// In ru, this message translates to:
  /// **'{fact} из {plan}'**
  String anPlanOf(String fact, String plan);

  /// No description provided for @anConversion.
  ///
  /// In ru, this message translates to:
  /// **'Конверсия визита'**
  String get anConversion;

  /// No description provided for @anRevenuePerOutlet.
  ///
  /// In ru, this message translates to:
  /// **'Выручка на ТТ'**
  String get anRevenuePerOutlet;

  /// No description provided for @anAkbPerAgent.
  ///
  /// In ru, this message translates to:
  /// **'АКБ на агента'**
  String get anAkbPerAgent;

  /// No description provided for @anTarget.
  ///
  /// In ru, this message translates to:
  /// **'цель {value}'**
  String anTarget(String value);

  /// No description provided for @anOfTarget.
  ///
  /// In ru, this message translates to:
  /// **'{pct} цели'**
  String anOfTarget(String pct);

  /// No description provided for @anVisitsNoOrder.
  ///
  /// In ru, this message translates to:
  /// **'Визиты без заказа'**
  String get anVisitsNoOrder;

  /// No description provided for @anVisitsNoOrderOf.
  ///
  /// In ru, this message translates to:
  /// **'из {n} визитов ТП'**
  String anVisitsNoOrderOf(String n);

  /// No description provided for @anAgentsAttention.
  ///
  /// In ru, this message translates to:
  /// **'{bad} из {all} ТП — внимание'**
  String anAgentsAttention(int bad, int all);

  /// No description provided for @anCritical.
  ///
  /// In ru, this message translates to:
  /// **'Критично'**
  String get anCritical;

  /// No description provided for @anRisk.
  ///
  /// In ru, this message translates to:
  /// **'Риск'**
  String get anRisk;

  /// No description provided for @anNormal.
  ///
  /// In ru, this message translates to:
  /// **'В норме'**
  String get anNormal;

  /// No description provided for @anRepublic.
  ///
  /// In ru, this message translates to:
  /// **'Республика'**
  String get anRepublic;

  /// No description provided for @anMyRegions.
  ///
  /// In ru, this message translates to:
  /// **'Мои регионы'**
  String get anMyRegions;

  /// No description provided for @anDirections.
  ///
  /// In ru, this message translates to:
  /// **'Направления'**
  String get anDirections;

  /// No description provided for @anRegions.
  ///
  /// In ru, this message translates to:
  /// **'Регионы'**
  String get anRegions;

  /// No description provided for @anAgents.
  ///
  /// In ru, this message translates to:
  /// **'Агенты'**
  String get anAgents;

  /// No description provided for @anPlanKg.
  ///
  /// In ru, this message translates to:
  /// **'План, кг'**
  String get anPlanKg;

  /// No description provided for @anFact.
  ///
  /// In ru, this message translates to:
  /// **'Факт'**
  String get anFact;

  /// No description provided for @anForecastShort.
  ///
  /// In ru, this message translates to:
  /// **'Прогноз'**
  String get anForecastShort;

  /// No description provided for @anStrike.
  ///
  /// In ru, this message translates to:
  /// **'Страйк'**
  String get anStrike;

  /// No description provided for @anExecution.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение'**
  String get anExecution;

  /// No description provided for @anSameDays.
  ///
  /// In ru, this message translates to:
  /// **'К прошлому месяцу за те же дни'**
  String get anSameDays;

  /// No description provided for @anByDays.
  ///
  /// In ru, this message translates to:
  /// **'Продажи по дням'**
  String get anByDays;

  /// No description provided for @anCategories.
  ///
  /// In ru, this message translates to:
  /// **'Категории'**
  String get anCategories;

  /// No description provided for @anSkuSold.
  ///
  /// In ru, this message translates to:
  /// **'продаётся {sold} из {total} SKU'**
  String anSkuSold(int sold, int total);

  /// No description provided for @anWeightShare.
  ///
  /// In ru, this message translates to:
  /// **'{pct} от общего веса'**
  String anWeightShare(String pct);

  /// No description provided for @anSilent.
  ///
  /// In ru, this message translates to:
  /// **'молчат {n}'**
  String anSilent(int n);

  /// No description provided for @anLost.
  ///
  /// In ru, this message translates to:
  /// **'пропало {n}'**
  String anLost(int n);

  /// No description provided for @anTeam.
  ///
  /// In ru, this message translates to:
  /// **'Команда'**
  String get anTeam;

  /// No description provided for @anPlanFactMonths.
  ///
  /// In ru, this message translates to:
  /// **'План и факт по месяцам'**
  String get anPlanFactMonths;

  /// No description provided for @anRevenueMix.
  ///
  /// In ru, this message translates to:
  /// **'Из чего складывается выручка'**
  String get anRevenueMix;

  /// No description provided for @anSupervisor.
  ///
  /// In ru, this message translates to:
  /// **'СВР: {name}'**
  String anSupervisor(String name);

  /// No description provided for @anDealer.
  ///
  /// In ru, this message translates to:
  /// **'дилер: {name}'**
  String anDealer(String name);

  /// No description provided for @anVacancy.
  ///
  /// In ru, this message translates to:
  /// **'вакансия'**
  String get anVacancy;

  /// No description provided for @anNoRemarks.
  ///
  /// In ru, this message translates to:
  /// **'Замечаний нет'**
  String get anNoRemarks;

  /// No description provided for @anSearchAgents.
  ///
  /// In ru, this message translates to:
  /// **'Имя, ID или регион'**
  String get anSearchAgents;

  /// No description provided for @anFilterAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get anFilterAll;

  /// No description provided for @anFilterPlanOk.
  ///
  /// In ru, this message translates to:
  /// **'Выполняют план'**
  String get anFilterPlanOk;

  /// No description provided for @anFilterBelow.
  ///
  /// In ru, this message translates to:
  /// **'Ниже плана'**
  String get anFilterBelow;

  /// No description provided for @anFilterNoPlan.
  ///
  /// In ru, this message translates to:
  /// **'Без плана'**
  String get anFilterNoPlan;

  /// No description provided for @anFilterRemarks.
  ///
  /// In ru, this message translates to:
  /// **'С замечаниями'**
  String get anFilterRemarks;

  /// No description provided for @anFilterVacancy.
  ///
  /// In ru, this message translates to:
  /// **'Вакансии'**
  String get anFilterVacancy;

  /// No description provided for @anSortExecution.
  ///
  /// In ru, this message translates to:
  /// **'По выполнению'**
  String get anSortExecution;

  /// No description provided for @anSortFact.
  ///
  /// In ru, this message translates to:
  /// **'По факту, кг'**
  String get anSortFact;

  /// No description provided for @anSortRevenue.
  ///
  /// In ru, this message translates to:
  /// **'По выручке'**
  String get anSortRevenue;

  /// No description provided for @anSortName.
  ///
  /// In ru, this message translates to:
  /// **'По имени'**
  String get anSortName;

  /// No description provided for @anShown.
  ///
  /// In ru, this message translates to:
  /// **'Показано {n} из {all} ТП'**
  String anShown(int n, int all);

  /// No description provided for @anAgentNoPlan.
  ///
  /// In ru, this message translates to:
  /// **'Факт {fact} · плана ТП в Linko на месяц нет'**
  String anAgentNoPlan(String fact);

  /// No description provided for @anRevenue.
  ///
  /// In ru, this message translates to:
  /// **'Выручка'**
  String get anRevenue;

  /// No description provided for @anVisits.
  ///
  /// In ru, this message translates to:
  /// **'Визиты'**
  String get anVisits;

  /// No description provided for @anWithOrder.
  ///
  /// In ru, this message translates to:
  /// **'{n} с заказом'**
  String anWithOrder(int n);

  /// No description provided for @anOrders.
  ///
  /// In ru, this message translates to:
  /// **'Заказы'**
  String get anOrders;

  /// No description provided for @anTempo.
  ///
  /// In ru, this message translates to:
  /// **'Темп'**
  String get anTempo;

  /// No description provided for @anCategoriesN.
  ///
  /// In ru, this message translates to:
  /// **'{n} категорий'**
  String anCategoriesN(int n);

  /// No description provided for @anMedian.
  ///
  /// In ru, this message translates to:
  /// **'медиана региона {value}'**
  String anMedian(String value);

  /// No description provided for @anSumPerVisit.
  ///
  /// In ru, this message translates to:
  /// **'Сум с визита'**
  String get anSumPerVisit;

  /// No description provided for @anAvgCheck.
  ///
  /// In ru, this message translates to:
  /// **'Средний чек'**
  String get anAvgCheck;

  /// No description provided for @anKgPerOutlet.
  ///
  /// In ru, this message translates to:
  /// **'Вес на точку'**
  String get anKgPerOutlet;

  /// No description provided for @anFindings.
  ///
  /// In ru, this message translates to:
  /// **'Что нашлось'**
  String get anFindings;

  /// No description provided for @anStores.
  ///
  /// In ru, this message translates to:
  /// **'Точки месяца'**
  String get anStores;

  /// No description provided for @anSilentMarkets.
  ///
  /// In ru, this message translates to:
  /// **'Ещё не купили'**
  String get anSilentMarkets;

  /// No description provided for @anNewMarkets.
  ///
  /// In ru, this message translates to:
  /// **'Новые точки'**
  String get anNewMarkets;

  /// No description provided for @anPrevMonth.
  ///
  /// In ru, this message translates to:
  /// **'прошлый месяц {value}'**
  String anPrevMonth(String value);

  /// No description provided for @anStore.
  ///
  /// In ru, this message translates to:
  /// **'Точка'**
  String get anStore;

  /// No description provided for @anStoreAgents.
  ///
  /// In ru, this message translates to:
  /// **'Продавали'**
  String get anStoreAgents;

  /// No description provided for @anProducts.
  ///
  /// In ru, this message translates to:
  /// **'Артикулы'**
  String get anProducts;

  /// No description provided for @anCompare.
  ///
  /// In ru, this message translates to:
  /// **'Сравнение периодов'**
  String get anCompare;

  /// No description provided for @anCompareWith.
  ///
  /// In ru, this message translates to:
  /// **'к {label}'**
  String anCompareWith(String label);

  /// No description provided for @anPeriodWeek.
  ///
  /// In ru, this message translates to:
  /// **'Неделя'**
  String get anPeriodWeek;

  /// No description provided for @anPeriodMonth.
  ///
  /// In ru, this message translates to:
  /// **'Месяц'**
  String get anPeriodMonth;

  /// No description provided for @anPeriodQuarter.
  ///
  /// In ru, this message translates to:
  /// **'Квартал'**
  String get anPeriodQuarter;

  /// No description provided for @anPeriodYear.
  ///
  /// In ru, this message translates to:
  /// **'Год'**
  String get anPeriodYear;

  /// No description provided for @anModSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи'**
  String get anModSales;

  /// No description provided for @anModFinance.
  ///
  /// In ru, this message translates to:
  /// **'Финансы'**
  String get anModFinance;

  /// No description provided for @anModProduction.
  ///
  /// In ru, this message translates to:
  /// **'Производство'**
  String get anModProduction;

  /// No description provided for @anModHr.
  ///
  /// In ru, this message translates to:
  /// **'Персонал'**
  String get anModHr;

  /// No description provided for @anNoData.
  ///
  /// In ru, this message translates to:
  /// **'нет данных'**
  String get anNoData;

  /// No description provided for @anIncomplete.
  ///
  /// In ru, this message translates to:
  /// **'Данные неполные'**
  String get anIncomplete;

  /// No description provided for @anSlow.
  ///
  /// In ru, this message translates to:
  /// **'Считаем — до нескольких секунд'**
  String get anSlow;

  /// No description provided for @anProductionToday.
  ///
  /// In ru, this message translates to:
  /// **'Выпуск сегодня'**
  String get anProductionToday;

  /// No description provided for @anCompletion.
  ///
  /// In ru, this message translates to:
  /// **'Выполнение плана'**
  String get anCompletion;

  /// No description provided for @anDefect.
  ///
  /// In ru, this message translates to:
  /// **'Брак'**
  String get anDefect;

  /// No description provided for @anActiveOrders.
  ///
  /// In ru, this message translates to:
  /// **'Заданий в работе'**
  String get anActiveOrders;

  /// No description provided for @anFindingsTitle.
  ///
  /// In ru, this message translates to:
  /// **'Находки Аналитика'**
  String get anFindingsTitle;

  /// No description provided for @anFindingsLine.
  ///
  /// In ru, this message translates to:
  /// **'{critical} критично · {warning} внимание · {info} к сведению'**
  String anFindingsLine(int critical, int warning, int info);

  /// No description provided for @anMonthPick.
  ///
  /// In ru, this message translates to:
  /// **'Месяц'**
  String get anMonthPick;

  /// No description provided for @anOpenAll.
  ///
  /// In ru, this message translates to:
  /// **'Открыть'**
  String get anOpenAll;

  /// No description provided for @anScopeNote.
  ///
  /// In ru, this message translates to:
  /// **'Ваш охват продаж: {scope}'**
  String anScopeNote(String scope);

  /// No description provided for @crmSearch.
  ///
  /// In ru, this message translates to:
  /// **'Название, адрес или ID точки'**
  String get crmSearch;

  /// No description provided for @crmAttAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get crmAttAll;

  /// No description provided for @crmAttDecline.
  ///
  /// In ru, this message translates to:
  /// **'Падение продаж'**
  String get crmAttDecline;

  /// No description provided for @crmAttLost.
  ///
  /// In ru, this message translates to:
  /// **'Перестали покупать'**
  String get crmAttLost;

  /// No description provided for @crmAttNotVisited.
  ///
  /// In ru, this message translates to:
  /// **'Давно не были'**
  String get crmAttNotVisited;

  /// No description provided for @crmAttNew.
  ///
  /// In ru, this message translates to:
  /// **'Без покупок'**
  String get crmAttNew;

  /// No description provided for @crmStatusActive.
  ///
  /// In ru, this message translates to:
  /// **'Активная'**
  String get crmStatusActive;

  /// No description provided for @crmStatusProblem.
  ///
  /// In ru, this message translates to:
  /// **'Проблемная'**
  String get crmStatusProblem;

  /// No description provided for @crmStatusInactive.
  ///
  /// In ru, this message translates to:
  /// **'Неактивная'**
  String get crmStatusInactive;

  /// No description provided for @crmPriorityLow.
  ///
  /// In ru, this message translates to:
  /// **'Низкий'**
  String get crmPriorityLow;

  /// No description provided for @crmPriorityMedium.
  ///
  /// In ru, this message translates to:
  /// **'Средний'**
  String get crmPriorityMedium;

  /// No description provided for @crmPriorityHigh.
  ///
  /// In ru, this message translates to:
  /// **'Высокий'**
  String get crmPriorityHigh;

  /// No description provided for @crmPriorityUrgent.
  ///
  /// In ru, this message translates to:
  /// **'Срочно'**
  String get crmPriorityUrgent;

  /// No description provided for @crmSortSales.
  ///
  /// In ru, this message translates to:
  /// **'По продажам за 30 дней'**
  String get crmSortSales;

  /// No description provided for @crmSortTrend.
  ///
  /// In ru, this message translates to:
  /// **'Сильнее упали'**
  String get crmSortTrend;

  /// No description provided for @crmSortLastVisit.
  ///
  /// In ru, this message translates to:
  /// **'Давно без визита'**
  String get crmSortLastVisit;

  /// No description provided for @crmSortName.
  ///
  /// In ru, this message translates to:
  /// **'По названию'**
  String get crmSortName;

  /// No description provided for @crmSales30.
  ///
  /// In ru, this message translates to:
  /// **'за 30 дней {value}'**
  String crmSales30(String value);

  /// No description provided for @crmLastVisit.
  ///
  /// In ru, this message translates to:
  /// **'визит {date}'**
  String crmLastVisit(String date);

  /// No description provided for @crmNoAccess.
  ///
  /// In ru, this message translates to:
  /// **'Список клиентов ведётся в Sales Base. Вас ещё не добавили в Sales Base — точки ваших ТП открываются из карточки ТП в «Аналитике».'**
  String get crmNoAccess;

  /// No description provided for @crmMonthToDate.
  ///
  /// In ru, this message translates to:
  /// **'С начала месяца'**
  String get crmMonthToDate;

  /// No description provided for @crmOrders30.
  ///
  /// In ru, this message translates to:
  /// **'Заказы, 30 дн.'**
  String get crmOrders30;

  /// No description provided for @crmVisits30.
  ///
  /// In ru, this message translates to:
  /// **'Визиты, 30 дн.'**
  String get crmVisits30;

  /// No description provided for @crmTrend.
  ///
  /// In ru, this message translates to:
  /// **'Тренд'**
  String get crmTrend;

  /// No description provided for @crmResponsible.
  ///
  /// In ru, this message translates to:
  /// **'Ответственный ТП'**
  String get crmResponsible;

  /// No description provided for @crmContact.
  ///
  /// In ru, this message translates to:
  /// **'Контакт'**
  String get crmContact;

  /// No description provided for @crmPhone.
  ///
  /// In ru, this message translates to:
  /// **'Телефон'**
  String get crmPhone;

  /// No description provided for @crmNote.
  ///
  /// In ru, this message translates to:
  /// **'Заметка'**
  String get crmNote;

  /// No description provided for @crmTarget.
  ///
  /// In ru, this message translates to:
  /// **'План на месяц'**
  String get crmTarget;

  /// No description provided for @crmMonths.
  ///
  /// In ru, this message translates to:
  /// **'Отгрузки по месяцам'**
  String get crmMonths;

  /// No description provided for @crmOrders.
  ///
  /// In ru, this message translates to:
  /// **'Заказы'**
  String get crmOrders;

  /// No description provided for @crmVisits.
  ///
  /// In ru, this message translates to:
  /// **'Визиты'**
  String get crmVisits;

  /// No description provided for @crmHistory.
  ///
  /// In ru, this message translates to:
  /// **'История изменений'**
  String get crmHistory;

  /// No description provided for @crmMonthSales.
  ///
  /// In ru, this message translates to:
  /// **'Продажи за месяц'**
  String get crmMonthSales;

  /// No description provided for @crmEdit.
  ///
  /// In ru, this message translates to:
  /// **'Изменить'**
  String get crmEdit;

  /// No description provided for @crmEditTitle.
  ///
  /// In ru, this message translates to:
  /// **'Карточка точки'**
  String get crmEditTitle;

  /// No description provided for @crmSaved.
  ///
  /// In ru, this message translates to:
  /// **'Сохранено'**
  String get crmSaved;

  /// No description provided for @crmStatus.
  ///
  /// In ru, this message translates to:
  /// **'Статус'**
  String get crmStatus;

  /// No description provided for @crmPriority.
  ///
  /// In ru, this message translates to:
  /// **'Приоритет'**
  String get crmPriority;

  /// No description provided for @crmLinkoReadonly.
  ///
  /// In ru, this message translates to:
  /// **'Данные Linko только для чтения: меняется карточка Sales Base (статус, приоритет, план, контакты).'**
  String get crmLinkoReadonly;

  /// No description provided for @actionSave.
  ///
  /// In ru, this message translates to:
  /// **'Сохранить'**
  String get actionSave;

  /// No description provided for @auditUpdated.
  ///
  /// In ru, this message translates to:
  /// **'Изменена карточка'**
  String get auditUpdated;

  /// No description provided for @auditAgent.
  ///
  /// In ru, this message translates to:
  /// **'Сменён агент'**
  String get auditAgent;

  /// No description provided for @crm30d.
  ///
  /// In ru, this message translates to:
  /// **'За 30 дней'**
  String get crm30d;

  /// No description provided for @crm90d.
  ///
  /// In ru, this message translates to:
  /// **'За 90 дней'**
  String get crm90d;

  /// No description provided for @anWas.
  ///
  /// In ru, this message translates to:
  /// **'было {value}'**
  String anWas(String value);

  /// No description provided for @finTabOverview.
  ///
  /// In ru, this message translates to:
  /// **'Обзор'**
  String get finTabOverview;

  /// No description provided for @finTabPnl.
  ///
  /// In ru, this message translates to:
  /// **'Доходы и расходы'**
  String get finTabPnl;

  /// No description provided for @finTabCash.
  ///
  /// In ru, this message translates to:
  /// **'Деньги'**
  String get finTabCash;

  /// No description provided for @finTabDebts.
  ///
  /// In ru, this message translates to:
  /// **'Долги'**
  String get finTabDebts;

  /// No description provided for @finTabAnomalies.
  ///
  /// In ru, this message translates to:
  /// **'Отклонения'**
  String get finTabAnomalies;

  /// No description provided for @finPeriodMtd.
  ///
  /// In ru, this message translates to:
  /// **'Этот месяц'**
  String get finPeriodMtd;

  /// No description provided for @finPeriodPrev.
  ///
  /// In ru, this message translates to:
  /// **'Прошлый месяц'**
  String get finPeriodPrev;

  /// No description provided for @finPeriodQuarter.
  ///
  /// In ru, this message translates to:
  /// **'Квартал'**
  String get finPeriodQuarter;

  /// No description provided for @finPeriodYear.
  ///
  /// In ru, this message translates to:
  /// **'С начала года'**
  String get finPeriodYear;

  /// No description provided for @finAllOrgs.
  ///
  /// In ru, this message translates to:
  /// **'Все организации'**
  String get finAllOrgs;

  /// No description provided for @finDemo.
  ///
  /// In ru, this message translates to:
  /// **'Демо-данные тестового источника 1С'**
  String get finDemo;

  /// No description provided for @finDataThrough.
  ///
  /// In ru, this message translates to:
  /// **'Проводки по {date}'**
  String finDataThrough(String date);

  /// No description provided for @finGrossProfit.
  ///
  /// In ru, this message translates to:
  /// **'Валовая прибыль'**
  String get finGrossProfit;

  /// No description provided for @finVsPrev.
  ///
  /// In ru, this message translates to:
  /// **'к прошлому периоду {value}'**
  String finVsPrev(String value);

  /// No description provided for @finMonths.
  ///
  /// In ru, this message translates to:
  /// **'12 месяцев'**
  String get finMonths;

  /// No description provided for @finCashTotal.
  ///
  /// In ru, this message translates to:
  /// **'Деньги на счетах и в кассе'**
  String get finCashTotal;

  /// No description provided for @finReceivables.
  ///
  /// In ru, this message translates to:
  /// **'Нам должны'**
  String get finReceivables;

  /// No description provided for @finPayables.
  ///
  /// In ru, this message translates to:
  /// **'Мы должны'**
  String get finPayables;

  /// No description provided for @finInventory.
  ///
  /// In ru, this message translates to:
  /// **'Запасы'**
  String get finInventory;

  /// No description provided for @finExpenseGrowth.
  ///
  /// In ru, this message translates to:
  /// **'Расходы, которые выросли'**
  String get finExpenseGrowth;

  /// No description provided for @finExpensesByAccount.
  ///
  /// In ru, this message translates to:
  /// **'Расходы по статьям'**
  String get finExpensesByAccount;

  /// No description provided for @finExpensesByDepartment.
  ///
  /// In ru, this message translates to:
  /// **'Расходы по подразделениям'**
  String get finExpensesByDepartment;

  /// No description provided for @finInflow.
  ///
  /// In ru, this message translates to:
  /// **'Поступления'**
  String get finInflow;

  /// No description provided for @finOutflow.
  ///
  /// In ru, this message translates to:
  /// **'Платежи'**
  String get finOutflow;

  /// No description provided for @finBalances.
  ///
  /// In ru, this message translates to:
  /// **'Остатки'**
  String get finBalances;

  /// No description provided for @finOverdue.
  ///
  /// In ru, this message translates to:
  /// **'Просрочено'**
  String get finOverdue;

  /// No description provided for @finAdvances.
  ///
  /// In ru, this message translates to:
  /// **'Авансы'**
  String get finAdvances;

  /// No description provided for @finBucket_not_due.
  ///
  /// In ru, this message translates to:
  /// **'Срок не наступил'**
  String get finBucket_not_due;

  /// No description provided for @finBucket_1_30.
  ///
  /// In ru, this message translates to:
  /// **'Просрочка 1–30 дней'**
  String get finBucket_1_30;

  /// No description provided for @finBucket_31_60.
  ///
  /// In ru, this message translates to:
  /// **'31–60 дней'**
  String get finBucket_31_60;

  /// No description provided for @finBucket_61_90.
  ///
  /// In ru, this message translates to:
  /// **'61–90 дней'**
  String get finBucket_61_90;

  /// No description provided for @finBucket_over_90.
  ///
  /// In ru, this message translates to:
  /// **'Больше 90 дней'**
  String get finBucket_over_90;

  /// No description provided for @finBucket_no_term.
  ///
  /// In ru, this message translates to:
  /// **'Срок не задан'**
  String get finBucket_no_term;

  /// No description provided for @finOverdueDays.
  ///
  /// In ru, this message translates to:
  /// **'просрочка до {n} дн.'**
  String finOverdueDays(int n);

  /// No description provided for @finChecks.
  ///
  /// In ru, this message translates to:
  /// **'Что проверено'**
  String get finChecks;

  /// No description provided for @finNoAnomalies.
  ///
  /// In ru, this message translates to:
  /// **'Отклонений не найдено'**
  String get finNoAnomalies;

  /// No description provided for @finEntries.
  ///
  /// In ru, this message translates to:
  /// **'Проводки'**
  String get finEntries;

  /// No description provided for @finEntriesTotal.
  ///
  /// In ru, this message translates to:
  /// **'{n} проводок на {sum}'**
  String finEntriesTotal(int n, String sum);

  /// No description provided for @finDocument.
  ///
  /// In ru, this message translates to:
  /// **'Документ 1С'**
  String get finDocument;

  /// No description provided for @finPosted.
  ///
  /// In ru, this message translates to:
  /// **'Проведён'**
  String get finPosted;

  /// No description provided for @finNotPosted.
  ///
  /// In ru, this message translates to:
  /// **'Не проведён'**
  String get finNotPosted;

  /// No description provided for @finCounterparty.
  ///
  /// In ru, this message translates to:
  /// **'Контрагент'**
  String get finCounterparty;

  /// No description provided for @finContract.
  ///
  /// In ru, this message translates to:
  /// **'Договор'**
  String get finContract;

  /// No description provided for @finWarehouse.
  ///
  /// In ru, this message translates to:
  /// **'Склад'**
  String get finWarehouse;

  /// No description provided for @finPurpose.
  ///
  /// In ru, this message translates to:
  /// **'Назначение'**
  String get finPurpose;

  /// No description provided for @finLines.
  ///
  /// In ru, this message translates to:
  /// **'Строки'**
  String get finLines;

  /// No description provided for @finVat.
  ///
  /// In ru, this message translates to:
  /// **'НДС'**
  String get finVat;

  /// No description provided for @finAccounts.
  ///
  /// In ru, this message translates to:
  /// **'Счета (ОСВ)'**
  String get finAccounts;

  /// No description provided for @finOpening.
  ///
  /// In ru, this message translates to:
  /// **'Начало'**
  String get finOpening;

  /// No description provided for @finTurnover.
  ///
  /// In ru, this message translates to:
  /// **'Обороты'**
  String get finTurnover;

  /// No description provided for @finClosing.
  ///
  /// In ru, this message translates to:
  /// **'Конец'**
  String get finClosing;

  /// No description provided for @finDebit.
  ///
  /// In ru, this message translates to:
  /// **'Дт'**
  String get finDebit;

  /// No description provided for @finCredit.
  ///
  /// In ru, this message translates to:
  /// **'Кт'**
  String get finCredit;

  /// No description provided for @prTabOverview.
  ///
  /// In ru, this message translates to:
  /// **'Обзор'**
  String get prTabOverview;

  /// No description provided for @prTabOrders.
  ///
  /// In ru, this message translates to:
  /// **'Задания'**
  String get prTabOrders;

  /// No description provided for @prTabShifts.
  ///
  /// In ru, this message translates to:
  /// **'Смены'**
  String get prTabShifts;

  /// No description provided for @prGood.
  ///
  /// In ru, this message translates to:
  /// **'Годный выпуск'**
  String get prGood;

  /// No description provided for @prPlan.
  ///
  /// In ru, this message translates to:
  /// **'План'**
  String get prPlan;

  /// No description provided for @prOpenShifts.
  ///
  /// In ru, this message translates to:
  /// **'Смены открыты'**
  String get prOpenShifts;

  /// No description provided for @prClosedShifts.
  ///
  /// In ru, this message translates to:
  /// **'Ждут утверждения'**
  String get prClosedShifts;

  /// No description provided for @prNormDev.
  ///
  /// In ru, this message translates to:
  /// **'Отклонение от нормы'**
  String get prNormDev;

  /// No description provided for @prByProduct.
  ///
  /// In ru, this message translates to:
  /// **'Выпуск по продуктам'**
  String get prByProduct;

  /// No description provided for @prIssues.
  ///
  /// In ru, this message translates to:
  /// **'Проблемы'**
  String get prIssues;

  /// No description provided for @prStockValue.
  ///
  /// In ru, this message translates to:
  /// **'Сырьё на складах'**
  String get prStockValue;

  /// No description provided for @prExpiring.
  ///
  /// In ru, this message translates to:
  /// **'Истекает за 30 дней'**
  String get prExpiring;

  /// No description provided for @prExpired.
  ///
  /// In ru, this message translates to:
  /// **'Просрочено'**
  String get prExpired;

  /// No description provided for @prBelowMin.
  ///
  /// In ru, this message translates to:
  /// **'Ниже минимума'**
  String get prBelowMin;

  /// No description provided for @prOverdueOrders.
  ///
  /// In ru, this message translates to:
  /// **'Просрочено заданий'**
  String get prOverdueOrders;

  /// No description provided for @prLowDays.
  ///
  /// In ru, this message translates to:
  /// **'Запаса меньше всего'**
  String get prLowDays;

  /// No description provided for @prDaysLeft.
  ///
  /// In ru, this message translates to:
  /// **'{n} дн.'**
  String prDaysLeft(String n);

  /// No description provided for @prStatus_Draft.
  ///
  /// In ru, this message translates to:
  /// **'Черновик'**
  String get prStatus_Draft;

  /// No description provided for @prStatus_Released.
  ///
  /// In ru, this message translates to:
  /// **'Выпущено'**
  String get prStatus_Released;

  /// No description provided for @prStatus_InProgress.
  ///
  /// In ru, this message translates to:
  /// **'В работе'**
  String get prStatus_InProgress;

  /// No description provided for @prStatus_Completed.
  ///
  /// In ru, this message translates to:
  /// **'Закрыто'**
  String get prStatus_Completed;

  /// No description provided for @prStatus_Cancelled.
  ///
  /// In ru, this message translates to:
  /// **'Отменено'**
  String get prStatus_Cancelled;

  /// No description provided for @prFilterOpen.
  ///
  /// In ru, this message translates to:
  /// **'Открытые'**
  String get prFilterOpen;

  /// No description provided for @prFilterDone.
  ///
  /// In ru, this message translates to:
  /// **'Закрытые'**
  String get prFilterDone;

  /// No description provided for @prFilterAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get prFilterAll;

  /// No description provided for @prPlanned.
  ///
  /// In ru, this message translates to:
  /// **'План {qty} · на {date}'**
  String prPlanned(String qty, String date);

  /// No description provided for @prOutput.
  ///
  /// In ru, this message translates to:
  /// **'Выпуск'**
  String get prOutput;

  /// No description provided for @prMaterials.
  ///
  /// In ru, this message translates to:
  /// **'Сырьё и материалы'**
  String get prMaterials;

  /// No description provided for @prRequired.
  ///
  /// In ru, this message translates to:
  /// **'нужно {v}'**
  String prRequired(String v);

  /// No description provided for @prIssued.
  ///
  /// In ru, this message translates to:
  /// **'выдано {v}'**
  String prIssued(String v);

  /// No description provided for @prLeft.
  ///
  /// In ru, this message translates to:
  /// **'осталось выдать {v}'**
  String prLeft(String v);

  /// No description provided for @prAvailable.
  ///
  /// In ru, this message translates to:
  /// **'на складе {v}'**
  String prAvailable(String v);

  /// No description provided for @prOverconsumed.
  ///
  /// In ru, this message translates to:
  /// **'перерасход'**
  String get prOverconsumed;

  /// No description provided for @prHistory.
  ///
  /// In ru, this message translates to:
  /// **'История'**
  String get prHistory;

  /// No description provided for @prRelease.
  ///
  /// In ru, this message translates to:
  /// **'Выпустить задание'**
  String get prRelease;

  /// No description provided for @prComplete.
  ///
  /// In ru, this message translates to:
  /// **'Закрыть задание'**
  String get prComplete;

  /// No description provided for @prCancel.
  ///
  /// In ru, this message translates to:
  /// **'Отменить задание'**
  String get prCancel;

  /// No description provided for @prReceive.
  ///
  /// In ru, this message translates to:
  /// **'Оприходовать выпуск'**
  String get prReceive;

  /// No description provided for @prOutputQty.
  ///
  /// In ru, this message translates to:
  /// **'Фактический выпуск, {unit}'**
  String prOutputQty(String unit);

  /// No description provided for @prReason.
  ///
  /// In ru, this message translates to:
  /// **'Причина'**
  String get prReason;

  /// No description provided for @prConfirmTitle.
  ///
  /// In ru, this message translates to:
  /// **'Подтвердите операцию'**
  String get prConfirmTitle;

  /// No description provided for @prConfirmRelease.
  ///
  /// In ru, this message translates to:
  /// **'Задание уйдёт в работу; нехватка сырья будет отмечена, снабжение получит уведомление.'**
  String get prConfirmRelease;

  /// No description provided for @prConfirmComplete.
  ///
  /// In ru, this message translates to:
  /// **'Будет зафиксирован выпуск {qty} и посчитан расход по норме. Отменить закрытие нельзя.'**
  String prConfirmComplete(String qty);

  /// No description provided for @prConfirmReceive.
  ///
  /// In ru, this message translates to:
  /// **'Выпуск будет оприходован на склад.'**
  String get prConfirmReceive;

  /// No description provided for @prDone.
  ///
  /// In ru, this message translates to:
  /// **'Готово: {what}'**
  String prDone(String what);

  /// No description provided for @prConflict.
  ///
  /// In ru, this message translates to:
  /// **'Задание изменили — обновили данные, проверьте и повторите.'**
  String get prConflict;

  /// No description provided for @prShift.
  ///
  /// In ru, this message translates to:
  /// **'Смена {n} · {date}'**
  String prShift(int n, String date);

  /// No description provided for @prShiftStatus_Draft.
  ///
  /// In ru, this message translates to:
  /// **'Ввод'**
  String get prShiftStatus_Draft;

  /// No description provided for @prShiftStatus_Closed.
  ///
  /// In ru, this message translates to:
  /// **'Ждёт утверждения'**
  String get prShiftStatus_Closed;

  /// No description provided for @prShiftStatus_Approved.
  ///
  /// In ru, this message translates to:
  /// **'Утверждена'**
  String get prShiftStatus_Approved;

  /// No description provided for @prShiftStatus_Superseded.
  ///
  /// In ru, this message translates to:
  /// **'Заменена'**
  String get prShiftStatus_Superseded;

  /// No description provided for @prShiftStatus_Cancelled.
  ///
  /// In ru, this message translates to:
  /// **'Отменена'**
  String get prShiftStatus_Cancelled;

  /// No description provided for @whTabStock.
  ///
  /// In ru, this message translates to:
  /// **'Остатки'**
  String get whTabStock;

  /// No description provided for @whTabMoves.
  ///
  /// In ru, this message translates to:
  /// **'Движения'**
  String get whTabMoves;

  /// No description provided for @whSearch.
  ///
  /// In ru, this message translates to:
  /// **'Артикул, название или штрихкод'**
  String get whSearch;

  /// No description provided for @whFilterAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get whFilterAll;

  /// No description provided for @whFilterLow.
  ///
  /// In ru, this message translates to:
  /// **'Ниже минимума'**
  String get whFilterLow;

  /// No description provided for @whFilterReserved.
  ///
  /// In ru, this message translates to:
  /// **'В резерве'**
  String get whFilterReserved;

  /// No description provided for @whFilterEmpty.
  ///
  /// In ru, this message translates to:
  /// **'Нет остатка'**
  String get whFilterEmpty;

  /// No description provided for @whFilterExpiring.
  ///
  /// In ru, this message translates to:
  /// **'Истекают'**
  String get whFilterExpiring;

  /// No description provided for @whFilterExpired.
  ///
  /// In ru, this message translates to:
  /// **'Просрочены'**
  String get whFilterExpired;

  /// No description provided for @whOnHand.
  ///
  /// In ru, this message translates to:
  /// **'Остаток'**
  String get whOnHand;

  /// No description provided for @whReserved.
  ///
  /// In ru, this message translates to:
  /// **'Резерв'**
  String get whReserved;

  /// No description provided for @whAvailable.
  ///
  /// In ru, this message translates to:
  /// **'Свободно'**
  String get whAvailable;

  /// No description provided for @whMin.
  ///
  /// In ru, this message translates to:
  /// **'Минимум'**
  String get whMin;

  /// No description provided for @whReservations.
  ///
  /// In ru, this message translates to:
  /// **'Резервы по заявкам'**
  String get whReservations;

  /// No description provided for @whMovements.
  ///
  /// In ru, this message translates to:
  /// **'Последние движения'**
  String get whMovements;

  /// No description provided for @whOperation.
  ///
  /// In ru, this message translates to:
  /// **'Операция'**
  String get whOperation;

  /// No description provided for @whOpReceipt.
  ///
  /// In ru, this message translates to:
  /// **'Приход'**
  String get whOpReceipt;

  /// No description provided for @whOpIssue.
  ///
  /// In ru, this message translates to:
  /// **'Выдача отделу'**
  String get whOpIssue;

  /// No description provided for @whOpWriteOff.
  ///
  /// In ru, this message translates to:
  /// **'Списание'**
  String get whOpWriteOff;

  /// No description provided for @whItem.
  ///
  /// In ru, this message translates to:
  /// **'Позиция'**
  String get whItem;

  /// No description provided for @whPickItem.
  ///
  /// In ru, this message translates to:
  /// **'Выберите позицию'**
  String get whPickItem;

  /// No description provided for @whQuantity.
  ///
  /// In ru, this message translates to:
  /// **'Количество, {unit}'**
  String whQuantity(String unit);

  /// No description provided for @whUnitCost.
  ///
  /// In ru, this message translates to:
  /// **'Цена за единицу, сум'**
  String get whUnitCost;

  /// No description provided for @whDepartment.
  ///
  /// In ru, this message translates to:
  /// **'Отдел'**
  String get whDepartment;

  /// No description provided for @whReceiver.
  ///
  /// In ru, this message translates to:
  /// **'Кто получил'**
  String get whReceiver;

  /// No description provided for @whComment.
  ///
  /// In ru, this message translates to:
  /// **'Комментарий'**
  String get whComment;

  /// No description provided for @whReference.
  ///
  /// In ru, this message translates to:
  /// **'Накладная'**
  String get whReference;

  /// No description provided for @whReasonRequired.
  ///
  /// In ru, this message translates to:
  /// **'Укажите причину списания'**
  String get whReasonRequired;

  /// No description provided for @whPost.
  ///
  /// In ru, this message translates to:
  /// **'Провести'**
  String get whPost;

  /// No description provided for @whConfirm.
  ///
  /// In ru, this message translates to:
  /// **'{op}: {item} — {qty}. Остаток на складе изменится.'**
  String whConfirm(String op, String item, String qty);

  /// No description provided for @whPosted.
  ///
  /// In ru, this message translates to:
  /// **'Документ проведён'**
  String get whPosted;

  /// No description provided for @whNoPosts.
  ///
  /// In ru, this message translates to:
  /// **'Проводить операции на складе может только кладовщик этого склада.'**
  String get whNoPosts;

  /// No description provided for @whWarehouse.
  ///
  /// In ru, this message translates to:
  /// **'Склад'**
  String get whWarehouse;

  /// No description provided for @whOnline.
  ///
  /// In ru, this message translates to:
  /// **'Операции склада проводятся только при связи с сервером.'**
  String get whOnline;

  /// No description provided for @mvReceipt.
  ///
  /// In ru, this message translates to:
  /// **'Приход'**
  String get mvReceipt;

  /// No description provided for @mvIssue.
  ///
  /// In ru, this message translates to:
  /// **'Выдача'**
  String get mvIssue;

  /// No description provided for @mvWriteOff.
  ///
  /// In ru, this message translates to:
  /// **'Списание'**
  String get mvWriteOff;

  /// No description provided for @mvAdjustment.
  ///
  /// In ru, this message translates to:
  /// **'Инвентаризация'**
  String get mvAdjustment;

  /// No description provided for @mvTransfer.
  ///
  /// In ru, this message translates to:
  /// **'Перемещение'**
  String get mvTransfer;

  /// No description provided for @mvReversal.
  ///
  /// In ru, this message translates to:
  /// **'Сторно'**
  String get mvReversal;

  /// No description provided for @mvProduction.
  ///
  /// In ru, this message translates to:
  /// **'Производство'**
  String get mvProduction;

  /// No description provided for @rqScopeMy.
  ///
  /// In ru, this message translates to:
  /// **'Мои'**
  String get rqScopeMy;

  /// No description provided for @rqScopeDepartment.
  ///
  /// In ru, this message translates to:
  /// **'Отдела'**
  String get rqScopeDepartment;

  /// No description provided for @rqScopeAll.
  ///
  /// In ru, this message translates to:
  /// **'Все'**
  String get rqScopeAll;

  /// No description provided for @rqItems.
  ///
  /// In ru, this message translates to:
  /// **'{n} поз.'**
  String rqItems(int n);

  /// No description provided for @rqDesired.
  ///
  /// In ru, this message translates to:
  /// **'нужно к {date}'**
  String rqDesired(String date);
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
