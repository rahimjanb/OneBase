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

/// Очередь операций, отправленных без сети (только безопасные для повтора: прочтение уведомлений, смена статуса задачи, настройки
/// уведомлений). Финансовые и складские операции сюда не попадают — они идут только при связи и с подтверждением сервера.
@DataClassName('PendingOpRow')
class PendingOps extends Table {
  TextColumn get id => text()();
  TextColumn get userId => text()();

  /// Вид операции для экрана («notification.read», «task.status») и ключ сущности — новая операция над той же сущностью заменяет старую.
  TextColumn get kind => text()();
  TextColumn get entity => text()();
  TextColumn get method => text()();
  TextColumn get path => text()();
  TextColumn get body => text().nullable()();
  DateTimeColumn get createdAt => dateTime()();
  IntColumn get attempts => integer().withDefault(const Constant(0))();

  /// pending — ждёт сети; failed — сервер отказал (показывается пользователю).
  TextColumn get status => text().withDefault(const Constant('pending'))();
  TextColumn get lastError => text().nullable()();

  @override
  Set<Column<Object>> get primaryKey => {id};
}

@DriftDatabase(tables: [CacheEntries, AppSettings, PendingOps])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  /// База приложения в его закрытой папке (`onebase.sqlite`).
  factory AppDatabase.open() => AppDatabase(driftDatabase(name: 'onebase'));

  /// 2 — очередь операций (этап 4). Миграция только добавляет таблицу: кэш и настройки не трогаются.
  @override
  int get schemaVersion => 2;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (m) => m.createAll(),
        onUpgrade: (m, from, to) async {
          if (from < 2) await m.createTable(pendingOps);
        },
      );
}
