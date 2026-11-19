import 'package:go_router/go_router.dart';

import '../shared/extensions/context_ext.dart';
import '../shared/widgets/coming_soon.dart';
import 'shell/app_tab.dart';

/// Разделы из веера и с главной — внутри ветки «Главная» (`/home/<раздел>`): нижняя панель остаётся, «назад» ведёт на главную.
final List<RouteBase> moduleRoutes = [
  GoRoute(path: 'search', builder: (context, state) => ComingSoonScreen(title: context.l10n.searchTitle, stage: 4)),
  for (final m in AppModule.values)
    GoRoute(path: m.segment, builder: (context, state) => ComingSoonScreen(title: m.label(context.l10n), stage: 5)),
];
