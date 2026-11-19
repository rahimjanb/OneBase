import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/shell/bottom_nav.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/navigation/web_links.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/states.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../../dashboard/presentation/dashboard_providers.dart';
import '../../dashboard/presentation/widgets/home_labels.dart';

/// AI-сотрудник (`GET /api/ai/agents`, бесплатно): главный консультант и сотрудники отделов.
typedef AiAgentView = ({String code, String name, String? role, String? description, bool isConsultant, bool available});

class AiAgentsBlock extends CachedBlock<List<AiAgentView>> {
  @override
  String get cacheKey => 'insights/agents';

  @override
  Future<List<AiAgentView>> fetch(CancelToken cancel) => ref.read(apiClientProvider).get('/api/ai/agents', _decode, cancel: cancel);

  static List<AiAgentView> _decode(Object? j) => [
    for (final a in (j! as List<Object?>).cast<Map<String, Object?>>())
      (
        code: a['code'] as String? ?? '',
        name: a['name'] as String? ?? '',
        role: a['role'] as String?,
        description: a['description'] as String?,
        isConsultant: a['isConsultant'] as bool? ?? false,
        available: a['available'] as bool? ?? false,
      ),
  ];

  @override
  List<AiAgentView> decode(Object? json) => _decode(json);

  @override
  Object? encode(List<AiAgentView> data) => [
    for (final a in data)
      {'code': a.code, 'name': a.name, 'role': a.role, 'description': a.description, 'isConsultant': a.isConsultant, 'available': a.available},
  ];
}

final aiAgentsBlock = NotifierProvider.autoDispose<AiAgentsBlock, BlockState<List<AiAgentView>>>(AiAgentsBlock.new);

String _dateTime(String iso) => DateFormat('dd.MM.yyyy, HH:mm').format(DateTime.parse(iso).toUtc().add(const Duration(hours: 5)));

/// «AI-находки» — AI Dashboard веба: что нашли правила по данным (без модели — цифры точные), по отделам; у каждой находки — «Открыть
/// данные» (экран приложения) и «Спросить консультанта» (вопрос подставляется в поле, отправляет пользователь); ниже — AI-сотрудники.
/// «Проверить сейчас» — только у администратора AI (`ai.settings.manage`).
class InsightsScreen extends ConsumerStatefulWidget {
  const InsightsScreen({super.key});

  @override
  ConsumerState<InsightsScreen> createState() => _InsightsScreenState();
}

class _InsightsScreenState extends ConsumerState<InsightsScreen> {
  String? _category;
  bool _running = false;
  String? _runError;

