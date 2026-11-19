import 'package:go_router/go_router.dart';

import 'presentation/tasks_screen.dart';

/// Вкладка «Задачи».
final tasksRoute = GoRoute(path: '/tasks', builder: (context, state) => const TasksScreen());
