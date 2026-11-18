import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../core/errors/app_failure.dart';
import '../extensions/context_ext.dart';
import 'ob_button.dart';

/// Загрузка: крутилка с подписью. Для блоков с известной формой лучше [Skeleton].
class LoadingView extends StatelessWidget {
  const LoadingView({super.key, this.label});

  final String? label;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox.square(dimension: 28, child: CircularProgressIndicator(strokeWidth: 2.5)),
            const SizedBox(height: ObSpace.md),
            Text(label ?? context.l10n.stateLoading, style: context.text.bodyMedium),
          ],
        ),
      );
}

/// Пусто: иконка, заголовок, пояснение и, если есть, действие. [compact] — внутри карточки блока.
class EmptyView extends StatelessWidget {
  const EmptyView({
    super.key,
    this.icon = Icons.inbox_outlined,
    required this.title,
    this.message,
    this.action,
    this.compact = false,
  });

  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final content = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: compact ? CrossAxisAlignment.start : CrossAxisAlignment.center,
      children: [
        if (!compact) ...[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(color: c.muted, borderRadius: BorderRadius.circular(ObRadius.lg)),
            child: Icon(icon, color: c.ink3, size: 28),
          ),
          const SizedBox(height: ObSpace.lg),
        ],
        Text(
          title,
          style: compact ? context.text.titleSmall : context.text.titleMedium,
          textAlign: compact ? TextAlign.start : TextAlign.center,
        ),
        if (message != null) ...[
          const SizedBox(height: ObSpace.xs),
          Text(message!, style: context.text.bodyMedium, textAlign: compact ? TextAlign.start : TextAlign.center),
        ],
        if (action != null) ...[const SizedBox(height: ObSpace.lg), action!],
      ],
    );
    if (compact) return content;
    return Center(child: Padding(padding: const EdgeInsets.all(ObSpace.xxl), child: content));
  }
}

/// Ошибка: понятный текст и «Повторить» (если повтор имеет смысл). [compact] — внутри карточки блока, остальные блоки работают.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.failure, this.onRetry, this.compact = false});

  final AppFailure failure;
  final VoidCallback? onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final retry = onRetry != null && failure.canRetry
        ? ObButton.text(label: context.l10n.actionRetry, icon: Icons.refresh_rounded, onPressed: onRetry)
        : null;
    if (compact) {
      return Row(
        children: [
          Icon(Icons.error_outline_rounded, color: c.bad, size: 20),
          const SizedBox(width: ObSpace.sm),
          Expanded(child: Text(failure.describe(context.l10n), style: context.text.bodyMedium)),
          ?retry,
        ],
      );
    }
    return EmptyView(
      icon: Icons.cloud_off_rounded,
      title: failure.describe(context.l10n),
      action: retry,
    );
  }
}