  Future<void> _run() async {
    setState(() {
      _running = true;
      _runError = null;
    });
    try {
      await ref.read(apiClientProvider).post<void>('/api/ai/alerts/run');
      await ref.read(alertsBlock.notifier).refresh();
    } on Object catch (e) {
      if (mounted) setState(() => _runError = AppFailure.from(e).describe(context.l10n));
    } finally {
      if (mounted) setState(() => _running = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final block = ref.watch(alertsBlock);
    final agents = ref.watch(aiAgentsBlock).data ?? const <AiAgentView>[];
    final canRun = ref.watch(currentProfileProvider)?.can('ai.settings.manage') ?? false;
    final data = block.data;
    final shown = data == null ? const <AiAlert>[] : data.alerts.where((a) => _category == null || a.category == _category).toList();
    final consultant = agents.where((a) => a.isConsultant).firstOrNull;
    final staff = agents.where((a) => !a.isConsultant).toList();

    return ObPage(
      title: l10n.moduleInsights,
      onRefresh: () async {
        await Future.wait([ref.read(alertsBlock.notifier).refresh(), ref.read(aiAgentsBlock.notifier).refresh()]);
      },
      slivers: [
        if (data == null)
          SliverFillRemaining(
            hasScrollBody: false,
            child: block.failure != null
                ? ErrorView(failure: block.failure!, onRetry: () => ref.read(alertsBlock.notifier).refresh())
                : const LoadingView(),
          )
        else ...[
          SliverPadding(
            padding: const EdgeInsets.only(top: 4),
            sliver: SliverToBoxAdapter(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(l10n.inSubtitle, style: TextStyle(fontSize: 13.5, height: 1.4, color: c.ink3)),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: _Tile(label: l10n.inProblems, value: data.summary.problems),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _Tile(label: l10n.inCriticalCount, value: data.summary.critical, color: data.summary.critical > 0 ? c.bad : null),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: _Tile(label: l10n.inRecommendations, value: data.summary.recommendations),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _Tile(
                          label: l10n.inOpportunities,
                          value: data.summary.opportunities,
                          color: data.summary.opportunities > 0 ? c.ok : null,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: Row(
                      children: [
                        FilterPill(
                          label: '${l10n.inAll} · ${data.alerts.length}',
                          active: _category == null,
                          chevron: false,
                          onTap: () => setState(() => _category = null),
                        ),
                        for (final cat in data.summary.byCategory)
                          if (cat.problems + cat.opportunities > 0)
                            Padding(
                              padding: const EdgeInsets.only(left: 8),
                              child: FilterPill(
                                label: [
                                  '${categoryName(l10n, cat.category)} · ${cat.problems + cat.opportunities}',
                                  if (cat.critical > 0) l10n.inCritShort(cat.critical),
                                ].join(' '),
                                active: _category == cat.category,
                                chevron: false,
                                onTap: () => setState(() => _category = cat.category),
                              ),
                            ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          data.lastRun?.at == null ? l10n.inSchedule : l10n.inChecked(_dateTime(data.lastRun!.at!)),
                          style: TextStyle(fontSize: 12, color: c.ink3),
                        ),
                      ),
                      if (canRun)
                        TextButton.icon(
                          onPressed: _running ? null : _run,
                          icon: _running
                              ? const SizedBox.square(dimension: 14, child: CircularProgressIndicator(strokeWidth: 2))
                              : const Icon(LucideIcons.refreshCw, size: 15),
                          label: Text(l10n.inRunNow),
                        ),
                    ],
                  ),
                  if (_runError != null) Text(_runError!, style: TextStyle(fontSize: 13, color: c.bad)),
                  if (data.lastRun?.errors.isNotEmpty ?? false)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(l10n.inPartial(data.lastRun!.errors.join('; ')), style: TextStyle(fontSize: 13, color: c.warn)),
                    ),
                  const SizedBox(height: 12),
                ],
              ),
            ),
          ),
          if (shown.isEmpty)
            SliverToBoxAdapter(
              child: Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(ObRadius.md),
                  border: Border.all(color: c.line),
                ),
                child: Text(
                  data.alerts.isEmpty ? l10n.inNone : l10n.inNoneInDept,
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 14, color: c.ink2),
                ),
              ),
            )
          else
            SliverList.separated(
              itemCount: shown.length,
              separatorBuilder: (context, i) => const SizedBox(height: 10),
              itemBuilder: (context, i) => AlertCard(alert: shown[i]),
            ),
          SliverPadding(
            padding: EdgeInsets.only(top: 14, bottom: navContentInset(context)),
            sliver: SliverToBoxAdapter(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(l10n.inNote, style: TextStyle(fontSize: 12, height: 1.45, color: c.ink3)),
                  if (consultant != null) ...[
                    const SizedBox(height: 24),
                    ObCard(
                      tint: c.kpi,
                      onTap: () => context.go('/ai'),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(LucideIcons.sparkle, size: 14, color: c.accentStrong),
                              const SizedBox(width: 6),
                              Text(
                                consultant.name.toUpperCase(),
                                style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, letterSpacing: 0.6, color: c.accentStrong),
                              ),
                            ],
                          ),
                          if (consultant.description != null) ...[
                            const SizedBox(height: 8),
                            Text(consultant.description!, style: TextStyle(fontSize: 14, height: 1.4, color: c.ink2)),
                          ],
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Text(
                                l10n.inAskConsultant,
                                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.accentStrong),
                              ),
                              const SizedBox(width: 4),
                              Icon(LucideIcons.arrowRight, size: 15, color: c.accentStrong),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                  if (staff.isNotEmpty) ...[
                    const SizedBox(height: 24),
                    Text(l10n.inAgents, style: context.text.titleMedium),
                    const SizedBox(height: 4),
                    Text(l10n.inAgentsNote, style: TextStyle(fontSize: 12.5, height: 1.4, color: c.ink3)),
                    const SizedBox(height: 12),
                    for (final a in staff)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: ObCard(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          a.name,
                                          style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink),
                                        ),
                                        if (a.role != null) Text(a.role!, style: TextStyle(fontSize: 13, color: c.ink3)),
                                      ],
                                    ),
                                  ),
                                  SoftBadge(label: a.available ? l10n.inAgentOn : l10n.inAgentOff, tone: a.available ? Tone.ok : Tone.neutral),
                                ],
                              ),
                              if (a.description != null) ...[
                                const SizedBox(height: 8),
                                Text(a.description!, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink2)),
                              ],
                            ],
                          ),
                        ),
                      ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.label, required this.value, this.color});

  final String label;
  final int value;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return ObCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 12.5, color: c.ink2),
          ),
          const SizedBox(height: 8),
          Text(
            '$value',
            style: TextStyle(fontSize: 28, height: 1, fontWeight: FontWeight.w700, color: color ?? c.ink),
          ),
        ],
      ),
    );
  }
}

