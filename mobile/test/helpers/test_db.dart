import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:onebase_mobile/core/database/app_database.dart';

/// База в памяти для тестов.
AppDatabase memoryDb() {
  driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;
  return AppDatabase(NativeDatabase.memory());
}
