import 'package:go_router/go_router.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Ждать, пока на экране не появится [finder] (живой сервер отвечает не мгновенно; бесконечные анимации не дают pumpAndSettle).
Future<void> pumpUntil(WidgetTester tester, Finder finder, {Duration timeout = const Duration(seconds: 30)}) async {
  final end = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 100));
    if (finder.evaluate().isNotEmpty) return;
  }
  throw TestFailure('Не дождались: $finder');
}

/// Ждать, пока не появится любой из [finders]; возвращает индекс найденного.
Future<int> pumpUntilAny(WidgetTester tester, List<Finder> finders, {Duration timeout = const Duration(seconds: 30)}) async {
  final end = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 100));
    for (var i = 0; i < finders.length; i++) {
      if (finders[i].evaluate().isNotEmpty) return i;
    }
  }
  throw TestFailure('Не дождались ни одного из: $finders');
}

/// Подождать, пока экран успокоится (без бесконечного ожидания анимаций).
Future<void> settle(WidgetTester tester, [Duration d = const Duration(milliseconds: 800)]) async {
  final end = DateTime.now().add(d);
  while (DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 50));
  }
}

/// Вкладка нижней панели по подписи.
Finder navItem(String label) => find.bySemanticsLabel(label);

/// Вход тестовым пользователем, если сеанса нет (`--dart-define=E2E_LOGIN/E2E_PASSWORD`).
Future<void> signInIfNeeded(WidgetTester tester, Finder signedIn) async {
  const login = String.fromEnvironment('E2E_LOGIN');
  const password = String.fromEnvironment('E2E_PASSWORD');
  final screen = await pumpUntilAny(tester, [signedIn, find.text('Войти')], timeout: const Duration(seconds: 60));
  if (screen == 1) {
    await tester.enterText(find.byType(TextField).at(0), login);
    await tester.enterText(find.byType(TextField).at(1), password);
    await tester.tap(find.text('Войти'));
    await pumpUntil(tester, signedIn, timeout: const Duration(seconds: 60));
  }
}

/// Прокрутить главный список экрана на [dy] и подождать.
Future<void> scrollBy(WidgetTester tester, double dy) async {
  await tester.drag(find.byType(Scrollable).first, Offset(0, -dy));
  await settle(tester, const Duration(milliseconds: 900));
}

/// Перейти по адресу приложения (как по ссылке) и подождать загрузку.
Future<void> goTo(WidgetTester tester, String location, {Duration wait = const Duration(seconds: 4)}) async {
  final context = tester.element(find.byType(Scaffold).first);
  GoRouter.of(context).go(location);
  await settle(tester, wait);
}
