import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/auth/user_profile.dart';
import '../../core/permissions/access.dart';
import '../../l10n/gen/app_localizations.dart';

/// Ветки нижней панели. Порядок — порядок веток в оболочке роутера (индекс ветки = [index]). Видны три вкладки и «Профиль»,
/// какие именно — по правам ([navFor]), как у веба.
enum AppTab {
  home('/home', LucideIcons.house),
  ai('/ai', LucideIcons.sparkles),
  analytics('/analytics', LucideIcons.chartNoAxesColumn),
  tasks('/tasks', LucideIcons.clipboardList),
  profile('/profile', LucideIcons.userRound);

  const AppTab(this.path, this.icon);

  final String path;
  final IconData icon;

  String label(AppLocalizations l10n) => switch (this) {
        AppTab.home => l10n.navHome,
        AppTab.analytics => l10n.navAnalytics,
        AppTab.tasks => l10n.navTasks,
        AppTab.ai => l10n.navAi,
        AppTab.profile => l10n.navProfile,
      };

  /// Короткая подпись на узком экране (уже 375).
  String short(AppLocalizations l10n) => this == AppTab.ai ? 'AI' : label(l10n);

  List<String> get access => switch (this) {
        AppTab.home || AppTab.profile => const [],
        AppTab.ai => Access.consultant,
        AppTab.analytics => Access.analytics,
        AppTab.tasks => Access.tasks,
      };
}

/// Разделы, которые открываются из веера и с главной (внутри ветки «Главная»: нижняя панель остаётся, «назад» ведёт на главную).
enum AppModule {
  departments('departments', LucideIcons.grid3x3, Color(0xFF22B573)),
  sales('sales', LucideIcons.store, Color(0xFF0878FF)),
  finance('finance', LucideIcons.wallet, Color(0xFF14B8A6)),
  production('production', LucideIcons.factory, Color(0xFFF97316)),
  warehouse('warehouse', LucideIcons.warehouse, Color(0xFFFF8A00)),
  requests('requests', LucideIcons.clipboardPen, Color(0xFFF59E0B)),
  hr('hr', LucideIcons.users, Color(0xFF06B6D4)),
  insights('insights', LucideIcons.brainCircuit, Color(0xFF7C3AED)),
  notifications('notifications', LucideIcons.bell, Color(0xFFF43F5E)),
  settings('settings', LucideIcons.slidersHorizontal, Color(0xFF64748B));

  const AppModule(this.segment, this.icon, this.color);

  final String segment;
  final IconData icon;

  /// Цвет значка в веере — у каждого раздела свой, умеренный (как у веба).
  final Color color;

  String get path => '${AppTab.home.path}/$segment';

  String label(AppLocalizations l10n) => switch (this) {
        AppModule.departments => l10n.moduleDepartments,
        AppModule.sales => l10n.moduleSales,
        AppModule.finance => l10n.moduleFinance,
        AppModule.production => l10n.moduleProduction,
        AppModule.warehouse => l10n.moduleWarehouse,
        AppModule.requests => l10n.moduleRequests,
        AppModule.hr => l10n.moduleHr,
        AppModule.insights => l10n.moduleInsights,
        AppModule.notifications => l10n.moduleNotifications,
        AppModule.settings => l10n.moduleSettings,
      };

  List<String> get access => switch (this) {
        AppModule.departments => Access.departments,
        AppModule.sales => Access.sales,
        AppModule.finance => Access.finance,
        AppModule.production => Access.production,
        AppModule.warehouse => Access.warehouse,
        AppModule.requests => Access.requests,
        AppModule.hr => Access.hr,
        AppModule.insights => Access.consultant,
        AppModule.notifications || AppModule.settings => const [],
      };
}

/// Пункт веера: раздел приложения или вкладка, которой нет на панели.
@immutable
class FanEntry {
  const FanEntry({required this.location, required this.icon, required this.color, required this.label});

  final String location;
  final IconData icon;
  final Color color;
  final String Function(AppLocalizations) label;
}

/// Что видно в навигации пользователя.
@immutable
class NavLayout {
  const NavLayout({required this.tabs, required this.fan, required this.permissions});

  /// Три вкладки слева и справа от синей кнопки; «Профиль» — всегда четвёртой.
  final List<AppTab> tabs;
  final List<FanEntry> fan;
  final List<String> permissions;

  bool canOpenTab(AppTab tab) => tab == AppTab.profile || tabs.contains(tab) || Access.canOpen(tab.access, permissions);

  bool canOpenModule(AppModule module) => Access.canOpen(module.access, permissions);
}

/// Навигация по правам — как у веба (`MobileNav`): вкладки «Главная», «Консультант», «Аналитика» (у кого есть), недостающие —
/// из веера («Задачи» первыми); остальное — в веере. Уведомления — колокольчик в шапке, настройки — в «Профиле».
NavLayout navFor(UserProfile? profile) {
  final perms = Access.screenPermissions(profile);
  bool can(List<String> need) => Access.canOpen(need, perms);
  final candidates = [AppTab.home, if (can(AppTab.ai.access)) AppTab.ai, if (can(AppTab.analytics.access)) AppTab.analytics];
  final fill = [if (can(AppTab.tasks.access)) AppTab.tasks];
  final tabs = [...candidates, ...fill].take(3).toList();

  FanEntry tabEntry(AppTab t, Color color) => FanEntry(location: t.path, icon: t.icon, color: color, label: t.label);
  FanEntry moduleEntry(AppModule m) => FanEntry(location: m.path, icon: m.icon, color: m.color, label: m.label);

  final fan = <FanEntry>[
    if (can(AppModule.departments.access)) moduleEntry(AppModule.departments),
    if (!tabs.contains(AppTab.tasks) && can(AppTab.tasks.access)) tabEntry(AppTab.tasks, const Color(0xFF8B5CF6)),
    if (!tabs.contains(AppTab.analytics) && can(AppTab.analytics.access)) tabEntry(AppTab.analytics, const Color(0xFF0878FF)),
    if (!tabs.contains(AppTab.ai) && can(AppTab.ai.access)) tabEntry(AppTab.ai, const Color(0xFF7C3AED)),
    for (final m in AppModule.values)
      if (m != AppModule.departments && m != AppModule.notifications && m != AppModule.settings && can(m.access)) moduleEntry(m),
  ];
  return NavLayout(tabs: tabs, fan: fan, permissions: perms);
}
