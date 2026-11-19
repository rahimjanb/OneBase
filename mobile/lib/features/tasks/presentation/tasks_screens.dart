import 'dart:io';

import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/cache/memory_bytes_cache.dart';
import '../../../core/cache/paged.dart';
import '../../../core/core_providers.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/utils/debouncer.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/rings.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../dashboard/domain/home_view.dart';
import '../data/task_models.dart';
import '../data/tasks_api.dart';

// ───────────── Провайдеры ─────────────

class TasksController extends PagedController<TaskRow> {
  TasksController(this.query);

  final TaskQuery query;

  @override
  Future<PageChunk<TaskRow>> fetchPage(int page, String? cursor, CancelToken cancel) => ref.read(tasksApiProvider).list(query, page, cancel);
}

final tasksProvider = NotifierProvider.autoDispose.family<TasksController, PagedState<TaskRow>, TaskQuery>(TasksController.new);

class TaskBlock extends CachedBlock<TaskDetail> {
  TaskBlock(this.id);

  final String id;

  @override
  String get cacheKey => 'tasks/detail/$id';

  /// Комментарии и вложения — рабочая переписка: на телефоне не сохраняются.
  @override
  bool get persist => false;

  @override
  Future<TaskDetail> fetch(CancelToken cancel) => ref.read(tasksApiProvider).detail(id, cancel);

  @override
  TaskDetail decode(Object? json) => TaskDetail.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(TaskDetail data) => data.toJson();
}

final taskBlock = NotifierProvider.autoDispose.family<TaskBlock, BlockState<TaskDetail>, String>(TaskBlock.new);

class WorkDepartmentsBlock extends CachedBlock<List<WorkDepartmentRef>> {
  @override
  String get cacheKey => 'tasks/departments';

  @override
  Future<List<WorkDepartmentRef>> fetch(CancelToken cancel) => ref.read(tasksApiProvider).departments(cancel);

  @override
  List<WorkDepartmentRef> decode(Object? json) => [for (final e in json! as List<Object?>) WorkDepartmentRef.fromJson(e! as Map<String, Object?>)];

  @override
  Object? encode(List<WorkDepartmentRef> data) => [for (final d in data) d.toJson()];
}

final workDepartmentsBlock = NotifierProvider.autoDispose<WorkDepartmentsBlock, BlockState<List<WorkDepartmentRef>>>(WorkDepartmentsBlock.new);

// ───────────── Подписи ─────────────

String taskStatusLabel(AppLocalizations l10n, String s) => switch (s) {
      'New' => l10n.tkSt_New,
      'InProgress' => l10n.tkSt_InProgress,
      'Review' => l10n.tkSt_Review,
      'Done' => l10n.tkSt_Done,
      'Cancelled' => l10n.tkSt_Cancelled,
      _ => s,
    };

Tone taskStatusTone(String s) => switch (s) {
      'New' => Tone.accent,
      'InProgress' => Tone.warn,
      'Review' => Tone.violet,
      'Done' => Tone.ok,
      _ => Tone.neutral,
    };

String priorityName(AppLocalizations l10n, String p) => switch (p) {
      'Low' => l10n.tkPr_Low,
      'High' => l10n.tkPr_High,
      'Urgent' => l10n.tkPr_Urgent,
      _ => l10n.tkPr_Medium,
    };

IconData priorityIcon(String p) => switch (p) {
      'Low' => LucideIcons.arrowDown,
      'High' => LucideIcons.arrowUp,
      'Urgent' => LucideIcons.flame,
      _ => LucideIcons.minus,
    };

/// Кнопка перехода статуса — как у веба (`statusActionLabel`).
String actionLabel(AppLocalizations l10n, String from, String to) => switch ((from, to)) {
      ('Review', 'InProgress') => l10n.tkAct_Return,
      ('Review', 'Done') => l10n.tkAct_Accept,
      ('Done' || 'Cancelled', 'InProgress') => l10n.tkAct_Reopen,
      (_, 'Review') => l10n.tkAct_Review,
      (_, 'Done') => l10n.tkAct_Done,
      (_, 'InProgress') => l10n.tkAct_InProgress,
      (_, 'Cancelled') => l10n.tkAct_Cancelled,
      _ => to,
    };

