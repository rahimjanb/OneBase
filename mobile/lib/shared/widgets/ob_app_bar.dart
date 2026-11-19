import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../app/shell/bottom_nav.dart';
import '../../app/theme/decor.dart';
import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';
import 'pills.dart';

/// Шапка экрана раздела: крупный заголовок (26/700, как h1 веба), при прокрутке сжимается в строку с размытием фона. Слева —
/// круглая кнопка «назад» (если есть куда), справа — действия.
class ObSliverAppBar extends StatelessWidget {
  const ObSliverAppBar({super.key, required this.title, this.subtitle, this.actions, this.leading, this.bottom, this.showBack});

  final String title;
  final Widget? subtitle;
  final List<Widget>? actions;
  final Widget? leading;

  /// Вкладки или фильтры под заголовком (закрепляются вместе с шапкой).
  final PreferredSizeWidget? bottom;

  /// Показать «назад» (по умолчанию — если стек экрана не пуст).
  final bool? showBack;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final canPop = showBack ?? GoRouter.of(context).canPop();
    final lead = leading ??
        (canPop
            ? Center(
                child: HeaderButton(
                  icon: LucideIcons.chevronLeft,
                  semanticLabel: MaterialLocalizations.of(context).backButtonTooltip,
                  onTap: () => context.pop(),
                ),
              )
            : null);
    final bottomHeight = bottom?.preferredSize.height ?? 0;
    return SliverAppBar(
      pinned: true,
      automaticallyImplyLeading: false,
      leading: lead,
      leadingWidth: lead == null ? 0 : 60,
      titleSpacing: lead == null ? ObSpace.page : 0,
      expandedHeight: (subtitle == null ? 108 : 128) + bottomHeight,
      backgroundColor: Colors.transparent,
      surfaceTintColor: Colors.transparent,
      actions: [...?actions, const SizedBox(width: 8)],
      bottom: bottom,
      flexibleSpace: LayoutBuilder(
        builder: (context, box) {
          final top = MediaQuery.paddingOf(context).top;
          final collapsed = box.maxHeight <= kToolbarHeight + top + bottomHeight + 1;
          return ClipRect(
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: collapsed ? 18 : 0, sigmaY: collapsed ? 18 : 0),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: collapsed ? c.page.withValues(alpha: 0.85) : Colors.transparent,
                  border: collapsed ? Border(bottom: BorderSide(color: c.line)) : null,
                ),
                child: FlexibleSpaceBar(
                  centerTitle: false,
                  titlePadding: EdgeInsetsDirectional.only(
                    start: collapsed && lead != null ? 64 : ObSpace.page + 4,
                    bottom: bottomHeight + (collapsed ? 16 : (subtitle == null ? 12 : 32)),
                    end: 56,
                  ),
                  expandedTitleScale: 26 / 17,
                  title: Semantics(
                    header: true,
                    child: Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: context.text.titleLarge!.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.3),
                    ),
                  ),
                  background: subtitle == null
                      ? null
                      : Align(
                          alignment: Alignment.bottomLeft,
                          child: Padding(
                            padding: EdgeInsets.fromLTRB(ObSpace.page + 4, 0, ObSpace.page, bottomHeight + 10),
                            child: DefaultTextStyle(style: context.text.bodySmall!.copyWith(fontSize: 13), child: subtitle!),
                          ),
                        ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

/// Экран раздела по вебу: фон со свечением, шапка с крупным заголовком, содержимое и отступ под нижнюю панель; [onRefresh] —
/// «потянуть, чтобы обновить».
class ObPage extends StatelessWidget {
  const ObPage({
    super.key,
    required this.title,
    required this.slivers,
    this.subtitle,
    this.actions,
    this.onRefresh,
    this.bottom,
    this.floatingActionButton,
    this.showBack,
    this.controller,
    this.padding = true,
  });

  final String title;
  final Widget? subtitle;
  final List<Widget>? actions;
  final List<Widget> slivers;
  final Future<void> Function()? onRefresh;
  final PreferredSizeWidget? bottom;
  final Widget? floatingActionButton;
  final bool? showBack;
  final ScrollController? controller;

  /// Поля экрана слева и справа у содержимого.
  final bool padding;

  @override
  Widget build(BuildContext context) {
    Widget scroll = CustomScrollView(
      controller: controller,
      physics: const AlwaysScrollableScrollPhysics(),
      slivers: [
        ObSliverAppBar(title: title, subtitle: subtitle, actions: actions, bottom: bottom, showBack: showBack),
        if (padding)
          for (final s in slivers) SliverPadding(padding: const EdgeInsets.symmetric(horizontal: ObSpace.page), sliver: s)
        else
          ...slivers,
        SliverToBoxAdapter(child: SizedBox(height: navContentInset(context))),
      ],
    );
    if (onRefresh != null) {
      scroll = RefreshIndicator.adaptive(
        edgeOffset: MediaQuery.paddingOf(context).top + kToolbarHeight + (bottom?.preferredSize.height ?? 0),
        onRefresh: onRefresh!,
        child: scroll,
      );
    }
    return ObBackground(
      child: Scaffold(
        backgroundColor: Colors.transparent,
        floatingActionButton: floatingActionButton == null
            ? null
            : Padding(padding: EdgeInsets.only(bottom: navBarHeight + navBottomInset(context) - 8), child: floatingActionButton),
        body: scroll,
      ),
    );
  }
}

/// Отступ между секциями экрана раздела.
const sliverGap = SliverToBoxAdapter(child: SizedBox(height: ObSpace.block));

/// Обёртка обычного виджета в sliver.
class SliverBox extends StatelessWidget {
  const SliverBox(this.child, {super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) => SliverToBoxAdapter(child: child);
}

/// Фон раздела без шапки (экраны с собственной вёрсткой: чат консультанта).
class ObPlainPage extends StatelessWidget {
  const ObPlainPage({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => ObBackground(child: Scaffold(backgroundColor: Colors.transparent, body: child));
}

/// Карточка-обёртка для sliver-списков с рамкой (строки во всю ширину). Используется разделами.
BoxDecoration listDecoration(ObColors c) => cardDecoration(c);
