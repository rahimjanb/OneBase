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
  );
  ref.onDispose(dio.close);
  return ApiClient(dio);
});

/// Dio для OneBase API: таймауты под мобильную сеть, перехватчики — сеанс, журнал (debug), повтор GET.
Dio buildDio({required AppConfig config, required ic.SessionInterceptor session, HttpClientAdapter? adapter, bool retry = true}) {
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
  if (retry) dio.interceptors.add(ic.RetryInterceptor(dio));
  return dio;
}

/// «OneBase-Mobile/0.1.0 (ios)» — попадает в сеанс входа на сервере (список сеансов пользователя).
String userAgentOf(AppConfig config) => 'OneBase-Mobile/${config.appVersion} (${defaultTargetPlatform.name})';
