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
