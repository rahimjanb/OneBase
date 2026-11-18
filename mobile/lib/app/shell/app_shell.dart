import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../shared/extensions/context_ext.dart';
import 'app_tab.dart';

/// Оболочка с нижней панелью. У каждой вкладки свой стек экранов (StatefulShellRoute): переход между вкладками не теряет
/// прокрутку и открытые экраны. [tabs] — видимые вкладки (по правам пользователя); повторное нажатие на текущую — к её началу.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.shell, required this.tabs});

  final StatefulNavigationShell shell;
  final List<AppTab> tabs;

  @override
  Widget build(BuildContext context) {
    final current = AppTab.values[shell.currentIndex];
    final selected = tabs.indexOf(current);
    final c = context.ob;
    return Scaffold(
      body: shell,
      bottomNavigationBar: DecoratedBox(
        decoration: BoxDecoration(border: Border(top: BorderSide(color: c.line))),
        child: NavigationBar(
          selectedIndex: selected < 0 ? 0 : selected,
          onDestinationSelected: (i) {
            final tab = tabs[i];
            HapticFeedback.selectionClick();
            shell.goBranch(tab.index, initialLocation: tab == current);
          },
          destinations: [
            for (final t in tabs)
              NavigationDestination(
                icon: Icon(t.icon),
                selectedIcon: Icon(t.selectedIcon),
                label: t.label(context.l10n),
                tooltip: '',
              ),
          ],
        ),
      ),
    );
  }
}
