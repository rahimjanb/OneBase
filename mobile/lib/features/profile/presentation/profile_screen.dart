import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/core_providers.dart';
import '../../../core/storage/preferences.dart';
import '../../../core/synchronization/outbox.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/segmented.dart';
import '../data/profile_api.dart';
import '../data/profile_models.dart';
import 'profile_providers.dart';

/// Профиль и настройки: кто вошёл, «Мой день», мои задачи, оформление и язык, «Что присылать», Telegram, безопасность,
/// синхронизация, о приложении, выход. Тема и язык — настройки телефона (сервер их не хранит); «Что присылать» — на сервере.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final profile = ref.watch(currentProfileProvider);
    final hasTasks = profile?.can(Perm.workTasks) ?? false;
    return ObPage(
      title: l10n.navProfile,
      onRefresh: () async {
        await Future.wait([
          ref.read(authControllerProvider.notifier).refreshProfile(force: true),
          ref.read(workdayBlock.notifier).refresh(),
          if (hasTasks) ref.read(myTasksBlock.notifier).refresh(),
          ref.read(notificationPrefsBlock.notifier).refresh(),
          ref.read(telegramBlock.notifier).refresh(),
        ]);
      },
      slivers: [
        SliverList.list(
          children: [
            const _UserCard(),
            const SizedBox(height: ObSpace.block),
            const _WorkdayCard(),
            if (hasTasks) const _MyTasksCard(),
            const _AppearanceCard(),
            const SizedBox(height: ObSpace.block),
            const _PrefsCard(),
            const SizedBox(height: ObSpace.block),
            const _TelegramCard(),
            const SizedBox(height: ObSpace.block),
            const _SyncCard(),
            const SizedBox(height: ObSpace.block),
            const _SecurityCard(),
            const SizedBox(height: ObSpace.block),
            const _AboutCard(),
            const SizedBox(height: ObSpace.block),
            const _LogoutButton(),
          ],
        ),
      ],
    );
  }
}

class _UserCard extends ConsumerWidget {
  const _UserCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    if (auth is! AuthSignedIn) return const SizedBox.shrink();
    final p = auth.profile;
    final c = context.ob;
    final l10n = context.l10n;
    final subtitle = [p.position, p.departmentName].whereType<String>().where((s) => s.trim().isNotEmpty).join(' · ');
    return ObCard(
      tint: c.kpi,
      radius: ObRadius.xl,
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 60,
                height: 60,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [c.accent, c.violet]),
                ),
                alignment: Alignment.center,
                child: Text(p.initials, style: const TextStyle(fontSize: 21, fontWeight: FontWeight.w700, color: Colors.white)),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(p.name.isEmpty ? p.login : p.name, style: context.text.titleLarge!.copyWith(fontSize: 19)),
                    if (subtitle.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(subtitle, style: TextStyle(fontSize: 13, color: c.ink3)),
                    ],
                    const SizedBox(height: 2),
                    Text(p.email ?? p.login, style: TextStyle(fontSize: 12, color: c.subtle)),
                  ],
                ),
              ),
            ],
          ),
          if (p.roles.isNotEmpty) ...[
            const SizedBox(height: 14),
            Wrap(spacing: 6, runSpacing: 6, children: [for (final r in p.roles) SoftBadge(label: r, tone: Tone.accent, size: 12)]),
          ],
          if (auth.offline) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Icon(LucideIcons.wifiOff, size: 14, color: c.warn),
                const SizedBox(width: 6),
                Expanded(child: Text(l10n.offlineProfile, style: TextStyle(fontSize: 12, color: c.warn))),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// «Мой день»: статус дня по Verifix, смена, приход и уход, неделя точками. Нет привязки к сотруднику — блока нет.