/// Срок: просрочен — красный; сегодня и завтра — жёлтый; позже — обычный; нет — «без срока».
(String, Color) dueText(BuildContext context, TaskRow t, DateTime now) {
  final l10n = context.l10n;
  final c = context.ob;
  if (t.dueDate == null) return (l10n.tkNoDue, c.ink3);
  final today = isoDate(tashkentNow(now));
  final tomorrow = addDays(today, 1);
  final time = t.dueTime == null ? '' : ' ${t.dueTime!.substring(0, 5)}';
  if (t.overdue) return (l10n.tkDue('${t.dueDate!.substring(8, 10)}.${t.dueDate!.substring(5, 7)}$time'), c.bad);
  if (t.dueDate == today) return (l10n.tkDue('${l10n.tkToday}$time'), c.warn);
  if (t.dueDate == tomorrow) return (l10n.tkDue('${l10n.tkTomorrow}$time'), c.warn);
  return (l10n.tkDue('${t.dueDate!.substring(8, 10)}.${t.dueDate!.substring(5, 7)}$time'), c.ink3);
}

// ───────────── Список ─────────────

/// «Задачи»: мои / все / отдела / архив, статусы (открытые, просроченные, заблокированные, закрытые), поиск по паузе ввода; подгрузка.
class TasksScreen extends ConsumerStatefulWidget {
  const TasksScreen({super.key, this.initialStatus, this.initialView, this.initialDepartment});

  final String? initialStatus;
  final String? initialView;

  /// Задачи отдела (код отдела) — вход из «Отделов», когда своего раздела у отдела нет.
  final String? initialDepartment;

  @override
  ConsumerState<TasksScreen> createState() => _TasksScreenState();
}

class _TasksScreenState extends ConsumerState<TasksScreen> {
  late TaskQuery _q = TaskQuery(
    scope: switch (widget.initialView) {
      _ when widget.initialDepartment != null => 'department',
      'all' => 'all',
      'archive' => 'archive',
      _ => widget.initialStatus != null ? 'all' : 'my',
    },
    department: widget.initialDepartment,
    status: switch (widget.initialStatus) {
      null => 'open',
      'overdue' => 'overdue',
      final s => s,
    },
  );
  final _debounce = Debouncer();
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter < 500) ref.read(tasksProvider(_q).notifier).loadMore();
    });
  }

  @override
  void dispose() {
    _debounce.cancel();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _pickDepartment() async {
    final l10n = context.l10n;
    final depts = ref.read(workDepartmentsBlock).data?.where((d) => d.hasTasks).toList() ?? const <WorkDepartmentRef>[];
    final code = await showObSheet<String>(context, title: l10n.tkDepartment, builder: (context) => Column(children: [
      for (final d in depts)
        Pressable(
          onTap: () => Navigator.pop(context, d.code),
          child: Container(height: 46, alignment: Alignment.centerLeft, child: Text(d.name, style: TextStyle(fontSize: 15, color: context.ob.ink))),
        ),
    ]));
    if (code != null) setState(() => _q = _q.copyWith(scope: 'department', department: () => code));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final s = ref.watch(tasksProvider(_q));
    ref.watch(workDepartmentsBlock);
    final depts = ref.watch(workDepartmentsBlock).data ?? const <WorkDepartmentRef>[];
    final deptName = depts.where((d) => d.code == _q.department).firstOrNull?.name;
    final scopes = {'my': l10n.tkScopeMy, 'all': l10n.tkScopeAll, 'department': deptName ?? l10n.tkScopeDepartment, 'archive': l10n.tkScopeArchive};
    final statuses = {'open': l10n.tkStatusOpen, 'overdue': l10n.tkStatusOverdue, 'blocked': l10n.tkStatusBlocked, 'closed': l10n.tkStatusClosed};
    final c = context.ob;
    return ObPage(
      title: l10n.navTasks,
      controller: _scroll,
      showBack: false,
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: c.accentFill,
        foregroundColor: Colors.white,
        onPressed: () => context.push('/tasks/new'),
        icon: const Icon(LucideIcons.plus),
        label: Text(l10n.tkNew),
      ),
      onRefresh: () => ref.read(tasksProvider(_q).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              clipBehavior: Clip.none,
              child: Row(children: [
                for (final e in scopes.entries) ...[
                  FilterPill(
                    label: e.value,
                    chevron: e.key == 'department',
                    active: e.key == _q.scope,
                    onTap: () => e.key == 'department' ? _pickDepartment() : setState(() => _q = _q.copyWith(scope: e.key)),
                  ),
                  const SizedBox(width: 8),
                ],
              ]),
            ),
            const SizedBox(height: 10),
            if (_q.scope != 'archive') ...[
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(children: [
                  for (final e in statuses.entries) ...[
                    FilterPill(label: e.value, chevron: false, active: e.key == _q.status, onTap: () => setState(() => _q = _q.copyWith(status: () => e.key))),
                    const SizedBox(width: 8),
                  ],
                ]),
              ),
              const SizedBox(height: 10),
            ],
            TextField(
              decoration: InputDecoration(hintText: l10n.tkSearch, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
              onChanged: (v) => _debounce(() => setState(() => _q = _q.copyWith(search: v))),
            ),
            const SizedBox(height: 12),
          ]),
        ),
        if (s.isInitialLoading)
          const SliverToBoxAdapter(child: LoadingView())
        else if (s.items.isEmpty && s.failure != null)
          SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(tasksProvider(_q).notifier).refresh()))
        else if (s.items.isEmpty)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.clipboardCheck, title: l10n.tasksNone, compact: true))
        else
          SliverList.separated(
            itemCount: s.items.length,
            separatorBuilder: (context, i) => const SizedBox(height: 10),
            itemBuilder: (context, i) => TaskCard(task: s.items[i]),
          ),
      ],
    );
  }
}

