import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/theme.dart';
import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';

/// Подразделения: явка сегодня по Verifix (на месте, опоздали, нет на месте, отпуск; сравнение с прошлым рабочим днём — с сервера)
/// и крупнейшие подразделения. Все — в детализации.
class DepartmentsSection extends ConsumerWidget {
  const DepartmentsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(hrBlock);
    final l10n = context.l10n;
    return BlockSection<HrOverview>(
      title: l10n.deptTitle,
      state: state,
      skeletonHeight: 260,
      moreLabel: l10n.actionAll,
      onMore: () => _showDivisions(context, state.data!),
      onRetry: () => ref.read(hrBlock.notifier).refresh(),
      builder: (context, h) => _Body(h: h, now: ref.watch(clockProvider)()),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.h, required this.now});

  final HrOverview h;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final share = h.presentShare;
    final top = [...h.divisions.where((d) => d.scheduled > 0)]..sort((a, b) => b.scheduled.compareTo(a.scheduled));
    final synced = DateTime.tryParse(h.sync?.lastSuccessAt ?? '');

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(l10n.hrAttendance, style: context.text.labelMedium),
                  const SizedBox(height: 4),
                  Text(
                    share == null ? '—' : fmt.percent(share),
                    style: context.text.headlineMedium!.copyWith(fontFeatures: tabularFigures),
                  ),
                ],
              ),
            ),
            Flexible(
              child: Text(
                h.scheduled == 0 ? l10n.hrNoSchedule : l10n.hrPresentOf(fmt.count(h.present), fmt.count(h.scheduled)),
                style: context.text.bodySmall,
                textAlign: TextAlign.end,
              ),
            ),
          ],
        ),
        const SizedBox(height: ObSpace.sm),
        _Bar(share: share ?? 0, color: c.series1),
        const SizedBox(height: ObSpace.md),
        Row(
          children: [
            Expanded(child: _Count(icon: Icons.schedule_rounded, color: c.warn, label: l10n.hrLate, value: h.late)),
            Expanded(child: _Count(icon: Icons.person_off_outlined, color: c.bad, label: l10n.hrAbsent, value: h.absent)),
            Expanded(child: _Count(icon: Icons.beach_access_outlined, color: c.ink2, label: l10n.hrOnLeave, value: h.onLeave)),
          ],
        ),
        if (h.compare?.presentPct != null) ...[
          const SizedBox(height: ObSpace.sm),
          Text(l10n.hrCompare(fmt.percent(h.compare!.presentPct! / 100)), style: context.text.bodySmall),
        ],
        if (top.isNotEmpty) ...[
          const SizedBox(height: ObSpace.md),
          Divider(color: c.line, height: 1),
          for (final d in top.take(4)) _DivisionRow(d: d),
        ],
        if (synced != null) ...[
          const SizedBox(height: ObSpace.sm),
          Text(l10n.hrSynced(fmt.ago(synced, now)), style: context.text.bodySmall),
        ],
      ],
    );
  }
}

class _Count extends StatelessWidget {
  const _Count({required this.icon, required this.color, required this.label, required this.value});

  final IconData icon;
  final Color color;
  final String label;
  final int value;

  @override
  Widget build(BuildContext context) => MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 16, color: color),
                const SizedBox(width: 4),
                Text(context.fmt.count(value), style: context.text.titleMedium!.copyWith(fontFeatures: tabularFigures)),
              ],
            ),
            Text(label, style: context.text.bodySmall, maxLines: 1, overflow: TextOverflow.ellipsis),
          ],
        ),
      );
}

/// Полоса доли: светлая дорожка, заливка цветом ряда, скругление.
class _Bar extends StatelessWidget {
  const _Bar({required this.share, required this.color, this.height = 8});

  final double share;
  final Color color;
  final double height;

  @override
  Widget build(BuildContext context) => ClipRRect(
        borderRadius: BorderRadius.circular(height / 2),
        child: LinearProgressIndicator(
          value: share.clamp(0, 1),
          minHeight: height,
          color: color,
          backgroundColor: context.ob.muted,
        ),
      );
}

class _DivisionRow extends StatelessWidget {
  const _DivisionRow({required this.d});

  final HrDivision d;

  @override
  Widget build(BuildContext context) {
    final share = d.scheduled > 0 ? d.present / d.scheduled : 0.0;
    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.md),
      child: MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(child: Text(d.name, style: context.text.bodyMedium!.copyWith(color: context.ob.ink), maxLines: 1, overflow: TextOverflow.ellipsis)),
                Text(
                  '${context.fmt.count(d.present)} / ${context.fmt.count(d.scheduled)}',
                  style: context.text.labelMedium!.copyWith(fontFeatures: tabularFigures),
                ),
              ],
            ),
            const SizedBox(height: 6),
            _Bar(share: share, color: context.ob.series1, height: 5),
          ],
        ),
      ),
    );
  }
}

void _showDivisions(BuildContext context, HrOverview h) {
  final l10n = context.l10n;
  final fmt = context.fmt;
  final all = [...h.divisions]..sort((a, b) => b.scheduled.compareTo(a.scheduled));
  final date = DateTime.tryParse(h.date);
  showDetailSheet(
    context,
    title: l10n.hrDivisionsAll,
    subtitle: date == null ? null : '${l10n.hrAttendance} · ${fmt.dayMonth(date)}',
    children: (context) => [
      DetailGroup(
        children: [
          for (final d in all)
            DetailRow(
              label: d.name,
              value: '${fmt.count(d.present)} / ${fmt.count(d.scheduled)}',
              hint: [
                if (d.late > 0) '${l10n.hrLate}: ${fmt.count(d.late)}',
                if (d.absent > 0) '${l10n.hrAbsent}: ${fmt.count(d.absent)}',
              ].join(' · ').ifEmpty(null),
            ),
        ],
      ),
    ],
  );
}

extension on String {
  String? ifEmpty(String? other) => isEmpty ? other : this;
}