class _WorkdayCard extends ConsumerWidget {
  const _WorkdayCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final w = ref.watch(workdayBlock).data;
    if (w == null || !w.available || w.today == null) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    final t = w.today!;
    String hm(String? iso) => iso == null || iso.length < 16 ? '—' : iso.substring(11, 16);
    final (label, tone) = dayStatus(l10n, t);
    String minutes(int m) => l10n.myDayHoursMinutes(m ~/ 60, m % 60);
    return Padding(
      padding: const EdgeInsets.only(bottom: ObSpace.block),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SectionHeader(title: l10n.myDayTitle),
          const SizedBox(height: 12),
          ObCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    SoftBadge(label: label, tone: tone, size: 12),
                    const Spacer(),
                    if (t.shiftStart != null && t.shiftEnd != null)
                      Text(l10n.myDayShift(hm(t.shiftStart), hm(t.shiftEnd)), style: TextStyle(fontSize: 12, color: c.ink3)),
                  ],
                ),
                const SizedBox(height: 14),
                Row(
                  children: [
                    _Mini(label: l10n.myDayArrival, value: hm(t.arrival)),
                    _Mini(label: l10n.myDayDeparture, value: hm(t.departure)),
                    _Mini(label: l10n.myDayWorked, value: t.workedMinutes == null ? '—' : minutes(t.workedMinutes!)),
                  ],
                ),
                if (w.week.isNotEmpty) ...[
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      for (final d in w.week)
                        Expanded(
                          child: Column(
                            children: [
                              Container(
                                height: 6,
                                margin: const EdgeInsets.symmetric(horizontal: 2),
                                decoration: BoxDecoration(color: weekColor(c, d.status), borderRadius: BorderRadius.circular(3)),
                              ),
                              const SizedBox(height: 4),
                              Text(d.date.substring(8, 10), style: TextStyle(fontSize: 10, color: c.ink3)),
                            ],
                          ),
                        ),
                    ],
                  ),
                ],
                if ((w.totals?.lateCount ?? 0) > 0) ...[
                  const SizedBox(height: 10),
                  Text('${l10n.myDayLateWeek}: ${w.totals!.lateCount}', style: TextStyle(fontSize: 12, color: c.warn)),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

(String, Tone) dayStatus(AppLocalizations l10n, WorkdayToday t) => switch (t.status) {
      'at_work' => ((t.lateMinutes ?? 0) > 0 ? l10n.dayLate(t.lateMinutes!) : l10n.dayStatus_at_work, (t.lateMinutes ?? 0) > 0 ? Tone.warn : Tone.ok),
      'on_break' => (l10n.dayStatus_on_break, Tone.accent),
      'out' => (l10n.dayStatus_out, Tone.neutral),
      'finished' => (l10n.dayStatus_finished, Tone.ok),
      'not_started' => (l10n.dayStatus_not_started, Tone.neutral),
      'day_off' => (l10n.dayStatus_day_off, Tone.neutral),
      'on_leave' => (t.leaveName ?? l10n.dayStatus_on_leave, Tone.violet),
      'absent' => (l10n.dayStatus_absent, Tone.bad),
      _ => (l10n.dayStatus_other, Tone.neutral),
    };

Color weekColor(ObColors c, String status) => switch (status) {
      'ok' => c.ok,
      'late' || 'early_leave' => c.warn,
      'absent' => c.bad,
      'leave' => c.violet,
      'future' || 'not_started' => c.muted,
      _ => c.lineStrong,
    };

class _Mini extends StatelessWidget {
  const _Mini({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: TextStyle(fontSize: 11, color: c.ink3)),
          const SizedBox(height: 3),
          Text(value, style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: c.ink)),
        ],
      ),
    );
  }
}

