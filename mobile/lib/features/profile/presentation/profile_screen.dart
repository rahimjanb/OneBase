import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/core_providers.dart';
import '../../../core/storage/preferences.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/section_header.dart';

/// Профиль: оформление (тема), язык, о приложении. Данные пользователя и выход — этап 2, остальные настройки — этап 12.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    return Scaffold(
      body: CustomScrollView(
        slivers: [
          ObSliverAppBar(title: l10n.navProfile),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, ObSpace.xxl),
            sliver: SliverList.list(
              children: const [
                AppearanceSection(),
                LanguageSection(),
                AboutSection(),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Тема: как в системе / светлая / тёмная.
class AppearanceSection extends ConsumerWidget {
  const AppearanceSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final mode = ref.watch(themeModeProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAppearance),
        SegmentedButton<ThemeMode>(
          showSelectedIcon: false,
          segments: [
            ButtonSegment(value: ThemeMode.system, label: Text(l10n.themeSystem), icon: const Icon(Icons.brightness_auto_outlined)),
            ButtonSegment(value: ThemeMode.light, label: Text(l10n.themeLight), icon: const Icon(Icons.light_mode_outlined)),
            ButtonSegment(value: ThemeMode.dark, label: Text(l10n.themeDark), icon: const Icon(Icons.dark_mode_outlined)),
          ],
          selected: {mode},
          onSelectionChanged: (s) => ref.read(themeModeProvider.notifier).set(s.first),
        ),
      ],
    );
  }
}

/// Язык интерфейса: русский или узбекский; сервер отвечает на нём же.
class LanguageSection extends ConsumerWidget {
  const LanguageSection({super.key});

  static const _names = {'ru': 'Русский', 'uz': "O'zbekcha"};

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final current = effectiveLanguage(ref.watch(localeProvider));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: context.l10n.language),
        ObCard(
          padding: const EdgeInsets.symmetric(vertical: ObSpace.xs),
          child: RadioGroup<String>(
            groupValue: current,
            onChanged: (code) {
              if (code != null) ref.read(localeProvider.notifier).set(Locale(code));
            },
            child: Column(
              children: [
                for (final l in supportedLocales)
                  RadioListTile<String>(
                    value: l.languageCode,
                    title: Text(_names[l.languageCode] ?? l.languageCode),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// Версия приложения и сервер, к которому оно подключено.
class AboutSection extends ConsumerWidget {
  const AboutSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final config = ref.watch(appConfigProvider);
    final l10n = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAbout),
        ObCard(
          padding: const EdgeInsets.symmetric(vertical: ObSpace.xs),
          child: Column(
            children: [
              ListTile(
                leading: const Icon(Icons.info_outline_rounded),
                title: Text(l10n.profileVersion(config.appVersion)),
                subtitle: Text(config.env.name),
              ),
              ListTile(
                leading: const Icon(Icons.dns_outlined),
                title: Text(l10n.profileServer),
                subtitle: Text(config.apiBaseUrl.isEmpty ? l10n.errorNotConfigured : config.apiBaseUrl),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