class TaskCard extends ConsumerWidget {
  const TaskCard({super.key, required this.task});

  final TaskRow task;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final (due, dueColor) = dueText(context, task, ref.watch(clockProvider)());
    return ObCard(
      onTap: () => context.push('/tasks/${task.id}'),
      padding: const EdgeInsets.all(14),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          SoftBadge(label: task.isBlocked ? l10n.tkBlocked : taskStatusLabel(l10n, task.status), tone: task.isBlocked ? Tone.bad : taskStatusTone(task.status)),
          const SizedBox(width: 8),
          Icon(priorityIcon(task.priority), size: 14, color: task.priority == 'Urgent' ? c.bad : task.priority == 'High' ? c.warn : c.ink3),
          const Spacer(),
          Text(due, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: dueColor)),
        ]),
        const SizedBox(height: 8),
        Text(task.title, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, height: 1.3, color: c.ink)),
        const SizedBox(height: 6),
        Text([task.assigneeName ?? l10n.tkAnyone, task.departmentName, ?task.projectName].join(' · '), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
        if (task.checklistTotal > 0 || task.progress > 0) ...[
          const SizedBox(height: 10),
          Row(children: [
            Expanded(child: ProgressLine(value: task.progress / 100, color: c.accent, height: 4)),
            const SizedBox(width: 10),
            Text(task.checklistTotal > 0 ? '${task.checklistDone}/${task.checklistTotal}' : '${task.progress}%', style: TextStyle(fontSize: 11, color: c.ink3)),
          ]),
        ],
      ]),
    );
  }
}

// ───────────── Карточка ─────────────

