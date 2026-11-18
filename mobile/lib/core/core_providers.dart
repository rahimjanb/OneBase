import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'cache/cache_store.dart';
import 'config/app_config.dart';
import 'database/app_database.dart';
import 'storage/settings_store.dart';

/// Провайдеры, которые задаёт `bootstrap` (или тест) через `overrides`: настройки сборки, база, сохранённые настройки.
final appConfigProvider = Provider<AppConfig>((ref) => throw UnimplementedError('appConfigProvider задаётся в bootstrap'));

final appDatabaseProvider = Provider<AppDatabase>((ref) => throw UnimplementedError('appDatabaseProvider задаётся в bootstrap'));

/// Настройки, прочитанные при запуске (тема, язык): ключ → значение.
final initialSettingsProvider = Provider<Map<String, String>>((ref) => const {});

final cacheStoreProvider = Provider<CacheStore>((ref) => CacheStore(ref.watch(appDatabaseProvider)));

final settingsStoreProvider = Provider<SettingsStore>((ref) => SettingsStore(ref.watch(appDatabaseProvider)));

/// Текущее время — через провайдер, чтобы тесты могли его зафиксировать.
final clockProvider = Provider<DateTime Function()>((ref) => DateTime.now);