class _MyTasksCard extends ConsumerWidget {
  const _MyTasksCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = ref.watch(myTasksBlock).data;
    if (t == null || !t.available) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    return Padding(
      padding: const EdgeInsets.only(bottom: ObSpace.block),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SectionHeader(title: l10n.myTasksTitle, onMore: () => context.go('/tasks?view=my'), moreLabel: l10n.actionAll),
          const SizedBox(height: 12),
          ObCard(
            onTap: () => context.go('/tasks?view=my'),
            child: Row(
              children: [
                _Mini(label: l10n.myTasksNew, value: '${t.fresh}'),
                _Mini(label: l10n.tasksInWork, value: '${t.inProgress}'),
                _Mini(label: l10n.myTasksWaiting, value: '${t.waiting}'),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(l10n.tasksOverdueLabel, style: TextStyle(fontSize: 11, color: c.ink3)),
                      const SizedBox(height: 3),
                      Text('${t.overdue}', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: t.overdue > 0 ? c.bad : c.ink)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _AppearanceCard extends ConsumerWidget {
  const _AppearanceCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final mode = ref.watch(themeModeProvider);
    final locale = ref.watch(localeProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAppearance),
        const SizedBox(height: 12),
        ObCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              ObSegmented<ThemeMode>(
                values: const [ThemeMode.dark, ThemeMode.light, ThemeMode.system],
                selected: mode,
                label: (m) => switch (m) {
                  ThemeMode.dark => l10n.themeDark,
                  ThemeMode.light => l10n.themeLight,
                  ThemeMode.system => l10n.themeSystem,
                },
                onChanged: (m) => ref.read(themeModeProvider.notifier).set(m),
              ),
              const SizedBox(height: 16),
              Text(l10n.language, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: c.ink3)),
              const SizedBox(height: 8),
              ObSegmented<String>(
                values: const ['ru', 'uz', 'system'],
                selected: locale?.languageCode ?? 'system',
                label: (v) => switch (v) {
                  'ru' => l10n.langRu,
                  'uz' => l10n.langUz,
                  _ => l10n.langSystem,
                },
                onChanged: (v) => ref.read(localeProvider.notifier).set(v == 'system' ? null : Locale(v)),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _PrefsCard extends ConsumerWidget {
  const _PrefsCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationPrefsBlock);
    final prefs = state.data;
    if (prefs == null) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    String label(String code) => switch (code) {
          'tasks' => l10n.prefCat_tasks,
          'ai' => l10n.prefCat_ai,
          'hr' => l10n.prefCat_hr,
          'sales' => l10n.prefCat_sales,
          'system' => l10n.prefCat_system,
          _ => code,
        };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.prefsTitle),
        const SizedBox(height: 12),
        ObCard(
          padding: const EdgeInsets.fromLTRB(18, 8, 10, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final code in prefs.categories)
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  title: Text(label(code), style: TextStyle(fontSize: 15, color: c.ink)),
                  value: !prefs.muted.contains(code),
                  onChanged: (v) async {
                    final messenger = ScaffoldMessenger.of(context);
                    final result = await ref.read(notificationPrefsBlock.notifier).toggle(code, enabled: v);
                    if (result == SendResult.queued) {
                      messenger.showSnackBar(SnackBar(content: Text(l10n.prefsQueued)));
                    } else if (result == SendResult.rejected) {
                      messenger.showSnackBar(SnackBar(content: Text(l10n.prefsRejected(l10n.errorUnknown))));
                    }
                  },
                ),
              Text(l10n.prefsHint, style: TextStyle(fontSize: 12, height: 1.4, color: c.ink3)),
            ],
          ),
        ),
      ],
    );
  }
}

class _TelegramCard extends ConsumerWidget {
  const _TelegramCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(telegramBlock);
    final links = state.data;
    if (links == null) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.telegramTitle),
        const SizedBox(height: 12),
        ObCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (links.isEmpty) Text(l10n.telegramNone, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink2)),
              for (final l in links)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Row(
                    children: [
                      const IconTile(icon: LucideIcons.send, tone: Tone.accent, size: 36, radius: 10),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(l.username == null ? l.account : '@${l.username}', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                            Text(l.mode == 'Full' ? l10n.telegramFull : l10n.telegramLimited, style: TextStyle(fontSize: 12, color: c.ink3)),
                          ],
                        ),
                      ),
                      TextButton(onPressed: () => _unlink(context, ref, l), child: Text(l10n.telegramUnlink, style: TextStyle(color: c.bad))),
                    ],
                  ),
                ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: () => _link(context, ref),
                icon: const Icon(LucideIcons.link, size: 18),
                label: Text(l10n.telegramLink),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Future<void> _unlink(BuildContext context, WidgetRef ref, TelegramLink l) async {
    final l10n = context.l10n;
    final ok = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(l10n.telegramUnlink),
        content: Text(l10n.telegramUnlinkConfirm),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
          TextButton(onPressed: () => Navigator.pop(context, true), child: Text(l10n.telegramUnlink)),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ref.read(profileApiProvider).unlinkTelegram(l.permissionId);
    } on Object {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.errorUnknown)));
    }
    await ref.read(telegramBlock.notifier).refresh();
  }

  Future<void> _link(BuildContext context, WidgetRef ref) async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    TelegramCode code;
    try {
      code = await ref.read(profileApiProvider).telegramCode();
    } on Object {
      messenger.showSnackBar(SnackBar(content: Text(l10n.errorUnknown)));
      return;
    }
    if (!context.mounted) return;
    final c = context.ob;
    await showAdaptiveDialog<void>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(l10n.telegramCodeTitle),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SelectableText(code.code, style: TextStyle(fontSize: 28, fontWeight: FontWeight.w700, letterSpacing: 2, color: c.ink)),
            const SizedBox(height: 8),
            Text(l10n.telegramCodeHint(code.sendTo.join(', ')), textAlign: TextAlign.center),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () {
              unawaited(Clipboard.setData(ClipboardData(text: code.code)));
              messenger.showSnackBar(SnackBar(content: Text(l10n.copied)));
            },
            child: Text(l10n.actionCopy),
          ),
          TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionClose)),
        ],
      ),
    );
    await ref.read(telegramBlock.notifier).refresh();
  }
}