/// Карточка задачи: статус, приоритет, срок, исполнитель, описание, чек-лист, действия по разрешённым переходам сервера (с причиной при
/// возврате), блокировка, комментарии, вложения (картинки — превью в памяти, остальное — «Поделиться»), история.
class TaskScreen extends ConsumerWidget {
  const TaskScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(taskBlock(id));
    final d = s.data;
    return ObPage(
      title: d == null ? l10n.navTasks : taskStatusLabel(l10n, d.task.status),
      onRefresh: () => ref.read(taskBlock(id).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, onRetry: () => ref.read(taskBlock(id).notifier).refresh(), (d) {
            final t = d.task;
            final (due, dueColor) = dueText(context, t, ref.read(clockProvider)());
            Widget line(IconData icon, String label, String value, {Color? color}) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 5),
                  child: Row(children: [
                    Icon(icon, size: 15, color: c.ink3),
                    const SizedBox(width: 10),
                    SizedBox(width: 110, child: Text(label, style: TextStyle(fontSize: 13, color: c.ink3))),
                    Expanded(child: Text(value, style: TextStyle(fontSize: 13, color: color ?? c.ink))),
                  ]),
                );
            return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text(t.title, style: context.text.headlineSmall),
              const SizedBox(height: 10),
              Wrap(spacing: 6, runSpacing: 6, children: [
                SoftBadge(label: taskStatusLabel(l10n, t.status), tone: taskStatusTone(t.status), size: 12),
                if (t.isBlocked) SoftBadge(label: l10n.tkBlocked, tone: Tone.bad, size: 12),
                SoftBadge(label: priorityName(l10n, t.priority), tone: t.priority == 'Urgent' ? Tone.bad : t.priority == 'High' ? Tone.warn : Tone.neutral, icon: priorityIcon(t.priority), size: 12),
              ]),
              if (t.isBlocked && t.blockedReason != null) ...[
                const SizedBox(height: 8),
                Text(t.blockedReason!, style: TextStyle(fontSize: 13, color: c.bad)),
              ],
              const SizedBox(height: 12),
              ObCard(child: Column(children: [
                line(LucideIcons.calendarClock, l10n.tkDueDate, due, color: dueColor),
                line(LucideIcons.userRound, l10n.tkAssignee, t.assigneeName ?? l10n.tkAnyone),
                if (t.createdByName != null) line(LucideIcons.userPen, l10n.tkAuthor, t.createdByName!),
                line(LucideIcons.building2, l10n.tkDepartment, t.departmentName),
                if (t.projectName != null) line(LucideIcons.folderKanban, l10n.tkProject, t.projectName!),
              ])),
              const SizedBox(height: 12),
              _TaskActions(detail: d),
              if ((t.description ?? '').isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.tkDescription),
                const SizedBox(height: 10),
                ObCard(child: SelectableText(t.description!, style: TextStyle(fontSize: 14, height: 1.45, color: c.ink))),
              ],
              if (d.checklist.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.tkChecklist, trailing: Text(l10n.tkProgress(t.progress), style: TextStyle(fontSize: 12, color: c.ink3))),
                const SizedBox(height: 10),
                ObListCard(children: [
                  for (final it in d.checklist)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                      child: Row(children: [
                        Icon(it.isDone ? LucideIcons.squareCheck : LucideIcons.square, size: 18, color: it.isDone ? c.ok : c.ink3),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(it.title,
                              style: TextStyle(fontSize: 14, color: it.isDone ? c.ink3 : c.ink, decoration: it.isDone ? TextDecoration.lineThrough : null)),
                        ),
                      ]),
                    ),
                ]),
              ],
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.tkAttachments, trailing: Text('${d.attachments.length}', style: TextStyle(fontSize: 12, color: c.ink3))),
              const SizedBox(height: 10),
              _Attachments(detail: d),
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.tkComments, trailing: Text('${d.comments.length}', style: TextStyle(fontSize: 12, color: c.ink3))),
              const SizedBox(height: 10),
              for (final cm in d.comments) ...[
                ObCard(
                  padding: const EdgeInsets.all(12),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text('${cm.authorName} · ${fmt.ago(DateTime.parse(cm.createdAt), ref.read(clockProvider)())}', style: TextStyle(fontSize: 11, color: c.ink3)),
                    const SizedBox(height: 4),
                    Text(cm.text, style: TextStyle(fontSize: 14, height: 1.4, color: c.ink)),
                  ]),
                ),
                const SizedBox(height: 8),
              ],
              if (d.canComment) _CommentBox(taskId: t.id),
              if (d.history.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.tkHistory),
                const SizedBox(height: 10),
                ObListCard(children: [
                  for (final h in d.history.take(30))
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(h.text, style: TextStyle(fontSize: 13, color: c.ink)),
                        Text('${h.actorName} · ${fmt.ago(DateTime.parse(h.createdAt), ref.read(clockProvider)())}', style: TextStyle(fontSize: 11, color: c.ink3)),
                      ]),
                    ),
                ]),
              ],
            ]);
          }),
        ),
      ],
    );
  }
}

class _TaskActions extends ConsumerStatefulWidget {
  const _TaskActions({required this.detail});

  final TaskDetail detail;

  @override
  ConsumerState<_TaskActions> createState() => _TaskActionsState();
}

class _TaskActionsState extends ConsumerState<_TaskActions> {
  bool _busy = false;

