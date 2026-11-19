import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:fake_async/fake_async.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/api/api_providers.dart';
import 'package:onebase_mobile/core/api/sse.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';
import 'package:onebase_mobile/features/ai_consultant/data/consultant_models.dart';
import 'package:onebase_mobile/features/ai_consultant/presentation/ai_screen.dart';
import 'package:onebase_mobile/features/ai_consultant/presentation/consultant_controller.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';
import 'package:onebase_mobile/shared/widgets/markdown_view.dart';

import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

Uint8List bytes(String s) => Uint8List.fromList(utf8.encode(s));

String frame(String event, Object data) => 'event: $event\ndata: ${jsonEncode(data)}\n\n';

ResponseBody sseBody(Stream<Uint8List> chunks) => ResponseBody(chunks, 200, headers: {
      Headers.contentTypeHeader: ['text/event-stream'],
    });

Map<String, Object?> userMsg(String text) => {'id': 'm1', 'role': 'user', 'content': text, 'createdAt': '2026-11-20T09:00:00Z'};

final answerMsg = <String, Object?>{
  'id': 'm2',
  'role': 'assistant',
  'content': '## Итог\nПлан выполнен на **92%**.',
  'createdAt': '2026-11-20T09:00:05Z',
  'durationMs': 4200,
  'answer': {
    'blocks': [
      {'type': 'summary', 'text': 'План выполнен на **92%**.', 'progress': {'label': 'План месяца', 'fact': 92, 'plan': 100, 'unit': 'т'}},
      {
        'type': 'kpis',
        'items': [
          {'label': 'Выручка', 'value': '4,5 млрд', 'delta': '+8%', 'tone': 'good'},
          {'label': 'АКБ', 'value': '1 250', 'tone': 'neutral'},
        ],
      },
      {'type': 'deviations', 'items': [{'title': 'Самарканд', 'text': 'Ниже плана на 12%', 'severity': 'high'}]},
      {'type': 'recommendations', 'items': [{'title': 'Проверить отгрузки', 'owner': 'РМ Самарканд', 'dueDate': '25.11'}]},
      {'type': 'unknown'},
    ],
    'followUps': ['А по регионам?'],
    'meta': {'facts': 3, 'recommendations': 1},
  },
  'details': {
    'sources': [{'title': 'Продажи', 'period': 'ноябрь 2026', 'href': '/sales'}],
    'grounding': {'checked': 5, 'supported': 5},
  },
};

ProviderContainer containerFor(FakeServer server) {
  final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server.adapter;
  return ProviderContainer.test(overrides: [apiClientProvider.overrideWithValue(ApiClient(dio, parseInBackground: false))]);
}

