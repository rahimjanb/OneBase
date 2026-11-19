import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/cache/paged.dart';
import '../../../core/core_providers.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/navigation/web_links.dart';
import '../../../core/synchronization/outbox.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/states.dart';
import '../../dashboard/domain/home_view.dart';
import '../data/notification_models.dart';

// ───────────── Счётчик колокольчика ─────────────

/// Непрочитанные: при открытии, по возвращении приложения и раз в минуту, пока кто-то смотрит (без частого опроса — realtime в
/// OneBase нет). Push FCM — после решения владельца (проект Firebase + расширение P1 на сервере).
class UnreadController extends Notifier<NotifCounts?> {
  Timer? _timer;

  @override
  NotifCounts? build() {
    if (ref.watch(currentProfileProvider) == null) return null;
    _timer = Timer.periodic(const Duration(seconds: 60), (_) => refresh());
    ref.onDispose(() => _timer?.cancel());
    Future.microtask(refresh);
    return null;
  }

  Future<void> refresh() async {
    try {
      final counts = await ref.read(apiClientProvider).get('/api/notifications/counts', (j) => NotifCounts.fromJson(j! as Map<String, Object?>));
      if (ref.mounted) state = counts;
    } on Object {
      // Нет сети — значок остаётся прежним.
    }
  }

  /// Отметка на экране сразу (оптимистично) — сервер подтвердит при следующем обновлении.
  void adjust(int delta) {
    final s = state;
    if (s == null) return;
    state = s.copyWith(unread: (s.unread + delta).clamp(0, 1 << 30));
  }
}

final unreadProvider = NotifierProvider.autoDispose<UnreadController, NotifCounts?>(UnreadController.new);

// ───────────── Лента ─────────────

class NotificationsController extends PagedController<Notif> {
  NotificationsController(this.filter);

  final String filter;

  @override
  Future<PageChunk<Notif>> fetchPage(int page, String? cursor, CancelToken cancel) => ref.read(apiClientProvider).get(
        '/api/notifications',
        (j) {
          final m = j! as Map<String, Object?>;
          final next = m['nextCursor'] as String?;
          return PageChunk([for (final e in m['items']! as List<Object?>) Notif.fromJson(e! as Map<String, Object?>)], hasMore: next != null, cursor: next);
        },
        query: {'filter': filter, 'cursor': cursor, 'take': 30},
        cancel: cancel,
      );

  /// Прочитано / не прочитано: сразу на экране, запрос — через очередь (идемпотентно; без сети уйдёт позже).
  Future<SendResult> setRead(Notif n, bool read) async {
    replaceWhere((x) => x.id == n.id, (x) => x.copyWith(read: read));
    ref.read(unreadProvider.notifier).adjust(read ? -1 : 1);
    final result = await ref.read(outboxProvider).send(
          kind: 'notification.read',
          entity: 'notification:${n.id}',
          method: 'POST',
          path: '/api/notifications/${n.id}/${read ? 'read' : 'unread'}',
        );
    if (result == SendResult.rejected && ref.mounted) {
      replaceWhere((x) => x.id == n.id, (x) => x.copyWith(read: !read));
      ref.read(unreadProvider.notifier).adjust(read ? 1 : -1);
    }
    return result;
  }

  Future<SendResult> readAll() async {
    replaceWhere((_) => true, (x) => x.copyWith(read: true));
    final result = await ref.read(outboxProvider).send(kind: 'notification.read-all', entity: 'notifications:all', method: 'POST', path: '/api/notifications/read-all');
    await ref.read(unreadProvider.notifier).refresh();
    return result;
  }
}

final notificationsProvider = NotifierProvider.autoDispose.family<NotificationsController, PagedState<Notif>, String>(NotificationsController.new);

IconData notifIcon(String type) => switch (type) {
      'task_assigned' => LucideIcons.squareCheckBig,
      'task_due' => LucideIcons.clock,
      'task_overdue' => LucideIcons.alarmClock,
      'task_done' => LucideIcons.circleCheckBig,
      'task_comment' => LucideIcons.messageSquareText,
      'mention' => LucideIcons.atSign,
      'calendar' => LucideIcons.calendarDays,
      'procurement' => LucideIcons.clipboardPen,
      'hr_event' => LucideIcons.users,
      'ai_insight' => LucideIcons.sparkles,
      'ai_budget' => LucideIcons.gauge,
      'sales_alert' => LucideIcons.trendingDown,
      _ => LucideIcons.refreshCw,
    };

Tone notifTone(Notif n) => switch (n.severity) {
      'critical' => Tone.bad,
      'warning' => Tone.warn,
      _ => n.category == 'ai' ? Tone.violet : (n.category == 'hr' ? Tone.ok : Tone.accent),
    };

/// Группа по дню (по времени Ташкента): сегодня, вчера, ранее.
int dayGroup(String createdAt, DateTime now) {
  final day = isoDate(tashkentNow(DateTime.parse(createdAt)));
  final today = isoDate(tashkentNow(now));
  if (day == today) return 0;
  if (day == addDays(today, -1)) return 1;
  return 2;
}

