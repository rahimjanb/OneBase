import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';
import '../core/core_providers.dart';
import '../core/database/app_database.dart';
import '../core/logging/log.dart';
import '../core/storage/settings_store.dart';
import 'app.dart';

/// Запуск: настройки сборки, журнал необработанных ошибок, база, сохранённые тема и язык, затем приложение.
/// Повторы провайдеров Riverpod выключены: сетевые повторы делает Dio (только GET), а блок сам показывает ошибку и «Повторить».
Future<void> bootstrap() async {
  WidgetsFlutterBinding.ensureInitialized();
  final config = AppConfig.fromEnvironment();

  FlutterError.onError = (details) {
    Log.e('flutter', details.exceptionAsString(), details.exception, details.stack);
    FlutterError.presentError(details);
  };
  PlatformDispatcher.instance.onError = (error, stack) {
    Log.e('app', 'Необработанная ошибка', error, stack);
    return true;
  };

  final db = AppDatabase.open();
  var settings = const <String, String>{};
  try {
    settings = await SettingsStore(db).readAll();
  } on Object catch (e, s) {
    Log.e('app', 'Настройки не прочитаны — по умолчанию', e, s);
  }
  Log.i('app', 'OneBase Mobile ${config.appVersion} · ${config.env.name}');

  runApp(
    ProviderScope(
      retry: (_, _) => null,
      overrides: [
        appConfigProvider.overrideWithValue(config),
        appDatabaseProvider.overrideWithValue(db),
        initialSettingsProvider.overrideWithValue(settings),
      ],
      child: const OneBaseApp(),
    ),
  );
}
