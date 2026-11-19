import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:share_plus/share_plus.dart';

import '../../../app/theme/decor.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/auth/user_profile.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/navigation/web_links.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/markdown_view.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/rings.dart';
import '../../../shared/widgets/states.dart';
import '../../dashboard/domain/home_view.dart';
import '../data/consultant_api.dart';
import '../data/consultant_models.dart';
import 'consultant_controller.dart';

class AiStatusBlock extends CachedBlock<AiStatus> {
  @override
  String get cacheKey => 'ai/status';

  @override
  bool get persist => false;

  @override
  Future<AiStatus> fetch(CancelToken cancel) => ref.read(consultantApiProvider).status(cancel);

  @override
  AiStatus decode(Object? json) => AiStatus.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AiStatus data) => data.toJson();
}

final aiStatusBlock = NotifierProvider.autoDispose<AiStatusBlock, BlockState<AiStatus>>(AiStatusBlock.new);

/// Быстрые вопросы — тот же список, что у веба (`quickQuestions`), по правам.
List<(IconData, Color, String)> quickQuestions(AppLocalizations l10n, UserProfile? p) => [
      if (p?.can(Perm.salesRead) ?? false) ...[
        (LucideIcons.trendingUp, const Color(0xFF3D7BFF), l10n.aiQ1),
        (LucideIcons.target, const Color(0xFFF6A93B), l10n.aiQ2),
        (LucideIcons.trendingDown, const Color(0xFFFF5A6A), l10n.aiQ3),
        (LucideIcons.gitCompare, const Color(0xFFA78BFA), l10n.aiQ4),
        (LucideIcons.package, const Color(0xFF14B8A6), l10n.aiQ5),
      ],
      if ((p?.can(Perm.hrRead) ?? false) || (p?.hrAccess ?? false)) (LucideIcons.userX, const Color(0xFF06B6D4), l10n.aiQ6),
      if (p?.can(Perm.financeRead) ?? false) (LucideIcons.wallet, const Color(0xFF2BD47D), l10n.aiQ7),
      if (p?.can(Perm.salesRead) ?? false) (LucideIcons.lightbulb, const Color(0xFFF6A93B), l10n.aiQ8),
      if (p?.can(Perm.procurementRead) ?? false) (LucideIcons.truck, const Color(0xFFF97316), l10n.aiQ9),
      if (p?.can('procurement.request') ?? false) (LucideIcons.clipboardPen, const Color(0xFF7C3AED), l10n.aiQ10),
    ];

/// «Консультант»: вопросы обычным языком, ответ по данным OneBase потоком — шаги AI-сотрудников, черновик (числа ещё не проверены),
/// затем ответ из блоков (главное, KPI, факты, отклонения, что сделать, Markdown с таблицами), источники и проверка чисел, уточнения.
/// [ask] — вопрос из главной или находки: подставляется в поле, отправляет пользователь (вызов модели платный).
class AiScreen extends ConsumerStatefulWidget {
  const AiScreen({super.key, this.conversationId, this.ask});

  final String? conversationId;
  final String? ask;

  @override
  ConsumerState<AiScreen> createState() => _AiScreenState();
}

class _AiScreenState extends ConsumerState<AiScreen> {
  late final _input = TextEditingController(text: widget.ask ?? '');
  final _scroll = ScrollController();

