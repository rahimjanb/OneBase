import 'package:flutter/material.dart';

import '../../l10n/gen/app_localizations.dart';

/// Пять разделов нижней панели. Порядок — порядок веток в оболочке роутера (индекс ветки = [index]).
enum AppTab {
  home('/home', Icons.space_dashboard_outlined, Icons.space_dashboard_rounded),
  analytics('/analytics', Icons.insights_outlined, Icons.insights_rounded),
  tasks('/tasks', Icons.task_alt_outlined, Icons.task_alt_rounded),
  ai('/ai', Icons.auto_awesome_outlined, Icons.auto_awesome),
  profile('/profile', Icons.person_outline_rounded, Icons.person_rounded);

  const AppTab(this.path, this.icon, this.selectedIcon);

  final String path;
  final IconData icon;
  final IconData selectedIcon;

  String label(AppLocalizations l10n) => switch (this) {
        AppTab.home => l10n.navHome,
        AppTab.analytics => l10n.navAnalytics,
        AppTab.tasks => l10n.navTasks,
        AppTab.ai => l10n.navAi,
        AppTab.profile => l10n.navProfile,
      };
}
