import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Крупный заголовок экрана, который при прокрутке сжимается в обычную шапку (как в iOS). [subtitle] — строка под заголовком
/// (дата, «обновлено …»), видна в развёрнутом виде.
class ObSliverAppBar extends StatelessWidget {
  const ObSliverAppBar({super.key, required this.title, this.subtitle, this.actions, this.leading});

  final String title;
  final Widget? subtitle;
  final List<Widget>? actions;
  final Widget? leading;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return SliverAppBar(
      pinned: true,
      expandedHeight: subtitle == null ? 104 : 124,
      backgroundColor: c.page,
      surfaceTintColor: Colors.transparent,
      leading: leading,
      actions: actions,
      flexibleSpace: LayoutBuilder(
        builder: (context, box) {
          final top = MediaQuery.paddingOf(context).top;
          final collapsed = box.maxHeight <= kToolbarHeight + top + 1;
          return FlexibleSpaceBar(
            centerTitle: false,
            titlePadding: EdgeInsetsDirectional.only(
              start: leading == null || !collapsed ? ObSpace.page : 56,
              bottom: collapsed ? 16 : (subtitle == null ? 12 : 30),
              end: ObSpace.page,
            ),
            expandedTitleScale: 1.55,
            title: Semantics(
              header: true,
              child: Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: context.text.titleLarge!.copyWith(fontWeight: FontWeight.w700),
              ),
            ),
            background: subtitle == null
                ? null
                : Align(
                    alignment: Alignment.bottomLeft,
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, 10),
                      child: DefaultTextStyle(style: context.text.bodySmall!, child: subtitle!),
                    ),
                  ),
          );
        },
      ),
    );
  }
}
