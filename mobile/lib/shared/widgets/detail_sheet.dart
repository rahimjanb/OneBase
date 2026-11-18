import 'package:flutter/material.dart';

import '../../app/theme/theme.dart';
import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Открыть детализацию снизу: лист на 85% экрана, тянется до полного, прокручивается.
Future<void> showDetailSheet(
  BuildContext context, {
  required String title,
  String? subtitle,
  required List<Widget> Function(BuildContext context) children,
}) =>
    showModalBottomSheet<void>(
      context: context,
      useRootNavigator: true,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (context) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.85,
        minChildSize: 0.5,
        maxChildSize: 1,
        builder: (context, controller) => ListView(
          controller: controller,
          padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, ObSpace.xxl),
          children: [
            Semantics(header: true, child: Text(title, style: context.text.headlineSmall)),
            if (subtitle != null) ...[
              const SizedBox(height: 4),
              Text(subtitle, style: context.text.bodyMedium),
            ],
            const SizedBox(height: ObSpace.lg),
            ...children(context),
          ],
        ),
      ),
    );

/// Строка детализации: подпись слева, значение справа (цифры одинаковой ширины), пояснение под подписью.
class DetailRow extends StatelessWidget {
  const DetailRow({super.key, required this.label, required this.value, this.hint, this.valueColor, this.trailing});

  final String label;
  final String value;
  final String? hint;
  final Color? valueColor;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => MergeSemantics(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 10),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(label, style: context.text.bodyLarge),
                    if (hint != null) Text(hint!, style: context.text.bodySmall),
                  ],
                ),
              ),
              const SizedBox(width: ObSpace.md),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    value,
                    style: context.text.titleMedium!.copyWith(fontFeatures: tabularFigures, color: valueColor),
                  ),
                  ?trailing,
                ],
              ),
            ],
          ),
        ),
      );
}

/// Группа строк в карточке с разделителями.
class DetailGroup extends StatelessWidget {
  const DetailGroup({super.key, this.title, required this.children});

  final String? title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Padding(
      padding: const EdgeInsets.only(bottom: ObSpace.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null)
            Padding(
              padding: const EdgeInsets.only(left: 4, bottom: ObSpace.sm),
              child: Text(title!, style: context.text.titleSmall!.copyWith(color: c.ink2)),
            ),
          DecoratedBox(
            decoration: BoxDecoration(
              color: c.muted.withValues(alpha: 0.6),
              borderRadius: BorderRadius.circular(ObRadius.md),
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: ObSpace.lg, vertical: 2),
              child: Column(
                children: [
                  for (var i = 0; i < children.length; i++) ...[
                    if (i > 0) Divider(height: 1, color: c.line),
                    children[i],
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