  @override
  void didUpdateWidget(AiScreen old) {
    super.didUpdateWidget(old);
    // Вкладка уже открыта, а пришёл новый вопрос по ссылке (`?ask=`) — подставить его в поле.
    if (widget.ask != null && widget.ask != old.ask) _input.text = widget.ask!;
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _toBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) _scroll.animateTo(_scroll.position.maxScrollExtent, duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
    });
  }

  Future<void> _send([String? text]) async {
    final q = (text ?? _input.text).trim();
    if (q.isEmpty) return;
    unawaited(HapticFeedback.lightImpact());
    _input.clear();
    FocusScope.of(context).unfocus();
    final t = tashkentNow();
    _toBottom();
    await ref.read(consultantProvider(widget.conversationId).notifier).ask(q, context: {'period': '${t.year}-${t.month.toString().padLeft(2, '0')}'});
    final s = ref.read(consultantProvider(widget.conversationId));
    if (s.phase == ChatPhase.failed && s.pendingQuestion != null && _input.text.isEmpty) _input.text = s.pendingQuestion!;
    _toBottom();
  }

  Future<void> _history() async {
    final id = await showModalBottomSheet<String>(
      context: context,
      useRootNavigator: true,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (context) => const FractionallySizedBox(heightFactor: 0.85, child: _HistorySheet()),
    );
    if (id != null && mounted) context.go('/ai/c/$id');
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final s = ref.watch(consultantProvider(widget.conversationId));
    final status = ref.watch(aiStatusBlock).data;
    ref.listen(consultantProvider(widget.conversationId).select((s) => (s.messages.length, s.draft.length)), (_, _) => _toBottom());
    final lastAnswer = s.messages.lastIndexWhere((m) => m.role == 'assistant');
    final ready = status?.ready ?? true;
    final top = MediaQuery.paddingOf(context).top;
    return ObBackground(
      child: Scaffold(
        backgroundColor: Colors.transparent,
        resizeToAvoidBottomInset: true,
        body: Column(children: [
          Padding(
            padding: EdgeInsets.fromLTRB(8, top + 6, 8, 6),
            child: Row(children: [
              if (widget.conversationId != null)
                HeaderButton(icon: LucideIcons.chevronLeft, semanticLabel: MaterialLocalizations.of(context).backButtonTooltip, onTap: () => context.go('/ai'))
              else
                HeaderButton(icon: LucideIcons.history, semanticLabel: l10n.aiHistory, onTap: _history),
              const SizedBox(width: 8),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(s.title ?? l10n.aiTitle2, maxLines: 1, overflow: TextOverflow.ellipsis, style: context.text.titleLarge),
                  Row(children: [
                    Container(width: 6, height: 6, decoration: BoxDecoration(color: ready ? c.ok : c.bad, shape: BoxShape.circle)),
                    const SizedBox(width: 6),
                    Flexible(
                      child: Text(ready ? l10n.aiConnected : (status?.message ?? l10n.aiNotReady),
                          maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                    ),
                  ]),
                ]),
              ),
              HeaderButton(
                icon: LucideIcons.squarePen,
                semanticLabel: l10n.aiNewChat,
                onTap: () {
                  ref.read(consultantProvider(null).notifier).reset();
                  context.go('/ai');
                },
              ),
            ]),
          ),
          Expanded(
            child: s.loading
                ? const LoadingView()
                : s.messages.isEmpty && s.pendingQuestion == null
                    ? _Start(onAsk: _send)
                    : ListView(
                        controller: _scroll,
                        padding: const EdgeInsets.fromLTRB(ObSpace.page, 8, ObSpace.page, 16),
                        children: [
                          for (var i = 0; i < s.messages.length; i++)
                            s.messages[i].role == 'user'
                                ? _UserBubble(text: s.messages[i].content)
                                : _Answer(
                                    message: s.messages[i],
                                    last: i == lastAnswer && !s.busy,
                                    onFollowUp: _send,
                                    onRetry: () {
                                      final q = s.messages.take(i).lastWhere((m) => m.role == 'user', orElse: () => s.messages[i]).content;
                                      _send(q);
                                    },
                                  ),
                          if (s.pendingQuestion != null) _UserBubble(text: s.pendingQuestion!, pending: true),
                          if (s.busy) _Pending(state: s),
                          if (s.phase == ChatPhase.cancelled) _Note(text: l10n.aiStopped),
                          if (s.phase == ChatPhase.failed && s.failure != null && (s.messages.isEmpty || s.messages.last.status != 'failed'))
                            // Лимиты AI (429) — текст сервера точнее общего «слишком много запросов».
                            _Note(text: (s.failure!.kind == ApiErrorKind.rateLimited ? s.failure!.serverMessage : null) ?? s.failure!.describe(l10n), error: true),
                        ],
                      ),
          ),
          _Composer(
            controller: _input,
            busy: s.busy,
            enabled: ready,
            hint: s.messages.isEmpty ? l10n.aiInputHint : l10n.aiInputFollow,
            onSend: _send,
            onStop: () => ref.read(consultantProvider(widget.conversationId).notifier).stop(),
          ),
        ]),
      ),
    );
  }
}

