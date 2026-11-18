import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/api/api_providers.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/core/config/app_config.dart';
import 'package:onebase_mobile/core/core_providers.dart';
import 'package:onebase_mobile/core/database/app_database.dart';
import 'package:onebase_mobile/core/network/connectivity.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';

import 'test_db.dart';

const testConfig = AppConfig(env: AppEnv.development, apiBaseUrl: 'http://onebase.test', appVersion: '0.1.0-test');

/// Общие подмены для тестов приложения: настройки сборки, база в памяти, фейковый сервер, «сеть есть».
List<Override> baseOverrides({
  required AppDatabase db,
  HttpClientAdapter? adapter,
  bool online = true,
  Map<String, String> settings = const {},
  DateTime Function()? clock,
  AppConfig config = testConfig,
  TokenStorage? tokens,
}) =>
    [
      tokenStorageProvider.overrideWithValue(tokens ?? MemoryTokenStorage()),
      appConfigProvider.overrideWithValue(config),
      appDatabaseProvider.overrideWithValue(db),
      initialSettingsProvider.overrideWithValue(settings),
      httpAdapterProvider.overrideWithValue(adapter),
      retryDelaysProvider.overrideWithValue(const [Duration.zero, Duration.zero]),
      isOnlineProvider.overrideWith((ref) => Stream.value(online)),
      if (clock != null) clockProvider.overrideWithValue(clock),
    ];

/// Один виджет в теме OneBase с русской локалью — для тестов общих виджетов.
Future<void> pumpInApp(WidgetTester tester, Widget child, {AppDatabase? db, Brightness brightness = Brightness.light}) async {
  await tester.pumpWidget(
    ProviderScope(
      retry: (_, _) => null,
      overrides: baseOverrides(db: db ?? memoryDb()),
      child: MaterialApp(
        theme: brightness == Brightness.light ? ObTheme.light() : ObTheme.dark(),
        locale: const Locale('ru'),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      ),
    ),
  );
}
