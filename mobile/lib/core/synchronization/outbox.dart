import 'dart:async';
import 'dart:convert';
import 'dart:math' as math;

import 'package:drift/drift.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/api_exception.dart';
import '../api/api_providers.dart';
import '../auth/auth_controller.dart';
import '../core_providers.dart';
import '../database/app_database.dart';
import '../logging/log.dart';
import '../network/connectivity.dart';

/// Операция в очереди.
class PendingOp {
  const PendingOp({
    required this.id,
    required this.kind,
    required this.entity,
    required this.method,
    required this.path,
    this.body,
    required this.createdAt,
    this.attempts = 0,
    this.failed = false,
    this.error,
  });

  final String id;
  final String kind;
  final String entity;
  final String method;
  final String path;
  final Object? body;
  final DateTime createdAt;
  final int attempts;
  final bool failed;
  final String? error;

  factory PendingOp.of(PendingOpRow r) => PendingOp(
        id: r.id,
        kind: r.kind,
        entity: r.entity,
        method: r.method,
        path: r.path,
        body: r.body == null ? null : jsonDecode(r.body!),
        createdAt: r.createdAt,
        attempts: r.attempts,
        failed: r.status == 'failed',
        error: r.lastError,
      );
}

/// Результат отправки: ушла сразу, ждёт сети или сервер отказал.
enum SendResult { sent, queued, rejected }

/// Очередь операций при слабой сети («ничего не потерять»):
/// - операция сначала пишется в SQLite, потом отправляется; нет сети — остаётся в очереди и уходит, когда сеть вернётся или приложение
///   откроется снова (по порядку, по одной);
/// - новая операция над той же сущностью заменяет ждущую (два нажатия «прочитано / не прочитано» — одна отправка);
/// - отказ сервера (4xx) не повторяется: операция помечается и показывается пользователю, интерфейс откатывает оптимистичное изменение;
/// - в очередь попадают только операции, безопасные для повтора (идемпотентные). Финансы и склад — только онлайн.
class Outbox {
  Outbox(this._db, this._api, this._userId, this._clock);

  final AppDatabase _db;
  final ApiClient _api;
  final String? Function() _userId;
  final DateTime Function() _clock;
  final _changes = StreamController<void>.broadcast();
  bool _flushing = false;
  final _rand = math.Random();

  /// Изменения очереди — для счётчика «ждёт отправки».
  Stream<void> get changes => _changes.stream;

  String _newId() => '${_clock().microsecondsSinceEpoch.toRadixString(36)}-${_rand.nextInt(1 << 32).toRadixString(36)}';

  Future<SendResult> send({required String kind, required String entity, required String method, required String path, Object? body}) async {
    final uid = _userId();
    if (uid == null) throw const ApiException(ApiErrorKind.unauthorized);
    final id = _newId();
    await _db.transaction(() async {
      await (_db.delete(_db.pendingOps)..where((t) => t.userId.equals(uid) & t.entity.equals(entity) & t.status.equals('pending'))).go();
      await _db.into(_db.pendingOps).insert(PendingOpsCompanion.insert(
            id: id,
            userId: uid,
            kind: kind,
            entity: entity,
            method: method,
            path: path,
            body: Value(body == null ? null : jsonEncode(body)),
            createdAt: _clock(),
          ));
    });
    _changes.add(null);
    final result = await _sendOne(id);
    _changes.add(null);
    return result;
  }

