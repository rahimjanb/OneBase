import 'package:go_router/go_router.dart';

import 'presentation/analytics_screen.dart';
import 'presentation/sales_screens.dart';

/// Вкладка «Аналитика»: хаб и продажи вглубь — республика / направление / «Мои регионы» → регион → ТП. Точка — в разделе «Клиенты».
final analyticsRoute = GoRoute(
  path: '/analytics',
  builder: (context, state) => const AnalyticsScreen(),
  routes: [
    GoRoute(
      path: 'sales',
      builder: (context, state) => const GroupScreen(path: 'republic'),
      routes: [
        GoRoute(path: 'my', builder: (context, state) => const GroupScreen(path: 'my')),
        GoRoute(path: 'directions/:id', builder: (context, state) => GroupScreen(path: 'directions/${state.pathParameters['id']}')),
        GoRoute(path: 'regions/:id', builder: (context, state) => RegionScreen(id: state.pathParameters['id']!)),
        GoRoute(path: 'agents', builder: (context, state) => const AgentsScreen()),
        GoRoute(path: 'agents/:id', builder: (context, state) => AgentScreen(id: state.pathParameters['id']!)),
      ],
    ),
  ],
);
