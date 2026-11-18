import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/core_providers.dart';
import '../extensions/context_ext.dart';

/// «Обновлено 5 мин назад» — время последней успешной синхронизации. Пересчитывается раз в 30 секунд, пока виден.
class SyncLabel extends ConsumerStatefulWidget {
  const SyncLabel({super.key, required this.at, this.refreshing = false, this.style});

  final DateTime? at;
  final bool refreshing;
  final TextStyle? style;

  @override
  ConsumerState<SyncLabel> createState() => _SyncLabelState();
}

class _SyncLabelState extends ConsumerState<SyncLabel> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final at = widget.at;
    if (at == null && !widget.refreshing) return const SizedBox.shrink();
    final style = widget.style ?? context.text.bodySmall;
    final now = ref.watch(clockProvider)();
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (widget.refreshing) ...[
          SizedBox.square(dimension: 10, child: CircularProgressIndicator(strokeWidth: 1.5, color: style?.color)),
          const SizedBox(width: 6),
        ],
        if (at != null) Flexible(child: Text(context.fmt.synced(at, now), style: style, overflow: TextOverflow.ellipsis)),
      ],
    );
  }
}
