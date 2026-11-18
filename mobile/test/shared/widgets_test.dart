import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/errors/app_failure.dart';
import 'package:onebase_mobile/shared/widgets/ob_button.dart';
import 'package:onebase_mobile/shared/widgets/ob_card.dart';
import 'package:onebase_mobile/shared/widgets/ob_text_field.dart';
import 'package:onebase_mobile/shared/widgets/skeleton.dart';
import 'package:onebase_mobile/shared/widgets/states.dart';
import 'package:onebase_mobile/shared/widgets/sync_label.dart';

import '../helpers/harness.dart';

void main() {
  testWidgets('ErrorView: сбой сети — текст и «Повторить»', (tester) async {
    var retried = 0;
    await pumpInApp(
      tester,
      ErrorView(failure: const AppFailure(ApiErrorKind.network), onRetry: () => retried++),
    );
    expect(find.text('Нет соединения с сервером. Проверьте интернет.'), findsOneWidget);
    await tester.tap(find.text('Повторить'));
    expect(retried, 1);
  });

  testWidgets('ErrorView: нет доступа — текст сервера, без «Повторить»', (tester) async {
    await pumpInApp(
      tester,
      ErrorView(
        failure: const AppFailure(ApiErrorKind.forbidden, serverMessage: 'Раздел «Финансы» вам не открыт.'),
        onRetry: () {},
        compact: true,
      ),
    );
    expect(find.text('Раздел «Финансы» вам не открыт.'), findsOneWidget);
    expect(find.text('Повторить'), findsNothing);
  });

  testWidgets('EmptyView: заголовок, пояснение, действие', (tester) async {
    await pumpInApp(
      tester,
      EmptyView(title: 'Нет данных', message: 'Данных 1С пока нет', action: ObButton(label: 'Настроить', onPressed: () {})),
    );
    expect(find.text('Нет данных'), findsOneWidget);
    expect(find.text('Данных 1С пока нет'), findsOneWidget);
    expect(find.text('Настроить'), findsOneWidget);
  });

  testWidgets('ObButton: при загрузке не нажимается и показывает крутилку', (tester) async {
    var taps = 0;
    await pumpInApp(tester, ObButton(label: 'Войти', loading: true, onPressed: () => taps++));
    await tester.tap(find.text('Войти'));
    expect(taps, 0);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('ObCard с onTap нажимается целиком', (tester) async {
    var taps = 0;
    await pumpInApp(tester, ObCard(onTap: () => taps++, child: const Text('Выручка')));
    await tester.tap(find.text('Выручка'));
    expect(taps, 1);
  });

  testWidgets('ObTextField: пароль скрыт, кнопка показывает', (tester) async {
    await pumpInApp(tester, const ObTextField(label: 'Пароль', obscure: true, showLabel: 'Показать', hideLabel: 'Скрыть'));
    TextField field() => tester.widget<TextField>(find.byType(TextField));
    expect(field().obscureText, isTrue);
    await tester.tap(find.byTooltip('Показать'));
    await tester.pump();
    expect(field().obscureText, isFalse);
  });

  testWidgets('SkeletonCard и SyncLabel рисуются в тёмной теме', (tester) async {
    await pumpInApp(
      tester,
      Column(
        children: [
          const SkeletonCard(),
          SyncLabel(at: DateTime.now().subtract(const Duration(minutes: 3))),
        ],
      ),
      brightness: Brightness.dark,
    );
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Обновлено 3 мин назад'), findsOneWidget);
  });
}
