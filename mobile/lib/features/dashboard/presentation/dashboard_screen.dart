import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable;

import '../../../app/theme/tokens.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/core_providers.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/offline_banner.dart';
import '../domain/dashboard_period.dart';
import 'dashboard_providers.dart';
import 'widgets/departments_section.dart';
import 'widgets/events_section.dart';
import 'widgets/filter_bar.dart';
import 'widgets/finance_section.dart';
import 'widgets/insights_section.dart';
import 'widgets/sales_section.dart';
import 'widgets/tasks_section.dart';

/// Главная — Dashboard на данных OneBase. Каждый блок грузится сам по себе и показывает последнее успешное состояние (с временем
/// синхронизации), если сеть или сервер подвели; медленный или упавший блок не держит остальные. Какие блоки видны — по правам
/// пользователя. Потянуть вниз — обновить все.
class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final access = ref.watch(dashboardAccessProvider);
    final filter = ref.watch(dashboardFilterProvider);
    final profile = ref.watch(currentProfileProvider);
    final now = ref.watch(clockProvider)();
    final name = (profile?.firstName?.trim().isNotEmpty ?? false) ? profile!.firstName!.trim() : (profile?.name ?? '');
    final nothing = !access.sales && !access.finance && !access.hr && !access.tasks && !access.ai;
    keepBlocksAlive(ref, access, filter);

    return Scaffold(
      body: RefreshIndicator.adaptive(
        onRefresh: () => refreshDashboard(ref),
        edgeOffset: 120,
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(parent: BouncingScrollPhysics()),
          slivers: [
            ObSliverAppBar(
              title: l10n.navHome,
              subtitle: Text('${l10n.greetingName(greeting(l10n, now), name)} · ${context.fmt.weekdayDate(now)}'),
            ),
            const SliverToBoxAdapter(child: OfflineBanner()),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, 32),
              sliver: SliverList.list(
                children: [
                  if (access.sales || access.finance) const DashboardFilterBar(),
                  if (access.sales) SalesSection(period: filter.period),
                  if (access.ai) const InsightsSection(),
                  if (access.finance) FinanceSection(args: (period: filter.period, organization: filter.organizationId)),
                  if (access.hr) const DepartmentsSection(),
                  if (access.tasks) const TasksSection(),
                  if (nothing)
                    Padding(
                      padding: const EdgeInsets.only(top: ObSpace.lg),
                      child: Text(l10n.dashboardNothing, style: context.text.bodyMedium),
                    ),
                  const EventsSection(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Блоки живут, пока открыт Dashboard, а не пока секция на экране: ленивый список закрывает ушедшие за край секции, и без этого
/// блок грузился бы заново при каждой прокрутке. `listen` не перестраивает экран — перестраиваются только сами секции.
void keepBlocksAlive(WidgetRef ref, DashboardAccess access, DashboardFilter filter) {
  void keep<T>(ProviderListenable<T> p) => ref.listen<T>(p, (_, _) {});
  if (access.sales) {
    keep(salesOverviewBlock(filter.period));
    keep(salesDailyBlock(filter.period));
    keep(salesMonthsBlock);
  }
  if (access.finance) {
    keep(financeBlock((period: filter.period, organization: filter.organizationId)));
    keep(financeOrganizationsBlock);
  }
  if (access.hr) keep(hrBlock);
  if (access.tasks) keep(workBlock);
  if (access.ai) keep(alertsBlock);
  keep(eventsBlock);
}

/// Приветствие по времени суток.
String greeting(AppLocalizations l10n, DateTime now) => switch (now.hour) {
      >= 5 && < 12 => l10n.greetingMorning,
      >= 12 && < 18 => l10n.greetingDay,
      >= 18 && < 23 => l10n.greetingEvening,
      _ => l10n.greetingNight,
    };
