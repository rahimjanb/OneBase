import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/paged.dart';
import 'task_models.dart';

/// Фильтр списка: область (my, all, department, archive), статус (open, overdue, blocked, closed или статусы через запятую), отдел, поиск.
class TaskQuery {
  const TaskQuery({this.scope = 'my', this.status = 'open', this.department, this.search = ''});

  final String scope;
  final String? status;
  final String? department;
  final String search;

  TaskQuery copyWith({String? scope, String? Function()? status, String? Function()? department, String? search}) => TaskQuery(
        scope: scope ?? this.scope,
        status: status == null ? this.status : status(),
        department: department == null ? this.department : department(),
        search: search ?? this.search,
      );

  @override
  bool operator ==(Object other) =>
      other is TaskQuery && other.scope == scope && other.status == status && other.department == department && other.search == search;

  @override
  int get hashCode => Object.hash(scope, status, department, search);
}

class TasksApi {
  TasksApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  Future<PageChunk<TaskRow>> list(TaskQuery q, int page, CancelToken c) => _api.get(
        '/api/work/tasks',
        (j) {
          final m = _map(j);
          final total = m['total'] as int? ?? 0;
          final size = m['pageSize'] as int? ?? 25;
          return PageChunk([for (final e in m['items']! as List<Object?>) TaskRow.fromJson(_map(e))], hasMore: page * size < total);
        },
        query: {
          'scope': q.scope,
          if (q.scope == 'my') 'mine': true,
          'status': q.scope == 'archive' ? null : q.status,
          'department': q.scope == 'department' ? q.department : null,
          'search': q.search.trim().isEmpty ? null : q.search.trim(),
          'page': page,
          'pageSize': 25,
        },
        cancel: c,
      );

  Future<TaskDetail> detail(String id, CancelToken c) => _api.get('/api/work/tasks/$id', (j) => TaskDetail.fromJson(_map(j)), cancel: c);

  Future<List<WorkDepartmentRef>> departments(CancelToken c) =>
      _api.get('/api/work/departments', (j) => [for (final e in j! as List<Object?>) WorkDepartmentRef.fromJson(_map(e))], cancel: c);

  Future<List<Assignee>> assignees(String department, CancelToken c) => _api.get(
        '/api/work/assignees',
        (j) => [for (final e in j! as List<Object?>) Assignee.fromJson(_map(e))],
        query: {'department': department},
        cancel: c,
      );

  /// Новая задача. Не идемпотентно — автоматически не повторяется (кнопка блокируется на время запроса).
  Future<String> create(Map<String, Object?> body) => _api.post('/api/work/tasks', body: body, decode: (j) => _map(j)['id']!.toString());

  /// Смена статуса: повтор того же статуса сервер отклоняет — поэтому только при связи, без очереди.
  Future<void> setStatus(String id, String status, {String? comment, String? result}) =>
      _api.post<void>('/api/work/tasks/$id/status', body: {'status': status, 'comment': ?comment, 'result': ?result});

  Future<void> block(String id, {required bool blocked, String? reason}) =>
      _api.post<void>('/api/work/tasks/$id/block', body: {'blocked': blocked, 'reason': reason});

  Future<void> comment(String id, String text) => _api.post<void>('/api/work/tasks/$id/comments', body: {'text': text});

  /// Вложение: multipart, поле `file`, один файл, до 20 МБ.
  Future<void> attach(String id, String name, Uint8List bytes) async {
    try {
      await _api.dio.post<Object?>(
        '/api/work/tasks/$id/attachments',
        data: FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: name)}),
      );
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  /// Файл вложения (Bearer обязателен; сервер отдаёт `no-store` — на диск не кладём, только память или временный файл для «Поделиться»).
  Future<Uint8List> download(String attachmentId) async {
    try {
      final r = await _api.dio.get<List<int>>('/api/work/attachments/$attachmentId', options: Options(responseType: ResponseType.bytes));
      return Uint8List.fromList(r.data ?? const []);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}

final tasksApiProvider = Provider<TasksApi>((ref) => TasksApi(ref.watch(apiClientProvider)));
