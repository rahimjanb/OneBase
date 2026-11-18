import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../../../shared/widgets/states.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';

/// Последние события ленты OneBase (задачи, HR, AI, система): пять свежих, остальные — в детализации.
class EventsSection extends ConsumerWidget {
  const EventsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(eventsBlock);
    final l10n = context.l10n;
    final now = ref.watch(clockProvider)();
    return BlockSection<NotificationPage>(
      title: l10n.eventsTitle,
      state: state,
      skeletonHeight: 220,
      moreLabel: l10n.actionAll,
      onMore: (state.data?.items.length ?? 0) > 5 ? () => _showEvents(context, state.data!, now) : null,
      onRetry: () => ref.read(eventsBlock.notifier).refresh(),
      builder: (context, page) => page.items.isEmpty
          ? EmptyView(compact: true, icon: Icons.notifications_none_rounded, title: l10n.eventsEmpty)
          : Column(
              children: [
                for (var i = 0; i < page.items.length && i < 5; i++) ...[
                  if (i > 0) Divider(color: context.ob.line, height: 20),
                  EventRow(item: page.items[i], now: now),
                ],
              ],
            ),
    );
  }
}

/// Событие: иконка раздела, заголовок (непрочитанное — жирнее и с точкой), текст и время.
class EventRow extends StatelessWidget {
  const EventRow({super.key, required this.item, required this.now});

  final NotificationItem item;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final critical = item.severity == 'critical';
    final icon = switch (item.category) {
      'tasks' => Icons.task_alt_rounded,
      'hr' => Icons.badge_outlined,
      'ai' => Icons.auto_awesome_outlined,
      _ => Icons.info_outline_rounded,
    };
    final at = DateTime.tryParse(item.createdAt);
    return MergeSemantics(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(color: critical ? c.badSoft : c.muted, borderRadius: BorderRadius.circular(10)),
            child: Icon(icon, size: 18, color: critical ? c.bad : c.ink2),
          ),
          const SizedBox(width: ObSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        item.title,
                        style: context.text.titleSmall!.copyWith(fontWeight: item.read ? FontWeight.w500 : FontWeight.w600),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (!item.read)
                      Container(
                        width: 8,
                        height: 8,
                        margin: const EdgeInsets.only(left: 6),
                        decoration: BoxDecoration(color: c.accent, shape: BoxShape.circle),
                      ),
                  ],
                ),
                if (item.body != null && item.body!.isNotEmpty)
                  Text(item.body!, style: context.text.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
                if (at != null) ...[
                  const SizedBox(height: 2),
                  Text(context.fmt.ago(at, now), style: context.text.labelSmall),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

void _showEvents(BuildContext context, NotificationPage page, DateTime now) {
  final l10n = context.l10n;
  showDetailSheet(
    context,
    title: l10n.eventsTitle,
    subtitle: page.unread > 0 ? l10n.eventsUnread(page.unread) : null,
    children: (context) => [
      for (var i = 0; i < page.items.length; i++) ...[
        if (i > 0) Divider(color: context.ob.line, height: 24),
        EventRow(item: page.items[i], now: now),
      ],
    ],
  );
}
