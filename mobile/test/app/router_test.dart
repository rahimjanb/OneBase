import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/router.dart';
import 'package:onebase_mobile/app/shell/app_tab.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';
import 'package:onebase_mobile/core/errors/app_failure.dart';

import '../helpers/fake_server.dart';

UserProfile profile(List<String> permissions) => UserProfile.fromJson(profileJson(permissions: permissions));

void main() {
  group('вкладки по правам', () {
    test('без прав — только «Главная» и «Профиль»', () {
      expect(tabsFor(profile([])), [AppTab.home, AppTab.profile]);
    });

    test('аналитика — при любом аналитическом разделе; задачи и AI — по своим правам', () {
      expect(tabsFor(profile(['finance.read'])), [AppTab.home, AppTab.analytics, AppTab.profile]);
      expect(tabsFor(profile(['work.tasks', 'ai.agents.run'])), [AppTab.home, AppTab.tasks, AppTab.ai, AppTab.profile]);
      expect(tabsFor(profile(profileJson()['permissions']! as List<String>)), AppTab.values);
    });

    test('не вошёл — главная и профиль', () {
      expect(tabsFor(null), [AppTab.home, AppTab.profile]);
    });
  });

  group('редирект по входу', () {
    const all = AppTab.values;
    final signedIn = AuthSignedIn(profile(['sales.read']));

    test('проверка входа и сбой проверки → экран проверки', () {
      expect(authRedirect(const AuthChecking(), '/home', all), Routes.splash);
      expect(authRedirect(const AuthChecking(), Routes.splash, all), isNull);
      expect(authRedirect(const AuthRestoreFailed(AppFailure(ApiErrorKind.network)), '/login', all), Routes.splash);
    });

    test('не вошёл → вход', () {
      expect(authRedirect(const AuthSignedOut(), '/home', all), Routes.login);
      expect(authRedirect(const AuthSignedOut(SignOutReason.expired), '/profile', all), Routes.login);
      expect(authRedirect(const AuthSignedOut(), Routes.login, all), isNull);
    });

    test('вошёл → со входа и проверки на главную', () {
      expect(authRedirect(signedIn, Routes.login, all), '/home');
      expect(authRedirect(signedIn, Routes.splash, all), '/home');
      expect(authRedirect(signedIn, '/profile', all), isNull);
    });

    test('вошёл, но раздел закрыт → на главную', () {
      final tabs = [AppTab.home, AppTab.analytics, AppTab.profile];
      expect(authRedirect(signedIn, '/tasks', tabs), '/home');
      expect(authRedirect(signedIn, '/ai', tabs), '/home');
      expect(authRedirect(signedIn, '/analytics', tabs), isNull);
    });
  });

  test('инициалы и охват продаж', () {
    final p = profile(['sales.read']);
    expect(p.initials, 'ИП');
    expect(p.salesOrganization, isTrue);
    expect(profile([]).salesOrganization, isFalse);
  });
}
