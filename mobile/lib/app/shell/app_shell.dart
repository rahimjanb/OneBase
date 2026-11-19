import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'app_tab.dart';
import 'bottom_nav.dart';

/// Оболочка с нижней панелью веба. У каждой вкладки свой стек экранов (StatefulShellRoute): переход между вкладками не теряет
/// прокрутку и открытые экраны; повторное нажатие на текущую вкладку — к её началу. Разделы из веера открываются внутри «Главной».
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.shell, required this.nav, required this.location});

  final StatefulNavigationShell shell;
  final NavLayout nav;
  final String location;

  @override
  Widget build(BuildContext context) {
    final branch = AppTab.values[shell.currentIndex];
    // Раздел из веера (/home/finance …) — ни одна вкладка не подсвечена, как у веба.
    final inModule = branch == AppTab.home && location != AppTab.home.path;
    return ObNavScaffold(
      body: shell,
      tabs: nav.tabs,
      current: inModule ? null : branch,
      currentLocation: location,
      fan: nav.fan,
      onTab: (tab) => shell.goBranch(tab.index, initialLocation: tab == branch),
      onFan: (entry) => context.go(entry.location),
    );
  }
}