  Future<void> _run(Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = context.l10n;
    setState(() => _busy = true);
    try {
      await action();
    } on Object catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(AppFailure.from(e).describe(l10n))));
    } finally {
      await ref.read(taskBlock(widget.detail.task.id).notifier).refresh();
      ref.invalidate(tasksProvider);
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<String?> _ask(String title, String label, {required bool required}) async {
    final ctrl = TextEditingController();
    final l10n = context.l10n;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: TextField(controller: ctrl, autofocus: true, maxLines: 3, decoration: InputDecoration(labelText: label)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(title)),
        ],
      ),
    );
    if (ok != true) return null;
    if (required && ctrl.text.trim().isEmpty) return null;
    return ctrl.text.trim();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final t = widget.detail.task;
    final api = ref.read(tasksApiProvider);
    final open = t.status == 'New' || t.status == 'InProgress' || t.status == 'Review';
    final buttons = <Widget>[
      for (final next in t.nextStatuses)
        () {
          final label = actionLabel(l10n, t.status, next);
          final returning = t.status == 'Review' && next == 'InProgress';
          final primary = next == 'Done' || next == 'Review' || (next == 'InProgress' && !returning);
          void onTap() async {
            if (returning) {
              final comment = await _ask(label, l10n.tkCommentRequired, required: true);
              if (comment != null) await _run(() => api.setStatus(t.id, next, comment: comment));
            } else if (next == 'Cancelled') {
              final comment = await _ask(label, l10n.prReason, required: false);
              if (comment != null) await _run(() => api.setStatus(t.id, next, comment: comment.isEmpty ? null : comment));
            } else if (next == 'Done') {
              final result = await _ask(label, l10n.tkResult, required: false);
              if (result != null) await _run(() => api.setStatus(t.id, next, result: result.isEmpty ? null : result));
            } else {
              await _run(() => api.setStatus(t.id, next));
            }
          }

          return primary
              ? FilledButton(onPressed: _busy ? null : onTap, child: Text(label))
              : OutlinedButton(onPressed: _busy ? null : onTap, child: Text(label, style: TextStyle(color: next == 'Cancelled' ? context.ob.bad : null)));
        }(),
      if (open && t.canWork)
        TextButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  if (t.isBlocked) {
                    await _run(() => api.block(t.id, blocked: false));
                  } else {
                    final reason = await _ask(l10n.tkBlock, l10n.tkBlockReason, required: true);
                    if (reason != null) await _run(() => api.block(t.id, blocked: true, reason: reason));
                  }
                },
          icon: Icon(t.isBlocked ? LucideIcons.lockOpen : LucideIcons.lock, size: 16),
          label: Text(t.isBlocked ? l10n.tkUnblock : l10n.tkBlock),
        ),
    ];
    if (buttons.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [for (final b in buttons) ...[b, const SizedBox(height: 8)]]);
  }
}

class _CommentBox extends ConsumerStatefulWidget {
  const _CommentBox({required this.taskId});

  final String taskId;

  @override
  ConsumerState<_CommentBox> createState() => _CommentBoxState();
}

class _CommentBoxState extends ConsumerState<_CommentBox> {
  final _text = TextEditingController();
  bool _sending = false;

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _text.text.trim();
    if (text.isEmpty) return;
    final messenger = ScaffoldMessenger.of(context);
    final l10n = context.l10n;
    setState(() => _sending = true);
    try {
      // Комментарий не идемпотентен: без очереди и без автоматического повтора; при ошибке текст остаётся в поле.
      await ref.read(tasksApiProvider).comment(widget.taskId, text);
      _text.clear();
      await ref.read(taskBlock(widget.taskId).notifier).refresh();
    } on Object catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(AppFailure.from(e).describe(l10n))));
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
      Expanded(
        child: TextField(controller: _text, minLines: 1, maxLines: 5, maxLength: 4000, decoration: InputDecoration(hintText: l10n.tkCommentHint, counterText: '')),
      ),
      const SizedBox(width: 8),
      IconButton.filled(
        onPressed: _sending ? null : _send,
        tooltip: l10n.tkSend,
        icon: _sending ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(LucideIcons.send, size: 18),
      ),
    ]);
  }
}

class _Attachments extends ConsumerStatefulWidget {
  const _Attachments({required this.detail});

  final TaskDetail detail;