  Future<SendResult> _sendOne(String id) async {
    final row = await (_db.select(_db.pendingOps)..where((t) => t.id.equals(id))).getSingleOrNull();
    if (row == null) return SendResult.sent;
    final op = PendingOp.of(row);
    try {
      await switch (op.method) {
        'POST' => _api.post<void>(op.path, body: op.body),
        'PUT' => _api.put<void>(op.path, body: op.body),
        'DELETE' => _api.delete<void>(op.path, body: op.body),
        _ => throw ArgumentError(op.method),
      };
      await (_db.delete(_db.pendingOps)..where((t) => t.id.equals(id))).go();
      return SendResult.sent;
    } on ApiException catch (e) {
      final retry = e.isTransient || e.kind == ApiErrorKind.network || e.kind == ApiErrorKind.timeout;
      await (_db.update(_db.pendingOps)..where((t) => t.id.equals(id))).write(PendingOpsCompanion(
            attempts: Value(op.attempts + 1),
            status: Value(retry ? 'pending' : 'failed'),
            lastError: Value(e.message ?? e.kind.name),
          ));
      if (!retry) Log.w('outbox', 'Сервер отказал: ${op.kind} ${op.path} — ${e.kind}');
      return retry ? SendResult.queued : SendResult.rejected;
    }
  }

  /// Отправить ждущие по порядку. Первая неудача по сети останавливает проход — остальные подождут следующего.
  Future<int> flush() async {
    final uid = _userId();
    if (uid == null || _flushing) return 0;
    _flushing = true;
    var sent = 0;
    try {
      final rows = await (_db.select(_db.pendingOps)
            ..where((t) => t.userId.equals(uid) & t.status.equals('pending'))
            ..orderBy([(t) => OrderingTerm.asc(t.createdAt)]))
          .get();
      for (final r in rows) {
        final result = await _sendOne(r.id);
        if (result == SendResult.queued) break;
        if (result == SendResult.sent) sent++;
      }
    } finally {
      _flushing = false;
      _changes.add(null);
    }
    if (sent > 0) Log.i('outbox', 'Отправлено из очереди: $sent');
    return sent;
  }

  Future<List<PendingOp>> list() async {
    final uid = _userId();
    if (uid == null) return const [];
    final rows = await (_db.select(_db.pendingOps)
          ..where((t) => t.userId.equals(uid))
          ..orderBy([(t) => OrderingTerm.asc(t.createdAt)]))
        .get();
    return rows.map(PendingOp.of).toList();
  }

  /// Ждут ли отправки операции над сущностью (экран показывает «отправится при появлении сети»).
  Future<bool> isPending(String entity) async {
    final uid = _userId();
    if (uid == null) return false;
    final row = await (_db.select(_db.pendingOps)..where((t) => t.userId.equals(uid) & t.entity.equals(entity))).getSingleOrNull();
    return row != null;
  }

  Future<void> dismiss(String id) async {
    await (_db.delete(_db.pendingOps)..where((t) => t.id.equals(id))).go();
    _changes.add(null);
  }

  /// Выход и смена пользователя — очередь чужого пользователя удаляется вместе с кэшем.
  Future<void> clear({String? keepUserId}) async {
    final q = _db.delete(_db.pendingOps);
    if (keepUserId != null) q.where((t) => t.userId.equals(keepUserId).not());
    await q.go();
    _changes.add(null);
  }

  void dispose() => _changes.close();
}

/// Удалить очередь операций (выход — вся; вход другого пользователя — чужая), не создавая Outbox.
Future<void> clearPendingOps(AppDatabase db, {String? keepUserId}) async {
  final q = db.delete(db.pendingOps);
  if (keepUserId != null) q.where((t) => t.userId.equals(keepUserId).not());
  await q.go();
}

final outboxProvider = Provider<Outbox>((ref) {
  final outbox = Outbox(
    ref.watch(appDatabaseProvider),
    ref.watch(apiClientProvider),
    () => ref.read(currentProfileProvider)?.id,
    ref.watch(clockProvider),
  );
  // Сеть вернулась — отправить ждущие.
  ref.listen<AsyncValue<bool>>(isOnlineProvider, (prev, next) {
    if (prev?.value == false && next.value == true) unawaited(outbox.flush());
  });
  ref.onDispose(outbox.dispose);
  return outbox;
});

/// Сколько операций ждут отправки (для плашки в профиле и «Синхронизации»).
final pendingOpsProvider = StreamProvider<List<PendingOp>>((ref) async* {
  final outbox = ref.watch(outboxProvider);
  yield await outbox.list();
  await for (final _ in outbox.changes) {
    yield await outbox.list();
  }
});