class _Start extends ConsumerWidget {
  const _Start({required this.onAsk});

  final void Function(String) onAsk;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final qs = quickQuestions(l10n, ref.watch(currentProfileProvider));
    return ListView(padding: const EdgeInsets.fromLTRB(ObSpace.page, 24, ObSpace.page, 16), children: [
      const Center(child: IconTile(icon: LucideIcons.sparkles, tone: Tone.violet, size: 56, radius: 18)),
      const SizedBox(height: 16),
      Text(l10n.aiStartTitle, textAlign: TextAlign.center, style: context.text.headlineSmall),
      const SizedBox(height: 8),
      Text(l10n.aiStartSubtitle, textAlign: TextAlign.center, style: TextStyle(fontSize: 14, height: 1.4, color: c.ink3)),
      const SizedBox(height: 24),
      Text(l10n.aiQuick, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink3)),
      const SizedBox(height: 10),
      for (var i = 0; i < qs.length; i += 2)
        Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: IntrinsicHeight(
            child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              for (final q in qs.skip(i).take(2)) ...[
                Expanded(
                  child: ObCard(
                    onTap: () => onAsk(q.$3),
                    padding: const EdgeInsets.all(14),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Icon(q.$1, size: 20, color: q.$2),
                      const SizedBox(height: 10),
                      Text(q.$3, style: TextStyle(fontSize: 13.5, height: 1.3, fontWeight: FontWeight.w500, color: c.ink)),
                    ]),
                  ),
                ),
                if (q != qs.skip(i).take(2).last) const SizedBox(width: 10),
              ],
              if (qs.skip(i).take(2).length == 1) ...[const SizedBox(width: 10), const Expanded(child: SizedBox.shrink())],
            ]),
          ),
        ),
    ]);
  }
}

class _UserBubble extends StatelessWidget {
  const _UserBubble({required this.text, this.pending = false});

  final String text;
  final bool pending;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Align(
      alignment: Alignment.centerRight,
      child: Container(
        margin: const EdgeInsets.only(left: 48, bottom: 12),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          color: c.accentFill.withValues(alpha: pending ? 0.7 : 1),
          borderRadius: const BorderRadius.only(topLeft: Radius.circular(18), topRight: Radius.circular(18), bottomLeft: Radius.circular(18), bottomRight: Radius.circular(6)),
        ),
        child: SelectableText(text, style: const TextStyle(fontSize: 14.5, height: 1.4, color: Colors.white)),
      ),
    );
  }
}

class _Note extends StatelessWidget {
  const _Note({required this.text, this.error = false});

  final String text;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: error ? c.badSoft : c.warnSoft, borderRadius: BorderRadius.circular(12)),
      child: Text(text, style: TextStyle(fontSize: 13, height: 1.4, color: error ? c.bad : c.warn)),
    );
  }
}

/// Пока ждём ответ: текущий шаг, чипы AI-сотрудников, черновик в пунктирной рамке (числа ещё не проверены).
class _Pending extends StatelessWidget {
  const _Pending({required this.state});

