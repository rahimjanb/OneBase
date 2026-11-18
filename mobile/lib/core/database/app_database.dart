import 'package:drift/drift.dart';
import 'package:drift_flutter/drift_flutter.dart';

part 'app_database.g.dart';

/// Последний успешный ответ блока (только нужный экрану срез, в JSON). Ключ — «блок + параметры», [userId] — чей кэш: при выходе
/// и при смене пользователя всё удаляется.
@DataClassName('CacheRow')
class CacheEntries extends Table {
  TextColumn get key => text()();
  TextColumn get userId => text()();
  TextColumn get json => text()();
  DateTimeColumn get savedAt => dateTime()();

  @override
  Set<Column<Object>> get primaryKey => {key};
}

/// Настройки приложения на устройстве: тема, язык.
@DataClassName('SettingRow')
class AppSettings extends Table {
  TextColumn get key => text()();
  TextColumn get value => text()();

  @override
  Set<Column<Object>> get primaryKey => {key};
}

@DriftDatabase(tables: [CacheEntries, AppSettings])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  /// База приложения в его закрытой папке (`onebase.sqlite`).
  factory AppDatabase.open() => AppDatabase(driftDatabase(name: 'onebase'));

  @override
  int get schemaVersion => 1;
}
