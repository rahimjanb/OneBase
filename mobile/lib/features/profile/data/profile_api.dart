import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import 'profile_models.dart';

/// Запросы профиля к существующим эндпоинтам. `GET /api/me/ai-plan` не используется: он ставит платную AI-генерацию.
class ProfileApi {
  ProfileApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  Future<MyWorkday> workday(CancelToken cancel) => _api.get('/api/me/workday', (j) => MyWorkday.fromJson(_map(j)), cancel: cancel);

  Future<MyTasks> tasks(CancelToken cancel) => _api.get('/api/me/tasks', (j) => MyTasks.fromJson(_map(j)), cancel: cancel);

  Future<NotificationPrefs> prefs(CancelToken cancel) =>
      _api.get('/api/notifications/preferences', (j) => NotificationPrefs.fromJson(_map(j)), cancel: cancel);

  Future<List<TelegramLink>> telegram(CancelToken cancel) => _api.get(
        '/api/me/telegram',
        (j) => [for (final e in j! as List<Object?>) TelegramLink.fromJson(_map(e))],
        cancel: cancel,
      );

  /// Новый код привязки Telegram (живёт 15 минут, прежние гаснут; не больше 10 в час).
  Future<TelegramCode> telegramCode() => _api.post('/api/me/telegram/code', decode: (j) => TelegramCode.fromJson(_map(j)));

  Future<void> unlinkTelegram(String permissionId) => _api.delete<void>('/api/me/telegram/$permissionId');
}

final profileApiProvider = Provider<ProfileApi>((ref) => ProfileApi(ref.watch(apiClientProvider)));
