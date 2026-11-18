import 'package:flutter/foundation.dart';

/// Окружение сборки: `--dart-define-from-file=env/<name>.json`.
enum AppEnv { development, staging, production }

/// Настройки сборки. Адрес API задаётся при сборке. В development без адреса — локальный API на :5081
/// (Android-эмулятор видит компьютер как 10.0.2.2). В staging и production адрес обязателен: без него приложение показывает
/// «Адрес сервера не задан» и в сеть не ходит.
@immutable
class AppConfig {
  const AppConfig({
    required this.env,
    required this.apiBaseUrl,
    required this.appVersion,
  });

  factory AppConfig.fromEnvironment({TargetPlatform? platform}) {
    const envName = String.fromEnvironment('APP_ENV', defaultValue: 'development');
    const base = String.fromEnvironment('API_BASE_URL');
    const android = String.fromEnvironment('API_BASE_URL_ANDROID');
    const version = String.fromEnvironment('APP_VERSION', defaultValue: '0.1.0');

    final env = AppEnv.values.firstWhere(
      (e) => e.name == envName,
      orElse: () => AppEnv.development,
    );
    final isAndroid = (platform ?? defaultTargetPlatform) == TargetPlatform.android;
    var url = isAndroid && android.isNotEmpty ? android : base;
    if (url.isEmpty && env == AppEnv.development) {
      url = isAndroid ? 'http://10.0.2.2:5081' : 'http://localhost:5081';
    }
    return AppConfig(env: env, apiBaseUrl: normalizeBaseUrl(url), appVersion: version);
  }

  final AppEnv env;

  /// Адрес OneBase без завершающего «/»: запросы идут на `$apiBaseUrl/api/...`. Пусто — не задан при сборке.
  final String apiBaseUrl;
  final String appVersion;

  bool get isConfigured => apiBaseUrl.isNotEmpty;
  bool get isDevelopment => env == AppEnv.development;

  /// «https://onebase.uz/» → «https://onebase.uz»; пробелы по краям убираются.
  static String normalizeBaseUrl(String url) {
    var u = url.trim();
    while (u.endsWith('/')) {
      u = u.substring(0, u.length - 1);
    }
    return u;
  }
}
