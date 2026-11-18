import 'dart:developer' as developer;

import 'package:flutter/foundation.dart';

/// Журнал приложения поверх `dart:developer`. Видно в DevTools и в консоли `flutter run`. Отладочные записи — только в debug.
/// Правило: никаких токенов, паролей, тел ответов и персональных данных — только путь, код, длительность и вид ошибки.
abstract final class Log {
  static void d(String tag, String message) {
    if (kDebugMode) developer.log(message, name: 'onebase.$tag', level: 500);
  }

  static void i(String tag, String message) => developer.log(message, name: 'onebase.$tag', level: 800);

  static void w(String tag, String message, [Object? error]) =>
      developer.log(message, name: 'onebase.$tag', level: 900, error: error);

  static void e(String tag, String message, [Object? error, StackTrace? stack]) =>
      developer.log(message, name: 'onebase.$tag', level: 1000, error: error, stackTrace: stack);
}