  final ChatState state;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final steps = state.steps.values.toList();
    final running = steps.lastWhere((s) => s.status == 'Running', orElse: () => const ProgressStep(stage: 'routing', status: 'Running'));
    final label = state.phase == ChatPhase.recovering
        ? l10n.aiRecovering
        : switch (running.stage) {
            'memory' => l10n.aiStepMemory,
            'routing' => l10n.aiStepRouting,
            'agent' => l10n.aiStepAgent(running.agentName ?? running.agent ?? 'AI'),
            'compose' => l10n.aiStepCompose,
            _ => l10n.aiStepCheck,
          };
    final agents = steps.where((s) => s.stage == 'agent').toList();
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Row(children: [
        const SizedBox.square(dimension: 14, child: CircularProgressIndicator(strokeWidth: 2)),
        const SizedBox(width: 8),
        Expanded(child: Text('$label…', style: TextStyle(fontSize: 13, color: c.ink2))),
      ]),
      if (agents.isNotEmpty) ...[
        const SizedBox(height: 8),
        Wrap(spacing: 6, runSpacing: 6, children: [
          for (final a in agents)
            SoftBadge(
              label: a.agentName ?? a.agent ?? 'AI',
              tone: a.status == 'Done' ? Tone.ok : a.status == 'Failed' ? Tone.bad : Tone.accent,
              icon: a.status == 'Done' ? LucideIcons.check : a.status == 'Running' ? LucideIcons.loader : null,
            ),
        ]),
      ],
      if (state.draft.isNotEmpty || state.draftMode != DraftMode.writing) ...[
        const SizedBox(height: 10),
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(borderRadius: BorderRadius.circular(14), border: Border.all(color: c.lineStrong)),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(
              switch (state.draftMode) {
                DraftMode.writing => l10n.aiDraftWriting,
                DraftMode.checking => l10n.aiDraftChecking,
                DraftMode.revising => l10n.aiDraftRevising,
              },
              style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w600, color: c.warn),
            ),
            if (state.draft.isNotEmpty) ...[const SizedBox(height: 6), MarkdownView(state.draft, muted: true)],
          ]),
        ),
      ],
      const SizedBox(height: 12),
    ]);
  }
}

/// Ответ консультанта: строка «AI-консультант · N фактов · M рекомендаций», блоки, источники, проверка чисел, действия, уточнения.
class _Answer extends ConsumerWidget {
  const _Answer({required this.message, required this.last, required this.onFollowUp, required this.onRetry});