/// «Уведомления»: вкладки (все, непрочитанные, задачи, AI, HR — по правам), группы по дням, прочтение по нажатию и смахиванию,
/// «Прочитать все», «Продлить срок» у просроченной задачи; подгрузка курсором.
class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  String _filter = 'all';
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter < 500) ref.read(notificationsProvider(_filter).notifier).loadMore();
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _open(Notif n) async {
    if (!n.read) unawaited(ref.read(notificationsProvider(_filter).notifier).setRead(n, true));
    if (n.href != null) unawaited(context.push(appRouteForWeb(n.href)));
  }

  Future<void> _extend(Notif n) async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final t = tashkentNow(ref.read(clockProvider)());
    final picked = await showDatePicker(context: context, firstDate: DateTime(t.year, t.month, t.day), lastDate: DateTime(t.year + 1), initialDate: DateTime(t.year, t.month, t.day + 1));
    if (picked == null) return;
    try {
      await ref.read(apiClientProvider).post<void>('/api/notifications/${n.id}/action', body: {'action': 'extend_due', 'due': isoDate(DateTime.utc(picked.year, picked.month, picked.day))});
      messenger.showSnackBar(SnackBar(content: Text(l10n.ntExtended)));
      await ref.read(notificationsProvider(_filter).notifier).refresh();
    } on Object catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(AppFailure.from(e).describe(l10n))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final profile = ref.watch(currentProfileProvider);
    final counts = ref.watch(unreadProvider);
    final s = ref.watch(notificationsProvider(_filter));
    final now = ref.watch(clockProvider)();
    final tabs = <String, String>{
      'all': counts == null ? l10n.ntAll : '${l10n.ntAll} ${counts.all}',
      'unread': counts == null ? l10n.ntUnread : '${l10n.ntUnread} ${counts.unread}',
      if (profile?.can(Perm.workTasks) ?? false) 'tasks': l10n.ntTasks,
      if (profile?.can(Perm.agentsRun) ?? false) 'ai': l10n.ntAi,
      if (profile?.hrAccess ?? false) 'hr': l10n.ntHr,
    };
    final groups = [l10n.ntToday, l10n.ntYesterday, l10n.ntEarlier];
    final rows = <Widget>[];
    int? lastGroup;
    for (final n in s.items) {
      final g = dayGroup(n.createdAt, now);
      if (g != lastGroup) {
        rows.add(Padding(
          padding: EdgeInsets.only(top: rows.isEmpty ? 0 : 16, bottom: 8, left: 4),
          child: Text(groups[g], style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink3)),
        ));
        lastGroup = g;
      }
      rows.add(Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Dismissible(
          key: ValueKey(n.id),
          direction: DismissDirection.endToStart,
          confirmDismiss: (_) async {
            final r = await ref.read(notificationsProvider(_filter).notifier).setRead(n, !n.read);
            if (r == SendResult.queued && context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.ntQueued)));
            return false;
          },
          background: Container(
            alignment: Alignment.centerRight,
            padding: const EdgeInsets.only(right: 20),
            decoration: BoxDecoration(color: c.accentSoft, borderRadius: BorderRadius.circular(ObRadius.lg)),
            child: Text(n.read ? l10n.ntMarkUnread : l10n.ntMarkRead, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong)),
          ),
          child: ObCard(
            onTap: () => _open(n),
            padding: const EdgeInsets.all(14),
            color: n.read ? null : Color.alphaBlend(c.accentSoft.withValues(alpha: 0.5), c.surface),
            child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              IconTile(icon: notifIcon(n.type), tone: notifTone(n), size: 36, radius: 10),
              const SizedBox(width: 12),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(n.title, style: TextStyle(fontSize: 14, height: 1.3, fontWeight: n.read ? FontWeight.w400 : FontWeight.w600, color: c.ink)),
                  if (n.body != null) ...[
                    const SizedBox(height: 3),
                    Text(n.body!, maxLines: 3, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12.5, height: 1.35, color: c.ink2)),
                  ],
                  const SizedBox(height: 4),
                  Text(fmt.ago(DateTime.parse(n.createdAt), now), style: TextStyle(fontSize: 11, color: c.ink3)),
                  if (n.actions.any((a) => a.action == 'extend_due')) ...[
                    const SizedBox(height: 6),
                    Pressable(
                      onTap: () => _extend(n),
                      child: Text(l10n.ntExtendDue, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong)),
                    ),
                  ],
                ]),
              ),
              if (!n.read) Padding(padding: const EdgeInsets.only(top: 4, left: 6), child: Container(width: 8, height: 8, decoration: BoxDecoration(color: c.accent, shape: BoxShape.circle))),
            ]),
          ),
        ),
      ));
    }

    return ObPage(
      title: l10n.moduleNotifications,
      controller: _scroll,
      actions: [
        IconButton(
          icon: const Icon(LucideIcons.checkCheck, size: 20),
          tooltip: l10n.ntReadAll,
          onPressed: () => ref.read(notificationsProvider(_filter).notifier).readAll(),
        ),
      ],
      onRefresh: () async {
        await Future.wait([ref.read(notificationsProvider(_filter).notifier).refresh(), ref.read(unreadProvider.notifier).refresh()]);
      },
      slivers: [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              clipBehavior: Clip.none,
              child: Row(children: [
                for (final e in tabs.entries) ...[
                  FilterPill(label: e.value, chevron: false, active: e.key == _filter, onTap: () => setState(() => _filter = e.key)),
                  const SizedBox(width: 8),
                ],
              ]),
            ),
          ),
        ),
        if (s.isInitialLoading)
          const SliverToBoxAdapter(child: LoadingView())
        else if (s.items.isEmpty && s.failure != null)
          SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(notificationsProvider(_filter).notifier).refresh()))
        else if (s.items.isEmpty)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.bellOff, title: l10n.ntEmpty, compact: true))
        else
          SliverList.list(children: rows),
      ],
    );
  }
}
