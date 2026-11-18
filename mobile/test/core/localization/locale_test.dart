import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/storage/preferences.dart';

void main() {
  test('выбранный язык важнее системного', () {
    expect(resolveLocale(const Locale('uz'), const Locale('ru', 'RU')), const Locale('uz'));
  });

  test('язык системы — если русский или узбекский', () {
    expect(resolveLocale(null, const Locale('uz', 'UZ')).languageCode, 'uz');
    expect(resolveLocale(null, const Locale('ru', 'UZ')).languageCode, 'ru');
  });

  test('английский с регионом «Узбекистан» — русский, и для сервера тоже', () {
    expect(resolveLocale(null, const Locale('en', 'UZ')).languageCode, 'ru');
    expect(effectiveLanguage(null, const Locale('en', 'UZ')), 'ru');
  });
}