  final ChatMessage message;
  final bool last;
  final void Function(String) onFollowUp;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    if (message.status == 'failed') {
      return Container(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: c.badSoft, borderRadius: BorderRadius.circular(14)),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(message.error ?? l10n.errorUnknown, style: TextStyle(fontSize: 13, height: 1.4, color: c.bad)),
          const SizedBox(height: 8),
          TextButton.icon(onPressed: onRetry, icon: const Icon(LucideIcons.rotateCcw, size: 16), label: Text(l10n.aiRetry)),
        ]),
      );
    }
    final a = AnswerView.of(message);
    void link(String href) => context.push(appRouteForWeb(href));
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          Icon(LucideIcons.sparkles, size: 14, color: c.violet),
          const SizedBox(width: 6),
          Expanded(child: Text(l10n.aiMeta(a.facts, a.recommendations), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3))),
        ]),
        const SizedBox(height: 8),
        for (final b in a.blocks) Padding(padding: const EdgeInsets.only(bottom: 10), child: _BlockView(block: b, onLink: link)),
        if (a.sources.isNotEmpty || a.checked != null)
          Pressable(
            onTap: () => showDetailSheet(context, title: l10n.aiHowGot, children: (context) => [
              if (a.checked != null) Padding(padding: const EdgeInsets.only(bottom: 12), child: Text(l10n.aiChecked(a.supported ?? 0, a.checked!), style: context.text.bodyMedium)),
              Text(l10n.aiSources, style: context.text.titleMedium),
              const SizedBox(height: 8),
              for (final s in a.sources)
                Pressable(
                  onTap: s.href == null ? null : () {
                    Navigator.of(context).pop();
                    link(s.href!);
                  },
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Row(children: [
                      Icon(LucideIcons.database, size: 16, color: context.ob.ink3),
                      const SizedBox(width: 10),
                      Expanded(child: Text([s.title, ?s.period].join(' · '), style: TextStyle(fontSize: 14, color: s.href == null ? context.ob.ink : context.ob.accentStrong))),
                    ]),
                  ),
                ),
            ]),
            child: Row(children: [
              Icon(LucideIcons.shieldCheck, size: 14, color: c.ok),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  [if (a.checked != null) l10n.aiChecked(a.supported ?? 0, a.checked!), if (a.sources.isNotEmpty) '${l10n.aiSources}: ${a.sources.length}'].join(' · '),
                  style: TextStyle(fontSize: 12, color: c.accentStrong),
                ),
              ),
            ]),
          ),
        const SizedBox(height: 8),
        Row(children: [
          _RoundButton(icon: LucideIcons.copy, label: 'copy', onTap: () {
            Clipboard.setData(ClipboardData(text: message.content));
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.aiCopied)));
          }),
          _RoundButton(icon: LucideIcons.share2, label: 'share', onTap: () => SharePlus.instance.share(ShareParams(text: message.content))),
          _RoundButton(icon: LucideIcons.thumbsUp, label: 'up', onTap: () => _feedback(context, ref, 'up')),
          _RoundButton(icon: LucideIcons.thumbsDown, label: 'down', onTap: () => _feedback(context, ref, 'down')),
          const Spacer(),
          if (message.durationMs > 0) Text(l10n.aiDuration(fmt.decimal(message.durationMs / 1000)), style: TextStyle(fontSize: 11, color: c.ink3)),
        ]),
        if (last && a.followUps.isNotEmpty) ...[
          const SizedBox(height: 10),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(children: [
              for (final f in a.followUps) ...[
                FilterPill(label: '↳ $f', chevron: false, onTap: () => onFollowUp(f)),
                const SizedBox(width: 8),
              ],
            ]),
          ),
        ],
      ]),
    );
  }

  Future<void> _feedback(BuildContext context, WidgetRef ref, String rating) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = context.l10n;
    try {
      await ref.read(consultantApiProvider).feedback(message.id, rating);
      messenger.showSnackBar(SnackBar(content: Text(l10n.aiThanks)));
    } on Object {
      messenger.showSnackBar(SnackBar(content: Text(l10n.errorUnknown)));
    }
  }
}

class _RoundButton extends StatelessWidget {
  const _RoundButton({required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Padding(
      padding: const EdgeInsets.only(right: 6),
      child: Pressable(
        onTap: onTap,
        semanticLabel: label,
        child: Container(
          width: 34,
          height: 34,
          decoration: BoxDecoration(shape: BoxShape.circle, border: Border.all(color: c.line), color: c.surface),
          child: Icon(icon, size: 15, color: c.ink2),
        ),
      ),
    );
  }
}

Tone _tone(String t) => switch (t) {
      'good' => Tone.ok,
      'bad' => Tone.bad,
      'warn' => Tone.warn,
      _ => Tone.neutral,
    };

/// Блок структурированного ответа.
class _BlockView extends StatelessWidget {
  const _BlockView({required this.block, required this.onLink});

