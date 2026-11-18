import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/token_holder.dart';
import '../config/app_config.dart';
import '../core_providers.dart';
import '../storage/preferences.dart';
import 'api_client.dart';
import 'interceptors.dart' as ic;

final tokenHolderProvider = Provider<TokenHolder>((ref) => TokenHolder());

final unauthorizedSignalProvider = Provider<UnauthorizedSignal>((ref) {
  final signal = UnauthorizedSignal();
  ref.onDispose(signal.close);
  return signal;
});

/// Адаптер HTTP — подменяется в тестах фейковым.
final httpAdapterProvider = Provider<HttpClientAdapter?>((ref) => null);

/// Паузы перед повторами GET (в тестах — нулевые).
final retryDelaysProvider = Provider<List<Duration>>(
  (ref) => const [Duration(milliseconds: 500), Duration(milliseconds: 1500)],
);

/// Разбор тяжёлых ответов в фоновом изоляте (в виджет-тестах выключается).
final backgroundParsingProvider = Provider<bool>((ref) => true);

final apiClientProvider = Provider<ApiClient>((ref) {
  final config = ref.watch(appConfigProvider);
  final tokens = ref.watch(tokenHolderProvider);
  final signal = ref.watch(unauthorizedSignalProvider);
  final dio = buildDio(
    config: config,
    session: ic.SessionInterceptor(
      token: () => tokens.value,
      language: () => effectiveLanguage(ref.read(localeProvider)),
      userAgent: userAgentOf(config),
      onUnauthorized: signal.emit,
    ),
    adapter: ref.watch(httpAdapterProvider),
    retryDelays: ref.watch(retryDelaysProvider),
  );
  ref.onDispose(dio.close);
  return ApiClient(dio, parseInBackground: ref.watch(backgroundParsingProvider));
});

/// Dio для OneBase API: таймауты под мобильную сеть, перехватчики — сеанс, журнал (debug), повтор GET.
Dio buildDio({
  required AppConfig config,
  required ic.SessionInterceptor session,
  HttpClientAdapter? adapter,
  List<Duration>? retryDelays,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: config.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      sendTimeout: const Duration(seconds: 30),
      receiveTimeout: const Duration(seconds: 30),
      contentType: Headers.jsonContentType,
      responseType: ResponseType.json,
    ),
  );
  if (adapter != null) dio.httpClientAdapter = adapter;
  dio.interceptors.add(session);
  if (kDebugMode) dio.interceptors.add(ic.LogInterceptor());
  if (retryDelays == null) {
    dio.interceptors.add(ic.RetryInterceptor(dio));
  } else if (retryDelays.isNotEmpty) {
    dio.interceptors.add(ic.RetryInterceptor(dio, delays: retryDelays));
  }
  return dio;
}

/// «OneBase-Mobile/0.1.0 (ios)» — попадает в сеанс входа на сервере (список сеансов пользователя).
String userAgentOf(AppConfig config) => 'OneBase-Mobile/${config.appVersion} (${defaultTargetPlatform.name})';
