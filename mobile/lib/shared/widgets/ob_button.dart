import 'package:flutter/material.dart';

enum ObButtonKind { primary, secondary, text }

/// Кнопка OneBase. [loading] — крутилка вместо иконки, нажатие отключено (повторный вход, отправка). [expanded] — во всю ширину.
class ObButton extends StatelessWidget {
  const ObButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.kind = ObButtonKind.primary,
    this.loading = false,
    this.expanded = false,
  });

  const ObButton.secondary({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.loading = false,
    this.expanded = false,
  }) : kind = ObButtonKind.secondary;

  const ObButton.text({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.loading = false,
    this.expanded = false,
  }) : kind = ObButtonKind.text;

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final ObButtonKind kind;
  final bool loading;
  final bool expanded;

  @override
  Widget build(BuildContext context) {
    final onTap = loading ? null : onPressed;
    final fg = kind == ObButtonKind.primary ? Colors.white : Theme.of(context).colorScheme.primary;
    final Widget? lead = loading
        ? SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2, color: fg))
        : icon == null
            ? null
            : Icon(icon, size: 20);
    final child = Text(label, maxLines: 1, overflow: TextOverflow.ellipsis);

    final Widget button = switch (kind) {
      ObButtonKind.primary =>
        lead == null ? FilledButton(onPressed: onTap, child: child) : FilledButton.icon(onPressed: onTap, icon: lead, label: child),
      ObButtonKind.secondary => lead == null
          ? OutlinedButton(onPressed: onTap, child: child)
          : OutlinedButton.icon(onPressed: onTap, icon: lead, label: child),
      ObButtonKind.text =>
        lead == null ? TextButton(onPressed: onTap, child: child) : TextButton.icon(onPressed: onTap, icon: lead, label: child),
    };
    return expanded ? SizedBox(width: double.infinity, child: button) : button;
  }
}