  final AnswerBlock block;
  final void Function(String) onLink;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    Widget title(String t) => Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Text(t.toUpperCase(), style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, letterSpacing: 0.8, color: c.ink3)),
        );
    return switch (block) {
      SummaryBlock(:final text, :final progress) => ObCard(
          tint: c.kpi,
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            title(l10n.aiSummary),
            if (progress != null) ...[
              () {
                final fact = (progress['fact'] as num?)?.toDouble() ?? 0;
                final plan = (progress['plan'] as num?)?.toDouble() ?? 0;
                final unit = progress['unit'] as String? ?? '';
                return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(progress['label'] as String? ?? '', style: TextStyle(fontSize: 12, color: c.ink3)),
                  const SizedBox(height: 4),
                  Text(plan > 0 ? '${fmt.decimal(100 * fact / plan)}%' : fmt.decimal(fact), style: context.text.displaySmall),
                  Text(l10n.aiPlan('${fmt.decimal(fact)} $unit', '${fmt.decimal(plan)} $unit'), style: TextStyle(fontSize: 12, color: c.ink3)),
                  const SizedBox(height: 8),
                  ProgressLine(value: plan > 0 ? fact / plan : 0, color: c.accent),
                  const SizedBox(height: 10),
                ]);
              }(),
            ],
            MarkdownView(text, onLink: onLink),
          ]),
        ),
      KpisBlock(:final items) => Column(children: [
          for (var i = 0; i < items.length; i += 2)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: IntrinsicHeight(
                child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  for (final k in items.skip(i).take(2)) ...[
                    Expanded(
                      child: ObCard(
                        padding: const EdgeInsets.all(12),
                        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Text(k.label, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11.5, color: c.ink3)),
                          const SizedBox(height: 4),
                          FittedBox(fit: BoxFit.scaleDown, alignment: Alignment.centerLeft, child: Text(k.value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: c.ink))),
                          if (k.delta != null) ...[const SizedBox(height: 6), SoftBadge(label: k.delta!, tone: _tone(k.tone))],
                        ]),
                      ),
                    ),
                    if (k != items.skip(i).take(2).last) const SizedBox(width: 8),
                  ],
                  if (items.skip(i).take(2).length == 1) ...[const SizedBox(width: 8), const Expanded(child: SizedBox.shrink())],
                ]),
              ),
            ),
        ]),
      FactsBlock(:final title, :final rows) => ObCard(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            _title(context, title ?? l10n.aiFacts),
            for (final r in rows)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 6),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Expanded(child: Text(r.label, style: TextStyle(fontSize: 13.5, color: c.ink2))),
                  Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
                    if (r.value != null) Text(r.value!, style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: c.ink)),
                    for (final d in r.deltas)
                      Padding(padding: const EdgeInsets.only(top: 3), child: SoftBadge(label: [d.value, ?d.caption].join(' '), tone: _tone(d.tone))),
                  ]),
                ]),
              ),
          ]),
        ),
      DeviationsBlock(:final items) => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          title(l10n.aiDeviations),
          for (final d in items)
            // Рамка со скруглением рисуется только одним цветом — цветная полоса слева отдельным элементом.
            Container(
              margin: const EdgeInsets.only(bottom: 8),
              clipBehavior: Clip.antiAlias,
              decoration: BoxDecoration(color: c.surface, borderRadius: BorderRadius.circular(12), border: Border.all(color: c.line)),
              child: IntrinsicHeight(
                child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  ColoredBox(color: d.high ? c.bad : c.warn, child: const SizedBox(width: 3)),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(d.title, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                        const SizedBox(height: 4),
                        MarkdownView(d.text, onLink: onLink),
                      ]),
                    ),
                  ),
                ]),
              ),
            ),
        ]),
      RecommendationsBlock(:final items) => ObCard(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            _title(context, l10n.aiRecommendations),
            for (var i = 0; i < items.length; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 6),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Container(
                    width: 22,
                    height: 22,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(color: c.accentSoft, shape: BoxShape.circle),
                    child: Text('${i + 1}', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: c.accentStrong)),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(items[i].title, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                      if (items[i].text != null) Text(items[i].text!, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink2)),
                      if (items[i].owner != null || items[i].dueDate != null)
                        Text([if (items[i].owner != null) l10n.aiOwner(items[i].owner!), if (items[i].dueDate != null) l10n.aiDue(items[i].dueDate!)].join(' · '),
                            style: TextStyle(fontSize: 11.5, color: c.ink3)),
                    ]),
                  ),
                ]),
              ),
          ]),
        ),
      MarkdownBlock(:final text) => ObCard(child: MarkdownView(text, onLink: onLink)),
    };
  }

  Widget _title(BuildContext context, String t) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Text(t.toUpperCase(), style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, letterSpacing: 0.8, color: context.ob.ink3)),
      );
}

