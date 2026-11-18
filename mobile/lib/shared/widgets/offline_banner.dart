import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/tokens.dart';
import '../../core/network/connectivity.dart';
import '../extensions/context_ext.dart';

/// Плашка «Нет сети — показаны сохранённые данные». Появляется и исчезает плавно; пока статус сети неизвестен — скрыта.
class OfflineBanner extends ConsumerWidget {
  const OfflineBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final offline = ref.watch(isOnlineProvider).value == false;
    final c = context.ob;
    return AnimatedSize(
      duration: const Duration(milliseconds: 220),
      curve: Curves.easeOut,
      child: offline
          ? Semantics(
              liveRegion: true,
              child: Container(
                width: double.infinity,
                margin: const EdgeInsets.fromLTRB(ObSpace.page, ObSpace.sm, ObSpace.page, 0),
                padding: const EdgeInsets.symmetric(horizontal: ObSpace.md, vertical: 10),
                decoration: BoxDecoration(color: c.warnSoft, borderRadius: BorderRadius.circular(ObRadius.md)),
                child: Row(
                  children: [
                    Icon(Icons.wifi_off_rounded, size: 18, color: c.warn),
                    const SizedBox(width: ObSpace.sm),
                    Expanded(child: Text(context.l10n.offlineBanner, style: context.text.labelLarge)),
                  ],
                ),
              ),
            )
          : const SizedBox(width: double.infinity),
    );
  }
}
