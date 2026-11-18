import 'package:flutter/material.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/cache/cached_block.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/ob_card.dart';
import '../../../../shared/widgets/section_header.dart';
import '../../../../shared/widgets/skeleton.dart';
import '../../../../shared/widgets/states.dart';
import '../../../../shared/widgets/sync_label.dart';

/// Блок Dashboard в одном виде для всех состояний:
/// - первая загрузка — заготовка;
/// - нет права (403) — блока нет;
/// - ошибка без данных — текст и «Повторить» внутри блока (остальные блоки работают);
/// - данные — содержимое; если они из кэша или не обновились — время синхронизации и причина.
class BlockSection<T> extends StatelessWidget {
  const BlockSection({
    super.key,
    required this.title,
    required this.state,
    required this.builder,
    required this.onRetry,
    this.onMore,
    this.moreLabel,
    this.skeletonHeight = 140,
    this.card = true,
  });

  final String title;
  final BlockState<T> state;
  final Widget Function(BuildContext context, T data) builder;
  final VoidCallback onRetry;
  final VoidCallback? onMore;
  final String? moreLabel;
  final double skeletonHeight;

  /// Обернуть содержимое в карточку (плитки KPI рисуют свои карточки сами).
  final bool card;

  @override
  Widget build(BuildContext context) {
    if (state.forbidden && state.data == null) return const SizedBox.shrink();
    final data = state.data;
    final Widget body;
    if (data != null) {
      final content = builder(context, data);
      body = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (card) ObCard(child: content) else content,
          if (state.stale) _StaleNote(state: state, onRetry: onRetry),
        ],
      );
    } else if (state.failure != null) {
      body = ObCard(child: ErrorView(failure: state.failure!, onRetry: onRetry, compact: true));
    } else {
      body = SkeletonCard(height: skeletonHeight);
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: title, onMore: data == null ? null : onMore, moreLabel: moreLabel),
        AnimatedSwitcher(
          duration: const Duration(milliseconds: 200),
          child: KeyedSubtree(key: ValueKey(data == null ? (state.failure == null ? 0 : 1) : 2), child: body),
        ),
      ],
    );
  }
}

/// Под блоком со старыми данными: когда обновлено и почему не обновилось (с «Повторить»).
class _StaleNote extends StatelessWidget {
  const _StaleNote({required this.state, required this.onRetry});

  final BlockState<Object?> state;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final failure = state.failure;
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 6, 0, 0),
      child: Row(
        children: [
          if (failure != null) ...[
            Icon(Icons.cloud_off_rounded, size: 14, color: context.ob.warn),
            const SizedBox(width: 6),
          ],
          Expanded(
            child: failure == null
                ? SyncLabel(at: state.syncedAt, refreshing: state.loading)
                : Text(
                    [
                      failure.describe(context.l10n),
                      if (state.syncedAt != null) context.fmt.synced(state.syncedAt!, DateTime.now()),
                    ].join(' · '),
                    style: context.text.bodySmall,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
          ),
          if (failure != null && failure.canRetry && !state.loading)
            TextButton(
              onPressed: onRetry,
              style: TextButton.styleFrom(visualDensity: VisualDensity.compact, padding: const EdgeInsets.symmetric(horizontal: ObSpace.sm)),
              child: Text(context.l10n.actionRetry),
            ),
        ],
      ),
    );
  }
}