  @override
  ConsumerState<_Attachments> createState() => _AttachmentsState();
}

class _AttachmentsState extends ConsumerState<_Attachments> {
  bool _uploading = false;

  Future<void> _upload() async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final files = await FilePicker.pickFiles();
    if (files.isEmpty) return;
    final f = files.first;
    final size = await f.length() ?? 0;
    if (size > 20 * 1024 * 1024) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.tkTooBig)));
      return;
    }
    setState(() => _uploading = true);
    try {
      await ref.read(tasksApiProvider).attach(widget.detail.task.id, f.name, await f.readAsBytes());
      messenger.showSnackBar(SnackBar(content: Text(l10n.tkAttachmentAdded)));
      await ref.read(taskBlock(widget.detail.task.id).notifier).refresh();
    } on Object catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(AppFailure.from(e).describe(l10n))));
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  Future<void> _open(TaskAttachment a) async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final cache = ref.read(memoryBytesCacheProvider);
    try {
      final bytes = cache.get(a.id) ?? await ref.read(tasksApiProvider).download(a.id);
      cache.put(a.id, bytes);
      if (!mounted) return;
      if (a.isImage) {
        await showDialog<void>(context: context, builder: (context) => Dialog(child: InteractiveViewer(child: Image.memory(bytes))));
        return;
      }
      // Остальные файлы — во временный файл для «Поделиться/Открыть», затем удаляются.
      final dir = await getTemporaryDirectory();
      final file = File('${dir.path}/${a.fileName}');
      await file.writeAsBytes(bytes, flush: true);
      await SharePlus.instance.share(ShareParams(files: [XFile(file.path, mimeType: a.contentType)]));
      if (await file.exists()) await file.delete();
    } on Object catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(AppFailure.from(e).describe(l10n))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final d = widget.detail;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      if (d.attachments.isNotEmpty)
        ObListCard(children: [
          for (final a in d.attachments)
            Pressable(
              onTap: () => _open(a),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                child: Row(children: [
                  Icon(a.isImage ? LucideIcons.image : LucideIcons.fileText, size: 18, color: c.ink2),
                  const SizedBox(width: 10),
                  Expanded(child: Text(a.fileName, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink))),
                  Text('${(a.sizeBytes / 1024).ceil()} KB', style: TextStyle(fontSize: 11, color: c.ink3)),
                ]),
              ),
            ),
        ]),
      if (d.canAttach) ...[
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: _uploading ? null : _upload,
          icon: _uploading ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(LucideIcons.paperclip, size: 16),
          label: Text(l10n.tkAttach),
        ),
      ],
    ]);
  }
}

// ───────────── Новая задача ─────────────

/// Новая задача: отдел (где можно ставить задачи), название, описание, исполнитель отдела, приоритет, срок и время. Создание не
/// идемпотентно — кнопка блокируется на время запроса, автоматического повтора нет.
class NewTaskScreen extends ConsumerStatefulWidget {
  const NewTaskScreen({super.key});

  @override
  ConsumerState<NewTaskScreen> createState() => _NewTaskScreenState();
}

