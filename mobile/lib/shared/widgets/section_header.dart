import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../extensions/context_ext.dart';
import 'pressable.dart';

/// Заголовок секции как в вебе: 17/700, рядом — счётчики, справа — ссылка «Подробнее ›» 13/600 (зона нажатия 44).
class SectionHeader extends StatelessWidget {
  const SectionHeader({super.key, required this.title, this.trailing, this.onMore, this.moreLabel});

  final String title;

  /// Счётчики рядом с названием («● 1 в норме»).
  final Widget? trailing;
  final VoidCallback? onMore;
  final String? moreLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Row(
        children: [
          Expanded(
            child: Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 10,
              runSpacing: 2,
              children: [
                Semantics(
                  header: true,
                  child: Text(title, style: context.text.titleLarge),
                ),
                ?trailing,
              ],
            ),
          ),
          if (onMore != null)
            Pressable(
              onTap: onMore,
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 44),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      moreLabel ?? context.l10n.dynMore,
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong),
                    ),
                    const SizedBox(width: 2),
                    Icon(LucideIcons.chevronRight, size: 15, color: c.accentStrong),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Точка с подписью: «● 1 в норме».
class DotLabel extends StatelessWidget {
  const DotLabel({super.key, required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: 5),
          Text(label, style: TextStyle(fontSize: 12, color: context.ob.ink3)),
        ],
      );
}
