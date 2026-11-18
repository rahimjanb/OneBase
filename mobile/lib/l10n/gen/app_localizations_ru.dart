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
}
