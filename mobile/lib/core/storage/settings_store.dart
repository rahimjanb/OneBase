import '../database/app_database.dart';

/// Настройки на устройстве (тема, язык) — в той же SQLite, без отдельной библиотеки. Читаются один раз при запуске.
class SettingsStore {
  SettingsStore(this._db);

  final AppDatabase _db;

  static const themeKey = 'theme_mode';
  static const localeKey = 'locale';

  Future<Map<String, String>> readAll() async {
    final rows = await _db.select(_db.appSettings).get();
    return {for (final r in rows) r.key: r.value};
  }

  Future<void> write(String key, String? value) async {
    if (value == null) {
      await (_db.delete(_db.appSettings)..where((t) => t.key.equals(key))).go();
      return;
    }
    await _db.into(_db.appSettings).insertOnConflictUpdate(AppSettingsCompanion.insert(key: key, value: value));
  }
}
