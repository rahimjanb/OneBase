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

/// Подождать, пока экран успокоится (без бесконечного ожидания анимаций).
Future<void> settle(WidgetTester tester, [Duration d = const Duration(milliseconds: 800)]) async {
  final end = DateTime.now().add(d);
  while (DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 50));
  }
}

Finder navItem(String label) => find.descendant(of: find.byType(NavigationBar), matching: find.text(label));
