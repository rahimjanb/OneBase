import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Поле ввода OneBase: подпись над полем, заливка, текст ошибки под полем. Для пароля — кнопка «показать».
class ObTextField extends StatefulWidget {
  const ObTextField({
    super.key,
    required this.label,
    this.controller,
    this.hint,
    this.error,
    this.obscure = false,
    this.showLabel,
    this.hideLabel,
    this.keyboardType,
    this.textInputAction,
    this.autofillHints,
    this.onSubmitted,
    this.onChanged,
    this.enabled = true,
    this.autofocus = false,
    this.prefixIcon,
  });

  final String label;
  final TextEditingController? controller;
  final String? hint;
  final String? error;
  final bool obscure;

  /// Подписи кнопки «показать / скрыть пароль» (для экранного диктора).
  final String? showLabel;
  final String? hideLabel;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final Iterable<String>? autofillHints;
  final ValueChanged<String>? onSubmitted;
  final ValueChanged<String>? onChanged;
  final bool enabled;
  final bool autofocus;
  final IconData? prefixIcon;

  @override
  State<ObTextField> createState() => _ObTextFieldState();
}

class _ObTextFieldState extends State<ObTextField> {
  late bool _hidden = widget.obscure;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(left: 4, bottom: 6),
          child: Text(widget.label, style: context.text.labelMedium),
        ),
        TextField(
          controller: widget.controller,
          obscureText: _hidden,
          enableSuggestions: !widget.obscure,
          autocorrect: !widget.obscure,
          keyboardType: widget.keyboardType,
          textInputAction: widget.textInputAction,
          autofillHints: widget.autofillHints,
          onSubmitted: widget.onSubmitted,
          onChanged: widget.onChanged,
          enabled: widget.enabled,
          autofocus: widget.autofocus,
          style: context.text.bodyLarge,
          decoration: InputDecoration(
            hintText: widget.hint,
            errorText: widget.error,
            errorMaxLines: 3,
            prefixIcon: widget.prefixIcon == null ? null : Icon(widget.prefixIcon, color: c.ink3, size: 20),
            suffixIcon: widget.obscure
                ? IconButton(
                    tooltip: _hidden ? widget.showLabel : widget.hideLabel,
                    icon: Icon(_hidden ? Icons.visibility_outlined : Icons.visibility_off_outlined, color: c.ink3, size: 20),
                    onPressed: () => setState(() => _hidden = !_hidden),
                  )
                : null,
          ),
        ),
        const SizedBox(height: ObSpace.xs),
      ],
    );
  }
}