class _SyncCard extends ConsumerWidget {
  const _SyncCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ops = ref.watch(pendingOpsProvider).value ?? const <PendingOp>[];
    final c = context.ob;
    final l10n = context.l10n;
    final pending = ops.where((o) => !o.failed).toList();
    final failed = ops.where((o) => o.failed).toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.syncTitle),
        const SizedBox(height: 12),
        ObCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(pending.isEmpty ? LucideIcons.circleCheck : LucideIcons.cloudUpload, size: 18, color: pending.isEmpty ? c.ok : c.warn),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(pending.isEmpty ? l10n.syncAllSent : l10n.syncPending(pending.length),
                        style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: c.ink)),
                  ),
                  if (pending.isNotEmpty) TextButton(onPressed: () => ref.read(outboxProvider).flush(), child: Text(l10n.syncNow)),
                ],
              ),
              for (final f in failed)
                Padding(
                  padding: const EdgeInsets.only(top: 10),
                  child: Row(
                    children: [
                      Icon(LucideIcons.triangleAlert, size: 16, color: c.bad),
                      const SizedBox(width: 8),
                      Expanded(child: Text(l10n.syncFailed(f.error ?? f.kind), style: TextStyle(fontSize: 12, color: c.bad))),
                      IconButton(
                        icon: const Icon(LucideIcons.x, size: 16),
                        tooltip: l10n.actionClose,
                        onPressed: () => ref.read(outboxProvider).dismiss(f.id),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

final _cacheCountProvider = FutureProvider.autoDispose<int>((ref) => ref.read(cacheStoreProvider).count());

class _SecurityCard extends ConsumerWidget {
  const _SecurityCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final count = ref.watch(_cacheCountProvider).value ?? 0;
    Widget row(IconData icon, String title, String hint, {Widget? trailing}) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              IconTile(icon: icon, size: 34, radius: 10),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: c.ink)),
                    const SizedBox(height: 2),
                    Text(hint, style: TextStyle(fontSize: 12, height: 1.35, color: c.ink3)),
                  ],
                ),
              ),
              ?trailing,
            ],
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.securityTitle),
        const SizedBox(height: 12),
        ObCard(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
          child: Column(
            children: [
              row(LucideIcons.keyRound, l10n.securityPassword, l10n.securityPasswordHint),
              row(LucideIcons.smartphone, l10n.securitySession, l10n.securitySessionHint),
              row(
                LucideIcons.hardDrive,
                l10n.securityStorage,
                l10n.securityStorageHint(count),
                trailing: TextButton(
                  onPressed: () async {
                    final me = ref.read(currentProfileProvider);
                    await ref.read(cacheStoreProvider).clear(keepUserId: '__none__');
                    if (me != null) await ref.read(authControllerProvider.notifier).refreshProfile(force: true);
                    ref.invalidate(_cacheCountProvider);
                    if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.cacheCleared)));
                  },
                  child: Text(l10n.securityClearCache),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _AboutCard extends ConsumerWidget {
  const _AboutCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final config = ref.watch(appConfigProvider);
    Widget line(String a, String b) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Row(
            children: [
              Expanded(child: Text(a, style: TextStyle(fontSize: 14, color: c.ink2))),
              Text(b, style: TextStyle(fontSize: 14, color: c.ink)),
            ],
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAbout),
        const SizedBox(height: 12),
        ObCard(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
          child: Column(
            children: [
              line(l10n.profileVersion(config.appVersion), config.env.name),
              line(l10n.profileServer, Uri.tryParse(config.apiBaseUrl)?.host ?? config.apiBaseUrl),
            ],
          ),
        ),
      ],
    );
  }
}

class _LogoutButton extends ConsumerWidget {
  const _LogoutButton();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    return Pressable(
      onTap: () async {
        final ok = await showAdaptiveDialog<bool>(
          context: context,
          builder: (context) => AlertDialog.adaptive(
            title: Text(l10n.logoutConfirmTitle),
            content: Text(l10n.logoutConfirmMessage),
            actions: [
              TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
              TextButton(onPressed: () => Navigator.pop(context, true), child: Text(l10n.logout)),
            ],
          ),
        );
        if (ok == true) await ref.read(authControllerProvider.notifier).logout();
      },
      semanticLabel: l10n.logout,
      child: Container(
        height: 52,
        decoration: BoxDecoration(color: c.badSoft, borderRadius: BorderRadius.circular(ObRadius.tile), border: Border.all(color: c.bad.withValues(alpha: 0.25))),
        alignment: Alignment.center,
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(LucideIcons.logOut, size: 18, color: c.bad),
            const SizedBox(width: 8),
            Text(l10n.logout, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.bad)),
          ],
        ),
      ),
    );
  }
}
