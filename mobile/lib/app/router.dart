import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/ai_consultant/presentation/ai_screen.dart';
import '../features/analytics/presentation/analytics_screen.dart';
import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/profile/presentation/profile_screen.dart';
import '../features/tasks/presentation/tasks_screen.dart';
import 'shell/app_shell.dart';
import 'shell/app_tab.dart';

/// Видимые вкладки нижней панели. На этапе 2 зависят от прав пользователя.
final visibleTabsProvider = Provider<List<AppTab>>((ref) => AppTab.values);

final routerProvider = Provider<GoRouter>((ref) {
  final router = GoRouter(
    initialLocation: AppTab.home.path,
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => Consumer(
          builder: (context, ref, _) => AppShell(shell: shell, tabs: ref.watch(visibleTabsProvider)),
        ),
        branches: [
          for (final tab in AppTab.values)
            StatefulShellBranch(
              routes: [GoRoute(path: tab.path, builder: (context, state) => _screen(tab))],
            ),
        ],
      ),
    ],
  );
  ref.onDispose(router.dispose);
  return router;
});

Widget _screen(AppTab tab) => switch (tab) {
      AppTab.home => const DashboardScreen(),
      AppTab.analytics => const AnalyticsScreen(),
      AppTab.tasks => const TasksScreen(),
      AppTab.ai => const AiScreen(),
      AppTab.profile => const ProfileScreen(),
    };
