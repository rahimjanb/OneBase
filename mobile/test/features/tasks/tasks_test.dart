import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/features/hr/data/hr_models.dart';
import 'package:onebase_mobile/features/tasks/data/tasks_api.dart';
import 'package:onebase_mobile/features/tasks/presentation/tasks_screens.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations_ru.dart';

import '../../helpers/fake_adapter.dart';
import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

Map<String, Object?> task({String status = 'Review', List<String> next = const ['InProgress', 'Done', 'Cancelled']}) => {
      'id': 't1', 'title': 'Сверить остатки', 'priority': 'High', 'status': status, 'dueDate': '2026-11-21', 'overdue': false, //
      'assigneeName': 'Иван Петров', 'createdAt': '2026-11-19T08:00:00+00:00', 'nextStatuses': next,
      'departmentCode': 'production', 'departmentName': 'Производство', 'progress': 40, 'checklistDone': 2, 'checklistTotal': 5,
      'canManage': true, 'canWork': true,
    };

void main() {
  final l10n = AppLocalizationsRu();

  test('подписи переходов — как у веба', () {
    expect(actionLabel(l10n, 'Review', 'InProgress'), 'Вернуть на доработку');
    expect(actionLabel(l10n, 'Review', 'Done'), 'Принять результат');
    expect(actionLabel(l10n, 'Done', 'InProgress'), 'Вернуть в работу');
    expect(actionLabel(l10n, 'New', 'InProgress'), 'В работу');
    expect(actionLabel(l10n, 'InProgress', 'Review'), 'Отправить на проверку');
    expect(actionLabel(l10n, 'New', 'Cancelled'), 'Отменить');
  });

  test('HR: имена Verifix — с заглавной, отметки по времени в карточку не попадают', () {
    expect(displayName("ABDUG'ANIYEV  ABDUSATTOR"), "Abdug'aniyev Abdusattor");
    final card = hrCardOf({
      'employee': {'id': 'e1', 'fullName': 'IVANOV IVAN', 'status': 'W'},
      'from': '2026-11-07',
      'to': '2026-11-20',
      'days': [{'date': '2026-11-19', 'status': 'Present', 'late': true, 'lateMinutes': 9}],
      'events': [for (var i = 0; i < 100; i++) {'id': i}],
      'summary': {'planDays': 12, 'presentDays': 10, 'lateDays': 10},
    });
    expect(card.days.single.lateMinutes, 9);
    expect(card.toJson().toString(), isNot(contains('events')));
  });

  test('параметры списка задач: «мои» — scope=my&mine=true; отдел — с кодом; архив — без статуса', () async {
    final server = FakeServer()..json('GET', '/api/work/tasks', {'items': <Object?>[], 'total': 0, 'page': 1, 'pageSize': 25});
    final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server.adapter;
    final api = TasksApi(ApiClient(dio, parseInBackground: false));
    await api.list(const TaskQuery(), 1, CancelToken());
    await api.list(const TaskQuery(scope: 'department', department: 'hr', status: 'overdue', search: ' отчёт '), 1, CancelToken());
    await api.list(const TaskQuery(scope: 'archive'), 2, CancelToken());
    final q = [for (final r in server.requests) r.queryParameters];
    expect((q[0]['scope'], q[0]['mine'], q[0]['status']), ('my', true, 'open'));
    expect((q[1]['department'], q[1]['status'], q[1]['search']), ('hr', 'overdue', 'отчёт'));
    expect((q[2]['status'], q[2]['page']), (null, 2));
  });

  testWidgets('возврат на доработку требует причину; комментарий уходит с переходом', (tester) async {
    tester.view.physicalSize = const Size(1179, 2556);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final db = memoryDb();
    addTearDown(db.close);
    Object? sent;
    final server = FakeServer()
      ..json('GET', '/api/work/tasks/t1', {'task': task(), 'checklist': <Object?>[], 'comments': <Object?>[], 'attachments': <Object?>[], 'history': <Object?>[], 'canComment': true, 'canAttach': true})
      ..on('POST', '/api/work/tasks/t1/status', (o) {
        sent = o.data is String ? jsonDecode(o.data as String) : o.data;
        return FakeAdapter.empty();
      });
    await tester.pumpWidget(ProviderScope(
      retry: (_, _) => null,
      overrides: baseOverrides(db: db, adapter: server.adapter, tokens: MemoryTokenStorage(), clock: () => DateTime.utc(2026, 11, 20, 9)),
      child: MaterialApp(
        theme: ObTheme.dark(),
        locale: const Locale('ru'),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const TaskScreen(id: 't1'),
      ),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Сверить остатки'), findsOneWidget);
    expect(find.text('срок завтра'), findsOneWidget);
    expect(find.text('Принять результат'), findsOneWidget);

    await tester.tap(find.text('Вернуть на доработку'));
    await tester.pumpAndSettle();
    // Пусто — перехода нет.
    await tester.tap(find.widgetWithText(FilledButton, 'Вернуть на доработку'));
    await tester.pumpAndSettle();
    expect(sent, isNull);

    await tester.tap(find.text('Вернуть на доработку'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, 'Нет подписи кладовщика');
    await tester.tap(find.widgetWithText(FilledButton, 'Вернуть на доработку'));
    await tester.pumpAndSettle();
    expect(sent, {'status': 'InProgress', 'comment': 'Нет подписи кладовщика'});
  });
}
