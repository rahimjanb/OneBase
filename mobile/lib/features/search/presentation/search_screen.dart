import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/router.dart';
import '../../../app/shell/app_tab.dart';
import '../../../app/theme/decor.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/permissions/access.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/pressable.dart';
import '../../departments/domain/departments_view.dart';
import '../../departments/presentation/departments_screen.dart';
import '../../hr/data/hr_models.dart';

typedef Hit = ({String key, String route, String title, String? hint, IconData icon});

/// Разделы приложения, открытые пользователю: вкладки, пункты веера, уведомления и профиль (там же настройки).
List<Hit> sectionHits(NavLayout nav, AppLocalizations l10n) {
  final entries = <FanEntry>[
    for (final t in nav.tabs)
      if (t != AppTab.home) FanEntry(location: t.path, icon: t.icon, color: const Color(0x00000000), label: t.label),
    ...nav.fan,
    FanEntry(location: AppModule.notifications.path, icon: AppModule.notifications.icon, color: AppModule.notifications.color, label: AppModule.notifications.label),
    FanEntry(location: AppTab.profile.path, icon: AppTab.profile.icon, color: const Color(0x00000000), label: AppTab.profile.label),
  ];
  final seen = <String>{};
  return [
    for (final e in entries)
      if (seen.add(e.location)) (key: e.location, route: e.location, title: e.label(l10n), hint: null, icon: e.icon),
  ];
}

