import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:onebase_mobile/core/localization/formatters.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';

/// Неразрывный пробел разрядов — сравниваем с обычным, чтобы тест читался.
String plain(String s) => s.replaceAll(' ', ' ').replaceAll(' ', ' ');

void main() {
  setUpAll(() async {
    await initializeDateFormatting('ru');
    await initializeDateFormatting('uz');
  });

  final ru = Fmt(lookupAppLocalizations(const Locale('ru')));
  final uz = Fmt(lookupAppLocalizations(const Locale('uz')));

  test('деньги кратко', () {
    expect(plain(ru.money(4535279646.16)), '4,54 млрд сум');
    expect(plain(ru.money(584970.93)), '585 тыс. сум');
    expect(plain(ru.money(12500000)), '12,5 млн сум');
    expect(plain(ru.money(950)), '950 сум');
    expect(plain(ru.money(-1200000000)), '−1,2 млрд сум');
    expect(plain(uz.money(4535279646.16)), "4,54 mlrd so'm");
  });

  test('вес', () {
    expect(plain(ru.weight(119977.235)), '120 т');
    expect(plain(ru.weight(1500)), '1,5 т');
    expect(plain(ru.weight(950)), '950 кг');
    expect(plain(uz.weight(1500)), '1,5 t');
  });

  test('проценты и изменение', () {
    expect(ru.percent(0.7600366), '76%');
    expect(ru.percent(0.042), '4,2%');
    expect(ru.percent(0), '0%');
    expect(ru.signedPercent(0.021), '+2,1%');
    expect(ru.signedPercent(-0.1318), '−13%');
  });

  test('целые с разрядами', () {
    expect(plain(ru.count(7753)), '7 753');
    expect(plain(ru.count(16518.4)), '16 518');
  });

  test('месяц', () {
    expect(ru.monthYear(2026, 11), 'Ноябрь 2026');
    expect(uz.month(2026, 11).toLowerCase(), contains('noyabr'));
  });

  test('когда обновлено', () {
    final now = DateTime(2026, 11, 19, 12, 0);
    expect(ru.synced(now.subtract(const Duration(seconds: 20)), now), 'Обновлено только что');
    expect(ru.synced(now.subtract(const Duration(minutes: 5)), now), 'Обновлено 5 мин назад');
    expect(ru.synced(now.subtract(const Duration(hours: 2)), now), 'Обновлено 2 ч назад');
    expect(ru.synced(DateTime(2026, 11, 18, 21, 31), now), 'Обновлено 18 ноября, 21:31');
    expect(uz.synced(now.subtract(const Duration(minutes: 5)), now), '5 daqiqa oldin yangilandi');
  });
}
