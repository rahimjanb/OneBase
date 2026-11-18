import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/auth_controller.dart';
import '../core/auth/perm.dart';
import '../core/auth/user_profile.dart';
import '../features/ai_consultant/presentation/ai_screen.dart';
import '../features/analytics/presentation/analytics_screen.dart';
import '../features/auth/presentation/login_screen.dart';
import '../features/auth/presentation/splash_screen.dart';
import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/profile/presentation/profile_screen.dart';
import '../features/tasks/presentation/tasks_screen.dart';
import 'shell/app_shell.dart';
import 'shell/app_tab.dart';

abstract final class Routes {
  static const splash = '/splash';
  static const login = '/login';
}

/// Вкладки, которые открыты пользователю: «Главная» и «Профиль» — всем, остальные — по правам (как разделы веба).
List<AppTab> tabsFor(UserProfile? profile) => [
      for (final tab in AppTab.values)
        if (switch (tab) {
          AppTab.home || AppTab.profile => true,
          AppTab.analytics => profile?.canAny(Perm.analytics) ?? false,
          AppTab.tasks => profile?.can(Perm.workTasks) ?? false,
          AppTab.ai => profile?.can(Perm.agentsRun) ?? false,
        })
          tab,
    ];

final visibleTabsProvider = Provider<List<AppTab>>((ref) => tabsFor(ref.watch(currentProfileProvider)));

/// Куда отправить по состоянию входа. Чистая функция — проверяется тестами.
/// - Проверка входа или сбой проверки → экран проверки.
/// - Не вошёл → экран входа.
/// - Вошёл → с экранов входа на главную; в раздел без права → на главную.
String? authRedirect(AuthState auth, String location, List<AppTab> tabs) {
  final atLogin = location == Routes.login;
  final atSplash = location == Routes.splash;
  switch (auth) {
    case AuthChecking() || AuthRestoreFailed():
      return atSplash ? null : Routes.splash;
    case AuthSignedOut():
      return atLogin ? null : Routes.login;
    case AuthSignedIn():
      if (atLogin || atSplash) return AppTab.home.path;
      final tab = AppTab.values.where((t) => location == t.path || location.startsWith('${t.path}/')).firstOrNull;
      if (tab != null && !tabs.contains(tab)) return AppTab.home.path;
      return null;
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  // Роутер создаётся один раз; изменения входа и прав доходят до него через refreshListenable.
  final auth = _RouterRefresh(ref.read(authControllerProvider));
  ref.listen(authControllerProvider, (_, next) => auth.update(next));
  ref.listen(visibleTabsProvider, (_, _) => auth.ping());

  final router = GoRouter(
    initialLocation: Routes.splash,
    refreshListenable: auth,
    redirect: (context, state) => authRedirect(auth.value, state.matchedLocation, ref.read(visibleTabsProvider)),
    routes: [
      GoRoute(path: Routes.splash, builder: (context, state) => const SplashScreen()),
      GoRoute(path: Routes.login, builder: (context, state) => const LoginScreen()),
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
  ref.onDispose(() {
    router.dispose();
    auth.dispose();
  });
  return router;
});

/// Состояние входа для роутера: меняется вход или права — роутер пересчитывает редирект.
class _RouterRefresh extends ChangeNotifier {
  _RouterRefresh(this.value);

  AuthState value;

  void update(AuthState next) {
    value = next;
    notifyListeners();
  }

  void ping() => notifyListeners();
}

Widget _screen(AppTab tab) => switch (tab) {
      AppTab.home => const DashboardScreen(),
      AppTab.analytics => const AnalyticsScreen(),
      AppTab.tasks => const TasksScreen(),
      AppTab.ai => const AiScreen(),
      AppTab.profile => const ProfileScreen(),
    };
