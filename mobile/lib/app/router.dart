import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/auth_controller.dart';
import '../features/ai_consultant/ai_routes.dart';
import '../features/analytics/analytics_routes.dart';
import '../features/auth/presentation/login_screen.dart';
import '../features/auth/presentation/splash_screen.dart';
import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/profile/profile_routes.dart';
import '../features/tasks/tasks_routes.dart';
import 'module_routes.dart';
import 'shell/app_shell.dart';
import 'shell/app_tab.dart';

abstract final class Routes {
  static const splash = '/splash';
  static const login = '/login';
}

/// Навигация пользователя (вкладки и веер) — по его правам.
final navLayoutProvider = Provider<NavLayout>((ref) => navFor(ref.watch(currentProfileProvider)));

/// Куда отправить по состоянию входа. Чистая функция — проверяется тестами.
/// - Проверка входа или сбой проверки → экран проверки.
/// - Не вошёл → экран входа.
/// - Вошёл → с экранов входа на главную; во вкладку или раздел без права → на главную.
String? authRedirect(AuthState auth, String location, NavLayout nav) {
  final atLogin = location == Routes.login;
  final atSplash = location == Routes.splash;
  switch (auth) {
    case AuthChecking() || AuthRestoreFailed():
      return atSplash ? null : Routes.splash;
    case AuthSignedOut():
      return atLogin ? null : Routes.login;
    case AuthSignedIn():
      if (atLogin || atSplash) return AppTab.home.path;
      bool under(String path) => location == path || location.startsWith('$path/');
      final tab = AppTab.values.where((t) => under(t.path)).firstOrNull;
      if (tab != null && tab != AppTab.home && !nav.canOpenTab(tab)) return AppTab.home.path;
      final module = AppModule.values.where((m) => under(m.path)).firstOrNull;
      if (module != null && !nav.canOpenModule(module)) return AppTab.home.path;
      return null;
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  // Роутер создаётся один раз; изменения входа и прав доходят до него через refreshListenable.
  final auth = _RouterRefresh(ref.read(authControllerProvider));
  ref.listen(authControllerProvider, (_, next) => auth.update(next));
  ref.listen(navLayoutProvider, (_, _) => auth.ping());

  final router = GoRouter(
    initialLocation: Routes.splash,
    refreshListenable: auth,
    redirect: (context, state) => authRedirect(auth.value, state.matchedLocation, ref.read(navLayoutProvider)),
    routes: [
      GoRoute(path: Routes.splash, builder: (context, state) => const SplashScreen()),
      GoRoute(path: Routes.login, builder: (context, state) => const LoginScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => Consumer(
          builder: (context, ref, _) => AppShell(shell: shell, nav: ref.watch(navLayoutProvider), location: state.uri.path),
        ),
        branches: [
          for (final tab in AppTab.values)
            StatefulShellBranch(
              routes: [
                switch (tab) {
                  AppTab.home => GoRoute(path: tab.path, builder: (context, state) => const DashboardScreen(), routes: moduleRoutes),
                  AppTab.ai => aiRoute,
                  AppTab.analytics => analyticsRoute,
                  AppTab.tasks => tasksRoute,
                  AppTab.profile => profileRoute,
                },
              ],
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
