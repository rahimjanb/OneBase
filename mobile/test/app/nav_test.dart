import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/router.dart';
import 'package:onebase_mobile/app/shell/app_tab.dart';
import 'package:onebase_mobile/app/shell/fan_layout.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';

UserProfile profile(List<String> permissions, {bool hrAccess = false, List<String> departments = const []}) =>
    UserProfile(id: 'u', login: 'u', name: 'Тест', permissions: permissions, hrAccess: hrAccess, departments: departments);

void main() {
  group('нижняя панель и веер по правам — как MobileNav веба', () {
    test('директор: Главная, Консультант, Аналитика; Задачи и разделы — в веере', () {
      final nav = navFor(profile(['ai.agents.run', 'sales.read', 'finance.read', 'work.tasks', 'hr.read', 'production.read'],
          hrAccess: true, departments: ['sales', 'finance']));
      expect(nav.tabs, [AppTab.home, AppTab.ai, AppTab.analytics]);
      final fan = nav.fan.map((e) => e.location).toList();
      expect(fan.first, '/home/departments');
      expect(fan, containsAll(['/tasks', '/home/finance', '/home/hr', '/home/production', '/home/sales', '/home/insights']));
      expect(fan, isNot(contains('/home/notifications')));
    });

    test('сотрудник только с задачами: Главная и Задачи вкладками, консультанта нет', () {
      final nav = navFor(profile(['work.tasks'], departments: ['production']));
      expect(nav.tabs, [AppTab.home, AppTab.tasks]);
      expect(nav.fan.map((e) => e.location), isNot(contains('/tasks')));
      expect(nav.canOpenTab(AppTab.ai), isFalse);
      expect(nav.canOpenModule(AppModule.finance), isFalse);
      expect(nav.canOpenModule(AppModule.departments), isTrue); // свой отдел
    });

    test('HR без права hr.read — по признаку hrAccess (руководитель с подразделениями Verifix)', () {
      final nav = navFor(profile(['work.tasks'], hrAccess: true));
      expect(nav.canOpenModule(AppModule.hr), isTrue);
    });
  });

  group('редирект по входу и правам', () {
    final signedIn = AuthSignedIn(profile(['work.tasks']));
    final nav = navFor(signedIn.profile);
    test('без входа — на вход; с входа — на главную', () {
      expect(authRedirect(const AuthSignedOut(), '/home', nav), '/login');
      expect(authRedirect(signedIn, '/login', nav), '/home');
      expect(authRedirect(const AuthChecking(), '/home', nav), '/splash');
    });
    test('в раздел без права — на главную; разрешённое — как есть', () {
      expect(authRedirect(signedIn, '/home/finance', nav), '/home');
      expect(authRedirect(signedIn, '/ai', nav), '/home');
      expect(authRedirect(signedIn, '/analytics/sales', nav), '/home');
      expect(authRedirect(signedIn, '/tasks/123', nav), isNull);
      expect(authRedirect(signedIn, '/home/notifications', nav), isNull);
      expect(authRedirect(signedIn, '/profile', nav), isNull);
    });
  });

  group('раскладка веера — как fan.ts', () {
    test('кнопки и подписи не пересекаются и не выходят за экран', () {
      const labels = ['Отделы', 'Задачи', 'Клиенты', 'Финансы', 'Производство', 'Склад', 'Заявки', 'HR', 'AI-отчёт'];
      const width = 390.0;
      final l = fanLayout(labels, width, width / 2, 790, 22);
      expect(l.slots, hasLength(labels.length));
      for (var i = 0; i < l.slots.length; i++) {
        final s = l.slots[i];
        final box = fanLabelBox(labels[i]);
        expect(width / 2 + s.x - box.width / 2, greaterThanOrEqualTo(8));
        expect(width / 2 + s.x + box.width / 2, lessThanOrEqualTo(width - 8));
        expect(s.y + l.size / 2 + 6 + box.height, lessThanOrEqualTo(22 - 4));
        for (var j = i + 1; j < l.slots.length; j++) {
          final o = l.slots[j];
          final dx = (s.x - o.x).abs();
          final dy = (s.y - o.y).abs();
          expect(dx >= l.size || dy >= l.size, isTrue, reason: 'кнопки $i и $j пересекаются');
        }
      }
      // Слева направо, середина — выше краёв.
      expect(l.slots.first.x, lessThan(0));
      expect(l.slots.last.x, greaterThan(0));
      expect(l.slots[4].y, lessThan(l.slots.first.y));
    });

    test('одна кнопка — сверху; узкий экран — кнопки меньше', () {
      expect(fanLayout(['Задачи'], 390, 195, 790, 22).slots.single.x.abs(), lessThan(1e-9));
      expect(fanLayout(['А', 'Б', 'В'], 340, 170, 700, 22).size, 56);
      expect(fanLayout(const [], 390, 195, 790, 22).slots, isEmpty);
    });
  });
}
