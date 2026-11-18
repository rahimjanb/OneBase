import 'package:flutter/material.dart';

import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/coming_soon.dart';

/// Задачи — этап 10.
class TasksScreen extends StatelessWidget {
  const TasksScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      ComingSoonScreen(title: context.l10n.navTasks, stage: 10, icon: Icons.task_alt_outlined);
}
