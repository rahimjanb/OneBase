import 'package:go_router/go_router.dart';

import 'presentation/tasks_screens.dart';

/// Вкладка «Задачи»: список (`?status=`, `?view=` — как ссылки веба; `?department=` — задачи отдела), новая задача, карточка.
final tasksRoute = GoRoute(
  path: '/tasks',
  builder: (context, state) => TasksScreen(
    initialStatus: state.uri.queryParameters['status'],
    initialView: state.uri.queryParameters['view'],
    initialDepartment: state.uri.queryParameters['department'],
  ),
  routes: [
    GoRoute(path: 'new', builder: (context, state) => const NewTaskScreen()),
    GoRoute(path: ':id', builder: (context, state) => TaskScreen(id: state.pathParameters['id']!)),
  ],
);