/// Поле вопроса над нижней панелью: отправка или «Остановить ответ».
class _Composer extends StatelessWidget {
  const _Composer({required this.controller, required this.busy, required this.enabled, required this.hint, required this.onSend, required this.onStop});

  final TextEditingController controller;
  final bool busy;
  final bool enabled;
  final String hint;
  final VoidCallback onSend;
  final VoidCallback onStop;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    // Нижний отступ экрана над панелью уже включает её высоту (extendBody); с клавиатурой панель скрыта и отступ ≈ 0.
    // +12 — чтобы поле не задевало выступ синей кнопки меню.
    return Padding(
      padding: EdgeInsets.fromLTRB(12, 6, 12, MediaQuery.paddingOf(context).bottom + 12),
      child: Container(
        padding: const EdgeInsets.fromLTRB(14, 4, 6, 4),
        decoration: BoxDecoration(color: c.surface, borderRadius: BorderRadius.circular(24), border: Border.all(color: c.lineStrong)),
        child: Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Expanded(
            child: TextField(
              controller: controller,
              enabled: enabled && !busy,
              minLines: 1,
              maxLines: 5,
              maxLength: 4000,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => onSend(),
              decoration: InputDecoration(hintText: hint, border: InputBorder.none, enabledBorder: InputBorder.none, focusedBorder: InputBorder.none, filled: false, counterText: '', isDense: true),
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Pressable(
              onTap: busy ? onStop : (enabled ? onSend : null),
              semanticLabel: busy ? l10n.aiStop : l10n.aiSend,
              child: Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(shape: BoxShape.circle, color: busy ? c.badSoft : c.accentFill),
                child: Icon(busy ? LucideIcons.square : LucideIcons.arrowUp, size: 18, color: busy ? c.bad : Colors.white),
              ),
            ),
          ),
        ]),
      ),
    );
  }
}

class _HistorySheet extends ConsumerStatefulWidget {
  const _HistorySheet();

  @override
  ConsumerState<_HistorySheet> createState() => _HistorySheetState();
}

class _HistorySheetState extends ConsumerState<_HistorySheet> {
  List<ConversationSummary>? _items;
  String _q = '';
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final items = await ref.read(consultantApiProvider).conversations(_q, CancelToken());
      if (mounted) setState(() => _items = items);
    } on Object {
      if (mounted) setState(() => _items = const []);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final now = DateTime.now();
    return Padding(
      padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, 16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text(l10n.aiHistory, style: context.text.titleLarge),
        const SizedBox(height: 12),
        TextField(
          decoration: InputDecoration(hintText: l10n.aiSearchChats, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
          onChanged: (v) {
            _q = v;
            _debounce?.cancel();
            _debounce = Timer(const Duration(milliseconds: 350), _load);
          },
        ),
        const SizedBox(height: 12),
        Expanded(
          child: _items == null
              ? const LoadingView()
              : _items!.isEmpty
                  ? EmptyView(icon: LucideIcons.messagesSquare, title: l10n.aiNoHistory, compact: true)
                  : ListView.separated(
                      itemCount: _items!.length,
                      separatorBuilder: (context, i) => Divider(height: 1, color: c.line),
                      itemBuilder: (context, i) {
                        final it = _items![i];
                        return Pressable(
                          onTap: () => Navigator.pop(context, it.id),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            child: Row(children: [
                              Icon(it.source == 'telegram' ? LucideIcons.send : LucideIcons.messageSquare, size: 16, color: c.ink3),
                              const SizedBox(width: 10),
                              Expanded(child: Text(it.title.replaceFirst('Telegram · ', ''), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink))),
                              Text(context.fmt.ago(DateTime.parse(it.lastMessageAt), now), style: TextStyle(fontSize: 11, color: c.ink3)),
                            ]),
                          ),
                        );
                      },
                    ),
        ),
      ]),
    );
  }
}