class _NewTaskScreenState extends ConsumerState<NewTaskScreen> {
  final _title = TextEditingController();
  final _description = TextEditingController();
  String? _department;
  Assignee? _assignee;
  List<Assignee> _assignees = const [];
  String _priority = 'Medium';
  DateTime? _due;
  TimeOfDay? _time;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
  }

  Future<void> _loadAssignees(String code) async {
    setState(() {
      _assignees = const [];
      _assignee = null;
    });
    try {
      final list = await ref.read(tasksApiProvider).assignees(code, CancelToken());
      if (mounted && _department == code) setState(() => _assignees = list);
    } on Object {
      // Без списка — «любой сотрудник отдела».
    }
  }

  Future<void> _save() async {
    final l10n = context.l10n;
    setState(() => _error = null);
    if (_department == null) return setState(() => _error = l10n.tkDeptRequired);
    if (_title.text.trim().isEmpty) return setState(() => _error = l10n.tkTitleRequired);
    setState(() => _saving = true);
    try {
      final id = await ref.read(tasksApiProvider).create({
        'department': _department,
        'title': _title.text.trim(),
        'description': _description.text.trim().isEmpty ? null : _description.text.trim(),
        'assigneeId': _assignee?.id,
        'priority': _priority,
        'dueDate': _due == null ? null : isoDate(_due!),
        'dueTime': _due == null || _time == null ? null : '${_time!.hour.toString().padLeft(2, '0')}:${_time!.minute.toString().padLeft(2, '0')}:00',
      });
      ref.invalidate(tasksProvider);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.tkCreated)));
      context.pushReplacement('/tasks/$id');
    } on Object catch (e) {
      setState(() {
        _saving = false;
        _error = AppFailure.from(e).describe(l10n);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final depts = ref.watch(workDepartmentsBlock).data?.where((d) => d.hasTasks).toList() ?? const <WorkDepartmentRef>[];
    if (_department == null && depts.isNotEmpty) {
      final mine = depts.where((d) => d.isMine).firstOrNull ?? depts.first;
      _department = mine.code;
      Future.microtask(() => _loadAssignees(mine.code));
    }
    return ObPage(
      title: l10n.tkNew,
      slivers: [
        SliverToBoxAdapter(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            DropdownButtonFormField<String>(
              initialValue: _department,
              isExpanded: true,
              decoration: InputDecoration(labelText: l10n.tkDepartment),
              items: [for (final d in depts) DropdownMenuItem(value: d.code, child: Text(d.name))],
              onChanged: (v) {
                setState(() => _department = v);
                if (v != null) _loadAssignees(v);
              },
            ),
            const SizedBox(height: 12),
            TextField(controller: _title, maxLength: 120, decoration: InputDecoration(labelText: l10n.tkTitle, counterText: '')),
            const SizedBox(height: 12),
            TextField(controller: _description, minLines: 3, maxLines: 8, maxLength: 4000, decoration: InputDecoration(labelText: l10n.tkDescription, counterText: '')),
            const SizedBox(height: 12),
            DropdownButtonFormField<String?>(
              initialValue: _assignee?.id,
              isExpanded: true,
              decoration: InputDecoration(labelText: l10n.tkAssignee),
              items: [
                DropdownMenuItem<String?>(child: Text(l10n.tkAnyone)),
                for (final a in _assignees) DropdownMenuItem<String?>(value: a.id, child: Text(a.position == null ? a.name : '${a.name} · ${a.position}', overflow: TextOverflow.ellipsis)),
              ],
              onChanged: (v) => setState(() => _assignee = _assignees.where((a) => a.id == v).firstOrNull),
            ),
            const SizedBox(height: 14),
            Text(l10n.tkPriority, style: TextStyle(fontSize: 12, color: c.ink3)),
            const SizedBox(height: 6),
            Wrap(spacing: 8, runSpacing: 8, children: [
              for (final p in const ['Low', 'Medium', 'High', 'Urgent'])
                FilterPill(label: priorityName(l10n, p), icon: priorityIcon(p), chevron: false, active: p == _priority, onTap: () => setState(() => _priority = p)),
            ]),
            const SizedBox(height: 14),
            Row(children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () async {
                    final now = tashkentNow();
                    final picked = await showDatePicker(context: context, firstDate: DateTime(now.year, now.month, now.day), lastDate: DateTime(now.year + 2), initialDate: _due ?? DateTime(now.year, now.month, now.day));
                    if (picked != null) setState(() => _due = DateTime.utc(picked.year, picked.month, picked.day));
                  },
                  icon: const Icon(LucideIcons.calendar, size: 16),
                  label: Text(_due == null ? l10n.tkDueDate : '${_due!.day.toString().padLeft(2, '0')}.${_due!.month.toString().padLeft(2, '0')}.${_due!.year}'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _due == null
                      ? null
                      : () async {
                          final picked = await showTimePicker(context: context, initialTime: _time ?? const TimeOfDay(hour: 18, minute: 0));
                          if (picked != null) setState(() => _time = picked);
                        },
                  icon: const Icon(LucideIcons.clock, size: 16),
                  label: Text(_time == null ? l10n.tkDueTime : _time!.format(context)),
                ),
              ),
            ]),
            if (_error != null) ...[const SizedBox(height: 10), Text(_error!, style: TextStyle(fontSize: 13, color: c.bad))],
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _saving ? null : _save,
              child: _saving ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : Text(l10n.tkCreate),
            ),
          ]),
        ),
      ],
    );
  }
}
