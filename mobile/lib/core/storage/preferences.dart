import 'dart:ui' show PlatformDispatcher;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core_providers.dart';
import 'settings_store.dart';

/// Языки интерфейса: русский и узбекский (латиница) — как у веба.
const supportedLocales = [Locale('ru'), Locale('uz')];

/// Тема: как в системе, светлая или тёмная. Сохраняется на устройстве.
class ThemeModeController extends Notifier<ThemeMode> {
  @override
  ThemeMode build() {
    final saved = ref.watch(initialSettingsProvider)[SettingsStore.themeKey];
    return ThemeMode.values.firstWhere((m) => m.name == saved, orElse: () => ThemeMode.system);
  }

  Future<void> set(ThemeMode mode) async {
    state = mode;
    await ref.read(settingsStoreProvider).write(SettingsStore.themeKey, mode == ThemeMode.system ? null : mode.name);
  }
}

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);

/// Язык, выбранный в профиле; null — язык системы (если он русский или узбекский, иначе русский).
class LocaleController extends Notifier<Locale?> {
  @override
  Locale? build() {
    final saved = ref.watch(initialSettingsProvider)[SettingsStore.localeKey];
    return supportedLocales.where((l) => l.languageCode == saved).firstOrNull;
  }

  Future<void> set(Locale? locale) async {
    state = locale;
    await ref.read(settingsStoreProvider).write(SettingsStore.localeKey, locale?.languageCode);
  }
}

final localeProvider = NotifierProvider<LocaleController, Locale?>(LocaleController.new);

/// Язык интерфейса — одно правило и для экрана, и для сервера (`Accept-Language`): выбранный в профиле; иначе язык системы, если он
/// русский или узбекский; иначе русский. Регион системы не учитывается (английский с регионом «Узбекистан» — это русский интерфейс).
Locale resolveLocale(Locale? chosen, [Locale? system]) {
  if (chosen != null) return chosen;
  final sys = system ?? PlatformDispatcher.instance.locale;
  return supportedLocales.firstWhere((l) => l.languageCode == sys.languageCode, orElse: () => supportedLocales.first);
}

/// Язык, на котором сервер отвечает текстами ошибок и подписями.
String effectiveLanguage(Locale? chosen, [Locale? system]) => resolveLocale(chosen, system).languageCode;
