import 'package:go_router/go_router.dart';

import 'presentation/analytics_screen.dart';

/// Вкладка «Аналитика».
final analyticsRoute = GoRoute(path: '/analytics', builder: (context, state) => const AnalyticsScreen());