/// Карточка находки: важность, отдел, «с дата», заголовок, текст, рекомендация, переходы.
class AlertCard extends StatelessWidget {
  const AlertCard({super.key, required this.alert});

  final AiAlert alert;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final (tone, label, icon) = switch (alert.severity) {
      'Critical' => (Tone.bad, l10n.inCritical, LucideIcons.triangleAlert),
      'Warning' => (Tone.warn, l10n.inWarning, LucideIcons.triangleAlert),
      _ => (Tone.ok, l10n.inOpportunity, LucideIcons.lightbulb),
    };
    final ask = l10n.inAskText(alert.title, alert.message ?? '');
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: c.surface,
        borderRadius: BorderRadius.circular(ObRadius.md),
        border: Border.all(color: c.line),
      ),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ColoredBox(color: c.toneFg(tone), child: const SizedBox(width: 4)),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        SoftBadge(label: label, tone: tone, icon: icon),
                        SoftBadge(label: categoryName(l10n, alert.category), tone: Tone.neutral),
                        if (alert.detectedAt != null) Text(l10n.inSince(_dateTime(alert.detectedAt!)), style: TextStyle(fontSize: 11, color: c.ink3)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      alert.title,
                      style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, height: 1.3, color: c.ink),
                    ),
                    if (alert.message != null) ...[
                      const SizedBox(height: 4),
                      Text(alert.message!, style: TextStyle(fontSize: 13.5, height: 1.4, color: c.ink2)),
                    ],
                    if (alert.recommendation != null) ...[
                      const SizedBox(height: 8),
                      Text.rich(
                        TextSpan(
                          children: [
                            TextSpan(
                              text: '${l10n.inRecommendation} ',
                              style: const TextStyle(fontWeight: FontWeight.w600),
                            ),
                            TextSpan(text: alert.recommendation),
                          ],
                        ),
                        style: TextStyle(fontSize: 13.5, height: 1.4, color: c.ink),
                      ),
                    ],
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 16,
                      runSpacing: 8,
                      children: [
                        if (alert.href != null)
                          _Link(
                            label: l10n.inOpenData,
                            icon: LucideIcons.arrowRight,
                            trailing: true,
                            onTap: () => context.push(appRouteForWeb(alert.href)),
                          ),
                        _Link(
                          label: l10n.inAsk,
                          icon: LucideIcons.messageSquare,
                          onTap: () => context.go('/ai?ask=${Uri.encodeQueryComponent(ask)}'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Link extends StatelessWidget {
  const _Link({required this.label, required this.icon, required this.onTap, this.trailing = false});

  final String label;
  final IconData icon;
  final VoidCallback onTap;
  final bool trailing;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final i = Icon(icon, size: 14, color: c.accentStrong);
    return Pressable(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (!trailing) ...[i, const SizedBox(width: 5)],
            Flexible(
              child: Text(
                label,
                style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: c.accentStrong),
              ),
            ),
            if (trailing) ...[const SizedBox(width: 4), i],
          ],
        ),
      ),
    );
  }
}
