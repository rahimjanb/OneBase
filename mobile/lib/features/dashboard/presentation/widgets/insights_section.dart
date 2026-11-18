import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../../../shared/widgets/kpi_tile.dart';
import '../../../../shared/widgets/states.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';

/// «Фокус дня»: находки AI-консультанта (проблемы, критичные, возможности) — сервер находит их сам по расписанию, телефон модель
/// не вызывает. Сверху — счётчики, ниже — три самые важные, все с рекомендациями — в детализации.
class InsightsSection extends ConsumerWidget {
  const InsightsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(alertsBlock);
    final l10n = context.l10n;
    return BlockSection<AiAlerts>(
      title: l10n.aiTitle,
      state: state,
      skeletonHeight: 200,
      moreLabel: l10n.actionAll,
      onMore: () => _showAlerts(context, state.data!),
      onRetry: () => ref.read(alertsBlock.notifier).refresh(),
      builder: (context, a) => _Body(a: a, now: ref.watch(clockProvider)()),
    );
  }
}

/// Сначала критичные, затем «внимание», затем возможности; внутри — свежие выше.
List<AiAlert> sortAlerts(List<AiAlert> alerts) {
  int rank(String s) => switch (s) {
        'Critical' => 0,
        'Warning' => 1,
        _ => 2,
      };
  return [...alerts]..sort((a, b) {
      final r = rank(a.severity).compareTo(rank(b.severity));
      return r != 0 ? r : (b.detectedAt ?? '').compareTo(a.detectedAt ?? '');
    });
}

({IconData icon, Color color, Color soft, String label}) severityStyle(BuildContext context, String severity) {
  final c = context.ob;
  final l10n = context.l10n;
  return switch (severity) {
    'Critical' => (icon: Icons.error_rounded, color: c.bad, soft: c.badSoft, label: l10n.severityCritical),
    'Warning' => (icon: Icons.warning_amber_rounded, color: c.warn, soft: c.warnSoft, label: l10n.severityWarning),
    _ => (icon: Icons.lightbulb_outline_rounded, color: c.ok, soft: c.okSoft, label: l10n.severityOpportunity),
  };
}

class _Body extends StatelessWidget {
  const _Body({required this.a, required this.now});

  final AiAlerts a;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final checked = DateTime.tryParse(a.lastRun?.at ?? '');
    if (a.alerts.isEmpty) {
      return EmptyView(
        compact: true,
        icon: Icons.check_circle_outline_rounded,
        title: l10n.aiNone,
        message: checked == null ? null : l10n.aiChecked(context.fmt.ago(checked, now)),
      );
    }
    final top = sortAlerts(a.alerts).take(3).toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Wrap(
          spacing: ObSpace.sm,
          runSpacing: ObSpace.sm,
          children: [
            if (a.summary.critical > 0)
              StatusChip(label: '${l10n.aiCritical} ${a.summary.critical}', color: c.bad, background: c.badSoft, icon: Icons.error_rounded),
            if (a.summary.problems > 0)
              StatusChip(
                label: '${l10n.aiProblems} ${a.summary.problems}',
                color: c.warn,
                background: c.warnSoft,
                icon: Icons.warning_amber_rounded,
              ),
            if (a.summary.opportunities > 0)
              StatusChip(
                label: '${l10n.aiOpportunities} ${a.summary.opportunities}',
                color: c.ok,
                background: c.okSoft,
                icon: Icons.lightbulb_outline_rounded,
              ),
          ],
        ),
        for (final alert in top) _AlertRow(alert: alert),
        if (checked != null) ...[
          const SizedBox(height: ObSpace.md),
          Text(l10n.aiChecked(context.fmt.ago(checked, now)), style: context.text.bodySmall),
        ],
      ],
    );
  }
}

class _AlertRow extends StatelessWidget {
  const _AlertRow({required this.alert});

  final AiAlert alert;

  @override
  Widget build(BuildContext context) {
    final s = severityStyle(context, alert.severity);
    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.md),
      child: MergeSemantics(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(label: s.label, child: Icon(s.icon, size: 20, color: s.color)),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(alert.title, style: context.text.titleSmall, maxLines: 2, overflow: TextOverflow.ellipsis),
                  if (alert.message != null)
                    Text(alert.message!, style: context.text.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

void _showAlerts(BuildContext context, AiAlerts a) {
  final l10n = context.l10n;
  showDetailSheet(
    context,
    title: l10n.aiAll,
    subtitle: '${l10n.aiCritical}: ${a.summary.critical} · ${l10n.aiProblems}: ${a.summary.problems} · '
        '${l10n.aiOpportunities}: ${a.summary.opportunities}',
    children: (context) => [
      for (final alert in sortAlerts(a.alerts))
        Padding(
          padding: const EdgeInsets.only(bottom: ObSpace.md),
          child: _AlertCard(alert: alert),
        ),
    ],
  );
}

class _AlertCard extends StatelessWidget {
  const _AlertCard({required this.alert});

  final AiAlert alert;

  @override
  Widget build(BuildContext context) {
    final s = severityStyle(context, alert.severity);
    final c = context.ob;
    return Container(
      padding: const EdgeInsets.all(ObSpace.lg),
      decoration: BoxDecoration(
        color: c.surface,
        borderRadius: BorderRadius.circular(ObRadius.md),
        border: Border.all(color: c.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          StatusChip(label: s.label, color: s.color, background: s.soft, icon: s.icon),
          const SizedBox(height: ObSpace.sm),
          Text(alert.title, style: context.text.titleMedium),
          if (alert.message != null) ...[
            const SizedBox(height: 4),
            Text(alert.message!, style: context.text.bodyMedium),
          ],
          if (alert.recommendation != null) ...[
            const SizedBox(height: ObSpace.md),
            Text(context.l10n.aiWhatToDo, style: context.text.labelMedium),
            const SizedBox(height: 2),
            Text(alert.recommendation!, style: context.text.bodyMedium!.copyWith(color: c.ink)),
          ],
        ],
      ),
    );
  }
}
