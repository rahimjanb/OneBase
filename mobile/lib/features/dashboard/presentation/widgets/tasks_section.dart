import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../app/shell/app_tab.dart';
import '../../../../app/theme/theme.dart';
import '../../../../app/theme/tokens.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';

/// Активные задачи: счётчики (открыто, в работе, на проверке, просрочено), мои ближайшие и отделы с просрочкой. «Открыть» — раздел
/// «Задачи».
class TasksSection extends ConsumerWidget {
  const TasksSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(workBlock);
    final l10n = context.l10n;
    return BlockSection<WorkOverview>(
      title: l10n.navTasks,
      state: state,
      skeletonHeight: 180,
      moreLabel: l10n.actionOpen,
      onMore: () => context.go(AppTab.tasks.path),
      onRetry: () => ref.read(workBlock.notifier).refresh(),
      builder: (context, w) => _Body(w: w),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.w});

  final WorkOverview w;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final departments = [...w.departments.where((d) => d.overdue > 0 || d.open > 0)]
      ..sort((a, b) => b.overdue != a.overdue ? b.overdue.compareTo(a.overdue) : b.open.compareTo(a.open));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: _Stat(value: w.open, label: l10n.tasksOpen)),
            Expanded(child: _Stat(value: w.inProgress, label: l10n.tasksInProgress)),
            Expanded(child: _Stat(value: w.review, label: l10n.tasksReview)),
            Expanded(
              child: _Stat(
                value: w.overdue,
                label: l10n.tasksOverdue,
                color: w.overdue > 0 ? c.bad : null,
                icon: w.overdue > 0 ? Icons.error_outline_rounded : null,
              ),
            ),
          ],
        ),
        const SizedBox(height: ObSpace.md),
        Divider(color: c.line, height: 1),
        const SizedBox(height: ObSpace.md),
        Text(l10n.tasksMine, style: context.text.titleSmall),
        if (w.myUpcoming.isEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Text(l10n.tasksNoUpcoming, style: context.text.bodyMedium),
          )
        else
          for (final t in w.myUpcoming.take(3)) _TaskRow(t: t),
        if (departments.isNotEmpty) ...[
          const SizedBox(height: ObSpace.md),
          Text(l10n.tasksDepartments, style: context.text.titleSmall),
          for (final d in departments.take(3))
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Row(
                children: [
                  Expanded(child: Text(d.name, style: context.text.bodyMedium!.copyWith(color: c.ink), maxLines: 1, overflow: TextOverflow.ellipsis)),
                  const SizedBox(width: ObSpace.sm),
                  Flexible(
                    child: Text(
                      l10n.tasksDeptLine(d.open, d.overdue),
                      style: context.text.bodySmall!.copyWith(color: d.overdue > 0 ? c.bad : null),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      textAlign: TextAlign.end,
                    ),
                  ),
                ],
              ),
            ),
        ],
      ],
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label, this.color, this.icon});

  final int value;
  final String label;
  final Color? color;
  final IconData? icon;

  @override
  Widget build(BuildContext context) => MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[Icon(icon, size: 16, color: color), const SizedBox(width: 3)],
                Text(
                  context.fmt.count(value),
                  style: context.text.headlineSmall!.copyWith(fontFeatures: tabularFigures, color: color),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.only(right: 6),
              child: Text(label, style: context.text.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
            ),
          ],
        ),
      );
}

class _TaskRow extends StatelessWidget {
  const _TaskRow({required this.t});

  final WorkTaskBrief t;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final due = t.dueDate == null ? null : DateTime.tryParse(t.dueDate!);
    final priority = switch (t.priority) {
      'Urgent' => c.bad,
      'High' => c.warn,
      _ => c.lineStrong,
    };
    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.sm),
      child: Row(
        children: [
          Container(width: 4, height: 28, decoration: BoxDecoration(color: priority, borderRadius: BorderRadius.circular(2))),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(t.title, style: context.text.bodyMedium!.copyWith(color: c.ink), maxLines: 1, overflow: TextOverflow.ellipsis),
                if (t.departmentName != null) Text(t.departmentName!, style: context.text.bodySmall, maxLines: 1),
              ],
            ),
          ),
          if (due != null)
            Flexible(
              child: Text(
                context.l10n.tasksDue(context.fmt.dayMonth(due)),
                style: context.text.labelMedium!.copyWith(color: t.overdue ? c.bad : null),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
        ],
      ),
    );
  }
}