void main() {
  group('SseParser', () {
    test('многобайтные символы и \\r\\n, разорванные между кусками; пульс; несколько data', () async {
      final raw = utf8.encode(': ping\r\n\r\nevent: draft\r\ndata: {"text":"Привет"}\r\n\r\nevent: x\ndata: a\ndata: b\n\nevent: empty\n\n');
      // Режем по одному байту — «П» (2 байта) и \r\n обязательно разорвутся.
      final chunks = [for (final b in raw) [b]];
      var pings = 0;
      final events = await Stream<List<int>>.fromIterable(chunks).transform(SseParser(onPing: () => pings++)).toList();
      expect(pings, 1);
      expect([for (final e in events) (e.event, e.data)], [('draft', '{"text":"Привет"}'), ('x', 'a\nb')]);
    });

    test('последний кадр без пустой строки в конце — не теряется', () async {
      final events = await Stream<List<int>>.value(utf8.encode('event: done\ndata: {}')).transform(const SseParser()).toList();
      expect(events.single.event, 'done');
    });
  });

  test('AnswerView: блоки, уточнения, источники, проверка чисел; без структуры — Markdown', () {
    final a = AnswerView.of(ChatMessage.fromJson(answerMsg));
    expect(a.blocks.map((b) => b.runtimeType), [SummaryBlock, KpisBlock, DeviationsBlock, RecommendationsBlock]);
    expect((a.facts, a.recommendations, a.checked, a.supported), (3, 1, 5, 5));
    expect(a.followUps, ['А по регионам?']);
    expect(a.sources.single.href, '/sales');
    expect((a.blocks[2] as DeviationsBlock).items.single.high, isTrue);

    final plain = AnswerView.of(const ChatMessage(id: 'x', role: 'assistant', content: 'Просто текст', createdAt: '2026-11-20T09:00:00Z'));
    expect(plain.blocks.single, isA<MarkdownBlock>());
  });

  test('поток ответа: start → шаги → черновик → done; черновик и шаги очищаются', () async {
    final chunks = StreamController<Uint8List>();
    final server = FakeServer()..on('POST', '/api/ai/chat', (_) => sseBody(chunks.stream));
    final c = containerFor(server);
    c.listen(consultantProvider(null), (_, _) {});
    final done = c.read(consultantProvider(null).notifier).ask('Как продажи?', context: {'period': '2026-11'});
    await pumpEventQueue();
    expect(c.read(consultantProvider(null)).phase, ChatPhase.sending);
    expect(c.read(consultantProvider(null)).pendingQuestion, 'Как продажи?');

    chunks.add(bytes(frame('start', {'conversationId': 'c1', 'title': 'Продажи ноября', 'userMessage': userMsg('Как продажи?')})));
    chunks.add(bytes(frame('progress', {'stage': 'agent', 'agent': 'sales', 'agentName': 'Аналитик продаж', 'status': 'Running'})));
    chunks.add(bytes(frame('draft', {'kind': 'text', 'text': 'План вып'})));
    chunks.add(bytes(frame('draft', {'kind': 'text', 'text': 'олнен'})));
    await pumpEventQueue();
    var s = c.read(consultantProvider(null));
    expect((s.phase, s.conversationId, s.title, s.pendingQuestion), (ChatPhase.streaming, 'c1', 'Продажи ноября', null));
    expect(s.messages.single.role, 'user');
    expect(s.steps['agent:sales']?.agentName, 'Аналитик продаж');
    expect(s.draft, 'План выполнен');

    chunks.add(bytes(frame('draft', {'kind': 'checking'})));
    await pumpEventQueue();
    expect(c.read(consultantProvider(null)).draftMode, DraftMode.checking);

    chunks.add(bytes(frame('done', {'message': answerMsg})));
    await chunks.close();
    await done;
    s = c.read(consultantProvider(null));
    expect((s.phase, s.draft, s.steps.isEmpty, s.messages.length), (ChatPhase.idle, '', true, 2));
    final body = server.requests.single.data;
    expect(body is String ? jsonDecode(body) : body, {'conversationId': null, 'message': 'Как продажи?', 'context': {'period': '2026-11'}});
  });

  test('ошибка до потока (429): текст сервера, вопрос остаётся, повтора нет', () async {
    final server = FakeServer()..json('POST', '/api/ai/chat', {'error': 'Дневной лимит AI исчерпан'}, status: 429);
    final c = containerFor(server);
    c.listen(consultantProvider(null), (_, _) {});
    await c.read(consultantProvider(null).notifier).ask('Как продажи?');
    final s = c.read(consultantProvider(null));
    expect(s.phase, ChatPhase.failed);
    expect((s.failure?.kind, s.failure?.serverMessage), (ApiErrorKind.rateLimited, 'Дневной лимит AI исчерпан'));
    expect(s.pendingQuestion, 'Как продажи?');
    expect(server.count('POST', '/api/ai/chat'), 1);
  });

  test('событие error: сообщение об ошибке в чате, состояние «ошибка»', () async {
    final server = FakeServer()
      ..on('POST', '/api/ai/chat', (_) => sseBody(Stream.fromIterable([
            bytes(frame('start', {'conversationId': 'c1', 'title': 'Вопрос', 'userMessage': userMsg('Вопрос')})),
            bytes(frame('error', {
              'error': 'Модель недоступна',
              'message': {'id': 'm2', 'role': 'assistant', 'status': 'failed', 'error': 'Модель недоступна', 'createdAt': '2026-11-20T09:00:05Z'},
            })),
          ])));
    final c = containerFor(server);
    c.listen(consultantProvider(null), (_, _) {});
    await c.read(consultantProvider(null).notifier).ask('Вопрос');
    final s = c.read(consultantProvider(null));
    expect(s.phase, ChatPhase.failed);
    expect(s.messages.last.status, 'failed');
    expect(s.failure?.serverMessage, 'Модель недоступна');
  });

  test('обрыв после start: экран опрашивает чат, пока сервер не допишет ответ', () {
    fakeAsync((async) {
      final server = FakeServer()
        ..on('POST', '/api/ai/chat', (o) {
          Stream<Uint8List> broken() async* {
            yield bytes(frame('start', {'conversationId': 'c1', 'title': 'Вопрос', 'userMessage': userMsg('Вопрос')}));
            throw DioException.connectionError(requestOptions: o, reason: 'обрыв');
          }

          return sseBody(broken());
        })
        ..json('GET', '/api/ai/conversations/c1', {'id': 'c1', 'title': 'Вопрос', 'messages': [userMsg('Вопрос')]});
      final c = containerFor(server);
      c.listen(consultantProvider(null), (_, _) {});
      c.read(consultantProvider(null).notifier).ask('Вопрос');
      async.elapse(const Duration(milliseconds: 100));
      expect(c.read(consultantProvider(null)).phase, ChatPhase.recovering);
      async.elapse(const Duration(seconds: 4));
      expect(server.count('GET', '/api/ai/conversations/c1'), 1);
      expect(c.read(consultantProvider(null)).phase, ChatPhase.recovering);

      server.json('GET', '/api/ai/conversations/c1', {'id': 'c1', 'title': 'Вопрос', 'messages': [userMsg('Вопрос'), answerMsg]});
      async.elapse(const Duration(seconds: 7));
      final s = c.read(consultantProvider(null));
      expect((s.phase, s.messages.length), (ChatPhase.idle, 2));
      expect(server.count('POST', '/api/ai/chat'), 1);
    });
  });

  testWidgets('экран: быстрые вопросы по правам; ?ask= только подставляет вопрос; сохранённый чат — блоки ответа', (tester) async {
    tester.view.physicalSize = const Size(1179, 2556);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final db = memoryDb();
    addTearDown(db.close);
    final server = FakeServer()
      ..json('GET', '/api/ai/status', {'ready': true})
      ..json('GET', '/api/ai/conversations/c1', {'id': 'c1', 'title': 'Продажи ноября', 'messages': [userMsg('Как продажи?'), answerMsg]});
    Widget app(Widget home) => ProviderScope(
          retry: (_, _) => null,
          overrides: [
            ...baseOverrides(db: db, adapter: server.adapter),
            currentProfileProvider.overrideWithValue(const UserProfile(id: 'u-1', login: 'u', name: 'Тест', permissions: ['sales.read', 'ai.agents.run'])),
          ],
          child: MaterialApp(
            theme: ObTheme.dark(),
            locale: const Locale('ru'),
            supportedLocales: AppLocalizations.supportedLocales,
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            home: home,
          ),
        );

    await tester.pumpWidget(app(const AiScreen(ask: 'Почему упал Самарканд?')));
    await tester.pumpAndSettle();
    final l10n = AppLocalizations.of(tester.element(find.byType(AiScreen)));
    expect(find.text(l10n.aiQ1), findsOneWidget);
    expect(find.text(l10n.aiQ7), findsNothing); // нет finance.read
    expect(find.text('Почему упал Самарканд?'), findsOneWidget);
    expect(server.count('POST', '/api/ai/chat'), 0);

    await tester.pumpWidget(app(const AiScreen(key: ValueKey('c1'), conversationId: 'c1')));
    await tester.pumpAndSettle();
    expect(find.text('Продажи ноября'), findsOneWidget);
    // Открытый чат прокручен к последнему ответу: внизу уточнения и проверка чисел, выше — блоки и вопрос.
    expect(find.text('↳ А по регионам?'), findsOneWidget);
    expect(find.textContaining(l10n.aiChecked(5, 5)), findsOneWidget);
    final list = find.descendant(of: find.byType(ListView), matching: find.byType(Scrollable)).first;
    for (final t in ['Проверить отгрузки', 'Самарканд', 'Выручка', 'Как продажи?']) {
      await tester.scrollUntilVisible(find.text(t), -150, scrollable: list);
      expect(find.text(t), findsOneWidget);
    }
    expect(server.count('POST', '/api/ai/chat'), 0);
  });

  testWidgets('Markdown: пункт списка — один абзац, мягкий перенос — пробел; таблица рисуется', (tester) async {
    await pumpInApp(tester, const MarkdownView('- **Разные проблемы.**\n  Каримов опаздывает каждый день.\n\n| Отдел | Доля |\n|---|---:|\n| Продажи | 12% |'));
    expect(find.text('Разные проблемы. Каримов опаздывает каждый день.', findRichText: true), findsOneWidget);
    expect(find.text('12%', findRichText: true), findsOneWidget);
  });
}
