import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../extensions/context_ext.dart';

/// Вкладки-сегменты как в «Динамике» веба: дорожка цветом фона с рамкой, выбранная — второй поверхностью с рамкой.
class ObSegmented<T> extends StatelessWidget {
  const ObSegmented({super.key, required this.values, required this.selected, required this.label, required this.onChanged});

  final List<T> values;
  final T selected;
  final String Function(T) label;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(color: c.page, borderRadius: BorderRadius.circular(12), border: Border.all(color: c.line)),
      child: Row(
        children: [
          for (final v in values)
            Expanded(
              child: Semantics(
                selected: v == selected,
                button: true,
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: () {
                    if (v == selected) return;
                    HapticFeedback.selectionClick();
                    onChanged(v);
                  },
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 150),
                    height: 34,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: v == selected ? c.muted : Colors.transparent,
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(color: v == selected ? c.lineStrong : Colors.transparent),
                    ),
                    child: Text(
                      label(v),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: v == selected ? c.ink : c.ink3),
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
