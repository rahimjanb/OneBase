import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/sse.dart';
import 'consultant_models.dart';

/// Запросы консультанта. `POST /api/ai/chat` — поток SSE и **платный** вызов модели: только по нажатию пользователя, без повтора
/// и без очереди. Голос сервера (платно) не используется.
class ConsultantApi {
  ConsultantApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  Future<AiStatus> status(CancelToken c) => _api.get('/api/ai/status', (j) => AiStatus.fromJson(_map(j)), cancel: c);

  Future<List<ConversationSummary>> conversations(String q, CancelToken c) => _api.get(
        '/api/ai/conversations',
        (j) => [for (final e in j! as List<Object?>) ConversationSummary.fromJson(_map(e))],
        query: {'q': q.trim().isEmpty ? null : q.trim()},
        cancel: c,
      );

  Future<ConversationView> conversation(String id, CancelToken? c) =>
      _api.get('/api/ai/conversations/$id', (j) => ConversationView.fromJson(_map(j)), cancel: c);

  Future<void> remove(String id) => _api.delete<void>('/api/ai/conversations/$id');

  Future<void> feedback(String messageId, String rating, {String? comment}) =>
      _api.post<void>('/api/ai/messages/$messageId/feedback', body: {'rating': rating, 'comment': ?comment});

  /// Вопрос консультанту: поток событий `start → (progress | draft)* → done | error`. Ошибки до потока (400, 403, 429) — [ApiException].
  Stream<SseEvent> chat({String? conversationId, required String message, Map<String, Object?>? context, required CancelToken cancel, void Function()? onPing}) async* {
    final Response<ResponseBody> r;
    try {
      r = await _api.dio.post<ResponseBody>(
        '/api/ai/chat',
        data: {'conversationId': conversationId, 'message': message, 'context': ?context},
        cancelToken: cancel,
        options: Options(
          responseType: ResponseType.stream,
          headers: {'Accept': 'text/event-stream'},
          receiveTimeout: const Duration(seconds: 60),
          extra: {'noRetry': true},
        ),
      );
    } on DioException catch (e) {
      // Тело ошибки до потока — поток байтов: прочитать и разобрать {error}.
      final body = e.response?.data;
      if (body is ResponseBody) {
        final text = await utf8.decodeStream(body.stream);
        Object? json;
        try {
          json = jsonDecode(text);
        } on Object {
          json = text;
        }
        throw ApiException(ApiException.kindOf(e.response?.statusCode), status: e.response?.statusCode, message: ApiException.serverMessage(json));
      }
      throw ApiException.fromDio(e);
    }
    yield* r.data!.stream.cast<List<int>>().transform(SseParser(onPing: onPing));
  }
}

final consultantApiProvider = Provider<ConsultantApi>((ref) => ConsultantApi(ref.watch(apiClientProvider)));
