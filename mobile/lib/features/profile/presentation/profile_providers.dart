import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/synchronization/outbox.dart';
import '../data/profile_api.dart';
import '../data/profile_models.dart';

ProfileApi _api(Ref ref) => ref.read(profileApiProvider);

class WorkdayBlock extends CachedBlock<MyWorkday> {
  @override
  String get cacheKey => 'profile/workday';

  @override
  Future<MyWorkday> fetch(CancelToken cancel) => _api(ref).workday(cancel);

  @override
  MyWorkday decode(Object? json) => MyWorkday.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(MyWorkday data) => data.toJson();
}

final workdayBlock = NotifierProvider.autoDispose<WorkdayBlock, BlockState<MyWorkday>>(WorkdayBlock.new);

class MyTasksBlock extends CachedBlock<MyTasks> {
  @override
  String get cacheKey => 'profile/tasks';

  @override
  Future<MyTasks> fetch(CancelToken cancel) => _api(ref).tasks(cancel);

  @override
  MyTasks decode(Object? json) => MyTasks.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(MyTasks data) => data.toJson();
}

final myTasksBlock = NotifierProvider.autoDispose<MyTasksBlock, BlockState<MyTasks>>(MyTasksBlock.new);

class TelegramBlock extends CachedBlock<List<TelegramLink>> {
  @override
  String get cacheKey => 'profile/telegram';

  @override
  bool get persist => false;

  @override
  Future<List<TelegramLink>> fetch(CancelToken cancel) => _api(ref).telegram(cancel);

  @override
  List<TelegramLink> decode(Object? json) => [for (final e in json! as List<Object?>) TelegramLink.fromJson(e! as Map<String, Object?>)];

  @override
  Object? encode(List<TelegramLink> data) => [for (final l in data) l.toJson()];
}

final telegramBlock = NotifierProvider.autoDispose<TelegramBlock, BlockState<List<TelegramLink>>>(TelegramBlock.new);

/// «Что присылать»: переключение — сразу на экране (оптимистично) и через очередь операций: нет сети — уйдёт позже;
/// сервер отказал — переключатель возвращается, пользователь видит причину. Операция идемпотентна (PUT всего списка).
class NotificationPrefsBlock extends CachedBlock<NotificationPrefs> {
  @override
  String get cacheKey => 'profile/notification-prefs';

  @override
  Future<NotificationPrefs> fetch(CancelToken cancel) => _api(ref).prefs(cancel);

  @override
  NotificationPrefs decode(Object? json) => NotificationPrefs.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(NotificationPrefs data) => data.toJson();

  /// Включить или выключить раздел. Возвращает результат отправки (sent / queued / rejected).
  Future<SendResult> toggle(String category, {required bool enabled}) async {
    final before = state.data;
    if (before == null) return SendResult.rejected;
    final muted = {...before.muted};
    enabled ? muted.remove(category) : muted.add(category);
    final next = before.copyWith(muted: [for (final c in before.categories) if (muted.contains(c)) c]);
    setOptimistic(next);
    final result = await ref.read(outboxProvider).send(
          kind: 'notification.prefs',
          entity: 'notification-prefs',
          method: 'PUT',
          path: '/api/notifications/preferences',
          body: {'muted': next.muted},
        );
    if (result == SendResult.rejected && ref.mounted) setOptimistic(before, failure: const AppFailure(ApiErrorKind.conflict));
    return result;
  }
}

final notificationPrefsBlock =
    NotifierProvider.autoDispose<NotificationPrefsBlock, BlockState<NotificationPrefs>>(NotificationPrefsBlock.new);
