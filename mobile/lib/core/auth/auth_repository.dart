import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/api_providers.dart';
import 'token_storage.dart';
import 'user_profile.dart';

/// Вход, профиль и выход через существующий OneBase Authentication API (`/api/auth/*`). Новой системы авторизации нет.
class AuthRepository {
  AuthRepository(this._api);

  final ApiClient _api;

  /// `POST /api/auth/login` — логин или почта и пароль. 401 — неверно, 429 — больше 10 попыток в минуту.
  Future<StoredToken> login(String login, String password) async {
    final json = await _api.post(
      '/api/auth/login',
      body: {'login': login, 'password': password},
      decode: (j) => j! as Map<String, Object?>,
    );
    final token = json['accessToken'];
    if (token is! String || token.isEmpty) throw const FormatException('Нет accessToken в ответе входа');
    final expires = json['expiresAt'];
    return StoredToken(token, expires is String ? DateTime.tryParse(expires) : null);
  }

  /// `GET /api/auth/me` — профиль и актуальные права.
  Future<UserProfile> me({CancelToken? cancel}) =>
      _api.get('/api/auth/me', (j) => UserProfile.fromJson(j! as Map<String, Object?>), cancel: cancel);

  /// `POST /api/auth/logout` — сервер закрывает сеанс этого входа; токен больше не действует, даже если его скопировали.
  Future<void> logout() => _api.post<void>('/api/auth/logout');
}

final authRepositoryProvider = Provider<AuthRepository>((ref) => AuthRepository(ref.watch(apiClientProvider)));

/// Id пользователя из токена (claim `sub`) — чтобы найти его сохранённый профиль без сети. Подпись не проверяется: это не доступ,
/// а ключ кэша; доступ проверяет сервер.
String? userIdOfToken(String token) {
  final parts = token.split('.');
  if (parts.length != 3) return null;
  try {
    final payload = jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))));
    final sub = payload is Map ? payload['sub'] : null;
    return sub is String && sub.isNotEmpty ? sub : null;
  } on FormatException {
    return null;
  }
}
