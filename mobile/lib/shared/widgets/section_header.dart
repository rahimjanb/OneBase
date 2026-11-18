import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Заголовок раздела экрана: название, пояснение справа или кнопка «Все».
class SectionHeader extends StatelessWidget {
  const SectionHeader({super.key, required this.title, this.trailing, this.onMore, this.moreLabel});

  final String title;
  final Widget? trailing;
  final VoidCallback? onMore;
  final String? moreLabel;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(4, ObSpace.xl, 0, ObSpace.sm),
        child: Row(
          children: [
            Expanded(
              child: Semantics(header: true, child: Text(title, style: context.text.titleLarge)),
            ),
            ?trailing,
            if (onMore != null)
              TextButton(
                onPressed: onMore,
                style: TextButton.styleFrom(visualDensity: VisualDensity.compact),
                child: Text(moreLabel ?? context.l10n.actionAll),
              ),
          ],
        ),
      );
}
