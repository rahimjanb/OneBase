import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_button.dart';
import 'brand_mark.dart';

/// Проверка сохранённого входа при запуске. Если проверить не удалось и сохранённого профиля нет (первый запуск без сети) —
/// причина, «Повторить» и «Выйти»: токен не удаляется сам, чтобы не создавать лишний сеанс повторным входом.
class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final l10n = context.l10n;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(ObSpace.xxl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const BrandMark(size: 88),
                const SizedBox(height: ObSpace.xxl),
                if (auth is AuthRestoreFailed) ...[
                  Text(l10n.restoreFailedTitle, style: context.text.titleMedium, textAlign: TextAlign.center),
                  const SizedBox(height: ObSpace.sm),
                  Text(auth.failure.describe(l10n), style: context.text.bodyMedium, textAlign: TextAlign.center),
                  const SizedBox(height: ObSpace.xl),
                  ObButton(
                    label: l10n.actionRetry,
                    icon: Icons.refresh_rounded,
                    onPressed: () => ref.read(authControllerProvider.notifier).restore(),
                  ),
                  const SizedBox(height: ObSpace.sm),
                  ObButton.text(label: l10n.logout, onPressed: () => ref.read(authControllerProvider.notifier).logout()),
                ] else ...[
                  const SizedBox.square(dimension: 24, child: CircularProgressIndicator(strokeWidth: 2.5)),
                  const SizedBox(height: ObSpace.md),
                  Text(l10n.splashChecking, style: context.text.bodyMedium),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