/// Поиск с главной — как мобильный поиск веба: разделы и отделы — сразу, задачи и сотрудники — с сервера по мере ввода (от двух букв,
/// через 250 мс; без права на раздел его результатов нет). Ответ на прежний запрос не перетирает новый.
class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _input = TextEditingController();
  Timer? _timer;
  CancelToken? _cancel;
  List<Hit> _tasks = const [];
  List<Hit> _people = const [];
  bool _loading = false;
  String _q = '';

  @override
  void dispose() {
    _timer?.cancel();
    _cancel?.cancel();
    _input.dispose();
    super.dispose();
  }

  void _changed(String v) {
    final q = v.trim();
    setState(() => _q = q);
    _timer?.cancel();
    _cancel?.cancel();
    if (q.length < 2) {
      setState(() {
        _tasks = const [];
        _people = const [];
        _loading = false;
      });
      return;
    }
    setState(() => _loading = true);
    _timer = Timer(const Duration(milliseconds: 250), () => _search(q));
  }

  Future<void> _search(String q) async {
    final cancel = _cancel = CancelToken();
    final api = ref.read(apiClientProvider);
    final perms = ref.read(navLayoutProvider).permissions;
    Future<List<Hit>> tasks() async {
      if (!Access.canOpen(Access.tasks, perms)) return const [];
      try {
        return await api.get('/api/work/tasks', (j) {
          final items = ((j! as Map<String, Object?>)['items'] as List<Object?>? ?? const []).cast<Map<String, Object?>>();
          return [
            for (final t in items)
              (key: t['id']! as String, route: '/tasks/${t['id']}', title: t['title'] as String? ?? '', hint: t['departmentName'] as String?, icon: LucideIcons.clipboardList),
          ];
        }, query: {'scope': 'all', 'search': q, 'pageSize': 5}, cancel: cancel);
      } on Object {
        return const [];
      }
    }

    Future<List<Hit>> people() async {
      if (!Access.canOpen(Access.hr, perms)) return const [];
      try {
        return await api.get('/api/hr/employees', (j) {
          final items = ((j! as Map<String, Object?>)['items'] as List<Object?>? ?? const []).cast<Map<String, Object?>>();
          return [
            for (final p in items)
              (
                key: p['id']! as String,
                route: '/home/hr/employees/${p['id']}',
                title: displayName(p['fullName'] as String? ?? ''),
                hint: p['job'] as String? ?? p['division'] as String?,
                icon: LucideIcons.userRound,
              ),
          ];
        }, query: {'search': q, 'pageSize': 5}, cancel: cancel);
      } on Object {
        return const [];
      }
    }

    final (t, p) = await (tasks(), people()).wait;
    if (!mounted || cancel.isCancelled || q != _q) return;
    setState(() {
      _tasks = t;
      _people = p;
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final nav = ref.watch(navLayoutProvider);
    final me = ref.watch(currentProfileProvider);
    final q = _q.toLowerCase();
    bool match(String text) => q.isEmpty || text.toLowerCase().contains(q);
    final sections = sectionHits(nav, l10n).where((h) => match(h.title)).toList();
    final departments = q.isEmpty
        ? const <Hit>[]
        : [
            for (final code in departmentCodes)
              if (Access.departmentOpen(code, nav.permissions, me?.departments ?? const []) && match(departmentName(l10n, code)))
                if (departmentRoute(code, me, nav.permissions) case final route?)
                  (key: code, route: route, title: departmentName(l10n, code), hint: null, icon: LucideIcons.building2),
          ];
    final groups = [
      (l10n.srSections, sections),
      (l10n.srDepartments, departments),
      (l10n.srTasks, _tasks),
      (l10n.srPeople, _people),
    ].where((g) => g.$2.isNotEmpty).toList();
    final top = MediaQuery.paddingOf(context).top;

    return ObBackground(
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Column(children: [
          Container(
            padding: EdgeInsets.fromLTRB(16, top + 8, 8, 10),
            decoration: BoxDecoration(border: Border(bottom: BorderSide(color: c.line))),
            child: Row(children: [
              Expanded(
                child: Container(
                  height: 44,
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  decoration: BoxDecoration(color: c.muted, borderRadius: BorderRadius.circular(ObRadius.md), border: Border.all(color: c.line)),
                  child: Row(children: [
                    Icon(LucideIcons.search, size: 16, color: c.ink3),
                    const SizedBox(width: 8),
                    Expanded(
                      child: TextField(
                        controller: _input,
                        autofocus: true,
                        textInputAction: TextInputAction.search,
                        onChanged: _changed,
                        style: TextStyle(fontSize: 15, color: c.ink),
                        decoration: InputDecoration(
                          hintText: l10n.srHint,
                          border: InputBorder.none,
                          enabledBorder: InputBorder.none,
                          focusedBorder: InputBorder.none,
                          filled: false,
                          isDense: true,
                          contentPadding: EdgeInsets.zero,
                        ),
                      ),
                    ),
                    if (_loading)
                      const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    else if (_input.text.isNotEmpty)
                      Pressable(
                        semanticLabel: l10n.srClear,
                        onTap: () {
                          _input.clear();
                          _changed('');
                        },
                        child: Icon(LucideIcons.x, size: 16, color: c.ink3),
                      ),
                  ]),
                ),
              ),
              TextButton(
                onPressed: () => context.canPop() ? context.pop() : context.go('/home'),
                child: Text(l10n.actionCancel, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: c.accentStrong)),
              ),
            ]),
          ),
          Expanded(
            child: ListView(
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              // Нижний отступ включает панель вкладок (extendBody); с клавиатурой панель скрыта.
              padding: EdgeInsets.fromLTRB(8, 0, 8, MediaQuery.paddingOf(context).bottom + 24),
              children: [
                for (final g in groups) ...[
                  Padding(
                    padding: const EdgeInsets.fromLTRB(12, 14, 12, 4),
                    child: Text(g.$1.toUpperCase(), style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 0.6, color: c.ink3)),
                  ),
                  for (final h in g.$2)
                    Pressable(
                      onTap: () {
                        FocusScope.of(context).unfocus();
                        context.go(h.route);
                      },
                      child: Container(
                        constraints: const BoxConstraints(minHeight: 52),
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        child: Row(children: [
                          Container(
                            width: 36,
                            height: 36,
                            decoration: BoxDecoration(color: c.muted, borderRadius: BorderRadius.circular(ObRadius.sm)),
                            child: Icon(h.icon, size: 18, color: c.ink2),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              Text(h.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 15, color: c.ink)),
                              if (h.hint != null) Text(h.hint!, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                            ]),
                          ),
                          Icon(LucideIcons.chevronRight, size: 16, color: c.ink3),
                        ]),
                      ),
                    ),
                ],
                if (_q.length >= 2 && !_loading && groups.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 40),
                    child: Text(l10n.srNothing, textAlign: TextAlign.center, style: TextStyle(fontSize: 14, color: c.ink3)),
                  ),
              ],
            ),
          ),
        ]),
      ),
    );
  }
}
