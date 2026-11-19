import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable;
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/shell/bottom_nav.dart';
import '../../../app/theme/decor.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/pills.dart';
import '../../notifications/presentation/notifications_screen.dart';
import 'dashboard_providers.dart';
import 'widgets/home_body.dart';
import 'widgets/home_extras.dart';
import 'widgets/home_head.dart';

/// Главная — мобильный «Обзор» веба: приветствие, «Ждут вас», фильтры, главный показатель, быстрые действия, «Фокус дня», динамика,
/// задачи, отделы; ниже — продажи месяца, финансы 1С и события; AI-отчёт и свежесть данных. Каждый блок грузится сам по себе и
/// показывает последнее успешное состояние; без права блок не запрашивается.
class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final access = ref.watch(dashboardAccessProvider);
    keepBlocksAlive(ref, access);
    final profile = ref.watch(currentProfileProvider);
    final name = (profile?.firstName?.trim().isNotEmpty ?? false) ? profile!.firstName!.trim() : (profile?.name ?? '');
    final top = MediaQuery.paddingOf(context).top;

    final children = <Widget>[
      const OfflineNote(),
      HomeGreeting(name: name),
      if (access.procurement) const HomeAttention(),
      const HomeFiltersBar(),
      const KpiHero(),
      const QuickActions(),
      if (access.ai) const FocusCard(),
      if (access.sales || access.hr) const DynamicsCard(),
      if (access.tasks) const TasksCard(),
      const DepartmentsCard(),
      if (access.sales) const SalesMonthCard(),
      if (access.finance) const FinanceCard(),
      const EventsCard(),
      if (access.ai) const ReportBanner(),
      DataStatus(onRetry: () => refreshHome(ref)),
    ];

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: ObBackground(
        child: Scaffold(
          backgroundColor: Colors.transparent,
          body: RefreshIndicator.adaptive(
            edgeOffset: top + _HomeTopBar.height,
            onRefresh: () => refreshHome(ref),
            child: CustomScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              slivers: [
                SliverPersistentHeader(pinned: true, delegate: _HomeTopBarDelegate(top: top)),
                SliverPadding(
                  padding: EdgeInsets.fromLTRB(ObSpace.page, 8, ObSpace.page, navContentInset(context)),
                  sliver: SliverList.separated(
                    itemCount: children.length,
                    itemBuilder: (context, i) => children[i],
                    separatorBuilder: (context, i) => _gapAfter(children[i]),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// Пустые блоки (нет доступа или данных) не оставляют двойного отступа: у них нулевая высота, отступ остаётся один.
  static Widget _gapAfter(Widget w) => const SizedBox(height: ObSpace.block);
}

/// Блоки живут, пока открыт экран, а не пока секция видна: иначе ленивый список закрывал бы ушедшую за край секцию, и прокрутка
/// запускала бы загрузку заново.
void keepBlocksAlive(WidgetRef ref, DashboardAccess access) {
  void keep<T>(ProviderListenable<T> p) => ref.listen<T>(p, (_, _) {});
  if (access.sales) keep(homeSalesBlock);
  if (access.hr) keep(homeHrBlock);
  if (access.sales || access.hr) keep(homeTrendsBlock);
  if (access.tasks) keep(workBlock);
  if (access.ai) keep(alertsBlock);
  if (access.procurement) keep(procurementHomeBlock);
  if (access.finance) keep(homeFinanceBlock);
  keep(eventsBlock);
}

class _HomeTopBarDelegate extends SliverPersistentHeaderDelegate {
  _HomeTopBarDelegate({required this.top});

  final double top;

  @override
  double get minExtent => top + _HomeTopBar.height;

  @override
  double get maxExtent => top + _HomeTopBar.height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) =>
      SizedBox.expand(child: _HomeTopBar(top: top, scrolled: overlapsContent || shrinkOffset > 0));

  @override
  bool shouldRebuild(_HomeTopBarDelegate old) => old.top != top;
}

/// Шапка главной как у веба на телефоне: логотип 1Base, поиск, уведомления со счётчиком, профиль. Тёмная в обеих темах.
class _HomeTopBar extends ConsumerWidget {
  const _HomeTopBar({required this.top, required this.scrolled});

  static const height = 60.0;
  final double top;
  final bool scrolled;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final profile = ref.watch(currentProfileProvider);
    final int unread = ref.watch(unreadProvider)?.unread ?? ref.watch(eventsBlock.select<int>((s) => s.data?.unread ?? 0));
    final bg = c.isDark ? c.page.withValues(alpha: scrolled ? 0.82 : 0) : c.header;
    return ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: c.isDark && scrolled ? 18 : 0, sigmaY: c.isDark && scrolled ? 18 : 0),
        child: Container(
          color: bg,
          padding: EdgeInsets.only(top: top, left: ObSpace.page, right: 12),
          child: Row(
            children: [
              Semantics(
                label: 'OneBase',
                image: true,
                child: Image.asset('assets/brand/logo-light.png', height: 26, filterQuality: FilterQuality.medium),
              ),
              const Spacer(),
              HeaderButton(onDark: true, icon: LucideIcons.search, semanticLabel: l10n.searchTitle, onTap: () => context.go('/home/search')),
              const SizedBox(width: 4),
              HeaderButton(
                onDark: true,
                icon: LucideIcons.bell,
                badge: unread,
                semanticLabel: unread > 0 ? l10n.notificationsUnread(unread) : l10n.moduleNotifications,
                onTap: () => context.go('/home/notifications'),
              ),
              const SizedBox(width: 4),
              HeaderButton(
                onDark: true,
                semanticLabel: l10n.profileOpen,
                onTap: () => context.go('/profile'),
                child: Text(
                  profile?.initials ?? '?',
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFFEEF1F7)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
