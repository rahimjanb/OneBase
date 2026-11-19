import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/auth/auth_controller.dart';
import '../core/core_providers.dart';
import '../core/storage/preferences.dart';
import '../core/synchronization/outbox.dart';
import '../l10n/gen/app_localizations.dart';
import '../shared/extensions/context_ext.dart';
import '../shared/widgets/states.dart';
import 'router.dart';
import 'theme/theme.dart';

/// Корень приложения: темы, язык, роутер. Крупный системный шрифт поддерживается до 1,4× — дальше вёрстка блоков не помещается.
class OneBaseApp extends ConsumerWidget {
  const OneBaseApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final config = ref.watch(appConfigProvider);
    final common = (
      theme: ObTheme.light(),
      darkTheme: ObTheme.dark(),
      themeMode: ref.watch(themeModeProvider),
      locale: ref.watch(localeProvider),
    );
    Locale resolve(Locale? device, Iterable<Locale> _) => resolveLocale(common.locale, device);
    Widget scale(BuildContext context, Widget? child) => MediaQuery.withClampedTextScaling(
          maxScaleFactor: 1.4,
          child: child ?? const SizedBox.shrink(),
        );

    if (!config.isConfigured) {
      return MaterialApp(
        debugShowCheckedModeBanner: false,
        onGenerateTitle: (c) => c.l10n.appTitle,
        theme: common.theme,
        darkTheme: common.darkTheme,
        themeMode: common.themeMode,
        locale: common.locale,
        supportedLocales: supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        localeResolutionCallback: resolve,
        builder: scale,
        home: const _NotConfigured(),
      );
    }

    return MaterialApp.router(
      debugShowCheckedModeBanner: false,
      onGenerateTitle: (c) => c.l10n.appTitle,
      theme: common.theme,
      darkTheme: common.darkTheme,
      themeMode: common.themeMode,
      locale: common.locale,
      supportedLocales: supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      localeResolutionCallback: resolve,
      routerConfig: ref.watch(routerProvider),
      builder: (context, child) => _SessionLifecycle(child: scale(context, child)),
    );
  }
}

/// Возврат приложения из фона → перечитать профиль и права (не чаще раза в минуту): у сервера права меняются без повторного входа;
/// отправить операции из очереди.
class _SessionLifecycle extends ConsumerStatefulWidget {
  const _SessionLifecycle({required this.child});

  final Widget child;

  @override
  ConsumerState<_SessionLifecycle> createState() => _SessionLifecycleState();
}

class _SessionLifecycleState extends ConsumerState<_SessionLifecycle> {
  late final AppLifecycleListener _listener = AppLifecycleListener(
    onResume: () {
      // Возврат из фона: права и профиль (не чаще раза в минуту) и отправка операций, ждавших сети.
      ref.read(authControllerProvider.notifier).refreshProfile();
      if (ref.read(currentProfileProvider) != null) ref.read(outboxProvider).flush();
    },
  );

  @override
  void initState() {
    super.initState();
    _listener;
  }

  @override
  void dispose() {
    _listener.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}

class _NotConfigured extends StatelessWidget {
  const _NotConfigured();

  @override
  Widget build(BuildContext context) => Scaffold(
        body: EmptyView(icon: Icons.dns_outlined, title: context.l10n.errorNotConfigured),
      );
}
