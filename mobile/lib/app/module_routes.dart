import 'package:go_router/go_router.dart';

import '../features/departments/presentation/departments_screen.dart';
import '../features/finance/presentation/finance_screens.dart';
import '../features/hr/presentation/hr_screens.dart';
import '../features/insights/presentation/insights_screen.dart';
import '../features/notifications/presentation/notifications_screen.dart';
import '../features/production/presentation/production_screens.dart';
import '../features/sales/presentation/clients_screens.dart';
import '../features/search/presentation/search_screen.dart';
import '../features/warehouse/presentation/warehouse_screens.dart';
import 'shell/app_tab.dart';

/// Разделы из веера и с главной — внутри ветки «Главная» (`/home/<раздел>`): нижняя панель остаётся, «назад» ведёт на главную.
final List<RouteBase> moduleRoutes = [
  GoRoute(path: 'search', builder: (context, state) => const SearchScreen()),
  // Настройки приложения — в «Профиле» (тема, язык, уведомления, Telegram, безопасность).
  GoRoute(path: AppModule.settings.segment, redirect: (context, state) => '/profile'),
  GoRoute(
    path: AppModule.sales.segment,
    builder: (context, state) => const ClientsScreen(),
    routes: [GoRoute(path: 'points/:id', builder: (context, state) => ClientScreen(id: int.parse(state.pathParameters['id']!)))],
  ),
  GoRoute(
    path: AppModule.finance.segment,
    builder: (context, state) => const FinanceScreen(),
    routes: [
      GoRoute(path: 'accounts', builder: (context, state) => const AccountsScreen()),
      GoRoute(
        path: 'entries',
        builder: (context, state) => EntriesScreen(
          account: state.uri.queryParameters['account'],
          counterparty: state.uri.queryParameters['counterparty'],
          registrar: state.uri.queryParameters['registrar'],
        ),
      ),
      GoRoute(path: 'documents/:id', builder: (context, state) => DocumentScreen(id: state.pathParameters['id']!)),
    ],
  ),
  GoRoute(
    path: AppModule.production.segment,
    builder: (context, state) => const ProductionScreen(),
    routes: [GoRoute(path: 'orders/:id', builder: (context, state) => OrderScreen(id: state.pathParameters['id']!))],
  ),
  GoRoute(
    path: AppModule.warehouse.segment,
    builder: (context, state) => const WarehouseScreen(),
    routes: [GoRoute(path: 'items/:id', builder: (context, state) => ItemScreen(id: state.pathParameters['id']!))],
  ),
  GoRoute(
    path: AppModule.requests.segment,
    builder: (context, state) => RequestsScreen(initialScope: switch (state.uri.queryParameters['tab']) {
      'department' => 'department',
      'approvals' || 'all' => 'all',
      _ => 'my',
    }),
  ),
  GoRoute(
    path: AppModule.hr.segment,
    builder: (context, state) => HrScreen(employees: state.uri.queryParameters['tab'] == 'employees'),
    routes: [GoRoute(path: 'employees/:id', builder: (context, state) => EmployeeScreen(id: state.pathParameters['id']!))],
  ),
  GoRoute(path: AppModule.notifications.segment, builder: (context, state) => const NotificationsScreen()),
  GoRoute(path: AppModule.departments.segment, builder: (context, state) => DepartmentsScreen(month: state.uri.queryParameters['month'])),
  GoRoute(path: AppModule.insights.segment, builder: (context, state) => const InsightsScreen()),
];
