import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';

import 'fake_adapter.dart';

/// Ответ маршрута: тело или исключение (обрыв сети).
typedef Route = FutureOr<ResponseBody> Function(RequestOptions o);

/// Фейковый OneBase API: маршруты «МЕТОД /путь» → ответ. Незаданный маршрут — 404. Запросы запоминаются.
class FakeServer {
  FakeServer() {
    adapter = FakeAdapter(_handle);
  }

  late final FakeAdapter adapter;
  final Map<String, Route> routes = {};

  List<RequestOptions> get requests => adapter.requests;

  void on(String method, String path, Route route) => routes['$method $path'] = route;

  void json(String method, String path, Object? body, {int status = 200}) =>
      on(method, path, (_) => FakeAdapter.json(body, status: status));

  /// Сеть недоступна для маршрута.
  void offline(String method, String path) =>
      on(method, path, (o) => throw DioException.connectionError(requestOptions: o, reason: 'offline'));

  int count(String method, String path) => requests.where((r) => r.method == method && r.path == path).length;

  FutureOr<ResponseBody> _handle(RequestOptions o) {
    final route = routes['${o.method} ${o.path}'];
    if (route == null) return FakeAdapter.json({'error': 'нет маршрута ${o.method} ${o.path}'}, status: 404);
    return route(o);
  }
}

/// JWT без подписи для тестов: нужен только claim `sub` (id пользователя).
String fakeJwt(String sub) {
  String part(Map<String, Object?> m) => base64Url.encode(utf8.encode(jsonEncode(m))).replaceAll('=', '');
  return '${part({'alg': 'HS256', 'typ': 'JWT'})}.${part({'sub': sub, 'sid': 'sess-1'})}.signature';
}

/// Профиль `/api/auth/me` — синтетический, не данные компании.
Map<String, Object?> profileJson({
  String id = 'u-1',
  String name = 'Иван Петров',
  List<String> permissions = const [
    'sales.read',
    'finance.read',
    'hr.read',
    'work.tasks',
    'ai.agents.run',
    'analytik.use',
    'production.read',
  ],
  List<String> roles = const ['Директор'],
  bool hrAccess = true,
}) =>
    {
      'id': id,
      'login': 'ivan',
      'email': 'ivan@example.test',
      'name': name,
      'firstName': 'Иван',
      'lastName': 'Петров',
      'position': 'Директор',
      'departmentName': 'Руководство',
      'roles': roles,
      'home': 'overview',
      'permissions': permissions,
      'canOpenSettings': true,
      'agentDepartments': <String>[],
      'hrAccess': hrAccess,
      'hiringRequests': false,
      'salesScope': {'level': 'Organization', 'home': '/sales'},
      'departments': ['sales'],
      'whatsNewSeen': null,
      'createdAt': '2026-10-01T00:00:00Z',
    };
