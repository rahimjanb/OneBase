import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../data/dashboard_models.dart';
import '../../domain/dashboard_period.dart';
import '../dashboard_providers.dart';

/// Фильтры Dashboard в одну строку над блоками: период и (если у компании больше одной организации в 1С) организация.
class DashboardFilterBar extends ConsumerWidget {
  const DashboardFilterBar({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final filter = ref.watch(dashboardFilterProvider);
    final access = ref.watch(dashboardAccessProvider);
    final orgs = access.finance ? (ref.watch(financeOrganizationsBlock).data ?? const <FinanceOrganization>[]) : const <FinanceOrganization>[];
    final l10n = context.l10n;
    final period = filter.period;
    final periodLabel = period.isLatest ? l10n.periodLatest : context.fmt.monthYear(period.year!, period.month!);
    final org = orgs.where((o) => o.id == filter.organizationId).firstOrNull;

    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.sm),
      child: Wrap(
        spacing: ObSpace.sm,
        runSpacing: ObSpace.sm,
        children: [
          _Pill(
            icon: Icons.calendar_month_outlined,
            label: periodLabel,
            semantics: '${l10n.periodTitle}: $periodLabel',
            onTap: () => _pickPeriod(context, ref),
          ),
          if (orgs.length > 1)
            _Pill(
              icon: Icons.apartment_outlined,
              label: org?.name ?? l10n.organizationAll,
              semantics: '${l10n.organizationTitle}: ${org?.name ?? l10n.organizationAll}',
              onTap: () => _pickOrganization(context, ref, orgs),
            ),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.icon, required this.label, required this.semantics, required this.onTap});

  final IconData icon;
  final String label;
  final String semantics;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Semantics(
      button: true,
      label: semantics,
      child: ExcludeSemantics(
        child: Material(
          color: c.surface,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md), side: BorderSide(color: c.line)),
          child: InkWell(
            onTap: onTap,
            borderRadius: BorderRadius.circular(ObRadius.md),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(icon, size: 17, color: c.accentStrong),
                  const SizedBox(width: 6),
                  Text(label, style: context.text.labelLarge),
                  const SizedBox(width: 2),
                  Icon(Icons.expand_more_rounded, size: 18, color: c.ink3),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

Future<void> _pickPeriod(BuildContext context, WidgetRef ref) => showModalBottomSheet<void>(
      context: context,
      useRootNavigator: true,
      useSafeArea: true,
      isScrollControlled: true,
      builder: (context) => Consumer(
        builder: (context, ref, _) {
          final l10n = context.l10n;
          final access = ref.watch(dashboardAccessProvider);
          final current = ref.watch(dashboardFilterProvider).period;
          final months = access.sales
              ? (ref.watch(salesMonthsBlock).data?.map((m) => DashboardPeriod(m.year, m.month)).toList() ??
                  recentMonths(ref.read(clockProvider)()))
              : recentMonths(ref.read(clockProvider)());
          void pick(DashboardPeriod p) {
            ref.read(dashboardFilterProvider.notifier).setPeriod(p);
            Navigator.of(context).pop();
          }

          return ConstrainedBox(
            constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.7),
            child: ListView(
              shrinkWrap: true,
              padding: const EdgeInsets.only(bottom: ObSpace.xl),
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, ObSpace.sm),
                  child: Text(l10n.periodTitle, style: context.text.headlineSmall),
                ),
                _Option(
                  title: l10n.periodLatest,
                  subtitle: l10n.periodLatestHint,
                  selected: current.isLatest,
                  onTap: () => pick(const DashboardPeriod.latest()),
                ),
                for (final p in months)
                  _Option(
                    title: context.fmt.monthYear(p.year!, p.month!),
                    selected: p == current,
                    onTap: () => pick(p),
                  ),
              ],
            ),
          );
        },
      ),
    );

Future<void> _pickOrganization(BuildContext context, WidgetRef ref, List<FinanceOrganization> orgs) =>
    showModalBottomSheet<void>(
      context: context,
      useRootNavigator: true,
      useSafeArea: true,
      builder: (context) {
        final l10n = context.l10n;
        final current = ref.read(dashboardFilterProvider).organizationId;
        void pick(String? id) {
          ref.read(dashboardFilterProvider.notifier).setOrganization(id);
          Navigator.of(context).pop();
        }

        return ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.only(bottom: ObSpace.xl),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, ObSpace.sm),
              child: Text(l10n.organizationTitle, style: context.text.headlineSmall),
            ),
            _Option(title: l10n.organizationAll, selected: current == null, onTap: () => pick(null)),
            for (final o in orgs) _Option(title: o.name, selected: o.id == current, onTap: () => pick(o.id)),
          ],
        );
      },
    );

class _Option extends StatelessWidget {
  const _Option({required this.title, this.subtitle, required this.selected, required this.onTap});

  final String title;
  final String? subtitle;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => ListTile(
        title: Text(title),
        subtitle: subtitle == null ? null : Text(subtitle!),
        selected: selected,
        trailing: selected ? Icon(Icons.check_rounded, color: context.ob.accentStrong) : null,
        onTap: onTap,
      );
}
